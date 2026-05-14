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

    [Header("Tower")]
    public int towersBuilt = 0;
    public int totalTowerXPGranted = 0;
    public int towerLevelUps = 0;
    public int highestTowerLevel = 1;
    public int upgradePointsEarned = 0;
    public int upgradePointsSpent = 0;
    public int metaPointsPrepared = 0;
    public int visualTierUps = 0;
    public int goldUpgradesBought = 0;
    public int pointUpgradesBought = 0;

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
        towersBuilt = 0;
        totalTowerXPGranted = 0;
        towerLevelUps = 0;
        highestTowerLevel = 1;
        upgradePointsEarned = 0;
        upgradePointsSpent = 0;
        metaPointsPrepared = 0;
        visualTierUps = 0;
        goldUpgradesBought = 0;
        pointUpgradesBought = 0;
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
        return "Tower gebaut " + towersBuilt + " | Upgradepunkte verdient/ausgegeben " + upgradePointsEarned + "/" + upgradePointsSpent + " | vorbereitete Meta-Punkte " + metaPointsPrepared + " | Visual-Tier-Ups " + visualTierUps;
    }

    public string GetUpgradeSummary()
    {
        return "Upgrades: Gold " + goldUpgradesBought + " | Punkte " + pointUpgradesBought;
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

    [Header("Tower Progression")]
    public int towersBuilt = 0;
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

    [Header("Tower Records")]
    public List<RunTowerStatsRecord> towerRecords = new List<RunTowerStatsRecord>();

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

        if (towerRecords == null)
            towerRecords = new List<RunTowerStatsRecord>();
    }

    public void ClearRunStats()
    {
        EnsureLists();
        economy.Clear();
        statistics.Clear();
        towersBuilt = 0;
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
        towerRecords.Clear();
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


    public void RecordBlockedEventChoice(string eventName, string eventType, int goldGained, int livesGained, float buildPhaseDuration)
    {
        EnsureLists();

        blockedEventsChosen++;
        lifeFromBlockedEvents += Mathf.Max(0, livesGained);
        timedBlockedBuildPhases++;
        totalBlockedBuildPhaseDuration += Mathf.Max(0f, buildPhaseDuration);
        lastBlockedEventName = string.IsNullOrEmpty(eventName) ? "Unbekannt" : eventName;
        lastBlockedEventType = string.IsNullOrEmpty(eventType) ? "Unknown" : eventType;

        SyncStatisticsSnapshot();

        if (logRunStatEvents)
        {
            Debug.Log(
                "RunStats: Verbau-Event " + lastBlockedEventName +
                " | Gold +" + Mathf.Max(0, goldGained) +
                " | Leben +" + Mathf.Max(0, livesGained) +
                " | Buildphase " + Mathf.Max(0f, buildPhaseDuration).ToString("0.0") + "s."
            );
        }
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
            "Ausgaben: Towerbau " + economy.towerBuildGoldSpent + " | Gold-Upgrades " + economy.goldUpgradeGoldSpent + " | Sonstiges " + economy.otherGoldSpent + "\n";
    }

    public string GetTowerProgressionSummaryText()
    {
        SyncStatisticsSnapshot();
        return
            "Tower gebaut: <b>" + towersBuilt + "</b> | Build-Kosten: " + totalTowerBuildCost + "\n" +
            "Tower-XP: <b>" + totalTowerXPGained + "</b> | Level-Ups: " + totalTowerLevelUps + " | höchste Tower-Stufe: " + highestTowerLevelReached + "\n" +
            "Upgradepunkte: verdient " + totalUpgradePointsGained + " | ausgegeben " + totalUpgradePointsSpent + " | vorbereitete Meta-Punkte " + totalMetaPointsPrepared + "\n" +
            "Upgrades: Gold " + totalGoldUpgradesPurchased + " | Punkte " + totalPointUpgradesPurchased + "\n";
    }

    public bool HasAnyTrackedData()
    {
        return economy.totalGoldEarned > 0 || economy.totalGoldSpent > 0 || blockedEventsChosen > 0 || towersBuilt > 0 || totalTowerXPGained > 0 || totalTowerLevelUps > 0 || totalGoldUpgradesPurchased > 0 || totalPointUpgradesPurchased > 0;
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
            Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude);

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
        statistics.towersBuilt = towersBuilt;
        statistics.totalTowerXPGranted = totalTowerXPGained;
        statistics.towerLevelUps = totalTowerLevelUps;
        statistics.highestTowerLevel = highestTowerLevelReached;
        statistics.upgradePointsEarned = totalUpgradePointsGained;
        statistics.upgradePointsSpent = totalUpgradePointsSpent;
        statistics.metaPointsPrepared = totalMetaPointsPrepared;
        statistics.visualTierUps = highestTowerVisualTierReached;
        statistics.goldUpgradesBought = totalGoldUpgradesPurchased;
        statistics.pointUpgradesBought = totalPointUpgradesPurchased;
    }
}
