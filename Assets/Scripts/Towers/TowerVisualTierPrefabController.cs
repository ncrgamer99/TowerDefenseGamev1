using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tower))]
public class TowerVisualTierPrefabController : MonoBehaviour
{
    private const string VisualRootName = "VisualRoot";
    private const string AimPivotName = "AimPivot";
    private const string BaseModelRootName = "BaseModelRoot";
    private const string AimModelRootName = "AimModelRoot";
    private const string GeneratedVisualPrefix = "VisualTierPrefab_";

    [Header("References")]
    public Tower tower;
    public Transform visualRoot;
    public Transform aimPivot;

    [Header("Visual Tier Prefabs")]
    public GameObject[] visualTierPrefabs = new GameObject[4];
    public bool useFixedVisualTierPrefabs = true;
    public bool destroyPreviousVisual = true;
    public int maxTierIndex = 3;
    public bool applyGeneratedModelRotationFix = true;
    public Vector3 generatedModelEulerCorrection = new Vector3(-90f, 0f, 0f);
    public bool applyGeneratedVisualGroundOffset = true;
    public Vector3 generatedVisualRootOffset = new Vector3(0f, -0.5f, 0f);
    public bool autoAlignGeneratedVisualsToGround = true;
    public float localGroundY = -0.5f;
    public float baseHideDepth = 0.01f;
    public float visiblePartGroundPadding = 0.005f;
    public bool applyGeneratedRoleColors = true;

    private readonly List<GameObject> spawnedVisuals = new List<GameObject>();
    private int appliedPrefabIndex = -1;
    private Transform baseModelRoot;
    private Transform aimModelRoot;

    private void Awake()
    {
        if (tower == null)
            tower = GetComponent<Tower>();

        EnsureVisualRoots();
        Refresh();
    }

    private void OnValidate()
    {
        maxTierIndex = Mathf.Max(0, maxTierIndex);

        if (visualTierPrefabs == null || visualTierPrefabs.Length < 4)
        {
            GameObject[] resizedPrefabs = new GameObject[4];

            if (visualTierPrefabs != null)
            {
                int copyCount = Mathf.Min(visualTierPrefabs.Length, resizedPrefabs.Length);
                for (int i = 0; i < copyCount; i++)
                    resizedPrefabs[i] = visualTierPrefabs[i];
            }

            visualTierPrefabs = resizedPrefabs;
        }
    }

    public void Refresh()
    {
        if (tower == null)
            tower = GetComponent<Tower>();

        ApplyTier(tower != null ? tower.visualTier : 0);
    }

    public void ApplyTier(int tier)
    {
        if (!useFixedVisualTierPrefabs)
            return;

        EnsureVisualRoots();

        int prefabIndex = ResolvePrefabIndex(tier);

        if (prefabIndex < 0)
        {
            ClearPreviousVisuals();
            appliedPrefabIndex = -1;
            Debug.LogWarning(name + ": Kein VisualTier-Prefab fuer Tier " + tier + " gefunden.");
            return;
        }

        if ((appliedPrefabIndex == prefabIndex || appliedPrefabIndex < 0) && HasExpectedGeneratedVisual(prefabIndex))
        {
            AlignGeneratedVisualsToGround();
            ApplyGeneratedRoleVisualColors();
            appliedPrefabIndex = prefabIndex;
            return;
        }

        if (appliedPrefabIndex == prefabIndex && HasSpawnedVisual())
            return;

        ClearPreviousVisuals();

        Transform defaultParent = aimPivot != null ? aimPivot : visualRoot;
        GameObject instance = Instantiate(visualTierPrefabs[prefabIndex], defaultParent, false);
        instance.name = GeneratedVisualPrefix + visualTierPrefabs[prefabIndex].name;
        spawnedVisuals.Add(instance);

        ArrangeVisualInstance(instance);
        AlignGeneratedVisualsToGround();
        ApplyGeneratedRoleVisualColors();
        appliedPrefabIndex = prefabIndex;
    }

