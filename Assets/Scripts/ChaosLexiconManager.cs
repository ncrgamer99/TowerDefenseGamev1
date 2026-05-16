using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChaosLexiconManager : MonoBehaviour
{
    [Header("Phase 6 Version Check")]
    public string phase6Version = "Phase6 V1 Step14 - Freischaltungen im Lexikon - 2026-05-13";

    [Header("References")]
    public GameManager gameManager;
    public ChaosJusticeManager chaosJusticeManager;
    public RunStatisticsTracker runStatisticsTracker;
    public ChaosUnlockManager unlockManager;
    public EnemySpawner enemySpawner;
    public ChaosLexiconUI lexiconUI;
    public Canvas targetCanvas;

    [Header("Open Button")]
    public bool autoCreateOpenButton = true;
    public Button openButton;
    public TextMeshProUGUI openButtonText;
    public string openButtonLabel = "Lexikon (F1)";

    [Header("Open / Close")]
    public bool enableLexicon = true;
    public KeyCode toggleKey = KeyCode.F1;
    public bool closeWithEscape = true;
    public bool allowOpeningDuringWave = true;
    public bool closeGameplayPanelsWhenOpening = true;

    [Header("Entry Generation")]
    public bool createDefaultEntriesOnStart = true;
    public bool showLockedFutureTeasers = true;
    public bool autoUnlockRuntimeEntries = true;
    public bool includeSelectedRiskModifierEntries = true;
    public bool includeChaosVariantEntry = true;
    public bool includeChaosWaveBlockEntry = true;
    public bool includeFutureInfoAsTeasers = true;
    public bool appendRuntimeData = true;

    [Header("Entries")]
    public List<ChaosLexiconEntry> entries = new List<ChaosLexiconEntry>();
    public List<string> discoveredEntryIds = new List<string>();

    [Header("Runtime Debug")]
    public bool lexiconOpen = false;
    public ChaosLexiconCategory currentCategory = ChaosLexiconCategory.Grundlagen;
    public string selectedEntryId = "";

    public bool IsLexiconOpen => lexiconOpen;

    private void Awake()
    {
        EnsureLists();

        if (createDefaultEntriesOnStart)
            CreateDefaultEntriesIfNeeded();
    }

    private void Start()
    {
        ResolveReferences();
        SetupOpenButton();

        Debug.Log("ChaosLexiconManager Version: " + phase6Version);

        if (lexiconUI != null)
            lexiconUI.Connect(this);

        CloseLexicon();
    }

    private void Update()
    {
        if (!enableLexicon)
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleLexicon();
            return;
        }

        if (lexiconOpen && closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
            CloseLexicon();
    }

    public void ToggleLexicon()
    {
        if (lexiconOpen)
            CloseLexicon();
        else
            OpenLexicon();
    }

    public void OpenLexicon()
    {
        if (!enableLexicon)
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

        RefreshRuntimeUnlocks();
        EnsureSelectedEntry();
        lexiconOpen = true;

        if (lexiconUI != null)
            lexiconUI.OpenLexicon();
    }

    public void CloseLexicon()
    {
        lexiconOpen = false;

        if (lexiconUI != null)
            lexiconUI.CloseLexicon();
    }

    public void RefreshAndReopenCurrentEntry()
    {
        RefreshRuntimeUnlocks();
        EnsureSelectedEntry();

        if (lexiconUI != null && lexiconOpen)
            lexiconUI.RefreshAll();
    }

    public void SelectEntry(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
            return;

        ChaosLexiconEntry entry = GetEntryById(entryId);

        if (entry == null)
            return;

        selectedEntryId = entry.entryId;
        currentCategory = entry.category;

        if (lexiconUI != null)
            lexiconUI.RefreshAll();
    }

    public void SelectCategory(ChaosLexiconCategory category)
    {
        currentCategory = category;
        List<ChaosLexiconEntry> visibleEntries = GetVisibleEntriesForCategory(category);

        if (visibleEntries.Count > 0)
            selectedEntryId = visibleEntries[0].entryId;

        if (lexiconUI != null)
            lexiconUI.RefreshAll();
    }

    public List<ChaosLexiconCategory> GetVisibleCategories()
    {
        RefreshRuntimeUnlocks();
        List<ChaosLexiconCategory> categories = new List<ChaosLexiconCategory>();

        foreach (ChaosLexiconEntry entry in GetVisibleEntries())
        {
            if (entry == null)
                continue;

            if (!categories.Contains(entry.category))
                categories.Add(entry.category);
        }

        categories.Sort((a, b) => ((int)a).CompareTo((int)b));
        return categories;
    }

    public List<ChaosLexiconEntry> GetVisibleEntriesForCategory(ChaosLexiconCategory category)
    {
        List<ChaosLexiconEntry> result = new List<ChaosLexiconEntry>();

        foreach (ChaosLexiconEntry entry in GetVisibleEntries())
        {
            if (entry != null && entry.category == category)
                result.Add(entry);
        }

        result.Sort(CompareEntries);
        return result;
    }

    public List<ChaosLexiconEntry> GetVisibleEntries()
    {
        RefreshRuntimeUnlocks();
        List<ChaosLexiconEntry> result = new List<ChaosLexiconEntry>();

        EnsureLists();

        foreach (ChaosLexiconEntry entry in entries)
        {
            if (entry == null)
                continue;

            ApplyDiscoveryState(entry);

            if (entry.IsVisible(showLockedFutureTeasers))
                result.Add(entry);
        }

        result.Sort(CompareEntries);
        return result;
    }

    public ChaosLexiconEntry GetSelectedEntry()
    {
        EnsureSelectedEntry();
        return GetEntryById(selectedEntryId);
    }

    public ChaosLexiconEntry GetEntryById(string entryId)
    {
        if (string.IsNullOrEmpty(entryId) || entries == null)
            return null;

        foreach (ChaosLexiconEntry entry in entries)
        {
            if (entry == null)
                continue;

            if (entry.entryId == entryId)
            {
                ApplyDiscoveryState(entry);
                return entry;
            }
        }

        return null;
    }

    public string GetEntryButtonLabel(ChaosLexiconEntry entry)
    {
        if (entry == null)
            return "Eintrag";

        string category = GetCategoryDisplayName(entry.category);
        string label = entry.title;

        if (!entry.IsUnlocked())
            label = "? " + label;

        if (entry.futureOnly)
            label += " [später]";

        return category + " | " + label;
    }

    public string GetEntryDetailText(ChaosLexiconEntry entry)
    {
        if (entry == null)
            return "Kein Lexikon-Eintrag ausgewählt.";

        StringBuilder builder = new StringBuilder();
        builder.Append("<b>").Append(entry.title).Append("</b>");
        builder.Append("\n<size=80%><color=#B9C2D0>").Append(GetCategoryDisplayName(entry.category)).Append("</color></size>");

        if (!entry.IsUnlocked())
        {
            builder.Append("\n\n");
            builder.Append(string.IsNullOrEmpty(entry.shortText) ? "Dieser Eintrag ist vorbereitet." : entry.shortText);
            builder.Append("\n\n<color=#B9C2D0>");
            builder.Append(string.IsNullOrEmpty(entry.detailText)
                ? "Die genaue Erklärung wird später sichtbar oder gehört nicht zur aktuellen V1-Umsetzung."
                : entry.detailText);
            builder.Append("</color>");
            return builder.ToString();
        }

        if (!string.IsNullOrEmpty(entry.shortText))
            builder.Append("\n\n<color=#B9C2D0>").Append(entry.shortText).Append("</color>");

        if (!string.IsNullOrEmpty(entry.detailText))
            builder.Append("\n\n").Append(entry.detailText);

        string runtime = GetRuntimeAppendixForEntry(entry);

        if (!string.IsNullOrEmpty(runtime))
            builder.Append("\n\n<color=#D6A441><size=90%>").Append(runtime).Append("</size></color>");

        return builder.ToString();
    }

    public string GetCategoryDisplayName(ChaosLexiconCategory category)
    {
        switch (category)
        {
            case ChaosLexiconCategory.Grundlagen: return "Grundlagen";
            case ChaosLexiconCategory.Gerechtigkeit: return "Gerechtigkeit";
            case ChaosLexiconCategory.Risiko: return "Risiko";
            case ChaosLexiconCategory.Waves: return "Chaos-Waves";
            case ChaosLexiconCategory.Gegner: return "Gegner";
            case ChaosLexiconCategory.Fairness: return "Fairness";
            case ChaosLexiconCategory.Auswertung: return "Auswertung";
            case ChaosLexiconCategory.Zukunft: return "Später";
            default: return category.ToString();
        }
    }

    public void UnlockEntry(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
            return;

        EnsureLists();

        if (!discoveredEntryIds.Contains(entryId))
            discoveredEntryIds.Add(entryId);

        ChaosLexiconEntry entry = GetEntryById(entryId);

        if (entry != null)
            entry.discovered = true;
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (chaosJusticeManager == null && gameManager != null)
            chaosJusticeManager = gameManager.GetChaosJusticeManager();

        if (chaosJusticeManager == null)
            chaosJusticeManager = FindObjectOfType<ChaosJusticeManager>();

        if (runStatisticsTracker == null && gameManager != null)
            runStatisticsTracker = gameManager.GetRunStatisticsTracker();

        if (runStatisticsTracker == null)
            runStatisticsTracker = FindObjectOfType<RunStatisticsTracker>();

        if (unlockManager == null && gameManager != null)
            unlockManager = gameManager.GetChaosUnlockManager();

        if (unlockManager == null)
            unlockManager = FindObjectOfType<ChaosUnlockManager>();

        if (enemySpawner == null && gameManager != null)
            enemySpawner = gameManager.enemySpawner;

        if (enemySpawner == null)
            enemySpawner = FindObjectOfType<EnemySpawner>();

        if (lexiconUI == null)
            lexiconUI = FindObjectOfType<ChaosLexiconUI>();

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
        openButton.onClick.AddListener(OpenLexicon);

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

        GameObject buttonObject = new GameObject("ChaosLexiconOpenButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(targetCanvas.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -18f);
        rect.sizeDelta = new Vector2(170f, 42f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(35, 45, 64, 235);
        image.raycastTarget = true;

        openButton = buttonObject.GetComponent<Button>();

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect);

        openButtonText = textObject.GetComponent<TextMeshProUGUI>();
    }

    private void EnsureSelectedEntry()
    {
        ChaosLexiconEntry selected = GetEntryById(selectedEntryId);

        if (selected != null && selected.IsVisible(showLockedFutureTeasers))
            return;

        List<ChaosLexiconEntry> visibleEntries = GetVisibleEntries();

        if (visibleEntries.Count <= 0)
        {
            selectedEntryId = "";
            return;
        }

        selectedEntryId = visibleEntries[0].entryId;
        currentCategory = visibleEntries[0].category;
    }

    private void RefreshRuntimeUnlocks()
    {
        if (!autoUnlockRuntimeEntries)
            return;

        ResolveReferences();

        int chaosLevel = chaosJusticeManager != null ? chaosJusticeManager.GetChaosLevel() : 0;
        int activeRisks = chaosJusticeManager != null ? chaosJusticeManager.GetActiveRiskModifierCount() : 0;
        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;

        if (chaosLevel > 0)
            UnlockEntry("chaos_level");

        if (activeRisks > 0)
            UnlockEntry("risiko_details");

        if (includeChaosVariantEntry && (chaosLevel >= 3 || (history != null && history.GetTotalChaosVariantSpawns() > 0)))
            UnlockEntry("chaos_variants");

        if (includeChaosWaveBlockEntry && (chaosLevel >= 2 || (history != null && history.GetTotalChaosWaveBlocksSeen() > 0)))
            UnlockEntry("chaos_waves");

        if (gameManager != null && gameManager.isGameOver)
            UnlockEntry("result_screen");
    }

    private void ApplyDiscoveryState(ChaosLexiconEntry entry)
    {
        if (entry == null || discoveredEntryIds == null)
            return;

        if (!string.IsNullOrEmpty(entry.entryId) && discoveredEntryIds.Contains(entry.entryId))
            entry.discovered = true;
    }

    private string GetRuntimeAppendixForEntry(ChaosLexiconEntry entry)
    {
        if (!appendRuntimeData || entry == null)
            return "";

        ResolveReferences();

        switch (entry.entryId)
        {
            case "grundidee":
            case "chaos_level":
                return BuildChaosRuntimeText();
            case "gerechtigkeit":
                return BuildJusticeRuntimeText();
            case "risiko_modifikatoren":
            case "risiko_details":
                return BuildRiskRuntimeText();
            case "chaos_variants":
                return BuildChaosVariantRuntimeText();
            case "chaos_waves":
                return BuildChaosWaveRuntimeText();
            case "result_screen":
                return BuildResultRuntimeText();
            case "wirtschaft_progression":
                return BuildEconomyProgressionRuntimeText();
            case "freischaltungen":
                return BuildUnlockRuntimeText();
            default:
                return "";
        }
    }

    private string BuildChaosRuntimeText()
    {
        if (chaosJusticeManager == null || chaosJusticeManager.runData == null)
            return "Aktueller Run: Chaos-Daten noch nicht verfügbar.";

        return
            "Aktueller Run: Chaos " + chaosJusticeManager.runData.chaosLevel + " / " + chaosJusticeManager.runData.maxChaosLevel +
            " | Höchstes Chaos " + chaosJusticeManager.runData.highestChaosLevel +
            "\n" + chaosJusticeManager.GetBalanceStatusLine();
    }

    private string BuildJusticeRuntimeText()
    {
        if (chaosJusticeManager == null || chaosJusticeManager.runData == null)
            return "Aktueller Run: Gerechtigkeitsdaten noch nicht verfügbar.";

        return
            "Aktueller Run: Gold-Gerechtigkeit " + chaosJusticeManager.runData.goldJusticeLevel +
            " | XP-Gerechtigkeit " + chaosJusticeManager.runData.xpJusticeLevel +
            "\nGold-Reward x" + chaosJusticeManager.GetGoldRewardMultiplier().ToString("0.00") +
            " | XP-Reward x" + chaosJusticeManager.GetXPRewardMultiplier().ToString("0.00");
    }

    private string BuildRiskRuntimeText()
    {
        if (chaosJusticeManager == null)
            return "Aktueller Run: Risiko-Daten noch nicht verfügbar.";

        return
            "Aktiver Risiko-Stand:\n" + chaosJusticeManager.GetGroupedRiskModifierSummary() +
            "\n\nDetails:\n" + chaosJusticeManager.GetDetailedRiskModifierText(10);
    }

    private string BuildChaosVariantRuntimeText()
    {
        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;

        if (history == null)
            return "Aktueller Run: Noch keine Variantenstatistik verfügbar.";

        return
            "Aktueller Run: Chaos-Varianten Spawns " + history.GetTotalChaosVariantSpawns() +
            " | Kills " + history.GetTotalChaosVariantKills() +
            " | Leaks " + history.GetTotalChaosVariantLeaks();
    }

    private string BuildChaosWaveRuntimeText()
    {
        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;

        if (history == null)
            return "Aktueller Run: Noch keine Chaos-Wave-Statistik verfügbar.";

        return
            "Aktueller Run: Chaos-Wave-Bausteine " + history.GetTotalChaosWaveBlocksSeen() +
            " in " + history.GetChaosWaveBlockWavesCompleted() + " Wave(s).";
    }

    private string BuildResultRuntimeText()
    {
        WaveHistory history = gameManager != null ? gameManager.GetWaveHistory() : null;

        if (gameManager == null || history == null)
            return "Aktueller Run: Ergebnisdaten noch nicht verfügbar.";

        string text =
            "Aktueller Run: Wave " + gameManager.waveNumber +
            " | Kills " + history.GetTotalKills() +
            " | Leaks " + history.GetTotalLeaks() +
            " | Base-Schaden " + history.GetTotalBaseDamageTaken();

        if (runStatisticsTracker != null)
        {
            text +=
                "\nWirtschaft: Gold verdient " + runStatisticsTracker.economy.totalGoldEarned +
                " | ausgegeben " + runStatisticsTracker.economy.totalGoldSpent +
                "\nTower: XP " + runStatisticsTracker.totalTowerXPGained +
                " | Level-Ups " + runStatisticsTracker.totalTowerLevelUps +
                " | höchste Stufe " + runStatisticsTracker.highestTowerLevelReached;
        }

        return text;
    }

    private string BuildEconomyProgressionRuntimeText()
    {
        if (runStatisticsTracker == null)
            return "Aktueller Run: Wirtschaft/Tower-Progression noch nicht verfügbar.";

        return runStatisticsTracker.GetEconomySummaryText() + "\n" + runStatisticsTracker.GetTowerProgressionSummaryText();
    }

    private string BuildUnlockRuntimeText()
    {
        if (unlockManager == null)
            return "Aktueller Run: Freischaltungsdaten noch nicht verfügbar.";

        return unlockManager.GetUnlockSummaryText() + "\n" + unlockManager.GetDetailedUnlockSummaryText(8);
    }

    private bool HasEntry(string entryId)
    {
        if (entries == null || string.IsNullOrEmpty(entryId))
            return false;

        foreach (ChaosLexiconEntry entry in entries)
        {
            if (entry != null && entry.entryId == entryId)
                return true;
        }

        return false;
    }

    private void AddMissingStep13Entries()
    {
        if (!HasEntry("wirtschaft_progression"))
        {
            AddEntry("wirtschaft_progression", "Wirtschaft / Tower-Progression", ChaosLexiconCategory.Auswertung, 10,
                "Dieser Eintrag erklärt und zeigt Run-Wirtschaft und Tower-Fortschritt.",
                "Die Run-Auswertung sammelt nicht nur Kills und Chaos-Daten, sondern auch Wirtschaft und Tower-Progression.\n\n" +
                "Erfasst werden verdientes Gold, ausgegebenes Gold, Goldquellen, Towerbau, Gold-Upgrades, Tower-XP, Tower-Level-Ups, Upgradepunkte und höchste Tower-Stufe.\n\n" +
                "Diese Daten sind reine Run-Auswertung. Sie geben keine Meta-Belohnung und verändern keine Balance während des Runs.",
                true, false, false, false);
        }

        if (!HasEntry("freischaltungen"))
        {
            AddEntry("freischaltungen", "Grund-Freischaltungen", ChaosLexiconCategory.Auswertung, 20,
                "V1-Freischaltungen erweitern den Content-Pool, nicht rohe Stärke.",
                "Grund-Freischaltungen öffnen neue Risiko-Modifikatoren, Chaos-Varianten- oder Chaos-Wave-Inhalte für den Angebots-Pool.\n\n" +
                "Wichtig: Eine Freischaltung garantiert keinen Sofortauftritt im nächsten Run. Sie erweitert nur den Pool. Das ist noch keine vollständige Meta-Progression und kein permanenter Stärke-Bonus.\n\n" +
                "Gesperrte Inhalte dürfen sichtbar, aber geheimnisvoll sein. Namen, genaue Bedingungen und Werte können verborgen bleiben, bis der Inhalt freigeschaltet wird.",
                true, false, false, false);
        }
    }

    private void CreateDefaultEntriesIfNeeded()
    {
        EnsureLists();

        if (entries.Count > 0)
        {
            AddMissingStep13Entries();
            return;
        }

        AddEntry("grundidee", "Chaos und Gerechtigkeit", ChaosLexiconCategory.Grundlagen, 0,
            "Nach Boss-Waves entscheidest du zwischen Ordnung und Risiko.",
            "Gerechtigkeit ist der sichere, planbare Weg. Sie verbessert Gold oder XP dauerhaft im aktuellen Run.\n\n" +
            "Chaos ist der riskante Weg. Es erhöht das Chaos-Level und kann Risiko-Modifikatoren, Chaos-Varianten und Chaos-Wave-Eigenschaften in den Run bringen.\n\n" +
            "Wichtig: Chaos-Level selbst gibt keine passiven Belohnungen. Belohnungen kommen über Gerechtigkeit, normale Rewards oder konkrete Risiko-Modifikatoren.",
            true, false, false, false);

        AddEntry("boss_entscheidung", "Boss-Entscheidung", ChaosLexiconCategory.Grundlagen, 10,
            "Nach geschafften Boss-Waves öffnet sich die Entscheidung.",
            "Die Hauptauswahl lautet: Gerechtigkeit festigen oder Chaos entfesseln.\n\n" +
            "Gerechtigkeit führt zu GoldGerechtigkeit oder XpGerechtigkeit.\n" +
            "Chaos führt zu drei zufälligen Risiko-Modifikatoren und zur sicheren Option 'Kein Modifikator'.\n\n" +
            "Alle V1-Risiken werden offen angezeigt. Boss und V1-Chaos zerstören keine Tower.",
            true, false, false, false);

        AddEntry("gerechtigkeit", "Gold- und XP-Gerechtigkeit", ChaosLexiconCategory.Gerechtigkeit, 0,
            "Gerechtigkeit ist sicherer, wirtschaftlicher Fortschritt.",
            "GoldGerechtigkeit erhöht zukünftige Gold-Belohnungen.\n" +
            "XpGerechtigkeit erhöht zukünftige Tower-XP-Belohnungen.\n\n" +
            "Beide Tracks werden getrennt gespeichert, aber als allgemeine Gerechtigkeit zusammen sichtbar.\n" +
            "Wenn du später einen Risiko-Modifikator wählst, wird die zuletzt aufgebaute passende Gerechtigkeit um 1 Stufe reduziert. 'Kein Modifikator' reduziert keine Gerechtigkeit.",
            true, false, false, false);

        AddEntry("chaos_level", "Chaos-Level 0 bis 5", ChaosLexiconCategory.Grundlagen, 20,
            "V1 nutzt Chaos 0 bis 5.",
            "Chaos 0 ist stabil. Chaos 1-2 erzeugen kontrollierten Druck. Ab Chaos 3 können Chaos-Varianten sichtbar werden. Chaos 4-5 machen Chaos-Waves deutlich präsenter.\n\n" +
            "V1 enthält kein Chaos 6 und kein Chaos 7. Es gibt in V1 keine Informationsverschleierung und keine versteckte Wave Preview.",
            true, false, false, false);

        AddEntry("risiko_modifikatoren", "Risiko-Modifikatoren", ChaosLexiconCategory.Risiko, 0,
            "Risiko-Modifikatoren sind der Kernhebel für Chaos-Belohnung und Gefahr.",
            "In der Chaos-Unterauswahl erscheinen drei zufällige Risiko-Modifikatoren.\n\n" +
            "Ein Risiko kann Gegnerzahl, Rollenmix, Spawn-Dichte, Chaos-Varianten oder Chaos-Wave-Bausteine beeinflussen. Manche Risiken geben dafür zusätzliche Gold- oder XP-Boni.\n\n" +
            "Dauerhafte Risiken sind levelbar: Wird derselbe Modifikator erneut gewählt, steigt seine Stufe statt doppelt als neuer Eintrag angelegt zu werden.",
            true, false, false, false);

        AddEntry("kein_modifikator", "Kein Modifikator", ChaosLexiconCategory.Risiko, 10,
            "Chaos kann steigen, ohne sofort einen Zusatzmodifikator zu wählen.",
            "'Kein Modifikator' erhöht das Chaos-Level, aktiviert aber keinen Risiko-Modifikator.\n\n" +
            "Diese Option gibt keine Zusatzbelohnung und reduziert keine Gerechtigkeit. Sie ist die kontrollierte Chaos-Variante innerhalb der Chaos-Unterauswahl.",
            true, false, false, false);

        AddEntry("balance", "Balance-Leiste", ChaosLexiconCategory.Grundlagen, 30,
            "Die Balance zeigt das Verhältnis zwischen Ordnung und Chaos.",
            "Die Balance-Leiste ist ein Feedback-Element. Sie soll zeigen, ob der Run stärker Richtung Gerechtigkeit oder Chaos kippt.\n\n" +
            "In dieser V1 ist sie funktional und datenbasiert. Später kann sie stärker visuell und atmosphärisch inszeniert werden.",
            true, false, false, false);

        AddEntry("fairness", "Fairness-Regeln V1", ChaosLexiconCategory.Fairness, 0,
            "Chaos darf hart sein, aber nicht unfair oder gelogen.",
            "V1-Regeln:\n" +
            "- Keine falschen Informationen.\n" +
            "- Keine versteckte Wave Preview.\n" +
            "- Keine globale Speed-Erhöhung nur durch Chaos-Level.\n" +
            "- Keine automatische Gegneranzahl-Erhöhung nur durch Chaos-Level.\n" +
            "- Base-Schaden wird nicht heimlich erhöht.\n" +
            "- Boss, MiniBoss und normale Chaos-Auswahl zerstören keine Tower.\n" +
            "- Tower-Zerstörung bleibt späteren Elite-Systemen vorbehalten.",
            true, false, false, false);

        AddEntry("chaos_variants", "Chaos-Varianten", ChaosLexiconCategory.Gegner, 0,
            "Ab Chaos 3 können einzelne Gegner als Chaos-Variante erscheinen.",
            "Chaos-Varianten ersetzen normale Gegner innerhalb der Wave. Sie erhöhen nicht automatisch die Gegneranzahl.\n\n" +
            "Sie sind lila/violett markiert und besitzen kleine Varianteneffekte, zum Beispiel mehr Haltbarkeit, Regeneration oder Widerstand gegen bestimmte Effekte.",
            false, false, true, false);

        AddEntry("chaos_waves", "Chaos-Wave-Bausteine", ChaosLexiconCategory.Waves, 0,
            "Chaos-Waves bestehen aus kontrollierten Bausteinen.",
            "V1-Bausteine sind z. B. Verdichtet, Zäh, Instabil, Nachhut, Violette Gruppe und Gepanzert.\n\n" +
            "Eine Wave hat maximal 3 Chaos-Wave-Bausteine. Bausteine sind Risiko-/Wave-Eigenschaften und geben selbst keine Extra-Rewards.",
            false, false, true, false);

        AddEntry("risiko_details", "Aktive Risiken im Run", ChaosLexiconCategory.Risiko, 20,
            "Dieser Eintrag zeigt aktuelle Risiko-Modifikatoren des Runs.",
            "Sobald Risiko-Modifikatoren aktiv sind, kannst du hier nachlesen, welche Gruppen und Stufen gerade im Run wirken.",
            false, false, true, false);

        AddEntry("result_screen", "Run-Auswertung", ChaosLexiconCategory.Auswertung, 0,
            "Der Ergebnis-Screen fasst den Run atmosphärisch und sachlich zusammen.",
            "Die Run-Auswertung zeigt Chaos-Level, Gerechtigkeit, aktive Risiken, Boss-Entscheidungen, wichtige Wave-Momente, Chaos-Varianten, Chaos-Wave-Bausteine und Kills/Leaks.\n\n" +
            "Sie ist kein Meta-System, sondern eine klare Rückschau auf den aktuellen Run.",
            true, false, false, false);

        AddEntry("chaos_elite_future", "Chaos-Elite", ChaosLexiconCategory.Zukunft, 0,
            "Der Riss antwortet.",
            "Chaos-Elite ist nicht Teil dieser aktuellen V1-Implementierung. Sie braucht zuerst ein eigenes normales Elite-System. Erst danach kann Chaos-Elite sauber eingebaut werden.",
            false, false, includeFutureInfoAsTeasers, true);

        AddEntry("autopath_future", "Autopath", ChaosLexiconCategory.Zukunft, 10,
            "Komfort, aber keine Erleichterung.",
            "Autopath ist für die vollständige Version 1.0 vorgesehen, aber nicht Teil dieses aktuellen Implementierungsschritts. Es soll keine Tower oder Upgrades automatisch übernehmen.",
            false, false, includeFutureInfoAsTeasers, true);

        AddEntry("chaos_67_future", "Chaos 6 / Chaos 7", ChaosLexiconCategory.Zukunft, 20,
            "Späteres Extrem-Chaos.",
            "Chaos 6 und Chaos 7 gehören nicht zur Version 1.0. Informationsverschleierung, ???, verschleierte Modifikator-Auswahl und verschleierte Wave Preview bleiben späteren 2.0-Schritten vorbehalten.",
            false, false, includeFutureInfoAsTeasers, true);

        AddMissingStep13Entries();
    }

    private void AddEntry(string id, string title, ChaosLexiconCategory category, int sortOrder, string shortText, string detailText, bool unlockedByDefault, bool discovered, bool teaser, bool futureOnly)
    {
        ChaosLexiconEntry entry = new ChaosLexiconEntry();
        entry.entryId = id;
        entry.title = title;
        entry.category = category;
        entry.sortOrder = sortOrder;
        entry.shortText = shortText;
        entry.detailText = detailText;
        entry.unlockedByDefault = unlockedByDefault;
        entry.discovered = discovered;
        entry.showAsLockedTeaser = teaser;
        entry.futureOnly = futureOnly;
        entry.sourceTag = "Step12Default";
        entries.Add(entry);
    }

    private int CompareEntries(ChaosLexiconEntry a, ChaosLexiconEntry b)
    {
        if (a == null && b == null)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int categoryCompare = ((int)a.category).CompareTo((int)b.category);

        if (categoryCompare != 0)
            return categoryCompare;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private void EnsureLists()
    {
        if (entries == null)
            entries = new List<ChaosLexiconEntry>();

        if (discoveredEntryIds == null)
            discoveredEntryIds = new List<string>();
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
