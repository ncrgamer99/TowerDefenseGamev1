using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum RunGoldSource
{
    Unknown,
    EnemyKill,
    WaveCompletion,
    BlockedEvent,
    Debug,
    Other
}

public enum RunGoldSpendSource
{
    Unknown,
    TowerBuild,
    GoldUpgrade,
    Debug,
    Other
}

public enum TowerUpgradeCategory
{
    Unknown,
    Damage,
    Range,
    FireRate,
    Effect
}

[System.Serializable]
public class RunBlockedEventTypeStats
{
    public string eventType = "Unknown";
    public string lastEventName = "";
    public int choices = 0;
    public int goldGained = 0;
    public int livesGained = 0;
    public float totalBuildPhaseDuration = 0f;
    public int firstWaveNumber = 0;
    public int lastWaveNumber = 0;

    public void Clear()
    {
        eventType = "Unknown";
        lastEventName = "";
        choices = 0;
        goldGained = 0;
        livesGained = 0;
        totalBuildPhaseDuration = 0f;
        firstWaveNumber = 0;
        lastWaveNumber = 0;
    }

    public void Record(string eventName, int gold, int lives, float buildPhaseDuration)
    {
        Record(eventName, gold, lives, buildPhaseDuration, 0);
    }

    public void Record(string eventName, int gold, int lives, float buildPhaseDuration, int waveNumber)
    {
        choices++;
        lastEventName = string.IsNullOrEmpty(eventName) ? eventType : eventName;
        goldGained += Mathf.Max(0, gold);
        livesGained += Mathf.Max(0, lives);
        totalBuildPhaseDuration += Mathf.Max(0f, buildPhaseDuration);

        int safeWaveNumber = Mathf.Max(0, waveNumber);
        if (safeWaveNumber > 0)
        {
            if (firstWaveNumber <= 0)
                firstWaveNumber = safeWaveNumber;

            lastWaveNumber = safeWaveNumber;
        }
    }

    public RunBlockedEventTypeStats CreateCopy()
    {
        return new RunBlockedEventTypeStats
        {
            eventType = eventType,
            lastEventName = lastEventName,
            choices = choices,
            goldGained = goldGained,
            livesGained = livesGained,
            totalBuildPhaseDuration = totalBuildPhaseDuration,
            firstWaveNumber = firstWaveNumber,
            lastWaveNumber = lastWaveNumber
        };
    }

    public float GetAverageBuildPhaseDuration()
    {
        return choices > 0 ? totalBuildPhaseDuration / choices : 0f;
    }

    public string GetCompactLine()
    {
        string label = string.IsNullOrEmpty(lastEventName) ? eventType : lastEventName;
        string waveText = lastWaveNumber > 0 ? " | letzte W " + lastWaveNumber : "";
        return label + " (" + eventType + "): " + choices + "x | Gold +" + goldGained + " | Leben +" + livesGained + " | Zeit " + Mathf.RoundToInt(totalBuildPhaseDuration) + "s | Avg " + Mathf.RoundToInt(GetAverageBuildPhaseDuration()) + "s" + waveText;
    }
}

[System.Serializable]
public class RunEconomyStats
{
    [Header("Gold Earned")]
    public int totalGoldEarned = 0;
    public int baseGoldBeforeRewardModifiers = 0;
    public int goldAddedByRewardModifiers = 0;
    public int enemyKillGoldEarned = 0;
    public int waveCompletionGoldEarned = 0;
    public int blockedEventGoldEarned = 0;
    public int otherGoldEarned = 0;

    [Header("Gold Spent")]
    public int totalGoldSpent = 0;
    public int towerBuildGoldSpent = 0;
    public int goldUpgradeGoldSpent = 0;
    public int otherGoldSpent = 0;

    public void Clear()
    {
        totalGoldEarned = 0;
        baseGoldBeforeRewardModifiers = 0;
        goldAddedByRewardModifiers = 0;
        enemyKillGoldEarned = 0;
        waveCompletionGoldEarned = 0;
        blockedEventGoldEarned = 0;
        otherGoldEarned = 0;
        totalGoldSpent = 0;
        towerBuildGoldSpent = 0;
        goldUpgradeGoldSpent = 0;
        otherGoldSpent = 0;
    }
}

[System.Serializable]
public class RunStatistics
{
    [Header("State")]
    public bool initialized = false;

    [Header("Economy")]
    public int totalGoldEarned = 0;
    public int totalGoldSpent = 0;
    public int goldFromEnemyKills = 0;
    public int goldFromWaveCompletion = 0;
    public int goldFromBlockedEvents = 0;
    public int goldFromOther = 0;
    public int goldBonusFromRewardModifiers = 0;

    [Header("Blocked Events")]
    public int blockedEventsChosen = 0;
    public int lifeFromBlockedEvents = 0;
    public int timedBlockedBuildPhases = 0;
    public float totalBlockedBuildPhaseDuration = 0f;
    public string lastBlockedEventName = "";
    public string lastBlockedEventType = "";
    public List<RunBlockedEventTypeStats> blockedEventTypeStats = new List<RunBlockedEventTypeStats>();

    [Header("Tower")]
    public int towersBuilt = 0;
    public int towersDestroyedByElite = 0;
    public int totalTowerXPGranted = 0;
    public int towerLevelUps = 0;
    public int highestTowerLevel = 1;
    public int upgradePointsEarned = 0;
    public int upgradePointsSpent = 0;
    public int metaPointsPrepared = 0;
    public int visualTierUps = 0;
    public int goldUpgradesBought = 0;
    public int pointUpgradesBought = 0;

    [Header("Elite")]
    public int eliteWavesCompleted = 0;
    public int eliteKills = 0;
    public int eliteLeaks = 0;
    public int eliteRewardsChosen = 0;
    public string lastEliteDestroyedTowerName = "";
    public string lastEliteRewardName = "";

    public void Clear()
    {
        initialized = true;
        totalGoldEarned = 0;
        totalGoldSpent = 0;
        goldFromEnemyKills = 0;
        goldFromWaveCompletion = 0;
        goldFromBlockedEvents = 0;
        goldFromOther = 0;
        goldBonusFromRewardModifiers = 0;
        blockedEventsChosen = 0;
        lifeFromBlockedEvents = 0;
        timedBlockedBuildPhases = 0;
        totalBlockedBuildPhaseDuration = 0f;
        lastBlockedEventName = "";
        lastBlockedEventType = "";
        if (blockedEventTypeStats == null)
            blockedEventTypeStats = new List<RunBlockedEventTypeStats>();
        else
            blockedEventTypeStats.Clear();
        towersBuilt = 0;
        towersDestroyedByElite = 0;
        totalTowerXPGranted = 0;
        towerLevelUps = 0;
        highestTowerLevel = 1;
        upgradePointsEarned = 0;
        upgradePointsSpent = 0;
        metaPointsPrepared = 0;
        visualTierUps = 0;
        goldUpgradesBought = 0;
        pointUpgradesBought = 0;
        eliteWavesCompleted = 0;
        eliteKills = 0;
        eliteLeaks = 0;
        eliteRewardsChosen = 0;
        lastEliteDestroyedTowerName = "";
        lastEliteRewardName = "";
    }

