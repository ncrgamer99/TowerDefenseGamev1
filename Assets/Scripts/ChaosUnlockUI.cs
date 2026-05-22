using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ChaosUnlockMenuSection
{
    Overview,
    General,
    TowerMastery,
    ChaosResearch,
    PathTechnique,
    EliteHunt,
    Archive
}

public class ChaosUnlockUI : MonoBehaviour
{
    private struct MetaHubEntry
    {
        public string entryId;
        public string title;
        public string stateText;
        public string detailText;
        public bool locked;

        public MetaHubEntry(string entryId, string title, string stateText, string detailText, bool locked = false)
        {
            this.entryId = entryId;
            this.title = title;
            this.stateText = stateText;
            this.detailText = detailText;
            this.locked = locked;
        }
    }

    [Header("References")]
    public ChaosUnlockManager manager;
    public Canvas targetCanvas;

    [Header("UI")]
    public GameObject rootPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI summaryText;
    public TextMeshProUGUI detailText;
    public Transform sectionButtonContent;
    public Transform entryListContent;
    public Button closeButton;
    public Button refreshButton;
    public Button primaryActionButton;
    public TextMeshProUGUI primaryActionButtonText;

    [Header("Notification")]
    public GameObject notificationPanel;
    public TextMeshProUGUI notificationText;
    public float notificationDuration = 4f;

    [Header("Auto Create")]
    public bool autoCreateUIIfMissing = true;

    [Header("Theme")]
    public Color overlayColor = new Color32(0, 0, 0, 135);
    public Color windowColor = new Color32(20, 24, 31, 248);
    public Color headerColor = new Color32(214, 164, 65, 255);
    public Color listPanelColor = new Color32(22, 39, 60, 245);
    public Color detailPanelColor = new Color32(20, 30, 44, 245);
    public Color buttonColor = new Color32(35, 45, 64, 255);
    public Color unlockedButtonColor = new Color32(65, 125, 245, 255);
    public Color lockedButtonColor = new Color32(55, 64, 80, 255);
    public Color closeButtonColor = new Color32(200, 75, 75, 255);
    public Color textColor = new Color32(240, 244, 250, 255);

    [Header("Text")]
    public float titleFontSize = 28f;
    public float summaryFontSize = 15f;
    public float detailFontSize = 16f;
    public float entryButtonFontSize = 15f;

    private readonly List<Button> generatedSectionButtons = new List<Button>();
    private readonly List<Button> generatedEntryButtons = new List<Button>();
    private ChaosUnlockMenuSection selectedSection = ChaosUnlockMenuSection.Overview;
    private string selectedMetaHubEntryId = "";
    private float notificationTimer = 0f;

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<ChaosUnlockManager>();

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (manager != null)
            manager.unlockUI = this;

