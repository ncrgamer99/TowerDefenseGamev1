using UnityEngine;

public class PathTileRailingBuilder : MonoBehaviour
{
    [Header("Generated Railing")]
    public bool generateOnConfigure = true;
    public float tileSize = 1f;
    public float railingHeight = 0.35f;
    public float railingThickness = 0.08f;
    public float railingYOffset = 0.2f;
    public Color railingColor = new Color32(58, 68, 82, 255);

    [Header("Connection Behaviour")]
    [Tooltip("Wenn aktiv, bleiben auch an den Laufweg-Öffnungen kleine Geländer sichtbar.")]
    public bool keepConnectedEdgesClosed = false;

    private Transform railingRoot;

    public void Configure(float newTileSize, bool openNorth, bool openEast, bool openSouth, bool openWest, float height, float thickness, Color color)
    {
        tileSize = Mathf.Max(0.1f, newTileSize);
        railingHeight = Mathf.Max(0.35f, height);
        railingThickness = Mathf.Max(0.08f, thickness);
        railingYOffset = Mathf.Max(0.08f, railingHeight * 0.5f + 0.025f);
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
        float inset = railingThickness * 0.5f;

        if (northClosed)
            CreateRail("Rail_North", new Vector3(0f, railingYOffset, halfTile - inset), new Vector3(tileSize, railingHeight, railingThickness), material);

        if (southClosed)
            CreateRail("Rail_South", new Vector3(0f, railingYOffset, -halfTile + inset), new Vector3(tileSize, railingHeight, railingThickness), material);

        if (eastClosed)
            CreateRail("Rail_East", new Vector3(halfTile - inset, railingYOffset, 0f), new Vector3(railingThickness, railingHeight, tileSize), material);

        if (westClosed)
            CreateRail("Rail_West", new Vector3(-halfTile + inset, railingYOffset, 0f), new Vector3(railingThickness, railingHeight, tileSize), material);

        CreateCornerPost("Post_NE", new Vector3(tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, tileSize * 0.5f), material);
        CreateCornerPost("Post_NW", new Vector3(-tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, tileSize * 0.5f), material);
        CreateCornerPost("Post_SE", new Vector3(tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, -tileSize * 0.5f), material);
        CreateCornerPost("Post_SW", new Vector3(-tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, -tileSize * 0.5f), material);
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

    private void CreateRail(string objectName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        CreateWall(objectName, localPosition, localScale, material);
    }

    private void CreateCornerPost(string objectName, Vector3 localPosition, Material material)
    {
        float postWidth = railingThickness * 1.25f;
        Vector3 postScale = new Vector3(postWidth, railingHeight * 1.25f, postWidth);

        CreateWall(objectName, localPosition, postScale, material);

        if (GetComponent<Collider>() != null)
            Destroy(GetComponent<Collider>());
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
