using System;

public static class WaveEventBus
{
    public static event Action<WaveData> WaveStarted;
    public static event Action<WaveData> MiniBossWaveStarted;
    public static event Action<WaveData> BossWaveStarted;

    public static event Action<WaveCompletionResult> WaveCompleted;
    public static event Action<WaveCompletionResult> MiniBossWaveCompleted;
    public static event Action<WaveCompletionResult> BossWaveCompleted;

    public static event Action<WaveData> BuildPhaseStarted;
    public static event Action<WaveData> BlockedBuildPhaseStarted;

    public static event Action GameOverTriggered;

    public static void RaiseWaveStarted(WaveData waveData)
    {
        if (waveData == null)
            return;

        WaveStarted?.Invoke(waveData);

        if (waveData.IsMiniBossWave())
            MiniBossWaveStarted?.Invoke(waveData);

        if (waveData.IsBossWave())
            BossWaveStarted?.Invoke(waveData);
    }

    public static void RaiseWaveCompleted(WaveCompletionResult result)
    {
        if (result == null)
            return;

        WaveCompleted?.Invoke(result);

        if (result.isMiniBossWave)
            MiniBossWaveCompleted?.Invoke(result);

        if (result.isBossWave)
            BossWaveCompleted?.Invoke(result);
    }

    public static void RaiseBuildPhaseStarted(WaveData nextWaveData)
    {
        BuildPhaseStarted?.Invoke(nextWaveData);
    }

    public static void RaiseBlockedBuildPhaseStarted(WaveData nextWaveData)
    {
        BlockedBuildPhaseStarted?.Invoke(nextWaveData);
    }

    public static void RaiseGameOverTriggered()
    {
        GameOverTriggered?.Invoke();
    }

    public static void ClearAllListeners()
    {
        WaveStarted = null;
        MiniBossWaveStarted = null;
        BossWaveStarted = null;

        WaveCompleted = null;
        MiniBossWaveCompleted = null;
        BossWaveCompleted = null;

        BuildPhaseStarted = null;
        BlockedBuildPhaseStarted = null;

        GameOverTriggered = null;
    }
}