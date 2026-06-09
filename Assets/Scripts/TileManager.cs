using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject startTilePrefab;
    public GameObject pathTilePrefab;
    public GameObject baseTilePrefab;
    public GameObject buildTilePrefab;

    [Header("Generated Tile Prefabs")]
    public GeneratedTilePrefabSet generatedTilePrefabSet;
    public bool useGeneratedTilePrefabs = true;
    public bool skipProceduralRailingsOnGeneratedTiles = true;
    public bool useGeneratedTileDirectRails = true;
    public bool invertGeneratedRailSideNames = true;
    public bool rotateGeneratedPathTileVisualsToFlow = true;
    public Vector3 generatedPathTileVisualEulerBase = Vector3.zero;
    public float generatedPathTileForwardYawOffset = 0f;
    public float generatedTileGroundLocalY = -0.05f;
    public GameObject trapTilePrefab;
    public GameObject slowTilePrefab;
    public GameObject knockTilePrefab;
    public GameObject comboTilePrefab;
    public GameObject goldTilePrefab;
    public GameObject rangeTilePrefab;
    public GameObject damageTilePrefab;
    public GameObject rateTilePrefab;
    public GameObject xpTilePrefab;
    public GameObject upgradeTilePrefab;
    public GameObject healTilePrefab;
    public GameObject weakpointTilePrefab;
    public GameObject blockedTilePrefab;

    [Header("Managers")]
    public GameManager gameManager;

    [Header("Settings")]
    public float tileSize = 1f;

    [Header("Run Initialization")]
    public bool createPathOnStart = false;

    [Header("World Visual Size")]
    public bool scaleWorldVisualOnStart = true;
    public Transform worldVisualRoot;
    public float worldVisualScaleMultiplier = 5f;

    [Header("Build Safety")]
    public bool reserveLastPathExtensionBuildTile = true;
    public string reservedPathExtensionWarning = "Letzte Weg-Erweiterung reserviert: Dort kann kein Tower gebaut werden.";
    public float reservedPathExtensionWarningDuration = 4f;

    [Header("Path Railing QoL")]
    public bool autoBuildPathRailings = true;
    public float pathRailingHeight = 0.18f;
    public float pathRailingThickness = 0.055f;
    public Color pathRailingColor = new Color32(58, 68, 82, 255);

    [Header("Tile Visuals V1")]
    public Color trapTileColor = new Color32(150, 25, 25, 255);
    public Color slowTileColor = new Color32(70, 180, 255, 255);
    public Color knockTileColor = new Color32(255, 170, 45, 255);
    public Color comboTileColor = new Color32(210, 80, 230, 255);
    public Color goldTileColor = new Color32(255, 210, 45, 255);
    public Color rangeTileColor = new Color32(65, 155, 255, 255);
    public Color damageTileColor = new Color32(235, 70, 70, 255);
    public Color rateTileColor = new Color32(75, 235, 130, 255);
    public Color xpTileColor = new Color32(155, 105, 255, 255);
    public Color upgradeTileColor = new Color32(255, 245, 120, 255);
    public Color healTileColor = new Color32(95, 225, 135, 255);
    public Color weakpointTileColor = new Color(0.1f, 0.17f, 0.25f, 1f);

    [Header("Blocked Event Base Tools V1")]
    public Color relocationGhostValidColor = new Color32(80, 220, 140, 190);
    public Color teleporterColor = new Color32(80, 210, 255, 255);
    public int teleporterSearchRadius = 5;

    private bool canBuild = true;
    private bool buildTilesVisible = false;

    private List<Vector2Int> pathPositions = new List<Vector2Int>();
    private Dictionary<Vector2Int, GameObject> pathTileObjects = new Dictionary<Vector2Int, GameObject>();
    private HashSet<Vector2Int> buildTilePositions = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> towerPositions = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> specialBlockedPositions = new HashSet<Vector2Int>();
    private List<GameObject> buildTileObjects = new List<GameObject>();
    private List<GameObject> specialTileObjects = new List<GameObject>();

    private Vector2Int startPosition;
    private Vector2Int basePosition;
    private Vector2Int currentDirection = Vector2Int.right;

    private GameObject currentStartTile;
    private GameObject currentBaseTile;

    private bool hasReservedPathExtension = false;
    private Vector2Int reservedPathExtensionPosition;
    private float reservedPathExtensionWarningTimer = 0f;
    private bool supportTilePlacementBuildModeActive = false;

    private bool baseRelocationModeActive = false;
    private bool worldVisualScaleApplied = false;
    private bool pathInitialized = false;
    private readonly List<Vector2Int> teleporterEntryPositions = new List<Vector2Int>();
    private readonly List<Vector2Int> teleporterExitPositions = new List<Vector2Int>();
    private readonly List<GameObject> teleporterObjects = new List<GameObject>();

    private void Awake()
    {
        ResolveGeneratedTilePrefabSetReference();
        ApplyGeneratedTilePrefabSetReferences();
    }

    private void Start()
    {
        ScaleWorldVisualIfNeeded();
        AbyssGroundRenderer.EnsureExists(this);

        if (createPathOnStart)
            InitializeRunPath();
    }

    private void Update()
    {
        if (reservedPathExtensionWarningTimer > 0f)
        {
            reservedPathExtensionWarningTimer -= Time.deltaTime;

            if (reservedPathExtensionWarningTimer < 0f)
                reservedPathExtensionWarningTimer = 0f;
        }
    }

    private void ScaleWorldVisualIfNeeded()
    {
        if (worldVisualScaleApplied)
            return;

        worldVisualScaleApplied = true;

        if (!scaleWorldVisualOnStart)
            return;

        if (worldVisualRoot == null)
            return;

        float safeMultiplier = Mathf.Max(1f, worldVisualScaleMultiplier);
        Vector3 scale = worldVisualRoot.localScale;
        scale.x *= safeMultiplier;
        scale.z *= safeMultiplier;
        worldVisualRoot.localScale = scale;
    }

    private void ResolveGeneratedTilePrefabSetReference()
    {
        if (!useGeneratedTilePrefabs || generatedTilePrefabSet != null)
            return;

        generatedTilePrefabSet = FindObjectOfType<GeneratedTilePrefabSet>();
    }

    private void ApplyGeneratedTilePrefabSetReferences()
    {
        if (!useGeneratedTilePrefabs || generatedTilePrefabSet == null)
            return;

        skipProceduralRailingsOnGeneratedTiles = true;
        useGeneratedTileDirectRails = true;
        invertGeneratedRailSideNames = true;
        generatedTileGroundLocalY = -0.05f;
        AssignGeneratedPrefabIfAvailable(ref pathTilePrefab, generatedTilePrefabSet.pathTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref startTilePrefab, generatedTilePrefabSet.startTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref baseTilePrefab, generatedTilePrefabSet.baseTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref buildTilePrefab, generatedTilePrefabSet.buildTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref trapTilePrefab, generatedTilePrefabSet.trapTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref slowTilePrefab, generatedTilePrefabSet.slowTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref knockTilePrefab, generatedTilePrefabSet.knockTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref comboTilePrefab, generatedTilePrefabSet.comboTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref goldTilePrefab, generatedTilePrefabSet.goldTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref rangeTilePrefab, generatedTilePrefabSet.rangeTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref damageTilePrefab, generatedTilePrefabSet.damageTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref rateTilePrefab, generatedTilePrefabSet.rateTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref xpTilePrefab, generatedTilePrefabSet.xpTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref upgradeTilePrefab, generatedTilePrefabSet.upgradeTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref healTilePrefab, generatedTilePrefabSet.healTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref weakpointTilePrefab, generatedTilePrefabSet.weakpointTilePrefab);
        AssignGeneratedPrefabIfAvailable(ref blockedTilePrefab, generatedTilePrefabSet.blockedTilePrefab);
    }

    private void AssignGeneratedPrefabIfAvailable(ref GameObject target, GameObject generatedPrefab)
    {
        if (generatedPrefab != null)
            target = generatedPrefab;
    }

    private void NormalizeGeneratedTileVisual(GameObject tileObject)
    {
        if (!useGeneratedTilePrefabs || tileObject == null)
            return;

        Transform visual = tileObject.transform.Find("Visual");

        if (visual == null)
            return;

        visual.localRotation = Quaternion.identity;
        Vector3 visualLocalPosition = visual.localPosition;
        visualLocalPosition.x = 0f;
        visualLocalPosition.z = 0f;
        visual.localPosition = visualLocalPosition;
        AlignGeneratedTileVisualToGround(tileObject, visual);
    }

    private void AlignGeneratedTileVisualToGround(GameObject tileObject, Transform visual)
    {
        if (tileObject == null || visual == null)
            return;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
            return;

        bool hasBounds = false;
        Bounds bounds = new Bounds();

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

        if (!hasBounds)
            return;

        float desiredMinY = tileObject.transform.position.y + generatedTileGroundLocalY;
        float deltaY = desiredMinY - bounds.min.y;

        if (Mathf.Abs(deltaY) > 0.001f)
            visual.position += Vector3.up * deltaY;
    }

    private void HideGeneratedTileRailsAndCorners(GameObject tileObject)
    {
        if (tileObject == null)
            return;

        SetChildrenActiveByNameContains(tileObject.transform, "Rail_", false);
        SetChildrenActiveByNameContains(tileObject.transform, "CornerPost", false);
        SetChildrenActiveByNameContains(tileObject.transform, "Corner_", false);
    }

    private void SetChildrenActiveByNameContains(Transform root, string namePart, bool active)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
            return;

        if (root.name.Contains(namePart))
            root.gameObject.SetActive(active);

        for (int i = 0; i < root.childCount; i++)
            SetChildrenActiveByNameContains(root.GetChild(i), namePart, active);
    }

    public void SetCanBuild(bool value)
    {
        canBuild = value;

        if (!canBuild)
        {
            SetBuildTilesVisible(false);
        }
    }

    private void CreateStartPath(int extraStartPathTiles)
    {
        if (pathInitialized)
            return;

        startPosition = new Vector2Int(0, 0);

        GameObject startPrefab = GetStartTilePrefab();
        if (startPrefab != null)
        {
            currentStartTile = Instantiate(startPrefab, GridToWorld(startPosition), Quaternion.identity);
            NormalizeGeneratedTileVisual(currentStartTile);
        }
        else
            Debug.LogError("TileManager: startTilePrefab fehlt.");

        pathPositions.Add(startPosition);

        int safeExtraStartPathTiles = Mathf.Clamp(extraStartPathTiles, 0, 2);
        int pathTileCount = 5 + safeExtraStartPathTiles;

        for (int x = 1; x <= pathTileCount; x++)
            AddPathTile(new Vector2Int(x, 0));

        ConfigureGeneratedStartTileRails();

        basePosition = new Vector2Int(pathTileCount + 1, 0);
        pathInitialized = true;
        PlaceBaseTile();
        RefreshPathRailings();

        RefreshBuildTiles();
    }

    public Vector2Int GetBasePosition()
    {
        return basePosition;
    }

    public Vector2Int GetCurrentDirection()
    {
        return currentDirection;
    }

    public bool IsRunPathInitialized()
    {
        return pathInitialized;
    }

    public bool InitializeRunPath()
    {
        return InitializeRunPath(0);
    }

    public bool InitializeRunPath(int extraStartPathTiles)
    {
        if (pathInitialized)
            return true;

        ScaleWorldVisualIfNeeded();
        CreateStartPath(extraStartPathTiles);

        AbyssGroundRenderer renderer = AbyssGroundRenderer.EnsureExists(this);
        if (renderer != null)
            renderer.ForceRefresh();

        return pathInitialized;
    }

    public Vector3 GridToWorldPublic(Vector2Int gridPosition)
    {
        return GridToWorld(gridPosition);
    }

    public Vector2Int WorldToGridPublic(Vector3 worldPosition)
    {
        return WorldToGrid(worldPosition);
    }

    public bool TryGetTeleporterExitWorld(Vector3 entryWorldPosition, out Vector3 exitWorldPosition)
    {
        exitWorldPosition = Vector3.zero;

        if (!pathInitialized)
            return false;

        if (teleporterEntryPositions == null || teleporterExitPositions == null)
            return false;

        Vector2Int entryPosition = WorldToGrid(entryWorldPosition);

        for (int i = teleporterEntryPositions.Count - 1; i >= 0; i--)
        {
            if (i >= teleporterExitPositions.Count)
                continue;

            if (teleporterEntryPositions[i] != entryPosition)
                continue;

            exitWorldPosition = GridToWorld(teleporterExitPositions[i]);
            return true;
        }

        return false;
    }

    public bool IsBuildAllowed()
    {
        return canBuild;
    }

    public bool CanExtendTo(Vector2Int targetPosition)
    {
        if (!pathInitialized)
            return false;

        if (!canBuild)
            return false;

        Vector2Int forward = basePosition + currentDirection;
        Vector2Int left = basePosition + new Vector2Int(-currentDirection.y, currentDirection.x);
        Vector2Int right = basePosition + new Vector2Int(currentDirection.y, -currentDirection.x);

        bool isAllowedDirection =
            targetPosition == forward ||
            targetPosition == left ||
            targetPosition == right;

        if (!isAllowedDirection)
            return false;

        return IsValidBasePosition(targetPosition);
    }

    public bool IsBaseRelocationModeActive()
    {
        return baseRelocationModeActive;
    }

    public void SetBaseRelocationModeActive(bool active)
    {
        baseRelocationModeActive = active;
        SetBuildTilesVisible(active);
    }

    public bool CanRelocateBaseTo(Vector2Int targetPosition)
    {
        if (!baseRelocationModeActive)
            return false;

        return IsValidBaseRelocationPosition(targetPosition);
    }

    public bool TryRelocateBaseTo(Vector2Int newBasePosition)
    {
        if (!CanRelocateBaseTo(newBasePosition))
        {
            ShowBuildWarning("Neue Basis kann nur auf ein gültiges Build-Feld neben bestehendem Weg gesetzt werden.");
            return false;
        }

        if (!TryFindBestNeighborPathIndex(newBasePosition, out int anchorIndex))
        {
            ShowBuildWarning("Neue Basis braucht Anschluss an einen bestehenden Weg.");
            return false;
        }

        RelocateBaseToAnchor(newBasePosition, anchorIndex, true);
        baseRelocationModeActive = false;
        return true;
    }

    public bool TryCreateTeleporterBase(int searchRadius)
    {
        int safeRadius = Mathf.Max(1, searchRadius);
        List<Vector2Int> candidates = GetTeleporterBaseCandidates(safeRadius);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("Teleporter: Keine sichere Position gefunden.");
            return false;
        }

        Vector2Int oldBasePosition = basePosition;
        Vector2Int newBasePosition = candidates[Random.Range(0, candidates.Count)];

        if (!TryFindNearestPathIndex(newBasePosition, out int anchorIndex))
            anchorIndex = Mathf.Max(0, pathPositions.Count - 1);

        teleporterEntryPositions.Add(oldBasePosition);
        teleporterExitPositions.Add(newBasePosition);
        CreateTeleporterVisual(oldBasePosition, newBasePosition);

        basePosition = newBasePosition;
        currentDirection = GetSafeDirectionFromAnchor(anchorIndex, newBasePosition);
        PlaceBaseTile();
        RefreshPathRailings();
        RefreshBuildTiles();
        return true;
    }

    public bool TryExtendPathTo(Vector2Int newBasePosition)
    {
        return TryExtendPathTo(newBasePosition, PathBuildOptionType.PathTile);
    }

    public bool TryExtendPathToWithOption(Vector2Int newBasePosition, PathBuildOption option)
    {
        if (option == null)
            return TryExtendPathTo(newBasePosition);

        return TryExtendPathToWithOption(newBasePosition, option.optionType);
    }

    public bool TryExtendPathToWithOption(Vector2Int newBasePosition, PathBuildOptionType optionType)
    {
        if (!IsPathVariantTileType(optionType))
        {
            Debug.LogWarning("TileManager: " + optionType + " ist keine sichere V1 Path-Variante.");
            return false;
        }

        return TryExtendPathTo(newBasePosition, optionType);
    }

    public bool TryExtendSpecialPathTo(Vector2Int newBasePosition, PathBuildOptionType specialTileType)
    {
        if (!IsSpecialPathTileType(specialTileType))
        {
            Debug.LogWarning("Ungültiger Spezial-PathTile-Typ: " + specialTileType);
            return false;
        }

        return TryExtendPathTo(newBasePosition, specialTileType);
    }

    private bool IsSpecialPathTileType(PathBuildOptionType specialTileType)
    {
        return specialTileType == PathBuildOptionType.TrapTile ||
               specialTileType == PathBuildOptionType.SlowTile ||
               specialTileType == PathBuildOptionType.KnockTile ||
               specialTileType == PathBuildOptionType.ComboTile ||
               specialTileType == PathBuildOptionType.WeakpointTile;
    }

    private bool IsPathVariantTileType(PathBuildOptionType tileType)
    {
        return tileType == PathBuildOptionType.PathTile ||
               tileType == PathBuildOptionType.TrapTile ||
               tileType == PathBuildOptionType.GoldTile ||
               tileType == PathBuildOptionType.SlowTile ||
               tileType == PathBuildOptionType.KnockTile ||
               tileType == PathBuildOptionType.ComboTile ||
               tileType == PathBuildOptionType.WeakpointTile;
    }

    public bool TryBuildGoldTileAt(Vector2Int goldTilePosition)
    {
        if (TryBuildGoldTileAsSupportTile(goldTilePosition, out bool success))
            return success;

        if (!CanExtendTo(goldTilePosition))
        {
            Debug.Log("Ungültige GoldTile-Position!");
            return false;
        }

        if (specialBlockedPositions.Contains(goldTilePosition))
            return false;

        if (!HasAlternativeValidExtension(goldTilePosition))
        {
            ShowBuildWarning("Diese Spezial-Kachel würde die letzte Weg-Erweiterung blockieren.");
            return false;
        }

        specialBlockedPositions.Add(goldTilePosition);
        CreateGoldTile(goldTilePosition);
        RefreshBuildTiles();

        if (gameManager != null)
            gameManager.OnPathExtended();

        return true;
    }

    private bool TryBuildGoldTileAsSupportTile(Vector2Int goldTilePosition, out bool success)
    {
        if (!IsTowerSupportTileType(PathBuildOptionType.GoldTile))
        {
            success = false;
            return false;
        }

        success = TryBuildSupportTileAt(goldTilePosition, PathBuildOptionType.GoldTile);

        if (success && gameManager != null)
            gameManager.RegisterGoldTileBuilt();

        return true;
    }


    public bool TryBuildSupportTileAt(Vector2Int supportTilePosition, PathBuildOptionType supportTileType)
    {
        if (!IsTowerSupportTileType(supportTileType))
        {
            Debug.LogWarning("Ungültiger Support-Tile-Typ: " + supportTileType);
            return false;
        }

        if (!CanExtendTo(supportTilePosition))
        {
            Debug.Log("Ungültige SupportTile-Position!");
            return false;
        }

        if (specialBlockedPositions.Contains(supportTilePosition))
            return false;

        if (!HasAlternativeValidExtension(supportTilePosition))
        {
            ShowBuildWarning("Diese Support-Kachel würde die letzte Weg-Erweiterung blockieren.");
            return false;
        }

        specialBlockedPositions.Add(supportTilePosition);
        CreateSupportTile(supportTilePosition, supportTileType);
        RefreshBuildTiles();

        if (gameManager != null)
            gameManager.OnPathExtended();

        return true;
    }

    public bool CanBuildSupportTileOnBuildTile(Vector2Int supportTilePosition, PathBuildOptionType supportTileType)
    {
        if (!IsTowerSupportTileType(supportTileType))
            return false;

        if (!pathInitialized || !canBuild)
            return false;

        if (!buildTilePositions.Contains(supportTilePosition))
            return false;

        if (IsPositionBlocked(supportTilePosition))
            return false;

        if (IsReservedPathExtensionPosition(supportTilePosition) && !supportTilePlacementBuildModeActive)
            return false;

        return true;
    }

    public bool TryBuildSupportTileOnBuildTile(Vector2Int supportTilePosition, PathBuildOptionType supportTileType)
    {
        if (!CanBuildSupportTileOnBuildTile(supportTilePosition, supportTileType))
        {
            ShowBuildWarning("Dieses Feld kann kein Verbau-Tile aufnehmen.");
            return false;
        }

        specialBlockedPositions.Add(supportTilePosition);
        CreateSupportTile(supportTilePosition, supportTileType);

        if (supportTileType == PathBuildOptionType.GoldTile && gameManager != null)
            gameManager.RegisterGoldTileBuilt();

        RefreshBuildTiles();
        return true;
    }

    private bool IsTowerSupportTileType(PathBuildOptionType tileType)
    {
        return tileType == PathBuildOptionType.RangeTile ||
               tileType == PathBuildOptionType.DamageTile ||
               tileType == PathBuildOptionType.RateTile ||
               tileType == PathBuildOptionType.XPTile ||
               tileType == PathBuildOptionType.UpgradeTile ||
               tileType == PathBuildOptionType.GoldTile ||
               tileType == PathBuildOptionType.HealTile;
    }

    private bool TryExtendPathTo(Vector2Int newBasePosition, PathBuildOptionType pathTileType)
    {
        if (!CanExtendTo(newBasePosition))
        {
            Debug.Log("Ungültige Path-Erweiterung!");
            return false;
        }

        Vector2Int oldBasePosition = basePosition;
        Vector2Int newDirection = newBasePosition - basePosition;
        currentDirection = newDirection;

        if (!pathPositions.Contains(oldBasePosition))
        {
            AddPathTile(oldBasePosition, pathTileType);
        }

        basePosition = newBasePosition;
        PlaceBaseTile();
        RefreshPathRailings();

        RefreshBuildTiles();

        if (gameManager != null)
        {
            gameManager.OnPathExtended();
        }

        return true;
    }

    public void ExtendPathTo(Vector2Int newBasePosition)
    {
        TryExtendPathTo(newBasePosition);
    }

    public bool HasAnyValidExtension()
    {
        if (!pathInitialized)
            return false;

        return GetValidExtensionPositions().Count > 0;
    }

    public Vector2Int[] GetPossibleExtensionPositions()
    {
        if (!pathInitialized)
            return new Vector2Int[0];

        Vector2Int forward = basePosition + currentDirection;
        Vector2Int left = basePosition + new Vector2Int(-currentDirection.y, currentDirection.x);
        Vector2Int right = basePosition + new Vector2Int(currentDirection.y, -currentDirection.x);

        return new Vector2Int[]
        {
            forward,
            left,
            right
        };
    }

    public bool IsPositionBlocked(Vector2Int position)
    {
        if (position == startPosition)
            return true;

        if (position == basePosition)
            return true;

        if (pathPositions.Contains(position))
            return true;

        if (towerPositions.Contains(position))
            return true;

        if (specialBlockedPositions.Contains(position))
            return true;

        return false;
    }

    public bool IsReservedPathExtensionPosition(Vector2Int position)
    {
        return hasReservedPathExtension && position == reservedPathExtensionPosition;
    }

    public bool CanPlaceTowerOnBuildTile(Vector3 worldPosition)
    {
        Vector2Int gridPosition = WorldToGrid(worldPosition);

        if (IsReservedPathExtensionPosition(gridPosition))
            return false;

        return true;
    }

    public void ShowBuildWarning(string message)
    {
        if (!string.IsNullOrEmpty(message))
            reservedPathExtensionWarning = message;

        ShowBuildRestrictionWarning();
        Debug.LogWarning(reservedPathExtensionWarning);
    }

    public string GetBuildWarningText()
    {
        return GetBuildRestrictionWarningText();
    }

    public void ShowBuildRestrictionWarning()
    {
        reservedPathExtensionWarningTimer = Mathf.Max(0.1f, reservedPathExtensionWarningDuration);
    }

    public string GetBuildRestrictionWarningText()
    {
        if (!hasReservedPathExtension)
            return "";

        if (reservedPathExtensionWarningTimer <= 0f)
            return "";

        return reservedPathExtensionWarning;
    }

    private List<Vector2Int> GetValidExtensionPositions()
    {
        List<Vector2Int> validPositions = new List<Vector2Int>();
        Vector2Int[] possiblePositions = GetPossibleExtensionPositions();

        foreach (Vector2Int position in possiblePositions)
        {
            if (IsValidBasePosition(position))
            {
                validPositions.Add(position);
            }
        }

        return validPositions;
    }

    private void UpdateReservedPathExtension()
    {
        hasReservedPathExtension = false;

        if (!reserveLastPathExtensionBuildTile)
            return;

        List<Vector2Int> validExtensions = GetValidExtensionPositions();

        if (validExtensions.Count != 1)
            return;

        reservedPathExtensionPosition = validExtensions[0];
        hasReservedPathExtension = true;
        ShowBuildRestrictionWarning();
    }

    private bool IsValidBaseRelocationPosition(Vector2Int position)
    {
        if (position == basePosition || position == startPosition)
            return false;

        if (!buildTilePositions.Contains(position))
            return false;

        if (towerPositions.Contains(position) || specialBlockedPositions.Contains(position) || pathPositions.Contains(position))
            return false;

        return HasNeighborPath(position);
    }

    private bool HasNeighborPath(Vector2Int position)
    {
        return TryFindBestNeighborPathIndex(position, out _);
    }

    private bool TryFindBestNeighborPathIndex(Vector2Int position, out int pathIndex)
    {
        pathIndex = -1;

        if (pathPositions == null)
            return false;

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int i = 0; i < pathPositions.Count; i++)
        {
            foreach (Vector2Int direction in directions)
            {
                if (pathPositions[i] + direction == position && i > pathIndex)
                    pathIndex = i;
            }
        }

        return pathIndex >= 0;
    }

    private bool TryFindNearestPathIndex(Vector2Int position, out int pathIndex)
    {
        pathIndex = -1;

        if (pathPositions == null || pathPositions.Count == 0)
            return false;

        int bestDistance = int.MaxValue;

        for (int i = 0; i < pathPositions.Count; i++)
        {
            int distance = Mathf.Abs(pathPositions[i].x - position.x) + Mathf.Abs(pathPositions[i].y - position.y);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                pathIndex = i;
            }
        }

        return pathIndex >= 0;
    }

    private bool IsValidBasePosition(Vector2Int position)
    {
        if (position == basePosition)
            return false;

        if (position == startPosition)
            return false;

        if (pathPositions.Contains(position))
            return false;

        if (towerPositions.Contains(position))
            return false;

        if (specialBlockedPositions.Contains(position))
            return false;

        return true;
    }


    private bool HasAlternativeValidExtension(Vector2Int blockedPosition)
    {
        List<Vector2Int> validExtensions = GetValidExtensionPositions();

        foreach (Vector2Int position in validExtensions)
        {
            if (position != blockedPosition)
                return true;
        }

        return false;
    }

    private void RelocateBaseToAnchor(Vector2Int newBasePosition, int anchorIndex, bool destroyOldRouteTail)
    {
        int safeAnchorIndex = Mathf.Clamp(anchorIndex, 0, Mathf.Max(0, pathPositions.Count - 1));
        List<Vector2Int> removedPathPositions = new List<Vector2Int>();

        if (destroyOldRouteTail)
        {
            for (int i = pathPositions.Count - 1; i > safeAnchorIndex; i--)
            {
                Vector2Int removedPosition = pathPositions[i];
                removedPathPositions.Add(removedPosition);
                pathPositions.RemoveAt(i);

                if (pathTileObjects.TryGetValue(removedPosition, out GameObject pathObject) && pathObject != null)
                    Destroy(pathObject);

                pathTileObjects.Remove(removedPosition);
            }

            DestroyTowersAroundRemovedPath(removedPathPositions);
            RemoveSpecialBlocksAroundRemovedPath(removedPathPositions);
        }

        basePosition = newBasePosition;
        currentDirection = GetSafeDirectionFromAnchor(safeAnchorIndex, newBasePosition);
        PlaceBaseTile();
        RefreshPathRailings();
        RefreshBuildTiles();
    }

    private Vector2Int GetSafeDirectionFromAnchor(int anchorIndex, Vector2Int targetPosition)
    {
        if (pathPositions == null || pathPositions.Count == 0)
            return Vector2Int.right;

        int safeAnchorIndex = Mathf.Clamp(anchorIndex, 0, pathPositions.Count - 1);
        Vector2Int direction = targetPosition - pathPositions[safeAnchorIndex];

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return new Vector2Int(direction.x > 0 ? 1 : -1, 0);

        if (direction.y != 0)
            return new Vector2Int(0, direction.y > 0 ? 1 : -1);

        return currentDirection == Vector2Int.zero ? Vector2Int.right : currentDirection;
    }

    private void DestroyTowersAroundRemovedPath(List<Vector2Int> removedPathPositions)
    {
        if (removedPathPositions == null || removedPathPositions.Count == 0)
            return;

        HashSet<Vector2Int> affectedBuildPositions = GetBuildPositionsAround(removedPathPositions);
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Tower tower in towers)
        {
            if (tower == null)
                continue;

            Vector2Int towerGridPosition = tower.hasBuildGridPosition ? tower.builtGridPosition : WorldToGrid(tower.transform.position);

            if (!affectedBuildPositions.Contains(towerGridPosition))
                continue;

            if (HasNeighborPath(towerGridPosition))
                continue;

            towerPositions.Remove(towerGridPosition);
            Destroy(tower.gameObject);
        }
    }

    private void RemoveSpecialBlocksAroundRemovedPath(List<Vector2Int> removedPathPositions)
    {
        if (removedPathPositions == null || removedPathPositions.Count == 0)
            return;

        HashSet<Vector2Int> affectedBuildPositions = GetBuildPositionsAround(removedPathPositions);

        foreach (Vector2Int position in affectedBuildPositions)
        {
            if (!HasNeighborPath(position))
                specialBlockedPositions.Remove(position);
        }

        for (int i = specialTileObjects.Count - 1; i >= 0; i--)
        {
            GameObject specialTileObject = specialTileObjects[i];

            if (specialTileObject == null)
            {
                specialTileObjects.RemoveAt(i);
                continue;
            }

            Vector2Int specialTilePosition = WorldToGrid(specialTileObject.transform.position);

            if (!affectedBuildPositions.Contains(specialTilePosition))
                continue;

            if (HasNeighborPath(specialTilePosition))
                continue;

            Destroy(specialTileObject);
            specialTileObjects.RemoveAt(i);
        }
    }

    private HashSet<Vector2Int> GetBuildPositionsAround(List<Vector2Int> pathTiles)
    {
        HashSet<Vector2Int> positions = new HashSet<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int pathTile in pathTiles)
        {
            foreach (Vector2Int direction in directions)
                positions.Add(pathTile + direction);
        }

        return positions;
    }

    private List<Vector2Int> GetTeleporterBaseCandidates(int radius)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        if (pathPositions == null)
            return candidates;

        foreach (Vector2Int pathPosition in pathPositions)
        {
            if (pathPosition == startPosition)
                continue;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int candidate = pathPosition + new Vector2Int(x, y);

                    if (Mathf.Abs(x) + Mathf.Abs(y) > radius)
                        continue;

                    if (candidate == basePosition || IsPositionBlocked(candidate))
                        continue;

                    if (!HasNeighborPath(candidate) && Mathf.Abs(x) + Mathf.Abs(y) <= 1)
                        continue;

                    if (!candidates.Contains(candidate))
                        candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    private void CreateTeleporterVisual(Vector2Int entryPosition, Vector2Int exitPosition)
    {
        CreateTeleporterMarker(entryPosition, "Teleporter Eingang");
        CreateTeleporterMarker(exitPosition, "Teleporter Ausgang");
    }

    private void CreateTeleporterMarker(Vector2Int gridPosition, string objectName)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = objectName;
        marker.transform.position = GridToWorld(gridPosition) + Vector3.up * 0.25f;
        marker.transform.localScale = new Vector3(tileSize * 0.45f, 0.18f, tileSize * 0.45f);
        ColorTile(marker, teleporterColor);
        teleporterObjects.Add(marker);
    }

    private void AddPathTile(Vector2Int gridPosition)
    {
        AddPathTile(gridPosition, PathBuildOptionType.PathTile);
    }

    private void AddPathTile(Vector2Int gridPosition, PathBuildOptionType pathTileType)
    {
        if (pathPositions.Contains(gridPosition))
            return;

        GameObject tilePrefab = GetPathTilePrefabForType(pathTileType);
        if (tilePrefab == null)
        {
            Debug.LogError("TileManager: Kein PathTile-Prefab fuer " + pathTileType + " zugewiesen.");
            return;
        }

        pathPositions.Add(gridPosition);
        GameObject pathTileObject = Instantiate(tilePrefab, GridToWorld(gridPosition), Quaternion.identity);
        NormalizeGeneratedTileVisual(pathTileObject);
        pathTileObjects[gridPosition] = pathTileObject;
        ConfigureSpecialPathTile(pathTileObject, gridPosition, pathTileType);
        RefreshPathRailings();
    }

    public GameObject GetGeneratedTilePrefab(PathBuildOptionType optionType)
    {
        if (!useGeneratedTilePrefabs)
            return null;

        if (generatedTilePrefabSet != null)
        {
            GameObject prefabFromSet = generatedTilePrefabSet.GetPrefabForPathOption(optionType);

            if (prefabFromSet != null)
                return prefabFromSet;
        }

        switch (optionType)
        {
            case PathBuildOptionType.PathTile:
                return pathTilePrefab;
            case PathBuildOptionType.TrapTile:
                return trapTilePrefab;
            case PathBuildOptionType.SlowTile:
                return slowTilePrefab;
            case PathBuildOptionType.KnockTile:
                return knockTilePrefab;
            case PathBuildOptionType.ComboTile:
                return comboTilePrefab;
            case PathBuildOptionType.GoldTile:
                return goldTilePrefab;
            case PathBuildOptionType.RangeTile:
                return rangeTilePrefab;
            case PathBuildOptionType.DamageTile:
                return damageTilePrefab;
            case PathBuildOptionType.RateTile:
                return rateTilePrefab;
            case PathBuildOptionType.XPTile:
                return xpTilePrefab;
            case PathBuildOptionType.UpgradeTile:
                return upgradeTilePrefab;
            case PathBuildOptionType.HealTile:
                return healTilePrefab;
            case PathBuildOptionType.WeakpointTile:
                return weakpointTilePrefab;
            default:
                return null;
        }
    }

    private GameObject GetPathTilePrefabForType(PathBuildOptionType pathTileType)
    {
        GameObject generatedPrefab = GetGeneratedTilePrefab(pathTileType);

        if (generatedPrefab != null)
            return generatedPrefab;

        return pathTilePrefab;
    }

    private GameObject GetStartTilePrefab()
    {
        if (useGeneratedTilePrefabs && generatedTilePrefabSet != null && generatedTilePrefabSet.startTilePrefab != null)
            return generatedTilePrefabSet.startTilePrefab;

        return startTilePrefab;
    }

    private GameObject GetBaseTilePrefab()
    {
        if (useGeneratedTilePrefabs && generatedTilePrefabSet != null && generatedTilePrefabSet.baseTilePrefab != null)
            return generatedTilePrefabSet.baseTilePrefab;

        return baseTilePrefab;
    }

    private GameObject GetBuildTilePrefab()
    {
        if (useGeneratedTilePrefabs && generatedTilePrefabSet != null && generatedTilePrefabSet.buildTilePrefab != null)
            return generatedTilePrefabSet.buildTilePrefab;

        return buildTilePrefab;
    }

    private void ConfigureSpecialPathTile(GameObject pathTileObject, Vector2Int gridPosition, PathBuildOptionType pathTileType)
    {
        if (pathTileObject == null)
            return;

        bool hasDedicatedGeneratedVisual = HasDedicatedGeneratedPathPrefab(pathTileType);

        if (pathTileType == PathBuildOptionType.TrapTile)
        {
            if (!hasDedicatedGeneratedVisual)
                ColorTile(pathTileObject, trapTileColor);
        }
        else if (pathTileType == PathBuildOptionType.SlowTile)
        {
            if (!hasDedicatedGeneratedVisual)
                ColorTile(pathTileObject, slowTileColor);
        }
        else if (pathTileType == PathBuildOptionType.KnockTile)
        {
            if (!hasDedicatedGeneratedVisual)
                ColorTile(pathTileObject, knockTileColor);
        }
        else if (pathTileType == PathBuildOptionType.ComboTile)
        {
            if (!hasDedicatedGeneratedVisual)
                ColorTile(pathTileObject, comboTileColor);
        }
        else if (pathTileType == PathBuildOptionType.WeakpointTile)
        {
            ColorWeakpointTile(pathTileObject);
            UpdatePathTileFlowMarker(gridPosition, pathTileObject);
        }
        else if (pathTileType == PathBuildOptionType.GoldTile)
        {
            if (!hasDedicatedGeneratedVisual)
                ColorTile(pathTileObject, goldTileColor);

            return;
        }
        else
            return;

        SpecialPathTileEffect effect = pathTileObject.GetComponent<SpecialPathTileEffect>();
        if (effect == null)
            effect = pathTileObject.AddComponent<SpecialPathTileEffect>();

        effect.Configure(pathTileType, gridPosition, tileSize);
    }

    private bool HasDedicatedGeneratedPathPrefab(PathBuildOptionType pathTileType)
    {
        if (!useGeneratedTilePrefabs)
            return false;

        if (generatedTilePrefabSet != null && generatedTilePrefabSet.GetPrefabForPathOption(pathTileType) != null)
            return true;

        switch (pathTileType)
        {
            case PathBuildOptionType.TrapTile:
                return trapTilePrefab != null;
            case PathBuildOptionType.SlowTile:
                return slowTilePrefab != null;
            case PathBuildOptionType.KnockTile:
                return knockTilePrefab != null;
            case PathBuildOptionType.ComboTile:
                return comboTilePrefab != null;
            case PathBuildOptionType.GoldTile:
                return goldTilePrefab != null;
            case PathBuildOptionType.WeakpointTile:
                return weakpointTilePrefab != null;
            default:
                return false;
        }
    }

    private void ColorTile(GameObject tileObject, Color color)
    {
        Renderer[] renderers = tileObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer tileRenderer in renderers)
        {
            if (tileRenderer != null && tileRenderer.material != null)
                ApplyTileMaterialColor(tileRenderer.material, color);
        }
    }

    private void ApplyTileMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void ColorWeakpointTile(GameObject tileObject)
    {
        Renderer[] renderers = tileObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer tileRenderer in renderers)
        {
            if (tileRenderer == null || tileRenderer.material == null)
                continue;

            string rendererName = tileRenderer.gameObject.name;
            string marker = (rendererName + " " + tileRenderer.material.name).ToLowerInvariant();

            if (rendererName.StartsWith("TD_PathTile_") || marker.Contains("td_mat_tile_"))
                continue;

            if (rendererName.Contains("Path_Line") || rendererName.Contains("PathLine") || rendererName.Contains("Flow_"))
                ApplyTileMaterialColor(tileRenderer.material, new Color(0f, 0.88f, 1f, 1f));
            else if (rendererName.Contains("Edge") || rendererName.Contains("Rail") || marker.Contains("edge"))
                ApplyTileMaterialColor(tileRenderer.material, new Color(0.36f, 0.39f, 0.42f, 1f));
            else if (marker.Contains("red"))
                ApplyTileMaterialColor(tileRenderer.material, new Color32(235, 70, 70, 255));
            else if (marker.Contains("orange"))
                ApplyTileMaterialColor(tileRenderer.material, new Color(0.36f, 0.39f, 0.42f, 1f));
            else if (rendererName.Contains("Surface") || marker.Contains("surface"))
                ApplyTileMaterialColor(tileRenderer.material, new Color(0.1f, 0.17f, 0.25f, 1f));
            else if (rendererName.Contains("Base") || rendererName.Contains("Road") || marker.Contains("road"))
                ApplyTileMaterialColor(tileRenderer.material, new Color(0.1f, 0.12f, 0.15f, 1f));
            else
                ApplyTileMaterialColor(tileRenderer.material, weakpointTileColor);
        }
    }

    private void CreateGoldTile(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        GameObject generatedGoldPrefab = GetGeneratedTilePrefab(PathBuildOptionType.GoldTile);

        if (generatedGoldPrefab != null)
        {
            GameObject generatedGoldTile = Instantiate(generatedGoldPrefab, worldPosition, Quaternion.identity);
            generatedGoldTile.name = "Gold Tile";

            GoldTileGenerator generatedGenerator = generatedGoldTile.GetComponent<GoldTileGenerator>();
            if (generatedGenerator == null)
                generatedGenerator = generatedGoldTile.AddComponent<GoldTileGenerator>();

            generatedGenerator.gameManager = gameManager;
            specialTileObjects.Add(generatedGoldTile);
            return;
        }

        GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tileObject.name = "Gold Tile";
        tileObject.transform.position = worldPosition;
        tileObject.transform.localScale = new Vector3(tileSize, 0.1f, tileSize);
        ColorTile(tileObject, goldTileColor);

        GameObject collectorObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        collectorObject.name = "Gold Collector";
        collectorObject.transform.SetParent(tileObject.transform);
        collectorObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        collectorObject.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
        ColorTile(collectorObject, goldTileColor);

        GoldTileGenerator generator = tileObject.AddComponent<GoldTileGenerator>();
        generator.gameManager = gameManager;

        specialTileObjects.Add(tileObject);
    }


    private void CreateSupportTile(Vector2Int gridPosition, PathBuildOptionType supportTileType)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        GameObject generatedSupportPrefab = GetGeneratedTilePrefab(supportTileType);

        if (generatedSupportPrefab != null)
        {
            GameObject generatedSupportTile = Instantiate(generatedSupportPrefab, worldPosition, Quaternion.identity);
            generatedSupportTile.name = supportTileType == PathBuildOptionType.GoldTile ? "Gold Tile" : supportTileType.ToString();
            NormalizeGeneratedTileVisual(generatedSupportTile);
            HideGeneratedTileRailsAndCorners(generatedSupportTile);

            if (supportTileType == PathBuildOptionType.HealTile)
                ConfigureHealTileVisual(generatedSupportTile);

            TowerSupportTileEffect generatedEffect = generatedSupportTile.GetComponent<TowerSupportTileEffect>();
            if (generatedEffect == null)
                generatedEffect = generatedSupportTile.AddComponent<TowerSupportTileEffect>();

            generatedEffect.Configure(supportTileType, gridPosition, tileSize);

            if (supportTileType == PathBuildOptionType.GoldTile)
            {
                GoldTileGenerator generator = generatedSupportTile.GetComponent<GoldTileGenerator>();
                if (generator == null)
                    generator = generatedSupportTile.AddComponent<GoldTileGenerator>();

                generator.gameManager = gameManager;
            }

            specialTileObjects.Add(generatedSupportTile);
            return;
        }

        Color tileColor = GetSupportTileColor(supportTileType);
        GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tileObject.name = supportTileType == PathBuildOptionType.GoldTile ? "Gold Tile" : supportTileType.ToString();
        tileObject.transform.position = worldPosition;
        tileObject.transform.localScale = new Vector3(tileSize, 0.1f, tileSize);
        ColorTile(tileObject, tileColor);

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerObject.name = supportTileType + " Marker";
        markerObject.transform.SetParent(tileObject.transform, false);
        markerObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        markerObject.transform.localScale = new Vector3(0.28f, 0.55f, 0.28f);
        ColorTile(markerObject, tileColor);

        TowerSupportTileEffect effect = tileObject.AddComponent<TowerSupportTileEffect>();
        effect.Configure(supportTileType, gridPosition, tileSize);

        if (supportTileType == PathBuildOptionType.GoldTile)
        {
            GoldTileGenerator generator = tileObject.AddComponent<GoldTileGenerator>();
            generator.gameManager = gameManager;
        }

        specialTileObjects.Add(tileObject);
    }

    private void ConfigureHealTileVisual(GameObject tileObject)
    {
        if (tileObject == null)
            return;

        ColorHealTile(tileObject);
    }

    private void ColorHealTile(GameObject tileObject)
    {
        Renderer[] renderers = tileObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer tileRenderer in renderers)
        {
            if (tileRenderer == null || tileRenderer.material == null)
                continue;

            string marker = (tileRenderer.gameObject.name + " " + tileRenderer.material.name).ToLowerInvariant();

            if (marker.Contains("cross") || marker.Contains("white"))
                ApplyTileMaterialColor(tileRenderer.material, new Color32(80, 245, 135, 255));
            else if (marker.Contains("energy") || marker.Contains("glow") || marker.Contains("node") || marker.Contains("heal_green"))
                ApplyTileMaterialColor(tileRenderer.material, new Color32(45, 125, 85, 255));
            else if (marker.Contains("edge"))
                ApplyTileMaterialColor(tileRenderer.material, new Color32(82, 92, 104, 255));
            else if (marker.Contains("base") || marker.Contains("tile") || marker.Contains("dark") || marker.Contains("surface"))
                ApplyTileMaterialColor(tileRenderer.material, new Color32(82, 92, 104, 255));
            else
                ApplyTileMaterialColor(tileRenderer.material, healTileColor);
        }
    }

    private Color GetSupportTileColor(PathBuildOptionType supportTileType)
    {
        switch (supportTileType)
        {
            case PathBuildOptionType.RangeTile:
                return rangeTileColor;
            case PathBuildOptionType.DamageTile:
                return damageTileColor;
            case PathBuildOptionType.RateTile:
                return rateTileColor;
            case PathBuildOptionType.XPTile:
                return xpTileColor;
            case PathBuildOptionType.UpgradeTile:
                return upgradeTileColor;
            case PathBuildOptionType.GoldTile:
                return goldTileColor;
            case PathBuildOptionType.HealTile:
                return healTileColor;
            default:
                return Color.white;
        }
    }

    private void PlaceBaseTile()
    {
        if (currentBaseTile != null)
        {
            Destroy(currentBaseTile);
        }

        GameObject basePrefab = GetBaseTilePrefab();
        if (basePrefab == null)
        {
            Debug.LogError("TileManager: baseTilePrefab fehlt.");
            return;
        }

        currentBaseTile = Instantiate(
            basePrefab,
            GridToWorld(basePosition),
            Quaternion.identity
        );

        NormalizeGeneratedTileVisual(currentBaseTile);
        ConfigureGeneratedBaseTileRails(currentBaseTile);
    }


    private void RefreshPathRailings()
    {
        if (!autoBuildPathRailings)
            return;

        if (pathTileObjects == null)
            return;

        foreach (KeyValuePair<Vector2Int, GameObject> pair in pathTileObjects)
        {
            Vector2Int position = pair.Key;
            GameObject tileObject = pair.Value;

            if (tileObject == null)
                continue;

            GetPathFlowOpenings(position, out bool openNorth, out bool openEast, out bool openSouth, out bool openWest);
            bool usesGeneratedDirectRails = ConfigureGeneratedPathTileVisualsAndRails(
                position,
                tileObject,
                openNorth,
                openEast,
                openSouth,
                openWest
            );

            if (usesGeneratedDirectRails || ShouldSkipProceduralRailings(tileObject))
                continue;

            PathTileRailingBuilder railingBuilder = tileObject.GetComponent<PathTileRailingBuilder>();

            if (railingBuilder == null)
                railingBuilder = tileObject.AddComponent<PathTileRailingBuilder>();

            railingBuilder.keepConnectedEdgesClosed = false;
            railingBuilder.Configure(tileSize, openNorth, openEast, openSouth, openWest, pathRailingHeight, pathRailingThickness, pathRailingColor);
        }
    }

    private bool ConfigureGeneratedPathTileVisualsAndRails(
        Vector2Int position,
        GameObject tileObject,
        bool openNorth,
        bool openEast,
        bool openSouth,
        bool openWest)
    {
        UpdatePathTileFlowMarker(position, tileObject);

        if (!useGeneratedTileDirectRails || tileObject == null)
            return false;

        if (!HasGeneratedRailObjects(tileObject))
            return false;

        SetGeneratedRailVisibility(tileObject, openNorth, openEast, openSouth, openWest);
        return true;
    }

    private void ConfigureGeneratedStartTileRails()
    {
        if (!useGeneratedTileDirectRails || currentStartTile == null)
            return;

        Vector2Int exitDirection = GetStartTileExitDirection();
        bool openNorth = exitDirection == Vector2Int.up;
        bool openEast = exitDirection == Vector2Int.right;
        bool openSouth = exitDirection == Vector2Int.down;
        bool openWest = exitDirection == Vector2Int.left;

        if (HasGeneratedRailObjects(currentStartTile))
            SetGeneratedRailVisibility(currentStartTile, openNorth, openEast, openSouth, openWest);

        SetFlowMarkerVisibility(currentStartTile, exitDirection);
    }

    private void ConfigureGeneratedBaseTileRails(GameObject baseTileObject)
    {
        if (!useGeneratedTileDirectRails || baseTileObject == null || !HasGeneratedRailObjects(baseTileObject))
            return;

        Vector2Int entranceDirection = GetBaseTileEntranceDirection();
        bool openNorth = entranceDirection == Vector2Int.up;
        bool openEast = entranceDirection == Vector2Int.right;
        bool openSouth = entranceDirection == Vector2Int.down;
        bool openWest = entranceDirection == Vector2Int.left;

        SetGeneratedRailVisibility(baseTileObject, openNorth, openEast, openSouth, openWest);
        SetFlowMarkerVisibility(baseTileObject, entranceDirection);
    }

    private Vector2Int GetStartTileExitDirection()
    {
        if (pathPositions != null && pathPositions.Count > 1)
            return NormalizeCardinalDirection(pathPositions[1] - startPosition);

        return NormalizeCardinalDirection(currentDirection);
    }

    private Vector2Int GetBaseTileEntranceDirection()
    {
        if (pathPositions != null && pathPositions.Count > 0)
            return NormalizeCardinalDirection(pathPositions[pathPositions.Count - 1] - basePosition);

        return NormalizeCardinalDirection(new Vector2Int(-currentDirection.x, -currentDirection.y));
    }

    private void SetGeneratedRailVisibility(GameObject tileObject, bool openNorth, bool openEast, bool openSouth, bool openWest)
    {
        bool logicalNorthClosed = !openNorth;
        bool logicalEastClosed = !openEast;
        bool logicalSouthClosed = !openSouth;
        bool logicalWestClosed = !openWest;

        bool northClosed = invertGeneratedRailSideNames ? logicalSouthClosed : logicalNorthClosed;
        bool eastClosed = invertGeneratedRailSideNames ? logicalWestClosed : logicalEastClosed;
        bool southClosed = invertGeneratedRailSideNames ? logicalNorthClosed : logicalSouthClosed;
        bool westClosed = invertGeneratedRailSideNames ? logicalEastClosed : logicalWestClosed;

        SetChildActiveByNameContains(tileObject.transform, "Rail_North", northClosed);
        SetChildActiveByNameContains(tileObject.transform, "Rail_East", eastClosed);
        SetChildActiveByNameContains(tileObject.transform, "Rail_South", southClosed);
        SetChildActiveByNameContains(tileObject.transform, "Rail_West", westClosed);

        SetCornerPostVisibility(tileObject.transform, "Corner_NE", "CornerPost_NE", northClosed && eastClosed);
        SetCornerPostVisibility(tileObject.transform, "Corner_NW", "CornerPost_NW", northClosed && westClosed);
        SetCornerPostVisibility(tileObject.transform, "Corner_SE", "CornerPost_SE", southClosed && eastClosed);
        SetCornerPostVisibility(tileObject.transform, "Corner_SW", "CornerPost_SW", southClosed && westClosed);
    }

    private void SetCornerPostVisibility(Transform root, string preferredNamePart, string fallbackNamePart, bool active)
    {
        if (!SetChildActiveByNameContains(root, preferredNamePart, active))
        {
            if (!SetChildActiveByNameContains(root, fallbackNamePart, active))
                SetGenericGeneratedCornerPostVisibility(root, preferredNamePart, active);
        }
    }

    private void SetGenericGeneratedCornerPostVisibility(Transform root, string preferredNamePart, bool active)
    {
        if (preferredNamePart == "Corner_NE")
            SetChildActiveByNameContains(root, "CornerPost_04", active);
        else if (preferredNamePart == "Corner_NW")
            SetChildActiveByNameContains(root, "CornerPost_03", active);
        else if (preferredNamePart == "Corner_SE")
            SetChildActiveByNameContains(root, "CornerPost_02", active);
        else if (preferredNamePart == "Corner_SW")
            SetChildActiveByNameContains(root, "CornerPost_01", active);
    }

    private bool HasGeneratedRailObjects(GameObject tileObject)
    {
        if (tileObject == null)
            return false;

        Transform root = tileObject.transform;
        return FindChildByNameContains(root, "Rail_North") != null &&
               FindChildByNameContains(root, "Rail_East") != null &&
               FindChildByNameContains(root, "Rail_South") != null &&
               FindChildByNameContains(root, "Rail_West") != null;
    }

    private bool SetChildActiveByNameContains(Transform root, string namePart, bool active)
    {
        Transform child = FindChildByNameContains(root, namePart);

        if (child == null)
            return false;

        child.gameObject.SetActive(active);
        return true;
    }

    private Transform FindChildByNameContains(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
            return null;

        if (root.name.Contains(namePart))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByNameContains(root.GetChild(i), namePart);

            if (result != null)
                return result;
        }

        return null;
    }

    private void UpdatePathTileVisualRotation(Vector2Int position, GameObject tileObject)
    {
        if (!rotateGeneratedPathTileVisualsToFlow || tileObject == null)
            return;

        Transform visual = tileObject.transform.Find("Visual");

        if (visual == null)
            return;

        Vector2Int direction = GetPathTileForwardDirection(position);

        if (direction == Vector2Int.zero)
            return;

        float yaw = GetYawForGridDirection(direction) + generatedPathTileForwardYawOffset;
        visual.localRotation = Quaternion.Euler(
            generatedPathTileVisualEulerBase.x,
            generatedPathTileVisualEulerBase.y + yaw,
            generatedPathTileVisualEulerBase.z
        );
    }

    private void UpdatePathTileFlowMarker(Vector2Int position, GameObject tileObject)
    {
        if (!rotateGeneratedPathTileVisualsToFlow || tileObject == null)
            return;

        Vector2Int direction = GetPathTileForwardDirection(position);

        if (direction == Vector2Int.zero)
            return;

        bool foundDirectionalMarker = SetFlowMarkerVisibility(tileObject, direction);
        SetKnockTileVisualDirection(tileObject, GetKnockTileVisualDirection(position, direction));

        if (foundDirectionalMarker)
            return;

        if (SetPathCenterLineRotation(tileObject, direction))
            return;

        if (HasGeneratedRailObjects(tileObject))
            return;

        UpdatePathTileVisualRotation(position, tileObject);
    }

    private bool SetPathCenterLineRotation(GameObject tileObject, Vector2Int direction)
    {
        Transform centerLine = FindChildByNameContains(tileObject.transform, "PathCenterLine");

        if (centerLine == null)
            centerLine = FindChildByNameContains(tileObject.transform, "CenterLine");

        if (centerLine == null)
            centerLine = FindChildByNameContains(tileObject.transform, "Path_Line");

        if (centerLine == null)
            centerLine = FindChildByNameContains(tileObject.transform, "PathLine");

        if (centerLine == null)
            return false;

        float localZRotation = direction.x != 0 ? 90f : 0f;
        centerLine.localRotation = Quaternion.Euler(0f, 0f, localZRotation);
        return true;
    }

    private bool SetFlowMarkerVisibility(GameObject tileObject, Vector2Int direction)
    {
        Transform north = FindChildByNameContains(tileObject.transform, "Flow_North");
        Transform east = FindChildByNameContains(tileObject.transform, "Flow_East");
        Transform south = FindChildByNameContains(tileObject.transform, "Flow_South");
        Transform west = FindChildByNameContains(tileObject.transform, "Flow_West");

        if (north == null && east == null && south == null && west == null)
            return false;

        if (north != null)
            north.gameObject.SetActive(direction == Vector2Int.up);

        if (east != null)
            east.gameObject.SetActive(direction == Vector2Int.right);

        if (south != null)
            south.gameObject.SetActive(direction == Vector2Int.down);

        if (west != null)
            west.gameObject.SetActive(direction == Vector2Int.left);

        Transform centerLine = FindChildByNameContains(tileObject.transform, "CenterLine");

        if (centerLine != null)
            centerLine.gameObject.SetActive(false);

        return true;
    }

    private Vector2Int GetKnockTileVisualDirection(Vector2Int position, Vector2Int fallbackForwardDirection)
    {
        Vector2Int pushDirection = Vector2Int.zero;

        if (pathPositions != null)
        {
            int pathIndex = pathPositions.IndexOf(position);
            if (pathIndex > 0)
                pushDirection = NormalizeCardinalDirection(pathPositions[pathIndex - 1] - position);
        }

        if (pushDirection == Vector2Int.zero)
            pushDirection = NormalizeCardinalDirection(new Vector2Int(-fallbackForwardDirection.x, -fallbackForwardDirection.y));

        return NormalizeCardinalDirection(new Vector2Int(-pushDirection.x, -pushDirection.y));
    }

    private void SetKnockTileVisualDirection(GameObject tileObject, Vector2Int knockDirection)
    {
        if (tileObject == null || knockDirection == Vector2Int.zero)
            return;

        bool hasKnockVisual =
            HasChildByExactNameOrPrefix(tileObject.transform, "Knock_North", "Knock_North_") ||
            HasChildByExactNameOrPrefix(tileObject.transform, "Knock_East", "Knock_East_") ||
            HasChildByExactNameOrPrefix(tileObject.transform, "Knock_South", "Knock_South_") ||
            HasChildByExactNameOrPrefix(tileObject.transform, "Knock_West", "Knock_West_");

        if (!hasKnockVisual)
            return;

        knockDirection = NormalizeCardinalDirection(knockDirection);

        SetKnockVisualDirectionActive(tileObject.transform, "Knock_North", "Knock_North_", knockDirection == Vector2Int.up);
        SetKnockVisualDirectionActive(tileObject.transform, "Knock_East", "Knock_East_", knockDirection == Vector2Int.right);
        SetKnockVisualDirectionActive(tileObject.transform, "Knock_South", "Knock_South_", knockDirection == Vector2Int.down);
        SetKnockVisualDirectionActive(tileObject.transform, "Knock_West", "Knock_West_", knockDirection == Vector2Int.left);

        KnockTileVisualAnimator animator = tileObject.GetComponent<KnockTileVisualAnimator>();
        if (animator != null)
            animator.SetDirection(knockDirection);
    }

    private Vector2Int GetPathTileForwardDirection(Vector2Int position)
    {
        if (pathPositions == null || pathPositions.Count == 0)
            return NormalizeCardinalDirection(currentDirection);

        int pathIndex = pathPositions.IndexOf(position);

        if (pathIndex >= 0)
        {
            if (pathIndex < pathPositions.Count - 1)
                return NormalizeCardinalDirection(pathPositions[pathIndex + 1] - position);

            Vector2Int toBase = NormalizeCardinalDirection(basePosition - position);

            if (toBase != Vector2Int.zero)
                return toBase;

            if (pathIndex > 0)
                return NormalizeCardinalDirection(position - pathPositions[pathIndex - 1]);
        }

        return NormalizeCardinalDirection(currentDirection);
    }

    private Transform FindChildByExactName(Transform root, string exactName)
    {
        if (root == null || string.IsNullOrEmpty(exactName))
            return null;

        if (root.name == exactName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByExactName(root.GetChild(i), exactName);

            if (result != null)
                return result;
        }

        return null;
    }

    private bool HasChildByExactNameOrPrefix(Transform root, string exactName, string prefix)
    {
        return FindChildByExactName(root, exactName) != null ||
               FindChildByNamePrefix(root, prefix) != null;
    }

    private void SetKnockVisualDirectionActive(Transform root, string exactName, string prefix, bool active)
    {
        Transform group = FindChildByExactName(root, exactName);
        if (group != null)
            group.gameObject.SetActive(active);

        SetChildrenActiveByNamePrefix(root, prefix, active);
    }

    private void SetChildrenActiveByNamePrefix(Transform root, string prefix, bool active)
    {
        if (root == null || string.IsNullOrEmpty(prefix))
            return;

        if (root.name.StartsWith(prefix))
            root.gameObject.SetActive(active);

        for (int i = 0; i < root.childCount; i++)
            SetChildrenActiveByNamePrefix(root.GetChild(i), prefix, active);
    }

    private Transform FindChildByNamePrefix(Transform root, string prefix)
    {
        if (root == null || string.IsNullOrEmpty(prefix))
            return null;

        if (root.name.StartsWith(prefix))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByNamePrefix(root.GetChild(i), prefix);

            if (result != null)
                return result;
        }

        return null;
    }

    private Vector2Int NormalizeCardinalDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return new Vector2Int(direction.x >= 0 ? 1 : -1, 0);

        return new Vector2Int(0, direction.y >= 0 ? 1 : -1);
    }

    private float GetYawForGridDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.right)
            return 90f;

        if (direction == Vector2Int.left)
            return -90f;

        if (direction == Vector2Int.down)
            return 180f;

        return 0f;
    }

    private bool ShouldSkipProceduralRailings(GameObject tileObject)
    {
        if (!skipProceduralRailingsOnGeneratedTiles || !useGeneratedTilePrefabs || tileObject == null)
            return false;

        return tileObject.name.StartsWith("TD_");
    }

    private bool IsPathOrBasePosition(Vector2Int position)
    {
        if (position == basePosition)
            return true;

        return pathPositions != null && pathPositions.Contains(position);
    }

    private void GetPathFlowOpenings(Vector2Int position, out bool openNorth, out bool openEast, out bool openSouth, out bool openWest)
    {
        openNorth = false;
        openEast = false;
        openSouth = false;
        openWest = false;

        if (pathPositions == null)
            return;

        int pathIndex = pathPositions.IndexOf(position);

        if (pathIndex < 0)
            return;

        if (pathIndex > 0)
            MarkOpeningForNeighbor(position, pathPositions[pathIndex - 1], ref openNorth, ref openEast, ref openSouth, ref openWest);
        else
            MarkOpeningForNeighbor(position, startPosition, ref openNorth, ref openEast, ref openSouth, ref openWest);

        if (pathIndex < pathPositions.Count - 1)
            MarkOpeningForNeighbor(position, pathPositions[pathIndex + 1], ref openNorth, ref openEast, ref openSouth, ref openWest);
        else
            MarkOpeningForNeighbor(position, basePosition, ref openNorth, ref openEast, ref openSouth, ref openWest);
    }

    private void MarkOpeningForNeighbor(Vector2Int position, Vector2Int neighbor, ref bool openNorth, ref bool openEast, ref bool openSouth, ref bool openWest)
    {
        Vector2Int direction = neighbor - position;

        if (direction == Vector2Int.up)
            openNorth = true;
        else if (direction == Vector2Int.right)
            openEast = true;
        else if (direction == Vector2Int.down)
            openSouth = true;
        else if (direction == Vector2Int.left)
            openWest = true;
    }


    private void RefreshBuildTiles()
    {
        foreach (GameObject tile in buildTileObjects)
        {
            if (tile != null)
            {
                Destroy(tile);
            }
        }

        buildTileObjects.Clear();
        buildTilePositions.Clear();

        if (!pathInitialized)
            return;

        UpdateReservedPathExtension();

        foreach (Vector2Int pathPos in pathPositions)
        {
            CreateBuildTilesAround(pathPos);
        }
    }

    private void CreateBuildTilesAround(Vector2Int pathPos)
    {
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        foreach (Vector2Int dir in directions)
        {
            Vector2Int buildPos = pathPos + dir;

            if (!IsValidBuildTilePosition(buildPos))
                continue;

            GameObject buildPrefab = GetBuildTilePrefab();
            if (buildPrefab == null)
            {
                Debug.LogError("TileManager: buildTilePrefab fehlt.");
                return;
            }

            GameObject tile = Instantiate(
                buildPrefab,
                GridToWorld(buildPos),
                Quaternion.identity
            );

            NormalizeGeneratedTileVisual(tile);
            HideGeneratedTileRailsAndCorners(tile);

            tile.SetActive(buildTilesVisible);

            buildTileObjects.Add(tile);
            buildTilePositions.Add(buildPos);
        }
    }

    private bool IsValidBuildTilePosition(Vector2Int position)
    {
        if (IsPositionBlocked(position))
            return false;

        if (IsReservedPathExtensionPosition(position) && !supportTilePlacementBuildModeActive)
            return false;

        if (buildTilePositions.Contains(position))
            return false;

        return true;
    }

    public void SetSupportTilePlacementBuildMode(bool active)
    {
        if (supportTilePlacementBuildModeActive == active)
            return;

        supportTilePlacementBuildModeActive = active;
        RefreshBuildTiles();
    }

    public void SetBuildTilesVisible(bool visible)
    {
        buildTilesVisible = visible;

        foreach (GameObject tile in buildTileObjects)
        {
            if (tile != null)
            {
                tile.SetActive(visible);
            }
        }
    }

    public void RegisterTowerPosition(Vector3 worldPosition)
    {
        Vector2Int gridPosition = WorldToGrid(worldPosition);

        if (!towerPositions.Contains(gridPosition))
        {
            towerPositions.Add(gridPosition);
        }

        RefreshBuildTiles();
    }


    public void UnregisterTowerPosition(Vector2Int gridPosition)
    {
        if (towerPositions.Contains(gridPosition))
            towerPositions.Remove(gridPosition);

        RefreshBuildTiles();
    }

    public void UnregisterTowerPosition(Vector3 worldPosition)
    {
        UnregisterTowerPosition(WorldToGrid(worldPosition));
    }

    private Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector3(
            gridPosition.x * tileSize,
            0.05f,
            gridPosition.y * tileSize
        );
    }

    private Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / tileSize),
            Mathf.RoundToInt(worldPosition.z / tileSize)
        );
    }

    public List<Vector3> GetWorldPath()
    {
        List<Vector3> worldPath = new List<Vector3>();

        if (!pathInitialized)
            return worldPath;

        foreach (Vector2Int gridPos in pathPositions)
        {
            int teleporterIndex = GetTeleporterIndexForExit(gridPos);
            if (teleporterIndex >= 0 && teleporterIndex < teleporterEntryPositions.Count)
                AddWorldPathPoint(worldPath, teleporterEntryPositions[teleporterIndex]);

            AddWorldPathPoint(worldPath, gridPos);
        }

        int baseTeleporterIndex = GetTeleporterIndexForExit(basePosition);
        if (baseTeleporterIndex >= 0 && baseTeleporterIndex < teleporterEntryPositions.Count)
            AddWorldPathPoint(worldPath, teleporterEntryPositions[baseTeleporterIndex]);

        AddWorldPathPoint(worldPath, basePosition);

        return worldPath;
    }

    private int GetTeleporterIndexForExit(Vector2Int exitPosition)
    {
        if (teleporterExitPositions == null)
            return -1;

        for (int i = teleporterExitPositions.Count - 1; i >= 0; i--)
        {
            if (teleporterExitPositions[i] == exitPosition)
                return i;
        }

        return -1;
    }

    private void AddWorldPathPoint(List<Vector3> worldPath, Vector2Int gridPosition)
    {
        if (worldPath == null)
            return;

        Vector3 worldPosition = GridToWorld(gridPosition);
        if (worldPath.Count > 0 && Vector3.Distance(worldPath[worldPath.Count - 1], worldPosition) <= 0.01f)
            return;

        worldPath.Add(worldPosition);
    }
}
