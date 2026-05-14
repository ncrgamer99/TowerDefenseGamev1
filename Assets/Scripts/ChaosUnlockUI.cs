using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChaosUnlockUI : MonoBehaviour
{
    [Header("References")]
    public ChaosUnlockManager manager;
    public Canvas targetCanvas;

    [Header("UI")]
    public GameObject rootPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI summaryText;
    public TextMeshProUGUI detailText;
    public Transform entryListContent;
    public Button closeButton;
    public Button refreshButton;

    [Header("Notification")]
    public GameObject notificationPanel;
    public TextMeshProUGUI notificationText;
    public float notificationDuration = 4f;

    [Header("Auto Create")]
    public bool autoCreateUIIfMissing = true;

    [Header("Theme")]
    public Color overlayColor = new Color32(0, 0, 0, 135);
    public Color windowColor = new Color32(20, 24, 31, 248);
    public Color headerColor = new Color32(214, 164, 65, 255);
    public Color listPanelColor = new Color32(22, 39, 60, 245);
    public Color detailPanelColor = new Color32(20, 30, 44, 245);
    public Color buttonColor = new Color32(35, 45, 64, 255);
    public Color unlockedButtonColor = new Color32(65, 125, 245, 255);
    public Color lockedButtonColor = new Color32(55, 64, 80, 255);
    public Color closeButtonColor = new Color32(200, 75, 75, 255);
    public Color textColor = new Color32(240, 244, 250, 255);

    [Header("Text")]
    public float titleFontSize = 28f;
    public float summaryFontSize = 15f;
    public float detailFontSize = 16f;
    public float entryButtonFontSize = 15f;

    private readonly List<Button> generatedEntryButtons = new List<Button>();
    private string selectedUnlockId = "";
    private float notificationTimer = 0f;

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<ChaosUnlockManager>();

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (manager != null)
            manager.unlockUI = this;

        EnsureUI();
        SetupButtons();
        CloseUnlocks();
    }

    private void Update()
    {
        if (notificationTimer > 0f)
        {
            notificationTimer -= Time.deltaTime;

            if (notificationTimer <= 0f && notificationPanel != null)
                notificationPanel.SetActive(false);
        }
    }

    public void Connect(ChaosUnlockManager newManager)
    {
        manager = newManager;
        EnsureUI();
        SetupButtons();
        CloseUnlocks();
    }

    public void OpenUnlocks()
    {
        EnsureUI();

        if (rootPanel != null)
            rootPanel.SetActive(true);

        EnsureSelection();
        RefreshAll();
    }

    public void CloseUnlocks()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void RefreshAll()
    {
        EnsureUI();
        RefreshList();
        RefreshDetail();
    }

    public void ShowNotification(string message)
    {
        EnsureUI();

        if (notificationPanel == null || notificationText == null)
        {
            Debug.Log(message);
            return;
        }

        notificationText.text = message;
        notificationPanel.SetActive(true);
        notificationTimer = Mathf.Max(0.5f, notificationDuration);
    }

    private void SetupButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseFromButton);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(RefreshFromButton);
        }
    }

    private void CloseFromButton()
    {
        if (manager != null)
            manager.CloseUnlocks();
        else
            CloseUnlocks();
    }

    private void RefreshFromButton()
    {
        if (manager != null)
            manager.RefreshAndNotify();
        else
            RefreshAll();
    }

    private void EnsureSelection()
    {
        if (manager == null)
            return;

        ChaosUnlockEntry current = manager.GetUnlockById(selectedUnlockId);

        if (current != null && current.IsVisible(manager.showLockedTeasers))
            return;

        List<ChaosUnlockEntry> visible = manager.GetVisibleUnlocks();
        selectedUnlockId = visible.Count > 0 && visible[0] != null ? visible[0].unlockId : "";
    }

    private void RefreshList()
    {
        if (manager == null || entryListContent == null)
            return;

        ClearGeneratedEntryButtons();
        List<ChaosUnlockEntry> visible = manager.GetVisibleUnlocks();

        if (titleText != null)
            titleText.text = "FREISCHALTUNGEN";

        if (summaryText != null)
            summaryText.text = manager.GetUnlockSummaryText();

        foreach (ChaosUnlockEntry entry in visible)
        {
            if (entry != null)
                generatedEntryButtons.Add(CreateEntryButton(entry));
        }
    }

    private Button CreateEntryButton(ChaosUnlockEntry entry)
    {
        GameObject buttonObject = new GameObject("UnlockEntryButton_" + entry.unlockId, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(entryListContent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 46f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = entry.IsUnlocked() ? unlockedButtonColor : lockedButtonColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string entryId = entry.unlockId;
        button.onClick.AddListener(() => SelectUnlock(entryId));

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", entryButtonFontSize, TextAlignmentOptions.MidlineLeft, Color.white);
        SetOffsets(text.rectTransform, 10f, 3f, 10f, 3f);
        text.text = (entry.IsUnlocked() ? "✓ " : "? ") + manager.GetUnlockDisplayTitle(entry);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return button;
    }

    private void SelectUnlock(string unlockId)
    {
        selectedUnlockId = unlockId;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (manager == null || detailText == null)
            return;

        EnsureSelection();
        ChaosUnlockEntry entry = manager.GetUnlockById(selectedUnlockId);
        detailText.text = manager.GetUnlockDetailText(entry);
    }

    private void ClearGeneratedEntryButtons()
    {
        foreach (Button button in generatedEntryButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedEntryButtons.Clear();
    }

    private void EnsureUI()
    {
        if (!autoCreateUIIfMissing || rootPanel != null)
            return;

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (targetCanvas == null)
            return;

        CreateAutoUI();
    }

    private void CreateAutoUI()
    {
        GameObject overlay = new GameObject("ChaosUnlockOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(targetCanvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;
        rootPanel = overlay;

        GameObject window = CreatePanel(overlay.transform, "ChaosUnlockWindow", windowColor, new Vector2(980f, 680f));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;

        GameObject header = CreatePanel(window.transform, "HeaderBar", headerColor, Vector2.zero);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(0f, -74f);
        headerRect.offsetMax = new Vector2(0f, 0f);

        titleText = CreateText(header.transform, "TitleText", titleFontSize, TextAlignmentOptions.Center, Color.white);
        Stretch(titleText.rectTransform);

        closeButton = CreateSmallButton(header.transform, "CloseButton", "X", closeButtonColor, new Vector2(-16f, -16f));
        refreshButton = CreateSmallButton(header.transform, "RefreshButton", "↻", buttonColor, new Vector2(-64f, -16f));

        GameObject summaryPanel = CreatePanel(window.transform, "SummaryPanel", detailPanelColor, Vector2.zero);
        RectTransform summaryRect = summaryPanel.GetComponent<RectTransform>();
        summaryRect.anchorMin = new Vector2(0f, 1f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.pivot = new Vector2(0.5f, 1f);
        summaryRect.offsetMin = new Vector2(24f, -116f);
        summaryRect.offsetMax = new Vector2(-24f, -82f);

        summaryText = CreateText(summaryPanel.transform, "SummaryText", summaryFontSize, TextAlignmentOptions.MidlineLeft, textColor);
        SetOffsets(summaryText.rectTransform, 10f, 3f, 10f, 3f);

        GameObject listPanel = CreatePanel(window.transform, "EntryListPanel", listPanelColor, Vector2.zero);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0f, 1f);
        listRect.pivot = new Vector2(0f, 0.5f);
        listRect.offsetMin = new Vector2(24f, 24f);
        listRect.offsetMax = new Vector2(340f, -132f);

        GameObject detailPanel = CreatePanel(window.transform, "DetailPanel", detailPanelColor, Vector2.zero);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(360f, 24f);
        detailRect.offsetMax = new Vector2(-24f, -132f);

        entryListContent = CreateContentWithLayout(listPanel.transform, "EntryListContent");
        detailText = CreateText(detailPanel.transform, "DetailText", detailFontSize, TextAlignmentOptions.TopLeft, textColor);
        detailText.enableWordWrapping = true;
        detailText.overflowMode = TextOverflowModes.Overflow;
        SetOffsets(detailText.rectTransform, 14f, 14f, 14f, 14f);

        notificationPanel = CreatePanel(overlay.transform, "UnlockNotificationPanel", new Color32(214, 164, 65, 245), new Vector2(460f, 64f));
        RectTransform notifyRect = notificationPanel.GetComponent<RectTransform>();
        notifyRect.anchorMin = new Vector2(0.5f, 1f);
        notifyRect.anchorMax = new Vector2(0.5f, 1f);
        notifyRect.pivot = new Vector2(0.5f, 1f);
        notifyRect.anchoredPosition = new Vector2(0f, -18f);
        notificationText = CreateText(notificationPanel.transform, "NotificationText", 18f, TextAlignmentOptions.Center, Color.white);
        Stretch(notificationText.rectTransform);
        notificationPanel.SetActive(false);
    }

    private Transform CreateContentWithLayout(Transform parent, string objectName)
    {
        GameObject content = new GameObject(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(parent, false);
        Stretch(content.GetComponent<RectTransform>());

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content.transform;
    }

    private GameObject CreatePanel(Transform parent, string objectName, Color color, Vector2 size)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateSmallButton(Transform parent, string objectName, string label, Color color, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(42f, 42f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", 18f, TextAlignmentOptions.Center, Color.white);
        text.text = label;
        Stretch(text.rectTransform);
        return button;
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
