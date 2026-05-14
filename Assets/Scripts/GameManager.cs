using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GamePhase
{
    Build,
    Wave
}

public class GameManager : MonoBehaviour
{
    public GamePhase currentPhase = GamePhase.Build;

    [Header("References")]
    public TileManager tileManager;
    public EnemySpawner enemySpawner;
    public BlockedEventManager blockedEventManager;
    public ChaosJusticeManager chaosJusticeManager;
    public ChaosLexiconUI chaosLexiconUI;
    public ChaosUnlockManager chaosUnlockManager;
    public ChaosUnlockUI chaosUnlockUI;
    public RunStatisticsTracker runStatisticsTracker;
    public bool autoCreateRunStatisticsTracker = true;
    public BuildManager buildManager;
    public BuildSelectionUI buildSelectionUI;
    public PathBuildManager pathBuildManager;
    public TowerUI towerUI;

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

    [Header("Wave Backend Events")]
    public bool fireWaveBackendEvents = true;

    [Header("Modal / Choice State Debug")]
    public bool postBossChoicePending = false;

    [Header("Blocked Build Phase")]
    public bool isBaseBlocked = false;
    public bool isTimedBlockedBuildPhase = false;
    public float blockedBuildTimeRemaining = 0f;

    private Coroutine blockedBuildTimerCoroutine;
    private bool blockedEventChosenForCurrentPosition = false;
    private Vector2Int blockedEventPosition;

    [Header("Gold")]
    public int gold = 100;

    [Header("Wave Completion Rewards")]
    public bool giveWaveCompletionGold = true;
    public int baseWaveCompletionGold = 7;
    public int waveCompletionGoldPerWave = 1;
    public int miniBossWaveCompletionGoldBonus = 10;
    public int bossWaveCompletionGoldBonus = 25;

    private int lastWaveRewarded = 0;
    private int lastWaveCompletionGoldReward = 0;

    [Header("Lives")]
    public int lives = 15;
    public bool isGameOver = false;

    private void Start()
    {
        currentPhase = GamePhase.Build;

        if (tileManager != null)
            tileManager.SetCanBuild(true);

        EnsureWaveHistory();
        ResolveOptionalInteractionReferences();
        GetChaosJusticeManager();
        GetRunStatisticsTracker();
        GetChaosUnlockManager();
        RefreshWaveScenarioDebug();
        RefreshWaveDataDebug();
        RaiseBuildPhaseStartedEvent();
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
        if (isGameOver || isBaseBlocked || currentPhase != GamePhase.Build || IsGameplayInputLockedByModalUI())
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
        if (isGameOver || IsChaosJusticeChoiceOpen())
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
        enemySpawner.StartWave(currentWaveData, OnWaveFinished);
        RefreshNextWaveDataDebug();
        RaiseWaveStartedEvent(currentWaveData);

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
        GiveWaveCompletionReward();
        currentPhase = GamePhase.Build;
        RefreshWaveScenarioDebug();
        RefreshWaveDataDebug();

        if (TryOpenChaosJusticeChoiceAfterWave())
        {
            postBossChoicePending = true;
            return;
        }

        CompletePostWaveBuildPhase();
    }

    private void CompletePostWaveBuildPhase()
    {
        if (isGameOver)
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

    private bool TryOpenChaosJusticeChoiceAfterWave()
    {
        if (lastCompletedWaveResult == null || !lastCompletedWaveResult.isBossWave || !lastCompletedWaveResult.waveCompleted)
            return false;

        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager == null || !manager.isActiveAndEnabled)
            return false;

        return manager.TryOpenBossChoice(lastCompletedWaveResult);
    }

