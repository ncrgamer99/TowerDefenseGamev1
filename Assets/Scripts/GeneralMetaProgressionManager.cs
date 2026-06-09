using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum GeneralMetaCategory
{
    Account,
    TowerUnlock,
    TileUnlock,
    QoL,
    StartOption,
    EnemyResearch,
    MetaLoadout
}

public enum GeneralMetaNodeKind
{
    ContentUnlock,
    TileUnlock,
    QoL,
    StartPower,
    EnemyInfo,
    EnemyPower,
    LoadoutSlot
}

[System.Serializable]
public class GeneralMetaNodeDefinition
{
    public string nodeId;
    public string displayName;
    public GeneralMetaCategory category;
    public GeneralMetaNodeKind kind;
    public int cost;
    public int requiredAccountLevel;
    public int slotCost;
    public bool unlockedByDefault;
    public string effectText;
    public string requirementText;

    public bool RequiresLoadoutSlot()
    {
        return slotCost > 0 && (kind == GeneralMetaNodeKind.StartPower || kind == GeneralMetaNodeKind.EnemyPower);
    }
}

[System.Serializable]
public class GeneralMetaNodeState
{
    public string nodeId;
    public bool purchased;
    public bool active;
}

public class GeneralMetaProgressionManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_GeneralMeta_";
    private const string StartTowerSetMigrationKey = PlayerPrefsPrefix + "StartTowerSetV2";
    private const string StartTileSetMigrationKey = PlayerPrefsPrefix + "StartTileSetV2";
    private const string GoldKnockTileSwapMigrationKey = PlayerPrefsPrefix + "GoldKnockTileSwapV1";
    private const int LoadoutProfileCount = 3;

    public static GeneralMetaProgressionManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Account")]
    public int accountLevel = 1;
    public int accountXP = 0;
    public int kernwissen = 0;
    public int baseLoadoutSlots = 1;
    public int maxLoadoutSlots = 12;

    [Header("Run-End Rewards")]
    public int kernwissenPerCompletedWave = 4;
    public int miniBossWaveKernwissenBonus = 15;
    public int bossWaveKernwissenBonus = 40;
    public int bossKillKernwissenBonus = 20;
    public int newHighestWaveKernwissenBonus = 25;
    public int perfectWaveKernwissenBonus = 2;
    public int perfectBossWaveKernwissenBonus = 15;
    public int firstChaosLevelKernwissenBonus = 30;
    public int eliteKillKernwissenBonus = 80;

    [Header("Persistent Milestones")]
    public int highestWaveEver = 0;
    public int highestChaosLevelEver = 0;
    public int totalGoldEarnedEver = 0;
    public int totalTowersBuiltEver = 0;
    public int totalTowerLevelUpsEver = 0;
    public int totalUpgradePointsEarnedEver = 0;
    public int totalRunnerKillsEver = 0;
    public int totalBossKillsEver = 0;
    public int blockedEventsChosenEver = 0;

    [Header("Last Run")]
    public int lastRunKernwissenGained = 0;
    public int lastRunAccountXPGained = 0;
    public int lastRunAccountLevelsGained = 0;

    [Header("Definitions / State")]
    public List<GeneralMetaNodeDefinition> definitions = new List<GeneralMetaNodeDefinition>();
    public List<GeneralMetaNodeState> nodeStates = new List<GeneralMetaNodeState>();

    private readonly Dictionary<string, GeneralMetaNodeDefinition> definitionById = new Dictionary<string, GeneralMetaNodeDefinition>();
    private readonly Dictionary<string, GeneralMetaNodeState> stateById = new Dictionary<string, GeneralMetaNodeState>();
    private int activeLoadoutIndex = 0;
    private int pendingRunKernwissen = 0;
    private int highestWaveReachedThisRun = 0;
    private int highestChaosLevelReachedThisRun = 0;
    private bool runFinalized = false;
    private bool firstTowerDiscountUsedThisRun = false;
    private bool startXPGrantedThisRun = false;
    private bool startProtectionUsedThisRun = false;
    private bool emergencyReserveUsedThisRun = false;

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

    public static GeneralMetaProgressionManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GeneralMetaProgressionManager existing = FindObjectOfType<GeneralMetaProgressionManager>();

        if (existing != null)
        {
            Instance = existing;

            if (Instance.gameManager == null && preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        GameObject systemObject = new GameObject("GeneralMetaProgressionSystem");
        GeneralMetaProgressionManager manager = systemObject.AddComponent<GeneralMetaProgressionManager>();
        manager.gameManager = preferredGameManager;
        Instance = manager;
        return manager;
    }

    public void StartNewRun()
    {
        pendingRunKernwissen = 0;
        highestWaveReachedThisRun = 0;
        highestChaosLevelReachedThisRun = 0;
        lastRunKernwissenGained = 0;
        lastRunAccountXPGained = 0;
        lastRunAccountLevelsGained = 0;
        firstTowerDiscountUsedThisRun = false;
        startXPGrantedThisRun = false;
        startProtectionUsedThisRun = false;
        emergencyReserveUsedThisRun = false;
        runFinalized = false;
        LoadProfile();
    }

    public void FinalizeRun()
    {
        if (runFinalized)
            return;

        runFinalized = true;

        if (IsMetaProgressionSuppressedForCurrentRun())
            return;

        int reward = Mathf.Max(0, pendingRunKernwissen);
        lastRunKernwissenGained = reward;
        lastRunAccountXPGained = reward;

        int oldLevel = accountLevel;
        kernwissen += reward;
        accountXP += reward;
        accountLevel = CalculateAccountLevel(accountXP);
        lastRunAccountLevelsGained = Mathf.Max(0, accountLevel - oldLevel);

        highestWaveEver = Mathf.Max(highestWaveEver, highestWaveReachedThisRun);
        highestChaosLevelEver = Mathf.Max(highestChaosLevelEver, highestChaosLevelReachedThisRun);
        AccumulateRunStatistics();
        SaveProfile();
    }

    public List<GeneralMetaNodeDefinition> GetDefinitions()
    {
        EnsureDefinitions();
        return definitions;
    }

    public GeneralMetaNodeDefinition GetDefinition(string nodeId)
    {
        EnsureDefinitions();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        GeneralMetaNodeDefinition definition;
        return definitionById.TryGetValue(nodeId, out definition) ? definition : null;
    }

    public GeneralMetaNodeState GetNodeState(string nodeId)
    {
        EnsureStates();

        if (string.IsNullOrEmpty(nodeId))
            return null;

        GeneralMetaNodeState state;
        return stateById.TryGetValue(nodeId, out state) ? state : null;
    }

    public bool IsNodePurchased(string nodeId)
    {
        GeneralMetaNodeState state = GetNodeState(nodeId);
        return state != null && state.purchased;
    }

    public bool IsNodeActive(string nodeId)
    {
        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);
        GeneralMetaNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased)
            return false;

        return definition.RequiresLoadoutSlot() ? state.active : true;
    }

    public bool CanPurchaseNode(string nodeId)
    {
        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return false;

        GeneralMetaNodeState state = GetNodeState(nodeId);
        if (state != null && state.purchased)
            return false;

        if (accountLevel < definition.requiredAccountLevel)
            return false;

        if (kernwissen < definition.cost)
            return false;

        return AreSpecialRequirementsMet(definition);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        if (!CanPurchaseNode(nodeId))
            return false;

        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);
        GeneralMetaNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null)
            return false;

        kernwissen -= Mathf.Max(0, definition.cost);
        state.purchased = true;
        state.active = !definition.RequiresLoadoutSlot();
        SaveProfile();
        return true;
    }

    public bool CanActivateNode(string nodeId)
    {
        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);
        GeneralMetaNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !state.purchased || state.active || !definition.RequiresLoadoutSlot())
            return false;

        return GetUsedLoadoutSlots() + definition.slotCost <= GetLoadoutSlotCapacity();
    }

    public bool TryActivateNode(string nodeId)
    {
        if (!CanActivateNode(nodeId))
            return false;

        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);
        GeneralMetaNodeState state = GetNodeState(nodeId);
        if (definition == null || state == null)
            return false;

        state.active = true;
        SaveProfile();
        return true;
    }

    public bool TryDeactivateNode(string nodeId)
    {
        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);
        GeneralMetaNodeState state = GetNodeState(nodeId);

        if (definition == null || state == null || !definition.RequiresLoadoutSlot() || !state.active)
            return false;

        state.active = false;
        SaveProfile();
        return true;
    }

    public bool IsTowerUnlocked(TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Basic:
                return IsNodePurchased("general.tower.basic");
            case TowerRole.Rapid:
                return IsNodePurchased("general.tower.rapid");
            case TowerRole.Heavy:
                return IsNodePurchased("general.tower.heavy");
            case TowerRole.Slow:
                return IsNodePurchased("general.tower.slow");
            case TowerRole.Fire:
                return IsNodePurchased("general.tower.fire");
            case TowerRole.Poison:
                return IsNodePurchased("general.tower.poison");
            case TowerRole.Sniper:
                return IsNodePurchased("general.tower.sniper");
            case TowerRole.Alchemist:
                return IsNodePurchased("general.tower.alchemist");
            case TowerRole.Lightning:
                return IsNodePurchased("general.tower.lightning");
            case TowerRole.Mortar:
                return IsNodePurchased("general.tower.mortar");
            case TowerRole.Spike:
                return IsNodePurchased("general.tower.spike");
            case TowerRole.Beam:
            case TowerRole.Support:
            case TowerRole.Frost:
                return false;
            default:
                return false;
        }
    }

    public bool IsTileUnlocked(PathBuildOptionType optionType)
    {
        switch (optionType)
        {
            case PathBuildOptionType.PathTile:
                return IsNodePurchased("general.tile.path");
            case PathBuildOptionType.TrapTile:
                return IsNodePurchased("general.tile.trap");
            case PathBuildOptionType.GoldTile:
                return IsNodePurchased("general.tile.gold");
            case PathBuildOptionType.SlowTile:
                return IsNodePurchased("general.tile.slow");
            case PathBuildOptionType.KnockTile:
                return IsNodePurchased("general.tile.knock");
            case PathBuildOptionType.RangeTile:
                return IsNodePurchased("general.tile.range");
            case PathBuildOptionType.XPTile:
                return IsNodePurchased("general.tile.xp");
            case PathBuildOptionType.DamageTile:
                return IsNodePurchased("general.tile.damage");
            case PathBuildOptionType.RateTile:
                return IsNodePurchased("general.tile.rate");
            case PathBuildOptionType.UpgradeTile:
                return IsNodePurchased("general.tile.upgrade");
            case PathBuildOptionType.ComboTile:
                return IsNodePurchased("general.tile.combo");
            case PathBuildOptionType.HealTile:
                return IsNodePurchased("general.tile.heal");
            case PathBuildOptionType.WeakpointTile:
                return IsNodePurchased("general.tile.weakpoint");
            default:
                return false;
        }
    }

    public int GetLoadoutSlotCapacity()
    {
        int slots = Mathf.Max(0, baseLoadoutSlots);

        if (IsNodePurchased("general.loadout.slot_2")) slots++;
        if (IsNodePurchased("general.loadout.slot_3")) slots++;
        if (IsNodePurchased("general.loadout.slot_4")) slots++;
        if (IsNodePurchased("general.loadout.slot_5")) slots++;
        if (IsNodePurchased("general.loadout.slot_6")) slots++;
        if (IsNodePurchased("general.loadout.slot_7")) slots++;
        if (IsNodePurchased("general.loadout.slot_8")) slots++;
        if (IsNodePurchased("general.loadout.slot_9")) slots++;
        if (IsNodePurchased("general.loadout.slot_10")) slots++;
        if (IsNodePurchased("general.loadout.slot_11")) slots++;
        if (IsNodePurchased("general.loadout.slot_12")) slots++;

        return Mathf.Clamp(slots, 0, Mathf.Max(baseLoadoutSlots, maxLoadoutSlots));
    }

    public int GetUsedLoadoutSlots()
    {
        EnsureStates();
        int used = 0;

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot())
                continue;

            GeneralMetaNodeState state = GetNodeState(definition.nodeId);
            if (state != null && state.purchased && state.active)
                used += Mathf.Max(0, definition.slotCost);
        }

        return used;
    }

    public int GetAvailableLoadoutSlots()
    {
        return Mathf.Max(0, GetLoadoutSlotCapacity() - GetUsedLoadoutSlots());
    }

    public int GetLoadoutProfileCount()
    {
        return LoadoutProfileCount;
    }

    public int GetActiveLoadoutIndex()
    {
        return Mathf.Clamp(activeLoadoutIndex, 0, LoadoutProfileCount - 1);
    }

    public string GetLoadoutDisplayName(int loadoutIndex)
    {
        int safeIndex = Mathf.Clamp(loadoutIndex, 0, LoadoutProfileCount - 1);
        return "Loadout " + (safeIndex + 1);
    }

    public bool SelectLoadout(int loadoutIndex)
    {
        int safeIndex = Mathf.Clamp(loadoutIndex, 0, LoadoutProfileCount - 1);
        SaveActiveLoadoutState();

        activeLoadoutIndex = safeIndex;
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "ActiveLoadout", activeLoadoutIndex);
        LoadActiveLoadoutState();
        SaveProfile();
        return true;
    }

    public bool IsNodeActiveInLoadout(string nodeId, int loadoutIndex)
    {
        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);
        GeneralMetaNodeState state = GetNodeState(nodeId);
        if (definition == null || state == null || !state.purchased)
            return false;

        if (!definition.RequiresLoadoutSlot())
            return true;

        int safeIndex = Mathf.Clamp(loadoutIndex, 0, LoadoutProfileCount - 1);
        string key = GetLoadoutActiveKey(safeIndex, nodeId);
        if (PlayerPrefs.HasKey(key))
            return PlayerPrefs.GetInt(key, 0) == 1;

        return safeIndex == activeLoadoutIndex && state.active;
    }

    public int GetUsedLoadoutSlots(int loadoutIndex)
    {
        EnsureStates();
        int used = 0;

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot())
                continue;

            if (IsNodeActiveInLoadout(definition.nodeId, loadoutIndex))
                used += Mathf.Max(0, definition.slotCost);
        }

        return used;
    }

    public int GetAvailableLoadoutSlots(int loadoutIndex)
    {
        return Mathf.Max(0, GetLoadoutSlotCapacity() - GetUsedLoadoutSlots(loadoutIndex));
    }

    public int GetActiveStartGoldBonus()
    {
        int bonus = 0;
        if (IsNodeActive("general.start.gold_1")) bonus += 10;
        if (IsNodeActive("general.start.gold_2")) bonus += 20;
        if (IsNodeActive("general.start.gold_3")) bonus += 30;
        if (IsNodeActive("general.start.gold_4")) bonus += 40;
        if (IsNodeActive("general.start.gold_5")) bonus += 50;
        return bonus;
    }

    public int GetActiveStartLifeBonus()
    {
        int bonus = 0;
        if (IsNodeActive("general.start.life_1")) bonus += 1;
        if (IsNodeActive("general.start.life_2")) bonus += 2;
        if (IsNodeActive("general.start.life_3")) bonus += 3;
        if (IsNodeActive("general.start.life_4")) bonus += 4;
        if (IsNodeActive("general.start.life_5")) bonus += 5;
        return bonus;
    }

    public int GetActiveStartXPBonus()
    {
        int bonus = 0;
        if (IsNodeActive("general.start.xp_1")) bonus += 5;
        if (IsNodeActive("general.start.xp_2")) bonus += 10;
        if (IsNodeActive("general.start.xp_3")) bonus += 15;
        if (IsNodeActive("general.start.xp_4")) bonus += 20;
        if (IsNodeActive("general.start.xp_5")) bonus += 25;
        return bonus;
    }

    public bool TryGrantStartXPToFirstTower(Tower tower)
    {
        if (tower == null || startXPGrantedThisRun)
            return false;

        int bonus = GetActiveStartXPBonus();
        if (bonus <= 0)
            return false;

        startXPGrantedThisRun = true;
        tower.AddXP(bonus);
        Debug.Log("Start-XP aktiviert: " + tower.towerName + " +" + bonus + " XP.");
        return true;
    }

    public int GetActiveStartPathBonus()
    {
        if (IsNodeActive("general.start.path_2")) return 2;
        if (IsNodeActive("general.start.path_1")) return 1;
        return 0;
    }

    public bool HasStartProtection()
    {
        return IsNodeActive("general.start.protection_1");
    }

    public int ApplyStartProtectionToLifeLoss(int lifeLoss)
    {
        int safeLoss = Mathf.Max(0, lifeLoss);
        if (safeLoss <= 0 || startProtectionUsedThisRun || !HasStartProtection())
            return safeLoss;

        startProtectionUsedThisRun = true;
        return Mathf.Max(0, safeLoss - 1);
    }

    public int GetEmergencyReserveGold()
    {
        return IsNodeActive("general.start.reserve_1") ? 120 : 0;
    }

    public bool TryTriggerEmergencyReserve(GameManager targetGameManager, int currentLives)
    {
        int reserveGold = GetEmergencyReserveGold();
        if (emergencyReserveUsedThisRun || reserveGold <= 0 || currentLives >= 5 || targetGameManager == null)
            return false;

        emergencyReserveUsedThisRun = true;
        targetGameManager.AddGold(reserveGold, true, RunGoldSource.Other);
        Debug.Log("Notreserve aktiviert: +" + reserveGold + " Gold.");
        return true;
    }

    public bool CanUseFastSpeed()
    {
        return IsNodeActive("general.qol.speed_fast");
    }

    public bool CanUseMediumSpeed()
    {
        return IsNodeActive("general.qol.speed_medium");
    }

    public bool CanUseFasterSpeed()
    {
        return IsNodeActive("general.qol.speed_faster");
    }

    public bool HasDPSDisplay()
    {
        return IsNodePurchased("general.qol.dps_display");
    }

    public bool HasRolePreviewI()
    {
        return false;
    }

    public bool HasRolePreviewII()
    {
        return false;
    }

    public bool HasBossPreview()
    {
        return false;
    }

    public bool HasChaosPreview()
    {
        return false;
    }

    public bool HasChaosWavePreview()
    {
        return false;
    }

    public int GetGoalPinCapacity()
    {
        return 0;
    }

    public bool IsStartScoutActive()
    {
        return false;
    }

    public int GetAvailableFirstTowerDiscount()
    {
        if (firstTowerDiscountUsedThisRun)
            return 0;

        int discount = 0;
        if (IsNodeActive("general.start.discount_1")) discount += 10;
        if (IsNodeActive("general.start.discount_2")) discount += 10;
        if (IsNodeActive("general.start.discount_3")) discount += 10;
        if (IsNodeActive("general.start.discount_4")) discount += 10;
        if (IsNodeActive("general.start.discount_5")) discount += 10;
        return discount;
    }

    public int GetBuildCostAfterStartOptions(int currentCost)
    {
        return Mathf.Max(0, Mathf.Max(0, currentCost) - GetAvailableFirstTowerDiscount());
    }

    public void ConsumeFirstTowerDiscountIfAvailable(int costBeforeDiscount)
    {
        if (costBeforeDiscount <= 0 || GetAvailableFirstTowerDiscount() <= 0)
            return;

        firstTowerDiscountUsedThisRun = true;
    }

    public int GetXPToNextAccountLevel()
    {
        return GetXPToNextAccountLevel(accountLevel);
    }

    public int GetAccountXPIntoCurrentLevel()
    {
        return GetXPIntoLevel(accountXP);
    }

    public string GetTopBarSummary()
    {
        return "Account Lv " + accountLevel +
               " | Kernwissen " + kernwissen +
               " | XP " + GetAccountXPIntoCurrentLevel() + "/" + GetXPToNextAccountLevel() +
               " | Loadout " + GetUsedLoadoutSlots() + "/" + GetLoadoutSlotCapacity() +
               " | letzte Auszahlung +" + lastRunKernwissenGained;
    }

    public string GetAccountOverviewText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>Account-Uebersicht</b>");
        builder.AppendLine("Account Level: " + accountLevel);
        builder.AppendLine("Kernwissen: " + kernwissen);
        builder.AppendLine("Account-XP: " + GetAccountXPIntoCurrentLevel() + " / " + GetXPToNextAccountLevel());
        builder.AppendLine("Loadout-Slots: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
        builder.AppendLine();
        builder.AppendLine("<b>Letzter Run</b>");
        builder.AppendLine("+ " + lastRunKernwissenGained + " Kernwissen");
        builder.AppendLine("+ " + lastRunAccountXPGained + " Account-XP");
        builder.AppendLine("Account-Level gewonnen: " + lastRunAccountLevelsGained);
        builder.AppendLine();
        builder.AppendLine("<b>Naechste allgemeine Meilensteine</b>");
        AppendNextMilestones(builder);
        builder.AppendLine();
        builder.AppendLine("Regel: Kernwissen wird ausgegeben. Account-XP bleibt als dauerhafter Level-Fortschritt erhalten.");
        return builder.ToString();
    }

    public string GetNodeStateText(GeneralMetaNodeDefinition definition)
    {
        if (definition == null)
            return "Unbekannt";

        GeneralMetaNodeState state = GetNodeState(definition.nodeId);

        if (state != null && state.purchased)
        {
            if (definition.RequiresLoadoutSlot())
                return state.active ? "Aktiv | Slot " + definition.slotCost : "Gekauft | Slot " + definition.slotCost;

            return definition.kind == GeneralMetaNodeKind.QoL || definition.kind == GeneralMetaNodeKind.EnemyInfo
                ? "Immer aktiv"
                : "Freigeschaltet";
        }

        if (CanPurchaseNode(definition.nodeId))
            return "Kaufbar | " + definition.cost + " Kernwissen";

        if (accountLevel < definition.requiredAccountLevel)
            return "Gesperrt | Account Lv " + definition.requiredAccountLevel;

        if (!AreSpecialRequirementsMet(definition))
            return "Gesperrt | Bedingung";

        if (kernwissen < definition.cost)
            return "Kernwissen fehlt | " + definition.cost;

        return "Gesperrt";
    }

    public string GetNodeDetailText(string nodeId)
    {
        GeneralMetaNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return "Allgemeiner Meta-Knoten nicht gefunden.";

        GeneralMetaNodeState state = GetNodeState(nodeId);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>" + definition.displayName + "</b>");
        builder.AppendLine("<size=90%><color=#B9C2D0>" + GetNodeStateText(definition) + "</color></size>");
        builder.AppendLine();
        builder.AppendLine("Kategorie: " + GetCategoryDisplayName(definition.category));
        builder.AppendLine("Typ: " + GetKindDisplayName(definition.kind));
        builder.AppendLine("Kosten: " + definition.cost + " Kernwissen");
        builder.AppendLine("Account-Level: " + definition.requiredAccountLevel);

        if (definition.RequiresLoadoutSlot())
        {
            builder.AppendLine("Slot-Kosten: " + definition.slotCost);
            builder.AppendLine("Loadout: " + GetUsedLoadoutSlots() + " / " + GetLoadoutSlotCapacity());
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
            builder.AppendLine(state.active ? "Aktiv fuer den naechsten Run." : "Gekauft, aber nicht im Loadout aktiv.");

        return builder.ToString();
    }

    public static string GetCategoryDisplayName(GeneralMetaCategory category)
    {
        switch (category)
        {
            case GeneralMetaCategory.Account: return "Account-Uebersicht";
            case GeneralMetaCategory.TowerUnlock: return "Tower-Freischaltungen";
            case GeneralMetaCategory.TileUnlock: return "Tile-Freischaltungen";
            case GeneralMetaCategory.QoL: return "Komfort / QoL";
            case GeneralMetaCategory.StartOption: return "Startoptionen";
            case GeneralMetaCategory.EnemyResearch: return "Gegnerforschung";
            case GeneralMetaCategory.MetaLoadout: return "Meta-Loadout";
            default: return category.ToString();
        }
    }

    public static string GetKindDisplayName(GeneralMetaNodeKind kind)
    {
        switch (kind)
        {
            case GeneralMetaNodeKind.ContentUnlock: return "Content-Unlock";
            case GeneralMetaNodeKind.TileUnlock: return "Tile-Pool-Unlock";
            case GeneralMetaNodeKind.QoL: return "QoL, immer aktiv";
            case GeneralMetaNodeKind.StartPower: return "Start-Power, Loadout";
            case GeneralMetaNodeKind.EnemyInfo: return "Info-Forschung";
            case GeneralMetaNodeKind.EnemyPower: return "Power-Forschung, Loadout";
            case GeneralMetaNodeKind.LoadoutSlot: return "Loadout-Budget";
            default: return kind.ToString();
        }
    }

    private void DeactivateExclusiveLoadoutGroup(GeneralMetaNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || definition.nodeId == activatedDefinition.nodeId || !definition.RequiresLoadoutSlot())
                continue;

            if (GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            GeneralMetaNodeState state = GetNodeState(definition.nodeId);
            if (state != null)
                state.active = false;
        }
    }

    private int GetActiveSlotsInExclusiveGroup(GeneralMetaNodeDefinition activatedDefinition)
    {
        if (activatedDefinition == null || !activatedDefinition.RequiresLoadoutSlot())
            return 0;

        string group = GetExclusiveLoadoutGroup(activatedDefinition.nodeId);
        int slots = 0;

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || GetExclusiveLoadoutGroup(definition.nodeId) != group)
                continue;

            GeneralMetaNodeState state = GetNodeState(definition.nodeId);
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

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || runFinalized || IsMetaProgressionSuppressedForCurrentRun())
            return;

        int reward = Mathf.Max(0, kernwissenPerCompletedWave);

        if (result.isMiniBossWave)
            reward += Mathf.Max(0, miniBossWaveKernwissenBonus);

        if (result.isBossWave)
            reward += Mathf.Max(0, bossWaveKernwissenBonus);

        if (result.bossDefeated)
            reward += Mathf.Max(0, bossKillKernwissenBonus);

        if (result.enemiesReachedBase <= 0 && result.baseDamageTaken <= 0)
        {
            reward += Mathf.Max(0, perfectWaveKernwissenBonus);

            if (result.isBossWave)
                reward += Mathf.Max(0, perfectBossWaveKernwissenBonus);
        }

        if (result.waveNumber > Mathf.Max(highestWaveEver, highestWaveReachedThisRun))
            reward += Mathf.Max(0, newHighestWaveKernwissenBonus);

        int knownChaos = Mathf.Max(highestChaosLevelEver, highestChaosLevelReachedThisRun);
        if (result.chaosLevelAtWaveStart > knownChaos)
            reward += (result.chaosLevelAtWaveStart - knownChaos) * Mathf.Max(0, firstChaosLevelKernwissenBonus);

        if (result.eliteDefeated)
            reward += Mathf.Max(0, eliteKillKernwissenBonus);

        pendingRunKernwissen += Mathf.Max(0, reward);
        highestWaveReachedThisRun = Mathf.Max(highestWaveReachedThisRun, result.waveNumber);
        highestChaosLevelReachedThisRun = Mathf.Max(highestChaosLevelReachedThisRun, result.chaosLevelAtWaveStart);
    }

    private void HandleGameOverTriggered()
    {
        FinalizeRun();
    }

    private bool IsMetaProgressionSuppressedForCurrentRun()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager != null && gameManager.IsMetaProgressionSuppressedForCurrentRun();
    }

    private void AccumulateRunStatistics()
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
        {
            totalGoldEarnedEver += Mathf.Max(0, stats.economy != null ? stats.economy.totalGoldEarned : 0);
            totalTowersBuiltEver += Mathf.Max(0, stats.towersBuilt);
            totalTowerLevelUpsEver += Mathf.Max(0, stats.totalTowerLevelUps);
            totalUpgradePointsEarnedEver += Mathf.Max(0, stats.totalUpgradePointsGained);
            blockedEventsChosenEver += Mathf.Max(0, stats.blockedEventsChosen);
        }

        WaveHistory history = GetWaveHistory();
        if (history != null && history.completedWaves != null)
        {
            int runnerKills = 0;
            foreach (WaveCompletionResult result in history.completedWaves)
            {
                if (result == null)
                    continue;

                runnerKills += result.GetKilledRoleCount(EnemyRole.Runner);
            }

            totalRunnerKillsEver += Mathf.Max(0, runnerKills);
            totalBossKillsEver += Mathf.Max(0, history.GetBossKills());
        }
    }

    private bool AreSpecialRequirementsMet(GeneralMetaNodeDefinition definition)
    {
        if (definition == null)
            return false;

        string id = definition.nodeId;
        WaveHistory history = GetWaveHistory();
        int highestWave = Mathf.Max(highestWaveEver, history != null ? history.GetHighestWaveNumberReached() : 0);

        switch (id)
        {
            case "general.tower.fire":
                return highestWave >= 3;
            case "general.tower.poison":
                return HasSeenScenario(WaveScenario.TankIntro) || highestWave >= 4;
            case "general.tower.sniper":
                return HasSeenScenario(WaveScenario.MageIntro) || highestWave >= 7;
            case "general.tower.alchemist":
                return IsNodePurchased("general.tower.fire") && IsNodePurchased("general.tower.poison");
            case "general.tower.lightning":
                return HasSeenScenario(WaveScenario.Mixed) || highestWave >= 16;
            case "general.tower.mortar":
                return highestWave >= 20;
            case "general.tower.spike":
                return IsNodePurchased("general.tile.trap") && (IsNodePurchased("general.tower.alchemist") || IsNodePurchased("general.tile.combo"));
            case "general.tower.beam":
                return highestWave >= 25;
            case "general.tower.support":
                return totalTowersBuiltEver >= 40 || (GetRunStatisticsTracker() != null && GetRunStatisticsTracker().towersBuilt >= 40);
            case "general.tower.frost":
                return IsNodePurchased("general.tower.slow") && highestWave >= 18;
            case "general.tile.trap":
                return highestWave >= 5;
            case "general.tile.gold":
                return GetTotalRunnerKillsIncludingCurrentHistory() >= 20 || highestWave >= 6;
            case "general.tile.slow":
                return true;
            case "general.tile.knock":
                return true;
            case "general.tile.range":
                return totalTowersBuiltEver >= 20 || (GetRunStatisticsTracker() != null && GetRunStatisticsTracker().towersBuilt >= 20);
            case "general.tile.xp":
                return totalTowerLevelUpsEver >= 20 || (GetRunStatisticsTracker() != null && GetRunStatisticsTracker().totalTowerLevelUps >= 20);
            case "general.tile.damage":
                return totalBossKillsEver >= 1 || (history != null && history.GetBossKills() >= 1);
            case "general.tile.rate":
                return GetTotalRunnerKillsIncludingCurrentHistory() >= 50;
            case "general.tile.upgrade":
                return totalUpgradePointsEarnedEver >= 30 || (GetRunStatisticsTracker() != null && GetRunStatisticsTracker().totalUpgradePointsGained >= 30);
            case "general.tile.combo":
                return IsNodePurchased("general.tower.fire") && IsNodePurchased("general.tower.poison") && (IsNodePurchased("general.tower.spike") || IsNodePurchased("general.tower.alchemist"));
            case "general.tile.heal":
                return totalBossKillsEver >= 1 || (history != null && history.GetBossKills() >= 1) || highestWave >= 18;
            case "general.tile.weakpoint":
                return IsNodePurchased("general.tile.damage") || highestWave >= 22;
            default:
                return true;
        }
    }

    private string GetRequirementProgressText(GeneralMetaNodeDefinition definition)
    {
        if (definition == null)
            return "Keine Daten.";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Account-Level: " + Mathf.Min(accountLevel, definition.requiredAccountLevel) + " / " + definition.requiredAccountLevel);
        builder.AppendLine("Kernwissen: " + Mathf.Min(kernwissen, definition.cost) + " / " + definition.cost);

        string id = definition.nodeId;
        WaveHistory history = GetWaveHistory();
        int highestWave = Mathf.Max(highestWaveEver, history != null ? history.GetHighestWaveNumberReached() : 0);

        if (id == "general.tower.slow")
            builder.AppendLine("Kernwissen-Freischaltung: bereit");
        else if (id == "general.tower.fire")
            builder.AppendLine("Wave 3 erreicht: " + FormatYesNo(highestWave >= 3));
        else if (id == "general.tower.poison")
            builder.AppendLine("TankIntro gesehen: " + FormatYesNo(HasSeenScenario(WaveScenario.TankIntro) || highestWave >= 4));
        else if (id == "general.tower.sniper")
            builder.AppendLine("MageIntro gesehen: " + FormatYesNo(HasSeenScenario(WaveScenario.MageIntro) || highestWave >= 7));
        else if (id == "general.tower.alchemist")
            builder.AppendLine("Fire + Poison freigeschaltet: " + FormatYesNo(IsNodePurchased("general.tower.fire") && IsNodePurchased("general.tower.poison")));
        else if (id == "general.tower.lightning")
            builder.AppendLine("Mixed-Wave geschafft: " + FormatYesNo(HasSeenScenario(WaveScenario.Mixed) || highestWave >= 16));
        else if (id == "general.tower.mortar")
            builder.AppendLine("Wave 20 erreicht: " + Mathf.Min(highestWave, 20) + " / 20");
        else if (id == "general.tower.spike")
            builder.AppendLine("Trap/Combo-System bereit: " + FormatYesNo(IsNodePurchased("general.tile.trap") && (IsNodePurchased("general.tower.alchemist") || IsNodePurchased("general.tile.combo"))));
        else if (id == "general.tower.beam")
            builder.AppendLine("Wave 25 erreicht: " + Mathf.Min(highestWave, 25) + " / 25");
        else if (id == "general.tower.support")
            builder.AppendLine("Tower gebaut: " + Mathf.Min(totalTowersBuiltEver, 40) + " / 40");
        else if (id == "general.tower.frost")
            builder.AppendLine("Slow Tower + Wave 18: " + FormatYesNo(IsNodePurchased("general.tower.slow")) + " | " + Mathf.Min(highestWave, 18) + " / 18");
        else if (id == "general.tile.trap")
            builder.AppendLine("Wave 5 erreicht: " + Mathf.Min(highestWave, 5) + " / 5");
        else if (id == "general.tile.gold")
            builder.AppendLine("Runner-Kills oder Wave 6: " + Mathf.Min(GetTotalRunnerKillsIncludingCurrentHistory(), 20) + " / 20 | Wave " + Mathf.Min(highestWave, 6) + " / 6");
        else if (id == "general.tile.slow")
            builder.AppendLine("Common-Start-Tile: freigeschaltet");
        else if (id == "general.tile.knock")
            builder.AppendLine("Common-Start-Tile: freigeschaltet");
        else if (id == "general.tile.range")
            builder.AppendLine("Tower gebaut: " + Mathf.Min(totalTowersBuiltEver, 20) + " / 20");
        else if (id == "general.tile.xp")
            builder.AppendLine("Tower-Level-Ups: " + Mathf.Min(totalTowerLevelUpsEver, 20) + " / 20");
        else if (id == "general.tile.damage")
            builder.AppendLine("Boss 1 besiegt: " + FormatYesNo(totalBossKillsEver >= 1 || (history != null && history.GetBossKills() >= 1)));
        else if (id == "general.tile.rate")
            builder.AppendLine("Runner-Kills: " + Mathf.Min(GetTotalRunnerKillsIncludingCurrentHistory(), 50) + " / 50");
        else if (id == "general.tile.upgrade")
            builder.AppendLine("Upgrade Points verdient: " + Mathf.Min(totalUpgradePointsEarnedEver, 30) + " / 30");
        else if (id == "general.tile.combo")
            builder.AppendLine("Fire + Poison + Spike/Alchemist: " + FormatYesNo(IsNodePurchased("general.tower.fire") && IsNodePurchased("general.tower.poison") && (IsNodePurchased("general.tower.spike") || IsNodePurchased("general.tower.alchemist"))));
        else if (id == "general.tile.heal")
            builder.AppendLine("Boss besiegt oder Wave 18: " + FormatYesNo(totalBossKillsEver >= 1 || (history != null && history.GetBossKills() >= 1)) + " | " + Mathf.Min(highestWave, 18) + " / 18");
        else if (id == "general.tile.weakpoint")
            builder.AppendLine("Damage Tile oder Wave 22: " + FormatYesNo(IsNodePurchased("general.tile.damage")) + " | " + Mathf.Min(highestWave, 22) + " / 22");
        else
            builder.AppendLine("Spezialbedingung: " + FormatYesNo(AreSpecialRequirementsMet(definition)));

        return builder.ToString();
    }

    private void AppendNextMilestones(StringBuilder builder)
    {
        int added = 0;

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || IsNodePurchased(definition.nodeId))
                continue;

            if (definition.requiredAccountLevel < accountLevel)
                continue;

            builder.AppendLine("- Account Level " + definition.requiredAccountLevel + ": " + definition.displayName + " kaufbar");
            added++;

            if (added >= 4)
                break;
        }

        if (added == 0)
            builder.AppendLine("- Keine naechsten Milestones in der aktuellen Datenbasis offen.");
    }

    private int GetTotalRunnerKillsIncludingCurrentHistory()
    {
        int total = Mathf.Max(0, totalRunnerKillsEver);
        WaveHistory history = GetWaveHistory();

        if (history == null || history.completedWaves == null)
            return total;

        foreach (WaveCompletionResult result in history.completedWaves)
        {
            if (result != null)
                total += Mathf.Max(0, result.GetKilledRoleCount(EnemyRole.Runner));
        }

        return total;
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

    private string FormatYesNo(bool value)
    {
        return value ? "erfuellt" : "offen";
    }

    private int CalculateAccountLevel(int totalXP)
    {
        int level = 1;
        int remaining = Mathf.Max(0, totalXP);

        while (level < 999)
        {
            int needed = GetXPToNextAccountLevel(level);
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

        while (level < accountLevel)
        {
            remaining -= GetXPToNextAccountLevel(level);
            level++;
        }

        return Mathf.Max(0, remaining);
    }

    private int GetXPToNextAccountLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        return 80 + safeLevel * 40 + Mathf.FloorToInt(safeLevel * safeLevel * 2f);
    }

    private WaveHistory GetWaveHistory()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetWaveHistory() : null;
    }

    private RunStatisticsTracker GetRunStatisticsTracker()
    {
        GameManager currentGameManager = GetGameManager();
        return currentGameManager != null ? currentGameManager.GetRunStatisticsTracker() : FindObjectOfType<RunStatisticsTracker>();
    }

    private GameManager GetGameManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return gameManager;
    }

    private void EnsureDefinitions()
    {
        if (definitions == null)
            definitions = new List<GeneralMetaNodeDefinition>();

        if (definitions.Count == 0)
            CreateDefaultDefinitions();

        ApplyCoreDefinitionMigrations();
        RebuildDefinitionLookup();
    }

    private void EnsureStates()
    {
        EnsureDefinitions();

        if (nodeStates == null)
            nodeStates = new List<GeneralMetaNodeState>();

        RebuildStateLookup();

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            if (stateById.ContainsKey(definition.nodeId))
                continue;

            GeneralMetaNodeState state = new GeneralMetaNodeState
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

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            definitionById[definition.nodeId] = definition;
        }
    }

    private void ApplyCoreDefinitionMigrations()
    {
        EnsureDefinition("general.tower.basic", "Basic Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 0, 1, "Guenstiger Allrounder.", 0, true, "");
        EnsureDefinition("general.tower.rapid", "Rapid Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 0, 1, "Schnelle Schuesse.", 0, true, "");
        EnsureDefinition("general.tower.heavy", "Heavy Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 0, 1, "Langsam, hoher Schaden.", 0, true, "");
        EnsureDefinition("general.tower.slow", "Slow Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 70, 2, "Verlangsamt Gegner.", 0, false, "Mit Kernwissen freischalten.");
        EnsureDefinition("general.tower.fire", "Fire Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 80, 2, "Verbrennt Gruppen.", 0, false, "Wave 3 erreicht.");
        EnsureDefinition("general.tower.poison", "Poison Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 100, 3, "Gift ueber Zeit.", 0, false, "TankIntro geschafft.");
        EnsureDefinition("general.tower.sniper", "Sniper Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 220, 7, "Sehr hohe Reichweite.", 0, false, "MageIntro gesehen.");
        EnsureDefinition("general.tower.alchemist", "Alchemist Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 350, 13, "Gift plus Verlangsamung.", 0, false, "Fire + Poison freigeschaltet.");
        EnsureDefinition("general.tower.lightning", "Lightning Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 450, 16, "Blitze springen weiter.", 0, false, "Mixed-Wave geschafft.");
        EnsureDefinition("general.tower.mortar", "Mortar Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 600, 20, "Flaechenschaden.", 0, false, "Wave 20 erreicht.");
        EnsureDefinition("general.tower.spike", "Spike Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 750, 24, "Blutung am Weg.", 0, false, "Trap/Combo-System freigeschaltet.");
        RemoveDefinition("general.tower.beam");
        RemoveDefinition("general.tower.support");
        RemoveDefinition("general.tower.frost");
        RemoveDefinition("general.tile.bridge");
        RemoveDefinition("general.start.scout_1");
        RemoveDefinition("general.qol.preview_roles_1");
        RemoveDefinition("general.qol.preview_roles_2");
        RemoveDefinition("general.qol.preview_boss");
        RemoveDefinition("general.qol.preview_chaos_1");
        RemoveDefinition("general.qol.preview_chaos_wave");
        RemoveDefinition("general.qol.goal_pin_1");
        RemoveDefinition("general.qol.goal_pin_2");
        EnsureDefinition("general.tile.path", "Path Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 0, 1, "Erweitert den Weg.", 0, true, "");
        EnsureDefinition("general.tile.gold", "Gold Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 160, 6, "Mehr Gold-Rewards.", 0, false, "20 Runner-Kills oder Wave 6 erreicht.");
        EnsureDefinition("general.tile.slow", "Slow Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 0, 1, "Verlangsamt Gegner.", 0, true, "");
        EnsureDefinition("general.tile.trap", "Trap Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 70, 2, "Schadet Gegnern.", 0, false, "Wave 5 erreicht.");
        EnsureDefinition("general.tile.knock", "Knock Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 0, 1, "Stoesst Gegner zurueck.", 0, true, "");
        EnsureDefinition("general.tile.range", "Range Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 220, 10, "Tower +1 Reichweite.", 0, false, "20 Tower gebaut.");
        EnsureDefinition("general.tile.damage", "Damage Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 340, 15, "Tower +25% Schaden.", 0, false, "Boss 1 besiegt.");
        EnsureDefinition("general.tile.rate", "Rate Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 380, 17, "Tower feuert schneller.", 0, false, "50 Runner-Kills.");
        EnsureDefinition("general.tile.xp", "XP Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 260, 12, "Tower erhaelt mehr XP.", 0, false, "20 Tower-Level-Ups.");
        EnsureDefinition("general.tile.upgrade", "Upgrade Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 450, 19, "Upgrades werden staerker.", 0, false, "30 Upgrade Points verdient.");
        EnsureDefinition("general.tile.combo", "Combo Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 700, 24, "Kombiniert Tile-Effekte.", 0, false, "Fire + Poison + Spike/Alchemist freigeschaltet.");
        EnsureDefinition("general.tile.heal", "Heal Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 520, 21, "2% Kill-Chance auf Leben.", 0, false, "Boss besiegt oder Wave 18.");
        EnsureDefinition("general.tile.weakpoint", "Weakpoint Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 620, 23, "Ruestung max. 2 und -50%.", 0, false, "Damage Tile oder Wave 22.");

        EnsureDefinition("general.loadout.slot_2", "Loadout Slot II", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 80, 2, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_3", "Loadout Slot III", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 150, 5, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_4", "Loadout Slot IV", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 300, 10, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_5", "Loadout Slot V", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 600, 15, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_6", "Loadout Slot VI", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 1100, 25, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_7", "Loadout Slot VII", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 1800, 40, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_8", "Loadout Slot VIII", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 2800, 60, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_9", "Loadout Slot IX", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 4200, 80, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_10", "Loadout Slot X", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 6000, 100, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_11", "Loadout Slot XI", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 8200, 120, "+1 Loadout-Slot.", 0, false, "");
        EnsureDefinition("general.loadout.slot_12", "Loadout Slot XII", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 10500, 140, "+1 Loadout-Slot.", 0, false, "");

        EnsureDefinition("general.qol.speed_fast", "GameSpeed 2x", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 50, 2, "2x Spieltempo.", 0, false, "");
        EnsureDefinition("general.qol.speed_medium", "GameSpeed 3x", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 140, 7, "3x Spieltempo.", 0, false, "");
        EnsureDefinition("general.qol.speed_faster", "GameSpeed 6x", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 250, 12, "6x Spieltempo.", 0, false, "");
        EnsureDefinition("general.qol.dps_display", "DPS-Anzeige", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 180, 8, "DPS im Towerpanel.", 0, false, "");

        EnsureDefinition("general.start.gold_1", "Startgold I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 120, 4, "+10 Startgold.", 1, false, "");
        EnsureDefinition("general.start.gold_2", "Startgold II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 220, 8, "+20 Startgold.", 1, false, "");
        EnsureDefinition("general.start.gold_3", "Startgold III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 380, 14, "+30 Startgold.", 2, false, "");
        EnsureDefinition("general.start.gold_4", "Startgold IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 600, 22, "+40 Startgold.", 2, false, "");
        EnsureDefinition("general.start.gold_5", "Startgold V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 900, 32, "+50 Startgold.", 3, false, "");
        EnsureDefinition("general.start.life_1", "Startleben I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 150, 5, "+1 Startleben.", 1, false, "");
        EnsureDefinition("general.start.life_2", "Startleben II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 300, 12, "+2 Startleben.", 2, false, "");
        EnsureDefinition("general.start.life_3", "Startleben III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 550, 22, "+3 Startleben.", 2, false, "");
        EnsureDefinition("general.start.life_4", "Startleben IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 850, 35, "+4 Startleben.", 3, false, "");
        EnsureDefinition("general.start.life_5", "Startleben V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 1250, 48, "+5 Startleben.", 3, false, "");
        EnsureDefinition("general.start.xp_1", "Start-XP I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 100, 4, "+5 XP fuer ersten Tower.", 1, false, "");
        EnsureDefinition("general.start.xp_2", "Start-XP II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 180, 8, "+10 XP fuer ersten Tower.", 1, false, "");
        EnsureDefinition("general.start.xp_3", "Start-XP III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 320, 14, "+15 XP fuer ersten Tower.", 1, false, "");
        EnsureDefinition("general.start.xp_4", "Start-XP IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 560, 24, "+20 XP fuer ersten Tower.", 2, false, "");
        EnsureDefinition("general.start.xp_5", "Start-XP V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 850, 36, "+25 XP fuer ersten Tower.", 2, false, "");
        EnsureDefinition("general.start.path_1", "Startweg I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 400, 12, "Startweg +1 Tile.", 2, false, "");
        EnsureDefinition("general.start.path_2", "Startweg II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 950, 28, "Startweg +2 Tiles.", 3, false, "");
        EnsureDefinition("general.start.protection_1", "Startschutz I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 260, 9, "Erster Leak kostet -1 Leben.", 1, false, "");
        EnsureDefinition("general.start.reserve_1", "Notreserve I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 700, 20, "+120 Gold unter 5 Leben.", 2, false, "");
        EnsureDefinition("general.start.discount_1", "Startkosten I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 300, 10, "Erster Tower kostet -10 Gold.", 1, false, "");
        EnsureDefinition("general.start.discount_2", "Startkosten II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 550, 18, "Erster Tower kostet weitere -10 Gold.", 1, false, "");
        EnsureDefinition("general.start.discount_3", "Startkosten III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 850, 28, "Erster Tower kostet weitere -10 Gold.", 1, false, "");
        EnsureDefinition("general.start.discount_4", "Startkosten IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 1200, 40, "Erster Tower kostet weitere -10 Gold.", 1, false, "");
        EnsureDefinition("general.start.discount_5", "Startkosten V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 1700, 55, "Erster Basic Tower kann kostenlos werden.", 1, false, "");
    }

    private void EnsureDefinition(string nodeId, string displayName, GeneralMetaCategory category, GeneralMetaNodeKind kind, int cost, int level, string effectText, int slotCost, bool unlockedByDefault, string requirementText)
    {
        GeneralMetaNodeDefinition definition = FindDefinition(nodeId);

        if (definition == null)
        {
            AddDefinition(nodeId, displayName, category, kind, cost, level, effectText, slotCost, unlockedByDefault, requirementText);
            return;
        }

        definition.displayName = displayName;
        definition.category = category;
        definition.kind = kind;
        definition.cost = Mathf.Max(0, cost);
        definition.requiredAccountLevel = Mathf.Max(1, level);
        definition.slotCost = Mathf.Max(0, slotCost);
        definition.unlockedByDefault = unlockedByDefault;
        definition.effectText = effectText;
        definition.requirementText = requirementText;
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

    private void NormalizeDefinition(string nodeId, int cost, int level, bool unlockedByDefault, string effectText, string requirementText)
    {
        GeneralMetaNodeDefinition definition = FindDefinition(nodeId);

        if (definition == null)
            return;

        definition.cost = Mathf.Max(0, cost);
        definition.requiredAccountLevel = Mathf.Max(1, level);
        definition.unlockedByDefault = unlockedByDefault;
        definition.effectText = effectText;
        definition.requirementText = requirementText;
    }

    private GeneralMetaNodeDefinition FindDefinition(string nodeId)
    {
        if (definitions == null || string.IsNullOrEmpty(nodeId))
            return null;

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition != null && definition.nodeId == nodeId)
                return definition;
        }

        return null;
    }

    private void RebuildStateLookup()
    {
        stateById.Clear();

        foreach (GeneralMetaNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            stateById[state.nodeId] = state;
        }
    }

    private void CreateDefaultDefinitions()
    {
        definitions.Clear();

        AddDefinition("general.tower.basic", "Basic Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 0, 1, "Guenstiger Allrounder.", true);
        AddDefinition("general.tower.rapid", "Rapid Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 0, 1, "Schnelle Schuesse.", true);
        AddDefinition("general.tower.heavy", "Heavy Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 0, 1, "Langsam, hoher Schaden.", true);
        AddDefinition("general.tower.slow", "Slow Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 70, 2, "Verlangsamt Gegner.", false, "Mit Kernwissen freischalten.");
        AddDefinition("general.tower.fire", "Fire Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 80, 2, "Verbrennt Gruppen.", false, "Wave 3 erreicht.");
        AddDefinition("general.tower.poison", "Poison Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 100, 3, "Gift ueber Zeit.", false, "TankIntro geschafft.");
        AddDefinition("general.tower.sniper", "Sniper Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 220, 7, "Sehr hohe Reichweite.", false, "MageIntro gesehen.");
        AddDefinition("general.tower.alchemist", "Alchemist Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 350, 13, "Gift plus Verlangsamung.", false, "Fire + Poison freigeschaltet.");
        AddDefinition("general.tower.lightning", "Lightning Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 450, 16, "Blitze springen weiter.", false, "Mixed-Wave geschafft.");
        AddDefinition("general.tower.mortar", "Mortar Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 600, 20, "Flaechenschaden.", false, "Wave 20 erreicht.");
        AddDefinition("general.tower.spike", "Spike Tower", GeneralMetaCategory.TowerUnlock, GeneralMetaNodeKind.ContentUnlock, 750, 24, "Blutung am Weg.", false, "Trap/Combo-System freigeschaltet.");
        AddDefinition("general.tile.path", "Path Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 0, 1, "Erweitert den Weg.", true);

        AddDefinition("general.loadout.slot_2", "Loadout Slot II", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 80, 2, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_3", "Loadout Slot III", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 150, 5, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_4", "Loadout Slot IV", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 300, 10, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_5", "Loadout Slot V", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 600, 15, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_6", "Loadout Slot VI", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 1100, 25, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_7", "Loadout Slot VII", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 1800, 40, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_8", "Loadout Slot VIII", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 2800, 60, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_9", "Loadout Slot IX", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 4200, 80, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_10", "Loadout Slot X", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 6000, 100, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_11", "Loadout Slot XI", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 8200, 120, "+1 Loadout-Slot.");
        AddDefinition("general.loadout.slot_12", "Loadout Slot XII", GeneralMetaCategory.MetaLoadout, GeneralMetaNodeKind.LoadoutSlot, 10500, 140, "+1 Loadout-Slot.");

        AddDefinition("general.tile.gold", "Gold Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 160, 6, "Mehr Gold-Rewards.", false, "20 Runner-Kills oder Wave 6 erreicht.");
        AddDefinition("general.tile.slow", "Slow Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 0, 1, "Verlangsamt Gegner.", true);
        AddDefinition("general.tile.trap", "Trap Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 70, 2, "Schadet Gegnern.", false, "Wave 5 erreicht.");
        AddDefinition("general.tile.knock", "Knock Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 0, 1, "Stoesst Gegner zurueck.", true);
        AddDefinition("general.tile.range", "Range Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 220, 10, "Tower +1 Reichweite.", false, "20 Tower gebaut.");
        AddDefinition("general.tile.damage", "Damage Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 340, 15, "Tower +25% Schaden.", false, "Boss 1 besiegt.");
        AddDefinition("general.tile.rate", "Rate Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 380, 17, "Tower feuert schneller.", false, "50 Runner-Kills.");
        AddDefinition("general.tile.xp", "XP Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 260, 12, "Tower erhaelt mehr XP.", false, "20 Tower-Level-Ups.");
        AddDefinition("general.tile.upgrade", "Upgrade Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 450, 19, "Upgrades werden staerker.", false, "30 Upgrade Points verdient.");
        AddDefinition("general.tile.combo", "Combo Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 700, 24, "Kombiniert Tile-Effekte.", false, "Fire + Poison + Spike/Alchemist freigeschaltet.");
        AddDefinition("general.tile.heal", "Heal Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 520, 21, "2% Kill-Chance auf Leben.", false, "Boss besiegt oder Wave 18.");
        AddDefinition("general.tile.weakpoint", "Weakpoint Tile", GeneralMetaCategory.TileUnlock, GeneralMetaNodeKind.TileUnlock, 620, 23, "Ruestung max. 2 und -50%.", false, "Damage Tile oder Wave 22.");

        AddDefinition("general.qol.speed_fast", "GameSpeed 2x", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 50, 2, "2x Spieltempo.");
        AddDefinition("general.qol.speed_medium", "GameSpeed 3x", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 140, 7, "3x Spieltempo.");
        AddDefinition("general.qol.speed_faster", "GameSpeed 6x", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 250, 12, "6x Spieltempo.");
        AddDefinition("general.qol.dps_display", "DPS-Anzeige", GeneralMetaCategory.QoL, GeneralMetaNodeKind.QoL, 180, 8, "DPS im Towerpanel.");

        AddDefinition("general.start.gold_1", "Startgold I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 120, 4, "+10 Startgold.", 1);
        AddDefinition("general.start.gold_2", "Startgold II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 220, 8, "+20 Startgold.", 1);
        AddDefinition("general.start.gold_3", "Startgold III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 380, 14, "+30 Startgold.", 2);
        AddDefinition("general.start.gold_4", "Startgold IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 600, 22, "+40 Startgold.", 2);
        AddDefinition("general.start.gold_5", "Startgold V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 900, 32, "+50 Startgold.", 3);
        AddDefinition("general.start.life_1", "Startleben I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 150, 5, "+1 Startleben.", 1);
        AddDefinition("general.start.life_2", "Startleben II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 300, 12, "+2 Startleben.", 2);
        AddDefinition("general.start.life_3", "Startleben III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 550, 22, "+3 Startleben.", 2);
        AddDefinition("general.start.life_4", "Startleben IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 850, 35, "+4 Startleben.", 3);
        AddDefinition("general.start.life_5", "Startleben V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 1250, 48, "+5 Startleben.", 3);
        AddDefinition("general.start.xp_1", "Start-XP I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 100, 4, "+5 XP fuer ersten Tower.", 1);
        AddDefinition("general.start.xp_2", "Start-XP II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 180, 8, "+10 XP fuer ersten Tower.", 1);
        AddDefinition("general.start.xp_3", "Start-XP III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 320, 14, "+15 XP fuer ersten Tower.", 1);
        AddDefinition("general.start.xp_4", "Start-XP IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 560, 24, "+20 XP fuer ersten Tower.", 2);
        AddDefinition("general.start.xp_5", "Start-XP V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 850, 36, "+25 XP fuer ersten Tower.", 2);
        AddDefinition("general.start.path_1", "Startweg I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 400, 12, "Startweg +1 Tile.", 2);
        AddDefinition("general.start.path_2", "Startweg II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 950, 28, "Startweg +2 Tiles.", 3);
        AddDefinition("general.start.protection_1", "Startschutz I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 260, 9, "Erster Leak kostet -1 Leben.", 1);
        AddDefinition("general.start.reserve_1", "Notreserve I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 700, 20, "+120 Gold unter 5 Leben.", 2);
        AddDefinition("general.start.discount_1", "Startkosten I", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 300, 10, "Erster Tower kostet -10 Gold.", 1);
        AddDefinition("general.start.discount_2", "Startkosten II", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 550, 18, "Erster Tower kostet weitere -10 Gold.", 1);
        AddDefinition("general.start.discount_3", "Startkosten III", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 850, 28, "Erster Tower kostet weitere -10 Gold.", 1);
        AddDefinition("general.start.discount_4", "Startkosten IV", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 1200, 40, "Erster Tower kostet weitere -10 Gold.", 1);
        AddDefinition("general.start.discount_5", "Startkosten V", GeneralMetaCategory.StartOption, GeneralMetaNodeKind.StartPower, 1700, 55, "Erster Basic Tower kann kostenlos werden.", 1);

        AddDefinition("general.enemy.runner_info", "Runner-Daten", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyInfo, 80, 3, "Bessere Runner-Preview.");
        AddDefinition("general.enemy.tank_info", "Tank-Daten", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyInfo, 100, 4, "Bessere Tank-Preview.");
        AddDefinition("general.enemy.knight_info", "Knight-Daten", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyInfo, 120, 5, "Armor-Hinweis.");
        AddDefinition("general.enemy.mage_info", "Mage-Daten", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyInfo, 180, 8, "Teleport-Hinweis.");
        AddDefinition("general.enemy.learner_info", "Learner-Daten", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyInfo, 220, 9, "Immunitaets-Hinweis.");
        AddDefinition("general.enemy.runner_damage_1", "Runner-Jagdprotokoll I", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyPower, 150, 6, "+2% Schaden gegen Runner.", 1);
        AddDefinition("general.enemy.runner_damage_2", "Runner-Jagdprotokoll II", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyPower, 300, 12, "+4% Schaden gegen Runner gesamt.", 1);
        AddDefinition("general.enemy.runner_damage_3", "Runner-Jagdprotokoll III", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyPower, 500, 20, "+6% Schaden gegen Runner gesamt.", 1);
        AddDefinition("general.enemy.tank_damage_1", "Tank-Analyse I", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyPower, 180, 7, "+2% Schaden gegen Tanks.", 1);
        AddDefinition("general.enemy.knight_damage_1", "Knight-Panzerkunde I", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyPower, 200, 8, "+2% Schaden gegen Knights.", 1);
        AddDefinition("general.enemy.learner_damage_1", "Learner-Gegenprobe I", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyPower, 350, 16, "+3% direkter Schaden gegen Learner.", 1);
        AddDefinition("general.enemy.boss_damage_1", "Boss-Studie I", GeneralMetaCategory.EnemyResearch, GeneralMetaNodeKind.EnemyPower, 400, 18, "+2% Schaden gegen Boss/MiniBoss.", 2);
    }

    private void AddDefinition(string nodeId, string displayName, GeneralMetaCategory category, GeneralMetaNodeKind kind, int cost, int level, string effectText, bool unlockedByDefault = false, string requirementText = "")
    {
        AddDefinition(nodeId, displayName, category, kind, cost, level, effectText, 0, unlockedByDefault, requirementText);
    }

    private void AddDefinition(string nodeId, string displayName, GeneralMetaCategory category, GeneralMetaNodeKind kind, int cost, int level, string effectText, int slotCost, bool unlockedByDefault = false, string requirementText = "")
    {
        definitions.Add(new GeneralMetaNodeDefinition
        {
            nodeId = nodeId,
            displayName = displayName,
            category = category,
            kind = kind,
            cost = Mathf.Max(0, cost),
            requiredAccountLevel = Mathf.Max(1, level),
            slotCost = Mathf.Max(0, slotCost),
            unlockedByDefault = unlockedByDefault,
            effectText = effectText,
            requirementText = requirementText
        });
    }

    private void LoadProfile()
    {
        EnsureStates();

        accountXP = PlayerPrefs.GetInt(PlayerPrefsPrefix + "AccountXP", accountXP);
        kernwissen = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Kernwissen", kernwissen);
        highestWaveEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        highestChaosLevelEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "HighestChaosLevelEver", highestChaosLevelEver);
        totalGoldEarnedEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalGoldEarnedEver", totalGoldEarnedEver);
        totalTowersBuiltEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalTowersBuiltEver", totalTowersBuiltEver);
        totalTowerLevelUpsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalTowerLevelUpsEver", totalTowerLevelUpsEver);
        totalUpgradePointsEarnedEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalUpgradePointsEarnedEver", totalUpgradePointsEarnedEver);
        totalRunnerKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalRunnerKillsEver", totalRunnerKillsEver);
        totalBossKillsEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalBossKillsEver", totalBossKillsEver);
        blockedEventsChosenEver = PlayerPrefs.GetInt(PlayerPrefsPrefix + "BlockedEventsChosenEver", blockedEventsChosenEver);
        lastRunKernwissenGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunKernwissen", lastRunKernwissenGained);
        lastRunAccountXPGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunAccountXP", lastRunAccountXPGained);
        lastRunAccountLevelsGained = PlayerPrefs.GetInt(PlayerPrefsPrefix + "LastRunAccountLevels", lastRunAccountLevelsGained);

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            GeneralMetaNodeState state = GetNodeState(definition.nodeId);
            if (state == null)
                continue;

            int defaultPurchased = definition.unlockedByDefault ? 1 : 0;
            int defaultActive = definition.unlockedByDefault && !definition.RequiresLoadoutSlot() ? 1 : 0;
            state.purchased = PlayerPrefs.GetInt(PlayerPrefsPrefix + definition.nodeId + ".Purchased", defaultPurchased) == 1;
            state.active = PlayerPrefs.GetInt(PlayerPrefsPrefix + definition.nodeId + ".Active", defaultActive) == 1;

            if (!definition.RequiresLoadoutSlot())
                state.active = state.purchased;
        }

        ApplyStartTowerSetMigration();
        ApplyStartTileSetMigration();
        ApplyGoldKnockTileSwapMigration();
        accountLevel = CalculateAccountLevel(accountXP);
        activeLoadoutIndex = Mathf.Clamp(PlayerPrefs.GetInt(PlayerPrefsPrefix + "ActiveLoadout", activeLoadoutIndex), 0, LoadoutProfileCount - 1);
        LoadActiveLoadoutState();
    }

    private void ApplyStartTowerSetMigration()
    {
        if (PlayerPrefs.GetInt(StartTowerSetMigrationKey, 0) == 1)
            return;

        GeneralMetaNodeState slowState = GetNodeState("general.tower.slow");
        if (slowState != null)
        {
            slowState.purchased = false;
            slowState.active = false;
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "general.tower.slow.Purchased", 0);
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "general.tower.slow.Active", 0);
        }

        PlayerPrefs.SetInt(StartTowerSetMigrationKey, 1);
        PlayerPrefs.Save();
    }

    private void ApplyStartTileSetMigration()
    {
        if (PlayerPrefs.GetInt(StartTileSetMigrationKey, 0) == 1)
            return;

        SetPersistentNodeState("general.tile.path", true, true);
        SetPersistentNodeState("general.tile.gold", false, false);
        SetPersistentNodeState("general.tile.knock", true, true);
        SetPersistentNodeState("general.tile.slow", true, true);
        ClearPersistentNodeState("general.tile.bridge");

        PlayerPrefs.SetInt(StartTileSetMigrationKey, 1);
        PlayerPrefs.Save();
    }

    private void ApplyGoldKnockTileSwapMigration()
    {
        if (PlayerPrefs.GetInt(GoldKnockTileSwapMigrationKey, 0) == 1)
            return;

        SetPersistentNodeState("general.tile.knock", true, true);
        SetPersistentNodeState("general.tile.gold", false, false);

        PlayerPrefs.SetInt(GoldKnockTileSwapMigrationKey, 1);
        PlayerPrefs.Save();
    }

    private void SetPersistentNodeState(string nodeId, bool purchased, bool active)
    {
        GeneralMetaNodeState state = GetNodeState(nodeId);
        if (state != null)
        {
            state.purchased = purchased;
            state.active = active;
        }

        PlayerPrefs.SetInt(PlayerPrefsPrefix + nodeId + ".Purchased", purchased ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + nodeId + ".Active", active ? 1 : 0);
    }

    private void ClearPersistentNodeState(string nodeId)
    {
        GeneralMetaNodeState state = GetNodeState(nodeId);
        if (state != null)
        {
            state.purchased = false;
            state.active = false;
        }

        PlayerPrefs.DeleteKey(PlayerPrefsPrefix + nodeId + ".Purchased");
        PlayerPrefs.DeleteKey(PlayerPrefsPrefix + nodeId + ".Active");
    }

    private void SaveProfile()
    {
        EnsureStates();
        SaveActiveLoadoutState();

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "AccountXP", accountXP);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "Kernwissen", kernwissen);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "ActiveLoadout", activeLoadoutIndex);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestWaveEver", highestWaveEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "HighestChaosLevelEver", highestChaosLevelEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalGoldEarnedEver", totalGoldEarnedEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalTowersBuiltEver", totalTowersBuiltEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalTowerLevelUpsEver", totalTowerLevelUpsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalUpgradePointsEarnedEver", totalUpgradePointsEarnedEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalRunnerKillsEver", totalRunnerKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalBossKillsEver", totalBossKillsEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "BlockedEventsChosenEver", blockedEventsChosenEver);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunKernwissen", lastRunKernwissenGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunAccountXP", lastRunAccountXPGained);
        PlayerPrefs.SetInt(PlayerPrefsPrefix + "LastRunAccountLevels", lastRunAccountLevelsGained);

        foreach (GeneralMetaNodeState state in nodeStates)
        {
            if (state == null || string.IsNullOrEmpty(state.nodeId))
                continue;

            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Purchased", state.purchased ? 1 : 0);
            PlayerPrefs.SetInt(PlayerPrefsPrefix + state.nodeId + ".Active", state.active ? 1 : 0);
        }

        PlayerPrefs.Save();
    }

    private string GetLoadoutActiveKey(int loadoutIndex, string nodeId)
    {
        return PlayerPrefsPrefix + "Loadout." + Mathf.Clamp(loadoutIndex, 0, LoadoutProfileCount - 1) + "." + nodeId + ".Active";
    }

    private void SaveActiveLoadoutState()
    {
        EnsureStates();
        int safeIndex = Mathf.Clamp(activeLoadoutIndex, 0, LoadoutProfileCount - 1);

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId) || !definition.RequiresLoadoutSlot())
                continue;

            GeneralMetaNodeState state = GetNodeState(definition.nodeId);
            bool active = state != null && state.purchased && state.active;
            PlayerPrefs.SetInt(GetLoadoutActiveKey(safeIndex, definition.nodeId), active ? 1 : 0);
        }
    }

    private void LoadActiveLoadoutState()
    {
        EnsureStates();
        int safeIndex = Mathf.Clamp(activeLoadoutIndex, 0, LoadoutProfileCount - 1);

        foreach (GeneralMetaNodeDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.nodeId))
                continue;

            GeneralMetaNodeState state = GetNodeState(definition.nodeId);
            if (state == null)
                continue;

            if (!definition.RequiresLoadoutSlot())
            {
                state.active = state.purchased;
                continue;
            }

            string key = GetLoadoutActiveKey(safeIndex, definition.nodeId);
            bool active = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key, 0) == 1 : state.active;
            state.active = state.purchased && active;
        }
    }
}
