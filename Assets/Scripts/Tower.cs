using System.Collections.Generic;
using UnityEngine;

public enum TowerTargetMode
{
    First,
    Last,
    Closest,
    Strongest,
    Elite,
    NoBurn,
    NoPoison,
    NoSlow,
    NoBleed
}

public enum TowerRole
{
    Basic,
    Rapid,
    Heavy,
    Fire,
    Slow,
    Poison,
    Sniper,
    Alchemist,
    Lightning,
    Mortar,
    Spike
}

public class Tower : MonoBehaviour
{
    [Header("Info")]
    public string towerName = "Basic Tower";
    public TowerRole towerRole = TowerRole.Basic;
    public bool autoApplyTowerRoleStats = true;

    [Header("Stats")]
    public int damage = 1;
    public float range = 3f;
    public float fireRate = 1f;

    [Header("Build / Sell")]
    public int originalBuildCost = 0;
    public float sellRefundPercent = 0.5f;
    public Vector2Int builtGridPosition;
    public bool hasBuildGridPosition = false;

    [Header("Targeting")]
    public TowerTargetMode targetMode = TowerTargetMode.First;

    [Header("Progression")]
    public int level = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 30;
    public int upgradePoints = 0;
    public float evolutionStatMultiplier = 1f;

    [Header("Combat Stats")]
    public int totalKills = 0;
    public int currentWaveKills = 0;

    public int totalAssists = 0;
    public int currentWaveAssists = 0;

    public float totalDamageDealt = 0f;
    public float currentWaveDamageDealt = 0f;

    [Header("Progression Settings")]
    public int baseXPToNextLevel = 30;
    public float xpGrowthMultiplier = 1.35f;
    public int xpFlatIncreasePerLevel = 8;

    public int upgradePointLevelInterval = 5;
    public int metaProgressionLevelInterval = 10;
    public int visualTierLevelInterval = 10;

    [Header("Prepared For Later")]
    public int metaProgressionPoints = 0;
    public int visualTier = 0;

    [Header("QoL Visual Feedback")]
    public bool autoCreateRangeIndicator = true;
    public bool autoCreateTowerVisualFeedback = true;
    public bool autoCreateVisualTierController = true;

    [Header("Click Hitbox")]
    public bool autoTightenClickCollider = true;
    public float clickColliderRadius = 0.36f;
    public float clickColliderHeight = 1.05f;
    public float clickColliderCenterY = 0.25f;

    [Header("Visual Tier Stat Bonus")]
    public bool applyVisualTierStatBonus = true;
    [Tooltip("0.25 bedeutet: pro Visual Tier +25% auf die ursprünglichen Basiswerte des Towers, ohne kaufbare Upgrades mitzuberechnen.")]
    public float visualTierStatBonusPerTier = 0.25f;

    private TowerRangeIndicator rangeIndicator;
    private TowerVisualFeedback towerVisualFeedback;
    private TowerVisualTierController visualTierController;
    private TowerVisualTierPrefabController visualTierPrefabController;
    private TowerAimController aimController;
    private Enemy currentTarget;
    private int basicMasteryShotCounter = 0;
    private int basicMasteryShotsSinceKill = 0;
    private int rapidMasteryShotCounter = 0;
    private int heavyMasteryShotCounter = 0;
    private int sniperMasteryShotCounter = 0;
    private int slowMasteryShotCounter = 0;

    private bool visualTierBaseStatsCaptured = false;
    private int baseDamageBeforeVisualTier = 0;
    private float baseRangeBeforeVisualTier = 0f;
    private float baseFireRateBeforeVisualTier = 0f;
    private int baseBurnDamageBeforeVisualTier = 0;
    private float baseBurnDurationBeforeVisualTier = 0f;
    private int basePoisonDamageBeforeVisualTier = 0;
    private float basePoisonDurationBeforeVisualTier = 0f;
    private float baseSlowAmountBeforeVisualTier = 1f;
    private float baseSlowDurationBeforeVisualTier = 0f;
    private int visualTierStatsAppliedForTier = -1;

    [Header("Gold Upgrade Costs")]
    public int damageUpgradeCost = 50;
    public int rangeUpgradeCost = 50;
    public int fireRateUpgradeCost = 50;
    public int effectUpgradeCost = 50;
    public int goldUpgradeCostIncrease = 25;

    [Tooltip("Multipliziert die Upgrade-Kosten nach jedem Kauf. 1.0 = nur linearer Kostenanstieg.")]
    public float goldUpgradeCostMultiplier = 1.25f;

    [Header("Gold Upgrade Power")]
    public int damageIncreasePerGoldUpgrade = 1;
    public float rangeIncreasePerGoldUpgrade = 0.25f;
    public float fireRateIncreasePerGoldUpgrade = 0.25f;

    public int burnDamageIncreasePerGoldUpgrade = 1;
    public int poisonDamageIncreasePerGoldUpgrade = 1;
    public float effectDurationIncreasePerGoldUpgrade = 0.25f;
    public float slowAmountIncreasePerGoldUpgrade = 0.02f;
    public float slowDurationIncreasePerGoldUpgrade = 0.25f;

    [Header("Upgrade Point Settings")]
    public int upgradePointCostPerUpgrade = 1;
    public int pointUpgradePowerMultiplier = 3;

    [Header("Gold Upgrade Levels")]
    public int damageGoldUpgradeLevel = 0;
    public int rangeGoldUpgradeLevel = 0;
    public int fireRateGoldUpgradeLevel = 0;
    public int effectGoldUpgradeLevel = 0;

    [Header("Point Upgrade Levels")]
    public int damagePointUpgradeLevel = 0;
    public int rangePointUpgradeLevel = 0;
    public int fireRatePointUpgradeLevel = 0;
    public int effectPointUpgradeLevel = 0;

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

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    private float fireCooldown = 0f;

    private void Awake()
    {
        if (autoApplyTowerRoleStats)
            ApplyTowerRoleStats(ResolveTowerRoleForPreset());

        ConfigureClickCollider();
        CaptureVisualTierBaseStatsIfNeeded();
        EnsureQoLVisualComponents();
        ApplyVisualTierStatBonusIfNeeded(true);
        RefreshVisualTierShape();
        RefreshUpgradePointAvailableVisual();

        RefreshProgressionValues();

        currentWaveKills = 0;
        currentWaveAssists = 0;
        currentWaveDamageDealt = 0f;

        WaveEventBus.WaveStarted += HandleWaveStarted;
    }

    private void OnDestroy()
    {
        WaveEventBus.WaveStarted -= HandleWaveStarted;
    }

    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        currentXP = Mathf.Max(0, currentXP);
        xpToNextLevel = Mathf.Max(1, xpToNextLevel);
        upgradePoints = Mathf.Max(0, upgradePoints);
        evolutionStatMultiplier = Mathf.Max(1f, evolutionStatMultiplier);

        baseXPToNextLevel = Mathf.Max(1, baseXPToNextLevel);
        xpGrowthMultiplier = Mathf.Max(1f, xpGrowthMultiplier);
        xpFlatIncreasePerLevel = Mathf.Max(0, xpFlatIncreasePerLevel);

        upgradePointLevelInterval = Mathf.Max(1, upgradePointLevelInterval);
        metaProgressionLevelInterval = Mathf.Max(1, metaProgressionLevelInterval);
        visualTierLevelInterval = Mathf.Max(1, visualTierLevelInterval);

        metaProgressionPoints = Mathf.Max(0, metaProgressionPoints);
        visualTier = Mathf.Max(0, visualTier);

        totalKills = Mathf.Max(0, totalKills);
        currentWaveKills = Mathf.Max(0, currentWaveKills);
        totalAssists = Mathf.Max(0, totalAssists);
        currentWaveAssists = Mathf.Max(0, currentWaveAssists);
        totalDamageDealt = Mathf.Max(0f, totalDamageDealt);
        currentWaveDamageDealt = Mathf.Max(0f, currentWaveDamageDealt);

        damageUpgradeCost = Mathf.Max(0, damageUpgradeCost);
        rangeUpgradeCost = Mathf.Max(0, rangeUpgradeCost);
        fireRateUpgradeCost = Mathf.Max(0, fireRateUpgradeCost);
        effectUpgradeCost = Mathf.Max(0, effectUpgradeCost);
        goldUpgradeCostIncrease = Mathf.Max(0, goldUpgradeCostIncrease);
        goldUpgradeCostMultiplier = Mathf.Max(1f, goldUpgradeCostMultiplier);

        damageIncreasePerGoldUpgrade = Mathf.Max(0, damageIncreasePerGoldUpgrade);
        rangeIncreasePerGoldUpgrade = Mathf.Max(0f, rangeIncreasePerGoldUpgrade);
        fireRateIncreasePerGoldUpgrade = Mathf.Max(0f, fireRateIncreasePerGoldUpgrade);

        burnDamageIncreasePerGoldUpgrade = Mathf.Max(0, burnDamageIncreasePerGoldUpgrade);
        poisonDamageIncreasePerGoldUpgrade = Mathf.Max(0, poisonDamageIncreasePerGoldUpgrade);
        effectDurationIncreasePerGoldUpgrade = Mathf.Max(0f, effectDurationIncreasePerGoldUpgrade);
        slowAmountIncreasePerGoldUpgrade = Mathf.Max(0f, slowAmountIncreasePerGoldUpgrade);
        slowDurationIncreasePerGoldUpgrade = Mathf.Max(0f, slowDurationIncreasePerGoldUpgrade);

