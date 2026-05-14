using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public enum EnemyRole
{
    Standard,
    Runner,
    Tank,
    Knight,
    Mage,
    Learner,
    AllRounder,
    MiniBoss,
    Boss
}

public enum EnemyVariantType
{
    Normal,
    Chaos
}

public class Enemy : MonoBehaviour
{
    [Header("Enemy Role")]
    public EnemyRole enemyRole = EnemyRole.Standard;
    public bool autoApplyRoleStats = true;

    [Header("Enemy Variant V1")]
    public EnemyVariantType enemyVariantType = EnemyVariantType.Normal;
    public bool autoApplyVariantStats = true;

    [Header("Chaos Level Scaling V1")]
    public bool useChaosLevelScaling = true;
    public float chaosHealthBonusPerLevel = 0.06f;
    public float chaosBossHealthBonusPerLevel = 0.03f;

    [Header("Chaos Variant Stats V1")]
    public float chaosStandardEffectDamageMultiplier = 0.90f;
    public float chaosStandardSlowResistanceBonus = 0.12f;
    public float chaosRunnerHealthMultiplier = 1.35f;
    public float chaosTankRegenPerSecond = 0.85f;
    public float chaosKnightSpeedMultiplier = 1.12f;
    public int chaosMageTeleportTilesBonus = 1;
    public float chaosMageTeleportCooldownMultiplier = 0.80f;
    public float chaosLearnerDotDamageMultiplier = 0.65f;
    public float chaosLearnerDotHealMultiplier = 0.25f;
    public float chaosAllRounderArmorInterval = 4f;
    public int chaosAllRounderMaxArmorBonus = 4;

    [Header("Chaos Variant Visual V1")]
    public Color chaosVariantPulseColor = new Color32(165, 70, 255, 255);
    public float chaosVariantPulseSpeed = 3f;
    [Range(0f, 1f)]
    public float chaosVariantPulseStrength = 0.45f;

    [Header("Chaos Wave Effects V1")]
    public bool hasChaosWaveEffect = false;
    public string chaosWaveEffectSummary = "";
    public float appliedChaosWaveHealthMultiplier = 1f;
    public int appliedChaosWaveArmorBonus = 0;
    public float appliedChaosWaveEffectDamageMultiplier = 1f;
    public float appliedChaosWaveSlowResistanceBonus = 0f;

    [Header("Base Stats")]
    public float maxHealth = 10f;
    public float currentHealth = 10f;
    public float speed = 2f;
    public int armor = 0;
    public int baseDamage = 1;

    [Header("Rewards")]
    public int goldReward = 4;
    public int killXPReward = 2;
    public int assistXPReward = 1;

    [Header("Global XP Rewards")]
    public int globalXPReward = 0;

    [Header("Wave Scaling")]
    public bool useWaveScaling = true;
    public int scalingStartWave = 11;
    public float healthScalingPerWave = 0.16f;
    public float rewardScalingPerWave = 0f;
    public float xpScalingPerWave = 0f;
    public float speedScalingPerWave = 0.012f;
    public float maxNormalSpeedBonus = 0.50f;
    public int armorBonusEveryWaves = 9;
    public int maxArmorBonus = 12;
    public int baseDamageBonusEveryWaves = 18;
    public int maxBaseDamageBonus = 6;
    public float tankHealthScalingBonus = 1.55f;
    public float knightHealthScalingBonus = 1.30f;
    public float learnerHealthScalingBonus = 1.35f;
    public float allRounderHealthScalingBonus = 1.40f;
    public float miniBossHealthScalingBonus = 2.00f;
    public float bossHealthScalingBonus = 2.50f;

    private int waveScalingAppliedForWave = -1;
    private int chaosScalingAppliedForWave = -1;
    private int chaosScalingAppliedForLevel = -1;
    private bool variantStatsApplied = false;
    private float chaosAllRounderArmorTimer = 0f;
    private int chaosAllRounderArmorBonusApplied = 0;

    [Header("Flags")]
    public bool isElite = false;
    public bool isMiniBoss = false;
    public bool isBoss = false;

    [Header("Effect Rules")]
    public bool immuneToEffects = false;

    [Range(0f, 0.9f)]
    public float slowResistance = 0f;

    [Range(0f, 2f)]
    public float effectDamageMultiplier = 1f;

    [Header("Mage Teleport")]
    public bool canTeleportOnHit = false;
    public int teleportTilesForward = 1;
    public float teleportCooldown = 3f;
    public float teleportExhaustSlowMultiplier = 0.55f;
    public float teleportExhaustDuration = 0.6f;

    [Header("Default Effect Values")]
    public float defaultBurnDamagePerSecond = 1f;
    public float defaultPoisonDamagePerSecond = 1f;
    public float defaultEffectTickRate = 0.5f;

    [Header("Health Bar")]
    public bool useHealthBar = true;
    public bool hideLegacyHealthTexts = true;
    public EnemyHealthBar healthBar;

    [Header("Visual Feedback")]
    public Renderer[] enemyRenderers;
    public Color defaultColor = Color.white;
    public Color burnColor = Color.red;
    public Color poisonColor = new Color(0.55f, 0f, 1f);
    public Color slowColor = new Color(0.35f, 0.75f, 1f, 1f);

    [Range(0f, 1f)]
    public float slowVisualBlend = 0.35f;

    [Header("Role Visual Colors")]
    public bool useRoleColors = true;
    public Color standardRoleColor = new Color32(220, 220, 220, 255);
    public Color runnerRoleColor = new Color32(255, 210, 60, 255);
    public Color tankRoleColor = new Color32(70, 160, 80, 255);
    public Color knightRoleColor = new Color32(145, 145, 155, 255);
    public Color mageRoleColor = new Color32(80, 190, 255, 255);
    public Color learnerRoleColor = new Color32(190, 90, 255, 255);
    public Color allRounderRoleColor = new Color32(255, 145, 60, 255);
    public Color miniBossRoleColor = new Color32(255, 85, 55, 255);
    public Color bossRoleColor = new Color32(120, 25, 25, 255);

    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private readonly Dictionary<Tower, float> contributingTowers = new Dictionary<Tower, float>();