    private int ResolvePrefabIndex(int tier)
    {
        if (visualTierPrefabs == null || visualTierPrefabs.Length == 0)
            return -1;

        int highestAllowedIndex = Mathf.Min(Mathf.Max(0, maxTierIndex), visualTierPrefabs.Length - 1);
        int desiredIndex = tier >= 3 ? Mathf.Min(3, highestAllowedIndex) : Mathf.Clamp(tier, 0, highestAllowedIndex);

        for (int i = desiredIndex; i >= 0; i--)
        {
            if (visualTierPrefabs[i] != null)
                return i;
        }

        return -1;
    }

    private void EnsureVisualRoots()
    {
        bool hadVisualRoot = visualRoot != null;
        Quaternion preservedVisualRootRotation = hadVisualRoot ? visualRoot.localRotation : Quaternion.identity;

        if (visualRoot == null)
        {
            Transform existingVisualRoot = transform.Find(VisualRootName);
            visualRoot = existingVisualRoot != null ? existingVisualRoot : CreateChild(transform, VisualRootName);
            hadVisualRoot = existingVisualRoot != null;
            preservedVisualRootRotation = hadVisualRoot ? visualRoot.localRotation : Quaternion.identity;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = applyGeneratedVisualGroundOffset ? generatedVisualRootOffset : Vector3.zero;
            visualRoot.localRotation = hadVisualRoot ? preservedVisualRootRotation : Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }

        bool hadAimPivot = aimPivot != null;
        Quaternion preservedAimPivotRotation = hadAimPivot ? aimPivot.localRotation : Quaternion.identity;

        if (aimPivot == null && visualRoot != null)
        {
            Transform existingAimPivot = visualRoot.Find(AimPivotName);
            aimPivot = existingAimPivot != null ? existingAimPivot : CreateChild(visualRoot, AimPivotName);
            hadAimPivot = existingAimPivot != null;
            preservedAimPivotRotation = hadAimPivot ? aimPivot.localRotation : Quaternion.identity;
        }

        if (aimPivot != null)
        {
            aimPivot.localPosition = Vector3.zero;
            aimPivot.localRotation = hadAimPivot ? preservedAimPivotRotation : Quaternion.identity;
            aimPivot.localScale = Vector3.one;
        }

        if (visualRoot != null && baseModelRoot == null)
        {
            Transform existingBaseModelRoot = visualRoot.Find(BaseModelRootName);
            baseModelRoot = existingBaseModelRoot != null ? existingBaseModelRoot : CreateChild(visualRoot, BaseModelRootName);
        }

        if (aimPivot != null && aimModelRoot == null)
        {
            Transform existingAimModelRoot = aimPivot.Find(AimModelRootName);
            aimModelRoot = existingAimModelRoot != null ? existingAimModelRoot : CreateChild(aimPivot, AimModelRootName);
        }

        ApplyModelRootRotation(baseModelRoot);
        ApplyModelRootRotation(aimModelRoot);
        SyncAimControllerPivotIfMissing();
        MoveExistingGeneratedVisualsIntoModelRoots();
    }

    private void SyncAimControllerPivotIfMissing()
    {
        if (aimPivot == null)
            return;

        TowerAimController aimController = GetComponent<TowerAimController>();
        if (aimController != null && aimController.aimPivot == null)
            aimController.aimPivot = aimPivot;
    }

