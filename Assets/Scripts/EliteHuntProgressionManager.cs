using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum EliteHuntCategory
{
    Overview,
    Contracts,
    Affixes,
    Rewards,
    Frequency,
    Counters,
    RiftElite
}

public enum EliteHuntNodeKind
{
    Core,
    Contract,
    Affix,
    Reward,
    Frequency,
    InfoCounter,
    PowerCounter,
    RiftElite
}

public enum EliteHuntMode
{
    Off = 0,
    Light = 1,
    Normal = 2,
    Hard = 3,
    RiftElite = 4
}

[System.Serializable]
public class EliteHuntNodeDefinition
{
    public string nodeId;
    public string displayName;
    public EliteHuntCategory category;
    public EliteHuntNodeKind kind;
    public int sealCost;
    public int riftCoreCost;
    public int blueprintCost;
    public int requiredEliteRank;
    public int slotCost;
    public bool unlockedByDefault;
    public string effectText;
    public string requirementText;

    public bool RequiresLoadoutSlot()
    {
        return slotCost > 0 && (kind == EliteHuntNodeKind.Reward || kind == EliteHuntNodeKind.Frequency || kind == EliteHuntNodeKind.PowerCounter || kind == EliteHuntNodeKind.RiftElite);
    }
}

[System.Serializable]
public class EliteHuntNodeState
{
    public string nodeId;
    public bool purchased;
    public bool active;
}

