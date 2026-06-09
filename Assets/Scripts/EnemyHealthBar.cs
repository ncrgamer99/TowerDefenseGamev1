using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    public Enemy enemy;
    public Camera targetCamera;

    [Header("Auto Created UI")]
    public Canvas canvas;
    public Image backgroundImage;
    public Image fillImage;
    public TextMeshProUGUI healthText;

    [Header("Layout")]
    public Vector3 localOffset = new Vector3(0f, 1.25f, 0f);
    public Vector2 canvasSize = new Vector2(120f, 30f);
    public float canvasScale = 0.012f;
    public float horizontalMargin = 6f;
    public float fillHeight = 16f;

    [Header("Status Icons")]
    public bool showStatusIcons = true;
    public Vector2 statusIconSize = new Vector2(18f, 18f);
    public float statusIconSpacing = 3f;
    public float statusIconYOffset = 8f;
    public Color burnIconColor = new Color32(255, 94, 55, 235);
    public Color slowIconColor = new Color32(90, 190, 255, 235);
    public Color poisonIconColor = new Color32(165, 82, 235, 235);
    public Color weakpointIconColor = new Color32(110, 150, 255, 235);
    public Color bleedIconColor = new Color32(210, 60, 60, 235);
    public Color darknessIconColor = new Color32(18, 18, 24, 245);
    public Color statusIconTextColor = Color.white;

    [Header("Text")]
    public bool showHealthText = true;
    public int fontSize = 16;
    public Color textColor = Color.white;
    public Color textOutlineColor = Color.black;
    public float textOutlineWidth = 0.18f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color normalHealthColor = new Color32(70, 220, 95, 255);
    public Color burnHealthColor = new Color32(255, 80, 65, 255);
    public Color poisonHealthColor = new Color32(175, 75, 255, 255);
    public Color darknessHealthColor = Color.black;

    [Header("Billboard")]
    public bool faceCamera = true;
    public bool useCameraRotation = true;

    [Header("Sorting")]
    public int sortingOrder = 50;

    private RectTransform canvasRect;
    private RectTransform fillRect;
    private RectTransform statusIconRoot;
    private Image burnIconImage;
    private Image slowIconImage;
    private Image poisonIconImage;
    private Image weakpointIconImage;
    private Image bleedIconImage;
    private Image darknessIconImage;
    private TextMeshProUGUI burnIconText;
    private TextMeshProUGUI slowIconText;
    private TextMeshProUGUI poisonIconText;
    private TextMeshProUGUI weakpointIconText;
    private TextMeshProUGUI bleedIconText;
    private TextMeshProUGUI darknessIconText;
    private bool isInitialized = false;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (enemy != null)
            Initialize(enemy);
    }

    private void LateUpdate()
    {
        if (enemy == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!isInitialized)
            Initialize(enemy);

        transform.localPosition = localOffset;

        if (faceCamera)
            FaceCamera();

        Refresh();
    }

    public void Initialize(Enemy owner)
    {
        enemy = owner;

        if (enemy == null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        CreateVisualsIfNeeded();
        Refresh();
        isInitialized = true;
    }

    public void Refresh()
    {
        if (enemy == null)
            return;

        CreateVisualsIfNeeded();
        SetHealth(enemy.currentHealth, enemy.maxHealth);
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        float safeMax = Mathf.Max(1f, maxHealth);
        float safeCurrent = Mathf.Clamp(currentHealth, 0f, safeMax);
        float percent = Mathf.Clamp01(safeCurrent / safeMax);

        if (fillRect != null)
        {
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(percent, 0.5f);
            fillRect.offsetMin = new Vector2(horizontalMargin, -fillHeight * 0.5f);
            fillRect.offsetMax = new Vector2(-horizontalMargin, fillHeight * 0.5f);
        }

        if (fillImage != null)
            fillImage.color = GetCurrentHealthColor();

        if (healthText != null)
        {
            healthText.gameObject.SetActive(showHealthText);

            if (showHealthText)
            {
                int current = Mathf.CeilToInt(safeCurrent);
                int max = Mathf.CeilToInt(safeMax);
                healthText.text = current + "/" + max;
            }
        }

        RefreshStatusIcons();
    }

    private void CreateVisualsIfNeeded()
    {
        if (canvas != null && canvasRect != null && backgroundImage != null && fillImage != null && healthText != null)
            return;

        canvas = GetComponentInChildren<Canvas>(true);

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("HealthBarCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = sortingOrder;
        canvas.worldCamera = targetCamera;

        canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale = Vector3.one * canvasScale;
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.dynamicPixelsPerUnit = 20f;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;

        EnsureBackground();
        EnsureFill();
        EnsureText();
        EnsureStatusIcons();
    }

    private void EnsureBackground()
    {
        Transform existing = canvas.transform.Find("Background");
        GameObject backgroundObject = existing != null ? existing.gameObject : new GameObject("Background");
        backgroundObject.transform.SetParent(canvas.transform, false);

        backgroundImage = backgroundObject.GetComponent<Image>();
        if (backgroundImage == null)
            backgroundImage = backgroundObject.AddComponent<Image>();

        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = false;

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureFill()
    {
        Transform existing = canvas.transform.Find("Fill");
        GameObject fillObject = existing != null ? existing.gameObject : new GameObject("Fill");
        fillObject.transform.SetParent(canvas.transform, false);

        fillImage = fillObject.GetComponent<Image>();
        if (fillImage == null)
            fillImage = fillObject.AddComponent<Image>();

        fillImage.raycastTarget = false;
        fillImage.color = normalHealthColor;

        fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(horizontalMargin, -fillHeight * 0.5f);
        fillRect.offsetMax = new Vector2(-horizontalMargin, fillHeight * 0.5f);
    }

    private void EnsureText()
    {
        Transform existing = canvas.transform.Find("HealthText");
        GameObject textObject = existing != null ? existing.gameObject : new GameObject("HealthText");
        textObject.transform.SetParent(canvas.transform, false);

        healthText = textObject.GetComponent<TextMeshProUGUI>();
        if (healthText == null)
            healthText = textObject.AddComponent<TextMeshProUGUI>();

        healthText.raycastTarget = false;
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.fontSize = fontSize;
        healthText.color = textColor;
        healthText.outlineColor = textOutlineColor;
        healthText.outlineWidth = textOutlineWidth;
        healthText.enableWordWrapping = false;
        healthText.text = "";

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureStatusIcons()
    {
        Transform existingRoot = canvas.transform.Find("StatusIcons");
        GameObject rootObject = existingRoot != null ? existingRoot.gameObject : new GameObject("StatusIcons");
        rootObject.transform.SetParent(canvas.transform, false);

        statusIconRoot = rootObject.GetComponent<RectTransform>();
        if (statusIconRoot == null)
            statusIconRoot = rootObject.AddComponent<RectTransform>();

        statusIconRoot.anchorMin = new Vector2(0.5f, 1f);
        statusIconRoot.anchorMax = new Vector2(0.5f, 1f);
        statusIconRoot.pivot = new Vector2(0.5f, 0f);
        statusIconRoot.anchoredPosition = new Vector2(0f, statusIconYOffset);
        statusIconRoot.sizeDelta = new Vector2(6f * statusIconSize.x + 5f * statusIconSpacing, statusIconSize.y);

        burnIconImage = EnsureStatusIcon(statusIconRoot, "BurnIcon", burnIconColor, out burnIconText);
        slowIconImage = EnsureStatusIcon(statusIconRoot, "SlowIcon", slowIconColor, out slowIconText);
        poisonIconImage = EnsureStatusIcon(statusIconRoot, "PoisonIcon", poisonIconColor, out poisonIconText);
        weakpointIconImage = EnsureStatusIcon(statusIconRoot, "WeakpointIcon", weakpointIconColor, out weakpointIconText);
        bleedIconImage = EnsureStatusIcon(statusIconRoot, "BleedIcon", bleedIconColor, out bleedIconText);
        darknessIconImage = EnsureStatusIcon(statusIconRoot, "DarknessIcon", darknessIconColor, out darknessIconText);
    }

    private Image EnsureStatusIcon(RectTransform parent, string objectName, Color color, out TextMeshProUGUI label)
    {
        Transform existing = parent.Find(objectName);
        GameObject iconObject = existing != null ? existing.gameObject : new GameObject(objectName);
        iconObject.transform.SetParent(parent, false);

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        if (rect == null)
            rect = iconObject.AddComponent<RectTransform>();

        rect.sizeDelta = statusIconSize;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = iconObject.GetComponent<Image>();
        if (image == null)
            image = iconObject.AddComponent<Image>();

        image.color = color;
        image.raycastTarget = false;

        Transform existingLabel = iconObject.transform.Find("Label");
        GameObject labelObject = existingLabel != null ? existingLabel.gameObject : new GameObject("Label");
        labelObject.transform.SetParent(iconObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        if (labelRect == null)
            labelRect = labelObject.AddComponent<RectTransform>();

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        label = labelObject.GetComponent<TextMeshProUGUI>();
        if (label == null)
            label = labelObject.AddComponent<TextMeshProUGUI>();

        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 12f;
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;
        label.color = statusIconTextColor;

        return image;
    }

    private void RefreshStatusIcons()
    {
        if (statusIconRoot == null)
            EnsureStatusIcons();

        if (statusIconRoot == null || enemy == null)
            return;

        bool hasAnyIcon = false;
        int visibleCount = 0;

        if (enemy.HasBurn())
            visibleCount++;

        if (enemy.HasSlow())
            visibleCount++;

        if (enemy.HasPoison())
            visibleCount++;

        if (enemy.HasWeakpointArmorBreak())
            visibleCount++;

        if (enemy.HasBleed())
            visibleCount++;

        if (enemy.HasDarkness())
            visibleCount++;

        int visibleIndex = 0;

        SetStatusIcon(burnIconImage, burnIconText, enemy.HasBurn(), "F" + Mathf.Max(1, enemy.ActiveBurnStacks), visibleIndex, visibleCount, ref hasAnyIcon);
        if (enemy.HasBurn())
            visibleIndex++;

        SetStatusIcon(slowIconImage, slowIconText, enemy.HasSlow(), "S", visibleIndex, visibleCount, ref hasAnyIcon);
        if (enemy.HasSlow())
            visibleIndex++;

        SetStatusIcon(poisonIconImage, poisonIconText, enemy.HasPoison(), "P", visibleIndex, visibleCount, ref hasAnyIcon);
        if (enemy.HasPoison())
            visibleIndex++;

        SetStatusIcon(weakpointIconImage, weakpointIconText, enemy.HasWeakpointArmorBreak(), "W", visibleIndex, visibleCount, ref hasAnyIcon);
        if (enemy.HasWeakpointArmorBreak())
            visibleIndex++;

        SetStatusIcon(bleedIconImage, bleedIconText, enemy.HasBleed(), "B", visibleIndex, visibleCount, ref hasAnyIcon);
        if (enemy.HasBleed())
            visibleIndex++;

        SetStatusIcon(darknessIconImage, darknessIconText, enemy.HasDarkness(), "D", visibleIndex, visibleCount, ref hasAnyIcon);

        statusIconRoot.gameObject.SetActive(showStatusIcons && hasAnyIcon);
    }

    private void SetStatusIcon(Image image, TextMeshProUGUI label, bool visible, string text, int visibleIndex, int visibleCount, ref bool hasAnyIcon)
    {
        if (image == null)
            return;

        bool active = showStatusIcons && visible;
        image.gameObject.SetActive(active);

        if (!active)
            return;

        hasAnyIcon = true;

        RectTransform rect = image.GetComponent<RectTransform>();
        if (rect != null)
        {
            float step = statusIconSize.x + statusIconSpacing;
            float startX = -Mathf.Max(0, visibleCount - 1) * step * 0.5f;
            float x = startX + visibleIndex * step;
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        if (label != null)
            label.text = text;
    }

    private Color GetCurrentHealthColor()
    {
        if (enemy == null)
            return normalHealthColor;

        switch (enemy.GetHealthBarEffectMode())
        {
            case EnemyHealthBarEffectMode.Burn:
                return burnHealthColor;

            case EnemyHealthBarEffectMode.Poison:
                return poisonHealthColor;

            case EnemyHealthBarEffectMode.Bleed:
                return burnHealthColor;

            case EnemyHealthBarEffectMode.Darkness:
                return darknessHealthColor;

            case EnemyHealthBarEffectMode.None:
            default:
                return normalHealthColor;
        }
    }

    private void FaceCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam == null)
            return;

        if (useCameraRotation)
        {
            transform.rotation = cam.transform.rotation;
            return;
        }

        Vector3 directionToCamera = cam.transform.position - transform.position;

        if (directionToCamera.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
    }
}
