using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum BasicTowerMasteryPath
{
    Trunk,
    Precision,
    Salvo,
    Stability
}

public enum BasicTowerMasteryMilestone
{
    None = 0,
    BasicI = 1,
    BasicII = 2,
    BasicIII = 3,
    BasicIV = 4,
    BasicV = 5
}

public enum BasicTowerKeystone
{
    None,
    AnchorProjectile,
    ControlledSalvo,
    Foundation
}

[System.Serializable]
public class BasicTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class BasicTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public BasicTowerMasteryPath path;
    public BasicTowerMasteryMilestone gate;
    public BasicTowerKeystone keystone;
    public string requiredNodeId;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public BasicTowerMasteryNodeDefinition(string nodeId, string displayName, BasicTowerMasteryPath path, BasicTowerMasteryMilestone gate, int maxRank, int[] rankCosts, string effectText, BasicTowerKeystone keystone = BasicTowerKeystone.None, string requiredNodeId = "")
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

public struct BasicTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public bool focusShot;
    public bool ignoreArmor;
    public bool reloadShot;
    public bool applyAnchorMark;
    public bool doubleTap;
    public Enemy controlledSalvoTarget;
}

public class BasicTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_BasicMastery_";

    public const string SolidCore = "solid_core";
    public const string CleanMechanics = "clean_mechanics";
    public const string MeasuredLine = "measured_line";
    public const string RoutineFire = "routine_fire";
    public const string BasicTraining = "basic_training";
    public const string KnightReader = "knight_reader";
    public const string TankReader = "tank_reader";
    public const string ArmorSight = "armor_sight";
    public const string FocusShot = "focus_shot";
    public const string FocusTraining = "focus_training";
    public const string MiniBossRoutine = "miniboss_routine";
    public const string BossRoutine = "boss_routine";
    public const string ChaosKnightAnalysis = "chaos_knight_analysis";
    public const string AnchorProjectile = "anchor_projectile";
    public const string FastBolt = "fast_bolt";
    public const string RunnerReader = "runner_reader";
    public const string CleanRetarget = "clean_retarget";
    public const string DoubleTap = "double_tap";
    public const string DoubleTapTraining = "double_tap_training";
    public const string MassRoutine = "mass_routine";
    public const string RiftSalvo = "rift_salvo";
    public const string ReloadWindow = "reload_window";
    public const string ControlledSalvo = "controlled_salvo";
    public const string SimpleBlueprint = "simple_blueprint";
    public const string SafeRefund = "safe_refund";
    public const string TrainingRoutine = "training_routine";
    public const string FirstAnchor = "first_anchor";
    public const string FirstAnchorTwo = "first_anchor_two";
    public const string SteadyHand = "steady_hand";
    public const string StabilizedLine = "stabilized_line";
    public const string EmergencyFocus = "emergency_focus";
    public const string Foundation = "foundation";

    public static BasicTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Profile")]
    public int masteryXP = 0;
    public int unspentPoints = 0;
    public int spentPoints = 0;
    public BasicTowerKeystone activeKeystone = BasicTowerKeystone.None;
    public List<BasicTowerMasteryNodeState> nodeStates = new List<BasicTowerMasteryNodeState>();

    [Header("Achievements")]
    public int bestBasicLevelEver = 1;
    public bool reachedBasicLevel10 = false;
    public bool reachedBasicLevel20 = false;
    public bool bossKillWithBasic = false;
    public bool chaos3WaveWithBasic = false;
    public bool chaos5BossOrEliteWithBasic = false;
    public int lastRunMasteryXPGained = 0;
    public int lastRunMasteryPointsGained = 0;

    [Header("XP Rewards")]
    public float damageToMasteryXPRatio = 0.04f;
    public int maxDamageMasteryXPPerWave = 24;
    public int killMasteryXP = 3;
    public int assistMasteryXP = 1;
    public int miniBossParticipationXP = 10;
    public int bossParticipationXP = 25;
    public int chaosWaveParticipationXP = 12;
    public int chaos5BossParticipationXP = 80;

    [Header("Run Point Rules")]
    public int minimumWaveForPointPayout = 5;
    public int maxPointsPerRun = 5;

    private readonly List<BasicTowerMasteryNodeDefinition> definitions = new List<BasicTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, BasicTowerMasteryNodeDefinition> definitionById = new Dictionary<string, BasicTowerMasteryNodeDefinition>();
    private readonly HashSet<int> awardedLevelMilestonesThisRun = new HashSet<int>();

    private int currentWaveNumber = 0;
    private int highestWaveReachedThisRun = 0;
    private int highestBasicLevelThisRun = 1;
    private int firstBasicTowerInstanceId = 0;
    private bool runFinalized = false;
    private bool currentWaveHadBasicContribution = false;
    private float currentWaveDamageMasteryXP = 0f;
    private float damageMasteryXPFraction = 0f;
    private float steadyHandUntilTime = 0f;
    private bool foundationBossUpgradeGranted = false;

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
        WaveEventBus.GameOverTriggered += HandleGameOverTriggered;
    }

    private void OnDisable()
    {
        WaveEventBus.WaveStarted -= HandleWaveStarted;
        WaveEventBus.WaveCompleted -= HandleWaveCompleted;
        WaveEventBus.GameOverTriggered -= HandleGameOverTriggered;

        if (Instance == this)
            Instance = null;
    }

    public static BasicTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        BasicTowerMasteryManager existing = FindObjectOfType<BasicTowerMasteryManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("BasicTowerMasterySystem");
        BasicTowerMasteryManager manager = systemObject.AddComponent<BasicTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public static bool TryGetActive(out BasicTowerMasteryManager manager)
    {
        manager = Instance != null ? Instance : FindObjectOfType<BasicTowerMasteryManager>();
        return manager != null;
    }

    public void StartNewRun()
    {
        currentWaveNumber = 0;
        highestWaveReachedThisRun = 0;
        highestBasicLevelThisRun = 1;
        firstBasicTowerInstanceId = 0;
        runFinalized = false;
        currentWaveHadBasicContribution = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        steadyHandUntilTime = 0f;
        foundationBossUpgradeGranted = false;
        lastRunMasteryXPGained = 0;
        lastRunMasteryPointsGained = 0;
        awardedLevelMilestonesThisRun.Clear();
    }

    public void FinalizeRun()
    {
        if (runFinalized)
            return;

        runFinalized = true;

        if (IsMetaProgressionSuppressedForCurrentRun())
            return;

        TowerMasteryManager towerMastery = TowerMasteryManager.GetOrCreate(gameManager);

        if (towerMastery != null)
        {
            towerMastery.FinalizeRun();
            ApplyGlobalTowerMasteryProfile(towerMastery.GetProfile(TowerRole.Basic));
        }
        else
        {
            RefreshHighestBasicLevelFromActiveTowers();
            SaveProfile();
        }
    }

    public IReadOnlyList<BasicTowerMasteryNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public BasicTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();
        return !string.IsNullOrEmpty(nodeId) && definitionById.TryGetValue(nodeId, out BasicTowerMasteryNodeDefinition definition) ? definition : null;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        EnsureNodeStates();

        foreach (BasicTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        return GetNodeRank(nodeId) > 0;
    }

    public bool IsMilestoneUnlocked(BasicTowerMasteryMilestone milestone)
    {
        int masteryLevel = GetMasteryLevel();

        switch (milestone)
        {
            case BasicTowerMasteryMilestone.None:
                return true;
            case BasicTowerMasteryMilestone.BasicI:
                return spentPoints >= 10 && masteryLevel >= 3 && reachedBasicLevel10;
            case BasicTowerMasteryMilestone.BasicII:
                return spentPoints >= 25 && masteryLevel >= 8 && reachedBasicLevel20;
            case BasicTowerMasteryMilestone.BasicIII:
                return spentPoints >= 50 && masteryLevel >= 15 && bossKillWithBasic;
            case BasicTowerMasteryMilestone.BasicIV:
                return spentPoints >= 85 && masteryLevel >= 25 && chaos3WaveWithBasic;
            case BasicTowerMasteryMilestone.BasicV:
                return spentPoints >= 130 && masteryLevel >= 40 && chaos5BossOrEliteWithBasic;
            default:
                return false;
        }
    }

    public bool CanPurchaseNode(string nodeId)
    {
        BasicTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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

        return unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        BasicTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);

        unspentPoints -= cost;
        spentPoints += cost;
        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != BasicTowerKeystone.None && activeKeystone == BasicTowerKeystone.None)
            activeKeystone = definition.keystone;

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(BasicTowerKeystone keystone)
    {
        if (keystone == BasicTowerKeystone.None)
            return false;

        if (!CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool CanEditMetaProgression()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
            return true;

        return !gameManager.gameStarted || gameManager.isGameOver;
    }

    public bool TryActivateKeystone(BasicTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        activeKeystone = keystone;
        SaveProfile();
        return true;
    }

    public string GetNodeStateText(BasicTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != BasicTowerKeystone.None && activeKeystone == definition.keystone)
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
        return unspentPoints >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Basic Mastery Level: " + GetMasteryLevel() + " | XP: " + masteryXP);
        builder.AppendLine("Punkte: " + unspentPoints + " frei | " + spentPoints + " ausgegeben");
        builder.AppendLine("Bester Basic im Run/Ewig: " + highestBasicLevelThisRun + " / " + bestBasicLevelEver);
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(activeKeystone));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Basic I: " + GetMilestoneProgressText(BasicTowerMasteryMilestone.BasicI));
        builder.AppendLine("- Basic II: " + GetMilestoneProgressText(BasicTowerMasteryMilestone.BasicII));
        builder.AppendLine("- Basic III: " + GetMilestoneProgressText(BasicTowerMasteryMilestone.BasicIII));
        builder.AppendLine("- Basic IV: " + GetMilestoneProgressText(BasicTowerMasteryMilestone.BasicIV));
        builder.AppendLine("- Basic V: " + GetMilestoneProgressText(BasicTowerMasteryMilestone.BasicV));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt: Level-Gate + Wave-Gate + Impact-Gate.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        return "Basic Mastery: XP " + masteryXP +
               " | Punkte frei " + unspentPoints +
               " | ausgegeben " + spentPoints +
               " | letzter Run +" + lastRunMasteryPointsGained + " Punkt(e), +" + lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(activeKeystone) +
               " | Visual " + GetMasteryVisualTier();
    }

    public int GetMasteryVisualTier()
    {
        if (IsMilestoneUnlocked(BasicTowerMasteryMilestone.BasicV)) return 5;
        if (IsMilestoneUnlocked(BasicTowerMasteryMilestone.BasicIV)) return 4;
        if (IsMilestoneUnlocked(BasicTowerMasteryMilestone.BasicIII)) return 3;
        if (IsMilestoneUnlocked(BasicTowerMasteryMilestone.BasicII)) return 2;
        if (IsMilestoneUnlocked(BasicTowerMasteryMilestone.BasicI)) return 1;
        return 0;
    }

    public int GetMasteryLevel()
    {
        return CalculateMasteryLevelFromXP(masteryXP);
    }

    public int GetXPToNextMasteryLevel(int currentMasteryLevel)
    {
        return 100 + Mathf.Max(1, currentMasteryLevel) * 25;
    }

    public void ApplyGlobalTowerMasteryProfile(TowerMasteryRoleProfile profile)
    {
        if (profile == null || profile.towerRole != TowerRole.Basic)
            return;

        masteryXP = Mathf.Max(0, profile.masteryXP);
        unspentPoints = Mathf.Max(0, profile.unspentPoints);
        spentPoints = Mathf.Max(0, profile.spentPoints);
        bestBasicLevelEver = Mathf.Max(1, profile.bestLevelEver);
        reachedBasicLevel10 = profile.reachedLevel10;
        reachedBasicLevel20 = profile.reachedLevel20;
        bossKillWithBasic = profile.bossKillWithTower;
        chaos3WaveWithBasic = profile.chaos3WaveWithTower;
        chaos5BossOrEliteWithBasic = profile.chaos5BossOrEliteWithTower;
        activeKeystone = ParseGlobalKeystoneId(profile.activeKeystoneId, activeKeystone);
        lastRunMasteryXPGained = Mathf.Max(0, profile.lastRunMasteryXPGained);
        lastRunMasteryPointsGained = Mathf.Max(0, profile.lastRunMasteryPointsGained);
        SaveProfile();
    }

    public string GetNodeDetailText(string nodeId)
    {
        BasicTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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
            BasicTowerMasteryNodeDefinition required = GetDefinition(definition.requiredNodeId);
            text += "Voraussetzung: " + (required != null ? required.displayName : definition.requiredNodeId) + "\n";
        }

        text += "\n" + definition.effectText;

        if (definition.keystone != BasicTowerKeystone.None)
            text += "\n\nKeystone-Regel: Pro Tower-Typ ist nur ein Keystone aktiv. Wechsel wirken fuer neue Runs.";

        return text;
    }

    public void RecordBasicDamage(Tower tower, float appliedDamage)
    {
        if (!IsBasicTower(tower) || appliedDamage <= 0f)
            return;

        MarkBasicContribution(tower);

        if (currentWaveDamageMasteryXP >= maxDamageMasteryXPPerWave)
            return;

        damageMasteryXPFraction += appliedDamage * Mathf.Max(0f, damageToMasteryXPRatio);
        int wholeXP = Mathf.FloorToInt(damageMasteryXPFraction);

        if (wholeXP <= 0)
            return;

        int remainingCap = Mathf.Max(0, maxDamageMasteryXPPerWave - Mathf.FloorToInt(currentWaveDamageMasteryXP));
        int awarded = Mathf.Min(wholeXP, remainingCap);

        if (awarded <= 0)
            return;

        damageMasteryXPFraction -= awarded;
        currentWaveDamageMasteryXP += awarded;
        AddMasteryXP(awarded);
    }

    public void RecordBasicKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsBasicTower(tower))
            return;

        MarkBasicContribution(tower);
        AddMasteryXP(killMasteryXP);
    }

    public void RecordBasicAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsBasicTower(tower))
            return;

        MarkBasicContribution(tower);
        AddMasteryXP(assistMasteryXP);
    }

    public void RecordBasicLevelReached(Tower tower, int level)
    {
        if (!IsBasicTower(tower) || IsMetaProgressionSuppressedForCurrentRun())
            return;

        int safeLevel = Mathf.Max(1, level);
        highestBasicLevelThisRun = Mathf.Max(highestBasicLevelThisRun, safeLevel);

        if (IsRunActiveForPayout())
            return;

        bestBasicLevelEver = Mathf.Max(bestBasicLevelEver, safeLevel);

        if (safeLevel >= 10)
            reachedBasicLevel10 = true;

        if (safeLevel >= 20)
            reachedBasicLevel20 = true;

        int milestone = safeLevel / 10 * 10;

        if (milestone >= 10 && milestone <= 50 && !awardedLevelMilestonesThisRun.Contains(milestone))
        {
            awardedLevelMilestonesThisRun.Add(milestone);
            AddMasteryXP(milestone / 10 * 15);
        }

        SaveProfile();
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower basicContributor = FindBasicContributor(killingTower, contributors);

        if (basicContributor == null)
            return;

        MarkBasicContribution(basicContributor);

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
            AddMasteryXP(miniBossParticipationXP);

        bool bossTarget = enemy.enemyRole == EnemyRole.Boss || enemy.isBoss;
        bool eliteTarget = enemy.enemyRole == EnemyRole.Elite || enemy.isElite;
        bool runActive = IsRunActiveForPayout();

        if (bossTarget)
        {
            if (!runActive)
                bossKillWithBasic = true;

            AddMasteryXP(bossParticipationXP);
            TryGrantFoundationBossUpgradePoint(basicContributor);
        }

        if (bossTarget && GetCurrentChaosLevel() >= 5)
        {
            if (!runActive)
                chaos5BossOrEliteWithBasic = true;

            AddMasteryXP(chaos5BossParticipationXP);
        }

        if (!runActive && (bossTarget || eliteTarget) && GetCurrentChaosLevel() >= 5)
            chaos5BossOrEliteWithBasic = true;

        if (!runActive)
            SaveProfile();
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsBasicTower(tower))
            return;

        RecordBasicLevelReached(tower, tower.level);

        if (firstBasicTowerInstanceId != 0)
            return;

        firstBasicTowerInstanceId = tower.GetInstanceID();

        int startingXP = GetNodeRank(BasicTraining) * 5;

        if (GetNodeRank(FirstAnchorTwo) > 0 || activeKeystone == BasicTowerKeystone.Foundation)
            tower.RaiseToMinimumLevel(activeKeystone == BasicTowerKeystone.Foundation ? 3 : 2);

        if (GetNodeRank(FirstAnchor) > 0)
            startingXP += 15;

        if (startingXP > 0)
            tower.AddXP(startingXP);
    }

    public void NotifyLivesDamaged()
    {
        if (GetNodeRank(SteadyHand) <= 0)
            return;

        steadyHandUntilTime = Mathf.Max(steadyHandUntilTime, Time.time + 5f);
    }

    public int GetModifiedBasicBuildCost(int baseCost)
    {
        int reduction = GetNodeRank(SimpleBlueprint);
        return Mathf.Max(0, Mathf.Max(0, baseCost) - reduction);
    }

    public static int GetModifiedBuildCost(int baseCost, GameObject prefab, string displayName)
    {
        if (!IsBasicBuildOption(prefab, displayName))
            return Mathf.Max(0, baseCost);

        BasicTowerMasteryManager manager = GetOrCreate();
        return manager != null ? manager.GetModifiedBasicBuildCost(baseCost) : Mathf.Max(0, baseCost);
    }

    public static bool IsBasicBuildOption(GameObject prefab, string displayName)
    {
        if (!string.IsNullOrEmpty(displayName) && displayName.ToLowerInvariant().Contains("basic"))
            return true;

        Tower tower = prefab != null ? prefab.GetComponent<Tower>() : null;
        return tower != null && tower.towerRole == TowerRole.Basic;
    }

    public float GetBasicSellRefundBonus()
    {
        return 0f;
    }

    public float GetBasicXPMultiplier()
    {
        return 1f + GetNodeRank(RoutineFire) * 0.03f + GetNodeRank(TrainingRoutine) * 0.04f;
    }

    public float GetBasicDamageBaseBonus()
    {
        return GetNodeRank(SolidCore) * 0.5f;
    }

    public float GetBasicFireRateAdditive()
    {
        return GetNodeRank(CleanMechanics) * 0.05f + GetNodeRank(FastBolt) * 0.04f;
    }

    public float GetBasicFireRateMultiplier(Tower tower)
    {
        float multiplier = 1f;

        int massRank = GetNodeRank(MassRoutine);
        if (massRank > 0 && CountEnemiesInRange(tower) >= 5)
            multiplier += massRank * 0.03f;

        if (GetNodeRank(SteadyHand) > 0 && Time.time <= steadyHandUntilTime)
            multiplier += 0.10f;

        return Mathf.Max(0.01f, multiplier);
    }

    public float GetCleanRetargetReadyBonus()
    {
        switch (GetNodeRank(CleanRetarget))
        {
            case 1: return 0.10f;
            case 2: return 0.15f;
            case 3: return 0.20f;
            default: return 0f;
        }
    }

    public float GetBasicRangeBonus(Tower tower, float currentRange)
    {
        float bonus = GetNodeRank(MeasuredLine) * 0.15f;

        if (GetNodeRank(StabilizedLine) > 0 && IsTowerNearBase(tower))
            bonus += Mathf.Max(0f, currentRange) * 0.05f;

        return bonus;
    }

    public BasicTowerMasteryShotContext PrepareBasicShot(Tower tower, Enemy currentTarget, int shotCounter, int shotsSinceKill)
    {
        BasicTowerMasteryShotContext context = new BasicTowerMasteryShotContext();
        context.primaryTarget = currentTarget;

        if (!IsBasicTower(tower))
            return context;

        context.reloadShot = GetNodeRank(ReloadWindow) > 0 && shotsSinceKill >= 10;

        int focusInterval = GetFocusShotInterval();
        context.focusShot = focusInterval > 0 && shotCounter % focusInterval == 0;

        if (context.focusShot)
        {
            Enemy strongest = FindStrongestEnemyInRange(tower);
            if (strongest != null)
                context.primaryTarget = strongest;
        }
        else if (GetNodeRank(EmergencyFocus) > 0)
        {
            Enemy emergencyTarget = FindEmergencyTarget(tower);
            if (emergencyTarget != null)
                context.primaryTarget = emergencyTarget;
        }

        int armorInterval = GetArmorSightInterval();
        context.ignoreArmor = armorInterval > 0 && shotCounter % armorInterval == 0;
        context.applyAnchorMark = context.focusShot && activeKeystone == BasicTowerKeystone.AnchorProjectile && IsHardTarget(context.primaryTarget);

        float doubleTapChance = GetDoubleTapChance();
        context.doubleTap = doubleTapChance > 0f && Random.value <= doubleTapChance;

        if (activeKeystone == BasicTowerKeystone.ControlledSalvo && shotCounter % 10 == 0)
            context.controlledSalvoTarget = FindSecondTargetInRange(tower, context.primaryTarget);

        return context;
    }

    public int CalculateBasicShotDamage(Tower tower, Enemy target, int baseDamage, BasicTowerMasteryShotContext context, float projectileMultiplier)
    {
        float damageValue = Mathf.Max(0f, baseDamage) * Mathf.Max(0f, projectileMultiplier);

        if (target != null)
        {
            if (target.enemyRole == EnemyRole.Knight)
                damageValue *= 1f + GetNodeRank(KnightReader) * 0.03f;

            if (target.enemyRole == EnemyRole.Tank)
                damageValue *= 1f + GetNodeRank(TankReader) * 0.03f;

            if (target.enemyRole == EnemyRole.Runner)
                damageValue *= 1f + GetNodeRank(RunnerReader) * 0.03f;

            if (target.enemyRole == EnemyRole.MiniBoss || target.isMiniBoss)
                damageValue *= 1f + GetNodeRank(MiniBossRoutine) * 0.05f;

            if (target.enemyRole == EnemyRole.Boss || target.isBoss)
                damageValue *= 1f + GetNodeRank(BossRoutine) * 0.04f;

            if (target.IsChaosVariant() && target.enemyRole == EnemyRole.Knight)
                damageValue *= 1f + GetNodeRank(ChaosKnightAnalysis) * 0.04f;

            if (target.HasBasicAnchorMark())
                damageValue *= 1.10f;
        }

        if (context.focusShot)
            damageValue *= 1.35f;

        if (context.reloadShot)
            damageValue *= 1.50f;

        return Mathf.Max(0, Mathf.RoundToInt(damageValue));
    }

    public int CalculateBasicDoubleTapDamage(Tower tower, Enemy target, int baseDamage, BasicTowerMasteryShotContext context)
    {
        float multiplier = 0.50f;

        if (target != null && target.IsChaosVariant() && GetNodeRank(RiftSalvo) > 0)
            multiplier *= 1.20f;

        return CalculateBasicShotDamage(tower, target, baseDamage, context, multiplier);
    }

    public int CalculateBasicControlledSalvoDamage(Tower tower, Enemy target, int baseDamage, BasicTowerMasteryShotContext context)
    {
        return CalculateBasicShotDamage(tower, target, baseDamage, context, 0.60f);
    }

    public string GetPathDisplayName(BasicTowerMasteryPath path)
    {
        switch (path)
        {
            case BasicTowerMasteryPath.Precision: return "Praezision";
            case BasicTowerMasteryPath.Salvo: return "Salve";
            case BasicTowerMasteryPath.Stability: return "Stabilitaet";
            default: return "Einstieg";
        }
    }

    public string GetMilestoneDisplayName(BasicTowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case BasicTowerMasteryMilestone.BasicI: return "Basic I: Vertrautheit";
            case BasicTowerMasteryMilestone.BasicII: return "Basic II: Spezialisierung";
            case BasicTowerMasteryMilestone.BasicIII: return "Basic III: Meisterschaft";
            case BasicTowerMasteryMilestone.BasicIV: return "Basic IV: Risspruefung";
            case BasicTowerMasteryMilestone.BasicV: return "Basic V: Grundpfeiler";
            default: return "Offen";
        }
    }

    public string GetKeystoneDisplayName(BasicTowerKeystone keystone)
    {
        switch (keystone)
        {
            case BasicTowerKeystone.AnchorProjectile: return "Ankerprojektil";
            case BasicTowerKeystone.ControlledSalvo: return "Kontrollierte Salve";
            case BasicTowerKeystone.Foundation: return "Grundpfeiler";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveHadBasicContribution = false;
        currentWaveDamageMasteryXP = 0f;
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        highestWaveReachedThisRun = Mathf.Max(highestWaveReachedThisRun, currentWaveNumber);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || IsMetaProgressionSuppressedForCurrentRun())
            return;

        highestWaveReachedThisRun = Mathf.Max(highestWaveReachedThisRun, result.waveNumber);

        if (!currentWaveHadBasicContribution)
            return;

        if (IsRunActiveForPayout())
            return;

        if (result.chaosLevelAtWaveStart > 0 || result.hadChaosWaveBlocksAtWaveStart || result.chaosVariantSpawnCount > 0)
            AddMasteryXP(chaosWaveParticipationXP);

        if (result.chaosLevelAtWaveStart >= 3)
            chaos3WaveWithBasic = true;

        SaveProfile();
    }

    private void HandleGameOverTriggered()
    {
        FinalizeRun();
    }

    private void TryGrantFoundationBossUpgradePoint(Tower basicContributor)
    {
        if (foundationBossUpgradeGranted || activeKeystone != BasicTowerKeystone.Foundation || basicContributor == null)
            return;

        foundationBossUpgradeGranted = true;

        Tower target = FindFirstBasicTowerForRun();
        if (target == null)
            target = basicContributor;

        target.AddUpgradePoints(1);
    }

    private void MarkBasicContribution(Tower tower)
    {
        if (!IsBasicTower(tower))
            return;

        currentWaveHadBasicContribution = true;
        RecordBasicLevelReached(tower, tower.level);
    }

    private void RefreshHighestBasicLevelFromActiveTowers()
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Tower tower in towers)
        {
            if (!IsBasicTower(tower))
                continue;

            RecordBasicLevelReached(tower, tower.level);
        }
    }

    private void AddMasteryXP(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager != null && gameManager.gameStarted && !runFinalized)
            return;

        masteryXP += safeAmount;
        SaveProfile();
    }

    private Tower FindBasicContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsBasicTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsBasicTower(tower))
                return tower;
        }

        return null;
    }

    private Tower FindFirstBasicTowerForRun()
    {
        if (firstBasicTowerInstanceId == 0)
            return null;

        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Tower tower in towers)
        {
            if (tower != null && tower.GetInstanceID() == firstBasicTowerInstanceId)
                return tower;
        }

        return null;
    }

    private bool IsBasicTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Basic;
    }

    private int GetCurrentChaosLevel()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
            return 0;

        WaveCompletionResult result = gameManager.GetCurrentWaveResult();
        if (result != null)
            return result.chaosLevelAtWaveStart;

        ChaosJusticeManager chaosJusticeManager = gameManager.GetChaosJusticeManager();
        return chaosJusticeManager != null ? chaosJusticeManager.GetChaosLevel() : 0;
    }

    private bool IsRunActiveForPayout()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager != null && gameManager.gameStarted && !runFinalized;
    }

    private bool IsMetaProgressionSuppressedForCurrentRun()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager != null && gameManager.IsMetaProgressionSuppressedForCurrentRun();
    }

    private int CalculateMasteryLevelFromXP(int totalMasteryXP)
    {
        int remainingXP = Mathf.Max(0, totalMasteryXP);
        int level = 1;

        while (remainingXP >= GetXPToNextMasteryLevel(level))
        {
            remainingXP -= GetXPToNextMasteryLevel(level);
            level++;

            if (level > 999)
                break;
        }

        return level;
    }

    private BasicTowerKeystone ParseGlobalKeystoneId(string keystoneId, BasicTowerKeystone fallback)
    {
        if (string.IsNullOrEmpty(keystoneId))
            return BasicTowerKeystone.None;

        string normalized = keystoneId.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

        if (normalized == "none" || normalized == "keinkeystone")
            return BasicTowerKeystone.None;

        if (normalized == "anchorprojectile" || normalized == "ankerprojektil")
            return BasicTowerKeystone.AnchorProjectile;

        if (normalized == "controlledsalvo" || normalized == "kontrolliertesalve")
            return BasicTowerKeystone.ControlledSalvo;

        if (normalized == "foundation" || normalized == "grundpfeiler")
            return BasicTowerKeystone.Foundation;

        return fallback;
    }

    private int CountEnemiesInRange(Tower tower)
    {
        if (tower == null)
            return 0;

        float range = Mathf.Max(0f, tower.GetEffectiveRange());
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

    private Enemy FindStrongestEnemyInRange(Tower tower)
    {
        if (tower == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy best = null;
        int bestHealth = -1;
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || Vector3.Distance(tower.transform.position, enemy.transform.position) > range)
                continue;

            int health = enemy.GetCurrentHealth();
            if (health > bestHealth)
            {
                bestHealth = health;
                best = enemy;
            }
        }

        return best;
    }

    private Enemy FindEmergencyTarget(Tower tower)
    {
        if (tower == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy best = null;
        float bestProgress = 0.70f;
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

    private Enemy FindSecondTargetInRange(Tower tower, Enemy excluded)
    {
        if (tower == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy best = null;
        float bestProgress = -Mathf.Infinity;
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy == excluded)
                continue;

            if (Vector3.Distance(tower.transform.position, enemy.transform.position) > range)
                continue;

            float progress = enemy.GetPathProgress();
            if (progress > bestProgress)
            {
                bestProgress = progress;
                best = enemy;
            }
        }

        return best;
    }

    private bool IsHardTarget(Enemy enemy)
    {
        if (enemy == null)
            return false;

        return enemy.enemyRole == EnemyRole.Knight ||
               enemy.enemyRole == EnemyRole.Tank ||
               enemy.enemyRole == EnemyRole.MiniBoss ||
               enemy.enemyRole == EnemyRole.Boss ||
               enemy.isMiniBoss ||
               enemy.isBoss ||
               enemy.isElite;
    }

    private bool IsTowerNearBase(Tower tower)
    {
        if (tower == null)
            return false;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        TileManager tileManager = gameManager != null ? gameManager.tileManager : FindObjectOfType<TileManager>();

        if (tileManager == null)
            return false;

        Vector2Int towerPosition = tower.hasBuildGridPosition ? tower.builtGridPosition : tileManager.WorldToGridPublic(tower.transform.position);
        Vector2Int basePosition = tileManager.GetBasePosition();
        int distance = Mathf.Abs(towerPosition.x - basePosition.x) + Mathf.Abs(towerPosition.y - basePosition.y);
        return distance <= 3;
    }

    private int GetArmorSightInterval()
    {
        int rank = GetNodeRank(ArmorSight);

        if (rank <= 0)
            return 0;

        return Mathf.Clamp(11 - rank, 8, 10);
    }

    private int GetFocusShotInterval()
    {
        if (GetNodeRank(FocusShot) <= 0)
            return 0;

        int training = GetNodeRank(FocusTraining);
        return Mathf.Clamp(8 - training, 6, 8);
    }

    private float GetDoubleTapChance()
    {
        if (GetNodeRank(DoubleTap) <= 0)
            return 0f;

        return Mathf.Clamp01(0.05f + GetNodeRank(DoubleTapTraining) * 0.05f);
    }

    private string GetKeystoneNodeId(BasicTowerKeystone keystone)
    {
        switch (keystone)
        {
            case BasicTowerKeystone.AnchorProjectile: return AnchorProjectile;
            case BasicTowerKeystone.ControlledSalvo: return ControlledSalvo;
            case BasicTowerKeystone.Foundation: return Foundation;
            default: return "";
        }
    }

    private string GetMilestoneProgressText(BasicTowerMasteryMilestone milestone)
    {
        bool unlocked = IsMilestoneUnlocked(milestone);
        string state = unlocked ? "frei" : "gesperrt";
        int masteryLevel = GetMasteryLevel();

        switch (milestone)
        {
            case BasicTowerMasteryMilestone.BasicI:
                return state + " | Punkte " + Mathf.Min(spentPoints, 10) + "/10 | Mastery Lv " + Mathf.Min(masteryLevel, 3) + "/3 | Level 10 " + (reachedBasicLevel10 ? "ja" : "nein");
            case BasicTowerMasteryMilestone.BasicII:
                return state + " | Punkte " + Mathf.Min(spentPoints, 25) + "/25 | Mastery Lv " + Mathf.Min(masteryLevel, 8) + "/8 | Level 20 " + (reachedBasicLevel20 ? "ja" : "nein");
            case BasicTowerMasteryMilestone.BasicIII:
                return state + " | Punkte " + Mathf.Min(spentPoints, 50) + "/50 | Mastery Lv " + Mathf.Min(masteryLevel, 15) + "/15 | Boss/Basisleistung " + (bossKillWithBasic ? "ja" : "nein");
            case BasicTowerMasteryMilestone.BasicIV:
                return state + " | Punkte " + Mathf.Min(spentPoints, 85) + "/85 | Mastery Lv " + Mathf.Min(masteryLevel, 25) + "/25 | Chaos 3 " + (chaos3WaveWithBasic ? "ja" : "nein");
            case BasicTowerMasteryMilestone.BasicV:
                return state + " | Punkte " + Mathf.Min(spentPoints, 130) + "/130 | Mastery Lv " + Mathf.Min(masteryLevel, 40) + "/40 | Chaos 5/Elite " + (chaos5BossOrEliteWithBasic ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(SolidCore, "Solider Kern", BasicTowerMasteryPath.Trunk, BasicTowerMasteryMilestone.None, 5, "Basic erhaelt +0,5 Basisschaden pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(CleanMechanics, "Saubere Mechanik", BasicTowerMasteryPath.Trunk, BasicTowerMasteryMilestone.None, 5, "Basic erhaelt +0,05 Fire Rate pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(MeasuredLine, "Vermessene Linie", BasicTowerMasteryPath.Trunk, BasicTowerMasteryMilestone.None, 3, "Basic erhaelt +0,15 Range pro Rang.", 1, 2, 3);
        AddDefinition(RoutineFire, "Routinefeuer", BasicTowerMasteryPath.Trunk, BasicTowerMasteryMilestone.None, 3, "Basic Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);
        AddDefinition(BasicTraining, "Grundausbildung", BasicTowerMasteryPath.Trunk, BasicTowerMasteryMilestone.None, 3, "Der erste Basic Tower pro Run startet mit +5 XP pro Rang.", 1, 2, 3);

        AddDefinition(KnightReader, "Knight-Leser", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicI, 5, "+3% Damage gegen Knights pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(TankReader, "Tank-Leser", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicI, 5, "+3% Damage gegen Tanks pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ArmorSight, "Ruestungsblick", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicII, 3, "Jeder 10./9./8. Basic-Schuss ignoriert 1 Armor.", 3, 4, 5);
        AddDefinition(FocusShot, "Fokusschuss", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicII, 1, "Jeder 8. Schuss gegen das staerkste Ziel verursacht +35% Damage.", 6);
        AddDefinition(FocusTraining, "Fokusschuss-Training", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicIII, 2, "Fokusschuss-Cooldown sinkt auf jeden 7./6. Schuss.", new int[] { 6, 8 }, BasicTowerKeystone.None, FocusShot);
        AddDefinition(MiniBossRoutine, "MiniBoss-Routine", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicIII, 3, "+5% Damage gegen MiniBoss pro Rang.", 4, 5, 6);
        AddDefinition(BossRoutine, "Boss-Routine", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicIV, 3, "+4% Damage gegen Boss pro Rang.", 5, 6, 7);
        AddDefinition(ChaosKnightAnalysis, "Chaos-Knight-Analyse", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicIV, 3, "+4% Damage gegen Chaos-Knights pro Rang.", 5, 7, 9);
        AddDefinition(AnchorProjectile, "Keystone: Ankerprojektil", BasicTowerMasteryPath.Precision, BasicTowerMasteryMilestone.BasicV, 1, "Fokusschuss markiert harte Ziele kurz. Basic Tower verursachen gegen markierte Ziele +10% Damage.", new int[] { 20 }, BasicTowerKeystone.AnchorProjectile);

        AddDefinition(FastBolt, "Schneller Verschluss", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicI, 5, "+0,04 Fire Rate pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(RunnerReader, "Runner-Leser", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicI, 5, "+3% Damage gegen Runner pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(CleanRetarget, "Sauberer Zielwechsel", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicII, 3, "Nach einem Kill wird der naechste Schuss 10%/15%/20% schneller bereit.", 3, 4, 5);
        AddDefinition(DoubleTap, "Doppeltipp", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicII, 1, "5% Chance auf einen zweiten schwaecheren Schuss.", 6);
        AddDefinition(DoubleTapTraining, "Doppeltipp-Training", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicIII, 2, "Doppeltipp-Chance +5% pro Rang, maximal 15%.", new int[] { 6, 8 }, BasicTowerKeystone.None, DoubleTap);
        AddDefinition(MassRoutine, "Massenroutine", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicIII, 3, "Bei 5+ Gegnern in Range: +3% Fire Rate pro Rang.", 4, 5, 6);
        AddDefinition(RiftSalvo, "Risssalve", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicIV, 1, "Gegen Chaos-Varianten verursacht der Doppeltipp-Schuss +20% Schaden.", 8);
        AddDefinition(ReloadWindow, "Nachladefenster", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicIV, 1, "Nach 10 Schuessen ohne Kill erhaelt der naechste Schuss +50% Damage.", 10);
        AddDefinition(ControlledSalvo, "Keystone: Kontrollierte Salve", BasicTowerMasteryPath.Salvo, BasicTowerMasteryMilestone.BasicV, 1, "Jeder 10. Schuss feuert ein Zusatzprojektil auf ein zweites Ziel in Range, 60% Damage.", new int[] { 20 }, BasicTowerKeystone.ControlledSalvo);

        AddDefinition(SimpleBlueprint, "Einfacher Bauplan", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicI, 5, "Basic Tower kostet -1 Gold pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(SafeRefund, "Sichere Rueckgabe", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicI, 5, "Verkauf bleibt fix bei 50% des damaligen Kaufpreises.", 1, 2, 3, 4, 5);
        AddDefinition(TrainingRoutine, "Trainingsroutine", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicII, 5, "Basic Tower bekommen +4% XP pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FirstAnchor, "Erster Anker", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicII, 1, "Der erste Basic Tower pro Run startet mit +15 XP.", 5);
        AddDefinition(FirstAnchorTwo, "Erster Anker II", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicIII, 1, "Der erste Basic Tower pro Run startet zusaetzlich auf Level 2.", new int[] { 8 }, BasicTowerKeystone.None, FirstAnchor);
        AddDefinition(SteadyHand, "Ruhige Hand", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicIII, 1, "Wenn Lives Schaden nehmen, erhalten Basic Tower 5s lang +10% Fire Rate.", 8);
        AddDefinition(StabilizedLine, "Stabilisierte Linie", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicIV, 1, "Basic Tower nahe der Base erhalten +5% Range.", 8);
        AddDefinition(EmergencyFocus, "Notfallfokus", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicIV, 1, "Wenn ein Gegner 70% des Weges erreicht, priorisieren Basic Tower ihn kurz besser.", 10);
        AddDefinition(Foundation, "Keystone: Grundpfeiler", BasicTowerMasteryPath.Stability, BasicTowerMasteryMilestone.BasicV, 1, "Der erste Basic Tower pro Run startet Level 3 und erhaelt beim ersten Bosskill +1 Upgrade Point.", new int[] { 22 }, BasicTowerKeystone.Foundation);
    }

    private void AddDefinition(string nodeId, string displayName, BasicTowerMasteryPath path, BasicTowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, BasicTowerKeystone.None, "");
    }

    private void AddDefinition(string nodeId, string displayName, BasicTowerMasteryPath path, BasicTowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, BasicTowerKeystone keystone, string requiredNodeId = "")
    {
        BasicTowerMasteryNodeDefinition definition = new BasicTowerMasteryNodeDefinition(nodeId, displayName, path, gate, maxRank, costs, effectText, keystone, requiredNodeId);
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
            nodeStates = new List<BasicTowerMasteryNodeState>();
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        EnsureNodeStates();

        foreach (BasicTowerMasteryNodeState state in nodeStates)
        {
            if (state == null || state.nodeId != nodeId)
                continue;

            state.rank = Mathf.Max(0, rank);
            return;
        }

        nodeStates.Add(new BasicTowerMasteryNodeState
        {
            nodeId = nodeId,
            rank = Mathf.Max(0, rank)
        });
    }

    private void LoadProfile()
    {
        EnsureDefinitions();
        EnsureNodeStates();

        masteryXP = PlayerPrefs.GetInt(PlayerPrefsPrefix + "XP", 0);
        unspentPoints = PlayerPrefs.GetInt(PlayerPrefsPrefix + "UnspentPoints", 0);
        spentPoints = PlayerPrefs.GetInt(PlayerPrefsPrefix + "SpentPoints", 0);
        bestBasicLevelEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "BestLevel", 1);
        reachedBasicLevel10 = PlayerPrefs.GetInt(PlayerPrefsPrefix + "ReachedLevel10", 0) == 1;
        reachedBasicLevel20 = PlayerPrefs.GetInt(PlayerPrefsPrefix + "ReachedLevel20", 0) == 1;
        bossKillWithBasic = PlayerPrefs.GetInt(PlayerPrefsPrefix + "BossBasic", 0) == 1;
        chaos3WaveWithBasic = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Chaos3Basic", 0) == 1;
        chaos5BossOrEliteWithBasic = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Chaos5Basic", 0) == 1;
        activeKeystone = (BasicTowerKeystone)PlayerPrefs.GetInt(PlayerPrefsPrefix + "ActiveKeystone", 0);

        nodeStates.Clear();

        foreach (BasicTowerMasteryNodeDefinition definition in definitions)
        {
            int rank = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, 0);

            if (rank > 0)
                SetNodeRank(definition.nodeId, Mathf.Min(rank, definition.maxRank));
        }
    }

    private void SaveProfile()
    {
        if (IsMetaProgressionSuppressedForCurrentRun())
            return;

        EnsureDefinitions();
        EnsureNodeStates();

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "XP", Mathf.Max(0, masteryXP));
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "UnspentPoints", Mathf.Max(0, unspentPoints));
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "SpentPoints", Mathf.Max(0, spentPoints));
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "BestLevel", Mathf.Max(1, bestBasicLevelEver));
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "ReachedLevel10", reachedBasicLevel10 ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "ReachedLevel20", reachedBasicLevel20 ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "BossBasic", bossKillWithBasic ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "Chaos3Basic", chaos3WaveWithBasic ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "Chaos5Basic", chaos5BossOrEliteWithBasic ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "ActiveKeystone", (int)activeKeystone);

        foreach (BasicTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.Save();
        SyncGlobalTowerMasteryProfile();
    }

    private void SyncGlobalTowerMasteryProfile()
    {
        TowerMasteryManager towerMastery = TowerMasteryManager.GetOrCreate(gameManager);

        if (towerMastery == null)
            return;

        towerMastery.SynchronizeRoleProfile(
            TowerRole.Basic,
            masteryXP,
            unspentPoints,
            spentPoints,
            bestBasicLevelEver,
            reachedBasicLevel10,
            reachedBasicLevel20,
            bossKillWithBasic,
            chaos3WaveWithBasic,
            chaos5BossOrEliteWithBasic,
            GetKeystoneDisplayName(activeKeystone),
            lastRunMasteryXPGained,
            lastRunMasteryPointsGained);
    }
}
