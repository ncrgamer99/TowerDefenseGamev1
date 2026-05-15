using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class TowerSelectionSlot
{
    [Header("Build Option")]
    public BuildOption option;

    [Header("UI")]
    public Button button;
    public Image iconImage;
}

public class BuildSelectionUI : MonoBehaviour
{
    [Header("References")]
    public BuildManager buildManager;

    [Header("Open / Close")]
    public Button openSelectionButton;
    public Button closeSelectionButton;
    public GameObject selectionPanel;

    [Header("Selected Window")]
    public GameObject selectedWindow;
    public TextMeshProUGUI selectedText;

    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipTitleText;
    public TextMeshProUGUI tooltipDescriptionText;

    [Header("Tower Slots")]
    public List<TowerSelectionSlot> towerSlots = new List<TowerSelectionSlot>();

    [Header("Default Build Option Values")]
    public bool autoApplyBuildOptionDefaults = true;

    [Header("Build Options - Legacy Buttons / Hotkeys")]
    public BuildOption basicTower;
    public BuildOption rapidTower;
    public BuildOption heavyTower;
    public BuildOption fireTower;
    public BuildOption slowTower;
    public BuildOption poisonTower;
    public BuildOption sniperTower;
    public BuildOption alchemistTower;

    [Header("New Tower Runtime Slots")]
    public bool autoAddNewTowerSlots = true;
    public bool autoCreateMissingSlotButtons = true;
    public Transform autoCreatedSlotParent;
    public GameObject slotButtonPrefab;

    [Header("Input QoL")]
    public bool closeWithRightClick = true;
    public bool rightClickAlsoClearsCurrentBuildSelection = true;
    public bool closeWithEscape = true;

    [Header("Auto Layout / Repair V1")]
    public bool autoRepairSlotLayoutOnStart = true;
    public bool forceIconInsideButton = true;
    public bool createMissingIconImages = true;
    public bool createSlotNameLabels = true;
    public bool showSlotCostLabels = true;
    public bool hideSelectionTitleAndCloseButton = true;
    public bool autoRepairSlotGridLayout = true;
    public int slotGridColumns = 6;
    public Vector2 slotGridSpacing = new Vector2(5f, 4f);
    public Vector2 slotSize = new Vector2(60f, 68f);
    public Vector2 iconSize = new Vector2(30f, 30f);
    public Vector2 labelSize = new Vector2(64f, 14f);
    public Vector2 costLabelSize = new Vector2(64f, 13f);
    public float labelYOffset = -14f;
    public float costLabelYOffset = -27f;
    public float slotIconYOffset = 11f;
    public bool preserveIconAspect = true;

    [Header("Top Right Layout QoL")]
    public bool autoPlaceSelectedWindowBelowUtilityButtons = true;
    public Vector2 selectedWindowTopRightPosition = new Vector2(-170f, -215f);

    [Header("Theme Colors")]
    public Color panelColor = new Color32(20, 24, 31, 245);
    public Color cardColor = new Color32(22, 39, 60, 255);
    public Color cardAltColor = new Color32(35, 45, 64, 255);
    public Color cardHoverColor = new Color32(31, 55, 82, 255);
    public Color cardSelectedColor = new Color32(37, 86, 120, 255);
    public Color iconFrameColor = new Color32(65, 150, 200, 255);
    public Color headerColor = new Color32(90, 185, 226, 255);
    public Color accentColor = new Color32(214, 164, 65, 255);
    public Color selectedWindowColor = new Color32(22, 39, 60, 245);
    public Color textPrimaryColor = new Color32(240, 244, 250, 255);
    public Color textSecondaryColor = new Color32(185, 194, 208, 255);

    [Header("Optional Theme Images")]
    public Image selectionPanelBackground;
    public Image selectedWindowBackground;
    public Image tooltipBackground;
    public Image openButtonBackground;
    public Image closeButtonBackground;

    private bool selectionOpen = false;
    private BuildOption selectedOption;

    private void Start()
    {
        ResolveReferences();
        ApplyBuildOptionDefaultsIfEnabled();
        SetupButtons();
        ApplyTheme();
        ApplyCompactSelectionHeaderIfNeeded();
        RepairAllSlotLayoutsIfNeeded();
        CloseSelectionPanel();
        HideTooltip();
        ClearSelectionText();
    }

