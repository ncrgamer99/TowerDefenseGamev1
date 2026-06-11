using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class MetaHubController : MonoBehaviour
{
    private const string VisualTreeResourcePath = "MetaHubScreen";
    private const string StyleSheetResourcePath = "MetaHubScreen";
    private const float LiveRefreshInterval = 0.5f;
    private static readonly MetaHubTone[] ReusableToneClasses = (MetaHubTone[])Enum.GetValues(typeof(MetaHubTone));

    [Header("References")]
    public GameManager gameManager;
    public VisualTreeAsset screenAsset;
    public StyleSheet styleSheet;
    public PanelSettings panelSettings;

    [Header("Behaviour")]
    public bool useLiveDataWhenAvailable = true;
    public bool useCanvasFallback = true;
    public bool startHidden = true;
    public bool closeWithEscape = true;

    public event Action DetailsRequested;
    public event Action AllGoalsRequested;
    public event Action RunStatisticsRequested;
    public event Action OptionsRequested;
    public event Action MainMenuRequested;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement documentRoot;
    private VisualElement screenRoot;
    private MetaHubData currentData;
    private ChaosUnlockManager owningUnlockManager;
    private bool visualTreeBuilt = false;
    private bool callbacksRegistered = false;
    private bool isVisible = false;
    private bool mainMenuUnlockMode = false;
    private bool runtimeUnlockInfoMode = false;
    private Canvas fallbackCanvas;
    private GameObject fallbackRoot;
    private GameObject fallbackDesignRoot;
    private string lastCanvasFallbackSignature = "";
    private string selectedNavigationId = "overview";
    private TowerRole selectedTowerRole = TowerRole.Basic;
    private string selectedGeneralTreeCategory = "tower";
    private string selectedTowerTreeCategory = "Trunk";
    private ChaosResearchCategory selectedChaosTreeCategory = ChaosResearchCategory.RiskPool;
    private PathTechniqueCategory selectedPathTreeCategory = PathTechniqueCategory.EventPool;
    private EliteHuntCategory selectedEliteTreeCategory = EliteHuntCategory.Contracts;
    private bool showGeneralLoadoutPicker = false;
    private bool showGeneralLoadoutEditor = false;
    private string pendingGeneralLoadoutPromptNodeId = "";
    private bool showAllGoalsOverlay = false;
    private bool showAllRisksOverlay = false;
    private float liveRefreshTimer = 0f;
    private readonly Dictionary<string, Sprite> artSprites = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> panelSprites = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> donutSprites = new Dictionary<string, Sprite>();

    private class TowerMasteryNodeView
    {
        public object definition;
        public string nodeId;
        public string displayName;
        public string effectText;
        public string pathKey;
        public string pathLabel;
        public bool isKeystone;
        public int rank;
        public int maxRank;
        public int nextCost;
        public bool canPurchase;
        public string stateText;
        public MetaHubTone tone;
    }

    private struct RuntimeSummaryItem
    {
        public string artName;
        public string label;
        public string caption;
        public string value;
        public MetaHubTone tone;
    }

    private class ResourceChipView
    {
        public Label icon;
        public Label value;
    }

    private class NavigationRowView
    {
        public Label icon;
        public Label label;
    }

    private class MetricCardView
    {
        public Label title;
        public Label icon;
        public Label value;
        public Label caption;
        public VisualElement progressTrack;
        public VisualElement progressFill;
    }

    private class SideStatRowView
    {
        public Label label;
        public Label value;
        public Label icon;
    }

    private class GoalRowView
    {
        public Label icon;
        public Label title;
        public Label progress;
    }

    private class EffectRowView
    {
        public Label icon;
        public Label title;
        public Label description;
        public Label duration;
    }

    private class KeystoneItemView
    {
        public Label icon;
        public Label level;
        public VisualElement progressTrack;
        public VisualElement progressFill;
    }

    private class RunStatRowView
    {
        public Label label;
        public Label value;
    }

    public bool IsOpen
    {
        get { return isVisible; }
    }

    public static MetaHubController CreateRuntimeInstance(GameManager gameManager = null)
    {
        MetaHubController existing = FindObjectOfType<MetaHubController>();
        if (existing != null)
        {
            if (existing.gameManager == null && gameManager != null)
                existing.gameManager = gameManager;

            return existing;
        }

        GameObject metaHubObject = new GameObject("MetaHubController");
        MetaHubController controller = metaHubObject.AddComponent<MetaHubController>();
        controller.gameManager = gameManager;
        return controller;
    }

    private void Awake()
    {
        EnsureDocument();

        if (currentData == null)
            currentData = MetaHubMockData.Create();

        Bind(currentData);
        SetVisible(!startHidden);
    }

    private void Start()
    {
        ResolveGameManager();

        if (useLiveDataWhenAvailable)
            RefreshData();
    }

    private void Update()
    {
        if (IsOpen && closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
            RequestClose();

        if (IsOpen && useLiveDataWhenAvailable && gameManager != null && ShouldAutoRefreshLiveData())
        {
            liveRefreshTimer -= Time.unscaledDeltaTime;
            if (liveRefreshTimer <= 0f)
            {
                liveRefreshTimer = LiveRefreshInterval;
                RefreshData();
            }
        }
    }

    private bool ShouldAutoRefreshLiveData()
    {
        if (!runtimeUnlockInfoMode)
            return false;

        return !IsPointerPressActive();
    }

    private bool IsPointerPressActive()
    {
        return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.touchCount > 0;
    }

    private void OnDestroy()
    {
        if (runtimePanelSettings != null)
            Destroy(runtimePanelSettings);
    }

    public void OpenFromUnlockManager(ChaosUnlockManager unlockManager, GameManager sourceGameManager)
    {
        OpenFromUnlockManager(unlockManager, sourceGameManager, false);
    }

    public void OpenMainMenuUnlocks(ChaosUnlockManager unlockManager, GameManager sourceGameManager)
    {
        OpenFromUnlockManager(unlockManager, sourceGameManager, true);
    }

    private void OpenFromUnlockManager(ChaosUnlockManager unlockManager, GameManager sourceGameManager, bool useMainMenuUnlockMode)
    {
        owningUnlockManager = unlockManager;
        bool useRuntimeUnlockInfoMode = !useMainMenuUnlockMode && sourceGameManager != null && sourceGameManager.gameStarted && !sourceGameManager.startMenuOpen && !sourceGameManager.isGameOver;
        if (!isVisible || mainMenuUnlockMode != useMainMenuUnlockMode || runtimeUnlockInfoMode != useRuntimeUnlockInfoMode)
            selectedNavigationId = "overview";

        mainMenuUnlockMode = useMainMenuUnlockMode;
        runtimeUnlockInfoMode = useRuntimeUnlockInfoMode;

        if (sourceGameManager != null)
            gameManager = sourceGameManager;

        EnsureDocument();
        liveRefreshTimer = 0f;

        if (useCanvasFallback)
        {
            SetVisible(false);

            try
            {
                ResolveGameManager();
                currentData = useLiveDataWhenAvailable && gameManager != null ? MetaHubMockData.CreateFromGame(gameManager) : MetaHubMockData.Create();
                liveRefreshTimer = LiveRefreshInterval;
                ApplyUnlockLayoutMode(currentData);
                ApplySelectedNavigation(currentData);
            }
            catch (Exception exception)
            {
                Debug.LogError("MetaHubController: Live-Daten konnten nicht vorbereitet werden. MockData wird verwendet.\n" + exception);
                currentData = MetaHubMockData.Create();
                liveRefreshTimer = LiveRefreshInterval;
                ApplyUnlockLayoutMode(currentData);
                ApplySelectedNavigation(currentData);
            }

            ShowCanvasFallback(currentData);
            return;
        }

        SetVisible(true);

        try
        {
            RefreshData();
        }
        catch (Exception exception)
        {
            Debug.LogError("MetaHubController: Live-Daten konnten nicht gebunden werden. MockData wird verwendet.\n" + exception);
            SetData(MetaHubMockData.Create());
        }
    }

    public void CloseFromUnlockManager()
    {
        owningUnlockManager = null;
        mainMenuUnlockMode = false;
        runtimeUnlockInfoMode = false;
        pendingGeneralLoadoutPromptNodeId = "";
        SetVisible(false);
        SetCanvasFallbackVisible(false);
    }

    public void SetData(MetaHubData data)
    {
        currentData = data != null ? data : MetaHubMockData.Create();
        liveRefreshTimer = LiveRefreshInterval;
        ApplyUnlockLayoutMode(currentData);
        ApplySelectedNavigation(currentData);

        if (IsCanvasFallbackActive())
        {
            RebuildCanvasFallback(currentData);
            return;
        }

        EnsureDocument();
        Bind(currentData);

    }

    public void RefreshData()
    {
        ResolveGameManager();

        MetaHubData data = useLiveDataWhenAvailable && gameManager != null ? MetaHubMockData.CreateFromGame(gameManager) : MetaHubMockData.Create();
        SetData(data);
    }

    private void RefreshViewOnly()
    {
        currentData = currentData != null ? currentData : MetaHubMockData.Create();
        liveRefreshTimer = LiveRefreshInterval;
        ApplySelectedNavigation(currentData);

        if (IsCanvasFallbackActive())
        {
            RebuildCanvasFallbackDynamicContent(currentData);
            return;
        }

        EnsureDocument();
        Bind(currentData);
    }

    private void RefreshMainFrameOnly()
    {
        currentData = currentData != null ? currentData : MetaHubMockData.Create();
        liveRefreshTimer = LiveRefreshInterval;
        ApplySelectedNavigation(currentData);

        if (IsCanvasFallbackActive())
        {
            RebuildCanvasFallbackMainContent(currentData);
            return;
        }

        EnsureDocument();
        Bind(currentData);
    }

    private bool IsCanvasFallbackActive()
    {
        return useCanvasFallback && fallbackCanvas != null && fallbackCanvas.gameObject.activeSelf;
    }

    public void RequestClose()
    {
        pendingGeneralLoadoutPromptNodeId = "";

        if (owningUnlockManager != null)
            owningUnlockManager.CloseUnlocks();
        else
        {
            SetVisible(false);
            SetCanvasFallbackVisible(false);
        }
    }

    public void RequestReturnToMainMenu()
    {
        if (MainMenuRequested != null)
            MainMenuRequested.Invoke();

        SetVisible(false);
        SetCanvasFallbackVisible(false);

        if (gameManager != null)
        {
            gameManager.AbortRunAndReturnToStartMenu();
            return;
        }

        Debug.LogWarning("MetaHub: Kein GameManager gefunden, Hauptmenü-Wechsel konnte nicht ausgeführt werden.");
    }

    private void RequestAllGoals()
    {
        if (AllGoalsRequested != null)
            AllGoalsRequested.Invoke();

        showAllGoalsOverlay = true;
        showAllRisksOverlay = false;
        Debug.Log("MetaHub: All goals button requested.");
        RefreshMainFrameOnly();
    }

    private void CloseAllGoalsOverlay()
    {
        showAllGoalsOverlay = false;
        RefreshMainFrameOnly();
    }

    private void RequestAllRisks()
    {
        showAllRisksOverlay = true;
        showAllGoalsOverlay = false;
        RefreshMainFrameOnly();
    }

    private void CloseAllRisksOverlay()
    {
        showAllRisksOverlay = false;
        RefreshMainFrameOnly();
    }

    private void EnsureDocument()
    {
        if (visualTreeBuilt && documentRoot != null && screenRoot != null)
            return;

        if (document == null)
            document = GetComponent<UIDocument>();

        if (document == null)
            document = gameObject.AddComponent<UIDocument>();

        if (document.panelSettings == null)
            document.panelSettings = panelSettings != null ? panelSettings : CreateRuntimePanelSettings();

        document.sortingOrder = 5000;

        VisualTreeAsset resolvedTree = screenAsset != null ? screenAsset : Resources.Load<VisualTreeAsset>(VisualTreeResourcePath);
        if (resolvedTree != null)
            document.visualTreeAsset = resolvedTree;

        StyleSheet resolvedStyleSheet = styleSheet != null ? styleSheet : Resources.Load<StyleSheet>(StyleSheetResourcePath);
        documentRoot = document.rootVisualElement;
        documentRoot.Clear();
        documentRoot.style.flexGrow = 1f;
        documentRoot.style.width = Length.Percent(100f);
        documentRoot.style.height = Length.Percent(100f);

        if (resolvedStyleSheet != null)
            documentRoot.styleSheets.Add(resolvedStyleSheet);

        if (resolvedTree != null)
            resolvedTree.CloneTree(documentRoot);
        else
            BuildFallbackTree(documentRoot);

        screenRoot = documentRoot.Q<VisualElement>("MetaHubScreen");
        if (screenRoot == null)
            screenRoot = documentRoot;

        visualTreeBuilt = true;
        RegisterButtonCallbacks();
    }

    private PanelSettings CreateRuntimePanelSettings()
    {
        runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        runtimePanelSettings.name = "MetaHubRuntimePanelSettings";
        runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        runtimePanelSettings.referenceResolution = new Vector2Int(1602, 982);
        runtimePanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        runtimePanelSettings.match = 0.5f;
        runtimePanelSettings.sortingOrder = 5000;
        return runtimePanelSettings;
    }

    private void BuildFallbackTree(VisualElement parent)
    {
        VisualElement fallback = new VisualElement();
        fallback.name = "MetaHubScreen";
        fallback.style.flexGrow = 1f;
        fallback.style.backgroundColor = new Color(0.02f, 0.04f, 0.06f, 0.96f);

        Label label = new Label("MetaHubScreen.uxml konnte nicht geladen werden.");
        label.style.color = Color.white;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.flexGrow = 1f;
        fallback.Add(label);
        parent.Add(fallback);
    }

    private void RegisterButtonCallbacks()
    {
        if (callbacksRegistered)
            return;

        RegisterButton("DetailsButton", delegate
        {
            if (DetailsRequested != null)
                DetailsRequested.Invoke();
            Debug.Log("MetaHub: Details button requested.");
        });

        RegisterButton("AllGoalsButton", RequestAllGoals);

        RegisterButton("OptionsButton", delegate
        {
            if (OptionsRequested != null)
                OptionsRequested.Invoke();
            Debug.Log("MetaHub: Options button requested.");
        });

        RegisterButton("MainMenuButton", RequestReturnToMainMenu);
        RegisterButton("BackButton", RequestClose);
        RegisterButton("CloseButton", RequestClose);
        callbacksRegistered = true;
    }

    private void RegisterButton(string buttonName, Action callback)
    {
        UnityEngine.UIElements.Button button = screenRoot != null ? screenRoot.Q<UnityEngine.UIElements.Button>(buttonName) : null;
        if (button != null && callback != null)
            button.clicked += callback;
    }

    private void SelectNavigationSection(string navId)
    {
        string nextNavigationId = string.IsNullOrEmpty(navId) ? "overview" : navId;
        if (selectedNavigationId == nextNavigationId && !showAllGoalsOverlay && !showAllRisksOverlay && !showGeneralLoadoutPicker && !showGeneralLoadoutEditor && string.IsNullOrEmpty(pendingGeneralLoadoutPromptNodeId))
            return;

        selectedNavigationId = nextNavigationId;
        showAllGoalsOverlay = false;
        showAllRisksOverlay = false;
        showGeneralLoadoutPicker = false;
        showGeneralLoadoutEditor = false;
        pendingGeneralLoadoutPromptNodeId = "";
        RefreshViewOnly();
    }

    private bool IsUnlockLayoutMode()
    {
        return mainMenuUnlockMode || runtimeUnlockInfoMode;
    }

    private void ApplyUnlockLayoutMode(MetaHubData data)
    {
        if (!IsUnlockLayoutMode() || data == null)
            return;

        data.screenSubtitle = runtimeUnlockInfoMode ? "RUN-HUB" : "FREISCHALTUNGEN";
        if (runtimeUnlockInfoMode)
        {
            data.footerTip = "Tipp: F1 zeigt deinen aktuellen Run. Meta-Kaeufe bleiben im Hauptmenue.";
            ApplyRuntimeNavigationLabels(data);
            ApplyRuntimeUnlockRunValues(data);
        }
        else
        {
            data.footerTip = "Tipp: Permanente Freischaltungen staerken deinen naechsten Run.";
            ApplyMainMenuUnlockLiveValues(data);
        }

        if (data.navigation == null)
            return;

        for (int i = data.navigation.Count - 1; i >= 0; i--)
        {
            MetaHubNavItemData item = data.navigation[i];
            if (item != null && item.id == "archive")
                data.navigation.RemoveAt(i);
        }
    }

    private void ApplyRuntimeUnlockRunValues(MetaHubData data)
    {
        if (data == null)
            return;

        RunStatistics runStats = gameManager != null ? gameManager.GetRunStatistics() : null;
        ChaosJusticeManager chaosJustice = gameManager != null ? gameManager.GetChaosJusticeManager() : null;
        int currentWave = GetRuntimeWaveNumber();
        int currentGold = GetRuntimeGold();
        int currentLives = GetRuntimeLives();
        int chaosLevel = chaosJustice != null ? Mathf.Max(0, chaosJustice.GetChaosLevel()) : 0;
        int eliteKills = runStats != null ? Mathf.Max(0, runStats.eliteKills) : 0;
        int towerXp = runStats != null ? Mathf.Max(0, runStats.totalTowerXPGranted) : 0;
        int towersBuilt = runStats != null ? Mathf.Max(0, runStats.towersBuilt) : 0;
        int runtimeRiskCount = GetRuntimeRiskCount();
        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;
        int completedWaves = history != null ? Mathf.Max(0, history.GetCompletedWaveCount()) : 0;
        int totalKills = history != null ? Mathf.Max(0, history.GetTotalKills()) : 0;
        int totalLeaks = history != null ? Mathf.Max(0, history.GetTotalLeaks()) : 0;
        int baseDamage = history != null ? Mathf.Max(0, history.GetTotalBaseDamageTaken()) : 0;
        bool inWavePhase = gameManager != null && gameManager.currentPhase == GamePhase.Wave;
        bool inBuildPhase = gameManager != null && gameManager.currentPhase == GamePhase.Build;
        bool isBaseBlocked = gameManager != null && gameManager.isBaseBlocked;

        data.account.level = Mathf.Max(0, currentWave);
        data.account.currentXP = towerXp;
        data.account.requiredXP = Mathf.Max(1, towerXp + Mathf.Max(25, (currentWave + 1) * 25));

        SetDataResource(data, "gold", "Gold", "G", currentGold, MetaHubTone.Gold);
        SetDataResource(data, "xp", "Leben", "LP", currentLives, MetaHubTone.Cyan);
        SetDataResource(data, "chaos", "Chaos", "C", chaosLevel, MetaHubTone.Purple);
        SetDataResource(data, "special", "Elite", "E", eliteKills, MetaHubTone.Red);

        SetDataMetric(data, "tower_mastery", "TOWER", "T", towersBuilt.ToString(), "Gebaut", towersBuilt, Mathf.Max(1, towersBuilt), false);
        SetDataMetric(data, "chaos_wissen", "PHASE", "P", GetRuntimePhaseShortLabel(), "Aktuell", inWavePhase ? 1 : 0, 1, false);
        SetDataMetric(data, "risikokerne", "RISIKEN", "R", runtimeRiskCount.ToString(), "Aktiv", runtimeRiskCount, Mathf.Max(1, runtimeRiskCount), false);
        SetDataMetric(data, "bauplaene", "WELLE", "W", currentWave.ToString(), "Aktuell", currentWave, Mathf.Max(1, currentWave + 1), false);
        SetDataMetric(data, "elite_jagd", "LEAKS", "L", totalLeaks.ToString(), "Im Run", totalLeaks, Mathf.Max(1, totalLeaks), false);

        SetDataSideStat(data, "free_points", "Phase", GetRuntimePhaseShortLabel(), "P", MetaHubTone.Gold);
        SetDataSideStat(data, "chaos_level", "Chaos", chaosLevel.ToString(), "C", MetaHubTone.Purple);
        SetDataSideStat(data, "gold_justice", "Risiko", runtimeRiskCount.ToString(), "R", MetaHubTone.Red);
        SetDataSideStat(data, "xp_justice", "Leaks", totalLeaks.ToString(), "L", MetaHubTone.Cyan);

        data.nextGoals.Clear();
        data.nextGoals.Add(CreateDataGoal("run_wave", "Naechste Welle starten", "W", currentWave, Mathf.Max(1, currentWave + 1), MetaHubTone.Gold));
        data.nextGoals.Add(CreateDataGoal("run_phase", "Bauphase sauber abschliessen", "P", inBuildPhase ? 1 : 0, 1, MetaHubTone.Cyan));
        data.nextGoals.Add(CreateDataGoal("run_block", "Verbau-Status pruefen", "V", isBaseBlocked ? 0 : 1, 1, MetaHubTone.Blue));
        data.nextGoals.Add(CreateDataGoal("run_boss", "Boss-Zyklus im Blick", "B", Mathf.Clamp(currentWave % 10, 0, 10), 10, MetaHubTone.Red));

        ApplyRuntimeEffectLists(data, chaosJustice);

        if (data.lastRunStats == null)
            data.lastRunStats = new List<MetaHubRunStatData>();

        data.lastRunStats.Clear();
        AddDataRunStat(data, "wave", "Wellen beendet", MetaHubMockData.FormatNumber(completedWaves));
        AddDataRunStat(data, "kills", "Kills", MetaHubMockData.FormatNumber(totalKills));
        AddDataRunStat(data, "leaks", "Leaks/Base", MetaHubMockData.FormatNumber(totalLeaks) + "/" + MetaHubMockData.FormatNumber(baseDamage));
        AddDataRunStat(data, "blocked_events", "Verbau-Events", runStats != null ? MetaHubMockData.FormatNumber(runStats.blockedEventsChosen) : "0");
        AddDataRunStat(data, "blocked_top", "Top-Verbau", FormatRuntimeTopBlockedEventType(runStats));
        AddDataRunStat(data, "tower_xp", "Tower-XP im Run", MetaHubMockData.FormatNumber(towerXp));
    }

    private string FormatRuntimeTopBlockedEventType(RunStatistics runStats)
    {
        RunBlockedEventTypeStats topStats = FindRuntimeTopBlockedEventTypeStats(runStats);
        if (topStats == null)
            return "Keiner";

        string label = string.IsNullOrEmpty(topStats.lastEventName) ? topStats.eventType : topStats.lastEventName;
        return Shorten(label, 18) + " x" + topStats.choices;
    }

    private RunBlockedEventTypeStats FindRuntimeTopBlockedEventTypeStats(RunStatistics runStats)
    {
        if (runStats == null || runStats.blockedEventTypeStats == null)
            return null;

        RunBlockedEventTypeStats best = null;

        foreach (RunBlockedEventTypeStats stats in runStats.blockedEventTypeStats)
        {
            if (stats == null || stats.choices <= 0)
                continue;

            if (best == null ||
                stats.choices > best.choices ||
                (stats.choices == best.choices && stats.lastWaveNumber > best.lastWaveNumber))
            {
                best = stats;
            }
        }

        return best;
    }

    private void ApplyRuntimeNavigationLabels(MetaHubData data)
    {
        if (data == null || data.navigation == null)
            return;

        SetDataNavigation(data, "overview", "RUN-STATUS", "R", MetaHubTone.Gold);
        SetDataNavigation(data, "general", "RESSOURCEN", "G", MetaHubTone.Gold);
        SetDataNavigation(data, "tower", "TOWER", "T", MetaHubTone.Purple);
        SetDataNavigation(data, "chaos", "RISIKEN", "C", MetaHubTone.Purple);
        SetDataNavigation(data, "path", "VERBAU", "P", MetaHubTone.Cyan);
        SetDataNavigation(data, "elite", "ELITE", "E", MetaHubTone.Red);
    }

    private void SetDataMetric(MetaHubData data, string id, string valueText, string caption, int current, int maximum, bool showProgress)
    {
        if (data == null || data.metricCards == null || string.IsNullOrEmpty(id))
            return;

        for (int i = 0; i < data.metricCards.Count; i++)
        {
            MetaHubMetricCardData card = data.metricCards[i];
            if (card == null || card.id != id)
                continue;

            card.valueText = valueText;
            card.caption = caption;
            card.current = Mathf.Max(0, current);
            card.maximum = Mathf.Max(0, maximum);
            card.showProgress = showProgress;
            return;
        }
    }

    private void SetDataMetric(MetaHubData data, string id, string title, string iconText, string valueText, string caption, int current, int maximum, bool showProgress)
    {
        if (data == null || data.metricCards == null || string.IsNullOrEmpty(id))
            return;

        for (int i = 0; i < data.metricCards.Count; i++)
        {
            MetaHubMetricCardData card = data.metricCards[i];
            if (card == null || card.id != id)
                continue;

            card.title = title;
            card.iconText = iconText;
            card.valueText = valueText;
            card.caption = caption;
            card.current = Mathf.Max(0, current);
            card.maximum = Mathf.Max(0, maximum);
            card.showProgress = showProgress;
            return;
        }
    }

    private void SetDataSideStat(MetaHubData data, string id, string valueText)
    {
        if (data == null || data.progressStats == null || string.IsNullOrEmpty(id))
            return;

        for (int i = 0; i < data.progressStats.Count; i++)
        {
            MetaHubSideStatData stat = data.progressStats[i];
            if (stat == null || stat.id != id)
                continue;

            stat.valueText = valueText;
            return;
        }
    }

    private void SetDataSideStat(MetaHubData data, string id, string label, string valueText, string iconText, MetaHubTone tone)
    {
        if (data == null || data.progressStats == null || string.IsNullOrEmpty(id))
            return;

        for (int i = 0; i < data.progressStats.Count; i++)
        {
            MetaHubSideStatData stat = data.progressStats[i];
            if (stat == null || stat.id != id)
                continue;

            stat.label = label;
            stat.valueText = valueText;
            stat.iconText = iconText;
            stat.tone = tone;
            return;
        }
    }

    private void SetDataNavigation(MetaHubData data, string id, string label, string iconText, MetaHubTone tone)
    {
        if (data == null || data.navigation == null || string.IsNullOrEmpty(id))
            return;

        for (int i = 0; i < data.navigation.Count; i++)
        {
            MetaHubNavItemData item = data.navigation[i];
            if (item == null || item.id != id)
                continue;

            item.label = label;
            item.iconText = iconText;
            item.tone = tone;
            return;
        }
    }

    private MetaHubGoalData CreateDataGoal(string id, string title, string iconText, int current, int required, MetaHubTone tone)
    {
        MetaHubGoalData goal = new MetaHubGoalData();
        goal.id = id;
        goal.title = title;
        goal.iconText = iconText;
        goal.current = Mathf.Max(0, current);
        goal.required = Mathf.Max(1, required);
        goal.tone = tone;
        return goal;
    }

    private MetaHubEffectData CreateDataEffect(string id, string title, string description, string durationText, string iconText, MetaHubTone tone)
    {
        MetaHubEffectData effect = new MetaHubEffectData();
        effect.id = id;
        effect.title = title;
        effect.description = description;
        effect.durationText = durationText;
        effect.iconText = iconText;
        effect.tone = tone;
        return effect;
    }

    private void ApplyRuntimeEffectLists(MetaHubData data, ChaosJusticeManager chaosJustice)
    {
        if (data == null)
            return;

        if (data.activeBuffs == null)
            data.activeBuffs = new List<MetaHubEffectData>();

        data.activeBuffs.Clear();
        AddRuntimeRewardBuffs(data, chaosJustice);

        if (data.activeRisks == null)
            data.activeRisks = new List<MetaHubEffectData>();

        data.activeRisks.Clear();
        if (chaosJustice != null)
        {
            List<string> risks = chaosJustice.GetSelectedRiskModifierDisplayNames();
            if (risks != null)
            {
                for (int i = 0; i < risks.Count; i++)
                {
                    string title = string.IsNullOrEmpty(risks[i]) ? "Risiko " + (i + 1) : Shorten(risks[i], 30);
                    data.activeRisks.Add(CreateDataEffect("runtime_risk_" + i, title, "Run-Modifikator", "", "R", MetaHubTone.Red));
                }
            }
        }

        if (data.activeRisks.Count == 0)
            data.activeRisks.Add(CreateDataEffect("no_risk", "Keine Risiken", "Keine Modifikatoren", "", "R", MetaHubTone.Neutral));
    }

    private void AddRuntimeRewardBuffs(MetaHubData data, ChaosJusticeManager chaosJustice)
    {
        if (data == null || data.activeBuffs == null)
            return;

        float goldMultiplier = GetRuntimeGoldRewardMultiplier(chaosJustice);
        float xpMultiplier = GetRuntimeXPRewardMultiplier(chaosJustice);

        data.activeBuffs.Add(CreateDataEffect("gold_reward_buff", "Gold-Buff", FormatRuntimePercentBonus(goldMultiplier), "", "G", MetaHubTone.Gold));
        data.activeBuffs.Add(CreateDataEffect("xp_reward_buff", "XP-Buff", FormatRuntimePercentBonus(xpMultiplier), "", "XP", MetaHubTone.Cyan));
    }

    private int GetRuntimeWaveNumber()
    {
        return gameManager != null ? Mathf.Max(0, gameManager.waveNumber) : 0;
    }

    private int GetRuntimeGold()
    {
        return gameManager != null ? Mathf.Max(0, gameManager.gold) : 0;
    }

    private int GetRuntimeLives()
    {
        return gameManager != null ? Mathf.Max(0, gameManager.lives) : 0;
    }

    private int GetRuntimeRiskCount()
    {
        ChaosJusticeManager chaosJustice = gameManager != null ? gameManager.GetChaosJusticeManager() : null;
        if (chaosJustice == null)
            return 0;

        List<string> risks = chaosJustice.GetSelectedRiskModifierDisplayNames();
        return risks != null ? risks.Count : 0;
    }

    private string GetRuntimePhaseShortLabel()
    {
        if (gameManager == null)
            return "?";

        return gameManager.currentPhase == GamePhase.Wave ? "Wave" : "Bau";
    }

    private string GetRuntimeBuildStateLabel()
    {
        if (gameManager == null)
            return "Unbekannt";

        if (!gameManager.gameStarted)
            return "Startmenue";

        if (gameManager.isGameOver)
            return "Game Over";

        if (gameManager.currentPhase == GamePhase.Wave)
            return "Welle laeuft";

        if (gameManager.isBaseBlocked && gameManager.isTimedBlockedBuildPhase)
            return "Verbau-Timer aktiv";

        if (gameManager.isBaseBlocked)
            return "Verbau-Auswahl offen";

        return "Bauphase offen";
    }

    private string GetRuntimeBuildStateValue()
    {
        if (gameManager == null)
            return "?";

        if (gameManager.currentPhase == GamePhase.Wave)
            return "Wave";

        if (gameManager.isBaseBlocked && gameManager.isTimedBlockedBuildPhase)
            return Mathf.CeilToInt(Mathf.Max(0f, gameManager.blockedBuildTimeRemaining)) + "s";

        if (gameManager.isBaseBlocked)
            return "Block";

        return "Offen";
    }

    private bool IsRuntimePathChoiceOpen()
    {
        return gameManager != null && gameManager.IsPathBuildChoiceOpen();
    }

    private WaveData GetRuntimeNextWaveData()
    {
        return gameManager != null ? gameManager.GetNextWaveData() : null;
    }

    private string GetRuntimeNextWaveName()
    {
        WaveData nextWave = GetRuntimeNextWaveData();
        if (nextWave != null)
        {
            string scenarioName = string.IsNullOrEmpty(nextWave.scenarioName) ? nextWave.scenario.ToString() : nextWave.scenarioName;
            return "W" + Mathf.Max(1, nextWave.waveNumber) + " " + scenarioName;
        }

        if (gameManager == null)
            return "Unbekannt";

        return "W" + Mathf.Max(1, gameManager.waveNumber + 1) + " " + gameManager.GetNextWaveScenarioName();
    }

    private string GetRuntimeNextWaveEnemyText()
    {
        WaveData nextWave = GetRuntimeNextWaveData();
        if (nextWave == null)
            return "Keine Preview";

        int total = Mathf.Max(nextWave.totalSpawnCount, nextWave.modifiedEnemyCount);
        total = Mathf.Max(total, nextWave.requestedEnemyCount);
        return MetaHubMockData.FormatNumber(total) + " Gegner";
    }

    private string GetRuntimeNextWaveSpecialText()
    {
        WaveData nextWave = GetRuntimeNextWaveData();
        if (nextWave == null)
            return "Keine Daten";

        if (nextWave.IsBossWave())
            return "Boss";

        if (nextWave.IsMiniBossWave())
            return "MiniBoss";

        if (nextWave.IsEliteWave())
            return "Elite";

        return "Normal";
    }

    private string GetRuntimeNextWaveModifierText()
    {
        WaveData nextWave = GetRuntimeNextWaveData();
        if (nextWave == null)
            return "Keine Daten";

        if (!string.IsNullOrEmpty(nextWave.chaosWaveSummary))
            return Shorten(nextWave.chaosWaveSummary, 42);

        if (!string.IsNullOrEmpty(nextWave.modifierSummary))
            return Shorten(nextWave.modifierSummary, 42);

        if (nextWave.previewHidden)
            return "Preview verborgen";

        return "Keine Zusatzinfo";
    }

    private string GetRuntimeRunStyleShortLabel(ChaosJusticeManager chaosJustice)
    {
        if (chaosJustice == null)
            return "Keine Daten";

        string label = chaosJustice.GetRunStyleLabel();
        if (string.IsNullOrEmpty(label))
            return "Keine Daten";

        if (label.Contains("Unentschied"))
            return "Auftakt";

        if (label.Contains("Chaosbetonter"))
            return "Chaos-Mix";

        if (label.Contains("Gerechtigkeitsbetonter"))
            return "Justice-Mix";

        return Shorten(label, 22);
    }

    private string GetRuntimeDecisionShortText(ChaosJusticeManager chaosJustice)
    {
        if (chaosJustice == null)
            return "Keine";

        return "G " + chaosJustice.GetJusticeChoiceCount() + " / C " + chaosJustice.GetChaosChoiceCount() + " / Hold " + chaosJustice.GetNoModifierChoiceCount();
    }

    private string GetRuntimeRiskGroupShortText(ChaosJusticeManager chaosJustice)
    {
        if (chaosJustice == null)
            return "Keine";

        string text = chaosJustice.GetGroupedRiskModifierSummary();
        if (string.IsNullOrEmpty(text))
            return "Keine";

        text = text.Replace("\r", " ").Replace("\n", " / ").Replace(".", "");
        return Shorten(text, 34);
    }

    private string FormatRuntimeMultiplier(float value)
    {
        return "x" + Mathf.Max(0f, value).ToString("0.00").Replace(",", ".");
    }

    private float GetRuntimeGoldRewardMultiplier(ChaosJusticeManager chaosJustice)
    {
        if (gameManager != null)
            return gameManager.GetTotalGoldRewardMultiplier();

        return chaosJustice != null ? chaosJustice.GetGoldRewardMultiplier() : 1f;
    }

    private float GetRuntimeXPRewardMultiplier(ChaosJusticeManager chaosJustice)
    {
        if (gameManager != null)
            return gameManager.GetTotalXPRewardMultiplier();

        return chaosJustice != null ? chaosJustice.GetXPRewardMultiplier() : 1f;
    }

    private string FormatRuntimePercentBonus(float multiplier)
    {
        int percent = Mathf.RoundToInt((Mathf.Max(0f, multiplier) - 1f) * 100f);
        return percent >= 0 ? "+" + percent + "%" : percent + "%";
    }

    private void ApplyMainMenuUnlockLiveValues(MetaHubData data)
    {
        if (data == null)
            return;

        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        EliteHuntProgressionManager elite = GetEliteHuntManager();

        if (general != null)
        {
            data.account.level = Mathf.Max(1, general.accountLevel);
            data.account.currentXP = Mathf.Max(0, general.GetAccountXPIntoCurrentLevel());
            data.account.requiredXP = Mathf.Max(1, general.GetXPToNextAccountLevel());
            SetDataResource(data, "gold", "Kernwissen", "KW", Mathf.Max(0, general.kernwissen), MetaHubTone.Gold);
            SetDataResource(data, "xp", "Account-XP", "XP", Mathf.Max(0, general.accountXP), MetaHubTone.Cyan);
        }

        if (chaos != null)
            SetDataResource(data, "chaos", "Chaos-Wissen", "C", Mathf.Max(0, chaos.chaosKnowledge), MetaHubTone.Purple);

        if (elite != null)
            SetDataResource(data, "special", "Elite-Siegel", "ES", Mathf.Max(0, elite.eliteSeals), MetaHubTone.Red);

        if (data.activeRisks != null)
            data.activeRisks.Clear();

        if (data.lastRunStats == null)
            data.lastRunStats = new List<MetaHubRunStatData>();

        data.lastRunStats.Clear();
        AddDataRunStat(data, "kernwissen", "Kernwissen", general != null ? "+" + MetaHubMockData.FormatNumber(general.lastRunKernwissenGained) : "+0");
        AddDataRunStat(data, "chaos", "Chaos-Wissen", chaos != null ? "+" + MetaHubMockData.FormatNumber(chaos.lastRunChaosKnowledgeGained) : "+0");
        AddDataRunStat(data, "blueprints", "Bauplaene", path != null ? "+" + MetaHubMockData.FormatNumber(path.lastRunBlueprintsGained) : "+0");
        AddDataRunStat(data, "elite", "Elite-Siegel", elite != null ? "+" + MetaHubMockData.FormatNumber(elite.lastRunEliteSealsGained) : "+0");
    }

    private void SetDataResource(MetaHubData data, string id, string label, string iconText, int value, MetaHubTone tone)
    {
        if (data == null || string.IsNullOrEmpty(id))
            return;

        if (data.resources == null)
            data.resources = new List<MetaHubResourceData>();

        for (int i = 0; i < data.resources.Count; i++)
        {
            MetaHubResourceData resource = data.resources[i];
            if (resource == null || resource.id != id)
                continue;

            resource.label = label;
            resource.iconText = iconText;
            resource.value = Mathf.Max(0, value);
            resource.tone = tone;
            return;
        }

        MetaHubResourceData newResource = new MetaHubResourceData();
        newResource.id = id;
        newResource.label = label;
        newResource.iconText = iconText;
        newResource.value = Mathf.Max(0, value);
        newResource.tone = tone;
        data.resources.Add(newResource);
    }

    private void AddDataRunStat(MetaHubData data, string id, string label, string valueText)
    {
        if (data == null)
            return;

        if (data.lastRunStats == null)
            data.lastRunStats = new List<MetaHubRunStatData>();

        MetaHubRunStatData stat = new MetaHubRunStatData();
        stat.id = id;
        stat.label = label;
        stat.valueText = valueText;
        data.lastRunStats.Add(stat);
    }

    private void ApplySelectedNavigation(MetaHubData data)
    {
        if (data == null || data.navigation == null || data.navigation.Count == 0)
            return;

        bool found = false;
        for (int i = 0; i < data.navigation.Count; i++)
        {
            if (data.navigation[i] != null && data.navigation[i].id == selectedNavigationId)
            {
                found = true;
                break;
            }
        }

        if (!found)
            selectedNavigationId = "overview";

        for (int i = 0; i < data.navigation.Count; i++)
        {
            if (data.navigation[i] != null)
                data.navigation[i].selected = data.navigation[i].id == selectedNavigationId;
        }

        data.selectedSectionTitle = GetSelectedNavigationTitle(data.navigation);
    }

    private string GetSelectedNavigationTitle(List<MetaHubNavItemData> navigation)
    {
        if (navigation != null)
        {
            for (int i = 0; i < navigation.Count; i++)
            {
                MetaHubNavItemData item = navigation[i];
                if (item != null && item.selected)
                    return item.label;
            }
        }

        return "ÜBERSICHT";
    }

    private void Bind(MetaHubData data)
    {
        if (screenRoot == null || data == null)
            return;

        screenRoot.EnableInClassList("main-menu-unlocks", IsUnlockLayoutMode());

        SetText("ScreenTitle", data.screenTitle);
        SetText("SidebarTitle", data.screenSubtitle);
        SetText("SectionTitle", data.selectedSectionTitle);
        SetText("FooterTip", data.footerTip);

        SetText("AccountLevelTop", runtimeUnlockInfoMode ? "Run Welle " + Mathf.Max(0, GetRuntimeWaveNumber()) : "Account Lv. " + data.account.level);
        SetText("AccountXpTop", runtimeUnlockInfoMode ? "" : MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP");
        SetText("AccountLevelCenter", runtimeUnlockInfoMode ? Mathf.Max(0, GetRuntimeWaveNumber()).ToString() : data.account.level.ToString());
        SetText("AccountXpBottom", runtimeUnlockInfoMode ? "Tower-XP " + MetaHubMockData.FormatNumber(data.account.currentXP) : MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP");
        SetFillPercent("AccountXpFill", Percent(data.account.currentXP, data.account.requiredXP));
        SetFillPercent("AccountRingFill", Percent(data.account.currentXP, data.account.requiredXP));

        BuildResources(data.resources);
        BuildNavigation(data.navigation);
        BuildMetricCards(data.metricCards);
        BuildProgressStats(data.progressStats);
        BindChaosJustice(data.chaosJustice);
        BuildGoals(data.nextGoals);
        BuildEffects("BuffList", data.activeBuffs);
        BuildEffects("RiskList", data.activeRisks);
        BuildKeystones(data.activeKeystones);
        BuildRunStats(data.lastRunStats);

        VisualElement mainMenuButton = Query("MainMenuButton");
        if (mainMenuButton != null)
            mainMenuButton.style.display = runtimeUnlockInfoMode || !IsUnlockLayoutMode() ? DisplayStyle.Flex : DisplayStyle.None;

        VisualElement optionsButton = Query("OptionsButton");
        if (optionsButton != null)
            optionsButton.style.display = runtimeUnlockInfoMode ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void BindChaosJustice(MetaHubChaosJusticeData chaosJustice)
    {
        SetText("SafetyScoreValue", chaosJustice.safetyScore.ToString());
        SetText("ChaosScoreValue", chaosJustice.chaosScore.ToString());
        SetText("BalanceValue", chaosJustice.safetyPercent + "%");
        SetText("StabilityLabel", chaosJustice.stabilityLabel);
        SetFillPercent("JusticeBalanceFill", chaosJustice.safetyPercent / 100f);
        SetFillPercent("ChaosBalanceFill", chaosJustice.chaosPercent / 100f);

        VisualElement marker = screenRoot.Q<VisualElement>("BalanceMarker");
        if (marker != null)
            marker.style.left = Length.Percent(Mathf.Clamp(chaosJustice.chaosPercent, 0, 100));
    }

    private void BuildResources(List<MetaHubResourceData> resources)
    {
        VisualElement container = Query("ResourceList");
        if (container == null)
            return;

        int count = resources != null ? resources.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubResourceData item = resources[i] ?? new MetaHubResourceData();
            VisualElement row = GetOrCreateReusableListItem(container, i, CreateResourceChipElement);
            ResourceChipView view = row.userData as ResourceChipView;
            if (view == null)
                continue;

            SetToneClass(row, item.tone);
            SetToneClass(view.icon, item.tone);
            SetLabelText(view.icon, item.iconText);
            SetLabelText(view.value, MetaHubMockData.FormatNumber(item.value));
            row.tooltip = item.label;
        }

        HideUnusedListItems(container, count);
    }

    private void BuildNavigation(List<MetaHubNavItemData> items)
    {
        VisualElement container = Query("NavList");
        if (container == null)
            return;

        int count = items != null ? items.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubNavItemData item = items[i] ?? new MetaHubNavItemData();
            VisualElement row = GetOrCreateReusableListItem(container, i, CreateNavigationRowElement);
            NavigationRowView view = row.userData as NavigationRowView;
            if (view == null)
                continue;

            SetToneClass(row, item.tone);
            SetToneClass(view.icon, item.tone);
            row.EnableInClassList("selected", item.selected);
            SetLabelText(view.icon, item.iconText);
            SetLabelText(view.label, item.label);
        }

        HideUnusedListItems(container, count);
    }

    private void BuildMetricCards(List<MetaHubMetricCardData> cards)
    {
        VisualElement container = Query("MetricCardList");
        if (container == null)
            return;

        int count = cards != null ? cards.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubMetricCardData item = cards[i] ?? new MetaHubMetricCardData();
            VisualElement card = GetOrCreateReusableListItem(container, i, CreateMetricCardElement);
            MetricCardView view = card.userData as MetricCardView;
            if (view == null)
                continue;

            SetToneClass(card, item.tone);
            SetToneClass(view.icon, item.tone);
            SetLabelText(view.title, item.title);
            SetLabelText(view.icon, item.iconText);
            SetLabelText(view.value, item.valueText);
            SetLabelText(view.caption, item.caption);
            SetProgressLine(view.progressTrack, view.progressFill, item.current, item.maximum, item.tone, item.showProgress);
        }

        HideUnusedListItems(container, count);
    }

    private void BuildProgressStats(List<MetaHubSideStatData> stats)
    {
        VisualElement container = Query("ProgressStatList");
        if (container == null)
            return;

        int count = stats != null ? stats.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubSideStatData item = stats[i] ?? new MetaHubSideStatData();
            VisualElement row = GetOrCreateReusableListItem(container, i, CreateSideStatRowElement);
            SideStatRowView view = row.userData as SideStatRowView;
            if (view == null)
                continue;

            SetToneClass(row, item.tone);
            SetToneClass(view.icon, item.tone);
            SetLabelText(view.label, item.label);
            SetLabelText(view.value, item.valueText);
            SetLabelText(view.icon, item.iconText);
        }

        HideUnusedListItems(container, count);
    }

    private void BuildGoals(List<MetaHubGoalData> goals)
    {
        VisualElement container = Query("GoalList");
        if (container == null)
            return;

        int count = goals != null ? goals.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubGoalData item = goals[i] ?? new MetaHubGoalData();
            VisualElement row = GetOrCreateReusableListItem(container, i, CreateGoalRowElement);
            GoalRowView view = row.userData as GoalRowView;
            if (view == null)
                continue;

            SetToneClass(row, item.tone);
            SetToneClass(view.icon, item.tone);
            SetLabelText(view.icon, item.iconText);
            SetLabelText(view.title, item.title);
            SetLabelText(view.progress, item.current + " / " + item.required);
        }

        HideUnusedListItems(container, count);
    }

    private void BuildEffects(string containerName, List<MetaHubEffectData> effects)
    {
        VisualElement container = Query(containerName);
        if (container == null)
            return;

        int count = effects != null ? effects.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubEffectData item = effects[i] ?? new MetaHubEffectData();
            VisualElement row = GetOrCreateReusableListItem(container, i, CreateEffectRowElement);
            EffectRowView view = row.userData as EffectRowView;
            if (view == null)
                continue;

            SetToneClass(row, item.tone);
            SetToneClass(view.icon, item.tone);
            SetLabelText(view.icon, item.iconText);
            SetLabelText(view.title, item.title);
            SetLabelText(view.description, item.description);
            SetLabelText(view.duration, item.durationText);
            view.duration.style.display = string.IsNullOrEmpty(item.durationText) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        HideUnusedListItems(container, count);
    }

    private void BuildKeystones(List<MetaHubKeystoneData> keystones)
    {
        VisualElement container = Query("KeystoneList");
        if (container == null)
            return;

        int count = keystones != null ? keystones.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubKeystoneData item = keystones[i] ?? new MetaHubKeystoneData();
            VisualElement column = GetOrCreateReusableListItem(container, i, CreateKeystoneItemElement);
            KeystoneItemView view = column.userData as KeystoneItemView;
            if (view == null)
                continue;

            SetToneClass(column, item.tone);
            SetToneClass(view.icon, item.tone);
            SetLabelText(view.icon, item.iconText);
            SetLabelText(view.level, "Lv. " + item.level);
            SetProgressLine(view.progressTrack, view.progressFill, item.current, item.maximum, item.tone, true);
        }

        HideUnusedListItems(container, count);
    }

    private void BuildRunStats(List<MetaHubRunStatData> stats)
    {
        VisualElement container = Query("LastRunList");
        if (container == null)
            return;

        int count = stats != null ? stats.Count : 0;

        for (int i = 0; i < count; i++)
        {
            MetaHubRunStatData item = stats[i] ?? new MetaHubRunStatData();
            VisualElement row = GetOrCreateReusableListItem(container, i, CreateRunStatRowElement);
            RunStatRowView view = row.userData as RunStatRowView;
            if (view == null)
                continue;

            SetLabelText(view.label, item.label);
            SetLabelText(view.value, item.valueText);
        }

        HideUnusedListItems(container, count);
    }

    private VisualElement GetOrCreateReusableListItem(VisualElement container, int index, Func<VisualElement> createElement)
    {
        while (container.childCount <= index)
            container.Add(createElement());

        VisualElement item = container.ElementAt(index);
        item.style.display = DisplayStyle.Flex;
        return item;
    }

    private void HideUnusedListItems(VisualElement container, int visibleCount)
    {
        if (container == null)
            return;

        for (int i = Mathf.Max(0, visibleCount); i < container.childCount; i++)
            container.ElementAt(i).style.display = DisplayStyle.None;
    }

    private VisualElement CreateResourceChipElement()
    {
        ResourceChipView view = new ResourceChipView();
        VisualElement row = new VisualElement();
        row.AddToClassList("resource-chip");
        view.icon = CreateIcon("", "resource-icon", MetaHubTone.Neutral);
        view.value = CreateLabel("", "resource-value");
        row.Add(view.icon);
        row.Add(view.value);
        row.userData = view;
        return row;
    }

    private VisualElement CreateNavigationRowElement()
    {
        NavigationRowView view = new NavigationRowView();
        VisualElement row = new VisualElement();
        row.AddToClassList("nav-row");
        view.icon = CreateIcon("", "nav-icon", MetaHubTone.Neutral);
        view.label = CreateLabel("", "nav-label");
        row.Add(view.icon);
        row.Add(view.label);
        row.userData = view;
        return row;
    }

    private VisualElement CreateMetricCardElement()
    {
        MetricCardView view = new MetricCardView();
        VisualElement card = new VisualElement();
        card.AddToClassList("metric-card");

        view.title = CreateLabel("", "metric-title");
        card.Add(view.title);

        VisualElement body = new VisualElement();
        body.AddToClassList("metric-body");
        view.icon = CreateIcon("", "metric-icon", MetaHubTone.Neutral);
        body.Add(view.icon);

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("metric-text-block");
        view.value = CreateLabel("", "metric-value");
        view.caption = CreateLabel("", "metric-caption");
        textBlock.Add(view.value);
        textBlock.Add(view.caption);
        body.Add(textBlock);
        card.Add(body);

        CreateReusableProgressLine(card, out view.progressTrack, out view.progressFill);
        card.userData = view;
        return card;
    }

    private VisualElement CreateSideStatRowElement()
    {
        SideStatRowView view = new SideStatRowView();
        VisualElement row = new VisualElement();
        row.AddToClassList("side-stat-row");
        view.label = CreateLabel("", "side-stat-label");
        view.value = CreateLabel("", "side-stat-value");
        view.icon = CreateIcon("", "side-stat-icon", MetaHubTone.Neutral);
        row.Add(view.label);
        row.Add(view.value);
        row.Add(view.icon);
        row.userData = view;
        return row;
    }

    private VisualElement CreateGoalRowElement()
    {
        GoalRowView view = new GoalRowView();
        VisualElement row = new VisualElement();
        row.AddToClassList("goal-row");
        view.icon = CreateIcon("", "goal-icon", MetaHubTone.Neutral);
        view.title = CreateLabel("", "goal-title");
        view.progress = CreateLabel("", "goal-progress");
        row.Add(view.icon);
        row.Add(view.title);
        row.Add(view.progress);
        row.userData = view;
        return row;
    }

    private VisualElement CreateEffectRowElement()
    {
        EffectRowView view = new EffectRowView();
        VisualElement row = new VisualElement();
        row.AddToClassList("effect-row");
        view.icon = CreateIcon("", "effect-icon", MetaHubTone.Neutral);
        row.Add(view.icon);

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("effect-text-block");
        view.title = CreateLabel("", "effect-title");
        view.description = CreateLabel("", "effect-description");
        textBlock.Add(view.title);
        textBlock.Add(view.description);
        row.Add(textBlock);

        view.duration = CreateLabel("", "effect-duration");
        row.Add(view.duration);
        row.userData = view;
        return row;
    }

    private VisualElement CreateKeystoneItemElement()
    {
        KeystoneItemView view = new KeystoneItemView();
        VisualElement column = new VisualElement();
        column.AddToClassList("keystone-item");
        view.icon = CreateIcon("", "keystone-icon", MetaHubTone.Neutral);
        view.level = CreateLabel("", "keystone-level");
        column.Add(view.icon);
        column.Add(view.level);
        CreateReusableProgressLine(column, out view.progressTrack, out view.progressFill);
        column.userData = view;
        return column;
    }

    private VisualElement CreateRunStatRowElement()
    {
        RunStatRowView view = new RunStatRowView();
        VisualElement row = new VisualElement();
        row.AddToClassList("run-stat-row");
        view.label = CreateLabel("", "run-stat-label");
        view.value = CreateLabel("", "run-stat-value");
        row.Add(view.label);
        row.Add(view.value);
        row.userData = view;
        return row;
    }

    private void CreateReusableProgressLine(VisualElement parent, out VisualElement track, out VisualElement fill)
    {
        track = new VisualElement();
        track.AddToClassList("mini-progress-track");
        fill = new VisualElement();
        fill.AddToClassList("mini-progress-fill");
        track.Add(fill);
        parent.Add(track);
    }

    private void SetProgressLine(VisualElement track, VisualElement fill, int current, int maximum, MetaHubTone tone, bool visible)
    {
        if (track == null || fill == null)
            return;

        track.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        SetToneClass(track, tone);
        SetToneClass(fill, tone);
        fill.style.width = Length.Percent(Percent(current, maximum) * 100f);
    }

    private void SetLabelText(Label label, string text)
    {
        if (label != null)
            label.text = string.IsNullOrEmpty(text) ? "" : text;
    }

    private VisualElement CreateProgressLine(int current, int maximum, MetaHubTone tone)
    {
        VisualElement track = new VisualElement();
        track.AddToClassList("mini-progress-track");
        AddToneClass(track, tone);

        VisualElement fill = new VisualElement();
        fill.AddToClassList("mini-progress-fill");
        AddToneClass(fill, tone);
        fill.style.width = Length.Percent(Percent(current, maximum) * 100f);
        track.Add(fill);
        return track;
    }

    private Label CreateIcon(string text, string className, MetaHubTone tone)
    {
        Label icon = new Label(string.IsNullOrEmpty(text) ? " " : text);
        icon.AddToClassList(className);
        AddToneClass(icon, tone);
        return icon;
    }

    private Label CreateLabel(string text, string className)
    {
        Label label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    private VisualElement Query(string name)
    {
        return screenRoot != null ? screenRoot.Q<VisualElement>(name) : null;
    }

    private void SetText(string elementName, string value)
    {
        Label label = screenRoot != null ? screenRoot.Q<Label>(elementName) : null;
        if (label != null)
            label.text = value;
    }

    private void SetFillPercent(string elementName, float percent01)
    {
        VisualElement element = screenRoot != null ? screenRoot.Q<VisualElement>(elementName) : null;
        if (element != null)
            element.style.width = Length.Percent(Mathf.Clamp01(percent01) * 100f);
    }

    private float Percent(int current, int maximum)
    {
        if (maximum <= 0)
            return 0f;

        return Mathf.Clamp01(current / (float)maximum);
    }

    private void AddToneClass(VisualElement element, MetaHubTone tone)
    {
        if (element == null)
            return;

        element.AddToClassList("tone-" + tone.ToString().ToLowerInvariant());
    }

    private void SetToneClass(VisualElement element, MetaHubTone tone)
    {
        if (element == null)
            return;

        for (int i = 0; i < ReusableToneClasses.Length; i++)
            element.RemoveFromClassList("tone-" + ReusableToneClasses[i].ToString().ToLowerInvariant());

        AddToneClass(element, tone);
    }

    private void SetVisible(bool visible)
    {
        if (screenRoot == null)
            return;

        isVisible = visible;
        screenRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ResolveGameManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void ShowCanvasFallback(MetaHubData data)
    {
        EnsureCanvasFallback();
        RebuildCanvasFallback(data != null ? data : MetaHubMockData.Create());
        SetCanvasFallbackVisible(true);
        isVisible = true;
    }

    private void EnsureCanvasFallback()
    {
        if (fallbackCanvas != null && fallbackRoot != null)
        {
            EnsureFallbackDesignRoot();
            return;
        }

        GameObject canvasObject = new GameObject("MetaHubCanvasOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        fallbackCanvas = canvasObject.GetComponent<Canvas>();
        fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fallbackCanvas.overrideSorting = true;
        fallbackCanvas.sortingOrder = 6000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 982f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        if (EventSystem.current == null)
        {
            GameObject eventSystemObject = new GameObject("MetaHubEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        fallbackRoot = new GameObject("MetaHubCanvasRoot", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        fallbackRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform rootRect = fallbackRoot.GetComponent<RectTransform>();
        Stretch(rootRect);
        UnityEngine.UI.Image rootImage = fallbackRoot.GetComponent<UnityEngine.UI.Image>();
        rootImage.color = new Color32(2, 8, 10, 246);
        rootImage.raycastTarget = true;

        EnsureFallbackDesignRoot();
    }

    private void EnsureFallbackDesignRoot()
    {
        if (fallbackRoot == null)
            return;

        if (fallbackDesignRoot == null)
        {
            Transform existingDesignRoot = fallbackRoot.transform.Find("MetaHubDesignRoot");
            fallbackDesignRoot = existingDesignRoot != null ? existingDesignRoot.gameObject : new GameObject("MetaHubDesignRoot", typeof(RectTransform));
        }

        fallbackDesignRoot.transform.SetParent(fallbackRoot.transform, false);
        RectTransform designRect = fallbackDesignRoot.GetComponent<RectTransform>();
        if (designRect == null)
            designRect = fallbackDesignRoot.AddComponent<RectTransform>();

        designRect.anchorMin = new Vector2(0.5f, 1f);
        designRect.anchorMax = new Vector2(0.5f, 1f);
        designRect.pivot = new Vector2(0.5f, 1f);
        designRect.anchoredPosition = Vector2.zero;
        designRect.sizeDelta = new Vector2(1600f, 982f);
    }

    private void RebuildCanvasFallback(MetaHubData data)
    {
        EnsureCanvasFallback();
        bool unlockLayout = IsUnlockLayoutMode();

        Transform root = fallbackDesignRoot != null ? fallbackDesignRoot.transform : fallbackRoot.transform;
        string fallbackSignature = BuildCanvasFallbackSignature(data, unlockLayout);
        if (root.childCount > 0 && fallbackSignature == lastCanvasFallbackSignature)
            return;

        lastCanvasFallbackSignature = fallbackSignature;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        CreateCanvasPanel(root, "TopBar", new Vector2(800f, -47f), new Vector2(1584f, 64f), new Color32(3, 12, 14, 245), new Color32(129, 83, 25, 255));
        CreateLine(root, "TopLeftOrnament", new Vector2(240f, -23f), new Vector2(465f, 1f), new Color32(116, 77, 24, 210));
        CreateLine(root, "TopRightOrnament", new Vector2(1322f, -23f), new Vector2(465f, 1f), new Color32(116, 77, 24, 210));
        TextMeshProUGUI title = CreateCanvasLabel(root, "Title", data.screenTitle, new Vector2(800f, -36f), new Vector2(540f, 48f), 34f, new Color32(244, 198, 98, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        title.characterSpacing = 9f;
        if (unlockLayout)
            CreateCanvasLabel(root, "TopSubtitle", runtimeUnlockInfoMode ? "RUN-HUB - INGAME-STATUS" : "FREISCHALTUNGEN - META-PROGRESSION", new Vector2(800f, -64f), new Vector2(430f, 18f), 11f, new Color32(225, 164, 54, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        for (int i = 0; i < data.resources.Count && i < 4; i++)
            CreateReferenceResource(root, data.resources[i], i);

        CreateCanvasLabel(root, "AccountTop", runtimeUnlockInfoMode ? "Run Welle " + Mathf.Max(0, GetRuntimeWaveNumber()) : "Account Lv. " + data.account.level, new Vector2(1188f, -46f), new Vector2(160f, 24f), 16f, new Color32(244, 226, 186, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasBar(root, "AccountXP", new Vector2(1312f, -47f), new Vector2(140f, 7f), Percent(data.account.currentXP, data.account.requiredXP), new Color32(150, 126, 255, 255));
        if (!runtimeUnlockInfoMode)
            CreateCanvasLabel(root, "AccountXPText", MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP", new Vector2(1443f, -46f), new Vector2(125f, 23f), 14f, new Color32(244, 226, 186, 255), TextAlignmentOptions.Right, FontStyles.Normal);
        CreateCanvasButton(root, "CloseTopButton", "X", new Vector2(1570f, -47f), new Vector2(34f, 42f), RequestClose);

        CreateCanvasFallbackDynamicContent(root, data, unlockLayout);

        CreateCanvasLabel(root, "Tip", "<> " + data.footerTip, new Vector2(375f, -907f), new Vector2(720f, 28f), 14f, new Color32(217, 194, 159, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (unlockLayout)
        {
            if (!runtimeUnlockInfoMode)
                CreateCanvasButton(root, "OptionsButton", "OPTIONEN", new Vector2(1300f, -942f), new Vector2(170f, 42f), delegate { Debug.Log("MetaHub: Options button requested."); });
            else
                CreateCanvasButton(root, "MainMenuButton", "HAUPTMENUE", new Vector2(1325f, -942f), new Vector2(180f, 42f), RequestReturnToMainMenu);

            CreateCanvasButton(root, "BackButton", "< ZURÜCK", new Vector2(1510f, -942f), new Vector2(170f, 42f), RequestClose);
        }
        else
        {
            CreateCanvasButton(root, "OptionsButton", "OPTIONEN", new Vector2(1135f, -942f), new Vector2(160f, 42f), delegate { Debug.Log("MetaHub: Options button requested."); });
            CreateCanvasButton(root, "MainMenuButton", "HAUPTMENÜ", new Vector2(1325f, -942f), new Vector2(180f, 42f), RequestReturnToMainMenu);
            CreateCanvasButton(root, "BackButton", "< ZURÜCK", new Vector2(1512f, -942f), new Vector2(160f, 42f), RequestClose);
        }
    }

    private string BuildCanvasFallbackSignature(MetaHubData data, bool unlockLayout)
    {
        StringBuilder builder = new StringBuilder(512);
        builder.Append(unlockLayout ? "unlock" : "hub");
        builder.Append('|').Append(runtimeUnlockInfoMode ? "runtime" : "menu");
        builder.Append('|').Append(selectedNavigationId);
        builder.Append('|').Append(selectedTowerRole);
        builder.Append('|').Append(selectedGeneralTreeCategory);
        builder.Append('|').Append(selectedTowerTreeCategory);
        builder.Append('|').Append(selectedChaosTreeCategory);
        builder.Append('|').Append(selectedPathTreeCategory);
        builder.Append('|').Append(selectedEliteTreeCategory);
        builder.Append('|').Append(showGeneralLoadoutPicker);
        builder.Append('|').Append(showGeneralLoadoutEditor);
        builder.Append('|').Append(showAllGoalsOverlay);
        builder.Append('|').Append(showAllRisksOverlay);

        if (data == null)
            return builder.ToString();

        builder.Append('|').Append(data.screenTitle);
        builder.Append('|').Append(data.screenSubtitle);
        builder.Append('|').Append(data.selectedSectionTitle);
        builder.Append('|').Append(data.footerTip);
        builder.Append("|acct:").Append(data.account != null ? data.account.level : 0)
            .Append(':').Append(data.account != null ? data.account.currentXP : 0)
            .Append(':').Append(data.account != null ? data.account.requiredXP : 0);
        AppendResourceSignature(builder, data.resources);
        AppendNavigationSignature(builder, data.navigation);
        AppendMetricSignature(builder, data.metricCards);
        AppendSideStatSignature(builder, data.progressStats);
        AppendGoalSignature(builder, data.nextGoals);
        AppendEffectSignature(builder, "buff", data.activeBuffs);
        AppendEffectSignature(builder, "risk", data.activeRisks);
        AppendKeystoneSignature(builder, data.activeKeystones);
        AppendRunStatSignature(builder, data.lastRunStats);
        builder.Append("|cj:")
            .Append(data.chaosJustice != null ? data.chaosJustice.safetyScore : 0).Append(':')
            .Append(data.chaosJustice != null ? data.chaosJustice.chaosScore : 0).Append(':')
            .Append(data.chaosJustice != null ? data.chaosJustice.safetyPercent : 0).Append(':')
            .Append(data.chaosJustice != null ? data.chaosJustice.chaosPercent : 0).Append(':')
            .Append(data.chaosJustice != null ? data.chaosJustice.stabilityLabel : "");
        return builder.ToString();
    }

    private void AppendResourceSignature(StringBuilder builder, List<MetaHubResourceData> resources)
    {
        builder.Append("|res:");
        if (resources == null)
            return;

        for (int i = 0; i < resources.Count; i++)
        {
            MetaHubResourceData item = resources[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.value).Append(',').Append(item.tone).Append(';');
        }
    }

    private void AppendNavigationSignature(StringBuilder builder, List<MetaHubNavItemData> items)
    {
        builder.Append("|nav:");
        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
        {
            MetaHubNavItemData item = items[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.selected ? 1 : 0).Append(',').Append(item.label).Append(';');
        }
    }

    private void AppendMetricSignature(StringBuilder builder, List<MetaHubMetricCardData> cards)
    {
        builder.Append("|met:");
        if (cards == null)
            return;

        for (int i = 0; i < cards.Count; i++)
        {
            MetaHubMetricCardData item = cards[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.valueText).Append(',')
                .Append(item.current).Append('/').Append(item.maximum).Append(',').Append(item.tone).Append(';');
        }
    }

    private void AppendSideStatSignature(StringBuilder builder, List<MetaHubSideStatData> stats)
    {
        builder.Append("|side:");
        if (stats == null)
            return;

        for (int i = 0; i < stats.Count; i++)
        {
            MetaHubSideStatData item = stats[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.valueText).Append(';');
        }
    }

    private void AppendGoalSignature(StringBuilder builder, List<MetaHubGoalData> goals)
    {
        builder.Append("|goal:");
        if (goals == null)
            return;

        for (int i = 0; i < goals.Count; i++)
        {
            MetaHubGoalData item = goals[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.current).Append('/').Append(item.required).Append(',').Append(item.title).Append(';');
        }
    }

    private void AppendEffectSignature(StringBuilder builder, string prefix, List<MetaHubEffectData> effects)
    {
        builder.Append('|').Append(prefix).Append(':');
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            MetaHubEffectData item = effects[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.title).Append(',').Append(item.description).Append(',').Append(item.durationText).Append(';');
        }
    }

    private void AppendKeystoneSignature(StringBuilder builder, List<MetaHubKeystoneData> keystones)
    {
        builder.Append("|key:");
        if (keystones == null)
            return;

        for (int i = 0; i < keystones.Count; i++)
        {
            MetaHubKeystoneData item = keystones[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.level).Append(',').Append(item.current).Append('/').Append(item.maximum).Append(';');
        }
    }

    private void AppendRunStatSignature(StringBuilder builder, List<MetaHubRunStatData> stats)
    {
        builder.Append("|run:");
        if (stats == null)
            return;

        for (int i = 0; i < stats.Count; i++)
        {
            MetaHubRunStatData item = stats[i];
            if (item == null)
                continue;

            builder.Append(item.id).Append('=').Append(item.valueText).Append(';');
        }
    }

    private void RebuildCanvasFallbackDynamicContent(MetaHubData data)
    {
        EnsureCanvasFallback();
        Transform root = fallbackDesignRoot != null ? fallbackDesignRoot.transform : fallbackRoot.transform;
        DestroyCanvasChild(root, "Sidebar");
        DestroyCanvasChild(root, "MainFrame");
        CreateCanvasFallbackDynamicContent(root, data, IsUnlockLayoutMode());
    }

    private void CreateCanvasFallbackDynamicContent(Transform root, MetaHubData data, bool unlockLayout)
    {
        if (root == null || data == null)
            return;

        Transform sidebar = CreateOrnatePanel(root, "Sidebar", new Vector2(146f, -473f), new Vector2(274f, 774f), new Color32(5, 17, 18, 246), new Color32(143, 99, 41, 255));
        TextMeshProUGUI sidebarTitle = CreateCanvasLabelTop(sidebar, "SidebarTitle", data.screenSubtitle, new Vector2(0f, -35f), new Vector2(268f, 42f), unlockLayout ? 18f : 21f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        sidebarTitle.characterSpacing = unlockLayout ? 0.5f : 2f;
        for (int i = 0; i < data.navigation.Count; i++)
            CreateReferenceNavRow(sidebar, data.navigation[i], i);
        if (runtimeUnlockInfoMode)
        {
            CreateRuntimeSidebarStatus(sidebar);
        }
        else
        {
            CreateCanvasLabelTop(sidebar, "KeystoneTitle", "AKTIVE KEYSTONES", new Vector2(-5f, -525f), new Vector2(238f, 30f), 17f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            for (int i = 0; i < data.activeKeystones.Count && i < 3; i++)
                CreateReferenceKeystone(sidebar, data.activeKeystones[i], i);
        }

        CreateCanvasFallbackMainContent(root, data, unlockLayout);
    }

    private void RebuildCanvasFallbackMainContent(MetaHubData data)
    {
        EnsureCanvasFallback();
        Transform root = fallbackDesignRoot != null ? fallbackDesignRoot.transform : fallbackRoot.transform;
        DestroyCanvasChild(root, "MainFrame");
        CreateCanvasFallbackMainContent(root, data, IsUnlockLayoutMode());
    }

    private void CreateCanvasFallbackMainContent(Transform root, MetaHubData data, bool unlockLayout)
    {
        if (root == null || data == null)
            return;

        Vector2 mainPosition = unlockLayout ? new Vector2(938.5f, -473f) : new Vector2(885f, -473f);
        Vector2 mainSize = unlockLayout ? new Vector2(1307f, 774f) : new Vector2(1216f, 774f);
        Transform main = CreateOrnatePanel(root, "MainFrame", mainPosition, mainSize, new Color32(7, 18, 19, 241), new Color32(104, 82, 45, 255));
        CreateCanvasLabelTop(main, "SectionTitle", data.selectedSectionTitle, new Vector2(unlockLayout ? -352f : -300f, -58f), new Vector2(560f, 32f), 21f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);

        if (unlockLayout)
        {
            CreateMainMenuUnlockContent(main, data);
        }
        else
        {
            for (int i = 0; i < data.metricCards.Count && i < 5; i++)
                CreateReferenceMetricCard(main, data.metricCards[i], i);

            CreateReferenceProgressPanel(main, data);
            CreateReferenceChaosPanel(main, data.chaosJustice);
            CreateReferenceGoalPanel(main, data.nextGoals);
            CreateReferenceEffectPanel(main, "BuffPanel", "AKTIVE BUFFS", data.activeBuffs, new Vector2(-390f, -270f), new Vector2(380f, 192f), new Color32(64, 156, 72, 255));
            CreateReferenceEffectPanel(main, "RiskPanel", "AKTIVE RISIKEN", data.activeRisks, new Vector2(60f, -270f), new Vector2(450f, 192f), new Color32(167, 54, 45, 255), RequestAllRisks, "ALLE RISIKEN");
            CreateReferenceRunPanel(main, data.lastRunStats);
        }

        if (showAllGoalsOverlay)
            CreateAllGoalsOverlay(main, data.nextGoals);

        if (showAllRisksOverlay)
            CreateAllRisksOverlay(main, data.activeRisks);
    }

    private void DestroyCanvasChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null || child.name != childName)
                continue;

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void CreateMainMenuUnlockContent(Transform main, MetaHubData data)
    {
        switch (selectedNavigationId)
        {
            case "general":
                if (runtimeUnlockInfoMode)
                    CreateRuntimeResourcesContent(main, data);
                else
                    CreateUnlockGeneralContent(main, data);
                break;
            case "tower":
                if (runtimeUnlockInfoMode)
                    CreateRuntimeTowerContent(main, data);
                else
                    CreateUnlockTowerContent(main, data);
                break;
            case "chaos":
                if (runtimeUnlockInfoMode)
                    CreateRuntimeRiskContent(main, data);
                else
                    CreateUnlockChaosContent(main, data);
                break;
            case "path":
                if (runtimeUnlockInfoMode)
                    CreateRuntimePathContent(main, data);
                else
                    CreateUnlockPathContent(main, data);
                break;
            case "elite":
                if (runtimeUnlockInfoMode)
                    CreateRuntimeEliteContent(main, data);
                else
                    CreateUnlockEliteContent(main, data);
                break;
            default:
                if (runtimeUnlockInfoMode)
                    CreateRuntimeOverviewContent(main, data);
                else
                    CreateUnlockOverviewContent(main, data);
                break;
        }
    }

    private void CreateUnlockOverviewContent(Transform main, MetaHubData data)
    {
        for (int i = 0; i < data.metricCards.Count && i < 5; i++)
            CreateReferenceMetricCard(main, data.metricCards[i], i);

        float mainWidth = GetPanelWidth(main, 1216f);
        float left = -mainWidth * 0.5f + 10f;
        float right = mainWidth * 0.5f - 10f;
        float progressWidth = 404f;
        float rowGap = 16f;
        float progressCenterX = left + progressWidth * 0.5f;
        float goalsLeft = progressCenterX + progressWidth * 0.5f + rowGap;
        float goalsWidth = Mathf.Max(360f, right - goalsLeft);

        CreateReferenceProgressPanel(main, data, new Vector2(progressCenterX, -10f), new Vector2(progressWidth, 296f));
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(goalsLeft + goalsWidth * 0.5f, -10f), new Vector2(goalsWidth, 296f));
        CreateReferenceRunPanel(main, data.lastRunStats, new Vector2((left + right) * 0.5f, -270f), new Vector2(right - left, 192f));
    }

    private void CreateRuntimeOverviewContent(Transform main, MetaHubData data)
    {
        for (int i = 0; i < data.metricCards.Count && i < 5; i++)
            CreateReferenceMetricCard(main, data.metricCards[i], i);

        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float gap = 18f;
        float statusWidth = 395f;
        float waveWidth = 435f;
        float pathWidth = Mathf.Max(360f, mainWidth - 44f - statusWidth - waveWidth - gap * 2f);
        float statusCenterX = left + statusWidth * 0.5f;
        float waveCenterX = left + statusWidth + gap + waveWidth * 0.5f;
        float pathCenterX = left + statusWidth + gap + waveWidth + gap + pathWidth * 0.5f;
        float topY = -10f;
        float topHeight = 296f;
        float bottomY = -270f;
        float bottomHeight = 192f;

        RunStatistics runStats = gameManager != null ? gameManager.GetRunStatistics() : null;
        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;
        int currentGold = GetRuntimeGold();
        int currentLives = GetRuntimeLives();
        int currentWave = GetRuntimeWaveNumber();
        int completedWaves = history != null ? Mathf.Max(0, history.GetCompletedWaveCount()) : 0;
        int totalKills = history != null ? Mathf.Max(0, history.GetTotalKills()) : 0;
        int totalLeaks = history != null ? Mathf.Max(0, history.GetTotalLeaks()) : 0;
        int baseDamage = history != null ? Mathf.Max(0, history.GetTotalBaseDamageTaken()) : 0;
        int towersBuilt = runStats != null ? Mathf.Max(0, runStats.towersBuilt) : 0;

        Transform runPanel = CreateUnlockPanel(main, "RuntimeRunState", "RUN-LAGE", new Vector2(statusCenterX, topY), new Vector2(statusWidth, topHeight), new Color32(126, 98, 51, 255));
        float statusRowWidth = statusWidth - 70f;
        CreateUnlockCompactRow(runPanel, "RuntimePhase", "icon_shield", "Phase", GetRuntimeBuildStateLabel(), GetRuntimePhaseShortLabel(), MetaHubTone.Gold, 66f, statusRowWidth);
        CreateUnlockCompactRow(runPanel, "RuntimeResources", "icon_gold", "Ressourcen", "Gold / Leben", MetaHubMockData.FormatNumber(currentGold) + "/" + MetaHubMockData.FormatNumber(currentLives), MetaHubTone.Gold, 22f, statusRowWidth);
        CreateUnlockCompactRow(runPanel, "RuntimeProgress", "icon_tower", "Run-Fortschritt", "Welle / gebaut", currentWave + "/" + towersBuilt, MetaHubTone.Purple, -22f, statusRowWidth);
        CreateUnlockCompactRow(runPanel, "RuntimeDamage", "icon_skull", "Gefahren", "Leaks / Base", totalLeaks + "/" + baseDamage, MetaHubTone.Red, -66f, statusRowWidth);

        Transform wavePanel = CreateUnlockPanel(main, "RuntimeNextWave", "NAECHSTE WELLE", new Vector2(waveCenterX, topY), new Vector2(waveWidth, topHeight), new Color32(128, 83, 176, 255));
        float waveRowWidth = waveWidth - 70f;
        CreateUnlockCompactRow(wavePanel, "RuntimeWaveName", "icon_path", Shorten(GetRuntimeNextWaveName(), 28), "Preview aus WaveData", GetRuntimeNextWaveSpecialText(), MetaHubTone.Cyan, 66f, waveRowWidth);
        CreateUnlockCompactRow(wavePanel, "RuntimeEnemyCount", "icon_runner", "Gegner", "Naechste Welle", GetRuntimeNextWaveEnemyText(), MetaHubTone.Red, 22f, waveRowWidth);
        CreateUnlockCompactRow(wavePanel, "RuntimeRiskCount", "icon_risk_core", "Aktive Risiken", "Run-Modifikatoren", GetRuntimeRiskCount().ToString(), MetaHubTone.Purple, -22f, waveRowWidth);
        CreateUnlockRow(wavePanel, "RuntimeWaveModifier", "icon_chaos", "Zusatzinfo", GetRuntimeNextWaveModifierText(), "", MetaHubTone.Purple, -78f, waveRowWidth);

        Transform pathPanel = CreateUnlockPanel(main, "RuntimePathState", "VERBAU / EINGABEN", new Vector2(pathCenterX, topY), new Vector2(pathWidth, topHeight), new Color32(47, 169, 210, 255));
        float pathRowWidth = pathWidth - 70f;
        CreateUnlockCompactRow(pathPanel, "RuntimeBuildState", "icon_path", "Bauzustand", GetRuntimeBuildStateLabel(), GetRuntimeBuildStateValue(), MetaHubTone.Cyan, 66f, pathRowWidth);
        CreateUnlockCompactRow(pathPanel, "RuntimePathChoice", "icon_blueprint", "Wegbau-Auswahl", "Ungueltige starten nicht", IsRuntimePathChoiceOpen() ? "Offen" : "Zu", MetaHubTone.Blue, 22f, pathRowWidth);
        CreateUnlockCompactRow(pathPanel, "RuntimeModalLock", "icon_shield", "Eingaben", "F1 sperrt andere UI", "Aktiv", MetaHubTone.Gold, -22f, pathRowWidth);
        CreateUnlockRow(pathPanel, "RuntimeReadOnly", "icon_keystone", "Meta-Shop", "Nur im Hauptmenue.", "Info", MetaHubTone.Neutral, -78f, pathRowWidth);

        CreateReferenceEffectPanel(main, "RuntimeBuffPanel", "AKTIVE BUFFS", data.activeBuffs, new Vector2(statusCenterX, bottomY), new Vector2(statusWidth, bottomHeight), new Color32(64, 156, 72, 255));
        CreateReferenceEffectPanel(main, "RuntimeRiskPanel", "AKTIVE RISIKEN", data.activeRisks, new Vector2(waveCenterX, bottomY), new Vector2(waveWidth, bottomHeight), new Color32(167, 54, 45, 255), RequestAllRisks, "ALLE RISIKEN");
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(pathCenterX, bottomY), new Vector2(pathWidth, bottomHeight));
    }

    private void CreateRuntimeResourcesContent(Transform main, MetaHubData data)
    {
        RunStatistics runStats = gameManager != null ? gameManager.GetRunStatistics() : null;
        ChaosJusticeManager chaosJustice = gameManager != null ? gameManager.GetChaosJusticeManager() : null;
        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float gap = 18f;
        float topY = 214f;
        float topHeight = 172f;
        float cardWidth = (right - left - gap * 2f) / 3f;
        int maxLives = gameManager != null ? gameManager.GetCurrentMaxLives() : GetRuntimeLives();

        CreateUnlockStatCard(main, "RuntimeGold", "GOLD", MetaHubMockData.FormatNumber(GetRuntimeGold()), "Aktuell", "icon_gold", MetaHubTone.Gold, new Vector2(left + cardWidth * 0.5f, topY), new Vector2(cardWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeLives", "LEBEN", GetRuntimeLives() + " / " + Mathf.Max(1, maxLives), "Base", "icon_xp", MetaHubTone.Cyan, new Vector2(left + cardWidth + gap + cardWidth * 0.5f, topY), new Vector2(cardWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeWave", "WELLE", MetaHubMockData.FormatNumber(GetRuntimeWaveNumber()), GetRuntimePhaseShortLabel(), "icon_path", MetaHubTone.Blue, new Vector2(left + (cardWidth + gap) * 2f + cardWidth * 0.5f, topY), new Vector2(cardWidth, topHeight));

        float panelWidth = (right - left - gap) * 0.5f;
        Transform economy = CreateUnlockPanel(main, "RuntimeEconomy", "WIRTSCHAFT", new Vector2(left + panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(126, 98, 51, 255));
        float rowWidth = panelWidth - 70f;
        CreateUnlockCompactRow(economy, "RuntimeEarned", "icon_gold", "Verdient", "Gold im Run", runStats != null ? MetaHubMockData.FormatNumber(runStats.totalGoldEarned) : "0", MetaHubTone.Gold, 82f, rowWidth);
        CreateUnlockCompactRow(economy, "RuntimeSpent", "icon_gold", "Ausgegeben", "Gold im Run", runStats != null ? MetaHubMockData.FormatNumber(runStats.totalGoldSpent) : "0", MetaHubTone.Red, 38f, rowWidth);
        CreateUnlockCompactRow(economy, "RuntimeNet", "icon_gold", "Netto", "Verdient - ausgegeben", runStats != null ? MetaHubMockData.FormatNumber(runStats.GetNetGoldDelta()) : "0", MetaHubTone.Cyan, -6f, rowWidth);
        CreateUnlockCompactRow(economy, "RuntimeSources", "icon_runner", "Quellen", "Kill / Wave / Verbau", runStats != null ? runStats.goldFromEnemyKills + "/" + runStats.goldFromWaveCompletion + "/" + runStats.goldFromBlockedEvents : "0/0/0", MetaHubTone.Green, -50f, rowWidth);
        CreateUnlockCompactRow(economy, "RuntimeRewardBonus", "icon_justice", "Bonus", "Reward-Mods", runStats != null ? "+" + MetaHubMockData.FormatNumber(runStats.goldBonusFromRewardModifiers) : "+0", MetaHubTone.Gold, -94f, rowWidth);

        Transform justice = CreateUnlockPanel(main, "RuntimeJustice", "GERECHTIGKEIT", new Vector2(right - panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(128, 83, 176, 255));
        int goldJustice = chaosJustice != null && chaosJustice.runData != null ? Mathf.Max(0, chaosJustice.runData.goldJusticeLevel) : 0;
        int xpJustice = chaosJustice != null && chaosJustice.runData != null ? Mathf.Max(0, chaosJustice.runData.xpJusticeLevel) : 0;
        float goldRewardMultiplier = GetRuntimeGoldRewardMultiplier(chaosJustice);
        float xpRewardMultiplier = GetRuntimeXPRewardMultiplier(chaosJustice);
        CreateUnlockCompactRow(justice, "RuntimeGoldJustice", "icon_justice", "Gold-Justice", "Gold-Multi", goldJustice.ToString(), MetaHubTone.Gold, 82f, rowWidth);
        CreateUnlockCompactRow(justice, "RuntimeXpJustice", "icon_xp", "XP-Justice", "XP-Multi", xpJustice.ToString(), MetaHubTone.Cyan, 38f, rowWidth);
        CreateUnlockCompactRow(justice, "RuntimeGoldMultiplier", "icon_gold", "Gold-Multi", "Aktiver Reward-Wert", FormatRuntimeMultiplier(goldRewardMultiplier), MetaHubTone.Gold, -6f, rowWidth);
        CreateUnlockCompactRow(justice, "RuntimeXpMultiplier", "icon_buff_xp", "XP-Multi", "Aktiver Reward-Wert", FormatRuntimeMultiplier(xpRewardMultiplier), MetaHubTone.Cyan, -50f, rowWidth);
        CreateUnlockCompactRow(justice, "RuntimeBalance", "icon_chaos", "Balance", GetRuntimeRunStyleShortLabel(chaosJustice), chaosJustice != null ? chaosJustice.GetBalanceSnapshot().chaosPercent + "% C" : "0% C", MetaHubTone.Purple, -94f, rowWidth);

        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;
        int nextReward = gameManager != null ? Mathf.Max(0, gameManager.GetWaveCompletionGoldPreview(GetRuntimeWaveNumber() + 1)) : 0;
        CreateRuntimeSummaryPanel(main, "RuntimeResourceSnapshot", "RUN-SNAPSHOT", new Vector2((left + right) * 0.5f, -306f), new Vector2(right - left, 118f),
            CreateRuntimeSummaryItem("icon_runner", "Kills", "Im Run", history != null ? MetaHubMockData.FormatNumber(history.GetTotalKills()) : "0", MetaHubTone.Red),
            CreateRuntimeSummaryItem("icon_shield", "Leaks/Base", "Schaden", history != null ? history.GetTotalLeaks() + "/" + history.GetTotalBaseDamageTaken() : "0/0", MetaHubTone.Cyan),
            CreateRuntimeSummaryItem("icon_gold", "Belohnung", "Wave-Gold", "+" + MetaHubMockData.FormatNumber(nextReward), MetaHubTone.Gold),
            CreateRuntimeSummaryItem("icon_path", "Wellen", "Fertig", history != null ? history.GetCompletedWaveCount().ToString() : "0", MetaHubTone.Blue));
    }

    private void CreateRuntimeTowerContent(Transform main, MetaHubData data)
    {
        RunStatistics runStats = gameManager != null ? gameManager.GetRunStatistics() : null;
        RunStatisticsTracker tracker = gameManager != null ? gameManager.GetRunStatisticsTracker() : null;
        RunTowerStatsRecord best = tracker != null ? tracker.GetStrongestTowerRecord() : null;
        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float gap = 18f;
        float topY = 214f;
        float topHeight = 172f;
        float statWidth = (right - left - gap * 3f) * 0.25f;

        CreateUnlockStatCard(main, "RuntimeTowersBuilt", "GEBAUT", runStats != null ? runStats.towersBuilt.ToString() : "0", "Tower", "icon_tower", MetaHubTone.Purple, new Vector2(left + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeTowerXP", "TOWER-XP", runStats != null ? MetaHubMockData.FormatNumber(runStats.totalTowerXPGranted) : "0", "Im Run", "icon_buff_xp", MetaHubTone.Cyan, new Vector2(left + statWidth + gap + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeTowerLevel", "TOP-LEVEL", runStats != null ? runStats.highestTowerLevel.ToString() : "1", "Hoechster Tower", "icon_keystone_purple", MetaHubTone.Gold, new Vector2(left + (statWidth + gap) * 2f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeEliteDestroyed", "ZERSTOERT", runStats != null ? runStats.towersDestroyedByElite.ToString() : "0", "Nur Elite", "icon_skull", MetaHubTone.Red, new Vector2(left + (statWidth + gap) * 3f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));

        float panelWidth = (right - left - gap) * 0.5f;
        Transform progress = CreateUnlockPanel(main, "RuntimeTowerProgress", "TOWER-FORTSCHRITT", new Vector2(left + panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(128, 83, 176, 255));
        float rowWidth = panelWidth - 70f;
        CreateUnlockCompactRow(progress, "RuntimeTowerLevelUps", "icon_tower", "Level-Ups", "Tower im Run", runStats != null ? runStats.towerLevelUps.ToString() : "0", MetaHubTone.Purple, 82f, rowWidth);
        CreateUnlockCompactRow(progress, "RuntimeUpgradePoints", "icon_keystone", "Upgrade-Pkt.", "Verdient / genutzt", runStats != null ? runStats.upgradePointsEarned + "/" + runStats.upgradePointsSpent : "0/0", MetaHubTone.Gold, 38f, rowWidth);
        CreateUnlockCompactRow(progress, "RuntimeMetaPoints", "icon_keystone_purple", "Meta-Punkte", "Vorbereitet", runStats != null ? runStats.metaPointsPrepared.ToString() : "0", MetaHubTone.Purple, -6f, rowWidth);
        CreateUnlockCompactRow(progress, "RuntimeGoldUpgrades", "icon_gold", "Gold-Upgrades", "Gekauft", runStats != null ? runStats.goldUpgradesBought.ToString() : "0", MetaHubTone.Gold, -50f, rowWidth);
        CreateUnlockCompactRow(progress, "RuntimePointUpgrades", "icon_blue_star", "Punkt-Upgrades", "Gekauft", runStats != null ? runStats.pointUpgradesBought.ToString() : "0", MetaHubTone.Blue, -94f, rowWidth);

        Transform bestPanel = CreateUnlockPanel(main, "RuntimeBestTower", "STAERKSTER TOWER", new Vector2(right - panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(47, 169, 210, 255));
        string bestName = best != null ? Shorten(best.towerName, 28) : "Noch keiner";
        string bestRole = best != null ? TowerMasteryManager.GetTowerDisplayName(best.towerRole) : "Keine Daten";
        CreateUnlockCompactRow(bestPanel, "RuntimeBestName", "icon_tower", bestName, bestRole, best != null ? "Lv " + best.highestLevel : "-", MetaHubTone.Cyan, 82f, rowWidth);
        CreateUnlockCompactRow(bestPanel, "RuntimeBestKills", "icon_skull", "Kills / Assists", "Kampfleistung", best != null ? best.totalKills + "/" + best.totalAssists : "0/0", MetaHubTone.Red, 38f, rowWidth);
        CreateUnlockCompactRow(bestPanel, "RuntimeBestDamage", "icon_chaos_sun", "Schaden", "Gerundet", best != null ? MetaHubMockData.FormatNumber(Mathf.RoundToInt(best.totalDamageDealt)) : "0", MetaHubTone.Gold, -6f, rowWidth);
        CreateUnlockCompactRow(bestPanel, "RuntimeBestScore", "icon_keystone", "Impact", "Score", best != null ? best.GetImpactScore().ToString() : "0", MetaHubTone.Purple, -50f, rowWidth);
        CreateUnlockCompactRow(bestPanel, "RuntimeLastEliteTower", "icon_shield", "Letzter Verlust", "Elite-exklusiv", runStats != null && !string.IsNullOrEmpty(runStats.lastEliteDestroyedTowerName) ? Shorten(runStats.lastEliteDestroyedTowerName, 14) : "Keiner", MetaHubTone.Red, -94f, rowWidth);

        CreateRuntimeSummaryPanel(main, "RuntimeTowerSnapshot", "RUN-SNAPSHOT", new Vector2((left + right) * 0.5f, -306f), new Vector2(right - left, 118f),
            CreateRuntimeSummaryItem("icon_tower", "Gebaut", "Tower", runStats != null ? runStats.towersBuilt.ToString() : "0", MetaHubTone.Purple),
            CreateRuntimeSummaryItem("icon_buff_xp", "Tower-XP", "Im Run", runStats != null ? MetaHubMockData.FormatNumber(runStats.totalTowerXPGranted) : "0", MetaHubTone.Cyan),
            CreateRuntimeSummaryItem("icon_keystone", "Upgrade-Pkt.", "Verd./gen.", runStats != null ? runStats.upgradePointsEarned + "/" + runStats.upgradePointsSpent : "0/0", MetaHubTone.Gold),
            CreateRuntimeSummaryItem("icon_skull", "Elite-Verlust", "Tower", runStats != null ? runStats.towersDestroyedByElite.ToString() : "0", MetaHubTone.Red));
    }

    private void CreateRuntimeRiskContent(Transform main, MetaHubData data)
    {
        ChaosJusticeManager chaosJustice = gameManager != null ? gameManager.GetChaosJusticeManager() : null;
        ChaosJusticeBalanceSnapshot snapshot = chaosJustice != null ? chaosJustice.GetBalanceSnapshot() : new ChaosJusticeBalanceSnapshot();
        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float gap = 18f;
        float topY = 214f;
        float topHeight = 172f;
        float statWidth = (right - left - gap * 3f) * 0.25f;

        CreateUnlockStatCard(main, "RuntimeChaosLevel", "CHAOS", chaosJustice != null ? chaosJustice.GetChaosLevel().ToString() : "0", "Level", "icon_chaos", MetaHubTone.Purple, new Vector2(left + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeRiskCount", "RISIKEN", GetRuntimeRiskCount().ToString(), "Aktiv", "icon_risk_core", MetaHubTone.Red, new Vector2(left + statWidth + gap + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeRiskPressure", "DRUCK", chaosJustice != null ? chaosJustice.GetTotalRiskPressureScore().ToString() : "0", "Risk-Score", "icon_spawn", MetaHubTone.Red, new Vector2(left + (statWidth + gap) * 2f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeChaosPercent", "BALANCE", snapshot.chaosPercent + "%", snapshot.label, "icon_justice", MetaHubTone.Gold, new Vector2(left + (statWidth + gap) * 3f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));

        float leftPanelWidth = (right - left - gap) * 0.5f;
        Transform summary = CreateUnlockPanel(main, "RuntimeRiskSummary", "RISIKO-ZUSAMMENFASSUNG", new Vector2(left + leftPanelWidth * 0.5f, -45f), new Vector2(leftPanelWidth, 330f), new Color32(128, 83, 176, 255));
        float rowWidth = leftPanelWidth - 70f;
        int goldJustice = chaosJustice != null && chaosJustice.runData != null ? Mathf.Max(0, chaosJustice.runData.goldJusticeLevel) : 0;
        int xpJustice = chaosJustice != null && chaosJustice.runData != null ? Mathf.Max(0, chaosJustice.runData.xpJusticeLevel) : 0;
        CreateUnlockCompactRow(summary, "RuntimeRiskStyle", "icon_chaos", "Run-Stil", GetRuntimeRunStyleShortLabel(chaosJustice), chaosJustice != null ? chaosJustice.GetChaosLevel().ToString() : "0", MetaHubTone.Purple, 82f, rowWidth);
        CreateUnlockCompactRow(summary, "RuntimeDecisionMix", "icon_justice", "Entscheidungen", GetRuntimeDecisionShortText(chaosJustice), goldJustice + "/" + xpJustice, MetaHubTone.Gold, 38f, rowWidth);
        CreateUnlockCompactRow(summary, "RuntimeRiskGroups", "icon_risk_core", "Gruppen", GetRuntimeRiskGroupShortText(chaosJustice), GetRuntimeRiskCount().ToString(), MetaHubTone.Red, -6f, rowWidth);
        CreateUnlockCompactRow(summary, "RuntimeNextRiskWave", "icon_path", "Naechste Welle", GetRuntimeNextWaveModifierText(), GetRuntimeNextWaveSpecialText(), MetaHubTone.Cyan, -50f, rowWidth);
        CreateUnlockCompactRow(summary, "RuntimeRiskRewards", "icon_gold", "Reward-Multi", "Gold / XP", FormatRuntimeMultiplier(GetRuntimeGoldRewardMultiplier(chaosJustice)) + "/" + FormatRuntimeMultiplier(GetRuntimeXPRewardMultiplier(chaosJustice)), MetaHubTone.Gold, -94f, rowWidth);

        CreateReferenceEffectPanel(main, "RuntimeRiskListPanel", "AKTIVE RISIKEN", data.activeRisks, new Vector2(right - leftPanelWidth * 0.5f, 5f), new Vector2(leftPanelWidth, 230f), new Color32(167, 54, 45, 255), RequestAllRisks, "ALLE RISIKEN");
        Transform hint = CreateUnlockPanel(main, "RuntimeRiskHint", "REGELN", new Vector2(right - leftPanelWidth * 0.5f, -201f), new Vector2(leftPanelWidth, 132f), new Color32(122, 94, 55, 255));
        CreateRuntimeRuleRow(hint, "RuntimeRiskRuleVisible", "icon_risk_core", "Offene Risiken", "Keine verdeckten Nachteile", "Sichtbar", MetaHubTone.Red, 16f, rowWidth);
        CreateRuntimeRuleRow(hint, "RuntimeRiskRuleBoss", "icon_chaos_sun", "Boss/MiniBoss", "Kein Towerverlust", "Sicher", MetaHubTone.Purple, -17f, rowWidth);
        CreateRuntimeRuleRow(hint, "RuntimeRiskRuleElite", "icon_shield", "Tower-Verlust", "Bleibt Elite-exklusiv", "Elite", MetaHubTone.Cyan, -50f, rowWidth);

        CreateRuntimeSummaryPanel(main, "RuntimeRiskSnapshot", "RISIKO-PRUEFUNG", new Vector2(left + leftPanelWidth * 0.5f, -306f), new Vector2(leftPanelWidth, 118f),
            CreateRuntimeSummaryItem("icon_risk_core", "Aktiv", "Risiken", GetRuntimeRiskCount().ToString(), MetaHubTone.Red),
            CreateRuntimeSummaryItem("icon_justice", "Balance", GetRuntimeRunStyleShortLabel(chaosJustice), snapshot.chaosPercent + "% C", MetaHubTone.Gold));
    }

    private void CreateRuntimePathContent(Transform main, MetaHubData data)
    {
        RunStatistics runStats = gameManager != null ? gameManager.GetRunStatistics() : null;
        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float gap = 18f;
        float topY = 214f;
        float topHeight = 172f;
        float statWidth = (right - left - gap * 3f) * 0.25f;

        CreateUnlockStatCard(main, "RuntimePathPhase", "PHASE", GetRuntimePhaseShortLabel(), GetRuntimeBuildStateLabel(), "icon_path", MetaHubTone.Cyan, new Vector2(left + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimePathChoiceState", "WEGBAU", IsRuntimePathChoiceOpen() ? "Offen" : "Zu", "Auswahl", "icon_blueprint", MetaHubTone.Blue, new Vector2(left + statWidth + gap + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeBlockedEvents", "VERBAU", runStats != null ? runStats.blockedEventsChosen.ToString() : "0", "Events", "icon_shield", MetaHubTone.Gold, new Vector2(left + (statWidth + gap) * 2f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeBlockedTimer", "TIMER", GetRuntimeBuildStateValue(), "Bauzustand", "icon_spawn", MetaHubTone.Red, new Vector2(left + (statWidth + gap) * 3f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));

        float panelWidth = (right - left - gap) * 0.5f;
        Transform status = CreateUnlockPanel(main, "RuntimePathStatus", "VERBAU-STATUS", new Vector2(left + panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(47, 169, 210, 255));
        float rowWidth = panelWidth - 70f;
        CreateUnlockCompactRow(status, "RuntimePathBuildState", "icon_path", "Bauzustand", GetRuntimeBuildStateLabel(), GetRuntimeBuildStateValue(), MetaHubTone.Cyan, 82f, rowWidth);
        CreateUnlockCompactRow(status, "RuntimePathInput", "icon_shield", "Modal-Lock", "Andere UI gesperrt", "Aktiv", MetaHubTone.Gold, 38f, rowWidth);
        CreateUnlockCompactRow(status, "RuntimePathInvalid", "icon_risk_core", "Ungueltige Richtung", "Startet keine Wave", "Sicher", MetaHubTone.Green, -6f, rowWidth);
        CreateUnlockCompactRow(status, "RuntimePathNext", "icon_runner", "Naechste Welle", GetRuntimeNextWaveName(), GetRuntimeNextWaveEnemyText(), MetaHubTone.Blue, -50f, rowWidth);
        CreateUnlockCompactRow(status, "RuntimePathPhaseRule", "icon_keystone", "OnPathExtended", "Nur nach gueltigem Wegbau", "1x", MetaHubTone.Cyan, -94f, rowWidth);

        Transform rewards = CreateUnlockPanel(main, "RuntimePathRewards", "VERBAU-REWARDS", new Vector2(right - panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(126, 98, 51, 255));
        string lastEvent = runStats != null && !string.IsNullOrEmpty(runStats.lastBlockedEventName) ? Shorten(runStats.lastBlockedEventName, 24) : "Keines";
        CreateUnlockCompactRow(rewards, "RuntimeBlockedGold", "icon_gold", "Gold aus Verbau", "Run-Statistik", runStats != null ? MetaHubMockData.FormatNumber(runStats.goldFromBlockedEvents) : "0", MetaHubTone.Gold, 82f, rowWidth);
        CreateUnlockCompactRow(rewards, "RuntimeBlockedLives", "icon_xp", "Leben aus Verbau", "Run-Statistik", runStats != null ? runStats.lifeFromBlockedEvents.ToString() : "0", MetaHubTone.Cyan, 38f, rowWidth);
        CreateUnlockCompactRow(rewards, "RuntimeBlockedPhases", "icon_path", "Timer-Phasen", "Anzahl", runStats != null ? runStats.timedBlockedBuildPhases.ToString() : "0", MetaHubTone.Blue, -6f, rowWidth);
        CreateUnlockCompactRow(rewards, "RuntimeBlockedDuration", "icon_spawn", "Timer gesamt", "Sekunden", runStats != null ? Mathf.RoundToInt(runStats.totalBlockedBuildPhaseDuration).ToString() : "0", MetaHubTone.Red, -50f, rowWidth);
        CreateUnlockCompactRow(rewards, "RuntimeLastBlockedEvent", "icon_keystone", "Letztes Event", runStats != null ? runStats.lastBlockedEventType : "", lastEvent, MetaHubTone.Gold, -94f, rowWidth);

        CreateRuntimeSummaryPanel(main, "RuntimePathSnapshot", "RUN-SNAPSHOT", new Vector2((left + right) * 0.5f, -306f), new Vector2(right - left, 118f),
            CreateRuntimeSummaryItem("icon_path", "Phase", GetRuntimeBuildStateLabel(), GetRuntimePhaseShortLabel(), MetaHubTone.Cyan),
            CreateRuntimeSummaryItem("icon_blueprint", "Wegbau", "Auswahl", IsRuntimePathChoiceOpen() ? "Offen" : "Zu", MetaHubTone.Blue),
            CreateRuntimeSummaryItem("icon_runner", "Welle", GetRuntimeNextWaveName(), GetRuntimeNextWaveEnemyText(), MetaHubTone.Gold),
            CreateRuntimeSummaryItem("icon_shield", "Events", "Gewaehlt", runStats != null ? runStats.blockedEventsChosen.ToString() : "0", MetaHubTone.Red));
    }

    private void CreateRuntimeEliteContent(Transform main, MetaHubData data)
    {
        RunStatistics runStats = gameManager != null ? gameManager.GetRunStatistics() : null;
        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;
        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float gap = 18f;
        float topY = 214f;
        float topHeight = 172f;
        float statWidth = (right - left - gap * 3f) * 0.25f;

        CreateUnlockStatCard(main, "RuntimeEliteKills", "ELITE-KILLS", runStats != null ? runStats.eliteKills.ToString() : "0", "Im Run", "icon_skull", MetaHubTone.Red, new Vector2(left + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeEliteLeaks", "ELITE-LEAKS", runStats != null ? runStats.eliteLeaks.ToString() : "0", "Durchbruch", "icon_risk_core", MetaHubTone.Red, new Vector2(left + statWidth + gap + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeEliteRewards", "REWARDS", runStats != null ? runStats.eliteRewardsChosen.ToString() : "0", "Gewaehlt", "icon_gold", MetaHubTone.Gold, new Vector2(left + (statWidth + gap) * 2f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));
        CreateUnlockStatCard(main, "RuntimeBossKills", "BOSS", history != null ? history.GetBossKills().ToString() : "0", "Besiegt", "icon_chaos_sun", MetaHubTone.Purple, new Vector2(left + (statWidth + gap) * 3f + statWidth * 0.5f, topY), new Vector2(statWidth, topHeight));

        float panelWidth = (right - left - gap) * 0.5f;
        Transform elite = CreateUnlockPanel(main, "RuntimeEliteState", "ELITE-STATUS", new Vector2(left + panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(167, 54, 45, 255));
        float rowWidth = panelWidth - 70f;
        CreateUnlockCompactRow(elite, "RuntimeEliteWaves", "icon_skull", "Elite-Waves", "Abgeschlossen", runStats != null ? runStats.eliteWavesCompleted.ToString() : "0", MetaHubTone.Red, 82f, rowWidth);
        CreateUnlockCompactRow(elite, "RuntimeEliteDestroy", "icon_shield", "Tower zerstoert", "Elite-exklusiv", runStats != null ? runStats.towersDestroyedByElite.ToString() : "0", MetaHubTone.Red, 38f, rowWidth);
        CreateUnlockCompactRow(elite, "RuntimeEliteLastTower", "icon_tower", "Letzter Verlust", "Durch Elite", runStats != null && !string.IsNullOrEmpty(runStats.lastEliteDestroyedTowerName) ? Shorten(runStats.lastEliteDestroyedTowerName, 14) : "Keiner", MetaHubTone.Cyan, -6f, rowWidth);
        CreateUnlockCompactRow(elite, "RuntimeEliteLastReward", "icon_gold", "Letzte Belohnung", "Elite-Reward", runStats != null && !string.IsNullOrEmpty(runStats.lastEliteRewardName) ? Shorten(runStats.lastEliteRewardName, 14) : "Keine", MetaHubTone.Gold, -50f, rowWidth);
        CreateUnlockCompactRow(elite, "RuntimeEliteNextWave", "icon_path", "Spezialwelle", GetRuntimeNextWaveName(), GetRuntimeNextWaveSpecialText(), MetaHubTone.Purple, -94f, rowWidth);

        Transform bosses = CreateUnlockPanel(main, "RuntimeBossState", "BOSS / MINIBOSS", new Vector2(right - panelWidth * 0.5f, -45f), new Vector2(panelWidth, 330f), new Color32(128, 83, 176, 255));
        CreateUnlockCompactRow(bosses, "RuntimeBossWaves", "icon_chaos_sun", "Boss-Waves", "Abgeschlossen", history != null ? history.GetBossWavesCompleted().ToString() : "0", MetaHubTone.Purple, 82f, rowWidth);
        CreateUnlockCompactRow(bosses, "RuntimeMiniBossWaves", "icon_skull", "MiniBoss-Waves", "Abgeschlossen", history != null ? history.GetMiniBossWavesCompleted().ToString() : "0", MetaHubTone.Red, 38f, rowWidth);
        CreateUnlockCompactRow(bosses, "RuntimeMiniBossKills", "icon_skull", "MiniBoss-Kills", "Besiegt", history != null ? history.GetMiniBossKills().ToString() : "0", MetaHubTone.Red, -6f, rowWidth);
        CreateUnlockCompactRow(bosses, "RuntimeEliteHistory", "icon_risk_core", "Elite-Historie", "Historie", history != null ? history.GetEliteWavesCompleted() + "/" + history.GetEliteKills() : "0/0", MetaHubTone.Gold, -50f, rowWidth);
        CreateUnlockCompactRow(bosses, "RuntimeBossRule", "icon_shield", "Regel", "Kein Towerverlust", "Sicher", MetaHubTone.Cyan, -94f, rowWidth);

        CreateRuntimeSummaryPanel(main, "RuntimeEliteSnapshot", "RUN-SNAPSHOT", new Vector2((left + right) * 0.5f, -306f), new Vector2(right - left, 118f),
            CreateRuntimeSummaryItem("icon_skull", "Elite-Kills", "Im Run", runStats != null ? runStats.eliteKills.ToString() : "0", MetaHubTone.Red),
            CreateRuntimeSummaryItem("icon_risk_core", "Elite-Leaks", "Durchbruch", runStats != null ? runStats.eliteLeaks.ToString() : "0", MetaHubTone.Red),
            CreateRuntimeSummaryItem("icon_chaos_sun", "Boss-Kills", "Historie", history != null ? history.GetBossKills().ToString() : "0", MetaHubTone.Purple),
            CreateRuntimeSummaryItem("icon_path", "Spezialwelle", GetRuntimeNextWaveName(), GetRuntimeNextWaveSpecialText(), MetaHubTone.Cyan));
    }

    private void CreateUnlockGeneralContent(Transform main, MetaHubData data)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float topY = 216f;
        float topPanelHeight = 180f;

        Transform account = CreateUnlockPanel(main, "GeneralAccount", "ACCOUNT", new Vector2(left + 175f, topY), new Vector2(350f, topPanelHeight), new Color32(126, 98, 51, 255));
        CreateDonutImage(account, "AccountRing", new Vector2(-108f, -13f), 70f, Percent(data.account.currentXP, data.account.requiredXP), new Color32(55, 209, 235, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(account, "Level", data.account.level.ToString(), new Vector2(-108f, -13f), new Vector2(54f, 26f), 19f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(account, "LevelCaption", "ACCOUNT LEVEL", new Vector2(-108f, -56f), new Vector2(112f, 18f), 8f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(account, "AccountXP", MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP", new Vector2(66f, 30f), new Vector2(160f, 18f), 11f, new Color32(224, 210, 184, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasBar(account, "AccountBar", new Vector2(70f, 10f), new Vector2(142f, 6f), Percent(data.account.currentXP, data.account.requiredXP), new Color32(55, 209, 235, 255));
        string accountInfo = runtimeUnlockInfoMode
            ? "Gold  " + MetaHubMockData.FormatNumber(GetRuntimeGold()) + "\nLeben  " + MetaHubMockData.FormatNumber(GetRuntimeLives())
            : "Kernwissen  " + (general != null ? MetaHubMockData.FormatNumber(general.kernwissen) : "0") +
                "\nLoadout  " + (general != null ? general.GetUsedLoadoutSlots() + " / " + general.GetLoadoutSlotCapacity() : "0 / 0");
        CreateCanvasLabel(account, "Bonus", accountInfo, new Vector2(82f, -34f), new Vector2(170f, 38f), 10f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Normal);

        float loadoutLeft = left + 366f;
        float loadoutWidth = right - loadoutLeft;
        Transform loadout = CreateUnlockPanel(main, "Loadout", "LOADOUT", new Vector2(loadoutLeft + loadoutWidth * 0.5f, topY), new Vector2(loadoutWidth, topPanelHeight), new Color32(89, 68, 41, 255));
        int activeLoadout = general != null ? general.GetActiveLoadoutIndex() : 0;
        string loadoutName = runtimeUnlockInfoMode ? "Aktueller Run" : general != null ? general.GetLoadoutDisplayName(activeLoadout) : "Loadout 1";
        float loadoutRowWidth = loadoutWidth - 68f;
        if (runtimeUnlockInfoMode)
        {
            RunStatistics runStats = gameManager != null ? gameManager.GetRunStatistics() : null;
            CreateUnlockRow(loadout, "Loadout1", "icon_gold", loadoutName, "Welle / Gold", GetRuntimeWaveNumber() + " / " + MetaHubMockData.FormatNumber(GetRuntimeGold()), MetaHubTone.Gold, 34f, loadoutRowWidth);
            CreateUnlockRow(loadout, "Loadout2", "icon_tower", "Run-Fortschritt", "Tower-XP / Elites", (runStats != null ? Mathf.Max(0, runStats.totalTowerXPGranted).ToString() : "0") + " / " + (runStats != null ? Mathf.Max(0, runStats.eliteKills).ToString() : "0"), MetaHubTone.Green, -20f, loadoutRowWidth);
            CreateCanvasLabel(loadout, "RuntimeInfo", "Read-only: permanente Loadouts im Hauptmenue aendern.", new Vector2(0f, -68f), new Vector2(loadoutRowWidth, 20f), 9f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        }
        else
        {
            CreateUnlockRow(loadout, "Loadout1", "icon_gold", loadoutName, "Aktives Profil", general != null ? general.GetUsedLoadoutSlots() + " / " + general.GetLoadoutSlotCapacity() : "0 / 0", MetaHubTone.Gold, 34f, loadoutRowWidth);
            CreateUnlockRow(loadout, "Loadout2", "icon_tower", "Freie Slots", "Verfuegbar", general != null ? general.GetAvailableLoadoutSlots().ToString() : "0", MetaHubTone.Green, -20f, loadoutRowWidth);
            CreateCanvasButton(loadout, "ChooseGeneralLoadout", "LOADOUT WAEHLEN", new Vector2(-126f, -67f), new Vector2(210f, 28f), OpenGeneralLoadoutPicker);
            CreateCanvasButton(loadout, "EditGeneralLoadout", "BONI EINSETZEN", new Vector2(126f, -67f), new Vector2(210f, 28f), OpenGeneralLoadoutEditor);
        }

        Transform tree = CreateUnlockPanel(main, "GeneralSkillTree", "FREISCHALTBAUM", new Vector2((left + right) * 0.5f, -150f), new Vector2(right - left, 430f), new Color32(126, 98, 51, 255));
        CreateGeneralTreeTabs(tree, right - left);
        CreateGeneralFocusedSkillTree(tree, right - left);

        if (!string.IsNullOrEmpty(pendingGeneralLoadoutPromptNodeId))
            CreateGeneralLoadoutPromptOverlay(main);
        else if (showGeneralLoadoutPicker)
            CreateGeneralLoadoutPickerOverlay(main);
        else if (showGeneralLoadoutEditor)
            CreateGeneralLoadoutEditorOverlay(main);
    }

    private void CreateGeneralTreeTabs(Transform parent, float panelWidth)
    {
        List<string> categoryIds = GetVisibleGeneralTreeCategoryIds();
        if (categoryIds.Count == 0)
            return;

        if (!categoryIds.Contains(selectedGeneralTreeCategory))
            selectedGeneralTreeCategory = categoryIds[0];

        float tabWidth = Mathf.Min(190f, (panelWidth - 110f) / categoryIds.Count);
        float gap = 18f;
        float gridWidth = categoryIds.Count * tabWidth + (categoryIds.Count - 1) * gap;
        float startX = -gridWidth * 0.5f + tabWidth * 0.5f;
        for (int i = 0; i < categoryIds.Count; i++)
        {
            string categoryId = categoryIds[i];
            CreateGeneralTreeTab(parent, categoryId, GetGeneralTreeTabLabel(categoryId), GetGeneralTreeTone(categoryId), new Vector2(startX + i * (tabWidth + gap), 168f), new Vector2(tabWidth, 36f));
        }
    }

    private void CreateGeneralTreeTab(Transform parent, string categoryId, string label, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        bool selected = selectedGeneralTreeCategory == categoryId;
        Color32 toneColor = ToneColor(tone);
        Color32 fill = selected ? new Color32(54, 39, 13, 248) : new Color32(5, 18, 18, 238);
        Transform tab = CreateCanvasPanel(parent, "GeneralTreeTab_" + categoryId, position, size, fill, selected ? toneColor : new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateCanvasLabel(tab, "Label", label, Vector2.zero, size, 12f, selected ? new Color32(255, 236, 186, 255) : new Color32(224, 214, 194, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        string capturedCategoryId = categoryId;
        MakeCanvasClickable(tab, delegate { SelectGeneralTreeCategory(capturedCategoryId); }, fill, toneColor);
    }

    private void CreateGeneralFocusedSkillTree(Transform parent, float panelWidth)
    {
        GeneralMetaCategory[] categories = GetGeneralTreeCategories(selectedGeneralTreeCategory);
        MetaHubTone tone = GetGeneralTreeTone(selectedGeneralTreeCategory);
        string title = GetGeneralTreeTitle(selectedGeneralTreeCategory);
        List<GeneralMetaNodeDefinition> nodes = GetGeneralBranchNodes(categories, 99, GetGeneralTreePreferredIds(selectedGeneralTreeCategory));
        int total = runtimeUnlockInfoMode ? nodes.Count : CountGeneralBranchNodes(categories);
        int purchased = runtimeUnlockInfoMode ? nodes.Count : CountPurchasedGeneralBranchNodes(categories);
        Color32 toneColor = ToneColor(tone);

        float left = -panelWidth * 0.5f + 58f;
        CreateCanvasLabel(parent, "FocusedTreeTitle", title, new Vector2(left + 76f, 122f), new Vector2(190f, 24f), 14f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(parent, "FocusedTreeCount", GetUnlockCountText(purchased, total), new Vector2(panelWidth * 0.5f - 82f, 122f), new Vector2(100f, 20f), 10f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
        CreateLine(parent, "FocusedTreeLine", new Vector2(0f, 98f), new Vector2(panelWidth - 150f, 2f), new Color32(toneColor.r, toneColor.g, toneColor.b, 130));

        if (nodes.Count == 0)
        {
            CreateCanvasLabel(parent, "FocusedTreeEmpty", "Noch keine Freischaltungen in dieser Gruppe.", new Vector2(0f, -18f), new Vector2(560f, 26f), 12f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);
            return;
        }

        int columns = 5;
        int maxVisibleNodes = selectedGeneralTreeCategory == "start" ? 10 : 15;
        int maxVisible = Mathf.Min(nodes.Count, maxVisibleNodes);
        Vector2 cardSize = new Vector2(174f, 82f);
        float gapX = 22f;
        float gapY = 18f;
        float gridWidth = columns * cardSize.x + (columns - 1) * gapX;
        float startX = -gridWidth * 0.5f + cardSize.x * 0.5f;
        float startY = 54f;

        for (int i = 0; i < maxVisible; i++)
        {
            GeneralMetaNodeDefinition definition = nodes[i];
            if (definition == null)
                continue;

            int column = i % columns;
            int row = i / columns;
            Vector2 position = new Vector2(startX + column * (cardSize.x + gapX), startY - row * (cardSize.y + gapY));
            CreateReadableGeneralNode(parent, "GeneralFocused_" + i, definition, position, cardSize, tone);
        }

        if (nodes.Count > maxVisible)
        {
            float moreY = maxVisible > 15 ? -304f : -200f;
            CreateCanvasLabel(parent, "FocusedTreeMore", "+" + (nodes.Count - maxVisible) + " weitere Freischaltungen nach naechstem Fortschritt", new Vector2(0f, moreY), new Vector2(520f, 20f), 10f, new Color32(190, 178, 150, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        }
    }

    private void CreateReadableGeneralNode(Transform parent, string name, GeneralMetaNodeDefinition definition, Vector2 position, Vector2 size, MetaHubTone fallbackTone)
    {
        string nodeId = definition.nodeId;
        string capturedNodeId = nodeId;
        MetaHubTone tone = GeneralNodeTone(nodeId, GetGeneralNodeDisplayTone(nodeId, fallbackTone));
        Color32 toneColor = ToneColor(tone);
        Color32 fill = new Color32(6, 18, 18, 235);
        Transform node = CreateCanvasPanel(parent, name, position, size, fill, toneColor);
        CreateArtIcon(node, "Icon", GetGeneralNodeDisplayArt(nodeId, GetCategoryArtName(definition.category)), new Vector2(-size.x * 0.5f + 28f, 19f), new Vector2(36f, 36f));
        CreateCanvasLabel(node, "Title", Shorten(definition.displayName, 20), new Vector2(26f, 22f), new Vector2(size.x - 62f, 18f), 8.6f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI effect = CreateCanvasLabel(node, "Effect", ToTwoLineText(string.IsNullOrEmpty(definition.effectText) ? definition.requirementText : definition.effectText, 24, 54), new Vector2(26f, -5f), new Vector2(size.x - 62f, 34f), 9.2f, new Color32(188, 178, 155, 255), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        effect.enableWordWrapping = true;
        ConfigureCardDescriptionText(effect, 7.2f, 9.2f);
        string costLabel = runtimeUnlockInfoMode ? "" : definition.cost + " KW";
        CreateCanvasLabel(node, "Cost", costLabel, new Vector2(-size.x * 0.25f, -31f), new Vector2(size.x * 0.44f, 14f), 7.3f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(node, "State", Shorten(GeneralNodeStateLabel(nodeId), 10), new Vector2(size.x * 0.25f, -31f), new Vector2(size.x * 0.44f, 14f), 7.3f, toneColor, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeCanvasClickable(node, delegate { HandleGeneralNodeClick(capturedNodeId); }, fill, toneColor);
    }

    private void CreateGeneralLoadoutPickerOverlay(Transform main)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        float mainWidth = GetPanelWidth(main, 1307f);
        CreateCanvasPanel(main, "GeneralLoadoutShade", Vector2.zero, new Vector2(mainWidth - 28f, 720f), new Color32(0, 0, 0, 165), new Color32(0, 0, 0, 0));
        Transform panel = CreateUnlockPanel(main, "GeneralLoadoutPicker", "LOADOUT WAEHLEN", Vector2.zero, new Vector2(540f, 340f), new Color32(170, 113, 35, 255));
        CreateCanvasLabel(panel, "Hint", "Waehle das Profil fuer den naechsten Run.", new Vector2(0f, 102f), new Vector2(450f, 22f), 11f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);

        int count = general != null ? general.GetLoadoutProfileCount() : 3;
        for (int i = 0; i < count; i++)
            CreateGeneralLoadoutChoiceRow(panel, general, i, 58f - i * 72f);

        CreateCanvasButton(panel, "CloseLoadoutPicker", "SCHLIESSEN", new Vector2(0f, -138f), new Vector2(210f, 34f), CloseGeneralLoadoutOverlay);
    }

    private void CreateGeneralLoadoutPromptOverlay(Transform main)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        string nodeId = pendingGeneralLoadoutPromptNodeId;
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        GeneralMetaNodeState state = general != null ? general.GetNodeState(nodeId) : null;
        if (general == null || definition == null || state == null || !state.purchased || !definition.RequiresLoadoutSlot())
        {
            pendingGeneralLoadoutPromptNodeId = "";
            return;
        }

        bool canActivate = general.CanActivateNode(nodeId);
        MetaHubTone tone = GetGeneralNodeDisplayTone(nodeId, MetaHubTone.Gold);
        Color32 toneColor = ToneColor(tone);
        float mainWidth = GetPanelWidth(main, 1307f);
        CreateCanvasPanel(main, "GeneralLoadoutPromptShade", Vector2.zero, new Vector2(mainWidth - 28f, 720f), new Color32(0, 0, 0, 165), new Color32(0, 0, 0, 0));
        Transform panel = CreateUnlockPanel(main, "GeneralLoadoutPrompt", "LOADOUT", Vector2.zero, new Vector2(620f, 300f), toneColor);
        CreateArtIcon(panel, "Icon", GetGeneralNodeDisplayArt(nodeId, GetCategoryArtName(definition.category)), new Vector2(-232f, 32f), new Vector2(72f, 72f));
        CreateCanvasLabel(panel, "Question", "Jetzt ins Loadout einsetzen?", new Vector2(54f, 78f), new Vector2(420f, 28f), 17f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(panel, "Name", definition.displayName, new Vector2(54f, 44f), new Vector2(420f, 22f), 13f, new Color32(236, 225, 202, 255), TextAlignmentOptions.Left, FontStyles.Bold);

        string slotText = definition.slotCost + " Slot" + (definition.slotCost == 1 ? "" : "s") + " | frei: " + general.GetAvailableLoadoutSlots();
        string hint = canActivate
            ? "Bonus ist gekauft und kann direkt fuer den naechsten Run aktiv werden."
            : "Nicht genug freie Slots. Oeffne das Loadout und nimm einen anderen Bonus heraus.";
        CreateCanvasLabel(panel, "SlotInfo", slotText, new Vector2(54f, 14f), new Vector2(420f, 20f), 11f, toneColor, TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(panel, "Hint", hint, new Vector2(54f, -30f), new Vector2(420f, 42f), 11f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Left, FontStyles.Normal);

        if (canActivate)
            CreateCanvasButton(panel, "ConfirmLoadoutPrompt", "EINSETZEN", new Vector2(-102f, -112f), new Vector2(230f, 34f), ActivatePromptedGeneralLoadoutNode);
        else
            CreateCanvasButton(panel, "ConfirmLoadoutPrompt", "LOADOUT OEFFNEN", new Vector2(-102f, -112f), new Vector2(230f, 34f), OpenGeneralLoadoutEditorFromPrompt);

        CreateCanvasButton(panel, "LaterLoadoutPrompt", "SPAETER", new Vector2(150f, -112f), new Vector2(180f, 34f), DismissGeneralLoadoutPrompt);
    }

    private void CreateGeneralLoadoutChoiceRow(Transform parent, GeneralMetaProgressionManager general, int loadoutIndex, float y)
    {
        bool selected = general != null && general.GetActiveLoadoutIndex() == loadoutIndex;
        MetaHubTone tone = selected ? MetaHubTone.Gold : MetaHubTone.Neutral;
        Color32 toneColor = ToneColor(tone);
        Color32 fill = selected ? new Color32(54, 39, 13, 248) : new Color32(6, 18, 18, 238);
        Transform row = CreateCanvasPanel(parent, "LoadoutChoice_" + loadoutIndex, new Vector2(0f, y), new Vector2(440f, 56f), fill, toneColor);
        CreateArtIcon(row, "Icon", selected ? "icon_gold" : "icon_keystone", new Vector2(-178f, 0f), new Vector2(38f, 38f));
        string name = general != null ? general.GetLoadoutDisplayName(loadoutIndex) : "Loadout " + (loadoutIndex + 1);
        string slots = general != null ? general.GetUsedLoadoutSlots(loadoutIndex) + " / " + general.GetLoadoutSlotCapacity() + " Slots" : "0 / 0 Slots";
        CreateCanvasLabel(row, "Title", name, new Vector2(-58f, 9f), new Vector2(210f, 18f), 12f, new Color32(236, 225, 202, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(row, "Slots", slots, new Vector2(-58f, -11f), new Vector2(210f, 16f), 8.5f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasLabel(row, "State", selected ? "AKTIV" : "WAEHLEN", new Vector2(162f, 0f), new Vector2(95f, 20f), 10f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
        int capturedIndex = loadoutIndex;
        MakeCanvasClickable(row, delegate { SelectGeneralLoadout(capturedIndex); }, fill, toneColor);
    }

    private void CreateGeneralLoadoutEditorOverlay(Transform main)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        float mainWidth = GetPanelWidth(main, 1307f);
        CreateCanvasPanel(main, "GeneralLoadoutEditorShade", Vector2.zero, new Vector2(mainWidth - 28f, 720f), new Color32(0, 0, 0, 165), new Color32(0, 0, 0, 0));
        Transform panel = CreateUnlockPanel(main, "GeneralLoadoutEditor", "BONI EINSETZEN", Vector2.zero, new Vector2(960f, 560f), new Color32(170, 113, 35, 255));
        int activeLoadout = general != null ? general.GetActiveLoadoutIndex() : 0;
        string loadoutName = general != null ? general.GetLoadoutDisplayName(activeLoadout) : "Loadout 1";
        string slotText = general != null ? general.GetUsedLoadoutSlots() + " / " + general.GetLoadoutSlotCapacity() + " Slots belegt" : "0 / 0 Slots belegt";
        CreateCanvasLabel(panel, "ActiveLoadout", loadoutName + "  |  " + slotText, new Vector2(-210f, 222f), new Vector2(390f, 22f), 12f, new Color32(236, 225, 202, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasButton(panel, "SwitchLoadoutFromEditor", "LOADOUT WECHSELN", new Vector2(250f, 222f), new Vector2(210f, 32f), OpenGeneralLoadoutPicker);

        List<GeneralMetaNodeDefinition> nodes = GetPurchasedLoadoutNodes(general);
        if (nodes.Count == 0)
        {
            CreateCanvasLabel(panel, "NoLoadoutNodes", "Noch keine einsetzbaren Loadout-Boni freigeschaltet.", new Vector2(0f, 20f), new Vector2(620f, 30f), 13f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        }
        else
        {
            int columns = 4;
            Vector2 cardSize = new Vector2(188f, 78f);
            float gapX = 26f;
            float gapY = 18f;
            float gridWidth = columns * cardSize.x + (columns - 1) * gapX;
            float startX = -gridWidth * 0.5f + cardSize.x * 0.5f;
            float startY = 150f;
            int maxVisible = Mathf.Min(nodes.Count, 16);
            for (int i = 0; i < maxVisible; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Vector2 position = new Vector2(startX + column * (cardSize.x + gapX), startY - row * (cardSize.y + gapY));
                CreateGeneralLoadoutSlotCard(panel, nodes[i], position, cardSize);
            }
        }

        CreateCanvasButton(panel, "CloseLoadoutEditor", "FERTIG", new Vector2(0f, -244f), new Vector2(220f, 36f), CloseGeneralLoadoutOverlay);
    }

    private List<GeneralMetaNodeDefinition> GetPurchasedLoadoutNodes(GeneralMetaProgressionManager general)
    {
        List<GeneralMetaNodeDefinition> nodes = new List<GeneralMetaNodeDefinition>();
        if (general == null)
            return nodes;

        List<GeneralMetaNodeDefinition> definitions = general.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            GeneralMetaNodeDefinition definition = definitions[i];
            if (definition == null || !definition.RequiresLoadoutSlot())
                continue;

            GeneralMetaNodeState state = general.GetNodeState(definition.nodeId);
            if (state != null && state.purchased)
                nodes.Add(definition);
        }

        return nodes;
    }

    private void CreateGeneralLoadoutSlotCard(Transform parent, GeneralMetaNodeDefinition definition, Vector2 position, Vector2 size)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        string nodeId = definition.nodeId;
        bool active = general != null && general.IsNodeActive(nodeId);
        bool canActivate = general != null && general.CanActivateNode(nodeId);
        MetaHubTone tone = active ? MetaHubTone.Green : canActivate ? GetGeneralNodeDisplayTone(nodeId, MetaHubTone.Gold) : MetaHubTone.Neutral;
        Color32 toneColor = ToneColor(tone);
        Color32 fill = active ? new Color32(8, 35, 18, 238) : new Color32(6, 18, 18, 238);
        Transform card = CreateCanvasPanel(parent, "LoadoutNode_" + nodeId, position, size, fill, toneColor);
        CreateArtIcon(card, "Icon", GetGeneralNodeDisplayArt(nodeId, GetCategoryArtName(definition.category)), new Vector2(-size.x * 0.5f + 28f, 16f), new Vector2(34f, 34f));
        CreateCanvasLabel(card, "Title", Shorten(definition.displayName, 19), new Vector2(28f, 20f), new Vector2(size.x - 64f, 18f), 8.8f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI effect = CreateCanvasLabel(card, "Effect", ToTwoLineText(definition.effectText, 24, 52), new Vector2(28f, -4f), new Vector2(size.x - 64f, 30f), 9.0f, new Color32(188, 178, 155, 255), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        effect.enableWordWrapping = true;
        ConfigureCardDescriptionText(effect, 7.0f, 9.0f);
        CreateCanvasLabel(card, "Slots", definition.slotCost + " Slot", new Vector2(-size.x * 0.28f, -30f), new Vector2(size.x * 0.42f, 14f), 7.1f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        string state = active ? "AKTIV" : canActivate ? "EINSETZEN" : "KEIN SLOT";
        CreateCanvasLabel(card, "State", state, new Vector2(size.x * 0.25f, -30f), new Vector2(size.x * 0.46f, 14f), 7.1f, toneColor, TextAlignmentOptions.Center, FontStyles.Bold);
        string capturedNodeId = nodeId;
        MakeCanvasClickable(card, delegate { ToggleGeneralLoadoutNode(capturedNodeId); }, fill, toneColor);
    }

    private GeneralMetaCategory[] GetGeneralTreeCategories(string categoryId)
    {
        switch (categoryId)
        {
            case "tiles": return new GeneralMetaCategory[] { GeneralMetaCategory.TileUnlock };
            case "comfort": return new GeneralMetaCategory[] { GeneralMetaCategory.QoL };
            case "start": return new GeneralMetaCategory[] { GeneralMetaCategory.StartOption, GeneralMetaCategory.MetaLoadout };
            case "tower":
            default: return new GeneralMetaCategory[] { GeneralMetaCategory.TowerUnlock };
        }
    }

    private MetaHubTone GetGeneralTreeTone(string categoryId)
    {
        switch (categoryId)
        {
            case "tiles": return MetaHubTone.Cyan;
            case "comfort": return MetaHubTone.Blue;
            case "start": return MetaHubTone.Gold;
            case "tower":
            default: return MetaHubTone.Purple;
        }
    }

    private string GetGeneralTreeTitle(string categoryId)
    {
        switch (categoryId)
        {
            case "tiles": return "TILE-FREISCHALTUNGEN";
            case "comfort": return "KOMFORT";
            case "start": return "START / LOADOUT";
            case "tower":
            default: return "TOWER-FREISCHALTUNGEN";
        }
    }

    private string GetGeneralTreeTabLabel(string categoryId)
    {
        switch (categoryId)
        {
            case "tiles": return "TILES";
            case "comfort": return "KOMFORT";
            case "start": return "START";
            case "tower":
            default: return "TOWER";
        }
    }

    private List<string> GetVisibleGeneralTreeCategoryIds()
    {
        List<string> categoryIds = new List<string>();
        AddVisibleGeneralTreeCategory(categoryIds, "tower");
        AddVisibleGeneralTreeCategory(categoryIds, "tiles");
        AddVisibleGeneralTreeCategory(categoryIds, "comfort");
        AddVisibleGeneralTreeCategory(categoryIds, "start");
        return categoryIds;
    }

    private void AddVisibleGeneralTreeCategory(List<string> categoryIds, string categoryId)
    {
        if (!runtimeUnlockInfoMode || HasUnlockedGeneralTreeCategory(categoryId))
            categoryIds.Add(categoryId);
    }

    private bool HasUnlockedGeneralTreeCategory(string categoryId)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general == null)
            return false;

        GeneralMetaCategory[] categories = GetGeneralTreeCategories(categoryId);
        List<GeneralMetaNodeDefinition> definitions = general.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            GeneralMetaNodeDefinition definition = definitions[i];
            if (definition != null && GeneralCategoryMatches(definition.category, categories) && IsGeneralNodeVisibleInRuntime(general, definition.nodeId))
                return true;
        }

        return false;
    }

    private string[] GetGeneralTreePreferredIds(string categoryId)
    {
        switch (categoryId)
        {
            case "tiles":
                return new string[] { "general.tile.path", "general.tile.knock", "general.tile.slow", "general.tile.trap", "general.tile.gold", "general.tile.range", "general.tile.xp", "general.tile.damage", "general.tile.rate", "general.tile.upgrade", "general.tile.heal", "general.tile.weakpoint", "general.tile.combo" };
            case "comfort":
                return new string[] { "general.qol.speed_fast", "general.qol.speed_medium", "general.qol.dps_display", "general.qol.goal_pin_1", "general.qol.speed_faster", "general.qol.goal_pin_2" };
            case "start":
                return new string[] { "general.start.gold_1", "general.start.life_1", "general.start.xp_1", "general.loadout.slot_2", "general.start.path_1", "general.start.discount_1", "general.start.protection_1", "general.start.gold_2", "general.start.life_2", "general.start.xp_2", "general.loadout.slot_3", "general.start.discount_2", "general.start.gold_3", "general.start.life_3", "general.start.xp_3", "general.loadout.slot_4", "general.start.discount_3", "general.start.reserve_1", "general.start.gold_4", "general.start.life_4", "general.start.xp_4", "general.start.path_2", "general.loadout.slot_5", "general.start.discount_4", "general.start.gold_5", "general.start.life_5", "general.start.xp_5", "general.loadout.slot_6", "general.start.discount_5", "general.loadout.slot_7", "general.loadout.slot_8", "general.loadout.slot_9", "general.loadout.slot_10", "general.loadout.slot_11", "general.loadout.slot_12" };
            case "tower":
            default:
                return new string[] { "general.tower.basic", "general.tower.rapid", "general.tower.heavy", "general.tower.slow", "general.tower.fire", "general.tower.poison", "general.tower.sniper", "general.tower.alchemist", "general.tower.lightning", "general.tower.mortar", "general.tower.spike" };
        }
    }

    private string ToTwoLineText(string text, int preferredBreak, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        string shortened = Shorten(text, maxLength);
        if (shortened.Length <= preferredBreak)
            return shortened;

        int searchStart = Mathf.Min(preferredBreak, shortened.Length - 1);
        int breakIndex = shortened.LastIndexOf(' ', searchStart);
        if (breakIndex < 8)
            breakIndex = searchStart;

        return shortened.Substring(0, breakIndex).Trim() + "\n" + shortened.Substring(breakIndex).Trim();
    }

    private void ConfigureCardDescriptionText(TextMeshProUGUI text, float minFontSize, float maxFontSize)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minFontSize;
        text.fontSizeMax = maxFontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void CreateUnlockTowerContent(Transform main, MetaHubData data)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerRole selectedRole = selectedTowerRole;
        if (runtimeUnlockInfoMode && !HasUnlockedTowerRole(selectedRole))
        {
            TowerRole firstUnlockedRole;
            if (TryGetFirstUnlockedTowerRole(out firstUnlockedRole))
                selectedRole = firstUnlockedRole;
        }
        selectedTowerRole = selectedRole;
        TowerMasteryRoleProfile selectedProfile = towerMastery != null ? towerMastery.GetProfile(selectedRole) : null;
        int masteryLevel = towerMastery != null ? towerMastery.GetMasteryLevel(selectedRole) : 1;
        int masteryCurrentXP = towerMastery != null ? towerMastery.GetMasteryXPIntoCurrentLevel(selectedRole) : 0;
        int masteryRequiredXP = towerMastery != null ? towerMastery.GetXPToNextMasteryLevel(masteryLevel) : 1;

        object roleManager = GetTowerRoleMasteryManager(selectedRole);
        EnsureSelectedTowerTreeCategory(roleManager);

        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float topY = 216f;
        float topPanelHeight = 180f;

        Transform account = CreateUnlockPanel(main, "TowerAccount", TowerMasteryManager.GetTowerDisplayName(selectedRole).ToUpperInvariant(), new Vector2(left + 175f, topY), new Vector2(350f, topPanelHeight), ToneColor(TowerTone(selectedRole)));
        CreateDonutImage(account, "MasteryRing", new Vector2(-108f, -13f), 70f, Percent(masteryCurrentXP, masteryRequiredXP), ToneColor(TowerTone(selectedRole)), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(account, "Level", masteryLevel.ToString(), new Vector2(-108f, -13f), new Vector2(54f, 26f), 19f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(account, "LevelCaption", "MASTERY LEVEL", new Vector2(-108f, -56f), new Vector2(112f, 18f), 8f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(account, "TowerXPText", MetaHubMockData.FormatNumber(masteryCurrentXP) + " / " + MetaHubMockData.FormatNumber(masteryRequiredXP) + " XP", new Vector2(66f, 30f), new Vector2(170f, 18f), 11f, new Color32(224, 210, 184, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasBar(account, "TowerXP", new Vector2(70f, 10f), new Vector2(145f, 6f), Percent(masteryCurrentXP, masteryRequiredXP), ToneColor(TowerTone(selectedRole)));
        string profileText = "Freie Punkte  " + (selectedProfile != null ? selectedProfile.unspentPoints.ToString() : "0") +
            "\nGesetzt  " + (selectedProfile != null ? selectedProfile.spentPoints.ToString() : "0") +
            "\nBester Lv.  " + (selectedProfile != null ? selectedProfile.bestLevelEver.ToString() : "1");
        CreateCanvasLabel(account, "Profile", profileText, new Vector2(84f, -36f), new Vector2(172f, 50f), 9.5f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Normal);

        float selectorLeft = left + 366f;
        float selectorWidth = right - selectorLeft;
        Transform selector = CreateUnlockPanel(main, "TowerRoleSelector", "TOWER-ROLLEN", new Vector2(selectorLeft + selectorWidth * 0.5f, topY), new Vector2(selectorWidth, topPanelHeight), new Color32(126, 98, 51, 255));
        CreateTowerRoleSelector(selector, towerMastery, selectedRole, selectorWidth);

        Transform tree = CreateUnlockPanel(main, "TowerFocusedTree", "FREISCHALTBAUM", new Vector2((left + right) * 0.5f, -150f), new Vector2(right - left, 430f), ToneColor(GetTowerTreeCategoryTone(roleManager, selectedTowerTreeCategory)));
        CreateTowerTreeTabs(tree, roleManager, right - left);
        CreateTowerFocusedSkillTree(tree, roleManager, right - left);
    }

    private void CreateTowerRoleSelector(Transform parent, TowerMasteryManager towerMastery, TowerRole selectedRole, float panelWidth)
    {
        List<TowerRole> visibleRoles = GetVisibleTowerRoles();
        if (visibleRoles.Count == 0)
        {
            CreateCanvasLabel(parent, "NoTowerRoles", "Noch keine Tower-Mastery im Run freigeschaltet.", Vector2.zero, new Vector2(panelWidth - 80f, 28f), 12f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);
            return;
        }

        int columns = 6;
        Vector2 cardSize = new Vector2(Mathf.Min(132f, (panelWidth - 92f) / columns), 48f);
        float gapX = 12f;
        float gapY = 12f;
        float gridWidth = columns * cardSize.x + (columns - 1) * gapX;
        float startX = -gridWidth * 0.5f + cardSize.x * 0.5f;
        float startY = 20f;

        for (int i = 0; i < visibleRoles.Count; i++)
        {
            TowerRole role = visibleRoles[i];
            int column = i % columns;
            int row = i / columns;
            Vector2 position = new Vector2(startX + column * (cardSize.x + gapX), startY - row * (cardSize.y + gapY));
            CreateTowerRoleChoice(parent, towerMastery, role, role == selectedRole, position, cardSize);
        }
    }

    private List<TowerRole> GetVisibleTowerRoles()
    {
        List<TowerRole> roles = new List<TowerRole>();
        TowerRole[] orderedRoles = TowerMasteryManager.GetOrderedTowerRoles();
        for (int i = 0; i < orderedRoles.Length; i++)
        {
            TowerRole role = orderedRoles[i];
            if (!runtimeUnlockInfoMode || HasUnlockedTowerRole(role))
                roles.Add(role);
        }

        return roles;
    }

    private bool TryGetFirstUnlockedTowerRole(out TowerRole role)
    {
        TowerRole[] orderedRoles = TowerMasteryManager.GetOrderedTowerRoles();
        for (int i = 0; i < orderedRoles.Length; i++)
        {
            if (HasUnlockedTowerRole(orderedRoles[i]))
            {
                role = orderedRoles[i];
                return true;
            }
        }

        role = TowerRole.Basic;
        return false;
    }

    private bool HasUnlockedTowerRole(TowerRole role)
    {
        object roleManager = GetTowerRoleMasteryManager(role);
        if (roleManager == null)
            return false;

        List<object> definitions = GetTowerDefinitionObjects(roleManager);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (IsTowerDefinitionVisibleInRuntime(roleManager, definitions[i]))
                return true;
        }

        return false;
    }

    private void CreateTowerRoleChoice(Transform parent, TowerMasteryManager towerMastery, TowerRole role, bool selected, Vector2 position, Vector2 size)
    {
        MetaHubTone tone = TowerTone(role);
        Color32 toneColor = ToneColor(tone);
        Color32 fill = selected ? new Color32(54, 39, 13, 248) : new Color32(6, 18, 18, 238);
        Transform row = CreateCanvasPanel(parent, "TowerRole_" + role, position, size, fill, selected ? toneColor : new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateArtIcon(row, "Icon", "icon_tower", new Vector2(-size.x * 0.5f + 24f, 0f), new Vector2(30f, 30f));
        CreateCanvasLabel(row, "Title", Shorten(TowerMasteryManager.GetTowerDisplayName(role).Replace(" Tower", ""), 12), new Vector2(18f, 8f), new Vector2(size.x - 58f, 17f), 8.2f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        int level = towerMastery != null ? towerMastery.GetMasteryLevel(role) : 1;
        CreateCanvasLabel(row, "Level", "Lv. " + level, new Vector2(18f, -10f), new Vector2(size.x - 58f, 15f), 7.1f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        TowerRole capturedRole = role;
        MakeCanvasClickable(row, delegate { SelectTowerRole(capturedRole); }, fill, toneColor);
    }

    private void CreateTowerTreeTabs(Transform parent, object roleManager, float panelWidth)
    {
        List<string> categories = GetTowerTreeCategoryKeys(roleManager);
        int count = Mathf.Max(1, categories.Count);
        float tabWidth = Mathf.Min(190f, (panelWidth - 74f - (count - 1) * 18f) / count);
        float gap = 18f;
        float gridWidth = count * tabWidth + (count - 1) * gap;
        float startX = -gridWidth * 0.5f + tabWidth * 0.5f;

        for (int i = 0; i < categories.Count; i++)
        {
            string category = categories[i];
            MetaHubTone tone = GetTowerTreeCategoryTone(roleManager, category);
            string label = GetTowerTreeCategoryDisplayName(roleManager, category).ToUpperInvariant();
            CreateTowerTreeTab(parent, category, Shorten(label, 15), tone, new Vector2(startX + i * (tabWidth + gap), 148f), new Vector2(tabWidth, 36f));
        }
    }

    private void CreateTowerTreeTab(Transform parent, string categoryId, string label, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        bool selected = selectedTowerTreeCategory == categoryId;
        Color32 toneColor = ToneColor(tone);
        Color32 fill = selected ? new Color32(54, 39, 13, 248) : new Color32(5, 18, 18, 238);
        Transform tab = CreateCanvasPanel(parent, "TowerTreeTab_" + categoryId, position, size, fill, selected ? toneColor : new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateCanvasLabel(tab, "Label", label, Vector2.zero, size, 11.5f, selected ? new Color32(255, 236, 186, 255) : new Color32(224, 214, 194, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        string capturedCategoryId = categoryId;
        MakeCanvasClickable(tab, delegate { SelectTowerTreeCategory(capturedCategoryId); }, fill, toneColor);
    }

    private void CreateTowerFocusedSkillTree(Transform parent, object roleManager, float panelWidth)
    {
        List<TowerMasteryNodeView> nodes = GetTowerNodeViews(roleManager, selectedTowerTreeCategory);
        int total = nodes.Count;
        int purchased = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].rank > 0)
                purchased++;
        }

        MetaHubTone tone = GetTowerTreeCategoryTone(roleManager, selectedTowerTreeCategory);
        Color32 toneColor = ToneColor(tone);
        string title = selectedTowerTreeCategory == "Keystones" ? "KEYSTONES" : GetTowerTreeCategoryDisplayName(roleManager, selectedTowerTreeCategory).ToUpperInvariant() + "-MASTERY";

        float left = -panelWidth * 0.5f + 58f;
        CreateCanvasLabel(parent, "TowerFocusedTitle", title, new Vector2(left + 96f, 114f), new Vector2(260f, 24f), 14f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(parent, "TowerFocusedCount", GetUnlockCountText(purchased, total), new Vector2(panelWidth * 0.5f - 82f, 114f), new Vector2(100f, 20f), 10f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
        CreateLine(parent, "TowerFocusedLine", new Vector2(0f, 90f), new Vector2(panelWidth - 150f, 2f), new Color32(toneColor.r, toneColor.g, toneColor.b, 130));

        if (nodes.Count == 0)
        {
            string message = runtimeUnlockInfoMode ? "Im aktuellen Run ist hier noch nichts freigeschaltet." : "Keine Freischaltungen in dieser Gruppe.";
            CreateCanvasLabel(parent, "TowerNoNodes", message, new Vector2(0f, -10f), new Vector2(560f, 24f), 12f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);
            return;
        }

        int columns = 5;
        int maxVisible = Mathf.Min(nodes.Count, 15);
        Vector2 cardSize = new Vector2(188f, 88f);
        float gapX = 28f;
        float gapY = 18f;
        float gridWidth = columns * cardSize.x + (columns - 1) * gapX;
        float startX = -gridWidth * 0.5f + cardSize.x * 0.5f;
        float startY = 42f;

        for (int i = 0; i < maxVisible; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Vector2 position = new Vector2(startX + column * (cardSize.x + gapX), startY - row * (cardSize.y + gapY));
            CreateReadableTowerMasteryNode(parent, "TowerFocusedNode_" + i, nodes[i], position, cardSize);
        }

        if (nodes.Count > maxVisible)
            CreateCanvasLabel(parent, "TowerFocusedMore", "+" + (nodes.Count - maxVisible) + " weitere Freischaltungen in dieser Gruppe", new Vector2(0f, -200f), new Vector2(520f, 20f), 10f, new Color32(190, 178, 150, 255), TextAlignmentOptions.Center, FontStyles.Normal);
    }

    private void CreateReadableTowerMasteryNode(Transform parent, string name, TowerMasteryNodeView view, Vector2 position, Vector2 size)
    {
        if (view == null)
            return;

        Color32 toneColor = ToneColor(view.tone);
        Color32 fill = view.rank >= view.maxRank ? new Color32(8, 35, 18, 235) : new Color32(6, 18, 18, 235);
        Transform node = CreateCanvasPanel(parent, name, position, size, fill, toneColor);
        CreateArtIcon(node, "Icon", view.isKeystone ? "icon_keystone_purple" : "icon_tower", new Vector2(-size.x * 0.5f + 30f, 21f), new Vector2(38f, 38f));
        CreateCanvasLabel(node, "Title", Shorten(view.displayName, 21), new Vector2(30f, 24f), new Vector2(size.x - 68f, 18f), 8.8f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI effect = CreateCanvasLabel(node, "Effect", ToTwoLineText(view.effectText, 28, 64), new Vector2(30f, -4f), new Vector2(size.x - 68f, 34f), 9.0f, new Color32(188, 178, 155, 255), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        effect.textWrappingMode = TextWrappingModes.Normal;
        ConfigureCardDescriptionText(effect, 7.1f, 9.0f);
        CreateCanvasLabel(node, "Cost", GetTowerNodeCostLabel(view), new Vector2(-size.x * 0.25f, -34f), new Vector2(size.x * 0.44f, 14f), 7.2f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(node, "State", Shorten(GetTowerNodeStateLabel(view), 11), new Vector2(size.x * 0.25f, -34f), new Vector2(size.x * 0.44f, 14f), 7.2f, toneColor, TextAlignmentOptions.Center, FontStyles.Bold);
        string capturedNodeId = view.nodeId;
        MakeCanvasClickable(node, delegate { HandleTowerMasteryNodeClick(capturedNodeId); }, fill, toneColor);
    }

    private object GetTowerRoleMasteryManager(TowerRole role)
    {
        ResolveGameManager();
        switch (role)
        {
            case TowerRole.Basic: return gameManager != null ? gameManager.GetBasicTowerMasteryManager() : BasicTowerMasteryManager.GetOrCreate();
            case TowerRole.Rapid: return gameManager != null ? gameManager.GetRapidTowerMasteryManager() : RapidTowerMasteryManager.GetOrCreate();
            case TowerRole.Heavy: return gameManager != null ? gameManager.GetHeavyTowerMasteryManager() : HeavyTowerMasteryManager.GetOrCreate();
            case TowerRole.Fire: return gameManager != null ? gameManager.GetFireTowerMasteryManager() : FireTowerMasteryManager.GetOrCreate();
            case TowerRole.Slow: return gameManager != null ? gameManager.GetSlowTowerMasteryManager() : SlowTowerMasteryManager.GetOrCreate();
            case TowerRole.Poison: return gameManager != null ? gameManager.GetPoisonTowerMasteryManager() : PoisonTowerMasteryManager.GetOrCreate();
            case TowerRole.Sniper: return gameManager != null ? gameManager.GetSniperTowerMasteryManager() : SniperTowerMasteryManager.GetOrCreate();
            case TowerRole.Alchemist: return gameManager != null ? gameManager.GetAlchemistTowerMasteryManager() : AlchemistTowerMasteryManager.GetOrCreate();
            case TowerRole.Lightning: return gameManager != null ? gameManager.GetLightningTowerMasteryManager() : LightningTowerMasteryManager.GetOrCreate();
            case TowerRole.Mortar: return gameManager != null ? gameManager.GetMortarTowerMasteryManager() : MortarTowerMasteryManager.GetOrCreate();
            case TowerRole.Spike: return gameManager != null ? gameManager.GetSpikeTowerMasteryManager() : SpikeTowerMasteryManager.GetOrCreate();
            default: return null;
        }
    }

    private void EnsureSelectedTowerTreeCategory(object roleManager)
    {
        List<string> categories = GetTowerTreeCategoryKeys(roleManager);
        if (categories.Count == 0)
        {
            selectedTowerTreeCategory = "Trunk";
            return;
        }

        if (!categories.Contains(selectedTowerTreeCategory))
            selectedTowerTreeCategory = categories[0];
    }

    private List<string> GetTowerTreeCategoryKeys(object roleManager)
    {
        List<string> categories = new List<string>();
        List<object> definitions = GetTowerDefinitionObjects(roleManager);
        bool hasKeystones = false;

        for (int i = 0; i < definitions.Count; i++)
        {
            object definition = definitions[i];
            if (IsTowerKeystoneDefinition(definition))
            {
                hasKeystones = true;
                continue;
            }

            string pathKey = GetTowerPathKey(definition);
            if (!categories.Contains(pathKey))
                categories.Add(pathKey);
        }

        if (categories.Remove("Trunk"))
            categories.Insert(0, "Trunk");

        if (hasKeystones)
            categories.Add("Keystones");

        if (runtimeUnlockInfoMode)
            categories = FilterUnlockedTowerCategories(roleManager, categories);

        if (categories.Count == 0 && !runtimeUnlockInfoMode)
            categories.Add("Trunk");

        return categories;
    }

    private List<string> FilterUnlockedTowerCategories(object roleManager, List<string> categories)
    {
        List<string> result = new List<string>();
        if (roleManager == null || categories == null)
            return result;

        for (int i = 0; i < categories.Count; i++)
        {
            string categoryId = categories[i];
            if (HasUnlockedTowerCategory(roleManager, categoryId))
                result.Add(categoryId);
        }

        return result;
    }

    private bool HasUnlockedTowerCategory(object roleManager, string categoryId)
    {
        List<object> definitions = GetTowerDefinitionObjects(roleManager);
        for (int i = 0; i < definitions.Count; i++)
        {
            object definition = definitions[i];
            if (definition == null)
                continue;

            bool isKeystone = IsTowerKeystoneDefinition(definition);
            string pathKey = GetTowerPathKey(definition);
            bool inCategory = categoryId == "Keystones" ? isKeystone : !isKeystone && pathKey == categoryId;
            if (inCategory && IsTowerDefinitionVisibleInRuntime(roleManager, definition))
                return true;
        }

        return false;
    }

    private List<TowerMasteryNodeView> GetTowerNodeViews(object roleManager, string categoryId)
    {
        List<TowerMasteryNodeView> views = new List<TowerMasteryNodeView>();
        List<object> definitions = GetTowerDefinitionObjects(roleManager);

        for (int i = 0; i < definitions.Count; i++)
        {
            TowerMasteryNodeView view = BuildTowerMasteryNodeView(roleManager, definitions[i]);
            if (view == null)
                continue;

            bool inCategory = categoryId == "Keystones" ? view.isKeystone : !view.isKeystone && view.pathKey == categoryId;
            if (inCategory && (!runtimeUnlockInfoMode || IsTowerNodeVisibleInRuntime(view)))
                views.Add(view);
        }

        return views;
    }

    private bool IsTowerNodeVisibleInRuntime(TowerMasteryNodeView view)
    {
        return view != null && (view.rank > 0 || (!string.IsNullOrEmpty(view.stateText) && view.stateText.IndexOf("Aktiv", StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private bool IsTowerDefinitionVisibleInRuntime(object roleManager, object definition)
    {
        if (roleManager == null || definition == null)
            return false;

        string nodeId = ReadStringMember(definition, "nodeId", "");
        if (string.IsNullOrEmpty(nodeId))
            return false;

        if (GetTowerNodeRank(roleManager, nodeId) > 0)
            return true;

        string stateText = GetTowerNodeStateText(roleManager, definition);
        return !string.IsNullOrEmpty(stateText) && stateText.IndexOf("Aktiv", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private TowerMasteryNodeView BuildTowerMasteryNodeView(object roleManager, object definition)
    {
        if (definition == null)
            return null;

        string nodeId = ReadStringMember(definition, "nodeId", "");
        if (string.IsNullOrEmpty(nodeId))
            return null;

        int rank = GetTowerNodeRank(roleManager, nodeId);
        int maxRank = Mathf.Max(1, ReadIntMember(definition, "maxRank", 1));
        string stateText = GetTowerNodeStateText(roleManager, definition);
        bool canPurchase = GetTowerNodeCanPurchase(roleManager, nodeId);
        bool isKeystone = IsTowerKeystoneDefinition(definition);
        string pathKey = GetTowerPathKey(definition);
        MetaHubTone baseTone = isKeystone ? MetaHubTone.Purple : GetTowerTreeCategoryTone(roleManager, pathKey);
        MetaHubTone tone = rank >= maxRank || stateText.IndexOf("Aktiv", StringComparison.OrdinalIgnoreCase) >= 0 ? MetaHubTone.Green : canPurchase ? MetaHubTone.Gold : baseTone;

        return new TowerMasteryNodeView
        {
            definition = definition,
            nodeId = nodeId,
            displayName = ReadStringMember(definition, "displayName", nodeId),
            effectText = ReadStringMember(definition, "effectText", stateText),
            pathKey = pathKey,
            pathLabel = GetTowerTreeCategoryDisplayName(roleManager, pathKey),
            isKeystone = isKeystone,
            rank = rank,
            maxRank = maxRank,
            nextCost = GetTowerNodeNextCost(definition, rank),
            canPurchase = canPurchase,
            stateText = stateText,
            tone = tone
        };
    }

    private List<object> GetTowerDefinitionObjects(object roleManager)
    {
        List<object> definitions = new List<object>();
        if (roleManager == null)
            return definitions;

        object result = InvokeNoArg(roleManager, "GetDefinitions");
        IEnumerable enumerable = result as IEnumerable;
        if (enumerable == null)
            return definitions;

        foreach (object definition in enumerable)
        {
            if (definition != null)
                definitions.Add(definition);
        }

        return definitions;
    }

    private string GetTowerTreeCategoryDisplayName(object roleManager, string categoryId)
    {
        if (categoryId == "Keystones")
            return "Keystones";

        object pathValue = FindTowerPathValue(roleManager, categoryId);
        string displayName = pathValue != null ? InvokeStringWithObjectArg(roleManager, "GetPathDisplayName", pathValue, "") : "";
        if (!string.IsNullOrEmpty(displayName))
            return displayName;

        if (categoryId == "Trunk")
            return "Basis";

        return SplitPascalCase(categoryId);
    }

    private MetaHubTone GetTowerTreeCategoryTone(object roleManager, string categoryId)
    {
        if (categoryId == "Keystones")
            return MetaHubTone.Purple;

        List<string> categories = GetTowerTreeCategoryKeys(roleManager);
        int index = categories.IndexOf(categoryId);
        if (index <= 0)
            return MetaHubTone.Purple;
        if (index == 1)
            return MetaHubTone.Gold;
        if (index == 2)
            return MetaHubTone.Cyan;
        if (index == 3)
            return MetaHubTone.Red;

        return MetaHubTone.Blue;
    }

    private string GetTowerPathKey(object definition)
    {
        object pathValue = ReadMember(definition, "path");
        return pathValue != null ? pathValue.ToString() : "Trunk";
    }

    private object FindTowerPathValue(object roleManager, string categoryId)
    {
        List<object> definitions = GetTowerDefinitionObjects(roleManager);
        for (int i = 0; i < definitions.Count; i++)
        {
            object pathValue = ReadMember(definitions[i], "path");
            if (pathValue != null && pathValue.ToString() == categoryId)
                return pathValue;
        }

        return null;
    }

    private bool IsTowerKeystoneDefinition(object definition)
    {
        object keystone = ReadMember(definition, "keystone");
        if (keystone == null)
            return false;

        return !string.Equals(keystone.ToString(), "None", StringComparison.OrdinalIgnoreCase);
    }

    private int GetTowerNodeRank(object roleManager, string nodeId)
    {
        object result = InvokeStringArg(roleManager, "GetNodeRank", nodeId);
        return result is int ? Mathf.Max(0, (int)result) : 0;
    }

    private bool GetTowerNodeCanPurchase(object roleManager, string nodeId)
    {
        object result = InvokeStringArg(roleManager, "CanPurchaseNode", nodeId);
        return result is bool && (bool)result;
    }

    private int GetTowerNodeNextCost(object definition, int rank)
    {
        object result = InvokeSingleArg(definition, "GetCostForNextRank", rank);
        return result is int ? Mathf.Max(0, (int)result) : 0;
    }

    private string GetTowerNodeStateText(object roleManager, object definition)
    {
        return InvokeStringWithObjectArg(roleManager, "GetNodeStateText", definition, "");
    }

    private string GetTowerNodeCostLabel(TowerMasteryNodeView view)
    {
        if (runtimeUnlockInfoMode)
            return "";

        if (view.rank >= view.maxRank)
            return view.isKeystone ? "KEYSTONE" : "MAX";

        return Mathf.Max(0, view.nextCost) + " P";
    }

    private string GetTowerNodeStateLabel(TowerMasteryNodeView view)
    {
        if (runtimeUnlockInfoMode)
        {
            if (view == null)
                return "";
            if (!string.IsNullOrEmpty(view.stateText) && view.stateText.IndexOf("Aktiv", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AKTIV";
            if (view.rank > 0)
                return "FREI";
            return "";
        }

        if (view.stateText.IndexOf("Aktiv", StringComparison.OrdinalIgnoreCase) >= 0)
            return "AKTIV";
        if (view.rank >= view.maxRank)
            return "FREI";
        if (view.canPurchase)
            return "KAUFEN";
        if (view.stateText.IndexOf("Read-only", StringComparison.OrdinalIgnoreCase) >= 0)
            return "RUN";
        if (view.stateText.IndexOf("Voraus", StringComparison.OrdinalIgnoreCase) >= 0)
            return "VORBED.";
        if (view.stateText.IndexOf("Gesperrt", StringComparison.OrdinalIgnoreCase) >= 0)
            return "GESPERRT";

        return Shorten(view.stateText, 10);
    }

    private void HandleTowerMasteryNodeClick(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return;

        object roleManager = GetTowerRoleMasteryManager(selectedTowerRole);
        object definition = GetTowerDefinitionObject(roleManager, nodeId);
        if (roleManager == null || definition == null)
            return;

        int rank = GetTowerNodeRank(roleManager, nodeId);
        int maxRank = Mathf.Max(1, ReadIntMember(definition, "maxRank", 1));
        bool changed = false;

        if (rank >= maxRank && IsTowerKeystoneDefinition(definition))
            changed = TryActivateTowerKeystone(roleManager, definition);

        if (!changed)
            changed = TryPurchaseTowerNode(roleManager, nodeId);

        if (changed)
            RefreshData();
    }

    private object GetTowerDefinitionObject(object roleManager, string nodeId)
    {
        object direct = InvokeStringArg(roleManager, "GetDefinition", nodeId);
        if (direct != null)
            return direct;

        List<object> definitions = GetTowerDefinitionObjects(roleManager);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (ReadStringMember(definitions[i], "nodeId", "") == nodeId)
                return definitions[i];
        }

        return null;
    }

    private bool TryPurchaseTowerNode(object roleManager, string nodeId)
    {
        object result = InvokeStringArg(roleManager, "TryPurchaseNode", nodeId);
        return result is bool && (bool)result;
    }

    private bool TryActivateTowerKeystone(object roleManager, object definition)
    {
        object keystone = ReadMember(definition, "keystone");
        if (keystone == null || string.Equals(keystone.ToString(), "None", StringComparison.OrdinalIgnoreCase))
            return false;

        object result = InvokeSingleArg(roleManager, "TryActivateKeystone", keystone);
        return result is bool && (bool)result;
    }

    private object ReadMember(object instance, string name)
    {
        if (instance == null || string.IsNullOrEmpty(name))
            return null;

        Type type = instance.GetType();
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            return field.GetValue(instance);

        PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.GetIndexParameters().Length == 0)
            return property.GetValue(instance, null);

        return null;
    }

    private string ReadStringMember(object instance, string name, string fallback)
    {
        object value = ReadMember(instance, name);
        return value != null ? value.ToString() : fallback;
    }

    private int ReadIntMember(object instance, string name, int fallback)
    {
        object value = ReadMember(instance, name);
        if (value is int)
            return (int)value;

        if (value is Enum)
            return Convert.ToInt32(value);

        return fallback;
    }

    private object InvokeNoArg(object target, string methodName)
    {
        if (target == null || string.IsNullOrEmpty(methodName))
            return null;

        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (method == null)
            return null;

        try
        {
            return method.Invoke(target, null);
        }
        catch (TargetInvocationException ex)
        {
            Debug.LogWarning("MetaHub: Tower mastery call failed: " + methodName + " - " + ex.Message);
            return null;
        }
    }

    private object InvokeStringArg(object target, string methodName, string value)
    {
        return InvokeSingleArg(target, methodName, value);
    }

    private string InvokeStringWithObjectArg(object target, string methodName, object value, string fallback)
    {
        object result = InvokeSingleArg(target, methodName, value);
        return result != null ? result.ToString() : fallback;
    }

    private object InvokeSingleArg(object target, string methodName, object value)
    {
        if (target == null || string.IsNullOrEmpty(methodName))
            return null;

        MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != methodName)
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1 || !CanPassArgument(parameters[0].ParameterType, value))
                continue;

            try
            {
                return method.Invoke(target, new object[] { value });
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogWarning("MetaHub: Tower mastery call failed: " + methodName + " - " + ex.Message);
                return null;
            }
        }

        return null;
    }

    private bool CanPassArgument(Type parameterType, object value)
    {
        if (value == null)
            return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;

        return parameterType.IsAssignableFrom(value.GetType());
    }

    private string SplitPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                builder.Append(' ');
            builder.Append(c);
        }

        return builder.ToString();
    }

    private void CreateUnlockChaosContent(Transform main, MetaHubData data)
    {
        if (CreateChaosSkillTreeLayout(main, data))
            return;

        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        int riskPurchased = chaos != null ? chaos.GetPurchasedCount(ChaosResearchCategory.RiskPool) : 0;
        int riskTotal = chaos != null ? Mathf.Max(1, chaos.GetDefinitionCount(ChaosResearchCategory.RiskPool)) : 1;
        int variantsPurchased = chaos != null ? chaos.GetPurchasedCount(ChaosResearchCategory.ChaosVariants) : 0;
        int variantsTotal = chaos != null ? Mathf.Max(1, chaos.GetDefinitionCount(ChaosResearchCategory.ChaosVariants)) : 1;

        CreateUnlockStatCard(main, "ChaosKnowledge", "CHAOS-WISSEN", chaos != null ? MetaHubMockData.FormatNumber(chaos.chaosKnowledge) : GetResourceValue(data, "chaos").ToString(), "Waehrung", "icon_chaos", MetaHubTone.Purple, new Vector2(-445f, 210f), new Vector2(230f, 145f));
        CreateUnlockStatCard(main, "RiskCore", "RISIKOKERNE", chaos != null ? MetaHubMockData.FormatNumber(chaos.riftCores) : GetMetricValue(data, "risikokerne", "0"), "Risskerne", "icon_risk_core", MetaHubTone.Red, new Vector2(-190f, 210f), new Vector2(230f, 145f));
        Transform summary = CreateUnlockPanel(main, "ChaosSummary", "CHAOS-FORTSCHRITT", new Vector2(90f, 210f), new Vector2(285f, 145f), new Color32(128, 83, 176, 255));
        CreateUnlockRow(summary, "Summary1", "icon_chaos", "Forschung", variantsPurchased + " Varianten", variantsPurchased + " / " + variantsTotal, MetaHubTone.Purple, 20f, 235f);
        CreateUnlockRow(summary, "Summary2", "icon_risk_core", "Risiko-Pool", "Freigeschaltet", riskPurchased + " / " + riskTotal, MetaHubTone.Red, -38f, 235f);
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(455f, 210f), new Vector2(260f, 185f));

        Transform pool = CreateUnlockPanel(main, "RiskPool", "RISIKO-POOL", new Vector2(-430f, 15f), new Vector2(260f, 210f), new Color32(128, 83, 176, 255));
        CreateDonutImage(pool, "RiskRing", new Vector2(-70f, 26f), 96f, Percent(riskPurchased, riskTotal), new Color32(176, 98, 255, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(pool, "RiskCount", riskPurchased.ToString(), new Vector2(-70f, 34f), new Vector2(80f, 34f), 30f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(pool, "RiskCaption", "Freigeschaltet", new Vector2(-70f, -8f), new Vector2(110f, 20f), 11f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        CreateChaosNodeRow(pool, "Pool1", "chaos.risk.core", "Grundrisiken", "icon_runner", MetaHubTone.Purple, 48f, 150f);
        CreateChaosNodeRow(pool, "Pool2", "chaos.risk.reward_1", "Belohnungsrisiken", "icon_spawn", MetaHubTone.Purple, -12f, 150f);
        CreateCanvasButton(pool, "ManageRisk", "VERWALTEN", new Vector2(0f, -82f), new Vector2(160f, 32f), delegate { Debug.Log("MetaHub: Risk pool requested."); });

        Transform variants = CreateUnlockPanel(main, "ChaosVariants", "CHAOS-VARIANTEN", new Vector2(-85f, 15f), new Vector2(380f, 210f), new Color32(167, 54, 45, 255));
        CreateChaosNodeCard(variants, "chaos.variant.runner", "Runner Studie", "icon_skull", MetaHubTone.Red, new Vector2(-118f, -12f), new Vector2(104f, 110f));
        CreateChaosNodeCard(variants, "chaos.variant.tank", "Tank Studie", "icon_spawn", MetaHubTone.Red, new Vector2(0f, -12f), new Vector2(104f, 110f));
        CreateChaosNodeCard(variants, "chaos.variant.knight", "Knight Studie", "icon_chaos_sun", MetaHubTone.Red, new Vector2(118f, -12f), new Vector2(104f, 110f));

        Transform waves = CreateUnlockPanel(main, "ChaosWaves", "CHAOS-WAVES", new Vector2(225f, 15f), new Vector2(170f, 210f), new Color32(167, 54, 45, 255));
        int highestWave = chaos != null ? Mathf.Max(0, chaos.highestWaveEver) : 0;
        CreateDonutImage(waves, "WaveRing", new Vector2(0f, 20f), 105f, Percent(highestWave, 30), new Color32(255, 84, 78, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(waves, "WaveValue", highestWave.ToString(), new Vector2(0f, 28f), new Vector2(80f, 38f), 32f, new Color32(255, 230, 180, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(waves, "WaveCaption", "Höchste Wave", new Vector2(0f, -15f), new Vector2(120f, 20f), 11f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        CreateCanvasButton(waves, "Waves", "WAVES ANZEIGEN", new Vector2(0f, -82f), new Vector2(140f, 32f), delegate { Debug.Log("MetaHub: Chaos waves requested."); });

        Transform progress = CreateUnlockPanel(main, "ResearchProgress", "AKTUELLER FORTSCHRITT", new Vector2(465f, -85f), new Vector2(260f, 250f), new Color32(128, 83, 176, 255));
        CreateUnlockRow(progress, "Research1", "icon_chaos", "Letzter Run", "Chaos-Wissen", chaos != null ? "+" + chaos.lastRunChaosKnowledgeGained : "+0", MetaHubTone.Purple, 55f, 220f);
        CreateUnlockRow(progress, "Research2", "icon_skull", "Risskerne", "Letzter Run", chaos != null ? "+" + chaos.lastRunRiftCoresGained : "+0", MetaHubTone.Red, 0f, 220f);
        CreateUnlockRow(progress, "Research3", "icon_chaos_sun", "Chaos-Waves", "Gesamt", chaos != null ? chaos.totalChaosWavesCompletedEver.ToString() : "0", MetaHubTone.Red, -55f, 220f);
        CreateCanvasButton(progress, "ActiveResearch", "ALLE AKTIVEN ANZEIGEN", new Vector2(0f, -100f), new Vector2(205f, 34f), delegate { Debug.Log("MetaHub: Active research requested."); });

        Transform counter = CreateUnlockPanel(main, "ChaosCounter", "CHAOS-KONTER", new Vector2(-360f, -250f), new Vector2(370f, 175f), new Color32(128, 83, 176, 255));
        CreateChaosNodeCard(counter, "chaos.counter.runner_1", "Runner", "icon_chaos_sun", MetaHubTone.Red, new Vector2(-115f, -18f), new Vector2(100f, 108f));
        CreateChaosNodeCard(counter, "chaos.counter.tank_1", "Tank", "icon_blue_star", MetaHubTone.Blue, new Vector2(0f, -18f), new Vector2(100f, 108f));
        CreateChaosNodeCard(counter, "chaos.counter.knight_1", "Knight", "icon_shield", MetaHubTone.Neutral, new Vector2(115f, -18f), new Vector2(100f, 108f));

        Transform endgame = CreateUnlockPanel(main, "ChaosEndgame", "CHAOS-5-ENDGAME", new Vector2(45f, -250f), new Vector2(380f, 175f), new Color32(167, 54, 45, 255));
        CreateChaosNodeRow(endgame, "End1", "chaos.endgame.unlock", "Risskern-Forschung", "icon_chaos_sun", MetaHubTone.Cyan, 38f, 330f);
        CreateChaosNodeRow(endgame, "End2", "chaos.endgame.wave_core_1", "Chaos-5-Wave", "icon_risk_core", MetaHubTone.Neutral, -18f, 330f);
        CreateChaosNodeRow(endgame, "End3", "chaos.endgame.boss_core_1", "Rissboss-Probe", "icon_skull", MetaHubTone.Neutral, -74f, 330f);
    }

    private void CreateUnlockPathContent(Transform main, MetaHubData data)
    {
        if (CreatePathSkillTreeLayout(main, data))
            return;

        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        int pathLevel = path != null ? path.pathTechniqueLevel : 1;
        int pathCurrentXP = path != null ? path.GetXPIntoCurrentLevel() : 0;
        int pathRequiredXP = path != null ? path.GetXPToNextPathTechniqueLevel() : 1;

        Transform header = CreateUnlockPanel(main, "PathHeader", "VERBAU / PFADTECHNIK", new Vector2(-250f, 210f), new Vector2(640f, 155f), new Color32(47, 169, 210, 255));
        CreateArtIcon(header, "PathIcon", "icon_path", new Vector2(-245f, 0f), new Vector2(100f, 100f));
        CreateDonutImage(header, "PathRing", new Vector2(-115f, 0f), 104f, Percent(pathCurrentXP, pathRequiredXP), new Color32(55, 209, 235, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(header, "PathLevel", pathLevel.ToString(), new Vector2(-115f, 8f), new Vector2(80f, 40f), 32f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(header, "PathXP", MetaHubMockData.FormatNumber(pathCurrentXP) + " / " + MetaHubMockData.FormatNumber(pathRequiredXP) + " XP", new Vector2(50f, 28f), new Vector2(220f, 22f), 13f, new Color32(244, 226, 186, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasBar(header, "PathBar", new Vector2(55f, 2f), new Vector2(220f, 7f), Percent(pathCurrentXP, pathRequiredXP), new Color32(55, 209, 235, 255));
        CreateCanvasLabel(header, "PathNext", "Blueprints " + (path != null ? path.blueprints.ToString() : "0") + " / Rissbauplaene " + (path != null ? path.riftBlueprints.ToString() : "0") + "\nSlots " + (path != null ? path.GetUsedLoadoutSlots() + " / " + path.GetLoadoutSlotCapacity() : "0 / 0"), new Vector2(70f, -42f), new Vector2(260f, 42f), 11f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateUnlockStatCard(main, "Blueprints", "BAUPLÄNE", path != null ? MetaHubMockData.FormatNumber(path.blueprints) : GetMetricValue(data, "bauplaene", "0"), "Aktuell", "icon_blueprint", MetaHubTone.Blue, new Vector2(245f, 210f), new Vector2(180f, 155f));
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(505f, 115f), new Vector2(240f, 280f));

        Transform events = CreateUnlockPanel(main, "EventPool", "EVENT-POOL", new Vector2(-340f, -70f), new Vector2(420f, 300f), new Color32(47, 169, 210, 255));
        CreatePathNodeCard(events, "path.event.core", "Grund-Events", "icon_gold", MetaHubTone.Cyan, new Vector2(-135f, 55f), new Vector2(108f, 108f));
        CreatePathNodeCard(events, "path.event.build_time", "Baupause", "icon_path", MetaHubTone.Cyan, new Vector2(0f, 55f), new Vector2(108f, 108f));
        CreatePathNodeCard(events, "path.event.path_scan", "Pfadscan", "icon_blueprint", MetaHubTone.Cyan, new Vector2(135f, 55f), new Vector2(108f, 108f));
        CreatePathNodeCard(events, "path.event.base_relocate", "Basisversatz", "icon_path", MetaHubTone.Cyan, new Vector2(-135f, -82f), new Vector2(108f, 108f));
        CreatePathNodeCard(events, "path.event.teleporter", "Teleportbasis", "icon_chaos", MetaHubTone.Cyan, new Vector2(0f, -82f), new Vector2(108f, 108f));
        CreatePathNodeCard(events, "path.event.choice_cache", "Krisenlager", "icon_blueprint", MetaHubTone.Cyan, new Vector2(135f, -82f), new Vector2(108f, 108f));

        Transform tools = CreateUnlockPanel(main, "PathTools", "PFADWERKZEUGE", new Vector2(125f, -70f), new Vector2(350f, 300f), new Color32(47, 169, 210, 255));
        CreatePathNodeRow(tools, "Tool1", "path.tool.reserved_preview", "Warnung", "icon_path", MetaHubTone.Cyan, 82f, 300f);
        CreatePathNodeRow(tools, "Tool2", "path.tool.direction_preview", "Richtungs-Vorschau", "icon_path", MetaHubTone.Cyan, 26f, 300f);
        CreatePathNodeRow(tools, "Tool3", "path.tool.path_scan_1", "Pfadscan I", "icon_path", MetaHubTone.Cyan, -30f, 300f);
        CreatePathNodeRow(tools, "Tool4", "path.tool.path_reroll_1", "Pfadwahl-Reroll", "icon_path", MetaHubTone.Cyan, -86f, 300f);
        CreateCanvasButton(tools, "ManageTools", "PFADWERKZEUGE VERWALTEN", new Vector2(0f, -124f), new Vector2(260f, 32f), delegate { Debug.Log("MetaHub: Path tools requested."); });

        Transform rescues = CreateUnlockPanel(main, "Rescues", "LETZTE RETTUNGEN", new Vector2(505f, -225f), new Vector2(240f, 220f), new Color32(47, 169, 210, 255));
        CreateUnlockRow(rescues, "Rescue1", "icon_path", "Pfad-XP", "Letzter Run", path != null ? "+" + path.lastRunPathTechniqueXPGained : "+0", MetaHubTone.Cyan, 48f, 200f);
        CreateUnlockRow(rescues, "Rescue2", "icon_path", "Bauplaene", "Letzter Run", path != null ? "+" + path.lastRunBlueprintsGained : "+0", MetaHubTone.Cyan, -6f, 200f);
        CreateUnlockRow(rescues, "Rescue3", "icon_path", "Rissbauplaene", "Letzter Run", path != null ? "+" + path.lastRunRiftBlueprintsGained : "+0", MetaHubTone.Cyan, -60f, 200f);
        CreateCanvasButton(rescues, "AllRescues", "ALLE RETTUNGEN", new Vector2(0f, -88f), new Vector2(180f, 30f), delegate { Debug.Log("MetaHub: Rescues requested."); });
    }

    private void CreateUnlockEliteContent(Transform main, MetaHubData data)
    {
        if (CreateEliteSkillTreeLayout(main, data))
            return;

        EliteHuntProgressionManager elite = GetEliteHuntManager();

        CreateUnlockStatCard(main, "EliteSeals", "ELITE-SIEGEL", elite != null ? MetaHubMockData.FormatNumber(elite.eliteSeals) : GetResourceValue(data, "special").ToString(), "Elite-Siegel", "icon_skull", MetaHubTone.Red, new Vector2(-440f, 210f), new Vector2(240f, 145f));
        CreateUnlockStatCard(main, "EliteRank", "ELITE-RANG", elite != null ? elite.eliteRank.ToString() : "1", elite != null ? EliteHuntProgressionManager.GetHuntModeDisplayName(elite.activeHuntMode) : "Aus", "icon_risk_core", MetaHubTone.Red, new Vector2(-170f, 210f), new Vector2(240f, 145f));
        CreateUnlockStatCard(main, "EliteKills", "ELITE-KILLS", elite != null ? MetaHubMockData.FormatNumber(elite.totalEliteKillsEver) : GetMetricValue(data, "elite_jagd", "0"), "Gesamt Elite-Kills", "icon_skull", MetaHubTone.Red, new Vector2(100f, 210f), new Vector2(240f, 145f));
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(415f, 210f), new Vector2(300f, 185f));

        Transform contracts = CreateUnlockPanel(main, "EliteContracts", "AKTIVE ELITE-AUFTRÄGE", new Vector2(-345f, 30f), new Vector2(430f, 230f), new Color32(167, 54, 45, 255));
        CreateEliteNodeRow(contracts, "Contract1", "elite.contract.runner_1", "Runner-Jagd", "icon_skull", MetaHubTone.Red, 58f, 380f);
        CreateEliteNodeRow(contracts, "Contract2", "elite.contract.chaos_1", "Rissjagd", "icon_chaos", MetaHubTone.Purple, -1f, 380f);
        CreateEliteNodeRow(contracts, "Contract3", "elite.contract.boss_1", "Elite-Boss", "icon_risk_core", MetaHubTone.Red, -60f, 380f);
        CreateCanvasButton(contracts, "AllContracts", "ALLE AUFTRÄGE ANZEIGEN", new Vector2(0f, -92f), new Vector2(250f, 32f), delegate { Debug.Log("MetaHub: Elite contracts requested."); });

        Transform affixes = CreateUnlockPanel(main, "EliteAffixes", "AKTIVE ELITE-AFFIXE", new Vector2(105f, 30f), new Vector2(350f, 230f), new Color32(167, 54, 45, 255));
        CreateEliteNodeCard(affixes, "elite.affix.unlock_basic", "Basis-Affixe", "icon_chaos", MetaHubTone.Purple, new Vector2(-105f, -12f), new Vector2(96f, 108f));
        CreateEliteNodeCard(affixes, "elite.affix.regen", "Regeneration", "icon_shield", MetaHubTone.Cyan, new Vector2(0f, -12f), new Vector2(96f, 108f));
        CreateEliteNodeCard(affixes, "elite.affix.chaotic", "Chaotisch", "icon_buff_xp", MetaHubTone.Green, new Vector2(105f, -12f), new Vector2(96f, 108f));

        Transform stats = CreateUnlockPanel(main, "EliteStats", "LETZTE JAGD - STATISTIKEN", new Vector2(465f, -55f), new Vector2(250f, 270f), new Color32(122, 94, 55, 255));
        CreateUnlockRow(stats, "Stat1", "icon_skull", "Gesehen", "Gesamt / letzter Run", elite != null ? elite.totalElitesSeenEver + " / " + elite.lastRunElitesSeen : "0 / 0", MetaHubTone.Red, 72f, 210f);
        CreateUnlockRow(stats, "Stat2", "icon_risk_core", "Besiegt", "Gesamt / letzter Run", elite != null ? elite.totalEliteKillsEver + " / " + elite.lastRunEliteKills : "0 / 0", MetaHubTone.Gold, 18f, 210f);
        CreateUnlockRow(stats, "Stat3", "icon_skull", "Leaks", "Gesamt", elite != null ? elite.totalEliteLeaksEver.ToString() : "0", MetaHubTone.Red, -36f, 210f);
        CreateUnlockRow(stats, "Stat4", "icon_gold", "Elite-Siegel", "Letzter Run", elite != null ? "+" + elite.lastRunEliteSealsGained : "+0", MetaHubTone.Gold, -90f, 210f);

        Transform rewards = CreateUnlockPanel(main, "EliteRewards", "ELITE-BELOHNUNGEN", new Vector2(-375f, -250f), new Vector2(360f, 175f), new Color32(167, 54, 45, 255));
        CreateEliteNodeCard(rewards, "elite.reward.seal_1", "Siegelkunde I", "icon_risk_core", MetaHubTone.Red, new Vector2(-115f, -18f), new Vector2(96f, 108f));
        CreateEliteNodeCard(rewards, "elite.reward.mastery_1", "Meisterproben", "icon_chaos", MetaHubTone.Purple, new Vector2(0f, -18f), new Vector2(96f, 108f));
        CreateEliteNodeCard(rewards, "elite.reward.knowledge_1", "Kernwissen", "icon_skull", MetaHubTone.Red, new Vector2(115f, -18f), new Vector2(96f, 108f));

        Transform frequency = CreateUnlockPanel(main, "EliteFrequency", "ELITE-HÄUFIGKEIT", new Vector2(45f, -250f), new Vector2(300f, 175f), new Color32(167, 54, 45, 255));
        int eliteCurrentXP = elite != null ? elite.GetXPIntoCurrentRank() : 0;
        int eliteRequiredXP = elite != null ? elite.GetXPToNextEliteRank() : 1;
        CreateCanvasLabel(frequency, "Chance", "Elite-Rang-XP: " + eliteCurrentXP + " / " + eliteRequiredXP + "\nAktiver Modus: " + (elite != null ? EliteHuntProgressionManager.GetHuntModeDisplayName(elite.activeHuntMode) : "Aus"), new Vector2(0f, 28f), new Vector2(250f, 50f), 13f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasBar(frequency, "ChanceBar", new Vector2(0f, -22f), new Vector2(220f, 8f), Percent(eliteCurrentXP, eliteRequiredXP), new Color32(255, 84, 78, 255));
        CreateCanvasButton(frequency, "ImproveElite", "MODUS VERWALTEN", new Vector2(0f, -66f), new Vector2(220f, 34f), delegate { Debug.Log("MetaHub: Elite frequency requested."); });
    }

    private GeneralMetaProgressionManager GetGeneralMetaManager()
    {
        ResolveGameManager();
        return gameManager != null ? gameManager.GetGeneralMetaProgressionManager() : FindObjectOfType<GeneralMetaProgressionManager>();
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        ResolveGameManager();
        return gameManager != null ? gameManager.GetTowerMasteryManager() : FindObjectOfType<TowerMasteryManager>();
    }

    private ChaosResearchProgressionManager GetChaosResearchManager()
    {
        ResolveGameManager();
        return gameManager != null ? gameManager.GetChaosResearchProgressionManager() : FindObjectOfType<ChaosResearchProgressionManager>();
    }

    private PathTechniqueProgressionManager GetPathTechniqueManager()
    {
        ResolveGameManager();
        return gameManager != null ? gameManager.GetPathTechniqueProgressionManager() : FindObjectOfType<PathTechniqueProgressionManager>();
    }

    private EliteHuntProgressionManager GetEliteHuntManager()
    {
        ResolveGameManager();
        return gameManager != null ? gameManager.GetEliteHuntProgressionManager() : FindObjectOfType<EliteHuntProgressionManager>();
    }

    private void SelectTowerRole(TowerRole role)
    {
        if (selectedTowerRole == role && selectedTowerTreeCategory == "Trunk")
            return;

        selectedTowerRole = role;
        selectedTowerTreeCategory = "Trunk";
        RefreshMainFrameOnly();
    }

    private void SelectTowerTreeCategory(string categoryId)
    {
        string nextCategoryId = string.IsNullOrEmpty(categoryId) ? "Trunk" : categoryId;
        if (selectedTowerTreeCategory == nextCategoryId)
            return;

        selectedTowerTreeCategory = nextCategoryId;
        RefreshMainFrameOnly();
    }

    private void SelectChaosTreeCategory(ChaosResearchCategory category)
    {
        ChaosResearchCategory nextCategory = category == ChaosResearchCategory.Overview ? ChaosResearchCategory.RiskPool : category;
        if (selectedChaosTreeCategory == nextCategory)
            return;

        selectedChaosTreeCategory = nextCategory;
        RefreshMainFrameOnly();
    }

    private void SelectPathTreeCategory(PathTechniqueCategory category)
    {
        PathTechniqueCategory nextCategory = category == PathTechniqueCategory.Overview ? PathTechniqueCategory.EventPool : category;
        if (selectedPathTreeCategory == nextCategory)
            return;

        selectedPathTreeCategory = nextCategory;
        RefreshMainFrameOnly();
    }

    private void SelectEliteTreeCategory(EliteHuntCategory category)
    {
        EliteHuntCategory nextCategory = category == EliteHuntCategory.Overview ? EliteHuntCategory.Contracts : category;
        if (selectedEliteTreeCategory == nextCategory)
            return;

        selectedEliteTreeCategory = nextCategory;
        RefreshMainFrameOnly();
    }

    private void TrySpendSelectedTowerPoint()
    {
        if (runtimeUnlockInfoMode)
            return;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null && towerMastery.TrySpendRolePoints(selectedTowerRole, 1))
            RefreshData();
    }

    private void SelectGeneralTreeCategory(string categoryId)
    {
        string nextCategoryId = string.IsNullOrEmpty(categoryId) ? "tower" : categoryId;
        if (selectedGeneralTreeCategory == nextCategoryId && !showGeneralLoadoutPicker && !showGeneralLoadoutEditor && string.IsNullOrEmpty(pendingGeneralLoadoutPromptNodeId))
            return;

        selectedGeneralTreeCategory = nextCategoryId;
        showGeneralLoadoutPicker = false;
        showGeneralLoadoutEditor = false;
        pendingGeneralLoadoutPromptNodeId = "";
        RefreshMainFrameOnly();
    }

    private void OpenGeneralLoadoutPicker()
    {
        if (runtimeUnlockInfoMode)
            return;

        pendingGeneralLoadoutPromptNodeId = "";
        showGeneralLoadoutPicker = true;
        showGeneralLoadoutEditor = false;
        RefreshMainFrameOnly();
    }

    private void OpenGeneralLoadoutEditor()
    {
        if (runtimeUnlockInfoMode)
            return;

        pendingGeneralLoadoutPromptNodeId = "";
        showGeneralLoadoutPicker = false;
        showGeneralLoadoutEditor = true;
        RefreshMainFrameOnly();
    }

    private void CloseGeneralLoadoutOverlay()
    {
        showGeneralLoadoutPicker = false;
        showGeneralLoadoutEditor = false;
        pendingGeneralLoadoutPromptNodeId = "";
        RefreshMainFrameOnly();
    }

    private void PromptGeneralLoadoutAfterPurchase(string nodeId)
    {
        pendingGeneralLoadoutPromptNodeId = string.IsNullOrEmpty(nodeId) ? "" : nodeId;
        showGeneralLoadoutPicker = false;
        showGeneralLoadoutEditor = false;
    }

    private void ActivatePromptedGeneralLoadoutNode()
    {
        if (runtimeUnlockInfoMode)
            return;

        string nodeId = pendingGeneralLoadoutPromptNodeId;
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general != null && general.TryActivateNode(nodeId))
        {
            pendingGeneralLoadoutPromptNodeId = "";
            RefreshMainFrameOnly();
            return;
        }

        OpenGeneralLoadoutEditorFromPrompt();
    }

    private void OpenGeneralLoadoutEditorFromPrompt()
    {
        if (runtimeUnlockInfoMode)
            return;

        pendingGeneralLoadoutPromptNodeId = "";
        showGeneralLoadoutPicker = false;
        showGeneralLoadoutEditor = true;
        RefreshMainFrameOnly();
    }

    private void DismissGeneralLoadoutPrompt()
    {
        pendingGeneralLoadoutPromptNodeId = "";
        RefreshMainFrameOnly();
    }

    private void SelectGeneralLoadout(int loadoutIndex)
    {
        if (runtimeUnlockInfoMode)
            return;

        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general != null)
            general.SelectLoadout(loadoutIndex);

        showGeneralLoadoutPicker = false;
        showGeneralLoadoutEditor = true;
        RefreshMainFrameOnly();
    }

    private void ToggleGeneralLoadoutNode(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return;

        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        GeneralMetaNodeState state = general != null ? general.GetNodeState(nodeId) : null;
        if (general == null || definition == null || state == null || !definition.RequiresLoadoutSlot() || !state.purchased)
            return;

        bool changed = state.active ? general.TryDeactivateNode(nodeId) : general.TryActivateNode(nodeId);
        if (changed)
            RefreshMainFrameOnly();
    }

    private void HandleGeneralNodeClick(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return;

        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        GeneralMetaNodeState state = general != null ? general.GetNodeState(nodeId) : null;
        if (general == null || definition == null || state == null)
            return;

        bool changed;
        bool purchasedNode = false;
        bool boughtLoadoutStartBonus = false;
        if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = general.TryDeactivateNode(nodeId);
        else if (general.CanActivateNode(nodeId))
            changed = general.TryActivateNode(nodeId);
        else
        {
            changed = general.TryPurchaseNode(nodeId);
            purchasedNode = changed;
            boughtLoadoutStartBonus = changed && definition.category == GeneralMetaCategory.StartOption && definition.RequiresLoadoutSlot();
        }

        if (boughtLoadoutStartBonus)
            PromptGeneralLoadoutAfterPurchase(nodeId);

        if (changed && purchasedNode)
            RefreshData();
        else if (changed)
            RefreshMainFrameOnly();
    }

    private void HandleChaosNodeClick(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return;

        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        ChaosResearchNodeDefinition definition = chaos != null ? chaos.GetDefinition(nodeId) : null;
        ChaosResearchNodeState state = chaos != null ? chaos.GetNodeState(nodeId) : null;
        if (chaos == null || definition == null || state == null)
            return;

        bool changed;
        bool purchasedNode = false;
        if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = chaos.TryDeactivateNode(nodeId);
        else if (chaos.CanActivateNode(nodeId))
            changed = chaos.TryActivateNode(nodeId);
        else
        {
            changed = chaos.TryPurchaseNode(nodeId);
            purchasedNode = changed;
        }

        if (changed && purchasedNode)
            RefreshData();
        else if (changed)
            RefreshMainFrameOnly();
    }

    private void HandlePathNodeClick(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return;

        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        PathTechniqueNodeDefinition definition = path != null ? path.GetDefinition(nodeId) : null;
        PathTechniqueNodeState state = path != null ? path.GetNodeState(nodeId) : null;
        if (path == null || definition == null || state == null)
            return;

        bool changed;
        bool purchasedNode = false;
        if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = path.TryDeactivateNode(nodeId);
        else if (path.CanActivateNode(nodeId))
            changed = path.TryActivateNode(nodeId);
        else
        {
            changed = path.TryPurchaseNode(nodeId);
            purchasedNode = changed;
        }

        if (changed && purchasedNode)
            RefreshData();
        else if (changed)
            RefreshMainFrameOnly();
    }

    private void HandleEliteNodeClick(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return;

        EliteHuntProgressionManager elite = GetEliteHuntManager();
        EliteHuntNodeDefinition definition = elite != null ? elite.GetDefinition(nodeId) : null;
        EliteHuntNodeState state = elite != null ? elite.GetNodeState(nodeId) : null;
        if (elite == null || definition == null || state == null)
            return;

        bool changed;
        bool purchasedNode = false;
        if (elite.IsHuntModeNode(nodeId) && elite.IsNodeActive(nodeId))
            changed = elite.TryDeactivateHuntMode();
        else if (elite.CanActivateHuntModeNode(nodeId))
            changed = elite.TryActivateHuntModeNode(nodeId);
        else if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = elite.TryDeactivateNode(nodeId);
        else if (elite.CanActivateNode(nodeId))
            changed = elite.TryActivateNode(nodeId);
        else
        {
            changed = elite.TryPurchaseNode(nodeId);
            purchasedNode = changed;
        }

        if (changed && purchasedNode)
            RefreshData();
        else if (changed)
            RefreshMainFrameOnly();
    }

    private string GetNextGeneralNodeId(GeneralMetaCategory category, string fallbackNodeId)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general == null)
            return fallbackNodeId;

        List<GeneralMetaNodeDefinition> definitions = general.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            GeneralMetaNodeDefinition definition = definitions[i];
            if (definition != null && definition.category == category && !general.IsNodePurchased(definition.nodeId))
                return definition.nodeId;
        }

        return fallbackNodeId;
    }

    private string GetGeneralNodeDisplayArt(string nodeId, string fallbackArtName)
    {
        if (string.IsNullOrEmpty(nodeId))
            return fallbackArtName;

        if (nodeId.IndexOf(".tile.", StringComparison.OrdinalIgnoreCase) >= 0)
            return nodeId.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0 ? "icon_gold" : nodeId.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0 ? "icon_risk_core" : "icon_path";

        return fallbackArtName;
    }

    private MetaHubTone GetGeneralNodeDisplayTone(string nodeId, MetaHubTone fallbackTone)
    {
        if (string.IsNullOrEmpty(nodeId))
            return fallbackTone;

        if (nodeId.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("damage", StringComparison.OrdinalIgnoreCase) >= 0)
            return MetaHubTone.Red;
        if (nodeId.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("rapid", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("sniper", StringComparison.OrdinalIgnoreCase) >= 0)
            return MetaHubTone.Gold;
        if (nodeId.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0)
            return MetaHubTone.Green;
        if (nodeId.IndexOf("slow", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("lightning", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("knock", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0)
            return MetaHubTone.Cyan;
        if (nodeId.IndexOf("alchemist", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("spike", StringComparison.OrdinalIgnoreCase) >= 0 || nodeId.IndexOf("combo", StringComparison.OrdinalIgnoreCase) >= 0)
            return MetaHubTone.Purple;

        return fallbackTone;
    }

    private void CreateGeneralNodeCard(Transform parent, string nodeId, string fallbackTitle, string artName, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        string capturedNodeId = nodeId;
        CreateUnlockShopCard(parent, "General_" + nodeId, GeneralNodeTitle(nodeId, fallbackTitle), GeneralNodeCost(nodeId), GeneralNodeStateLabel(nodeId), artName, GeneralNodeTone(nodeId, tone), position, size, delegate { HandleGeneralNodeClick(capturedNodeId); });
    }

    private string GeneralNodeTitle(string nodeId, string fallbackTitle)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        return Shorten(definition != null ? definition.displayName : fallbackTitle, 18);
    }

    private string GeneralNodeCost(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return "";

        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        return definition != null ? definition.cost.ToString() : "0";
    }

    private string GeneralNodeStateLabel(string nodeId)
    {
        if (runtimeUnlockInfoMode)
        {
            GeneralMetaProgressionManager runtimeGeneral = GetGeneralMetaManager();
            if (runtimeGeneral != null && runtimeGeneral.IsNodeActive(nodeId))
                return "AKTIV";
            if (runtimeGeneral != null && runtimeGeneral.IsNodePurchased(nodeId))
                return "FREI";
            return "";
        }

        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        if (general == null || definition == null)
            return "OFFEN";

        GeneralMetaNodeState state = general.GetNodeState(nodeId);
        if (general.IsNodeActive(nodeId))
            return "AKTIV";
        if (state != null && state.purchased)
            return "FREI";
        if (general.CanPurchaseNode(nodeId))
            return "KAUFBAR";
        if (general.accountLevel < definition.requiredAccountLevel)
            return "LV. " + definition.requiredAccountLevel;
        return "GESPERRT";
    }

    private MetaHubTone GeneralNodeTone(string nodeId, MetaHubTone fallback)
    {
        if (runtimeUnlockInfoMode)
            return fallback;

        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general == null)
            return fallback;

        if (general.IsNodeActive(nodeId) || general.IsNodePurchased(nodeId) || general.CanPurchaseNode(nodeId))
            return fallback;

        return MetaHubTone.Neutral;
    }

    private void CreateChaosNodeCard(Transform parent, string nodeId, string fallbackTitle, string artName, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        string capturedNodeId = nodeId;
        CreateUnlockShopCard(parent, "Chaos_" + nodeId, ChaosNodeTitle(nodeId, fallbackTitle), ChaosNodeCost(nodeId), ChaosNodeStateLabel(nodeId), artName, ChaosNodeTone(nodeId, tone), position, size, delegate { HandleChaosNodeClick(capturedNodeId); });
    }

    private void CreateChaosNodeRow(Transform parent, string name, string nodeId, string fallbackTitle, string artName, MetaHubTone tone, float y, float width)
    {
        string capturedNodeId = nodeId;
        CreateUnlockRow(parent, name, artName, ChaosNodeTitle(nodeId, fallbackTitle), ChaosNodeCost(nodeId), ChaosNodeStateLabel(nodeId), ChaosNodeTone(nodeId, tone), y, width, delegate { HandleChaosNodeClick(capturedNodeId); });
    }

    private string ChaosNodeTitle(string nodeId, string fallbackTitle)
    {
        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        ChaosResearchNodeDefinition definition = chaos != null ? chaos.GetDefinition(nodeId) : null;
        return Shorten(definition != null ? definition.displayName : fallbackTitle, 19);
    }

    private string ChaosNodeCost(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return "";

        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        ChaosResearchNodeDefinition definition = chaos != null ? chaos.GetDefinition(nodeId) : null;
        if (definition == null)
            return "0";
        if (definition.chaosKnowledgeCost > 0 && definition.riftCoreCost > 0)
            return definition.chaosKnowledgeCost + " CW+" + definition.riftCoreCost + " RK";
        if (definition.riftCoreCost > 0)
            return definition.riftCoreCost + " RK";
        return definition.chaosKnowledgeCost + " CW";
    }

    private string ChaosNodeStateLabel(string nodeId)
    {
        if (runtimeUnlockInfoMode)
        {
            ChaosResearchProgressionManager runtimeChaos = GetChaosResearchManager();
            if (runtimeChaos != null && runtimeChaos.IsNodeActive(nodeId))
                return "AKTIV";
            if (runtimeChaos != null && runtimeChaos.IsNodePurchased(nodeId))
                return "ERFORSCHT";
            return "";
        }

        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        if (chaos == null || chaos.GetDefinition(nodeId) == null)
            return "OFFEN";
        if (chaos.IsNodeActive(nodeId))
            return "AKTIV";
        if (chaos.IsNodePurchased(nodeId))
            return "ERFORSCHT";
        if (chaos.CanPurchaseNode(nodeId))
            return "ERFORSCHBAR";
        return "GESPERRT";
    }

    private MetaHubTone ChaosNodeTone(string nodeId, MetaHubTone fallback)
    {
        if (runtimeUnlockInfoMode)
            return fallback;

        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        if (chaos == null)
            return fallback;
        return chaos.IsNodeActive(nodeId) || chaos.IsNodePurchased(nodeId) || chaos.CanPurchaseNode(nodeId) ? fallback : MetaHubTone.Neutral;
    }

    private void CreatePathNodeCard(Transform parent, string nodeId, string fallbackTitle, string artName, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        string capturedNodeId = nodeId;
        CreateUnlockShopCard(parent, "Path_" + nodeId, PathNodeTitle(nodeId, fallbackTitle), PathNodeCost(nodeId), PathNodeStateLabel(nodeId), artName, PathNodeTone(nodeId, tone), position, size, delegate { HandlePathNodeClick(capturedNodeId); });
    }

    private void CreatePathNodeRow(Transform parent, string name, string nodeId, string fallbackTitle, string artName, MetaHubTone tone, float y, float width)
    {
        string capturedNodeId = nodeId;
        CreateUnlockRow(parent, name, artName, PathNodeTitle(nodeId, fallbackTitle), PathNodeCost(nodeId), PathNodeStateLabel(nodeId), PathNodeTone(nodeId, tone), y, width, delegate { HandlePathNodeClick(capturedNodeId); });
    }

    private string PathNodeTitle(string nodeId, string fallbackTitle)
    {
        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        PathTechniqueNodeDefinition definition = path != null ? path.GetDefinition(nodeId) : null;
        return Shorten(definition != null ? definition.displayName : fallbackTitle, 19);
    }

    private string PathNodeCost(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return "";

        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        PathTechniqueNodeDefinition definition = path != null ? path.GetDefinition(nodeId) : null;
        if (definition == null)
            return "0 BP";
        if (definition.blueprintCost > 0 && definition.riftBlueprintCost > 0)
            return definition.blueprintCost + " BP+" + definition.riftBlueprintCost + " RB";
        if (definition.riftBlueprintCost > 0)
            return definition.riftBlueprintCost + " RB";
        return definition.blueprintCost + " BP";
    }

    private string PathNodeStateLabel(string nodeId)
    {
        if (runtimeUnlockInfoMode)
        {
            PathTechniqueProgressionManager runtimePath = GetPathTechniqueManager();
            if (runtimePath != null && runtimePath.IsNodeActive(nodeId))
                return "AKTIV";
            if (runtimePath != null && runtimePath.IsNodePurchased(nodeId))
                return "FREI";
            return "";
        }

        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        if (path == null || path.GetDefinition(nodeId) == null)
            return "OFFEN";
        if (path.IsNodeActive(nodeId))
            return "AKTIV";
        if (path.IsNodePurchased(nodeId))
            return "FREI";
        if (path.CanPurchaseNode(nodeId))
            return "KAUFBAR";
        return "GESPERRT";
    }

    private MetaHubTone PathNodeTone(string nodeId, MetaHubTone fallback)
    {
        if (runtimeUnlockInfoMode)
            return fallback;

        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        if (path == null)
            return fallback;
        return path.IsNodeActive(nodeId) || path.IsNodePurchased(nodeId) || path.CanPurchaseNode(nodeId) ? fallback : MetaHubTone.Neutral;
    }

    private void CreateEliteNodeCard(Transform parent, string nodeId, string fallbackTitle, string artName, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        string capturedNodeId = nodeId;
        CreateUnlockShopCard(parent, "Elite_" + nodeId, EliteNodeTitle(nodeId, fallbackTitle), EliteNodeCost(nodeId), EliteNodeStateLabel(nodeId), artName, EliteNodeTone(nodeId, tone), position, size, delegate { HandleEliteNodeClick(capturedNodeId); });
    }

    private void CreateEliteNodeRow(Transform parent, string name, string nodeId, string fallbackTitle, string artName, MetaHubTone tone, float y, float width)
    {
        string capturedNodeId = nodeId;
        CreateUnlockRow(parent, name, artName, EliteNodeTitle(nodeId, fallbackTitle), EliteNodeCost(nodeId), EliteNodeStateLabel(nodeId), EliteNodeTone(nodeId, tone), y, width, delegate { HandleEliteNodeClick(capturedNodeId); });
    }

    private string EliteNodeTitle(string nodeId, string fallbackTitle)
    {
        EliteHuntProgressionManager elite = GetEliteHuntManager();
        EliteHuntNodeDefinition definition = elite != null ? elite.GetDefinition(nodeId) : null;
        return Shorten(definition != null ? definition.displayName : fallbackTitle, 19);
    }

    private string EliteNodeCost(string nodeId)
    {
        if (runtimeUnlockInfoMode)
            return "";

        EliteHuntProgressionManager elite = GetEliteHuntManager();
        EliteHuntNodeDefinition definition = elite != null ? elite.GetDefinition(nodeId) : null;
        if (definition == null)
            return "0";
        if (definition.sealCost > 0)
            return definition.sealCost + " ES";
        if (definition.riftCoreCost > 0)
            return definition.riftCoreCost + " RK";
        if (definition.blueprintCost > 0)
            return definition.blueprintCost + " BP";
        return "0";
    }

    private string EliteNodeStateLabel(string nodeId)
    {
        if (runtimeUnlockInfoMode)
        {
            EliteHuntProgressionManager runtimeElite = GetEliteHuntManager();
            if (runtimeElite != null && runtimeElite.IsNodeActive(nodeId))
                return "AKTIV";
            if (runtimeElite != null && runtimeElite.IsNodePurchased(nodeId))
                return "FREI";
            return "";
        }

        EliteHuntProgressionManager elite = GetEliteHuntManager();
        if (elite == null || elite.GetDefinition(nodeId) == null)
            return "OFFEN";
        if (elite.IsNodeActive(nodeId))
            return "AKTIV";
        if (elite.IsNodePurchased(nodeId))
            return "FREI";
        if (elite.CanPurchaseNode(nodeId))
            return "KAUFBAR";
        return "GESPERRT";
    }

    private MetaHubTone EliteNodeTone(string nodeId, MetaHubTone fallback)
    {
        if (runtimeUnlockInfoMode)
            return fallback;

        EliteHuntProgressionManager elite = GetEliteHuntManager();
        if (elite == null)
            return fallback;
        return elite.IsNodeActive(nodeId) || elite.IsNodePurchased(nodeId) || elite.CanPurchaseNode(nodeId) ? fallback : MetaHubTone.Neutral;
    }

    private bool CreateChaosSkillTreeLayout(Transform main, MetaHubData data)
    {
        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        EnsureSelectedChaosTreeCategory(chaos);

        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float topY = 216f;
        float topPanelHeight = 180f;
        float gap = 16f;
        float statWidth = 260f;
        float summaryLeft = left + statWidth * 2f + gap * 2f;
        float summaryWidth = Mathf.Max(420f, right - summaryLeft);

        int riskPurchased = chaos != null ? chaos.GetPurchasedCount(ChaosResearchCategory.RiskPool) : 0;
        int riskTotal = chaos != null ? Mathf.Max(1, chaos.GetDefinitionCount(ChaosResearchCategory.RiskPool)) : 1;
        int variantsPurchased = chaos != null ? chaos.GetPurchasedCount(ChaosResearchCategory.ChaosVariants) : 0;
        int variantsTotal = chaos != null ? Mathf.Max(1, chaos.GetDefinitionCount(ChaosResearchCategory.ChaosVariants)) : 1;
        int activeSlots = chaos != null ? chaos.GetUsedLoadoutSlots() : 0;
        int maxSlots = chaos != null ? chaos.GetLoadoutSlotCapacity() : 0;

        CreateUnlockStatCard(main, "ChaosKnowledge", "CHAOS-WISSEN", chaos != null ? MetaHubMockData.FormatNumber(chaos.chaosKnowledge) : GetResourceValue(data, "chaos").ToString(), "Forschung", "icon_chaos", MetaHubTone.Purple, new Vector2(left + statWidth * 0.5f, topY), new Vector2(statWidth, topPanelHeight));
        CreateUnlockStatCard(main, "RiskCore", "RISIKOKERNE", chaos != null ? MetaHubMockData.FormatNumber(chaos.riftCores) : GetMetricValue(data, "risikokerne", "0"), "Endgame-Kerne", "icon_risk_core", MetaHubTone.Red, new Vector2(left + statWidth + gap + statWidth * 0.5f, topY), new Vector2(statWidth, topPanelHeight));

        Transform summary = CreateUnlockPanel(main, "ChaosSummary", "CHAOS-STATUS", new Vector2(summaryLeft + summaryWidth * 0.5f, topY), new Vector2(summaryWidth, topPanelHeight), new Color32(128, 83, 176, 255));
        float rowWidth = summaryWidth - 68f;
        CreateUnlockCompactRow(summary, "SummaryVariants", "icon_chaos", "Varianten", "Erforscht", variantsPurchased + " / " + variantsTotal, MetaHubTone.Purple, 22f, rowWidth);
        CreateUnlockCompactRow(summary, "SummaryRisk", "icon_risk_core", "Risiko-Pool", "Freigeschaltet", riskPurchased + " / " + riskTotal, MetaHubTone.Red, -22f, rowWidth);
        CreateUnlockCompactRow(summary, "SummaryLoadout", "icon_keystone", "Chaos-Loadout", "Aktive Forschung", activeSlots + " / " + maxSlots, MetaHubTone.Gold, -66f, rowWidth);

        Transform tree = CreateUnlockPanel(main, "ChaosFocusedTree", "FORSCHUNGSBAUM", new Vector2((left + right) * 0.5f, -150f), new Vector2(right - left, 430f), ToneColor(GetChaosTreeCategoryTone(selectedChaosTreeCategory)));
        CreateChaosTreeTabs(tree, chaos, right - left);
        CreateChaosFocusedSkillTree(tree, chaos, right - left);
        return true;
    }

    private bool CreatePathSkillTreeLayout(Transform main, MetaHubData data)
    {
        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        EnsureSelectedPathTreeCategory(path);

        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float topY = 216f;
        float topPanelHeight = 180f;
        float gap = 16f;
        float headerWidth = 540f;
        float statWidth = 220f;
        float summaryLeft = left + headerWidth + gap + statWidth + gap;
        float summaryWidth = Mathf.Max(380f, right - summaryLeft);
        int pathLevel = path != null ? path.pathTechniqueLevel : 1;
        int pathCurrentXP = path != null ? path.GetXPIntoCurrentLevel() : 0;
        int pathRequiredXP = path != null ? path.GetXPToNextPathTechniqueLevel() : 1;

        Transform header = CreateUnlockPanel(main, "PathHeader", "VERBAU / PFADTECHNIK", new Vector2(left + headerWidth * 0.5f, topY), new Vector2(headerWidth, topPanelHeight), new Color32(47, 169, 210, 255));
        CreateArtIcon(header, "PathIcon", "icon_path", new Vector2(-headerWidth * 0.5f + 82f, -2f), new Vector2(78f, 78f));
        CreateDonutImage(header, "PathRing", new Vector2(-headerWidth * 0.5f + 175f, -2f), 82f, Percent(pathCurrentXP, pathRequiredXP), new Color32(55, 209, 235, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(header, "PathLevel", pathLevel.ToString(), new Vector2(-headerWidth * 0.5f + 175f, 5f), new Vector2(64f, 32f), 26f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(header, "PathXP", MetaHubMockData.FormatNumber(pathCurrentXP) + " / " + MetaHubMockData.FormatNumber(pathRequiredXP) + " XP", new Vector2(120f, 32f), new Vector2(235f, 20f), 12f, new Color32(244, 226, 186, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasBar(header, "PathBar", new Vector2(124f, 12f), new Vector2(230f, 6f), Percent(pathCurrentXP, pathRequiredXP), new Color32(55, 209, 235, 255));
        string pathInfo = "Blueprints " + (path != null ? path.blueprints.ToString() : "0") + " / Rissbauplaene " + (path != null ? path.riftBlueprints.ToString() : "0") +
            "\nSlots " + (path != null ? path.GetUsedLoadoutSlots() + " / " + path.GetLoadoutSlotCapacity() : "0 / 0");
        CreateCanvasLabel(header, "PathInfo", pathInfo, new Vector2(128f, -42f), new Vector2(260f, 36f), 10f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Left, FontStyles.Normal);

        CreateUnlockStatCard(main, "Blueprints", "BAUPLAENE", path != null ? MetaHubMockData.FormatNumber(path.blueprints) : GetMetricValue(data, "bauplaene", "0"), "Aktuell", "icon_blueprint", MetaHubTone.Blue, new Vector2(left + headerWidth + gap + statWidth * 0.5f, topY), new Vector2(statWidth, topPanelHeight));

        Transform summary = CreateUnlockPanel(main, "PathSummary", "PFAD-STATUS", new Vector2(summaryLeft + summaryWidth * 0.5f, topY), new Vector2(summaryWidth, topPanelHeight), new Color32(47, 169, 210, 255));
        float rowWidth = summaryWidth - 68f;
        CreateUnlockCompactRow(summary, "PathLast1", "icon_path", "Pfad-XP", "Letzter Run", path != null ? "+" + path.lastRunPathTechniqueXPGained : "+0", MetaHubTone.Cyan, 22f, rowWidth);
        CreateUnlockCompactRow(summary, "PathLast2", "icon_blueprint", "Bauplaene", "Letzter Run", path != null ? "+" + path.lastRunBlueprintsGained : "+0", MetaHubTone.Blue, -22f, rowWidth);
        CreateUnlockCompactRow(summary, "PathSlots", "icon_keystone", "Pfad-Loadout", "Aktive Technik", path != null ? path.GetUsedLoadoutSlots() + " / " + path.GetLoadoutSlotCapacity() : "0 / 0", MetaHubTone.Gold, -66f, rowWidth);

        Transform tree = CreateUnlockPanel(main, "PathFocusedTree", "PFADTECHNIK-BAUM", new Vector2((left + right) * 0.5f, -150f), new Vector2(right - left, 430f), ToneColor(GetPathTreeCategoryTone(selectedPathTreeCategory)));
        CreatePathTreeTabs(tree, path, right - left);
        CreatePathFocusedSkillTree(tree, path, right - left);
        return true;
    }

    private bool CreateEliteSkillTreeLayout(Transform main, MetaHubData data)
    {
        EliteHuntProgressionManager elite = GetEliteHuntManager();
        EnsureSelectedEliteTreeCategory(elite);

        float mainWidth = GetPanelWidth(main, 1307f);
        float left = -mainWidth * 0.5f + 22f;
        float right = mainWidth * 0.5f - 22f;
        float topY = 216f;
        float topPanelHeight = 180f;
        float gap = 16f;
        float statWidth = 235f;
        float summaryLeft = left + statWidth * 3f + gap * 3f;
        float summaryWidth = Mathf.Max(330f, right - summaryLeft);

        CreateUnlockStatCard(main, "EliteSeals", "ELITE-SIEGEL", elite != null ? MetaHubMockData.FormatNumber(elite.eliteSeals) : GetResourceValue(data, "special").ToString(), "Waehrung", "icon_skull", MetaHubTone.Red, new Vector2(left + statWidth * 0.5f, topY), new Vector2(statWidth, topPanelHeight));
        CreateUnlockStatCard(main, "EliteRank", "ELITE-RANG", elite != null ? elite.eliteRank.ToString() : "1", elite != null ? EliteHuntProgressionManager.GetHuntModeDisplayName(elite.activeHuntMode) : "Aus", "icon_risk_core", MetaHubTone.Red, new Vector2(left + statWidth + gap + statWidth * 0.5f, topY), new Vector2(statWidth, topPanelHeight));
        CreateUnlockStatCard(main, "EliteKills", "ELITE-KILLS", elite != null ? MetaHubMockData.FormatNumber(elite.totalEliteKillsEver) : GetMetricValue(data, "elite_jagd", "0"), "Gesamt", "icon_skull", MetaHubTone.Red, new Vector2(left + statWidth * 2f + gap * 2f + statWidth * 0.5f, topY), new Vector2(statWidth, topPanelHeight));

        Transform summary = CreateUnlockPanel(main, "EliteSummary", "JAGD-STATUS", new Vector2(summaryLeft + summaryWidth * 0.5f, topY), new Vector2(summaryWidth, topPanelHeight), new Color32(167, 54, 45, 255));
        int eliteCurrentXP = elite != null ? elite.GetXPIntoCurrentRank() : 0;
        int eliteRequiredXP = elite != null ? elite.GetXPToNextEliteRank() : 1;
        CreateCanvasLabel(summary, "RankXP", "Elite-Rang-XP: " + eliteCurrentXP + " / " + eliteRequiredXP, new Vector2(10f, 44f), new Vector2(summaryWidth - 72f, 20f), 11f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasBar(summary, "RankBar", new Vector2(10f, 24f), new Vector2(summaryWidth - 100f, 6f), Percent(eliteCurrentXP, eliteRequiredXP), new Color32(255, 84, 78, 255));
        float rowWidth = summaryWidth - 68f;
        CreateUnlockCompactRow(summary, "EliteLast1", "icon_skull", "Besiegt", "Gesamt / letzter Run", elite != null ? elite.totalEliteKillsEver + " / " + elite.lastRunEliteKills : "0 / 0", MetaHubTone.Red, -18f, rowWidth);
        CreateUnlockCompactRow(summary, "EliteLoadout", "icon_keystone", "Elite-Loadout", "Aktive Boni", elite != null ? elite.GetUsedLoadoutSlots() + " / " + elite.GetLoadoutSlotCapacity() : "0 / 0", MetaHubTone.Gold, -66f, rowWidth);

        Transform tree = CreateUnlockPanel(main, "EliteFocusedTree", "JAGD-BAUM", new Vector2((left + right) * 0.5f, -150f), new Vector2(right - left, 430f), ToneColor(GetEliteTreeCategoryTone(selectedEliteTreeCategory)));
        CreateEliteTreeTabs(tree, elite, right - left);
        CreateEliteFocusedSkillTree(tree, elite, right - left);
        return true;
    }

    private void EnsureSelectedChaosTreeCategory(ChaosResearchProgressionManager chaos)
    {
        List<ChaosResearchCategory> categories = GetChaosTreeCategories(chaos);
        if (categories.Count == 0)
        {
            selectedChaosTreeCategory = ChaosResearchCategory.RiskPool;
            return;
        }

        if (!categories.Contains(selectedChaosTreeCategory))
            selectedChaosTreeCategory = categories[0];
    }

    private void EnsureSelectedPathTreeCategory(PathTechniqueProgressionManager path)
    {
        List<PathTechniqueCategory> categories = GetPathTreeCategories(path);
        if (categories.Count == 0)
        {
            selectedPathTreeCategory = PathTechniqueCategory.EventPool;
            return;
        }

        if (!categories.Contains(selectedPathTreeCategory))
            selectedPathTreeCategory = categories[0];
    }

    private void EnsureSelectedEliteTreeCategory(EliteHuntProgressionManager elite)
    {
        List<EliteHuntCategory> categories = GetEliteTreeCategories(elite);
        if (categories.Count == 0)
        {
            selectedEliteTreeCategory = EliteHuntCategory.Contracts;
            return;
        }

        if (!categories.Contains(selectedEliteTreeCategory))
            selectedEliteTreeCategory = categories[0];
    }

    private List<ChaosResearchCategory> GetChaosTreeCategories(ChaosResearchProgressionManager chaos)
    {
        List<ChaosResearchCategory> categories = new List<ChaosResearchCategory>();
        AddChaosTreeCategory(categories, chaos, ChaosResearchCategory.RiskPool);
        AddChaosTreeCategory(categories, chaos, ChaosResearchCategory.ChaosVariants);
        AddChaosTreeCategory(categories, chaos, ChaosResearchCategory.ChaosCounters);
        AddChaosTreeCategory(categories, chaos, ChaosResearchCategory.ChaosWaves);
        AddChaosTreeCategory(categories, chaos, ChaosResearchCategory.OfferControl);
        AddChaosTreeCategory(categories, chaos, ChaosResearchCategory.Chaos5Endgame);
        AddChaosTreeCategory(categories, chaos, ChaosResearchCategory.JusticeOrder);
        return categories;
    }

    private void AddChaosTreeCategory(List<ChaosResearchCategory> categories, ChaosResearchProgressionManager chaos, ChaosResearchCategory category)
    {
        if (runtimeUnlockInfoMode)
        {
            if (CountVisibleChaosNodes(chaos, category) > 0)
                categories.Add(category);
            return;
        }

        if (chaos == null || chaos.GetDefinitionCount(category) > 0)
            categories.Add(category);
    }

    private List<PathTechniqueCategory> GetPathTreeCategories(PathTechniqueProgressionManager path)
    {
        List<PathTechniqueCategory> categories = new List<PathTechniqueCategory>();
        AddPathTreeCategory(categories, path, PathTechniqueCategory.EventPool);
        AddPathTreeCategory(categories, path, PathTechniqueCategory.EventQuality);
        AddPathTreeCategory(categories, path, PathTechniqueCategory.RescuePower);
        AddPathTreeCategory(categories, path, PathTechniqueCategory.PathTools);
        AddPathTreeCategory(categories, path, PathTechniqueCategory.TileTechnique);
        AddPathTreeCategory(categories, path, PathTechniqueCategory.RiftArchitecture);
        return categories;
    }

    private void AddPathTreeCategory(List<PathTechniqueCategory> categories, PathTechniqueProgressionManager path, PathTechniqueCategory category)
    {
        if (runtimeUnlockInfoMode)
        {
            if (CountVisiblePathNodes(path, category) > 0)
                categories.Add(category);
            return;
        }

        if (path == null || path.GetDefinitionCount(category) > 0)
            categories.Add(category);
    }

    private List<EliteHuntCategory> GetEliteTreeCategories(EliteHuntProgressionManager elite)
    {
        List<EliteHuntCategory> categories = new List<EliteHuntCategory>();
        AddEliteTreeCategory(categories, elite, EliteHuntCategory.Contracts);
        AddEliteTreeCategory(categories, elite, EliteHuntCategory.Affixes);
        AddEliteTreeCategory(categories, elite, EliteHuntCategory.Rewards);
        AddEliteTreeCategory(categories, elite, EliteHuntCategory.Frequency);
        AddEliteTreeCategory(categories, elite, EliteHuntCategory.Counters);
        AddEliteTreeCategory(categories, elite, EliteHuntCategory.RiftElite);
        return categories;
    }

    private void AddEliteTreeCategory(List<EliteHuntCategory> categories, EliteHuntProgressionManager elite, EliteHuntCategory category)
    {
        if (runtimeUnlockInfoMode)
        {
            if (CountVisibleEliteNodes(elite, category) > 0)
                categories.Add(category);
            return;
        }

        if (elite == null || elite.GetDefinitionCount(category) > 0)
            categories.Add(category);
    }

    private int CountVisibleChaosNodes(ChaosResearchProgressionManager chaos, ChaosResearchCategory category)
    {
        if (chaos == null)
            return 0;

        int count = 0;
        List<ChaosResearchNodeDefinition> definitions = chaos.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            ChaosResearchNodeDefinition definition = definitions[i];
            if (definition != null && definition.category == category && IsChaosNodeVisibleInRuntime(chaos, definition.nodeId))
                count++;
        }

        return count;
    }

    private int CountVisiblePathNodes(PathTechniqueProgressionManager path, PathTechniqueCategory category)
    {
        if (path == null)
            return 0;

        int count = 0;
        List<PathTechniqueNodeDefinition> definitions = path.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            PathTechniqueNodeDefinition definition = definitions[i];
            if (definition != null && definition.category == category && IsPathNodeVisibleInRuntime(path, definition.nodeId))
                count++;
        }

        return count;
    }

    private int CountVisibleEliteNodes(EliteHuntProgressionManager elite, EliteHuntCategory category)
    {
        if (elite == null)
            return 0;

        int count = 0;
        List<EliteHuntNodeDefinition> definitions = elite.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            EliteHuntNodeDefinition definition = definitions[i];
            if (definition != null && definition.category == category && IsEliteNodeVisibleInRuntime(elite, definition.nodeId))
                count++;
        }

        return count;
    }

    private void CreateChaosTreeTabs(Transform parent, ChaosResearchProgressionManager chaos, float panelWidth)
    {
        List<ChaosResearchCategory> categories = GetChaosTreeCategories(chaos);
        CreateChaosTreeTabs(parent, categories, panelWidth);
    }

    private void CreateChaosTreeTabs(Transform parent, List<ChaosResearchCategory> categories, float panelWidth)
    {
        int count = Mathf.Max(1, categories.Count);
        float gap = 12f;
        float tabWidth = Mathf.Min(162f, (panelWidth - 74f - (count - 1) * gap) / count);
        float gridWidth = count * tabWidth + (count - 1) * gap;
        float startX = -gridWidth * 0.5f + tabWidth * 0.5f;
        for (int i = 0; i < categories.Count; i++)
        {
            ChaosResearchCategory category = categories[i];
            CreateChaosTreeTab(parent, category, GetChaosTreeCategoryLabel(category), GetChaosTreeCategoryTone(category), new Vector2(startX + i * (tabWidth + gap), 150f), new Vector2(tabWidth, 34f));
        }
    }

    private void CreatePathTreeTabs(Transform parent, PathTechniqueProgressionManager path, float panelWidth)
    {
        List<PathTechniqueCategory> categories = GetPathTreeCategories(path);
        int count = Mathf.Max(1, categories.Count);
        float gap = 14f;
        float tabWidth = Mathf.Min(174f, (panelWidth - 74f - (count - 1) * gap) / count);
        float gridWidth = count * tabWidth + (count - 1) * gap;
        float startX = -gridWidth * 0.5f + tabWidth * 0.5f;
        for (int i = 0; i < categories.Count; i++)
        {
            PathTechniqueCategory category = categories[i];
            CreatePathTreeTab(parent, category, GetPathTreeCategoryLabel(category), GetPathTreeCategoryTone(category), new Vector2(startX + i * (tabWidth + gap), 150f), new Vector2(tabWidth, 34f));
        }
    }

    private void CreateEliteTreeTabs(Transform parent, EliteHuntProgressionManager elite, float panelWidth)
    {
        List<EliteHuntCategory> categories = GetEliteTreeCategories(elite);
        int count = Mathf.Max(1, categories.Count);
        float gap = 14f;
        float tabWidth = Mathf.Min(174f, (panelWidth - 74f - (count - 1) * gap) / count);
        float gridWidth = count * tabWidth + (count - 1) * gap;
        float startX = -gridWidth * 0.5f + tabWidth * 0.5f;
        for (int i = 0; i < categories.Count; i++)
        {
            EliteHuntCategory category = categories[i];
            CreateEliteTreeTab(parent, category, GetEliteTreeCategoryLabel(category), GetEliteTreeCategoryTone(category), new Vector2(startX + i * (tabWidth + gap), 150f), new Vector2(tabWidth, 34f));
        }
    }

    private void CreateChaosTreeTab(Transform parent, ChaosResearchCategory category, string label, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        bool selected = selectedChaosTreeCategory == category;
        Color32 toneColor = ToneColor(tone);
        Color32 fill = selected ? new Color32(54, 39, 13, 248) : new Color32(5, 18, 18, 238);
        Transform tab = CreateCanvasPanel(parent, "ChaosTreeTab_" + category, position, size, fill, selected ? toneColor : new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateCanvasLabel(tab, "Label", Shorten(label, 16), Vector2.zero, size, 11.2f, selected ? new Color32(255, 236, 186, 255) : new Color32(224, 214, 194, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        ChaosResearchCategory capturedCategory = category;
        MakeCanvasClickable(tab, delegate { SelectChaosTreeCategory(capturedCategory); }, fill, toneColor);
    }

    private void CreatePathTreeTab(Transform parent, PathTechniqueCategory category, string label, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        bool selected = selectedPathTreeCategory == category;
        Color32 toneColor = ToneColor(tone);
        Color32 fill = selected ? new Color32(54, 39, 13, 248) : new Color32(5, 18, 18, 238);
        Transform tab = CreateCanvasPanel(parent, "PathTreeTab_" + category, position, size, fill, selected ? toneColor : new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateCanvasLabel(tab, "Label", Shorten(label, 16), Vector2.zero, size, 11.2f, selected ? new Color32(255, 236, 186, 255) : new Color32(224, 214, 194, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        PathTechniqueCategory capturedCategory = category;
        MakeCanvasClickable(tab, delegate { SelectPathTreeCategory(capturedCategory); }, fill, toneColor);
    }

    private void CreateEliteTreeTab(Transform parent, EliteHuntCategory category, string label, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        bool selected = selectedEliteTreeCategory == category;
        Color32 toneColor = ToneColor(tone);
        Color32 fill = selected ? new Color32(54, 39, 13, 248) : new Color32(5, 18, 18, 238);
        Transform tab = CreateCanvasPanel(parent, "EliteTreeTab_" + category, position, size, fill, selected ? toneColor : new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateCanvasLabel(tab, "Label", Shorten(label, 16), Vector2.zero, size, 11.2f, selected ? new Color32(255, 236, 186, 255) : new Color32(224, 214, 194, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        EliteHuntCategory capturedCategory = category;
        MakeCanvasClickable(tab, delegate { SelectEliteTreeCategory(capturedCategory); }, fill, toneColor);
    }

    private void CreateChaosFocusedSkillTree(Transform parent, ChaosResearchProgressionManager chaos, float panelWidth)
    {
        ChaosResearchCategory category = selectedChaosTreeCategory;
        List<ChaosResearchNodeDefinition> nodes = GetChaosTreeNodes(chaos, category, 15);
        int total = runtimeUnlockInfoMode ? nodes.Count : chaos != null ? chaos.GetDefinitionCount(category) : nodes.Count;
        int purchased = runtimeUnlockInfoMode ? nodes.Count : chaos != null ? chaos.GetPurchasedCount(category) : 0;
        MetaHubTone tone = GetChaosTreeCategoryTone(category);
        CreateFocusedTreeHeader(parent, "ChaosFocused", GetChaosTreeCategoryTitle(category), purchased, total, panelWidth, tone);

        if (nodes.Count == 0)
        {
            string message = runtimeUnlockInfoMode ? "Im aktuellen Run ist hier noch nichts freigeschaltet." : "Keine Forschung in dieser Gruppe.";
            CreateCanvasLabel(parent, "ChaosNoNodes", message, new Vector2(0f, -10f), new Vector2(560f, 24f), 12f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);
            return;
        }

        CreateFocusedNodeGrid(parent, nodes.Count, delegate(int i, Vector2 position, Vector2 size)
        {
            ChaosResearchNodeDefinition definition = nodes[i];
            if (definition == null)
                return;

            string nodeId = definition.nodeId;
            string effect = string.IsNullOrEmpty(definition.effectText) ? definition.requirementText : definition.effectText;
            string capturedNodeId = nodeId;
            string costLabel = runtimeUnlockInfoMode ? "" : ChaosNodeCost(nodeId);
            CreateReadableProgressionNode(parent, "ChaosFocusedNode_" + i, definition.displayName, effect, costLabel, ChaosNodeStateLabel(nodeId), GetChaosCategoryArtName(category), ChaosNodeTone(nodeId, tone), position, size, delegate { HandleChaosNodeClick(capturedNodeId); });
        });
    }

    private void CreatePathFocusedSkillTree(Transform parent, PathTechniqueProgressionManager path, float panelWidth)
    {
        PathTechniqueCategory category = selectedPathTreeCategory;
        List<PathTechniqueNodeDefinition> nodes = GetPathTreeNodes(path, category, 15);
        int total = runtimeUnlockInfoMode ? nodes.Count : path != null ? path.GetDefinitionCount(category) : nodes.Count;
        int purchased = runtimeUnlockInfoMode ? nodes.Count : path != null ? path.GetPurchasedCount(category) : 0;
        MetaHubTone tone = GetPathTreeCategoryTone(category);
        CreateFocusedTreeHeader(parent, "PathFocused", GetPathTreeCategoryTitle(category), purchased, total, panelWidth, tone);

        if (nodes.Count == 0)
        {
            string message = runtimeUnlockInfoMode ? "Im aktuellen Run ist hier noch nichts freigeschaltet." : "Keine Pfadtechnik in dieser Gruppe.";
            CreateCanvasLabel(parent, "PathNoNodes", message, new Vector2(0f, -10f), new Vector2(560f, 24f), 12f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);
            return;
        }

        CreateFocusedNodeGrid(parent, nodes.Count, delegate(int i, Vector2 position, Vector2 size)
        {
            PathTechniqueNodeDefinition definition = nodes[i];
            if (definition == null)
                return;

            string nodeId = definition.nodeId;
            string effect = string.IsNullOrEmpty(definition.effectText) ? definition.requirementText : definition.effectText;
            string capturedNodeId = nodeId;
            string costLabel = runtimeUnlockInfoMode ? "" : PathNodeCost(nodeId);
            CreateReadableProgressionNode(parent, "PathFocusedNode_" + i, definition.displayName, effect, costLabel, PathNodeStateLabel(nodeId), GetPathCategoryArtName(category), PathNodeTone(nodeId, tone), position, size, delegate { HandlePathNodeClick(capturedNodeId); });
        });
    }

    private void CreateEliteFocusedSkillTree(Transform parent, EliteHuntProgressionManager elite, float panelWidth)
    {
        EliteHuntCategory category = selectedEliteTreeCategory;
        List<EliteHuntNodeDefinition> nodes = GetEliteTreeNodes(elite, category, 15);
        int total = runtimeUnlockInfoMode ? nodes.Count : elite != null ? elite.GetDefinitionCount(category) : nodes.Count;
        int purchased = runtimeUnlockInfoMode ? nodes.Count : elite != null ? elite.GetPurchasedCount(category) : 0;
        MetaHubTone tone = GetEliteTreeCategoryTone(category);
        CreateFocusedTreeHeader(parent, "EliteFocused", GetEliteTreeCategoryTitle(category), purchased, total, panelWidth, tone);

        if (nodes.Count == 0)
        {
            string message = runtimeUnlockInfoMode ? "Im aktuellen Run ist hier noch nichts freigeschaltet." : "Keine Jagd-Freischaltungen in dieser Gruppe.";
            CreateCanvasLabel(parent, "EliteNoNodes", message, new Vector2(0f, -10f), new Vector2(560f, 24f), 12f, new Color32(224, 214, 196, 255), TextAlignmentOptions.Center, FontStyles.Normal);
            return;
        }

        CreateFocusedNodeGrid(parent, nodes.Count, delegate(int i, Vector2 position, Vector2 size)
        {
            EliteHuntNodeDefinition definition = nodes[i];
            if (definition == null)
                return;

            string nodeId = definition.nodeId;
            string effect = string.IsNullOrEmpty(definition.effectText) ? definition.requirementText : definition.effectText;
            string capturedNodeId = nodeId;
            string costLabel = runtimeUnlockInfoMode ? "" : EliteNodeCost(nodeId);
            CreateReadableProgressionNode(parent, "EliteFocusedNode_" + i, definition.displayName, effect, costLabel, EliteNodeStateLabel(nodeId), GetEliteCategoryArtName(category), EliteNodeTone(nodeId, tone), position, size, delegate { HandleEliteNodeClick(capturedNodeId); });
        });
    }

    private void CreateFocusedTreeHeader(Transform parent, string prefix, string title, int purchased, int total, float panelWidth, MetaHubTone tone)
    {
        Color32 toneColor = ToneColor(tone);
        float left = -panelWidth * 0.5f + 58f;
        CreateCanvasLabel(parent, prefix + "Title", title, new Vector2(left + 130f, 110f), new Vector2(360f, 24f), 14f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(parent, prefix + "Count", GetUnlockCountText(purchased, total), new Vector2(panelWidth * 0.5f - 82f, 110f), new Vector2(100f, 20f), 10f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
        CreateLine(parent, prefix + "Line", new Vector2(0f, 84f), new Vector2(panelWidth - 150f, 2f), new Color32(toneColor.r, toneColor.g, toneColor.b, 130));
    }

    private string GetUnlockCountText(int unlocked, int total)
    {
        if (runtimeUnlockInfoMode)
            return Mathf.Max(0, unlocked).ToString();

        return unlocked + " / " + Mathf.Max(total, unlocked);
    }

    private delegate void FocusedNodeRenderer(int index, Vector2 position, Vector2 size);

    private void CreateFocusedNodeGrid(Transform parent, int nodeCount, FocusedNodeRenderer renderer)
    {
        int columns = 5;
        int maxVisible = Mathf.Min(nodeCount, 15);
        Vector2 cardSize = new Vector2(196f, 82f);
        float gapX = 24f;
        float gapY = 16f;
        float gridWidth = columns * cardSize.x + (columns - 1) * gapX;
        float startX = -gridWidth * 0.5f + cardSize.x * 0.5f;
        float startY = 34f;

        for (int i = 0; i < maxVisible; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Vector2 position = new Vector2(startX + column * (cardSize.x + gapX), startY - row * (cardSize.y + gapY));
            if (renderer != null)
                renderer(i, position, cardSize);
        }
    }

    private void CreateReadableProgressionNode(Transform parent, string name, string title, string effect, string cost, string state, string artName, MetaHubTone tone, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Color32 toneColor = ToneColor(tone);
        string stateUpper = string.IsNullOrEmpty(state) ? "" : state.ToUpperInvariant();
        bool active = stateUpper.Contains("AKTIV");
        bool owned = active || stateUpper.Contains("FREI") || stateUpper.Contains("ERFORSCHT");
        Color32 fill = active ? new Color32(8, 35, 18, 235) : owned ? new Color32(7, 26, 22, 235) : new Color32(6, 18, 18, 235);
        Transform node = CreateCanvasPanel(parent, name, position, size, fill, toneColor);
        CreateArtIcon(node, "Icon", artName, new Vector2(-size.x * 0.5f + 30f, 18f), new Vector2(34f, 34f));
        CreateCanvasLabel(node, "Title", Shorten(title, 22), new Vector2(31f, 20f), new Vector2(size.x - 68f, 18f), 8.8f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI effectLabel = CreateCanvasLabel(node, "Effect", ToTwoLineText(effect, 28, 68), new Vector2(31f, -5f), new Vector2(size.x - 68f, 30f), 8.8f, new Color32(188, 178, 155, 255), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        effectLabel.textWrappingMode = TextWrappingModes.Normal;
        effectLabel.enableWordWrapping = true;
        ConfigureCardDescriptionText(effectLabel, 6.9f, 8.8f);
        CreateCanvasLabel(node, "Cost", Shorten(cost, 15), new Vector2(-size.x * 0.25f, -31f), new Vector2(size.x * 0.44f, 14f), 7.1f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(node, "State", Shorten(state, 12), new Vector2(size.x * 0.25f, -31f), new Vector2(size.x * 0.44f, 14f), 7.1f, toneColor, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeCanvasClickable(node, onClick, fill, toneColor);
    }

    private List<ChaosResearchNodeDefinition> GetChaosTreeNodes(ChaosResearchProgressionManager chaos, ChaosResearchCategory category, int maxNodes)
    {
        List<ChaosResearchNodeDefinition> nodes = new List<ChaosResearchNodeDefinition>();
        if (chaos == null)
            return nodes;

        List<ChaosResearchNodeDefinition> definitions = chaos.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            ChaosResearchNodeDefinition definition = definitions[i];
            if (definition != null && definition.category == category && (!runtimeUnlockInfoMode || IsChaosNodeVisibleInRuntime(chaos, definition.nodeId)))
                nodes.Add(definition);
        }

        return CreateNodeWindow(nodes, maxNodes, delegate(ChaosResearchNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return chaos.IsNodePurchased(nodeId); });
    }

    private bool IsChaosNodeVisibleInRuntime(ChaosResearchProgressionManager chaos, string nodeId)
    {
        return chaos != null && !string.IsNullOrEmpty(nodeId) && (chaos.IsNodeActive(nodeId) || chaos.IsNodePurchased(nodeId));
    }

    private List<PathTechniqueNodeDefinition> GetPathTreeNodes(PathTechniqueProgressionManager path, PathTechniqueCategory category, int maxNodes)
    {
        List<PathTechniqueNodeDefinition> nodes = new List<PathTechniqueNodeDefinition>();
        if (path == null)
            return nodes;

        List<PathTechniqueNodeDefinition> definitions = path.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            PathTechniqueNodeDefinition definition = definitions[i];
            if (definition != null && definition.category == category && (!runtimeUnlockInfoMode || IsPathNodeVisibleInRuntime(path, definition.nodeId)))
                nodes.Add(definition);
        }

        return CreateNodeWindow(nodes, maxNodes, delegate(PathTechniqueNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return path.IsNodePurchased(nodeId); });
    }

    private bool IsPathNodeVisibleInRuntime(PathTechniqueProgressionManager path, string nodeId)
    {
        return path != null && !string.IsNullOrEmpty(nodeId) && (path.IsNodeActive(nodeId) || path.IsNodePurchased(nodeId));
    }

    private List<EliteHuntNodeDefinition> GetEliteTreeNodes(EliteHuntProgressionManager elite, EliteHuntCategory category, int maxNodes)
    {
        List<EliteHuntNodeDefinition> nodes = new List<EliteHuntNodeDefinition>();
        if (elite == null)
            return nodes;

        List<EliteHuntNodeDefinition> definitions = elite.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            EliteHuntNodeDefinition definition = definitions[i];
            if (definition != null && definition.category == category && (!runtimeUnlockInfoMode || IsEliteNodeVisibleInRuntime(elite, definition.nodeId)))
                nodes.Add(definition);
        }

        return CreateNodeWindow(nodes, maxNodes, delegate(EliteHuntNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return elite.IsNodePurchased(nodeId); });
    }

    private bool IsEliteNodeVisibleInRuntime(EliteHuntProgressionManager elite, string nodeId)
    {
        return elite != null && !string.IsNullOrEmpty(nodeId) && (elite.IsNodeActive(nodeId) || elite.IsNodePurchased(nodeId));
    }

    private string GetChaosTreeCategoryLabel(ChaosResearchCategory category)
    {
        switch (category)
        {
            case ChaosResearchCategory.RiskPool: return "RISIKEN";
            case ChaosResearchCategory.ChaosVariants: return "VARIANTEN";
            case ChaosResearchCategory.ChaosCounters: return "KONTER";
            case ChaosResearchCategory.ChaosWaves: return "WAVES";
            case ChaosResearchCategory.OfferControl: return "ANGEBOTE";
            case ChaosResearchCategory.Chaos5Endgame: return "ENDGAME";
            case ChaosResearchCategory.JusticeOrder: return "ORDNUNG";
            default: return "CHAOS";
        }
    }

    private string GetPathTreeCategoryLabel(PathTechniqueCategory category)
    {
        switch (category)
        {
            case PathTechniqueCategory.EventPool: return "EVENTS";
            case PathTechniqueCategory.EventQuality: return "QUALITAET";
            case PathTechniqueCategory.RescuePower: return "RETTUNG";
            case PathTechniqueCategory.PathTools: return "WERKZEUGE";
            case PathTechniqueCategory.TileTechnique: return "TILES";
            case PathTechniqueCategory.RiftArchitecture: return "RISS";
            default: return "PFAD";
        }
    }

    private string GetEliteTreeCategoryLabel(EliteHuntCategory category)
    {
        switch (category)
        {
            case EliteHuntCategory.Contracts: return "AUFTRAEGE";
            case EliteHuntCategory.Affixes: return "AFFIXE";
            case EliteHuntCategory.Rewards: return "BELOHNUNG";
            case EliteHuntCategory.Frequency: return "JAGDMODUS";
            case EliteHuntCategory.Counters: return "KONTER";
            case EliteHuntCategory.RiftElite: return "RISS-ELITE";
            default: return "JAGD";
        }
    }

    private string GetChaosTreeCategoryTitle(ChaosResearchCategory category)
    {
        return GetChaosTreeCategoryLabel(category) + "-FORSCHUNG";
    }

    private string GetPathTreeCategoryTitle(PathTechniqueCategory category)
    {
        return GetPathTreeCategoryLabel(category) + "-TECHNIK";
    }

    private string GetEliteTreeCategoryTitle(EliteHuntCategory category)
    {
        return GetEliteTreeCategoryLabel(category);
    }

    private MetaHubTone GetChaosTreeCategoryTone(ChaosResearchCategory category)
    {
        switch (category)
        {
            case ChaosResearchCategory.RiskPool:
            case ChaosResearchCategory.ChaosVariants:
            case ChaosResearchCategory.ChaosWaves:
            case ChaosResearchCategory.Chaos5Endgame:
                return MetaHubTone.Red;
            case ChaosResearchCategory.ChaosCounters:
                return MetaHubTone.Cyan;
            case ChaosResearchCategory.OfferControl:
            case ChaosResearchCategory.JusticeOrder:
                return MetaHubTone.Gold;
            default:
                return MetaHubTone.Purple;
        }
    }

    private MetaHubTone GetPathTreeCategoryTone(PathTechniqueCategory category)
    {
        switch (category)
        {
            case PathTechniqueCategory.EventQuality:
            case PathTechniqueCategory.RiftArchitecture:
                return MetaHubTone.Purple;
            case PathTechniqueCategory.RescuePower:
                return MetaHubTone.Gold;
            case PathTechniqueCategory.TileTechnique:
                return MetaHubTone.Blue;
            default:
                return MetaHubTone.Cyan;
        }
    }

    private MetaHubTone GetEliteTreeCategoryTone(EliteHuntCategory category)
    {
        switch (category)
        {
            case EliteHuntCategory.Affixes:
                return MetaHubTone.Purple;
            case EliteHuntCategory.Rewards:
                return MetaHubTone.Gold;
            case EliteHuntCategory.Counters:
                return MetaHubTone.Cyan;
            default:
                return MetaHubTone.Red;
        }
    }

    private void CreateGeneralSkillBranch(Transform parent, string name, string title, GeneralMetaCategory[] categories, float y, MetaHubTone tone, int maxNodes, params string[] preferredNodeIds)
    {
        List<GeneralMetaNodeDefinition> nodes = GetGeneralBranchNodes(categories, maxNodes, preferredNodeIds);
        int total = CountGeneralBranchNodes(categories);
        int purchased = CountPurchasedGeneralBranchNodes(categories);
        const float branchWidth = 840f;
        const float nodeAreaWidth = 660f;
        const float nodeAreaOffsetX = 90f;
        CreateSkillBranchHeader(parent, name + "Header", title, purchased, total, y, tone, branchWidth);
        CreateSkillConnector(parent, name + "Connector", y, nodes.Count, nodeAreaWidth, tone, nodeAreaOffsetX);

        for (int i = 0; i < nodes.Count; i++)
        {
            GeneralMetaNodeDefinition definition = nodes[i];
            string nodeId = definition.nodeId;
            MetaHubTone nodeTone = GeneralNodeTone(nodeId, GetGeneralNodeDisplayTone(nodeId, tone));
            string artName = GetGeneralNodeDisplayArt(nodeId, GetCategoryArtName(definition.category));
            string effect = string.IsNullOrEmpty(definition.effectText) ? definition.requirementText : definition.effectText;
            Vector2 position = new Vector2(nodeAreaOffsetX + GetSkillNodeX(i, nodes.Count, nodeAreaWidth), y - 34f);
            string capturedNodeId = nodeId;
            string costLabel = runtimeUnlockInfoMode ? "" : GeneralNodeCost(nodeId) + " KW";
            CreateSkillTreeNode(parent, name + "_Node_" + i, GeneralNodeTitle(nodeId, definition.displayName), effect, costLabel, GeneralNodeStateLabel(nodeId), artName, nodeTone, position, new Vector2(126f, 72f), delegate { HandleGeneralNodeClick(capturedNodeId); });
        }
    }

    private void CreateChaosSkillBranch(Transform parent, string name, string title, ChaosResearchCategory category, float y, MetaHubTone tone, int maxNodes)
    {
        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        List<ChaosResearchNodeDefinition> nodes = new List<ChaosResearchNodeDefinition>();
        if (chaos != null)
        {
            List<ChaosResearchNodeDefinition> definitions = chaos.GetDefinitions();
            for (int i = 0; i < definitions.Count; i++)
            {
                ChaosResearchNodeDefinition definition = definitions[i];
                if (definition != null && definition.category == category)
                    nodes.Add(definition);
            }
        }

        const float branchWidth = 650f;
        const float nodeAreaWidth = 500f;
        const float nodeAreaOffsetX = 105f;
        int total = chaos != null ? chaos.GetDefinitionCount(category) : nodes.Count;
        int purchased = chaos != null ? chaos.GetPurchasedCount(category) : 0;
        nodes = CreateNodeWindow(nodes, maxNodes, delegate(ChaosResearchNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return chaos != null && chaos.IsNodePurchased(nodeId); });
        CreateSkillBranchHeader(parent, name + "Header", title, purchased, total, y, tone, branchWidth);
        CreateSkillConnector(parent, name + "Connector", y, nodes.Count, nodeAreaWidth, tone, nodeAreaOffsetX);
        for (int i = 0; i < nodes.Count; i++)
        {
            ChaosResearchNodeDefinition definition = nodes[i];
            string nodeId = definition.nodeId;
            Vector2 position = new Vector2(nodeAreaOffsetX + GetSkillNodeX(i, nodes.Count, nodeAreaWidth), y - 34f);
            string capturedNodeId = nodeId;
            CreateSkillTreeNode(parent, name + "_Node_" + i, ChaosNodeTitle(nodeId, definition.displayName), definition.effectText, ChaosNodeCost(nodeId), ChaosNodeStateLabel(nodeId), GetChaosCategoryArtName(category), ChaosNodeTone(nodeId, tone), position, new Vector2(126f, 74f), delegate { HandleChaosNodeClick(capturedNodeId); });
        }
    }

    private void CreatePathSkillBranch(Transform parent, string name, string title, PathTechniqueCategory category, float y, MetaHubTone tone, int maxNodes)
    {
        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        List<PathTechniqueNodeDefinition> nodes = new List<PathTechniqueNodeDefinition>();
        if (path != null)
        {
            List<PathTechniqueNodeDefinition> definitions = path.GetDefinitions();
            for (int i = 0; i < definitions.Count; i++)
            {
                PathTechniqueNodeDefinition definition = definitions[i];
                if (definition != null && definition.category == category)
                    nodes.Add(definition);
            }
        }

        const float branchWidth = 650f;
        const float nodeAreaWidth = 500f;
        const float nodeAreaOffsetX = 105f;
        int total = path != null ? path.GetDefinitionCount(category) : nodes.Count;
        int purchased = path != null ? path.GetPurchasedCount(category) : 0;
        nodes = CreateNodeWindow(nodes, maxNodes, delegate(PathTechniqueNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return path != null && path.IsNodePurchased(nodeId); });
        CreateSkillBranchHeader(parent, name + "Header", title, purchased, total, y, tone, branchWidth);
        CreateSkillConnector(parent, name + "Connector", y, nodes.Count, nodeAreaWidth, tone, nodeAreaOffsetX);
        for (int i = 0; i < nodes.Count; i++)
        {
            PathTechniqueNodeDefinition definition = nodes[i];
            string nodeId = definition.nodeId;
            Vector2 position = new Vector2(nodeAreaOffsetX + GetSkillNodeX(i, nodes.Count, nodeAreaWidth), y - 34f);
            string capturedNodeId = nodeId;
            CreateSkillTreeNode(parent, name + "_Node_" + i, PathNodeTitle(nodeId, definition.displayName), definition.effectText, PathNodeCost(nodeId), PathNodeStateLabel(nodeId), GetPathCategoryArtName(category), PathNodeTone(nodeId, tone), position, new Vector2(126f, 74f), delegate { HandlePathNodeClick(capturedNodeId); });
        }
    }

    private void CreateEliteSkillBranch(Transform parent, string name, string title, EliteHuntCategory category, float y, MetaHubTone tone, int maxNodes)
    {
        EliteHuntProgressionManager elite = GetEliteHuntManager();
        List<EliteHuntNodeDefinition> nodes = new List<EliteHuntNodeDefinition>();
        if (elite != null)
        {
            List<EliteHuntNodeDefinition> definitions = elite.GetDefinitions();
            for (int i = 0; i < definitions.Count; i++)
            {
                EliteHuntNodeDefinition definition = definitions[i];
                if (definition != null && definition.category == category)
                    nodes.Add(definition);
            }
        }

        const float branchWidth = 650f;
        const float nodeAreaWidth = 500f;
        const float nodeAreaOffsetX = 105f;
        int total = elite != null ? elite.GetDefinitionCount(category) : nodes.Count;
        int purchased = elite != null ? elite.GetPurchasedCount(category) : 0;
        nodes = CreateNodeWindow(nodes, maxNodes, delegate(EliteHuntNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return elite != null && elite.IsNodePurchased(nodeId); });
        CreateSkillBranchHeader(parent, name + "Header", title, purchased, total, y, tone, branchWidth);
        CreateSkillConnector(parent, name + "Connector", y, nodes.Count, nodeAreaWidth, tone, nodeAreaOffsetX);
        for (int i = 0; i < nodes.Count; i++)
        {
            EliteHuntNodeDefinition definition = nodes[i];
            string nodeId = definition.nodeId;
            Vector2 position = new Vector2(nodeAreaOffsetX + GetSkillNodeX(i, nodes.Count, nodeAreaWidth), y - 34f);
            string capturedNodeId = nodeId;
            CreateSkillTreeNode(parent, name + "_Node_" + i, EliteNodeTitle(nodeId, definition.displayName), definition.effectText, EliteNodeCost(nodeId), EliteNodeStateLabel(nodeId), GetEliteCategoryArtName(category), EliteNodeTone(nodeId, tone), position, new Vector2(126f, 74f), delegate { HandleEliteNodeClick(capturedNodeId); });
        }
    }

    private List<GeneralMetaNodeDefinition> GetGeneralBranchNodes(GeneralMetaCategory[] categories, int maxNodes, string[] preferredNodeIds)
    {
        List<GeneralMetaNodeDefinition> ordered = new List<GeneralMetaNodeDefinition>();
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general == null)
            return ordered;

        if (preferredNodeIds != null)
        {
            for (int i = 0; i < preferredNodeIds.Length; i++)
            {
                GeneralMetaNodeDefinition definition = general.GetDefinition(preferredNodeIds[i]);
                if (definition != null && GeneralCategoryMatches(definition.category, categories) && !ContainsGeneralNode(ordered, definition.nodeId))
                    ordered.Add(definition);
            }
        }

        List<GeneralMetaNodeDefinition> definitions = general.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            GeneralMetaNodeDefinition definition = definitions[i];
            if (definition != null && GeneralCategoryMatches(definition.category, categories) && !ContainsGeneralNode(ordered, definition.nodeId))
                ordered.Add(definition);
        }

        if (runtimeUnlockInfoMode)
            ordered = FilterUnlockedGeneralNodes(ordered, general);
        else if (IsGeneralStartBranch(categories))
            ordered = FilterProgressiveStartGeneralNodes(ordered, general);

        return CreateNodeWindow(ordered, maxNodes, delegate(GeneralMetaNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return general.IsNodePurchased(nodeId); });
    }

    private bool IsGeneralStartBranch(GeneralMetaCategory[] categories)
    {
        if (categories == null || categories.Length != 2)
            return false;

        bool hasStart = false;
        bool hasLoadout = false;
        for (int i = 0; i < categories.Length; i++)
        {
            if (categories[i] == GeneralMetaCategory.StartOption)
                hasStart = true;
            else if (categories[i] == GeneralMetaCategory.MetaLoadout)
                hasLoadout = true;
        }

        return hasStart && hasLoadout;
    }

    private List<GeneralMetaNodeDefinition> FilterProgressiveStartGeneralNodes(List<GeneralMetaNodeDefinition> nodes, GeneralMetaProgressionManager general)
    {
        List<GeneralMetaNodeDefinition> result = new List<GeneralMetaNodeDefinition>();
        if (nodes == null || general == null)
            return result;

        AddNextGeneralChainNode(nodes, result, general, "general.start.gold_1", "general.start.gold_2", "general.start.gold_3", "general.start.gold_4", "general.start.gold_5");
        AddNextGeneralChainNode(nodes, result, general, "general.start.life_1", "general.start.life_2", "general.start.life_3", "general.start.life_4", "general.start.life_5");
        AddNextGeneralChainNode(nodes, result, general, "general.start.xp_1", "general.start.xp_2", "general.start.xp_3", "general.start.xp_4", "general.start.xp_5");
        AddNextGeneralChainNode(nodes, result, general, "general.start.path_1", "general.start.path_2");
        AddNextGeneralChainNode(nodes, result, general, "general.start.protection_1");
        AddNextGeneralChainNode(nodes, result, general, "general.start.reserve_1");
        AddNextGeneralChainNode(nodes, result, general, "general.loadout.slot_2", "general.loadout.slot_3", "general.loadout.slot_4", "general.loadout.slot_5", "general.loadout.slot_6", "general.loadout.slot_7", "general.loadout.slot_8", "general.loadout.slot_9", "general.loadout.slot_10", "general.loadout.slot_11", "general.loadout.slot_12");
        AddNextGeneralChainNode(nodes, result, general, "general.start.discount_1", "general.start.discount_2", "general.start.discount_3", "general.start.discount_4", "general.start.discount_5");
        return result;
    }

    private void AddNextGeneralChainNode(List<GeneralMetaNodeDefinition> source, List<GeneralMetaNodeDefinition> result, GeneralMetaProgressionManager general, params string[] nodeIds)
    {
        if (source == null || result == null || general == null || nodeIds == null)
            return;

        for (int i = 0; i < nodeIds.Length; i++)
        {
            string nodeId = nodeIds[i];
            if (string.IsNullOrEmpty(nodeId) || general.IsNodePurchased(nodeId))
                continue;

            AddGeneralNodeIfPresent(source, result, nodeId);
            return;
        }
    }

    private void AddUnpurchasedGeneralNode(List<GeneralMetaNodeDefinition> source, List<GeneralMetaNodeDefinition> result, GeneralMetaProgressionManager general, string nodeId)
    {
        if (general != null && !general.IsNodePurchased(nodeId))
            AddGeneralNodeIfPresent(source, result, nodeId);
    }

    private void AddGeneralNodeIfPresent(List<GeneralMetaNodeDefinition> source, List<GeneralMetaNodeDefinition> result, string nodeId)
    {
        if (source == null || result == null || string.IsNullOrEmpty(nodeId) || ContainsGeneralNode(result, nodeId))
            return;

        for (int i = 0; i < source.Count; i++)
        {
            GeneralMetaNodeDefinition definition = source[i];
            if (definition != null && definition.nodeId == nodeId)
            {
                result.Add(definition);
                return;
            }
        }
    }

    private List<GeneralMetaNodeDefinition> FilterUnlockedGeneralNodes(List<GeneralMetaNodeDefinition> nodes, GeneralMetaProgressionManager general)
    {
        List<GeneralMetaNodeDefinition> result = new List<GeneralMetaNodeDefinition>();
        if (nodes == null || general == null)
            return result;

        for (int i = 0; i < nodes.Count; i++)
        {
            GeneralMetaNodeDefinition definition = nodes[i];
            if (definition != null && IsGeneralNodeVisibleInRuntime(general, definition.nodeId))
                result.Add(definition);
        }

        return result;
    }

    private bool IsGeneralNodeVisibleInRuntime(GeneralMetaProgressionManager general, string nodeId)
    {
        return general != null && !string.IsNullOrEmpty(nodeId) && (general.IsNodeActive(nodeId) || general.IsNodePurchased(nodeId));
    }

    private List<T> CreateNodeWindow<T>(List<T> orderedNodes, int maxNodes, Func<T, string> getNodeId, Func<string, bool> isPurchased)
    {
        List<T> result = new List<T>();
        if (orderedNodes == null || orderedNodes.Count == 0 || maxNodes <= 0)
            return result;

        if (orderedNodes.Count <= maxNodes)
        {
            result.AddRange(orderedNodes);
            return result;
        }

        int focusIndex = -1;
        for (int i = 0; i < orderedNodes.Count; i++)
        {
            string nodeId = getNodeId != null ? getNodeId(orderedNodes[i]) : "";
            bool purchased = !string.IsNullOrEmpty(nodeId) && isPurchased != null && isPurchased(nodeId);
            if (!purchased)
            {
                focusIndex = i;
                break;
            }
        }

        if (focusIndex < 0)
            focusIndex = orderedNodes.Count - 1;

        int startIndex = focusIndex < maxNodes ? 0 : focusIndex - 1;
        startIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, orderedNodes.Count - maxNodes));
        for (int i = startIndex; i < startIndex + maxNodes && i < orderedNodes.Count; i++)
            result.Add(orderedNodes[i]);

        return result;
    }

    private int CountGeneralBranchNodes(GeneralMetaCategory[] categories)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general == null)
            return 0;

        int count = 0;
        List<GeneralMetaNodeDefinition> definitions = general.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            GeneralMetaNodeDefinition definition = definitions[i];
            if (definition != null && GeneralCategoryMatches(definition.category, categories))
                count++;
        }

        return count;
    }

    private int CountPurchasedGeneralBranchNodes(GeneralMetaCategory[] categories)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        if (general == null)
            return 0;

        int count = 0;
        List<GeneralMetaNodeDefinition> definitions = general.GetDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            GeneralMetaNodeDefinition definition = definitions[i];
            if (definition != null && GeneralCategoryMatches(definition.category, categories) && general.IsNodePurchased(definition.nodeId))
                count++;
        }

        return count;
    }

    private bool GeneralCategoryMatches(GeneralMetaCategory category, GeneralMetaCategory[] categories)
    {
        if (categories == null)
            return false;

        for (int i = 0; i < categories.Length; i++)
        {
            if (categories[i] == category)
                return true;
        }

        return false;
    }

    private bool ContainsGeneralNode(List<GeneralMetaNodeDefinition> nodes, string nodeId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].nodeId == nodeId)
                return true;
        }

        return false;
    }

    private void CreateSkillBranchHeader(Transform parent, string name, string title, int progress, int total, float y, MetaHubTone tone, float branchWidth)
    {
        float headerY = y + 32f;
        Color32 toneColor = ToneColor(tone);
        CreateCanvasLabel(parent, name + "Title", title, new Vector2(-branchWidth * 0.5f + 82f, headerY), new Vector2(150f, 22f), 13f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(parent, name + "Count", progress + " / " + Mathf.Max(progress, total), new Vector2(branchWidth * 0.5f - 42f, headerY), new Vector2(82f, 18f), 9f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
        CreateLine(parent, name + "Line", new Vector2(0f, y + 7f), new Vector2(branchWidth, 2f), new Color32(toneColor.r, toneColor.g, toneColor.b, 115));
    }

    private void CreateSkillConnector(Transform parent, string name, float y, int nodeCount, float branchWidth, MetaHubTone tone, float centerX = 0f)
    {
        if (nodeCount <= 1)
            return;

        Color32 toneColor = ToneColor(tone);
        CreateLine(parent, name + "Main", new Vector2(centerX, y + 2f), new Vector2(branchWidth - 100f, 2f), new Color32(toneColor.r, toneColor.g, toneColor.b, 80));
        for (int i = 0; i < nodeCount; i++)
            CreateLine(parent, name + "Tick" + i, new Vector2(centerX + GetSkillNodeX(i, nodeCount, branchWidth), y - 13f), new Vector2(2f, 28f), new Color32(toneColor.r, toneColor.g, toneColor.b, 95));
    }

    private float GetSkillNodeX(int index, int nodeCount, float branchWidth)
    {
        if (nodeCount <= 1)
            return 0f;

        float usableWidth = branchWidth - 96f;
        return -usableWidth * 0.5f + usableWidth * index / Mathf.Max(1, nodeCount - 1);
    }

    private void CreateSkillTreeNode(Transform parent, string name, string title, string effect, string cost, string state, string artName, MetaHubTone tone, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Color32 toneColor = ToneColor(tone);
        Color32 fill = new Color32(6, 18, 18, 235);
        Transform node = CreateCanvasPanel(parent, name, position, size, fill, toneColor);
        CreateArtIcon(node, "Icon", artName, new Vector2(-size.x * 0.5f + 26f, 11f), new Vector2(34f, 34f));
        CreateCanvasLabel(node, "Title", Shorten(title, 18), new Vector2(20f, 17f), new Vector2(size.x - 58f, 18f), 8.3f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(node, "Effect", Shorten(effect, 34), new Vector2(20f, -3f), new Vector2(size.x - 58f, 18f), 6.8f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasLabel(node, "Cost", Shorten(cost, 11), new Vector2(-size.x * 0.25f, -26f), new Vector2(size.x * 0.45f, 14f), 7.2f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(node, "State", Shorten(state, 10), new Vector2(size.x * 0.25f, -26f), new Vector2(size.x * 0.45f, 14f), 7.2f, toneColor, TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(node, "InfoMarker", "i", new Vector2(size.x * 0.5f - 10f, size.y * 0.5f - 11f), new Vector2(14f, 14f), 7.5f, toneColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Transform tooltip = CreateSkillTreeTooltip(parent, name + "_Tooltip", title, effect, cost, state, tone, position);
        AddHoverTooltip(node, tooltip);
        MakeCanvasClickable(node, onClick, fill, toneColor);
    }

    private Transform CreateSkillTreeTooltip(Transform parent, string name, string title, string effect, string cost, string state, MetaHubTone tone, Vector2 sourcePosition)
    {
        Color32 toneColor = ToneColor(tone);
        Vector2 size = new Vector2(270f, 118f);
        float tooltipY = sourcePosition.y > 0f ? sourcePosition.y - 112f : sourcePosition.y + 112f;
        Vector2 position = new Vector2(Mathf.Clamp(sourcePosition.x, -238f, 238f), Mathf.Clamp(tooltipY, -166f, 166f));
        Transform tooltip = CreateCanvasPanel(parent, name, position, size, new Color32(3, 11, 13, 252), toneColor);
        tooltip.gameObject.SetActive(false);
        SetRaycastTargetRecursive(tooltip, false);

        CreateCanvasLabel(tooltip, "Title", title, new Vector2(0f, 42f), new Vector2(236f, 20f), 11f, new Color32(246, 236, 211, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(tooltip, "Meta", Shorten(state + "  |  " + cost, 38), new Vector2(0f, 22f), new Vector2(236f, 18f), 8.5f, toneColor, TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI body = CreateCanvasLabel(tooltip, "Description", string.IsNullOrEmpty(effect) ? "Keine Beschreibung vorhanden." : effect, new Vector2(0f, -21f), new Vector2(236f, 62f), 8.4f, new Color32(224, 214, 196, 255), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Ellipsis;
        return tooltip;
    }

    private void AddHoverTooltip(Transform target, Transform tooltip)
    {
        if (target == null || tooltip == null)
            return;

        EventTrigger trigger = target.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry enter = new EventTrigger.Entry();
        enter.eventID = EventTriggerType.PointerEnter;
        enter.callback.AddListener(delegate
        {
            if (tooltip != null)
            {
                tooltip.gameObject.SetActive(true);
                tooltip.SetAsLastSibling();
            }
        });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener(delegate
        {
            if (tooltip != null)
                tooltip.gameObject.SetActive(false);
        });
        trigger.triggers.Add(exit);
    }

    private void SetRaycastTargetRecursive(Transform root, bool enabled)
    {
        if (root == null)
            return;

        UnityEngine.UI.Graphic[] graphics = root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = enabled;
    }

    private string GetCategoryArtName(GeneralMetaCategory category)
    {
        switch (category)
        {
            case GeneralMetaCategory.TowerUnlock: return "icon_tower";
            case GeneralMetaCategory.TileUnlock: return "icon_path";
            case GeneralMetaCategory.QoL: return "icon_blue_star";
            case GeneralMetaCategory.StartOption: return "icon_gold";
            case GeneralMetaCategory.MetaLoadout: return "icon_keystone";
            case GeneralMetaCategory.EnemyResearch: return "icon_skull";
            default: return "icon_shield";
        }
    }

    private string GetChaosCategoryArtName(ChaosResearchCategory category)
    {
        switch (category)
        {
            case ChaosResearchCategory.RiskPool: return "icon_risk_core";
            case ChaosResearchCategory.ChaosVariants: return "icon_skull";
            case ChaosResearchCategory.ChaosWaves: return "icon_chaos_sun";
            case ChaosResearchCategory.ChaosCounters: return "icon_blue_star";
            case ChaosResearchCategory.Chaos5Endgame: return "icon_chaos_sun";
            case ChaosResearchCategory.JusticeOrder: return "icon_justice";
            default: return "icon_chaos";
        }
    }

    private string GetPathCategoryArtName(PathTechniqueCategory category)
    {
        switch (category)
        {
            case PathTechniqueCategory.EventPool: return "icon_path";
            case PathTechniqueCategory.EventQuality: return "icon_blue_star";
            case PathTechniqueCategory.RescuePower: return "icon_gold";
            case PathTechniqueCategory.PathTools: return "icon_path";
            case PathTechniqueCategory.TileTechnique: return "icon_blueprint";
            case PathTechniqueCategory.RiftArchitecture: return "icon_chaos_sun";
            default: return "icon_path";
        }
    }

    private string GetEliteCategoryArtName(EliteHuntCategory category)
    {
        switch (category)
        {
            case EliteHuntCategory.Contracts: return "icon_skull";
            case EliteHuntCategory.Affixes: return "icon_chaos";
            case EliteHuntCategory.Rewards: return "icon_risk_core";
            case EliteHuntCategory.Frequency: return "icon_skull";
            case EliteHuntCategory.Counters: return "icon_blue_star";
            case EliteHuntCategory.RiftElite: return "icon_chaos_sun";
            default: return "icon_skull";
        }
    }

    private void CreateTowerMilestoneNode(Transform parent, string name, TowerMasteryManager towerMastery, TowerRole role, TowerMasteryRoleProfile profile, TowerMasteryMilestone milestone, string title, string artName, MetaHubTone tone, Vector2 position)
    {
        int requirement = TowerMasteryManager.GetMilestoneSpentPointRequirement(milestone);
        int spent = profile != null ? profile.spentPoints : 0;
        bool unlocked = towerMastery != null && towerMastery.IsMilestoneUnlocked(role, milestone);
        string progress = unlocked ? "OK" : Mathf.Min(spent, requirement) + " / " + requirement;
        CreateUnlockNode(parent, name, title, progress, artName, unlocked ? tone : MetaHubTone.Neutral, position);
    }

    private MetaHubTone TowerTone(TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Fire:
            case TowerRole.Mortar:
                return MetaHubTone.Red;
            case TowerRole.Slow:
            case TowerRole.Lightning:
                return MetaHubTone.Cyan;
            case TowerRole.Poison:
                return MetaHubTone.Green;
            case TowerRole.Alchemist:
            case TowerRole.Spike:
                return MetaHubTone.Purple;
            case TowerRole.Basic:
            case TowerRole.Rapid:
            case TowerRole.Heavy:
            case TowerRole.Sniper:
                return MetaHubTone.Gold;
            default:
                return MetaHubTone.Neutral;
        }
    }

    private string Shorten(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || maxLength <= 0)
            return "";
        if (text.Length <= maxLength)
            return text;
        return text.Substring(0, Mathf.Max(1, maxLength - 1)) + ".";
    }

    private Transform CreateUnlockPanel(Transform parent, string name, string title, Vector2 position, Vector2 size, Color32 border)
    {
        Transform panel = CreateOrnatePanel(parent, name, position, size, new Color32(5, 18, 18, 240), border);
        float titleFontSize = size.x < 280f ? 14f : 16f;
        if (runtimeUnlockInfoMode)
            titleFontSize += 2f;
        CreateCanvasLabel(panel, "Title", title, new Vector2(0f, size.y * 0.5f - 26f), new Vector2(size.x - 36f, 28f), titleFontSize, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        return panel;
    }

    private void CreateUnlockStatCard(Transform parent, string name, string title, string value, string caption, string artName, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        Transform card = CreateOrnatePanel(parent, name, position, size, new Color32(7, 18, 20, 240), ToneColor(tone));
        CreateCanvasLabel(card, "Title", title, new Vector2(0f, size.y * 0.5f - 28f), new Vector2(size.x - 38f, 24f), runtimeUnlockInfoMode ? 15.5f : 14f, new Color32(246, 236, 211, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateArtIcon(card, "Icon", artName, new Vector2(-size.x * 0.25f, 0f), new Vector2(68f, 68f));
        float valueWidth = Mathf.Clamp(size.x * 0.42f, 112f, 180f);
        float valueFontSize = !string.IsNullOrEmpty(value) && value.Length > 7 ? 23f : !string.IsNullOrEmpty(value) && value.Length > 4 ? 25f : 28f;
        if (runtimeUnlockInfoMode)
            valueFontSize += 1.5f;
        TextMeshProUGUI valueLabel = CreateCanvasLabel(card, "Value", value, new Vector2(34f, 12f), new Vector2(valueWidth, 34f), valueFontSize, new Color32(245, 195, 92, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        valueLabel.enableAutoSizing = true;
        valueLabel.fontSizeMin = 18f;
        valueLabel.fontSizeMax = valueFontSize;
        CreateCanvasLabel(card, "Caption", caption, new Vector2(42f, -22f), new Vector2(valueWidth + 10f, 22f), runtimeUnlockInfoMode ? 12.5f : 11f, new Color32(225, 209, 184, 255), TextAlignmentOptions.Left, FontStyles.Normal);
    }

    private void CreateTowerListRow(Transform parent, string name, string title, string caption, bool selected, float y, MetaHubTone tone, UnityEngine.Events.UnityAction onClick)
    {
        Color32 border = selected ? new Color32(176, 98, 255, 255) : new Color32(45, 58, 53, 255);
        Color32 fill = selected ? new Color32(37, 20, 64, 245) : new Color32(5, 18, 18, 238);
        Transform row = CreateCanvasPanel(parent, "TowerRow_" + name, new Vector2(0f, y), new Vector2(140f, 42f), fill, border);
        CreateArtIcon(row, "Icon", GetNavArtName("tower"), new Vector2(-48f, 0f), new Vector2(34f, 34f));
        CreateCanvasLabel(row, "Title", title, new Vector2(18f, 7f), new Vector2(82f, 18f), 11f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(row, "Caption", caption, new Vector2(21f, -10f), new Vector2(88f, 16f), 8f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        MakeCanvasClickable(row, onClick, fill, ToneColor(tone));
    }

    private void CreateUnlockNode(Transform parent, string name, string title, string progress, string artName, MetaHubTone tone, Vector2 position)
    {
        Transform node = CreateCanvasPanel(parent, name, position, new Vector2(138f, 66f), new Color32(9, 20, 20, 238), ToneColor(tone));
        CreateArtIcon(node, "Icon", artName, new Vector2(-45f, 5f), new Vector2(40f, 40f));
        CreateCanvasLabel(node, "Title", title, new Vector2(28f, 10f), new Vector2(82f, 24f), 9f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(node, "Progress", progress, new Vector2(27f, -16f), new Vector2(78f, 18f), 10f, ToneColor(tone), TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private void CreateUnlockShopCard(Transform parent, string name, string title, string cost, string state, string artName, MetaHubTone tone, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick = null)
    {
        Transform card = CreateCanvasPanel(parent, name, position, size, new Color32(7, 18, 20, 238), ToneColor(tone));
        float iconSize = Mathf.Min(42f, size.x * 0.34f);
        CreateArtIcon(card, "Icon", artName, new Vector2(0f, size.y * 0.22f), new Vector2(iconSize, iconSize));
        CreateCanvasLabel(card, "Title", Shorten(title, size.x < 108f ? 13 : 17), new Vector2(0f, -5f), new Vector2(size.x - 18f, 28f), 8.5f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(card, "Cost", Shorten(cost, size.x < 108f ? 9 : 13), new Vector2(0f, -size.y * 0.25f), new Vector2(size.x - 16f, 16f), 8f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(card, "State", state, new Vector2(0f, -size.y * 0.5f + 16f), new Vector2(size.x - 20f, 18f), 8f, ToneColor(tone), TextAlignmentOptions.Center, FontStyles.Bold);
        MakeCanvasClickable(card, onClick, new Color32(7, 18, 20, 238), ToneColor(tone));
    }

    private void CreateUnlockKeystoneCard(Transform parent, string title, string state, string artName, MetaHubTone tone, Vector2 position)
    {
        Transform card = CreateCanvasPanel(parent, "Keystone_" + title, position, new Vector2(145f, 118f), new Color32(7, 18, 20, 238), ToneColor(tone));
        CreateArtIcon(card, "Icon", artName, new Vector2(-42f, 20f), new Vector2(54f, 54f));
        CreateCanvasLabel(card, "Title", title, new Vector2(38f, 24f), new Vector2(78f, 38f), 9f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(card, "State", state, new Vector2(0f, -38f), new Vector2(120f, 30f), 9f, ToneColor(tone), TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private void CreateUnlockRow(Transform parent, string name, string artName, string title, string description, string value, MetaHubTone tone, float y, float width, UnityEngine.Events.UnityAction onClick = null)
    {
        Color32 toneColor = ToneColor(tone);
        Color32 fill = new Color32(7, 18, 20, 220);
        Transform row = CreateCanvasPanel(parent, name, new Vector2(0f, y), new Vector2(width, 46f), fill, new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateArtIcon(row, "Icon", artName, new Vector2(-width * 0.5f + 26f, 0f), new Vector2(30f, 30f));
        float valueWidth = string.IsNullOrEmpty(value) ? 0f : Mathf.Min(70f, width * 0.3f);
        float textLeft = 54f;
        float textRightPadding = string.IsNullOrEmpty(value) ? 12f : valueWidth + 16f;
        float textWidth = Mathf.Max(54f, width - textLeft - textRightPadding);
        float textX = -width * 0.5f + textLeft + textWidth * 0.5f;
        float titleFontSize = runtimeUnlockInfoMode ? 10.8f : 9f;
        float descriptionFontSize = runtimeUnlockInfoMode ? 8.8f : 7.5f;
        float valueFontSize = runtimeUnlockInfoMode ? 10.4f : 9f;
        CreateCanvasLabel(row, "Title", Shorten(title, Mathf.RoundToInt(textWidth / 7f)), new Vector2(textX, 8.5f), new Vector2(textWidth, 20f), titleFontSize, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(row, "Description", Shorten(description, Mathf.RoundToInt(textWidth / 6f)), new Vector2(textX, -10.5f), new Vector2(textWidth, 18f), descriptionFontSize, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (!string.IsNullOrEmpty(value))
            CreateCanvasLabel(row, "Value", Shorten(value, Mathf.RoundToInt(valueWidth / 6f)), new Vector2(width * 0.5f - valueWidth * 0.5f - 10f, 0f), new Vector2(valueWidth, 22f), valueFontSize, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
        MakeCanvasClickable(row, onClick, fill, toneColor);
    }

    private void CreateUnlockCompactRow(Transform parent, string name, string artName, string title, string description, string value, MetaHubTone tone, float y, float width, UnityEngine.Events.UnityAction onClick = null)
    {
        Color32 toneColor = ToneColor(tone);
        Color32 fill = new Color32(7, 18, 20, 220);
        Transform row = CreateCanvasPanel(parent, name, new Vector2(0f, y), new Vector2(width, 38f), fill, new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateArtIcon(row, "Icon", artName, new Vector2(-width * 0.5f + 24f, 0f), new Vector2(26f, 26f));
        float valueWidth = string.IsNullOrEmpty(value) ? 0f : Mathf.Min(70f, width * 0.28f);
        float textLeft = 50f;
        float textRightPadding = string.IsNullOrEmpty(value) ? 12f : valueWidth + 16f;
        float textWidth = Mathf.Max(54f, width - textLeft - textRightPadding);
        float textX = -width * 0.5f + textLeft + textWidth * 0.5f;
        float titleFontSize = runtimeUnlockInfoMode ? 10.2f : 8.4f;
        float descriptionFontSize = runtimeUnlockInfoMode ? 8.4f : 6.8f;
        float valueFontSize = runtimeUnlockInfoMode ? 9.8f : 8.2f;
        CreateCanvasLabel(row, "Title", Shorten(title, Mathf.RoundToInt(textWidth / 7f)), new Vector2(textX, 7.5f), new Vector2(textWidth, 18f), titleFontSize, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(row, "Description", Shorten(description, Mathf.RoundToInt(textWidth / 6f)), new Vector2(textX, -9.5f), new Vector2(textWidth, 16f), descriptionFontSize, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (!string.IsNullOrEmpty(value))
            CreateCanvasLabel(row, "Value", Shorten(value, Mathf.RoundToInt(valueWidth / 6f)), new Vector2(width * 0.5f - valueWidth * 0.5f - 10f, 0f), new Vector2(valueWidth, 18f), valueFontSize, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
        MakeCanvasClickable(row, onClick, fill, toneColor);
    }

    private void MakeCanvasClickable(Transform target, UnityEngine.Events.UnityAction onClick, Color baseColor, Color highlightColor)
    {
        if (target == null || onClick == null)
            return;

        UnityEngine.UI.Button button = target.gameObject.GetComponent<UnityEngine.UI.Button>();
        if (button == null)
            button = target.gameObject.AddComponent<UnityEngine.UI.Button>();

        button.onClick.AddListener(onClick);
        ApplyCanvasButtonColors(button, baseColor, highlightColor);
    }

    private string GetMetricValue(MetaHubData data, string id, string fallback)
    {
        if (data == null || data.metricCards == null)
            return fallback;

        for (int i = 0; i < data.metricCards.Count; i++)
        {
            MetaHubMetricCardData card = data.metricCards[i];
            if (card != null && card.id == id)
                return string.IsNullOrEmpty(card.valueText) ? fallback : card.valueText;
        }

        return fallback;
    }

    private string GetSideStatValue(MetaHubData data, string id)
    {
        if (data == null || data.progressStats == null)
            return "0";

        for (int i = 0; i < data.progressStats.Count; i++)
        {
            MetaHubSideStatData stat = data.progressStats[i];
            if (stat != null && stat.id == id)
                return string.IsNullOrEmpty(stat.valueText) ? "0" : stat.valueText;
        }

        return "0";
    }

    private int GetResourceValue(MetaHubData data, string id)
    {
        if (data == null || data.resources == null)
            return 0;

        for (int i = 0; i < data.resources.Count; i++)
        {
            MetaHubResourceData resource = data.resources[i];
            if (resource != null && resource.id == id)
                return resource.value;
        }

        return 0;
    }

    private void CreateReferenceResource(Transform parent, MetaHubResourceData data, int index)
    {
        float x = 40f + index * 118f;
        CreateArtIcon(parent, "ResourceIcon_" + data.id, GetResourceArtName(data.id), new Vector2(x, -47f), new Vector2(48f, 48f));
        CreateCanvasLabel(parent, "ResourceValue_" + data.id, MetaHubMockData.FormatNumber(data.value), new Vector2(x + 74f, -46f), new Vector2(88f, 28f), 16f, new Color32(244, 222, 181, 255), TextAlignmentOptions.Left, FontStyles.Bold);
    }

    private void CreateReferenceNavRow(Transform parent, MetaHubNavItemData data, int index)
    {
        float y = -92f - index * 68f;
        Color32 tone = ToneColor(data.tone);
        Color32 fill = data.selected ? new Color32(42, 32, 13, 238) : new Color32(3, 17, 18, 238);
        Transform row = CreateCanvasPanelTop(parent, "Nav_" + data.id, new Vector2(0f, y), new Vector2(248f, 60f), fill, data.selected ? new Color32(218, 159, 50, 255) : tone);
        CreateArtIcon(row, "NavIcon_" + data.id, GetNavArtName(data.id), new Vector2(-86f, 0f), new Vector2(50f, 50f));
        CreateCanvasLabel(row, "NavLabel_" + data.id, ToReferenceNavLabel(data.label), new Vector2(32f, 0f), new Vector2(165f, 42f), 15f, data.selected ? new Color32(255, 255, 255, 255) : new Color32(221, 210, 187, 255), TextAlignmentOptions.Left, FontStyles.Bold);

        UnityEngine.UI.Button button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
        string navId = data.id;
        button.onClick.AddListener(delegate { SelectNavigationSection(navId); });
        ApplyCanvasButtonColors(button, fill, tone);
    }

    private void CreateReferenceKeystone(Transform parent, MetaHubKeystoneData data, int index)
    {
        float x = -65f + index * 64f;
        CreateArtIcon(parent, "KeystoneIcon_" + data.id, GetKeystoneArtName(data.id), new Vector2(x, -620f), new Vector2(72f, 72f), true);
        CreateCanvasLabelTop(parent, "KeystoneLevel_" + data.id, "Lv. " + data.level, new Vector2(x, -673f), new Vector2(54f, 20f), 12f, new Color32(244, 200, 102, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateLine(parent, "KeystoneBar_" + data.id, new Vector2(x, -692f), new Vector2(50f, 3f), new Color32(193, 137, 50, 230), true);
    }

    private void CreateRuntimeSidebarStatus(Transform parent)
    {
        CreateCanvasLabelTop(parent, "RuntimeSidebarTitle", "RUN-INFOS", new Vector2(-5f, -525f), new Vector2(238f, 30f), 17f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateRuntimeSidebarRow(parent, "RuntimeSideWave", "icon_path", "Welle", GetRuntimeWaveNumber().ToString(), MetaHubTone.Cyan, -575f);
        CreateRuntimeSidebarRow(parent, "RuntimeSideRes", "icon_gold", "Gold / Leben", GetRuntimeGold() + " / " + GetRuntimeLives(), MetaHubTone.Gold, -629f);
        CreateRuntimeSidebarRow(parent, "RuntimeSideRisk", "icon_risk_core", "Risiken", GetRuntimeRiskCount().ToString(), MetaHubTone.Red, -683f);
    }

    private void CreateRuntimeSidebarRow(Transform parent, string name, string artName, string label, string value, MetaHubTone tone, float y)
    {
        Color32 toneColor = ToneColor(tone);
        Transform row = CreateCanvasPanelTop(parent, name, new Vector2(0f, y), new Vector2(228f, 44f), new Color32(7, 18, 20, 220), new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateArtIcon(row, "Icon", artName, new Vector2(-82f, 0f), new Vector2(32f, 32f));
        CreateCanvasLabel(row, "Label", label, new Vector2(-10f, 8f), new Vector2(118f, 18f), 10f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(row, "Value", value, new Vector2(-10f, -10f), new Vector2(118f, 18f), 9f, toneColor, TextAlignmentOptions.Left, FontStyles.Bold);
    }

    private RuntimeSummaryItem CreateRuntimeSummaryItem(string artName, string label, string caption, string value, MetaHubTone tone)
    {
        RuntimeSummaryItem item = new RuntimeSummaryItem();
        item.artName = artName;
        item.label = label;
        item.caption = caption;
        item.value = value;
        item.tone = tone;
        return item;
    }

    private void CreateRuntimeRuleRow(Transform parent, string name, string artName, string title, string description, string value, MetaHubTone tone, float y, float width)
    {
        Color32 toneColor = ToneColor(tone);
        Transform row = CreateCanvasPanel(parent, name, new Vector2(0f, y), new Vector2(width, 32f), new Color32(7, 18, 20, 220), new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateArtIcon(row, "Icon", artName, new Vector2(-width * 0.5f + 21f, 0f), new Vector2(24f, 24f));

        float valueWidth = 72f;
        float textLeft = 46f;
        float textRight = valueWidth + 12f;
        float textWidth = Mathf.Max(120f, width - textLeft - textRight);
        float textX = -width * 0.5f + textLeft + textWidth * 0.5f;
        CreateCanvasLabel(row, "Title", Shorten(title, Mathf.RoundToInt(textWidth / 7f)), new Vector2(textX, 6.5f), new Vector2(textWidth, 16f), 10.2f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(row, "Description", Shorten(description, Mathf.RoundToInt(textWidth / 6f)), new Vector2(textX, -8.5f), new Vector2(textWidth, 14f), 7.8f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasLabel(row, "Value", Shorten(value, 10), new Vector2(width * 0.5f - valueWidth * 0.5f - 10f, 0f), new Vector2(valueWidth, 20f), 9.8f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
    }

    private void CreateRuntimeSummaryPanel(Transform parent, string name, string title, Vector2 position, Vector2 size, params RuntimeSummaryItem[] items)
    {
        Transform panel = CreateUnlockPanel(parent, name, title, position, size, new Color32(122, 94, 55, 255));
        if (items == null || items.Length == 0)
            return;

        int visibleCount = Mathf.Min(items.Length, 4);
        float innerWidth = size.x - 74f;
        float gap = 14f;
        float tileWidth = (innerWidth - gap * (visibleCount - 1)) / visibleCount;
        float startX = -innerWidth * 0.5f + tileWidth * 0.5f;

        for (int i = 0; i < visibleCount; i++)
        {
            RuntimeSummaryItem item = items[i];
            Color32 toneColor = ToneColor(item.tone);
            float x = startX + i * (tileWidth + gap);
            Transform tile = CreateCanvasPanel(panel, "RuntimeSummary_" + i, new Vector2(x, -18f), new Vector2(tileWidth, 54f), new Color32(7, 18, 20, 220), new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
            CreateArtIcon(tile, "Icon", item.artName, new Vector2(-tileWidth * 0.5f + 28f, 0f), new Vector2(32f, 32f));

            float valueWidth = Mathf.Clamp(tileWidth * 0.26f, 48f, 82f);
            float textLeft = 56f;
            float textRight = valueWidth + 14f;
            float textWidth = Mathf.Max(64f, tileWidth - textLeft - textRight);
            float textX = -tileWidth * 0.5f + textLeft + textWidth * 0.5f;
            TextMeshProUGUI label = CreateCanvasLabel(tile, "Label", Shorten(item.label, Mathf.RoundToInt(textWidth / 7f)), new Vector2(textX, 10f), new Vector2(textWidth, 19f), 10.6f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 10.6f;

            TextMeshProUGUI caption = CreateCanvasLabel(tile, "Caption", Shorten(item.caption, Mathf.RoundToInt(textWidth / 6f)), new Vector2(textX, -9f), new Vector2(textWidth, 17f), 8.5f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 6.8f;
            caption.fontSizeMax = 8.5f;

            TextMeshProUGUI value = CreateCanvasLabel(tile, "Value", Shorten(item.value, Mathf.RoundToInt(valueWidth / 6f)), new Vector2(tileWidth * 0.5f - valueWidth * 0.5f - 10f, 0f), new Vector2(valueWidth, 22f), 10.6f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
            value.enableAutoSizing = true;
            value.fontSizeMin = 8f;
            value.fontSizeMax = 10.6f;
        }
    }

    private void CreateReferenceMetricCard(Transform parent, MetaHubMetricCardData data, int index)
    {
        const float cardWidth = 228f;
        float parentWidth = GetPanelWidth(parent, 1216f);
        float gap = Mathf.Max(12f, (parentWidth - 20f - cardWidth * 5f) / 4f);
        float x = -parentWidth * 0.5f + 10f + cardWidth * 0.5f + index * (cardWidth + gap);
        Transform card = CreateOrnatePanel(parent, "Metric_" + data.id, new Vector2(x, 214f), new Vector2(cardWidth, 150f), new Color32(7, 18, 20, 240), ToneColor(data.tone));
        CreateCanvasLabel(card, "Title", data.title, new Vector2(0f, 54f), new Vector2(200f, 22f), 15f, new Color32(246, 236, 211, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateArtIcon(card, "Icon", GetMetricArtName(data.id), new Vector2(-55f, 10f), new Vector2(78f, 78f));
        CreateCanvasLabel(card, "Value", data.valueText, new Vector2(30f, 10f), new Vector2(80f, 34f), 25f, new Color32(245, 195, 92, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(card, "Caption", data.caption, new Vector2(42f, -20f), new Vector2(130f, 20f), 12f, new Color32(225, 209, 184, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (data.showProgress)
            CreateCanvasBar(card, "Progress", new Vector2(42f, -48f), new Vector2(100f, 5f), Percent(data.current, data.maximum), ToneColor(data.tone));
        if (data.showProgress)
            CreateCanvasLabel(card, "ProgressText", data.current + " / " + data.maximum, new Vector2(30f, -35f), new Vector2(100f, 18f), 11f, new Color32(236, 211, 161, 255), TextAlignmentOptions.Center, FontStyles.Normal);
    }

    private float GetPanelWidth(Transform panel, float fallback)
    {
        RectTransform rect = panel as RectTransform;
        if (rect != null && rect.sizeDelta.x > 0.01f)
            return rect.sizeDelta.x;

        return fallback;
    }

    private void CreateReferenceProgressPanel(Transform parent, MetaHubData data)
    {
        CreateReferenceProgressPanel(parent, data, new Vector2(-390f, -10f), new Vector2(382f, 296f));
    }

    private void CreateReferenceProgressPanel(Transform parent, MetaHubData data, Vector2 position, Vector2 size)
    {
        Transform panel = CreateOrnatePanel(parent, "ProgressPanel", position, size, new Color32(6, 18, 20, 238), new Color32(122, 94, 55, 255));
        CreateCanvasLabel(panel, "Title", "FORTSCHRITT", new Vector2(-92f, 118f), new Vector2(170f, 30f), 20f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateDonutImage(panel, "AccountRing", new Vector2(-85f, 5f), 168f, Percent(data.account.currentXP, data.account.requiredXP), new Color32(55, 209, 235, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(panel, "Level", data.account.level.ToString(), new Vector2(-85f, 5f), new Vector2(96f, 46f), 38f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "LevelCaption", runtimeUnlockInfoMode ? "RUN WELLE" : "ACCOUNT LEVEL", new Vector2(-85f, -34f), new Vector2(130f, 20f), 12f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "XPBottom", runtimeUnlockInfoMode ? "Tower-XP " + MetaHubMockData.FormatNumber(data.account.currentXP) : MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP", new Vector2(-85f, -98f), new Vector2(180f, 22f), 14f, new Color32(238, 222, 194, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        for (int i = 0; i < data.progressStats.Count; i++)
            CreateReferenceSideStat(panel, data.progressStats[i], i);
    }

    private void CreateReferenceSideStat(Transform parent, MetaHubSideStatData data, int index)
    {
        Transform row = CreateCanvasPanel(parent, "ProgressStat_" + data.id, new Vector2(105f, 66f - index * 50f), new Vector2(160f, 43f), new Color32(9, 23, 28, 238), new Color32(50, 69, 74, 255));
        CreateCanvasLabel(row, "Label", data.label, new Vector2(-28f, 0f), new Vector2(102f, 22f), 12f, new Color32(226, 213, 189, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasLabel(row, "Value", data.valueText, new Vector2(65f, 0f), new Vector2(30f, 22f), 12f, ToneColor(data.tone), TextAlignmentOptions.Right, FontStyles.Bold);
        CreateArtIcon(row, "Icon", GetSideStatArtName(data.id), new Vector2(78f, 0f), new Vector2(30f, 30f));
    }

    private void CreateReferenceChaosPanel(Transform parent, MetaHubChaosJusticeData data)
    {
        Transform panel = CreateOrnatePanel(parent, "ChaosPanel", new Vector2(58f, -10f), new Vector2(500f, 296f), new Color32(6, 18, 20, 238), new Color32(122, 94, 55, 255));
        CreateCanvasLabel(panel, "Title", "CHAOS / GERECHTIGKEIT", new Vector2(-84f, 118f), new Vector2(320f, 34f), 20f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateArtIcon(panel, "JusticeIcon", "icon_justice", new Vector2(-200f, 42f), new Vector2(72f, 72f));
        CreateArtIcon(panel, "ChaosIcon", "icon_chaos_sun", new Vector2(200f, 42f), new Vector2(72f, 72f));
        CreateCanvasLabel(panel, "JusticeLabel", "GERECHTIGKEIT", new Vector2(-112f, 47f), new Vector2(150f, 22f), 13f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "BalanceLabel", "BALANCE", new Vector2(0f, 47f), new Vector2(100f, 22f), 13f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "ChaosLabel", "CHAOS", new Vector2(112f, 47f), new Vector2(110f, 22f), 13f, new Color32(255, 84, 78, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasBar(panel, "Balance", new Vector2(0f, 16f), new Vector2(290f, 14f), data.safetyPercent / 100f, new Color32(229, 171, 48, 255));
        CreateLine(panel, "BalanceMarker", new Vector2(0f, 16f), new Vector2(3f, 43f), new Color32(248, 230, 183, 255));
        CreateCanvasPanel(panel, "SafetyBox", new Vector2(-145f, -78f), new Vector2(140f, 84f), new Color32(4, 16, 17, 238), new Color32(39, 50, 49, 255));
        CreateCanvasPanel(panel, "BalanceBox", new Vector2(0f, -78f), new Vector2(140f, 84f), new Color32(4, 16, 17, 238), new Color32(39, 50, 49, 255));
        CreateCanvasPanel(panel, "ChaosBox", new Vector2(145f, -78f), new Vector2(140f, 84f), new Color32(4, 16, 17, 238), new Color32(39, 50, 49, 255));
        CreateCanvasLabel(panel, "SafetyScore", "SAFETY SCORE\n" + data.safetyScore, new Vector2(-145f, -78f), new Vector2(130f, 65f), 17f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "BalanceValue", data.safetyPercent + "%\n" + data.stabilityLabel, new Vector2(0f, -78f), new Vector2(120f, 65f), 21f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "ChaosScore", "CHAOS SCORE\n" + data.chaosScore, new Vector2(145f, -78f), new Vector2(130f, 65f), 17f, new Color32(255, 84, 78, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasButton(panel, "DetailsButton", "DETAILS ANZEIGEN", new Vector2(0f, -133f), new Vector2(205f, 34f), delegate { Debug.Log("MetaHub: Details button requested."); });
    }

    private void CreateReferenceGoalPanel(Transform parent, List<MetaHubGoalData> goals)
    {
        CreateReferenceGoalPanel(parent, goals, new Vector2(453f, -10f), new Vector2(312f, 296f));
    }

    private void CreateReferenceGoalPanel(Transform parent, List<MetaHubGoalData> goals, Vector2 position, Vector2 size)
    {
        Transform panel = CreateOrnatePanel(parent, "GoalPanel", position, size, new Color32(6, 18, 20, 238), new Color32(122, 94, 55, 255));
        float titleFontSize = size.x < 280f ? 16f : 20f;
        CreateCanvasLabel(panel, "Title", runtimeUnlockInfoMode ? "RUN-ZIELE" : "NÄCHSTE ZIELE", new Vector2(0f, size.y * 0.5f - 30f), new Vector2(size.x - 44f, 30f), titleFontSize, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        float iconX = -size.x * 0.5f + 45f;
        float valueWidth = 64f;
        float valueX = size.x * 0.5f - valueWidth * 0.5f - 18f;
        float titleLeft = iconX + 28f;
        float titleRight = valueX - valueWidth * 0.5f - 8f;
        float titleWidth = Mathf.Max(128f, titleRight - titleLeft);
        float titleX = titleLeft + titleWidth * 0.5f;
        int maxGoalRows = size.y < 155f ? 1 : size.y < 220f ? 2 : size.y < 280f ? 3 : 4;
        float rowSpacing = size.y < 220f ? 38f : size.y < 280f ? 40f : 44f;
        float rowStart = size.y < 155f ? size.y * 0.5f - 68f : size.y < 220f ? size.y * 0.5f - 58f : size.y < 280f ? size.y * 0.5f - 68f : 68f;
        int goalCount = goals != null ? goals.Count : 0;
        for (int i = 0; i < goalCount && i < maxGoalRows; i++)
        {
            MetaHubGoalData goal = goals[i];
            float y = rowStart - i * rowSpacing;
            CreateArtIcon(panel, "GoalIcon_" + i, GetGoalArtName(goal), new Vector2(iconX, y), new Vector2(34f, 34f));
            float goalFontSize = size.x < 280f ? 10f : 12f;
            if (runtimeUnlockInfoMode)
                goalFontSize += 1.5f;
            CreateCanvasLabel(panel, "GoalTitle_" + i, Shorten(goal.title, Mathf.RoundToInt(titleWidth / 7f)), new Vector2(titleX, y), new Vector2(titleWidth, 24f), goalFontSize, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            CreateCanvasLabel(panel, "GoalValue_" + i, goal.current + " / " + goal.required, new Vector2(valueX, y), new Vector2(valueWidth, 24f), goalFontSize, new Color32(232, 220, 198, 255), TextAlignmentOptions.Right, FontStyles.Bold);
            if (i < maxGoalRows - 1)
                CreateLine(panel, "GoalDivider_" + i, new Vector2(0f, y - rowSpacing * 0.55f), new Vector2(size.x - 62f, 1f), new Color32(45, 54, 50, 210));
        }
        float buttonHeight = size.y < 180f ? 34f : 38f;
        CreateCanvasButton(panel, "AllGoals", runtimeUnlockInfoMode ? "ALLE RUN-ZIELE" : "ALLE ZIELE ANZEIGEN", new Vector2(0f, -size.y * 0.5f + 24f), new Vector2(Mathf.Min(245f, size.x - 52f), buttonHeight), RequestAllGoals);
    }

    private void CreateAllGoalsOverlay(Transform parent, List<MetaHubGoalData> goals)
    {
        CreateCanvasPanel(parent, "AllGoalsShade", Vector2.zero, new Vector2(1180f, 720f), new Color32(0, 0, 0, 150), new Color32(0, 0, 0, 0));
        Transform panel = CreateOrnatePanel(parent, "AllGoalsOverlay", new Vector2(0f, -18f), new Vector2(640f, 430f), new Color32(5, 17, 18, 248), new Color32(184, 124, 38, 255));
        CreateCanvasLabel(panel, "Title", runtimeUnlockInfoMode ? "RUN-ZIELE" : "ALLE ZIELE", new Vector2(-218f, 174f), new Vector2(180f, 30f), 22f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);

        int goalCount = goals != null ? goals.Count : 0;
        int visibleRows = Mathf.Min(goalCount, 6);
        for (int i = 0; i < visibleRows; i++)
        {
            MetaHubGoalData goal = goals[i];
            string title = goal != null ? goal.title : "";
            string progress = goal != null ? goal.current + " / " + goal.required : "";
            string artName = goal != null ? GetGoalArtName(goal) : "icon_gold";
            MetaHubTone tone = goal != null ? goal.tone : MetaHubTone.Gold;
            CreateUnlockRow(panel, "AllGoalRow_" + i, artName, title, "Fortschritt", progress, tone, 122f - i * 44f, 555f);
        }

        if (visibleRows == 0)
            CreateCanvasLabel(panel, "Empty", "Aktuell sind keine Ziele verfuegbar.", new Vector2(0f, 30f), new Vector2(430f, 34f), 15f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Bold);

        CreateCanvasButton(panel, "CloseAllGoals", "SCHLIESSEN", new Vector2(0f, -176f), new Vector2(210f, 36f), CloseAllGoalsOverlay);
    }

    private void CreateAllRisksOverlay(Transform parent, List<MetaHubEffectData> risks)
    {
        CreateCanvasPanel(parent, "AllRisksShade", Vector2.zero, new Vector2(1180f, 720f), new Color32(0, 0, 0, 150), new Color32(0, 0, 0, 0));
        Transform panel = CreateOrnatePanel(parent, "AllRisksOverlay", new Vector2(0f, -18f), new Vector2(760f, 520f), new Color32(5, 17, 18, 248), new Color32(185, 67, 57, 255));
        CreateCanvasLabel(panel, "Title", "AKTIVE RISIKEN", new Vector2(-258f, 218f), new Vector2(260f, 30f), 22f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);

        int riskCount = risks != null ? risks.Count : 0;
        int visibleRows = Mathf.Min(riskCount, 16);
        bool twoColumns = visibleRows > 8;
        int rowsPerColumn = twoColumns ? Mathf.CeilToInt(visibleRows * 0.5f) : visibleRows;
        float rowWidth = twoColumns ? 320f : 680f;
        Transform leftColumn = twoColumns ? CreateCanvasPanel(panel, "AllRisksLeftColumn", new Vector2(-178f, 0f), new Vector2(340f, 380f), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0)) : panel;
        Transform rightColumn = twoColumns ? CreateCanvasPanel(panel, "AllRisksRightColumn", new Vector2(178f, 0f), new Vector2(340f, 380f), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0)) : panel;

        for (int i = 0; i < visibleRows; i++)
        {
            MetaHubEffectData risk = risks[i];
            int columnIndex = twoColumns && i >= rowsPerColumn ? 1 : 0;
            int rowIndex = twoColumns ? i % rowsPerColumn : i;
            Transform rowParent = columnIndex == 1 ? rightColumn : leftColumn;
            string title = risk != null ? risk.title : "";
            string description = risk != null ? risk.description : "";
            string value = risk != null ? risk.durationText : "";
            string artName = risk != null ? GetEffectArtName(risk) : "icon_risk_core";
            MetaHubTone tone = risk != null ? risk.tone : MetaHubTone.Red;
            CreateUnlockCompactRow(rowParent, "AllRiskRow_" + i, artName, title, description, value, tone, 164f - rowIndex * 45f, rowWidth);
        }

        if (riskCount > visibleRows)
            CreateCanvasLabel(panel, "Overflow", "+" + (riskCount - visibleRows) + " weitere Risiken", new Vector2(0f, -202f), new Vector2(430f, 24f), 13f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Bold);

        if (visibleRows == 0)
            CreateCanvasLabel(panel, "Empty", "Aktuell sind keine Risiken aktiv.", new Vector2(0f, 30f), new Vector2(430f, 34f), 15f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Bold);

        CreateCanvasButton(panel, "CloseAllRisks", "SCHLIESSEN", new Vector2(0f, -224f), new Vector2(210f, 36f), CloseAllRisksOverlay);
    }

    private void CreateReferenceEffectPanel(Transform parent, string name, string title, List<MetaHubEffectData> effects, Vector2 position, Vector2 size, Color32 border, UnityEngine.Events.UnityAction footerAction = null, string footerLabel = "")
    {
        Transform panel = CreateOrnatePanel(parent, name, position, size, new Color32(5, 21, 18, 238), border);
        CreateCanvasLabel(panel, "Title", title, new Vector2(-size.x * 0.5f + 126f, size.y * 0.5f - 30f), new Vector2(240f, 30f), 19f, border, TextAlignmentOptions.Left, FontStyles.Bold);
        int effectCount = effects != null ? effects.Count : 0;
        for (int i = 0; i < effectCount && i < 2; i++)
        {
            MetaHubEffectData effect = effects[i];
            float y = 38f - i * 66f;
            CreateArtIcon(panel, "EffectIcon_" + i, GetEffectArtName(effect), new Vector2(-size.x * 0.5f + 55f, y), new Vector2(43f, 43f));
            string effectText = string.IsNullOrEmpty(effect.description) ? effect.title : effect.title + "\n" + effect.description;
            float textWidth = Mathf.Max(190f, size.x - 250f);
            float effectFontSize = runtimeUnlockInfoMode ? 13.5f : 12f;
            CreateCanvasLabel(panel, "EffectText_" + i, effectText, new Vector2(-size.x * 0.5f + 78f + textWidth * 0.5f, y), new Vector2(textWidth, 45f), effectFontSize, new Color32(226, 236, 215, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            CreateCanvasLabel(panel, "EffectDuration_" + i, effect.durationText, new Vector2(size.x * 0.5f - 80f, y), new Vector2(90f, 24f), effectFontSize, new Color32(239, 219, 184, 255), TextAlignmentOptions.Right, FontStyles.Normal);
            if (i == 0)
                CreateLine(panel, "EffectDivider", new Vector2(20f, 5f), new Vector2(size.x - 48f, 1f), new Color32(border.r, border.g, border.b, 120));
        }

        if (footerAction != null)
        {
            string safeLabel = string.IsNullOrEmpty(footerLabel) ? "ALLE ANZEIGEN" : footerLabel;
            CreateCanvasButton(panel, name + "AllButton", safeLabel, new Vector2(0f, -size.y * 0.5f + 24f), new Vector2(Mathf.Min(210f, size.x - 52f), 34f), footerAction);
        }
    }

    private void CreateReferenceRunPanel(Transform parent, List<MetaHubRunStatData> stats)
    {
        CreateReferenceRunPanel(parent, stats, new Vector2(453f, -270f), new Vector2(300f, 192f));
    }

    private void CreateReferenceRunPanel(Transform parent, List<MetaHubRunStatData> stats, Vector2 position, Vector2 size)
    {
        Transform panel = CreateOrnatePanel(parent, "RunPanel", position, size, new Color32(7, 19, 23, 238), new Color32(90, 100, 110, 255));
        CreateCanvasLabel(panel, "Title", runtimeUnlockInfoMode ? "RUN-STATUS" : "LETZTER RUN", new Vector2(-size.x * 0.5f + 118f, size.y * 0.5f - 32f), new Vector2(210f, 30f), 20f, new Color32(246, 236, 211, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateLine(panel, "RunDividerTop", new Vector2(0f, size.y * 0.5f - 58f), new Vector2(size.x - 70f, 1f), new Color32(45, 54, 50, 210));

        int statCount = stats != null ? Mathf.Min(stats.Count, 4) : 0;
        if (size.x > 560f)
        {
            float availableWidth = size.x - 120f;
            float slotWidth = availableWidth / Mathf.Max(1, statCount);
            float cardWidth = Mathf.Min(250f, slotWidth - 18f);
            float startX = -availableWidth * 0.5f + slotWidth * 0.5f;
            for (int i = 0; i < statCount; i++)
            {
                float x = startX + i * slotWidth;
                CreateRunStatCard(panel, "RunStatCard_" + i, stats[i], new Vector2(x, -18f), new Vector2(cardWidth, 74f));
            }
            return;
        }

        for (int i = 0; i < statCount; i++)
        {
            float y = 18f - i * 31f;
            CreateCanvasLabel(panel, "RunLabel_" + i, stats[i].label, new Vector2(-48f, y), new Vector2(150f, 20f), 12f, new Color32(190, 198, 190, 255), TextAlignmentOptions.Left, FontStyles.Normal);
            CreateCanvasLabel(panel, "RunValue_" + i, stats[i].valueText, new Vector2(110f, y), new Vector2(60f, 20f), 12f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Right, FontStyles.Bold);
        }
    }

    private void CreateRunStatCard(Transform parent, string name, MetaHubRunStatData stat, Vector2 position, Vector2 size)
    {
        if (stat == null)
            return;

        MetaHubTone tone = GetRunStatTone(stat.id);
        Color32 toneColor = ToneColor(tone);
        Transform card = CreateCanvasPanel(parent, name, position, size, new Color32(5, 17, 19, 230), new Color32(toneColor.r, toneColor.g, toneColor.b, 150));
        CreateArtIcon(card, "Icon", GetRunStatArtName(stat.id), new Vector2(-size.x * 0.5f + 34f, 0f), new Vector2(38f, 38f));
        float valueWidth = 62f;
        float textLeft = 66f;
        float textRight = valueWidth + 28f;
        float textWidth = Mathf.Max(86f, size.x - textLeft - textRight);
        float textX = -size.x * 0.5f + textLeft + textWidth * 0.5f;
        CreateCanvasLabel(card, "Label", stat.label, new Vector2(textX, 12f), new Vector2(textWidth, 20f), runtimeUnlockInfoMode ? 12.5f : 11f, new Color32(220, 211, 195, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(card, "Caption", runtimeUnlockInfoMode ? "Aktueller Run" : "Letzter Run", new Vector2(textX, -10f), new Vector2(textWidth, 18f), runtimeUnlockInfoMode ? 9.4f : 8f, new Color32(166, 174, 168, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasLabel(card, "Value", stat.valueText, new Vector2(size.x * 0.5f - valueWidth * 0.5f - 12f, 0f), new Vector2(valueWidth, 22f), runtimeUnlockInfoMode ? 12.5f : 11f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
    }

    private string GetRunStatArtName(string id)
    {
        switch (id)
        {
            case "chaos": return "icon_chaos";
            case "blueprints": return "icon_blueprint";
            case "elite": return "icon_skull";
            case "wave": return "icon_path";
            case "kills": return "icon_skull";
            case "leaks": return "icon_shield";
            case "tower_xp": return "icon_tower";
            case "lives": return "icon_xp";
            case "kernwissen":
            default: return "icon_gold";
        }
    }

    private MetaHubTone GetRunStatTone(string id)
    {
        switch (id)
        {
            case "chaos": return MetaHubTone.Purple;
            case "blueprints": return MetaHubTone.Cyan;
            case "elite": return MetaHubTone.Red;
            case "wave": return MetaHubTone.Cyan;
            case "kills": return MetaHubTone.Red;
            case "leaks": return MetaHubTone.Red;
            case "tower_xp": return MetaHubTone.Purple;
            case "lives": return MetaHubTone.Cyan;
            case "kernwissen":
            default: return MetaHubTone.Gold;
        }
    }

    private Transform CreateOrnatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color32 fill, Color32 border)
    {
        Transform panel = CreateCanvasPanel(parent, name, anchoredPosition, size, fill, border);
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        float cornerLength = Mathf.Min(42f, Mathf.Min(size.x, size.y) * 0.18f);

        CreateLine(panel, name + "CornerTLH", new Vector2(-halfWidth + cornerLength * 0.5f, halfHeight - 1f), new Vector2(cornerLength, 2f), border);
        CreateLine(panel, name + "CornerTLV", new Vector2(-halfWidth + 1f, halfHeight - cornerLength * 0.5f), new Vector2(2f, cornerLength), border);
        CreateLine(panel, name + "CornerTRH", new Vector2(halfWidth - cornerLength * 0.5f, halfHeight - 1f), new Vector2(cornerLength, 2f), border);
        CreateLine(panel, name + "CornerTRV", new Vector2(halfWidth - 1f, halfHeight - cornerLength * 0.5f), new Vector2(2f, cornerLength), border);
        CreateLine(panel, name + "CornerBLH", new Vector2(-halfWidth + cornerLength * 0.5f, -halfHeight + 1f), new Vector2(cornerLength, 2f), border);
        CreateLine(panel, name + "CornerBLV", new Vector2(-halfWidth + 1f, -halfHeight + cornerLength * 0.5f), new Vector2(2f, cornerLength), border);
        CreateLine(panel, name + "CornerBRH", new Vector2(halfWidth - cornerLength * 0.5f, -halfHeight + 1f), new Vector2(cornerLength, 2f), border);
        CreateLine(panel, name + "CornerBRV", new Vector2(halfWidth - 1f, -halfHeight + cornerLength * 0.5f), new Vector2(2f, cornerLength), border);

        return panel;
    }

    private void CreateLine(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color32 color, bool topAnchored = false)
    {
        GameObject lineObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        lineObject.transform.SetParent(parent, false);
        RectTransform rect = lineObject.GetComponent<RectTransform>();
        bool rootChild = IsCanvasRootChild(parent);
        rect.anchorMin = rootChild ? new Vector2(0f, 1f) : topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchorMax = rootChild ? new Vector2(0f, 1f) : topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        UnityEngine.UI.Image image = lineObject.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void CreateDiamondIcon(Transform parent, string name, string text, Vector2 anchoredPosition, float size, Color32 tone, bool glow, bool topAnchored = false)
    {
        GameObject diamondObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
        diamondObject.transform.SetParent(parent, false);
        RectTransform rect = diamondObject.GetComponent<RectTransform>();
        bool rootChild = IsCanvasRootChild(parent);
        rect.anchorMin = rootChild ? new Vector2(0f, 1f) : topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchorMax = rootChild ? new Vector2(0f, 1f) : topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(size, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        UnityEngine.UI.Image image = diamondObject.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color32(4, 13, 15, 230);
        image.raycastTarget = false;
        UnityEngine.UI.Outline outline = diamondObject.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = tone;
        outline.effectDistance = new Vector2(2f, -2f);

        if (glow)
        {
            GameObject glowObject = new GameObject(name + "Glow", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            glowObject.transform.SetParent(diamondObject.transform, false);
            RectTransform glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = Vector2.zero;
            glowRect.sizeDelta = new Vector2(size * 0.55f, size * 0.55f);
            UnityEngine.UI.Image glowImage = glowObject.GetComponent<UnityEngine.UI.Image>();
            glowImage.color = new Color32(tone.r, tone.g, tone.b, 110);
            glowImage.raycastTarget = false;
        }

        TextMeshProUGUI label = CreateCanvasLabel(diamondObject.transform, name + "Label", string.IsNullOrEmpty(text) ? " " : text, Vector2.zero, new Vector2(size * 0.85f, size * 0.45f), Mathf.Max(10f, size * 0.24f), tone, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
    }

    private void CreateDonutImage(Transform parent, string name, Vector2 anchoredPosition, float size, float percent, Color32 fillColor, Color32 trackColor)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(size, size);

        UnityEngine.UI.Image image = imageObject.GetComponent<UnityEngine.UI.Image>();
        image.sprite = CreateDonutSprite(Mathf.Clamp01(percent), fillColor, trackColor);
        image.raycastTarget = false;
    }

    private Sprite CreateDonutSprite(float percent, Color32 fillColor, Color32 trackColor)
    {
        int percentKey = Mathf.RoundToInt(Mathf.Clamp01(percent) * 256f);
        string key = percentKey + "_" + fillColor.r + "_" + fillColor.g + "_" + fillColor.b + "_" + fillColor.a + "_" + trackColor.r + "_" + trackColor.g + "_" + trackColor.b + "_" + trackColor.a;
        Sprite cachedSprite;
        if (donutSprites.TryGetValue(key, out cachedSprite))
            return cachedSprite;

        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "MetaHubRuntimeDonut";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float cachedPercent = percentKey / 256f;

        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float outer = textureSize * 0.47f;
        float inner = textureSize * 0.34f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 point = new Vector2(x, y);
                float distance = Vector2.Distance(point, center);
                if (distance < inner || distance > outer)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                Vector2 dir = point - center;
                float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                if (angle < 0f)
                    angle += 360f;

                texture.SetPixel(x, y, angle <= cachedPercent * 360f ? fillColor : trackColor);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        donutSprites[key] = sprite;
        return sprite;
    }

    private string ToReferenceNavLabel(string label)
    {
        switch (label)
        {
            case "TOWER MASTERY":
                return "TOWER MASTERY";
            case "UEBERSICHT":
                return "ÜBERSICHT";
            case "CHAOS-FORSCHUNG":
                return "CHAOS-\nFORSCHUNG";
            case "VERBAU / PFADTECHNIK":
                return "VERBAU /\nPFADTECHNIK";
            default:
                return label;
        }
    }

    private void CreateArtIcon(Transform parent, string name, string artName, Vector2 anchoredPosition, Vector2 size, bool topAnchored = false)
    {
        GameObject iconObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        iconObject.transform.SetParent(parent, false);

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        bool rootChild = IsCanvasRootChild(parent);
        rect.anchorMin = rootChild ? new Vector2(0f, 1f) : topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchorMax = rootChild ? new Vector2(0f, 1f) : topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        UnityEngine.UI.Image image = iconObject.GetComponent<UnityEngine.UI.Image>();
        image.sprite = LoadArtSprite(artName);
        image.type = UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private Sprite LoadArtSprite(string artName)
    {
        if (string.IsNullOrEmpty(artName))
            return null;

        Sprite cachedSprite;
        if (artSprites.TryGetValue(artName, out cachedSprite))
            return cachedSprite;

        Texture2D texture = Resources.Load<Texture2D>("MetaHubArt/" + artName);
        Sprite sprite = null;
        if (texture != null)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
        else
        {
            sprite = CreateFallbackIconSprite(artName);
        }

        artSprites[artName] = sprite;
        return sprite;
    }

    private Sprite CreateFallbackIconSprite(string artName)
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "MetaHubFallbackIcon_" + artName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32 tone = ToneColorFromArtName(artName);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float diamondRadius = size * 0.39f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);
                float distance = dx + dy;
                if (distance > diamondRadius + 5f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float normalized = Mathf.Clamp01(1f - distance / diamondRadius);
                float ring = Mathf.Abs(distance - diamondRadius);
                if (ring < 3f)
                {
                    texture.SetPixel(x, y, tone);
                    continue;
                }

                byte alpha = (byte)Mathf.Lerp(40f, 220f, normalized);
                byte red = (byte)Mathf.Clamp(tone.r * (0.25f + normalized * 0.75f), 0f, 255f);
                byte green = (byte)Mathf.Clamp(tone.g * (0.25f + normalized * 0.75f), 0f, 255f);
                byte blue = (byte)Mathf.Clamp(tone.b * (0.25f + normalized * 0.75f), 0f, 255f);
                texture.SetPixel(x, y, new Color32(red, green, blue, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Color32 ToneColorFromArtName(string artName)
    {
        if (artName.Contains("xp") || artName.Contains("blueprint") || artName.Contains("path") || artName.Contains("cyan"))
            return ToneColor(MetaHubTone.Cyan);
        if (artName.Contains("chaos") || artName.Contains("purple"))
            return ToneColor(MetaHubTone.Purple);
        if (artName.Contains("risk") || artName.Contains("skull") || artName.Contains("runner") || artName.Contains("spawn") || artName.Contains("red"))
            return ToneColor(MetaHubTone.Red);
        if (artName.Contains("buff") || artName.Contains("green"))
            return ToneColor(MetaHubTone.Green);

        return ToneColor(MetaHubTone.Gold);
    }

    private string GetResourceArtName(string id)
    {
        switch (id)
        {
            case "gold": return "icon_gold";
            case "xp": return "icon_xp";
            case "chaos": return "icon_chaos";
            case "special": return IsUnlockLayoutMode() ? "icon_skull" : "icon_keystone";
            default: return "icon_keystone";
        }
    }

    private string GetNavArtName(string id)
    {
        switch (id)
        {
            case "overview": return "icon_home";
            case "general": return "icon_shield";
            case "tower": return "icon_tower";
            case "chaos": return "icon_chaos";
            case "path": return "icon_path";
            case "elite": return "icon_skull";
            case "archive": return "icon_book";
            default: return "icon_shield";
        }
    }

    private string GetKeystoneArtName(string id)
    {
        if (id.Contains("red"))
            return "icon_keystone_red";
        if (id.Contains("purple") || id.Contains("violet"))
            return "icon_keystone_purple";
        if (id.Contains("blue") || id.Contains("cyan"))
            return "icon_keystone_cyan";

        return "icon_keystone";
    }

    private string GetMetricArtName(string id)
    {
        switch (id)
        {
            case "tower_mastery": return "icon_tower";
            case "chaos_wissen": return runtimeUnlockInfoMode ? "icon_shield" : "icon_chaos";
            case "risikokerne": return "icon_risk_core";
            case "bauplaene": return runtimeUnlockInfoMode ? "icon_path" : "icon_blueprint";
            case "elite_jagd": return runtimeUnlockInfoMode ? "icon_shield" : "icon_skull";
            default: return "icon_keystone";
        }
    }

    private string GetSideStatArtName(string id)
    {
        switch (id)
        {
            case "free_points": return "icon_keystone";
            case "chaos_level": return "icon_risk_core";
            case "gold_justice": return "icon_gold";
            case "xp_justice": return "icon_xp";
            default: return "icon_keystone";
        }
    }

    private string GetGoalArtName(MetaHubGoalData goal)
    {
        string key = ((goal != null ? goal.id + " " + goal.title : string.Empty) ?? string.Empty).ToLowerInvariant();
        if (key.Contains("tower"))
            return "icon_tower";
        if (key.Contains("chaos"))
            return "icon_chaos";
        if (key.Contains("risiko") || key.Contains("risk"))
            return "icon_risk_core";
        if (key.Contains("elite"))
            return "icon_skull";

        return "icon_keystone";
    }

    private string GetEffectArtName(MetaHubEffectData effect)
    {
        string key = ((effect != null ? effect.id + " " + effect.title : string.Empty) ?? string.Empty).ToLowerInvariant();
        if (key.Contains("xp"))
            return "icon_buff_xp";
        if (key.Contains("gold"))
            return "icon_buff_gold";
        if (key.Contains("runner"))
            return "icon_runner";
        if (key.Contains("spawn"))
            return "icon_spawn";
        if (key.Contains("risiko") || key.Contains("risk"))
            return "icon_shield";

        return "icon_buff_gold";
    }

    private void CreateResourceChip(Transform parent, MetaHubResourceData data, int index)
    {
        Transform chip = CreateCanvasPanel(parent, "Resource_" + data.id, new Vector2(-195f + index * 112f, 0f), new Vector2(98f, 42f), new Color32(8, 18, 24, 245), ToneColor(data.tone));
        CreateCanvasLabel(chip, "Icon", data.iconText, new Vector2(-30f, 0f), new Vector2(28f, 28f), 12f, ToneColor(data.tone), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(chip, "Value", MetaHubMockData.FormatNumber(data.value), new Vector2(20f, 4f), new Vector2(62f, 20f), 14f, new Color32(235, 220, 192, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(chip, "Label", data.label, new Vector2(20f, -12f), new Vector2(62f, 16f), 8f, new Color32(166, 174, 182, 255), TextAlignmentOptions.Left, FontStyles.Normal);
    }

    private void CreateNavRow(Transform parent, MetaHubNavItemData data, int index)
    {
        Color32 border = data.selected ? new Color32(224, 170, 55, 255) : ToneColor(data.tone);
        Color32 fill = data.selected ? new Color32(50, 36, 12, 255) : new Color32(3, 8, 11, 255);
        Transform row = CreateCanvasPanelTop(parent, "Nav_" + data.id, new Vector2(0f, -96f - index * 72f), new Vector2(240f, 62f), fill, border);
        CreateCanvasLabel(row, "Icon", data.iconText, new Vector2(-85f, 0f), new Vector2(42f, 42f), 24f, ToneColor(data.tone), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(row, "Label", data.label, new Vector2(24f, 0f), new Vector2(150f, 34f), 16f, new Color32(236, 228, 212, 255), TextAlignmentOptions.Left, FontStyles.Bold);
    }

    private void CreateKeystone(Transform parent, MetaHubKeystoneData data, int index)
    {
        Transform item = CreateCanvasPanelTop(parent, "Keystone_" + data.id, new Vector2(-76f + index * 76f, -700f), new Vector2(64f, 88f), new Color32(8, 13, 20, 255), ToneColor(data.tone));
        CreateCanvasLabel(item, "Icon", data.iconText, new Vector2(0f, 10f), new Vector2(42f, 42f), 20f, ToneColor(data.tone), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(item, "Level", "Lv. " + data.level, new Vector2(0f, -31f), new Vector2(54f, 18f), 11f, new Color32(232, 199, 146, 255), TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private void CreateMetricCard(Transform parent, MetaHubMetricCardData data, int index)
    {
        float x = 405f + index * 248f;
        Transform card = CreateCanvasPanel(parent, "Metric_" + data.id, new Vector2(x, -220f), new Vector2(224f, 142f), new Color32(8, 18, 25, 248), ToneColor(data.tone));
        CreateCanvasLabel(card, "Title", data.title, new Vector2(0f, 47f), new Vector2(190f, 22f), 15f, new Color32(230, 222, 208, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(card, "Icon", data.iconText, new Vector2(-55f, 4f), new Vector2(54f, 54f), 24f, ToneColor(data.tone), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(card, "Value", data.valueText, new Vector2(28f, 10f), new Vector2(80f, 32f), 26f, new Color32(240, 194, 116, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(card, "Caption", data.caption, new Vector2(40f, -18f), new Vector2(100f, 18f), 11f, new Color32(196, 184, 166, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (data.showProgress)
            CreateCanvasBar(card, "Progress", new Vector2(44f, -50f), new Vector2(118f, 5f), Percent(data.current, data.maximum), ToneColor(data.tone));
    }

    private void CreateProgressPanel(Transform parent, MetaHubData data)
    {
        Transform panel = CreateCanvasPanel(parent, "ProgressPanel", new Vector2(520f, -495f), new Vector2(420f, 280f), new Color32(8, 18, 25, 245), new Color32(90, 62, 28, 255));
        CreateCanvasLabel(panel, "Title", "FORTSCHRITT", new Vector2(-130f, 112f), new Vector2(150f, 28f), 18f, new Color32(220, 163, 68, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        Transform ring = CreateCanvasPanel(panel, "Ring", new Vector2(-105f, -10f), new Vector2(160f, 160f), new Color32(12, 16, 24, 255), new Color32(60, 100, 110, 255));
        CreateCanvasLabel(ring, "Level", data.account.level.ToString(), new Vector2(0f, 20f), new Vector2(100f, 48f), 40f, new Color32(238, 224, 204, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(ring, "Caption", "ACCOUNT LEVEL", new Vector2(0f, -26f), new Vector2(140f, 22f), 12f, new Color32(225, 183, 81, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasBar(ring, "AccountBar", new Vector2(0f, -68f), new Vector2(120f, 6f), Percent(data.account.currentXP, data.account.requiredXP), new Color32(48, 190, 214, 255));
        for (int i = 0; i < data.progressStats.Count; i++)
            CreateSideStat(panel, data.progressStats[i], i);
    }

    private void CreateSideStat(Transform parent, MetaHubSideStatData data, int index)
    {
        Transform row = CreateCanvasPanel(parent, "Stat_" + data.id, new Vector2(105f, 55f - index * 52f), new Vector2(170f, 42f), new Color32(10, 20, 26, 245), new Color32(45, 62, 70, 255));
        CreateCanvasLabel(row, "Label", data.label, new Vector2(-30f, 0f), new Vector2(105f, 20f), 12f, new Color32(220, 211, 195, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasLabel(row, "Value", data.valueText, new Vector2(66f, 0f), new Vector2(32f, 20f), 12f, ToneColor(data.tone), TextAlignmentOptions.Right, FontStyles.Bold);
    }

    private void CreateChaosPanel(Transform parent, MetaHubChaosJusticeData data)
    {
        Transform panel = CreateCanvasPanel(parent, "ChaosPanel", new Vector2(965f, -495f), new Vector2(448f, 280f), new Color32(8, 18, 25, 245), new Color32(90, 62, 28, 255));
        CreateCanvasLabel(panel, "Title", "CHAOS / GERECHTIGKEIT", new Vector2(-95f, 112f), new Vector2(250f, 28f), 18f, new Color32(220, 163, 68, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(panel, "Justice", "GERECHTIGKEIT", new Vector2(-112f, 55f), new Vector2(130f, 22f), 13f, new Color32(225, 183, 81, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "Chaos", "CHAOS", new Vector2(112f, 55f), new Vector2(130f, 22f), 13f, new Color32(255, 100, 90, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasBar(panel, "Balance", new Vector2(0f, 25f), new Vector2(290f, 12f), data.safetyPercent / 100f, new Color32(218, 160, 46, 255));
        CreateCanvasLabel(panel, "SafetyScore", "SAFETY SCORE\n" + data.safetyScore, new Vector2(-125f, -52f), new Vector2(140f, 70f), 16f, new Color32(240, 194, 116, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "BalanceValue", data.safetyPercent + "%\n" + data.stabilityLabel, new Vector2(0f, -52f), new Vector2(120f, 70f), 18f, new Color32(240, 194, 116, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "ChaosScore", "CHAOS SCORE\n" + data.chaosScore, new Vector2(125f, -52f), new Vector2(140f, 70f), 16f, new Color32(255, 100, 90, 255), TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private void CreateGoalPanel(Transform parent, List<MetaHubGoalData> goals)
    {
        Transform panel = CreateCanvasPanel(parent, "GoalPanel", new Vector2(1388f, -495f), new Vector2(302f, 280f), new Color32(8, 18, 25, 245), new Color32(90, 62, 28, 255));
        CreateCanvasLabel(panel, "Title", "NÄCHSTE ZIELE", new Vector2(-50f, 112f), new Vector2(190f, 28f), 18f, new Color32(220, 163, 68, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        for (int i = 0; i < goals.Count && i < 4; i++)
        {
            MetaHubGoalData goal = goals[i];
            CreateCanvasLabel(panel, "Goal" + i, goal.title, new Vector2(-30f, 62f - i * 42f), new Vector2(180f, 24f), 12f, new Color32(220, 211, 195, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            CreateCanvasLabel(panel, "GoalProgress" + i, goal.current + " / " + goal.required, new Vector2(105f, 62f - i * 42f), new Vector2(60f, 24f), 12f, new Color32(220, 211, 195, 255), TextAlignmentOptions.Right, FontStyles.Normal);
        }
        CreateCanvasButton(panel, "AllGoals", "ALLE ZIELE ANZEIGEN", new Vector2(0f, -112f), new Vector2(230f, 34f), RequestAllGoals);
    }

    private void CreateEffectPanel(Transform parent, string name, string title, List<MetaHubEffectData> effects, Vector2 position, Vector2 size, Color32 border)
    {
        Transform panel = CreateCanvasPanel(parent, name, position, size, new Color32(8, 18, 25, 245), border);
        CreateCanvasLabel(panel, "Title", title, new Vector2(-125f, 70f), new Vector2(180f, 28f), 18f, border, TextAlignmentOptions.Left, FontStyles.Bold);
        for (int i = 0; i < effects.Count && i < 2; i++)
        {
            MetaHubEffectData effect = effects[i];
            CreateCanvasLabel(panel, "Effect" + i, effect.title + "\n" + effect.description, new Vector2(-20f, 28f - i * 58f), new Vector2(260f, 44f), 12f, new Color32(220, 226, 216, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            CreateCanvasLabel(panel, "Duration" + i, effect.durationText, new Vector2(125f, 28f - i * 58f), new Vector2(70f, 28f), 12f, new Color32(235, 216, 185, 255), TextAlignmentOptions.Right, FontStyles.Normal);
        }
    }

    private void CreateRunPanel(Transform parent, List<MetaHubRunStatData> stats)
    {
        Transform panel = CreateCanvasPanel(parent, "RunPanel", new Vector2(1388f, -760f), new Vector2(302f, 190f), new Color32(8, 18, 25, 245), new Color32(95, 105, 120, 255));
        CreateCanvasLabel(panel, "Title", "LETZTER RUN", new Vector2(-70f, 70f), new Vector2(160f, 28f), 18f, new Color32(220, 211, 195, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        for (int i = 0; i < stats.Count && i < 4; i++)
        {
            CreateCanvasLabel(panel, "RunStat" + i, stats[i].label, new Vector2(-45f, 34f - i * 24f), new Vector2(150f, 20f), 12f, new Color32(190, 198, 205, 255), TextAlignmentOptions.Left, FontStyles.Normal);
            CreateCanvasLabel(panel, "RunValue" + i, stats[i].valueText, new Vector2(95f, 34f - i * 24f), new Vector2(70f, 20f), 12f, new Color32(240, 194, 116, 255), TextAlignmentOptions.Right, FontStyles.Bold);
        }
    }

    private Transform CreateCanvasGroup(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject groupObject = new GameObject(name, typeof(RectTransform));
        groupObject.transform.SetParent(parent, false);
        RectTransform rect = groupObject.GetComponent<RectTransform>();
        bool rootChild = IsCanvasRootChild(parent);
        rect.anchorMin = rootChild ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
        rect.anchorMax = rootChild ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return groupObject.transform;
    }

    private Transform CreateCanvasPanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color32 fill, Color32 border)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        bool rootChild = IsCanvasRootChild(parent);
        rect.anchorMin = rootChild ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchorMax = rootChild ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        UnityEngine.UI.Image image = panelObject.GetComponent<UnityEngine.UI.Image>();
        image.sprite = CreatePanelSprite(fill, border);
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.color = Color.white;
        return panelObject.transform;
    }

    private Sprite CreatePanelSprite(Color32 fill, Color32 border)
    {
        string key = fill.r + "_" + fill.g + "_" + fill.b + "_" + fill.a + "_" + border.r + "_" + border.g + "_" + border.b + "_" + border.a;
        Sprite cachedSprite;
        if (panelSprites.TryGetValue(key, out cachedSprite))
            return cachedSprite;

        const int textureSize = 64;
        const float radius = 8f;
        const int borderSize = 3;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "MetaHubPanel_" + key;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                if (!IsInsideRoundedRect(x, y, textureSize, radius))
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                bool borderPixel = x < borderSize || y < borderSize || x >= textureSize - borderSize || y >= textureSize - borderSize;
                float minX = Mathf.Min(x, textureSize - 1 - x);
                float minY = Mathf.Min(y, textureSize - 1 - y);
                if (minX < radius && minY < radius)
                {
                    float dx = radius - minX;
                    float dy = radius - minY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    borderPixel = borderPixel || Mathf.Abs(distance - radius) <= borderSize;
                }

                if (borderPixel)
                {
                    texture.SetPixel(x, y, border);
                    continue;
                }

                float vignette = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 46f);
                byte red = (byte)Mathf.Clamp(fill.r + vignette * 10f, 0f, 255f);
                byte green = (byte)Mathf.Clamp(fill.g + vignette * 10f, 0f, 255f);
                byte blue = (byte)Mathf.Clamp(fill.b + vignette * 10f, 0f, 255f);
                texture.SetPixel(x, y, new Color32(red, green, blue, fill.a));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
        panelSprites[key] = sprite;
        return sprite;
    }

    private bool IsInsideRoundedRect(int x, int y, int size, float radius)
    {
        float minX = Mathf.Min(x, size - 1 - x);
        float minY = Mathf.Min(y, size - 1 - y);
        if (minX >= radius || minY >= radius)
            return true;

        float dx = radius - minX;
        float dy = radius - minY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private Transform CreateCanvasPanelTop(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color32 fill, Color32 border)
    {
        Transform panel = CreateCanvasPanel(parent, name, anchoredPosition, size, fill, border);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        return panel;
    }

    private TextMeshProUGUI CreateCanvasLabel(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, float fontSize, Color32 color, TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        bool rootChild = IsCanvasRootChild(parent);
        rect.anchorMin = rootChild ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchorMax = rootChild ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = style;
        label.enableWordWrapping = !string.IsNullOrEmpty(text) && text.Contains("\n");
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private TextMeshProUGUI CreateCanvasLabelTop(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, float fontSize, Color32 color, TextAlignmentOptions alignment, FontStyles style)
    {
        TextMeshProUGUI label = CreateCanvasLabel(parent, name, text, anchoredPosition, size, fontSize, color, alignment, style);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        return label;
    }

    private void CreateCanvasBar(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float percent, Color32 fillColor)
    {
        Transform track = CreateCanvasPanel(parent, name + "Track", anchoredPosition, size, new Color32(20, 24, 28, 255), new Color32(85, 68, 42, 255));
        GameObject fillObject = new GameObject(name + "Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        fillObject.transform.SetParent(track, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(size.x * Mathf.Clamp01(percent), 0f);
        fillObject.GetComponent<UnityEngine.UI.Image>().color = fillColor;
    }

    private UnityEngine.UI.Button CreateCanvasButton(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Transform buttonTransform = CreateCanvasPanel(parent, name, anchoredPosition, size, new Color32(6, 11, 15, 255), new Color32(170, 113, 35, 255));
        UnityEngine.UI.Button button = buttonTransform.gameObject.AddComponent<UnityEngine.UI.Button>();
        if (onClick != null)
            button.onClick.AddListener(onClick);
        ApplyCanvasButtonColors(button, new Color32(6, 11, 15, 255), new Color32(170, 113, 35, 255));
        CreateCanvasLabel(buttonTransform, "Label", text, Vector2.zero, size, 14f, new Color32(236, 218, 188, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        return button;
    }

    private void ApplyCanvasButtonColors(UnityEngine.UI.Button button, Color baseColor, Color highlightColor)
    {
        if (button == null)
            return;

        UnityEngine.UI.Image image = button.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
            button.targetGraphic = image;

        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        UnityEngine.UI.Navigation navigation = button.navigation;
        navigation.mode = UnityEngine.UI.Navigation.Mode.None;
        button.navigation = navigation;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(Color.white, highlightColor, 0.22f);
        colors.pressedColor = Color.Lerp(baseColor, highlightColor, 0.45f);
        colors.selectedColor = Color.Lerp(Color.white, highlightColor, 0.18f);
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        colors.fadeDuration = 0.04f;
        button.colors = colors;
    }

    private void SetCanvasFallbackVisible(bool visible)
    {
        if (fallbackCanvas != null)
            fallbackCanvas.gameObject.SetActive(visible);

        if (fallbackRoot != null)
            fallbackRoot.SetActive(visible);

        if (fallbackCanvas != null || fallbackRoot != null)
            isVisible = visible;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private bool IsCanvasRootChild(Transform parent)
    {
        return (fallbackRoot != null && parent == fallbackRoot.transform) || (fallbackDesignRoot != null && parent == fallbackDesignRoot.transform);
    }

    private Color32 ToneColor(MetaHubTone tone)
    {
        switch (tone)
        {
            case MetaHubTone.Gold: return new Color32(230, 170, 55, 255);
            case MetaHubTone.Cyan: return new Color32(65, 200, 235, 255);
            case MetaHubTone.Purple: return new Color32(188, 105, 255, 255);
            case MetaHubTone.Red: return new Color32(255, 86, 78, 255);
            case MetaHubTone.Green: return new Color32(95, 220, 105, 255);
            case MetaHubTone.Blue: return new Color32(80, 180, 245, 255);
            default: return new Color32(165, 154, 137, 255);
        }
    }
}