    public int GetNetGoldDelta()
    {
        return totalGoldEarned - totalGoldSpent;
    }

    public string GetGoldSourceSummary()
    {
        return "Kills " + goldFromEnemyKills +
               " | Wave " + goldFromWaveCompletion +
               " | Verbau " + goldFromBlockedEvents +
               " | Sonstiges " + goldFromOther +
               " | Bonus +" + goldBonusFromRewardModifiers;
    }

    public string GetXPSummary()
    {
        return "Tower-XP verdient " + totalTowerXPGranted + " | Level-Ups " + towerLevelUps + " | höchstes Tower-Level " + highestTowerLevel;
    }

    public string GetTowerProgressionSummary()
    {
        return "Tower gebaut " + towersBuilt + " | Elite zerstörte " + towersDestroyedByElite + " | Upgradepunkte verdient/ausgegeben " + upgradePointsEarned + "/" + upgradePointsSpent + " | vorbereitete Meta-Punkte " + metaPointsPrepared + " | Visual-Tier-Ups " + visualTierUps;
    }

    public string GetUpgradeSummary()
    {
        return "Upgrades: Gold " + goldUpgradesBought + " | Punkte " + pointUpgradesBought;
    }
}

[System.Serializable]
public class RunWaveTowerStatsRecord
{
    [Header("Identity")]
    public int instanceId = 0;
    public string towerName = "Tower";
    public TowerRole towerRole = TowerRole.Basic;
    public Tower towerReference;

    [Header("Wave Impact")]
    public int kills = 0;
    public int assists = 0;
    public float damageDealt = 0f;
    public int xpGained = 0;
    public int levelAtEnd = 1;

    public void InitializeFromTower(Tower tower)
    {
        towerReference = tower;

        if (tower == null)
            return;

        instanceId = tower.GetInstanceID();
        towerName = string.IsNullOrEmpty(tower.towerName) ? tower.name : tower.towerName;
        towerRole = tower.towerRole;
        RefreshFromTower();
    }

    public void RefreshFromTower()
    {
        if (towerReference == null)
            return;

        towerName = string.IsNullOrEmpty(towerReference.towerName) ? towerReference.name : towerReference.towerName;
        towerRole = towerReference.towerRole;
        kills = Mathf.Max(kills, towerReference.currentWaveKills);
        assists = Mathf.Max(assists, towerReference.currentWaveAssists);
        damageDealt = Mathf.Max(damageDealt, towerReference.currentWaveDamageDealt);
        levelAtEnd = Mathf.Max(levelAtEnd, towerReference.level);
    }

    public int GetImpactScore()
    {
        RefreshFromTower();
        int damageScore = Mathf.RoundToInt(damageDealt * 0.10f);
        int xpScore = Mathf.RoundToInt(xpGained * 0.50f);
        return kills * 10 + assists * 4 + damageScore + xpScore + levelAtEnd * 2;
    }

    public string GetCompactLine()
    {
        RefreshFromTower();
        return towerName +
               " | Lvl " + levelAtEnd +
               " | K " + kills +
               " | A " + assists +
               " | Dmg " + Mathf.RoundToInt(damageDealt) +
               " | XP " + xpGained;
    }

    public RunWaveTowerStatsRecord CreateCopy()
    {
        return new RunWaveTowerStatsRecord
        {
            instanceId = instanceId,
            towerName = towerName,
            towerRole = towerRole,
            towerReference = null,
            kills = kills,
            assists = assists,
            damageDealt = damageDealt,
            xpGained = xpGained,
            levelAtEnd = levelAtEnd
        };
    }
}

[System.Serializable]
public class RunWaveStatistics
{
    [Header("State")]
    public bool initialized = false;
    public bool isActiveTracking = false;

    [Header("Wave")]
    public int waveNumber = 0;
    public string scenarioName = "";
    public bool survivedWave = false;
    public bool waveCompleted = false;
    public int totalSpawnCount = 0;
    public int kills = 0;
    public int baseDamageTaken = 0;
    public EnemyRole topBaseDamageRole = EnemyRole.Standard;
    public int topBaseDamageAmount = 0;

    [Header("Rewards")]
    public int goldEarned = 0;
    public int xpEarned = 0;

    [Header("Chaos")]
    public List<WaveModifier> activeRiskModifiers = new List<WaveModifier>();

    [Header("Tower")]
    public List<RunWaveTowerStatsRecord> strongestTowerRecords = new List<RunWaveTowerStatsRecord>();

    public void Clear()
    {
        initialized = false;
        isActiveTracking = false;
        waveNumber = 0;
        scenarioName = "";
        survivedWave = false;
        waveCompleted = false;
        totalSpawnCount = 0;
        kills = 0;
        baseDamageTaken = 0;
        topBaseDamageRole = EnemyRole.Standard;
        topBaseDamageAmount = 0;
        goldEarned = 0;
        xpEarned = 0;

        if (activeRiskModifiers == null)
            activeRiskModifiers = new List<WaveModifier>();
        else
            activeRiskModifiers.Clear();

        if (strongestTowerRecords == null)
            strongestTowerRecords = new List<RunWaveTowerStatsRecord>();
        else
            strongestTowerRecords.Clear();
    }

    public void InitializeFromResult(WaveCompletionResult result)
    {
        Clear();
        initialized = true;
        isActiveTracking = true;
        CaptureFromResult(result, false);
    }

    public void CaptureFromResult(WaveCompletionResult result, bool survived)
    {
        if (result == null)
            return;

        initialized = true;
        waveNumber = result.waveNumber;
        scenarioName = string.IsNullOrEmpty(result.scenarioName) ? result.scenario.ToString() : result.scenarioName;
        survivedWave = survived;
        waveCompleted = result.waveCompleted;
        totalSpawnCount = result.totalSpawnCount;
        kills = result.enemiesKilled;
        baseDamageTaken = result.baseDamageTaken;
        CaptureTopBaseDamageRole(result);
        SetActiveRiskModifiers(result.activeRiskModifiersAtWaveStart);
    }