    private void Update()
    {
        if (closeWithEscape && selectionOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTowerBuildSelection();
            return;
        }

        if (!closeWithRightClick)
            return;

        if (!Input.GetMouseButtonDown(1))
            return;

        bool hasActiveBuildSelection = buildManager != null && buildManager.selectedBuildOption != null;
        bool selectedWindowVisible = selectedWindow != null && selectedWindow.activeSelf;

        if (selectionOpen || hasActiveBuildSelection || selectedWindowVisible)
            CancelTowerBuildSelection();
    }

    private void ResolveReferences()
    {
        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();
    }

    private void ApplyBuildOptionDefaultsIfEnabled()
    {
        if (!autoApplyBuildOptionDefaults)
            return;

        ApplyDefaultBuildOption(basicTower, "Basic Tower", 50, "Günstiger Allrounder für den Spielstart.");
        ApplyDefaultBuildOption(slowTower, "Slow Tower", 55, "Kontrolltower, der Gegner verlangsamt und andere Tower stärker macht.");
        ApplyDefaultBuildOption(poisonTower, "Poison Tower", 70, "DoT-Tower gegen Tanks, MiniBoss und Boss.");
        ApplyDefaultBuildOption(rapidTower, "Rapid Tower", 65, "Schneller Tower gegen Runner und zum Aufräumen angeschlagener Gegner.");
        ApplyDefaultBuildOption(heavyTower, "Heavy Tower", 95, "Langsamer Einzelschaden gegen Armor, Tanks und Bosse.");
        ApplyDefaultBuildOption(fireTower, "Fire Tower", 80, "Burn-Tower gegen Gruppen und Standard-Gegner.");
        ApplyDefaultBuildOption(sniperTower, "Sniper Tower", 120, "Sehr hohe Reichweite und Einzelschaden gegen Elite, MiniBoss und Boss.");
        ApplyDefaultBuildOption(alchemistTower, "Alchemist Tower", 90, "Hybrid-Tower: vergiftet Gegner und verlangsamt sie kurz.");

        EnsureDefaultNewTowerSlotsIfEnabled();

        if (towerSlots == null)
            return;

        foreach (TowerSelectionSlot slot in towerSlots)
        {
            if (slot == null || slot.option == null)
                continue;

            ApplyDefaultBuildOptionByName(slot.option);
        }
    }

    private void ApplyDefaultBuildOptionByName(BuildOption option)
    {
        if (option == null)
            return;

        string lowerName = string.IsNullOrEmpty(option.displayName) ? "" : option.displayName.ToLowerInvariant();

        if (lowerName.Contains("basic"))
            ApplyDefaultBuildOption(option, "Basic Tower", 50, "Günstiger Allrounder für den Spielstart.");
        else if (lowerName.Contains("slow"))
            ApplyDefaultBuildOption(option, "Slow Tower", 55, "Kontrolltower, der Gegner verlangsamt und andere Tower stärker macht.");
        else if (lowerName.Contains("poison"))
            ApplyDefaultBuildOption(option, "Poison Tower", 70, "DoT-Tower gegen Tanks, MiniBoss und Boss.");
        else if (lowerName.Contains("rapid"))
            ApplyDefaultBuildOption(option, "Rapid Tower", 65, "Schneller Tower gegen Runner und zum Aufräumen angeschlagener Gegner.");
        else if (lowerName.Contains("sniper"))
            ApplyDefaultBuildOption(option, "Sniper Tower", 120, "Sehr hohe Reichweite und Einzelschaden gegen Elite, MiniBoss und Boss.");
        else if (lowerName.Contains("heavy"))
            ApplyDefaultBuildOption(option, "Heavy Tower", 95, "Langsamer Einzelschaden gegen Armor, Tanks und Bosse.");
        else if (lowerName.Contains("alchemist"))
            ApplyDefaultBuildOption(option, "Alchemist Tower", 90, "Hybrid-Tower: vergiftet Gegner und verlangsamt sie kurz.");
        else if (lowerName.Contains("lightning"))
            ApplyDefaultBuildOption(option, "Lightning Tower", 110, "Kettenblitz-Tower: Treffer und Chains verlangsamen Gegner kurz.");
        else if (lowerName.Contains("mortar"))
            ApplyDefaultBuildOption(option, "Mortar Tower", 130, "Langsamer Mörser: Projektil schlägt an Zielposition ein und verursacht AOE-Schaden.");
        else if (lowerName.Contains("spike"))
            ApplyDefaultBuildOption(option, "Spike Tower", 85, "Kurze Reichweite: Treffer bluten und hinterlassen einmalige Stacheln auf dem Weg.");
        else if (lowerName.Contains("fire"))
            ApplyDefaultBuildOption(option, "Fire Tower", 80, "Burn-Tower gegen Gruppen und Standard-Gegner.");
    }

