using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BlockedEventType
{
    Continue,
    GoldBonus,
    LifeBonus,
    BuildTimeBonus,
    PersistentRewardBonus,
    EvolutionPoint,
    LargeLifeBoost,
    RaiseLowTowersToLevelFive,
    ChaosResetKeepOneRisk,
    RelocateBaseTile,
    TeleporterBase
}

[System.Serializable]
public class BlockedEventOption
{
    public string displayName;
    public string description;
    public BlockedEventType eventType;

    [Header("Event Values")]
    public int goldAmount = 0;
    public int lifeAmount = 0;

    [Tooltip("Optional. Wenn > 0, nutzt diese Option eine eigene Verbau-Buildphase-Dauer.")]
    public float buildPhaseDurationOverride = 0f;
}

public class BlockedEventManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public PathBuildManager pathBuildManagerStyleSource;

    [Header("UI - Mirrors PathBuildManager")]
    public GameObject eventTopBar;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    public Button optionButton1;
    public Button optionButton2;
    public Button optionButton3;

    public TextMeshProUGUI optionText1;
    public TextMeshProUGUI optionText2;
    public TextMeshProUGUI optionText3;

    [Header("UI Layout Defaults")]
    public bool applyChoiceUILayoutDefaults = true;
    public Vector2 eventTopBarSize = new Vector2(0f, 88f);
    public float eventTopBarTopOffset = 116f;
    public float eventTopBarLeftInset = 360f;
    public float eventTopBarRightInset = 220f;
    public bool hideTitleTextForChoiceBar = true;
    public bool hideUnusedTopBarTextLabels = true;
    public Color choiceBarColor = new Color32(18, 22, 30, 245);
    public Color choiceButtonColor = new Color32(65, 95, 145, 255);
    public Color choiceDescriptionColor = new Color32(255, 220, 120, 255);
    public float choiceButtonRowSpacing = 14f;
    public bool forceChoiceButtonRectLayout = true;
    public float choiceButtonTopPadding = 6f;
    public float choiceButtonHeight = 46f;
    public float choiceDescriptionHeight = 30f;
    public float choiceDescriptionBottomPadding = 4f;
    public float choiceDescriptionFontSize = 16f;

    [Header("PathBuildManager Mirror")]
    public bool mirrorPathBuildManagerLayout = true;

    [Header("Settings")]
    public float timedBuildPhaseDuration = 60f;

    [Header("Possible Events")]
    public List<BlockedEventOption> possibleEvents = new List<BlockedEventOption>();

    private BlockedEventOption[] currentOptions = new BlockedEventOption[3];
    private bool selectionOpen = false;
    private bool riskKeepSelectionOpen = false;
    private float pendingRiskKeepBuildPhaseDuration = 0f;
    private List<string> currentRiskKeepOptions = new List<string>();

    private void Start()
    {
        ApplyChoiceUILayoutDefaults();
        CloseSelection();
        SetupButtons();
        CreateDefaultEventsIfEmpty();
    }

    private void Update()
    {
        if (!selectionOpen && !riskKeepSelectionOpen)
            return;

        if (gameManager != null && gameManager.IsChaosJusticeChoiceOpen())
            return;

        if (riskKeepSelectionOpen)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                ChooseRiskKeepOption(0);

            if (Input.GetKeyDown(KeyCode.Alpha2))
                ChooseRiskKeepOption(1);

            if (Input.GetKeyDown(KeyCode.Alpha3))
                ChooseRiskKeepOption(2);

            if (Input.GetKeyDown(KeyCode.Alpha4))
                ChooseRiskKeepOption(3);

            if (Input.GetKeyDown(KeyCode.Alpha5))
                ChooseRiskKeepOption(4);

            if (Input.GetKeyDown(KeyCode.Alpha6))
                ChooseRiskKeepOption(5);

            if (Input.GetKeyDown(KeyCode.Alpha7))
                ChooseRiskKeepOption(6);

            if (Input.GetKeyDown(KeyCode.Alpha8))
                ChooseRiskKeepOption(7);

            if (Input.GetKeyDown(KeyCode.Alpha9))
                ChooseRiskKeepOption(8);

            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ChooseOption(0);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            ChooseOption(1);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            ChooseOption(2);
    }

    private void SetupButtons()
    {
        if (optionButton1 != null)
        {
            optionButton1.onClick.RemoveAllListeners();
            optionButton1.onClick.AddListener(() => ChooseCurrentButton(0));
            SetupOptionHover(optionButton1, 0);
        }

        if (optionButton2 != null)
        {
            optionButton2.onClick.RemoveAllListeners();
            optionButton2.onClick.AddListener(() => ChooseCurrentButton(1));
            SetupOptionHover(optionButton2, 1);
        }

        if (optionButton3 != null)
        {
            optionButton3.onClick.RemoveAllListeners();
            optionButton3.onClick.AddListener(() => ChooseCurrentButton(2));
            SetupOptionHover(optionButton3, 2);
        }
    }

    private void ApplyChoiceUILayoutDefaults()
    {
        if (!applyChoiceUILayoutDefaults)
            return;

        CopyPathBuildManagerLayoutDefaults();

        if (eventTopBar != null)
        {
            RectTransform rect = eventTopBar.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2((eventTopBarLeftInset - eventTopBarRightInset) * 0.5f, -eventTopBarTopOffset);
                rect.sizeDelta = new Vector2(-(eventTopBarLeftInset + eventTopBarRightInset), eventTopBarSize.y);
            }

            Image image = eventTopBar.GetComponent<Image>();
            if (image != null)
                image.color = choiceBarColor;

            HideUnusedTopBarTextLabels();
        }

        if (titleText != null && hideTitleTextForChoiceBar)
            titleText.gameObject.SetActive(false);

        ConfigureChoiceButtonRowLayout();
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
        if (!mirrorPathBuildManagerLayout)
            return;

        if (pathBuildManagerStyleSource == null)
            pathBuildManagerStyleSource = FindObjectOfType<PathBuildManager>();

        if (pathBuildManagerStyleSource == null)
            return;

        eventTopBarSize = pathBuildManagerStyleSource.pathTopBarSize;
        eventTopBarTopOffset = pathBuildManagerStyleSource.pathTopBarTopOffset;
        eventTopBarLeftInset = pathBuildManagerStyleSource.pathTopBarLeftInset;
        eventTopBarRightInset = pathBuildManagerStyleSource.pathTopBarRightInset;
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

    private void ConfigureChoiceButtonRowLayout()
    {
        Transform row = GetChoiceButtonRowTransform();
        if (row == null)
            return;

        HorizontalLayoutGroup horizontalLayout = row.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout == null)
            horizontalLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();

        if (horizontalLayout == null)
            return;

        horizontalLayout.spacing = choiceButtonRowSpacing;
        horizontalLayout.padding = new RectOffset(0, 0, 0, 0);
        horizontalLayout.childAlignment = TextAnchor.UpperCenter;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = true;
        horizontalLayout.childForceExpandHeight = false;

        VerticalLayoutGroup verticalLayout = row.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null)
            verticalLayout.enabled = false;
    }

    private Transform GetChoiceButtonRowTransform()
    {
        if (optionButton1 == null || optionButton2 == null || optionButton3 == null)
            return null;

        Transform parent = optionButton1.transform.parent;

        if (parent != null && optionButton2.transform.parent == parent && optionButton3.transform.parent == parent)
            return parent;

        return null;
    }

    private void ApplyChoiceButtonRectLayout()
    {
        if (!forceChoiceButtonRectLayout || eventTopBar == null)
            return;

        DisableChoiceBarLayoutGroups(eventTopBar.transform);

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

            if (buttonRect.parent != eventTopBar.transform)
                buttonRect.SetParent(eventTopBar.transform, false);

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
        if (descriptionText == null || eventTopBar == null)
            return;

        RectTransform descriptionRect = descriptionText.GetComponent<RectTransform>();
        if (descriptionRect == null)
            return;

        if (descriptionRect.parent != eventTopBar.transform)
            descriptionRect.SetParent(eventTopBar.transform, false);

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
        if (!hideUnusedTopBarTextLabels || eventTopBar == null)
            return;

        TextMeshProUGUI[] labels = eventTopBar.GetComponentsInChildren<TextMeshProUGUI>(true);
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
            layout.preferredHeight = 46f;
            layout.minHeight = 42f;
        }

        if (text != null)
        {
            text.fontSize = Mathf.Max(text.fontSize, 17f);
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    private void SetupOptionHover(Button button, int optionIndex)
    {
        if (button == null)
            return;

        BlockedEventOptionHoverProxy hoverProxy = button.GetComponent<BlockedEventOptionHoverProxy>();

        if (hoverProxy == null)
            hoverProxy = button.gameObject.AddComponent<BlockedEventOptionHoverProxy>();

        hoverProxy.Initialize(this, optionIndex);
    }

    private void CreateDefaultEventsIfEmpty()
    {
        if (possibleEvents != null && possibleEvents.Count > 0)
            return;

        possibleEvents = new List<BlockedEventOption>();

        possibleEvents.Add(CreatePersistentRewardBonusOption());
        possibleEvents.Add(CreateEvolutionPointOption());
        possibleEvents.Add(CreateLargeLifeBoostOption());
        possibleEvents.Add(CreateRaiseLowTowersOption());
        possibleEvents.Add(CreateLongBuildPhaseOption());
    }

    public void OpenBlockedEventSelection()
    {
        if (selectionOpen)
            return;

        if (gameManager != null && gameManager.IsChaosJusticeChoiceOpen())
        {
            Debug.LogWarning("BlockedEventManager: Verbau-Auswahl wird nicht geöffnet, solange Chaos/Gerechtigkeit offen ist.");
            return;
        }

        if (gameManager != null)
            gameManager.ClosePathAndBuildSelectionsForModal();

        ApplyChoiceUILayoutDefaults();
        CreateDefaultEventsIfEmpty();
        GenerateOptions();
        UpdateUI();

        selectionOpen = true;

        if (eventTopBar != null)
            eventTopBar.SetActive(true);
    }

    public void CloseSelection()
    {
        selectionOpen = false;
        riskKeepSelectionOpen = false;

        if (eventTopBar != null)
            eventTopBar.SetActive(false);
    }

    private void ChooseCurrentButton(int index)
    {
        if (riskKeepSelectionOpen)
            ChooseRiskKeepOption(index);
        else
            ChooseOption(index);
    }

    private void GenerateOptions()
    {
        List<BlockedEventOption> eventPool = CreateSelectableEventPool();

        for (int i = 0; i < currentOptions.Length; i++)
            currentOptions[i] = GetRandomOptionFromPool(eventPool);
    }

    private List<BlockedEventOption> CreateSelectableEventPool()
    {
        List<BlockedEventOption> eventPool = new List<BlockedEventOption>();

        if (possibleEvents != null)
        {
            foreach (BlockedEventOption option in possibleEvents)
            {
                if (!IsSelectableEventOption(option))
                    continue;

                AddOptionIfUnique(eventPool, option);
            }
        }

        AddBuiltInBlockedRewardOptions(eventPool);
        AddFallbackOptionsUntilReady(eventPool);
        return eventPool;
    }

    private void AddBuiltInBlockedRewardOptions(List<BlockedEventOption> eventPool)
    {
        AddOptionIfUnique(eventPool, CreatePersistentRewardBonusOption());
        AddOptionIfUnique(eventPool, CreateEvolutionPointOption());
        AddOptionIfUnique(eventPool, CreateLargeLifeBoostOption());
        AddOptionIfUnique(eventPool, CreateRaiseLowTowersOption());
        AddOptionIfUnique(eventPool, CreateChaosResetKeepOneRiskOption());
        AddOptionIfUnique(eventPool, CreateRelocateBaseTileOption());
        AddOptionIfUnique(eventPool, CreateTeleporterBaseOption());
    }

    private bool IsSelectableEventOption(BlockedEventOption option)
    {
        if (option == null)
            return false;

        // "Weiter" ist in Phase 7 keine auswählbare Verbau-Option mehr.
        return option.eventType != BlockedEventType.Continue;
    }

    private void AddFallbackOptionsUntilReady(List<BlockedEventOption> eventPool)
    {
        List<BlockedEventOption> fallbackOptions = CreateFallbackOptions();

        foreach (BlockedEventOption fallbackOption in fallbackOptions)
        {
            if (eventPool.Count >= currentOptions.Length)
                return;

            AddOptionIfUnique(eventPool, fallbackOption);
        }
    }

    private List<BlockedEventOption> CreateFallbackOptions()
    {
        return new List<BlockedEventOption>
        {
            CreatePersistentRewardBonusOption(),
            CreateRelocateBaseTileOption(),
            CreateLongBuildPhaseOption()
        };
    }

    private void AddOptionIfUnique(List<BlockedEventOption> options, BlockedEventOption option)
    {
        if (options == null || option == null)
            return;

        foreach (BlockedEventOption existingOption in options)
        {
            if (IsSameOption(existingOption, option))
                return;
        }

        options.Add(option);
    }

    private bool IsSameOption(BlockedEventOption first, BlockedEventOption second)
    {
        if (first == null || second == null)
            return false;

        return first.eventType == second.eventType &&
               first.displayName == second.displayName &&
               first.goldAmount == second.goldAmount &&
               first.lifeAmount == second.lifeAmount &&
               Mathf.Approximately(first.buildPhaseDurationOverride, second.buildPhaseDurationOverride);
    }

    private BlockedEventOption GetRandomOptionFromPool(List<BlockedEventOption> eventPool)
    {
        if (eventPool == null || eventPool.Count == 0)
            return CreateLongBuildPhaseOption();

        int randomIndex = Random.Range(0, eventPool.Count);
        BlockedEventOption option = eventPool[randomIndex];
        eventPool.RemoveAt(randomIndex);

        return option;
    }

    private BlockedEventOption CreatePersistentRewardBonusOption()
    {
        return new BlockedEventOption
        {
            displayName = "Weg-Kompensation",
            description = "Ab jetzt geben Gold und XP dauerhaft +1%. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.PersistentRewardBonus
        };
    }

    private BlockedEventOption CreateEvolutionPointOption()
    {
        return new BlockedEventOption
        {
            displayName = "Evolutionspunkt",
            description = "Der nächste angeklickte Tower erhält +50% aktuelle Werte. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.EvolutionPoint
        };
    }

    private BlockedEventOption CreateLargeLifeBoostOption()
    {
        return new BlockedEventOption
        {
            displayName = "Lebensreserve",
            description = "Du erhältst +50 Leben. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.LargeLifeBoost,
            lifeAmount = 50
        };
    }

    private BlockedEventOption CreateRaiseLowTowersOption()
    {
        return new BlockedEventOption
        {
            displayName = "Nachschulung",
            description = "Alle Tower unter Level 5 werden sofort Level 5. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.RaiseLowTowersToLevelFive
        };
    }

    private BlockedEventOption CreateChaosResetKeepOneRiskOption()
    {
        return new BlockedEventOption
        {
            displayName = "Chaos ordnen",
            description = "Chaos wird auf 1 gesetzt. Du behältst genau einen aktiven Risiko-Modifikator.",
            eventType = BlockedEventType.ChaosResetKeepOneRisk
        };
    }

    private BlockedEventOption CreateRelocateBaseTileOption()
    {
        return new BlockedEventOption
        {
            displayName = "Neue Basis",
            description = "Setze die Basis auf ein gültiges Build-Feld am Weg. Der alte Wegschwanz samt nahen Towern wird entfernt.",
            eventType = BlockedEventType.RelocateBaseTile
        };
    }

    private BlockedEventOption CreateTeleporterBaseOption()
    {
        return new BlockedEventOption
        {
            displayName = "Teleporter",
            description = "Eine neue Basis erscheint zufällig nahe am Weg. Ein Teleporter verbindet alte und neue Basis dauerhaft.",
            eventType = BlockedEventType.TeleporterBase
        };
    }

    private BlockedEventOption CreateGoldReserveOption()
    {
        return new BlockedEventOption
        {
            displayName = "Goldreserve",
            description = "Du erhältst 100 Gold. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.GoldBonus,
            goldAmount = 100
        };
    }

    private BlockedEventOption CreateLifeRepairOption()
    {
        return new BlockedEventOption
        {
            displayName = "Notfall-Reparatur",
            description = "Du erhältst 3 Leben. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.LifeBonus,
            lifeAmount = 3
        };
    }

    private BlockedEventOption CreateLongBuildPhaseOption()
    {
        float safeDuration = Mathf.Max(timedBuildPhaseDuration, 90f);

        return new BlockedEventOption
        {
            displayName = "Baupause",
            description = "Keine Sofortbelohnung. Du erhältst " + FormatBuildPhaseDurationFor(safeDuration) + " Buildphase.",
            eventType = BlockedEventType.BuildTimeBonus,
            buildPhaseDurationOverride = safeDuration
        };
    }

    private string GetTimedBuildPhaseText()
    {
        return "hast du " + FormatBuildPhaseDuration() + " Buildphase";
    }

    private string FormatBuildPhaseDuration()
    {
        return FormatBuildPhaseDurationFor(timedBuildPhaseDuration);
    }

    private string FormatBuildPhaseDurationFor(float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);

        if (Mathf.Approximately(safeDuration, Mathf.Round(safeDuration)))
            return Mathf.RoundToInt(safeDuration) + " Sekunden";

        return safeDuration.ToString("0.0") + " Sekunden";
    }

    private void UpdateUI()
    {
        if (titleText != null)
        {
            titleText.text = "VERBAUT!";
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

        if (currentOptions[index] == null)
        {
            textField.text = "[" + (index + 1) + "] Fehlerhafte Option";
            return;
        }

        textField.text = "[" + (index + 1) + "] " + currentOptions[index].displayName;
    }

    public void ShowOptionDescription(int optionIndex)
    {
        if (descriptionText == null)
            return;

        if (riskKeepSelectionOpen)
        {
            ShowRiskKeepOptionDescription(optionIndex);
            return;
        }

        if (!selectionOpen)
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
        if (descriptionText == null)
            return;

        if (riskKeepSelectionOpen)
        {
            descriptionText.text = BuildRiskKeepDescriptionText();
            return;
        }

        descriptionText.text = "Wähle eine Verbau-Option. Hover zeigt den Effekt.";
    }

    private void ChooseOption(int index)
    {
        if (!selectionOpen)
            return;

        if (gameManager != null && (gameManager.isGameOver || gameManager.IsChaosJusticeChoiceOpen()))
            return;

        if (index < 0 || index >= currentOptions.Length)
            return;

        BlockedEventOption option = currentOptions[index];

        if (option == null)
            return;

        CloseSelection();
        ApplyEvent(option);
    }


    public bool IsSelectionOpen()
    {
        return selectionOpen || riskKeepSelectionOpen;
    }

    private void ApplyEvent(BlockedEventOption option)
    {
        if (gameManager == null)
        {
            Debug.LogError("GameManager fehlt im BlockedEventManager!");
            return;
        }

        int appliedGold = 0;
        int appliedLives = 0;
        float buildPhaseDuration = GetBuildPhaseDurationForOption(option);

        switch (option.eventType)
        {
            case BlockedEventType.GoldBonus:
                appliedGold = Mathf.Max(0, option.goldAmount);
                gameManager.AddGold(appliedGold, true, RunGoldSource.BlockedEvent);
                Debug.Log("Blocked Event gewählt: Gold +" + appliedGold);
                break;

            case BlockedEventType.LifeBonus:
                appliedLives = Mathf.Max(0, option.lifeAmount);
                gameManager.AddLives(appliedLives);
                Debug.Log("Blocked Event gewählt: Leben +" + appliedLives);
                break;

            case BlockedEventType.BuildTimeBonus:
                Debug.Log("Blocked Event gewählt: Bauzeit " + FormatBuildPhaseDurationFor(buildPhaseDuration));
                break;

            case BlockedEventType.PersistentRewardBonus:
                gameManager.AddBlockedEventRewardBonusStack();
                Debug.Log("Blocked Event gewählt: dauerhafter Gold/XP-Bonus +1%.");
                break;

            case BlockedEventType.EvolutionPoint:
                gameManager.AddEvolutionTowerBoost();
                Debug.Log("Blocked Event gewählt: Evolutionspunkt.");
                break;

            case BlockedEventType.LargeLifeBoost:
                appliedLives = Mathf.Max(0, option.lifeAmount);
                gameManager.AddLives(appliedLives);
                Debug.Log("Blocked Event gewählt: Leben +" + appliedLives);
                break;

            case BlockedEventType.RaiseLowTowersToLevelFive:
                gameManager.RaiseLowTowersToLevelFive();
                Debug.Log("Blocked Event gewählt: Tower unter Level 5 angehoben.");
                break;

            case BlockedEventType.ChaosResetKeepOneRisk:
                OpenRiskKeepSelection(option, buildPhaseDuration);
                return;

            case BlockedEventType.RelocateBaseTile:
                gameManager.RegisterBlockedEventChoice(option.displayName, option.eventType.ToString(), appliedGold, appliedLives, buildPhaseDuration);
                gameManager.MarkBlockedEventChosenForCurrentPosition();
                gameManager.BeginBlockedBaseRelocation(buildPhaseDuration);
                Debug.Log("Blocked Event gewählt: Neue Basis platzieren.");
                return;

            case BlockedEventType.TeleporterBase:
                if (!gameManager.TryCreateBlockedTeleporterBase())
                {
                    gameManager.AddLives(10);
                    appliedLives = 10;
                    Debug.LogWarning("Teleporter fehlgeschlagen; sichere Ersatzbelohnung +10 Leben.");
                }
                else
                {
                    Debug.Log("Blocked Event gewählt: Teleporter-Basis erstellt.");
                }
                break;

            case BlockedEventType.Continue:
                Debug.LogWarning("BlockedEventManager: Continue ist keine auswählbare Verbau-Option mehr und wurde nur als Fallback verarbeitet.");
                break;
        }

        gameManager.RegisterBlockedEventChoice(option.displayName, option.eventType.ToString(), appliedGold, appliedLives, buildPhaseDuration);
        gameManager.MarkBlockedEventChosenForCurrentPosition();
        gameManager.StartTimedBuildPhaseAfterBlockedEvent(buildPhaseDuration);
    }


    private void OpenRiskKeepSelection(BlockedEventOption sourceOption, float buildPhaseDuration)
    {
        if (gameManager == null)
            return;

        currentRiskKeepOptions = gameManager.GetActiveRiskModifierDisplayNames();

        if (currentRiskKeepOptions == null || currentRiskKeepOptions.Count == 0)
        {
            gameManager.ResetChaosToOneKeepingRiskModifier(-1);
            gameManager.RegisterBlockedEventChoice(sourceOption.displayName, sourceOption.eventType.ToString(), 0, 0, buildPhaseDuration);
            gameManager.MarkBlockedEventChosenForCurrentPosition();
            gameManager.StartTimedBuildPhaseAfterBlockedEvent(buildPhaseDuration);
            return;
        }

        pendingRiskKeepBuildPhaseDuration = buildPhaseDuration;
        riskKeepSelectionOpen = true;
        selectionOpen = false;
        UpdateRiskKeepUI();

        if (eventTopBar != null)
            eventTopBar.SetActive(true);
    }

    private void UpdateRiskKeepUI()
    {
        ApplyChoiceUILayoutDefaults();

        if (titleText != null)
        {
            titleText.text = "RISIKO BEHALTEN";
            titleText.gameObject.SetActive(!hideTitleTextForChoiceBar);
        }

        ShowDefaultChoiceDescription();

        SetRiskKeepText(optionText1, 0);
        SetRiskKeepText(optionText2, 1);
        SetRiskKeepText(optionText3, 2);
    }

    private string BuildRiskKeepDescriptionText()
    {
        string text = "Wähle, welcher Risiko-Modifikator aktiv bleibt. Chaos wird auf 1 gesetzt.";

        if (currentRiskKeepOptions == null || currentRiskKeepOptions.Count == 0)
            return text;

        text += "\n";

        for (int i = 0; i < currentRiskKeepOptions.Count; i++)
        {
            if (i >= 9)
            {
                text += "\nWeitere Risiken werden entfernt.";
                break;
            }

            text += "\n[" + (i + 1) + "] " + currentRiskKeepOptions[i];
        }

        return text;
    }

    private void SetRiskKeepText(TextMeshProUGUI textField, int index)
    {
        if (textField == null)
            return;

        if (currentRiskKeepOptions == null || index >= currentRiskKeepOptions.Count)
        {
            textField.text = "[" + (index + 1) + "] Keine weitere Auswahl";
            return;
        }

        textField.text = "[" + (index + 1) + "] " + currentRiskKeepOptions[index];
    }

    private void ShowRiskKeepOptionDescription(int optionIndex)
    {
        if (descriptionText == null)
            return;

        if (currentRiskKeepOptions == null || optionIndex < 0 || optionIndex >= currentRiskKeepOptions.Count)
        {
            ShowDefaultChoiceDescription();
            return;
        }

        descriptionText.text = currentRiskKeepOptions[optionIndex] + " bleibt aktiv. Chaos wird auf 1 gesetzt; alle anderen Risiko-Modifikatoren werden entfernt.";
    }

    private void ChooseRiskKeepOption(int index)
    {
        if (!riskKeepSelectionOpen || gameManager == null)
            return;

        if (currentRiskKeepOptions == null || index < 0 || index >= currentRiskKeepOptions.Count)
            return;

        riskKeepSelectionOpen = false;
        CloseSelection();
        gameManager.ResetChaosToOneKeepingRiskModifier(index);
        gameManager.RegisterBlockedEventChoice("Chaos ordnen", BlockedEventType.ChaosResetKeepOneRisk.ToString(), 0, 0, pendingRiskKeepBuildPhaseDuration);
        gameManager.MarkBlockedEventChosenForCurrentPosition();
        gameManager.StartTimedBuildPhaseAfterBlockedEvent(pendingRiskKeepBuildPhaseDuration);
    }

    private float GetBuildPhaseDurationForOption(BlockedEventOption option)
    {
        if (option != null && option.buildPhaseDurationOverride > 0f)
            return option.buildPhaseDurationOverride;

        return timedBuildPhaseDuration;
    }

}

public class BlockedEventOptionHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private BlockedEventManager owner;
    private int optionIndex;

    public void Initialize(BlockedEventManager newOwner, int newOptionIndex)
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
