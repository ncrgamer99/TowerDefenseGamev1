using UnityEngine;

[System.Serializable]
public class MainMenuLexiconPathTileEntry
{
    [Header("Identity")]
    public string entryId = "path_tile";
    public PathBuildOptionType optionType = PathBuildOptionType.PathTile;
    public string title = "Path Tile";
    public int sortOrder = 0;

    [Header("Description")]
    public string category = "Weg-Tile";

    [TextArea(1, 3)]
    public string description = "";

    [TextArea(1, 4)]
    public string functionText = "";

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(title);
    }
}