    private Transform CreateChild(Transform parent, string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private void ApplyModelRootRotation(Transform modelRoot)
    {
        if (modelRoot == null)
            return;

        modelRoot.localPosition = Vector3.zero;
        modelRoot.localScale = Vector3.one;
        modelRoot.localRotation = applyGeneratedModelRotationFix
            ? Quaternion.Euler(GetEffectiveGeneratedModelEulerCorrection())
            : Quaternion.identity;
    }

    private Vector3 GetEffectiveGeneratedModelEulerCorrection()
    {
        if (UsesUnityOrientedGeneratedModel())
            return Vector3.zero;

        return generatedModelEulerCorrection;
    }

    private bool UsesUnityOrientedGeneratedModel()
    {
        if (tower == null)
            tower = GetComponent<Tower>();

        if (tower == null)
            return false;

        return tower.towerRole == TowerRole.Beam ||
               tower.towerRole == TowerRole.Support ||
               tower.towerRole == TowerRole.Frost;
    }

    private void MoveExistingGeneratedVisualsIntoModelRoots()
    {
        MoveGeneratedVisualChildrenToModelRoots(visualRoot);
        MoveGeneratedVisualChildrenToModelRoots(aimPivot);
    }

    private void MoveGeneratedVisualChildrenToModelRoots(Transform parent)
    {
        if (parent == null)
            return;

        List<Transform> childrenToMove = new List<Transform>();

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child == null || !child.name.StartsWith(GeneratedVisualPrefix))
                continue;

            childrenToMove.Add(child);
        }

