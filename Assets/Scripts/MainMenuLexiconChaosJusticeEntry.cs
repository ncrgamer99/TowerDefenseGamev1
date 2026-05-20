using UnityEngine;

public enum MainMenuLexiconChaosJusticeEntryType
{
    Justice,
    RiskModifier,
    SafeOption
}

[System.Serializable]
public class MainMenuLexiconChaosJusticeEntry
{
    [Header("Identity")]
    public string entryId = "chaos_justice";
    public MainMenuLexiconChaosJusticeEntryType entryType = MainMenuLexiconChaosJusticeEntryType.RiskModifier;
    public string title = "Chaos & Gerechtigkeit";
    public int sortOrder = 0;

    [Header("Description")]
    [TextArea(1, 3)]
    public string description = "";

    [Header("Effect")]
    [TextArea(1, 4)]
    public string riskText = "Kein Zusatzrisiko.";

    [TextArea(1, 4)]
    public string rewardText = "Kein Zusatzreward.";

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(title);
    }
}
