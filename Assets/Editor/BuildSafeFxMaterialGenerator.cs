#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BuildSafeFxMaterialGenerator
{
    private const string MaterialDirectory = "Assets/Resources/Materials";
    private const string TransparentMaterialPath = "Assets/Resources/Materials/FX_Unlit_Transparent.mat";

    static BuildSafeFxMaterialGenerator()
    {
        EditorApplication.delayCall += EnsureBuildSafeFxMaterials;
    }

    [MenuItem("Tools/Tower Defense/Ensure Build-Safe FX Materials")]
    public static void EnsureBuildSafeFxMaterials()
    {
        if (!Directory.Exists(MaterialDirectory))
            Directory.CreateDirectory(MaterialDirectory);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TransparentMaterialPath);
        bool created = false;

        if (material == null)
        {
            Shader shader = FindPreferredTransparentShader();
            if (shader == null)
            {
                Debug.LogWarning("BuildSafeFxMaterialGenerator: Kein passender Shader für FX_Unlit_Transparent gefunden.");
                return;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, TransparentMaterialPath);
            created = true;
        }

        ConfigureTransparentMaterial(material, Color.white);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        if (created)
            Debug.Log("Build-safe FX material created: " + TransparentMaterialPath);
    }

    private static Shader FindPreferredTransparentShader()
    {
        string[] candidates =
        {
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "UI/Default",
            "Unlit/Color",
            "Unlit/Transparent",
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        foreach (string shaderName in candidates)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
                return shader;
        }

        return null;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }
}
#endif
