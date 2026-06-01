using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class AbyssGroundRenderer : MonoBehaviour
{
    [Header("References")]
    public TileManager tileManager;
    public string sceneGroundObjectName = "Ground";
    public bool hideSceneGroundRenderer = true;

    [Header("Shape")]
    public float pathBridgeHalfWidth = 0.82f;
    public float cliffHalfWidth = 3.4f;
    public float pathEndExtension = 7f;
    public float environmentPadding = 72f;
    public float minimumEnvironmentSize = 150f;
    public float sampleSpacing = 0.24f;
    [Range(0f, 1f)] public float cornerRoundness = 0.68f;
    public int smoothingPasses = 2;
    public float maxContinuousPathGap = 2.2f;
    public float edgeNoiseAmplitude = 0.34f;

    [Header("Floating Island")]
    public float islandHalfWidth = 1.24f;
    public float islandEdgeNoiseAmplitude = 0.18f;
    public float islandSurfaceNoiseAmplitude = 0.012f;
    public float cliffOutset = 1.05f;
    public float rockStepHeight = 0.16f;

    [Header("Mountain Ridge")]
    public float ridgeShoulderWidth = 0.42f;
    public float ridgeEdgeNoiseAmplitude = 0.16f;
    public float ridgeSurfaceNoiseAmplitude = 0.008f;

    [Header("Vertical Layers")]
    public float visualSurfaceY = 0.006f;
    public float cliffTopY = -0.035f;
    public float cliffMidY = -0.72f;
    public float abyssY = -1.82f;
    public float fogBaseY = 0.03f;
    public float rimLift = 0.018f;

    [Header("Fog")]
    public int fogLayerCount = 0;
    public float fogAlpha = 0.18f;
    public float fogDriftSpeed = 0.12f;
    public int fogTextureSize = 256;
    public float fogInnerOffset = 0.2f;
    public float fogNearWidth = 2.4f;
    public float fogFarWidth = 8.4f;
    public float fogEdgeNoiseAmplitude = 0.78f;

    [Header("Cloud Puffs")]
    public int cloudRowsPerSide = 3;
    public float cloudPuffSpacing = 1.65f;
    public float cloudInnerDistance = 0.18f;
    public float cloudRowSpacing = 1.05f;
    public float cloudPuffRadius = 1.5f;
    public float cloudHeight = 0.22f;
    public float cloudBaseY = -0.58f;
    public float cloudDriftAmplitude = 0.045f;
    public float cloudDriftSpeed = 0.18f;

    [Header("Low Poly Details")]
    public float detailSpacing = 5.5f;
    public float detailInnerDistance = 0.24f;
    public float detailEdgeInset = 0.12f;
    public float stonePlateScale = 0.1f;
    public float rockDetailScale = 0.08f;
    public float vegetationScale = 0.12f;
    public int waterfallMaxCount = 4;
    public float waterfallWidth = 0.2f;
    public float waterfallDropDepth = 1.45f;

    [Header("Colors")]
    public Color abyssColor = new Color32(228, 233, 232, 255);
    public Color distantMistColor = new Color32(245, 244, 238, 170);
    public Color cliffColor = new Color32(158, 82, 44, 255);
    public Color cliffRimColor = new Color32(226, 171, 98, 255);
    public Color deepFogColor = new Color32(250, 248, 241, 205);
    public Color ridgeTopColor = new Color32(202, 126, 70, 255);
    public Color plateauTopColor = new Color32(226, 171, 105, 255);
    public Color cloudPuffColor = new Color32(255, 253, 246, 74);
    public Color stonePlateColor = new Color32(244, 203, 140, 255);
    public Color rockDetailColor = new Color32(86, 57, 48, 255);
    public Color vegetationColor = new Color32(31, 84, 43, 255);
    public Color waterfallColor = new Color32(75, 198, 238, 255);

    [Header("Refresh")]
    public float refreshInterval = 0.25f;

    private const string RuntimeRootName = "Abyss Ground Runtime Visuals";
    private static readonly Vector2Int[] TileDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private Transform visualRoot;
    private MeshFilter abyssFilter;
    private MeshFilter ridgeTopFilter;
    private MeshFilter leftCliffFilter;
    private MeshFilter rightCliffFilter;
    private MeshFilter leftRimFilter;
    private MeshFilter rightRimFilter;
    private MeshFilter stonePlateFilter;
    private MeshFilter rockDetailFilter;
    private MeshFilter vegetationFilter;
    private MeshFilter waterfallFilter;
    private MeshFilter cloudPuffFilter;

    private Material abyssMaterial;
    private Material ridgeTopMaterial;
    private Material cliffMaterial;
    private Material rimMaterial;
    private Material stonePlateMaterial;
    private Material rockDetailMaterial;
    private Material vegetationMaterial;
    private Material waterfallMaterial;
    private Material cloudPuffMaterial;
    private Texture2D abyssTexture;
    private Texture2D ridgeTexture;
    private Texture2D fogTextureA;
    private Texture2D fogTextureB;
    private Shader sceneGroundShaderFallback;

    private readonly List<FogLayer> fogLayers = new List<FogLayer>();
    private float refreshTimer;
    private int lastPathHash = int.MinValue;
    private bool initialized;
    private bool groundVisualHidden;

    private class FogLayer
    {
        public MeshFilter filter;
        public Material material;
        public Vector2 offset;
        public Vector2 speed;
    }

    public static AbyssGroundRenderer EnsureExists(TileManager source)
    {
        AbyssGroundRenderer renderer = FindFirstObjectByType<AbyssGroundRenderer>();

        if (renderer == null)
        {
            GameObject rendererObject = new GameObject("AbyssGroundRenderer");
            renderer = rendererObject.AddComponent<AbyssGroundRenderer>();
        }

        if (renderer.tileManager == null)
            renderer.tileManager = source;

        renderer.InitializeRuntime();
        return renderer;
    }

    private void Awake()
    {
        InitializeRuntime();
    }

    private void Start()
    {
        InitializeRuntime();
        ForceRefresh();
    }

    private void Update()
    {
        InitializeRuntime();
        AnimateFog();
        AnimateCloudPuffs();

        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = Mathf.Max(0.05f, refreshInterval);
        RefreshEnvironment(false);
    }

    public void ForceRefresh()
    {
        RefreshEnvironment(true);
    }

    private void InitializeRuntime()
    {
        if (tileManager == null)
            tileManager = FindFirstObjectByType<TileManager>();

        HideSceneGroundVisual();

        if (initialized)
            return;

        initialized = true;
        EnsureVisualRoot();
        ClearVisualRoot();
        CreateMaterials();
        CreateStaticMeshObjects();
        CreateFogLayers();
    }

    private void HideSceneGroundVisual()
    {
        if (groundVisualHidden)
            return;

        if (!hideSceneGroundRenderer)
            return;

        bool hidAny = false;

        GameObject groundObject = string.IsNullOrEmpty(sceneGroundObjectName) ? null : GameObject.Find(sceneGroundObjectName);
        if (groundObject != null)
            hidAny |= DisableGroundRenderers(groundObject.transform, true);

        if (tileManager != null && tileManager.worldVisualRoot != null)
            hidAny |= DisableGroundRenderers(tileManager.worldVisualRoot, true);

        hidAny |= DisableLikelySceneGroundRenderers();

        groundVisualHidden = hidAny;
    }

    private bool DisableGroundRenderers(Transform root, bool force)
    {
        if (root == null)
            return false;

        bool hidAny = false;
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (!force && !IsLikelySceneGroundRenderer(renderers[i]))
                continue;

            CacheSceneGroundShader(renderers[i]);
            renderers[i].enabled = false;
            hidAny = true;
        }

        return hidAny;
    }

    private bool DisableLikelySceneGroundRenderers()
    {
        bool hidAny = false;
        MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (!IsLikelySceneGroundRenderer(renderer))
                continue;

            CacheSceneGroundShader(renderer);
            renderer.enabled = false;
            hidAny = true;
        }

        return hidAny;
    }

    private bool IsLikelySceneGroundRenderer(MeshRenderer renderer)
    {
        if (renderer == null)
            return false;

        Transform rendererTransform = renderer.transform;
        if (visualRoot != null && rendererTransform.IsChildOf(visualRoot))
            return false;

        Bounds bounds = renderer.bounds;
        bool largeFlatSurface = bounds.size.x >= 18f && bounds.size.z >= 18f && bounds.size.y <= 4f;
        if (largeFlatSurface)
            return true;

        string objectName = renderer.gameObject.name.ToLowerInvariant();
        bool groundName = objectName.Contains("ground") || objectName.Contains("floor") || objectName.Contains("plane");
        return groundName && Mathf.Max(bounds.size.x, bounds.size.z) >= 12f;
    }

    private void CacheSceneGroundShader(MeshRenderer renderer)
    {
        if (sceneGroundShaderFallback != null || renderer == null)
            return;

        Material material = renderer.sharedMaterial;
        if (material != null && material.shader != null)
            sceneGroundShaderFallback = material.shader;
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot != null)
            return;

        GameObject existingRoot = GameObject.Find(RuntimeRootName);
        if (existingRoot != null)
        {
            visualRoot = existingRoot.transform;
            visualRoot.SetParent(transform, true);
            return;
        }

        GameObject rootObject = new GameObject(RuntimeRootName);
        rootObject.transform.SetParent(transform, false);
        visualRoot = rootObject.transform;
    }

    private void ClearVisualRoot()
    {
        if (visualRoot == null)
            return;

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = visualRoot.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void CreateMaterials()
    {
        abyssTexture = CreateAbyssTexture(5191);
        ridgeTexture = CreateRidgeTexture(6389);
        fogTextureA = CreateFogTexture(7123);
        fogTextureB = CreateFogTexture(9149);

        abyssMaterial = CreateOpaqueMaterial("Abyss Sky Mist", abyssColor, 0.04f);
        SetMaterialTexture(abyssMaterial, abyssTexture);
        SetTextureScale(abyssMaterial, new Vector2(4.6f, 4.6f));

        ridgeTopMaterial = CreateLitMaterial("Abyss Sandstone Plateau", plateauTopColor, 0.16f);
        SetMaterialTexture(ridgeTopMaterial, ridgeTexture);
        SetTextureScale(ridgeTopMaterial, new Vector2(1.15f, 1.15f));

        cliffMaterial = CreateLitMaterial("Abyss Warm Rock Cliff", cliffColor, 0.16f);
        SetMaterialTexture(cliffMaterial, ridgeTexture);
        SetTextureScale(cliffMaterial, new Vector2(2.2f, 5.4f));

        rimMaterial = CreateLitMaterial("Abyss Sandstone Edge", cliffRimColor, 0.2f);
        SetMaterialTexture(rimMaterial, ridgeTexture);
        SetTextureScale(rimMaterial, new Vector2(1.8f, 7f));

        stonePlateMaterial = CreateLitMaterial("Abyss Sandstone Detail Plates", stonePlateColor, 0.16f);
        SetMaterialTexture(stonePlateMaterial, ridgeTexture);
        SetTextureScale(stonePlateMaterial, new Vector2(1.4f, 1.4f));

        rockDetailMaterial = CreateLitMaterial("Abyss Low Poly Rock Details", rockDetailColor, 0.18f);
        SetMaterialTexture(rockDetailMaterial, ridgeTexture);
        SetTextureScale(rockDetailMaterial, new Vector2(1.7f, 1.7f));

        vegetationMaterial = CreateLitMaterial("Abyss Low Poly Vegetation", vegetationColor, 0.1f);
        waterfallMaterial = CreateLitMaterial("Abyss Low Poly Waterfalls", waterfallColor, 0.12f);
        cloudPuffMaterial = CreateTransparentMaterial("Abyss Low Poly Clouds", cloudPuffColor, fogTextureA, 3025);
    }

    private void CreateStaticMeshObjects()
    {
        abyssFilter = CreateMeshObject("Abyss Dark Depth", abyssMaterial);
        ridgeTopFilter = CreateMeshObject("Abyss Mountain Ridge Top", ridgeTopMaterial);
        leftCliffFilter = CreateMeshObject("Abyss Left Cliff", cliffMaterial);
        rightCliffFilter = CreateMeshObject("Abyss Right Cliff", cliffMaterial);
        leftRimFilter = CreateMeshObject("Abyss Left Organic Rim", rimMaterial);
        rightRimFilter = CreateMeshObject("Abyss Right Organic Rim", rimMaterial);
        stonePlateFilter = CreateMeshObject("Abyss Sandstone Detail Plates", stonePlateMaterial);
        rockDetailFilter = CreateMeshObject("Abyss Low Poly Rock Details", rockDetailMaterial);
        vegetationFilter = CreateMeshObject("Abyss Low Poly Vegetation", vegetationMaterial);
        waterfallFilter = CreateMeshObject("Abyss Low Poly Waterfalls", waterfallMaterial);
        cloudPuffFilter = CreateMeshObject("Abyss Cloud Puffs", cloudPuffMaterial);
    }

    private void CreateFogLayers()
    {
        fogLayers.Clear();
        if (fogLayerCount <= 0)
            return;

        int layerCount = Mathf.Clamp(fogLayerCount, 1, 18);

        for (int i = 0; i < layerCount; i++)
        {
            float t = layerCount <= 1 ? 0f : i / (float)(layerCount - 1);
            Texture2D texture = i % 2 == 0 ? fogTextureA : fogTextureB;
            Color layerColor = Color.Lerp(deepFogColor, distantMistColor, t);
            layerColor.a = Mathf.Clamp01(fogAlpha * Mathf.Lerp(0.8f, 1.18f, Mathf.Sin(t * Mathf.PI)));

            Material material = CreateTransparentMaterial("Abyss Fog Layer " + (i + 1), layerColor, texture, 3010 + i);
            float textureScaleX = Mathf.Lerp(1.6f, 4.6f, t);
            float textureScaleY = Mathf.Lerp(2.6f, 6.2f, 1f - t * 0.35f);
            SetTextureScale(material, new Vector2(textureScaleX, textureScaleY));

            MeshFilter filter = CreateMeshObject("Abyss Moving Fog " + (i + 1), material);
            FogLayer layer = new FogLayer
            {
                filter = filter,
                material = material,
                offset = new Vector2(t * 0.37f, t * 0.61f),
                speed = new Vector2(
                    Mathf.Lerp(0.018f, -0.045f, t) * fogDriftSpeed,
                    Mathf.Lerp(0.05f, 0.015f, t) * fogDriftSpeed)
            };

            fogLayers.Add(layer);
        }
    }

    private MeshFilter CreateMeshObject(string objectName, Material material)
    {
        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(visualRoot, false);

        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh
        {
            name = objectName + " Mesh"
        };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return filter;
    }

    private void RefreshEnvironment(bool force)
    {
        if (!initialized)
            InitializeRuntime();

        List<List<Vector3>> pathSegments = CollectPathSegments();
        List<Vector3> tileCenters = CollectTileCenters();
        int pathHash = ComputePathHash(pathSegments) * 31 + ComputeTileHash(tileCenters);

        if (!force && pathHash == lastPathHash)
            return;

        lastPathHash = pathHash;

        Bounds bounds = CalculateEnvironmentBounds(pathSegments);
        BuildAbyssBase(bounds);
        BuildTileIslandTopMesh(tileCenters, ridgeTopFilter);
        BuildTileIslandCliffMesh(tileCenters, leftCliffFilter);
        ClearMesh(rightCliffFilter);
        BuildTileIslandRimMesh(tileCenters, leftRimFilter);
        ClearMesh(rightRimFilter);
        BuildTileStoneDetailMesh(tileCenters, stonePlateFilter);
        BuildTileRockDetailMesh(tileCenters, rockDetailFilter);
        BuildTileVegetationMesh(tileCenters, vegetationFilter);
        BuildTileWaterfallMesh(tileCenters, waterfallFilter);
        BuildTileCloudHaloMesh(tileCenters, cloudPuffFilter);
        BuildFogMeshes(pathSegments);
    }

    private List<List<Vector3>> CollectPathSegments()
    {
        List<Vector3> rawPath = tileManager != null ? tileManager.GetWorldPath() : null;

        if (rawPath == null || rawPath.Count < 2)
            rawPath = CreateFallbackPath();

        List<Vector3> cleanPath = new List<Vector3>();
        for (int i = 0; i < rawPath.Count; i++)
        {
            Vector3 point = rawPath[i];
            point.y = 0f;

            if (cleanPath.Count > 0 && Vector3.Distance(cleanPath[cleanPath.Count - 1], point) < 0.01f)
                continue;

            cleanPath.Add(point);
        }

        if (cleanPath.Count < 2)
            cleanPath = CreateFallbackPath();

        List<List<Vector3>> segments = new List<List<Vector3>>();
        List<Vector3> currentSegment = new List<Vector3>();
        float safeTileSize = tileManager != null ? Mathf.Max(0.25f, tileManager.tileSize) : 1f;
        float maxGap = Mathf.Max(safeTileSize * 1.55f, maxContinuousPathGap);

        for (int i = 0; i < cleanPath.Count; i++)
        {
            if (currentSegment.Count > 0 && Vector3.Distance(currentSegment[currentSegment.Count - 1], cleanPath[i]) > maxGap)
            {
                AddSegmentIfValid(segments, currentSegment);
                currentSegment = new List<Vector3>();
            }

            currentSegment.Add(cleanPath[i]);
        }

        AddSegmentIfValid(segments, currentSegment);

        if (segments.Count == 0)
            AddSegmentIfValid(segments, CreateFallbackPath());

        return segments;
    }

    private List<Vector3> CollectTileCenters()
    {
        List<Vector3> rawPath = tileManager != null ? tileManager.GetWorldPath() : null;

        if (rawPath == null || rawPath.Count == 0)
            rawPath = CreateFallbackPath();

        List<Vector3> centers = new List<Vector3>();
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        float safeTileSize = GetSafeTileSize();

        for (int i = 0; i < rawPath.Count; i++)
        {
            AddUniqueTileCenter(centers, seen, rawPath[i], safeTileSize);
        }

        BuildTile[] buildTiles = FindObjectsByType<BuildTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buildTiles.Length; i++)
        {
            if (buildTiles[i] == null)
                continue;

            AddUniqueTileCenter(centers, seen, buildTiles[i].transform.position, safeTileSize);
        }

        TowerSupportTileEffect[] supportTiles = FindObjectsByType<TowerSupportTileEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < supportTiles.Length; i++)
        {
            if (supportTiles[i] == null)
                continue;

            AddUniqueTileCenter(centers, seen, supportTiles[i].transform.position, safeTileSize);
        }

        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < towers.Length; i++)
        {
            if (towers[i] == null)
                continue;

            if (towers[i].hasBuildGridPosition)
                AddUniqueTileCenter(centers, seen, new Vector3(towers[i].builtGridPosition.x * safeTileSize, 0f, towers[i].builtGridPosition.y * safeTileSize), safeTileSize);
        }

        if (centers.Count == 0)
            centers = CreateFallbackPath();

        return centers;
    }

    private void AddUniqueTileCenter(List<Vector3> centers, HashSet<Vector2Int> seen, Vector3 worldPosition, float tileSize)
    {
        Vector3 point = worldPosition;
        point.y = 0f;
        Vector2Int key = WorldToTileKey(point, tileSize);

        if (seen.Contains(key))
            return;

        seen.Add(key);
        centers.Add(new Vector3(key.x * tileSize, 0f, key.y * tileSize));
    }

    private float GetSafeTileSize()
    {
        return tileManager != null ? Mathf.Max(0.25f, tileManager.tileSize) : 1f;
    }

    private Vector2Int WorldToTileKey(Vector3 point, float tileSize)
    {
        float safeTileSize = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(point.x / safeTileSize),
            Mathf.RoundToInt(point.z / safeTileSize));
    }

    private Dictionary<Vector2Int, Vector3> BuildTileLookup(List<Vector3> tileCenters)
    {
        Dictionary<Vector2Int, Vector3> lookup = new Dictionary<Vector2Int, Vector3>();
        float safeTileSize = GetSafeTileSize();

        for (int i = 0; i < tileCenters.Count; i++)
        {
            Vector3 center = tileCenters[i];
            Vector2Int key = WorldToTileKey(center, safeTileSize);

            if (!lookup.ContainsKey(key))
                lookup.Add(key, new Vector3(key.x * safeTileSize, 0f, key.y * safeTileSize));
        }

        return lookup;
    }

    private void AddSegmentIfValid(List<List<Vector3>> segments, List<Vector3> rawSegment)
    {
        if (rawSegment == null || rawSegment.Count < 2)
            return;

        segments.Add(SmoothSegment(DensifySegment(ExtendSegmentEnds(rawSegment))));
    }

    private List<Vector3> ExtendSegmentEnds(List<Vector3> rawSegment)
    {
        if (rawSegment == null || rawSegment.Count < 2 || pathEndExtension <= 0f)
            return rawSegment;

        List<Vector3> extended = new List<Vector3>(rawSegment);
        Vector3 startDirection = extended[0] - extended[1];
        Vector3 endDirection = extended[extended.Count - 1] - extended[extended.Count - 2];
        startDirection.y = 0f;
        endDirection.y = 0f;

        if (startDirection.sqrMagnitude > 0.0001f)
            extended[0] += startDirection.normalized * pathEndExtension;

        if (endDirection.sqrMagnitude > 0.0001f)
            extended[extended.Count - 1] += endDirection.normalized * pathEndExtension;

        return extended;
    }

    private List<Vector3> CreateFallbackPath()
    {
        List<Vector3> fallback = new List<Vector3>();
        float safeTileSize = tileManager != null ? Mathf.Max(0.25f, tileManager.tileSize) : 1f;

        for (int i = 0; i <= 6; i++)
            fallback.Add(new Vector3(i * safeTileSize, 0f, 0f));

        return fallback;
    }

    private List<Vector3> DensifySegment(List<Vector3> rawSegment)
    {
        List<Vector3> dense = new List<Vector3>();
        float safeSpacing = Mathf.Max(0.12f, sampleSpacing);

        for (int i = 0; i < rawSegment.Count - 1; i++)
        {
            Vector3 from = rawSegment[i];
            Vector3 to = rawSegment[i + 1];
            float distance = Vector3.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / safeSpacing));

            for (int step = 0; step < steps; step++)
            {
                float t = step / (float)steps;
                dense.Add(Vector3.Lerp(from, to, t));
            }
        }

        dense.Add(rawSegment[rawSegment.Count - 1]);
        return dense;
    }

    private List<Vector3> SmoothSegment(List<Vector3> segment)
    {
        if (segment.Count < 4 || smoothingPasses <= 0 || cornerRoundness <= 0f)
            return segment;

        List<Vector3> current = new List<Vector3>(segment);
        int passes = Mathf.Clamp(smoothingPasses, 1, 5);
        float blend = Mathf.Clamp01(cornerRoundness);

        for (int pass = 0; pass < passes; pass++)
        {
            List<Vector3> smoothed = new List<Vector3>(current.Count);
            smoothed.Add(current[0]);

            for (int i = 1; i < current.Count - 1; i++)
            {
                Vector3 neighborAverage = (current[i - 1] + current[i + 1]) * 0.5f;
                smoothed.Add(Vector3.Lerp(current[i], neighborAverage, blend));
            }

            smoothed.Add(current[current.Count - 1]);
            current = smoothed;
        }

        return current;
    }

    private int ComputePathHash(List<List<Vector3>> segments)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + segments.Count;

            for (int s = 0; s < segments.Count; s++)
            {
                List<Vector3> segment = segments[s];
                hash = hash * 31 + segment.Count;

                for (int i = 0; i < segment.Count; i += Mathf.Max(1, segment.Count / 12))
                {
                    Vector3 point = segment[i];
                    hash = hash * 31 + Mathf.RoundToInt(point.x * 20f);
                    hash = hash * 31 + Mathf.RoundToInt(point.z * 20f);
                }
            }

            return hash;
        }
    }

    private int ComputeTileHash(List<Vector3> tileCenters)
    {
        unchecked
        {
            int hash = 23;
            hash = hash * 31 + tileCenters.Count;

            for (int i = 0; i < tileCenters.Count; i++)
            {
                Vector3 point = tileCenters[i];
                int x = Mathf.RoundToInt(point.x * 10f);
                int z = Mathf.RoundToInt(point.z * 10f);
                hash += (x * 73856093) ^ (z * 19349663);
            }

            return hash;
        }
    }

    private Bounds CalculateEnvironmentBounds(List<List<Vector3>> segments)
    {
        bool hasPoint = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            for (int i = 0; i < segment.Count; i++)
            {
                Vector3 point = segment[i];

                if (!hasPoint)
                {
                    min = point;
                    max = point;
                    hasPoint = true;
                }
                else
                {
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
            }
        }

        if (!hasPoint)
        {
            min = new Vector3(-6f, 0f, -6f);
            max = new Vector3(6f, 0f, 6f);
        }

        float padding = Mathf.Max(environmentPadding, cliffHalfWidth + 4f);
        min.x -= padding;
        min.z -= padding;
        max.x += padding;
        max.z += padding;

        Vector3 size = max - min;
        float minSize = Mathf.Max(8f, minimumEnvironmentSize);

        if (size.x < minSize)
        {
            float add = (minSize - size.x) * 0.5f;
            min.x -= add;
            max.x += add;
        }

        if (size.z < minSize)
        {
            float add = (minSize - size.z) * 0.5f;
            min.z -= add;
            max.z += add;
        }

        Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
        return bounds;
    }

    private void BuildAbyssBase(Bounds bounds)
    {
        const int gridResolution = 14;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int z = 0; z <= gridResolution; z++)
        {
            float zT = z / (float)gridResolution;
            float worldZ = Mathf.Lerp(min.z, max.z, zT);

            for (int x = 0; x <= gridResolution; x++)
            {
                float xT = x / (float)gridResolution;
                float worldX = Mathf.Lerp(min.x, max.x, xT);
                float noise = Mathf.PerlinNoise(worldX * 0.08f + 11.7f, worldZ * 0.08f - 3.1f);
                float y = abyssY - 0.08f + (noise - 0.5f) * 0.055f;

                vertices.Add(new Vector3(worldX, y, worldZ));
                uvs.Add(new Vector2(worldX * 0.018f, worldZ * 0.018f));
            }
        }

        int rowSize = gridResolution + 1;
        for (int z = 0; z < gridResolution; z++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                int a = z * rowSize + x;
                int b = a + 1;
                int c = a + rowSize;
                int d = c + 1;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        ApplyMesh(abyssFilter, vertices, triangles, uvs);
    }

    private void BuildTileIslandTopMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.78f;
        float topY = visualSurfaceY - 0.055f;
        int edgeIndex = 0;

        for (int i = 0; i < tileCenters.Count; i++)
            AddTileTopPlate(tileCenters[i], halfSize, topY, i, vertices, uvs, triangles);

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                AddTileShoulderShelf(tile.Value, direction, halfSize, topY, edgeIndex, vertices, uvs, triangles);
                edgeIndex++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildTileIslandCliffMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.82f;
        float topY = visualSurfaceY - 0.065f;
        int edgeIndex = 0;

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                AddTileCliffEdge(tile.Value, direction, halfSize, topY, edgeIndex, vertices, uvs, triangles);
                edgeIndex++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildTileIslandRimMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.82f;
        float rimWidth = Mathf.Max(0.06f, safeTileSize * 0.075f);
        float topY = visualSurfaceY - 0.035f;

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                AddTileRimEdge(tile.Value, direction, halfSize, rimWidth, topY, vertices, uvs, triangles);
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildTileStoneDetailMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.82f;
        float topY = visualSurfaceY - 0.022f;
        int detailIndex = 0;

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                float seed = GetDetailSeed(tile.Value, detailIndex, direction.x + direction.y, 131);
                if (Hash01(seed) < 0.91f)
                {
                    detailIndex++;
                    continue;
                }

                Vector3 normal = new Vector3(direction.x, 0f, direction.y);
                Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
                Vector3 center = tile.Value + normal * (halfSize * 0.64f) + tangent * ((Hash01(seed + 3.1f) - 0.5f) * safeTileSize * 0.52f);
                center.y = topY;

                AddStonePlate(
                    center,
                    tangent,
                    normal,
                    safeTileSize * Mathf.Lerp(0.12f, 0.22f, Hash01(seed + 1.2f)),
                    safeTileSize * Mathf.Lerp(0.045f, 0.085f, Hash01(seed + 2.4f)),
                    vertices,
                    uvs,
                    triangles);

                detailIndex++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildTileRockDetailMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.76f;
        int detailIndex = 0;

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                float seed = GetDetailSeed(tile.Value, detailIndex, direction.x + direction.y, 211);
                if (Hash01(seed) < 0.92f)
                {
                    detailIndex++;
                    continue;
                }

                Vector3 normal = new Vector3(direction.x, 0f, direction.y);
                Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
                Vector3 center = tile.Value + normal * (halfSize * 0.8f) + tangent * ((Hash01(seed + 4.2f) - 0.5f) * safeTileSize * 0.7f);
                center.y = visualSurfaceY - 0.015f;

                AddRockShard(
                    center,
                    safeTileSize * Mathf.Lerp(0.06f, 0.11f, Hash01(seed + 1.5f)),
                    safeTileSize * Mathf.Lerp(0.06f, 0.15f, Hash01(seed + 2.7f)),
                    Hash01(seed + 6.6f) * Mathf.PI * 2f,
                    vertices,
                    uvs,
                    triangles);

                detailIndex++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildTileVegetationMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.74f;
        int detailIndex = 0;

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                float seed = GetDetailSeed(tile.Value, detailIndex, direction.x + direction.y, 307);
                if (Hash01(seed) < 0.9f)
                {
                    detailIndex++;
                    continue;
                }

                Vector3 normal = new Vector3(direction.x, 0f, direction.y);
                Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
                Vector3 center = tile.Value + normal * (halfSize * 0.62f) + tangent * ((Hash01(seed + 2.8f) - 0.5f) * safeTileSize * 0.58f);
                center.y = visualSurfaceY - 0.01f;

                AddLowPolyPine(center, safeTileSize * Mathf.Lerp(0.15f, 0.26f, Hash01(seed + 5.8f)), vertices, uvs, triangles);
                detailIndex++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildTileWaterfallMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.73f;
        int placed = 0;
        int edgeIndex = 0;
        int maxCount = Mathf.Clamp(waterfallMaxCount, 0, 6);

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                if (placed >= maxCount)
                    break;

                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                float seed = GetDetailSeed(tile.Value, edgeIndex, direction.x + direction.y, 401);
                if (placed > 0 && Hash01(seed) < 0.74f)
                {
                    edgeIndex++;
                    continue;
                }

                AddTileWaterfallEdge(tile.Value, direction, halfSize, seed, vertices, uvs, triangles);
                placed++;
                edgeIndex++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildTileCloudHaloMesh(List<Vector3> tileCenters, MeshFilter filter)
    {
        if (cloudRowsPerSide <= 0)
        {
            ClearMesh(filter);
            return;
        }

        Dictionary<Vector2Int, Vector3> lookup = BuildTileLookup(tileCenters);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float safeTileSize = GetSafeTileSize();
        float halfSize = safeTileSize * 0.86f;
        int edgeIndex = 0;

        foreach (KeyValuePair<Vector2Int, Vector3> tile in lookup)
        {
            for (int i = 0; i < TileDirections.Length; i++)
            {
                Vector2Int direction = TileDirections[i];
                if (lookup.ContainsKey(tile.Key + direction))
                    continue;

                Vector3 normal = new Vector3(direction.x, 0f, direction.y);
                Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
                float seed = GetDetailSeed(tile.Value, edgeIndex, direction.x + direction.y, 503);

                int rowCount = Mathf.Clamp(cloudRowsPerSide, 1, 4);
                for (int row = 0; row < rowCount; row++)
                {
                    float rowT = row / Mathf.Max(1f, rowCount - 1f);
                    if (Hash01(seed + row * 17.31f + 6.2f) < Mathf.Lerp(0.08f, 0.2f, rowT))
                        continue;

                    Vector3 center = tile.Value
                        + normal * (halfSize + safeTileSize * Mathf.Lerp(0.72f, 2.0f, rowT))
                        + tangent * ((Hash01(seed + row * 9.7f) - 0.5f) * safeTileSize * Mathf.Lerp(0.8f, 1.7f, rowT));
                    center.y = cloudBaseY - rowT * 0.14f + (Hash01(seed + row * 2.6f) - 0.5f) * cloudHeight * 0.12f;

                    float radiusX = safeTileSize * cloudPuffRadius * Mathf.Lerp(0.7f, 1.22f, rowT) * Mathf.Lerp(0.8f, 1.22f, Hash01(seed + row * 3.3f));
                    float radiusZ = safeTileSize * cloudPuffRadius * Mathf.Lerp(0.54f, 1.02f, rowT) * Mathf.Lerp(0.82f, 1.18f, Hash01(seed + row * 4.9f));
                    AddFlatCloudBlob(center, tangent, normal, radiusX, radiusZ, vertices, uvs, triangles);
                }

                edgeIndex++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildRidgeTopMesh(List<List<Vector3>> segments, MeshFilter filter)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            AddRidgeShoulderStrip(segment, -1, vertices, uvs, triangles);
            AddRidgeShoulderStrip(segment, 1, vertices, uvs, triangles);
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void AddRidgeShoulderStrip(
        List<Vector3> segment,
        int side,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        if (segment == null || segment.Count < 2)
            return;

        int segmentStart = vertices.Count;
        float innerDistance = Mathf.Max(0.34f, pathBridgeHalfWidth * 0.66f);

        for (int i = 0; i < segment.Count; i++)
        {
            Vector3 point = segment[i];
            Vector3 normal = GetSideNormal(segment, i, side);
            float edgeNoise = GetIslandEdgeNoise(point, i, side);
            float outerDistance = Mathf.Max(pathBridgeHalfWidth + 0.28f, islandHalfWidth + edgeNoise);
            float surfaceNoise = (Mathf.PerlinNoise(point.x * 0.19f + 4.7f, point.z * 0.19f - 1.3f) - 0.5f) * islandSurfaceNoiseAmplitude;

            Vector3 inner = point + normal * innerDistance;
            Vector3 outer = point + normal * outerDistance;
            inner.y = visualSurfaceY + surfaceNoise + 0.006f;
            outer.y = visualSurfaceY + surfaceNoise - 0.006f;

            vertices.Add(inner);
            vertices.Add(outer);

            float v = i / Mathf.Max(1f, segment.Count - 1f);
            uvs.Add(new Vector2(0f, v * 4f));
            uvs.Add(new Vector2(1f, v * 4f));
        }

        for (int i = 0; i < segment.Count - 1; i++)
        {
            int a = segmentStart + i * 2;
            int b = a + 2;
            AddQuad(triangles, a, b, a + 1, b + 1);
        }
    }

    private void BuildCliffMesh(List<List<Vector3>> segments, int side, MeshFilter filter)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            int segmentStart = vertices.Count;

            for (int i = 0; i < segment.Count; i++)
            {
                Vector3 normal = GetSideNormal(segment, i, side);
                Vector3 point = segment[i];
                float edgeNoise = GetIslandEdgeNoise(point, i, side);
                float topWidth = Mathf.Max(pathBridgeHalfWidth + 0.28f, islandHalfWidth + edgeNoise);
                float middleWidth = topWidth + cliffOutset * 0.45f + GetRockStepNoise(point, i, side) * 0.24f;
                float bottomWidth = topWidth + cliffOutset + Mathf.Abs(GetRockStepNoise(point, i + 11, side)) * 0.52f;

                Vector3 top = point + normal * topWidth;
                Vector3 mid = point + normal * middleWidth;
                Vector3 bottom = point + normal * bottomWidth;

                top.y = visualSurfaceY - 0.02f;
                mid.y = cliffMidY + GetRockStepNoise(point, i + 23, side) * rockStepHeight;
                bottom.y = abyssY + 0.22f + GetRockStepNoise(point, i + 41, side) * rockStepHeight * 1.2f;

                vertices.Add(top);
                vertices.Add(mid);
                vertices.Add(bottom);

                float v = i / Mathf.Max(1f, segment.Count - 1f);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(0.45f, v));
                uvs.Add(new Vector2(1f, v));
            }

            for (int i = 0; i < segment.Count - 1; i++)
            {
                int a = segmentStart + i * 3;
                int b = a + 3;

                AddQuad(triangles, a, b, a + 1, b + 1);
                AddQuad(triangles, a + 1, b + 1, a + 2, b + 2);
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildRimMesh(List<List<Vector3>> segments, int side, MeshFilter filter)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float rimWidth = Mathf.Max(0.14f, pathBridgeHalfWidth * 0.22f);

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            int segmentStart = vertices.Count;

            for (int i = 0; i < segment.Count; i++)
            {
                Vector3 normal = GetSideNormal(segment, i, side);
                Vector3 point = segment[i];
                float edgeNoise = GetIslandEdgeNoise(point, i, side);
                float ridgeEdge = Mathf.Max(pathBridgeHalfWidth + 0.28f, islandHalfWidth + edgeNoise);

                Vector3 inner = point + normal * Mathf.Max(0.2f, ridgeEdge - rimWidth * 0.4f);
                Vector3 outer = point + normal * Mathf.Max(0.3f, ridgeEdge + rimWidth);

                inner.y = visualSurfaceY + rimLift * 0.65f;
                outer.y = cliffTopY + rimLift * 0.55f;

                vertices.Add(inner);
                vertices.Add(outer);

                float v = i / Mathf.Max(1f, segment.Count - 1f);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
            }

            for (int i = 0; i < segment.Count - 1; i++)
            {
                int a = segmentStart + i * 2;
                int b = a + 2;
                AddQuad(triangles, a, b, a + 1, b + 1);
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildStonePlateMesh(List<List<Vector3>> segments, MeshFilter filter)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        int pointStep = Mathf.Max(4, Mathf.RoundToInt(Mathf.Max(0.5f, detailSpacing) / Mathf.Max(0.08f, sampleSpacing)));

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            if (segment == null || segment.Count < pointStep * 2)
                continue;

            for (int i = pointStep; i < segment.Count - pointStep; i += pointStep)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 point = segment[i];
                    float seed = GetDetailSeed(point, i, side, 13);
                    if (Hash01(seed) < 0.96f)
                        continue;

                    if (!TryGetDetailPoint(segment, i, side, seed, 0.04f, out Vector3 center, out Vector3 tangent, out Vector3 normal))
                        continue;

                    float length = stonePlateScale * Mathf.Lerp(0.7f, 1.28f, Hash01(seed + 1.8f));
                    float width = stonePlateScale * Mathf.Lerp(0.42f, 0.92f, Hash01(seed + 2.6f));
                    center.y = visualSurfaceY + 0.026f;
                    AddStonePlate(center, tangent, normal, length, width, vertices, uvs, triangles);
                }
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildRockDetailMesh(List<List<Vector3>> segments, MeshFilter filter)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        int pointStep = Mathf.Max(6, Mathf.RoundToInt(Mathf.Max(0.8f, detailSpacing * 1.55f) / Mathf.Max(0.08f, sampleSpacing)));

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            if (segment == null || segment.Count < pointStep * 2)
                continue;

            for (int i = pointStep; i < segment.Count - pointStep; i += pointStep)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 point = segment[i];
                    float seed = GetDetailSeed(point, i, side, 41);
                    if (Hash01(seed) < 0.985f)
                        continue;

                    if (!TryGetDetailPoint(segment, i, side, seed, 0.22f, out Vector3 center, out Vector3 tangent, out Vector3 normal))
                        continue;

                    int count = 1 + Mathf.FloorToInt(Hash01(seed + 6.1f) * 3f);
                    for (int r = 0; r < count; r++)
                    {
                        float localSeed = seed + r * 17.31f;
                        Vector3 local = center
                            + tangent * ((Hash01(localSeed + 1.4f) - 0.5f) * 0.58f)
                            + normal * ((Hash01(localSeed + 2.7f) - 0.5f) * 0.46f);
                        local.y = visualSurfaceY + 0.02f;

                        float size = rockDetailScale * Mathf.Lerp(0.62f, 1.2f, Hash01(localSeed + 3.2f));
                        float height = rockDetailScale * Mathf.Lerp(0.34f, 0.95f, Hash01(localSeed + 4.9f));
                        float rotation = Hash01(localSeed + 5.8f) * Mathf.PI * 2f;
                        AddRockShard(local, size, height, rotation, vertices, uvs, triangles);
                    }
                }
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildVegetationMesh(List<List<Vector3>> segments, MeshFilter filter)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        int pointStep = Mathf.Max(7, Mathf.RoundToInt(Mathf.Max(0.9f, detailSpacing * 1.85f) / Mathf.Max(0.08f, sampleSpacing)));

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            if (segment == null || segment.Count < pointStep * 2)
                continue;

            for (int i = pointStep; i < segment.Count - pointStep; i += pointStep)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 point = segment[i];
                    float seed = GetDetailSeed(point, i, side, 73);
                    if (Hash01(seed) < 0.992f)
                        continue;

                    if (!TryGetDetailPoint(segment, i, side, seed, 0.18f, out Vector3 center, out Vector3 tangent, out Vector3 normal))
                        continue;

                    center += normal * 0.16f;
                    center.y = visualSurfaceY + 0.028f;

                    float scale = vegetationScale * Mathf.Lerp(0.68f, 1.25f, Hash01(seed + 7.4f));
                    AddLowPolyPine(center, scale, vertices, uvs, triangles);
                }
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildWaterfallMesh(List<List<Vector3>> segments, MeshFilter filter)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        int targetCount = Mathf.Clamp(waterfallMaxCount, 0, 8);
        int placed = 0;
        int minimumSpacing = Mathf.Max(10, Mathf.RoundToInt(4.2f / Mathf.Max(0.08f, sampleSpacing)));

        for (int s = 0; s < segments.Count && placed < targetCount; s++)
        {
            List<Vector3> segment = segments[s];
            if (segment == null || segment.Count < minimumSpacing * 2)
                continue;

            for (int i = minimumSpacing; i < segment.Count - minimumSpacing && placed < targetCount; i += minimumSpacing)
            {
                Vector3 point = segment[i];
                int side = Hash01(GetDetailSeed(point, i, 1, 91)) > 0.5f ? 1 : -1;
                float seed = GetDetailSeed(point, i, side, 97);

                if (placed >= 2 && Hash01(seed) < 0.42f)
                    continue;

                AddWaterfallRibbon(segment, i, side, seed, vertices, uvs, triangles);
                placed++;
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void BuildFogMeshes(List<List<Vector3>> segments)
    {
        for (int i = 0; i < fogLayers.Count; i++)
        {
            FogLayer layer = fogLayers[i];
            float t = fogLayers.Count <= 1 ? 0f : i / (float)(fogLayers.Count - 1);
            float innerDistance = pathBridgeHalfWidth + Mathf.Lerp(fogInnerOffset, fogInnerOffset + 0.55f, t);
            float outerWidth = Mathf.Lerp(fogNearWidth, fogFarWidth, Mathf.SmoothStep(0f, 1f, t));
            float y = fogBaseY + i * 0.0042f;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int s = 0; s < segments.Count; s++)
            {
                AddFogRibbon(segments[s], -1, innerDistance, outerWidth, y, t, i, vertices, uvs, triangles);
                AddFogRibbon(segments[s], 1, innerDistance, outerWidth, y, t, i, vertices, uvs, triangles);
            }

            ApplyMesh(layer.filter, vertices, triangles, uvs);
        }
    }

    private void AddFogRibbon(
        List<Vector3> segment,
        int side,
        float innerDistance,
        float outerWidth,
        float baseY,
        float layerT,
        int layerIndex,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        if (segment == null || segment.Count < 2)
            return;

        int segmentStart = vertices.Count;

        for (int i = 0; i < segment.Count; i++)
        {
            Vector3 point = segment[i];
            Vector3 normal = GetSideNormal(segment, i, side);
            float fogNoise = GetFogNoise(point, i, side, layerIndex);
            float inner = Mathf.Max(0.25f, innerDistance + fogNoise * 0.16f);
            float outer = inner + Mathf.Max(0.6f, outerWidth + fogNoise * fogEdgeNoiseAmplitude);
            float wave = Mathf.Sin(point.x * 0.22f + point.z * 0.17f + layerIndex * 0.74f) * Mathf.Lerp(0.006f, 0.018f, layerT);

            Vector3 innerPoint = point + normal * inner;
            Vector3 outerPoint = point + normal * outer;
            innerPoint.y = baseY + wave;
            outerPoint.y = baseY + wave * 0.55f + Mathf.Lerp(0.004f, 0.018f, layerT);

            vertices.Add(innerPoint);
            vertices.Add(outerPoint);

            float v = i * sampleSpacing * Mathf.Lerp(0.35f, 0.7f, layerT);
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(Mathf.Lerp(1.4f, 3.6f, layerT), v));
        }

        for (int i = 0; i < segment.Count - 1; i++)
        {
            int a = segmentStart + i * 2;
            int b = a + 2;
            AddQuad(triangles, a, b, a + 1, b + 1);
        }
    }

    private void BuildCloudPuffMesh(List<List<Vector3>> segments, MeshFilter filter)
    {
        if (cloudRowsPerSide <= 0)
        {
            ClearMesh(filter);
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        int rows = Mathf.Clamp(cloudRowsPerSide, 1, 7);
        int pointStep = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(0.3f, cloudPuffSpacing) / Mathf.Max(0.08f, sampleSpacing)));

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            if (segment == null || segment.Count < 2)
                continue;

            for (int i = 0; i < segment.Count; i += pointStep)
            {
                Vector3 point = segment[i];
                Vector3 tangent = GetTangent(segment, i);

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 normal = GetSideNormal(segment, i, side);

                    for (int row = 0; row < rows; row++)
                    {
                        float rowT = rows <= 1 ? 0f : row / (float)(rows - 1);
                        float seed = GetCloudSeed(point, i, side, row);
                        float skipPattern = Mathf.PerlinNoise(seed * 0.73f + row * 1.7f, seed * 0.21f + side * 2.1f);
                        if (row > 0 && skipPattern < 0.24f)
                            continue;

                        float alongJitter = (Mathf.PerlinNoise(seed * 0.17f, seed * 0.31f) - 0.5f) * Mathf.Lerp(0.65f, 1.55f, rowT);
                        float sideJitter = (Mathf.PerlinNoise(seed * 0.41f + 3.1f, seed * 0.29f - 5.4f) - 0.5f) * 0.75f;
                        float distance = islandHalfWidth + cloudInnerDistance + row * cloudRowSpacing + sideJitter;
                        Vector3 center = point + normal * distance + tangent * alongJitter;

                        float radius = cloudPuffRadius * Mathf.Lerp(1.05f, 1.85f, rowT) * Mathf.Lerp(0.78f, 1.22f, skipPattern);
                        float squash = Mathf.Lerp(0.7f, 1.35f, Mathf.PerlinNoise(seed * 0.13f + 6.2f, seed * 0.37f));
                        float height = cloudHeight * Mathf.Lerp(0.75f, 1.25f, skipPattern);
                        center.y = cloudBaseY + rowT * -0.08f + (skipPattern - 0.5f) * 0.08f;

                        AddCloudDome(center, radius * squash, radius, height, vertices, uvs, triangles);
                    }
                }
            }
        }

        ApplyMesh(filter, vertices, triangles, uvs);
    }

    private void AddCloudDome(
        Vector3 center,
        float radiusX,
        float radiusZ,
        float height,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        const int sides = 10;
        int start = vertices.Count;

        vertices.Add(center + Vector3.up * height);
        uvs.Add(new Vector2(0.5f, 1f));

        for (int ring = 0; ring < 2; ring++)
        {
            float ringT = ring == 0 ? 0.48f : 1f;
            float y = center.y + height * (ring == 0 ? 0.54f : 0.08f);

            for (int i = 0; i < sides; i++)
            {
                float angle = (i / (float)sides) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radiusX * ringT;
                float z = Mathf.Sin(angle) * radiusZ * ringT;
                vertices.Add(new Vector3(center.x + x, y, center.z + z));
                uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }
        }

        int middleStart = start + 1;
        int outerStart = middleStart + sides;

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;

            triangles.Add(start);
            triangles.Add(middleStart + i);
            triangles.Add(middleStart + next);

            AddQuad(triangles, middleStart + i, middleStart + next, outerStart + i, outerStart + next);
        }
    }

    private void AddTileTopPlate(
        Vector3 center,
        float halfSize,
        float topY,
        int index,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        const int sides = 8;
        int start = vertices.Count;
        float chamfer = halfSize * Mathf.Lerp(0.18f, 0.29f, Hash01(index * 19.37f + 4.2f));
        float wobble = halfSize * 0.055f;

        vertices.Add(new Vector3(center.x, topY + islandSurfaceNoiseAmplitude, center.z));
        uvs.Add(new Vector2(0.5f, 0.5f));

        Vector2[] ring =
        {
            new Vector2(-halfSize + chamfer, -halfSize),
            new Vector2(halfSize - chamfer, -halfSize),
            new Vector2(halfSize, -halfSize + chamfer),
            new Vector2(halfSize, halfSize - chamfer),
            new Vector2(halfSize - chamfer, halfSize),
            new Vector2(-halfSize + chamfer, halfSize),
            new Vector2(-halfSize, halfSize - chamfer),
            new Vector2(-halfSize, -halfSize + chamfer)
        };

        for (int i = 0; i < sides; i++)
        {
            float seed = index * 37.13f + i * 5.91f;
            Vector2 local = ring[i];
            local.x += (Hash01(seed + 1.3f) - 0.5f) * wobble;
            local.y += (Hash01(seed + 2.8f) - 0.5f) * wobble;

            float height = topY + (Hash01(seed + 7.4f) - 0.5f) * islandSurfaceNoiseAmplitude;
            vertices.Add(new Vector3(center.x + local.x, height, center.z + local.y));
            uvs.Add(new Vector2(local.x / (halfSize * 2f) + 0.5f, local.y / (halfSize * 2f) + 0.5f));
        }

        for (int i = 0; i < sides; i++)
        {
            int current = start + 1 + i;
            int next = start + 1 + ((i + 1) % sides);
            triangles.Add(start);
            triangles.Add(next);
            triangles.Add(current);
        }
    }

    private void AddTileShoulderShelf(
        Vector3 center,
        Vector2Int direction,
        float halfSize,
        float topY,
        int edgeIndex,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        const int segments = 3;
        Vector3 normal = new Vector3(direction.x, 0f, direction.y);
        Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
        float edgeSeed = center.x * 23.37f + center.z * 41.11f + edgeIndex * 13.19f;

        for (int segment = 0; segment < segments; segment++)
        {
            float tA = segment / (float)segments;
            float tB = (segment + 1) / (float)segments;
            float alongA = Mathf.Lerp(-halfSize, halfSize, tA);
            float alongB = Mathf.Lerp(-halfSize, halfSize, tB);
            float seedA = edgeSeed + segment * 7.31f;
            float seedB = edgeSeed + (segment + 1) * 7.31f;
            float shelfDepthA = halfSize * Mathf.Lerp(0.24f, 0.58f, Hash01(seedA + 1.7f));
            float shelfDepthB = halfSize * Mathf.Lerp(0.24f, 0.58f, Hash01(seedB + 1.7f));
            float jitterA = (Hash01(seedA + 2.8f) - 0.5f) * halfSize * 0.18f;
            float jitterB = (Hash01(seedB + 2.8f) - 0.5f) * halfSize * 0.18f;

            Vector3 innerA = center + normal * (halfSize * 0.7f) + tangent * (alongA + jitterA * 0.35f);
            Vector3 innerB = center + normal * (halfSize * 0.7f) + tangent * (alongB + jitterB * 0.35f);
            Vector3 outerA = center + normal * (halfSize + shelfDepthA) + tangent * (alongA + jitterA);
            Vector3 outerB = center + normal * (halfSize + shelfDepthB) + tangent * (alongB + jitterB);

            innerA.y = topY - 0.002f;
            innerB.y = topY + (Hash01(seedB + 3.9f) - 0.5f) * islandSurfaceNoiseAmplitude;
            outerA.y = topY - Mathf.Lerp(0.01f, 0.045f, Hash01(seedA + 4.5f));
            outerB.y = topY - Mathf.Lerp(0.01f, 0.045f, Hash01(seedB + 4.5f));

            int start = vertices.Count;
            vertices.Add(innerA);
            vertices.Add(innerB);
            vertices.Add(outerA);
            vertices.Add(outerB);

            uvs.Add(new Vector2(tA, 0.18f));
            uvs.Add(new Vector2(tB, 0.18f));
            uvs.Add(new Vector2(tA, 1f));
            uvs.Add(new Vector2(tB, 1f));

            AddQuad(triangles, start, start + 1, start + 2, start + 3);
        }
    }

    private void AddTileCliffEdge(
        Vector3 center,
        Vector2Int direction,
        float halfSize,
        float topY,
        int edgeIndex,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        const int segments = 3;
        Vector3 normal = new Vector3(direction.x, 0f, direction.y);
        Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
        float edgeSeed = edgeIndex * 29.71f + center.x * 3.1f + center.z * 1.7f;
        float baseDepth = cliffOutset * Mathf.Lerp(0.82f, 1.28f, Hash01(edgeSeed + 2.6f));

        for (int segment = 0; segment < segments; segment++)
        {
            float tA = segment / (float)segments;
            float tB = (segment + 1) / (float)segments;
            float alongA = Mathf.Lerp(-halfSize, halfSize, tA);
            float alongB = Mathf.Lerp(-halfSize, halfSize, tB);

            float seedA = edgeSeed + segment * 11.43f;
            float seedB = edgeSeed + (segment + 1) * 11.43f;
            float chipA = (Hash01(seedA + 1.7f) - 0.5f) * halfSize * 0.16f;
            float chipB = (Hash01(seedB + 1.7f) - 0.5f) * halfSize * 0.16f;
            float depthA = baseDepth * Mathf.Lerp(0.82f, 1.12f, Hash01(seedA + 4.1f));
            float depthB = baseDepth * Mathf.Lerp(0.82f, 1.12f, Hash01(seedB + 4.1f));

            Vector3 topA = center + normal * (halfSize + chipA * 0.18f) + tangent * (alongA + chipA);
            Vector3 topB = center + normal * (halfSize + chipB * 0.18f) + tangent * (alongB + chipB);
            Vector3 midA = topA + normal * (depthA * 0.42f) + tangent * chipA * 0.28f;
            Vector3 midB = topB + normal * (depthB * 0.42f) - tangent * chipB * 0.24f;
            Vector3 bottomA = topA + normal * depthA + tangent * chipA * 0.42f;
            Vector3 bottomB = topB + normal * depthB - tangent * chipB * 0.32f;

            topA.y = topY + (Hash01(seedA + 8.2f) - 0.5f) * rockStepHeight * 0.08f;
            topB.y = topY + (Hash01(seedB + 8.2f) - 0.5f) * rockStepHeight * 0.08f;
            midA.y = cliffMidY + Hash01(seedA + 7.2f) * rockStepHeight * 0.42f;
            midB.y = cliffMidY + Hash01(seedB + 9.8f) * rockStepHeight * 0.42f;
            bottomA.y = abyssY + 0.32f + Hash01(seedA + 3.5f) * rockStepHeight * 0.7f;
            bottomB.y = abyssY + 0.26f + Hash01(seedB + 5.4f) * rockStepHeight * 0.7f;

            int start = vertices.Count;
            vertices.Add(topA);
            vertices.Add(topB);
            vertices.Add(midA);
            vertices.Add(midB);
            vertices.Add(bottomA);
            vertices.Add(bottomB);

            uvs.Add(new Vector2(tA, 0f));
            uvs.Add(new Vector2(tB, 0f));
            uvs.Add(new Vector2(tA, 0.48f));
            uvs.Add(new Vector2(tB, 0.48f));
            uvs.Add(new Vector2(tA, 1f));
            uvs.Add(new Vector2(tB, 1f));

            AddQuad(triangles, start, start + 1, start + 2, start + 3);
            AddQuad(triangles, start + 2, start + 3, start + 4, start + 5);
        }
    }

    private void AddTileRimEdge(
        Vector3 center,
        Vector2Int direction,
        float halfSize,
        float rimWidth,
        float topY,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        const int segments = 3;
        Vector3 normal = new Vector3(direction.x, 0f, direction.y);
        Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
        float edgeSeed = center.x * 17.31f + center.z * 9.97f + direction.x * 31.7f + direction.y * 43.9f;

        for (int segment = 0; segment < segments; segment++)
        {
            float tA = segment / (float)segments;
            float tB = (segment + 1) / (float)segments;
            float alongA = Mathf.Lerp(-halfSize, halfSize, tA);
            float alongB = Mathf.Lerp(-halfSize, halfSize, tB);
            float seedA = edgeSeed + segment * 5.17f;
            float seedB = edgeSeed + (segment + 1) * 5.17f;
            float jitterA = (Hash01(seedA + 1.2f) - 0.5f) * halfSize * 0.075f;
            float jitterB = (Hash01(seedB + 1.2f) - 0.5f) * halfSize * 0.075f;
            float widthA = rimWidth * Mathf.Lerp(0.82f, 1.35f, Hash01(seedA + 2.4f));
            float widthB = rimWidth * Mathf.Lerp(0.82f, 1.35f, Hash01(seedB + 2.4f));

            Vector3 outerA = center + normal * (halfSize + jitterA * 0.2f) + tangent * (alongA + jitterA);
            Vector3 outerB = center + normal * (halfSize + jitterB * 0.2f) + tangent * (alongB + jitterB);
            Vector3 innerA = outerA - normal * widthA;
            Vector3 innerB = outerB - normal * widthB;

            outerA.y = topY + (Hash01(seedA + 3.6f) - 0.5f) * rimLift;
            outerB.y = topY + (Hash01(seedB + 3.6f) - 0.5f) * rimLift;
            innerA.y = topY + rimLift;
            innerB.y = topY + rimLift;

            int start = vertices.Count;
            vertices.Add(innerA);
            vertices.Add(innerB);
            vertices.Add(outerA);
            vertices.Add(outerB);

            uvs.Add(new Vector2(tA, 0f));
            uvs.Add(new Vector2(tB, 0f));
            uvs.Add(new Vector2(tA, 1f));
            uvs.Add(new Vector2(tB, 1f));

            AddQuad(triangles, start, start + 1, start + 2, start + 3);
        }
    }

    private void AddTileWaterfallEdge(
        Vector3 center,
        Vector2Int direction,
        float halfSize,
        float seed,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        Vector3 normal = new Vector3(direction.x, 0f, direction.y);
        Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
        float offset = (Hash01(seed + 2.4f) - 0.5f) * halfSize * 0.85f;
        float width = Mathf.Max(0.04f, waterfallWidth * GetSafeTileSize() * Mathf.Lerp(0.7f, 1.15f, Hash01(seed + 5.7f)));
        Vector3 edgeCenter = center + normal * (halfSize + 0.015f) + tangent * offset;
        Vector3 halfWidth = tangent * width;

        Vector3 topA = edgeCenter - halfWidth;
        Vector3 topB = edgeCenter + halfWidth;
        Vector3 bottomA = topA + normal * (cliffOutset * 0.58f);
        Vector3 bottomB = topB + normal * (cliffOutset * 0.58f);

        topA.y = visualSurfaceY - 0.018f;
        topB.y = visualSurfaceY - 0.018f;
        bottomA.y = visualSurfaceY - waterfallDropDepth;
        bottomB.y = visualSurfaceY - waterfallDropDepth * 0.96f;

        int start = vertices.Count;
        vertices.Add(topA);
        vertices.Add(topB);
        vertices.Add(bottomA);
        vertices.Add(bottomB);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));

        AddQuad(triangles, start, start + 1, start + 2, start + 3);
    }

    private void AddFlatCloudBlob(
        Vector3 center,
        Vector3 tangent,
        Vector3 normal,
        float radiusX,
        float radiusZ,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        const int sides = 20;
        int start = vertices.Count;
        vertices.Add(center);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f;
            float wobble = Mathf.Lerp(0.9f, 1.1f, Hash01(center.x * 11.3f + center.z * 4.7f + i * 2.19f));
            Vector3 point = center
                + tangent.normalized * (Mathf.Cos(angle) * radiusX * wobble)
                + normal.normalized * (Mathf.Sin(angle) * radiusZ * wobble);
            point.y = center.y + Mathf.Sin(angle * 2.0f) * 0.01f;

            vertices.Add(point);
            uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
        }

        for (int i = 0; i < sides; i++)
        {
            int current = start + 1 + i;
            int next = start + 1 + ((i + 1) % sides);
            triangles.Add(start);
            triangles.Add(next);
            triangles.Add(current);
        }
    }

    private bool TryGetDetailPoint(
        List<Vector3> segment,
        int index,
        int side,
        float seed,
        float innerExtra,
        out Vector3 center,
        out Vector3 tangent,
        out Vector3 normal)
    {
        center = Vector3.zero;
        tangent = GetTangent(segment, index);
        normal = GetSideNormal(segment, index, side);

        Vector3 point = segment[index];
        float edgeNoise = GetIslandEdgeNoise(point, index, side);
        float outerDistance = islandHalfWidth + edgeNoise - Mathf.Max(0.05f, detailEdgeInset);
        float innerDistance = pathBridgeHalfWidth + Mathf.Max(0.2f, detailInnerDistance + innerExtra);

        if (outerDistance <= innerDistance + 0.08f)
            return false;

        float distance = Mathf.Lerp(innerDistance, outerDistance, Hash01(seed + 0.77f));
        float along = (Hash01(seed + 1.91f) - 0.5f) * 0.58f;
        center = point + normal * distance + tangent * along;
        return true;
    }

    private void AddStonePlate(
        Vector3 center,
        Vector3 tangent,
        Vector3 normal,
        float length,
        float width,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int start = vertices.Count;
        Vector3 halfLength = tangent.normalized * (length * 0.5f);
        Vector3 halfWidth = normal.normalized * (width * 0.5f);

        vertices.Add(center - halfLength - halfWidth);
        vertices.Add(center + halfLength - halfWidth);
        vertices.Add(center - halfLength + halfWidth);
        vertices.Add(center + halfLength + halfWidth);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));

        AddQuad(triangles, start, start + 1, start + 2, start + 3);
    }

    private void AddRockShard(
        Vector3 center,
        float size,
        float height,
        float rotation,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int start = vertices.Count;
        Vector3 axisA = new Vector3(Mathf.Cos(rotation), 0f, Mathf.Sin(rotation));
        Vector3 axisB = new Vector3(-axisA.z, 0f, axisA.x);
        float halfA = size * 0.52f;
        float halfB = size * 0.38f;
        Vector3 top = center + axisA * (size * 0.12f) + axisB * (size * 0.06f) + Vector3.up * height;

        vertices.Add(center - axisA * halfA - axisB * halfB);
        vertices.Add(center + axisA * halfA - axisB * halfB * 0.82f);
        vertices.Add(center + axisA * halfA * 0.82f + axisB * halfB);
        vertices.Add(center - axisA * halfA * 0.75f + axisB * halfB * 0.9f);
        vertices.Add(top);

        for (int i = 0; i < 5; i++)
            uvs.Add(new Vector2(i * 0.23f, i == 4 ? 1f : 0f));

        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 4);

        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 4);

        triangles.Add(start + 2);
        triangles.Add(start + 3);
        triangles.Add(start + 4);

        triangles.Add(start + 3);
        triangles.Add(start);
        triangles.Add(start + 4);
    }

    private void AddLowPolyPine(
        Vector3 center,
        float scale,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        AddCone(center + Vector3.up * (scale * 0.05f), scale * 0.52f, scale * 0.96f, 5, vertices, uvs, triangles);
        AddCone(center + Vector3.up * (scale * 0.38f), scale * 0.38f, scale * 0.78f, 5, vertices, uvs, triangles);
    }

    private void AddCone(
        Vector3 baseCenter,
        float radius,
        float height,
        int sides,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int start = vertices.Count;
        sides = Mathf.Clamp(sides, 4, 9);

        vertices.Add(baseCenter + Vector3.up * height);
        uvs.Add(new Vector2(0.5f, 1f));

        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f;
            vertices.Add(baseCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
        }

        for (int i = 0; i < sides; i++)
        {
            int current = start + 1 + i;
            int next = start + 1 + ((i + 1) % sides);
            triangles.Add(start);
            triangles.Add(current);
            triangles.Add(next);
        }
    }

    private void AddWaterfallRibbon(
        List<Vector3> segment,
        int index,
        int side,
        float seed,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        Vector3 point = segment[index];
        Vector3 tangent = GetTangent(segment, index);
        Vector3 normal = GetSideNormal(segment, index, side);
        float edgeNoise = GetIslandEdgeNoise(point, index, side);
        float edgeDistance = islandHalfWidth + edgeNoise + 0.05f;
        float lowerDistance = edgeDistance + cliffOutset * Mathf.Lerp(0.42f, 0.82f, Hash01(seed + 2.2f));
        float width = waterfallWidth * Mathf.Lerp(0.82f, 1.46f, Hash01(seed + 3.9f));
        Vector3 halfWidth = tangent.normalized * (width * 0.5f);

        Vector3 top = point + normal * edgeDistance;
        Vector3 middle = point + normal * Mathf.Lerp(edgeDistance, lowerDistance, 0.52f);
        Vector3 bottom = point + normal * lowerDistance;

        top.y = visualSurfaceY + 0.035f;
        middle.y = visualSurfaceY - waterfallDropDepth * 0.42f;
        bottom.y = visualSurfaceY - waterfallDropDepth;

        int start = vertices.Count;
        vertices.Add(top - halfWidth);
        vertices.Add(top + halfWidth);
        vertices.Add(middle - halfWidth * 0.82f + normal * 0.08f);
        vertices.Add(middle + halfWidth * 0.82f + normal * 0.08f);
        vertices.Add(bottom - halfWidth * 0.58f);
        vertices.Add(bottom + halfWidth * 0.58f);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(0f, 0.5f));
        uvs.Add(new Vector2(1f, 0.5f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));

        AddQuad(triangles, start, start + 1, start + 2, start + 3);
        AddQuad(triangles, start + 2, start + 3, start + 4, start + 5);
    }

    private Vector3 GetSideNormal(List<Vector3> segment, int index, int side)
    {
        Vector3 previous = index > 0 ? segment[index - 1] : segment[index];
        Vector3 next = index < segment.Count - 1 ? segment[index + 1] : segment[index];
        Vector3 tangent = next - previous;
        tangent.y = 0f;

        if (tangent.sqrMagnitude <= 0.0001f)
            tangent = Vector3.right;

        tangent.Normalize();
        Vector3 right = new Vector3(tangent.z, 0f, -tangent.x);
        return right.normalized * side;
    }

    private Vector3 GetTangent(List<Vector3> segment, int index)
    {
        Vector3 previous = index > 0 ? segment[index - 1] : segment[index];
        Vector3 next = index < segment.Count - 1 ? segment[index + 1] : segment[index];
        Vector3 tangent = next - previous;
        tangent.y = 0f;

        if (tangent.sqrMagnitude <= 0.0001f)
            tangent = Vector3.forward;

        return tangent.normalized;
    }

    private float GetEdgeNoise(Vector3 point, int index, int side)
    {
        float perlin = Mathf.PerlinNoise(point.x * 0.37f + side * 9.1f, point.z * 0.37f + index * 0.021f);
        float wave = Mathf.Sin(point.x * 1.73f + point.z * 1.17f + side * 2.4f) * 0.5f + 0.5f;
        return ((perlin * 0.72f + wave * 0.28f) - 0.5f) * edgeNoiseAmplitude;
    }

    private float GetIslandEdgeNoise(Vector3 point, int index, int side)
    {
        float broad = Mathf.PerlinNoise(point.x * 0.28f + side * 5.9f, point.z * 0.28f + index * 0.017f);
        float chipped = Mathf.PerlinNoise(point.x * 1.05f - side * 3.4f, point.z * 1.05f + 2.2f);
        float rhythm = Mathf.Sin(point.x * 0.77f + point.z * 0.63f + side * 1.9f) * 0.5f + 0.5f;
        return ((broad * 0.58f + chipped * 0.28f + rhythm * 0.14f) - 0.5f) * islandEdgeNoiseAmplitude;
    }

    private float GetRockStepNoise(Vector3 point, int index, int side)
    {
        float stepped = Mathf.PerlinNoise(point.x * 1.7f + index * 0.05f, point.z * 1.7f + side * 4.8f);
        return Mathf.Round((stepped - 0.5f) * 4f) * 0.25f;
    }

    private float GetRidgeNoise(Vector3 point, int index, int side)
    {
        float perlin = Mathf.PerlinNoise(point.x * 0.43f + side * 3.7f, point.z * 0.43f + index * 0.033f);
        float brokenRock = Mathf.PerlinNoise(point.x * 1.35f - side * 4.1f, point.z * 1.35f + 6.2f);
        return ((perlin * 0.78f + brokenRock * 0.22f) - 0.5f) * ridgeEdgeNoiseAmplitude;
    }

    private float GetFogNoise(Vector3 point, int index, int side, int layerIndex)
    {
        float broad = Mathf.PerlinNoise(point.x * 0.16f + side * 8.7f + layerIndex * 0.19f, point.z * 0.16f - layerIndex * 0.23f);
        float wisps = Mathf.PerlinNoise(point.x * 0.72f - layerIndex * 0.11f, point.z * 0.72f + side * 2.9f);
        float drift = Mathf.Sin(index * 0.19f + layerIndex * 0.71f + side * 1.8f) * 0.5f + 0.5f;
        return (broad * 0.52f + wisps * 0.33f + drift * 0.15f) - 0.5f;
    }

    private float GetCloudSeed(Vector3 point, int index, int side, int row)
    {
        return point.x * 12.9898f + point.z * 78.233f + index * 0.173f + side * 37.719f + row * 11.137f;
    }

    private float GetDetailSeed(Vector3 point, int index, int side, int salt)
    {
        return point.x * 19.371f + point.z * 52.973f + index * 0.319f + side * 23.617f + salt * 7.931f;
    }

    private float Hash01(float seed)
    {
        return Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1f);
    }

    private void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(c);
        triangles.Add(b);
        triangles.Add(d);
    }

    private void ApplyMesh(MeshFilter filter, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        if (filter == null)
            return;

        Mesh mesh = filter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void ClearMesh(MeshFilter filter)
    {
        if (filter == null || filter.sharedMesh == null)
            return;

        filter.sharedMesh.Clear();
    }

    private void AnimateFog()
    {
        for (int i = 0; i < fogLayers.Count; i++)
        {
            FogLayer layer = fogLayers[i];
            if (layer == null || layer.material == null)
                continue;

            layer.offset += layer.speed * Time.deltaTime;
            SetTextureOffset(layer.material, layer.offset);
        }
    }

    private void AnimateCloudPuffs()
    {
        if (cloudPuffFilter == null)
            return;

        float amplitude = Mathf.Max(0f, cloudDriftAmplitude);
        float speed = Mathf.Max(0f, cloudDriftSpeed);
        if (amplitude <= 0f || speed <= 0f)
        {
            cloudPuffFilter.transform.localPosition = Vector3.zero;
            return;
        }

        float time = Application.isPlaying ? Time.time : 0f;
        cloudPuffFilter.transform.localPosition = new Vector3(
            Mathf.Sin(time * speed) * amplitude,
            Mathf.Sin(time * speed * 0.73f + 1.2f) * amplitude * 0.18f,
            Mathf.Cos(time * speed * 0.91f) * amplitude * 0.7f);
    }

    private Material CreateOpaqueMaterial(string materialName, Color color, float smoothness)
    {
        Shader shader = FindRuntimeShader(
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Unlit/Color",
            "Standard");

        Material material = new Material(shader)
        {
            name = materialName
        };

        SetColor(material, color);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        return material;
    }

    private Material CreateLitMaterial(string materialName, Color color, float smoothness)
    {
        Shader shader = FindRuntimeShader(
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Standard");

        Material material = new Material(shader)
        {
            name = materialName
        };

        SetColor(material, color);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        return material;
    }

    private Material CreateTransparentMaterial(string materialName, Color color, Texture2D texture, int renderQueue)
    {
        Shader shader = FindRuntimeShader(
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Unlit/Transparent",
            "Standard");

        Material material = new Material(shader)
        {
            name = materialName,
            renderQueue = renderQueue
        };

        SetColor(material, color);

        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_BLENDMODE_ALPHA");

        return material;
    }

    private Shader FindRuntimeShader(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null)
                return shader;
        }

        if (sceneGroundShaderFallback != null)
            return sceneGroundShaderFallback;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
            return spriteShader;

        Debug.LogError("AbyssGroundRenderer: Kein kompatibler Runtime-Shader gefunden.");
        Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
        return errorShader != null ? errorShader : Shader.Find("Standard");
    }

    private Texture2D CreateRidgeTexture(int seed)
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Procedural Abyss Mountain Ridge " + seed,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        Color warmDust = plateauTopColor;
        Color darkStone = Color.Lerp(cliffColor, ridgeTopColor, 0.28f);
        Color paleStone = Color.Lerp(cliffRimColor, Color.white, 0.18f);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float broad = Mathf.PerlinNoise(u * 4.6f + seed * 0.001f, v * 4.6f - seed * 0.001f);
                float grain = Mathf.PerlinNoise(u * 18.0f - seed * 0.0004f, v * 18.0f + seed * 0.0006f);
                float cracks = Mathf.Abs(Mathf.Sin((u * 24.0f + broad * 2.4f) + (v * 7.0f)));
                float ridge = Mathf.SmoothStep(0.18f, 0.9f, broad * 0.62f + grain * 0.28f + cracks * 0.1f);

                Color color = Color.Lerp(darkStone, warmDust, ridge);
                color = Color.Lerp(color, paleStone, Mathf.Clamp01(grain * 0.18f));
                color.a = 1f;
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private Texture2D CreateFogTexture(int seed)
    {
        int size = Mathf.Clamp(fogTextureSize, 64, 512);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Procedural Abyss Fog " + seed,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        System.Random random = new System.Random(seed);
        const int blobCount = 18;
        Vector2[] centers = new Vector2[blobCount];
        float[] radii = new float[blobCount];

        for (int i = 0; i < blobCount; i++)
        {
            centers[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            radii[i] = Mathf.Lerp(0.12f, 0.36f, (float)random.NextDouble());
        }

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float density = 0f;

                for (int i = 0; i < blobCount; i++)
                {
                    Vector2 delta = new Vector2(u, v) - centers[i];
                    delta.x = Mathf.Min(Mathf.Abs(delta.x), 1f - Mathf.Abs(delta.x));
                    delta.y = Mathf.Min(Mathf.Abs(delta.y), 1f - Mathf.Abs(delta.y));
                    float distance = delta.magnitude;
                    float radius = Mathf.Max(0.01f, radii[i]);
                    density += Mathf.Exp(-(distance * distance) / (radius * radius)) * 0.58f;
                }

                float wisps = Mathf.PerlinNoise(u * 7.3f + seed * 0.001f, v * 7.3f - seed * 0.001f);
                float strands = Mathf.Sin((u * 16.5f + v * 9.7f + seed * 0.013f)) * 0.5f + 0.5f;
                density = density * 0.72f + wisps * 0.23f + strands * 0.05f;

                float alpha = Mathf.SmoothStep(0.22f, 0.86f, density);
                Color color = Color.Lerp(new Color(0.45f, 0.5f, 0.48f, alpha), Color.white, Mathf.Clamp01(density * 0.18f));
                color.a = alpha;
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private Texture2D CreateAbyssTexture(int seed)
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Procedural Abyss Painted Void " + seed,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float centeredX = u - 0.5f;
                float centeredY = v - 0.5f;
                float distance = Mathf.Sqrt(centeredX * centeredX + centeredY * centeredY);

                float broadSmoke = Mathf.PerlinNoise(u * 2.4f + seed * 0.001f, v * 2.4f - seed * 0.001f);
                float fineSmoke = Mathf.PerlinNoise(u * 8.6f - seed * 0.0007f, v * 8.6f + seed * 0.0009f);
                float vein = Mathf.Sin((u * 19.0f + fineSmoke * 2.4f) + (v * 7.5f)) * 0.5f + 0.5f;
                float swirl = Mathf.Sin(Mathf.Atan2(centeredY, centeredX) * 3.0f + distance * 18.0f + broadSmoke * 1.7f) * 0.5f + 0.5f;

                float mist = broadSmoke * 0.48f + fineSmoke * 0.25f + vein * 0.14f + swirl * 0.13f;
                float vignette = Mathf.SmoothStep(0.1f, 0.74f, distance);
                float brightness = Mathf.Lerp(0.48f, 0.96f, mist) * Mathf.Lerp(1.04f, 0.78f, vignette);

                Color deep = new Color(0.73f, 0.78f, 0.8f, 1f);
                Color smoke = new Color(0.98f, 0.97f, 0.93f, 1f);
                Color blueCold = new Color(0.82f, 0.88f, 0.93f, 1f);

                Color color = Color.Lerp(deep, smoke, brightness);
                color = Color.Lerp(color, blueCold, Mathf.Clamp01(fineSmoke * 0.16f));
                color.a = 1f;
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void SetColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void SetTextureScale(Material material, Vector2 scale)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureScale("_BaseMap", scale);
        if (material.HasProperty("_MainTex"))
            material.SetTextureScale("_MainTex", scale);
    }

    private void SetMaterialTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    private void SetTextureOffset(Material material, Vector2 offset)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureOffset("_BaseMap", offset);
        if (material.HasProperty("_MainTex"))
            material.SetTextureOffset("_MainTex", offset);
    }
}
