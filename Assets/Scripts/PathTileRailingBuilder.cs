using UnityEngine;

public class PathTileRailingBuilder : MonoBehaviour
{
    [Header("Generated Railing")]
    public bool generateOnConfigure = true;
    public float tileSize = 1f;
    public float railingHeight = 0.6f;
    public float railingThickness = 0.12f;
    public float railingYOffset = 0.33f;
    public float railingOverlap = 0.08f;
    public Color railingColor = new Color32(38, 48, 62, 255);

    [Header("Connection Behaviour")]
    [Tooltip("Wenn aktiv, bleiben auch an den Laufweg-Öffnungen kleine Geländer sichtbar.")]
    public bool keepConnectedEdgesClosed = false;

    private Transform railingRoot;

    public void Configure(float newTileSize, bool openNorth, bool openEast, bool openSouth, bool openWest, float height, float thickness, Color color)
    {
        tileSize = Mathf.Max(0.1f, newTileSize);
        railingHeight = Mathf.Max(0.6f, height);
        railingThickness = Mathf.Max(0.12f, thickness);
        railingYOffset = Mathf.Max(0.08f, railingHeight * 0.5f + 0.03f);
        railingOverlap = Mathf.Max(0.02f, railingThickness * 0.75f);
        railingColor = color;

        if (!generateOnConfigure)
            return;

        Rebuild(openNorth, openEast, openSouth, openWest);
    }

    public void Rebuild(bool openNorth, bool openEast, bool openSouth, bool openWest)
    {
        ClearOldRailings();

        GameObject rootObject = new GameObject("__AutoRailings");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        railingRoot = rootObject.transform;

        Material material = CreateMaterial(railingColor);

        bool northClosed = keepConnectedEdgesClosed || !openNorth;
        bool southClosed = keepConnectedEdgesClosed || !openSouth;
        bool eastClosed = keepConnectedEdgesClosed || !openEast;
        bool westClosed = keepConnectedEdgesClosed || !openWest;

        float halfTile = tileSize * 0.5f;
        float wallOffset = halfTile + railingThickness * 0.5f;
        float wallLength = tileSize + railingOverlap * 2f;

        if (northClosed)
            CreateWall("Wall_North", new Vector3(0f, railingYOffset, wallOffset), new Vector3(wallLength, railingHeight, railingThickness), material);

        if (southClosed)
            CreateWall("Wall_South", new Vector3(0f, railingYOffset, -wallOffset), new Vector3(wallLength, railingHeight, railingThickness), material);

        if (eastClosed)
            CreateWall("Wall_East", new Vector3(wallOffset, railingYOffset, 0f), new Vector3(railingThickness, railingHeight, wallLength), material);

        if (westClosed)
            CreateWall("Wall_West", new Vector3(-wallOffset, railingYOffset, 0f), new Vector3(railingThickness, railingHeight, wallLength), material);
    }

    private void CreateWall(string objectName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = objectName;
        wall.transform.SetParent(railingRoot, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localScale = localScale;

        Renderer renderer = wall.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;

        Collider collider = wall.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);
    }

    private void ClearOldRailings()
    {
        Transform existing = transform.Find("__AutoRailings");

        if (existing != null)
            Destroy(existing.gameObject);

        railingRoot = null;
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        return material;
    }
}
