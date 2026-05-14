using UnityEngine;

[System.Serializable]
public class EnemySpawnEntry
{
    [Header("Enemy Type")]
    public EnemyRole enemyRole = EnemyRole.Standard;

    [Header("Variant")]
    public EnemyVariantType variantType = EnemyVariantType.Normal;

    [Header("Amount")]
    public int amount = 1;

    [Header("Timing")]
    public float spawnDelay = 0.4f;

    [Header("Chaos Wave Entry Effects V1")]
    public bool hasChaosWaveEffect = false;
    public float waveHealthMultiplier = 1f;
    public int waveArmorBonus = 0;
    public float waveEffectDamageMultiplier = 1f;
    public float waveSlowResistanceBonus = 0f;
    public string waveEffectSummary = "";

    [Header("Optional Override")]
    public GameObject enemyPrefabOverride;

    public EnemySpawnEntry()
    {
        enemyRole = EnemyRole.Standard;
        variantType = EnemyVariantType.Normal;
        amount = 1;
        spawnDelay = 0.4f;
        enemyPrefabOverride = null;
        ResetWaveEffects();
    }

    public EnemySpawnEntry(EnemyRole role, int amount, float spawnDelay)
    {
        this.enemyRole = role;
        this.variantType = EnemyVariantType.Normal;
        this.amount = Mathf.Max(1, amount);
        this.spawnDelay = Mathf.Max(0.05f, spawnDelay);
        this.enemyPrefabOverride = null;
        ResetWaveEffects();
    }

    public EnemySpawnEntry(EnemyRole role, int amount, float spawnDelay, GameObject prefabOverride)
    {
        this.enemyRole = role;
        this.variantType = EnemyVariantType.Normal;
        this.amount = Mathf.Max(1, amount);
        this.spawnDelay = Mathf.Max(0.05f, spawnDelay);
        this.enemyPrefabOverride = prefabOverride;
        ResetWaveEffects();
    }

    public EnemySpawnEntry(EnemyRole role, EnemyVariantType variantType, int amount, float spawnDelay)
    {
        this.enemyRole = role;
        this.variantType = variantType;
        this.amount = Mathf.Max(1, amount);
        this.spawnDelay = Mathf.Max(0.05f, spawnDelay);
        this.enemyPrefabOverride = null;
        ResetWaveEffects();
    }

    public EnemySpawnEntry(EnemyRole role, EnemyVariantType variantType, int amount, float spawnDelay, GameObject prefabOverride)
    {
        this.enemyRole = role;
        this.variantType = variantType;
        this.amount = Mathf.Max(1, amount);
        this.spawnDelay = Mathf.Max(0.05f, spawnDelay);
        this.enemyPrefabOverride = prefabOverride;
        ResetWaveEffects();
    }

    public void ResetWaveEffects()
    {
        hasChaosWaveEffect = false;
        waveHealthMultiplier = 1f;
        waveArmorBonus = 0;
        waveEffectDamageMultiplier = 1f;
        waveSlowResistanceBonus = 0f;
        waveEffectSummary = "";
    }

    public bool HasChaosWaveEntryEffect()
    {
        return hasChaosWaveEffect ||
               waveHealthMultiplier != 1f ||
               waveArmorBonus != 0 ||
               waveEffectDamageMultiplier != 1f ||
               waveSlowResistanceBonus != 0f;
    }

    public void ApplyChaosWaveBlockEffect(ChaosWaveBlock block, float healthMultiplierOverride, int armorBonusOverride, float effectDamageMultiplierOverride, float slowResistanceBonusOverride)
    {
        if (block == null || !block.IsValid())
            return;

        hasChaosWaveEffect = true;
        waveHealthMultiplier *= Mathf.Max(0.1f, healthMultiplierOverride);
        waveArmorBonus += armorBonusOverride;
        waveEffectDamageMultiplier *= Mathf.Max(0.1f, effectDamageMultiplierOverride);
        waveSlowResistanceBonus += slowResistanceBonusOverride;

        string label = block.displayName;
        if (string.IsNullOrEmpty(label))
            label = block.blockType.ToString();

        if (!string.IsNullOrEmpty(waveEffectSummary))
            waveEffectSummary += ", ";

        waveEffectSummary += label;
    }

    public void CopyWaveEffectsFrom(EnemySpawnEntry source)
    {
        if (source == null)
        {
            ResetWaveEffects();
            return;
        }

        hasChaosWaveEffect = source.hasChaosWaveEffect;
        waveHealthMultiplier = source.waveHealthMultiplier;
        waveArmorBonus = source.waveArmorBonus;
        waveEffectDamageMultiplier = source.waveEffectDamageMultiplier;
        waveSlowResistanceBonus = source.waveSlowResistanceBonus;
        waveEffectSummary = source.waveEffectSummary;
    }

    public EnemySpawnEntry CreateCopy()
    {
        EnemySpawnEntry copy = new EnemySpawnEntry(enemyRole, variantType, amount, spawnDelay, enemyPrefabOverride);
        copy.CopyWaveEffectsFrom(this);
        return copy;
    }

    public EnemySpawnEntry CreateCopyWithAmount(int newAmount)
    {
        EnemySpawnEntry copy = new EnemySpawnEntry(enemyRole, variantType, Mathf.Max(1, newAmount), spawnDelay, enemyPrefabOverride);
        copy.CopyWaveEffectsFrom(this);
        return copy;
    }

    public EnemySpawnEntry CreateCopyWithVariantAndAmount(EnemyVariantType newVariantType, int newAmount)
    {
        EnemySpawnEntry copy = new EnemySpawnEntry(enemyRole, newVariantType, Mathf.Max(1, newAmount), spawnDelay, enemyPrefabOverride);
        copy.CopyWaveEffectsFrom(this);
        return copy;
    }
}
