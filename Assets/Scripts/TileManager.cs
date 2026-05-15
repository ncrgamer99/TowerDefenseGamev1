using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject startTilePrefab;
    public GameObject pathTilePrefab;
    public GameObject baseTilePrefab;
    public GameObject buildTilePrefab;

    [Header("Managers")]
    public GameManager gameManager;

    [Header("Settings")]
    public float tileSize = 1f;

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

    [Header("Special Tile Visuals V1")]
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

    private GameObject currentBaseTile;

    private bool hasReservedPathExtension = false;
    private Vector2Int reservedPathExtensionPosition;
    private float reservedPathExtensionWarningTimer = 0f;

    private void Start()
    {
        ScaleWorldVisualIfNeeded();
        CreateStartPath();
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

    public void SetCanBuild(bool value)
    {
        canBuild = value;

        if (!canBuild)
        {
            SetBuildTilesVisible(false);
        }
    }

    private void CreateStartPath()
    {
        startPosition = new Vector2Int(0, 0);

        Instantiate(startTilePrefab, GridToWorld(startPosition), Quaternion.identity);
        pathPositions.Add(startPosition);

        AddPathTile(new Vector2Int(1, 0));
        AddPathTile(new Vector2Int(2, 0));
        AddPathTile(new Vector2Int(3, 0));
        AddPathTile(new Vector2Int(4, 0));
        AddPathTile(new Vector2Int(5, 0));

        basePosition = new Vector2Int(6, 0);
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

    public Vector3 GridToWorldPublic(Vector2Int gridPosition)
    {
        return GridToWorld(gridPosition);
    }

    public Vector2Int WorldToGridPublic(Vector3 worldPosition)
    {
        return WorldToGrid(worldPosition);
    }

    public bool IsBuildAllowed()
    {
        return canBuild;
    }

    public bool CanExtendTo(Vector2Int targetPosition)
    {
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

    public bool TryExtendPathTo(Vector2Int newBasePosition)
    {
        return TryExtendPathTo(newBasePosition, PathBuildOptionType.PathTile);
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
               specialTileType == PathBuildOptionType.ComboTile;
    }

    public bool TryBuildGoldTileAt(Vector2Int goldTilePosition)
    {
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

    private bool IsTowerSupportTileType(PathBuildOptionType tileType)
    {
        return tileType == PathBuildOptionType.RangeTile ||
               tileType == PathBuildOptionType.DamageTile ||
               tileType == PathBuildOptionType.RateTile ||
               tileType == PathBuildOptionType.XPTile ||
               tileType == PathBuildOptionType.UpgradeTile;
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
        return GetValidExtensionPositions().Count > 0;
    }

    public Vector2Int[] GetPossibleExtensionPositions()
    {
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

    private void AddPathTile(Vector2Int gridPosition)
    {
        AddPathTile(gridPosition, PathBuildOptionType.PathTile);
    }

    private void AddPathTile(Vector2Int gridPosition, PathBuildOptionType pathTileType)
    {
        if (pathPositions.Contains(gridPosition))
            return;

        pathPositions.Add(gridPosition);
        GameObject pathTileObject = Instantiate(pathTilePrefab, GridToWorld(gridPosition), Quaternion.identity);
        pathTileObjects[gridPosition] = pathTileObject;
        ConfigureSpecialPathTile(pathTileObject, gridPosition, pathTileType);
        RefreshPathRailings();
    }

    private void ConfigureSpecialPathTile(GameObject pathTileObject, Vector2Int gridPosition, PathBuildOptionType pathTileType)
    {
        if (pathTileObject == null)
            return;

        if (pathTileType == PathBuildOptionType.TrapTile)
            ColorTile(pathTileObject, trapTileColor);
        else if (pathTileType == PathBuildOptionType.SlowTile)
            ColorTile(pathTileObject, slowTileColor);
        else if (pathTileType == PathBuildOptionType.KnockTile)
            ColorTile(pathTileObject, knockTileColor);
        else if (pathTileType == PathBuildOptionType.ComboTile)
            ColorTile(pathTileObject, comboTileColor);
        else
            return;

        SpecialPathTileEffect effect = pathTileObject.GetComponent<SpecialPathTileEffect>();
        if (effect == null)
            effect = pathTileObject.AddComponent<SpecialPathTileEffect>();

        effect.Configure(pathTileType, gridPosition, tileSize);
    }

    private void ColorTile(GameObject tileObject, Color color)
    {
        Renderer[] renderers = tileObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer tileRenderer in renderers)
        {
            if (tileRenderer != null && tileRenderer.material != null)
                tileRenderer.material.color = color;
        }
    }

    private void CreateGoldTile(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
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
        Color tileColor = GetSupportTileColor(supportTileType);
        GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tileObject.name = supportTileType.ToString();
        tileObject.transform.position = worldPosition;
        tileObject.transform.localScale = new Vector3(tileSize, 0.1f, tileSize);
        ColorTile(tileObject, tileColor);

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerObject.name = supportTileType + " Marker";
        markerObject.transform.SetParent(tileObject.transform, false);
        markerObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        markerObject.transform.localScale = new Vector3(0.28f, 0.55f, 0.28f);
        ColorTile(markerObject, tileColor);

        GameObject auraObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        auraObject.name = supportTileType + " Aura";
        auraObject.transform.SetParent(tileObject.transform, false);
        auraObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        auraObject.transform.localScale = new Vector3(2.25f, 0.02f, 2.25f);
        Color auraColor = tileColor;
        auraColor.a = 0.35f;
        ColorTile(auraObject, auraColor);

        TowerSupportTileEffect effect = tileObject.AddComponent<TowerSupportTileEffect>();
        effect.Configure(supportTileType, gridPosition, tileSize);

        specialTileObjects.Add(tileObject);
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

        currentBaseTile = Instantiate(
            baseTilePrefab,
            GridToWorld(basePosition),
            Quaternion.identity
        );
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

            PathTileRailingBuilder railingBuilder = tileObject.GetComponent<PathTileRailingBuilder>();

            if (railingBuilder == null)
                railingBuilder = tileObject.AddComponent<PathTileRailingBuilder>();

            GetPathFlowOpenings(position, out bool openNorth, out bool openEast, out bool openSouth, out bool openWest);

            railingBuilder.keepConnectedEdgesClosed = false;
            railingBuilder.Configure(tileSize, openNorth, openEast, openSouth, openWest, pathRailingHeight, pathRailingThickness, pathRailingColor);
        }
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

            GameObject tile = Instantiate(
                buildTilePrefab,
                GridToWorld(buildPos),
                Quaternion.identity
            );

            tile.SetActive(buildTilesVisible);

            buildTileObjects.Add(tile);
            buildTilePositions.Add(buildPos);
        }
    }

    private bool IsValidBuildTilePosition(Vector2Int position)
    {
        if (IsPositionBlocked(position))
            return false;

        if (IsReservedPathExtensionPosition(position))
            return false;

        if (buildTilePositions.Contains(position))
            return false;

        return true;
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

        foreach (Vector2Int gridPos in pathPositions)
        {
            worldPath.Add(GridToWorld(gridPos));
        }

        worldPath.Add(GridToWorld(basePosition));

        return worldPath;
    }
}