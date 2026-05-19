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
    private const string AutoRepairSessionKey = "GeneratedTowerPrefabSetup.AutoRepairRanV4";

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
        visualTierPrefabController.generatedModelEulerCorrection = new Vector3(-90f, 0f, 0f);
        visualTierPrefabController.applyGeneratedVisualGroundOffset = true;
        visualTierPrefabController.generatedVisualRootOffset = new Vector3(0f, -0.5f, 0f);
        visualTierPrefabController.autoAlignGeneratedVisualsToGround = true;
        visualTierPrefabController.localGroundY = -0.5f;
        visualTierPrefabController.baseHideDepth = 0.01f;
        visualTierPrefabController.visiblePartGroundPadding = 0.005f;
        visualTierPrefabController.visualTierPrefabs = tierModels;
        visualTierPrefabController.ApplyTier(0);

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
        int[] knownCosts = { 50, 65, 75, 80, 85, 90, 95, 100, 110, 115, 120, 125 };

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

            Transform visualRoot = prefab.transform.Find("VisualRoot");
            Transform aimPivot = visualRoot != null ? visualRoot.Find("AimPivot") : null;

            if (visualRoot == null || aimPivot == null)
                return true;

            if (visualRoot.Find("BaseModelRoot") == null || aimPivot.Find("AimModelRoot") == null)
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
