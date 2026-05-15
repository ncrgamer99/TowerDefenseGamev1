using UnityEngine;

public enum WaveModifierType
{
    None,

    ExtraEnemies,
    FewerEnemies,

    AddRole,
    MoreRunners,
    MoreTanks,
    MoreKnights,
    MoreMages,
    MoreLearners,
    MoreAllRounders,

    MixedRolePressure,
    ChaosVariantPressure,
    MiniBossPressure,
    PreBossPressure,
    ChaosWaveBlockPressure,

    FasterSpawns,

    HiddenPreview,

    RewardBoostPrepared,
    ChaosPrepared
}

[System.Serializable]
public class WaveModifier
{
    [Header("Info")]
    public string displayName = "Modifier";
    public string description = "";

    [Header("Type")]
    public WaveModifierType modifierType = WaveModifierType.None;

    [Header("Risk Level / Duration")]
    [Tooltip("Intern starten dauerhafte Risiko-Modifikatoren bei 0; die UI zeigt diese erste aktive Stufe als Stufe 1.")]
    public bool isPermanentRiskModifier = true;
    public bool isTemporaryRiskModifier = false;
    public int riskLevel = 0;
    public int timesSelected = 0;

    [Header("Enemy Count")]
    public float enemyCountMultiplier = 1f;
    public int flatEnemyCountBonus = 0;

    [Header("Role Add - Primary")]
    public EnemyRole roleToAdd = EnemyRole.Standard;
    public int extraRoleAmount = 0;
    public float extraRoleSpawnDelay = 0.4f;

    [Header("Role Add - Secondary Optional")]
    public bool useSecondaryRoleToAdd = false;
    public EnemyRole secondaryRoleToAdd = EnemyRole.Standard;
    public int secondaryExtraRoleAmount = 0;
    public float secondaryExtraRoleSpawnDelay = 0.4f;

    [Header("Role Add - Tertiary Optional")]
    public bool useTertiaryRoleToAdd = false;
    public EnemyRole tertiaryRoleToAdd = EnemyRole.Standard;
    public int tertiaryExtraRoleAmount = 0;
    public float tertiaryExtraRoleSpawnDelay = 0.4f;

    [Header("Spawn Timing")]
    public float spawnDelayMultiplier = 1f;

    [Header("Chaos Variant Pressure")]
    public bool increasesChaosVariantChance = false;
    public float chaosVariantChanceBonus = 0f;
    public int flatChaosVariantBonus = 0;
    public bool usePreferredChaosVariantRole = false;
    public EnemyRole preferredChaosVariantRole = EnemyRole.Standard;

    [Header("Chaos Wave Block Pressure")]
    public bool strengthensChaosWaveBlocks = false;
    public ChaosWaveBlockType preferredChaosWaveBlockType = ChaosWaveBlockType.None;
    public int chaosWaveBlockStrengthBonus = 0;
    public float chaosWaveBlockChanceBonus = 0f;

    [Header("Reward Bonus")]
    [Tooltip("Zusätzlicher Gold-Reward-Multiplikator durch diesen Modifier. 0.10 = +10%.")]
    public float goldRewardMultiplierBonus = 0f;

    [Tooltip("Zusätzlicher XP-Reward-Multiplikator durch diesen Modifier. 0.10 = +10%.")]
    public float xpRewardMultiplierBonus = 0f;

    [Header("Prepared Flags")]
    public bool hidePreview = false;
    public bool isChaosModifier = false;
    public bool isRewardModifier = false;

    public bool IsValid()
    {
        return modifierType != WaveModifierType.None;
    }

    public bool HasSecondaryRoleAdd()
    {
        return useSecondaryRoleToAdd && secondaryExtraRoleAmount > 0;
    }

    public bool HasTertiaryRoleAdd()
    {
        return useTertiaryRoleToAdd && tertiaryExtraRoleAmount > 0;
    }

    public bool AffectsWave(int waveNumber, WaveScenario scenario)
    {
        int safeWave = Mathf.Max(1, waveNumber);
        bool isMiniBossWave = scenario == WaveScenario.MiniBoss || (safeWave % 5 == 0 && safeWave % 10 != 0);
        bool isPreBossWave = scenario == WaveScenario.PreBoss || safeWave % 10 == 9;
        bool isBossWave = scenario == WaveScenario.Boss || safeWave % 10 == 0;

        switch (modifierType)
        {
            case WaveModifierType.MiniBossPressure:
                return isMiniBossWave;

            case WaveModifierType.PreBossPressure:
                return isPreBossWave || isBossWave;

            default:
                return true;
        }
    }