    public void FinishTracking(WaveCompletionResult result, bool survived)
    {
        CaptureFromResult(result, survived);
        isActiveTracking = false;
    }

    public void CopyFrom(RunWaveStatistics other)
    {
        Clear();

        if (other == null || !other.initialized)
            return;

        initialized = other.initialized;
        isActiveTracking = false;
        waveNumber = other.waveNumber;
        scenarioName = other.scenarioName;
        survivedWave = other.survivedWave;
        waveCompleted = other.waveCompleted;
        totalSpawnCount = other.totalSpawnCount;
        kills = other.kills;
        baseDamageTaken = other.baseDamageTaken;
        topBaseDamageRole = other.topBaseDamageRole;
        topBaseDamageAmount = other.topBaseDamageAmount;
        goldEarned = other.goldEarned;
        xpEarned = other.xpEarned;

        if (other.activeRiskModifiers != null)
        {
            foreach (WaveModifier modifier in other.activeRiskModifiers)
            {
                if (modifier != null)
                    activeRiskModifiers.Add(modifier.CreateCopy());
            }
        }

        if (other.strongestTowerRecords != null)
        {
            foreach (RunWaveTowerStatsRecord record in other.strongestTowerRecords)
            {
                if (record != null)
                    strongestTowerRecords.Add(record.CreateCopy());
            }
        }
    }

    public RunWaveTowerStatsRecord GetOrCreateTowerRecord(Tower tower)
    {
        if (tower == null)
            return null;

        if (strongestTowerRecords == null)
            strongestTowerRecords = new List<RunWaveTowerStatsRecord>();

        int instanceId = tower.GetInstanceID();

        foreach (RunWaveTowerStatsRecord record in strongestTowerRecords)
        {
            if (record != null && record.instanceId == instanceId)
                return record;
        }

        RunWaveTowerStatsRecord newRecord = new RunWaveTowerStatsRecord();
        newRecord.InitializeFromTower(tower);
        strongestTowerRecords.Add(newRecord);
        return newRecord;
    }

    public string GetDetailText()
    {
        if (!initialized)
            return "Noch keine Wave-Daten gespeichert.";

        string text =
            "<b>Wave " + waveNumber + (string.IsNullOrEmpty(scenarioName) ? "" : " - " + scenarioName) + "</b>\n" +
            "Überlebt: <b>" + (survivedWave ? "Ja" : "Nein") + "</b>\n" +
            "Kills: <b>" + kills + "</b> / " + totalSpawnCount + "\n" +
            "Erhaltene XP: <b>" + xpEarned + "</b>\n" +
            "Erhaltenes Gold: <b>" + goldEarned + "</b>\n" +
            "Base-Schaden: " + baseDamageTaken + "\n" +
            "Gegner-Typ mit meistem Base-Schaden: " + GetTopBaseDamageText() + "\n\n" +
            "<b>Stärkste 5 Tower</b>\n" +
            GetStrongestTowerText() +
            "<b>Chaos-Modifikatoren</b>\n" +
            GetChaosModifierText();

        return text;
    }

    public string GetStrongestTowerText()
    {
        if (strongestTowerRecords == null || strongestTowerRecords.Count == 0)
            return "Keine Tower-Daten für diese Wave.\n\n";

        SortAndLimitTowerRecords(5);
        string text = "";

        for (int i = 0; i < strongestTowerRecords.Count; i++)
        {
            RunWaveTowerStatsRecord record = strongestTowerRecords[i];

            if (record == null)
                continue;

            text += "- " + record.GetCompactLine() + "\n";
        }

        return string.IsNullOrEmpty(text) ? "Keine Tower-Daten für diese Wave.\n\n" : text + "\n";
    }

    public string GetChaosModifierText()
    {
        if (activeRiskModifiers == null || activeRiskModifiers.Count == 0)
            return "Keine aktiven Chaos-Modifikatoren.";

        string text = "";

        foreach (WaveModifier modifier in activeRiskModifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "- " + modifier.GetDisplayNameWithLevel();
        }

        return string.IsNullOrEmpty(text) ? "Keine aktiven Chaos-Modifikatoren." : text;
    }

    public void SortAndLimitTowerRecords(int maxRecords)
    {
        if (strongestTowerRecords == null)
            strongestTowerRecords = new List<RunWaveTowerStatsRecord>();

        strongestTowerRecords.RemoveAll(record => record == null);
        strongestTowerRecords.Sort((a, b) => b.GetImpactScore().CompareTo(a.GetImpactScore()));

        int safeMax = Mathf.Max(1, maxRecords);
        if (strongestTowerRecords.Count > safeMax)
            strongestTowerRecords.RemoveRange(safeMax, strongestTowerRecords.Count - safeMax);
    }

    private void CaptureTopBaseDamageRole(WaveCompletionResult result)
    {
        EnemyRoleDamageCount topDamage = result.GetTopBaseDamageRole();

        if (topDamage == null)
        {
            topBaseDamageRole = EnemyRole.Standard;
            topBaseDamageAmount = 0;
            return;
        }

        topBaseDamageRole = topDamage.role;
        topBaseDamageAmount = Mathf.Max(0, topDamage.damage);
    }

    private void SetActiveRiskModifiers(List<WaveModifier> modifiers)
    {
        if (activeRiskModifiers == null)
            activeRiskModifiers = new List<WaveModifier>();

        activeRiskModifiers.Clear();

        if (modifiers == null)
            return;

        foreach (WaveModifier modifier in modifiers)
        {
            if (modifier != null && modifier.IsValid())
                activeRiskModifiers.Add(modifier.CreateCopy());
        }
    }

    private string GetTopBaseDamageText()
    {
        if (topBaseDamageAmount <= 0)
            return "Keiner";

        return topBaseDamageRole + " (" + topBaseDamageAmount + ")";
    }
}

[System.Serializable]
public class RunTowerStatsRecord
{
    [Header("Identity")]
    public int instanceId = 0;
    public string towerName = "Tower";
    public TowerRole towerRole = TowerRole.Basic;
    public Tower towerReference;

    [Header("Build")]
    public bool wasRegisteredAsBuilt = false;
    public int builtAtWave = 0;
    public int buildCost = 0;
    public Vector2Int builtGridPosition;
    public Vector3 builtWorldPosition;

    [Header("Progression")]
    public int highestLevel = 1;
    public int towerXPGained = 0;
    public int levelUps = 0;
    public int upgradePointsGained = 0;
    public int metaPointsGained = 0;
    public int highestVisualTier = 0;

    [Header("Upgrades")]
    public int goldUpgradesPurchased = 0;
    public int pointUpgradesPurchased = 0;
    public int goldSpentOnUpgrades = 0;
    public int upgradePointsSpent = 0;

