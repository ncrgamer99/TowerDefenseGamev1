using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSpeedManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;

    [Header("Speed Values")]
    public float normalSpeed = 1f;
    public float fastSpeed = 2f;
    public float mediumSpeed = 3f;
    public float fasterSpeed = 6f;

    [Header("Safety")]
    public bool applyFixedDeltaTimeScaling = true;
    public float maxAllowedTimeScale = 8f;

    [Header("Meta Unlocks")]
    public bool requireGeneralMetaUnlocks = true;

    [Header("Runtime UI")]
    public bool showRuntimeSpeedButton = true;
    public Vector2 runtimeButtonAnchoredPosition = new Vector2(0f, -4f);
    public Vector2 runtimeButtonSize = new Vector2(156f, 38f);
    public int runtimeButtonSortingOrder = 850;

    private int currentSpeedMode = 1;
    private float defaultFixedDeltaTime;
    private GameObject runtimeButtonRoot;
    private RectTransform runtimeButtonRect;
    private TextMeshProUGUI runtimeButtonLabel;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void Start()
    {
        SetNormalSpeed();
    }

    private void Update()
    {
        ResolveReferences();
        RefreshRuntimeButton();

        if (gameManager != null && gameManager.IsGameplayInputLockedByModalUI())
            return;

        if (Input.GetKeyDown(KeyCode.Alpha7))
            SetNormalSpeed();

        if (Input.GetKeyDown(KeyCode.Alpha8))
            SetFastSpeed();

        if (Input.GetKeyDown(KeyCode.Alpha9))
            SetMaxSpeed();
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void OnDisable()
    {
        ResetTimeScale();
    }

    private void OnDestroy()
    {
        ResetTimeScale();
    }

    public void SetNormalSpeed()
    {
        currentSpeedMode = 1;
        ApplySpeed(normalSpeed, "Normal");
    }

    public void SetFastSpeed()
    {
        if (!CanUseFastSpeed())
        {
            Debug.Log("Game Speed Fast ist noch gesperrt. Im Meta-Hub unter Komfort / QoL freischalten.");
            SetNormalSpeed();
            return;
        }

        currentSpeedMode = 2;
        ApplySpeed(fastSpeed, "Fast");
    }

    public void SetFasterSpeed()
    {
        if (!CanUseFasterSpeed())
        {
            Debug.Log("Game Speed Faster ist noch gesperrt. Im Meta-Hub unter Komfort / QoL freischalten.");
            SetNormalSpeed();
            return;
        }

        currentSpeedMode = 4;
        ApplySpeed(fasterSpeed, "Faster");
    }

    public void SetMediumSpeed()
    {
        if (!CanUseMediumSpeed())
        {
            Debug.Log("Game Speed 3x ist noch gesperrt. Im Meta-Hub unter Komfort / QoL freischalten.");
            SetNormalSpeed();
            return;
        }

        currentSpeedMode = 3;
        ApplySpeed(mediumSpeed, "Medium");
    }

    public void SetMaxSpeed()
    {
        if (CanUseFasterSpeed())
            SetFasterSpeed();
        else if (CanUseMediumSpeed())
            SetMediumSpeed();
        else if (CanUseFastSpeed())
            SetFastSpeed();
        else
            SetNormalSpeed();
    }

    public void CycleSpeed()
    {
        ResolveReferences();

        if (gameManager != null && gameManager.IsGameplayInputLockedByModalUI())
            return;

        if (CanUseFasterSpeed())
        {
            if (currentSpeedMode == 1)
                SetFastSpeed();
            else if (currentSpeedMode == 2)
            {
                if (CanUseMediumSpeed())
                    SetMediumSpeed();
                else
                    SetFasterSpeed();
            }
            else if (currentSpeedMode == 3)
                SetFasterSpeed();
            else
                SetNormalSpeed();

            return;
        }

        if (CanUseMediumSpeed())
        {
            if (currentSpeedMode == 1)
                SetFastSpeed();
            else if (currentSpeedMode == 2)
                SetMediumSpeed();
            else
                SetNormalSpeed();

            return;
        }

        if (CanUseFastSpeed())
        {
            if (currentSpeedMode == 1)
                SetFastSpeed();
            else
                SetNormalSpeed();

            return;
        }

        SetNormalSpeed();
    }

    private bool CanUseFastSpeed()
    {
        if (!requireGeneralMetaUnlocks)
            return true;

        GeneralMetaProgressionManager generalMeta = GeneralMetaProgressionManager.GetOrCreate(gameManager);
        return generalMeta != null && generalMeta.CanUseFastSpeed();
    }

    private bool CanUseFasterSpeed()
    {
        if (!requireGeneralMetaUnlocks)
            return true;

        GeneralMetaProgressionManager generalMeta = GeneralMetaProgressionManager.GetOrCreate(gameManager);
        return generalMeta != null && generalMeta.CanUseFasterSpeed();
    }

    private bool CanUseMediumSpeed()
    {
        if (!requireGeneralMetaUnlocks)
            return true;

        GeneralMetaProgressionManager generalMeta = GeneralMetaProgressionManager.GetOrCreate(gameManager);
        return generalMeta != null && generalMeta.CanUseMediumSpeed();
    }

    private void ApplySpeed(float targetSpeed, string label)
    {
        float safeMax = Mathf.Max(0.1f, maxAllowedTimeScale);
        float safeSpeed = Mathf.Clamp(targetSpeed, 0.1f, safeMax);

        Time.timeScale = safeSpeed;

        if (applyFixedDeltaTimeScaling)
        {
            Time.fixedDeltaTime = defaultFixedDeltaTime * safeSpeed;
        }

        Debug.Log("Game Speed: " + label + " | TimeScale: " + Time.timeScale);
        UpdateRuntimeButtonLabel();
    }

    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    public int GetCurrentSpeedMode()
    {
        return currentSpeedMode;
    }

    private void RefreshRuntimeButton()
    {
        if (!CanShowRuntimeButton())
        {
            if (runtimeButtonRoot != null)
                runtimeButtonRoot.SetActive(false);

            return;
        }

        EnsureRuntimeButton();

        if (runtimeButtonRoot != null && !runtimeButtonRoot.activeSelf)
            runtimeButtonRoot.SetActive(true);

        if (runtimeButtonRect != null)
            runtimeButtonRect.anchoredPosition = GetRuntimeButtonAnchoredPosition();

        UpdateRuntimeButtonLabel();
    }

    private bool CanShowRuntimeButton()
    {
        if (!showRuntimeSpeedButton)
            return false;

        if (gameManager == null || !gameManager.gameStarted || gameManager.startMenuOpen || gameManager.isGameOver)
            return false;

        if (gameManager.IsGameplayInputLockedByModalUI())
            return false;

        return CanUseFastSpeed() || CanUseMediumSpeed() || CanUseFasterSpeed();
    }

    private void EnsureRuntimeButton()
    {
        if (runtimeButtonRoot != null)
            return;

        GameObject canvasObject = new GameObject("GameSpeedRuntimeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        runtimeButtonRoot = canvasObject;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = runtimeButtonSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonObject = new GameObject("GameSpeedButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        runtimeButtonRect = buttonObject.GetComponent<RectTransform>();
        runtimeButtonRect.anchorMin = new Vector2(0.5f, 1f);
        runtimeButtonRect.anchorMax = new Vector2(0.5f, 1f);
        runtimeButtonRect.pivot = new Vector2(0.5f, 1f);
        runtimeButtonRect.anchoredPosition = GetRuntimeButtonAnchoredPosition();
        runtimeButtonRect.sizeDelta = runtimeButtonSize;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(18, 25, 30, 225);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(CycleSpeed);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(235, 190, 100, 255);
        colors.pressedColor = new Color32(170, 113, 35, 255);
        colors.selectedColor = new Color32(220, 170, 80, 255);
        colors.disabledColor = new Color32(90, 90, 90, 160);
        button.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        runtimeButtonLabel = labelObject.GetComponent<TextMeshProUGUI>();
        runtimeButtonLabel.alignment = TextAlignmentOptions.Center;
        runtimeButtonLabel.fontSize = 15f;
        runtimeButtonLabel.fontStyle = FontStyles.Bold;
        runtimeButtonLabel.color = new Color32(236, 225, 202, 255);
        runtimeButtonLabel.raycastTarget = false;
    }

    private Vector2 GetRuntimeButtonAnchoredPosition()
    {
        float topOffset = Mathf.Clamp(Mathf.Abs(runtimeButtonAnchoredPosition.y), 4f, 8f);
        return new Vector2(0f, -topOffset);
    }

    private void UpdateRuntimeButtonLabel()
    {
        if (runtimeButtonLabel == null)
            return;

        runtimeButtonLabel.text = "SPEED " + GetCurrentSpeedLabel();
    }

    private string GetCurrentSpeedLabel()
    {
        if (currentSpeedMode == 4 && CanUseFasterSpeed())
            return Mathf.RoundToInt(fasterSpeed) + "x";

        if (currentSpeedMode == 3 && CanUseMediumSpeed())
            return Mathf.RoundToInt(mediumSpeed) + "x";

        if (currentSpeedMode == 2 && CanUseFastSpeed())
            return Mathf.RoundToInt(fastSpeed) + "x";

        return Mathf.RoundToInt(normalSpeed) + "x";
    }
}
