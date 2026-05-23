using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SniperTowerMasteryPath
{
    Trunk,
    EliteHunter,
    BossFocus,
    LongWatch
}

public enum SniperTowerKeystone
{
    None,
    HeadshotProtocol,
    TargetMark,
    WatcherLine
}

[Serializable]
public class SniperTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

[Serializable]
public class SniperTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public SniperTowerMasteryPath path;
    public int maxRank;
    public int[] costs;
    public TowerMasteryMilestone gate;
    public SniperTowerKeystone keystone;

    public bool IsKeystone => keystone != SniperTowerKeystone.None;

    public int GetCostForNextRank(int currentRank)
    {
        int safeRank = Mathf.Clamp(currentRank, 0, maxRank);

        if (safeRank >= maxRank)
        {
            return 0;
        }

        if (costs == null || costs.Length == 0)
        {
            return TowerMasteryManager.GetRepeatingUpgradeCostForRank(safeRank + 1);
        }

        int index = Mathf.Clamp(safeRank, 0, costs.Length - 1);
        return Mathf.Max(1, costs[index]);
    }
}

public class SniperTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public bool applyHeadshotMark;
    public bool consumeHeadshotMark;
    public bool applyBossMark;
    public bool watcherLineShot;
}

public class SniperTowerMasteryManager : MonoBehaviour
{
    public const string NodeCalibratedBarrel = "sniper_trunk_calibrated_barrel";
    public const string NodeCalmBreath = "sniper_trunk_calm_breath";
    public const string NodeLongScope = "sniper_trunk_long_scope";
    public const string NodeFocusTraining = "sniper_trunk_focus_training";
    public const string NodeFirstHit = "sniper_trunk_first_hit";

    public const string NodeMageReader = "sniper_elite_mage_reader";
    public const string NodeLearnerReader = "sniper_elite_learner_reader";
    public const string NodeElitePriority = "sniper_elite_priority";
    public const string NodeWeakPoint = "sniper_elite_weak_point";
    public const string NodeHeadshotWindow = "sniper_elite_headshot_window";
    public const string NodeAllRounderAnalysis = "sniper_elite_allrounder_analysis";
    public const string NodeChaosMageAnalysis = "sniper_elite_chaos_mage_analysis";
    public const string NodeChaosLearnerAnalysis = "sniper_elite_chaos_learner_analysis";
    public const string NodeHeadshotProtocol = "sniper_elite_headshot_protocol";

    public const string NodeMiniBossSight = "sniper_boss_miniboss_sight";
    public const string NodeBossSight = "sniper_boss_boss_sight";
    public const string NodeTargetBinding = "sniper_boss_target_binding";
    public const string NodeMarkingShot = "sniper_boss_marking_shot";
    public const string NodeMarkPrecision = "sniper_boss_mark_precision";
    public const string NodeLongBreath = "sniper_boss_long_breath";
    public const string NodeRiftBossOptics = "sniper_boss_rift_boss_optics";
    public const string NodePreBossWatcher = "sniper_boss_pre_boss_watcher";
    public const string NodeTargetMark = "sniper_boss_target_mark";

    public const string NodeWatchtowerOptics = "sniper_watch_watchtower_optics";
    public const string NodeEscapeLine = "sniper_watch_escape_line";
    public const string NodeInterceptShot = "sniper_watch_intercept_shot";
    public const string NodeWatchRoutine = "sniper_watch_watch_routine";
    public const string NodeRearguardAnalysis = "sniper_watch_rearguard_analysis";
    public const string NodePreciseRedirect = "sniper_watch_precise_redirect";
    public const string NodePurpleWatch = "sniper_watch_purple_watch";
    public const string NodePreviewTraining = "sniper_watch_preview_training";
    public const string NodeWatcherLine = "sniper_watch_watcher_line";

    public static SniperTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Sniper XP")]
    [SerializeField] private float damageToMasteryXPRatio = 0.08f;
    [SerializeField] private int maxDamageMasteryXPPerWave = 44;
    [SerializeField] private int mageKillBonusXP = 12;
    [SerializeField] private int learnerKillBonusXP = 12;
    [SerializeField] private int allRounderKillBonusXP = 8;
    [SerializeField] private int highProgressKillBonusXP = 6;
    [SerializeField] private int chaosVariantKillBonusXP = 5;
    [SerializeField] private int miniBossParticipationBonusXP = 18;
    [SerializeField] private int bossParticipationBonusXP = 45;
    [SerializeField] private int eliteParticipationBonusXP = 30;

    [Header("Sniper Effects")]
    [SerializeField] private float headshotMarkDuration = 4f;
    [SerializeField] private float bossMarkDuration = 5f;
    [SerializeField] private float watcherLineRangeMultiplier = 1.6f;
    [SerializeField] private float watcherLineMinimumProgress = 0.7f;
    [SerializeField] private float headshotFinisherMultiplier = 1.75f;
    [SerializeField] private float reducedHeadshotFinisherMultiplier = 1.2f;
    [SerializeField] private float watcherLineDamageMultiplier = 1.35f;

    [Header("Milestone Tracking")]
    [SerializeField] private int specialKillsForMasteryGate = 40;

    private readonly Dictionary<string, SniperTowerMasteryNodeDefinition> definitionsById = new Dictionary<string, SniperTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, SniperTowerMasteryNodeState> nodeStatesById = new Dictionary<string, SniperTowerMasteryNodeState>();
    private readonly Dictionary<int, SniperTowerRuntimeState> runtimeStateByTowerId = new Dictionary<int, SniperTowerRuntimeState>();
    private readonly HashSet<int> firstSpecialHitsThisRun = new HashSet<int>();

    private int currentWaveNumber = 0;
    private int currentWaveDamageMasteryXP = 0;
    private float damageMasteryXPFraction = 0f;
    private bool currentWaveHadSniperContribution = false;
    private bool currentWaveHasRearguard = false;
    private bool currentWaveIsPreBoss = false;
    private bool currentWaveIsBoss = false;
    private int totalSpecialSniperKills = 0;
    private SniperTowerKeystone activeKeystone = SniperTowerKeystone.None;

