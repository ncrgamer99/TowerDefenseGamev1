using System.Collections.Generic;
using UnityEngine;

public enum ProjectileBehavior
{
    Direct,
    LightningChain,
    MortarAOE,
    SpikeTrap
}

public static class ProjectilePool
{
    private const int MaxStoredProjectilesPerPrefab = 128;
    private static readonly Dictionary<GameObject, Stack<Projectile>> projectilePools = new Dictionary<GameObject, Stack<Projectile>>();

    public static Projectile Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        Projectile projectile = GetPooledProjectile(prefab);

        if (projectile == null)
        {
            GameObject projectileObject = Object.Instantiate(prefab, position, rotation);
            projectile = projectileObject.GetComponent<Projectile>();

            if (projectile == null)
            {
                Object.Destroy(projectileObject);
                return null;
            }
        }

        projectile.PrepareForReuse(prefab, position, rotation);
        return projectile;
    }

    public static void Release(Projectile projectile, GameObject prefab)
    {
        if (projectile == null || prefab == null)
        {
            if (projectile != null)
                Object.Destroy(projectile.gameObject);

            return;
        }

        if (!projectilePools.TryGetValue(prefab, out Stack<Projectile> pool))
        {
            pool = new Stack<Projectile>();
            projectilePools.Add(prefab, pool);
        }

        if (pool.Count >= MaxStoredProjectilesPerPrefab)
        {
            Object.Destroy(projectile.gameObject);
            return;
        }

        projectile.gameObject.SetActive(false);
        pool.Push(projectile);
    }

    private static Projectile GetPooledProjectile(GameObject prefab)
    {
        if (!projectilePools.TryGetValue(prefab, out Stack<Projectile> pool))
            return null;

        while (pool.Count > 0)
        {
            Projectile projectile = pool.Pop();

            if (projectile != null)
                return projectile;
        }

        return null;
    }
}

public class Projectile : MonoBehaviour
{
    public float speed = 40f;
    public int damage = 1;
    public bool ignoreArmor = false;
    public int armorPierce = 0;
    public bool applyBasicAnchorMark = false;
    public bool applyRapidEscapeMark = false;
    public float rapidEscapeMarkDuration = 2.5f;
    public bool applyRapidNeedleMark = false;
    public int rapidNeedleMarkThreshold = 0;
    public int rapidNeedleMarkApplications = 1;
    public float rapidNeedleMarkDuration = 3f;
    public int rapidNeedleBonusDamage = 0;
    public bool rapidNeedleBonusArmorPierce = false;
    public int rapidNeedleHailDamage = 0;
    public bool applyHeavyArmorBreakMark = false;
    public float heavyArmorBreakMarkDuration = 4f;
    public bool applyHeavyArmorWeaken = false;
    public float heavyArmorWeakenDuration = 3f;
    public bool applyHeavyImpactStagger = false;
    public float heavyImpactStaggerDuration = 0.15f;
    public bool applySniperHeadshotMark = false;
    public bool consumeSniperHeadshotMark = false;
    public float sniperHeadshotMarkDuration = 4f;
    public bool applySniperBossMark = false;
    public float sniperBossMarkDuration = 5f;
    public bool applyLightningStaticShock = false;
    public bool applyLightningAnchorShock = false;
    public bool lightningDisruptsMage = false;
    public float lightningShockDuration = 0.15f;
    public float lightningShockSpeedMultiplier = 0.88f;
    public float lightningStaticShockDuration = 0.15f;
    public float lightningStaticShockSpeedMultiplier = 0.88f;
    public float lightningAnchorShockDuration = 0.35f;
    public float lightningAnchorShockSpeedMultiplier = 0.72f;
    public bool lightningOverloadBurst = false;
    public int lightningOverloadSecondaryBonusDamage = 0;
    public int lightningOverloadSecondaryBonusTargets = 0;
    public ProjectileBehavior behavior = ProjectileBehavior.Direct;
    public Color directProjectileColor = new Color32(255, 230, 90, 255);
    public Color lightningProjectileColor = new Color32(125, 220, 255, 255);
    public Color mortarProjectileColor = new Color32(255, 150, 45, 255);
    public Color spikeProjectileColor = new Color32(125, 125, 135, 255);

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
    public bool slowMasteryControlPulse = false;
    public bool slowMasteryLastLine = false;
    public bool applySlowStasisField = false;
    public float slowStasisRadius = 0.95f;
    public float slowStasisAmount = 0.25f;
    public float slowStasisDuration = 0.9f;

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
    public bool mortarSiegeStrike = false;
    public int mortarFragmentTargets = 0;
    public float mortarFragmentDamageMultiplier = 0f;
    public float mortarFragmentSearchRadius = 0.65f;
    public bool mortarCreateCrater = false;
    public float mortarCraterRadius = 0.65f;
    public float mortarCraterDuration = 1f;
    public float mortarCraterDamagePerTick = 1f;
    public float mortarCraterTickInterval = 0.45f;
    public bool mortarCraterStaggers = false;
    public float mortarCraterStaggerMultiplier = 0.9f;
    public float mortarCraterStaggerDuration = 0.2f;