    private void ApplyDefaultBuildOption(BuildOption option, string displayName, int cost, string description)
    {
        if (option == null)
            return;

        option.displayName = displayName;
        option.cost = cost;
        option.placementType = PlacementType.BuildTile;

        if (string.IsNullOrEmpty(option.description))
            option.description = description;

        if (option.icon == null)
            option.icon = LoadTowerIcon(displayName);
    }

    private Sprite LoadTowerIcon(string displayName)
    {
        string resourceName = GetTowerIconResourceName(displayName);

        if (string.IsNullOrEmpty(resourceName))
            return null;

        Sprite sprite = Resources.Load<Sprite>("TowerIcons/" + resourceName);

        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>("TowerIcons/" + resourceName);

        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private string GetTowerIconResourceName(string displayName)
    {
        string lower = string.IsNullOrEmpty(displayName) ? "" : displayName.ToLowerInvariant();

        if (lower.Contains("basic")) return "BasicTower";
        if (lower.Contains("rapid")) return "RapidTower";
        if (lower.Contains("lightning")) return "RapidTower";
        if (lower.Contains("mortar")) return "HeavyTower";
        if (lower.Contains("spike")) return "PoisonTower";
        if (lower.Contains("sniper")) return "HeavyTower";
        if (lower.Contains("heavy")) return "HeavyTower";
        if (lower.Contains("alchemist")) return "PoisonTower";
        if (lower.Contains("fire")) return "FireTower";
        if (lower.Contains("slow")) return "SlowTower";
        if (lower.Contains("poison")) return "PoisonTower";

        return "";
    }

    private void SetupButtons()
    {
        if (openSelectionButton != null)
        {
            openSelectionButton.onClick.RemoveAllListeners();
            openSelectionButton.onClick.AddListener(ToggleSelectionPanel);
        }

        if (closeSelectionButton != null)
        {
            closeSelectionButton.onClick.RemoveAllListeners();
            closeSelectionButton.onClick.AddListener(CancelTowerBuildSelection);

            if (hideSelectionTitleAndCloseButton)
                closeSelectionButton.gameObject.SetActive(false);
        }

        EnsureLegacySlotsIfNeeded();

        foreach (TowerSelectionSlot slot in towerSlots)
            SetupSlot(slot);
    }

    private void EnsureLegacySlotsIfNeeded()
    {
        if (towerSlots != null && towerSlots.Count > 0)
            return;

        towerSlots = new List<TowerSelectionSlot>();

        TryAddLegacySlot(basicTower);
        TryAddLegacySlot(slowTower);
        TryAddLegacySlot(poisonTower);
        TryAddLegacySlot(rapidTower);
        TryAddLegacySlot(heavyTower);
        TryAddLegacySlot(fireTower);
        TryAddLegacySlot(sniperTower);
        TryAddLegacySlot(alchemistTower);
    }

    private void TryAddLegacySlot(BuildOption option)
    {
        if (option == null)
            return;

        towerSlots.Add(new TowerSelectionSlot
        {
            option = option,
            button = null,
            iconImage = null
        });
    }

    private void EnsureDefaultNewTowerSlotsIfEnabled()
    {
        if (!autoAddNewTowerSlots)
            return;

        if (towerSlots == null)
            towerSlots = new List<TowerSelectionSlot>();

        EnsureDefaultNewTowerSlot("Sniper Tower", 120, "Sehr hohe Reichweite und Einzelschaden gegen Elite, MiniBoss und Boss.", "Sniper_Tower");
        EnsureDefaultNewTowerSlot("Alchemist Tower", 90, "Hybrid-Tower: vergiftet Gegner und verlangsamt sie kurz.", "Alchemist_Tower");
        EnsureDefaultNewTowerSlot("Lightning Tower", 110, "Kettenblitz-Tower: Treffer und Chains verlangsamen Gegner kurz.", "Lightning_Tower");
        EnsureDefaultNewTowerSlot("Mortar Tower", 130, "Langsamer Mörser: Projektil schlägt an Zielposition ein und verursacht AOE-Schaden.", "Mortar_Tower");
        EnsureDefaultNewTowerSlot("Spike Tower", 85, "Kurze Reichweite: Treffer bluten und hinterlassen einmalige Stacheln auf dem Weg.", "Spike_Tower");
    }

    private void EnsureDefaultNewTowerSlot(string displayName, int cost, string description, string prefabResourceName)
    {
        if (HasTowerSlot(displayName))
            return;

        GameObject prefab = Resources.Load<GameObject>("TowerPrefabs/" + prefabResourceName);

        if (prefab == null)
            return;

        BuildOption option = new BuildOption
        {
            displayName = displayName,
            description = description,
            prefab = prefab,
            cost = cost,
            placementType = PlacementType.BuildTile
        };

        option.icon = LoadTowerIcon(displayName);

        towerSlots.Add(new TowerSelectionSlot
        {
            option = option,
            button = null,
            iconImage = null
        });
    }

    private bool HasTowerSlot(string displayName)
    {
        if (towerSlots == null)
            return false;

        string targetName = NormalizeTowerName(displayName);

        foreach (TowerSelectionSlot slot in towerSlots)
        {
            if (slot == null || slot.option == null)
                continue;

            if (NormalizeTowerName(slot.option.displayName) == targetName)
                return true;
        }

        return false;
    }

    private string NormalizeTowerName(string displayName)
    {
        return string.IsNullOrEmpty(displayName) ? "" : displayName.Replace(" Tower", "").Trim().ToLowerInvariant();
    }

    private void SetupSlot(TowerSelectionSlot slot)
    {
        if (slot == null)
            return;

        if (slot.button == null && autoCreateMissingSlotButtons)
            slot.button = CreateSlotButton(slot);

        if (slot.button == null)
            return;

        if (forceIconInsideButton)
            slot.iconImage = EnsureIconInsideButton(slot);

        ClearButtonChildTextsExceptProtected(slot.button);
        SetupSlotButtonVisual(slot);
        SetupSlotIcon(slot);
        SetupSlotLabel(slot);
        SetupSlotCostLabel(slot);

        BuildOption capturedOption = slot.option;
        slot.button.onClick.RemoveAllListeners();
        slot.button.onClick.AddListener(() => SelectOption(capturedOption));

        TowerSelectionHoverProxy hoverProxy = slot.button.GetComponent<TowerSelectionHoverProxy>();
        if (hoverProxy == null)
            hoverProxy = slot.button.gameObject.AddComponent<TowerSelectionHoverProxy>();

        hoverProxy.Initialize(this, capturedOption);
    }

    private Button CreateSlotButton(TowerSelectionSlot slot)
    {
        Transform parent = ResolveAutoCreatedSlotParent();

        if (parent == null)
            return null;

        GameObject buttonObject;

        if (slotButtonPrefab != null)
            buttonObject = Instantiate(slotButtonPrefab, parent);
        else
            buttonObject = new GameObject("TowerSlot_" + GetShortTowerName(slot != null ? slot.option : null), typeof(RectTransform), typeof(Image), typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();

        image.color = cardAltColor;
        image.raycastTarget = true;

        return button;
    }

    private Transform ResolveAutoCreatedSlotParent()
    {
        if (autoCreatedSlotParent != null)
            return autoCreatedSlotParent;

        if (towerSlots != null)
        {
            foreach (TowerSelectionSlot existingSlot in towerSlots)
            {
                if (existingSlot != null && existingSlot.button != null)
                    return existingSlot.button.transform.parent;
            }
        }

        return selectionPanel != null ? selectionPanel.transform : transform;
    }

    private Image EnsureIconInsideButton(TowerSelectionSlot slot)
    {
        if (slot == null || slot.button == null)
            return null;

        Image icon = null;

        if (slot.iconImage != null && slot.iconImage.transform.IsChildOf(slot.button.transform))
            icon = slot.iconImage;

        if (icon == null)
        {
            Transform existing = slot.button.transform.Find("Icon");
            if (existing != null)
                icon = existing.GetComponent<Image>();
        }

        if (icon == null && createMissingIconImages)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(slot.button.transform, false);
            icon = iconObject.GetComponent<Image>();
        }

        return icon;
    }

    private void SetupSlotButtonVisual(TowerSelectionSlot slot)
    {
        if (slot == null || slot.button == null)
            return;

        Image buttonImage = slot.button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = selectedOption != null && slot.option == selectedOption ? cardSelectedColor : cardAltColor;
            buttonImage.raycastTarget = true;
        }

        LayoutElement layoutElement = slot.button.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = slot.button.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = slotSize.x;
        layoutElement.preferredHeight = slotSize.y;
        layoutElement.minWidth = slotSize.x;
        layoutElement.minHeight = slotSize.y;
    }