    public IReadOnlyDictionary<string, SniperTowerMasteryNodeDefinition> DefinitionsById => definitionsById;
    public SniperTowerKeystone ActiveKeystone => GetActiveKeystone();
    public int TotalSpecialSniperKills => totalSpecialSniperKills;

    private class SniperTowerRuntimeState
    {
        public Enemy boundTarget;
        public float boundUntilTime = -1f;
        public int watcherLineUsedWave = -1;
    }

    public static SniperTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        var existing = FindObjectOfType<SniperTowerMasteryManager>();
        if (existing != null)
        {
            if (preferredGameManager != null)
                existing.gameManager = preferredGameManager;

            Instance = existing;
            return existing;
        }

        var go = new GameObject("SniperTowerMasteryManager");
        var manager = go.AddComponent<SniperTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        return manager;
    }

    public static bool TryGetActive(out SniperTowerMasteryManager manager)
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
        LoadPersistentState();
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
        runtimeStateByTowerId.Clear();
        firstSpecialHitsThisRun.Clear();
        currentWaveNumber = 0;
        currentWaveDamageMasteryXP = 0;
        damageMasteryXPFraction = 0f;
        currentWaveHadSniperContribution = false;
        currentWaveHasRearguard = false;
        currentWaveIsPreBoss = false;
        currentWaveIsBoss = false;
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (tower == null || tower.towerRole != TowerRole.Sniper)
        {
            return;
        }

        GetRuntimeState(tower);
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        currentWaveDamageMasteryXP = 0;
        damageMasteryXPFraction = 0f;
        currentWaveHadSniperContribution = false;
        currentWaveHasRearguard = WaveHasBlock(waveData, ChaosWaveBlockType.Rearguard);
        currentWaveIsPreBoss = waveData != null && waveData.scenario == WaveScenario.PreBoss;
        currentWaveIsBoss = waveData != null && (waveData.scenario == WaveScenario.Boss || waveData.scenario == WaveScenario.MiniBoss);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        currentWaveDamageMasteryXP = 0;
        damageMasteryXPFraction = 0f;
        currentWaveHadSniperContribution = false;
        currentWaveHasRearguard = false;
        currentWaveIsPreBoss = false;
        currentWaveIsBoss = false;
    }

    public void RecordSniperDamage(Tower tower, float damage)
    {
        if (tower == null || tower.towerRole != TowerRole.Sniper || damage <= 0f)
        {
            return;
        }

        currentWaveHadSniperContribution = true;
        if (currentWaveDamageMasteryXP >= maxDamageMasteryXPPerWave)
        {
            return;
        }

        damageMasteryXPFraction += damage * damageToMasteryXPRatio;
        int xp = Mathf.FloorToInt(damageMasteryXPFraction);
        if (xp <= 0)
        {
            return;
        }

        damageMasteryXPFraction -= xp;
        int cappedXP = Mathf.Min(xp, maxDamageMasteryXPPerWave - currentWaveDamageMasteryXP);
        if (cappedXP <= 0)
        {
            return;
        }

        currentWaveDamageMasteryXP += cappedXP;
        AddSniperMasteryXP(cappedXP);
    }

    public void RecordSniperKill(Tower tower, EnemyRole killedRole)
    {
        if (tower == null || tower.towerRole != TowerRole.Sniper)
        {
            return;
        }

        currentWaveHadSniperContribution = true;
        int xp = 0;
        switch (killedRole)
        {
            case EnemyRole.Mage:
                xp += mageKillBonusXP;
                RegisterSpecialSniperKill();
                break;
            case EnemyRole.Learner:
                xp += learnerKillBonusXP;
                RegisterSpecialSniperKill();
                break;
            case EnemyRole.AllRounder:
                xp += allRounderKillBonusXP;
                RegisterSpecialSniperKill();
                break;
            case EnemyRole.MiniBoss:
                xp += miniBossParticipationBonusXP;
                MarkMasteryThreeObjective(true);
                break;
            case EnemyRole.Boss:
                xp += bossParticipationBonusXP;
                MarkMasteryThreeObjective(true);
                break;
        }

        if (xp > 0)
        {
            AddSniperMasteryXP(xp);
        }
    }

    public void RecordSniperAssist(Tower tower, EnemyRole assistedRole)
    {
        if (tower == null || tower.towerRole != TowerRole.Sniper)
        {
            return;
        }

        currentWaveHadSniperContribution = true;
        int xp = 0;
        if (assistedRole == EnemyRole.Mage || assistedRole == EnemyRole.Learner || assistedRole == EnemyRole.AllRounder)
        {
            xp += 4;
        }

        if (xp > 0)
        {
            AddSniperMasteryXP(xp);
        }
    }

    public void RecordSniperHit(Tower tower, Enemy enemy, float appliedDamage)
    {
        if (tower == null || enemy == null || tower.towerRole != TowerRole.Sniper)
        {
            return;
        }

        currentWaveHadSniperContribution = true;
        if (IsSpecialOrEliteTarget(enemy))
        {
            firstSpecialHitsThisRun.Add(enemy.GetInstanceID());
        }
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
        {
            return;
        }

        Tower sniperContributor = FindSniperContributor(contributors);
        bool sniperKilled = killingTower != null && killingTower.towerRole == TowerRole.Sniper;
        if (sniperContributor == null && !sniperKilled)
        {
            return;
        }

        currentWaveHadSniperContribution = true;
        int xp = 0;
        EnemyRole role = enemy.enemyRole;

        if (!sniperKilled && role == EnemyRole.Mage)
        {
            xp += mageKillBonusXP;
            RegisterSpecialSniperKill();
        }
        else if (!sniperKilled && role == EnemyRole.Learner)
        {
            xp += learnerKillBonusXP;
            RegisterSpecialSniperKill();
        }
        else if (!sniperKilled && role == EnemyRole.AllRounder)
        {
            xp += allRounderKillBonusXP;
            RegisterSpecialSniperKill();
        }

        if (!sniperKilled && IsBossOnly(enemy))
        {
            xp += bossParticipationBonusXP;
            MarkMasteryThreeObjective(true);
        }
        else if (!sniperKilled && IsMiniBossOnly(enemy))
        {
            xp += miniBossParticipationBonusXP;
            MarkMasteryThreeObjective(true);
        }

        if (IsEliteOnly(enemy))
        {
            xp += eliteParticipationBonusXP;
        }

        if (enemy.GetPathProgressPercent() >= 0.7f)
        {
            xp += highProgressKillBonusXP;
        }

        if (enemy.IsChaosVariant())
        {
            xp += chaosVariantKillBonusXP;
        }

        if (xp > 0)
        {
            AddSniperMasteryXP(xp);
        }
    }

    public SniperTowerMasteryShotContext PrepareSniperShot(Tower tower, Enemy currentTarget, int shotCounter)
    {
        var context = new SniperTowerMasteryShotContext
        {
            primaryTarget = currentTarget
        };

        if (tower == null)
        {
            return context;
        }

        SniperTowerRuntimeState state = GetRuntimeState(tower);
        Enemy boundTarget = GetValidBoundTarget(tower, state);
        if (boundTarget != null)
        {
            context.primaryTarget = boundTarget;
        }

        if (IsNodeUnlocked(NodeElitePriority) || IsNodeUnlocked(NodePreBossWatcher))
        {
            Enemy specialTarget = FindSpecialPriorityTarget(tower);
            if (specialTarget != null && ShouldReplaceTargetWithPriority(context.primaryTarget, specialTarget))
            {
                context.primaryTarget = specialTarget;
            }
        }

        if (IsNodeUnlocked(NodeEscapeLine))
        {
            Enemy escapeTarget = FindEscapeLineTarget(tower);
            if (escapeTarget != null && (context.primaryTarget == null || escapeTarget.GetPathProgressPercent() > context.primaryTarget.GetPathProgressPercent() + 0.12f))
            {
                context.primaryTarget = escapeTarget;
            }
        }

        if (ActiveKeystone == SniperTowerKeystone.WatcherLine && state.watcherLineUsedWave != currentWaveNumber)
        {
            Enemy watcherTarget = FindWatcherLineTarget(tower);
            if (watcherTarget != null)
            {
                context.primaryTarget = watcherTarget;
                context.watcherLineShot = true;
                state.watcherLineUsedWave = currentWaveNumber;
            }
        }

        Enemy target = context.primaryTarget;
        if (target == null)
        {
            return context;
        }

        if (IsNodeUnlocked(NodeTargetBinding) && IsBossOrMiniBossOnly(target))
        {
            state.boundTarget = target;
            state.boundUntilTime = Time.time + 2f;
        }

        if (IsNodeUnlocked(NodeMarkingShot) && IsBossOrMiniBossOnly(target) && !target.HasSniperBossMark())
        {
            context.applyBossMark = true;
        }

        if (ActiveKeystone == SniperTowerKeystone.TargetMark && IsBossOrMiniBossOnly(target))
        {
            context.applyBossMark = true;
        }

        if (ActiveKeystone == SniperTowerKeystone.HeadshotProtocol && IsSpecialOrEliteTarget(target) && !target.HasSniperHeadshotMark())
        {
            context.applyHeadshotMark = true;
        }

        if (ActiveKeystone == SniperTowerKeystone.HeadshotProtocol && target.HasSniperHeadshotMark() && target.GetHealthPercent() <= 0.35f)
        {
            context.consumeHeadshotMark = true;
        }

        return context;
    }

    public int CalculateSniperShotDamage(Tower tower, Enemy target, int baseDamage, SniperTowerMasteryShotContext context, float multiplier = 1f)
    {
        if (target == null)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }

        float damage = Mathf.Max(1, baseDamage) * multiplier;
        EnemyRole role = target.enemyRole;

        if (target.GetHealthPercent() > 0.8f)
        {
            damage *= 1f + GetNodeRank(NodeFirstHit) * 0.02f;
        }

        if (role == EnemyRole.Mage)
        {
            damage *= 1f + GetNodeRank(NodeMageReader) * 0.05f;
            if (target.IsChaosVariant())
            {
                damage *= 1f + GetNodeRank(NodeChaosMageAnalysis) * 0.06f;
            }
        }
        else if (role == EnemyRole.Learner)
        {
            damage *= 1f + GetNodeRank(NodeLearnerReader) * 0.05f;
            if (target.IsChaosVariant())
            {
                damage *= 1f + GetNodeRank(NodeChaosLearnerAnalysis) * 0.06f;
            }
        }
        else if (role == EnemyRole.AllRounder)
        {
            damage *= 1f + GetNodeRank(NodeAllRounderAnalysis) * 0.05f;
        }
        else if (IsMiniBossOnly(target))
        {
            damage *= 1f + GetNodeRank(NodeMiniBossSight) * 0.05f;
        }
        else if (IsBossOnly(target))
        {
            damage *= 1f + GetNodeRank(NodeBossSight) * 0.04f;
        }

        if (IsNodeUnlocked(NodeWeakPoint) && IsSpecialOrEliteTarget(target) && !firstSpecialHitsThisRun.Contains(target.GetInstanceID()))
        {
            damage *= 1.2f;
        }

        if (IsNodeUnlocked(NodeHeadshotWindow) && IsSpecialOrEliteTarget(target) && !IsBossOrMiniBossOnly(target) && target.GetHealthPercent() <= 0.25f)
        {
            damage *= 1.25f;
        }

        if (IsNodeUnlocked(NodeMarkPrecision) && target.HasSniperBossMark() && target.GetSniperBossMarkSourceTower() == tower)
        {
            damage *= 1.1f;
        }

        if (ActiveKeystone == SniperTowerKeystone.TargetMark && target.HasSniperBossMark())
        {
            damage *= 1.15f;
        }

        if (IsNodeUnlocked(NodeRiftBossOptics) && IsBossOrMiniBossOnly(target) && GetCurrentChaosLevel() >= 3)
        {
            damage *= 1.05f;
        }

        if (IsNodeUnlocked(NodeInterceptShot) && target.GetPathProgressPercent() >= 0.7f)
        {
            damage *= 1.1f;
        }

        if (IsNodeUnlocked(NodeRearguardAnalysis) && (currentWaveHasRearguard || currentWaveIsPreBoss) && IsHardOrSpecialTarget(target))
        {
            damage *= 1.08f;
        }

        if (target.IsChaosVariant() && target.GetPathProgressPercent() >= 0.7f)
        {
            damage *= 1f + GetNodeRank(NodePurpleWatch) * 0.06f;
        }

        if (context != null && context.watcherLineShot)
        {
            damage *= watcherLineDamageMultiplier;
        }

        if (context != null && context.consumeHeadshotMark)
        {
            damage *= IsBossOrMiniBossOnly(target) ? reducedHeadshotFinisherMultiplier : headshotFinisherMultiplier;
        }

        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    public void FillSniperProjectileData(Projectile projectile, SniperTowerMasteryShotContext context)
    {
        if (projectile == null || context == null)
        {
            return;
        }

        projectile.applySniperHeadshotMark = context.applyHeadshotMark;
        projectile.consumeSniperHeadshotMark = context.consumeHeadshotMark;
        projectile.sniperHeadshotMarkDuration = headshotMarkDuration;
        projectile.applySniperBossMark = context.applyBossMark;
        projectile.sniperBossMarkDuration = bossMarkDuration + GetNodeRank(NodeLongBreath);
    }

    public float GetSniperDamageBaseBonus()
    {
        return GetNodeRank(NodeCalibratedBarrel);
    }

    public float GetSniperRangeBonus()
    {
        return GetNodeRank(NodeLongScope) * 0.15f + GetNodeRank(NodeWatchtowerOptics) * 0.2f;
    }

    public float GetSniperFireRateAdditive()
    {
        return GetNodeRank(NodeCalmBreath) * 0.02f;
    }

    public float GetSniperFireRateMultiplier(Tower tower)
    {
        return 1f;
    }

    public float GetSniperXPMultiplier()
    {
        return 1f + GetNodeRank(NodeFocusTraining) * 0.03f;
    }

    public float GetPreciseRedirectReadyBonus()
    {
        return IsNodeUnlocked(NodePreciseRedirect) ? 0.1f : 0f;
    }

    public int GetUnspentPoints()
    {
        TowerMasteryRoleProfile profile = GetSniperProfile();
        return profile != null ? profile.unspentPoints : 0;
    }

    public int GetSpentPoints()
    {
        TowerMasteryRoleProfile profile = GetSniperProfile();
        if (profile != null)
        {
            return profile.spentPoints;
        }

        int spent = 0;
        foreach (var kvp in nodeStatesById)
        {
            if (!definitionsById.TryGetValue(kvp.Key, out SniperTowerMasteryNodeDefinition definition))
            {
                continue;
            }

            int rank = Mathf.Clamp(kvp.Value.rank, 0, definition.maxRank);
            for (int i = 0; i < rank; i++)
            {
                spent += definition.GetCostForNextRank(i);
            }
        }

        return spent;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !nodeStatesById.TryGetValue(nodeId, out SniperTowerMasteryNodeState state))
        {
            return 0;
        }

        return Mathf.Max(0, state.rank);
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        return GetNodeRank(nodeId) > 0;
    }

    public IEnumerable<SniperTowerMasteryNodeDefinition> GetDefinitions()
    {
        return definitionsById.Values;
    }

    public SniperTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        definitionsById.TryGetValue(nodeId, out SniperTowerMasteryNodeDefinition definition);
        return definition;
    }

    public bool CanPurchaseNode(string nodeId)
    {
        return CanPurchaseNode(nodeId, out _);
    }

    public bool CanPurchaseNode(string nodeId, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(nodeId) || !definitionsById.TryGetValue(nodeId, out SniperTowerMasteryNodeDefinition definition))
        {
            reason = "Unbekannter Sniper-Knoten.";
            return false;
        }

        int currentRank = GetNodeRank(nodeId);
        if (currentRank >= definition.maxRank)
        {
            reason = "Knoten ist bereits voll ausgebaut.";
            return false;
        }

        if (!CanEditMetaProgression())
        {
            reason = "Meta-Kaeufe sind im Run nur read-only.";
            return false;
        }

        if (!IsMilestoneUnlocked(definition.gate))
        {
            reason = $"{GetMilestoneName(definition.gate)} ist noch gesperrt.";
            return false;
        }

        int nextCost = definition.GetCostForNextRank(currentRank);
        if (GetUnspentPoints() < nextCost)
        {
            reason = $"Nicht genug Sniper Points ({GetUnspentPoints()} / {nextCost}).";
            return false;
        }

        reason = "Bereit zum Kaufen.";
        return true;
    }

    public bool PurchaseNode(string nodeId, out string reason)
    {
        if (!CanPurchaseNode(nodeId, out reason))
        {
            return false;
        }

        int currentRank = GetNodeRank(nodeId);
        int cost = definitionsById[nodeId].GetCostForNextRank(currentRank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Sniper, cost))
        {
            reason = "Sniper Points konnten nicht ausgegeben werden.";
            return false;
        }

        if (!nodeStatesById.TryGetValue(nodeId, out SniperTowerMasteryNodeState state))
        {
            state = new SniperTowerMasteryNodeState { nodeId = nodeId, rank = 0 };
            nodeStatesById[nodeId] = state;
        }

        state.rank += 1;
        if (definitionsById[nodeId].IsKeystone && GetActiveKeystone() == SniperTowerKeystone.None)
        {
            ActivateKeystone(definitionsById[nodeId].keystone, out _);
        }

        SavePersistentState();
        reason = $"{definitionsById[nodeId].displayName} gekauft.";
        return true;
    }

    public bool TryPurchaseNode(string nodeId)
    {
        return PurchaseNode(nodeId, out _);
    }

    public bool CanActivateKeystone(SniperTowerKeystone keystone)
    {
        if (keystone == SniperTowerKeystone.None || !CanEditMetaProgression())
        {
            return false;
        }

        SniperTowerMasteryNodeDefinition definition = definitionsById.Values.FirstOrDefault(node => node.keystone == keystone);
        return definition != null && IsNodeUnlocked(definition.nodeId);
    }

    public bool ActivateKeystone(SniperTowerKeystone keystone, out string reason)
    {
        reason = string.Empty;
        if (keystone == SniperTowerKeystone.None)
        {
            TowerMasteryManager towerMastery = GetTowerMasteryManager();
            if (towerMastery != null && !towerMastery.TrySetActiveKeystone(TowerRole.Sniper, ""))
            {
                reason = "Keystone kann im laufenden Run nicht gewechselt werden.";
                return false;
            }

            activeKeystone = SniperTowerKeystone.None;
            SavePersistentState();
            reason = "Sniper Keystone deaktiviert.";
            return true;
        }

        SniperTowerMasteryNodeDefinition definition = definitionsById.Values.FirstOrDefault(node => node.keystone == keystone);
        if (definition == null)
        {
            reason = "Unbekannter Sniper Keystone.";
            return false;
        }

        if (!IsNodeUnlocked(definition.nodeId))
        {
            reason = "Dieser Sniper Keystone ist noch nicht freigeschaltet.";
            return false;
        }

        TowerMasteryManager manager = GetTowerMasteryManager();
        if (manager == null || !manager.TrySetActiveKeystone(TowerRole.Sniper, keystone.ToString()))
        {
            reason = "Keystone kann im laufenden Run nicht gewechselt werden.";
            return false;
        }

        activeKeystone = keystone;
        SavePersistentState();
        reason = $"{definition.displayName} aktiviert.";
        return true;
    }

    public bool TryActivateKeystone(SniperTowerKeystone keystone)
    {
        return ActivateKeystone(keystone, out _);
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetSniperProfile();
        int xp = profile != null ? profile.masteryXP : 0;
        int unspent = profile != null ? profile.unspentPoints : 0;
        int spent = profile != null ? profile.spentPoints : 0;
        int runBest = towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Sniper) : 1;
        int bestEver = profile != null ? profile.bestLevelEver : 1;
        return $"Sniper Mastery XP: {xp}\nPunkte: {unspent} frei | {spent} ausgegeben\nBester Sniper im Run/Ewig: {runBest} / {bestEver}\nAktiver Keystone: {GetKeystoneDisplayName(GetActiveKeystone())}\nSpezial-Kills fuer Sniper III: {Mathf.Min(totalSpecialSniperKills, Mathf.Max(1, specialKillsForMasteryGate))} / {Mathf.Max(1, specialKillsForMasteryGate)}";
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetSniperProfile();

        if (profile == null)
        {
            return "Sniper Mastery: vorbereitet";
        }

        return "Sniper Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !definitionsById.TryGetValue(nodeId, out SniperTowerMasteryNodeDefinition definition))
        {
            return "Waehle einen Sniper-Knoten aus.";
        }

        int rank = GetNodeRank(nodeId);
        int nextCost = rank < definition.maxRank ? definition.GetCostForNextRank(rank) : 0;
        string status = rank >= definition.maxRank ? "voll ausgebaut" : $"naechster Rang kostet {nextCost}";
        return $"{definition.displayName}\n{definition.description}\n\nPfad: {GetPathDisplayName(definition.path)}\nRang: {rank}/{definition.maxRank}\nGate: {GetMilestoneName(definition.gate)}\nStatus: {status}";
    }

    public string GetNodeStateText(SniperTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
        {
            return "";
        }

        int rank = GetNodeRank(definition.nodeId);
        if (rank >= definition.maxRank)
        {
            if (definition.keystone != SniperTowerKeystone.None && GetActiveKeystone() == definition.keystone)
            {
                return "Aktiv";
            }

            return "Freigeschaltet";
        }

        if (!CanEditMetaProgression())
        {
            return "Read-only im Run";
        }

        if (!IsMilestoneUnlocked(definition.gate))
        {
            return "Gesperrt: " + GetMilestoneName(definition.gate);
        }

        int cost = definition.GetCostForNextRank(rank);
        int unspent = GetUnspentPoints();
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public IEnumerable<SniperTowerMasteryNodeDefinition> GetDefinitionsByPath(SniperTowerMasteryPath path)
    {
        return definitionsById.Values.Where(definition => definition.path == path);
    }

    private void BuildDefinitions()
    {
        definitionsById.Clear();

        AddDefinition(NodeCalibratedBarrel, "Kalibrierter Lauf", "+1 Sniper Damage pro Rang.", SniperTowerMasteryPath.Trunk, 5, Costs(1, 2, 3, 4, 5), TowerMasteryMilestone.None);
        AddDefinition(NodeCalmBreath, "Ruhiger Atem", "+0,02 Fire Rate pro Rang.", SniperTowerMasteryPath.Trunk, 5, Costs(1, 2, 3, 4, 5), TowerMasteryMilestone.None);
        AddDefinition(NodeLongScope, "Langes Visier", "+0,15 Range pro Rang.", SniperTowerMasteryPath.Trunk, 3, Costs(1, 2, 3), TowerMasteryMilestone.None);
        AddDefinition(NodeFocusTraining, "Fokusausbildung", "Sniper Tower erhalten +3% Tower-XP pro Rang.", SniperTowerMasteryPath.Trunk, 3, Costs(1, 2, 3), TowerMasteryMilestone.None);
        AddDefinition(NodeFirstHit, "Erster Treffer", "+2% Damage gegen Ziele ueber 80% HP pro Rang.", SniperTowerMasteryPath.Trunk, 3, Costs(1, 2, 3), TowerMasteryMilestone.None);

        AddDefinition(NodeMageReader, "Mage-Leser", "+5% Damage gegen Mages pro Rang.", SniperTowerMasteryPath.EliteHunter, 5, Costs(1, 2, 3, 4, 5), TowerMasteryMilestone.I);
        AddDefinition(NodeLearnerReader, "Learner-Leser", "+5% direkter Damage gegen Learner pro Rang.", SniperTowerMasteryPath.EliteHunter, 5, Costs(1, 2, 3, 4, 5), TowerMasteryMilestone.I);
        AddDefinition(NodeElitePriority, "Elite-Prioritaet", "Sniper bevorzugt Elite-/Spezialziele zuverlaessiger.", SniperTowerMasteryPath.EliteHunter, 1, Costs(5), TowerMasteryMilestone.II);
        AddDefinition(NodeWeakPoint, "Schwacher Punkt", "Erster Sniper-Treffer gegen Mage, Learner, AllRounder oder Elite verursacht +20% Damage.", SniperTowerMasteryPath.EliteHunter, 1, Costs(6), TowerMasteryMilestone.II);
        AddDefinition(NodeHeadshotWindow, "Kopfschussfenster", "Gegen Nicht-Boss-Spezialziele unter 25% HP verursacht Sniper +25% Damage.", SniperTowerMasteryPath.EliteHunter, 1, Costs(8), TowerMasteryMilestone.III);
        AddDefinition(NodeAllRounderAnalysis, "AllRounder-Analyse", "+5% Damage gegen AllRounder pro Rang.", SniperTowerMasteryPath.EliteHunter, 2, Costs(6, 8), TowerMasteryMilestone.III);
        AddDefinition(NodeChaosMageAnalysis, "Chaos-Mage-Analyse", "+6% Damage gegen Chaos-Mages pro Rang.", SniperTowerMasteryPath.EliteHunter, 2, Costs(7, 9), TowerMasteryMilestone.IV);
        AddDefinition(NodeChaosLearnerAnalysis, "Chaos-Learner-Analyse", "+6% direkter Damage gegen Chaos-Learner pro Rang.", SniperTowerMasteryPath.EliteHunter, 2, Costs(7, 9), TowerMasteryMilestone.IV);
        AddDefinition(NodeHeadshotProtocol, "Keystone: Kopfschussprotokoll", "Sniper markiert Spezial-/Elite-Ziele und kann sie bei niedriger HP mit einem Finisher treffen. Boss/MiniBoss stark reduziert.", SniperTowerMasteryPath.EliteHunter, 1, Costs(24), TowerMasteryMilestone.V, SniperTowerKeystone.HeadshotProtocol);

        AddDefinition(NodeMiniBossSight, "MiniBoss-Visier", "+5% Damage gegen MiniBoss pro Rang.", SniperTowerMasteryPath.BossFocus, 5, Costs(1, 2, 3, 4, 5), TowerMasteryMilestone.I);
        AddDefinition(NodeBossSight, "Boss-Visier", "+4% Damage gegen Boss pro Rang.", SniperTowerMasteryPath.BossFocus, 5, Costs(1, 2, 3, 4, 5), TowerMasteryMilestone.I);
        AddDefinition(NodeTargetBinding, "Zielbindung", "Wenn Sniper auf Boss/MiniBoss feuert, bleibt er kurz auf diesem Ziel, falls moeglich.", SniperTowerMasteryPath.BossFocus, 1, Costs(5), TowerMasteryMilestone.II);
        AddDefinition(NodeMarkingShot, "Markierungsschuss", "Erster Treffer gegen Boss/MiniBoss setzt eine Sniper-Markierung.", SniperTowerMasteryPath.BossFocus, 1, Costs(6), TowerMasteryMilestone.II);
        AddDefinition(NodeMarkPrecision, "Markenpraezision", "Sniper verursacht gegen eigene markierte Boss/MiniBoss-Ziele +10% Damage.", SniperTowerMasteryPath.BossFocus, 1, Costs(8), TowerMasteryMilestone.III);
        AddDefinition(NodeLongBreath, "Langer Atem", "Sniper-Markierungen halten +1s pro Rang.", SniperTowerMasteryPath.BossFocus, 2, Costs(5, 7), TowerMasteryMilestone.III);
        AddDefinition(NodeRiftBossOptics, "Rissboss-Optik", "Gegen Boss/MiniBoss bei Chaos-Level 3+ erhaelt Sniper +5% Damage.", SniperTowerMasteryPath.BossFocus, 1, Costs(10), TowerMasteryMilestone.IV);
        AddDefinition(NodePreBossWatcher, "Vor-Boss-Waechter", "In PreBoss-/Boss-Waves priorisiert Sniper harte Spezialziele besser.", SniperTowerMasteryPath.BossFocus, 1, Costs(8), TowerMasteryMilestone.IV);
        AddDefinition(NodeTargetMark, "Keystone: Zielmarkierung", "Sniper setzt auf Boss/MiniBoss eine starke Zielmarkierung. Sniper Tower verursachen gegen dieses Ziel +15% Damage.", SniperTowerMasteryPath.BossFocus, 1, Costs(24), TowerMasteryMilestone.V, SniperTowerKeystone.TargetMark);

        AddDefinition(NodeWatchtowerOptics, "Wachturmoptik", "+0,20 Range pro Rang.", SniperTowerMasteryPath.LongWatch, 5, Costs(1, 2, 3, 4, 5), TowerMasteryMilestone.I);
        AddDefinition(NodeEscapeLine, "Fluchtlinie erkennen", "Sniper priorisiert Gegner mit hohem Wegfortschritt zuverlaessiger.", SniperTowerMasteryPath.LongWatch, 1, Costs(5), TowerMasteryMilestone.II);
        AddDefinition(NodeInterceptShot, "Abfangschuss", "Gegen Gegner ueber 70% Wegfortschritt: +10% Damage.", SniperTowerMasteryPath.LongWatch, 1, Costs(6), TowerMasteryMilestone.II);
        AddDefinition(NodeWatchRoutine, "Ueberwachungsroutine", "Bereitet spaetere Zielwechsel-Optimierung vor.", SniperTowerMasteryPath.LongWatch, 1, Costs(8), TowerMasteryMilestone.III);
        AddDefinition(NodeRearguardAnalysis, "Nachhut-Analyse", "In Rearguard-/PreBoss-Waves: +8% Damage gegen spaete harte Ziele.", SniperTowerMasteryPath.LongWatch, 1, Costs(8), TowerMasteryMilestone.III);
        AddDefinition(NodePreciseRedirect, "Praezise Umleitung", "Wenn ein Ziel stirbt, wird der naechste Schuss etwas schneller bereit.", SniperTowerMasteryPath.LongWatch, 1, Costs(8), TowerMasteryMilestone.III);
        AddDefinition(NodePurpleWatch, "Violette Wache", "+6% Damage gegen Chaos-Varianten mit hohem Wegfortschritt pro Rang.", SniperTowerMasteryPath.LongWatch, 2, Costs(7, 9), TowerMasteryMilestone.IV);
        AddDefinition(NodePreviewTraining, "Preview-Schulung", "Bereitet bessere Prioritaetslogik fuer spaetere Hidden-Preview-Waves vor.", SniperTowerMasteryPath.LongWatch, 1, Costs(8), TowerMasteryMilestone.IV);
        AddDefinition(NodeWatcherLine, "Keystone: Waechterlinie", "Einmal pro Wave feuert Sniper einen Prioritaets-Abfangschuss auf ein gefaehrliches Ziel mit hohem Wegfortschritt.", SniperTowerMasteryPath.LongWatch, 1, Costs(22), TowerMasteryMilestone.V, SniperTowerKeystone.WatcherLine);
    }

    private void AddDefinition(string nodeId, string displayName, string description, SniperTowerMasteryPath path, int maxRank, int[] costs, TowerMasteryMilestone gate, SniperTowerKeystone keystone = SniperTowerKeystone.None)
    {
        definitionsById[nodeId] = new SniperTowerMasteryNodeDefinition
        {
            nodeId = nodeId,
            displayName = displayName,
            description = description,
            path = path,
            maxRank = maxRank,
            costs = costs,
            gate = gate,
            keystone = keystone
        };
    }

    private int[] Costs(params int[] costs)
    {
        return costs;
    }

    private SniperTowerRuntimeState GetRuntimeState(Tower tower)
    {
        int id = tower.GetInstanceID();
        if (!runtimeStateByTowerId.TryGetValue(id, out SniperTowerRuntimeState state))
        {
            state = new SniperTowerRuntimeState();
            runtimeStateByTowerId[id] = state;
        }

        return state;
    }

    private Enemy GetValidBoundTarget(Tower tower, SniperTowerRuntimeState state)
    {
        if (state == null || state.boundTarget == null || Time.time > state.boundUntilTime)
        {
            return null;
        }

        Enemy target = state.boundTarget;
        if (target.currentHealth <= 0 || Vector3.Distance(tower.transform.position, target.transform.position) > tower.GetEffectiveRange())
        {
            state.boundTarget = null;
            return null;
        }

        return target;
    }

    private Enemy FindSpecialPriorityTarget(Tower tower)
    {
        if (tower == null)
        {
            return null;
        }

        Enemy best = null;
        float bestScore = float.MinValue;
        float range = tower.GetEffectiveRange();
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.currentHealth <= 0)
            {
                continue;
            }

            float distance = Vector3.Distance(tower.transform.position, enemy.transform.position);
            if (distance > range)
            {
                continue;
            }

            float score = GetPriorityScore(enemy);
            if (currentWaveIsPreBoss && IsHardOrSpecialTarget(enemy))
            {
                score += 0.75f;
            }

            if (score > bestScore)
            {
                best = enemy;
                bestScore = score;
            }
        }

        return bestScore > 0f ? best : null;
    }

    private Enemy FindWatcherLineTarget(Tower tower)
    {
        if (tower == null)
        {
            return null;
        }

        Enemy best = null;
        float bestScore = float.MinValue;
        float range = tower.GetEffectiveRange() * watcherLineRangeMultiplier;
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.currentHealth <= 0)
            {
                continue;
            }

            float progress = enemy.GetPathProgressPercent();
            if (progress < watcherLineMinimumProgress)
            {
                continue;
            }

            float distance = Vector3.Distance(tower.transform.position, enemy.transform.position);
            if (distance > range)
            {
                continue;
            }

            float score = progress * 2f + GetPriorityScore(enemy);
            if (score > bestScore)
            {
                best = enemy;
                bestScore = score;
            }
        }

        return best;
    }

    private Enemy FindEscapeLineTarget(Tower tower)
    {
        if (tower == null)
        {
            return null;
        }

        Enemy best = null;
        float bestProgress = 0f;
        float range = tower.GetEffectiveRange();
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.currentHealth <= 0)
            {
                continue;
            }

            float progress = enemy.GetPathProgressPercent();
            if (progress < 0.6f)
            {
                continue;
            }

            float distance = Vector3.Distance(tower.transform.position, enemy.transform.position);
            if (distance > range)
            {
                continue;
            }

            if (progress > bestProgress)
            {
                best = enemy;
                bestProgress = progress;
            }
        }

        return best;
    }

    private bool ShouldReplaceTargetWithPriority(Enemy currentTarget, Enemy priorityTarget)
    {
        if (priorityTarget == null)
        {
            return false;
        }

        if (currentTarget == null)
        {
            return true;
        }

        return GetPriorityScore(priorityTarget) > GetPriorityScore(currentTarget) + 0.25f;
    }

    private float GetPriorityScore(Enemy enemy)
    {
        if (enemy == null)
        {
            return 0f;
        }

        float score = enemy.GetPathProgressPercent();
        EnemyRole role = enemy.enemyRole;
        if (role == EnemyRole.Mage || role == EnemyRole.Learner)
        {
            score += 3f;
        }
        else if (role == EnemyRole.AllRounder)
        {
            score += 2.25f;
        }
        else if (IsEliteOnly(enemy))
        {
            score += 2.5f;
        }
        else if (IsBossOrMiniBossTarget(enemy))
        {
            score += 2f;
        }

        if (enemy.IsChaosVariant())
        {
            score += 0.5f;
        }

        return score;
    }

    private Tower FindSniperContributor(IEnumerable<Tower> contributors)
    {
        if (contributors == null)
        {
            return null;
        }

        foreach (Tower tower in contributors)
        {
            if (tower != null && tower.towerRole == TowerRole.Sniper)
            {
                return tower;
            }
        }

        return null;
    }

    private bool IsSpecialOrEliteTarget(Enemy enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        EnemyRole role = enemy.enemyRole;
        return role == EnemyRole.Mage || role == EnemyRole.Learner || role == EnemyRole.AllRounder || IsEliteOnly(enemy);
    }

    private bool IsHardOrSpecialTarget(Enemy enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        EnemyRole role = enemy.enemyRole;
        return IsSpecialOrEliteTarget(enemy) || IsBossOrMiniBossTarget(enemy) || role == EnemyRole.Tank || role == EnemyRole.Knight;
    }

    private bool IsBossOrMiniBossTarget(Enemy enemy)
    {
        return enemy != null && enemy.IsBossOrMiniBossTarget();
    }

    private bool IsBossOrMiniBossOnly(Enemy enemy)
    {
        return IsBossOnly(enemy) || IsMiniBossOnly(enemy);
    }

    private bool IsBossOnly(Enemy enemy)
    {
        return enemy != null && (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss);
    }

    private bool IsMiniBossOnly(Enemy enemy)
    {
        return enemy != null && (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss);
    }

    private bool IsEliteOnly(Enemy enemy)
    {
        return enemy != null && (enemy.isElite || enemy.enemyRole == EnemyRole.Elite);
    }

    private bool WaveHasBlock(WaveData waveData, ChaosWaveBlockType blockType)
    {
        if (waveData == null || waveData.chaosWaveBlocks == null)
        {
            return false;
        }

        foreach (ChaosWaveBlock block in waveData.chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
            {
                continue;
            }

            if (block.blockType == blockType)
            {
                return true;
            }
        }

        return false;
    }

    private void RegisterSpecialSniperKill()
    {
        totalSpecialSniperKills += 1;
        if (totalSpecialSniperKills >= specialKillsForMasteryGate)
        {
            MarkMasteryThreeObjective(false);
        }

        SavePersistentState();
    }

    private void MarkMasteryThreeObjective(bool forceByBossParticipation)
    {
        TowerMasteryRoleProfile profile = GetSniperProfile();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (profile == null || towerMastery == null)
        {
            return;
        }

        if (!forceByBossParticipation && totalSpecialSniperKills < Mathf.Max(1, specialKillsForMasteryGate))
        {
            return;
        }

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Sniper);
    }

    private void AddSniperMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
        {
            towerMastery.AddRoleMasteryXP(TowerRole.Sniper, amount);
        }
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        if (milestone == TowerMasteryMilestone.None)
        {
            return true;
        }

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Sniper, milestone);
    }

    private string GetMilestoneName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I:
                return "Sniper I: Vertrautheit";
            case TowerMasteryMilestone.II:
                return "Sniper II: Visierdisziplin";
            case TowerMasteryMilestone.III:
                return "Sniper III: Eliminierung";
            case TowerMasteryMilestone.IV:
                return "Sniper IV: Risssicht";
            case TowerMasteryMilestone.V:
                return "Sniper V: Adlerkern";
            default:
                return "Linearer Einstieg";
        }
    }

    public string GetPathDisplayName(SniperTowerMasteryPath path)
    {
        switch (path)
        {
            case SniperTowerMasteryPath.EliteHunter:
                return "Elite-Jaeger";
            case SniperTowerMasteryPath.BossFocus:
                return "Boss-Fokus";
            case SniperTowerMasteryPath.LongWatch:
                return "Fernwache";
            default:
                return "Linearer Einstieg";
        }
    }

    public string GetKeystoneDisplayName(SniperTowerKeystone keystone)
    {
        switch (keystone)
        {
            case SniperTowerKeystone.HeadshotProtocol:
                return "Kopfschussprotokoll";
            case SniperTowerKeystone.TargetMark:
                return "Zielmarkierung";
            case SniperTowerKeystone.WatcherLine:
                return "Waechterlinie";
            default:
                return "Keiner";
        }
    }

    public SniperTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetSniperProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
        {
            return activeKeystone;
        }

        try
        {
            return (SniperTowerKeystone)Enum.Parse(typeof(SniperTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return activeKeystone;
        }
    }

    private int GetCurrentChaosLevel()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (gameManager == null)
        {
            return 0;
        }

        WaveCompletionResult result = gameManager.GetCurrentWaveResult();
        if (result != null)
        {
            return result.chaosLevelAtWaveStart;
        }

        ChaosJusticeManager chaosJusticeManager = gameManager.GetChaosJusticeManager();
        return chaosJusticeManager != null ? chaosJusticeManager.GetChaosLevel() : 0;
    }

    private TowerMasteryRoleProfile GetSniperProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Sniper) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private void LoadPersistentState()
    {
        nodeStatesById.Clear();
        foreach (string nodeId in definitionsById.Keys)
        {
            int rank = PlayerPrefs.GetInt(GetNodePrefKey(nodeId), 0);
            if (rank > 0)
            {
                nodeStatesById[nodeId] = new SniperTowerMasteryNodeState { nodeId = nodeId, rank = rank };
            }
        }

        activeKeystone = (SniperTowerKeystone)PlayerPrefs.GetInt("TD_SniperMastery_ActiveKeystone", 0);
        TowerMasteryRoleProfile profile = GetSniperProfile();
        if (profile != null && !string.IsNullOrEmpty(profile.activeKeystoneId))
        {
            try
            {
                activeKeystone = (SniperTowerKeystone)Enum.Parse(typeof(SniperTowerKeystone), profile.activeKeystoneId);
            }
            catch
            {
                activeKeystone = SniperTowerKeystone.None;
            }
        }

        totalSpecialSniperKills = PlayerPrefs.GetInt("TD_SniperMastery_TotalSpecialKills", 0);

        if (activeKeystone != SniperTowerKeystone.None)
        {
            SniperTowerMasteryNodeDefinition definition = definitionsById.Values.FirstOrDefault(node => node.keystone == activeKeystone);
            if (definition == null || !IsNodeUnlocked(definition.nodeId))
            {
                activeKeystone = SniperTowerKeystone.None;
            }
        }
    }

    private void SavePersistentState()
    {
        foreach (string nodeId in definitionsById.Keys)
        {
            PlayerPrefs.SetInt(GetNodePrefKey(nodeId), GetNodeRank(nodeId));
        }

        PlayerPrefs.SetInt("TD_SniperMastery_ActiveKeystone", (int)activeKeystone);
        PlayerPrefs.SetInt("TD_SniperMastery_TotalSpecialKills", totalSpecialSniperKills);
        PlayerPrefs.Save();
    }

    private string GetNodePrefKey(string nodeId)
    {
        return "TD_SniperMastery_Node_" + nodeId;
    }
}
