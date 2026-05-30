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
    public float pathBridgeHalfWidth = 1.18f;
    public float cliffHalfWidth = 5.8f;
    public float pathEndExtension = 10f;
    public float environmentPadding = 24f;
    public float minimumEnvironmentSize = 58f;
    public float sampleSpacing = 0.24f;
    [Range(0f, 1f)] public float cornerRoundness = 0.68f;
    public int smoothingPasses = 2;
    public float maxContinuousPathGap = 2.2f;
    public float edgeNoiseAmplitude = 0.62f;

    [Header("Vertical Layers")]
    public float visualSurfaceY = 0.012f;
    public float cliffTopY = -0.035f;
    public float cliffMidY = -0.11f;
    public float abyssY = -0.12f;
    public float fogBaseY = 0.02f;
    public float rimLift = 0.018f;

    [Header("Fog")]
    public int fogLayerCount = 14;
    public float fogAlpha = 0.34f;
    public float fogDriftSpeed = 0.08f;
    public int fogTextureSize = 256;

    [Header("Colors")]
    public Color abyssColor = new Color32(10, 16, 20, 255);
    public Color distantMistColor = new Color32(112, 121, 116, 100);
    public Color cliffColor = new Color32(8, 11, 14, 128);
    public Color cliffRimColor = new Color32(56, 68, 70, 255);
    public Color deepFogColor = new Color32(182, 188, 178, 88);

    [Header("Refresh")]
    public float refreshInterval = 0.25f;

    private const string RuntimeRootName = "Abyss Ground Runtime Visuals";

    private Transform visualRoot;
    private MeshFilter abyssFilter;
    private MeshFilter leftCliffFilter;
    private MeshFilter rightCliffFilter;
    private MeshFilter leftRimFilter;
    private MeshFilter rightRimFilter;

    private Material abyssMaterial;
    private Material cliffMaterial;
    private Material rimMaterial;
    private Texture2D abyssTexture;
    private Texture2D fogTextureA;
    private Texture2D fogTextureB;

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

        GameObject groundObject = GameObject.Find(sceneGroundObjectName);
        if (groundObject == null)
            return;

        MeshRenderer[] renderers = groundObject.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = false;

        groundVisualHidden = true;
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
        fogTextureA = CreateFogTexture(7123);
        fogTextureB = CreateFogTexture(9149);

        abyssMaterial = CreateOpaqueMaterial("Abyss Painted Void", abyssColor, 0.12f);
        SetMaterialTexture(abyssMaterial, abyssTexture);
        SetTextureScale(abyssMaterial, new Vector2(2.2f, 2.2f));

        cliffMaterial = CreateTransparentMaterial("Abyss Soft Cliff Shadow", cliffColor, fogTextureB, 2990);
        SetTextureScale(cliffMaterial, new Vector2(2.6f, 9f));
        rimMaterial = CreateOpaqueMaterial("Abyss Cliff Rim", cliffRimColor, 0.22f);
    }

    private void CreateStaticMeshObjects()
    {
        abyssFilter = CreateMeshObject("Abyss Dark Depth", abyssMaterial);
        leftCliffFilter = CreateMeshObject("Abyss Left Cliff", cliffMaterial);
        rightCliffFilter = CreateMeshObject("Abyss Right Cliff", cliffMaterial);
        leftRimFilter = CreateMeshObject("Abyss Left Organic Rim", rimMaterial);
        rightRimFilter = CreateMeshObject("Abyss Right Organic Rim", rimMaterial);
    }

    private void CreateFogLayers()
    {
        fogLayers.Clear();
        int layerCount = Mathf.Clamp(fogLayerCount, 4, 18);

        for (int i = 0; i < layerCount; i++)
        {
            float t = layerCount <= 1 ? 0f : i / (float)(layerCount - 1);
            Texture2D texture = i % 2 == 0 ? fogTextureA : fogTextureB;
            Color layerColor = Color.Lerp(deepFogColor, distantMistColor, t);
            layerColor.a = fogAlpha * Mathf.Lerp(0.55f, 1.05f, Mathf.Sin(t * Mathf.PI));

            Material material = CreateTransparentMaterial("Abyss Fog Layer " + (i + 1), layerColor, texture, 3010 + i);
            float textureScaleX = Mathf.Lerp(1.25f, 3.6f, t);
            float textureScaleY = Mathf.Lerp(1.8f, 4.8f, 1f - t * 0.45f);
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
        int pathHash = ComputePathHash(pathSegments);

        if (!force && pathHash == lastPathHash)
            return;

        lastPathHash = pathHash;

        Bounds bounds = CalculateEnvironmentBounds(pathSegments);
        BuildAbyssBase(bounds);
        ClearMesh(leftCliffFilter);
        ClearMesh(rightCliffFilter);
        ClearMesh(leftRimFilter);
        ClearMesh(rightRimFilter);
        BuildFogMeshes(bounds);
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
                float y = visualSurfaceY + (noise - 0.5f) * 0.006f;

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
                float edgeNoise = GetEdgeNoise(point, i, side);

                Vector3 top = point + normal * Mathf.Max(0.35f, pathBridgeHalfWidth + edgeNoise * 0.18f);
                Vector3 mid = point + normal * Mathf.Max(0.7f, pathBridgeHalfWidth + cliffHalfWidth * 0.28f + edgeNoise * 0.38f);
                Vector3 bottom = point + normal * Mathf.Max(1f, cliffHalfWidth + edgeNoise);

                top.y = cliffTopY;
                mid.y = cliffMidY;
                bottom.y = abyssY + 0.03f;

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

        float rimWidth = Mathf.Max(0.16f, pathBridgeHalfWidth * 0.28f);

        for (int s = 0; s < segments.Count; s++)
        {
            List<Vector3> segment = segments[s];
            int segmentStart = vertices.Count;

            for (int i = 0; i < segment.Count; i++)
            {
                Vector3 normal = GetSideNormal(segment, i, side);
                Vector3 point = segment[i];
                float edgeNoise = GetEdgeNoise(point, i, side) * 0.28f;

                Vector3 inner = point + normal * Mathf.Max(0.2f, pathBridgeHalfWidth - rimWidth * 0.35f + edgeNoise);
                Vector3 outer = point + normal * Mathf.Max(0.3f, pathBridgeHalfWidth + rimWidth + edgeNoise);

                inner.y = cliffTopY + rimLift;
                outer.y = cliffTopY + rimLift * 0.45f;

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

    private void BuildFogMeshes(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int i = 0; i < fogLayers.Count; i++)
        {
            FogLayer layer = fogLayers[i];
            float t = fogLayers.Count <= 1 ? 0f : i / (float)(fogLayers.Count - 1);
            float extra = Mathf.Lerp(0f, 10f, t);
            float y = fogBaseY + i * 0.0035f;
            int resolution = Mathf.Lerp(6, 10, t) > 8f ? 10 : 7;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int z = 0; z <= resolution; z++)
            {
                float zT = z / (float)resolution;
                float worldZ = Mathf.Lerp(min.z - extra, max.z + extra, zT);

                for (int x = 0; x <= resolution; x++)
                {
                    float xT = x / (float)resolution;
                    float worldX = Mathf.Lerp(min.x - extra, max.x + extra, xT);
                    float wave = Mathf.Sin((worldX * 0.13f + worldZ * 0.09f) + i * 0.7f) * 0.01f;

                    vertices.Add(new Vector3(worldX, y + wave, worldZ));
                    uvs.Add(new Vector2(xT * 2.4f, zT * 2.4f));
                }
            }

            int rowSize = resolution + 1;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
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

            ApplyMesh(layer.filter, vertices, triangles, uvs);
        }
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

    private float GetEdgeNoise(Vector3 point, int index, int side)
    {
        float perlin = Mathf.PerlinNoise(point.x * 0.37f + side * 9.1f, point.z * 0.37f + index * 0.021f);
        float wave = Mathf.Sin(point.x * 1.73f + point.z * 1.17f + side * 2.4f) * 0.5f + 0.5f;
        return ((perlin * 0.72f + wave * 0.28f) - 0.5f) * edgeNoiseAmplitude;
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

    private Material CreateOpaqueMaterial(string materialName, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

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
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Standard");

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

                float broadSmoke = Mathf.PerlinNoise(u * 3.1f + seed * 0.001f, v * 3.1f - seed * 0.001f);
                float fineSmoke = Mathf.PerlinNoise(u * 11.7f - seed * 0.0007f, v * 11.7f + seed * 0.0009f);
                float vein = Mathf.Sin((u * 19.0f + fineSmoke * 2.4f) + (v * 7.5f)) * 0.5f + 0.5f;
                float swirl = Mathf.Sin(Mathf.Atan2(centeredY, centeredX) * 3.0f + distance * 18.0f + broadSmoke * 1.7f) * 0.5f + 0.5f;

                float mist = broadSmoke * 0.48f + fineSmoke * 0.25f + vein * 0.14f + swirl * 0.13f;
                float vignette = Mathf.SmoothStep(0.1f, 0.74f, distance);
                float brightness = Mathf.Lerp(0.22f, 0.68f, mist) * Mathf.Lerp(1.05f, 0.46f, vignette);

                Color deep = new Color(0.014f, 0.024f, 0.03f, 1f);
                Color smoke = new Color(0.34f, 0.39f, 0.36f, 1f);
                Color blueCold = new Color(0.08f, 0.16f, 0.2f, 1f);

                Color color = Color.Lerp(deep, smoke, brightness);
                color = Color.Lerp(color, blueCold, Mathf.Clamp01(fineSmoke * 0.22f));
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
