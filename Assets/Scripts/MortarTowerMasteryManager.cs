using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum MortarTowerMasteryPath
{
    Trunk,
    FragmentField,
    Siege,
    CraterZone
}

public enum MortarTowerKeystone
{
    None,
    ShrapnelHail,
    SiegeStrike,
    BurningCrater
}

[Serializable]
public class MortarTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class MortarTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public MortarTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public MortarTowerKeystone keystone;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public int GetCostForNextRank(int currentRank)
    {
        return TowerMasteryManager.GetMasteryNodeCostForNextRank(currentRank, maxRank, rankCosts);
    }
}

public class MortarTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public bool siegeStrike;
    public bool armorPierceShot;
}

public class MortarTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_MortarMastery_";

    public const string HeavyImpact = "heavy_impact";
    public const string LargerRadius = "larger_radius";
    public const string StableBreech = "stable_breech";
    public const string MappedTrajectory = "mapped_trajectory";
    public const string ArtilleryRoutine = "artillery_routine";

    public const string FragmentCharge = "fragment_charge";
    public const string BroadImpact = "broad_impact";
    public const string FragmentRain = "fragment_rain";
    public const string FragmentTraining = "fragment_training";
    public const string ReadDensity = "read_density";
    public const string DensityBombardment = "density_bombardment";
    public const string VioletFragment = "violet_fragment";
    public const string ShrapnelHail = "shrapnel_hail";

    public const string KnightBombardment = "knight_bombardment";
    public const string TankBombardment = "tank_bombardment";
    public const string SiegeAngle = "siege_angle";
    public const string ArmorCrater = "armor_crater";
    public const string HeavyFormation = "heavy_formation";
    public const string RearguardBattery = "rearguard_battery";
    public const string ChaosArmorBombardment = "chaos_armor_bombardment";
    public const string BreakToughFormation = "break_tough_formation";
    public const string SiegeStrike = "siege_strike";

    public const string HotGround = "hot_ground";
    public const string LongerCrater = "longer_crater";
    public const string CraterHeat = "crater_heat";
    public const string GroundShock = "ground_shock";
    public const string ZoneReading = "zone_reading";
    public const string RiftCrater = "rift_crater";
    public const string DenseZone = "dense_zone";
    public const string ComboCrater = "combo_crater";
    public const string BurningCrater = "burning_crater";

    public static MortarTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<MortarTowerMasteryNodeState> nodeStates = new List<MortarTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public float damageToMasteryXPRatio = 0.04f;
    public int maxDamageMasteryXPPerWave = 24;
    public int threeTargetImpactXP = 8;
    public int fiveTargetImpactXP = 16;
    public int maxImpactMasteryXPPerWave = 40;
    public int quickMultiKillXP = 8;
    public int densityWaveXP = 14;
    public int toughnessArmorWaveXP = 10;
    public int chaosVariantGroupXP = 10;
    public int miniBossParticipationXP = 8;
    public int bossParticipationXP = 12;

    [Header("Milestone Gates")]
    public int threeTargetImpactsForMasteryGate = 8;

    [Header("Effect Tuning")]
    public int siegeStrikeInterval = 5;
    public float siegeStrikeDamageBonus = 0.25f;
    public float fragmentSearchBonusRadius = 0.65f;
    public float craterBaseDuration = 1.0f;
    public float craterTickInterval = 0.45f;
    public float craterDamageMultiplier = 0.18f;
    public float craterStaggerMultiplier = 0.9f;
    public float craterStaggerDuration = 0.2f;

    private readonly List<MortarTowerMasteryNodeDefinition> definitions = new List<MortarTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, MortarTowerMasteryNodeDefinition> definitionById = new Dictionary<string, MortarTowerMasteryNodeDefinition>();
    private readonly Dictionary<int, int> shotCounterByTowerId = new Dictionary<int, int>();

    private bool currentWaveHadMortarContribution = false;
    private bool currentWaveHasDensity = false;
    private bool currentWaveHasToughness = false;
    private bool currentWaveHasArmor = false;
    private bool currentWaveHasRearguard = false;
    private bool currentWaveHasChaosVariantGroup = false;
    private float currentWaveDamageMasteryXP = 0f;
    private float damageMasteryXPFraction = 0f;
    private int currentWaveImpactMasteryXP = 0;
    private int currentWaveRecentMortarKills = 0;
    private float lastMortarKillTime = -999f;
    private int totalThreeTargetImpacts = 0;

    public static MortarTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        MortarTowerMasteryManager existing = FindObjectOfType<MortarTowerMasteryManager>();
        if (existing != null)
        {
            if (preferredGameManager != null)
                existing.gameManager = preferredGameManager;

            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("MortarTowerMasteryManager");
        MortarTowerMasteryManager manager = go.AddComponent<MortarTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        return manager;
    }

    public static bool TryGetActive(out MortarTowerMasteryManager manager)
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
        currentWaveHadMortarContribution = false;
        currentWaveHasDensity = false;
        currentWaveHasToughness = false;
        currentWaveHasArmor = false;
        currentWaveHasRearguard = false;
        currentWaveHasChaosVariantGroup = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveImpactMasteryXP = 0;
        currentWaveRecentMortarKills = 0;
        lastMortarKillTime = -999f;
        shotCounterByTowerId.Clear();
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsMortarTower(tower))
            return;
    }

    public void RecordMortarKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsMortarTower(tower))
            return;

        currentWaveHadMortarContribution = true;

        if (Time.time - lastMortarKillTime <= 1.5f)
            currentWaveRecentMortarKills++;
        else
            currentWaveRecentMortarKills = 1;

        lastMortarKillTime = Time.time;

        if (currentWaveRecentMortarKills == 3)
            AddMortarMasteryXP(quickMultiKillXP);
    }

    public void RecordMortarAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsMortarTower(tower))
            return;

        currentWaveHadMortarContribution = true;
    }

    public void RecordMortarDamage(Tower tower, float appliedDamage)
    {
        if (!IsMortarTower(tower) || appliedDamage <= 0f)
            return;

        currentWaveHadMortarContribution = true;

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
        AddMortarMasteryXP(awarded);
    }

    public MortarTowerMasteryShotContext PrepareMortarShot(Tower tower, Enemy target)
    {
        MortarTowerMasteryShotContext context = new MortarTowerMasteryShotContext
        {
            primaryTarget = target,
            armorPierceShot = false,
            siegeStrike = false
        };

        if (!IsMortarTower(tower))
            return context;

        int shotCounter = IncrementShotCounter(tower);

        if (GetNodeRank(SiegeAngle) > 0 && shotCounter % Mathf.Max(1, siegeStrikeInterval) == 0)
            context.armorPierceShot = true;

        if (GetActiveKeystone() == MortarTowerKeystone.SiegeStrike && shotCounter % Mathf.Max(1, siegeStrikeInterval) == 0)
        {
            context.siegeStrike = true;
            context.armorPierceShot = true;
        }

        return context;
    }

    public int CalculateMortarShotDamage(Tower tower, Enemy target, int baseDamage, MortarTowerMasteryShotContext context)
    {
        return Mathf.Max(0, baseDamage);
    }

    public int ModifyMortarHitDamage(Tower tower, Enemy enemy, int baseDamage, bool siegeStrike)
    {
        if (!IsMortarTower(tower) || enemy == null)
            return baseDamage;

        float damage = Mathf.Max(0, baseDamage);

        if (enemy.enemyRole == EnemyRole.Standard)
            damage *= 1f + GetNodeRank(FragmentCharge) * 0.03f;

        if (enemy.enemyRole == EnemyRole.Knight)
            damage *= 1f + GetNodeRank(KnightBombardment) * 0.04f;

        if (enemy.enemyRole == EnemyRole.Tank)
            damage *= 1f + GetNodeRank(TankBombardment) * 0.04f;

        if (GetNodeRank(ReadDensity) > 0 && CountEnemiesNear(enemy.transform.position, GetBaseImpactRadius(tower)) >= 5)
            damage *= 1.10f;

        if (currentWaveHasDensity && GetNodeRank(DensityBombardment) > 0)
            damage *= 1.05f;

        if (enemy.armor > 0 && GetNodeRank(ArmorCrater) > 0)
            damage *= 1.08f;

        if (enemy.armor > 0 && GetNodeRank(HeavyFormation) > 0 && CountArmoredEnemiesNear(enemy.transform.position, GetBaseImpactRadius(tower)) >= 2)
            damage *= 1.10f;

        if (enemy.armor > 0 && currentWaveHasArmor && GetNodeRank(ChaosArmorBombardment) > 0)
            damage *= 1.08f;

        if (currentWaveHasToughness && GetNodeRank(BreakToughFormation) > 0)
            damage *= 1.05f;

        if (currentWaveHasRearguard && GetNodeRank(RearguardBattery) > 0 && IsHardFormationTarget(enemy) && enemy.GetPathProgressPercent() >= 0.55f)
            damage *= 1.08f;

        if (GetNodeRank(ZoneReading) > 0 && enemy.HasMortarCraterStagger())
            damage *= 1.08f;

        if (enemy.IsChaosVariant() && GetNodeRank(RiftCrater) > 0)
            damage *= 1.08f;

        if (siegeStrike && IsHardFormationTarget(enemy))
            damage *= 1f + siegeStrikeDamageBonus;

        return Mathf.Max(0, Mathf.RoundToInt(damage));
    }

    public void FillMortarProjectileData(Projectile projectile, Tower tower, MortarTowerMasteryShotContext context)
    {
        if (projectile == null || !IsMortarTower(tower))
            return;

        projectile.mortarRadius += GetMortarRadiusBonus(tower);
        projectile.mortarSiegeStrike = context != null && context.siegeStrike;

        if (context != null && context.armorPierceShot)
            projectile.armorPierce = Mathf.Max(projectile.armorPierce, 1);

        int fragmentTargets = GetFragmentTargetCount();
        projectile.mortarFragmentTargets = fragmentTargets;
        projectile.mortarFragmentDamageMultiplier = GetFragmentDamageMultiplier();
        projectile.mortarFragmentSearchRadius = fragmentSearchBonusRadius;

        bool createCrater = GetNodeRank(HotGround) > 0 || GetActiveKeystone() == MortarTowerKeystone.BurningCrater;
        projectile.mortarCreateCrater = createCrater;
        projectile.mortarCraterRadius = Mathf.Max(0.15f, projectile.mortarRadius * 0.75f);
        projectile.mortarCraterDuration = GetCraterDuration();
        projectile.mortarCraterDamagePerTick = Mathf.Max(0.1f, tower.GetEffectiveDamage() * GetCraterDamageMultiplier());
        projectile.mortarCraterTickInterval = Mathf.Max(0.1f, craterTickInterval);
        projectile.mortarCraterStaggers = GetNodeRank(GroundShock) > 0;
        projectile.mortarCraterStaggerMultiplier = craterStaggerMultiplier;
        projectile.mortarCraterStaggerDuration = craterStaggerDuration;
    }

    public void RecordMortarImpact(Tower tower, Vector3 position, float radius, IList<Enemy> hitEnemies)
    {
        if (!IsMortarTower(tower))
            return;

        currentWaveHadMortarContribution = true;
        int hitCount = hitEnemies != null ? hitEnemies.Count : 0;

        if (hitCount >= 3)
        {
            totalThreeTargetImpacts += 1;
            SaveProfile();
            AwardImpactXP(threeTargetImpactXP);
            MarkMasteryThreeObjective(false);
        }

        if (hitCount >= 5)
            AwardImpactXP(fiveTargetImpactXP);

        if (hitEnemies == null)
            return;

        int chaosHits = 0;
        int armorHits = 0;

        foreach (Enemy enemy in hitEnemies)
        {
            if (enemy == null)
                continue;

            if (enemy.IsChaosVariant())
                chaosHits++;

            if (enemy.armor > 0)
                armorHits++;
        }

        if (chaosHits >= 2 && currentWaveHasChaosVariantGroup)
            AwardImpactXP(chaosVariantGroupXP);

        if (armorHits >= 2 && GetNodeRank(HeavyFormation) > 0)
            AwardImpactXP(4);
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        if (!HasMortarContributor(killingTower, contributors))
            return;

        currentWaveHadMortarContribution = true;

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
        {
            AddMortarMasteryXP(miniBossParticipationXP);
            MarkMasteryThreeObjective(true);
        }

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
        {
            AddMortarMasteryXP(bossParticipationXP);
            MarkMasteryThreeObjective(true);
        }
    }

    public float GetMortarDamageBaseBonus()
    {
        return GetNodeRank(HeavyImpact) * 1f;
    }

    public float GetMortarRangeBonus()
    {
        return GetNodeRank(MappedTrajectory) * 0.15f;
    }

    public float GetMortarFireRateAdditive()
    {
        return GetNodeRank(StableBreech) * 0.02f;
    }

    public float GetMortarXPMultiplier()
    {
        return 1f + GetNodeRank(ArtilleryRoutine) * 0.03f;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        foreach (MortarTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public MortarTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        definitionById.TryGetValue(nodeId, out MortarTowerMasteryNodeDefinition definition);
        return definition;
    }

    public IEnumerable<MortarTowerMasteryNodeDefinition> GetDefinitions()
    {
        return definitions;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        if (milestone == TowerMasteryMilestone.None)
            return true;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Mortar, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        MortarTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanEditMetaProgression())
            return false;

        int rank = GetNodeRank(nodeId);
        if (rank >= definition.maxRank)
            return false;

        if (!IsMilestoneUnlocked(definition.gate))
            return false;

        TowerMasteryRoleProfile profile = GetMortarProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        MortarTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Mortar, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != MortarTowerKeystone.None && GetActiveKeystone() == MortarTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(MortarTowerKeystone keystone)
    {
        if (keystone == MortarTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(MortarTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Mortar, keystone.ToString());
    }

    public MortarTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetMortarProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return MortarTowerKeystone.None;

        try
        {
            return (MortarTowerKeystone)Enum.Parse(typeof(MortarTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return MortarTowerKeystone.None;
        }
    }

    public string GetNodeStateText(MortarTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != MortarTowerKeystone.None && GetActiveKeystone() == definition.keystone)
                return "Aktiv";

            return "Freigeschaltet";
        }

        if (!CanEditMetaProgression())
            return "Read-only im Run";

        if (!IsMilestoneUnlocked(definition.gate))
            return "Gesperrt: " + GetMilestoneDisplayName(definition.gate);

        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryRoleProfile profile = GetMortarProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetMortarProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Mortar Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Mortar im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Mortar) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine("3+-Ziel-Einschlaege fuer Mortar III: " + Mathf.Min(totalThreeTargetImpacts, Mathf.Max(1, threeTargetImpactsForMasteryGate)) + " / " + Mathf.Max(1, threeTargetImpactsForMasteryGate));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Mortar I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Mortar II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Mortar III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Mortar IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Mortar V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Mortar Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetMortarProfile();

        if (profile == null)
            return "Mortar Mastery: vorbereitet";

        return "Mortar Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        MortarTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

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

        if (definition.keystone != MortarTowerKeystone.None)
            text += "\n\nKeystone-Regel: Nur ein Mortar Keystone kann aktiv sein. Wechsel wirken erst fuer den naechsten Run.";

        return text;
    }

    public string GetPathDisplayName(MortarTowerMasteryPath path)
    {
        switch (path)
        {
            case MortarTowerMasteryPath.FragmentField: return "Splitterfeld";
            case MortarTowerMasteryPath.Siege: return "Belagerung";
            case MortarTowerMasteryPath.CraterZone: return "Kraterzone";
            default: return "Linearer Einstieg";
        }
    }

    public string GetKeystoneDisplayName(MortarTowerKeystone keystone)
    {
        switch (keystone)
        {
            case MortarTowerKeystone.ShrapnelHail: return "Splitterhagel";
            case MortarTowerKeystone.SiegeStrike: return "Belagerungsschlag";
            case MortarTowerKeystone.BurningCrater: return "Brennender Krater";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveHadMortarContribution = false;
        currentWaveDamageMasteryXP = 0f;
        damageMasteryXPFraction = 0f;
        currentWaveImpactMasteryXP = 0;
        currentWaveRecentMortarKills = 0;
        lastMortarKillTime = -999f;
        currentWaveHasDensity = WaveHasBlock(waveData, ChaosWaveBlockType.Density);
        currentWaveHasToughness = WaveHasBlock(waveData, ChaosWaveBlockType.Toughness);
        currentWaveHasArmor = WaveHasBlock(waveData, ChaosWaveBlockType.Armor);
        currentWaveHasRearguard = WaveHasBlock(waveData, ChaosWaveBlockType.Rearguard);
        currentWaveHasChaosVariantGroup = WaveHasBlock(waveData, ChaosWaveBlockType.ChaosVariantGroup);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadMortarContribution)
            return;

        if (currentWaveHasDensity && result.chaosLevelAtWaveStart > 0)
            AddMortarMasteryXP(densityWaveXP);

        if ((currentWaveHasToughness || currentWaveHasArmor) && result.chaosLevelAtWaveStart > 0)
            AddMortarMasteryXP(toughnessArmorWaveXP);
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(HeavyImpact, "Schwerer Einschlag", MortarTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+1 Mortar Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(LargerRadius, "Groesserer Radius", MortarTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,04 Einschlagsradius pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(StableBreech, "Stabiler Verschluss", MortarTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,02 Fire Rate pro Rang.", 1, 2, 3);
        AddDefinition(MappedTrajectory, "Kartierte Flugbahn", MortarTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,15 Range pro Rang.", 1, 2, 3);
        AddDefinition(ArtilleryRoutine, "Artillerieroutine", MortarTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Mortar Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);

        AddDefinition(FragmentCharge, "Splitterladung", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.I, 5, "+3% Damage gegen Standard-Gegner pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(BroadImpact, "Breiter Einschlag", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.I, 5, "Einschlagsradius +0,05 pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(FragmentRain, "Splitterregen", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.II, 1, "Einschlag verursacht 25% Zusatzschaden an bis zu 1 nahen Ziel ausserhalb des Hauptzentrums.", 6);
        AddDefinition(FragmentTraining, "Splittertraining", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.III, 2, "Zusatzschaden trifft weitere Ziele und wird staerker.", 6, 8);
        AddDefinition(ReadDensity, "Dichte lesen", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.III, 1, "Bei 5+ Gegnern im Zielbereich: +10% Mortar Damage.", 8);
        AddDefinition(DensityBombardment, "Verdichtungsbeschuss", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.IV, 1, "In Density-Chaos-Waves: Radius +0,08 und +5% Damage.", 10);
        AddDefinition(VioletFragment, "Violetter Splitter", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Variant-Gruppen verliert Splitterregen weniger Wirkung.", 10);
        AddDefinition(ShrapnelHail, "Keystone: Splitterhagel", MortarTowerMasteryPath.FragmentField, TowerMasteryMilestone.V, 1, "Mortar-Einschlaege erzeugen Splitter, die bis zu 3 nahe Gegner mit reduziertem Schaden treffen.", new int[] { 24 }, MortarTowerKeystone.ShrapnelHail);

        AddDefinition(KnightBombardment, "Knight-Beschuss", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.I, 5, "+4% Mortar Damage gegen Knights pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(TankBombardment, "Tank-Beschuss", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.I, 5, "+4% Mortar Damage gegen Tanks pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(SiegeAngle, "Belagerungswinkel", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.II, 1, "Jeder 5. Einschlag gegen Armor-Ziele ignoriert 1 Armor.", 6);
        AddDefinition(ArmorCrater, "Panzerkrater", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.II, 1, "Getroffene Armor-Ziele nehmen kurz +8% Mortar-Schaden.", 8);
        AddDefinition(HeavyFormation, "Schwere Formation", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.III, 1, "Wenn 2+ Armor-Ziele im Radius sind: +10% Mortar Damage.", 8);
        AddDefinition(RearguardBattery, "Nachhut-Batterie", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.III, 1, "In Rearguard-/PreBoss-Waves: +8% Damage gegen spaete harte Ziele.", 8);
        AddDefinition(ChaosArmorBombardment, "Chaos-Panzerbeschuss", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.IV, 1, "In Armor-Chaos-Waves: Mortar Damage +8% gegen gepanzerte normale Gegner.", 10);
        AddDefinition(BreakToughFormation, "Zaehe-Formation brechen", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.IV, 1, "In Toughness-Waves: +5% Damage und +0,05 Radius.", 10);
        AddDefinition(SiegeStrike, "Keystone: Belagerungsschlag", MortarTowerMasteryPath.Siege, TowerMasteryMilestone.V, 1, "Jeder X-te Mortar-Einschlag auf harte Gruppen wird zum Belagerungsschlag.", new int[] { 24 }, MortarTowerKeystone.SiegeStrike);

        AddDefinition(HotGround, "Heisser Boden", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.I, 1, "Einschlaege hinterlassen 1s lang kleinen Nachschaden im Zentrum.", 6);
        AddDefinition(LongerCrater, "Laengerer Krater", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.II, 3, "Kraterdauer +0,20s pro Rang.", 3, 4, 5);
        AddDefinition(CraterHeat, "Kraterhitze", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.II, 3, "Krater-Nachschaden +10% pro Rang.", 4, 5, 6);
        AddDefinition(GroundShock, "Bodenerschuetterung", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.III, 1, "Gegner im Krater werden minimal gestoert, kein echter Slow.", 8);
        AddDefinition(ZoneReading, "Zonenlesen", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.III, 1, "Wenn ein Gegner bereits im Krater gestoert wird: +8% Mortar Damage.", 8);
        AddDefinition(RiftCrater, "Risskrater", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten verursacht Krater/Mortar +8% Nachdruck.", 10);
        AddDefinition(DenseZone, "Verdichtete Zone", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.IV, 1, "In Density-Waves haelt der Krater +0,25s laenger.", 10);
        AddDefinition(ComboCrater, "Combo-Krater", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.IV, 1, "Bereitet spaetere Burn/Bleed/Poison-Combos als Setup vor.", 10);
        AddDefinition(BurningCrater, "Keystone: Brennender Krater", MortarTowerMasteryPath.CraterZone, TowerMasteryMilestone.V, 1, "Mortar-Einschlaege hinterlassen eine kurze brennende Einschlagszone mit Nachschaden.", new int[] { 22 }, MortarTowerKeystone.BurningCrater);
    }

    private void AddDefinition(string nodeId, string displayName, MortarTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, MortarTowerKeystone.None);
    }

    private void AddDefinition(string nodeId, string displayName, MortarTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, MortarTowerKeystone keystone)
    {
        MortarTowerMasteryNodeDefinition definition = new MortarTowerMasteryNodeDefinition
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

    private int IncrementShotCounter(Tower tower)
    {
        if (tower == null)
            return 0;

        int towerId = tower.GetInstanceID();
        shotCounterByTowerId.TryGetValue(towerId, out int counter);
        counter++;
        shotCounterByTowerId[towerId] = counter;
        return counter;
    }

    private void AwardImpactXP(int amount)
    {
        if (currentWaveImpactMasteryXP >= maxImpactMasteryXPPerWave)
            return;

        int award = Mathf.Min(Mathf.Max(0, amount), maxImpactMasteryXPPerWave - currentWaveImpactMasteryXP);

        if (award <= 0)
            return;

        currentWaveImpactMasteryXP += award;
        AddMortarMasteryXP(award);
    }

    private float GetMortarRadiusBonus(Tower tower)
    {
        float bonus = GetNodeRank(LargerRadius) * 0.04f;
        bonus += GetNodeRank(BroadImpact) * 0.05f;

        if (GetNodeRank(DensityBombardment) > 0 && currentWaveHasDensity)
            bonus += 0.08f;

        if (GetNodeRank(BreakToughFormation) > 0 && currentWaveHasToughness)
            bonus += 0.05f;

        return bonus;
    }

    private float GetBaseImpactRadius(Tower tower)
    {
        return 0.85f + GetMortarRadiusBonus(tower);
    }

    private int GetFragmentTargetCount()
    {
        int count = 0;

        if (GetNodeRank(FragmentRain) > 0)
            count = 1;

        if (GetNodeRank(FragmentTraining) > 0)
            count += GetNodeRank(FragmentTraining);

        if (GetActiveKeystone() == MortarTowerKeystone.ShrapnelHail)
            count = Mathf.Max(count, 3);

        return Mathf.Clamp(count, 0, 3);
    }

    private float GetFragmentDamageMultiplier()
    {
        float multiplier = GetNodeRank(FragmentRain) > 0 ? 0.25f : 0f;
        multiplier += GetNodeRank(FragmentTraining) * 0.05f;

        if (GetActiveKeystone() == MortarTowerKeystone.ShrapnelHail)
            multiplier = Mathf.Max(multiplier, 0.35f);

        if (currentWaveHasChaosVariantGroup && GetNodeRank(VioletFragment) > 0)
            multiplier += 0.10f;

        return Mathf.Clamp(multiplier, 0f, 0.6f);
    }

    private float GetCraterDuration()
    {
        float duration = craterBaseDuration + GetNodeRank(LongerCrater) * 0.20f;

        if (currentWaveHasDensity && GetNodeRank(DenseZone) > 0)
            duration += 0.25f;

        if (GetActiveKeystone() == MortarTowerKeystone.BurningCrater)
            duration += 0.5f;

        return Mathf.Max(0.1f, duration);
    }

    private float GetCraterDamageMultiplier()
    {
        float multiplier = craterDamageMultiplier;
        multiplier *= 1f + GetNodeRank(CraterHeat) * 0.10f;

        if (GetActiveKeystone() == MortarTowerKeystone.BurningCrater)
            multiplier += 0.08f;

        return Mathf.Max(0.01f, multiplier);
    }

    private int CountEnemiesNear(Vector3 position, float radius)
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        int count = 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.currentHealth <= 0f)
                continue;

            if (Vector3.Distance(enemy.transform.position, position) <= radius)
                count++;
        }

        return count;
    }

    private int CountArmoredEnemiesNear(Vector3 position, float radius)
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        int count = 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.currentHealth <= 0f || enemy.armor <= 0)
                continue;

            if (Vector3.Distance(position, enemy.transform.position) <= radius)
                count++;
        }

        return count;
    }

    private bool IsHardFormationTarget(Enemy enemy)
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

    private bool HasMortarContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsMortarTower(killingTower))
            return true;

        if (contributors == null)
            return false;

        foreach (Tower tower in contributors)
        {
            if (IsMortarTower(tower))
                return true;
        }

        return false;
    }

    private bool IsMortarTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Mortar;
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

    private void AddMortarMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Mortar, amount);
    }

    private void MarkMasteryThreeObjective(bool forceByBossParticipation)
    {
        TowerMasteryRoleProfile profile = GetMortarProfile();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (profile == null || towerMastery == null)
            return;

        if (!forceByBossParticipation && totalThreeTargetImpacts < Mathf.Max(1, threeTargetImpactsForMasteryGate))
            return;

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Mortar);
    }

    private TowerMasteryRoleProfile GetMortarProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Mortar) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetMortarProfile();
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
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | 3er-Einschlag/Boss " + (profile.bossKillWithTower ? "ja" : "nein");
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
            case TowerMasteryMilestone.I: return "Mortar I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Mortar II: Einschlagslehre";
            case TowerMasteryMilestone.III: return "Mortar III: Belagerungspraxis";
            case TowerMasteryMilestone.IV: return "Mortar IV: Rissbeschuss";
            case TowerMasteryMilestone.V: return "Mortar V: Artilleriekern";
            default: return "Linearer Einstieg";
        }
    }

    private string GetKeystoneNodeId(MortarTowerKeystone keystone)
    {
        switch (keystone)
        {
            case MortarTowerKeystone.ShrapnelHail: return ShrapnelHail;
            case MortarTowerKeystone.SiegeStrike: return SiegeStrike;
            case MortarTowerKeystone.BurningCrater: return BurningCrater;
            default: return "";
        }
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        MortarTowerMasteryNodeState state = null;

        foreach (MortarTowerMasteryNodeState candidate in nodeStates)
        {
            if (candidate != null && candidate.nodeId == nodeId)
            {
                state = candidate;
                break;
            }
        }

        if (state == null)
        {
            state = new MortarTowerMasteryNodeState { nodeId = nodeId, rank = 0 };
            nodeStates.Add(state);
        }

        MortarTowerMasteryNodeDefinition definition = GetDefinition(nodeId);
        int maxRank = definition != null ? definition.maxRank : 1;
        state.rank = Mathf.Clamp(rank, 0, maxRank);
    }

    private void LoadProfile()
    {
        nodeStates.Clear();
        foreach (MortarTowerMasteryNodeDefinition definition in definitions)
        {
            int rank = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, 0);
            if (rank > 0)
                nodeStates.Add(new MortarTowerMasteryNodeState { nodeId = definition.nodeId, rank = Mathf.Clamp(rank, 0, definition.maxRank) });
        }

        totalThreeTargetImpacts = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalThreeTargetImpacts", 0);
    }

    private void SaveProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null && towerMastery.IsMetaProgressionSuppressedForCurrentRun())
            return;

        foreach (MortarTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalThreeTargetImpacts", Mathf.Max(0, totalThreeTargetImpacts));
        PlayerPrefs.Save();
    }
}