        upgradePointCostPerUpgrade = Mathf.Max(1, upgradePointCostPerUpgrade);
        pointUpgradePowerMultiplier = Mathf.Max(1, pointUpgradePowerMultiplier);

        clickColliderRadius = Mathf.Clamp(clickColliderRadius, 0.05f, 1.0f);
        clickColliderHeight = Mathf.Clamp(clickColliderHeight, clickColliderRadius * 2f, 2.5f);
        clickColliderCenterY = Mathf.Clamp(clickColliderCenterY, -0.5f, 1.5f);

        damageGoldUpgradeLevel = Mathf.Max(0, damageGoldUpgradeLevel);
        rangeGoldUpgradeLevel = Mathf.Max(0, rangeGoldUpgradeLevel);
        fireRateGoldUpgradeLevel = Mathf.Max(0, fireRateGoldUpgradeLevel);
        effectGoldUpgradeLevel = Mathf.Max(0, effectGoldUpgradeLevel);

        damagePointUpgradeLevel = Mathf.Max(0, damagePointUpgradeLevel);
        rangePointUpgradeLevel = Mathf.Max(0, rangePointUpgradeLevel);
        fireRatePointUpgradeLevel = Mathf.Max(0, fireRatePointUpgradeLevel);
        effectPointUpgradeLevel = Mathf.Max(0, effectPointUpgradeLevel);

        damage = Mathf.Max(0, damage);
        range = Mathf.Max(0f, range);
        fireRate = Mathf.Max(0.01f, fireRate);

        burnDamage = Mathf.Max(0, burnDamage);
        burnDuration = Mathf.Max(0f, burnDuration);

        poisonDamage = Mathf.Max(0, poisonDamage);
        poisonDuration = Mathf.Max(0f, poisonDuration);

        slowAmount = Mathf.Clamp01(slowAmount);
        slowDuration = Mathf.Max(0f, slowDuration);

