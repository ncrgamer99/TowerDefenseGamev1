using UnityEditor;
using UnityEngine;

public static class GeneratedAssetColorRepair
{
    [MenuItem("Tools/Tower Defense/Generated Assets/Repair Generated Colors")]
    public static void RunAll()
    {
        Debug.Log("GeneratedAssetColorRepair: Starte Material-Remap fuer Generated Tiles und Tower.");
        GeneratedTilePrefabSetup.CreateOrUpdateGeneratedTilePrefabs();
        GeneratedTowerPrefabSetup.CreateOrUpdateGeneratedTowerPrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GeneratedAssetColorRepair: Fertig.");
    }
}
