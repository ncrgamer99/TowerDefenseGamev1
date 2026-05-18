using UnityEngine;

[RequireComponent(typeof(Tower))]
public class TowerVisualTierController : MonoBehaviour
{
    [Header("References")]
    public Tower tower;

    [Header("Visual Tier Shape")]
    public bool autoBuildTierShape = true;
    public Color tierColor = new Color32(90, 185, 226, 255);
    public Color highTierColor = new Color32(214, 164, 65, 255);
    public float baseYOffset = 0.42f;
    public float ringRadius = 0.62f;
    public float ringThickness = 0.075f;
    public float spireHeight = 0.55f;
    public float spireWidth = 0.18f;
    public bool addBasePedestal = true;
    public bool addTierGlow = true;

    private int appliedTier = -1;
    private Transform tierRoot;

    private void Awake()
    {
        if (tower == null)
            tower = GetComponent<Tower>();

        ApplyTier(tower != null ? tower.visualTier : 0);
    }

    public void ApplyTier(int visualTier)
    {
        int safeTier = Mathf.Max(0, visualTier);

        if (appliedTier == safeTier)
            return;

        appliedTier = safeTier;
        RebuildVisualTierShape(safeTier);
    }

    private void RebuildVisualTierShape(int tier)
    {
        if (!autoBuildTierShape)
            return;

        ClearOldVisuals();

        if (tier <= 0)
            return;

        GameObject rootObject = new GameObject("VisualTierShape_Root");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        tierRoot = rootObject.transform;

        Color color = tier >= 3 ? highTierColor : tierColor;
        Material solidMaterial = CreateMaterial(color, 0.35f, false);
        Material glowMaterial = CreateMaterial(color, 0.80f, true);

        if (addBasePedestal)
            CreateBasePedestal(solidMaterial, tier);

        CreateBaseRing(solidMaterial, tier);
        CreateTopBand(solidMaterial, tier);

        int spireCount = Mathf.Clamp(tier + 1, 2, 6);
        for (int i = 0; i < spireCount; i++)
            CreateSpireAtIndex(i, spireCount, solidMaterial, tier);

        if (tier >= 2)
            CreateTierCore(solidMaterial, tier);

        if (tier >= 3)
            CreateTopCrown(glowMaterial, tier);

        if (addTierGlow)
            CreateGlowHalo(glowMaterial, tier);
    }

    private void ClearOldVisuals()
    {
        Transform existing = transform.Find("VisualTierShape_Root");

        if (existing != null)
            Destroy(existing.gameObject);

        tierRoot = null;
    }

    private void CreateBasePedestal(Material material, int tier)
    {
        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "TierPedestal";
        pedestal.transform.SetParent(tierRoot, false);
        pedestal.transform.localPosition = new Vector3(0f, baseYOffset - 0.22f, 0f);
        pedestal.transform.localScale = new Vector3(0.72f + tier * 0.08f, 0.08f, 0.72f + tier * 0.08f);
        ApplyMaterialAndRemoveCollider(pedestal, material);
    }

    private void CreateBaseRing(Material material, int tier)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TierRing_Lower";
        ring.transform.SetParent(tierRoot, false);
        ring.transform.localPosition = new Vector3(0f, baseYOffset, 0f);
        ring.transform.localScale = new Vector3(ringRadius * (1.32f + tier * 0.10f), ringThickness, ringRadius * (1.32f + tier * 0.10f));
        ApplyMaterialAndRemoveCollider(ring, material);
    }

    private void CreateTopBand(Material material, int tier)
    {
        GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        band.name = "TierRing_Upper";
        band.transform.SetParent(tierRoot, false);
        band.transform.localPosition = new Vector3(0f, baseYOffset + 0.34f + tier * 0.03f, 0f);
        band.transform.localScale = new Vector3(ringRadius * (0.95f + tier * 0.07f), ringThickness * 0.75f, ringRadius * (0.95f + tier * 0.07f));
        ApplyMaterialAndRemoveCollider(band, material);
    }

    private void CreateSpireAtIndex(int index, int count, Material material, int tier)
    {
        float angle = (Mathf.PI * 2f / Mathf.Max(1, count)) * index;
        float radius = ringRadius * (0.88f + tier * 0.045f);
        Vector3 localPosition = new Vector3(Mathf.Cos(angle) * radius, baseYOffset, Mathf.Sin(angle) * radius);

        GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spire.name = "TierSpire_" + index;
        spire.transform.SetParent(tierRoot, false);
        spire.transform.localPosition = localPosition + Vector3.up * (spireHeight * 0.5f + tier * 0.055f);
        spire.transform.localScale = new Vector3(spireWidth + tier * 0.015f, spireHeight + tier * 0.12f, spireWidth + tier * 0.015f);
        spire.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
        ApplyMaterialAndRemoveCollider(spire, material);
    }

    private void CreateTierCore(Material material, int tier)
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.name = "TierCore";
        core.transform.SetParent(tierRoot, false);
        core.transform.localPosition = Vector3.up * (baseYOffset + 0.42f + tier * 0.04f);
        core.transform.localScale = new Vector3(0.28f + tier * 0.035f, 0.30f + tier * 0.04f, 0.28f + tier * 0.035f);
        ApplyMaterialAndRemoveCollider(core, material);
    }

    private void CreateTopCrown(Material material, int tier)
    {
        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "TierCrown_Glow";
        crown.transform.SetParent(tierRoot, false);
        crown.transform.localPosition = Vector3.up * (baseYOffset + spireHeight + 0.54f + tier * 0.10f);
        crown.transform.localScale = Vector3.one * (0.28f + tier * 0.045f);
        ApplyMaterialAndRemoveCollider(crown, material);
    }

    private void CreateGlowHalo(Material material, int tier)
    {
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "TierGlowHalo";
        halo.transform.SetParent(tierRoot, false);
        halo.transform.localPosition = new Vector3(0f, 0.035f, 0f);
        halo.transform.localScale = new Vector3(0.82f + tier * 0.10f, 0.012f, 0.82f + tier * 0.10f);
        ApplyMaterialAndRemoveCollider(halo, material);
    }

    private void ApplyMaterialAndRemoveCollider(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;

        Collider collider = target.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);
    }

    private Material CreateMaterial(Color color, float emissionStrength, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        Color finalColor = color;
        finalColor.a = transparent ? 0.38f : color.a;
        material.color = finalColor;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", finalColor);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * Mathf.Max(0f, emissionStrength));
        }

        if (transparent)
            ConfigureTransparentMaterial(material);

        return material;
    }

    private void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }
}
