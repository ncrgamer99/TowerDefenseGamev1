using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum SlowTowerMasteryPath
{
    Trunk,
    TimeAnchor,
    SupportTiming,
    EmergencyBrake
}

public enum SlowTowerKeystone
{
    None,
    StasisField,
    ControlWindow,
    LastLine
}

[System.Serializable]
public class SlowTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class SlowTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public SlowTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public SlowTowerKeystone keystone;
    public string requiredNodeId;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public SlowTowerMasteryNodeDefinition(string nodeId, string displayName, SlowTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, int[] rankCosts, string effectText, SlowTowerKeystone keystone = SlowTowerKeystone.None, string requiredNodeId = "")
    {
        this.nodeId = nodeId;
        this.displayName = displayName;
        this.path = path;
        this.gate = gate;
        this.maxRank = Mathf.Max(1, maxRank);
        this.rankCosts = rankCosts ?? new int[] { 1 };
        this.effectText = string.IsNullOrEmpty(effectText) ? "" : effectText;
        this.keystone = keystone;
        this.requiredNodeId = string.IsNullOrEmpty(requiredNodeId) ? "" : requiredNodeId;
    }

    public int GetCostForNextRank(int currentRank)
    {
        return TowerMasteryManager.GetMasteryNodeCostForNextRank(currentRank, maxRank, rankCosts);
    }
}

public struct SlowTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public bool controlPulse;
    public bool stasisField;
    public bool lastLine;
}

