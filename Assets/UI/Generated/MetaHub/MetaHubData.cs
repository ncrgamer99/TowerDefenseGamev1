using System;
using System.Collections.Generic;
using UnityEngine;

public enum MetaHubTone
{
    Neutral,
    Gold,
    Cyan,
    Purple,
    Red,
    Green,
    Blue
}

[Serializable]
public class MetaHubData
{
    public string screenTitle = "TOWER DEFENSE";
    public string screenSubtitle = "META-HUB";
    public string selectedSectionTitle = "ÜBERSICHT";
    public string footerTip = "Tipp: Aktiviere Keystones im Tower Mastery und baue dein System strategisch auf.";

    public List<MetaHubResourceData> resources = new List<MetaHubResourceData>();
    public MetaHubAccountData account = new MetaHubAccountData();
    public List<MetaHubNavItemData> navigation = new List<MetaHubNavItemData>();
    public List<MetaHubMetricCardData> metricCards = new List<MetaHubMetricCardData>();
    public List<MetaHubSideStatData> progressStats = new List<MetaHubSideStatData>();
    public MetaHubChaosJusticeData chaosJustice = new MetaHubChaosJusticeData();
    public List<MetaHubGoalData> nextGoals = new List<MetaHubGoalData>();
    public List<MetaHubEffectData> activeBuffs = new List<MetaHubEffectData>();
    public List<MetaHubEffectData> activeRisks = new List<MetaHubEffectData>();
    public List<MetaHubKeystoneData> activeKeystones = new List<MetaHubKeystoneData>();
    public List<MetaHubRunStatData> lastRunStats = new List<MetaHubRunStatData>();
}

[Serializable]
public class MetaHubResourceData
{
    public string id = "";
    public string label = "";
    public string iconText = "";
    public int value = 0;
    public MetaHubTone tone = MetaHubTone.Neutral;
}

[Serializable]
public class MetaHubAccountData
{
    public int level = 1;
    public int currentXP = 0;
    public int requiredXP = 1;
}

[Serializable]
public class MetaHubNavItemData
{
    public string id = "";
    public string label = "";
    public string iconText = "";
    public MetaHubTone tone = MetaHubTone.Neutral;
    public bool selected = false;
}

[Serializable]
public class MetaHubMetricCardData
{
    public string id = "";
    public string title = "";
    public string iconText = "";
    public string valueText = "";
    public string caption = "";
    public int current = 0;
    public int maximum = 0;
    public bool showProgress = false;
    public MetaHubTone tone = MetaHubTone.Neutral;
}

[Serializable]
public class MetaHubSideStatData
{
    public string id = "";
    public string label = "";
    public string valueText = "";
    public string iconText = "";
    public MetaHubTone tone = MetaHubTone.Neutral;
}

[Serializable]
public class MetaHubChaosJusticeData
{
    public int safetyScore = 0;
    public int chaosScore = 0;
    public int safetyPercent = 100;
    public int chaosPercent = 0;
    public int goldJusticeLevel = 0;
    public int xpJusticeLevel = 0;
    public int chaosLevel = 0;
    public string stabilityLabel = "Stabil";
}

[Serializable]
public class MetaHubGoalData
{
    public string id = "";
    public string title = "";
    public string iconText = "";
    public int current = 0;
    public int required = 1;
    public MetaHubTone tone = MetaHubTone.Neutral;
}

[Serializable]
public class MetaHubEffectData
{
    public string id = "";
    public string title = "";
    public string description = "";
    public string durationText = "";
    public string iconText = "";
    public MetaHubTone tone = MetaHubTone.Neutral;
}

[Serializable]
public class MetaHubKeystoneData
{
    public string id = "";
    public string title = "";
    public string iconText = "";
    public int level = 1;
    public int current = 0;
    public int maximum = 1;
    public MetaHubTone tone = MetaHubTone.Neutral;
}

[Serializable]
public class MetaHubRunStatData
{
    public string id = "";
    public string label = "";
    public string valueText = "";
}
