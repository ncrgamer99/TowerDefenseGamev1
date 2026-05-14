using UnityEngine;

public enum ChaosLexiconCategory
{
    Grundlagen,
    Gerechtigkeit,
    Risiko,
    Waves,
    Gegner,
    Fairness,
    Auswertung,
    Zukunft
}

[System.Serializable]
public class ChaosLexiconEntry
{
    [Header("Identity")]
    public string entryId = "entry";
    public string title = "Eintrag";
    public ChaosLexiconCategory category = ChaosLexiconCategory.Grundlagen;
    public int sortOrder = 0;

    [Header("Text")]
    [TextArea(2, 5)]
    public string shortText = "";

    [TextArea(5, 16)]
    public string detailText = "";

    [Header("Unlock / Visibility")]
    public bool unlockedByDefault = true;
    public bool discovered = false;
    public bool showAsLockedTeaser = false;
    public bool futureOnly = false;

    [Header("Debug")]
    public string sourceTag = "Default";

    public bool IsUnlocked()
    {
        return unlockedByDefault || discovered;
    }

    public bool IsVisible(bool showLockedTeasers)
    {
        if (IsUnlocked())
            return true;

        return showLockedTeasers && showAsLockedTeaser;
    }
}
