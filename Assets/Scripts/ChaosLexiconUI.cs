using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChaosLexiconUI : MonoBehaviour
{
    [Header("References")]
    public ChaosLexiconManager manager;

    [Header("Root UI")]
    public GameObject rootPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI detailText;
    public Button closeButton;
    public Button refreshButton;

    [Header("Entry List")]
    public RectTransform entryListContent;
    public bool rebuildEntryButtonsOnRefresh = true;

    [Header("Auto Create")]
    public bool autoCreateUIIfMissing = true;
    public Canvas targetCanvas;

    [Header("Theme")]
    public Color overlayColor = new Color32(0, 0, 0, 145);
    public Color windowColor = new Color32(20, 24, 31, 248);
    public Color headerColor = new Color32(90, 185, 226, 255);
    public Color listPanelColor = new Color32(16, 20, 28, 245);
    public Color detailPanelColor = new Color32(22, 39, 60, 255);
    public Color entryButtonColor = new Color32(35, 45, 64, 255);
    public Color selectedEntryButtonColor = new Color32(214, 164, 65, 255);
    public Color lockedEntryButtonColor = new Color32(55, 64, 80, 255);
    public Color closeButtonColor = new Color32(200, 75, 75, 255);
    public Color refreshButtonColor = new Color32(65, 125, 245, 255);
    public bool hideRefreshButton = true;
    public Vector2 closeButtonReadableSize = new Vector2(132f, 48f);
    public Color textPrimaryColor = new Color32(240, 244, 250, 255);
    public Color textSecondaryColor = new Color32(185, 194, 208, 255);

    [Header("Text")]
    public float titleFontSize = 27f;
    public float entryFontSize = 14.5f;
    public float detailFontSize = 16f;

    [Header("Runtime")]
    public bool isOpen = false;

    public bool IsOpen => isOpen;

    private readonly List<Button> generatedEntryButtons = new List<Button>();

    private void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<ChaosLexiconManager>();

        if (manager != null)
            manager.lexiconUI = this;

        if (autoCreateUIIfMissing && rootPanel == null)
            CreateAutoUI();

        SetupButtons();
        CloseLexicon();
    }

    public void Connect(ChaosLexiconManager newManager)
    {
        manager = newManager;

        if (autoCreateUIIfMissing && rootPanel == null)
            CreateAutoUI();

        SetupButtons();
        CloseLexicon();
    }

    public void OpenLexicon()
    {
        if (autoCreateUIIfMissing && rootPanel == null)
            CreateAutoUI();

        isOpen = true;

        if (rootPanel != null)
            rootPanel.SetActive(true);

        RefreshAll();
    }

    public void CloseLexicon()
    {
        isOpen = false;

        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void RefreshAll()
    {
        if (manager == null)
            return;

        if (titleText != null)
            titleText.text = "CHAOS / GERECHTIGKEIT - LEXIKON V1";

        if (rebuildEntryButtonsOnRefresh)
            RebuildEntryButtons();

        RefreshDetailText();
    }

    private void SetupButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                if (manager != null)
                    manager.CloseLexicon();
                else
                    CloseLexicon();
            });

            ApplyButtonLabel(closeButton, "Schließen");
            ApplyTopButtonReadableLayout(closeButton, closeButtonReadableSize, new Vector2(-18f, -12f));
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(() =>
            {
                if (manager != null)
                    manager.RefreshAndReopenCurrentEntry();
                else
                    RefreshAll();
            });

            ApplyButtonLabel(refreshButton, "Aktualisieren");
            refreshButton.gameObject.SetActive(!hideRefreshButton);
        }
    }

    private void ApplyTopButtonReadableLayout(Button button, Vector2 size, Vector2 anchoredPosition)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }

    private void RebuildEntryButtons()
    {
        if (entryListContent == null || manager == null)
            return;

        for (int i = entryListContent.childCount - 1; i >= 0; i--)
            Destroy(entryListContent.GetChild(i).gameObject);

        generatedEntryButtons.Clear();
        List<ChaosLexiconEntry> entries = manager.GetVisibleEntries();

        foreach (ChaosLexiconEntry entry in entries)
        {
            if (entry == null)
                continue;

            Button button = CreateEntryButton(entryListContent, entry);
            generatedEntryButtons.Add(button);
        }
    }

    private Button CreateEntryButton(RectTransform parent, ChaosLexiconEntry entry)
    {
        GameObject buttonObject = new GameObject("EntryButton_" + entry.entryId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 48f;
        layoutElement.flexibleWidth = 1f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = GetEntryButtonColor(entry);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string capturedId = entry.entryId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (manager != null)
                manager.SelectEntry(capturedId);
        });

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", entryFontSize, TextAlignmentOptions.Left);
        text.text = manager != null ? manager.GetEntryButtonLabel(entry) : entry.title;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = new Vector4(10f, 4f, 10f, 4f);
        Stretch(text.rectTransform);

        return button;
    }

    private Color GetEntryButtonColor(ChaosLexiconEntry entry)
    {
        if (manager != null && entry != null && entry.entryId == manager.selectedEntryId)
            return selectedEntryButtonColor;

        if (entry != null && !entry.IsUnlocked())
            return lockedEntryButtonColor;

        return entryButtonColor;
    }

    private void RefreshDetailText()
    {
        if (detailText == null || manager == null)
            return;

        ChaosLexiconEntry entry = manager.GetSelectedEntry();
        detailText.text = manager.GetEntryDetailText(entry);
    }

    private void ApplyButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (text == null)
        {
            text = CreateText(button.transform, "Text", 16f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
        }

        text.text = label;
        text.fontSize = label == "Schließen" ? 18f : 16f;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void CreateAutoUI()
    {
        ResolveCanvas();

        if (targetCanvas == null)
        {
            Debug.LogWarning("ChaosLexiconUI: Kein Canvas gefunden. Auto-UI konnte nicht erstellt werden.");
            return;
        }

        GameObject overlay = new GameObject("ChaosLexiconOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(targetCanvas.transform, false);
        rootPanel = overlay;
        Stretch(overlay.GetComponent<RectTransform>());

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;

        GameObject window = CreatePanel(overlay.transform, "ChaosLexiconWindow", windowColor, true);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(1120f, 740f);

        GameObject header = CreatePanel(window.transform, "HeaderBar", headerColor, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(0f, -72f);
        headerRect.offsetMax = Vector2.zero;

        titleText = CreateText(header.transform, "TitleText", titleFontSize, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;
        Stretch(titleText.rectTransform);

        closeButton = CreateTopButton(header.transform, "CloseButton", "Schließen", closeButtonColor, new Vector2(-18f, -12f), closeButtonReadableSize);
        refreshButton = CreateTopButton(header.transform, "RefreshButton", "Aktualisieren", refreshButtonColor, new Vector2(-162f, -12f), new Vector2(132f, 48f));

        GameObject listPanel = CreatePanel(window.transform, "EntryListPanel", listPanelColor, true);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0f, 1f);
        listRect.pivot = new Vector2(0f, 0.5f);
        listRect.offsetMin = new Vector2(26f, 26f);
        listRect.offsetMax = new Vector2(342f, -98f);

        entryListContent = CreateScrollContent(listPanel.transform, "EntryListScrollView", "EntryListContent");
        VerticalLayoutGroup entryLayout = entryListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        entryLayout.padding = new RectOffset(8, 8, 8, 8);
        entryLayout.spacing = 7f;
        entryLayout.childAlignment = TextAnchor.UpperLeft;
        entryLayout.childControlWidth = true;
        entryLayout.childControlHeight = true;
        entryLayout.childForceExpandWidth = true;
        entryLayout.childForceExpandHeight = false;
        ContentSizeFitter entryFitter = entryListContent.gameObject.AddComponent<ContentSizeFitter>();
        entryFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        entryFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject detailPanel = CreatePanel(window.transform, "DetailPanel", detailPanelColor, true);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.pivot = new Vector2(0.5f, 0.5f);
        detailRect.offsetMin = new Vector2(360f, 26f);
        detailRect.offsetMax = new Vector2(-26f, -98f);

        RectTransform detailContent = CreateScrollContent(detailPanel.transform, "DetailScrollView", "DetailContent");
        detailText = CreateText(detailContent, "DetailText", detailFontSize, TextAlignmentOptions.TopLeft);
        detailText.enableWordWrapping = true;
        detailText.overflowMode = TextOverflowModes.Overflow;
        detailText.margin = new Vector4(14f, 12f, 14f, 12f);
        LayoutElement detailLayout = detailText.gameObject.AddComponent<LayoutElement>();
        detailLayout.minHeight = 900f;
        detailLayout.flexibleWidth = 1f;
        Stretch(detailText.rectTransform);

        ContentSizeFitter detailFitter = detailContent.gameObject.AddComponent<ContentSizeFitter>();
        detailFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        detailFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        SetupButtons();
    }

    private void ResolveCanvas()
    {
        if (targetCanvas != null)
            return;

        if (manager != null && manager.targetCanvas != null)
        {
            targetCanvas = manager.targetCanvas;
            return;
        }

        targetCanvas = FindObjectOfType<Canvas>();
    }

    private RectTransform CreateScrollContent(Transform parent, string scrollName, string contentName)
    {
        GameObject scrollObject = new GameObject(scrollName, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        Stretch(scrollObject.GetComponent<RectTransform>());

        Image scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color32(0, 0, 0, 0);
        scrollImage.raycastTarget = true;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollObject.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 10);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new GameObject(contentName, typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(0f, 0f);
        contentRect.offsetMax = new Vector2(0f, 0f);
        contentRect.sizeDelta = new Vector2(0f, 900f);

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        return contentRect;
    }

    private Button CreateTopButton(Transform parent, string name, string label, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
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

        Button button = buttonObject.GetComponent<Button>();
        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", 18f, TextAlignmentOptions.Center);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private GameObject CreatePanel(Transform parent, string name, Color color, bool raycastTarget)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.richText = true;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = textPrimaryColor;
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
}
