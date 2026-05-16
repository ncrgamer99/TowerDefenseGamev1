using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class PathBuildOption
{
    public string displayName;
    public string description;
    public PathBuildOptionType optionType;
}

public class PathBuildManager : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public TileManager tileManager;
    public BuildManager buildManager;
    public GameManager gameManager;
    public GameObject pathGhostPrefab;

    [Header("UI")]
    public GameObject pathTopBar;
    public Button optionButton1;
    public Button optionButton2;
    public Button optionButton3;
    public TextMeshProUGUI optionText1;
    public TextMeshProUGUI optionText2;
    public TextMeshProUGUI optionText3;
    public TextMeshProUGUI descriptionText;
    public bool applyChoiceUILayoutDefaults = true;
    public Vector2 pathTopBarSize = new Vector2(0f, 88f);
    public float pathTopBarTopOffset = 116f;
    public float pathTopBarLeftInset = 360f;
    public float pathTopBarRightInset = 220f;
    public bool hideUnusedTopBarTextLabels = true;
    public Color choiceBarColor = new Color32(18, 22, 30, 245);
    public Color choiceButtonColor = new Color32(65, 95, 145, 255);
    public Color choiceDescriptionColor = new Color32(255, 220, 120, 255);

    [Header("Available Random Options")]
    public List<PathBuildOption> randomOptions = new List<PathBuildOption>();

    [Header("Special Tile Selection Timing")]
    public int specialTileSelectionWaveInterval = 5;

    private GameObject currentGhost;
    private Vector2Int hoveredGridPosition;
    private bool hasValidHover = false;
    private bool choiceOpen = false;
    private bool optionsGeneratedForCurrentBuildPhase = false;

    private PathBuildOption[] currentOptions = new PathBuildOption[3];

    private void Start()
    {
        ResolveGameManagerReference();

        ApplyChoiceUILayoutDefaults();

        if (pathTopBar != null)
            pathTopBar.SetActive(false);

        if (pathGhostPrefab != null)
        {
            currentGhost = Instantiate(pathGhostPrefab);
            currentGhost.SetActive(false);
        }
        else
        {
            Debug.LogError("PathBuildManager: pathGhostPrefab fehlt!");
        }

        SetupButtonEvents();
    }

    private void Update()
    {
        ResolveGameManagerReference();

        if (IsInputLockedByModalUI())
        {
            CancelChoice();
            return;
        }

        if (tileManager == null || mainCamera == null)
            return;

        if (!tileManager.IsBuildAllowed())
        {
            CancelChoice();
            return;
        }

        if (IsTowerBuildSelectionActive())
        {
            CancelChoice();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelChoice();
            return;
        }

        if (choiceOpen)
        {
            HandleHotkeys();
            return;
        }

        if (IsPointerOverUI())
        {
            HideGhostOnly();
            return;
        }

        UpdateGhost();

        if (hasValidHover && Input.GetMouseButtonDown(0))
        {
            OpenTileChoice();
        }
    }


    private void ResolveGameManagerReference()
    {
        if (gameManager != null)
            return;

        if (tileManager != null && tileManager.gameManager != null)
        {
            gameManager = tileManager.gameManager;
            return;
        }

        if (buildManager != null && buildManager.gameManager != null)
        {
            gameManager = buildManager.gameManager;
            return;
        }

        gameManager = FindObjectOfType<GameManager>();
    }

    private bool IsInputLockedByModalUI()
    {
        ResolveGameManagerReference();

        if (gameManager == null)
            return false;

        return gameManager.IsPathInputLockedByModalUI();
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsTowerBuildSelectionActive()
    {
        if (buildManager == null)
            return false;

        if (buildManager.selectedBuildOption == null)
            return false;

        return buildManager.selectedBuildOption.placementType == PlacementType.BuildTile;
    }

    private void ApplyChoiceUILayoutDefaults()
    {
        if (!applyChoiceUILayoutDefaults)
            return;

        if (pathTopBar != null)
        {
            RectTransform rect = pathTopBar.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2((pathTopBarLeftInset - pathTopBarRightInset) * 0.5f, -pathTopBarTopOffset);
                rect.sizeDelta = new Vector2(-(pathTopBarLeftInset + pathTopBarRightInset), pathTopBarSize.y);
            }

            Image image = pathTopBar.GetComponent<Image>();
            if (image != null)
                image.color = choiceBarColor;

            HideUnusedTopBarTextLabels();
        }

        StyleChoiceButton(optionButton1, optionText1);
        StyleChoiceButton(optionButton2, optionText2);
        StyleChoiceButton(optionButton3, optionText3);

        if (descriptionText != null)
        {
            descriptionText.fontSize = Mathf.Max(descriptionText.fontSize, 16f);
            descriptionText.color = choiceDescriptionColor;
            descriptionText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void HideUnusedTopBarTextLabels()
    {
        if (!hideUnusedTopBarTextLabels || pathTopBar == null)
            return;

        TextMeshProUGUI[] labels = pathTopBar.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label == null || label == optionText1 || label == optionText2 || label == optionText3 || label == descriptionText)
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

    private void SetupButtonEvents()
    {
        if (optionButton1 != null)
        {
            optionButton1.onClick.RemoveAllListeners();
            optionButton1.onClick.AddListener(() => ChooseOption(0));
            SetupOptionHover(optionButton1, 0);
        }

        if (optionButton2 != null)
        {
            optionButton2.onClick.RemoveAllListeners();
            optionButton2.onClick.AddListener(() => ChooseOption(1));
            SetupOptionHover(optionButton2, 1);
        }

        if (optionButton3 != null)
        {
            optionButton3.onClick.RemoveAllListeners();
            optionButton3.onClick.AddListener(() => ChooseOption(2));
            SetupOptionHover(optionButton3, 2);
        }

        
    }

    private void SetupOptionHover(Button button, int optionIndex)
    {
        if (button == null)
            return;

        PathBuildOptionHoverProxy hoverProxy = button.GetComponent<PathBuildOptionHoverProxy>();

        if (hoverProxy == null)
            hoverProxy = button.gameObject.AddComponent<PathBuildOptionHoverProxy>();

        hoverProxy.Initialize(this, optionIndex);
    }

    public void ShowOptionDescription(int optionIndex)
    {
        if (!choiceOpen || descriptionText == null)
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
            descriptionText.text = "Wähle ein Tile, bevor die nächste Wave startet. Hover zeigt den Effekt.";
    }

    private void UpdateGhost()
    {
        hasValidHover = false;

        if (currentGhost == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            currentGhost.SetActive(false);
            return;
        }

        Vector2Int gridPos = tileManager.WorldToGridPublic(hit.point);

        if (!tileManager.CanExtendTo(gridPos))
        {
            currentGhost.SetActive(false);
            return;
        }

        hoveredGridPosition = gridPos;
        hasValidHover = true;

        currentGhost.transform.position = tileManager.GridToWorldPublic(gridPos);
        currentGhost.SetActive(true);
    }

    private void OpenTileChoice()
    {
        if (IsInputLockedByModalUI())
            return;

        if (!hasValidHover)
            return;

        if (!tileManager.CanExtendTo(hoveredGridPosition))
            return;

        if (!ShouldOpenSpecialTileSelection())
        {
            BuildNormalPathTileAtHoveredPosition();
            return;
        }

        choiceOpen = true;

        if (!optionsGeneratedForCurrentBuildPhase)
        {
            GenerateCurrentOptions();
            optionsGeneratedForCurrentBuildPhase = true;
        }

        UpdateOptionUI();

        if (pathTopBar != null)
            pathTopBar.SetActive(true);

        if (currentGhost != null)
            currentGhost.SetActive(true);
    }

    private bool ShouldOpenSpecialTileSelection()
    {
        int interval = Mathf.Max(1, specialTileSelectionWaveInterval);

        if (gameManager == null)
            return false;

        return gameManager.waveNumber > 0 && gameManager.waveNumber % interval == 0;
    }

    private void BuildNormalPathTileAtHoveredPosition()
    {
        if (!tileManager.CanExtendTo(hoveredGridPosition))
        {
            Debug.Log("Gewählte Position ist nicht mehr gültig.");
            CancelChoice();
            return;
        }

        bool success = tileManager.TryExtendPathTo(hoveredGridPosition);

        if (success)
        {
            optionsGeneratedForCurrentBuildPhase = false;
            CloseChoiceUI();
        }
    }

    private void GenerateCurrentOptions()
    {
        currentOptions[0] = new PathBuildOption
        {
            displayName = "Path Tile",
            description = "Normaler Weg: Base zieht weiter, danach startet die nächste Welle.",
            optionType = PathBuildOptionType.PathTile
        };

        List<PathBuildOption> optionPool = CreateSpecialOptionPool();
        currentOptions[1] = DrawRandomOptionFromPool(optionPool);
        currentOptions[2] = DrawRandomOptionFromPool(optionPool);
    }

    private List<PathBuildOption> CreateSpecialOptionPool()
    {
        List<PathBuildOption> optionPool = new List<PathBuildOption>();

        if (randomOptions != null)
        {
            foreach (PathBuildOption option in randomOptions)
            {
                if (IsSupportedSpecialOption(option))
                    AddOptionIfTypeMissing(optionPool, CreateDisplayOption(option));
            }
        }

        AddMissingDefaultSpecialOptions(optionPool);

        if (optionPool.Count == 0)
            optionPool.AddRange(CreateDefaultSpecialOptions());

        return optionPool;
    }

    private PathBuildOption DrawRandomOptionFromPool(List<PathBuildOption> optionPool)
    {
        if (optionPool == null || optionPool.Count == 0)
            return CreateDefaultSpecialOptions()[0];

        int randomIndex = Random.Range(0, optionPool.Count);
        PathBuildOption option = optionPool[randomIndex];
        optionPool.RemoveAt(randomIndex);
        return option;
    }

    private PathBuildOption CreateDisplayOption(PathBuildOption option)
    {
        if (option == null)
            return null;

        PathBuildOption defaultOption = GetDefaultSpecialOption(option.optionType);

        if (defaultOption == null)
            return option;

        return new PathBuildOption
        {
            displayName = string.IsNullOrWhiteSpace(option.displayName) ? defaultOption.displayName : option.displayName,
            description = ShouldUseDefaultDescription(option.description) ? defaultOption.description : option.description,
            optionType = option.optionType
        };
    }

    private bool ShouldUseDefaultDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return true;

        string lowerDescription = description.ToLowerInvariant();
        return lowerDescription.Contains("platzhalter") || lowerDescription.Contains("später");
    }

    private PathBuildOption GetDefaultSpecialOption(PathBuildOptionType optionType)
    {
        foreach (PathBuildOption option in CreateDefaultSpecialOptions())
        {
            if (option != null && option.optionType == optionType)
                return option;
        }

        return null;
    }

    private void AddMissingDefaultSpecialOptions(List<PathBuildOption> optionPool)
    {
        if (optionPool == null)
            return;

        foreach (PathBuildOption defaultOption in CreateDefaultSpecialOptions())
        {
            AddOptionIfTypeMissing(optionPool, defaultOption);
        }
    }

    private void AddOptionIfTypeMissing(List<PathBuildOption> optionPool, PathBuildOption option)
    {
        if (optionPool == null || option == null)
            return;

        if (!HasOptionType(optionPool, option.optionType))
            optionPool.Add(option);
    }

    private bool HasOptionType(List<PathBuildOption> optionPool, PathBuildOptionType optionType)
    {
        if (optionPool == null)
            return false;

        foreach (PathBuildOption option in optionPool)
        {
            if (option != null && option.optionType == optionType)
                return true;
        }

        return false;
    }

    private bool IsSupportedSpecialOption(PathBuildOption option)
    {
        if (option == null)
            return false;

        return option.optionType == PathBuildOptionType.TrapTile ||
               option.optionType == PathBuildOptionType.GoldTile ||
               option.optionType == PathBuildOptionType.SlowTile ||
               option.optionType == PathBuildOptionType.KnockTile ||
               option.optionType == PathBuildOptionType.RangeTile ||
               option.optionType == PathBuildOptionType.DamageTile ||
               option.optionType == PathBuildOptionType.RateTile ||
               option.optionType == PathBuildOptionType.XPTile ||
               option.optionType == PathBuildOptionType.UpgradeTile ||
               option.optionType == PathBuildOptionType.ComboTile;
    }

    private List<PathBuildOption> CreateDefaultSpecialOptions()
    {
        return new List<PathBuildOption>
        {
            new PathBuildOption
            {
                displayName = "Gold Tile",
                description = "Gold-Tile: blockiert dieses Feld und erzeugt während Waves Gold.",
                optionType = PathBuildOptionType.GoldTile
            },
            new PathBuildOption
            {
                displayName = "Trap Tile",
                description = "Trap-Tile: Weg-Tile. Gegner bluten 20s lang: 2 Schaden alle 3s.",
                optionType = PathBuildOptionType.TrapTile
            },
            new PathBuildOption
            {
                displayName = "Slow Tile",
                description = "Slow-Tile: Weg-Tile. Gegner werden kurz stark verlangsamt.",
                optionType = PathBuildOptionType.SlowTile
            },
            new PathBuildOption
            {
                displayName = "Knock Tile",
                description = "Knock-Tile: Weg-Tile. Wirft normale Gegner zurück; Boss/MiniBoss immun.",
                optionType = PathBuildOptionType.KnockTile
            },
            new PathBuildOption
            {
                displayName = "Range Tile",
                description = "Range-Tile: kein Weg. Nahe Tower erhalten mehr Reichweite.",
                optionType = PathBuildOptionType.RangeTile
            },
            new PathBuildOption
            {
                displayName = "Damage Tile",
                description = "Damage-Tile: kein Weg. Nahe Tower verursachen mehr Schaden.",
                optionType = PathBuildOptionType.DamageTile
            },
            new PathBuildOption
            {
                displayName = "Rate Tile",
                description = "Rate-Tile: kein Weg. Nahe Tower feuern schneller.",
                optionType = PathBuildOptionType.RateTile
            },
            new PathBuildOption
            {
                displayName = "XP Tile",
                description = "XP-Tile: kein Weg. Nahe Tower erhalten mehr XP.",
                optionType = PathBuildOptionType.XPTile
            },
            new PathBuildOption
            {
                displayName = "Upgrade Tile",
                description = "Upgrade-Tile: kein Weg. Point-Upgrades naher Tower sind stärker.",
                optionType = PathBuildOptionType.UpgradeTile
            },
            new PathBuildOption
            {
                displayName = "Combo Tile",
                description = "Combo-Tile: Darkness, wenn Gegner Burn, Poison und Bleed gleichzeitig haben.",
                optionType = PathBuildOptionType.ComboTile
            }
        };
    }

    private void UpdateOptionUI()
    {
        if (optionText1 != null)
            optionText1.text = "[1] " + currentOptions[0].displayName;

        if (optionText2 != null)
            optionText2.text = "[2] " + currentOptions[1].displayName;

        if (optionText3 != null)
            optionText3.text = "[3] " + currentOptions[2].displayName;

        ShowDefaultChoiceDescription();
    }

    private void HandleHotkeys()
    {
        if (IsInputLockedByModalUI())
        {
            CancelChoice();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ChooseOption(0);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            ChooseOption(1);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            ChooseOption(2);
    }

    private void ChooseOption(int index)
    {
        if (IsInputLockedByModalUI())
        {
            CancelChoice();
            return;
        }

        if (!choiceOpen)
            return;

        if (index < 0 || index >= currentOptions.Length)
            return;

        PathBuildOption option = currentOptions[index];

        if (option == null)
            return;

        if (!tileManager.CanExtendTo(hoveredGridPosition))
        {
            Debug.Log("Gewählte Position ist nicht mehr gültig.");
            CancelChoice();
            return;
        }

        bool success = false;

        if (option.optionType == PathBuildOptionType.PathTile)
        {
            success = tileManager.TryExtendPathTo(hoveredGridPosition);
        }
        else if (IsSpecialPathOption(option.optionType))
        {
            success = tileManager.TryExtendSpecialPathTo(hoveredGridPosition, option.optionType);
        }
        else if (option.optionType == PathBuildOptionType.GoldTile)
        {
            success = tileManager.TryBuildGoldTileAt(hoveredGridPosition);
        }
        else if (IsSupportTileOption(option.optionType))
        {
            success = tileManager.TryBuildSupportTileAt(hoveredGridPosition, option.optionType);
        }
        else
        {
            Debug.Log(option.displayName + " gewählt. Funktion kommt später.");
        }

        if (success)
        {
            optionsGeneratedForCurrentBuildPhase = false;
            CloseChoiceUI();
            return;
        }

        if (descriptionText != null)
        {
            descriptionText.text = option.displayName + " konnte hier nicht gebaut werden.";
        }
    }

    private bool IsSpecialPathOption(PathBuildOptionType optionType)
    {
        return optionType == PathBuildOptionType.TrapTile ||
               optionType == PathBuildOptionType.SlowTile ||
               optionType == PathBuildOptionType.KnockTile ||
               optionType == PathBuildOptionType.ComboTile;
    }

    private bool IsSupportTileOption(PathBuildOptionType optionType)
    {
        return optionType == PathBuildOptionType.RangeTile ||
               optionType == PathBuildOptionType.DamageTile ||
               optionType == PathBuildOptionType.RateTile ||
               optionType == PathBuildOptionType.XPTile ||
               optionType == PathBuildOptionType.UpgradeTile;
    }

    public void CancelChoice()
    {
        choiceOpen = false;
        hasValidHover = false;

        ApplyChoiceUILayoutDefaults();

        if (pathTopBar != null)
            pathTopBar.SetActive(false);

        if (currentGhost != null)
            currentGhost.SetActive(false);
    }

    private void HideGhostOnly()
    {
        hasValidHover = false;

        if (currentGhost != null)
        {
            currentGhost.SetActive(false);
        }
    }

    private void CloseChoiceUI()
    {
        choiceOpen = false;
        hasValidHover = false;

        ApplyChoiceUILayoutDefaults();

        if (pathTopBar != null)
            pathTopBar.SetActive(false);

        if (currentGhost != null)
            currentGhost.SetActive(false);
    }

    public bool IsChoiceOpen()
    {
        return choiceOpen;
    }
}

public class PathBuildOptionHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PathBuildManager owner;
    private int optionIndex;

    public void Initialize(PathBuildManager newOwner, int newOptionIndex)
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
