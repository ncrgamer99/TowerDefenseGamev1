using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum ChaosResearchCategory
{
    Overview,
    RiskPool,
    ChaosVariants,
    ChaosWaves,
    ChaosCounters,
    OfferControl,
    Chaos5Endgame,
    JusticeOrder
}

public enum ChaosResearchNodeKind
{
    Core,
    RiskPoolUnlock,
    VariantResearch,
    WaveResearch,
    Counter,
    OfferControl,
    Endgame,
    JusticeOrder
}

[System.Serializable]
public class ChaosResearchNodeDefinition
{
    public string nodeId;
    public string displayName;
    public ChaosResearchCategory category;
    public ChaosResearchNodeKind kind;
    public int chaosKnowledgeCost;
    public int riftCoreCost;
    public int slotCost;
    public bool unlockedByDefault;
    public string effectText;
    public string requirementText;

    public bool RequiresLoadoutSlot()
    {
        return slotCost > 0 && (kind == ChaosResearchNodeKind.Counter || kind == ChaosResearchNodeKind.OfferControl || kind == ChaosResearchNodeKind.Endgame || kind == ChaosResearchNodeKind.JusticeOrder);
    }
}

[System.Serializable]
public class ChaosResearchNodeState
{
    public string nodeId;
    public bool purchased;
    public bool active;
}

