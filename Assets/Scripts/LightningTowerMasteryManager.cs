using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum LightningTowerMasteryPath
{
    Trunk,
    StormChain,
    ShockControl,
    Overload
}

public enum LightningTowerKeystone
{
    None,
    EndlessConduction,
    LightningAnchor,
    OverloadBurst
}

[Serializable]
public class LightningTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class LightningTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public LightningTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public LightningTowerKeystone keystone;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public int GetCostForNextRank(int currentRank)
    {
        return TowerMasteryManager.GetMasteryNodeCostForNextRank(currentRank, maxRank, rankCosts);
    }
}

public class LightningTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public bool overloadReady;
    public bool overloadBurst;
    public int primaryBonusDamage;
    public int secondaryBonusDamage;
    public int secondaryBonusTargets;
    public bool applyStaticShock;
    public bool applyAnchorShock;
}

public class LightningTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_LightningMastery_";

    public const string ChargedCore = "charged_core";
    public const string FastImpulse = "fast_impulse";
    public const string ConductiveRange = "conductive_range";
    public const string StableConduction = "stable_conduction";
    public const string CurrentRoutine = "current_routine";

    public const string BetterConduction = "better_conduction";
    public const string ExtraJump = "extra_jump";
    public const string JumpTraining = "jump_training";
    public const string DenseDischarge = "dense_discharge";
    public const string ConductiveFocus = "conductive_focus";
    public const string StormPath = "storm_path";
    public const string DensityConduction = "density_conduction";
    public const string VioletConduction = "violet_conduction";
    public const string EndlessConduction = "endless_conduction";

    public const string StaticHit = "static_hit";
    public const string RunnerShock = "runner_shock";
    public const string ShortCircuitWindow = "short_circuit_window";
    public const string MageDisruption = "mage_disruption";
    public const string ShockTargetLogic = "shock_target_logic";
    public const string LeakVoltage = "leak_voltage";
    public const string ChaosRunnerShock = "chaos_runner_shock";
    public const string RiftDisruption = "rift_disruption";
    public const string LightningAnchor = "lightning_anchor";

    public const string ChargeCollector = "charge_collector";
    public const string VoltageDamage = "voltage_damage";
    public const string ChargeCapacity = "charge_capacity";
    public const string FastCharge = "fast_charge";
    public const string OverloadedJump = "overloaded_jump";
    public const string PressureCharge = "pressure_charge";
    public const string ToughWaveVoltage = "tough_wave_voltage";
    public const string VioletBattery = "violet_battery";
    public const string OverloadBurst = "overload_burst";

    public static LightningTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<LightningTowerMasteryNodeState> nodeStates = new List<LightningTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public float damageToMasteryXPRatio = 0.04f;
    public int maxDamageMasteryXPPerWave = 22;
    public int chainHitMasteryXP = 1;
    public int maxChainHitMasteryXPPerWave = 18;
    public int threeTargetChainXP = 8;
    public int runnerMageKillXP = 8;
    public int chaosVariantKillXP = 8;
    public int multiChaosVariantWaveXP = 10;
    public int densityVariantWaveXP = 12;
    public int miniBossParticipationXP = 8;
    public int bossParticipationXP = 12;

    [Header("Milestone Gates")]
    public int threeTargetChainsForMasteryGate = 8;

    [Header("Effect Tuning")]
    public float baseShockDuration = 0.15f;
    public float staticShockMultiplier = 0.88f;
    public float anchorShockDuration = 0.35f;
    public float anchorShockMultiplier = 0.72f;
    public float shockMageDisruptionDelay = 0.25f;
    public int overloadSecondaryTargetLimit = 2;

    private readonly List<LightningTowerMasteryNodeDefinition> definitions = new List<LightningTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, LightningTowerMasteryNodeDefinition> definitionById = new Dictionary<string, LightningTowerMasteryNodeDefinition>();
    private readonly Dictionary<int, int> overloadChargeByTowerId = new Dictionary<int, int>();
    private readonly Dictionary<int, float> densityJumpCooldownByTowerId = new Dictionary<int, float>();
    private readonly Dictionary<int, float> endlessJumpCooldownByTowerId = new Dictionary<int, float>();
    private readonly HashSet<int> violetBatteryEnemiesThisWave = new HashSet<int>();
    private readonly HashSet<int> chaosVariantHitEnemiesThisWave = new HashSet<int>();

    private bool currentWaveHadLightningContribution = false;
    private bool currentWaveHasDensity = false;
    private bool currentWaveHasToughness = false;
    private bool currentWaveHasChaosVariantGroup = false;
    private float currentWaveDamageMasteryXP = 0f;
    private float damageMasteryXPFraction = 0f;
    private int currentWaveChainHitXP = 0;
    private int totalThreeTargetChains = 0;

    public static LightningTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        LightningTowerMasteryManager existing = FindObjectOfType<LightningTowerMasteryManager>();
        if (existing != null)
        {
            if (preferredGameManager != null)
                existing.gameManager = preferredGameManager;

            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("LightningTowerMasteryManager");
        LightningTowerMasteryManager manager = go.AddComponent<LightningTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        return manager;
    }

    public static bool TryGetActive(out LightningTowerMasteryManager manager)
    {
        manager = Instance;
        return manager != null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildDefinitions();
        LoadProfile();
    }

    private void OnEnable()
    {
        WaveEventBus.WaveStarted += HandleWaveStarted;
        WaveEventBus.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        WaveEventBus.WaveStarted -= HandleWaveStarted;
        WaveEventBus.WaveCompleted -= HandleWaveCompleted;
    }

    public void StartNewRun()
    {
        currentWaveHadLightningContribution = false;
        currentWaveHasDensity = false;
        currentWaveHasToughness = false;
        currentWaveHasChaosVariantGroup = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveChainHitXP = 0;
        overloadChargeByTowerId.Clear();
        densityJumpCooldownByTowerId.Clear();
        endlessJumpCooldownByTowerId.Clear();
        violetBatteryEnemiesThisWave.Clear();
        chaosVariantHitEnemiesThisWave.Clear();
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsLightningTower(tower))
            return;
    }

    public void RecordLightningKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsLightningTower(tower))
            return;

        currentWaveHadLightningContribution = true;

        if (killedRole == EnemyRole.Runner || killedRole == EnemyRole.Mage)
            AddLightningMasteryXP(runnerMageKillXP);
    }

    public void RecordLightningAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsLightningTower(tower))
            return;

        currentWaveHadLightningContribution = true;
    }

    public LightningTowerMasteryShotContext PrepareLightningShot(Tower tower, Enemy target)
    {
        LightningTowerMasteryShotContext context = new LightningTowerMasteryShotContext
        {
            primaryTarget = target,
            applyStaticShock = GetNodeRank(StaticHit) > 0,
            applyAnchorShock = GetActiveKeystone() == LightningTowerKeystone.LightningAnchor
        };

        int charge = GetOverloadCharge(tower);
        int maxCharge = GetMaxOverloadCharge();
        bool hasVoltageDamage = GetNodeRank(VoltageDamage) > 0;
        bool hasOverloadBurst = GetActiveKeystone() == LightningTowerKeystone.OverloadBurst;

        if (charge >= maxCharge && (hasVoltageDamage || hasOverloadBurst))
        {
            context.overloadReady = true;
            context.overloadBurst = hasOverloadBurst;
            context.primaryBonusDamage = hasOverloadBurst ? Mathf.Max(2, Mathf.RoundToInt(tower.GetEffectiveDamage() * 0.35f)) : 0;
            context.secondaryBonusDamage = hasOverloadBurst ? Mathf.Max(1, Mathf.RoundToInt(tower.GetEffectiveDamage() * 0.18f)) : 0;
            context.secondaryBonusTargets = hasOverloadBurst ? Mathf.Max(1, overloadSecondaryTargetLimit) : 0;
            SetOverloadCharge(tower, 0);
        }

        return context;
    }

    public int CalculateLightningShotDamage(Tower tower, Enemy target, int baseDamage, LightningTowerMasteryShotContext context)
    {
        float damage = Mathf.Max(0, baseDamage);

        if (target == null)
            return Mathf.Max(0, Mathf.RoundToInt(damage));

        if (target.enemyRole == EnemyRole.Runner)
            damage *= 1f + GetNodeRank(RunnerShock) * 0.04f;

        if (target.enemyRole == EnemyRole.Runner && target.IsChaosVariant())
            damage *= 1f + GetNodeRank(ChaosRunnerShock) * 0.05f;

        if (target.GetPathProgressPercent() >= 0.7f && GetNodeRank(LeakVoltage) > 0)
            damage *= 1.05f;

        if (context != null && context.overloadReady && GetNodeRank(VoltageDamage) > 0)
            damage *= 1.20f;

        if (context != null && context.primaryBonusDamage > 0)
            damage += context.primaryBonusDamage;

        return Mathf.Max(0, Mathf.RoundToInt(damage));
    }

    public void FillLightningProjectileData(Projectile projectile, Tower tower, LightningTowerMasteryShotContext context)
    {
        if (projectile == null || !IsLightningTower(tower))
            return;

        projectile.lightningChainTargets += GetAdditionalChainTargets(tower);
        projectile.lightningChainRange += GetLightningChainRangeBonus(tower);
        projectile.lightningChainDamageMultiplier = GetModifiedChainDamageMultiplier(projectile.lightningChainDamageMultiplier);
        projectile.lightningLineColor = GetActiveKeystone() == LightningTowerKeystone.OverloadBurst
            ? new Color32(170, 225, 255, 255)
            : projectile.lightningLineColor;

        if (context == null)
            return;

        projectile.applyLightningStaticShock = context.applyStaticShock;
        projectile.applyLightningAnchorShock = context.applyAnchorShock;
        projectile.lightningShockDuration = GetLightningShockDuration(context.applyAnchorShock);
        projectile.lightningShockSpeedMultiplier = context.applyAnchorShock ? anchorShockMultiplier : staticShockMultiplier;
        projectile.lightningStaticShockDuration = GetLightningShockDuration(false);
        projectile.lightningStaticShockSpeedMultiplier = staticShockMultiplier;
        projectile.lightningAnchorShockDuration = GetLightningShockDuration(true);
        projectile.lightningAnchorShockSpeedMultiplier = anchorShockMultiplier;
        projectile.lightningDisruptsMage = GetNodeRank(MageDisruption) > 0 || GetNodeRank(RiftDisruption) > 0 || context.applyAnchorShock;
        projectile.lightningOverloadBurst = context.overloadBurst;
        projectile.lightningOverloadSecondaryBonusDamage = context.secondaryBonusDamage;
        projectile.lightningOverloadSecondaryBonusTargets = context.secondaryBonusTargets;
    }

    public void RecordLightningHit(Tower tower, Enemy enemy, float appliedDamage, bool chainHit, int chainIndex)
    {
        if (!IsLightningTower(tower) || enemy == null)
            return;

        currentWaveHadLightningContribution = true;
        AwardDamageMasteryXP(appliedDamage);

        if (chainHit)
        {
            AwardChainHitXP();
            AddOverloadChargeFromChainHit(tower, enemy);
        }

        if (enemy.IsChaosVariant())
            chaosVariantHitEnemiesThisWave.Add(enemy.GetInstanceID());
    }

    public void RecordLightningChainCompleted(Tower tower, int targetsHit)
    {
        if (!IsLightningTower(tower) || targetsHit <= 1)
            return;

        currentWaveHadLightningContribution = true;

        if (targetsHit >= 3)
        {
            totalThreeTargetChains += 1;
            SaveProfile();
            AddLightningMasteryXP(threeTargetChainXP);
            MarkMasteryThreeObjective(false);

            if (GetNodeRank(StormPath) > 0)
                AddOverloadCharge(tower, 1);
        }
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        if (!HasLightningContributor(killingTower, contributors))
            return;

        currentWaveHadLightningContribution = true;

        if (enemy.IsChaosVariant())
            AddLightningMasteryXP(chaosVariantKillXP);

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
        {
            AddLightningMasteryXP(miniBossParticipationXP);
            MarkMasteryThreeObjective(true);
        }

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
        {
            AddLightningMasteryXP(bossParticipationXP);
            MarkMasteryThreeObjective(true);
        }
    }

    public float GetLightningDamageBaseBonus()
    {
        return GetNodeRank(ChargedCore) * 0.5f;
    }

    public float GetLightningRangeBonus()
    {
        return GetNodeRank(ConductiveRange) * 0.12f;
    }

    public float GetLightningFireRateAdditive()
    {
        return GetNodeRank(FastImpulse) * 0.03f;
    }

    public float GetLightningFireRateMultiplier(Tower tower)
    {
        float multiplier = 1f;

        if (GetNodeRank(StormPath) > 0 && CountEnemiesInTowerRange(tower) >= 5)
            multiplier += 0.05f;

        if (GetNodeRank(PressureCharge) > 0 && CountEnemiesInTowerRange(tower) >= 6)
            multiplier += 0.03f;

        return multiplier;
    }

    public float GetLightningXPMultiplier()
    {
        return 1f + GetNodeRank(CurrentRoutine) * 0.03f;
    }

    public float GetModifiedLightningSlowAmount(Tower tower, Enemy enemy, float baseSlowAmount)
    {
        if (!IsLightningTower(tower))
            return baseSlowAmount;

        float amount = Mathf.Clamp(baseSlowAmount, 0.1f, 1f);

        if (GetActiveKeystone() == LightningTowerKeystone.LightningAnchor && enemy != null && !enemy.IsBossOrMiniBossTarget())
            amount = Mathf.Max(0.1f, amount - 0.04f);

        return amount;
    }

    public float GetModifiedLightningSlowDuration(Tower tower, Enemy enemy, float baseDuration)
    {
        if (!IsLightningTower(tower))
            return baseDuration;

        float duration = Mathf.Max(0f, baseDuration);
        duration += GetNodeRank(ShortCircuitWindow) * 0.05f;

        if (enemy != null && enemy.enemyRole == EnemyRole.Mage && GetNodeRank(MageDisruption) > 0)
            duration += 0.15f;

        if (enemy != null && enemy.enemyRole == EnemyRole.Mage && enemy.IsChaosVariant() && GetNodeRank(RiftDisruption) > 0)
            duration += 0.15f;

        if (GetActiveKeystone() == LightningTowerKeystone.LightningAnchor)
            duration += 0.15f;

        return duration;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        foreach (LightningTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public LightningTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        definitionById.TryGetValue(nodeId, out LightningTowerMasteryNodeDefinition definition);
        return definition;
    }

    public IEnumerable<LightningTowerMasteryNodeDefinition> GetDefinitions()
    {
        return definitions;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        if (milestone == TowerMasteryMilestone.None)
            return true;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Lightning, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        LightningTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanEditMetaProgression())
            return false;

        int rank = GetNodeRank(nodeId);
        if (rank >= definition.maxRank)
            return false;

        if (!IsMilestoneUnlocked(definition.gate))
            return false;

        TowerMasteryRoleProfile profile = GetLightningProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        LightningTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Lightning, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != LightningTowerKeystone.None && GetActiveKeystone() == LightningTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(LightningTowerKeystone keystone)
    {
        if (keystone == LightningTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(LightningTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Lightning, keystone.ToString());
    }

    public LightningTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetLightningProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return LightningTowerKeystone.None;

        try
        {
            return (LightningTowerKeystone)Enum.Parse(typeof(LightningTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return LightningTowerKeystone.None;
        }
    }

    public string GetNodeStateText(LightningTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != LightningTowerKeystone.None && GetActiveKeystone() == definition.keystone)
                return "Aktiv";

            return "Freigeschaltet";
        }

        if (!CanEditMetaProgression())
            return "Read-only im Run";

        if (!IsMilestoneUnlocked(definition.gate))
            return "Gesperrt: " + GetMilestoneDisplayName(definition.gate);

        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryRoleProfile profile = GetLightningProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetLightningProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Lightning Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Lightning im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Lightning) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine("3+-Ziel-Chains fuer Lightning III: " + Mathf.Min(totalThreeTargetChains, Mathf.Max(1, threeTargetChainsForMasteryGate)) + " / " + Mathf.Max(1, threeTargetChainsForMasteryGate));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Lightning I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Lightning II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Lightning III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Lightning IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Lightning V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Lightning Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetLightningProfile();

        if (profile == null)
            return "Lightning Mastery: vorbereitet";

        return "Lightning Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        LightningTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return GetOverviewText();

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        string text =
            "<b>" + definition.displayName + "</b>\n" +
            "Pfad: " + GetPathDisplayName(definition.path) + "\n" +
            "Rang: " + rank + " / " + definition.maxRank + "\n" +
            "Gate: " + GetMilestoneDisplayName(definition.gate) + "\n" +
            "Status: " + GetNodeStateText(definition) + "\n";

        if (rank < definition.maxRank)
            text += "Naechste Kosten: " + cost + " Punkt(e)\n";

        text += "\n" + definition.effectText;

        if (definition.keystone != LightningTowerKeystone.None)
            text += "\n\nKeystone-Regel: Nur ein Lightning Keystone kann aktiv sein. Wechsel wirken erst fuer den naechsten Run.";

        return text;
    }

    public string GetPathDisplayName(LightningTowerMasteryPath path)
    {
        switch (path)
        {
            case LightningTowerMasteryPath.StormChain: return "Sturmkette";
            case LightningTowerMasteryPath.ShockControl: return "Schockkontrolle";
            case LightningTowerMasteryPath.Overload: return "Ueberladung";
            default: return "Linearer Einstieg";
        }
    }

    public string GetKeystoneDisplayName(LightningTowerKeystone keystone)
    {
        switch (keystone)
        {
            case LightningTowerKeystone.EndlessConduction: return "Endlose Leitung";
            case LightningTowerKeystone.LightningAnchor: return "Blitzanker";
            case LightningTowerKeystone.OverloadBurst: return "Ueberladungsausbruch";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveHadLightningContribution = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveChainHitXP = 0;
        currentWaveHasDensity = WaveHasBlock(waveData, ChaosWaveBlockType.Density);
        currentWaveHasToughness = WaveHasBlock(waveData, ChaosWaveBlockType.Toughness);
        currentWaveHasChaosVariantGroup = WaveHasBlock(waveData, ChaosWaveBlockType.ChaosVariantGroup);
        violetBatteryEnemiesThisWave.Clear();
        chaosVariantHitEnemiesThisWave.Clear();
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadLightningContribution)
            return;

        if ((currentWaveHasDensity || currentWaveHasChaosVariantGroup) && result.chaosLevelAtWaveStart > 0)
            AddLightningMasteryXP(densityVariantWaveXP);

        if (chaosVariantHitEnemiesThisWave.Count >= 3)
            AddLightningMasteryXP(multiChaosVariantWaveXP);
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(ChargedCore, "Geladener Kern", LightningTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,5 Lightning Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FastImpulse, "Schneller Impuls", LightningTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,03 Fire Rate pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ConductiveRange, "Leitreichweite", LightningTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,12 Range pro Rang.", 1, 2, 3);
        AddDefinition(StableConduction, "Stabile Leitung", LightningTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Chain-Reichweite +0,10 pro Rang.", 1, 2, 3);
        AddDefinition(CurrentRoutine, "Stromroutine", LightningTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Lightning Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);

        AddDefinition(BetterConduction, "Bessere Leitung", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.I, 5, "Chain-Reichweite +0,15 pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ExtraJump, "Zusaetzlicher Sprung", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.II, 1, "+1 Chain-Ziel.", 6);
        AddDefinition(JumpTraining, "Sprungtraining", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.II, 3, "Chain-Schaden verliert 5% weniger pro Sprung pro Rang.", 4, 5, 6);
        AddDefinition(DenseDischarge, "Dichte Entladung", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.III, 1, "Bei 5+ Gegnern in Range: Chain-Reichweite +0,25.", 8);
        AddDefinition(ConductiveFocus, "Leitender Fokus", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.III, 1, "Chain bevorzugt noch nicht getroffene Ziele. Basis-Chain schliesst getroffene Ziele bereits aus.", 8);
        AddDefinition(StormPath, "Sturmpfad", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.III, 1, "Wenn eine Chain 3+ Ziele trifft, erhaelt Lightning kurz Wert und Overload-Ladung.", 8);
        AddDefinition(DensityConduction, "Verdichtungsleitung", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.IV, 1, "In Density-Chaos-Waves: +1 moegliches Chain-Ziel mit Cooldown.", 12);
        AddDefinition(VioletConduction, "Violette Leitung", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Variant-Gruppen verliert Chain-Schaden 10% weniger.", 10);
        AddDefinition(EndlessConduction, "Keystone: Endlose Leitung", LightningTowerMasteryPath.StormChain, TowerMasteryMilestone.V, 1, "Wenn eine Chain ihr letztes Ziel trifft und ein weiteres gueltiges Ziel nahe ist, kann sie einmal zusaetzlich springen. Interner Cooldown pro Schuss.", new int[] { 24 }, LightningTowerKeystone.EndlessConduction);

        AddDefinition(StaticHit, "Statischer Treffer", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.I, 1, "Erster Treffer auf ein Ziel reduziert kurz dessen Speed minimal.", 5);
        AddDefinition(RunnerShock, "Runner-Schock", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.I, 5, "+4% Damage gegen Runner pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ShortCircuitWindow, "Kurzschlussfenster", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.II, 3, "Schock-/Slowdauer +0,05s pro Rang.", 3, 4, 5);
        AddDefinition(MageDisruption, "Mage-Stoerung", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.II, 1, "Treffer gegen Mage stoeren Teleportdruck leicht, ohne Teleport zu verbieten.", 8);
        AddDefinition(ShockTargetLogic, "Schockziel-Logik", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.III, 1, "Vorbereitet bessere Prioritaet fuer nicht geschockte schnelle Ziele.", 6);
        AddDefinition(LeakVoltage, "Leckspannung", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.III, 1, "Gegner ueber 70% Wegfortschritt nehmen +5% Lightning Damage.", 8);
        AddDefinition(ChaosRunnerShock, "Chaos-Runner-Schock", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.IV, 2, "+5% Damage gegen Chaos-Runner pro Rang.", 6, 8);
        AddDefinition(RiftDisruption, "Rissstoerung", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Mage wirkt Schock leicht besser, aber kein Teleport-Verbot.", 10);
        AddDefinition(LightningAnchor, "Keystone: Blitzanker", LightningTowerMasteryPath.ShockControl, TowerMasteryMilestone.V, 1, "Der erste Chain-Treffer setzt einen Blitzanker: Ziel wird kurz staerker gestoert und Chain springt bevorzugt von ihm weiter. Boss/MiniBoss reduziert.", new int[] { 22 }, LightningTowerKeystone.LightningAnchor);

        AddDefinition(ChargeCollector, "Ladungssammler", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.I, 1, "Jeder Chain-Treffer erzeugt 1 Ladung, max. 10.", 6);
        AddDefinition(VoltageDamage, "Spannungsschaden", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.II, 1, "Bei max. Ladung verursacht der naechste Haupttreffer +20% Damage.", 6);
        AddDefinition(ChargeCapacity, "Ladungskapazitaet", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.II, 2, "Max. Ladung +2 pro Rang.", 5, 7);
        AddDefinition(FastCharge, "Schnelle Aufladung", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.III, 1, "Chain-Treffer auf Chaos-Varianten geben +1 zusaetzliche Ladung, mit Cap.", 8);
        AddDefinition(OverloadedJump, "Ueberladener Sprung", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.III, 1, "Entladungs-Treffer verbessert den naechsten Chain-Sprung-Schaden.", 8);
        AddDefinition(PressureCharge, "Druckladung", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.III, 1, "Bei 6+ Gegnern in Range laedt Lightning etwas schneller.", 8);
        AddDefinition(ToughWaveVoltage, "Zaehe-Wellen-Spannung", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.IV, 1, "In Toughness-Chaos-Waves: Ladung baut schneller auf.", 10);
        AddDefinition(VioletBattery, "Violette Batterie", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.IV, 1, "Chaos-Varianten erhoehen Overload-Ladung staerker, aber nur einmal pro Ziel.", 10);
        AddDefinition(OverloadBurst, "Keystone: Ueberladungsausbruch", LightningTowerMasteryPath.Overload, TowerMasteryMilestone.V, 1, "Bei voller Ladung entlaedt Lightning einen starken Overload: Hauptziel Bonus-Schaden, bis zu 2 Chain-Ziele reduzierter Bonus. Danach Cooldown.", new int[] { 24 }, LightningTowerKeystone.OverloadBurst);
    }

    private void AddDefinition(string nodeId, string displayName, LightningTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, LightningTowerKeystone.None);
    }

    private void AddDefinition(string nodeId, string displayName, LightningTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, LightningTowerKeystone keystone)
    {
        LightningTowerMasteryNodeDefinition definition = new LightningTowerMasteryNodeDefinition
        {
            nodeId = nodeId,
            displayName = displayName,
            path = path,
            gate = gate,
            maxRank = Mathf.Max(1, maxRank),
            rankCosts = costs ?? new int[] { 1 },
            effectText = effectText ?? "",
            keystone = keystone
        };

        definitions.Add(definition);
        definitionById[nodeId] = definition;
    }

    private void AwardDamageMasteryXP(float appliedDamage)
    {
        if (appliedDamage <= 0f || currentWaveDamageMasteryXP >= maxDamageMasteryXPPerWave)
            return;

        damageMasteryXPFraction += appliedDamage * Mathf.Max(0f, damageToMasteryXPRatio);
        int wholeXP = Mathf.FloorToInt(damageMasteryXPFraction);
        int remainingCap = Mathf.Max(0, maxDamageMasteryXPPerWave - Mathf.FloorToInt(currentWaveDamageMasteryXP));
        int awarded = Mathf.Min(wholeXP, remainingCap);

        if (awarded <= 0)
            return;

        damageMasteryXPFraction -= awarded;
        currentWaveDamageMasteryXP += awarded;
        AddLightningMasteryXP(awarded);
    }

    private void AwardChainHitXP()
    {
        if (currentWaveChainHitXP >= maxChainHitMasteryXPPerWave)
            return;

        currentWaveChainHitXP += chainHitMasteryXP;
        AddLightningMasteryXP(chainHitMasteryXP);
    }

    private int GetAdditionalChainTargets(Tower tower)
    {
        int extra = 0;

        if (GetNodeRank(ExtraJump) > 0)
            extra += 1;

        if (GetNodeRank(DensityConduction) > 0 && currentWaveHasDensity && CanUseTowerCooldown(densityJumpCooldownByTowerId, tower, 2.5f))
            extra += 1;

        if (GetActiveKeystone() == LightningTowerKeystone.EndlessConduction && CanUseTowerCooldown(endlessJumpCooldownByTowerId, tower, 1.0f))
            extra += 1;

        return extra;
    }

    private float GetLightningChainRangeBonus(Tower tower)
    {
        float bonus = GetNodeRank(StableConduction) * 0.10f + GetNodeRank(BetterConduction) * 0.15f;

        if (GetNodeRank(DenseDischarge) > 0 && CountEnemiesInTowerRange(tower) >= 5)
            bonus += 0.25f;

        return bonus;
    }

    private float GetModifiedChainDamageMultiplier(float baseMultiplier)
    {
        float multiplier = Mathf.Clamp01(baseMultiplier);
        multiplier += GetNodeRank(JumpTraining) * 0.05f;

        if (GetNodeRank(VioletConduction) > 0 && currentWaveHasChaosVariantGroup)
            multiplier += 0.10f;

        if (GetNodeRank(OverloadedJump) > 0 && GetActiveKeystone() == LightningTowerKeystone.OverloadBurst)
            multiplier += 0.05f;

        return Mathf.Clamp(multiplier, 0.1f, 0.9f);
    }

    private void AddOverloadChargeFromChainHit(Tower tower, Enemy enemy)
    {
        if (GetNodeRank(ChargeCollector) <= 0)
            return;

        int amount = 1;

        if (enemy != null && enemy.IsChaosVariant() && GetNodeRank(FastCharge) > 0)
            amount += 1;

        if (enemy != null && enemy.IsChaosVariant() && GetNodeRank(VioletBattery) > 0 && violetBatteryEnemiesThisWave.Add(enemy.GetInstanceID()))
            amount += 1;

        if (currentWaveHasToughness && GetNodeRank(ToughWaveVoltage) > 0)
            amount += 1;

        AddOverloadCharge(tower, amount);
    }

    private void AddOverloadCharge(Tower tower, int amount)
    {
        if (!IsLightningTower(tower) || amount <= 0)
            return;

        int towerId = tower.GetInstanceID();
        int current = GetOverloadCharge(tower);
        overloadChargeByTowerId[towerId] = Mathf.Clamp(current + amount, 0, GetMaxOverloadCharge());
    }

    private int GetOverloadCharge(Tower tower)
    {
        if (!IsLightningTower(tower))
            return 0;

        overloadChargeByTowerId.TryGetValue(tower.GetInstanceID(), out int charge);
        return Mathf.Max(0, charge);
    }

    private void SetOverloadCharge(Tower tower, int charge)
    {
        if (!IsLightningTower(tower))
            return;

        overloadChargeByTowerId[tower.GetInstanceID()] = Mathf.Clamp(charge, 0, GetMaxOverloadCharge());
    }

    private int GetMaxOverloadCharge()
    {
        return 10 + GetNodeRank(ChargeCapacity) * 2;
    }

    private bool CanUseTowerCooldown(Dictionary<int, float> cooldowns, Tower tower, float cooldown)
    {
        if (cooldowns == null || tower == null)
            return false;

        int towerId = tower.GetInstanceID();
        float now = Time.time;

        if (cooldowns.TryGetValue(towerId, out float readyAt) && now < readyAt)
            return false;

        cooldowns[towerId] = now + Mathf.Max(0.1f, cooldown);
        return true;
    }

    private int CountEnemiesInTowerRange(Tower tower)
    {
        if (tower == null)
            return 0;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int count = 0;
        float range = Mathf.Max(0.1f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.currentHealth <= 0f)
                continue;

            if (Vector3.Distance(tower.transform.position, enemy.transform.position) <= range)
                count++;
        }

        return count;
    }

    private float GetLightningShockDuration(bool anchorShock)
    {
        float duration = anchorShock ? anchorShockDuration : baseShockDuration;
        duration += GetNodeRank(ShortCircuitWindow) * 0.05f;

        if (GetNodeRank(RiftDisruption) > 0)
            duration += 0.05f;

        return Mathf.Max(0.05f, duration);
    }

    private bool HasLightningContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsLightningTower(killingTower))
            return true;

        if (contributors == null)
            return false;

        foreach (Tower tower in contributors)
        {
            if (IsLightningTower(tower))
                return true;
        }

        return false;
    }

    private bool IsLightningTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Lightning;
    }

    private bool WaveHasBlock(WaveData waveData, ChaosWaveBlockType blockType)
    {
        if (waveData == null || waveData.chaosWaveBlocks == null)
            return false;

        foreach (ChaosWaveBlock block in waveData.chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
                continue;

            if (block.blockType == blockType)
                return true;
        }

        return false;
    }

    private void AddLightningMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Lightning, amount);
    }

    private void MarkMasteryThreeObjective(bool forceByBossParticipation)
    {
        TowerMasteryRoleProfile profile = GetLightningProfile();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (profile == null || towerMastery == null)
            return;

        if (!forceByBossParticipation && totalThreeTargetChains < Mathf.Max(1, threeTargetChainsForMasteryGate))
            return;

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Lightning);
    }

    private TowerMasteryRoleProfile GetLightningProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Lightning) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetLightningProfile();
        bool unlocked = IsMilestoneUnlocked(milestone);
        string state = unlocked ? "frei" : "gesperrt";

        if (profile == null)
            return state;

        switch (milestone)
        {
            case TowerMasteryMilestone.I:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 10) + "/10 | Level 10 " + (profile.reachedLevel10 ? "ja" : "nein");
            case TowerMasteryMilestone.II:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 25) + "/25 | Level 20 " + (profile.reachedLevel20 ? "ja" : "nein");
            case TowerMasteryMilestone.III:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | 3er-Chain/Boss " + (profile.bossKillWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.IV:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 85) + "/85 | Chaos 3 " + (profile.chaos3WaveWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.V:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 130) + "/130 | Chaos 5/Elite " + (profile.chaos5BossOrEliteWithTower ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private string GetMilestoneDisplayName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return "Lightning I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Lightning II: Leitfaehigkeit";
            case TowerMasteryMilestone.III: return "Lightning III: Strommeisterschaft";
            case TowerMasteryMilestone.IV: return "Lightning IV: Rissladung";
            case TowerMasteryMilestone.V: return "Lightning V: Gewitterkern";
            default: return "Linearer Einstieg";
        }
    }

    private string GetKeystoneNodeId(LightningTowerKeystone keystone)
    {
        switch (keystone)
        {
            case LightningTowerKeystone.EndlessConduction: return EndlessConduction;
            case LightningTowerKeystone.LightningAnchor: return LightningAnchor;
            case LightningTowerKeystone.OverloadBurst: return OverloadBurst;
            default: return "";
        }
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        LightningTowerMasteryNodeState state = null;

        foreach (LightningTowerMasteryNodeState candidate in nodeStates)
        {
            if (candidate != null && candidate.nodeId == nodeId)
            {
                state = candidate;
                break;
            }
        }

        if (state == null)
        {
            state = new LightningTowerMasteryNodeState { nodeId = nodeId, rank = 0 };
            nodeStates.Add(state);
        }

        LightningTowerMasteryNodeDefinition definition = GetDefinition(nodeId);
        int maxRank = definition != null ? definition.maxRank : 1;
        state.rank = Mathf.Clamp(rank, 0, maxRank);
    }

    private void LoadProfile()
    {
        nodeStates.Clear();
        foreach (LightningTowerMasteryNodeDefinition definition in definitions)
        {
            int rank = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, 0);
            if (rank > 0)
                nodeStates.Add(new LightningTowerMasteryNodeState { nodeId = definition.nodeId, rank = Mathf.Clamp(rank, 0, definition.maxRank) });
        }

        totalThreeTargetChains = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalThreeTargetChains", 0);
    }

    private void SaveProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null && towerMastery.IsMetaProgressionSuppressedForCurrentRun())
            return;

        foreach (LightningTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalThreeTargetChains", Mathf.Max(0, totalThreeTargetChains));
        PlayerPrefs.Save();
    }
}
