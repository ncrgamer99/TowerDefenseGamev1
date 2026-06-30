using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum TowerMasteryPath
{
    Trunk,
    PathA,
    PathB,
    PathC
}

public enum TowerMasteryMilestone
{
    None = 0,
    I = 1,
    II = 2,
    III = 3,
    IV = 4,
    V = 5
}

[System.Serializable]
public class TowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

[System.Serializable]
public class TowerMasteryRoleProfile
{
    public TowerRole towerRole = TowerRole.Basic;
    public int masteryXP = 0;
    public int unspentPoints = 0;
    public int spentPoints = 0;
    public int totalEarnedPoints = 0;
    public int bestLevelEver = 1;
    public bool reachedLevel10 = false;
    public bool reachedLevel20 = false;
    public bool reachedLevel30 = false;
    public bool reachedLevel40 = false;
    public bool reachedLevel50 = false;
    public bool bossKillWithTower = false;
    public bool chaos3WaveWithTower = false;
    public bool chaos5BossOrEliteWithTower = false;
    public string activeKeystoneId = "";
    public int lastRunMasteryXPGained = 0;
    public int lastRunMasteryPointsGained = 0;
    public int lastRunHighestLevel = 1;
    public int lastRunTowerXP = 0;
    public int lastRunImpactScore = 0;
    public int lastRunWaveTier = 0;
    public int lastRunLevelTier = 0;
    public int lastRunImpactTier = 0;
    public List<TowerMasteryNodeState> nodeStates = new List<TowerMasteryNodeState>();
}