public class ChaosResearchProgressionManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_ChaosResearch_";

    public static ChaosResearchProgressionManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Currencies")]
    public int chaosKnowledge = 0;
    public int riftCores = 0;

    [Header("Chaos Loadout")]
    public int baseChaosLoadoutSlots = 1;
    public int maxChaosLoadoutSlots = 4;

    [Header("Run-End Rewards")]
    public int chaosChoiceKnowledgeReward = 10;
    public int chaosWaveCompletedKnowledgeReward = 8;
    public int chaosTwoPlusWaveKnowledgeReward = 4;
    public int chaosTwoPlusBossKnowledgeReward = 25;
    public int chaosVariantKillKnowledgeReward = 1;
    public int maxChaosVariantKillKnowledgePerRun = 100;
    public int chaosWaveBlockKnowledgeReward = 10;
    public int newHighestChaosLevelKnowledgeReward = 40;
    public int firstChaosVariantRoleKnowledgeReward = 30;
    public int firstChaosWaveBlockKnowledgeReward = 30;
    public int maxRiftCoresFromChaos5WavesPerRun = 3;
    public int maxRiftCoresFromChaos5BlockWavesPerRun = 2;
    public int chaos5BossRiftCoreReward = 3;
    public int perfectChaos5BossRiftCoreBonus = 1;

    [Header("Persistent Progress")]
    public int highestChaosLevelEver = 0;
    public int highestGoldJusticeEver = 0;
    public int highestXpJusticeEver = 0;
    public int totalChaosChoicesEver = 0;
    public int totalJusticeChoicesEver = 0;
    public int totalNoModifierChoicesEver = 0;
    public int totalChaosWavesCompletedEver = 0;
    public int totalChaosVariantKillsEver = 0;
    public int totalChaosWaveBlockWavesEver = 0;
    public int totalChaos5WavesCompletedEver = 0;
    public int totalChaos5BossKillsEver = 0;
    public int totalBossKillsAtChaosTwoPlusEver = 0;
    public int totalSafeBossWithoutChaosEver = 0;
    public int totalRiskModifiersSeenEver = 0;
    public int totalMiniBossKillsEver = 0;
    public int highestWaveEver = 0;

    [Header("Last Run")]
    public int lastRunChaosKnowledgeGained = 0;
    public int lastRunRiftCoresGained = 0;
    public int lastRunChaosChoices = 0;
    public int lastRunChaosWavesCompleted = 0;
    public int lastRunChaosVariantKills = 0;
    public int lastRunChaosWaveBlockWaves = 0;
    public int lastRunChaos5WavesCompleted = 0;
    public int lastRunChaos5BossKills = 0;

    [Header("Definitions / State")]
    public List<ChaosResearchNodeDefinition> definitions = new List<ChaosResearchNodeDefinition>();
    public List<ChaosResearchNodeState> nodeStates = new List<ChaosResearchNodeState>();

    private readonly Dictionary<string, ChaosResearchNodeDefinition> definitionById = new Dictionary<string, ChaosResearchNodeDefinition>();
    private readonly Dictionary<string, ChaosResearchNodeState> stateById = new Dictionary<string, ChaosResearchNodeState>();
    private readonly Dictionary<EnemyRole, int> chaosVariantKillsByRole = new Dictionary<EnemyRole, int>();
    private readonly Dictionary<ChaosWaveBlockType, int> chaosWaveBlockCompletionsByType = new Dictionary<ChaosWaveBlockType, int>();
    private readonly HashSet<string> firstChaosVariantRolesClaimed = new HashSet<string>();
    private readonly HashSet<string> firstChaosWaveBlocksClaimed = new HashSet<string>();
    private readonly HashSet<string> riskModifierKeysSeen = new HashSet<string>();
    private readonly HashSet<string> riskLevelTwoPlusKeysSeen = new HashSet<string>();

    private int pendingRunChaosKnowledge = 0;
    private int pendingRunRiftCores = 0;
    private int currentRunChaosVariantKillKnowledge = 0;
    private int currentRunChaos5WaveRiftCores = 0;
    private int currentRunChaos5BlockWaveRiftCores = 0;
    private int baselineChaosChoiceCount = 0;
    private int baselineJusticeChoiceCount = 0;
    private int baselineNoModifierChoiceCount = 0;
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

    public static ChaosResearchProgressionManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        ChaosResearchProgressionManager existing = FindObjectOfType<ChaosResearchProgressionManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("ChaosResearchProgressionSystem");
        ChaosResearchProgressionManager manager = systemObject.AddComponent<ChaosResearchProgressionManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public void StartNewRun()
    {
        pendingRunChaosKnowledge = 0;
        pendingRunRiftCores = 0;
        currentRunChaosVariantKillKnowledge = 0;
        currentRunChaos5WaveRiftCores = 0;
        currentRunChaos5BlockWaveRiftCores = 0;
        lastRunChaosKnowledgeGained = 0;
        lastRunRiftCoresGained = 0;
        lastRunChaosChoices = 0;
        lastRunChaosWavesCompleted = 0;
        lastRunChaosVariantKills = 0;
        lastRunChaosWaveBlockWaves = 0;
        lastRunChaos5WavesCompleted = 0;
        lastRunChaos5BossKills = 0;
        runFinalized = false;

        ChaosJusticeManager chaosJustice = GetChaosJusticeManager();
        baselineChaosChoiceCount = chaosJustice != null ? chaosJustice.GetChaosChoiceCount() : 0;
        baselineJusticeChoiceCount = chaosJustice != null ? chaosJustice.GetJusticeChoiceCount() : 0;
        baselineNoModifierChoiceCount = chaosJustice != null ? chaosJustice.GetNoModifierChoiceCount() : 0;

        LoadProfile();
    }

    public void FinalizeRun()
    {
        if (runFinalized)
            return;

        runFinalized = true;

        if (IsMetaProgressionSuppressedForCurrentRun())
            return;

        ChaosJusticeManager chaosJustice = GetChaosJusticeManager();
        int chaosChoicesThisRun = chaosJustice != null ? Mathf.Max(0, chaosJustice.GetChaosChoiceCount() - baselineChaosChoiceCount) : 0;
        int justiceChoicesThisRun = chaosJustice != null ? Mathf.Max(0, chaosJustice.GetJusticeChoiceCount() - baselineJusticeChoiceCount) : 0;
        int noModifierChoicesThisRun = chaosJustice != null ? Mathf.Max(0, chaosJustice.GetNoModifierChoiceCount() - baselineNoModifierChoiceCount) : 0;

        pendingRunChaosKnowledge += chaosChoicesThisRun * Mathf.Max(0, chaosChoiceKnowledgeReward);

        lastRunChaosChoices = chaosChoicesThisRun;
        lastRunChaosKnowledgeGained = Mathf.Max(0, pendingRunChaosKnowledge);
        lastRunRiftCoresGained = Mathf.Max(0, pendingRunRiftCores);

        chaosKnowledge += lastRunChaosKnowledgeGained;
        riftCores += lastRunRiftCoresGained;

        totalChaosChoicesEver += chaosChoicesThisRun;
        totalJusticeChoicesEver += justiceChoicesThisRun;
        totalNoModifierChoicesEver += noModifierChoicesThisRun;

        if (chaosJustice != null)
        {
            baselineChaosChoiceCount = chaosJustice.GetChaosChoiceCount();
            baselineJusticeChoiceCount = chaosJustice.GetJusticeChoiceCount();
            baselineNoModifierChoiceCount = chaosJustice.GetNoModifierChoiceCount();
        }

        AccumulateRunHistory();
        SaveProfile();
    }

    public List<ChaosResearchNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public ChaosResearchNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        ChaosResearchNodeDefinition definition;
        return definitionById.TryGetValue(nodeId, out definition) ? definition : null;
    }

    public ChaosResearchNodeState GetNodeState(string nodeId)
    {
        EnsureStates();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        ChaosResearchNodeState state;
        return stateById.TryGetValue(nodeId, out state) ? state : null;
    }

    public bool IsNodePurchased(string nodeId)
    {
        ChaosResearchNodeState state = GetNodeState(nodeId);
        return state != null && state.purchased;
    }

    public bool IsNodeActive(string nodeId)
    {
        ChaosResearchNodeDefinition definition = GetDefinition(nodeId);
        ChaosResearchNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased)
            return false;

        return definition.RequiresLoadoutSlot() ? state.active : true;
    }

    public bool CanPurchaseNode(string nodeId)
    {
        ChaosResearchNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return false;

        ChaosResearchNodeState state = GetNodeState(nodeId);
        if (state != null && state.purchased)
            return false;

        if (chaosKnowledge < definition.chaosKnowledgeCost)
            return false;

        if (riftCores < definition.riftCoreCost)
            return false;

        return AreSpecialRequirementsMet(definition);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        if (!CanPurchaseNode(nodeId))
            return false;

        ChaosResearchNodeDefinition definition = GetDefinition(nodeId);
        ChaosResearchNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null)
            return false;

        chaosKnowledge -= Mathf.Max(0, definition.chaosKnowledgeCost);
        riftCores -= Mathf.Max(0, definition.riftCoreCost);
        state.purchased = true;
        state.active = !definition.RequiresLoadoutSlot();
        SaveProfile();
        return true;
    }

    public bool CanActivateNode(string nodeId)
    {
        ChaosResearchNodeDefinition definition = GetDefinition(nodeId);
        ChaosResearchNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased || state.active || !definition.RequiresLoadoutSlot())
            return false;

        int usedSlotsAfterReplacingGroup = GetUsedLoadoutSlots() - GetActiveSlotsInExclusiveGroup(definition);
        return usedSlotsAfterReplacingGroup + definition.slotCost <= GetLoadoutSlotCapacity();
    }

    public bool TryActivateNode(string nodeId)
    {
        if (!CanActivateNode(nodeId))
            return false;

        ChaosResearchNodeDefinition definition = GetDefinition(nodeId);
        ChaosResearchNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null)
            return false;

        DeactivateExclusiveLoadoutGroup(definition);
        state.active = true;
        SaveProfile();
        return true;
    }

    public bool TryDeactivateNode(string nodeId)
    {
        ChaosResearchNodeDefinition definition = GetDefinition(nodeId);
        ChaosResearchNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !definition.RequiresLoadoutSlot() || !state.active)
            return false;

        state.active = false;
        SaveProfile();
        return true;
    }

    public bool TrySpendRiftCores(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return true;

        if (riftCores < safeAmount)
            return false;

        riftCores -= safeAmount;
        SaveProfile();
        return true;
    }

    public int GetLoadoutSlotCapacity()
    {
        int slots = Mathf.Max(0, baseChaosLoadoutSlots);

        if (highestChaosLevelEver >= 3)
            slots++;

        if (highestChaosLevelEver >= 5)
            slots++;

        if (totalChaos5BossKillsEver > 0)
            slots++;

        return Mathf.Clamp(slots, 0, Mathf.Max(baseChaosLoadoutSlots, maxChaosLoadoutSlots));
    }

    public int GetUsedLoadoutSlots()
    {
        EnsureStates();
        int used = 0;

        foreach (ChaosResearchNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot())
                continue;

            ChaosResearchNodeState state = GetNodeState(definition.nodeId);
            if (state != null && state.purchased && state.active)
                used += Mathf.Max(0, definition.slotCost);
        }

        return used;
    }

    public int GetAvailableLoadoutSlots()
    {
        return Mathf.Max(0, GetLoadoutSlotCapacity() - GetUsedLoadoutSlots());
    }

    public string GetTopBarSummary()
    {
        return "Chaos-Wissen " + chaosKnowledge +
               " | Risskerne " + riftCores +
               " | Chaos " + highestChaosLevelEver + "/5" +
               " | Chaos-Loadout " + GetUsedLoadoutSlots() + "/" + GetLoadoutSlotCapacity();
    }

    public string GetOverviewText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>Chaos-Forschung</b>");
        builder.AppendLine("Chaos-Wissen: " + chaosKnowledge);
        builder.AppendLine("Risskerne: " + riftCores);
        builder.AppendLine("Hoechstes Chaos-Level: " + highestChaosLevelEver + " / 5");
        builder.AppendLine("Chaos-Waves ueberstanden: " + totalChaosWavesCompletedEver);
        builder.AppendLine("Chaos-Varianten getoetet: " + totalChaosVariantKillsEver);
        builder.AppendLine("Chaos-Wave-Baustein-Waves: " + totalChaosWaveBlockWavesEver);
        builder.AppendLine("Chaos-Loadout: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
        builder.AppendLine();
        builder.AppendLine("<b>Letzter Run</b>");
        builder.AppendLine("+ " + lastRunChaosKnowledgeGained + " Chaos-Wissen");
        builder.AppendLine("+ " + lastRunRiftCoresGained + " Risskerne");
        builder.AppendLine("Chaos-Choices: " + lastRunChaosChoices);
        builder.AppendLine("Chaos-Waves: " + lastRunChaosWavesCompleted);
        builder.AppendLine("Chaos-Varianten-Kills: " + lastRunChaosVariantKills);
        builder.AppendLine("Block-Waves: " + lastRunChaosWaveBlockWaves);
        builder.AppendLine();
        builder.AppendLine("<b>Naechste Ziele</b>");
        AppendNextGoals(builder);
        builder.AppendLine();
        builder.AppendLine("Regel: Chaos-Forschung verbessert Lesbarkeit, Angebotskontrolle und kleine spezifische Konter. Chaos bleibt gefaehrlich.");
        return builder.ToString();
    }

    public string GetNodeStateText(ChaosResearchNodeDefinition definition)
    {
        if (definition == null)
            return "Unbekannt";

        ChaosResearchNodeState state = GetNodeState(definition.nodeId);

        if (state != null && state.purchased)
        {
            if (definition.RequiresLoadoutSlot())
                return state.active ? "Aktiv | Slot " + definition.slotCost : "Gekauft | Slot " + definition.slotCost;

            return definition.kind == ChaosResearchNodeKind.Counter || definition.kind == ChaosResearchNodeKind.Endgame
                ? "Freigeschaltet"
                : "Erforscht";
        }

        if (CanPurchaseNode(definition.nodeId))
            return "Kaufbar | " + FormatCost(definition);

        if (!AreSpecialRequirementsMet(definition))
            return "Gesperrt | Bedingung";

        if (chaosKnowledge < definition.chaosKnowledgeCost || riftCores < definition.riftCoreCost)
            return "Ressourcen fehlen | " + FormatCost(definition);

        return "Gesperrt";
    }

    public string GetNodeDetailText(string nodeId)
    {
        ChaosResearchNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return "Chaos-Forschungs-Knoten nicht gefunden.";

        ChaosResearchNodeState state = GetNodeState(nodeId);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>" + definition.displayName + "</b>");
        builder.AppendLine("<size=90%><color=#B9C2D0>" + GetNodeStateText(definition) + "</color></size>");
        builder.AppendLine();
        builder.AppendLine("Kategorie: " + GetCategoryDisplayName(definition.category));
        builder.AppendLine("Typ: " + GetKindDisplayName(definition.kind));
        builder.AppendLine("Kosten: " + FormatCost(definition));

        if (definition.RequiresLoadoutSlot())
        {
            builder.AppendLine("Slot-Kosten: " + definition.slotCost);
            builder.AppendLine("Chaos-Loadout: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
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
            builder.AppendLine(state.active ? "Aktiv fuer den naechsten Run." : "Gekauft, aber nicht im Chaos-Loadout aktiv.");

        return builder.ToString();
    }

    public static string GetCategoryDisplayName(ChaosResearchCategory category)
    {
        switch (category)
        {
            case ChaosResearchCategory.Overview: return "Uebersicht";
            case ChaosResearchCategory.RiskPool: return "Risiko-Pool";
            case ChaosResearchCategory.ChaosVariants: return "Chaos-Varianten";
            case ChaosResearchCategory.ChaosWaves: return "Chaos-Waves";
            case ChaosResearchCategory.ChaosCounters: return "Chaos-Konter";
            case ChaosResearchCategory.OfferControl: return "Angebotskontrolle";
            case ChaosResearchCategory.Chaos5Endgame: return "Chaos-5-Endgame";
            case ChaosResearchCategory.JusticeOrder: return "Gerechtigkeit / Ordnung";
            default: return category.ToString();
        }
    }

    public static string GetKindDisplayName(ChaosResearchNodeKind kind)
    {
        switch (kind)
        {
            case ChaosResearchNodeKind.Core: return "Basis / Uebersicht";
            case ChaosResearchNodeKind.RiskPoolUnlock: return "Risiko-Pool-Unlock";
            case ChaosResearchNodeKind.VariantResearch: return "Varianten-Forschung";
            case ChaosResearchNodeKind.WaveResearch: return "Chaos-Wave-Forschung";
            case ChaosResearchNodeKind.Counter: return "Chaos-Konter, Loadout";
            case ChaosResearchNodeKind.OfferControl: return "Angebotskontrolle";
            case ChaosResearchNodeKind.Endgame: return "Chaos-5-Endgame";
            case ChaosResearchNodeKind.JusticeOrder: return "Gerechtigkeit / Ordnung";
            default: return kind.ToString();
        }
    }

    public int GetPurchasedCount(ChaosResearchCategory category)
    {
        int purchased = 0;

        foreach (ChaosResearchNodeDefinition definition in GetDefinitions())
        {
            if (definition != null && definition.category == category && IsNodePurchased(definition.nodeId))
                purchased++;
        }

        return purchased;
    }

    public int GetDefinitionCount(ChaosResearchCategory category)
    {
        int total = 0;

        foreach (ChaosResearchNodeDefinition definition in GetDefinitions())
        {
            if (definition != null && definition.category == category)
                total++;
        }

        return total;
    }

    public int GetActiveCounterCount()
    {
        int total = 0;

        foreach (ChaosResearchNodeDefinition definition in GetDefinitions())
        {
            if (definition != null && definition.RequiresLoadoutSlot() && IsNodeActive(definition.nodeId))
                total++;
        }

        return total;
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || runFinalized || IsMetaProgressionSuppressedForCurrentRun())
            return;

        int knownHighest = Mathf.Max(highestChaosLevelEver, GetHighestChaosLevelInCurrentHistoryBefore(result));
        if (result.chaosLevelAtWaveStart > knownHighest)
            pendingRunChaosKnowledge += (result.chaosLevelAtWaveStart - knownHighest) * Mathf.Max(0, newHighestChaosLevelKnowledgeReward);

        highestWaveEver = Mathf.Max(highestWaveEver, result.waveNumber);
        highestChaosLevelEver = Mathf.Max(highestChaosLevelEver, result.chaosLevelAtWaveStart);
        highestGoldJusticeEver = Mathf.Max(highestGoldJusticeEver, result.goldJusticeLevelAtWaveStart);
        highestXpJusticeEver = Mathf.Max(highestXpJusticeEver, result.xpJusticeLevelAtWaveStart);

        if (result.chaosLevelAtWaveStart > 0)
        {
            pendingRunChaosKnowledge += Mathf.Max(0, chaosWaveCompletedKnowledgeReward);
            lastRunChaosWavesCompleted++;
        }

        if (result.chaosLevelAtWaveStart >= 2)
            pendingRunChaosKnowledge += Mathf.Max(0, chaosTwoPlusWaveKnowledgeReward);

        if (result.bossDefeated && result.chaosLevelAtWaveStart >= 2)
        {
            pendingRunChaosKnowledge += Mathf.Max(0, chaosTwoPlusBossKnowledgeReward);
            totalBossKillsAtChaosTwoPlusEver++;
        }

        if (result.bossDefeated && result.chaosLevelAtWaveStart <= 0)
            totalSafeBossWithoutChaosEver++;

        AddChaosVariantKillRewards(result);
        AddChaosWaveBlockRewards(result);
        AddRiskModifierRuntimeProgress(result);
        AddChaos5RiftCoreRewards(result);

        if (result.isMiniBossWave && result.miniBossDefeated)
            totalMiniBossKillsEver++;
    }

    private void AddChaosVariantKillRewards(WaveCompletionResult result)
    {
        int variantKills = Mathf.Max(0, result.chaosVariantKilledCount);
        lastRunChaosVariantKills += variantKills;

        int remainingKillRewardCap = Mathf.Max(0, maxChaosVariantKillKnowledgePerRun - currentRunChaosVariantKillKnowledge);
        int rewardedKills = Mathf.Min(variantKills, remainingKillRewardCap);
        pendingRunChaosKnowledge += rewardedKills * Mathf.Max(0, chaosVariantKillKnowledgeReward);
        currentRunChaosVariantKillKnowledge += rewardedKills;

        if (result.killedChaosVariantRoles == null)
            return;

        foreach (EnemyRoleCount roleCount in result.killedChaosVariantRoles)
        {
            if (roleCount == null || roleCount.count <= 0)
                continue;

            EnemyRole role = roleCount.role;
            chaosVariantKillsByRole[role] = GetChaosVariantKills(role) + roleCount.count;

            string key = role.ToString();
            if (!firstChaosVariantRolesClaimed.Contains(key))
            {
                firstChaosVariantRolesClaimed.Add(key);
                pendingRunChaosKnowledge += Mathf.Max(0, firstChaosVariantRoleKnowledgeReward);
            }
        }
    }

    private void AddChaosWaveBlockRewards(WaveCompletionResult result)
    {
        if (!result.hadChaosWaveBlocksAtWaveStart)
            return;

        pendingRunChaosKnowledge += Mathf.Max(0, chaosWaveBlockKnowledgeReward);
        lastRunChaosWaveBlockWaves++;

        if (result.chaosWaveBlocksAtWaveStart == null)
            return;

        foreach (ChaosWaveBlock block in result.chaosWaveBlocksAtWaveStart)
        {
            if (block == null || !block.IsValid())
                continue;

            ChaosWaveBlockType blockType = block.blockType;
            chaosWaveBlockCompletionsByType[blockType] = GetChaosWaveBlockCompletions(blockType) + 1;

            string key = blockType.ToString();
            if (!firstChaosWaveBlocksClaimed.Contains(key))
            {
                firstChaosWaveBlocksClaimed.Add(key);
                pendingRunChaosKnowledge += Mathf.Max(0, firstChaosWaveBlockKnowledgeReward);
            }
        }
    }

    private void AddRiskModifierRuntimeProgress(WaveCompletionResult result)
    {
        if (result.activeRiskModifiersAtWaveStart == null)
            return;

        totalRiskModifiersSeenEver += Mathf.Max(0, result.activeRiskModifiersAtWaveStart.Count);

        foreach (WaveModifier modifier in result.activeRiskModifiersAtWaveStart)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            string key = modifier.GetStableId();
            riskModifierKeysSeen.Add(key);

            if (modifier.GetDisplayRiskLevel() >= 2)
                riskLevelTwoPlusKeysSeen.Add(key);
        }
    }

    private void AddChaos5RiftCoreRewards(WaveCompletionResult result)
    {
        if (result.chaosLevelAtWaveStart < 5)
            return;

        lastRunChaos5WavesCompleted++;

        if (currentRunChaos5WaveRiftCores < Mathf.Max(0, maxRiftCoresFromChaos5WavesPerRun))
        {
            pendingRunRiftCores++;
            currentRunChaos5WaveRiftCores++;
        }

        if (result.GetChaosWaveBlockCount() >= 2 && currentRunChaos5BlockWaveRiftCores < Mathf.Max(0, maxRiftCoresFromChaos5BlockWavesPerRun))
        {
            pendingRunRiftCores++;
            currentRunChaos5BlockWaveRiftCores++;
        }

        if (result.bossDefeated)
        {
            pendingRunRiftCores += Mathf.Max(0, chaos5BossRiftCoreReward);
            lastRunChaos5BossKills++;

            if (result.enemiesReachedBase <= 0 && result.baseDamageTaken <= 0)
                pendingRunRiftCores += Mathf.Max(0, perfectChaos5BossRiftCoreBonus);
        }
    }

    private void HandleGameOverTriggered()
    {
        FinalizeRun();
    }

    private void AccumulateRunHistory()
    {
        totalChaosWavesCompletedEver += lastRunChaosWavesCompleted;
        totalChaosVariantKillsEver += lastRunChaosVariantKills;
        totalChaosWaveBlockWavesEver += lastRunChaosWaveBlockWaves;
        totalChaos5WavesCompletedEver += lastRunChaos5WavesCompleted;
        totalChaos5BossKillsEver += lastRunChaos5BossKills;

        WaveHistory history = GetWaveHistory();
        if (history == null)
            return;

        highestChaosLevelEver = Mathf.Max(highestChaosLevelEver, history.GetHighestChaosLevelSeen());
        highestGoldJusticeEver = Mathf.Max(highestGoldJusticeEver, history.GetHighestGoldJusticeLevelSeen());
        highestXpJusticeEver = Mathf.Max(highestXpJusticeEver, history.GetHighestXpJusticeLevelSeen());
        highestWaveEver = Mathf.Max(highestWaveEver, history.GetHighestWaveNumberReached());
    }

    private bool AreSpecialRequirementsMet(ChaosResearchNodeDefinition definition)
    {
        if (definition == null)
            return false;

        string id = definition.nodeId;

        switch (id)
        {
            case "chaos.core.unlock":
                return GetTotalBossKillsIncludingCurrentHistory() >= 1 || highestWaveEver >= 10;
            case "chaos.core.risk_preview":
            case "chaos.offer.preview_plus":
                return GetTotalChaosChoicesIncludingCurrentRun() >= 1;
            case "chaos.core.balance_bar":
            case "justice.balance_tracker":
                return GetTotalChaosChoicesIncludingCurrentRun() >= 1 && GetTotalJusticeChoicesIncludingCurrentRun() >= 1;
            case "chaos.core.result_summary":
                return totalChaosWavesCompletedEver > 0 || lastRunChaosWavesCompleted > 0;
            case "chaos.risk.reward_1":
                return GetTotalChaosChoicesIncludingCurrentRun() >= 1;
            case "chaos.risk.reward_2":
            case "chaos.offer.category_hint":
                return GetTotalChaosChoicesIncludingCurrentRun() >= 3;
            case "chaos.risk.mage":
                return HasSeenScenario(WaveScenario.MageIntro) || highestWaveEver >= 6;
            case "chaos.risk.learner":
                return HasSeenScenario(WaveScenario.EffectImmunity) || highestWaveEver >= 7;
            case "chaos.risk.mixed":
                return GetTotalBossKillsIncludingCurrentHistory() >= 1;
            case "chaos.risk.allrounder":
                return HasSeenRole(EnemyRole.AllRounder);
            case "chaos.risk.preboss":
                return highestWaveEver >= 9;
            case "chaos.risk.miniboss":
                return GetTotalMiniBossKillsIncludingCurrentHistory() >= 3;
            case "chaos.risk.variant_pressure":
                return highestChaosLevelEver >= 3;
            case "chaos.risk.wave_density":
            case "chaos.risk.wave_toughness":
                return highestChaosLevelEver >= 2;
            case "chaos.risk.wave_rearguard":
                return highestChaosLevelEver >= 3;
            case "chaos.risk.wave_armor":
                return highestChaosLevelEver >= 4;
            case "chaos.risk.wave_resistance":
            case "chaos.endgame.unlock":
            case "chaos.offer.reroll_2":
            case "chaos.offer.no_modifier_value":
                return highestChaosLevelEver >= 5;
            case "chaos.risk.preview_hidden":
            case "chaos.offer.fourth_card":
            case "chaos.endgame.boss_core_1":
            case "chaos.endgame.rift_visuals":
                return totalChaos5BossKillsEver > 0 || lastRunChaos5BossKills > 0;
            case "chaos.variant.runner":
                return totalChaosVariantKillsEver + lastRunChaosVariantKills >= 5;
            case "chaos.variant.tank":
                return totalChaosVariantKillsEver + lastRunChaosVariantKills >= 10;
            case "chaos.variant.knight":
                return totalChaosVariantKillsEver + lastRunChaosVariantKills >= 15;
            case "chaos.variant.mage":
                return GetChaosVariantKills(EnemyRole.Mage) > 0 || HasSeenScenario(WaveScenario.MageIntro);
            case "chaos.variant.learner":
                return GetChaosVariantKills(EnemyRole.Learner) > 0 || HasSeenScenario(WaveScenario.EffectImmunity);
            case "chaos.variant.allrounder":
                return GetChaosVariantKills(EnemyRole.AllRounder) > 0 || HasSeenRole(EnemyRole.AllRounder);
            case "chaos.variant.visual_readability":
                return totalChaosVariantKillsEver + lastRunChaosVariantKills >= 20;
            case "chaos.variant.preview_roles":
            case "chaos.endgame.variant_core":
                return totalChaosVariantKillsEver + lastRunChaosVariantKills >= 50;
            case "chaos.variant.kill_bonus":
                return highestChaosLevelEver >= 5;
            case "chaos.wave.density":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.Density) > 0;
            case "chaos.wave.toughness":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.Toughness) > 0;
            case "chaos.wave.role_pressure":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.RolePressure) > 0;
            case "chaos.wave.rearguard":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.Rearguard) > 0;
            case "chaos.wave.variant_group":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.ChaosVariantGroup) > 0;
            case "chaos.wave.armor":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.Armor) > 0;
            case "chaos.wave.resistance":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.Resistance) > 0;
            case "chaos.wave.preview_hidden":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.PreviewHidden) > 0;
            case "chaos.wave.summary_plus":
                return totalChaosWaveBlockWavesEver + lastRunChaosWaveBlockWaves >= 5;
            case "chaos.wave.block_forecast":
                return highestChaosLevelEver >= 5;
            case "chaos.counter.runner_1":
                return GetChaosVariantKills(EnemyRole.Runner) >= 5;
            case "chaos.counter.runner_2":
                return GetChaosVariantKills(EnemyRole.Runner) >= 20;
            case "chaos.counter.tank_1":
                return GetChaosVariantKills(EnemyRole.Tank) >= 5;
            case "chaos.counter.knight_1":
                return GetChaosVariantKills(EnemyRole.Knight) >= 5;
            case "chaos.counter.knight_2":
                return GetChaosVariantKills(EnemyRole.Knight) >= 20;
            case "chaos.counter.mage_1":
                return GetChaosVariantKills(EnemyRole.Mage) > 0;
            case "chaos.counter.learner_1":
                return GetChaosVariantKills(EnemyRole.Learner) > 0;
            case "chaos.counter.learner_dot_1":
                return GetChaosVariantKills(EnemyRole.Learner) >= 10;
            case "chaos.counter.allrounder_1":
                return GetChaosVariantKills(EnemyRole.AllRounder) > 0;
            case "chaos.counter.armor_wave_1":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.Armor) > 0;
            case "chaos.counter.resistance_1":
                return GetChaosWaveBlockCompletions(ChaosWaveBlockType.Resistance) > 0;
            case "chaos.offer.reroll_1":
                return GetTotalChaosChoicesIncludingCurrentRun() >= 5;
            case "chaos.offer.ban_1":
                return GetRiskModifierSeenCount() >= 10 || totalRiskModifiersSeenEver >= 10;
            case "chaos.offer.prefer_1":
                return GetTotalChaosChoicesIncludingCurrentRun() >= 10;
            case "chaos.endgame.wave_core_1":
                return totalChaos5WavesCompletedEver + lastRunChaos5WavesCompleted >= 3;
            case "chaos.endgame.wave_block_core":
                return totalChaosWaveBlockWavesEver + lastRunChaosWaveBlockWaves >= 10;
            case "chaos.endgame.safe_hold":
                return highestChaosLevelEver >= 5 && GetTotalNoModifierChoicesIncludingCurrentRun() > 0;
            case "chaos.endgame.risk_mastery":
                return riskLevelTwoPlusKeysSeen.Count >= 3;
            case "chaos.endgame.elite_bridge":
                return GetTotalEliteKillsIncludingCurrentHistory() > 0 || highestWaveEver >= 30;
            case "justice.gold.record_1":
                return highestGoldJusticeEver >= 1;
            case "justice.gold.record_2":
                return highestGoldJusticeEver >= 3;
            case "justice.gold.safe_bonus":
            case "justice.xp.safe_bonus":
                return totalSafeBossWithoutChaosEver > 0;
            case "justice.gold.loadout":
            case "justice.xp.loadout":
            case "justice.archivist":
                return GetTotalJusticeChoicesIncludingCurrentRun() >= 5;
            case "justice.xp.record_1":
                return highestXpJusticeEver >= 1;
            case "justice.xp.record_2":
                return highestXpJusticeEver >= 3;
            case "justice.safe_path_rewards":
                return totalSafeBossWithoutChaosEver >= 3;
            case "justice.mixed_path":
                return GetTotalChaosChoicesIncludingCurrentRun() >= 1 && GetTotalJusticeChoicesIncludingCurrentRun() >= 1;
            default:
                return true;
        }
    }

    private string GetRequirementProgressText(ChaosResearchNodeDefinition definition)
    {
        if (definition == null)
            return "Keine Daten.";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Chaos-Wissen: " + Mathf.Min(chaosKnowledge, definition.chaosKnowledgeCost) + " / " + definition.chaosKnowledgeCost);

        if (definition.riftCoreCost > 0)
            builder.AppendLine("Risskerne: " + Mathf.Min(riftCores, definition.riftCoreCost) + " / " + definition.riftCoreCost);

        string id = definition.nodeId;

        if (id.Contains(".risk.") || id.Contains(".offer."))
        {
            builder.AppendLine("Chaos-Choices: " + GetTotalChaosChoicesIncludingCurrentRun());
            builder.AppendLine("Hoechstes Chaos: " + highestChaosLevelEver + " / 5");
        }

        if (id.Contains(".variant.") || id.Contains(".counter."))
        {
            builder.AppendLine("Chaos-Varianten-Kills: " + (totalChaosVariantKillsEver + lastRunChaosVariantKills));
            builder.AppendLine("Runner/Tank/Knight/Mage/Learner/AllRounder: " +
                GetChaosVariantKills(EnemyRole.Runner) + "/" +
                GetChaosVariantKills(EnemyRole.Tank) + "/" +
                GetChaosVariantKills(EnemyRole.Knight) + "/" +
                GetChaosVariantKills(EnemyRole.Mage) + "/" +
                GetChaosVariantKills(EnemyRole.Learner) + "/" +
                GetChaosVariantKills(EnemyRole.AllRounder));
        }

        if (id.Contains(".wave."))
        {
            builder.AppendLine("Block-Waves: " + (totalChaosWaveBlockWavesEver + lastRunChaosWaveBlockWaves));
            builder.AppendLine("Density/Toughness/Rearguard/Armor/Resistance: " +
                GetChaosWaveBlockCompletions(ChaosWaveBlockType.Density) + "/" +
                GetChaosWaveBlockCompletions(ChaosWaveBlockType.Toughness) + "/" +
                GetChaosWaveBlockCompletions(ChaosWaveBlockType.Rearguard) + "/" +
                GetChaosWaveBlockCompletions(ChaosWaveBlockType.Armor) + "/" +
                GetChaosWaveBlockCompletions(ChaosWaveBlockType.Resistance));
        }

        if (id.Contains(".endgame."))
        {
            builder.AppendLine("Chaos-5-Waves: " + (totalChaos5WavesCompletedEver + lastRunChaos5WavesCompleted));
            builder.AppendLine("Chaos-5-Bosskills: " + (totalChaos5BossKillsEver + lastRunChaos5BossKills));
            builder.AppendLine("Risiken Stufe 2+: " + riskLevelTwoPlusKeysSeen.Count + " / 3");
        }

        if (id.StartsWith("justice."))
        {
            builder.AppendLine("Gold-Gerechtigkeit: " + highestGoldJusticeEver);
            builder.AppendLine("XP-Gerechtigkeit: " + highestXpJusticeEver);
            builder.AppendLine("Justice-Choices: " + GetTotalJusticeChoicesIncludingCurrentRun());
            builder.AppendLine("Boss ohne Chaos: " + totalSafeBossWithoutChaosEver);
        }

        builder.AppendLine("Spezialbedingung: " + FormatYesNo(AreSpecialRequirementsMet(definition)));
        return builder.ToString();
    }

    private void AppendNextGoals(StringBuilder builder)
    {
        if (highestChaosLevelEver < 5)
            builder.AppendLine("- Erreiche Chaos " + Mathf.Min(5, highestChaosLevelEver + 1) + ".");

        if (totalBossKillsAtChaosTwoPlusEver <= 0)
            builder.AppendLine("- Besiege einen Boss bei Chaos 2+.");

        if (totalChaosWaveBlockWavesEver < 3)
            builder.AppendLine("- Ueberstehe 3 Waves mit Chaos-Wave-Bausteinen.");

        if (totalChaosVariantKillsEver < 20)
            builder.AppendLine("- Toete 20 Chaos-Varianten.");

        if (totalJusticeChoicesEver < 3)
            builder.AppendLine("- Waehle Gerechtigkeit mehrmals, damit Ordnung sichtbar bleibt.");

        if (highestChaosLevelEver >= 5 && totalChaos5BossKillsEver <= 0)
            builder.AppendLine("- Besiege einen Boss bei Chaos 5.");
    }

    private void EnsureDefinitions()
    {
        if (definitions == null)
            definitions = new List<ChaosResearchNodeDefinition>();

        if (definitions.Count == 0)
            CreateDefaultDefinitions();

        RebuildDefinitionLookup();
    }

    private void EnsureStates()
    {
        EnsureDefinitions();

        if (nodeStates == null)
            nodeStates = new List<ChaosResearchNodeState>();

        RebuildStateLookup();

        foreach (ChaosResearchNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            if (stateById.ContainsKey(definition.nodeId))
                continue;

            ChaosResearchNodeState state = new ChaosResearchNodeState
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

        foreach (ChaosResearchNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            definitionById[definition.nodeId] = definition;
        }
    }

    private void RebuildStateLookup()
    {
        stateById.Clear();

        foreach (ChaosResearchNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            stateById[state.nodeId] = state;
        }
    }

    private void CreateDefaultDefinitions()
    {
        definitions.Clear();

        AddDefinition("chaos.core.unlock", "Chaos-Forschung", ChaosResearchCategory.Overview, ChaosResearchNodeKind.Core, 0, 0, "Der Chaos-Forschung-Tab wird sichtbar und erklaert Chaos-Wissen, Risskerne, Risiko-Pool, Varianten, Wave-Bausteine und Ordnung.", false, "Boss 1 erreicht.");
        AddDefinition("chaos.core.risk_preview", "Risiko-Vorschau", ChaosResearchCategory.Overview, ChaosResearchNodeKind.Core, 80, 0, "Risiko-Angebote zeigen Kategorie und Effekt klarer.", false, "1x Chaos gewaehlt.");
        AddDefinition("chaos.core.balance_bar", "Balance-Balken", ChaosResearchCategory.Overview, ChaosResearchNodeKind.Core, 100, 0, "Chaos/Ordnung-Anzeige im Meta-Hub wird klarer.", false, "1x Chaos und 1x Justice gewaehlt.");
        AddDefinition("chaos.core.result_summary", "Chaos-Auswertung", ChaosResearchCategory.Overview, ChaosResearchNodeKind.Core, 120, 0, "Result zeigt Chaos-Wissen, Risskerne und Quellen genauer.", false, "Game Over nach Chaos-Run.");

        AddDefinition("chaos.risk.core", "Grundrisiken", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 0, 0, "Grundrisiken wie mehr Gegner, Runner-/Tank-/Knight-Druck und schnellere Spawns gehoeren zur Basis des Chaos-Pools.", true);
        AddDefinition("chaos.risk.reward_1", "Belohnungsrisiken I", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 80, 0, "Goldrausch/Eile-Risiken werden fuer den Risiko-Pool vorbereitet.", false, "1 Chaos-Choice.");
        AddDefinition("chaos.risk.reward_2", "Belohnungsrisiken II", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 160, 0, "XP-Pruefung und staerkere Reward-Risiken werden vorbereitet.", false, "3 Chaos-Choices.");
        AddDefinition("chaos.risk.mage", "Mage-Druck", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 140, 0, "Mage-Druck kann im Risiko-Pool erscheinen.", false, "MageIntro gesehen.");
        AddDefinition("chaos.risk.learner", "Learner-Druck", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 160, 0, "Learner-Druck kann im Risiko-Pool erscheinen.", false, "EffectImmunity gesehen.");
        AddDefinition("chaos.risk.mixed", "Gemischter Rollendruck", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 220, 0, "MixedRolePressure wird fuer Chaos-Angebote vorbereitet.", false, "Boss 1 besiegt.");
        AddDefinition("chaos.risk.allrounder", "AllRounder-Druck", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 300, 0, "AllRounder-Druck wird vorbereitet.", false, "AllRounder gesehen.");
        AddDefinition("chaos.risk.preboss", "Vor-Boss-Druck", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 320, 0, "PreBossPressure wird vorbereitet.", false, "Wave 9 erreicht.");
        AddDefinition("chaos.risk.miniboss", "MiniBoss-Druck", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 450, 0, "MiniBossPressure wird vorbereitet.", false, "3 MiniBosskills.");
        AddDefinition("chaos.risk.variant_pressure", "Violette Verformung", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 380, 0, "ChaosVariantPressure wird vorbereitet.", false, "Chaos 3 erreicht.");
        AddDefinition("chaos.risk.wave_density", "Staerkere Verdichtung", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 280, 0, "Density-Wave-Risiko wird vorbereitet.", false, "Chaos 2 erreicht.");
        AddDefinition("chaos.risk.wave_toughness", "Zaehe Wellen", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 300, 0, "Toughness-Wave-Risiko wird vorbereitet.", false, "Chaos 2 erreicht.");
        AddDefinition("chaos.risk.wave_rearguard", "Staerkere Nachhut", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 420, 0, "Rearguard-Wave-Risiko wird vorbereitet.", false, "Chaos 3 erreicht.");
        AddDefinition("chaos.risk.wave_armor", "Chaos-Panzerung", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 550, 0, "Armor-Wave-Risiko wird vorbereitet.", false, "Chaos 4 erreicht.");
        AddDefinition("chaos.risk.wave_resistance", "Resistente Wellen", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 700, 1, "Resistance-Wave-Risiko wird vorbereitet.", false, "Chaos 5 erreicht.");
        AddDefinition("chaos.risk.preview_hidden", "Verhuellte Preview", ChaosResearchCategory.RiskPool, ChaosResearchNodeKind.RiskPoolUnlock, 900, 4, "PreviewHidden-Risiko wird optional und spaet vorbereitet.", false, "Chaos-5-Boss besiegt.");

        AddDefinition("chaos.variant.runner", "Chaos Runner Studie", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 120, 0, "Chaos Runner werden im Lexikon und in der Preview besser erklaert.", false, "5 Varianten getoetet.");
        AddDefinition("chaos.variant.tank", "Chaos Tank Studie", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 160, 0, "Tank-Regen wird erklaert.", false, "10 Varianten getoetet.");
        AddDefinition("chaos.variant.knight", "Chaos Knight Studie", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 180, 0, "Speed-/Armor-Hinweis fuer Chaos Knights.", false, "15 Varianten getoetet.");
        AddDefinition("chaos.variant.mage", "Chaos Mage Studie", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 240, 0, "Teleportbonus wird erklaert.", false, "Chaos Mage gesehen.");
        AddDefinition("chaos.variant.learner", "Chaos Learner Studie", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 260, 0, "DoT-Heilung und DoT-Reduktion werden erklaert.", false, "Chaos Learner gesehen.");
        AddDefinition("chaos.variant.allrounder", "Chaos AllRounder Studie", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 340, 0, "Armor-Aufbau wird erklaert.", false, "Chaos AllRounder gesehen.");
        AddDefinition("chaos.variant.visual_readability", "Violette Lesbarkeit", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 220, 0, "Chaos-Varianten erhalten klarere Visual-/HUD-Hinweise.", false, "20 Varianten gesehen.");
        AddDefinition("chaos.variant.preview_roles", "Varianten-Preview", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 360, 0, "Preview nennt wahrscheinliche Chaos-Varianten-Rollen besser.", false, "50 Varianten getoetet.");
        AddDefinition("chaos.variant.kill_bonus", "Varianten-Probe", ChaosResearchCategory.ChaosVariants, ChaosResearchNodeKind.VariantResearch, 0, 2, "Chaos-Varianten geben bei Chaos 5 minimal mehr Chaos-Wissen, gedeckelt.", false, "Chaos 5 erreicht.");

        AddDefinition("chaos.wave.density", "Verdichtung verstehen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 120, 0, "Density-Erklaerung und SpawnDelay-Hinweise werden besser.", false, "Density-Wave ueberstanden.");
        AddDefinition("chaos.wave.toughness", "Zaehigkeit verstehen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 140, 0, "Health-Multiplier-Hinweis wird besser.", false, "Toughness-Wave ueberstanden.");
        AddDefinition("chaos.wave.role_pressure", "Rollendruck lesen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 160, 0, "Preview zeigt bevorzugte Rolle besser.", false, "RolePressure gesehen.");
        AddDefinition("chaos.wave.rearguard", "Nachhut lesen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 220, 0, "Nachhut-Hinweis in der Preview.", false, "Rearguard-Wave ueberstanden.");
        AddDefinition("chaos.wave.variant_group", "Violette Gruppen lesen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 260, 0, "Variantenwahrscheinlichkeit wird besser angezeigt.", false, "ChaosVariantGroup-Wave.");
        AddDefinition("chaos.wave.armor", "Chaos-Panzerung lesen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 320, 0, "Armor-Bonus-Hinweis wird besser.", false, "Armor-Wave ueberstanden.");
        AddDefinition("chaos.wave.resistance", "Resistenz lesen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 500, 1, "Effektresistenz-Hinweis wird besser.", false, "Resistance-Wave gesehen.");
        AddDefinition("chaos.wave.preview_hidden", "Verhuellung lesen", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 700, 2, "Minimale Rest-Info trotz Hidden Preview.", false, "PreviewHidden gesehen.");
        AddDefinition("chaos.wave.summary_plus", "Chaos-Wave-Archiv", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 300, 0, "Result-Screen zeigt bessere Chaos-Wave-Historie.", false, "5 Block-Waves.");
        AddDefinition("chaos.wave.block_forecast", "Baustein-Vorhersage", ChaosResearchCategory.ChaosWaves, ChaosResearchNodeKind.WaveResearch, 0, 3, "Vorschau zeigt den wahrscheinlichen Hauptbaustein frueher.", false, "Chaos 5 erreicht.");

        AddDefinition("chaos.counter.runner_1", "Chaos-Runner-Analyse I", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 180, 0, "+3% Schaden gegen Chaos Runner.", 1, false, "5 Chaos Runner gesehen.");
        AddDefinition("chaos.counter.runner_2", "Chaos-Runner-Analyse II", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 320, 0, "+6% Schaden gegen Chaos Runner gesamt.", 1, false, "20 Chaos Runner getoetet.");
        AddDefinition("chaos.counter.tank_1", "Chaos-Tank-Probe", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 220, 0, "+3% Schaden gegen Chaos Tank und besserer Regen-Hinweis.", 1, false, "5 Chaos Tanks gesehen.");
        AddDefinition("chaos.counter.knight_1", "Chaos-Knight-Analyse I", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 240, 0, "+3% Schaden gegen Chaos Knight.", 1, false, "5 Chaos Knights gesehen.");
        AddDefinition("chaos.counter.knight_2", "Chaos-Knight-Analyse II", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 420, 0, "+6% Schaden gegen Chaos Knight gesamt.", 1, false, "20 Chaos Knights getoetet.");
        AddDefinition("chaos.counter.mage_1", "Chaos-Mage-Stoerung", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 300, 0, "Teleport-Hinweis und kleiner direkter Schadensbonus.", 1, false, "Chaos Mage gesehen.");
        AddDefinition("chaos.counter.learner_1", "Chaos-Learner-Gegenprobe", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 350, 0, "+4% direkter Schaden gegen Chaos Learner.", 1, false, "Chaos Learner gesehen.");
        AddDefinition("chaos.counter.learner_dot_1", "DoT-Gegenprobe", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 0, 2, "DoT-Heilung von Chaos Learnern minimal reduziert, z. B. -5%.", 2, false, "10 Chaos Learner gesehen.");
        AddDefinition("chaos.counter.allrounder_1", "Chaos-AllRounder-Studie", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 360, 0, "Armor-Aufbau-Hinweis und +3% direkter Schaden.", 1, false, "Chaos AllRounder gesehen.");
        AddDefinition("chaos.counter.armor_wave_1", "Risspanzer-Analyse", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 400, 0, "+3% gegen gepanzerte Chaos-Wave-Ziele.", 1, false, "Armor-Wave ueberstanden.");
        AddDefinition("chaos.counter.resistance_1", "Resistenz-Gegenprobe", ChaosResearchCategory.ChaosCounters, ChaosResearchNodeKind.Counter, 0, 3, "Effektverlust in Resistance-Waves minimal schwaecher.", 2, false, "Resistance-Wave ueberstanden.");

        AddDefinition("chaos.offer.preview_plus", "Risiko-Vorschau I", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 180, 0, "Risiko-Angebote zeigen bessere Details.", false, "1 Chaos.");
        AddDefinition("chaos.offer.category_hint", "Kategorie-Hinweis", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 240, 0, "Risiko-Angebote zeigen Kategorie und Schwere klarer.", false, "3 Chaos.");
        AddDefinition("chaos.offer.reroll_1", "Chaos-Reroll I", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 450, 0, "1 Risiko-Angebot pro Run rerollen.", 2, false, "5 Chaos.");
        AddDefinition("chaos.offer.reroll_2", "Chaos-Reroll II", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 0, 3, "Zweiter Reroll, nur Chaos 5+.", 3, false, "Chaos 5 erreicht.");
        AddDefinition("chaos.offer.ban_1", "Risiko-Bann I", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 600, 0, "Eine Risiko-Kategorie wird seltener angeboten.", 2, false, "10 Risiken gesehen.");
        AddDefinition("chaos.offer.prefer_1", "Risiko-Praeferenz I", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 700, 0, "Eine Risiko-Kategorie wird etwas haeufiger angeboten.", 2, false, "10 Chaos.");
        AddDefinition("chaos.offer.no_modifier_value", "Chaos halten lernen", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 0, 2, "Chaos halten gibt eine kleine Belohnung.", 1, false, "Chaos 5.");
        AddDefinition("chaos.offer.fourth_card", "Vierte Risikokarte", ChaosResearchCategory.OfferControl, ChaosResearchNodeKind.OfferControl, 0, 5, "+1 Risiko-Angebot nach Boss.", 3, false, "Chaos-5-Boss.");

        AddDefinition("chaos.endgame.unlock", "Risskern-Forschung", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 0, "Risskerne werden als Endgame-Waehrung sichtbar.", false, "Chaos 5 erreicht.");
        AddDefinition("chaos.endgame.wave_core_1", "Chaos-5-Wave-Probe", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 2, "+1 Chaos-Wissen pro Chaos-5-Wave.", false, "3 Chaos-5-Waves.");
        AddDefinition("chaos.endgame.boss_core_1", "Rissboss-Probe", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 4, "+1 Risskern pro Run bei Chaos-5-Boss.", 2, false, "Chaos-5-Boss.");
        AddDefinition("chaos.endgame.variant_core", "Varianten-Rissprobe", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 3, "Varianten bei Chaos 5 geben mehr Chaos-Wissen, gedeckelt.", 1, false, "50 Varianten.");
        AddDefinition("chaos.endgame.wave_block_core", "Baustein-Rissprobe", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 4, "2+ Bausteine geben Bonus-Chaos-Wissen.", 1, false, "10 Block-Waves.");
        AddDefinition("chaos.endgame.safe_hold", "Chaos halten", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 3, "Chaos halten wird belohnt.", 1, false, "Chaos 5 + NoModifier.");
        AddDefinition("chaos.endgame.risk_mastery", "Risikomeisterschaft", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 6, "Risiko-Angebote zeigen Level-Auswirkung genauer.", false, "3 Risiken auf Stufe 2+.");
        AddDefinition("chaos.endgame.rift_visuals", "Riss-Visuals", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 5, "Kosmetische Riss-Visuals im Meta-Hub.", false, "Chaos-5-Boss.");
        AddDefinition("chaos.endgame.elite_bridge", "Riss-Elite-Spur", ChaosResearchCategory.Chaos5Endgame, ChaosResearchNodeKind.Endgame, 0, 8, "Chaos+Elite-Crossover wird fuer spaeter vorbereitet.", false, "Elite-Jagd frei.");

        AddDefinition("justice.gold.record_1", "Goldene Ordnung I", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 100, 0, "Bessere GoldJustice-Anzeige.", false, "Gold-Gerechtigkeit 1.");
        AddDefinition("justice.gold.record_2", "Goldene Ordnung II", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 200, 0, "Result-Details fuer GoldJustice.", false, "Gold-Gerechtigkeit 3.");
        AddDefinition("justice.gold.safe_bonus", "Sicherer Ueberschuss", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 350, 0, "Sichere Runs geben etwas Kernwissen.", false, "Boss ohne Chaos.");
        AddDefinition("justice.gold.loadout", "Ordnungsvorrat", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 500, 0, "Kleiner Startgold-Bonus als Loadout-Bonus.", 1, false, "5 Justice-Choices.");
        AddDefinition("justice.xp.record_1", "Lernende Ordnung I", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 100, 0, "Bessere XPJustice-Anzeige.", false, "XP-Gerechtigkeit 1.");
        AddDefinition("justice.xp.record_2", "Lernende Ordnung II", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 200, 0, "Result-Details fuer XPJustice.", false, "XP-Gerechtigkeit 3.");
        AddDefinition("justice.xp.safe_bonus", "Sichere Ausbildung", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 350, 0, "Sichere Runs geben etwas Tower-Mastery-XP.", false, "Boss ohne Chaos.");
        AddDefinition("justice.xp.loadout", "Ordnungslehre", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 500, 0, "Kleiner XP-Startbonus als Loadout-Bonus.", 1, false, "5 Justice-Choices.");
        AddDefinition("justice.balance_tracker", "Balance-Anzeige", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 180, 0, "Bessere Runstil-Anzeige.", false, "Chaos + Justice gewaehlt.");
        AddDefinition("justice.safe_path_rewards", "Stabiler Pfad", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 300, 0, "Sichere Runs werden leicht belohnt.", false, "3 Runs ohne Chaos bis Boss.");
        AddDefinition("justice.mixed_path", "Gemischter Pfad", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 400, 0, "Hybrid-Ziele werden sichtbar.", false, "Chaos + Justice im Run.");
        AddDefinition("justice.archivist", "Ordnungschronik", ChaosResearchCategory.JusticeOrder, ChaosResearchNodeKind.JusticeOrder, 250, 0, "Lexikon erweitert den Ordnungspfad.", false, "10 Justice-Choices.");
    }

    private void AddDefinition(string nodeId, string displayName, ChaosResearchCategory category, ChaosResearchNodeKind kind, int chaosCost, int riftCost, string effectText, bool unlockedByDefault = false, string requirementText = "")
    {
        AddDefinition(nodeId, displayName, category, kind, chaosCost, riftCost, effectText, 0, unlockedByDefault, requirementText);
    }

    private void AddDefinition(string nodeId, string displayName, ChaosResearchCategory category, ChaosResearchNodeKind kind, int chaosCost, int riftCost, string effectText, int slotCost, bool unlockedByDefault = false, string requirementText = "")
    {
        definitions.Add(new ChaosResearchNodeDefinition
        {
            nodeId = nodeId,
            displayName = displayName,
            category = category,
            kind = kind,
            chaosKnowledgeCost = Mathf.Max(0, chaosCost),
            riftCoreCost = Mathf.Max(0, riftCost),
            slotCost = Mathf.Max(0, slotCost),
            unlockedByDefault = unlockedByDefault,
            effectText = effectText,
            requirementText = requirementText
        });
    }

    private void DeactivateExclusiveLoadoutGroup(ChaosResearchNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);

        foreach (ChaosResearchNodeDefinition definition in definitions)
        {
            if (definition == null || definition.nodeId == activatedDefinition.nodeId || !definition.RequiresLoadoutSlot())
                continue;

            if (GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            ChaosResearchNodeState state = GetNodeState(definition.nodeId);
            if (state != null)
                state.active = false;
        }
    }

    private int GetActiveSlotsInExclusiveGroup(ChaosResearchNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return 0;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);
        int slots = 0;

        foreach (ChaosResearchNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            ChaosResearchNodeState state = GetNodeState(definition.nodeId);
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

    private string FormatCost(ChaosResearchNodeDefinition definition)
    {
        if (definition == null)
            return "0 CW";

        string text = definition.chaosKnowledgeCost + " CW";
        if (definition.riftCoreCost > 0)
            text += " + " + definition.riftCoreCost + " RK";

        return text;
    }

    private int GetChaosVariantKills(EnemyRole role)
    {
        int value;
        return chaosVariantKillsByRole.TryGetValue(role, out value) ? Mathf.Max(0, value) : 0;
    }

    private int GetChaosWaveBlockCompletions(ChaosWaveBlockType blockType)
    {
        int value;
        return chaosWaveBlockCompletionsByType.TryGetValue(blockType, out value) ? Mathf.Max(0, value) : 0;
    }

    private int GetRiskModifierSeenCount()
    {
        return riskModifierKeysSeen.Count;
    }

    private int GetHighestChaosLevelInCurrentHistoryBefore(WaveCompletionResult currentResult)
    {
        WaveHistory history = GetWaveHistory();

        if (history == null || history.completedWaves == null)
            return 0;

        int highest = 0;
        foreach (WaveCompletionResult result in history.completedWaves)
        {
            if (result == null || result == currentResult)
                continue;

            if (result.waveNumber >= currentResult.waveNumber)
                continue;

            highest = Mathf.Max(highest, result.chaosLevelAtWaveStart);
        }

        return highest;
    }

    private int GetTotalChaosChoicesIncludingCurrentRun()
    {
        ChaosJusticeManager chaosJustice = GetChaosJusticeManager();
        int currentRun = chaosJustice != null ? Mathf.Max(0, chaosJustice.GetChaosChoiceCount() - baselineChaosChoiceCount) : 0;
        return totalChaosChoicesEver + currentRun;
    }

    private int GetTotalJusticeChoicesIncludingCurrentRun()
    {
        ChaosJusticeManager chaosJustice = GetChaosJusticeManager();
        int currentRun = chaosJustice != null ? Mathf.Max(0, chaosJustice.GetJusticeChoiceCount() - baselineJusticeChoiceCount) : 0;
        return totalJusticeChoicesEver + currentRun;
    }

    private int GetTotalNoModifierChoicesIncludingCurrentRun()
    {
        ChaosJusticeManager chaosJustice = GetChaosJusticeManager();
        int currentRun = chaosJustice != null ? Mathf.Max(0, chaosJustice.GetNoModifierChoiceCount() - baselineNoModifierChoiceCount) : 0;
        return totalNoModifierChoicesEver + currentRun;
    }

    private int GetTotalBossKillsIncludingCurrentHistory()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? Mathf.Max(totalBossKillsAtChaosTwoPlusEver, history.GetBossKills()) : totalBossKillsAtChaosTwoPlusEver;
    }

    private int GetTotalMiniBossKillsIncludingCurrentHistory()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? Mathf.Max(totalMiniBossKillsEver, history.GetMiniBossWavesCompleted()) : totalMiniBossKillsEver;
    }

    private int GetTotalEliteKillsIncludingCurrentHistory()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetEliteKills() : 0;
    }

    private bool HasSeenScenario(WaveScenario scenario)
    {
        WaveHistory history = GetWaveHistory();

        if (history == null || history.completedWaves == null)
            return false;

        foreach (WaveCompletionResult result in history.completedWaves)
        {
            if (result != null && result.scenario == scenario && result.waveCompleted)
                return true;
        }

        return false;
    }

    private bool HasSeenRole(EnemyRole role)
    {
        WaveHistory history = GetWaveHistory();

        if (history == null || history.completedWaves == null)
            return false;

        foreach (WaveCompletionResult result in history.completedWaves)
        {
            if (result == null)
                continue;

            if (result.GetKilledRoleCount(role) > 0 || result.GetLeakedRoleCount(role) > 0)
                return true;
        }

        return false;
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

    private ChaosJusticeManager GetChaosJusticeManager()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetChaosJusticeManager() : FindObjectOfType<ChaosJusticeManager>();
    }

    private GameManager GetGameManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager;
    }

    private bool IsMetaProgressionSuppressedForCurrentRun()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null && currentGameManager.IsMetaProgressionSuppressedForCurrentRun();
    }

    private void LoadProfile()
    {
        EnsureStates();
        LoadRuntimeCollections();

        chaosKnowledge = PlayerPrefs.GetInt(PlayerPrefsPrefix + "ChaosKnowledge", chaosKnowledge);
        riftCores = PlayerPrefs.GetInt(PlayerPrefsPrefix + "RiftCores", riftCores);
        highestChaosLevelEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestChaosLevelEver", highestChaosLevelEver);
        highestGoldJusticeEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestGoldJusticeEver", highestGoldJusticeEver);
        highestXpJusticeEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestXpJusticeEver", highestXpJusticeEver);
        totalChaosChoicesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosChoicesEver", totalChaosChoicesEver);
        totalJusticeChoicesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalJusticeChoicesEver", totalJusticeChoicesEver);
        totalNoModifierChoicesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalNoModifierChoicesEver", totalNoModifierChoicesEver);
        totalChaosWavesCompletedEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosWavesCompletedEver", totalChaosWavesCompletedEver);
        totalChaosVariantKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosVariantKillsEver", totalChaosVariantKillsEver);
        totalChaosWaveBlockWavesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosWaveBlockWavesEver", totalChaosWaveBlockWavesEver);
        totalChaos5WavesCompletedEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaos5WavesCompletedEver", totalChaos5WavesCompletedEver);
        totalChaos5BossKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaos5BossKillsEver", totalChaos5BossKillsEver);
        totalBossKillsAtChaosTwoPlusEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBossKillsAtChaosTwoPlusEver", totalBossKillsAtChaosTwoPlusEver);
        totalSafeBossWithoutChaosEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalSafeBossWithoutChaosEver", totalSafeBossWithoutChaosEver);
        totalRiskModifiersSeenEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalRiskModifiersSeenEver", totalRiskModifiersSeenEver);
        totalMiniBossKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalMiniBossKillsEver", totalMiniBossKillsEver);
        highestWaveEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        lastRunChaosKnowledgeGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaosKnowledge", lastRunChaosKnowledgeGained);
        lastRunRiftCoresGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunRiftCores", lastRunRiftCoresGained);
        lastRunChaosChoices = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaosChoices", lastRunChaosChoices);
        lastRunChaosWavesCompleted = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaosWavesCompleted", lastRunChaosWavesCompleted);
        lastRunChaosVariantKills = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaosVariantKills", lastRunChaosVariantKills);
        lastRunChaosWaveBlockWaves = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaosWaveBlockWaves", lastRunChaosWaveBlockWaves);
        lastRunChaos5WavesCompleted = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaos5WavesCompleted", lastRunChaos5WavesCompleted);
        lastRunChaos5BossKills = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaos5BossKills", lastRunChaos5BossKills);

        foreach (ChaosResearchNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            ChaosResearchNodeState state = GetNodeState(definition.nodeId);
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

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "ChaosKnowledge", chaosKnowledge);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "RiftCores", riftCores);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestChaosLevelEver", highestChaosLevelEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestGoldJusticeEver", highestGoldJusticeEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestXpJusticeEver", highestXpJusticeEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosChoicesEver", totalChaosChoicesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalJusticeChoicesEver", totalJusticeChoicesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalNoModifierChoicesEver", totalNoModifierChoicesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosWavesCompletedEver", totalChaosWavesCompletedEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosVariantKillsEver", totalChaosVariantKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosWaveBlockWavesEver", totalChaosWaveBlockWavesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaos5WavesCompletedEver", totalChaos5WavesCompletedEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaos5BossKillsEver", totalChaos5BossKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBossKillsAtChaosTwoPlusEver", totalBossKillsAtChaosTwoPlusEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalSafeBossWithoutChaosEver", totalSafeBossWithoutChaosEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalRiskModifiersSeenEver", totalRiskModifiersSeenEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalMiniBossKillsEver", totalMiniBossKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaosKnowledge", lastRunChaosKnowledgeGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunRiftCores", lastRunRiftCoresGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaosChoices", lastRunChaosChoices);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaosWavesCompleted", lastRunChaosWavesCompleted);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaosVariantKills", lastRunChaosVariantKills);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaosWaveBlockWaves", lastRunChaosWaveBlockWaves);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaos5WavesCompleted", lastRunChaos5WavesCompleted);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaos5BossKills", lastRunChaos5BossKills);

        foreach (ChaosResearchNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Purchased", state.purchased ? 1 : 0);
            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Active", state.active ? 1 : 0);
        }

        SaveRuntimeCollections();
        PlayerPrefs.Save();
    }

    private void LoadRuntimeCollections()
    {
        chaosVariantKillsByRole.Clear();
        chaosWaveBlockCompletionsByType.Clear();
        firstChaosVariantRolesClaimed.Clear();
        firstChaosWaveBlocksClaimed.Clear();

        foreach (EnemyRole role in System.Enum.GetValues(typeof(EnemyRole)))
        {
            int value = PlayerPrefs.GetInt(PlayerPrefsPrefix + "VariantKills." + role, 0);
            if (value > 0)
                chaosVariantKillsByRole[role] = value;

            if (PlayerPrefs.GetInt(PlayerPrefsPrefix + "VariantRoleClaimed." + role, 0) == 1)
                firstChaosVariantRolesClaimed.Add(role.ToString());
        }

        foreach (ChaosWaveBlockType blockType in System.Enum.GetValues(typeof(ChaosWaveBlockType)))
        {
            int value = PlayerPrefs.GetInt(PlayerPrefsPrefix + "BlockCompletions." + blockType, 0);
            if (value > 0)
                chaosWaveBlockCompletionsByType[blockType] = value;

            if (PlayerPrefs.GetInt(PlayerPrefsPrefix + "BlockClaimed." + blockType, 0) == 1)
                firstChaosWaveBlocksClaimed.Add(blockType.ToString());
        }

        LoadStringSet(PlayerPrefsPrefix + "RiskKeysSeen", riskModifierKeysSeen);
        LoadStringSet(PlayerPrefsPrefix + "RiskLevelTwoPlusKeysSeen", riskLevelTwoPlusKeysSeen);
    }

    private void SaveRuntimeCollections()
    {
        foreach (EnemyRole role in System.Enum.GetValues(typeof(EnemyRole)))
        {
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "VariantKills." + role, GetChaosVariantKills(role));
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "VariantRoleClaimed." + role, firstChaosVariantRolesClaimed.Contains(role.ToString()) ? 1 : 0);
        }

        foreach (ChaosWaveBlockType blockType in System.Enum.GetValues(typeof(ChaosWaveBlockType)))
        {
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "BlockCompletions." + blockType, GetChaosWaveBlockCompletions(blockType));
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "BlockClaimed." + blockType, firstChaosWaveBlocksClaimed.Contains(blockType.ToString()) ? 1 : 0);
        }

        SaveStringSet(PlayerPrefsPrefix + "RiskKeysSeen", riskModifierKeysSeen);
        SaveStringSet(PlayerPrefsPrefix + "RiskLevelTwoPlusKeysSeen", riskLevelTwoPlusKeysSeen);
    }

    private void LoadStringSet(string key, HashSet<string> target)
    {
        target.Clear();
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

    private void SaveStringSet(string key, HashSet<string> source)
    {
        if (source == null || source.Count == 0)
        {
            PlayerPrefs.SetString(key, "");
            return;
        }

        StringBuilder builder = new StringBuilder();
        foreach (string value in source)
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
