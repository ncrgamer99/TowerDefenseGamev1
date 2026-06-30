using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum FireTowerMasteryPath
{
    Trunk,
    Wildfire,
    EmberPressure,
    RiftFlame
}

public enum FireTowerKeystone
{
    None,
    FlameJump,
    EmberCore,
    PurpleBurn
}

[System.Serializable]
public class FireTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class FireTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public FireTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public FireTowerKeystone keystone;
    public string requiredNodeId;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public FireTowerMasteryNodeDefinition(string nodeId, string displayName, FireTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, int[] rankCosts, string effectText, FireTowerKeystone keystone = FireTowerKeystone.None, string requiredNodeId = "")
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

public class FireTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_FireMastery_";

    public const string HotTip = "hot_tip";
    public const string StableFlame = "stable_flame";
    public const string LongerEmber = "longer_ember";
    public const string FlameLine = "flame_line";
    public const string BurnRoutine = "burn_routine";

    public const string SparkFlight = "spark_flight";
    public const string FireNest = "fire_nest";
    public const string AshTransfer = "ash_transfer";
    public const string AshTransferTraining = "ash_transfer_training";
    public const string GroupHeat = "group_heat";
    public const string DensityBurn = "density_burn";
    public const string PurpleEmberTrail = "purple_ember_trail";
    public const string FlameJump = "flame_jump";

    public const string DeepEmber = "deep_ember";
    public const string Afterburn = "afterburn";
    public const string EmberStacks = "ember_stacks";
    public const string CleanCombustion = "clean_combustion";
    public const string EmberPressure = "ember_pressure";
    public const string MiniBossEmber = "miniboss_ember";
    public const string BossEmber = "boss_ember";
    public const string ToughWaveOven = "tough_wave_oven";
    public const string EmberCore = "ember_core";

    public const string RiftSparks = "rift_sparks";
    public const string PurpleHeat = "purple_heat";
    public const string LearnerStudy = "learner_study";
    public const string MageFlameWindow = "mage_flame_window";
    public const string RiftBurnControl = "rift_burn_control";
    public const string PurpleGroupRead = "purple_group_read";
    public const string ResistanceHeat = "resistance_heat";
    public const string ChaosLearnerCounterSample = "chaos_learner_counter_sample";
    public const string PurpleBurn = "purple_burn";

    public static FireTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<FireTowerMasteryNodeState> nodeStates = new List<FireTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public float directDamageToMasteryXPRatio = 0.02f;
    public int maxDirectDamageMasteryXPPerWave = 16;
    public float burnDamageToMasteryXPRatio = 0.07f;
    public int maxBurnDamageMasteryXPPerWave = 46;
    public int standardKillXP = 3;
    public int burnKillBonusXP = 5;
    public int multiBurnBonusXP = 8;
    public int chaosBurnBonusXP = 2;
    public int maxChaosBurnBonusXPPerWave = 18;
    public int miniBossParticipationBonusXP = 8;
    public int bossParticipationBonusXP = 14;
    public int densityToughnessChaosWaveBonusXP = 16;

    [Header("Milestone Tracking")]
    public int burnKillsForMasteryGate = 100;
    public int totalFireBurnKills = 0;

    [Header("Runtime")]
    public float flameJumpCooldown = 1.0f;
    public float emberCoreWarmupSeconds = 3f;

    private readonly List<FireTowerMasteryNodeDefinition> definitions = new List<FireTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, FireTowerMasteryNodeDefinition> definitionById = new Dictionary<string, FireTowerMasteryNodeDefinition>();
    private readonly HashSet<int> burnedEnemiesThisWave = new HashSet<int>();
    private readonly Dictionary<int, float> fireTowerSpreadCooldownUntil = new Dictionary<int, float>();
    private readonly Dictionary<int, float> burnStartedAtByEnemyId = new Dictionary<int, float>();
    private readonly Dictionary<int, int> emberCoreTargetByTowerId = new Dictionary<int, int>();

    private int currentWaveNumber = 0;
    private bool currentWaveHadFireContribution = false;
    private bool currentWaveHasDensity = false;
    private bool currentWaveHasToughness = false;
    private bool currentWaveHasChaosVariantGroup = false;
    private bool currentWaveHasResistance = false;
    private float currentWaveDirectMasteryXP = 0f;
    private float directMasteryXPFraction = 0f;
    private float currentWaveBurnMasteryXP = 0f;
    private float burnMasteryXPFraction = 0f;
    private int currentWaveChaosBurnBonusXP = 0;
    private bool awardedMultiBurnBonusThisWave = false;

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

    public static FireTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        FireTowerMasteryManager existing = FindObjectOfType<FireTowerMasteryManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("FireTowerMasterySystem");
        FireTowerMasteryManager manager = systemObject.AddComponent<FireTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public static bool TryGetActive(out FireTowerMasteryManager manager)
    {
        manager = Instance != null ? Instance : FindObjectOfType<FireTowerMasteryManager>();
        return manager != null;
    }

    public IReadOnlyList<FireTowerMasteryNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public FireTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();
        return !string.IsNullOrEmpty(nodeId) && definitionById.TryGetValue(nodeId, out FireTowerMasteryNodeDefinition definition) ? definition : null;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        EnsureNodeStates();

        foreach (FireTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Fire, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        FireTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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

        TowerMasteryRoleProfile profile = GetFireProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        FireTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Fire, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != FireTowerKeystone.None && GetActiveKeystone() == FireTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(FireTowerKeystone keystone)
    {
        if (keystone == FireTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(FireTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Fire, keystone.ToString());
    }

    public FireTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetFireProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return FireTowerKeystone.None;

        try
        {
            return (FireTowerKeystone)System.Enum.Parse(typeof(FireTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return FireTowerKeystone.None;
        }
    }

    public string GetNodeStateText(FireTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != FireTowerKeystone.None && GetActiveKeystone() == definition.keystone)
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
        TowerMasteryRoleProfile profile = GetFireProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetFireProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Fire Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Fire im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Fire) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine("Burn-Kills fuer Fire III: " + Mathf.Min(totalFireBurnKills, Mathf.Max(1, burnKillsForMasteryGate)) + " / " + Mathf.Max(1, burnKillsForMasteryGate));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Fire I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Fire II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Fire III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Fire IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Fire V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Fire Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetFireProfile();

        if (profile == null)
            return "Fire Mastery: vorbereitet";

        return "Fire Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        FireTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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
            FireTowerMasteryNodeDefinition required = GetDefinition(definition.requiredNodeId);
            text += "Voraussetzung: " + (required != null ? required.displayName : definition.requiredNodeId) + "\n";
        }

        text += "\n" + definition.effectText;

        if (definition.keystone != FireTowerKeystone.None)
            text += "\n\nKeystone-Regel: Pro Tower-Typ ist nur ein Keystone aktiv. Wechsel wirken fuer neue Runs.";

        return text;
    }

    public void StartNewRun()
    {
        burnedEnemiesThisWave.Clear();
        fireTowerSpreadCooldownUntil.Clear();
        burnStartedAtByEnemyId.Clear();
        emberCoreTargetByTowerId.Clear();
        currentWaveNumber = 0;
        currentWaveHadFireContribution = false;
        currentWaveHasDensity = false;
        currentWaveHasToughness = false;
        currentWaveHasChaosVariantGroup = false;
        currentWaveHasResistance = false;
        currentWaveDirectMasteryXP = 0f;
        directMasteryXPFraction = 0f;
        currentWaveBurnMasteryXP = 0f;
        burnMasteryXPFraction = 0f;
        currentWaveChaosBurnBonusXP = 0;
        awardedMultiBurnBonusThisWave = false;
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsFireTower(tower))
            return;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.RecordTowerLevelReached(tower, tower.level);
    }

    public void RecordFireDirectDamage(Tower tower, Enemy target, float appliedDamage)
    {
        if (!IsFireTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadFireContribution = true;
        AddCappedXP(appliedDamage, directDamageToMasteryXPRatio, ref directMasteryXPFraction, ref currentWaveDirectMasteryXP, maxDirectDamageMasteryXPPerWave);
    }

    public void RecordFireBurnDamage(Tower tower, Enemy target, float appliedDamage)
    {
        if (!IsFireTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadFireContribution = true;
        AddCappedXP(appliedDamage, burnDamageToMasteryXPRatio, ref burnMasteryXPFraction, ref currentWaveBurnMasteryXP, maxBurnDamageMasteryXPPerWave);
    }

    public void RecordFireBurnApplied(Tower tower, Enemy target)
    {
        if (!IsFireTower(tower) || target == null)
            return;

        currentWaveHadFireContribution = true;
        int enemyId = target.GetInstanceID();

        if (!burnStartedAtByEnemyId.ContainsKey(enemyId))
            burnStartedAtByEnemyId[enemyId] = Time.time;

        if (!burnedEnemiesThisWave.Contains(enemyId))
        {
            burnedEnemiesThisWave.Add(enemyId);

            if (!awardedMultiBurnBonusThisWave && burnedEnemiesThisWave.Count >= 4)
            {
                awardedMultiBurnBonusThisWave = true;
                AddFireMasteryXP(multiBurnBonusXP);
            }
        }

        if (target.IsChaosVariant() && currentWaveChaosBurnBonusXP < maxChaosBurnBonusXPPerWave)
        {
            int award = Mathf.Min(Mathf.Max(0, chaosBurnBonusXP), Mathf.Max(0, maxChaosBurnBonusXPPerWave - currentWaveChaosBurnBonusXP));
            currentWaveChaosBurnBonusXP += award;
            AddFireMasteryXP(award);
        }
    }

    public void RecordFireKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsFireTower(tower))
            return;

        currentWaveHadFireContribution = true;

        if (killedRole == EnemyRole.Standard)
            AddFireMasteryXP(standardKillXP);
    }

    public void RecordFireAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsFireTower(tower))
            return;

        currentWaveHadFireContribution = true;
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower fireContributor = FindFireContributor(killingTower, contributors);

        if (fireContributor == null)
            return;

        currentWaveHadFireContribution = true;

        if (IsFireTower(killingTower) && enemy.HasBurn())
        {
            AddFireMasteryXP(burnKillBonusXP);
            totalFireBurnKills++;
            UnlockFireMasteryGateIIIIfReady(false);
            SaveProfile();
        }

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
        {
            AddFireMasteryXP(miniBossParticipationBonusXP);
            UnlockFireMasteryGateIIIIfReady(true);
        }

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
        {
            AddFireMasteryXP(bossParticipationBonusXP);
            UnlockFireMasteryGateIIIIfReady(true);
        }

        TrySpreadBurnOnDeath(enemy, fireContributor, IsFireTower(killingTower));
    }

    public int CalculateFireShotDamage(Tower tower, Enemy target, int baseDamage)
    {
        float damageValue = Mathf.Max(0f, baseDamage);

        if (target != null && target.enemyRole == EnemyRole.Learner)
            damageValue *= 1f + GetNodeRank(LearnerStudy) * 0.04f;

        return Mathf.Max(0, Mathf.RoundToInt(damageValue));
    }

    public float GetFireDirectDamageBaseBonus()
    {
        return GetNodeRank(HotTip) * 0.25f;
    }

    public float GetFireRangeBonus()
    {
        return GetNodeRank(FlameLine) * 0.12f;
    }

    public float GetFireXPMultiplier()
    {
        return 1f + GetNodeRank(BurnRoutine) * 0.03f;
    }

    public float GetFireFireRateMultiplier(Tower tower)
    {
        if (!IsFireTower(tower))
            return 1f;

        float multiplier = 1f;

        if (GetNodeRank(GroupHeat) > 0 && CountBurningEnemies() >= 4)
            multiplier += 0.05f;

        return multiplier;
    }

    public float GetModifiedFireBurnDamage(Tower tower, Enemy target, float baseBurnDamage)
    {
        if (!IsFireTower(tower))
            return Mathf.Max(0.1f, baseBurnDamage);

        float damage = Mathf.Max(0.1f, baseBurnDamage);
        damage += GetNodeRank(StableFlame) * 0.25f;
        damage += GetNodeRank(DeepEmber) * 0.35f;

        if (target != null)
        {
            if (target.enemyRole == EnemyRole.Standard)
                damage *= 1f + GetNodeRank(SparkFlight) * 0.03f;

            if (target.enemyRole == EnemyRole.Tank)
                damage *= 1f + GetNodeRank(CleanCombustion) * 0.04f;

            if (target.enemyRole == EnemyRole.MiniBoss || target.isMiniBoss)
                damage *= 1f + GetNodeRank(MiniBossEmber) * 0.05f;

            if (target.enemyRole == EnemyRole.Boss || target.isBoss)
                damage *= 1f + GetNodeRank(BossEmber) * 0.03f;

            if (target.IsChaosVariant())
                damage *= 1f + GetNodeRank(RiftSparks) * 0.03f;

            if (IsBurningLongEnough(target) && GetNodeRank(EmberPressure) > 0)
                damage *= 1.10f;

            if (IsEmberCoreActiveForTower(tower, target))
                damage *= 1.25f;
        }

        return Mathf.Max(0.1f, damage);
    }

    public float GetModifiedFireBurnDuration(Tower tower, Enemy target, float baseDuration)
    {
        if (!IsFireTower(tower))
            return Mathf.Max(0.1f, baseDuration);

        float duration = Mathf.Max(0.1f, baseDuration);
        duration += GetNodeRank(LongerEmber) * 0.20f;
        duration += GetNodeRank(Afterburn) * 0.25f;

        if (CountEnemiesInRange(tower) >= 3)
            duration += GetNodeRank(FireNest) * 0.15f;

        if (target != null && target.HasBurn() && GetNodeRank(EmberStacks) > 0)
            duration += 0.35f;

        if (target != null && target.IsChaosVariant())
            duration += GetNodeRank(PurpleHeat) * 0.15f;

        if (target != null && target.enemyRole == EnemyRole.Mage && GetNodeRank(MageFlameWindow) > 0)
            duration += 0.50f;

        if (currentWaveHasToughness && GetNodeRank(ToughWaveOven) > 0)
            duration += 0.50f;

        return Mathf.Max(0.1f, duration);
    }

    public float ModifyFireBurnTickDamage(Tower tower, Enemy target, float tickDamage)
    {
        if (!IsFireTower(tower) || target == null || tickDamage <= 0f)
            return tickDamage;

        float modified = tickDamage;

        if (currentWaveHasResistance && GetNodeRank(ResistanceHeat) > 0)
            modified *= 1.10f;

        if (target.IsChaosVariant() && GetActiveKeystone() == FireTowerKeystone.PurpleBurn)
            modified *= 1.08f;

        return modified;
    }

    public float ModifyFireChaosLearnerBurnDamage(Tower tower, Enemy target, float rawTickDamage, float reducedDamage)
    {
        if (!IsFireTower(tower) || target == null || rawTickDamage <= 0f)
            return reducedDamage;

        float result = reducedDamage;

        if (GetNodeRank(RiftBurnControl) > 0)
            result += rawTickDamage * 0.05f;

        if (GetActiveKeystone() == FireTowerKeystone.PurpleBurn)
            result += rawTickDamage * 0.12f;

        return Mathf.Min(rawTickDamage, result);
    }

    public float ModifyFireChaosLearnerHeal(Tower tower, float rawHeal)
    {
        if (!IsFireTower(tower))
            return rawHeal;

        if (GetNodeRank(ChaosLearnerCounterSample) > 0)
            return rawHeal * 0.90f;

        return rawHeal;
    }

    public string GetPathDisplayName(FireTowerMasteryPath path)
    {
        switch (path)
        {
            case FireTowerMasteryPath.Wildfire: return "Wildbrand";
            case FireTowerMasteryPath.EmberPressure: return "Glutdruck";
            case FireTowerMasteryPath.RiftFlame: return "Rissflamme";
            default: return "Einstieg";
        }
    }

    public string GetMilestoneDisplayName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return "Fire I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Fire II: Gluttechnik";
            case TowerMasteryMilestone.III: return "Fire III: Flammenmeisterschaft";
            case TowerMasteryMilestone.IV: return "Fire IV: Rissflamme";
            case TowerMasteryMilestone.V: return "Fire V: Inferno-Kern";
            default: return "Offen";
        }
    }

    public string GetKeystoneDisplayName(FireTowerKeystone keystone)
    {
        switch (keystone)
        {
            case FireTowerKeystone.FlameJump: return "Flammenuebersprung";
            case FireTowerKeystone.EmberCore: return "Glutkern";
            case FireTowerKeystone.PurpleBurn: return "Purpurbrand";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        burnedEnemiesThisWave.Clear();
        burnStartedAtByEnemyId.Clear();
        currentWaveHadFireContribution = false;
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        currentWaveDirectMasteryXP = 0f;
        directMasteryXPFraction = 0f;
        currentWaveBurnMasteryXP = 0f;
        burnMasteryXPFraction = 0f;
        currentWaveChaosBurnBonusXP = 0;
        awardedMultiBurnBonusThisWave = false;
        currentWaveHasDensity = WaveHasBlock(waveData, ChaosWaveBlockType.Density);
        currentWaveHasToughness = WaveHasBlock(waveData, ChaosWaveBlockType.Toughness);
        currentWaveHasChaosVariantGroup = WaveHasBlock(waveData, ChaosWaveBlockType.ChaosVariantGroup);
        currentWaveHasResistance = WaveHasBlock(waveData, ChaosWaveBlockType.Resistance);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadFireContribution)
            return;

        if ((currentWaveHasDensity || currentWaveHasToughness) && result.chaosLevelAtWaveStart > 0)
            AddFireMasteryXP(densityToughnessChaosWaveBonusXP);
    }

    private void TrySpreadBurnOnDeath(Enemy deadEnemy, Tower fireTower, bool fireKilled)
    {
        if (deadEnemy == null || !IsFireTower(fireTower))
            return;

        if (GetNodeRank(AshTransfer) <= 0 && GetActiveKeystone() != FireTowerKeystone.FlameJump)
            return;

        if (!CanUseSpreadCooldown(fireTower))
            return;

        float chance = GetSpreadChance(deadEnemy, fireKilled);

        if (chance <= 0f || Random.value > chance)
            return;

        Enemy target = FindNearbyBurnTarget(deadEnemy, 1.75f);

        if (target == null)
            return;

        float damage = GetModifiedFireBurnDamage(fireTower, target, Mathf.Max(1f, fireTower.burnDamage));
        float duration = GetModifiedFireBurnDuration(fireTower, target, Mathf.Max(0.5f, fireTower.burnDuration)) * 0.50f;
        target.ApplyBurn(damage, duration, fireTower);
        SetSpreadCooldown(fireTower);
    }

    private float GetSpreadChance(Enemy deadEnemy, bool fireKilled)
    {
        float chance = 0f;

        if (GetNodeRank(AshTransfer) > 0)
            chance = 0.10f + GetNodeRank(AshTransferTraining) * 0.05f;

        if (currentWaveHasDensity && GetNodeRank(DensityBurn) > 0)
            chance += 0.10f;

        if (currentWaveHasChaosVariantGroup && GetNodeRank(PurpleGroupRead) > 0)
            chance += 0.05f;

        if (!fireKilled && deadEnemy != null && deadEnemy.IsChaosVariant() && GetNodeRank(PurpleEmberTrail) > 0)
            chance = Mathf.Max(chance, 0.12f);

        if (GetActiveKeystone() == FireTowerKeystone.FlameJump)
            chance = Mathf.Max(chance, 1f);

        return Mathf.Clamp01(chance);
    }

    private Enemy FindNearbyBurnTarget(Enemy deadEnemy, float radius)
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        Enemy best = null;
        float bestDistance = Mathf.Max(0.1f, radius);

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy == deadEnemy || enemy.HasBurn() || !enemy.CanReceiveBurnStack())
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

    private bool CanUseSpreadCooldown(Tower fireTower)
    {
        int key = fireTower != null ? fireTower.GetInstanceID() : 0;
        return !fireTowerSpreadCooldownUntil.TryGetValue(key, out float untilTime) || Time.time >= untilTime;
    }

    private void SetSpreadCooldown(Tower fireTower)
    {
        int key = fireTower != null ? fireTower.GetInstanceID() : 0;
        fireTowerSpreadCooldownUntil[key] = Time.time + Mathf.Max(0.1f, flameJumpCooldown);
    }

    private bool IsBurningLongEnough(Enemy target)
    {
        if (target == null)
            return false;

        int targetId = target.GetInstanceID();
        return burnStartedAtByEnemyId.TryGetValue(targetId, out float startedAt) && Time.time - startedAt >= 3f;
    }

    private bool IsEmberCoreActiveForTower(Tower tower, Enemy target)
    {
        if (GetActiveKeystone() != FireTowerKeystone.EmberCore || !IsFireTower(tower) || target == null)
            return false;

        if (!IsBurningLongEnough(target))
            return false;

        int towerId = tower.GetInstanceID();
        int targetId = target.GetInstanceID();

        if (!emberCoreTargetByTowerId.TryGetValue(towerId, out int activeTargetId) || activeTargetId == 0)
            emberCoreTargetByTowerId[towerId] = targetId;

        return emberCoreTargetByTowerId.TryGetValue(towerId, out activeTargetId) && activeTargetId == targetId;
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
        AddFireMasteryXP(awarded);
    }

    private int CountEnemiesInRange(Tower tower)
    {
        if (tower == null)
            return 0;

        var enemies = EnemyRegistry.ActiveEnemies;
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

    private int CountBurningEnemies()
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        int count = 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && enemy.HasBurn())
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

    private Tower FindFireContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsFireTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsFireTower(tower))
                return tower;
        }

        return null;
    }

    private bool IsFireTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Fire;
    }

    private void AddFireMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Fire, amount);
    }

    private void UnlockFireMasteryGateIIIIfReady(bool forceByBossParticipation)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = towerMastery != null ? towerMastery.GetProfile(TowerRole.Fire) : null;

        if (profile == null || profile.bossKillWithTower)
            return;

        if (!forceByBossParticipation && totalFireBurnKills < Mathf.Max(1, burnKillsForMasteryGate))
            return;

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Fire);
    }

    private TowerMasteryRoleProfile GetFireProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Fire) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetFireProfile();
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
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | Boss/Burn-Ziel " + (profile.bossKillWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.IV:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 85) + "/85 | Chaos 3 " + (profile.chaos3WaveWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.V:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 130) + "/130 | Chaos 5/Elite " + (profile.chaos5BossOrEliteWithTower ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private string GetKeystoneNodeId(FireTowerKeystone keystone)
    {
        switch (keystone)
        {
            case FireTowerKeystone.FlameJump: return FlameJump;
            case FireTowerKeystone.EmberCore: return EmberCore;
            case FireTowerKeystone.PurpleBurn: return PurpleBurn;
            default: return "";
        }
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(HotTip, "Heisse Spitze", FireTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,25 direkter Fire Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(StableFlame, "Stabile Flamme", FireTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,25 Burn Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(LongerEmber, "Laengere Glut", FireTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,20s Burn Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FlameLine, "Flammenlinie", FireTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,12 Range pro Rang.", 1, 2, 3);
        AddDefinition(BurnRoutine, "Brandroutine", FireTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Fire Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);

        AddDefinition(SparkFlight, "Funkenflug", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.I, 5, "Burn-Schaden gegen Standard-Gegner +3% pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FireNest, "Brandherd", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.II, 3, "Wenn 3+ Gegner in Range sind: Burn Duration +0,15s pro Rang.", 3, 4, 5);
        AddDefinition(AshTransfer, "Ascheuebertrag", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.II, 1, "Brennende Gegner haben beim Tod 10% Chance, einen nahen Gegner kurz zu entzuenden.", 6);
        AddDefinition(AshTransferTraining, "Ascheuebertrag-Training", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.III, 2, "Uebertragungs-Chance +5% pro Rang.", new int[] { 6, 8 }, FireTowerKeystone.None, AshTransfer);
        AddDefinition(GroupHeat, "Gruppenhitze", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.III, 1, "Wenn 4+ Gegner gleichzeitig brennen: Fire Tower erhaelt +5% Fire Rate.", 8);
        AddDefinition(DensityBurn, "Verdichtungsbrand", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.IV, 1, "In Chaos-Waves mit Density: Burn-Ausbreitung +10% Chance.", 10);
        AddDefinition(PurpleEmberTrail, "Violette Glutspur", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten kann Ascheuebertrag auch bei Assist-Kill ausloesen, aber mit reduzierter Dauer.", 10);
        AddDefinition(FlameJump, "Keystone: Flammenuebersprung", FireTowerMasteryPath.Wildfire, TowerMasteryMilestone.V, 1, "Wenn ein brennender Gegner stirbt, springt Burn mit 50% reduzierter Dauer auf bis zu 1 nahes Ziel. Interner Cooldown pro Fire Tower.", new int[] { 22 }, FireTowerKeystone.FlameJump);

        AddDefinition(DeepEmber, "Tiefere Glut", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.I, 5, "+0,35 Burn Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(Afterburn, "Nachbrand", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.I, 5, "+0,25s Burn Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(EmberStacks, "Glutstapel", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.II, 1, "Wiederholtes Treffen eines brennenden Ziels erhoeht Burn Duration leicht, mit Cap.", 6);
        AddDefinition(CleanCombustion, "Saubere Verbrennung", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.II, 3, "Burn-Ticks verursachen gegen Tanks +4% Schaden pro Rang.", 4, 5, 6);
        AddDefinition(EmberPressure, "Glutdruck", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.III, 1, "Wenn ein Ziel mindestens 3s brennt, erhaelt Burn +10% Schaden gegen dieses Ziel.", 8);
        AddDefinition(MiniBossEmber, "MiniBoss-Glut", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.III, 3, "+5% Burn Damage gegen MiniBoss pro Rang.", 5, 6, 7);
        AddDefinition(BossEmber, "Boss-Glut", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.IV, 3, "+3% Burn Damage gegen Boss pro Rang.", 6, 7, 8);
        AddDefinition(ToughWaveOven, "Zaehe-Wellen-Ofen", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.IV, 1, "In Chaos-Waves mit Toughness: Burn Duration +0,5s.", 10);
        AddDefinition(EmberCore, "Keystone: Glutkern", FireTowerMasteryPath.EmberPressure, TowerMasteryMilestone.V, 1, "Brennt ein Ziel lange genug, wird ein Glutkern aktiv: Burn verursacht gegen dieses Ziel +25% Schaden. Max. 1 aktiver Glutkern pro Fire Tower.", new int[] { 24 }, FireTowerKeystone.EmberCore);

        AddDefinition(RiftSparks, "Rissfunken", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.I, 5, "+3% Burn Damage gegen Chaos-Varianten pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(PurpleHeat, "Purpurhitze", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.II, 3, "Chaos-Varianten brennen +0,15s laenger pro Rang.", 3, 4, 5);
        AddDefinition(LearnerStudy, "Learner-Studie", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.II, 3, "Gegen Learner zaehlt Fire-Direktschaden +4% pro Rang, Burn bleibt eingeschraenkt.", 4, 5, 6);
        AddDefinition(MageFlameWindow, "Mage-Flammenfenster", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.III, 1, "Gegen Mages bleibt Burn 0,5s laenger aktiv.", 6);
        AddDefinition(RiftBurnControl, "Rissbrand-Kontrolle", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.III, 1, "DoT-Reduktion von Chaos-Learnern wird gegen Fire-Burn minimal abgeschwaecht.", 8);
        AddDefinition(PurpleGroupRead, "Violette Gruppe lesen", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.IV, 1, "In Chaos-Waves mit ChaosVariantGroup: Fire erhaelt +5% Burn-Ausbreitung.", 8);
        AddDefinition(ResistanceHeat, "Resistenzhitze", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.IV, 1, "In Resistance-Waves verliert Fire-Burn 10% weniger Wirkung.", 10);
        AddDefinition(ChaosLearnerCounterSample, "Chaos-Learner-Gegenprobe", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.IV, 1, "Chaos-Learner-Heilung durch Fire-Burn wird leicht reduziert.", 12);
        AddDefinition(PurpleBurn, "Keystone: Purpurbrand", FireTowerMasteryPath.RiftFlame, TowerMasteryMilestone.V, 1, "Fire-Burn gegen Chaos-Varianten erhaelt einen kleinen Rissanteil, der Chaos-DoT-Reduktion nur teilweise umgeht.", new int[] { 24 }, FireTowerKeystone.PurpleBurn);
    }

    private void AddDefinition(string nodeId, string displayName, FireTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, FireTowerKeystone.None, "");
    }

    private void AddDefinition(string nodeId, string displayName, FireTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, FireTowerKeystone keystone, string requiredNodeId = "")
    {
        FireTowerMasteryNodeDefinition definition = new FireTowerMasteryNodeDefinition(nodeId, displayName, path, gate, maxRank, costs, effectText, keystone, requiredNodeId);
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
            nodeStates = new List<FireTowerMasteryNodeState>();
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        EnsureNodeStates();

        foreach (FireTowerMasteryNodeState state in nodeStates)
        {
            if (state == null || state.nodeId != nodeId)
                continue;

            state.rank = Mathf.Max(0, rank);
            return;
        }

        nodeStates.Add(new FireTowerMasteryNodeState
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
        totalFireBurnKills = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBurnKills", 0);

        foreach (FireTowerMasteryNodeDefinition definition in definitions)
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

        foreach (FireTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBurnKills", Mathf.Max(0, totalFireBurnKills));
        PlayerPrefs.Save();
    }
}
