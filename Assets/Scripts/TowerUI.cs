using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum TowerUIMenu
{
    GoldUpgrades,
    PointUpgrades
}

public class TowerUI : MonoBehaviour
{
    [Header("Main")]
    public GameObject panel;
    public GameManager gameManager;
    public Button closeButton;

    [Header("Background Images")]
    public Image panelBackground;
    public Image headerBackground;
    public Image progressBackground;
    public Image statsBackground;
    public Image targetBackground;
    public Image tabRowBackground;
    public Image upgradeAreaBackground;
    public Image goldUpgradePanelBackground;
    public Image pointUpgradePanelBackground;
    public Image xpFillImage;

    [Header("Header")]
    public TextMeshProUGUI titleText;

    [Header("Progress UI")]
    public Slider xpBar;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI upgradePointText;
    public TextMeshProUGUI metaPointText;
    public TextMeshProUGUI visualTierText;

    [Header("Stats UI")]
    public TextMeshProUGUI statsText;

    [Header("Targeting UI")]
    public Button targetModeButton;
    public TextMeshProUGUI targetModeButtonText;

    [Header("Sell UI")]
    public Button sellButton;
    public TextMeshProUGUI sellButtonText;
    public bool autoCreateSellButton = true;
    public bool hideCloseButtonBecauseRightClickCloses = true;
    public bool hideMetaAndVisualTierText = true;
    public float sellButtonBottomBarHeight = 52f;
    public Color sellButtonColor = new Color32(200, 75, 75, 255);
    public Vector2 sellButtonTopRightSize = new Vector2(150f, 34f);
    public int towerPanelSiblingIndexWhenOpen = 2;

    [Header("Menu Tabs")]
    public Button goldUpgradeTabButton;
    public Button pointUpgradeTabButton;
    public TextMeshProUGUI goldUpgradeTabText;
    public TextMeshProUGUI pointUpgradeTabText;

    [Header("Menu Panels")]
    public GameObject goldUpgradePanel;
    public GameObject pointUpgradePanel;

    [Header("Gold Upgrade UI")]
    public TextMeshProUGUI goldUpgradeInfoText;
    public Button goldDamageButton;
    public Button goldRangeButton;
    public Button goldFireRateButton;
    public Button goldEffectButton;
    public TextMeshProUGUI goldDamageButtonText;
    public TextMeshProUGUI goldRangeButtonText;
    public TextMeshProUGUI goldFireRateButtonText;
    public TextMeshProUGUI goldEffectButtonText;

    [Header("Upgrade Point UI")]
    public TextMeshProUGUI pointUpgradeInfoText;
    public Button pointDamageButton;
    public Button pointRangeButton;
    public Button pointFireRateButton;
    public Button pointEffectButton;
    public TextMeshProUGUI pointDamageButtonText;
    public TextMeshProUGUI pointRangeButtonText;
    public TextMeshProUGUI pointFireRateButtonText;
    public TextMeshProUGUI pointEffectButtonText;

    [Header("Theme Colors")]
    public Color panelColor = new Color32(20, 24, 31, 245);
    public Color cardColor = new Color32(22, 39, 60, 255);
    public Color cardAltColor = new Color32(35, 45, 64, 255);
    public Color headerColor = new Color32(90, 185, 226, 255);
    public Color targetButtonColor = new Color32(96, 193, 255, 255);
    public Color goldAccentColor = new Color32(214, 164, 65, 255);
    public Color pointAccentColor = new Color32(65, 125, 245, 255);
    public Color closeButtonColor = new Color32(200, 75, 75, 255);
    public Color inactiveTabColor = new Color32(50, 58, 78, 255);
    public Color disabledButtonColor = new Color32(55, 64, 80, 255);
    public Color textPrimaryColor = new Color32(240, 244, 250, 255);
    public Color textSecondaryColor = new Color32(185, 194, 208, 255);
    public Color textDisabledColor = new Color32(120, 128, 140, 255);

    private Tower selectedTower;
    private TowerUIMenu currentMenu = TowerUIMenu.GoldUpgrades;

    private void Start()
    {
        CreateSellButtonIfNeeded();
        SetupButtons();
        ApplyStaticTheme();

        if (panel != null)
            panel.SetActive(false);
    }

