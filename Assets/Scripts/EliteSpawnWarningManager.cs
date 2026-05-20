using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EliteSpawnWarningManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;

    [Header("UI")]
    public bool autoCreateWarningUI = true;
    public GameObject warningRoot;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public Button okButton;
    public TextMeshProUGUI okButtonText;

    [Header("Text")]
    public string title = "ELITE ERSCHEINT";
    public string body = "Ein Elite-Gegner ist gespawnt.\nEr erscheint allein, hat hohe Widerstände und zerstört bei einem Durchbruch einen starken Tower.";
    public string okLabel = "OK";

    [Header("Visuals")]
    public Color overlayColor = new Color32(4, 6, 10, 190);
    public Color panelColor = new Color32(18, 22, 30, 250);
    public Color titleColor = new Color32(255, 235, 105, 255);
    public Color bodyColor = new Color32(235, 240, 248, 255);
    public Color okButtonColor = new Color32(80, 105, 145, 255);

    private bool warningOpen = false;
    private bool hasStoredTimeScale = false;
    private float storedTimeScale = 1f;

    private void Start()
    {
        ResolveReferences();
        EnsureWarningUI();
        SetupButton();
        CloseWarningAndRestoreTime();
    }

    private void Update()
    {
        if (!warningOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            ConfirmWarning();
    }

    public void Connect(GameManager manager)
    {
        gameManager = manager;
    }

    public bool IsWarningOpen()
    {
        return warningOpen;
    }

    public bool OpenEliteSpawnWarning(Enemy eliteEnemy)
    {
        if (warningOpen)
            return true;

        ResolveReferences();

        if (!EnsureWarningUI())
            return false;

        RefreshText();
        StoreAndPauseTime();
        warningOpen = true;

        if (warningRoot != null)
        {
            warningRoot.transform.SetAsLastSibling();
            warningRoot.SetActive(true);
        }

        return true;
    }

    public void ConfirmWarning()
    {
        if (!warningOpen)
            return;

        CloseWarningAndRestoreTime();
    }

    public void CloseWarningAndRestoreTime()
    {
        warningOpen = false;

        if (warningRoot != null)
            warningRoot.SetActive(false);

        RestoreTime();
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private bool EnsureWarningUI()
    {
        if (!autoCreateWarningUI && warningRoot == null)
            return false;

        EnsureEventSystem();

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("EliteSpawnWarningCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (warningRoot == null)
            CreateWarningUI(canvas.transform);

        RefreshText();
        SetupButton();
        return warningRoot != null && okButton != null;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void CreateWarningUI(Transform parent)
    {
        warningRoot = new GameObject("EliteSpawnWarningRoot", typeof(RectTransform), typeof(Image));
        warningRoot.transform.SetParent(parent, false);

        RectTransform rootRect = warningRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImage = warningRoot.GetComponent<Image>();
        rootImage.color = overlayColor;
        rootImage.raycastTarget = true;

        GameObject panel = new GameObject("EliteSpawnWarningPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(warningRoot.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(620f, 260f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = true;

        titleText = CreateText(panel.transform, "Title", title, 34f, FontStyles.Bold, titleColor);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(-48f, 54f);

        bodyText = CreateText(panel.transform, "Body", body, 20f, FontStyles.Normal, bodyColor);
        RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.5f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = new Vector2(0f, -32f);
        bodyRect.sizeDelta = new Vector2(-64f, 110f);

        okButton = CreateButton(panel.transform, "OkButton", okLabel);
        RectTransform buttonRect = okButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 24f);
        buttonRect.sizeDelta = new Vector2(180f, 52f);
    }

    private TextMeshProUGUI CreateText(Transform parent, string objectName, string text, float fontSize, FontStyles style, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(Transform parent, string objectName, string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = okButtonColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        okButtonText = CreateText(buttonObject.transform, objectName + "Text", label, 22f, FontStyles.Bold, Color.white);

        RectTransform textRect = okButtonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private void SetupButton()
    {
        if (okButton == null)
            return;

        okButton.onClick.RemoveAllListeners();
        okButton.onClick.AddListener(ConfirmWarning);

        Image image = okButton.GetComponent<Image>();
        if (image != null)
            image.color = okButtonColor;
    }

    private void RefreshText()
    {
        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = body;

        if (okButtonText != null)
            okButtonText.text = okLabel;
    }

    private void StoreAndPauseTime()
    {
        if (!hasStoredTimeScale)
        {
            storedTimeScale = Time.timeScale;
            hasStoredTimeScale = true;
        }

        Time.timeScale = 0f;
    }

    private void RestoreTime()
    {
        if (!hasStoredTimeScale)
            return;

        Time.timeScale = Mathf.Approximately(storedTimeScale, 0f) ? 1f : storedTimeScale;
        hasStoredTimeScale = false;
        storedTimeScale = 1f;
    }
}
