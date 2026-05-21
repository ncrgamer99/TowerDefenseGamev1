using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuStatisticsUI : MonoBehaviour
{
    [Header("References")]
    public MainMenuStatisticsManager manager;
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
    public Color cardColor = new Color32(35, 45, 64, 255);
    public Color selectedCardColor = new Color32(58, 82, 114, 255);
    public Color closeButtonColor = new Color32(190, 68, 76, 255);
    public Color textPrimaryColor = new Color32(240, 244, 250, 255);
    public Color textSecondaryColor = new Color32(184, 194, 208, 255);
    public Color totalAccentColor = new Color32(86, 164, 236, 255);
    public Color lastRunAccentColor = new Color32(92, 184, 130, 255);

    [Header("Text")]
    public float titleFontSize = 34f;
    public float subtitleFontSize = 17f;
    public float chapterTitleFontSize = 17f;
    public float chapterStateFontSize = 12.5f;
    public float contentTitleFontSize = 25f;
    public float placeholderFontSize = 18f;

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private readonly List<Button> generatedChapterButtons = new List<Button>();

    private void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<MainMenuStatisticsManager>();

        if (manager != null)
            manager.statisticsUI = this;

        EnsureUI();
        SetupButtons();

        if (manager == null || !manager.IsOpen)
            CloseStatistics();
    }

    public void Connect(MainMenuStatisticsManager newManager)
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

    public void OpenStatistics()
    {
        EnsureUI();

        if (rootPanel != null)
        {
            rootPanel.transform.SetAsLastSibling();
            rootPanel.SetActive(true);
        }

        RefreshAll();
    }

    public void CloseStatistics()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void RefreshAll()
    {
        if (rootPanel == null)
            return;

        if (titleText != null)
            titleText.text = "STATISTIK";

        if (subtitleText != null)
            subtitleText.text = "Gesamt und letztes Spiel";

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
                manager.CloseStatistics();
            else
                CloseStatistics();
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
        List<MainMenuStatisticsChapterDefinition> chapters = manager.GetVisibleChapters();

        for (int i = 0; i < chapters.Count; i++)
        {
            MainMenuStatisticsChapterDefinition chapter = chapters[i];

            if (chapter != null)
                generatedChapterButtons.Add(CreateChapterButton(chapter, i + 1));
        }
    }

    private Button CreateChapterButton(MainMenuStatisticsChapterDefinition chapter, int displayIndex)
    {
        bool selected = manager != null && chapter.chapterType == manager.selectedChapter;
        Color accentColor = GetAccentColor(chapter.chapterType);

        GameObject buttonObject = new GameObject("StatisticsChapterButton_" + chapter.chapterId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(chapterGridContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 76f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? selectedCardColor : cardColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        MainMenuStatisticsChapterType capturedType = chapter.chapterType;
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

        if (placeholderContentRoot != null)
            placeholderContentRoot.SetActive(true);

        if (placeholderText != null)
        {
            bool showLastRunStats = manager.selectedChapter == MainMenuStatisticsChapterType.LastRun ||
                                    manager.selectedChapter == MainMenuStatisticsChapterType.LastWave;
            placeholderText.text = showLastRunStats ? manager.GetLastWaveDetailText() : manager.GetPreparedChapterText();
            placeholderText.alignment = showLastRunStats ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
            placeholderText.fontStyle = showLastRunStats ? FontStyles.Normal : FontStyles.Bold;
            placeholderText.color = showLastRunStats ? textPrimaryColor : textSecondaryColor;
        }
    }

    private void RefreshSelectedText()
    {
        if (selectedText == null || manager == null)
            return;

        selectedText.text = "Ausgewählt: " + manager.GetSelectedChapterTitle();
    }

    private void EnsureUI()
    {
        if (!autoCreateUIIfMissing || rootPanel != null)
            return;

        ResolveCanvasAndParent();

        if (targetCanvas == null && rootParent == null)
        {
            Debug.LogWarning("MainMenuStatisticsUI: Kein Canvas gefunden. Auto-UI konnte nicht erstellt werden.");
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

        GameObject overlay = new GameObject("MainMenuStatisticsOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        rootPanel = overlay;
        Stretch(overlay.GetComponent<RectTransform>());

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;

        GameObject window = CreatePanel(overlay.transform, "MainMenuStatisticsWindow", windowColor, true);
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
        GameObject navigationPanel = CreatePanel(parent, "StatisticsChapterNavigationPanel", navigationPanelColor, true);
        RectTransform navigationRect = navigationPanel.GetComponent<RectTransform>();
        navigationRect.anchorMin = new Vector2(0f, 0f);
        navigationRect.anchorMax = new Vector2(0f, 1f);
        navigationRect.pivot = new Vector2(0f, 0.5f);
        navigationRect.offsetMin = new Vector2(36f, 78f);
        navigationRect.offsetMax = new Vector2(296f, -112f);

        GameObject listObject = new GameObject("StatisticsChapterListContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
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
        GameObject contentPanel = CreatePanel(parent, "StatisticsContentPanel", contentPanelColor, true);
        RectTransform contentRect = contentPanel.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(316f, 78f);
        contentRect.offsetMax = new Vector2(-36f, -112f);

        contentTitleText = CreateText(contentPanel.transform, "ContentTitleText", contentTitleFontSize, TextAlignmentOptions.Left, textPrimaryColor);
        contentTitleText.fontStyle = FontStyles.Bold;
        SetOffsets(contentTitleText.rectTransform, 22f, 16f, 22f, 486f);

        placeholderContentRoot = new GameObject("PlaceholderContentRoot", typeof(RectTransform));
        placeholderContentRoot.transform.SetParent(contentPanel.transform, false);
        RectTransform placeholderRect = placeholderContentRoot.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(22f, 86f);
        placeholderRect.offsetMax = new Vector2(-22f, -72f);

        placeholderText = CreateText(placeholderContentRoot.transform, "PlaceholderText", placeholderFontSize, TextAlignmentOptions.Center, textSecondaryColor);
        placeholderText.fontStyle = FontStyles.Bold;
        Stretch(placeholderText.rectTransform);
    }

    private void CreateFooter(Transform parent)
    {
        selectedText = CreateText(parent, "SelectedStatisticsChapterText", 16f, TextAlignmentOptions.Center, textSecondaryColor);
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

    private Color GetAccentColor(MainMenuStatisticsChapterType chapterType)
    {
        switch (chapterType)
        {
            case MainMenuStatisticsChapterType.Total:
                return totalAccentColor;
            case MainMenuStatisticsChapterType.LastRun:
                return lastRunAccentColor;
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