        EnsureUI();
        SetupButtons();
        CloseUnlocks();
    }

    private void Update()
    {
        if (notificationTimer > 0f)
        {
            notificationTimer -= Time.deltaTime;

            if (notificationTimer <= 0f && notificationPanel != null)
                notificationPanel.SetActive(false);
        }
    }

    public void Connect(ChaosUnlockManager newManager)
    {
        manager = newManager;
        EnsureUI();
        SetupButtons();
        CloseUnlocks();
    }

    public void OpenUnlocks()
    {
        EnsureUI();

        if (rootPanel != null)
        {
            rootPanel.transform.SetAsLastSibling();
            rootPanel.SetActive(true);
        }

        RefreshAll();
    }

    public void CloseUnlocks()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void RefreshAll()
    {
        EnsureUI();
        RefreshSectionButtons();
        RefreshList();
        RefreshDetail();
    }

    public void ShowNotification(string message)
    {
        EnsureUI();

        if (notificationPanel == null || notificationText == null)
        {
            Debug.Log(message);
            return;
        }

        notificationText.text = message;
        notificationPanel.SetActive(true);
        notificationTimer = Mathf.Max(0.5f, notificationDuration);
    }

    private void SetupButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseFromButton);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(RefreshFromButton);
        }

        if (primaryActionButton != null)
        {
            primaryActionButton.onClick.RemoveAllListeners();
            primaryActionButton.onClick.AddListener(HandlePrimaryActionButton);
        }
    }

    private void CloseFromButton()
    {
        if (manager != null)
            manager.CloseUnlocks();
        else
            CloseUnlocks();
    }

    private void RefreshFromButton()
    {
        if (manager != null)
            manager.RefreshAndNotify();
        else
            RefreshAll();
    }

    private void RefreshList()
    {
        if (entryListContent == null)
            return;

        ClearGeneratedEntryButtons();

        if (titleText != null)
            titleText.text = "META-HUB / ARCHIV DER VERTEIDIGUNG";

        if (summaryText != null)
            summaryText.text = GetTopBarText();

        List<MetaHubEntry> entries = GetVisibleMetaHubEntries();
        EnsureSelectedMetaHubEntry(entries);

        foreach (MetaHubEntry entry in entries)
            generatedEntryButtons.Add(CreateMetaHubEntryButton(entry));
    }

    private Button CreateMetaHubEntryButton(MetaHubEntry entry)
    {
        bool selected = entry.entryId == selectedMetaHubEntryId;
        GameObject buttonObject = new GameObject("MetaHubEntryButton_" + entry.entryId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(entryListContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 58f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? GetSectionAccentColor(selectedSection) : entry.locked ? lockedButtonColor : buttonColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string entryId = entry.entryId;
        button.onClick.AddListener(() => SelectMetaHubEntry(entryId));
        ApplySelectableColors(button, image.color, GetSectionAccentColor(selectedSection));

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", entryButtonFontSize, TextAlignmentOptions.MidlineLeft, Color.white);
        SetOffsets(text.rectTransform, 12f, 5f, 12f, 24f);
        text.text = entry.title;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI state = CreateText(buttonObject.transform, "StateText", 12f, TextAlignmentOptions.BottomLeft, new Color32(184, 194, 208, 255));
        SetOffsets(state.rectTransform, 12f, 35f, 12f, 6f);
        state.text = entry.stateText;
        state.enableWordWrapping = false;
        state.overflowMode = TextOverflowModes.Ellipsis;
        return button;
    }

    private void SelectMetaHubEntry(string entryId)
    {
        selectedMetaHubEntryId = entryId;
        RefreshList();
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (detailText == null)
            return;

        MetaHubEntry entry = GetSelectedMetaHubEntry();
        detailText.richText = true;
        detailText.text = GetDetailText(entry);
        RefreshPrimaryActionButton(entry);
    }

    private void ClearGeneratedEntryButtons()
    {
        foreach (Button button in generatedEntryButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedEntryButtons.Clear();
    }

    private void RefreshSectionButtons()
    {
        if (sectionButtonContent == null)
            return;

        ClearGeneratedSectionButtons();

        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.Overview));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.General));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.TowerMastery));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.ChaosResearch));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.PathTechnique));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.EliteHunt));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.Archive));
    }

    private Button CreateSectionButton(ChaosUnlockMenuSection section)
    {
        bool selected = selectedSection == section;
        Color accentColor = GetSectionAccentColor(section);
        Color baseColor = selected ? accentColor : buttonColor;

        GameObject buttonObject = new GameObject("MetaHubSectionButton_" + section, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(sectionButtonContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 45f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = baseColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectSection(section));
        ApplySelectableColors(button, baseColor, accentColor);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", entryButtonFontSize, TextAlignmentOptions.MidlineLeft, Color.white);
        text.text = GetSectionDisplayName(section);
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(text.rectTransform, 12f, 3f, 12f, 3f);
        return button;
    }

    private void SelectSection(ChaosUnlockMenuSection section)
    {
        selectedSection = section;
        selectedMetaHubEntryId = "";
        RefreshAll();
    }

    private void ClearGeneratedSectionButtons()
    {
        foreach (Button button in generatedSectionButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedSectionButtons.Clear();
    }

    private void EnsureSelectedMetaHubEntry(List<MetaHubEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            selectedMetaHubEntryId = "";
            return;
        }

        foreach (MetaHubEntry entry in entries)
        {
            if (entry.entryId == selectedMetaHubEntryId)
                return;
        }

        selectedMetaHubEntryId = entries[0].entryId;
    }

    private MetaHubEntry GetSelectedMetaHubEntry()
    {
        List<MetaHubEntry> entries = GetVisibleMetaHubEntries();
        EnsureSelectedMetaHubEntry(entries);

        foreach (MetaHubEntry entry in entries)
        {
            if (entry.entryId == selectedMetaHubEntryId)
                return entry;
        }

        return entries.Count > 0 ? entries[0] : new MetaHubEntry("empty", "Vorbereitet", "Leer", "Dieser Bereich ist vorbereitet.");
    }

    private List<MetaHubEntry> GetVisibleMetaHubEntries()
    {
        switch (selectedSection)
        {
            case ChaosUnlockMenuSection.General:
                return BuildGeneralEntries();
            case ChaosUnlockMenuSection.TowerMastery:
                return BuildTowerMasteryEntries();
            case ChaosUnlockMenuSection.ChaosResearch:
                return BuildChaosResearchEntries();
            case ChaosUnlockMenuSection.PathTechnique:
                return BuildPathTechniqueEntries();
            case ChaosUnlockMenuSection.EliteHunt:
                return BuildEliteHuntEntries();
            case ChaosUnlockMenuSection.Archive:
                return BuildArchiveEntries();
            default:
                return BuildOverviewEntries();
        }
    }

    private List<MetaHubEntry> BuildOverviewEntries()
    {
        return new List<MetaHubEntry>
        {
            Entry("overview_last_run", "Letzter Run", "Nachbericht vorbereitet", "Kurzfassung fuer letzten Run: erreichte Wave, Bosskills, Chaos, Tower-Mastery-Auszahlung, Kernwissen und neue Unlocks.\n\nAktuell ist dies nur eine Anzeigevorbereitung. Die echte Auszahlung wird spaeter an Result-/RunStatistics-Daten angebunden."),
            Entry("overview_goals", "Naechste Ziele", "Zielvorschlaege vorbereitet", "Empfohlene Ziele: Tower-Level-Meilensteine, Chaos-3-Waves, Verbau ueberleben, Milestones freischalten.\n\nSpaeter koennen Ziele angepinnt und im Run-HUD angezeigt werden."),
            Entry("overview_progress_cards", "Fortschrittskarten", "Bereiche vorbereitet", "Karten fuer Allgemein, Tower Mastery, Chaos-Forschung, Pfadtechnik und Elite-Jagd.\n\nSie zeigen spaeter verfuegbare Kaeufe, unspent Points, gesperrte Systeme und neue Unlock-Badges."),
            Entry("overview_loadout", "Aktives Loadout", "Read-only Vorschau", "Zeigt spaeter aktive allgemeine Boni, Tower-Keystones, Chaos-Konter, Pfadtechnik und Elite-Jagd.\n\nRegel: Meta-Kaeufe und Keystone-Wechsel wirken nie auf den laufenden Run.")
        };
    }

    private List<MetaHubEntry> BuildGeneralEntries()
    {
        return new List<MetaHubEntry>
        {
            Entry("general_account", "Account-Level", "Kernwissen-XP vorbereitet", "Account-Level, Kernwissen-XP, neue Bestleistungen und allgemeine Milestones.\n\nAllgemein ist das Dachsystem fuer Vorbereitung und Optionen, nicht fuer globale All-Tower-Schadensboni."),
            Entry("general_tower_unlocks", "Tower freischalten", "Content-Unlocks", "Langfristige Tower-Freischaltung: Basic, Rapid, Heavy, Fire, Slow, Poison, spaeter Sniper, Alchemist, Lightning, Mortar und Spike.\n\nContent-Unlocks sind dauerhaft aktiv."),
            Entry("general_tile_unlocks", "Tiles freischalten", "Pfad-Content", "Spezial-Tiles wie Trap, Gold, Slow, Range, Damage, Rate, XP, Upgrade, Combo, Bridge und Special Path.\n\nPower-Tiles bleiben vorsichtig und spaeter gatebar."),
            Entry("general_qol", "Komfort / QoL", "Immer aktiv", "GameSpeed, bessere Wave-Preview, bessere Tower-Stats, Ziel-Pinning, Result-Zusammenfassung und Tooltips.\n\nQoL braucht kein Power-Loadout."),
            Entry("general_start_options", "Startoptionen", "Loadout-pflichtig", "Startgold, Startleben, Startweg, erster Tower guenstiger oder Start-Reroll.\n\nStarke Startoptionen brauchen Slot-Kosten und wirken erst ab dem naechsten Run."),
            Entry("general_loadout", "Meta-Loadout", "Power begrenzen", "Besitzen ist dauerhaft. Aktivieren ist begrenzt.\n\nContent- und QoL-Unlocks sind dauerhaft aktiv; Power-Unlocks brauchen Slots oder Budget.")
        };
    }

    private List<MetaHubEntry> BuildTowerMasteryEntries()
    {
        List<MetaHubEntry> entries = new List<MetaHubEntry>();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        BasicTowerMasteryManager basicMastery = GetBasicTowerMasteryManager();
        RapidTowerMasteryManager rapidMastery = GetRapidTowerMasteryManager();
        HeavyTowerMasteryManager heavyMastery = GetHeavyTowerMasteryManager();
        FireTowerMasteryManager fireMastery = GetFireTowerMasteryManager();

        entries.Add(Entry("tower_global_rules", "Globale Mastery-Regeln", "Aktiv", towerMastery != null ? towerMastery.GetGlobalRulesText() : "Globale Tower-Mastery-Regeln vorbereitet."));
        entries.Add(Entry("tower_basic_overview", "Basic Tower Mastery", GetBasicMasteryStateText(basicMastery), ""));

        if (basicMastery != null)
        {
            foreach (BasicTowerMasteryNodeDefinition definition in basicMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !basicMastery.IsMilestoneUnlocked(definition.gate) && basicMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("basic_node_" + definition.nodeId, definition.displayName, basicMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_rapid_overview", "Rapid Tower Mastery", GetRapidMasteryStateText(rapidMastery), ""));

        if (rapidMastery != null)
        {
            foreach (RapidTowerMasteryNodeDefinition definition in rapidMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !rapidMastery.IsMilestoneUnlocked(definition.gate) && rapidMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("rapid_node_" + definition.nodeId, definition.displayName, rapidMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_heavy_overview", "Heavy Tower Mastery", GetHeavyMasteryStateText(heavyMastery), ""));

        if (heavyMastery != null)
        {
            foreach (HeavyTowerMasteryNodeDefinition definition in heavyMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !heavyMastery.IsMilestoneUnlocked(definition.gate) && heavyMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("heavy_node_" + definition.nodeId, definition.displayName, heavyMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_fire_overview", "Fire Tower Mastery", GetFireMasteryStateText(fireMastery), ""));

        if (fireMastery != null)
        {
            foreach (FireTowerMasteryNodeDefinition definition in fireMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !fireMastery.IsMilestoneUnlocked(definition.gate) && fireMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("fire_node_" + definition.nodeId, definition.displayName, fireMastery.GetNodeStateText(definition), "", locked));
            }
        }

        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            if (role == TowerRole.Basic || role == TowerRole.Rapid || role == TowerRole.Heavy || role == TowerRole.Fire)
                continue;

            entries.Add(TowerEntry(role, IsLaterTowerRole(role), towerMastery));
        }

        return entries;
    }

    private List<MetaHubEntry> BuildChaosResearchEntries()
    {
        return new List<MetaHubEntry>
        {
            Entry("chaos_risk_pool", "Risiko-Pool", GetChaosUnlockStateText(), "Neue Risiko-Modifikatoren, Reward-Risiken, Rollen-Druck, MiniBoss- und PreBoss-Druck.\n\n" + GetChaosUnlockRuntimeText()),
            Entry("chaos_variants", "Chaos-Varianten", "Forschung vorbereitet", "Chaos-Runner, Chaos-Tank, Chaos-Knight, Chaos-Mage, Chaos-Learner und Chaos-AllRounder.\n\nKonter bleiben klein und spezifisch."),
            Entry("chaos_waves", "Chaos-Waves", "Bausteine vorbereitet", "Density, Toughness, ChaosVariantGroup, Rearguard, Armor, Resistance und spaeter PreviewHidden.\n\nKeine Informationsverschleierung in der aktuellen Phase."),
            Entry("chaos_counters", "Chaos-Konter", "kleine Konter", "Spezifische Antworten wie Knight-Analyse, Learner-Gegenprobe, Mage-Stoerung und Armor-Forschung.\n\nKeine globalen starken Chaos-Nerfs."),
            Entry("chaos_endgame", "Chaos-5-Endgame", "Spaeter", "Seltene Risskern-Upgrades, Chaos-Angebot-Rerolls, bessere Vorschau und spaete Chaos-Keystones.", true),
            Entry("chaos_order", "Gerechtigkeit / Ordnung", "Justice-Milestones", "Gold-/XP-Gerechtigkeit, sichere Runs, Ordnungspfad-Lexikon und kleine Boni fuer Runs ohne Chaos.")
        };
    }

    private List<MetaHubEntry> BuildPathTechniqueEntries()
    {
        return new List<MetaHubEntry>
        {
            Entry("path_event_pool", "Event-Pool", "Verbau-Optionen", "Neue Verbau-Optionen wie Baupause, Lebensreserve, Goldreserve, Evolutionspunkt, Nachschulung, Neue Basis, Teleporter, Chaos ordnen und Pfadscan."),
            Entry("path_event_quality", "Event-Qualitaet", "Auswahl verbessern", "Bessere Auswahl, Reroll, Option bannen, +1 Auswahloption und bessere Vorschau.\n\nJedes Event bleibt sichtbar und darf keine unfairen Sofortverluste erzeugen."),
            Entry("path_rescue_power", "Rettungsstaerke", "hart gecappt", "Kleine Verbesserungen bestehender Events: mehr Leben, Gold, Bauzeit oder bessere Nachschulung.\n\nKeine endlose Verbau-Farm."),
            Entry("path_tools", "Pfadwerkzeuge", "Werkzeuge vorbereitet", "Teleporter, Base-Relocation, Pfad-Reroll, Spezial-Tile-Auswahl und bessere Anzeige der letzten Weg-Erweiterung."),
            Entry("path_tile_tech", "Tile-Technik", "Tile-Forschung", "Trap, Gold, Bridge, Special Path und spaeter Combo, XP, Upgrade, Range, Damage und Rate Tiles.")
        };
    }

    private List<MetaHubEntry> BuildEliteHuntEntries()
    {
        return new List<MetaHubEntry>
        {
            Entry("elite_locked", "Elite-Jagd", "Gesperrt", "Spaeteres Endgame-System.\n\nMoegliche Bedingungen: Boss bei Chaos 5 besiegen, Account-Level erreichen oder Wave 30 ueberstehen.", true),
            Entry("elite_contracts", "Elite-Auftraege", "Gesperrt", "Auftraege wie Elite Runner besiegen, Elite Mage ohne Base-Schaden oder Elite-Wave mit Chaos-Wave-Block ueberstehen.", true),
            Entry("elite_affixes", "Elite-Affixe", "Gesperrt", "Schnell, gepanzert, regenerierend, teleportierend, resistent, splitternd, Nachhut-Elite und Riss-Elite.", true),
            Entry("elite_rewards", "Elite-Belohnungen", "Gesperrt", "Kernwissen, Elite-Siegel, seltene Tower-Mastery-XP, kosmetische Tower-Visuals, Elite-Lexikon und Trophäen.", true),
            Entry("elite_frequency", "Elite-Haeufigkeit", "Opt-in spaeter", "Elite-Jagd: Aus / Leicht / Normal / Hart. Mehr Risiko gibt bessere Belohnungen.", true),
            Entry("elite_counters", "Elite-Konter", "Gesperrt", "Kleine spezifische Forschung, bessere Anzeige und klare Preview. Keine globalen starken Nerfs.", true)
        };
    }

    private List<MetaHubEntry> BuildArchiveEntries()
    {
        return new List<MetaHubEntry>
        {
            Entry("archive_basics", "Grundlagen", "Archiv", "Runs, Meta, Keystone-Regeln, Loadout und Auszahlung erklaeren."),
            Entry("archive_tower", "Tower", "Archiv", "Tower-Rollen, Mastery, Keystones und Milestone-Regeln."),
            Entry("archive_enemies", "Gegner", "Archiv", "Gegnerrollen, Varianten, Elite und Konterwissen."),
            Entry("archive_chaos", "Chaos", "Archiv", "Chaos-Level, Risiken, Varianten, Chaos-Waves und Risskerne."),
            Entry("archive_justice", "Gerechtigkeit", "Archiv", "Gold-/XP-Gerechtigkeit und Ordnungspfad."),
            Entry("archive_path", "Pfadtechnik", "Archiv", "Tiles, Verbau, Teleporter, Base-Relocation und Pfadwerkzeuge."),
            Entry("archive_results", "Auswertung", "Archiv", "Run-Stats, Tower-Stats, Wave-History und Freischaltungen."),
            Entry("archive_future", "Zukunft", "Archiv", "Spaetere Systeme und bewusst gesperrte Inhalte.")
        };
    }

    private MetaHubEntry TowerEntry(TowerRole role, bool locked, TowerMasteryManager towerMastery)
    {
        string state = locked ? "Gesperrt" : (towerMastery != null ? towerMastery.GetRoleListStateText(role) : "Tree vorbereitet");
        string detail =
            "Struktur: linearer Einstieg, drei Pfade, Milestones I-V und drei Keystones.\n\n" +
            "Regeln:\n" +
            "- Kleine und mittlere Nodes bleiben passiv aktiv.\n" +
            "- Pro Tower ist genau ein Keystone aktiv.\n" +
            "- Punkte entstehen am Run-Ende ueber Level, Wave-Tiefe und Impact.\n" +
            "- Kaufen und Keystone-Wechsel wirken erst fuer den naechsten Run.";

        if (locked)
            detail += "\n\n<color=#B9C2D0>Dieser Tower ist fuer spaetere Freischaltung vorbereitet.</color>";

        return Entry("tower_role_" + role.ToString().ToLowerInvariant(), TowerMasteryManager.GetTowerDisplayName(role), state, detail, locked);
    }

    private MetaHubEntry Entry(string entryId, string title, string stateText, string detailText, bool locked = false)
    {
        return new MetaHubEntry(entryId, title, stateText, detailText, locked);
    }

    private string GetDetailText(MetaHubEntry entry)
    {
        if (string.IsNullOrEmpty(entry.entryId))
            return "Dieser Bereich ist vorbereitet.";

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        BasicTowerMasteryManager basicMastery = GetBasicTowerMasteryManager();
        RapidTowerMasteryManager rapidMastery = GetRapidTowerMasteryManager();
        HeavyTowerMasteryManager heavyMastery = GetHeavyTowerMasteryManager();
        FireTowerMasteryManager fireMastery = GetFireTowerMasteryManager();

        if (entry.entryId == "tower_global_rules" && towerMastery != null)
            return "<b>Globale Tower-Mastery-Regeln</b>\n<size=90%><color=#B9C2D0>Aktiv fuer alle TowerRoles</color></size>\n\n" + towerMastery.GetGlobalRulesText();

        if (entry.entryId == "tower_basic_overview" && basicMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Basic) + "\n\n" : "";
            return "<b>Basic Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetBasicMasteryStateText(basicMastery) + "</color></size>\n\n" + globalText + "Konkreter Basic-Tree:\n" + basicMastery.GetOverviewText();
        }

        string basicNodeId = GetBasicNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(basicNodeId) && basicMastery != null)
            return basicMastery.GetNodeDetailText(basicNodeId);

        if (entry.entryId == "tower_rapid_overview" && rapidMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Rapid) + "\n\n" : "";
            return "<b>Rapid Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetRapidMasteryStateText(rapidMastery) + "</color></size>\n\n" + globalText + "Konkreter Rapid-Tree:\n" + rapidMastery.GetOverviewText();
        }

        string rapidNodeId = GetRapidNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(rapidNodeId) && rapidMastery != null)
            return rapidMastery.GetNodeDetailText(rapidNodeId);

        if (entry.entryId == "tower_heavy_overview" && heavyMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Heavy) + "\n\n" : "";
            return "<b>Heavy Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetHeavyMasteryStateText(heavyMastery) + "</color></size>\n\n" + globalText + "Konkreter Heavy-Tree:\n" + heavyMastery.GetOverviewText();
        }

        string heavyNodeId = GetHeavyNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(heavyNodeId) && heavyMastery != null)
            return heavyMastery.GetNodeDetailText(heavyNodeId);

        if (entry.entryId == "tower_fire_overview" && fireMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Fire) + "\n\n" : "";
            return "<b>Fire Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetFireMasteryStateText(fireMastery) + "</color></size>\n\n" + globalText + "Konkreter Fire-Tree:\n" + fireMastery.GetOverviewText();
        }

        string fireNodeId = GetFireNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(fireNodeId) && fireMastery != null)
            return fireMastery.GetNodeDetailText(fireNodeId);

        TowerRole towerRole;
        if (TryGetTowerRoleFromEntry(entry.entryId, out towerRole) && towerMastery != null)
            return "<b>" + TowerMasteryManager.GetTowerDisplayName(towerRole) + " Mastery</b>\n<size=90%><color=#B9C2D0>" + entry.stateText + "</color></size>\n\n" + towerMastery.GetRoleOverviewText(towerRole) + "\n" + entry.detailText;

        string status = entry.locked ? "Gesperrt" : entry.stateText;
        return "<b>" + entry.title + "</b>\n<size=90%><color=#B9C2D0>" + status + "</color></size>\n\n" + entry.detailText;
    }

    private void RefreshPrimaryActionButton(MetaHubEntry entry)
    {
        if (primaryActionButton == null)
            return;

        string basicNodeId = GetBasicNodeIdFromEntry(entry.entryId);
        BasicTowerMasteryManager basicMastery = GetBasicTowerMasteryManager();

        if (!string.IsNullOrEmpty(basicNodeId) && basicMastery != null)
        {
            RefreshBasicPrimaryActionButton(basicMastery, basicNodeId);
            return;
        }

        string rapidNodeId = GetRapidNodeIdFromEntry(entry.entryId);
        RapidTowerMasteryManager rapidMastery = GetRapidTowerMasteryManager();

        if (!string.IsNullOrEmpty(rapidNodeId) && rapidMastery != null)
        {
            RefreshRapidPrimaryActionButton(rapidMastery, rapidNodeId);
            return;
        }

        string heavyNodeId = GetHeavyNodeIdFromEntry(entry.entryId);
        HeavyTowerMasteryManager heavyMastery = GetHeavyTowerMasteryManager();

        if (!string.IsNullOrEmpty(heavyNodeId) && heavyMastery != null)
        {
            RefreshHeavyPrimaryActionButton(heavyMastery, heavyNodeId);
            return;
        }

        string fireNodeId = GetFireNodeIdFromEntry(entry.entryId);
        FireTowerMasteryManager fireMastery = GetFireTowerMasteryManager();

        if (!string.IsNullOrEmpty(fireNodeId) && fireMastery != null)
        {
            RefreshFirePrimaryActionButton(fireMastery, fireNodeId);
            return;
        }

        primaryActionButton.gameObject.SetActive(false);
    }

    private void RefreshBasicPrimaryActionButton(BasicTowerMasteryManager basicMastery, string basicNodeId)
    {
        if (basicMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        BasicTowerMasteryNodeDefinition definition = basicMastery.GetDefinition(basicNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = basicMastery.GetNodeRank(basicNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = basicMastery.CanPurchaseNode(basicNodeId);
        bool canActivate = maxed && definition.keystone != BasicTowerKeystone.None && basicMastery.activeKeystone != definition.keystone && basicMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = basicMastery.CanEditMetaProgression();

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivate;

        if (primaryActionButtonText != null)
        {
            if (!canEdit)
                primaryActionButtonText.text = "Read-only im Run";
            else if (canBuy)
                primaryActionButtonText.text = "Kaufen (" + definition.GetCostForNextRank(rank) + ")";
            else if (canActivate)
                primaryActionButtonText.text = "Keystone aktivieren";
            else if (maxed && definition.keystone != BasicTowerKeystone.None && basicMastery.activeKeystone == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = basicMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshRapidPrimaryActionButton(RapidTowerMasteryManager rapidMastery, string rapidNodeId)
    {
        if (rapidMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        RapidTowerMasteryNodeDefinition definition = rapidMastery.GetDefinition(rapidNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = rapidMastery.GetNodeRank(rapidNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = rapidMastery.CanPurchaseNode(rapidNodeId);
        bool canActivate = maxed && definition.keystone != RapidTowerKeystone.None && rapidMastery.GetActiveKeystone() != definition.keystone && rapidMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = rapidMastery.CanEditMetaProgression();

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivate;

        if (primaryActionButtonText != null)
        {
            if (!canEdit)
                primaryActionButtonText.text = "Read-only im Run";
            else if (canBuy)
                primaryActionButtonText.text = "Kaufen (" + definition.GetCostForNextRank(rank) + ")";
            else if (canActivate)
                primaryActionButtonText.text = "Keystone aktivieren";
            else if (maxed && definition.keystone != RapidTowerKeystone.None && rapidMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = rapidMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshHeavyPrimaryActionButton(HeavyTowerMasteryManager heavyMastery, string heavyNodeId)
    {
        if (heavyMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        HeavyTowerMasteryNodeDefinition definition = heavyMastery.GetDefinition(heavyNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = heavyMastery.GetNodeRank(heavyNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = heavyMastery.CanPurchaseNode(heavyNodeId);
        bool canActivate = maxed && definition.keystone != HeavyTowerKeystone.None && heavyMastery.GetActiveKeystone() != definition.keystone && heavyMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = heavyMastery.CanEditMetaProgression();

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivate;

        if (primaryActionButtonText != null)
        {
            if (!canEdit)
                primaryActionButtonText.text = "Read-only im Run";
            else if (canBuy)
                primaryActionButtonText.text = "Kaufen (" + definition.GetCostForNextRank(rank) + ")";
            else if (canActivate)
                primaryActionButtonText.text = "Keystone aktivieren";
            else if (maxed && definition.keystone != HeavyTowerKeystone.None && heavyMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = heavyMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshFirePrimaryActionButton(FireTowerMasteryManager fireMastery, string fireNodeId)
    {
        if (fireMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        FireTowerMasteryNodeDefinition definition = fireMastery.GetDefinition(fireNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = fireMastery.GetNodeRank(fireNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = fireMastery.CanPurchaseNode(fireNodeId);
        bool canActivate = maxed && definition.keystone != FireTowerKeystone.None && fireMastery.GetActiveKeystone() != definition.keystone && fireMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = fireMastery.CanEditMetaProgression();

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivate;

        if (primaryActionButtonText != null)
        {
            if (!canEdit)
                primaryActionButtonText.text = "Read-only im Run";
            else if (canBuy)
                primaryActionButtonText.text = "Kaufen (" + definition.GetCostForNextRank(rank) + ")";
            else if (canActivate)
                primaryActionButtonText.text = "Keystone aktivieren";
            else if (maxed && definition.keystone != FireTowerKeystone.None && fireMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = fireMastery.GetNodeStateText(definition);
        }
    }

    private void HandlePrimaryActionButton()
    {
        string basicNodeId = GetBasicNodeIdFromEntry(selectedMetaHubEntryId);
        BasicTowerMasteryManager basicMastery = GetBasicTowerMasteryManager();

        if (!string.IsNullOrEmpty(basicNodeId) && basicMastery != null)
        {
            BasicTowerMasteryNodeDefinition definition = basicMastery.GetDefinition(basicNodeId);

            if (definition == null)
                return;

            int rank = basicMastery.GetNodeRank(basicNodeId);

            if (rank >= definition.maxRank && definition.keystone != BasicTowerKeystone.None)
                basicMastery.TryActivateKeystone(definition.keystone);
            else
                basicMastery.TryPurchaseNode(basicNodeId);

            RefreshAll();
            return;
        }

        string fireNodeId = GetFireNodeIdFromEntry(selectedMetaHubEntryId);
        FireTowerMasteryManager fireMastery = GetFireTowerMasteryManager();

        if (!string.IsNullOrEmpty(fireNodeId) && fireMastery != null)
        {
            FireTowerMasteryNodeDefinition definition = fireMastery.GetDefinition(fireNodeId);

            if (definition == null)
                return;

            int rank = fireMastery.GetNodeRank(fireNodeId);

            if (rank >= definition.maxRank && definition.keystone != FireTowerKeystone.None)
                fireMastery.TryActivateKeystone(definition.keystone);
            else
                fireMastery.TryPurchaseNode(fireNodeId);

            RefreshAll();
            return;
        }

        string rapidNodeId = GetRapidNodeIdFromEntry(selectedMetaHubEntryId);
        RapidTowerMasteryManager rapidMastery = GetRapidTowerMasteryManager();

        if (string.IsNullOrEmpty(rapidNodeId) || rapidMastery == null)
        {
            string heavyNodeId = GetHeavyNodeIdFromEntry(selectedMetaHubEntryId);
            HeavyTowerMasteryManager heavyMastery = GetHeavyTowerMasteryManager();

            if (string.IsNullOrEmpty(heavyNodeId) || heavyMastery == null)
                return;

            HeavyTowerMasteryNodeDefinition heavyDefinition = heavyMastery.GetDefinition(heavyNodeId);

            if (heavyDefinition == null)
                return;

            int heavyRank = heavyMastery.GetNodeRank(heavyNodeId);

            if (heavyRank >= heavyDefinition.maxRank && heavyDefinition.keystone != HeavyTowerKeystone.None)
                heavyMastery.TryActivateKeystone(heavyDefinition.keystone);
            else
                heavyMastery.TryPurchaseNode(heavyNodeId);

            RefreshAll();
            return;
        }

        RapidTowerMasteryNodeDefinition rapidDefinition = rapidMastery.GetDefinition(rapidNodeId);

        if (rapidDefinition == null)
            return;

        int rapidRank = rapidMastery.GetNodeRank(rapidNodeId);

        if (rapidRank >= rapidDefinition.maxRank && rapidDefinition.keystone != RapidTowerKeystone.None)
            rapidMastery.TryActivateKeystone(rapidDefinition.keystone);
        else
            rapidMastery.TryPurchaseNode(rapidNodeId);

        RefreshAll();
    }

    private string GetBasicNodeIdFromEntry(string entryId)
    {
        const string prefix = "basic_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetHeavyNodeIdFromEntry(string entryId)
    {
        const string prefix = "heavy_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetFireNodeIdFromEntry(string entryId)
    {
        const string prefix = "fire_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetRapidNodeIdFromEntry(string entryId)
    {
        const string prefix = "rapid_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private bool TryGetTowerRoleFromEntry(string entryId, out TowerRole role)
    {
        const string prefix = "tower_role_";
        role = TowerRole.Basic;

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return false;

        string roleName = entryId.Substring(prefix.Length);

        foreach (TowerRole candidate in TowerMasteryManager.GetOrderedTowerRoles())
        {
            if (candidate.ToString().ToLowerInvariant() == roleName)
            {
                role = candidate;
                return true;
            }
        }

        return false;
    }

    private bool IsLaterTowerRole(TowerRole role)
    {
        return role == TowerRole.Sniper ||
               role == TowerRole.Alchemist ||
               role == TowerRole.Lightning ||
               role == TowerRole.Mortar ||
               role == TowerRole.Spike;
    }

    private string GetBasicMasteryStateText(BasicTowerMasteryManager basicMastery)
    {
        if (basicMastery == null)
            return "System vorbereitet";

        return "XP " + basicMastery.masteryXP + " | Punkte " + basicMastery.unspentPoints + " frei | " + basicMastery.spentPoints + " ausgegeben";
    }

    private string GetRapidMasteryStateText(RapidTowerMasteryManager rapidMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (rapidMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Rapid);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetHeavyMasteryStateText(HeavyTowerMasteryManager heavyMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (heavyMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Heavy);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetFireMasteryStateText(FireTowerMasteryManager fireMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (fireMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Fire);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private BasicTowerMasteryManager GetBasicTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetBasicTowerMasteryManager();

        return BasicTowerMasteryManager.GetOrCreate();
    }

    private RapidTowerMasteryManager GetRapidTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetRapidTowerMasteryManager();

        return RapidTowerMasteryManager.GetOrCreate();
    }

    private HeavyTowerMasteryManager GetHeavyTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetHeavyTowerMasteryManager();

        return HeavyTowerMasteryManager.GetOrCreate();
    }

    private FireTowerMasteryManager GetFireTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetFireTowerMasteryManager();

        return FireTowerMasteryManager.GetOrCreate();
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetTowerMasteryManager();

        return TowerMasteryManager.GetOrCreate();
    }

    private string GetChaosUnlockStateText()
    {
        if (manager == null)
            return "Chaos-Unlocks vorbereitet";

        int totalUnlocks = manager.unlocks != null ? manager.unlocks.Count : 0;
        return "Unlocks: " + manager.GetUnlockedCount() + " / " + Mathf.Max(1, totalUnlocks);
    }

    private string GetChaosUnlockRuntimeText()
    {
        if (manager == null)
            return "ChaosUnlockManager wird spaeter angebunden.";

        return "Aktueller Stand:\n" + manager.GetUnlockSummaryText();
    }

    private string GetTopBarText()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;
        string accessMode = gameManager != null && gameManager.gameStarted && !gameManager.isGameOver
            ? "Read-only im laufenden Run"
            : "Voller Hub vorbereitet";

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        BasicTowerMasteryManager basicMastery = GetBasicTowerMasteryManager();
        RapidTowerMasteryManager rapidMastery = GetRapidTowerMasteryManager();
        HeavyTowerMasteryManager heavyMastery = GetHeavyTowerMasteryManager();
        FireTowerMasteryManager fireMastery = GetFireTowerMasteryManager();
        string basicText = basicMastery != null ? " | Basic XP " + basicMastery.masteryXP + " | Basic Punkte " + basicMastery.unspentPoints : "";
        string rapidText = "";
        string heavyText = "";
        string fireText = "";

        if (towerMastery != null && rapidMastery != null)
        {
            TowerMasteryRoleProfile rapidProfile = towerMastery.GetProfile(TowerRole.Rapid);
            rapidText = " | Rapid XP " + rapidProfile.masteryXP + " | Rapid Punkte " + rapidProfile.unspentPoints;
        }

        if (towerMastery != null && heavyMastery != null)
        {
            TowerMasteryRoleProfile heavyProfile = towerMastery.GetProfile(TowerRole.Heavy);
            heavyText = " | Heavy XP " + heavyProfile.masteryXP + " | Heavy Punkte " + heavyProfile.unspentPoints;
        }

        if (towerMastery != null && fireMastery != null)
        {
            TowerMasteryRoleProfile fireProfile = towerMastery.GetProfile(TowerRole.Fire);
            fireText = " | Fire XP " + fireProfile.masteryXP + " | Fire Punkte " + fireProfile.unspentPoints;
        }

        string towerText = towerMastery != null ? " | " + towerMastery.GetCompactSummaryText() : " | Tower Mastery vorbereitet";

        return accessMode + " | Kernwissen: vorbereitet" + towerText + basicText + rapidText + heavyText + fireText + " | Risskerne: vorbereitet | Bauplaene: vorbereitet | Elite-Siegel: 0";
    }

    private string GetSectionDisplayName(ChaosUnlockMenuSection section)
    {
        switch (section)
        {
            case ChaosUnlockMenuSection.Overview:
                return "Übersicht";
            case ChaosUnlockMenuSection.General:
                return "Allgemein";
            case ChaosUnlockMenuSection.TowerMastery:
                return "Tower Mastery";
            case ChaosUnlockMenuSection.ChaosResearch:
                return "Chaos-Forschung";
            case ChaosUnlockMenuSection.PathTechnique:
                return "Verbau / Pfadtechnik";
            case ChaosUnlockMenuSection.EliteHunt:
                return "Elite-Jagd";
            case ChaosUnlockMenuSection.Archive:
                return "Archiv";
            default:
                return section.ToString();
        }
    }

    private Color GetSectionAccentColor(ChaosUnlockMenuSection section)
    {
        switch (section)
        {
            case ChaosUnlockMenuSection.Overview:
                return headerColor;
            case ChaosUnlockMenuSection.General:
                return new Color32(92, 184, 130, 255);
            case ChaosUnlockMenuSection.TowerMastery:
                return unlockedButtonColor;
            case ChaosUnlockMenuSection.ChaosResearch:
                return new Color32(168, 116, 226, 255);
            case ChaosUnlockMenuSection.PathTechnique:
                return new Color32(92, 176, 210, 255);
            case ChaosUnlockMenuSection.EliteHunt:
                return new Color32(220, 84, 80, 255);
            case ChaosUnlockMenuSection.Archive:
                return new Color32(184, 194, 208, 255);
            default:
                return headerColor;
        }
    }

    private void EnsureUI()
    {
        if (!autoCreateUIIfMissing || rootPanel != null)
            return;

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (targetCanvas == null)
            return;

        CreateAutoUI();
    }

    private void CreateAutoUI()
    {
        GameObject overlay = new GameObject("MetaHubOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(targetCanvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;
        rootPanel = overlay;

        GameObject window = CreatePanel(overlay.transform, "MetaHubWindow", windowColor, new Vector2(1160f, 720f));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;

        CreateHeader(window.transform);
        CreateTopBar(window.transform);
        CreateNavigation(window.transform);
        CreateEntryList(window.transform);
        CreateDetailPanel(window.transform);
        CreateFooterHint(window.transform);
        CreatePrimaryActionButton(window.transform);
        CreateNotification(overlay.transform);
    }

    private void CreateHeader(Transform parent)
    {
        GameObject header = CreatePanel(parent, "HeaderBar", headerColor, Vector2.zero);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(0f, -82f);
        headerRect.offsetMax = Vector2.zero;

        titleText = CreateText(header.transform, "TitleText", titleFontSize, TextAlignmentOptions.MidlineLeft, Color.white);
        titleText.fontStyle = FontStyles.Bold;
        SetOffsets(titleText.rectTransform, 24f, 10f, 170f, 10f);

        closeButton = CreateSmallButton(header.transform, "CloseButton", "X", closeButtonColor, new Vector2(-18f, -18f));
        refreshButton = CreateSmallButton(header.transform, "RefreshButton", "R", buttonColor, new Vector2(-66f, -18f));
    }

    private void CreateTopBar(Transform parent)
    {
        GameObject summaryPanel = CreatePanel(parent, "MetaHubTopBar", detailPanelColor, Vector2.zero);
        RectTransform summaryRect = summaryPanel.GetComponent<RectTransform>();
        summaryRect.anchorMin = new Vector2(0f, 1f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.pivot = new Vector2(0.5f, 1f);
        summaryRect.offsetMin = new Vector2(24f, -128f);
        summaryRect.offsetMax = new Vector2(-24f, -92f);

        summaryText = CreateText(summaryPanel.transform, "SummaryText", summaryFontSize, TextAlignmentOptions.MidlineLeft, textColor);
        summaryText.enableWordWrapping = false;
        summaryText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(summaryText.rectTransform, 12f, 3f, 12f, 3f);
    }

    private void CreateNavigation(Transform parent)
    {
        GameObject navigationPanel = CreatePanel(parent, "MetaHubNavigationPanel", listPanelColor, Vector2.zero);
        RectTransform navigationRect = navigationPanel.GetComponent<RectTransform>();
        navigationRect.anchorMin = new Vector2(0f, 0f);
        navigationRect.anchorMax = new Vector2(0f, 1f);
        navigationRect.pivot = new Vector2(0f, 0.5f);
        navigationRect.offsetMin = new Vector2(24f, 72f);
        navigationRect.offsetMax = new Vector2(220f, -146f);

        sectionButtonContent = CreateContentWithLayout(navigationPanel.transform, "MetaHubNavigationContent");
    }

    private void CreateEntryList(Transform parent)
    {
        GameObject listPanel = CreatePanel(parent, "MetaHubEntryListPanel", listPanelColor, Vector2.zero);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0f, 1f);
        listRect.pivot = new Vector2(0f, 0.5f);
        listRect.offsetMin = new Vector2(238f, 72f);
        listRect.offsetMax = new Vector2(536f, -146f);

        entryListContent = CreateScrollableContentWithLayout(listPanel.transform, "MetaHubEntryListContent");
    }

    private void CreateDetailPanel(Transform parent)
    {
        GameObject detailPanel = CreatePanel(parent, "MetaHubDetailPanel", detailPanelColor, Vector2.zero);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(554f, 72f);
        detailRect.offsetMax = new Vector2(-24f, -146f);

        detailText = CreateText(detailPanel.transform, "DetailText", detailFontSize, TextAlignmentOptions.TopLeft, textColor);
        detailText.enableWordWrapping = true;
        detailText.overflowMode = TextOverflowModes.Overflow;
        SetOffsets(detailText.rectTransform, 18f, 16f, 18f, 16f);
    }

    private void CreateFooterHint(Transform parent)
    {
        TextMeshProUGUI footerText = CreateText(parent, "MetaHubFooterHint", 14f, TextAlignmentOptions.Center, new Color32(184, 194, 208, 255));
        footerText.text = "Zurueck | Ziele anpinnen vorbereitet | Respec vorbereitet | Keystone-Wechsel vorbereitet | Run-Start wirkt spaeter";
        footerText.enableWordWrapping = false;
        footerText.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform footerRect = footerText.rectTransform;
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.offsetMin = new Vector2(24f, 28f);
        footerRect.offsetMax = new Vector2(-270f, 60f);
    }

    private void CreatePrimaryActionButton(Transform parent)
    {
        primaryActionButton = CreateAnchoredButton(parent, "MetaHubPrimaryActionButton", "Kaufen", unlockedButtonColor, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 28f), new Vector2(220f, 42f));
        primaryActionButtonText = primaryActionButton.GetComponentInChildren<TextMeshProUGUI>(true);
        primaryActionButton.gameObject.SetActive(false);
    }

    private void CreateNotification(Transform parent)
    {
        notificationPanel = CreatePanel(parent, "UnlockNotificationPanel", new Color32(214, 164, 65, 245), new Vector2(460f, 64f));
        RectTransform notifyRect = notificationPanel.GetComponent<RectTransform>();
        notifyRect.anchorMin = new Vector2(0.5f, 1f);
        notifyRect.anchorMax = new Vector2(0.5f, 1f);
        notifyRect.pivot = new Vector2(0.5f, 1f);
        notifyRect.anchoredPosition = new Vector2(0f, -18f);
        notificationText = CreateText(notificationPanel.transform, "NotificationText", 18f, TextAlignmentOptions.Center, Color.white);
        Stretch(notificationText.rectTransform);
        notificationPanel.SetActive(false);
    }

    private Transform CreateScrollableContentWithLayout(Transform parent, string objectName)
    {
        ScrollRect scrollRect = parent.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(parent, false);
        Stretch(viewport.GetComponent<RectTransform>());

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 8);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        Transform content = CreateContentWithLayout(viewport.transform, objectName);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 760f);

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        return content;
    }

    private Transform CreateContentWithLayout(Transform parent, string objectName)
    {
        GameObject content = new GameObject(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(parent, false);
        Stretch(content.GetComponent<RectTransform>());

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content.transform;
    }

    private GameObject CreatePanel(Transform parent, string objectName, Color color, Vector2 size)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateSmallButton(Transform parent, string objectName, string label, Color color, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(42f, 42f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", 18f, TextAlignmentOptions.Center, Color.white);
        text.text = label;
        Stretch(text.rectTransform);
        return button;
    }

    private Button CreateAnchoredButton(Transform parent, string objectName, string label, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", 16f, TextAlignmentOptions.Center, Color.white);
        text.text = label;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(text.rectTransform);
        return button;
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

    private void SetOffsets(RectTransform rect, float left, float top, float right, float bottom)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void ApplySelectableColors(Button button, Color baseColor, Color accentColor)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, accentColor, 0.28f);
        colors.pressedColor = Color.Lerp(baseColor, accentColor, 0.45f);
        colors.selectedColor = baseColor;
        colors.disabledColor = lockedButtonColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }
}