    [Header("Spike Trap")]
    public float spikeTriggerRadius = 0.45f;
    public float spikeBleedDamagePerTick = 3f;
    public float spikeBleedTickInterval = 2.0f;
    public float spikeBleedDuration = 14f;
    public int spikeMaxTrapTriggers = 1;
    public float spikeTriggerCooldown = 0.16f;
    public int spikeTrapDirectDamage = 0;
    public int spikeExtraTargets = 0;
    public float spikeExtraDamageMultiplier = 0f;
    public float spikeExtraTargetRadius = 0.55f;

    private Enemy target;
    private Tower ownerTower;
    private Vector3 mortarImpactPosition;
    private bool hasMortarImpactPosition = false;
    private GameObject poolPrefab;
    private bool returnedToPool = false;

    private void Awake()
    {
        ApplyProjectileVisualMaterial();
    }

    public void PrepareForReuse(GameObject sourcePrefab, Vector3 position, Quaternion rotation)
    {
        StopAllCoroutines();
        poolPrefab = sourcePrefab;
        returnedToPool = false;
        target = null;
        ownerTower = null;
        hasMortarImpactPosition = false;
        transform.SetPositionAndRotation(position, rotation);
        ResetRuntimeModifiers();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void ResetRuntimeModifiers()
    {
        damage = 1;
        ignoreArmor = false;
        armorPierce = 0;
        applyBasicAnchorMark = false;
        applyRapidEscapeMark = false;
        rapidEscapeMarkDuration = 2.5f;
        applyRapidNeedleMark = false;
        rapidNeedleMarkThreshold = 0;
        rapidNeedleMarkApplications = 1;
        rapidNeedleMarkDuration = 3f;
        rapidNeedleBonusDamage = 0;
        rapidNeedleBonusArmorPierce = false;
        rapidNeedleHailDamage = 0;
        applyHeavyArmorBreakMark = false;
        heavyArmorBreakMarkDuration = 4f;
        applyHeavyArmorWeaken = false;
        heavyArmorWeakenDuration = 3f;
        applyHeavyImpactStagger = false;
        heavyImpactStaggerDuration = 0.15f;
        applySniperHeadshotMark = false;
        consumeSniperHeadshotMark = false;
        sniperHeadshotMarkDuration = 4f;
        applySniperBossMark = false;
        sniperBossMarkDuration = 5f;
        applyLightningStaticShock = false;
        applyLightningAnchorShock = false;
        lightningDisruptsMage = false;
        lightningShockDuration = 0.15f;
        lightningShockSpeedMultiplier = 0.88f;
        lightningStaticShockDuration = 0.15f;
        lightningStaticShockSpeedMultiplier = 0.88f;
        lightningAnchorShockDuration = 0.35f;
        lightningAnchorShockSpeedMultiplier = 0.72f;
        lightningOverloadBurst = false;
        lightningOverloadSecondaryBonusDamage = 0;
        lightningOverloadSecondaryBonusTargets = 0;
        behavior = ProjectileBehavior.Direct;
        appliesBurn = false;
        burnDamage = 1;
        burnDuration = 3f;
        appliesPoison = false;
        poisonDamage = 1;
        poisonDuration = 4f;
        appliesSlow = false;
        slowAmount = 0.5f;
        slowDuration = 2f;
        slowMasteryControlPulse = false;
        slowMasteryLastLine = false;
        applySlowStasisField = false;
        slowStasisRadius = 0.95f;
        slowStasisAmount = 0.25f;
        slowStasisDuration = 0.9f;
        lightningChainTargets = 2;
        lightningBonusChainChance = 0f;
        lightningChainRange = 2.5f;
        lightningChainDamageMultiplier = 0.55f;
        lightningLineDuration = 0.12f;
        lightningLineColor = new Color32(125, 220, 255, 255);
        mortarRadius = 0.85f;
        mortarSiegeStrike = false;
        mortarFragmentTargets = 0;
        mortarFragmentDamageMultiplier = 0f;
        mortarFragmentSearchRadius = 0.65f;
        mortarCreateCrater = false;
        mortarCraterRadius = 0.65f;
        mortarCraterDuration = 1f;
        mortarCraterDamagePerTick = 1f;
        mortarCraterTickInterval = 0.45f;
        mortarCraterStaggers = false;
        mortarCraterStaggerMultiplier = 0.9f;
        mortarCraterStaggerDuration = 0.2f;
        spikeTriggerRadius = 0.45f;
        spikeBleedDamagePerTick = 3f;
        spikeBleedTickInterval = 2.0f;
        spikeBleedDuration = 14f;
        spikeMaxTrapTriggers = 1;
        spikeTriggerCooldown = 0.16f;
        spikeTrapDirectDamage = 0;
        spikeExtraTargets = 0;
        spikeExtraDamageMultiplier = 0f;
        spikeExtraTargetRadius = 0.55f;
    }

    private void FinishProjectile()
    {
        if (returnedToPool)
            return;

        returnedToPool = true;
        target = null;
        ownerTower = null;
        hasMortarImpactPosition = false;
        ProjectilePool.Release(this, poolPrefab);
    }

    public void SetTarget(Enemy newTarget, Tower tower)
    {
        target = newTarget;
        ownerTower = tower;
        ApplyProjectileVisualMaterial();

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
            FinishProjectile();
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
            FinishProjectile();
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
            FinishProjectile();
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

        FinishProjectile();
    }

    private void ApplyDirectHit(Enemy enemy, int hitDamage)
    {
        ApplyDirectHit(enemy, hitDamage, false, 0);
    }

    private void ApplyDirectHit(Enemy enemy, int hitDamage, bool lightningChainHit, int lightningChainIndex)
    {
        if (enemy == null)
            return;

        int finalHitDamage = Mathf.Max(0, hitDamage);

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            finalHitDamage = mortarMasteryManager.ModifyMortarHitDamage(ownerTower, enemy, finalHitDamage, mortarSiegeStrike);

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManager))
            finalHitDamage = spikeMasteryManager.ModifySpikeDirectHitDamage(ownerTower, enemy, finalHitDamage);

