using UnityEngine;

public class BuildTile : MonoBehaviour
{
    [Header("State")]
    public bool isOccupied = false;

    [Header("Hover Feedback")]
    public bool useHoverFeedback = true;
    public Color hoverColor = new Color32(45, 90, 210, 255);

    private Renderer[] cachedRenderers;
    private Color[] defaultColors;
    private bool isHovered = false;

    private void Awake()
    {
        CacheRenderers();
        CacheDefaultColors();
    }

    private void OnEnable()
    {
        isHovered = false;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        isHovered = false;
    }

    public bool PlaceTower(GameObject towerPrefab, GameManager gameManager, TileManager tileManager, int cost)
    {
        if (tileManager == null || !tileManager.IsBuildAllowed())
        {
            Debug.Log("Bauen ist aktuell nicht erlaubt.");
            return false;
        }

        if (towerPrefab == null)
        {
            Debug.Log("Kein Tower ausgewählt!");
            return false;
        }

        if (isOccupied)
        {
            Debug.Log("Dieses BuildTile ist bereits belegt!");
            return false;
        }

        Vector2Int gridPosition = tileManager.WorldToGridPublic(transform.position);

        if (tileManager.IsReservedPathExtensionPosition(gridPosition))
        {
            tileManager.ShowBuildRestrictionWarning();
            Debug.LogWarning("Dieses Tile ist die letzte mögliche Weg-Erweiterung und kann nicht bebaut werden.");
            return false;
        }

        if (gameManager == null)
        {
            Debug.LogError("BuildTile: GameManager fehlt!");
            return false;
        }

        if (!gameManager.SpendGold(cost, RunGoldSpendSource.TowerBuild))
            return false;

        Vector3 towerPosition = transform.position + Vector3.up * 0.5f;

        GameObject towerObject = Instantiate(towerPrefab, towerPosition, Quaternion.identity);
        Tower builtTower = towerObject != null ? towerObject.GetComponent<Tower>() : null;
        gameManager.RegisterTowerBuilt(builtTower, cost, gridPosition, towerPosition);

        tileManager.RegisterTowerPosition(transform.position);

        isOccupied = true;
        SetHovered(false);

        Debug.Log("Tower gebaut und Position blockiert!");
        return true;
    }

    public void SetHovered(bool value)
    {
        if (!useHoverFeedback)
            return;

        if (isHovered == value)
            return;

        isHovered = value;
        ApplyVisualState();
    }

    private void CacheRenderers()
    {
        if (cachedRenderers != null && cachedRenderers.Length > 0)
            return;

        cachedRenderers = GetComponentsInChildren<Renderer>();
    }

    private void CacheDefaultColors()
    {
        CacheRenderers();

        if (cachedRenderers == null)
        {
            defaultColors = new Color[0];
            return;
        }

        defaultColors = new Color[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null || cachedRenderers[i].material == null)
            {
                defaultColors[i] = Color.white;
                continue;
            }

            defaultColors[i] = cachedRenderers[i].material.color;
        }
    }

    private void ApplyVisualState()
    {
        CacheRenderers();

        if (cachedRenderers == null)
            return;

        if (defaultColors == null || defaultColors.Length != cachedRenderers.Length)
            CacheDefaultColors();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer targetRenderer = cachedRenderers[i];

            if (targetRenderer == null || targetRenderer.material == null)
                continue;

            targetRenderer.material.color = isHovered ? hoverColor : defaultColors[i];
        }
    }
}