public class SlowTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_SlowMastery_";

    public const string ColdCoil = "cold_coil";
    public const string LongerInhibition = "longer_inhibition";
    public const string ControlLine = "control_line";
    public const string CalmCadence = "calm_cadence";
    public const string ControlRoutine = "control_routine";

    public const string DeeperCold = "deeper_cold";
    public const string TimeStretch = "time_stretch";
    public const string StableFollowup = "stable_followup";
    public const string ControlPulse = "control_pulse";
    public const string DenseRead = "dense_read";
    public const string MiniBossInhibition = "miniboss_inhibition";
    public const string DensityAnchor = "density_anchor";
    public const string ResistanceAnalysis = "resistance_analysis";
    public const string StasisField = "stasis_field";

    public const string OpenTarget = "open_target";
    public const string CoordinatedFire = "coordinated_fire";
    public const string SetWindow = "set_window";
    public const string WindowTraining = "window_training";
    public const string DotTimeGain = "dot_time_gain";
    public const string GuideHeavyTargets = "guide_heavy_targets";
    public const string ChaosCoordination = "chaos_coordination";
    public const string RiftSupport = "rift_support";
    public const string ControlWindow = "control_window";

    public const string RunnerBrake = "runner_brake";
    public const string EscapeLine = "escape_line";
    public const string LateCold = "late_cold";
    public const string LeakGuard = "leak_guard";
    public const string RearguardFocus = "rearguard_focus";
    public const string FasterReaction = "faster_reaction";
    public const string ChaosRunnerBrake = "chaos_runner_brake";
    public const string BaseCalm = "base_calm";
    public const string LastLine = "last_line";

    public static SlowTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<SlowTowerMasteryNodeState> nodeStates = new List<SlowTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public int firstSlowApplicationXP = 1;
    public int maxSlowApplicationXPPerWave = 30;
    public int slowedDefeatAssistXP = 4;
    public int runnerSlowBonusXP = 3;
    public int nearBaseSlowBonusXP = 3;
    public int miniBossSlowBonusXP = 7;
    public int bossSlowBonusXP = 10;
    public int chaosSlowBonusXP = 2;
    public int maxChaosSlowBonusXPPerWave = 18;
    public int densityRearguardChaosWaveBonusXP = 16;
    public int maxCoordinatedSupportXPPerWave = 18;

    [Header("Milestone Tracking")]
    public int slowAssistsForMasteryGate = 250;
    public int totalSlowAssists = 0;

    [Header("Runtime")]
    public float stasisFieldCooldown = 8f;
    public float stasisFieldRadius = 0.95f;
    public float controlWindowBaseDuration = 2f;
    public float keystoneControlWindowDuration = 3f;

    private class SlowTowerRuntimeState
    {
        public int leakGuardUsedWave = -1;
        public int lastLineUsedWave = -1;
        public float reactionUntilTime = -1f;
        public float stasisCooldownUntilTime = -1f;
    }

    private readonly List<SlowTowerMasteryNodeDefinition> definitions = new List<SlowTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, SlowTowerMasteryNodeDefinition> definitionById = new Dictionary<string, SlowTowerMasteryNodeDefinition>();
    private readonly Dictionary<int, SlowTowerRuntimeState> runtimeStateByTowerId = new Dictionary<int, SlowTowerRuntimeState>();
    private readonly HashSet<int> slowedEnemiesThisWave = new HashSet<int>();
    private readonly HashSet<int> bossSlowBonusEnemiesThisWave = new HashSet<int>();
    private readonly HashSet<int> controlWindowAppliedEnemiesThisRun = new HashSet<int>();

    private int currentWaveNumber = 0;
    private bool currentWaveHadSlowContribution = false;
    private bool currentWaveHasDensity = false;
    private bool currentWaveHasRearguard = false;
    private bool currentWaveHasToughness = false;
    private bool currentWaveHasResistance = false;
    private bool currentWaveHasChaosVariantGroup = false;
    private bool currentWaveHasRunnerPressure = false;
    private int currentWaveSlowApplicationXP = 0;
    private int currentWaveChaosSlowBonusXP = 0;
    private int currentWaveCoordinatedSupportXP = 0;
    private int baseCalmBonusWaveNumber = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
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

        if (Instance == this)
            Instance = null;
    }

    public static SlowTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        SlowTowerMasteryManager existing = FindObjectOfType<SlowTowerMasteryManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("SlowTowerMasterySystem");
        SlowTowerMasteryManager manager = systemObject.AddComponent<SlowTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public static bool TryGetActive(out SlowTowerMasteryManager manager)
    {
        manager = Instance != null ? Instance : FindObjectOfType<SlowTowerMasteryManager>();
        return manager != null;
    }

    public IReadOnlyList<SlowTowerMasteryNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public SlowTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();
        return !string.IsNullOrEmpty(nodeId) && definitionById.TryGetValue(nodeId, out SlowTowerMasteryNodeDefinition definition) ? definition : null;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        EnsureNodeStates();

        foreach (SlowTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Slow, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        SlowTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return false;

        if (!CanEditMetaProgression())
            return false;

        int rank = GetNodeRank(nodeId);

        if (rank >= definition.maxRank)
            return false;

        if (!IsMilestoneUnlocked(definition.gate))
            return false;

        if (!string.IsNullOrEmpty(definition.requiredNodeId) && GetNodeRank(definition.requiredNodeId) <= 0)
            return false;

        TowerMasteryRoleProfile profile = GetSlowProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        SlowTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Slow, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != SlowTowerKeystone.None && GetActiveKeystone() == SlowTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(SlowTowerKeystone keystone)
    {
        if (keystone == SlowTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(SlowTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Slow, keystone.ToString());
    }

    public SlowTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetSlowProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return SlowTowerKeystone.None;

        try
        {
            return (SlowTowerKeystone)System.Enum.Parse(typeof(SlowTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return SlowTowerKeystone.None;
        }
    }

    public string GetNodeStateText(SlowTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != SlowTowerKeystone.None && GetActiveKeystone() == definition.keystone)
                return "Aktiv";

            return "Freigeschaltet";
        }

        if (!CanEditMetaProgression())
            return "Read-only im Run";

        if (!IsMilestoneUnlocked(definition.gate))
            return "Gesperrt: " + GetMilestoneDisplayName(definition.gate);

        if (!string.IsNullOrEmpty(definition.requiredNodeId) && GetNodeRank(definition.requiredNodeId) <= 0)
            return "Voraussetzung fehlt";

        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryRoleProfile profile = GetSlowProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetSlowProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Slow Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Slow im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Slow) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine("Slow-Assists fuer Slow III: " + Mathf.Min(totalSlowAssists, Mathf.Max(1, slowAssistsForMasteryGate)) + " / " + Mathf.Max(1, slowAssistsForMasteryGate));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Slow I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Slow II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Slow III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Slow IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Slow V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Slow Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetSlowProfile();

        if (profile == null)
            return "Slow Mastery: vorbereitet";

        return "Slow Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        SlowTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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

        if (!string.IsNullOrEmpty(definition.requiredNodeId))
        {
            SlowTowerMasteryNodeDefinition required = GetDefinition(definition.requiredNodeId);
            text += "Voraussetzung: " + (required != null ? required.displayName : definition.requiredNodeId) + "\n";
        }

        text += "\n" + definition.effectText;

        if (definition.keystone != SlowTowerKeystone.None)
            text += "\n\nKeystone-Regel: Pro Tower-Typ ist nur ein Keystone aktiv. Wechsel wirken fuer neue Runs.";

        return text;
    }

    public void StartNewRun()
    {
        runtimeStateByTowerId.Clear();
        slowedEnemiesThisWave.Clear();
        bossSlowBonusEnemiesThisWave.Clear();
        controlWindowAppliedEnemiesThisRun.Clear();
        currentWaveNumber = 0;
        currentWaveHadSlowContribution = false;
        currentWaveHasDensity = false;
        currentWaveHasRearguard = false;
        currentWaveHasToughness = false;
        currentWaveHasResistance = false;
        currentWaveHasChaosVariantGroup = false;
        currentWaveHasRunnerPressure = false;
        currentWaveSlowApplicationXP = 0;
        currentWaveChaosSlowBonusXP = 0;
        currentWaveCoordinatedSupportXP = 0;
        baseCalmBonusWaveNumber = -1;
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsSlowTower(tower))
            return;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.RecordTowerLevelReached(tower, tower.level);
    }

    public void NotifyLivesDamaged()
    {
        if (GetNodeRank(BaseCalm) <= 0)
            return;

        baseCalmBonusWaveNumber = Mathf.Max(baseCalmBonusWaveNumber, currentWaveNumber + 1);
    }

    public void RecordSlowKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsSlowTower(tower))
            return;

        currentWaveHadSlowContribution = true;
    }

    public void RecordSlowAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsSlowTower(tower))
            return;

        currentWaveHadSlowContribution = true;
    }

    public void RecordSlowDamage(Tower tower, float amount)
    {
        if (!IsSlowTower(tower) || amount <= 0f)
            return;

        currentWaveHadSlowContribution = true;
    }

    public void RecordSlowApplied(Tower tower, Enemy target, float finalSlowMultiplier, float finalDuration)
    {
        if (!IsSlowTower(tower) || target == null)
            return;

        currentWaveHadSlowContribution = true;
        int enemyId = target.GetInstanceID();

        if (!slowedEnemiesThisWave.Contains(enemyId))
        {
            slowedEnemiesThisWave.Add(enemyId);
            AddCappedSlowApplicationXP(firstSlowApplicationXP);

            if (target.enemyRole == EnemyRole.Runner)
                AddSlowMasteryXP(runnerSlowBonusXP);

            if (target.GetPathProgressPercent() >= 0.70f)
                AddSlowMasteryXP(nearBaseSlowBonusXP);
        }

        if (target.IsChaosVariant() && currentWaveChaosSlowBonusXP < maxChaosSlowBonusXPPerWave)
        {
            int award = Mathf.Min(Mathf.Max(0, chaosSlowBonusXP), Mathf.Max(0, maxChaosSlowBonusXPPerWave - currentWaveChaosSlowBonusXP));
            currentWaveChaosSlowBonusXP += award;
            AddSlowMasteryXP(award);
        }

        if (IsBossOrMiniBoss(target) && !bossSlowBonusEnemiesThisWave.Contains(enemyId))
        {
            bossSlowBonusEnemiesThisWave.Add(enemyId);
            AddSlowMasteryXP(target.enemyRole == EnemyRole.Boss || target.isBoss ? bossSlowBonusXP : miniBossSlowBonusXP);
        }

        ApplyControlWindowIfNeeded(tower, target);
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower slowContributor = FindSlowContributor(killingTower, contributors);

        if (slowContributor == null && enemy.WasRecentlySlowedBySlowTower())
            slowContributor = enemy.GetSlowMasterySourceTower();

        if (slowContributor == null)
            return;

        currentWaveHadSlowContribution = true;

        if (enemy.WasRecentlySlowedBySlowTower() || enemy.HasSlowControlWindow())
        {
            AddSlowMasteryXP(slowedDefeatAssistXP);
            totalSlowAssists++;
            UnlockSlowMasteryGateIIIIfReady(false);
            GrantCoordinatedSupportXP(killingTower, contributors);
            SaveProfile();
        }

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss || enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
            UnlockSlowMasteryGateIIIIfReady(true);
    }

    public float GetSlowXPMultiplier()
    {
        return 1f + GetNodeRank(ControlRoutine) * 0.03f;
    }

    public float GetSlowRangeBonus()
    {
        return GetNodeRank(ControlLine) * 0.12f;
    }

    public float GetSlowFireRateAdditive()
    {
        return GetNodeRank(CalmCadence) * 0.04f;
    }

    public float GetSlowFireRateMultiplier(Tower tower)
    {
        if (!IsSlowTower(tower))
            return 1f;

        float multiplier = 1f;

        if (GetNodeRank(DensityAnchor) > 0 && currentWaveHasDensity)
            multiplier += 0.05f;

        SlowTowerRuntimeState state = GetRuntimeState(tower);
        if (GetNodeRank(FasterReaction) > 0 && Time.time <= state.reactionUntilTime)
            multiplier += 0.10f;

        return Mathf.Max(0.01f, multiplier);
    }

    public SlowTowerMasteryShotContext PrepareSlowShot(Tower tower, Enemy currentTarget, int shotCounter)
    {
        SlowTowerMasteryShotContext context = new SlowTowerMasteryShotContext();
        context.primaryTarget = currentTarget;

        if (!IsSlowTower(tower))
            return context;

        currentWaveHadSlowContribution = true;
        SlowTowerRuntimeState state = GetRuntimeState(tower);

        if (GetNodeRank(FasterReaction) > 0 && FindEmergencyTarget(tower, 0.80f) != null)
            state.reactionUntilTime = Mathf.Max(state.reactionUntilTime, Time.time + 2f);

        Enemy priorityTarget = FindSlowPriorityTarget(tower, currentTarget, state);
        if (priorityTarget != null)
            context.primaryTarget = priorityTarget;

        int pulseInterval = GetControlPulseInterval();
        context.controlPulse = pulseInterval > 0 && shotCounter % pulseInterval == 0;

        if (GetActiveKeystone() == SlowTowerKeystone.StasisField && Time.time >= state.stasisCooldownUntilTime)
        {
            context.stasisField = true;
            state.stasisCooldownUntilTime = Time.time + Mathf.Max(0.1f, stasisFieldCooldown);
        }

        if (GetActiveKeystone() == SlowTowerKeystone.LastLine && state.lastLineUsedWave != currentWaveNumber)
        {
            Enemy lastLineTarget = FindEmergencyTarget(tower, 0.88f);

            if (lastLineTarget != null)
            {
                context.primaryTarget = lastLineTarget;
                context.lastLine = true;
                state.lastLineUsedWave = currentWaveNumber;
            }
        }

        return context;
    }

    public void FillSlowProjectileData(Projectile projectile, SlowTowerMasteryShotContext context)
    {
        if (projectile == null)
            return;

        projectile.slowMasteryControlPulse = context.controlPulse;
        projectile.slowMasteryLastLine = context.lastLine;
        projectile.applySlowStasisField = context.stasisField;
        projectile.slowStasisRadius = stasisFieldRadius;
        projectile.slowStasisAmount = context.lastLine ? 0.22f : 0.25f;
        projectile.slowStasisDuration = context.lastLine ? 1.4f : 0.9f;
    }

    public float GetModifiedSlowAmount(Tower tower, Enemy target, float baseSlowMultiplier, bool stasisField, bool lastLine)
    {
        if (!IsSlowTower(tower))
            return Mathf.Clamp(baseSlowMultiplier, 0.1f, 1f);

        float slowMultiplier = Mathf.Clamp(baseSlowMultiplier, 0.1f, 1f);
        slowMultiplier -= GetNodeRank(ColdCoil) * 0.01f;
        slowMultiplier -= GetNodeRank(DeeperCold) * 0.01f;

        if (target != null && target.GetPathProgressPercent() >= 0.70f && GetNodeRank(LateCold) > 0)
            slowMultiplier = Mathf.Lerp(slowMultiplier, 0.1f, 0.05f);

        if (currentWaveHasResistance && GetNodeRank(ResistanceAnalysis) > 0)
            slowMultiplier = Mathf.Lerp(slowMultiplier, 0.1f, 0.10f);

        if (stasisField)
            slowMultiplier = Mathf.Min(slowMultiplier, 0.25f);

        if (lastLine)
            slowMultiplier = Mathf.Min(slowMultiplier, 0.20f);

        if (IsBossOrMiniBoss(target))
        {
            float bossReduction = GetNodeRank(MiniBossInhibition) > 0 ? 0.45f : 0.55f;
            slowMultiplier = Mathf.Lerp(slowMultiplier, 1f, bossReduction);
        }

        return Mathf.Clamp(slowMultiplier, 0.1f, 1f);
    }

    public float GetModifiedSlowDuration(Tower tower, Enemy target, float baseDuration, bool controlPulse, bool stasisField, bool lastLine)
    {
        if (!IsSlowTower(tower))
            return Mathf.Max(0.1f, baseDuration);

        float duration = Mathf.Max(0.1f, baseDuration);
        duration += GetNodeRank(LongerInhibition) * 0.15f;
        duration += GetNodeRank(TimeStretch) * 0.20f;

        if (target != null && target.HasSlow() && GetNodeRank(StableFollowup) > 0)
            duration += 0.30f;

        if (controlPulse)
            duration += 0.25f;

        if (CountEnemiesInRange(tower) >= 5 && GetNodeRank(DenseRead) > 0)
            duration += 0.25f;

        if (currentWaveHasDensity && GetNodeRank(DensityAnchor) > 0)
            duration += 0.25f;

        if (target != null && target.enemyRole == EnemyRole.Runner)
            duration += GetNodeRank(RunnerBrake) * 0.20f;

        if (target != null && target.IsChaosVariant() && target.enemyRole == EnemyRole.Runner)
            duration += GetNodeRank(ChaosRunnerBrake) * 0.25f;

        if (target != null && target.GetPathProgressPercent() >= 0.75f && GetNodeRank(LeakGuard) > 0)
        {
            SlowTowerRuntimeState state = GetRuntimeState(tower);

            if (state.leakGuardUsedWave != currentWaveNumber)
            {
                duration += 1f;
                state.leakGuardUsedWave = currentWaveNumber;
            }
        }

        if (GetNodeRank(BaseCalm) > 0 && baseCalmBonusWaveNumber == currentWaveNumber)
            duration += 0.25f;

        if (stasisField)
            duration = Mathf.Max(duration, 0.90f);

        if (lastLine)
            duration += 1.20f;

        if (IsBossOrMiniBoss(target))
            duration *= GetNodeRank(MiniBossInhibition) > 0 ? 0.55f : 0.45f;

        return Mathf.Max(0.1f, duration);
    }

    public void ApplyStasisField(Tower tower, Enemy center, float slowMultiplier, float duration, float radius)
    {
        if (!IsSlowTower(tower) || center == null)
            return;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float safeRadius = Mathf.Max(0.1f, radius);

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (Vector3.Distance(enemy.transform.position, center.transform.position) > safeRadius)
                continue;

            float finalSlow = GetModifiedSlowAmount(tower, enemy, slowMultiplier, true, false);
            float finalDuration = GetModifiedSlowDuration(tower, enemy, duration, false, true, false);
            enemy.ApplySlow(finalSlow, finalDuration, tower);
        }
    }

    public float ModifyDamageAgainstSlowedTarget(Tower sourceTower, Enemy target, float damage)
    {
        if (sourceTower == null || target == null || damage <= 0f)
            return damage;

        if (!target.WasRecentlySlowedBySlowTower() && !target.HasSlowControlWindow())
            return damage;

        float modified = damage;
        int openTargetRank = GetNodeRank(OpenTarget);

        if (openTargetRank > 0)
            modified *= 1f + openTargetRank * 0.01f;

        if (target.HasSlowControlWindow())
        {
            if (GetActiveKeystone() == SlowTowerKeystone.ControlWindow)
                modified *= 1.08f;

            if (target.IsChaosVariant() && GetNodeRank(ChaosCoordination) > 0)
                modified *= 1.05f;
        }

        return modified;
    }

    public float ModifyDotDurationOnSlowedTarget(Tower sourceTower, Enemy target, float duration)
    {
        if (sourceTower == null || IsSlowTower(sourceTower) || target == null || duration <= 0f)
            return duration;

        if (GetNodeRank(DotTimeGain) <= 0)
            return duration;

        if (!target.WasRecentlySlowedBySlowTower() && !target.HasSlowControlWindow())
            return duration;

        return duration * 1.05f;
    }

    public string GetPathDisplayName(SlowTowerMasteryPath path)
    {
        switch (path)
        {
            case SlowTowerMasteryPath.TimeAnchor: return "Zeitanker";
            case SlowTowerMasteryPath.SupportTiming: return "Support-Taktung";
            case SlowTowerMasteryPath.EmergencyBrake: return "Notbremse";
            default: return "Einstieg";
        }
    }

    public string GetMilestoneDisplayName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return "Slow I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Slow II: Kontrolltechnik";
            case TowerMasteryMilestone.III: return "Slow III: Zeitmeisterschaft";
            case TowerMasteryMilestone.IV: return "Slow IV: Risskontrolle";
            case TowerMasteryMilestone.V: return "Slow V: Chronokern";
            default: return "Offen";
        }
    }

    public string GetKeystoneDisplayName(SlowTowerKeystone keystone)
    {
        switch (keystone)
        {
            case SlowTowerKeystone.StasisField: return "Stillstandsfeld";
            case SlowTowerKeystone.ControlWindow: return "Kontrollfenster";
            case SlowTowerKeystone.LastLine: return "Letzte Linie";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        slowedEnemiesThisWave.Clear();
        bossSlowBonusEnemiesThisWave.Clear();
        currentWaveHadSlowContribution = false;
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        currentWaveSlowApplicationXP = 0;
        currentWaveChaosSlowBonusXP = 0;
        currentWaveCoordinatedSupportXP = 0;
        currentWaveHasDensity = WaveHasBlock(waveData, ChaosWaveBlockType.Density);
        currentWaveHasRearguard = WaveHasBlock(waveData, ChaosWaveBlockType.Rearguard);
        currentWaveHasToughness = WaveHasBlock(waveData, ChaosWaveBlockType.Toughness);
        currentWaveHasResistance = WaveHasBlock(waveData, ChaosWaveBlockType.Resistance);
        currentWaveHasChaosVariantGroup = WaveHasBlock(waveData, ChaosWaveBlockType.ChaosVariantGroup);
        currentWaveHasRunnerPressure = WaveHasRole(waveData, EnemyRole.Runner);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadSlowContribution)
            return;

        bool chaosPressure = result.chaosLevelAtWaveStart > 0 || result.hadChaosWaveBlocksAtWaveStart || result.chaosVariantSpawnCount > 0;

        if (chaosPressure && (currentWaveHasDensity || currentWaveHasRearguard || currentWaveHasRunnerPressure || currentWaveHasChaosVariantGroup))
            AddSlowMasteryXP(densityRearguardChaosWaveBonusXP);
    }

    private Enemy FindSlowPriorityTarget(Tower tower, Enemy currentTarget, SlowTowerRuntimeState state)
    {
        if (tower == null || state == null)
            return currentTarget;

        if (GetNodeRank(RearguardFocus) > 0 && currentWaveHasRearguard)
        {
            Enemy rearguardTarget = FindLateHardTarget(tower);

            if (rearguardTarget != null)
                return rearguardTarget;
        }

        if (GetNodeRank(EscapeLine) > 0 || GetNodeRank(LateCold) > 0 || GetNodeRank(LeakGuard) > 0)
        {
            Enemy escapeTarget = FindEscapeTarget(tower);

            if (escapeTarget != null)
                return escapeTarget;
        }

        return currentTarget;
    }

    private Enemy FindEscapeTarget(Tower tower)
    {
        if (tower == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy best = null;
        float bestScore = -Mathf.Infinity;
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || Vector3.Distance(tower.transform.position, enemy.transform.position) > range)
                continue;

            float progress = enemy.GetPathProgressPercent();
            float score = progress;

            if (enemy.enemyRole == EnemyRole.Runner)
                score += 0.35f;

            if (enemy.IsChaosVariant() && enemy.enemyRole == EnemyRole.Runner)
                score += 0.20f;

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return bestScore >= 0.60f ? best : null;
    }

    private Enemy FindEmergencyTarget(Tower tower, float minimumProgress)
    {
        if (tower == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy best = null;
        float bestProgress = Mathf.Clamp01(minimumProgress);
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || Vector3.Distance(tower.transform.position, enemy.transform.position) > range)
                continue;

            float progress = enemy.GetPathProgressPercent();

            if (progress >= bestProgress)
            {
                bestProgress = progress;
                best = enemy;
            }
        }

        return best;
    }

    private Enemy FindLateHardTarget(Tower tower)
    {
        if (tower == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy best = null;
        float bestScore = -Mathf.Infinity;
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || Vector3.Distance(tower.transform.position, enemy.transform.position) > range)
                continue;

            if (!IsHardTarget(enemy))
                continue;

            float score = enemy.GetPathProgressPercent() + enemy.GetHealthPercent() * 0.20f;

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return bestScore >= 0.45f ? best : null;
    }

    private void ApplyControlWindowIfNeeded(Tower tower, Enemy target)
    {
        if (!IsSlowTower(tower) || target == null)
            return;

        bool shouldApplyWindow = GetNodeRank(SetWindow) > 0 || GetActiveKeystone() == SlowTowerKeystone.ControlWindow;

        if (!shouldApplyWindow)
            return;

        int enemyId = target.GetInstanceID();

        if (GetActiveKeystone() == SlowTowerKeystone.ControlWindow)
        {
            if (controlWindowAppliedEnemiesThisRun.Contains(enemyId))
                return;

            controlWindowAppliedEnemiesThisRun.Add(enemyId);
        }

        float duration = GetActiveKeystone() == SlowTowerKeystone.ControlWindow ? keystoneControlWindowDuration : controlWindowBaseDuration;
        duration += GetNodeRank(WindowTraining) * 0.25f;

        if (currentWaveHasToughness && GetNodeRank(RiftSupport) > 0)
            duration += 0.25f;

        if (IsBossOrMiniBoss(target) && GetNodeRank(GuideHeavyTargets) > 0)
            duration += 0.25f;

        target.ApplySlowControlWindow(duration, tower);
    }

    private void GrantCoordinatedSupportXP(Tower killingTower, IEnumerable<Tower> contributors)
    {
        int rank = GetNodeRank(CoordinatedFire);

        if (rank <= 0 || currentWaveCoordinatedSupportXP >= maxCoordinatedSupportXPPerWave)
            return;

        HashSet<Tower> towers = new HashSet<Tower>();

        if (killingTower != null)
            towers.Add(killingTower);

        if (contributors != null)
        {
            foreach (Tower tower in contributors)
            {
                if (tower != null)
                    towers.Add(tower);
            }
        }

        foreach (Tower tower in towers)
        {
            if (tower == null || IsSlowTower(tower))
                continue;

            if (currentWaveCoordinatedSupportXP >= maxCoordinatedSupportXPPerWave)
                break;

            int award = Mathf.Min(Mathf.Max(1, rank), maxCoordinatedSupportXPPerWave - currentWaveCoordinatedSupportXP);
            tower.AddXP(award);
            currentWaveCoordinatedSupportXP += award;
        }
    }

    private int GetControlPulseInterval()
    {
        switch (GetNodeRank(ControlPulse))
        {
            case 1: return 6;
            case 2: return 5;
            case 3: return 4;
            default: return 0;
        }
    }

    private void AddCappedSlowApplicationXP(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0 || currentWaveSlowApplicationXP >= maxSlowApplicationXPPerWave)
            return;

        int award = Mathf.Min(safeAmount, maxSlowApplicationXPPerWave - currentWaveSlowApplicationXP);
        currentWaveSlowApplicationXP += award;
        AddSlowMasteryXP(award);
    }

    private int CountEnemiesInRange(Tower tower)
    {
        if (tower == null)
            return 0;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int count = 0;
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (Vector3.Distance(tower.transform.position, enemy.transform.position) <= range)
                count++;
        }

        return count;
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

    private bool WaveHasRole(WaveData waveData, EnemyRole role)
    {
        if (waveData == null || waveData.spawnEntries == null)
            return false;

        foreach (EnemySpawnEntry entry in waveData.spawnEntries)
        {
            if (entry != null && entry.enemyRole == role && entry.amount > 0)
                return true;
        }

        return false;
    }

    private Tower FindSlowContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsSlowTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsSlowTower(tower))
                return tower;
        }

        return null;
    }

    private bool IsSlowTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Slow;
    }

    private bool IsBossOrMiniBoss(Enemy target)
    {
        return target != null && (target.enemyRole == EnemyRole.Boss || target.enemyRole == EnemyRole.MiniBoss || target.isBoss || target.isMiniBoss || target.isElite || target.enemyRole == EnemyRole.Elite);
    }

    private bool IsHardTarget(Enemy target)
    {
        return target != null &&
               (target.enemyRole == EnemyRole.Tank ||
                target.enemyRole == EnemyRole.Knight ||
                target.enemyRole == EnemyRole.MiniBoss ||
                target.enemyRole == EnemyRole.Boss ||
                target.enemyRole == EnemyRole.Elite ||
                target.isMiniBoss ||
                target.isBoss ||
                target.isElite);
    }

    private void AddSlowMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Slow, amount);
    }

    private void UnlockSlowMasteryGateIIIIfReady(bool forceByBossParticipation)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = towerMastery != null ? towerMastery.GetProfile(TowerRole.Slow) : null;

        if (profile == null || profile.bossKillWithTower)
            return;

        if (!forceByBossParticipation && totalSlowAssists < Mathf.Max(1, slowAssistsForMasteryGate))
            return;

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Slow);
    }

    private TowerMasteryRoleProfile GetSlowProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Slow) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private SlowTowerRuntimeState GetRuntimeState(Tower tower)
    {
        int key = tower != null ? tower.GetInstanceID() : 0;

        if (!runtimeStateByTowerId.TryGetValue(key, out SlowTowerRuntimeState state) || state == null)
        {
            state = new SlowTowerRuntimeState();
            runtimeStateByTowerId[key] = state;
        }

        return state;
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetSlowProfile();
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
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | Boss/Assist-Ziel " + (profile.bossKillWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.IV:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 85) + "/85 | Chaos 3 " + (profile.chaos3WaveWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.V:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 130) + "/130 | Chaos 5/Elite " + (profile.chaos5BossOrEliteWithTower ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private string GetKeystoneNodeId(SlowTowerKeystone keystone)
    {
        switch (keystone)
        {
            case SlowTowerKeystone.StasisField: return StasisField;
            case SlowTowerKeystone.ControlWindow: return ControlWindow;
            case SlowTowerKeystone.LastLine: return LastLine;
            default: return "";
        }
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(ColdCoil, "Kaeltere Spule", SlowTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "Slow-Multiplier wird um 0,01 pro Rang reduziert.", 1, 2, 3, 4, 5);
        AddDefinition(LongerInhibition, "Laengere Hemmung", SlowTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,15s Slow Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ControlLine, "Kontrolllinie", SlowTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,12 Range pro Rang.", 1, 2, 3);
        AddDefinition(CalmCadence, "Ruhiger Takt", SlowTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,04 Fire Rate pro Rang.", 1, 2, 3);
        AddDefinition(ControlRoutine, "Kontrollroutine", SlowTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Slow Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);

        AddDefinition(DeeperCold, "Tiefere Kaelte", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.I, 5, "Slow-Multiplier wird um 0,01 pro Rang reduziert.", 1, 2, 3, 4, 5);
        AddDefinition(TimeStretch, "Zeitdehnung", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.I, 5, "+0,20s Slow Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(StableFollowup, "Stabiles Nachsetzen", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.II, 1, "Treffer auf bereits verlangsamte Gegner erneuern Slow effizienter.", 6);
        AddDefinition(ControlPulse, "Kontrollpuls", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.II, 3, "Jeder 6./5./4. Treffer verlaengert Slow zusaetzlich um 0,25s.", 4, 5, 6);
        AddDefinition(DenseRead, "Dichte lesen", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.III, 1, "Bei 5+ Gegnern in Range: Slow Duration +0,25s.", 8);
        AddDefinition(MiniBossInhibition, "MiniBoss-Hemmung", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.III, 1, "Slow gegen MiniBoss/Boss wirkt etwas zuverlaessiger, bleibt aber reduziert.", 8);
        AddDefinition(DensityAnchor, "Verdichtungsanker", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.IV, 1, "In Density-Chaos-Waves: +5% Fire Rate und +0,25s Slow Duration.", 10);
        AddDefinition(ResistanceAnalysis, "Resistenzanalyse", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.IV, 1, "In Resistance-Waves verliert Slow 10% weniger Wirkung.", 12);
        AddDefinition(StasisField, "Keystone: Stillstandsfeld", SlowTowerMasteryPath.TimeAnchor, TowerMasteryMilestone.V, 1, "Alle paar Sekunden erzeugt der naechste Treffer ein kurzes starkes Slow-Feld. Kein Freeze, Boss/MiniBoss reduziert.", new int[] { 24 }, SlowTowerKeystone.StasisField);

        AddDefinition(OpenTarget, "Offenes Ziel", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.I, 5, "Durch Slow Tower verlangsamte Gegner nehmen +1% Schaden von Towern pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(CoordinatedFire, "Koordinierter Beschuss", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.II, 3, "Slow-Assists geben kleine Bonus-XP an beteiligte Tower, stark gedeckelt.", 3, 4, 5);
        AddDefinition(SetWindow, "Fenster setzen", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.II, 1, "Neu verlangsamte Gegner erhalten kurz ein Kontrollfenster.", 6);
        AddDefinition(WindowTraining, "Fenstertraining", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.III, 2, "Kontrollfenster dauert +0,25s pro Rang.", 5, 7);
        AddDefinition(DotTimeGain, "DoT-Zeitgewinn", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.III, 1, "Burn/Poison von anderen Towern laeuft auf verlangsamten Gegnern 5% laenger.", 8);
        AddDefinition(GuideHeavyTargets, "Schwere Ziele fuehren", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.III, 1, "Slow setzt harte Ziele verlaesslicher ins Schadensfenster, ohne Burst-Schaden.", 8);
        AddDefinition(ChaosCoordination, "Chaos-Koordination", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten gibt Kontrollfenster +5% Schaden, falls Ziel nicht immun ist.", 10);
        AddDefinition(RiftSupport, "Riss-Support", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.IV, 1, "In Chaos-Waves mit Toughness haelt Kontrollfenster +0,25s laenger.", 10);
        AddDefinition(ControlWindow, "Keystone: Kontrollfenster", SlowTowerMasteryPath.SupportTiming, TowerMasteryMilestone.V, 1, "Der erste Slow auf einem Gegner markiert ihn 3s lang. Alle Tower verursachen gegen markierte Ziele +8% Schaden. Einmal pro Gegner.", new int[] { 22 }, SlowTowerKeystone.ControlWindow);

        AddDefinition(RunnerBrake, "Runner-Bremse", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.I, 5, "Slow Duration gegen Runner +0,20s pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(EscapeLine, "Fluchtlinie erkennen", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.II, 1, "Slow Tower priorisiert Gegner mit hohem Wegfortschritt etwas besser.", 5);
        AddDefinition(LateCold, "Spaete Kaelte", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.II, 1, "Gegner ueber 70% Wegfortschritt werden 5% staerker verlangsamt.", 6);
        AddDefinition(LeakGuard, "Leckschutz", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.III, 1, "Einmal pro Wave: erster Gegner ueber 75% Wegfortschritt erhaelt +1s Slow Duration.", 8);
        AddDefinition(RearguardFocus, "Nachhut-Fokus", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.III, 1, "In Rearguard-/PreBoss-Waves priorisiert Slow spaete harte Gegner besser.", 8);
        AddDefinition(FasterReaction, "Schnellere Reaktion", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.III, 1, "Wenn ein Gegner 80% Wegfortschritt erreicht, erhaelt Slow 2s lang +10% Fire Rate.", 8);
        AddDefinition(ChaosRunnerBrake, "Chaos-Runner-Bremse", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.IV, 2, "+0,25s Slow Duration gegen Chaos-Runner pro Rang.", 6, 8);
        AddDefinition(BaseCalm, "Base-Ruhe", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.IV, 1, "Wenn Lives Schaden genommen haben, erhalten Slow Tower in der naechsten Wave +0,25s Slow Duration.", 10);
        AddDefinition(LastLine, "Keystone: Letzte Linie", SlowTowerMasteryPath.EmergencyBrake, TowerMasteryMilestone.V, 1, "Einmal pro Wave wird der erste gefaehrliche Gegner nahe der Base stark verlangsamt. Boss/MiniBoss reduziert.", new int[] { 22 }, SlowTowerKeystone.LastLine);
    }

    private void AddDefinition(string nodeId, string displayName, SlowTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, SlowTowerKeystone.None, "");
    }

    private void AddDefinition(string nodeId, string displayName, SlowTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, SlowTowerKeystone keystone, string requiredNodeId = "")
    {
        SlowTowerMasteryNodeDefinition definition = new SlowTowerMasteryNodeDefinition(nodeId, displayName, path, gate, maxRank, costs, effectText, keystone, requiredNodeId);
        definitions.Add(definition);
        definitionById[nodeId] = definition;
    }

    private void EnsureDefinitions()
    {
        if (definitions.Count == 0 || definitionById.Count == 0)
            BuildDefinitions();
    }

    private void EnsureNodeStates()
    {
        if (nodeStates == null)
            nodeStates = new List<SlowTowerMasteryNodeState>();
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        EnsureNodeStates();

        foreach (SlowTowerMasteryNodeState state in nodeStates)
        {
            if (state == null || state.nodeId != nodeId)
                continue;

            state.rank = Mathf.Max(0, rank);
            return;
        }

        nodeStates.Add(new SlowTowerMasteryNodeState
        {
            nodeId = nodeId,
            rank = Mathf.Max(0, rank)
        });
    }

    private void LoadProfile()
    {
        EnsureDefinitions();
        EnsureNodeStates();
        nodeStates.Clear();
        totalSlowAssists = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalSlowAssists", 0);

        foreach (SlowTowerMasteryNodeDefinition definition in definitions)
        {
            int rank = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, 0);

            if (rank > 0)
                SetNodeRank(definition.nodeId, Mathf.Min(rank, definition.maxRank));
        }
    }

    private void SaveProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null && towerMastery.IsMetaProgressionSuppressedForCurrentRun())
            return;

        EnsureDefinitions();
        EnsureNodeStates();

        foreach (SlowTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalSlowAssists", Mathf.Max(0, totalSlowAssists));
        PlayerPrefs.Save();
    }
}
