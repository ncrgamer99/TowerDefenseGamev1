using UnityEngine;

public class PathTileRailingBuilder : MonoBehaviour
{
    [Header("Generated Railing")]
    public bool generateOnConfigure = true;
    public float tileSize = 1f;
    public float railingHeight = 0.18f;
    public float railingThickness = 0.055f;
    public float railingYOffset = 0.12f;
    public Color railingColor = new Color32(58, 68, 82, 255);

    [Header("Connection Behaviour")]
    [Tooltip("Wenn aktiv, bleiben auch zwischen verbundenen PathTiles kleine Geländer sichtbar. Das macht den Labyrinth-Look klarer.")]
    public bool keepConnectedEdgesClosed = true;

    private Transform railingRoot;

    public void Configure(float newTileSize, bool openNorth, bool openEast, bool openSouth, bool openWest, float height, float thickness, Color color)
    {
        tileSize = Mathf.Max(0.1f, newTileSize);
        railingHeight = Mathf.Max(0.02f, height);
        railingThickness = Mathf.Max(0.01f, thickness);
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

        if (northClosed)
            CreateRail("Rail_North", new Vector3(0f, railingYOffset, tileSize * 0.5f), new Vector3(tileSize, railingHeight, railingThickness), material);

        if (southClosed)
            CreateRail("Rail_South", new Vector3(0f, railingYOffset, -tileSize * 0.5f), new Vector3(tileSize, railingHeight, railingThickness), material);

        if (eastClosed)
            CreateRail("Rail_East", new Vector3(tileSize * 0.5f, railingYOffset, 0f), new Vector3(railingThickness, railingHeight, tileSize), material);

        if (westClosed)
            CreateRail("Rail_West", new Vector3(-tileSize * 0.5f, railingYOffset, 0f), new Vector3(railingThickness, railingHeight, tileSize), material);

        CreateCornerPost("Post_NE", new Vector3(tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, tileSize * 0.5f), material);
        CreateCornerPost("Post_NW", new Vector3(-tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, tileSize * 0.5f), material);
        CreateCornerPost("Post_SE", new Vector3(tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, -tileSize * 0.5f), material);
        CreateCornerPost("Post_SW", new Vector3(-tileSize * 0.5f, railingYOffset + railingHeight * 0.18f, -tileSize * 0.5f), material);
    }

    private void CreateRail(string objectName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = objectName;
        rail.transform.SetParent(railingRoot, false);
        rail.transform.localPosition = localPosition;
        rail.transform.localScale = localScale;

        Renderer renderer = rail.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;

        Collider collider = rail.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);
    }

    private void CreateCornerPost(string objectName, Vector3 localPosition, Material material)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = objectName;
        post.transform.SetParent(railingRoot, false);
        post.transform.localPosition = localPosition;
        float postWidth = railingThickness * 1.25f;
        post.transform.localScale = new Vector3(postWidth, railingHeight * 1.25f, postWidth);

        Renderer renderer = post.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        Collider collider = post.GetComponent<Collider>();
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