    public int damageGoldUpgrades = 0;
    public int rangeGoldUpgrades = 0;
    public int fireRateGoldUpgrades = 0;
    public int effectGoldUpgrades = 0;

    public int damagePointUpgrades = 0;
    public int rangePointUpgrades = 0;
    public int fireRatePointUpgrades = 0;
    public int effectPointUpgrades = 0;

    [Header("Combat Snapshot")]
    public int totalKills = 0;
    public int totalAssists = 0;
    public float totalDamageDealt = 0f;

    public void InitializeFromTower(Tower tower)
    {
        towerReference = tower;

        if (tower == null)
            return;

        instanceId = tower.GetInstanceID();
        towerName = string.IsNullOrEmpty(tower.towerName) ? tower.name : tower.towerName;
        towerRole = tower.towerRole;
        RefreshFromTower();
    }

    public void RefreshFromTower()
    {
        if (towerReference == null)
            return;

        towerName = string.IsNullOrEmpty(towerReference.towerName) ? towerReference.name : towerReference.towerName;
        towerRole = towerReference.towerRole;
        highestLevel = Mathf.Max(highestLevel, towerReference.level);
        highestVisualTier = Mathf.Max(highestVisualTier, towerReference.visualTier);
        totalKills = towerReference.totalKills;
        totalAssists = towerReference.totalAssists;
        totalDamageDealt = towerReference.totalDamageDealt;
    }

    public int GetImpactScore()
    {
        RefreshFromTower();
        int damageScore = Mathf.RoundToInt(totalDamageDealt * 0.10f);
        return totalKills * 8 + totalAssists * 3 + damageScore + highestLevel * 5 + goldUpgradesPurchased * 2 + pointUpgradesPurchased * 3;
    }

    public string GetCompactLine()
    {
        RefreshFromTower();
        return towerName + " | Lvl " + highestLevel + " | K " + totalKills + " | A " + totalAssists + " | Dmg " + Mathf.RoundToInt(totalDamageDealt) + " | Gold-Upg " + goldUpgradesPurchased + " | Point-Upg " + pointUpgradesPurchased;
    }
}

public class RunStatisticsTracker : MonoBehaviour
{
    private const string LastWaveStatisticsPlayerPrefsKey = "TD_LastGame_LastWaveStatistics_V1";

    [Header("Phase 6 Version Check")]
    public string phase6Version = "Phase6 V1 Step13 - Economy/Tower Run Stats V1 - 2026-05-13";

    [Header("References")]
    public GameManager gameManager;

    [Header("Settings")]
    public bool autoDiscoverExistingTowers = true;
    public bool logRunStatEvents = false;
    public int maxTowerRecordsInSummary = 8;

    [Header("Economy")]
    public RunEconomyStats economy = new RunEconomyStats();
    public RunStatistics statistics = new RunStatistics();

    [Header("Blocked Events")]
    public int blockedEventsChosen = 0;
    public int lifeFromBlockedEvents = 0;
    public int timedBlockedBuildPhases = 0;
    public float totalBlockedBuildPhaseDuration = 0f;
    public string lastBlockedEventName = "";
    public string lastBlockedEventType = "";
    public List<RunBlockedEventTypeStats> blockedEventTypeStats = new List<RunBlockedEventTypeStats>();

    [Header("Tower Progression")]
    public int towersBuilt = 0;
    public int towersDestroyedByElite = 0;
    public int totalTowerBuildCost = 0;
    public int totalTowerXPGained = 0;
    public int totalTowerLevelUps = 0;
    public int totalUpgradePointsGained = 0;
    public int totalMetaPointsPrepared = 0;
    public int highestTowerLevelReached = 1;
    public int highestTowerVisualTierReached = 0;
    public int totalGoldUpgradesPurchased = 0;
    public int totalPointUpgradesPurchased = 0;
    public int totalUpgradePointsSpent = 0;

    [Header("Elite")]
    public int eliteWavesCompleted = 0;
    public int eliteKills = 0;
    public int eliteLeaks = 0;
    public int eliteRewardsChosen = 0;
    public string lastEliteDestroyedTowerName = "";
    public string lastEliteRewardName = "";

    [Header("Tower Records")]
    public List<RunTowerStatsRecord> towerRecords = new List<RunTowerStatsRecord>();

    [Header("Last Wave")]
    public RunWaveStatistics activeWaveStatistics = new RunWaveStatistics();
    public RunWaveStatistics lastWaveStatistics = new RunWaveStatistics();

    private bool lastWaveStatisticsLoadedFromPrefs = false;

    private void Awake()
    {
        EnsureLists();
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        Debug.Log("RunStatisticsTracker Version: " + phase6Version);
    }

    public void Connect(GameManager manager)
    {
        gameManager = manager;
        EnsureLists();
    }

