using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GamePhase
{
    Build,
    Wave
}

public enum GameStartMode
{
    Normal,
    Balancing
}

public class GameManager : MonoBehaviour
{
    private static bool forceStartMenuOnNextSceneLoad = false;

    public GamePhase currentPhase = GamePhase.Build;

    [Header("References")]
    public TileManager tileManager;
    public EnemySpawner enemySpawner;
    public BlockedEventManager blockedEventManager;
    public ChaosJusticeManager chaosJusticeManager;
    public ChaosLexiconUI chaosLexiconUI;
    public ChaosUnlockManager chaosUnlockManager;
    public ChaosUnlockUI chaosUnlockUI;
    public ChaosUnlockRuntimeUI gameplayUnlockUI;
    public GeneralMetaProgressionManager generalMetaProgressionManager;
    public ChaosResearchProgressionManager chaosResearchProgressionManager;
    public PathTechniqueProgressionManager pathTechniqueProgressionManager;
    public EliteHuntProgressionManager eliteHuntProgressionManager;
    public TowerMasteryManager towerMasteryManager;
    public BasicTowerMasteryManager basicTowerMasteryManager;
    public RapidTowerMasteryManager rapidTowerMasteryManager;
    public HeavyTowerMasteryManager heavyTowerMasteryManager;
    public FireTowerMasteryManager fireTowerMasteryManager;
    public PoisonTowerMasteryManager poisonTowerMasteryManager;
    public SlowTowerMasteryManager slowTowerMasteryManager;
    public SniperTowerMasteryManager sniperTowerMasteryManager;
    public AlchemistTowerMasteryManager alchemistTowerMasteryManager;
    public LightningTowerMasteryManager lightningTowerMasteryManager;
    public MortarTowerMasteryManager mortarTowerMasteryManager;
    public SpikeTowerMasteryManager spikeTowerMasteryManager;
    public MainMenuLexiconManager mainMenuLexiconManager;
    public MainMenuLexiconUI mainMenuLexiconUI;
    public bool autoCreateMainMenuLexicon = true;
    public MainMenuStatisticsManager mainMenuStatisticsManager;
    public MainMenuStatisticsUI mainMenuStatisticsUI;
    public bool autoCreateMainMenuStatistics = true;
    public RunStatisticsTracker runStatisticsTracker;
    public bool autoCreateRunStatisticsTracker = true;
    public BuildManager buildManager;
    public BuildSelectionUI buildSelectionUI;
    public PathBuildManager pathBuildManager;
    public EliteRewardChoiceManager eliteRewardChoiceManager;
    public bool autoCreateEliteRewardChoiceManager = true;
    public EliteSpawnWarningManager eliteSpawnWarningManager;
    public bool autoCreateEliteSpawnWarningManager = true;
    public TowerUI towerUI;

    [Header("Start Menu")]
    public bool showStartMenuOnStart = true;
    public int normalStartGold = 120;
    public int normalStartLives = 20;
    public int balancingStartGold = 999999;
    public int balancingStartLives = 999999;
    public bool gameStarted = false;
    public bool startMenuOpen = false;
    public GameStartMode currentStartMode = GameStartMode.Normal;
    public Canvas startMenuCanvas;
    public GameObject startMenuRoot;
    public TextMeshProUGUI startMenuTitleText;
    public TextMeshProUGUI startMenuDescriptionText;
    public Button startGameButton;
    public Button startBalancingGameButton;
    public Button quitGameButton;
    public Button startUnlocksButton;
    public Button startLexiconButton;
    public Button startStatsButton;
    public Button startOptionsButton;
    public Button startResetButton;
    private bool startMenuResetArmed = false;

    [Header("Wave Settings")]
    public int waveNumber = 0;
    public int baseEnemyCount = 10;
    public float delayBeforeWave = 1f;

    [Header("Wave Scenario Debug")]
    public WaveScenario currentWaveScenario = WaveScenario.StandardIntro;
    public WaveScenario nextWaveScenario = WaveScenario.StandardIntro;
    public string currentWaveScenarioName = "";
    public string nextWaveScenarioName = "";

    [Header("Wave Data Debug")]
    public WaveData currentWaveData;
    public WaveData nextWaveData;

    [Header("Wave Completion Debug")]
    public WaveCompletionResult currentWaveResult;
    public WaveCompletionResult lastCompletedWaveResult;

    [Header("Wave History Debug")]
    public WaveHistory waveHistory = new WaveHistory();

    [Header("Enemy Wave Debug Window")]
    public bool autoCreateEnemyWaveDebugWindow = true;
    public KeyCode enemyWaveDebugToggleKey = KeyCode.F3;

    [Header("Wave Backend Events")]
    public bool fireWaveBackendEvents = true;

    [Header("Modal / Choice State Debug")]
    public bool postBossChoicePending = false;
    public bool postEliteRewardChoicePending = false;

    [Header("Elite V1")]
    public int eliteLeakLifeDamage = 5;
    public bool destroyEliteLeakTower = true;

    [Header("Blocked Build Phase")]
    public bool isBaseBlocked = false;
    public bool isTimedBlockedBuildPhase = false;
    public float blockedBuildTimeRemaining = 0f;

    private Coroutine blockedBuildTimerCoroutine;
    private bool blockedEventChosenForCurrentPosition = false;
    private Vector2Int blockedEventPosition;
    private bool blockedBaseRelocationPending = false;
    private float pendingBaseRelocationBuildPhaseDuration = 0f;

    [Header("Gold")]
    public int gold = 120;

    [Header("Tower Build Cost Scaling")]
    public bool scaleTowerBuildCostByTypePurchases = true;
    public float towerTypePurchaseCostIncrease = 0.5f;

    [Header("Wave Completion Rewards")]
    public bool giveWaveCompletionGold = true;
    public int baseWaveCompletionGold = 6;
    public int waveCompletionGoldPerWave = 1;
    public int miniBossWaveCompletionGoldBonus = 10;
    public int bossWaveCompletionGoldBonus = 24;

    [Header("Blocked Event Rewards V1")]
    public float blockedEventRewardBonusPerStack = 0.03f;
    public int blockedEventRewardBonusStacks = 0;
    public int blockedEventRewardBonusGrowthStacks = 0;
    public int pendingEvolutionTowerBoosts = 0;

    [Header("Gold Tile Rewards")]
    public float goldTileRewardBonusPerTile = 0.05f;
    public int goldTileRewardBonusStacks = 0;

    private float goldRewardModifierRemainder = 0f;
    private float xpRewardModifierRemainder = 0f;
    private readonly Dictionary<TowerRole, int> towerBuildCountByRole = new Dictionary<TowerRole, int>();

    private int lastWaveRewarded = 0;
    private int lastWaveCompletionGoldReward = 0;

    [Header("Lives")]
    public int lives = 15;
    public bool isGameOver = false;

    private void Start()
    {
        Time.timeScale = 1f;
        currentPhase = GamePhase.Build;

        EnsureWaveHistory();
        ResolveOptionalInteractionReferences();
        GetChaosJusticeManager();
        GetRunStatisticsTracker();
        GetChaosUnlockManager();
        GetGeneralMetaProgressionManager();
        GetChaosResearchProgressionManager();
        GetPathTechniqueProgressionManager();
        GetEliteHuntProgressionManager();
        GetTowerMasteryManager();
        GetBasicTowerMasteryManager();
        GetRapidTowerMasteryManager();
        GetHeavyTowerMasteryManager();
        GetFireTowerMasteryManager();
        GetPoisonTowerMasteryManager();
        GetSlowTowerMasteryManager();
        GetSniperTowerMasteryManager();
        GetAlchemistTowerMasteryManager();
        GetLightningTowerMasteryManager();
        GetMortarTowerMasteryManager();
        GetSpikeTowerMasteryManager();
        GetEliteRewardChoiceManager();
        GetEliteSpawnWarningManager();
        EnsureEnemyWaveDebugWindow();

        if (showStartMenuOnStart || forceStartMenuOnNextSceneLoad)
        {
            forceStartMenuOnNextSceneLoad = false;
            OpenStartMenu();
            RefreshWaveScenarioDebug();
            RefreshWaveDataDebug();
            return;
        }

        StartSelectedGame(GameStartMode.Normal);
    }

    private void EnsureEnemyWaveDebugWindow()
    {
        if (!autoCreateEnemyWaveDebugWindow)
            return;

        EnemyWaveDebugWindow.EnsureExists(this, enemyWaveDebugToggleKey);
    }

    private void Update()
    {
        if (isGameOver && Input.GetKeyDown(KeyCode.R))
            RestartGame();
    }

    public bool IsPlayerBlocked()
    {
        return isBaseBlocked;
    }

    public void OnPathExtended()
    {
        if (!gameStarted || isGameOver || isBaseBlocked || currentPhase != GamePhase.Build || IsGameplayInputLockedByModalUI())
            return;

        StopBlockedBuildTimer();
        StartCoroutine(StartWaveAfterDelay());
    }

    private IEnumerator StartWaveAfterDelay()
    {
        currentPhase = GamePhase.Wave;

        if (tileManager != null)
            tileManager.SetCanBuild(false);

        yield return new WaitForSeconds(delayBeforeWave);
        StartNextWave();
    }

    private void StartNextWave()
    {
        if (!gameStarted || isGameOver || IsChaosJusticeChoiceOpen() || IsEliteRewardChoiceOpen())
            return;

        currentPhase = GamePhase.Wave;

        if (tileManager != null)
            tileManager.SetCanBuild(false);

        waveNumber++;

        if (enemySpawner == null)
        {
            Debug.LogError("GameManager: EnemySpawner fehlt!");
            OnWaveFinished();
            return;
        }

        currentWaveData = BuildWaveDataForGameWave(waveNumber);

        if (currentWaveData == null)
        {
            Debug.LogError("GameManager: Konnte keine WaveData erzeugen!");
            OnWaveFinished();
            return;
        }

        currentWaveScenario = currentWaveData.scenario;
        currentWaveScenarioName = currentWaveData.scenarioName;
        CreateCurrentWaveResult(currentWaveData);
        RaiseWaveStartedEvent(currentWaveData);
        enemySpawner.StartWave(currentWaveData, OnWaveFinished);
        RefreshNextWaveDataDebug();

        Debug.Log(
            "Wave " + waveNumber +
            " started | Scenario: " + currentWaveData.scenario +
            " | Name: " + currentWaveData.scenarioName +
            " | Requested Count: " + currentWaveData.requestedEnemyCount +
            " | Modified Count: " + currentWaveData.modifiedEnemyCount +
            " | Total Spawn Count: " + currentWaveData.totalSpawnCount +
            " | ChaosWave: " + (string.IsNullOrEmpty(currentWaveData.chaosWaveSummary) ? "Keine" : currentWaveData.chaosWaveSummary)
        );
    }

    private void OnWaveFinished()
    {
        if (isGameOver)
        {
            if (tileManager != null)
                tileManager.SetCanBuild(false);

            StopBlockedBuildTimer();
            return;
        }

        FinalizeCurrentWaveResult();
        RegisterEliteWaveCompletionIfNeeded();
        GiveWaveCompletionReward();
        RecordLastWaveStatistics(true);
        ApplyBlockedEventRewardBonusGrowthAfterCompletedWave();
        currentPhase = GamePhase.Build;
        RefreshWaveScenarioDebug();
        RefreshWaveDataDebug();

        if (TryOpenChaosJusticeChoiceAfterWave())
        {
            postBossChoicePending = true;
            return;
        }

        if (TryOpenEliteRewardChoiceAfterWave())
        {
            postEliteRewardChoicePending = true;
            return;
        }

        CompletePostWaveBuildPhase();
    }

    private void CompletePostWaveBuildPhase()
    {
        if (!gameStarted || isGameOver)
            return;

        postBossChoicePending = false;
        currentPhase = GamePhase.Build;
        RefreshWaveScenarioDebug();
        RefreshWaveDataDebug();

        if (tileManager != null && !tileManager.HasAnyValidExtension())
        {
            HandleBaseBlocked();
            return;
        }

        isBaseBlocked = false;
        isTimedBlockedBuildPhase = false;
        blockedEventChosenForCurrentPosition = false;

        if (tileManager != null)
            tileManager.SetCanBuild(true);

        RaiseBuildPhaseStartedEvent();
        Debug.Log("Wave finished. Build phase started.");
    }

