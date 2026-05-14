using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WaveHistory
{
    [Header("Settings")]
    public int maxStoredResults = 200;
    public bool keepFullRunHistoryForResultScreen = true;

    [Header("Stored Results")]
    public List<WaveCompletionResult> completedWaves = new List<WaveCompletionResult>();

    public void AddResult(WaveCompletionResult result)
    {
        if (result == null)
            return;

        if (completedWaves == null)
            completedWaves = new List<WaveCompletionResult>();

        completedWaves.Add(result);
        TrimHistory();
    }

    public void Clear()
    {
        if (completedWaves == null)
        {
            completedWaves = new List<WaveCompletionResult>();
            return;
        }

        completedWaves.Clear();
    }

    public WaveCompletionResult GetLatestResult()
    {
        if (completedWaves == null || completedWaves.Count == 0)
            return null;

        return completedWaves[completedWaves.Count - 1];
    }

    public int GetCompletedWaveCount()
    {
        if (completedWaves == null)
            return 0;

        return completedWaves.Count;
    }

    public int GetHighestWaveNumberReached()
    {
        int highest = 0;

        if (completedWaves == null)
            return highest;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            highest = Mathf.Max(highest, result.waveNumber);
        }

        return highest;
    }

    public int GetTotalKills()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            total += result.enemiesKilled;
        }

        return total;
    }

    public int GetTotalLeaks()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            total += result.enemiesReachedBase;
        }

        return total;
    }

    public int GetTotalBaseDamageTaken()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            total += result.baseDamageTaken;
        }

        return total;
    }

    public int GetTotalSpawnedEnemies()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            total += result.totalSpawnCount;
        }

        return total;
    }

    public float GetOverallKillPercent()
    {
        int spawned = GetTotalSpawnedEnemies();

        if (spawned <= 0)
            return 0f;

        return Mathf.Clamp01(GetTotalKills() / (float)spawned);
    }

    public int GetBossWavesCompleted()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            if (result.isBossWave && result.waveCompleted)
                total++;
        }

        return total;
    }

    public int GetMiniBossWavesCompleted()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            if (result.isMiniBossWave && result.waveCompleted)
                total++;
        }

        return total;
    }

    public int GetBossKills()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            if (result.bossDefeated)
                total++;
        }

        return total;
    }

    public int GetMiniBossKills()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            if (result.miniBossDefeated)
                total++;
        }

        return total;
    }

    public int GetChaosWavesCompleted()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            if (result.waveCompleted && result.chaosLevelAtWaveStart > 0)
                total++;
        }

        return total;
    }

    public int GetHighestChaosLevelSeen()
    {
        int highest = 0;

        if (completedWaves == null)
            return highest;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            highest = Mathf.Max(highest, result.chaosLevelAtWaveStart);
        }

        return highest;
    }

    public int GetHighestGoldJusticeLevelSeen()
    {
        int highest = 0;

        if (completedWaves == null)
            return highest;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            highest = Mathf.Max(highest, result.goldJusticeLevelAtWaveStart);
        }

        return highest;
    }

    public int GetHighestXpJusticeLevelSeen()
    {
        int highest = 0;

        if (completedWaves == null)
            return highest;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            highest = Mathf.Max(highest, result.xpJusticeLevelAtWaveStart);
        }

        return highest;
    }

    public int GetMaxActiveRiskModifierCountSeen()
    {
        int highest = 0;

        if (completedWaves == null)
            return highest;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            highest = Mathf.Max(highest, result.activeRiskModifierCountAtWaveStart);
        }

        return highest;
    }

    public int GetTotalChaosVariantSpawns()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            total += result.chaosVariantSpawnCount;
        }

        return total;
    }

    public int GetTotalChaosVariantKills()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            total += result.chaosVariantKilledCount;
        }

        return total;
    }

    public int GetTotalChaosVariantLeaks()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            total += result.chaosVariantReachedBaseCount;
        }

        return total;
    }

    public int GetChaosWaveBlockWavesCompleted()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            if (result.waveCompleted && result.hadChaosWaveBlocksAtWaveStart)
                total++;
        }

        return total;
    }

    public int GetTotalChaosWaveBlocksSeen()
    {
        int total = 0;

        if (completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null || result.chaosWaveBlocksAtWaveStart == null)
                continue;

            total += result.chaosWaveBlocksAtWaveStart.Count;
        }

        return total;
    }

    public WaveCompletionResult GetMostDangerousWave()
    {
        WaveCompletionResult best = null;
        int bestScore = -1;

        if (completedWaves == null)
            return null;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            int score = result.GetDangerScore();

            if (score > bestScore)
            {
                bestScore = score;
                best = result;
            }
        }

        return best;
    }

    public WaveCompletionResult GetMostImpactfulWave()
    {
        WaveCompletionResult best = null;
        int bestScore = -1;

        if (completedWaves == null)
            return null;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            int score = result.GetRunImpactScore();

            if (score > bestScore)
            {
                bestScore = score;
                best = result;
            }
        }

        return best;
    }

    public WaveCompletionResult GetHighestChaosVariantWave()
    {
        WaveCompletionResult best = null;
        int bestCount = -1;

        if (completedWaves == null)
            return null;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            if (result.chaosVariantSpawnCount > bestCount)
            {
                bestCount = result.chaosVariantSpawnCount;
                best = result;
            }
        }

        return bestCount > 0 ? best : null;
    }

    public WaveCompletionResult GetHighestChaosWaveBlockWave()
    {
        WaveCompletionResult best = null;
        int bestCount = -1;

        if (completedWaves == null)
            return null;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null)
                continue;

            int count = result.GetChaosWaveBlockCount();

            if (count > bestCount)
            {
                bestCount = count;
                best = result;
            }
        }

        return bestCount > 0 ? best : null;
    }

    public WaveCompletionResult GetClosestSurvivedWave()
    {
        WaveCompletionResult best = null;
        int bestScore = -1;

        if (completedWaves == null)
            return null;

        foreach (WaveCompletionResult result in completedWaves)
        {
            if (result == null || !result.waveCompleted)
                continue;

            int score = result.enemiesReachedBase * 3 + result.baseDamageTaken * 5;

            if (score > bestScore)
            {
                bestScore = score;
                best = result;
            }
        }

        return bestScore > 0 ? best : null;
    }

    public string GetChaosWaveBlockHistorySummary(int maxEntries)
    {
        if (completedWaves == null || completedWaves.Count == 0)
            return "Keine";

        int safeMax = Mathf.Max(1, maxEntries);
        int shown = 0;
        string text = "";

        for (int i = completedWaves.Count - 1; i >= 0 && shown < safeMax; i--)
        {
            WaveCompletionResult result = completedWaves[i];

            if (result == null || !result.hadChaosWaveBlocksAtWaveStart)
                continue;

            if (!string.IsNullOrEmpty(text))
                text += "\n";

            string waveName = string.IsNullOrEmpty(result.chaosWaveNameAtWaveStart) ? "Chaos-Wave" : result.chaosWaveNameAtWaveStart;
            text += "Wave " + result.waveNumber + " - " + waveName + ": " + result.GetChaosWaveBlockDetailsText();
            shown++;
        }

        return string.IsNullOrEmpty(text) ? "Keine" : text;
    }

    public string GetRecentWaveSummary(int maxEntries)
    {
        if (completedWaves == null || completedWaves.Count == 0)
            return "Keine abgeschlossenen Waves.";

        int safeMax = Mathf.Max(1, maxEntries);
        int startIndex = Mathf.Max(0, completedWaves.Count - safeMax);
        string text = "";

        for (int i = startIndex; i < completedWaves.Count; i++)
        {
            WaveCompletionResult result = completedWaves[i];

            if (result == null)
                continue;

            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "- " + result.GetCompactSummaryLine();
        }

        return string.IsNullOrEmpty(text) ? "Keine abgeschlossenen Waves." : text;
    }

    public string GetBossTimelineSummary(int maxEntries)
    {
        if (completedWaves == null || completedWaves.Count == 0)
            return "Keine Boss-Waves.";

        int safeMax = Mathf.Max(1, maxEntries);
        int shown = 0;
        string text = "";

        for (int i = completedWaves.Count - 1; i >= 0 && shown < safeMax; i--)
        {
            WaveCompletionResult result = completedWaves[i];

            if (result == null || !result.isBossWave)
                continue;

            if (!string.IsNullOrEmpty(text))
                text = "\n" + text;

            string line = "- Wave " + result.waveNumber + ": " + result.GetOutcomeText() + " | Chaos " + result.chaosLevelAtWaveStart + " | Leaks " + result.enemiesReachedBase + " | Base " + result.baseDamageTaken;
            text = line + text;
            shown++;
        }

        return string.IsNullOrEmpty(text) ? "Keine Boss-Waves." : text;
    }

    public string GetDebugSummary()
    {
        return
            "Wave History" +
            " | Completed Waves: " + GetCompletedWaveCount() +
            " | Kills: " + GetTotalKills() +
            " | Leaks: " + GetTotalLeaks() +
            " | Base Damage: " + GetTotalBaseDamageTaken() +
            " | Kill%: " + Mathf.RoundToInt(GetOverallKillPercent() * 100f) + "%" +
            " | Chaos Variant Kills: " + GetTotalChaosVariantKills() + "/" + GetTotalChaosVariantSpawns() +
            " | Chaos Block Waves: " + GetChaosWaveBlockWavesCompleted() +
            " | MiniBoss Waves: " + GetMiniBossWavesCompleted() +
            " | Boss Waves: " + GetBossWavesCompleted() +
            " | Boss Kills: " + GetBossKills() +
            " | Chaos Waves: " + GetChaosWavesCompleted() +
            " | Highest Chaos: " + GetHighestChaosLevelSeen();
    }

    private void TrimHistory()
    {
        if (completedWaves == null)
            return;

        if (keepFullRunHistoryForResultScreen)
            return;

        int safeMax = Mathf.Max(1, maxStoredResults);

        while (completedWaves.Count > safeMax)
            completedWaves.RemoveAt(0);
    }
}
