using System.Collections.Generic;
using UnityEngine;

public class MainMenuLexiconManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public MainMenuLexiconUI lexiconUI;
    public Canvas targetCanvas;
    public Transform rootParent;

    [Header("Setup")]
    public bool autoCreateUIIfMissing = true;
    public bool createDefaultChaptersOnStart = true;

    [Header("Chapters")]
    public List<MainMenuLexiconChapterDefinition> chapters = new List<MainMenuLexiconChapterDefinition>();

    [Header("Enemy Chapter")]
    public List<MainMenuLexiconEnemyEntry> enemyEntries = new List<MainMenuLexiconEnemyEntry>();
    public string selectedEnemyEntryId = "standard";
    public EnemyRole selectedEnemyRole = EnemyRole.Standard;

    [Header("Tower Chapter")]
    public List<MainMenuLexiconTowerEntry> towerEntries = new List<MainMenuLexiconTowerEntry>();
    public string selectedTowerEntryId = "tower_general";
    public TowerRole selectedTowerRole = TowerRole.Basic;

    [Header("Chaos & Justice Chapter")]
    public List<MainMenuLexiconChaosJusticeEntry> chaosJusticeEntries = new List<MainMenuLexiconChaosJusticeEntry>();
    public string selectedChaosJusticeEntryId = "justice_gold";

    [Header("Wegbau Chapter")]
    public List<MainMenuLexiconPathTileEntry> pathTileEntries = new List<MainMenuLexiconPathTileEntry>();
    public string selectedPathTileEntryId = "path_tile";

    [Header("Runtime")]
    public bool isOpen = false;
    public MainMenuLexiconChapterType selectedChapter = MainMenuLexiconChapterType.Enemies;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        EnsureChapterList();

        if (createDefaultChaptersOnStart)
        {
            CreateDefaultChaptersIfNeeded();
            CreateDefaultEnemyEntriesIfNeeded();
            CreateDefaultTowerEntriesIfNeeded();
            CreateDefaultChaosJusticeEntriesIfNeeded();
            CreateDefaultPathTileEntriesIfNeeded();
        }
    }

    private void Start()
    {
        EnsureInitialized();

        if (!isOpen)
            CloseLexicon();
    }

    public void EnsureInitialized()
    {
        EnsureChapterList();

        if (createDefaultChaptersOnStart)
        {
            CreateDefaultChaptersIfNeeded();
            CreateDefaultEnemyEntriesIfNeeded();
            CreateDefaultTowerEntriesIfNeeded();
            CreateDefaultChaosJusticeEntriesIfNeeded();
            CreateDefaultPathTileEntriesIfNeeded();
        }

        ResolveReferences();
        EnsureSelectedChapter();
        EnsureSelectedEnemy();
        EnsureSelectedTower();
        EnsureSelectedChaosJustice();
        EnsureSelectedPathTile();
    }

    public void OpenLexicon()
    {
        EnsureInitialized();
        isOpen = true;

        if (lexiconUI != null)
            lexiconUI.OpenLexicon();
    }

    public void CloseLexicon()
    {
        isOpen = false;

        if (lexiconUI != null)
            lexiconUI.CloseLexicon();
    }

    public void SelectChapter(MainMenuLexiconChapterType chapterType)
    {
        MainMenuLexiconChapterDefinition chapter = GetChapterByType(chapterType);

        if (chapter == null || !chapter.IsVisible())
            return;

        selectedChapter = chapter.chapterType;

        if (lexiconUI != null)
            lexiconUI.RefreshAll();
    }

    public MainMenuLexiconChapterDefinition GetSelectedChapter()
    {
        EnsureSelectedChapter();
        return GetChapterByType(selectedChapter);
    }

    public MainMenuLexiconChapterDefinition GetChapterByType(MainMenuLexiconChapterType chapterType)
    {
        EnsureChapterList();

        foreach (MainMenuLexiconChapterDefinition chapter in chapters)
        {
            if (chapter != null && chapter.chapterType == chapterType)
                return chapter;
        }

        return null;
    }

    public List<MainMenuLexiconChapterDefinition> GetVisibleChapters()
    {
        EnsureChapterList();

        if (createDefaultChaptersOnStart)
            CreateDefaultChaptersIfNeeded();

        List<MainMenuLexiconChapterDefinition> result = new List<MainMenuLexiconChapterDefinition>();

        foreach (MainMenuLexiconChapterDefinition chapter in chapters)
        {
            if (chapter != null && chapter.IsVisible())
                result.Add(chapter);
        }

        result.Sort(CompareChapters);
        return result;
    }

    public string GetSelectedChapterTitle()
    {
        MainMenuLexiconChapterDefinition chapter = GetSelectedChapter();
        return chapter != null ? chapter.title : "Kein Kapitel";
    }

    public void SelectEnemy(EnemyRole enemyRole)
    {
        MainMenuLexiconEnemyEntry entry = GetEnemyEntryByRole(enemyRole);

        if (entry != null)
            SelectEnemyEntry(entry.entryId);
    }

    public void SelectEnemyEntry(string entryId)
    {
        MainMenuLexiconEnemyEntry entry = GetEnemyEntryById(entryId);

        if (entry == null || !entry.IsValid())
            return;

        selectedEnemyEntryId = entry.entryId;
        selectedEnemyRole = entry.enemyRole;

        if (lexiconUI != null)
            lexiconUI.RefreshAll();
    }

    public MainMenuLexiconEnemyEntry GetSelectedEnemyEntry()
    {
        EnsureSelectedEnemy();
        return GetEnemyEntryById(selectedEnemyEntryId);
    }

    public MainMenuLexiconEnemyEntry GetEnemyEntryById(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
            return null;

        EnsureEnemyEntryList();

        foreach (MainMenuLexiconEnemyEntry entry in enemyEntries)
        {
            if (entry != null && entry.entryId == entryId)
                return entry;
        }

        return null;
    }

    public MainMenuLexiconEnemyEntry GetEnemyEntryByRole(EnemyRole enemyRole)
    {
        EnsureEnemyEntryList();

        foreach (MainMenuLexiconEnemyEntry entry in enemyEntries)
        {
            if (entry != null && entry.enemyRole == enemyRole)
                return entry;
        }

        return null;
    }

    public List<MainMenuLexiconEnemyEntry> GetVisibleEnemyEntries()
    {
        EnsureEnemyEntryList();

        if (createDefaultChaptersOnStart)
            CreateDefaultEnemyEntriesIfNeeded();

        List<MainMenuLexiconEnemyEntry> result = new List<MainMenuLexiconEnemyEntry>();

        foreach (MainMenuLexiconEnemyEntry entry in enemyEntries)
        {
            if (entry != null && entry.IsValid())
                result.Add(entry);
        }

        result.Sort(CompareEnemyEntries);
        return result;
    }

    public string GetEnemyDetailText(MainMenuLexiconEnemyEntry entry)
    {
        if (entry == null)
            return "Kein Gegner ausgewählt.";

        string text = "<b>" + entry.title + "</b>";

        if (!string.IsNullOrEmpty(entry.description))
            text += "\n<size=90%><color=#B8C2D0>" + entry.description + "</color></size>";

        text += "\n\n<b>Basiswerte</b>";
        text += "\nHP=" + FormatStat(entry.hp);
        text += "\nSpeed=" + entry.speed.ToString("0.00");
        text += "\nArmor=" + entry.armor;
        text += "\nSchaden=" + entry.baseDamage;
        text += "\nEffektresistenz=" + (string.IsNullOrEmpty(entry.effectResistance) ? "Keine" : entry.effectResistance);

        text += "\n\n<b>Rewards</b>";
        text += "\nGold=" + entry.goldReward;
        text += "\nXP=" + entry.xpReward;

        if (entry.globalXPReward > 0)
            text += "\nGlobal-XP=" + entry.globalXPReward;

        text += "\n\n<b>Starke Tower</b>";
        text += "\n" + GetStrongTowerText(entry);
        return text;
    }

    public void SelectTowerEntry(string entryId)
    {
        MainMenuLexiconTowerEntry entry = GetTowerEntryById(entryId);

        if (entry == null || !entry.IsValid())
            return;

        selectedTowerEntryId = entry.entryId;
        selectedTowerRole = entry.towerRole;

        if (lexiconUI != null)
            lexiconUI.RefreshAll();
    }

    public MainMenuLexiconTowerEntry GetSelectedTowerEntry()
    {
        EnsureSelectedTower();
        return GetTowerEntryById(selectedTowerEntryId);
    }

    public MainMenuLexiconTowerEntry GetTowerEntryById(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
            return null;

        EnsureTowerEntryList();

        foreach (MainMenuLexiconTowerEntry entry in towerEntries)
        {
            if (entry != null && entry.entryId == entryId)
                return entry;
        }

        return null;
    }

    public List<MainMenuLexiconTowerEntry> GetVisibleTowerEntries()
    {
        EnsureTowerEntryList();

        if (createDefaultChaptersOnStart)
            CreateDefaultTowerEntriesIfNeeded();

        List<MainMenuLexiconTowerEntry> result = new List<MainMenuLexiconTowerEntry>();

        foreach (MainMenuLexiconTowerEntry entry in towerEntries)
        {
            if (entry != null && entry.IsValid())
                result.Add(entry);
        }

        result.Sort(CompareTowerEntries);
        return result;
    }

    public string GetTowerDetailText(MainMenuLexiconTowerEntry entry)
    {
        if (entry == null)
            return "Kein Tower ausgewählt.";

        string text = "<b>" + entry.title + "</b>";

        if (!string.IsNullOrEmpty(entry.description))
            text += "\n<size=90%><color=#B8C2D0>" + entry.description + "</color></size>";

        if (entry.isGeneralInfo)
        {
            if (!string.IsNullOrEmpty(entry.generalInfoText))
                text += "\n\n" + entry.generalInfoText;

            return text;
        }

        text += "\n\n<b>Basiswerte</b>";
        text += "\nKosten=" + entry.cost;
        text += "\nSchaden=" + entry.damage;
        text += "\nReichweite=" + entry.range.ToString("0.00");
        text += "\nFeuerrate=" + entry.fireRate.ToString("0.00") + "/s";
        text += "\nEffekt=" + (string.IsNullOrEmpty(entry.effectText) ? "Keine" : entry.effectText);

        text += "\n\n<b>Stark gegen</b>";
        text += "\n" + GetStrongAgainstText(entry);
        return text;
    }

    public void SelectChaosJusticeEntry(string entryId)
    {
        MainMenuLexiconChaosJusticeEntry entry = GetChaosJusticeEntryById(entryId);

        if (entry == null || !entry.IsValid())
            return;

        selectedChaosJusticeEntryId = entry.entryId;

        if (lexiconUI != null)
            lexiconUI.RefreshAll();
    }

    public MainMenuLexiconChaosJusticeEntry GetSelectedChaosJusticeEntry()
    {
        EnsureSelectedChaosJustice();
        return GetChaosJusticeEntryById(selectedChaosJusticeEntryId);
    }

    public MainMenuLexiconChaosJusticeEntry GetChaosJusticeEntryById(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
            return null;

        EnsureChaosJusticeEntryList();

        foreach (MainMenuLexiconChaosJusticeEntry entry in chaosJusticeEntries)
        {
            if (entry != null && entry.entryId == entryId)
                return entry;
        }

        return null;
    }

    public List<MainMenuLexiconChaosJusticeEntry> GetVisibleChaosJusticeEntries()
    {
        EnsureChaosJusticeEntryList();

        if (createDefaultChaptersOnStart)
            CreateDefaultChaosJusticeEntriesIfNeeded();

        List<MainMenuLexiconChaosJusticeEntry> result = new List<MainMenuLexiconChaosJusticeEntry>();

        foreach (MainMenuLexiconChaosJusticeEntry entry in chaosJusticeEntries)
        {
            if (entry != null && entry.IsValid())
                result.Add(entry);
        }

        result.Sort(CompareChaosJusticeEntries);
        return result;
    }

    public string GetChaosJusticeDetailText(MainMenuLexiconChaosJusticeEntry entry)
    {
        if (entry == null)
            return "Kein Eintrag ausgewählt.";

        string text = "<b>" + entry.title + "</b>";

        if (!string.IsNullOrEmpty(entry.description))
            text += "\n<size=90%><color=#B8C2D0>" + entry.description + "</color></size>";

        text += "\n\n<b>Art</b>";
        text += "\n" + GetChaosJusticeTypeLabel(entry.entryType);

        text += "\n\n<b>Risiko</b>";
        text += "\n" + (string.IsNullOrEmpty(entry.riskText) ? "Kein Zusatzrisiko." : entry.riskText);

        text += "\n\n<b>Reward</b>";
        text += "\n" + (string.IsNullOrEmpty(entry.rewardText) ? "Kein Zusatzreward." : entry.rewardText);
        return text;
    }

    public void SelectPathTileEntry(string entryId)
    {
        MainMenuLexiconPathTileEntry entry = GetPathTileEntryById(entryId);

        if (entry == null || !entry.IsValid())
            return;

        selectedPathTileEntryId = entry.entryId;

        if (lexiconUI != null)
            lexiconUI.RefreshAll();
    }

    public MainMenuLexiconPathTileEntry GetSelectedPathTileEntry()
    {
        EnsureSelectedPathTile();
        return GetPathTileEntryById(selectedPathTileEntryId);
    }

    public MainMenuLexiconPathTileEntry GetPathTileEntryById(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
            return null;

        EnsurePathTileEntryList();

        foreach (MainMenuLexiconPathTileEntry entry in pathTileEntries)
        {
            if (entry != null && entry.entryId == entryId)
                return entry;
        }

        return null;
    }

    public List<MainMenuLexiconPathTileEntry> GetVisiblePathTileEntries()
    {
        EnsurePathTileEntryList();

        if (createDefaultChaptersOnStart)
            CreateDefaultPathTileEntriesIfNeeded();

        List<MainMenuLexiconPathTileEntry> result = new List<MainMenuLexiconPathTileEntry>();

        foreach (MainMenuLexiconPathTileEntry entry in pathTileEntries)
        {
            if (entry != null && entry.IsValid())
                result.Add(entry);
        }

        result.Sort(ComparePathTileEntries);
        return result;
    }

    public string GetPathTileDetailText(MainMenuLexiconPathTileEntry entry)
    {
        if (entry == null)
            return "Kein Tile ausgewählt.";

        string text = "<b>" + entry.title + "</b>";

        if (!string.IsNullOrEmpty(entry.description))
            text += "\n<size=90%><color=#B8C2D0>" + entry.description + "</color></size>";

        text += "\n\n<b>Typ</b>";
        text += "\n" + (string.IsNullOrEmpty(entry.category) ? "Tile" : entry.category);

        text += "\n\n<b>Funktion</b>";
        text += "\n" + (string.IsNullOrEmpty(entry.functionText) ? "Keine Sonderfunktion." : entry.functionText);
        return text;
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (targetCanvas == null && gameManager != null)
            targetCanvas = gameManager.startMenuCanvas;

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (rootParent == null && gameManager != null && gameManager.startMenuRoot != null)
            rootParent = gameManager.startMenuRoot.transform;

        if (lexiconUI == null)
            lexiconUI = FindObjectOfType<MainMenuLexiconUI>();

        if (lexiconUI == null && autoCreateUIIfMissing)
            lexiconUI = gameObject.AddComponent<MainMenuLexiconUI>();

        if (lexiconUI != null)
        {
            lexiconUI.manager = this;
            lexiconUI.targetCanvas = targetCanvas;
            lexiconUI.rootParent = rootParent;
            lexiconUI.Connect(this);
        }
    }

    private void EnsureSelectedChapter()
    {
        MainMenuLexiconChapterDefinition selected = GetChapterByType(selectedChapter);

        if (selected != null && selected.IsVisible())
            return;

        List<MainMenuLexiconChapterDefinition> visibleChapters = GetVisibleChapters();

        if (visibleChapters.Count <= 0)
            return;

        selectedChapter = visibleChapters[0].chapterType;
    }

    private void EnsureSelectedEnemy()
    {
        MainMenuLexiconEnemyEntry selected = GetEnemyEntryById(selectedEnemyEntryId);

        if (selected != null && selected.IsValid())
        {
            selectedEnemyRole = selected.enemyRole;
            return;
        }

        List<MainMenuLexiconEnemyEntry> visibleEnemies = GetVisibleEnemyEntries();

        if (visibleEnemies.Count <= 0)
            return;

        selectedEnemyEntryId = visibleEnemies[0].entryId;
        selectedEnemyRole = visibleEnemies[0].enemyRole;
    }

    private void EnsureSelectedTower()
    {
        MainMenuLexiconTowerEntry selected = GetTowerEntryById(selectedTowerEntryId);

        if (selected != null && selected.IsValid())
        {
            selectedTowerRole = selected.towerRole;
            return;
        }

        List<MainMenuLexiconTowerEntry> visibleTowers = GetVisibleTowerEntries();

        if (visibleTowers.Count <= 0)
            return;

        selectedTowerEntryId = visibleTowers[0].entryId;
        selectedTowerRole = visibleTowers[0].towerRole;
    }

    private void EnsureSelectedChaosJustice()
    {
        MainMenuLexiconChaosJusticeEntry selected = GetChaosJusticeEntryById(selectedChaosJusticeEntryId);

        if (selected != null && selected.IsValid())
            return;

        List<MainMenuLexiconChaosJusticeEntry> visibleEntries = GetVisibleChaosJusticeEntries();

        if (visibleEntries.Count <= 0)
            return;

        selectedChaosJusticeEntryId = visibleEntries[0].entryId;
    }

    private void EnsureSelectedPathTile()
    {
        MainMenuLexiconPathTileEntry selected = GetPathTileEntryById(selectedPathTileEntryId);

        if (selected != null && selected.IsValid())
            return;

        List<MainMenuLexiconPathTileEntry> visibleEntries = GetVisiblePathTileEntries();

        if (visibleEntries.Count <= 0)
            return;

        selectedPathTileEntryId = visibleEntries[0].entryId;
    }

    private void CreateDefaultChaptersIfNeeded()
    {
        EnsureChapterList();
        AddChapterIfMissing("enemies", MainMenuLexiconChapterType.Enemies, "Gegner", 0);
        AddChapterIfMissing("towers", MainMenuLexiconChapterType.Towers, "Tower", 10);
        AddChapterIfMissing("chaos_justice", MainMenuLexiconChapterType.ChaosJustice, "Chaos & Gerechtigkeit", 20);
        AddChapterIfMissing("blocked_building", MainMenuLexiconChapterType.BlockedBuilding, "Wegbau", 30);
        AddChapterIfMissing("meta_progression", MainMenuLexiconChapterType.MetaProgression, "Meta-Progression", 40);
    }

    private void CreateDefaultEnemyEntriesIfNeeded()
    {
        EnsureEnemyEntryList();

        AddEnemyEntryIfMissing("standard", EnemyRole.Standard, EnemyVariantType.Normal, "Standard", 0,
            "Gerader Basisgegner ohne Sonderregel. Prüft, ob deine Grundabdeckung stabil ist.",
            10f, 2f, 0, 1, 4, 2, 0, "Keine", "Basic Tower", "Fire Tower");

        AddEnemyEntryIfMissing("runner", EnemyRole.Runner, EnemyVariantType.Normal, "Runner", 10,
            "Sehr schneller, leichter Gegner. Bestraft Lücken und lange Nachladezeiten.",
            7f, 3.20f, 0, 1, 4, 3, 0, "Slow-Resistenz 15%", "Rapid Tower", "Slow Tower", "Lightning Tower");

        AddEnemyEntryIfMissing("tank", EnemyRole.Tank, EnemyVariantType.Normal, "Tank", 20,
            "Langsamer Lebenspool. Hält lange im Feuer und ist anfällig für Damage-over-Time.",
            30f, 1.00f, 0, 1, 9, 9, 0, "Keine, Effektschaden x1.20", "Poison Tower", "Heavy Tower");

        AddEnemyEntryIfMissing("knight", EnemyRole.Knight, EnemyVariantType.Normal, "Knight", 30,
            "Gepanzerter Frontkämpfer. Kleine Treffer werden durch Armor stark gedrückt.",
            18f, 1.75f, 2, 1, 8, 7, 0, "Keine", "Heavy Tower", "Sniper Tower");

        AddEnemyEntryIfMissing("mage", EnemyRole.Mage, EnemyVariantType.Normal, "Mage", 40,
            "Fragiler Gegner, der bei Treffern nach vorn teleportieren kann.",
            8f, 1.80f, 0, 1, 6, 6, 0, "Keine", "Sniper Tower", "Heavy Tower");

        AddEnemyEntryIfMissing("learner", EnemyRole.Learner, EnemyVariantType.Normal, "Learner", 50,
            "Ignoriert Effekte komplett. Burn, Poison, Slow, Bleed und Darkness greifen nicht.",
            12f, 1.80f, 0, 1, 7, 8, 0, "Effektimmun", "Basic Tower", "Rapid Tower", "Heavy Tower");

        AddEnemyEntryIfMissing("allrounder", EnemyRole.AllRounder, EnemyVariantType.Normal, "AllRounder", 60,
            "Solider Mischgegner mit Armor, höherem Base-Schaden und wachsender Effektresistenz bei Anwendungen.",
            28f, 1.95f, 1, 2, 12, 11, 0, "Slow-Resistenz 20%, baut Effektresistenz auf", "Heavy Tower", "Sniper Tower");

        AddEnemyEntryIfMissing("miniboss", EnemyRole.MiniBoss, EnemyVariantType.Normal, "MiniBoss", 70,
            "Einzelner Zwischenboss mit hohem Leben und höherem Base-Schaden.",
            70f, 0.90f, 0, 2, 25, 20, 3, "Keine", "Poison Tower", "Heavy Tower", "Sniper Tower");

        AddEnemyEntryIfMissing("boss", EnemyRole.Boss, EnemyVariantType.Normal, "Boss", 80,
            "Massiver Boss mit sehr hohem Leben, Armor und hohem Base-Schaden.",
            260f, 0.90f, 3, 5, 90, 50, 8, "Keine", "Poison Tower", "Heavy Tower", "Sniper Tower");

        AddEnemyEntryIfMissing("elite", EnemyRole.Elite, EnemyVariantType.Normal, "Elite", 90,
            "Seltener Hochdruckgegner. Zäh, schnell genug und mit wachsender Effektresistenz.",
            140f, 2.00f, 0, 5, 45, 34, 4, "Slow-Resistenz 15%, Effektschaden x0.95, baut Effektresistenz auf", "Sniper Tower", "Heavy Tower");

        AddEnemyEntryIfMissing("chaos_standard", EnemyRole.Standard, EnemyVariantType.Chaos, "Chaos Standard", 100,
            "Chaos-Variante des Basisgegners. Weniger anfällig für Effektschaden und etwas widerstandsfähiger gegen Slow.",
            10f, 2f, 0, 1, 4, 2, 0, "Slow-Resistenz 12%, Effektschaden x0.90", "Basic Tower", "Rapid Tower");

        AddEnemyEntryIfMissing("chaos_runner", EnemyRole.Runner, EnemyVariantType.Chaos, "Chaos Runner", 110,
            "Schneller Runner mit mehr Leben. Druckt frühe Verteidigungen stärker über Tempo.",
            9.45f, 3.20f, 0, 1, 4, 3, 0, "Slow-Resistenz 15%", "Rapid Tower", "Slow Tower", "Lightning Tower");

        AddEnemyEntryIfMissing("chaos_tank", EnemyRole.Tank, EnemyVariantType.Chaos, "Chaos Tank", 120,
            "Tank-Variante mit Regeneration. Erholt sich, solange er nicht konsequent unter Feuer bleibt.",
            30f, 1.00f, 0, 1, 9, 9, 0, "Keine, Effektschaden x1.20", "Poison Tower", "Heavy Tower");

        AddEnemyEntryIfMissing("chaos_knight", EnemyRole.Knight, EnemyVariantType.Chaos, "Chaos Knight", 130,
            "Gepanzerter Knight mit höherem Tempo. Er verbindet Armor-Druck mit weniger Reaktionszeit.",
            18f, 1.96f, 2, 1, 8, 7, 0, "Keine", "Heavy Tower", "Sniper Tower");

        AddEnemyEntryIfMissing("chaos_mage", EnemyRole.Mage, EnemyVariantType.Chaos, "Chaos Mage", 140,
            "Mage-Variante mit stärkerem Teleport. Springt weiter nach vorn und kann das öfter tun.",
            8f, 1.80f, 0, 1, 6, 6, 0, "Keine", "Sniper Tower", "Heavy Tower");

        AddEnemyEntryIfMissing("chaos_learner", EnemyRole.Learner, EnemyVariantType.Chaos, "Chaos Learner", 150,
            "Lerner-Variante ohne volle Effektimmunität, aber mit starker DoT-Abschwächung und Heilung durch DoT-Druck.",
            12f, 1.80f, 0, 1, 7, 8, 0, "Slow-Resistenz 35%, DoT-Schaden x0.65, heilt durch DoT", "Basic Tower", "Rapid Tower", "Heavy Tower");

        AddEnemyEntryIfMissing("chaos_allrounder", EnemyRole.AllRounder, EnemyVariantType.Chaos, "Chaos AllRounder", 160,
            "AllRounder-Variante, die im Kampf zusätzliche Armor aufbauen kann.",
            28f, 1.95f, 1, 2, 12, 11, 0, "Slow-Resistenz 20%, baut Effektresistenz und Armor auf", "Heavy Tower", "Sniper Tower");

        AddEnemyEntryIfMissing("chaos_miniboss", EnemyRole.MiniBoss, EnemyVariantType.Chaos, "Chaos MiniBoss", 170,
            "Vorbereitete Chaos-Variante des MiniBoss. In V1 wird sie nicht regulär als Chaos-Variante angeboten.",
            70f, 0.90f, 0, 2, 25, 20, 3, "Keine", "Poison Tower", "Heavy Tower", "Sniper Tower");

        AddEnemyEntryIfMissing("chaos_boss", EnemyRole.Boss, EnemyVariantType.Chaos, "Chaos Boss", 180,
            "Vorbereitete Chaos-Variante des Boss. In V1 wird sie nicht regulär als Chaos-Variante angeboten.",
            260f, 0.90f, 3, 5, 90, 50, 8, "Keine", "Poison Tower", "Heavy Tower", "Sniper Tower");
    }

    private void CreateDefaultTowerEntriesIfNeeded()
    {
        EnsureTowerEntryList();

        AddTowerGeneralInfoIfMissing();

        AddTowerEntryIfMissing("tower_basic", TowerRole.Basic, "Basic Tower", 10,
            "Günstiger Allrounder für den Spielstart. Stabil gegen einfache Gegner, aber ohne Spezialeffekt.",
            50, 5, 3.0f, 1.45f, "Keine", "Standard", "Chaos Standard");

        AddTowerEntryIfMissing("tower_rapid", TowerRole.Rapid, "Rapid Tower", 20,
            "Sehr schnelle Schüsse mit kleinem Einzelschaden. Gut zum Abfangen schneller und angeschlagener Ziele.",
            75, 2, 2.3f, 2.80f, "Keine", "Runner", "Chaos Runner", "Learner");

        AddTowerEntryIfMissing("tower_heavy", TowerRole.Heavy, "Heavy Tower", 30,
            "Langsamer Tower mit hohem Einzelschaden. Gut gegen Armor und hohe Lebenspools.",
            100, 16, 2.8f, 0.42f, "Keine", "Tank", "Knight", "Boss", "Elite");

        AddTowerEntryIfMissing("tower_fire", TowerRole.Fire, "Fire Tower", 40,
            "Burn-Tower gegen Gruppen und normale Gegner. Trägt Schaden über Zeit nach.",
            80, 2, 2.5f, 1.15f, "Burn: 2 Schaden/s für 4.00s", "Standard", "Gruppen");

        AddTowerEntryIfMissing("tower_slow", TowerRole.Slow, "Slow Tower", 50,
            "Kontrolltower. Reduziert gegnerisches Tempo und gibt anderen Towern mehr Zeit.",
            65, 1, 2.6f, 0.95f, "Slow: Ziel läuft auf 55% Speed für 2.20s", "Runner", "Chaos Runner");

        AddTowerEntryIfMissing("tower_poison", TowerRole.Poison, "Poison Tower", 60,
            "Starker DoT-Tower gegen zähe Gegner. Poison ignoriert Armor über den Tick-Schaden.",
            80, 1, 2.7f, 0.80f, "Poison: 4 Schaden/s für 6.50s", "Tank", "MiniBoss", "Boss");

        AddTowerEntryIfMissing("tower_sniper", TowerRole.Sniper, "Sniper Tower", 70,
            "Sehr hohe Reichweite und starker Einzelschuss. Priorisiert gefährliche Elite-Ziele.",
            120, 42, 5.2f, 0.30f, "Keine", "Mage", "Knight", "Boss", "Elite");

        AddTowerEntryIfMissing("tower_alchemist", TowerRole.Alchemist, "Alchemist Tower", 80,
            "Hybrid aus Gift und Kontrolle. Schwächer als spezialisierte Tower, aber flexibel.",
            85, 1, 2.6f, 0.75f, "Poison: 1 Schaden/s für 4.50s | Slow: Ziel läuft auf 78% Speed für 1.30s", "Tank", "Runner");

        AddTowerEntryIfMissing("tower_lightning", TowerRole.Lightning, "Lightning Tower", 90,
            "Kettenblitz mit kurzer Verlangsamung. Stark, wenn Gegner nah beieinander laufen.",
            90, 7, 3.0f, 0.85f, "Kettenblitz + Slow: Ziel läuft auf 75% Speed für 1.25s", "Runner", "Gruppen");

        AddTowerEntryIfMissing("tower_mortar", TowerRole.Mortar, "Mortar Tower", 100,
            "Langsamer Einschlag mit Flächenschaden. Bestraft dichte Gegnergruppen.",
            110, 10, 4.0f, 0.30f, "AOE-Einschlag, Radius 0.85", "Gruppen", "Chaos-Waves");

        AddTowerEntryIfMissing("tower_spike", TowerRole.Spike, "Spike Tower", 110,
            "Kurze Reichweite mit schnellen Treffern und Bleed-Fallen.",
            75, 2, 1.5f, 1.10f, "Bleed-Falle: 3 Schaden/Tick alle 2.50s für 14.00s", "Runner", "Ziele auf engem Weg");
    }

    private void CreateDefaultChaosJusticeEntriesIfNeeded()
    {
        EnsureChaosJusticeEntryList();

        AddChaosJusticeEntryIfMissing("justice_gold", MainMenuLexiconChaosJusticeEntryType.Justice, "Gold-Gerechtigkeit", 0,
            "Sichere Wahl nach Boss-Waves.",
            "Kein Zusatzrisiko.",
            "Gold-Rewards +3% pro Stufe.");

        AddChaosJusticeEntryIfMissing("justice_xp", MainMenuLexiconChaosJusticeEntryType.Justice, "XP-Gerechtigkeit", 10,
            "Sichere Wahl nach Boss-Waves.",
            "Kein Zusatzrisiko.",
            "XP-Rewards +3% pro Stufe.");

        AddChaosJusticeEntryIfMissing("safe_no_modifier", MainMenuLexiconChaosJusticeEntryType.SafeOption, "Kein Modifikator", 20,
            "Sichere Chaos-Option.",
            "Kein Zusatzrisiko.",
            "Kein Zusatzreward.");

        AddChaosJusticeEntryIfMissing("risk_extra_enemies", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Mehr Gegner", 100,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: Gegneranzahl x1.12 und +1 Zusatzgegner.",
            "Gold +5% | XP +4%.");

        AddChaosJusticeEntryIfMissing("risk_runner_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Runner-Druck", 110,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +2 Runner.",
            "Gold +4%.");

        AddChaosJusticeEntryIfMissing("risk_tank_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Tank-Druck", 120,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +1 Tank.",
            "Gold +5% | XP +4%.");

        AddChaosJusticeEntryIfMissing("risk_knight_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Knight-Druck", 130,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +1 Knight.",
            "Gold +5% | XP +3%.");

        AddChaosJusticeEntryIfMissing("risk_mage_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Mage-Druck", 140,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +1 Mage.",
            "Kein direkter Reward.");

        AddChaosJusticeEntryIfMissing("risk_learner_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Learner-Druck", 150,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +1 Learner.",
            "Kein direkter Reward.");

        AddChaosJusticeEntryIfMissing("risk_faster_spawns", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Schnellere Spawns", 160,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: SpawnDelay x0.90.",
            "Gold +6%.");

        AddChaosJusticeEntryIfMissing("risk_mixed_role_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Gemischter Rollendruck", 170,
            "Erscheint erst bei höherem Chaos.",
            "Zukünftige Waves: +2 Runner, optional +1 Mage und +1 Learner.",
            "Kein direkter Reward.");

        AddChaosJusticeEntryIfMissing("risk_allrounder_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "AllRounder-Druck", 180,
            "Erscheint erst bei höherem Chaos.",
            "Zukünftige Waves: +1 AllRounder.",
            "Kein direkter Reward.");

        AddChaosJusticeEntryIfMissing("risk_pre_boss_pressure", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Vor-Boss-Druck", 190,
            "Erscheint erst bei höherem Chaos.",
            "Vor-Boss-/Boss-Waves: +Knight, optional +Mage.",
            "Kein direkter Reward.");

        AddChaosJusticeEntryIfMissing("risk_miniboss_light", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "MiniBoss-Druck light", 200,
            "Erscheint erst bei höherem Chaos.",
            "MiniBoss-Waves: +1 MiniBoss. Keine Tower-Zerstörung.",
            "Kein direkter Reward.");

        AddChaosJusticeEntryIfMissing("risk_chaos_variants", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Violette Verformung", 210,
            "Erscheint erst bei höherem Chaos.",
            "Chaos-Varianten häufiger: Chance +12%, bis zu +1 zusätzliche Variante.",
            "XP +6%.");

        AddChaosJusticeEntryIfMissing("risk_wave_density", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Stärkere Verdichtung", 220,
            "Erscheint erst bei höherem Chaos.",
            "Chaos-Waves: Verdichtung häufiger, Stärke +1, Chance +8%.",
            "Gold +5%.");

        AddChaosJusticeEntryIfMissing("risk_wave_toughness", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Zähe Wellen", 230,
            "Erscheint erst bei höherem Chaos.",
            "Chaos-Waves: Zähigkeit häufiger, Stärke +1, Chance +7%.",
            "XP +5%.");

        AddChaosJusticeEntryIfMissing("risk_wave_rearguard", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Stärkere Nachhut", 240,
            "Erscheint erst bei höherem Chaos.",
            "Chaos-Waves: Nachhut häufiger, Stärke +1, Chance +7%.",
            "Gold +5% | XP +4%.");

        AddChaosJusticeEntryIfMissing("risk_wave_chaos_variant_group", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Violette Wellen", 250,
            "Erscheint erst bei höherem Chaos.",
            "Chaos-Waves: violette Gruppen häufiger, Stärke +1, Chance +6%.",
            "XP +6%.");

        AddChaosJusticeEntryIfMissing("risk_wave_armor", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Chaos-Panzerung", 260,
            "Erscheint erst bei höherem Chaos.",
            "Chaos-Waves: Armor-Gruppen häufiger, Stärke +1, Chance +5%.",
            "Gold +7%.");

        AddChaosJusticeEntryIfMissing("risk_greedy_swarm", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Gieriger Ansturm", 270,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: Gegneranzahl x1.10 und +1 Zusatzgegner.",
            "Gold +20% | XP +12%.");

        AddChaosJusticeEntryIfMissing("risk_gold_rush", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Goldrausch-Risiko", 280,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: Gegneranzahl x1.07 und +1 Zusatzgegner.",
            "Gold +22%.");

        AddChaosJusticeEntryIfMissing("risk_xp_trial", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "XP-Prüfung", 290,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +1 Learner und +1 Mage.",
            "XP +22%.");

        AddChaosJusticeEntryIfMissing("risk_haste_for_gold", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Eile gegen Gold", 300,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: SpawnDelay x0.92.",
            "Gold +17%.");

        AddChaosJusticeEntryIfMissing("risk_bounty_hunters", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "Kopfgeld-Jagd", 310,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +1 Runner, optional +1 Tank.",
            "Gold +16% | XP +13%.");

        AddChaosJusticeEntryIfMissing("risk_allrounder_bounty", MainMenuLexiconChaosJusticeEntryType.RiskModifier, "AllRounder-Kopfgeld", 320,
            "Startwert bei erster Stufe.",
            "Zukünftige Waves: +1 AllRounder.",
            "Gold +18% | XP +15%.");
    }

    private void CreateDefaultPathTileEntriesIfNeeded()
    {
        EnsurePathTileEntryList();

        AddPathTileEntryIfMissing("path_tile", PathBuildOptionType.PathTile, "Path Tile", 0,
            "Normale Weg-Erweiterung.",
            "Weg-Tile",
            "Verlängert den Weg, die Base zieht weiter und danach startet die nächste Wave.");

        AddPathTileEntryIfMissing("trap_tile", PathBuildOptionType.TrapTile, "Trap Tile", 10,
            "Weg-Tile mit Bleed.",
            "Weg-Tile",
            "Gegner auf dem Tile bluten: 2 Schaden alle 3s für 20s.");

        AddPathTileEntryIfMissing("slow_tile", PathBuildOptionType.SlowTile, "Slow Tile", 20,
            "Weg-Tile mit Slow.",
            "Weg-Tile",
            "Gegner auf dem Tile laufen kurz auf 45% Speed für 2s.");

        AddPathTileEntryIfMissing("knock_tile", PathBuildOptionType.KnockTile, "Knock Tile", 30,
            "Weg-Tile mit Rückstoß.",
            "Weg-Tile",
            "Wirft normale Gegner 3 Weg-Tiles zurück. Boss, MiniBoss und Elite sind immun.");

        AddPathTileEntryIfMissing("range_tile", PathBuildOptionType.RangeTile, "Range Tile", 40,
            "Bebaubares Support-Tile neben dem Weg.",
            "Support-Tile",
            "Baue einen Tower direkt auf dieses Tile. Nur dieser Tower erhaelt +1 Reichweite.");

        AddPathTileEntryIfMissing("damage_tile", PathBuildOptionType.DamageTile, "Damage Tile", 50,
            "Bebaubares Support-Tile neben dem Weg.",
            "Support-Tile",
            "Baue einen Tower direkt auf dieses Tile. Nur dieser Tower verursacht +20% Schaden.");

        AddPathTileEntryIfMissing("rate_tile", PathBuildOptionType.RateTile, "Rate Tile", 60,
            "Bebaubares Support-Tile neben dem Weg.",
            "Support-Tile",
            "Baue einen Tower direkt auf dieses Tile. Nur dieser Tower feuert +20% schneller.");

        AddPathTileEntryIfMissing("xp_tile", PathBuildOptionType.XPTile, "XP Tile", 70,
            "Bebaubares Support-Tile neben dem Weg.",
            "Support-Tile",
            "Baue einen Tower direkt auf dieses Tile. Nur dieser Tower erhaelt +25% XP.");

        AddPathTileEntryIfMissing("upgrade_tile", PathBuildOptionType.UpgradeTile, "Upgrade Tile", 80,
            "Bebaubares Support-Tile neben dem Weg.",
            "Support-Tile",
            "Baue einen Tower direkt auf dieses Tile. Nur dessen Point-Upgrades sind +1 staerker.");

        AddPathTileEntryIfMissing("combo_tile", PathBuildOptionType.ComboTile, "Combo Tile", 90,
            "Weg-Tile für Effekt-Kombos.",
            "Weg-Tile",
            "Löst Darkness aus, wenn ein Gegner gleichzeitig Burn, Poison und Bleed hat.");
    }

    private void AddTowerGeneralInfoIfMissing()
    {
        if (GetTowerEntryById("tower_general") != null)
            return;

        MainMenuLexiconTowerEntry entry = new MainMenuLexiconTowerEntry();
        entry.entryId = "tower_general";
        entry.title = "Tower Allgemein";
        entry.sortOrder = 0;
        entry.isGeneralInfo = true;
        entry.description = "Tower werden durch Kills, Assists und XP stärker und erhalten planbare Fortschrittspunkte.";
        entry.generalInfoText =
            "<b>Level-Regeln</b>\n" +
            "Alle 5 Tower-Level: +1 Upgradepoint.\n" +
            "Alle 10 Tower-Level: Entwicklung + Metapoint.\n\n" +
            "<b>Upgradepoints</b>\n" +
            "Upgradepoints werden direkt am Tower genutzt und verstärken Schaden, Reichweite, Feuerrate oder den Spezialeffekt.\n\n" +
            "<b>Entwicklung</b>\n" +
            "Entwicklung ist der größere Fortschrittsschritt eines Towers. Sie ist für sichtbare und spätere langfristige Progression vorbereitet.\n\n" +
            "<b>Metapoint</b>\n" +
            "Metapoints sind bereits vorbereitet, auch wenn das vollständige Meta-Progression-System noch nicht im Spiel ist.";
        towerEntries.Add(entry);
    }

    private void AddChapterIfMissing(string id, MainMenuLexiconChapterType chapterType, string title, int sortOrder)
    {
        if (GetChapterByType(chapterType) != null)
            return;

        MainMenuLexiconChapterDefinition chapter = new MainMenuLexiconChapterDefinition();
        chapter.chapterId = id;
        chapter.chapterType = chapterType;
        chapter.title = title;
        chapter.sortOrder = sortOrder;
        chapter.visible = true;
        chapter.preparedForFutureContent = true;
        chapters.Add(chapter);
    }

    private void AddEnemyEntryIfMissing(string entryId, EnemyRole role, EnemyVariantType variantType, string title, int sortOrder, string description, float hp, float speed, int armor, int baseDamage, int goldReward, int xpReward, int globalXPReward, string effectResistance, params string[] strongTowerNames)
    {
        if (GetEnemyEntryById(entryId) != null)
            return;

        MainMenuLexiconEnemyEntry entry = new MainMenuLexiconEnemyEntry();
        entry.entryId = entryId;
        entry.enemyRole = role;
        entry.variantType = variantType;
        entry.title = title;
        entry.sortOrder = sortOrder;
        entry.description = description;
        entry.hp = hp;
        entry.speed = speed;
        entry.armor = armor;
        entry.baseDamage = baseDamage;
        entry.goldReward = goldReward;
        entry.xpReward = xpReward;
        entry.globalXPReward = globalXPReward;
        entry.effectResistance = effectResistance;
        entry.strongTowerNames = new List<string>();

        if (strongTowerNames != null)
            entry.strongTowerNames.AddRange(strongTowerNames);

        enemyEntries.Add(entry);
    }

    private void AddTowerEntryIfMissing(string entryId, TowerRole towerRole, string title, int sortOrder, string description, int cost, int damage, float range, float fireRate, string effectText, params string[] strongAgainst)
    {
        if (GetTowerEntryById(entryId) != null)
            return;

        MainMenuLexiconTowerEntry entry = new MainMenuLexiconTowerEntry();
        entry.entryId = entryId;
        entry.towerRole = towerRole;
        entry.title = title;
        entry.sortOrder = sortOrder;
        entry.description = description;
        entry.cost = cost;
        entry.damage = damage;
        entry.range = range;
        entry.fireRate = fireRate;
        entry.effectText = effectText;
        entry.strongAgainst = new List<string>();

        if (strongAgainst != null)
            entry.strongAgainst.AddRange(strongAgainst);

        towerEntries.Add(entry);
    }

    private void AddChaosJusticeEntryIfMissing(string entryId, MainMenuLexiconChaosJusticeEntryType entryType, string title, int sortOrder, string description, string riskText, string rewardText)
    {
        if (GetChaosJusticeEntryById(entryId) != null)
            return;

        MainMenuLexiconChaosJusticeEntry entry = new MainMenuLexiconChaosJusticeEntry();
        entry.entryId = entryId;
        entry.entryType = entryType;
        entry.title = title;
        entry.sortOrder = sortOrder;
        entry.description = description;
        entry.riskText = riskText;
        entry.rewardText = rewardText;
        chaosJusticeEntries.Add(entry);
    }

    private void AddPathTileEntryIfMissing(string entryId, PathBuildOptionType optionType, string title, int sortOrder, string description, string category, string functionText)
    {
        if (GetPathTileEntryById(entryId) != null)
            return;

        MainMenuLexiconPathTileEntry entry = new MainMenuLexiconPathTileEntry();
        entry.entryId = entryId;
        entry.optionType = optionType;
        entry.title = title;
        entry.sortOrder = sortOrder;
        entry.description = description;
        entry.category = category;
        entry.functionText = functionText;
        pathTileEntries.Add(entry);
    }

    private int CompareChapters(MainMenuLexiconChapterDefinition a, MainMenuLexiconChapterDefinition b)
    {
        if (a == null && b == null)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private int CompareEnemyEntries(MainMenuLexiconEnemyEntry a, MainMenuLexiconEnemyEntry b)
    {
        if (a == null && b == null)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private int CompareTowerEntries(MainMenuLexiconTowerEntry a, MainMenuLexiconTowerEntry b)
    {
        if (a == null && b == null)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private int CompareChaosJusticeEntries(MainMenuLexiconChaosJusticeEntry a, MainMenuLexiconChaosJusticeEntry b)
    {
        if (a == null && b == null)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private int ComparePathTileEntries(MainMenuLexiconPathTileEntry a, MainMenuLexiconPathTileEntry b)
    {
        if (a == null && b == null)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
    }

    private string GetChaosJusticeTypeLabel(MainMenuLexiconChaosJusticeEntryType entryType)
    {
        switch (entryType)
        {
            case MainMenuLexiconChaosJusticeEntryType.Justice:
                return "Gerechtigkeit";
            case MainMenuLexiconChaosJusticeEntryType.SafeOption:
                return "Sichere Option";
            case MainMenuLexiconChaosJusticeEntryType.RiskModifier:
                return "Risiko-Modifikator";
            default:
                return "Eintrag";
        }
    }

    private string GetStrongTowerText(MainMenuLexiconEnemyEntry entry)
    {
        if (entry == null || entry.strongTowerNames == null || entry.strongTowerNames.Count == 0)
            return "Keine klare Schwäche.";

        return string.Join(", ", entry.strongTowerNames);
    }

    private string GetStrongAgainstText(MainMenuLexiconTowerEntry entry)
    {
        if (entry == null || entry.strongAgainst == null || entry.strongAgainst.Count == 0)
            return "Keine klare Spezialisierung.";

        return string.Join(", ", entry.strongAgainst);
    }

    private string FormatStat(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
            return Mathf.RoundToInt(value).ToString();

        return value.ToString("0.##");
    }

    private void EnsureChapterList()
    {
        if (chapters == null)
            chapters = new List<MainMenuLexiconChapterDefinition>();
    }

    private void EnsureEnemyEntryList()
    {
        if (enemyEntries == null)
            enemyEntries = new List<MainMenuLexiconEnemyEntry>();
    }

    private void EnsureTowerEntryList()
    {
        if (towerEntries == null)
            towerEntries = new List<MainMenuLexiconTowerEntry>();
    }

    private void EnsureChaosJusticeEntryList()
    {
        if (chaosJusticeEntries == null)
            chaosJusticeEntries = new List<MainMenuLexiconChaosJusticeEntry>();
    }

    private void EnsurePathTileEntryList()
    {
        if (pathTileEntries == null)
            pathTileEntries = new List<MainMenuLexiconPathTileEntry>();
    }
}
