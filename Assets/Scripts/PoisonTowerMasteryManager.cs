using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum PoisonTowerMasteryPath
{
    Trunk,
    DeepPoison,
    ArmorDissolution,
    PlagueChain
}

public enum PoisonTowerKeystone
{
    None,
    ToxicCore,
    Corrosion,
    PlagueJump
}

[System.Serializable]
public class PoisonTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class PoisonTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public PoisonTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public PoisonTowerKeystone keystone;
    public string requiredNodeId;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public PoisonTowerMasteryNodeDefinition(string nodeId, string displayName, PoisonTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, int[] rankCosts, string effectText, PoisonTowerKeystone keystone = PoisonTowerKeystone.None, string requiredNodeId = "")
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

public struct PoisonTowerMasteryShotContext
{
    public Enemy primaryTarget;
}

public class PoisonTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_PoisonMastery_";

    public const string PureToxin = "pure_toxin";
    public const string LongerEffect = "longer_effect";
    public const string FineDosage = "fine_dosage";
    public const string ToxicRange = "toxic_range";
    public const string LabRoutine = "lab_routine";

    public const string ConcentratedPoison = "concentrated_poison";
    public const string DeepDuration = "deep_duration";
    public const string RePoisoning = "re_poisoning";
    public const string ToxinBinding = "toxin_binding";
    public const string DeepReaction = "deep_reaction";
    public const string MiniBossToxin = "miniboss_toxin";
    public const string BossToxin = "boss_toxin";
    public const string ToughWaveFormula = "tough_wave_formula";
    public const string ToxicCore = "toxic_core";

    public const string TankDissolution = "tank_dissolution";
    public const string KnightDissolution = "knight_dissolution";
    public const string CorrosiveFormula = "corrosive_formula";
    public const string ArmorAnalysis = "armor_analysis";
    public const string TimeArmorBreak = "time_armor_break";
    public const string AllRounderSolution = "allrounder_solution";
    public const string ChaosKnightFormula = "chaos_knight_formula";
    public const string ChaosArmorKnowledge = "chaos_armor_knowledge";
    public const string Corrosion = "corrosion";

    public const string InfectionTraces = "infection_traces";
    public const string SlowSpread = "slow_spread";
    public const string PlagueNest = "plague_nest";
    public const string ControlledInfection = "controlled_infection";
    public const string ToughPoisonField = "tough_poison_field";
    public const string PurplePlague = "purple_plague";
    public const string ResistanceProbe = "resistance_probe";
    public const string LearnerCounterSample = "learner_counter_sample";
    public const string PlagueJump = "plague_jump";

    public static PoisonTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<PoisonTowerMasteryNodeState> nodeStates = new List<PoisonTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public float directDamageToMasteryXPRatio = 0.01f;
    public int maxDirectDamageMasteryXPPerWave = 8;
    public float poisonDamageToMasteryXPRatio = 0.07f;
    public int maxPoisonDamageMasteryXPPerWave = 52;
    public int poisonedDefeatXP = 4;
    public int poisonedTankKnightDefeatXP = 7;
    public int chaosPoisonBonusXP = 2;
    public int maxChaosPoisonBonusXPPerWave = 18;
    public int miniBossPoisonBonusXP = 12;
    public int bossPoisonBonusXP = 20;
    public int toughnessArmorWaveBonusXP = 18;

    [Header("Milestone Tracking")]
    public int poisonedTankKnightKillsForMasteryGate = 60;
    public int totalPoisonedTankKnightKills = 0;

    [Header("Runtime")]
    public float plagueJumpCooldown = 1.25f;
    public float toxicCoreWarmupSeconds = 4f;
    public float corrosionWarmupSeconds = 5f;

    private readonly List<PoisonTowerMasteryNodeDefinition> definitions = new List<PoisonTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, PoisonTowerMasteryNodeDefinition> definitionById = new Dictionary<string, PoisonTowerMasteryNodeDefinition>();
    private readonly HashSet<int> poisonedEnemiesThisWave = new HashSet<int>();
    private readonly Dictionary<int, float> poisonStartedAtByEnemyId = new Dictionary<int, float>();
    private readonly Dictionary<int, float> poisonTowerSpreadCooldownUntil = new Dictionary<int, float>();
    private readonly Dictionary<int, int> toxicCoreTargetByTowerId = new Dictionary<int, int>();

    private int currentWaveNumber = 0;
    private bool currentWaveHadPoisonContribution = false;
    private bool currentWaveHasToughness = false;
    private bool currentWaveHasArmor = false;
    private bool currentWaveHasResistance = false;
    private bool currentWaveHasChaosVariantGroup = false;
    private float currentWaveDirectMasteryXP = 0f;
    private float directMasteryXPFraction = 0f;
    private float currentWavePoisonMasteryXP = 0f;
    private float poisonMasteryXPFraction = 0f;
    private int currentWaveChaosPoisonBonusXP = 0;

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

    public static PoisonTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        PoisonTowerMasteryManager existing = FindObjectOfType<PoisonTowerMasteryManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("PoisonTowerMasterySystem");
        PoisonTowerMasteryManager manager = systemObject.AddComponent<PoisonTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public static bool TryGetActive(out PoisonTowerMasteryManager manager)
    {
        manager = Instance != null ? Instance : FindObjectOfType<PoisonTowerMasteryManager>();
        return manager != null;
    }

    public IReadOnlyList<PoisonTowerMasteryNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public PoisonTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();
        return !string.IsNullOrEmpty(nodeId) && definitionById.TryGetValue(nodeId, out PoisonTowerMasteryNodeDefinition definition) ? definition : null;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        EnsureNodeStates();

        foreach (PoisonTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Poison, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        PoisonTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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

        TowerMasteryRoleProfile profile = GetPoisonProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        PoisonTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Poison, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != PoisonTowerKeystone.None && GetActiveKeystone() == PoisonTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(PoisonTowerKeystone keystone)
    {
        if (keystone == PoisonTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(PoisonTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Poison, keystone.ToString());
    }

    public PoisonTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetPoisonProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return PoisonTowerKeystone.None;

        try
        {
            return (PoisonTowerKeystone)System.Enum.Parse(typeof(PoisonTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return PoisonTowerKeystone.None;
        }
    }

    public string GetNodeStateText(PoisonTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != PoisonTowerKeystone.None && GetActiveKeystone() == definition.keystone)
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
        TowerMasteryRoleProfile profile = GetPoisonProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetPoisonProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Poison Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Poison im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Poison) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine("Vergiftete Tank/Knight-Kills fuer Poison III: " + Mathf.Min(totalPoisonedTankKnightKills, Mathf.Max(1, poisonedTankKnightKillsForMasteryGate)) + " / " + Mathf.Max(1, poisonedTankKnightKillsForMasteryGate));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Poison I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Poison II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Poison III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Poison IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Poison V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Poison Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetPoisonProfile();

        if (profile == null)
            return "Poison Mastery: vorbereitet";

        return "Poison Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        PoisonTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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
            PoisonTowerMasteryNodeDefinition required = GetDefinition(definition.requiredNodeId);
            text += "Voraussetzung: " + (required != null ? required.displayName : definition.requiredNodeId) + "\n";
        }

        text += "\n" + definition.effectText;

        if (definition.keystone != PoisonTowerKeystone.None)
            text += "\n\nKeystone-Regel: Pro Tower-Typ ist nur ein Keystone aktiv. Wechsel wirken fuer neue Runs.";

        return text;
    }

    public void StartNewRun()
    {
        poisonedEnemiesThisWave.Clear();
        poisonStartedAtByEnemyId.Clear();
        poisonTowerSpreadCooldownUntil.Clear();
        toxicCoreTargetByTowerId.Clear();
        currentWaveNumber = 0;
        currentWaveHadPoisonContribution = false;
        currentWaveHasToughness = false;
        currentWaveHasArmor = false;
        currentWaveHasResistance = false;
        currentWaveHasChaosVariantGroup = false;
        currentWaveDirectMasteryXP = 0f;
        directMasteryXPFraction = 0f;
        currentWavePoisonMasteryXP = 0f;
        poisonMasteryXPFraction = 0f;
        currentWaveChaosPoisonBonusXP = 0;
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsPoisonTower(tower))
            return;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.RecordTowerLevelReached(tower, tower.level);
    }

    public void RecordPoisonDirectDamage(Tower tower, Enemy target, float appliedDamage)
    {
        if (!IsPoisonTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadPoisonContribution = true;
        AddCappedXP(appliedDamage, directDamageToMasteryXPRatio, ref directMasteryXPFraction, ref currentWaveDirectMasteryXP, maxDirectDamageMasteryXPPerWave);
    }

    public void RecordPoisonDamage(Tower tower, Enemy target, float appliedDamage)
    {
        if (!IsPoisonTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadPoisonContribution = true;
        AddCappedXP(appliedDamage, poisonDamageToMasteryXPRatio, ref poisonMasteryXPFraction, ref currentWavePoisonMasteryXP, maxPoisonDamageMasteryXPPerWave);
    }

    public void RecordPoisonApplied(Tower tower, Enemy target)
    {
        if (!IsPoisonTower(tower) || target == null)
            return;

        currentWaveHadPoisonContribution = true;
        int enemyId = target.GetInstanceID();

        if (!poisonStartedAtByEnemyId.ContainsKey(enemyId))
            poisonStartedAtByEnemyId[enemyId] = Time.time;

        poisonedEnemiesThisWave.Add(enemyId);

        if (target.IsChaosVariant() && currentWaveChaosPoisonBonusXP < maxChaosPoisonBonusXPPerWave)
        {
            int award = Mathf.Min(Mathf.Max(0, chaosPoisonBonusXP), Mathf.Max(0, maxChaosPoisonBonusXPPerWave - currentWaveChaosPoisonBonusXP));
            currentWaveChaosPoisonBonusXP += award;
            AddPoisonMasteryXP(award);
        }
    }

    public void RecordPoisonKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsPoisonTower(tower))
            return;

        currentWaveHadPoisonContribution = true;
    }

    public void RecordPoisonAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsPoisonTower(tower))
            return;

        currentWaveHadPoisonContribution = true;
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower poisonContributor = FindPoisonContributor(killingTower, contributors);

        if (poisonContributor == null)
            return;

        currentWaveHadPoisonContribution = true;
        bool poisonedDefeat = enemy.HasPoison() || enemy.WasRecentlyPoisonedByPoisonTower() || IsPoisonTower(killingTower);

        if (poisonedDefeat)
            AddPoisonMasteryXP(poisonedDefeatXP);

        if (poisonedDefeat && (enemy.enemyRole == EnemyRole.Tank || enemy.enemyRole == EnemyRole.Knight))
        {
            AddPoisonMasteryXP(poisonedTankKnightDefeatXP);
            totalPoisonedTankKnightKills++;
            UnlockPoisonMasteryGateIIIIfReady(false);
            SaveProfile();
        }

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
        {
            AddPoisonMasteryXP(miniBossPoisonBonusXP);
            UnlockPoisonMasteryGateIIIIfReady(true);
        }

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
        {
            AddPoisonMasteryXP(bossPoisonBonusXP);
            UnlockPoisonMasteryGateIIIIfReady(true);
        }

        TrySpreadPoisonOnDeath(enemy, poisonContributor, poisonedDefeat);
    }

    public PoisonTowerMasteryShotContext PreparePoisonShot(Tower tower, Enemy currentTarget)
    {
        PoisonTowerMasteryShotContext context = new PoisonTowerMasteryShotContext
        {
            primaryTarget = currentTarget
        };

        if (!IsPoisonTower(tower))
            return context;

        if (GetNodeRank(ArmorAnalysis) > 0)
        {
            Enemy armorTarget = FindArmorPriorityTarget(tower, currentTarget);

            if (armorTarget != null)
                context.primaryTarget = armorTarget;
        }

        return context;
    }

    public int CalculatePoisonShotDamage(Tower tower, Enemy target, int baseDamage)
    {
        float damageValue = Mathf.Max(0f, baseDamage);

        if (!IsPoisonTower(tower))
            return Mathf.Max(0, Mathf.RoundToInt(damageValue));

        if (target != null && target.enemyRole == EnemyRole.Learner)
            damageValue *= 1f + GetNodeRank(LearnerCounterSample) * 0.04f;

        if (target != null && target.HasPoisonCorrosion() && GetNodeRank(TimeArmorBreak) > 0)
            damageValue *= 1.10f;

        return Mathf.Max(0, Mathf.RoundToInt(damageValue));
    }

    public float GetPoisonRangeBonus()
    {
        return GetNodeRank(ToxicRange) * 0.12f;
    }

    public float GetPoisonXPMultiplier()
    {
        return 1f + GetNodeRank(LabRoutine) * 0.03f;
    }

    public float GetPoisonFireRateAdditive()
    {
        return GetNodeRank(FineDosage) * 0.03f;
    }

    public float GetPoisonFireRateMultiplier(Tower tower)
    {
        if (!IsPoisonTower(tower))
            return 1f;

        float multiplier = 1f;

        if (GetNodeRank(PlagueNest) > 0 && CountPoisonedEnemies() >= 3)
            multiplier += 0.05f;

        return multiplier;
    }

    public float GetModifiedPoisonDamage(Tower tower, Enemy target, float basePoisonDamage)
    {
        if (!IsPoisonTower(tower))
            return Mathf.Max(0.1f, basePoisonDamage);

        float damage = Mathf.Max(0.1f, basePoisonDamage);
        damage += GetNodeRank(PureToxin) * 0.30f;
        damage += GetNodeRank(ConcentratedPoison) * 0.40f;

        if (target != null)
        {
            if (target.enemyRole == EnemyRole.Tank)
                damage *= 1f + GetNodeRank(TankDissolution) * 0.04f;

            if (target.enemyRole == EnemyRole.Knight)
                damage *= 1f + GetNodeRank(KnightDissolution) * 0.04f;

            if (target.enemyRole == EnemyRole.AllRounder)
                damage *= 1f + GetNodeRank(AllRounderSolution) * 0.05f;

            if (target.enemyRole == EnemyRole.MiniBoss || target.isMiniBoss)
                damage *= 1f + GetNodeRank(MiniBossToxin) * 0.05f;

            if (target.enemyRole == EnemyRole.Boss || target.isBoss)
                damage *= 1f + GetNodeRank(BossToxin) * 0.04f;

            if (target.IsChaosVariant() && target.enemyRole == EnemyRole.Knight)
                damage *= 1f + GetNodeRank(ChaosKnightFormula) * 0.05f;

            if (target.GetArmor() > 0 && GetNodeRank(CorrosiveFormula) > 0)
                damage *= 1.05f;

            if (currentWaveHasArmor && target.GetArmor() > 0 && GetNodeRank(ChaosArmorKnowledge) > 0 && !target.IsBossOrMiniBossTarget())
                damage *= 1.08f;
        }

        return Mathf.Max(0.1f, damage);
    }

    public float GetModifiedPoisonDuration(Tower tower, Enemy target, float baseDuration, bool transferred = false)
    {
        if (!IsPoisonTower(tower))
            return Mathf.Max(0.1f, baseDuration);

        float duration = Mathf.Max(0.1f, baseDuration);
        duration += GetNodeRank(LongerEffect) * 0.25f;
        duration += GetNodeRank(DeepDuration) * 0.30f;

        if (target != null && target.HasPoison() && GetNodeRank(RePoisoning) > 0)
            duration += 0.50f;

        if (currentWaveHasToughness && GetNodeRank(ToughWaveFormula) > 0)
            duration += 0.50f;

        if (transferred)
            duration = duration * 0.40f + GetNodeRank(SlowSpread) * 0.20f;

        return Mathf.Max(0.1f, duration);
    }

    public float ModifyPoisonTickDamage(Tower tower, Enemy target, float tickDamage)
    {
        if (!IsPoisonTower(tower) || target == null || tickDamage <= 0f)
            return tickDamage;

        float modified = tickDamage;
        bool longEnoughForToxin = IsPoisonedLongEnough(target, GetToxinBindingWarmupSeconds());
        bool longEnoughForCorrosion = IsPoisonedLongEnough(target, corrosionWarmupSeconds);

        if (longEnoughForToxin && GetNodeRank(ToxinBinding) > 0)
            modified *= 1.10f;

        if (IsToxicCoreActiveForTower(tower, target))
            modified *= 1.30f;

        if (target.GetArmor() > 0 && longEnoughForCorrosion && (GetNodeRank(TimeArmorBreak) > 0 || GetActiveKeystone() == PoisonTowerKeystone.Corrosion))
            target.ApplyPoisonCorrosion(2f, tower);

        if (target.GetArmor() > 0 && target.HasPoisonCorrosion() && GetActiveKeystone() == PoisonTowerKeystone.Corrosion)
            modified *= 1.20f;

        if (currentWaveHasResistance && GetNodeRank(ResistanceProbe) > 0)
            modified *= 1.10f;

        return modified;
    }

    public float ModifyPoisonChaosLearnerDamage(Tower tower, Enemy target, float rawTickDamage, float reducedDamage)
    {
        return reducedDamage;
    }

    public float ModifyPoisonChaosLearnerHeal(Tower tower, float rawHeal)
    {
        return rawHeal;
    }

    public string GetPathDisplayName(PoisonTowerMasteryPath path)
    {
        switch (path)
        {
            case PoisonTowerMasteryPath.DeepPoison: return "Tiefengift";
            case PoisonTowerMasteryPath.ArmorDissolution: return "Panzerzersetzung";
            case PoisonTowerMasteryPath.PlagueChain: return "Seuchenkette";
            default: return "Einstieg";
        }
    }

    public string GetMilestoneDisplayName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return "Poison I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Poison II: Toxinlehre";
            case TowerMasteryMilestone.III: return "Poison III: Zersetzungsmeisterschaft";
            case TowerMasteryMilestone.IV: return "Poison IV: Rissgift";
            case TowerMasteryMilestone.V: return "Poison V: Toxinkern";
            default: return "Offen";
        }
    }

    public string GetKeystoneDisplayName(PoisonTowerKeystone keystone)
    {
        switch (keystone)
        {
            case PoisonTowerKeystone.ToxicCore: return "Toxischer Kern";
            case PoisonTowerKeystone.Corrosion: return "Korrosion";
            case PoisonTowerKeystone.PlagueJump: return "Seuchensprung";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        poisonedEnemiesThisWave.Clear();
        poisonStartedAtByEnemyId.Clear();
        currentWaveHadPoisonContribution = false;
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        currentWaveDirectMasteryXP = 0f;
        directMasteryXPFraction = 0f;
        currentWavePoisonMasteryXP = 0f;
        poisonMasteryXPFraction = 0f;
        currentWaveChaosPoisonBonusXP = 0;
        currentWaveHasToughness = WaveHasBlock(waveData, ChaosWaveBlockType.Toughness);
        currentWaveHasArmor = WaveHasBlock(waveData, ChaosWaveBlockType.Armor);
        currentWaveHasResistance = WaveHasBlock(waveData, ChaosWaveBlockType.Resistance);
        currentWaveHasChaosVariantGroup = WaveHasBlock(waveData, ChaosWaveBlockType.ChaosVariantGroup);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadPoisonContribution)
            return;

        if ((currentWaveHasToughness || currentWaveHasArmor) && result.chaosLevelAtWaveStart > 0)
            AddPoisonMasteryXP(toughnessArmorWaveBonusXP);
    }

    private Enemy FindArmorPriorityTarget(Tower tower, Enemy currentTarget)
    {
        if (!IsPoisonTower(tower))
            return currentTarget;

        var enemies = EnemyRegistry.ActiveEnemies;
        Enemy best = null;
        float bestProgress = -Mathf.Infinity;
        float range = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.GetArmor() <= 0 || enemy.HasPoison())
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

    private void TrySpreadPoisonOnDeath(Enemy deadEnemy, Tower poisonTower, bool poisonKilled)
    {
        if (deadEnemy == null || !IsPoisonTower(poisonTower))
            return;

        if (GetNodeRank(InfectionTraces) <= 0 && GetActiveKeystone() != PoisonTowerKeystone.PlagueJump)
            return;

        if (!CanUseSpreadCooldown(poisonTower))
            return;

        float chance = GetSpreadChance(deadEnemy, poisonKilled);

        if (chance <= 0f || Random.value > chance)
            return;

        Enemy target = FindNearbyPoisonTarget(deadEnemy, 1.75f);

        if (target == null)
            return;

        float damage = GetModifiedPoisonDamage(poisonTower, target, Mathf.Max(1f, poisonTower.poisonDamage));
        float duration = GetModifiedPoisonDuration(poisonTower, target, Mathf.Max(0.5f, poisonTower.poisonDuration), true);
        target.ApplyPoison(damage, duration, poisonTower);
        SetSpreadCooldown(poisonTower);
    }

    private float GetSpreadChance(Enemy deadEnemy, bool poisonKilled)
    {
        int infectionRank = GetNodeRank(InfectionTraces);
        bool standardDeath = deadEnemy != null && deadEnemy.enemyRole == EnemyRole.Standard;
        float chance = standardDeath ? infectionRank * 0.05f : 0f;

        if (currentWaveHasToughness && infectionRank > 0 && GetNodeRank(ToughPoisonField) > 0)
            chance += 0.10f;

        if (currentWaveHasChaosVariantGroup && GetNodeRank(PurplePlague) > 0)
            chance = Mathf.Max(chance, 0.12f);

        if (!poisonKilled && deadEnemy != null && deadEnemy.IsChaosVariant() && GetNodeRank(PurplePlague) > 0)
            chance = Mathf.Max(chance, 0.12f);

        if (GetActiveKeystone() == PoisonTowerKeystone.PlagueJump)
            chance = Mathf.Max(chance, 1f);

        return Mathf.Clamp01(chance);
    }

    private Enemy FindNearbyPoisonTarget(Enemy deadEnemy, float radius)
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        Enemy best = null;
        float bestDistance = Mathf.Max(0.1f, radius);
        bool requireCleanTarget = GetActiveKeystone() == PoisonTowerKeystone.PlagueJump || GetNodeRank(ControlledInfection) > 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy == deadEnemy)
                continue;

            if (requireCleanTarget && enemy.HasPoison())
                continue;

            float distance = Vector3.Distance(deadEnemy.transform.position, enemy.transform.position);

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }

        return best;
    }

    private bool CanUseSpreadCooldown(Tower poisonTower)
    {
        int key = poisonTower != null ? poisonTower.GetInstanceID() : 0;
        return !poisonTowerSpreadCooldownUntil.TryGetValue(key, out float untilTime) || Time.time >= untilTime;
    }

    private void SetSpreadCooldown(Tower poisonTower)
    {
        int key = poisonTower != null ? poisonTower.GetInstanceID() : 0;
        poisonTowerSpreadCooldownUntil[key] = Time.time + Mathf.Max(0.1f, plagueJumpCooldown);
    }

    private bool IsPoisonedLongEnough(Enemy target, float seconds)
    {
        if (target == null)
            return false;

        int targetId = target.GetInstanceID();
        return poisonStartedAtByEnemyId.TryGetValue(targetId, out float startedAt) && Time.time - startedAt >= Mathf.Max(0f, seconds);
    }

    private bool IsToxicCoreActiveForTower(Tower tower, Enemy target)
    {
        if (GetActiveKeystone() != PoisonTowerKeystone.ToxicCore || !IsPoisonTower(tower) || target == null)
            return false;

        if (!IsPoisonedLongEnough(target, toxicCoreWarmupSeconds))
            return false;

        int towerId = tower.GetInstanceID();
        int targetId = target.GetInstanceID();

        if (!toxicCoreTargetByTowerId.TryGetValue(towerId, out int activeTargetId) || activeTargetId == 0)
            toxicCoreTargetByTowerId[towerId] = targetId;

        return toxicCoreTargetByTowerId.TryGetValue(towerId, out activeTargetId) && activeTargetId == targetId;
    }

    private float GetToxinBindingWarmupSeconds()
    {
        return Mathf.Max(1f, 4f - GetNodeRank(DeepReaction) * 0.5f);
    }

    private int CountPoisonedEnemies()
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        int count = 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && enemy.HasPoison())
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

    private Tower FindPoisonContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsPoisonTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsPoisonTower(tower))
                return tower;
        }

        return null;
    }

    private bool IsPoisonTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Poison;
    }

    private void AddCappedXP(float value, float ratio, ref float fraction, ref float currentWaveXP, int cap)
    {
        if (currentWaveXP >= cap)
            return;

        fraction += Mathf.Max(0f, value) * Mathf.Max(0f, ratio);
        int wholeXP = Mathf.FloorToInt(fraction);

        if (wholeXP <= 0)
            return;

        int remainingCap = Mathf.Max(0, cap - Mathf.FloorToInt(currentWaveXP));
        int awarded = Mathf.Min(wholeXP, remainingCap);

        if (awarded <= 0)
            return;

        fraction -= awarded;
        currentWaveXP += awarded;
        AddPoisonMasteryXP(awarded);
    }

    private void AddPoisonMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Poison, amount);
    }

    private void UnlockPoisonMasteryGateIIIIfReady(bool forceByBossParticipation)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = towerMastery != null ? towerMastery.GetProfile(TowerRole.Poison) : null;

        if (profile == null || profile.bossKillWithTower)
            return;

        if (!forceByBossParticipation && totalPoisonedTankKnightKills < Mathf.Max(1, poisonedTankKnightKillsForMasteryGate))
            return;

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Poison);
    }

    private TowerMasteryRoleProfile GetPoisonProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Poison) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetPoisonProfile();
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
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | Boss/Tank-Ziel " + (profile.bossKillWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.IV:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 85) + "/85 | Chaos 3 " + (profile.chaos3WaveWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.V:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 130) + "/130 | Chaos 5/Elite " + (profile.chaos5BossOrEliteWithTower ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private string GetKeystoneNodeId(PoisonTowerKeystone keystone)
    {
        switch (keystone)
        {
            case PoisonTowerKeystone.ToxicCore: return ToxicCore;
            case PoisonTowerKeystone.Corrosion: return Corrosion;
            case PoisonTowerKeystone.PlagueJump: return PlagueJump;
            default: return "";
        }
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(PureToxin, "Reines Toxin", PoisonTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,30 Poison Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(LongerEffect, "Laengere Wirkung", PoisonTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,25s Poison Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FineDosage, "Feine Dosierung", PoisonTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,03 Fire Rate pro Rang.", 1, 2, 3);
        AddDefinition(ToxicRange, "Toxische Reichweite", PoisonTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,12 Range pro Rang.", 1, 2, 3);
        AddDefinition(LabRoutine, "Laborroutine", PoisonTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Poison Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);

        AddDefinition(ConcentratedPoison, "Konzentriertes Gift", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.I, 5, "+0,40 Poison Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(DeepDuration, "Tiefe Dauer", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.I, 5, "+0,30s Poison Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(RePoisoning, "Nachvergiftung", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.II, 1, "Wiederholtes Treffen eines vergifteten Ziels verlaengert Poison leicht, mit Cap.", 6);
        AddDefinition(ToxinBinding, "Toxinbindung", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.II, 1, "Wenn ein Ziel mindestens 4s vergiftet ist, nimmt es +10% Poison-Schaden von diesem Tower.", 8);
        AddDefinition(DeepReaction, "Tiefenreaktion", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.III, 2, "Toxinbindung aktiviert 0,5s schneller pro Rang.", new int[] { 6, 8 }, PoisonTowerKeystone.None, ToxinBinding);
        AddDefinition(MiniBossToxin, "MiniBoss-Toxin", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.III, 3, "+5% Poison Damage gegen MiniBoss pro Rang.", 5, 6, 7);
        AddDefinition(BossToxin, "Boss-Toxin", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.IV, 3, "+4% Poison Damage gegen Boss pro Rang.", 6, 7, 8);
        AddDefinition(ToughWaveFormula, "Zaehe-Wellen-Formel", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.IV, 1, "In Toughness-Chaos-Waves: Poison Duration +0,5s.", 10);
        AddDefinition(ToxicCore, "Keystone: Toxischer Kern", PoisonTowerMasteryPath.DeepPoison, TowerMasteryMilestone.V, 1, "Wenn ein Ziel lange genug vergiftet ist, bildet sich ein Toxinkern: Poison verursacht gegen dieses Ziel +30% Schaden. Max. 1 aktiver Toxinkern pro Poison Tower.", new int[] { 24 }, PoisonTowerKeystone.ToxicCore);

        AddDefinition(TankDissolution, "Tank-Zersetzung", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.I, 5, "+4% Poison Damage gegen Tanks pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(KnightDissolution, "Knight-Zersetzung", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.I, 5, "+4% Poison Damage gegen Knights pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(CorrosiveFormula, "Korrosive Formel", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.II, 1, "Vergiftete Armor-Ziele nehmen +5% Poison-Schaden.", 6);
        AddDefinition(ArmorAnalysis, "Ruestungsanalyse", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.II, 1, "Poison Tower priorisiert nicht vergiftete Armor-Ziele etwas besser.", 5);
        AddDefinition(TimeArmorBreak, "Panzerbruch ueber Zeit", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.III, 1, "Nach 5s Poison wird ein Armor-Ziel korrodiert. Direkte Poison-Treffer +10%.", 8);
        AddDefinition(AllRounderSolution, "AllRounder-Loesung", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.III, 2, "+5% Poison Damage gegen AllRounder pro Rang.", 6, 8);
        AddDefinition(ChaosKnightFormula, "Chaos-Knight-Formel", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.IV, 3, "+5% Poison Damage gegen Chaos-Knights pro Rang.", 5, 7, 9);
        AddDefinition(ChaosArmorKnowledge, "Chaos-Panzerkunde", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.IV, 1, "In Armor-Chaos-Waves: Poison Damage +8% gegen gepanzerte normale Gegner.", 10);
        AddDefinition(Corrosion, "Keystone: Korrosion", PoisonTowerMasteryPath.ArmorDissolution, TowerMasteryMilestone.V, 1, "Laenger vergiftete Armor-Ziele werden korrodiert. Poison Tower verursachen gegen korrodierte Armor-Ziele +20% Poison-Schaden.", new int[] { 22 }, PoisonTowerKeystone.Corrosion);

        AddDefinition(InfectionTraces, "Infektionsspuren", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.I, 5, "Vergiftete Standard-Gegner geben beim Tod +5% Chance pro Rang, Poison kurz auf ein nahes Ziel zu uebertragen.", 1, 2, 3, 4, 5);
        AddDefinition(SlowSpread, "Langsame Ausbreitung", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.II, 3, "Uebertragener Poison haelt +0,20s laenger pro Rang.", 3, 4, 5);
        AddDefinition(PlagueNest, "Seuchenherd", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.II, 1, "Wenn 3+ Gegner gleichzeitig vergiftet sind: Poison Tower erhaelt +5% Fire Rate.", 6);
        AddDefinition(ControlledInfection, "Kontrollierte Infektion", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.III, 1, "Poison springt bevorzugt auf nicht vergiftete Ziele.", 6);
        AddDefinition(ToughPoisonField, "Zaehes Giftfeld", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.III, 1, "In Toughness-Waves: Infektionsspuren-Chance +10%.", 8);
        AddDefinition(PurplePlague, "Violette Seuche", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten kann Uebertragung mit reduzierter Dauer ausloesen.", 10);
        AddDefinition(ResistanceProbe, "Resistance-Probe", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.IV, 1, "In Resistance-Waves verliert Poison 10% weniger Effektwirkung.", 12);
        AddDefinition(LearnerCounterSample, "Learner-Gegenprobe", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.IV, 2, "Gegen Learner zaehlt direkter Poison-Tower-Schaden +4% pro Rang, Poison bleibt eingeschraenkt.", 6, 8);
        AddDefinition(PlagueJump, "Keystone: Seuchensprung", PoisonTowerMasteryPath.PlagueChain, TowerMasteryMilestone.V, 1, "Wenn ein vergifteter Gegner stirbt, springt Poison mit 60% reduzierter Dauer auf bis zu 1 nahes nicht vergiftetes Ziel. Interner Cooldown pro Poison Tower.", new int[] { 22 }, PoisonTowerKeystone.PlagueJump);
    }

    private void AddDefinition(string nodeId, string displayName, PoisonTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, PoisonTowerKeystone.None, "");
    }

    private void AddDefinition(string nodeId, string displayName, PoisonTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, PoisonTowerKeystone keystone, string requiredNodeId = "")
    {
        PoisonTowerMasteryNodeDefinition definition = new PoisonTowerMasteryNodeDefinition(nodeId, displayName, path, gate, maxRank, costs, effectText, keystone, requiredNodeId);
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
            nodeStates = new List<PoisonTowerMasteryNodeState>();
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        EnsureNodeStates();

        foreach (PoisonTowerMasteryNodeState state in nodeStates)
        {
            if (state == null || state.nodeId != nodeId)
                continue;

            state.rank = Mathf.Max(0, rank);
            return;
        }

        nodeStates.Add(new PoisonTowerMasteryNodeState
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
        totalPoisonedTankKnightKills = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalPoisonedTankKnightKills", 0);

        foreach (PoisonTowerMasteryNodeDefinition definition in definitions)
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

        foreach (PoisonTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalPoisonedTankKnightKills", Mathf.Max(0, totalPoisonedTankKnightKills));
        PlayerPrefs.Save();
    }
}
