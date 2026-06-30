using System.Collections.Generic;
using UnityEngine;

public class SpikeTrapEffect : MonoBehaviour
{
    private static readonly List<SpikeTrapEffect> activeSpikes = new List<SpikeTrapEffect>();
    private static readonly Color SpikeVisualColor = new Color32(125, 125, 135, 255);

    public Vector3 worldPosition;
    public float triggerRadius = 0.35f;
    public float bleedDamagePerTick = 2f;
    public float bleedTickInterval = 2.5f;
    public float bleedDuration = 12f;
    public Tower sourceTower;
    public Enemy excludedEnemy;
    public int maxTriggers = 1;
    public float triggerCooldown = 0.16f;
    public int triggerDamage = 0;
    public int extraTargets = 0;
    public float extraDamageMultiplier = 0f;
    public float extraTargetRadius = 0.55f;

    private int triggerCount = 0;
    private float lastTriggerTime = -999f;
    private readonly HashSet<int> triggeredEnemyIds = new HashSet<int>();
    private readonly List<Enemy> extraTargetSnapshot = new List<Enemy>();

    public static void CreateSpikeAtWorldPosition(Vector3 position, float radius, float damagePerTick, float tickInterval, float duration, Tower sourceTower = null, Enemy excludedEnemy = null, int maxTriggers = 1, float triggerCooldown = 0.16f, int triggerDamage = 0, int extraTargets = 0, float extraDamageMultiplier = 0f, float extraTargetRadius = 0.55f)
    {
        GameObject spikeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spikeObject.name = "Spike Trap";
        spikeObject.transform.position = new Vector3(position.x, 0.08f, position.z);
        float visualRadius = Mathf.Max(0.28f, radius * 1.1f);
        spikeObject.transform.localScale = new Vector3(visualRadius, 0.08f, visualRadius);

        Collider spikeCollider = spikeObject.GetComponent<Collider>();
        if (spikeCollider != null)
            Destroy(spikeCollider);

        Renderer spikeRenderer = spikeObject.GetComponent<Renderer>();
        if (spikeRenderer != null)
            spikeRenderer.sharedMaterial = CreateBuildSafeMaterial(SpikeVisualColor);

        SpikeTrapEffect spike = spikeObject.AddComponent<SpikeTrapEffect>();
        spike.worldPosition = spikeObject.transform.position;
        spike.triggerRadius = Mathf.Max(0.1f, radius);
        spike.bleedDamagePerTick = Mathf.Max(0.1f, damagePerTick);
        spike.bleedTickInterval = Mathf.Max(0.1f, tickInterval);
        spike.bleedDuration = Mathf.Max(0.1f, duration);
        spike.sourceTower = sourceTower;
        spike.excludedEnemy = excludedEnemy;
        spike.maxTriggers = Mathf.Max(1, maxTriggers);
        spike.triggerCooldown = Mathf.Max(0.02f, triggerCooldown);
        spike.triggerDamage = Mathf.Max(0, triggerDamage);
        spike.extraTargets = Mathf.Max(0, extraTargets);
        spike.extraDamageMultiplier = Mathf.Clamp01(extraDamageMultiplier);
        spike.extraTargetRadius = Mathf.Max(0.1f, extraTargetRadius);
    }

    private void OnEnable()
    {
        if (!activeSpikes.Contains(this))
            activeSpikes.Add(this);

        WaveEventBus.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        activeSpikes.Remove(this);
        WaveEventBus.WaveCompleted -= HandleWaveCompleted;
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        Destroy(gameObject);
    }

    public static void TryApplyAtWorldPosition(Vector3 position, Enemy enemy)
    {
        if (enemy == null || activeSpikes.Count == 0)
            return;

        for (int i = activeSpikes.Count - 1; i >= 0; i--)
        {
            SpikeTrapEffect spike = activeSpikes[i];

            if (spike == null)
            {
                activeSpikes.RemoveAt(i);
                continue;
            }

            if (enemy == spike.excludedEnemy || enemy.HasBleed() || spike.triggeredEnemyIds.Contains(enemy.GetInstanceID()))
                continue;

            if (Time.time - spike.lastTriggerTime < spike.triggerCooldown)
                continue;

            Vector3 flatPosition = new Vector3(position.x, spike.worldPosition.y, position.z);

            if (Vector3.Distance(flatPosition, spike.worldPosition) > spike.triggerRadius)
                continue;

            spike.TriggerEnemy(enemy);
            return;
        }
    }

    private void TriggerEnemy(Enemy enemy)
    {
        if (enemy == null)
            return;

        triggeredEnemyIds.Add(enemy.GetInstanceID());
        lastTriggerTime = Time.time;
        triggerCount++;

        if (sourceTower != null && sourceTower.towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMastery))
            spikeMastery.RecordSpikeTrapTriggered(sourceTower, enemy);

        enemy.ApplyBleed(bleedDamagePerTick, bleedDuration, bleedTickInterval, sourceTower);

        if (triggerDamage > 0)
            enemy.TakeDamage(triggerDamage, sourceTower, false, 0);

        ApplyExtraTargets(enemy);

        if (triggerCount >= Mathf.Max(1, maxTriggers))
            Destroy(gameObject);
    }

    private void ApplyExtraTargets(Enemy primaryEnemy)
    {
        if (extraTargets <= 0 || extraDamageMultiplier <= 0f)
            return;

        EnemyRegistry.CopyActiveEnemies(extraTargetSnapshot);
        int applied = 0;

        foreach (Enemy enemy in extraTargetSnapshot)
        {
            if (enemy == null || enemy == primaryEnemy || enemy == excludedEnemy || enemy.currentHealth <= 0f || enemy.HasBleed())
                continue;

            if (triggeredEnemyIds.Contains(enemy.GetInstanceID()))
                continue;

            if (Vector3.Distance(enemy.transform.position, primaryEnemy.transform.position) > extraTargetRadius)
                continue;

            triggeredEnemyIds.Add(enemy.GetInstanceID());

            if (sourceTower != null && sourceTower.towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMastery))
                spikeMastery.RecordSpikeTrapTriggered(sourceTower, enemy);

            enemy.ApplyBleed(bleedDamagePerTick * extraDamageMultiplier, bleedDuration * Mathf.Clamp(extraDamageMultiplier, 0.25f, 1f), bleedTickInterval, sourceTower);

            if (triggerDamage > 0)
                enemy.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(triggerDamage * extraDamageMultiplier)), sourceTower, false, 0);

            applied++;

            if (applied >= extraTargets)
                return;
        }
    }

    private static Material CreateBuildSafeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            shader = Shader.Find("Hidden/InternalErrorShader");

        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        return material;
    }
}
