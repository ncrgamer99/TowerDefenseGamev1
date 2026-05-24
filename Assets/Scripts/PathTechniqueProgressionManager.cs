using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum PathTechniqueCategory
{
    Overview,
    EventPool,
    EventQuality,
    RescuePower,
    PathTools,
    TileTechnique,
    RiftArchitecture
}

public enum PathTechniqueNodeKind
{
    Core,
    EventPoolUnlock,
    EventQuality,
    RescuePower,
    PathTool,
    TileTechnique,
    RiftArchitecture
}

[System.Serializable]
public class PathTechniqueNodeDefinition
{
    public string nodeId;
    public string displayName;
    public PathTechniqueCategory category;
    public PathTechniqueNodeKind kind;
    public int blueprintCost;
    public int riftBlueprintCost;
    public int requiredLevel;
    public int slotCost;
    public bool unlockedByDefault;
    public string effectText;
    public string requirementText;

    public bool RequiresLoadoutSlot()
    {
        return slotCost > 0 && (kind == PathTechniqueNodeKind.EventQuality || kind == PathTechniqueNodeKind.RescuePower || kind == PathTechniqueNodeKind.PathTool || kind == PathTechniqueNodeKind.TileTechnique || kind == PathTechniqueNodeKind.RiftArchitecture);
    }
}

[System.Serializable]
public class PathTechniqueNodeState
{
    public string nodeId;
    public bool purchased;
    public bool active;
}