    private void Update()
    {
        if (selectedTower != null && Input.GetMouseButtonDown(1))
        {
            Close();
            return;
        }

        if (selectedTower != null)
        {
            UpdateUI();
            selectedTower.RefreshRangeIndicatorIfVisible();
        }
    }

    private void SetupButtons()
    {
        if (closeButton != null && closeButton != sellButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);

            if (hideCloseButtonBecauseRightClickCloses)
                closeButton.gameObject.SetActive(false);
        }
        else if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
        }

        if (targetModeButton != null)
        {
            targetModeButton.onClick.RemoveAllListeners();
            targetModeButton.onClick.AddListener(CycleTargetMode);
        }

        if (sellButton != null)
        {
            ApplySellButtonBottomBarLayout();
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(SellSelectedTower);
        }

        if (goldUpgradeTabButton != null)
        {
            goldUpgradeTabButton.onClick.RemoveAllListeners();
            goldUpgradeTabButton.onClick.AddListener(OpenGoldUpgradeMenu);
        }

        if (pointUpgradeTabButton != null)
        {
            pointUpgradeTabButton.onClick.RemoveAllListeners();
            pointUpgradeTabButton.onClick.AddListener(OpenPointUpgradeMenu);
        }

        if (goldDamageButton != null)
        {
            goldDamageButton.onClick.RemoveAllListeners();
            goldDamageButton.onClick.AddListener(UpgradeDamage);
        }

        if (goldRangeButton != null)
        {
            goldRangeButton.onClick.RemoveAllListeners();
            goldRangeButton.onClick.AddListener(UpgradeRange);
        }

        if (goldFireRateButton != null)
        {
            goldFireRateButton.onClick.RemoveAllListeners();
            goldFireRateButton.onClick.AddListener(UpgradeFireRate);
        }

        if (goldEffectButton != null)
        {
            goldEffectButton.onClick.RemoveAllListeners();
            goldEffectButton.onClick.AddListener(UpgradeEffectWithGold);
        }

        if (pointDamageButton != null)
        {
            pointDamageButton.onClick.RemoveAllListeners();
            pointDamageButton.onClick.AddListener(UpgradeDamageWithPoint);
        }

        if (pointRangeButton != null)
        {
            pointRangeButton.onClick.RemoveAllListeners();
            pointRangeButton.onClick.AddListener(UpgradeRangeWithPoint);
        }

        if (pointFireRateButton != null)
        {
            pointFireRateButton.onClick.RemoveAllListeners();
            pointFireRateButton.onClick.AddListener(UpgradeFireRateWithPoint);
        }

        if (pointEffectButton != null)
        {
            pointEffectButton.onClick.RemoveAllListeners();
            pointEffectButton.onClick.AddListener(UpgradeEffectWithPoint);
        }
    }

    public void SelectTower(Tower tower)
    {
        if (selectedTower != null && selectedTower != tower)
            selectedTower.SetRangeIndicatorVisible(false);

        selectedTower = tower;

        if (panel != null)
        {
            panel.transform.SetSiblingIndex(Mathf.Max(0, towerPanelSiblingIndexWhenOpen));
            panel.SetActive(true);
        }

        currentMenu = TowerUIMenu.GoldUpgrades;
        ApplyCurrentMenuVisibility();
        ApplyStaticTheme();
        UpdateUI();

        if (selectedTower != null)
            selectedTower.SetRangeIndicatorVisible(true);
    }

    public void Close()
    {
        if (selectedTower != null)
            selectedTower.SetRangeIndicatorVisible(false);

        selectedTower = null;

        if (panel != null)
            panel.SetActive(false);
    }

    public void OpenGoldUpgradeMenu()
    {
        currentMenu = TowerUIMenu.GoldUpgrades;
        ApplyCurrentMenuVisibility();
        UpdateUI();
    }

    public void OpenPointUpgradeMenu()
    {
        currentMenu = TowerUIMenu.PointUpgrades;
        ApplyCurrentMenuVisibility();
        UpdateUI();
    }

    private void ApplyCurrentMenuVisibility()
    {
        if (goldUpgradePanel != null)
            goldUpgradePanel.SetActive(currentMenu == TowerUIMenu.GoldUpgrades);

        if (pointUpgradePanel != null)
            pointUpgradePanel.SetActive(currentMenu == TowerUIMenu.PointUpgrades);
    }

    private void ApplyStaticTheme()
    {
        SetImageColor(panelBackground, panelColor);
        SetImageColor(headerBackground, headerColor);
        SetImageColor(progressBackground, cardColor);
        SetImageColor(statsBackground, cardColor);
        SetImageColor(targetBackground, cardColor);
        SetImageColor(tabRowBackground, cardAltColor);
        SetImageColor(upgradeAreaBackground, cardColor);
        SetImageColor(goldUpgradePanelBackground, cardColor);
        SetImageColor(pointUpgradePanelBackground, cardColor);

        SetTextColor(titleText, textPrimaryColor);
        SetTextColor(levelText, textPrimaryColor);
        SetTextColor(xpText, textPrimaryColor);
        SetTextColor(upgradePointText, new Color32(70, 140, 255, 255));
        SetTextColor(metaPointText, textSecondaryColor);
        SetTextColor(visualTierText, textSecondaryColor);
        SetTextColor(statsText, textPrimaryColor);

        if (xpFillImage != null)
            xpFillImage.color = new Color32(70, 220, 120, 255);

        ApplySellButtonVisualStyle();
    }

    private void UpdateUI()
    {
        if (selectedTower == null)
            return;

        UpdateHeaderUI();
        UpdateProgressUI();
        UpdateStatsUI();
        UpdateTargetUI();
        UpdateGoldUpgradeUI();
        UpdatePointUpgradeUI();
        UpdateDynamicTheme();
        UpdateButtonStates();
        selectedTower.RefreshRangeIndicatorIfVisible();
    }

    private void UpdateHeaderUI()
    {
        if (titleText != null)
            titleText.text = selectedTower.towerName + " (Lv " + selectedTower.level + ")";
    }

    private void UpdateProgressUI()
    {
        if (xpBar != null)
        {
            xpBar.maxValue = selectedTower.xpToNextLevel;
            xpBar.value = selectedTower.currentXP;
        }

        if (levelText != null)
            levelText.text = "Level: " + selectedTower.level;

        if (xpText != null)
            xpText.text = "XP: " + selectedTower.currentXP + " / " + selectedTower.xpToNextLevel;

        if (upgradePointText != null)
            upgradePointText.text = "Upgrade Points: " + selectedTower.upgradePoints;

        if (metaPointText != null)
        {
            metaPointText.text = hideMetaAndVisualTierText ? "" : "Meta vorbereitet: " + selectedTower.metaProgressionPoints;
            metaPointText.gameObject.SetActive(!hideMetaAndVisualTierText);
        }

        if (visualTierText != null)
        {
            visualTierText.text = hideMetaAndVisualTierText ? "" : "Visual Tier: " + selectedTower.visualTier;
            visualTierText.gameObject.SetActive(!hideMetaAndVisualTierText);
        }
    }

    private void UpdateStatsUI()
    {
        if (statsText == null)
            return;

        statsText.text =
            "Stats" +
            "\nDMG: " + BuildEffectiveStatText(selectedTower.damage.ToString(), selectedTower.GetEffectiveDamage().ToString()) +
            " | RNG: " + BuildEffectiveStatText(selectedTower.range.ToString("0.0"), selectedTower.GetEffectiveRange().ToString("0.0")) +
            " | FR: " + BuildEffectiveStatText(selectedTower.fireRate.ToString("0.00"), selectedTower.GetEffectiveFireRate().ToString("0.00")) +
            "\nTarget: " + selectedTower.GetTargetModeName() +
            GetEffectStatsText() +
            "\n\nWave" +
            "\nKills: " + selectedTower.currentWaveKills +
            " | Assists: " + selectedTower.currentWaveAssists +
            "\nDamage: " + selectedTower.currentWaveDamageDealt.ToString("0") +
            "\n\nTotal" +
            "\nKills: " + selectedTower.totalKills +
            " | Assists: " + selectedTower.totalAssists +
            "\nDamage: " + selectedTower.totalDamageDealt.ToString("0");
    }

    private string BuildEffectiveStatText(string baseValue, string effectiveValue)
    {
        if (baseValue == effectiveValue)
            return baseValue;

        return baseValue + " > " + effectiveValue;
    }

    private string GetEffectStatsText()
    {
        if (selectedTower == null || !selectedTower.HasAnyEffect())
            return "";

        string text = "";

        if (selectedTower.appliesBurn)
        {
            text += "\nBurn: " + selectedTower.burnDamage + " / " + selectedTower.burnDuration.ToString("0.0") + "s | max 3 Stacks";
        }

        if (selectedTower.appliesPoison)
        {
            text += "\nPoison: " + selectedTower.poisonDamage + " / " + selectedTower.poisonDuration.ToString("0.0") + "s | ignoriert Armor";
        }

        if (selectedTower.appliesSlow)
        {
            text += "\nSlow: " + selectedTower.slowAmount.ToString("0.00") + " / " + selectedTower.slowDuration.ToString("0.0") + "s";
        }

        return text;
    }

    private void UpdateTargetUI()
    {
        if (targetModeButtonText != null)
            targetModeButtonText.text = "Target: " + selectedTower.GetTargetModeName();
    }

    private void UpdateGoldUpgradeUI()
    {
        if (goldUpgradeInfoText != null)
        {
            goldUpgradeInfoText.text = "";
            goldUpgradeInfoText.gameObject.SetActive(false);
        }

        if (goldDamageButtonText != null)
            goldDamageButtonText.text = BuildUpgradeButtonText("Damage", "+" + selectedTower.damageIncreasePerGoldUpgrade, selectedTower.damageUpgradeCost + " Gold");

        if (goldRangeButtonText != null)
            goldRangeButtonText.text = BuildUpgradeButtonText("Range", "+" + selectedTower.rangeIncreasePerGoldUpgrade.ToString("0.00"), selectedTower.rangeUpgradeCost + " Gold");

        if (goldFireRateButtonText != null)
            goldFireRateButtonText.text = BuildUpgradeButtonText("Fire Rate", "+" + selectedTower.fireRateIncreasePerGoldUpgrade.ToString("0.00"), selectedTower.fireRateUpgradeCost + " Gold");

        if (goldEffectButtonText != null)
        {
            if (!selectedTower.HasAnyEffect())
                goldEffectButtonText.text = BuildUpgradeButtonText("Effect Power", "Nicht verfügbar", "-");
            else
                goldEffectButtonText.text = BuildUpgradeButtonText("Effect Power", GetGoldEffectPowerIncreaseText(), selectedTower.effectUpgradeCost + " Gold");
        }
    }

    private void UpdatePointUpgradeUI()
    {
        if (pointUpgradeInfoText != null)
        {
            pointUpgradeInfoText.text = "";
            pointUpgradeInfoText.gameObject.SetActive(false);
        }

        if (pointDamageButtonText != null)
            pointDamageButtonText.text = BuildUpgradeButtonText("Damage", "+" + selectedTower.GetPointDamageIncreasePreview(), selectedTower.GetUpgradePointCost() + " Point");

        if (pointRangeButtonText != null)
            pointRangeButtonText.text = BuildUpgradeButtonText("Range", "+" + selectedTower.GetPointRangeIncreasePreview().ToString("0.00"), selectedTower.GetUpgradePointCost() + " Point");

        if (pointFireRateButtonText != null)
            pointFireRateButtonText.text = BuildUpgradeButtonText("Fire Rate", "+" + selectedTower.GetPointFireRateIncreasePreview().ToString("0.00"), selectedTower.GetUpgradePointCost() + " Point");

        if (pointEffectButtonText != null)
        {
            if (!selectedTower.HasAnyEffect())
                pointEffectButtonText.text = BuildUpgradeButtonText("Effect Power", "Nicht verfügbar", "-");
            else
                pointEffectButtonText.text = BuildUpgradeButtonText("Effect Power", GetPointEffectPowerIncreaseText(), selectedTower.GetUpgradePointCost() + " Point");
        }
    }

    private string GetGoldEffectPowerIncreaseText()
    {
        if (selectedTower == null)
            return "";

        if (selectedTower.appliesBurn)
            return "+" + selectedTower.burnDamageIncreasePerGoldUpgrade + " Burn / +" + selectedTower.effectDurationIncreasePerGoldUpgrade.ToString("0.00") + "s";

        if (selectedTower.appliesPoison)
            return "+" + selectedTower.poisonDamageIncreasePerGoldUpgrade + " Poison / +" + selectedTower.effectDurationIncreasePerGoldUpgrade.ToString("0.00") + "s";

        if (selectedTower.appliesSlow)
            return "-" + selectedTower.slowAmountIncreasePerGoldUpgrade.ToString("0.00") + " Slow / +" + selectedTower.slowDurationIncreasePerGoldUpgrade.ToString("0.00") + "s";

        return "+0";
    }

    private string GetPointEffectPowerIncreaseText()
    {
        if (selectedTower == null)
            return "";

        if (selectedTower.appliesBurn)
            return "+" + selectedTower.GetPointBurnDamageIncreasePreview() + " Burn / +" + selectedTower.GetPointEffectDurationIncreasePreview().ToString("0.00") + "s";

        if (selectedTower.appliesPoison)
            return "+" + selectedTower.GetPointPoisonDamageIncreasePreview() + " Poison / +" + selectedTower.GetPointEffectDurationIncreasePreview().ToString("0.00") + "s";

        if (selectedTower.appliesSlow)
            return "-" + selectedTower.GetPointSlowAmountIncreasePreview().ToString("0.00") + " Slow / +" + selectedTower.GetPointSlowDurationIncreasePreview().ToString("0.00") + "s";

        return "+0";
    }

    private string BuildUpgradeButtonText(string title, string valueText, string costText)
    {
        return title + "\n" + valueText + "\n" + costText;
    }

    private void UpdateDynamicTheme()
    {
        SetTabButtonStyle(goldUpgradeTabButton, goldUpgradeTabText, currentMenu == TowerUIMenu.GoldUpgrades, goldAccentColor);
        SetTabButtonStyle(pointUpgradeTabButton, pointUpgradeTabText, currentMenu == TowerUIMenu.PointUpgrades, pointAccentColor);
        SetActionButtonStyle(targetModeButton, targetModeButtonText, true, targetButtonColor);
        ApplySellButtonVisualStyle();
        SetCloseButtonStyle();
    }

    private void UpdateButtonStates()
    {
        if (selectedTower == null)
            return;

        bool hasGameManager = gameManager != null;
        int currentGold = hasGameManager ? gameManager.gold : 0;
        bool canBuyGoldDamage = hasGameManager && currentGold >= selectedTower.damageUpgradeCost;
        bool canBuyGoldRange = hasGameManager && currentGold >= selectedTower.rangeUpgradeCost;
        bool canBuyGoldFireRate = hasGameManager && currentGold >= selectedTower.fireRateUpgradeCost;
        bool canBuyGoldEffect = hasGameManager && selectedTower.HasAnyEffect() && currentGold >= selectedTower.effectUpgradeCost;
        bool hasEnoughPoints = selectedTower.upgradePoints >= selectedTower.GetUpgradePointCost();
        bool canBuyPointEffect = hasEnoughPoints && selectedTower.HasAnyEffect();

        if (sellButtonText != null)
        {
            sellButtonText.text = "Verkaufen für: " + selectedTower.GetSellRefundAmount() + " Gold";
            sellButtonText.fontSize = 16f;
            sellButtonText.fontStyle = FontStyles.Bold;
            sellButtonText.alignment = TextAlignmentOptions.Center;
            sellButtonText.color = Color.white;
        }

        ApplySellButtonVisualStyle();

        SetActionButtonStyle(goldDamageButton, goldDamageButtonText, canBuyGoldDamage, goldAccentColor);
        SetActionButtonStyle(goldRangeButton, goldRangeButtonText, canBuyGoldRange, goldAccentColor);
        SetActionButtonStyle(goldFireRateButton, goldFireRateButtonText, canBuyGoldFireRate, goldAccentColor);
        SetActionButtonStyle(goldEffectButton, goldEffectButtonText, canBuyGoldEffect, goldAccentColor);
        SetActionButtonStyle(pointDamageButton, pointDamageButtonText, hasEnoughPoints, pointAccentColor);
        SetActionButtonStyle(pointRangeButton, pointRangeButtonText, hasEnoughPoints, pointAccentColor);
        SetActionButtonStyle(pointFireRateButton, pointFireRateButtonText, hasEnoughPoints, pointAccentColor);
        SetActionButtonStyle(pointEffectButton, pointEffectButtonText, canBuyPointEffect, pointAccentColor);
    }

    private void SetActionButtonStyle(Button button, TextMeshProUGUI label, bool interactable, Color accentColor)
    {
        if (button != null)
        {
            button.interactable = interactable;
            Image img = button.GetComponent<Image>();
            Color visibleColor = interactable ? accentColor : new Color32(42, 52, 68, 255);

            if (img != null)
                img.color = visibleColor;

            ColorBlock colors = button.colors;
            colors.normalColor = visibleColor;
            colors.highlightedColor = interactable ? LightenColor(accentColor, 1.15f) : visibleColor;
            colors.pressedColor = interactable ? DarkenColor(accentColor, 0.85f) : visibleColor;
            colors.selectedColor = visibleColor;
            colors.disabledColor = visibleColor;
            button.colors = colors;
        }

        if (label != null)
            label.color = interactable ? Color.white : new Color32(165, 174, 190, 255);
    }

    private Color LightenColor(Color color, float factor)
    {
        return new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), color.a);
    }

    private Color DarkenColor(Color color, float factor)
    {
        return new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), color.a);
    }

    private void SetTabButtonStyle(Button button, TextMeshProUGUI label, bool active, Color accentColor)
    {
        if (button != null)
        {
            Image img = button.GetComponent<Image>();
            if (img != null)
                img.color = active ? accentColor : inactiveTabColor;
        }

        if (label != null)
            label.color = active ? textPrimaryColor : textSecondaryColor;
    }

    private void SetCloseButtonStyle()
    {
        if (closeButton == null || hideCloseButtonBecauseRightClickCloses)
            return;

        Image img = closeButton.GetComponent<Image>();

        if (img != null)
            img.color = closeButtonColor;

        ColorBlock colors = closeButton.colors;
        colors.normalColor = closeButtonColor;
        colors.highlightedColor = new Color32(225, 95, 95, 255);
        colors.pressedColor = new Color32(160, 45, 45, 255);
        colors.selectedColor = closeButtonColor;
        colors.disabledColor = closeButtonColor;
        closeButton.colors = colors;

        TextMeshProUGUI buttonText = closeButton.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null)
        {
            buttonText.text = "X";
            buttonText.color = Color.white;
        }
    }

    private void CreateSellButtonIfNeeded()
    {
        if (!autoCreateSellButton || sellButton != null || panel == null)
            return;

        Transform sellButtonParent = panel.transform;

        if (closeButton != null && closeButton.transform.parent != null)
        {
            sellButtonParent = closeButton.transform.parent;
            closeButton.gameObject.SetActive(false);
        }

        GameObject buttonObject = new GameObject("SellButton_Auto", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(sellButtonParent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        ApplySellButtonBottomBarLayout(rectTransform);

        Image image = buttonObject.GetComponent<Image>();
        image.color = closeButtonColor;

        sellButton = buttonObject.GetComponent<Button>();

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        sellButtonText = textObject.GetComponent<TextMeshProUGUI>();
        sellButtonText.alignment = TextAlignmentOptions.Center;
        sellButtonText.fontSize = 16f;
        sellButtonText.enableAutoSizing = true;
        sellButtonText.fontSizeMin = 10f;
        sellButtonText.fontSizeMax = 16f;
        sellButtonText.color = Color.white;
    }

    private void ApplySellButtonVisualStyle()
    {
        if (sellButton == null)
            return;

        sellButton.interactable = selectedTower != null;
        Image buttonImage = sellButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = sellButtonColor;
            buttonImage.raycastTarget = true;
        }

        Image parentImage = sellButton.transform.parent != null ? sellButton.transform.parent.GetComponent<Image>() : null;
        if (parentImage != null)
        {
            parentImage.color = sellButtonColor;
            parentImage.raycastTarget = false;
        }

        Image[] childImages = sellButton.GetComponentsInChildren<Image>(true);
        foreach (Image image in childImages)
        {
            if (image == null)
                continue;

            image.color = sellButtonColor;
            image.raycastTarget = image == buttonImage;
        }

        Graphic[] childGraphics = sellButton.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in childGraphics)
        {
            if (graphic == null || graphic == buttonImage || graphic is TextMeshProUGUI || graphic is Image)
                continue;

            graphic.color = sellButtonColor;
            graphic.raycastTarget = false;
        }

        ColorBlock colors = sellButton.colors;
        colors.normalColor = sellButtonColor;
        colors.highlightedColor = new Color32(225, 95, 95, 255);
        colors.pressedColor = new Color32(160, 45, 45, 255);
        colors.selectedColor = sellButtonColor;
        colors.disabledColor = new Color32(90, 55, 60, 255);
        sellButton.colors = colors;

        if (sellButtonText == null)
            sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (sellButtonText != null)
        {
            RectTransform textRect = sellButtonText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10f, 2f);
                textRect.offsetMax = new Vector2(-10f, -2f);
            }

            sellButtonText.alignment = TextAlignmentOptions.Center;
            sellButtonText.fontSize = 16f;
            sellButtonText.fontStyle = FontStyles.Bold;
            sellButtonText.enableAutoSizing = true;
            sellButtonText.fontSizeMin = 12f;
            sellButtonText.fontSizeMax = 18f;
            sellButtonText.color = Color.white;
            sellButtonText.transform.SetAsLastSibling();
        }
    }

    private void ApplySellButtonBottomBarLayout()
    {
        if (sellButton == null)
            return;

        ApplySellButtonBottomBarLayout(sellButton.GetComponent<RectTransform>());
    }

    private void ApplySellButtonBottomBarLayout(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        bool useExistingBottomRow = rectTransform.parent is RectTransform && panel != null && rectTransform.parent != panel.transform;

        if (useExistingBottomRow)
        {
            RectTransform rowRect = (RectTransform)rectTransform.parent;

            if (panel != null && rowRect.parent != panel.transform)
                rowRect.SetParent(panel.transform, false);

            LayoutGroup rowLayout = rowRect.GetComponent<LayoutGroup>();
            if (rowLayout != null)
                rowLayout.enabled = false;

            ApplyBottomBarRect(rowRect);
            rowRect.SetAsLastSibling();

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return;
        }

        if (panel != null && rectTransform.parent != panel.transform)
            rectTransform.SetParent(panel.transform, false);

        ApplyBottomBarRect(rectTransform);
        rectTransform.SetAsLastSibling();
    }

    private void ApplyBottomBarRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(0f, sellButtonBottomBarHeight);
        rectTransform.offsetMin = new Vector2(0f, 0f);
        rectTransform.offsetMax = new Vector2(0f, sellButtonBottomBarHeight);
    }

    private void SetImageColor(Image img, Color color)
    {
        if (img != null)
            img.color = color;
    }

    private void SetTextColor(TextMeshProUGUI textField, Color color)
    {
        if (textField != null)
            textField.color = color;
    }

    public void CycleTargetMode()
    {
        if (selectedTower == null)
            return;

        selectedTower.CycleTargetMode();
        UpdateUI();
    }

    public void SellSelectedTower()
    {
        if (selectedTower == null || gameManager == null)
            return;

        Tower towerToSell = selectedTower;
        int refund = towerToSell.GetSellRefundAmount();
        TileManager tileManager = gameManager.tileManager != null ? gameManager.tileManager : FindObjectOfType<TileManager>();

        if (refund > 0)
            gameManager.AddGold(refund, false, RunGoldSource.Other);

        if (tileManager != null)
        {
            if (towerToSell.hasBuildGridPosition)
                tileManager.UnregisterTowerPosition(towerToSell.builtGridPosition);
            else
                tileManager.UnregisterTowerPosition(towerToSell.transform.position);
        }

        Close();
        Destroy(towerToSell.gameObject);
        Debug.Log("Tower verkauft: +" + refund + " Gold");
    }

    public void UpgradeDamage()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryGoldUpgradeDamage(gameManager);
        UpdateUI();
    }

    public void UpgradeRange()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryGoldUpgradeRange(gameManager);
        UpdateUI();
    }

    public void UpgradeFireRate()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryGoldUpgradeFireRate(gameManager);
        UpdateUI();
    }

    public void UpgradeEffectWithGold()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryGoldUpgradeEffect(gameManager);
        UpdateUI();
    }

    public void UpgradeDamageWithPoint()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryPointUpgradeDamage();
        UpdateUI();
    }

    public void UpgradeRangeWithPoint()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryPointUpgradeRange();
        UpdateUI();
    }

    public void UpgradeFireRateWithPoint()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryPointUpgradeFireRate();
        UpdateUI();
    }

    public void UpgradeEffectWithPoint()
    {
        if (selectedTower == null)
            return;

        selectedTower.TryPointUpgradeEffect();
        UpdateUI();
    }
}
