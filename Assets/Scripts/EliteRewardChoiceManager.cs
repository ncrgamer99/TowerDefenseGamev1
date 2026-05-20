using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EliteRewardType
{
    PendingReward,
    EliteGoldReserve,
    EliteXPBoost,
    FocusTraining,
    StrengthenWeak,
    StrongestTowerUpgradePoint,
    EliteRepair,
    EliteLegacy,
    TileChoiceQuality,
    DoubleRisk
}

[System.Serializable]
public class EliteRewardOption
{
    public string displayName = "Elite-Belohnung";
    public string description = "Diese Elite-Belohnung wird später festgelegt.";
    public EliteRewardType rewardType = EliteRewardType.PendingReward;
}

public class EliteRewardChoiceManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public PathBuildManager pathBuildManagerStyleSource;

    [Header("UI - Mirrors PathBuildManager")]
    public GameObject rewardTopBar;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    public Button optionButton1;
    public Button optionButton2;
    public Button optionButton3;

    public TextMeshProUGUI optionText1;
    public TextMeshProUGUI optionText2;
    public TextMeshProUGUI optionText3;

    [Header("UI Layout Defaults")]
    public bool autoCreateChoiceUI = true;
    public bool applyChoiceUILayoutDefaults = true;
    public Vector2 rewardTopBarSize = new Vector2(0f, 88f);
    public float rewardTopBarTopOffset = 116f;
    public float rewardTopBarLeftInset = 360f;
    public float rewardTopBarRightInset = 220f;
    public bool hideTitleTextForChoiceBar = true;
    public bool hideUnusedTopBarTextLabels = true;
    public Color choiceBarColor = new Color32(18, 22, 30, 245);
    public Color choiceButtonColor = new Color32(80, 105, 145, 255);
    public Color choiceDescriptionColor = new Color32(255, 235, 130, 255);
    public float choiceButtonRowSpacing = 14f;
    public bool forceChoiceButtonRectLayout = true;
    public float choiceButtonTopPadding = 6f;
    public float choiceButtonHeight = 46f;
    public float choiceDescriptionHeight = 30f;
    public float choiceDescriptionBottomPadding = 4f;
    public float choiceDescriptionFontSize = 16f;

    [Header("Reward Pool V1")]
    public bool usePlaceholderRewardsUntilDesigned = false;
    public List<EliteRewardOption> possibleRewards = new List<EliteRewardOption>();

    [Header("Reward Values V1")]
    public int eliteGoldPerWave = 100;
    public int eliteXPBase = 20;
    public int focusTrainingXPBase = 50;
    public int focusTrainingXPPerWave = 2;
    public int strengthenWeakTargetLevel = 5;
    public int strengthenWeakLevelUps = 1;
    public int strongestTowerUpgradePoints = 1;
    public int eliteRepairLives = 10;
    public float eliteLegacyStatBonus = 0.03f;
    public int tileChoiceQualityBoosts = 1;

    private readonly EliteRewardOption[] currentOptions = new EliteRewardOption[3];
    private bool selectionOpen = false;
    private WaveCompletionResult currentWaveResult;
    private int currentSelectionPowerMultiplier = 1;
    private bool doubleRiskPendingForNextSelection = false;
    private bool doubleRiskOfferedInPreviousSelection = false;

    private void Start()
    {
        ResolveReferences();
        EnsureChoiceUI();
        ApplyChoiceUILayoutDefaults();
        CloseSelectionWithoutResume();
        SetupButtons();
    }

    private void Update()
    {
        if (!selectionOpen)
            return;

        if (gameManager != null && gameManager.isGameOver)
        {
            CloseSelectionWithoutResume();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ChooseOption(0);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            ChooseOption(1);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            ChooseOption(2);
    }

    public void Connect(GameManager manager)
    {
        gameManager = manager;

        if (pathBuildManagerStyleSource == null && gameManager != null)
            pathBuildManagerStyleSource = gameManager.pathBuildManager;
    }

    public bool OpenEliteRewardSelection(WaveCompletionResult result)
    {
        if (result == null || !result.isEliteWave || !result.eliteDefeated)
            return false;

        ResolveReferences();

        if (gameManager != null && gameManager.isGameOver)
            return false;

        if (!EnsureChoiceUI())
            return false;

        if (gameManager != null)
        {
            gameManager.ClosePathAndBuildSelectionsForModal();
            gameManager.CloseTowerUIForModal();
        }

        currentWaveResult = result;
        GenerateOptions();
        ApplyChoiceUILayoutDefaults();
        UpdateUI();
        selectionOpen = true;

        if (rewardTopBar != null)
        {
            rewardTopBar.transform.SetAsLastSibling();
            rewardTopBar.SetActive(true);
        }

        return true;
    }

    public bool IsSelectionOpen()
    {
        return selectionOpen;
    }

    public void CloseSelectionWithoutResume()
    {
        selectionOpen = false;

        if (rewardTopBar != null)
            rewardTopBar.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (pathBuildManagerStyleSource == null)
        {
            if (gameManager != null && gameManager.pathBuildManager != null)
                pathBuildManagerStyleSource = gameManager.pathBuildManager;
            else
                pathBuildManagerStyleSource = FindObjectOfType<PathBuildManager>();
        }
    }

    private bool EnsureChoiceUI()
    {
        if (!autoCreateChoiceUI && rewardTopBar == null)
            return false;

        EnsureEventSystem();

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("EliteRewardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (rewardTopBar == null)
        {
            rewardTopBar = new GameObject("EliteRewardTopBar", typeof(RectTransform), typeof(Image));
            rewardTopBar.transform.SetParent(canvas.transform, false);
        }

        if (titleText == null)
            titleText = CreateChoiceText(rewardTopBar.transform, "EliteRewardTitle", "ELITE BESIEGT", 20f, FontStyles.Bold);

        if (optionButton1 == null)
            optionButton1 = CreateChoiceButton(rewardTopBar.transform, "EliteRewardOption1", out optionText1);

        if (optionButton2 == null)
            optionButton2 = CreateChoiceButton(rewardTopBar.transform, "EliteRewardOption2", out optionText2);

        if (optionButton3 == null)
            optionButton3 = CreateChoiceButton(rewardTopBar.transform, "EliteRewardOption3", out optionText3);

        if (descriptionText == null)
            descriptionText = CreateChoiceText(rewardTopBar.transform, "EliteRewardDescription", "", choiceDescriptionFontSize, FontStyles.Normal);

        SetupButtons();
        return rewardTopBar != null && optionButton1 != null && optionButton2 != null && optionButton3 != null;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private Button CreateChoiceButton(Transform parent, string objectName, out TextMeshProUGUI label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = choiceButtonColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        label = CreateChoiceText(buttonObject.transform, objectName + "Text", "", 17f, FontStyles.Bold);

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private TextMeshProUGUI CreateChoiceText(Transform parent, string objectName, string text, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private void SetupButtons()
    {
        SetupButton(optionButton1, 0);
        SetupButton(optionButton2, 1);
        SetupButton(optionButton3, 2);
    }

    private void SetupButton(Button button, int index)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ChooseOption(index));
        SetupOptionHover(button, index);
    }

    private void SetupOptionHover(Button button, int optionIndex)
    {
        if (button == null)
            return;

        EliteRewardOptionHoverProxy hoverProxy = button.GetComponent<EliteRewardOptionHoverProxy>();

        if (hoverProxy == null)
            hoverProxy = button.gameObject.AddComponent<EliteRewardOptionHoverProxy>();

        hoverProxy.Initialize(this, optionIndex);
    }

    private void ApplyChoiceUILayoutDefaults()
    {
        if (!applyChoiceUILayoutDefaults)
            return;

        CopyPathBuildManagerLayoutDefaults();

        if (rewardTopBar != null)
        {
            RectTransform rect = rewardTopBar.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2((rewardTopBarLeftInset - rewardTopBarRightInset) * 0.5f, -rewardTopBarTopOffset);
                rect.sizeDelta = new Vector2(-(rewardTopBarLeftInset + rewardTopBarRightInset), rewardTopBarSize.y);
            }

            Image image = rewardTopBar.GetComponent<Image>();
            if (image != null)
                image.color = choiceBarColor;

            HideUnusedTopBarTextLabels();
        }

        if (titleText != null)
            titleText.gameObject.SetActive(!hideTitleTextForChoiceBar);

        ApplyChoiceButtonRectLayout();
        StyleChoiceButton(optionButton1, optionText1);
        StyleChoiceButton(optionButton2, optionText2);
        StyleChoiceButton(optionButton3, optionText3);

        if (descriptionText != null)
        {
            descriptionText.fontSize = choiceDescriptionFontSize;
            descriptionText.color = choiceDescriptionColor;
            descriptionText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void CopyPathBuildManagerLayoutDefaults()
    {
        if (pathBuildManagerStyleSource == null)
            pathBuildManagerStyleSource = FindObjectOfType<PathBuildManager>();

        if (pathBuildManagerStyleSource == null)
            return;

        rewardTopBarSize = pathBuildManagerStyleSource.pathTopBarSize;
        rewardTopBarTopOffset = pathBuildManagerStyleSource.pathTopBarTopOffset;
        rewardTopBarLeftInset = pathBuildManagerStyleSource.pathTopBarLeftInset;
        rewardTopBarRightInset = pathBuildManagerStyleSource.pathTopBarRightInset;
        hideUnusedTopBarTextLabels = pathBuildManagerStyleSource.hideUnusedTopBarTextLabels;
        choiceBarColor = pathBuildManagerStyleSource.choiceBarColor;
        choiceButtonColor = pathBuildManagerStyleSource.choiceButtonColor;
        choiceDescriptionColor = pathBuildManagerStyleSource.choiceDescriptionColor;
        choiceButtonRowSpacing = pathBuildManagerStyleSource.choiceButtonRowSpacing;
        forceChoiceButtonRectLayout = pathBuildManagerStyleSource.forceChoiceButtonRectLayout;
        choiceButtonTopPadding = pathBuildManagerStyleSource.choiceButtonTopPadding;
        choiceButtonHeight = pathBuildManagerStyleSource.choiceButtonHeight;
        choiceDescriptionHeight = pathBuildManagerStyleSource.choiceDescriptionHeight;
        choiceDescriptionBottomPadding = pathBuildManagerStyleSource.choiceDescriptionBottomPadding;
        choiceDescriptionFontSize = pathBuildManagerStyleSource.choiceDescriptionFontSize;
    }

    private void ApplyChoiceButtonRectLayout()
    {
        if (!forceChoiceButtonRectLayout || rewardTopBar == null)
            return;

        DisableChoiceBarLayoutGroups(rewardTopBar.transform);

        Button[] buttons = { optionButton1, optionButton2, optionButton3 };
        int visibleButtonCount = 3;
        float spacing = Mathf.Max(0f, choiceButtonRowSpacing);
        float topPadding = Mathf.Max(0f, choiceButtonTopPadding);
        float buttonHeight = Mathf.Max(24f, choiceButtonHeight);
        float totalSpacing = spacing * (visibleButtonCount - 1);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null)
                continue;

            if (buttonRect.parent != rewardTopBar.transform)
                buttonRect.SetParent(rewardTopBar.transform, false);

            float minX = i / (float)visibleButtonCount;
            float maxX = (i + 1f) / visibleButtonCount;
            float leftOffset = i == 0 ? 0f : spacing * 0.5f;
            float rightOffset = i == visibleButtonCount - 1 ? 0f : -spacing * 0.5f;

            buttonRect.anchorMin = new Vector2(minX, 1f);
            buttonRect.anchorMax = new Vector2(maxX, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -topPadding);
            buttonRect.sizeDelta = new Vector2(-totalSpacing / visibleButtonCount, buttonHeight);
            buttonRect.offsetMin = new Vector2(leftOffset, buttonRect.offsetMin.y);
            buttonRect.offsetMax = new Vector2(rightOffset, buttonRect.offsetMax.y);
        }

        ApplyChoiceDescriptionRect();
    }

    private void ApplyChoiceDescriptionRect()
    {
        if (descriptionText == null || rewardTopBar == null)
            return;

        RectTransform descriptionRect = descriptionText.GetComponent<RectTransform>();
        if (descriptionRect == null)
            return;

        if (descriptionRect.parent != rewardTopBar.transform)
            descriptionRect.SetParent(rewardTopBar.transform, false);

        float bottomPadding = Mathf.Max(0f, choiceDescriptionBottomPadding);
        float descriptionHeight = Mathf.Max(18f, choiceDescriptionHeight);

        descriptionRect.anchorMin = new Vector2(0f, 0f);
        descriptionRect.anchorMax = new Vector2(1f, 0f);
        descriptionRect.pivot = new Vector2(0.5f, 0f);
        descriptionRect.anchoredPosition = new Vector2(0f, bottomPadding);
        descriptionRect.sizeDelta = new Vector2(0f, descriptionHeight);
        descriptionRect.offsetMin = new Vector2(0f, bottomPadding);
        descriptionRect.offsetMax = new Vector2(0f, bottomPadding + descriptionHeight);
    }

    private void DisableChoiceBarLayoutGroups(Transform root)
    {
        if (root == null)
            return;

        LayoutGroup[] layoutGroups = root.GetComponentsInChildren<LayoutGroup>(true);

        foreach (LayoutGroup layoutGroup in layoutGroups)
        {
            if (layoutGroup != null)
                layoutGroup.enabled = false;
        }
    }

    private void HideUnusedTopBarTextLabels()
    {
        if (!hideUnusedTopBarTextLabels || rewardTopBar == null)
            return;

        TextMeshProUGUI[] labels = rewardTopBar.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI label in labels)
        {
            if (label == null || label == optionText1 || label == optionText2 || label == optionText3 || label == descriptionText || label == titleText)
                continue;

            if (label.text == "New Text" || string.IsNullOrWhiteSpace(label.text))
                label.gameObject.SetActive(false);
        }
    }

    private void StyleChoiceButton(Button button, TextMeshProUGUI text)
    {
        if (button != null)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = choiceButtonColor;

            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null)
                layout = button.gameObject.AddComponent<LayoutElement>();

            layout.preferredHeight = choiceButtonHeight;
            layout.minHeight = Mathf.Max(24f, choiceButtonHeight - 4f);
        }

        if (text != null)
        {
            text.fontSize = Mathf.Max(text.fontSize, 17f);
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    private void GenerateOptions()
    {
        currentSelectionPowerMultiplier = doubleRiskPendingForNextSelection ? 2 : 1;

        List<EliteRewardOption> rewardPool = CreateSelectableRewardPool();

        for (int i = 0; i < currentOptions.Length; i++)
            currentOptions[i] = DrawRewardFromPool(rewardPool, i);

        doubleRiskOfferedInPreviousSelection = CurrentOptionsContainReward(EliteRewardType.DoubleRisk);
    }

    private List<EliteRewardOption> CreateSelectableRewardPool()
    {
        List<EliteRewardOption> rewardPool = new List<EliteRewardOption>();

        foreach (EliteRewardType rewardType in GetDefaultRewardTypes())
            AddRewardIfTypeMissing(rewardPool, CreateDefaultRewardOption(rewardType));

        if (usePlaceholderRewardsUntilDesigned || possibleRewards == null)
            return rewardPool;

        foreach (EliteRewardOption option in possibleRewards)
        {
            if (option == null)
                continue;

            if (option.rewardType == EliteRewardType.PendingReward)
                continue;

            if (option.rewardType == EliteRewardType.DoubleRisk && doubleRiskOfferedInPreviousSelection)
                continue;

            AddRewardIfTypeMissing(rewardPool, CreateConfiguredRewardOption(option));
        }

        return rewardPool;
    }

    private List<EliteRewardType> GetDefaultRewardTypes()
    {
        List<EliteRewardType> rewardTypes = new List<EliteRewardType>
        {
            EliteRewardType.EliteGoldReserve,
            EliteRewardType.EliteXPBoost,
            EliteRewardType.FocusTraining,
            EliteRewardType.StrengthenWeak,
            EliteRewardType.StrongestTowerUpgradePoint,
            EliteRewardType.EliteRepair,
            EliteRewardType.EliteLegacy,
            EliteRewardType.TileChoiceQuality
        };

        if (!doubleRiskOfferedInPreviousSelection)
            rewardTypes.Add(EliteRewardType.DoubleRisk);

        return rewardTypes;
    }

    private bool CurrentOptionsContainReward(EliteRewardType rewardType)
    {
        foreach (EliteRewardOption option in currentOptions)
        {
            if (option != null && option.rewardType == rewardType)
                return true;
        }

        return false;
    }

    private EliteRewardOption CreateDefaultRewardOption(EliteRewardType rewardType)
    {
        return new EliteRewardOption
        {
            displayName = GetDefaultRewardDisplayName(rewardType),
            description = BuildRewardDescription(rewardType, Mathf.Max(1, currentSelectionPowerMultiplier)),
            rewardType = rewardType
        };
    }

    private EliteRewardOption CreateConfiguredRewardOption(EliteRewardOption option)
    {
        if (option == null)
            return null;

        string displayName = string.IsNullOrWhiteSpace(option.displayName)
            ? GetDefaultRewardDisplayName(option.rewardType)
            : option.displayName;

        string description = string.IsNullOrWhiteSpace(option.description)
            ? BuildRewardDescription(option.rewardType, Mathf.Max(1, currentSelectionPowerMultiplier))
            : option.description;

        return new EliteRewardOption
        {
            displayName = displayName,
            description = description,
            rewardType = option.rewardType
        };
    }

    private void AddRewardIfTypeMissing(List<EliteRewardOption> rewardPool, EliteRewardOption option)
    {
        if (rewardPool == null || option == null)
            return;

        foreach (EliteRewardOption existingOption in rewardPool)
        {
            if (existingOption != null && existingOption.rewardType == option.rewardType)
                return;
        }

        rewardPool.Add(option);
    }

    private string GetDefaultRewardDisplayName(EliteRewardType rewardType)
    {
        switch (rewardType)
        {
            case EliteRewardType.EliteGoldReserve:
                return "Elite-Goldreserve";
            case EliteRewardType.EliteXPBoost:
                return "Elite-XP-Schub";
            case EliteRewardType.FocusTraining:
                return "Fokus-Training";
            case EliteRewardType.StrengthenWeak:
                return "Schwache stärken";
            case EliteRewardType.StrongestTowerUpgradePoint:
                return "Upgradepunkt";
            case EliteRewardType.EliteRepair:
                return "Elite-Reparatur";
            case EliteRewardType.EliteLegacy:
                return "Elite-Erbe";
            case EliteRewardType.TileChoiceQuality:
                return "Auswahlqualität";
            case EliteRewardType.DoubleRisk:
                return "Dopple Risk";
            default:
                return "Elite-Belohnung";
        }
    }

    private string BuildRewardDescription(EliteRewardType rewardType, int powerMultiplier)
    {
        int safePower = Mathf.Max(1, powerMultiplier);
        int wave = GetRewardWaveNumber();

        switch (rewardType)
        {
            case EliteRewardType.EliteGoldReserve:
                return "Erhalte +" + CalculateEliteGoldReserveAmount(wave, safePower) + " Gold.";
            case EliteRewardType.EliteXPBoost:
                return "Alle aktuellen Tower erhalten +" + CalculateEliteXPBoostAmount(wave, safePower) + " XP.";
            case EliteRewardType.FocusTraining:
                return "Der stärkste Tower erhält +" + CalculateFocusTrainingXPAmount(wave, safePower) + " XP.";
            case EliteRewardType.StrengthenWeak:
                return "Tower unter Level " + Mathf.Max(1, strengthenWeakTargetLevel) + " steigen um +" + CalculateStrengthenWeakLevelUps(safePower) + " Level, aber nicht über das Ziel-Level.";
            case EliteRewardType.StrongestTowerUpgradePoint:
                return "Der stärkste Tower erhält +" + CalculateStrongestTowerUpgradePoints(safePower) + " Upgradepunkt(e).";
            case EliteRewardType.EliteRepair:
                return "Heilt +" + CalculateEliteRepairLives(safePower) + " Leben, aber nie über dein Start-Maximum.";
            case EliteRewardType.EliteLegacy:
                return "Alle aktuellen Tower erhalten dauerhaft +" + FormatPercent(CalculateEliteLegacyBonus(safePower)) + " aktuelle Werte.";
            case EliteRewardType.TileChoiceQuality:
                return "Merkt +" + CalculateTileChoiceQualityBoosts(safePower) + " Qualitätsladung für spätere Tile-Auswahlen vor.";
            case EliteRewardType.DoubleRisk:
                return "Die nächste Elite-Belohnungsauswahl ist doppelt so stark. Diese Option erscheint nicht direkt danach.";
            default:
                return "Diese Elite-Belohnung ist noch ohne Gameplay-Effekt.";
        }
    }

    private int GetRewardWaveNumber()
    {
        if (currentWaveResult != null && currentWaveResult.waveNumber > 0)
            return currentWaveResult.waveNumber;

        if (gameManager != null && gameManager.waveNumber > 0)
            return gameManager.waveNumber;

        return 1;
    }

    private int CalculateEliteGoldReserveAmount(int wave, int powerMultiplier)
    {
        return Mathf.Max(0, eliteGoldPerWave) * Mathf.Max(1, wave) * Mathf.Max(1, powerMultiplier);
    }

    private int CalculateEliteXPBoostAmount(int wave, int powerMultiplier)
    {
        return Mathf.Max(0, eliteXPBase + Mathf.Max(1, wave)) * Mathf.Max(1, powerMultiplier);
    }

    private int CalculateFocusTrainingXPAmount(int wave, int powerMultiplier)
    {
        int baseAmount = Mathf.Max(0, focusTrainingXPBase) + Mathf.Max(0, focusTrainingXPPerWave) * Mathf.Max(1, wave);
        return baseAmount * Mathf.Max(1, powerMultiplier);
    }

    private int CalculateStrengthenWeakLevelUps(int powerMultiplier)
    {
        return Mathf.Max(1, strengthenWeakLevelUps) * Mathf.Max(1, powerMultiplier);
    }

    private int CalculateStrongestTowerUpgradePoints(int powerMultiplier)
    {
        return Mathf.Max(1, strongestTowerUpgradePoints) * Mathf.Max(1, powerMultiplier);
    }

    private int CalculateEliteRepairLives(int powerMultiplier)
    {
        return Mathf.Max(1, eliteRepairLives) * Mathf.Max(1, powerMultiplier);
    }

    private float CalculateEliteLegacyBonus(int powerMultiplier)
    {
        return Mathf.Max(0f, eliteLegacyStatBonus) * Mathf.Max(1, powerMultiplier);
    }

    private int CalculateTileChoiceQualityBoosts(int powerMultiplier)
    {
        return Mathf.Max(1, tileChoiceQualityBoosts) * Mathf.Max(1, powerMultiplier);
    }

    private string FormatPercent(float percentAsFraction)
    {
        return (Mathf.Max(0f, percentAsFraction) * 100f).ToString("0.#") + "%";
    }

    private EliteRewardOption DrawRewardFromPool(List<EliteRewardOption> rewardPool, int optionIndex)
    {
        if (rewardPool == null || rewardPool.Count == 0)
            return CreatePendingRewardOption(optionIndex);

        int randomIndex = Random.Range(0, rewardPool.Count);
        EliteRewardOption option = rewardPool[randomIndex];
        rewardPool.RemoveAt(randomIndex);
        return option != null ? option : CreatePendingRewardOption(optionIndex);
    }

    private EliteRewardOption CreatePendingRewardOption(int optionIndex)
    {
        return new EliteRewardOption
        {
            displayName = "Belohnung " + (optionIndex + 1),
            description = "Platzhalter: Die echte Elite-Belohnung legen wir im nächsten Schritt fest. Aktuell schließt die Auswahl ohne Effekt.",
            rewardType = EliteRewardType.PendingReward
        };
    }

    private void UpdateUI()
    {
        if (titleText != null)
        {
            titleText.text = "ELITE BESIEGT";
            titleText.gameObject.SetActive(!hideTitleTextForChoiceBar);
        }

        ShowDefaultChoiceDescription();
        SetOptionText(optionText1, 0);
        SetOptionText(optionText2, 1);
        SetOptionText(optionText3, 2);
    }

    private void SetOptionText(TextMeshProUGUI textField, int index)
    {
        if (textField == null)
            return;

        if (index < 0 || index >= currentOptions.Length || currentOptions[index] == null)
        {
            textField.text = "[" + (index + 1) + "] Belohnung";
            return;
        }

        textField.text = "[" + (index + 1) + "] " + currentOptions[index].displayName;
    }

    public void ShowOptionDescription(int optionIndex)
    {
        if (!selectionOpen || descriptionText == null)
            return;

        if (optionIndex < 0 || optionIndex >= currentOptions.Length || currentOptions[optionIndex] == null)
        {
            ShowDefaultChoiceDescription();
            return;
        }

        descriptionText.text = currentOptions[optionIndex].description;
    }

    public void ShowDefaultChoiceDescription()
    {
        if (descriptionText != null)
            descriptionText.text = currentSelectionPowerMultiplier > 1
                ? "Dopple Risk aktiv: Diese Auswahl ist doppelt so stark."
                : "Wähle eine Elite-Belohnung. Hover zeigt den Effekt.";
    }

    private void ChooseOption(int index)
    {
        if (!selectionOpen)
            return;

        if (index < 0 || index >= currentOptions.Length)
            return;

        EliteRewardOption option = currentOptions[index];

        if (option == null)
            return;

        selectionOpen = false;

        if (rewardTopBar != null)
            rewardTopBar.SetActive(false);

        ApplyReward(option);

        if (gameManager != null)
            gameManager.ResumeBuildPhaseAfterEliteRewardChoice();
    }

    private void ApplyReward(EliteRewardOption option)
    {
        if (gameManager == null || option == null)
            return;

        string rewardName = string.IsNullOrEmpty(option.displayName) ? "Elite-Belohnung" : option.displayName;
        int powerMultiplier = Mathf.Max(1, currentSelectionPowerMultiplier);
        string trackedRewardName = powerMultiplier > 1 ? rewardName + " x" + powerMultiplier : rewardName;

        doubleRiskPendingForNextSelection = false;

        gameManager.RegisterEliteRewardChoice(trackedRewardName);

        int wave = GetRewardWaveNumber();

        switch (option.rewardType)
        {
            case EliteRewardType.EliteGoldReserve:
                gameManager.AddGold(CalculateEliteGoldReserveAmount(wave, powerMultiplier), false, RunGoldSource.Other);
                break;
            case EliteRewardType.EliteXPBoost:
                gameManager.GrantXPToAllTowers(CalculateEliteXPBoostAmount(wave, powerMultiplier), false);
                break;
            case EliteRewardType.FocusTraining:
                gameManager.GrantXPToStrongestTower(CalculateFocusTrainingXPAmount(wave, powerMultiplier), false);
                break;
            case EliteRewardType.StrengthenWeak:
                gameManager.RaiseWeakTowersByLevels(strengthenWeakTargetLevel, CalculateStrengthenWeakLevelUps(powerMultiplier));
                break;
            case EliteRewardType.StrongestTowerUpgradePoint:
                gameManager.GrantUpgradePointsToStrongestTower(CalculateStrongestTowerUpgradePoints(powerMultiplier));
                break;
            case EliteRewardType.EliteRepair:
                gameManager.AddLivesCapped(CalculateEliteRepairLives(powerMultiplier));
                break;
            case EliteRewardType.EliteLegacy:
                gameManager.ApplyEliteLegacyBoostToAllTowers(CalculateEliteLegacyBonus(powerMultiplier));
                break;
            case EliteRewardType.TileChoiceQuality:
                gameManager.AddEliteTileChoiceQualityBoosts(CalculateTileChoiceQualityBoosts(powerMultiplier));
                break;
            case EliteRewardType.DoubleRisk:
                doubleRiskPendingForNextSelection = true;
                Debug.Log("Elite-Belohnung gewählt: Dopple Risk. Die nächste Elite-Auswahl ist doppelt so stark.");
                break;
            default:
                Debug.Log("Elite-Belohnung gewählt: " + rewardName + " (noch ohne Gameplay-Effekt).");
                break;
        }

        currentSelectionPowerMultiplier = 1;
    }
}

public class EliteRewardOptionHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private EliteRewardChoiceManager owner;
    private int optionIndex;

    public void Initialize(EliteRewardChoiceManager newOwner, int newOptionIndex)
    {
        owner = newOwner;
        optionIndex = newOptionIndex;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
            owner.ShowOptionDescription(optionIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
            owner.ShowDefaultChoiceDescription();
    }
}