    private int CalculateEnemyCountForWave(int targetWaveNumber)
    {
        int safeWave = Mathf.Max(1, targetWaveNumber);
        int baseCount = Mathf.Max(8, baseEnemyCount);

        switch (safeWave)
        {
            case 1: return 8;
            case 2: return 10;
            case 3: return 12;
            case 4: return 14;
            case 5: return 15;
            case 6: return 17;
            case 7: return 19;
            case 8: return 21;
            case 9: return 24;
            case 10: return 16;
        }

        if (safeWave == 11)
            return 22;

        WaveScenario scenario = GetWaveScenarioForWave(safeWave);
        int wavesAfterTen = safeWave - 10;
        int tenWaveBlock = wavesAfterTen / 10;
        int blockBase = baseCount + 12 + tenWaveBlock * 8;
        int calculatedCount;

        switch (scenario)
        {
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

        int softCap = 70 + tenWaveBlock * 10;
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

        return enemySpawner.GetScenarioNameForWave(Mathf.Max(1, waveNumber));
    }

    public string GetNextWaveScenarioName()
    {
        if (enemySpawner == null)
            return GetNextWaveScenario().ToString();

        return enemySpawner.GetScenarioNameForWave(waveNumber + 1);
    }

    private WaveData BuildWaveDataForGameWave(int targetWaveNumber)
    {
        if (enemySpawner == null)
            return null;

        int safeWave = Mathf.Max(1, targetWaveNumber);
        int enemyCount = CalculateEnemyCountForWave(safeWave);
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

        string modifierText = string.IsNullOrEmpty(data.modifierSummary)
            ? ""
            : "\nAktive Risiken: " + data.modifierSummary;

        string chaosWaveText = string.IsNullOrEmpty(data.chaosWaveSummary)
            ? ""
            : "\nChaos-Wave: " + data.chaosWaveName + " | " + data.chaosWaveSummary;

        string chaosVariantText = string.IsNullOrEmpty(data.chaosVariantSummary)
            ? ""
            : "\nChaos-Varianten: " + data.chaosVariantSummary;

        string chaosLevelText = "";
        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager != null && manager.GetChaosLevel() > 0)
        {
            chaosLevelText = "\nChaos-Einfluss: Haltbarkeit erhöht. Keine globale Speed-/BaseDamage-Erhöhung.";

            if (manager.AreChaosVariantsUnlocked())
                chaosLevelText += " Chaos-Varianten ab Chaos 3 möglich.";
        }

        return
            "Wave " + data.waveNumber + " - " + data.scenarioName +
            "\nGesamt: " + data.totalSpawnCount + " Gegner" +
            "\nHinweis: " + data.specialHint +
            modifierText +
            chaosLevelText +
            chaosWaveText +
            chaosVariantText +
            "\n\n" + enemyPreview;
    }

    private void CreateCurrentWaveResult(WaveData waveData)
    {
        currentWaveResult = new WaveCompletionResult();
        currentWaveResult.InitializeFromWaveData(waveData);

        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager != null)
            manager.ApplySnapshotToWaveResult(currentWaveResult);
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

        int damage = Mathf.Max(1, enemy.baseDamage);

        if (currentWaveResult != null)
            currentWaveResult.RegisterEnemyReachedBase(enemy, damage);

        LoseLife(damage);
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
            tileManager.SetCanBuild(false);

        RaiseBlockedBuildPhaseStartedEvent();
        Debug.Log("Timed Buildphase nach Verbau gestartet: " + duration + " Sekunden.");

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

        ChaosJusticeManager manager = GetChaosJusticeManager();

        if (manager == null)
            return safeAmount;

        return manager.ApplyGoldRewardModifiers(safeAmount);
    }

    public void AddLives(int amount)
    {
        if (isGameOver)
            return;

        lives += amount;
        Debug.Log("Lives: " + lives);
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

        lives -= amount;

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
            tileManager.SetCanBuild(false);

        StopBlockedBuildTimer();
        postBossChoicePending = false;

        if (blockedEventManager != null)
            blockedEventManager.CloseSelection();

        ClosePathAndBuildSelectionsForModal();

        if (chaosJusticeManager != null)
            chaosJusticeManager.CloseSelectionWithoutResume();

        if (fireWaveBackendEvents)
            WaveEventBus.RaiseGameOverTriggered();

        Debug.Log("GAME OVER!");
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


    public void ResolveOptionalInteractionReferences()
    {
        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();

        if (buildSelectionUI == null)
            buildSelectionUI = FindObjectOfType<BuildSelectionUI>();

        if (pathBuildManager == null)
            pathBuildManager = FindObjectOfType<PathBuildManager>();

        if (towerUI == null)
            towerUI = FindObjectOfType<TowerUI>();

        if (chaosLexiconUI == null)
            chaosLexiconUI = FindObjectOfType<ChaosLexiconUI>();

        if (chaosUnlockManager == null)
            chaosUnlockManager = FindObjectOfType<ChaosUnlockManager>();

        if (chaosUnlockUI == null)
            chaosUnlockUI = FindObjectOfType<ChaosUnlockUI>();
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

    public bool IsChaosLexiconOpen()
    {
        ChaosLexiconUI lexicon = GetChaosLexiconUI();
        return lexicon != null && lexicon.IsOpen;
    }

    public bool IsChaosUnlockOpen()
    {
        ChaosUnlockUI unlockUI = GetChaosUnlockUI();
        return unlockUI != null && unlockUI.IsOpen;
    }

    public bool IsGameplayInputLockedByModalUI()
    {
        if (isGameOver)
            return false;

        return IsChaosJusticeChoiceOpen() || IsBlockedEventSelectionOpen() || IsChaosLexiconOpen() || IsChaosUnlockOpen();
    }

    public bool IsPathInputLockedByModalUI()
    {
        if (isGameOver)
            return true;

        return IsChaosJusticeChoiceOpen() || IsBlockedEventSelectionOpen() || IsChaosLexiconOpen() || IsChaosUnlockOpen();
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

    public void RegisterTowerBuilt(Tower tower, int cost, Vector2Int gridPosition, Vector3 worldPosition)
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats != null)
            stats.RecordTowerBuilt(tower, cost, waveNumber, gridPosition, worldPosition);
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
