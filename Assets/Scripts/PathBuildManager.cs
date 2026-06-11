using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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
    private const float GeneratedGhostVisualBottomLocalY = -0.05f;

    private enum PathBuildDirection
    {
        Forward = 0,
        Left = 1,
        Right = 2
    }

    private enum PathBuildInputSource
    {
        Mouse,
        Hotkey,
        UI
    }

    [Header("References")]
    public Camera mainCamera;
    public TileManager tileManager;
    public BuildManager buildManager;
    public GameManager gameManager;
    public GameObject pathGhostPrefab;

    [Header("Generated Tile Prefabs")]
    public GeneratedTilePrefabSet generatedTilePrefabSet;
    public bool useGeneratedTilePrefabs = true;
    public bool allowSpecialTilePlacementV1 = true;
    public bool specialTilesUsePathBehaviourInV1 = true;

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
    public float choiceButtonRowSpacing = 14f;
    public bool forceChoiceButtonRectLayout = true;
    public float choiceButtonTopPadding = 6f;
    public float choiceButtonHeight = 46f;
    public float choiceDescriptionHeight = 30f;
    public float choiceDescriptionBottomPadding = 4f;
    public float choiceDescriptionFontSize = 16f;

    [Header("Available Random Options")]
    public List<PathBuildOption> randomOptions = new List<PathBuildOption>();

    [Header("Elite Tile Quality V1")]
    public List<PathBuildOption> eliteQualityOptions = new List<PathBuildOption>();
    public int pendingEliteTileQualityBoosts = 0;

    [Header("Tile Choice Timing")]
    public int specialTileSelectionWaveInterval = 5;

    [Header("Path Direction Hotkeys")]
    public bool enablePathDirectionHotkeys = true;
    public KeyCode forwardPathBuildKey = KeyCode.Alpha1;
    public KeyCode leftPathBuildKey = KeyCode.Alpha2;
    public KeyCode rightPathBuildKey = KeyCode.Alpha3;

    [Header("Verbau Choice")]
    [FormerlySerializedAs("enablePostWaveVerbauChoice")]
    public bool enableBlockedVerbauChoice = true;
    public string verbauChoiceDisplayName = "Verbauauswahl";
    public string noVerbauDisplayName = "Kein Tile";

    private GameObject currentGhost;
    private Vector2Int hoveredGridPosition;
    private bool hasValidHover = false;
    private bool choiceOpen = false;
    private bool optionsGeneratedForCurrentBuildPhase = false;
    private bool specialTileChoicePending = false;
    private bool specialTileChoiceActive = false;
    private bool blockedVerbauChoicePending = false;
    private bool verbauChoiceActive = false;
    private bool immediateVerbauChoiceActive = false;
    private bool immediateVerbauChoiceCompletesPostWave = false;
    private bool supportTilePlacementModeActive = false;
    private PathBuildOption pendingSupportTilePlacementOption;
    private BuildTile hoveredSupportPlacementTile;

    private PathBuildOption[] currentOptions = new PathBuildOption[3];

    private void Start()
    {
        ResolveGameManagerReference();
        ResolveGeneratedTilePrefabSetReference();
        SyncGeneratedTilePrefabSetToTileManager();
        EnsureDefaultRandomOptions();

        ApplyChoiceUILayoutDefaults();

        if (pathTopBar != null)
            pathTopBar.SetActive(false);

        if (pathGhostPrefab != null)
        {
            currentGhost = Instantiate(pathGhostPrefab);
            NormalizeGeneratedGhostVisual(currentGhost);
            currentGhost.SetActive(false);
        }
        else
        {
            Debug.LogError("PathBuildManager: pathGhostPrefab fehlt!");
        }

        SetupButtonEvents();
        AutopathManager.EnsureExists(this);
    }

    private void NormalizeGeneratedGhostVisual(GameObject ghostObject)
    {
        if (ghostObject == null)
            return;

        ghostObject.transform.rotation = Quaternion.identity;

        Transform visual = ghostObject.transform.Find("Visual");

        if (visual != null)
        {
            visual.localRotation = Quaternion.identity;
            AlignGeneratedGhostVisualToGround(ghostObject, visual);
        }

        HideGeneratedGhostRailsAndCorners(ghostObject.transform);
    }

    private void ShowCurrentGhostAt(Vector2Int gridPos)
    {
        if (currentGhost == null || tileManager == null)
            return;

        currentGhost.transform.position = tileManager.GridToWorldPublic(gridPos);

        Transform visual = currentGhost.transform.Find("Visual");

        if (visual != null)
            AlignGeneratedGhostVisualToGround(currentGhost, visual);

        currentGhost.SetActive(true);
    }

    private void AlignGeneratedGhostVisualToGround(GameObject ghostObject, Transform visual)
    {
        if (ghostObject == null || visual == null)
            return;

        if (!TryGetRendererBounds(visual, out Bounds bounds))
            return;

        float localGroundY = tileManager != null ? tileManager.generatedTileGroundLocalY : GeneratedGhostVisualBottomLocalY;
        float desiredMinY = ghostObject.transform.position.y + localGroundY;
        float deltaY = desiredMinY - bounds.min.y;

        if (Mathf.Abs(deltaY) > 0.001f)
            visual.position += Vector3.up * deltaY;
    }

    private bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();

        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private void HideGeneratedGhostRailsAndCorners(Transform root)
    {
        if (root == null)
            return;

        if (root.name.Contains("Rail_") || root.name.Contains("CornerPost") || root.name.Contains("Corner_"))
            root.gameObject.SetActive(false);

        for (int i = 0; i < root.childCount; i++)
            HideGeneratedGhostRailsAndCorners(root.GetChild(i));
    }

    private void Update()
    {
        ResolveGameManagerReference();

        if (tileManager != null && tileManager.IsBaseRelocationModeActive())
        {
            HandleBaseRelocationPlacement();
            return;
        }

        if (IsInputLockedByModalUI() && !CanIgnorePathLockForImmediateVerbauChoice())
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

        if (supportTilePlacementModeActive)
        {
            HandleSupportTilePlacementMode();
            return;
        }

        if (choiceOpen)
        {
            HandleHotkeys();
            return;
        }

        if (IsTowerBuildSelectionActive())
        {
            CancelChoice();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            if (immediateVerbauChoiceActive)
            {
                CompleteImmediateVerbauChoice();
                return;
            }

            CancelChoice();
            return;
        }
        if (IsPointerOverUI())
        {
            HideGhostOnly();
            return;
        }

        if (HandleDirectionHotkeys())
            return;

        UpdateGhost();

        if (hasValidHover && Input.GetMouseButtonDown(0))
        {
            OpenTileChoice();
        }
    }

    public void RequestForwardPathBuildFromUI()
    {
        RequestPathBuildDirectionFromUI((int)PathBuildDirection.Forward);
    }

    public void RequestLeftPathBuildFromUI()
    {
        RequestPathBuildDirectionFromUI((int)PathBuildDirection.Left);
    }

    public void RequestRightPathBuildFromUI()
    {
        RequestPathBuildDirectionFromUI((int)PathBuildDirection.Right);
    }

    public void RequestPathBuildDirectionFromUI(int directionIndex)
    {
        if (directionIndex < (int)PathBuildDirection.Forward || directionIndex > (int)PathBuildDirection.Right)
        {
            ShowPathBuildRequestFailure("Unbekannte Wegbau-Richtung.");
            return;
        }

        TryRequestPathBuildDirection((PathBuildDirection)directionIndex, PathBuildInputSource.UI);
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

    private void ResolveGeneratedTilePrefabSetReference()
    {
        if (!useGeneratedTilePrefabs)
            return;

        if (generatedTilePrefabSet == null && tileManager != null && tileManager.generatedTilePrefabSet != null)
            generatedTilePrefabSet = tileManager.generatedTilePrefabSet;

        if (generatedTilePrefabSet == null)
            generatedTilePrefabSet = FindObjectOfType<GeneratedTilePrefabSet>();

        if (generatedTilePrefabSet != null && generatedTilePrefabSet.pathGhostTilePrefab != null)
            pathGhostPrefab = generatedTilePrefabSet.pathGhostTilePrefab;
    }

    private void SyncGeneratedTilePrefabSetToTileManager()
    {
        if (!useGeneratedTilePrefabs || tileManager == null || generatedTilePrefabSet == null)
            return;

        if (tileManager.generatedTilePrefabSet == null)
            tileManager.generatedTilePrefabSet = generatedTilePrefabSet;

        tileManager.useGeneratedTilePrefabs = true;
    }

    private void EnsureDefaultRandomOptions()
    {
        if (randomOptions == null)
            randomOptions = new List<PathBuildOption>();

        RemoveRemovedPathBuildOptions(randomOptions);

        AddOptionIfTypeMissing(randomOptions, new PathBuildOption
        {
            displayName = "Trap Tile",
            description = "Gefaehrliches Pfad-Tile. V1: zaehlt als Pfad-Variante.",
            optionType = PathBuildOptionType.TrapTile
        });
    }

    private void RemoveRemovedPathBuildOptions(List<PathBuildOption> options)
    {
        if (options == null)
            return;

        for (int i = options.Count - 1; i >= 0; i--)
        {
            PathBuildOption option = options[i];

            if (option == null || IsRemovedPathBuildSelectionOption(option.optionType))
                options.RemoveAt(i);
        }
    }

    private bool IsRemovedPathBuildSelectionOption(PathBuildOptionType optionType)
    {
        int rawOptionValue = (int)optionType;
        return rawOptionValue == 2 || rawOptionValue == 3;
    }

    private bool IsInputLockedByModalUI()
    {
        ResolveGameManagerReference();

        if (gameManager == null)
            return false;

        return gameManager.IsPathInputLockedByModalUI();
    }

    private bool CanIgnorePathLockForImmediateVerbauChoice()
    {
        if (!immediateVerbauChoiceActive && !supportTilePlacementModeActive)
            return false;

        ResolveGameManagerReference();

        if (gameManager == null)
            return true;

        return !gameManager.IsGameplayInputLockedByModalUI();
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

        if (buildManager.buildSelectionUI != null && buildManager.buildSelectionUI.IsSelectionOpen())
            return true;

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

    private void ApplyChoiceButtonRectLayout()
    {
        if (!forceChoiceButtonRectLayout || pathTopBar == null)
            return;

        DisableChoiceBarLayoutGroups(pathTopBar.transform);

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

            if (buttonRect.parent != pathTopBar.transform)
                buttonRect.SetParent(pathTopBar.transform, false);

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
        if (descriptionText == null || pathTopBar == null)
            return;

        RectTransform descriptionRect = descriptionText.GetComponent<RectTransform>();
        if (descriptionRect == null)
            return;

        if (descriptionRect.parent != pathTopBar.transform)
            descriptionRect.SetParent(pathTopBar.transform, false);

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

    private void ConfigureChoiceButtonRowLayout()
    {
        Transform row = GetChoiceButtonRowTransform();
        if (row == null)
            return;

        HorizontalLayoutGroup horizontalLayout = row.GetComponent<HorizontalLayoutGroup>();
        VerticalLayoutGroup verticalLayout = row.GetComponent<VerticalLayoutGroup>();
        if (horizontalLayout == null)
        {
            if (verticalLayout != null)
            {
                if (forceChoiceButtonRectLayout)
                    verticalLayout.enabled = false;

                return;
            }

            horizontalLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        if (horizontalLayout == null)
            return;

        horizontalLayout.spacing = choiceButtonRowSpacing;
        horizontalLayout.padding = new RectOffset(0, 0, 0, 0);
        horizontalLayout.childAlignment = TextAnchor.UpperCenter;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = true;
        horizontalLayout.childForceExpandHeight = false;

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
        {
            if (verbauChoiceActive)
                descriptionText.text = verbauChoiceDisplayName + ": Waehle ein Nicht-Weg-Tile oder ueberspringe die Auswahl.";
            else
                descriptionText.text = "Wähle ein Tile, bevor die nächste Wave startet. Hover zeigt den Effekt.";
        }
    }

    private void HandleBaseRelocationPlacement()
    {
        if (tileManager == null || mainCamera == null)
            return;

        choiceOpen = false;

        if (pathTopBar != null)
            pathTopBar.SetActive(false);

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            tileManager.SetBaseRelocationModeActive(false);

            if (gameManager != null)
                gameManager.CancelBlockedBaseRelocation();

            HideGhostOnly();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            HideGhostOnly();
            return;
        }

        Vector2Int gridPos = tileManager.WorldToGridPublic(hit.point);

        if (!tileManager.CanRelocateBaseTo(gridPos))
        {
            HideGhostOnly();
            return;
        }

        hoveredGridPosition = gridPos;
        hasValidHover = true;

        if (currentGhost != null)
            ShowCurrentGhostAt(gridPos);

        if (Input.GetMouseButtonDown(0))
        {
            bool success = tileManager.TryRelocateBaseTo(gridPos);

            if (success && gameManager != null)
                gameManager.CompleteBlockedBaseRelocation();

            HideGhostOnly();
        }
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

        if (!TryValidatePathBuildTarget(gridPos, out string unusedFailureReason))
        {
            currentGhost.SetActive(false);
            return;
        }

        hoveredGridPosition = gridPos;
        hasValidHover = true;

        ShowCurrentGhostAt(gridPos);
    }

    private void OpenTileChoice()
    {
        if (!hasValidHover)
            return;

        TryRequestPathBuildAt(hoveredGridPosition, PathBuildInputSource.Mouse);
    }

    private bool HandleDirectionHotkeys()
    {
        if (!enablePathDirectionHotkeys)
            return false;

        if (WasPathDirectionKeyPressed(forwardPathBuildKey))
        {
            TryRequestPathBuildDirection(PathBuildDirection.Forward, PathBuildInputSource.Hotkey);
            return true;
        }

        if (WasPathDirectionKeyPressed(leftPathBuildKey))
        {
            TryRequestPathBuildDirection(PathBuildDirection.Left, PathBuildInputSource.Hotkey);
            return true;
        }

        if (WasPathDirectionKeyPressed(rightPathBuildKey))
        {
            TryRequestPathBuildDirection(PathBuildDirection.Right, PathBuildInputSource.Hotkey);
            return true;
        }

        return false;
    }

    private bool WasPathDirectionKeyPressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    private bool TryRequestPathBuildDirection(PathBuildDirection direction, PathBuildInputSource source)
    {
        if (!TryGetPathBuildDirectionPosition(direction, out Vector2Int targetPosition))
        {
            ShowPathBuildRequestFailure("Keine Wegbau-Richtung verfuegbar.");
            return false;
        }

        return TryRequestPathBuildAt(targetPosition, source);
    }

    private bool TryGetPathBuildDirectionPosition(PathBuildDirection direction, out Vector2Int targetPosition)
    {
        targetPosition = Vector2Int.zero;

        if (tileManager == null)
            return false;

        Vector2Int[] positions = tileManager.GetPossibleExtensionPositions();
        int index = (int)direction;

        if (positions == null || index < 0 || index >= positions.Length)
            return false;

        targetPosition = positions[index];
        return true;
    }

    private bool TryRequestPathBuildAt(Vector2Int targetPosition, PathBuildInputSource source)
    {
        if (!CanHandlePathBuildRequest(source))
            return false;

        if (!TryValidatePathBuildTarget(targetPosition, out string failureReason))
        {
            ShowPathBuildRequestFailure(failureReason);
            return false;
        }

        hoveredGridPosition = targetPosition;
        hasValidHover = true;

        bool openVerbauChoice = ShouldOpenPendingVerbauChoice();
        bool openSpecialTileChoice = !openVerbauChoice && ShouldOpenSpecialTileSelection();

        if (!openVerbauChoice && !openSpecialTileChoice)
            return BuildNormalPathTileAtHoveredPosition();

        return OpenTileChoiceAtCurrentTarget(openVerbauChoice, openSpecialTileChoice);
    }

    private bool CanHandlePathBuildRequest(PathBuildInputSource source)
    {
        if (source != PathBuildInputSource.Hotkey)
            ResolveGameManagerReference();

        if (IsInputLockedByModalUI())
            return false;

        if (tileManager == null || !tileManager.IsBuildAllowed())
            return false;

        if (choiceOpen || supportTilePlacementModeActive)
            return false;

        if (IsTowerBuildSelectionActive())
            return false;

        return true;
    }

    private bool TryValidatePathBuildTarget(Vector2Int targetPosition, out string failureReason)
    {
        failureReason = "";

        if (tileManager == null)
        {
            failureReason = "TileManager fehlt.";
            return false;
        }

        if (!tileManager.IsBuildAllowed())
        {
            failureReason = "Wegbau ist aktuell gesperrt.";
            return false;
        }

        if (!tileManager.CanExtendTo(targetPosition))
        {
            failureReason = "Diese Wegbau-Richtung ist nicht gueltig.";
            return false;
        }

        return true;
    }

    private bool OpenTileChoiceAtCurrentTarget(bool openVerbauChoice, bool openSpecialTileChoice)
    {
        choiceOpen = true;
        verbauChoiceActive = openVerbauChoice;
        specialTileChoiceActive = openSpecialTileChoice;

        if (!optionsGeneratedForCurrentBuildPhase)
        {
            GenerateCurrentOptions();
            optionsGeneratedForCurrentBuildPhase = true;
        }

        UpdateOptionUI();

        if (pathTopBar != null)
            pathTopBar.SetActive(true);

        if (currentGhost != null)
            ShowCurrentGhostAt(hoveredGridPosition);

        return true;
    }

    private void ShowPathBuildRequestFailure(string message)
    {
        string safeMessage = string.IsNullOrWhiteSpace(message) ? "Wegbau ist hier nicht moeglich." : message;

        if (descriptionText != null && choiceOpen)
            descriptionText.text = safeMessage;

        Debug.Log(safeMessage);
    }

    private bool ShouldOpenSpecialTileSelection()
    {
        if (gameManager == null)
            return false;

        if (gameManager.IsDevGameMode())
            return true;

        return specialTileChoicePending;
    }

    public bool OpenPostWaveVerbauChoiceForCompletedWave(WaveCompletionResult completedWaveResult)
    {
        if (completedWaveResult == null || !completedWaveResult.waveCompleted)
            return false;

        int interval = Mathf.Max(1, specialTileSelectionWaveInterval);
        int completedWave = Mathf.Max(0, completedWaveResult.waveNumber);

        if (completedWave <= 0 || completedWave % interval != 0)
            return false;

        return OpenImmediateVerbauChoice(true, "Verbauauswahl nach Wave " + completedWave + " geoeffnet.");
    }

    public bool QueueSpecialTileChoiceForCompletedWave(WaveCompletionResult completedWaveResult)
    {
        if (completedWaveResult == null || !completedWaveResult.waveCompleted)
            return false;

        int interval = GetSpecialTileSelectionWaveInterval();
        int completedWave = Mathf.Max(0, completedWaveResult.waveNumber);

        if (completedWave <= 0 || completedWave % interval != 0)
            return false;

        specialTileChoicePending = true;
        specialTileChoiceActive = false;
        optionsGeneratedForCurrentBuildPhase = false;
        Debug.Log("Normale Tileauswahl fuer Wave " + completedWave + " vorgemerkt.");
        return true;
    }

    public int GetSpecialTileSelectionWaveInterval()
    {
        return Mathf.Max(1, specialTileSelectionWaveInterval);
    }

    public bool OpenTileVorratChoiceForCompletedWave(WaveCompletionResult completedWaveResult)
    {
        if (completedWaveResult == null || !completedWaveResult.waveCompleted)
            return false;

        int completedWave = Mathf.Max(0, completedWaveResult.waveNumber);
        if (completedWave <= 0)
            return false;

        return OpenImmediateVerbauChoice(true, "Tile-Vorrat-Auswahl nach Wave " + completedWave + " geoeffnet.");
    }

    private bool ShouldOpenPendingVerbauChoice()
    {
        return enableBlockedVerbauChoice && blockedVerbauChoicePending;
    }

    public bool CanQueueBlockedVerbauChoice()
    {
        return enableBlockedVerbauChoice && HasAvailableVerbauChoiceOption();
    }

    public bool QueueBlockedVerbauChoice()
    {
        if (!CanQueueBlockedVerbauChoice())
        {
            Debug.Log(verbauChoiceDisplayName + ": keine freigeschalteten Nicht-Weg-Tiles verfuegbar.");
            return false;
        }

        blockedVerbauChoicePending = true;
        verbauChoiceActive = false;
        optionsGeneratedForCurrentBuildPhase = false;

        Debug.Log(verbauChoiceDisplayName + " durch Verbau-Event vorbereitet.");
        return true;
    }

    public bool OpenBlockedVerbauChoice()
    {
        return OpenImmediateVerbauChoice(false, verbauChoiceDisplayName + " durch Verbau-Event geoeffnet.");
    }

    public void ResetVerbauChoice()
    {
        blockedVerbauChoicePending = false;
        verbauChoiceActive = false;
        optionsGeneratedForCurrentBuildPhase = false;
    }

    public void ResetQueuedTileChoices()
    {
        specialTileChoicePending = false;
        specialTileChoiceActive = false;
        immediateVerbauChoiceActive = false;
        immediateVerbauChoiceCompletesPostWave = false;
        supportTilePlacementModeActive = false;
        pendingSupportTilePlacementOption = null;
        SetHoveredSupportPlacementTile(null);
        SetChoiceButtonsInteractable(true);

        if (tileManager != null)
            tileManager.SetSupportTilePlacementBuildMode(false);

        ResetVerbauChoice();
    }

    private bool OpenImmediateVerbauChoice(bool completesPostWave, string logMessage)
    {
        if (!enableBlockedVerbauChoice)
            return false;

        if (choiceOpen || supportTilePlacementModeActive)
            return false;

        choiceOpen = true;
        verbauChoiceActive = true;
        immediateVerbauChoiceActive = true;
        immediateVerbauChoiceCompletesPostWave = completesPostWave;
        specialTileChoiceActive = false;
        optionsGeneratedForCurrentBuildPhase = false;

        GenerateCurrentOptions();
        optionsGeneratedForCurrentBuildPhase = true;
        UpdateOptionUI();
        SetChoiceButtonsInteractable(true);

        if (pathTopBar != null)
            pathTopBar.SetActive(true);

        if (currentGhost != null)
            currentGhost.SetActive(false);

        if (!string.IsNullOrWhiteSpace(logMessage))
            Debug.Log(logMessage);

        return true;
    }

    private bool BuildNormalPathTileAtHoveredPosition()
    {
        return TryApplyPathBuildOptionAtTarget(CreatePathTileOption(), hoveredGridPosition, true);
    }

    private bool TryApplyPathBuildOptionAtTarget(PathBuildOption option, Vector2Int targetPosition, bool cancelWhenTargetInvalid)
    {
        if (option == null)
            return false;

        if (!TryValidatePathBuildTarget(targetPosition, out string failureReason))
        {
            ShowPathBuildRequestFailure(failureReason);

            if (cancelWhenTargetInvalid)
                CancelChoice();

            return false;
        }

        hoveredGridPosition = targetPosition;
        hasValidHover = true;

        if (!IsPathBuildOptionUnlocked(option.optionType))
        {
            Debug.Log(option.displayName + " ist noch nicht freigeschaltet.");

            if (descriptionText != null)
                descriptionText.text = option.displayName + " ist noch nicht freigeschaltet.";

            return false;
        }

        bool success = false;

        if (option.optionType == PathBuildOptionType.NoTile)
        {
            success = tileManager.TryExtendPathTo(targetPosition);

            if (success)
                Debug.Log(verbauChoiceDisplayName + ": Kein Verbau gewaehlt, Weg normal erweitert.");
        }
        else if (option.optionType == PathBuildOptionType.PathTile)
        {
            success = tileManager.TryExtendPathTo(targetPosition);
        }
        else if (ShouldPlaceAsV1PathVariant(option.optionType))
        {
            success = tileManager.TryExtendPathToWithOption(targetPosition, option);

            if (success)
                Debug.Log(option.optionType + " platziert als V1 Path-Variante.");
        }
        else if (IsSpecialPathOption(option.optionType))
        {
            success = tileManager.TryExtendSpecialPathTo(targetPosition, option.optionType);
        }
        else if (option.optionType == PathBuildOptionType.GoldTile)
        {
            success = tileManager.TryBuildGoldTileAt(targetPosition);
        }
        else if (IsSupportTileOption(option.optionType))
        {
            success = tileManager.TryBuildSupportTileAt(targetPosition, option.optionType);
        }
        else
        {
            Debug.Log(option.displayName + " gewaehlt. Funktion kommt spaeter.");
        }

        if (success)
        {
            ConsumeSpecialTileChoiceIfActive();
            ConsumeVerbauChoiceIfActive();
            optionsGeneratedForCurrentBuildPhase = false;
            CloseChoiceUI();
            return true;
        }

        if (descriptionText != null)
            descriptionText.text = option.displayName + " konnte hier nicht gebaut werden.";

        return false;
    }

    public void AddEliteTileQualityBoosts(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        pendingEliteTileQualityBoosts = Mathf.Max(0, pendingEliteTileQualityBoosts + safeAmount);
        optionsGeneratedForCurrentBuildPhase = false;
        Debug.Log("Elite-Reward: +" + safeAmount + " Qualitätsladung(en) für spätere Tile-Auswahlen vorgemerkt.");
    }

    private void GenerateCurrentOptions()
    {
        if (verbauChoiceActive)
        {
            GenerateVerbauChoiceOptions();
            return;
        }

        currentOptions[0] = CreatePathTileOption();

        List<PathBuildOption> optionPool = CreateSpecialOptionPool();

        if (TryApplyPendingEliteQualityOptions(optionPool))
            return;

        currentOptions[1] = DrawRandomOptionFromPool(optionPool);
        currentOptions[2] = DrawRandomOptionFromPool(optionPool);
    }

    private PathBuildOption CreatePathTileOption()
    {
        return new PathBuildOption
        {
            displayName = "Path Tile",
            description = GetTileChoiceDescription(PathBuildOptionType.PathTile),
            optionType = PathBuildOptionType.PathTile
        };
    }

    private void GenerateVerbauChoiceOptions()
    {
        List<PathBuildOption> optionPool = CreateVerbauChoiceOptionPool();

        if (optionPool.Count <= 0)
        {
            currentOptions[0] = CreateNoVerbauOption();
            currentOptions[1] = CreateNoVerbauOption();
            currentOptions[2] = CreateNoVerbauOption();
            return;
        }

        currentOptions[0] = DrawRandomOptionFromPool(optionPool);
        currentOptions[1] = optionPool.Count > 0
            ? DrawRandomOptionFromPool(optionPool)
            : ClonePathBuildOption(currentOptions[0]);
        currentOptions[2] = CreateNoVerbauOption();
    }

    private List<PathBuildOption> CreateSpecialOptionPool()
    {
        List<PathBuildOption> optionPool = new List<PathBuildOption>();

        if (randomOptions != null)
        {
            foreach (PathBuildOption option in randomOptions)
            {
                if (IsSupportedSpecialOption(option) && IsPathBuildOptionUnlocked(option.optionType))
                    AddOptionIfTypeMissing(optionPool, CreateDisplayOption(option));
            }
        }

        AddMissingDefaultSpecialOptions(optionPool);

        if (optionPool.Count == 0)
            optionPool.Add(CreateFallbackSpecialOption());

        return optionPool;
    }

    private bool TryApplyPendingEliteQualityOptions(List<PathBuildOption> fallbackOptionPool)
    {
        if (pendingEliteTileQualityBoosts <= 0)
            return false;

        List<PathBuildOption> qualityPool = CreateEliteQualityOptionPool();

        if (qualityPool.Count == 0)
        {
            Debug.Log("Elite-Reward: Auswahlqualität ist vorgemerkt, aber der Qualitäts-Pool enthält noch keine Tiles.");
            return false;
        }

        currentOptions[1] = DrawRandomOptionFromPool(qualityPool, true);
        RemoveOptionTypeFromPool(fallbackOptionPool, currentOptions[1]);

        currentOptions[2] = qualityPool.Count > 0
            ? DrawRandomOptionFromPool(qualityPool, true)
            : DrawRandomOptionFromPool(fallbackOptionPool);

        pendingEliteTileQualityBoosts = Mathf.Max(0, pendingEliteTileQualityBoosts - 1);
        Debug.Log("Elite-Reward: Auswahlqualität auf diese Tile-Auswahl angewendet.");
        return true;
    }

    private List<PathBuildOption> CreateEliteQualityOptionPool()
    {
        List<PathBuildOption> optionPool = new List<PathBuildOption>();

        if (eliteQualityOptions != null)
        {
            foreach (PathBuildOption option in eliteQualityOptions)
            {
                if (IsSupportedSpecialOption(option) && IsPathBuildOptionUnlocked(option.optionType))
                    AddOptionIfTypeMissing(optionPool, CreateDisplayOption(option));
            }
        }

        foreach (PathBuildOption defaultOption in CreateDefaultSpecialOptions())
        {
            if (defaultOption == null || IsRemovedPathBuildSelectionOption(defaultOption.optionType))
                continue;

            PathBuildOptionRarity rarity = GetOptionRarity(defaultOption.optionType);
            if (rarity == PathBuildOptionRarity.Common)
                continue;

            if (IsPathBuildOptionUnlocked(defaultOption.optionType))
                AddOptionIfTypeMissing(optionPool, CreateDisplayOption(defaultOption));
        }

        return optionPool;
    }

    private bool HasAvailableVerbauChoiceOption()
    {
        return CreateVerbauChoiceOptionPool().Count > 0;
    }

    private List<PathBuildOption> CreateVerbauChoiceOptionPool()
    {
        List<PathBuildOption> optionPool = new List<PathBuildOption>();

        if (randomOptions != null)
        {
            foreach (PathBuildOption option in randomOptions)
            {
                if (option != null && IsVerbauChoiceOption(option.optionType) && IsPathBuildOptionUnlocked(option.optionType))
                    AddOptionIfTypeMissing(optionPool, CreateDisplayOption(option));
            }
        }

        foreach (PathBuildOption defaultOption in CreateDefaultSpecialOptions())
        {
            if (defaultOption == null || IsRemovedPathBuildSelectionOption(defaultOption.optionType))
                continue;

            if (IsVerbauChoiceOption(defaultOption.optionType) && IsPathBuildOptionUnlocked(defaultOption.optionType))
                AddOptionIfTypeMissing(optionPool, CreateDisplayOption(defaultOption));
        }

        if (optionPool.Count < 2)
            AddMissingDefaultVerbauOptions(optionPool, true);

        return optionPool;
    }

    private void AddMissingDefaultVerbauOptions(List<PathBuildOption> optionPool, bool ignoreUnlockState)
    {
        if (optionPool == null)
            return;

        foreach (PathBuildOption defaultOption in CreateDefaultSpecialOptions())
        {
            if (defaultOption == null || IsRemovedPathBuildSelectionOption(defaultOption.optionType))
                continue;

            if (!IsVerbauChoiceOption(defaultOption.optionType))
                continue;

            if (!ignoreUnlockState && !IsPathBuildOptionUnlocked(defaultOption.optionType))
                continue;

            AddOptionIfTypeMissing(optionPool, CreateDisplayOption(defaultOption));
        }
    }

    private void RemoveOptionTypeFromPool(List<PathBuildOption> optionPool, PathBuildOption selectedOption)
    {
        if (optionPool == null || selectedOption == null)
            return;

        for (int i = optionPool.Count - 1; i >= 0; i--)
        {
            PathBuildOption option = optionPool[i];

            if (option != null && option.optionType == selectedOption.optionType)
                optionPool.RemoveAt(i);
        }
    }

    private PathBuildOption DrawRandomOptionFromPool(List<PathBuildOption> optionPool, bool eliteQualityBias = false)
    {
        if (optionPool == null || optionPool.Count == 0)
            return CreateFallbackSpecialOption();

        int randomIndex = GetWeightedRandomOptionIndex(optionPool, eliteQualityBias);
        PathBuildOption option = optionPool[randomIndex];
        optionPool.RemoveAt(randomIndex);
        return option;
    }

    private int GetWeightedRandomOptionIndex(List<PathBuildOption> optionPool, bool eliteQualityBias)
    {
        if (optionPool == null || optionPool.Count == 0)
            return 0;

        int totalWeight = 0;
        for (int i = 0; i < optionPool.Count; i++)
            totalWeight += Mathf.Max(1, GetOptionWeight(optionPool[i], eliteQualityBias));

        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        int accumulated = 0;

        for (int i = 0; i < optionPool.Count; i++)
        {
            accumulated += Mathf.Max(1, GetOptionWeight(optionPool[i], eliteQualityBias));
            if (roll < accumulated)
                return i;
        }

        return optionPool.Count - 1;
    }

    private int GetOptionWeight(PathBuildOption option, bool eliteQualityBias)
    {
        if (option == null)
            return 1;

        PathBuildOptionRarity rarity = GetOptionRarity(option.optionType);

        if (eliteQualityBias)
        {
            switch (rarity)
            {
                case PathBuildOptionRarity.Legendary:
                    return 5;
                case PathBuildOptionRarity.Rare:
                    return 4;
                default:
                    return 1;
            }
        }

        switch (rarity)
        {
            case PathBuildOptionRarity.Common:
                return 8;
            case PathBuildOptionRarity.Rare:
                return 3;
            case PathBuildOptionRarity.Legendary:
                return 1;
            default:
                return 1;
        }
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
            description = GetTileChoiceDescription(option.optionType),
            optionType = option.optionType
        };
    }

    private string GetTileChoiceDescription(PathBuildOptionType optionType)
    {
        switch (optionType)
        {
            case PathBuildOptionType.NoTile:
                return "Auswahl ueberspringen.";
            case PathBuildOptionType.PathTile:
                return "Weg wird erweitert";
            case PathBuildOptionType.GoldTile:
                return "+3% Gold und +10% Gold pro Tower-Kill";
            case PathBuildOptionType.DamageTile:
                return "Tower +25% Damage";
            case PathBuildOptionType.RangeTile:
                return "Tower +1 Reichweite";
            case PathBuildOptionType.SlowTile:
                return "Gegner werden kurz verlangsamt";
            case PathBuildOptionType.TrapTile:
                return "Gegner bluten für 20s";
            case PathBuildOptionType.KnockTile:
                return "Gegner werden zurückgeworfen";
            case PathBuildOptionType.WeakpointTile:
                return "Gegner verlieren kurz Rüstung";
            case PathBuildOptionType.RateTile:
                return "Tower +20% Feuerrate";
            case PathBuildOptionType.XPTile:
                return "+25% XP pro Tower-Kill";
            case PathBuildOptionType.HealTile:
                return "+1 Lebenschance pro Tower-Kill";
            case PathBuildOptionType.ComboTile:
                return "Darkness: 20 Schaden alle 3s";
            case PathBuildOptionType.UpgradeTile:
                return "Point-Upgrades +1 effektiver";
            default:
                return "";
        }
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
            if (defaultOption == null || IsRemovedPathBuildSelectionOption(defaultOption.optionType))
                continue;

            if (IsPathBuildOptionUnlocked(defaultOption.optionType))
                AddOptionIfTypeMissing(optionPool, CreateDisplayOption(defaultOption));
        }
    }

    private PathBuildOption CreateFallbackSpecialOption()
    {
        PathBuildOption fallback = GetDefaultSpecialOption(PathBuildOptionType.SlowTile);
        if (fallback != null)
            return CreateDisplayOption(fallback);

        return new PathBuildOption
        {
            displayName = "Slow Tile",
            description = GetTileChoiceDescription(PathBuildOptionType.SlowTile),
            optionType = PathBuildOptionType.SlowTile
        };
    }

    private PathBuildOption CreateNoVerbauOption()
    {
        return new PathBuildOption
        {
            displayName = string.IsNullOrWhiteSpace(noVerbauDisplayName) ? "Kein Tile" : noVerbauDisplayName,
            description = GetTileChoiceDescription(PathBuildOptionType.NoTile),
            optionType = PathBuildOptionType.NoTile
        };
    }

    private PathBuildOption ClonePathBuildOption(PathBuildOption option)
    {
        if (option == null)
            return CreateNoVerbauOption();

        return new PathBuildOption
        {
            displayName = option.displayName,
            description = option.description,
            optionType = option.optionType
        };
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
               option.optionType == PathBuildOptionType.ComboTile ||
               option.optionType == PathBuildOptionType.HealTile ||
               option.optionType == PathBuildOptionType.WeakpointTile;
    }

    private bool IsVerbauChoiceOption(PathBuildOptionType optionType)
    {
        return optionType == PathBuildOptionType.GoldTile ||
               IsSupportTileOption(optionType);
    }

    private List<PathBuildOption> CreateDefaultSpecialOptions()
    {
        return new List<PathBuildOption>
        {
            new PathBuildOption
            {
                displayName = "Gold Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.GoldTile),
                optionType = PathBuildOptionType.GoldTile
            },
            new PathBuildOption
            {
                displayName = "Trap Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.TrapTile),
                optionType = PathBuildOptionType.TrapTile
            },
            new PathBuildOption
            {
                displayName = "Slow Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.SlowTile),
                optionType = PathBuildOptionType.SlowTile
            },
            new PathBuildOption
            {
                displayName = "Knock Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.KnockTile),
                optionType = PathBuildOptionType.KnockTile
            },
            new PathBuildOption
            {
                displayName = "Range Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.RangeTile),
                optionType = PathBuildOptionType.RangeTile
            },
            new PathBuildOption
            {
                displayName = "Damage Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.DamageTile),
                optionType = PathBuildOptionType.DamageTile
            },
            new PathBuildOption
            {
                displayName = "Rate Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.RateTile),
                optionType = PathBuildOptionType.RateTile
            },
            new PathBuildOption
            {
                displayName = "XP Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.XPTile),
                optionType = PathBuildOptionType.XPTile
            },
            new PathBuildOption
            {
                displayName = "Upgrade Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.UpgradeTile),
                optionType = PathBuildOptionType.UpgradeTile
            },
            new PathBuildOption
            {
                displayName = "Heal Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.HealTile),
                optionType = PathBuildOptionType.HealTile
            },
            new PathBuildOption
            {
                displayName = "Weakpoint Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.WeakpointTile),
                optionType = PathBuildOptionType.WeakpointTile
            },
            new PathBuildOption
            {
                displayName = "Combo Tile",
                description = GetTileChoiceDescription(PathBuildOptionType.ComboTile),
                optionType = PathBuildOptionType.ComboTile
            }
        };
    }

    private PathBuildOptionRarity GetOptionRarity(PathBuildOptionType optionType)
    {
        switch (optionType)
        {
            case PathBuildOptionType.PathTile:
            case PathBuildOptionType.NoTile:
            case PathBuildOptionType.GoldTile:
            case PathBuildOptionType.SlowTile:
                return PathBuildOptionRarity.Common;
            case PathBuildOptionType.TrapTile:
            case PathBuildOptionType.RangeTile:
            case PathBuildOptionType.DamageTile:
            case PathBuildOptionType.RateTile:
            case PathBuildOptionType.KnockTile:
            case PathBuildOptionType.WeakpointTile:
                return PathBuildOptionRarity.Rare;
            case PathBuildOptionType.XPTile:
            case PathBuildOptionType.UpgradeTile:
            case PathBuildOptionType.ComboTile:
            case PathBuildOptionType.HealTile:
                return PathBuildOptionRarity.Legendary;
            default:
                return PathBuildOptionRarity.Common;
        }
    }

    private bool IsPathBuildOptionUnlocked(PathBuildOptionType optionType)
    {
        if (optionType == PathBuildOptionType.NoTile)
            return true;

        ResolveGameManagerReference();
        if (gameManager != null && gameManager.IsDevGameMode())
            return true;

        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        return generalMeta == null || generalMeta.IsTileUnlocked(optionType);
    }

    private GeneralMetaProgressionManager GetGeneralMetaProgressionManager()
    {
        ResolveGameManagerReference();
        return gameManager != null ? gameManager.GetGeneralMetaProgressionManager() : GeneralMetaProgressionManager.GetOrCreate();
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

    private void SetChoiceButtonsInteractable(bool interactable)
    {
        if (optionButton1 != null)
            optionButton1.interactable = interactable;

        if (optionButton2 != null)
            optionButton2.interactable = interactable;

        if (optionButton3 != null)
            optionButton3.interactable = interactable;
    }

    private void HandleHotkeys()
    {
        if (IsInputLockedByModalUI() && !CanIgnorePathLockForImmediateVerbauChoice())
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
        if (IsInputLockedByModalUI() && !CanIgnorePathLockForImmediateVerbauChoice())
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

        if (immediateVerbauChoiceActive)
        {
            ChooseImmediateVerbauOption(option);
            return;
        }

        TryApplyPathBuildOptionAtTarget(option, hoveredGridPosition, true);
    }

    private void ChooseImmediateVerbauOption(PathBuildOption option)
    {
        if (option == null)
            return;

        if (!IsVerbauChoiceOption(option.optionType) && !IsPathBuildOptionUnlocked(option.optionType))
        {
            if (descriptionText != null)
                descriptionText.text = option.displayName + " ist noch nicht freigeschaltet.";

            return;
        }

        if (option.optionType == PathBuildOptionType.NoTile)
        {
            CompleteImmediateVerbauChoice();
            return;
        }

        if (!IsImmediateSupportTileOption(option.optionType))
        {
            if (descriptionText != null)
                descriptionText.text = option.displayName + " kann in dieser Verbauauswahl nicht platziert werden.";

            return;
        }

        pendingSupportTilePlacementOption = ClonePathBuildOption(option);
        choiceOpen = false;
        verbauChoiceActive = false;
        supportTilePlacementModeActive = true;
        SetChoiceButtonsInteractable(false);

        if (buildManager != null)
            buildManager.ClearCurrentSelection();

        if (tileManager != null)
        {
            tileManager.SetSupportTilePlacementBuildMode(true);
            tileManager.SetBuildTilesVisible(true);
        }

        ShowSupportTilePlacementPrompt();
    }

    private void HandleSupportTilePlacementMode()
    {
        if (pendingSupportTilePlacementOption == null)
        {
            CompleteImmediateVerbauChoice();
            return;
        }

        if (IsPointerOverUI())
        {
            SetHoveredSupportPlacementTile(null);
            return;
        }

        BuildTile buildTile = GetBuildTileUnderMouse();
        SetHoveredSupportPlacementTile(buildTile);

        if (!Input.GetMouseButtonDown(0))
            return;

        if (buildTile == null)
        {
            ShowSupportTilePlacementPrompt("Waehle ein freies Build-Feld.");
            return;
        }

        Vector2Int gridPosition = tileManager.WorldToGridPublic(buildTile.transform.position);
        PathBuildOptionType tileType = pendingSupportTilePlacementOption.optionType;

        if (!tileManager.TryBuildSupportTileOnBuildTile(gridPosition, tileType))
        {
            ShowSupportTilePlacementPrompt("Dieses Feld ist nicht gueltig.");
            return;
        }

        Debug.Log(verbauChoiceDisplayName + ": " + pendingSupportTilePlacementOption.displayName + " platziert.");
        CompleteImmediateVerbauChoice();
    }

    private BuildTile GetBuildTileUnderMouse()
    {
        if (mainCamera == null)
            return null;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return null;

        return hit.collider.GetComponent<BuildTile>();
    }

    private void SetHoveredSupportPlacementTile(BuildTile buildTile)
    {
        if (hoveredSupportPlacementTile == buildTile)
            return;

        if (hoveredSupportPlacementTile != null)
            hoveredSupportPlacementTile.SetHovered(false);

        hoveredSupportPlacementTile = buildTile;

        if (hoveredSupportPlacementTile != null)
            hoveredSupportPlacementTile.SetHovered(true);
    }

    private void ShowSupportTilePlacementPrompt(string overrideText = "")
    {
        string tileName = pendingSupportTilePlacementOption != null ? pendingSupportTilePlacementOption.displayName : "Tile";

        if (optionText1 != null)
            optionText1.text = tileName;

        if (optionText2 != null)
            optionText2.text = "Build-Feld waehlen";

        if (optionText3 != null)
            optionText3.text = "Tile platzieren";

        if (descriptionText != null)
            descriptionText.text = string.IsNullOrWhiteSpace(overrideText)
                ? "Platziere " + tileName + " auf einem freien Build-Feld."
                : overrideText;

        if (pathTopBar != null)
            pathTopBar.SetActive(true);
    }

    private void CompleteImmediateVerbauChoice()
    {
        bool shouldResumePostWave = immediateVerbauChoiceCompletesPostWave;

        choiceOpen = false;
        specialTileChoiceActive = false;
        verbauChoiceActive = false;
        immediateVerbauChoiceActive = false;
        immediateVerbauChoiceCompletesPostWave = false;
        supportTilePlacementModeActive = false;
        pendingSupportTilePlacementOption = null;
        blockedVerbauChoicePending = false;

        SetHoveredSupportPlacementTile(null);
        SetChoiceButtonsInteractable(true);

        if (tileManager != null)
        {
            tileManager.SetSupportTilePlacementBuildMode(false);
            tileManager.SetBuildTilesVisible(false);
        }

        if (pathTopBar != null)
            pathTopBar.SetActive(false);

        if (currentGhost != null)
            currentGhost.SetActive(false);

        if (shouldResumePostWave && gameManager != null)
            gameManager.FinishPostWaveVerbauChoice();
    }

    private bool IsImmediateSupportTileOption(PathBuildOptionType optionType)
    {
        return optionType == PathBuildOptionType.GoldTile || IsSupportTileOption(optionType);
    }

    private bool IsSpecialPathOption(PathBuildOptionType optionType)
    {
        return optionType == PathBuildOptionType.TrapTile ||
               optionType == PathBuildOptionType.SlowTile ||
               optionType == PathBuildOptionType.KnockTile ||
               optionType == PathBuildOptionType.ComboTile ||
               optionType == PathBuildOptionType.WeakpointTile;
    }

    private bool ShouldPlaceAsV1PathVariant(PathBuildOptionType optionType)
    {
        if (!allowSpecialTilePlacementV1 || !specialTilesUsePathBehaviourInV1)
            return false;

        return optionType == PathBuildOptionType.TrapTile;
    }

    private bool IsSupportTileOption(PathBuildOptionType optionType)
    {
        return optionType == PathBuildOptionType.RangeTile ||
               optionType == PathBuildOptionType.DamageTile ||
               optionType == PathBuildOptionType.RateTile ||
               optionType == PathBuildOptionType.XPTile ||
               optionType == PathBuildOptionType.UpgradeTile ||
               optionType == PathBuildOptionType.HealTile;
    }

    private void ConsumeVerbauChoiceIfActive()
    {
        if (!verbauChoiceActive)
            return;

        Debug.Log(verbauChoiceDisplayName + " aus Verbau-Event abgeschlossen.");
        blockedVerbauChoicePending = false;
        verbauChoiceActive = false;
    }

    private void ConsumeSpecialTileChoiceIfActive()
    {
        if (!specialTileChoiceActive)
            return;

        specialTileChoicePending = false;
        specialTileChoiceActive = false;
    }

    public void CancelChoice()
    {
        bool wasSupportTilePlacementModeActive = supportTilePlacementModeActive;

        choiceOpen = false;
        hasValidHover = false;
        specialTileChoiceActive = false;
        verbauChoiceActive = false;
        immediateVerbauChoiceActive = false;
        immediateVerbauChoiceCompletesPostWave = false;
        supportTilePlacementModeActive = false;
        pendingSupportTilePlacementOption = null;
        SetHoveredSupportPlacementTile(null);
        SetChoiceButtonsInteractable(true);

        if (tileManager != null)
        {
            tileManager.SetSupportTilePlacementBuildMode(false);

            if (wasSupportTilePlacementModeActive)
                tileManager.SetBuildTilesVisible(false);
        }

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
        bool wasSupportTilePlacementModeActive = supportTilePlacementModeActive;

        choiceOpen = false;
        hasValidHover = false;
        specialTileChoiceActive = false;
        verbauChoiceActive = false;
        immediateVerbauChoiceActive = false;
        immediateVerbauChoiceCompletesPostWave = false;
        supportTilePlacementModeActive = false;
        pendingSupportTilePlacementOption = null;
        SetHoveredSupportPlacementTile(null);
        SetChoiceButtonsInteractable(true);

        if (tileManager != null)
        {
            tileManager.SetSupportTilePlacementBuildMode(false);

            if (wasSupportTilePlacementModeActive)
                tileManager.SetBuildTilesVisible(false);
        }

        ApplyChoiceUILayoutDefaults();

        if (pathTopBar != null)
            pathTopBar.SetActive(false);

        if (currentGhost != null)
            currentGhost.SetActive(false);
    }

    public bool IsChoiceOpen()
    {
        return choiceOpen || supportTilePlacementModeActive;
    }

    public bool IsImmediateVerbauChoiceOrPlacementActive()
    {
        return immediateVerbauChoiceActive || supportTilePlacementModeActive;
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
