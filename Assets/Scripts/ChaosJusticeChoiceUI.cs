using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChaosJusticeChoiceUI : MonoBehaviour
{
    [Header("References")]
    public ChaosJusticeManager manager;

    [Header("UI")]
    public GameObject choiceTopBar;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    public Button optionButton1;
    public Button optionButton2;
    public Button optionButton3;
    public Button optionButton4;

    public TextMeshProUGUI optionText1;
    public TextMeshProUGUI optionText2;
    public TextMeshProUGUI optionText3;
    public TextMeshProUGUI optionText4;

    [Header("Dynamic Layout")]
    public bool hideUnusedOptionButtons = true;
    public bool applyModalLayoutDefaults = true;
    public Vector2 choiceTitlePosition = new Vector2(0f, -58f);
    public Vector2 choiceDescriptionPosition = new Vector2(0f, -108f);
    public Vector2 choiceButtonSize = new Vector2(270f, 250f);
    public float choiceButtonY = -98f;
    public float choiceButtonSpacing = 24f;

    [Header("Theme")]
    public bool applyThemeOnStart = true;
    public Color overlayColor = new Color32(0, 0, 0, 120);
    public Color windowColor = new Color32(20, 24, 31, 245);
    public Color titleColor = new Color32(240, 244, 250, 255);
    public Color descriptionColor = new Color32(185, 194, 208, 255);
    public Color goldButtonColor = new Color32(214, 164, 65, 255);
    public Color xpButtonColor = new Color32(65, 125, 245, 255);
    public Color chaosButtonColor = new Color32(185, 70, 95, 255);
    public Color noModifierButtonColor = new Color32(75, 85, 105, 255);
    public Color backButtonColor = new Color32(55, 64, 80, 255);
    public Color disabledButtonColor = new Color32(55, 64, 80, 255);
    public Color optionTextColor = Color.white;
    public Color optionDescriptionColor = new Color32(228, 235, 245, 255);

    [Header("Text Sizes")]
    public float titleFontSize = 30f;
    public float descriptionFontSize = 16f;
    public float optionFontSize = 15f;
    public float optionTitleFontSize = 18f;

    private readonly List<ChaosJusticeChoiceOption> currentOptions = new List<ChaosJusticeChoiceOption>();

    private void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<ChaosJusticeManager>();

        if (manager != null)
            manager.choiceUI = this;

        SetupButtons();

        if (applyThemeOnStart)
            ApplyStaticTheme();

        CloseSelection();
    }

    public void Connect(ChaosJusticeManager newManager)
    {
        manager = newManager;
        SetupButtons();

        if (applyThemeOnStart)
            ApplyStaticTheme();

        CloseSelection();
    }

    private void SetupButtons()
    {
        SetupButton(optionButton1, 0);
        SetupButton(optionButton2, 1);
        SetupButton(optionButton3, 2);
        SetupButton(optionButton4, 3);
    }

    private void SetupButton(Button button, int index)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ChooseOption(index));
    }

    public void OpenSelection(List<ChaosJusticeChoiceOption> options)
    {
        currentOptions.Clear();

        if (options != null)
            currentOptions.AddRange(options);

        if (applyThemeOnStart)
            ApplyStaticTheme();

        if (applyModalLayoutDefaults)
            ApplyModalLayoutDefaults();

        UpdateUI();

        if (choiceTopBar != null)
            choiceTopBar.SetActive(true);
    }

    public void CloseSelection()
    {
        if (choiceTopBar != null)
            choiceTopBar.SetActive(false);
    }

    private void ChooseOption(int index)
    {
        if (manager == null)
            return;

        manager.ChooseOption(index);
    }

    private void UpdateUI()
    {
        if (titleText != null)
            titleText.text = manager != null ? manager.GetChoiceTitleText() : "BOSS-WAVE ABGESCHLOSSEN";

        if (descriptionText != null)
        {
            descriptionText.text = manager != null
                ? manager.GetChoiceDescriptionText()
                : "Wähle einen sicheren Gerechtigkeits-Bonus oder erhöhe bewusst das Risiko. Alle Risiken sind offen sichtbar; Boss und V1-Chaos zerstören keine Tower.";
        }

        SetOptionUI(optionButton1, optionText1, 0, GetOptionColor(0));
        SetOptionUI(optionButton2, optionText2, 1, GetOptionColor(1));
        SetOptionUI(optionButton3, optionText3, 2, GetOptionColor(2));
        SetOptionUI(optionButton4, optionText4, 3, GetOptionColor(3));
    }

    private Color GetOptionColor(int index)
    {
        ChaosJusticeChoiceOption option = GetOption(index);

        if (option == null)
            return disabledButtonColor;

        switch (option.choiceType)
        {
            case ChaosJusticeChoiceType.OpenJusticeSubChoice:
            case ChaosJusticeChoiceType.GoldJustice:
                return goldButtonColor;

            case ChaosJusticeChoiceType.XpJustice:
                return xpButtonColor;

            case ChaosJusticeChoiceType.OpenChaosSubChoice:
            case ChaosJusticeChoiceType.ChaosRiskModifier:
                return chaosButtonColor;

            case ChaosJusticeChoiceType.NoRiskModifier:
                return noModifierButtonColor;

            case ChaosJusticeChoiceType.BackToMainChoice:
                return backButtonColor;

            default:
                return disabledButtonColor;
        }
    }

    private void SetOptionUI(Button button, TextMeshProUGUI textField, int index, Color activeColor)
    {
        ChaosJusticeChoiceOption option = GetOption(index);
        bool hasOption = option != null;
        bool isEnabled = hasOption && option.isEnabled;

        if (button != null)
        {
            if (hideUnusedOptionButtons)
                button.gameObject.SetActive(hasOption);

            button.interactable = isEnabled;
            ApplyButtonStyle(button, isEnabled ? activeColor : disabledButtonColor, isEnabled);
        }

        if (textField == null)
            return;

        if (hideUnusedOptionButtons && !hasOption)
        {
            textField.text = "";
            return;
        }

        ApplyOptionTextStyle(textField);

        if (option == null)
        {
            textField.text = "<b>[" + (index + 1) + "] KEINE OPTION</b>";
            return;
        }

        string disabledSuffix = option.isEnabled ? "" : "\n<size=14><color=#B0B8C8>Nicht verfügbar</color></size>";
        string optionTitle = "[" + (index + 1) + "] " + option.displayName.ToUpperInvariant();
        string safeDescription = string.IsNullOrEmpty(option.description) ? "Keine Beschreibung gesetzt." : option.description;

        textField.text =
            "<size=" + Mathf.RoundToInt(optionTitleFontSize) + "><b>" + optionTitle + "</b></size>" +
            "\n<size=" + Mathf.RoundToInt(optionFontSize) + "><color=#" + ColorUtility.ToHtmlStringRGB(optionDescriptionColor) + ">" + safeDescription + "</color></size>" +
            disabledSuffix;
    }

    private ChaosJusticeChoiceOption GetOption(int index)
    {
        if (index < 0 || index >= currentOptions.Count)
            return null;

        return currentOptions[index];
    }

    private void ApplyStaticTheme()
    {
        ApplyPanelImageColor(choiceTopBar, overlayColor);

        ApplyHeaderTextStyle(titleText, titleFontSize, titleColor, TextAlignmentOptions.Center);
        ApplyHeaderTextStyle(descriptionText, descriptionFontSize, descriptionColor, TextAlignmentOptions.Center);

        ApplyOptionTextStyle(optionText1);
        ApplyOptionTextStyle(optionText2);
        ApplyOptionTextStyle(optionText3);
        ApplyOptionTextStyle(optionText4);

        ApplyButtonStyle(optionButton1, goldButtonColor, true);
        ApplyButtonStyle(optionButton2, xpButtonColor, true);
        ApplyButtonStyle(optionButton3, chaosButtonColor, true);
        ApplyButtonStyle(optionButton4, noModifierButtonColor, true);

        if (applyModalLayoutDefaults)
            ApplyModalLayoutDefaults();
    }

    private void ApplyModalLayoutDefaults()
    {
        ApplyHeaderRect(titleText, choiceTitlePosition, new Vector2(1040f, 48f));
        ApplyHeaderRect(descriptionText, choiceDescriptionPosition, new Vector2(1040f, 42f));

        int visibleCount = Mathf.Max(1, currentOptions.Count > 0 ? currentOptions.Count : 4);
        float totalWidth = visibleCount * choiceButtonSize.x + (visibleCount - 1) * choiceButtonSpacing;
        ApplyOptionButtonRect(optionButton1, 0, visibleCount, totalWidth);
        ApplyOptionButtonRect(optionButton2, 1, visibleCount, totalWidth);
        ApplyOptionButtonRect(optionButton3, 2, visibleCount, totalWidth);
        ApplyOptionButtonRect(optionButton4, 3, visibleCount, totalWidth);
    }

    private void ApplyHeaderRect(TextMeshProUGUI textField, Vector2 anchoredPosition, Vector2 size)
    {
        if (textField == null)
            return;

        RectTransform rect = textField.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void ApplyOptionButtonRect(Button button, int index, int visibleCount, float totalWidth)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = choiceButtonSize;
        rect.anchoredPosition = new Vector2(-totalWidth * 0.5f + choiceButtonSize.x * 0.5f + index * (choiceButtonSize.x + choiceButtonSpacing), choiceButtonY);
    }

    private void ApplyPanelImageColor(GameObject targetObject, Color color)
    {
        if (targetObject == null)
            return;

        Image image = targetObject.GetComponent<Image>();

        if (image != null)
            image.color = color;
    }

    private void ApplyHeaderTextStyle(TextMeshProUGUI textField, float size, Color color, TextAlignmentOptions alignment)
    {
        if (textField == null)
            return;

        textField.richText = true;
        textField.enableWordWrapping = true;
        textField.overflowMode = TextOverflowModes.Overflow;
        textField.fontSize = size;
        textField.color = color;
        textField.alignment = alignment;
    }

    private void ApplyOptionTextStyle(TextMeshProUGUI textField)
    {
        if (textField == null)
            return;

        textField.richText = true;
        textField.enableWordWrapping = true;
        textField.overflowMode = TextOverflowModes.Overflow;
        textField.fontSize = optionFontSize;
        textField.color = optionTextColor;
        textField.alignment = TextAlignmentOptions.TopLeft;
        textField.margin = new Vector4(18f, 14f, 18f, 10f);
    }

    private void ApplyButtonStyle(Button button, Color baseColor, bool interactable)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();

        if (image != null)
        {
            image.color = baseColor;
            button.targetGraphic = image;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = interactable ? LightenColor(baseColor, 1.12f) : baseColor;
        colors.pressedColor = interactable ? DarkenColor(baseColor, 0.82f) : baseColor;
        colors.selectedColor = baseColor;
        colors.disabledColor = disabledButtonColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private Color LightenColor(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }

    private Color DarkenColor(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }
}
