using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class ChaosVariantEnemyPrefabSetup : MonoBehaviour
{
    [Header("Chaos Variant Setup")]
    public bool forceChaosVariantOnAwake = true;
    public bool keepRoleColorReadable = true;

    [Header("Readable Purple Tint")]
    public Color glowColor = new Color32(175, 70, 255, 255);
    [Range(0f, 1f)]
    public float bodyTintBlend = 0.12f;
    public float emissionStrength = 0.20f;
    public float pulseStrength = 0.16f;
    public float pulseSpeed = 2.8f;

    [Header("Bottom Aura")]
    public bool createBottomAura = true;
    public float bottomAuraRadius = 0.58f;
    public float bottomAuraHeight = 0.018f;
    public float bottomAuraYOffset = 0.035f;
    [Range(0f, 1f)]
    public float bottomAuraAlpha = 0.32f;

    [Header("Flame Wisps")]
    public bool createFlameWisps = true;
    public int flameWispCount = 5;
    public float flameRadius = 0.42f;
    public float flameHeight = 0.55f;
    public float flameWidth = 0.08f;
    public float flameYOffset = 0.26f;
    [Range(0f, 1f)]
    public float flameAlpha = 0.24f;
    public float flameFlickerSpeed = 4.0f;
    public float flameFlickerAmount = 0.28f;

    private Enemy enemy;
    private Renderer[] cachedRenderers;
    private Material[] runtimeMaterials;
    private Color[] baseColors;
    private Transform auraRoot;
    private Material auraMaterial;
    private Renderer[] flameRenderers;
    private Vector3[] flameBaseScales;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();

        if (enemy != null && forceChaosVariantOnAwake)
        {
            enemy.enemyVariantType = EnemyVariantType.Chaos;
            enemy.autoApplyVariantStats = true;
            enemy.chaosVariantPulseColor = glowColor;
            enemy.chaosVariantPulseStrength = Mathf.Clamp01(pulseStrength);
            enemy.chaosVariantPulseSpeed = Mathf.Max(0.1f, pulseSpeed);
        }

        CacheMaterials();
        ApplyReadableBodyGlow();
        BuildAuraVisualsIfNeeded();
    }

    private void LateUpdate()
    {
        ApplyReadableBodyGlow();
        UpdateFlameWisps();
    }

    private void CacheMaterials()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (cachedRenderers == null)
        {
            runtimeMaterials = new Material[0];
            baseColors = new Color[0];
            return;
        }

        runtimeMaterials = new Material[cachedRenderers.Length];
        baseColors = new Color[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] == null)
                continue;

            Transform auraTransform = transform.Find("ChaosVariantAuraFX");
            if (auraTransform != null && cachedRenderers[i].transform.IsChildOf(auraTransform))
                continue;

            runtimeMaterials[i] = cachedRenderers[i].material;
            baseColors[i] = runtimeMaterials[i] != null ? GetMaterialBaseColor(runtimeMaterials[i]) : Color.white;
        }
    }

    private void ApplyReadableBodyGlow()
    {
        if (!keepRoleColorReadable)
            return;

        if (runtimeMaterials == null || runtimeMaterials.Length == 0)
            CacheMaterials();

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, pulseSpeed));
        float blend = Mathf.Clamp01(bodyTintBlend + pulse * pulseStrength * 0.45f);

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material material = runtimeMaterials[i];

            if (material == null)
                continue;

            Color readableColor = Color.Lerp(baseColors[i], glowColor, blend);
            readableColor.a = baseColors[i].a;

            SetMaterialBaseColor(material, readableColor);

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glowColor * Mathf.Max(0f, emissionStrength * (0.65f + pulse * 0.35f)));
            }
        }
    }

    private Color GetMaterialBaseColor(Material material)
    {
        if (material == null)
            return Color.white;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return material.color;
    }

    private void SetMaterialBaseColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
    }

    private void BuildAuraVisualsIfNeeded()
    {
        Transform existing = transform.Find("ChaosVariantAuraFX");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject rootObject = new GameObject("ChaosVariantAuraFX");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        auraRoot = rootObject.transform;

        auraMaterial = CreateTransparentMaterial(glowColor, bottomAuraAlpha, 0.85f);

        if (createBottomAura)
            CreateBottomAura();

        if (createFlameWisps)
            CreateFlameWisps();
    }

    private void CreateBottomAura()
    {
        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        aura.name = "BottomPurpleAura";
        aura.transform.SetParent(auraRoot, false);
        aura.transform.localPosition = new Vector3(0f, bottomAuraYOffset, 0f);
        aura.transform.localScale = new Vector3(bottomAuraRadius, bottomAuraHeight, bottomAuraRadius);
        ApplyAuraMaterialAndRemoveCollider(aura, auraMaterial);
    }

    private void CreateFlameWisps()
    {
        int count = Mathf.Clamp(flameWispCount, 1, 12);
        flameRenderers = new Renderer[count];
        flameBaseScales = new Vector3[count];
        Material flameMaterial = CreateTransparentMaterial(glowColor, flameAlpha, 1.15f);

        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            GameObject wisp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wisp.name = "PurpleFlameWisp_" + i;
            wisp.transform.SetParent(auraRoot, false);
            wisp.transform.localPosition = new Vector3(Mathf.Cos(angle) * flameRadius, flameYOffset, Mathf.Sin(angle) * flameRadius);
            wisp.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            wisp.transform.localScale = new Vector3(flameWidth, flameHeight, flameWidth);
            ApplyAuraMaterialAndRemoveCollider(wisp, flameMaterial);

            flameRenderers[i] = wisp.GetComponent<Renderer>();
            flameBaseScales[i] = wisp.transform.localScale;
        }
    }

    private void UpdateFlameWisps()
    {
        if (flameRenderers == null || flameBaseScales == null)
            return;

        for (int i = 0; i < flameRenderers.Length; i++)
        {
            if (flameRenderers[i] == null)
                continue;

            float phase = Time.time * Mathf.Max(0.1f, flameFlickerSpeed) + i * 1.37f;
            float flicker = 1f + Mathf.Sin(phase) * flameFlickerAmount + Mathf.Sin(phase * 1.71f) * flameFlickerAmount * 0.45f;
            flicker = Mathf.Clamp(flicker, 0.55f, 1.65f);

            Transform t = flameRenderers[i].transform;
            Vector3 baseScale = flameBaseScales[i];
            t.localScale = new Vector3(baseScale.x * (0.85f + flicker * 0.12f), baseScale.y * flicker, baseScale.z * (0.85f + flicker * 0.12f));
        }
    }

    private void ApplyAuraMaterialAndRemoveCollider(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private Material CreateTransparentMaterial(Color color, float alpha, float emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        Color finalColor = color;
        finalColor.a = Mathf.Clamp01(alpha);
        material.color = finalColor;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", finalColor);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * Mathf.Max(0f, emission));
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        return material;
    }
}
