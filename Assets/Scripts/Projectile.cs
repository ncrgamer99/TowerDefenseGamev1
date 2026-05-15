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
    public int lightningChainTargets = 3;
    public float lightningChainRange = 2.5f;
    public float lightningChainDamageMultiplier = 0.5f;
    public float lightningLineDuration = 0.12f;
    public Color lightningLineColor = new Color32(125, 220, 255, 255);

    [Header("Mortar")]
    public float mortarRadius = 1.5f;
    public Color mortarImpactColor = new Color32(255, 150, 45, 185);

    [Header("Spike Trap")]
    public float spikeTriggerRadius = 0.45f;
    public float spikeBleedDamagePerTick = 2f;
    public float spikeBleedTickInterval = 3f;
    public float spikeBleedDuration = 20f;

    private Enemy target;
    private Tower ownerTower;
    private Vector3 mortarImpactPosition;
    private bool hasMortarImpactPosition = false;

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
        if (behavior == ProjectileBehavior.MortarAOE)
        {
            UpdateMortarProjectile();
            return;
        }

        if (target == null)
        {
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
            Enemy nextTarget = FindNearestChainTarget(chainSource, hitEnemies);

            if (nextTarget == null)
                return;

            DrawTemporaryLine(chainSource.transform.position + Vector3.up * 0.35f, nextTarget.transform.position + Vector3.up * 0.35f, lightningLineColor, lightningLineDuration);
            ApplyDirectHit(nextTarget, chainDamage);
            hitEnemies.Add(nextTarget);
            chainSource = nextTarget;
        }
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
        SpikeTrapEffect.CreateSpikeAtWorldPosition(spikePosition, spikeTriggerRadius, spikeBleedDamagePerTick, spikeBleedTickInterval, spikeBleedDuration);
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
            renderer.material.color = mortarImpactColor;

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
        line.material = CreateLineMaterial(color);
        line.startColor = color;
        line.endColor = color;
        Destroy(lineObject, Mathf.Max(0.02f, duration));
    }

    private Material CreateLineMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        return material;
    }
}
