using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WaveData
{
    [Header("Wave Info")]
    public int waveNumber;
    public WaveScenario scenario;
    public string scenarioName;
    public string specialHint;

    [Header("Enemy Count")]
    public int requestedEnemyCount;
    public int modifiedEnemyCount;
    public int totalSpawnCount;
    public int normalEnemyCount;
    public int specialEnemyCount;

    [Header("Special Wave Info")]
    public bool isMiniBossWave = false;
    public bool isBossWave = false;
    public bool hasSpecialEnemy = false;

    public EnemyRole mainSpecialRole = EnemyRole.Standard;
    public int miniBossCount = 0;
    public int bossCount = 0;

    [Header("Preview")]
    public bool previewHidden = false;

    [Header("Chaos Variants V1")]
    public bool hasChaosVariants = false;
    public int chaosVariantCount = 0;
    public string chaosVariantSummary = "";

    [Header("Chaos Wave Blocks V1")]
    public bool hasChaosWaveBlocks = false;
    public string chaosWaveName = "";
    public string chaosWaveSummary = "";
    public List<ChaosWaveBlock> chaosWaveBlocks = new List<ChaosWaveBlock>();

    [Header("Applied Modifiers")]
    public List<WaveModifier> appliedModifiers = new List<WaveModifier>();
    public string modifierSummary = "";

    [Header("Spawn Entries")]
    public List<EnemySpawnEntry> spawnEntries = new List<EnemySpawnEntry>();

    public WaveData()
    {
        waveNumber = 1;
        scenario = WaveScenario.StandardIntro;
        scenarioName = "";
        specialHint = "";

        requestedEnemyCount = 0;
        modifiedEnemyCount = 0;
        totalSpawnCount = 0;
        normalEnemyCount = 0;
        specialEnemyCount = 0;

        isMiniBossWave = false;
        isBossWave = false;
        hasSpecialEnemy = false;

        mainSpecialRole = EnemyRole.Standard;
        miniBossCount = 0;
        bossCount = 0;

        previewHidden = false;
        hasChaosVariants = false;
        chaosVariantCount = 0;
        chaosVariantSummary = "";
        hasChaosWaveBlocks = false;
        chaosWaveName = "";
        chaosWaveSummary = "";
        chaosWaveBlocks = new List<ChaosWaveBlock>();
        appliedModifiers = new List<WaveModifier>();
        modifierSummary = "";
        spawnEntries = new List<EnemySpawnEntry>();
    }

    public void RecalculateTotalSpawnCount()
    {
        RecalculateDerivedData();
    }

    public void RecalculateDerivedData()
    {
        totalSpawnCount = 0;
        normalEnemyCount = 0;
        specialEnemyCount = 0;

        miniBossCount = 0;
        bossCount = 0;

        isMiniBossWave = false;
        isBossWave = false;
        hasSpecialEnemy = false;
        hasChaosVariants = false;
        chaosVariantCount = 0;
        chaosVariantSummary = "";
        RefreshChaosWaveBlockSummary();

        mainSpecialRole = EnemyRole.Standard;

        if (spawnEntries == null)
            return;

        foreach (EnemySpawnEntry entry in spawnEntries)
        {
            if (entry == null)
                continue;

            int amount = Mathf.Max(0, entry.amount);

            if (amount <= 0)
                continue;

            totalSpawnCount += amount;

            if (entry.variantType == EnemyVariantType.Chaos)
            {
                chaosVariantCount += amount;
                AppendChaosVariantSummary(entry.enemyRole, amount);
            }

            if (entry.enemyRole == EnemyRole.MiniBoss)
            {
                miniBossCount += amount;
                specialEnemyCount += amount;
                continue;
            }

            if (entry.enemyRole == EnemyRole.Boss)
            {
                bossCount += amount;
                specialEnemyCount += amount;
                continue;
            }

            normalEnemyCount += amount;
        }

        isMiniBossWave = scenario == WaveScenario.MiniBoss || miniBossCount > 0;
        isBossWave = scenario == WaveScenario.Boss || bossCount > 0;

        hasSpecialEnemy = miniBossCount > 0 || bossCount > 0;
        hasChaosVariants = chaosVariantCount > 0;

        if (bossCount > 0)
        {
            mainSpecialRole = EnemyRole.Boss;
        }
        else if (miniBossCount > 0)
        {
            mainSpecialRole = EnemyRole.MiniBoss;
        }
        else
        {
            mainSpecialRole = EnemyRole.Standard;
        }
    }

    public bool IsBossWave()
    {
        return isBossWave;
    }

    public bool IsMiniBossWave()
    {
        return isMiniBossWave;
    }

    public bool HasSpecialWave()
    {
        return isBossWave || isMiniBossWave || hasSpecialEnemy;
    }

    public void SetChaosWaveBlocks(List<ChaosWaveBlock> blocks)
    {
        if (chaosWaveBlocks == null)
            chaosWaveBlocks = new List<ChaosWaveBlock>();

        chaosWaveBlocks.Clear();

        if (blocks != null)
        {
            foreach (ChaosWaveBlock block in blocks)
            {
                if (block == null || !block.IsValid())
                    continue;

                chaosWaveBlocks.Add(block.CreateCopy());
            }
        }

        RefreshChaosWaveBlockSummary();
    }

    public void RefreshChaosWaveBlockSummary()
    {
        hasChaosWaveBlocks = chaosWaveBlocks != null && chaosWaveBlocks.Count > 0;
        chaosWaveName = "";
        chaosWaveSummary = "";

        if (!hasChaosWaveBlocks)
            return;

        ChaosWaveBlock mainBlock = GetMainChaosWaveBlock();

        if (mainBlock != null)
            chaosWaveName = mainBlock.displayName;

        foreach (ChaosWaveBlock block in chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
                continue;

            if (!string.IsNullOrEmpty(chaosWaveSummary))
                chaosWaveSummary += "; ";

            chaosWaveSummary += block.GetShortSummary();
        }
    }

    public ChaosWaveBlock GetMainChaosWaveBlock()
    {
        if (chaosWaveBlocks == null || chaosWaveBlocks.Count == 0)
            return null;

        ChaosWaveBlock best = null;

        foreach (ChaosWaveBlock block in chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
                continue;

            if (best == null)
            {
                best = block;
                continue;
            }

            if (block.priority > best.priority)
            {
                best = block;
                continue;
            }

            if (block.priority == best.priority && block.strengthLevel > best.strengthLevel)
                best = block;
        }

        return best;
    }

    public string GetChaosWaveDetailsText()
    {
        if (!hasChaosWaveBlocks || string.IsNullOrEmpty(chaosWaveSummary))
            return "Keine";

        return chaosWaveSummary;
    }

    private void AppendChaosVariantSummary(EnemyRole role, int amount)
    {
        if (amount <= 0)
            return;

        if (!string.IsNullOrEmpty(chaosVariantSummary))
            chaosVariantSummary += ", ";

        chaosVariantSummary += amount + " Chaos " + role;
    }

    public bool ContainsRole(EnemyRole role)
    {
        return GetRoleCount(role) > 0;
    }

    public int GetRoleCount(EnemyRole role)
    {
        if (spawnEntries == null)
            return 0;

        int count = 0;

        foreach (EnemySpawnEntry entry in spawnEntries)
        {
            if (entry == null)
                continue;

            if (entry.enemyRole != role)
                continue;

            count += Mathf.Max(0, entry.amount);
        }

        return count;
    }

    public EnemyRole GetMainSpecialRole()
    {
        if (bossCount > 0)
            return EnemyRole.Boss;

        if (miniBossCount > 0)
            return EnemyRole.MiniBoss;

        return EnemyRole.Standard;
    }

    public string GetSpecialWaveLabel()
    {
        if (bossCount > 0)
            return "Boss";

        if (miniBossCount > 0)
            return "MiniBoss";

        return "";
    }

    public string GetDebugSummary()
    {
        string summary =
            "Wave " + waveNumber +
            " | Scenario: " + scenario +
            " | Total: " + totalSpawnCount +
            " | Normal: " + normalEnemyCount +
            " | Special: " + specialEnemyCount;

        if (miniBossCount > 0)
            summary += " | MiniBoss: " + miniBossCount;

        if (bossCount > 0)
            summary += " | Boss: " + bossCount;

        if (!string.IsNullOrEmpty(chaosVariantSummary))
            summary += " | Chaos-Varianten: " + chaosVariantSummary;

        if (!string.IsNullOrEmpty(chaosWaveSummary))
            summary += " | Chaos-Wave: " + chaosWaveName + " | Bausteine: " + chaosWaveSummary;

        if (!string.IsNullOrEmpty(modifierSummary))
            summary += " | Modifier: " + modifierSummary;

        return summary;
    }
}