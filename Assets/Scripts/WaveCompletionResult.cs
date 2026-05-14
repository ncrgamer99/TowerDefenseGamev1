using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyRoleCount
{
    public EnemyRole role;
    public int count;

    public EnemyRoleCount(EnemyRole role, int count)
    {
        this.role = role;
        this.count = count;
    }
}

[System.Serializable]
public class WaveCompletionResult
{
    [Header("Wave Info")]
    public int waveNumber;
    public WaveScenario scenario;
    public string scenarioName;

    [Header("Completion")]
    public bool waveCompleted = false;
    public bool isBossWave = false;
    public bool isMiniBossWave = false;
    public bool bossDefeated = false;
    public bool miniBossDefeated = false;

    [Header("Enemy Stats")]
    public int totalSpawnCount = 0;
    public int enemiesKilled = 0;
    public int enemiesReachedBase = 0;
    public int baseDamageTaken = 0;

    [Header("Role Stats")]
    public List<EnemyRoleCount> killedRoles = new List<EnemyRoleCount>();
    public List<EnemyRoleCount> leakedRoles = new List<EnemyRoleCount>();

    [Header("Chaos Variants V1")]
    public int chaosVariantSpawnCount = 0;
    public int chaosVariantKilledCount = 0;
    public int chaosVariantReachedBaseCount = 0;
    public string chaosVariantSummaryAtWaveStart = "";
    public List<EnemyRoleCount> killedChaosVariantRoles = new List<EnemyRoleCount>();
    public List<EnemyRoleCount> leakedChaosVariantRoles = new List<EnemyRoleCount>();

    [Header("Chaos Wave Blocks V1")]
    public bool hadChaosWaveBlocksAtWaveStart = false;
    public string chaosWaveNameAtWaveStart = "";
    public string chaosWaveBlockSummaryAtWaveStart = "";
    public List<ChaosWaveBlock> chaosWaveBlocksAtWaveStart = new List<ChaosWaveBlock>();

    [Header("Chaos / Justice Snapshot")]
    public int chaosLevelAtWaveStart = 0;
    public int goldJusticeLevelAtWaveStart = 0;
    public int xpJusticeLevelAtWaveStart = 0;
    public float goldRewardMultiplierAtWaveStart = 1f;
    public float xpRewardMultiplierAtWaveStart = 1f;
    public string activeRiskModifierSummary = "";
    public int activeRiskModifierCountAtWaveStart = 0;
    public List<WaveModifier> activeRiskModifiersAtWaveStart = new List<WaveModifier>();

    public void InitializeFromWaveData(WaveData waveData)
    {
        if (waveData == null)
            return;

        waveNumber = waveData.waveNumber;
        scenario = waveData.scenario;
        scenarioName = waveData.scenarioName;

        totalSpawnCount = waveData.totalSpawnCount;
        chaosVariantSpawnCount = waveData.chaosVariantCount;
        chaosVariantSummaryAtWaveStart = waveData.chaosVariantSummary;
        SetChaosWaveBlockSnapshot(waveData.chaosWaveBlocks, waveData.chaosWaveName, waveData.chaosWaveSummary);

        isBossWave = waveData.IsBossWave();
        isMiniBossWave = waveData.IsMiniBossWave();

        waveCompleted = false;
        bossDefeated = false;
        miniBossDefeated = false;

        enemiesKilled = 0;
        enemiesReachedBase = 0;
        baseDamageTaken = 0;
        chaosVariantKilledCount = 0;
        chaosVariantReachedBaseCount = 0;

        chaosLevelAtWaveStart = 0;
        goldJusticeLevelAtWaveStart = 0;
        xpJusticeLevelAtWaveStart = 0;
        goldRewardMultiplierAtWaveStart = 1f;
        xpRewardMultiplierAtWaveStart = 1f;
        activeRiskModifierSummary = waveData.modifierSummary;
        SetActiveRiskModifierSnapshot(waveData.appliedModifiers);

        killedRoles.Clear();
        leakedRoles.Clear();
        killedChaosVariantRoles.Clear();
        leakedChaosVariantRoles.Clear();
    }