    public RunStatistics GetRunStatistics()
    {
        EnsureLists();
        SyncStatisticsSnapshot();
        return statistics;
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void EnsureLists()
    {
        if (economy == null)
            economy = new RunEconomyStats();

        if (statistics == null)
            statistics = new RunStatistics();

        if (!statistics.initialized)
            statistics.Clear();

        if (statistics.blockedEventTypeStats == null)
            statistics.blockedEventTypeStats = new List<RunBlockedEventTypeStats>();

        if (blockedEventTypeStats == null)
            blockedEventTypeStats = new List<RunBlockedEventTypeStats>();

        if (towerRecords == null)
            towerRecords = new List<RunTowerStatsRecord>();

        if (activeWaveStatistics == null)
            activeWaveStatistics = new RunWaveStatistics();

        if (lastWaveStatistics == null)
            lastWaveStatistics = new RunWaveStatistics();

        LoadPersistedLastWaveStatisticsIfNeeded();
    }

    public void ClearRunStats()
    {
        EnsureLists();
        economy.Clear();
        statistics.Clear();
        towersBuilt = 0;
        towersDestroyedByElite = 0;
        totalTowerBuildCost = 0;
        totalTowerXPGained = 0;
        totalTowerLevelUps = 0;
        totalUpgradePointsGained = 0;
        totalMetaPointsPrepared = 0;
        highestTowerLevelReached = 1;
        highestTowerVisualTierReached = 0;
        totalGoldUpgradesPurchased = 0;
        totalPointUpgradesPurchased = 0;
        totalUpgradePointsSpent = 0;
        blockedEventsChosen = 0;
        lifeFromBlockedEvents = 0;
        timedBlockedBuildPhases = 0;
        totalBlockedBuildPhaseDuration = 0f;
        lastBlockedEventName = "";
        lastBlockedEventType = "";
        blockedEventTypeStats.Clear();
        eliteWavesCompleted = 0;
        eliteKills = 0;
        eliteLeaks = 0;
        eliteRewardsChosen = 0;
        lastEliteDestroyedTowerName = "";
        lastEliteRewardName = "";
        towerRecords.Clear();
        activeWaveStatistics.Clear();
        lastWaveStatistics.Clear();
    }

    public void BeginWaveTracking(WaveCompletionResult result)
    {
        EnsureLists();

        if (result == null)
            return;

        activeWaveStatistics.Clear();
        activeWaveStatistics.InitializeFromResult(result);
    }

    public void CompleteWaveTracking(WaveCompletionResult result, bool survivedWave)
    {
        EnsureLists();

        if (result == null)
            return;

        if (!activeWaveStatistics.initialized || activeWaveStatistics.waveNumber != result.waveNumber)
            activeWaveStatistics.InitializeFromResult(result);

        activeWaveStatistics.FinishTracking(result, survivedWave);
        CaptureActiveTowerWaveStats();
        activeWaveStatistics.SortAndLimitTowerRecords(5);
        lastWaveStatistics.CopyFrom(activeWaveStatistics);
        SavePersistedLastWaveStatistics();
    }

    public void RecordGoldEarned(int finalAmount, int baseAmount, RunGoldSource source, bool rewardModifiersApplied)
    {
        EnsureLists();
        int safeFinal = Mathf.Max(0, finalAmount);
        int safeBase = Mathf.Max(0, baseAmount);

        if (safeFinal <= 0)
            return;

        economy.totalGoldEarned += safeFinal;
        economy.baseGoldBeforeRewardModifiers += safeBase;

        if (activeWaveStatistics != null && activeWaveStatistics.isActiveTracking)
            activeWaveStatistics.goldEarned += safeFinal;

        if (rewardModifiersApplied)
            economy.goldAddedByRewardModifiers += Mathf.Max(0, safeFinal - safeBase);

        switch (source)
        {
            case RunGoldSource.EnemyKill:
                economy.enemyKillGoldEarned += safeFinal;
                break;
            case RunGoldSource.WaveCompletion:
                economy.waveCompletionGoldEarned += safeFinal;
                break;
            case RunGoldSource.BlockedEvent:
                economy.blockedEventGoldEarned += safeFinal;
                break;
            default:
                economy.otherGoldEarned += safeFinal;
                break;
        }

        SyncStatisticsSnapshot();

        if (logRunStatEvents)
            Debug.Log("RunStats: Gold +" + safeFinal + " from " + source + " (base " + safeBase + ").");
    }

    public void RecordGoldSpent(int amount, RunGoldSpendSource source)
    {
        EnsureLists();
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        economy.totalGoldSpent += safeAmount;

        switch (source)
        {
            case RunGoldSpendSource.TowerBuild:
                economy.towerBuildGoldSpent += safeAmount;
                break;
            case RunGoldSpendSource.GoldUpgrade:
                economy.goldUpgradeGoldSpent += safeAmount;
                break;
            default:
                economy.otherGoldSpent += safeAmount;
                break;
        }

        SyncStatisticsSnapshot();
    }


    public void RecordBlockedEventChoice(string eventName, string eventType, int goldGained, int livesGained, float buildPhaseDuration, int waveNumber = 0)
    {
        EnsureLists();

        blockedEventsChosen++;
        lifeFromBlockedEvents += Mathf.Max(0, livesGained);
        timedBlockedBuildPhases++;
        totalBlockedBuildPhaseDuration += Mathf.Max(0f, buildPhaseDuration);
        lastBlockedEventName = string.IsNullOrEmpty(eventName) ? "Unbekannt" : eventName;
        lastBlockedEventType = string.IsNullOrEmpty(eventType) ? "Unknown" : eventType;
        int safeWaveNumber = waveNumber > 0 ? waveNumber : (gameManager != null ? gameManager.waveNumber : 0);
        GetOrCreateBlockedEventTypeStats(lastBlockedEventType).Record(lastBlockedEventName, goldGained, livesGained, buildPhaseDuration, safeWaveNumber);

        SyncStatisticsSnapshot();

        if (logRunStatEvents)
        {
            Debug.Log(
                "RunStats: Verbau-Event " + lastBlockedEventName +
                " | Typ " + lastBlockedEventType +
                (safeWaveNumber > 0 ? " | Wave " + safeWaveNumber : "") +
                " | Gold +" + Mathf.Max(0, goldGained) +
                " | Leben +" + Mathf.Max(0, livesGained) +
                " | Buildphase " + Mathf.Max(0f, buildPhaseDuration).ToString("0.0") + "s."
            );
        }
    }

    public void RecordEliteWaveCompleted(WaveCompletionResult result)
    {
        EnsureLists();

        if (result == null || !result.isEliteWave || !result.waveCompleted)
            return;

        eliteWavesCompleted++;

        if (result.eliteDefeated)
            eliteKills++;

        if (result.eliteReachedBase)
            eliteLeaks++;

        if (result.eliteDestroyedTower)
            lastEliteDestroyedTowerName = result.eliteDestroyedTowerName;

        SyncStatisticsSnapshot();
    }

    public void RecordTowerDestroyedByElite(Tower tower, int waveNumber, string reason)
    {
        EnsureLists();

        towersDestroyedByElite++;
        lastEliteDestroyedTowerName = tower != null
            ? string.IsNullOrEmpty(tower.towerName) ? tower.name : tower.towerName
            : "Unbekannter Tower";

        RunTowerStatsRecord record = GetOrCreateTowerRecord(tower);
        record.RefreshFromTower();
        CaptureTowerForActiveWave(tower);
        SyncStatisticsSnapshot();

        if (logRunStatEvents)
        {
            Debug.Log(
                "RunStats: Elite zerstörte Tower " + lastEliteDestroyedTowerName +
                " in Wave " + Mathf.Max(0, waveNumber) +
                (string.IsNullOrEmpty(reason) ? "." : " | " + reason)
            );
        }
    }

    public void RecordEliteRewardChoice(string rewardName)
    {
        EnsureLists();

        eliteRewardsChosen++;
        lastEliteRewardName = string.IsNullOrEmpty(rewardName) ? "Unbekannte Elite-Belohnung" : rewardName;
        SyncStatisticsSnapshot();
    }

    public void RecordTowerBuilt(Tower tower, int cost, int waveNumber, Vector2Int gridPosition, Vector3 worldPosition)
    {
        EnsureLists();
        towersBuilt++;
        totalTowerBuildCost += Mathf.Max(0, cost);

        RunTowerStatsRecord record = GetOrCreateTowerRecord(tower);
        record.wasRegisteredAsBuilt = true;
        record.builtAtWave = waveNumber;
        record.buildCost = Mathf.Max(0, cost);
        record.builtGridPosition = gridPosition;
        record.builtWorldPosition = worldPosition;
        record.RefreshFromTower();

        SyncTowerPeakValues(record);
        SyncStatisticsSnapshot();
    }

    public void RecordTowerXPGained(Tower tower, int amount)
    {
        EnsureLists();
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        totalTowerXPGained += safeAmount;

        if (activeWaveStatistics != null && activeWaveStatistics.isActiveTracking)
        {
            activeWaveStatistics.xpEarned += safeAmount;
            RunWaveTowerStatsRecord waveRecord = activeWaveStatistics.GetOrCreateTowerRecord(tower);

            if (waveRecord != null)
                waveRecord.xpGained += safeAmount;
        }

        RunTowerStatsRecord record = GetOrCreateTowerRecord(tower);
        record.towerXPGained += safeAmount;
        record.RefreshFromTower();
        SyncTowerPeakValues(record);
        SyncStatisticsSnapshot();
    }

    public void RecordTowerLevelUp(Tower tower, int reachedLevel, bool gainedUpgradePoint, bool gainedMetaPoint, bool gainedVisualTier)
    {
        EnsureLists();
        totalTowerLevelUps++;

        if (gainedUpgradePoint)
            totalUpgradePointsGained++;

        if (gainedMetaPoint)
            totalMetaPointsPrepared++;

        RunTowerStatsRecord record = GetOrCreateTowerRecord(tower);
        record.levelUps++;
        record.highestLevel = Mathf.Max(record.highestLevel, reachedLevel);

        if (gainedUpgradePoint)
            record.upgradePointsGained++;

        if (gainedMetaPoint)
            record.metaPointsGained++;

        if (gainedVisualTier)
            record.highestVisualTier = Mathf.Max(record.highestVisualTier, tower != null ? tower.visualTier : 0);

        SyncTowerPeakValues(record);
        SyncStatisticsSnapshot();
    }

    public void RecordTowerUpgradePointsGranted(Tower tower, int amount)
    {
        EnsureLists();
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        totalUpgradePointsGained += safeAmount;

        RunTowerStatsRecord record = GetOrCreateTowerRecord(tower);
        record.upgradePointsGained += safeAmount;
        record.RefreshFromTower();

        SyncTowerPeakValues(record);
        SyncStatisticsSnapshot();
    }

    public void RecordGoldUpgrade(Tower tower, TowerUpgradeCategory category, int goldSpent, int newUpgradeLevel)
    {
        EnsureLists();
        totalGoldUpgradesPurchased++;

        RunTowerStatsRecord record = GetOrCreateTowerRecord(tower);
        record.goldUpgradesPurchased++;
        record.goldSpentOnUpgrades += Mathf.Max(0, goldSpent);
        IncrementUpgradeCategory(record, category, true);
        record.RefreshFromTower();

        SyncTowerPeakValues(record);
        SyncStatisticsSnapshot();
    }

    public void RecordPointUpgrade(Tower tower, TowerUpgradeCategory category, int pointsSpent, int newUpgradeLevel)
    {
        EnsureLists();
        int safePoints = Mathf.Max(0, pointsSpent);
        totalPointUpgradesPurchased++;
        totalUpgradePointsSpent += safePoints;

        RunTowerStatsRecord record = GetOrCreateTowerRecord(tower);
        record.pointUpgradesPurchased++;
        record.upgradePointsSpent += safePoints;
        IncrementUpgradeCategory(record, category, false);
        record.RefreshFromTower();

        SyncTowerPeakValues(record);
        SyncStatisticsSnapshot();
    }

    public RunTowerStatsRecord GetStrongestTowerRecord()
    {
        EnsureLists();
        RefreshAllTowerRecords();
        RunTowerStatsRecord best = null;
        int bestScore = -1;

        foreach (RunTowerStatsRecord record in towerRecords)
        {
            if (record == null)
                continue;

            int score = record.GetImpactScore();

            if (score > bestScore)
            {
                best = record;
                bestScore = score;
            }
        }

        return best;
    }

    public string GetTopTowerRecordsText(int maxEntries)
    {
        EnsureLists();
        RefreshAllTowerRecords();

        List<RunTowerStatsRecord> records = new List<RunTowerStatsRecord>();

        foreach (RunTowerStatsRecord record in towerRecords)
        {
            if (record != null)
                records.Add(record);
        }

        records.Sort((a, b) => b.GetImpactScore().CompareTo(a.GetImpactScore()));

        if (records.Count == 0)
            return "Keine Tower-Daten gespeichert.\n";

        int safeMax = Mathf.Max(1, maxEntries);
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < records.Count && i < safeMax; i++)
            builder.AppendLine("- " + records[i].GetCompactLine());

        if (records.Count > safeMax)
            builder.AppendLine("... " + (records.Count - safeMax) + " weitere Tower ausgeblendet.");

        return builder.ToString();
    }

