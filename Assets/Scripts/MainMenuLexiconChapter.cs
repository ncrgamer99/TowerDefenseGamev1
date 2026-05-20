using UnityEngine;

public enum MainMenuLexiconChapterType
{
    Enemies,
    Towers,
    ChaosJustice,
    BlockedBuilding,
    MetaProgression
}

[System.Serializable]
public class MainMenuLexiconChapterDefinition
{
    [Header("Identity")]
    public string chapterId = "chapter";
    public MainMenuLexiconChapterType chapterType = MainMenuLexiconChapterType.Enemies;
    public string title = "Kapitel";
    public int sortOrder = 0;

    [Header("Availability")]
    public bool visible = true;
    public bool preparedForFutureContent = true;

    public bool IsVisible()
    {
        return visible;
    }
}