        float healthBeforeHit = enemy.currentHealth;

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Heavy && HeavyTowerMasteryManager.TryGetActive(out HeavyTowerMasteryManager heavyMasteryManager))
        {
            heavyMasteryManager.RecordPotentialOverkill(ownerTower, enemy, finalHitDamage);
            enemy.TakeDamage(finalHitDamage, ownerTower, ignoreArmor, armorPierce);
            heavyMasteryManager.RecordHeavyHit(ownerTower, enemy);
        }
        else
        {
            enemy.TakeDamage(finalHitDamage, ownerTower, ignoreArmor, armorPierce);
        }

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager fireMasteryManager))
            fireMasteryManager.RecordFireDirectDamage(ownerTower, enemy, Mathf.Max(0f, healthBeforeHit - enemy.currentHealth));

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMasteryManager))
            poisonMasteryManager.RecordPoisonDirectDamage(ownerTower, enemy, Mathf.Max(0f, healthBeforeHit - enemy.currentHealth));

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Sniper && SniperTowerMasteryManager.TryGetActive(out SniperTowerMasteryManager sniperMasteryManager))
            sniperMasteryManager.RecordSniperHit(ownerTower, enemy, Mathf.Max(0f, healthBeforeHit - enemy.currentHealth));

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            lightningMasteryManager.RecordLightningHit(ownerTower, enemy, Mathf.Max(0f, healthBeforeHit - enemy.currentHealth), lightningChainHit, lightningChainIndex);

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMasteryManager))
            alchemistMasteryManager.RecordAlchemistDirectDamage(ownerTower, enemy, Mathf.Max(0f, healthBeforeHit - enemy.currentHealth));

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Spike && SpikeTowerMasteryManager.TryGetActive(out SpikeTowerMasteryManager spikeMasteryManagerAfterHit))
            spikeMasteryManagerAfterHit.RecordSpikeDirectDamage(ownerTower, enemy, Mathf.Max(0f, healthBeforeHit - enemy.currentHealth));

        if (applyBasicAnchorMark)
            enemy.ApplyBasicAnchorMark(3f, ownerTower);

        if (applyRapidEscapeMark)
            enemy.ApplyRapidEscapeMark(rapidEscapeMarkDuration, ownerTower);

        if (applyRapidNeedleMark && enemy.ApplyRapidNeedleMarks(rapidNeedleMarkApplications, rapidNeedleMarkThreshold, rapidNeedleMarkDuration, ownerTower))
        {
            if (rapidNeedleBonusDamage > 0)
                enemy.TakeDamage(rapidNeedleBonusDamage, ownerTower, false, rapidNeedleBonusArmorPierce ? 1 : 0);

            if (rapidNeedleHailDamage > 0)
                enemy.TakeDamage(rapidNeedleHailDamage, ownerTower, false, 0);
        }

        if (applyHeavyArmorBreakMark)
            enemy.ApplyHeavyArmorBreakMark(heavyArmorBreakMarkDuration, ownerTower);

        if (applyHeavyArmorWeaken)
            enemy.ApplyHeavyArmorWeaken(heavyArmorWeakenDuration, ownerTower);

        if (applyHeavyImpactStagger)
            enemy.ApplyHeavyImpactStagger(heavyImpactStaggerDuration, ownerTower);

        if (applySniperHeadshotMark)
            enemy.ApplySniperHeadshotMark(sniperHeadshotMarkDuration, ownerTower);

        if (consumeSniperHeadshotMark)
            enemy.ConsumeSniperHeadshotMark(ownerTower);

        if (applySniperBossMark)
            enemy.ApplySniperBossMark(sniperBossMarkDuration, ownerTower);

        bool applyLightningAnchorThisHit = applyLightningAnchorShock && !lightningChainHit;
        if ((applyLightningStaticShock || applyLightningAnchorThisHit) && ownerTower != null && ownerTower.towerRole == TowerRole.Lightning)
        {
            float shockDuration = applyLightningAnchorThisHit ? lightningAnchorShockDuration : lightningStaticShockDuration;
            float shockMultiplier = applyLightningAnchorThisHit ? lightningAnchorShockSpeedMultiplier : lightningStaticShockSpeedMultiplier;
            enemy.ApplyLightningShock(shockDuration, shockMultiplier, ownerTower, lightningDisruptsMage);
        }

        if (lightningChainHit && lightningOverloadBurst && lightningOverloadSecondaryBonusDamage > 0 && lightningChainIndex <= Mathf.Max(0, lightningOverloadSecondaryBonusTargets))
            enemy.TakeDamage(lightningOverloadSecondaryBonusDamage, ownerTower, false, 0);

        if (appliesBurn)
        {
            float finalBurnDamage = burnDamage;
            float finalBurnDuration = burnDuration;

            if (ownerTower != null && ownerTower.towerRole == TowerRole.Fire && FireTowerMasteryManager.TryGetActive(out FireTowerMasteryManager burnMasteryManager))
            {
                finalBurnDamage = burnMasteryManager.GetModifiedFireBurnDamage(ownerTower, enemy, burnDamage);
                finalBurnDuration = burnMasteryManager.GetModifiedFireBurnDuration(ownerTower, enemy, burnDuration);
            }

            enemy.ApplyBurn(finalBurnDamage, finalBurnDuration, ownerTower);
        }

        if (appliesPoison)
        {
            float finalPoisonDamage = poisonDamage;
            float finalPoisonDuration = poisonDuration;

            if (ownerTower != null && ownerTower.towerRole == TowerRole.Poison && PoisonTowerMasteryManager.TryGetActive(out PoisonTowerMasteryManager poisonMastery))
            {
                finalPoisonDamage = poisonMastery.GetModifiedPoisonDamage(ownerTower, enemy, poisonDamage);
                finalPoisonDuration = poisonMastery.GetModifiedPoisonDuration(ownerTower, enemy, poisonDuration);
            }

            if (ownerTower != null && ownerTower.towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager alchemistMastery))
            {
                finalPoisonDamage = alchemistMastery.GetModifiedAlchemistPoisonDamage(ownerTower, enemy, poisonDamage);
                finalPoisonDuration = alchemistMastery.GetModifiedAlchemistPoisonDuration(ownerTower, enemy, poisonDuration);
            }

            enemy.ApplyPoison(finalPoisonDamage, finalPoisonDuration, ownerTower);
        }

        if (appliesSlow)
        {
            float finalSlowAmount = slowAmount;
            float finalSlowDuration = slowDuration;
            SlowTowerMasteryManager slowMasteryManager = null;
            bool useSlowMastery = ownerTower != null && ownerTower.towerRole == TowerRole.Slow && SlowTowerMasteryManager.TryGetActive(out slowMasteryManager);

            if (useSlowMastery)
            {
                finalSlowAmount = slowMasteryManager.GetModifiedSlowAmount(ownerTower, enemy, slowAmount, applySlowStasisField, slowMasteryLastLine);
                finalSlowDuration = slowMasteryManager.GetModifiedSlowDuration(ownerTower, enemy, slowDuration, slowMasteryControlPulse, applySlowStasisField, slowMasteryLastLine);
            }

            AlchemistTowerMasteryManager alchemistSlowMastery = null;
            bool useAlchemistMastery = ownerTower != null && ownerTower.towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out alchemistSlowMastery);

            if (useAlchemistMastery)
            {
                finalSlowAmount = alchemistSlowMastery.GetModifiedAlchemistSlowAmount(ownerTower, enemy, slowAmount);
                finalSlowDuration = alchemistSlowMastery.GetModifiedAlchemistSlowDuration(ownerTower, enemy, slowDuration);
            }

            if (ownerTower != null && ownerTower.towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMastery))
            {
                finalSlowAmount = lightningMastery.GetModifiedLightningSlowAmount(ownerTower, enemy, finalSlowAmount);
                finalSlowDuration = lightningMastery.GetModifiedLightningSlowDuration(ownerTower, enemy, finalSlowDuration);
            }

            enemy.ApplySlow(finalSlowAmount, finalSlowDuration, ownerTower);

            if (useSlowMastery && applySlowStasisField)
                slowMasteryManager.ApplyStasisField(ownerTower, enemy, slowStasisAmount, slowStasisDuration, slowStasisRadius);
        }

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Alchemist && AlchemistTowerMasteryManager.TryGetActive(out AlchemistTowerMasteryManager postHitAlchemistMastery))
            postHitAlchemistMastery.ApplyAlchemistPostHitEffects(ownerTower, enemy);
    }

    private void HitLightningTarget()
    {
        ApplyDirectHit(target, damage, false, 0);
        int targetsHit = ChainLightningFrom(target);

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Lightning && LightningTowerMasteryManager.TryGetActive(out LightningTowerMasteryManager lightningMasteryManager))
            lightningMasteryManager.RecordLightningChainCompleted(ownerTower, targetsHit);
    }

    private int ChainLightningFrom(Enemy firstTarget)
    {
        if (firstTarget == null)
            return 0;

        List<Enemy> hitEnemies = new List<Enemy> { firstTarget };
        Enemy chainSource = firstTarget;
        int chainDamage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Clamp01(lightningChainDamageMultiplier)));
        int chains = Mathf.Max(0, lightningChainTargets);

        for (int i = 0; i < chains; i++)
        {
            if (!TryChainToNextTarget(ref chainSource, hitEnemies, chainDamage))
                return hitEnemies.Count;
        }

        if (Random.value <= Mathf.Clamp01(lightningBonusChainChance))
            TryChainToNextTarget(ref chainSource, hitEnemies, chainDamage);

        return hitEnemies.Count;
    }

    private bool TryChainToNextTarget(ref Enemy chainSource, List<Enemy> hitEnemies, int chainDamage)
    {
        Enemy nextTarget = FindNearestChainTarget(chainSource, hitEnemies);

        if (nextTarget == null)
            return false;

        DrawTemporaryLine(chainSource.transform.position + Vector3.up * 0.35f, nextTarget.transform.position + Vector3.up * 0.35f, lightningLineColor, lightningLineDuration);
        ApplyDirectHit(nextTarget, chainDamage, true, hitEnemies.Count);
        hitEnemies.Add(nextTarget);
        chainSource = nextTarget;
        return true;
    }

    private Enemy FindNearestChainTarget(Enemy chainSource, List<Enemy> excludedEnemies)
    {
        if (chainSource == null)
            return null;

        IReadOnlyList<Enemy> enemies = EnemyRegistry.ActiveEnemies;
        Enemy bestTarget = null;
        float bestDistance = Mathf.Max(0.1f, lightningChainRange);

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.IsActiveTarget || excludedEnemies.Contains(enemy))
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
        SpikeTrapEffect.CreateSpikeAtWorldPosition(spikePosition, spikeTriggerRadius, spikeBleedDamagePerTick, spikeBleedTickInterval, spikeBleedDuration, ownerTower, target, spikeMaxTrapTriggers, spikeTriggerCooldown, spikeTrapDirectDamage, spikeExtraTargets, spikeExtraDamageMultiplier, spikeExtraTargetRadius);
    }

    private void ExplodeMortar()
    {
        IReadOnlyList<Enemy> enemies = EnemyRegistry.ActiveEnemies;
        float radius = Mathf.Max(0.1f, mortarRadius);
        List<Enemy> hitEnemies = new List<Enemy>();
        List<Enemy> allEnemies = new List<Enemy>();

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.IsActiveTarget)
                continue;

            allEnemies.Add(enemy);

            if (Vector3.Distance(enemy.transform.position, mortarImpactPosition) <= radius)
                hitEnemies.Add(enemy);
        }

        foreach (Enemy enemy in hitEnemies)
        {
            if (enemy != null && enemy.IsActiveTarget)
                ApplyDirectHit(enemy, damage);
        }

        if (ownerTower != null && ownerTower.towerRole == TowerRole.Mortar && MortarTowerMasteryManager.TryGetActive(out MortarTowerMasteryManager mortarMasteryManager))
            mortarMasteryManager.RecordMortarImpact(ownerTower, mortarImpactPosition, radius, hitEnemies);

        ApplyMortarFragments(allEnemies, hitEnemies, radius);

        if (mortarCreateCrater)
            MortarCraterEffect.CreateCraterAtWorldPosition(mortarImpactPosition, mortarCraterRadius, mortarCraterDamagePerTick, mortarCraterTickInterval, mortarCraterDuration, ownerTower, mortarCraterStaggers, mortarCraterStaggerMultiplier, mortarCraterStaggerDuration);

        CreateMortarImpactVisual(mortarImpactPosition, radius);
        FinishProjectile();
    }

    private void ApplyMortarFragments(List<Enemy> allEnemies, List<Enemy> directHits, float radius)
    {
        int targets = Mathf.Max(0, mortarFragmentTargets);
        float multiplier = Mathf.Clamp01(mortarFragmentDamageMultiplier);

        if (targets <= 0 || multiplier <= 0f || allEnemies == null)
            return;

        List<Enemy> selectedTargets = new List<Enemy>();
        float searchRadius = radius + Mathf.Max(0f, mortarFragmentSearchRadius);

        for (int i = 0; i < targets; i++)
        {
            Enemy bestTarget = null;
            float bestDistance = searchRadius;

            foreach (Enemy enemy in allEnemies)
            {
                if (enemy == null || enemy.currentHealth <= 0f)
                    continue;

                if (directHits != null && directHits.Contains(enemy))
                    continue;

                if (selectedTargets.Contains(enemy))
                    continue;

                float distance = Vector3.Distance(enemy.transform.position, mortarImpactPosition);

                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                bestTarget = enemy;
            }

            if (bestTarget == null)
                return;

            selectedTargets.Add(bestTarget);
            int fragmentDamage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
            ApplyDirectHit(bestTarget, fragmentDamage);
        }
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
            renderer.sharedMaterial = CreateBuildSafeMaterial(mortarImpactColor, false);

        Destroy(impactObject, 0.25f);
    }

    private void ApplyProjectileVisualMaterial()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer == null)
            return;

        renderer.sharedMaterial = CreateBuildSafeMaterial(GetProjectileColor(), false);
    }

    private Color GetProjectileColor()
    {
        switch (behavior)
        {
            case ProjectileBehavior.LightningChain:
                return lightningProjectileColor;
            case ProjectileBehavior.MortarAOE:
                return mortarProjectileColor;
            case ProjectileBehavior.SpikeTrap:
                return spikeProjectileColor;
            default:
                return directProjectileColor;
        }
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
        return CreateBuildSafeMaterial(color, true);
    }

    private Material CreateBuildSafeMaterial(Color color, bool unlit)
    {
        Shader shader = FindBuildSafeShader(unlit);
        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        return material;
    }

    private Shader FindBuildSafeShader(bool unlit)
    {
        Shader shader = null;

        if (unlit)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        else
            shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null && unlit)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        return shader != null ? shader : Shader.Find("Hidden/InternalErrorShader");
    }
}
