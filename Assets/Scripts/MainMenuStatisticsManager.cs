using System.Collections.Generic;
using UnityEngine;

public class MainMenuStatisticsManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public MainMenuStatisticsUI statisticsUI;
    public Canvas targetCanvas;
    public Transform rootParent;

    [Header("Setup")]
    public bool autoCreateUIIfMissing = true;
    public bool createDefaultChaptersOnStart = true;

    [Header("Chapters")]
    public List<MainMenuStatisticsChapterDefinition> chapters = new List<MainMenuStatisticsChapterDefinition>();

    [Header("Runtime")]
    public bool isOpen = false;
    public MainMenuStatisticsChapterType selectedChapter = MainMenuStatisticsChapterType.Total;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        EnsureChapterList();

        if (createDefaultChaptersOnStart)
            CreateDefaultChaptersIfNeeded();
    }

    private void Start()
    {
        EnsureInitialized();

        if (!isOpen)
            CloseStatistics();
    }

    public void Connect(GameManager manager)
    {
        gameManager = manager;

        if (gameManager != null)
        {
            targetCanvas = gameManager.startMenuCanvas;
            rootParent = gameManager.startMenuRoot != null ? gameManager.startMenuRoot.transform : null;
        }

        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        EnsureChapterList();

        if (createDefaultChaptersOnStart)
            CreateDefaultChaptersIfNeeded();

        ResolveReferences();
        EnsureSelectedChapter();
    }

    public void OpenStatistics()
    {
        EnsureInitialized();

        if (gameManager != null && !gameManager.startMenuOpen && !gameManager.CanOpenAuxiliaryModalUI())
            return;

        if (gameManager != null && !gameManager.startMenuOpen)
        {
            gameManager.ClosePathAndBuildSelectionsForModal();
            gameManager.CloseTowerUIForModal();
        }

        isOpen = true;

        if (statisticsUI != null)
            statisticsUI.OpenStatistics();
    }

    public void CloseStatistics()
    {
        isOpen = false;

        if (statisticsUI != null)
            statisticsUI.CloseStatistics();
    }

    public void SelectChapter(MainMenuStatisticsChapterType chapterType)
    {
        MainMenuStatisticsChapterDefinition chapter = GetChapterByType(chapterType);

        if (chapter == null || !chapter.IsVisible())
            return;

        selectedChapter = chapter.chapterType;

        if (statisticsUI != null)
            statisticsUI.RefreshAll();
    }

    public MainMenuStatisticsChapterDefinition GetSelectedChapter()
    {
        EnsureSelectedChapter();
        return GetChapterByType(selectedChapter);
    }

    public MainMenuStatisticsChapterDefinition GetChapterByType(MainMenuStatisticsChapterType chapterType)
    {
        EnsureChapterList();

        foreach (MainMenuStatisticsChapterDefinition chapter in chapters)
        {
            if (chapter != null && chapter.chapterType == chapterType)
                return chapter;
        }

        return null;
    }

    public List<MainMenuStatisticsChapterDefinition> GetVisibleChapters()
    {
        EnsureChapterList();

        if (createDefaultChaptersOnStart)
            CreateDefaultChaptersIfNeeded();

        List<MainMenuStatisticsChapterDefinition> result = new List<MainMenuStatisticsChapterDefinition>();

        foreach (MainMenuStatisticsChapterDefinition chapter in chapters)
        {
            if (chapter != null && chapter.IsVisible())
                result.Add(chapter);
        }

        result.Sort(CompareChapters);
        return result;
    }

    public string GetSelectedChapterTitle()
    {
        MainMenuStatisticsChapterDefinition chapter = GetSelectedChapter();
        return chapter != null ? chapter.title : "Keine Statistik";
    }

    public string GetPreparedChapterText()
    {
        return "Dieses Statistik-Kapitel ist vorbereitet.\nInhalte folgen in einem späteren Schritt.";
    }

    public string GetLastWaveDetailText()
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats == null || !stats.HasLastWaveStatistics())
            return "Noch keine Wave-Daten gespeichert.";

        return stats.GetLastWaveStatisticsText();
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (targetCanvas == null && gameManager != null)
            targetCanvas = gameManager.startMenuCanvas;

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (rootParent == null && gameManager != null && gameManager.startMenuRoot != null)
            rootParent = gameManager.startMenuRoot.transform;

        if (statisticsUI == null)
            statisticsUI = FindObjectOfType<MainMenuStatisticsUI>();

        if (statisticsUI == null && autoCreateUIIfMissing)
            statisticsUI = gameObject.AddComponent<MainMenuStatisticsUI>();

        if (statisticsUI != null)
        {
            statisticsUI.manager = this;
            statisticsUI.targetCanvas = targetCanvas;
            statisticsUI.rootParent = rootParent;
            statisticsUI.Connect(this);
        }
    }

    private void EnsureSelectedChapter()
    {
        MainMenuStatisticsChapterDefinition selected = GetChapterByType(selectedChapter);

        if (selected != null && selected.IsVisible())
            return;

        List<MainMenuStatisticsChapterDefinition> visibleChapters = GetVisibleChapters();

        if (visibleChapters.Count <= 0)
            return;

        selectedChapter = visibleChapters[0].chapterType;
    }

    private void CreateDefaultChaptersIfNeeded()
    {
        EnsureChapterList();
        AddChapterIfMissing("total", MainMenuStatisticsChapterType.Total, "Gesamt", 0);
        AddChapterIfMissing("last_run", MainMenuStatisticsChapterType.LastRun, "Letztes Spiel", 10);
    }

    private RunStatisticsTracker GetRunStatisticsTracker()
    {
        if (gameManager != null)
            return gameManager.GetRunStatisticsTracker();

        return FindObjectOfType<RunStatisticsTracker>();
    }

    private void AddChapterIfMissing(string id, MainMenuStatisticsChapterType chapterType, string title, int sortOrder)
    {
        if (GetChapterByType(chapterType) != null)
            return;

        MainMenuStatisticsChapterDefinition chapter = new MainMenuStatisticsChapterDefinition();
        chapter.chapterId = id;
        chapter.chapterType = chapterType;
        chapter.title = title;
        chapter.sortOrder = sortOrder;
        chapter.visible = true;
        chapter.preparedForFutureContent = true;
        chapters.Add(chapter);
    }

    private int CompareChapters(MainMenuStatisticsChapterDefinition a, MainMenuStatisticsChapterDefinition b)
    {
        if (a == null && b == null)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private void EnsureChapterList()
    {
        if (chapters == null)
            chapters = new List<MainMenuStatisticsChapterDefinition>();
    }
}
