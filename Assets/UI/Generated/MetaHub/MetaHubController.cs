using System;
using System.Collections.Generic;
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
    private Canvas fallbackCanvas;
    private GameObject fallbackRoot;
    private GameObject fallbackDesignRoot;
    private string selectedNavigationId = "overview";
    private TowerRole selectedTowerRole = TowerRole.Basic;
    private bool showAllGoalsOverlay = false;
    private float liveRefreshTimer = 0f;
    private readonly Dictionary<string, Sprite> artSprites = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> panelSprites = new Dictionary<string, Sprite>();

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

        if (IsOpen && useLiveDataWhenAvailable && gameManager != null)
        {
            liveRefreshTimer -= Time.unscaledDeltaTime;
            if (liveRefreshTimer <= 0f)
            {
                liveRefreshTimer = LiveRefreshInterval;
                RefreshData();
            }
        }
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
        if (!isVisible || mainMenuUnlockMode != useMainMenuUnlockMode)
            selectedNavigationId = "overview";

        mainMenuUnlockMode = useMainMenuUnlockMode;

        if (sourceGameManager != null)
            gameManager = sourceGameManager;

        EnsureDocument();
        SetVisible(true);
        liveRefreshTimer = 0f;

        try
        {
            RefreshData();
        }
        catch (Exception exception)
        {
            Debug.LogError("MetaHubController: Live-Daten konnten nicht gebunden werden. MockData wird verwendet.\n" + exception);
            SetData(MetaHubMockData.Create());
        }

        if (useCanvasFallback)
        {
            SetVisible(false);
            ShowCanvasFallback(currentData);
        }
    }

    public void CloseFromUnlockManager()
    {
        owningUnlockManager = null;
        mainMenuUnlockMode = false;
        SetVisible(false);
        SetCanvasFallbackVisible(false);
    }

    public void SetData(MetaHubData data)
    {
        currentData = data != null ? data : MetaHubMockData.Create();
        ApplyMainMenuUnlockMode(currentData);
        ApplySelectedNavigation(currentData);
        EnsureDocument();
        Bind(currentData);

        if (useCanvasFallback && fallbackCanvas != null && fallbackCanvas.gameObject.activeSelf)
            RebuildCanvasFallback(currentData);
    }

    public void RefreshData()
    {
        ResolveGameManager();

        MetaHubData data = useLiveDataWhenAvailable && gameManager != null ? MetaHubMockData.CreateFromGame(gameManager) : MetaHubMockData.Create();
        SetData(data);
    }

    public void RequestClose()
    {
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
        Debug.Log("MetaHub: All goals button requested.");

        if (useLiveDataWhenAvailable)
            RefreshData();
        else
            SetData(currentData);
    }

    private void CloseAllGoalsOverlay()
    {
        showAllGoalsOverlay = false;

        if (useLiveDataWhenAvailable)
            RefreshData();
        else
            SetData(currentData);
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

        RegisterButton("RunStatsButton", delegate
        {
            if (RunStatisticsRequested != null)
                RunStatisticsRequested.Invoke();
            Debug.Log("MetaHub: Run statistics button requested.");
        });

        RegisterButton("OptionsButton", delegate
        {
            if (OptionsRequested != null)
                OptionsRequested.Invoke();
            Debug.Log("MetaHub: Options button requested.");
        });

        RegisterButton("MainMenuButton", RequestReturnToMainMenu);
        RegisterButton("BackButton", RequestClose);
        RegisterButton("CloseButton", RequestClose);
        RegisterButton("SettingsButton", delegate
        {
            if (OptionsRequested != null)
                OptionsRequested.Invoke();
            Debug.Log("MetaHub: Settings button requested.");
        });

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
        selectedNavigationId = string.IsNullOrEmpty(navId) ? "overview" : navId;
        showAllGoalsOverlay = false;

        if (useLiveDataWhenAvailable)
            RefreshData();
        else
            SetData(currentData);
    }

    private void ApplyMainMenuUnlockMode(MetaHubData data)
    {
        if (!mainMenuUnlockMode || data == null)
            return;

        data.screenSubtitle = "FREISCHALTUNGEN";
        data.footerTip = "Tipp: Permanente Freischaltungen staerken deinen naechsten Run.";
        ApplyMainMenuUnlockLiveValues(data);

        if (data.navigation == null)
            return;

        for (int i = data.navigation.Count - 1; i >= 0; i--)
        {
            MetaHubNavItemData item = data.navigation[i];
            if (item != null && item.id == "archive")
                data.navigation.RemoveAt(i);
        }
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

        screenRoot.EnableInClassList("main-menu-unlocks", mainMenuUnlockMode);

        SetText("ScreenTitle", data.screenTitle);
        SetText("SidebarTitle", data.screenSubtitle);
        SetText("SectionTitle", data.selectedSectionTitle);
        SetText("FooterTip", data.footerTip);

        SetText("AccountLevelTop", "Account Lv. " + data.account.level);
        SetText("AccountXpTop", MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP");
        SetText("AccountLevelCenter", data.account.level.ToString());
        SetText("AccountXpBottom", MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP");
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
            mainMenuButton.style.display = mainMenuUnlockMode ? DisplayStyle.None : DisplayStyle.Flex;
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

        container.Clear();

        for (int i = 0; i < resources.Count; i++)
        {
            MetaHubResourceData item = resources[i];
            VisualElement row = new VisualElement();
            row.AddToClassList("resource-chip");
            AddToneClass(row, item.tone);

            row.Add(CreateIcon(item.iconText, "resource-icon", item.tone));
            Label value = CreateLabel(MetaHubMockData.FormatNumber(item.value), "resource-value");
            row.Add(value);
            container.Add(row);
        }
    }

    private void BuildNavigation(List<MetaHubNavItemData> items)
    {
        VisualElement container = Query("NavList");
        if (container == null)
            return;

        container.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            MetaHubNavItemData item = items[i];
            VisualElement row = new VisualElement();
            row.AddToClassList("nav-row");
            AddToneClass(row, item.tone);

            if (item.selected)
                row.AddToClassList("selected");

            row.Add(CreateIcon(item.iconText, "nav-icon", item.tone));
            row.Add(CreateLabel(item.label, "nav-label"));
            container.Add(row);
        }
    }

    private void BuildMetricCards(List<MetaHubMetricCardData> cards)
    {
        VisualElement container = Query("MetricCardList");
        if (container == null)
            return;

        container.Clear();

        for (int i = 0; i < cards.Count; i++)
        {
            MetaHubMetricCardData item = cards[i];
            VisualElement card = new VisualElement();
            card.AddToClassList("metric-card");
            AddToneClass(card, item.tone);

            card.Add(CreateLabel(item.title, "metric-title"));

            VisualElement body = new VisualElement();
            body.AddToClassList("metric-body");
            body.Add(CreateIcon(item.iconText, "metric-icon", item.tone));

            VisualElement textBlock = new VisualElement();
            textBlock.AddToClassList("metric-text-block");
            textBlock.Add(CreateLabel(item.valueText, "metric-value"));
            textBlock.Add(CreateLabel(item.caption, "metric-caption"));
            body.Add(textBlock);
            card.Add(body);

            if (item.showProgress)
                card.Add(CreateProgressLine(item.current, item.maximum, item.tone));

            container.Add(card);
        }
    }

    private void BuildProgressStats(List<MetaHubSideStatData> stats)
    {
        VisualElement container = Query("ProgressStatList");
        if (container == null)
            return;

        container.Clear();

        for (int i = 0; i < stats.Count; i++)
        {
            MetaHubSideStatData item = stats[i];
            VisualElement row = new VisualElement();
            row.AddToClassList("side-stat-row");
            AddToneClass(row, item.tone);
            row.Add(CreateLabel(item.label, "side-stat-label"));
            row.Add(CreateLabel(item.valueText, "side-stat-value"));
            row.Add(CreateIcon(item.iconText, "side-stat-icon", item.tone));
            container.Add(row);
        }
    }

    private void BuildGoals(List<MetaHubGoalData> goals)
    {
        VisualElement container = Query("GoalList");
        if (container == null)
            return;

        container.Clear();

        for (int i = 0; i < goals.Count; i++)
        {
            MetaHubGoalData item = goals[i];
            VisualElement row = new VisualElement();
            row.AddToClassList("goal-row");
            AddToneClass(row, item.tone);
            row.Add(CreateIcon(item.iconText, "goal-icon", item.tone));
            row.Add(CreateLabel(item.title, "goal-title"));
            row.Add(CreateLabel(item.current + " / " + item.required, "goal-progress"));
            container.Add(row);
        }
    }

    private void BuildEffects(string containerName, List<MetaHubEffectData> effects)
    {
        VisualElement container = Query(containerName);
        if (container == null)
            return;

        container.Clear();

        for (int i = 0; i < effects.Count; i++)
        {
            MetaHubEffectData item = effects[i];
            VisualElement row = new VisualElement();
            row.AddToClassList("effect-row");
            AddToneClass(row, item.tone);
            row.Add(CreateIcon(item.iconText, "effect-icon", item.tone));

            VisualElement textBlock = new VisualElement();
            textBlock.AddToClassList("effect-text-block");
            textBlock.Add(CreateLabel(item.title, "effect-title"));
            textBlock.Add(CreateLabel(item.description, "effect-description"));
            row.Add(textBlock);

            if (!string.IsNullOrEmpty(item.durationText))
                row.Add(CreateLabel(item.durationText, "effect-duration"));

            container.Add(row);
        }
    }

    private void BuildKeystones(List<MetaHubKeystoneData> keystones)
    {
        VisualElement container = Query("KeystoneList");
        if (container == null)
            return;

        container.Clear();

        for (int i = 0; i < keystones.Count; i++)
        {
            MetaHubKeystoneData item = keystones[i];
            VisualElement column = new VisualElement();
            column.AddToClassList("keystone-item");
            AddToneClass(column, item.tone);
            column.Add(CreateIcon(item.iconText, "keystone-icon", item.tone));
            column.Add(CreateLabel("Lv. " + item.level, "keystone-level"));
            column.Add(CreateProgressLine(item.current, item.maximum, item.tone));
            container.Add(column);
        }
    }

    private void BuildRunStats(List<MetaHubRunStatData> stats)
    {
        VisualElement container = Query("LastRunList");
        if (container == null)
            return;

        container.Clear();

        for (int i = 0; i < stats.Count; i++)
        {
            MetaHubRunStatData item = stats[i];
            VisualElement row = new VisualElement();
            row.AddToClassList("run-stat-row");
            row.Add(CreateLabel(item.label, "run-stat-label"));
            row.Add(CreateLabel(item.valueText, "run-stat-value"));
            container.Add(row);
        }
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

        Transform root = fallbackDesignRoot != null ? fallbackDesignRoot.transform : fallbackRoot.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);

        CreateCanvasPanel(root, "TopBar", new Vector2(800f, -47f), new Vector2(1584f, 64f), new Color32(3, 12, 14, 245), new Color32(129, 83, 25, 255));
        CreateLine(root, "TopLeftOrnament", new Vector2(240f, -23f), new Vector2(465f, 1f), new Color32(116, 77, 24, 210));
        CreateLine(root, "TopRightOrnament", new Vector2(1322f, -23f), new Vector2(465f, 1f), new Color32(116, 77, 24, 210));
        TextMeshProUGUI title = CreateCanvasLabel(root, "Title", data.screenTitle, new Vector2(800f, -36f), new Vector2(540f, 48f), 34f, new Color32(244, 198, 98, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        title.characterSpacing = 9f;
        if (mainMenuUnlockMode)
            CreateCanvasLabel(root, "TopSubtitle", "FREISCHALTUNGEN - META-PROGRESSION", new Vector2(800f, -64f), new Vector2(430f, 18f), 11f, new Color32(225, 164, 54, 255), TextAlignmentOptions.Center, FontStyles.Normal);
        for (int i = 0; i < data.resources.Count && i < 4; i++)
            CreateReferenceResource(root, data.resources[i], i);

        CreateCanvasLabel(root, "AccountTop", "Account Lv. " + data.account.level, new Vector2(1188f, -46f), new Vector2(160f, 24f), 16f, new Color32(244, 226, 186, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasBar(root, "AccountXP", new Vector2(1312f, -47f), new Vector2(140f, 7f), Percent(data.account.currentXP, data.account.requiredXP), new Color32(150, 126, 255, 255));
        CreateCanvasLabel(root, "AccountXPText", MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP", new Vector2(1443f, -46f), new Vector2(125f, 23f), 14f, new Color32(244, 226, 186, 255), TextAlignmentOptions.Right, FontStyles.Normal);
        CreateCanvasButton(root, "SettingsButton", "O", new Vector2(1531f, -47f), new Vector2(34f, 42f), delegate { Debug.Log("MetaHub: Options button requested."); });
        CreateCanvasButton(root, "CloseTopButton", "X", new Vector2(1570f, -47f), new Vector2(34f, 42f), RequestClose);

        Transform sidebar = CreateOrnatePanel(root, "Sidebar", new Vector2(146f, -473f), new Vector2(274f, 774f), new Color32(5, 17, 18, 246), new Color32(143, 99, 41, 255));
        TextMeshProUGUI sidebarTitle = CreateCanvasLabelTop(sidebar, "SidebarTitle", data.screenSubtitle, new Vector2(0f, -35f), new Vector2(268f, 42f), mainMenuUnlockMode ? 18f : 21f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        sidebarTitle.characterSpacing = mainMenuUnlockMode ? 0.5f : 2f;
        for (int i = 0; i < data.navigation.Count; i++)
            CreateReferenceNavRow(sidebar, data.navigation[i], i);
        CreateCanvasLabelTop(sidebar, "KeystoneTitle", "AKTIVE KEYSTONES", new Vector2(-5f, -525f), new Vector2(238f, 30f), 17f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        for (int i = 0; i < data.activeKeystones.Count && i < 3; i++)
            CreateReferenceKeystone(sidebar, data.activeKeystones[i], i);

        Transform main = CreateOrnatePanel(root, "MainFrame", new Vector2(885f, -473f), new Vector2(1216f, 774f), new Color32(7, 18, 19, 241), new Color32(104, 82, 45, 255));
        CreateCanvasLabelTop(main, "SectionTitle", data.selectedSectionTitle, new Vector2(-300f, -58f), new Vector2(560f, 32f), 21f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);

        if (mainMenuUnlockMode)
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
            CreateReferenceEffectPanel(main, "RiskPanel", "AKTIVE RISIKEN", data.activeRisks, new Vector2(60f, -270f), new Vector2(450f, 192f), new Color32(167, 54, 45, 255));
            CreateReferenceRunPanel(main, data.lastRunStats);
        }

        if (showAllGoalsOverlay)
            CreateAllGoalsOverlay(main, data.nextGoals);

        CreateCanvasLabel(root, "Tip", "<> " + data.footerTip, new Vector2(375f, -907f), new Vector2(720f, 28f), 14f, new Color32(217, 194, 159, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (mainMenuUnlockMode)
        {
            CreateCanvasButton(root, "OptionsButton", "OPTIONEN", new Vector2(1300f, -942f), new Vector2(170f, 42f), delegate { Debug.Log("MetaHub: Options button requested."); });
            CreateCanvasButton(root, "BackButton", "< ZURÜCK", new Vector2(1510f, -942f), new Vector2(170f, 42f), RequestClose);
        }
        else
        {
            CreateCanvasButton(root, "OptionsButton", "OPTIONEN", new Vector2(1135f, -942f), new Vector2(160f, 42f), delegate { Debug.Log("MetaHub: Options button requested."); });
            CreateCanvasButton(root, "MainMenuButton", "HAUPTMENÜ", new Vector2(1325f, -942f), new Vector2(180f, 42f), RequestReturnToMainMenu);
            CreateCanvasButton(root, "BackButton", "< ZURÜCK", new Vector2(1512f, -942f), new Vector2(160f, 42f), RequestClose);
        }
    }

    private void CreateMainMenuUnlockContent(Transform main, MetaHubData data)
    {
        switch (selectedNavigationId)
        {
            case "general":
                CreateUnlockGeneralContent(main, data);
                break;
            case "tower":
                CreateUnlockTowerContent(main, data);
                break;
            case "chaos":
                CreateUnlockChaosContent(main, data);
                break;
            case "path":
                CreateUnlockPathContent(main, data);
                break;
            case "elite":
                CreateUnlockEliteContent(main, data);
                break;
            default:
                CreateUnlockOverviewContent(main, data);
                break;
        }
    }

    private void CreateUnlockOverviewContent(Transform main, MetaHubData data)
    {
        for (int i = 0; i < data.metricCards.Count && i < 5; i++)
            CreateReferenceMetricCard(main, data.metricCards[i], i);

        CreateReferenceProgressPanel(main, data);
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(285f, -10f), new Vector2(650f, 296f));
        CreateReferenceEffectPanel(main, "BuffPanel", "AKTIVE BONI", data.activeBuffs, new Vector2(-230f, -270f), new Vector2(700f, 192f), new Color32(64, 156, 72, 255));
        CreateReferenceRunPanel(main, data.lastRunStats);
    }

    private void CreateUnlockGeneralContent(Transform main, MetaHubData data)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();

        Transform account = CreateUnlockPanel(main, "GeneralAccount", "ACCOUNT", new Vector2(-438f, 210f), new Vector2(320f, 140f), new Color32(126, 98, 51, 255));
        CreateDonutImage(account, "AccountRing", new Vector2(-108f, -13f), 70f, Percent(data.account.currentXP, data.account.requiredXP), new Color32(55, 209, 235, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(account, "Level", data.account.level.ToString(), new Vector2(-108f, -7f), new Vector2(58f, 28f), 22f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(account, "LevelCaption", "ACCOUNT LEVEL", new Vector2(-108f, -34f), new Vector2(104f, 18f), 7.5f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(account, "AccountXP", MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP", new Vector2(66f, 30f), new Vector2(160f, 18f), 11f, new Color32(224, 210, 184, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasBar(account, "AccountBar", new Vector2(70f, 10f), new Vector2(142f, 6f), Percent(data.account.currentXP, data.account.requiredXP), new Color32(55, 209, 235, 255));
        string accountInfo = "Kernwissen  " + (general != null ? MetaHubMockData.FormatNumber(general.kernwissen) : "0") +
            "\nLoadout  " + (general != null ? general.GetUsedLoadoutSlots() + " / " + general.GetLoadoutSlotCapacity() : "0 / 0");
        CreateCanvasLabel(account, "Bonus", accountInfo, new Vector2(82f, -34f), new Vector2(170f, 38f), 10f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Normal);

        Transform loadout = CreateUnlockPanel(main, "Loadout", "LOADOUT", new Vector2(-84f, 210f), new Vector2(330f, 140f), new Color32(89, 68, 41, 255));
        CreateUnlockRow(loadout, "Loadout1", "icon_gold", "Slots belegt", "Aktive Meta-Boni", general != null ? general.GetUsedLoadoutSlots() + " / " + general.GetLoadoutSlotCapacity() : "0 / 0", MetaHubTone.Gold, 12f, 285f);
        CreateUnlockRow(loadout, "Loadout2", "icon_tower", "Freie Slots", "Verfuegbar", general != null ? general.GetAvailableLoadoutSlots().ToString() : "0", MetaHubTone.Green, -42f, 285f);

        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(380f, 210f), new Vector2(360f, 160f));

        Transform tree = CreateUnlockPanel(main, "GeneralSkillTree", "FREISCHALTBAUM", new Vector2(0f, -110f), new Vector2(1040f, 450f), new Color32(126, 98, 51, 255));
        CreateGeneralSkillBranch(tree, "GeneralBranchTower", "TOWER", new GeneralMetaCategory[] { GeneralMetaCategory.TowerUnlock }, 118f, MetaHubTone.Purple, 5,
            "general.tower.basic", "general.tower.rapid", "general.tower.heavy", "general.tower.slow", "general.tower.fire");
        CreateGeneralSkillBranch(tree, "GeneralBranchTile", "TILES", new GeneralMetaCategory[] { GeneralMetaCategory.TileUnlock }, 34f, MetaHubTone.Cyan, 5,
            "general.tile.path", "general.tile.gold", "general.tile.slow", "general.tile.trap", "general.tile.knock");
        CreateGeneralSkillBranch(tree, "GeneralBranchQol", "KOMFORT", new GeneralMetaCategory[] { GeneralMetaCategory.QoL }, -50f, MetaHubTone.Blue, 5,
            "general.qol.speed_fast", "general.qol.preview_roles_1", "general.qol.goal_pin_1", "general.qol.preview_boss", "general.qol.preview_chaos_1");
        CreateGeneralSkillBranch(tree, "GeneralBranchStart", "START / LOADOUT", new GeneralMetaCategory[] { GeneralMetaCategory.StartOption, GeneralMetaCategory.MetaLoadout }, -134f, MetaHubTone.Gold, 5,
            "general.start.gold_1", "general.start.life_1", "general.start.path_1", "general.loadout.slot_4", "general.start.discount_1");
    }

    private void CreateUnlockTowerContent(Transform main, MetaHubData data)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerRole selectedRole = selectedTowerRole;
        TowerMasteryRoleProfile selectedProfile = towerMastery != null ? towerMastery.GetProfile(selectedRole) : null;
        int masteryLevel = towerMastery != null ? towerMastery.GetMasteryLevel(selectedRole) : 1;
        int masteryCurrentXP = towerMastery != null ? towerMastery.GetMasteryXPIntoCurrentLevel(selectedRole) : 0;
        int masteryRequiredXP = towerMastery != null ? towerMastery.GetXPToNextMasteryLevel(masteryLevel) : 1;

        Transform list = CreateUnlockPanel(main, "TowerList", "TÜRME", new Vector2(-500f, 25f), new Vector2(165f, 525f), new Color32(126, 98, 51, 255));
        TowerRole[] roles = TowerMasteryManager.GetOrderedTowerRoles();
        for (int i = 0; i < roles.Length && i < 9; i++)
        {
            TowerRole role = roles[i];
            int roleLevel = towerMastery != null ? towerMastery.GetMasteryLevel(role) : 1;
            TowerRole capturedRole = role;
            CreateTowerListRow(list, role.ToString(), Shorten(TowerMasteryManager.GetTowerDisplayName(role).Replace(" Tower", ""), 12), "Mastery " + roleLevel, role == selectedRole, 190f - i * 47f, TowerTone(role), delegate { SelectTowerRole(capturedRole); });
        }

        Transform header = CreateUnlockPanel(main, "TowerHeader", TowerMasteryManager.GetTowerDisplayName(selectedRole).ToUpperInvariant() + " MASTERY", new Vector2(-145f, 210f), new Vector2(500f, 145f), new Color32(128, 83, 176, 255));
        CreateArtIcon(header, "TowerIcon", "icon_tower", new Vector2(-175f, -4f), new Vector2(92f, 92f));
        CreateCanvasLabel(header, "LevelLabel", "MASTERY LEVEL", new Vector2(-40f, 30f), new Vector2(160f, 20f), 12f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasLabel(header, "Level", masteryLevel.ToString(), new Vector2(-80f, -2f), new Vector2(60f, 42f), 34f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(header, "FreePoints", "FREIE PUNKTE  " + (selectedProfile != null ? selectedProfile.unspentPoints.ToString() : "0"), new Vector2(150f, 24f), new Vector2(180f, 22f), 12f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasBar(header, "TowerXP", new Vector2(4f, -45f), new Vector2(250f, 6f), Percent(masteryCurrentXP, masteryRequiredXP), new Color32(176, 98, 255, 255));
        CreateCanvasLabel(header, "TowerXPText", MetaHubMockData.FormatNumber(masteryCurrentXP) + " / " + MetaHubMockData.FormatNumber(masteryRequiredXP) + " XP", new Vector2(6f, -25f), new Vector2(250f, 20f), 11f, new Color32(235, 211, 169, 255), TextAlignmentOptions.Left, FontStyles.Normal);

        Transform tree = CreateUnlockPanel(main, "TowerTree", "MEILENSTEINE", new Vector2(-145f, -20f), new Vector2(500f, 270f), new Color32(126, 98, 51, 255));
        CreateTowerMilestoneNode(tree, "Node1", towerMastery, selectedRole, selectedProfile, TowerMasteryMilestone.I, "Meilenstein I", "icon_shield", MetaHubTone.Gold, new Vector2(-165f, 60f));
        CreateTowerMilestoneNode(tree, "Node2", towerMastery, selectedRole, selectedProfile, TowerMasteryMilestone.II, "Meilenstein II", "icon_blueprint", MetaHubTone.Cyan, new Vector2(0f, 60f));
        CreateTowerMilestoneNode(tree, "Node3", towerMastery, selectedRole, selectedProfile, TowerMasteryMilestone.III, "Meilenstein III", "icon_risk_core", MetaHubTone.Red, new Vector2(165f, 60f));
        CreateTowerMilestoneNode(tree, "Node4", towerMastery, selectedRole, selectedProfile, TowerMasteryMilestone.IV, "Meilenstein IV", "icon_gold", MetaHubTone.Gold, new Vector2(-165f, -25f));
        CreateTowerMilestoneNode(tree, "Node5", towerMastery, selectedRole, selectedProfile, TowerMasteryMilestone.V, "Meilenstein V", "icon_shield", MetaHubTone.Neutral, new Vector2(0f, -25f));
        CreateUnlockNode(tree, "Node6", "Bester Tower", selectedProfile != null ? "Lv. " + selectedProfile.bestLevelEver : "Lv. 1", "icon_tower", MetaHubTone.Purple, new Vector2(165f, -25f));
        CreateUnlockNode(tree, "Node7", "Punkte frei", selectedProfile != null ? selectedProfile.unspentPoints.ToString() : "0", "icon_gold", MetaHubTone.Gold, new Vector2(-165f, -110f));
        CreateUnlockNode(tree, "Node8", "Punkte gesetzt", selectedProfile != null ? selectedProfile.spentPoints.ToString() : "0", "icon_shield", MetaHubTone.Neutral, new Vector2(0f, -110f));
        CreateUnlockNode(tree, "Node9", "Letzter Run", selectedProfile != null ? "+" + selectedProfile.lastRunMasteryXPGained + " XP" : "+0 XP", "icon_blue_star", MetaHubTone.Cyan, new Vector2(165f, -110f));

        Transform keystones = CreateUnlockPanel(main, "TowerKeystones", "KEYSTONES", new Vector2(-145f, -270f), new Vector2(500f, 155f), new Color32(128, 83, 176, 255));
        string activeKeystone = selectedProfile != null && !string.IsNullOrEmpty(selectedProfile.activeKeystoneId) ? selectedProfile.activeKeystoneId : "Kein Keystone";
        CreateUnlockKeystoneCard(keystones, "Aktiv", Shorten(activeKeystone, 18), "icon_keystone_purple", MetaHubTone.Purple, new Vector2(-165f, -15f));
        CreateUnlockKeystoneCard(keystones, "Milestone IV", towerMastery != null && towerMastery.IsMilestoneUnlocked(selectedRole, TowerMasteryMilestone.IV) ? "FREI" : "GESPERRT", "icon_shield", MetaHubTone.Neutral, new Vector2(0f, -15f));
        CreateUnlockKeystoneCard(keystones, "Milestone V", towerMastery != null && towerMastery.IsMilestoneUnlocked(selectedRole, TowerMasteryMilestone.V) ? "FREI" : "GESPERRT", "icon_shield", MetaHubTone.Neutral, new Vector2(165f, -15f));

        Transform detail = CreateUnlockPanel(main, "TowerDetail", "MASTERY-STATUS", new Vector2(380f, -40f), new Vector2(330f, 555f), new Color32(128, 83, 176, 255));
        CreateArtIcon(detail, "DetailIcon", "icon_blueprint", new Vector2(-105f, 170f), new Vector2(74f, 74f));
        CreateCanvasLabel(detail, "DetailRank", "VISUAL TIER " + (towerMastery != null ? towerMastery.GetMasteryVisualTier(selectedRole).ToString() : "0"), new Vector2(46f, 192f), new Vector2(210f, 22f), 13f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(detail, "DetailText", "Echte Werte der ausgewaehlten Tower-Rolle.", new Vector2(50f, 135f), new Vector2(220f, 50f), 11f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateUnlockRow(detail, "NextRank", "icon_gold", "Gesamt-XP", "Mastery-Profil", selectedProfile != null ? MetaHubMockData.FormatNumber(selectedProfile.masteryXP) : "0", MetaHubTone.Gold, 52f, 280f);
        CreateUnlockRow(detail, "Req1", "icon_shield", "Letzter Run", "Punkte / XP", selectedProfile != null ? "+" + selectedProfile.lastRunMasteryPointsGained + " / +" + selectedProfile.lastRunMasteryXPGained : "+0 / +0", MetaHubTone.Green, -18f, 280f);
        CreateUnlockRow(detail, "Req2", "icon_keystone", "Freie Punkte", "Aktuell verfuegbar", selectedProfile != null ? selectedProfile.unspentPoints.ToString() : "0", MetaHubTone.Purple, -88f, 280f);
        CreateCanvasButton(detail, "SpendPoint", "PUNKT VERWENDEN   1", new Vector2(0f, -225f), new Vector2(250f, 42f), TrySpendSelectedTowerPoint);
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
        selectedTowerRole = role;
        RefreshData();
    }

    private void TrySpendSelectedTowerPoint()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null && towerMastery.TrySpendRolePoints(selectedTowerRole, 1))
            RefreshData();
    }

    private void HandleGeneralNodeClick(string nodeId)
    {
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        GeneralMetaNodeState state = general != null ? general.GetNodeState(nodeId) : null;
        if (general == null || definition == null || state == null)
            return;

        bool changed;
        if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = general.TryDeactivateNode(nodeId);
        else if (general.CanActivateNode(nodeId))
            changed = general.TryActivateNode(nodeId);
        else
            changed = general.TryPurchaseNode(nodeId);

        if (changed)
            RefreshData();
    }

    private void HandleChaosNodeClick(string nodeId)
    {
        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        ChaosResearchNodeDefinition definition = chaos != null ? chaos.GetDefinition(nodeId) : null;
        ChaosResearchNodeState state = chaos != null ? chaos.GetNodeState(nodeId) : null;
        if (chaos == null || definition == null || state == null)
            return;

        bool changed;
        if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = chaos.TryDeactivateNode(nodeId);
        else if (chaos.CanActivateNode(nodeId))
            changed = chaos.TryActivateNode(nodeId);
        else
            changed = chaos.TryPurchaseNode(nodeId);

        if (changed)
            RefreshData();
    }

    private void HandlePathNodeClick(string nodeId)
    {
        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        PathTechniqueNodeDefinition definition = path != null ? path.GetDefinition(nodeId) : null;
        PathTechniqueNodeState state = path != null ? path.GetNodeState(nodeId) : null;
        if (path == null || definition == null || state == null)
            return;

        bool changed;
        if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = path.TryDeactivateNode(nodeId);
        else if (path.CanActivateNode(nodeId))
            changed = path.TryActivateNode(nodeId);
        else
            changed = path.TryPurchaseNode(nodeId);

        if (changed)
            RefreshData();
    }

    private void HandleEliteNodeClick(string nodeId)
    {
        EliteHuntProgressionManager elite = GetEliteHuntManager();
        EliteHuntNodeDefinition definition = elite != null ? elite.GetDefinition(nodeId) : null;
        EliteHuntNodeState state = elite != null ? elite.GetNodeState(nodeId) : null;
        if (elite == null || definition == null || state == null)
            return;

        bool changed;
        if (elite.IsHuntModeNode(nodeId) && elite.IsNodeActive(nodeId))
            changed = elite.TryDeactivateHuntMode();
        else if (elite.CanActivateHuntModeNode(nodeId))
            changed = elite.TryActivateHuntModeNode(nodeId);
        else if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
            changed = elite.TryDeactivateNode(nodeId);
        else if (elite.CanActivateNode(nodeId))
            changed = elite.TryActivateNode(nodeId);
        else
            changed = elite.TryPurchaseNode(nodeId);

        if (changed)
            RefreshData();
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
        GeneralMetaProgressionManager general = GetGeneralMetaManager();
        GeneralMetaNodeDefinition definition = general != null ? general.GetDefinition(nodeId) : null;
        return definition != null ? definition.cost.ToString() : "0";
    }

    private string GeneralNodeStateLabel(string nodeId)
    {
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
        EliteHuntProgressionManager elite = GetEliteHuntManager();
        if (elite == null)
            return fallback;
        return elite.IsNodeActive(nodeId) || elite.IsNodePurchased(nodeId) || elite.CanPurchaseNode(nodeId) ? fallback : MetaHubTone.Neutral;
    }

    private bool CreateChaosSkillTreeLayout(Transform main, MetaHubData data)
    {
        ChaosResearchProgressionManager chaos = GetChaosResearchManager();
        int riskPurchased = chaos != null ? chaos.GetPurchasedCount(ChaosResearchCategory.RiskPool) : 0;
        int riskTotal = chaos != null ? Mathf.Max(1, chaos.GetDefinitionCount(ChaosResearchCategory.RiskPool)) : 1;
        int variantsPurchased = chaos != null ? chaos.GetPurchasedCount(ChaosResearchCategory.ChaosVariants) : 0;
        int variantsTotal = chaos != null ? Mathf.Max(1, chaos.GetDefinitionCount(ChaosResearchCategory.ChaosVariants)) : 1;

        CreateUnlockStatCard(main, "ChaosKnowledge", "CHAOS-WISSEN", chaos != null ? MetaHubMockData.FormatNumber(chaos.chaosKnowledge) : GetResourceValue(data, "chaos").ToString(), "Forschung", "icon_chaos", MetaHubTone.Purple, new Vector2(-450f, 210f), new Vector2(225f, 135f));
        CreateUnlockStatCard(main, "RiskCore", "RISIKOKERNE", chaos != null ? MetaHubMockData.FormatNumber(chaos.riftCores) : GetMetricValue(data, "risikokerne", "0"), "Endgame-Kerne", "icon_risk_core", MetaHubTone.Red, new Vector2(-205f, 210f), new Vector2(225f, 135f));

        Transform summary = CreateUnlockPanel(main, "ChaosSummary", "CHAOS-FORTSCHRITT", new Vector2(64f, 210f), new Vector2(285f, 135f), new Color32(128, 83, 176, 255));
        CreateUnlockRow(summary, "SummaryVariants", "icon_chaos", "Varianten", "Erforscht", variantsPurchased + " / " + variantsTotal, MetaHubTone.Purple, 18f, 240f);
        CreateUnlockRow(summary, "SummaryRisk", "icon_risk_core", "Risiko-Pool", "Freigeschaltet", riskPurchased + " / " + riskTotal, MetaHubTone.Red, -38f, 240f);
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(410f, 205f), new Vector2(320f, 160f));

        Transform tree = CreateUnlockPanel(main, "ChaosSkillTree", "FORSCHUNGSBAUM", new Vector2(-125f, -110f), new Vector2(760f, 455f), new Color32(128, 83, 176, 255));
        CreateChaosSkillBranch(tree, "ChaosRiskBranch", "RISIKO-POOL", ChaosResearchCategory.RiskPool, 128f, MetaHubTone.Purple, 4);
        CreateChaosSkillBranch(tree, "ChaosVariantBranch", "VARIANTEN", ChaosResearchCategory.ChaosVariants, 40f, MetaHubTone.Red, 4);
        CreateChaosSkillBranch(tree, "ChaosCounterBranch", "KONTER", ChaosResearchCategory.ChaosCounters, -48f, MetaHubTone.Cyan, 4);
        CreateChaosSkillBranch(tree, "ChaosWaveBranch", "WAVES", ChaosResearchCategory.ChaosWaves, -136f, MetaHubTone.Red, 4);

        Transform progress = CreateUnlockPanel(main, "ChaosProgress", "STATUS", new Vector2(420f, -110f), new Vector2(310f, 455f), new Color32(128, 83, 176, 255));
        int highestWave = chaos != null ? Mathf.Max(0, chaos.highestWaveEver) : 0;
        CreateDonutImage(progress, "WaveRing", new Vector2(-84f, 120f), 92f, Percent(highestWave, 30), new Color32(255, 84, 78, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(progress, "WaveValue", highestWave.ToString(), new Vector2(-84f, 128f), new Vector2(70f, 34f), 28f, new Color32(255, 230, 180, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(progress, "WaveCaption", "HOECHSTE WAVE", new Vector2(-84f, 84f), new Vector2(116f, 18f), 9f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateUnlockRow(progress, "Progress1", "icon_chaos", "Letzter Run", "Chaos-Wissen", chaos != null ? "+" + chaos.lastRunChaosKnowledgeGained : "+0", MetaHubTone.Purple, 32f, 255f);
        CreateUnlockRow(progress, "Progress2", "icon_risk_core", "Risskerne", "Letzter Run", chaos != null ? "+" + chaos.lastRunRiftCoresGained : "+0", MetaHubTone.Red, -26f, 255f);
        CreateUnlockRow(progress, "Progress3", "icon_chaos_sun", "Chaos-Waves", "Gesamt", chaos != null ? chaos.totalChaosWavesCompletedEver.ToString() : "0", MetaHubTone.Red, -84f, 255f);
        CreateCanvasButton(progress, "AllChaosGoals", "ALLE ZIELE ANZEIGEN", new Vector2(0f, -178f), new Vector2(240f, 36f), RequestAllGoals);
        return true;
    }

    private bool CreatePathSkillTreeLayout(Transform main, MetaHubData data)
    {
        PathTechniqueProgressionManager path = GetPathTechniqueManager();
        int pathLevel = path != null ? path.pathTechniqueLevel : 1;
        int pathCurrentXP = path != null ? path.GetXPIntoCurrentLevel() : 0;
        int pathRequiredXP = path != null ? path.GetXPToNextPathTechniqueLevel() : 1;

        Transform header = CreateUnlockPanel(main, "PathHeader", "VERBAU / PFADTECHNIK", new Vector2(-210f, 210f), new Vector2(545f, 135f), new Color32(47, 169, 210, 255));
        CreateArtIcon(header, "PathIcon", "icon_path", new Vector2(-210f, -2f), new Vector2(74f, 74f));
        CreateDonutImage(header, "PathRing", new Vector2(-115f, -2f), 82f, Percent(pathCurrentXP, pathRequiredXP), new Color32(55, 209, 235, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(header, "PathLevel", pathLevel.ToString(), new Vector2(-115f, 5f), new Vector2(64f, 32f), 26f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(header, "PathXP", MetaHubMockData.FormatNumber(pathCurrentXP) + " / " + MetaHubMockData.FormatNumber(pathRequiredXP) + " XP", new Vector2(90f, 28f), new Vector2(235f, 20f), 12f, new Color32(244, 226, 186, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        CreateCanvasBar(header, "PathBar", new Vector2(94f, 6f), new Vector2(230f, 6f), Percent(pathCurrentXP, pathRequiredXP), new Color32(55, 209, 235, 255));
        string pathInfo = "Blueprints " + (path != null ? path.blueprints.ToString() : "0") + " / Rissbauplaene " + (path != null ? path.riftBlueprints.ToString() : "0") +
            "\nSlots " + (path != null ? path.GetUsedLoadoutSlots() + " / " + path.GetLoadoutSlotCapacity() : "0 / 0");
        CreateCanvasLabel(header, "PathInfo", pathInfo, new Vector2(112f, -36f), new Vector2(260f, 36f), 10f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Left, FontStyles.Normal);

        CreateUnlockStatCard(main, "Blueprints", "BAUPLAENE", path != null ? MetaHubMockData.FormatNumber(path.blueprints) : GetMetricValue(data, "bauplaene", "0"), "Aktuell", "icon_blueprint", MetaHubTone.Blue, new Vector2(175f, 210f), new Vector2(180f, 135f));
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(440f, 205f), new Vector2(280f, 160f));

        Transform tree = CreateUnlockPanel(main, "PathSkillTree", "PFADTECHNIK-BAUM", new Vector2(-130f, -110f), new Vector2(760f, 455f), new Color32(47, 169, 210, 255));
        CreatePathSkillBranch(tree, "PathEventBranch", "EVENT-POOL", PathTechniqueCategory.EventPool, 128f, MetaHubTone.Cyan, 4);
        CreatePathSkillBranch(tree, "PathRescueBranch", "RETTUNG", PathTechniqueCategory.RescuePower, 40f, MetaHubTone.Gold, 4);
        CreatePathSkillBranch(tree, "PathToolsBranch", "WERKZEUGE", PathTechniqueCategory.PathTools, -48f, MetaHubTone.Cyan, 4);
        CreatePathSkillBranch(tree, "PathTileBranch", "TILE-TECHNIK", PathTechniqueCategory.TileTechnique, -136f, MetaHubTone.Blue, 4);

        Transform status = CreateUnlockPanel(main, "PathStatus", "LETZTE RETTUNGEN", new Vector2(420f, -110f), new Vector2(310f, 455f), new Color32(47, 169, 210, 255));
        CreateUnlockRow(status, "PathLast1", "icon_path", "Pfad-XP", "Letzter Run", path != null ? "+" + path.lastRunPathTechniqueXPGained : "+0", MetaHubTone.Cyan, 112f, 255f);
        CreateUnlockRow(status, "PathLast2", "icon_blueprint", "Bauplaene", "Letzter Run", path != null ? "+" + path.lastRunBlueprintsGained : "+0", MetaHubTone.Blue, 54f, 255f);
        CreateUnlockRow(status, "PathLast3", "icon_path", "Rissbauplaene", "Letzter Run", path != null ? "+" + path.lastRunRiftBlueprintsGained : "+0", MetaHubTone.Cyan, -4f, 255f);
        CreateUnlockRow(status, "PathLast4", "icon_gold", "Hoechste Wave", "Meta-Fortschritt", path != null ? path.highestWaveEver.ToString() : "0", MetaHubTone.Gold, -62f, 255f);
        CreateCanvasButton(status, "AllPathGoals", "ALLE ZIELE ANZEIGEN", new Vector2(0f, -178f), new Vector2(240f, 36f), RequestAllGoals);
        return true;
    }

    private bool CreateEliteSkillTreeLayout(Transform main, MetaHubData data)
    {
        EliteHuntProgressionManager elite = GetEliteHuntManager();

        CreateUnlockStatCard(main, "EliteSeals", "ELITE-SIEGEL", elite != null ? MetaHubMockData.FormatNumber(elite.eliteSeals) : GetResourceValue(data, "special").ToString(), "Waehrung", "icon_skull", MetaHubTone.Red, new Vector2(-450f, 210f), new Vector2(225f, 135f));
        CreateUnlockStatCard(main, "EliteRank", "ELITE-RANG", elite != null ? elite.eliteRank.ToString() : "1", elite != null ? EliteHuntProgressionManager.GetHuntModeDisplayName(elite.activeHuntMode) : "Aus", "icon_risk_core", MetaHubTone.Red, new Vector2(-205f, 210f), new Vector2(225f, 135f));
        CreateUnlockStatCard(main, "EliteKills", "ELITE-KILLS", elite != null ? MetaHubMockData.FormatNumber(elite.totalEliteKillsEver) : GetMetricValue(data, "elite_jagd", "0"), "Gesamt", "icon_skull", MetaHubTone.Red, new Vector2(40f, 210f), new Vector2(225f, 135f));
        CreateReferenceGoalPanel(main, data.nextGoals, new Vector2(410f, 205f), new Vector2(320f, 160f));

        Transform tree = CreateUnlockPanel(main, "EliteSkillTree", "JAGD-BAUM", new Vector2(-125f, -110f), new Vector2(760f, 455f), new Color32(167, 54, 45, 255));
        CreateEliteSkillBranch(tree, "EliteContractsBranch", "AUFTRAEGE", EliteHuntCategory.Contracts, 128f, MetaHubTone.Red, 4);
        CreateEliteSkillBranch(tree, "EliteAffixesBranch", "AFFIXE", EliteHuntCategory.Affixes, 40f, MetaHubTone.Purple, 4);
        CreateEliteSkillBranch(tree, "EliteRewardsBranch", "BELOHNUNGEN", EliteHuntCategory.Rewards, -48f, MetaHubTone.Gold, 4);
        CreateEliteSkillBranch(tree, "EliteFrequencyBranch", "JAGDMODUS", EliteHuntCategory.Frequency, -136f, MetaHubTone.Red, 4);

        Transform stats = CreateUnlockPanel(main, "EliteStatus", "JAGD-STATUS", new Vector2(420f, -110f), new Vector2(310f, 455f), new Color32(167, 54, 45, 255));
        int eliteCurrentXP = elite != null ? elite.GetXPIntoCurrentRank() : 0;
        int eliteRequiredXP = elite != null ? elite.GetXPToNextEliteRank() : 1;
        CreateCanvasLabel(stats, "RankXP", "Elite-Rang-XP: " + eliteCurrentXP + " / " + eliteRequiredXP, new Vector2(0f, 132f), new Vector2(250f, 22f), 12f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasBar(stats, "RankBar", new Vector2(0f, 108f), new Vector2(235f, 7f), Percent(eliteCurrentXP, eliteRequiredXP), new Color32(255, 84, 78, 255));
        CreateUnlockRow(stats, "EliteLast1", "icon_skull", "Gesehen", "Gesamt / letzter Run", elite != null ? elite.totalElitesSeenEver + " / " + elite.lastRunElitesSeen : "0 / 0", MetaHubTone.Red, 54f, 255f);
        CreateUnlockRow(stats, "EliteLast2", "icon_risk_core", "Besiegt", "Gesamt / letzter Run", elite != null ? elite.totalEliteKillsEver + " / " + elite.lastRunEliteKills : "0 / 0", MetaHubTone.Gold, -4f, 255f);
        CreateUnlockRow(stats, "EliteLast3", "icon_skull", "Leaks", "Gesamt", elite != null ? elite.totalEliteLeaksEver.ToString() : "0", MetaHubTone.Red, -62f, 255f);
        CreateUnlockRow(stats, "EliteLast4", "icon_gold", "Elite-Siegel", "Letzter Run", elite != null ? "+" + elite.lastRunEliteSealsGained : "+0", MetaHubTone.Gold, -120f, 255f);
        CreateCanvasButton(stats, "AllEliteGoals", "ALLE ZIELE ANZEIGEN", new Vector2(0f, -178f), new Vector2(240f, 36f), RequestAllGoals);
        return true;
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
            CreateSkillTreeNode(parent, name + "_Node_" + i, GeneralNodeTitle(nodeId, definition.displayName), effect, GeneralNodeCost(nodeId) + " KW", GeneralNodeStateLabel(nodeId), artName, nodeTone, position, new Vector2(126f, 72f), delegate { HandleGeneralNodeClick(capturedNodeId); });
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

        return CreateNodeWindow(ordered, maxNodes, delegate(GeneralMetaNodeDefinition definition) { return definition != null ? definition.nodeId : ""; }, delegate(string nodeId) { return general.IsNodePurchased(nodeId); });
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
        CreateCanvasLabel(panel, "Title", title, new Vector2(0f, size.y * 0.5f - 26f), new Vector2(size.x - 36f, 28f), titleFontSize, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        return panel;
    }

    private void CreateUnlockStatCard(Transform parent, string name, string title, string value, string caption, string artName, MetaHubTone tone, Vector2 position, Vector2 size)
    {
        Transform card = CreateOrnatePanel(parent, name, position, size, new Color32(7, 18, 20, 240), ToneColor(tone));
        CreateCanvasLabel(card, "Title", title, new Vector2(0f, size.y * 0.5f - 28f), new Vector2(size.x - 38f, 24f), 14f, new Color32(246, 236, 211, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateArtIcon(card, "Icon", artName, new Vector2(-size.x * 0.25f, 0f), new Vector2(68f, 68f));
        CreateCanvasLabel(card, "Value", value, new Vector2(34f, 12f), new Vector2(90f, 34f), 28f, new Color32(245, 195, 92, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(card, "Caption", caption, new Vector2(42f, -22f), new Vector2(130f, 22f), 11f, new Color32(225, 209, 184, 255), TextAlignmentOptions.Left, FontStyles.Normal);
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
        CreateCanvasLabel(row, "Title", Shorten(title, Mathf.RoundToInt(textWidth / 7f)), new Vector2(textX, 8f), new Vector2(textWidth, 18f), 9f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(row, "Description", Shorten(description, Mathf.RoundToInt(textWidth / 6f)), new Vector2(textX, -10f), new Vector2(textWidth, 18f), 7.5f, new Color32(188, 178, 155, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (!string.IsNullOrEmpty(value))
            CreateCanvasLabel(row, "Value", Shorten(value, Mathf.RoundToInt(valueWidth / 6f)), new Vector2(width * 0.5f - valueWidth * 0.5f - 10f, 0f), new Vector2(valueWidth, 22f), 9f, toneColor, TextAlignmentOptions.Right, FontStyles.Bold);
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

    private void CreateReferenceMetricCard(Transform parent, MetaHubMetricCardData data, int index)
    {
        float x = -485f + index * 240f;
        Transform card = CreateOrnatePanel(parent, "Metric_" + data.id, new Vector2(x, 214f), new Vector2(228f, 150f), new Color32(7, 18, 20, 240), ToneColor(data.tone));
        CreateCanvasLabel(card, "Title", data.title, new Vector2(0f, 54f), new Vector2(200f, 22f), 15f, new Color32(246, 236, 211, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateArtIcon(card, "Icon", GetMetricArtName(data.id), new Vector2(-55f, 10f), new Vector2(78f, 78f));
        CreateCanvasLabel(card, "Value", data.valueText, new Vector2(30f, 10f), new Vector2(80f, 34f), 25f, new Color32(245, 195, 92, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateCanvasLabel(card, "Caption", data.caption, new Vector2(42f, -20f), new Vector2(130f, 20f), 12f, new Color32(225, 209, 184, 255), TextAlignmentOptions.Left, FontStyles.Normal);
        if (data.showProgress)
            CreateCanvasBar(card, "Progress", new Vector2(42f, -48f), new Vector2(100f, 5f), Percent(data.current, data.maximum), ToneColor(data.tone));
        if (data.showProgress)
            CreateCanvasLabel(card, "ProgressText", data.current + " / " + data.maximum, new Vector2(30f, -35f), new Vector2(100f, 18f), 11f, new Color32(236, 211, 161, 255), TextAlignmentOptions.Center, FontStyles.Normal);
    }

    private void CreateReferenceProgressPanel(Transform parent, MetaHubData data)
    {
        Transform panel = CreateOrnatePanel(parent, "ProgressPanel", new Vector2(-390f, -10f), new Vector2(382f, 296f), new Color32(6, 18, 20, 238), new Color32(122, 94, 55, 255));
        CreateCanvasLabel(panel, "Title", "FORTSCHRITT", new Vector2(-92f, 118f), new Vector2(170f, 30f), 20f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateDonutImage(panel, "AccountRing", new Vector2(-85f, 5f), 168f, Percent(data.account.currentXP, data.account.requiredXP), new Color32(55, 209, 235, 255), new Color32(55, 46, 29, 210));
        CreateCanvasLabel(panel, "Level", data.account.level.ToString(), new Vector2(-85f, 5f), new Vector2(96f, 46f), 38f, new Color32(255, 255, 255, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "LevelCaption", "ACCOUNT LEVEL", new Vector2(-85f, -34f), new Vector2(130f, 20f), 12f, new Color32(242, 191, 75, 255), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateCanvasLabel(panel, "XPBottom", MetaHubMockData.FormatNumber(data.account.currentXP) + " / " + MetaHubMockData.FormatNumber(data.account.requiredXP) + " XP", new Vector2(-85f, -98f), new Vector2(180f, 22f), 14f, new Color32(238, 222, 194, 255), TextAlignmentOptions.Center, FontStyles.Normal);
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
        CreateCanvasLabel(panel, "Title", "NÄCHSTE ZIELE", new Vector2(0f, size.y * 0.5f - 30f), new Vector2(size.x - 44f, 30f), titleFontSize, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);
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
            CreateCanvasLabel(panel, "GoalTitle_" + i, Shorten(goal.title, Mathf.RoundToInt(titleWidth / 7f)), new Vector2(titleX, y), new Vector2(titleWidth, 24f), size.x < 280f ? 10f : 12f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            CreateCanvasLabel(panel, "GoalValue_" + i, goal.current + " / " + goal.required, new Vector2(valueX, y), new Vector2(valueWidth, 24f), size.x < 280f ? 10f : 12f, new Color32(232, 220, 198, 255), TextAlignmentOptions.Right, FontStyles.Bold);
            if (i < maxGoalRows - 1)
                CreateLine(panel, "GoalDivider_" + i, new Vector2(0f, y - rowSpacing * 0.55f), new Vector2(size.x - 62f, 1f), new Color32(45, 54, 50, 210));
        }
        float buttonHeight = size.y < 180f ? 34f : 38f;
        CreateCanvasButton(panel, "AllGoals", "ALLE ZIELE ANZEIGEN", new Vector2(0f, -size.y * 0.5f + 24f), new Vector2(Mathf.Min(245f, size.x - 52f), buttonHeight), RequestAllGoals);
    }

    private void CreateAllGoalsOverlay(Transform parent, List<MetaHubGoalData> goals)
    {
        CreateCanvasPanel(parent, "AllGoalsShade", Vector2.zero, new Vector2(1180f, 720f), new Color32(0, 0, 0, 150), new Color32(0, 0, 0, 0));
        Transform panel = CreateOrnatePanel(parent, "AllGoalsOverlay", new Vector2(0f, -18f), new Vector2(640f, 430f), new Color32(5, 17, 18, 248), new Color32(184, 124, 38, 255));
        CreateCanvasLabel(panel, "Title", "ALLE ZIELE", new Vector2(-218f, 174f), new Vector2(180f, 30f), 22f, new Color32(234, 178, 67, 255), TextAlignmentOptions.Left, FontStyles.Bold);

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

    private void CreateReferenceEffectPanel(Transform parent, string name, string title, List<MetaHubEffectData> effects, Vector2 position, Vector2 size, Color32 border)
    {
        Transform panel = CreateOrnatePanel(parent, name, position, size, new Color32(5, 21, 18, 238), border);
        CreateCanvasLabel(panel, "Title", title, new Vector2(-size.x * 0.5f + 126f, size.y * 0.5f - 30f), new Vector2(240f, 30f), 19f, border, TextAlignmentOptions.Left, FontStyles.Bold);
        for (int i = 0; i < effects.Count && i < 2; i++)
        {
            MetaHubEffectData effect = effects[i];
            float y = 38f - i * 66f;
            CreateArtIcon(panel, "EffectIcon_" + i, GetEffectArtName(effect), new Vector2(-size.x * 0.5f + 55f, y), new Vector2(43f, 43f));
            string effectText = string.IsNullOrEmpty(effect.description) ? effect.title : effect.title + "\n" + effect.description;
            float textWidth = Mathf.Max(190f, size.x - 250f);
            CreateCanvasLabel(panel, "EffectText_" + i, effectText, new Vector2(-size.x * 0.5f + 78f + textWidth * 0.5f, y), new Vector2(textWidth, 45f), 12f, new Color32(226, 236, 215, 255), TextAlignmentOptions.Left, FontStyles.Bold);
            CreateCanvasLabel(panel, "EffectDuration_" + i, effect.durationText, new Vector2(size.x * 0.5f - 80f, y), new Vector2(90f, 24f), 12f, new Color32(239, 219, 184, 255), TextAlignmentOptions.Right, FontStyles.Normal);
            if (i == 0)
                CreateLine(panel, "EffectDivider", new Vector2(20f, 5f), new Vector2(size.x - 48f, 1f), new Color32(border.r, border.g, border.b, 120));
        }
    }

    private void CreateReferenceRunPanel(Transform parent, List<MetaHubRunStatData> stats)
    {
        Transform panel = CreateOrnatePanel(parent, "RunPanel", new Vector2(453f, -270f), new Vector2(300f, 192f), new Color32(7, 19, 23, 238), new Color32(90, 100, 110, 255));
        CreateCanvasLabel(panel, "Title", "LETZTER RUN", new Vector2(-36f, 64f), new Vector2(210f, 30f), 20f, new Color32(246, 236, 211, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateLine(panel, "RunDividerTop", new Vector2(25f, 38f), new Vector2(260f, 1f), new Color32(45, 54, 50, 210));
        for (int i = 0; i < stats.Count && i < 4; i++)
        {
            float y = 15f - i * 26f;
            CreateCanvasLabel(panel, "RunLabel_" + i, stats[i].label, new Vector2(-48f, y), new Vector2(150f, 20f), 12f, new Color32(190, 198, 190, 255), TextAlignmentOptions.Left, FontStyles.Normal);
            CreateCanvasLabel(panel, "RunValue_" + i, stats[i].valueText, new Vector2(110f, y), new Vector2(60f, 20f), 12f, new Color32(244, 196, 88, 255), TextAlignmentOptions.Right, FontStyles.Bold);
        }
        CreateCanvasButton(panel, "RunStatsButton", "RUN-STATISTIKEN", new Vector2(0f, -74f), new Vector2(245f, 36f), delegate { Debug.Log("MetaHub: Run statistics requested."); });
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
        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "MetaHubRuntimeDonut";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

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

                texture.SetPixel(x, y, angle <= percent * 360f ? fillColor : trackColor);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
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
            case "special": return mainMenuUnlockMode ? "icon_skull" : "icon_keystone";
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
            case "chaos_wissen": return "icon_chaos";
            case "risikokerne": return "icon_risk_core";
            case "bauplaene": return "icon_blueprint";
            case "elite_jagd": return "icon_skull";
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

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(Color.white, highlightColor, 0.22f);
        colors.pressedColor = Color.Lerp(baseColor, highlightColor, 0.45f);
        colors.selectedColor = Color.Lerp(Color.white, highlightColor, 0.18f);
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
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