    public void SetChaosWaveBlockSnapshot(List<ChaosWaveBlock> blocks, string waveName, string summary)
    {
        if (chaosWaveBlocksAtWaveStart == null)
            chaosWaveBlocksAtWaveStart = new List<ChaosWaveBlock>();

        chaosWaveBlocksAtWaveStart.Clear();
        chaosWaveNameAtWaveStart = string.IsNullOrEmpty(waveName) ? "" : waveName;
        chaosWaveBlockSummaryAtWaveStart = string.IsNullOrEmpty(summary) ? "" : summary;
        hadChaosWaveBlocksAtWaveStart = false;

        if (blocks == null)
            return;

        foreach (ChaosWaveBlock block in blocks)
        {
            if (block == null || !block.IsValid())
                continue;

            chaosWaveBlocksAtWaveStart.Add(block.CreateCopy());
        }

        hadChaosWaveBlocksAtWaveStart = chaosWaveBlocksAtWaveStart.Count > 0;
    }

    public string GetChaosWaveBlockDetailsText()
    {
        if (!hadChaosWaveBlocksAtWaveStart)
            return "Keine";

        if (!string.IsNullOrEmpty(chaosWaveBlockSummaryAtWaveStart))
            return chaosWaveBlockSummaryAtWaveStart;

        if (chaosWaveBlocksAtWaveStart == null || chaosWaveBlocksAtWaveStart.Count == 0)
            return "Keine";

        string text = "";

        foreach (ChaosWaveBlock block in chaosWaveBlocksAtWaveStart)
        {
            if (block == null || !block.IsValid())
                continue;

            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "- " + block.GetShortSummary();
        }

        return string.IsNullOrEmpty(text) ? "Keine" : text;
    }