public class EliteHuntProgressionManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_EliteHunt_";

    public static EliteHuntProgressionManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Progression")]
    public int eliteRank = 1;
    public int eliteRankXP = 0;
    public int eliteSeals = 0;
    public EliteHuntMode activeHuntMode = EliteHuntMode.Off;

    [Header("Loadout")]
    public int baseEliteLoadoutSlots = 2;
    public int maxEliteLoadoutSlots = 6;

    [Header("Elite Rewards")]
    public int eliteSeenXP = 10;
    public int eliteKillXP = 50;
    public int eliteMiniBossKillXP = 100;
    public int eliteBossKillXP = 200;
    public int eliteChaosFiveBonusXP = 200;
    public int firstEliteRoleXP = 150;
    public int firstEliteAffixXP = 150;
    public int lightSealCap = 3;
    public int normalSealCap = 6;
    public int hardSealCap = 10;
    public int riftSealCap = 14;

    [Header("Persistent Progress")]
    public int totalElitesSeenEver = 0;
    public int totalEliteKillsEver = 0;
    public int totalEliteLeaksEver = 0;
    public int totalEliteBossKillsEver = 0;
    public int totalEliteMiniBossKillsEver = 0;
    public int totalChaosEliteKillsEver = 0;
    public int totalChaosFiveEliteKillsEver = 0;
    public int totalRiftEliteKillsEver = 0;
    public int totalEliteNoLeakWavesEver = 0;
    public int totalEliteAfterBlockKillsEver = 0;
    public int totalEliteContractsCompletedEver = 0;
    public int highestWaveEver = 0;
    public int highestChaosLevelEver = 0;

    [Header("Last Run")]
    public int lastRunEliteSealsGained = 0;
    public int lastRunEliteRankXPGained = 0;
    public int lastRunElitesSeen = 0;
    public int lastRunEliteKills = 0;
    public int lastRunEliteLeaks = 0;
    public int lastRunChaosEliteKills = 0;
    public int lastRunRiftEliteKills = 0;

    [Header("Definitions / State")]
    public List<EliteHuntNodeDefinition> definitions = new List<EliteHuntNodeDefinition>();
    public List<EliteHuntNodeState> nodeStates = new List<EliteHuntNodeState>();

    private readonly Dictionary<string, EliteHuntNodeDefinition> definitionById = new Dictionary<string, EliteHuntNodeDefinition>();
    private readonly Dictionary<string, EliteHuntNodeState> stateById = new Dictionary<string, EliteHuntNodeState>();
    private readonly HashSet<string> firstEliteRolesClaimed = new HashSet<string>();
    private readonly HashSet<string> firstEliteAffixesClaimed = new HashSet<string>();

    private int pendingRunEliteSeals = 0;
    private int pendingRunEliteRankXP = 0;
    private int highestWaveThisRun = 0;
    private int highestChaosThisRun = 0;
    private bool firstEliteKillSealAwardedThisRun = false;
    private bool eliteMiniBossBonusAwardedThisRun = false;
    private bool eliteBossBonusAwardedThisRun = false;
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

    public static EliteHuntProgressionManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        EliteHuntProgressionManager existing = FindObjectOfType<EliteHuntProgressionManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("EliteHuntProgressionSystem");
        EliteHuntProgressionManager manager = systemObject.AddComponent<EliteHuntProgressionManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public void StartNewRun()
    {
        LoadProfile();

        pendingRunEliteSeals = 0;
        pendingRunEliteRankXP = 0;
        highestWaveThisRun = 0;
        highestChaosThisRun = 0;
        firstEliteKillSealAwardedThisRun = false;
        eliteMiniBossBonusAwardedThisRun = false;
        eliteBossBonusAwardedThisRun = false;
        lastRunEliteSealsGained = 0;
        lastRunEliteRankXPGained = 0;
        lastRunElitesSeen = 0;
        lastRunEliteKills = 0;
        lastRunEliteLeaks = 0;
        lastRunChaosEliteKills = 0;
        lastRunRiftEliteKills = 0;
        runFinalized = false;

        ApplyRunSettingsToEnemySpawner(GetEnemySpawner());
    }

    public void FinalizeRun()
    {
        if (runFinalized)
            return;

        runFinalized = true;
        lastRunEliteSealsGained = Mathf.Max(0, pendingRunEliteSeals);
        lastRunEliteRankXPGained = Mathf.Max(0, pendingRunEliteRankXP);

        eliteSeals += lastRunEliteSealsGained;
        eliteRankXP += lastRunEliteRankXPGained;
        eliteRank = CalculateEliteRank(eliteRankXP);
        highestWaveEver = Mathf.Max(highestWaveEver, highestWaveThisRun);
        highestChaosLevelEver = Mathf.Max(highestChaosLevelEver, highestChaosThisRun);
        SaveProfile();
    }

    public void ApplyRunSettingsToEnemySpawner(EnemySpawner spawner)
    {
        if (spawner == null)
            return;

        bool eliteEnabled = IsEliteHuntUnlocked() && activeHuntMode != EliteHuntMode.Off;
        spawner.enableEliteWavesV1 = eliteEnabled;

        if (!eliteEnabled)
        {
            spawner.pendingEliteWaveNumber = 0;
            return;
        }

        switch (activeHuntMode)
        {
            case EliteHuntMode.Light:
                spawner.eliteStartWave = 18;
                spawner.eliteRareChanceStartWave = 18;
                spawner.eliteMinimumWavesBetween = 7;
                spawner.eliteMinimumTowerCount = 3;
                spawner.eliteKillsSinceLastThreshold = 120;
                spawner.eliteConditionSpawnChance = 0.016f;
                spawner.eliteRareChanceWithoutThreshold = 0.001f;
                break;
            case EliteHuntMode.Hard:
                spawner.eliteStartWave = 18;
                spawner.eliteRareChanceStartWave = 18;
                spawner.eliteMinimumWavesBetween = 4;
                spawner.eliteMinimumTowerCount = 3;
                spawner.eliteKillsSinceLastThreshold = 80;
                spawner.eliteConditionSpawnChance = 0.02f;
                spawner.eliteRareChanceWithoutThreshold = 0.002f;
                break;
            case EliteHuntMode.RiftElite:
                spawner.eliteStartWave = 18;
                spawner.eliteRareChanceStartWave = 18;
                spawner.eliteMinimumWavesBetween = 3;
                spawner.eliteMinimumTowerCount = 3;
                spawner.eliteKillsSinceLastThreshold = 60;
                spawner.eliteConditionSpawnChance = 0.02f;
                spawner.eliteRareChanceWithoutThreshold = 0.002f;
                break;
            default:
                spawner.eliteStartWave = 18;
                spawner.eliteRareChanceStartWave = 18;
                spawner.eliteMinimumWavesBetween = 5;
                spawner.eliteMinimumTowerCount = 3;
                spawner.eliteKillsSinceLastThreshold = 100;
                spawner.eliteConditionSpawnChance = 0.02f;
                spawner.eliteRareChanceWithoutThreshold = 0.002f;
                break;
        }
    }

    public List<EliteHuntNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public EliteHuntNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        EliteHuntNodeDefinition definition;
        return definitionById.TryGetValue(nodeId, out definition) ? definition : null;
    }

    public EliteHuntNodeState GetNodeState(string nodeId)
    {
        EnsureStates();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        EliteHuntNodeState state;
        return stateById.TryGetValue(nodeId, out state) ? state : null;
    }

    public bool IsNodePurchased(string nodeId)
    {
        EliteHuntNodeState state = GetNodeState(nodeId);
        return state != null && state.purchased;
    }

    public bool IsNodeActive(string nodeId)
    {
        EliteHuntNodeDefinition definition = GetDefinition(nodeId);
        EliteHuntNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased)
            return false;

        if (IsHuntModeNode(nodeId))
            return GetModeForNode(nodeId) == activeHuntMode;

        return definition.RequiresLoadoutSlot() ? state.active : true;
    }

    public bool IsEliteHuntUnlocked()
    {
        return IsNodePurchased("elite.core.unlock");
    }

    public bool IsRiftEliteVisible()
    {
        return highestChaosLevelEver >= 5 || totalChaosFiveEliteKillsEver > 0 || IsNodePurchased("elite.rift.unlock");
    }

    public bool CanPurchaseNode(string nodeId)
    {
        EliteHuntNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return false;

        EliteHuntNodeState state = GetNodeState(nodeId);
        if (state != null && state.purchased)
            return false;

        if (eliteRank < definition.requiredEliteRank)
            return false;

        if (eliteSeals < definition.sealCost)
            return false;

        if (GetAvailableRiftCores() < definition.riftCoreCost)
            return false;

        if (GetAvailableBlueprints() < definition.blueprintCost)
            return false;

        return AreSpecialRequirementsMet(definition);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        if (!CanPurchaseNode(nodeId))
            return false;

        EliteHuntNodeDefinition definition = GetDefinition(nodeId);
        EliteHuntNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null)
            return false;

        if (!TrySpendExternalCurrencies(definition))
            return false;

        eliteSeals -= Mathf.Max(0, definition.sealCost);
        state.purchased = true;
        state.active = !definition.RequiresLoadoutSlot();

        if (definition.nodeId == "elite.core.unlock" && IsNodePurchased("elite.frequency.unlock_light"))
            activeHuntMode = EliteHuntMode.Light;

        SaveProfile();
        return true;
    }

    public bool CanActivateNode(string nodeId)
    {
        EliteHuntNodeDefinition definition = GetDefinition(nodeId);
        EliteHuntNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased || state.active || !definition.RequiresLoadoutSlot())
            return false;

        int usedSlotsAfterReplacingGroup = GetUsedLoadoutSlots() - GetActiveSlotsInExclusiveGroup(definition);
        return usedSlotsAfterReplacingGroup + definition.slotCost <= GetLoadoutSlotCapacity();
    }

    public bool TryActivateNode(string nodeId)
    {
        if (!CanActivateNode(nodeId))
            return false;

        EliteHuntNodeDefinition definition = GetDefinition(nodeId);
        EliteHuntNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null)
            return false;

        DeactivateExclusiveLoadoutGroup(definition);
        state.active = true;
        SaveProfile();
        return true;
    }

    public bool TryDeactivateNode(string nodeId)
    {
        EliteHuntNodeDefinition definition = GetDefinition(nodeId);
        EliteHuntNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !definition.RequiresLoadoutSlot() || !state.active)
            return false;

        state.active = false;
        SaveProfile();
        return true;
    }

    public bool CanActivateHuntModeNode(string nodeId)
    {
        if (!IsHuntModeNode(nodeId))
            return false;

        if (!IsEliteHuntUnlocked() || !IsNodePurchased(nodeId))
            return false;

        EliteHuntMode mode = GetModeForNode(nodeId);
        return mode != EliteHuntMode.Off && activeHuntMode != mode;
    }

    public bool TryActivateHuntModeNode(string nodeId)
    {
        if (!CanActivateHuntModeNode(nodeId))
            return false;

        activeHuntMode = GetModeForNode(nodeId);
        SaveProfile();
        ApplyRunSettingsToEnemySpawner(GetEnemySpawner());
        return true;
    }

    public bool TryDeactivateHuntMode()
    {
        if (activeHuntMode == EliteHuntMode.Off)
            return false;

        activeHuntMode = EliteHuntMode.Off;
        SaveProfile();
        ApplyRunSettingsToEnemySpawner(GetEnemySpawner());
        return true;
    }

    public bool IsHuntModeNode(string nodeId)
    {
        return nodeId == "elite.frequency.unlock_light" ||
               nodeId == "elite.frequency.normal" ||
               nodeId == "elite.frequency.hard" ||
               nodeId == "elite.frequency.rift";
    }

    public EliteHuntMode GetModeForNode(string nodeId)
    {
        switch (nodeId)
        {
            case "elite.frequency.unlock_light": return EliteHuntMode.Light;
            case "elite.frequency.normal": return EliteHuntMode.Normal;
            case "elite.frequency.hard": return EliteHuntMode.Hard;
            case "elite.frequency.rift": return EliteHuntMode.RiftElite;
            default: return EliteHuntMode.Off;
        }
    }

    public int GetLoadoutSlotCapacity()
    {
        int slots = Mathf.Max(0, baseEliteLoadoutSlots);

        if (eliteRank >= 5)
            slots++;

        if (eliteRank >= 12)
            slots++;

        if (eliteRank >= 20)
            slots++;

        if (eliteRank >= 35)
            slots++;

        return Mathf.Clamp(slots, 0, Mathf.Max(baseEliteLoadoutSlots, maxEliteLoadoutSlots));
    }

    public int GetUsedLoadoutSlots()
    {
        EnsureStates();
        int used = 0;

        foreach (EliteHuntNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot())
                continue;

            EliteHuntNodeState state = GetNodeState(definition.nodeId);
            if (state != null && state.purchased && state.active)
                used += Mathf.Max(0, definition.slotCost);
        }

        return used;
    }

    public int GetAvailableLoadoutSlots()
    {
        return Mathf.Max(0, GetLoadoutSlotCapacity() - GetUsedLoadoutSlots());
    }

    public int GetXPToNextEliteRank()
    {
        return GetXPToNextEliteRank(eliteRank);
    }

    public int GetXPIntoCurrentRank()
    {
        return GetXPIntoRank(eliteRankXP);
    }

    public string GetTopBarSummary()
    {
        return "Elite Rang " + eliteRank +
               " | Siegel " + eliteSeals +
               " | Modus " + GetHuntModeDisplayName(activeHuntMode) +
               " | Loadout " + GetUsedLoadoutSlots() + "/" + GetLoadoutSlotCapacity();
    }

    public string GetOverviewText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>Elite-Jagd</b>");
        builder.AppendLine("Status: " + (IsEliteHuntUnlocked() ? "Freigeschaltet" : GetEliteUnlockStateText()));
        builder.AppendLine("Elite-Jagd: " + GetHuntModeDisplayName(activeHuntMode));
        builder.AppendLine("Elite-Rang: " + eliteRank);
        builder.AppendLine("Elite-Rang-XP: " + GetXPIntoCurrentRank() + " / " + GetXPToNextEliteRank());
        builder.AppendLine("Elite-Siegel: " + eliteSeals);
        builder.AppendLine("Elite-Loadout: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
        builder.AppendLine();
        builder.AppendLine("Elite-Kills: " + totalEliteKillsEver + " | Elite-Leaks: " + totalEliteLeaksEver);
        builder.AppendLine("Chaos-Elite-Kills: " + totalChaosEliteKillsEver + " | Riss-Elite-Kills: " + totalRiftEliteKillsEver);
        builder.AppendLine("Elite nach Verbau: " + totalEliteAfterBlockKillsEver);
        builder.AppendLine();
        builder.AppendLine("<b>Letzter Run</b>");
        builder.AppendLine("+ " + lastRunEliteSealsGained + " Elite-Siegel");
        builder.AppendLine("+ " + lastRunEliteRankXPGained + " Elite-Rang-XP");
        builder.AppendLine("Gesehen: " + lastRunElitesSeen + " | Besiegt: " + lastRunEliteKills + " | Geleakt: " + lastRunEliteLeaks);
        builder.AppendLine();
        builder.AppendLine("<b>Naechste Ziele</b>");
        AppendNextGoals(builder);
        builder.AppendLine();
        builder.AppendLine("Regel: Elite ist opt-in. Ohne aktiven Jagdmodus bleiben normale Runs frei von Elite-Jagd-Druck.");
        return builder.ToString();
    }

    public string GetNodeStateText(EliteHuntNodeDefinition definition)
    {
        if (definition == null)
            return "Unbekannt";

        EliteHuntNodeState state = GetNodeState(definition.nodeId);

        if (state != null && state.purchased)
        {
            if (IsHuntModeNode(definition.nodeId))
                return GetModeForNode(definition.nodeId) == activeHuntMode ? "Aktiv | " + GetHuntModeDisplayName(activeHuntMode) : "Freigeschaltet | Modus";

            if (definition.RequiresLoadoutSlot())
                return state.active ? "Aktiv | Slot " + definition.slotCost : "Gekauft | Slot " + definition.slotCost;

            return "Freigeschaltet";
        }

        if (CanPurchaseNode(definition.nodeId))
            return "Kaufbar | " + FormatCost(definition);

        if (eliteRank < definition.requiredEliteRank)
            return "Gesperrt | Elite-Rang " + definition.requiredEliteRank;

        if (!AreSpecialRequirementsMet(definition))
            return "Gesperrt | Bedingung";

        if (eliteSeals < definition.sealCost || GetAvailableRiftCores() < definition.riftCoreCost || GetAvailableBlueprints() < definition.blueprintCost)
            return "Ressourcen fehlen | " + FormatCost(definition);

        return "Gesperrt";
    }

    public string GetNodeDetailText(string nodeId)
    {
        EliteHuntNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return "Elite-Jagd-Knoten nicht gefunden.";

        EliteHuntNodeState state = GetNodeState(nodeId);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>" + definition.displayName + "</b>");
        builder.AppendLine("<size=90%><color=#B9C2D0>" + GetNodeStateText(definition) + "</color></size>");
        builder.AppendLine();
        builder.AppendLine("Kategorie: " + GetCategoryDisplayName(definition.category));
        builder.AppendLine("Typ: " + GetKindDisplayName(definition.kind));
        builder.AppendLine("Kosten: " + FormatCost(definition));
        builder.AppendLine("Elite-Rang: " + definition.requiredEliteRank);

        if (definition.RequiresLoadoutSlot())
        {
            builder.AppendLine("Slot-Kosten: " + definition.slotCost);
            builder.AppendLine("Elite-Loadout: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
        }

        if (IsHuntModeNode(definition.nodeId))
            builder.AppendLine("Jagdmodus: " + GetHuntModeDisplayName(GetModeForNode(definition.nodeId)));

        if (!string.IsNullOrEmpty(definition.requirementText))
            builder.AppendLine("Bedingung: " + definition.requirementText);

        builder.AppendLine();
        builder.AppendLine("<b>Effekt</b>");
        builder.AppendLine(definition.effectText);
        builder.AppendLine();
        builder.AppendLine("<b>Fortschritt</b>");
        builder.AppendLine(GetRequirementProgressText(definition));

        if (state != null && state.purchased && definition.RequiresLoadoutSlot())
            builder.AppendLine(state.active ? "Aktiv fuer den naechsten Run." : "Gekauft, aber nicht im Elite-Loadout aktiv.");

        if (IsHuntModeNode(definition.nodeId) && state != null && state.purchased)
            builder.AppendLine(GetModeForNode(definition.nodeId) == activeHuntMode ? "Dieser Jagdmodus ist aktiv." : "Freigeschaltet. Kann als Elite-Jagd-Modus fuer den naechsten Run gewaehlt werden.");

        return builder.ToString();
    }

    public int GetPurchasedCount(EliteHuntCategory category)
    {
        int purchased = 0;

        foreach (EliteHuntNodeDefinition definition in GetDefinitions())
        {
            if (definition != null && definition.category == category && IsNodePurchased(definition.nodeId))
                purchased++;
        }

        return purchased;
    }

    public int GetDefinitionCount(EliteHuntCategory category)
    {
        int total = 0;

        foreach (EliteHuntNodeDefinition definition in GetDefinitions())
        {
            if (definition != null && definition.category == category)
                total++;
        }

        return total;
    }

    public static string GetCategoryDisplayName(EliteHuntCategory category)
    {
        switch (category)
        {
            case EliteHuntCategory.Overview: return "Uebersicht";
            case EliteHuntCategory.Contracts: return "Elite-Auftraege";
            case EliteHuntCategory.Affixes: return "Elite-Affixe";
            case EliteHuntCategory.Rewards: return "Elite-Belohnungen";
            case EliteHuntCategory.Frequency: return "Elite-Haeufigkeit";
            case EliteHuntCategory.Counters: return "Elite-Konter";
            case EliteHuntCategory.RiftElite: return "Riss-Elite";
            default: return category.ToString();
        }
    }

    public static string GetKindDisplayName(EliteHuntNodeKind kind)
    {
        switch (kind)
        {
            case EliteHuntNodeKind.Core: return "Basis / Uebersicht";
            case EliteHuntNodeKind.Contract: return "Elite-Auftrag";
            case EliteHuntNodeKind.Affix: return "Elite-Affix";
            case EliteHuntNodeKind.Reward: return "Elite-Belohnung";
            case EliteHuntNodeKind.Frequency: return "Elite-Haeufigkeit";
            case EliteHuntNodeKind.InfoCounter: return "Info-Konter";
            case EliteHuntNodeKind.PowerCounter: return "Power-Konter";
            case EliteHuntNodeKind.RiftElite: return "Riss-Elite";
            default: return kind.ToString();
        }
    }

    public static string GetHuntModeDisplayName(EliteHuntMode mode)
    {
        switch (mode)
        {
            case EliteHuntMode.Light: return "Leicht";
            case EliteHuntMode.Normal: return "Normal";
            case EliteHuntMode.Hard: return "Hart";
            case EliteHuntMode.RiftElite: return "Riss-Elite";
            default: return "Aus";
        }
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || runFinalized)
            return;

        highestWaveThisRun = Mathf.Max(highestWaveThisRun, result.waveNumber);
        highestChaosThisRun = Mathf.Max(highestChaosThisRun, result.chaosLevelAtWaveStart);
        highestWaveEver = Mathf.Max(highestWaveEver, result.waveNumber);
        highestChaosLevelEver = Mathf.Max(highestChaosLevelEver, result.chaosLevelAtWaveStart);

        if (!result.isEliteWave)
            return;

        lastRunElitesSeen++;
        totalElitesSeenEver++;
        AddEliteRankXP(eliteSeenXP);

        if (result.eliteReachedBase)
        {
            lastRunEliteLeaks++;
            totalEliteLeaksEver++;
        }

        if (!result.eliteDefeated)
            return;

        lastRunEliteKills++;
        totalEliteKillsEver++;
        AddEliteRankXP(eliteKillXP);
        AwardFirstEliteRoleXP("Elite");

        if (!firstEliteKillSealAwardedThisRun)
        {
            firstEliteKillSealAwardedThisRun = true;
            AwardEliteSeals(1);

            if (IsNodeActive("elite.reward.seal_1"))
                AddEliteRankXP(10);
        }

        if (result.enemiesReachedBase <= 0 && result.baseDamageTaken <= 0)
        {
            totalEliteNoLeakWavesEver++;
            AwardEliteSeals(1);
        }

        if (result.chaosLevelAtWaveStart >= 3)
        {
            lastRunChaosEliteKills++;
            totalChaosEliteKillsEver++;
            AwardEliteSeals(1);
        }

        if (result.chaosLevelAtWaveStart >= 5)
        {
            totalChaosFiveEliteKillsEver++;
            AddEliteRankXP(eliteChaosFiveBonusXP);
            AwardEliteSeals(2);
        }

        if (activeHuntMode == EliteHuntMode.RiftElite || result.chaosLevelAtWaveStart >= 5)
        {
            lastRunRiftEliteKills++;
            totalRiftEliteKillsEver++;
        }

        if ((result.isMiniBossWave || result.miniBossDefeated) && !eliteMiniBossBonusAwardedThisRun)
        {
            eliteMiniBossBonusAwardedThisRun = true;
            totalEliteMiniBossKillsEver++;
            AddEliteRankXP(eliteMiniBossKillXP);
            AwardEliteSeals(2);

            if (IsNodeActive("elite.reward.seal_2"))
                AwardEliteSeals(1);
        }

        if ((result.isBossWave || result.bossDefeated) && !eliteBossBonusAwardedThisRun)
        {
            eliteBossBonusAwardedThisRun = true;
            totalEliteBossKillsEver++;
            AddEliteRankXP(eliteBossKillXP);
            AwardEliteSeals(4);

            if (IsNodeActive("elite.reward.seal_3"))
                AwardEliteSeals(1);
        }

        if (DidEliteKillHappenAfterBlockedRecovery())
        {
            totalEliteAfterBlockKillsEver++;
            AwardEliteSeals(1);
        }

        SaveProfile();
    }

    private void HandleGameOverTriggered()
    {
        FinalizeRun();
    }

    private void AddEliteRankXP(int amount)
    {
        pendingRunEliteRankXP += Mathf.Max(0, amount);
    }

    private void AwardEliteSeals(int amount)
    {
        if (amount <= 0)
            return;

        int cap = GetSealCapForActiveMode();
        int grant = Mathf.Min(amount, Mathf.Max(0, cap - pendingRunEliteSeals));
        pendingRunEliteSeals += Mathf.Max(0, grant);
    }

    private int GetSealCapForActiveMode()
    {
        switch (activeHuntMode)
        {
            case EliteHuntMode.Light: return Mathf.Max(0, lightSealCap);
            case EliteHuntMode.Normal: return Mathf.Max(0, normalSealCap);
            case EliteHuntMode.Hard: return Mathf.Max(0, hardSealCap);
            case EliteHuntMode.RiftElite: return Mathf.Max(0, riftSealCap);
            default: return 0;
        }
    }

    private void AwardFirstEliteRoleXP(string roleKey)
    {
        if (string.IsNullOrEmpty(roleKey) || firstEliteRolesClaimed.Contains(roleKey))
            return;

        firstEliteRolesClaimed.Add(roleKey);
        AddEliteRankXP(firstEliteRoleXP);
    }

    private bool DidEliteKillHappenAfterBlockedRecovery()
    {
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        return pathTechnique != null && pathTechnique.lastRunBlockedCrises > 0 && pathTechnique.lastRunWavesAfterBlock >= 2;
    }

    private bool AreSpecialRequirementsMet(EliteHuntNodeDefinition definition)
    {
        if (definition == null)
            return false;

        string id = definition.nodeId;

        switch (id)
        {
            case "elite.core.teaser":
                return GetAccountLevel() >= 25 || highestWaveEver >= 25;
            case "elite.core.unlock":
                return IsEliteUnlockConditionMet();
            case "elite.core.result_summary":
                return GetTotalEliteSeenIncludingCurrentRun() > 0;
            case "elite.core.lexicon":
            case "elite.core.contracts":
            case "elite.frequency.unlock_light":
            case "elite.affix.unlock_basic":
                return IsEliteHuntUnlocked();
            case "elite.frequency.normal":
                return eliteRank >= 5 && IsEliteHuntUnlocked();
            case "elite.frequency.hard":
                return eliteRank >= 12 && GetTotalEliteKillsIncludingCurrentRun() >= 10;
            case "elite.frequency.rift":
            case "elite.rift.unlock":
                return highestChaosLevelEver >= 5 && eliteRank >= 20;
            case "elite.frequency.role_focus":
                return eliteRank >= 8;
            case "elite.frequency.affix_focus":
                return GetPurchasedCount(EliteHuntCategory.Affixes) >= 5;
            case "elite.frequency.no_boss_elite":
                return totalEliteBossKillsEver > 0 || totalEliteKillsEver >= 10;
            case "elite.frequency.safe_early":
                return totalElitesSeenEver >= 3;
            case "elite.affix.regen":
                return GetTotalEliteKillsIncludingCurrentRun() >= 3;
            case "elite.affix.resistant":
                return HasSeenChaosWaveBlock(ChaosWaveBlockType.Resistance);
            case "elite.affix.phasing":
                return eliteRank >= 5 && HasSeenScenario(WaveScenario.MageIntro);
            case "elite.affix.commander":
                return HasSeenScenario(WaveScenario.Mixed);
            case "elite.affix.guardian":
                return GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.affix.rusher":
                return GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.affix.splitting":
                return GetHighestWaveIncludingHistory() >= 25;
            case "elite.affix.magebound":
                return HasSeenScenario(WaveScenario.MageIntro) && GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.affix.learner_core":
                return HasSeenScenario(WaveScenario.EffectImmunity) && GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.affix.chaotic":
                return highestChaosLevelEver >= 5;
            case "elite.affix.volatile":
                return GetChaosFiveBossKills() > 0;
            case "elite.contract.runner_1":
                return IsEliteHuntUnlocked();
            case "elite.contract.tank_1":
            case "elite.contract.knight_1":
            case "elite.contract.no_leak":
                return GetTotalEliteSeenIncludingCurrentRun() > 0;
            case "elite.contract.mage_1":
                return HasSeenScenario(WaveScenario.MageIntro);
            case "elite.contract.learner_1":
                return HasSeenScenario(WaveScenario.EffectImmunity);
            case "elite.contract.allrounder_1":
                return HasSeenRole(EnemyRole.AllRounder) || GetHighestWaveIncludingHistory() >= 18;
            case "elite.contract.miniboss_1":
                return GetTotalMiniBossKills() >= 5;
            case "elite.contract.boss_1":
                return GetChaosFiveBossKills() > 0 || highestChaosLevelEver >= 3;
            case "elite.contract.chaos_1":
                return highestChaosLevelEver >= 3;
            case "elite.contract.chaos_5":
                return highestChaosLevelEver >= 5;
            case "elite.contract.after_block":
                return GetPathTechniqueProgressionManager() != null && GetPathTechniqueProgressionManager().pathTechniqueLevel >= 15;
            case "elite.contract.sniper":
                return IsGeneralTowerUnlocked(TowerRole.Sniper);
            case "elite.contract.combo":
                return (IsGeneralTowerUnlocked(TowerRole.Spike) || IsGeneralTowerUnlocked(TowerRole.Alchemist)) && IsGeneralTowerUnlocked(TowerRole.Fire) && IsGeneralTowerUnlocked(TowerRole.Poison);
            case "elite.reward.seal_1":
                return GetTotalEliteKillsIncludingCurrentRun() >= 3;
            case "elite.reward.seal_2":
                return eliteRank >= 5;
            case "elite.reward.seal_3":
                return eliteRank >= 12;
            case "elite.reward.mastery_1":
                return CountTowerMasteryMilestone(TowerMasteryMilestone.I) >= 3;
            case "elite.reward.mastery_2":
                return eliteRank >= 10;
            case "elite.reward.knowledge_1":
                return IsEliteHuntUnlocked();
            case "elite.reward.chaos_1":
                return totalChaosEliteKillsEver > 0 || lastRunChaosEliteKills > 0;
            case "elite.reward.path_1":
                return totalEliteAfterBlockKillsEver > 0;
            case "elite.reward.cosmetic_1":
                return GetTotalEliteKillsIncludingCurrentRun() >= 10;
            case "elite.reward.rift_core":
                return totalChaosFiveEliteKillsEver > 0 || totalRiftEliteKillsEver > 0;
            case "elite.info.marker":
                return GetTotalEliteSeenIncludingCurrentRun() > 0;
            case "elite.info.affix_preview":
                return totalElitesSeenEver + lastRunElitesSeen >= 3;
            case "elite.info.role_hint":
                return GetTotalEliteKillsIncludingCurrentRun() >= 3;
            case "elite.info.reward_preview":
            case "elite.info.history":
                return GetTotalEliteKillsIncludingCurrentRun() >= 5;
            case "elite.counter.runner_1":
            case "elite.counter.tank_1":
            case "elite.counter.knight_1":
                return GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.counter.mage_1":
                return HasSeenScenario(WaveScenario.MageIntro) && GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.counter.learner_1":
                return HasSeenScenario(WaveScenario.EffectImmunity) && GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.counter.allrounder_1":
                return HasSeenRole(EnemyRole.AllRounder) && GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.counter.boss_1":
                return totalEliteBossKillsEver > 0 || totalEliteKillsEver >= 10;
            case "elite.counter.affix_swift":
            case "elite.counter.affix_armored":
                return GetTotalEliteKillsIncludingCurrentRun() >= 1;
            case "elite.counter.affix_resistant":
                return IsNodePurchased("elite.affix.resistant");
            case "elite.counter.rift_1":
                return totalRiftEliteKillsEver > 0 || totalChaosFiveEliteKillsEver > 0;
            case "elite.rift.spawn_light":
                return totalChaosFiveEliteKillsEver > 0 || (highestChaosLevelEver >= 5 && totalEliteKillsEver > 0);
            case "elite.rift.reward_1":
                return totalRiftEliteKillsEver >= 3 || totalChaosFiveEliteKillsEver >= 3;
            case "elite.rift.core_1":
                return totalRiftEliteKillsEver > 0 || totalChaosFiveEliteKillsEver > 0;
            case "elite.rift.affix_chaotic":
                return IsNodePurchased("elite.affix.chaotic");
            case "elite.rift.affix_volatile":
                return GetChaosFiveBossKills() > 0;
            case "elite.rift.tower_trial":
                return CountTowerMasteryMilestone(TowerMasteryMilestone.V) >= 1;
            case "elite.rift.path_trial":
                return GetPathTechniqueProgressionManager() != null && GetPathTechniqueProgressionManager().pathTechniqueLevel >= 40;
            case "elite.rift.cosmetic":
                return totalRiftEliteKillsEver >= 5 || totalChaosFiveEliteKillsEver >= 5;
            default:
                return true;
        }
    }

    private string GetRequirementProgressText(EliteHuntNodeDefinition definition)
    {
        if (definition == null)
            return "Keine Daten.";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Elite-Siegel: " + Mathf.Min(eliteSeals, definition.sealCost) + " / " + definition.sealCost);

        if (definition.riftCoreCost > 0)
            builder.AppendLine("Risskerne: " + Mathf.Min(GetAvailableRiftCores(), definition.riftCoreCost) + " / " + definition.riftCoreCost);

        if (definition.blueprintCost > 0)
            builder.AppendLine("Bauplaene: " + Mathf.Min(GetAvailableBlueprints(), definition.blueprintCost) + " / " + definition.blueprintCost);

        builder.AppendLine("Elite-Rang: " + Mathf.Min(eliteRank, definition.requiredEliteRank) + " / " + definition.requiredEliteRank);
        builder.AppendLine("Elite gesehen: " + GetTotalEliteSeenIncludingCurrentRun());
        builder.AppendLine("Elite-Kills: " + GetTotalEliteKillsIncludingCurrentRun());
        builder.AppendLine("Elite-Leaks: " + (totalEliteLeaksEver + lastRunEliteLeaks));
        builder.AppendLine("Elite ohne Leak: " + totalEliteNoLeakWavesEver);
        builder.AppendLine("Chaos-Elite: " + (totalChaosEliteKillsEver + lastRunChaosEliteKills));
        builder.AppendLine("Riss-Elite: " + (totalRiftEliteKillsEver + lastRunRiftEliteKills));
        builder.AppendLine("Elite nach Verbau: " + totalEliteAfterBlockKillsEver);
        builder.AppendLine("Hoechste Wave: " + GetHighestWaveIncludingHistory());
        builder.AppendLine("Hoechstes Chaos: " + Mathf.Max(highestChaosLevelEver, highestChaosThisRun) + " / 5");
        builder.AppendLine("Spezialbedingung: " + FormatYesNo(AreSpecialRequirementsMet(definition)));
        return builder.ToString();
    }

    private void AppendNextGoals(StringBuilder builder)
    {
        if (!IsEliteHuntUnlocked())
        {
            builder.AppendLine("- Besiege einen Boss bei Chaos 5 oder erreiche Wave 35.");
            return;
        }

        if (activeHuntMode == EliteHuntMode.Off)
            builder.AppendLine("- Waehle einen Elite-Jagdmodus fuer den naechsten Run.");

        if (totalEliteKillsEver <= 0)
            builder.AppendLine("- Besiege die erste Elite.");

        if (eliteRank < 5)
            builder.AppendLine("- Erreiche Elite-Rang 5 fuer Elite-Jagd Normal.");

        if (totalChaosEliteKillsEver <= 0 && highestChaosLevelEver >= 3)
            builder.AppendLine("- Besiege eine Elite bei Chaos 3+.");

        if (highestChaosLevelEver >= 5 && totalChaosFiveEliteKillsEver <= 0)
            builder.AppendLine("- Besiege eine Elite bei Chaos 5.");

        if (totalEliteAfterBlockKillsEver <= 0)
            builder.AppendLine("- Besiege spaeter eine Elite nach einem echten Verbau-Comeback.");
    }

    private bool IsEliteUnlockConditionMet()
    {
        if (GetChaosFiveBossKills() > 0)
            return true;

        if (GetHighestWaveIncludingHistory() >= 35)
            return true;

        if (GetAccountLevel() >= 35)
            return true;

        return CountTowerMasteryMilestone(TowerMasteryMilestone.IV) >= 3;
    }

    private string GetEliteUnlockStateText()
    {
        if (IsEliteUnlockConditionMet())
            return "Freischaltbar";

        if (GetAccountLevel() >= 25 || GetHighestWaveIncludingHistory() >= 25)
            return "Teaser sichtbar";

        return "Gesperrt";
    }

    private int GetTotalEliteSeenIncludingCurrentRun()
    {
        return totalElitesSeenEver + lastRunElitesSeen;
    }

    private int GetTotalEliteKillsIncludingCurrentRun()
    {
        return totalEliteKillsEver + lastRunEliteKills;
    }

    private int GetHighestWaveIncludingHistory()
    {
        WaveHistory history = GetWaveHistory();
        int historyWave = history != null ? history.GetHighestWaveNumberReached() : 0;
        return Mathf.Max(highestWaveEver, highestWaveThisRun, historyWave);
    }

    private int GetAccountLevel()
    {
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        return generalMeta != null ? generalMeta.accountLevel : 1;
    }

    private int GetChaosFiveBossKills()
    {
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        return chaosResearch != null ? Mathf.Max(0, chaosResearch.totalChaos5BossKillsEver + chaosResearch.lastRunChaos5BossKills) : 0;
    }

    private int GetTotalMiniBossKills()
    {
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        WaveHistory history = GetWaveHistory();
        int chaosValue = chaosResearch != null ? chaosResearch.totalMiniBossKillsEver : 0;
        int historyValue = history != null ? history.GetMiniBossKills() : 0;
        return Mathf.Max(chaosValue, historyValue);
    }

    private bool HasSeenScenario(WaveScenario scenario)
    {
        WaveHistory history = GetWaveHistory();

        if (history == null || history.completedWaves == null)
            return false;

        foreach (WaveCompletionResult result in history.completedWaves)
        {
            if (result != null && result.scenario == scenario)
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

    private bool HasSeenChaosWaveBlock(ChaosWaveBlockType blockType)
    {
        WaveHistory history = GetWaveHistory();

        if (history == null || history.completedWaves == null)
            return false;

        foreach (WaveCompletionResult result in history.completedWaves)
        {
            if (result == null || result.chaosWaveBlocksAtWaveStart == null)
                continue;

            foreach (ChaosWaveBlock block in result.chaosWaveBlocksAtWaveStart)
            {
                if (block != null && block.blockType == blockType)
                    return true;
            }
        }

        return false;
    }

    private bool IsGeneralTowerUnlocked(TowerRole role)
    {
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        return generalMeta == null || generalMeta.IsTowerUnlocked(role);
    }

    private int CountTowerMasteryMilestone(TowerMasteryMilestone milestone)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery == null)
            return 0;

        int count = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            if (towerMastery.IsMilestoneUnlocked(role, milestone))
                count++;
        }

        return count;
    }

    private int GetAvailableRiftCores()
    {
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        return chaosResearch != null ? Mathf.Max(0, chaosResearch.riftCores) : 0;
    }

    private int GetAvailableBlueprints()
    {
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        return pathTechnique != null ? Mathf.Max(0, pathTechnique.blueprints) : 0;
    }

    private bool TrySpendExternalCurrencies(EliteHuntNodeDefinition definition)
    {
        if (definition == null)
            return false;

        if (definition.riftCoreCost > 0)
        {
            ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
            if (chaosResearch == null || !chaosResearch.TrySpendRiftCores(definition.riftCoreCost))
                return false;
        }

        if (definition.blueprintCost > 0)
        {
            PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
            if (pathTechnique == null || !pathTechnique.TrySpendBlueprints(definition.blueprintCost, 0))
                return false;
        }

        return true;
    }

    private int CalculateEliteRank(int totalXP)
    {
        int level = 1;
        int remaining = Mathf.Max(0, totalXP);

        while (level < 999)
        {
            int needed = GetXPToNextEliteRank(level);
            if (remaining < needed)
                break;

            remaining -= needed;
            level++;
        }

        return level;
    }

    private int GetXPIntoRank(int totalXP)
    {
        int level = 1;
        int remaining = Mathf.Max(0, totalXP);

        while (level < eliteRank)
        {
            remaining -= GetXPToNextEliteRank(level);
            level++;
        }

        return Mathf.Max(0, remaining);
    }

    private int GetXPToNextEliteRank(int rank)
    {
        int safeRank = Mathf.Max(1, rank);
        return 100 + safeRank * 50 + Mathf.FloorToInt(safeRank * safeRank * 2f);
    }

    private void EnsureDefinitions()
    {
        if (definitions == null)
            definitions = new List<EliteHuntNodeDefinition>();

        if (definitions.Count == 0)
            CreateDefaultDefinitions();

        RebuildDefinitionLookup();
    }

    private void EnsureStates()
    {
        EnsureDefinitions();

        if (nodeStates == null)
            nodeStates = new List<EliteHuntNodeState>();

        RebuildStateLookup();

        foreach (EliteHuntNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            if (stateById.ContainsKey(definition.nodeId))
                continue;

            EliteHuntNodeState state = new EliteHuntNodeState
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

        foreach (EliteHuntNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            definitionById[definition.nodeId] = definition;
        }
    }

    private void RebuildStateLookup()
    {
        stateById.Clear();

        foreach (EliteHuntNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            stateById[state.nodeId] = state;
        }
    }

    private void CreateDefaultDefinitions()
    {
        definitions.Clear();

        AddDefinition("elite.core.teaser", "Elite-Jagd-Teaser", EliteHuntCategory.Overview, EliteHuntNodeKind.Core, 0, 0, 0, 1, "Elite-Tab wird als Endgame-Teaser sichtbar.", false, "Account-Level 25.");
        AddDefinition("elite.core.unlock", "Elite-Jagd", EliteHuntCategory.Overview, EliteHuntNodeKind.Core, 0, 0, 0, 1, "Elite-Jagd wird freigeschaltet. Elite bleibt opt-in und startet erst mit gewaehltem Jagdmodus.", false, "Chaos-5-Boss oder Wave 35.");
        AddDefinition("elite.core.result_summary", "Elite-Protokoll", EliteHuntCategory.Overview, EliteHuntNodeKind.Core, 2, 0, 0, 1, "Result zeigt Elite-Daten genauer.", false, "1 Elite gesehen.");
        AddDefinition("elite.core.lexicon", "Elite-Archiv", EliteHuntCategory.Overview, EliteHuntNodeKind.Core, 2, 0, 0, 1, "Archiv-Kategorie Elite wird vorbereitet.", false, "Elite frei.");
        AddDefinition("elite.core.contracts", "Jagdauftraege", EliteHuntCategory.Overview, EliteHuntNodeKind.Core, 0, 0, 0, 1, "Elite-Auftraege werden sichtbar.", false, "Elite frei.");

        AddDefinition("elite.contract.runner_1", "Runner-Jagd I", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 0, 0, 0, 1, "Auftrag: 3 Elite Runner besiegen. Belohnung: 2 Siegel.", false, "Elite frei.");
        AddDefinition("elite.contract.tank_1", "Tank-Jagd I", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 1, 0, 0, 1, "Auftrag: 2 Elite Tanks besiegen. Belohnung: 3 Siegel.", false, "1 Elite gesehen.");
        AddDefinition("elite.contract.knight_1", "Ritterbruch I", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 1, 0, 0, 1, "Auftrag: 2 Elite Knights besiegen. Belohnung: 3 Siegel.", false, "1 Elite gesehen.");
        AddDefinition("elite.contract.mage_1", "Magierjagd I", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 2, 0, 0, 1, "Auftrag: 2 Elite Mages besiegen. Belohnung: 4 Siegel.", false, "MageIntro gesehen.");
        AddDefinition("elite.contract.learner_1", "Lernbrecher I", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 2, 0, 0, 1, "Auftrag: 2 Elite Learners besiegen. Belohnung: 4 Siegel.", false, "Learner gesehen.");
        AddDefinition("elite.contract.allrounder_1", "AllRounder-Pruefung", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 3, 0, 0, 1, "Auftrag: 1 Elite AllRounder besiegen. Belohnung: 5 Siegel.", false, "AllRounder gesehen.");
        AddDefinition("elite.contract.miniboss_1", "Elite-MiniBoss", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 4, 0, 0, 1, "Auftrag: 1 Elite MiniBoss besiegen. Belohnung: 6 Siegel.", false, "MiniBoss 5x besiegt.");
        AddDefinition("elite.contract.boss_1", "Elite-Boss I", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 6, 0, 0, 1, "Auftrag: 1 Elite Boss besiegen. Belohnung: 10 Siegel.", false, "Chaos 3 Boss besiegt.");
        AddDefinition("elite.contract.chaos_1", "Rissjagd I", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 3, 0, 0, 1, "Auftrag: 3 Elite bei Chaos 3+ besiegen. Belohnung: 6 Siegel.", false, "Chaos 3 erreicht.");
        AddDefinition("elite.contract.chaos_5", "Rissjagd II", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 8, 0, 0, 1, "Auftrag: 1 Elite bei Chaos 5 besiegen. Belohnung: 10 Siegel.", false, "Chaos 5 erreicht.");
        AddDefinition("elite.contract.no_leak", "Saubere Jagd", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 2, 0, 0, 1, "Auftrag: Elite-Wave ohne Leak abschliessen. Belohnung: 4 Siegel.", false, "Elite gesehen.");
        AddDefinition("elite.contract.after_block", "Krisenjagd", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 4, 0, 0, 1, "Auftrag: Elite nach Verbau besiegen. Belohnung: 8 Siegel.", false, "Pfadtechnik III.");
        AddDefinition("elite.contract.sniper", "Praezisionsjagd", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 3, 0, 0, 1, "Auftrag: Elite mit Sniper-Beteiligung besiegen. Belohnung: 5 Siegel.", false, "Sniper frei.");
        AddDefinition("elite.contract.combo", "Dunkle Jagd", EliteHuntCategory.Contracts, EliteHuntNodeKind.Contract, 6, 0, 0, 1, "Auftrag: Elite mit 3 Status-Effekten besiegen. Belohnung: 10 Siegel.", false, "Spike/Alchemist + Combo frei.");

        AddDefinition("elite.affix.unlock_basic", "Basis-Affixe", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 0, 0, 0, 1, "Swift, Armored und Fortified werden fuer Elite vorbereitet.", false, "Elite frei.");
        AddDefinition("elite.affix.regen", "Regeneration", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 3, 0, 0, 1, "Regenerierend-Affix im Pool.", false, "3 Elite besiegt.");
        AddDefinition("elite.affix.resistant", "Resistenz-Elite", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 5, 0, 0, 1, "Resistant-Affix im Pool.", false, "Resistance gesehen.");
        AddDefinition("elite.affix.phasing", "Phasenlauf", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 5, 0, 0, 5, "Phasing-Affix im Pool.", false, "Elite Rang 5.");
        AddDefinition("elite.affix.commander", "Kommandant", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 6, 0, 0, 1, "Commander-Affix im Pool.", false, "Mixed-Wave geschafft.");
        AddDefinition("elite.affix.guardian", "Waechter", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 6, 0, 0, 1, "Guardian-Affix im Pool.", false, "Elite Knight besiegt.");
        AddDefinition("elite.affix.rusher", "Durchbruch", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 6, 0, 0, 1, "Rusher-Affix im Pool.", false, "Elite Runner besiegt.");
        AddDefinition("elite.affix.splitting", "Splitternd", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 8, 0, 0, 1, "Splitting-Affix im Pool.", false, "Wave 25.");
        AddDefinition("elite.affix.magebound", "Rissmagie", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 8, 0, 0, 1, "Magebound-Affix im Pool.", false, "Elite Mage.");
        AddDefinition("elite.affix.learner_core", "Lernkern", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 8, 0, 0, 1, "Learner-Core-Affix im Pool.", false, "Elite Learner.");
        AddDefinition("elite.affix.chaotic", "Chaotisch", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 10, 3, 0, 1, "Chaotic-Affix im Pool.", false, "Chaos 5.");
        AddDefinition("elite.affix.volatile", "Instabil", EliteHuntCategory.Affixes, EliteHuntNodeKind.Affix, 15, 5, 0, 1, "Volatile-Affix im Pool.", false, "Chaos-5-Boss.");

        AddDefinition("elite.reward.seal_1", "Siegelkunde I", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 3, 0, 0, 1, "Erster Elite-Kill pro Run gibt etwas mehr Elite-Rang-XP.");
        AddDefinition("elite.reward.seal_2", "Siegelkunde II", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 8, 0, 0, 5, "+1 Siegel bei Elite-MiniBoss, einmal pro Run.", 1);
        AddDefinition("elite.reward.seal_3", "Siegelkunde III", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 15, 0, 0, 12, "+1 Siegel bei Elite-Boss, einmal pro Run.", 2);
        AddDefinition("elite.reward.mastery_1", "Meisterproben I", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 5, 0, 0, 1, "Elite-Kills geben beteiligten Towern spaeter Mastery-XP.", 1, false, "Tower-Mastery I bei 3 Towern.");
        AddDefinition("elite.reward.mastery_2", "Meisterproben II", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 12, 0, 0, 10, "Mehr Tower-Mastery-XP aus Elite-Kills, gedeckelt.", 2);
        AddDefinition("elite.reward.knowledge_1", "Kernwissen-Probe", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 4, 0, 0, 1, "Elite-Kills geben spaeter etwas Kernwissen.");
        AddDefinition("elite.reward.chaos_1", "Rissprobe", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 8, 1, 0, 1, "Chaos-Elite gibt mehr Chaos-Wissen.", 1, false, "Chaos-Elite besiegt.");
        AddDefinition("elite.reward.path_1", "Krisenprobe", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 8, 0, 1, 1, "Elite nach Verbau gibt Pfadtechnik-XP.", 1, false, "Elite nach Verbau besiegt.");
        AddDefinition("elite.reward.cosmetic_1", "Elite-Trophaeen", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 10, 0, 0, 1, "Kosmetische Trophaeen sichtbar.", false, "10 Elite besiegt.");
        AddDefinition("elite.reward.rift_core", "Riss-Elite-Kern", EliteHuntCategory.Rewards, EliteHuntNodeKind.Reward, 20, 5, 0, 1, "Riss-Elite kann selten Risskerne geben, hart gecappt.", 2, false, "Chaos-5-Elite besiegt.");

        AddDefinition("elite.frequency.unlock_light", "Elite-Jagd Leicht", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 0, 0, 0, 1, "Elite-Jagd Leicht ist als opt-in Modus verfuegbar.", false, "Elite freigeschaltet.");
        AddDefinition("elite.frequency.normal", "Elite-Jagd Normal", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 6, 0, 0, 5, "Elite-Jagd Normal ist verfuegbar.", false, "Elite Rang 5.");
        AddDefinition("elite.frequency.hard", "Elite-Jagd Hart", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 14, 0, 0, 12, "Elite-Jagd Hart ist verfuegbar.", false, "Elite Rang 12 + 10 Elite-Kills.");
        AddDefinition("elite.frequency.rift", "Riss-Elite-Stufe", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 20, 5, 0, 20, "Riss-Elite-Modus ist verfuegbar.", false, "Chaos 5 + Elite Rang 20.");
        AddDefinition("elite.frequency.role_focus", "Rollen-Fokus", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 8, 0, 0, 8, "Eine Elite-Rolle wird leicht haeufiger.", 1);
        AddDefinition("elite.frequency.affix_focus", "Affix-Fokus", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 10, 0, 0, 1, "Ein Affix wird leicht haeufiger.", 1, false, "5 Affixe freigeschaltet.");
        AddDefinition("elite.frequency.no_boss_elite", "Boss-Elite sperren", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 5, 0, 0, 1, "Elite-Boss kann deaktiviert werden.", false, "Elite-Boss gesehen.");
        AddDefinition("elite.frequency.safe_early", "Fruehe Sicherheit", EliteHuntCategory.Frequency, EliteHuntNodeKind.Frequency, 4, 0, 0, 1, "Elite erscheinen nie vor Wave 8. V1 nutzt weiterhin mindestens Wave 18.", false, "3 Elite-Waves ueberlebt.");

        AddDefinition("elite.info.marker", "Elite-Markierung", EliteHuntCategory.Counters, EliteHuntNodeKind.InfoCounter, 2, 0, 0, 1, "Elite bekommen ein klareres UI-Symbol.", false, "Elite gesehen.");
        AddDefinition("elite.info.affix_preview", "Affix-Preview", EliteHuntCategory.Counters, EliteHuntNodeKind.InfoCounter, 4, 0, 0, 1, "Affixe werden spaeter in der Preview angezeigt.", false, "3 Elite gesehen.");
        AddDefinition("elite.info.role_hint", "Rollenhinweis", EliteHuntCategory.Counters, EliteHuntNodeKind.InfoCounter, 4, 0, 0, 1, "Elite-Rolle wird klarer angezeigt.", false, "3 Elite-Kills.");
        AddDefinition("elite.info.reward_preview", "Belohnungs-Preview", EliteHuntCategory.Counters, EliteHuntNodeKind.InfoCounter, 5, 0, 0, 1, "Erwartete Elite-Belohnung sichtbar.", false, "5 Elite-Kills.");
        AddDefinition("elite.info.history", "Elite-Chronik", EliteHuntCategory.Counters, EliteHuntNodeKind.InfoCounter, 5, 0, 0, 1, "Archiv/Result zeigt Elite-Historie.", false, "5 Elite gesehen.");
        AddDefinition("elite.counter.runner_1", "Elite-Runner-Jagd I", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 5, 0, 0, 1, "+3% Schaden gegen Elite Runner.", 1, false, "Elite Runner besiegt.");
        AddDefinition("elite.counter.tank_1", "Elite-Tank-Analyse I", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 5, 0, 0, 1, "+3% Schaden gegen Elite Tank.", 1, false, "Elite Tank besiegt.");
        AddDefinition("elite.counter.knight_1", "Elite-Knight-Bruch I", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 6, 0, 0, 1, "+3% Schaden gegen Elite Knight.", 1, false, "Elite Knight besiegt.");
        AddDefinition("elite.counter.mage_1", "Elite-Mage-Stoerung I", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 8, 0, 0, 1, "Magebound/Teleport-Hinweis und kleiner Bonus.", 1, false, "Elite Mage besiegt.");
        AddDefinition("elite.counter.learner_1", "Elite-Learner-Probe I", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 8, 0, 0, 1, "+4% direkter Schaden gegen Elite Learner.", 1, false, "Elite Learner besiegt.");
        AddDefinition("elite.counter.allrounder_1", "Elite-AllRounder-Studie", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 10, 0, 0, 1, "+3% direkter Schaden gegen Elite AllRounder.", 1, false, "Elite AllRounder besiegt.");
        AddDefinition("elite.counter.boss_1", "Elite-Boss-Studie I", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 14, 0, 0, 1, "+2% gegen Elite Boss/MiniBoss.", 2, false, "Elite Boss gesehen.");
        AddDefinition("elite.counter.affix_swift", "Tempo-Forschung", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 6, 0, 0, 1, "+3% gegen Swift-Elite.", 1, false, "Swift-Elite besiegt.");
        AddDefinition("elite.counter.affix_armored", "Elite-Panzerkunde", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 8, 0, 0, 1, "+3% gegen Armored-Elite.", 1, false, "Armored-Elite besiegt.");
        AddDefinition("elite.counter.affix_resistant", "Resistenz-Probe", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 10, 0, 0, 1, "Direkte Treffer +3% gegen Resistant-Elite.", 1, false, "Resistant-Elite besiegt.");
        AddDefinition("elite.counter.rift_1", "Riss-Elite-Gegenprobe", EliteHuntCategory.Counters, EliteHuntNodeKind.PowerCounter, 12, 3, 0, 1, "+3% gegen Chaotic/Riss-Elite.", 2, false, "Riss-Elite besiegt.");

        AddDefinition("elite.rift.unlock", "Riss-Elite", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 0, 0, 0, 20, "Riss-Elite wird sichtbar.", false, "Chaos 5 + Elite Rang 20.");
        AddDefinition("elite.rift.spawn_light", "Riss-Spur I", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 12, 4, 0, 20, "Riss-Elite kann selten erscheinen.", false, "1 Elite bei Chaos 5.");
        AddDefinition("elite.rift.reward_1", "Riss-Siegel I", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 16, 6, 0, 20, "Mehr Elite-Siegel bei Riss-Elite, gedeckelt.", 1, false, "3 Riss-Elite gesehen.");
        AddDefinition("elite.rift.core_1", "Risskern-Probe", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 20, 8, 0, 20, "Riss-Elite kann +1 Risskern geben, max. 1/Run.", 2, false, "Riss-Elite besiegt.");
        AddDefinition("elite.rift.affix_chaotic", "Chaotisches Affix", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 12, 5, 0, 20, "Chaotic-Affix im Pool.", false, "Chaotic-Affix frei.");
        AddDefinition("elite.rift.affix_volatile", "Instabiles Affix", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 18, 7, 0, 20, "Volatile-Affix im Pool.", false, "Chaos-5-Boss.");
        AddDefinition("elite.rift.tower_trial", "Riss-Meisterpruefung", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 25, 10, 0, 20, "Tower-Elite-Auftraege fuer Keystone-Builds.", false, "Tower Milestone V bei 1 Tower.");
        AddDefinition("elite.rift.path_trial", "Riss-Krisenjagd", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 20, 8, 0, 20, "Verbau-Elite-Auftraege.", false, "Pfadtechnik V.");
        AddDefinition("elite.rift.cosmetic", "Riss-Trophaeen", EliteHuntCategory.RiftElite, EliteHuntNodeKind.RiftElite, 20, 6, 0, 20, "Kosmetische Elite-Riss-Visuals.", false, "5 Riss-Elite besiegt.");
    }

    private void AddDefinition(string nodeId, string displayName, EliteHuntCategory category, EliteHuntNodeKind kind, int sealCost, int riftCoreCost, int blueprintCost, int requiredEliteRank, string effectText, bool unlockedByDefault = false, string requirementText = "")
    {
        AddDefinition(nodeId, displayName, category, kind, sealCost, riftCoreCost, blueprintCost, requiredEliteRank, effectText, 0, unlockedByDefault, requirementText);
    }

    private void AddDefinition(string nodeId, string displayName, EliteHuntCategory category, EliteHuntNodeKind kind, int sealCost, int riftCoreCost, int blueprintCost, int requiredEliteRank, string effectText, int slotCost, bool unlockedByDefault = false, string requirementText = "")
    {
        definitions.Add(new EliteHuntNodeDefinition
        {
            nodeId = nodeId,
            displayName = displayName,
            category = category,
            kind = kind,
            sealCost = Mathf.Max(0, sealCost),
            riftCoreCost = Mathf.Max(0, riftCoreCost),
            blueprintCost = Mathf.Max(0, blueprintCost),
            requiredEliteRank = Mathf.Max(1, requiredEliteRank),
            slotCost = Mathf.Max(0, slotCost),
            unlockedByDefault = unlockedByDefault,
            effectText = effectText,
            requirementText = requirementText
        });
    }

    private void DeactivateExclusiveLoadoutGroup(EliteHuntNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);

        foreach (EliteHuntNodeDefinition definition in definitions)
        {
            if (definition == null || definition.nodeId == activatedDefinition.nodeId || !definition.RequiresLoadoutSlot())
                continue;

            if (GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            EliteHuntNodeState state = GetNodeState(definition.nodeId);
            if (state != null)
                state.active = false;
        }
    }

    private int GetActiveSlotsInExclusiveGroup(EliteHuntNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return 0;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);
        int slots = 0;

        foreach (EliteHuntNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            EliteHuntNodeState state = GetNodeState(definition.nodeId);
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

    private string FormatCost(EliteHuntNodeDefinition definition)
    {
        if (definition == null)
            return "0 Siegel";

        string text = definition.sealCost + " Siegel";
        if (definition.riftCoreCost > 0)
            text += " + " + definition.riftCoreCost + " RK";

        if (definition.blueprintCost > 0)
            text += " + " + definition.blueprintCost + " BP";

        return text;
    }

    private string FormatYesNo(bool value)
    {
        return value ? "erfuellt" : "offen";
    }

    private EnemySpawner GetEnemySpawner()
    {
        GameManager currentGameManager = GetGameManager();

        if (currentGameManager != null && currentGameManager.enemySpawner != null)
            return currentGameManager.enemySpawner;

        return FindObjectOfType<EnemySpawner>();
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

    private GeneralMetaProgressionManager GetGeneralMetaProgressionManager()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetGeneralMetaProgressionManager() : GeneralMetaProgressionManager.GetOrCreate();
    }

    private ChaosResearchProgressionManager GetChaosResearchProgressionManager()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetChaosResearchProgressionManager() : ChaosResearchProgressionManager.GetOrCreate();
    }

    private PathTechniqueProgressionManager GetPathTechniqueProgressionManager()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetPathTechniqueProgressionManager() : PathTechniqueProgressionManager.GetOrCreate();
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetTowerMasteryManager() : TowerMasteryManager.GetOrCreate();
    }

    private void LoadProfile()
    {
        EnsureStates();
        LoadStringSet(PlayerPrefsPrefix + "FirstEliteRolesClaimed", firstEliteRolesClaimed);
        LoadStringSet(PlayerPrefsPrefix + "FirstEliteAffixesClaimed", firstEliteAffixesClaimed);

        eliteRankXP = PlayerPrefs.GetInt(PlayerPrefsPrefix + "EliteRankXP", eliteRankXP);
        eliteRank = CalculateEliteRank(eliteRankXP);
        eliteSeals = PlayerPrefs.GetInt(PlayerPrefsPrefix + "EliteSeals", eliteSeals);
        activeHuntMode = (EliteHuntMode)PlayerPrefs.GetInt(PlayerPrefsPrefix + "ActiveHuntMode", (int)activeHuntMode);
        totalElitesSeenEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalElitesSeenEver", totalElitesSeenEver);
        totalEliteKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteKillsEver", totalEliteKillsEver);
        totalEliteLeaksEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteLeaksEver", totalEliteLeaksEver);
        totalEliteBossKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteBossKillsEver", totalEliteBossKillsEver);
        totalEliteMiniBossKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteMiniBossKillsEver", totalEliteMiniBossKillsEver);
        totalChaosEliteKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosEliteKillsEver", totalChaosEliteKillsEver);
        totalChaosFiveEliteKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalChaosFiveEliteKillsEver", totalChaosFiveEliteKillsEver);
        totalRiftEliteKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalRiftEliteKillsEver", totalRiftEliteKillsEver);
        totalEliteNoLeakWavesEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteNoLeakWavesEver", totalEliteNoLeakWavesEver);
        totalEliteAfterBlockKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteAfterBlockKillsEver", totalEliteAfterBlockKillsEver);
        totalEliteContractsCompletedEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalEliteContractsCompletedEver", totalEliteContractsCompletedEver);
        highestWaveEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        highestChaosLevelEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestChaosLevelEver", highestChaosLevelEver);
        lastRunEliteSealsGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunEliteSeals", lastRunEliteSealsGained);
        lastRunEliteRankXPGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunEliteRankXP", lastRunEliteRankXPGained);
        lastRunElitesSeen = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunElitesSeen", lastRunElitesSeen);
        lastRunEliteKills = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunEliteKills", lastRunEliteKills);
        lastRunEliteLeaks = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunEliteLeaks", lastRunEliteLeaks);
        lastRunChaosEliteKills = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunChaosEliteKills", lastRunChaosEliteKills);
        lastRunRiftEliteKills = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunRiftEliteKills", lastRunRiftEliteKills);

        foreach (EliteHuntNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            EliteHuntNodeState state = GetNodeState(definition.nodeId);
            if (state == null)
                continue;

            int defaultPurchased = definition.unlockedByDefault ? 1 : 0;
            int defaultActive = definition.unlockedByDefault && !definition.RequiresLoadoutSlot() ? 1 : 0;
            state.purchased = PlayerPrefs.GetInt(PlayerPrefsPrefix + definition.nodeId + ".Purchased", defaultPurchased) == 1;
            state.active = PlayerPrefs.GetInt(PlayerPrefsPrefix + definition.nodeId + ".Active", defaultActive) == 1;

            if (!definition.RequiresLoadoutSlot())
                state.active = state.purchased;
        }

        if (!IsEliteHuntUnlocked())
            activeHuntMode = EliteHuntMode.Off;
    }

    private void SaveProfile()
    {
        EnsureStates();

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "EliteRankXP", eliteRankXP);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "EliteSeals", eliteSeals);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "ActiveHuntMode", (int)activeHuntMode);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalElitesSeenEver", totalElitesSeenEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteKillsEver", totalEliteKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteLeaksEver", totalEliteLeaksEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteBossKillsEver", totalEliteBossKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteMiniBossKillsEver", totalEliteMiniBossKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosEliteKillsEver", totalChaosEliteKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalChaosFiveEliteKillsEver", totalChaosFiveEliteKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalRiftEliteKillsEver", totalRiftEliteKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteNoLeakWavesEver", totalEliteNoLeakWavesEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteAfterBlockKillsEver", totalEliteAfterBlockKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalEliteContractsCompletedEver", totalEliteContractsCompletedEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestChaosLevelEver", highestChaosLevelEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunEliteSeals", lastRunEliteSealsGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunEliteRankXP", lastRunEliteRankXPGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunElitesSeen", lastRunElitesSeen);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunEliteKills", lastRunEliteKills);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunEliteLeaks", lastRunEliteLeaks);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunChaosEliteKills", lastRunChaosEliteKills);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunRiftEliteKills", lastRunRiftEliteKills);

        foreach (EliteHuntNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Purchased", state.purchased ? 1 : 0);
            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Active", state.active ? 1 : 0);
        }

        SaveStringSet(PlayerPrefsPrefix + "FirstEliteRolesClaimed", firstEliteRolesClaimed);
        SaveStringSet(PlayerPrefsPrefix + "FirstEliteAffixesClaimed", firstEliteAffixesClaimed);
        PlayerPrefs.Save();
    }

    private void LoadStringSet(string key, HashSet<string> target)
    {
        if (target == null)
            return;

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