    public void ResumeBuildPhaseAfterChaosJusticeChoice()
    {
        if (isGameOver)
            return;

        CompletePostWaveBuildPhase();
    }

    public void ResumeBuildPhaseAfterEliteRewardChoice()
    {
        if (isGameOver)
            return;

        postEliteRewardChoicePending = false;
        CompletePostWaveBuildPhase();
    }

    private bool TryOpenChaosJusticeChoiceAfterWave()
    {
        if (lastCompletedWaveResult == null || !lastCompletedWaveResult.isBossWave || !lastCompletedWaveResult.waveCompleted)
            return false;

        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager == null || !manager.isActiveAndEnabled)
            return false;

        return manager.TryOpenBossChoice(lastCompletedWaveResult);
    }

    private bool TryOpenEliteRewardChoiceAfterWave()
    {
        if (lastCompletedWaveResult == null || !lastCompletedWaveResult.isEliteWave || !lastCompletedWaveResult.waveCompleted || !lastCompletedWaveResult.eliteDefeated)
            return false;

        EliteRewardChoiceManager manager = GetEliteRewardChoiceManager();

        if (manager == null || !manager.isActiveAndEnabled)
            return false;

        return manager.OpenEliteRewardSelection(lastCompletedWaveResult);
    }


    private int GetBalancingBaseEnemyCountForWave(int targetWaveNumber)
    {
        int cycleWave = ((Mathf.Max(1, targetWaveNumber) - 1) % 10) + 1;
        return cycleWave == 5 || cycleWave == 10 ? 1 : 5;
    }

    public bool IsBalancingGameMode()
    {
        return currentStartMode == GameStartMode.Balancing;
    }

    public void OpenStartMenu()
    {
        gameStarted = false;
        startMenuOpen = true;
        currentPhase = GamePhase.Build;
        startMenuResetArmed = false;

        if (tileManager != null)
            tileManager.SetCanBuild(false);

        EnsureStartMenuUI();

        if (startMenuRoot != null)
        {
            ApplyStartMenuOverlayLayout();
            startMenuRoot.transform.SetAsLastSibling();
            startMenuRoot.SetActive(true);
        }
    }

    public void StartNormalGame()
    {
        StartSelectedGame(GameStartMode.Normal);
    }

    public void StartBalancingGame()
    {
        StartSelectedGame(GameStartMode.Balancing);
    }

    private void StartSelectedGame(GameStartMode mode)
    {
        startMenuResetArmed = false;
        currentStartMode = mode;
        gameStarted = true;
        startMenuOpen = false;
        currentPhase = GamePhase.Build;
        Time.timeScale = 1f;
        postBossChoicePending = false;
        postEliteRewardChoicePending = false;
        blockedEventRewardBonusStacks = 0;
        blockedEventRewardBonusGrowthStacks = 0;
        goldTileRewardBonusStacks = 0;
        goldRewardModifierRemainder = 0f;
        xpRewardModifierRemainder = 0f;
        towerBuildCountByRole.Clear();
        pendingEvolutionTowerBoosts = 0;
        ApplyStartModeResources(mode);

        TowerMasteryManager towerMasteryManager = GetTowerMasteryManager();
        if (towerMasteryManager != null)
            towerMasteryManager.StartNewRun();

        GeneralMetaProgressionManager generalMetaManager = GetGeneralMetaProgressionManager();
        if (generalMetaManager != null)
            generalMetaManager.StartNewRun();

        ChaosResearchProgressionManager chaosResearchManager = GetChaosResearchProgressionManager();
        if (chaosResearchManager != null)
            chaosResearchManager.StartNewRun();

        PathTechniqueProgressionManager pathTechniqueManager = GetPathTechniqueProgressionManager();
        if (pathTechniqueManager != null)
            pathTechniqueManager.StartNewRun();

        EliteHuntProgressionManager eliteHuntManager = GetEliteHuntProgressionManager();
        if (eliteHuntManager != null)
            eliteHuntManager.StartNewRun();

        BasicTowerMasteryManager basicMasteryManager = GetBasicTowerMasteryManager();
        if (basicMasteryManager != null)
            basicMasteryManager.StartNewRun();

        RapidTowerMasteryManager rapidMasteryManager = GetRapidTowerMasteryManager();
        if (rapidMasteryManager != null)
            rapidMasteryManager.StartNewRun();

        HeavyTowerMasteryManager heavyMasteryManager = GetHeavyTowerMasteryManager();
        if (heavyMasteryManager != null)
            heavyMasteryManager.StartNewRun();

        FireTowerMasteryManager fireMasteryManager = GetFireTowerMasteryManager();
        if (fireMasteryManager != null)
            fireMasteryManager.StartNewRun();

        PoisonTowerMasteryManager poisonMasteryManager = GetPoisonTowerMasteryManager();
        if (poisonMasteryManager != null)
            poisonMasteryManager.StartNewRun();

        SlowTowerMasteryManager slowMasteryManager = GetSlowTowerMasteryManager();
        if (slowMasteryManager != null)
            slowMasteryManager.StartNewRun();

        SniperTowerMasteryManager sniperMasteryManager = GetSniperTowerMasteryManager();
        if (sniperMasteryManager != null)
            sniperMasteryManager.StartNewRun();

        AlchemistTowerMasteryManager alchemistMasteryManager = GetAlchemistTowerMasteryManager();
        if (alchemistMasteryManager != null)
            alchemistMasteryManager.StartNewRun();

        LightningTowerMasteryManager lightningMasteryManager = GetLightningTowerMasteryManager();
        if (lightningMasteryManager != null)
            lightningMasteryManager.StartNewRun();

        MortarTowerMasteryManager mortarMasteryManager = GetMortarTowerMasteryManager();
        if (mortarMasteryManager != null)
            mortarMasteryManager.StartNewRun();

        SpikeTowerMasteryManager spikeMasteryManager = GetSpikeTowerMasteryManager();
        if (spikeMasteryManager != null)
            spikeMasteryManager.StartNewRun();

        if (startMenuRoot != null)
            startMenuRoot.SetActive(false);

        CloseMainMenuUnlocks();
        CloseMainMenuLexicon();
        CloseMainMenuStatistics();

        if (tileManager != null)
        {
            int extraStartPathTiles = 0;
            if (mode == GameStartMode.Normal)
            {
                GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
                extraStartPathTiles = generalMeta != null ? generalMeta.GetActiveStartPathBonus() : 0;
            }

            tileManager.InitializeRunPath(extraStartPathTiles);
            tileManager.SetCanBuild(true);
        }

        RefreshWaveScenarioDebug();
        RefreshWaveDataDebug();
        RaiseBuildPhaseStartedEvent();

        Debug.Log(mode == GameStartMode.Balancing
            ? "Balancing Game gestartet: feste Enemy-Typ-Waves aktiv."
            : "Spiel gestartet.");
    }

    private void ApplyStartModeResources(GameStartMode mode)
    {
        gold = mode == GameStartMode.Balancing ? balancingStartGold : normalStartGold;
        lives = mode == GameStartMode.Balancing ? balancingStartLives : normalStartLives;

        if (mode == GameStartMode.Normal)
        {
            GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
            if (generalMeta != null)
            {
                gold += Mathf.Max(0, generalMeta.GetActiveStartGoldBonus());
                lives += Mathf.Max(0, generalMeta.GetActiveStartLifeBonus());
            }
        }

        isGameOver = false;
    }

    public void QuitGameFromStartMenu()
    {
        startMenuResetArmed = false;
        Debug.Log("Spiel schließen gewählt.");
        Application.Quit();
    }

    public void AbortRunAndReturnToStartMenu()
    {
        startMenuResetArmed = false;
        Time.timeScale = 1f;

        CloseMainMenuUnlocks();
        CloseMainMenuLexicon();
        CloseMainMenuStatistics();
        ClosePathAndBuildSelectionsForModal();
        CloseTowerUIForModal();

        if (blockedEventManager != null)
            blockedEventManager.CloseSelection();

        if (chaosJusticeManager != null)
            chaosJusticeManager.CloseSelectionWithoutResume();

        if (eliteRewardChoiceManager != null)
            eliteRewardChoiceManager.CloseSelectionWithoutResume();

        if (eliteSpawnWarningManager != null)
            eliteSpawnWarningManager.CloseWarningAndRestoreTime();

        forceStartMenuOnNextSceneLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void EnsureStartMenuUI()
    {
        if (startMenuCanvas == null)
            startMenuCanvas = FindObjectOfType<Canvas>();

        if (startMenuCanvas == null)
        {
            GameObject canvasObject = new GameObject("StartMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            startMenuCanvas = canvasObject.GetComponent<Canvas>();
            startMenuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            startMenuCanvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (startMenuRoot == null)
        {
            startMenuRoot = new GameObject("StartMenuRoot", typeof(RectTransform), typeof(Image));
            startMenuRoot.transform.SetParent(startMenuCanvas.transform, false);
        }

        ApplyStartMenuOverlayLayout();
        RebuildStartMenuContent();
        SetupStartMenuButtons();
        RefreshStartMenuTexts();
    }

    private void RebuildStartMenuContent()
    {
        if (startMenuRoot == null)
            return;

        for (int i = startMenuRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(startMenuRoot.transform.GetChild(i).gameObject);

        startGameButton = null;
        startBalancingGameButton = null;
        quitGameButton = null;
        startUnlocksButton = null;
        startLexiconButton = null;
        startStatsButton = null;
        startOptionsButton = null;
        startResetButton = null;
        startMenuTitleText = null;
        startMenuDescriptionText = null;

        startMenuTitleText = CreateStartMenuOverlayText(startMenuRoot.transform, "StartMenuTitle", "TOWER DEFENSE", 42f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(760f, 72f));
        startMenuDescriptionText = CreateStartMenuOverlayText(startMenuRoot.transform, "StartMenuDescription", "", 17f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(820f, 56f));

        GameObject leftPanel = CreateStartMenuButtonGroup("StartMenuLeftButtons", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(300f, -170f));
        VerticalLayoutGroup leftLayout = leftPanel.AddComponent<VerticalLayoutGroup>();
        leftLayout.padding = new RectOffset(18, 18, 18, 18);
        leftLayout.spacing = 16f;
        leftLayout.childAlignment = TextAnchor.MiddleCenter;
        leftLayout.childControlWidth = true;
        leftLayout.childControlHeight = false;
        leftLayout.childForceExpandWidth = true;
        leftLayout.childForceExpandHeight = false;

        startGameButton = CreateStartMenuButton(leftPanel.transform, "StartGameButton", "Spiel starten", new Vector2(0f, 64f), 20f);
        startUnlocksButton = CreateStartMenuButton(leftPanel.transform, "StartUnlocksButton", "Freischaltungen", new Vector2(0f, 64f), 18f);
        startLexiconButton = CreateStartMenuButton(leftPanel.transform, "StartLexiconButton", "Lexikon", new Vector2(0f, 64f), 18f);
        startStatsButton = CreateStartMenuButton(leftPanel.transform, "StartStatsButton", "Statistik", new Vector2(0f, 64f), 18f);
        quitGameButton = CreateStartMenuButton(leftPanel.transform, "QuitGameButton", "Beenden", new Vector2(0f, 64f), 18f);

        startBalancingGameButton = CreateAnchoredStartMenuButton(startMenuRoot.transform, "DevStartButton", "Dev", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(86f, 42f), 16f, new Color32(85, 65, 145, 245));
        startOptionsButton = CreateAnchoredStartMenuButton(startMenuRoot.transform, "OptionsButton", "Optionen", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-156f, 24f), new Vector2(132f, 42f), 15f, new Color32(35, 45, 64, 235));
        startResetButton = CreateAnchoredStartMenuButton(startMenuRoot.transform, "ResetButton", "Reset", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 24f), new Vector2(112f, 42f), 15f, new Color32(80, 45, 50, 235));
    }


    private GameObject CreateStartMenuButtonGroup(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject group = new GameObject(objectName, typeof(RectTransform));
        group.transform.SetParent(startMenuRoot.transform, false);

        RectTransform rect = group.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        return group;
    }

    private GameObject CreateStartMenuPanel(string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(startMenuRoot.transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private GameObject CreateStartMenuDecoration(Transform parent, string objectName, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private void AddStartMenuFrame(Transform parent, Color color, float thickness)
    {
        AddStartMenuLine(parent, "FrameTop", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        AddStartMenuLine(parent, "FrameBottom", color, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        AddStartMenuLine(parent, "FrameLeft", color, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        AddStartMenuLine(parent, "FrameRight", color, new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
    }

    private void AddStartMenuLine(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject line = CreateStartMenuDecoration(parent, objectName, color);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void AddStartMenuAccent(Transform parent, Color color, float width)
    {
        GameObject accent = CreateStartMenuDecoration(parent, "LeftAccent", color);
        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(width, 0f);
    }

    private TextMeshProUGUI CreateStartMenuOverlayText(Transform parent, string objectName, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = new Color32(240, 244, 250, 255);
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateAnchoredStartMenuButton(Transform parent, string objectName, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color)
    {
        Button button = CreateStartMenuButton(parent, objectName, label, size, fontSize);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, new Color32(220, 150, 43, 255), 0.25f);
            colors.pressedColor = Color.Lerp(color, new Color32(220, 150, 43, 255), 0.45f);
            colors.selectedColor = color;
            button.colors = colors;
        }

        return button;
    }

    private Button CreateStartMenuButton(Transform parent, string objectName, string label, Vector2 preferredSize, float fontSize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredSize.y;
        layoutElement.preferredWidth = preferredSize.x > 0f ? preferredSize.x : 240f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(24, 28, 29, 245);
        image.raycastTarget = true;
        AddStartMenuFrame(buttonObject.transform, new Color32(72, 63, 45, 255), 2f);
        AddStartMenuAccent(buttonObject.transform, new Color32(220, 150, 43, 255), 8f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color32(66, 50, 25, 255);
        colors.pressedColor = new Color32(120, 76, 25, 255);
        colors.selectedColor = image.color;
        colors.disabledColor = new Color32(34, 34, 34, 180);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI text = CreateStartMenuOverlayText(buttonObject.transform, objectName + "Text", label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.enableWordWrapping = false;
        return button;
    }

    private void ApplyStartMenuOverlayLayout()
    {
        if (startMenuCanvas != null)
        {
            startMenuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            startMenuCanvas.sortingOrder = Mathf.Max(startMenuCanvas.sortingOrder, 1000);
        }

        if (startMenuRoot == null)
            return;

        RectTransform rootRect = startMenuRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        Image rootImage = startMenuRoot.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = new Color32(1, 8, 7, 255);
            rootImage.raycastTarget = true;
        }
    }

    public void OpenStartMenuPlaceholder(string label)
    {
        Debug.Log(label + " ist im Hauptmenü vorbereitet, aber noch ohne Funktion.");
    }

    private void SetupStartMenuButtons()
    {
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(StartNormalGame);
        }

        if (startBalancingGameButton != null)
        {
            startBalancingGameButton.onClick.RemoveAllListeners();
            startBalancingGameButton.onClick.AddListener(StartBalancingGame);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveAllListeners();
            quitGameButton.onClick.AddListener(QuitGameFromStartMenu);
        }

        SetupStartMenuUnlocksButton();
        SetupStartMenuLexiconButton();
        SetupStartMenuStatisticsButton();
        SetupPlaceholderStartMenuButton(startOptionsButton, "Optionen");
        SetupStartMenuResetButton();
    }

    private void SetupStartMenuUnlocksButton()
    {
        if (startUnlocksButton == null)
            return;

        startUnlocksButton.onClick.RemoveAllListeners();
        startUnlocksButton.onClick.AddListener(OpenMainMenuUnlocks);
    }

    private void SetupStartMenuLexiconButton()
    {
        if (startLexiconButton == null)
            return;

        startLexiconButton.onClick.RemoveAllListeners();
        startLexiconButton.onClick.AddListener(OpenMainMenuLexicon);
    }

    private void SetupStartMenuStatisticsButton()
    {
        if (startStatsButton == null)
            return;

        startStatsButton.onClick.RemoveAllListeners();
        startStatsButton.onClick.AddListener(OpenMainMenuStatistics);
    }

    private void SetupPlaceholderStartMenuButton(Button button, string label)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            DisarmStartMenuResetConfirmation();
            OpenStartMenuPlaceholder(label);
        });
    }

    private void SetupStartMenuResetButton()
    {
        if (startResetButton == null)
            return;

        startResetButton.onClick.RemoveAllListeners();
        startResetButton.onClick.AddListener(HandleStartMenuResetPressed);
        SetStartMenuButtonLabel(startResetButton, "Reset");
    }

    private void HandleStartMenuResetPressed()
    {
        if (!startMenuResetArmed)
        {
            startMenuResetArmed = true;
            SetStartMenuButtonLabel(startResetButton, "Sicher?");

            if (startMenuDescriptionText != null)
                startMenuDescriptionText.text = "Reset bereit: Nochmal Reset druecken, um Meta-Progression und alle Freischaltungen zu loeschen.";

            return;
        }

        ResetAllPersistentProgressionAndReload();
    }

    private void ResetAllPersistentProgressionAndReload()
    {
        CloseMainMenuUnlocks();
        CloseMainMenuLexicon();
        CloseMainMenuStatistics();

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void DisarmStartMenuResetConfirmation()
    {
        if (!startMenuResetArmed)
            return;

        startMenuResetArmed = false;
        SetStartMenuButtonLabel(startResetButton, "Reset");
        RefreshStartMenuTexts();
    }

    private void SetStartMenuButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = label;
    }

    public void OpenMainMenuLexicon()
    {
        DisarmStartMenuResetConfirmation();
        CloseMainMenuUnlocks();
        CloseMainMenuStatistics();

        MainMenuLexiconManager manager = GetMainMenuLexiconManager();

        if (manager == null)
        {
            OpenStartMenuPlaceholder("Lexikon");
            return;
        }

        manager.OpenLexicon();
    }

    public void OpenMainMenuStatistics()
    {
        DisarmStartMenuResetConfirmation();
        CloseMainMenuUnlocks();
        CloseMainMenuLexicon();

        MainMenuStatisticsManager manager = GetMainMenuStatisticsManager();

        if (manager == null)
        {
            OpenStartMenuPlaceholder("Statistik");
            return;
        }

        manager.OpenStatistics();
    }

    public void OpenMainMenuUnlocks()
    {
        DisarmStartMenuResetConfirmation();
        CloseMainMenuLexicon();
        CloseMainMenuStatistics();

        ChaosUnlockManager manager = GetChaosUnlockManager();

        if (manager == null)
        {
            OpenStartMenuPlaceholder("Freischaltungen");
            return;
        }

        manager.OpenUnlocks();
    }

    public void CloseMainMenuUnlocks()
    {
        if (chaosUnlockManager != null)
            chaosUnlockManager.CloseUnlocks();
        else if (chaosUnlockUI != null)
            chaosUnlockUI.CloseUnlocks();
    }

    public void CloseMainMenuLexicon()
    {
        if (mainMenuLexiconManager != null)
            mainMenuLexiconManager.CloseLexicon();
        else if (mainMenuLexiconUI != null)
            mainMenuLexiconUI.CloseLexicon();
    }

    public void CloseMainMenuStatistics()
    {
        if (mainMenuStatisticsManager != null)
            mainMenuStatisticsManager.CloseStatistics();
        else if (mainMenuStatisticsUI != null)
            mainMenuStatisticsUI.CloseStatistics();
    }

    private void RefreshStartMenuTexts()
    {
        if (startMenuTitleText != null)
            startMenuTitleText.text = "TOWER DEFENSE";

        if (startMenuDescriptionText != null)
            startMenuDescriptionText.text = "Wähle einen Modus oder öffne später freigeschaltete Hauptmenü-Bereiche.";
    }

    private int CalculateEnemyCountForWave(int targetWaveNumber)
    {
        int safeWave = Mathf.Max(1, targetWaveNumber);

        if (currentStartMode == GameStartMode.Balancing)
            return GetBalancingBaseEnemyCountForWave(safeWave);

        int baseCount = Mathf.Max(10, baseEnemyCount);

        switch (safeWave)
        {
            case 1: return 8;
            case 2: return 10;
            case 3: return 12;
            case 4: return 14;
            case 5: return 15;
            case 6: return 18;
            case 7: return 21;
            case 8: return 24;
            case 9: return 27;
            case 10: return 18;
        }

        if (safeWave == 11)
            return 25;

        WaveScenario scenario = GetWaveScenarioForWave(safeWave);
        int wavesAfterTen = safeWave - 10;
        int tenWaveBlock = wavesAfterTen / 10;
        int waveRamp = Mathf.FloorToInt(wavesAfterTen * 0.45f);
        int blockBase = baseCount + 14 + tenWaveBlock * 11 + waveRamp;
        int calculatedCount;

        switch (scenario)
        {
            case WaveScenario.Elite:
                return 1;
            case WaveScenario.Boss:
                calculatedCount = blockBase + 6;
                break;
            case WaveScenario.MiniBoss:
                calculatedCount = blockBase + 9;
                break;
            case WaveScenario.RunnerAttack:
                calculatedCount = blockBase + 14;
                break;
            case WaveScenario.TankArmorCheck:
                calculatedCount = blockBase + 10;
                break;
            case WaveScenario.EffectCheck:
                calculatedCount = blockBase + 12;
                break;
            case WaveScenario.Mixed:
            default:
                calculatedCount = blockBase + 13;
                break;
        }

        int softCap = 82 + tenWaveBlock * 14 + Mathf.FloorToInt(wavesAfterTen * 0.4f);
        return Mathf.Clamp(calculatedCount, 10, softCap);
    }

    public int GetEnemyCountPreviewForWave(int targetWaveNumber)
    {
        return CalculateEnemyCountForWave(targetWaveNumber);
    }

    public WaveScenario GetWaveScenarioForWave(int targetWaveNumber)
    {
        if (enemySpawner == null)
            return WaveScenario.Mixed;

        if (currentStartMode == GameStartMode.Balancing)
            return enemySpawner.GetBalancingWaveScenario(targetWaveNumber);

        return enemySpawner.GetWaveScenario(targetWaveNumber);
    }

    public WaveScenario GetCurrentWaveScenario()
    {
        return currentWaveScenario;
    }

    public WaveScenario GetNextWaveScenario()
    {
        return GetWaveScenarioForWave(waveNumber + 1);
    }

    public string GetCurrentWaveScenarioName()
    {
        if (enemySpawner == null)
            return currentWaveScenario.ToString();

        if (currentStartMode == GameStartMode.Balancing)
            return enemySpawner.GetBalancingScenarioNameForWave(Mathf.Max(1, waveNumber));

        return enemySpawner.GetScenarioNameForWave(Mathf.Max(1, waveNumber));
    }

    public string GetNextWaveScenarioName()
    {
        if (enemySpawner == null)
            return GetNextWaveScenario().ToString();

        if (currentStartMode == GameStartMode.Balancing)
            return enemySpawner.GetBalancingScenarioNameForWave(waveNumber + 1);

        return enemySpawner.GetScenarioNameForWave(waveNumber + 1);
    }

    private WaveData BuildWaveDataForGameWave(int targetWaveNumber)
    {
        if (enemySpawner == null)
            return null;

        int safeWave = Mathf.Max(1, targetWaveNumber);
        int enemyCount = CalculateEnemyCountForWave(safeWave);

        if (currentStartMode == GameStartMode.Balancing)
            return enemySpawner.BuildBalancingWaveDataForWave(safeWave);

        return enemySpawner.BuildWaveDataForWave(safeWave, enemyCount);
    }

    private void RefreshWaveScenarioDebug()
    {
        if (enemySpawner == null)
        {
            currentWaveScenario = WaveScenario.Mixed;
            nextWaveScenario = WaveScenario.Mixed;
            currentWaveScenarioName = currentWaveScenario.ToString();
            nextWaveScenarioName = nextWaveScenario.ToString();
            return;
        }

        if (waveNumber > 0)
        {
            currentWaveScenario = GetWaveScenarioForWave(waveNumber);
            currentWaveScenarioName = enemySpawner.GetScenarioNameForWave(waveNumber);
        }
        else
        {
            currentWaveScenario = GetWaveScenarioForWave(1);
            currentWaveScenarioName = "Noch keine Wave gestartet";
        }

        nextWaveScenario = GetWaveScenarioForWave(waveNumber + 1);
        nextWaveScenarioName = enemySpawner.GetScenarioNameForWave(waveNumber + 1);
    }

    private void RefreshWaveDataDebug()
    {
        if (enemySpawner == null)
        {
            currentWaveData = null;
            nextWaveData = null;
            return;
        }

        if (waveNumber > 0)
            currentWaveData = BuildWaveDataForGameWave(waveNumber);
        else
            currentWaveData = null;

        nextWaveData = BuildWaveDataForGameWave(waveNumber + 1);
    }

    private void RefreshNextWaveDataDebug()
    {
        if (enemySpawner == null)
        {
            nextWaveData = null;
            return;
        }

        nextWaveData = BuildWaveDataForGameWave(waveNumber + 1);

        if (nextWaveData != null)
        {
            nextWaveScenario = nextWaveData.scenario;
            nextWaveScenarioName = nextWaveData.scenarioName;
        }
    }

    public WaveData GetCurrentWaveData()
    {
        return currentWaveData;
    }

    public WaveData GetNextWaveData()
    {
        if (nextWaveData == null)
            nextWaveData = BuildWaveDataForGameWave(waveNumber + 1);

        return nextWaveData;
    }

    public string GetNextWavePreviewText()
    {
        WaveData data = GetNextWaveData();

        if (data == null || enemySpawner == null)
            return "Keine Wave Preview verfügbar.";

        string enemyPreview = enemySpawner.GetPreviewTextForWave(data.waveNumber, data.requestedEnemyCount);
        GeneralMetaProgressionManager generalMeta = currentStartMode == GameStartMode.Normal ? GetGeneralMetaProgressionManager() : null;

        string previewText =
            "Wave " + data.waveNumber + " - " + data.scenarioName +
            "\n\n" + enemyPreview;

        previewText += BuildMetaPreviewDetails(data, generalMeta);
        previewText += BuildStartScoutPreview(data.waveNumber, generalMeta);

        return previewText;
    }

    private string BuildMetaPreviewDetails(WaveData data, GeneralMetaProgressionManager generalMeta)
    {
        if (data == null || generalMeta == null)
            return "";

        StringBuilder builder = new StringBuilder();

        if (generalMeta.HasRolePreviewI() && enemySpawner != null)
            builder.AppendLine("Hinweis: " + enemySpawner.GetSpecialHintForWave(data.waveNumber));

        if (generalMeta.HasRolePreviewII())
            builder.AppendLine("Gesamt: " + data.totalSpawnCount + " Gegner | Normal " + data.normalEnemyCount + " | Spezial " + data.specialEnemyCount);

        if (generalMeta.HasBossPreview() && (data.IsMiniBossWave() || data.IsBossWave()))
        {
            string label = data.IsBossWave() ? "Boss-Wave" : "MiniBoss-Wave";
            builder.AppendLine(label + ": Boss/MiniBoss zerstoeren keine Tower und geben wichtige XP/Rewards.");
        }

        if (generalMeta.HasChaosPreview())
        {
            if (!string.IsNullOrEmpty(data.modifierSummary))
                builder.AppendLine("Risiken: " + data.modifierSummary);

            if (data.hasChaosVariants && !string.IsNullOrEmpty(data.chaosVariantSummary))
                builder.AppendLine("Chaos-Varianten: " + data.chaosVariantSummary);
        }

        if (generalMeta.HasChaosWavePreview() && data.hasChaosWaveBlocks)
            builder.AppendLine("Chaos-Wave: " + data.GetChaosWaveDetailsText());

        if (builder.Length <= 0)
            return "";

        return "\n\n" + builder.ToString().TrimEnd();
    }

    private string BuildStartScoutPreview(int nextWaveNumber, GeneralMetaProgressionManager generalMeta)
    {
        if (generalMeta == null || !generalMeta.IsStartScoutActive() || nextWaveNumber >= 3)
            return "";

        StringBuilder builder = new StringBuilder();

        for (int wave = nextWaveNumber + 1; wave <= 3; wave++)
        {
            WaveData scoutData = BuildWaveDataForGameWave(wave);
            if (scoutData == null)
                continue;

            if (builder.Length == 0)
                builder.AppendLine("Start-Scout:");

            builder.AppendLine("Wave " + scoutData.waveNumber + " - " + scoutData.scenarioName + " | " + scoutData.totalSpawnCount + " Gegner");
        }

        if (builder.Length <= 0)
            return "";

        return "\n\n" + builder.ToString().TrimEnd();
    }

    private void CreateCurrentWaveResult(WaveData waveData)
    {
        currentWaveResult = new WaveCompletionResult();
        currentWaveResult.InitializeFromWaveData(waveData);

        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager != null)
            manager.ApplySnapshotToWaveResult(currentWaveResult);

        currentWaveResult.goldRewardMultiplierAtWaveStart = GetTotalGoldRewardMultiplier();
        currentWaveResult.xpRewardMultiplierAtWaveStart = GetTotalXPRewardMultiplier();

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
            stats.BeginWaveTracking(currentWaveResult);
    }

    private void FinalizeCurrentWaveResult()
    {
        if (currentWaveResult == null)
            return;

        currentWaveResult.MarkCompleted();
        lastCompletedWaveResult = currentWaveResult;
        EnsureWaveHistory();
        waveHistory.AddResult(currentWaveResult);
        RaiseWaveCompletedEvent(currentWaveResult);
        Debug.Log("Wave Result: " + currentWaveResult.GetDebugSummary());
        Debug.Log(waveHistory.GetDebugSummary());
    }

    private void RegisterEliteWaveCompletionIfNeeded()
    {
        if (lastCompletedWaveResult == null || !lastCompletedWaveResult.isEliteWave)
            return;

        if (enemySpawner != null)
            enemySpawner.RegisterEliteWaveCompleted(lastCompletedWaveResult);

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
            stats.RecordEliteWaveCompleted(lastCompletedWaveResult);
    }

    public void RegisterEnemyKilled(Enemy enemy)
    {
        if (enemy == null || currentWaveResult == null)
            return;

        currentWaveResult.RegisterEnemyKilled(enemy);
    }

    public void EnemyReachedBase(Enemy enemy)
    {
        if (enemy == null)
            return;

        bool isEliteEnemy = enemy.isElite || enemy.enemyRole == EnemyRole.Elite;
        int damage = isEliteEnemy ? Mathf.Max(1, eliteLeakLifeDamage) : Mathf.Max(1, enemy.baseDamage);

        if (currentWaveResult != null)
            currentWaveResult.RegisterEnemyReachedBase(enemy, damage);

        if (isEliteEnemy)
            HandleEliteReachedBaseTowerPenalty();

        LoseLife(damage);
    }

    private void HandleEliteReachedBaseTowerPenalty()
    {
        if (!destroyEliteLeakTower)
            return;

        bool destroyedTower = TryDestroyRandomTopEliteTower();

        if (!destroyedTower)
            Debug.LogWarning("Elite ist durchgekommen, aber es wurde kein aktiver Tower zum Zerstören gefunden.");
    }

    private bool TryDestroyRandomTopEliteTower()
    {
        List<Tower> candidates = GetTopEliteTowerCandidates(5);

        if (candidates.Count == 0)
            return false;

        Tower target = candidates[Random.Range(0, candidates.Count)];

        if (target == null)
            return false;

        string towerName = string.IsNullOrEmpty(target.towerName) ? target.name : target.towerName;

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
            stats.RecordTowerDestroyedByElite(target, waveNumber, "Elite Leak");

        if (currentWaveResult != null)
            currentWaveResult.RegisterEliteTowerDestroyed(towerName);

        if (tileManager != null)
        {
            if (target.hasBuildGridPosition)
                tileManager.UnregisterTowerPosition(target.builtGridPosition);
            else
                tileManager.UnregisterTowerPosition(target.transform.position);
        }

        MarkEliteDestroyedBuildTileFree(target);
        CloseTowerUIForModal();
        Destroy(target.gameObject);
        Debug.Log("Elite Leak: Tower zerstört - " + towerName);
        return true;
    }

    private void MarkEliteDestroyedBuildTileFree(Tower tower)
    {
        if (tower == null || tileManager == null)
            return;

        Vector2Int towerGridPosition = tower.hasBuildGridPosition
            ? tower.builtGridPosition
            : tileManager.WorldToGridPublic(tower.transform.position);

        BuildTile[] buildTiles = FindObjectsByType<BuildTile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (BuildTile buildTile in buildTiles)
        {
            if (buildTile == null)
                continue;

            if (tileManager.WorldToGridPublic(buildTile.transform.position) != towerGridPosition)
                continue;

            buildTile.isOccupied = false;
            buildTile.SetHovered(false);
            return;
        }
    }

    private List<Tower> GetTopEliteTowerCandidates(int maxCandidates)
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<Tower> candidates = new List<Tower>();

        if (towers == null)
            return candidates;

        foreach (Tower tower in towers)
        {
            if (tower != null)
                candidates.Add(tower);
        }

        candidates.Sort((a, b) => GetEliteTowerScore(b).CompareTo(GetEliteTowerScore(a)));

        int safeMax = Mathf.Max(1, maxCandidates);
        if (candidates.Count > safeMax)
            candidates.RemoveRange(safeMax, candidates.Count - safeMax);

        return candidates;
    }

    private int GetEliteTowerScore(Tower tower)
    {
        if (tower == null)
            return 0;

        float effectiveDps = tower.GetEffectiveDamage() * tower.GetEffectiveFireRate();
        int upgradeScore =
            tower.damageGoldUpgradeLevel +
            tower.rangeGoldUpgradeLevel +
            tower.fireRateGoldUpgradeLevel +
            tower.effectGoldUpgradeLevel +
            tower.damagePointUpgradeLevel * 2 +
            tower.rangePointUpgradeLevel * 2 +
            tower.fireRatePointUpgradeLevel * 2 +
            tower.effectPointUpgradeLevel * 2;

        int score = 0;
        score += Mathf.RoundToInt(effectiveDps * 20f);
        score += Mathf.RoundToInt(tower.GetEffectiveRange() * 4f);
        score += Mathf.Max(1, tower.level) * 25;
        score += Mathf.Max(0, tower.visualTier) * 20;
        score += Mathf.Max(0, upgradeScore) * 8;
        score += Mathf.Max(0, tower.totalKills) * 8;
        score += Mathf.Max(0, tower.totalAssists) * 3;
        score += Mathf.RoundToInt(Mathf.Max(0f, tower.totalDamageDealt) * 0.15f);
        return score;
    }

    public WaveCompletionResult GetCurrentWaveResult()
    {
        return currentWaveResult;
    }

    public WaveCompletionResult GetLastCompletedWaveResult()
    {
        return lastCompletedWaveResult;
    }

    private void EnsureWaveHistory()
    {
        if (waveHistory == null)
            waveHistory = new WaveHistory();
    }

    public WaveHistory GetWaveHistory()
    {
        EnsureWaveHistory();
        return waveHistory;
    }

    public void ClearWaveHistory()
    {
        EnsureWaveHistory();
        waveHistory.Clear();
    }

    private void GiveWaveCompletionReward()
    {
        lastWaveCompletionGoldReward = 0;

        if (!giveWaveCompletionGold || waveNumber <= 0 || lastWaveRewarded == waveNumber)
            return;

        lastWaveRewarded = waveNumber;
        int reward = CalculateWaveCompletionGold(waveNumber);

        if (reward <= 0)
            return;

        int finalReward = ApplyGoldRewardModifiers(reward);
        lastWaveCompletionGoldReward = finalReward;
        AddGoldPrecalculated(reward, finalReward, RunGoldSource.WaveCompletion, true);
        Debug.Log("Wave " + waveNumber + " completion reward: +" + finalReward + " Gold (Base: " + reward + ")");
    }

    private int CalculateWaveCompletionGold(int finishedWaveNumber)
    {
        int safeWave = Mathf.Max(1, finishedWaveNumber);
        int reward = baseWaveCompletionGold + safeWave * waveCompletionGoldPerWave;

        if (IsBossWave(safeWave))
            reward += bossWaveCompletionGoldBonus;
        else if (IsMiniBossWave(safeWave))
            reward += miniBossWaveCompletionGoldBonus;

        return Mathf.Max(0, reward);
    }

    public int GetWaveCompletionGoldPreview(int targetWaveNumber)
    {
        if (!giveWaveCompletionGold)
            return 0;

        return CalculateWaveCompletionGold(targetWaveNumber);
    }

    public int GetLastWaveCompletionGoldReward()
    {
        return lastWaveCompletionGoldReward;
    }

    private bool IsMiniBossWave(int targetWaveNumber)
    {
        return targetWaveNumber > 0 && targetWaveNumber % 5 == 0 && targetWaveNumber % 10 != 0;
    }

    private bool IsBossWave(int targetWaveNumber)
    {
        return targetWaveNumber > 0 && targetWaveNumber % 10 == 0;
    }

    private void HandleBaseBlocked()
    {
        currentPhase = GamePhase.Build;
        isBaseBlocked = true;
        isTimedBlockedBuildPhase = false;
        StopBlockedBuildTimer();

        if (tileManager != null)
            tileManager.SetCanBuild(false);

        Vector2Int currentBlockedPosition = Vector2Int.zero;

        if (tileManager != null)
            currentBlockedPosition = tileManager.GetBasePosition();

        bool alreadyChoseEventHere = blockedEventChosenForCurrentPosition && blockedEventPosition == currentBlockedPosition;

        if (alreadyChoseEventHere)
        {
            Debug.LogWarning("Immer noch verbaut an gleicher Position. Keine neue Event-Auswahl, Timed-Buildphase startet automatisch.");

            if (blockedEventManager != null)
                StartTimedBuildPhaseAfterBlockedEvent(blockedEventManager.timedBuildPhaseDuration);
            else
                StartTimedBuildPhaseAfterBlockedEvent(60f);

            RaiseBlockedBuildPhaseStartedEvent();
            return;
        }

        if (IsChaosJusticeChoiceOpen())
        {
            Debug.LogWarning("VERBAUT erkannt, aber Chaos/Gerechtigkeit-Auswahl ist offen. Verbau-Event wird erst nach der Boss-Entscheidung geöffnet.");
            return;
        }

        Debug.LogWarning("VERBAUT! Event-Auswahl wird geöffnet.");
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        WaveCompletionResult latestResult = lastCompletedWaveResult != null ? lastCompletedWaveResult : (waveHistory != null ? waveHistory.GetLatestResult() : null);

        if (pathTechnique != null)
        {
            int completedWaveNumber = latestResult != null ? latestResult.waveNumber : 0;
            int chaosLevel = latestResult != null ? latestResult.chaosLevelAtWaveStart : 0;
            bool hadChaosWaveBlock = latestResult != null && latestResult.hadChaosWaveBlocksAtWaveStart;
            pathTechnique.RecordBlockedCrisis(currentBlockedPosition, completedWaveNumber, chaosLevel, hadChaosWaveBlock);
        }

        RaiseBlockedBuildPhaseStartedEvent();

        if (blockedEventManager != null)
            blockedEventManager.OpenBlockedEventSelection();
        else
            Debug.LogError("BlockedEventManager fehlt im GameManager!");
    }

    public void StartTimedBuildPhaseAfterBlockedEvent(float duration)
    {
        if (isGameOver)
            return;

        StopBlockedBuildTimer();
        blockedBuildTimerCoroutine = StartCoroutine(TimedBlockedBuildPhaseCoroutine(duration));
    }

    private IEnumerator TimedBlockedBuildPhaseCoroutine(float duration)
    {
        currentPhase = GamePhase.Build;
        isBaseBlocked = true;
        isTimedBlockedBuildPhase = true;
        blockedBuildTimeRemaining = duration;

        if (tileManager != null)
            tileManager.SetCanBuild(true);

        RaiseBlockedBuildPhaseStartedEvent();
        Debug.Log("Timed Buildphase nach Verbau gestartet: " + duration + " Sekunden. Towerbau ist erlaubt, Wegbau bleibt gesperrt.");

        while (blockedBuildTimeRemaining > 0f)
        {
            if (isGameOver)
                yield break;

            blockedBuildTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        blockedBuildTimeRemaining = 0f;
        isTimedBlockedBuildPhase = false;
        blockedBuildTimerCoroutine = null;
        StartNextWave();
    }

    private void StopBlockedBuildTimer()
    {
        if (blockedBuildTimerCoroutine != null)
        {
            StopCoroutine(blockedBuildTimerCoroutine);
            blockedBuildTimerCoroutine = null;
        }

        blockedBuildTimeRemaining = 0f;
        isTimedBlockedBuildPhase = false;
    }

    public void AddGold(int amount)
    {
        AddGold(amount, true, RunGoldSource.Unknown);
    }

    public void AddGold(int amount, bool applyRewardModifiers)
    {
        AddGold(amount, applyRewardModifiers, RunGoldSource.Unknown);
    }

    public void AddGold(int amount, bool applyRewardModifiers, RunGoldSource source)
    {
        int baseAmount = Mathf.Max(0, amount);
        int finalAmount = baseAmount;

        if (applyRewardModifiers)
            finalAmount = ApplyGoldRewardModifiers(baseAmount);

        AddGoldPrecalculated(baseAmount, finalAmount, source, applyRewardModifiers);
    }

    private void AddGoldPrecalculated(int baseAmount, int finalAmount, RunGoldSource source, bool rewardModifiersApplied)
    {
        int safeBase = Mathf.Max(0, baseAmount);
        int safeFinal = Mathf.Max(0, finalAmount);

        if (safeFinal <= 0)
            return;

        gold += safeFinal;

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
            stats.RecordGoldEarned(safeFinal, safeBase, source, rewardModifiersApplied);

        Debug.Log("Gold: " + gold + " (+" + safeFinal + ")");
    }

    public int ApplyGoldRewardModifiers(int baseAmount)
    {
        int safeAmount = Mathf.Max(0, baseAmount);
        return ApplyRewardMultiplierWithRemainder(safeAmount, GetTotalGoldRewardMultiplier(), ref goldRewardModifierRemainder);
    }

    public int ApplyXPRewardModifiers(int baseAmount)
    {
        int safeAmount = Mathf.Max(0, baseAmount);
        return ApplyRewardMultiplierWithRemainder(safeAmount, GetTotalXPRewardMultiplier(), ref xpRewardModifierRemainder);
    }

    private int ApplyRewardMultiplierWithRemainder(int baseAmount, float multiplier, ref float remainder)
    {
        int safeAmount = Mathf.Max(0, baseAmount);

        if (safeAmount <= 0)
            return safeAmount;

        float safeMultiplier = Mathf.Max(0f, multiplier);
        if (safeMultiplier < 1f)
        {
            remainder = 0f;
            return Mathf.Max(0, Mathf.RoundToInt(safeAmount * safeMultiplier));
        }

        float rawAmount = safeAmount * safeMultiplier + Mathf.Max(0f, remainder);
        int finalAmount = Mathf.FloorToInt(rawAmount + 0.0001f);
        remainder = Mathf.Clamp(rawAmount - finalAmount, 0f, 0.9999f);
        return Mathf.Max(0, finalAmount);
    }

    public float GetBlockedEventRewardMultiplier()
    {
        return 1f + Mathf.Max(0, blockedEventRewardBonusStacks) * Mathf.Max(0f, blockedEventRewardBonusPerStack);
    }

    public float GetGoldTileRewardMultiplier()
    {
        return 1f + Mathf.Max(0, goldTileRewardBonusStacks) * Mathf.Max(0f, goldTileRewardBonusPerTile);
    }

    public float GetTotalGoldRewardMultiplier()
    {
        ChaosJusticeManager manager = GetChaosJusticeManager();
        float multiplier = manager != null ? manager.GetGoldRewardMultiplier() : 1f;
        return Mathf.Max(0f, multiplier * GetBlockedEventRewardMultiplier() * GetGoldTileRewardMultiplier());
    }

    public float GetTotalXPRewardMultiplier()
    {
        ChaosJusticeManager manager = GetChaosJusticeManager();
        float multiplier = manager != null ? manager.GetXPRewardMultiplier() : 1f;
        return Mathf.Max(0f, multiplier * GetBlockedEventRewardMultiplier());
    }

    public void RegisterGoldTileBuilt()
    {
        goldTileRewardBonusStacks = Mathf.Max(0, goldTileRewardBonusStacks + 1);
        Debug.Log("Gold Tile gebaut: Gold-Rewards jetzt +" + (goldTileRewardBonusStacks * goldTileRewardBonusPerTile * 100f).ToString("0") + "%.");
    }

    private void ApplyBlockedEventRewardBonusGrowthAfterCompletedWave()
    {
        int growthStacks = Mathf.Max(0, blockedEventRewardBonusGrowthStacks);

        if (growthStacks <= 0)
            return;

        blockedEventRewardBonusStacks = Mathf.Max(0, blockedEventRewardBonusStacks + growthStacks);
        Debug.Log("Weg-Kompensation: bestandene Wave +" + (growthStacks * blockedEventRewardBonusPerStack * 100f).ToString("0") + "% Gold/XP. Gesamt: +" + (blockedEventRewardBonusStacks * blockedEventRewardBonusPerStack * 100f).ToString("0") + "%.");
    }

    public void ActivateBlockedEventRewardBonusGrowth()
    {
        blockedEventRewardBonusGrowthStacks = Mathf.Max(0, blockedEventRewardBonusGrowthStacks + 1);
        Debug.Log("Weg-Kompensation aktiv: +" + (blockedEventRewardBonusPerStack * 100f).ToString("0") + "% Gold/XP pro bestandener Wave.");
    }

    public void AddBlockedEventRewardBonusStack()
    {
        blockedEventRewardBonusStacks = Mathf.Max(0, blockedEventRewardBonusStacks + 1);
        Debug.Log("Verbau-Bonus: Gold/XP-Rewards jetzt +" + (blockedEventRewardBonusStacks * blockedEventRewardBonusPerStack * 100f).ToString("0") + "%.");
    }

    public void AddEvolutionTowerBoost()
    {
        pendingEvolutionTowerBoosts = Mathf.Max(0, pendingEvolutionTowerBoosts + 1);
        Debug.Log("Evolutionspunkt erhalten. Der nächste ausgewählte Tower erhält +50% aktuelle Werte.");
    }

    public bool TryApplyPendingEvolutionToTower(Tower tower)
    {
        if (tower == null || pendingEvolutionTowerBoosts <= 0)
            return false;

        pendingEvolutionTowerBoosts--;
        tower.ApplyEvolutionBoost(0.5f);
        return true;
    }

    public void RaiseLowTowersToLevelFive()
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Tower tower in towers)
        {
            if (tower == null)
                continue;

            tower.RaiseToMinimumLevel(5);
        }
    }

    public int GrantXPToAllTowers(int baseAmount, bool applyRewardModifiers)
    {
        int finalAmount = GetFinalTowerXPRewardAmount(baseAmount, applyRewardModifiers);

        if (finalAmount <= 0)
            return 0;

        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int affectedTowers = 0;

        foreach (Tower tower in towers)
        {
            if (tower == null)
                continue;

            tower.AddXP(finalAmount);
            affectedTowers++;
        }

        Debug.Log("Elite-Reward: " + affectedTowers + " Tower erhalten +" + finalAmount + " XP.");
        return affectedTowers;
    }

    public bool GrantXPToStrongestTower(int baseAmount, bool applyRewardModifiers)
    {
        Tower tower = GetStrongestTowerForEliteReward();

        if (tower == null)
            return false;

        int finalAmount = GetFinalTowerXPRewardAmount(baseAmount, applyRewardModifiers);

        if (finalAmount <= 0)
            return false;

        tower.AddXP(finalAmount);
        Debug.Log("Elite-Reward: " + GetTowerDisplayName(tower) + " erhält +" + finalAmount + " XP.");
        return true;
    }

    private int GetFinalTowerXPRewardAmount(int baseAmount, bool applyRewardModifiers)
    {
        int safeAmount = Mathf.Max(0, baseAmount);

        if (safeAmount <= 0)
            return 0;

        return applyRewardModifiers ? ApplyXPRewardModifiers(safeAmount) : safeAmount;
    }

    public int RaiseWeakTowersByLevels(int targetLevel, int levelUps)
    {
        int safeTargetLevel = Mathf.Max(1, targetLevel);
        int safeLevelUps = Mathf.Max(1, levelUps);
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int affectedTowers = 0;
        int totalLevelUps = 0;

        foreach (Tower tower in towers)
        {
            if (tower == null || tower.level >= safeTargetLevel)
                continue;

            int gainedLevels = tower.RaiseByLevelsUpTo(safeTargetLevel, safeLevelUps);

            if (gainedLevels <= 0)
                continue;

            affectedTowers++;
            totalLevelUps += gainedLevels;
        }

        Debug.Log("Elite-Reward: " + affectedTowers + " schwache Tower gestärkt, +" + totalLevelUps + " Level gesamt.");
        return totalLevelUps;
    }

    public bool GrantUpgradePointsToStrongestTower(int amount)
    {
        Tower tower = GetStrongestTowerForEliteReward();

        if (tower == null)
            return false;

        int safeAmount = Mathf.Max(1, amount);
        tower.AddUpgradePoints(safeAmount);
        Debug.Log("Elite-Reward: " + GetTowerDisplayName(tower) + " erhält +" + safeAmount + " Upgradepunkt(e).");
        return true;
    }

    public int ApplyEliteLegacyBoostToAllTowers(float bonusPercent)
    {
        float safeBonus = Mathf.Max(0f, bonusPercent);

        if (safeBonus <= 0f)
            return 0;

        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int affectedTowers = 0;

        foreach (Tower tower in towers)
        {
            if (tower == null)
                continue;

            tower.ApplyEvolutionBoost(safeBonus);
            affectedTowers++;
        }

        Debug.Log("Elite-Reward: Elite-Erbe stärkt " + affectedTowers + " Tower um +" + (safeBonus * 100f).ToString("0.#") + "%.");
        return affectedTowers;
    }

    public int AddLivesCapped(int amount)
    {
        if (isGameOver)
            return 0;

        int safeAmount = Mathf.Max(0, amount);
        int maxLives = GetCurrentMaxLives();
        int before = lives;

        if (before >= maxLives)
        {
            Debug.Log("Lives: " + lives + " (+0, Max " + maxLives + ")");
            return 0;
        }

        lives = Mathf.Min(maxLives, lives + safeAmount);

        int applied = Mathf.Max(0, lives - before);
        Debug.Log("Lives: " + lives + " (+" + applied + ", Max " + maxLives + ")");
        return applied;
    }

    public int GetCurrentMaxLives()
    {
        if (currentStartMode == GameStartMode.Balancing)
            return Mathf.Max(1, balancingStartLives);

        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        int startLifeBonus = generalMeta != null ? Mathf.Max(0, generalMeta.GetActiveStartLifeBonus()) : 0;
        return Mathf.Max(1, normalStartLives + startLifeBonus);
    }

    public void AddEliteTileChoiceQualityBoosts(int amount)
    {
        int safeAmount = Mathf.Max(1, amount);

        ResolveOptionalInteractionReferences();

        if (pathBuildManager == null)
            return;

        pathBuildManager.AddEliteTileQualityBoosts(safeAmount);
    }

    public Tower GetStrongestTowerForEliteReward()
    {
        List<Tower> candidates = GetTopEliteTowerCandidates(1);
        return candidates.Count > 0 ? candidates[0] : null;
    }

    private string GetTowerDisplayName(Tower tower)
    {
        if (tower == null)
            return "Unbekannter Tower";

        return string.IsNullOrEmpty(tower.towerName) ? tower.name : tower.towerName;
    }

    public List<string> GetActiveRiskModifierDisplayNames()
    {
        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager == null)
            return new List<string>();

        return manager.GetSelectedRiskModifierDisplayNames();
    }

    public void ResetChaosToOneKeepingRiskModifier(int keepIndex)
    {
        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager == null)
            return;

        manager.ResetChaosToOneKeepingRiskModifierAt(keepIndex);
        RefreshWaveDebugDataAfterChaosJusticeChange();
    }

    public void BeginBlockedBaseRelocation(float buildPhaseDuration)
    {
        if (tileManager == null)
        {
            Debug.LogError("Neue Basis: TileManager fehlt.");
            StartTimedBuildPhaseAfterBlockedEvent(buildPhaseDuration);
            return;
        }

        StopBlockedBuildTimer();
        ClosePathAndBuildSelectionsForModal();
        blockedBaseRelocationPending = true;
        pendingBaseRelocationBuildPhaseDuration = Mathf.Max(0f, buildPhaseDuration);
        currentPhase = GamePhase.Build;
        isBaseBlocked = true;
        isTimedBlockedBuildPhase = false;
        tileManager.SetBaseRelocationModeActive(true);
        RaiseBlockedBuildPhaseStartedEvent();
        Debug.Log("Neue Basis: Platzierungsmodus aktiv. Linksklick setzt die Basis, Rechtsklick/Escape bricht ab.");
    }

    public void CompleteBlockedBaseRelocation()
    {
        if (!blockedBaseRelocationPending)
            return;

        blockedBaseRelocationPending = false;

        if (tileManager != null)
            tileManager.SetBaseRelocationModeActive(false);

        MarkBlockedEventChosenForCurrentPosition();
        StartTimedBuildPhaseAfterBlockedEvent(pendingBaseRelocationBuildPhaseDuration);
    }

    public void CancelBlockedBaseRelocation()
    {
        if (!blockedBaseRelocationPending)
            return;

        blockedBaseRelocationPending = false;

        if (tileManager != null)
            tileManager.SetBaseRelocationModeActive(false);

        StartTimedBuildPhaseAfterBlockedEvent(pendingBaseRelocationBuildPhaseDuration);
    }

    public bool TryCreateBlockedTeleporterBase()
    {
        if (tileManager == null)
            return false;

        int radius = Mathf.Max(1, tileManager.teleporterSearchRadius);
        bool success = tileManager.TryCreateTeleporterBase(radius);

        if (success)
            MarkBlockedEventChosenForCurrentPosition();

        return success;
    }

    public void AddLives(int amount)
    {
        if (isGameOver)
            return;

        lives += amount;
        Debug.Log("Lives: " + lives);
    }

    public int GetTowerBuildCostWithTypeScaling(int currentCost, GameObject towerPrefab, string displayName)
    {
        int safeCost = Mathf.Max(0, currentCost);

        if (!scaleTowerBuildCostByTypePurchases || safeCost <= 0)
            return safeCost;

        if (!TryResolveTowerRole(null, towerPrefab, displayName, out TowerRole role))
            return safeCost;

        return CalculateTowerTypePurchaseScaledCost(safeCost, GetTowerTypePurchaseCount(role));
    }

    public int GetTowerTypePurchaseCount(GameObject towerPrefab, string displayName)
    {
        if (!TryResolveTowerRole(null, towerPrefab, displayName, out TowerRole role))
            return 0;

        return GetTowerTypePurchaseCount(role);
    }

    private int GetTowerTypePurchaseCount(TowerRole role)
    {
        return towerBuildCountByRole.TryGetValue(role, out int count) ? Mathf.Max(0, count) : 0;
    }

    private int CalculateTowerTypePurchaseScaledCost(int currentCost, int purchaseCount)
    {
        int safeCost = Mathf.Max(0, currentCost);
        int safePurchaseCount = Mathf.Max(0, purchaseCount);

        if (!scaleTowerBuildCostByTypePurchases || safeCost <= 0 || safePurchaseCount <= 0)
            return safeCost;

        float safeIncrease = Mathf.Max(0f, towerTypePurchaseCostIncrease);

        if (safeIncrease <= 0f)
            return safeCost;

        float rawCost = safeCost * Mathf.Pow(1f + safeIncrease, safePurchaseCount);

        if (rawCost >= int.MaxValue)
            return int.MaxValue;

        return Mathf.Max(safeCost, Mathf.CeilToInt(rawCost - 0.0001f));
    }

    private void RegisterTowerTypePurchase(Tower tower, GameObject towerPrefab, string displayName)
    {
        if (!TryResolveTowerRole(tower, towerPrefab, displayName, out TowerRole role))
            return;

        towerBuildCountByRole[role] = GetTowerTypePurchaseCount(role) + 1;
    }

    private bool TryResolveTowerRole(Tower tower, GameObject towerPrefab, string displayName, out TowerRole role)
    {
        if (tower != null)
        {
            role = tower.towerRole;
            return true;
        }

        Tower prefabTower = towerPrefab != null ? towerPrefab.GetComponent<Tower>() : null;
        if (prefabTower != null)
        {
            role = prefabTower.towerRole;
            return true;
        }

        string lowerName = string.IsNullOrEmpty(displayName) ? "" : displayName.ToLowerInvariant();

        if (lowerName.Contains("basic")) { role = TowerRole.Basic; return true; }
        if (lowerName.Contains("rapid")) { role = TowerRole.Rapid; return true; }
        if (lowerName.Contains("heavy")) { role = TowerRole.Heavy; return true; }
        if (lowerName.Contains("fire")) { role = TowerRole.Fire; return true; }
        if (lowerName.Contains("slow")) { role = TowerRole.Slow; return true; }
        if (lowerName.Contains("poison")) { role = TowerRole.Poison; return true; }
        if (lowerName.Contains("sniper")) { role = TowerRole.Sniper; return true; }
        if (lowerName.Contains("alchemist")) { role = TowerRole.Alchemist; return true; }
        if (lowerName.Contains("lightning")) { role = TowerRole.Lightning; return true; }
        if (lowerName.Contains("mortar")) { role = TowerRole.Mortar; return true; }
        if (lowerName.Contains("spike")) { role = TowerRole.Spike; return true; }

        role = TowerRole.Basic;
        return false;
    }

    public bool SpendGold(int amount)
    {
        return SpendGold(amount, RunGoldSpendSource.Unknown);
    }

    public bool SpendGold(int amount, RunGoldSpendSource source)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (gold < safeAmount)
        {
            Debug.Log("Nicht genug Gold!");
            return false;
        }

        gold -= safeAmount;

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
            stats.RecordGoldSpent(safeAmount, source);

        Debug.Log("Gold: " + gold);
        return true;
    }

    public void LoseLife(int amount)
    {
        if (isGameOver)
            return;

        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount > 0)
        {
            BasicTowerMasteryManager masteryManager = GetBasicTowerMasteryManager();
            if (masteryManager != null)
                masteryManager.NotifyLivesDamaged();

            SlowTowerMasteryManager slowMasteryManager = GetSlowTowerMasteryManager();
            if (slowMasteryManager != null)
                slowMasteryManager.NotifyLivesDamaged();
        }

        lives -= safeAmount;

        if (lives <= 0)
        {
            lives = 0;
            GameOver();
        }

        Debug.Log("Lives: " + lives);
    }

    private void GameOver()
    {
        isGameOver = true;
        currentPhase = GamePhase.Wave;

        if (tileManager != null)
        {
            tileManager.SetCanBuild(false);
            tileManager.SetBaseRelocationModeActive(false);
        }

        blockedBaseRelocationPending = false;
        StopBlockedBuildTimer();
        postBossChoicePending = false;
        postEliteRewardChoicePending = false;

        if (blockedEventManager != null)
            blockedEventManager.CloseSelection();

        ClosePathAndBuildSelectionsForModal();
        CloseAuxiliaryMenusForGameOver();

        if (chaosJusticeManager != null)
            chaosJusticeManager.CloseSelectionWithoutResume();

        if (eliteRewardChoiceManager != null)
            eliteRewardChoiceManager.CloseSelectionWithoutResume();

        if (eliteSpawnWarningManager != null)
            eliteSpawnWarningManager.CloseWarningAndRestoreTime();

        RecordLastWaveStatistics(false);

        if (fireWaveBackendEvents)
            WaveEventBus.RaiseGameOverTriggered();

        Debug.Log("GAME OVER!");
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void CloseAuxiliaryMenusForGameOver()
    {
        if (chaosUnlockManager != null)
            chaosUnlockManager.CloseUnlocks();
        else if (chaosUnlockUI != null)
            chaosUnlockUI.CloseUnlocks();

        ChaosLexiconManager gameplayLexiconManager = FindObjectOfType<ChaosLexiconManager>();

        if (gameplayLexiconManager != null)
            gameplayLexiconManager.CloseLexicon();
        else if (chaosLexiconUI != null)
            chaosLexiconUI.CloseLexicon();

        CloseMainMenuLexicon();
        CloseMainMenuStatistics();
    }


    public void ResolveOptionalInteractionReferences()
    {
        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();

        if (buildSelectionUI == null)
            buildSelectionUI = FindObjectOfType<BuildSelectionUI>();

        if (pathBuildManager == null)
            pathBuildManager = FindObjectOfType<PathBuildManager>();

        if (eliteRewardChoiceManager == null)
            eliteRewardChoiceManager = FindObjectOfType<EliteRewardChoiceManager>();

        if (eliteSpawnWarningManager == null)
            eliteSpawnWarningManager = FindObjectOfType<EliteSpawnWarningManager>();

        if (towerUI == null)
            towerUI = FindObjectOfType<TowerUI>();

        if (chaosLexiconUI == null)
            chaosLexiconUI = FindObjectOfType<ChaosLexiconUI>();

        if (chaosUnlockManager == null)
            chaosUnlockManager = FindObjectOfType<ChaosUnlockManager>();

        if (chaosUnlockUI == null)
            chaosUnlockUI = FindObjectOfType<ChaosUnlockUI>();

        if (gameplayUnlockUI == null)
            gameplayUnlockUI = FindObjectOfType<ChaosUnlockRuntimeUI>();

        if (mainMenuLexiconManager == null)
            mainMenuLexiconManager = FindObjectOfType<MainMenuLexiconManager>();

        if (mainMenuLexiconUI == null)
            mainMenuLexiconUI = FindObjectOfType<MainMenuLexiconUI>();
    }

    public bool IsChaosJusticeChoiceOpen()
    {
        ChaosJusticeManager manager = GetChaosJusticeManager();
        return manager != null && manager.IsChoiceOpen;
    }

    public bool IsBlockedEventSelectionOpen()
    {
        return blockedEventManager != null && blockedEventManager.IsSelectionOpen();
    }

    public bool IsPathBuildChoiceOpen()
    {
        ResolveOptionalInteractionReferences();
        return pathBuildManager != null && pathBuildManager.IsChoiceOpen();
    }

    public bool IsEliteRewardChoiceOpen()
    {
        EliteRewardChoiceManager manager = GetEliteRewardChoiceManager();
        return manager != null && manager.IsSelectionOpen();
    }

    public bool IsEliteSpawnWarningOpen()
    {
        EliteSpawnWarningManager manager = GetEliteSpawnWarningManager();
        return manager != null && manager.IsWarningOpen();
    }

    public bool IsChaosLexiconOpen()
    {
        ChaosLexiconUI lexicon = GetChaosLexiconUI();
        return lexicon != null && lexicon.IsOpen;
    }

    public bool IsChaosUnlockOpen()
    {
        ChaosUnlockManager manager = GetChaosUnlockManager();
        if (manager != null && manager.IsOpen)
            return true;

        ChaosUnlockUI unlockUI = GetChaosUnlockUI();
        if (unlockUI != null && unlockUI.IsOpen)
            return true;

        if (gameplayUnlockUI == null)
            gameplayUnlockUI = FindObjectOfType<ChaosUnlockRuntimeUI>();

        return gameplayUnlockUI != null && gameplayUnlockUI.IsOpen;
    }

    public bool IsMainMenuLexiconOpen()
    {
        if (mainMenuLexiconManager != null && mainMenuLexiconManager.IsOpen)
            return true;

        if (mainMenuLexiconUI == null)
            mainMenuLexiconUI = FindObjectOfType<MainMenuLexiconUI>();

        return mainMenuLexiconUI != null && mainMenuLexiconUI.IsOpen;
    }

    public bool IsMainMenuStatisticsOpen()
    {
        if (mainMenuStatisticsManager != null && mainMenuStatisticsManager.IsOpen)
            return true;

        if (mainMenuStatisticsUI == null)
            mainMenuStatisticsUI = FindObjectOfType<MainMenuStatisticsUI>();

        return mainMenuStatisticsUI != null && mainMenuStatisticsUI.IsOpen;
    }

    public bool IsGameplayInputLockedByModalUI()
    {
        if (isGameOver)
            return false;

        return startMenuOpen || blockedBaseRelocationPending || IsEliteSpawnWarningOpen() || IsChaosJusticeChoiceOpen() || IsEliteRewardChoiceOpen() || IsBlockedEventSelectionOpen() || IsChaosLexiconOpen() || IsChaosUnlockOpen() || IsMainMenuLexiconOpen() || IsMainMenuStatisticsOpen();
    }

    public bool CanOpenAuxiliaryModalUI()
    {
        if (isGameOver)
            return false;

        return !startMenuOpen && !blockedBaseRelocationPending && !IsEliteSpawnWarningOpen() && !IsChaosJusticeChoiceOpen() && !IsEliteRewardChoiceOpen() && !IsBlockedEventSelectionOpen() && !IsChaosLexiconOpen() && !IsChaosUnlockOpen() && !IsMainMenuLexiconOpen() && !IsMainMenuStatisticsOpen();
    }

    public bool IsPathInputLockedByModalUI()
    {
        if (isGameOver)
            return true;

        return startMenuOpen || blockedBaseRelocationPending || isBaseBlocked || isTimedBlockedBuildPhase || IsEliteSpawnWarningOpen() || IsChaosJusticeChoiceOpen() || IsEliteRewardChoiceOpen() || IsBlockedEventSelectionOpen() || IsChaosLexiconOpen() || IsChaosUnlockOpen() || IsMainMenuLexiconOpen() || IsMainMenuStatisticsOpen();
    }

    public void ClosePathAndBuildSelectionsForModal()
    {
        ResolveOptionalInteractionReferences();

        if (pathBuildManager != null)
            pathBuildManager.CancelChoice();

        if (buildSelectionUI != null)
            buildSelectionUI.CloseSelectionPanel();

        if (buildManager != null)
            buildManager.ClearCurrentSelection();
    }

    public void CloseTowerUIForModal()
    {
        ResolveOptionalInteractionReferences();

        if (towerUI != null)
            towerUI.Close();
    }

    public void RefreshWaveDebugDataAfterChaosJusticeChange()
    {
        RefreshWaveScenarioDebug();
        RefreshWaveDataDebug();
    }

    public ChaosJusticeManager GetChaosJusticeManager()
    {
        if (chaosJusticeManager == null)
            chaosJusticeManager = FindObjectOfType<ChaosJusticeManager>();

        return chaosJusticeManager;
    }

    public RunStatisticsTracker GetRunStatisticsTracker()
    {
        if (runStatisticsTracker == null)
            runStatisticsTracker = GetComponent<RunStatisticsTracker>();

        if (runStatisticsTracker == null)
            runStatisticsTracker = FindObjectOfType<RunStatisticsTracker>();

        if (runStatisticsTracker == null && autoCreateRunStatisticsTracker)
            runStatisticsTracker = gameObject.AddComponent<RunStatisticsTracker>();

        if (runStatisticsTracker != null)
            runStatisticsTracker.Connect(this);

        return runStatisticsTracker;
    }

    public RunStatistics GetRunStatistics()
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();
        return stats != null ? stats.GetRunStatistics() : null;
    }

    private void RecordLastWaveStatistics(bool survivedWave)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats == null || currentWaveResult == null)
            return;

        stats.CompleteWaveTracking(currentWaveResult, survivedWave && !isGameOver);
    }

    public EliteRewardChoiceManager GetEliteRewardChoiceManager()
    {
        if (eliteRewardChoiceManager == null)
            eliteRewardChoiceManager = GetComponent<EliteRewardChoiceManager>();

        if (eliteRewardChoiceManager == null)
            eliteRewardChoiceManager = FindObjectOfType<EliteRewardChoiceManager>();

        if (eliteRewardChoiceManager == null && autoCreateEliteRewardChoiceManager)
            eliteRewardChoiceManager = gameObject.AddComponent<EliteRewardChoiceManager>();

        if (eliteRewardChoiceManager != null)
            eliteRewardChoiceManager.Connect(this);

        return eliteRewardChoiceManager;
    }

    public EliteSpawnWarningManager GetEliteSpawnWarningManager()
    {
        if (eliteSpawnWarningManager == null)
            eliteSpawnWarningManager = GetComponent<EliteSpawnWarningManager>();

        if (eliteSpawnWarningManager == null)
            eliteSpawnWarningManager = FindObjectOfType<EliteSpawnWarningManager>();

        if (eliteSpawnWarningManager == null && autoCreateEliteSpawnWarningManager)
            eliteSpawnWarningManager = gameObject.AddComponent<EliteSpawnWarningManager>();

        if (eliteSpawnWarningManager != null)
            eliteSpawnWarningManager.Connect(this);

        return eliteSpawnWarningManager;
    }

    public void OpenEliteSpawnWarning(Enemy eliteEnemy)
    {
        if (isGameOver || eliteEnemy == null)
            return;

        EliteSpawnWarningManager manager = GetEliteSpawnWarningManager();

        if (manager != null)
            manager.OpenEliteSpawnWarning(eliteEnemy);
    }

    public void RegisterTowerBuilt(Tower tower, int cost, Vector2Int gridPosition, Vector3 worldPosition)
    {
        RegisterTowerBuilt(tower, cost, gridPosition, worldPosition, null, "");
    }

    public void RegisterTowerBuilt(Tower tower, int cost, Vector2Int gridPosition, Vector3 worldPosition, GameObject towerPrefab, string displayName)
    {
        RegisterTowerTypePurchase(tower, towerPrefab, displayName);

        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordTowerBuilt(tower, cost, waveNumber, gridPosition, worldPosition);

        TowerMasteryManager towerMasteryManager = GetTowerMasteryManager();
        if (towerMasteryManager != null)
            towerMasteryManager.HandleTowerBuilt(tower);

        BasicTowerMasteryManager basicMasteryManager = GetBasicTowerMasteryManager();
        if (basicMasteryManager != null)
            basicMasteryManager.HandleTowerBuilt(tower);

        RapidTowerMasteryManager rapidMasteryManager = GetRapidTowerMasteryManager();
        if (rapidMasteryManager != null)
            rapidMasteryManager.HandleTowerBuilt(tower);

        HeavyTowerMasteryManager heavyMasteryManager = GetHeavyTowerMasteryManager();
        if (heavyMasteryManager != null)
            heavyMasteryManager.HandleTowerBuilt(tower);

        SniperTowerMasteryManager sniperMasteryManager = GetSniperTowerMasteryManager();
        if (sniperMasteryManager != null)
            sniperMasteryManager.HandleTowerBuilt(tower);

        AlchemistTowerMasteryManager alchemistMasteryManager = GetAlchemistTowerMasteryManager();
        if (alchemistMasteryManager != null)
            alchemistMasteryManager.HandleTowerBuilt(tower);

        LightningTowerMasteryManager lightningMasteryManager = GetLightningTowerMasteryManager();
        if (lightningMasteryManager != null)
            lightningMasteryManager.HandleTowerBuilt(tower);

        FireTowerMasteryManager fireMasteryManager = GetFireTowerMasteryManager();
        if (fireMasteryManager != null)
            fireMasteryManager.HandleTowerBuilt(tower);

        PoisonTowerMasteryManager poisonMasteryManager = GetPoisonTowerMasteryManager();
        if (poisonMasteryManager != null)
            poisonMasteryManager.HandleTowerBuilt(tower);

        SlowTowerMasteryManager slowMasteryManager = GetSlowTowerMasteryManager();
        if (slowMasteryManager != null)
            slowMasteryManager.HandleTowerBuilt(tower);

        MortarTowerMasteryManager mortarMasteryManager = GetMortarTowerMasteryManager();
        if (mortarMasteryManager != null)
            mortarMasteryManager.HandleTowerBuilt(tower);

        SpikeTowerMasteryManager spikeMasteryManager = GetSpikeTowerMasteryManager();
        if (spikeMasteryManager != null)
            spikeMasteryManager.HandleTowerBuilt(tower);
    }

    public void RegisterTowerXPGained(Tower tower, int amount)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordTowerXPGained(tower, amount);
    }

    public void RegisterTowerLevelUp(Tower tower, int reachedLevel, bool gainedUpgradePoint, bool gainedMetaPoint, bool gainedVisualTier)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordTowerLevelUp(tower, reachedLevel, gainedUpgradePoint, gainedMetaPoint, gainedVisualTier);
    }

    public void RegisterTowerUpgradePointsGranted(Tower tower, int amount)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordTowerUpgradePointsGranted(tower, amount);
    }

    public void RegisterTowerGoldUpgrade(Tower tower, TowerUpgradeCategory category, int goldSpent, int newUpgradeLevel)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordGoldUpgrade(tower, category, goldSpent, newUpgradeLevel);
    }

    public void RegisterTowerPointUpgrade(Tower tower, TowerUpgradeCategory category, int pointsSpent, int newUpgradeLevel)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordPointUpgrade(tower, category, pointsSpent, newUpgradeLevel);
    }

    public void RegisterEliteRewardChoice(string rewardName)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordEliteRewardChoice(rewardName);
    }

    public ChaosLexiconUI GetChaosLexiconUI()
    {
        if (chaosLexiconUI == null)
            chaosLexiconUI = FindObjectOfType<ChaosLexiconUI>();

        return chaosLexiconUI;
    }

    public ChaosUnlockManager GetChaosUnlockManager()
    {
        if (chaosUnlockManager == null)
            chaosUnlockManager = FindObjectOfType<ChaosUnlockManager>();

        return chaosUnlockManager;
    }

    public ChaosUnlockUI GetChaosUnlockUI()
    {
        if (chaosUnlockUI == null)
            chaosUnlockUI = FindObjectOfType<ChaosUnlockUI>();

        return chaosUnlockUI;
    }

    public ChaosUnlockRuntimeUI GetGameplayUnlockUI()
    {
        if (gameplayUnlockUI == null)
            gameplayUnlockUI = FindObjectOfType<ChaosUnlockRuntimeUI>();

        return gameplayUnlockUI;
    }

    public GeneralMetaProgressionManager GetGeneralMetaProgressionManager()
    {
        if (generalMetaProgressionManager == null)
            generalMetaProgressionManager = GeneralMetaProgressionManager.GetOrCreate(this);

        if (generalMetaProgressionManager != null && generalMetaProgressionManager.gameManager == null)
            generalMetaProgressionManager.gameManager = this;

        return generalMetaProgressionManager;
    }

    public ChaosResearchProgressionManager GetChaosResearchProgressionManager()
    {
        if (chaosResearchProgressionManager == null)
            chaosResearchProgressionManager = ChaosResearchProgressionManager.GetOrCreate(this);

        if (chaosResearchProgressionManager != null && chaosResearchProgressionManager.gameManager == null)
            chaosResearchProgressionManager.gameManager = this;

        return chaosResearchProgressionManager;
    }

    public PathTechniqueProgressionManager GetPathTechniqueProgressionManager()
    {
        if (pathTechniqueProgressionManager == null)
            pathTechniqueProgressionManager = PathTechniqueProgressionManager.GetOrCreate(this);

        if (pathTechniqueProgressionManager != null && pathTechniqueProgressionManager.gameManager == null)
            pathTechniqueProgressionManager.gameManager = this;

        return pathTechniqueProgressionManager;
    }

    public EliteHuntProgressionManager GetEliteHuntProgressionManager()
    {
        if (eliteHuntProgressionManager == null)
            eliteHuntProgressionManager = EliteHuntProgressionManager.GetOrCreate(this);

        if (eliteHuntProgressionManager != null && eliteHuntProgressionManager.gameManager == null)
            eliteHuntProgressionManager.gameManager = this;

        return eliteHuntProgressionManager;
    }

    public TowerMasteryManager GetTowerMasteryManager()
    {
        if (towerMasteryManager == null)
            towerMasteryManager = TowerMasteryManager.GetOrCreate(this);

        if (towerMasteryManager != null && towerMasteryManager.gameManager == null)
            towerMasteryManager.gameManager = this;

        return towerMasteryManager;
    }

    public BasicTowerMasteryManager GetBasicTowerMasteryManager()
    {
        if (basicTowerMasteryManager == null)
            basicTowerMasteryManager = BasicTowerMasteryManager.GetOrCreate(this);

        if (basicTowerMasteryManager != null && basicTowerMasteryManager.gameManager == null)
            basicTowerMasteryManager.gameManager = this;

        return basicTowerMasteryManager;
    }

    public RapidTowerMasteryManager GetRapidTowerMasteryManager()
    {
        if (rapidTowerMasteryManager == null)
            rapidTowerMasteryManager = RapidTowerMasteryManager.GetOrCreate(this);

        if (rapidTowerMasteryManager != null && rapidTowerMasteryManager.gameManager == null)
            rapidTowerMasteryManager.gameManager = this;

        return rapidTowerMasteryManager;
    }

    public HeavyTowerMasteryManager GetHeavyTowerMasteryManager()
    {
        if (heavyTowerMasteryManager == null)
            heavyTowerMasteryManager = HeavyTowerMasteryManager.GetOrCreate(this);

        if (heavyTowerMasteryManager != null && heavyTowerMasteryManager.gameManager == null)
            heavyTowerMasteryManager.gameManager = this;

        return heavyTowerMasteryManager;
    }

    public FireTowerMasteryManager GetFireTowerMasteryManager()
    {
        if (fireTowerMasteryManager == null)
            fireTowerMasteryManager = FireTowerMasteryManager.GetOrCreate(this);

        if (fireTowerMasteryManager != null && fireTowerMasteryManager.gameManager == null)
            fireTowerMasteryManager.gameManager = this;

        return fireTowerMasteryManager;
    }

    public PoisonTowerMasteryManager GetPoisonTowerMasteryManager()
    {
        if (poisonTowerMasteryManager == null)
            poisonTowerMasteryManager = PoisonTowerMasteryManager.GetOrCreate(this);

        if (poisonTowerMasteryManager != null && poisonTowerMasteryManager.gameManager == null)
            poisonTowerMasteryManager.gameManager = this;

        return poisonTowerMasteryManager;
    }

    public SlowTowerMasteryManager GetSlowTowerMasteryManager()
    {
        if (slowTowerMasteryManager == null)
            slowTowerMasteryManager = SlowTowerMasteryManager.GetOrCreate(this);

        if (slowTowerMasteryManager != null && slowTowerMasteryManager.gameManager == null)
            slowTowerMasteryManager.gameManager = this;

        return slowTowerMasteryManager;
    }

    public SniperTowerMasteryManager GetSniperTowerMasteryManager()
    {
        if (sniperTowerMasteryManager == null)
            sniperTowerMasteryManager = SniperTowerMasteryManager.GetOrCreate(this);

        if (sniperTowerMasteryManager != null && sniperTowerMasteryManager.gameManager == null)
            sniperTowerMasteryManager.gameManager = this;

        return sniperTowerMasteryManager;
    }

    public AlchemistTowerMasteryManager GetAlchemistTowerMasteryManager()
    {
        if (alchemistTowerMasteryManager == null)
            alchemistTowerMasteryManager = AlchemistTowerMasteryManager.GetOrCreate(this);

        if (alchemistTowerMasteryManager != null && alchemistTowerMasteryManager.gameManager == null)
            alchemistTowerMasteryManager.gameManager = this;

        return alchemistTowerMasteryManager;
    }

    public LightningTowerMasteryManager GetLightningTowerMasteryManager()
    {
        if (lightningTowerMasteryManager == null)
            lightningTowerMasteryManager = LightningTowerMasteryManager.GetOrCreate(this);

        if (lightningTowerMasteryManager != null && lightningTowerMasteryManager.gameManager == null)
            lightningTowerMasteryManager.gameManager = this;

        return lightningTowerMasteryManager;
    }

    public MortarTowerMasteryManager GetMortarTowerMasteryManager()
    {
        if (mortarTowerMasteryManager == null)
            mortarTowerMasteryManager = MortarTowerMasteryManager.GetOrCreate(this);

        if (mortarTowerMasteryManager != null && mortarTowerMasteryManager.gameManager == null)
            mortarTowerMasteryManager.gameManager = this;

        return mortarTowerMasteryManager;
    }

    public SpikeTowerMasteryManager GetSpikeTowerMasteryManager()
    {
        if (spikeTowerMasteryManager == null)
            spikeTowerMasteryManager = SpikeTowerMasteryManager.GetOrCreate(this);

        if (spikeTowerMasteryManager != null && spikeTowerMasteryManager.gameManager == null)
            spikeTowerMasteryManager.gameManager = this;

        return spikeTowerMasteryManager;
    }

    public MainMenuLexiconManager GetMainMenuLexiconManager()
    {
        if (mainMenuLexiconManager == null)
            mainMenuLexiconManager = FindObjectOfType<MainMenuLexiconManager>();

        if (mainMenuLexiconManager == null && autoCreateMainMenuLexicon)
            mainMenuLexiconManager = CreateMainMenuLexiconManager();

        return mainMenuLexiconManager;
    }

    private MainMenuLexiconManager CreateMainMenuLexiconManager()
    {
        if (startMenuCanvas == null)
            startMenuCanvas = FindObjectOfType<Canvas>();

        GameObject systemObject = new GameObject("MainMenuLexiconSystem");
        MainMenuLexiconManager manager = systemObject.AddComponent<MainMenuLexiconManager>();
        MainMenuLexiconUI ui = systemObject.AddComponent<MainMenuLexiconUI>();

        manager.gameManager = this;
        manager.lexiconUI = ui;
        manager.targetCanvas = startMenuCanvas;
        manager.rootParent = startMenuRoot != null ? startMenuRoot.transform : null;

        ui.manager = manager;
        ui.targetCanvas = startMenuCanvas;
        ui.rootParent = manager.rootParent;

        mainMenuLexiconManager = manager;
        mainMenuLexiconUI = ui;
        manager.EnsureInitialized();
        return manager;
    }

    public MainMenuStatisticsManager GetMainMenuStatisticsManager()
    {
        if (mainMenuStatisticsManager == null)
            mainMenuStatisticsManager = FindObjectOfType<MainMenuStatisticsManager>();

        if (mainMenuStatisticsManager == null && autoCreateMainMenuStatistics)
            mainMenuStatisticsManager = CreateMainMenuStatisticsManager();

        return mainMenuStatisticsManager;
    }

    private MainMenuStatisticsManager CreateMainMenuStatisticsManager()
    {
        if (startMenuCanvas == null)
            startMenuCanvas = FindObjectOfType<Canvas>();

        GameObject systemObject = new GameObject("MainMenuStatisticsSystem");
        MainMenuStatisticsManager manager = systemObject.AddComponent<MainMenuStatisticsManager>();
        MainMenuStatisticsUI ui = systemObject.AddComponent<MainMenuStatisticsUI>();

        manager.gameManager = this;
        manager.statisticsUI = ui;
        manager.targetCanvas = startMenuCanvas;
        manager.rootParent = startMenuRoot != null ? startMenuRoot.transform : null;

        ui.manager = manager;
        ui.targetCanvas = startMenuCanvas;
        ui.rootParent = manager.rootParent;

        mainMenuStatisticsManager = manager;
        mainMenuStatisticsUI = ui;
        manager.EnsureInitialized();
        return manager;
    }


    public void RegisterBlockedEventChoice(string eventName, string eventType, int goldGained, int livesGained, float buildPhaseDuration)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordBlockedEventChoice(eventName, eventType, goldGained, livesGained, buildPhaseDuration);

        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();

        if (pathTechnique != null)
            pathTechnique.RecordBlockedEventChoice(eventName, eventType);
    }

    public void MarkBlockedEventChosenForCurrentPosition()
    {
        blockedEventChosenForCurrentPosition = true;

        if (tileManager != null)
            blockedEventPosition = tileManager.GetBasePosition();
    }

    private void RaiseWaveStartedEvent(WaveData waveData)
    {
        if (!fireWaveBackendEvents)
            return;

        WaveEventBus.RaiseWaveStarted(waveData);
    }

    private void RaiseWaveCompletedEvent(WaveCompletionResult result)
    {
        if (!fireWaveBackendEvents)
            return;

        WaveEventBus.RaiseWaveCompleted(result);
    }

    private void RaiseBuildPhaseStartedEvent()
    {
        if (!fireWaveBackendEvents)
            return;

        WaveEventBus.RaiseBuildPhaseStarted(GetNextWaveData());
    }

    private void RaiseBlockedBuildPhaseStartedEvent()
    {
        if (!fireWaveBackendEvents)
            return;

        WaveEventBus.RaiseBlockedBuildPhaseStarted(GetNextWaveData());
    }
}