    public string GetEconomySummaryText()
    {
        SyncStatisticsSnapshot();
        return
            "Gold verdient: <b>" + economy.totalGoldEarned + "</b> | ausgegeben: <b>" + economy.totalGoldSpent + "</b> | Netto: " + (economy.totalGoldEarned - economy.totalGoldSpent) + "\n" +
            "Quellen: Kills " + economy.enemyKillGoldEarned + " | Wave-Abschluss " + economy.waveCompletionGoldEarned + " | Verbau-Event " + economy.blockedEventGoldEarned + " | Sonstiges " + economy.otherGoldEarned + "\n" +
            "Reward-Bonus durch Gerechtigkeit/Risiko: +" + economy.goldAddedByRewardModifiers + " Gold\n" +
            "Verbau-Events: " + blockedEventsChosen + " | Leben aus Verbau: " + lifeFromBlockedEvents + " | Buildphasen: " + timedBlockedBuildPhases + "\n" +
            GetBlockedEventTypeSummaryText(4) +
            "Ausgaben: Towerbau " + economy.towerBuildGoldSpent + " | Gold-Upgrades " + economy.goldUpgradeGoldSpent + " | Sonstiges " + economy.otherGoldSpent + "\n";
    }

    public string GetBlockedEventTypeSummaryText(int maxEntries)
    {
        EnsureLists();

        if (blockedEventTypeStats == null || blockedEventTypeStats.Count == 0)
            return "";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Verbau-Typen:");

        int safeMax = Mathf.Max(1, maxEntries);
        int shown = 0;

        List<RunBlockedEventTypeStats> sortedStats = CreateBlockedEventTypeStatsSnapshot(true);

        foreach (RunBlockedEventTypeStats stats in sortedStats)
        {
            if (stats == null || stats.choices <= 0)
                continue;

            if (shown >= safeMax)
                break;

            builder.AppendLine("- " + stats.GetCompactLine());
            shown++;
        }

        int hidden = Mathf.Max(0, CountTrackedBlockedEventTypes() - shown);
        if (hidden > 0)
            builder.AppendLine("... " + hidden + " weitere Verbau-Typen.");

        return builder.ToString();
    }

