using TMPro;
using UnityEngine;

public class BuildDebugUI : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public TileManager tileManager;
    public BuildManager buildManager;
    public PathBuildManager pathBuildManager;

    [Header("UI")]
    public TextMeshProUGUI debugText;

    [Header("Settings")]
    public bool showDebug = true;
    public float updateInterval = 0.1f;

    private float updateTimer = 0f;

    private void Update()
    {
        if (!showDebug)
        {
            if (debugText != null && debugText.gameObject.activeSelf)
            {
                debugText.gameObject.SetActive(false);
            }

            return;
        }

        if (debugText != null && !debugText.gameObject.activeSelf)
        {
            debugText.gameObject.SetActive(true);
        }

        updateTimer -= Time.deltaTime;

        if (updateTimer > 0f)
            return;

        updateTimer = updateInterval;

        UpdateDebugText();
    }

    private void UpdateDebugText()
    {
        if (debugText == null)
            return;

        if (gameManager == null || tileManager == null)
        {
            debugText.text = "Build Debug UI:\nMissing References!";
            return;
        }

        string phaseText = gameManager.currentPhase.ToString();
        bool buildAllowed = tileManager.IsBuildAllowed();

        string modeText = GetCurrentModeText();
        string selectedBuildText = GetSelectedBuildText();
        string pathChoiceText = GetPathChoiceText();
        string blockedText = GetBlockedText();

        Vector2Int basePosition = tileManager.GetBasePosition();
        Vector2Int currentDirection = tileManager.GetCurrentDirection();

        string validExtensionText;

        if (buildAllowed)
        {
            validExtensionText = tileManager.HasAnyValidExtension().ToString();
        }
        else
        {
            validExtensionText = "Not checked during Wave/Blocked";
        }

        string warningText = tileManager.GetBuildRestrictionWarningText();

        debugText.text =
            "BUILD DEBUG" +
            "\nPhase: " + phaseText +
            "\nBuild Allowed: " + buildAllowed +
            "\nMode: " + modeText +
            "\nPath Choice Open: " + pathChoiceText +
            "\nPlayer Blocked: " + blockedText +
            "\nBase Position: " + basePosition +
            "\nDirection: " + currentDirection +
            "\nValid Extension: " + validExtensionText +
            "\nSelected Build: " + selectedBuildText;

        if (!string.IsNullOrEmpty(warningText))
        {
            debugText.text += "\n\nWARNUNG:\n" + warningText;
        }
    }

    private string GetCurrentModeText()
    {
        if (gameManager != null && gameManager.IsPlayerBlocked())
        {
            return "Blocked";
        }

        if (tileManager != null && !tileManager.IsBuildAllowed())
        {
            return "Wave / Build Locked";
        }

        if (pathBuildManager != null && pathBuildManager.IsChoiceOpen())
        {
            return "Path Choice";
        }

        if (buildManager != null &&
            buildManager.selectedBuildOption != null &&
            buildManager.selectedBuildOption.placementType == PlacementType.BuildTile)
        {
            return "Tower Mode";
        }

        return "Path Mode";
    }

    private string GetSelectedBuildText()
    {
        if (buildManager == null)
            return "BuildManager Missing";

        if (buildManager.selectedBuildOption == null)
            return "None";

        return buildManager.selectedBuildOption.displayName;
    }

    private string GetPathChoiceText()
    {
        if (pathBuildManager == null)
            return "PathBuildManager Missing";

        return pathBuildManager.IsChoiceOpen().ToString();
    }

    private string GetBlockedText()
    {
        if (gameManager == null)
            return "GameManager Missing";

        return gameManager.IsPlayerBlocked().ToString();
    }
}