using UnityEngine;

[RequireComponent(typeof(Tower))]
public class TowerRangeIndicator : MonoBehaviour
{
    [Header("References")]
    public Tower tower;

    [Header("Range Visual")]
    public bool autoCreateOnShow = true;
    public Color fillColor = new Color32(55, 145, 255, 55);
    public Color outlineColor = new Color32(80, 180, 255, 185);
    public float yOffset = 0.035f;
    public int circleSegments = 96;
    public float outlineWidth = 0.045f;

    private GameObject fillObject;
    private MeshRenderer fillRenderer;
    private MeshFilter fillFilter;
    private LineRenderer outlineRenderer;
    private float lastRange = -1f;
    private bool isVisible = false;

    private void Awake()
    {
        if (tower == null)
            tower = GetComponent<Tower>();
    }

    private void LateUpdate()
    {
        if (!isVisible)
            return;

        RefreshRange();
    }

    public void Show()
    {
        isVisible = true;
        EnsureVisuals();
        SetVisualsActive(true);
        RefreshRange(true);
    }

    public void Hide()
    {
        isVisible = false;
        SetVisualsActive(false);
    }

    public bool IsVisible()
    {
        return isVisible;
    }

    public void RefreshRange(bool force = false)
    {
        if (tower == null)
            tower = GetComponent<Tower>();

        if (tower == null)
            return;

        if (!isVisible)
            return;

        EnsureVisuals();

        float range = Mathf.Max(0.05f, tower.range);

        if (!force && Mathf.Abs(range - lastRange) < 0.001f)
            return;

        lastRange = range;
        BuildFillMesh(range);
        BuildOutline(range);
    }

    private void EnsureVisuals()
    {
        if (!autoCreateOnShow)
            return;

        if (fillObject == null)
        {
            fillObject = new GameObject("RangeIndicator_Fill");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.localPosition = Vector3.up * yOffset;
            fillObject.transform.localRotation = Quaternion.identity;

            fillFilter = fillObject.AddComponent<MeshFilter>();
            fillRenderer = fillObject.AddComponent<MeshRenderer>();
            fillRenderer.sharedMaterial = CreateTransparentMaterial(fillColor);
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillRenderer.receiveShadows = false;
        }

        if (outlineRenderer == null)
        {
            GameObject outlineObject = new GameObject("RangeIndicator_Outline");
            outlineObject.transform.SetParent(transform, false);
            outlineObject.transform.localPosition = Vector3.up * (yOffset + 0.006f);
            outlineObject.transform.localRotation = Quaternion.identity;

            outlineRenderer = outlineObject.AddComponent<LineRenderer>();
            outlineRenderer.loop = true;
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.widthMultiplier = outlineWidth;
            outlineRenderer.positionCount = Mathf.Max(12, circleSegments);
            outlineRenderer.material = CreateTransparentMaterial(outlineColor);
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
        }
    }

    private Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.renderQueue = 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private void SetVisualsActive(bool active)
    {
        if (fillObject != null)
            fillObject.SetActive(active);

        if (outlineRenderer != null)
            outlineRenderer.gameObject.SetActive(active);
    }

    private void BuildFillMesh(float radius)
    {
        if (fillFilter == null)
            return;

        int segments = Mathf.Clamp(circleSegments, 24, 192);
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        for (int i = 0; i < segments; i++)
        {
            int tri = i * 3;
            triangles[tri] = 0;
            triangles[tri + 1] = i + 1;
            triangles[tri + 2] = i == segments - 1 ? 1 : i + 2;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Tower Range Fill";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        fillFilter.sharedMesh = mesh;
    }

    private void BuildOutline(float radius)
    {
        if (outlineRenderer == null)
            return;

        int segments = Mathf.Clamp(circleSegments, 24, 192);
        outlineRenderer.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            outlineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }
}
