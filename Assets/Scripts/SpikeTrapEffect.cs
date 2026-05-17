using System.Collections.Generic;
using UnityEngine;

public class SpikeTrapEffect : MonoBehaviour
{
    private static readonly List<SpikeTrapEffect> activeSpikes = new List<SpikeTrapEffect>();

    public Vector3 worldPosition;
    public float triggerRadius = 0.35f;
    public float bleedDamagePerTick = 2f;
    public float bleedTickInterval = 2.5f;
    public float bleedDuration = 12f;
    public Tower sourceTower;
    public Enemy excludedEnemy;

    public static void CreateSpikeAtWorldPosition(Vector3 position, float radius, float damagePerTick, float tickInterval, float duration, Tower sourceTower = null, Enemy excludedEnemy = null)
    {
        GameObject spikeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spikeObject.name = "Spike Trap";
        spikeObject.transform.position = new Vector3(position.x, 0.08f, position.z);
        spikeObject.transform.localScale = new Vector3(0.28f, 0.08f, 0.28f);

        Collider spikeCollider = spikeObject.GetComponent<Collider>();
        if (spikeCollider != null)
            Destroy(spikeCollider);

        Renderer spikeRenderer = spikeObject.GetComponent<Renderer>();
        if (spikeRenderer != null)
            spikeRenderer.material.color = new Color32(125, 125, 135, 255);

        SpikeTrapEffect spike = spikeObject.AddComponent<SpikeTrapEffect>();
        spike.worldPosition = spikeObject.transform.position;
        spike.triggerRadius = Mathf.Max(0.1f, radius);
        spike.bleedDamagePerTick = Mathf.Max(0.1f, damagePerTick);
        spike.bleedTickInterval = Mathf.Max(0.1f, tickInterval);
        spike.bleedDuration = Mathf.Max(0.1f, duration);
        spike.sourceTower = sourceTower;
        spike.excludedEnemy = excludedEnemy;
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

            if (enemy == spike.excludedEnemy || enemy.HasBleed())
                continue;

            Vector3 flatPosition = new Vector3(position.x, spike.worldPosition.y, position.z);

            if (Vector3.Distance(flatPosition, spike.worldPosition) > spike.triggerRadius)
                continue;

            enemy.ApplyBleed(spike.bleedDamagePerTick, spike.bleedDuration, spike.bleedTickInterval, spike.sourceTower);
            Destroy(spike.gameObject);
            return;
        }
    }
}
