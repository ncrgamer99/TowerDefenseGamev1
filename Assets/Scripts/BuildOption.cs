using UnityEngine;

public enum PlacementType
{
    BuildTile,
    PathTile
}

[System.Serializable]
public class BuildOption
{
    [Header("Info")]
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("Visual")]
    public Sprite icon;

    [Header("Build")]
    public GameObject prefab;
    public int cost;
    public PlacementType placementType;

    public string GetTooltipText()
    {
        string safeName = string.IsNullOrEmpty(displayName) ? "Unbekannter Tower" : displayName;
        string safeDescription = string.IsNullOrEmpty(description) ? "Keine Beschreibung gesetzt." : description;

        return safeName + "\n" + safeDescription;
    }
}
