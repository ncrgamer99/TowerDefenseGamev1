using UnityEngine;

public static class BuildSafeFxMaterialUtility
{
    public const string DefaultTransparentResourcePath = "Materials/FX_Unlit_Transparent";

    public static Material CreateTransparentMaterial(Color color)
    {
        return CreateTransparentMaterial(color, null, DefaultTransparentResourcePath);
    }

    public static Material CreateTransparentMaterial(Color color, Material template, string resourcePath)
    {
        Material material = CreateMaterialFromTemplateOrResource(template, resourcePath, true);

        if (material == null)
            return null;

        ApplyColor(material, color);
        ApplyTransparentSettings(material);
        return material;
    }

    public static Material CreateSolidMaterial(Color color)
    {
        Material material = CreateMaterialFromTemplateOrResource(null, "", false);

        if (material == null)
            return null;

        ApplyColor(material, color);
        return material;
    }

    public static bool IsMissingOrErrorMaterial(Material material)
    {
        if (material == null)
            return true;

        Shader shader = material.shader;
        if (shader == null)
            return true;

        string shaderName = shader.name;
        return string.IsNullOrEmpty(shaderName) || shaderName == "Hidden/InternalErrorShader";
    }

    public static void ApplyColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
    }

    public static void ApplyEmission(Material material, Color color, float strength)
    {
        if (material == null || !material.HasProperty("_EmissionColor"))
            return;

        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * Mathf.Max(0f, strength));
    }

    public static void ApplyTransparentSettings(Material material)
    {
        if (material == null)
            return;

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

    private static Material CreateMaterialFromTemplateOrResource(Material template, string resourcePath, bool preferUnlit)
    {
        if (template != null && !IsMissingOrErrorMaterial(template))
            return new Material(template);

        if (!string.IsNullOrEmpty(resourcePath))
        {
            Material resourceMaterial = Resources.Load<Material>(resourcePath);
            if (resourceMaterial != null && !IsMissingOrErrorMaterial(resourceMaterial))
                return new Material(resourceMaterial);
        }

        Shader shader = FindBuildSafeShader(preferUnlit);
        if (shader == null)
        {
            Debug.LogWarning("BuildSafeFxMaterialUtility: Kein build-sicherer Shader gefunden. Prüfe Render Pipeline und Shader-Stripping-Einstellungen.");
            return null;
        }

        return new Material(shader);
    }

    private static Shader FindBuildSafeShader(bool preferUnlit)
    {
        string[] unlitCandidates =
        {
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "UI/Default",
            "Unlit/Color",
            "Unlit/Transparent",
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        string[] litCandidates =
        {
            "Universal Render Pipeline/Lit",
            "Standard",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "UI/Default",
            "Unlit/Color"
        };

        string[] candidates = preferUnlit ? unlitCandidates : litCandidates;

        foreach (string shaderName in candidates)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
                return shader;
        }

        return null;
    }
}
