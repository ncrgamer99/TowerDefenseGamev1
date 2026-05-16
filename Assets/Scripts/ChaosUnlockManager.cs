using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChaosUnlockManager : MonoBehaviour
{
    [Header("Phase 6 Version Check")]
    public string phase6Version = "Phase6 V1 Step14 - Grund-Freischaltungen Content-Pool V1 - 2026-05-13";

    [Header("References")]
    public GameManager gameManager;
    public ChaosJusticeManager chaosJusticeManager;
    public EnemySpawner enemySpawner;
    public ChaosLexiconManager lexiconManager;
    public ChaosUnlockUI unlockUI;
    public Canvas targetCanvas;

    [Header("Unlock Behaviour V1")]
    public bool enableUnlockSystem = true;
    public bool filterRiskModifierPoolByUnlocks = true;
    public bool createDefaultUnlocksOnStart = true;
    public bool showLockedTeasers = true;
    public bool revealLockedTitles = false;
    public bool showUnlockNotifications = true;
    public bool autoRefreshOnWaveCompleted = true;

    [Header("Persistence")]
    public bool usePersistentUnlocks = true;
    public string playerPrefsPrefix = "TD_ChaosUnlock_";

    [Header("Open Button")]
    public bool autoCreateOpenButton = true;
    public Button openButton;
    public TextMeshProUGUI openButtonText;
    public string openButtonLabel = "Freischaltungen (F2)";

    [Header("Open / Close")]
    public bool enableUnlockUI = true;
    public KeyCode toggleKey = KeyCode.F2;
    public bool closeWithEscape = true;
    public bool allowOpeningDuringWave = true;
    public bool closeGameplayPanelsWhenOpening = true;

    [Header("Entries")]
    public List<ChaosUnlockEntry> unlocks = new List<ChaosUnlockEntry>();

    [Header("Runtime Debug")]
    public bool unlockUIOpen = false;
    public string lastUnlockNotification = "";
    public List<string> newlyUnlockedThisSession = new List<string>();

    public bool IsOpen => unlockUIOpen;

    private void Awake()
    {
        EnsureLists();

        if (createDefaultUnlocksOnStart)
            CreateDefaultUnlocksIfNeeded();

        LoadPersistentUnlocks();
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
    }

    private void Start()
    {
        ResolveReferences();
        SetupOpenButton();
        Debug.Log("ChaosUnlockManager Version: " + phase6Version);

        if (unlockUI != null)
            unlockUI.Connect(this);

        RefreshUnlocks(false);
        CloseUnlocks();
    }

    private void Update()
    {
        if (!enableUnlockUI)
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUnlocks();
            return;
        }

        if (unlockUIOpen && closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
            CloseUnlocks();
    }

    public void ToggleUnlocks()
    {
        if (unlockUIOpen)
            CloseUnlocks();
        else
            OpenUnlocks();
    }

    public void OpenUnlocks()
    {
        if (!enableUnlockUI)
            return;

        ResolveReferences();

        if (gameManager != null && !gameManager.CanOpenAuxiliaryModalUI())
            return;

        if (!allowOpeningDuringWave && gameManager != null && gameManager.currentPhase == GamePhase.Wave)
            return;

        if (closeGameplayPanelsWhenOpening && gameManager != null)
        {
            gameManager.ClosePathAndBuildSelectionsForModal();
            gameManager.CloseTowerUIForModal();
        }

        RefreshUnlocks(false);
        unlockUIOpen = true;

        if (unlockUI != null)
            unlockUI.OpenUnlocks();
    }

    public void CloseUnlocks()
    {
        unlockUIOpen = false;

        if (unlockUI != null)
            unlockUI.CloseUnlocks();
    }

    public void RefreshAndNotify()
    {
        RefreshUnlocks(true);

        if (unlockUI != null && unlockUIOpen)
            unlockUI.RefreshAll();
    }

    public List<WaveModifier> FilterRiskModifierPool(List<WaveModifier> sourcePool)
    {
        if (!enableUnlockSystem || !filterRiskModifierPoolByUnlocks || sourcePool == null)
            return sourcePool;

        RefreshUnlocks(false);
        List<WaveModifier> filtered = new List<WaveModifier>();

        foreach (WaveModifier modifier in sourcePool)
        {
            if (modifier == null || !modifier.IsValid())
                continue;

            if (IsRiskModifierUnlocked(modifier.displayName))
                filtered.Add(modifier);
        }

        return filtered.Count > 0 ? filtered : sourcePool;
    }

    public bool IsRiskModifierUnlocked(string modifierName)
    {
        if (!enableUnlockSystem || !filterRiskModifierPoolByUnlocks)
            return true;

        if (string.IsNullOrEmpty(modifierName))
            return true;

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry == null)
                continue;

            if (entry.UnlocksRiskModifier(modifierName))
                return entry.IsUnlocked();
        }

        return true;
    }

    public List<ChaosUnlockEntry> GetVisibleUnlocks()
    {
        RefreshUnlocks(false);
        List<ChaosUnlockEntry> result = new List<ChaosUnlockEntry>();

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry != null && entry.IsVisible(showLockedTeasers))
                result.Add(entry);
        }

        result.Sort(CompareUnlockEntries);
        return result;
    }

    public int GetUnlockedCount()
    {
        int count = 0;

        if (unlocks == null)
            return count;

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry != null && entry.IsUnlocked())
                count++;
        }

        return count;
    }

    public int GetVisibleCount()
    {
        int count = 0;

        if (unlocks == null)
            return count;

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry != null && entry.IsVisible(showLockedTeasers))
                count++;
        }

        return count;
    }

    public int GetNewlyUnlockedThisSessionCount()
    {
        return newlyUnlockedThisSession != null ? newlyUnlockedThisSession.Count : 0;
    }

    public ChaosUnlockEntry GetUnlockById(string unlockId)
    {
        if (string.IsNullOrEmpty(unlockId) || unlocks == null)
            return null;

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry != null && entry.unlockId == unlockId)
                return entry;
        }

        return null;
    }

    public string GetUnlockDisplayTitle(ChaosUnlockEntry entry)
    {
        return entry != null ? entry.GetDisplayTitle(revealLockedTitles) : "Freischaltung";
    }

    public string GetUnlockDetailText(ChaosUnlockEntry entry)
    {
        if (entry == null)
            return "Keine Freischaltung ausgewählt.";

        bool unlocked = entry.IsUnlocked();
        StringBuilder builder = new StringBuilder();
        builder.Append("<b>").Append(entry.GetDisplayTitle(revealLockedTitles || unlocked)).Append("</b>");
        builder.Append("\n<size=80%><color=#B9C2D0>").Append(GetCategoryDisplayName(entry.category)).Append("</color></size>");

        if (!unlocked)
        {
            builder.Append("\n\n").Append(string.IsNullOrEmpty(entry.lockedHint) ? "Diese Freischaltung ist noch verborgen." : entry.lockedHint);
            builder.Append("\n\n<color=#B9C2D0>Bedingung: ").Append(entry.GetConditionText()).Append("</color>");
            builder.Append("\n<color=#B9C2D0>Fortschritt: ").Append(entry.lastObservedProgress).Append(" / ").Append(Mathf.Max(1, entry.requiredValue)).Append("</color>");
            return builder.ToString();
        }

        if (!string.IsNullOrEmpty(entry.description))
            builder.Append("\n\n").Append(entry.description);

        builder.Append("\n\n<color=#D6A441><b>Freigeschaltet:</b>\n").Append(entry.GetUnlockedContentText()).Append("</color>");
        builder.Append("\n\n<color=#B9C2D0>Bedingung: ").Append(entry.GetConditionText()).Append("</color>");
        return builder.ToString();
    }

    public string GetCategoryDisplayName(ChaosUnlockCategory category)
    {
        switch (category)
        {
            case ChaosUnlockCategory.Grundlagen: return "Grundlagen";
            case ChaosUnlockCategory.RisikoPool: return "Risiko-Pool";
            case ChaosUnlockCategory.ChaosVarianten: return "Chaos-Varianten";
            case ChaosUnlockCategory.ChaosWaves: return "Chaos-Waves";
            case ChaosUnlockCategory.Gerechtigkeit: return "Gerechtigkeit";
            case ChaosUnlockCategory.Auswertung: return "Auswertung";
            case ChaosUnlockCategory.Zukunft: return "Später";
            default: return category.ToString();
        }
    }

    public string GetUnlockSummaryText()
    {
        int unlockedCount = 0;
        int visibleCount = 0;
        int riskModifierUnlocks = 0;

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry == null)
                continue;

            if (entry.IsVisible(showLockedTeasers))
                visibleCount++;

            if (entry.IsUnlocked())
            {
                unlockedCount++;

                if (entry.unlockedRiskModifierNames != null)
                    riskModifierUnlocks += entry.unlockedRiskModifierNames.Count;
            }
        }

        return "Freischaltungen: " + unlockedCount + " / " + Mathf.Max(1, unlocks.Count) +
               " | Sichtbar: " + visibleCount +
               " | Risiko-Pool-Inhalte: " + riskModifierUnlocks;
    }

    public string GetDetailedUnlockSummaryText(int maxEntries)
    {
        List<ChaosUnlockEntry> visible = GetVisibleUnlocks();
        int safeMax = Mathf.Max(1, maxEntries);
        StringBuilder builder = new StringBuilder();
        int count = 0;

        foreach (ChaosUnlockEntry entry in visible)
        {
            if (entry == null || count >= safeMax)
                continue;

            builder.Append("- ").Append(entry.IsUnlocked() ? "[OK] " : "[???] ").Append(GetUnlockDisplayTitle(entry));

            if (!entry.IsUnlocked())
                builder.Append(" | ").Append(entry.GetConditionText());

            builder.Append("\n");
            count++;
        }

        return builder.Length > 0 ? builder.ToString().TrimEnd('\n') : "Keine Freischaltungen sichtbar.";
    }

    public void RefreshUnlocks(bool notify)
    {
        if (!enableUnlockSystem)
            return;

        ResolveReferences();
        EnsureLists();

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry == null || entry.IsUnlocked())
                continue;

            int progress = GetProgressForEntry(entry);
            entry.lastObservedProgress = progress;

            if (progress >= Mathf.Max(1, entry.requiredValue) || entry.conditionType == ChaosUnlockConditionType.AlwaysUnlocked)
                UnlockEntry(entry, notify);
        }

        UnlockLexiconEntriesForUnlockedContent();
    }

    private void UnlockEntry(ChaosUnlockEntry entry, bool notify)
    {
        if (entry == null || entry.IsUnlocked())
            return;

        entry.unlocked = true;

        if (!newlyUnlockedThisSession.Contains(entry.unlockId))
            newlyUnlockedThisSession.Add(entry.unlockId);

        SaveUnlock(entry);
        UnlockLexiconEntryIds(entry);
        lastUnlockNotification = "Freischaltung: " + entry.title;

        if (notify && showUnlockNotifications)
        {
            Debug.Log("Chaos-Freischaltung erhalten: " + entry.title + " | " + entry.GetUnlockedContentText());

            if (unlockUI != null)
                unlockUI.ShowNotification(lastUnlockNotification);
        }
    }

    private int GetProgressForEntry(ChaosUnlockEntry entry)
    {
        if (entry == null)
            return 0;

        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;
        int chaosLevel = chaosJusticeManager != null ? chaosJusticeManager.GetChaosLevel() : 0;

        switch (entry.conditionType)
        {
            case ChaosUnlockConditionType.AlwaysUnlocked:
                return entry.requiredValue;
            case ChaosUnlockConditionType.ReachChaosLevel:
                return chaosJusticeManager != null && chaosJusticeManager.runData != null ? chaosJusticeManager.runData.highestChaosLevel : chaosLevel;
            case ChaosUnlockConditionType.SurviveChaosWaves:
                return chaosJusticeManager != null && chaosJusticeManager.runData != null ? chaosJusticeManager.runData.chaosWavesSurvived : history != null ? history.GetChaosWavesCompleted() : 0;
            case ChaosUnlockConditionType.TotalBossKills:
                return history != null ? history.GetBossKills() : 0;
            case ChaosUnlockConditionType.BossKillsAtOrAboveChaos:
                return CountBossKillsAtOrAboveChaos(history, entry.requiredChaosLevel);
            case ChaosUnlockConditionType.TotalChaosChoices:
                return chaosJusticeManager != null ? chaosJusticeManager.GetChaosChoiceCount() : 0;
            case ChaosUnlockConditionType.TotalJusticeChoices:
                return chaosJusticeManager != null ? chaosJusticeManager.GetJusticeChoiceCount() : 0;
            case ChaosUnlockConditionType.HighestGoldJustice:
                return chaosJusticeManager != null && chaosJusticeManager.runData != null ? chaosJusticeManager.runData.goldJusticeLevel : history != null ? history.GetHighestGoldJusticeLevelSeen() : 0;
            case ChaosUnlockConditionType.HighestXpJustice:
                return chaosJusticeManager != null && chaosJusticeManager.runData != null ? chaosJusticeManager.runData.xpJusticeLevel : history != null ? history.GetHighestXpJusticeLevelSeen() : 0;
            case ChaosUnlockConditionType.ChaosVariantKills:
                return history != null ? history.GetTotalChaosVariantKills() : 0;
            case ChaosUnlockConditionType.ChaosWaveBlockWaves:
                return history != null ? history.GetChaosWaveBlockWavesCompleted() : 0;
            default:
                return 0;
        }
    }

    private int CountBossKillsAtOrAboveChaos(WaveHistory history, int requiredChaos)
    {
        if (history == null || history.completedWaves == null)
            return 0;

        int count = 0;

        foreach (WaveCompletionResult result in history.completedWaves)
        {
            if (result != null && result.isBossWave && result.bossDefeated && result.chaosLevelAtWaveStart >= requiredChaos)
                count++;
        }

        return count;
    }

    private void UnlockLexiconEntriesForUnlockedContent()
    {
        if (lexiconManager == null)
            return;

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry != null && entry.IsUnlocked())
                UnlockLexiconEntryIds(entry);
        }
    }

    private void UnlockLexiconEntryIds(ChaosUnlockEntry entry)
    {
        if (entry == null || lexiconManager == null || entry.unlockedLexiconEntryIds == null)
            return;

        foreach (string entryId in entry.unlockedLexiconEntryIds)
        {
            if (!string.IsNullOrEmpty(entryId))
                lexiconManager.UnlockEntry(entryId);
        }
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (autoRefreshOnWaveCompleted)
            RefreshUnlocks(true);
    }

    private void HandleGameOverTriggered()
    {
        RefreshUnlocks(true);
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (chaosJusticeManager == null)
        {
            if (gameManager != null)
                chaosJusticeManager = gameManager.GetChaosJusticeManager();

            if (chaosJusticeManager == null)
                chaosJusticeManager = FindObjectOfType<ChaosJusticeManager>();
        }

        if (enemySpawner == null && gameManager != null)
            enemySpawner = gameManager.enemySpawner;

        if (enemySpawner == null)
            enemySpawner = FindObjectOfType<EnemySpawner>();

        if (lexiconManager == null)
            lexiconManager = FindObjectOfType<ChaosLexiconManager>();

        if (unlockUI == null)
            unlockUI = FindObjectOfType<ChaosUnlockUI>();

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();
    }

    private void SetupOpenButton()
    {
        if (openButton == null && autoCreateOpenButton)
            CreateOpenButton();

        if (openButton == null)
            return;

        openButton.onClick.RemoveAllListeners();
        openButton.onClick.AddListener(OpenUnlocks);

        if (openButtonText == null)
            openButtonText = openButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (openButtonText != null)
        {
            openButtonText.text = openButtonLabel;
            openButtonText.fontSize = 16f;
            openButtonText.alignment = TextAlignmentOptions.Center;
            openButtonText.color = Color.white;
            openButtonText.enableWordWrapping = false;
            openButtonText.raycastTarget = false;
        }
    }

    private void CreateOpenButton()
    {
        ResolveReferences();

        if (targetCanvas == null)
            return;

        GameObject buttonObject = new GameObject("ChaosUnlockOpenButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(targetCanvas.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -66f);
        rect.sizeDelta = new Vector2(190f, 42f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(35, 45, 64, 235);
        image.raycastTarget = true;

        openButton = buttonObject.GetComponent<Button>();

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        Stretch(textObject.GetComponent<RectTransform>());
        openButtonText = textObject.GetComponent<TextMeshProUGUI>();
    }

    private void CreateDefaultUnlocksIfNeeded()
    {
        EnsureLists();

        if (unlocks.Count > 0)
            return;

        AddUnlock("core_risks", "Grundrisiken", ChaosUnlockCategory.RisikoPool, 0,
            "Die ersten Risiko-Modifikatoren sind direkt verfügbar.", "Grundrisiken des Chaos-Pools.",
            ChaosUnlockConditionType.AlwaysUnlocked, 1, 0, true,
            new string[] { "Mehr Gegner", "Runner-Druck", "Tank-Druck", "Knight-Druck", "Schnellere Spawns", "Gieriger Ansturm" },
            new string[] { "risiko_modifikatoren" });

        AddUnlock("reward_risks", "Belohnungsrisiken", ChaosUnlockCategory.RisikoPool, 10,
            "Riskante Reward-Varianten werden Teil des Angebots.", "Ein unbekannter Reward-Risiko-Inhalt wartet im Pool.",
            ChaosUnlockConditionType.TotalChaosChoices, 1, 0, false,
            new string[] { "Goldrausch-Risiko", "Eile gegen Gold" }, new string[] { "risiko_details" });

        AddUnlock("advanced_roles", "Erweiterter Rollendruck", ChaosUnlockCategory.RisikoPool, 20,
            "Mage-, Learner- und gemischte Rollendruck-Risiken können angeboten werden.", "Ein unbekannter Rollen-Risiko-Inhalt wartet im Pool.",
            ChaosUnlockConditionType.TotalBossKills, 1, 0, false,
            new string[] { "Mage-Druck", "Learner-Druck", "Gemischter Rollendruck", "XP-Prüfung" }, new string[] { "risiko_details" });

        AddUnlock("allrounder_pressure", "AllRounder-Druck", ChaosUnlockCategory.RisikoPool, 30,
            "AllRounder können als Risiko-Modifikator in den Pool kommen, wenn das Prefab existiert.", "Ein unbekannter Gegnerdruck wartet im Pool.",
            ChaosUnlockConditionType.ReachChaosLevel, 2, 0, false,
            new string[] { "AllRounder-Druck" }, new string[] { "risiko_details" });

        AddUnlock("preboss_pressure", "Vor-Boss-Druck", ChaosUnlockCategory.RisikoPool, 40,
            "Vor-Boss- und Boss-Waves können durch einen eigenen Risiko-Modifikator verschärft werden.", "Ein unbekannter Bossnaher Risiko-Inhalt wartet im Pool.",
            ChaosUnlockConditionType.BossKillsAtOrAboveChaos, 1, 1, false,
            new string[] { "Vor-Boss-Druck" }, new string[] { "risiko_details" });

        AddUnlock("miniboss_pressure", "MiniBoss-Druck light", ChaosUnlockCategory.RisikoPool, 50,
            "MiniBoss-Waves können durch einen zusätzlichen MiniBoss verschärft werden. Keine Elite-Regeln, keine Tower-Zerstörung.", "Ein unbekannter MiniBoss-Risiko-Inhalt wartet im Pool.",
            ChaosUnlockConditionType.ReachChaosLevel, 4, 0, false,
            new string[] { "MiniBoss-Druck light" }, new string[] { "risiko_details" });

        AddUnlock("chaos_variants", "Violette Verformung", ChaosUnlockCategory.ChaosVarianten, 0,
            "Der Risiko-Modifikator für häufigere Chaos-Varianten wird Teil des Pools.", "Ein unbekannter violetter Gegnerinhalt wartet im Pool.",
            ChaosUnlockConditionType.ReachChaosLevel, 3, 0, false,
            new string[] { "Violette Verformung" }, new string[] { "chaos_variants" });

        AddUnlock("chaos_wave_blocks_early", "Instabile Wellen I", ChaosUnlockCategory.ChaosWaves, 0,
            "Frühe Chaos-Wave-Risiken für Verdichtung und Zähigkeit werden Teil des Pools.", "Ein unbekannter Chaos-Wave-Inhalt wartet im Pool.",
            ChaosUnlockConditionType.ReachChaosLevel, 2, 0, false,
            new string[] { "Stärkere Verdichtung", "Zähe Wellen" }, new string[] { "chaos_waves" });

        AddUnlock("chaos_wave_blocks_late", "Instabile Wellen II", ChaosUnlockCategory.ChaosWaves, 10,
            "Nachhut und violette Wave-Gruppen können als Risiko-Modifikatoren erscheinen.", "Ein stärkerer Chaos-Wave-Inhalt wartet im Pool.",
            ChaosUnlockConditionType.ReachChaosLevel, 3, 0, false,
            new string[] { "Stärkere Nachhut", "Violette Wellen" }, new string[] { "chaos_waves" });

        AddUnlock("chaos_armor", "Chaos-Panzerung", ChaosUnlockCategory.ChaosWaves, 20,
            "Armor-orientierte Chaos-Wave-Risiken können angeboten werden.", "Ein unbekannter Panzerungsinhalt wartet im Pool.",
            ChaosUnlockConditionType.ReachChaosLevel, 4, 0, false,
            new string[] { "Chaos-Panzerung" }, new string[] { "chaos_waves" });

        AddUnlock("justice_archive", "Gerechtigkeitsprotokoll", ChaosUnlockCategory.Gerechtigkeit, 0,
            "Das Lexikon zeigt mehr Kontext zu aufgebauter Gerechtigkeit.", "Ein unbekannter Ordnungseintrag wartet.",
            ChaosUnlockConditionType.TotalJusticeChoices, 1, 0, false,
            new string[] { }, new string[] { "gerechtigkeit" });
    }

    private void AddUnlock(string id, string title, ChaosUnlockCategory category, int sortOrder, string description, string lockedHint, ChaosUnlockConditionType condition, int requiredValue, int requiredChaosLevel, bool unlockedByDefault, string[] riskNames, string[] lexiconEntryIds)
    {
        ChaosUnlockEntry entry = new ChaosUnlockEntry();
        entry.unlockId = id;
        entry.title = title;
        entry.category = category;
        entry.sortOrder = sortOrder;
        entry.description = description;
        entry.lockedHint = lockedHint;
        entry.conditionType = condition;
        entry.requiredValue = Mathf.Max(1, requiredValue);
        entry.requiredChaosLevel = Mathf.Max(0, requiredChaosLevel);
        entry.unlockedByDefault = unlockedByDefault;
        entry.unlocked = unlockedByDefault || condition == ChaosUnlockConditionType.AlwaysUnlocked;
        entry.sourceTag = "Step14Default";

        if (riskNames != null)
            entry.unlockedRiskModifierNames.AddRange(riskNames);

        if (lexiconEntryIds != null)
            entry.unlockedLexiconEntryIds.AddRange(lexiconEntryIds);

        unlocks.Add(entry);
    }

    private int CompareUnlockEntries(ChaosUnlockEntry a, ChaosUnlockEntry b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int categoryCompare = ((int)a.category).CompareTo((int)b.category);
        if (categoryCompare != 0) return categoryCompare;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);
        if (orderCompare != 0) return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private void LoadPersistentUnlocks()
    {
        if (!usePersistentUnlocks)
            return;

        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry == null || string.IsNullOrEmpty(entry.unlockId))
                continue;

            if (PlayerPrefs.GetInt(playerPrefsPrefix + entry.unlockId, entry.unlockedByDefault ? 1 : 0) == 1)
                entry.unlocked = true;
        }
    }

    private void SaveUnlock(ChaosUnlockEntry entry)
    {
        if (!usePersistentUnlocks || entry == null || string.IsNullOrEmpty(entry.unlockId))
            return;

        PlayerPrefs.SetInt(playerPrefsPrefix + entry.unlockId, 1);
        PlayerPrefs.Save();
    }

    [ContextMenu("Debug/Reset Chaos Unlocks")]
    public void ResetPersistentUnlocksForTesting()
    {
        foreach (ChaosUnlockEntry entry in unlocks)
        {
            if (entry == null || string.IsNullOrEmpty(entry.unlockId))
                continue;

            PlayerPrefs.DeleteKey(playerPrefsPrefix + entry.unlockId);
            entry.unlocked = entry.unlockedByDefault || entry.conditionType == ChaosUnlockConditionType.AlwaysUnlocked;
        }

        newlyUnlockedThisSession.Clear();
        lastUnlockNotification = "";
        PlayerPrefs.Save();
        RefreshUnlocks(false);
    }

    private void EnsureLists()
    {
        if (unlocks == null)
            unlocks = new List<ChaosUnlockEntry>();

        if (newlyUnlockedThisSession == null)
            newlyUnlockedThisSession = new List<string>();
    }

    private void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
