using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Tower))]
public class TowerVisualFeedback : MonoBehaviour
{
    [Header("References")]
    public Tower tower;

    [Header("Level Up Animation")]
    public Color levelUpColor = new Color32(255, 220, 80, 210);
    public float levelUpDuration = 0.85f;
    public float levelUpScalePulse = 1.28f;
    public float levelUpRingRadius = 1.15f;
    public int levelUpRingSegments = 64;

    [Header("Upgrade Point Idle Animation")]
    public bool animateWhenUpgradePointAvailable = true;
    public Color upgradePointColor = new Color32(70, 150, 255, 190);
    public float idlePulseSpeed = 3.0f;
    public float idleScaleStrength = 0.06f;
    public float idleRingRadius = 0.72f;
    public float idleRingYOffset = 1.05f;

    private Vector3 baseScale;
    private Coroutine levelUpRoutine;
    private LineRenderer upgradePointRing;
    private bool upgradePointAvailable = false;

    private void Awake()
    {
        if (tower == null)
            tower = GetComponent<Tower>();

        baseScale = transform.localScale;
        EnsureUpgradePointRing();
        SetUpgradePointAvailable(tower != null && tower.upgradePoints > 0);
    }

    private void Update()
    {
        UpdateUpgradePointIdleAnimation();
    }

    public void PlayLevelUpAnimation(bool gainedUpgradePoint)
    {
        if (levelUpRoutine != null)
            StopCoroutine(levelUpRoutine);

        levelUpRoutine = StartCoroutine(LevelUpRoutine(gainedUpgradePoint));
    }

    public void SetUpgradePointAvailable(bool available)
    {
        upgradePointAvailable = available;
        EnsureUpgradePointRing();

        if (upgradePointRing != null)
            upgradePointRing.gameObject.SetActive(available && animateWhenUpgradePointAvailable);
    }

    private IEnumerator LevelUpRoutine(bool gainedUpgradePoint)
    {
        GameObject ringObject = new GameObject("LevelUp_BurstRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.up * 0.08f;

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.widthMultiplier = 0.055f;
        ring.positionCount = Mathf.Max(24, levelUpRingSegments);
        ring.material = CreateTransparentMaterial(gainedUpgradePoint ? upgradePointColor : levelUpColor);
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        BuildRing(ring, 0.15f);

        float timer = 0f;

        while (timer < levelUpDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, levelUpDuration));
            float pulse = Mathf.Sin(t * Mathf.PI);
            transform.localScale = baseScale * Mathf.Lerp(1f, levelUpScalePulse, pulse);

            float radius = Mathf.Lerp(0.15f, levelUpRingRadius, t);
            BuildRing(ring, radius);

            Color color = gainedUpgradePoint ? upgradePointColor : levelUpColor;
            color.a *= 1f - t;
            ring.startColor = color;
            ring.endColor = color;

            yield return null;
        }

        transform.localScale = baseScale;

        if (ringObject != null)
            Destroy(ringObject);

        levelUpRoutine = null;
    }

    private void EnsureUpgradePointRing()
    {
        if (upgradePointRing != null)
            return;

        GameObject ringObject = new GameObject("UpgradePoint_AvailableRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.up * idleRingYOffset;

        upgradePointRing = ringObject.AddComponent<LineRenderer>();
        upgradePointRing.loop = true;
        upgradePointRing.useWorldSpace = false;
        upgradePointRing.widthMultiplier = 0.045f;
        upgradePointRing.positionCount = 64;
        upgradePointRing.material = CreateTransparentMaterial(upgradePointColor);
        upgradePointRing.startColor = upgradePointColor;
        upgradePointRing.endColor = upgradePointColor;
        upgradePointRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        upgradePointRing.receiveShadows = false;
        BuildRing(upgradePointRing, idleRingRadius);
        upgradePointRing.gameObject.SetActive(false);
    }

    private void UpdateUpgradePointIdleAnimation()
    {
        if (!upgradePointAvailable || !animateWhenUpgradePointAvailable)
            return;

        EnsureUpgradePointRing();

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * idlePulseSpeed);
        float scale = 1f + pulse * idleScaleStrength;

        if (levelUpRoutine == null)
            transform.localScale = baseScale * scale;

        if (upgradePointRing != null)
        {
            float radius = idleRingRadius * (1f + pulse * 0.12f);
            BuildRing(upgradePointRing, radius);

            Color color = upgradePointColor;
            color.a = Mathf.Lerp(95f / 255f, 210f / 255f, pulse);
            upgradePointRing.startColor = color;
            upgradePointRing.endColor = color;
        }
    }

    private void BuildRing(LineRenderer ring, float radius)
    {
        if (ring == null)
            return;

        int segments = Mathf.Max(24, ring.positionCount);
        ring.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.renderQueue = 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }
}
