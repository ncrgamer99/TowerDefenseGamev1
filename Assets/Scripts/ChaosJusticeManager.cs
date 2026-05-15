using System.Collections.Generic;
using UnityEngine;

public enum ChaosJusticeChoiceType
{
    None,
    OpenJusticeSubChoice,
    OpenChaosSubChoice,
    GoldJustice,
    XpJustice,
    ChaosRiskModifier,
    NoRiskModifier,
    BackToMainChoice
}

public enum JusticeTrackType
{
    None,
    GoldJustice,
    XpJustice
}

public enum ChaosJusticeChoiceStep
{
    MainChoice,
    JusticeSubChoice,
    ChaosRiskSubChoice
}

[System.Serializable]
public class ChaosJusticeChoiceOption
{
    public ChaosJusticeChoiceType choiceType = ChaosJusticeChoiceType.None;
    public string displayName = "";
    public string description = "";
    public bool isEnabled = true;
    public WaveModifier modifier;
    public bool closesChoice = true;
}

[System.Serializable]
public class ChaosJusticeChoiceRecord
{
    public int afterBossWaveNumber = 0;
    public ChaosJusticeChoiceType choiceType = ChaosJusticeChoiceType.None;
    public JusticeTrackType justiceTrack = JusticeTrackType.None;
    public string displayName = "";
    public string modifierName = "";
    public bool bossDefeatedBeforeChoice = false;
    public bool bossWaveCompletedBeforeChoice = false;
    public int chaosLevelAfterChoice = 0;
    public int goldJusticeLevelAfterChoice = 0;
    public int xpJusticeLevelAfterChoice = 0;
    public int modifierLevelAfterChoice = -1;
    public bool noModifierChosen = false;
}

[System.Serializable]
public class ChaosJusticeRunData
{
    [Header("Core")]
    public int chaosLevel = 0;
    public int highestChaosLevel = 0;
    public int maxChaosLevel = 5;

    [Header("Justice")]
    public int goldJusticeLevel = 0;
    public int xpJusticeLevel = 0;
    public JusticeTrackType lastChosenJustice = JusticeTrackType.None;
    public List<JusticeTrackType> justiceSelectionHistory = new List<JusticeTrackType>();

    [Header("Chaos Modifiers")]
    public List<WaveModifier> selectedRiskModifiers = new List<WaveModifier>();
    public List<string> selectedRiskModifierNames = new List<string>();

    [Header("Result Prep")]
    public int bossChoicesOpened = 0;
    public int bossChoicesResolved = 0;
    public int chaosWavesSurvived = 0;
    public int bossKillsDuringRun = 0;

    [Header("Choice History")]
    public List<ChaosJusticeChoiceRecord> choiceHistory = new List<ChaosJusticeChoiceRecord>();
}


[System.Serializable]
public class ChaosJusticeBalanceSnapshot
{
    public int safetyScore = 0;
    public int chaosScore = 0;
    public int totalScore = 0;
    public int safetyPercent = 100;
    public int chaosPercent = 0;
    public string label = "Stabil";
    public string barText = "";
}

public class ChaosJusticeManager : MonoBehaviour
{
    [Header("Phase 6 Version Check")]
    public string phase6Version = "Phase6 V1 Step14 - Grund-Freischaltungen Content-Pool V1 - 2026-05-13";

    [Header("References")]
    public GameManager gameManager;
    public EnemySpawner enemySpawner;
    public ChaosJusticeChoiceUI choiceUI;
    public ChaosUnlockManager chaosUnlockManager;
    public BuildManager buildManager;
    public BuildSelectionUI buildSelectionUI;
    public PathBuildManager pathBuildManager;
    public BlockedEventManager blockedEventManager;
    public TowerUI towerUI;

    [Header("V1 Settings")]
    public bool openChoiceAfterBossWaves = true;
    public int maxChaosLevel = 5;
    public float goldJusticeBonusPerLevel = 0.02f;
    public float xpJusticeBonusPerLevel = 0.02f;

    [Header("Risk Modifier Pool")]
    public bool useInspectorRiskModifierPool = false;
    public List<WaveModifier> riskModifierPool = new List<WaveModifier>();
    public bool useUnlockGateForRiskModifierPool = true;

    [Header("Risk Offer Selection V1")]
    public bool useCompletelyRandomRiskSelection = false;
    public bool allowImmediateRiskRepeats = false;
    public bool logRandomRiskSelection = false;
    public int riskOfferCount = 3;
    public bool allowDuplicateRiskCardsInSameOffer = false;
    public bool showNoModifierOption = true;
    public bool allowNoModifierAtMaxChaos = true;

    [Header("Risk Reward Safety V1")]
    public bool useRewardRiskDiminishingReturns = true;
    public float rewardRiskDiminishingFactor = 0.65f;
    public float maxChaosRewardBonus = 0.75f;
    public float maxSingleRiskRewardBonus = 0.25f;

    [Header("Risk Level Caps V1")]
    public float maxEnemyCountMultiplierPerRisk = 1.65f;
    public int maxFlatEnemyBonusPerRisk = 12;
    public int maxExtraRoleAmountPerRisk = 10;
    public float minSpawnDelayMultiplierPerRisk = 0.55f;
    public float maxChaosVariantChanceBonusPerRisk = 0.30f;
    public int maxChaosVariantFlatBonusPerRisk = 5;
    public int maxChaosWaveBlockStrengthBonusPerRisk = 3;
    public float maxChaosWaveBlockChanceBonusPerRisk = 0.20f;

    [Header("Fair Risk Selection V1 - Legacy / Optional")]
    public bool useFairRiskRotation = true;
    public bool avoidDuplicateRiskModifiersUntilPoolExhausted = true;
    public bool includeRiskSummaryInChoiceText = true;
    public bool includeFairnessNotesInChoiceText = true;
    public bool allowHiddenPreviewModifiersInV1 = false;
    public bool randomizeFirstRiskOffer = true;
    public bool avoidOfferingSameRiskTwiceInARow = true;
    public bool avoidPreviouslyOfferedRiskModifiersUntilPoolExhausted = true;

    [Header("Expanded V1 Risk Pool")]
    public bool filterRiskModifiersByAvailablePrefabs = true;
    public bool includeMageAndLearnerRiskModifiers = true;
    public bool includeMixedRoleRiskModifiers = true;
    public bool includeAllRounderRiskModifierWhenPrefabExists = true;
    public bool includeMiniBossPressureRiskModifier = true;
    public bool includePreBossPressureRiskModifier = true;
    public bool includeAdditionalRewardRiskVariants = true;
    public bool includeChaosVariantRiskModifiers = true;
    public bool includeChaosWaveBlockRiskModifiers = true;

    [Header("Modal Safety / Fairness Guards V1")]
    public bool blockChoiceWhileGameOver = true;
    public bool forceTileBuildLockWhileChoiceOpen = true;
    public bool closePathChoiceWhenOpening = true;
    public bool closeBlockedEventSelectionWhenOpening = true;
    public bool clearTowerBuildSelectionWhenOpening = true;
    public bool closeBuildSelectionPanelWhenOpening = true;
    public bool closeTowerUIWhenOpening = true;
    public bool refreshPreviewAfterChoice = true;

    [Header("Balance / Pressure Display V1")]
    public int baseSafetyScore = 6;
    public int justiceScorePerLevel = 2;
    public int chaosScorePerLevel = 2;
    public int riskScorePerFlatEnemy = 1;
    public int riskScorePerExtraLightEnemy = 1;
    public int riskScorePerExtraHeavyEnemy = 2;
    public int balanceBarWidth = 18;
    public int maxRiskDetailsInHud = 5;

    [Header("Runtime Debug")]
    public ChaosJusticeRunData runData = new ChaosJusticeRunData();
    public bool selectionOpen = false;
    public ChaosJusticeChoiceStep currentChoiceStep = ChaosJusticeChoiceStep.MainChoice;
    public bool chaosCommittedForCurrentChoice = false;
    public int riskRotationCursor = -1;
    public string lastOfferedRiskModifierName = "";
    public List<string> offeredRiskModifierNames = new List<string>();

    private readonly List<ChaosJusticeChoiceOption> currentOptions = new List<ChaosJusticeChoiceOption>();
    private readonly List<WaveModifier> currentRiskOffers = new List<WaveModifier>();
    private WaveCompletionResult pendingBossResult;
    private System.Random riskRandom;

    public bool IsChoiceOpen => selectionOpen;

    private void Awake()
    {
        if (runData == null)
            runData = new ChaosJusticeRunData();

        runData.maxChaosLevel = Mathf.Max(1, maxChaosLevel);

        if (offeredRiskModifierNames == null)
            offeredRiskModifierNames = new List<string>();

        if (runData.justiceSelectionHistory == null)
            runData.justiceSelectionHistory = new List<JusticeTrackType>();

        if (riskRandom == null)
            riskRandom = new System.Random(System.Environment.TickCount ^ GetInstanceID());

        if (riskRotationCursor < 0)
            riskRotationCursor = randomizeFirstRiskOffer ? Random.Range(0, 1000000) : 0;
    }

    private void Start()
    {
        ResolveReferences();
        Debug.Log("ChaosJusticeManager Version: " + phase6Version);
        ApplyPreparedModifiersToSpawner();

        if (choiceUI != null)
            choiceUI.Connect(this);
    }

    private void OnEnable()
    {
        WaveEventBus.WaveCompleted += HandleWaveCompletedForResultPrep;
        WaveEventBus.GameOverTriggered += HandleGameOverTriggered;
    }

    private void OnDisable()
    {
        WaveEventBus.WaveCompleted -= HandleWaveCompletedForResultPrep;
        WaveEventBus.GameOverTriggered -= HandleGameOverTriggered;
    }

    private void Update()
    {
        if (!selectionOpen)
            return;

        if (blockChoiceWhileGameOver && gameManager != null && gameManager.isGameOver)
        {
            CloseSelectionWithoutResume();
            return;
        }

        if (forceTileBuildLockWhileChoiceOpen && gameManager != null && gameManager.tileManager != null)
            gameManager.tileManager.SetCanBuild(false);

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ChooseOption(0);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            ChooseOption(1);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            ChooseOption(2);

        if (Input.GetKeyDown(KeyCode.Alpha4))
            ChooseOption(3);
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (enemySpawner == null)
        {
            if (gameManager != null)
                enemySpawner = gameManager.enemySpawner;

            if (enemySpawner == null)
                enemySpawner = FindObjectOfType<EnemySpawner>();
        }

        if (chaosUnlockManager == null && gameManager != null)
            chaosUnlockManager = gameManager.chaosUnlockManager;

        if (chaosUnlockManager == null)
            chaosUnlockManager = FindObjectOfType<ChaosUnlockManager>();

        if (choiceUI == null)
            choiceUI = FindObjectOfType<ChaosJusticeChoiceUI>();

        ResolveOptionalInteractionReferences();
    }


    private void ResolveOptionalInteractionReferences()
    {
        if (gameManager != null)
        {
            if (buildManager == null)
                buildManager = gameManager.buildManager;

            if (buildSelectionUI == null)
                buildSelectionUI = gameManager.buildSelectionUI;

            if (pathBuildManager == null)
                pathBuildManager = gameManager.pathBuildManager;

            if (blockedEventManager == null)
                blockedEventManager = gameManager.blockedEventManager;

            if (towerUI == null)
                towerUI = gameManager.towerUI;
        }

        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();

        if (buildSelectionUI == null)
            buildSelectionUI = FindObjectOfType<BuildSelectionUI>();

        if (pathBuildManager == null)
            pathBuildManager = FindObjectOfType<PathBuildManager>();

        if (blockedEventManager == null)
            blockedEventManager = FindObjectOfType<BlockedEventManager>();

        if (towerUI == null)
            towerUI = FindObjectOfType<TowerUI>();
    }

    private void ApplyModalLockBeforeOpeningChoice()
    {
        ResolveReferences();
        ResolveOptionalInteractionReferences();

        if (closeBlockedEventSelectionWhenOpening && blockedEventManager != null)
            blockedEventManager.CloseSelection();

        if (closePathChoiceWhenOpening && pathBuildManager != null)
            pathBuildManager.CancelChoice();

        if (closeBuildSelectionPanelWhenOpening && buildSelectionUI != null)
            buildSelectionUI.CloseSelectionPanel();

        if (clearTowerBuildSelectionWhenOpening && buildManager != null)
            buildManager.ClearCurrentSelection();

        if (closeTowerUIWhenOpening && towerUI != null)
            towerUI.Close();

        if (forceTileBuildLockWhileChoiceOpen && gameManager != null && gameManager.tileManager != null)
            gameManager.tileManager.SetCanBuild(false);
    }

    public bool TryOpenBossChoice(WaveCompletionResult bossResult)
    {
        if (!openChoiceAfterBossWaves)
            return false;

        if (blockChoiceWhileGameOver && gameManager != null && gameManager.isGameOver)
            return false;

        if (bossResult == null || !bossResult.isBossWave || !bossResult.waveCompleted)
            return false;

        if (selectionOpen)
            return true;

        ResolveReferences();
        ApplyModalLockBeforeOpeningChoice();
        pendingBossResult = bossResult;
        selectionOpen = true;
        runData.bossChoicesOpened++;

        GenerateCurrentOptions();

        if (gameManager != null && gameManager.tileManager != null)
            gameManager.tileManager.SetCanBuild(false);

        if (choiceUI != null)
            choiceUI.OpenSelection(currentOptions);

        Debug.Log("Chaos/Gerechtigkeit-Auswahl geöffnet nach Boss-Wave " + bossResult.waveNumber + "\n" + GetCurrentChoiceDebugText());
        return true;
    }

