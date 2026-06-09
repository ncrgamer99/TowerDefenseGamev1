using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TowerSupportTileBuildUI : MonoBehaviour
{
    [Header("References")]
    public BuildManager buildManager;
    public BuildSelectionUI buildSelectionUI;
    public GameManager gameManager;
    public TileManager tileManager;
    public Camera mainCamera;

    [Header("Runtime UI")]
    public Canvas canvas;
    public GameObject panel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statusText;
    public Button buildButton;
    public Button closeButton;
    public Vector2 panelSize = new Vector2(300f, 168f);
    public Vector2 screenOffset = new Vector2(24f, 58f);

    private TowerSupportTileEffect currentTile;

    public static TowerSupportTileBuildUI EnsureExists(BuildManager owner)
    {
        TowerSupportTileBuildUI ui = FindObjectOfType<TowerSupportTileBuildUI>();

        if (ui == null)
        {
            GameObject uiObject = new GameObject("TowerSupportTileBuildUI");
            ui = uiObject.AddComponent<TowerSupportTileBuildUI>();
        }

        ui.buildManager = owner != null ? owner : ui.buildManager;
        ui.ResolveReferences();
        ui.EnsureUI();
        ui.Hide();
        return ui;
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureUI();
        Hide();
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            Hide();
    }

    public void Show(TowerSupportTileEffect tile)
    {
        if (tile == null || !tile.IsTowerSupportTile() || !tile.IsAvailableForBuild())
        {
            Hide();
            return;
        }

        ResolveReferences();
        EnsureUI();

        currentTile = tile;

        if (panel != null)
            panel.SetActive(true);

        Refresh();
        PositionNearTile(tile);
    }

    public void Refresh()
    {
        if (currentTile == null)
            return;

        bool available = currentTile.IsAvailableForBuild();
        if (!available)
        {
            Hide();
            return;
        }

        if (titleText != null)
            titleText.text = TowerSupportTileEffect.GetDisplayName(currentTile.tileType);

        if (descriptionText != null)
            descriptionText.text = TowerSupportTileEffect.GetEffectDescription(currentTile.tileType);

        if (statusText != null)
            statusText.text = "Frei";

        if (buildButton != null)
            buildButton.interactable = available && buildSelectionUI != null && tileManager != null && tileManager.IsBuildAllowed();
    }

    public void Hide()
    {
        currentTile = null;

        if (panel != null)
            panel.SetActive(false);
    }

    private void HandleBuildClicked()
    {
        if (currentTile == null)
            return;

        if (!currentTile.IsAvailableForBuild())
        {
            Hide();
            return;
        }

        ResolveReferences();

        if (buildSelectionUI == null)
        {
            Debug.LogWarning("TowerSupportTileBuildUI: BuildSelectionUI fehlt.");
            return;
        }

        buildSelectionUI.OpenForSupportTile(currentTile, this);
    }

    private void ResolveReferences()
    {
        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();

        if (buildManager != null)
        {
            if (gameManager == null)
                gameManager = buildManager.gameManager;

            if (tileManager == null)
                tileManager = buildManager.tileManager;

            if (buildSelectionUI == null)
                buildSelectionUI = buildManager.buildSelectionUI;

            if (mainCamera == null)
                mainCamera = buildManager.mainCamera;
        }

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (tileManager == null)
            tileManager = FindObjectOfType<TileManager>();

        if (buildSelectionUI == null)
            buildSelectionUI = FindObjectOfType<BuildSelectionUI>();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void EnsureUI()
    {
        if (panel != null)
            return;

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            canvas = CreateCanvas();

        GameObject panelObject = new GameObject("SupportTileBuildPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        panel = panelObject;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color32(22, 31, 43, 245);
        panelImage.raycastTarget = true;

        titleText = CreateText(panelObject.transform, "Title", new Vector2(14f, -14f), new Vector2(-52f, 28f), 18f, TextAlignmentOptions.Left, new Color32(238, 245, 250, 255));
        descriptionText = CreateText(panelObject.transform, "Description", new Vector2(14f, -48f), new Vector2(-28f, 56f), 13f, TextAlignmentOptions.TopLeft, new Color32(190, 201, 213, 255));
        statusText = CreateText(panelObject.transform, "Status", new Vector2(14f, -108f), new Vector2(-140f, 24f), 12f, TextAlignmentOptions.Left, new Color32(220, 196, 105, 255));

        buildButton = CreateButton(panelObject.transform, "BuildButton", "Build", new Vector2(176f, 28f), new Vector2(92f, 34f), new Color32(214, 164, 65, 255));
        closeButton = CreateButton(panelObject.transform, "CloseButton", "X", new Vector2(266f, -16f), new Vector2(26f, 26f), new Color32(95, 105, 118, 255));

        buildButton.onClick.AddListener(HandleBuildClicked);
        closeButton.onClick.AddListener(Hide);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("SupportTileUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        return createdCanvas;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, Vector2 topLeft, Vector2 sizeOffset, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = sizeOffset;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();

        TextMeshProUGUI labelText = CreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.zero, 13f, TextAlignmentOptions.Center, new Color32(35, 32, 24, 255));
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelText.enableWordWrapping = false;
        labelText.text = label;

        return button;
    }

    private void PositionNearTile(TowerSupportTileEffect tile)
    {
        if (tile == null || panel == null || canvas == null)
            return;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        RectTransform canvasRect = canvas.transform as RectTransform;

        if (panelRect == null || canvasRect == null)
            return;

        Camera worldCamera = mainCamera != null ? mainCamera : Camera.main;

        if (worldCamera == null)
            return;

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(tile.transform.position + Vector3.up * 0.9f);
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            return;

        localPoint += screenOffset;

        Vector2 halfCanvas = canvasRect.rect.size * 0.5f;
        Vector2 size = panelRect.sizeDelta;
        localPoint.x = Mathf.Clamp(localPoint.x, -halfCanvas.x + 12f, halfCanvas.x - size.x - 12f);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfCanvas.y + size.y * 0.5f + 12f, halfCanvas.y - size.y * 0.5f - 12f);

        panelRect.anchoredPosition = localPoint;
        panel.transform.SetAsLastSibling();
    }
}
