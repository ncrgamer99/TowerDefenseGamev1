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

    private GameObject currentBaseTile;

    private bool hasReservedPathExtension = false;
    private Vector2Int reservedPathExtensionPosition;
    private float reservedPathExtensionWarningTimer = 0f;

    private bool baseRelocationModeActive = false;
    private readonly List<Vector2Int> teleporterEntryPositions = new List<Vector2Int>();
    private readonly List<Vector2Int> teleporterExitPositions = new List<Vector2Int>();
    private readonly List<GameObject> teleporterObjects = new List<GameObject>();

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

            Vector2Int towerGridPosition = WorldToGrid(tower.transform.position);

            if (!affectedBuildPositions.Contains(towerGridPosition))
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
            specialBlockedPositions.Remove(position);

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

        int teleporterIndex = teleporterExitPositions.LastIndexOf(basePosition);
        if (teleporterIndex >= 0 && teleporterIndex < teleporterEntryPositions.Count)
            worldPath.Add(GridToWorld(teleporterEntryPositions[teleporterIndex]));

        worldPath.Add(GridToWorld(basePosition));

        return worldPath;
    }
}