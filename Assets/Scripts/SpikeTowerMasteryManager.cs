using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum SpikeTowerMasteryPath
{
    Trunk,
    BloodTrail,
    SpikeField,
    ComboCatalyst
}

public enum SpikeTowerKeystone
{
    None,
    BloodNet,
    SpikeBelt,
    DarkReaction
}

[Serializable]
public class SpikeTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class SpikeTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public SpikeTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public SpikeTowerKeystone keystone;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public int GetCostForNextRank(int currentRank)
    {
        return TowerMasteryManager.GetMasteryNodeCostForNextRank(currentRank, maxRank, rankCosts);
    }
}

public class SpikeTowerMasteryShotContext
{
    public Enemy primaryTarget;
}

public class SpikeTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_SpikeMastery_";

    public const string SharpenedSpikes = "sharpened_spikes";
    public const string DeeperCuts = "deeper_cuts";
    public const string LongerBleeding = "longer_bleeding";
    public const string ThornRange = "thorn_range";
    public const string TrapRoutine = "trap_routine";

    public const string DeepWound = "deep_wound";
    public const string OpenTrail = "open_trail";
    public const string KnightCut = "knight_cut";
    public const string TankCut = "tank_cut";
    public const string Afterbleed = "afterbleed";
    public const string HeavyBleeding = "heavy_bleeding";
    public const string ChaosKnightCut = "chaos_knight_cut";
    public const string RiftBleeding = "rift_bleeding";
    public const string BloodNet = "blood_net";

    public const string BroadThornCircle = "broad_thorn_circle";
    public const string QuickTrigger = "quick_trigger";
    public const string MultiSpike = "multi_spike";
    public const string SpikeFieldTraining = "spike_field_training";
    public const string PathReader = "path_reader";
    public const string DenseTrap = "dense_trap";
    public const string RearguardThorn = "rearguard_thorn";
    public const string RiftThornField = "rift_thorn_field";
    public const string SpikeBelt = "spike_belt";

    public const string BloodyTrigger = "bloody_trigger";
    public const string ReactiveWound = "reactive_wound";
    public const string TripleTrail = "triple_trail";
    public const string UnstableCut = "unstable_cut";
    public const string AlchemistSynergy = "alchemist_synergy";
    public const string ComboPreparation = "combo_preparation";
    public const string RiftCombo = "rift_combo";
    public const string ResistanceCut = "resistance_cut";
    public const string DarkReaction = "dark_reaction";

    public static SpikeTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<SpikeTowerMasteryNodeState> nodeStates = new List<SpikeTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public float damageToMasteryXPRatio = 0.025f;
    public int maxDamageMasteryXPPerWave = 16;
    public float bleedToMasteryXPRatio = 0.05f;
    public int maxBleedMasteryXPPerWave = 28;
    public int trapTriggerMasteryXP = 2;
    public int maxTrapTriggerMasteryXPPerWave = 22;
    public int bleedAssistXP = 3;
    public int tankKnightBleedKillXP = 8;
    public int comboKillXP = 12;
    public int chaosVariantKillXP = 7;
    public int miniBossParticipationXP = 8;
    public int bossParticipationXP = 12;
    public int densityRearguardWaveXP = 12;
    public int armorResistanceWaveXP = 10;

    [Header("Milestone Gates")]
    public int bleedAssistsForMasteryGate = 25;

    [Header("Effect Tuning")]
    public float baseTrapTriggerCooldown = 0.16f;
    public float quickTriggerCooldownReduction = 0.025f;
    public float extraTargetRadius = 0.55f;
    public float spikeBeltDirectDamageMultiplier = 0.35f;
    public float bloodNetDamageMultiplier = 0.45f;
    public float bloodNetDurationMultiplier = 0.45f;
    public float darkReactionDamageMultiplier = 0.35f;

    private readonly List<SpikeTowerMasteryNodeDefinition> definitions = new List<SpikeTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, SpikeTowerMasteryNodeDefinition> definitionById = new Dictionary<string, SpikeTowerMasteryNodeDefinition>();
    private readonly HashSet<int> comboReactionTargetsThisWave = new HashSet<int>();
    private readonly HashSet<int> bloodNetTargetsThisWave = new HashSet<int>();

    private bool currentWaveHadSpikeContribution = false;
    private bool currentWaveHasDensity = false;
    private bool currentWaveHasRearguard = false;
    private bool currentWaveHasArmor = false;
    private bool currentWaveHasResistance = false;
    private float currentWaveDamageMasteryXP = 0f;
    private float damageMasteryXPFraction = 0f;
    private float currentWaveBleedMasteryXP = 0f;
    private float bleedMasteryXPFraction = 0f;
    private int currentWaveTrapTriggerXP = 0;
    private int totalBleedAssists = 0;

    public static SpikeTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        SpikeTowerMasteryManager existing = FindObjectOfType<SpikeTowerMasteryManager>();
        if (existing != null)
        {
            if (preferredGameManager != null)
                existing.gameManager = preferredGameManager;

            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("SpikeTowerMasteryManager");
        SpikeTowerMasteryManager manager = go.AddComponent<SpikeTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        return manager;
    }

    public static bool TryGetActive(out SpikeTowerMasteryManager manager)
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
        currentWaveHadSpikeContribution = false;
        currentWaveHasDensity = false;
        currentWaveHasRearguard = false;
        currentWaveHasArmor = false;
        currentWaveHasResistance = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveBleedMasteryXP = 0f;
        bleedMasteryXPFraction = 0f;
        currentWaveTrapTriggerXP = 0;
        comboReactionTargetsThisWave.Clear();
        bloodNetTargetsThisWave.Clear();
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsSpikeTower(tower))
            return;
    }

    public void RecordSpikeKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsSpikeTower(tower))
            return;

        currentWaveHadSpikeContribution = true;
    }

    public void RecordSpikeAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsSpikeTower(tower))
            return;

        currentWaveHadSpikeContribution = true;
    }

    public void RecordSpikeDamage(Tower tower, float appliedDamage)
    {
        if (!IsSpikeTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadSpikeContribution = true;

        if (currentWaveDamageMasteryXP >= maxDamageMasteryXPPerWave)
            return;

        damageMasteryXPFraction += appliedDamage * Mathf.Max(0f, damageToMasteryXPRatio);
        int wholeXP = Mathf.FloorToInt(damageMasteryXPFraction);
        int remainingCap = Mathf.Max(0, maxDamageMasteryXPPerWave - Mathf.FloorToInt(currentWaveDamageMasteryXP));
        int awarded = Mathf.Min(wholeXP, remainingCap);

        if (awarded <= 0)
            return;

        damageMasteryXPFraction -= awarded;
        currentWaveDamageMasteryXP += awarded;
        AddSpikeMasteryXP(awarded);
    }

    public void RecordSpikeDirectDamage(Tower tower, Enemy enemy, float appliedDamage)
    {
        if (!IsSpikeTower(tower) || enemy == null || appliedDamage <= 0f)
            return;

        currentWaveHadSpikeContribution = true;
    }

    public void RecordSpikeTrapTriggered(Tower tower, Enemy enemy)
    {
        if (!IsSpikeTower(tower) || enemy == null)
            return;

        currentWaveHadSpikeContribution = true;

        if (currentWaveTrapTriggerXP < maxTrapTriggerMasteryXPPerWave)
        {
            int awarded = Mathf.Min(trapTriggerMasteryXP, maxTrapTriggerMasteryXPPerWave - currentWaveTrapTriggerXP);
            currentWaveTrapTriggerXP += awarded;
            AddSpikeMasteryXP(awarded);
        }

        TryApplySpikeComboReaction(tower, enemy);
    }

    public void RecordSpikeBleedApplied(Tower tower, Enemy enemy, float duration)
    {
        if (!IsSpikeTower(tower) || enemy == null)
            return;

        currentWaveHadSpikeContribution = true;
        TryApplySpikeComboReaction(tower, enemy);
    }

    public void RecordSpikeBleedDamage(Tower tower, Enemy enemy, float appliedDamage)
    {
        if (!IsSpikeTower(tower) || enemy == null || appliedDamage <= 0f)
            return;

        currentWaveHadSpikeContribution = true;

        if (currentWaveBleedMasteryXP < maxBleedMasteryXPPerWave)
        {
            bleedMasteryXPFraction += appliedDamage * Mathf.Max(0f, bleedToMasteryXPRatio);
            int wholeXP = Mathf.FloorToInt(bleedMasteryXPFraction);
            int remainingCap = Mathf.Max(0, maxBleedMasteryXPPerWave - Mathf.FloorToInt(currentWaveBleedMasteryXP));
            int awarded = Mathf.Min(wholeXP, remainingCap);

            if (awarded > 0)
            {
                bleedMasteryXPFraction -= awarded;
                currentWaveBleedMasteryXP += awarded;
                AddSpikeMasteryXP(awarded);
            }
        }

        TryApplySpikeComboReaction(tower, enemy);
    }

    public SpikeTowerMasteryShotContext PrepareSpikeShot(Tower tower, Enemy target)
    {
        return new SpikeTowerMasteryShotContext
        {
            primaryTarget = target
        };
    }

    public int CalculateSpikeShotDamage(Tower tower, Enemy target, int baseDamage, SpikeTowerMasteryShotContext context)
    {
        return Mathf.Max(0, baseDamage);
    }

    public int ModifySpikeDirectHitDamage(Tower tower, Enemy enemy, int baseDamage)
    {
        if (!IsSpikeTower(tower) || enemy == null)
            return baseDamage;

        float damage = Mathf.Max(0, baseDamage);

        if ((enemy.HasBurn() || enemy.HasPoison()) && GetNodeRank(BloodyTrigger) > 0)
            damage *= 1f + GetNodeRank(BloodyTrigger) * 0.02f;

        if (GetNodeRank(PathReader) > 0 && enemy.GetPathProgressPercent() >= 0.7f)
            damage *= 1.08f;

        if (currentWaveHasRearguard && GetNodeRank(RearguardThorn) > 0 && IsHardTarget(enemy) && enemy.GetPathProgressPercent() >= 0.55f)
            damage *= 1.08f;

        if (enemy.IsChaosVariant() && GetNodeRank(RiftThornField) > 0)
            damage *= 1.08f;

        return Mathf.Max(0, Mathf.RoundToInt(damage));
    }

    public float GetModifiedSpikeBleedDamage(Tower tower, Enemy enemy, float baseDamage)
    {
        if (!IsSpikeTower(tower))
            return baseDamage;

        float damage = Mathf.Max(0.1f, baseDamage);
        damage += GetNodeRank(DeepWound) * 0.35f;

        if (enemy != null)
        {
            if (enemy.enemyRole == EnemyRole.Knight)
                damage *= 1f + GetNodeRank(KnightCut) * 0.04f;

            if (enemy.enemyRole == EnemyRole.Tank)
                damage *= 1f + GetNodeRank(TankCut) * 0.04f;

            if (enemy.currentHealth > enemy.maxHealth * 0.5f && GetNodeRank(HeavyBleeding) > 0)
                damage *= 1.10f;

            if (enemy.IsChaosVariant() && enemy.enemyRole == EnemyRole.Knight)
                damage *= 1f + GetNodeRank(ChaosKnightCut) * 0.05f;

            if ((enemy.HasBurn() || enemy.HasPoison()) && GetNodeRank(BloodyTrigger) > 0)
                damage *= 1f + GetNodeRank(BloodyTrigger) * 0.02f;

            if ((enemy.HasBurn() || enemy.HasPoison()) && GetNodeRank(UnstableCut) > 0 && enemy.HasBleed())
                damage *= 1.10f;

            if (enemy.IsChaosVariant() && GetNodeRank(RiftCombo) > 0 && enemy.GetActiveStatusEffectCount() >= 2)
                damage *= 1.05f;

            if (currentWaveHasResistance && GetNodeRank(ResistanceCut) > 0)
                damage *= 1.10f;
        }

        return Mathf.Max(0.1f, damage);
    }

    public float GetModifiedSpikeBleedDuration(Tower tower, Enemy enemy, float baseDuration)
    {
        if (!IsSpikeTower(tower))
            return baseDuration;

        float duration = Mathf.Max(0.1f, baseDuration);
        duration += GetNodeRank(OpenTrail) * 0.25f;

        if (GetNodeRank(Afterbleed) > 0)
            duration += 0.35f;

        if (enemy != null)
        {
            if ((enemy.HasBurn() || enemy.HasPoison()) && GetNodeRank(ReactiveWound) > 0)
                duration += GetNodeRank(ReactiveWound) * 0.15f;

            if (enemy.IsChaosVariant() && GetNodeRank(RiftBleeding) > 0 && !enemy.immuneToEffects)
                duration += 0.25f;

            if (currentWaveHasResistance && GetNodeRank(ResistanceCut) > 0)
                duration += 0.25f;
        }

        return Mathf.Max(0.1f, duration);
    }

    public float ModifySpikeBleedTickDamage(Tower tower, Enemy enemy, float tickDamage)
    {
        if (!IsSpikeTower(tower) || enemy == null)
            return tickDamage;

        float damage = Mathf.Max(0f, tickDamage);

        if (GetNodeRank(UnstableCut) > 0 && enemy.HasBurn() && enemy.HasPoison() && enemy.HasBleed())
            damage *= 1.10f;

        if (GetActiveKeystone() == SpikeTowerKeystone.DarkReaction && enemy.HasBurn() && enemy.HasPoison() && enemy.HasBleed())
            damage *= 1.10f;

        return Mathf.Max(0f, damage);
    }

    public void FillSpikeProjectileData(Projectile projectile, Tower tower, SpikeTowerMasteryShotContext context)
    {
        if (projectile == null || !IsSpikeTower(tower))
            return;

        projectile.spikeTriggerRadius += GetSpikeTrapRadiusBonus();
        projectile.spikeMaxTrapTriggers = GetSpikeMaxTrapTriggers();
        projectile.spikeTriggerCooldown = GetSpikeTrapTriggerCooldown();
        projectile.spikeTrapDirectDamage = GetSpikeTrapDirectDamage(tower);
        projectile.spikeExtraTargets = GetSpikeExtraTargetCount();
        projectile.spikeExtraDamageMultiplier = GetSpikeExtraDamageMultiplier();
        projectile.spikeExtraTargetRadius = extraTargetRadius;
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower spikeContributor = FindSpikeContributor(killingTower, contributors);

        if (spikeContributor == null)
            return;

        currentWaveHadSpikeContribution = true;
        bool bleedingKill = enemy.HasBleed();

        if (bleedingKill)
        {
            totalBleedAssists += 1;
            SaveProfile();
            AddSpikeMasteryXP(bleedAssistXP);
            MarkMasteryThreeObjective(false);
        }

        if (bleedingKill && (enemy.enemyRole == EnemyRole.Tank || enemy.enemyRole == EnemyRole.Knight))
            AddSpikeMasteryXP(tankKnightBleedKillXP);

        if (enemy.HasBurn() && enemy.HasPoison() && bleedingKill)
            AddSpikeMasteryXP(comboKillXP);

        if (enemy.IsChaosVariant())
            AddSpikeMasteryXP(chaosVariantKillXP);

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
        {
            AddSpikeMasteryXP(miniBossParticipationXP);
            MarkMasteryThreeObjective(true);
        }

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
        {
            AddSpikeMasteryXP(bossParticipationXP);
            MarkMasteryThreeObjective(true);
        }

        if (bleedingKill && GetActiveKeystone() == SpikeTowerKeystone.BloodNet)
            TryApplyBloodNet(spikeContributor, enemy);
    }

    public float GetSpikeDamageBaseBonus()
    {
        return GetNodeRank(SharpenedSpikes) * 0.5f;
    }

    public float GetSpikeBleedDamageBaseBonus()
    {
        return GetNodeRank(DeeperCuts) * 0.25f;
    }

    public float GetSpikeBleedDurationBonus()
    {
        return GetNodeRank(LongerBleeding) * 0.20f;
    }

    public float GetSpikeRangeBonus()
    {
        return GetNodeRank(ThornRange) * 0.08f;
    }

    public float GetSpikeXPMultiplier()
    {
        return 1f + GetNodeRank(TrapRoutine) * 0.03f;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        foreach (SpikeTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public SpikeTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        definitionById.TryGetValue(nodeId, out SpikeTowerMasteryNodeDefinition definition);
        return definition;
    }

    public IEnumerable<SpikeTowerMasteryNodeDefinition> GetDefinitions()
    {
        return definitions;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        if (milestone == TowerMasteryMilestone.None)
            return true;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Spike, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        SpikeTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanEditMetaProgression())
            return false;

        int rank = GetNodeRank(nodeId);
        if (rank >= definition.maxRank)
            return false;

        if (!IsMilestoneUnlocked(definition.gate))
            return false;

        TowerMasteryRoleProfile profile = GetSpikeProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        SpikeTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Spike, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != SpikeTowerKeystone.None && GetActiveKeystone() == SpikeTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(SpikeTowerKeystone keystone)
    {
        if (keystone == SpikeTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(SpikeTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Spike, keystone.ToString());
    }

    public SpikeTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetSpikeProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return SpikeTowerKeystone.None;

        try
        {
            return (SpikeTowerKeystone)Enum.Parse(typeof(SpikeTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return SpikeTowerKeystone.None;
        }
    }

    public string GetNodeStateText(SpikeTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != SpikeTowerKeystone.None && GetActiveKeystone() == definition.keystone)
                return "Aktiv";

            return "Freigeschaltet";
        }

        if (!CanEditMetaProgression())
            return "Read-only im Run";

        if (!IsMilestoneUnlocked(definition.gate))
            return "Gesperrt: " + GetMilestoneDisplayName(definition.gate);

        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryRoleProfile profile = GetSpikeProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetSpikeProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Spike Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Spike im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Spike) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine("Bleed-Assists fuer Spike III: " + Mathf.Min(totalBleedAssists, Mathf.Max(1, bleedAssistsForMasteryGate)) + " / " + Mathf.Max(1, bleedAssistsForMasteryGate));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Spike I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Spike II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Spike III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Spike IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Spike V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Spike Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetSpikeProfile();

        if (profile == null)
            return "Spike Mastery: vorbereitet";

        return "Spike Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        SpikeTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return "Unbekannter Spike-Knoten.";

        int rank = GetNodeRank(nodeId);
        string text = "<b>" + definition.displayName + "</b>\n";
        text += "Pfad: " + GetPathDisplayName(definition.path) + "\n";
        text += "Gate: " + GetMilestoneDisplayName(definition.gate) + "\n";
        text += "Rang: " + rank + " / " + definition.maxRank + "\n";
        text += "Status: " + GetNodeStateText(definition) + "\n";

        if (rank < definition.maxRank)
            text += "Naechste Kosten: " + definition.GetCostForNextRank(rank) + " Spike Point(s)\n";

        text += "\nEffekt:\n" + definition.effectText;

        if (definition.keystone != SpikeTowerKeystone.None)
            text += "\n\nKeystone-Regel: Nur ein Spike Keystone kann aktiv sein. Wechsel wirken erst fuer den naechsten Run.";

        return text;
    }

    public string GetPathDisplayName(SpikeTowerMasteryPath path)
    {
        switch (path)
        {
            case SpikeTowerMasteryPath.BloodTrail: return "Blutspur";
            case SpikeTowerMasteryPath.SpikeField: return "Stachelfeld";
            case SpikeTowerMasteryPath.ComboCatalyst: return "Kombo-Katalysator";
            default: return "Linearer Einstieg";
        }
    }

    public string GetKeystoneDisplayName(SpikeTowerKeystone keystone)
    {
        switch (keystone)
        {
            case SpikeTowerKeystone.BloodNet: return "Blutnetz";
            case SpikeTowerKeystone.SpikeBelt: return "Stachelguertel";
            case SpikeTowerKeystone.DarkReaction: return "Dunkle Reaktion";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveHadSpikeContribution = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveBleedMasteryXP = 0f;
        bleedMasteryXPFraction = 0f;
        currentWaveTrapTriggerXP = 0;
        comboReactionTargetsThisWave.Clear();
        bloodNetTargetsThisWave.Clear();
        currentWaveHasDensity = WaveHasBlock(waveData, ChaosWaveBlockType.Density);
        currentWaveHasRearguard = WaveHasBlock(waveData, ChaosWaveBlockType.Rearguard);
        currentWaveHasArmor = WaveHasBlock(waveData, ChaosWaveBlockType.Armor);
        currentWaveHasResistance = WaveHasBlock(waveData, ChaosWaveBlockType.Resistance);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadSpikeContribution)
            return;

        if ((currentWaveHasDensity || currentWaveHasRearguard) && result.chaosLevelAtWaveStart > 0)
            AddSpikeMasteryXP(densityRearguardWaveXP);

        if ((currentWaveHasArmor || currentWaveHasResistance) && result.chaosLevelAtWaveStart > 0)
            AddSpikeMasteryXP(armorResistanceWaveXP);
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(SharpenedSpikes, "Geschaerfte Spitzen", SpikeTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,5 Spike Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(DeeperCuts, "Tiefere Schnitte", SpikeTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,25 Bleed Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(LongerBleeding, "Laengere Blutung", SpikeTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,20s Bleed Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ThornRange, "Dornenreichweite", SpikeTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,08 Range und etwas Wirkbereich pro Rang.", 1, 2, 3);
        AddDefinition(TrapRoutine, "Fallenroutine", SpikeTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Spike Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);

        AddDefinition(DeepWound, "Tiefe Wunde", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.I, 5, "+0,35 Bleed Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(OpenTrail, "Offene Spur", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.I, 5, "+0,25s Bleed Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(KnightCut, "Knight-Schnitt", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.II, 5, "+4% Bleed Damage gegen Knights pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(TankCut, "Tank-Schnitt", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.II, 5, "+4% Bleed Damage gegen Tanks pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(Afterbleed, "Nachbluten", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.III, 1, "Wiederholte Spike-Treffer verlaengern Bleed leicht, mit Cap.", 8);
        AddDefinition(HeavyBleeding, "Schwere Blutung", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.III, 1, "Ziele ueber 50% HP nehmen +10% Bleed Damage von Spike.", 8);
        AddDefinition(ChaosKnightCut, "Chaos-Knight-Schnitt", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.IV, 3, "+5% Bleed Damage gegen Chaos-Knights pro Rang.", 5, 7, 9);
        AddDefinition(RiftBleeding, "Rissblutung", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten haelt Bleed +0,25s laenger, falls Ziel nicht immun ist.", 10);
        AddDefinition(BloodNet, "Keystone: Blutnetz", SpikeTowerMasteryPath.BloodTrail, TowerMasteryMilestone.V, 1, "Blutende Ziele koennen bei Tod eine reduzierte Blutung auf ein nahes Ziel uebertragen.", new int[] { 24 }, SpikeTowerKeystone.BloodNet);

        AddDefinition(BroadThornCircle, "Breiter Dornkreis", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.I, 5, "Trigger-/Wirkbereich +0,05 pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(QuickTrigger, "Schnelle Ausloesung", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.II, 3, "Spike-Trigger-Cooldown leicht reduziert.", 3, 4, 5);
        AddDefinition(MultiSpike, "Mehrfachstachel", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.II, 1, "Eine Spike-Ausloesung kann bis zu 1 weiteres Ziel in kleinem Bereich treffen.", 6);
        AddDefinition(SpikeFieldTraining, "Stachelfeld-Training", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.III, 2, "Zusatzziel-Schaden +10% pro Rang.", 5, 7);
        AddDefinition(PathReader, "Wegleser", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.III, 1, "Gegner mit hohem Wegfortschritt loesen Spike zuverlaessiger aus und nehmen mehr Spike-Schaden.", 8);
        AddDefinition(DenseTrap, "Dichte Falle", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.IV, 1, "In Density-Waves: Triggerbereich +0,05 und Trigger-Cooldown leicht reduziert.", 10);
        AddDefinition(RearguardThorn, "Nachhutdorn", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.IV, 1, "In Rearguard-Waves: +8% Spike Damage gegen spaete harte Ziele.", 10);
        AddDefinition(RiftThornField, "Rissdornenfeld", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten verursacht Spike +8% direkten Schaden.", 10);
        AddDefinition(SpikeBelt, "Keystone: Stachelguertel", SpikeTowerMasteryPath.SpikeField, TowerMasteryMilestone.V, 1, "Spike-Fallen koennen mehrere Gegner nacheinander treffen, jeder Gegner nur begrenzt.", new int[] { 24 }, SpikeTowerKeystone.SpikeBelt);

        AddDefinition(BloodyTrigger, "Blutiger Ausloeser", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.I, 5, "Gegner mit Burn oder Poison nehmen +2% Spike Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ReactiveWound, "Reaktive Wunde", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.II, 3, "Bleed haelt +0,15s laenger, wenn Ziel bereits Burn oder Poison hat.", 3, 4, 5);
        AddDefinition(TripleTrail, "Dreifachspur", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.II, 1, "Wenn Ziel Burn + Poison + Bleed traegt, wird ein Combo-Fenster vorbereitet.", 8);
        AddDefinition(UnstableCut, "Instabiler Schnitt", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.III, 1, "Instabile Ziele nehmen +10% Bleed Damage von Spike.", 8);
        AddDefinition(AlchemistSynergy, "Alchemist-Synergie", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.III, 1, "Alchemist-Katalyse auf blutenden Zielen wird spaeter leichter vorbereitet.", 8);
        AddDefinition(ComboPreparation, "Combo-Vorbereitung", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.III, 1, "ComboTile/Darkness-Ausloesung wird durch Spike-Bleed leichter vorbereitet.", 10);
        AddDefinition(RiftCombo, "Risskombo", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten zaehlt Bleed fuer Combo-Ausloesung zuverlaessiger, falls Ziel nicht immun ist.", 10);
        AddDefinition(ResistanceCut, "Resistance-Schnitt", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.IV, 1, "In Resistance-Waves verliert Bleed 10% weniger Wirkung.", 12);
        AddDefinition(DarkReaction, "Keystone: Dunkle Reaktion", SpikeTowerMasteryPath.ComboCatalyst, TowerMasteryMilestone.V, 1, "Burn + Poison + Bleed koennen eine dunkle Reaktion vorbereiten: kurzer Bonus-Schaden. Interner Cooldown pro Ziel.", new int[] { 24 }, SpikeTowerKeystone.DarkReaction);
    }

    private void AddDefinition(string nodeId, string displayName, SpikeTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, SpikeTowerKeystone.None);
    }

    private void AddDefinition(string nodeId, string displayName, SpikeTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, SpikeTowerKeystone keystone)
    {
        SpikeTowerMasteryNodeDefinition definition = new SpikeTowerMasteryNodeDefinition
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

    private float GetSpikeTrapRadiusBonus()
    {
        float bonus = GetNodeRank(ThornRange) * 0.04f;
        bonus += GetNodeRank(BroadThornCircle) * 0.05f;

        if (currentWaveHasDensity && GetNodeRank(DenseTrap) > 0)
            bonus += 0.05f;

        return bonus;
    }

    private int GetSpikeMaxTrapTriggers()
    {
        int triggers = 1;

        if (GetActiveKeystone() == SpikeTowerKeystone.SpikeBelt)
            triggers = 4;

        if (currentWaveHasDensity && GetNodeRank(DenseTrap) > 0 && triggers > 1)
            triggers += 1;

        return Mathf.Clamp(triggers, 1, 5);
    }

    private float GetSpikeTrapTriggerCooldown()
    {
        float cooldown = baseTrapTriggerCooldown - GetNodeRank(QuickTrigger) * quickTriggerCooldownReduction;

        if (currentWaveHasDensity && GetNodeRank(DenseTrap) > 0)
            cooldown -= 0.03f;

        return Mathf.Max(0.05f, cooldown);
    }

    private int GetSpikeTrapDirectDamage(Tower tower)
    {
        if (!IsSpikeTower(tower))
            return 0;

        bool directTrapDamage = GetActiveKeystone() == SpikeTowerKeystone.SpikeBelt || GetNodeRank(MultiSpike) > 0;

        if (!directTrapDamage)
            return 0;

        float multiplier = GetActiveKeystone() == SpikeTowerKeystone.SpikeBelt ? spikeBeltDirectDamageMultiplier : 0.20f;
        return Mathf.Max(1, Mathf.RoundToInt(tower.GetEffectiveDamage() * multiplier));
    }

    private int GetSpikeExtraTargetCount()
    {
        return GetNodeRank(MultiSpike) > 0 ? 1 : 0;
    }

    private float GetSpikeExtraDamageMultiplier()
    {
        if (GetNodeRank(MultiSpike) <= 0)
            return 0f;

        return Mathf.Clamp01(0.45f + GetNodeRank(SpikeFieldTraining) * 0.10f);
    }

    private void TryApplySpikeComboReaction(Tower tower, Enemy enemy)
    {
        if (!IsSpikeTower(tower) || enemy == null || enemy.currentHealth <= 0f)
            return;

        bool hasTripleStatus = enemy.HasBurn() && enemy.HasPoison() && enemy.HasBleed();

        if (!hasTripleStatus)
            return;

        if (GetNodeRank(TripleTrail) > 0)
            AddSpikeMasteryXP(1);

        if (GetActiveKeystone() != SpikeTowerKeystone.DarkReaction && GetNodeRank(ComboPreparation) <= 0)
            return;

        int enemyId = enemy.GetInstanceID();
        if (comboReactionTargetsThisWave.Contains(enemyId))
            return;

        comboReactionTargetsThisWave.Add(enemyId);
        float multiplier = GetActiveKeystone() == SpikeTowerKeystone.DarkReaction ? darkReactionDamageMultiplier : 0.16f;
        float bonusDamage = Mathf.Max(1f, tower.GetEffectiveDamage() * multiplier);

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.enemyRole == EnemyRole.Boss || enemy.isMiniBoss || enemy.isBoss)
            bonusDamage *= 0.4f;

        enemy.TakeDamage(bonusDamage, tower, false, 0);
        AddSpikeMasteryXP(4);
    }

    private void TryApplyBloodNet(Tower tower, Enemy sourceEnemy)
    {
        if (!IsSpikeTower(tower) || sourceEnemy == null)
            return;

        int sourceId = sourceEnemy.GetInstanceID();
        if (bloodNetTargetsThisWave.Contains(sourceId))
            return;

        bloodNetTargetsThisWave.Add(sourceId);
        Enemy target = FindNearestBloodNetTarget(sourceEnemy.transform.position, sourceEnemy);

        if (target == null)
            return;

        float bleedDamage = Mathf.Max(0.1f, tower.GetSpikeBleedDamagePerTick() * bloodNetDamageMultiplier);
        float bleedDuration = Mathf.Max(0.1f, tower.GetSpikeBleedDuration() * bloodNetDurationMultiplier);
        target.ApplyBleed(bleedDamage, bleedDuration, 2.5f, tower);
        AddSpikeMasteryXP(5);
    }

    private Enemy FindNearestBloodNetTarget(Vector3 position, Enemy excludedEnemy)
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy bestTarget = null;
        float bestDistance = 1.1f;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy == excludedEnemy || enemy.currentHealth <= 0f || enemy.HasBleed())
                continue;

            float distance = Vector3.Distance(position, enemy.transform.position);

            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    private bool IsHardTarget(Enemy enemy)
    {
        if (enemy == null)
            return false;

        return enemy.armor > 0 ||
               enemy.enemyRole == EnemyRole.Knight ||
               enemy.enemyRole == EnemyRole.Tank ||
               enemy.enemyRole == EnemyRole.AllRounder ||
               enemy.enemyRole == EnemyRole.MiniBoss ||
               enemy.enemyRole == EnemyRole.Boss ||
               enemy.isMiniBoss ||
               enemy.isBoss;
    }

    private Tower FindSpikeContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsSpikeTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsSpikeTower(tower))
                return tower;
        }

        return null;
    }

    private bool IsSpikeTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Spike;
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

    private void AddSpikeMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Spike, amount);
    }

    private void MarkMasteryThreeObjective(bool forceByBossParticipation)
    {
        TowerMasteryRoleProfile profile = GetSpikeProfile();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (profile == null || towerMastery == null)
            return;

        if (!forceByBossParticipation && totalBleedAssists < Mathf.Max(1, bleedAssistsForMasteryGate))
            return;

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Spike);
    }

    private TowerMasteryRoleProfile GetSpikeProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Spike) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetSpikeProfile();
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
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | Bleed-Assists/Boss " + (profile.bossKillWithTower ? "ja" : "nein");
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
            case TowerMasteryMilestone.I: return "Spike I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Spike II: Bluttechnik";
            case TowerMasteryMilestone.III: return "Spike III: Fallenmeisterschaft";
            case TowerMasteryMilestone.IV: return "Spike IV: Rissdornen";
            case TowerMasteryMilestone.V: return "Spike V: Dornenkern";
            default: return "Linearer Einstieg";
        }
    }

    private string GetKeystoneNodeId(SpikeTowerKeystone keystone)
    {
        switch (keystone)
        {
            case SpikeTowerKeystone.BloodNet: return BloodNet;
            case SpikeTowerKeystone.SpikeBelt: return SpikeBelt;
            case SpikeTowerKeystone.DarkReaction: return DarkReaction;
            default: return "";
        }
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        SpikeTowerMasteryNodeState state = null;

        foreach (SpikeTowerMasteryNodeState candidate in nodeStates)
        {
            if (candidate != null && candidate.nodeId == nodeId)
            {
                state = candidate;
                break;
            }
        }

        if (state == null)
        {
            state = new SpikeTowerMasteryNodeState { nodeId = nodeId, rank = 0 };
            nodeStates.Add(state);
        }

        SpikeTowerMasteryNodeDefinition definition = GetDefinition(nodeId);
        int maxRank = definition != null ? definition.maxRank : 1;
        state.rank = Mathf.Clamp(rank, 0, maxRank);
    }

    private void LoadProfile()
    {
        nodeStates.Clear();
        foreach (SpikeTowerMasteryNodeDefinition definition in definitions)
        {
            int rank = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, 0);
            if (rank > 0)
                nodeStates.Add(new SpikeTowerMasteryNodeState { nodeId = definition.nodeId, rank = Mathf.Clamp(rank, 0, definition.maxRank) });
        }

        totalBleedAssists = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBleedAssists", 0);
    }

    private void SaveProfile()
    {
        foreach (SpikeTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBleedAssists", Mathf.Max(0, totalBleedAssists));
        PlayerPrefs.Save();
    }
}