        RefreshProgressionValues();
    }

    private void ConfigureClickCollider()
    {
        if (!autoTightenClickCollider)
            return;

        CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();

        if (capsuleCollider == null)
            return;

        float radius = Mathf.Clamp(clickColliderRadius, 0.05f, 1.0f);
        float height = Mathf.Clamp(clickColliderHeight, radius * 2f, 2.5f);

        capsuleCollider.direction = 1;
        capsuleCollider.radius = radius;
        capsuleCollider.height = height;
        capsuleCollider.center = new Vector3(0f, clickColliderCenterY, 0f);
    }

    private void Update()
    {
        fireCooldown -= Time.deltaTime;

        Enemy target = FindTarget();
        SetCurrentTarget(target);

        int shotsThisFrame = 0;
        int maxShotsPerFrame = 5;

        while (fireCooldown <= 0f && shotsThisFrame < maxShotsPerFrame)
        {
            if (target == null)
            {
                fireCooldown = Mathf.Max(fireCooldown, 0f);
                break;
            }

            Shoot(target);

            float shotInterval = 1f / Mathf.Max(0.01f, GetEffectiveFireRate());
            fireCooldown += shotInterval;

            shotsThisFrame++;

            if (fireCooldown <= 0f && shotsThisFrame < maxShotsPerFrame)
            {
                target = FindTarget();
                SetCurrentTarget(target);
            }
        }

        if (shotsThisFrame >= maxShotsPerFrame)
        {
            fireCooldown = Mathf.Max(fireCooldown, 0f);
        }
    }

    private void SetCurrentTarget(Enemy target)
    {
        currentTarget = target;
        UpdateAimController();
    }

    private void UpdateAimController()
    {
        if (aimController == null)
            aimController = GetComponent<TowerAimController>();

        if (aimController == null)
            return;

        if (aimController.tower == null)
            aimController.tower = this;

        aimController.AimAt(currentTarget);
    }

    private TowerRole ResolveTowerRoleForPreset()
    {
        string lowerName = string.IsNullOrEmpty(towerName) ? "" : towerName.ToLowerInvariant();

        if (lowerName.Contains("lightning"))
            return TowerRole.Lightning;

        if (lowerName.Contains("mortar"))
            return TowerRole.Mortar;

        if (lowerName.Contains("spike"))
            return TowerRole.Spike;

        if (lowerName.Contains("rapid"))
            return TowerRole.Rapid;

        if (lowerName.Contains("sniper"))
            return TowerRole.Sniper;

        if (lowerName.Contains("heavy"))
            return TowerRole.Heavy;

        if (lowerName.Contains("alchemist"))
            return TowerRole.Alchemist;

        if (lowerName.Contains("fire"))
            return TowerRole.Fire;

        if (lowerName.Contains("slow"))
            return TowerRole.Slow;

        if (lowerName.Contains("poison"))
            return TowerRole.Poison;

        return towerRole;
    }

    private void ApplyTowerRoleStats(TowerRole role)
    {
        towerRole = role;

        appliesBurn = false;
        appliesPoison = false;
        appliesSlow = false;

        burnDamage = 0;
        burnDuration = 0f;
        poisonDamage = 0;
        poisonDuration = 0f;
        slowAmount = 1f;
        slowDuration = 0f;

        upgradePointCostPerUpgrade = 1;
        pointUpgradePowerMultiplier = 3;

        switch (role)
        {
            case TowerRole.Basic:
                towerName = "Basic Tower";
                damage = 5;
                range = 3.0f;
                fireRate = 1.45f;
                targetMode = TowerTargetMode.First;

                damageUpgradeCost = 45;
                rangeUpgradeCost = 50;
                fireRateUpgradeCost = 60;
                effectUpgradeCost = 999;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.25f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.20f;
                break;

            case TowerRole.Rapid:
                towerName = "Rapid Tower";
                damage = 2;
                range = 2.3f;
                fireRate = 2.8f;
                targetMode = TowerTargetMode.First;

                damageUpgradeCost = 55;
                rangeUpgradeCost = 55;
                fireRateUpgradeCost = 80;
                effectUpgradeCost = 999;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.25f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.25f;
                break;


            case TowerRole.Sniper:
                towerName = "Sniper Tower";
                damage = 42;
                range = 5.2f;
                fireRate = 0.30f;
                targetMode = TowerTargetMode.Elite;

                damageUpgradeCost = 95;
                rangeUpgradeCost = 80;
                fireRateUpgradeCost = 90;
                effectUpgradeCost = 999;
                goldUpgradeCostIncrease = 30;
                goldUpgradeCostMultiplier = 1.35f;

                damageIncreasePerGoldUpgrade = 8;
                rangeIncreasePerGoldUpgrade = 0.35f;
                fireRateIncreasePerGoldUpgrade = 0.05f;
                break;

            case TowerRole.Heavy:
                towerName = "Heavy Tower";
                damage = 16;
                range = 2.8f;
                fireRate = 0.42f;
                targetMode = TowerTargetMode.Strongest;

                damageUpgradeCost = 80;
                rangeUpgradeCost = 65;
                fireRateUpgradeCost = 75;
                effectUpgradeCost = 999;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.30f;

                damageIncreasePerGoldUpgrade = 2;
                rangeIncreasePerGoldUpgrade = 0.20f;
                fireRateIncreasePerGoldUpgrade = 0.10f;
                break;

            case TowerRole.Fire:
                towerName = "Fire Tower";
                damage = 2;
                range = 2.5f;
                fireRate = 1.15f;
                targetMode = TowerTargetMode.NoBurn;

                appliesBurn = true;
                burnDamage = 2;
                burnDuration = 4.0f;

                damageUpgradeCost = 65;
                rangeUpgradeCost = 60;
                fireRateUpgradeCost = 80;
                effectUpgradeCost = 80;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.30f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.20f;
                burnDamageIncreasePerGoldUpgrade = 1;
                effectDurationIncreasePerGoldUpgrade = 0.25f;
                break;

            case TowerRole.Slow:
                towerName = "Slow Tower";
                damage = 1;
                range = 2.6f;
                fireRate = 0.95f;
                targetMode = TowerTargetMode.NoSlow;

                appliesSlow = true;
                slowAmount = 0.55f;
                slowDuration = 2.2f;

                damageUpgradeCost = 55;
                rangeUpgradeCost = 70;
                fireRateUpgradeCost = 80;
                effectUpgradeCost = 75;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.25f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.25f;
                slowAmountIncreasePerGoldUpgrade = 0.015f;
                slowDurationIncreasePerGoldUpgrade = 0.20f;
                break;


            case TowerRole.Alchemist:
                towerName = "Alchemist Tower";
                damage = 1;
                range = 2.6f;
                fireRate = 0.75f;
                targetMode = TowerTargetMode.NoPoison;

                appliesPoison = true;
                poisonDamage = 1;
                poisonDuration = 4.5f;

                appliesSlow = true;
                slowAmount = 0.78f;
                slowDuration = 1.3f;

                damageUpgradeCost = 60;
                rangeUpgradeCost = 60;
                fireRateUpgradeCost = 80;
                effectUpgradeCost = 90;
                goldUpgradeCostIncrease = 30;
                goldUpgradeCostMultiplier = 1.30f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.20f;
                poisonDamageIncreasePerGoldUpgrade = 1;
                slowAmountIncreasePerGoldUpgrade = 0.015f;
                slowDurationIncreasePerGoldUpgrade = 0.15f;
                effectDurationIncreasePerGoldUpgrade = 0.25f;
                break;


            case TowerRole.Lightning:
                towerName = "Lightning Tower";
                damage = 7;
                range = 3.0f;
                fireRate = 0.85f;
                targetMode = TowerTargetMode.First;

                appliesSlow = true;
                slowAmount = 0.75f;
                slowDuration = 1.25f;

                damageUpgradeCost = 65;
                rangeUpgradeCost = 65;
                fireRateUpgradeCost = 80;
                effectUpgradeCost = 80;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.30f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.18f;
                slowAmountIncreasePerGoldUpgrade = 0.01f;
                slowDurationIncreasePerGoldUpgrade = 0.15f;
                break;

            case TowerRole.Mortar:
                towerName = "Mortar Tower";
                damage = 10;
                range = 4.0f;
                fireRate = 0.30f;
                targetMode = TowerTargetMode.Strongest;

                damageUpgradeCost = 85;
                rangeUpgradeCost = 75;
                fireRateUpgradeCost = 90;
                effectUpgradeCost = 999;
                goldUpgradeCostIncrease = 30;
                goldUpgradeCostMultiplier = 1.35f;

                damageIncreasePerGoldUpgrade = 2;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.06f;
                break;

            case TowerRole.Spike:
                towerName = "Spike Tower";
                damage = 2;
                range = 1.5f;
                fireRate = 1.1f;
                targetMode = TowerTargetMode.Closest;

                damageUpgradeCost = 55;
                rangeUpgradeCost = 60;
                fireRateUpgradeCost = 70;
                effectUpgradeCost = 75;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.25f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.20f;
                fireRateIncreasePerGoldUpgrade = 0.20f;
                effectDurationIncreasePerGoldUpgrade = 0.30f;
                break;

            case TowerRole.Poison:
                towerName = "Poison Tower";
                damage = 1;
                range = 2.7f;
                fireRate = 0.80f;
                targetMode = TowerTargetMode.NoPoison;

                appliesPoison = true;
                poisonDamage = 4;
                poisonDuration = 6.5f;

                damageUpgradeCost = 60;
                rangeUpgradeCost = 60;
                fireRateUpgradeCost = 80;
                effectUpgradeCost = 90;
                goldUpgradeCostIncrease = 25;
                goldUpgradeCostMultiplier = 1.30f;

                damageIncreasePerGoldUpgrade = 1;
                rangeIncreasePerGoldUpgrade = 0.25f;
                fireRateIncreasePerGoldUpgrade = 0.25f;
                poisonDamageIncreasePerGoldUpgrade = 2;
                effectDurationIncreasePerGoldUpgrade = 0.45f;
                break;
        }
    }


    public void InitializeBuildData(int buildCost, Vector2Int gridPosition)
    {
        originalBuildCost = Mathf.Max(0, buildCost);
        builtGridPosition = gridPosition;
        hasBuildGridPosition = true;
    }

    public int GetSellRefundAmount()
    {
        return Mathf.FloorToInt(Mathf.Max(0, originalBuildCost) * 0.5f);
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveKills = 0;
        currentWaveAssists = 0;
        currentWaveDamageDealt = 0f;
    }

    public void RegisterKill(EnemyRole killedRole)
    {
        totalKills++;
        currentWaveKills++;

        if (TowerMasteryManager.TryGetActive(out TowerMasteryManager towerMasteryManager))
            towerMasteryManager.RecordTowerKill(this, killedRole);

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
        {
            rapidMasteryManager.RecordRapidKill(this, killedRole);

            float readyBonus = rapidMasteryManager.GetCleanRetargetReadyBonus();
            if (readyBonus > 0f)
            {
                float interval = 1f / Mathf.Max(0.01f, GetEffectiveFireRate());
                fireCooldown = Mathf.Min(fireCooldown, interval * (1f - readyBonus));
            }
        }

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
            heavyMasteryManager.RecordHeavyKill(this, killedRole);

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
        {
            sniperMasteryManager.RecordSniperKill(this, killedRole);

            float readyBonus = sniperMasteryManager.GetPreciseRedirectReadyBonus();
            if (readyBonus > 0f)
            {
                float interval = 1f / Mathf.Max(0.01f, GetEffectiveFireRate());
                fireCooldown = Mathf.Min(fireCooldown, interval * (1f - readyBonus));
            }
        }

        if (towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            lightningMasteryManager.RecordLightningKill(this, killedRole);

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            mortarMasteryManager.RecordMortarKill(this, killedRole);

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            spikeMasteryManager.RecordSpikeKill(this, killedRole);

        if (towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
            fireMasteryManager.RecordFireKill(this, killedRole);

        if (towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
            alchemistMasteryManager.RecordAlchemistKill(this, killedRole);

        if (towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMasteryManager))
            poisonMasteryManager.RecordPoisonKill(this, killedRole);

        if (towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out SlowTowerMasteryManager slowMasteryManager))
            slowMasteryManager.RecordSlowKill(this, killedRole);

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
        {
            basicMasteryShotsSinceKill = 0;
            masteryManager.RecordBasicKill(this, killedRole);

            float readyBonus = masteryManager.GetCleanRetargetReadyBonus();
            if (readyBonus > 0f)
            {
                float interval = 1f / Mathf.Max(0.01f, GetEffectiveFireRate());
                fireCooldown = Mathf.Min(fireCooldown, interval * (1f - readyBonus));
            }
        }
    }

    public void RegisterAssist(EnemyRole assistedRole)
    {
        totalAssists++;
        currentWaveAssists++;

        if (TowerMasteryManager.TryGetActive(out TowerMasteryManager towerMasteryManager))
            towerMasteryManager.RecordTowerAssist(this, assistedRole);

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
            rapidMasteryManager.RecordRapidAssist(this, assistedRole);

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
            heavyMasteryManager.RecordHeavyAssist(this, assistedRole);

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
            sniperMasteryManager.RecordSniperAssist(this, assistedRole);

        if (towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            lightningMasteryManager.RecordLightningAssist(this, assistedRole);

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            mortarMasteryManager.RecordMortarAssist(this, assistedRole);

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            spikeMasteryManager.RecordSpikeAssist(this, assistedRole);

        if (towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
            fireMasteryManager.RecordFireAssist(this, assistedRole);

        if (towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
            alchemistMasteryManager.RecordAlchemistAssist(this, assistedRole);

        if (towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMasteryManager))
            poisonMasteryManager.RecordPoisonAssist(this, assistedRole);

        if (towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out SlowTowerMasteryManager slowMasteryManager))
            slowMasteryManager.RecordSlowAssist(this, assistedRole);

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
            masteryManager.RecordBasicAssist(this, assistedRole);
    }

    public void RegisterDamage(float amount)
    {
        if (amount <= 0f)
            return;

        totalDamageDealt += amount;
        currentWaveDamageDealt += amount;

        if (TowerMasteryManager.TryGetActive(out TowerMasteryManager towerMasteryManager))
            towerMasteryManager.RecordTowerDamage(this, amount);

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
            rapidMasteryManager.RecordRapidDamage(this, amount);

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
            heavyMasteryManager.RecordHeavyDamage(this, amount);

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
            sniperMasteryManager.RecordSniperDamage(this, amount);

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            mortarMasteryManager.RecordMortarDamage(this, amount);

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            spikeMasteryManager.RecordSpikeDamage(this, amount);

        if (towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out SlowTowerMasteryManager slowMasteryManager))
            slowMasteryManager.RecordSlowDamage(this, amount);

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
            masteryManager.RecordBasicDamage(this, amount);
    }

    private void RefreshProgressionValues()
    {
        xpToNextLevel = CalculateXPToNextLevel(level);
        visualTier = CalculateVisualTier(level);
    }

    private int CalculateXPToNextLevel(int currentLevel)
    {
        int safeLevel = Mathf.Max(1, currentLevel);

        float scaledXP = baseXPToNextLevel * Mathf.Pow(xpGrowthMultiplier, safeLevel - 1);
        int flatXP = (safeLevel - 1) * xpFlatIncreasePerLevel;

        int calculatedXP = Mathf.RoundToInt(scaledXP + flatXP);

        return Mathf.Max(1, calculatedXP);
    }

    private int CalculateVisualTier(int currentLevel)
    {
        return currentLevel / visualTierLevelInterval;
    }

    private bool ShouldGainUpgradePoint(int reachedLevel)
    {
        return reachedLevel % upgradePointLevelInterval == 0;
    }

    private bool ShouldGainMetaProgressionPoint(int reachedLevel)
    {
        return reachedLevel % metaProgressionLevelInterval == 0;
    }

    private Enemy FindTarget()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (!IsTargetModeAvailable(targetMode))
        {
            targetMode = TowerTargetMode.First;
        }

        switch (targetMode)
        {
            case TowerTargetMode.First:
                return FindFirstEnemy(enemies);
            case TowerTargetMode.Last:
                return FindLastEnemy(enemies);
            case TowerTargetMode.Closest:
                return FindClosestEnemy(enemies);
            case TowerTargetMode.Strongest:
                return FindStrongestEnemy(enemies);
            case TowerTargetMode.Elite:
                return FindEliteEnemy(enemies);
            case TowerTargetMode.NoBurn:
                return FindNoBurnEnemy(enemies);
            case TowerTargetMode.NoPoison:
                return FindNoPoisonEnemy(enemies);
            case TowerTargetMode.NoSlow:
                return FindNoSlowEnemy(enemies);
            case TowerTargetMode.NoBleed:
                return FindNoBleedEnemy(enemies);
            default:
                return FindFirstEnemy(enemies);
        }
    }

    private Enemy FindFirstEnemy(Enemy[] enemies)
    {
        Enemy bestTarget = null;
        float bestProgress = -Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            if (!IsEnemyInRange(enemy))
                continue;

            float progress = enemy.GetPathProgress();

            if (progress > bestProgress)
            {
                bestProgress = progress;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    private Enemy FindLastEnemy(Enemy[] enemies)
    {
        Enemy bestTarget = null;
        float bestProgress = Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            if (!IsEnemyInRange(enemy))
                continue;

            float progress = enemy.GetPathProgress();

            if (progress < bestProgress)
            {
                bestProgress = progress;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    private Enemy FindClosestEnemy(Enemy[] enemies)
    {
        Enemy bestTarget = null;
        float bestDistance = Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            if (!IsEnemyInRange(enemy))
                continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    private Enemy FindStrongestEnemy(Enemy[] enemies)
    {
        Enemy bestTarget = null;
        int highestHealth = -1;

        foreach (Enemy enemy in enemies)
        {
            if (!IsEnemyInRange(enemy))
                continue;

            int health = enemy.GetCurrentHealth();

            if (health > highestHealth)
            {
                highestHealth = health;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    private Enemy FindEliteEnemy(Enemy[] enemies)
    {
        Enemy bestEliteTarget = null;
        float bestEliteProgress = -Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            if (!IsEnemyInRange(enemy))
                continue;

            if (!enemy.IsEliteTarget())
                continue;

            float progress = enemy.GetPathProgress();

            if (progress > bestEliteProgress)
            {
                bestEliteProgress = progress;
                bestEliteTarget = enemy;
            }
        }

        if (bestEliteTarget != null)
            return bestEliteTarget;

        return FindFirstEnemy(enemies);
    }

    private Enemy FindNoBurnEnemy(Enemy[] enemies)
    {
        return FindFirstEnemyMissingEffect(enemies, TowerTargetMode.NoBurn);
    }

    private Enemy FindNoPoisonEnemy(Enemy[] enemies)
    {
        return FindFirstEnemyMissingEffect(enemies, TowerTargetMode.NoPoison);
    }

    private Enemy FindNoSlowEnemy(Enemy[] enemies)
    {
        return FindFirstEnemyMissingEffect(enemies, TowerTargetMode.NoSlow);
    }

    private Enemy FindNoBleedEnemy(Enemy[] enemies)
    {
        return FindFirstEnemyMissingEffect(enemies, TowerTargetMode.NoBleed);
    }

    private Enemy FindFirstEnemyMissingEffect(Enemy[] enemies, TowerTargetMode effectTargetMode)
    {
        Enemy bestTarget = null;
        float bestProgress = -Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            if (!IsEnemyInRange(enemy))
                continue;

            if (!EnemyIsMissingEffect(enemy, effectTargetMode))
                continue;

            float progress = enemy.GetPathProgress();

            if (progress > bestProgress)
            {
                bestProgress = progress;
                bestTarget = enemy;
            }
        }

        if (bestTarget != null)
            return bestTarget;

        return FindFirstEnemy(enemies);
    }

    private bool EnemyIsMissingEffect(Enemy enemy, TowerTargetMode effectTargetMode)
    {
        if (enemy == null)
            return false;

        switch (effectTargetMode)
        {
            case TowerTargetMode.NoBurn:
                return enemy.CanReceiveBurnStack();
            case TowerTargetMode.NoPoison:
                return !enemy.HasPoison();
            case TowerTargetMode.NoSlow:
                return !enemy.HasSlow();
            case TowerTargetMode.NoBleed:
                return !enemy.HasBleed();
            default:
                return false;
        }
    }

    private bool IsEnemyInRange(Enemy enemy)
    {
        if (enemy == null)
            return false;

        float distance = Vector3.Distance(transform.position, enemy.transform.position);
        return distance <= GetEffectiveRange();
    }

    private void Shoot(Enemy target)
    {
        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
        {
            basicMasteryShotCounter++;
            basicMasteryShotsSinceKill++;

            BasicTowerMasteryShotContext context = masteryManager.PrepareBasicShot(this, target, basicMasteryShotCounter, basicMasteryShotsSinceKill);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int primaryDamage = masteryManager.CalculateBasicShotDamage(this, target, baseDamage, context, 1f);
            FireProjectile(target, primaryDamage, context.ignoreArmor, context.applyAnchorMark);

            if (context.reloadShot)
                basicMasteryShotsSinceKill = 0;

            if (context.doubleTap && target != null)
            {
                int doubleTapDamage = masteryManager.CalculateBasicDoubleTapDamage(this, target, baseDamage, context);
                FireProjectile(target, doubleTapDamage, false, false);
            }

            if (context.controlledSalvoTarget != null)
            {
                int controlledDamage = masteryManager.CalculateBasicControlledSalvoDamage(this, context.controlledSalvoTarget, baseDamage, context);
                FireProjectile(context.controlledSalvoTarget, controlledDamage, false, false);
            }

            return;
        }

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
        {
            rapidMasteryShotCounter++;

            RapidTowerMasteryShotContext context = rapidMasteryManager.PrepareRapidShot(this, target, rapidMasteryShotCounter);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int primaryDamage = rapidMasteryManager.CalculateRapidShotDamage(this, target, baseDamage, context, 1f);
            Projectile projectile = FireProjectile(target, primaryDamage, false, false);
            rapidMasteryManager.FillRapidProjectileData(projectile, context, baseDamage);
            return;
        }

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
        {
            heavyMasteryShotCounter++;

            HeavyTowerMasteryShotContext context = heavyMasteryManager.PrepareHeavyShot(this, target, heavyMasteryShotCounter);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int primaryDamage = heavyMasteryManager.CalculateHeavyShotDamage(this, target, baseDamage, context, 1f);
            Projectile projectile = FireProjectile(target, primaryDamage, false, false);
            heavyMasteryManager.FillHeavyProjectileData(projectile, context);

            if (context.secondaryTarget != null)
            {
                HeavyTowerMasteryShotContext secondaryContext = context;
                secondaryContext.consumeOverkillBonus = false;
                secondaryContext.siegeDischarge = false;
                secondaryContext.colossusStrike = false;
                secondaryContext.applyArmorBreakMark = false;
                secondaryContext.applyArmorWeaken = false;
                int secondaryDamage = heavyMasteryManager.CalculateHeavyShotDamage(this, context.secondaryTarget, baseDamage, secondaryContext, context.secondaryDamageMultiplier);
                Projectile secondaryProjectile = FireProjectile(context.secondaryTarget, secondaryDamage, false, false);
                heavyMasteryManager.FillHeavyProjectileData(secondaryProjectile, secondaryContext);
            }

            return;
        }

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
        {
            sniperMasteryShotCounter++;

            SniperTowerMasteryShotContext context = sniperMasteryManager.PrepareSniperShot(this, target, sniperMasteryShotCounter);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int sniperDamage = sniperMasteryManager.CalculateSniperShotDamage(this, target, baseDamage, context, 1f);
            Projectile projectile = FireProjectile(target, sniperDamage, false, false);
            sniperMasteryManager.FillSniperProjectileData(projectile, context);
            return;
        }

        if (towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
        {
            LightningTowerMasteryShotContext context = lightningMasteryManager.PrepareLightningShot(this, target);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int lightningDamage = lightningMasteryManager.CalculateLightningShotDamage(this, target, baseDamage, context);
            Projectile projectile = FireProjectile(target, lightningDamage, false, false);
            lightningMasteryManager.FillLightningProjectileData(projectile, this, context);
            return;
        }

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
        {
            MortarTowerMasteryShotContext context = mortarMasteryManager.PrepareMortarShot(this, target);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int mortarDamage = mortarMasteryManager.CalculateMortarShotDamage(this, target, baseDamage, context);
            Projectile projectile = FireProjectile(target, mortarDamage, context.armorPierceShot, false);
            mortarMasteryManager.FillMortarProjectileData(projectile, this, context);
            return;
        }

        if (towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
        {
            int baseDamage = GetEffectiveDamage();
            int fireDamage = fireMasteryManager.CalculateFireShotDamage(this, target, baseDamage);
            FireProjectile(target, fireDamage, false, false);
            return;
        }

        if (towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
        {
            AlchemistTowerMasteryShotContext context = alchemistMasteryManager.PrepareAlchemistShot(this, target);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int alchemistDamage = alchemistMasteryManager.CalculateAlchemistShotDamage(this, target, baseDamage, context);
            FireProjectile(target, alchemistDamage, false, false);
            return;
        }

        if (towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMasteryManager))
        {
            PoisonTowerMasteryShotContext context = poisonMasteryManager.PreparePoisonShot(this, target);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int poisonShotDamage = poisonMasteryManager.CalculatePoisonShotDamage(this, target, baseDamage);
            FireProjectile(target, poisonShotDamage, false, false);
            return;
        }

        if (towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out SlowTowerMasteryManager slowMasteryManager))
        {
            slowMasteryShotCounter++;
            SlowTowerMasteryShotContext context = slowMasteryManager.PrepareSlowShot(this, target, slowMasteryShotCounter);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            Projectile projectile = FireProjectile(target, GetEffectiveDamage(), false, false);
            slowMasteryManager.FillSlowProjectileData(projectile, context);
            return;
        }

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
        {
            SpikeTowerMasteryShotContext context = spikeMasteryManager.PrepareSpikeShot(this, target);

            if (context.primaryTarget != null)
                target = context.primaryTarget;

            int baseDamage = GetEffectiveDamage();
            int spikeDamage = spikeMasteryManager.CalculateSpikeShotDamage(this, target, baseDamage, context);
            Projectile projectile = FireProjectile(target, spikeDamage, false, false);
            spikeMasteryManager.FillSpikeProjectileData(projectile, this, context);
            return;
        }

        FireProjectile(target, GetEffectiveDamage(), false, false);
    }

    private Projectile FireProjectile(Enemy target, int projectileDamage, bool ignoreArmor, bool applyBasicAnchorMark)
    {
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogError("Tower fehlt ProjectilePrefab oder FirePoint!");
            return null;
        }

        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile == null)
        {
            Debug.LogError("Projectile Script fehlt auf Projectile Prefab!");
            Destroy(projectileObject);
            return null;
        }

        projectile.damage = Mathf.Max(0, projectileDamage);
        projectile.ignoreArmor = false;
        projectile.armorPierce = ignoreArmor ? 1 : 0;
        projectile.applyBasicAnchorMark = applyBasicAnchorMark;
        projectile.speed = GetProjectileSpeedForRole();
        projectile.behavior = GetProjectileBehaviorForRole();
        ConfigureRoleProjectile(projectile, target);
        projectile.appliesBurn = appliesBurn;
        projectile.burnDamage = burnDamage;
        projectile.burnDuration = burnDuration;
        projectile.appliesPoison = appliesPoison;
        projectile.poisonDamage = poisonDamage;
        projectile.poisonDuration = poisonDuration;
        projectile.appliesSlow = appliesSlow;
        projectile.slowAmount = slowAmount;
        projectile.slowDuration = slowDuration;
        projectile.SetTarget(target, this);
        return projectile;
    }

    private float GetProjectileSpeedForRole()
    {
        switch (towerRole)
        {
            case TowerRole.Mortar:
                return 8f;
            case TowerRole.Spike:
                return 24f;
            case TowerRole.Lightning:
                return 55f;
            default:
                return 40f;
        }
    }

    private ProjectileBehavior GetProjectileBehaviorForRole()
    {
        switch (towerRole)
        {
            case TowerRole.Lightning:
                return ProjectileBehavior.LightningChain;
            case TowerRole.Mortar:
                return ProjectileBehavior.MortarAOE;
            case TowerRole.Spike:
                return ProjectileBehavior.SpikeTrap;
            default:
                return ProjectileBehavior.Direct;
        }
    }

    private void ConfigureRoleProjectile(Projectile projectile, Enemy target)
    {
        if (projectile == null)
            return;

        if (towerRole == TowerRole.Lightning)
        {
            projectile.lightningChainTargets = GetLightningGuaranteedChainJumps();
            projectile.lightningBonusChainChance = GetLightningBonusChainRemainderChance();
            projectile.lightningChainRange = 2.5f;
            projectile.lightningChainDamageMultiplier = GetLightningChainDamageMultiplier();
        }
        else if (towerRole == TowerRole.Mortar)
        {
            projectile.mortarRadius = 0.85f;

            if (target != null)
                projectile.SetMortarImpactPosition(target.transform.position);
        }
        else if (towerRole == TowerRole.Spike)
        {
            projectile.spikeTriggerRadius = 0.45f;
            projectile.spikeBleedDamagePerTick = GetSpikeBleedDamagePerTick();
            projectile.spikeBleedTickInterval = 2.5f;
            projectile.spikeBleedDuration = GetSpikeBleedDuration();
        }
    }

    public void CycleTargetMode()
    {
        List<TowerTargetMode> availableModes = GetAvailableTargetModes();

        if (availableModes.Count == 0)
        {
            targetMode = TowerTargetMode.First;
            return;
        }

        int currentIndex = availableModes.IndexOf(targetMode);
        int nextIndex = currentIndex < 0 ? 0 : currentIndex + 1;

        if (nextIndex >= availableModes.Count)
            nextIndex = 0;

        targetMode = availableModes[nextIndex];
        Debug.Log(towerName + " Target Mode: " + GetTargetModeName());
    }

    public void SetTargetMode(TowerTargetMode newMode)
    {
        if (!IsTargetModeAvailable(newMode))
        {
            targetMode = TowerTargetMode.First;
            return;
        }

        targetMode = newMode;
    }

    private bool IsTargetModeAvailable(TowerTargetMode mode)
    {
        return GetAvailableTargetModes().Contains(mode);
    }

    private List<TowerTargetMode> GetAvailableTargetModes()
    {
        List<TowerTargetMode> modes = new List<TowerTargetMode>();

        modes.Add(TowerTargetMode.First);
        modes.Add(TowerTargetMode.Last);
        modes.Add(TowerTargetMode.Closest);
        modes.Add(TowerTargetMode.Strongest);
        modes.Add(TowerTargetMode.Elite);

        if (appliesBurn)
            modes.Add(TowerTargetMode.NoBurn);

        if (appliesPoison)
            modes.Add(TowerTargetMode.NoPoison);

        if (appliesSlow)
            modes.Add(TowerTargetMode.NoSlow);

        if (towerRole == TowerRole.Spike)
            modes.Add(TowerTargetMode.NoBleed);

        return modes;
    }

    public string GetTargetModeName()
    {
        switch (targetMode)
        {
            case TowerTargetMode.First:
                return "First";
            case TowerTargetMode.Last:
                return "Last";
            case TowerTargetMode.Closest:
                return "Closest";
            case TowerTargetMode.Strongest:
                return "Strongest";
            case TowerTargetMode.Elite:
                return "Elite";
            case TowerTargetMode.NoBurn:
                return appliesBurn ? "Burn Stack" : "No Burn";
            case TowerTargetMode.NoPoison:
                return "No Poison";
            case TowerTargetMode.NoSlow:
                return "No Slow";
            case TowerTargetMode.NoBleed:
                return "No Bleed";
            default:
                return targetMode.ToString();
        }
    }

    public void RaiseToMinimumLevel(int targetLevel)
    {
        int safeTargetLevel = Mathf.Max(1, targetLevel);

        while (level < safeTargetLevel)
            LevelUp();

        currentXP = Mathf.Clamp(currentXP, 0, Mathf.Max(0, xpToNextLevel - 1));
    }

    public int RaiseByLevelsUpTo(int targetLevel, int levelCount)
    {
        int safeTargetLevel = Mathf.Max(1, targetLevel);
        int remainingLevelUps = Mathf.Max(0, levelCount);
        int startLevel = level;

        while (level < safeTargetLevel && remainingLevelUps > 0)
        {
            LevelUp();
            remainingLevelUps--;
        }

        currentXP = Mathf.Clamp(currentXP, 0, Mathf.Max(0, xpToNextLevel - 1));
        return Mathf.Max(0, level - startLevel);
    }

    public void AddUpgradePoints(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0)
            return;

        upgradePoints += safeAmount;
        RefreshUpgradePointAvailableVisual();

        GameManager manager = ResolveGameManagerForRunStats();

        if (manager != null)
            manager.RegisterTowerUpgradePointsGranted(this, safeAmount);
    }

    public void ApplyEvolutionBoost(float bonusPercent)
    {
        float multiplier = 1f + Mathf.Max(0f, bonusPercent);
        evolutionStatMultiplier = Mathf.Max(1f, evolutionStatMultiplier) * multiplier;
        ApplyVisualTierStatBonusIfNeeded(true);
        RefreshRangeIndicatorIfVisible();
        RefreshUpgradePointAvailableVisual();
        Debug.Log(towerName + " Evolution angewendet: +" + (Mathf.Max(0f, bonusPercent) * 100f).ToString("0") + "% aktuelle Werte.");
    }

    public void AddXP(int amount)
    {
        if (amount <= 0)
            return;

        int finalAmount = TowerSupportTileEffect.ApplyXPMultiplier(this, amount);

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * masteryManager.GetBasicXPMultiplier()));

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * rapidMasteryManager.GetRapidXPMultiplier()));

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * heavyMasteryManager.GetHeavyXPMultiplier()));

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * sniperMasteryManager.GetSniperXPMultiplier()));

        if (towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * lightningMasteryManager.GetLightningXPMultiplier()));

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * mortarMasteryManager.GetMortarXPMultiplier()));

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * spikeMasteryManager.GetSpikeXPMultiplier()));

        if (towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * fireMasteryManager.GetFireXPMultiplier()));

        if (towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * alchemistMasteryManager.GetAlchemistXPMultiplier()));

        if (towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * poisonMasteryManager.GetPoisonXPMultiplier()));

        if (towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out SlowTowerMasteryManager slowMasteryManager))
            finalAmount = Mathf.Max(0, Mathf.RoundToInt(finalAmount * slowMasteryManager.GetSlowXPMultiplier()));

        currentXP += finalAmount;
        RecordTowerXPGainedForRunStats(finalAmount);

        if (xpToNextLevel <= 0)
            xpToNextLevel = CalculateXPToNextLevel(level);

        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        level++;

        bool gainedUpgradePoint = false;
        bool gainedMetaPoint = false;
        bool gainedVisualTier = false;

        if (ShouldGainUpgradePoint(level))
        {
            upgradePoints++;
            gainedUpgradePoint = true;
        }

        if (ShouldGainMetaProgressionPoint(level))
        {
            metaProgressionPoints++;
            gainedMetaPoint = true;
        }

        int oldVisualTier = visualTier;
        visualTier = CalculateVisualTier(level);

        if (visualTier > oldVisualTier)
            gainedVisualTier = true;

        xpToNextLevel = CalculateXPToNextLevel(level);

        string message = towerName + " Level Up! Level: " + level + " | XP bis nächstes Level: " + xpToNextLevel;

        if (gainedUpgradePoint)
            message += " | +1 Upgrade Point";

        if (gainedMetaPoint)
            message += " | +1 Meta Point vorbereitet";

        if (gainedVisualTier)
            message += " | Neue Visual Tier: " + visualTier;

        ApplyVisualTierStatBonusIfNeeded(true);
        RefreshVisualTierShape();
        PlayLevelUpVisual(gainedUpgradePoint);
        RefreshUpgradePointAvailableVisual();
        RecordTowerLevelUpForRunStats(gainedUpgradePoint, gainedMetaPoint, gainedVisualTier);

        if (TowerMasteryManager.TryGetActive(out TowerMasteryManager towerMasteryManager))
            towerMasteryManager.RecordTowerLevelReached(this, level);

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
            masteryManager.RecordBasicLevelReached(this, level);

        Debug.Log(message);
    }

    public bool TryGoldUpgradeDamage(GameManager gameManager)
    {
        int spentGold = damageUpgradeCost;

        if (!TrySpendGold(gameManager, spentGold))
            return false;

        damage += damageIncreasePerGoldUpgrade;
        damageGoldUpgradeLevel++;
        RecordTowerGoldUpgradeForRunStats(gameManager, TowerUpgradeCategory.Damage, spentGold, damageGoldUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        damageUpgradeCost = IncreaseGoldCost(damageUpgradeCost);
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Gold Damage Upgrade gekauft. Damage: " + damage);
        return true;
    }

    public bool TryGoldUpgradeRange(GameManager gameManager)
    {
        int spentGold = rangeUpgradeCost;

        if (!TrySpendGold(gameManager, spentGold))
            return false;

        range += rangeIncreasePerGoldUpgrade;
        rangeGoldUpgradeLevel++;
        RecordTowerGoldUpgradeForRunStats(gameManager, TowerUpgradeCategory.Range, spentGold, rangeGoldUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        rangeUpgradeCost = IncreaseGoldCost(rangeUpgradeCost);
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Gold Range Upgrade gekauft. Range: " + range);
        return true;
    }

    public bool TryGoldUpgradeFireRate(GameManager gameManager)
    {
        int spentGold = fireRateUpgradeCost;

        if (!TrySpendGold(gameManager, spentGold))
            return false;

        fireRate += fireRateIncreasePerGoldUpgrade;
        fireRateGoldUpgradeLevel++;
        RecordTowerGoldUpgradeForRunStats(gameManager, TowerUpgradeCategory.FireRate, spentGold, fireRateGoldUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        fireRateUpgradeCost = IncreaseGoldCost(fireRateUpgradeCost);
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Gold Fire Rate Upgrade gekauft. Fire Rate: " + fireRate);
        return true;
    }

    public bool TryGoldUpgradeEffect(GameManager gameManager)
    {
        if (!HasAnyEffect())
        {
            Debug.Log(towerName + " hat keinen Effekt, der verbessert werden kann.");
            return false;
        }

        int spentGold = effectUpgradeCost;

        if (!TrySpendGold(gameManager, spentGold))
            return false;

        ApplyEffectUpgrade(1);
        effectGoldUpgradeLevel++;
        RecordTowerGoldUpgradeForRunStats(gameManager, TowerUpgradeCategory.Effect, spentGold, effectGoldUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        effectUpgradeCost = IncreaseGoldCost(effectUpgradeCost);
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Gold Effect Upgrade gekauft. Effect Gold Level: " + effectGoldUpgradeLevel);
        return true;
    }

    public bool TryPointUpgradeDamage()
    {
        int spentPoints = upgradePointCostPerUpgrade;

        if (!SpendUpgradePoints(spentPoints))
            return false;

        damage += GetPointDamageIncreasePreview();
        damagePointUpgradeLevel++;
        RecordTowerPointUpgradeForRunStats(TowerUpgradeCategory.Damage, spentPoints, damagePointUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        RefreshUpgradePointAvailableVisual();
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Point Damage Upgrade gekauft. Damage: " + damage);
        return true;
    }

    public bool TryPointUpgradeRange()
    {
        int spentPoints = upgradePointCostPerUpgrade;

        if (!SpendUpgradePoints(spentPoints))
            return false;

        range += GetPointRangeIncreasePreview();
        rangePointUpgradeLevel++;
        RecordTowerPointUpgradeForRunStats(TowerUpgradeCategory.Range, spentPoints, rangePointUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        RefreshUpgradePointAvailableVisual();
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Point Range Upgrade gekauft. Range: " + range);
        return true;
    }

    public bool TryPointUpgradeFireRate()
    {
        int spentPoints = upgradePointCostPerUpgrade;

        if (!SpendUpgradePoints(spentPoints))
            return false;

        fireRate += GetPointFireRateIncreasePreview();
        fireRatePointUpgradeLevel++;
        RecordTowerPointUpgradeForRunStats(TowerUpgradeCategory.FireRate, spentPoints, fireRatePointUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        RefreshUpgradePointAvailableVisual();
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Point Fire Rate Upgrade gekauft. Fire Rate: " + fireRate);
        return true;
    }

    public bool TryPointUpgradeEffect()
    {
        if (!HasAnyEffect())
        {
            Debug.Log(towerName + " hat keinen Effekt, der verbessert werden kann.");
            return false;
        }

        int spentPoints = upgradePointCostPerUpgrade;

        if (!SpendUpgradePoints(spentPoints))
            return false;

        ApplyEffectUpgrade(pointUpgradePowerMultiplier);
        effectPointUpgradeLevel++;
        RecordTowerPointUpgradeForRunStats(TowerUpgradeCategory.Effect, spentPoints, effectPointUpgradeLevel);
        ApplyVisualTierStatBonusIfNeeded(true);
        RefreshUpgradePointAvailableVisual();
        RefreshRangeIndicatorIfVisible();
        Debug.Log(towerName + " Point Effect Upgrade gekauft. Effect Point Level: " + effectPointUpgradeLevel);
        return true;
    }

    private bool TrySpendGold(GameManager gameManager, int amount)
    {
        if (gameManager == null)
        {
            Debug.LogError("Tower: GameManager fehlt für Gold Upgrade!");
            return false;
        }

        return gameManager.SpendGold(amount, RunGoldSpendSource.GoldUpgrade);
    }

    private GameManager ResolveGameManagerForRunStats(GameManager preferred = null)
    {
        if (preferred != null)
            return preferred;

        return FindObjectOfType<GameManager>();
    }

    private void RecordTowerXPGainedForRunStats(int amount)
    {
        GameManager manager = ResolveGameManagerForRunStats();

        if (manager != null)
            manager.RegisterTowerXPGained(this, amount);
    }

    private void RecordTowerLevelUpForRunStats(bool gainedUpgradePoint, bool gainedMetaPoint, bool gainedVisualTier)
    {
        GameManager manager = ResolveGameManagerForRunStats();

        if (manager != null)
            manager.RegisterTowerLevelUp(this, level, gainedUpgradePoint, gainedMetaPoint, gainedVisualTier);
    }

    private void RecordTowerGoldUpgradeForRunStats(GameManager manager, TowerUpgradeCategory category, int spentGold, int newUpgradeLevel)
    {
        GameManager resolvedManager = ResolveGameManagerForRunStats(manager);

        if (resolvedManager != null)
            resolvedManager.RegisterTowerGoldUpgrade(this, category, spentGold, newUpgradeLevel);
    }

    private void RecordTowerPointUpgradeForRunStats(TowerUpgradeCategory category, int spentPoints, int newUpgradeLevel)
    {
        GameManager manager = ResolveGameManagerForRunStats();

        if (manager != null)
            manager.RegisterTowerPointUpgrade(this, category, spentPoints, newUpgradeLevel);
    }

    private int IncreaseGoldCost(int oldCost)
    {
        int safeOldCost = Mathf.Max(0, oldCost);
        int flatIncrease = Mathf.Max(0, goldUpgradeCostIncrease);
        float multiplier = Mathf.Max(1f, goldUpgradeCostMultiplier);

        int multipliedCost = Mathf.RoundToInt(safeOldCost * multiplier);
        int newCost = multipliedCost + flatIncrease;

        return Mathf.Max(safeOldCost + 1, newCost);
    }

    private bool SpendUpgradePoints(int amount)
    {
        if (amount <= 0)
            return true;

        if (upgradePoints < amount)
        {
            Debug.Log(towerName + " hat nicht genug Upgrade Points.");
            return false;
        }

        upgradePoints -= amount;
        RefreshUpgradePointAvailableVisual();
        return true;
    }

    private void ApplyEffectUpgrade(int multiplier)
    {
        int safeMultiplier = Mathf.Max(1, multiplier);

        if (towerRole == TowerRole.Lightning)
            return;

        if (appliesBurn)
        {
            burnDamage += burnDamageIncreasePerGoldUpgrade * safeMultiplier;
            burnDuration += effectDurationIncreasePerGoldUpgrade * safeMultiplier;
        }

        if (appliesPoison)
        {
            poisonDamage += poisonDamageIncreasePerGoldUpgrade * safeMultiplier;
            poisonDuration += effectDurationIncreasePerGoldUpgrade * safeMultiplier;
        }

        if (appliesSlow)
        {
            slowAmount -= slowAmountIncreasePerGoldUpgrade * safeMultiplier;
            slowAmount = Mathf.Clamp(slowAmount, 0.1f, 1f);
            slowDuration += slowDurationIncreasePerGoldUpgrade * safeMultiplier;
        }
    }

    public float GetSpikeBleedDamagePerTick()
    {
        float bleedDamage = 3f + effectGoldUpgradeLevel + effectPointUpgradeLevel * GetPointSpikeBleedDamageIncreasePreview();

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            bleedDamage += spikeMasteryManager.GetSpikeBleedDamageBaseBonus();

        return Mathf.Max(0.1f, bleedDamage);
    }

    public float GetSpikeBleedDuration()
    {
        float bleedDuration = 14f + effectGoldUpgradeLevel * effectDurationIncreasePerGoldUpgrade + effectPointUpgradeLevel * GetPointSpikeBleedDurationIncreasePreview();

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            bleedDuration += spikeMasteryManager.GetSpikeBleedDurationBonus();

        return Mathf.Max(0.1f, bleedDuration);
    }

    public int GetPointSpikeBleedDamageIncreasePreview()
    {
        return GetEffectivePointUpgradePowerMultiplier();
    }

    public float GetPointSpikeBleedDurationIncreasePreview()
    {
        return effectDurationIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public int GetLightningGuaranteedChainJumps()
    {
        return 2 + Mathf.FloorToInt(GetLightningBonusChainChance() + 0.0001f);
    }

    public int GetLightningGuaranteedChainTargetCount()
    {
        return 1 + GetLightningGuaranteedChainJumps();
    }

    public float GetLightningBonusChainChance()
    {
        float chance = effectGoldUpgradeLevel * GetLightningBonusChainChanceIncreasePerGoldUpgrade();
        chance += effectPointUpgradeLevel * GetPointLightningBonusChainChanceIncreasePreview();
        return Mathf.Max(0f, chance);
    }

    public float GetLightningBonusChainRemainderChance()
    {
        float chance = GetLightningBonusChainChance();
        int guaranteedBonusChains = Mathf.FloorToInt(chance + 0.0001f);
        return Mathf.Clamp01(chance - guaranteedBonusChains);
    }

    public float GetLightningBonusChainChanceIncreasePerGoldUpgrade()
    {
        return 0.20f;
    }

    public float GetLightningChainDamageMultiplier()
    {
        return 0.55f;
    }

    public float GetPointLightningBonusChainChanceIncreasePreview()
    {
        return GetLightningBonusChainChanceIncreasePerGoldUpgrade() * GetEffectivePointUpgradePowerMultiplier();
    }


    private void EnsureQoLVisualComponents()
    {
        if (aimController == null)
        {
            aimController = GetComponent<TowerAimController>();

            if (aimController != null && aimController.tower == null)
                aimController.tower = this;
        }

        if (autoCreateRangeIndicator && rangeIndicator == null)
        {
            rangeIndicator = GetComponent<TowerRangeIndicator>();
            if (rangeIndicator == null)
                rangeIndicator = gameObject.AddComponent<TowerRangeIndicator>();
            rangeIndicator.tower = this;
        }

        if (autoCreateTowerVisualFeedback && towerVisualFeedback == null)
        {
            towerVisualFeedback = GetComponent<TowerVisualFeedback>();
            if (towerVisualFeedback == null)
                towerVisualFeedback = gameObject.AddComponent<TowerVisualFeedback>();
            towerVisualFeedback.tower = this;
        }

        bool useFixedVisualTierPrefabs = TryResolveFixedVisualTierController();

        if (useFixedVisualTierPrefabs)
        {
            visualTierController = GetComponent<TowerVisualTierController>();

            if (visualTierController != null)
                visualTierController.enabled = false;
        }

        if (autoCreateVisualTierController && !useFixedVisualTierPrefabs && visualTierController == null)
        {
            visualTierController = GetComponent<TowerVisualTierController>();
            if (visualTierController == null)
                visualTierController = gameObject.AddComponent<TowerVisualTierController>();
            visualTierController.tower = this;
        }

        if (!useFixedVisualTierPrefabs && visualTierController != null && !visualTierController.enabled)
            visualTierController.enabled = true;
    }

    private bool TryResolveFixedVisualTierController()
    {
        if (visualTierPrefabController == null)
            visualTierPrefabController = GetComponent<TowerVisualTierPrefabController>();

        if (visualTierPrefabController == null || !visualTierPrefabController.useFixedVisualTierPrefabs)
            return false;

        if (visualTierPrefabController.tower == null)
            visualTierPrefabController.tower = this;

        return true;
    }

    public void SetRangeIndicatorVisible(bool visible)
    {
        EnsureQoLVisualComponents();

        if (rangeIndicator == null)
            return;

        if (visible)
            rangeIndicator.Show();
        else
            rangeIndicator.Hide();
    }

    public void RefreshRangeIndicatorIfVisible()
    {
        EnsureQoLVisualComponents();

        if (rangeIndicator != null && rangeIndicator.IsVisible())
            rangeIndicator.RefreshRange(true);
    }

    private void PlayLevelUpVisual(bool gainedUpgradePoint)
    {
        EnsureQoLVisualComponents();

        if (towerVisualFeedback != null)
            towerVisualFeedback.PlayLevelUpAnimation(gainedUpgradePoint);
    }

    private void RefreshUpgradePointAvailableVisual()
    {
        EnsureQoLVisualComponents();

        if (towerVisualFeedback != null)
            towerVisualFeedback.SetUpgradePointAvailable(upgradePoints > 0);
    }

    private void RefreshVisualTierShape()
    {
        EnsureQoLVisualComponents();

        if (TryResolveFixedVisualTierController())
        {
            visualTierPrefabController.ApplyTier(visualTier);
            return;
        }

        if (visualTierController != null)
            visualTierController.ApplyTier(visualTier);
    }

    private void CaptureVisualTierBaseStatsIfNeeded()
    {
        if (visualTierBaseStatsCaptured)
            return;

        visualTierBaseStatsCaptured = true;
        baseDamageBeforeVisualTier = damage;
        baseRangeBeforeVisualTier = range;
        baseFireRateBeforeVisualTier = fireRate;
        baseBurnDamageBeforeVisualTier = burnDamage;
        baseBurnDurationBeforeVisualTier = burnDuration;
        basePoisonDamageBeforeVisualTier = poisonDamage;
        basePoisonDurationBeforeVisualTier = poisonDuration;
        baseSlowAmountBeforeVisualTier = slowAmount;
        baseSlowDurationBeforeVisualTier = slowDuration;
    }

    private void ApplyVisualTierStatBonusIfNeeded(bool force)
    {
        if (!applyVisualTierStatBonus)
            return;

        CaptureVisualTierBaseStatsIfNeeded();

        int safeTier = Mathf.Max(0, visualTier);

        if (!force && visualTierStatsAppliedForTier == safeTier)
            return;

        visualTierStatsAppliedForTier = safeTier;
        float bonusMultiplier = Mathf.Max(0f, visualTierStatBonusPerTier) * safeTier;
        float safeEvolutionMultiplier = Mathf.Max(1f, evolutionStatMultiplier);

        int damageFromGold = damageGoldUpgradeLevel * damageIncreasePerGoldUpgrade;
        int damageFromPoints = damagePointUpgradeLevel * GetPointDamageIncreasePreview();
        damage = Mathf.Max(0, Mathf.RoundToInt((baseDamageBeforeVisualTier + damageFromGold + damageFromPoints + Mathf.RoundToInt(baseDamageBeforeVisualTier * bonusMultiplier)) * safeEvolutionMultiplier));

        float rangeFromGold = rangeGoldUpgradeLevel * rangeIncreasePerGoldUpgrade;
        float rangeFromPoints = rangePointUpgradeLevel * GetPointRangeIncreasePreview();
        range = Mathf.Max(0f, (baseRangeBeforeVisualTier + rangeFromGold + rangeFromPoints + baseRangeBeforeVisualTier * bonusMultiplier) * safeEvolutionMultiplier);

        float fireRateFromGold = fireRateGoldUpgradeLevel * fireRateIncreasePerGoldUpgrade;
        float fireRateFromPoints = fireRatePointUpgradeLevel * GetPointFireRateIncreasePreview();
        fireRate = Mathf.Max(0.01f, (baseFireRateBeforeVisualTier + fireRateFromGold + fireRateFromPoints + baseFireRateBeforeVisualTier * bonusMultiplier) * safeEvolutionMultiplier);

        if (appliesBurn)
        {
            burnDamage = Mathf.Max(0, Mathf.RoundToInt((baseBurnDamageBeforeVisualTier + effectGoldUpgradeLevel * burnDamageIncreasePerGoldUpgrade + effectPointUpgradeLevel * GetPointBurnDamageIncreasePreview() + Mathf.RoundToInt(baseBurnDamageBeforeVisualTier * bonusMultiplier)) * safeEvolutionMultiplier));
            burnDuration = Mathf.Max(0f, (baseBurnDurationBeforeVisualTier + effectGoldUpgradeLevel * effectDurationIncreasePerGoldUpgrade + effectPointUpgradeLevel * GetPointEffectDurationIncreasePreview() + baseBurnDurationBeforeVisualTier * bonusMultiplier) * safeEvolutionMultiplier);
        }

        if (appliesPoison)
        {
            poisonDamage = Mathf.Max(0, Mathf.RoundToInt((basePoisonDamageBeforeVisualTier + effectGoldUpgradeLevel * poisonDamageIncreasePerGoldUpgrade + effectPointUpgradeLevel * GetPointPoisonDamageIncreasePreview() + Mathf.RoundToInt(basePoisonDamageBeforeVisualTier * bonusMultiplier)) * safeEvolutionMultiplier));
            poisonDuration = Mathf.Max(0f, (basePoisonDurationBeforeVisualTier + effectGoldUpgradeLevel * effectDurationIncreasePerGoldUpgrade + effectPointUpgradeLevel * GetPointEffectDurationIncreasePreview() + basePoisonDurationBeforeVisualTier * bonusMultiplier) * safeEvolutionMultiplier);
        }

        if (appliesSlow)
        {
            float baseSlowStrength = 1f - Mathf.Clamp01(baseSlowAmountBeforeVisualTier);
            float upgradedSlowStrength = baseSlowStrength * (1f + bonusMultiplier);

            if (towerRole != TowerRole.Lightning)
            {
                upgradedSlowStrength += effectGoldUpgradeLevel * slowAmountIncreasePerGoldUpgrade;
                upgradedSlowStrength += effectPointUpgradeLevel * GetPointSlowAmountIncreasePreview();
            }

            upgradedSlowStrength *= safeEvolutionMultiplier;
            slowAmount = Mathf.Clamp(1f - upgradedSlowStrength, 0.1f, 1f);

            if (towerRole == TowerRole.Lightning)
                slowDuration = Mathf.Max(0f, (baseSlowDurationBeforeVisualTier + baseSlowDurationBeforeVisualTier * bonusMultiplier) * safeEvolutionMultiplier);
            else
                slowDuration = Mathf.Max(0f, (baseSlowDurationBeforeVisualTier + effectGoldUpgradeLevel * slowDurationIncreasePerGoldUpgrade + effectPointUpgradeLevel * GetPointSlowDurationIncreasePreview() + baseSlowDurationBeforeVisualTier * bonusMultiplier) * safeEvolutionMultiplier);
        }
    }

    public bool HasAnyEffect()
    {
        return appliesBurn || appliesPoison || appliesSlow || towerRole == TowerRole.Spike;
    }

    public float GetEffectiveRange()
    {
        float effectiveRange = range + TowerSupportTileEffect.GetRangeBonus(this);

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
            effectiveRange += masteryManager.GetBasicRangeBonus(this, effectiveRange);

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
            effectiveRange += rapidMasteryManager.GetRapidRangeBonus();

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
            effectiveRange += heavyMasteryManager.GetHeavyRangeBonus();

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
            effectiveRange += sniperMasteryManager.GetSniperRangeBonus();

        if (towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            effectiveRange += lightningMasteryManager.GetLightningRangeBonus();

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            effectiveRange += mortarMasteryManager.GetMortarRangeBonus();

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            effectiveRange += spikeMasteryManager.GetSpikeRangeBonus();

        if (towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
            effectiveRange += fireMasteryManager.GetFireRangeBonus();

        if (towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
            effectiveRange += alchemistMasteryManager.GetAlchemistRangeBonus();

        if (towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMasteryManager))
            effectiveRange += poisonMasteryManager.GetPoisonRangeBonus();

        if (towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out SlowTowerMasteryManager slowMasteryManager))
            effectiveRange += slowMasteryManager.GetSlowRangeBonus();

        return Mathf.Max(0f, effectiveRange);
    }

    public float GetEffectiveFireRate()
    {
        float baseFireRate = fireRate;

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
            baseFireRate = (baseFireRate + masteryManager.GetBasicFireRateAdditive()) * masteryManager.GetBasicFireRateMultiplier(this);

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
            baseFireRate = (baseFireRate + rapidMasteryManager.GetRapidFireRateAdditive()) * rapidMasteryManager.GetRapidFireRateMultiplier(this);

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
            baseFireRate += heavyMasteryManager.GetHeavyFireRateAdditive();

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
            baseFireRate = (baseFireRate + sniperMasteryManager.GetSniperFireRateAdditive()) * sniperMasteryManager.GetSniperFireRateMultiplier(this);

        if (towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            baseFireRate = (baseFireRate + lightningMasteryManager.GetLightningFireRateAdditive()) * lightningMasteryManager.GetLightningFireRateMultiplier(this);

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            baseFireRate += mortarMasteryManager.GetMortarFireRateAdditive();

        if (towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
            baseFireRate *= fireMasteryManager.GetFireFireRateMultiplier(this);

        if (towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
            baseFireRate += alchemistMasteryManager.GetAlchemistFireRateAdditive();

        if (towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMasteryManager))
            baseFireRate = (baseFireRate + poisonMasteryManager.GetPoisonFireRateAdditive()) * poisonMasteryManager.GetPoisonFireRateMultiplier(this);

        if (towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out SlowTowerMasteryManager slowMasteryManager))
            baseFireRate = (baseFireRate + slowMasteryManager.GetSlowFireRateAdditive()) * slowMasteryManager.GetSlowFireRateMultiplier(this);

        return Mathf.Max(0.01f, baseFireRate * TowerSupportTileEffect.GetFireRateMultiplier(this));
    }

    public int GetEffectiveDamage()
    {
        float baseDamage = damage;

        if (towerRole == TowerRole.Basic && BasicTowerMasteryManager.TryGetActive(out BasicTowerMasteryManager masteryManager))
            baseDamage += masteryManager.GetBasicDamageBaseBonus();

        if (towerRole == TowerRole.Rapid && RapidTowerMasteryManager.TryGetActive(out RapidTowerMasteryManager rapidMasteryManager))
            baseDamage += rapidMasteryManager.GetRapidDamageBaseBonus();

        if (towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
            baseDamage += heavyMasteryManager.GetHeavyDamageBaseBonus();

        if (towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
            baseDamage += sniperMasteryManager.GetSniperDamageBaseBonus();

        if (towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            baseDamage += lightningMasteryManager.GetLightningDamageBaseBonus();

        if (towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            baseDamage += mortarMasteryManager.GetMortarDamageBaseBonus();

        if (towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            baseDamage += spikeMasteryManager.GetSpikeDamageBaseBonus();

        if (towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
            baseDamage += fireMasteryManager.GetFireDirectDamageBaseBonus();

        if (towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
            baseDamage += alchemistMasteryManager.GetAlchemistDamageBaseBonus();

        return Mathf.Max(0, Mathf.RoundToInt(baseDamage * TowerSupportTileEffect.GetDamageMultiplier(this)));
    }

    private int GetEffectivePointUpgradePowerMultiplier()
    {
        return Mathf.Max(1, pointUpgradePowerMultiplier + TowerSupportTileEffect.GetPointUpgradePowerBonus(this));
    }

    public int GetPointDamageIncreasePreview()
    {
        return Mathf.Max(1, damageIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier());
    }

    public float GetPointRangeIncreasePreview()
    {
        return rangeIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public float GetPointFireRateIncreasePreview()
    {
        return fireRateIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public int GetPointBurnDamageIncreasePreview()
    {
        return burnDamageIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public int GetPointPoisonDamageIncreasePreview()
    {
        return poisonDamageIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public float GetPointEffectDurationIncreasePreview()
    {
        return effectDurationIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public float GetPointSlowAmountIncreasePreview()
    {
        return slowAmountIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public float GetPointSlowDurationIncreasePreview()
    {
        return slowDurationIncreasePerGoldUpgrade * GetEffectivePointUpgradePowerMultiplier();
    }

    public int GetUpgradePointCost()
    {
        return upgradePointCostPerUpgrade;
    }

    public int GetVisualTier()
    {
        return visualTier;
    }

    public int GetMetaProgressionPoints()
    {
        return metaProgressionPoints;
    }
}