public class TowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_TowerMastery_";

    public static TowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Profiles")]
    public List<TowerMasteryRoleProfile> roleProfiles = new List<TowerMasteryRoleProfile>();

    [Header("Run-End Point Rules")]
    public int maxPointsPerRun = 5;
    public int wavesPerPointTier = 5;
    public int levelsPerPointTier = 10;
    public int impactPerPointTier = 25;
    public float killImpact = 6f;
    public float assistImpact = 3f;
    public float damageImpactDivisor = 50f;

    [Header("Run-End XP Payout")]
    public float towerXPToMasteryXPConversion = 0.35f;
    public int maxBaseMasteryXPFromTowerXP = 500;
    public int masteryXPPerEarnedPoint = 15;
    public int miniBossParticipationXP = 20;
    public int bossParticipationXP = 50;
    public int maxBossMasteryXPPerRun = 150;
    public int chaosWaveParticipationXP = 10;
    public int maxChaosWaveMasteryXPPerRun = 150;
    public int chaosVariantContributionXP = 2;
    public int maxChaosVariantMasteryXPPerRun = 100;
    public int maxRoleBonusMasteryXPPerRun = 500;

    [Header("First-Time XP")]
    public int firstLevel10MasteryXP = 50;
    public int firstLevel20MasteryXP = 75;
    public int firstLevel30MasteryXP = 100;
    public int firstLevel40MasteryXP = 125;
    public int firstLevel50MasteryXP = 150;
    public int firstBossParticipationXP = 100;
    public int firstChaos3WaveXP = 150;
    public int firstChaos5BossOrEliteXP = 250;

    [HideInInspector] public float damageToMasteryXPRatio = 0.04f;
    [HideInInspector] public int maxDamageMasteryXPPerWave = 24;
    [HideInInspector] public int killMasteryXP = 3;
    [HideInInspector] public int assistMasteryXP = 1;
    [HideInInspector] public int chaos5BossParticipationXP = 80;
    [HideInInspector] public int minimumWaveForPointPayout = 5;

    private class TowerMasteryRunState
    {
        public int highestLevelThisRun = 1;
        public bool currentWaveHadContribution = false;
        public int towerXPGainedThisRun = 0;
        public int killsThisRun = 0;
        public int assistsThisRun = 0;
        public float damageThisRun = 0f;
        public int builtCountThisRun = 0;
        public int metaPointsPreparedThisRun = 0;
        public int highestVisualTierThisRun = 0;
        public int miniBossParticipationCount = 0;
        public int bossParticipationCount = 0;
        public int chaosWaveParticipationCount = 0;
        public int chaosVariantContributionCount = 0;
        public int pendingRoleBonusXP = 0;
        public bool firstBossObjectiveThisRun = false;
        public bool firstChaos3ObjectiveThisRun = false;
        public bool firstChaos5ObjectiveThisRun = false;
    }

    private class TowerMasteryRoleRunSummary
    {
        public TowerRole role;
        public int highestLevel = 1;
        public int totalTowerXP = 0;
        public int kills = 0;
        public int assists = 0;
        public float damage = 0f;
        public int builtCount = 0;
        public int metaPointsPrepared = 0;
        public int highestVisualTier = 0;
        public int miniBossParticipationCount = 0;
        public int bossParticipationCount = 0;
        public int chaosWaveParticipationCount = 0;
        public int chaosVariantContributionCount = 0;
        public int pendingRoleBonusXP = 0;
        public bool firstBossObjectiveThisRun = false;
        public bool firstChaos3ObjectiveThisRun = false;
        public bool firstChaos5ObjectiveThisRun = false;
    }

    private readonly Dictionary<TowerRole, TowerMasteryRoleProfile> profileByRole = new Dictionary<TowerRole, TowerMasteryRoleProfile>();
    private readonly Dictionary<TowerRole, TowerMasteryRunState> runStateByRole = new Dictionary<TowerRole, TowerMasteryRunState>();
    private int currentWaveNumber = 0;
    private int highestWaveReachedThisRun = 0;
    private int highestCompletedWaveThisRun = 0;
    private bool runFinalized = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureAllProfiles();
        LoadProfiles();
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

    public static TowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        TowerMasteryManager existing = FindObjectOfType<TowerMasteryManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("TowerMasterySystem");
        TowerMasteryManager manager = systemObject.AddComponent<TowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public static bool TryGetActive(out TowerMasteryManager manager)
    {
        manager = Instance != null ? Instance : FindObjectOfType<TowerMasteryManager>();
        return manager != null;
    }

    public static TowerRole[] GetOrderedTowerRoles()
    {
        return new TowerRole[]
        {
            TowerRole.Basic,
            TowerRole.Rapid,
            TowerRole.Heavy,
            TowerRole.Fire,
            TowerRole.Slow,
            TowerRole.Poison,
            TowerRole.Sniper,
            TowerRole.Alchemist,
            TowerRole.Lightning,
            TowerRole.Mortar,
            TowerRole.Spike
        };
    }

    public static string GetTowerDisplayName(TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Basic: return "Basic Tower";
            case TowerRole.Rapid: return "Rapid Tower";
            case TowerRole.Heavy: return "Heavy Tower";
            case TowerRole.Fire: return "Fire Tower";
            case TowerRole.Slow: return "Slow Tower";
            case TowerRole.Poison: return "Poison Tower";
            case TowerRole.Sniper: return "Sniper Tower";
            case TowerRole.Alchemist: return "Alchemist Tower";
            case TowerRole.Lightning: return "Lightning Tower";
            case TowerRole.Mortar: return "Mortar Tower";
            case TowerRole.Spike: return "Spike Tower";
            case TowerRole.Beam: return "Beam Tower";
            case TowerRole.Support: return "Support Tower";
            case TowerRole.Frost: return "Frost Tower";
            default: return role.ToString();
        }
    }

    public static int GetRepeatingUpgradeCostForRank(int nextRank)
    {
        int safeRank = Mathf.Max(1, nextRank);
        return Mathf.Max(1, (safeRank + 1) / 2);
    }

    public static int GetMasteryNodeCostForNextRank(int currentRank, int maxRank, int[] rankCosts)
    {
        int safeMaxRank = Mathf.Max(1, maxRank);
        int safeRank = Mathf.Clamp(currentRank, 0, safeMaxRank);

        if (safeRank >= safeMaxRank)
            return 0;

        if (safeMaxRank > 1)
            return GetRepeatingUpgradeCostForRank(safeRank + 1);

        if (rankCosts == null || rankCosts.Length == 0)
            return GetRepeatingUpgradeCostForRank(safeRank + 1);

        int index = Mathf.Clamp(safeRank, 0, rankCosts.Length - 1);
        return Mathf.Max(1, rankCosts[index]);
    }

    public static int GetMilestoneSpentPointRequirement(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return 10;
            case TowerMasteryMilestone.II: return 25;
            case TowerMasteryMilestone.III: return 50;
            case TowerMasteryMilestone.IV: return 85;
            case TowerMasteryMilestone.V: return 130;
            default: return 0;
        }
    }

    public void StartNewRun()
    {
        currentWaveNumber = 0;
        highestWaveReachedThisRun = 0;
        highestCompletedWaveThisRun = 0;
        runFinalized = false;

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = GetProfile(role);
            LoadProfile(profile);

            TowerMasteryRunState state = GetRunState(role);
            state.highestLevelThisRun = 1;
            state.currentWaveHadContribution = false;
            state.towerXPGainedThisRun = 0;
            state.killsThisRun = 0;
            state.assistsThisRun = 0;
            state.damageThisRun = 0f;
            state.builtCountThisRun = 0;
            state.metaPointsPreparedThisRun = 0;
            state.highestVisualTierThisRun = 0;
            state.miniBossParticipationCount = 0;
            state.bossParticipationCount = 0;
            state.chaosWaveParticipationCount = 0;
            state.chaosVariantContributionCount = 0;
            state.pendingRoleBonusXP = 0;
            state.firstBossObjectiveThisRun = false;
            state.firstChaos3ObjectiveThisRun = false;
            state.firstChaos5ObjectiveThisRun = false;

            profile.lastRunMasteryXPGained = 0;
            profile.lastRunMasteryPointsGained = 0;
            profile.lastRunHighestLevel = 1;
            profile.lastRunTowerXP = 0;
            profile.lastRunImpactScore = 0;
            profile.lastRunWaveTier = 0;
            profile.lastRunLevelTier = 0;
            profile.lastRunImpactTier = 0;
        }
    }

    public void FinalizeRun()
    {
        if (runFinalized)
            return;

        if (IsMetaProgressionSuppressedForCurrentRun())
        {
            runFinalized = true;
            return;
        }

        RefreshHighestLevelsFromActiveTowers();
        SyncRunStateFromStatistics();
        runFinalized = true;
        int waveTier = Mathf.Clamp(highestCompletedWaveThisRun / Mathf.Max(1, wavesPerPointTier), 0, Mathf.Max(0, maxPointsPerRun));

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = GetProfile(role);
            TowerMasteryRoleRunSummary summary = BuildRoleRunSummary(role);
            int levelTier = Mathf.Clamp(summary.highestLevel / Mathf.Max(1, levelsPerPointTier), 0, Mathf.Max(0, maxPointsPerRun));
            int impact = CalculateRoleImpact(summary);
            int impactTier = Mathf.Clamp(impact / Mathf.Max(1, impactPerPointTier), 0, Mathf.Max(0, maxPointsPerRun));
            int earnedPoints = Mathf.Min(levelTier, waveTier, impactTier, Mathf.Max(0, maxPointsPerRun));
            int masteryXP = CalculateRunEndMasteryXP(profile, summary, earnedPoints);

            profile.lastRunHighestLevel = summary.highestLevel;
            profile.lastRunTowerXP = summary.totalTowerXP;
            profile.lastRunImpactScore = impact;
            profile.lastRunWaveTier = waveTier;
            profile.lastRunLevelTier = levelTier;
            profile.lastRunImpactTier = impactTier;

            if (earnedPoints > 0)
            {
                profile.unspentPoints += earnedPoints;
                profile.totalEarnedPoints += earnedPoints;
                profile.lastRunMasteryPointsGained = earnedPoints;
            }

            if (masteryXP > 0)
            {
                profile.masteryXP += masteryXP;
                profile.lastRunMasteryXPGained = masteryXP;
            }

            ApplyRunEndMilestoneProgress(profile, summary);

            if (earnedPoints > 0 || masteryXP > 0)
            {
                Debug.Log(
                    GetTowerDisplayName(role) +
                    " Mastery Payout: +" + earnedPoints +
                    " Punkt(e), +" + masteryXP +
                    " XP | LevelTier " + levelTier +
                    " | WaveTier " + waveTier +
                    " | ImpactTier " + impactTier +
                    " | Impact " + impact + "."
                );
            }

            SaveProfile(profile);
            SyncLegacyBasicProfileIfNeeded(role, profile);
        }

        PlayerPrefs.Save();
    }

    public TowerMasteryRoleProfile GetProfile(TowerRole role)
    {
        EnsureAllProfiles();
        return profileByRole[role];
    }

    public int GetHighestLevelThisRun(TowerRole role)
    {
        return GetRunState(role).highestLevelThisRun;
    }

    public bool CanEditMetaProgression()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
            return true;

        return !gameManager.gameStarted || gameManager.isGameOver;
    }

    public bool IsMilestoneUnlocked(TowerRole role, TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        int masteryLevel = GetMasteryLevel(role);

        switch (milestone)
        {
            case TowerMasteryMilestone.None:
                return true;
            case TowerMasteryMilestone.I:
                return profile.spentPoints >= 10 && masteryLevel >= 3 && profile.reachedLevel10;
            case TowerMasteryMilestone.II:
                return profile.spentPoints >= 25 && masteryLevel >= 8 && profile.reachedLevel20;
            case TowerMasteryMilestone.III:
                return profile.spentPoints >= 50 && masteryLevel >= 15 && profile.bossKillWithTower;
            case TowerMasteryMilestone.IV:
                return profile.spentPoints >= 85 && masteryLevel >= 25 && profile.chaos3WaveWithTower;
            case TowerMasteryMilestone.V:
                return profile.spentPoints >= 130 && masteryLevel >= 40 && profile.chaos5BossOrEliteWithTower;
            default:
                return false;
        }
    }

    public int GetMasteryVisualTier(TowerRole role)
    {
        if (IsMilestoneUnlocked(role, TowerMasteryMilestone.V)) return 5;
        if (IsMilestoneUnlocked(role, TowerMasteryMilestone.IV)) return 4;
        if (IsMilestoneUnlocked(role, TowerMasteryMilestone.III)) return 3;
        if (IsMilestoneUnlocked(role, TowerMasteryMilestone.II)) return 2;
        if (IsMilestoneUnlocked(role, TowerMasteryMilestone.I)) return 1;
        return 0;
    }

    public int GetMasteryLevel(TowerRole role)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        return CalculateMasteryLevelFromXP(profile != null ? profile.masteryXP : 0);
    }

    public int GetMasteryXPIntoCurrentLevel(TowerRole role)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        int remainingXP = profile != null ? Mathf.Max(0, profile.masteryXP) : 0;
        int level = 1;

        while (remainingXP >= GetXPToNextMasteryLevel(level))
        {
            remainingXP -= GetXPToNextMasteryLevel(level);
            level++;

            if (level > 999)
                break;
        }

        return remainingXP;
    }

    public int GetXPToNextMasteryLevel(int currentMasteryLevel)
    {
        return 100 + Mathf.Max(1, currentMasteryLevel) * 25;
    }

    public bool TrySetActiveKeystone(TowerRole role, string keystoneId)
    {
        if (!CanEditMetaProgression())
            return false;

        TowerMasteryRoleProfile profile = GetProfile(role);
        profile.activeKeystoneId = string.IsNullOrEmpty(keystoneId) ? "" : keystoneId;
        SaveProfile(profile);
        SyncLegacyBasicProfileIfNeeded(role, profile);
        PlayerPrefs.Save();
        return true;
    }

    public bool TrySpendRolePoints(TowerRole role, int cost)
    {
        int safeCost = Mathf.Max(0, cost);

        if (safeCost <= 0)
            return true;

        if (!CanEditMetaProgression())
            return false;

        TowerMasteryRoleProfile profile = GetProfile(role);

        if (profile.unspentPoints < safeCost)
            return false;

        profile.unspentPoints -= safeCost;
        profile.spentPoints += safeCost;
        SaveProfile(profile);
        SyncLegacyBasicProfileIfNeeded(role, profile);
        PlayerPrefs.Save();
        return true;
    }

    public void AddRoleMasteryXP(TowerRole role, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        if (IsMetaProgressionSuppressedForCurrentRun())
            return;

        if (IsRunActiveForPayout())
        {
            TowerMasteryRunState state = GetRunState(role);
            state.pendingRoleBonusXP += safeAmount;
            return;
        }

        AddMasteryXP(role, safeAmount);
    }

    public void SaveRoleProfile(TowerRole role)
    {
        if (IsRunActiveForPayout())
            return;

        SaveProfile(GetProfile(role));
        PlayerPrefs.Save();
    }

    public void SynchronizeRoleProfile(
        TowerRole role,
        int masteryXP,
        int unspentPoints,
        int spentPoints,
        int bestLevelEver,
        bool reachedLevel10,
        bool reachedLevel20,
        bool bossKillWithTower,
        bool chaos3WaveWithTower,
        bool chaos5BossOrEliteWithTower,
        string activeKeystoneId,
        int lastRunMasteryXPGained,
        int lastRunMasteryPointsGained)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        profile.masteryXP = Mathf.Max(0, masteryXP);
        profile.unspentPoints = Mathf.Max(0, unspentPoints);
        profile.spentPoints = Mathf.Max(0, spentPoints);
        profile.totalEarnedPoints = Mathf.Max(profile.totalEarnedPoints, profile.unspentPoints + profile.spentPoints);
        profile.bestLevelEver = Mathf.Max(1, bestLevelEver);
        profile.reachedLevel10 = reachedLevel10;
        profile.reachedLevel20 = reachedLevel20;
        profile.bossKillWithTower = bossKillWithTower;
        profile.chaos3WaveWithTower = chaos3WaveWithTower;
        profile.chaos5BossOrEliteWithTower = chaos5BossOrEliteWithTower;
        profile.activeKeystoneId = string.IsNullOrEmpty(activeKeystoneId) ? "" : activeKeystoneId;
        profile.lastRunMasteryXPGained = Mathf.Max(0, lastRunMasteryXPGained);
        profile.lastRunMasteryPointsGained = Mathf.Max(0, lastRunMasteryPointsGained);
        SaveProfile(profile);
        PlayerPrefs.Save();
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (tower == null)
            return;

        TowerMasteryRunState state = GetRunState(tower.towerRole);
        state.builtCountThisRun++;
        RecordTowerLevelReached(tower, tower.level);
    }

    public void RecordTowerDamage(Tower tower, float appliedDamage)
    {
        if (tower == null || appliedDamage <= 0f)
            return;

        MarkContribution(tower);
        GetRunState(tower.towerRole).damageThisRun += appliedDamage;
    }

    public void RecordTowerKill(Tower tower, EnemyRole killedRole)
    {
        if (tower == null)
            return;

        MarkContribution(tower);
        TowerMasteryRunState state = GetRunState(tower.towerRole);
        state.killsThisRun++;
    }

    public void RecordTowerAssist(Tower tower, EnemyRole assistedRole)
    {
        if (tower == null)
            return;

        MarkContribution(tower);
        TowerMasteryRunState state = GetRunState(tower.towerRole);
        state.assistsThisRun++;
    }

    public void RecordTowerLevelReached(Tower tower, int level)
    {
        if (tower == null || IsMetaProgressionSuppressedForCurrentRun())
            return;

        TowerRole role = tower.towerRole;
        TowerMasteryRoleProfile profile = GetProfile(role);
        TowerMasteryRunState state = GetRunState(role);
        int safeLevel = Mathf.Max(1, level);

        state.highestLevelThisRun = Mathf.Max(state.highestLevelThisRun, safeLevel);

        if (IsRunActiveForPayout())
            return;

        profile.bestLevelEver = Mathf.Max(profile.bestLevelEver, safeLevel);
        SaveProfile(profile);
        SyncLegacyBasicProfileIfNeeded(role, profile);
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        HashSet<TowerRole> contributingRoles = GatherContributingRoles(killingTower, contributors);

        foreach (TowerRole role in contributingRoles)
        {
            TowerMasteryRoleProfile profile = GetProfile(role);
            TowerMasteryRunState state = GetRunState(role);
            state.currentWaveHadContribution = true;

            if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
            {
                state.miniBossParticipationCount++;
                state.firstBossObjectiveThisRun = state.firstBossObjectiveThisRun || !profile.bossKillWithTower;
            }

            bool bossTarget = enemy.enemyRole == EnemyRole.Boss || enemy.isBoss;
            bool eliteTarget = enemy.enemyRole == EnemyRole.Elite || enemy.isElite;

            if (bossTarget)
            {
                state.bossParticipationCount++;
                state.firstBossObjectiveThisRun = state.firstBossObjectiveThisRun || !profile.bossKillWithTower;
            }

            if (bossTarget && GetCurrentChaosLevel() >= 5)
            {
                state.firstChaos5ObjectiveThisRun = state.firstChaos5ObjectiveThisRun || !profile.chaos5BossOrEliteWithTower;
            }

            if ((bossTarget || eliteTarget) && GetCurrentChaosLevel() >= 5)
                state.firstChaos5ObjectiveThisRun = state.firstChaos5ObjectiveThisRun || !profile.chaos5BossOrEliteWithTower;

            if (eliteTarget)
                state.bossParticipationCount++;
        }
    }

    public string GetRoleListStateText(TowerRole role)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        return "Mastery Lv " + GetMasteryLevel(role) +
               " | bester Tower " + profile.bestLevelEver +
               " | Punkte " + profile.unspentPoints +
               " frei | " + profile.spentPoints +
               " ausgegeben | Keystone " + GetActiveKeystoneDisplayText(profile);
    }

    public string GetRoleOverviewText(TowerRole role)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        StringBuilder builder = new StringBuilder();
        int masteryLevel = GetMasteryLevel(role);
        int xpIntoLevel = GetMasteryXPIntoCurrentLevel(role);
        int xpToNext = GetXPToNextMasteryLevel(masteryLevel);

        builder.AppendLine(GetTowerDisplayName(role) + " Mastery");
        builder.AppendLine("Mastery Level: " + masteryLevel + " | XP " + xpIntoLevel + " / " + xpToNext + " | Gesamt-XP " + profile.masteryXP);
        builder.AppendLine("Punkte: " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben | " + profile.totalEarnedPoints + " verdient");
        builder.AppendLine("Bester Tower im Run/Ewig: " + GetHighestLevelThisRun(role) + " / " + profile.bestLevelEver);
        builder.AppendLine("Aktiver Keystone: " + GetActiveKeystoneDisplayText(profile));
        builder.AppendLine("Visual Mastery Tier: " + GetMasteryVisualTier(role));
        builder.AppendLine("Letzter Run: +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP | Level " + profile.lastRunHighestLevel + " | Tower-XP " + profile.lastRunTowerXP + " | Impact " + profile.lastRunImpactScore);
        builder.AppendLine("Letzte Point-Tiers: Level " + profile.lastRunLevelTier + " | Wave " + profile.lastRunWaveTier + " | Impact " + profile.lastRunImpactTier);
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- I: " + GetMilestoneProgressText(role, TowerMasteryMilestone.I));
        builder.AppendLine("- II: " + GetMilestoneProgressText(role, TowerMasteryMilestone.II));
        builder.AppendLine("- III: " + GetMilestoneProgressText(role, TowerMasteryMilestone.III));
        builder.AppendLine("- IV: " + GetMilestoneProgressText(role, TowerMasteryMilestone.IV));
        builder.AppendLine("- V: " + GetMilestoneProgressText(role, TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Globale Struktur:");
        builder.AppendLine("- Linearer Einstieg mit kleinen Stats, XP-/Kosten-/Rollenverbesserungen.");
        builder.AppendLine("- Milestone I oeffnet drei Pfade.");
        builder.AppendLine("- Pfad A: Hauptrolle / Kernschaden.");
        builder.AppendLine("- Pfad B: alternative Kampfrolle.");
        builder.AppendLine("- Pfad C: Utility / Support / Synergie.");
        builder.AppendLine("- Milestones II-V oeffnen tiefere Abschnitte.");
        builder.AppendLine("- Drei Keystones koennen freigeschaltet werden, aber nur einer ist aktiv.");
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt: min(LevelTier, WaveTier, ImpactTier, 5). Es zaehlt pro Rolle nur der beste Tower-Level, aber alle Kills/Assists/Damage dieser Rolle fuer den Impact.");

        return builder.ToString();
    }

    public string GetGlobalRulesText()
    {
        return "Progression zaehlt pro TowerRole, nicht pro einzelner Instanz.\n\n" +
               "Run-Ende-Auszahlung:\n" +
               "- Run-Tower-XP bleibt nur Run-Fortschritt und wird erst am Run-Ende in Mastery XP umgerechnet.\n" +
               "- Tower Mastery Points entstehen durch Level, Run-Tiefe und echte Beteiligung gleichzeitig.\n" +
               "- levelTier = floor(hoechster Tower-Level / 10).\n" +
               "- waveTier = floor(hoechste abgeschlossene Wave / 5).\n" +
               "- impactTier = floor((Kills x 6 + Assists x 3 + Damage / 50) / 25).\n" +
               "- earnedPoints = min(levelTier, waveTier, impactTier, 5).\n" +
               "- Vorbereitete Meta-Punkte im Run sind nur Anzeige/Debug, nicht die finale Waehrung.\n\n" +
               "Mastery XP:\n" +
               "- Basis: 35% der Run-Tower-XP, pro Rolle gedeckelt.\n" +
               "- +15 XP pro verdientem Mastery Point.\n" +
               "- Boss-, Chaos-, Variant- und rollenbezogene Bonus-XP werden am Run-Ende addiert und gecappt.\n" +
               "- First-Time-Boni gibt es fuer Level 10/20/30/40/50, erste Boss-Beteiligung, erste Chaos-3-Wave und Chaos-5/Elite-Ziele.\n\n" +
               "Kostenmodell:\n" +
               "- Mehrstufige Rangs: 1 / 1 / 2 / 2 / 3 / 3 ... Punkte.\n" +
               "- Einmalige Utility-Knoten behalten feste Kosten.\n" +
               "- Normale Faehigkeiten behalten feste Kosten.\n" +
               "- Starke Faehigkeiten behalten feste Kosten.\n" +
               "- Keystones: 15-25 Punkte.\n\n" +
               "Milestones gelten fuer alle Tower:\n" +
               "- I: 10 Punkte ausgegeben + Mastery-Level 3 + Level 10.\n" +
               "- II: 25 Punkte + Mastery-Level 8 + Level 20.\n" +
               "- III: 50 Punkte + Mastery-Level 15 + Boss/MiniBoss oder rollenpassende Spezialleistung.\n" +
               "- IV: 85 Punkte + Mastery-Level 25 + Chaos-3-Wave-Beteiligung.\n" +
               "- V: 130 Punkte + Mastery-Level 40 + Chaos-5-Boss oder spaeter Elite-Ziel.\n\n" +
               "Keystone-Regel: Pro Tower-Typ ist nur ein Keystone gleichzeitig aktiv. Kleine und mittlere Knoten bleiben passiv aktiv.";
    }

    public string GetCompactSummaryText()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Tower Mastery:");

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = GetProfile(role);

            if (profile.unspentPoints <= 0 && profile.lastRunMasteryPointsGained <= 0)
                continue;

            builder.Append(" ");
            builder.Append(role);
            builder.Append(" ");
            builder.Append(profile.unspentPoints);
            builder.Append(" frei");

            if (profile.lastRunMasteryPointsGained > 0)
                builder.Append(" (+").Append(profile.lastRunMasteryPointsGained).Append(")");

            builder.Append(" |");
        }

        if (builder[builder.Length - 1] == ':')
            builder.Append(" vorbereitet");

        return builder.ToString().TrimEnd('|', ' ');
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        highestWaveReachedThisRun = Mathf.Max(highestWaveReachedThisRun, currentWaveNumber);

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRunState state = GetRunState(role);
            state.currentWaveHadContribution = false;
        }
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || IsMetaProgressionSuppressedForCurrentRun())
            return;

        highestWaveReachedThisRun = Mathf.Max(highestWaveReachedThisRun, result.waveNumber);
        highestCompletedWaveThisRun = Mathf.Max(highestCompletedWaveThisRun, result.waveNumber);

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRunState state = GetRunState(role);

            if (!state.currentWaveHadContribution)
                continue;

            TowerMasteryRoleProfile profile = GetProfile(role);

            if (result.chaosLevelAtWaveStart > 0 || result.hadChaosWaveBlocksAtWaveStart || result.chaosVariantSpawnCount > 0)
            {
                state.chaosWaveParticipationCount++;
                state.chaosVariantContributionCount += Mathf.Max(0, result.chaosVariantKilledCount);
            }

            if (result.chaosLevelAtWaveStart >= 3)
                state.firstChaos3ObjectiveThisRun = state.firstChaos3ObjectiveThisRun || !profile.chaos3WaveWithTower;
        }
    }

    private void HandleGameOverTriggered()
    {
        FinalizeRun();
    }

    private void MarkContribution(Tower tower)
    {
        if (tower == null)
            return;

        GetRunState(tower.towerRole).currentWaveHadContribution = true;
        RecordTowerLevelReached(tower, tower.level);
    }

    private void RefreshHighestLevelsFromActiveTowers()
    {
        var towers = TowerRegistry.ActiveTowers;

        foreach (Tower tower in towers)
        {
            if (tower == null)
                continue;

            RecordTowerLevelReached(tower, tower.level);
        }
    }

    private void AddMasteryXP(TowerRole role, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        if (IsMetaProgressionSuppressedForCurrentRun())
            return;

        if (IsRunActiveForPayout())
        {
            GetRunState(role).pendingRoleBonusXP += safeAmount;
            return;
        }

        TowerMasteryRoleProfile profile = GetProfile(role);
        profile.masteryXP += safeAmount;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager != null && gameManager.gameStarted && !runFinalized)
            profile.lastRunMasteryXPGained += safeAmount;

        SaveProfile(profile);
    }

    public bool IsMetaProgressionSuppressedForCurrentRun()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager != null && gameManager.IsMetaProgressionSuppressedForCurrentRun();
    }

    private HashSet<TowerRole> GatherContributingRoles(Tower killingTower, IEnumerable<Tower> contributors)
    {
        HashSet<TowerRole> roles = new HashSet<TowerRole>();

        if (killingTower != null)
            roles.Add(killingTower.towerRole);

        if (contributors == null)
            return roles;

        foreach (Tower tower in contributors)
        {
            if (tower != null)
                roles.Add(tower.towerRole);
        }

        return roles;
    }

    private TowerMasteryRoleRunSummary BuildRoleRunSummary(TowerRole role)
    {
        TowerMasteryRunState state = GetRunState(role);
        TowerMasteryRoleRunSummary summary = new TowerMasteryRoleRunSummary
        {
            role = role,
            highestLevel = Mathf.Max(1, state.highestLevelThisRun),
            totalTowerXP = Mathf.Max(0, state.towerXPGainedThisRun),
            kills = Mathf.Max(0, state.killsThisRun),
            assists = Mathf.Max(0, state.assistsThisRun),
            damage = Mathf.Max(0f, state.damageThisRun),
            builtCount = Mathf.Max(0, state.builtCountThisRun),
            metaPointsPrepared = Mathf.Max(0, state.metaPointsPreparedThisRun),
            highestVisualTier = Mathf.Max(0, state.highestVisualTierThisRun),
            miniBossParticipationCount = Mathf.Max(0, state.miniBossParticipationCount),
            bossParticipationCount = Mathf.Max(0, state.bossParticipationCount),
            chaosWaveParticipationCount = Mathf.Max(0, state.chaosWaveParticipationCount),
            chaosVariantContributionCount = Mathf.Max(0, state.chaosVariantContributionCount),
            pendingRoleBonusXP = Mathf.Max(0, state.pendingRoleBonusXP),
            firstBossObjectiveThisRun = state.firstBossObjectiveThisRun,
            firstChaos3ObjectiveThisRun = state.firstChaos3ObjectiveThisRun,
            firstChaos5ObjectiveThisRun = state.firstChaos5ObjectiveThisRun
        };

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats == null || stats.towerRecords == null)
            return summary;

        int recordTowerXP = 0;
        int recordKills = 0;
        int recordAssists = 0;
        float recordDamage = 0f;
        int recordBuiltCount = 0;
        int recordMetaPoints = 0;
        int recordHighestVisualTier = 0;
        int recordHighestLevel = 1;

        foreach (RunTowerStatsRecord record in stats.towerRecords)
        {
            if (record == null || record.towerRole != role)
                continue;

            record.RefreshFromTower();
            recordHighestLevel = Mathf.Max(recordHighestLevel, record.highestLevel);
            recordTowerXP += Mathf.Max(0, record.towerXPGained);
            recordKills += Mathf.Max(0, record.totalKills);
            recordAssists += Mathf.Max(0, record.totalAssists);
            recordDamage += Mathf.Max(0f, record.totalDamageDealt);
            recordBuiltCount += record.wasRegisteredAsBuilt ? 1 : 0;
            recordMetaPoints += Mathf.Max(0, record.metaPointsGained);
            recordHighestVisualTier = Mathf.Max(recordHighestVisualTier, record.highestVisualTier);
        }

        summary.highestLevel = Mathf.Max(summary.highestLevel, recordHighestLevel);
        summary.totalTowerXP = Mathf.Max(summary.totalTowerXP, recordTowerXP);
        summary.kills = Mathf.Max(summary.kills, recordKills);
        summary.assists = Mathf.Max(summary.assists, recordAssists);
        summary.damage = Mathf.Max(summary.damage, recordDamage);
        summary.builtCount = Mathf.Max(summary.builtCount, recordBuiltCount);
        summary.metaPointsPrepared = Mathf.Max(summary.metaPointsPrepared, recordMetaPoints);
        summary.highestVisualTier = Mathf.Max(summary.highestVisualTier, recordHighestVisualTier);
        return summary;
    }

    private int CalculateRoleImpact(TowerMasteryRoleRunSummary summary)
    {
        if (summary == null)
            return 0;

        float damageScore = damageImpactDivisor <= 0f ? 0f : summary.damage / damageImpactDivisor;
        return Mathf.Max(0, Mathf.FloorToInt(summary.kills * killImpact + summary.assists * assistImpact + damageScore));
    }

    private int CalculateRunEndMasteryXP(TowerMasteryRoleProfile profile, TowerMasteryRoleRunSummary summary, int earnedPoints)
    {
        if (profile == null || summary == null)
            return 0;

        int baseXP = Mathf.Min(
            Mathf.Max(0, Mathf.FloorToInt(summary.totalTowerXP * Mathf.Max(0f, towerXPToMasteryXPConversion))),
            Mathf.Max(0, maxBaseMasteryXPFromTowerXP)
        );

        int pointXP = Mathf.Max(0, earnedPoints) * Mathf.Max(0, masteryXPPerEarnedPoint);
        int bossXP = Mathf.Min(
            summary.miniBossParticipationCount * Mathf.Max(0, miniBossParticipationXP) + summary.bossParticipationCount * Mathf.Max(0, bossParticipationXP),
            Mathf.Max(0, maxBossMasteryXPPerRun)
        );
        int chaosWaveXP = Mathf.Min(summary.chaosWaveParticipationCount * Mathf.Max(0, chaosWaveParticipationXP), Mathf.Max(0, maxChaosWaveMasteryXPPerRun));
        int chaosVariantXP = Mathf.Min(summary.chaosVariantContributionCount * Mathf.Max(0, chaosVariantContributionXP), Mathf.Max(0, maxChaosVariantMasteryXPPerRun));
        int roleBonusXP = Mathf.Min(summary.pendingRoleBonusXP, Mathf.Max(0, maxRoleBonusMasteryXPPerRun));
        int firstTimeXP = CalculateFirstTimeMasteryXP(profile, summary);

        return Mathf.Max(0, baseXP + pointXP + bossXP + chaosWaveXP + chaosVariantXP + roleBonusXP + firstTimeXP);
    }

    private int CalculateFirstTimeMasteryXP(TowerMasteryRoleProfile profile, TowerMasteryRoleRunSummary summary)
    {
        if (profile == null || summary == null)
            return 0;

        int xp = 0;

        if (summary.highestLevel >= 10 && !profile.reachedLevel10)
            xp += Mathf.Max(0, firstLevel10MasteryXP);

        if (summary.highestLevel >= 20 && !profile.reachedLevel20)
            xp += Mathf.Max(0, firstLevel20MasteryXP);

        if (summary.highestLevel >= 30 && !profile.reachedLevel30)
            xp += Mathf.Max(0, firstLevel30MasteryXP);

        if (summary.highestLevel >= 40 && !profile.reachedLevel40)
            xp += Mathf.Max(0, firstLevel40MasteryXP);

        if (summary.highestLevel >= 50 && !profile.reachedLevel50)
            xp += Mathf.Max(0, firstLevel50MasteryXP);

        if (summary.firstBossObjectiveThisRun)
            xp += Mathf.Max(0, firstBossParticipationXP);

        if (summary.firstChaos3ObjectiveThisRun)
            xp += Mathf.Max(0, firstChaos3WaveXP);

        if (summary.firstChaos5ObjectiveThisRun)
            xp += Mathf.Max(0, firstChaos5BossOrEliteXP);

        return xp;
    }

    private void ApplyRunEndMilestoneProgress(TowerMasteryRoleProfile profile, TowerMasteryRoleRunSummary summary)
    {
        if (profile == null || summary == null)
            return;

        profile.bestLevelEver = Mathf.Max(profile.bestLevelEver, summary.highestLevel);

        if (summary.highestLevel >= 10)
            profile.reachedLevel10 = true;

        if (summary.highestLevel >= 20)
            profile.reachedLevel20 = true;

        if (summary.highestLevel >= 30)
            profile.reachedLevel30 = true;

        if (summary.highestLevel >= 40)
            profile.reachedLevel40 = true;

        if (summary.highestLevel >= 50)
            profile.reachedLevel50 = true;

        if (summary.miniBossParticipationCount > 0 || summary.bossParticipationCount > 0 || IsRoleSpecificMilestoneThreeComplete(summary))
            profile.bossKillWithTower = true;

        if (summary.chaosWaveParticipationCount > 0 && summary.firstChaos3ObjectiveThisRun)
            profile.chaos3WaveWithTower = true;

        if (summary.firstChaos5ObjectiveThisRun)
            profile.chaos5BossOrEliteWithTower = true;
    }

    private bool IsRoleSpecificMilestoneThreeComplete(TowerMasteryRoleRunSummary summary)
    {
        if (summary == null)
            return false;

        switch (summary.role)
        {
            case TowerRole.Basic:
                return summary.highestLevel >= 30;
            case TowerRole.Rapid:
                return summary.kills + summary.assists >= 100;
            case TowerRole.Heavy:
                return summary.kills + summary.assists >= 50 && summary.damage >= 1000f;
            case TowerRole.Fire:
                return summary.kills + summary.assists >= 100;
            case TowerRole.Slow:
                return summary.assists >= 250;
            default:
                return false;
        }
    }

    private RunStatisticsTracker GetRunStatisticsTracker()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager != null ? gameManager.GetRunStatisticsTracker() : FindObjectOfType<RunStatisticsTracker>();
    }

    private void SyncRunStateFromStatistics()
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats == null || stats.towerRecords == null)
            return;

        foreach (RunTowerStatsRecord record in stats.towerRecords)
        {
            if (record == null)
                continue;

            record.RefreshFromTower();
            TowerMasteryRunState state = GetRunState(record.towerRole);
            state.highestLevelThisRun = Mathf.Max(state.highestLevelThisRun, record.highestLevel);
            state.towerXPGainedThisRun = Mathf.Max(state.towerXPGainedThisRun, record.towerXPGained);
            state.killsThisRun = Mathf.Max(state.killsThisRun, record.totalKills);
            state.assistsThisRun = Mathf.Max(state.assistsThisRun, record.totalAssists);
            state.damageThisRun = Mathf.Max(state.damageThisRun, record.totalDamageDealt);
            state.metaPointsPreparedThisRun = Mathf.Max(state.metaPointsPreparedThisRun, record.metaPointsGained);
            state.highestVisualTierThisRun = Mathf.Max(state.highestVisualTierThisRun, record.highestVisualTier);
        }
    }

    private bool IsRunActiveForPayout()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager != null && gameManager.gameStarted && !runFinalized;
    }

    private int CalculateMasteryLevelFromXP(int masteryXP)
    {
        int remainingXP = Mathf.Max(0, masteryXP);
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

    private TowerMasteryRunState GetRunState(TowerRole role)
    {
        if (!runStateByRole.TryGetValue(role, out TowerMasteryRunState state) || state == null)
        {
            state = new TowerMasteryRunState();
            runStateByRole[role] = state;
        }

        return state;
    }

    private void SyncLegacyBasicProfileIfNeeded(TowerRole role, TowerMasteryRoleProfile profile)
    {
        if (role != TowerRole.Basic || profile == null)
            return;

        if (BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager basicMastery))
            basicMastery.ApplyGlobalTowerMasteryProfile(profile);
    }

    private string GetMilestoneProgressText(TowerRole role, TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        bool unlocked = IsMilestoneUnlocked(role, milestone);
        string state = unlocked ? "frei" : "gesperrt";
        int masteryLevel = GetMasteryLevel(role);

        switch (milestone)
        {
            case TowerMasteryMilestone.I:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 10) + "/10 | Mastery Lv " + Mathf.Min(masteryLevel, 3) + "/3 | Level 10 " + (profile.reachedLevel10 ? "ja" : "nein");
            case TowerMasteryMilestone.II:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 25) + "/25 | Mastery Lv " + Mathf.Min(masteryLevel, 8) + "/8 | Level 20 " + (profile.reachedLevel20 ? "ja" : "nein");
            case TowerMasteryMilestone.III:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | Mastery Lv " + Mathf.Min(masteryLevel, 15) + "/15 | Spezial/Boss " + (profile.bossKillWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.IV:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 85) + "/85 | Mastery Lv " + Mathf.Min(masteryLevel, 25) + "/25 | Chaos 3 " + (profile.chaos3WaveWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.V:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 130) + "/130 | Mastery Lv " + Mathf.Min(masteryLevel, 40) + "/40 | Chaos 5/Elite " + (profile.chaos5BossOrEliteWithTower ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private string GetActiveKeystoneDisplayText(TowerMasteryRoleProfile profile)
    {
        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return "Keiner";

        return profile.activeKeystoneId;
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

    private void EnsureAllProfiles()
    {
        profileByRole.Clear();

        if (roleProfiles == null)
            roleProfiles = new List<TowerMasteryRoleProfile>();

        foreach (TowerMasteryRoleProfile profile in roleProfiles)
        {
            if (profile == null)
                continue;

            if (!profileByRole.ContainsKey(profile.towerRole))
                profileByRole.Add(profile.towerRole, profile);
        }

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            if (profileByRole.ContainsKey(role))
                continue;

            TowerMasteryRoleProfile profile = new TowerMasteryRoleProfile
            {
                towerRole = role,
                bestLevelEver = 1
            };

            roleProfiles.Add(profile);
            profileByRole.Add(role, profile);
        }
    }

    private void LoadProfiles()
    {
        foreach (TowerRole role in GetOrderedTowerRoles())
            LoadProfile(GetProfile(role));
    }

    private void LoadProfile(TowerMasteryRoleProfile profile)
    {
        if (profile == null)
            return;

        string prefix = GetProfilePrefix(profile.towerRole);
        profile.masteryXP = PlayerPrefs.GetInt(prefix + "XP", 0);
        profile.unspentPoints = PlayerPrefs.GetInt(prefix + "UnspentPoints", 0);
        profile.spentPoints = PlayerPrefs.GetInt(prefix + "SpentPoints", 0);
        profile.totalEarnedPoints = PlayerPrefs.GetInt(prefix + "TotalEarnedPoints", profile.unspentPoints + profile.spentPoints);
        profile.bestLevelEver = PlayerPrefs.GetInt(prefix + "BestLevel", 1);
        profile.reachedLevel10 = PlayerPrefs.GetInt(prefix + "ReachedLevel10", 0) == 1;
        profile.reachedLevel20 = PlayerPrefs.GetInt(prefix + "ReachedLevel20", 0) == 1;
        profile.reachedLevel30 = PlayerPrefs.GetInt(prefix + "ReachedLevel30", 0) == 1;
        profile.reachedLevel40 = PlayerPrefs.GetInt(prefix + "ReachedLevel40", 0) == 1;
        profile.reachedLevel50 = PlayerPrefs.GetInt(prefix + "ReachedLevel50", 0) == 1;
        profile.bossKillWithTower = PlayerPrefs.GetInt(prefix + "Boss", 0) == 1;
        profile.chaos3WaveWithTower = PlayerPrefs.GetInt(prefix + "Chaos3", 0) == 1;
        profile.chaos5BossOrEliteWithTower = PlayerPrefs.GetInt(prefix + "Chaos5", 0) == 1;
        profile.activeKeystoneId = PlayerPrefs.GetString(prefix + "ActiveKeystone", "");

        if (profile.nodeStates == null)
            profile.nodeStates = new List<TowerMasteryNodeState>();
    }

    private void SaveProfile(TowerMasteryRoleProfile profile)
    {
        if (profile == null)
            return;

        string prefix = GetProfilePrefix(profile.towerRole);
        profile.totalEarnedPoints = Mathf.Max(profile.totalEarnedPoints, profile.unspentPoints + profile.spentPoints);
        PlayerPrefs.SetInt(prefix + "XP", Mathf.Max(0, profile.masteryXP));
        PlayerPrefs.SetInt(prefix + "UnspentPoints", Mathf.Max(0, profile.unspentPoints));
        PlayerPrefs.SetInt(prefix + "SpentPoints", Mathf.Max(0, profile.spentPoints));
        PlayerPrefs.SetInt(prefix + "TotalEarnedPoints", Mathf.Max(0, profile.totalEarnedPoints));
        PlayerPrefs.SetInt(prefix + "BestLevel", Mathf.Max(1, profile.bestLevelEver));
        PlayerPrefs.SetInt(prefix + "ReachedLevel10", profile.reachedLevel10 ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "ReachedLevel20", profile.reachedLevel20 ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "ReachedLevel30", profile.reachedLevel30 ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "ReachedLevel40", profile.reachedLevel40 ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "ReachedLevel50", profile.reachedLevel50 ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "Boss", profile.bossKillWithTower ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "Chaos3", profile.chaos3WaveWithTower ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "Chaos5", profile.chaos5BossOrEliteWithTower ? 1 : 0);
        PlayerPrefs.SetString(prefix + "ActiveKeystone", string.IsNullOrEmpty(profile.activeKeystoneId) ? "" : profile.activeKeystoneId);
    }

    private string GetProfilePrefix(TowerRole role)
    {
        return PlayerPrefsPrefix + role + "_";
    }
}
