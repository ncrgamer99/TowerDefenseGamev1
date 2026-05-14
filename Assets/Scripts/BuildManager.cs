using UnityEngine;
using UnityEngine.EventSystems;

public class BuildManager : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public GameManager gameManager;
    public TileManager tileManager;
    public TowerUI towerUI;
    public BuildSelectionUI buildSelectionUI;
    public PathBuildManager pathBuildManager;

    [Header("Build Selection")]
    [System.NonSerialized]
    public BuildOption selectedBuildOption;

    [Header("Hover Feedback")]
    public bool highlightHoveredBuildTile = true;

    private bool buildTilesCurrentlyVisible = false;
    private BuildTile hoveredBuildTile;

    private void Start()
    {
        selectedBuildOption = null;
        buildTilesCurrentlyVisible = false;

        if (tileManager != null)
        {
            tileManager.SetBuildTilesVisible(false);
        }

        if (buildSelectionUI != null)
        {
            buildSelectionUI.ClearSelectionText();
        }
    }

    private void Update()
    {
        if (IsBlockedByModalUI())
        {
            ClearSelectionForModalLock();
            return;
        }

        RefreshBuildTileVisibility();
        UpdateBuildTileHover();

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            ClearSelection();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI())
            {
                if (gameManager != null && gameManager.isGameOver)
                {
                    TrySelectTowerUnderMouse();
                }

                return;
            }

            TryHandleClick();
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    public void SelectBuildOption(BuildOption option)
    {
        if (IsBlockedByModalUI())
        {
            ClearSelectionForModalLock();
            return;
        }

        selectedBuildOption = option;

        RefreshBuildTileVisibility();
        UpdateBuildTileHover();

        if (option != null)
        {
            Debug.Log("Selected: " + option.displayName);
        }
    }

    public void ClearCurrentSelection()
    {
        ClearSelection();
    }

    private void ClearSelection()
    {
        selectedBuildOption = null;
        buildTilesCurrentlyVisible = false;

        ClearHoveredBuildTile();

        if (tileManager != null)
        {
            tileManager.SetBuildTilesVisible(false);
        }

        if (buildSelectionUI != null)
        {
            buildSelectionUI.ClearSelectionText();
        }

        Debug.Log("Build selection cleared.");
    }


    private void ClearSelectionForModalLock()
    {
        bool hadSelection = selectedBuildOption != null || buildTilesCurrentlyVisible || hoveredBuildTile != null;

        selectedBuildOption = null;
        buildTilesCurrentlyVisible = false;
        ClearHoveredBuildTile();

        if (tileManager != null)
            tileManager.SetBuildTilesVisible(false);

        if (buildSelectionUI != null)
            buildSelectionUI.CloseSelectionPanel();

        if (hadSelection && buildSelectionUI != null)
            buildSelectionUI.ClearSelectionText();
    }

    private bool IsBlockedByModalUI()
    {
        if (gameManager == null)
            return false;

        return gameManager.IsGameplayInputLockedByModalUI();
    }

    private void RefreshBuildTileVisibility()
    {
        if (tileManager == null)
            return;

        bool shouldShowBuildTiles =
            selectedBuildOption != null &&
            selectedBuildOption.placementType == PlacementType.BuildTile &&
            tileManager.IsBuildAllowed();

        if (buildTilesCurrentlyVisible == shouldShowBuildTiles)
            return;

        buildTilesCurrentlyVisible = shouldShowBuildTiles;
        tileManager.SetBuildTilesVisible(shouldShowBuildTiles);

        if (!shouldShowBuildTiles)
        {
            ClearHoveredBuildTile();
        }
    }

    private void UpdateBuildTileHover()
    {
        if (!highlightHoveredBuildTile)
        {
            ClearHoveredBuildTile();
            return;
        }

        if (IsBlockedByModalUI())
        {
            ClearHoveredBuildTile();
            return;
        }

        if (selectedBuildOption == null || selectedBuildOption.placementType != PlacementType.BuildTile)
        {
            ClearHoveredBuildTile();
            return;
        }

        if (tileManager == null || !tileManager.IsBuildAllowed())
        {
            ClearHoveredBuildTile();
            return;
        }

        if (pathBuildManager != null && pathBuildManager.IsChoiceOpen())
        {
            ClearHoveredBuildTile();
            return;
        }

        if (IsPointerOverUI())
        {
            ClearHoveredBuildTile();
            return;
        }

        BuildTile buildTile = GetBuildTileUnderMouse();
        SetHoveredBuildTile(buildTile);
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

    private void SetHoveredBuildTile(BuildTile buildTile)
    {
        if (hoveredBuildTile == buildTile)
            return;

        ClearHoveredBuildTile();

        hoveredBuildTile = buildTile;

        if (hoveredBuildTile != null)
        {
            hoveredBuildTile.SetHovered(true);
        }
    }

    private void ClearHoveredBuildTile()
    {
        if (hoveredBuildTile != null)
        {
            hoveredBuildTile.SetHovered(false);
            hoveredBuildTile = null;
        }
    }

    private void TryHandleClick()
    {
        if (IsBlockedByModalUI())
            return;

        if (pathBuildManager != null && pathBuildManager.IsChoiceOpen())
            return;

        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        Tower tower = hit.collider.GetComponent<Tower>();

        if (tower != null)
        {
            if (towerUI != null)
            {
                towerUI.SelectTower(tower);
            }

            return;
        }

        BuildTile buildTile = hit.collider.GetComponent<BuildTile>();

        if (buildTile == null)
            return;

        if (selectedBuildOption == null)
        {
            Debug.Log("Kein Tower ausgewählt!");
            return;
        }

        if (tileManager == null || !tileManager.IsBuildAllowed())
        {
            Debug.Log("Bauen ist aktuell nicht erlaubt. Auswahl bleibt gespeichert.");
            RefreshBuildTileVisibility();
            return;
        }

        if (selectedBuildOption.placementType != PlacementType.BuildTile)
        {
            Debug.Log("Diese Auswahl kann nicht auf BuildTiles platziert werden.");
            return;
        }

        bool placed = buildTile.PlaceTower(
            selectedBuildOption.prefab,
            gameManager,
            tileManager,
            selectedBuildOption.cost
        );

        if (placed)
        {
            ClearSelection();
        }
    }

    private void TrySelectTowerUnderMouse()
    {
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        Tower tower = hit.collider.GetComponent<Tower>();

        if (tower == null)
            return;

        if (towerUI != null)
        {
            towerUI.SelectTower(tower);
        }
    }
}