    public List<RunBlockedEventTypeStats> GetBlockedEventTypeStatsSnapshot(int maxEntries = 0)
    {
        EnsureLists();
        List<RunBlockedEventTypeStats> snapshot = CreateBlockedEventTypeStatsSnapshot(true);

        if (maxEntries <= 0 || snapshot.Count <= maxEntries)
            return snapshot;

        snapshot.RemoveRange(maxEntries, snapshot.Count - maxEntries);
        return snapshot;
    }

    public RunBlockedEventTypeStats GetMostUsedBlockedEventTypeStats()
    {
        List<RunBlockedEventTypeStats> snapshot = GetBlockedEventTypeStatsSnapshot(1);
        return snapshot.Count > 0 ? snapshot[0] : null;
    }

    public string GetTowerProgressionSummaryText()
    {
        SyncStatisticsSnapshot();
        return
            "Tower gebaut: <b>" + towersBuilt + "</b> | Build-Kosten: " + totalTowerBuildCost + "\n" +
            "Elite zerstörte Tower: <b>" + towersDestroyedByElite + "</b>" + (string.IsNullOrEmpty(lastEliteDestroyedTowerName) ? "" : " | zuletzt: " + lastEliteDestroyedTowerName) + "\n" +
            "Tower-XP: <b>" + totalTowerXPGained + "</b> | Level-Ups: " + totalTowerLevelUps + " | höchste Tower-Stufe: " + highestTowerLevelReached + "\n" +
            "Upgradepunkte: verdient " + totalUpgradePointsGained + " | ausgegeben " + totalUpgradePointsSpent + " | vorbereitete Meta-Punkte " + totalMetaPointsPrepared + "\n" +
            "Upgrades: Gold " + totalGoldUpgradesPurchased + " | Punkte " + totalPointUpgradesPurchased + "\n";
    }

    public bool HasLastWaveStatistics()
    {
        EnsureLists();
        LoadPersistedLastWaveStatisticsIfNeeded();
        return lastWaveStatistics != null && lastWaveStatistics.initialized;
    }

    public string GetLastWaveStatisticsText()
    {
        EnsureLists();
        LoadPersistedLastWaveStatisticsIfNeeded();

        if (!HasLastWaveStatistics())
            return "Noch keine Wave-Daten gespeichert.";

        return lastWaveStatistics.GetDetailText();
    }

    public bool HasAnyTrackedData()
    {
        return economy.totalGoldEarned > 0 ||
               economy.totalGoldSpent > 0 ||
               blockedEventsChosen > 0 ||
               towersBuilt > 0 ||
               towersDestroyedByElite > 0 ||
               totalTowerXPGained > 0 ||
               totalTowerLevelUps > 0 ||
               totalGoldUpgradesPurchased > 0 ||
               totalPointUpgradesPurchased > 0 ||
               eliteWavesCompleted > 0 ||
               eliteKills > 0 ||
               eliteLeaks > 0 ||
               eliteRewardsChosen > 0;
    }

    private RunBlockedEventTypeStats GetOrCreateBlockedEventTypeStats(string eventType)
    {
        EnsureLists();

        string safeType = string.IsNullOrEmpty(eventType) ? "Unknown" : eventType;

        foreach (RunBlockedEventTypeStats stats in blockedEventTypeStats)
        {
            if (stats != null && stats.eventType == safeType)
                return stats;
        }

        RunBlockedEventTypeStats newStats = new RunBlockedEventTypeStats
        {
            eventType = safeType
        };

        blockedEventTypeStats.Add(newStats);
        return newStats;
    }

    private int CountTrackedBlockedEventTypes()
    {
        int count = 0;

        if (blockedEventTypeStats == null)
            return count;

        foreach (RunBlockedEventTypeStats stats in blockedEventTypeStats)
        {
            if (stats != null && stats.choices > 0)
                count++;
        }

        return count;
    }

    private List<RunBlockedEventTypeStats> CreateBlockedEventTypeStatsSnapshot(bool sortByChoices)
    {
        List<RunBlockedEventTypeStats> snapshot = new List<RunBlockedEventTypeStats>();

        if (blockedEventTypeStats == null)
            return snapshot;

        foreach (RunBlockedEventTypeStats stats in blockedEventTypeStats)
        {
            if (stats != null && stats.choices > 0)
                snapshot.Add(stats.CreateCopy());
        }

        if (sortByChoices)
            snapshot.Sort(CompareBlockedEventTypeStats);

        return snapshot;
    }

    private static int CompareBlockedEventTypeStats(RunBlockedEventTypeStats first, RunBlockedEventTypeStats second)
    {
        if (first == null && second == null)
            return 0;

        if (first == null)
            return 1;

        if (second == null)
            return -1;

        int choicesComparison = second.choices.CompareTo(first.choices);
        if (choicesComparison != 0)
            return choicesComparison;

        int waveComparison = second.lastWaveNumber.CompareTo(first.lastWaveNumber);
        if (waveComparison != 0)
            return waveComparison;

        return string.Compare(first.eventType, second.eventType, System.StringComparison.Ordinal);
    }

    private RunTowerStatsRecord GetOrCreateTowerRecord(Tower tower)
    {
        EnsureLists();

        if (tower == null)
            return CreateFallbackTowerRecord();

        int instanceId = tower.GetInstanceID();

        foreach (RunTowerStatsRecord record in towerRecords)
        {
            if (record != null && record.instanceId == instanceId)
                return record;
        }

        RunTowerStatsRecord newRecord = new RunTowerStatsRecord();
        newRecord.InitializeFromTower(tower);
        towerRecords.Add(newRecord);
        return newRecord;
    }

    private RunTowerStatsRecord CreateFallbackTowerRecord()
    {
        RunTowerStatsRecord record = new RunTowerStatsRecord();
        record.towerName = "Unbekannter Tower";
        towerRecords.Add(record);
        return record;
    }