    private void SetupSlotIcon(TowerSelectionSlot slot)
    {
        if (slot == null || slot.iconImage == null)
            return;

        slot.iconImage.sprite = slot.option != null ? slot.option.icon : null;
        slot.iconImage.enabled = slot.iconImage.sprite != null;
        slot.iconImage.preserveAspect = preserveIconAspect;
        slot.iconImage.raycastTarget = false;
        slot.iconImage.color = Color.white;

        RectTransform rect = slot.iconImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, slotIconYOffset);
        rect.sizeDelta = iconSize;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        slot.iconImage.transform.SetAsFirstSibling();
    }

    private void SetupSlotLabel(TowerSelectionSlot slot)
    {
        if (!createSlotNameLabels || slot == null || slot.button == null)
            return;

        TextMeshProUGUI label = null;
        Transform existing = slot.button.transform.Find("Label");

        if (existing != null)
            label = existing.GetComponent<TextMeshProUGUI>();

        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(slot.button.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, labelYOffset);
        rect.sizeDelta = labelSize;

        label.richText = true;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 9f;
        label.color = textSecondaryColor;
        label.raycastTarget = false;
        label.text = GetShortTowerName(slot.option);
    }

    private void SetupSlotCostLabel(TowerSelectionSlot slot)
    {
        if (!showSlotCostLabels || slot == null || slot.button == null)
            return;

        TextMeshProUGUI costLabel = null;
        Transform existing = slot.button.transform.Find("CostLabel");

        if (existing != null)
            costLabel = existing.GetComponent<TextMeshProUGUI>();

        if (costLabel == null)
        {
            GameObject labelObject = new GameObject("CostLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(slot.button.transform, false);
            costLabel = labelObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = costLabel.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, costLabelYOffset);
        rect.sizeDelta = costLabelSize;

        costLabel.richText = true;
        costLabel.enableWordWrapping = false;
        costLabel.overflowMode = TextOverflowModes.Ellipsis;
        costLabel.alignment = TextAlignmentOptions.Center;
        costLabel.fontSize = 8f;
        costLabel.color = accentColor;
        costLabel.raycastTarget = false;
        costLabel.text = GetCostLabelText(slot.option);
    }

    private string GetCostLabelText(BuildOption option)
    {
        if (option == null)
            return "? Gold";

        return Mathf.Max(0, option.cost) + " Gold";
    }

    private string GetShortTowerName(BuildOption option)
    {
        if (option == null || string.IsNullOrEmpty(option.displayName))
            return "Tower";

        string name = option.displayName.Replace(" Tower", "").Trim();
        return string.IsNullOrEmpty(name) ? option.displayName : name;
    }

    private void RepairAllSlotLayoutsIfNeeded()
    {
        if (!autoRepairSlotLayoutOnStart || towerSlots == null)
            return;

        foreach (TowerSelectionSlot slot in towerSlots)
            SetupSlot(slot);

        RepairSlotGridLayoutIfNeeded();
    }

    private void RepairSlotGridLayoutIfNeeded()
    {
        if (!autoRepairSlotGridLayout || towerSlots == null)
            return;

        Transform gridParent = ResolveAutoCreatedSlotParent();

        if (gridParent == null)
            return;

        GridLayoutGroup grid = gridParent.GetComponent<GridLayoutGroup>();

        if (grid == null)
            return;

        int columns = Mathf.Max(1, slotGridColumns);
        int visibleSlotCount = 0;

        foreach (TowerSelectionSlot slot in towerSlots)
        {
            if (slot != null && slot.button != null)
                visibleSlotCount++;
        }

        int rows = Mathf.Max(1, Mathf.CeilToInt(visibleSlotCount / (float)columns));
        float requiredGridWidth = columns * slotSize.x + (columns - 1) * slotGridSpacing.x;
        float requiredGridHeight = rows * slotSize.y + (rows - 1) * slotGridSpacing.y;

        grid.cellSize = slotSize;
        grid.spacing = slotGridSpacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        RectTransform rect = gridParent.GetComponent<RectTransform>();

        if (rect != null)
            rect.sizeDelta = new Vector2(requiredGridWidth, requiredGridHeight);

        RectTransform panelRect = selectionPanel != null ? selectionPanel.GetComponent<RectTransform>() : null;

        if (panelRect != null)
        {
            float verticalPadding = hideSelectionTitleAndCloseButton ? 45f : 90f;
            panelRect.sizeDelta = new Vector2(requiredGridWidth + 60f, requiredGridHeight + verticalPadding);
        }
    }

    private void ApplyCompactSelectionHeaderIfNeeded()
    {
        if (!hideSelectionTitleAndCloseButton || selectionPanel == null)
            return;

        Transform title = selectionPanel.transform.Find("Header/TitleText");

        if (title == null)
            title = selectionPanel.transform.Find("TitleText");

        if (title != null)
            title.gameObject.SetActive(false);

        if (closeSelectionButton != null)
            closeSelectionButton.gameObject.SetActive(false);
    }

    private void ClearButtonChildTextsExceptProtected(Button button)
    {
        if (button == null)
            return;

        TextMeshProUGUI[] texts = button.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null)
                continue;

            if (text.gameObject.name == "Label" || text.gameObject.name == "CostLabel")
                continue;

            text.text = "";
        }
    }

    private void ApplyTheme()
    {
        SetImageColor(selectionPanelBackground, panelColor);
        SetImageColor(selectedWindowBackground, selectedWindowColor);
        SetImageColor(tooltipBackground, cardColor);
        SetImageColor(openButtonBackground, accentColor);
        SetImageColor(closeButtonBackground, new Color32(200, 75, 75, 255));

        SetTextColor(selectedText, textPrimaryColor);
        SetTextColor(tooltipTitleText, textPrimaryColor);
        SetTextColor(tooltipDescriptionText, textSecondaryColor);
    }

    private void SetImageColor(Image image, Color color)
    {
        if (image != null)
            image.color = color;
    }

    private void SetTextColor(TextMeshProUGUI text, Color color)
    {
        if (text != null)
            text.color = color;
    }

    public bool IsSelectionOpen()
    {
        return selectionOpen;
    }

    private bool IsBlockedByModalUI()
    {
        if (buildManager == null || buildManager.gameManager == null)
            return false;

        return buildManager.gameManager.IsGameplayInputLockedByModalUI();
    }

    public void ToggleSelectionPanel()
    {
        if (IsBlockedByModalUI())
        {
            CloseSelectionPanel();
            return;
        }

        if (selectionOpen)
            CloseSelectionPanel();
        else
            OpenSelectionPanel();
    }

    public void OpenSelectionPanel()
    {
        if (IsBlockedByModalUI())
        {
            CloseSelectionPanel();
            return;
        }

        selectionOpen = true;
        ApplyCompactSelectionHeaderIfNeeded();
        RepairAllSlotLayoutsIfNeeded();

        if (selectionPanel != null)
            selectionPanel.SetActive(true);
    }

    public void CloseSelectionPanel()
    {
        selectionOpen = false;

        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        HideTooltip();
    }

    public void CancelTowerBuildSelection()
    {
        CloseSelectionPanel();
        HideTooltip();
        selectedOption = null;
        ClearSelectionText();

        if (rightClickAlsoClearsCurrentBuildSelection && buildManager != null)
            buildManager.ClearCurrentSelection();

        RefreshSlotSelectionVisuals();
    }

    public void SelectBasicTower() { SelectOption(basicTower); }
    public void SelectRapidTower() { SelectOption(rapidTower); }
    public void SelectHeavyTower() { SelectOption(heavyTower); }
    public void SelectFireTower() { SelectOption(fireTower); }
    public void SelectSlowTower() { SelectOption(slowTower); }
    public void SelectPoisonTower() { SelectOption(poisonTower); }
    public void SelectSniperTower() { SelectOption(sniperTower); }
    public void SelectAlchemistTower() { SelectOption(alchemistTower); }

    private void SelectOption(BuildOption option)
    {
        if (IsBlockedByModalUI())
        {
            CloseSelectionPanel();
            return;
        }

        if (option == null)
            return;

        selectedOption = option;

        if (buildManager != null)
            buildManager.SelectBuildOption(option);

        UpdateSelectedWindow(option);
        CloseSelectionPanel();
        RefreshSlotSelectionVisuals();
    }

    private void RefreshSlotSelectionVisuals()
    {
        if (towerSlots == null)
            return;

        foreach (TowerSelectionSlot slot in towerSlots)
            SetupSlotButtonVisual(slot);
    }

    private void UpdateSelectedWindow(BuildOption option)
    {
        if (selectedWindow != null)
        {
            ApplySelectedWindowTopRightLayout();
            selectedWindow.SetActive(true);
        }

        if (selectedText != null)
            selectedText.text = option.displayName + " ausgewählt";
    }

    private void ApplySelectedWindowTopRightLayout()
    {
        if (!autoPlaceSelectedWindowBelowUtilityButtons || selectedWindow == null)
            return;

        RectTransform rect = selectedWindow.GetComponent<RectTransform>();

        if (rect == null)
            return;

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = selectedWindowTopRightPosition;
    }

    public void ClearSelectionText()
    {
        selectedOption = null;

        if (selectedText != null)
            selectedText.text = "Kein Tower ausgewählt";

        if (selectedWindow != null)
            selectedWindow.SetActive(false);

        RefreshSlotSelectionVisuals();
    }

    public void ShowTooltip(BuildOption option)
    {
        if (option == null)
        {
            HideTooltip();
            return;
        }

        if (tooltipPanel != null)
            tooltipPanel.SetActive(true);

        if (tooltipTitleText != null)
            tooltipTitleText.text = option.displayName;

        if (tooltipDescriptionText != null)
            tooltipDescriptionText.text = BuildTooltipDescription(option);
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private string BuildTooltipDescription(BuildOption option)
    {
        string description = string.IsNullOrEmpty(option.description)
            ? GetFallbackTowerDescription(option.displayName)
            : option.description;

        return description + "\nKosten: " + option.cost + " Gold";
    }

    private string GetFallbackTowerDescription(string towerName)
    {
        string lowerName = string.IsNullOrEmpty(towerName) ? "" : towerName.ToLowerInvariant();

        if (lowerName.Contains("basic")) return "Günstiger Allrounder für den Spielstart.";
        if (lowerName.Contains("rapid")) return "Schneller Tower für Runner und Cleanup.";
        if (lowerName.Contains("lightning")) return "Kettenblitz-Tower: Treffer und Chains verlangsamen Gegner kurz.";
        if (lowerName.Contains("mortar")) return "Langsamer Mörser: Einschlag verursacht AOE-Schaden.";
        if (lowerName.Contains("spike")) return "Kurze Reichweite: Bleed-Treffer plus einmalige Weg-Stacheln.";
        if (lowerName.Contains("sniper")) return "Sehr hohe Reichweite und Einzelschaden gegen Elite, MiniBoss und Boss.";
        if (lowerName.Contains("heavy")) return "Langsamer Einzelschaden gegen Tanks, Knights und Bosse.";
        if (lowerName.Contains("alchemist")) return "Hybrid-Tower: vergiftet Gegner und verlangsamt sie kurz.";
        if (lowerName.Contains("fire")) return "Burn-Tower gegen Gruppen und Standard-Gegner.";
        if (lowerName.Contains("slow")) return "Kontrolltower, der Gegner verlangsamt.";
        if (lowerName.Contains("poison")) return "DoT-Tower gegen Tanks, MiniBoss und Bosse.";

        return "Tower auswählen und auf einem BuildTile platzieren.";
    }

    public void NotifySlotHover(TowerSelectionSlot slot, bool hovered)
    {
        if (slot == null || slot.button == null)
            return;

        Image image = slot.button.GetComponent<Image>();
        if (image == null)
            return;

        if (selectedOption != null && slot.option == selectedOption)
            image.color = cardSelectedColor;
        else
            image.color = hovered ? cardHoverColor : cardAltColor;
    }
}

public class TowerSelectionHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private BuildSelectionUI owner;
    private BuildOption option;

    public void Initialize(BuildSelectionUI newOwner, BuildOption newOption)
    {
        owner = newOwner;
        option = newOption;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.ShowTooltip(option);
            owner.NotifySlotHover(FindSlot(), true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.HideTooltip();
            owner.NotifySlotHover(FindSlot(), false);
        }
    }

    private TowerSelectionSlot FindSlot()
    {
        if (owner == null || owner.towerSlots == null)
            return null;

        foreach (TowerSelectionSlot slot in owner.towerSlots)
        {
            if (slot == null || slot.button == null)
                continue;

            if (slot.button.gameObject == gameObject)
                return slot;
        }

        return null;
    }
}
