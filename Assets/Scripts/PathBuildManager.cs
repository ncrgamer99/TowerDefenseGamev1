using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PathBuildOptionType
{
    PathTile = 0,
    TrapTile = 1,
    SpecialTile = 2,
    GoldTile = 4,
    SlowTile = 5,
    KnockTile = 6
}

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

    [Header("Available Random Options")]
    public List<PathBuildOption> randomOptions = new List<PathBuildOption>();

    private GameObject currentGhost;
    private Vector2Int hoveredGridPosition;
    private bool hasValidHover = false;
    private bool choiceOpen = false;
    private bool optionsGeneratedForCurrentBuildPhase = false;

    private PathBuildOption[] currentOptions = new PathBuildOption[3];

    private void Start()
    {
        ResolveGameManagerReference();

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

    private void SetupButtonEvents()
    {
        if (optionButton1 != null)
        {
            optionButton1.onClick.RemoveAllListeners();
            optionButton1.onClick.AddListener(() => ChooseOption(0));
        }

        if (optionButton2 != null)
        {
            optionButton2.onClick.RemoveAllListeners();
            optionButton2.onClick.AddListener(() => ChooseOption(1));
        }

        if (optionButton3 != null)
        {
            optionButton3.onClick.RemoveAllListeners();
            optionButton3.onClick.AddListener(() => ChooseOption(2));
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

    private void GenerateCurrentOptions()
    {
        currentOptions[0] = new PathBuildOption
        {
            displayName = "Path Tile",
            description = "Verlängert den Weg und startet danach die nächste Welle.",
            optionType = PathBuildOptionType.PathTile
        };

        currentOptions[1] = GetRandomOption();
        currentOptions[2] = GetRandomOption();
    }

    private PathBuildOption GetRandomOption()
    {
        List<PathBuildOption> optionPool = new List<PathBuildOption>();

        if (randomOptions != null)
        {
            foreach (PathBuildOption option in randomOptions)
            {
                if (IsSupportedSpecialOption(option))
                    optionPool.Add(option);
            }
        }

        if (optionPool.Count == 0)
            optionPool.AddRange(CreateDefaultSpecialOptions());

        int randomIndex = Random.Range(0, optionPool.Count);
        return optionPool[randomIndex];
    }

    private bool IsSupportedSpecialOption(PathBuildOption option)
    {
        if (option == null)
            return false;

        return option.optionType == PathBuildOptionType.TrapTile ||
               option.optionType == PathBuildOptionType.GoldTile ||
               option.optionType == PathBuildOptionType.SlowTile ||
               option.optionType == PathBuildOptionType.KnockTile;
    }

    private List<PathBuildOption> CreateDefaultSpecialOptions()
    {
        return new List<PathBuildOption>
        {
            new PathBuildOption
            {
                displayName = "Gold Tile",
                description = "Baut ein Goldtile. Die Base bleibt stehen. Während Waves erzeugt es +5 Gold pro Sekunde.",
                optionType = PathBuildOptionType.GoldTile
            },
            new PathBuildOption
            {
                displayName = "Trap Tile",
                description = "Zählt als PathTile. Gegner erhalten Blutung: 5s lang 10 Schaden pro Sekunde.",
                optionType = PathBuildOptionType.TrapTile
            },
            new PathBuildOption
            {
                displayName = "Slow Tile",
                description = "Zählt als PathTile. Gegner werden 2s auf 0,45 Speed verlangsamt.",
                optionType = PathBuildOptionType.SlowTile
            },
            new PathBuildOption
            {
                displayName = "Knock Tile",
                description = "Zählt als PathTile. Wirft Gegner 3 PathTiles zurück. 2s Cooldown pro Tile.",
                optionType = PathBuildOptionType.KnockTile
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

        if (descriptionText != null)
        {
            descriptionText.text = "Wähle ein Tile für diese Position.";
        }
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
            success = tileManager.TryExtendPathTo(hoveredGridPosition);
        else if (option.optionType == PathBuildOptionType.TrapTile ||
                 option.optionType == PathBuildOptionType.SlowTile ||
                 option.optionType == PathBuildOptionType.KnockTile)
            success = tileManager.TryExtendSpecialPathTo(hoveredGridPosition, option.optionType);
        else if (option.optionType == PathBuildOptionType.GoldTile)
            success = tileManager.TryBuildGoldTileAt(hoveredGridPosition);
        else
            Debug.Log(option.displayName + " gewählt. Funktion kommt später.");

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
<<<<<<< Updated upstream
    }

    private void ShowInvalidDirectionMessage(int index)
    {
        string directionName = index >= 0 && index < currentDirectionLabels.Length ? currentDirectionLabels[index] : "Richtung";
        string message = directionName + " ist blockiert. Wähle eine Richtung mit OK.";

        Debug.Log(message);

        if (descriptionText != null)
            descriptionText.text = message;
=======
>>>>>>> Stashed changes
    }

    public void CancelChoice()
    {
        choiceOpen = false;
        hasValidHover = false;

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