    private void RefreshAllTowerRecords()
    {
        if (autoDiscoverExistingTowers)
        {
            var towers = TowerRegistry.ActiveTowers;

            foreach (Tower tower in towers)
                GetOrCreateTowerRecord(tower).RefreshFromTower();
        }

        foreach (RunTowerStatsRecord record in towerRecords)
        {
            if (record == null)
                continue;

            record.RefreshFromTower();
            SyncTowerPeakValues(record);
        }

        SyncStatisticsSnapshot();
    }

    private void CaptureActiveTowerWaveStats()
    {
        if (activeWaveStatistics == null || !activeWaveStatistics.initialized)
            return;

        var towers = TowerRegistry.ActiveTowers;

        foreach (Tower tower in towers)
            CaptureTowerForActiveWave(tower);
    }

    private void CaptureTowerForActiveWave(Tower tower)
    {
        if (tower == null || activeWaveStatistics == null || !activeWaveStatistics.initialized)
            return;

        RunWaveTowerStatsRecord record = activeWaveStatistics.GetOrCreateTowerRecord(tower);

        if (record != null)
            record.RefreshFromTower();
    }

    private void LoadPersistedLastWaveStatisticsIfNeeded()
    {
        if (lastWaveStatisticsLoadedFromPrefs)
            return;

        lastWaveStatisticsLoadedFromPrefs = true;

        if (lastWaveStatistics != null && lastWaveStatistics.initialized)
            return;

        if (!PlayerPrefs.HasKey(LastWaveStatisticsPlayerPrefsKey))
            return;

        string json = PlayerPrefs.GetString(LastWaveStatisticsPlayerPrefsKey, "");

        if (string.IsNullOrEmpty(json))
            return;

        RunWaveStatistics loadedStatistics = JsonUtility.FromJson<RunWaveStatistics>(json);

        if (loadedStatistics == null || !loadedStatistics.initialized)
            return;

        if (lastWaveStatistics == null)
            lastWaveStatistics = new RunWaveStatistics();

        lastWaveStatistics.CopyFrom(loadedStatistics);
    }

    private void SavePersistedLastWaveStatistics()
    {
        if (lastWaveStatistics == null || !lastWaveStatistics.initialized)
            return;

        string json = JsonUtility.ToJson(lastWaveStatistics);

        if (string.IsNullOrEmpty(json))
            return;

        PlayerPrefs.SetString(LastWaveStatisticsPlayerPrefsKey, json);
        PlayerPrefs.Save();
        lastWaveStatisticsLoadedFromPrefs = true;
    }

    private void SyncTowerPeakValues(RunTowerStatsRecord record)
    {
        if (record == null)
            return;

        highestTowerLevelReached = Mathf.Max(highestTowerLevelReached, record.highestLevel);
        highestTowerVisualTierReached = Mathf.Max(highestTowerVisualTierReached, record.highestVisualTier);
    }

    private void IncrementUpgradeCategory(RunTowerStatsRecord record, TowerUpgradeCategory category, bool gold)
    {
        if (record == null)
            return;

        if (gold)
        {
            switch (category)
            {
                case TowerUpgradeCategory.Damage:
                    record.damageGoldUpgrades++;
                    break;
                case TowerUpgradeCategory.Range:
                    record.rangeGoldUpgrades++;
                    break;
                case TowerUpgradeCategory.FireRate:
                    record.fireRateGoldUpgrades++;
                    break;
                case TowerUpgradeCategory.Effect:
                    record.effectGoldUpgrades++;
                    break;
            }
        }
        else
        {
            switch (category)
            {
                case TowerUpgradeCategory.Damage:
                    record.damagePointUpgrades++;
                    break;
                case TowerUpgradeCategory.Range:
                    record.rangePointUpgrades++;
                    break;
                case TowerUpgradeCategory.FireRate:
                    record.fireRatePointUpgrades++;
                    break;
                case TowerUpgradeCategory.Effect:
                    record.effectPointUpgrades++;
                    break;
            }
        }
    }

    private void SyncStatisticsSnapshot()
    {
        if (statistics == null)
            statistics = new RunStatistics();

        statistics.initialized = true;
        statistics.totalGoldEarned = economy.totalGoldEarned;
        statistics.totalGoldSpent = economy.totalGoldSpent;
        statistics.goldFromEnemyKills = economy.enemyKillGoldEarned;
        statistics.goldFromWaveCompletion = economy.waveCompletionGoldEarned;
        statistics.goldFromBlockedEvents = economy.blockedEventGoldEarned;
        statistics.goldFromOther = economy.otherGoldEarned;
        statistics.goldBonusFromRewardModifiers = economy.goldAddedByRewardModifiers;
        statistics.blockedEventsChosen = blockedEventsChosen;
        statistics.lifeFromBlockedEvents = lifeFromBlockedEvents;
        statistics.timedBlockedBuildPhases = timedBlockedBuildPhases;
        statistics.totalBlockedBuildPhaseDuration = totalBlockedBuildPhaseDuration;
        statistics.lastBlockedEventName = lastBlockedEventName;
        statistics.lastBlockedEventType = lastBlockedEventType;
        SyncBlockedEventTypeStatsSnapshot();
        statistics.towersBuilt = towersBuilt;
        statistics.towersDestroyedByElite = towersDestroyedByElite;
        statistics.totalTowerXPGranted = totalTowerXPGained;
        statistics.towerLevelUps = totalTowerLevelUps;
        statistics.highestTowerLevel = highestTowerLevelReached;
        statistics.upgradePointsEarned = totalUpgradePointsGained;
        statistics.upgradePointsSpent = totalUpgradePointsSpent;
        statistics.metaPointsPrepared = totalMetaPointsPrepared;
        statistics.visualTierUps = highestTowerVisualTierReached;
        statistics.goldUpgradesBought = totalGoldUpgradesPurchased;
        statistics.pointUpgradesBought = totalPointUpgradesPurchased;
        statistics.eliteWavesCompleted = eliteWavesCompleted;
        statistics.eliteKills = eliteKills;
        statistics.eliteLeaks = eliteLeaks;
        statistics.eliteRewardsChosen = eliteRewardsChosen;
        statistics.lastEliteDestroyedTowerName = lastEliteDestroyedTowerName;
        statistics.lastEliteRewardName = lastEliteRewardName;
    }

    private void SyncBlockedEventTypeStatsSnapshot()
    {
        if (statistics.blockedEventTypeStats == null)
            statistics.blockedEventTypeStats = new List<RunBlockedEventTypeStats>();
        else
            statistics.blockedEventTypeStats.Clear();

        List<RunBlockedEventTypeStats> snapshot = CreateBlockedEventTypeStatsSnapshot(true);
        foreach (RunBlockedEventTypeStats stats in snapshot)
            statistics.blockedEventTypeStats.Add(stats);
    }
}