    private void GenerateCurrentOptions()
    {
        currentOptions.Clear();
        currentRiskOffers.Clear();

        if (runData.chaosLevel >= runData.maxChaosLevel)
        {
            currentChoiceStep = ChaosJusticeChoiceStep.ChaosRiskSubChoice;
            chaosCommittedForCurrentChoice = true;
            GenerateChaosRiskSubOptions();
            return;
        }

        currentChoiceStep = ChaosJusticeChoiceStep.MainChoice;
        chaosCommittedForCurrentChoice = false;
        GenerateMainChoiceOptions();
    }

    private void GenerateMainChoiceOptions()
    {
        currentOptions.Clear();

        currentOptions.Add(CreateOpenJusticeSubChoiceOption());
        currentOptions.Add(CreateOpenChaosSubChoiceOption());
    }

    private void GenerateJusticeSubOptions()
    {
        currentChoiceStep = ChaosJusticeChoiceStep.JusticeSubChoice;
        currentOptions.Clear();

        currentOptions.Add(CreateGoldJusticeOption());
        currentOptions.Add(CreateXpJusticeOption());
        currentOptions.Add(CreateBackToMainOption("Zurück", "Zurück zur Hauptentscheidung zwischen Gerechtigkeit und Chaos."));
    }

    private void GenerateChaosRiskSubOptions()
    {
        currentChoiceStep = ChaosJusticeChoiceStep.ChaosRiskSubChoice;
        currentOptions.Clear();
        currentRiskOffers.Clear();

        List<WaveModifier> offers = GenerateRandomRiskOffers(Mathf.Max(1, riskOfferCount));

        foreach (WaveModifier offer in offers)
        {
            if (offer == null)
                continue;

            currentRiskOffers.Add(offer.CreateCopy());
            currentOptions.Add(CreateChaosRiskOption(offer));
        }

        if (showNoModifierOption && (allowNoModifierAtMaxChaos || runData.chaosLevel < runData.maxChaosLevel))
            currentOptions.Add(CreateNoModifierOption());

        if (currentOptions.Count == 0)
            currentOptions.Add(CreateNoModifierOption());
    }

    private ChaosJusticeChoiceOption CreateOpenJusticeSubChoiceOption()
    {
        int generalJustice = runData.goldJusticeLevel + runData.xpJusticeLevel;

        return new ChaosJusticeChoiceOption
        {
            choiceType = ChaosJusticeChoiceType.OpenJusticeSubChoice,
            displayName = "Gerechtigkeit festigen",
            description =
                "Sicherer Weg. Du wählst danach Gold- oder XP-Gerechtigkeit. " +
                "Kein zusätzliches Risiko. Aktuelle allgemeine Gerechtigkeit: " + generalJustice + ".",
            isEnabled = true,
            modifier = null,
            closesChoice = false
        };
    }

    private ChaosJusticeChoiceOption CreateOpenChaosSubChoiceOption()
    {
        int nextChaosLevel = Mathf.Min(runData.maxChaosLevel, runData.chaosLevel + 1);

        return new ChaosJusticeChoiceOption
        {
            choiceType = ChaosJusticeChoiceType.OpenChaosSubChoice,
            displayName = "Chaos entfesseln",
            description =
                "Riskanter Weg. Danach erscheinen 3 zufällige Risiko-Modifikatoren und 'Kein Modifikator'. " +
                "Nach dieser Entscheidung ist kein Wechsel zurück zur Gerechtigkeit mehr möglich. " +
                "Chaos steigt auf Level " + nextChaosLevel + ".",
            isEnabled = runData.chaosLevel < runData.maxChaosLevel,
            modifier = null,
            closesChoice = false
        };
    }

    private ChaosJusticeChoiceOption CreateBackToMainOption(string title, string description)
    {
        return new ChaosJusticeChoiceOption
        {
            choiceType = ChaosJusticeChoiceType.BackToMainChoice,
            displayName = title,
            description = description,
            isEnabled = true,
            modifier = null,
            closesChoice = false
        };
    }

    private ChaosJusticeChoiceOption CreateGoldJusticeOption()
    {
        int nextLevel = runData.goldJusticeLevel + 1;
        int nextBonusPercent = Mathf.RoundToInt(nextLevel * goldJusticeBonusPerLevel * 100f);

        return new ChaosJusticeChoiceOption
        {
            choiceType = ChaosJusticeChoiceType.GoldJustice,
            displayName = "Gold-Gerechtigkeit",
            description = "Sicherer Weg. Erhöht zukünftige Gold-Belohnungen auf Stufe " + nextLevel + " (gesamt ca. +" + nextBonusPercent + "%).",
            isEnabled = true,
            modifier = null,
            closesChoice = true
        };
    }

    private ChaosJusticeChoiceOption CreateXpJusticeOption()
    {
        int nextLevel = runData.xpJusticeLevel + 1;
        int nextBonusPercent = Mathf.RoundToInt(nextLevel * xpJusticeBonusPerLevel * 100f);

        return new ChaosJusticeChoiceOption
        {
            choiceType = ChaosJusticeChoiceType.XpJustice,
            displayName = "XP-Gerechtigkeit",
            description = "Sicherer Weg. Erhöht zukünftige Tower-XP-Belohnungen auf Stufe " + nextLevel + " (gesamt ca. +" + nextBonusPercent + "%).",
            isEnabled = true,
            modifier = null,
            closesChoice = true
        };
    }

    private ChaosJusticeChoiceOption CreateChaosOption()
    {
        // Legacy-Fallback: bleibt für alte Debug-Aufrufe erhalten, wird in der neuen UI-Struktur nicht mehr direkt genutzt.
        List<WaveModifier> offers = GenerateRandomRiskOffers(1);
        WaveModifier modifier = offers.Count > 0 ? offers[0] : null;

        if (modifier == null)
            return CreateNoModifierOption();

        return CreateChaosRiskOption(modifier);
    }

    private ChaosJusticeChoiceOption CreateChaosRiskOption(WaveModifier modifier)
    {
        if (modifier == null)
            return CreateNoModifierOption();

        int nextRiskLevel = GetNextRiskModifierLevel(modifier.displayName);
        WaveModifier previewModifier = CreateLeveledModifierCopy(modifier, nextRiskLevel);

        return new ChaosJusticeChoiceOption
        {
            choiceType = ChaosJusticeChoiceType.ChaosRiskModifier,
            displayName = "Risiko: " + previewModifier.GetDisplayNameWithLevel(),
            description = BuildChaosRiskChoiceDescription(previewModifier, GetNextChaosLevelPreview(), nextRiskLevel),
            isEnabled = true,
            modifier = previewModifier,
            closesChoice = true
        };
    }

    private ChaosJusticeChoiceOption CreateNoModifierOption()
    {
        int nextChaosLevel = GetNextChaosLevelPreview();
        bool chaosCanRise = runData.chaosLevel < runData.maxChaosLevel;
        string chaosText = chaosCanRise
            ? "Chaos steigt auf Level " + nextChaosLevel + "."
            : "Chaos bleibt bewusst auf dem V1-Maximum " + runData.maxChaosLevel + ".";
        string displayName = chaosCanRise ? "Kein Modifikator" : "Chaos halten";
        string noRiskText = chaosCanRise
            ? "Es wird kein Risiko-Modifikator aktiviert und keine Gerechtigkeit reduziert. Keine Zusatzbelohnung."
            : "Bewusste Safe-Option am Chaos-Maximum: kein neues Risiko, kein Gerechtigkeits-Rückbau, keine Zusatzbelohnung.";

        return new ChaosJusticeChoiceOption
        {
            choiceType = ChaosJusticeChoiceType.NoRiskModifier,
            displayName = displayName,
            description = chaosText + " " + noRiskText,
            isEnabled = chaosCanRise || allowNoModifierAtMaxChaos,
            modifier = null,
            closesChoice = true
        };
    }

    private int GetNextChaosLevelPreview()
    {
        return Mathf.Min(runData.maxChaosLevel, runData.chaosLevel + 1);
    }

    private List<WaveModifier> GenerateRandomRiskOffers(int requestedCount, bool consumeOffers = true)
    {
        List<WaveModifier> result = new List<WaveModifier>();
        List<WaveModifier> pool = GetAllowedRiskModifierPool(GetRiskModifierPool());

        if (pool == null || pool.Count == 0)
            return result;

        int safeCount = Mathf.Max(1, requestedCount);

        if (riskRandom == null)
            riskRandom = new System.Random(System.Environment.TickCount ^ GetInstanceID());

        List<WaveModifier> workingPool = new List<WaveModifier>();

        foreach (WaveModifier modifier in pool)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!allowDuplicateRiskCardsInSameOffer && ContainsModifierName(workingPool, modifier.displayName))
                continue;

