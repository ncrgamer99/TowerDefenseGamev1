using System.Collections.Generic;
using UnityEngine;

public class MortarCraterEffect : MonoBehaviour
{
    public Vector3 worldPosition;
    public float radius = 0.5f;
    public float damagePerTick = 1f;
    public float tickInterval = 0.45f;
    public float duration = 1f;
    public bool applyStagger = false;
    public float staggerMultiplier = 0.9f;
    public float staggerDuration = 0.2f;
    public Tower sourceTower;

    private float remainingDuration;
    private float tickTimer;
    private readonly List<Enemy> enemyTickSnapshot = new List<Enemy>();

    public static void CreateCraterAtWorldPosition(Vector3 position, float radius, float damagePerTick, float tickInterval, float duration, Tower sourceTower, bool applyStagger, float staggerMultiplier, float staggerDuration)
    {
        GameObject craterObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        craterObject.name = "Mortar Crater";
        craterObject.transform.position = new Vector3(position.x, 0.045f, position.z);
        craterObject.transform.localScale = new Vector3(radius * 2f, 0.025f, radius * 2f);

        Collider craterCollider = craterObject.GetComponent<Collider>();
        if (craterCollider != null)
            Destroy(craterCollider);

        Renderer renderer = craterObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateBuildSafeMaterial(new Color32(210, 92, 44, 150));

        MortarCraterEffect crater = craterObject.AddComponent<MortarCraterEffect>();
        crater.worldPosition = craterObject.transform.position;
        crater.radius = Mathf.Max(0.1f, radius);
        crater.damagePerTick = Mathf.Max(0.1f, damagePerTick);
        crater.tickInterval = Mathf.Max(0.1f, tickInterval);
        crater.duration = Mathf.Max(0.1f, duration);
        crater.applyStagger = applyStagger;
        crater.staggerMultiplier = Mathf.Clamp(staggerMultiplier, 0.1f, 1f);
        crater.staggerDuration = Mathf.Max(0.05f, staggerDuration);
        crater.sourceTower = sourceTower;
    }

    private void OnEnable()
    {
        remainingDuration = Mathf.Max(0.1f, duration);
        tickTimer = 0f;
        WaveEventBus.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        WaveEventBus.WaveCompleted -= HandleWaveCompleted;
    }

    private void Update()
    {
        remainingDuration -= Time.deltaTime;
        tickTimer -= Time.deltaTime;

        if (tickTimer <= 0f)
        {
            tickTimer = Mathf.Max(0.1f, tickInterval);
            ApplyCraterTick();
        }

        if (remainingDuration <= 0f)
            Destroy(gameObject);
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        Destroy(gameObject);
    }

    private void ApplyCraterTick()
    {
        EnemyRegistry.CopyActiveEnemies(enemyTickSnapshot);

        foreach (Enemy enemy in enemyTickSnapshot)
        {
            if (enemy == null || enemy.currentHealth <= 0f)
                continue;

            Vector3 flatPosition = new Vector3(enemy.transform.position.x, worldPosition.y, enemy.transform.position.z);

            if (Vector3.Distance(flatPosition, worldPosition) > radius)
                continue;

            if (applyStagger)
                enemy.ApplyMortarCraterStagger(staggerDuration, staggerMultiplier, sourceTower);

            enemy.TakeDamage(damagePerTick, sourceTower, false, 0);
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
