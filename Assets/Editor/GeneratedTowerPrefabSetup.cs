using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GeneratedTowerPrefabSetup
{
    private const string GeneratedPrefabFolder = "Assets/Prefabs/Towers/Generated";
    private const string GeneratedModelFolder = "Assets/Models/Generated/Towers";
    private const string GeneratedMaterialFolder = "Assets/Materials/Generated/Towers";
    private const string DefaultProjectilePath = "Assets/Prefabs/Projectile.prefab";
    private const string AutoRepairSessionKey = "GeneratedTowerPrefabSetup.AutoRepairRanV6";

    private static readonly TowerDefinition[] TowerDefinitions =
    {
        new TowerDefinition("Basic", TowerRole.Basic, "Basic Tower", 50, "Guenstiger Allrounder fuer den Spielstart.", "Assets/Prefabs/Tower/Basic_Tower.prefab"),
        new TowerDefinition("Rapid", TowerRole.Rapid, "Rapid Tower", 75, "Schneller Tower gegen Runner und Cleanup.", "Assets/Prefabs/Tower/Rapid_Tower.prefab"),
        new TowerDefinition("Heavy", TowerRole.Heavy, "Heavy Tower", 100, "Langsamer Einzelschaden gegen Tanks und Bosse.", "Assets/Prefabs/Tower/Heavy_Tower.prefab"),
        new TowerDefinition("Fire", TowerRole.Fire, "Fire Tower", 80, "Burn-Tower gegen Gruppen und Standard-Gegner.", "Assets/Prefabs/Tower/Fire_Tower.prefab"),
        new TowerDefinition("Slow", TowerRole.Slow, "Slow Tower", 65, "Kontrolltower, der Gegner verlangsamt.", "Assets/Prefabs/Tower/Slow_Tower.prefab"),
        new TowerDefinition("Poison", TowerRole.Poison, "Poison Tower", 80, "DoT-Tower gegen Tanks, MiniBoss und Boss.", "Assets/Prefabs/Tower/Poison_Tower.prefab"),
        new TowerDefinition("Alchemist", TowerRole.Alchemist, "Alchemist Tower", 85, "Gift und Kontrolle in einem kompakten Support-Tower.", "Assets/Resources/TowerPrefabs/Alchemist_Tower.prefab"),
        new TowerDefinition("Lightning", TowerRole.Lightning, "Lightning Tower", 90, "Schneller Tower mit Kettenblitz und kurzer Verlangsamung.", "Assets/Resources/TowerPrefabs/Lightning_Tower.prefab"),
        new TowerDefinition("Mortar", TowerRole.Mortar, "Mortar Tower", 110, "Langsamer Tower mit hohem Einschlagsschaden.", "Assets/Resources/TowerPrefabs/Mortar_Tower.prefab"),
        new TowerDefinition("Sniper", TowerRole.Sniper, "Sniper Tower", 120, "Sehr hohe Reichweite und starker Einzelschuss.", "Assets/Resources/TowerPrefabs/Sniper_Tower.prefab"),
        new TowerDefinition("Spike", TowerRole.Spike, "Spike Tower", 75, "Kurze Reichweite mit schnellen Treffern und Bleed.", "Assets/Resources/TowerPrefabs/Spike_Tower.prefab")
    };

    [DidReloadScripts]
    private static void AutoRepairGeneratedTowerSetupAfterReload()
    {
        EditorApplication.delayCall += AutoRepairGeneratedTowerSetupIfNeeded;
    }

    private static void AutoRepairGeneratedTowerSetupIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (SessionState.GetBool(AutoRepairSessionKey, false))
            return;

        if (!NeedsGeneratedTowerSetupRepair())
            return;

        SessionState.SetBool(AutoRepairSessionKey, true);
        Debug.Log("GeneratedTowerPrefabSetup: Alte oder fehlende Generated Tower Prefabs erkannt. Auto-Setup wird einmalig ausgefuehrt.");
        CreateOrUpdateGeneratedTowerPrefabs();
        ApplyGeneratedTowerPrefabsToOpenScene(false);
    }

    [MenuItem("Tools/Tower Defense/Generated Towers/Create Or Update Generated Tower Prefabs")]
    public static void CreateOrUpdateGeneratedTowerPrefabs()
    {
        EnsureFolder(GeneratedPrefabFolder);
        EnsureFolder(GeneratedMaterialFolder);

        int createdOrUpdatedCount = 0;

        foreach (TowerDefinition definition in TowerDefinitions)
        {
            GameObject[] tierModels = LoadTierModels(definition);
            GameObject prefabRoot = CreateGeneratedTowerRoot(definition, tierModels);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, definition.GeneratedPrefabPath);

            Object.DestroyImmediate(prefabRoot);

            if (savedPrefab == null)
            {
                Debug.LogError("GeneratedTowerPrefabSetup: Prefab konnte nicht gespeichert werden: " + definition.GeneratedPrefabPath);
                continue;
            }

            createdOrUpdatedCount++;
            Debug.Log("GeneratedTowerPrefabSetup: Prefab zugewiesen/aktualisiert: " + definition.GeneratedPrefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("GeneratedTowerPrefabSetup: Fertig. " + createdOrUpdatedCount + " Generated Tower Prefabs erstellt oder aktualisiert.");
    }

    [MenuItem("Tools/Tower Defense/Generated Towers/Apply Generated Tower Prefabs To Open Scene")]
    public static void ApplyGeneratedTowerPrefabsToOpenScene()
    {
        ApplyGeneratedTowerPrefabsToOpenScene(true);
    }

    private static void ApplyGeneratedTowerPrefabsToOpenScene(bool createMissingPrefabsFirst)
    {
        BuildSelectionUI[] buildSelectionUIs = Object.FindObjectsOfType<BuildSelectionUI>(true);

        if (buildSelectionUIs == null || buildSelectionUIs.Length == 0)
        {
            Debug.LogWarning("GeneratedTowerPrefabSetup: Keine BuildSelectionUI in der aktuell geoeffneten Szene gefunden.");
            return;
        }

        if (createMissingPrefabsFirst && !AllGeneratedPrefabsExist())
        {
            Debug.Log("GeneratedTowerPrefabSetup: Generated Prefabs fehlen noch. Erstelle sie vor dem Anwenden auf die Szene.");
            CreateOrUpdateGeneratedTowerPrefabs();
        }

        foreach (BuildSelectionUI buildSelectionUI in buildSelectionUIs)
        {
            if (buildSelectionUI == null)
                continue;

            Undo.RecordObject(buildSelectionUI, "Apply Generated Tower Prefabs");

            foreach (TowerDefinition definition in TowerDefinitions)
            {
                GameObject generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.GeneratedPrefabPath);

                if (generatedPrefab == null)
                {
                    Debug.LogWarning("GeneratedTowerPrefabSetup: Generated Prefab fehlt noch: " + definition.GeneratedPrefabPath);
                    continue;
                }

                BuildOption option = GetOrCreateBuildOption(buildSelectionUI, definition.Role);
                ConfigureBuildOption(option, definition, generatedPrefab);
                SetBuildOption(buildSelectionUI, definition.Role, option);
                EnsureTowerSlot(buildSelectionUI, option);

                Debug.Log("GeneratedTowerPrefabSetup: " + definition.DisplayName + " -> " + definition.GeneratedPrefabPath);
            }

            EditorUtility.SetDirty(buildSelectionUI);

            Scene scene = buildSelectionUI.gameObject.scene;
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log("GeneratedTowerPrefabSetup: BuildSelectionUI in der offenen Szene wurde auf Generated Tower Prefabs aktualisiert.");
    }

    private static GameObject CreateGeneratedTowerRoot(TowerDefinition definition, GameObject[] tierModels)
    {
        GameObject root = new GameObject(definition.GeneratedObjectName);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject visualRootObject = new GameObject("VisualRoot");
        visualRootObject.transform.SetParent(root.transform, false);
        visualRootObject.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        GameObject aimPivotObject = new GameObject("AimPivot");
        aimPivotObject.transform.SetParent(visualRootObject.transform, false);

        GameObject firePointObject = new GameObject("FirePoint");
        firePointObject.transform.SetParent(aimPivotObject.transform, false);
        firePointObject.transform.localPosition = GetFirePointLocalPosition(definition.Role);

        Tower sourceTower = LoadSourceTower(definition);
        Tower tower = root.AddComponent<Tower>();

        if (sourceTower != null)
            EditorUtility.CopySerializedManagedFieldsOnly(sourceTower, tower);
        else
            ApplyTowerRoleDefaults(tower, definition.Role);

        tower.towerName = definition.DisplayName;
        tower.towerRole = definition.Role;
        tower.originalBuildCost = definition.Cost;
        tower.autoApplyTowerRoleStats = true;
        tower.autoCreateRangeIndicator = true;
        tower.autoCreateTowerVisualFeedback = true;
        tower.autoCreateVisualTierController = false;
        tower.projectilePrefab = ResolveProjectilePrefab(sourceTower);
        tower.firePoint = firePointObject.transform;

        CopyOrCreateCollider(root, definition);

        TowerRangeIndicator rangeIndicator = root.AddComponent<TowerRangeIndicator>();
        CopyManagedComponentIfPresent(sourceTower, rangeIndicator);
        rangeIndicator.tower = tower;

        TowerVisualFeedback visualFeedback = root.AddComponent<TowerVisualFeedback>();
        CopyManagedComponentIfPresent(sourceTower, visualFeedback);
        visualFeedback.tower = tower;

        TowerAimController aimController = root.AddComponent<TowerAimController>();
        aimController.tower = tower;
        aimController.aimPivot = aimPivotObject.transform;

        TowerVisualTierPrefabController visualTierPrefabController = root.AddComponent<TowerVisualTierPrefabController>();
        visualTierPrefabController.tower = tower;
        visualTierPrefabController.visualRoot = visualRootObject.transform;
        visualTierPrefabController.aimPivot = aimPivotObject.transform;
        visualTierPrefabController.useFixedVisualTierPrefabs = true;
        visualTierPrefabController.destroyPreviousVisual = true;
        visualTierPrefabController.maxTierIndex = 3;
        visualTierPrefabController.applyGeneratedModelRotationFix = true;
        visualTierPrefabController.generatedModelEulerCorrection = GetGeneratedModelEulerCorrection(definition.Role);
        visualTierPrefabController.applyGeneratedVisualGroundOffset = true;
        visualTierPrefabController.generatedVisualRootOffset = new Vector3(0f, -0.5f, 0f);
        visualTierPrefabController.autoAlignGeneratedVisualsToGround = true;
        visualTierPrefabController.localGroundY = -0.5f;
        visualTierPrefabController.baseHideDepth = 0.01f;
        visualTierPrefabController.visiblePartGroundPadding = 0.005f;
        visualTierPrefabController.visualTierPrefabs = tierModels;
        visualTierPrefabController.ApplyTier(0);
        ApplyTowerGeneratedMaterials(root, definition);

        TowerVisualTierController proceduralTierController = root.GetComponent<TowerVisualTierController>();
        if (proceduralTierController != null)
            proceduralTierController.enabled = false;

        return root;
    }

    private static GameObject[] LoadTierModels(TowerDefinition definition)
    {
        GameObject[] models = new GameObject[4];

        for (int tier = 0; tier < models.Length; tier++)
        {
            string modelPath = definition.GetModelPath(tier);
            EnsureTowerModelMaterialRemaps(definition, tier, modelPath);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            models[tier] = model;

            if (model == null)
            {
                Debug.LogWarning("GeneratedTowerPrefabSetup: FBX fehlt: " + modelPath);
                continue;
            }

            ValidateModelAsset(model, modelPath);
        }

        return models;
    }

    private static void ValidateModelAsset(GameObject model, string modelPath)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("GeneratedTowerPrefabSetup: FBX enthaelt keine sichtbaren Renderer: " + modelPath);
            return;
        }

        bool hasMissingMaterial = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
            {
                hasMissingMaterial = true;
                continue;
            }

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                    hasMissingMaterial = true;
            }
        }

        if (hasMissingMaterial)
            Debug.LogWarning("GeneratedTowerPrefabSetup: FBX hat Renderer ohne Materialzuweisung: " + modelPath);
    }

    private static void EnsureTowerModelMaterialRemaps(TowerDefinition definition, int tier, string modelPath)
    {
        if (definition == null || !UsesGeneratedColorMaterials(definition.Role))
            return;

        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
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
        string[] sourceMaterialNames = GetExpectedTowerMaterialNames(definition, tier);

        foreach (string sourceMaterialName in sourceMaterialNames)
        {
            if (string.IsNullOrEmpty(sourceMaterialName))
                continue;

            Material targetMaterial = GetOrCreateTowerMaterial(definition, sourceMaterialName);
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
            Debug.Log("GeneratedTowerPrefabSetup: Material-Remaps aktualisiert: " + modelPath);
        }
    }

    private static void ApplyTowerGeneratedMaterials(GameObject root, TowerDefinition definition)
    {
        if (root == null || definition == null || !UsesGeneratedColorMaterials(definition.Role))
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
                continue;

            bool changed = false;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material sourceMaterial = sharedMaterials[i];
                if (sourceMaterial == null || string.IsNullOrEmpty(sourceMaterial.name))
                    continue;

                Material replacement = GetOrCreateTowerMaterial(definition, sourceMaterial.name);
                if (replacement == null || replacement == sourceMaterial)
                    continue;

                sharedMaterials[i] = replacement;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = sharedMaterials;
        }
    }

    private static Material GetOrCreateTowerMaterial(TowerDefinition definition, string sourceMaterialName)
    {
        string materialFolder = GeneratedMaterialFolder + "/" + definition.GeneratedObjectName;
        EnsureFolder(materialFolder);

        string materialPath = GetTowerMaterialPath(definition, sourceMaterialName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        bool created = material == null;

        if (material == null)
            material = new Material(GetGeneratedMaterialShader());

        material.name = GetTowerMaterialAssetName(definition.Role, sourceMaterialName);
        string marker = sourceMaterialName.ToLowerInvariant();
        Color color = ResolveTowerMaterialColor(definition.Role, marker);
        ApplyGeneratedMaterialColor(material, color, IsTowerEmissionMaterial(marker));

        if (created)
            AssetDatabase.CreateAsset(material, materialPath);
        else
            EditorUtility.SetDirty(material);

        return material;
    }

    private static string GetTowerMaterialPath(TowerDefinition definition, string sourceMaterialName)
    {
        return GeneratedMaterialFolder + "/" + definition.GeneratedObjectName + "/" + GetTowerMaterialAssetName(definition.Role, sourceMaterialName) + ".mat";
    }

    private static string GetTowerMaterialAssetName(TowerRole role, string sourceMaterialName)
    {
        string marker = string.IsNullOrEmpty(sourceMaterialName) ? "" : sourceMaterialName.ToLowerInvariant();

        switch (role)
        {
            case TowerRole.Beam:
                if (marker.Contains("energy") || marker.Contains("cyan") || marker.Contains("lens"))
                    return "Beam_Energy";
                if (marker.Contains("white") || marker.Contains("glow"))
                    return "Beam_Glow";
                if (marker.Contains("gold") || marker.Contains("trim") || marker.Contains("ring"))
                    return "Beam_Gold";
                if (marker.Contains("steel") || marker.Contains("barrel"))
                    return "Beam_Steel";
                return "Beam_Dark";

            case TowerRole.Support:
                if (marker.Contains("green") || marker.Contains("energy") || marker.Contains("beacon") || marker.Contains("plus"))
                    return "Support_Energy";
                if (marker.Contains("white") || marker.Contains("glow"))
                    return "Support_Glow";
                if (marker.Contains("gold") || marker.Contains("trim"))
                    return "Support_Gold";
                if (marker.Contains("steel") || marker.Contains("antenna"))
                    return "Support_Steel";
                return "Support_Dark";

            case TowerRole.Frost:
                if (marker.Contains("ice") || marker.Contains("blue") || marker.Contains("crystal") || marker.Contains("core"))
                    return "Frost_Ice";
                if (marker.Contains("white") || marker.Contains("glow"))
                    return "Frost_Glow";
                if (marker.Contains("steel"))
                    return "Frost_Steel";
                return "Frost_Dark";

            default:
                return SanitizeAssetFileName(sourceMaterialName);
        }
    }

    private static bool UsesGeneratedColorMaterials(TowerRole role)
    {
        return role == TowerRole.Beam ||
               role == TowerRole.Support ||
               role == TowerRole.Frost;
    }

    private static string[] GetExpectedTowerMaterialNames(TowerDefinition definition, int tier)
    {
        string prefix = definition.RoleName.ToLowerInvariant() + "_t" + tier + "_";

        switch (definition.Role)
        {
            case TowerRole.Beam:
                return new[]
                {
                    prefix + "cyan_energy",
                    prefix + "dark_metal",
                    prefix + "steel",
                    prefix + "gold_trim",
                    prefix + "white_glow"
                };
            case TowerRole.Support:
                return new[]
                {
                    prefix + "gold_trim",
                    prefix + "dark_metal",
                    prefix + "green_energy",
                    prefix + "steel",
                    prefix + "white_glow"
                };
            case TowerRole.Frost:
                return new[]
                {
                    prefix + "dark_metal",
                    prefix + "white_glow",
                    prefix + "steel",
                    prefix + "ice_blue"
                };
            default:
                return new string[0];
        }
    }

    private static Color ResolveTowerMaterialColor(TowerRole role, string marker)
    {
        switch (role)
        {
            case TowerRole.Beam:
                if (marker.Contains("energy") || marker.Contains("cyan") || marker.Contains("lens") || marker.Contains("glow"))
                    return new Color32(70, 235, 255, 255);
                if (marker.Contains("white"))
                    return new Color32(230, 255, 255, 255);
                if (marker.Contains("gold") || marker.Contains("trim") || marker.Contains("ring"))
                    return new Color32(245, 185, 60, 255);
                if (marker.Contains("steel") || marker.Contains("barrel"))
                    return new Color32(105, 135, 165, 255);
                if (marker.Contains("dark") || marker.Contains("metal") || marker.Contains("base"))
                    return new Color32(36, 48, 68, 255);
                return new Color32(90, 220, 245, 255);

            case TowerRole.Support:
                if (marker.Contains("green") || marker.Contains("energy") || marker.Contains("beacon") || marker.Contains("plus"))
                    return new Color32(95, 245, 135, 255);
                if (marker.Contains("white") || marker.Contains("glow"))
                    return new Color32(230, 255, 238, 255);
                if (marker.Contains("gold") || marker.Contains("trim"))
                    return new Color32(235, 190, 70, 255);
                if (marker.Contains("steel") || marker.Contains("antenna"))
                    return new Color32(135, 170, 155, 255);
                if (marker.Contains("dark") || marker.Contains("metal") || marker.Contains("base") || marker.Contains("pedestal"))
                    return new Color32(34, 58, 48, 255);
                return new Color32(90, 220, 120, 255);

            case TowerRole.Frost:
                if (marker.Contains("ice") || marker.Contains("blue") || marker.Contains("crystal") || marker.Contains("core"))
                    return new Color32(120, 225, 255, 255);
                if (marker.Contains("white") || marker.Contains("glow"))
                    return new Color32(235, 255, 255, 255);
                if (marker.Contains("steel"))
                    return new Color32(110, 150, 175, 255);
                if (marker.Contains("dark") || marker.Contains("metal") || marker.Contains("base"))
                    return new Color32(34, 52, 70, 255);
                return new Color32(130, 215, 255, 255);

            default:
                return Color.white;
        }
    }

    private static bool IsTowerEmissionMaterial(string marker)
    {
        return marker.Contains("energy") ||
               marker.Contains("glow") ||
               marker.Contains("cyan") ||
               marker.Contains("green") ||
               marker.Contains("ice") ||
               marker.Contains("white");
    }

    private static Tower LoadSourceTower(TowerDefinition definition)
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.SourcePrefabPath);

        if (sourcePrefab == null)
        {
            Debug.LogWarning("GeneratedTowerPrefabSetup: Kein altes/source Prefab gefunden fuer " + definition.DisplayName + ". Nutze TowerRole-Defaults.");
            return null;
        }

        Tower sourceTower = sourcePrefab.GetComponent<Tower>();

        if (sourceTower == null)
            Debug.LogWarning("GeneratedTowerPrefabSetup: Source Prefab hat keine Tower-Komponente: " + definition.SourcePrefabPath);

        return sourceTower;
    }

    private static void CopyOrCreateCollider(GameObject root, TowerDefinition definition)
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.SourcePrefabPath);
        Collider sourceCollider = sourcePrefab != null ? sourcePrefab.GetComponent<Collider>() : null;

        if (sourceCollider is CapsuleCollider)
        {
            CapsuleCollider sourceCapsule = (CapsuleCollider)sourceCollider;
            CapsuleCollider targetCollider = root.AddComponent<CapsuleCollider>();
            targetCollider.enabled = sourceCapsule.enabled;
            targetCollider.isTrigger = sourceCapsule.isTrigger;
            targetCollider.sharedMaterial = sourceCapsule.sharedMaterial;
            targetCollider.center = sourceCapsule.center;
            targetCollider.radius = sourceCapsule.radius;
            targetCollider.height = sourceCapsule.height;
            targetCollider.direction = sourceCapsule.direction;
            return;
        }

        if (sourceCollider is BoxCollider)
        {
            BoxCollider sourceBox = (BoxCollider)sourceCollider;
            BoxCollider targetCollider = root.AddComponent<BoxCollider>();
            targetCollider.enabled = sourceBox.enabled;
            targetCollider.isTrigger = sourceBox.isTrigger;
            targetCollider.sharedMaterial = sourceBox.sharedMaterial;
            targetCollider.center = sourceBox.center;
            targetCollider.size = sourceBox.size;
            return;
        }

        CapsuleCollider fallbackCollider = root.AddComponent<CapsuleCollider>();
        fallbackCollider.radius = 0.48f;
        fallbackCollider.height = 1.7f;
        fallbackCollider.center = new Vector3(0f, 0.45f, 0f);
        fallbackCollider.direction = 1;
    }

    private static void CopyManagedComponentIfPresent<T>(Tower sourceTower, T targetComponent) where T : Component
    {
        if (sourceTower == null || targetComponent == null)
            return;

        T sourceComponent = sourceTower.GetComponent<T>();

        if (sourceComponent != null)
            EditorUtility.CopySerializedManagedFieldsOnly(sourceComponent, targetComponent);
    }

    private static GameObject ResolveProjectilePrefab(Tower sourceTower)
    {
        if (sourceTower != null && sourceTower.projectilePrefab != null)
            return sourceTower.projectilePrefab;

        GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultProjectilePath);

        if (projectilePrefab == null)
            Debug.LogWarning("GeneratedTowerPrefabSetup: Default Projectile Prefab fehlt: " + DefaultProjectilePath);

        return projectilePrefab;
    }

    private static void ApplyTowerRoleDefaults(Tower tower, TowerRole role)
    {
        if (tower == null)
            return;

        MethodInfo applyStats = typeof(Tower).GetMethod("ApplyTowerRoleStats", BindingFlags.Instance | BindingFlags.NonPublic);

        if (applyStats != null)
            applyStats.Invoke(tower, new object[] { role });
        else
            tower.towerRole = role;
    }

    private static Vector3 GetFirePointLocalPosition(TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Mortar:
                return new Vector3(0f, 1.15f, 0.45f);
            case TowerRole.Sniper:
                return new Vector3(0f, 0.95f, 1.05f);
            case TowerRole.Heavy:
                return new Vector3(0f, 0.9f, 0.9f);
            case TowerRole.Rapid:
                return new Vector3(0f, 0.82f, 0.78f);
            case TowerRole.Beam:
                return new Vector3(0f, 0.92f, 0.9f);
            case TowerRole.Support:
                return new Vector3(0f, 0.76f, 0.66f);
            case TowerRole.Frost:
                return new Vector3(0f, 0.84f, 0.7f);
            case TowerRole.Spike:
                return new Vector3(0f, 0.55f, 0.5f);
            case TowerRole.Fire:
            case TowerRole.Poison:
            case TowerRole.Alchemist:
            case TowerRole.Lightning:
            case TowerRole.Slow:
                return new Vector3(0f, 0.86f, 0.68f);
            default:
                return new Vector3(0f, 0.86f, 0.78f);
        }
    }

    private static Vector3 GetGeneratedModelEulerCorrection(TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Beam:
            case TowerRole.Support:
            case TowerRole.Frost:
                return Vector3.zero;
            default:
                return new Vector3(-90f, 0f, 0f);
        }
    }

    private static BuildOption GetOrCreateBuildOption(BuildSelectionUI buildSelectionUI, TowerRole role)
    {
        BuildOption option = GetBuildOption(buildSelectionUI, role);
        return option ?? new BuildOption();
    }

    private static BuildOption GetBuildOption(BuildSelectionUI buildSelectionUI, TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Basic:
                return buildSelectionUI.basicTower;
            case TowerRole.Rapid:
                return buildSelectionUI.rapidTower;
            case TowerRole.Heavy:
                return buildSelectionUI.heavyTower;
            case TowerRole.Fire:
                return buildSelectionUI.fireTower;
            case TowerRole.Slow:
                return buildSelectionUI.slowTower;
            case TowerRole.Poison:
                return buildSelectionUI.poisonTower;
            case TowerRole.Alchemist:
                return buildSelectionUI.alchemistTower;
            case TowerRole.Lightning:
                return buildSelectionUI.lightningTower;
            case TowerRole.Mortar:
                return buildSelectionUI.mortarTower;
            case TowerRole.Sniper:
                return buildSelectionUI.sniperTower;
            case TowerRole.Spike:
                return buildSelectionUI.spikeTower;
            case TowerRole.Beam:
                return buildSelectionUI.beamTower;
            case TowerRole.Support:
                return buildSelectionUI.supportTower;
            case TowerRole.Frost:
                return buildSelectionUI.frostTower;
            default:
                return null;
        }
    }

    private static void SetBuildOption(BuildSelectionUI buildSelectionUI, TowerRole role, BuildOption option)
    {
        switch (role)
        {
            case TowerRole.Basic:
                buildSelectionUI.basicTower = option;
                break;
            case TowerRole.Rapid:
                buildSelectionUI.rapidTower = option;
                break;
            case TowerRole.Heavy:
                buildSelectionUI.heavyTower = option;
                break;
            case TowerRole.Fire:
                buildSelectionUI.fireTower = option;
                break;
            case TowerRole.Slow:
                buildSelectionUI.slowTower = option;
                break;
            case TowerRole.Poison:
                buildSelectionUI.poisonTower = option;
                break;
            case TowerRole.Alchemist:
                buildSelectionUI.alchemistTower = option;
                break;
            case TowerRole.Lightning:
                buildSelectionUI.lightningTower = option;
                break;
            case TowerRole.Mortar:
                buildSelectionUI.mortarTower = option;
                break;
            case TowerRole.Sniper:
                buildSelectionUI.sniperTower = option;
                break;
            case TowerRole.Spike:
                buildSelectionUI.spikeTower = option;
                break;
            case TowerRole.Beam:
                buildSelectionUI.beamTower = option;
                break;
            case TowerRole.Support:
                buildSelectionUI.supportTower = option;
                break;
            case TowerRole.Frost:
                buildSelectionUI.frostTower = option;
                break;
        }
    }

    private static void ConfigureBuildOption(BuildOption option, TowerDefinition definition, GameObject prefab)
    {
        if (option == null)
            return;

        option.prefab = prefab;
        option.placementType = PlacementType.BuildTile;

        if (string.IsNullOrEmpty(option.displayName) || IsKnownTowerDisplayName(option.displayName))
            option.displayName = definition.DisplayName;

        if (option.cost <= 0 || IsKnownDefaultCost(option.cost))
            option.cost = definition.Cost;

        if (string.IsNullOrEmpty(option.description) || IsKnownDefaultDescription(option.description))
            option.description = definition.Description;
    }

    private static void EnsureTowerSlot(BuildSelectionUI buildSelectionUI, BuildOption option)
    {
        if (buildSelectionUI.towerSlots == null)
            buildSelectionUI.towerSlots = new List<TowerSelectionSlot>();

        foreach (TowerSelectionSlot slot in buildSelectionUI.towerSlots)
        {
            if (slot == null || slot.option == null)
                continue;

            if (slot.option == option || NormalizeTowerName(slot.option.displayName) == NormalizeTowerName(option.displayName))
            {
                slot.option = option;
                return;
            }
        }

        buildSelectionUI.towerSlots.Add(new TowerSelectionSlot
        {
            option = option,
            button = null,
            iconImage = null
        });
    }

    private static bool IsKnownTowerDisplayName(string displayName)
    {
        string normalizedName = NormalizeTowerName(displayName);

        foreach (TowerDefinition definition in TowerDefinitions)
        {
            if (NormalizeTowerName(definition.DisplayName) == normalizedName)
                return true;
        }

        return false;
    }

    private static bool IsKnownDefaultCost(int cost)
    {
        int[] knownCosts = { 50, 65, 75, 80, 85, 90, 95, 100, 105, 110, 115, 120, 125, 130 };

        for (int i = 0; i < knownCosts.Length; i++)
        {
            if (knownCosts[i] == cost)
                return true;
        }

        return false;
    }

    private static bool IsKnownDefaultDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
            return true;

        string lowerDescription = description.ToLowerInvariant();

        return lowerDescription.Contains("tower") ||
               lowerDescription.Contains("runner") ||
               lowerDescription.Contains("boss") ||
               lowerDescription.Contains("bleed") ||
               lowerDescription.Contains("kettenblitz") ||
               lowerDescription.Contains("einschlag") ||
               lowerDescription.Contains("gift") ||
               lowerDescription.Contains("burn") ||
               lowerDescription.Contains("slow") ||
               lowerDescription.Contains("laser") ||
               lowerDescription.Contains("support") ||
               lowerDescription.Contains("frost") ||
               lowerDescription.Contains("aoe") ||
               lowerDescription.Contains("kontroll");
    }

    private static string NormalizeTowerName(string displayName)
    {
        return string.IsNullOrEmpty(displayName) ? "" : displayName.Replace(" Tower", "").Trim().ToLowerInvariant();
    }

    private static bool NeedsGeneratedTowerSetupRepair()
    {
        if (!AllGeneratedPrefabsExist())
            return true;

        if (AnyGeneratedPrefabNeedsOrientationRepair())
            return true;

        if (AnyGeneratedPrefabNeedsGroundOffsetRepair())
            return true;

        if (AnyGeneratedTowerModelNeedsMaterialRepair())
            return true;

        BuildSelectionUI[] buildSelectionUIs = Object.FindObjectsOfType<BuildSelectionUI>(true);

        if (buildSelectionUIs == null || buildSelectionUIs.Length == 0)
            return false;

        foreach (BuildSelectionUI buildSelectionUI in buildSelectionUIs)
        {
            if (buildSelectionUI == null)
                continue;

            if (BuildSelectionUIUsesOldOrMissingTowerPrefabs(buildSelectionUI))
                return true;
        }

        return false;
    }

    private static bool AnyGeneratedTowerModelNeedsMaterialRepair()
    {
        foreach (TowerDefinition definition in TowerDefinitions)
        {
            if (!UsesGeneratedColorMaterials(definition.Role))
                continue;

            for (int tier = 0; tier < 4; tier++)
            {
                if (TowerModelMaterialRemapsNeedRepair(definition, tier, definition.GetModelPath(tier)))
                    return true;
            }
        }

        return false;
    }

    private static bool TowerModelMaterialRemapsNeedRepair(TowerDefinition definition, int tier, string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
            return true;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
            importer.materialLocation != ModelImporterMaterialLocation.External)
        {
            return true;
        }

        var externalObjectMap = importer.GetExternalObjectMap();
        string[] sourceMaterialNames = GetExpectedTowerMaterialNames(definition, tier);

        foreach (string sourceMaterialName in sourceMaterialNames)
        {
            if (string.IsNullOrEmpty(sourceMaterialName))
                continue;

            string expectedPath = GetTowerMaterialPath(definition, sourceMaterialName);
            Material expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(expectedPath);
            if (expectedMaterial == null)
                return true;

            AssetImporter.SourceAssetIdentifier sourceIdentifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterialName);

            if (!externalObjectMap.TryGetValue(sourceIdentifier, out Object mappedObject) || mappedObject != expectedMaterial)
                return true;
        }

        return false;
    }

    private static bool AllGeneratedPrefabsExist()
    {
        foreach (TowerDefinition definition in TowerDefinitions)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.GeneratedPrefabPath) == null)
                return false;
        }

        return true;
    }

    private static bool AnyGeneratedPrefabNeedsOrientationRepair()
    {
        foreach (TowerDefinition definition in TowerDefinitions)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.GeneratedPrefabPath);

            if (prefab == null)
                return true;

            TowerVisualTierPrefabController prefabController = prefab.GetComponent<TowerVisualTierPrefabController>();

            if (prefabController == null || !prefabController.applyGeneratedModelRotationFix)
                return true;

            Vector3 expectedEulerCorrection = GetGeneratedModelEulerCorrection(definition.Role);

            if (Vector3.Distance(prefabController.generatedModelEulerCorrection, expectedEulerCorrection) > 0.001f)
                return true;

            Transform visualRoot = prefab.transform.Find("VisualRoot");
            Transform aimPivot = visualRoot != null ? visualRoot.Find("AimPivot") : null;

            if (visualRoot == null || aimPivot == null)
                return true;

            Transform baseModelRoot = visualRoot.Find("BaseModelRoot");
            Transform aimModelRoot = aimPivot.Find("AimModelRoot");

            if (baseModelRoot == null || aimModelRoot == null)
                return true;

            Quaternion expectedModelRootRotation = Quaternion.Euler(expectedEulerCorrection);

            if (Quaternion.Angle(baseModelRoot.localRotation, expectedModelRootRotation) > 0.1f ||
                Quaternion.Angle(aimModelRoot.localRotation, expectedModelRootRotation) > 0.1f)
                return true;
        }

        return false;
    }

    private static bool AnyGeneratedPrefabNeedsGroundOffsetRepair()
    {
        foreach (TowerDefinition definition in TowerDefinitions)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.GeneratedPrefabPath);

            if (prefab == null)
                return true;

            TowerVisualTierPrefabController prefabController = prefab.GetComponent<TowerVisualTierPrefabController>();

            if (prefabController == null || !prefabController.applyGeneratedVisualGroundOffset || !prefabController.autoAlignGeneratedVisualsToGround)
                return true;

            if (Vector3.Distance(prefabController.generatedVisualRootOffset, new Vector3(0f, -0.5f, 0f)) > 0.001f)
                return true;

            if (Mathf.Abs(prefabController.localGroundY + 0.5f) > 0.001f)
                return true;

            if (Mathf.Abs(prefabController.baseHideDepth - 0.01f) > 0.001f)
                return true;

            Transform visualRoot = prefab.transform.Find("VisualRoot");

            if (visualRoot == null)
                return true;
        }

        return false;
    }

    private static bool BuildSelectionUIUsesOldOrMissingTowerPrefabs(BuildSelectionUI buildSelectionUI)
    {
        foreach (TowerDefinition definition in TowerDefinitions)
        {
            BuildOption option = FindBuildOptionForRole(buildSelectionUI, definition.Role);

            if (option == null)
                return true;

            if (option.prefab == null)
                return true;

            string prefabPath = AssetDatabase.GetAssetPath(option.prefab).Replace("\\", "/");

            if (prefabPath != definition.GeneratedPrefabPath)
                return true;
        }

        return false;
    }

    private static BuildOption FindBuildOptionForRole(BuildSelectionUI buildSelectionUI, TowerRole role)
    {
        BuildOption directOption = GetBuildOption(buildSelectionUI, role);

        if (directOption != null && directOption.prefab != null)
            return directOption;

        string roleName = NormalizeTowerName(GetDisplayNameForRole(role));

        if (buildSelectionUI.towerSlots == null)
            return directOption;

        foreach (TowerSelectionSlot slot in buildSelectionUI.towerSlots)
        {
            if (slot == null || slot.option == null)
                continue;

            string optionName = NormalizeTowerName(slot.option.displayName);

            if (optionName == roleName)
                return slot.option;
        }

        return directOption;
    }

    private static string GetDisplayNameForRole(TowerRole role)
    {
        foreach (TowerDefinition definition in TowerDefinitions)
        {
            if (definition.Role == role)
                return definition.DisplayName;
        }

        return role + " Tower";
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

    private static void ApplyGeneratedMaterialColor(Material material, Color color, bool emission)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.15f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.45f);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.45f);

        Color emissionColor = emission ? color * 1.35f : Color.black;

        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emissionColor);

        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    private static string SanitizeAssetFileName(string fileName)
    {
        string safeName = string.IsNullOrEmpty(fileName) ? "Material" : fileName.Trim();

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(invalidChar, '_');

        return string.IsNullOrEmpty(safeName) ? "Material" : safeName;
    }

    private static void EnsureFolder(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            return;

        parent = parent.Replace("\\", "/");
        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private sealed class TowerDefinition
    {
        public readonly string RoleName;
        public readonly TowerRole Role;
        public readonly string DisplayName;
        public readonly int Cost;
        public readonly string Description;
        public readonly string SourcePrefabPath;

        public TowerDefinition(string roleName, TowerRole role, string displayName, int cost, string description, string sourcePrefabPath)
        {
            RoleName = roleName;
            Role = role;
            DisplayName = displayName;
            Cost = cost;
            Description = description;
            SourcePrefabPath = sourcePrefabPath;
        }

        public string GeneratedObjectName
        {
            get { return "TD_" + RoleName + "Tower"; }
        }

        public string GeneratedPrefabPath
        {
            get { return GeneratedPrefabFolder + "/" + GeneratedObjectName + ".prefab"; }
        }

        public string GetModelPath(int tier)
        {
            return GeneratedModelFolder + "/" + GeneratedObjectName + "_T" + tier + ".fbx";
        }
    }
}