    public GameManager gameManager;
    public System.Action<Enemy> OnEnemyFinished;

    private int currentPathIndex = 0;
    private float distanceTravelled = 0f;
    private bool roleStatsApplied = false;
    private bool isDead = false;
    private bool reachedBase = false;
    private bool isBurning = false;
    private bool isPoisoned = false;
    private bool isBleeding = false;
    private bool isSlowed = false;
    private bool isTowerSlowed = false;
    private bool isTileSlowed = false;
    private bool isBeingKnockedBack = false;
    private EnemyHealthBarEffectMode healthBarEffectMode = EnemyHealthBarEffectMode.None;
    private float currentSlowMultiplier = 1f;
    private float towerSlowMultiplier = 1f;
    private float tileSlowMultiplier = 1f;
    private float temporarySpeedMultiplier = 1f;
    private float nextTeleportTime = 0f;
    private Coroutine burnRoutine;
    private Coroutine poisonRoutine;
    private Coroutine bleedRoutine;
    private Coroutine towerSlowRoutine;
    private Coroutine tileSlowRoutine;
    private Coroutine knockBackRoutine;
    private Coroutine teleportExhaustRoutine;

    public float DistanceTravelled => distanceTravelled;
    public int CurrentPathIndex => currentPathIndex;
    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public bool IsBurning => isBurning;
    public bool IsPoisoned => isPoisoned;
    public bool IsBleeding => isBleeding;
    public bool IsSlowed => isSlowed;
    public bool HasAnyEffect => isBurning || isPoisoned || isBleeding || isSlowed;

    private void Awake()
    {
        CacheRenderersIfNeeded();
        CacheDefaultColor();

        if (hideLegacyHealthTexts)
            DisableLegacyHealthTexts();

        EnsureHealthBar();
    }

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (autoApplyRoleStats && !roleStatsApplied)
            ApplyRoleStats(enemyRole);

        ApplyWaveScalingFromGameManager();
        ApplyChaosLevelScalingFromGameManager();