    public void SetActiveRiskModifierSnapshot(List<WaveModifier> modifiers)
    {
        if (activeRiskModifiersAtWaveStart == null)
            activeRiskModifiersAtWaveStart = new List<WaveModifier>();

        activeRiskModifiersAtWaveStart.Clear();
        activeRiskModifierCountAtWaveStart = 0;

        if (modifiers == null)
            return;

        foreach (WaveModifier modifier in modifiers)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            activeRiskModifiersAtWaveStart.Add(modifier.CreateCopy());
            activeRiskModifierCountAtWaveStart++;
        }
    }

    public string GetActiveRiskModifierDetailsText(int maxEntries)
    {
        if (activeRiskModifiersAtWaveStart == null || activeRiskModifiersAtWaveStart.Count == 0)
            return "Keine";

        int safeMax = Mathf.Max(1, maxEntries);
        int startIndex = Mathf.Max(0, activeRiskModifiersAtWaveStart.Count - safeMax);
        string text = "";

        for (int i = startIndex; i < activeRiskModifiersAtWaveStart.Count; i++)
        {
            WaveModifier modifier = activeRiskModifiersAtWaveStart[i];

            if (modifier == null)
                continue;

            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "- " + modifier.GetDebugSummary();
        }

        int hiddenCount = activeRiskModifiersAtWaveStart.Count - safeMax;
        if (hiddenCount > 0)
            text += "\n- ... " + hiddenCount + " ältere Risiko-Modifikator(en) ausgeblendet.";

        if (string.IsNullOrEmpty(text))
            return "Keine";

        return text;
    }

    public void RegisterEnemyKilled(EnemyRole role)
    {
        RegisterEnemyKilled(role, EnemyVariantType.Normal);
    }

    public void RegisterEnemyKilled(Enemy enemy)
    {
        if (enemy == null)
            return;

        RegisterEnemyKilled(enemy.enemyRole, enemy.enemyVariantType);
    }

    public void RegisterEnemyKilled(EnemyRole role, EnemyVariantType variantType)
    {
        enemiesKilled++;
        AddRoleCount(killedRoles, role, 1);

        if (variantType == EnemyVariantType.Chaos)
        {
            chaosVariantKilledCount++;
            AddRoleCount(killedChaosVariantRoles, role, 1);
        }

        if (role == EnemyRole.Boss)
            bossDefeated = true;

        if (role == EnemyRole.MiniBoss)
            miniBossDefeated = true;
    }

    public void RegisterEnemyReachedBase(EnemyRole role, int damage)
    {
        RegisterEnemyReachedBase(role, EnemyVariantType.Normal, damage);
    }

    public void RegisterEnemyReachedBase(Enemy enemy, int damage)
    {
        if (enemy == null)
            return;

        RegisterEnemyReachedBase(enemy.enemyRole, enemy.enemyVariantType, damage);
    }

    public void RegisterEnemyReachedBase(EnemyRole role, EnemyVariantType variantType, int damage)
    {
        enemiesReachedBase++;
        baseDamageTaken += Mathf.Max(0, damage);

        AddRoleCount(leakedRoles, role, 1);

        if (variantType == EnemyVariantType.Chaos)
        {
            chaosVariantReachedBaseCount++;
            AddRoleCount(leakedChaosVariantRoles, role, 1);
        }
    }

    public void MarkCompleted()
    {
        waveCompleted = true;
    }

    public int GetHandledEnemyCount()
    {
        return enemiesKilled + enemiesReachedBase;
    }

    public bool AllSpawnedEnemiesHandled()
    {
        return GetHandledEnemyCount() >= totalSpawnCount;
    }

    public int GetKilledRoleCount(EnemyRole role)
    {
        return GetRoleCount(killedRoles, role);
    }

    public int GetLeakedRoleCount(EnemyRole role)
    {
        return GetRoleCount(leakedRoles, role);
    }

    public float GetKillPercent()
    {
        if (totalSpawnCount <= 0)
            return 0f;

        return Mathf.Clamp01(enemiesKilled / (float)totalSpawnCount);
    }

    public float GetLeakPercent()
    {
        if (totalSpawnCount <= 0)
            return 0f;

        return Mathf.Clamp01(enemiesReachedBase / (float)totalSpawnCount);
    }

    public int GetChaosWaveBlockCount()
    {
        if (chaosWaveBlocksAtWaveStart == null)
            return 0;

        return chaosWaveBlocksAtWaveStart.Count;
    }

    public int GetDangerScore()
    {
        int score = 0;
        score += baseDamageTaken * 4;
        score += enemiesReachedBase * 3;
        score += activeRiskModifierCountAtWaveStart * 2;
        score += chaosLevelAtWaveStart * 2;
        score += GetChaosWaveBlockCount() * 3;
        score += chaosVariantSpawnCount;

        if (isBossWave)
            score += 8;
        else if (isMiniBossWave)
            score += 4;

        return score;
    }

    public int GetRunImpactScore()
    {
        int score = Mathf.Max(0, waveNumber);
        score += enemiesKilled;
        score += bossDefeated ? 30 : 0;
        score += miniBossDefeated ? 12 : 0;
        score += chaosLevelAtWaveStart * 5;
        score += activeRiskModifierCountAtWaveStart * 3;
        score += chaosVariantKilledCount * 2;
        score += GetChaosWaveBlockCount() * 5;
        score -= baseDamageTaken * 2;
        score -= enemiesReachedBase;
        return Mathf.Max(0, score);
    }

    public string GetOutcomeText()
    {
        if (!waveCompleted)
            return "Nicht abgeschlossen";

        if (isBossWave)
            return bossDefeated ? "Boss besiegt" : "Boss-Wave überstanden";

        if (isMiniBossWave)
            return miniBossDefeated ? "MiniBoss besiegt" : "MiniBoss-Wave überstanden";

        if (enemiesReachedBase <= 0)
            return "Perfekt verteidigt";

        if (baseDamageTaken > 0)
            return "Überstanden mit Schaden";

        return "Überstanden";
    }

    public string GetShortWaveLabel()
    {
        string safeName = string.IsNullOrEmpty(scenarioName) ? scenario.ToString() : scenarioName;
        return "Wave " + waveNumber + " - " + safeName;
    }

    public string GetCompactSummaryLine()
    {
        string text =
            GetShortWaveLabel() +
            " | " + GetOutcomeText() +
            " | Kills " + enemiesKilled + "/" + totalSpawnCount +
            " | Leaks " + enemiesReachedBase +
            " | Base " + baseDamageTaken;

        if (chaosLevelAtWaveStart > 0)
            text += " | Chaos " + chaosLevelAtWaveStart;

        if (hadChaosWaveBlocksAtWaveStart)
            text += " | Bausteine " + GetChaosWaveBlockCount();

        if (chaosVariantSpawnCount > 0)
            text += " | Varianten " + chaosVariantSpawnCount;

        return text;
    }

    public string GetRoleBreakdownText()
    {
        string killedText = BuildRoleCountText(killedRoles);
        string leakedText = BuildRoleCountText(leakedRoles);
        string text = "";

        if (!string.IsNullOrEmpty(killedText))
            text += "Getötet: " + killedText;

        if (!string.IsNullOrEmpty(leakedText))
        {
            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "Geleakt: " + leakedText;
        }

        return string.IsNullOrEmpty(text) ? "Keine Rollenaufschlüsselung." : text;
    }

    public string GetChaosSnapshotText()
    {
        string text =
            "Chaos " + chaosLevelAtWaveStart +
            " | Gold-G " + goldJusticeLevelAtWaveStart +
            " | XP-G " + xpJusticeLevelAtWaveStart +
            " | Gold x" + goldRewardMultiplierAtWaveStart.ToString("0.00") +
            " | XP x" + xpRewardMultiplierAtWaveStart.ToString("0.00");

        if (activeRiskModifierCountAtWaveStart > 0)
            text += " | Risiken " + activeRiskModifierCountAtWaveStart;

        if (hadChaosWaveBlocksAtWaveStart)
            text += " | Chaos-Wave " + (string.IsNullOrEmpty(chaosWaveNameAtWaveStart) ? "Ja" : chaosWaveNameAtWaveStart);

        if (chaosVariantSpawnCount > 0)
            text += " | Varianten " + chaosVariantSpawnCount;

        return text;
    }

    public string GetDebugSummary()
    {
        string summary =
            "Wave " + waveNumber +
            " completed: " + waveCompleted +
            " | Scenario: " + scenario +
            " | Spawned: " + totalSpawnCount +
            " | Killed: " + enemiesKilled +
            " | Leaked: " + enemiesReachedBase +
            " | Base Damage: " + baseDamageTaken +
            " | Chaos Variants: " + chaosVariantKilledCount + "/" + chaosVariantSpawnCount;

        if (chaosLevelAtWaveStart > 0 || goldJusticeLevelAtWaveStart > 0 || xpJusticeLevelAtWaveStart > 0)
        {
            summary +=
                " | Chaos: " + chaosLevelAtWaveStart +
                " | GoldJustice: " + goldJusticeLevelAtWaveStart +
                " | XPJustice: " + xpJusticeLevelAtWaveStart +
                " | GoldReward x" + goldRewardMultiplierAtWaveStart.ToString("0.00") +
                " | XPReward x" + xpRewardMultiplierAtWaveStart.ToString("0.00");
        }

        if (!string.IsNullOrEmpty(chaosWaveBlockSummaryAtWaveStart))
            summary += " | Chaos-Wave: " + chaosWaveNameAtWaveStart + " | Bausteine: " + chaosWaveBlockSummaryAtWaveStart;

        if (!string.IsNullOrEmpty(activeRiskModifierSummary))
            summary += " | Modifier: " + activeRiskModifierSummary;

        return summary;
    }


    public string GetChaosVariantDetailsText()
    {
        if (chaosVariantSpawnCount <= 0 && chaosVariantKilledCount <= 0 && chaosVariantReachedBaseCount <= 0)
            return "Keine";

        string text = "Spawn: " + chaosVariantSpawnCount + " | Kills: " + chaosVariantKilledCount + " | Leaks: " + chaosVariantReachedBaseCount;

        if (!string.IsNullOrEmpty(chaosVariantSummaryAtWaveStart))
            text += "\nVarianten: " + chaosVariantSummaryAtWaveStart;

        string killedText = BuildRoleCountText(killedChaosVariantRoles);
        if (!string.IsNullOrEmpty(killedText))
            text += "\nGetötet: " + killedText;

        string leakedText = BuildRoleCountText(leakedChaosVariantRoles);
        if (!string.IsNullOrEmpty(leakedText))
            text += "\nGeleakt: " + leakedText;

        return text;
    }

    private string BuildRoleCountText(List<EnemyRoleCount> list)
    {
        if (list == null || list.Count == 0)
            return "";

        string text = "";

        foreach (EnemyRoleCount entry in list)
        {
            if (entry == null || entry.count <= 0)
                continue;

            if (!string.IsNullOrEmpty(text))
                text += ", ";

            text += entry.count + " " + entry.role;
        }

        return text;
    }

    private void AddRoleCount(List<EnemyRoleCount> list, EnemyRole role, int amount)
    {
        if (list == null)
            return;

        if (amount <= 0)
            return;

        foreach (EnemyRoleCount entry in list)
        {
            if (entry == null)
                continue;

            if (entry.role == role)
            {
                entry.count += amount;
                return;
            }
        }

        list.Add(new EnemyRoleCount(role, amount));
    }

    private int GetRoleCount(List<EnemyRoleCount> list, EnemyRole role)
    {
        if (list == null)
            return 0;

        foreach (EnemyRoleCount entry in list)
        {
            if (entry == null)
                continue;

            if (entry.role == role)
                return entry.count;
        }

        return 0;
    }
}
