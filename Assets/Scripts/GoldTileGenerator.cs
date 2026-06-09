using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class GoldTileGenerator : MonoBehaviour
{
    public GameManager gameManager;
    public int goldPerSecond = 0;
    public int totalGoldProduced = 0;
    public int baseGoldGenerated = 0;
    public int generationTicks = 0;
    public float runGoldRewardBonus = 0.03f;
    public float towerKillGoldBonus = 0.10f;

    private const float PopupWidth = 320f;
    private const float PopupHeight = 124f;
    private const float PopupScreenPadding = 18f;
    private static Canvas popupCanvas;
    private static RectTransform popupPanel;
    private static TextMeshProUGUI popupText;
    private static GoldTileGenerator activePopupTarget;

    private void Awake()
    {
        EnsureClickCollider();
    }

    private void Update()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (activePopupTarget == this)
        {
            UpdatePopupText();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                HidePopup();
        }

    }

    private void OnMouseDown()
    {
        if (IsPointerOverUI())
            return;

        HidePopup();
    }

    private void OnDestroy()
    {
        if (activePopupTarget == this)
            HidePopup();
    }

    private void EnsureClickCollider()
    {
        if (GetComponent<Collider>() != null)
            return;

        BoxCollider clickCollider = gameObject.AddComponent<BoxCollider>();
        clickCollider.center = new Vector3(0f, 0.2f, 0f);
        clickCollider.size = new Vector3(1f, 0.35f, 1f);
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void ShowPopup()
    {
        EnsurePopupUI();
        activePopupTarget = this;
        PositionPopupAtMouse();
        UpdatePopupText();

        if (popupPanel != null)
            popupPanel.gameObject.SetActive(true);
    }

    private static void HidePopup()
    {
        activePopupTarget = null;

        if (popupPanel != null)
            popupPanel.gameObject.SetActive(false);
    }

    private static void EnsurePopupUI()
    {
        if (popupCanvas != null && popupPanel != null && popupText != null)
            return;

        GameObject canvasObject = new GameObject("GoldTileInfoCanvas");
        popupCanvas = canvasObject.AddComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.sortingOrder = 1200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("GoldTileInfoPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        popupPanel = panelObject.AddComponent<RectTransform>();
        popupPanel.sizeDelta = new Vector2(PopupWidth, PopupHeight);
        popupPanel.pivot = new Vector2(0f, 1f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color32(10, 14, 20, 240);

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color32(214, 164, 65, 255);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f);
        textRect.offsetMax = new Vector2(-16f, -12f);

        popupText = textObject.AddComponent<TextMeshProUGUI>();
        popupText.fontSize = 17f;
        popupText.color = new Color32(240, 244, 250, 255);
        popupText.alignment = TextAlignmentOptions.TopLeft;
        popupText.enableWordWrapping = true;

        popupPanel.gameObject.SetActive(false);
    }

    private static void PositionPopupAtMouse()
    {
        if (popupCanvas == null || popupPanel == null)
            return;

        RectTransform canvasRect = popupCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
            return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out localPoint);

        Rect rect = canvasRect.rect;
        float x = Mathf.Clamp(localPoint.x + 18f, rect.xMin + PopupScreenPadding, rect.xMax - PopupWidth - PopupScreenPadding);
        float y = Mathf.Clamp(localPoint.y - 18f, rect.yMin + PopupHeight + PopupScreenPadding, rect.yMax - PopupScreenPadding);
        popupPanel.anchoredPosition = new Vector2(x, y);
    }

    private void UpdatePopupText()
    {
        if (popupText == null)
            return;

        float shownRunBonus = gameManager != null ? gameManager.goldTileRewardBonusPerTile : runGoldRewardBonus;
        TowerSupportTileEffect supportTile = GetComponent<TowerSupportTileEffect>();
        float shownKillBonus = supportTile != null ? supportTile.goldKillMultiplierBonus : towerKillGoldBonus;

        popupText.text =
            "<color=#D6A441><b>Gold Tile</b></color>\n" +
            "Run-Bonus: <b>+" + Mathf.RoundToInt(Mathf.Max(0f, shownRunBonus) * 100f) + "% Gold-Rewards</b>\n" +
            "Tower auf Tile: +" + Mathf.RoundToInt(Mathf.Max(0f, shownKillBonus) * 100f) + "% Gold pro Kill\n" +
            "Passive Produktion: aus\n" +
            "<size=80%><color=#B9C2D0>Rechtsklick oder Esc schliesst.</color></size>";
    }
}
