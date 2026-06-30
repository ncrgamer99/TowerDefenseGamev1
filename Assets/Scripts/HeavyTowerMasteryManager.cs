using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum HeavyTowerMasteryPath
{
    Trunk,
    ArmorBreaker,
    BossHammer,
    ImpactShell
}

public enum HeavyTowerKeystone
{
    None,
    ArmorBreak,
    ColossusStrike,
    TremorHit
}

[System.Serializable]
public class HeavyTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class HeavyTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public HeavyTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public HeavyTowerKeystone keystone;
    public string requiredNodeId;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public HeavyTowerMasteryNodeDefinition(string nodeId, string displayName, HeavyTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, int[] rankCosts, string effectText, HeavyTowerKeystone keystone = HeavyTowerKeystone.None, string requiredNodeId = "")
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

public struct HeavyTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public Enemy secondaryTarget;
    public int armorPierce;
    public bool applyArmorBreakMark;
    public bool applyArmorWeaken;
    public bool applyImpactStagger;
    public bool siegeDischarge;
    public bool colossusStrike;
    public bool consumeOverkillBonus;
    public float secondaryDamageMultiplier;
}

public class HeavyTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_HeavyMastery_";

    public const string HeavyCore = "heavy_core";
    public const string StableBarrel = "stable_barrel";
    public const string DrawnLine = "drawn_line";
    public const string WeightFeel = "weight_feel";
    public const string TargetWeighting = "target_weighting";

    public const string KnightBreaker = "knight_breaker";
    public const string TankBreaker = "tank_breaker";
    public const string ArmorNotch = "armor_notch";
    public const string ArmorReader = "armor_reader";
    public const string WeakeningImpact = "weakening_impact";
    public const string MassiveBreakthrough = "massive_breakthrough";
    public const string ChaosKnightBreaker = "chaos_knight_breaker";
    public const string ChaosArmorKnowledge = "chaos_armor_knowledge";
    public const string ArmorBreak = "armor_break";

    public const string MiniBossHammer = "miniboss_hammer";
    public const string BossHammer = "boss_hammer";
    public const string TargetBinding = "target_binding";
    public const string ImpactCharge = "impact_charge";
    public const string ImpactDischarge = "impact_discharge";
    public const string SiegeRhythm = "siege_rhythm";
    public const string RiftBossAnalysis = "rift_boss_analysis";
    public const string SteadfastFocus = "steadfast_focus";
    public const string ColossusStrike = "colossus_strike";

    public const string HeavyBullet = "heavy_bullet";
    public const string OverkillRoutine = "overkill_routine";
    public const string ImpactPressure = "impact_pressure";
    public const string ImpactLine = "impact_line";
    public const string TremoringHit = "tremoring_hit";
    public const string LightShockwave = "light_shockwave";
    public const string RiftImpact = "rift_impact";
    public const string RearguardBreaker = "rearguard_breaker";
    public const string TremorHit = "tremor_hit";

    public static HeavyTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<HeavyTowerMasteryNodeState> nodeStates = new List<HeavyTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public float heavyDamageToMasteryXPRatio = 0.08f;
    public int maxHeavyDamageMasteryXPPerWave = 42;
    public int tankKnightHitBonusXP = 1;
    public int maxTankKnightHitBonusXPPerWave = 20;
    public int tankKnightKillBonusXP = 8;
    public int miniBossParticipationBonusXP = 18;
    public int bossParticipationBonusXP = 45;
    public int armorWaveBonusXP = 18;
    public int chaosTankKnightKillBonusXP = 14;

    [Header("Runtime")]
    public float armorBreakMarkDuration = 4f;
    public float armorWeakenDuration = 3f;
    public float impactStaggerDuration = 0.15f;

    private class HeavyTowerRuntimeState
    {
        public Enemy boundTarget;
        public float boundUntilTime = -1f;
        public int chargeTargetId = 0;
        public int impactCharge = 0;
        public int colossusTargetId = 0;
        public int colossusHitCount = 0;
        public float overkillBonusUntilTime = -1f;
        public float overkillBonusMultiplier = 1f;
    }

    private readonly List<HeavyTowerMasteryNodeDefinition> definitions = new List<HeavyTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, HeavyTowerMasteryNodeDefinition> definitionById = new Dictionary<string, HeavyTowerMasteryNodeDefinition>();
    private readonly Dictionary<int, HeavyTowerRuntimeState> runtimeStateByTowerId = new Dictionary<int, HeavyTowerRuntimeState>();

    private int currentWaveNumber = 0;
    private bool currentWaveHadHeavyContribution = false;
    private bool currentWaveHasArmorPressure = false;
    private bool currentWaveHasRearguardPressure = false;
    private float currentWaveDamageMasteryXP = 0f;
    private float damageMasteryXPFraction = 0f;
    private int currentWaveTankKnightHitBonusXP = 0;

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

    public static HeavyTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        HeavyTowerMasteryManager existing = FindObjectOfType<HeavyTowerMasteryManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("HeavyTowerMasterySystem");
        HeavyTowerMasteryManager manager = systemObject.AddComponent<HeavyTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public static bool TryGetActive(out HeavyTowerMasteryManager manager)
    {
        manager = Instance != null ? Instance : FindObjectOfType<HeavyTowerMasteryManager>();
        return manager != null;
    }

    public IReadOnlyList<HeavyTowerMasteryNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public HeavyTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();
        return !string.IsNullOrEmpty(nodeId) && definitionById.TryGetValue(nodeId, out HeavyTowerMasteryNodeDefinition definition) ? definition : null;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        EnsureNodeStates();

        foreach (HeavyTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Heavy, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        HeavyTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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

        TowerMasteryRoleProfile profile = GetHeavyProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        HeavyTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Heavy, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != HeavyTowerKeystone.None && GetActiveKeystone() == HeavyTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(HeavyTowerKeystone keystone)
    {
        if (keystone == HeavyTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(HeavyTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Heavy, keystone.ToString());
    }

    public HeavyTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetHeavyProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return HeavyTowerKeystone.None;

        try
        {
            return (HeavyTowerKeystone)System.Enum.Parse(typeof(HeavyTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return HeavyTowerKeystone.None;
        }
    }

    public string GetNodeStateText(HeavyTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != HeavyTowerKeystone.None && GetActiveKeystone() == definition.keystone)
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
        TowerMasteryRoleProfile profile = GetHeavyProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetHeavyProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Heavy Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Heavy im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Heavy) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Heavy I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Heavy II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Heavy III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Heavy IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Heavy V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Heavy Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetHeavyProfile();

        if (profile == null)
            return "Heavy Mastery: vorbereitet";

        return "Heavy Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        HeavyTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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
            HeavyTowerMasteryNodeDefinition required = GetDefinition(definition.requiredNodeId);
            text += "Voraussetzung: " + (required != null ? required.displayName : definition.requiredNodeId) + "\n";
        }

        text += "\n" + definition.effectText;

        if (definition.keystone != HeavyTowerKeystone.None)
            text += "\n\nKeystone-Regel: Pro Tower-Typ ist nur ein Keystone aktiv. Wechsel wirken fuer neue Runs.";

        return text;
    }

    public void StartNewRun()
    {
        runtimeStateByTowerId.Clear();
        currentWaveNumber = 0;
        currentWaveHadHeavyContribution = false;
        currentWaveHasArmorPressure = false;
        currentWaveHasRearguardPressure = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveTankKnightHitBonusXP = 0;
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsHeavyTower(tower))
            return;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.RecordTowerLevelReached(tower, tower.level);
    }

    public void RecordHeavyDamage(Tower tower, float appliedDamage)
    {
        if (!IsHeavyTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadHeavyContribution = true;

        if (currentWaveDamageMasteryXP >= maxHeavyDamageMasteryXPPerWave)
            return;

        damageMasteryXPFraction += appliedDamage * Mathf.Max(0f, heavyDamageToMasteryXPRatio);
        int wholeXP = Mathf.FloorToInt(damageMasteryXPFraction);

        if (wholeXP <= 0)
            return;

        int remainingCap = Mathf.Max(0, maxHeavyDamageMasteryXPPerWave - Mathf.FloorToInt(currentWaveDamageMasteryXP));
        int awarded = Mathf.Min(wholeXP, remainingCap);

        if (awarded <= 0)
            return;

        damageMasteryXPFraction -= awarded;
        currentWaveDamageMasteryXP += awarded;
        AddHeavyMasteryXP(awarded);
    }

    public void RecordHeavyHit(Tower tower, Enemy enemy)
    {
        if (!IsHeavyTower(tower) || enemy == null)
            return;

        currentWaveHadHeavyContribution = true;

        if ((enemy.enemyRole == EnemyRole.Tank || enemy.enemyRole == EnemyRole.Knight) && currentWaveTankKnightHitBonusXP < maxTankKnightHitBonusXPPerWave)
        {
            int award = Mathf.Min(tankKnightHitBonusXP, maxTankKnightHitBonusXPPerWave - currentWaveTankKnightHitBonusXP);
            currentWaveTankKnightHitBonusXP += Mathf.Max(0, award);
            AddHeavyMasteryXP(award);
        }
    }

    public void RecordHeavyKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsHeavyTower(tower))
            return;

        currentWaveHadHeavyContribution = true;

        if (killedRole == EnemyRole.Tank || killedRole == EnemyRole.Knight)
            AddHeavyMasteryXP(tankKnightKillBonusXP);
    }

    public void RecordHeavyAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsHeavyTower(tower))
            return;

        currentWaveHadHeavyContribution = true;
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower heavyContributor = FindHeavyContributor(killingTower, contributors);

        if (heavyContributor == null)
            return;

        currentWaveHadHeavyContribution = true;

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
            AddHeavyMasteryXP(miniBossParticipationBonusXP);

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
            AddHeavyMasteryXP(bossParticipationBonusXP);

        if (enemy.IsChaosVariant() && (enemy.enemyRole == EnemyRole.Tank || enemy.enemyRole == EnemyRole.Knight))
            AddHeavyMasteryXP(chaosTankKnightKillBonusXP);
    }

    public void RecordPotentialOverkill(Tower tower, Enemy target, int rawDamage)
    {
        if (!IsHeavyTower(tower) || target == null)
            return;

        int rank = GetNodeRank(OverkillRoutine);

        if (rank <= 0)
            return;

        if (rawDamage <= target.GetCurrentHealth())
            return;

        HeavyTowerRuntimeState state = GetRuntimeState(tower);
        float bonus = rank == 1 ? 1.05f : rank == 2 ? 1.10f : 1.15f;
        float duration = 2f + GetNodeRank(ImpactLine);
        state.overkillBonusMultiplier = Mathf.Max(state.overkillBonusMultiplier, bonus);
        state.overkillBonusUntilTime = Time.time + Mathf.Max(0.1f, duration);
    }

    public float GetHeavyXPMultiplier()
    {
        return 1f + GetNodeRank(WeightFeel) * 0.03f;
    }

    public float GetHeavyDamageBaseBonus()
    {
        return GetNodeRank(HeavyCore);
    }

    public float GetHeavyRangeBonus()
    {
        return GetNodeRank(DrawnLine) * 0.12f;
    }

    public float GetHeavyFireRateAdditive()
    {
        return GetNodeRank(StableBarrel) * 0.02f;
    }

    public HeavyTowerMasteryShotContext PrepareHeavyShot(Tower tower, Enemy currentTarget, int shotCounter)
    {
        HeavyTowerMasteryShotContext context = new HeavyTowerMasteryShotContext();
        context.primaryTarget = currentTarget;

        if (!IsHeavyTower(tower))
            return context;

        currentWaveHadHeavyContribution = true;

        HeavyTowerRuntimeState state = GetRuntimeState(tower);
        Enemy overrideTarget = FindHeavyPriorityTarget(tower, currentTarget, state);

        if (overrideTarget != null)
            context.primaryTarget = overrideTarget;

        if (context.primaryTarget != null && context.primaryTarget.HasHeavyArmorWeaken())
            context.armorPierce += 1;

        int armorInterval = GetArmorNotchInterval();
        bool armorNotchShot = armorInterval > 0 && shotCounter % armorInterval == 0;

        if (armorNotchShot)
        {
            context.armorPierce += 1;

            if (GetNodeRank(MassiveBreakthrough) > 0 && context.primaryTarget != null && context.primaryTarget.GetArmor() > 0 && !IsBossOrMiniBoss(context.primaryTarget))
                context.armorPierce += 1;
        }

        context.applyArmorWeaken = GetNodeRank(WeakeningImpact) > 0 && context.primaryTarget != null && context.primaryTarget.GetArmor() > 0;
        context.applyArmorBreakMark = GetActiveKeystone() == HeavyTowerKeystone.ArmorBreak && context.primaryTarget != null && context.primaryTarget.GetArmor() > 0;
        context.applyImpactStagger = GetNodeRank(ImpactPressure) > 0 && context.primaryTarget != null && IsNormalNonBossTarget(context.primaryTarget);

        context.siegeDischarge = ShouldTriggerImpactDischarge(tower, context.primaryTarget, state);
        context.colossusStrike = ShouldTriggerColossusStrike(context.primaryTarget, state);
        context.consumeOverkillBonus = state.overkillBonusUntilTime >= Time.time && state.overkillBonusMultiplier > 1f;

        if (ShouldTriggerTremorShot(shotCounter))
        {
            context.secondaryTarget = FindHeavySecondaryTarget(tower, context.primaryTarget, GetNodeRank(LightShockwave) > 0);
            context.secondaryDamageMultiplier = GetActiveKeystone() == HeavyTowerKeystone.TremorHit && shotCounter % 6 == 0 ? 0.50f : 0.35f;
        }

        return context;
    }

    public int CalculateHeavyShotDamage(Tower tower, Enemy target, int baseDamage, HeavyTowerMasteryShotContext context, float projectileMultiplier)
    {
        float damageValue = Mathf.Max(0f, baseDamage) * Mathf.Max(0f, projectileMultiplier);

        if (target != null)
        {
            if (target.GetHealthPercent() >= 0.60f)
                damageValue *= 1f + GetNodeRank(TargetWeighting) * 0.02f;

            if (target.GetHealthPercent() >= 0.50f)
                damageValue *= 1f + GetNodeRank(HeavyBullet) * 0.03f;

            if (target.enemyRole == EnemyRole.Knight)
                damageValue *= 1f + GetNodeRank(KnightBreaker) * 0.04f;

            if (target.enemyRole == EnemyRole.Tank)
                damageValue *= 1f + GetNodeRank(TankBreaker) * 0.04f;

            if (target.GetArmor() > 0 && GetNodeRank(ArmorReader) > 0)
                damageValue *= 1.10f;

            if (target.HasHeavyArmorBreakMark())
                damageValue *= 1.15f;

            if (target.enemyRole == EnemyRole.MiniBoss || target.isMiniBoss)
                damageValue *= 1f + GetNodeRank(MiniBossHammer) * 0.05f;

            if (target.enemyRole == EnemyRole.Boss || target.isBoss)
                damageValue *= 1f + GetNodeRank(BossHammer) * 0.04f;

            if (target.IsChaosVariant() && target.enemyRole == EnemyRole.Knight)
                damageValue *= 1f + GetNodeRank(ChaosKnightBreaker) * 0.05f;

            if (currentWaveHasArmorPressure && GetNodeRank(ChaosArmorKnowledge) > 0)
                damageValue *= 1.08f;

            if (GetNodeRank(RiftBossAnalysis) > 0 && IsBossOrMiniBoss(target) && GetCurrentChaosLevel() >= 3)
                damageValue *= 1.05f;

            if (GetNodeRank(RearguardBreaker) > 0 && currentWaveHasRearguardPressure && IsHardTarget(target))
                damageValue *= 1.08f;
        }

        if (context.consumeOverkillBonus)
            damageValue *= ConsumeOverkillBonus(tower);

        if (context.siegeDischarge && IsBossOrMiniBoss(target))
            damageValue *= 1.35f;

        if (context.colossusStrike && IsBossOrMiniBoss(target))
            damageValue *= 1.75f;

        if (target != null && target.IsChaosVariant() && GetNodeRank(RiftImpact) > 0 && context.consumeOverkillBonus)
            damageValue *= 1.10f;

        return Mathf.Max(0, Mathf.RoundToInt(damageValue));
    }

    public void FillHeavyProjectileData(Projectile projectile, HeavyTowerMasteryShotContext context)
    {
        if (projectile == null)
            return;

        projectile.armorPierce = Mathf.Max(projectile.armorPierce, context.armorPierce);
        projectile.applyHeavyArmorBreakMark = context.applyArmorBreakMark;
        projectile.applyHeavyArmorWeaken = context.applyArmorWeaken;
        projectile.applyHeavyImpactStagger = context.applyImpactStagger;
        projectile.heavyArmorBreakMarkDuration = armorBreakMarkDuration;
        projectile.heavyArmorWeakenDuration = armorWeakenDuration;
        projectile.heavyImpactStaggerDuration = impactStaggerDuration;
    }

    public string GetPathDisplayName(HeavyTowerMasteryPath path)
    {
        switch (path)
        {
            case HeavyTowerMasteryPath.ArmorBreaker: return "Panzerbrecher";
            case HeavyTowerMasteryPath.BossHammer: return "Bosshammer";
            case HeavyTowerMasteryPath.ImpactShell: return "Wuchtgeschoss";
            default: return "Einstieg";
        }
    }

    public string GetMilestoneDisplayName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return "Heavy I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Heavy II: Einschlagtechnik";
            case TowerMasteryMilestone.III: return "Heavy III: Belagerungsmeisterschaft";
            case TowerMasteryMilestone.IV: return "Heavy IV: Rissbrecher";
            case TowerMasteryMilestone.V: return "Heavy V: Kolossenkern";
            default: return "Offen";
        }
    }

    public string GetKeystoneDisplayName(HeavyTowerKeystone keystone)
    {
        switch (keystone)
        {
            case HeavyTowerKeystone.ArmorBreak: return "Ruestungsbruch";
            case HeavyTowerKeystone.ColossusStrike: return "Kolossenschlag";
            case HeavyTowerKeystone.TremorHit: return "Erschuetterungstreffer";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveHadHeavyContribution = false;
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveTankKnightHitBonusXP = 0;
        currentWaveHasArmorPressure = WaveHasArmorPressure(waveData);
        currentWaveHasRearguardPressure = WaveHasRearguardPressure(waveData);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadHeavyContribution)
            return;

        if (currentWaveHasArmorPressure)
            AddHeavyMasteryXP(armorWaveBonusXP);
    }

    private Enemy FindHeavyPriorityTarget(Tower tower, Enemy currentTarget, HeavyTowerRuntimeState state)
    {
        if (tower == null || state == null)
            return currentTarget;

        if (GetNodeRank(TargetBinding) > 0 && tower.targetMode == TowerTargetMode.Strongest)
        {
            if (state.boundTarget != null && Time.time <= state.boundUntilTime && IsEnemyInRange(tower, state.boundTarget))
                return state.boundTarget;

            state.boundTarget = currentTarget;
            state.boundUntilTime = Time.time + 2f;
        }

        if (GetNodeRank(SteadfastFocus) > 0)
        {
            Enemy boss = FindBossOrMiniBossInRange(tower);

            if (boss != null)
                return boss;
        }

        return currentTarget;
    }

    private bool ShouldTriggerImpactDischarge(Tower tower, Enemy target, HeavyTowerRuntimeState state)
    {
        if (tower == null || target == null || state == null || GetNodeRank(ImpactCharge) <= 0 || GetNodeRank(ImpactDischarge) <= 0)
            return false;

        if (!IsBossOrMiniBoss(target))
            return false;

        int targetId = target.GetInstanceID();

        if (state.chargeTargetId != targetId)
        {
            state.chargeTargetId = targetId;
            state.impactCharge = 0;
        }

        state.impactCharge++;
        int threshold = GetNodeRank(SiegeRhythm) > 0 ? 4 : 5;

        if (state.impactCharge < threshold)
            return false;

        state.impactCharge = 0;
        return true;
    }

    private bool ShouldTriggerColossusStrike(Enemy target, HeavyTowerRuntimeState state)
    {
        if (target == null || state == null || GetActiveKeystone() != HeavyTowerKeystone.ColossusStrike || !IsBossOrMiniBoss(target))
            return false;

        int targetId = target.GetInstanceID();

        if (state.colossusTargetId != targetId)
        {
            state.colossusTargetId = targetId;
            state.colossusHitCount = 0;
        }

        state.colossusHitCount++;

        if (state.colossusHitCount < 4)
            return false;

        state.colossusHitCount = 0;
        return true;
    }

    private bool ShouldTriggerTremorShot(int shotCounter)
    {
        if (GetActiveKeystone() == HeavyTowerKeystone.TremorHit && shotCounter % 6 == 0)
            return true;

        return GetNodeRank(TremoringHit) > 0 && shotCounter % 8 == 0;
    }

    private Enemy FindHeavySecondaryTarget(Tower tower, Enemy primaryTarget, bool preferNearPrimary)
    {
        if (tower == null || primaryTarget == null)
            return null;

        var enemies = EnemyRegistry.ActiveEnemies;
        Enemy best = null;
        float bestScore = Mathf.Infinity;
        float towerRange = Mathf.Max(0f, tower.GetEffectiveRange());

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy == primaryTarget)
                continue;

            if (Vector3.Distance(tower.transform.position, enemy.transform.position) > towerRange)
                continue;

            float score = preferNearPrimary
                ? Vector3.Distance(primaryTarget.transform.position, enemy.transform.position)
                : Vector3.Distance(tower.transform.position, enemy.transform.position);

            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    private Enemy FindBossOrMiniBossInRange(Tower tower)
    {
        if (tower == null)
            return null;

        var enemies = EnemyRegistry.ActiveEnemies;
        Enemy best = null;
        float bestProgress = -Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !IsBossOrMiniBoss(enemy) || !IsEnemyInRange(tower, enemy))
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

    private bool IsEnemyInRange(Tower tower, Enemy enemy)
    {
        if (tower == null || enemy == null)
            return false;

        return Vector3.Distance(tower.transform.position, enemy.transform.position) <= tower.GetEffectiveRange();
    }

    private float ConsumeOverkillBonus(Tower tower)
    {
        HeavyTowerRuntimeState state = GetRuntimeState(tower);
        float bonus = Mathf.Max(1f, state.overkillBonusMultiplier);
        state.overkillBonusMultiplier = 1f;
        state.overkillBonusUntilTime = -1f;
        return bonus;
    }

    private int GetArmorNotchInterval()
    {
        int rank = GetNodeRank(ArmorNotch);

        if (rank <= 0)
            return 0;

        return Mathf.Clamp(7 - rank, 4, 6);
    }

    private bool WaveHasArmorPressure(WaveData waveData)
    {
        if (waveData == null)
            return false;

        if (waveData.ContainsRole(EnemyRole.Knight) || waveData.ContainsRole(EnemyRole.Tank) || waveData.ContainsRole(EnemyRole.Boss))
            return true;

        if (waveData.chaosWaveBlocks == null)
            return false;

        foreach (ChaosWaveBlock block in waveData.chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
                continue;

            if (block.blockType == ChaosWaveBlockType.Armor || block.blockType == ChaosWaveBlockType.Toughness)
                return true;
        }

        return false;
    }

    private bool WaveHasRearguardPressure(WaveData waveData)
    {
        if (waveData == null || waveData.chaosWaveBlocks == null)
            return false;

        foreach (ChaosWaveBlock block in waveData.chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
                continue;

            if (block.blockType == ChaosWaveBlockType.Rearguard || block.blockType == ChaosWaveBlockType.RolePressure)
                return true;
        }

        return false;
    }

    private Tower FindHeavyContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsHeavyTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsHeavyTower(tower))
                return tower;
        }

        return null;
    }

    private bool IsHeavyTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Heavy;
    }

    private bool IsBossOrMiniBoss(Enemy enemy)
    {
        return enemy != null && (enemy.enemyRole == EnemyRole.Boss || enemy.enemyRole == EnemyRole.MiniBoss || enemy.isBoss || enemy.isMiniBoss);
    }

    private bool IsHardTarget(Enemy enemy)
    {
        if (enemy == null)
            return false;

        return enemy.enemyRole == EnemyRole.Tank ||
               enemy.enemyRole == EnemyRole.Knight ||
               enemy.enemyRole == EnemyRole.MiniBoss ||
               enemy.enemyRole == EnemyRole.Boss ||
               enemy.isMiniBoss ||
               enemy.isBoss ||
               enemy.isElite;
    }

    private bool IsNormalNonBossTarget(Enemy enemy)
    {
        return enemy != null && !IsBossOrMiniBoss(enemy) && !enemy.isElite && enemy.enemyRole != EnemyRole.Elite;
    }

    private HeavyTowerRuntimeState GetRuntimeState(Tower tower)
    {
        int key = tower != null ? tower.GetInstanceID() : 0;

        if (!runtimeStateByTowerId.TryGetValue(key, out HeavyTowerRuntimeState state) || state == null)
        {
            state = new HeavyTowerRuntimeState();
            runtimeStateByTowerId[key] = state;
        }

        return state;
    }

    private void AddHeavyMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Heavy, amount);
    }

    private TowerMasteryRoleProfile GetHeavyProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Heavy) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
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

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetHeavyProfile();
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

    private string GetKeystoneNodeId(HeavyTowerKeystone keystone)
    {
        switch (keystone)
        {
            case HeavyTowerKeystone.ArmorBreak: return ArmorBreak;
            case HeavyTowerKeystone.ColossusStrike: return ColossusStrike;
            case HeavyTowerKeystone.TremorHit: return TremorHit;
            default: return "";
        }
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(HeavyCore, "Schwerer Kern", HeavyTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+1 Heavy Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(StableBarrel, "Stabiler Lauf", HeavyTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,02 Fire Rate pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(DrawnLine, "Gezogene Linie", HeavyTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,12 Range pro Rang.", 1, 2, 3);
        AddDefinition(WeightFeel, "Gewichtsgefuehl", HeavyTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Heavy Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);
        AddDefinition(TargetWeighting, "Zielgewichtung", HeavyTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+2% Damage gegen Gegner ueber 60% HP pro Rang.", 1, 2, 3);

        AddDefinition(KnightBreaker, "Knight-Brecher", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.I, 5, "+4% Damage gegen Knights pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(TankBreaker, "Tank-Brecher", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.I, 5, "+4% Damage gegen Tanks pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(ArmorNotch, "Ruestungskerbe", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.II, 3, "Jeder 6./5./4. Heavy-Schuss ignoriert 1 Armor.", 3, 4, 5);
        AddDefinition(ArmorReader, "Panzerleser", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.II, 1, "Heavy verursacht +10% Damage gegen Ziele mit Armor > 0.", 6);
        AddDefinition(WeakeningImpact, "Schwaechender Einschlag", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.III, 1, "Getroffene Armor-Ziele erhalten kurz -1 effektive Armor gegen Heavy-Treffer.", 8);
        AddDefinition(MassiveBreakthrough, "Massiver Durchbruch", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.III, 1, "Ruestungskerbe ignoriert bei normalen Armor-Zielen zusaetzlich 1 Armor, nicht bei Boss/MiniBoss.", 8);
        AddDefinition(ChaosKnightBreaker, "Chaos-Knight-Brecher", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.IV, 3, "+5% Damage gegen Chaos-Knights pro Rang.", 5, 7, 9);
        AddDefinition(ChaosArmorKnowledge, "Chaos-Panzerkunde", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Wave-Armor-Bausteine erhaelt Heavy +8% Damage.", 10);
        AddDefinition(ArmorBreak, "Keystone: Ruestungsbruch", HeavyTowerMasteryPath.ArmorBreaker, TowerMasteryMilestone.V, 1, "Heavy-Treffer auf Armor-Ziele markieren sie kurz. Heavy Tower verursachen gegen markierte Armor-Ziele +15% Damage.", new int[] { 22 }, HeavyTowerKeystone.ArmorBreak);

        AddDefinition(MiniBossHammer, "MiniBoss-Hammer", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.I, 5, "+5% Damage gegen MiniBoss pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(BossHammer, "Boss-Hammer", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.I, 5, "+4% Damage gegen Boss pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(TargetBinding, "Zielbindung", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.II, 1, "Wenn Heavy auf Strongest feuert, bleibt er kurz auf demselben Ziel, falls moeglich.", 5);
        AddDefinition(ImpactCharge, "Wuchtladung", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.II, 1, "Jeder Treffer auf dasselbe Boss-/MiniBoss-Ziel laedt Wucht.", 6);
        AddDefinition(ImpactDischarge, "Wuchtentladung", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.III, 1, "Bei voller Wucht verursacht ein Heavy-Schuss +35% Damage und setzt Wucht zurueck.", new int[] { 8 }, HeavyTowerKeystone.None, ImpactCharge);
        AddDefinition(SiegeRhythm, "Belagerungsrhythmus", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.III, 1, "Wuchtentladung benoetigt nur 4 Treffer.", new int[] { 10 }, HeavyTowerKeystone.None, ImpactDischarge);
        AddDefinition(RiftBossAnalysis, "Rissboss-Analyse", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.IV, 1, "+5% Damage gegen Boss/MiniBoss bei Chaos-Level 3+.", 10);
        AddDefinition(SteadfastFocus, "Standhafter Fokus", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.IV, 1, "Wenn ein Boss in Range ist, bleibt Heavy staerker fokussiert.", 8);
        AddDefinition(ColossusStrike, "Keystone: Kolossenschlag", HeavyTowerMasteryPath.BossHammer, TowerMasteryMilestone.V, 1, "Jeder 4. Treffer auf denselben Boss/MiniBoss verursacht +75% Damage. Nur Boss/MiniBoss.", new int[] { 24 }, HeavyTowerKeystone.ColossusStrike);

        AddDefinition(HeavyBullet, "Schwere Kugel", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.I, 5, "+3% Damage gegen Gegner ueber 50% HP pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(OverkillRoutine, "Overkill-Routine", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.II, 3, "Wenn Heavy mit Overkill toetet, erhaelt der naechste Schuss +5%/+10%/+15% Damage.", 3, 4, 5);
        AddDefinition(ImpactPressure, "Einschlagdruck", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.II, 1, "Treffer gegen normale Gegner verzoegern sie minimal.", 6);
        AddDefinition(ImpactLine, "Wuchtlinie", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.III, 2, "Overkill-Routine haelt 1 zusaetzliche Sekunde pro Rang.", 5, 7);
        AddDefinition(TremoringHit, "Erschuetternder Treffer", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.III, 1, "Jeder 8. Heavy-Schuss verursacht kleinen Zusatzschaden an einem nahen Gegner, 35% Damage.", 8);
        AddDefinition(LightShockwave, "Druckwelle light", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.III, 1, "Zusatzschaden trifft bevorzugt Gegner nahe am Ziel, aber maximal 1 Ziel.", new int[] { 8 }, HeavyTowerKeystone.None, TremoringHit);
        AddDefinition(RiftImpact, "Risswucht", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten verursacht Overkill-Routine +10% zusaetzlichen Bonus.", 10);
        AddDefinition(RearguardBreaker, "Nachhutbrecher", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.IV, 1, "Gegen Rearguard-/PreBoss-Druck erhaelt Heavy +8% Damage auf harte Ziele.", 10);
        AddDefinition(TremorHit, "Keystone: Erschuetterungstreffer", HeavyTowerMasteryPath.ImpactShell, TowerMasteryMilestone.V, 1, "Jeder 6. Heavy-Schuss erzeugt eine kleine Erschuetterung: Hauptziel voller Schaden, ein nahes Ziel 50% Schaden. Kein echter AoE.", new int[] { 22 }, HeavyTowerKeystone.TremorHit);
    }

    private void AddDefinition(string nodeId, string displayName, HeavyTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, HeavyTowerKeystone.None, "");
    }

    private void AddDefinition(string nodeId, string displayName, HeavyTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, HeavyTowerKeystone keystone, string requiredNodeId = "")
    {
        HeavyTowerMasteryNodeDefinition definition = new HeavyTowerMasteryNodeDefinition(nodeId, displayName, path, gate, maxRank, costs, effectText, keystone, requiredNodeId);
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
            nodeStates = new List<HeavyTowerMasteryNodeState>();
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        EnsureNodeStates();

        foreach (HeavyTowerMasteryNodeState state in nodeStates)
        {
            if (state == null || state.nodeId != nodeId)
                continue;

            state.rank = Mathf.Max(0, rank);
            return;
        }

        nodeStates.Add(new HeavyTowerMasteryNodeState
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

        foreach (HeavyTowerMasteryNodeDefinition definition in definitions)
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

        foreach (HeavyTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.Save();
    }
}
