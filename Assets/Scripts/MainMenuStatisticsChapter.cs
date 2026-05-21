using UnityEngine;

public enum MainMenuStatisticsChapterType
{
    Total,
    LastRun,
    LastWave
}

[System.Serializable]
public class MainMenuStatisticsChapterDefinition
{
    [Header("Identity")]
    public string chapterId = "statistics_chapter";
    public MainMenuStatisticsChapterType chapterType = MainMenuStatisticsChapterType.Total;
    public string title = "Statistik";
    public int sortOrder = 0;

    [Header("Availability")]
    public bool visible = true;
    public bool preparedForFutureContent = true;

    public bool IsVisible()
    {
        return visible;
    }
}