        foreach (Transform child in childrenToMove)
        {
            Transform targetParent = ResolveParentForPart(child.name);

            if (targetParent != null && child.parent != targetParent)
                child.SetParent(targetParent, false);
        }
    }

    private bool HasSpawnedVisual()
    {
        for (int i = 0; i < spawnedVisuals.Count; i++)
        {
            if (spawnedVisuals[i] != null)
                return true;
        }

        return HasGeneratedVisualChild(visualRoot) ||
               HasGeneratedVisualChild(aimPivot) ||
               HasGeneratedVisualChild(baseModelRoot) ||
               HasGeneratedVisualChild(aimModelRoot);
    }

    private bool HasGeneratedVisualChild(Transform parent)
    {
        if (parent == null)
            return false;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child != null && child.name.StartsWith(GeneratedVisualPrefix))
                return true;
        }

        return false;
    }

    private bool HasExpectedGeneratedVisual(int prefabIndex)
    {
        if (visualTierPrefabs == null || prefabIndex < 0 || prefabIndex >= visualTierPrefabs.Length || visualTierPrefabs[prefabIndex] == null)
            return false;

        string expectedName = visualTierPrefabs[prefabIndex].name;
        return HasExpectedGeneratedVisualChild(visualRoot, expectedName) ||
               HasExpectedGeneratedVisualChild(aimPivot, expectedName) ||
               HasExpectedGeneratedVisualChild(baseModelRoot, expectedName) ||
               HasExpectedGeneratedVisualChild(aimModelRoot, expectedName);
    }

    private bool HasExpectedGeneratedVisualChild(Transform parent, string expectedName)
    {
        if (parent == null || string.IsNullOrEmpty(expectedName))
            return false;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child != null && child.name.StartsWith(GeneratedVisualPrefix) && child.name.Contains(expectedName))
                return true;
        }

        return false;
    }

    private void ClearPreviousVisuals()
    {
        if (!destroyPreviousVisual)
            return;

        for (int i = spawnedVisuals.Count - 1; i >= 0; i--)
            DestroyVisual(spawnedVisuals[i]);

        spawnedVisuals.Clear();

        ClearGeneratedVisualChildren(visualRoot);
        ClearGeneratedVisualChildren(aimPivot);
        ClearGeneratedVisualChildren(baseModelRoot);
        ClearGeneratedVisualChildren(aimModelRoot);
    }

    private void ClearGeneratedVisualChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);

            if (child != null && child.name.StartsWith(GeneratedVisualPrefix))
                DestroyVisual(child.gameObject);
        }
    }

    private void ArrangeVisualInstance(GameObject instance)
    {
        if (instance == null || visualRoot == null || aimPivot == null)
            return;

        Transform instanceTransform = instance.transform;
        List<Transform> directChildren = new List<Transform>();

        for (int i = 0; i < instanceTransform.childCount; i++)
            directChildren.Add(instanceTransform.GetChild(i));

        if (directChildren.Count == 0)
        {
            instanceTransform.SetParent(ResolveParentForPart(instanceTransform.name), false);
            return;
        }

        foreach (Transform child in directChildren)
        {
            if (child == null)
                continue;

            Transform targetParent = ResolveParentForPart(child.name);
            child.SetParent(targetParent, false);

            if (!child.name.StartsWith(GeneratedVisualPrefix))
                child.name = GeneratedVisualPrefix + child.name;

            spawnedVisuals.Add(child.gameObject);
        }

        if (!HasRenderableContent(instanceTransform))
        {
            spawnedVisuals.Remove(instance);
            DestroyVisual(instance);
        }
    }

    private Transform ResolveParentForPart(string partName)
    {
        if (IsStationaryBasePart(partName) || aimPivot == null)
            return baseModelRoot != null ? baseModelRoot : visualRoot;

        return aimModelRoot != null ? aimModelRoot : aimPivot;
    }

    private bool IsStationaryBasePart(string partName)
    {
        string lowerName = string.IsNullOrEmpty(partName) ? "" : partName.ToLowerInvariant();
        return lowerName.Contains("base") ||
               lowerName.Contains("sockel") ||
               lowerName.Contains("pedestal") ||
               lowerName.Contains("foundation") ||
               lowerName.Contains("plinth");
    }

    private void AlignGeneratedVisualsToGround()
    {
        if (!autoAlignGeneratedVisualsToGround || visualRoot == null)
            return;

        BoundsInfo baseBounds = new BoundsInfo();
        BoundsInfo visibleBounds = new BoundsInfo();

        CollectRendererBounds(baseModelRoot, true, baseBounds, visibleBounds);
        CollectRendererBounds(aimModelRoot, false, baseBounds, visibleBounds);

        if (!baseBounds.hasBounds && !visibleBounds.hasBounds)
            return;

        float deltaY = 0f;

        if (baseBounds.hasBounds)
        {
            float hideBaseDelta = localGroundY - baseHideDepth - baseBounds.maxY;
            deltaY = hideBaseDelta;

            if (visibleBounds.hasBounds)
            {
                float keepVisibleDelta = localGroundY + visiblePartGroundPadding - visibleBounds.minY;

                if (keepVisibleDelta > hideBaseDelta)
                {
                    deltaY = keepVisibleDelta;
                    Debug.LogWarning(name + ": Sockel kann nicht vollstaendig versenkt werden, ohne sichtbare Tower-Teile unter den Ground zu schieben.");
                }
            }
        }
        else if (visibleBounds.hasBounds)
        {
            deltaY = localGroundY + visiblePartGroundPadding - visibleBounds.minY;
        }

        if (Mathf.Abs(deltaY) < 0.0001f)
            return;

        visualRoot.localPosition += Vector3.up * deltaY;
    }

    private void CollectRendererBounds(Transform root, bool forceBase, BoundsInfo baseBounds, BoundsInfo visibleBounds)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            bool isBasePart = forceBase || IsStationaryBasePart(renderer.gameObject.name);
            AddRendererBounds(renderer, isBasePart ? baseBounds : visibleBounds);
        }
    }

    private void AddRendererBounds(Renderer renderer, BoundsInfo targetBounds)
    {
        Bounds bounds = renderer.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    float localY = transform.InverseTransformPoint(worldCorner).y;
                    targetBounds.Add(localY);
                }
            }
        }
    }

    private void ApplyGeneratedRoleVisualColors()
    {
        if (!Application.isPlaying || !applyGeneratedRoleColors)
            return;

        if (tower == null)
            tower = GetComponent<Tower>();

        if (tower == null)
            return;

        if (!UsesGeneratedRolePalette(tower.towerRole))
            return;

        ApplyGeneratedRoleVisualColors(baseModelRoot);
        ApplyGeneratedRoleVisualColors(aimModelRoot);
    }

    private void ApplyGeneratedRoleVisualColors(Transform root)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                string marker = (targetRenderer.gameObject.name + " " + material.name).ToLowerInvariant();
                Color color = ResolveGeneratedRoleColor(tower.towerRole, marker);
                ApplyMaterialColor(material, color, IsGeneratedRoleGlowPart(marker));
            }
        }
    }

    private bool UsesGeneratedRolePalette(TowerRole role)
    {
        return role == TowerRole.Beam ||
               role == TowerRole.Support ||
               role == TowerRole.Frost;
    }

    private Color ResolveGeneratedRoleColor(TowerRole role, string marker)
    {
        switch (role)
        {
            case TowerRole.Beam:
                if (marker.Contains("energy") || marker.Contains("lens") || marker.Contains("glow"))
                    return new Color32(70, 235, 255, 255);
                if (marker.Contains("gold") || marker.Contains("trim") || marker.Contains("ring"))
                    return new Color32(245, 185, 60, 255);
                if (marker.Contains("barrel") || marker.Contains("steel"))
                    return new Color32(105, 135, 165, 255);
                if (marker.Contains("base") || marker.Contains("dark"))
                    return new Color32(36, 48, 68, 255);
                return new Color32(90, 220, 245, 255);

            case TowerRole.Support:
                if (marker.Contains("energy") || marker.Contains("beacon") || marker.Contains("plus") || marker.Contains("glow"))
                    return new Color32(95, 245, 135, 255);
                if (marker.Contains("gold") || marker.Contains("trim"))
                    return new Color32(235, 190, 70, 255);
                if (marker.Contains("antenna") || marker.Contains("steel"))
                    return new Color32(135, 170, 155, 255);
                if (marker.Contains("base") || marker.Contains("pedestal") || marker.Contains("dark"))
                    return new Color32(34, 58, 48, 255);
                return new Color32(90, 220, 120, 255);

            case TowerRole.Frost:
                if (marker.Contains("ice") || marker.Contains("crystal") || marker.Contains("core") || marker.Contains("glow") || marker.Contains("center"))
                    return new Color32(120, 225, 255, 255);
                if (marker.Contains("ring"))
                    return new Color32(185, 245, 255, 255);
                if (marker.Contains("steel"))
                    return new Color32(110, 150, 175, 255);
                if (marker.Contains("base") || marker.Contains("dark"))
                    return new Color32(34, 52, 70, 255);
                return new Color32(130, 215, 255, 255);

            default:
                return Color.white;
        }
    }

    private bool IsGeneratedRoleGlowPart(string marker)
    {
        return marker.Contains("energy") ||
               marker.Contains("glow") ||
               marker.Contains("beacon") ||
               marker.Contains("plus") ||
               marker.Contains("crystal") ||
               marker.Contains("core") ||
               marker.Contains("lens");
    }

    private void ApplyMaterialColor(Material material, Color color, bool glow)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_EmissionColor"))
        {
            Color emissionColor = glow ? color * 1.45f : Color.black;
            material.SetColor("_EmissionColor", emissionColor);
        }
    }

    private sealed class BoundsInfo
    {
        public bool hasBounds;
        public float minY;
        public float maxY;

        public void Add(float value)
        {
            if (!hasBounds)
            {
                hasBounds = true;
                minY = value;
                maxY = value;
                return;
            }

            minY = Mathf.Min(minY, value);
            maxY = Mathf.Max(maxY, value);
        }
    }

    private bool HasRenderableContent(Transform target)
    {
        if (target == null)
            return false;

        return target.GetComponent<MeshRenderer>() != null ||
               target.GetComponent<SkinnedMeshRenderer>() != null ||
               target.GetComponent<MeshFilter>() != null;
    }

    private void DestroyVisual(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
