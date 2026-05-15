using UnityEngine;

public enum ChaosWaveBlockType
{
    None,
    RolePressure,
    Density,
    Toughness,
    ChaosVariantGroup,
    Rearguard,
    Armor,
    Resistance,
    PreviewHidden
}

[System.Serializable]
public class ChaosWaveBlock
{
    [Header("Info")]
    public ChaosWaveBlockType blockType = ChaosWaveBlockType.None;
    public string displayName = "Chaos-Baustein";
    public string detailText = "";

    [Header("Strength")]
    public int strengthLevel = 1;
    public int priority = 0;

    [Header("Spawn Timing")]
    public float spawnDelayMultiplier = 1f;

    [Header("Enemy Stats")]
    public float healthMultiplier = 1f;
    public int armorBonus = 0;
    public float effectDamageMultiplier = 1f;
    public float slowResistanceBonus = 0f;

    [Header("Role / Rearguard")]
    public EnemyRole preferredRole = EnemyRole.Standard;
    public int convertedEnemyCount = 0;
    public bool appendAtEnd = false;

    [Header("Chaos Variants")]
    public float chaosVariantChanceBonus = 0f;
    public int flatChaosVariantBonus = 0;

    [Header("Runtime")]
    public bool wasApplied = false;
    public int actualAffectedEnemies = 0;

    public bool IsValid()
    {
        return blockType != ChaosWaveBlockType.None;
    }

    public ChaosWaveBlock CreateCopy()
    {
        return new ChaosWaveBlock
        {
            blockType = blockType,
            displayName = displayName,
            detailText = detailText,
            strengthLevel = strengthLevel,
            priority = priority,
            spawnDelayMultiplier = spawnDelayMultiplier,
            healthMultiplier = healthMultiplier,
            armorBonus = armorBonus,
            effectDamageMultiplier = effectDamageMultiplier,
            slowResistanceBonus = slowResistanceBonus,
            preferredRole = preferredRole,
            convertedEnemyCount = convertedEnemyCount,
            appendAtEnd = appendAtEnd,
            chaosVariantChanceBonus = chaosVariantChanceBonus,
            flatChaosVariantBonus = flatChaosVariantBonus,
            wasApplied = wasApplied,
            actualAffectedEnemies = actualAffectedEnemies
        };
    }

    public string GetDisplayNameWithStrength()
    {
        string safeName = string.IsNullOrEmpty(displayName) ? blockType.ToString() : displayName;
        return safeName + " (Stufe " + Mathf.Max(1, strengthLevel) + ")";
    }

    public string GetShortSummary()
    {
        string text = "Wave-Effekt: " + GetDisplayNameWithStrength();

        switch (blockType)
        {
            case ChaosWaveBlockType.Density:
                text += " | SpawnDelay x" + spawnDelayMultiplier.ToString("0.00");
                break;
            case ChaosWaveBlockType.Toughness:
                text += " | Leben x" + healthMultiplier.ToString("0.00");
                break;
            case ChaosWaveBlockType.Rearguard:
                text += " | Nachhut: " + Mathf.Max(0, actualAffectedEnemies) + " " + preferredRole;
                break;
            case ChaosWaveBlockType.RolePressure:
                text += " | Rollendruck: " + Mathf.Max(0, actualAffectedEnemies) + " " + preferredRole;
                break;
            case ChaosWaveBlockType.Armor:
                text += " | Rüstung +" + Mathf.Max(0, armorBonus);
                break;
            case ChaosWaveBlockType.ChaosVariantGroup:
                text += " | Chaos-Varianten +" + Mathf.RoundToInt(Mathf.Max(0f, chaosVariantChanceBonus) * 100f) + "%";
                if (flatChaosVariantBonus > 0)
                    text += " / +" + flatChaosVariantBonus + " Cap";
                break;
            case ChaosWaveBlockType.Resistance:
                text += " | Effektresistenz";
                break;
            case ChaosWaveBlockType.PreviewHidden:
                text += " | Preview verborgen (nicht für V1)";
                break;
        }

        if (!string.IsNullOrEmpty(detailText))
            text += " | " + detailText;

        return text;
    }
}