            workingPool.Add(modifier);
        }

        if (workingPool.Count == 0)
            return result;

        while (result.Count < safeCount && workingPool.Count > 0)
        {
            int index = GetRiskOfferIndexFromWorkingPool(workingPool);

            if (index < 0 || index >= workingPool.Count)
                break;

            WaveModifier selected = workingPool[index];

            if (selected != null)
            {
                if (consumeOffers)
                    RememberOfferedRiskModifier(selected, index, workingPool.Count);

                WaveModifier copy = selected.CreateCopy();
                copy.riskLevel = GetNextRiskModifierLevel(copy.displayName);
                copy.timesSelected = Mathf.Max(1, copy.riskLevel + 1);
                result.Add(copy);

                if (logRandomRiskSelection)
                    Debug.Log("Chaos/Gerechtigkeit Risk Offer Card (" + GetRiskSelectionModeLabel() + "): " + copy.GetDisplayNameWithLevel());
            }

            if (!allowDuplicateRiskCardsInSameOffer)
                workingPool.RemoveAt(index);
        }

        return result;
    }

    private int GetRiskOfferIndexFromWorkingPool(List<WaveModifier> workingPool)
    {
        if (workingPool == null || workingPool.Count == 0)
            return -1;

        bool useFairPoolRules = !useCompletelyRandomRiskSelection && useFairRiskRotation;
        List<int> validIndices = GetValidRiskModifierIndices(workingPool, useFairPoolRules);

        if (validIndices.Count == 0)
            validIndices = GetValidRiskModifierIndices(workingPool, false);

        if (validIndices.Count == 0)
            return -1;

        if (useCompletelyRandomRiskSelection)
        {
            if (!allowImmediateRiskRepeats && validIndices.Count > 1)
                RemoveLastOfferedRiskFromIndexList(workingPool, validIndices);

            return validIndices[riskRandom.Next(validIndices.Count)];
        }

        if (useFairRiskRotation)
        {
            if (avoidOfferingSameRiskTwiceInARow && validIndices.Count > 1)
                RemoveLastOfferedRiskFromIndexList(workingPool, validIndices);

            if (avoidPreviouslyOfferedRiskModifiersUntilPoolExhausted && validIndices.Count > 1)
                RemovePreviouslyOfferedRiskModifiersFromIndexList(workingPool, validIndices);

            if (validIndices.Count == 0)
                validIndices = GetValidRiskModifierIndices(workingPool, false);

            int startIndex = GetRiskRotationStartIndex(workingPool);

            for (int offset = 0; offset < workingPool.Count; offset++)
            {
                int candidateIndex = (startIndex + offset) % workingPool.Count;

                if (validIndices.Contains(candidateIndex))
                    return candidateIndex;
            }
        }

        return validIndices[riskRandom.Next(validIndices.Count)];
    }

    private bool ContainsModifierName(List<WaveModifier> modifiers, string modifierName)
    {
        if (modifiers == null || string.IsNullOrEmpty(modifierName))
            return false;

        foreach (WaveModifier modifier in modifiers)
        {
            if (modifier == null)
                continue;

            if (modifier.displayName == modifierName)
                return true;
        }

        return false;
    }

    private string GetJusticeLossPreviewText()
    {
        JusticeTrackType track = PeekLastActiveJusticeTrack();

        if (track == JusticeTrackType.GoldJustice)
            return " Gerechtigkeit-Rückbau: zuletzt aufgebaute Gold-Gerechtigkeit -1.";

        if (track == JusticeTrackType.XpJustice)
            return " Gerechtigkeit-Rückbau: zuletzt aufgebaute XP-Gerechtigkeit -1.";

        return " Keine Gerechtigkeit wird reduziert, weil keine passende Stufe aktiv ist.";
    }

    private JusticeTrackType PeekLastActiveJusticeTrack()
    {
        if (runData == null)
            return JusticeTrackType.None;

        if (runData.justiceSelectionHistory != null && runData.justiceSelectionHistory.Count > 0)
        {
            for (int i = runData.justiceSelectionHistory.Count - 1; i >= 0; i--)
            {
                JusticeTrackType track = runData.justiceSelectionHistory[i];

                if (track == JusticeTrackType.GoldJustice && runData.goldJusticeLevel > 0)
                    return track;

                if (track == JusticeTrackType.XpJustice && runData.xpJusticeLevel > 0)
                    return track;
            }
        }

        if (runData.lastChosenJustice == JusticeTrackType.GoldJustice && runData.goldJusticeLevel > 0)
            return JusticeTrackType.GoldJustice;

        if (runData.lastChosenJustice == JusticeTrackType.XpJustice && runData.xpJusticeLevel > 0)
            return JusticeTrackType.XpJustice;

        return JusticeTrackType.None;
    }

    private WaveModifier GetRandomRiskModifierForChoice()
    {
        return GetRiskModifierForChoice(true);
    }

    private WaveModifier PeekRiskModifierForChoice()
    {
        return GetRiskModifierForChoice(false);
    }

    private WaveModifier GetRiskModifierForChoice(bool consumeOffer)
    {
        List<WaveModifier> pool = GetAllowedRiskModifierPool(GetRiskModifierPool());

        if (pool == null || pool.Count == 0)
            return null;

        WaveModifier selected;

        if (useCompletelyRandomRiskSelection)
            selected = GetCompletelyRandomRiskModifierFromPool(pool, consumeOffer);
        else if (useFairRiskRotation)
            selected = GetRiskModifierFromFairRotation(pool, consumeOffer);
        else
            selected = GetRandomRiskModifierFromPool(pool, consumeOffer);

        if (selected == null)
            return null;

        return selected.CreateCopy();
    }

    private List<WaveModifier> GetAllowedRiskModifierPool(List<WaveModifier> sourcePool)
    {
        List<WaveModifier> allowedPool = new List<WaveModifier>();

        if (sourcePool == null)
            return allowedPool;

        foreach (WaveModifier modifier in sourcePool)
        {
            if (!IsRiskModifierAllowedInV1(modifier))
                continue;

            allowedPool.Add(modifier);
        }

        if (useUnlockGateForRiskModifierPool)
        {
            ResolveReferences();

            if (chaosUnlockManager != null)
                allowedPool = chaosUnlockManager.FilterRiskModifierPool(allowedPool);
        }

        return allowedPool;
    }

    public List<WaveModifier> GetUnlockedRiskModifierPoolPreview()
    {
        return GetAllowedRiskModifierPool(GetRiskModifierPool());
    }

    private bool IsRiskModifierAllowedInV1(WaveModifier modifier)
    {
        if (modifier == null || !modifier.IsValid())
            return false;

        if (!allowHiddenPreviewModifiersInV1 && (modifier.hidePreview || modifier.modifierType == WaveModifierType.HiddenPreview))
            return false;

        return true;
    }

    private WaveModifier GetCompletelyRandomRiskModifierFromPool(List<WaveModifier> pool, bool consumeOffer)
    {
        if (pool == null || pool.Count == 0)
            return null;

        List<int> validIndices = GetValidRiskModifierIndices(pool, false);

        if (validIndices.Count == 0)
            return null;

        if (!allowImmediateRiskRepeats && validIndices.Count > 1)
            RemoveLastOfferedRiskFromIndexList(pool, validIndices);

        if (validIndices.Count == 0)
            return null;

        if (riskRandom == null)
            riskRandom = new System.Random(System.Environment.TickCount ^ GetInstanceID());

        int selectedListIndex = riskRandom.Next(validIndices.Count);
        int selectedPoolIndex = validIndices[selectedListIndex];
        WaveModifier selected = pool[selectedPoolIndex];

        if (consumeOffer)
        {
            RememberOfferedRiskModifier(selected, selectedPoolIndex, pool.Count);

            if (logRandomRiskSelection && selected != null)
                Debug.Log("Chaos/Gerechtigkeit Random Risk Offer: " + selected.displayName + " (" + selectedPoolIndex + "/" + pool.Count + ")");
        }

        return selected;
    }

    private WaveModifier GetRandomRiskModifierFromPool(List<WaveModifier> pool, bool consumeOffer)
    {
        if (pool == null || pool.Count == 0)
            return null;

        List<int> validIndices = GetValidRiskModifierIndices(pool, false);

        if (validIndices.Count == 0)
            return null;

        if (avoidOfferingSameRiskTwiceInARow && validIndices.Count > 1)
            RemoveLastOfferedRiskFromIndexList(pool, validIndices);

        int selectedListIndex = Random.Range(0, validIndices.Count);
        int selectedPoolIndex = validIndices[selectedListIndex];
        WaveModifier selected = pool[selectedPoolIndex];

        if (consumeOffer)
            RememberOfferedRiskModifier(selected, selectedPoolIndex, pool.Count);

        return selected;
    }

    private WaveModifier GetRiskModifierFromFairRotation(List<WaveModifier> pool, bool consumeOffer)
    {
        if (pool == null || pool.Count == 0)
            return null;

        List<int> validIndices = GetValidRiskModifierIndices(pool, true);

        if (validIndices.Count == 0)
            validIndices = GetValidRiskModifierIndices(pool, false);

        if (validIndices.Count == 0)
            return null;

        if (avoidOfferingSameRiskTwiceInARow && validIndices.Count > 1)
            RemoveLastOfferedRiskFromIndexList(pool, validIndices);

        if (avoidPreviouslyOfferedRiskModifiersUntilPoolExhausted && validIndices.Count > 1)
            RemovePreviouslyOfferedRiskModifiersFromIndexList(pool, validIndices);

        int safeCount = pool.Count;
        int startIndex = GetRiskRotationStartIndex(pool);

        for (int offset = 0; offset < safeCount; offset++)
        {
            int index = (startIndex + offset) % safeCount;

            if (!validIndices.Contains(index))
                continue;

            WaveModifier candidate = pool[index];

            if (candidate == null || !candidate.IsValid())
                continue;

            if (consumeOffer)
                RememberOfferedRiskModifier(candidate, index, safeCount);

            return candidate;
        }

        int fallbackIndex = validIndices[0];
        WaveModifier fallback = pool[fallbackIndex];

        if (consumeOffer)
            RememberOfferedRiskModifier(fallback, fallbackIndex, safeCount);

        return fallback;
    }

    private List<int> GetValidRiskModifierIndices(List<WaveModifier> pool, bool excludeAlreadySelected)
    {
        List<int> indices = new List<int>();

        if (pool == null)
            return indices;

        for (int i = 0; i < pool.Count; i++)
        {
            WaveModifier candidate = pool[i];

            if (candidate == null || !candidate.IsValid())
                continue;

            if (excludeAlreadySelected && avoidDuplicateRiskModifiersUntilPoolExhausted && HasSelectedRiskModifierName(candidate.displayName))
                continue;

            indices.Add(i);
        }

        return indices;
    }

    private int GetRiskRotationStartIndex(List<WaveModifier> pool)
    {
        if (pool == null || pool.Count == 0)
            return 0;

        int safeCount = pool.Count;
        int startIndex = riskRotationCursor >= 0
            ? riskRotationCursor % safeCount
            : (randomizeFirstRiskOffer ? Random.Range(0, safeCount) : 0);

        // V1-Fix: Der Default-Pool startet mit "Mehr Gegner". Bei frischen Tests
        // soll der erste angebotene Modifier nicht immer genau dieser erste Listeneintrag sein.
        if (safeCount > 1 && randomizeFirstRiskOffer && offeredRiskModifierNames != null && offeredRiskModifierNames.Count == 0)
        {
            if (pool[startIndex] != null && pool[startIndex].displayName == "Mehr Gegner")
                startIndex = (startIndex + 1) % safeCount;
        }

        return startIndex;
    }

    private void RemoveLastOfferedRiskFromIndexList(List<WaveModifier> pool, List<int> indices)
    {
        if (pool == null || indices == null || indices.Count <= 1)
            return;

        if (string.IsNullOrEmpty(lastOfferedRiskModifierName))
            return;

        for (int i = indices.Count - 1; i >= 0; i--)
        {
            int poolIndex = indices[i];

            if (poolIndex < 0 || poolIndex >= pool.Count)
                continue;

            WaveModifier candidate = pool[poolIndex];

            if (candidate == null)
                continue;

            if (candidate.displayName == lastOfferedRiskModifierName)
                indices.RemoveAt(i);
        }
    }

    private void RemovePreviouslyOfferedRiskModifiersFromIndexList(List<WaveModifier> pool, List<int> indices)
    {
        if (pool == null || indices == null || indices.Count <= 1)
            return;

        if (offeredRiskModifierNames == null || offeredRiskModifierNames.Count == 0)
            return;

        if (HaveAllIndexedRiskModifiersBeenOffered(pool, indices))
            return;

        for (int i = indices.Count - 1; i >= 0; i--)
        {
            int poolIndex = indices[i];

            if (poolIndex < 0 || poolIndex >= pool.Count)
                continue;

            WaveModifier candidate = pool[poolIndex];

            if (candidate == null)
                continue;

            if (HasOfferedRiskModifierName(candidate.displayName))
                indices.RemoveAt(i);
        }
    }

    private bool HaveAllIndexedRiskModifiersBeenOffered(List<WaveModifier> pool, List<int> indices)
    {
        if (pool == null || indices == null || indices.Count == 0)
            return false;

        foreach (int poolIndex in indices)
        {
            if (poolIndex < 0 || poolIndex >= pool.Count)
                continue;

            WaveModifier candidate = pool[poolIndex];

            if (candidate == null || !candidate.IsValid())
                continue;

            if (!HasOfferedRiskModifierName(candidate.displayName))
                return false;
        }

        return true;
    }

    private bool HasOfferedRiskModifierName(string modifierName)
    {
        if (string.IsNullOrEmpty(modifierName) || offeredRiskModifierNames == null)
            return false;

        foreach (string entry in offeredRiskModifierNames)
        {
            if (entry == modifierName)
                return true;
        }

        return false;
    }

    private void RememberOfferedRiskModifier(WaveModifier modifier, int selectedIndex, int poolCount)
    {
        if (offeredRiskModifierNames == null)
            offeredRiskModifierNames = new List<string>();

        if (modifier != null)
        {
            lastOfferedRiskModifierName = modifier.displayName;

            if (!HasOfferedRiskModifierName(modifier.displayName))
                offeredRiskModifierNames.Add(modifier.displayName);
        }

        int safeCount = Mathf.Max(1, poolCount);
        riskRotationCursor = (Mathf.Max(0, selectedIndex) + 1) % safeCount;
    }

    private bool HasSelectedRiskModifierName(string modifierName)
    {
        if (string.IsNullOrEmpty(modifierName))
            return false;

        if (runData == null || runData.selectedRiskModifierNames == null)
            return false;

        foreach (string selectedName in runData.selectedRiskModifierNames)
        {
            if (string.IsNullOrEmpty(selectedName))
                continue;

            if (selectedName == modifierName)
                return true;
        }

        return false;
    }

    private int GetSelectedRiskModifierIndex(string modifierName)
    {
        if (runData == null || runData.selectedRiskModifiers == null || string.IsNullOrEmpty(modifierName))
            return -1;

        for (int i = 0; i < runData.selectedRiskModifiers.Count; i++)
        {
            WaveModifier modifier = runData.selectedRiskModifiers[i];

            if (modifier == null)
                continue;

            if (modifier.displayName == modifierName)
                return i;
        }

        return -1;
    }

    private int GetCurrentRiskModifierLevel(string modifierName)
    {
        int index = GetSelectedRiskModifierIndex(modifierName);

        if (index < 0 || runData == null || runData.selectedRiskModifiers == null)
            return -1;

        WaveModifier modifier = runData.selectedRiskModifiers[index];
        return modifier != null ? Mathf.Max(0, modifier.riskLevel) : -1;
    }

    private int GetNextRiskModifierLevel(string modifierName)
    {
        int currentLevel = GetCurrentRiskModifierLevel(modifierName);

        if (currentLevel < 0)
            return 0;

        return currentLevel + 1;
    }

    private WaveModifier CreateLeveledModifierCopy(WaveModifier source, int riskLevel)
    {
        if (source == null)
            return null;

        WaveModifier copy = source.CreateCopy();
        copy.isPermanentRiskModifier = true;
        copy.isTemporaryRiskModifier = false;
        copy.riskLevel = Mathf.Max(0, riskLevel);
        copy.timesSelected = copy.riskLevel + 1;

        ApplyRiskLevelScaling(copy);
        return copy;
    }

    private void ApplyRiskLevelScaling(WaveModifier modifier)
    {
        if (modifier == null)
            return;

        int level = Mathf.Max(0, modifier.riskLevel);

        if (level <= 0)
        {
            ApplyRiskLevelCaps(modifier);
            return;
        }

        float softLevel = Mathf.Sqrt(level);
        float enemyCountScale = 1f + 0.45f * softLevel;
        float roleScale = 1f + 0.40f * softLevel;
        float rewardScale = 1f + 0.25f * softLevel;
        float spawnPressureScale = 1f + 0.35f * softLevel;

        if (modifier.enemyCountMultiplier > 1f)
        {
            float extra = modifier.enemyCountMultiplier - 1f;
            modifier.enemyCountMultiplier = 1f + extra * enemyCountScale;
        }
        else if (modifier.enemyCountMultiplier < 1f)
        {
            float reduction = 1f - modifier.enemyCountMultiplier;
            modifier.enemyCountMultiplier = Mathf.Clamp(1f - reduction * enemyCountScale, 0.25f, 1f);
        }

        if (modifier.flatEnemyCountBonus > 0)
            modifier.flatEnemyCountBonus = Mathf.Max(modifier.flatEnemyCountBonus + 1, Mathf.RoundToInt(modifier.flatEnemyCountBonus * enemyCountScale));
        else if (modifier.flatEnemyCountBonus < 0)
            modifier.flatEnemyCountBonus = Mathf.RoundToInt(modifier.flatEnemyCountBonus * enemyCountScale);

        modifier.extraRoleAmount = ScaleRoleAmount(modifier.extraRoleAmount, roleScale);
        modifier.secondaryExtraRoleAmount = ScaleRoleAmount(modifier.secondaryExtraRoleAmount, roleScale);
        modifier.tertiaryExtraRoleAmount = ScaleRoleAmount(modifier.tertiaryExtraRoleAmount, roleScale);

        if (modifier.spawnDelayMultiplier > 0f && modifier.spawnDelayMultiplier < 1f)
        {
            float pressure = 1f - modifier.spawnDelayMultiplier;
            modifier.spawnDelayMultiplier = Mathf.Clamp(1f - pressure * spawnPressureScale, 0.45f, 1f);
        }
        else if (modifier.spawnDelayMultiplier > 1f)
        {
            modifier.spawnDelayMultiplier = Mathf.Max(1f, modifier.spawnDelayMultiplier * (1f + 0.10f * softLevel));
        }

        if (modifier.chaosVariantChanceBonus > 0f)
            modifier.chaosVariantChanceBonus *= Mathf.Clamp(1f + 0.25f * softLevel, 1f, 2.5f);

        if (modifier.flatChaosVariantBonus > 0)
            modifier.flatChaosVariantBonus = Mathf.Max(modifier.flatChaosVariantBonus + 1, Mathf.RoundToInt(modifier.flatChaosVariantBonus * (1f + 0.35f * softLevel)));

        if (modifier.chaosWaveBlockStrengthBonus > 0)
            modifier.chaosWaveBlockStrengthBonus = Mathf.Max(modifier.chaosWaveBlockStrengthBonus, Mathf.RoundToInt(modifier.chaosWaveBlockStrengthBonus * (1f + 0.25f * softLevel)));

        if (modifier.chaosWaveBlockChanceBonus > 0f)
            modifier.chaosWaveBlockChanceBonus *= Mathf.Clamp(1f + 0.20f * softLevel, 1f, 2.0f);

        if (modifier.goldRewardMultiplierBonus > 0f)
            modifier.goldRewardMultiplierBonus *= rewardScale;

        if (modifier.xpRewardMultiplierBonus > 0f)
            modifier.xpRewardMultiplierBonus *= rewardScale;

        ApplyRiskLevelCaps(modifier);
    }

    private void ApplyRiskLevelCaps(WaveModifier modifier)
    {
        if (modifier == null)
            return;

        if (modifier.enemyCountMultiplier > 1f)
            modifier.enemyCountMultiplier = Mathf.Min(modifier.enemyCountMultiplier, Mathf.Max(1f, maxEnemyCountMultiplierPerRisk));

        if (modifier.flatEnemyCountBonus > 0)
            modifier.flatEnemyCountBonus = Mathf.Min(modifier.flatEnemyCountBonus, Mathf.Max(0, maxFlatEnemyBonusPerRisk));

        modifier.extraRoleAmount = CapRoleAmount(modifier.extraRoleAmount);
        modifier.secondaryExtraRoleAmount = CapRoleAmount(modifier.secondaryExtraRoleAmount);
        modifier.tertiaryExtraRoleAmount = CapRoleAmount(modifier.tertiaryExtraRoleAmount);

        if (modifier.spawnDelayMultiplier > 0f && modifier.spawnDelayMultiplier < 1f)
            modifier.spawnDelayMultiplier = Mathf.Max(modifier.spawnDelayMultiplier, Mathf.Clamp(minSpawnDelayMultiplierPerRisk, 0.1f, 1f));

        if (modifier.chaosVariantChanceBonus > 0f)
            modifier.chaosVariantChanceBonus = Mathf.Min(modifier.chaosVariantChanceBonus, Mathf.Max(0f, maxChaosVariantChanceBonusPerRisk));

        if (modifier.flatChaosVariantBonus > 0)
            modifier.flatChaosVariantBonus = Mathf.Min(modifier.flatChaosVariantBonus, Mathf.Max(0, maxChaosVariantFlatBonusPerRisk));

        if (modifier.chaosWaveBlockStrengthBonus > 0)
            modifier.chaosWaveBlockStrengthBonus = Mathf.Min(modifier.chaosWaveBlockStrengthBonus, Mathf.Max(0, maxChaosWaveBlockStrengthBonusPerRisk));

        if (modifier.chaosWaveBlockChanceBonus > 0f)
            modifier.chaosWaveBlockChanceBonus = Mathf.Min(modifier.chaosWaveBlockChanceBonus, Mathf.Max(0f, maxChaosWaveBlockChanceBonusPerRisk));

        if (modifier.goldRewardMultiplierBonus > 0f)
            modifier.goldRewardMultiplierBonus = Mathf.Min(modifier.goldRewardMultiplierBonus, Mathf.Max(0f, maxSingleRiskRewardBonus));

        if (modifier.xpRewardMultiplierBonus > 0f)
            modifier.xpRewardMultiplierBonus = Mathf.Min(modifier.xpRewardMultiplierBonus, Mathf.Max(0f, maxSingleRiskRewardBonus));
    }

    private int CapRoleAmount(int amount)
    {
        if (amount <= 0)
            return amount;

        return Mathf.Min(amount, Mathf.Max(1, maxExtraRoleAmountPerRisk));
    }

    private int ScaleRoleAmount(int amount, float scale)
    {
        if (amount <= 0)
            return amount;

        return Mathf.Max(amount + 1, Mathf.RoundToInt(amount * scale));
    }

    private string BuildChaosRiskChoiceDescription(WaveModifier modifier, int nextChaosLevel, int nextRiskLevel)
    {
        if (modifier == null)
            return "Chaos ist aktuell nicht verfügbar.";

        bool chaosCanRise = runData.chaosLevel < runData.maxChaosLevel;
        string chaosText = chaosCanRise
            ? "Chaos steigt auf Level " + nextChaosLevel + "."
            : "Chaos bleibt auf dem V1-Maximum " + runData.maxChaosLevel + ".";

        int displayRiskLevel = Mathf.Max(0, nextRiskLevel) + 1;
        string levelText = nextRiskLevel <= 0
            ? "Dieser dauerhafte Risiko-Modifikator startet auf Stufe 1."
            : "Dieser dauerhafte Risiko-Modifikator wird auf Stufe " + displayRiskLevel + " erhöht.";

        string text =
            "Riskanter Weg. " + chaosText + " " + levelText + " " +
            "Die Wirkung wird dadurch stärker, aber abflachend skaliert.";

        if (!string.IsNullOrEmpty(modifier.description))
            text += "\n" + modifier.description;

        if (includeRiskSummaryInChoiceText)
        {
            text +=
                "\n\nRisiko-Vorschau: " + GetModifierImpactPreviewText(modifier) +
                "\nReward-Vorschau: " + GetModifierRewardPreviewText(modifier);
        }

        text += "\n" + GetJusticeLossPreviewText();

        if (includeFairnessNotesInChoiceText)
            text += "\nFairness: Keine Tower-Zerstörung. Keine versteckten Chaos-6/7-Regeln. Die nächste Wave-Preview bleibt ehrlich.";

        return text;
    }

    private string GetModifierImpactPreviewText(WaveModifier modifier)
    {
        if (modifier == null)
            return "Kein Risiko.";

        switch (modifier.modifierType)
        {
            case WaveModifierType.ExtraEnemies:
            case WaveModifierType.FewerEnemies:
            case WaveModifierType.ChaosPrepared:
                return "Gegneranzahl x" + modifier.enemyCountMultiplier.ToString("0.00") + " und " + SignedInt(modifier.flatEnemyCountBonus) + " Zusatzgegner." + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.AddRole:
                return FormatPrimaryRolePreview(modifier, "pro zukünftiger Wave") + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.MoreRunners:
                return "+" + Mathf.Max(0, modifier.extraRoleAmount) + " Runner pro zukünftiger Wave." + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.MoreTanks:
                return "+" + Mathf.Max(0, modifier.extraRoleAmount) + " Tanks pro zukünftiger Wave." + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.MoreKnights:
                return "+" + Mathf.Max(0, modifier.extraRoleAmount) + " Knights pro zukünftiger Wave." + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.MoreMages:
                return "+" + Mathf.Max(0, modifier.extraRoleAmount) + " Mages pro zukünftiger Wave." + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.MoreLearners:
                return "+" + Mathf.Max(0, modifier.extraRoleAmount) + " Learner pro zukünftiger Wave." + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.MoreAllRounders:
                return "+" + Mathf.Max(0, modifier.extraRoleAmount) + " AllRounder pro zukünftiger Wave." + GetAdditionalRolePreviewText(modifier);

            case WaveModifierType.MixedRolePressure:
                return "Gemischter Rollendruck: " + BuildRoleAddListText(modifier) + " pro zukünftiger Wave.";

            case WaveModifierType.ChaosVariantPressure:
                return "Erhöht die Chance auf sichtbare Chaos-Varianten um +" + Mathf.RoundToInt(Mathf.Max(0f, modifier.chaosVariantChanceBonus) * 100f) + "% und erlaubt bis zu +" + Mathf.Max(0, modifier.flatChaosVariantBonus) + " zusätzliche Chaos-Variante(n) pro betroffener Wave. Keine zusätzlichen Gegner.";

            case WaveModifierType.MiniBossPressure:
                return "Nur auf MiniBoss-Waves: " + BuildRoleAddListText(modifier) + ". Keine Elite-Regeln, keine Tower-Zerstörung.";

            case WaveModifierType.PreBossPressure:
                return "Auf Vor-Boss-/Boss-Waves: " + BuildRoleAddListText(modifier) + ". Die Preview zeigt diese Zusatzgegner nur, wenn die betroffene Wave kommt.";

            case WaveModifierType.FasterSpawns:
                return "SpawnDelay x" + modifier.spawnDelayMultiplier.ToString("0.00") + "; Gegner kommen dichter.";

            case WaveModifierType.ChaosWaveBlockPressure:
                return BuildChaosWaveBlockPressurePreviewText(modifier);

            case WaveModifierType.HiddenPreview:
                return "Preview würde versteckt. In V1 nicht empfohlen.";

            default:
                return modifier.GetDebugSummary();
        }
    }

    private string BuildChaosWaveBlockPressurePreviewText(WaveModifier modifier)
    {
        if (modifier == null)
            return "Verstärkt Chaos-Wave-Bausteine.";

        string target = modifier.preferredChaosWaveBlockType == ChaosWaveBlockType.None
            ? "beliebige Chaos-Wave-Bausteine"
            : GetChaosWaveBlockDisplayName(modifier.preferredChaosWaveBlockType);

        string text = "Erhöht allgemein die Chance auf Chaos-Wave-Bausteine und bevorzugt " + target + " bei der Auswahl.";

        if (modifier.chaosWaveBlockStrengthBonus > 0)
            text += " Wenn der bevorzugte Typ erscheint, erhält er Baustein-Stärke +" + modifier.chaosWaveBlockStrengthBonus + ".";

        if (modifier.chaosWaveBlockChanceBonus > 0f)
            text += " Allgemeine Baustein-Chance +" + Mathf.RoundToInt(modifier.chaosWaveBlockChanceBonus * 100f) + "%.";

        text += " Keine eigenen Extra-Rewards durch den Baustein selbst; Rewards kommen nur über diesen Risiko-Modifikator.";
        return text;
    }

    private string GetChaosWaveBlockDisplayName(ChaosWaveBlockType blockType)
    {
        switch (blockType)
        {
            case ChaosWaveBlockType.RolePressure:
                return "Rollendruck";
            case ChaosWaveBlockType.Density:
                return "Verdichtung";
            case ChaosWaveBlockType.Toughness:
                return "Zähigkeit";
            case ChaosWaveBlockType.ChaosVariantGroup:
                return "Violette Gruppen";
            case ChaosWaveBlockType.Rearguard:
                return "Nachhut";
            case ChaosWaveBlockType.Armor:
                return "Armor";
            case ChaosWaveBlockType.Resistance:
                return "Resistenz";
            case ChaosWaveBlockType.PreviewHidden:
                return "Preview-Verhüllung";
            default:
                return "Chaos-Wave-Bausteine";
        }
    }

    private string FormatPrimaryRolePreview(WaveModifier modifier, string suffix)
    {
        if (modifier == null)
            return "Kein Rollenrisiko.";

        return "+" + Mathf.Max(0, modifier.extraRoleAmount) + " " + modifier.roleToAdd + " " + suffix + ".";
    }

    private string GetAdditionalRolePreviewText(WaveModifier modifier)
    {
        if (modifier == null)
            return "";

        string text = "";

        if (modifier.HasSecondaryRoleAdd())
            text += " Zusätzlich +" + modifier.secondaryExtraRoleAmount + " " + modifier.secondaryRoleToAdd + ".";

        if (modifier.HasTertiaryRoleAdd())
            text += " Zusätzlich +" + modifier.tertiaryExtraRoleAmount + " " + modifier.tertiaryRoleToAdd + ".";

        return text;
    }

    private string BuildRoleAddListText(WaveModifier modifier)
    {
        if (modifier == null)
            return "keine Zusatzrollen";

        string text = "";
        AppendRoleAddText(ref text, modifier.roleToAdd, modifier.extraRoleAmount);

        if (modifier.HasSecondaryRoleAdd())
            AppendRoleAddText(ref text, modifier.secondaryRoleToAdd, modifier.secondaryExtraRoleAmount);

        if (modifier.HasTertiaryRoleAdd())
            AppendRoleAddText(ref text, modifier.tertiaryRoleToAdd, modifier.tertiaryExtraRoleAmount);

        if (string.IsNullOrEmpty(text))
            return "keine Zusatzrollen";

        return text;
    }

    private void AppendRoleAddText(ref string text, EnemyRole role, int amount)
    {
        if (amount <= 0)
            return;

        if (!string.IsNullOrEmpty(text))
            text += ", ";

        text += "+" + amount + " " + role;
    }

    private string GetModifierRewardPreviewText(WaveModifier modifier)
    {
        if (modifier == null)
            return "Keine zusätzlichen Rewards.";

        int goldBonus = Mathf.RoundToInt(Mathf.Max(0f, modifier.goldRewardMultiplierBonus) * 100f);
        int xpBonus = Mathf.RoundToInt(Mathf.Max(0f, modifier.xpRewardMultiplierBonus) * 100f);

        if (goldBonus <= 0 && xpBonus <= 0)
            return "Keine direkten Rewards durch diesen Modifikator. Rewards kommen weiter über Gerechtigkeit/Wave-Rewards.";

        return "Gold +" + goldBonus + "% | XP +" + xpBonus + "% durch diesen Modifikator.";
    }

    private string SignedInt(int value)
    {
        if (value >= 0)
            return "+" + value;

        return value.ToString();
    }

    private List<WaveModifier> GetRiskModifierPool()
    {
        if (useInspectorRiskModifierPool && riskModifierPool != null && riskModifierPool.Count > 0)
            return FilterRiskPoolByAvailablePrefabs(riskModifierPool);

        return CreateDefaultRiskModifierPool();
    }

    private List<WaveModifier> CreateDefaultRiskModifierPool()
    {
        int levelForScaling = Mathf.Clamp(runData.chaosLevel + 1, 1, runData.maxChaosLevel);
        int lightExtra = Mathf.Clamp(1 + levelForScaling, 2, 5);
        int supportExtra = Mathf.Clamp(1 + levelForScaling / 2, 1, 4);
        int heavyExtra = Mathf.Clamp(1 + (levelForScaling - 1) / 2, 1, 3);
        int allRounderExtra = Mathf.Clamp(levelForScaling / 3, 1, 2);
        int mixedLightExtra = Mathf.Clamp(1 + levelForScaling / 2, 1, 3);
        int mixedSupportExtra = Mathf.Clamp(levelForScaling / 2, 1, 2);

        float enemyMultiplier = 1f + 0.10f + levelForScaling * 0.02f;
        float greedyEnemyMultiplier = 1f + 0.08f + levelForScaling * 0.015f;
        float fasterSpawnMultiplier = Mathf.Clamp(0.92f - levelForScaling * 0.025f, 0.78f, 0.90f);
        float greedyGoldBonus = 0.08f + levelForScaling * 0.02f;
        float greedyXpBonus = 0.04f + levelForScaling * 0.01f;
        float goldRushBonus = 0.10f + levelForScaling * 0.025f;
        float xpTrialBonus = 0.08f + levelForScaling * 0.02f;
        float rewardSpawnMultiplier = Mathf.Clamp(0.94f - levelForScaling * 0.02f, 0.82f, 0.92f);

        List<WaveModifier> pool = new List<WaveModifier>();

        pool.Add(new WaveModifier
        {
            displayName = "Mehr Gegner",
            description = "Alle zukünftigen Waves erhalten mehr Gegner. Die Stärke skaliert vorsichtig mit dem aktuellen Chaos-Level.",
            modifierType = WaveModifierType.ExtraEnemies,
            enemyCountMultiplier = enemyMultiplier,
            flatEnemyCountBonus = levelForScaling,
            isChaosModifier = true
        });

        pool.Add(new WaveModifier
        {
            displayName = "Runner-Druck",
            description = "Zukünftige Waves erhalten zusätzliche Runner. Das erhöht vor allem frühen Durchbruchsdruck.",
            modifierType = WaveModifierType.MoreRunners,
            extraRoleAmount = lightExtra,
            extraRoleSpawnDelay = 0.18f,
            isChaosModifier = true
        });

        pool.Add(new WaveModifier
        {
            displayName = "Tank-Druck",
            description = "Zukünftige Waves erhalten zusätzliche Tanks. Das erhöht den Bedarf an DoT, Heavy-Schaden und Fokusfeuer.",
            modifierType = WaveModifierType.MoreTanks,
            extraRoleAmount = heavyExtra,
            extraRoleSpawnDelay = 0.72f,
            isChaosModifier = true
        });

        pool.Add(new WaveModifier
        {
            displayName = "Knight-Druck",
            description = "Zukünftige Waves erhalten zusätzliche Knights. Das prüft Schaden pro Treffer und Targeting.",
            modifierType = WaveModifierType.MoreKnights,
            extraRoleAmount = heavyExtra,
            extraRoleSpawnDelay = 0.64f,
            isChaosModifier = true
        });

        if (includeMageAndLearnerRiskModifiers)
        {
            if (EnemySpawnerHasPrefab(EnemyRole.Mage))
            {
                pool.Add(new WaveModifier
                {
                    displayName = "Mage-Druck",
                    description = "Zukünftige Waves erhalten zusätzliche Mages. Das prüft Kontrolle, Fokusfeuer und Umgang mit Teleport-Druck.",
                    modifierType = WaveModifierType.MoreMages,
                    extraRoleAmount = supportExtra,
                    extraRoleSpawnDelay = 0.58f,
                    isChaosModifier = true
                });
            }

            if (EnemySpawnerHasPrefab(EnemyRole.Learner))
            {
                pool.Add(new WaveModifier
                {
                    displayName = "Learner-Druck",
                    description = "Zukünftige Waves erhalten zusätzliche Learner. Normale Learner ignorieren Status-Effekte wie Burn, Poison, Slow, Bleed und Darkness; Chaos-Learner schwächen DoT-Pläne.",
                    modifierType = WaveModifierType.MoreLearners,
                    extraRoleAmount = supportExtra,
                    extraRoleSpawnDelay = 0.54f,
                    isChaosModifier = true
                });
            }
        }

        pool.Add(new WaveModifier
        {
            displayName = "Schnellere Spawns",
            description = "Zukünftige Waves spawnen dichter. Der Effekt startet moderat und wird mit höherem Chaos spürbarer.",
            modifierType = WaveModifierType.FasterSpawns,
            spawnDelayMultiplier = fasterSpawnMultiplier,
            isChaosModifier = true
        });

        if (includeMixedRoleRiskModifiers && levelForScaling >= 2 && EnemySpawnerHasPrefab(EnemyRole.Runner))
        {
            pool.Add(new WaveModifier
            {
                displayName = "Gemischter Rollendruck",
                description = "Zukünftige Waves erhalten mehrere Zusatzrollen gleichzeitig. Das ist breiter Druck statt nur mehr Masse.",
                modifierType = WaveModifierType.MixedRolePressure,
                roleToAdd = EnemyRole.Runner,
                extraRoleAmount = mixedLightExtra,
                extraRoleSpawnDelay = 0.22f,
                useSecondaryRoleToAdd = EnemySpawnerHasPrefab(EnemyRole.Mage),
                secondaryRoleToAdd = EnemyRole.Mage,
                secondaryExtraRoleAmount = mixedSupportExtra,
                secondaryExtraRoleSpawnDelay = 0.58f,
                useTertiaryRoleToAdd = EnemySpawnerHasPrefab(EnemyRole.Learner),
                tertiaryRoleToAdd = EnemyRole.Learner,
                tertiaryExtraRoleAmount = mixedSupportExtra,
                tertiaryExtraRoleSpawnDelay = 0.56f,
                isChaosModifier = true
            });
        }

        if (includeAllRounderRiskModifierWhenPrefabExists && levelForScaling >= 3 && EnemySpawnerHasPrefab(EnemyRole.AllRounder))
        {
            pool.Add(new WaveModifier
            {
                displayName = "AllRounder-Druck",
                description = "Zukünftige Waves erhalten zusätzliche AllRounder. Das ist ein späteres V1-Risiko, weil AllRounder mehrere Rollenchecks kombiniert.",
                modifierType = WaveModifierType.MoreAllRounders,
                extraRoleAmount = allRounderExtra,
                extraRoleSpawnDelay = 0.78f,
                isChaosModifier = true
            });
        }

        if (includePreBossPressureRiskModifier && levelForScaling >= 2 && EnemySpawnerHasPrefab(EnemyRole.Knight))
        {
            pool.Add(new WaveModifier
            {
                displayName = "Vor-Boss-Druck",
                description = "Vor-Boss- und Boss-Waves erhalten zusätzliche harte Vorhut. Der Effekt ist nicht jede Wave aktiv, wird aber in der Preview ehrlich angezeigt.",
                modifierType = WaveModifierType.PreBossPressure,
                roleToAdd = EnemyRole.Knight,
                extraRoleAmount = heavyExtra,
                extraRoleSpawnDelay = 0.62f,
                useSecondaryRoleToAdd = EnemySpawnerHasPrefab(EnemyRole.Mage),
                secondaryRoleToAdd = EnemyRole.Mage,
                secondaryExtraRoleAmount = Mathf.Max(1, mixedSupportExtra),
                secondaryExtraRoleSpawnDelay = 0.58f,
                isChaosModifier = true
            });
        }

        if (includeMiniBossPressureRiskModifier && levelForScaling >= 4 && EnemySpawnerHasPrefab(EnemyRole.MiniBoss))
        {
            pool.Add(new WaveModifier
            {
                displayName = "MiniBoss-Druck light",
                description = "Zukünftige MiniBoss-Waves erhalten einen zusätzlichen MiniBoss. Das ist kein Elite-System und zerstört keine Tower.",
                modifierType = WaveModifierType.MiniBossPressure,
                roleToAdd = EnemyRole.MiniBoss,
                extraRoleAmount = 1,
                extraRoleSpawnDelay = 1.35f,
                isChaosModifier = true
            });
        }

        if (includeChaosVariantRiskModifiers && levelForScaling >= 3)
        {
            pool.Add(new WaveModifier
            {
                displayName = "Violette Verformung",
                description = "Zukünftige Waves enthalten häufiger sichtbare Chaos-Varianten. Diese Gegner ersetzen normale Gegner; es entstehen dadurch keine zusätzlichen Gegner.",
                modifierType = WaveModifierType.ChaosVariantPressure,
                increasesChaosVariantChance = true,
                chaosVariantChanceBonus = 0.08f + levelForScaling * 0.015f,
                flatChaosVariantBonus = Mathf.Max(1, levelForScaling / 3),
                xpRewardMultiplierBonus = 0.03f + levelForScaling * 0.01f,
                isChaosModifier = true,
                isRewardModifier = true
            });
        }

        if (includeChaosWaveBlockRiskModifiers && levelForScaling >= 2)
        {
            pool.Add(new WaveModifier
            {
                displayName = "Stärkere Verdichtung",
                description = "Chaos-Waves können häufiger und stärker verdichtet sein. Das betrifft Spawn-Abstände, nicht globale Gegnergeschwindigkeit.",
                modifierType = WaveModifierType.ChaosWaveBlockPressure,
                strengthensChaosWaveBlocks = true,
                preferredChaosWaveBlockType = ChaosWaveBlockType.Density,
                chaosWaveBlockStrengthBonus = 1,
                chaosWaveBlockChanceBonus = 0.08f,
                goldRewardMultiplierBonus = 0.03f + levelForScaling * 0.01f,
                isChaosModifier = true,
                isRewardModifier = true
            });

            pool.Add(new WaveModifier
            {
                displayName = "Zähe Wellen",
                description = "Chaos-Waves können häufiger und stärker zäh werden. Das erhöht Haltbarkeit einzelner Waves, aber nicht Base-Schaden oder Speed.",
                modifierType = WaveModifierType.ChaosWaveBlockPressure,
                strengthensChaosWaveBlocks = true,
                preferredChaosWaveBlockType = ChaosWaveBlockType.Toughness,
                chaosWaveBlockStrengthBonus = 1,
                chaosWaveBlockChanceBonus = 0.07f,
                xpRewardMultiplierBonus = 0.03f + levelForScaling * 0.01f,
                isChaosModifier = true,
                isRewardModifier = true
            });
        }

        if (includeChaosWaveBlockRiskModifiers && levelForScaling >= 3)
        {
            pool.Add(new WaveModifier
            {
                displayName = "Stärkere Nachhut",
                description = "Chaos-Waves können häufiger und stärker eine Nachhut bilden. Gegner werden innerhalb der Wave verlagert; es entstehen keine zusätzlichen Gegner.",
                modifierType = WaveModifierType.ChaosWaveBlockPressure,
                strengthensChaosWaveBlocks = true,
                preferredChaosWaveBlockType = ChaosWaveBlockType.Rearguard,
                chaosWaveBlockStrengthBonus = 1,
                chaosWaveBlockChanceBonus = 0.07f,
                goldRewardMultiplierBonus = 0.03f + levelForScaling * 0.008f,
                xpRewardMultiplierBonus = 0.02f + levelForScaling * 0.006f,
                isChaosModifier = true,
                isRewardModifier = true
            });

            pool.Add(new WaveModifier
            {
                displayName = "Violette Wellen",
                description = "Chaos-Waves können häufiger violette Gruppen bilden. Chaos-Varianten ersetzen normale Gegner; die Gegneranzahl steigt dadurch nicht automatisch.",
                modifierType = WaveModifierType.ChaosWaveBlockPressure,
                strengthensChaosWaveBlocks = true,
                preferredChaosWaveBlockType = ChaosWaveBlockType.ChaosVariantGroup,
                chaosWaveBlockStrengthBonus = 1,
                chaosWaveBlockChanceBonus = 0.06f,
                xpRewardMultiplierBonus = 0.04f + levelForScaling * 0.008f,
                isChaosModifier = true,
                isRewardModifier = true
            });
        }

        if (includeChaosWaveBlockRiskModifiers && levelForScaling >= 4)
        {
            pool.Add(new WaveModifier
            {
                displayName = "Chaos-Panzerung",
                description = "Chaos-Waves können häufiger kleine Armor-Gruppen bilden. Boss/MiniBoss werden dadurch in V1 nicht unfair eskaliert.",
                modifierType = WaveModifierType.ChaosWaveBlockPressure,
                strengthensChaosWaveBlocks = true,
                preferredChaosWaveBlockType = ChaosWaveBlockType.Armor,
                chaosWaveBlockStrengthBonus = 1,
                chaosWaveBlockChanceBonus = 0.05f,
                goldRewardMultiplierBonus = 0.04f + levelForScaling * 0.008f,
                isChaosModifier = true,
                isRewardModifier = true
            });
        }

        pool.Add(new WaveModifier
        {
            displayName = "Gieriger Ansturm",
            description = "Zukünftige Waves erhalten mehr Gegner. Dafür geben Rewards über diesen Modifier zusätzlich Gold und XP.",
            modifierType = WaveModifierType.ChaosPrepared,
            enemyCountMultiplier = greedyEnemyMultiplier,
            flatEnemyCountBonus = Mathf.Max(1, levelForScaling / 2),
            goldRewardMultiplierBonus = greedyGoldBonus,
            xpRewardMultiplierBonus = greedyXpBonus,
            isChaosModifier = true,
            isRewardModifier = true
        });

        if (includeAdditionalRewardRiskVariants)
        {
            pool.Add(new WaveModifier
            {
                displayName = "Goldrausch-Risiko",
                description = "Zukünftige Waves erhalten etwas mehr Gegner. Dafür steigen Gold-Rewards stärker.",
                modifierType = WaveModifierType.ChaosPrepared,
                enemyCountMultiplier = 1.06f + levelForScaling * 0.012f,
                flatEnemyCountBonus = Mathf.Max(1, levelForScaling / 2),
                goldRewardMultiplierBonus = goldRushBonus,
                isChaosModifier = true,
                isRewardModifier = true
            });

            if (EnemySpawnerHasPrefab(EnemyRole.Learner) && EnemySpawnerHasPrefab(EnemyRole.Mage))
            {
                pool.Add(new WaveModifier
                {
                    displayName = "XP-Prüfung",
                    description = "Zukünftige Waves erhalten zusätzliche Learner und Mages. Dafür steigen XP-Rewards.",
                    modifierType = WaveModifierType.MixedRolePressure,
                    roleToAdd = EnemyRole.Learner,
                    extraRoleAmount = supportExtra,
                    extraRoleSpawnDelay = 0.54f,
                    useSecondaryRoleToAdd = true,
                    secondaryRoleToAdd = EnemyRole.Mage,
                    secondaryExtraRoleAmount = Mathf.Max(1, mixedSupportExtra),
                    secondaryExtraRoleSpawnDelay = 0.58f,
                    xpRewardMultiplierBonus = xpTrialBonus,
                    isChaosModifier = true,
                    isRewardModifier = true
                });
            }

            pool.Add(new WaveModifier
            {
                displayName = "Eile gegen Gold",
                description = "Zukünftige Waves spawnen dichter. Dafür steigen Gold-Rewards leicht.",
                modifierType = WaveModifierType.FasterSpawns,
                spawnDelayMultiplier = rewardSpawnMultiplier,
                goldRewardMultiplierBonus = 0.06f + levelForScaling * 0.015f,
                isChaosModifier = true,
                isRewardModifier = true
            });
        }

        return FilterRiskPoolByAvailablePrefabs(pool);
    }

    private List<WaveModifier> FilterRiskPoolByAvailablePrefabs(List<WaveModifier> sourcePool)
    {
        if (!filterRiskModifiersByAvailablePrefabs || sourcePool == null)
            return sourcePool;

        List<WaveModifier> filtered = new List<WaveModifier>();

        foreach (WaveModifier modifier in sourcePool)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!RequiredEnemyPrefabsAvailable(modifier))
                continue;

            filtered.Add(modifier);
        }

        return filtered;
    }

    private bool RequiredEnemyPrefabsAvailable(WaveModifier modifier)
    {
        if (modifier == null)
            return false;

        if (modifier.extraRoleAmount > 0 && !EnemySpawnerHasPrefab(GetPrimaryRoleForModifier(modifier)))
            return false;

        if (modifier.HasSecondaryRoleAdd() && !EnemySpawnerHasPrefab(modifier.secondaryRoleToAdd))
            return false;

        if (modifier.HasTertiaryRoleAdd() && !EnemySpawnerHasPrefab(modifier.tertiaryRoleToAdd))
            return false;

        return true;
    }

    private bool EnemySpawnerHasPrefab(EnemyRole role)
    {
        ResolveReferences();

        if (enemySpawner == null)
            return true;

        return enemySpawner.HasEnemyPrefabForRole(role);
    }

    public void ChooseOption(int index)
    {
        if (!selectionOpen)
            return;

        if (blockChoiceWhileGameOver && gameManager != null && gameManager.isGameOver)
        {
            CloseSelectionWithoutResume();
            return;
        }

        if (index < 0 || index >= currentOptions.Count)
            return;

        ChaosJusticeChoiceOption option = currentOptions[index];

        if (option == null || !option.isEnabled)
        {
            Debug.LogWarning("Chaos/Gerechtigkeit: Diese Option ist aktuell nicht verfügbar.");
            return;
        }

        if (!option.closesChoice)
        {
            ApplyChoice(option);

            if (choiceUI != null)
                choiceUI.OpenSelection(currentOptions);

            Debug.Log("Chaos/Gerechtigkeit Unterauswahl: " + option.displayName + "\n" + GetCurrentChoiceDebugText());
            return;
        }

        selectionOpen = false;

        if (choiceUI != null)
            choiceUI.CloseSelection();

        ApplyChoice(option);
        pendingBossResult = null;
        currentRiskOffers.Clear();
        chaosCommittedForCurrentChoice = false;

        if (gameManager != null)
            gameManager.ResumeBuildPhaseAfterChaosJusticeChoice();
    }

    private void ApplyChoice(ChaosJusticeChoiceOption option)
    {
        if (option == null)
            return;

        switch (option.choiceType)
        {
            case ChaosJusticeChoiceType.OpenJusticeSubChoice:
                GenerateJusticeSubOptions();
                return;

            case ChaosJusticeChoiceType.OpenChaosSubChoice:
                chaosCommittedForCurrentChoice = true;
                GenerateChaosRiskSubOptions();
                return;

            case ChaosJusticeChoiceType.BackToMainChoice:
                GenerateMainChoiceOptions();
                currentChoiceStep = ChaosJusticeChoiceStep.MainChoice;
                chaosCommittedForCurrentChoice = false;
                return;

            case ChaosJusticeChoiceType.GoldJustice:
                ApplyGoldJusticeChoice(option);
                break;

            case ChaosJusticeChoiceType.XpJustice:
                ApplyXpJusticeChoice(option);
                break;

            case ChaosJusticeChoiceType.ChaosRiskModifier:
                ApplyChaosRiskChoice(option);
                break;

            case ChaosJusticeChoiceType.NoRiskModifier:
                ApplyNoRiskModifierChoice(option);
                break;
        }

        runData.bossChoicesResolved++;
        AddChoiceRecord(option);
        ApplyPreparedModifiersToSpawner();

        if (chaosUnlockManager != null)
            chaosUnlockManager.RefreshAndNotify();

        if (refreshPreviewAfterChoice && gameManager != null)
            gameManager.RefreshWaveDebugDataAfterChaosJusticeChange();

        Debug.Log("Chaos/Gerechtigkeit gewählt: " + option.displayName + " | " + GetBalanceDebugText());
    }

    private void ApplyGoldJusticeChoice(ChaosJusticeChoiceOption option)
    {
        runData.goldJusticeLevel++;
        RegisterJusticeChoice(JusticeTrackType.GoldJustice);
    }

    private void ApplyXpJusticeChoice(ChaosJusticeChoiceOption option)
    {
        runData.xpJusticeLevel++;
        RegisterJusticeChoice(JusticeTrackType.XpJustice);
    }

    private void RegisterJusticeChoice(JusticeTrackType track)
    {
        if (track == JusticeTrackType.None)
            return;

        if (runData.justiceSelectionHistory == null)
            runData.justiceSelectionHistory = new List<JusticeTrackType>();

        runData.justiceSelectionHistory.Add(track);
        runData.lastChosenJustice = track;
    }

    private void ApplyChaosRiskChoice(ChaosJusticeChoiceOption option)
    {
        IncreaseChaosLevelForChoice();

        if (option == null || option.modifier == null)
            return;

        ReduceLastChosenJusticeByOne();
        AddOrLevelRiskModifier(option.modifier);
    }

    private void ApplyNoRiskModifierChoice(ChaosJusticeChoiceOption option)
    {
        IncreaseChaosLevelForChoice();
        Debug.Log("Chaos gewählt ohne Risiko-Modifikator. Gerechtigkeit bleibt unverändert.");
    }

    private void IncreaseChaosLevelForChoice()
    {
        int oldLevel = runData.chaosLevel;
        runData.chaosLevel = Mathf.Clamp(runData.chaosLevel + 1, 0, runData.maxChaosLevel);
        runData.highestChaosLevel = Mathf.Max(runData.highestChaosLevel, runData.chaosLevel);

        if (runData.chaosLevel == oldLevel && oldLevel >= runData.maxChaosLevel)
            Debug.Log("Chaos ist bereits auf dem V1-Maximum: " + runData.maxChaosLevel);
    }

    private void AddOrLevelRiskModifier(WaveModifier selectedModifier)
    {
        if (selectedModifier == null)
            return;

        if (runData.selectedRiskModifiers == null)
            runData.selectedRiskModifiers = new List<WaveModifier>();

        if (runData.selectedRiskModifierNames == null)
            runData.selectedRiskModifierNames = new List<string>();

        int index = GetSelectedRiskModifierIndex(selectedModifier.displayName);
        int targetLevel = index >= 0 ? Mathf.Max(0, runData.selectedRiskModifiers[index].riskLevel + 1) : 0;
        WaveModifier leveledCopy = selectedModifier.CreateCopy();

        if (leveledCopy == null)
            return;

        leveledCopy.isPermanentRiskModifier = true;
        leveledCopy.isTemporaryRiskModifier = false;
        leveledCopy.riskLevel = targetLevel;
        leveledCopy.timesSelected = targetLevel + 1;

        if (index >= 0)
        {
            runData.selectedRiskModifiers[index] = leveledCopy;
            Debug.Log("Risiko-Modifikator gelevelt: " + leveledCopy.GetDisplayNameWithLevel());
        }
        else
        {
            runData.selectedRiskModifiers.Add(leveledCopy);

            if (!HasSelectedRiskModifierName(leveledCopy.displayName))
                runData.selectedRiskModifierNames.Add(leveledCopy.displayName);

            Debug.Log("Risiko-Modifikator aktiviert: " + leveledCopy.GetDisplayNameWithLevel());
        }
    }

    private void ReduceLastChosenJusticeByOne()
    {
        if (runData.justiceSelectionHistory != null && runData.justiceSelectionHistory.Count > 0)
        {
            for (int i = runData.justiceSelectionHistory.Count - 1; i >= 0; i--)
            {
                JusticeTrackType track = runData.justiceSelectionHistory[i];
                runData.justiceSelectionHistory.RemoveAt(i);

                if (TryReduceJusticeTrack(track))
                {
                    RefreshLastChosenJusticeFromHistory();
                    return;
                }
            }

            RefreshLastChosenJusticeFromHistory();
            return;
        }

        if (TryReduceJusticeTrack(runData.lastChosenJustice))
        {
            RefreshLastChosenJusticeFromHistory();
            return;
        }

        runData.lastChosenJustice = JusticeTrackType.None;
    }

    private bool TryReduceJusticeTrack(JusticeTrackType track)
    {
        if (track == JusticeTrackType.GoldJustice && runData.goldJusticeLevel > 0)
        {
            runData.goldJusticeLevel--;
            return true;
        }

        if (track == JusticeTrackType.XpJustice && runData.xpJusticeLevel > 0)
        {
            runData.xpJusticeLevel--;
            return true;
        }

        return false;
    }

    private void RefreshLastChosenJusticeFromHistory()
    {
        if (runData.justiceSelectionHistory == null || runData.justiceSelectionHistory.Count == 0)
        {
            runData.lastChosenJustice = JusticeTrackType.None;
            return;
        }

        runData.lastChosenJustice = runData.justiceSelectionHistory[runData.justiceSelectionHistory.Count - 1];
    }

    private void AddChoiceRecord(ChaosJusticeChoiceOption option)
    {
        ChaosJusticeChoiceRecord record = new ChaosJusticeChoiceRecord
        {
            afterBossWaveNumber = pendingBossResult != null ? pendingBossResult.waveNumber : 0,
            choiceType = option.choiceType,
            justiceTrack = GetJusticeTrackForOption(option.choiceType),
            displayName = option.displayName,
            modifierName = option.modifier != null ? option.modifier.displayName : "",
            bossDefeatedBeforeChoice = pendingBossResult != null && pendingBossResult.bossDefeated,
            bossWaveCompletedBeforeChoice = pendingBossResult != null && pendingBossResult.waveCompleted,
            chaosLevelAfterChoice = runData.chaosLevel,
            goldJusticeLevelAfterChoice = runData.goldJusticeLevel,
            xpJusticeLevelAfterChoice = runData.xpJusticeLevel,
            modifierLevelAfterChoice = option.modifier != null ? GetCurrentRiskModifierLevel(option.modifier.displayName) : -1,
            noModifierChosen = option.choiceType == ChaosJusticeChoiceType.NoRiskModifier
        };

        if (runData.choiceHistory == null)
            runData.choiceHistory = new List<ChaosJusticeChoiceRecord>();

        runData.choiceHistory.Add(record);
    }

    private JusticeTrackType GetJusticeTrackForOption(ChaosJusticeChoiceType choiceType)
    {
        switch (choiceType)
        {
            case ChaosJusticeChoiceType.GoldJustice:
                return JusticeTrackType.GoldJustice;
            case ChaosJusticeChoiceType.XpJustice:
                return JusticeTrackType.XpJustice;
            default:
                return JusticeTrackType.None;
        }
    }

    private void ApplyPreparedModifiersToSpawner()
    {
        ResolveReferences();

        if (enemySpawner == null)
            return;

        if (runData.selectedRiskModifiers == null || runData.selectedRiskModifiers.Count == 0)
        {
            enemySpawner.ClearPreparedWaveModifiers();
            return;
        }

        enemySpawner.SetPreparedWaveModifiers(runData.selectedRiskModifiers);
    }

    public int ApplyGoldRewardModifiers(int baseAmount)
    {
        int safeAmount = Mathf.Max(0, baseAmount);

        if (safeAmount <= 0)
            return safeAmount;

        return Mathf.Max(0, Mathf.RoundToInt(safeAmount * GetGoldRewardMultiplier()));
    }

    public int ApplyXPRewardModifiers(int baseAmount)
    {
        int safeAmount = Mathf.Max(0, baseAmount);

        if (safeAmount <= 0)
            return safeAmount;

        return Mathf.Max(0, Mathf.RoundToInt(safeAmount * GetXPRewardMultiplier()));
    }

    public float GetGoldRewardMultiplier()
    {
        return Mathf.Max(0f, 1f + runData.goldJusticeLevel * goldJusticeBonusPerLevel + GetChaosGoldRewardBonus());
    }

    public float GetXPRewardMultiplier()
    {
        return Mathf.Max(0f, 1f + runData.xpJusticeLevel * xpJusticeBonusPerLevel + GetChaosXPRewardBonus());
    }

    private float GetChaosGoldRewardBonus()
    {
        return GetChaosRewardBonus(true);
    }

    private float GetChaosXPRewardBonus()
    {
        return GetChaosRewardBonus(false);
    }

    private float GetChaosRewardBonus(bool goldReward)
    {
        if (runData.selectedRiskModifiers == null)
            return 0f;

        float bonus = 0f;
        float diminishingWeight = 1f;
        float safeDiminishingFactor = Mathf.Clamp(rewardRiskDiminishingFactor, 0.1f, 1f);
        float safeSingleCap = Mathf.Max(0f, maxSingleRiskRewardBonus);

        foreach (WaveModifier modifier in runData.selectedRiskModifiers)
        {
            if (modifier == null)
                continue;

            float modifierBonus = goldReward ? modifier.goldRewardMultiplierBonus : modifier.xpRewardMultiplierBonus;
            modifierBonus = Mathf.Min(Mathf.Max(0f, modifierBonus), safeSingleCap);

            if (modifierBonus <= 0f)
                continue;

            bonus += useRewardRiskDiminishingReturns ? modifierBonus * diminishingWeight : modifierBonus;
            diminishingWeight *= safeDiminishingFactor;
        }

        return Mathf.Min(bonus, Mathf.Max(0f, maxChaosRewardBonus));
    }

    public void ApplySnapshotToWaveResult(WaveCompletionResult result)
    {
        if (result == null)
            return;

        result.chaosLevelAtWaveStart = runData.chaosLevel;
        result.goldJusticeLevelAtWaveStart = runData.goldJusticeLevel;
        result.xpJusticeLevelAtWaveStart = runData.xpJusticeLevel;
        result.goldRewardMultiplierAtWaveStart = GetGoldRewardMultiplier();
        result.xpRewardMultiplierAtWaveStart = GetXPRewardMultiplier();

        // Wichtig für Step 7:
        // WaveCompletionResult.InitializeFromWaveData speichert bereits die tatsächlich
        // auf diese konkrete Wave angewendeten Modifier aus WaveData.appliedModifiers.
        // Conditional Risks wie MiniBoss-Druck oder Vor-Boss-Druck dürfen hier nicht
        // nachträglich durch die komplette Run-Liste überschrieben werden.
        if (string.IsNullOrEmpty(result.activeRiskModifierSummary))
            result.activeRiskModifierSummary = GetSelectedRiskModifierSummary();
    }

    private void HandleWaveCompletedForResultPrep(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted)
            return;

        if (result.chaosLevelAtWaveStart > 0)
            runData.chaosWavesSurvived++;

        if (result.bossDefeated)
            runData.bossKillsDuringRun++;
    }


    public List<WaveModifier> GetSelectedRiskModifierCopies()
    {
        List<WaveModifier> copies = new List<WaveModifier>();

        if (runData.selectedRiskModifiers == null)
            return copies;

        foreach (WaveModifier modifier in runData.selectedRiskModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            copies.Add(modifier.CreateCopy());
        }

        return copies;
    }

    public string GetSelectedRiskModifierSummary()
    {
        if (runData.selectedRiskModifiers == null || runData.selectedRiskModifiers.Count == 0)
            return "Keine";

        string summary = "";

        foreach (WaveModifier modifier in runData.selectedRiskModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!string.IsNullOrEmpty(summary))
                summary += ", ";

            summary += modifier.GetDisplayNameWithLevel();
        }

        if (string.IsNullOrEmpty(summary))
            return "Keine";

        return summary;
    }

    public int GetChaosLevel()
    {
        if (runData == null)
            return 0;

        return Mathf.Max(0, runData.chaosLevel);
    }

    public bool AreChaosVariantsUnlocked()
    {
        return GetChaosLevel() >= 3;
    }

    public ChaosJusticeBalanceSnapshot GetBalanceSnapshot()
    {
        ChaosJusticeBalanceSnapshot snapshot = new ChaosJusticeBalanceSnapshot();

        int safeJusticeScorePerLevel = Mathf.Max(1, justiceScorePerLevel);
        int safeChaosScorePerLevel = Mathf.Max(1, chaosScorePerLevel);

        int safetyScore = Mathf.Max(0, baseSafetyScore) + (runData.goldJusticeLevel + runData.xpJusticeLevel) * safeJusticeScorePerLevel;
        int chaosScore = runData.chaosLevel * safeChaosScorePerLevel + GetTotalRiskPressureScore();
        int rawTotal = safetyScore + chaosScore;
        int total = Mathf.Max(1, rawTotal);

        snapshot.safetyScore = safetyScore;
        snapshot.chaosScore = chaosScore;
        snapshot.totalScore = total;

        if (rawTotal <= 0)
        {
            snapshot.safetyPercent = 100;
            snapshot.chaosPercent = 0;
        }
        else
        {
            snapshot.safetyPercent = Mathf.RoundToInt((safetyScore / (float)total) * 100f);
            snapshot.chaosPercent = Mathf.RoundToInt((chaosScore / (float)total) * 100f);
        }

        snapshot.label = GetBalanceLabel(snapshot.chaosPercent);
        snapshot.barText = BuildBalanceBar(snapshot.safetyPercent, snapshot.chaosPercent);

        return snapshot;
    }

    public string GetBalanceStatusLine()
    {
        ChaosJusticeBalanceSnapshot snapshot = GetBalanceSnapshot();

        return
            snapshot.barText + " " + snapshot.label +
            " | Sicherheit " + snapshot.safetyPercent + "%" +
            " / Chaos " + snapshot.chaosPercent + "%";
    }

    public string GetBalanceDebugText()
    {
        ChaosJusticeBalanceSnapshot snapshot = GetBalanceSnapshot();

        return
            "Balance | " + snapshot.barText + " " + snapshot.label +
            "\nSicherheit: " + snapshot.safetyScore + " (" + snapshot.safetyPercent + "%)" +
            " | Chaos: " + snapshot.chaosScore + " (" + snapshot.chaosPercent + "%)" +
            "\nGold-Gerechtigkeit: " + runData.goldJusticeLevel + " | XP-Gerechtigkeit: " + runData.xpJusticeLevel +
            "\nGold-Reward x" + GetGoldRewardMultiplier().ToString("0.00") + " | XP-Reward x" + GetXPRewardMultiplier().ToString("0.00") +
            "\nRisiko-Auswahlmodus: " + GetRiskSelectionModeLabel() +
            "\nRisiko-Gruppen:\n" + GetGroupedRiskModifierSummary() +
            "\nAktive Risiken:\n" + GetDetailedRiskModifierText(maxRiskDetailsInHud);
    }

    public string GetGroupedRiskModifierSummary()
    {
        if (runData.selectedRiskModifiers == null || runData.selectedRiskModifiers.Count == 0)
            return "Keine aktiven Risiken.";

        int enemyPressure = 0;
        int rolePressure = 0;
        int spawnPressure = 0;
        int specialPressure = 0;
        int chaosVariantPressure = 0;
        int wavePressure = 0;
        int rewardRisk = 0;

        foreach (WaveModifier modifier in runData.selectedRiskModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            switch (GetRiskCategory(modifier))
            {
                case "Gegnerdruck":
                    enemyPressure++;
                    break;
                case "Rollendruck":
                    rolePressure++;
                    break;
                case "Spawn-Druck":
                    spawnPressure++;
                    break;
                case "Spezialdruck":
                    specialPressure++;
                    break;
                case "Chaos-Varianten":
                    chaosVariantPressure++;
                    break;
                case "Wave-Druck":
                    wavePressure++;
                    break;
                case "Reward-Risiko":
                    rewardRisk++;
                    break;
            }
        }

        string text = "";
        AppendRiskGroupLine(ref text, "Gegnerdruck", enemyPressure);
        AppendRiskGroupLine(ref text, "Rollendruck", rolePressure);
        AppendRiskGroupLine(ref text, "Spawn-Druck", spawnPressure);
        AppendRiskGroupLine(ref text, "Spezialdruck", specialPressure);
        AppendRiskGroupLine(ref text, "Chaos-Varianten", chaosVariantPressure);
        AppendRiskGroupLine(ref text, "Wave-Druck", wavePressure);
        AppendRiskGroupLine(ref text, "Reward-Risiko", rewardRisk);

        if (string.IsNullOrEmpty(text))
            return "Keine aktiven Risiken.";

        return text;
    }

    public string GetDetailedRiskModifierText(int maxEntries)
    {
        if (runData.selectedRiskModifiers == null || runData.selectedRiskModifiers.Count == 0)
            return "Keine";

        int safeMax = Mathf.Max(1, maxEntries);
        int startIndex = Mathf.Max(0, runData.selectedRiskModifiers.Count - safeMax);
        string text = "";

        for (int i = startIndex; i < runData.selectedRiskModifiers.Count; i++)
        {
            WaveModifier modifier = runData.selectedRiskModifiers[i];

            if (modifier == null)
                continue;

            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "- " + modifier.GetDisplayNameWithLevel() + " [" + GetRiskCategory(modifier) + "]: " + GetModifierImpactPreviewText(modifier);

            string rewardText = GetModifierRewardPreviewText(modifier);
            if (!string.IsNullOrEmpty(rewardText) && !rewardText.StartsWith("Keine direkten"))
                text += " | " + rewardText;
        }

        int hiddenCount = runData.selectedRiskModifiers.Count - safeMax;
        if (hiddenCount > 0)
            text += "\n- ... " + hiddenCount + " ältere Risiko-Modifikator(en) ausgeblendet.";

        if (string.IsNullOrEmpty(text))
            return "Keine";

        return text;
    }

    public int GetTotalRiskPressureScore()
    {
        if (runData.selectedRiskModifiers == null || runData.selectedRiskModifiers.Count == 0)
            return 0;

        int score = 0;

        foreach (WaveModifier modifier in runData.selectedRiskModifiers)
        {
            score += GetRiskPressureScore(modifier);
        }

        return Mathf.Max(0, score);
    }

    public int GetActiveRiskModifierCount()
    {
        if (runData.selectedRiskModifiers == null)
            return 0;

        return runData.selectedRiskModifiers.Count;
    }

    private string GetRiskCategory(WaveModifier modifier)
    {
        if (modifier == null)
            return "Unbekannt";

        if (modifier.modifierType == WaveModifierType.MiniBossPressure || modifier.modifierType == WaveModifierType.PreBossPressure)
            return "Spezialdruck";

        if (modifier.modifierType == WaveModifierType.ChaosWaveBlockPressure || modifier.strengthensChaosWaveBlocks)
            return "Wave-Druck";

        if (modifier.isRewardModifier || modifier.goldRewardMultiplierBonus > 0f || modifier.xpRewardMultiplierBonus > 0f)
            return "Reward-Risiko";

        switch (modifier.modifierType)
        {
            case WaveModifierType.ExtraEnemies:
            case WaveModifierType.FewerEnemies:
            case WaveModifierType.ChaosPrepared:
                return "Gegnerdruck";

            case WaveModifierType.ChaosVariantPressure:
                return "Chaos-Varianten";

            case WaveModifierType.AddRole:
            case WaveModifierType.MoreRunners:
            case WaveModifierType.MoreTanks:
            case WaveModifierType.MoreKnights:
            case WaveModifierType.MoreMages:
            case WaveModifierType.MoreLearners:
            case WaveModifierType.MoreAllRounders:
            case WaveModifierType.MixedRolePressure:
                return "Rollendruck";

            case WaveModifierType.FasterSpawns:
                return "Spawn-Druck";

            default:
                return "Sonstiges";
        }
    }

    private int GetRiskPressureScore(WaveModifier modifier)
    {
        if (modifier == null || !modifier.IsValid())
            return 0;

        int score = 0;

        if (modifier.enemyCountMultiplier > 1f)
            score += Mathf.RoundToInt((modifier.enemyCountMultiplier - 1f) * 20f);

        if (modifier.flatEnemyCountBonus > 0)
            score += modifier.flatEnemyCountBonus * Mathf.Max(1, riskScorePerFlatEnemy);

        score += GetRolePressureScore(GetPrimaryRoleForModifier(modifier), modifier.extraRoleAmount);

        if (modifier.HasSecondaryRoleAdd())
            score += GetRolePressureScore(modifier.secondaryRoleToAdd, modifier.secondaryExtraRoleAmount);

        if (modifier.HasTertiaryRoleAdd())
            score += GetRolePressureScore(modifier.tertiaryRoleToAdd, modifier.tertiaryExtraRoleAmount);

        if (modifier.spawnDelayMultiplier > 0f && modifier.spawnDelayMultiplier < 1f)
            score += Mathf.RoundToInt((1f - modifier.spawnDelayMultiplier) * 16f);

        if (modifier.modifierType == WaveModifierType.ChaosVariantPressure || modifier.increasesChaosVariantChance)
        {
            score += Mathf.RoundToInt(Mathf.Max(0f, modifier.chaosVariantChanceBonus) * 30f);
            score += Mathf.Max(0, modifier.flatChaosVariantBonus) * 2;
        }

        if (modifier.modifierType == WaveModifierType.ChaosWaveBlockPressure || modifier.strengthensChaosWaveBlocks)
        {
            score += 2;
            score += Mathf.Max(0, modifier.chaosWaveBlockStrengthBonus) * 2;
            score += Mathf.RoundToInt(Mathf.Max(0f, modifier.chaosWaveBlockChanceBonus) * 20f);
        }

        if (modifier.modifierType == WaveModifierType.MiniBossPressure || modifier.modifierType == WaveModifierType.PreBossPressure)
            score += 2;

        if (modifier.isRewardModifier)
            score += 1;

        return Mathf.Max(1, score);
    }

    private EnemyRole GetPrimaryRoleForModifier(WaveModifier modifier)
    {
        if (modifier == null)
            return EnemyRole.Standard;

        return modifier.GetPrimaryRoleForModifier();
    }

    private int GetRolePressureScore(EnemyRole role, int amount)
    {
        if (amount <= 0)
            return 0;

        bool heavyRole =
            role == EnemyRole.Tank ||
            role == EnemyRole.Knight ||
            role == EnemyRole.Mage ||
            role == EnemyRole.Learner ||
            role == EnemyRole.AllRounder ||
            role == EnemyRole.MiniBoss ||
            role == EnemyRole.Boss;

        int weight = heavyRole ? Mathf.Max(1, riskScorePerExtraHeavyEnemy) : Mathf.Max(1, riskScorePerExtraLightEnemy);
        return amount * weight;
    }

    private string GetBalanceLabel(int chaosPercent)
    {
        if (chaosPercent <= 20)
            return "Stabil";

        if (chaosPercent <= 40)
            return "Leicht riskant";

        if (chaosPercent <= 60)
            return "Ausgeglichen riskant";

        if (chaosPercent <= 80)
            return "Chaos-dominiert";

        return "Kritisch";
    }

    private string BuildBalanceBar(int safetyPercent, int chaosPercent)
    {
        int width = Mathf.Clamp(balanceBarWidth, 8, 32);
        int safetySegments = Mathf.Clamp(Mathf.RoundToInt(width * safetyPercent / 100f), 0, width);
        int chaosSegments = width - safetySegments;

        string safety = new string('S', safetySegments);
        string chaos = new string('C', chaosSegments);

        return "[" + safety + chaos + "]";
    }

    private void AppendRiskGroupLine(ref string text, string label, int count)
    {
        if (count <= 0)
            return;

        if (!string.IsNullOrEmpty(text))
            text += "\n";

        text += "- " + label + ": " + count;
    }

    public string GetNextChaosRiskPreviewText()
    {
        List<WaveModifier> offers = GenerateRandomRiskOffers(Mathf.Max(1, riskOfferCount), false);

        if (offers == null || offers.Count == 0)
            return "Keine Risiko-Modifikatoren verfügbar.";

        int nextChaosLevel = GetNextChaosLevelPreview();
        string text = "Mögliche Risiko-Auswahl | Chaos " + runData.chaosLevel + " -> " + nextChaosLevel + " | Modus: " + GetRiskSelectionModeLabel();

        for (int i = 0; i < offers.Count; i++)
        {
            WaveModifier modifier = offers[i];

            if (modifier == null)
                continue;

            text += "\n[" + (i + 1) + "] " + modifier.GetDisplayNameWithLevel() + ": " + GetModifierImpactPreviewText(modifier);
        }

        if (showNoModifierOption && (allowNoModifierAtMaxChaos || runData.chaosLevel < runData.maxChaosLevel))
            text += "\n[" + (offers.Count + 1) + "] " + (runData.chaosLevel >= runData.maxChaosLevel ? "Chaos halten" : "Kein Modifikator") + ": kein Zusatzrisiko, kein Gerechtigkeits-Rückbau und keine Zusatzbelohnung.";

        return text;
    }

    private string GetRiskSelectionModeLabel()
    {
        if (useCompletelyRandomRiskSelection)
            return "Random";

        if (useFairRiskRotation)
        {
            if (avoidPreviouslyOfferedRiskModifiersUntilPoolExhausted || avoidDuplicateRiskModifiersUntilPoolExhausted)
                return "Fair Rotation / Pool Exhaustion";

            return "Fair Rotation";
        }

        return "Random";
    }

    public string GetCurrentChoiceDebugText()
    {
        if (currentOptions == null || currentOptions.Count == 0)
            return "Keine Optionen.";

        string text = "Risiko-Auswahlmodus: " + GetRiskSelectionModeLabel();

        for (int i = 0; i < currentOptions.Count; i++)
        {
            ChaosJusticeChoiceOption option = currentOptions[i];

            if (option == null)
                continue;

            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "[" + (i + 1) + "] " + option.displayName + " - " + option.description;
        }

        return text;
    }

    public int GetJusticeChoiceCount()
    {
        if (runData == null || runData.choiceHistory == null)
            return 0;

        int count = 0;

        foreach (ChaosJusticeChoiceRecord record in runData.choiceHistory)
        {
            if (record == null)
                continue;

            if (record.choiceType == ChaosJusticeChoiceType.GoldJustice || record.choiceType == ChaosJusticeChoiceType.XpJustice)
                count++;
        }

        return count;
    }

    public int GetChaosChoiceCount()
    {
        if (runData == null || runData.choiceHistory == null)
            return 0;

        int count = 0;

        foreach (ChaosJusticeChoiceRecord record in runData.choiceHistory)
        {
            if (record == null)
                continue;

            if (record.choiceType == ChaosJusticeChoiceType.ChaosRiskModifier || record.choiceType == ChaosJusticeChoiceType.NoRiskModifier)
                count++;
        }

        return count;
    }

    public int GetNoModifierChoiceCount()
    {
        if (runData == null || runData.choiceHistory == null)
            return 0;

        int count = 0;

        foreach (ChaosJusticeChoiceRecord record in runData.choiceHistory)
        {
            if (record == null)
                continue;

            if (record.noModifierChosen || record.choiceType == ChaosJusticeChoiceType.NoRiskModifier)
                count++;
        }

        return count;
    }

    public int GetHighestRiskModifierLevel()
    {
        int highest = -1;

        if (runData == null || runData.selectedRiskModifiers == null)
            return highest;

        foreach (WaveModifier modifier in runData.selectedRiskModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            highest = Mathf.Max(highest, modifier.riskLevel);
        }

        return highest;
    }

    public WaveModifier GetStrongestRiskModifier()
    {
        WaveModifier strongest = null;
        int strongestScore = -1;

        if (runData == null || runData.selectedRiskModifiers == null)
            return null;

        foreach (WaveModifier modifier in runData.selectedRiskModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            int score = modifier.riskLevel * 100 + GetRiskPressureScore(modifier);

            if (score > strongestScore)
            {
                strongestScore = score;
                strongest = modifier;
            }
        }

        return strongest;
    }

    public string GetStrongestRiskModifierText()
    {
        WaveModifier strongest = GetStrongestRiskModifier();

        if (strongest == null)
            return "Keine aktiven Risiko-Modifikatoren.";

        return strongest.GetDisplayNameWithLevel() + " | " + GetModifierImpactPreviewText(strongest);
    }

    public string GetDecisionMixText()
    {
        int justice = GetJusticeChoiceCount();
        int chaos = GetChaosChoiceCount();
        int noModifier = GetNoModifierChoiceCount();

        return
            "Gerechtigkeit: " + justice +
            " | Chaos: " + chaos +
            " | Kein Modifikator: " + noModifier;
    }

    public string GetRunStyleLabel()
    {
        int justice = GetJusticeChoiceCount();
        int chaos = GetChaosChoiceCount();

        if (chaos <= 0 && justice <= 0)
            return "Unentschiedener Auftakt";

        if (chaos <= 0)
            return "Ordnungslauf";

        if (justice <= 0)
            return "Chaoslauf";

        if (chaos > justice + 1)
            return "Chaosbetonter Mischlauf";

        if (justice > chaos + 1)
            return "Gerechtigkeitsbetonter Mischlauf";

        return "Balance-Lauf";
    }

    public string GetResultScreenDebugText()
    {
        WaveHistory history = null;

        if (gameManager != null)
            history = gameManager.GetWaveHistory();

        string historyText = history != null
            ? "Kills: " + history.GetTotalKills() + " | Leaks: " + history.GetTotalLeaks() + " | Bosskills: " + history.GetBossKills()
            : "WaveHistory nicht verfügbar";

        return
            "ERGEBNIS-DATEN V1" +
            "\nHöchstes Chaos-Level: " + runData.highestChaosLevel +
            "\nAktuelles Balance-Verhältnis: " + GetBalanceStatusLine() +
            "\nGold-Gerechtigkeit: " + runData.goldJusticeLevel +
            "\nXP-Gerechtigkeit: " + runData.xpJusticeLevel +
            "\nRisiko-Gruppen:\n" + GetGroupedRiskModifierSummary() +
            "\nAktive Risiko-Modifikatoren:\n" + GetDetailedRiskModifierText(12) +
            "\nÜberstandene Chaos-Waves: " + runData.chaosWavesSurvived +
            "\n" + historyText;
    }


    public string GetChoiceTitleText()
    {
        string baseTitle;

        if (pendingBossResult == null)
            baseTitle = "BOSS-WAVE ABGESCHLOSSEN";
        else if (pendingBossResult.bossDefeated)
            baseTitle = "BOSS BESIEGT";
        else
            baseTitle = "BOSS-WAVE ÜBERSTANDEN";

        switch (currentChoiceStep)
        {
            case ChaosJusticeChoiceStep.JusticeSubChoice:
                return baseTitle + " - GERECHTIGKEIT";
            case ChaosJusticeChoiceStep.ChaosRiskSubChoice:
                return baseTitle + " - RISIKO-MODIFIKATOR";
            case ChaosJusticeChoiceStep.MainChoice:
            default:
                return baseTitle;
        }
    }

    public string GetChoiceDescriptionText()
    {
        string outcome = pendingBossResult != null && pendingBossResult.bossDefeated
            ? "Der Boss wurde besiegt."
            : "Die Boss-Wave wurde überstanden.";

        switch (currentChoiceStep)
        {
            case ChaosJusticeChoiceStep.JusticeSubChoice:
                return outcome + " Wähle Gold- oder XP-Gerechtigkeit. Gerechtigkeit ist sicher, dauerhaft und erzeugt kein zusätzliches Risiko.";

            case ChaosJusticeChoiceStep.ChaosRiskSubChoice:
                if (runData.chaosLevel >= runData.maxChaosLevel)
                    return outcome + " Chaos ist auf dem V1-Maximum. Wähle einen konkreten Risiko-Modifikator oder bewusst 'Chaos halten'. Kein Risiko zerstört Tower. Auswahlmodus: " + GetRiskSelectionModeLabel() + ".";

                return outcome + " Du hast Chaos gewählt. Wähle jetzt einen von drei Risiko-Modifikatoren oder 'Kein Modifikator'. Risiko-Modifikatoren können zuletzt aufgebaute Gerechtigkeit abbauen. Auswahlmodus: " + GetRiskSelectionModeLabel() + ".";

            case ChaosJusticeChoiceStep.MainChoice:
            default:
                return outcome + " Entscheide zuerst zwischen sicherer Gerechtigkeit und riskantem Chaos. Alle Risiken sind offen sichtbar; Boss und V1-Chaos zerstören keine Tower.";
        }
    }

    private void HandleGameOverTriggered()
    {
        CloseSelectionWithoutResume();
    }

    public void CloseSelectionWithoutResume()
    {
        selectionOpen = false;
        pendingBossResult = null;
        currentChoiceStep = ChaosJusticeChoiceStep.MainChoice;
        chaosCommittedForCurrentChoice = false;
        currentRiskOffers.Clear();
        currentOptions.Clear();

        if (choiceUI != null)
            choiceUI.CloseSelection();
    }
}
