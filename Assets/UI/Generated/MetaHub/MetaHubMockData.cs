using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class MetaHubMockData
{
    public static MetaHubData Create()
    {
        MetaHubData data = new MetaHubData();

        data.resources.Add(Resource("gold", "Gold", "G", 12450, MetaHubTone.Gold));
        data.resources.Add(Resource("xp", "XP", "XP", 8760, MetaHubTone.Cyan));
        data.resources.Add(Resource("chaos", "Chaos-Wissen", "C", 350, MetaHubTone.Purple));
        data.resources.Add(Resource("special", "Ressource", "R", 15, MetaHubTone.Purple));

        data.account.level = 11;
        data.account.currentXP = 3250;
        data.account.requiredXP = 7500;

        AddNavigation(data);

        data.metricCards.Add(Metric("tower_mastery", "TOWER MASTERY", "TM", "11", "Aktive Tower", 11, 17, true, MetaHubTone.Gold));
        data.metricCards.Add(Metric("chaos_wissen", "CHAOS-WISSEN", "CW", "0", "Forschungsstufen", 0, 25, true, MetaHubTone.Purple));
        data.metricCards.Add(Metric("risikokerne", "RISIKOKERNE", "RK", "0", "Aktive Kerne", 0, 0, false, MetaHubTone.Red));
        data.metricCards.Add(Metric("bauplaene", "BAUPLÄNE", "BP", "0", "Freigeschaltet", 0, 0, false, MetaHubTone.Blue));
        data.metricCards.Add(Metric("elite_jagd", "ELITE-JAGD", "EJ", "0", "Elite besiegt", 0, 0, false, MetaHubTone.Red));

        data.progressStats.Add(SideStat("free_points", "Freie Punkte", "0", "P", MetaHubTone.Purple));
        data.progressStats.Add(SideStat("chaos_level", "Chaos-Level", "0", "C", MetaHubTone.Red));
        data.progressStats.Add(SideStat("gold_justice", "Gold-Gerechtigkeit", "0", "G", MetaHubTone.Gold));
        data.progressStats.Add(SideStat("xp_justice", "XP-Gerechtigkeit", "0", "XP", MetaHubTone.Cyan));

        data.chaosJustice.safetyScore = 6;
        data.chaosJustice.chaosScore = 0;
        data.chaosJustice.safetyPercent = 100;
        data.chaosJustice.chaosPercent = 0;
        data.chaosJustice.goldJusticeLevel = 0;
        data.chaosJustice.xpJusticeLevel = 0;
        data.chaosJustice.chaosLevel = 0;
        data.chaosJustice.stabilityLabel = "Stabil";

        data.nextGoals.Add(Goal("tower_12", "Tower Mastery Level 12", "TM", 11, 12, MetaHubTone.Gold));
        data.nextGoals.Add(Goal("chaos_research_1", "Chaos-Forschung Stufe 1", "CW", 0, 1, MetaHubTone.Purple));
        data.nextGoals.Add(Goal("risk_core", "Risikokern aktivieren", "RK", 0, 1, MetaHubTone.Red));
        data.nextGoals.Add(Goal("elite_kill", "Elite besiegen", "EJ", 0, 1, MetaHubTone.Red));

        data.activeBuffs.Add(Effect("gold_boost", "Gold-Boost I", "+10% Goldgewinn", "2 Wellen", "G", MetaHubTone.Green));
        data.activeBuffs.Add(Effect("xp_boost", "XP-Boost I", "+10% XP-Gewinn", "2 Wellen", "XP", MetaHubTone.Green));

        data.activeRisks.Add(Effect("runner", "Mehr Runner I", "+20% Runner", "3 Wellen", "R", MetaHubTone.Red));
        data.activeRisks.Add(Effect("spawn", "Schnellere Spawns I", "-15% Spawn-Delay", "3 Wellen", "S", MetaHubTone.Red));

        data.activeKeystones.Add(Keystone("red_core", "Roter Kern", "K", 1, 1, 3, MetaHubTone.Red));
        data.activeKeystones.Add(Keystone("violet_core", "Violetter Kern", "K", 1, 1, 3, MetaHubTone.Purple));
        data.activeKeystones.Add(Keystone("blue_core", "Blauer Kern", "K", 1, 1, 3, MetaHubTone.Cyan));

        data.lastRunStats.Add(RunStat("waves", "Wellen überlebt", "10"));
        data.lastRunStats.Add(RunStat("boss", "Boss besiegt", "1"));
        data.lastRunStats.Add(RunStat("chaos", "Chaos-Level erreicht", "0"));
        data.lastRunStats.Add(RunStat("score", "Punkte verdient", "8.450"));

        return data;
    }

    public static MetaHubData CreateFromGame(GameManager gameManager)
    {
        MetaHubData data = Create();

        if (gameManager == null)
            return data;

        GeneralMetaProgressionManager general = gameManager.GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = gameManager.GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = gameManager.GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = gameManager.GetEliteHuntProgressionManager();
        TowerMasteryManager towerMastery = gameManager.GetTowerMasteryManager();
        ChaosJusticeManager chaosJustice = gameManager.GetChaosJusticeManager();
        RunStatistics runStats = gameManager.GetRunStatistics();

        int accountLevel = general != null ? Mathf.Max(1, general.accountLevel) : 1;
        int accountCurrentXP = general != null ? Mathf.Max(0, general.GetAccountXPIntoCurrentLevel()) : 0;
        int accountRequiredXP = general != null ? Mathf.Max(1, general.GetXPToNextAccountLevel()) : 1;

        data.account.level = accountLevel;
        data.account.currentXP = accountCurrentXP;
        data.account.requiredXP = accountRequiredXP;

        SetResource(data, "gold", Mathf.Max(0, gameManager.gold));
        SetResource(data, "xp", general != null ? Mathf.Max(0, general.accountXP) : 0);
        SetResource(data, "chaos", chaosResearch != null ? Mathf.Max(0, chaosResearch.chaosKnowledge) : 0);
        SetResource(data, "special", eliteHunt != null ? Mathf.Max(0, eliteHunt.eliteSeals) : 0);

        int activeTowerCount = CountActiveTowerMasteries(towerMastery);
        int maxTowerCount = TowerMasteryManager.GetOrderedTowerRoles().Length;
        int freeTowerPoints = CountFreeTowerPoints(towerMastery);
        int topTowerLevel = GetHighestTowerMasteryLevel(towerMastery);

        SetMetric(data, "tower_mastery", activeTowerCount.ToString(), "Aktive Tower", activeTowerCount, Mathf.Max(1, maxTowerCount), true);
        SetMetric(data, "chaos_wissen", CountPurchasedChaosResearch(chaosResearch).ToString(), "Forschungsstufen", CountPurchasedChaosResearch(chaosResearch), CountChaosResearchDefinitions(chaosResearch), true);
        SetMetric(data, "risikokerne", chaosResearch != null ? Mathf.Max(0, chaosResearch.riftCores).ToString() : "0", "Aktive Kerne", 0, 0, false);
        SetMetric(data, "bauplaene", pathTechnique != null ? Mathf.Max(0, pathTechnique.blueprints).ToString() : "0", "Freigeschaltet", 0, 0, false);
        SetMetric(data, "elite_jagd", eliteHunt != null ? Mathf.Max(0, eliteHunt.totalEliteKillsEver).ToString() : "0", "Elite besiegt", 0, 0, false);

        SetSideStat(data, "free_points", freeTowerPoints.ToString());
        SetSideStat(data, "chaos_level", chaosJustice != null ? chaosJustice.GetChaosLevel().ToString() : "0");
        SetSideStat(data, "gold_justice", chaosJustice != null && chaosJustice.runData != null ? chaosJustice.runData.goldJusticeLevel.ToString() : "0");
        SetSideStat(data, "xp_justice", chaosJustice != null && chaosJustice.runData != null ? chaosJustice.runData.xpJusticeLevel.ToString() : "0");

        if (chaosJustice != null)
        {
            ChaosJusticeBalanceSnapshot snapshot = chaosJustice.GetBalanceSnapshot();
            data.chaosJustice.safetyScore = snapshot.safetyScore;
            data.chaosJustice.chaosScore = snapshot.chaosScore;
            data.chaosJustice.safetyPercent = snapshot.safetyPercent;
            data.chaosJustice.chaosPercent = snapshot.chaosPercent;
            data.chaosJustice.stabilityLabel = snapshot.label;
            data.chaosJustice.chaosLevel = chaosJustice.GetChaosLevel();
            data.chaosJustice.goldJusticeLevel = chaosJustice.runData != null ? chaosJustice.runData.goldJusticeLevel : 0;
            data.chaosJustice.xpJusticeLevel = chaosJustice.runData != null ? chaosJustice.runData.xpJusticeLevel : 0;
        }

        data.nextGoals.Clear();
        data.nextGoals.Add(Goal("tower_next", "Tower Mastery Level " + (topTowerLevel + 1), "TM", topTowerLevel, topTowerLevel + 1, MetaHubTone.Gold));
        data.nextGoals.Add(Goal("chaos_research_next", "Chaos-Forschung Stufe " + (CountPurchasedChaosResearch(chaosResearch) + 1), "CW", CountPurchasedChaosResearch(chaosResearch), Mathf.Max(1, CountPurchasedChaosResearch(chaosResearch) + 1), MetaHubTone.Purple));
        data.nextGoals.Add(Goal("risk_core_next", "Risikokern aktivieren", "RK", chaosResearch != null ? chaosResearch.riftCores : 0, 1, MetaHubTone.Red));
        data.nextGoals.Add(Goal("elite_next", "Elite besiegen", "EJ", eliteHunt != null ? eliteHunt.totalEliteKillsEver : 0, Mathf.Max(1, (eliteHunt != null ? eliteHunt.totalEliteKillsEver : 0) + 1), MetaHubTone.Red));

        data.activeBuffs.Clear();
        AddLiveBuffs(data, general);

        data.activeRisks.Clear();
        if (chaosJustice != null)
        {
            List<string> risks = chaosJustice.GetSelectedRiskModifierDisplayNames();
            for (int i = 0; i < risks.Count; i++)
                data.activeRisks.Add(Effect("risk_" + i, risks[i], "Aktiver Risiko-Modifikator", "", "R", MetaHubTone.Red));
        }

        data.activeKeystones.Clear();
        AddLiveKeystones(data, towerMastery);

        if (data.activeKeystones.Count == 0)
            data.activeKeystones.Add(Keystone("no_keystone", "Keystone frei", "K", 0, 0, 1, MetaHubTone.Neutral));

        data.lastRunStats.Clear();
        int bossKills = gameManager.waveHistory != null ? gameManager.waveHistory.GetBossKills() : 0;
        int pointsEarned = runStats != null ? runStats.totalGoldEarned + runStats.totalTowerXPGranted : 0;
        data.lastRunStats.Add(RunStat("waves", "Wellen überlebt", Mathf.Max(0, gameManager.waveNumber).ToString()));
        data.lastRunStats.Add(RunStat("boss", "Boss besiegt", Mathf.Max(0, bossKills).ToString()));
        data.lastRunStats.Add(RunStat("chaos", "Chaos-Level erreicht", chaosJustice != null ? chaosJustice.GetChaosLevel().ToString() : "0"));
        data.lastRunStats.Add(RunStat("score", "Punkte verdient", FormatNumber(Mathf.Max(0, pointsEarned))));

        return data;
    }

    public static string FormatNumber(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
    }

    private static void AddNavigation(MetaHubData data)
    {
        data.navigation.Add(Nav("overview", "ÜBERSICHT", "H", MetaHubTone.Gold, true));
        data.navigation.Add(Nav("general", "ALLGEMEIN", "A", MetaHubTone.Neutral, false));
        data.navigation.Add(Nav("tower", "TOWER MASTERY", "T", MetaHubTone.Purple, false));
        data.navigation.Add(Nav("chaos", "CHAOS-FORSCHUNG", "C", MetaHubTone.Purple, false));
        data.navigation.Add(Nav("path", "VERBAU / PFADTECHNIK", "P", MetaHubTone.Cyan, false));
        data.navigation.Add(Nav("elite", "ELITE-JAGD", "E", MetaHubTone.Red, false));
    }

    private static MetaHubResourceData Resource(string id, string label, string iconText, int value, MetaHubTone tone)
    {
        MetaHubResourceData item = new MetaHubResourceData();
        item.id = id;
        item.label = label;
        item.iconText = iconText;
        item.value = value;
        item.tone = tone;
        return item;
    }

    private static MetaHubNavItemData Nav(string id, string label, string iconText, MetaHubTone tone, bool selected)
    {
        MetaHubNavItemData item = new MetaHubNavItemData();
        item.id = id;
        item.label = label;
        item.iconText = iconText;
        item.tone = tone;
        item.selected = selected;
        return item;
    }

    private static MetaHubMetricCardData Metric(string id, string title, string iconText, string valueText, string caption, int current, int maximum, bool showProgress, MetaHubTone tone)
    {
        MetaHubMetricCardData item = new MetaHubMetricCardData();
        item.id = id;
        item.title = title;
        item.iconText = iconText;
        item.valueText = valueText;
        item.caption = caption;
        item.current = current;
        item.maximum = maximum;
        item.showProgress = showProgress;
        item.tone = tone;
        return item;
    }

    private static MetaHubSideStatData SideStat(string id, string label, string valueText, string iconText, MetaHubTone tone)
    {
        MetaHubSideStatData item = new MetaHubSideStatData();
        item.id = id;
        item.label = label;
        item.valueText = valueText;
        item.iconText = iconText;
        item.tone = tone;
        return item;
    }

    private static MetaHubGoalData Goal(string id, string title, string iconText, int current, int required, MetaHubTone tone)
    {
        MetaHubGoalData item = new MetaHubGoalData();
        item.id = id;
        item.title = title;
        item.iconText = iconText;
        item.current = current;
        item.required = Mathf.Max(1, required);
        item.tone = tone;
        return item;
    }

    private static MetaHubEffectData Effect(string id, string title, string description, string durationText, string iconText, MetaHubTone tone)
    {
        MetaHubEffectData item = new MetaHubEffectData();
        item.id = id;
        item.title = title;
        item.description = description;
        item.durationText = durationText;
        item.iconText = iconText;
        item.tone = tone;
        return item;
    }

    private static MetaHubKeystoneData Keystone(string id, string title, string iconText, int level, int current, int maximum, MetaHubTone tone)
    {
        MetaHubKeystoneData item = new MetaHubKeystoneData();
        item.id = id;
        item.title = title;
        item.iconText = iconText;
        item.level = level;
        item.current = current;
        item.maximum = Mathf.Max(1, maximum);
        item.tone = tone;
        return item;
    }

    private static MetaHubRunStatData RunStat(string id, string label, string valueText)
    {
        MetaHubRunStatData item = new MetaHubRunStatData();
        item.id = id;
        item.label = label;
        item.valueText = valueText;
        return item;
    }

    private static void SetResource(MetaHubData data, string id, int value)
    {
        for (int i = 0; i < data.resources.Count; i++)
        {
            if (data.resources[i].id == id)
            {
                data.resources[i].value = value;
                return;
            }
        }
    }

    private static void SetMetric(MetaHubData data, string id, string valueText, string caption, int current, int maximum, bool showProgress)
    {
        for (int i = 0; i < data.metricCards.Count; i++)
        {
            if (data.metricCards[i].id == id)
            {
                data.metricCards[i].valueText = valueText;
                data.metricCards[i].caption = caption;
                data.metricCards[i].current = current;
                data.metricCards[i].maximum = maximum;
                data.metricCards[i].showProgress = showProgress;
                return;
            }
        }
    }

    private static void SetSideStat(MetaHubData data, string id, string valueText)
    {
        for (int i = 0; i < data.progressStats.Count; i++)
        {
            if (data.progressStats[i].id == id)
            {
                data.progressStats[i].valueText = valueText;
                return;
            }
        }
    }

    private static void AddLiveBuffs(MetaHubData data, GeneralMetaProgressionManager general)
    {
        if (data == null || general == null)
            return;

        int startGold = Mathf.Max(0, general.GetActiveStartGoldBonus());
        if (startGold > 0)
            data.activeBuffs.Add(Effect("start_gold", "Startgold", "+" + FormatNumber(startGold) + " Gold", "Loadout", "G", MetaHubTone.Green));

        int startLife = Mathf.Max(0, general.GetActiveStartLifeBonus());
        if (startLife > 0)
            data.activeBuffs.Add(Effect("start_life", "Startleben", "+" + startLife + " Leben", "Loadout", "L", MetaHubTone.Green));

        int startPath = Mathf.Max(0, general.GetActiveStartPathBonus());
        if (startPath > 0)
            data.activeBuffs.Add(Effect("start_path", "Startweg", "+" + startPath + " Feld(er)", "Loadout", "P", MetaHubTone.Green));

        int discount = Mathf.Max(0, general.GetAvailableFirstTowerDiscount());
        if (discount > 0)
            data.activeBuffs.Add(Effect("tower_discount", "Tower-Rabatt", "-" + discount + "% erster Tower", "Loadout", "T", MetaHubTone.Green));

        if (general.IsStartScoutActive())
            data.activeBuffs.Add(Effect("start_scout", "Start-Scout", "Naechste Wave sichtbar", "Loadout", "S", MetaHubTone.Green));
    }

    private static int CountActiveTowerMasteries(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null || towerMastery.roleProfiles == null)
            return 0;

        int count = 0;
        for (int i = 0; i < towerMastery.roleProfiles.Count; i++)
        {
            TowerMasteryRoleProfile profile = towerMastery.roleProfiles[i];
            if (profile != null && (profile.masteryXP > 0 || profile.totalEarnedPoints > 0 || profile.bestLevelEver > 1))
                count++;
        }

        return count;
    }

    private static int CountFreeTowerPoints(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null || towerMastery.roleProfiles == null)
            return 0;

        int points = 0;
        for (int i = 0; i < towerMastery.roleProfiles.Count; i++)
        {
            TowerMasteryRoleProfile profile = towerMastery.roleProfiles[i];
            if (profile != null)
                points += Mathf.Max(0, profile.unspentPoints);
        }

        return points;
    }

    private static int GetHighestTowerMasteryLevel(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null)
            return 1;

        int highest = 1;
        TowerRole[] roles = TowerMasteryManager.GetOrderedTowerRoles();
        for (int i = 0; i < roles.Length; i++)
            highest = Mathf.Max(highest, towerMastery.GetMasteryLevel(roles[i]));

        return highest;
    }

    private static int CountPurchasedChaosResearch(ChaosResearchProgressionManager chaosResearch)
    {
        if (chaosResearch == null || chaosResearch.nodeStates == null)
            return 0;

        int count = 0;
        for (int i = 0; i < chaosResearch.nodeStates.Count; i++)
        {
            if (chaosResearch.nodeStates[i] != null && chaosResearch.nodeStates[i].purchased)
                count++;
        }

        return count;
    }

    private static int CountChaosResearchDefinitions(ChaosResearchProgressionManager chaosResearch)
    {
        if (chaosResearch == null || chaosResearch.definitions == null)
            return 1;

        return Mathf.Max(1, chaosResearch.definitions.Count);
    }

    private static void AddLiveKeystones(MetaHubData data, TowerMasteryManager towerMastery)
    {
        if (towerMastery == null || towerMastery.roleProfiles == null)
            return;

        for (int i = 0; i < towerMastery.roleProfiles.Count && data.activeKeystones.Count < 3; i++)
        {
            TowerMasteryRoleProfile profile = towerMastery.roleProfiles[i];
            if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
                continue;

            string title = TowerMasteryManager.GetTowerDisplayName(profile.towerRole);
            data.activeKeystones.Add(Keystone(profile.activeKeystoneId, title, "K", Mathf.Max(1, towerMastery.GetMasteryLevel(profile.towerRole)), profile.unspentPoints, Mathf.Max(1, profile.totalEarnedPoints), MetaHubTone.Cyan));
        }
    }
}