    public WaveModifier CreateCopy()
    {
        return new WaveModifier
        {
            displayName = displayName,
            description = description,
            modifierType = modifierType,
            isPermanentRiskModifier = isPermanentRiskModifier,
            isTemporaryRiskModifier = isTemporaryRiskModifier,
            riskLevel = riskLevel,
            timesSelected = timesSelected,
            enemyCountMultiplier = enemyCountMultiplier,
            flatEnemyCountBonus = flatEnemyCountBonus,
            roleToAdd = roleToAdd,
            extraRoleAmount = extraRoleAmount,
            extraRoleSpawnDelay = extraRoleSpawnDelay,
            useSecondaryRoleToAdd = useSecondaryRoleToAdd,
            secondaryRoleToAdd = secondaryRoleToAdd,
            secondaryExtraRoleAmount = secondaryExtraRoleAmount,
            secondaryExtraRoleSpawnDelay = secondaryExtraRoleSpawnDelay,
            useTertiaryRoleToAdd = useTertiaryRoleToAdd,
            tertiaryRoleToAdd = tertiaryRoleToAdd,
            tertiaryExtraRoleAmount = tertiaryExtraRoleAmount,
            tertiaryExtraRoleSpawnDelay = tertiaryExtraRoleSpawnDelay,
            spawnDelayMultiplier = spawnDelayMultiplier,
            increasesChaosVariantChance = increasesChaosVariantChance,
            chaosVariantChanceBonus = chaosVariantChanceBonus,
            flatChaosVariantBonus = flatChaosVariantBonus,
            usePreferredChaosVariantRole = usePreferredChaosVariantRole,
            preferredChaosVariantRole = preferredChaosVariantRole,
            strengthensChaosWaveBlocks = strengthensChaosWaveBlocks,
            preferredChaosWaveBlockType = preferredChaosWaveBlockType,
            chaosWaveBlockStrengthBonus = chaosWaveBlockStrengthBonus,
            chaosWaveBlockChanceBonus = chaosWaveBlockChanceBonus,
            goldRewardMultiplierBonus = goldRewardMultiplierBonus,
            xpRewardMultiplierBonus = xpRewardMultiplierBonus,
            hidePreview = hidePreview,
            isChaosModifier = isChaosModifier,
            isRewardModifier = isRewardModifier
        };
    }

    public EnemyRole GetPrimaryRoleForModifier()
    {
        switch (modifierType)
        {
            case WaveModifierType.MoreRunners:
                return EnemyRole.Runner;
            case WaveModifierType.MoreTanks:
                return EnemyRole.Tank;
            case WaveModifierType.MoreKnights:
                return EnemyRole.Knight;
            case WaveModifierType.MoreMages:
                return EnemyRole.Mage;
            case WaveModifierType.MoreLearners:
                return EnemyRole.Learner;
            case WaveModifierType.MoreAllRounders:
                return EnemyRole.AllRounder;
            case WaveModifierType.MiniBossPressure:
                return EnemyRole.MiniBoss;
            default:
                return roleToAdd;
        }
    }


    public int GetDisplayRiskLevel()
    {
        return Mathf.Max(0, riskLevel) + 1;
    }

    public string GetDisplayNameWithLevel()
    {
        string safeName = string.IsNullOrEmpty(displayName) ? modifierType.ToString() : displayName;

        if (isPermanentRiskModifier)
            return safeName + " (Stufe " + GetDisplayRiskLevel() + ")";

        return safeName;
    }

    public string GetDebugSummary()
    {
        string summary = GetDisplayNameWithLevel();

        if (enemyCountMultiplier != 1f || flatEnemyCountBonus != 0)
            summary += " | Count x" + enemyCountMultiplier.ToString("0.00") + " " + SignedInt(flatEnemyCountBonus);

        AppendRoleSummary(ref summary, GetPrimaryRoleForModifier(), extraRoleAmount);

        if (HasSecondaryRoleAdd())
            AppendRoleSummary(ref summary, secondaryRoleToAdd, secondaryExtraRoleAmount);

        if (HasTertiaryRoleAdd())
            AppendRoleSummary(ref summary, tertiaryRoleToAdd, tertiaryExtraRoleAmount);

        if (spawnDelayMultiplier != 1f)
            summary += " | SpawnDelay x" + spawnDelayMultiplier.ToString("0.00");

        if (increasesChaosVariantChance || chaosVariantChanceBonus > 0f || flatChaosVariantBonus > 0)
        {
            summary += " | Chaos-Varianten +" + Mathf.RoundToInt(Mathf.Max(0f, chaosVariantChanceBonus) * 100f) + "%";

            if (flatChaosVariantBonus > 0)
                summary += " und +" + flatChaosVariantBonus + " garantiert";

            if (usePreferredChaosVariantRole)
                summary += " | bevorzugt: " + preferredChaosVariantRole;
        }

        if (modifierType == WaveModifierType.MiniBossPressure)
            summary += " | wirkt nur auf MiniBoss-Waves";

        if (modifierType == WaveModifierType.PreBossPressure)
            summary += " | wirkt auf Vor-Boss-/Boss-Waves";

        if (modifierType == WaveModifierType.ChaosWaveBlockPressure || strengthensChaosWaveBlocks)
        {
            summary += " | Chaos-Wave-Bausteine";

            if (preferredChaosWaveBlockType != ChaosWaveBlockType.None)
                summary += " bevorzugt: " + preferredChaosWaveBlockType;

            if (chaosWaveBlockStrengthBonus > 0)
                summary += " | Stärke +" + chaosWaveBlockStrengthBonus;

            if (chaosWaveBlockChanceBonus > 0f)
                summary += " | Chance +" + Mathf.RoundToInt(chaosWaveBlockChanceBonus * 100f) + "%";
        }

        if (goldRewardMultiplierBonus > 0f)
            summary += " | Gold +" + Mathf.RoundToInt(goldRewardMultiplierBonus * 100f) + "%";

        if (xpRewardMultiplierBonus > 0f)
            summary += " | XP +" + Mathf.RoundToInt(xpRewardMultiplierBonus * 100f) + "%";

        return summary;
    }

    private void AppendRoleSummary(ref string summary, EnemyRole role, int amount)
    {
        if (amount <= 0)
            return;

        summary += " | +" + amount + " " + role;
    }

    private string SignedInt(int value)
    {
        if (value >= 0)
            return "+" + value;

        return value.ToString();
    }
}
