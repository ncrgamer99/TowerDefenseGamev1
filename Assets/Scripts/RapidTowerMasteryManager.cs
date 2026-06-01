using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum RapidTowerMasteryPath
{
    Trunk,
    Stormfire,
    RunnerHunt,
    NeedlePrecision
}

public enum RapidTowerKeystone
{
    None,
    ContinuousFireCore,
    EscapeStopper,
    NeedleHail
}

[System.Serializable]
public class RapidTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class RapidTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public RapidTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public RapidTowerKeystone keystone;
    public string requiredNodeId;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public RapidTowerMasteryNodeDefinition(string nodeId, string displayName, RapidTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, int[] rankCosts, string effectText, RapidTowerKeystone keystone = RapidTowerKeystone.None, string requiredNodeId = "")
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

public struct RapidTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public bool criticalHit;
    public bool applyEscapeMark;
    public bool applyNeedleMark;
    public int needleMarkThreshold;
    public int needleMarkApplications;
    public int needleBonusDamage;
    public bool needleBonusArmorPierce;
    public int needleHailDamage;
}

public class RapidTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_RapidMastery_";

    public const string LightTrigger = "light_trigger";
    public const string CleanBullets = "clean_bullets";
    public const string QuickAdjustment = "quick_adjustment";
    public const string TrainedHand = "trained_hand";
    public const string CleanupRoutine = "cleanup_routine";

    public const string FireCadence = "fire_cadence";
    public const string Warmup = "warmup";
    public const string MagazineFlow = "magazine_flow";
    public const string ContinuousFireDiscipline = "continuous_fire_discipline";
    public const string OverheatControl = "overheat_control";
    public const string DenseRain = "dense_rain";
    public const string ChaosTiming = "chaos_timing";
    public const string ContinuousFireCore = "continuous_fire_core";

    public const string RunnerReader = "runner_reader";
    public const string FastRetarget = "fast_retarget";
    public const string EscapeLine = "escape_line";
    public const string LeakGuard = "leak_guard";
    public const string NervousHand = "nervous_hand";
    public const string ChaosRunnerAnalysis = "chaos_runner_analysis";
    public const string LastSecond = "last_second";
    public const string EscapeStopper = "escape_stopper";

    public const string WeakPointSight = "weak_point_sight";
    public const string FineAmmo = "fine_ammo";
    public const string NeedleMarks = "needle_marks";
    public const string MarkTraining = "mark_training";
    public const string ArmorChip = "armor_chip";
    public const string MageInterrupt = "mage_interrupt";
    public const string LearnerAnalysis = "learner_analysis";
    public const string NeedleHail = "needle_hail";

    public static RapidTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<RapidTowerMasteryNodeState> nodeStates = new List<RapidTowerMasteryNodeState>();

    [Header("XP Bonuses")]
    public int runnerKillBonusXP = 4;
    public int nearBaseKillBonusXP = 3;
    public int runnerDensityChaosWaveBonusXP = 16;

    [Header("Runtime")]
    public int overdriveShotsRequired = 24;
    public float overdriveDuration = 4f;
    public float overdriveCooldown = 10f;
    public float rapidEscapeMarkDuration = 2.5f;
    public float rapidNeedleMarkDuration = 3f;
    public float sustainedFirePauseWindow = 0.85f;

    private class RapidTowerRuntimeState
    {
        public float lastShotTime = -100f;
        public int sustainedShotCount = 0;
        public float warmupUntilTime = -1f;
        public int warmupStacks = 0;
        public int overdriveCharge = 0;
        public float overdriveUntilTime = -1f;
        public float overdriveCooldownUntilTime = -1f;
        public float runnerPulseUntilTime = -1f;
        public int lastSecondUsedWave = -1;
    }

    private readonly List<RapidTowerMasteryNodeDefinition> definitions = new List<RapidTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, RapidTowerMasteryNodeDefinition> definitionById = new Dictionary<string, RapidTowerMasteryNodeDefinition>();
    private readonly Dictionary<int, RapidTowerRuntimeState> runtimeStateByTowerId = new Dictionary<int, RapidTowerRuntimeState>();

    private int currentWaveNumber = 0;
    private bool currentWaveHadRapidContribution = false;
    private bool currentWaveHasRunnerPressure = false;
    private bool currentWaveHasDensityPressure = false;

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

    public static RapidTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        RapidTowerMasteryManager existing = FindObjectOfType<RapidTowerMasteryManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("RapidTowerMasterySystem");
        RapidTowerMasteryManager manager = systemObject.AddComponent<RapidTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public static bool TryGetActive(out RapidTowerMasteryManager manager)
    {
        manager = Instance != null ? Instance : FindObjectOfType<RapidTowerMasteryManager>();
        return manager != null;
    }

    public IReadOnlyList<RapidTowerMasteryNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public RapidTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();
        return !string.IsNullOrEmpty(nodeId) && definitionById.TryGetValue(nodeId, out RapidTowerMasteryNodeDefinition definition) ? definition : null;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        EnsureNodeStates();

        foreach (RapidTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Rapid, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        RapidTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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

        TowerMasteryRoleProfile profile = GetRapidProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        RapidTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Rapid, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != RapidTowerKeystone.None && GetActiveKeystone() == RapidTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(RapidTowerKeystone keystone)
    {
        if (keystone == RapidTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(RapidTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Rapid, keystone.ToString());
    }

    public RapidTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetRapidProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return RapidTowerKeystone.None;

        try
        {
            return (RapidTowerKeystone)System.Enum.Parse(typeof(RapidTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return RapidTowerKeystone.None;
        }
    }

    public string GetNodeStateText(RapidTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != RapidTowerKeystone.None && GetActiveKeystone() == definition.keystone)
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
        TowerMasteryRoleProfile profile = GetRapidProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetRapidProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Rapid Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Rapid im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Rapid) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Rapid I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Rapid II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Rapid III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Rapid IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Rapid V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Rapid Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetRapidProfile();

        if (profile == null)
            return "Rapid Mastery: vorbereitet";

        return "Rapid Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        RapidTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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
            RapidTowerMasteryNodeDefinition required = GetDefinition(definition.requiredNodeId);
            text += "Voraussetzung: " + (required != null ? required.displayName : definition.requiredNodeId) + "\n";
        }

        text += "\n" + definition.effectText;

        if (definition.keystone != RapidTowerKeystone.None)
            text += "\n\nKeystone-Regel: Pro Tower-Typ ist nur ein Keystone aktiv. Wechsel wirken fuer neue Runs.";

        return text;
    }

    public void StartNewRun()
    {
        runtimeStateByTowerId.Clear();
        currentWaveNumber = 0;
        currentWaveHadRapidContribution = false;
        currentWaveHasRunnerPressure = false;
        currentWaveHasDensityPressure = false;
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsRapidTower(tower))
            return;

        GetTowerMasteryManager()?.RecordTowerLevelReached(tower, tower.level);
    }

    public void RecordRapidDamage(Tower tower, float appliedDamage)
    {
        if (!IsRapidTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadRapidContribution = true;
    }

    public void RecordRapidKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsRapidTower(tower))
            return;

        currentWaveHadRapidContribution = true;

        if (killedRole == EnemyRole.Runner)
            AddRapidMasteryXP(runnerKillBonusXP);
    }

    public void RecordRapidAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsRapidTower(tower))
            return;

        currentWaveHadRapidContribution = true;
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower rapidContributor = FindRapidContributor(killingTower, contributors);

        if (rapidContributor == null)
            return;

        currentWaveHadRapidContribution = true;

        if (IsRapidTower(killingTower) && enemy.GetPathProgressPercent() >= 0.70f)
            AddRapidMasteryXP(nearBaseKillBonusXP);
    }

    public float GetRapidXPMultiplier()
    {
        return 1f + GetNodeRank(TrainedHand) * 0.03f;
    }

    public float GetRapidDamageBaseBonus()
    {
        return GetNodeRank(CleanBullets) * 0.25f;
    }

    public float GetRapidRangeBonus()
    {
        return GetNodeRank(QuickAdjustment) * 0.08f;
    }

    public float GetRapidFireRateAdditive()
    {
        return GetNodeRank(LightTrigger) * 0.04f + GetNodeRank(FireCadence) * 0.05f;
    }

    public float GetRapidFireRateMultiplier(Tower tower)
    {
        if (!IsRapidTower(tower))
            return 1f;

        RapidTowerRuntimeState state = GetRuntimeState(tower);
        float multiplier = 1f;

        if (Time.time > state.warmupUntilTime)
            state.warmupStacks = 0;

        if (state.warmupStacks > 0 && Time.time <= state.warmupUntilTime)
            multiplier += 0.08f * state.warmupStacks;

        int disciplineRank = GetNodeRank(ContinuousFireDiscipline);
        if (disciplineRank > 0 && CountEnemiesInRange(tower) >= 6)
            multiplier += disciplineRank * 0.03f;

        if (GetNodeRank(DenseRain) > 0 && currentWaveHasDensityPressure)
            multiplier += 0.05f;

        if (GetNodeRank(NervousHand) > 0 && Time.time <= state.runnerPulseUntilTime)
            multiplier += 0.10f;

        if (GetActiveKeystone() == RapidTowerKeystone.ContinuousFireCore && Time.time <= state.overdriveUntilTime)
            multiplier += 0.25f;

        return Mathf.Max(0.01f, multiplier);
    }

    public float GetCleanRetargetReadyBonus()
    {
        switch (GetNodeRank(FastRetarget))
        {
            case 1: return 0.10f;
            case 2: return 0.15f;
            case 3: return 0.20f;
            default: return 0f;
        }
    }

    public RapidTowerMasteryShotContext PrepareRapidShot(Tower tower, Enemy currentTarget, int shotCounter)
    {
        RapidTowerMasteryShotContext context = new RapidTowerMasteryShotContext();
        context.primaryTarget = currentTarget;

        if (!IsRapidTower(tower))
            return context;

        currentWaveHadRapidContribution = true;

        RapidTowerRuntimeState state = GetRuntimeState(tower);
        NotifyRapidShot(tower, currentTarget, state);

        Enemy priorityTarget = FindRapidPriorityTarget(tower, state);
        if (priorityTarget != null)
            context.primaryTarget = priorityTarget;

        if (context.primaryTarget != null && GetNodeRank(WeakPointSight) > 0)
            context.criticalHit = Random.value <= Mathf.Clamp01(GetNodeRank(WeakPointSight) * 0.02f);

        if (context.primaryTarget != null && GetActiveKeystone() == RapidTowerKeystone.EscapeStopper && IsEscapeTarget(context.primaryTarget))
            context.applyEscapeMark = true;

        if (context.primaryTarget != null && GetNodeRank(NeedleMarks) > 0)
        {
            context.applyNeedleMark = true;
            context.needleMarkThreshold = GetNeedleMarkThreshold();
            context.needleMarkApplications = 1 + (GetNodeRank(MageInterrupt) > 0 && context.primaryTarget.enemyRole == EnemyRole.Mage ? 1 : 0);
            context.needleBonusArmorPierce = GetNodeRank(ArmorChip) > 0;
        }

        return context;
    }

    public int CalculateRapidShotDamage(Tower tower, Enemy target, int baseDamage, RapidTowerMasteryShotContext context, float projectileMultiplier)
    {
        float damageValue = Mathf.Max(0f, baseDamage) * Mathf.Max(0f, projectileMultiplier);

        if (target != null)
        {
            if (target.GetHealthPercent() <= 0.35f)
                damageValue *= 1f + GetNodeRank(CleanupRoutine) * 0.02f;

            if (target.enemyRole == EnemyRole.Runner)
                damageValue *= 1f + GetNodeRank(RunnerReader) * 0.04f;

            if (target.GetPathProgressPercent() >= 0.70f)
                damageValue *= 1f + GetNodeRank(LeakGuard) * 0.03f;

            if (target.IsChaosVariant() && target.enemyRole == EnemyRole.Runner)
                damageValue *= 1f + GetNodeRank(ChaosRunnerAnalysis) * 0.05f;

            if (target.enemyRole == EnemyRole.Learner)
                damageValue *= 1f + GetNodeRank(LearnerAnalysis) * 0.05f;

            if (target.HasRapidEscapeMark())
                damageValue *= 1.20f;
        }

        if (context.criticalHit)
            damageValue *= GetCritDamageMultiplier();

        return Mathf.Max(0, Mathf.RoundToInt(damageValue));
    }

    public void FillRapidProjectileData(Projectile projectile, RapidTowerMasteryShotContext context, int baseDamage)
    {
        if (projectile == null)
            return;

        projectile.applyRapidEscapeMark = context.applyEscapeMark;
        projectile.applyRapidNeedleMark = context.applyNeedleMark;
        projectile.rapidNeedleMarkThreshold = context.needleMarkThreshold;
        projectile.rapidNeedleMarkApplications = context.needleMarkApplications;
        projectile.rapidNeedleBonusDamage = context.applyNeedleMark ? Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseDamage) * 0.45f)) : 0;
        projectile.rapidNeedleBonusArmorPierce = context.needleBonusArmorPierce;
        projectile.rapidNeedleHailDamage = context.applyNeedleMark && GetActiveKeystone() == RapidTowerKeystone.NeedleHail
            ? Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseDamage) * 0.25f))
            : 0;
        projectile.rapidEscapeMarkDuration = rapidEscapeMarkDuration;
        projectile.rapidNeedleMarkDuration = rapidNeedleMarkDuration;
    }

    public string GetPathDisplayName(RapidTowerMasteryPath path)
    {
        switch (path)
        {
            case RapidTowerMasteryPath.Stormfire: return "Sturmfeuer";
            case RapidTowerMasteryPath.RunnerHunt: return "Runner-Jagd";
            case RapidTowerMasteryPath.NeedlePrecision: return "Nadelpraezision";
            default: return "Einstieg";
        }
    }

    public string GetMilestoneDisplayName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return "Rapid I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Rapid II: Taktfeuer";
            case TowerMasteryMilestone.III: return "Rapid III: Feuerdisziplin";
            case TowerMasteryMilestone.IV: return "Rapid IV: Rissdruck";
            case TowerMasteryMilestone.V: return "Rapid V: Uebersteuerung";
            default: return "Offen";
        }
    }

    public string GetKeystoneDisplayName(RapidTowerKeystone keystone)
    {
        switch (keystone)
        {
            case RapidTowerKeystone.ContinuousFireCore: return "Dauerfeuerkern";
            case RapidTowerKeystone.EscapeStopper: return "Fluchtstopper";
            case RapidTowerKeystone.NeedleHail: return "Nadelhagel";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveHadRapidContribution = false;
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        currentWaveHasRunnerPressure = waveData != null && waveData.ContainsRole(EnemyRole.Runner);
        currentWaveHasDensityPressure = WaveHasDensityPressure(waveData);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadRapidContribution)
            return;

        if ((currentWaveHasRunnerPressure || currentWaveHasDensityPressure) && result.chaosLevelAtWaveStart > 0)
            AddRapidMasteryXP(runnerDensityChaosWaveBonusXP);
    }

    private void NotifyRapidShot(Tower tower, Enemy target, RapidTowerRuntimeState state)
    {
        if (state == null)
            return;

        float timeSinceLastShot = Time.time - state.lastShotTime;
        state.sustainedShotCount = timeSinceLastShot <= sustainedFirePauseWindow ? state.sustainedShotCount + 1 : 1;
        state.lastShotTime = Time.time;

        int warmupThreshold = GetWarmupShotThreshold(target);

        if (warmupThreshold > 0 && state.sustainedShotCount >= warmupThreshold)
        {
            int maxStacks = GetNodeRank(OverheatControl) > 0 ? 2 : 1;
            state.warmupStacks = Mathf.Clamp(state.warmupStacks + 1, 1, maxStacks);
            state.warmupUntilTime = Mathf.Max(state.warmupUntilTime, Time.time + GetWarmupDuration());
            state.sustainedShotCount = 0;
        }

        if (GetNodeRank(NervousHand) > 0 && FindRunnerInRange(tower) != null)
            state.runnerPulseUntilTime = Mathf.Max(state.runnerPulseUntilTime, Time.time + 2f);

        if (GetActiveKeystone() == RapidTowerKeystone.ContinuousFireCore)
            UpdateOverdriveState(state);
    }

    private void UpdateOverdriveState(RapidTowerRuntimeState state)
    {
        if (state == null)
            return;

        if (Time.time <= state.overdriveUntilTime)
            return;

        if (Time.time < state.overdriveCooldownUntilTime)
            return;

        state.overdriveCharge++;

        if (state.overdriveCharge < Mathf.Max(1, overdriveShotsRequired))
            return;

        state.overdriveCharge = 0;
        state.overdriveUntilTime = Time.time + Mathf.Max(0.1f, overdriveDuration);
        state.overdriveCooldownUntilTime = state.overdriveUntilTime + Mathf.Max(0.1f, overdriveCooldown);
    }

    private Enemy FindRapidPriorityTarget(Tower tower, RapidTowerRuntimeState state)
    {
        if (tower == null)
            return null;

        if (GetNodeRank(LastSecond) > 0 && state != null && state.lastSecondUsedWave != currentWaveNumber)
        {
            Enemy lastSecondTarget = FindEmergencyTarget(tower, 0.90f);

            if (lastSecondTarget != null)
            {
                state.lastSecondUsedWave = currentWaveNumber;
                return lastSecondTarget;
            }
        }

        if (GetNodeRank(EscapeLine) > 0 || GetActiveKeystone() == RapidTowerKeystone.EscapeStopper)
        {
            Enemy escapeTarget = FindEscapeTarget(tower);

            if (escapeTarget != null)
                return escapeTarget;
        }

        return null;
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

    private Enemy FindRunnerInRange(Tower tower)
    {
        if (tower == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.enemyRole != EnemyRole.Runner)
                continue;

            if (Vector3.Distance(tower.transform.position, enemy.transform.position) <= range)
                return enemy;
        }

        return null;
    }

    private int CountEnemiesInRange(Tower tower)
    {
        if (tower == null)
            return 0;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float range = Mathf.Max(0f, tower.GetEffectiveRange());
        int count = 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (Vector3.Distance(tower.transform.position, enemy.transform.position) <= range)
                count++;
        }

        return count;
    }

    private bool IsEscapeTarget(Enemy enemy)
    {
        if (enemy == null)
            return false;

        return enemy.enemyRole == EnemyRole.Runner || enemy.GetPathProgressPercent() >= 0.70f;
    }

    private int GetWarmupShotThreshold(Enemy target)
    {
        int rank = GetNodeRank(Warmup);

        if (rank <= 0)
            return 0;

        int threshold = rank == 1 ? 12 : rank == 2 ? 10 : 8;

        if (target != null && target.IsChaosVariant() && GetNodeRank(ChaosTiming) > 0)
            threshold = Mathf.Max(6, Mathf.RoundToInt(threshold * 0.85f));

        return threshold;
    }

    private float GetWarmupDuration()
    {
        return 2f * (1f + GetNodeRank(MagazineFlow) * 0.15f);
    }

    private int GetNeedleMarkThreshold()
    {
        if (GetNodeRank(NeedleMarks) <= 0)
            return 0;

        return Mathf.Clamp(8 - GetNodeRank(MarkTraining), 6, 8);
    }

    private float GetCritDamageMultiplier()
    {
        switch (GetNodeRank(FineAmmo))
        {
            case 1: return 1.35f;
            case 2: return 1.40f;
            case 3: return 1.45f;
            default: return 1.25f;
        }
    }

    private bool WaveHasDensityPressure(WaveData waveData)
    {
        if (waveData == null)
            return false;

        if (waveData.totalSpawnCount >= 20 || waveData.modifiedEnemyCount >= waveData.requestedEnemyCount + 5)
            return true;

        if (waveData.chaosWaveBlocks == null)
            return false;

        foreach (ChaosWaveBlock block in waveData.chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
                continue;

            if (block.blockType == ChaosWaveBlockType.Density || block.blockType == ChaosWaveBlockType.RolePressure)
                return true;
        }

        return false;
    }

    private Tower FindRapidContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsRapidTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsRapidTower(tower))
                return tower;
        }

        return null;
    }

    private bool IsRapidTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Rapid;
    }

    private RapidTowerRuntimeState GetRuntimeState(Tower tower)
    {
        int key = tower != null ? tower.GetInstanceID() : 0;

        if (!runtimeStateByTowerId.TryGetValue(key, out RapidTowerRuntimeState state) || state == null)
        {
            state = new RapidTowerRuntimeState();
            runtimeStateByTowerId[key] = state;
        }

        return state;
    }

    private void AddRapidMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Rapid, amount);
    }

    private TowerMasteryRoleProfile GetRapidProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Rapid) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetRapidProfile();
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
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | Boss-Beteiligung " + (profile.bossKillWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.IV:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 85) + "/85 | Chaos 3 " + (profile.chaos3WaveWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.V:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 130) + "/130 | Chaos 5/Elite " + (profile.chaos5BossOrEliteWithTower ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private string GetKeystoneNodeId(RapidTowerKeystone keystone)
    {
        switch (keystone)
        {
            case RapidTowerKeystone.ContinuousFireCore: return ContinuousFireCore;
            case RapidTowerKeystone.EscapeStopper: return EscapeStopper;
            case RapidTowerKeystone.NeedleHail: return NeedleHail;
            default: return "";
        }
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(LightTrigger, "Leichter Abzug", RapidTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,04 Fire Rate pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(CleanBullets, "Saubere Kugeln", RapidTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,25 Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(QuickAdjustment, "Schnelle Justierung", RapidTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,08 Range pro Rang.", 1, 2, 3);
        AddDefinition(TrainedHand, "Trainierte Hand", RapidTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Rapid Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);
        AddDefinition(CleanupRoutine, "Aufraeumroutine", RapidTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+2% Damage gegen Gegner unter 35% HP pro Rang.", 1, 2, 3);

        AddDefinition(FireCadence, "Feuerkadenz", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.I, 5, "+0,05 Fire Rate pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(Warmup, "Warmlauf", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.II, 3, "Nach 12/10/8 Schuessen ohne Pause erhaelt Rapid kurz +8% Fire Rate.", 3, 4, 5);
        AddDefinition(MagazineFlow, "Magazinfluss", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.II, 2, "Warmlauf haelt 15%/30% laenger.", 5, 7);
        AddDefinition(ContinuousFireDiscipline, "Dauerfeuer-Disziplin", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.III, 3, "Bei 6+ Gegnern in Range: +3% Fire Rate pro Rang.", 4, 5, 6);
        AddDefinition(OverheatControl, "Ueberhitzungskontrolle", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.III, 1, "Warmlauf kann 1 zusaetzliche Stufe speichern.", 8);
        AddDefinition(DenseRain, "Dichter Regen", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.IV, 1, "Gegen Waves mit Verdichtung/hohem Spawn-Druck: +5% Fire Rate.", 8);
        AddDefinition(ChaosTiming, "Chaos-Taktung", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten laedt Warmlauf 15% schneller.", 10);
        AddDefinition(ContinuousFireCore, "Keystone: Dauerfeuerkern", RapidTowerMasteryPath.Stormfire, TowerMasteryMilestone.V, 1, "Kontinuierliches Feuern baut Overdrive auf. Voller Overdrive gibt kurz +25% Fire Rate.", new int[] { 22 }, RapidTowerKeystone.ContinuousFireCore);

        AddDefinition(RunnerReader, "Runner-Leser", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.I, 5, "+4% Damage gegen Runner pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FastRetarget, "Schneller Zielwechsel", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.II, 3, "Nach einem Kill wird der naechste Schuss 10%/15%/20% schneller bereit.", 3, 4, 5);
        AddDefinition(EscapeLine, "Fluchtlinie erkennen", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.II, 1, "Rapid priorisiert Gegner mit hohem Wegfortschritt etwas besser.", 5);
        AddDefinition(LeakGuard, "Leckschutz", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.III, 3, "Gegner ueber 70% Wegfortschritt erhalten +3% Damage pro Rang.", 4, 5, 6);
        AddDefinition(NervousHand, "Nervoese Hand", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.III, 1, "Wenn ein Runner in Range kommt, erhaelt Rapid 2s lang +10% Fire Rate.", 8);
        AddDefinition(ChaosRunnerAnalysis, "Chaos-Runner-Analyse", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.IV, 3, "+5% Damage gegen Chaos-Runner pro Rang.", 5, 7, 9);
        AddDefinition(LastSecond, "Letzte Sekunde", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.IV, 1, "Einmal pro Wave: Gegner kurz vor der Base wird von Rapid bevorzugt.", 10);
        AddDefinition(EscapeStopper, "Keystone: Fluchtstopper", RapidTowerMasteryPath.RunnerHunt, TowerMasteryMilestone.V, 1, "Der erste Treffer gegen schnelle oder weit fortgeschrittene Gegner markiert sie kurz. Rapid verursacht gegen markierte Fluchtziele +20% Damage.", new int[] { 20 }, RapidTowerKeystone.EscapeStopper);

        AddDefinition(WeakPointSight, "Schwachstellenblick", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.I, 5, "+2% Crit-Chance pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FineAmmo, "Feine Munition", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.II, 3, "Crits verursachen moderaten Bonusdamage.", 3, 4, 5);
        AddDefinition(NeedleMarks, "Nadelmarken", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.II, 1, "Jeder Treffer setzt eine kurzlebige Marke. Bei 8 Marken: kleiner Bonus-Hit.", 6);
        AddDefinition(MarkTraining, "Marken-Training", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.III, 2, "Bonus-Hit bei 7/6 Marken.", new int[] { 6, 8 }, RapidTowerKeystone.None, NeedleMarks);
        AddDefinition(ArmorChip, "Armor-Chip", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.III, 1, "Nadelmarken-Bonus-Hit ignoriert 1 Armor.", 8);
        AddDefinition(MageInterrupt, "Mage-Unterbrechung", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.IV, 1, "Trefferketten gegen Mages laden Marken schneller.", 8);
        AddDefinition(LearnerAnalysis, "Learner-Analyse", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.IV, 2, "Rapid verursacht +5% Damage gegen Learner pro Rang, ohne Effektumgehung.", 6, 8);
        AddDefinition(NeedleHail, "Keystone: Nadelhagel", RapidTowerMasteryPath.NeedlePrecision, TowerMasteryMilestone.V, 1, "Wenn Nadelmarken ausloesen, trifft Rapid das gleiche Ziel mit einem zusaetzlichen schwachen Projektiltreffer.", new int[] { 22 }, RapidTowerKeystone.NeedleHail);
    }

    private void AddDefinition(string nodeId, string displayName, RapidTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, RapidTowerKeystone.None, "");
    }

    private void AddDefinition(string nodeId, string displayName, RapidTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, RapidTowerKeystone keystone, string requiredNodeId = "")
    {
        RapidTowerMasteryNodeDefinition definition = new RapidTowerMasteryNodeDefinition(nodeId, displayName, path, gate, maxRank, costs, effectText, keystone, requiredNodeId);
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
            nodeStates = new List<RapidTowerMasteryNodeState>();
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        EnsureNodeStates();

        foreach (RapidTowerMasteryNodeState state in nodeStates)
        {
            if (state == null || state.nodeId != nodeId)
                continue;

            state.rank = Mathf.Max(0, rank);
            return;
        }

        nodeStates.Add(new RapidTowerMasteryNodeState
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

        foreach (RapidTowerMasteryNodeDefinition definition in definitions)
        {
            int rank = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, 0);

            if (rank > 0)
                SetNodeRank(definition.nodeId, Mathf.Min(rank, definition.maxRank));
        }
    }

    private void SaveProfile()
    {
        EnsureDefinitions();
        EnsureNodeStates();

        foreach (RapidTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.Save();
    }
}
