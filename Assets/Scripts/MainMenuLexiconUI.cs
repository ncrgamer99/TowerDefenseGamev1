using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuLexiconUI : MonoBehaviour
{
    [Header("References")]
    public MainMenuLexiconManager manager;
    public Canvas targetCanvas;
    public Transform rootParent;

    [Header("Root UI")]
    public GameObject rootPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI selectedText;
    public RectTransform chapterGridContent;
    public Button closeButton;

    [Header("Chapter Content")]
    public TextMeshProUGUI contentTitleText;
    public GameObject enemyContentRoot;
    public RectTransform enemyTabListContent;
    public TextMeshProUGUI enemyDetailText;
    public GameObject towerContentRoot;
    public RectTransform towerTabListContent;
    public TextMeshProUGUI towerDetailText;
    public GameObject chaosJusticeContentRoot;
    public RectTransform chaosJusticeTabListContent;
    public TextMeshProUGUI chaosJusticeDetailText;
    public GameObject pathTileContentRoot;
    public RectTransform pathTileTabListContent;
    public TextMeshProUGUI pathTileDetailText;
    public GameObject placeholderContentRoot;
    public TextMeshProUGUI placeholderText;

    [Header("Auto Create")]
    public bool autoCreateUIIfMissing = true;

    [Header("Theme")]
    public Color overlayColor = new Color32(4, 7, 12, 232);
    public Color windowColor = new Color32(18, 22, 30, 250);
    public Color headerColor = new Color32(36, 52, 74, 255);
    public Color navigationPanelColor = new Color32(12, 16, 23, 220);
    public Color contentPanelColor = new Color32(20, 27, 38, 235);
    public Color detailPanelColor = new Color32(14, 20, 30, 235);
    public Color cardColor = new Color32(35, 45, 64, 255);
    public Color selectedCardColor = new Color32(58, 82, 114, 255);
    public Color closeButtonColor = new Color32(190, 68, 76, 255);
    public Color textPrimaryColor = new Color32(240, 244, 250, 255);
    public Color textSecondaryColor = new Color32(184, 194, 208, 255);
    public Color enemyAccentColor = new Color32(220, 84, 80, 255);
    public Color towerAccentColor = new Color32(86, 164, 236, 255);
    public Color chaosJusticeAccentColor = new Color32(218, 168, 64, 255);
    public Color blockedBuildingAccentColor = new Color32(92, 184, 130, 255);
    public Color metaProgressionAccentColor = new Color32(168, 116, 226, 255);

    [Header("Text")]
    public float titleFontSize = 34f;
    public float subtitleFontSize = 17f;
    public float chapterTitleFontSize = 17f;
    public float chapterStateFontSize = 12.5f;
    public float contentTitleFontSize = 25f;
    public float enemyTabFontSize = 14.5f;
    public float enemyDetailFontSize = 17f;
    public float towerTabFontSize = 14.5f;
    public float towerDetailFontSize = 17f;
    public float chaosJusticeTabFontSize = 14.5f;
    public float chaosJusticeDetailFontSize = 17f;
    public float pathTileTabFontSize = 14.5f;
    public float pathTileDetailFontSize = 17f;

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private readonly List<Button> generatedChapterButtons = new List<Button>();
    private readonly List<Button> generatedEnemyButtons = new List<Button>();
    private readonly List<Button> generatedTowerButtons = new List<Button>();
    private readonly List<Button> generatedChaosJusticeButtons = new List<Button>();
    private readonly List<Button> generatedPathTileButtons = new List<Button>();

    private void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<MainMenuLexiconManager>();

        if (manager != null)
            manager.lexiconUI = this;

        EnsureUI();
        SetupButtons();

        if (manager == null || !manager.IsOpen)
            CloseLexicon();
    }

    public void Connect(MainMenuLexiconManager newManager)
    {
        manager = newManager;

        if (manager != null)
        {
            targetCanvas = manager.targetCanvas;
            rootParent = manager.rootParent;
        }

        EnsureUI();
        SetupButtons();
        RefreshAll();
    }

    public void OpenLexicon()
    {
        EnsureUI();

        if (rootPanel != null)
        {
            rootPanel.transform.SetAsLastSibling();
            rootPanel.SetActive(true);
        }

        RefreshAll();
    }

    public void CloseLexicon()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void RefreshAll()
    {
        if (rootPanel == null)
            return;

        if (titleText != null)
            titleText.text = "LEXIKON";

        if (subtitleText != null)
            subtitleText.text = "Kapitel und Spielwissen";

        RebuildChapterButtons();
        RefreshChapterContent();
        RefreshSelectedText();
    }

    private void SetupButtons()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() =>
        {
            if (manager != null)
                manager.CloseLexicon();
            else
                CloseLexicon();
        });

        ApplyButtonLabel(closeButton, "Zurück");
    }

    private void RebuildChapterButtons()
    {
        if (chapterGridContent == null || manager == null)
            return;

        for (int i = chapterGridContent.childCount - 1; i >= 0; i--)
            Destroy(chapterGridContent.GetChild(i).gameObject);

        generatedChapterButtons.Clear();
        List<MainMenuLexiconChapterDefinition> chapters = manager.GetVisibleChapters();

        for (int i = 0; i < chapters.Count; i++)
        {
            MainMenuLexiconChapterDefinition chapter = chapters[i];

            if (chapter != null)
                generatedChapterButtons.Add(CreateChapterButton(chapter, i + 1));
        }
    }

    private Button CreateChapterButton(MainMenuLexiconChapterDefinition chapter, int displayIndex)
    {
        bool selected = manager != null && chapter.chapterType == manager.selectedChapter;
        Color accentColor = GetAccentColor(chapter.chapterType);

        GameObject buttonObject = new GameObject("ChapterButton_" + chapter.chapterId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(chapterGridContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 76f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? selectedCardColor : cardColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        MainMenuLexiconChapterType capturedType = chapter.chapterType;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (manager != null)
                manager.SelectChapter(capturedType);
        });

        ApplySelectableColors(button, selected ? selectedCardColor : cardColor, accentColor);

        GameObject accent = CreatePanel(buttonObject.transform, "Accent", accentColor, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = new Vector2(7f, 0f);

        TextMeshProUGUI indexText = CreateText(buttonObject.transform, "IndexText", 13f, TextAlignmentOptions.TopRight, accentColor);
        indexText.text = displayIndex.ToString("00");
        indexText.fontStyle = FontStyles.Bold;
        SetOffsets(indexText.rectTransform, 18f, 10f, 14f, 42f);

        TextMeshProUGUI title = CreateText(buttonObject.transform, "TitleText", chapterTitleFontSize, TextAlignmentOptions.Left, textPrimaryColor);
        title.text = chapter.title;
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = true;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(title.rectTransform, 18f, 13f, 48f, 26f);

        TextMeshProUGUI state = CreateText(buttonObject.transform, "StateText", chapterStateFontSize, TextAlignmentOptions.BottomLeft, selected ? accentColor : textSecondaryColor);
        state.text = selected ? "Ausgewählt" : "Kapitel";
        state.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        state.enableWordWrapping = false;
        state.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(state.rectTransform, 18f, 48f, 18f, 9f);

        return button;
    }

    private void RefreshChapterContent()
    {
        if (manager == null)
            return;

        if (contentTitleText != null)
            contentTitleText.text = manager.GetSelectedChapterTitle();

        bool showEnemies = manager.selectedChapter == MainMenuLexiconChapterType.Enemies;
        bool showTowers = manager.selectedChapter == MainMenuLexiconChapterType.Towers;
        bool showChaosJustice = manager.selectedChapter == MainMenuLexiconChapterType.ChaosJustice;
        bool showPathTiles = manager.selectedChapter == MainMenuLexiconChapterType.BlockedBuilding;

        if (enemyContentRoot != null)
            enemyContentRoot.SetActive(showEnemies);

        if (towerContentRoot != null)
            towerContentRoot.SetActive(showTowers);

        if (chaosJusticeContentRoot != null)
            chaosJusticeContentRoot.SetActive(showChaosJustice);

        if (pathTileContentRoot != null)
            pathTileContentRoot.SetActive(showPathTiles);

        if (placeholderContentRoot != null)
            placeholderContentRoot.SetActive(!showEnemies && !showTowers && !showChaosJustice && !showPathTiles);

        if (showEnemies)
        {
            RebuildEnemyTabs();
            RefreshEnemyDetail();
            return;
        }

        if (showTowers)
        {
            RebuildTowerTabs();
            RefreshTowerDetail();
            return;
        }

        if (showChaosJustice)
        {
            RebuildChaosJusticeTabs();
            RefreshChaosJusticeDetail();
            return;
        }

        if (showPathTiles)
        {
            RebuildPathTileTabs();
            RefreshPathTileDetail();
            return;
        }

        if (placeholderText != null)
            placeholderText.text = "Dieses Kapitel ist vorbereitet.\nDie Inhalte folgen in einem späteren Schritt.";
    }

    private void RebuildEnemyTabs()
    {
        if (enemyTabListContent == null || manager == null)
            return;

        for (int i = enemyTabListContent.childCount - 1; i >= 0; i--)
            Destroy(enemyTabListContent.GetChild(i).gameObject);

        generatedEnemyButtons.Clear();
        List<MainMenuLexiconEnemyEntry> entries = manager.GetVisibleEnemyEntries();

        foreach (MainMenuLexiconEnemyEntry entry in entries)
        {
            if (entry != null)
                generatedEnemyButtons.Add(CreateEnemyTabButton(entry));
        }
    }

    private Button CreateEnemyTabButton(MainMenuLexiconEnemyEntry entry)
    {
        bool selected = manager != null && entry.entryId == manager.selectedEnemyEntryId;
        Color accentColor = enemyAccentColor;

        GameObject buttonObject = new GameObject("EnemyTab_" + entry.entryId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(enemyTabListContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 36f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? selectedCardColor : cardColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string capturedEntryId = entry.entryId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (manager != null)
                manager.SelectEnemyEntry(capturedEntryId);
        });

        ApplySelectableColors(button, selected ? selectedCardColor : cardColor, accentColor);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", enemyTabFontSize, TextAlignmentOptions.MidlineLeft, selected ? Color.white : textPrimaryColor);
        text.text = entry.title;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(text.rectTransform, 12f, 2f, 12f, 2f);
        return button;
    }

    private void RefreshEnemyDetail()
    {
        if (enemyDetailText == null || manager == null)
            return;

        MainMenuLexiconEnemyEntry entry = manager.GetSelectedEnemyEntry();
        enemyDetailText.text = manager.GetEnemyDetailText(entry);
    }

    private void RebuildTowerTabs()
    {
        if (towerTabListContent == null || manager == null)
            return;

        for (int i = towerTabListContent.childCount - 1; i >= 0; i--)
            Destroy(towerTabListContent.GetChild(i).gameObject);

        generatedTowerButtons.Clear();
        List<MainMenuLexiconTowerEntry> entries = manager.GetVisibleTowerEntries();

        foreach (MainMenuLexiconTowerEntry entry in entries)
        {
            if (entry != null)
                generatedTowerButtons.Add(CreateTowerTabButton(entry));
        }
    }

    private Button CreateTowerTabButton(MainMenuLexiconTowerEntry entry)
    {
        bool selected = manager != null && entry.entryId == manager.selectedTowerEntryId;
        Color accentColor = towerAccentColor;

        GameObject buttonObject = new GameObject("TowerTab_" + entry.entryId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(towerTabListContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 36f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? selectedCardColor : cardColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string capturedEntryId = entry.entryId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (manager != null)
                manager.SelectTowerEntry(capturedEntryId);
        });

        ApplySelectableColors(button, selected ? selectedCardColor : cardColor, accentColor);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", towerTabFontSize, TextAlignmentOptions.MidlineLeft, selected ? Color.white : textPrimaryColor);
        text.text = entry.title;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(text.rectTransform, 12f, 2f, 12f, 2f);
        return button;
    }

    private void RefreshTowerDetail()
    {
        if (towerDetailText == null || manager == null)
            return;

        MainMenuLexiconTowerEntry entry = manager.GetSelectedTowerEntry();
        towerDetailText.text = manager.GetTowerDetailText(entry);
    }

    private void RebuildChaosJusticeTabs()
    {
        if (chaosJusticeTabListContent == null || manager == null)
            return;

        for (int i = chaosJusticeTabListContent.childCount - 1; i >= 0; i--)
            Destroy(chaosJusticeTabListContent.GetChild(i).gameObject);

        generatedChaosJusticeButtons.Clear();
        List<MainMenuLexiconChaosJusticeEntry> entries = manager.GetVisibleChaosJusticeEntries();

        foreach (MainMenuLexiconChaosJusticeEntry entry in entries)
        {
            if (entry != null)
                generatedChaosJusticeButtons.Add(CreateChaosJusticeTabButton(entry));
        }
    }

    private Button CreateChaosJusticeTabButton(MainMenuLexiconChaosJusticeEntry entry)
    {
        bool selected = manager != null && entry.entryId == manager.selectedChaosJusticeEntryId;
        Color accentColor = chaosJusticeAccentColor;

        GameObject buttonObject = new GameObject("ChaosJusticeTab_" + entry.entryId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(chaosJusticeTabListContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 38f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? selectedCardColor : cardColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string capturedEntryId = entry.entryId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (manager != null)
                manager.SelectChaosJusticeEntry(capturedEntryId);
        });

        ApplySelectableColors(button, selected ? selectedCardColor : cardColor, accentColor);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", chaosJusticeTabFontSize, TextAlignmentOptions.MidlineLeft, selected ? Color.white : textPrimaryColor);
        text.text = entry.title;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(text.rectTransform, 12f, 2f, 12f, 2f);
        return button;
    }

    private void RefreshChaosJusticeDetail()
    {
        if (chaosJusticeDetailText == null || manager == null)
            return;

        MainMenuLexiconChaosJusticeEntry entry = manager.GetSelectedChaosJusticeEntry();
        chaosJusticeDetailText.text = manager.GetChaosJusticeDetailText(entry);
    }

    private void RebuildPathTileTabs()
    {
        if (pathTileTabListContent == null || manager == null)
            return;

        for (int i = pathTileTabListContent.childCount - 1; i >= 0; i--)
            Destroy(pathTileTabListContent.GetChild(i).gameObject);

        generatedPathTileButtons.Clear();
        List<MainMenuLexiconPathTileEntry> entries = manager.GetVisiblePathTileEntries();

        foreach (MainMenuLexiconPathTileEntry entry in entries)
        {
            if (entry != null)
                generatedPathTileButtons.Add(CreatePathTileTabButton(entry));
        }
    }

    private Button CreatePathTileTabButton(MainMenuLexiconPathTileEntry entry)
    {
        bool selected = manager != null && entry.entryId == manager.selectedPathTileEntryId;
        Color accentColor = blockedBuildingAccentColor;

        GameObject buttonObject = new GameObject("PathTileTab_" + entry.entryId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(pathTileTabListContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 38f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? selectedCardColor : cardColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string capturedEntryId = entry.entryId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (manager != null)
                manager.SelectPathTileEntry(capturedEntryId);
        });

        ApplySelectableColors(button, selected ? selectedCardColor : cardColor, accentColor);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", pathTileTabFontSize, TextAlignmentOptions.MidlineLeft, selected ? Color.white : textPrimaryColor);
        text.text = entry.title;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(text.rectTransform, 12f, 2f, 12f, 2f);
        return button;
    }

    private void RefreshPathTileDetail()
    {
        if (pathTileDetailText == null || manager == null)
            return;

        MainMenuLexiconPathTileEntry entry = manager.GetSelectedPathTileEntry();
        pathTileDetailText.text = manager.GetPathTileDetailText(entry);
    }

    private void RefreshSelectedText()
    {
        if (selectedText == null || manager == null)
            return;

        if (manager.selectedChapter == MainMenuLexiconChapterType.Enemies)
        {
            MainMenuLexiconEnemyEntry enemy = manager.GetSelectedEnemyEntry();
            selectedText.text = enemy != null
                ? "Kapitel: Gegner | Reiter: " + enemy.title
                : "Kapitel: Gegner";
            return;
        }

        if (manager.selectedChapter == MainMenuLexiconChapterType.Towers)
        {
            MainMenuLexiconTowerEntry tower = manager.GetSelectedTowerEntry();
            selectedText.text = tower != null
                ? "Kapitel: Tower | Reiter: " + tower.title
                : "Kapitel: Tower";
            return;
        }

        if (manager.selectedChapter == MainMenuLexiconChapterType.ChaosJustice)
        {
            MainMenuLexiconChaosJusticeEntry entry = manager.GetSelectedChaosJusticeEntry();
            selectedText.text = entry != null
                ? "Kapitel: Chaos & Gerechtigkeit | Reiter: " + entry.title
                : "Kapitel: Chaos & Gerechtigkeit";
            return;
        }

        if (manager.selectedChapter == MainMenuLexiconChapterType.BlockedBuilding)
        {
            MainMenuLexiconPathTileEntry entry = manager.GetSelectedPathTileEntry();
            selectedText.text = entry != null
                ? "Kapitel: Wegbau | Reiter: " + entry.title
                : "Kapitel: Wegbau";
            return;
        }

        selectedText.text = "Ausgewählt: " + manager.GetSelectedChapterTitle() + " | Inhalte folgen im nächsten Schritt.";
    }

    private void EnsureUI()
    {
        if (!autoCreateUIIfMissing || rootPanel != null)
            return;

        ResolveCanvasAndParent();

        if (targetCanvas == null && rootParent == null)
        {
            Debug.LogWarning("MainMenuLexiconUI: Kein Canvas gefunden. Auto-UI konnte nicht erstellt werden.");
            return;
        }

        CreateAutoUI();
    }

    private void ResolveCanvasAndParent()
    {
        if (manager != null)
        {
            if (targetCanvas == null)
                targetCanvas = manager.targetCanvas;

            if (rootParent == null)
                rootParent = manager.rootParent;
        }

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();
    }

    private void CreateAutoUI()
    {
        Transform parent = rootParent != null ? rootParent : targetCanvas.transform;

        GameObject overlay = new GameObject("MainMenuLexiconOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        rootPanel = overlay;
        Stretch(overlay.GetComponent<RectTransform>());

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;

        GameObject window = CreatePanel(overlay.transform, "MainMenuLexiconWindow", windowColor, true);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(1160f, 720f);

        CreateHeader(window.transform);
        CreateChapterNavigation(window.transform);
        CreateContentArea(window.transform);
        CreateFooter(window.transform);
        SetupButtons();
    }

    private void CreateHeader(Transform parent)
    {
        GameObject header = CreatePanel(parent, "HeaderBar", headerColor, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(0f, -88f);
        headerRect.offsetMax = Vector2.zero;

        titleText = CreateText(header.transform, "TitleText", titleFontSize, TextAlignmentOptions.Left, textPrimaryColor);
        titleText.fontStyle = FontStyles.Bold;
        SetOffsets(titleText.rectTransform, 28f, 14f, 190f, 32f);

        subtitleText = CreateText(header.transform, "SubtitleText", subtitleFontSize, TextAlignmentOptions.Left, textSecondaryColor);
        SetOffsets(subtitleText.rectTransform, 30f, 54f, 190f, 12f);

        closeButton = CreateButton(header.transform, "CloseButton", closeButtonColor, new Vector2(-22f, -18f), new Vector2(148f, 52f));
    }

    private void CreateChapterNavigation(Transform parent)
    {
        GameObject navigationPanel = CreatePanel(parent, "ChapterNavigationPanel", navigationPanelColor, true);
        RectTransform navigationRect = navigationPanel.GetComponent<RectTransform>();
        navigationRect.anchorMin = new Vector2(0f, 0f);
        navigationRect.anchorMax = new Vector2(0f, 1f);
        navigationRect.pivot = new Vector2(0f, 0.5f);
        navigationRect.offsetMin = new Vector2(36f, 78f);
        navigationRect.offsetMax = new Vector2(296f, -112f);

        GameObject listObject = new GameObject("ChapterListContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listObject.transform.SetParent(navigationPanel.transform, false);
        chapterGridContent = listObject.GetComponent<RectTransform>();
        Stretch(chapterGridContent);

        VerticalLayoutGroup layout = listObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void CreateContentArea(Transform parent)
    {
        GameObject contentPanel = CreatePanel(parent, "ChapterContentPanel", contentPanelColor, true);
        RectTransform contentRect = contentPanel.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(316f, 78f);
        contentRect.offsetMax = new Vector2(-36f, -112f);

        contentTitleText = CreateText(contentPanel.transform, "ContentTitleText", contentTitleFontSize, TextAlignmentOptions.Left, textPrimaryColor);
        contentTitleText.fontStyle = FontStyles.Bold;
        SetOffsets(contentTitleText.rectTransform, 22f, 16f, 22f, 486f);

        enemyContentRoot = new GameObject("EnemyContentRoot", typeof(RectTransform));
        enemyContentRoot.transform.SetParent(contentPanel.transform, false);
        RectTransform enemyRootRect = enemyContentRoot.GetComponent<RectTransform>();
        enemyRootRect.anchorMin = Vector2.zero;
        enemyRootRect.anchorMax = Vector2.one;
        enemyRootRect.offsetMin = new Vector2(20f, 20f);
        enemyRootRect.offsetMax = new Vector2(-20f, -68f);

        CreateEnemyContent(enemyContentRoot.transform);

        towerContentRoot = new GameObject("TowerContentRoot", typeof(RectTransform));
        towerContentRoot.transform.SetParent(contentPanel.transform, false);
        RectTransform towerRootRect = towerContentRoot.GetComponent<RectTransform>();
        towerRootRect.anchorMin = Vector2.zero;
        towerRootRect.anchorMax = Vector2.one;
        towerRootRect.offsetMin = new Vector2(20f, 20f);
        towerRootRect.offsetMax = new Vector2(-20f, -68f);

        CreateTowerContent(towerContentRoot.transform);

        chaosJusticeContentRoot = new GameObject("ChaosJusticeContentRoot", typeof(RectTransform));
        chaosJusticeContentRoot.transform.SetParent(contentPanel.transform, false);
        RectTransform chaosJusticeRootRect = chaosJusticeContentRoot.GetComponent<RectTransform>();
        chaosJusticeRootRect.anchorMin = Vector2.zero;
        chaosJusticeRootRect.anchorMax = Vector2.one;
        chaosJusticeRootRect.offsetMin = new Vector2(20f, 20f);
        chaosJusticeRootRect.offsetMax = new Vector2(-20f, -68f);

        CreateChaosJusticeContent(chaosJusticeContentRoot.transform);

        pathTileContentRoot = new GameObject("PathTileContentRoot", typeof(RectTransform));
        pathTileContentRoot.transform.SetParent(contentPanel.transform, false);
        RectTransform pathTileRootRect = pathTileContentRoot.GetComponent<RectTransform>();
        pathTileRootRect.anchorMin = Vector2.zero;
        pathTileRootRect.anchorMax = Vector2.one;
        pathTileRootRect.offsetMin = new Vector2(20f, 20f);
        pathTileRootRect.offsetMax = new Vector2(-20f, -68f);

        CreatePathTileContent(pathTileContentRoot.transform);

        placeholderContentRoot = new GameObject("PlaceholderContentRoot", typeof(RectTransform));
        placeholderContentRoot.transform.SetParent(contentPanel.transform, false);
        RectTransform placeholderRect = placeholderContentRoot.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(22f, 22f);
        placeholderRect.offsetMax = new Vector2(-22f, -72f);

        placeholderText = CreateText(placeholderContentRoot.transform, "PlaceholderText", 18f, TextAlignmentOptions.Center, textSecondaryColor);
        placeholderText.fontStyle = FontStyles.Bold;
        Stretch(placeholderText.rectTransform);
    }

    private void CreateEnemyContent(Transform parent)
    {
        GameObject tabsPanel = CreatePanel(parent, "EnemyTabsPanel", detailPanelColor, true);
        RectTransform tabsRect = tabsPanel.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 0f);
        tabsRect.anchorMax = new Vector2(0f, 1f);
        tabsRect.pivot = new Vector2(0f, 0.5f);
        tabsRect.offsetMin = Vector2.zero;
        tabsRect.offsetMax = new Vector2(212f, 0f);

        ScrollRect scrollRect = tabsPanel.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(tabsPanel.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 8);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject tabListObject = new GameObject("EnemyTabListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tabListObject.transform.SetParent(viewport.transform, false);
        enemyTabListContent = tabListObject.GetComponent<RectTransform>();
        enemyTabListContent.anchorMin = new Vector2(0f, 1f);
        enemyTabListContent.anchorMax = new Vector2(1f, 1f);
        enemyTabListContent.pivot = new Vector2(0.5f, 1f);
        enemyTabListContent.offsetMin = Vector2.zero;
        enemyTabListContent.offsetMax = Vector2.zero;
        enemyTabListContent.sizeDelta = new Vector2(0f, 760f);

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = enemyTabListContent;

        VerticalLayoutGroup tabLayout = tabListObject.GetComponent<VerticalLayoutGroup>();
        tabLayout.padding = new RectOffset(8, 8, 8, 8);
        tabLayout.spacing = 5f;
        tabLayout.childAlignment = TextAnchor.UpperCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = false;

        ContentSizeFitter tabFitter = tabListObject.GetComponent<ContentSizeFitter>();
        tabFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        tabFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject detailPanel = CreatePanel(parent, "EnemyDetailPanel", detailPanelColor, true);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(230f, 0f);
        detailRect.offsetMax = Vector2.zero;

        enemyDetailText = CreateText(detailPanel.transform, "EnemyDetailText", enemyDetailFontSize, TextAlignmentOptions.TopLeft, textPrimaryColor);
        enemyDetailText.enableWordWrapping = true;
        enemyDetailText.overflowMode = TextOverflowModes.Overflow;
        SetOffsets(enemyDetailText.rectTransform, 18f, 16f, 18f, 16f);
    }

    private void CreateTowerContent(Transform parent)
    {
        GameObject tabsPanel = CreatePanel(parent, "TowerTabsPanel", detailPanelColor, true);
        RectTransform tabsRect = tabsPanel.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 0f);
        tabsRect.anchorMax = new Vector2(0f, 1f);
        tabsRect.pivot = new Vector2(0f, 0.5f);
        tabsRect.offsetMin = Vector2.zero;
        tabsRect.offsetMax = new Vector2(212f, 0f);

        ScrollRect scrollRect = tabsPanel.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(tabsPanel.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 8);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject tabListObject = new GameObject("TowerTabListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tabListObject.transform.SetParent(viewport.transform, false);
        towerTabListContent = tabListObject.GetComponent<RectTransform>();
        towerTabListContent.anchorMin = new Vector2(0f, 1f);
        towerTabListContent.anchorMax = new Vector2(1f, 1f);
        towerTabListContent.pivot = new Vector2(0.5f, 1f);
        towerTabListContent.offsetMin = Vector2.zero;
        towerTabListContent.offsetMax = Vector2.zero;
        towerTabListContent.sizeDelta = new Vector2(0f, 620f);

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = towerTabListContent;

        VerticalLayoutGroup tabLayout = tabListObject.GetComponent<VerticalLayoutGroup>();
        tabLayout.padding = new RectOffset(8, 8, 8, 8);
        tabLayout.spacing = 5f;
        tabLayout.childAlignment = TextAnchor.UpperCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = false;

        ContentSizeFitter tabFitter = tabListObject.GetComponent<ContentSizeFitter>();
        tabFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        tabFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject detailPanel = CreatePanel(parent, "TowerDetailPanel", detailPanelColor, true);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(230f, 0f);
        detailRect.offsetMax = Vector2.zero;

        towerDetailText = CreateText(detailPanel.transform, "TowerDetailText", towerDetailFontSize, TextAlignmentOptions.TopLeft, textPrimaryColor);
        towerDetailText.enableWordWrapping = true;
        towerDetailText.overflowMode = TextOverflowModes.Overflow;
        SetOffsets(towerDetailText.rectTransform, 18f, 16f, 18f, 16f);
    }

    private void CreateChaosJusticeContent(Transform parent)
    {
        GameObject tabsPanel = CreatePanel(parent, "ChaosJusticeTabsPanel", detailPanelColor, true);
        RectTransform tabsRect = tabsPanel.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 0f);
        tabsRect.anchorMax = new Vector2(0f, 1f);
        tabsRect.pivot = new Vector2(0f, 0.5f);
        tabsRect.offsetMin = Vector2.zero;
        tabsRect.offsetMax = new Vector2(244f, 0f);

        ScrollRect scrollRect = tabsPanel.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(tabsPanel.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 8);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject tabListObject = new GameObject("ChaosJusticeTabListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tabListObject.transform.SetParent(viewport.transform, false);
        chaosJusticeTabListContent = tabListObject.GetComponent<RectTransform>();
        chaosJusticeTabListContent.anchorMin = new Vector2(0f, 1f);
        chaosJusticeTabListContent.anchorMax = new Vector2(1f, 1f);
        chaosJusticeTabListContent.pivot = new Vector2(0.5f, 1f);
        chaosJusticeTabListContent.offsetMin = Vector2.zero;
        chaosJusticeTabListContent.offsetMax = Vector2.zero;
        chaosJusticeTabListContent.sizeDelta = new Vector2(0f, 980f);

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = chaosJusticeTabListContent;

        VerticalLayoutGroup tabLayout = tabListObject.GetComponent<VerticalLayoutGroup>();
        tabLayout.padding = new RectOffset(8, 8, 8, 8);
        tabLayout.spacing = 5f;
        tabLayout.childAlignment = TextAnchor.UpperCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = false;

        ContentSizeFitter tabFitter = tabListObject.GetComponent<ContentSizeFitter>();
        tabFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        tabFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject detailPanel = CreatePanel(parent, "ChaosJusticeDetailPanel", detailPanelColor, true);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(262f, 0f);
        detailRect.offsetMax = Vector2.zero;

        chaosJusticeDetailText = CreateText(detailPanel.transform, "ChaosJusticeDetailText", chaosJusticeDetailFontSize, TextAlignmentOptions.TopLeft, textPrimaryColor);
        chaosJusticeDetailText.enableWordWrapping = true;
        chaosJusticeDetailText.overflowMode = TextOverflowModes.Overflow;
        SetOffsets(chaosJusticeDetailText.rectTransform, 18f, 16f, 18f, 16f);
    }

    private void CreatePathTileContent(Transform parent)
    {
        GameObject tabsPanel = CreatePanel(parent, "PathTileTabsPanel", detailPanelColor, true);
        RectTransform tabsRect = tabsPanel.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 0f);
        tabsRect.anchorMax = new Vector2(0f, 1f);
        tabsRect.pivot = new Vector2(0f, 0.5f);
        tabsRect.offsetMin = Vector2.zero;
        tabsRect.offsetMax = new Vector2(220f, 0f);

        ScrollRect scrollRect = tabsPanel.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(tabsPanel.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 8);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject tabListObject = new GameObject("PathTileTabListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tabListObject.transform.SetParent(viewport.transform, false);
        pathTileTabListContent = tabListObject.GetComponent<RectTransform>();
        pathTileTabListContent.anchorMin = new Vector2(0f, 1f);
        pathTileTabListContent.anchorMax = new Vector2(1f, 1f);
        pathTileTabListContent.pivot = new Vector2(0.5f, 1f);
        pathTileTabListContent.offsetMin = Vector2.zero;
        pathTileTabListContent.offsetMax = Vector2.zero;
        pathTileTabListContent.sizeDelta = new Vector2(0f, 520f);

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = pathTileTabListContent;

        VerticalLayoutGroup tabLayout = tabListObject.GetComponent<VerticalLayoutGroup>();
        tabLayout.padding = new RectOffset(8, 8, 8, 8);
        tabLayout.spacing = 5f;
        tabLayout.childAlignment = TextAnchor.UpperCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = false;

        ContentSizeFitter tabFitter = tabListObject.GetComponent<ContentSizeFitter>();
        tabFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        tabFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject detailPanel = CreatePanel(parent, "PathTileDetailPanel", detailPanelColor, true);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(238f, 0f);
        detailRect.offsetMax = Vector2.zero;

        pathTileDetailText = CreateText(detailPanel.transform, "PathTileDetailText", pathTileDetailFontSize, TextAlignmentOptions.TopLeft, textPrimaryColor);
        pathTileDetailText.enableWordWrapping = true;
        pathTileDetailText.overflowMode = TextOverflowModes.Overflow;
        SetOffsets(pathTileDetailText.rectTransform, 18f, 16f, 18f, 16f);
    }

    private void CreateFooter(Transform parent)
    {
        selectedText = CreateText(parent, "SelectedChapterText", 16f, TextAlignmentOptions.Center, textSecondaryColor);
        selectedText.enableWordWrapping = false;
        selectedText.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform selectedRect = selectedText.rectTransform;
        selectedRect.anchorMin = new Vector2(0f, 0f);
        selectedRect.anchorMax = new Vector2(1f, 0f);
        selectedRect.pivot = new Vector2(0.5f, 0f);
        selectedRect.offsetMin = new Vector2(36f, 30f);
        selectedRect.offsetMax = new Vector2(-36f, 68f);
    }

    private Button CreateButton(Transform parent, string objectName, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        return buttonObject.GetComponent<Button>();
    }

    private void ApplyButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (text == null)
        {
            text = CreateText(button.transform, "Text", 18f, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);
        }

        text.text = label;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void ApplySelectableColors(Button button, Color baseColor, Color accentColor)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, accentColor, 0.28f);
        colors.pressedColor = Color.Lerp(baseColor, accentColor, 0.45f);
        colors.selectedColor = baseColor;
        colors.disabledColor = new Color32(38, 42, 50, 255);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private Color GetAccentColor(MainMenuLexiconChapterType chapterType)
    {
        switch (chapterType)
        {
            case MainMenuLexiconChapterType.Enemies:
                return enemyAccentColor;
            case MainMenuLexiconChapterType.Towers:
                return towerAccentColor;
            case MainMenuLexiconChapterType.ChaosJustice:
                return chaosJusticeAccentColor;
            case MainMenuLexiconChapterType.BlockedBuilding:
                return blockedBuildingAccentColor;
            case MainMenuLexiconChapterType.MetaProgression:
                return metaProgressionAccentColor;
            default:
                return textSecondaryColor;
        }
    }

    private GameObject CreatePanel(Transform parent, string objectName, Color color, bool raycastTarget)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.richText = true;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetOffsets(RectTransform rect, float left, float top, float right, float bottom)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
