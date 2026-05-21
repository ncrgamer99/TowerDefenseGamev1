using System.Collections.Generic;
using UnityEngine;

public class GeneratedTilePrefabSet : MonoBehaviour
{
    public GameObject pathTilePrefab;
    public GameObject startTilePrefab;
    public GameObject baseTilePrefab;
    public GameObject buildTilePrefab;
    public GameObject trapTilePrefab;
    public GameObject slowTilePrefab;
    public GameObject knockTilePrefab;
    public GameObject comboTilePrefab;
    public GameObject specialTilePrefab;
    public GameObject bridgeTilePrefab;
    public GameObject goldTilePrefab;
    public GameObject rangeTilePrefab;
    public GameObject damageTilePrefab;
    public GameObject rateTilePrefab;
    public GameObject xpTilePrefab;
    public GameObject upgradeTilePrefab;
    public GameObject pathGhostTilePrefab;
    public GameObject blockedTilePrefab;

    public GameObject GetPrefabForPathOption(PathBuildOptionType optionType)
    {
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
            case PathBuildOptionType.SpecialTile:
                return specialTilePrefab;
            case PathBuildOptionType.BridgeTile:
                return bridgeTilePrefab;
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
            default:
                return null;
        }
    }

    public bool HasAllCorePrefabs()
    {
        return pathTilePrefab != null &&
               startTilePrefab != null &&
               baseTilePrefab != null &&
               buildTilePrefab != null &&
               pathGhostTilePrefab != null;
    }

    public string GetMissingPrefabReport()
    {
        List<string> missing = new List<string>();

        AddMissing(missing, pathTilePrefab, "Path Tile");
        AddMissing(missing, startTilePrefab, "Start Tile");
        AddMissing(missing, baseTilePrefab, "Base Tile");
        AddMissing(missing, buildTilePrefab, "Build Tile");
        AddMissing(missing, trapTilePrefab, "Trap Tile");
        AddMissing(missing, slowTilePrefab, "Slow Tile");
        AddMissing(missing, knockTilePrefab, "Knock Tile");
        AddMissing(missing, comboTilePrefab, "Combo Tile");
        AddMissing(missing, specialTilePrefab, "Special Tile");
        AddMissing(missing, bridgeTilePrefab, "Bridge Tile");
        AddMissing(missing, goldTilePrefab, "Gold Tile");
        AddMissing(missing, rangeTilePrefab, "Range Tile");
        AddMissing(missing, damageTilePrefab, "Damage Tile");
        AddMissing(missing, rateTilePrefab, "Rate Tile");
        AddMissing(missing, xpTilePrefab, "XP Tile");
        AddMissing(missing, upgradeTilePrefab, "Upgrade Tile");
        AddMissing(missing, pathGhostTilePrefab, "Path Ghost Tile");
        AddMissing(missing, blockedTilePrefab, "Blocked Tile");

        if (missing.Count == 0)
            return "Alle Generated Tile Prefabs sind zugewiesen.";

        return "Fehlende Generated Tile Prefabs: " + string.Join(", ", missing);
    }

    private static void AddMissing(List<string> missing, GameObject prefab, string label)
    {
        if (prefab == null)
            missing.Add(label);
    }
}
