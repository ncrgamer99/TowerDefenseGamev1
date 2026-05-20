using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GeneratedEnemyPrefabSetup
{
    private const string GeneratedPrefabFolder = "Assets/Prefabs/Enemys/Generated";
    private const string GeneratedModelFolder = "Assets/Models/Generated/Enemies";
    private const string GeneratedMaterialFolder = "Assets/Materials/Generated/Enemies";
    private const string AutoRepairSessionKey = "GeneratedEnemyPrefabSetup.AutoRepairRanV1";
    private static readonly Vector3 GeneratedModelEulerCorrection = Vector3.zero;
    private static readonly Vector3[] UprightRotationCandidates =
    {
        Vector3.zero,
        new Vector3(-90f, 0f, 0f),
        new Vector3(90f, 0f, 0f),
        new Vector3(0f, 0f, -90f),
        new Vector3(0f, 0f, 90f)
    };
    private const float VisualBottomLocalY = 0f;

    private static readonly EnemyDefinition[] EnemyDefinitions =
    {
        new EnemyDefinition("TD_Enemy_Standard", EnemyRole.Standard, EnemyVariantType.Normal, new Color32(220, 220, 220, 255)),
        new EnemyDefinition("TD_Enemy_Runner", EnemyRole.Runner, EnemyVariantType.Normal, new Color32(255, 210, 60, 255)),
        new EnemyDefinition("TD_Enemy_Tank", EnemyRole.Tank, EnemyVariantType.Normal, new Color32(70, 160, 80, 255)),
        new EnemyDefinition("TD_Enemy_Knight", EnemyRole.Knight, EnemyVariantType.Normal, new Color32(145, 145, 155, 255)),
        new EnemyDefinition("TD_Enemy_Mage", EnemyRole.Mage, EnemyVariantType.Normal, new Color32(80, 190, 255, 255)),
        new EnemyDefinition("TD_Enemy_Learner", EnemyRole.Learner, EnemyVariantType.Normal, new Color32(190, 90, 255, 255)),
        new EnemyDefinition("TD_Enemy_AllRounder", EnemyRole.AllRounder, EnemyVariantType.Normal, new Color32(255, 145, 60, 255)),
        new EnemyDefinition("TD_Enemy_MiniBoss", EnemyRole.MiniBoss, EnemyVariantType.Normal, new Color32(255, 85, 55, 255)),
        new EnemyDefinition("TD_Enemy_Boss", EnemyRole.Boss, EnemyVariantType.Normal, new Color32(120, 25, 25, 255)),
        new EnemyDefinition("TD_Enemy_Elite", EnemyRole.Elite, EnemyVariantType.Normal, new Color32(255, 235, 105, 255)),
        new EnemyDefinition("TD_Enemy_Standard_Chaos", EnemyRole.Standard, EnemyVariantType.Chaos, new Color32(165, 70, 255, 255)),
        new EnemyDefinition("TD_Enemy_Runner_Chaos", EnemyRole.Runner, EnemyVariantType.Chaos, new Color32(185, 80, 255, 255)),
        new EnemyDefinition("TD_Enemy_Tank_Chaos", EnemyRole.Tank, EnemyVariantType.Chaos, new Color32(145, 70, 225, 255)),
        new EnemyDefinition("TD_Enemy_Knight_Chaos", EnemyRole.Knight, EnemyVariantType.Chaos, new Color32(180, 90, 245, 255)),
        new EnemyDefinition("TD_Enemy_Mage_Chaos", EnemyRole.Mage, EnemyVariantType.Chaos, new Color32(120, 120, 255, 255)),
        new EnemyDefinition("TD_Enemy_Learner_Chaos", EnemyRole.Learner, EnemyVariantType.Chaos, new Color32(190, 90, 255, 255)),
        new EnemyDefinition("TD_Enemy_AllRounder_Chaos", EnemyRole.AllRounder, EnemyVariantType.Chaos, new Color32(200, 85, 255, 255)),
        new EnemyDefinition("TD_Enemy_MiniBoss_Chaos", EnemyRole.MiniBoss, EnemyVariantType.Chaos, new Color32(170, 55, 255, 255)),
        new EnemyDefinition("TD_Enemy_Boss_Chaos", EnemyRole.Boss, EnemyVariantType.Chaos, new Color32(130, 35, 200, 255))
    };

    static GeneratedEnemyPrefabSetup()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [DidReloadScripts]
    private static void AutoRepairGeneratedEnemySetupAfterReload()
    {
        EditorApplication.delayCall += AutoRepairGeneratedEnemySetupIfNeeded;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode)
            EditorApplication.delayCall += AutoRepairGeneratedEnemySetupIfNeeded;
    }

    private static void AutoRepairGeneratedEnemySetupIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (SessionState.GetBool(AutoRepairSessionKey, false))
            return;

        if (!NeedsGeneratedEnemySetupRepair())
            return;

        SessionState.SetBool(AutoRepairSessionKey, true);
        Debug.Log("GeneratedEnemyPrefabSetup: Alte oder fehlende Generated Enemy Prefabs erkannt. Auto-Setup wird einmalig ausgefuehrt.");
        CreateOrUpdateGeneratedEnemyPrefabs();
        ApplyGeneratedEnemyPrefabsToOpenScene(false);
    }

    [MenuItem("Tools/Tower Defense/Generated Enemies/Create Or Update Generated Enemy Prefabs")]
    public static void CreateOrUpdateGeneratedEnemyPrefabs()
    {
        EnsureFolder(GeneratedPrefabFolder);
        EnsureFolder(GeneratedMaterialFolder);

        int createdOrUpdatedCount = 0;

        foreach (EnemyDefinition definition in EnemyDefinitions)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);

            if (model == null)
            {
                Debug.LogWarning("GeneratedEnemyPrefabSetup: FBX fehlt, Prefab wird nicht ueberschrieben: " + definition.ModelPath);
                continue;
            }

            GameObject prefabRoot = CreateGeneratedEnemyRoot(definition, model);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, definition.PrefabPath);
            Object.DestroyImmediate(prefabRoot);

            if (savedPrefab == null)
            {
                Debug.LogError("GeneratedEnemyPrefabSetup: Prefab konnte nicht gespeichert werden: " + definition.PrefabPath);
                continue;
            }

            createdOrUpdatedCount++;
            Debug.Log("GeneratedEnemyPrefabSetup: Prefab erstellt/aktualisiert: " + definition.PrefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GeneratedEnemyPrefabSetup: Fertig. " + createdOrUpdatedCount + " Generated Enemy Prefabs erstellt oder aktualisiert.");
    }

    [MenuItem("Tools/Tower Defense/Generated Enemies/Apply Generated Enemy Prefabs To Open Scene")]
    public static void ApplyGeneratedEnemyPrefabsToOpenScene()
    {
        ApplyGeneratedEnemyPrefabsToOpenScene(true);
    }

    private static void ApplyGeneratedEnemyPrefabsToOpenScene(bool createMissingPrefabsFirst)
    {
        if (createMissingPrefabsFirst && !AllGeneratedPrefabsExist())
        {
            Debug.Log("GeneratedEnemyPrefabSetup: Generated Enemy Prefabs fehlen noch. Erstelle sie vor dem Anwenden auf die Szene.");
            CreateOrUpdateGeneratedEnemyPrefabs();
        }

        EnemySpawner[] spawners = Object.FindObjectsOfType<EnemySpawner>(true);

        if (spawners == null || spawners.Length == 0)
        {
            Debug.LogWarning("GeneratedEnemyPrefabSetup: Kein EnemySpawner in der offenen Szene gefunden.");
            return;
        }

        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            Undo.RecordObject(spawner, "Apply Generated Enemy Prefabs");
            ApplyToSpawner(spawner);
            EditorUtility.SetDirty(spawner);

            if (spawner.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

            Debug.Log("GeneratedEnemyPrefabSetup: EnemySpawner aktualisiert: " + spawner.name);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("GeneratedEnemyPrefabSetup: Generated Enemy Prefabs wurden auf die offene Szene angewendet.");
    }

    private static GameObject CreateGeneratedEnemyRoot(EnemyDefinition definition, GameObject model)
    {
        GameObject root = new GameObject(definition.AssetName);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject visual = InstantiateModelVisual(model);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(GeneratedModelEulerCorrection);
        visual.transform.localScale = Vector3.one;

        RemoveUnwantedImportedObjects(visual);
        ApplyBestUprightRotation(visual);
        AlignVisualToEnemyRoot(visual, definition);
        ValidateAndRepairMaterials(root, definition);

        Bounds bounds = GetSafeBounds(root, definition);
        AddEnemyCollider(root, bounds);

        Enemy enemy = root.AddComponent<Enemy>();
        ConfigureEnemy(enemy, root, definition, bounds);

        return root;
    }

    private static GameObject InstantiateModelVisual(GameObject model)
    {
        GameObject visual = PrefabUtility.InstantiatePrefab(model) as GameObject;

        if (visual == null)
            visual = Object.Instantiate(model);

        return visual;
    }

    private static void ApplyBestUprightRotation(GameObject visual)
    {
        if (visual == null)
            return;

        Vector3 bestEuler = GeneratedModelEulerCorrection;
        float bestScore = float.NegativeInfinity;

        foreach (Vector3 candidate in UprightRotationCandidates)
        {
            visual.transform.localRotation = Quaternion.Euler(candidate);

            if (!TryGetRendererBounds(visual, out Bounds bounds))
                continue;

            float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
            float score = bounds.size.y - horizontalSize * 0.05f;

            if (score > bestScore)
            {
                bestScore = score;
                bestEuler = candidate;
            }
        }

        visual.transform.localRotation = Quaternion.Euler(bestEuler);
    }

    private static void AlignVisualToEnemyRoot(GameObject visual, EnemyDefinition definition)
    {
        if (visual == null)
            return;

        if (!TryGetRendererBounds(visual, out Bounds bounds))
        {
            Debug.LogWarning("GeneratedEnemyPrefabSetup: Konnte Visual-Bounds nicht bestimmen: " + definition.ModelPath);
            return;
        }

        Vector3 localPosition = visual.transform.localPosition;
        localPosition.x -= bounds.center.x;
        localPosition.y += VisualBottomLocalY - bounds.min.y;
        localPosition.z -= bounds.center.z;
        visual.transform.localPosition = localPosition;

        if (TryGetRendererBounds(visual, out Bounds alignedBounds))
        {
            Debug.Log(
                "GeneratedEnemyPrefabSetup: " + definition.AssetName +
                " rotiert (" + visual.transform.localEulerAngles + ") und auf Ground ausgerichtet. Bounds: " +
                alignedBounds.size
            );
        }
    }

    private static Bounds GetSafeBounds(GameObject root, EnemyDefinition definition)
    {
        if (TryGetRendererBounds(root, out Bounds bounds))
            return bounds;

        Debug.LogWarning("GeneratedEnemyPrefabSetup: Fallback-Bounds fuer Enemy verwendet: " + definition.AssetName);
        return new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 1f, 0.6f));
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

    private static void AddEnemyCollider(GameObject root, Bounds bounds)
    {
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float radius = Mathf.Clamp(width * 0.35f, 0.12f, 1.4f);
        float height = Mathf.Max(bounds.size.y, radius * 2f, 0.5f);

        collider.direction = 1;
        collider.radius = radius;
        collider.height = height;
        collider.center = new Vector3(0f, VisualBottomLocalY + height * 0.5f, 0f);
    }

    private static void ConfigureEnemy(Enemy enemy, GameObject root, EnemyDefinition definition, Bounds bounds)
    {
        if (enemy == null)
            return;

        enemy.enemyRole = definition.Role;
        enemy.enemyVariantType = definition.VariantType;
        enemy.autoApplyRoleStats = true;
        enemy.autoApplyVariantStats = true;
        enemy.useRoleColors = false;
        enemy.preserveRendererMaterialColors = true;
        enemy.useHealthBar = true;
        enemy.hideLegacyHealthTexts = true;
        enemy.rotateToMovementDirection = true;
        enemy.movementTurnSpeed = 720f;
        enemy.rotateOnlyYaw = true;
        enemy.movementFacingEulerOffset = Vector3.zero;
        enemy.defaultColor = definition.FallbackColor;
        enemy.enemyRenderers = root.GetComponentsInChildren<Renderer>(true);

        GameObject healthBarObject = new GameObject("EnemyHealthBar");
        healthBarObject.transform.SetParent(root.transform, false);

        EnemyHealthBar healthBar = healthBarObject.AddComponent<EnemyHealthBar>();
        healthBar.enemy = enemy;
        healthBar.localOffset = new Vector3(0f, Mathf.Max(0.9f, bounds.max.y + 0.25f), 0f);
        enemy.healthBar = healthBar;
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

    private static void ValidateAndRepairMaterials(GameObject root, EnemyDefinition definition)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("GeneratedEnemyPrefabSetup: FBX enthaelt keine sichtbaren Renderer: " + definition.ModelPath);
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
                Debug.LogWarning("GeneratedEnemyPrefabSetup: Renderer ohne Material repariert: " + definition.ModelPath);
                continue;
            }

            bool changed = false;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                if (sharedMaterials[i] != null)
                    continue;

                fallbackMaterial = fallbackMaterial != null ? fallbackMaterial : GetOrCreateFallbackMaterial(definition);
                sharedMaterials[i] = fallbackMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = sharedMaterials;
                Debug.LogWarning("GeneratedEnemyPrefabSetup: Fehlende Material-Slots repariert: " + definition.ModelPath);
            }
        }
    }

    private static Material GetOrCreateFallbackMaterial(EnemyDefinition definition)
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
        Debug.LogWarning("GeneratedEnemyPrefabSetup: Fallback-Material erstellt, weil FBX-Material fehlte: " + materialPath);
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ApplyToSpawner(EnemySpawner spawner)
    {
        spawner.useGeneratedEnemyPrefabs = true;
        spawner.preferChaosVariantPrefabs = true;

        AssignIfNotNull(ref spawner.standardEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Standard"));
        AssignIfNotNull(ref spawner.runnerEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Runner"));
        AssignIfNotNull(ref spawner.tankEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Tank"));
        AssignIfNotNull(ref spawner.knightEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Knight"));
        AssignIfNotNull(ref spawner.mageEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Mage"));
        AssignIfNotNull(ref spawner.learnerEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Learner"));
        AssignIfNotNull(ref spawner.allRounderEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_AllRounder"));
        AssignIfNotNull(ref spawner.miniBossEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_MiniBoss"));
        AssignIfNotNull(ref spawner.bossEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Boss"));
        AssignIfNotNull(ref spawner.eliteEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Elite"));

        AssignIfNotNull(ref spawner.chaosStandardEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Standard_Chaos"));
        AssignIfNotNull(ref spawner.chaosRunnerEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Runner_Chaos"));
        AssignIfNotNull(ref spawner.chaosTankEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Tank_Chaos"));
        AssignIfNotNull(ref spawner.chaosKnightEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Knight_Chaos"));
        AssignIfNotNull(ref spawner.chaosMageEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Mage_Chaos"));
        AssignIfNotNull(ref spawner.chaosLearnerEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Learner_Chaos"));
        AssignIfNotNull(ref spawner.chaosAllRounderEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_AllRounder_Chaos"));
        AssignIfNotNull(ref spawner.chaosMiniBossEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_MiniBoss_Chaos"));
        AssignIfNotNull(ref spawner.chaosBossEnemyPrefab, LoadGeneratedPrefab("TD_Enemy_Boss_Chaos"));
    }

    private static bool NeedsGeneratedEnemySetupRepair()
    {
        if (!AnyGeneratedEnemyModelExists())
            return false;

        if (!AllGeneratedPrefabsExist())
            return true;

        EnemySpawner[] spawners = Object.FindObjectsOfType<EnemySpawner>(true);

        if (spawners == null || spawners.Length == 0)
            return false;

        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            if (SpawnerNeedsGeneratedAssignment(spawner))
                return true;
        }

        return false;
    }

    private static bool AnyGeneratedEnemyModelExists()
    {
        foreach (EnemyDefinition definition in EnemyDefinitions)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath) != null)
                return true;
        }

        return false;
    }

    private static bool AllGeneratedPrefabsExist()
    {
        foreach (EnemyDefinition definition in EnemyDefinitions)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath) == null)
                return false;
        }

        return true;
    }

    private static bool SpawnerNeedsGeneratedAssignment(EnemySpawner spawner)
    {
        return !IsAssignedGeneratedPrefab(spawner.standardEnemyPrefab, "TD_Enemy_Standard") ||
            !IsAssignedGeneratedPrefab(spawner.runnerEnemyPrefab, "TD_Enemy_Runner") ||
            !IsAssignedGeneratedPrefab(spawner.tankEnemyPrefab, "TD_Enemy_Tank") ||
            !IsAssignedGeneratedPrefab(spawner.knightEnemyPrefab, "TD_Enemy_Knight") ||
            !IsAssignedGeneratedPrefab(spawner.mageEnemyPrefab, "TD_Enemy_Mage") ||
            !IsAssignedGeneratedPrefab(spawner.learnerEnemyPrefab, "TD_Enemy_Learner") ||
            !IsAssignedGeneratedPrefab(spawner.allRounderEnemyPrefab, "TD_Enemy_AllRounder") ||
            !IsAssignedGeneratedPrefab(spawner.miniBossEnemyPrefab, "TD_Enemy_MiniBoss") ||
            !IsAssignedGeneratedPrefab(spawner.bossEnemyPrefab, "TD_Enemy_Boss") ||
            !IsAssignedGeneratedPrefab(spawner.eliteEnemyPrefab, "TD_Enemy_Elite") ||
            !IsAssignedGeneratedPrefab(spawner.chaosStandardEnemyPrefab, "TD_Enemy_Standard_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosRunnerEnemyPrefab, "TD_Enemy_Runner_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosTankEnemyPrefab, "TD_Enemy_Tank_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosKnightEnemyPrefab, "TD_Enemy_Knight_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosMageEnemyPrefab, "TD_Enemy_Mage_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosLearnerEnemyPrefab, "TD_Enemy_Learner_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosAllRounderEnemyPrefab, "TD_Enemy_AllRounder_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosMiniBossEnemyPrefab, "TD_Enemy_MiniBoss_Chaos") ||
            !IsAssignedGeneratedPrefab(spawner.chaosBossEnemyPrefab, "TD_Enemy_Boss_Chaos");
    }

    private static bool IsAssignedGeneratedPrefab(GameObject assignedPrefab, string generatedPrefabName)
    {
        GameObject generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedPrefabFolder + "/" + generatedPrefabName + ".prefab");

        if (generatedPrefab == null)
            return false;

        return assignedPrefab == generatedPrefab;
    }

    private static void AssignIfNotNull(ref GameObject target, GameObject value)
    {
        if (value != null)
            target = value;
    }

    private static GameObject LoadGeneratedPrefab(string prefabName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedPrefabFolder + "/" + prefabName + ".prefab");

        if (prefab == null)
            Debug.LogWarning("GeneratedEnemyPrefabSetup: Generated Prefab fehlt: " + prefabName);

        return prefab;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent.Replace("\\", "/"));

        AssetDatabase.CreateFolder(parent.Replace("\\", "/"), folderName);
    }

    private class EnemyDefinition
    {
        public readonly string AssetName;
        public readonly EnemyRole Role;
        public readonly EnemyVariantType VariantType;
        public readonly Color FallbackColor;

        public EnemyDefinition(string assetName, EnemyRole role, EnemyVariantType variantType, Color fallbackColor)
        {
            AssetName = assetName;
            Role = role;
            VariantType = variantType;
            FallbackColor = fallbackColor;
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
