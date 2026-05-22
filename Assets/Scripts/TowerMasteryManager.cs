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
    public int bestLevelEver = 1;
    public bool reachedLevel10 = false;
    public bool reachedLevel20 = false;
    public bool bossKillWithTower = false;
    public bool chaos3WaveWithTower = false;
    public bool chaos5BossOrEliteWithTower = false;
    public string activeKeystoneId = "";
    public int lastRunMasteryXPGained = 0;
    public int lastRunMasteryPointsGained = 0;
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

    private class TowerMasteryRunState
    {
        public int highestLevelThisRun = 1;
        public bool currentWaveHadContribution = false;
        public float currentWaveDamageMasteryXP = 0f;
        public float damageMasteryXPFraction = 0f;
        public readonly HashSet<int> awardedLevelMilestonesThisRun = new HashSet<int>();
    }

    private readonly Dictionary<TowerRole, TowerMasteryRoleProfile> profileByRole = new Dictionary<TowerRole, TowerMasteryRoleProfile>();
    private readonly Dictionary<TowerRole, TowerMasteryRunState> runStateByRole = new Dictionary<TowerRole, TowerMasteryRunState>();
    private int currentWaveNumber = 0;
    private int highestWaveReachedThisRun = 0;
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
            default: return role.ToString();
        }
    }

    public static int GetRepeatingUpgradeCostForRank(int nextRank)
    {
        return Mathf.Clamp(nextRank, 1, 5);
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
        runFinalized = false;

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRunState state = GetRunState(role);
            state.highestLevelThisRun = 1;
            state.currentWaveHadContribution = false;
            state.currentWaveDamageMasteryXP = 0f;
            state.damageMasteryXPFraction = 0f;
            state.awardedLevelMilestonesThisRun.Clear();

            TowerMasteryRoleProfile profile = GetProfile(role);
            profile.lastRunMasteryXPGained = 0;
            profile.lastRunMasteryPointsGained = 0;
        }
    }

    public void FinalizeRun()
    {
        if (runFinalized)
            return;

        runFinalized = true;
        RefreshHighestLevelsFromActiveTowers();

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = GetProfile(role);
            TowerMasteryRunState state = GetRunState(role);

            if (highestWaveReachedThisRun < Mathf.Max(1, minimumWaveForPointPayout))
            {
                SaveProfile(profile);
                continue;
            }

            int points = Mathf.Clamp(state.highestLevelThisRun / 10, 0, Mathf.Max(0, maxPointsPerRun));

            if (points > 0)
            {
                profile.unspentPoints += points;
                profile.lastRunMasteryPointsGained = points;
                Debug.Log(GetTowerDisplayName(role) + " Mastery: +" + points + " Punkt(e) fuer hoechsten Tower Level " + state.highestLevelThisRun + ".");
            }

            SaveProfile(profile);
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

        switch (milestone)
        {
            case TowerMasteryMilestone.None:
                return true;
            case TowerMasteryMilestone.I:
                return profile.spentPoints >= 10 && profile.reachedLevel10;
            case TowerMasteryMilestone.II:
                return profile.spentPoints >= 25 && profile.reachedLevel20;
            case TowerMasteryMilestone.III:
                return profile.spentPoints >= 50 && profile.bossKillWithTower;
            case TowerMasteryMilestone.IV:
                return profile.spentPoints >= 85 && profile.chaos3WaveWithTower;
            case TowerMasteryMilestone.V:
                return profile.spentPoints >= 130 && profile.chaos5BossOrEliteWithTower;
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

    public bool TrySetActiveKeystone(TowerRole role, string keystoneId)
    {
        if (!CanEditMetaProgression())
            return false;

        TowerMasteryRoleProfile profile = GetProfile(role);
        profile.activeKeystoneId = string.IsNullOrEmpty(keystoneId) ? "" : keystoneId;
        SaveProfile(profile);
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
        PlayerPrefs.Save();
        return true;
    }

    public void AddRoleMasteryXP(TowerRole role, int amount)
    {
        AddMasteryXP(role, amount);
    }

    public void SaveRoleProfile(TowerRole role)
    {
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

        RecordTowerLevelReached(tower, tower.level);
    }

    public void RecordTowerDamage(Tower tower, float appliedDamage)
    {
        if (tower == null || appliedDamage <= 0f)
            return;

        TowerRole role = tower.towerRole;
        MarkContribution(tower);

        TowerMasteryRunState state = GetRunState(role);

        if (state.currentWaveDamageMasteryXP >= maxDamageMasteryXPPerWave)
            return;

        state.damageMasteryXPFraction += appliedDamage * Mathf.Max(0f, damageToMasteryXPRatio);
        int wholeXP = Mathf.FloorToInt(state.damageMasteryXPFraction);

        if (wholeXP <= 0)
            return;

        int remainingCap = Mathf.Max(0, maxDamageMasteryXPPerWave - Mathf.FloorToInt(state.currentWaveDamageMasteryXP));
        int awarded = Mathf.Min(wholeXP, remainingCap);

        if (awarded <= 0)
            return;

        state.damageMasteryXPFraction -= awarded;
        state.currentWaveDamageMasteryXP += awarded;
        AddMasteryXP(role, awarded);
    }

    public void RecordTowerKill(Tower tower, EnemyRole killedRole)
    {
        if (tower == null)
            return;

        MarkContribution(tower);
        AddMasteryXP(tower.towerRole, killMasteryXP);
    }

    public void RecordTowerAssist(Tower tower, EnemyRole assistedRole)
    {
        if (tower == null)
            return;

        MarkContribution(tower);
        AddMasteryXP(tower.towerRole, assistMasteryXP);
    }

    public void RecordTowerLevelReached(Tower tower, int level)
    {
        if (tower == null)
            return;

        TowerRole role = tower.towerRole;
        TowerMasteryRoleProfile profile = GetProfile(role);
        TowerMasteryRunState state = GetRunState(role);
        int safeLevel = Mathf.Max(1, level);

        state.highestLevelThisRun = Mathf.Max(state.highestLevelThisRun, safeLevel);
        profile.bestLevelEver = Mathf.Max(profile.bestLevelEver, safeLevel);

        if (safeLevel >= 10)
            profile.reachedLevel10 = true;

        if (safeLevel >= 20)
            profile.reachedLevel20 = true;

        int milestone = safeLevel / 10 * 10;

        if (milestone >= 10 && milestone <= 50 && !state.awardedLevelMilestonesThisRun.Contains(milestone))
        {
            state.awardedLevelMilestonesThisRun.Add(milestone);
            AddMasteryXP(role, milestone / 10 * 15);
        }

        SaveProfile(profile);
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
                AddMasteryXP(role, GetMiniBossParticipationXP(role));

            bool bossTarget = enemy.enemyRole == EnemyRole.Boss || enemy.isBoss;
            bool eliteTarget = enemy.enemyRole == EnemyRole.Elite || enemy.isElite;

            if (bossTarget)
            {
                profile.bossKillWithTower = true;
                AddMasteryXP(role, GetBossParticipationXP(role));
            }

            if (bossTarget && GetCurrentChaosLevel() >= 5)
            {
                profile.chaos5BossOrEliteWithTower = true;
                AddMasteryXP(role, GetChaos5BossParticipationXP(role));
            }

            if ((bossTarget || eliteTarget) && GetCurrentChaosLevel() >= 5)
                profile.chaos5BossOrEliteWithTower = true;

            SaveProfile(profile);
        }

        PlayerPrefs.Save();
    }

    public string GetRoleListStateText(TowerRole role)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        return "Lv " + profile.bestLevelEver +
               " | Punkte " + profile.unspentPoints +
               " frei | " + profile.spentPoints +
               " ausgegeben | Keystone " + GetActiveKeystoneDisplayText(profile);
    }

    public string GetRoleOverviewText(TowerRole role)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(GetTowerDisplayName(role) + " Mastery");
        builder.AppendLine("Mastery XP: " + profile.masteryXP);
        builder.AppendLine("Punkte: " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben");
        builder.AppendLine("Bester Tower im Run/Ewig: " + GetHighestLevelThisRun(role) + " / " + profile.bestLevelEver);
        builder.AppendLine("Aktiver Keystone: " + GetActiveKeystoneDisplayText(profile));
        builder.AppendLine("Visual Mastery Tier: " + GetMasteryVisualTier(role));
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
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Tower dieser Rolle im Run.");

        return builder.ToString();
    }

    public string GetGlobalRulesText()
    {
        return "Progression zaehlt pro TowerRole, nicht pro einzelner Instanz.\n\n" +
               "Kostenmodell:\n" +
               "- Wiederholbare Rangs: 1 / 2 / 3 / 4 / 5 Punkte.\n" +
               "- Kleine Utility-Knoten: 2-3 Punkte.\n" +
               "- Normale Faehigkeiten: 4-6 Punkte.\n" +
               "- Starke Faehigkeiten: 8-12 Punkte.\n" +
               "- Keystones: 15-25 Punkte.\n\n" +
               "Milestones gelten fuer alle Tower:\n" +
               "- I: 10 Punkte ausgegeben + Level 10.\n" +
               "- II: 25 Punkte + Level 20.\n" +
               "- III: 50 Punkte + Bossbeteiligung.\n" +
               "- IV: 85 Punkte + Chaos-3-Wave-Beteiligung.\n" +
               "- V: 130 Punkte + Chaos-5-Boss oder spaeter Elite-Ziel.\n\n" +
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
            state.currentWaveDamageMasteryXP = 0f;
            state.damageMasteryXPFraction = 0f;
        }
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted)
            return;

        highestWaveReachedThisRun = Mathf.Max(highestWaveReachedThisRun, result.waveNumber);

        foreach (TowerRole role in GetOrderedTowerRoles())
        {
            TowerMasteryRunState state = GetRunState(role);

            if (!state.currentWaveHadContribution)
                continue;

            TowerMasteryRoleProfile profile = GetProfile(role);

            if (result.chaosLevelAtWaveStart > 0 || result.hadChaosWaveBlocksAtWaveStart || result.chaosVariantSpawnCount > 0)
                AddMasteryXP(role, chaosWaveParticipationXP);

            if (result.chaosLevelAtWaveStart >= 3)
                profile.chaos3WaveWithTower = true;

            SaveProfile(profile);
        }

        PlayerPrefs.Save();
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
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

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

        TowerMasteryRoleProfile profile = GetProfile(role);
        profile.masteryXP += safeAmount;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager != null && gameManager.gameStarted && !runFinalized)
            profile.lastRunMasteryXPGained += safeAmount;

        SaveProfile(profile);
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

    private int GetMiniBossParticipationXP(TowerRole role)
    {
        return role == TowerRole.Rapid || role == TowerRole.Fire ? Mathf.Max(1, miniBossParticipationXP / 2) : miniBossParticipationXP;
    }

    private int GetBossParticipationXP(TowerRole role)
    {
        return role == TowerRole.Rapid || role == TowerRole.Fire ? Mathf.Max(1, bossParticipationXP / 2) : bossParticipationXP;
    }

    private int GetChaos5BossParticipationXP(TowerRole role)
    {
        return role == TowerRole.Rapid || role == TowerRole.Fire ? Mathf.Max(1, chaos5BossParticipationXP / 2) : chaos5BossParticipationXP;
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

    private string GetMilestoneProgressText(TowerRole role, TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetProfile(role);
        bool unlocked = IsMilestoneUnlocked(role, milestone);
        string state = unlocked ? "frei" : "gesperrt";

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
        profile.bestLevelEver = PlayerPrefs.GetInt(prefix + "BestLevel", 1);
        profile.reachedLevel10 = PlayerPrefs.GetInt(prefix + "ReachedLevel10", 0) == 1;
        profile.reachedLevel20 = PlayerPrefs.GetInt(prefix + "ReachedLevel20", 0) == 1;
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
        PlayerPrefs.SetInt(prefix + "XP", Mathf.Max(0, profile.masteryXP));
        PlayerPrefs.SetInt(prefix + "UnspentPoints", Mathf.Max(0, profile.unspentPoints));
        PlayerPrefs.SetInt(prefix + "SpentPoints", Mathf.Max(0, profile.spentPoints));
        PlayerPrefs.SetInt(prefix + "BestLevel", Mathf.Max(1, profile.bestLevelEver));
        PlayerPrefs.SetInt(prefix + "ReachedLevel10", profile.reachedLevel10 ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "ReachedLevel20", profile.reachedLevel20 ? 1 : 0);
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
