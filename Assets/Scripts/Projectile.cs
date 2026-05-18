using System.Collections.Generic;
using UnityEngine;

public enum ProjectileBehavior
{
    Direct,
    LightningChain,
    MortarAOE,
    SpikeTrap
}

public class Projectile : MonoBehaviour
{
    public float speed = 40f;
    public int damage = 1;
    public ProjectileBehavior behavior = ProjectileBehavior.Direct;

    [Header("Lifetime Safety")]
    public float maxLifetime = 6f;
    public bool clearTrailsOnHit = true;

    [Header("Build-Safe FX Material")]
    public bool repairMissingFxMaterialsOnAwake = true;
    public Material projectileFxMaterial;
    public Color fallbackFxColor = Color.white;
    public string fallbackFxMaterialResourcePath = "Materials/FX_Unlit_Transparent";

    [Header("Status Effects")]
    public bool appliesBurn = false;
    public int burnDamage = 1;
    public float burnDuration = 3f;

    public bool appliesPoison = false;
    public int poisonDamage = 1;
    public float poisonDuration = 4f;

    public bool appliesSlow = false;
    public float slowAmount = 0.5f;
    public float slowDuration = 2f;

    [Header("Lightning Chain")]
    public int lightningChainTargets = 2;
    public float lightningBonusChainChance = 0f;
    public float lightningChainRange = 2.5f;
    public float lightningChainDamageMultiplier = 0.55f;
    public float lightningLineDuration = 0.12f;
    public Color lightningLineColor = new Color32(125, 220, 255, 255);

    [Header("Mortar")]
    public float mortarRadius = 0.85f;
    public Color mortarImpactColor = new Color32(255, 150, 45, 185);

    [Header("Spike Trap")]
    public float spikeTriggerRadius = 0.45f;
    public float spikeBleedDamagePerTick = 3f;
    public float spikeBleedTickInterval = 2.0f;
    public float spikeBleedDuration = 14f;

    private Enemy target;
    private Tower ownerTower;
    private Vector3 mortarImpactPosition;
    private bool hasMortarImpactPosition = false;
    private float lifetimeTimer = 0f;
    private Material runtimeFallbackFxMaterial;

    private void Awake()
    {
        RepairFxMaterialsIfNeeded();
    }

    private void OnEnable()
    {
        lifetimeTimer = 0f;
        RepairFxMaterialsIfNeeded();
    }

    public void SetTarget(Enemy newTarget, Tower tower)
    {
        target = newTarget;
        ownerTower = tower;

        if (behavior == ProjectileBehavior.MortarAOE && target != null)
            SetMortarImpactPosition(target.transform.position);
    }

    public void SetMortarImpactPosition(Vector3 impactPosition)
    {
        mortarImpactPosition = new Vector3(impactPosition.x, impactPosition.y + 0.3f, impactPosition.z);
        hasMortarImpactPosition = true;
    }

    private void Update()
    {
        lifetimeTimer += Time.deltaTime;

        if (lifetimeTimer >= Mathf.Max(0.1f, maxLifetime))
        {
            ClearTransientFx();
            Destroy(gameObject);
            return;
        }

        if (behavior == ProjectileBehavior.MortarAOE)
        {
            UpdateMortarProjectile();
            return;
        }

        if (target == null)
        {
            ClearTransientFx();
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.transform.position + Vector3.up * 0.3f;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            HitTarget();
        }
    }

