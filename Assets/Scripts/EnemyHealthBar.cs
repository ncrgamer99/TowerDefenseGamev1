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

    [Header("Billboard")]
    public bool faceCamera = true;
    public bool useCameraRotation = true;

    [Header("Sorting")]
    public int sortingOrder = 50;

    private RectTransform canvasRect;
    private RectTransform fillRect;
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