        if (autoApplyVariantStats && !variantStatsApplied)
            ApplyVariantStats(enemyVariantType);

        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        RefreshHealthBar();
        UpdateVisualColor();
    }

    private void Update()
    {
        MoveAlongPath();
        UpdateChaosVariantRuntimeEffects();
    }

    public void Initialize(EnemyRole role, List<Vector3> path, GameManager manager = null)
    {
        Initialize(role, EnemyVariantType.Normal, path, manager);
    }

    public void Initialize(EnemyRole role, EnemyVariantType variantType, List<Vector3> path, GameManager manager = null)
    {
        enemyRole = role;
        enemyVariantType = variantType;
        gameManager = manager;
        ApplyRoleStats(enemyRole);
        ApplyWaveScalingFromGameManager();
        ApplyChaosLevelScalingFromGameManager();
        ApplyVariantStats(enemyVariantType);
        SetPath(path);
        RefreshHealthBar();
        UpdateVisualColor();
    }

    public void SetPath(List<Vector3> newPath)
    {
        pathPoints.Clear();

        if (newPath == null || newPath.Count == 0)
        {
            Debug.LogWarning("Enemy hat keinen gültigen Path bekommen.");
            return;
        }

        pathPoints.AddRange(newPath);
        currentPathIndex = 0;
        distanceTravelled = 0f;
        reachedBase = false;
        transform.position = pathPoints[0];

        if (pathPoints.Count > 1)
            currentPathIndex = 1;
    }

    public void SetStats(float newMaxHealth, float newSpeed, int newGoldReward, int newKillXPReward)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = maxHealth;
        speed = Mathf.Max(0.1f, newSpeed);
        goldReward = Mathf.Max(0, newGoldReward);
        killXPReward = Mathf.Max(0, newKillXPReward);
        RefreshHealthBar();
    }

    public void SetStats(float newMaxHealth, float newSpeed, int newGoldReward, int newKillXPReward, int newBaseDamage)
    {
        SetStats(newMaxHealth, newSpeed, newGoldReward, newKillXPReward);
        baseDamage = Mathf.Max(1, newBaseDamage);
    }

    public void ApplyRoleStats(EnemyRole role)
    {
        roleStatsApplied = true;
        enemyRole = role;
        isMiniBoss = false;
        isBoss = false;
        immuneToEffects = false;
        slowResistance = 0f;
        effectDamageMultiplier = 1f;
        canTeleportOnHit = false;
        teleportTilesForward = 1;
        teleportCooldown = 3f;
        teleportExhaustSlowMultiplier = 0.55f;
        teleportExhaustDuration = 0.6f;
        armor = 0;
        baseDamage = 1;
        isElite = false;
        globalXPReward = 0;
        ResetVariantRuntimeStats();

        switch (role)
        {
            case EnemyRole.Standard:
                maxHealth = 10f;
                speed = 2f;
                armor = 0;
                baseDamage = 1;
                goldReward = 4;
                killXPReward = 2;
                assistXPReward = 1;
                break;

            case EnemyRole.Runner:
                maxHealth = 6f;
                speed = 3.25f;
                armor = 0;
                baseDamage = 1;
                goldReward = 3;
                killXPReward = 3;
                assistXPReward = 1;
                slowResistance = 0.15f;
                break;

            case EnemyRole.Tank:
                maxHealth = 26f;
                speed = 1.10f;
                armor = 0;
                baseDamage = 1;
                goldReward = 8;
                killXPReward = 8;
                assistXPReward = 2;
                effectDamageMultiplier = 1.2f;
                break;

            case EnemyRole.Knight:
                maxHealth = 16f;
                speed = 1.75f;
                armor = 3;
                baseDamage = 1;
                goldReward = 8;
                killXPReward = 7;
                assistXPReward = 2;
                break;

            case EnemyRole.Mage:
                maxHealth = 6f;
                speed = 1.8f;
                armor = 0;
                baseDamage = 1;
                goldReward = 5;
                killXPReward = 5;
                assistXPReward = 2;
                canTeleportOnHit = true;
                teleportTilesForward = 1;
                teleportCooldown = 3.0f;
                teleportExhaustSlowMultiplier = 0.55f;
                teleportExhaustDuration = 0.6f;
                break;

            case EnemyRole.Learner:
                maxHealth = 11f;
                speed = 1.85f;
                armor = 0;
                baseDamage = 1;
                goldReward = 6;
                killXPReward = 7;
                assistXPReward = 2;
                immuneToEffects = true;
                break;

            case EnemyRole.AllRounder:
                maxHealth = 24f;
                speed = 2.2f;
                armor = 2;
                baseDamage = 2;
                goldReward = 12;
                killXPReward = 10;
                assistXPReward = 3;
                slowResistance = 0.2f;
                break;

            case EnemyRole.MiniBoss:
                maxHealth = 50f;
                speed = 0.95f;
                armor = 0;
                baseDamage = 2;
                goldReward = 18;
                killXPReward = 15;
                assistXPReward = 4;
                globalXPReward = 2;
                isMiniBoss = true;
                break;

            case EnemyRole.Boss:
                maxHealth = 220f;
                speed = 0.95f;
                armor = 4;
                baseDamage = 5;
                goldReward = 75;
                killXPReward = 40;
                assistXPReward = 8;
                globalXPReward = 5;
                isBoss = true;
                break;
        }

        currentHealth = maxHealth;
        healthBarEffectMode = EnemyHealthBarEffectMode.None;
        ApplyRoleVisualColor();
        RefreshHealthBar();
    }

    private void ApplyWaveScalingFromGameManager()
    {
        if (!useWaveScaling || gameManager == null)
            return;

        ApplyWaveScaling(gameManager.waveNumber);
    }

    private void ApplyWaveScaling(int waveNumber)
    {
        if (!useWaveScaling)
            return;

        int safeWave = Mathf.Max(1, waveNumber);

        if (safeWave < scalingStartWave)
            return;

        if (waveScalingAppliedForWave == safeWave)
            return;

        waveScalingAppliedForWave = safeWave;

        int scalingWave = safeWave - scalingStartWave + 1;
        float healthMultiplier = 1f + scalingWave * healthScalingPerWave;
        float rewardMultiplier = 1f + scalingWave * rewardScalingPerWave;
        float xpMultiplier = 1f + scalingWave * xpScalingPerWave;
        float speedMultiplier = 1f + Mathf.Min(maxNormalSpeedBonus, scalingWave * speedScalingPerWave);
        int armorBonus = 0;
        int baseDamageBonus = 0;

        if (armorBonusEveryWaves > 0)
            armorBonus = Mathf.Min(maxArmorBonus, scalingWave / armorBonusEveryWaves);

        if (baseDamageBonusEveryWaves > 0)
            baseDamageBonus = Mathf.Min(maxBaseDamageBonus, scalingWave / baseDamageBonusEveryWaves);

        ApplyRoleSpecificScaling(scalingWave, ref healthMultiplier, ref rewardMultiplier, ref xpMultiplier, ref speedMultiplier, ref armorBonus, ref baseDamageBonus);

        maxHealth = Mathf.Max(1f, maxHealth * healthMultiplier);
        currentHealth = maxHealth;
        speed = Mathf.Max(0.1f, speed * speedMultiplier);
        armor += armorBonus;
        baseDamage += baseDamageBonus;
        goldReward = Mathf.Max(0, Mathf.RoundToInt(goldReward * rewardMultiplier));
        killXPReward = Mathf.Max(0, Mathf.RoundToInt(killXPReward * xpMultiplier));
        assistXPReward = Mathf.Max(0, Mathf.RoundToInt(assistXPReward * xpMultiplier));

        if (globalXPReward > 0)
            globalXPReward = Mathf.Max(1, Mathf.RoundToInt(globalXPReward * xpMultiplier));

        if (canTeleportOnHit)
        {
            float cooldownReduction = Mathf.Min(0.25f, scalingWave * 0.005f);
            teleportCooldown = Mathf.Max(1.2f, teleportCooldown * (1f - cooldownReduction));
        }

        if (slowResistance > 0f)
            slowResistance = Mathf.Clamp01(slowResistance + Mathf.Min(0.2f, scalingWave * 0.004f));

        Debug.Log(enemyRole + " scaled for Wave " + safeWave + " | HP: " + maxHealth.ToString("0") + " | Speed: " + speed.ToString("0.00") + " | Armor: " + armor + " | Gold: " + goldReward + " | XP: " + killXPReward);
    }

    private void ApplyRoleSpecificScaling(int scalingWave, ref float healthMultiplier, ref float rewardMultiplier, ref float xpMultiplier, ref float speedMultiplier, ref int armorBonus, ref int baseDamageBonus)
    {
        switch (enemyRole)
        {
            case EnemyRole.Standard:
                break;

            case EnemyRole.Runner:
                healthMultiplier *= 0.85f;
                speedMultiplier = 1f + Mathf.Min(0.55f, scalingWave * speedScalingPerWave * 1.5f);
                armorBonus = 0;
                break;

            case EnemyRole.Tank:
                healthMultiplier *= tankHealthScalingBonus;
                speedMultiplier = 1f + Mathf.Min(0.18f, scalingWave * speedScalingPerWave * 0.5f);
                break;

            case EnemyRole.Knight:
                healthMultiplier *= knightHealthScalingBonus;
                armorBonus += Mathf.Min(maxArmorBonus, scalingWave / Mathf.Max(1, armorBonusEveryWaves));
                speedMultiplier = 1f + Mathf.Min(0.22f, scalingWave * speedScalingPerWave * 0.6f);
                break;

            case EnemyRole.Mage:
                healthMultiplier *= 1.0f;
                speedMultiplier = 1f + Mathf.Min(0.30f, scalingWave * speedScalingPerWave);
                break;

            case EnemyRole.Learner:
                healthMultiplier *= learnerHealthScalingBonus;
                speedMultiplier = 1f + Mathf.Min(0.30f, scalingWave * speedScalingPerWave * 0.8f);
                break;

            case EnemyRole.AllRounder:
                healthMultiplier *= allRounderHealthScalingBonus;
                armorBonus += Mathf.Min(maxArmorBonus, scalingWave / Mathf.Max(1, armorBonusEveryWaves));
                break;

            case EnemyRole.MiniBoss:
                healthMultiplier *= miniBossHealthScalingBonus;
                speedMultiplier = 1f + Mathf.Min(0.20f, scalingWave * speedScalingPerWave * 0.5f);
                baseDamageBonus += Mathf.Min(2, scalingWave / 20);
                break;

            case EnemyRole.Boss:
                healthMultiplier *= bossHealthScalingBonus;
                speedMultiplier = 1f + Mathf.Min(0.14f, scalingWave * speedScalingPerWave * 0.35f);
                baseDamageBonus += Mathf.Min(3, scalingWave / 15);
                armorBonus += Mathf.Min(maxArmorBonus, scalingWave / Mathf.Max(1, armorBonusEveryWaves));
                break;
        }
    }

    private void MoveAlongPath()
    {
        if (isDead || reachedBase || isBeingKnockedBack)
            return;

        if (pathPoints == null || pathPoints.Count == 0)
            return;

        if (currentPathIndex >= pathPoints.Count)
        {
            ReachBase();
            return;
        }

        Vector3 oldPosition = transform.position;
        Vector3 targetPosition = pathPoints[currentPathIndex];
        float finalSpeed = Mathf.Max(0.05f, speed * currentSlowMultiplier * temporarySpeedMultiplier);

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, finalSpeed * Time.deltaTime);
        distanceTravelled += Vector3.Distance(oldPosition, transform.position);

        if (Vector3.Distance(transform.position, targetPosition) <= 0.02f)
        {
            currentPathIndex++;
            SpecialPathTileEffect.TryApplyAtWorldPosition(targetPosition, this);

            if (currentPathIndex >= pathPoints.Count)
                ReachBase();
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, Tower sourceTower)
    {
        TakeDamageInternal(damage, sourceTower, false, true);
    }

    public void TakeDamage(float damage, Tower sourceTower, bool ignoreArmor)
    {
        TakeDamageInternal(damage, sourceTower, ignoreArmor, true);
    }

    private void TakeDamageInternal(float damage, Tower sourceTower, bool ignoreArmor, bool canTriggerOnHitEffects)
    {
        if (isDead || reachedBase)
            return;

        if (damage <= 0f)
            return;

        RegisterContributor(sourceTower, damage);
        float finalDamage = CalculateFinalDamage(damage, ignoreArmor);
        float previousHealth = currentHealth;
        currentHealth -= finalDamage;
        currentHealth = Mathf.Max(0f, currentHealth);
        float appliedDamage = Mathf.Max(0f, previousHealth - currentHealth);

        if (sourceTower != null)
            sourceTower.RegisterDamage(appliedDamage);

        RefreshHealthBar();

        if (canTriggerOnHitEffects)
            TryMageTeleport();

        if (currentHealth <= 0f)
            Die(sourceTower);
    }

    private float CalculateFinalDamage(float damage, bool ignoreArmor)
    {
        if (ignoreArmor || armor <= 0)
            return damage;

        return Mathf.Max(1f, damage - armor);
    }

    public void ApplyBurn(float duration)
    {
        ApplyBurn(defaultBurnDamagePerSecond, duration, null);
    }

    public void ApplyBurn(float duration, Tower sourceTower)
    {
        ApplyBurn(defaultBurnDamagePerSecond, duration, sourceTower);
    }

    public void ApplyBurn(float damagePerSecond, float duration)
    {
        ApplyBurn(damagePerSecond, duration, null);
    }

    public void ApplyBurn(float damagePerSecond, float duration, Tower sourceTower)
    {
        if (isDead || reachedBase || immuneToEffects)
            return;

        damagePerSecond = Mathf.Max(0.1f, damagePerSecond);
        duration = Mathf.Max(0.1f, duration);
        RegisterContributor(sourceTower, 0.1f);
        RegisterHealthBarDamageEffect(EnemyHealthBarEffectMode.Burn);

        if (burnRoutine != null)
            StopCoroutine(burnRoutine);

        burnRoutine = StartCoroutine(DamageOverTimeRoutine(damagePerSecond, duration, sourceTower, false, EnemyDamageOverTimeType.Burn));
    }

    public void ApplyPoison(float duration)
    {
        ApplyPoison(defaultPoisonDamagePerSecond, duration, null);
    }

    public void ApplyPoison(float duration, Tower sourceTower)
    {
        ApplyPoison(defaultPoisonDamagePerSecond, duration, sourceTower);
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        ApplyPoison(damagePerSecond, duration, null);
    }

    public void ApplyPoison(float damagePerSecond, float duration, Tower sourceTower)
    {
        if (isDead || reachedBase || immuneToEffects)
            return;

        damagePerSecond = Mathf.Max(0.1f, damagePerSecond);
        duration = Mathf.Max(0.1f, duration);
        RegisterContributor(sourceTower, 0.1f);
        RegisterHealthBarDamageEffect(EnemyHealthBarEffectMode.Poison);

        if (poisonRoutine != null)
            StopCoroutine(poisonRoutine);

        poisonRoutine = StartCoroutine(DamageOverTimeRoutine(damagePerSecond, duration, sourceTower, true, EnemyDamageOverTimeType.Poison));
    }

    public void ApplySlow(float slowMultiplier, float duration)
    {
        ApplySlow(slowMultiplier, duration, null);
    }

    public void ApplySlow(float slowMultiplier, float duration, Tower sourceTower)
    {
        if (isDead || reachedBase || immuneToEffects)
            return;

        RegisterContributor(sourceTower, 0.1f);

        if (sourceTower != null)
        {
            if (towerSlowRoutine != null)
                StopCoroutine(towerSlowRoutine);

            towerSlowRoutine = StartCoroutine(SlowRoutine(slowMultiplier, duration, true));
            return;
        }

        if (tileSlowRoutine != null)
            StopCoroutine(tileSlowRoutine);

        tileSlowRoutine = StartCoroutine(SlowRoutine(slowMultiplier, duration, false));
    }

    public void ApplyBleed(float damagePerSecond, float duration)
    {
        ApplyBleed(damagePerSecond, duration, null);
    }

    public void ApplyBleed(float damagePerSecond, float duration, Tower sourceTower)
    {
        if (isDead || reachedBase || immuneToEffects)
            return;

        damagePerSecond = Mathf.Max(0.1f, damagePerSecond);
        duration = Mathf.Max(0.1f, duration);
        RegisterContributor(sourceTower, 0.1f);
        RegisterHealthBarDamageEffect(EnemyHealthBarEffectMode.Bleed);

        if (bleedRoutine != null)
            StopCoroutine(bleedRoutine);

        bleedRoutine = StartCoroutine(DamageOverTimeRoutine(damagePerSecond, duration, sourceTower, false, EnemyDamageOverTimeType.Bleed));
    }

    private IEnumerator DamageOverTimeRoutine(float damagePerSecond, float duration, Tower sourceTower, bool ignoreArmor, EnemyDamageOverTimeType dotType)
    {
        if (dotType == EnemyDamageOverTimeType.Burn)
            isBurning = true;

        if (dotType == EnemyDamageOverTimeType.Poison)
            isPoisoned = true;

        if (dotType == EnemyDamageOverTimeType.Bleed)
            isBleeding = true;

        UpdateVisualColor();
        float elapsed = 0f;
        float tickRate = Mathf.Max(0.1f, defaultEffectTickRate);

        while (elapsed < duration && !isDead && !reachedBase)
        {
            float tickDamage = damagePerSecond * tickRate * effectDamageMultiplier;

            if (tickDamage > 0f)
            {
                if (IsChaosVariantRole(EnemyRole.Learner) && (dotType == EnemyDamageOverTimeType.Burn || dotType == EnemyDamageOverTimeType.Poison || dotType == EnemyDamageOverTimeType.Bleed))
                {
                    float reducedDamage = tickDamage * Mathf.Clamp01(chaosLearnerDotDamageMultiplier);
                    TakeDamageInternal(reducedDamage, sourceTower, ignoreArmor, false);
                    HealChaosVariant(tickDamage * Mathf.Max(0f, chaosLearnerDotHealMultiplier));
                }
                else
                {
                    TakeDamageInternal(tickDamage, sourceTower, ignoreArmor, false);
                }
            }

            elapsed += tickRate;
            yield return new WaitForSeconds(tickRate);
        }

        if (dotType == EnemyDamageOverTimeType.Burn)
            isBurning = false;

        if (dotType == EnemyDamageOverTimeType.Poison)
            isPoisoned = false;

        if (dotType == EnemyDamageOverTimeType.Bleed)
            isBleeding = false;

        RefreshHealthBarEffectModeAfterEffectEnded();
        RefreshHealthBar();
        UpdateVisualColor();
    }

    private IEnumerator SlowRoutine(float slowMultiplier, float duration, bool fromTower)
    {
        float clampedSlow = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
        float resistedSlow = Mathf.Lerp(clampedSlow, 1f, slowResistance);

        if (fromTower)
        {
            towerSlowMultiplier = resistedSlow;
            isTowerSlowed = true;
        }
        else
        {
            tileSlowMultiplier = resistedSlow;
            isTileSlowed = true;
        }

        RefreshStackedSlow();
        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

        if (fromTower)
        {
            towerSlowMultiplier = 1f;
            isTowerSlowed = false;
            towerSlowRoutine = null;
        }
        else
        {
            tileSlowMultiplier = 1f;
            isTileSlowed = false;
            tileSlowRoutine = null;
        }

        RefreshStackedSlow();
    }

    private void RefreshStackedSlow()
    {
        isSlowed = isTowerSlowed || isTileSlowed;
        currentSlowMultiplier = isSlowed ? Mathf.Clamp(towerSlowMultiplier * tileSlowMultiplier, 0.1f, 1f) : 1f;
        UpdateVisualColor();
    }

    public bool KnockBackPathTiles(int tilesBack, float duration)
    {
        if (isDead || reachedBase || immuneToEffects || pathPoints == null || pathPoints.Count == 0)
            return false;

        int currentTileIndex = Mathf.Clamp(currentPathIndex - 1, 0, pathPoints.Count - 1);
        int targetIndex = Mathf.Clamp(currentTileIndex - Mathf.Max(1, tilesBack), 0, pathPoints.Count - 1);

        if (targetIndex >= currentTileIndex)
            return false;

        if (knockBackRoutine != null)
            StopCoroutine(knockBackRoutine);

        knockBackRoutine = StartCoroutine(KnockBackRoutine(targetIndex, duration));
        return true;
    }

    private IEnumerator KnockBackRoutine(int targetIndex, float duration)
    {
        isBeingKnockedBack = true;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = pathPoints[targetIndex];
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);

        while (elapsed < safeDuration && !isDead && !reachedBase)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        if (!isDead && !reachedBase)
        {
            transform.position = targetPosition;
            currentPathIndex = Mathf.Clamp(targetIndex + 1, 0, pathPoints.Count);
        }

        isBeingKnockedBack = false;
        knockBackRoutine = null;
    }

    private void TryMageTeleport()
    {
        if (!canTeleportOnHit)
            return;

        if (Time.time < nextTeleportTime)
            return;

        if (pathPoints == null || pathPoints.Count == 0)
            return;

        if (currentPathIndex >= pathPoints.Count)
            return;

        nextTeleportTime = Time.time + teleportCooldown;
        int newIndex = Mathf.Clamp(currentPathIndex + teleportTilesForward, currentPathIndex, pathPoints.Count - 1);
        transform.position = pathPoints[newIndex];
        currentPathIndex = Mathf.Clamp(newIndex + 1, 0, pathPoints.Count);

        if (teleportExhaustRoutine != null)
            StopCoroutine(teleportExhaustRoutine);

        teleportExhaustRoutine = StartCoroutine(TeleportExhaustRoutine());

        if (currentPathIndex >= pathPoints.Count)
            ReachBase();
    }

    private IEnumerator TeleportExhaustRoutine()
    {
        temporarySpeedMultiplier = Mathf.Clamp(teleportExhaustSlowMultiplier, 0.1f, 1f);
        yield return new WaitForSeconds(teleportExhaustDuration);
        temporarySpeedMultiplier = 1f;
    }

    private void RegisterContributor(Tower tower, float contributionValue)
    {
        if (tower == null)
            return;

        if (contributingTowers.ContainsKey(tower))
            contributingTowers[tower] += contributionValue;
        else
            contributingTowers.Add(tower, contributionValue);
    }

    private void Die(Tower killingTower)
    {
        if (isDead)
            return;

        isDead = true;

        if (killingTower != null)
            killingTower.RegisterKill(enemyRole);

        NotifyGameManagerEnemyKilled();
        GiveGoldReward();
        GiveXPRewards(killingTower);
        GiveGlobalXPReward();
        OnEnemyFinished?.Invoke(this);
        Destroy(gameObject);
    }

    private void NotifyGameManagerEnemyKilled()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager != null)
            gameManager.RegisterEnemyKilled(this);
    }

    private void GiveGoldReward()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager != null)
            gameManager.AddGold(goldReward, true, RunGoldSource.EnemyKill);
    }

    private void GiveXPRewards(Tower killingTower)
    {
        if (killingTower != null)
            GiveTowerXP(killingTower, killXPReward);

        foreach (KeyValuePair<Tower, float> entry in contributingTowers)
        {
            Tower tower = entry.Key;

            if (tower == null)
                continue;

            if (tower == killingTower)
                continue;

            tower.RegisterAssist(enemyRole);
            GiveTowerXP(tower, assistXPReward);
        }
    }

    private void GiveGlobalXPReward()
    {
        if (globalXPReward <= 0)
            return;

        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude);

        foreach (Tower tower in towers)
        {
            if (tower == null)
                continue;

            GiveTowerXP(tower, globalXPReward);
        }

        Debug.Log(enemyRole + " defeated. All towers gained +" + globalXPReward + " XP.");
    }

    private void GiveTowerXP(Tower tower, int amount)
    {
        if (tower == null || amount <= 0)
            return;

        int finalAmount = amount;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager != null)
        {
            ChaosJusticeManager chaosJusticeManager = gameManager.GetChaosJusticeManager();

            if (chaosJusticeManager != null)
                finalAmount = chaosJusticeManager.ApplyXPRewardModifiers(amount);
        }

        if (finalAmount <= 0)
            return;

        MethodInfo method =
            tower.GetType().GetMethod("AddXP") ??
            tower.GetType().GetMethod("AddExperience") ??
            tower.GetType().GetMethod("GainXP") ??
            tower.GetType().GetMethod("GainExperience");

        if (method == null)
        {
            Debug.LogWarning("Tower hat keine XP-Methode gefunden: AddXP / AddExperience / GainXP / GainExperience");
            return;
        }

        method.Invoke(tower, new object[] { finalAmount });
    }

    private void ReachBase()
    {
        if (reachedBase || isDead)
            return;

        reachedBase = true;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        TryDamageBase();
        OnEnemyFinished?.Invoke(this);
        Destroy(gameObject);
    }

    private void TryDamageBase()
    {
        if (gameManager == null)
            return;

        MethodInfo method =
            gameManager.GetType().GetMethod("EnemyReachedBase", new[] { typeof(Enemy) }) ??
            gameManager.GetType().GetMethod("OnEnemyReachedBase", new[] { typeof(Enemy) });

        if (method != null)
        {
            method.Invoke(gameManager, new object[] { this });
            return;
        }

        method =
            gameManager.GetType().GetMethod("LoseLives", new[] { typeof(int) }) ??
            gameManager.GetType().GetMethod("LoseLife", new[] { typeof(int) }) ??
            gameManager.GetType().GetMethod("TakeDamage", new[] { typeof(int) }) ??
            gameManager.GetType().GetMethod("RemoveLives", new[] { typeof(int) });

        if (method != null)
        {
            method.Invoke(gameManager, new object[] { baseDamage });
            return;
        }

        Debug.LogWarning("Keine passende GameManager-Methode für Base-Schaden gefunden.");
    }

    public float GetPathProgress()
    {
        return currentPathIndex + distanceTravelled * 0.001f;
    }

    public bool HasEffect(EnemyEffectCheck effectCheck)
    {
        switch (effectCheck)
        {
            case EnemyEffectCheck.Burn:
                return isBurning;
            case EnemyEffectCheck.Poison:
                return isPoisoned;
            case EnemyEffectCheck.Slow:
                return isSlowed;
            case EnemyEffectCheck.Bleed:
                return isBleeding;
            case EnemyEffectCheck.Any:
                return HasAnyEffect;
            default:
                return false;
        }
    }

    private void RegisterHealthBarDamageEffect(EnemyHealthBarEffectMode effectMode)
    {
        if (effectMode == EnemyHealthBarEffectMode.None)
            return;

        if (healthBarEffectMode == EnemyHealthBarEffectMode.None)
            healthBarEffectMode = effectMode;

        RefreshHealthBar();
    }

    private void RefreshHealthBarEffectModeAfterEffectEnded()
    {
        if (healthBarEffectMode == EnemyHealthBarEffectMode.Burn && isBurning)
            return;

        if (healthBarEffectMode == EnemyHealthBarEffectMode.Poison && isPoisoned)
            return;

        if (healthBarEffectMode == EnemyHealthBarEffectMode.Bleed && isBleeding)
            return;

        if (isBurning)
            healthBarEffectMode = EnemyHealthBarEffectMode.Burn;
        else if (isPoisoned)
            healthBarEffectMode = EnemyHealthBarEffectMode.Poison;
        else if (isBleeding)
            healthBarEffectMode = EnemyHealthBarEffectMode.Bleed;
        else
            healthBarEffectMode = EnemyHealthBarEffectMode.None;
    }


    private void ApplyChaosLevelScalingFromGameManager()
    {
        if (!useChaosLevelScaling)
            return;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
            return;

        ChaosJusticeManager chaosJusticeManager = gameManager.GetChaosJusticeManager();

        if (chaosJusticeManager == null)
            return;

        int chaosLevel = Mathf.Max(0, chaosJusticeManager.GetChaosLevel());

        if (chaosLevel <= 0)
            return;

        int wave = Mathf.Max(1, gameManager.waveNumber);

        if (chaosScalingAppliedForWave == wave && chaosScalingAppliedForLevel == chaosLevel)
            return;

        chaosScalingAppliedForWave = wave;
        chaosScalingAppliedForLevel = chaosLevel;

        float perLevel = (isBoss || isMiniBoss || enemyRole == EnemyRole.Boss || enemyRole == EnemyRole.MiniBoss)
            ? chaosBossHealthBonusPerLevel
            : chaosHealthBonusPerLevel;

        float multiplier = 1f + Mathf.Max(0f, perLevel) * chaosLevel;
        maxHealth = Mathf.Max(1f, maxHealth * multiplier);
        currentHealth = maxHealth;
    }

    public void SetVariantType(EnemyVariantType variantType)
    {
        enemyVariantType = variantType;
        variantStatsApplied = false;
        ApplyVariantStats(enemyVariantType);
    }

    public void ApplyVariantStats(EnemyVariantType variantType)
    {
        enemyVariantType = variantType;
        variantStatsApplied = true;

        if (variantType != EnemyVariantType.Chaos)
        {
            UpdateVisualColor();
            return;
        }

        ApplyChaosVariantStatsForRole(enemyRole);
        currentHealth = maxHealth;
        RefreshHealthBar();
        UpdateVisualColor();
    }

    public void ApplySpawnEntryEffects(EnemySpawnEntry entry)
    {
        if (entry == null || !entry.HasChaosWaveEntryEffect())
        {
            UpdateVisualColor();
            return;
        }

        hasChaosWaveEffect = true;
        chaosWaveEffectSummary = string.IsNullOrEmpty(entry.waveEffectSummary)
            ? "Chaos-Wave-Effekt"
            : entry.waveEffectSummary;

        appliedChaosWaveHealthMultiplier = Mathf.Max(0.1f, entry.waveHealthMultiplier);
        appliedChaosWaveArmorBonus = Mathf.Max(0, entry.waveArmorBonus);
        appliedChaosWaveEffectDamageMultiplier = Mathf.Max(0.1f, entry.waveEffectDamageMultiplier);
        appliedChaosWaveSlowResistanceBonus = Mathf.Max(0f, entry.waveSlowResistanceBonus);

        if (appliedChaosWaveHealthMultiplier != 1f)
        {
            float oldHealthPercent = maxHealth <= 0f ? 1f : Mathf.Clamp01(currentHealth / maxHealth);
            maxHealth = Mathf.Max(1f, maxHealth * appliedChaosWaveHealthMultiplier);
            currentHealth = Mathf.Max(1f, maxHealth * oldHealthPercent);
        }

        if (appliedChaosWaveArmorBonus > 0)
            armor = Mathf.Max(0, armor + appliedChaosWaveArmorBonus);

        if (appliedChaosWaveEffectDamageMultiplier != 1f)
            effectDamageMultiplier = Mathf.Clamp(effectDamageMultiplier * appliedChaosWaveEffectDamageMultiplier, 0.1f, 2f);

        if (appliedChaosWaveSlowResistanceBonus > 0f)
            slowResistance = Mathf.Clamp01(slowResistance + appliedChaosWaveSlowResistanceBonus);

        RefreshHealthBar();
        UpdateVisualColor();
    }

    private void ResetVariantRuntimeStats()
    {
        variantStatsApplied = false;
        chaosAllRounderArmorTimer = 0f;
        chaosAllRounderArmorBonusApplied = 0;
        hasChaosWaveEffect = false;
        chaosWaveEffectSummary = "";
        appliedChaosWaveHealthMultiplier = 1f;
        appliedChaosWaveArmorBonus = 0;
        appliedChaosWaveEffectDamageMultiplier = 1f;
        appliedChaosWaveSlowResistanceBonus = 0f;
    }

    private void ApplyChaosVariantStatsForRole(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.Standard:
                effectDamageMultiplier *= Mathf.Clamp(chaosStandardEffectDamageMultiplier, 0.25f, 1f);
                slowResistance = Mathf.Clamp01(slowResistance + Mathf.Max(0f, chaosStandardSlowResistanceBonus));
                break;

            case EnemyRole.Runner:
                maxHealth = Mathf.Max(1f, maxHealth * Mathf.Max(1f, chaosRunnerHealthMultiplier));
                break;

            case EnemyRole.Tank:
                break;

            case EnemyRole.Knight:
                speed = Mathf.Max(0.1f, speed * Mathf.Max(1f, chaosKnightSpeedMultiplier));
                break;

            case EnemyRole.Mage:
                canTeleportOnHit = true;
                teleportTilesForward = Mathf.Max(1, teleportTilesForward + Mathf.Max(0, chaosMageTeleportTilesBonus));
                teleportCooldown = Mathf.Max(1.0f, teleportCooldown * Mathf.Clamp(chaosMageTeleportCooldownMultiplier, 0.25f, 1f));
                break;

            case EnemyRole.Learner:
                immuneToEffects = false;
                slowResistance = Mathf.Clamp01(Mathf.Max(slowResistance, 0.35f));
                break;

            case EnemyRole.AllRounder:
                break;
        }
    }

    private void UpdateChaosVariantRuntimeEffects()
    {
        if (!HasChaosVisualSignal())
            return;

        if (IsChaosVariantRole(EnemyRole.Tank))
            RegenerateChaosTank();

        if (IsChaosVariantRole(EnemyRole.AllRounder))
            GrowChaosAllRounderArmor();

        UpdateVisualColor();
    }

    private void RegenerateChaosTank()
    {
        if (isDead || reachedBase)
            return;

        if (chaosTankRegenPerSecond <= 0f)
            return;

        if (currentHealth >= maxHealth)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + chaosTankRegenPerSecond * Time.deltaTime);
        RefreshHealthBar();
    }

    private void GrowChaosAllRounderArmor()
    {
        if (isDead || reachedBase)
            return;

        if (chaosAllRounderMaxArmorBonus <= 0)
            return;

        if (chaosAllRounderArmorBonusApplied >= chaosAllRounderMaxArmorBonus)
            return;

        chaosAllRounderArmorTimer += Time.deltaTime;

        if (chaosAllRounderArmorTimer < Mathf.Max(0.5f, chaosAllRounderArmorInterval))
            return;

        chaosAllRounderArmorTimer = 0f;
        chaosAllRounderArmorBonusApplied++;
        armor += 1;
    }

    private void HealChaosVariant(float amount)
    {
        if (amount <= 0f || isDead || reachedBase)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RefreshHealthBar();
    }

    public bool IsChaosVariant()
    {
        return enemyVariantType == EnemyVariantType.Chaos;
    }

    public bool HasChaosVisualSignal()
    {
        return IsChaosVariant() || hasChaosWaveEffect;
    }

    private bool IsChaosVariantRole(EnemyRole role)
    {
        return IsChaosVariant() && enemyRole == role;
    }

    public string GetVariantDisplayName()
    {
        if (IsChaosVariant())
            return "Chaos " + enemyRole;

        if (hasChaosWaveEffect)
            return enemyRole + " (Chaos-Wave)";

        return enemyRole.ToString();
    }

    private void EnsureHealthBar()
    {
        if (!useHealthBar)
            return;

        if (healthBar == null)
            healthBar = GetComponentInChildren<EnemyHealthBar>(true);

        if (healthBar == null)
        {
            GameObject healthBarObject = new GameObject("EnemyHealthBar");
            healthBarObject.transform.SetParent(transform, false);
            healthBar = healthBarObject.AddComponent<EnemyHealthBar>();
        }

        if (healthBar != null)
            healthBar.Initialize(this);
    }

    private void RefreshHealthBar()
    {
        if (!useHealthBar)
            return;

        EnsureHealthBar();

        if (healthBar != null)
            healthBar.Refresh();
    }

    private void DisableLegacyHealthTexts()
    {
        TMP_Text[] legacyTexts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text legacyText in legacyTexts)
        {
            if (legacyText == null)
                continue;

            if (legacyText.GetComponentInParent<EnemyHealthBar>() != null)
                continue;

            legacyText.gameObject.SetActive(false);
        }

        TextMesh[] legacyTextMeshes = GetComponentsInChildren<TextMesh>(true);

        foreach (TextMesh legacyTextMesh in legacyTextMeshes)
        {
            if (legacyTextMesh == null)
                continue;

            if (legacyTextMesh.GetComponentInParent<EnemyHealthBar>() != null)
                continue;

            legacyTextMesh.gameObject.SetActive(false);
        }
    }

    public EnemyHealthBarEffectMode GetHealthBarEffectMode()
    {
        return healthBarEffectMode;
    }

    private void ApplyRoleVisualColor()
    {
        if (!useRoleColors)
            return;

        defaultColor = GetRoleColor(enemyRole);
        UpdateVisualColor();
    }

    private Color GetRoleColor(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.Standard:
                return standardRoleColor;
            case EnemyRole.Runner:
                return runnerRoleColor;
            case EnemyRole.Tank:
                return tankRoleColor;
            case EnemyRole.Knight:
                return knightRoleColor;
            case EnemyRole.Mage:
                return mageRoleColor;
            case EnemyRole.Learner:
                return learnerRoleColor;
            case EnemyRole.AllRounder:
                return allRounderRoleColor;
            case EnemyRole.MiniBoss:
                return miniBossRoleColor;
            case EnemyRole.Boss:
                return bossRoleColor;
            default:
                return standardRoleColor;
        }
    }

    private void UpdateVisualColor()
    {
        if (enemyRenderers == null || enemyRenderers.Length == 0)
            return;

        Color targetColor = defaultColor;

        if (HasChaosVisualSignal())
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, chaosVariantPulseSpeed));
            float blend = Mathf.Clamp01(chaosVariantPulseStrength) * pulse;
            Color pulseColor = chaosVariantPulseColor;
            pulseColor.a = defaultColor.a;
            targetColor = Color.Lerp(defaultColor, pulseColor, blend);
        }

        if (isBleeding)
        {
            Color bleedVisualColor = burnColor;
            bleedVisualColor.a = defaultColor.a;
            targetColor = Color.Lerp(targetColor, bleedVisualColor, 0.25f);
        }

        if (isSlowed)
        {
            Color slowVisualColor = slowColor;
            slowVisualColor.a = defaultColor.a;
            targetColor = Color.Lerp(targetColor, slowVisualColor, slowVisualBlend);
        }

        for (int i = 0; i < enemyRenderers.Length; i++)
        {
            if (enemyRenderers[i] == null)
                continue;

            if (enemyRenderers[i].material != null)
                enemyRenderers[i].material.color = targetColor;
        }
    }

    private void CacheRenderersIfNeeded()
    {
        if (enemyRenderers != null && enemyRenderers.Length > 0)
            return;

        enemyRenderers = GetComponentsInChildren<Renderer>();
    }

    private void CacheDefaultColor()
    {
        if (enemyRenderers == null || enemyRenderers.Length == 0)
            return;

        for (int i = 0; i < enemyRenderers.Length; i++)
        {
            if (enemyRenderers[i] == null)
                continue;

            if (enemyRenderers[i].material == null)
                continue;

            defaultColor = enemyRenderers[i].material.color;
            return;
        }
    }

    public int GetCurrentHealth()
    {
        return Mathf.CeilToInt(currentHealth);
    }

    public bool IsEliteTarget()
    {
        return isElite || isMiniBoss || isBoss || enemyRole == EnemyRole.MiniBoss || enemyRole == EnemyRole.Boss;
    }

    public bool HasBurn()
    {
        return isBurning;
    }

    public bool HasPoison()
    {
        return isPoisoned;
    }

    public bool HasBleed()
    {
        return isBleeding;
    }

    public bool HasSlow()
    {
        return isSlowed;
    }
}

public enum EnemyEffectCheck
{
    Burn,
    Poison,
    Slow,
    Bleed,
    Any
}

public enum EnemyHealthBarEffectMode
{
    None,
    Burn,
    Poison,
    Bleed
}

public enum EnemyDamageOverTimeType
{
    Burn,
    Poison,
    Bleed
}