    private void UpdateMortarProjectile()
    {
        if (!hasMortarImpactPosition)
        {
            ClearTransientFx();
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, mortarImpactPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, mortarImpactPosition) < 0.1f)
            ExplodeMortar();
    }

    private void HitTarget()
    {
        if (target == null)
        {
            ClearTransientFx();
            Destroy(gameObject);
            return;
        }

        switch (behavior)
        {
            case ProjectileBehavior.LightningChain:
                HitLightningTarget();
                break;
            case ProjectileBehavior.SpikeTrap:
                HitSpikeTarget();
                break;
            case ProjectileBehavior.Direct:
            default:
                ApplyDirectHit(target, damage);
                break;
        }

        ClearTransientFx();
        Destroy(gameObject);
    }

    private void ApplyDirectHit(Enemy enemy, int hitDamage)
    {
        if (enemy == null)
            return;

        enemy.TakeDamage(hitDamage, ownerTower);

        if (appliesBurn)
            enemy.ApplyBurn(burnDamage, burnDuration, ownerTower);

        if (appliesPoison)
            enemy.ApplyPoison(poisonDamage, poisonDuration, ownerTower);

        if (appliesSlow)
            enemy.ApplySlow(slowAmount, slowDuration, ownerTower);
    }

    private void HitLightningTarget()
    {
        ApplyDirectHit(target, damage);
        ChainLightningFrom(target);
    }

    private void ChainLightningFrom(Enemy firstTarget)
    {
        if (firstTarget == null)
            return;

        List<Enemy> hitEnemies = new List<Enemy> { firstTarget };
        Enemy chainSource = firstTarget;
        int chainDamage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Clamp01(lightningChainDamageMultiplier)));
        int chains = Mathf.Max(0, lightningChainTargets);

        for (int i = 0; i < chains; i++)
        {
            if (!TryChainToNextTarget(ref chainSource, hitEnemies, chainDamage))
                return;
        }

        if (Random.value <= Mathf.Clamp01(lightningBonusChainChance))
            TryChainToNextTarget(ref chainSource, hitEnemies, chainDamage);
    }

    private bool TryChainToNextTarget(ref Enemy chainSource, List<Enemy> hitEnemies, int chainDamage)
    {
        Enemy nextTarget = FindNearestChainTarget(chainSource, hitEnemies);

        if (nextTarget == null)
            return false;

        DrawTemporaryLine(chainSource.transform.position + Vector3.up * 0.35f, nextTarget.transform.position + Vector3.up * 0.35f, lightningLineColor, lightningLineDuration);
        ApplyDirectHit(nextTarget, chainDamage);
        hitEnemies.Add(nextTarget);
        chainSource = nextTarget;
        return true;
    }

    private Enemy FindNearestChainTarget(Enemy chainSource, List<Enemy> excludedEnemies)
    {
        if (chainSource == null)
            return null;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy bestTarget = null;
        float bestDistance = Mathf.Max(0.1f, lightningChainRange);

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || excludedEnemies.Contains(enemy))
                continue;

            float distance = Vector3.Distance(chainSource.transform.position, enemy.transform.position);

            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    private void HitSpikeTarget()
    {
        Vector3 spikePosition = target.transform.position;
        ApplyDirectHit(target, damage);
        SpikeTrapEffect.CreateSpikeAtWorldPosition(spikePosition, spikeTriggerRadius, spikeBleedDamagePerTick, spikeBleedTickInterval, spikeBleedDuration, ownerTower, target);
    }

    private void ExplodeMortar()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float radius = Mathf.Max(0.1f, mortarRadius);

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (Vector3.Distance(enemy.transform.position, mortarImpactPosition) <= radius)
                ApplyDirectHit(enemy, damage);
        }

        CreateMortarImpactVisual(mortarImpactPosition, radius);
        ClearTransientFx();
        Destroy(gameObject);
    }

    private void CreateMortarImpactVisual(Vector3 position, float radius)
    {
        GameObject impactObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        impactObject.name = "Mortar Impact";
        impactObject.transform.position = new Vector3(position.x, 0.06f, position.z);
        impactObject.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);

        Collider impactCollider = impactObject.GetComponent<Collider>();
        if (impactCollider != null)
            Destroy(impactCollider);

        Renderer renderer = impactObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color finalColor = mortarImpactColor;
            renderer.sharedMaterial = CreateBuildSafeTransparentMaterial(finalColor);
            renderer.material.color = finalColor;
        }

        Destroy(impactObject, 0.25f);
    }

    private void DrawTemporaryLine(Vector3 start, Vector3 end, Color color, float duration)
    {
        GameObject lineObject = new GameObject("Lightning Chain Line");
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.055f;
        line.endWidth = 0.02f;
        line.material = CreateBuildSafeTransparentMaterial(color);
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        Destroy(lineObject, Mathf.Max(0.02f, duration));
    }

    private void RepairFxMaterialsIfNeeded()
    {
        if (!repairMissingFxMaterialsOnAwake)
            return;

        LineRenderer[] lineRenderers = GetComponentsInChildren<LineRenderer>(true);
        foreach (LineRenderer lineRenderer in lineRenderers)
        {
            if (lineRenderer == null)
                continue;

            if (IsMissingOrErrorMaterial(lineRenderer.sharedMaterial))
                lineRenderer.sharedMaterial = GetRuntimeFallbackFxMaterial();
        }

        TrailRenderer[] trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        foreach (TrailRenderer trailRenderer in trailRenderers)
        {
            if (trailRenderer == null)
                continue;

            if (IsMissingOrErrorMaterial(trailRenderer.sharedMaterial))
                trailRenderer.sharedMaterial = GetRuntimeFallbackFxMaterial();
        }

        ParticleSystemRenderer[] particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach (ParticleSystemRenderer particleRenderer in particleRenderers)
        {
            if (particleRenderer == null)
                continue;

            if (IsMissingOrErrorMaterial(particleRenderer.sharedMaterial))
                particleRenderer.sharedMaterial = GetRuntimeFallbackFxMaterial();
        }

        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            if (meshRenderer == null)
                continue;

            if (IsMissingOrErrorMaterial(meshRenderer.sharedMaterial))
                meshRenderer.sharedMaterial = GetRuntimeFallbackFxMaterial();
        }
    }

    private Material GetRuntimeFallbackFxMaterial()
    {
        if (runtimeFallbackFxMaterial != null)
            return runtimeFallbackFxMaterial;

        runtimeFallbackFxMaterial = CreateBuildSafeTransparentMaterial(fallbackFxColor);
        return runtimeFallbackFxMaterial;
    }

    private void ClearTransientFx()
    {
        if (!clearTrailsOnHit)
            return;

        TrailRenderer[] trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        foreach (TrailRenderer trailRenderer in trailRenderers)
        {
            if (trailRenderer != null)
                trailRenderer.Clear();
        }

        LineRenderer[] lineRenderers = GetComponentsInChildren<LineRenderer>(true);
        foreach (LineRenderer lineRenderer in lineRenderers)
        {
            if (lineRenderer != null)
                lineRenderer.enabled = false;
        }

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem != null)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private Material CreateBuildSafeTransparentMaterial(Color color)
    {
        Material template = projectileFxMaterial;

        if (template == null && !string.IsNullOrEmpty(fallbackFxMaterialResourcePath))
            template = Resources.Load<Material>(fallbackFxMaterialResourcePath);

        Material material = null;

        if (template != null && !IsMissingOrErrorMaterial(template))
            material = new Material(template);

        if (material == null)
        {
            Shader shader = FindBuildSafeShader(true);
            if (shader == null)
                return null;

            material = new Material(shader);
        }

        ApplyMaterialColor(material, color);
        ApplyTransparentSettings(material);
        return material;
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

    private static bool IsMissingOrErrorMaterial(Material material)
    {
        if (material == null)
            return true;

        Shader shader = material.shader;
        if (shader == null)
            return true;

        string shaderName = shader.name;
        return string.IsNullOrEmpty(shaderName) || shaderName == "Hidden/InternalErrorShader";
    }

    private static void ApplyMaterialColor(Material material, Color color)
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

    private static void ApplyTransparentSettings(Material material)
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
}
