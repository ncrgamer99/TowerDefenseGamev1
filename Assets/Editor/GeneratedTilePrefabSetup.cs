using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GeneratedTilePrefabSetup
{
    private const string GeneratedPrefabFolder = "Assets/Prefabs/Tiles/Generated";
    private const string GeneratedModelFolder = "Assets/Models/Generated/Tiles";
    private const string GeneratedMaterialFolder = "Assets/Materials/Generated/Tiles";
    private const string PrefabSetObjectName = "GeneratedTilePrefabSet";
    private const string AutoRepairSessionKey = "GeneratedTilePrefabSetup.AutoRepairRanV21";
    private static readonly Vector3 DefaultGeneratedModelEulerCorrection = Vector3.zero;
    private const float VisualBottomLocalY = -0.05f;
    private static readonly Vector3 GeneratedVisualLocalPosition = new Vector3(0f, VisualBottomLocalY, 0f);

    private static readonly TileDefinition[] TileDefinitions =
    {
        new TileDefinition("TD_PathTile", new Color32(130, 105, 80, 255), false, false),
        new TileDefinition("TD_StartTile", new Color32(80, 160, 240, 255), false, false),
        new TileDefinition("TD_BaseTile", new Color32(220, 70, 70, 255), false, false),
        new TileDefinition("TD_BuildTile", new Color32(70, 120, 220, 255), true, false),
        new TileDefinition("TD_TrapTile", new Color32(170, 40, 40, 255), false, false),
        new TileDefinition("TD_SlowTile", new Color32(70, 180, 255, 255), false, false),
        new TileDefinition("TD_KnockTile", new Color32(60, 220, 240, 255), false, false),
        new TileDefinition("TD_ComboTile", new Color32(210, 80, 230, 255), false, false),
        new TileDefinition("TD_GoldTile", new Color32(245, 190, 40, 255), false, false),
        new TileDefinition("TD_RangeTile", new Color32(65, 155, 255, 255), false, false),
        new TileDefinition("TD_DamageTile", new Color32(235, 70, 70, 255), false, false),
        new TileDefinition("TD_RateTile", new Color32(75, 235, 130, 255), false, false),
        new TileDefinition("TD_XpTile", new Color32(155, 105, 255, 255), false, false),
        new TileDefinition("TD_UpgradeTile", new Color32(255, 245, 120, 255), false, false),
        new TileDefinition("TD_HealTile", new Color32(95, 225, 135, 255), false, false),
        new TileDefinition("TD_WeakpointTile", new Color(0.1f, 0.17f, 0.25f, 1f), false, false),
        new TileDefinition("TD_PathGhostTile", new Color32(90, 210, 255, 120), false, true),
        new TileDefinition("TD_BlockedTile", new Color32(80, 85, 95, 255), false, false)
    };

    [DidReloadScripts]
    private static void AutoRepairGeneratedTileSetupAfterReload()
    {
        EditorApplication.delayCall += AutoRepairGeneratedTileSetupIfNeeded;
    }

    private static void AutoRepairGeneratedTileSetupIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (SessionState.GetBool(AutoRepairSessionKey, false))
            return;

        if (!NeedsGeneratedTileSetupRepair())
            return;

        SessionState.SetBool(AutoRepairSessionKey, true);
        Debug.Log("GeneratedTilePrefabSetup: Alte oder fehlende Generated Tile Prefabs erkannt. Auto-Setup wird einmalig ausgefuehrt.");

        CreateOrUpdateGeneratedTilePrefabs();
        GeneratedTilePrefabSet prefabSet = CreateOrUpdateGeneratedTilePrefabSetInOpenSceneInternal();
        ApplyToPathBuildManagers(prefabSet);
        ApplyToTileManagers(prefabSet);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Tower Defense/Generated Tiles/Create Or Update Generated Tile Prefabs")]
    public static void CreateOrUpdateGeneratedTilePrefabs()
    {
        EnsureFolder(GeneratedPrefabFolder);
        EnsureFolder(GeneratedMaterialFolder);

        int createdOrUpdatedCount = 0;

        foreach (TileDefinition definition in TileDefinitions)
        {
            EnsureTileModelMaterialRemaps(definition);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            GameObject prefabRoot = UsesProceduralGeneratedTileVisual(definition)
                ? CreateProceduralGeneratedTileRoot(definition)
                : CreateGeneratedTileRoot(definition, model);

            if (prefabRoot == null)
            {
                Debug.LogWarning("GeneratedTilePrefabSetup: FBX fehlt, Prefab wird nicht ueberschrieben: " + definition.ModelPath);
                continue;
            }
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, definition.PrefabPath);
            Object.DestroyImmediate(prefabRoot);

            if (savedPrefab == null)
            {
                Debug.LogError("GeneratedTilePrefabSetup: Prefab konnte nicht gespeichert werden: " + definition.PrefabPath);
                continue;
            }

            createdOrUpdatedCount++;
            Debug.Log("GeneratedTilePrefabSetup: Prefab erstellt/aktualisiert: " + definition.PrefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("GeneratedTilePrefabSetup: Fertig. " + createdOrUpdatedCount + " Generated Tile Prefabs erstellt oder aktualisiert.");
    }

    [MenuItem("Tools/Tower Defense/Generated Tiles/Create Or Update Generated Tile Prefab Set In Open Scene")]
    public static void CreateOrUpdateGeneratedTilePrefabSetInOpenScene()
    {
        CreateOrUpdateGeneratedTilePrefabSetInOpenSceneInternal();
    }

    [MenuItem("Tools/Tower Defense/Generated Tiles/Apply Generated Tile Prefabs To Open Scene")]
    public static void ApplyGeneratedTilePrefabsToOpenScene()
    {
        CreateOrUpdateGeneratedTilePrefabs();
        GeneratedTilePrefabSet prefabSet = CreateOrUpdateGeneratedTilePrefabSetInOpenSceneInternal();

        ApplyToPathBuildManagers(prefabSet);
        ApplyToTileManagers(prefabSet);

        AssetDatabase.SaveAssets();

        Debug.Log("GeneratedTilePrefabSetup: Generated Tile Prefabs wurden auf die offene Szene angewendet.");
    }

    [MenuItem("Tools/Tower Defense/Generated Tiles/Replace Existing BuildTile Instances In Open Scene")]
    public static void ReplaceExistingBuildTileInstancesInOpenScene()
    {
        GameObject generatedBuildTilePrefab = LoadGeneratedPrefab("TD_BuildTile");

        if (generatedBuildTilePrefab == null)
        {
            Debug.LogWarning("GeneratedTilePrefabSetup: TD_BuildTile Prefab fehlt. Fuehre zuerst Create Or Update Generated Tile Prefabs aus.");
            return;
        }

        BuildTile[] buildTiles = Object.FindObjectsOfType<BuildTile>(true);
        int replacedCount = 0;

        foreach (BuildTile buildTile in buildTiles)
        {
            if (buildTile == null || !IsSceneObject(buildTile.gameObject))
                continue;

            if (IsGeneratedPrefabInstance(buildTile.gameObject, "TD_BuildTile"))
                continue;

            bool wasOccupied = buildTile.isOccupied;
            GameObject replacement = ReplaceSceneObjectWithPrefab(buildTile.gameObject, generatedBuildTilePrefab, "Replace BuildTile Instance");
            BuildTile replacementBuildTile = replacement != null ? replacement.GetComponent<BuildTile>() : null;

            if (replacementBuildTile != null)
                replacementBuildTile.isOccupied = wasOccupied;

            replacedCount++;
        }

        MarkAllLoadedScenesDirty();
        Debug.Log("GeneratedTilePrefabSetup: BuildTile-Instanzen ersetzt: " + replacedCount);
    }

    [MenuItem("Tools/Tower Defense/Generated Tiles/Replace Start And Base Tile Instances In Open Scene")]
    public static void ReplaceStartAndBaseTileInstancesInOpenScene()
    {
        int replacedCount = 0;
        replacedCount += ReplaceSceneTileInstances("StartTile", "TD_StartTile", "Assets/Prefabs/StartTile.prefab");
        replacedCount += ReplaceSceneTileInstances("BaseTile", "TD_BaseTile", "Assets/Prefabs/BaseTile.prefab");

        MarkAllLoadedScenesDirty();
        Debug.Log("GeneratedTilePrefabSetup: Start/Base-Tile-Instanzen ersetzt: " + replacedCount);
    }

    private static GameObject CreateGeneratedTileRoot(TileDefinition definition, GameObject model)
    {
        if (model == null)
            return null;

        GameObject root = new GameObject(definition.AssetName);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject visual = InstantiateModelVisual(model);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        Vector3 modelEulerCorrection = GetGeneratedModelEulerCorrection(definition);
        visual.transform.localRotation = Quaternion.Euler(modelEulerCorrection);
        visual.transform.localScale = Vector3.one;

        RemoveUnwantedImportedObjects(visual);
        AlignVisualToTileRoot(visual, definition);
        ValidateAndRepairMaterials(root, definition);
        AddTileCollider(root, definition);
        if (definition.AssetName == "TD_KnockTile")
            ConfigureKnockTile(root);

        if (definition.IsBuildTile)
            ConfigureBuildTile(root);

        return root;
    }

    private static GameObject CreateProceduralGeneratedTileRoot(TileDefinition definition)
    {
        if (definition.AssetName == "TD_WeakpointTile")
            return CreateWeakpointTileRootFromPathVisual(definition);

        GameObject root = new GameObject(definition.AssetName);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = GeneratedVisualLocalPosition;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        if (definition.AssetName == "TD_HealTile")
            CreateProceduralHealTileVisual(visual.transform, definition);

        ValidateAndRepairMaterials(root, definition);
        AddTileCollider(root, definition);
        return root;
    }

    private static GameObject CreateWeakpointTileRootFromPathVisual(TileDefinition definition)
    {
        GameObject pathModel = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModelFolder + "/TD_PathTile.fbx");
        if (pathModel == null)
            return null;

        GameObject root = new GameObject(definition.AssetName);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject visual = InstantiateModelVisual(pathModel);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        RemoveUnwantedImportedObjects(visual);
        RemoveVisibleFlowMarkerObjects(visual);
        AlignVisualToTileRoot(visual, definition);
        CreateFlowMarkerChildren(visual.transform, null);
        CreateWeakpointCrackVisual(visual.transform, definition);

        AddTileCollider(root, definition);
        return root;
    }

    private static void CreateProceduralHealTileVisual(Transform visual, TileDefinition definition)
    {
        Material baseMaterial = GetGeneratedTileMaterialOrFallback(definition, "heal_tile_dark");
        Material energyMaterial = GetGeneratedTileMaterialOrFallback(definition, "heal_heal_green");
        Material crossMaterial = GetGeneratedTileMaterialOrFallback(definition, "heal_white");

        CreatePrimitiveChild(visual, "Heal_Base", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(1f, 0.08f, 1f), baseMaterial);
        CreatePrimitiveChild(visual, "Heal_Surface", PrimitiveType.Cube, new Vector3(0f, 0.095f, 0f), new Vector3(0.8f, 0.035f, 0.8f), baseMaterial);
        CreatePrimitiveChild(visual, "Heal_Energy_Disc", PrimitiveType.Cylinder, new Vector3(0f, 0.142f, 0f), new Vector3(0.34f, 0.014f, 0.34f), energyMaterial);
        CreatePrimitiveChild(visual, "Heal_Cross_Horizontal", PrimitiveType.Cube, new Vector3(0f, 0.168f, 0f), new Vector3(0.54f, 0.03f, 0.1f), crossMaterial);
        CreatePrimitiveChild(visual, "Heal_Cross_Vertical", PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(0.1f, 0.032f, 0.54f), crossMaterial);
    }

    private static void CreateWeakpointCrackVisual(Transform visual, TileDefinition definition)
    {
        Material redMaterial = GetGeneratedTileMaterialOrFallback(definition, "weak_weak_red");

        CreatePrimitiveChild(visual, "Weakpoint_Crack_Main", PrimitiveType.Cube, new Vector3(0f, 0.172f, 0f), new Vector3(0.32f, 0.018f, 0.045f), redMaterial, Quaternion.Euler(0f, 45f, 0f));
        CreatePrimitiveChild(visual, "Weakpoint_Crack_Cross", PrimitiveType.Cube, new Vector3(0f, 0.174f, 0f), new Vector3(0.23f, 0.018f, 0.038f), redMaterial, Quaternion.Euler(0f, -45f, 0f));
        CreatePrimitiveChild(visual, "Weakpoint_Crack_Detail_A", PrimitiveType.Cube, new Vector3(-0.09f, 0.176f, 0.08f), new Vector3(0.14f, 0.016f, 0.032f), redMaterial, Quaternion.Euler(0f, -15f, 0f));
        CreatePrimitiveChild(visual, "Weakpoint_Crack_Detail_B", PrimitiveType.Cube, new Vector3(0.1f, 0.178f, -0.07f), new Vector3(0.12f, 0.016f, 0.032f), redMaterial, Quaternion.Euler(0f, 20f, 0f));
    }

    private static void CreatePathRailChildren(Transform visual, Material material)
    {
        CreatePrimitiveChild(visual, "Rail_South", PrimitiveType.Cube, new Vector3(0f, 0.145f, 0.49f), new Vector3(1.08f, 0.12f, 0.06f), material);
        CreatePrimitiveChild(visual, "Rail_North", PrimitiveType.Cube, new Vector3(0f, 0.145f, -0.49f), new Vector3(1.08f, 0.12f, 0.06f), material);
        CreatePrimitiveChild(visual, "Rail_West", PrimitiveType.Cube, new Vector3(0.49f, 0.145f, 0f), new Vector3(0.06f, 0.12f, 1.08f), material);
        CreatePrimitiveChild(visual, "Rail_East", PrimitiveType.Cube, new Vector3(-0.49f, 0.145f, 0f), new Vector3(0.06f, 0.12f, 1.08f), material);

        CreatePrimitiveChild(visual, "Corner_NE", PrimitiveType.Cube, new Vector3(-0.45f, 0.145f, -0.45f), new Vector3(0.09f, 0.13f, 0.09f), material);
        CreatePrimitiveChild(visual, "Corner_NW", PrimitiveType.Cube, new Vector3(0.45f, 0.145f, -0.45f), new Vector3(0.09f, 0.13f, 0.09f), material);
        CreatePrimitiveChild(visual, "Corner_SE", PrimitiveType.Cube, new Vector3(-0.45f, 0.145f, 0.45f), new Vector3(0.09f, 0.13f, 0.09f), material);
        CreatePrimitiveChild(visual, "Corner_SW", PrimitiveType.Cube, new Vector3(0.45f, 0.145f, 0.45f), new Vector3(0.09f, 0.13f, 0.09f), material);
    }

    private static void CreateFlowMarkerChildren(Transform visual, Material material)
    {
        CreateEmptyChild(visual, "Flow_North", Vector3.zero);
        CreateEmptyChild(visual, "Flow_South", Vector3.zero);
        CreateEmptyChild(visual, "Flow_East", Vector3.zero);
        CreateEmptyChild(visual, "Flow_West", Vector3.zero);
    }

    private static GameObject CreateEmptyChild(Transform parent, string childName, Vector3 localPosition)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child;
    }

    private static GameObject CreatePrimitiveChild(
        Transform parent,
        string childName,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        return CreatePrimitiveChild(parent, childName, primitiveType, localPosition, localScale, material, Quaternion.identity);
    }

    private static GameObject CreatePrimitiveChild(
        Transform parent,
        string childName,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        Quaternion localRotation)
    {
        GameObject child = GameObject.CreatePrimitive(primitiveType);
        child.name = childName;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;

        Renderer renderer = child.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        Collider[] colliders = child.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider != null)
                Object.DestroyImmediate(collider);
        }

        return child;
    }

    private static Material GetGeneratedTileMaterialOrFallback(TileDefinition definition, string sourceMaterialName)
    {
        Material material = GetGeneratedTileMaterialForSlot(definition, sourceMaterialName);
        return material != null ? material : GetOrCreateFallbackMaterial(definition);
    }

    private static bool UsesProceduralGeneratedTileVisual(TileDefinition definition)
    {
        return definition != null &&
               (definition.AssetName == "TD_HealTile" ||
                definition.AssetName == "TD_WeakpointTile");
    }

    private static GameObject InstantiateModelVisual(GameObject model)
    {
        GameObject visual = PrefabUtility.InstantiatePrefab(model) as GameObject;

        if (visual == null)
            visual = Object.Instantiate(model);

        return visual;
    }

    private static void AlignVisualToTileRoot(GameObject visual, TileDefinition definition)
    {
        if (visual == null)
            return;

        if (!TryGetRendererBounds(visual, out Bounds alignedBounds))
        {
            Debug.LogWarning("GeneratedTilePrefabSetup: Konnte Visual-Bounds nicht bestimmen: " + definition.ModelPath);
            return;
        }

        visual.transform.localPosition = GeneratedVisualLocalPosition;

        if (!TryGetRendererBounds(visual, out alignedBounds))
            return;

        Debug.Log(
            "GeneratedTilePrefabSetup: " + definition.AssetName +
            " rotiert (" + GetGeneratedModelEulerCorrection(definition) + ") und auf Ground ausgerichtet. Bounds: " +
            alignedBounds.size
        );
    }

    private static Vector3 GetGeneratedModelEulerCorrection(TileDefinition definition)
    {
        return DefaultGeneratedModelEulerCorrection;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();

        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void RemoveUnwantedImportedObjects(GameObject visualRoot)
    {
        Camera[] cameras = visualRoot.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera != null)
                Object.DestroyImmediate(camera.gameObject);
        }

        Light[] lights = visualRoot.GetComponentsInChildren<Light>(true);
        foreach (Light light in lights)
        {
            if (light != null)
                Object.DestroyImmediate(light.gameObject);
        }
    }

    private static void RemoveVisibleFlowMarkerObjects(GameObject visualRoot)
    {
        if (visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.gameObject == null)
                continue;

            if (IsVisibleFlowMarkerObjectName(renderer.gameObject.name))
                Object.DestroyImmediate(renderer.gameObject);
        }
    }

    private static void ValidateAndRepairMaterials(GameObject root, TileDefinition definition)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("GeneratedTilePrefabSetup: FBX enthaelt keine sichtbaren Renderer: " + definition.ModelPath);
            return;
        }

        Material fallbackMaterial = null;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] sharedMaterials = renderer.sharedMaterials;

            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                fallbackMaterial = fallbackMaterial != null ? fallbackMaterial : GetOrCreateFallbackMaterial(definition);
                renderer.sharedMaterials = new[] { fallbackMaterial };
                Debug.LogWarning("GeneratedTilePrefabSetup: Renderer ohne Material repariert: " + definition.ModelPath);
                continue;
            }

            bool changed = false;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material forcedMaterial = GetGeneratedTileMaterialForSlot(definition, sharedMaterials[i] != null ? sharedMaterials[i].name : "");
                if (forcedMaterial != null)
                {
                    if (sharedMaterials[i] != forcedMaterial)
                    {
                        sharedMaterials[i] = forcedMaterial;
                        changed = true;
                    }

                    continue;
                }

                if (sharedMaterials[i] != null)
                    continue;

                fallbackMaterial = fallbackMaterial != null ? fallbackMaterial : GetOrCreateFallbackMaterial(definition);
                sharedMaterials[i] = fallbackMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = sharedMaterials;
                Debug.LogWarning("GeneratedTilePrefabSetup: Fehlende Material-Slots repariert: " + definition.ModelPath);
            }
        }
    }

    private static void EnsureTileModelMaterialRemaps(TileDefinition definition)
    {
        if (definition == null || !UsesGeneratedTileModelMaterialRemaps(definition))
            return;

        ModelImporter importer = AssetImporter.GetAtPath(definition.ModelPath) as ModelImporter;
        if (importer == null)
            return;

        bool changed = false;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            changed = true;
        }

        if (importer.materialLocation != ModelImporterMaterialLocation.External)
        {
            importer.materialLocation = ModelImporterMaterialLocation.External;
            changed = true;
        }

        var externalObjectMap = importer.GetExternalObjectMap();
        string[] sourceMaterialNames = GetExpectedTileMaterialNames(definition);

        foreach (string sourceMaterialName in sourceMaterialNames)
        {
            if (string.IsNullOrEmpty(sourceMaterialName))
                continue;

            Material targetMaterial = GetGeneratedTileMaterialForSlot(definition, sourceMaterialName);
            if (targetMaterial == null)
                continue;

            AssetImporter.SourceAssetIdentifier sourceIdentifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterialName);

            if (!externalObjectMap.TryGetValue(sourceIdentifier, out Object mappedObject) || mappedObject != targetMaterial)
            {
                importer.AddRemap(sourceIdentifier, targetMaterial);
                changed = true;
            }
        }

        if (changed)
        {
            importer.SaveAndReimport();
            Debug.Log("GeneratedTilePrefabSetup: Material-Remaps aktualisiert: " + definition.ModelPath);
        }
    }

    private static Material GetGeneratedTileMaterialForSlot(TileDefinition definition, string sourceMaterialName)
    {
        if (definition == null || !UsesGeneratedTileMaterials(definition) || string.IsNullOrEmpty(sourceMaterialName))
            return null;

        string marker = sourceMaterialName.ToLowerInvariant();

        if (definition.AssetName == "TD_WeakpointTile" &&
            !marker.Contains("weak") &&
            !marker.Contains("weakpoint"))
        {
            return null;
        }

        Color color = ResolveTileMaterialColor(definition, marker);
        bool emission = IsTileEmissionMaterial(marker);

        string materialFolder = GeneratedMaterialFolder + "/" + definition.AssetName;
        EnsureFolder(materialFolder);

        string materialPath = GetGeneratedTileMaterialPath(definition, sourceMaterialName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        bool created = material == null;

        if (material == null)
            material = new Material(GetGeneratedMaterialShader());

        material.name = GetGeneratedTileMaterialAssetName(definition, sourceMaterialName);
        ApplyMaterialColor(material, color);
        ApplyEmissionColor(material, emission ? color : Color.black);

        if (created)
            AssetDatabase.CreateAsset(material, materialPath);
        else
            EditorUtility.SetDirty(material);

        return material;
    }

    private static string GetGeneratedTileMaterialPath(TileDefinition definition, string sourceMaterialName)
    {
        return GeneratedMaterialFolder + "/" + definition.AssetName + "/" + GetGeneratedTileMaterialAssetName(definition, sourceMaterialName) + ".mat";
    }

    private static string GetGeneratedTileMaterialAssetName(TileDefinition definition, string sourceMaterialName)
    {
        string marker = string.IsNullOrEmpty(sourceMaterialName) ? "" : sourceMaterialName.ToLowerInvariant();

        if (definition.AssetName == "TD_HealTile")
        {
            if (marker.Contains("white") || marker.Contains("cross"))
                return "Heal_Cross";
            if (marker.Contains("energy") || marker.Contains("green") || marker.Contains("glow") || marker.Contains("node"))
                return "Heal_Energy";
            if (marker.Contains("edge"))
                return "Heal_Edge";
            return "Heal_Dark";
        }

        if (definition.AssetName == "TD_WeakpointTile")
        {
            if (marker.Contains("path") || marker.Contains("cyan"))
                return "Weakpoint_Path";
            if (marker.Contains("red"))
                return "Weakpoint_Red";
            if (marker.Contains("orange"))
                return "Weakpoint_Orange";
            if (marker.Contains("edge"))
                return "Weakpoint_Edge";
            if (marker.Contains("surface"))
                return "Weakpoint_Surface";
            return "Weakpoint_Base";
        }

        if (definition.AssetName == "TD_KnockTile")
        {
            if (marker.Contains("surface"))
                return "Knock_Surface";
            if (marker.Contains("cyan") || marker.Contains("path") || marker.Contains("glow"))
                return "Knock_Path";
            if (marker.Contains("edge") || marker.Contains("rail"))
                return "Knock_Edge";
            if (marker.Contains("piston_dark"))
                return "Knock_Piston_Dark";
            if (marker.Contains("piston_blue"))
                return "Knock_Piston_Blue";
            if (marker.Contains("piston") || marker.Contains("metal"))
                return "Knock_Piston_Metal";
            if (marker.Contains("panel"))
                return "Knock_Panel";
            return "Knock_Base";
        }

        return SanitizeAssetFileName(sourceMaterialName);
    }

    private static bool UsesGeneratedTileMaterials(TileDefinition definition)
    {
        return definition.AssetName == "TD_HealTile" ||
               definition.AssetName == "TD_WeakpointTile";
    }

    private static bool UsesGeneratedTileModelMaterialRemaps(TileDefinition definition)
    {
        return definition.AssetName == "TD_HealTile" ||
               definition.AssetName == "TD_WeakpointTile";
    }

    private static string[] GetExpectedTileMaterialNames(TileDefinition definition)
    {
        if (definition.AssetName == "TD_HealTile")
        {
            return new[]
            {
                "heal_white",
                "heal_heal_green",
                "heal_tile_dark",
                "heal_edge"
            };
        }

        if (definition.AssetName == "TD_WeakpointTile")
        {
            return new[]
            {
                "weak_weak_red",
                "weak_edge",
                "weak_path_cyan",
                "weak_weak_orange",
                "weak_road",
                "weak_base",
                "weak_surface"
            };
        }

        if (definition.AssetName == "TD_KnockTile")
        {
            return new[]
            {
                "knock_base",
                "knock_surface",
                "knock_edge",
                "knock_path_cyan",
                "knock_piston_dark",
                "knock_piston_metal",
                "knock_piston_blue"
            };
        }

        return new string[0];
    }

    private static Color ResolveTileMaterialColor(TileDefinition definition, string marker)
    {
        if (definition.AssetName == "TD_HealTile")
        {
            if (marker.Contains("white") || marker.Contains("cross"))
                return new Color32(80, 245, 135, 255);
            if (marker.Contains("energy") || marker.Contains("green") || marker.Contains("glow") || marker.Contains("node"))
                return new Color32(45, 125, 85, 255);
            if (marker.Contains("edge"))
                return new Color32(82, 92, 104, 255);
            if (marker.Contains("dark") || marker.Contains("tile") || marker.Contains("base") || marker.Contains("surface"))
                return new Color32(82, 92, 104, 255);
            return definition.FallbackColor;
        }

        if (definition.AssetName == "TD_WeakpointTile")
        {
            if (marker.Contains("path") || marker.Contains("cyan"))
                return new Color(0f, 0.88f, 1f, 1f);
            if (marker.Contains("red"))
                return new Color32(235, 70, 70, 255);
            if (marker.Contains("orange"))
                return new Color(0.36f, 0.39f, 0.42f, 1f);
            if (marker.Contains("edge"))
                return new Color(0.36f, 0.39f, 0.42f, 1f);
            if (marker.Contains("surface"))
                return new Color(0.1f, 0.17f, 0.25f, 1f);
            if (marker.Contains("road") || marker.Contains("base"))
                return new Color(0.1f, 0.12f, 0.15f, 1f);
            return definition.FallbackColor;
        }

        if (definition.AssetName == "TD_KnockTile")
        {
            if (marker.Contains("surface"))
                return new Color32(26, 43, 64, 255);
            if (marker.Contains("path") || marker.Contains("cyan") || marker.Contains("glow"))
                return new Color32(0, 224, 255, 255);
            if (marker.Contains("edge") || marker.Contains("rail"))
                return new Color32(92, 99, 107, 255);
            if (marker.Contains("piston_blue"))
                return new Color32(0, 224, 255, 255);
            if (marker.Contains("piston_dark"))
                return new Color32(26, 43, 64, 255);
            if (marker.Contains("piston") || marker.Contains("metal"))
                return new Color32(92, 99, 107, 255);
            if (marker.Contains("panel"))
                return new Color32(26, 43, 64, 255);
            if (marker.Contains("base") || marker.Contains("road"))
                return new Color32(26, 31, 38, 255);
            return definition.FallbackColor;
        }

        return definition.FallbackColor;
    }

    private static bool IsTileEmissionMaterial(string marker)
    {
        return marker.Contains("energy") ||
               marker.Contains("green") ||
               marker.Contains("glow") ||
               marker.Contains("path") ||
               marker.Contains("cyan") ||
               marker.Contains("piston_blue");
    }

    private static Material GetOrCreateFallbackMaterial(TileDefinition definition)
    {
        string materialPath = GeneratedMaterialFolder + "/" + definition.AssetName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        material = new Material(shader);
        material.name = definition.AssetName;
        ApplyMaterialColor(material, definition.FallbackColor);
        AssetDatabase.CreateAsset(material, materialPath);
        Debug.LogWarning("GeneratedTilePrefabSetup: Fallback-Material erstellt, weil FBX-Material fehlte: " + materialPath);
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (color.a < 0.99f)
        {
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3000;
        }
    }

    private static Shader GetGeneratedMaterialShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        return shader;
    }

    private static void ApplyEmissionColor(Material material, Color emissionColor)
    {
        if (material == null)
            return;

        bool emissionEnabled = emissionColor.maxColorComponent > 0.001f;

        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emissionEnabled ? emissionColor * 1.35f : Color.black);

        if (emissionEnabled)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.1f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.45f);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.45f);
    }

    private static string SanitizeAssetFileName(string fileName)
    {
        string safeName = string.IsNullOrEmpty(fileName) ? "Material" : fileName.Trim();

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(invalidChar, '_');

        return string.IsNullOrEmpty(safeName) ? "Material" : safeName;
    }

    private static void AddTileCollider(GameObject root, TileDefinition definition)
    {
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = definition.IsGhostTile ? new Vector3(0f, 0.05f, 0f) : new Vector3(0f, 0.1f, 0f);
        collider.size = definition.IsGhostTile ? new Vector3(1f, 0.1f, 1f) : new Vector3(1f, 0.25f, 1f);
        collider.isTrigger = definition.IsGhostTile;
    }

    private static void ConfigureBuildTile(GameObject root)
    {
        BuildTile buildTile = root.AddComponent<BuildTile>();
        buildTile.isOccupied = false;
        buildTile.useHoverFeedback = true;
    }

    private static void ConfigureKnockTile(GameObject root)
    {
        if (root != null && root.GetComponent<KnockTileVisualAnimator>() == null)
            root.AddComponent<KnockTileVisualAnimator>();
    }

    private static GeneratedTilePrefabSet CreateOrUpdateGeneratedTilePrefabSetInOpenSceneInternal()
    {
        GameObject prefabSetObject = FindSceneGameObjectByName(PrefabSetObjectName);

        if (prefabSetObject == null)
        {
            prefabSetObject = new GameObject(PrefabSetObjectName);
            Undo.RegisterCreatedObjectUndo(prefabSetObject, "Create Generated Tile Prefab Set");
        }

        GeneratedTilePrefabSet prefabSet = prefabSetObject.GetComponent<GeneratedTilePrefabSet>();
        if (prefabSet == null)
            prefabSet = Undo.AddComponent<GeneratedTilePrefabSet>(prefabSetObject);

        Undo.RecordObject(prefabSet, "Update Generated Tile Prefab Set");
        AssignPrefabSetFields(prefabSet);
        EditorUtility.SetDirty(prefabSet);

        if (prefabSetObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(prefabSetObject.scene);

        Debug.Log("GeneratedTilePrefabSetup: GeneratedTilePrefabSet aktualisiert. " + prefabSet.GetMissingPrefabReport());
        return prefabSet;
    }

    private static void AssignPrefabSetFields(GeneratedTilePrefabSet prefabSet)
    {
        if (prefabSet == null)
            return;

        prefabSet.pathTilePrefab = LoadGeneratedPrefab("TD_PathTile");
        prefabSet.startTilePrefab = LoadGeneratedPrefab("TD_StartTile");
        prefabSet.baseTilePrefab = LoadGeneratedPrefab("TD_BaseTile");
        prefabSet.buildTilePrefab = LoadGeneratedPrefab("TD_BuildTile");
        prefabSet.trapTilePrefab = LoadGeneratedPrefab("TD_TrapTile");
        prefabSet.slowTilePrefab = LoadGeneratedPrefab("TD_SlowTile");
        prefabSet.knockTilePrefab = LoadGeneratedPrefab("TD_KnockTile");
        prefabSet.comboTilePrefab = LoadGeneratedPrefab("TD_ComboTile");
        prefabSet.goldTilePrefab = LoadGeneratedPrefab("TD_GoldTile");
        prefabSet.rangeTilePrefab = LoadGeneratedPrefab("TD_RangeTile");
        prefabSet.damageTilePrefab = LoadGeneratedPrefab("TD_DamageTile");
        prefabSet.rateTilePrefab = LoadGeneratedPrefab("TD_RateTile");
        prefabSet.xpTilePrefab = LoadGeneratedPrefab("TD_XpTile");
        prefabSet.upgradeTilePrefab = LoadGeneratedPrefab("TD_UpgradeTile");
        prefabSet.healTilePrefab = LoadGeneratedPrefab("TD_HealTile");
        prefabSet.weakpointTilePrefab = LoadGeneratedPrefab("TD_WeakpointTile");
        prefabSet.pathGhostTilePrefab = LoadGeneratedPrefab("TD_PathGhostTile");
        prefabSet.blockedTilePrefab = LoadGeneratedPrefab("TD_BlockedTile");
    }

    private static void ApplyToPathBuildManagers(GeneratedTilePrefabSet prefabSet)
    {
        PathBuildManager[] pathBuildManagers = Object.FindObjectsOfType<PathBuildManager>(true);

        if (pathBuildManagers == null || pathBuildManagers.Length == 0)
        {
            Debug.LogWarning("GeneratedTilePrefabSetup: Kein PathBuildManager in der offenen Szene gefunden.");
            return;
        }

        foreach (PathBuildManager pathBuildManager in pathBuildManagers)
        {
            if (pathBuildManager == null)
                continue;

            Undo.RecordObject(pathBuildManager, "Apply Generated Tile Prefabs");
            pathBuildManager.generatedTilePrefabSet = prefabSet;
            pathBuildManager.useGeneratedTilePrefabs = true;
            pathBuildManager.allowSpecialTilePlacementV1 = true;
            pathBuildManager.specialTilesUsePathBehaviourInV1 = true;

            if (prefabSet != null && prefabSet.pathGhostTilePrefab != null)
                pathBuildManager.pathGhostPrefab = prefabSet.pathGhostTilePrefab;
            else
                Debug.LogWarning("GeneratedTilePrefabSetup: TD_PathGhostTile fehlt, pathGhostPrefab wurde nicht gesetzt.");

            if (pathBuildManager.randomOptions == null)
                pathBuildManager.randomOptions = new List<PathBuildOption>();

            EnsurePathBuildRandomOptions(pathBuildManager.randomOptions);
            EditorUtility.SetDirty(pathBuildManager);

            if (pathBuildManager.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(pathBuildManager.gameObject.scene);

            Debug.Log("GeneratedTilePrefabSetup: PathBuildManager aktualisiert: " + pathBuildManager.name);
        }
    }

    private static void ApplyToTileManagers(GeneratedTilePrefabSet prefabSet)
    {
        TileManager[] tileManagers = Object.FindObjectsOfType<TileManager>(true);

        if (tileManagers == null || tileManagers.Length == 0)
        {
            Debug.LogWarning("GeneratedTilePrefabSetup: Kein TileManager in der offenen Szene gefunden.");
            return;
        }

        foreach (TileManager tileManager in tileManagers)
        {
            if (tileManager == null)
                continue;

            Undo.RecordObject(tileManager, "Apply Generated Tile Prefabs");
            tileManager.generatedTilePrefabSet = prefabSet;
            tileManager.useGeneratedTilePrefabs = true;
            tileManager.skipProceduralRailingsOnGeneratedTiles = true;
            tileManager.useGeneratedTileDirectRails = true;
            tileManager.invertGeneratedRailSideNames = true;
            tileManager.rotateGeneratedPathTileVisualsToFlow = true;
            tileManager.generatedPathTileVisualEulerBase = Vector3.zero;
            tileManager.generatedPathTileForwardYawOffset = 0f;
            tileManager.generatedTileGroundLocalY = VisualBottomLocalY;

            if (prefabSet != null)
            {
                AssignIfNotNull(ref tileManager.pathTilePrefab, prefabSet.pathTilePrefab);
                AssignIfNotNull(ref tileManager.startTilePrefab, prefabSet.startTilePrefab);
                AssignIfNotNull(ref tileManager.baseTilePrefab, prefabSet.baseTilePrefab);
                AssignIfNotNull(ref tileManager.buildTilePrefab, prefabSet.buildTilePrefab);
                AssignIfNotNull(ref tileManager.trapTilePrefab, prefabSet.trapTilePrefab);
                AssignIfNotNull(ref tileManager.slowTilePrefab, prefabSet.slowTilePrefab);
                AssignIfNotNull(ref tileManager.knockTilePrefab, prefabSet.knockTilePrefab);
                AssignIfNotNull(ref tileManager.comboTilePrefab, prefabSet.comboTilePrefab);
                AssignIfNotNull(ref tileManager.goldTilePrefab, prefabSet.goldTilePrefab);
                AssignIfNotNull(ref tileManager.rangeTilePrefab, prefabSet.rangeTilePrefab);
                AssignIfNotNull(ref tileManager.damageTilePrefab, prefabSet.damageTilePrefab);
                AssignIfNotNull(ref tileManager.rateTilePrefab, prefabSet.rateTilePrefab);
                AssignIfNotNull(ref tileManager.xpTilePrefab, prefabSet.xpTilePrefab);
                AssignIfNotNull(ref tileManager.upgradeTilePrefab, prefabSet.upgradeTilePrefab);
                AssignIfNotNull(ref tileManager.healTilePrefab, prefabSet.healTilePrefab);
                AssignIfNotNull(ref tileManager.weakpointTilePrefab, prefabSet.weakpointTilePrefab);
                AssignIfNotNull(ref tileManager.blockedTilePrefab, prefabSet.blockedTilePrefab);
            }

            EditorUtility.SetDirty(tileManager);

            if (tileManager.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(tileManager.gameObject.scene);

            Debug.Log("GeneratedTilePrefabSetup: TileManager aktualisiert: " + tileManager.name);
        }
    }

    private static void EnsurePathBuildRandomOptions(List<PathBuildOption> options)
    {
        if (options == null)
            return;

        RemoveRemovedPathBuildOptions(options);
        AddOrUpdatePathOption(options, PathBuildOptionType.TrapTile, "Trap Tile", "Gefaehrliches Pfad-Tile. V1: zaehlt als Pfad-Variante.");
        AddOrUpdatePathOption(options, PathBuildOptionType.SlowTile, "Slow Tile", "Slow-Tile: Weg-Tile. Gegner werden kurz stark verlangsamt.");
        AddOrUpdatePathOption(options, PathBuildOptionType.KnockTile, "Knock Tile", "Knock-Tile: Weg-Tile. Wirft normale Gegner zurueck; Boss/MiniBoss/Elite immun.");
        AddOrUpdatePathOption(options, PathBuildOptionType.RangeTile, "Range Tile", "Range-Tile: kein Weg. Baue einen Tower darauf: +1 Reichweite.");
        AddOrUpdatePathOption(options, PathBuildOptionType.DamageTile, "Damage Tile", "Damage-Tile: kein Weg. Baue einen Tower darauf: +25% Schaden.");
        AddOrUpdatePathOption(options, PathBuildOptionType.RateTile, "Rate Tile", "Rate-Tile: kein Weg. Baue einen Tower darauf: +20% Feuerrate.");
        AddOrUpdatePathOption(options, PathBuildOptionType.XPTile, "XP Tile", "XP-Tile: kein Weg. Baue einen Tower darauf: +25% XP.");
        AddOrUpdatePathOption(options, PathBuildOptionType.UpgradeTile, "Upgrade Tile", "Upgrade-Tile: kein Weg. Baue einen Tower darauf: Point-Upgrades +1 staerker.");
        AddOrUpdatePathOption(options, PathBuildOptionType.HealTile, "Heal Tile", "Heal-Tile: kein Weg. Tower macht weniger Schaden, 2% Kill-Chance auf +1 Leben.");
        AddOrUpdatePathOption(options, PathBuildOptionType.WeakpointTile, "Weakpoint Tile", "Weakpoint-Tile: Weg-Tile. Ruestung max. 2 und -50% fuer 10s.");
        AddOrUpdatePathOption(options, PathBuildOptionType.ComboTile, "Combo Tile", "Combo-Tile: Darkness 20 Schaden alle 3s fuer 10s, wenn Burn, Poison und Bleed aktiv sind.");
        RemoveDuplicatePathOptions(options);
    }

    private static void RemoveRemovedPathBuildOptions(List<PathBuildOption> options)
    {
        if (options == null)
            return;

        for (int i = options.Count - 1; i >= 0; i--)
        {
            PathBuildOption option = options[i];

            if (option == null || IsRemovedPathBuildSelectionOption(option.optionType))
                options.RemoveAt(i);
        }
    }

    private static bool IsRemovedPathBuildSelectionOption(PathBuildOptionType optionType)
    {
        int rawOptionValue = (int)optionType;
        return rawOptionValue == 2 ||
               rawOptionValue == 3 ||
               optionType == PathBuildOptionType.GoldTile;
    }

    private static void AddOrUpdatePathOption(List<PathBuildOption> options, PathBuildOptionType optionType, string displayName, string description)
    {
        PathBuildOption option = null;

        foreach (PathBuildOption candidate in options)
        {
            if (candidate != null && candidate.optionType == optionType)
            {
                option = candidate;
                break;
            }
        }

        if (option == null)
        {
            options.Add(new PathBuildOption
            {
                displayName = displayName,
                description = description,
                optionType = optionType
            });
            return;
        }

        if (string.IsNullOrEmpty(option.displayName) || IsGeneratedTileOptionName(option.displayName))
            option.displayName = displayName;

        if (ShouldReplaceOptionDescription(option.description))
            option.description = description;
    }

    private static void RemoveDuplicatePathOptions(List<PathBuildOption> options)
    {
        HashSet<PathBuildOptionType> seenTypes = new HashSet<PathBuildOptionType>();

        for (int i = 0; i < options.Count; i++)
        {
            PathBuildOption option = options[i];

            if (option == null)
            {
                options.RemoveAt(i);
                i--;
                continue;
            }

            if (seenTypes.Contains(option.optionType))
            {
                options.RemoveAt(i);
                i--;
            }
            else
            {
                seenTypes.Add(option.optionType);
            }
        }
    }

    private static bool IsGeneratedTileOptionName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
            return true;

        string normalized = displayName.Trim().ToLowerInvariant();
        return normalized == "trap tile" ||
               normalized == "slow tile" ||
               normalized == "slowtile" ||
               normalized == "knock tile" ||
               normalized == "knocktile" ||
               normalized == "range tile" ||
               normalized == "rangetile" ||
               normalized == "damage tile" ||
               normalized == "damagetile" ||
               normalized == "rate tile" ||
               normalized == "ratetile" ||
               normalized == "xp tile" ||
               normalized == "xptile" ||
               normalized == "upgrade tile" ||
               normalized == "upgradetile" ||
               normalized == "combo tile" ||
               normalized == "combotile" ||
               normalized == "special tile" ||
               normalized == "bridge tile" ||
               normalized == "gold tile";
    }

    private static bool ShouldReplaceOptionDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
            return true;

        string normalized = description.ToLowerInvariant();
        return normalized.Contains("platzhalter") ||
               normalized.Contains("spater") ||
               normalized.Contains("spaeter") ||
               normalized.Contains("kommt") ||
               normalized.Contains("blockiert dieses feld") ||
               normalized.Contains("gold");
    }

    private static int ReplaceSceneTileInstances(string oldObjectName, string generatedPrefabName, string oldPrefabPath)
    {
        GameObject generatedPrefab = LoadGeneratedPrefab(generatedPrefabName);

        if (generatedPrefab == null)
        {
            Debug.LogWarning("GeneratedTilePrefabSetup: Generated Prefab fehlt: " + generatedPrefabName);
            return 0;
        }

        List<GameObject> candidates = FindSceneObjectsByNameOrPrefab(oldObjectName, oldPrefabPath, generatedPrefabName);
        int replacedCount = 0;

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || IsGeneratedPrefabInstance(candidate, generatedPrefabName))
                continue;

            ReplaceSceneObjectWithPrefab(candidate, generatedPrefab, "Replace " + oldObjectName + " Instance");
            replacedCount++;
        }

        return replacedCount;
    }

    private static GameObject ReplaceSceneObjectWithPrefab(GameObject oldObject, GameObject prefab, string undoName)
    {
        if (oldObject == null || prefab == null)
            return null;

        Transform oldTransform = oldObject.transform;
        Transform oldParent = oldTransform.parent;
        int oldSiblingIndex = oldTransform.GetSiblingIndex();
        Vector3 oldLocalPosition = oldTransform.localPosition;
        Quaternion oldLocalRotation = oldTransform.localRotation;
        Vector3 oldLocalScale = oldTransform.localScale;
        bool wasActive = oldObject.activeSelf;
        Scene scene = oldObject.scene;

        GameObject replacement = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (replacement == null)
            replacement = Object.Instantiate(prefab);

        Undo.RegisterCreatedObjectUndo(replacement, undoName);
        replacement.transform.SetParent(oldParent, false);
        replacement.transform.localPosition = oldLocalPosition;
        replacement.transform.localRotation = oldLocalRotation;
        replacement.transform.localScale = oldLocalScale;
        replacement.transform.SetSiblingIndex(oldSiblingIndex);
        replacement.SetActive(wasActive);

        Undo.DestroyObjectImmediate(oldObject);
        return replacement;
    }

    private static List<GameObject> FindSceneObjectsByNameOrPrefab(string objectName, string oldPrefabPath, string generatedPrefabName)
    {
        List<GameObject> results = new List<GameObject>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform transform in transforms)
                {
                    if (transform == null)
                        continue;

                    GameObject candidate = transform.gameObject;

                    if (candidate.name == objectName || IsPrefabInstanceFromPath(candidate, oldPrefabPath))
                    {
                        if (!IsGeneratedPrefabInstance(candidate, generatedPrefabName))
                            results.Add(candidate);
                    }
                }
            }
        }

        return results;
    }

    private static GameObject FindSceneGameObjectByName(string objectName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform transform in transforms)
                {
                    if (transform != null && transform.gameObject.name == objectName)
                        return transform.gameObject;
                }
            }
        }

        return null;
    }

    private static bool IsSceneObject(GameObject gameObject)
    {
        return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
    }

    private static bool IsGeneratedPrefabInstance(GameObject gameObject, string generatedPrefabName)
    {
        if (gameObject == null)
            return false;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        return prefabPath == GeneratedPrefabFolder + "/" + generatedPrefabName + ".prefab";
    }

    private static bool IsPrefabInstanceFromPath(GameObject gameObject, string prefabPath)
    {
        if (gameObject == null)
            return false;

        return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject) == prefabPath;
    }

    private static void MarkAllLoadedScenesDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static GameObject LoadGeneratedPrefab(string prefabName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedPrefabFolder + "/" + prefabName + ".prefab");
    }

    private static void AssignIfNotNull(ref GameObject target, GameObject value)
    {
        if (value != null)
            target = value;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');

        if (parts.Length == 0)
            return;

        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[i]);

            currentPath = nextPath;
        }
    }

    private static bool NeedsGeneratedTileSetupRepair()
    {
        if (!AllGeneratedTilePrefabsExist())
            return true;

        if (AnyGeneratedTileModelNeedsMaterialRepair())
            return true;

        if (AnyGeneratedTilePrefabNeedsDirectRailRepair())
            return true;

        GeneratedTilePrefabSet[] prefabSets = Object.FindObjectsOfType<GeneratedTilePrefabSet>(true);
        if (prefabSets == null || prefabSets.Length == 0)
            return HasLoadedScene();

        foreach (GeneratedTilePrefabSet prefabSet in prefabSets)
        {
            if (PrefabSetNeedsRepair(prefabSet))
                return true;
        }

        TileManager[] tileManagers = Object.FindObjectsOfType<TileManager>(true);
        foreach (TileManager tileManager in tileManagers)
        {
            if (TileManagerNeedsRepair(tileManager))
                return true;
        }

        PathBuildManager[] pathBuildManagers = Object.FindObjectsOfType<PathBuildManager>(true);
        foreach (PathBuildManager pathBuildManager in pathBuildManagers)
        {
            if (PathBuildManagerNeedsRepair(pathBuildManager))
                return true;
        }

        return false;
    }

    private static bool AnyGeneratedTileModelNeedsMaterialRepair()
    {
        foreach (TileDefinition definition in TileDefinitions)
        {
            if (!UsesGeneratedTileModelMaterialRemaps(definition))
                continue;

            if (TileModelMaterialRemapsNeedRepair(definition))
                return true;
        }

        return false;
    }

    private static bool TileModelMaterialRemapsNeedRepair(TileDefinition definition)
    {
        ModelImporter importer = AssetImporter.GetAtPath(definition.ModelPath) as ModelImporter;
        if (importer == null)
            return true;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
            importer.materialLocation != ModelImporterMaterialLocation.External)
        {
            return true;
        }

        var externalObjectMap = importer.GetExternalObjectMap();
        string[] sourceMaterialNames = GetExpectedTileMaterialNames(definition);

        foreach (string sourceMaterialName in sourceMaterialNames)
        {
            if (string.IsNullOrEmpty(sourceMaterialName))
                continue;

            string expectedPath = GetGeneratedTileMaterialPath(definition, sourceMaterialName);
            Material expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(expectedPath);
            if (expectedMaterial == null)
                return true;

            AssetImporter.SourceAssetIdentifier sourceIdentifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterialName);

            if (!externalObjectMap.TryGetValue(sourceIdentifier, out Object mappedObject) || mappedObject != expectedMaterial)
                return true;
        }

        return false;
    }

    private static TileDefinition FindTileDefinition(string assetName)
    {
        foreach (TileDefinition definition in TileDefinitions)
        {
            if (definition != null && definition.AssetName == assetName)
                return definition;
        }

        return null;
    }

    private static bool AnyGeneratedTilePrefabNeedsDirectRailRepair()
    {
        foreach (TileDefinition definition in TileDefinitions)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);

            if (prefab == null)
                return true;

            if (GeneratedModelIsNewerThanPrefab(definition))
                return true;

            if (UsesProceduralGeneratedTileVisual(definition) && !PrefabUsesProceduralGeneratedTileVisual(prefab, definition))
                return true;

            if (PrefabShouldHideVisibleFlowMarkers(definition) && PrefabHasVisibleFlowMarker(prefab))
                return true;

            if (PrefabVisualNeedsRotationRepair(prefab, definition))
                return true;

            if (PrefabVisualBottomNeedsRepair(prefab))
                return true;
        }

        if (PrefabHasAnyRailOrCorner(LoadGeneratedPrefab("TD_BuildTile")) ||
            PrefabHasAnyRailOrCorner(LoadGeneratedPrefab("TD_BlockedTile")) ||
            PrefabHasAnyRailOrCorner(LoadGeneratedPrefab("TD_PathGhostTile")))
        {
            return true;
        }

        GameObject startPrefab = LoadGeneratedPrefab("TD_StartTile");

        if (startPrefab == null ||
            FindChildByNameContains(startPrefab.transform, "StartChevron") != null ||
            FindChildByNameContains(startPrefab.transform, "StartCircle") != null)
        {
            return true;
        }

        string[] pathLikePrefabNames =
        {
            "TD_PathTile",
            "TD_StartTile",
            "TD_TrapTile",
            "TD_GoldTile",
            "TD_KnockTile",
            "TD_WeakpointTile"
        };

        foreach (string prefabName in pathLikePrefabNames)
        {
            GameObject prefab = LoadGeneratedPrefab(prefabName);

            if (prefab == null)
                return true;

            if (FindChildByNameContains(prefab.transform, "Flow_North") == null)
                return true;

            if (PrefabFlowMarkerNeedsCenterRepair(prefab))
                return true;

            if (prefabName != "TD_PathTile" && PrefabHasVisibleFlowMarker(prefab))
                return true;

            if (!PrefabHasAllNamedRails(prefab))
                return true;
        }

        GameObject knockPrefab = LoadGeneratedPrefab("TD_KnockTile");
        if (knockPrefab == null ||
            knockPrefab.GetComponent<KnockTileVisualAnimator>() == null ||
            FindChildByNameContains(knockPrefab.transform, "Knock_North") == null ||
            FindChildByNameContains(knockPrefab.transform, "Knock_East") == null ||
            FindChildByNameContains(knockPrefab.transform, "Knock_South") == null ||
            FindChildByNameContains(knockPrefab.transform, "Knock_West") == null ||
            PrefabUsesGeneratedKnockMaterials(knockPrefab))
        {
            return true;
        }

        GameObject basePrefab = LoadGeneratedPrefab("TD_BaseTile");

        if (basePrefab == null)
            return true;

        return PrefabVisualNeedsRotationRepair(basePrefab, FindTileDefinition("TD_BaseTile")) ||
               !PrefabHasAllNamedRails(basePrefab) ||
               FindChildByNameContains(basePrefab.transform, "Corner_NE") == null ||
               FindChildByNameContains(basePrefab.transform, "Corner_NW") == null ||
               FindChildByNameContains(basePrefab.transform, "Corner_SE") == null ||
               FindChildByNameContains(basePrefab.transform, "Corner_SW") == null;
    }

    private static bool PrefabUsesGeneratedKnockMaterials(GameObject prefab)
    {
        if (prefab == null)
            return true;

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            foreach (Material material in materials)
            {
                if (material == null)
                    continue;

                string materialPath = AssetDatabase.GetAssetPath(material);
                if (!string.IsNullOrEmpty(materialPath) &&
                    materialPath.StartsWith(GeneratedMaterialFolder + "/TD_KnockTile/", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PrefabShouldHideVisibleFlowMarkers(TileDefinition definition)
    {
        return definition != null &&
               definition.AssetName != "TD_PathTile" &&
               definition.AssetName != "TD_PathGhostTile";
    }

    private static bool PrefabHasVisibleFlowMarker(GameObject prefab)
    {
        if (prefab == null)
            return true;

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.gameObject == null)
                continue;

            string objectName = renderer.gameObject.name;
            if (IsVisibleFlowMarkerObjectName(objectName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVisibleFlowMarkerObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.StartsWith("Flow_") ||
               objectName.Contains("_Flow_") ||
               objectName.Contains("PathCenterLine") ||
               objectName.Contains("Weakpoint_Path_Line");
    }

    private static bool PrefabVisualNeedsRotationRepair(GameObject prefab, TileDefinition definition)
    {
        if (prefab == null)
            return true;

        Transform visual = prefab.transform.Find("Visual");

        if (visual == null)
            return true;

        Quaternion expectedRotation = Quaternion.Euler(GetGeneratedModelEulerCorrection(definition));
        return Quaternion.Angle(visual.localRotation, expectedRotation) > 0.1f;
    }

    private static bool PrefabUsesProceduralGeneratedTileVisual(GameObject prefab, TileDefinition definition)
    {
        if (prefab == null || definition == null)
            return false;

        if (definition.AssetName == "TD_HealTile")
        {
            return FindChildByNameContains(prefab.transform, "Heal_Base") != null &&
                   FindChildByNameContains(prefab.transform, "Heal_Surface") != null &&
                   FindChildByNameContains(prefab.transform, "Heal_Cross_Horizontal") != null &&
                   FindChildByNameContains(prefab.transform, "Heal_Cross_Vertical") != null;
        }

        if (definition.AssetName == "TD_WeakpointTile")
        {
            return FindChildByNameContains(prefab.transform, "TD_PathTile_BasePlate") != null &&
                   FindChildByNameContains(prefab.transform, "TD_PathTile_Surface") != null &&
                   FindChildByNameContains(prefab.transform, "Weakpoint_Crack_Main") != null &&
                   FindChildByNameContains(prefab.transform, "Weakpoint_Crack_Detail_A") != null &&
                   FindChildByNameContains(prefab.transform, "Weakpoint_Surface") == null &&
                   FindChildByNameContains(prefab.transform, "Weakpoint_Marker_Base") == null &&
                   FindChildByNameContains(prefab.transform, "Flow_North") != null &&
                   PrefabHasAllNamedRails(prefab);
        }

        return true;
    }

    private static bool PrefabVisualBottomNeedsRepair(GameObject prefab)
    {
        if (prefab == null)
            return true;

        Transform visual = prefab.transform.Find("Visual");

        if (visual == null)
            return true;

        return Vector3.Distance(visual.localPosition, GeneratedVisualLocalPosition) > 0.01f;
    }

    private static bool GeneratedModelIsNewerThanPrefab(TileDefinition definition)
    {
        if (definition == null)
            return false;

        if (!File.Exists(definition.ModelPath))
            return false;

        if (!File.Exists(definition.PrefabPath))
            return true;

        System.DateTime prefabWriteTime = File.GetLastWriteTimeUtc(definition.PrefabPath).AddSeconds(1);

        if (File.GetLastWriteTimeUtc(definition.ModelPath) > prefabWriteTime)
            return true;

        if (definition.AssetName == "TD_WeakpointTile")
        {
            string pathTileModelPath = GeneratedModelFolder + "/TD_PathTile.fbx";
            return File.Exists(pathTileModelPath) &&
                   File.GetLastWriteTimeUtc(pathTileModelPath) > prefabWriteTime;
        }

        return false;
    }

    private static bool PrefabFlowMarkerNeedsCenterRepair(GameObject prefab)
    {
        if (prefab == null)
            return true;

        Transform north = FindChildByNameContains(prefab.transform, "Flow_North");
        Transform east = FindChildByNameContains(prefab.transform, "Flow_East");

        if (north == null || east == null)
            return true;

        return Mathf.Abs(north.localPosition.x) > 0.03f ||
               Mathf.Abs(north.localPosition.z) > 0.03f ||
               Mathf.Abs(east.localPosition.x) > 0.03f ||
               Mathf.Abs(east.localPosition.z) > 0.03f;
    }

    private static bool PrefabHasAllNamedRails(GameObject prefab)
    {
        if (prefab == null)
            return false;

        return FindChildByNameContains(prefab.transform, "Rail_North") != null &&
               FindChildByNameContains(prefab.transform, "Rail_East") != null &&
               FindChildByNameContains(prefab.transform, "Rail_South") != null &&
               FindChildByNameContains(prefab.transform, "Rail_West") != null;
    }

    private static bool PrefabHasAnyRailOrCorner(GameObject prefab)
    {
        if (prefab == null)
            return true;

        return FindChildByNameContains(prefab.transform, "Rail_") != null ||
               FindChildByNameContains(prefab.transform, "CornerPost") != null ||
               FindChildByNameContains(prefab.transform, "Corner_") != null;
    }

    private static bool AllGeneratedTilePrefabsExist()
    {
        foreach (TileDefinition definition in TileDefinitions)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath) == null)
                return false;
        }

        return true;
    }

    private static bool PrefabSetNeedsRepair(GeneratedTilePrefabSet prefabSet)
    {
        if (prefabSet == null)
            return true;

        return !IsGeneratedPrefab(prefabSet.pathTilePrefab, "TD_PathTile") ||
               !IsGeneratedPrefab(prefabSet.startTilePrefab, "TD_StartTile") ||
               !IsGeneratedPrefab(prefabSet.baseTilePrefab, "TD_BaseTile") ||
               !IsGeneratedPrefab(prefabSet.buildTilePrefab, "TD_BuildTile") ||
               !IsGeneratedPrefab(prefabSet.trapTilePrefab, "TD_TrapTile") ||
               !IsGeneratedPrefab(prefabSet.slowTilePrefab, "TD_SlowTile") ||
               !IsGeneratedPrefab(prefabSet.knockTilePrefab, "TD_KnockTile") ||
               !IsGeneratedPrefab(prefabSet.comboTilePrefab, "TD_ComboTile") ||
               !IsGeneratedPrefab(prefabSet.goldTilePrefab, "TD_GoldTile") ||
               !IsGeneratedPrefab(prefabSet.rangeTilePrefab, "TD_RangeTile") ||
               !IsGeneratedPrefab(prefabSet.damageTilePrefab, "TD_DamageTile") ||
               !IsGeneratedPrefab(prefabSet.rateTilePrefab, "TD_RateTile") ||
               !IsGeneratedPrefab(prefabSet.xpTilePrefab, "TD_XpTile") ||
               !IsGeneratedPrefab(prefabSet.upgradeTilePrefab, "TD_UpgradeTile") ||
               !IsGeneratedPrefab(prefabSet.healTilePrefab, "TD_HealTile") ||
               !IsGeneratedPrefab(prefabSet.weakpointTilePrefab, "TD_WeakpointTile") ||
               !IsGeneratedPrefab(prefabSet.pathGhostTilePrefab, "TD_PathGhostTile") ||
               !IsGeneratedPrefab(prefabSet.blockedTilePrefab, "TD_BlockedTile");
    }

    private static bool TileManagerNeedsRepair(TileManager tileManager)
    {
        if (tileManager == null)
            return false;

        if (!tileManager.useGeneratedTilePrefabs || tileManager.generatedTilePrefabSet == null)
            return true;

        if (!tileManager.skipProceduralRailingsOnGeneratedTiles ||
            !tileManager.useGeneratedTileDirectRails ||
            !tileManager.invertGeneratedRailSideNames ||
            !tileManager.rotateGeneratedPathTileVisualsToFlow)
            return true;

        if (Vector3.Distance(tileManager.generatedPathTileVisualEulerBase, Vector3.zero) > 0.001f)
            return true;

        if (Mathf.Abs(tileManager.generatedTileGroundLocalY - VisualBottomLocalY) > 0.001f)
            return true;

        return !IsGeneratedPrefab(tileManager.pathTilePrefab, "TD_PathTile") ||
               !IsGeneratedPrefab(tileManager.startTilePrefab, "TD_StartTile") ||
               !IsGeneratedPrefab(tileManager.baseTilePrefab, "TD_BaseTile") ||
               !IsGeneratedPrefab(tileManager.buildTilePrefab, "TD_BuildTile") ||
               !IsGeneratedPrefab(tileManager.trapTilePrefab, "TD_TrapTile") ||
               !IsGeneratedPrefab(tileManager.slowTilePrefab, "TD_SlowTile") ||
               !IsGeneratedPrefab(tileManager.knockTilePrefab, "TD_KnockTile") ||
               !IsGeneratedPrefab(tileManager.comboTilePrefab, "TD_ComboTile") ||
               !IsGeneratedPrefab(tileManager.goldTilePrefab, "TD_GoldTile") ||
               !IsGeneratedPrefab(tileManager.rangeTilePrefab, "TD_RangeTile") ||
               !IsGeneratedPrefab(tileManager.damageTilePrefab, "TD_DamageTile") ||
               !IsGeneratedPrefab(tileManager.rateTilePrefab, "TD_RateTile") ||
               !IsGeneratedPrefab(tileManager.xpTilePrefab, "TD_XpTile") ||
               !IsGeneratedPrefab(tileManager.upgradeTilePrefab, "TD_UpgradeTile") ||
               !IsGeneratedPrefab(tileManager.healTilePrefab, "TD_HealTile") ||
               !IsGeneratedPrefab(tileManager.weakpointTilePrefab, "TD_WeakpointTile") ||
               !IsGeneratedPrefab(tileManager.blockedTilePrefab, "TD_BlockedTile");
    }

    private static bool PathBuildManagerNeedsRepair(PathBuildManager pathBuildManager)
    {
        if (pathBuildManager == null)
            return false;

        if (!pathBuildManager.useGeneratedTilePrefabs || pathBuildManager.generatedTilePrefabSet == null)
            return true;

        if (!IsGeneratedPrefab(pathBuildManager.pathGhostPrefab, "TD_PathGhostTile"))
            return true;

        return HasRemovedPathBuildOption(pathBuildManager.randomOptions) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.TrapTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.SlowTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.KnockTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.RangeTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.DamageTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.RateTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.XPTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.UpgradeTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.HealTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.WeakpointTile) ||
               !HasPathBuildOption(pathBuildManager.randomOptions, PathBuildOptionType.ComboTile);
    }

    private static bool IsGeneratedPrefab(GameObject prefab, string prefabName)
    {
        if (prefab == null)
            return false;

        return AssetDatabase.GetAssetPath(prefab) == GeneratedPrefabFolder + "/" + prefabName + ".prefab";
    }

    private static bool HasPathBuildOption(List<PathBuildOption> options, PathBuildOptionType optionType)
    {
        if (options == null)
            return false;

        foreach (PathBuildOption option in options)
        {
            if (option != null && option.optionType == optionType)
                return true;
        }

        return false;
    }

    private static bool HasRemovedPathBuildOption(List<PathBuildOption> options)
    {
        if (options == null)
            return false;

        foreach (PathBuildOption option in options)
        {
            if (option != null && IsRemovedPathBuildSelectionOption(option.optionType))
                return true;
        }

        return false;
    }

    private static bool HasLoadedScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.IsValid() && scene.isLoaded)
                return true;
        }

        return false;
    }

    private static Transform FindChildByNameContains(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
            return null;

        if (root.name.Contains(namePart))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByNameContains(root.GetChild(i), namePart);

            if (result != null)
                return result;
        }

        return null;
    }

    private class TileDefinition
    {
        public readonly string AssetName;
        public readonly Color FallbackColor;
        public readonly bool IsBuildTile;
        public readonly bool IsGhostTile;

        public TileDefinition(string assetName, Color fallbackColor, bool isBuildTile, bool isGhostTile)
        {
            AssetName = assetName;
            FallbackColor = fallbackColor;
            IsBuildTile = isBuildTile;
            IsGhostTile = isGhostTile;
        }

        public string ModelPath
        {
            get { return GeneratedModelFolder + "/" + AssetName + ".fbx"; }
        }

        public string PrefabPath
        {
            get { return GeneratedPrefabFolder + "/" + AssetName + ".prefab"; }
        }
    }
}