public class PathTechniqueProgressionManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_PathTechnique_";

    public static PathTechniqueProgressionManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Progression")]
    public int pathTechniqueLevel = 1;
    public int pathTechniqueXP = 0;
    public int blueprints = 0;
    public int riftBlueprints = 0;

    [Header("Loadout")]
    public int basePathLoadoutSlots = 2;
    public int maxPathLoadoutSlots = 6;

    [Header("XP Rewards")]
    public int firstBlockedCrisisXP = 20;
    public int blockedEventChosenXP = 10;
    public int oneWaveAfterBlockXP = 15;
    public int twoWavesAfterBlockXP = 25;
    public int miniBossAfterBlockXP = 35;
    public int bossAfterBlockXP = 75;
    public int chaosThreeBlockSurvivalXP = 50;
    public int chaosFiveBlockSurvivalXP = 100;
    public int firstEventTypeUsedXP = 50;

    [Header("Run Caps")]
    public int maxBlueprintsEarlyRun = 1;
    public int maxBlueprintsMidRun = 3;
    public int maxBlueprintsLateRun = 5;
    public int maxBlueprintsLongRun = 7;
    public int maxBlueprintsChaosFiveRun = 9;
    public int maxRiftBlueprintsChaosRun = 2;
    public int maxRiftBlueprintsChaosFiveRun = 4;
    public int maxRiftBlueprintsEliteRun = 6;

    [Header("Persistent Progress")]
    public int totalBlockedCrisesEver = 0;
    public int totalBlockedEventsChosenEver = 0;
    public int totalBlockedRecoveriesEver = 0;
    public int totalBossKillsAfterBlockEver = 0;
    public int totalMiniBossesAfterBlockEver = 0;
    public int totalChaosBlockedRecoveriesEver = 0;
    public int totalChaosFiveBlockedRecoveriesEver = 0;
    public int totalEliteAfterBlockEver = 0;
    public int highestWaveAfterBlockEver = 0;
    public int highestWaveEver = 0;

    [Header("Last Run")]
    public int lastRunPathTechniqueXPGained = 0;
    public int lastRunBlueprintsGained = 0;
    public int lastRunRiftBlueprintsGained = 0;
    public int lastRunBlockedCrises = 0;
    public int lastRunBlockedEventsChosen = 0;
    public int lastRunWavesAfterBlock = 0;
    public int lastRunBossKillsAfterBlock = 0;
    public int lastRunMiniBossesAfterBlock = 0;

    [Header("Definitions / State")]
    public List<PathTechniqueNodeDefinition> definitions = new List<PathTechniqueNodeDefinition>();
    public List<PathTechniqueNodeState> nodeStates = new List<PathTechniqueNodeState>();

    private readonly Dictionary<string, PathTechniqueNodeDefinition> definitionById = new Dictionary<string, PathTechniqueNodeDefinition>();
    private readonly Dictionary<string, PathTechniqueNodeState> stateById = new Dictionary<string, PathTechniqueNodeState>();
    private readonly HashSet<string> usedEventTypes = new HashSet<string>();
    private readonly HashSet<string> rewardedEventTypes = new HashSet<string>();
    private readonly HashSet<string> blockedPositionsThisRun = new HashSet<string>();

    private int pendingPathTechniqueXP = 0;
    private int pendingBlueprints = 0;
    private int pendingRiftBlueprints = 0;
    private int pendingBlueprintProgressPercent = 0;
    private int highestWaveThisRun = 0;
    private int highestChaosThisRun = 0;
    private bool eliteAfterBlockThisRun = false;
    private bool activeCrisis = false;
    private int activeCrisisIndex = 0;
    private int activeCrisisWave = 0;
    private int activeCrisisChaosLevel = 0;
    private bool activeCrisisHadChaosWaveBlock = false;
    private bool activeCrisisRepeatedPosition = false;
    private bool activeCrisisTwoWaveRewarded = false;
    private bool activeCrisisMiniBossRewarded = false;
    private bool activeCrisisBossRewarded = false;
    private bool activeCrisisChaosRewarded = false;
    private bool activeCrisisChaosFiveRewarded = false;
    private bool activeCrisisHadLeaksInFirstTwoWaves = false;
    private bool activeCrisisEventTypeBlueprintPending = false;
    private string activeCrisisEventType = "";
    private int activeCrisisWavesSurvived = 0;
    private bool runFinalized = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureDefinitions();
        EnsureStates();
        LoadProfile();
    }

    private void OnEnable()
    {
        WaveEventBus.WaveCompleted += HandleWaveCompleted;
        WaveEventBus.GameOverTriggered += HandleGameOverTriggered;
    }

    private void OnDisable()
    {
        WaveEventBus.WaveCompleted -= HandleWaveCompleted;
        WaveEventBus.GameOverTriggered -= HandleGameOverTriggered;

        if (Instance == this)
            Instance = null;
    }

    public static PathTechniqueProgressionManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        PathTechniqueProgressionManager existing = FindObjectOfType<PathTechniqueProgressionManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("PathTechniqueProgressionSystem");
        PathTechniqueProgressionManager manager = systemObject.AddComponent<PathTechniqueProgressionManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public void StartNewRun()
    {
        LoadProfile();

        pendingPathTechniqueXP = 0;
        pendingBlueprints = 0;
        pendingRiftBlueprints = 0;
        pendingBlueprintProgressPercent = 0;
        highestWaveThisRun = 0;
        highestChaosThisRun = 0;
        eliteAfterBlockThisRun = false;
        activeCrisis = false;
        activeCrisisIndex = 0;
        activeCrisisWave = 0;
        activeCrisisChaosLevel = 0;
        activeCrisisHadChaosWaveBlock = false;
        activeCrisisRepeatedPosition = false;
        activeCrisisTwoWaveRewarded = false;
        activeCrisisMiniBossRewarded = false;
        activeCrisisBossRewarded = false;
        activeCrisisChaosRewarded = false;
        activeCrisisChaosFiveRewarded = false;
        activeCrisisHadLeaksInFirstTwoWaves = false;
        activeCrisisEventTypeBlueprintPending = false;
        activeCrisisEventType = "";
        activeCrisisWavesSurvived = 0;
        lastRunPathTechniqueXPGained = 0;
        lastRunBlueprintsGained = 0;
        lastRunRiftBlueprintsGained = 0;
        lastRunBlockedCrises = 0;
        lastRunBlockedEventsChosen = 0;
        lastRunWavesAfterBlock = 0;
        lastRunBossKillsAfterBlock = 0;
        lastRunMiniBossesAfterBlock = 0;
        blockedPositionsThisRun.Clear();
        runFinalized = false;
    }

    public void RecordBlockedCrisis(Vector2Int blockedPosition, int completedWaveNumber, int chaosLevel, bool hadChaosWaveBlock)
    {
        if (runFinalized)
            return;

        string positionKey = blockedPosition.x + "," + blockedPosition.y;
        bool repeatedPosition = blockedPositionsThisRun.Contains(positionKey);
        blockedPositionsThisRun.Add(positionKey);

        activeCrisis = true;
        activeCrisisIndex = Mathf.Max(1, lastRunBlockedCrises + 1);
        activeCrisisWave = Mathf.Max(0, completedWaveNumber);
        activeCrisisChaosLevel = Mathf.Max(0, chaosLevel);
        activeCrisisHadChaosWaveBlock = hadChaosWaveBlock;
        activeCrisisRepeatedPosition = repeatedPosition;
        activeCrisisTwoWaveRewarded = false;
        activeCrisisMiniBossRewarded = false;
        activeCrisisBossRewarded = false;
        activeCrisisChaosRewarded = false;
        activeCrisisChaosFiveRewarded = false;
        activeCrisisHadLeaksInFirstTwoWaves = false;
        activeCrisisEventTypeBlueprintPending = false;
        activeCrisisEventType = "";
        activeCrisisWavesSurvived = 0;

        lastRunBlockedCrises++;
        highestWaveThisRun = Mathf.Max(highestWaveThisRun, completedWaveNumber);
        highestChaosThisRun = Mathf.Max(highestChaosThisRun, chaosLevel);
        totalBlockedCrisesEver++;

        AddPathTechniqueXP(GetBlockedCrisisXP(activeCrisisIndex));
        SaveProfile();
    }

    public void RecordBlockedEventChoice(string eventName, string eventType)
    {
        if (runFinalized)
            return;

        lastRunBlockedEventsChosen++;
        totalBlockedEventsChosenEver++;
        AddPathTechniqueXP(blockedEventChosenXP);

        string safeType = string.IsNullOrEmpty(eventType) ? "Unknown" : eventType;
        if (!usedEventTypes.Contains(safeType))
        {
            usedEventTypes.Add(safeType);
            AddPathTechniqueXP(firstEventTypeUsedXP);
        }

        if (!rewardedEventTypes.Contains(safeType))
        {
            activeCrisisEventType = safeType;
            activeCrisisEventTypeBlueprintPending = true;
        }

        SaveProfile();
    }

    public void FinalizeRun()
    {
        if (runFinalized)
            return;

        runFinalized = true;
        lastRunPathTechniqueXPGained = Mathf.Max(0, pendingPathTechniqueXP);
        lastRunBlueprintsGained = Mathf.Max(0, pendingBlueprints);
        lastRunRiftBlueprintsGained = Mathf.Max(0, pendingRiftBlueprints);

        pathTechniqueXP += lastRunPathTechniqueXPGained;
        blueprints += lastRunBlueprintsGained;
        riftBlueprints += lastRunRiftBlueprintsGained;
        pathTechniqueLevel = CalculatePathTechniqueLevel(pathTechniqueXP);

        highestWaveEver = Mathf.Max(highestWaveEver, highestWaveThisRun);
        SaveProfile();
    }

    public List<PathTechniqueNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public PathTechniqueNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        PathTechniqueNodeDefinition definition;
        return definitionById.TryGetValue(nodeId, out definition) ? definition : null;
    }

    public PathTechniqueNodeState GetNodeState(string nodeId)
    {
        EnsureStates();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        PathTechniqueNodeState state;
        return stateById.TryGetValue(nodeId, out state) ? state : null;
    }

    public bool IsNodePurchased(string nodeId)
    {
        PathTechniqueNodeState state = GetNodeState(nodeId);
        return state != null && state.purchased;
    }

    public bool IsNodeActive(string nodeId)
    {
        PathTechniqueNodeDefinition definition = GetDefinition(nodeId);
        PathTechniqueNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased)
            return false;

        return definition.RequiresLoadoutSlot() ? state.active : true;
    }

    public bool CanPurchaseNode(string nodeId)
    {
        PathTechniqueNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return false;

        PathTechniqueNodeState state = GetNodeState(nodeId);
        if (state != null && state.purchased)
            return false;

        if (pathTechniqueLevel < definition.requiredLevel)
            return false;

        if (blueprints < definition.blueprintCost)
            return false;

        if (riftBlueprints < definition.riftBlueprintCost)
            return false;

        return AreSpecialRequirementsMet(definition);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        if (!CanPurchaseNode(nodeId))
            return false;

        PathTechniqueNodeDefinition definition = GetDefinition(nodeId);
        PathTechniqueNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null)
            return false;

        blueprints -= Mathf.Max(0, definition.blueprintCost);
        riftBlueprints -= Mathf.Max(0, definition.riftBlueprintCost);
        state.purchased = true;
        state.active = !definition.RequiresLoadoutSlot();
        SaveProfile();
        return true;
    }

    public bool CanActivateNode(string nodeId)
    {
        PathTechniqueNodeDefinition definition = GetDefinition(nodeId);
        PathTechniqueNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased || state.active || !definition.RequiresLoadoutSlot())
            return false;

        int usedSlotsAfterReplacingGroup = GetUsedLoadoutSlots() - GetActiveSlotsInExclusiveGroup(definition);
        return usedSlotsAfterReplacingGroup + definition.slotCost <= GetLoadoutSlotCapacity();
    }

    public bool TryActivateNode(string nodeId)
    {
        if (!CanActivateNode(nodeId))
            return false;

        PathTechniqueNodeDefinition definition = GetDefinition(nodeId);
        PathTechniqueNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null)
            return false;

        DeactivateExclusiveLoadoutGroup(definition);
        state.active = true;
        SaveProfile();
        return true;
    }

    public bool TryDeactivateNode(string nodeId)
    {
        PathTechniqueNodeDefinition definition = GetDefinition(nodeId);
        PathTechniqueNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !definition.RequiresLoadoutSlot() || !state.active)
            return false;

        state.active = false;
        SaveProfile();
        return true;
    }

    public bool TrySpendBlueprints(int blueprintAmount, int riftBlueprintAmount)
    {
        int safeBlueprints = Mathf.Max(0, blueprintAmount);
        int safeRiftBlueprints = Mathf.Max(0, riftBlueprintAmount);

        if (safeBlueprints <= 0 && safeRiftBlueprints <= 0)
            return true;

        if (blueprints < safeBlueprints || riftBlueprints < safeRiftBlueprints)
            return false;

        blueprints -= safeBlueprints;
        riftBlueprints -= safeRiftBlueprints;
        SaveProfile();
        return true;
    }

    public int GetLoadoutSlotCapacity()
    {
        int slots = Mathf.Max(0, basePathLoadoutSlots);

        if (pathTechniqueLevel >= 8)
            slots++;

        if (pathTechniqueLevel >= 15)
            slots++;

        if (totalChaosBlockedRecoveriesEver > 0)
            slots++;

        if (totalChaosFiveBlockedRecoveriesEver > 0 || totalEliteAfterBlockEver > 0)
            slots++;

        return Mathf.Clamp(slots, 0, Mathf.Max(basePathLoadoutSlots, maxPathLoadoutSlots));
    }

    public int GetUsedLoadoutSlots()
    {
        EnsureStates();
        int used = 0;

        foreach (PathTechniqueNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot())
                continue;

            PathTechniqueNodeState state = GetNodeState(definition.nodeId);
            if (state != null && state.purchased && state.active)
                used += Mathf.Max(0, definition.slotCost);
        }

        return used;
    }

    public int GetAvailableLoadoutSlots()
    {
        return Mathf.Max(0, GetLoadoutSlotCapacity() - GetUsedLoadoutSlots());
    }

    public int GetXPToNextPathTechniqueLevel()
    {
        return GetXPToNextPathTechniqueLevel(pathTechniqueLevel);
    }

    public int GetXPIntoCurrentLevel()
    {
        return GetXPIntoLevel(pathTechniqueXP);
    }

    public string GetTopBarSummary()
    {
        return "Pfadtechnik Lv " + pathTechniqueLevel +
               " | Bauplaene " + blueprints +
               " | Rissbauplaene " + riftBlueprints +
               " | Loadout " + GetUsedLoadoutSlots() + "/" + GetLoadoutSlotCapacity();
    }

    public string GetOverviewText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>Pfadtechnik</b>");
        builder.AppendLine("Pfadtechnik-Level: " + pathTechniqueLevel);
        builder.AppendLine("Pfadtechnik-XP: " + GetXPIntoCurrentLevel() + " / " + GetXPToNextPathTechniqueLevel());
        builder.AppendLine("Bauplaene: " + blueprints);
        builder.AppendLine("Rissbauplaene: " + riftBlueprints);
        builder.AppendLine("Pfadtechnik-Loadout: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
        builder.AppendLine();
        builder.AppendLine("Verbau-Krisen erlebt: " + totalBlockedCrisesEver);
        builder.AppendLine("Verbau-Krisen ueberlebt: " + totalBlockedRecoveriesEver);
        builder.AppendLine("Boss nach Verbau besiegt: " + totalBossKillsAfterBlockEver);
        builder.AppendLine("Chaos-Verbau ueberlebt: " + totalChaosBlockedRecoveriesEver);
        builder.AppendLine();
        builder.AppendLine("<b>Letzter Run</b>");
        builder.AppendLine("+ " + lastRunPathTechniqueXPGained + " Pfadtechnik-XP");
        builder.AppendLine("+ " + lastRunBlueprintsGained + " Bauplaene");
        builder.AppendLine("+ " + lastRunRiftBlueprintsGained + " Rissbauplaene");
        builder.AppendLine("Krisen: " + lastRunBlockedCrises + " | Events: " + lastRunBlockedEventsChosen + " | Waves nach Verbau: " + lastRunWavesAfterBlock);
        builder.AppendLine();
        builder.AppendLine("<b>Naechste Ziele</b>");
        AppendNextGoals(builder);
        builder.AppendLine();
        builder.AppendLine("Regel: Verbau selbst gibt keine Bauplaene. Bauplaene entstehen erst durch Ueberleben nach der Krise.");
        return builder.ToString();
    }

    public string GetNodeStateText(PathTechniqueNodeDefinition definition)
    {
        if (definition == null)
            return "Unbekannt";

        PathTechniqueNodeState state = GetNodeState(definition.nodeId);

        if (state != null && state.purchased)
        {
            if (definition.RequiresLoadoutSlot())
                return state.active ? "Aktiv | Slot " + definition.slotCost : "Gekauft | Slot " + definition.slotCost;

            return "Freigeschaltet";
        }

        if (CanPurchaseNode(definition.nodeId))
            return "Kaufbar | " + FormatCost(definition);

        if (pathTechniqueLevel < definition.requiredLevel)
            return "Gesperrt | Level " + definition.requiredLevel;

        if (!AreSpecialRequirementsMet(definition))
            return "Gesperrt | Bedingung";

        if (blueprints < definition.blueprintCost || riftBlueprints < definition.riftBlueprintCost)
            return "Ressourcen fehlen | " + FormatCost(definition);

        return "Gesperrt";
    }

    public string GetNodeDetailText(string nodeId)
    {
        PathTechniqueNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return "Pfadtechnik-Knoten nicht gefunden.";

        PathTechniqueNodeState state = GetNodeState(nodeId);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>" + definition.displayName + "</b>");
        builder.AppendLine("<size=90%><color=#B9C2D0>" + GetNodeStateText(definition) + "</color></size>");
        builder.AppendLine();
        builder.AppendLine("Kategorie: " + GetCategoryDisplayName(definition.category));
        builder.AppendLine("Typ: " + GetKindDisplayName(definition.kind));
        builder.AppendLine("Kosten: " + FormatCost(definition));
        builder.AppendLine("Pfadtechnik-Level: " + definition.requiredLevel);

        if (definition.RequiresLoadoutSlot())
        {
            builder.AppendLine("Slot-Kosten: " + definition.slotCost);
            builder.AppendLine("Pfadtechnik-Loadout: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
        }

        if (!string.IsNullOrEmpty(definition.requirementText))
            builder.AppendLine("Bedingung: " + definition.requirementText);

        builder.AppendLine();
        builder.AppendLine("<b>Effekt</b>");
        builder.AppendLine(definition.effectText);
        builder.AppendLine();
        builder.AppendLine("<b>Fortschritt</b>");
        builder.AppendLine(GetRequirementProgressText(definition));

        if (state != null && state.purchased && definition.RequiresLoadoutSlot())
            builder.AppendLine(state.active ? "Aktiv fuer den naechsten Run." : "Gekauft, aber nicht im Pfadtechnik-Loadout aktiv.");

        return builder.ToString();
    }

    public int GetPurchasedCount(PathTechniqueCategory category)
    {
        int purchased = 0;

        foreach (PathTechniqueNodeDefinition definition in GetDefinitions())
        {
            if (definition != null && definition.category == category && IsNodePurchased(definition.nodeId))
                purchased++;
        }

        return purchased;
    }

    public int GetDefinitionCount(PathTechniqueCategory category)
    {
        int total = 0;

        foreach (PathTechniqueNodeDefinition definition in GetDefinitions())
        {
            if (definition != null && definition.category == category)
                total++;
        }

        return total;
    }

    public static string GetCategoryDisplayName(PathTechniqueCategory category)
    {
        switch (category)
        {
            case PathTechniqueCategory.Overview: return "Uebersicht";
            case PathTechniqueCategory.EventPool: return "Event-Pool";
            case PathTechniqueCategory.EventQuality: return "Event-Qualitaet";
            case PathTechniqueCategory.RescuePower: return "Rettungsstaerke";
            case PathTechniqueCategory.PathTools: return "Pfadwerkzeuge";
            case PathTechniqueCategory.TileTechnique: return "Tile-Technik";
            case PathTechniqueCategory.RiftArchitecture: return "Rissarchitektur";
            default: return category.ToString();
        }
    }

    public static string GetKindDisplayName(PathTechniqueNodeKind kind)
    {
        switch (kind)
        {
            case PathTechniqueNodeKind.Core: return "Basis / Uebersicht";
            case PathTechniqueNodeKind.EventPoolUnlock: return "Event-Pool-Unlock";
            case PathTechniqueNodeKind.EventQuality: return "Event-Qualitaet";
            case PathTechniqueNodeKind.RescuePower: return "Rettungsstaerke";
            case PathTechniqueNodeKind.PathTool: return "Pfadwerkzeug";
            case PathTechniqueNodeKind.TileTechnique: return "Tile-Technik";
            case PathTechniqueNodeKind.RiftArchitecture: return "Rissarchitektur";
            default: return kind.ToString();
        }
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || runFinalized)
            return;

        highestWaveThisRun = Mathf.Max(highestWaveThisRun, result.waveNumber);
        highestChaosThisRun = Mathf.Max(highestChaosThisRun, result.chaosLevelAtWaveStart);
        highestWaveEver = Mathf.Max(highestWaveEver, result.waveNumber);

        if (!activeCrisis || result.waveNumber <= activeCrisisWave)
            return;

        activeCrisisWavesSurvived++;
        lastRunWavesAfterBlock++;
        highestWaveAfterBlockEver = Mathf.Max(highestWaveAfterBlockEver, result.waveNumber);

        if (activeCrisisWavesSurvived <= 2 && (result.enemiesReachedBase > 0 || result.baseDamageTaken > 0))
            activeCrisisHadLeaksInFirstTwoWaves = true;

        if (activeCrisisWavesSurvived == 1)
            AddPathTechniqueXP(oneWaveAfterBlockXP);

        if (activeCrisisWavesSurvived >= 2 && !activeCrisisTwoWaveRewarded)
        {
            activeCrisisTwoWaveRewarded = true;
            totalBlockedRecoveriesEver++;
            AddPathTechniqueXP(twoWavesAfterBlockXP);
            AwardBlueprints(1);

            if (!activeCrisisHadLeaksInFirstTwoWaves)
                AwardBlueprints(1);

            if (activeCrisisWave >= 20 || result.waveNumber >= 20)
                AwardBlueprints(1);

            if (activeCrisisChaosLevel >= 3)
            {
                AddPathTechniqueXP(chaosThreeBlockSurvivalXP);
                totalChaosBlockedRecoveriesEver++;
                AwardBlueprints(1);
                AwardRiftBlueprints(1);
                activeCrisisChaosRewarded = true;
            }

            if (activeCrisisChaosLevel >= 5)
            {
                AddPathTechniqueXP(chaosFiveBlockSurvivalXP);
                totalChaosFiveBlockedRecoveriesEver++;
                AwardRiftBlueprints(1);
                activeCrisisChaosFiveRewarded = true;
            }

            if (activeCrisisHadChaosWaveBlock)
                AwardRiftBlueprints(1);

            if (activeCrisisEventTypeBlueprintPending && !string.IsNullOrEmpty(activeCrisisEventType) && !rewardedEventTypes.Contains(activeCrisisEventType))
            {
                rewardedEventTypes.Add(activeCrisisEventType);
                AwardBlueprints(1);
            }
        }

        if ((result.isMiniBossWave || result.miniBossDefeated) && !activeCrisisMiniBossRewarded)
        {
            activeCrisisMiniBossRewarded = true;
            lastRunMiniBossesAfterBlock++;
            totalMiniBossesAfterBlockEver++;
            AddPathTechniqueXP(miniBossAfterBlockXP);
            AwardBlueprints(1);
        }

        if (result.bossDefeated && !activeCrisisBossRewarded)
        {
            activeCrisisBossRewarded = true;
            lastRunBossKillsAfterBlock++;
            totalBossKillsAfterBlockEver++;
            AddPathTechniqueXP(bossAfterBlockXP);
            AwardBlueprints(2);

            if (activeCrisisIndex == lastRunBlockedCrises)
                AwardBlueprints(1);

            if (result.chaosLevelAtWaveStart >= 5 || activeCrisisChaosLevel >= 5)
                AwardRiftBlueprints(2);
        }

        if (result.eliteDefeated)
        {
            eliteAfterBlockThisRun = true;
            totalEliteAfterBlockEver++;
            AwardRiftBlueprints(1);
        }
    }

    private void HandleGameOverTriggered()
    {
        FinalizeRun();
    }

    private void AddPathTechniqueXP(int amount)
    {
        pendingPathTechniqueXP += Mathf.Max(0, amount);
    }

    private int GetBlockedCrisisXP(int crisisIndex)
    {
        if (crisisIndex <= 1)
            return Mathf.Max(0, firstBlockedCrisisXP);

        if (crisisIndex == 2)
            return Mathf.Max(0, Mathf.RoundToInt(firstBlockedCrisisXP * 0.5f));

        if (crisisIndex == 3)
            return Mathf.Max(0, Mathf.RoundToInt(firstBlockedCrisisXP * 0.25f));

        return Mathf.Max(0, Mathf.RoundToInt(firstBlockedCrisisXP * 0.1f));
    }

    private void AwardBlueprints(int amount)
    {
        if (amount <= 0 || activeCrisisRepeatedPosition)
            return;

        int percent = GetActiveCrisisBlueprintPercent();
        if (percent <= 0)
            return;

        pendingBlueprintProgressPercent += amount * percent;
        int earned = pendingBlueprintProgressPercent / 100;
        pendingBlueprintProgressPercent %= 100;

        if (earned <= 0)
            return;

        int cap = GetBlueprintCapForRun();
        int grant = Mathf.Min(earned, Mathf.Max(0, cap - pendingBlueprints));
        pendingBlueprints += Mathf.Max(0, grant);
    }

    private void AwardRiftBlueprints(int amount)
    {
        if (amount <= 0 || activeCrisisRepeatedPosition)
            return;

        int cap = GetRiftBlueprintCapForRun();
        int grant = Mathf.Min(amount, Mathf.Max(0, cap - pendingRiftBlueprints));
        pendingRiftBlueprints += Mathf.Max(0, grant);
    }

    private int GetActiveCrisisBlueprintPercent()
    {
        if (activeCrisisIndex <= 1)
            return 100;

        if (activeCrisisIndex == 2)
            return 50;

        if (activeCrisisIndex == 3)
            return 25;

        return 0;
    }

    private int GetBlueprintCapForRun()
    {
        if (highestChaosThisRun >= 5 || activeCrisisChaosLevel >= 5 || eliteAfterBlockThisRun)
            return Mathf.Max(0, maxBlueprintsChaosFiveRun);

        int wave = Mathf.Max(highestWaveThisRun, activeCrisisWave);
        if (wave >= 30)
            return Mathf.Max(0, maxBlueprintsLongRun);

        if (wave >= 20)
            return Mathf.Max(0, maxBlueprintsLateRun);

        if (wave >= 10)
            return Mathf.Max(0, maxBlueprintsMidRun);

        return Mathf.Max(0, maxBlueprintsEarlyRun);
    }

    private int GetRiftBlueprintCapForRun()
    {
        if (eliteAfterBlockThisRun)
            return Mathf.Max(0, maxRiftBlueprintsEliteRun);

        if (highestChaosThisRun >= 5 || activeCrisisChaosLevel >= 5)
            return Mathf.Max(0, maxRiftBlueprintsChaosFiveRun);

        if (highestChaosThisRun >= 3 || activeCrisisChaosLevel >= 3)
            return Mathf.Max(0, maxRiftBlueprintsChaosRun);

        return 0;
    }

    private bool AreSpecialRequirementsMet(PathTechniqueNodeDefinition definition)
    {
        if (definition == null)
            return false;

        string id = definition.nodeId;

        switch (id)
        {
            case "path.core.unlock":
            case "path.core.result_summary":
                return totalBlockedCrisesEver > 0 || lastRunBlockedCrises > 0;
            case "path.core.lexicon":
                return IsNodePurchased("path.core.unlock") || totalBlockedCrisesEver > 0;
            case "path.core.recovery_goal":
                return totalBlockedCrisesEver > 0 || lastRunBlockedCrises > 0;
            case "path.event.path_scan":
            case "path.quality.no_duplicate":
                return totalBlockedCrisesEver + lastRunBlockedCrises >= 2;
            case "path.event.emergency_xp":
            case "path.quality.reroll_1":
            case "path.tool.path_scan_2":
                return totalBlockedCrisesEver + lastRunBlockedCrises >= 3;
            case "path.event.evolution_point":
                return totalBossKillsAfterBlockEver > 0 || totalMiniBossesAfterBlockEver > 0;
            case "path.event.base_relocate":
            case "path.quality.prefer_1":
            case "path.tool.dead_end_memory":
                return totalBlockedRecoveriesEver >= 5;
            case "path.event.teleporter":
            case "path.quality.reroll_2":
            case "path.rescue.training_2":
            case "path.rescue.evolution_2":
                return totalBossKillsAfterBlockEver > 0;
            case "path.event.chaos_order":
            case "path.rift.unlock":
            case "path.rift.chaos_order_1":
            case "path.rescue.training_3":
            case "path.rescue.evolution_control":
                return totalChaosBlockedRecoveriesEver > 0;
            case "path.event.rift_anchor":
            case "path.rift.rift_anchor_1":
                return totalChaosFiveBlockedRecoveriesEver > 0;
            case "path.event.choice_cache":
            case "path.quality.emergency_memory":
                return totalBlockedCrisesEver + lastRunBlockedCrises >= 10;
            case "path.quality.ban_1":
                return usedEventTypes.Count >= 5;
            case "path.rescue.training_1":
                return IsNodePurchased("path.event.raise_low_towers");
            case "path.rescue.evolution_1":
                return IsNodePurchased("path.event.evolution_point");
            case "path.tool.path_scan_1":
                return IsNodePurchased("path.event.path_scan");
            case "path.tool.path_reroll_2":
                return highestWaveEver >= 20 || highestWaveThisRun >= 20;
            case "path.tool.base_relocate_1":
                return IsNodePurchased("path.event.base_relocate");
            case "path.tool.base_relocate_2":
                return totalBossKillsAfterBlockEver > 0 && IsNodePurchased("path.tool.base_relocate_1");
            case "path.tool.teleporter_1":
                return IsNodePurchased("path.event.teleporter");
            case "path.tool.teleporter_2":
            case "path.rift.teleporter_stability":
                return totalChaosBlockedRecoveriesEver > 0 && IsNodePurchased("path.tool.teleporter_1");
            case "path.tile.trap_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.TrapTile);
            case "path.tile.trap_2":
                return IsNodePurchased("path.tile.trap_1");
            case "path.tile.gold_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.GoldTile);
            case "path.tile.gold_2":
                return IsNodePurchased("path.tile.gold_1");
            case "path.tile.slow_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.SlowTile);
            case "path.tile.range_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.RangeTile);
            case "path.tile.xp_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.XPTile);
            case "path.tile.damage_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.DamageTile);
            case "path.tile.rate_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.RateTile);
            case "path.tile.upgrade_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.UpgradeTile);
            case "path.tile.combo_1":
                return IsGeneralTileUnlocked(PathBuildOptionType.ComboTile);
            case "path.tile.combo_2":
                return totalChaosFiveBlockedRecoveriesEver > 0 && IsNodePurchased("path.tile.combo_1");
            case "path.rift.chaos_order_2":
                return totalChaosFiveBlockedRecoveriesEver > 0 || highestChaosThisRun >= 5;
            case "path.rift.tile_echo":
                return totalChaosBlockedRecoveriesEver > 0;
            case "path.rift.build_against_chaos":
                return totalChaosFiveBlockedRecoveriesEver > 0 && totalBossKillsAfterBlockEver > 0;
            case "path.rift.elite_bridge":
                return totalEliteAfterBlockEver > 0 || highestWaveEver >= 30;
            case "path.rift.cosmetic":
                return IsNodePurchased("path.rift.unlock");
            default:
                return true;
        }
    }

    private string GetRequirementProgressText(PathTechniqueNodeDefinition definition)
    {
        if (definition == null)
            return "Keine Daten.";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Pfadtechnik-Level: " + Mathf.Min(pathTechniqueLevel, definition.requiredLevel) + " / " + definition.requiredLevel);
        builder.AppendLine("Bauplaene: " + Mathf.Min(blueprints, definition.blueprintCost) + " / " + definition.blueprintCost);

        if (definition.riftBlueprintCost > 0)
            builder.AppendLine("Rissbauplaene: " + Mathf.Min(riftBlueprints, definition.riftBlueprintCost) + " / " + definition.riftBlueprintCost);

        builder.AppendLine("Verbau-Krisen: " + totalBlockedCrisesEver);
        builder.AppendLine("Verbau-Rettungen: " + totalBlockedRecoveriesEver);
        builder.AppendLine("Boss nach Verbau: " + totalBossKillsAfterBlockEver);
        builder.AppendLine("Chaos-Verbau: " + totalChaosBlockedRecoveriesEver);
        builder.AppendLine("Chaos-5-Verbau: " + totalChaosFiveBlockedRecoveriesEver);
        builder.AppendLine("Verschiedene Eventtypen: " + usedEventTypes.Count);
        builder.AppendLine("Spezialbedingung: " + FormatYesNo(AreSpecialRequirementsMet(definition)));
        return builder.ToString();
    }

    private void AppendNextGoals(StringBuilder builder)
    {
        if (totalBlockedCrisesEver <= 0)
            builder.AppendLine("- Erlebe die erste echte Verbau-Krise.");

        if (totalBlockedRecoveriesEver <= 0)
            builder.AppendLine("- Ueberlebe nach Verbau 2 Waves.");

        if (totalBossKillsAfterBlockEver <= 0)
            builder.AppendLine("- Besiege einen Boss nach Verbau.");

        if (pathTechniqueLevel < 8)
            builder.AppendLine("- Erreiche Pfadtechnik-Level 8 fuer Wegformer-Knoten.");

        if (totalChaosBlockedRecoveriesEver <= 0)
            builder.AppendLine("- Ueberlebe einen Verbau bei Chaos 3+.");

        if (totalChaosFiveBlockedRecoveriesEver <= 0 && totalChaosBlockedRecoveriesEver > 0)
            builder.AppendLine("- Ueberlebe einen Verbau bei Chaos 5.");
    }

    private bool IsGeneralTileUnlocked(PathBuildOptionType optionType)
    {
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        return generalMeta == null || generalMeta.IsTileUnlocked(optionType);
    }

    private GeneralMetaProgressionManager GetGeneralMetaProgressionManager()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetGeneralMetaProgressionManager() : GeneralMetaProgressionManager.GetOrCreate();
    }

    private int CalculatePathTechniqueLevel(int totalXP)
    {
        int level = 1;
        int remaining = Mathf.Max(0, totalXP);

        while (level < 999)
        {
            int needed = GetXPToNextPathTechniqueLevel(level);
            if (remaining < needed)
                break;

            remaining -= needed;
            level++;
        }

        return level;
    }

    private int GetXPIntoLevel(int totalXP)
    {
        int level = 1;
        int remaining = Mathf.Max(0, totalXP);

        while (level < pathTechniqueLevel)
        {
            remaining -= GetXPToNextPathTechniqueLevel(level);
            level++;
        }

        return Mathf.Max(0, remaining);
    }

    private int GetXPToNextPathTechniqueLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        return 100 + safeLevel * 35 + Mathf.FloorToInt(safeLevel * safeLevel * 1.5f);
    }

    private void EnsureDefinitions()
    {
        if (definitions == null)
            definitions = new List<PathTechniqueNodeDefinition>();

        if (definitions.Count == 0)
            CreateDefaultDefinitions();

        RemoveBridgeTileDefinitions();
        RebuildDefinitionLookup();
    }

    private void RemoveBridgeTileDefinitions()
    {
        RemoveDefinition("path.event.bridge_path");
        RemoveDefinition("path.tool.bridge_emergency");
        RemoveDefinition("path.tile.bridge_1");
        RemoveDefinition("path.tile.bridge_2");
    }

    private void RemoveDefinition(string nodeId)
    {
        if (definitions != null)
            definitions.RemoveAll(definition => definition != null && definition.nodeId == nodeId);

        if (nodeStates != null)
            nodeStates.RemoveAll(state => state != null && state.nodeId == nodeId);

        PlayerPrefs.DeleteKey(PlayerPrefsPrefix + nodeId + ".Purchased");
        PlayerPrefs.DeleteKey(PlayerPrefsPrefix + nodeId + ".Active");
    }

    private void EnsureStates()
    {
        EnsureDefinitions();

        if (nodeStates == null)
            nodeStates = new List<PathTechniqueNodeState>();

        RebuildStateLookup();

        foreach (PathTechniqueNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            if (stateById.ContainsKey(definition.nodeId))
                continue;

            PathTechniqueNodeState state = new PathTechniqueNodeState
            {
                nodeId = definition.nodeId,
                purchased = definition.unlockedByDefault,
                active = definition.unlockedByDefault && !definition.RequiresLoadoutSlot()
            };

            nodeStates.Add(state);
            stateById[definition.nodeId] = state;
        }
    }

    private void RebuildDefinitionLookup()
    {
        definitionById.Clear();

        foreach (PathTechniqueNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            definitionById[definition.nodeId] = definition;
        }
    }

    private void RebuildStateLookup()
    {
        stateById.Clear();

        foreach (PathTechniqueNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            stateById[state.nodeId] = state;
        }
    }

    private void CreateDefaultDefinitions()
    {
        definitions.Clear();

        AddDefinition("path.core.unlock", "Pfadtechnik", PathTechniqueCategory.Overview, PathTechniqueNodeKind.Core, 0, 0, 1, "Der Pfadtechnik-Tab wird sichtbar und erklaert Krisen, Bauplaene und Rissbauplaene.", false, "Erster Verbau erlebt.");
        AddDefinition("path.core.result_summary", "Krisenprotokoll", PathTechniqueCategory.Overview, PathTechniqueNodeKind.Core, 2, 0, 1, "Result zeigt Verbau-Daten genauer.", false, "Erster Verbau erlebt.");
        AddDefinition("path.core.lexicon", "Pfadtechnik-Archiv", PathTechniqueCategory.Overview, PathTechniqueNodeKind.Core, 2, 0, 1, "Lexikon-Kategorie Pfadtechnik wird vorbereitet.", false, "Pfadtechnik sichtbar.");
        AddDefinition("path.core.recovery_goal", "Rettungsziel", PathTechniqueCategory.Overview, PathTechniqueNodeKind.Core, 3, 0, 1, "Zeigt das Ziel 'nach Verbau 2 Waves ueberleben' im Meta-Hub.", false, "1 Verbau-Krise.");

        AddDefinition("path.event.core", "Grund-Events", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 0, 0, 1, "Goldreserve, Notfall-Reparatur und sichere Weiterfuehrung gehoeren zur Basis.", true);
        AddDefinition("path.event.build_time", "Baupause", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 3, 0, 2, "Baupause-Event wird fuer den Event-Pool vorbereitet.");
        AddDefinition("path.event.path_scan", "Pfadscan", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 4, 0, 1, "Pfadscan-Event zeigt sichere Erweiterungsrichtungen / bessere Vorschau.", false, "2x verbaut gewesen.");
        AddDefinition("path.event.emergency_xp", "Notfall-Ausbildung", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 5, 0, 1, "Tower-XP-Event wird vorbereitet.", false, "3 Verbau-Krisen.");
        AddDefinition("path.event.evolution_point", "Evolutionsfunke", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 8, 0, 1, "Evolutions-Event fuer einen Run-internen Tower-Bonus.", false, "Boss nach Verbau erreicht.");
        AddDefinition("path.event.raise_low_towers", "Nachschulung", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 10, 0, 8, "Low-Level-Tower-Event wird vorbereitet.");
        AddDefinition("path.event.base_relocate", "Basisversatz", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 12, 0, 1, "Base-Relocation-Event wird vorbereitet.", false, "5 Krisen ueberlebt.");
        AddDefinition("path.event.teleporter", "Teleporterbasis", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 18, 0, 1, "Teleporter-Event wird vorbereitet.", false, "Boss nach Verbau.");
        AddDefinition("path.event.chaos_order", "Chaos ordnen", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 20, 1, 1, "Chaos-Ordnen-Event wird vorbereitet.", false, "Chaos-Verbau.");
        AddDefinition("path.event.rift_anchor", "Rissanker", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 0, 3, 1, "Starke Endgame-Rettung mit Risiko.", false, "Chaos 5 nach Verbau.");
        AddDefinition("path.event.debt_build", "Schuldenbau", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 12, 0, 10, "Sofortressourcen gegen naechste-Wave-Risiko.");
        AddDefinition("path.event.choice_cache", "Krisenlager", PathTechniqueCategory.EventPool, PathTechniqueNodeKind.EventPoolUnlock, 15, 0, 1, "Kleine Auswahl aus Gold, Leben oder Bauzeit.", false, "10 Verbau-Krisen.");

        AddDefinition("path.quality.preview_1", "Krisen-Vorschau I", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 2, 0, 1, "Event-Texte zeigen klare Folgen.", false, "1 Verbau erlebt.");
        AddDefinition("path.quality.no_duplicate", "Keine Doppelkrise", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 4, 0, 1, "Gleiche Event-Art erscheint seltener direkt erneut.", false, "2 Verbau-Krisen.");
        AddDefinition("path.quality.weight_utility", "Utility-Gewichtung", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 5, 0, 4, "Utility-Events werden leicht haeufiger vorbereitet.");
        AddDefinition("path.quality.reroll_1", "Verbau-Reroll I", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 8, 0, 1, "1 Reroll pro Run.", 1, false, "3 Verbau-Krisen ueberlebt.");
        AddDefinition("path.quality.reroll_2", "Verbau-Reroll II", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 18, 0, 1, "2. Reroll pro Run.", 2, false, "Boss nach Verbau besiegt.");
        AddDefinition("path.quality.option_plus_1", "Dritte Hilfe", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 15, 0, 10, "Eine zusaetzliche Event-Option.", 2);
        AddDefinition("path.quality.ban_1", "Event-Bann I", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 12, 0, 1, "Eine Event-Kategorie wird seltener.", 1, false, "5 verschiedene Events genutzt.");
        AddDefinition("path.quality.prefer_1", "Event-Praeferenz I", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 14, 0, 1, "Eine Event-Kategorie wird haeufiger.", 1, false, "5 Verbau-Rettungen.");
        AddDefinition("path.quality.rare_teaser", "Seltene Zeichen", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 10, 0, 8, "Seltene Events werden als Teaser sichtbar.");
        AddDefinition("path.quality.emergency_memory", "Krisengedaechtnis", PathTechniqueCategory.EventQuality, PathTechniqueNodeKind.EventQuality, 20, 0, 1, "Eventangebot vermeidet schlechte Wiederholungen.", false, "10 Verbau-Krisen.");

        AddDefinition("path.rescue.gold_1", "Goldreserve I", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 3, 0, 1, "Goldreserve +25 Gold.");
        AddDefinition("path.rescue.gold_2", "Goldreserve II", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 6, 0, 1, "Goldreserve weitere +25 Gold.");
        AddDefinition("path.rescue.gold_3", "Goldreserve III", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 10, 0, 1, "Goldreserve weitere +25 Gold.", 1);
        AddDefinition("path.rescue.gold_4", "Goldreserve IV", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 16, 0, 1, "Goldreserve weitere +25 Gold.", 1);
        AddDefinition("path.rescue.life_1", "Notfall-Reparatur I", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 4, 0, 1, "+1 zusaetzliches Leben.");
        AddDefinition("path.rescue.life_2", "Notfall-Reparatur II", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 8, 0, 1, "+1 weiteres Leben.", 1);
        AddDefinition("path.rescue.life_3", "Notfall-Reparatur III", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 14, 0, 1, "+1 weiteres Leben.", 1);
        AddDefinition("path.rescue.time_1", "Bauzeit I", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 3, 0, 1, "Timed Buildphase +10s.");
        AddDefinition("path.rescue.time_2", "Bauzeit II", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 7, 0, 1, "Timed Buildphase weitere +10s.");
        AddDefinition("path.rescue.time_3", "Bauzeit III", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 12, 0, 1, "Timed Buildphase weitere +10s.", 1);
        AddDefinition("path.rescue.training_1", "Nachschulung I", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 10, 0, 1, "Tower unter Level 3 erhalten XP bis Level 3.", 1, false, "Event freigeschaltet.");
        AddDefinition("path.rescue.training_2", "Nachschulung II", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 18, 0, 1, "Tower unter Level 4 erhalten XP bis Level 4.", 1, false, "Boss nach Verbau besiegt.");
        AddDefinition("path.rescue.training_3", "Nachschulung III", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 30, 1, 1, "Tower unter Level 5 erhalten XP bis Level 5.", 2, false, "Chaos-Verbau ueberlebt.");
        AddDefinition("path.rescue.evolution_1", "Evolutionsfunke I", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 12, 0, 1, "Kleiner Run-Evolutionsbonus.", 1, false, "Event freigeschaltet.");
        AddDefinition("path.rescue.evolution_2", "Evolutionsfunke II", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 24, 0, 1, "Staerkerer Run-Evolutionsbonus.", 2, false, "Boss nach Verbau besiegt.");
        AddDefinition("path.rescue.evolution_control", "Evolutionswahl", PathTechniqueCategory.RescuePower, PathTechniqueNodeKind.RescuePower, 0, 2, 1, "Spieler kann Ziel-Tower bewusster auswaehlen.", 2, false, "Chaos-Verbau ueberlebt.");

        AddDefinition("path.tool.reserved_preview", "Letzte-Erweiterung-Warnung", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 2, 0, 1, "Gefaehrliche letzte Erweiterung wird klarer.", false, "1 Verbau erlebt.");
        AddDefinition("path.tool.direction_preview", "Richtungs-Vorschau", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 4, 0, 3, "Forward/Left/Right werden deutlicher.");
        AddDefinition("path.tool.path_scan_1", "Pfadscan I", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 5, 0, 1, "Eine sichere Verlaengerungsempfehlung.", false, "Event freigeschaltet.");
        AddDefinition("path.tool.path_scan_2", "Pfadscan II", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 10, 0, 1, "Risiko-Hinweis zu allen Richtungen.", false, "3 Verbau-Krisen.");
        AddDefinition("path.tool.path_reroll_1", "Pfadwahl-Reroll I", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 12, 0, 8, "Einmal pro Run Spezial-Tile-Angebot rerollen.", 1);
        AddDefinition("path.tool.path_reroll_2", "Pfadwahl-Reroll II", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 22, 0, 1, "Zweiter Spezial-Tile-Reroll.", 2, false, "Wave 20 erreicht.");
        AddDefinition("path.tool.base_relocate_1", "Basisversatz I", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 14, 0, 1, "Basisversatz-Event wird zuverlaessiger.", 1, false, "Event freigeschaltet.");
        AddDefinition("path.tool.base_relocate_2", "Basisversatz II", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 28, 0, 1, "Mehr gueltige Zielpositionen.", 2, false, "Boss nach Basisversatz geschafft.");
        AddDefinition("path.tool.teleporter_1", "Teleporter I", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 20, 0, 1, "Teleporter sucht bessere Positionen.", 2, false, "Teleporter-Event freigeschaltet.");
        AddDefinition("path.tool.teleporter_2", "Teleporter II", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 0, 2, 1, "Teleporter gibt bessere Chaos-Vorschau.", 2, false, "Chaos-Verbau ueberlebt.");
        AddDefinition("path.tool.dead_end_memory", "Sackgassen-Gedaechtnis", PathTechniqueCategory.PathTools, PathTechniqueNodeKind.PathTool, 16, 0, 1, "Gefaehrliche Pfadentscheidungen werden im HUD markiert.", false, "5 Verbau-Krisen.");

        AddDefinition("path.tile.trap_1", "Trap-Technik I", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 4, 0, 1, "Trap Tile wird staerker/lesbarer.", false, "Trap Tile freigeschaltet.");
        AddDefinition("path.tile.trap_2", "Trap-Technik II", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 10, 0, 1, "Trap Tile wird haeufiger vorbereitet.", false, "Trap-Technik I.");
        AddDefinition("path.tile.gold_1", "Goldader I", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 5, 0, 1, "Gold Tile leicht besser.", 1, false, "Gold Tile freigeschaltet.");
        AddDefinition("path.tile.gold_2", "Goldader II", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 14, 0, 1, "Gold Tile stabiler / besserer Tooltip.", 1, false, "Goldader I.");
        AddDefinition("path.tile.slow_1", "Bremsfeldtechnik", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 10, 0, 1, "Slow Tile etwas zuverlaessiger.", 1, false, "Slow Tile freigeschaltet.");
        AddDefinition("path.tile.range_1", "Reichweitenanker", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 12, 0, 1, "Range Tile Tooltip und Radius klarer.", false, "Range Tile freigeschaltet.");
        AddDefinition("path.tile.xp_1", "Trainingsfeld", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 14, 0, 1, "XP Tile gibt minimal besseren XP-Bonus.", 1, false, "XP Tile freigeschaltet.");
        AddDefinition("path.tile.damage_1", "Schadensanker", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 18, 0, 1, "Damage Tile leicht besser, aber selten.", 2, false, "Damage Tile freigeschaltet.");
        AddDefinition("path.tile.rate_1", "Taktanker", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 18, 0, 1, "Rate Tile leicht besser, aber selten.", 2, false, "Rate Tile freigeschaltet.");
        AddDefinition("path.tile.upgrade_1", "Upgrade-Feldtechnik", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 22, 0, 1, "Upgrade Tile wirkt stabiler/verstaendlicher.", 2, false, "Upgrade Tile freigeschaltet.");
        AddDefinition("path.tile.combo_1", "Kombo-Feld I", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 30, 1, 1, "Combo Tile bereitet Status-Combos besser vor.", 2, false, "Combo Tile freigeschaltet.");
        AddDefinition("path.tile.combo_2", "Kombo-Feld II", PathTechniqueCategory.TileTechnique, PathTechniqueNodeKind.TileTechnique, 0, 3, 1, "Combo Tile bekommt Riss-Variante.", 3, false, "Chaos 5 + Combo genutzt.");

        AddDefinition("path.rift.unlock", "Rissarchitektur", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 0, 1, "Rissbauplaene werden sichtbar.", false, "Chaos-Verbau ueberlebt.");
        AddDefinition("path.rift.chaos_order_1", "Chaos ordnen I", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 1, 1, "Chaos-Ordnen-Event im Pool.", 1, false, "Chaos 3 nach Verbau ueberlebt.");
        AddDefinition("path.rift.chaos_order_2", "Chaos ordnen II", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 3, 1, "Risiko-Level kann leicht geglaettet werden.", 2, false, "Chaos 5 erreicht.");
        AddDefinition("path.rift.rift_anchor_1", "Rissanker I", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 3, 1, "Starke Chaos-Rettung mit Risiko.", 2, false, "Chaos 5 nach Verbau ueberlebt.");
        AddDefinition("path.rift.teleporter_stability", "Stabiler Riss-Teleporter", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 4, 1, "Teleporter bei Chaos besser vorhersehbar.", 2, false, "Teleporter bei Chaos genutzt.");
        AddDefinition("path.rift.tile_echo", "Tile-Echo", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 4, 1, "Naechstes Spezial-Tile-Angebot besser.", 1, false, "Chaos-Wave-Block nach Verbau ueberlebt.");
        AddDefinition("path.rift.build_against_chaos", "Gegen den Riss bauen", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 5, 1, "Verbau-Events geben kleines Chaos-Wissen.", 1, false, "Chaos-5-Boss nach Verbau.");
        AddDefinition("path.rift.elite_bridge", "Elite-Risspfad", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 6, 1, "Elite+Verbau-Ziele sichtbar.", false, "Elite-Jagd freigeschaltet.");
        AddDefinition("path.rift.cosmetic", "Rissgelaender", PathTechniqueCategory.RiftArchitecture, PathTechniqueNodeKind.RiftArchitecture, 0, 3, 1, "Kosmetische Pfad-/Verbau-Visuals.", false, "Rissarchitektur Level I.");
    }

    private void AddDefinition(string nodeId, string displayName, PathTechniqueCategory category, PathTechniqueNodeKind kind, int blueprintCost, int riftBlueprintCost, int requiredLevel, string effectText, bool unlockedByDefault = false, string requirementText = "")
    {
        AddDefinition(nodeId, displayName, category, kind, blueprintCost, riftBlueprintCost, requiredLevel, effectText, 0, unlockedByDefault, requirementText);
    }

    private void AddDefinition(string nodeId, string displayName, PathTechniqueCategory category, PathTechniqueNodeKind kind, int blueprintCost, int riftBlueprintCost, int requiredLevel, string effectText, int slotCost, bool unlockedByDefault = false, string requirementText = "")
    {
        definitions.Add(new PathTechniqueNodeDefinition
        {
            nodeId = nodeId,
            displayName = displayName,
            category = category,
            kind = kind,
            blueprintCost = Mathf.Max(0, blueprintCost),
            riftBlueprintCost = Mathf.Max(0, riftBlueprintCost),
            requiredLevel = Mathf.Max(1, requiredLevel),
            slotCost = Mathf.Max(0, slotCost),
            unlockedByDefault = unlockedByDefault,
            effectText = effectText,
            requirementText = requirementText
        });
    }

    private void DeactivateExclusiveLoadoutGroup(PathTechniqueNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);

        foreach (PathTechniqueNodeDefinition definition in definitions)
        {
            if (definition == null || definition.nodeId == activatedDefinition.nodeId || !definition.RequiresLoadoutSlot())
                continue;

            if (GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            PathTechniqueNodeState state = GetNodeState(definition.nodeId);
            if (state != null)
                state.active = false;
        }
    }

    private int GetActiveSlotsInExclusiveGroup(PathTechniqueNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return 0;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);
        int slots = 0;

        foreach (PathTechniqueNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            PathTechniqueNodeState state = GetNodeState(definition.nodeId);
            if (state != null && state.active)
                slots += Mathf.Max(0, definition.slotCost);
        }

        return slots;
    }

    private string GetExclusiveLoadoutGroup(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return "";

        int underscoreIndex = nodeId.LastIndexOf('_');
        if (underscoreIndex <= 0 || underscoreIndex >= nodeId.Length - 1)
            return nodeId;

        for (int i = underscoreIndex + 1; i < nodeId.Length; i++)
        {
            if (!char.IsDigit(nodeId[i]))
                return nodeId;
        }

        return nodeId.Substring(0, underscoreIndex);
    }

    private string FormatCost(PathTechniqueNodeDefinition definition)
    {
        if (definition == null)
            return "0 BP";

        string text = definition.blueprintCost + " BP";
        if (definition.riftBlueprintCost > 0)
            text += " + " + definition.riftBlueprintCost + " RBP";

        return text;
    }

    private string FormatYesNo(bool value)
    {
        return value ? "erfuellt" : "offen";
    }

    private WaveHistory GetWaveHistory()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetWaveHistory() : null;
    }

    private GameManager GetGameManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager;
    }

    private void LoadProfile()
    {
        EnsureStates();
        LoadEventTypeSets();

        pathTechniqueXP = PlayerPrefs.GetInt(PlayerPrefsPrefix + "PathTechniqueXP", pathTechniqueXP);
        pathTechniqueLevel = CalculatePathTechniqueLevel(pathTechniqueXP);
        blueprints = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Blueprints", blueprints);
        riftBlueprints = PlayerPrefs.GetInt(PlayerPrefsPrefix + "RiftBlueprints", riftBlueprints);
        totalBlockedCrisesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBlockedCrisesEver", totalBlockedCrisesEver);
        totalBlockedEventsChosenEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBlockedEventsChosenEver", totalBlockedEventsChosenEver);
        totalBlockedRecoveriesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBlockedRecoveriesEver", totalBlockedRecoveriesEver);
        totalBossKillsAfterBlockEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBossKillsAfterBlockEver", totalBossKillsAfterBlockEver);
        totalMiniBossesAfterBlockEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalMiniBossesAfterBlockEver", totalMiniBossesAfterBlockEver);
        totalChaosBlockedRecoveriesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosBlockedRecoveriesEver", totalChaosBlockedRecoveriesEver);
        totalChaosFiveBlockedRecoveriesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosFiveBlockedRecoveriesEver", totalChaosFiveBlockedRecoveriesEver);
        totalEliteAfterBlockEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteAfterBlockEver", totalEliteAfterBlockEver);
        highestWaveAfterBlockEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestWaveAfterBlockEver", highestWaveAfterBlockEver);
        highestWaveEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        lastRunPathTechniqueXPGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunPathTechniqueXP", lastRunPathTechniqueXPGained);
        lastRunBlueprintsGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunBlueprints", lastRunBlueprintsGained);
        lastRunRiftBlueprintsGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunRiftBlueprints", lastRunRiftBlueprintsGained);
        lastRunBlockedCrises = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunBlockedCrises", lastRunBlockedCrises);
        lastRunBlockedEventsChosen = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunBlockedEventsChosen", lastRunBlockedEventsChosen);
        lastRunWavesAfterBlock = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunWavesAfterBlock", lastRunWavesAfterBlock);
        lastRunBossKillsAfterBlock = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunBossKillsAfterBlock", lastRunBossKillsAfterBlock);
        lastRunMiniBossesAfterBlock = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunMiniBossesAfterBlock", lastRunMiniBossesAfterBlock);

        foreach (PathTechniqueNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            PathTechniqueNodeState state = GetNodeState(definition.nodeId);
            if (state == null)
                continue;

            int defaultPurchased = definition.unlockedByDefault ? 1 : 0;
            int defaultActive = definition.unlockedByDefault && !definition.RequiresLoadoutSlot() ? 1 : 0;
            state.purchased = PlayerPrefs.GetInt(PlayerPrefsPrefix + definition.nodeId + ".Purchased", defaultPurchased) == 1;
            state.active = PlayerPrefs.GetInt(PlayerPrefsPrefix + definition.nodeId + ".Active", defaultActive) == 1;

            if (!definition.RequiresLoadoutSlot())
                state.active = state.purchased;
        }
    }

    private void SaveProfile()
    {
        EnsureStates();

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "PathTechniqueXP", pathTechniqueXP);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "Blueprints", blueprints);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "RiftBlueprints", riftBlueprints);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBlockedCrisesEver", totalBlockedCrisesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBlockedEventsChosenEver", totalBlockedEventsChosenEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBlockedRecoveriesEver", totalBlockedRecoveriesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBossKillsAfterBlockEver", totalBossKillsAfterBlockEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalMiniBossesAfterBlockEver", totalMiniBossesAfterBlockEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosBlockedRecoveriesEver", totalChaosBlockedRecoveriesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosFiveBlockedRecoveriesEver", totalChaosFiveBlockedRecoveriesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteAfterBlockEver", totalEliteAfterBlockEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestWaveAfterBlockEver", highestWaveAfterBlockEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunPathTechniqueXP", lastRunPathTechniqueXPGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunBlueprints", lastRunBlueprintsGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunRiftBlueprints", lastRunRiftBlueprintsGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunBlockedCrises", lastRunBlockedCrises);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunBlockedEventsChosen", lastRunBlockedEventsChosen);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunWavesAfterBlock", lastRunWavesAfterBlock);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunBossKillsAfterBlock", lastRunBossKillsAfterBlock);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunMiniBossesAfterBlock", lastRunMiniBossesAfterBlock);

        foreach (PathTechniqueNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Purchased", state.purchased ? 1 : 0);
            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Active", state.active ? 1 : 0);
        }

        SaveEventTypeSets();
        PlayerPrefs.Save();
    }

    private void LoadEventTypeSets()
    {
        usedEventTypes.Clear();
        rewardedEventTypes.Clear();
        LoadStringSet(PlayerPrefsPrefix + "UsedEventTypes", usedEventTypes);
        LoadStringSet(PlayerPrefsPrefix + "RewardedEventTypes", rewardedEventTypes);
    }

    private void LoadStringSet(string key, HashSet<string> target)
    {
        if (target == null)
            return;

        string raw = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(raw))
            return;

        string[] parts = raw.Split('|');
        foreach (string part in parts)
        {
            if (!string.IsNullOrEmpty(part))
                target.Add(part);
        }
    }

    private void SaveEventTypeSets()
    {
        SaveStringSet(PlayerPrefsPrefix + "UsedEventTypes", usedEventTypes);
        SaveStringSet(PlayerPrefsPrefix + "RewardedEventTypes", rewardedEventTypes);
    }

    private void SaveStringSet(string key, HashSet<string> values)
    {
        if (values == null || values.Count == 0)
        {
            PlayerPrefs.SetString(key, "");
            return;
        }

        StringBuilder builder = new StringBuilder();
        foreach (string value in values)
        {
            if (string.IsNullOrEmpty(value))
                continue;

            if (builder.Length > 0)
                builder.Append("|");

            builder.Append(value);
        }

        PlayerPrefs.SetString(key, builder.ToString());
    }
}
