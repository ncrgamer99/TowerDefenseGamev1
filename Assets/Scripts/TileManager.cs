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

    private bool canBuild = true;
    private bool buildTilesVisible = false;

    private List<Vector2Int> pathPositions = new List<Vector2Int>();
    private Dictionary<Vector2Int, GameObject> pathTileObjects = new Dictionary<Vector2Int, GameObject>();
    private HashSet<Vector2Int> buildTilePositions = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> towerPositions = new HashSet<Vector2Int>();
    private List<GameObject> buildTileObjects = new List<GameObject>();

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
        if (!CanExtendTo(newBasePosition))
        {
            Debug.Log("Ungültige Path-Erweiterung!");
            return false;
        }

        Vector2Int newDirection = newBasePosition - basePosition;
        currentDirection = newDirection;

        if (!pathPositions.Contains(basePosition))
        {
            AddPathTile(basePosition);
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

        return true;
    }

    private void AddPathTile(Vector2Int gridPosition)
    {
        if (pathPositions.Contains(gridPosition))
            return;

        pathPositions.Add(gridPosition);
        GameObject pathTileObject = Instantiate(pathTilePrefab, GridToWorld(gridPosition), Quaternion.identity);
        pathTileObjects[gridPosition] = pathTileObject;
        RefreshPathRailings();
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

            bool openNorth = IsPathOrBasePosition(position + Vector2Int.up);
            bool openSouth = IsPathOrBasePosition(position + Vector2Int.down);
            bool openEast = IsPathOrBasePosition(position + Vector2Int.right);
            bool openWest = IsPathOrBasePosition(position + Vector2Int.left);

            railingBuilder.Configure(tileSize, openNorth, openEast, openSouth, openWest, pathRailingHeight, pathRailingThickness, pathRailingColor);
        }
    }

    private bool IsPathOrBasePosition(Vector2Int position)
    {
        if (position == basePosition)
            return true;

        return pathPositions != null && pathPositions.Contains(position);
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
