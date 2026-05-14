using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject standardEnemyPrefab;
    public GameObject tankEnemyPrefab;
    public GameObject knightEnemyPrefab;
    public GameObject runnerEnemyPrefab;
    public GameObject mageEnemyPrefab;
    public GameObject learnerEnemyPrefab;
    public GameObject allRounderEnemyPrefab;
    public GameObject miniBossEnemyPrefab;
    public GameObject bossEnemyPrefab;

    [Header("Chaos Variant Enemy Prefabs - Optional V1")]
    public bool preferChaosVariantPrefabs = true;
    public GameObject chaosStandardEnemyPrefab;
    public GameObject chaosRunnerEnemyPrefab;
    public GameObject chaosTankEnemyPrefab;
    public GameObject chaosKnightEnemyPrefab;
    public GameObject chaosMageEnemyPrefab;
    public GameObject chaosLearnerEnemyPrefab;
    public GameObject chaosAllRounderEnemyPrefab;

    [Header("References")]
    public TileManager tileManager;
    public GameManager gameManager;

    [Header("Spawn Settings")]
    public float spawnDelay = 0.4f;

    [Header("Spawn Delay Scaling")]
    public bool scaleSpawnDelayByWave = true;
    public int spawnDelayScalingStartWave = 11;
    public float spawnDelayReductionPer10Waves = 0.11f;
    public float minSpawnDelayMultiplier = 0.45f;
    public float minimumSpawnDelay = 0.06f;

    [Header("Debug / Backend Preview")]
    public List<EnemySpawnEntry> lastGeneratedWave = new List<EnemySpawnEntry>();
    public WaveData lastWaveData;

    [Header("Prepared Wave Modifiers")]
    public bool usePreparedWaveModifiers = false;
    public List<WaveModifier> activeWaveModifiers = new List<WaveModifier>();

    [Header("Chaos Variants V1")]
    public bool enableChaosVariantsV1 = true;
    public int chaosVariantsStartLevel = 3;
    public float chaosVariantChanceAtStartLevel = 0.12f;
    public float chaosVariantChancePerAdditionalLevel = 0.06f;
    public float maxChaosVariantChance = 0.35f;
    public int maxChaosVariantsAtStartLevel = 2;
    public int maxChaosVariantsPerAdditionalLevel = 1;
    public int chaosVariantSeedSalt = 9137;
    public bool allowChaosVariantBossesInV1 = false;
    public bool allowChaosVariantMiniBossesInV1 = false;
    public bool logChaosVariantGeneration = false;

    [Header("Chaos Wave Blocks V1")]
    public bool enableChaosWaveBlocksV1 = true;
    public int chaosWaveBlocksStartLevel = 2;
    public int forceChaosWaveBlockAtLevel = 3;
    public int maxChaosWaveBlocksV1 = 3;
    public float chaosWaveBlockChanceAtStartLevel = 0.45f;
    public float chaosWaveBlockChancePerAdditionalLevel = 0.12f;
    public int chaosWaveBlockSeedSalt = 44117;
    public bool allowRolePressureBlock = true;
    public bool allowDensityBlock = true;
    public bool allowToughnessBlock = true;
    public bool allowRearGuardBlock = true;
    public bool allowArmorBlock = true;
    public bool allowChaosVariantGroupBlock = true;
    public bool allowResistanceBlockInV1 = false;
    public bool allowPreviewHiddenBlockInV1 = false;
    public bool logChaosWaveBlockGeneration = false;

    private int aliveEnemies = 0;
    private Action onWaveFinished;
    private Coroutine activeSpawnCoroutine;

    public void StartWave(int enemyCount, Action finishedCallback)
    {
        int waveNumber = 1;

        if (gameManager != null)
            waveNumber = gameManager.waveNumber;

        WaveData waveData = BuildWaveDataForWave(waveNumber, enemyCount);
        lastWaveData = waveData;
        StartWave(waveData.spawnEntries, finishedCallback);
    }

    public void StartWave(WaveData waveData, Action finishedCallback)
    {
        if (waveData == null)
        {
            Debug.LogWarning("EnemySpawner: WaveData fehlt. Wave wird direkt beendet.");
            aliveEnemies = 0;
            onWaveFinished = finishedCallback;
            onWaveFinished?.Invoke();
            return;
        }

        lastWaveData = waveData;

        if (waveData.spawnEntries == null || waveData.spawnEntries.Count == 0)
        {
            Debug.LogWarning("EnemySpawner: WaveData enthält keine SpawnEntries. Wave wird direkt beendet.");
            aliveEnemies = 0;
            onWaveFinished = finishedCallback;
            onWaveFinished?.Invoke();
            return;
        }

        StartWave(waveData.spawnEntries, finishedCallback);
    }

    public void StartWave(List<EnemySpawnEntry> spawnEntries, Action finishedCallback)
    {
        if (activeSpawnCoroutine != null)
        {
            StopCoroutine(activeSpawnCoroutine);
            activeSpawnCoroutine = null;
        }

        onWaveFinished = finishedCallback;

        if (spawnEntries == null || spawnEntries.Count == 0)
        {
            Debug.LogWarning("EnemySpawner: Keine SpawnEntries vorhanden. Wave wird direkt beendet.");
            aliveEnemies = 0;
            onWaveFinished?.Invoke();
            return;
        }

        lastGeneratedWave = new List<EnemySpawnEntry>(spawnEntries);
        aliveEnemies = CountEnemies(spawnEntries);

        if (aliveEnemies <= 0)
        {
            Debug.LogWarning("EnemySpawner: SpawnEntries enthalten keine Gegner. Wave wird direkt beendet.");
            onWaveFinished?.Invoke();
            return;
        }

        activeSpawnCoroutine = StartCoroutine(SpawnWave(spawnEntries));
    }

    private IEnumerator SpawnWave(List<EnemySpawnEntry> spawnEntries)
    {
        foreach (EnemySpawnEntry entry in spawnEntries)
        {
            if (entry == null)
                continue;

            int amount = Mathf.Max(0, entry.amount);
            float delay = GetSafeSpawnDelay(entry);

            for (int i = 0; i < amount; i++)
            {
                SpawnEnemy(entry);
                yield return new WaitForSeconds(delay);
            }
        }

        activeSpawnCoroutine = null;
    }

    private int CountEnemies(List<EnemySpawnEntry> spawnEntries)
    {
        int count = 0;

        foreach (EnemySpawnEntry entry in spawnEntries)
        {
            if (entry == null)
                continue;

            count += Mathf.Max(0, entry.amount);
        }

        return count;
    }

    private float GetSafeSpawnDelay(EnemySpawnEntry entry)
    {
        float baseDelay;

        if (entry == null)
            baseDelay = spawnDelay;
        else if (entry.spawnDelay > 0f)
            baseDelay = entry.spawnDelay;
        else
            baseDelay = spawnDelay;

        baseDelay = Mathf.Max(0.05f, baseDelay);
        float waveMultiplier = GetWaveSpawnDelayMultiplier();
        float scaledDelay = baseDelay * waveMultiplier;
        return Mathf.Max(minimumSpawnDelay, scaledDelay);
    }

    private float GetWaveSpawnDelayMultiplier()
    {
        if (!scaleSpawnDelayByWave)
            return 1f;

        if (gameManager == null)
            return 1f;

        int currentWave = Mathf.Max(1, gameManager.waveNumber);

        if (currentWave < spawnDelayScalingStartWave)
            return 1f;

        float wavesAfterStart = currentWave - spawnDelayScalingStartWave + 1;
        float tenWaveSteps = wavesAfterStart / 10f;
        float multiplier = 1f - tenWaveSteps * spawnDelayReductionPer10Waves;
        return Mathf.Clamp(multiplier, minSpawnDelayMultiplier, 1f);
    }

    public WaveData BuildWaveDataForWave(int waveNumber, int enemyCount)
    {
        int safeWave = Mathf.Max(1, waveNumber);
        int safeEnemyCount = Mathf.Max(1, enemyCount);
        WaveData waveData = new WaveData();

        waveData.waveNumber = safeWave;
        waveData.requestedEnemyCount = safeEnemyCount;
        waveData.scenario = GetWaveScenario(safeWave);
        waveData.scenarioName = GetScenarioNameForWave(safeWave);
        waveData.specialHint = GetSpecialHintForWave(safeWave);
        waveData.modifiedEnemyCount = GetModifiedEnemyCount(safeEnemyCount, safeWave, waveData.scenario);
        waveData.previewHidden = HasHiddenPreviewModifierForWave(safeWave, waveData.scenario);
        waveData.appliedModifiers = GetActiveWaveModifierCopiesForWave(safeWave, waveData.scenario);
        waveData.modifierSummary = GetActiveWaveModifierSummaryForWave(safeWave, waveData.scenario);
        waveData.spawnEntries = GenerateSpawnEntriesForWave(safeWave, waveData.modifiedEnemyCount);

        ApplyPreparedWaveModifiersToEntries(waveData.spawnEntries, safeWave, waveData.scenario);

        List<ChaosWaveBlock> chaosWaveBlocks = GenerateChaosWaveBlocksForWave(safeWave, waveData.scenario, waveData.spawnEntries);
        ApplyChaosWaveBlocksToEntries(waveData.spawnEntries, chaosWaveBlocks, safeWave, waveData.scenario);
        waveData.SetChaosWaveBlocks(chaosWaveBlocks);

        ApplyChaosVariantsToEntries(waveData.spawnEntries, safeWave, waveData.scenario, chaosWaveBlocks);
        waveData.RecalculateTotalSpawnCount();
        return waveData;
    }

    private int GetModifiedEnemyCount(int originalEnemyCount, int waveNumber, WaveScenario scenario)
    {
        int modifiedCount = Mathf.Max(1, originalEnemyCount);

        if (!usePreparedWaveModifiers)
            return modifiedCount;

        if (activeWaveModifiers == null || activeWaveModifiers.Count == 0)
            return modifiedCount;

        float multiplier = 1f;
        int flatBonus = 0;

        foreach (WaveModifier modifier in activeWaveModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!modifier.AffectsWave(waveNumber, scenario))
                continue;

            if (modifier.enemyCountMultiplier != 1f || modifier.flatEnemyCountBonus != 0)
            {
                multiplier *= Mathf.Max(0.1f, modifier.enemyCountMultiplier);
                flatBonus += modifier.flatEnemyCountBonus;
            }
        }

        modifiedCount = Mathf.RoundToInt(modifiedCount * multiplier);
        modifiedCount += flatBonus;
        return Mathf.Max(1, modifiedCount);
    }

    private void ApplyPreparedWaveModifiersToEntries(List<EnemySpawnEntry> entries, int waveNumber, WaveScenario scenario)
    {
        if (!usePreparedWaveModifiers || entries == null || activeWaveModifiers == null || activeWaveModifiers.Count == 0)
            return;

        foreach (WaveModifier modifier in activeWaveModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!modifier.AffectsWave(waveNumber, scenario))
                continue;

            ApplySingleWaveModifier(entries, modifier);
        }
    }

    private void ApplySingleWaveModifier(List<EnemySpawnEntry> entries, WaveModifier modifier)
    {
        if (entries == null || modifier == null)
            return;

        bool primaryRoleHandledByType = false;
        bool spawnDelayAlreadyApplied = false;

        switch (modifier.modifierType)
        {
            case WaveModifierType.AddRole:
            case WaveModifierType.MixedRolePressure:
            case WaveModifierType.MiniBossPressure:
            case WaveModifierType.PreBossPressure:
            case WaveModifierType.RewardBoostPrepared:
            case WaveModifierType.ChaosPrepared:
                AddModifierRoleEntry(entries, modifier.roleToAdd, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.MoreRunners:
                AddModifierRoleEntry(entries, EnemyRole.Runner, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.MoreTanks:
                AddModifierRoleEntry(entries, EnemyRole.Tank, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.MoreKnights:
                AddModifierRoleEntry(entries, EnemyRole.Knight, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.MoreMages:
                AddModifierRoleEntry(entries, EnemyRole.Mage, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.MoreLearners:
                AddModifierRoleEntry(entries, EnemyRole.Learner, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.MoreAllRounders:
                AddModifierRoleEntry(entries, EnemyRole.AllRounder, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.ChaosVariantPressure:
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.ChaosWaveBlockPressure:
                primaryRoleHandledByType = true;
                break;
            case WaveModifierType.FasterSpawns:
                ApplySpawnDelayMultiplier(entries, modifier.spawnDelayMultiplier);
                spawnDelayAlreadyApplied = true;
                break;
        }

        if (!primaryRoleHandledByType && modifier.extraRoleAmount > 0)
            AddModifierRoleEntry(entries, modifier.roleToAdd, modifier.extraRoleAmount, modifier.extraRoleSpawnDelay);

        if (modifier.HasSecondaryRoleAdd())
            AddModifierRoleEntry(entries, modifier.secondaryRoleToAdd, modifier.secondaryExtraRoleAmount, modifier.secondaryExtraRoleSpawnDelay);

        if (modifier.HasTertiaryRoleAdd())
            AddModifierRoleEntry(entries, modifier.tertiaryRoleToAdd, modifier.tertiaryExtraRoleAmount, modifier.tertiaryExtraRoleSpawnDelay);

        if (!spawnDelayAlreadyApplied && modifier.spawnDelayMultiplier > 0f && modifier.spawnDelayMultiplier != 1f)
            ApplySpawnDelayMultiplier(entries, modifier.spawnDelayMultiplier);
    }

    private void AddModifierRoleEntry(List<EnemySpawnEntry> entries, EnemyRole role, int amount, float delay)
    {
        if (entries == null || amount <= 0)
            return;

        if (!HasEnemyPrefabForRole(role))
        {
            Debug.LogWarning("EnemySpawner: Risiko-Modifikator wollte " + role + " hinzufügen, aber das passende Prefab fehlt. Eintrag wird übersprungen.");
            return;
        }

        float safeDelay = Mathf.Max(0.05f, delay);
        entries.Add(new EnemySpawnEntry(role, amount, safeDelay));
    }

    private void ApplySpawnDelayMultiplier(List<EnemySpawnEntry> entries, float multiplier)
    {
        if (entries == null)
            return;

        float safeMultiplier = Mathf.Max(0.1f, multiplier);

        foreach (EnemySpawnEntry entry in entries)
        {
            if (entry == null)
                continue;

            entry.spawnDelay = Mathf.Max(0.05f, entry.spawnDelay * safeMultiplier);
        }
    }


    private List<ChaosWaveBlock> GenerateChaosWaveBlocksForWave(int waveNumber, WaveScenario scenario, List<EnemySpawnEntry> entries)
    {
        List<ChaosWaveBlock> selectedBlocks = new List<ChaosWaveBlock>();

        if (!enableChaosWaveBlocksV1 || entries == null || entries.Count == 0)
            return selectedBlocks;

        int chaosLevel = GetCurrentChaosLevel();
        int startLevel = Mathf.Max(1, chaosWaveBlocksStartLevel);

        if (chaosLevel < startLevel)
            return selectedBlocks;

        System.Random rng = CreateChaosWaveBlockRandom(waveNumber, scenario, chaosLevel);
        float chance = GetChaosWaveBlockChanceForWave(chaosLevel, waveNumber, scenario);
        bool forced = forceChaosWaveBlockAtLevel > 0 && chaosLevel >= forceChaosWaveBlockAtLevel;

        if (!forced && rng.NextDouble() > chance)
            return selectedBlocks;

        List<ChaosWaveBlock> candidates = CreateChaosWaveBlockCandidates(chaosLevel, waveNumber, scenario, rng);

        if (candidates.Count == 0)
            return selectedBlocks;

        ShuffleChaosWaveBlocks(candidates, rng);

        int desiredCount = GetDesiredChaosWaveBlockCount(chaosLevel, rng);
        desiredCount = Mathf.Clamp(desiredCount, 1, Mathf.Max(1, maxChaosWaveBlocksV1));
        desiredCount = Mathf.Min(desiredCount, candidates.Count);

        HashSet<ChaosWaveBlockType> usedTypes = new HashSet<ChaosWaveBlockType>();

        foreach (ChaosWaveBlock candidate in candidates)
        {
            if (candidate == null || !candidate.IsValid())
                continue;

            if (usedTypes.Contains(candidate.blockType))
                continue;

            selectedBlocks.Add(candidate.CreateCopy());
            usedTypes.Add(candidate.blockType);

            if (selectedBlocks.Count >= desiredCount)
                break;
        }

        if (logChaosWaveBlockGeneration && selectedBlocks.Count > 0)
        {
            string summary = "";
            foreach (ChaosWaveBlock block in selectedBlocks)
            {
                if (!string.IsNullOrEmpty(summary))
                    summary += ", ";
                summary += block.GetDisplayNameWithStrength();
            }

            Debug.Log("Chaos-Wave-Bausteine V1 für Wave " + waveNumber + " bei Chaos " + chaosLevel + ": " + summary);
        }

        return selectedBlocks;
    }

    private float GetChaosWaveBlockChanceForWave(int chaosLevel, int waveNumber, WaveScenario scenario)
    {
        int startLevel = Mathf.Max(1, chaosWaveBlocksStartLevel);
        int levelsAfterStart = Mathf.Max(0, chaosLevel - startLevel);
        float chance = Mathf.Max(0f, chaosWaveBlockChanceAtStartLevel) + levelsAfterStart * Mathf.Max(0f, chaosWaveBlockChancePerAdditionalLevel);

        if (usePreparedWaveModifiers && activeWaveModifiers != null)
        {
            foreach (WaveModifier modifier in activeWaveModifiers)
            {
                if (modifier == null || !modifier.IsValid())
                    continue;

                if (!modifier.AffectsWave(waveNumber, scenario))
                    continue;

                if (modifier.modifierType == WaveModifierType.ChaosWaveBlockPressure || modifier.strengthensChaosWaveBlocks)
                    chance += Mathf.Max(0f, modifier.chaosWaveBlockChanceBonus);
            }
        }

        return Mathf.Clamp01(chance);
    }

    private int GetDesiredChaosWaveBlockCount(int chaosLevel, System.Random rng)
    {
        int count = 1;
        int maxCount = Mathf.Clamp(maxChaosWaveBlocksV1, 1, 3);

        if (maxCount >= 2 && chaosLevel >= 4 && rng.NextDouble() < 0.55f)
            count++;

        if (maxCount >= 3 && chaosLevel >= 5 && rng.NextDouble() < 0.35f)
            count++;

        return Mathf.Clamp(count, 1, maxCount);
    }

    private List<ChaosWaveBlock> CreateChaosWaveBlockCandidates(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        List<ChaosWaveBlock> candidates = new List<ChaosWaveBlock>();

        if (allowDensityBlock)
            candidates.Add(CreateDensityBlock(chaosLevel, waveNumber, scenario, rng));

        if (allowToughnessBlock)
            candidates.Add(CreateToughnessBlock(chaosLevel, waveNumber, scenario, rng));

        if (allowRolePressureBlock)
            candidates.Add(CreateRolePressureBlock(chaosLevel, waveNumber, scenario, rng));

        if (allowRearGuardBlock && chaosLevel >= 3)
            candidates.Add(CreateRearguardBlock(chaosLevel, waveNumber, scenario, rng));

        if (allowChaosVariantGroupBlock && enableChaosVariantsV1 && chaosLevel >= Mathf.Max(3, chaosVariantsStartLevel))
            candidates.Add(CreateChaosVariantGroupBlock(chaosLevel, waveNumber, scenario, rng));

        if (allowArmorBlock && chaosLevel >= 4)
            candidates.Add(CreateArmorBlock(chaosLevel, waveNumber, scenario, rng));

        if (allowResistanceBlockInV1 && chaosLevel >= 6)
            candidates.Add(CreateResistanceBlock(chaosLevel, waveNumber, scenario, rng));

        if (allowPreviewHiddenBlockInV1 && chaosLevel >= 7)
            candidates.Add(CreatePreviewHiddenBlock(chaosLevel, waveNumber, scenario, rng));

        candidates.RemoveAll(block => block == null || !block.IsValid());
        return candidates;
    }

    private ChaosWaveBlock CreateDensityBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        int strength = GetChaosWaveBlockStrength(ChaosWaveBlockType.Density, chaosLevel, rng);
        float multiplier = Mathf.Clamp(0.96f - strength * 0.045f, 0.78f, 0.95f);

        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.Density,
            displayName = "Verdichtet",
            detailText = "Gegner spawnen dichter. Keine globale Speed-Erhöhung.",
            strengthLevel = strength,
            priority = 50 + strength,
            spawnDelayMultiplier = multiplier
        };
    }

    private ChaosWaveBlock CreateToughnessBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        int strength = GetChaosWaveBlockStrength(ChaosWaveBlockType.Toughness, chaosLevel, rng);
        float multiplier = 1f + 0.06f + strength * 0.035f;

        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.Toughness,
            displayName = "Zäh",
            detailText = "Betroffene Gegner erhalten mehr Haltbarkeit. Keine Extra-Rewards.",
            strengthLevel = strength,
            priority = 60 + strength,
            healthMultiplier = multiplier
        };
    }

    private ChaosWaveBlock CreateRolePressureBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        int strength = GetChaosWaveBlockStrength(ChaosWaveBlockType.RolePressure, chaosLevel, rng);
        EnemyRole role = SelectPressureRoleForScenario(scenario, rng);

        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.RolePressure,
            displayName = "Instabil",
            detailText = "Ein Teil der normalen Gegner wird in eine passendere Druckrolle umgewandelt. Die Gesamtanzahl steigt nicht.",
            strengthLevel = strength,
            priority = 45 + strength,
            preferredRole = role,
            convertedEnemyCount = Mathf.Clamp(1 + strength, 1, 5)
        };
    }

    private ChaosWaveBlock CreateRearguardBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        int strength = GetChaosWaveBlockStrength(ChaosWaveBlockType.Rearguard, chaosLevel, rng);
        EnemyRole role = SelectRearguardRoleForScenario(scenario, rng);

        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.Rearguard,
            displayName = "Nachhut",
            detailText = "Späte Gegner werden in eine gefährlichere Nachhut umgewandelt. Die Gesamtanzahl steigt nicht.",
            strengthLevel = strength,
            priority = 70 + strength,
            preferredRole = role,
            convertedEnemyCount = Mathf.Clamp(1 + strength, 1, 5),
            appendAtEnd = true
        };
    }

    private ChaosWaveBlock CreateArmorBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        int strength = GetChaosWaveBlockStrength(ChaosWaveBlockType.Armor, chaosLevel, rng);
        int armor = Mathf.Clamp(1 + strength / 2, 1, 3);

        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.Armor,
            displayName = "Gepanzert",
            detailText = "Betroffene normale Gegner erhalten kleine Chaos-Rüstung. Boss/MiniBoss werden in V1 nicht unfair gepanzert.",
            strengthLevel = strength,
            priority = 65 + strength,
            armorBonus = armor
        };
    }

    private ChaosWaveBlock CreateChaosVariantGroupBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        int strength = GetChaosWaveBlockStrength(ChaosWaveBlockType.ChaosVariantGroup, chaosLevel, rng);

        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.ChaosVariantGroup,
            displayName = "Violette Gruppe",
            detailText = "Diese Wave erzeugt eher sichtbare Chaos-Varianten. Varianten ersetzen Gegner, statt zusätzliche Gegner zu erzeugen.",
            strengthLevel = strength,
            priority = 55 + strength,
            chaosVariantChanceBonus = 0.05f + strength * 0.025f,
            flatChaosVariantBonus = Mathf.Clamp(1 + strength / 2, 1, 3)
        };
    }

    private ChaosWaveBlock CreateResistanceBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        int strength = GetChaosWaveBlockStrength(ChaosWaveBlockType.Resistance, chaosLevel, rng);

        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.Resistance,
            displayName = "Resistent",
            detailText = "V1-Standard: deaktiviert. Resistenz-Bausteine gehören eigentlich erst zu späteren Chaos-Stufen.",
            strengthLevel = strength,
            priority = 75 + strength,
            effectDamageMultiplier = Mathf.Clamp(0.92f - strength * 0.06f, 0.65f, 0.92f),
            slowResistanceBonus = Mathf.Clamp01(0.06f + strength * 0.04f)
        };
    }

    private ChaosWaveBlock CreatePreviewHiddenBlock(int chaosLevel, int waveNumber, WaveScenario scenario, System.Random rng)
    {
        return new ChaosWaveBlock
        {
            blockType = ChaosWaveBlockType.PreviewHidden,
            displayName = "Verhüllt",
            detailText = "Nicht für V1 aktiv. Preview-Verhüllung gehört ausschließlich zu Chaos 7.",
            strengthLevel = 1,
            priority = 100
        };
    }

    private int GetChaosWaveBlockStrength(ChaosWaveBlockType blockType, int chaosLevel, System.Random rng)
    {
        int baseStrength = 1;

        if (chaosLevel >= 4 && rng.NextDouble() < 0.45f)
            baseStrength++;

        if (chaosLevel >= 5 && rng.NextDouble() < 0.30f)
            baseStrength++;

        baseStrength += GetChaosWaveBlockStrengthBonus(blockType);
        return Mathf.Clamp(baseStrength, 1, 5);
    }

    private int GetChaosWaveBlockStrengthBonus(ChaosWaveBlockType blockType)
    {
        if (!usePreparedWaveModifiers || activeWaveModifiers == null)
            return 0;

        int bonus = 0;

        foreach (WaveModifier modifier in activeWaveModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!(modifier.modifierType == WaveModifierType.ChaosWaveBlockPressure || modifier.strengthensChaosWaveBlocks))
                continue;

            if (modifier.preferredChaosWaveBlockType != ChaosWaveBlockType.None && modifier.preferredChaosWaveBlockType != blockType)
                continue;

            bonus += Mathf.Max(0, modifier.chaosWaveBlockStrengthBonus);
        }

        return bonus;
    }

    private void ShuffleChaosWaveBlocks(List<ChaosWaveBlock> blocks, System.Random rng)
    {
        if (blocks == null || rng == null)
            return;

        for (int i = 0; i < blocks.Count; i++)
        {
            int swapIndex = rng.Next(i, blocks.Count);
            ChaosWaveBlock temp = blocks[i];
            blocks[i] = blocks[swapIndex];
            blocks[swapIndex] = temp;
        }
    }

    private System.Random CreateChaosWaveBlockRandom(int waveNumber, WaveScenario scenario, int chaosLevel)
    {
        int hash = chaosWaveBlockSeedSalt;
        hash = hash * 31 + Mathf.Max(1, waveNumber);
        hash = hash * 31 + chaosLevel;
        hash = hash * 31 + (int)scenario;
        hash = hash * 31 + GetActiveRiskModifierSeedContribution();
        return new System.Random(hash);
    }

    private void ApplyChaosWaveBlocksToEntries(List<EnemySpawnEntry> entries, List<ChaosWaveBlock> blocks, int waveNumber, WaveScenario scenario)
    {
        if (entries == null || blocks == null || blocks.Count == 0)
            return;

        foreach (ChaosWaveBlock block in blocks)
        {
            if (block == null || !block.IsValid())
                continue;

            switch (block.blockType)
            {
                case ChaosWaveBlockType.Density:
                    ApplySpawnDelayMultiplier(entries, block.spawnDelayMultiplier);
                    block.wasApplied = true;
                    break;

                case ChaosWaveBlockType.Toughness:
                    block.actualAffectedEnemies = ApplyChaosWaveStatEffect(entries, block, block.healthMultiplier, 0, 1f, 0f, true);
                    block.wasApplied = block.actualAffectedEnemies > 0;
                    break;

                case ChaosWaveBlockType.Armor:
                    block.actualAffectedEnemies = ApplyChaosWaveStatEffect(entries, block, 1f, block.armorBonus, 1f, 0f, false);
                    block.wasApplied = block.actualAffectedEnemies > 0;
                    break;

                case ChaosWaveBlockType.Resistance:
                    block.actualAffectedEnemies = ApplyChaosWaveStatEffect(entries, block, 1f, 0, block.effectDamageMultiplier, block.slowResistanceBonus, true);
                    block.wasApplied = block.actualAffectedEnemies > 0;
                    break;

                case ChaosWaveBlockType.RolePressure:
                case ChaosWaveBlockType.Rearguard:
                    block.actualAffectedEnemies = ConvertEntriesToChaosPressureRole(entries, block, block.preferredRole, block.convertedEnemyCount, block.appendAtEnd);
                    block.wasApplied = block.actualAffectedEnemies > 0;
                    break;

                case ChaosWaveBlockType.ChaosVariantGroup:
                    block.wasApplied = true;
                    break;

                case ChaosWaveBlockType.PreviewHidden:
                    if (allowPreviewHiddenBlockInV1)
                        block.wasApplied = true;
                    break;
            }
        }

        RemoveEmptySpawnEntries(entries);
    }

    private int ApplyChaosWaveStatEffect(List<EnemySpawnEntry> entries, ChaosWaveBlock block, float healthMultiplier, int armorBonus, float effectDamageMultiplier, float slowResistanceBonus, bool includeSpecialEnemiesWeakly)
    {
        if (entries == null || block == null)
            return 0;

        int affected = 0;

        foreach (EnemySpawnEntry entry in entries)
        {
            if (entry == null || entry.amount <= 0)
                continue;

            float entryHealthMultiplier = Mathf.Max(0.1f, healthMultiplier);
            int entryArmorBonus = armorBonus;
            float entryEffectMultiplier = Mathf.Max(0.1f, effectDamageMultiplier);
            float entrySlowResistanceBonus = slowResistanceBonus;

            if (entry.enemyRole == EnemyRole.Boss || entry.enemyRole == EnemyRole.MiniBoss)
            {
                if (!includeSpecialEnemiesWeakly)
                    continue;

                entryHealthMultiplier = 1f + (entryHealthMultiplier - 1f) * 0.35f;
                entryArmorBonus = Mathf.FloorToInt(entryArmorBonus * 0.35f);
                entryEffectMultiplier = 1f + (entryEffectMultiplier - 1f) * 0.35f;
                entrySlowResistanceBonus *= 0.35f;
            }

            entry.ApplyChaosWaveBlockEffect(block, entryHealthMultiplier, entryArmorBonus, entryEffectMultiplier, entrySlowResistanceBonus);
            affected += Mathf.Max(0, entry.amount);
        }

        return affected;
    }

    private int ConvertEntriesToChaosPressureRole(List<EnemySpawnEntry> entries, ChaosWaveBlock block, EnemyRole targetRole, int requestedAmount, bool appendAtEnd)
    {
        if (entries == null || block == null || requestedAmount <= 0)
            return 0;

        if (!HasEnemyPrefabForRole(targetRole))
            return 0;

        int remaining = Mathf.Max(0, requestedAmount);
        int converted = 0;
        List<EnemySpawnEntry> convertedEntries = new List<EnemySpawnEntry>();

        ConvertFromEntries(entries, convertedEntries, block, targetRole, ref remaining, ref converted, EnemyRole.Standard);

        if (remaining > 0)
            ConvertFromEntries(entries, convertedEntries, block, targetRole, ref remaining, ref converted, EnemyRole.Runner);

        if (remaining > 0)
            ConvertFromEntries(entries, convertedEntries, block, targetRole, ref remaining, ref converted, EnemyRole.Tank);

        if (remaining > 0)
            ConvertFromEntries(entries, convertedEntries, block, targetRole, ref remaining, ref converted, EnemyRole.Knight);

        if (remaining > 0)
            ConvertFromEntries(entries, convertedEntries, block, targetRole, ref remaining, ref converted, EnemyRole.Mage);

        if (remaining > 0)
            ConvertFromEntries(entries, convertedEntries, block, targetRole, ref remaining, ref converted, EnemyRole.Learner);

        if (convertedEntries.Count > 0)
        {
            if (appendAtEnd)
            {
                entries.AddRange(convertedEntries);
            }
            else
            {
                int insertIndex = Mathf.Clamp(entries.Count / 2, 0, entries.Count);
                entries.InsertRange(insertIndex, convertedEntries);
            }
        }

        RemoveEmptySpawnEntries(entries);
        return converted;
    }

    private void ConvertFromEntries(List<EnemySpawnEntry> entries, List<EnemySpawnEntry> convertedEntries, ChaosWaveBlock block, EnemyRole targetRole, ref int remaining, ref int converted, EnemyRole sourceRole)
    {
        if (remaining <= 0 || entries == null || convertedEntries == null)
            return;

        for (int i = entries.Count - 1; i >= 0 && remaining > 0; i--)
        {
            EnemySpawnEntry source = entries[i];

            if (source == null || source.amount <= 0)
                continue;

            if (source.enemyRole != sourceRole)
                continue;

            if (source.enemyRole == targetRole)
                continue;

            if (source.enemyRole == EnemyRole.Boss || source.enemyRole == EnemyRole.MiniBoss)
                continue;

            int take = Mathf.Min(remaining, source.amount);
            source.amount -= take;
            remaining -= take;
            converted += take;

            EnemySpawnEntry convertedEntry = new EnemySpawnEntry(targetRole, EnemyVariantType.Normal, take, GetRecommendedSpawnDelayForRole(targetRole), null);
            convertedEntry.ApplyChaosWaveBlockEffect(block, 1f, 0, 1f, 0f);
            convertedEntries.Add(convertedEntry);
        }
    }

    private void RemoveEmptySpawnEntries(List<EnemySpawnEntry> entries)
    {
        if (entries == null)
            return;

        entries.RemoveAll(entry => entry == null || entry.amount <= 0);
    }

    private EnemyRole SelectPressureRoleForScenario(WaveScenario scenario, System.Random rng)
    {
        List<EnemyRole> roles = new List<EnemyRole>();

        switch (scenario)
        {
            case WaveScenario.RunnerIntro:
            case WaveScenario.RunnerAttack:
                AddRoleIfAvailable(roles, EnemyRole.Runner);
                AddRoleIfAvailable(roles, EnemyRole.Mage);
                break;
            case WaveScenario.TankIntro:
            case WaveScenario.ArmorCheck:
            case WaveScenario.TankArmorCheck:
                AddRoleIfAvailable(roles, EnemyRole.Knight);
                AddRoleIfAvailable(roles, EnemyRole.Tank);
                break;
            case WaveScenario.EffectCheck:
            case WaveScenario.EffectImmunity:
            case WaveScenario.MageIntro:
                AddRoleIfAvailable(roles, EnemyRole.Learner);
                AddRoleIfAvailable(roles, EnemyRole.Mage);
                break;
            case WaveScenario.PreBoss:
            case WaveScenario.Boss:
                AddRoleIfAvailable(roles, EnemyRole.Knight);
                AddRoleIfAvailable(roles, EnemyRole.Mage);
                AddRoleIfAvailable(roles, EnemyRole.Tank);
                break;
            default:
                AddRoleIfAvailable(roles, EnemyRole.Runner);
                AddRoleIfAvailable(roles, EnemyRole.Knight);
                AddRoleIfAvailable(roles, EnemyRole.Mage);
                break;
        }

        if (roles.Count == 0)
            return EnemyRole.Runner;

        return roles[rng.Next(roles.Count)];
    }

    private EnemyRole SelectRearguardRoleForScenario(WaveScenario scenario, System.Random rng)
    {
        List<EnemyRole> roles = new List<EnemyRole>();

        AddRoleIfAvailable(roles, EnemyRole.Knight);
        AddRoleIfAvailable(roles, EnemyRole.Tank);

        if (scenario == WaveScenario.EffectCheck || scenario == WaveScenario.MageIntro || scenario == WaveScenario.PreBoss || scenario == WaveScenario.Boss)
            AddRoleIfAvailable(roles, EnemyRole.Mage);

        if (roles.Count == 0)
            AddRoleIfAvailable(roles, EnemyRole.Runner);

        if (roles.Count == 0)
            return EnemyRole.Standard;

        return roles[rng.Next(roles.Count)];
    }

    private void AddRoleIfAvailable(List<EnemyRole> roles, EnemyRole role)
    {
        if (roles == null)
            return;

        if (HasEnemyPrefabForRole(role))
            roles.Add(role);
    }

    private float GetRecommendedSpawnDelayForRole(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.Runner: return 0.22f;
            case EnemyRole.Tank: return 0.72f;
            case EnemyRole.Knight: return 0.64f;
            case EnemyRole.Mage: return 0.58f;
            case EnemyRole.Learner: return 0.54f;
            case EnemyRole.AllRounder: return 0.78f;
            case EnemyRole.MiniBoss: return 1.25f;
            case EnemyRole.Boss: return 1.50f;
            default: return 0.35f;
        }
    }


    private void ApplyChaosVariantsToEntries(List<EnemySpawnEntry> entries, int waveNumber, WaveScenario scenario, List<ChaosWaveBlock> chaosWaveBlocks)
    {
        if (!enableChaosVariantsV1 || entries == null || entries.Count == 0)
            return;

        int chaosLevel = GetCurrentChaosLevel();

        if (chaosLevel < Mathf.Max(1, chaosVariantsStartLevel))
            return;

        List<int> eligibleIndices = GetEligibleChaosVariantEntryIndices(entries);

        if (eligibleIndices.Count == 0)
            return;

        float chance = GetChaosVariantChanceForWave(chaosLevel, waveNumber, scenario, chaosWaveBlocks);
        int maxVariants = GetChaosVariantCapForWave(chaosLevel, waveNumber, scenario, chaosWaveBlocks);

        if (chance <= 0f || maxVariants <= 0)
            return;

        System.Random rng = CreateChaosVariantRandom(waveNumber, scenario, chaosLevel);
        int remainingCap = maxVariants;
        int totalSelected = 0;
        List<EnemySpawnEntry> rebuiltEntries = new List<EnemySpawnEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            EnemySpawnEntry original = entries[i];

            if (original == null || original.amount <= 0)
                continue;

            if (!CanRoleBecomeChaosVariant(original.enemyRole) || original.variantType == EnemyVariantType.Chaos || remainingCap <= 0)
            {
                rebuiltEntries.Add(original.CreateCopy());
                continue;
            }

            int variantAmount = RollChaosVariantAmount(original.amount, chance, remainingCap, rng);

            if (variantAmount <= 0)
            {
                rebuiltEntries.Add(original.CreateCopy());
                continue;
            }

            int normalAmount = Mathf.Max(0, original.amount - variantAmount);

            if (normalAmount > 0)
                rebuiltEntries.Add(original.CreateCopyWithVariantAndAmount(EnemyVariantType.Normal, normalAmount));

            rebuiltEntries.Add(original.CreateCopyWithVariantAndAmount(EnemyVariantType.Chaos, variantAmount));

            remainingCap -= variantAmount;
            totalSelected += variantAmount;
        }

        if (totalSelected <= 0 && maxVariants > 0 && rng.NextDouble() < Mathf.Clamp01(chance * eligibleIndices.Count))
        {
            ForceOneChaosVariant(rebuiltEntries, rng);
            totalSelected = 1;
        }

        entries.Clear();
        entries.AddRange(rebuiltEntries);

        if (logChaosVariantGeneration && totalSelected > 0)
            Debug.Log("Chaos-Varianten V1 für Wave " + waveNumber + ": " + totalSelected + " Variante(n) bei Chaos " + chaosLevel + ".");
    }

    private List<int> GetEligibleChaosVariantEntryIndices(List<EnemySpawnEntry> entries)
    {
        List<int> indices = new List<int>();

        if (entries == null)
            return indices;

        for (int i = 0; i < entries.Count; i++)
        {
            EnemySpawnEntry entry = entries[i];

            if (entry == null || entry.amount <= 0)
                continue;

            if (!CanRoleBecomeChaosVariant(entry.enemyRole))
                continue;

            if (entry.variantType == EnemyVariantType.Chaos)
                continue;

            indices.Add(i);
        }

        return indices;
    }

    private bool CanRoleBecomeChaosVariant(EnemyRole role)
    {
        if (role == EnemyRole.Boss)
            return allowChaosVariantBossesInV1;

        if (role == EnemyRole.MiniBoss)
            return allowChaosVariantMiniBossesInV1;

        return HasEnemyPrefabForRole(role);
    }

    private int RollChaosVariantAmount(int amount, float chance, int remainingCap, System.Random rng)
    {
        int safeAmount = Mathf.Max(0, amount);
        int safeCap = Mathf.Max(0, remainingCap);
        float safeChance = Mathf.Clamp01(chance);

        if (safeAmount <= 0 || safeCap <= 0 || safeChance <= 0f)
            return 0;

        int selected = 0;

        for (int i = 0; i < safeAmount && selected < safeCap; i++)
        {
            if (rng.NextDouble() < safeChance)
                selected++;
        }

        return Mathf.Clamp(selected, 0, safeCap);
    }

    private void ForceOneChaosVariant(List<EnemySpawnEntry> entries, System.Random rng)
    {
        if (entries == null || entries.Count == 0)
            return;

        List<int> candidates = new List<int>();

        for (int i = 0; i < entries.Count; i++)
        {
            EnemySpawnEntry entry = entries[i];

            if (entry == null || entry.amount <= 0)
                continue;

            if (entry.variantType == EnemyVariantType.Chaos)
                continue;

            if (!CanRoleBecomeChaosVariant(entry.enemyRole))
                continue;

            candidates.Add(i);
        }

        if (candidates.Count == 0)
            return;

        int selectedIndex = candidates[rng.Next(candidates.Count)];
        EnemySpawnEntry selected = entries[selectedIndex];

        if (selected.amount <= 1)
        {
            selected.variantType = EnemyVariantType.Chaos;
            return;
        }

        selected.amount -= 1;
        entries.Insert(selectedIndex + 1, selected.CreateCopyWithVariantAndAmount(EnemyVariantType.Chaos, 1));
    }

    private float GetChaosVariantChanceForWave(int chaosLevel, int waveNumber, WaveScenario scenario, List<ChaosWaveBlock> chaosWaveBlocks)
    {
        int startLevel = Mathf.Max(1, chaosVariantsStartLevel);
        int levelsAfterStart = Mathf.Max(0, chaosLevel - startLevel);
        float chance = Mathf.Max(0f, chaosVariantChanceAtStartLevel) + levelsAfterStart * Mathf.Max(0f, chaosVariantChancePerAdditionalLevel);

        if (chaosWaveBlocks != null)
        {
            foreach (ChaosWaveBlock block in chaosWaveBlocks)
            {
                if (block == null || !block.IsValid())
                    continue;

                if (block.blockType == ChaosWaveBlockType.ChaosVariantGroup)
                    chance += Mathf.Max(0f, block.chaosVariantChanceBonus);
            }
        }

        if (usePreparedWaveModifiers && activeWaveModifiers != null)
        {
            foreach (WaveModifier modifier in activeWaveModifiers)
            {
                if (modifier == null || !modifier.IsValid())
                    continue;

                if (!modifier.AffectsWave(waveNumber, scenario))
                    continue;

                if (modifier.modifierType == WaveModifierType.ChaosVariantPressure || modifier.increasesChaosVariantChance)
                    chance += Mathf.Max(0f, modifier.chaosVariantChanceBonus);
            }
        }

        return Mathf.Clamp(chance, 0f, Mathf.Clamp01(maxChaosVariantChance));
    }

    private int GetChaosVariantCapForWave(int chaosLevel, int waveNumber, WaveScenario scenario, List<ChaosWaveBlock> chaosWaveBlocks)
    {
        int startLevel = Mathf.Max(1, chaosVariantsStartLevel);
        int levelsAfterStart = Mathf.Max(0, chaosLevel - startLevel);
        int cap = Mathf.Max(0, maxChaosVariantsAtStartLevel) + levelsAfterStart * Mathf.Max(0, maxChaosVariantsPerAdditionalLevel);

        if (chaosWaveBlocks != null)
        {
            foreach (ChaosWaveBlock block in chaosWaveBlocks)
            {
                if (block == null || !block.IsValid())
                    continue;

                if (block.blockType == ChaosWaveBlockType.ChaosVariantGroup)
                    cap += Mathf.Max(0, block.flatChaosVariantBonus);
            }
        }

        if (usePreparedWaveModifiers && activeWaveModifiers != null)
        {
            foreach (WaveModifier modifier in activeWaveModifiers)
            {
                if (modifier == null || !modifier.IsValid())
                    continue;

                if (!modifier.AffectsWave(waveNumber, scenario))
                    continue;

                if (modifier.modifierType == WaveModifierType.ChaosVariantPressure || modifier.increasesChaosVariantChance)
                    cap += Mathf.Max(0, modifier.flatChaosVariantBonus);
            }
        }

        return Mathf.Max(0, cap);
    }

    private System.Random CreateChaosVariantRandom(int waveNumber, WaveScenario scenario, int chaosLevel)
    {
        int hash = chaosVariantSeedSalt;
        hash = hash * 31 + Mathf.Max(1, waveNumber);
        hash = hash * 31 + chaosLevel;
        hash = hash * 31 + (int)scenario;
        hash = hash * 31 + GetActiveRiskModifierSeedContribution();

        return new System.Random(hash);
    }

    private int GetActiveRiskModifierSeedContribution()
    {
        if (activeWaveModifiers == null || activeWaveModifiers.Count == 0)
            return 0;

        unchecked
        {
            int hash = 17;

            foreach (WaveModifier modifier in activeWaveModifiers)
            {
                if (modifier == null)
                    continue;

                hash = hash * 31 + (string.IsNullOrEmpty(modifier.displayName) ? 0 : modifier.displayName.GetHashCode());
                hash = hash * 31 + modifier.riskLevel;
                hash = hash * 31 + modifier.timesSelected;
            }

            return hash;
        }
    }

    private int GetCurrentChaosLevel()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
            return 0;

        ChaosJusticeManager chaosJusticeManager = gameManager.GetChaosJusticeManager();

        if (chaosJusticeManager == null)
            return 0;

        return Mathf.Max(0, chaosJusticeManager.GetChaosLevel());
    }

    private List<EnemySpawnEntry> GenerateSpawnEntriesForWave(int waveNumber, int enemyCount)
    {
        List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>();
        int safeWave = Mathf.Max(1, waveNumber);
        int safeEnemyCount = Mathf.Max(1, enemyCount);
        WaveScenario scenario = GetWaveScenario(safeWave);

        if (TryAddFixedWaveOneToTenEntries(entries, safeWave, scenario))
            return entries;

        switch (scenario)
        {
            case WaveScenario.Boss:
                AddBossWaveEntries(entries, safeWave, safeEnemyCount);
                break;
            case WaveScenario.MiniBoss:
                AddMiniBossWaveEntries(entries, safeWave, safeEnemyCount);
                break;
            case WaveScenario.RunnerAttack:
                AddRunnerScenario(entries, safeEnemyCount);
                break;
            case WaveScenario.TankArmorCheck:
                AddTankScenario(entries, safeEnemyCount);
                break;
            case WaveScenario.EffectCheck:
                AddEffectCheckScenario(entries, safeEnemyCount);
                break;
            case WaveScenario.Mixed:
            default:
                AddMixedScenario(entries, safeEnemyCount);
                break;
        }

        return entries;
    }

    public WaveScenario GetWaveScenario(int waveNumber)
    {
        int safeWave = Mathf.Max(1, waveNumber);

        switch (safeWave)
        {
            case 1: return WaveScenario.StandardIntro;
            case 2: return WaveScenario.RunnerIntro;
            case 3: return WaveScenario.TankIntro;
            case 4: return WaveScenario.ArmorCheck;
            case 5: return WaveScenario.MiniBoss;
            case 6: return WaveScenario.MageIntro;
            case 7: return WaveScenario.EffectImmunity;
            case 8: return WaveScenario.Mixed;
            case 9: return WaveScenario.PreBoss;
            case 10: return WaveScenario.Boss;
        }

        if (safeWave % 10 == 0)
            return WaveScenario.Boss;

        if (safeWave % 5 == 0)
            return WaveScenario.MiniBoss;

        int scenarioIndex = safeWave % 4;

        switch (scenarioIndex)
        {
            case 0: return WaveScenario.RunnerAttack;
            case 1: return WaveScenario.TankArmorCheck;
            case 2: return WaveScenario.EffectCheck;
            default: return WaveScenario.Mixed;
        }
    }

    private bool TryAddFixedWaveOneToTenEntries(List<EnemySpawnEntry> entries, int waveNumber, WaveScenario scenario)
    {
        if (entries == null)
            return false;

        switch (scenario)
        {
            case WaveScenario.StandardIntro:
                AddEntry(entries, EnemyRole.Standard, 5, 0.55f);
                return true;
            case WaveScenario.RunnerIntro:
                AddEntry(entries, EnemyRole.Standard, 5, 0.55f);
                AddEntry(entries, EnemyRole.Runner, 1, 0.38f);
                return true;
            case WaveScenario.TankIntro:
                AddEntry(entries, EnemyRole.Standard, 6, 0.5f);
                AddEntry(entries, EnemyRole.Tank, 2, 0.9f);
                return true;
            case WaveScenario.ArmorCheck:
                AddEntry(entries, EnemyRole.Standard, 7, 0.42f);
                AddEntry(entries, EnemyRole.Runner, 2, 0.3f);
                AddEntry(entries, EnemyRole.Knight, 2, 0.75f);
                return true;
            case WaveScenario.MiniBoss:
                if (waveNumber == 5)
                {
                    AddEntry(entries, EnemyRole.Standard, 6, 0.45f);
                    AddEntry(entries, EnemyRole.Tank, 2, 0.9f);
                    AddEntry(entries, EnemyRole.MiniBoss, 1, 1.5f);
                    AddEntry(entries, EnemyRole.Standard, 5, 0.42f);
                    AddEntry(entries, EnemyRole.Runner, 1, 0.32f);
                    return true;
                }
                return false;
            case WaveScenario.MageIntro:
                AddEntry(entries, EnemyRole.Standard, 8, 0.42f);
                AddEntry(entries, EnemyRole.Runner, 2, 0.3f);
                AddEntry(entries, EnemyRole.Mage, 2, 0.75f);
                return true;
            case WaveScenario.EffectImmunity:
                AddEntry(entries, EnemyRole.Standard, 9, 0.38f);
                AddEntry(entries, EnemyRole.Tank, 3, 0.8f);
                AddEntry(entries, EnemyRole.Learner, 4, 0.58f);
                AddEntry(entries, EnemyRole.Runner, 3, 0.3f);
                return true;
            case WaveScenario.Mixed:
                if (waveNumber == 8)
                {
                    AddEntry(entries, EnemyRole.Standard, 7, 0.36f);
                    AddEntry(entries, EnemyRole.Runner, 4, 0.26f);
                    AddEntry(entries, EnemyRole.Tank, 3, 0.75f);
                    AddEntry(entries, EnemyRole.Mage, 3, 0.7f);
                    AddEntry(entries, EnemyRole.Knight, 2, 0.7f);
                    return true;
                }
                return false;
            case WaveScenario.PreBoss:
                AddEntry(entries, EnemyRole.Standard, 7, 0.36f);
                AddEntry(entries, EnemyRole.Runner, 4, 0.26f);
                AddEntry(entries, EnemyRole.Tank, 4, 0.75f);
                AddEntry(entries, EnemyRole.Knight, 5, 0.7f);
                AddEntry(entries, EnemyRole.Learner, 4, 0.58f);
                return true;
            case WaveScenario.Boss:
                if (waveNumber == 10)
                {
                    AddEntry(entries, EnemyRole.Standard, 7, 0.38f);
                    AddEntry(entries, EnemyRole.Tank, 2, 0.7f);
                    AddEntry(entries, EnemyRole.Knight, 3, 0.65f);
                    AddEntry(entries, EnemyRole.Mage, 2, 0.6f);
                    AddEntry(entries, EnemyRole.Runner, 1, 0.24f);
                    AddEntry(entries, EnemyRole.Boss, 1, 1.5f);
                    return true;
                }
                return false;
        }

        return false;
    }

    private void AddMiniBossWaveEntries(List<EnemySpawnEntry> entries, int waveNumber, int enemyCount)
    {
        if (entries == null)
            return;

        int normalEnemies = Mathf.Max(0, enemyCount - 1);
        MiniBossSpawnPosition spawnPosition = GetMiniBossSpawnPositionForWave(waveNumber);
        int enemiesBeforeMiniBoss;

        switch (spawnPosition)
        {
            case MiniBossSpawnPosition.Early:
                enemiesBeforeMiniBoss = Mathf.RoundToInt(normalEnemies * 0.25f);
                break;
            case MiniBossSpawnPosition.Late:
                enemiesBeforeMiniBoss = Mathf.RoundToInt(normalEnemies * 0.75f);
                break;
            case MiniBossSpawnPosition.Middle:
            default:
                enemiesBeforeMiniBoss = Mathf.RoundToInt(normalEnemies * 0.5f);
                break;
        }

        enemiesBeforeMiniBoss = Mathf.Clamp(enemiesBeforeMiniBoss, 0, normalEnemies);
        int enemiesAfterMiniBoss = normalEnemies - enemiesBeforeMiniBoss;
        AddScenarioEntriesForBackend(entries, GetWaveScenario(waveNumber), enemiesBeforeMiniBoss);
        AddEntry(entries, EnemyRole.MiniBoss, 1, 1.25f);
        AddScenarioEntriesForBackend(entries, GetWaveScenario(waveNumber + 1), enemiesAfterMiniBoss);
    }

    private void AddBossWaveEntries(List<EnemySpawnEntry> entries, int waveNumber, int enemyCount)
    {
        if (entries == null)
            return;

        int normalEnemies = Mathf.Max(0, enemyCount - 1);
        int openerEnemies = Mathf.RoundToInt(normalEnemies * 0.7f);
        openerEnemies = Mathf.Clamp(openerEnemies, 0, normalEnemies);
        int finalPressureEnemies = normalEnemies - openerEnemies;
        AddScenarioEntriesForBackend(entries, GetWaveScenario(waveNumber - 1), openerEnemies);
        AddScenarioEntriesForBackend(entries, WaveScenario.Mixed, finalPressureEnemies);
        AddEntry(entries, EnemyRole.Boss, 1, 1.5f);
    }

    private void AddScenarioEntriesForBackend(List<EnemySpawnEntry> entries, WaveScenario scenario, int enemyCount)
    {
        if (entries == null || enemyCount <= 0)
            return;

        switch (scenario)
        {
            case WaveScenario.RunnerIntro:
            case WaveScenario.RunnerAttack:
                AddRunnerScenario(entries, enemyCount);
                break;
            case WaveScenario.TankIntro:
            case WaveScenario.ArmorCheck:
            case WaveScenario.TankArmorCheck:
                AddTankScenario(entries, enemyCount);
                break;
            case WaveScenario.MageIntro:
            case WaveScenario.EffectImmunity:
            case WaveScenario.EffectCheck:
                AddEffectCheckScenario(entries, enemyCount);
                break;
            case WaveScenario.PreBoss:
            case WaveScenario.Mixed:
            default:
                AddMixedScenario(entries, enemyCount);
                break;
        }
    }

    private void AddRunnerScenario(List<EnemySpawnEntry> entries, int enemyCount)
    {
        int runners = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.5f));
        int standards = Mathf.Max(0, enemyCount - runners);
        AddEntry(entries, EnemyRole.Standard, standards, 0.35f);
        AddEntry(entries, EnemyRole.Runner, runners, 0.2f);
    }

    private void AddTankScenario(List<EnemySpawnEntry> entries, int enemyCount)
    {
        int tanks = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.4f));
        int knights = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.2f));
        int standards = Mathf.Max(0, enemyCount - tanks - knights);
        AddEntry(entries, EnemyRole.Standard, standards, 0.38f);
        AddEntry(entries, EnemyRole.Tank, tanks, 0.75f);
        AddEntry(entries, EnemyRole.Knight, knights, 0.65f);
    }

    private void AddEffectCheckScenario(List<EnemySpawnEntry> entries, int enemyCount)
    {
        int mages = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.25f));
        int learners = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.2f));
        int runners = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.15f));
        int standards = Mathf.Max(0, enemyCount - mages - learners - runners);
        AddEntry(entries, EnemyRole.Standard, standards, 0.36f);
        AddEntry(entries, EnemyRole.Runner, runners, 0.24f);
        AddEntry(entries, EnemyRole.Mage, mages, 0.6f);
        AddEntry(entries, EnemyRole.Learner, learners, 0.55f);
    }

    private void AddMixedScenario(List<EnemySpawnEntry> entries, int enemyCount)
    {
        int runners = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.2f));
        int tanks = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.2f));
        int knights = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.15f));
        int mages = Mathf.Max(1, Mathf.RoundToInt(enemyCount * 0.15f));
        int standards = Mathf.Max(0, enemyCount - runners - tanks - knights - mages);
        AddEntry(entries, EnemyRole.Standard, standards, 0.35f);
        AddEntry(entries, EnemyRole.Runner, runners, 0.24f);
        AddEntry(entries, EnemyRole.Tank, tanks, 0.7f);
        AddEntry(entries, EnemyRole.Knight, knights, 0.6f);
        AddEntry(entries, EnemyRole.Mage, mages, 0.6f);
    }

    private void AddEntry(List<EnemySpawnEntry> entries, EnemyRole role, int amount, float delay)
    {
        if (entries == null || amount <= 0)
            return;

        entries.Add(new EnemySpawnEntry(role, amount, delay));
    }

    private void SpawnEnemy(EnemySpawnEntry entry)
    {
        if (entry == null)
        {
            HandleEnemyFinished(null);
            return;
        }

        GameObject prefabToSpawn = entry.enemyPrefabOverride;

        if (prefabToSpawn == null)
            prefabToSpawn = GetEnemyPrefabForRole(entry.enemyRole, entry.variantType);

        SpawnEnemy(prefabToSpawn, entry.enemyRole, entry.variantType, entry);
    }

    private void SpawnEnemy(GameObject enemyPrefab, EnemyRole role, EnemyVariantType variantType, EnemySpawnEntry spawnEntry)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy Prefab fehlt im EnemySpawner für Rolle: " + role);
            HandleEnemyFinished(null);
            return;
        }

        if (tileManager == null)
        {
            Debug.LogError("EnemySpawner: TileManager fehlt!");
            HandleEnemyFinished(null);
            return;
        }

        GameObject enemyObject = Instantiate(enemyPrefab);
        Enemy enemy = enemyObject.GetComponent<Enemy>();

        if (enemy == null)
        {
            Debug.LogError("Enemy Script fehlt auf Enemy Prefab!");
            Destroy(enemyObject);
            HandleEnemyFinished(null);
            return;
        }

        enemy.OnEnemyFinished += HandleEnemyFinished;
        List<Vector3> path = tileManager.GetWorldPath();
        enemy.Initialize(role, variantType, path, gameManager);
        enemy.ApplySpawnEntryEffects(spawnEntry);
    }

    private GameObject GetEnemyPrefabForRole(EnemyRole role, EnemyVariantType variantType)
    {
        if (preferChaosVariantPrefabs && variantType == EnemyVariantType.Chaos)
        {
            GameObject chaosPrefab = GetChaosVariantPrefabForRole(role);

            if (chaosPrefab != null)
                return chaosPrefab;
        }

        return GetEnemyPrefabForRole(role);
    }

    public GameObject GetChaosVariantPrefabForRole(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.Standard: return chaosStandardEnemyPrefab;
            case EnemyRole.Runner: return chaosRunnerEnemyPrefab;
            case EnemyRole.Tank: return chaosTankEnemyPrefab;
            case EnemyRole.Knight: return chaosKnightEnemyPrefab;
            case EnemyRole.Mage: return chaosMageEnemyPrefab;
            case EnemyRole.Learner: return chaosLearnerEnemyPrefab;
            case EnemyRole.AllRounder: return chaosAllRounderEnemyPrefab;
            default: return null;
        }
    }

    private GameObject GetEnemyPrefabForRole(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.Standard: return standardEnemyPrefab;
            case EnemyRole.Runner: return runnerEnemyPrefab;
            case EnemyRole.Tank: return tankEnemyPrefab;
            case EnemyRole.Knight: return knightEnemyPrefab;
            case EnemyRole.Mage: return mageEnemyPrefab;
            case EnemyRole.Learner: return learnerEnemyPrefab;
            case EnemyRole.AllRounder: return allRounderEnemyPrefab;
            case EnemyRole.MiniBoss: return miniBossEnemyPrefab;
            case EnemyRole.Boss: return bossEnemyPrefab;
            default: return standardEnemyPrefab;
        }
    }

    private void HandleEnemyFinished(Enemy enemy)
    {
        if (enemy != null)
            enemy.OnEnemyFinished -= HandleEnemyFinished;

        aliveEnemies--;

        if (aliveEnemies <= 0)
        {
            aliveEnemies = 0;
            onWaveFinished?.Invoke();
        }
    }

    private MiniBossSpawnPosition GetMiniBossSpawnPositionForWave(int waveNumber)
    {
        int safeWave = Mathf.Max(5, waveNumber);

        if (safeWave == 5)
            return MiniBossSpawnPosition.Middle;

        int miniBossIndex = safeWave / 5;

        switch (miniBossIndex % 3)
        {
            case 0: return MiniBossSpawnPosition.Late;
            case 1: return MiniBossSpawnPosition.Early;
            default: return MiniBossSpawnPosition.Middle;
        }
    }

    private string GetMiniBossSpawnPositionName(int waveNumber)
    {
        MiniBossSpawnPosition position = GetMiniBossSpawnPositionForWave(waveNumber);

        switch (position)
        {
            case MiniBossSpawnPosition.Early: return "früh";
            case MiniBossSpawnPosition.Middle: return "mittig";
            case MiniBossSpawnPosition.Late: return "spät";
            default: return "mittig";
        }
    }

    public void SetPreparedWaveModifiers(List<WaveModifier> modifiers)
    {
        if (activeWaveModifiers == null)
            activeWaveModifiers = new List<WaveModifier>();

        activeWaveModifiers.Clear();

        if (modifiers != null)
        {
            foreach (WaveModifier modifier in modifiers)
            {
                if (modifier == null)
                    continue;

                activeWaveModifiers.Add(modifier.CreateCopy());
            }
        }

        usePreparedWaveModifiers = activeWaveModifiers.Count > 0;
    }

    public void AddPreparedWaveModifier(WaveModifier modifier)
    {
        if (modifier == null)
            return;

        if (activeWaveModifiers == null)
            activeWaveModifiers = new List<WaveModifier>();

        activeWaveModifiers.Add(modifier.CreateCopy());
        usePreparedWaveModifiers = true;
    }

    public void ClearPreparedWaveModifiers()
    {
        if (activeWaveModifiers == null)
        {
            usePreparedWaveModifiers = false;
            return;
        }

        activeWaveModifiers.Clear();
        usePreparedWaveModifiers = false;
    }

    public List<WaveModifier> GetActiveWaveModifierCopies()
    {
        List<WaveModifier> copies = new List<WaveModifier>();

        if (!usePreparedWaveModifiers || activeWaveModifiers == null)
            return copies;

        foreach (WaveModifier modifier in activeWaveModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            copies.Add(modifier.CreateCopy());
        }

        return copies;
    }

    public List<WaveModifier> GetActiveWaveModifierCopiesForWave(int waveNumber, WaveScenario scenario)
    {
        List<WaveModifier> copies = new List<WaveModifier>();

        if (!usePreparedWaveModifiers || activeWaveModifiers == null)
            return copies;

        foreach (WaveModifier modifier in activeWaveModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!modifier.AffectsWave(waveNumber, scenario))
                continue;

            copies.Add(modifier.CreateCopy());
        }

        return copies;
    }

    public string GetActiveWaveModifierSummary()
    {
        return BuildWaveModifierSummary(GetActiveWaveModifierCopies());
    }

    public string GetActiveWaveModifierSummaryForWave(int waveNumber, WaveScenario scenario)
    {
        return BuildWaveModifierSummary(GetActiveWaveModifierCopiesForWave(waveNumber, scenario));
    }

    private string BuildWaveModifierSummary(List<WaveModifier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
            return "";

        string summary = "";

        foreach (WaveModifier modifier in modifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            string name = modifier.GetDisplayNameWithLevel();

            if (!string.IsNullOrEmpty(summary))
                summary += ", ";

            summary += name;
        }

        return summary;
    }

    public bool HasHiddenPreviewModifier()
    {
        int waveNumber = gameManager != null ? Mathf.Max(1, gameManager.waveNumber + 1) : 1;
        WaveScenario scenario = GetWaveScenario(waveNumber);
        return HasHiddenPreviewModifierForWave(waveNumber, scenario);
    }

    public bool HasHiddenPreviewModifierForWave(int waveNumber, WaveScenario scenario)
    {
        if (!usePreparedWaveModifiers || activeWaveModifiers == null)
            return false;

        foreach (WaveModifier modifier in activeWaveModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!modifier.AffectsWave(waveNumber, scenario))
                continue;

            if (modifier.hidePreview || modifier.modifierType == WaveModifierType.HiddenPreview)
                return true;
        }

        return false;
    }

    public bool HasEnemyPrefabForRole(EnemyRole role)
    {
        return GetEnemyPrefabForRole(role) != null;
    }

    public string GetScenarioNameForWave(int waveNumber)
    {
        WaveScenario scenario = GetWaveScenario(waveNumber);

        switch (scenario)
        {
            case WaveScenario.StandardIntro: return "Standard Einführung";
            case WaveScenario.RunnerIntro: return "Runner Einführung";
            case WaveScenario.TankIntro: return "Tank Einführung";
            case WaveScenario.ArmorCheck: return "Armor Check";
            case WaveScenario.MiniBoss: return "MiniBoss Check - " + GetMiniBossSpawnPositionName(waveNumber);
            case WaveScenario.MageIntro: return "Mage Einführung";
            case WaveScenario.EffectImmunity: return "Effect Immunity Check";
            case WaveScenario.Mixed: return "Mixed Wave";
            case WaveScenario.PreBoss: return "Vor-Boss Damage Check";
            case WaveScenario.Boss: return "Boss Check - Boss am Ende";
            case WaveScenario.RunnerAttack: return "Runner Angriff";
            case WaveScenario.TankArmorCheck: return "Tank & Armor Check";
            case WaveScenario.EffectCheck: return "Effect Check";
            default: return scenario.ToString();
        }
    }

    public string GetSpecialHintForWave(int waveNumber)
    {
        WaveScenario scenario = GetWaveScenario(waveNumber);

        switch (scenario)
        {
            case WaveScenario.StandardIntro: return "Basis-Wave zum Aufbau.";
            case WaveScenario.RunnerIntro:
            case WaveScenario.RunnerAttack: return "Runner sind schnell, aber haben wenig Leben.";
            case WaveScenario.TankIntro: return "Tanks sind langsam, aber halten deutlich mehr aus.";
            case WaveScenario.ArmorCheck:
            case WaveScenario.TankArmorCheck: return "Knights testen deinen Schaden pro Treffer durch Armor.";
            case WaveScenario.MiniBoss: return "MiniBoss erscheint " + GetMiniBossSpawnPositionName(waveNumber) + " und gibt globale XP.";
            case WaveScenario.MageIntro: return "Mages teleportieren bei Treffern nach vorne.";
            case WaveScenario.EffectImmunity: return "Learner sind immun gegen Burn, Poison und Slow.";
            case WaveScenario.EffectCheck: return "Effekt-Wave. Achte auf Gegner, die Effekte umgehen oder stören.";
            case WaveScenario.PreBoss: return "Vor-Boss-Test: Prüft, ob dein Schaden für den Boss reicht.";
            case WaveScenario.Boss: return "Boss erscheint am Ende. Boss zerstört keine Tower und gibt globale XP.";
            case WaveScenario.Mixed:
            default: return "Gemischte Wave. Gutes Targeting ist wichtig.";
        }
    }

    public List<EnemySpawnEntry> GetPreviewEntriesForWave(int waveNumber, int enemyCount)
    {
        WaveData waveData = BuildWaveDataForWave(waveNumber, enemyCount);

        if (waveData == null || waveData.spawnEntries == null)
            return new List<EnemySpawnEntry>();

        return new List<EnemySpawnEntry>(waveData.spawnEntries);
    }

    public string GetPreviewTextForWave(int waveNumber, int enemyCount)
    {
        WaveData waveData = BuildWaveDataForWave(waveNumber, enemyCount);

        if (waveData == null)
            return "Keine Gegner";

        if (waveData.previewHidden)
            return "???";

        List<EnemySpawnEntry> previewEntries = waveData.spawnEntries;

        if (previewEntries == null || previewEntries.Count == 0)
            return "Keine Gegner";

        Dictionary<EnemyRole, int> roleCounts = new Dictionary<EnemyRole, int>();
        Dictionary<EnemyRole, int> chaosVariantRoleCounts = new Dictionary<EnemyRole, int>();

        foreach (EnemySpawnEntry entry in previewEntries)
        {
            if (entry == null || entry.amount <= 0)
                continue;

            Dictionary<EnemyRole, int> targetCounts = entry.variantType == EnemyVariantType.Chaos
                ? chaosVariantRoleCounts
                : roleCounts;

            if (targetCounts.ContainsKey(entry.enemyRole))
                targetCounts[entry.enemyRole] += entry.amount;
            else
                targetCounts.Add(entry.enemyRole, entry.amount);
        }

        EnemyRole[] displayOrder =
        {
            EnemyRole.Standard,
            EnemyRole.Runner,
            EnemyRole.Tank,
            EnemyRole.Knight,
            EnemyRole.Mage,
            EnemyRole.Learner,
            EnemyRole.AllRounder,
            EnemyRole.MiniBoss,
            EnemyRole.Boss
        };

        string previewText = "";

        foreach (EnemyRole role in displayOrder)
        {
            AppendRolePreviewLine(ref previewText, roleCounts, role, false);
        }

        foreach (EnemyRole role in displayOrder)
        {
            AppendRolePreviewLine(ref previewText, chaosVariantRoleCounts, role, true);
        }

        if (string.IsNullOrEmpty(previewText))
            return "Keine Gegner";

        return previewText;
    }


    private void AppendRolePreviewLine(ref string previewText, Dictionary<EnemyRole, int> counts, EnemyRole role, bool chaosVariant)
    {
        if (counts == null || !counts.ContainsKey(role))
            return;

        int amount = counts[role];

        if (amount <= 0)
            return;

        if (!string.IsNullOrEmpty(previewText))
            previewText += "\n";

        string prefix = chaosVariant ? "Chaos " : "";
        previewText += amount + " " + prefix + GetEnemyRoleDisplayName(role);
    }

    private string GetEnemyRoleDisplayName(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.Standard: return "Standard";
            case EnemyRole.Runner: return "Runner";
            case EnemyRole.Tank: return "Tank";
            case EnemyRole.Knight: return "Knight";
            case EnemyRole.Mage: return "Mage";
            case EnemyRole.Learner: return "Learner";
            case EnemyRole.AllRounder: return "AllRounder";
            case EnemyRole.MiniBoss: return "MiniBoss";
            case EnemyRole.Boss: return "Boss";
            default: return role.ToString();
        }
    }
}
