using System.Collections.Generic;
using System.Text;
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
    public Transform resourceChipContent;
    public Transform mainContent;
    public Transform rightColumnContent;
    public Transform activeKeystoneContent;
    public TextMeshProUGUI bottomHintText;
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
    public bool useIndustrialMetaHubTheme = true;
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
    private ScrollRect detailScrollRect;
    private RectTransform accountXPFillRect;
    private TextMeshProUGUI accountXPBarText;
    private ChaosUnlockMenuSection selectedSection = ChaosUnlockMenuSection.Overview;
    private string selectedMetaHubEntryId = "";
    private bool showingSectionTopics = false;
    private float notificationTimer = 0f;
    private readonly Color mutedGray = new Color32(185, 194, 208, 255);
    private const string RootSectionEntryPrefix = "meta_section_";
    private const string BackToRootEntryId = "meta_back_to_sections";

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

        showingSectionTopics = false;
        selectedSection = ChaosUnlockMenuSection.Overview;
        selectedMetaHubEntryId = GetRootSectionEntryId(ChaosUnlockMenuSection.Overview);
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
        RefreshResourceBar();
        RefreshSidebar();
        RefreshDashboardContent();
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

    private void RefreshResourceBar()
    {
        if (resourceChipContent == null)
            return;

        ClearChildren(resourceChipContent);

        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();

        CreateResourceChip(resourceChipContent, "KW", "Kernwissen", FormatNumber(generalMeta != null ? generalMeta.kernwissen : 0), headerColor);
        CreateResourceChip(resourceChipContent, "XP", "Account-XP", FormatNumber(generalMeta != null ? generalMeta.accountXP : 0), new Color32(58, 175, 234, 255));
        CreateResourceChip(resourceChipContent, "CH", "Chaos", FormatNumber(chaosResearch != null ? chaosResearch.chaosKnowledge : 0), GetSectionAccentColor(ChaosUnlockMenuSection.ChaosResearch));
        CreateResourceChip(resourceChipContent, "RK", "Risskerne", FormatNumber(chaosResearch != null ? chaosResearch.riftCores : 0), new Color32(225, 70, 78, 255));
        CreateResourceChip(resourceChipContent, "BP", "Bauplaene", FormatNumber(pathTechnique != null ? pathTechnique.blueprints : 0), GetSectionAccentColor(ChaosUnlockMenuSection.PathTechnique));
        CreateResourceChip(resourceChipContent, "RB", "Rissbau", FormatNumber(pathTechnique != null ? pathTechnique.riftBlueprints : 0), new Color32(92, 176, 210, 255));
        CreateResourceChip(resourceChipContent, "ES", "Elite", FormatNumber(eliteHunt != null ? eliteHunt.eliteSeals : 0), GetSectionAccentColor(ChaosUnlockMenuSection.EliteHunt));

        if (titleText != null)
        {
            string sectionName = GetSectionDisplayName(selectedSection).ToUpperInvariant();
            titleText.text = "TOWER DEFENSE\n<size=45%><color=#D6A441>META-HUB / " + sectionName + "</color></size>";
        }

        if (summaryText != null)
        {
            int level = generalMeta != null ? generalMeta.accountLevel : 1;
            int xpIntoLevel = generalMeta != null ? generalMeta.GetAccountXPIntoCurrentLevel() : 0;
            int xpToNext = generalMeta != null ? generalMeta.GetXPToNextAccountLevel() : 100;
            summaryText.text = "Account Lv. " + level + "\n<size=70%><color=#B9C2D0>" + xpIntoLevel + " / " + xpToNext + " XP</color></size>";

            if (accountXPFillRect != null)
                accountXPFillRect.anchorMax = new Vector2(xpToNext > 0 ? Mathf.Clamp01(xpIntoLevel / (float)xpToNext) : 0f, 1f);

            if (accountXPBarText != null)
                accountXPBarText.text = xpIntoLevel + " / " + xpToNext + " XP";
        }
    }

    private void RefreshSidebar()
    {
        if (sectionButtonContent == null)
            return;

        ClearChildren(sectionButtonContent);
        generatedSectionButtons.Clear();

        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.Overview));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.General));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.TowerMastery));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.ChaosResearch));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.PathTechnique));
        generatedSectionButtons.Add(CreateSectionButton(ChaosUnlockMenuSection.EliteHunt));

        RefreshKeystonePreview();
    }

    private void RefreshKeystonePreview()
    {
        if (activeKeystoneContent == null)
            return;

        ClearChildren(activeKeystoneContent);
        CreateKeystoneSlot(activeKeystoneContent, "B", "Basic", GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery));
        CreateKeystoneSlot(activeKeystoneContent, "R", "Rapid", new Color32(165, 82, 255, 255));
        CreateKeystoneSlot(activeKeystoneContent, "F", "Fire", new Color32(225, 70, 78, 255));
    }

    private void RefreshDashboardContent()
    {
        if (mainContent == null || rightColumnContent == null)
            return;

        ClearChildren(mainContent);
        ClearChildren(rightColumnContent);
        generatedEntryButtons.Clear();

        if (primaryActionButton != null)
            primaryActionButton.gameObject.SetActive(false);

        if (bottomHintText != null)
            bottomHintText.text = "Tipp: Aktiviere Keystones im Tower Mastery und baue dein System strategisch auf.";

        switch (selectedSection)
        {
            case ChaosUnlockMenuSection.Overview:
                RenderOverviewDashboard();
                break;
            case ChaosUnlockMenuSection.TowerMastery:
                RenderTowerMasteryDashboard();
                break;
            default:
                RenderCardGridDashboard(selectedSection);
                break;
        }
    }

    private void RenderOverviewDashboard()
    {
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        AddSectionHeading(mainContent, "UEBERSICHT", "Meta-Fortschritt, aktive Systeme und naechste Ziele", headerColor);

        GameObject cardsRow = CreateAnchoredPanel(mainContent, "OverviewMetricCards", new Color32(0, 0, 0, 0), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -188f), new Vector2(0f, -54f));
        HorizontalLayoutGroup cardLayout = cardsRow.AddComponent<HorizontalLayoutGroup>();
        cardLayout.padding = new RectOffset(0, 0, 0, 0);
        cardLayout.spacing = 12f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = true;

        int towerCount = TowerMasteryManager.GetOrderedTowerRoles().Length;
        int unlockedTowerCount = CountUnlockedTowerRoles(generalMeta);
        int highestChaos = chaosResearch != null ? chaosResearch.highestChaosLevelEver : 0;
        int pathLevel = pathTechnique != null ? pathTechnique.pathTechniqueLevel : 1;
        int eliteRank = eliteHunt != null ? eliteHunt.eliteRank : 1;

        CreateMetricCard(cardsRow.transform, "TOWER MASTERY", FormatNumber(unlockedTowerCount), "Tower freigeschaltet", unlockedTowerCount + " / " + Mathf.Max(1, towerCount), towerCount > 0 ? unlockedTowerCount / (float)towerCount : 0f, GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery), "T");
        CreateMetricCard(cardsRow.transform, "CHAOS-WISSEN", FormatNumber(chaosResearch != null ? chaosResearch.chaosKnowledge : 0), "Forschungswaehrung", "Chaos " + highestChaos + " / 5", highestChaos / 5f, GetSectionAccentColor(ChaosUnlockMenuSection.ChaosResearch), "C");
        CreateMetricCard(cardsRow.transform, "RISIKOKERNE", FormatNumber(chaosResearch != null ? chaosResearch.riftCores : 0), "Endgame-Kerne", "Chaos " + highestChaos + " / 5", highestChaos / 5f, new Color32(225, 70, 78, 255), "R");
        CreateMetricCard(cardsRow.transform, "BAUPLAENE", FormatNumber(pathTechnique != null ? pathTechnique.blueprints : 0), "Pfadtechnik", "Lv " + pathLevel + " / 40", pathLevel / 40f, GetSectionAccentColor(ChaosUnlockMenuSection.PathTechnique), "B");
        CreateMetricCard(cardsRow.transform, "ELITE-JAGD", FormatNumber(eliteHunt != null ? eliteHunt.totalEliteKillsEver : 0), "Elite besiegt", "Rang " + eliteRank + " / 35", eliteRank / 35f, GetSectionAccentColor(ChaosUnlockMenuSection.EliteHunt), "E");

        GameObject progressPanel = CreateDashboardPanel(mainContent, "ProgressPanel", "FORTSCHRITT", headerColor, new Vector2(0f, 0.32f), new Vector2(0.50f, 0.72f), new Vector2(0f, 0f), new Vector2(-8f, -10f));
        RenderProgressPanel(progressPanel.transform, generalMeta, chaosResearch, towerMastery);

        GameObject systemPanel = CreateDashboardPanel(mainContent, "SystemStatusPanel", "SYSTEMSTATUS", headerColor, new Vector2(0.51f, 0.32f), new Vector2(1f, 0.72f), new Vector2(8f, 0f), new Vector2(0f, -10f));
        RenderSystemStatusPanel(systemPanel.transform, generalMeta, chaosResearch, pathTechnique, eliteHunt, towerMastery);

        GameObject buffsPanel = CreateDashboardPanel(mainContent, "BuffsPanel", "AKTIVE BUFFS", new Color32(74, 219, 123, 255), new Vector2(0f, 0f), new Vector2(0.49f, 0.30f), new Vector2(0f, 0f), new Vector2(-8f, 0f));
        CreateStatusRow(buffsPanel.transform, "Startgold", "Aktiver Meta-Loadout-Bonus", "+" + FormatNumber(generalMeta != null ? generalMeta.GetActiveStartGoldBonus() : 0), new Color32(74, 219, 123, 255));
        CreateStatusRow(buffsPanel.transform, "Startleben", "Aktiver Meta-Loadout-Bonus", "+" + FormatNumber(generalMeta != null ? generalMeta.GetActiveStartLifeBonus() : 0), new Color32(74, 219, 123, 255));

        GameObject risksPanel = CreateDashboardPanel(mainContent, "ResearchPanel", "FORSCHUNGSSTAND", GetSectionAccentColor(ChaosUnlockMenuSection.ChaosResearch), new Vector2(0.50f, 0f), new Vector2(1f, 0.30f), new Vector2(8f, 0f), Vector2.zero);
        CreateStatusRow(risksPanel.transform, "Chaos-Wissen", "Gesammelte Forschungswaehrung", FormatNumber(chaosResearch != null ? chaosResearch.chaosKnowledge : 0), GetSectionAccentColor(ChaosUnlockMenuSection.ChaosResearch));
        CreateStatusRow(risksPanel.transform, "Risskerne / Elite-Siegel", "Endgame-Ressourcen", FormatNumber(chaosResearch != null ? chaosResearch.riftCores : 0) + " / " + FormatNumber(eliteHunt != null ? eliteHunt.eliteSeals : 0), new Color32(225, 70, 78, 255));

        RenderOverviewRightColumn(generalMeta, chaosResearch, pathTechnique, eliteHunt, towerMastery);
    }

    private void RenderProgressPanel(Transform parent, GeneralMetaProgressionManager generalMeta, ChaosResearchProgressionManager chaosResearch, TowerMasteryManager towerMastery)
    {
        int accountLevel = generalMeta != null ? generalMeta.accountLevel : 1;
        int xpInto = generalMeta != null ? generalMeta.GetAccountXPIntoCurrentLevel() : 0;
        int xpNext = generalMeta != null ? generalMeta.GetXPToNextAccountLevel() : 100;

        GameObject levelBox = CreateAnchoredPanel(parent, "AccountLevelBox", new Color32(8, 14, 18, 210), new Vector2(0f, 0f), new Vector2(0.52f, 0.82f), new Vector2(16f, 18f), new Vector2(-12f, -48f));
        AddEdgeFrame(levelBox.transform, new Color32(58, 84, 90, 255), 2f);
        TextMeshProUGUI levelText = CreateText(levelBox.transform, "LevelText", 42f, TextAlignmentOptions.Center, textColor);
        levelText.text = accountLevel + "\n<size=35%><color=#D6A441>ACCOUNT LEVEL</color></size>";
        levelText.fontStyle = FontStyles.Bold;
        Stretch(levelText.rectTransform);

        CreateProgressBar(parent, new Vector2(0f, 0f), new Vector2(0.52f, 0f), new Vector2(28f, 18f), new Vector2(-28f, 34f), xpNext > 0 ? xpInto / (float)xpNext : 0f, GetSectionAccentColor(ChaosUnlockMenuSection.PathTechnique), xpInto + " / " + xpNext + " XP");

        int freePoints = GetTotalUnspentTowerPoints(towerMastery);
        CreateMiniStat(parent, "Freie Punkte", freePoints.ToString(), GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery), new Vector2(0.55f, 0.58f), new Vector2(1f, 0.80f));
        CreateMiniStat(parent, "Chaos-Level", chaosResearch != null ? chaosResearch.highestChaosLevelEver.ToString() : "0", GetSectionAccentColor(ChaosUnlockMenuSection.ChaosResearch), new Vector2(0.55f, 0.38f), new Vector2(1f, 0.56f));
        CreateMiniStat(parent, "Gold-Gerechtigkeit", chaosResearch != null ? chaosResearch.highestGoldJusticeEver.ToString() : "0", headerColor, new Vector2(0.55f, 0.18f), new Vector2(1f, 0.36f));
        CreateMiniStat(parent, "XP-Gerechtigkeit", chaosResearch != null ? chaosResearch.highestXpJusticeEver.ToString() : "0", GetSectionAccentColor(ChaosUnlockMenuSection.PathTechnique), new Vector2(0.55f, 0.00f), new Vector2(1f, 0.16f));
    }

    private void RenderBalancePanel(Transform parent, ChaosResearchProgressionManager chaosResearch)
    {
        TextMeshProUGUI label = CreateText(parent, "BalanceLabels", 15f, TextAlignmentOptions.Center, textColor);
        label.text = "<color=#D6A441>GERECHTIGKEIT</color>        BALANCE        <color=#E14B4B>CHAOS</color>";
        SetOffsets(label.rectTransform, 26f, 62f, 26f, 156f);

        CreateProgressBar(parent, new Vector2(0.13f, 0.47f), new Vector2(0.87f, 0.58f), Vector2.zero, Vector2.zero, 0.52f, headerColor, "");
        GameObject redEnd = CreateAnchoredPanel(parent, "ChaosBarEnd", new Color32(120, 35, 35, 230), new Vector2(0.58f, 0.49f), new Vector2(0.86f, 0.56f), Vector2.zero, Vector2.zero);
        redEnd.transform.SetAsLastSibling();

        int justice = chaosResearch != null ? Mathf.Max(chaosResearch.highestGoldJusticeEver, chaosResearch.highestXpJusticeEver) : 0;
        int chaos = chaosResearch != null ? chaosResearch.highestChaosLevelEver : 0;
        CreateScoreBox(parent, "SAFETY SCORE", (6 + justice).ToString(), headerColor, new Vector2(0.04f, 0.15f), new Vector2(0.30f, 0.36f));
        CreateScoreBox(parent, "STABILITAET", "100%", headerColor, new Vector2(0.36f, 0.13f), new Vector2(0.64f, 0.38f));
        CreateScoreBox(parent, "CHAOS SCORE", chaos.ToString(), GetSectionAccentColor(ChaosUnlockMenuSection.EliteHunt), new Vector2(0.70f, 0.15f), new Vector2(0.96f, 0.36f));
        CreateAnchoredButton(parent, "BalanceDetailsButton", "DETAILS ANZEIGEN", buttonColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(210f, 34f));
    }

    private void RenderSystemStatusPanel(Transform parent, GeneralMetaProgressionManager generalMeta, ChaosResearchProgressionManager chaosResearch, PathTechniqueProgressionManager pathTechnique, EliteHuntProgressionManager eliteHunt, TowerMasteryManager towerMastery)
    {
        int usedLoadoutSlots = generalMeta != null ? generalMeta.GetUsedLoadoutSlots() : 0;
        int loadoutSlots = generalMeta != null ? generalMeta.GetLoadoutSlotCapacity() : 0;
        int activeKeystones = GetActiveTowerKeystoneCount(towerMastery);

        CreateStatusRow(parent, "Meta-Loadout", "Aktive allgemeine Power-Slots", usedLoadoutSlots + " / " + loadoutSlots, headerColor);
        CreateStatusRow(parent, "Tower-Keystones", "Bleiben fuer den naechsten Run aktiv", activeKeystones.ToString(), GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery));
        CreateStatusRow(parent, "Pfadtechnik", "Level und Bauplaene", (pathTechnique != null ? "Lv " + pathTechnique.pathTechniqueLevel + " | " + FormatNumber(pathTechnique.blueprints) + " BP" : "Lv 1 | 0 BP"), GetSectionAccentColor(ChaosUnlockMenuSection.PathTechnique));
        CreateStatusRow(parent, "Elite-Jagd", "Opt-in Status", (eliteHunt != null ? EliteHuntProgressionManager.GetHuntModeDisplayName(eliteHunt.activeHuntMode) : "Aus"), GetSectionAccentColor(ChaosUnlockMenuSection.EliteHunt));
    }

    private void RenderOverviewRightColumn(GeneralMetaProgressionManager generalMeta, ChaosResearchProgressionManager chaosResearch, PathTechniqueProgressionManager pathTechnique, EliteHuntProgressionManager eliteHunt, TowerMasteryManager towerMastery)
    {
        GameObject goalsPanel = CreateDashboardPanel(rightColumnContent, "GoalsPanel", "NAECHSTE ZIELE", headerColor, new Vector2(0f, 0.40f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        int accountLevel = generalMeta != null ? generalMeta.accountLevel : 1;
        int nextAccountLevel = accountLevel + 1;
        int xpInto = generalMeta != null ? generalMeta.GetAccountXPIntoCurrentLevel() : 0;
        int xpNext = generalMeta != null ? generalMeta.GetXPToNextAccountLevel() : 100;
        int highestChaos = chaosResearch != null ? chaosResearch.highestChaosLevelEver : 0;
        int nextChaos = Mathf.Clamp(highestChaos + 1, 1, 5);
        int pathLevel = pathTechnique != null ? pathTechnique.pathTechniqueLevel : 1;
        int nextPathLevel = pathLevel + 1;

        CreateGoalRow(goalsPanel.transform, "Account Level " + nextAccountLevel, xpInto + " / " + xpNext, headerColor);
        CreateGoalRow(goalsPanel.transform, "Tower-Punkte ausgeben", FormatNumber(GetTotalUnspentTowerPoints(towerMastery)) + " frei", GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery));
        CreateGoalRow(goalsPanel.transform, "Chaos-Level " + nextChaos + " erreichen", highestChaos + " / 5", GetSectionAccentColor(ChaosUnlockMenuSection.ChaosResearch));
        CreateGoalRow(goalsPanel.transform, "Pfadtechnik Level " + nextPathLevel, (pathTechnique != null ? pathTechnique.GetXPIntoCurrentLevel() + " / " + pathTechnique.GetXPToNextPathTechniqueLevel() : "0 / 100"), GetSectionAccentColor(ChaosUnlockMenuSection.PathTechnique));
        CreateGoalRow(goalsPanel.transform, "Elite-Siegel sammeln", FormatNumber(eliteHunt != null ? eliteHunt.eliteSeals : 0), GetSectionAccentColor(ChaosUnlockMenuSection.EliteHunt));
        CreateAnchoredButton(goalsPanel.transform, "AllGoalsButton", "ALLE ZIELE ANZEIGEN", buttonColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(240f, 34f));

        GameObject lastRunPanel = CreateDashboardPanel(rightColumnContent, "LastRunPanel", "LETZTER RUN", new Color32(184, 194, 208, 255), new Vector2(0f, 0f), new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero);
        CreateInfoLine(lastRunPanel.transform, "Kernwissen", "+" + FormatNumber(generalMeta != null ? generalMeta.lastRunKernwissenGained : 0), 0.66f);
        CreateInfoLine(lastRunPanel.transform, "Account-XP", "+" + FormatNumber(generalMeta != null ? generalMeta.lastRunAccountXPGained : 0), 0.53f);
        CreateInfoLine(lastRunPanel.transform, "Chaos-Wissen", "+" + FormatNumber(chaosResearch != null ? chaosResearch.lastRunChaosKnowledgeGained : 0), 0.40f);
        CreateInfoLine(lastRunPanel.transform, "Bauplaene / Elite", "+" + FormatNumber(pathTechnique != null ? pathTechnique.lastRunBlueprintsGained : 0) + " / +" + FormatNumber(eliteHunt != null ? eliteHunt.lastRunEliteSealsGained : 0), 0.27f);
        CreateAnchoredButton(lastRunPanel.transform, "RunStatsButton", "RUN-STATISTIKEN", buttonColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(230f, 34f));
    }

    private void RenderCardGridDashboard(ChaosUnlockMenuSection section)
    {
        List<MetaHubEntry> entries = GetEntriesForSection(section);
        EnsureSelectedMetaHubEntry(entries);

        AddSectionHeading(mainContent, GetSectionDisplayName(section).ToUpperInvariant(), GetSectionSubtitle(section), GetSectionAccentColor(section));

        GameObject scrollPanel = CreateAnchoredPanel(mainContent, "CardGridScrollPanel", new Color32(0, 0, 0, 0), new Vector2(0f, 0f), new Vector2(1f, 0.88f), Vector2.zero, Vector2.zero);
        Transform gridContent = CreateScrollableGridContent(scrollPanel.transform, "CardGridContent", new Vector2(255f, 132f), 3);

        foreach (MetaHubEntry entry in entries)
            generatedEntryButtons.Add(CreateGridEntryCard(gridContent, entry, GetSectionAccentColor(section)));

        RenderSelectedEntryDetail(entries, section);
    }

    private void RenderTowerMasteryDashboard()
    {
        List<MetaHubEntry> entries = BuildTowerMasteryEntries();
        EnsureTowerSelection(entries);
        TowerRole selectedRole = GetSelectedTowerRole();
        Color accent = GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery);

        AddSectionHeading(mainContent, "TOWER MASTERY", "Tower-Fortschritt, Milestones, Nodes und Keystones", accent);

        GameObject towerListPanel = CreateDashboardPanel(mainContent, "TowerListPanel", "TOWER", accent, new Vector2(0f, 0f), new Vector2(0.32f, 0.88f), Vector2.zero, new Vector2(-8f, 0f));
        GameObject towerScrollPanel = CreateAnchoredPanel(towerListPanel.transform, "TowerListScroll", new Color32(0, 0, 0, 0), Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -50f));
        Transform towerList = CreateScrollableContentWithLayout(towerScrollPanel.transform, "TowerListContent");
        VerticalLayoutGroup towerListLayout = towerList.GetComponent<VerticalLayoutGroup>();
        if (towerListLayout != null)
        {
            towerListLayout.padding = new RectOffset(12, 12, 12, 12);
            towerListLayout.spacing = 12f;
            towerListLayout.childControlHeight = true;
            towerListLayout.childForceExpandHeight = false;
        }

        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            MetaHubEntry towerEntry = FindEntry(entries, GetTowerOverviewEntryId(role));
            generatedEntryButtons.Add(CreateTowerRoleCard(towerList, role, towerEntry, role == selectedRole));
        }

        GameObject centerPanel = CreateDashboardPanel(mainContent, "TowerCenterPanel", TowerMasteryManager.GetTowerDisplayName(selectedRole).ToUpperInvariant(), accent, new Vector2(0.33f, 0f), new Vector2(1f, 0.88f), new Vector2(8f, 0f), Vector2.zero);
        RenderTowerCenterPanel(centerPanel.transform, entries, selectedRole);
        RenderSelectedEntryDetail(entries, ChaosUnlockMenuSection.TowerMastery);
    }

    private List<MetaHubEntry> GetEntriesForSection(ChaosUnlockMenuSection section)
    {
        switch (section)
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

    private MetaHubEntry FindEntry(List<MetaHubEntry> entries, string entryId)
    {
        if (entries == null || string.IsNullOrEmpty(entryId))
            return new MetaHubEntry("", "Vorbereitet", "Leer", "");

        foreach (MetaHubEntry entry in entries)
        {
            if (entry.entryId == entryId)
                return entry;
        }

        return entries.Count > 0 ? entries[0] : new MetaHubEntry("", "Vorbereitet", "Leer", "");
    }

    private void RenderSelectedEntryDetail(List<MetaHubEntry> entries, ChaosUnlockMenuSection section)
    {
        MetaHubEntry entry = GetSelectedEntryFromList(entries);
        Color accent = GetSectionAccentColor(section);
        GameObject panel = CreateDashboardPanel(rightColumnContent, "DetailPanel", "DETAILS", accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI title = CreateText(panel.transform, "DetailTitle", 19f, TextAlignmentOptions.TopLeft, textColor);
        title.text = entry.title;
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(title.rectTransform, 18f, 48f, 18f, 360f);

        TextMeshProUGUI state = CreateText(panel.transform, "DetailState", 13f, TextAlignmentOptions.TopLeft, entry.locked ? new Color32(184, 194, 208, 255) : accent);
        state.text = entry.locked ? "Gesperrt" : entry.stateText;
        state.enableWordWrapping = false;
        state.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(state.rectTransform, 18f, 82f, 18f, 330f);

        TextMeshProUGUI body = CreateText(panel.transform, "DetailBody", 15f, TextAlignmentOptions.TopLeft, textColor);
        body.text = GetDetailText(entry);
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(body.rectTransform, 18f, 116f, 18f, 18f);

        RefreshPrimaryActionButton(entry);
    }

    private MetaHubEntry GetSelectedEntryFromList(List<MetaHubEntry> entries)
    {
        EnsureSelectedMetaHubEntry(entries);
        return FindEntry(entries, selectedMetaHubEntryId);
    }

    private Button CreateGridEntryCard(Transform parent, MetaHubEntry entry, Color accent)
    {
        bool selected = entry.entryId == selectedMetaHubEntryId;
        GameObject card = CreatePanel(parent, "GridEntry_" + entry.entryId, selected ? new Color32(42, 28, 18, 248) : new Color32(16, 25, 35, 245), Vector2.zero);
        card.AddComponent<LayoutElement>().preferredHeight = 132f;
        AddEdgeFrame(card.transform, selected ? accent : new Color32(58, 64, 72, 255), selected ? 3f : 1f);
        AddVerticalAccent(card.transform, entry.locked ? lockedButtonColor : accent, 6f);

        Button button = card.AddComponent<Button>();
        string id = entry.entryId;
        button.onClick.AddListener(() => SelectMetaHubEntry(id));
        ApplySelectableColors(button, card.GetComponent<Image>().color, accent);

        TextMeshProUGUI title = CreateText(card.transform, "Title", 16f, TextAlignmentOptions.TopLeft, Color.white);
        title.text = entry.title;
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(title.rectTransform, 18f, 14f, 14f, 82f);

        TextMeshProUGUI state = CreateText(card.transform, "State", 12f, TextAlignmentOptions.TopLeft, entry.locked ? mutedGray : accent);
        state.text = entry.stateText;
        state.enableWordWrapping = true;
        state.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(state.rectTransform, 18f, 48f, 14f, 18f);

        return button;
    }

    private void EnsureTowerSelection(List<MetaHubEntry> entries)
    {
        if (string.IsNullOrEmpty(selectedMetaHubEntryId) || selectedMetaHubEntryId == BackToRootEntryId || selectedMetaHubEntryId.StartsWith(RootSectionEntryPrefix))
            selectedMetaHubEntryId = "tower_basic_overview";

        foreach (MetaHubEntry entry in entries)
        {
            if (entry.entryId == selectedMetaHubEntryId)
                return;
        }

        selectedMetaHubEntryId = "tower_basic_overview";
    }

    private TowerRole GetSelectedTowerRole()
    {
        string id = selectedMetaHubEntryId;

        if (id.Contains("rapid"))
            return TowerRole.Rapid;
        if (id.Contains("heavy"))
            return TowerRole.Heavy;
        if (id.Contains("fire"))
            return TowerRole.Fire;
        if (id.Contains("slow"))
            return TowerRole.Slow;
        if (id.Contains("poison"))
            return TowerRole.Poison;
        if (id.Contains("sniper"))
            return TowerRole.Sniper;
        if (id.Contains("alchemist"))
            return TowerRole.Alchemist;
        if (id.Contains("lightning"))
            return TowerRole.Lightning;
        if (id.Contains("mortar"))
            return TowerRole.Mortar;
        if (id.Contains("spike"))
            return TowerRole.Spike;

        return TowerRole.Basic;
    }

    private string GetTowerOverviewEntryId(TowerRole role)
    {
        return "tower_" + role.ToString().ToLowerInvariant() + "_overview";
    }

    private string GetTowerRoleIcon(TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Basic:
                return "B";
            case TowerRole.Rapid:
                return "R";
            case TowerRole.Heavy:
                return "H";
            case TowerRole.Fire:
                return "F";
            case TowerRole.Slow:
                return "S";
            case TowerRole.Poison:
                return "P";
            case TowerRole.Sniper:
                return "N";
            case TowerRole.Alchemist:
                return "A";
            case TowerRole.Lightning:
                return "L";
            case TowerRole.Mortar:
                return "M";
            case TowerRole.Spike:
                return "K";
            default:
                return "T";
        }
    }

    private Button CreateTowerRoleCard(Transform parent, TowerRole role, MetaHubEntry entry, bool selected)
    {
        Color accent = GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery);
        GameObject card = CreatePanel(parent, "TowerRole_" + role, selected ? new Color32(52, 28, 70, 248) : new Color32(16, 25, 35, 245), Vector2.zero);
        LayoutElement layoutElement = card.AddComponent<LayoutElement>();
        layoutElement.minHeight = 98f;
        layoutElement.preferredHeight = 112f;
        layoutElement.flexibleHeight = 0f;
        AddEdgeFrame(card.transform, selected ? accent : new Color32(58, 64, 72, 255), selected ? 3f : 1f);
        AddVerticalAccent(card.transform, accent, selected ? 10f : 7f);

        Button button = card.AddComponent<Button>();
        string id = GetTowerOverviewEntryId(role);
        button.onClick.AddListener(() => SelectMetaHubEntry(id));
        ApplySelectableColors(button, card.GetComponent<Image>().color, accent);

        GameObject iconBox = CreateAnchoredPanel(card.transform, "TowerIcon", new Color32(7, 12, 18, 230), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, -28f), new Vector2(78f, 28f));
        AddEdgeFrame(iconBox.transform, accent, 1f);

        TextMeshProUGUI icon = CreateText(iconBox.transform, "Icon", 25f, TextAlignmentOptions.Center, selected ? Color.white : accent);
        icon.text = GetTowerRoleIcon(role);
        icon.fontStyle = FontStyles.Bold;
        Stretch(icon.rectTransform);

        TextMeshProUGUI label = CreateText(card.transform, "Label", 19f, TextAlignmentOptions.TopLeft, Color.white);
        label.text = TowerMasteryManager.GetTowerDisplayName(role);
        label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(label.rectTransform, 92f, 18f, 16f, 54f);

        TextMeshProUGUI state = CreateText(card.transform, "State", 13f, TextAlignmentOptions.BottomLeft, selected ? accent : mutedGray);
        state.text = entry.stateText;
        state.enableWordWrapping = false;
        state.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(state.rectTransform, 92f, 58f, 16f, 18f);
        return button;
    }

    private void RenderTowerCenterPanel(Transform parent, List<MetaHubEntry> entries, TowerRole role)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = towerMastery != null ? towerMastery.GetProfile(role) : null;
        int masteryLevel = towerMastery != null ? towerMastery.GetMasteryLevel(role) : 1;
        int xpInto = towerMastery != null ? towerMastery.GetMasteryXPIntoCurrentLevel(role) : 0;
        int xpNext = towerMastery != null ? towerMastery.GetXPToNextMasteryLevel(masteryLevel) : 100;

        TextMeshProUGUI header = CreateText(parent, "TowerHeader", 18f, TextAlignmentOptions.TopLeft, textColor);
        header.text = TowerMasteryManager.GetTowerDisplayName(role) + " Mastery\n<size=75%><color=#B9C2D0>Mastery Level " + masteryLevel + " | XP " + xpInto + " / " + xpNext + " | Freie Punkte " + (profile != null ? profile.unspentPoints : 0) + " | Ausgegeben " + (profile != null ? profile.spentPoints : 0) + "</color></size>";
        header.fontStyle = FontStyles.Bold;
        SetOffsets(header.rectTransform, 18f, 50f, 18f, 356f);
        CreateProgressBar(parent, new Vector2(0f, 0.78f), new Vector2(1f, 0.84f), new Vector2(18f, 0f), new Vector2(-18f, 0f), xpNext > 0 ? xpInto / (float)xpNext : 0f, GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery), "");

        RenderMilestones(parent, towerMastery, role);
        RenderNodePreview(parent, entries, role);
        RenderKeystonePreview(parent, role);
    }

    private void RenderMilestones(Transform parent, TowerMasteryManager towerMastery, TowerRole role)
    {
        GameObject row = CreateAnchoredPanel(parent, "MilestoneRow", new Color32(0, 0, 0, 0), new Vector2(0f, 0.61f), new Vector2(1f, 0.74f), new Vector2(18f, 0f), new Vector2(-18f, 0f));
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateMilestoneCard(row.transform, "I", towerMastery != null && towerMastery.IsMilestoneUnlocked(role, TowerMasteryMilestone.I));
        CreateMilestoneCard(row.transform, "II", towerMastery != null && towerMastery.IsMilestoneUnlocked(role, TowerMasteryMilestone.II));
        CreateMilestoneCard(row.transform, "III", towerMastery != null && towerMastery.IsMilestoneUnlocked(role, TowerMasteryMilestone.III));
        CreateMilestoneCard(row.transform, "IV", towerMastery != null && towerMastery.IsMilestoneUnlocked(role, TowerMasteryMilestone.IV));
        CreateMilestoneCard(row.transform, "V", towerMastery != null && towerMastery.IsMilestoneUnlocked(role, TowerMasteryMilestone.V));
    }

    private void RenderNodePreview(Transform parent, List<MetaHubEntry> entries, TowerRole role)
    {
        GameObject gridPanel = CreateAnchoredPanel(parent, "NodePreviewGrid", new Color32(0, 0, 0, 0), new Vector2(0f, 0.19f), new Vector2(1f, 0.58f), new Vector2(18f, 0f), new Vector2(-18f, 0f));
        GridLayoutGroup grid = gridPanel.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170f, 72f);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        string prefix = role.ToString().ToLowerInvariant() + "_node_";
        int count = 0;
        foreach (MetaHubEntry entry in entries)
        {
            if (!entry.entryId.StartsWith(prefix))
                continue;

            generatedEntryButtons.Add(CreateNodePreviewCard(gridPanel.transform, entry));
            count++;
            if (count >= 9)
                break;
        }
    }

    private void RenderKeystonePreview(Transform parent, TowerRole role)
    {
        GameObject row = CreateAnchoredPanel(parent, "KeystoneRow", new Color32(0, 0, 0, 0), new Vector2(0f, 0.02f), new Vector2(1f, 0.16f), new Vector2(18f, 0f), new Vector2(-18f, 0f));
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateKeystoneCard(row.transform, "Keystone A", "Gesperrt");
        CreateKeystoneCard(row.transform, "Keystone B", "Gesperrt");
        CreateKeystoneCard(row.transform, "Keystone C", "Gesperrt");
    }




    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private GameObject CreateAnchoredPanel(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject panel = CreatePanel(parent, objectName, color, Vector2.zero);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return panel;
    }

    private void AddSectionHeading(Transform parent, string title, string subtitle, Color accent)
    {
        GameObject heading = CreateAnchoredPanel(parent, "SectionHeading", new Color32(0, 0, 0, 0), new Vector2(0f, 0.88f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI titleText = CreateText(heading.transform, "Title", 26f, TextAlignmentOptions.TopLeft, accent);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(titleText.rectTransform, 4f, 2f, 12f, 38f);

        TextMeshProUGUI subtitleText = CreateText(heading.transform, "Subtitle", 14f, TextAlignmentOptions.TopLeft, mutedGray);
        subtitleText.text = subtitle;
        subtitleText.enableWordWrapping = false;
        subtitleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(subtitleText.rectTransform, 6f, 40f, 12f, 8f);
    }

    private void CreateResourceChip(Transform parent, string icon, string label, string value, Color accent)
    {
        GameObject chip = CreatePanel(parent, "ResourceChip_" + label, new Color32(12, 22, 28, 230), Vector2.zero);
        LayoutElement layoutElement = chip.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 70f;
        layoutElement.minWidth = 54f;
        layoutElement.flexibleWidth = 0f;
        AddEdgeFrame(chip.transform, new Color32(78, 60, 32, 255), 1f);

        GameObject iconBox = CreateAnchoredPanel(chip.transform, "IconBox", new Color32(18, 28, 38, 255), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(7f, -14f), new Vector2(32f, 14f));
        AddEdgeFrame(iconBox.transform, accent, 1f);

        TextMeshProUGUI iconText = CreateText(iconBox.transform, "Icon", 11f, TextAlignmentOptions.Center, accent);
        iconText.text = icon;
        iconText.fontStyle = FontStyles.Bold;
        Stretch(iconText.rectTransform);

        TextMeshProUGUI valueText = CreateText(chip.transform, "Value", 14f, TextAlignmentOptions.MidlineLeft, textColor);
        valueText.text = value;
        valueText.fontStyle = FontStyles.Bold;
        valueText.enableWordWrapping = false;
        valueText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(valueText.rectTransform, 38f, 4f, 4f, 18f);

        TextMeshProUGUI labelText = CreateText(chip.transform, "Label", 9f, TextAlignmentOptions.MidlineLeft, mutedGray);
        labelText.text = label;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(labelText.rectTransform, 38f, 28f, 4f, 4f);
    }

    private void CreateMetricCard(Transform parent, string title, string value, string subtitle, string progressText, float progress, Color accent, string icon)
    {
        GameObject card = CreatePanel(parent, "MetricCard_" + title, new Color32(16, 25, 35, 245), Vector2.zero);
        card.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddEdgeFrame(card.transform, accent, 1f);

        TextMeshProUGUI titleText = CreateText(card.transform, "Title", 15f, TextAlignmentOptions.Top, textColor);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(titleText.rectTransform, 10f, 14f, 10f, 96f);

        GameObject iconBox = CreateAnchoredPanel(card.transform, "MetricIcon", new Color32(8, 14, 18, 215), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 40f), new Vector2(76f, 98f));
        AddEdgeFrame(iconBox.transform, accent, 1f);
        TextMeshProUGUI iconText = CreateText(iconBox.transform, "Icon", 25f, TextAlignmentOptions.Center, accent);
        iconText.text = icon;
        iconText.fontStyle = FontStyles.Bold;
        Stretch(iconText.rectTransform);

        TextMeshProUGUI valueText = CreateText(card.transform, "Value", 31f, TextAlignmentOptions.TopLeft, accent);
        valueText.text = value;
        valueText.fontStyle = FontStyles.Bold;
        valueText.enableWordWrapping = false;
        valueText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(valueText.rectTransform, 92f, 58f, 14f, 48f);

        TextMeshProUGUI subtitleText = CreateText(card.transform, "Subtitle", 12f, TextAlignmentOptions.TopLeft, mutedGray);
        subtitleText.text = subtitle;
        subtitleText.enableWordWrapping = false;
        subtitleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(subtitleText.rectTransform, 92f, 96f, 14f, 26f);

        CreateProgressBar(card.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(92f, 16f), new Vector2(-18f, 26f), progress, accent, progressText);
    }

    private GameObject CreateDashboardPanel(Transform parent, string objectName, string title, Color accent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject panel = CreateAnchoredPanel(parent, objectName, detailPanelColor, anchorMin, anchorMax, offsetMin, offsetMax);
        AddEdgeFrame(panel.transform, new Color32(58, 64, 72, 255), 1f);
        AddVerticalAccent(panel.transform, accent, 4f);

        TextMeshProUGUI titleText = CreateText(panel.transform, "PanelTitle", 18f, TextAlignmentOptions.TopLeft, accent);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(16f, -44f);
        titleText.rectTransform.offsetMax = new Vector2(-14f, -12f);
        return panel;
    }

    private void CreateKeystoneSlot(Transform parent, string icon, string label, Color accent)
    {
        GameObject slot = CreatePanel(parent, "KeystoneSlot_" + label, new Color32(16, 25, 35, 245), Vector2.zero);
        slot.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddEdgeFrame(slot.transform, accent, 1f);

        TextMeshProUGUI iconText = CreateText(slot.transform, "Icon", 23f, TextAlignmentOptions.Center, accent);
        iconText.text = icon;
        iconText.fontStyle = FontStyles.Bold;
        SetOffsets(iconText.rectTransform, 2f, 4f, 2f, 24f);

        TextMeshProUGUI labelText = CreateText(slot.transform, "Label", 10f, TextAlignmentOptions.Center, textColor);
        labelText.text = "Lv. 1";
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(labelText.rectTransform, 2f, 50f, 2f, 4f);
    }

    private void CreateStatusRow(Transform parent, string title, string subtitle, string trailing, Color accent)
    {
        int index = CountChildrenWithPrefix(parent, "StatusRow_");
        float top = 58f + index * 60f;
        GameObject row = CreateAnchoredPanel(parent, "StatusRow_" + index, new Color32(12, 22, 28, 220), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -top - 48f), new Vector2(-16f, -top));
        AddEdgeFrame(row.transform, accent, 1f);

        TextMeshProUGUI titleText = CreateText(row.transform, "Title", 14f, TextAlignmentOptions.TopLeft, accent);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(titleText.rectTransform, 14f, 7f, 90f, 23f);

        TextMeshProUGUI subtitleText = CreateText(row.transform, "Subtitle", 12f, TextAlignmentOptions.BottomLeft, mutedGray);
        subtitleText.text = subtitle;
        subtitleText.enableWordWrapping = false;
        subtitleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(subtitleText.rectTransform, 14f, 25f, 90f, 6f);

        TextMeshProUGUI trailingText = CreateText(row.transform, "Trailing", 12f, TextAlignmentOptions.MidlineRight, textColor);
        trailingText.text = trailing;
        trailingText.enableWordWrapping = false;
        trailingText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(trailingText.rectTransform, 10f, 8f, 14f, 8f);
    }

    private void CreateGoalRow(Transform parent, string title, string progress, Color accent)
    {
        int index = CountChildrenWithPrefix(parent, "GoalRow_");
        float top = 58f + index * 48f;
        GameObject row = CreateAnchoredPanel(parent, "GoalRow_" + index, new Color32(12, 22, 28, 215), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -top - 40f), new Vector2(-16f, -top));
        AddVerticalAccent(row.transform, accent, 4f);

        TextMeshProUGUI titleText = CreateText(row.transform, "GoalTitle", 12f, TextAlignmentOptions.MidlineLeft, textColor);
        titleText.text = title;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(titleText.rectTransform, 14f, 4f, 68f, 4f);

        TextMeshProUGUI progressText = CreateText(row.transform, "GoalProgress", 12f, TextAlignmentOptions.MidlineRight, mutedGray);
        progressText.text = progress;
        progressText.enableWordWrapping = false;
        progressText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(progressText.rectTransform, 8f, 4f, 10f, 4f);
    }

    private void CreateInfoLine(Transform parent, string label, string value, float y)
    {
        TextMeshProUGUI line = CreateText(parent, "InfoLine_" + label, 12f, TextAlignmentOptions.MidlineLeft, mutedGray);
        line.text = label + " <align=right><color=#F0F4FA>" + value + "</color>";
        line.enableWordWrapping = false;
        line.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, y);
        rect.anchorMax = new Vector2(1f, y + 0.08f);
        rect.offsetMin = new Vector2(18f, 0f);
        rect.offsetMax = new Vector2(-18f, 0f);
    }

    private GameObject CreateProgressBar(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float progress, Color fillColor, string label)
    {
        GameObject bar = CreateAnchoredPanel(parent, "ProgressBar", new Color32(23, 27, 34, 255), anchorMin, anchorMax, offsetMin, offsetMax);
        AddEdgeFrame(bar.transform, new Color32(75, 58, 31, 255), 1f);

        GameObject fill = CreateDecoration(bar.transform, "Fill", fillColor);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI labelText = CreateText(bar.transform, "Label", 9f, TextAlignmentOptions.Center, textColor);
            labelText.text = label;
            labelText.enableWordWrapping = false;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(labelText.rectTransform);
        }

        return bar;
    }

    private void CreateMiniStat(Transform parent, string label, string value, Color accent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject stat = CreateAnchoredPanel(parent, "MiniStat_" + label, new Color32(12, 22, 28, 230), anchorMin, anchorMax, new Vector2(0f, 0f), new Vector2(-16f, 0f));
        AddEdgeFrame(stat.transform, new Color32(58, 64, 72, 255), 1f);

        TextMeshProUGUI statText = CreateText(stat.transform, "Text", 13f, TextAlignmentOptions.MidlineLeft, textColor);
        statText.text = label + " <align=right><color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">" + value + "</color>";
        statText.enableWordWrapping = false;
        statText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(statText.rectTransform, 14f, 6f, 14f, 6f);
    }

    private void CreateScoreBox(Transform parent, string label, string value, Color accent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject box = CreateAnchoredPanel(parent, "ScoreBox_" + label, new Color32(12, 22, 28, 230), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        AddEdgeFrame(box.transform, new Color32(58, 64, 72, 255), 1f);

        TextMeshProUGUI valueText = CreateText(box.transform, "Value", 25f, TextAlignmentOptions.Center, accent);
        valueText.text = value + "\n<size=48%><color=#B9C2D0>" + label + "</color></size>";
        valueText.fontStyle = FontStyles.Bold;
        Stretch(valueText.rectTransform);
    }

    private Transform CreateScrollableGridContent(Transform parent, string objectName, Vector2 cellSize, int columnCount)
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
        viewportImage.color = new Color32(255, 255, 255, 4);
        viewportImage.raycastTarget = true;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject(objectName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 900f);

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(2, 12, 2, 12);
        grid.spacing = new Vector2(12f, 12f);
        grid.cellSize = cellSize;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columnCount);

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        return contentObject.transform;
    }

    private void CreateMilestoneCard(Transform parent, string label, bool unlocked)
    {
        Color color = unlocked ? headerColor : lockedButtonColor;
        GameObject card = CreatePanel(parent, "Milestone_" + label, unlocked ? new Color32(49, 38, 18, 245) : new Color32(16, 25, 35, 245), Vector2.zero);
        card.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddEdgeFrame(card.transform, color, unlocked ? 2f : 1f);

        TextMeshProUGUI text = CreateText(card.transform, "Text", 18f, TextAlignmentOptions.Center, unlocked ? textColor : mutedGray);
        text.text = label + "\n<size=50%>" + (unlocked ? "Frei" : "Gesperrt") + "</size>";
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);
    }

    private Button CreateNodePreviewCard(Transform parent, MetaHubEntry entry)
    {
        bool selected = entry.entryId == selectedMetaHubEntryId;
        Color accent = GetSectionAccentColor(ChaosUnlockMenuSection.TowerMastery);
        GameObject card = CreatePanel(parent, "NodePreview_" + entry.entryId, selected ? new Color32(52, 28, 70, 248) : new Color32(16, 25, 35, 245), Vector2.zero);
        AddEdgeFrame(card.transform, selected ? accent : new Color32(58, 64, 72, 255), selected ? 2f : 1f);
        AddVerticalAccent(card.transform, entry.locked ? lockedButtonColor : accent, 4f);

        Button button = card.AddComponent<Button>();
        string id = entry.entryId;
        button.onClick.AddListener(() => SelectMetaHubEntry(id));
        ApplySelectableColors(button, card.GetComponent<Image>().color, accent);

        TextMeshProUGUI title = CreateText(card.transform, "Title", 12f, TextAlignmentOptions.TopLeft, textColor);
        title.text = entry.title;
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(title.rectTransform, 10f, 8f, 8f, 34f);

        TextMeshProUGUI state = CreateText(card.transform, "State", 10f, TextAlignmentOptions.BottomLeft, entry.locked ? mutedGray : accent);
        state.text = entry.stateText;
        state.enableWordWrapping = false;
        state.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(state.rectTransform, 10f, 38f, 8f, 8f);
        return button;
    }

    private void CreateKeystoneCard(Transform parent, string title, string state)
    {
        GameObject card = CreatePanel(parent, "KeystoneCard_" + title, new Color32(12, 22, 28, 235), Vector2.zero);
        card.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddEdgeFrame(card.transform, new Color32(84, 68, 92, 255), 1f);

        TextMeshProUGUI text = CreateText(card.transform, "Text", 12f, TextAlignmentOptions.Center, textColor);
        text.text = title + "\n<size=78%><color=#B9C2D0>" + state + "</color></size>";
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(text.rectTransform);
    }

    private int CountChildrenWithPrefix(Transform parent, string prefix)
    {
        if (parent == null)
            return 0;

        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name.StartsWith(prefix))
                count++;
        }

        return count;
    }

    private int GetTotalUnspentTowerPoints(TowerMasteryManager towerMastery)
    {
        return GetTotalUnspentTowerMasteryPoints(towerMastery);
    }

    private string GetSectionSubtitle(ChaosUnlockMenuSection section)
    {
        switch (section)
        {
            case ChaosUnlockMenuSection.General:
                return "Account, Content-Freischaltungen, QoL, Startoptionen und Loadout.";
            case ChaosUnlockMenuSection.ChaosResearch:
                return "Risiko-Pool, Varianten, Chaos-Waves, Konter, Risskerne und Ordnung.";
            case ChaosUnlockMenuSection.PathTechnique:
                return "Belohnt wird die Rettung nach Verbau. Events, Tools und Tile-Technik.";
            case ChaosUnlockMenuSection.EliteHunt:
                return "Optionales Endgame mit Auftraegen, Affixen, Siegeln und Riss-Elite.";
            case ChaosUnlockMenuSection.Archive:
                return "Lexikon-Kategorien und erklaerte Systeme.";
            default:
                return "Meta-Hub Uebersicht.";
        }
    }

    private string GetSectionIcon(ChaosUnlockMenuSection section)
    {
        switch (section)
        {
            case ChaosUnlockMenuSection.Overview:
                return "H";
            case ChaosUnlockMenuSection.General:
                return "A";
            case ChaosUnlockMenuSection.TowerMastery:
                return "T";
            case ChaosUnlockMenuSection.ChaosResearch:
                return "C";
            case ChaosUnlockMenuSection.PathTechnique:
                return "P";
            case ChaosUnlockMenuSection.EliteHunt:
                return "E";
            case ChaosUnlockMenuSection.Archive:
                return "B";
            default:
                return "?";
        }
    }

    private string FormatNumber(int value)
    {
        return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");
    }

    private void RefreshList()
    {
        if (entryListContent == null)
            return;

        ClearGeneratedEntryButtons();

        if (titleText != null)
            titleText.text = showingSectionTopics ? "META-HUB / " + GetSectionDisplayName(selectedSection).ToUpperInvariant() : "META-HUB / ARCHIV DER VERTEIDIGUNG";

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
        ChaosUnlockMenuSection rootSection;
        bool isRootSectionEntry = TryGetSectionFromRootEntryId(entry.entryId, out rootSection);
        bool isBackEntry = entry.entryId == BackToRootEntryId;
        Color accentColor = isRootSectionEntry ? GetSectionAccentColor(rootSection) : GetSectionAccentColor(selectedSection);
        Color cardColor = selected ? new Color32(93, 58, 20, 255) : entry.locked ? lockedButtonColor : buttonColor;
        GameObject buttonObject = new GameObject("MetaHubEntryButton_" + entry.entryId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(entryListContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = isBackEntry ? 48f : 78f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = cardColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        string entryId = entry.entryId;
        button.onClick.AddListener(() => HandleMetaHubEntryClicked(entryId));
        ApplySelectableColors(button, cardColor, accentColor);

        AddEdgeFrame(buttonObject.transform, selected ? accentColor : new Color32(58, 64, 62, 255), selected ? 3f : 1f);
        AddVerticalAccent(buttonObject.transform, selected ? accentColor : new Color32(93, 74, 42, 255), 8f);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", entryButtonFontSize, TextAlignmentOptions.MidlineLeft, Color.white);
        SetOffsets(text.rectTransform, 20f, isBackEntry ? 5f : 8f, 12f, isBackEntry ? 5f : 31f);
        text.text = entry.title;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI state = CreateText(buttonObject.transform, "StateText", 12f, TextAlignmentOptions.BottomLeft, selected ? new Color32(255, 218, 130, 255) : new Color32(176, 179, 168, 255));
        SetOffsets(state.rectTransform, 20f, 43f, 12f, 8f);
        state.text = entry.stateText;
        state.enableWordWrapping = false;
        state.overflowMode = TextOverflowModes.Ellipsis;
        state.gameObject.SetActive(!isBackEntry);
        return button;
    }

    private void HandleMetaHubEntryClicked(string entryId)
    {
        if (entryId == BackToRootEntryId)
        {
            showingSectionTopics = false;
            selectedMetaHubEntryId = GetRootSectionEntryId(selectedSection);
            RefreshAll();
            return;
        }

        ChaosUnlockMenuSection section;
        if (!showingSectionTopics && TryGetSectionFromRootEntryId(entryId, out section))
        {
            SelectSection(section);
            return;
        }

        SelectMetaHubEntry(entryId);
    }

    private void SelectMetaHubEntry(string entryId)
    {
        selectedMetaHubEntryId = entryId;
        RefreshDashboardContent();
    }

    private void RefreshDetail()
    {
        if (detailText == null)
            return;

        MetaHubEntry entry = GetSelectedMetaHubEntry();
        detailText.richText = true;
        detailText.text = GetDetailText(entry);
        if (detailScrollRect != null)
            detailScrollRect.verticalNormalizedPosition = 1f;
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
        sectionButtonContent.gameObject.SetActive(false);

        if (sectionButtonContent.parent != null)
            sectionButtonContent.parent.gameObject.SetActive(false);
    }

    private Button CreateSectionButton(ChaosUnlockMenuSection section)
    {
        bool selected = selectedSection == section;
        Color accentColor = GetSectionAccentColor(section);
        Color baseColor = selected ? Color.Lerp(buttonColor, accentColor, 0.28f) : buttonColor;

        GameObject buttonObject = new GameObject("MetaHubSectionButton_" + section, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(sectionButtonContent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 64f;
        layoutElement.preferredHeight = 92f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 1f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = baseColor;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectSection(section));
        ApplySelectableColors(button, baseColor, accentColor);

        AddEdgeFrame(buttonObject.transform, selected ? accentColor : new Color32(48, 53, 52, 255), selected ? 3f : 1f);
        AddVerticalAccent(buttonObject.transform, accentColor, selected ? 10f : 5f);

        TextMeshProUGUI icon = CreateText(buttonObject.transform, "Icon", 30f, TextAlignmentOptions.Center, selected ? Color.white : accentColor);
        icon.text = GetSectionIcon(section);
        icon.fontStyle = FontStyles.Bold;
        SetOffsets(icon.rectTransform, 18f, 8f, 212f, 8f);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", entryButtonFontSize + 4f, TextAlignmentOptions.MidlineLeft, Color.white);
        text.text = GetSectionDisplayName(section);
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(text.rectTransform, 82f, 3f, 12f, 3f);
        return button;
    }

    private void SelectSection(ChaosUnlockMenuSection section)
    {
        showingSectionTopics = true;
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
            if (entry.entryId == BackToRootEntryId)
                continue;

            if (entry.entryId == selectedMetaHubEntryId)
                return;
        }

        foreach (MetaHubEntry entry in entries)
        {
            if (entry.entryId != BackToRootEntryId)
            {
                selectedMetaHubEntryId = entry.entryId;
                return;
            }
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
        if (!showingSectionTopics)
            return BuildRootSectionEntries();

        List<MetaHubEntry> entries;
        switch (selectedSection)
        {
            case ChaosUnlockMenuSection.General:
                entries = BuildGeneralEntries();
                break;
            case ChaosUnlockMenuSection.TowerMastery:
                entries = BuildTowerMasteryEntries();
                break;
            case ChaosUnlockMenuSection.ChaosResearch:
                entries = BuildChaosResearchEntries();
                break;
            case ChaosUnlockMenuSection.PathTechnique:
                entries = BuildPathTechniqueEntries();
                break;
            case ChaosUnlockMenuSection.EliteHunt:
                entries = BuildEliteHuntEntries();
                break;
            case ChaosUnlockMenuSection.Archive:
                entries = BuildArchiveEntries();
                break;
            default:
                entries = BuildOverviewEntries();
                break;
        }

        entries.Insert(0, Entry(BackToRootEntryId, "< Oberthemen", "Zurueck", "Zurueck zur Meta-Hub-Uebersicht mit allen Hauptbereichen."));
        return entries;
    }

    private List<MetaHubEntry> BuildRootSectionEntries()
    {
        return new List<MetaHubEntry>
        {
            BuildRootSectionEntry(ChaosUnlockMenuSection.Overview, "Run-Nachbericht, Ziele, Fortschrittskarten und aktives Loadout."),
            BuildRootSectionEntry(ChaosUnlockMenuSection.General, "Account-Fortschritt, Tower-/Tile-Freischaltungen, QoL, Startoptionen und Loadout-Budget."),
            BuildRootSectionEntry(ChaosUnlockMenuSection.TowerMastery, "Tower-spezifische Mastery-Trees, Milestones, Punkte, XP und Keystones."),
            BuildRootSectionEntry(ChaosUnlockMenuSection.ChaosResearch, "Risiko-Pool, Chaos-Varianten, Chaos-Waves, Konter, Risskerne und Ordnung."),
            BuildRootSectionEntry(ChaosUnlockMenuSection.PathTechnique, "Verbau-Events, Rettungsstaerke, Pfadwerkzeuge, Tile-Technik und Rissarchitektur."),
            BuildRootSectionEntry(ChaosUnlockMenuSection.EliteHunt, "Optionale Elite-Jagd, Auftraege, Affixe, Belohnungen, Konter und Riss-Elite."),
            BuildRootSectionEntry(ChaosUnlockMenuSection.Archive, "Lexikon und Erklaerungen zu Spielsystemen, Gegnern, Chaos, Pfadtechnik und Auswertung.")
        };
    }

    private MetaHubEntry BuildRootSectionEntry(ChaosUnlockMenuSection section, string description)
    {
        return Entry(GetRootSectionEntryId(section), GetSectionDisplayName(section), GetRootSectionStateText(section), BuildRootSectionDetailText(section, description));
    }

    private string BuildRootSectionDetailText(ChaosUnlockMenuSection section, string description)
    {
        return description + "\n\nKlicke links auf diesen Bereich, um die Unterthemen zu oeffnen. Die Oberthemen verschwinden dann und die linke Liste zeigt nur noch die passenden Unterthemen.";
    }

    private string GetRootSectionStateText(ChaosUnlockMenuSection section)
    {
        switch (section)
        {
            case ChaosUnlockMenuSection.Overview:
                return "Start und Ziele";
            case ChaosUnlockMenuSection.General:
                return "Account / Content";
            case ChaosUnlockMenuSection.TowerMastery:
                return "Tower-Trees";
            case ChaosUnlockMenuSection.ChaosResearch:
                return "Chaos-System";
            case ChaosUnlockMenuSection.PathTechnique:
                return "Verbau / Tiles";
            case ChaosUnlockMenuSection.EliteHunt:
                return "Endgame opt-in";
            case ChaosUnlockMenuSection.Archive:
                return "Lexikon";
            default:
                return "Bereich";
        }
    }

    private string GetRootSectionEntryId(ChaosUnlockMenuSection section)
    {
        return RootSectionEntryPrefix + section.ToString();
    }

    private bool TryGetSectionFromRootEntryId(string entryId, out ChaosUnlockMenuSection section)
    {
        section = ChaosUnlockMenuSection.Overview;

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(RootSectionEntryPrefix))
            return false;

        string rawSection = entryId.Substring(RootSectionEntryPrefix.Length);
        return System.Enum.TryParse<ChaosUnlockMenuSection>(rawSection, out section);
    }

    private List<MetaHubEntry> BuildOverviewEntries()
    {
        return new List<MetaHubEntry>
        {
            Entry("overview_last_run", "Letzter Run", GetLastRunStateText(), BuildLastRunOverviewText()),
            Entry("overview_goals", "Naechste Ziele", GetRecommendedGoalsStateText(), BuildRecommendedGoalsText()),
            Entry("overview_progress_cards", "Fortschrittskarten", GetProgressCardsStateText(), BuildProgressCardsText()),
            Entry("overview_loadout", "Aktives Loadout", GetLoadoutStateText(), BuildActiveLoadoutText())
        };
    }

    private List<MetaHubEntry> BuildGeneralEntries()
    {
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        GeneralMetaCategory selectedCategory;
        bool filterByCategory = TryGetSelectedGeneralCategoryFilter(generalMeta, out selectedCategory);
        List<MetaHubEntry> entries = new List<MetaHubEntry>();

        if (!filterByCategory)
        {
            entries.Add(Entry("general_account", "Account-Uebersicht", GetGeneralAccountStateText(generalMeta), BuildGeneralAccountText(generalMeta)));
            entries.Add(Entry("general_tower_unlocks", "Tower-Freischaltungen", GetGeneralCategoryStateText(generalMeta, GeneralMetaCategory.TowerUnlock), BuildGeneralTowerUnlocksText()));
            entries.Add(Entry("general_tile_unlocks", "Tile-Freischaltungen", GetGeneralCategoryStateText(generalMeta, GeneralMetaCategory.TileUnlock), BuildGeneralTileUnlocksText()));
            entries.Add(Entry("general_qol", "Komfort / QoL", GetGeneralCategoryStateText(generalMeta, GeneralMetaCategory.QoL), BuildGeneralQoLText()));
            entries.Add(Entry("general_start_options", "Startoptionen", GetGeneralCategoryStateText(generalMeta, GeneralMetaCategory.StartOption), BuildGeneralStartOptionsText()));
            entries.Add(Entry("general_enemy_research", "Gegnerforschung", GetGeneralCategoryStateText(generalMeta, GeneralMetaCategory.EnemyResearch), BuildGeneralEnemyResearchText()));
            entries.Add(Entry("general_loadout", "Meta-Loadout", GetGeneralLoadoutStateText(generalMeta), BuildGeneralLoadoutText(generalMeta)));
        }

        if (generalMeta == null)
            return entries;

        if (!filterByCategory)
            return entries;

        foreach (GeneralMetaNodeDefinition definition in generalMeta.GetDefinitions())
        {
            if (definition == null)
                continue;

            if (definition.category != selectedCategory)
                continue;

            entries.Add(Entry("general_node_" + definition.nodeId, definition.displayName, generalMeta.GetNodeStateText(definition), "", IsGeneralNodeLocked(generalMeta, definition)));
        }

        return entries;
    }

    private bool TryGetSelectedGeneralCategoryFilter(GeneralMetaProgressionManager generalMeta, out GeneralMetaCategory category)
    {
        if (TryGetGeneralCategoryFilterFromOverviewEntry(selectedMetaHubEntryId, out category))
            return true;

        string generalNodeId = GetGeneralNodeIdFromEntry(selectedMetaHubEntryId);
        if (!string.IsNullOrEmpty(generalNodeId) && generalMeta != null)
        {
            GeneralMetaNodeDefinition definition = generalMeta.GetDefinition(generalNodeId);
            if (definition != null && definition.category != GeneralMetaCategory.Account)
            {
                category = definition.category;
                return true;
            }
        }

        category = GeneralMetaCategory.Account;
        return false;
    }

    private bool TryGetGeneralCategoryFilterFromOverviewEntry(string entryId, out GeneralMetaCategory category)
    {
        category = GeneralMetaCategory.Account;

        switch (entryId)
        {
            case "general_tower_unlocks":
                category = GeneralMetaCategory.TowerUnlock;
                return true;
            case "general_tile_unlocks":
                category = GeneralMetaCategory.TileUnlock;
                return true;
            case "general_qol":
                category = GeneralMetaCategory.QoL;
                return true;
            case "general_start_options":
                category = GeneralMetaCategory.StartOption;
                return true;
            case "general_enemy_research":
                category = GeneralMetaCategory.EnemyResearch;
                return true;
            case "general_loadout":
                category = GeneralMetaCategory.MetaLoadout;
                return true;
            default:
                return false;
        }
    }

    private List<MetaHubEntry> BuildTowerMasteryEntries()
    {
        List<MetaHubEntry> entries = new List<MetaHubEntry>();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        BasicTowerMasteryManager basicMastery = GetBasicTowerMasteryManager();
        RapidTowerMasteryManager rapidMastery = GetRapidTowerMasteryManager();
        HeavyTowerMasteryManager heavyMastery = GetHeavyTowerMasteryManager();
        FireTowerMasteryManager fireMastery = GetFireTowerMasteryManager();
        SlowTowerMasteryManager slowMastery = GetSlowTowerMasteryManager();
        PoisonTowerMasteryManager poisonMastery = GetPoisonTowerMasteryManager();
        SniperTowerMasteryManager sniperMastery = GetSniperTowerMasteryManager();
        AlchemistTowerMasteryManager alchemistMastery = GetAlchemistTowerMasteryManager();
        LightningTowerMasteryManager lightningMastery = GetLightningTowerMasteryManager();
        MortarTowerMasteryManager mortarMastery = GetMortarTowerMasteryManager();
        SpikeTowerMasteryManager spikeMastery = GetSpikeTowerMasteryManager();

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

        entries.Add(Entry("tower_slow_overview", "Slow Tower Mastery", GetSlowMasteryStateText(slowMastery), ""));

        if (slowMastery != null)
        {
            foreach (SlowTowerMasteryNodeDefinition definition in slowMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !slowMastery.IsMilestoneUnlocked(definition.gate) && slowMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("slow_node_" + definition.nodeId, definition.displayName, slowMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_poison_overview", "Poison Tower Mastery", GetPoisonMasteryStateText(poisonMastery), ""));

        if (poisonMastery != null)
        {
            foreach (PoisonTowerMasteryNodeDefinition definition in poisonMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !poisonMastery.IsMilestoneUnlocked(definition.gate) && poisonMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("poison_node_" + definition.nodeId, definition.displayName, poisonMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_sniper_overview", "Sniper Tower Mastery", GetSniperMasteryStateText(sniperMastery), ""));

        if (sniperMastery != null)
        {
            foreach (SniperTowerMasteryNodeDefinition definition in sniperMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !sniperMastery.IsMilestoneUnlocked(definition.gate) && sniperMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("sniper_node_" + definition.nodeId, definition.displayName, sniperMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_alchemist_overview", "Alchemist Tower Mastery", GetAlchemistMasteryStateText(alchemistMastery), ""));

        if (alchemistMastery != null)
        {
            foreach (AlchemistTowerMasteryNodeDefinition definition in alchemistMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !alchemistMastery.IsMilestoneUnlocked(definition.gate) && alchemistMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("alchemist_node_" + definition.nodeId, definition.displayName, alchemistMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_lightning_overview", "Lightning Tower Mastery", GetLightningMasteryStateText(lightningMastery), ""));

        if (lightningMastery != null)
        {
            foreach (LightningTowerMasteryNodeDefinition definition in lightningMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !lightningMastery.IsMilestoneUnlocked(definition.gate) && lightningMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("lightning_node_" + definition.nodeId, definition.displayName, lightningMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_mortar_overview", "Mortar Tower Mastery", GetMortarMasteryStateText(mortarMastery), ""));

        if (mortarMastery != null)
        {
            foreach (MortarTowerMasteryNodeDefinition definition in mortarMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !mortarMastery.IsMilestoneUnlocked(definition.gate) && mortarMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("mortar_node_" + definition.nodeId, definition.displayName, mortarMastery.GetNodeStateText(definition), "", locked));
            }
        }

        entries.Add(Entry("tower_spike_overview", "Spike Tower Mastery", GetSpikeMasteryStateText(spikeMastery), ""));

        if (spikeMastery != null)
        {
            foreach (SpikeTowerMasteryNodeDefinition definition in spikeMastery.GetDefinitions())
            {
                if (definition == null)
                    continue;

                bool locked = !spikeMastery.IsMilestoneUnlocked(definition.gate) && spikeMastery.GetNodeRank(definition.nodeId) < definition.maxRank;
                entries.Add(Entry("spike_node_" + definition.nodeId, definition.displayName, spikeMastery.GetNodeStateText(definition), "", locked));
            }
        }

        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            if (role == TowerRole.Basic || role == TowerRole.Rapid || role == TowerRole.Heavy || role == TowerRole.Fire || role == TowerRole.Slow || role == TowerRole.Poison || role == TowerRole.Sniper || role == TowerRole.Alchemist || role == TowerRole.Lightning || role == TowerRole.Mortar || role == TowerRole.Spike)
                continue;

            entries.Add(TowerEntry(role, IsLaterTowerRole(role), towerMastery));
        }

        return entries;
    }

    private List<MetaHubEntry> BuildChaosResearchEntries()
    {
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        List<MetaHubEntry> entries = new List<MetaHubEntry>
        {
            Entry("chaos_research_overview", "Chaos-Uebersicht", GetChaosResearchOverviewStateText(chaosResearch), BuildChaosResearchOverviewText(chaosResearch)),
            Entry("chaos_risk_pool", "Risiko-Pool", GetChaosResearchCategoryStateText(chaosResearch, ChaosResearchCategory.RiskPool), BuildChaosRiskPoolText()),
            Entry("chaos_variants", "Chaos-Varianten", GetChaosResearchCategoryStateText(chaosResearch, ChaosResearchCategory.ChaosVariants), BuildChaosVariantResearchText()),
            Entry("chaos_waves", "Chaos-Waves", GetChaosResearchCategoryStateText(chaosResearch, ChaosResearchCategory.ChaosWaves), BuildChaosWaveResearchText()),
            Entry("chaos_counters", "Chaos-Konter", GetChaosResearchCategoryStateText(chaosResearch, ChaosResearchCategory.ChaosCounters), BuildChaosCounterResearchText()),
            Entry("chaos_offers", "Angebotskontrolle", GetChaosResearchCategoryStateText(chaosResearch, ChaosResearchCategory.OfferControl), BuildChaosOfferControlText()),
            Entry("chaos_endgame", "Chaos-5-Endgame", GetChaosResearchCategoryStateText(chaosResearch, ChaosResearchCategory.Chaos5Endgame), BuildChaosEndgameText(chaosResearch), chaosResearch == null || chaosResearch.highestChaosLevelEver < 5),
            Entry("chaos_order", "Gerechtigkeit / Ordnung", GetChaosResearchCategoryStateText(chaosResearch, ChaosResearchCategory.JusticeOrder), BuildChaosJusticeOrderText())
        };

        if (chaosResearch == null)
            return entries;

        foreach (ChaosResearchNodeDefinition definition in chaosResearch.GetDefinitions())
        {
            if (definition == null)
                continue;

            entries.Add(Entry("chaos_research_node_" + definition.nodeId, definition.displayName, chaosResearch.GetNodeStateText(definition), "", IsChaosResearchNodeLocked(chaosResearch, definition)));
        }

        return entries;
    }

    private List<MetaHubEntry> BuildPathTechniqueEntries()
    {
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        List<MetaHubEntry> entries = new List<MetaHubEntry>
        {
            Entry("path_overview", "Pfadtechnik-Uebersicht", GetPathTechniqueOverviewStateText(pathTechnique), BuildPathTechniqueOverviewText(pathTechnique)),
            Entry("path_event_pool", "Event-Pool", GetPathTechniqueCategoryStateText(pathTechnique, PathTechniqueCategory.EventPool), BuildPathEventPoolText()),
            Entry("path_event_quality", "Event-Qualitaet", GetPathTechniqueCategoryStateText(pathTechnique, PathTechniqueCategory.EventQuality), BuildPathEventQualityText()),
            Entry("path_rescue_power", "Rettungsstaerke", GetPathTechniqueCategoryStateText(pathTechnique, PathTechniqueCategory.RescuePower), BuildPathRescuePowerText()),
            Entry("path_tools", "Pfadwerkzeuge", GetPathTechniqueCategoryStateText(pathTechnique, PathTechniqueCategory.PathTools), BuildPathToolsText()),
            Entry("path_tile_tech", "Tile-Technik", GetPathTechniqueCategoryStateText(pathTechnique, PathTechniqueCategory.TileTechnique), BuildPathTileTechniqueText()),
            Entry("path_rift_architecture", "Rissarchitektur", GetPathTechniqueCategoryStateText(pathTechnique, PathTechniqueCategory.RiftArchitecture), BuildPathRiftArchitectureText(pathTechnique), pathTechnique == null || pathTechnique.totalChaosBlockedRecoveriesEver <= 0)
        };

        if (pathTechnique == null)
            return entries;

        foreach (PathTechniqueNodeDefinition definition in pathTechnique.GetDefinitions())
        {
            if (definition == null)
                continue;

            entries.Add(Entry("path_technique_node_" + definition.nodeId, definition.displayName, pathTechnique.GetNodeStateText(definition), "", IsPathTechniqueNodeLocked(pathTechnique, definition)));
        }

        return entries;
    }

    private List<MetaHubEntry> BuildEliteHuntEntries()
    {
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();
        List<MetaHubEntry> entries = new List<MetaHubEntry>
        {
            Entry("elite_overview", "Elite-Uebersicht", GetEliteHuntOverviewStateText(eliteHunt), BuildEliteHuntOverviewText(eliteHunt)),
            Entry("elite_contracts", "Elite-Auftraege", GetEliteHuntCategoryStateText(eliteHunt, EliteHuntCategory.Contracts), BuildEliteContractsText()),
            Entry("elite_affixes", "Elite-Affixe", GetEliteHuntCategoryStateText(eliteHunt, EliteHuntCategory.Affixes), BuildEliteAffixesText()),
            Entry("elite_rewards", "Elite-Belohnungen", GetEliteHuntCategoryStateText(eliteHunt, EliteHuntCategory.Rewards), BuildEliteRewardsText()),
            Entry("elite_frequency", "Elite-Haeufigkeit", GetEliteHuntCategoryStateText(eliteHunt, EliteHuntCategory.Frequency), BuildEliteFrequencyText()),
            Entry("elite_counters", "Elite-Konter", GetEliteHuntCategoryStateText(eliteHunt, EliteHuntCategory.Counters), BuildEliteCountersText()),
            Entry("elite_rift", "Riss-Elite / Endgame", GetEliteHuntCategoryStateText(eliteHunt, EliteHuntCategory.RiftElite), BuildEliteRiftText(eliteHunt), eliteHunt == null || !eliteHunt.IsRiftEliteVisible())
        };

        if (eliteHunt == null)
            return entries;

        foreach (EliteHuntNodeDefinition definition in eliteHunt.GetDefinitions())
        {
            if (definition == null)
                continue;

            entries.Add(Entry("elite_hunt_node_" + definition.nodeId, definition.displayName, eliteHunt.GetNodeStateText(definition), "", IsEliteHuntNodeLocked(eliteHunt, definition)));
        }

        return entries;

/*
        return new List<MetaHubEntry>
        {
            Entry("elite_locked", "Elite-Jagd", "Gesperrt", "Spaeteres Endgame-System.\n\nMoegliche Bedingungen: Boss bei Chaos 5 besiegen, Account-Level erreichen oder Wave 30 ueberstehen.", true),
            Entry("elite_contracts", "Elite-Auftraege", "Gesperrt", "Auftraege wie Elite Runner besiegen, Elite Mage ohne Base-Schaden oder Elite-Wave mit Chaos-Wave-Block ueberstehen.", true),
            Entry("elite_affixes", "Elite-Affixe", "Gesperrt", "Schnell, gepanzert, regenerierend, teleportierend, resistent, splitternd, Nachhut-Elite und Riss-Elite.", true),
            Entry("elite_rewards", "Elite-Belohnungen", "Gesperrt", "Kernwissen, Elite-Siegel, seltene Tower-Mastery-XP, kosmetische Tower-Visuals, Elite-Lexikon und Trophäen.", true),
            Entry("elite_frequency", "Elite-Haeufigkeit", "Opt-in spaeter", "Elite-Jagd: Aus / Leicht / Normal / Hart. Mehr Risiko gibt bessere Belohnungen.", true),
            Entry("elite_counters", "Elite-Konter", "Gesperrt", "Kleine spezifische Forschung, bessere Anzeige und klare Preview. Keine globalen starken Nerfs.", true)
        };
*/
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
        SlowTowerMasteryManager slowMastery = GetSlowTowerMasteryManager();
        PoisonTowerMasteryManager poisonMastery = GetPoisonTowerMasteryManager();
        SniperTowerMasteryManager sniperMastery = GetSniperTowerMasteryManager();
        AlchemistTowerMasteryManager alchemistMastery = GetAlchemistTowerMasteryManager();
        LightningTowerMasteryManager lightningMastery = GetLightningTowerMasteryManager();
        MortarTowerMasteryManager mortarMastery = GetMortarTowerMasteryManager();
        SpikeTowerMasteryManager spikeMastery = GetSpikeTowerMasteryManager();
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();

        string generalNodeId = GetGeneralNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(generalNodeId) && generalMeta != null)
            return generalMeta.GetNodeDetailText(generalNodeId);

        string chaosResearchNodeId = GetChaosResearchNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(chaosResearchNodeId) && chaosResearch != null)
            return chaosResearch.GetNodeDetailText(chaosResearchNodeId);

        if (entry.entryId == "chaos_research_overview" && chaosResearch != null)
            return chaosResearch.GetOverviewText();

        string pathTechniqueNodeId = GetPathTechniqueNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(pathTechniqueNodeId) && pathTechnique != null)
            return pathTechnique.GetNodeDetailText(pathTechniqueNodeId);

        if (entry.entryId == "path_overview" && pathTechnique != null)
            return pathTechnique.GetOverviewText();

        string eliteHuntNodeId = GetEliteHuntNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(eliteHuntNodeId) && eliteHunt != null)
            return eliteHunt.GetNodeDetailText(eliteHuntNodeId);

        if (entry.entryId == "elite_overview" && eliteHunt != null)
            return eliteHunt.GetOverviewText();

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

        if (entry.entryId == "tower_slow_overview" && slowMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Slow) + "\n\n" : "";
            return "<b>Slow Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetSlowMasteryStateText(slowMastery) + "</color></size>\n\n" + globalText + "Konkreter Slow-Tree:\n" + slowMastery.GetOverviewText();
        }

        string slowNodeId = GetSlowNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(slowNodeId) && slowMastery != null)
            return slowMastery.GetNodeDetailText(slowNodeId);

        if (entry.entryId == "tower_poison_overview" && poisonMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Poison) + "\n\n" : "";
            return "<b>Poison Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetPoisonMasteryStateText(poisonMastery) + "</color></size>\n\n" + globalText + "Konkreter Poison-Tree:\n" + poisonMastery.GetOverviewText();
        }

        string poisonNodeId = GetPoisonNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(poisonNodeId) && poisonMastery != null)
            return poisonMastery.GetNodeDetailText(poisonNodeId);

        if (entry.entryId == "tower_sniper_overview" && sniperMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Sniper) + "\n\n" : "";
            return "<b>Sniper Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetSniperMasteryStateText(sniperMastery) + "</color></size>\n\n" + globalText + "Konkreter Sniper-Tree:\n" + sniperMastery.GetOverviewText();
        }

        string sniperNodeId = GetSniperNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(sniperNodeId) && sniperMastery != null)
            return sniperMastery.GetNodeDetailText(sniperNodeId);

        if (entry.entryId == "tower_alchemist_overview" && alchemistMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Alchemist) + "\n\n" : "";
            return "<b>Alchemist Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetAlchemistMasteryStateText(alchemistMastery) + "</color></size>\n\n" + globalText + "Konkreter Alchemist-Tree:\n" + alchemistMastery.GetOverviewText();
        }

        string alchemistNodeId = GetAlchemistNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(alchemistNodeId) && alchemistMastery != null)
            return alchemistMastery.GetNodeDetailText(alchemistNodeId);

        if (entry.entryId == "tower_lightning_overview" && lightningMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Lightning) + "\n\n" : "";
            return "<b>Lightning Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetLightningMasteryStateText(lightningMastery) + "</color></size>\n\n" + globalText + "Konkreter Lightning-Tree:\n" + lightningMastery.GetOverviewText();
        }

        string lightningNodeId = GetLightningNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(lightningNodeId) && lightningMastery != null)
            return lightningMastery.GetNodeDetailText(lightningNodeId);

        if (entry.entryId == "tower_mortar_overview" && mortarMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Mortar) + "\n\n" : "";
            return "<b>Mortar Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetMortarMasteryStateText(mortarMastery) + "</color></size>\n\n" + globalText + "Konkreter Mortar-Tree:\n" + mortarMastery.GetOverviewText();
        }

        string mortarNodeId = GetMortarNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(mortarNodeId) && mortarMastery != null)
            return mortarMastery.GetNodeDetailText(mortarNodeId);

        if (entry.entryId == "tower_spike_overview" && spikeMastery != null)
        {
            string globalText = towerMastery != null ? towerMastery.GetRoleOverviewText(TowerRole.Spike) + "\n\n" : "";
            return "<b>Spike Tower Mastery</b>\n<size=90%><color=#B9C2D0>" + GetSpikeMasteryStateText(spikeMastery) + "</color></size>\n\n" + globalText + "Konkreter Spike-Tree:\n" + spikeMastery.GetOverviewText();
        }

        string spikeNodeId = GetSpikeNodeIdFromEntry(entry.entryId);
        if (!string.IsNullOrEmpty(spikeNodeId) && spikeMastery != null)
            return spikeMastery.GetNodeDetailText(spikeNodeId);

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

        string generalNodeId = GetGeneralNodeIdFromEntry(entry.entryId);
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();

        if (!string.IsNullOrEmpty(generalNodeId) && generalMeta != null)
        {
            RefreshGeneralPrimaryActionButton(generalMeta, generalNodeId);
            return;
        }

        string chaosResearchNodeId = GetChaosResearchNodeIdFromEntry(entry.entryId);
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();

        if (!string.IsNullOrEmpty(chaosResearchNodeId) && chaosResearch != null)
        {
            RefreshChaosResearchPrimaryActionButton(chaosResearch, chaosResearchNodeId);
            return;
        }

        string pathTechniqueNodeId = GetPathTechniqueNodeIdFromEntry(entry.entryId);
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();

        if (!string.IsNullOrEmpty(pathTechniqueNodeId) && pathTechnique != null)
        {
            RefreshPathTechniquePrimaryActionButton(pathTechnique, pathTechniqueNodeId);
            return;
        }

        string eliteHuntNodeId = GetEliteHuntNodeIdFromEntry(entry.entryId);
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();

        if (!string.IsNullOrEmpty(eliteHuntNodeId) && eliteHunt != null)
        {
            RefreshEliteHuntPrimaryActionButton(eliteHunt, eliteHuntNodeId);
            return;
        }

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

        string slowNodeId = GetSlowNodeIdFromEntry(entry.entryId);
        SlowTowerMasteryManager slowMastery = GetSlowTowerMasteryManager();

        if (!string.IsNullOrEmpty(slowNodeId) && slowMastery != null)
        {
            RefreshSlowPrimaryActionButton(slowMastery, slowNodeId);
            return;
        }

        string poisonNodeId = GetPoisonNodeIdFromEntry(entry.entryId);
        PoisonTowerMasteryManager poisonMastery = GetPoisonTowerMasteryManager();

        if (!string.IsNullOrEmpty(poisonNodeId) && poisonMastery != null)
        {
            RefreshPoisonPrimaryActionButton(poisonMastery, poisonNodeId);
            return;
        }

        string sniperNodeId = GetSniperNodeIdFromEntry(entry.entryId);
        SniperTowerMasteryManager sniperMastery = GetSniperTowerMasteryManager();

        if (!string.IsNullOrEmpty(sniperNodeId) && sniperMastery != null)
        {
            RefreshSniperPrimaryActionButton(sniperMastery, sniperNodeId);
            return;
        }

        string alchemistNodeId = GetAlchemistNodeIdFromEntry(entry.entryId);
        AlchemistTowerMasteryManager alchemistMastery = GetAlchemistTowerMasteryManager();

        if (!string.IsNullOrEmpty(alchemistNodeId) && alchemistMastery != null)
        {
            RefreshAlchemistPrimaryActionButton(alchemistMastery, alchemistNodeId);
            return;
        }

        string lightningNodeId = GetLightningNodeIdFromEntry(entry.entryId);
        LightningTowerMasteryManager lightningMastery = GetLightningTowerMasteryManager();

        if (!string.IsNullOrEmpty(lightningNodeId) && lightningMastery != null)
        {
            RefreshLightningPrimaryActionButton(lightningMastery, lightningNodeId);
            return;
        }

        string mortarNodeId = GetMortarNodeIdFromEntry(entry.entryId);
        MortarTowerMasteryManager mortarMastery = GetMortarTowerMasteryManager();

        if (!string.IsNullOrEmpty(mortarNodeId) && mortarMastery != null)
        {
            RefreshMortarPrimaryActionButton(mortarMastery, mortarNodeId);
            return;
        }

        string spikeNodeId = GetSpikeNodeIdFromEntry(entry.entryId);
        SpikeTowerMasteryManager spikeMastery = GetSpikeTowerMasteryManager();

        if (!string.IsNullOrEmpty(spikeNodeId) && spikeMastery != null)
        {
            RefreshSpikePrimaryActionButton(spikeMastery, spikeNodeId);
            return;
        }

        primaryActionButton.gameObject.SetActive(false);
    }

    private void RefreshGeneralPrimaryActionButton(GeneralMetaProgressionManager generalMeta, string generalNodeId)
    {
        if (generalMeta == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        GeneralMetaNodeDefinition definition = generalMeta.GetDefinition(generalNodeId);
        GeneralMetaNodeState state = generalMeta.GetNodeState(generalNodeId);

        if (definition == null || state == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        bool readOnly = !IsFullMetaHubAvailable(GetGameManager());
        bool purchased = state.purchased;
        bool canBuy = !readOnly && generalMeta.CanPurchaseNode(generalNodeId);
        bool canActivate = !readOnly && generalMeta.CanActivateNode(generalNodeId);
        bool canDeactivate = !readOnly && definition.RequiresLoadoutSlot() && purchased && state.active;

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivate || canDeactivate;

        if (primaryActionButtonText == null)
            return;

        if (readOnly)
            primaryActionButtonText.text = "Read-only im Run";
        else if (canBuy)
            primaryActionButtonText.text = "Kaufen (" + definition.cost + ")";
        else if (canActivate)
            primaryActionButtonText.text = "Aktivieren";
        else if (canDeactivate)
            primaryActionButtonText.text = "Deaktivieren";
        else if (purchased && definition.RequiresLoadoutSlot())
            primaryActionButtonText.text = state.active ? "Aktiv" : "Gekauft";
        else if (purchased)
            primaryActionButtonText.text = "Freigeschaltet";
        else
            primaryActionButtonText.text = generalMeta.GetNodeStateText(definition);
    }

    private void RefreshChaosResearchPrimaryActionButton(ChaosResearchProgressionManager chaosResearch, string chaosResearchNodeId)
    {
        if (chaosResearch == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        ChaosResearchNodeDefinition definition = chaosResearch.GetDefinition(chaosResearchNodeId);
        ChaosResearchNodeState state = chaosResearch.GetNodeState(chaosResearchNodeId);

        if (definition == null || state == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        bool readOnly = !IsFullMetaHubAvailable(GetGameManager());
        bool purchased = state.purchased;
        bool canBuy = !readOnly && chaosResearch.CanPurchaseNode(chaosResearchNodeId);
        bool canActivate = !readOnly && chaosResearch.CanActivateNode(chaosResearchNodeId);
        bool canDeactivate = !readOnly && definition.RequiresLoadoutSlot() && purchased && state.active;

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivate || canDeactivate;

        if (primaryActionButtonText == null)
            return;

        if (readOnly)
            primaryActionButtonText.text = "Read-only im Run";
        else if (canBuy)
            primaryActionButtonText.text = "Erforschen";
        else if (canActivate)
            primaryActionButtonText.text = "Aktivieren";
        else if (canDeactivate)
            primaryActionButtonText.text = "Deaktivieren";
        else if (purchased && definition.RequiresLoadoutSlot())
            primaryActionButtonText.text = state.active ? "Aktiv" : "Gekauft";
        else if (purchased)
            primaryActionButtonText.text = "Erforscht";
        else
            primaryActionButtonText.text = chaosResearch.GetNodeStateText(definition);
    }

    private void RefreshPathTechniquePrimaryActionButton(PathTechniqueProgressionManager pathTechnique, string pathTechniqueNodeId)
    {
        if (pathTechnique == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        PathTechniqueNodeDefinition definition = pathTechnique.GetDefinition(pathTechniqueNodeId);
        PathTechniqueNodeState state = pathTechnique.GetNodeState(pathTechniqueNodeId);

        if (definition == null || state == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        bool readOnly = !IsFullMetaHubAvailable(GetGameManager());
        bool purchased = state.purchased;
        bool canBuy = !readOnly && pathTechnique.CanPurchaseNode(pathTechniqueNodeId);
        bool canActivate = !readOnly && pathTechnique.CanActivateNode(pathTechniqueNodeId);
        bool canDeactivate = !readOnly && definition.RequiresLoadoutSlot() && purchased && state.active;

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivate || canDeactivate;

        if (primaryActionButtonText == null)
            return;

        if (readOnly)
            primaryActionButtonText.text = "Read-only im Run";
        else if (canBuy)
            primaryActionButtonText.text = "Kaufen";
        else if (canActivate)
            primaryActionButtonText.text = "Aktivieren";
        else if (canDeactivate)
            primaryActionButtonText.text = "Deaktivieren";
        else if (purchased && definition.RequiresLoadoutSlot())
            primaryActionButtonText.text = state.active ? "Aktiv" : "Gekauft";
        else if (purchased)
            primaryActionButtonText.text = "Freigeschaltet";
        else
            primaryActionButtonText.text = pathTechnique.GetNodeStateText(definition);
    }

    private void RefreshEliteHuntPrimaryActionButton(EliteHuntProgressionManager eliteHunt, string eliteHuntNodeId)
    {
        if (eliteHunt == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        EliteHuntNodeDefinition definition = eliteHunt.GetDefinition(eliteHuntNodeId);
        EliteHuntNodeState state = eliteHunt.GetNodeState(eliteHuntNodeId);

        if (definition == null || state == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        bool readOnly = !IsFullMetaHubAvailable(GetGameManager());
        bool purchased = state.purchased;
        bool activeModeNode = eliteHunt.IsHuntModeNode(eliteHuntNodeId) && eliteHunt.IsNodeActive(eliteHuntNodeId);
        bool canBuy = !readOnly && eliteHunt.CanPurchaseNode(eliteHuntNodeId);
        bool canActivateMode = !readOnly && eliteHunt.CanActivateHuntModeNode(eliteHuntNodeId);
        bool canDeactivateMode = !readOnly && activeModeNode;
        bool canActivate = !readOnly && eliteHunt.CanActivateNode(eliteHuntNodeId);
        bool canDeactivate = !readOnly && definition.RequiresLoadoutSlot() && purchased && state.active;

        primaryActionButton.gameObject.SetActive(true);
        primaryActionButton.interactable = canBuy || canActivateMode || canDeactivateMode || canActivate || canDeactivate;

        if (primaryActionButtonText == null)
            return;

        if (readOnly)
            primaryActionButtonText.text = "Read-only im Run";
        else if (canBuy)
            primaryActionButtonText.text = "Kaufen";
        else if (canActivateMode)
            primaryActionButtonText.text = "Modus aktivieren";
        else if (canDeactivateMode)
            primaryActionButtonText.text = "Elite aus";
        else if (canActivate)
            primaryActionButtonText.text = "Aktivieren";
        else if (canDeactivate)
            primaryActionButtonText.text = "Deaktivieren";
        else if (purchased && eliteHunt.IsHuntModeNode(eliteHuntNodeId))
            primaryActionButtonText.text = activeModeNode ? "Modus aktiv" : "Modus frei";
        else if (purchased && definition.RequiresLoadoutSlot())
            primaryActionButtonText.text = state.active ? "Aktiv" : "Gekauft";
        else if (purchased)
            primaryActionButtonText.text = "Freigeschaltet";
        else
            primaryActionButtonText.text = eliteHunt.GetNodeStateText(definition);
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

    private void RefreshSlowPrimaryActionButton(SlowTowerMasteryManager slowMastery, string slowNodeId)
    {
        if (slowMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        SlowTowerMasteryNodeDefinition definition = slowMastery.GetDefinition(slowNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = slowMastery.GetNodeRank(slowNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = slowMastery.CanPurchaseNode(slowNodeId);
        bool canActivate = maxed && definition.keystone != SlowTowerKeystone.None && slowMastery.GetActiveKeystone() != definition.keystone && slowMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = slowMastery.CanEditMetaProgression();

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
            else if (maxed && definition.keystone != SlowTowerKeystone.None && slowMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = slowMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshPoisonPrimaryActionButton(PoisonTowerMasteryManager poisonMastery, string poisonNodeId)
    {
        if (poisonMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        PoisonTowerMasteryNodeDefinition definition = poisonMastery.GetDefinition(poisonNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = poisonMastery.GetNodeRank(poisonNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = poisonMastery.CanPurchaseNode(poisonNodeId);
        bool canActivate = maxed && definition.keystone != PoisonTowerKeystone.None && poisonMastery.GetActiveKeystone() != definition.keystone && poisonMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = poisonMastery.CanEditMetaProgression();

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
            else if (maxed && definition.keystone != PoisonTowerKeystone.None && poisonMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = poisonMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshSniperPrimaryActionButton(SniperTowerMasteryManager sniperMastery, string sniperNodeId)
    {
        if (sniperMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        SniperTowerMasteryNodeDefinition definition = sniperMastery.GetDefinition(sniperNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = sniperMastery.GetNodeRank(sniperNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = sniperMastery.CanPurchaseNode(sniperNodeId);
        bool canActivate = maxed && definition.keystone != SniperTowerKeystone.None && sniperMastery.GetActiveKeystone() != definition.keystone && sniperMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = sniperMastery.CanEditMetaProgression();

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
            else if (maxed && definition.keystone != SniperTowerKeystone.None && sniperMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = sniperMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshAlchemistPrimaryActionButton(AlchemistTowerMasteryManager alchemistMastery, string alchemistNodeId)
    {
        if (alchemistMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        AlchemistTowerMasteryNodeDefinition definition = alchemistMastery.GetDefinition(alchemistNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = alchemistMastery.GetNodeRank(alchemistNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = alchemistMastery.CanPurchaseNode(alchemistNodeId);
        bool canActivate = maxed && definition.keystone != AlchemistTowerKeystone.None && alchemistMastery.GetActiveKeystone() != definition.keystone && alchemistMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = alchemistMastery.CanEditMetaProgression();

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
            else if (maxed && definition.keystone != AlchemistTowerKeystone.None && alchemistMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = alchemistMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshLightningPrimaryActionButton(LightningTowerMasteryManager lightningMastery, string lightningNodeId)
    {
        if (lightningMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        LightningTowerMasteryNodeDefinition definition = lightningMastery.GetDefinition(lightningNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = lightningMastery.GetNodeRank(lightningNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = lightningMastery.CanPurchaseNode(lightningNodeId);
        bool canActivate = maxed && definition.keystone != LightningTowerKeystone.None && lightningMastery.GetActiveKeystone() != definition.keystone && lightningMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = lightningMastery.CanEditMetaProgression();

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
            else if (maxed && definition.keystone != LightningTowerKeystone.None && lightningMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = lightningMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshMortarPrimaryActionButton(MortarTowerMasteryManager mortarMastery, string mortarNodeId)
    {
        if (mortarMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        MortarTowerMasteryNodeDefinition definition = mortarMastery.GetDefinition(mortarNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = mortarMastery.GetNodeRank(mortarNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = mortarMastery.CanPurchaseNode(mortarNodeId);
        bool canActivate = maxed && definition.keystone != MortarTowerKeystone.None && mortarMastery.GetActiveKeystone() != definition.keystone && mortarMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = mortarMastery.CanEditMetaProgression();

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
            else if (maxed && definition.keystone != MortarTowerKeystone.None && mortarMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = mortarMastery.GetNodeStateText(definition);
        }
    }

    private void RefreshSpikePrimaryActionButton(SpikeTowerMasteryManager spikeMastery, string spikeNodeId)
    {
        if (spikeMastery == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        SpikeTowerMasteryNodeDefinition definition = spikeMastery.GetDefinition(spikeNodeId);

        if (definition == null)
        {
            primaryActionButton.gameObject.SetActive(false);
            return;
        }

        int rank = spikeMastery.GetNodeRank(spikeNodeId);
        bool maxed = rank >= definition.maxRank;
        bool canBuy = spikeMastery.CanPurchaseNode(spikeNodeId);
        bool canActivate = maxed && definition.keystone != SpikeTowerKeystone.None && spikeMastery.GetActiveKeystone() != definition.keystone && spikeMastery.CanActivateKeystone(definition.keystone);
        bool canEdit = spikeMastery.CanEditMetaProgression();

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
            else if (maxed && definition.keystone != SpikeTowerKeystone.None && spikeMastery.GetActiveKeystone() == definition.keystone)
                primaryActionButtonText.text = "Keystone aktiv";
            else if (maxed)
                primaryActionButtonText.text = "Maximalrang";
            else
                primaryActionButtonText.text = spikeMastery.GetNodeStateText(definition);
        }
    }

    private void HandlePrimaryActionButton()
    {
        string generalNodeId = GetGeneralNodeIdFromEntry(selectedMetaHubEntryId);
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();

        if (!string.IsNullOrEmpty(generalNodeId) && generalMeta != null)
        {
            if (!IsFullMetaHubAvailable(GetGameManager()))
                return;

            GeneralMetaNodeDefinition definition = generalMeta.GetDefinition(generalNodeId);
            GeneralMetaNodeState state = generalMeta.GetNodeState(generalNodeId);

            if (definition == null || state == null)
                return;

            if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
                generalMeta.TryDeactivateNode(generalNodeId);
            else if (generalMeta.CanActivateNode(generalNodeId))
                generalMeta.TryActivateNode(generalNodeId);
            else
                generalMeta.TryPurchaseNode(generalNodeId);

            RefreshAll();
            return;
        }

        string chaosResearchNodeId = GetChaosResearchNodeIdFromEntry(selectedMetaHubEntryId);
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();

        if (!string.IsNullOrEmpty(chaosResearchNodeId) && chaosResearch != null)
        {
            if (!IsFullMetaHubAvailable(GetGameManager()))
                return;

            ChaosResearchNodeDefinition definition = chaosResearch.GetDefinition(chaosResearchNodeId);
            ChaosResearchNodeState state = chaosResearch.GetNodeState(chaosResearchNodeId);

            if (definition == null || state == null)
                return;

            if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
                chaosResearch.TryDeactivateNode(chaosResearchNodeId);
            else if (chaosResearch.CanActivateNode(chaosResearchNodeId))
                chaosResearch.TryActivateNode(chaosResearchNodeId);
            else
                chaosResearch.TryPurchaseNode(chaosResearchNodeId);

            RefreshAll();
            return;
        }

        string pathTechniqueNodeId = GetPathTechniqueNodeIdFromEntry(selectedMetaHubEntryId);
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();

        if (!string.IsNullOrEmpty(pathTechniqueNodeId) && pathTechnique != null)
        {
            if (!IsFullMetaHubAvailable(GetGameManager()))
                return;

            PathTechniqueNodeDefinition definition = pathTechnique.GetDefinition(pathTechniqueNodeId);
            PathTechniqueNodeState state = pathTechnique.GetNodeState(pathTechniqueNodeId);

            if (definition == null || state == null)
                return;

            if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
                pathTechnique.TryDeactivateNode(pathTechniqueNodeId);
            else if (pathTechnique.CanActivateNode(pathTechniqueNodeId))
                pathTechnique.TryActivateNode(pathTechniqueNodeId);
            else
                pathTechnique.TryPurchaseNode(pathTechniqueNodeId);

            RefreshAll();
            return;
        }

        string eliteHuntNodeId = GetEliteHuntNodeIdFromEntry(selectedMetaHubEntryId);
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();

        if (!string.IsNullOrEmpty(eliteHuntNodeId) && eliteHunt != null)
        {
            if (!IsFullMetaHubAvailable(GetGameManager()))
                return;

            EliteHuntNodeDefinition definition = eliteHunt.GetDefinition(eliteHuntNodeId);
            EliteHuntNodeState state = eliteHunt.GetNodeState(eliteHuntNodeId);

            if (definition == null || state == null)
                return;

            if (eliteHunt.IsHuntModeNode(eliteHuntNodeId) && eliteHunt.IsNodeActive(eliteHuntNodeId))
                eliteHunt.TryDeactivateHuntMode();
            else if (eliteHunt.CanActivateHuntModeNode(eliteHuntNodeId))
                eliteHunt.TryActivateHuntModeNode(eliteHuntNodeId);
            else if (definition.RequiresLoadoutSlot() && state.purchased && state.active)
                eliteHunt.TryDeactivateNode(eliteHuntNodeId);
            else if (eliteHunt.CanActivateNode(eliteHuntNodeId))
                eliteHunt.TryActivateNode(eliteHuntNodeId);
            else
                eliteHunt.TryPurchaseNode(eliteHuntNodeId);

            RefreshAll();
            return;
        }

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

        string slowNodeId = GetSlowNodeIdFromEntry(selectedMetaHubEntryId);
        SlowTowerMasteryManager slowMastery = GetSlowTowerMasteryManager();

        if (!string.IsNullOrEmpty(slowNodeId) && slowMastery != null)
        {
            SlowTowerMasteryNodeDefinition definition = slowMastery.GetDefinition(slowNodeId);

            if (definition == null)
                return;

            int rank = slowMastery.GetNodeRank(slowNodeId);

            if (rank >= definition.maxRank && definition.keystone != SlowTowerKeystone.None)
                slowMastery.TryActivateKeystone(definition.keystone);
            else
                slowMastery.TryPurchaseNode(slowNodeId);

            RefreshAll();
            return;
        }

        string poisonNodeId = GetPoisonNodeIdFromEntry(selectedMetaHubEntryId);
        PoisonTowerMasteryManager poisonMastery = GetPoisonTowerMasteryManager();

        if (!string.IsNullOrEmpty(poisonNodeId) && poisonMastery != null)
        {
            PoisonTowerMasteryNodeDefinition definition = poisonMastery.GetDefinition(poisonNodeId);

            if (definition == null)
                return;

            int rank = poisonMastery.GetNodeRank(poisonNodeId);

            if (rank >= definition.maxRank && definition.keystone != PoisonTowerKeystone.None)
                poisonMastery.TryActivateKeystone(definition.keystone);
            else
                poisonMastery.TryPurchaseNode(poisonNodeId);

            RefreshAll();
            return;
        }

        string sniperNodeId = GetSniperNodeIdFromEntry(selectedMetaHubEntryId);
        SniperTowerMasteryManager sniperMastery = GetSniperTowerMasteryManager();

        if (!string.IsNullOrEmpty(sniperNodeId) && sniperMastery != null)
        {
            SniperTowerMasteryNodeDefinition definition = sniperMastery.GetDefinition(sniperNodeId);

            if (definition == null)
                return;

            int rank = sniperMastery.GetNodeRank(sniperNodeId);

            if (rank >= definition.maxRank && definition.keystone != SniperTowerKeystone.None)
                sniperMastery.TryActivateKeystone(definition.keystone);
            else
                sniperMastery.TryPurchaseNode(sniperNodeId);

            RefreshAll();
            return;
        }

        string alchemistNodeId = GetAlchemistNodeIdFromEntry(selectedMetaHubEntryId);
        AlchemistTowerMasteryManager alchemistMastery = GetAlchemistTowerMasteryManager();

        if (!string.IsNullOrEmpty(alchemistNodeId) && alchemistMastery != null)
        {
            AlchemistTowerMasteryNodeDefinition definition = alchemistMastery.GetDefinition(alchemistNodeId);

            if (definition == null)
                return;

            int rank = alchemistMastery.GetNodeRank(alchemistNodeId);

            if (rank >= definition.maxRank && definition.keystone != AlchemistTowerKeystone.None)
                alchemistMastery.TryActivateKeystone(definition.keystone);
            else
                alchemistMastery.TryPurchaseNode(alchemistNodeId);

            RefreshAll();
            return;
        }

        string lightningNodeId = GetLightningNodeIdFromEntry(selectedMetaHubEntryId);
        LightningTowerMasteryManager lightningMastery = GetLightningTowerMasteryManager();

        if (!string.IsNullOrEmpty(lightningNodeId) && lightningMastery != null)
        {
            LightningTowerMasteryNodeDefinition definition = lightningMastery.GetDefinition(lightningNodeId);

            if (definition == null)
                return;

            int rank = lightningMastery.GetNodeRank(lightningNodeId);

            if (rank >= definition.maxRank && definition.keystone != LightningTowerKeystone.None)
                lightningMastery.TryActivateKeystone(definition.keystone);
            else
                lightningMastery.TryPurchaseNode(lightningNodeId);

            RefreshAll();
            return;
        }

        string mortarNodeId = GetMortarNodeIdFromEntry(selectedMetaHubEntryId);
        MortarTowerMasteryManager mortarMastery = GetMortarTowerMasteryManager();

        if (!string.IsNullOrEmpty(mortarNodeId) && mortarMastery != null)
        {
            MortarTowerMasteryNodeDefinition definition = mortarMastery.GetDefinition(mortarNodeId);

            if (definition == null)
                return;

            int rank = mortarMastery.GetNodeRank(mortarNodeId);

            if (rank >= definition.maxRank && definition.keystone != MortarTowerKeystone.None)
                mortarMastery.TryActivateKeystone(definition.keystone);
            else
                mortarMastery.TryPurchaseNode(mortarNodeId);

            RefreshAll();
            return;
        }

        string spikeNodeId = GetSpikeNodeIdFromEntry(selectedMetaHubEntryId);
        SpikeTowerMasteryManager spikeMastery = GetSpikeTowerMasteryManager();

        if (!string.IsNullOrEmpty(spikeNodeId) && spikeMastery != null)
        {
            SpikeTowerMasteryNodeDefinition definition = spikeMastery.GetDefinition(spikeNodeId);

            if (definition == null)
                return;

            int rank = spikeMastery.GetNodeRank(spikeNodeId);

            if (rank >= definition.maxRank && definition.keystone != SpikeTowerKeystone.None)
                spikeMastery.TryActivateKeystone(definition.keystone);
            else
                spikeMastery.TryPurchaseNode(spikeNodeId);

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

    private string GetGeneralNodeIdFromEntry(string entryId)
    {
        const string prefix = "general_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetChaosResearchNodeIdFromEntry(string entryId)
    {
        const string prefix = "chaos_research_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetPathTechniqueNodeIdFromEntry(string entryId)
    {
        const string prefix = "path_technique_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetEliteHuntNodeIdFromEntry(string entryId)
    {
        const string prefix = "elite_hunt_node_";

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

    private string GetSlowNodeIdFromEntry(string entryId)
    {
        const string prefix = "slow_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetPoisonNodeIdFromEntry(string entryId)
    {
        const string prefix = "poison_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetSniperNodeIdFromEntry(string entryId)
    {
        const string prefix = "sniper_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetAlchemistNodeIdFromEntry(string entryId)
    {
        const string prefix = "alchemist_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetLightningNodeIdFromEntry(string entryId)
    {
        const string prefix = "lightning_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetMortarNodeIdFromEntry(string entryId)
    {
        const string prefix = "mortar_node_";

        if (string.IsNullOrEmpty(entryId) || !entryId.StartsWith(prefix))
            return "";

        return entryId.Substring(prefix.Length);
    }

    private string GetSpikeNodeIdFromEntry(string entryId)
    {
        const string prefix = "spike_node_";

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
        return false;
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

    private string GetSlowMasteryStateText(SlowTowerMasteryManager slowMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (slowMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Slow);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetPoisonMasteryStateText(PoisonTowerMasteryManager poisonMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (poisonMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Poison);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetSniperMasteryStateText(SniperTowerMasteryManager sniperMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (sniperMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Sniper);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetAlchemistMasteryStateText(AlchemistTowerMasteryManager alchemistMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (alchemistMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Alchemist);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetLightningMasteryStateText(LightningTowerMasteryManager lightningMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (lightningMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Lightning);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetMortarMasteryStateText(MortarTowerMasteryManager mortarMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (mortarMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Mortar);
        return "XP " + profile.masteryXP + " | Punkte " + profile.unspentPoints + " frei | " + profile.spentPoints + " ausgegeben";
    }

    private string GetSpikeMasteryStateText(SpikeTowerMasteryManager spikeMastery)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (spikeMastery == null || towerMastery == null)
            return "System vorbereitet";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(TowerRole.Spike);
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

    private SlowTowerMasteryManager GetSlowTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetSlowTowerMasteryManager();

        return SlowTowerMasteryManager.GetOrCreate();
    }

    private PoisonTowerMasteryManager GetPoisonTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetPoisonTowerMasteryManager();

        return PoisonTowerMasteryManager.GetOrCreate();
    }

    private SniperTowerMasteryManager GetSniperTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetSniperTowerMasteryManager();

        return SniperTowerMasteryManager.GetOrCreate();
    }

    private AlchemistTowerMasteryManager GetAlchemistTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetAlchemistTowerMasteryManager();

        return AlchemistTowerMasteryManager.GetOrCreate();
    }

    private LightningTowerMasteryManager GetLightningTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetLightningTowerMasteryManager();

        return LightningTowerMasteryManager.GetOrCreate();
    }

    private MortarTowerMasteryManager GetMortarTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetMortarTowerMasteryManager();

        return MortarTowerMasteryManager.GetOrCreate();
    }

    private SpikeTowerMasteryManager GetSpikeTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetSpikeTowerMasteryManager();

        return SpikeTowerMasteryManager.GetOrCreate();
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetTowerMasteryManager();

        return TowerMasteryManager.GetOrCreate();
    }

    private GeneralMetaProgressionManager GetGeneralMetaProgressionManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetGeneralMetaProgressionManager();

        return GeneralMetaProgressionManager.GetOrCreate();
    }

    private ChaosResearchProgressionManager GetChaosResearchProgressionManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetChaosResearchProgressionManager();

        return ChaosResearchProgressionManager.GetOrCreate();
    }

    private PathTechniqueProgressionManager GetPathTechniqueProgressionManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetPathTechniqueProgressionManager();

        return PathTechniqueProgressionManager.GetOrCreate();
    }

    private EliteHuntProgressionManager GetEliteHuntProgressionManager()
    {
        GameManager gameManager = manager != null ? manager.gameManager : null;

        if (gameManager != null)
            return gameManager.GetEliteHuntProgressionManager();

        return EliteHuntProgressionManager.GetOrCreate();
    }

    private string GetGeneralAccountStateText(GeneralMetaProgressionManager generalMeta)
    {
        return generalMeta != null ? "Lv " + generalMeta.accountLevel + " | Kernwissen " + generalMeta.kernwissen : "Vorbereitet";
    }

    private string BuildGeneralAccountText(GeneralMetaProgressionManager generalMeta)
    {
        if (generalMeta == null)
            return "Account-Level, Kernwissen, Account-XP und allgemeine Meilensteine sind vorbereitet.";

        return generalMeta.GetAccountOverviewText();
    }

    private string GetGeneralCategoryStateText(GeneralMetaProgressionManager generalMeta, GeneralMetaCategory category)
    {
        if (generalMeta == null)
            return "Vorbereitet";

        int total = 0;
        int purchased = 0;

        foreach (GeneralMetaNodeDefinition definition in generalMeta.GetDefinitions())
        {
            if (definition == null || definition.category != category)
                continue;

            total++;
            if (generalMeta.IsNodePurchased(definition.nodeId))
                purchased++;
        }

        return purchased + " / " + Mathf.Max(1, total);
    }

    private string GetGeneralLoadoutStateText(GeneralMetaProgressionManager generalMeta)
    {
        if (generalMeta == null)
            return "Power begrenzen";

        return generalMeta.GetUsedLoadoutSlots() + " / " + generalMeta.GetLoadoutSlotCapacity() + " Slots";
    }

    private bool IsGeneralNodeLocked(GeneralMetaProgressionManager generalMeta, GeneralMetaNodeDefinition definition)
    {
        if (generalMeta == null || definition == null)
            return true;

        return !generalMeta.IsNodePurchased(definition.nodeId) && !generalMeta.CanPurchaseNode(definition.nodeId);
    }

    private string BuildGeneralTowerUnlocksText()
    {
        return "<b>Tower-Freischaltungen</b>\n" +
               "<size=90%><color=#B9C2D0>Content-Unlocks, immer aktiv</color></size>\n\n" +
               "Start: Basic, Rapid und Heavy.\n" +
               "Frueh: Slow, Fire und Poison.\n" +
               "Spaeter: Sniper, Alchemist, Lightning, Mortar und Spike.\n\n" +
               "Freigeschaltete Tower bleiben dauerhaft verfuegbar und brauchen keine Loadout-Slots. BuildSelectionUI nutzt diese Daten als aktiven Filter fuer den Build-Pool.";
    }

    private string BuildGeneralTileUnlocksText()
    {
        return "<b>Tile-Freischaltungen</b>\n" +
               "<size=90%><color=#B9C2D0>Content-Pool fuer PathBuild-Angebote</color></size>\n\n" +
               "Start/Common: Path Tile, Gold Tile und Slow Tile.\n" +
               "Rare: Trap Tile, Range Tile, Damage Tile, Rate Tile und Knock Tile.\n" +
               "Legendary: XP Tile, Upgrade Tile und Combo Tile.\n\n" +
               "PathBuildManager nutzt nur freigeschaltete Tile-Typen und gewichtet die Angebote nach Seltenheit; Elite-Qualitaet verschiebt die Auswahl staerker Richtung Rare/Legendary.";
    }

    private string BuildGeneralQoLText()
    {
        return "<b>Komfort / QoL</b>\n" +
               "<size=90%><color=#B9C2D0>Immer aktiv, keine Slots</color></size>\n\n" +
               "GameSpeed, Rollen-Preview, Boss-Preview, Chaos-Preview, Chaos-Wave-Erklaerung und Ziel-Pinning gehoeren hierhin.\n\n" +
               "QoL verkauft keine Macht. Es macht Runs lesbarer und die Auswertung klarer.";
    }

    private string BuildGeneralStartOptionsText()
    {
        return "<b>Startoptionen</b>\n" +
               "<size=90%><color=#B9C2D0>Power-Boni, Loadout-pflichtig</color></size>\n\n" +
               "Startgold, Startleben, Startweg, erster Tower Rabatt und Start-Scout werden gekauft und danach bewusst im Loadout aktiviert.\n\n" +
               "Wichtig: Startweg ist hart begrenzt. Gekaufte Boni wirken erst fuer den naechsten Run.";
    }

    private string BuildGeneralEnemyResearchText()
    {
        return "<b>Gegnerforschung</b>\n" +
               "<size=90%><color=#B9C2D0>Info immer aktiv, Power braucht Slots</color></size>\n\n" +
               "Info-Forschung verbessert Preview und Erklaerung fuer Runner, Tanks, Knights, Mages und Learner.\n\n" +
               "Power-Forschung bleibt klein: wenige Prozent gegen klare Rollen und mit Slot-Kosten. Das ergaenzt Tower-Mastery, ersetzt sie aber nicht.";
    }

    private string BuildGeneralLoadoutText(GeneralMetaProgressionManager generalMeta)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>Meta-Loadout</b>");

        if (generalMeta == null)
        {
            builder.AppendLine("Power-Boni werden spaeter ueber Slots begrenzt.");
            return builder.ToString();
        }

        builder.AppendLine("<size=90%><color=#B9C2D0>" + generalMeta.GetUsedLoadoutSlots() + " / " + generalMeta.GetLoadoutSlotCapacity() + " Slots belegt</color></size>");
        builder.AppendLine();
        builder.AppendLine("Content-Unlocks, Tile-Unlocks und QoL sind dauerhaft aktiv.");
        builder.AppendLine("Startoptionen und Gegner-Power-Forschung brauchen Slots.");
        builder.AppendLine("Tower-Keystones zaehlen nicht als allgemeine Slots, weil pro Tower ohnehin nur ein Keystone aktiv sein darf.");
        builder.AppendLine();
        builder.AppendLine("Aktive allgemeine Power-Boni:");

        bool any = false;
        foreach (GeneralMetaNodeDefinition definition in generalMeta.GetDefinitions())
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || !generalMeta.IsNodeActive(definition.nodeId))
                continue;

            any = true;
            builder.AppendLine("- " + definition.displayName + " [" + definition.slotCost + "]");
        }

        if (!any)
            builder.AppendLine("- Keine allgemeinen Power-Boni aktiv.");

        return builder.ToString();
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

    private string GetChaosResearchOverviewStateText(ChaosResearchProgressionManager chaosResearch)
    {
        return chaosResearch != null
            ? "CW " + chaosResearch.chaosKnowledge + " | RK " + chaosResearch.riftCores + " | Chaos " + chaosResearch.highestChaosLevelEver + "/5"
            : "Vorbereitet";
    }

    private string BuildChaosResearchOverviewText(ChaosResearchProgressionManager chaosResearch)
    {
        if (chaosResearch == null)
            return "Chaos-Wissen, Risskerne, Risiko-Pool, Varianten, Wave-Bausteine, Konter und Ordnung sind vorbereitet.";

        return chaosResearch.GetOverviewText();
    }

    private string GetChaosResearchCategoryStateText(ChaosResearchProgressionManager chaosResearch, ChaosResearchCategory category)
    {
        if (chaosResearch == null)
            return "Vorbereitet";

        return chaosResearch.GetPurchasedCount(category) + " / " + Mathf.Max(1, chaosResearch.GetDefinitionCount(category));
    }

    private bool IsChaosResearchNodeLocked(ChaosResearchProgressionManager chaosResearch, ChaosResearchNodeDefinition definition)
    {
        if (chaosResearch == null || definition == null)
            return true;

        return !chaosResearch.IsNodePurchased(definition.nodeId) && !chaosResearch.CanPurchaseNode(definition.nodeId);
    }

    private string BuildChaosRiskPoolText()
    {
        return "<b>Risiko-Pool</b>\n" +
               "<size=90%><color=#B9C2D0>Neue Chaos-Angebote, keine direkte Spieler-Power</color></size>\n\n" +
               "Dieser Bereich steuert, welche Risiko-Modifikatoren nach Boss-Waves im Angebot erscheinen koennen: Reward-Risiken, Mage-/Learner-Druck, gemischter Rollendruck, Chaos-Varianten-Druck und spaeter Wave-Block-Risiken.\n\n" +
               GetChaosUnlockRuntimeText() + "\n\n" +
               "Wichtig: Ein Risiko-Pool-Unlock erweitert die moeglichen Run-Varianten. Er macht Chaos nicht automatisch leichter.";
    }

    private string BuildChaosVariantResearchText()
    {
        return "<b>Chaos-Varianten</b>\n" +
               "<size=90%><color=#B9C2D0>Erklaeren, lesen, klein kontern</color></size>\n\n" +
               "Chaos Runner, Tank, Knight, Mage, Learner und AllRounder bekommen eigene Studien. Fruehe Forschung verbessert Lexikon, Preview und HUD-Lesbarkeit.\n\n" +
               "Keine Studie entfernt die Identitaet der Variante: Chaos-Learner bleiben Anti-Effect-Gegner, Chaos-Mage bleibt mobil und Chaos-Tank behaelt Regenerationsdruck.";
    }

    private string BuildChaosWaveResearchText()
    {
        return "<b>Chaos-Waves</b>\n" +
               "<size=90%><color=#B9C2D0>Bausteine verstehen, nicht entschaerfen</color></size>\n\n" +
               "Density, Toughness, RolePressure, ChaosVariantGroup, Rearguard, Armor, Resistance und spaeter PreviewHidden werden ueber Forschung besser erklaert und in der Preview klarer angezeigt.\n\n" +
               "Die Bausteine bleiben gefaehrlich. Forschung hilft vor allem dabei, passende Tower und Loadout-Konter zu planen.";
    }

    private string BuildChaosCounterResearchText()
    {
        return "<b>Chaos-Konter</b>\n" +
               "<size=90%><color=#B9C2D0>Kleine spezifische Power, Slot-pflichtig</color></size>\n\n" +
               "Konter wie Chaos-Runner-Analyse, Chaos-Knight-Analyse, DoT-Gegenprobe und Resistenz-Gegenprobe sind bewusst klein und brauchen Chaos-Loadout-Slots.\n\n" +
               "So kann ein spaeter Spieler nicht alle Chaos-Probleme gleichzeitig passiv loesen.";
    }

    private string BuildChaosOfferControlText()
    {
        return "<b>Angebotskontrolle</b>\n" +
               "<size=90%><color=#B9C2D0>Mehr Kontrolle, keine perfekte Deterministik</color></size>\n\n" +
               "Rerolls, weiche Risiko-Bans, Praeferenzen und eine vierte Risikokarte sind starke Komfort- und Planungswerkzeuge. Deshalb kosten sie Slots oder Risskerne.\n\n" +
               "Kein Knoten garantiert immer exakt ein bestimmtes Risiko.";
    }

    private string BuildChaosEndgameText(ChaosResearchProgressionManager chaosResearch)
    {
        string state = chaosResearch != null && chaosResearch.highestChaosLevelEver >= 5 ? "Sichtbar" : "Gesperrt bis Chaos 5";
        return "<b>Chaos-5-Endgame</b>\n" +
               "<size=90%><color=#B9C2D0>" + state + "</color></size>\n\n" +
               "Chaos 5 erzeugt Risskerne und spaete Ziele: Rissboss-Probe, Varianten-Rissprobe, Baustein-Rissprobe, Chaos halten und kosmetische Riss-Visuals.\n\n" +
               "Risskerne entstehen nur durch echte Chaos-5-Leistung und bleiben selten.";
    }

    private string BuildChaosJusticeOrderText()
    {
        return "<b>Gerechtigkeit / Ordnung</b>\n" +
               "<size=90%><color=#B9C2D0>Der sichere Gegenpol zu Chaos</color></size>\n\n" +
               "Goldene Ordnung, Lernende Ordnung, stabile Pfade und Hybrid-Ziele halten Justice langfristig interessant.\n\n" +
               "Ordnung verbessert vor allem Anzeige, Statistik und kleine sichere Meta-Boni. Sie soll Chaos nicht ersetzen, aber sichere Runs sinnvoll belohnen.";
    }

    private string GetPathTechniqueOverviewStateText(PathTechniqueProgressionManager pathTechnique)
    {
        return pathTechnique != null
            ? "Lv " + pathTechnique.pathTechniqueLevel + " | BP " + pathTechnique.blueprints + " | RBP " + pathTechnique.riftBlueprints
            : "Vorbereitet";
    }

    private string BuildPathTechniqueOverviewText(PathTechniqueProgressionManager pathTechnique)
    {
        if (pathTechnique == null)
            return "Pfadtechnik-XP, Bauplaene, Rissbauplaene, Event-Pool, Rettungsstaerke, Pfadwerkzeuge und Tile-Technik sind vorbereitet.";

        return pathTechnique.GetOverviewText();
    }

    private string GetPathTechniqueCategoryStateText(PathTechniqueProgressionManager pathTechnique, PathTechniqueCategory category)
    {
        if (pathTechnique == null)
            return "Vorbereitet";

        return pathTechnique.GetPurchasedCount(category) + " / " + Mathf.Max(1, pathTechnique.GetDefinitionCount(category));
    }

    private bool IsPathTechniqueNodeLocked(PathTechniqueProgressionManager pathTechnique, PathTechniqueNodeDefinition definition)
    {
        if (pathTechnique == null || definition == null)
            return true;

        return !pathTechnique.IsNodePurchased(definition.nodeId) && !pathTechnique.CanPurchaseNode(definition.nodeId);
    }

    private string BuildPathEventPoolText()
    {
        return "<b>Event-Pool</b>\n" +
               "<size=90%><color=#B9C2D0>Neue Verbau-Optionen, keine Sofort-Farm</color></size>\n\n" +
               "Dieser Bereich erweitert, welche Optionen bei einer echten Verbau-Krise erscheinen koennen: Baupause, Pfadscan, Notfall-Ausbildung, Evolutionsfunke, Nachschulung, Basisversatz, Teleporterbasis, Chaos ordnen und Rissanker.\n\n" +
               "Grundregel: Verbau selbst gibt keine Bauplaene. Erst Ueberleben nach der Krise zahlt aus.";
    }

    private string BuildPathEventQualityText()
    {
        return "<b>Event-Qualitaet</b>\n" +
               "<size=90%><color=#B9C2D0>Auswahl verbessern, nicht Krise entfernen</color></size>\n\n" +
               "Krisen-Vorschau, Duplikat-Schutz, Rerolls, Bann, Praeferenz und eine zusaetzliche Hilfe verbessern spaeter die Auswahl.\n\n" +
               "Reroll, Bann, Praeferenz und zusaetzliche Optionen brauchen Loadout-Slots, weil Angebotskontrolle sehr stark ist.";
    }

    private string BuildPathRescuePowerText()
    {
        return "<b>Rettungsstaerke</b>\n" +
               "<size=90%><color=#B9C2D0>Gold, Leben und Bauzeit hart gecappt</color></size>\n\n" +
               "Goldreserve, Notfall-Reparatur und Bauzeit koennen verbessert werden, bleiben aber klar begrenzt: Goldreserve max. 200 Gold, Reparatur max. 6 Leben, Timed Buildphase max. 90 Sekunden.\n\n" +
               "Nachschulung und Evolutionsfunke bleiben Run-intern und duerfen keine permanente Meta-Power erzeugen.";
    }

    private string BuildPathToolsText()
    {
        return "<b>Pfadwerkzeuge</b>\n" +
               "<size=90%><color=#B9C2D0>Planung, Vorschau und spaete Rettungswerkzeuge</color></size>\n\n" +
               "Letzte-Erweiterung-Warnung, Richtungs-Vorschau, Pfadscan, Pfadwahl-Reroll, Basisversatz und Teleporter helfen, Krisen besser zu lesen und zu loesen.\n\n" +
               "Basisversatz und Teleporter sind bewusst spaet und slotpflichtig, weil sie Pfadfehler stark abfedern koennen.";
    }

    private string BuildPathTileTechniqueText()
    {
        return "<b>Tile-Technik</b>\n" +
               "<size=90%><color=#B9C2D0>Allgemein schaltet frei, Pfadtechnik verbessert</color></size>\n\n" +
               "Trap, Gold, Slow, Knock, Range, XP, Damage, Rate, Upgrade und Combo Tiles werden hier verstaendlicher, stabiler oder etwas staerker.\n\n" +
               "Power-Tiles bleiben selten und brauchen Slots. Combo-Tiles bleiben ein spaeter Status-/Endgame-Bereich.";
    }

    private string BuildPathRiftArchitectureText(PathTechniqueProgressionManager pathTechnique)
    {
        string state = pathTechnique != null && pathTechnique.totalChaosBlockedRecoveriesEver > 0 ? "Sichtbar" : "Gesperrt bis Chaos-Verbau ueberlebt";
        return "<b>Rissarchitektur</b>\n" +
               "<size=90%><color=#B9C2D0>" + state + "</color></size>\n\n" +
               "Rissarchitektur verbindet Verbau mit Chaos-Endgame: Rissbauplaene, Chaos ordnen, Rissanker, Riss-Teleporter, Tile-Echo und Elite-Verbau-Ziele.\n\n" +
               "Rissbauplaene entstehen nur durch echte Chaos-Verbau-Leistung und bleiben stark gecappt.";
    }

    private string GetEliteHuntOverviewStateText(EliteHuntProgressionManager eliteHunt)
    {
        if (eliteHunt == null)
            return "Vorbereitet";

        return "Rang " + eliteHunt.eliteRank + " | Siegel " + eliteHunt.eliteSeals + " | " + EliteHuntProgressionManager.GetHuntModeDisplayName(eliteHunt.activeHuntMode);
    }

    private string BuildEliteHuntOverviewText(EliteHuntProgressionManager eliteHunt)
    {
        if (eliteHunt == null)
            return "Elite-Jagd ist als opt-in Endgame-System vorbereitet.";

        return eliteHunt.GetOverviewText();
    }

    private string GetEliteHuntCategoryStateText(EliteHuntProgressionManager eliteHunt, EliteHuntCategory category)
    {
        if (eliteHunt == null)
            return "Vorbereitet";

        int purchased = eliteHunt.GetPurchasedCount(category);
        int total = eliteHunt.GetDefinitionCount(category);

        if (category == EliteHuntCategory.RiftElite && !eliteHunt.IsRiftEliteVisible())
            return "Gesperrt";

        return purchased + " / " + Mathf.Max(1, total) + " freigeschaltet";
    }

    private bool IsEliteHuntNodeLocked(EliteHuntProgressionManager eliteHunt, EliteHuntNodeDefinition definition)
    {
        if (eliteHunt == null || definition == null)
            return true;

        return !eliteHunt.IsNodePurchased(definition.nodeId) && !eliteHunt.CanPurchaseNode(definition.nodeId);
    }

    private string BuildEliteContractsText()
    {
        return "<b>Elite-Auftraege</b>\n" +
               "<size=90%><color=#B9C2D0>Dauerhafte Herausforderungen, keine Daily-/Weekly-Rotation.</color></size>\n\n" +
               "Auftraege foerdern konkrete Spielstile: Runner-Jagd, Tank-/Knight-Bruch, Mage-/Learner-Antworten, Chaos-Elite, perfekte Elite-Waves, Sniper-Beteiligung, Combo-Jagd und Elite nach Verbau.\n\n" +
               "Die Knoten schalten Ziele frei; echte Auftragserfuellung bleibt fuer die spaetere Elite-Auswertung vorbereitet.";
    }

    private string BuildEliteAffixesText()
    {
        return "<b>Elite-Affixe</b>\n" +
               "<size=90%><color=#B9C2D0>Elite soll nicht nur mehr HP haben.</color></size>\n\n" +
               "Affixe wie Swift, Armored, Fortified, Regen, Resistant, Phasing, Commander, Guardian, Splitting, Chaotic und Volatile geben Elite klare Antworten und klare Risiken.\n\n" +
               "Fruehe Affixe sind lesbar. Chaotic/Volatile bleiben Riss-Endgame.";
    }

    private string BuildEliteRewardsText()
    {
        return "<b>Elite-Belohnungen</b>\n" +
               "<size=90%><color=#B9C2D0>Attraktiv, aber hart gecappt.</color></size>\n\n" +
               "Elite-Siegel sind die Hauptwaehrung. Elite-Rang-XP ist Fortschritt. Spaetere Belohnungen koennen Tower-Mastery-XP, Kernwissen, Chaos-Wissen, Pfadtechnik-XP und kosmetische Trophaeen vorbereiten.\n\n" +
               "Belohnungsboosts mit echter Power brauchen Slots.";
    }

    private string BuildEliteFrequencyText()
    {
        return "<b>Elite-Haeufigkeit</b>\n" +
               "<size=90%><color=#B9C2D0>Elite ist opt-in.</color></size>\n\n" +
               "Der aktive Modus ist Aus, Leicht, Normal, Hart oder Riss-Elite. Die Stufe selbst kostet keinen Loadout-Slot, sondern ist die freiwillige Schwierigkeit.\n\n" +
               "V1 nutzt den vorhandenen Elite-Spawn-Pfad und startet weiterhin fruehestens ab Wave 18.";
    }

    private string BuildEliteCountersText()
    {
        return "<b>Elite-Konter</b>\n" +
               "<size=90%><color=#B9C2D0>Kleine gezielte Vorteile statt harte Nerfs.</color></size>\n\n" +
               "Info-Konter verbessern Markierung, Affix-Preview, Rollenhinweise und Historie. Power-Konter geben kleine spezifische Boni gegen Elite-Rollen oder Affixe und brauchen Slots.\n\n" +
               "Elite-Affixe werden nicht entfernt und Elite-Bosse verlieren keine Mechanik.";
    }

    private string BuildEliteRiftText(EliteHuntProgressionManager eliteHunt)
    {
        string state = eliteHunt != null && eliteHunt.IsRiftEliteVisible() ? "Sichtbar" : "Gesperrt bis Chaos 5 + Elite-Erfahrung";
        return "<b>Riss-Elite / Endgame</b>\n" +
               "<size=90%><color=#B9C2D0>" + state + "</color></size>\n\n" +
               "Riss-Elite verbindet Elite mit Chaos 5: Chaotic-Affixe, Riss-Elite-Modus, seltene Risskern-Belohnungen, Tower-Meisterpruefungen und Verbau-Elite-Ziele.\n\n" +
               "Riss-Elite bleibt spaetes opt-in Endgame und darf normale Runs nicht veraendern.";
    }

    private string GetLastRunStateText()
    {
        WaveHistory history = GetWaveHistory();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        int towerPoints = GetLastRunTowerMasteryPoints(towerMastery);

        if (history != null && history.GetCompletedWaveCount() > 0)
            return "Wave " + history.GetHighestWaveNumberReached() + " | Kernwissen +" + (generalMeta != null ? generalMeta.lastRunKernwissenGained : 0) + " | Tower +" + towerPoints;

        return towerPoints > 0 ? "Tower Mastery +" + towerPoints : "Noch kein Run";
    }

    private string BuildLastRunOverviewText()
    {
        StringBuilder builder = new StringBuilder();
        WaveHistory history = GetWaveHistory();
        RunStatisticsTracker stats = GetRunStatisticsTracker();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();

        builder.AppendLine("<b>Letzter Run</b>");

        if (history != null && history.GetCompletedWaveCount() > 0)
        {
            builder.AppendLine("Wave erreicht: " + history.GetHighestWaveNumberReached());
            builder.AppendLine("Bosskills: " + history.GetBossKills() + " | MiniBoss-Waves: " + history.GetMiniBossWavesCompleted());
            builder.AppendLine("Hoechstes Chaos: " + history.GetHighestChaosLevelSeen() + " | Chaos-Waves: " + history.GetChaosWavesCompleted());
            builder.AppendLine("Kills: " + history.GetTotalKills() + " | Leaks: " + history.GetTotalLeaks() + " | Base-Schaden: " + history.GetTotalBaseDamageTaken());
        }
        else
        {
            builder.AppendLine("Noch keine abgeschlossene Run-History gespeichert.");
        }

        builder.AppendLine();
        builder.AppendLine("<b>Allgemein</b>");
        if (generalMeta != null)
        {
            builder.AppendLine("Kernwissen: +" + generalMeta.lastRunKernwissenGained);
            builder.AppendLine("Account-XP: +" + generalMeta.lastRunAccountXPGained);
            builder.AppendLine("Account-Level gewonnen: " + generalMeta.lastRunAccountLevelsGained);
        }
        else
        {
            builder.AppendLine("Kernwissen-Auszahlung vorbereitet.");
        }

        builder.AppendLine();
        AppendTowerMasteryLastRunSummary(builder, towerMastery);

        builder.AppendLine();
        builder.AppendLine("<b>Chaos-Forschung</b>");
        if (chaosResearch != null)
        {
            builder.AppendLine("Chaos-Wissen: +" + chaosResearch.lastRunChaosKnowledgeGained);
            builder.AppendLine("Risskerne: +" + chaosResearch.lastRunRiftCoresGained);
            builder.AppendLine("Chaos-Choices: " + chaosResearch.lastRunChaosChoices + " | Chaos-Waves: " + chaosResearch.lastRunChaosWavesCompleted);
            builder.AppendLine("Varianten-Kills: " + chaosResearch.lastRunChaosVariantKills + " | Block-Waves: " + chaosResearch.lastRunChaosWaveBlockWaves);
        }
        else
        {
            builder.AppendLine("Chaos-Wissen und Risskerne vorbereitet.");
        }

        builder.AppendLine();
        builder.AppendLine("<b>Pfadtechnik</b>");
        if (pathTechnique != null)
        {
            builder.AppendLine("Pfadtechnik-XP: +" + pathTechnique.lastRunPathTechniqueXPGained);
            builder.AppendLine("Bauplaene: +" + pathTechnique.lastRunBlueprintsGained);
            builder.AppendLine("Rissbauplaene: +" + pathTechnique.lastRunRiftBlueprintsGained);
            builder.AppendLine("Krisen: " + pathTechnique.lastRunBlockedCrises + " | Events: " + pathTechnique.lastRunBlockedEventsChosen + " | Waves nach Verbau: " + pathTechnique.lastRunWavesAfterBlock);
        }
        else
        {
            builder.AppendLine("Pfadtechnik-XP, Bauplaene und Rissbauplaene vorbereitet.");
        }

        builder.AppendLine();
        builder.AppendLine("<b>Elite-Jagd</b>");
        if (eliteHunt != null)
        {
            builder.AppendLine("Elite-Siegel: +" + eliteHunt.lastRunEliteSealsGained);
            builder.AppendLine("Elite-Rang-XP: +" + eliteHunt.lastRunEliteRankXPGained);
            builder.AppendLine("Gesehen: " + eliteHunt.lastRunElitesSeen + " | Besiegt: " + eliteHunt.lastRunEliteKills + " | Geleakt: " + eliteHunt.lastRunEliteLeaks);
            builder.AppendLine("Chaos-Elite: " + eliteHunt.lastRunChaosEliteKills + " | Riss-Elite: " + eliteHunt.lastRunRiftEliteKills);
        }
        else
        {
            builder.AppendLine("Elite-Siegel, Elite-Rang-XP und Jagdmodus vorbereitet.");
        }

        if (stats != null && stats.HasAnyTrackedData())
        {
            builder.AppendLine();
            builder.AppendLine("<b>Run-Auswertung</b>");
            builder.Append(stats.GetTowerProgressionSummaryText());
        }

        if (manager != null)
        {
            builder.AppendLine();
            builder.AppendLine("<b>Freischaltungen</b>");
            builder.AppendLine(manager.GetUnlockSummaryText());
            int newUnlocks = manager.GetNewlyUnlockedThisSessionCount();
            builder.AppendLine("Neue Unlocks diese Session: " + newUnlocks);
        }

        return builder.ToString();
    }

    private string GetRecommendedGoalsStateText()
    {
        int unspentTowerPoints = GetTotalUnspentTowerMasteryPoints(GetTowerMasteryManager());
        if (unspentTowerPoints > 0)
            return unspentTowerPoints + " Punkt(e) ausgeben";

        WaveHistory history = GetWaveHistory();
        if (history == null || history.GetHighestChaosLevelSeen() < 3)
            return "Chaos-3-Ziel";

        return "Ziele bereit";
    }

    private string BuildRecommendedGoalsText()
    {
        StringBuilder builder = new StringBuilder();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        WaveHistory history = GetWaveHistory();

        builder.AppendLine("<b>Empfohlene naechste Ziele</b>");

        int unspentTowerPoints = GetTotalUnspentTowerMasteryPoints(towerMastery);
        if (unspentTowerPoints > 0)
            builder.AppendLine("- Gib " + unspentTowerPoints + " Tower-Mastery-Punkt(e) aus.");

        if (towerMastery != null)
        {
            AppendTowerLevelGoal(builder, towerMastery, TowerRole.Basic, 20);
            AppendTowerLevelGoal(builder, towerMastery, TowerRole.Fire, 20);
            AppendTowerLevelGoal(builder, towerMastery, TowerRole.Heavy, 20);
        }

        if (history == null || history.GetBossKills() <= 0)
            builder.AppendLine("- Besiege den ersten Boss.");

        if (history == null || history.GetHighestChaosLevelSeen() < 3)
            builder.AppendLine("- Ueberstehe eine Chaos-3-Wave.");

        builder.AppendLine("- Ueberlebe nach einem Verbau mindestens 2 Waves.");
        builder.AppendLine("- Pruefe Milestones, sobald Punkte und Mastery-Level reichen.");
        builder.AppendLine();
        builder.AppendLine("Spaeter kann diese Liste als Ziel-Pinning ins Run-HUD wandern.");
        return builder.ToString();
    }

    private string GetProgressCardsStateText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();
        int towersWithPoints = GetTowerRolesWithUnspentPoints(towerMastery);
        int chaosLevel = chaosResearch != null ? chaosResearch.highestChaosLevelEver : 0;
        int blueprints = pathTechnique != null ? pathTechnique.blueprints : 0;
        string eliteState = eliteHunt != null ? EliteHuntProgressionManager.GetHuntModeDisplayName(eliteHunt.activeHuntMode) : "vorbereitet";
        return "Tower " + towersWithPoints + " | Chaos " + chaosLevel + "/5 | BP " + blueprints + " | Elite " + eliteState;
    }

    private string BuildProgressCardsText()
    {
        StringBuilder builder = new StringBuilder();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();
        WaveHistory history = GetWaveHistory();

        builder.AppendLine("<b>Fortschrittskarten</b>");
        builder.AppendLine(generalMeta != null
            ? "Allgemein: Lv " + generalMeta.accountLevel + " | Kernwissen " + generalMeta.kernwissen + " | Loadout " + generalMeta.GetUsedLoadoutSlots() + "/" + generalMeta.GetLoadoutSlotCapacity()
            : "Allgemein: Account-Level, Kernwissen, QoL und Startoptionen vorbereitet.");
        builder.AppendLine("Tower Mastery: " + GetTowerRolesWithUnspentPoints(towerMastery) + " Tower mit freien Punkten | " + GetActiveTowerMasteryCount(towerMastery) + " aktive TowerProfile.");

        if (chaosResearch != null)
            builder.AppendLine("Chaos-Forschung: CW " + chaosResearch.chaosKnowledge + " | RK " + chaosResearch.riftCores + " | Chaos " + chaosResearch.highestChaosLevelEver + "/5 | Loadout " + chaosResearch.GetUsedLoadoutSlots() + "/" + chaosResearch.GetLoadoutSlotCapacity());
        else if (manager != null)
            builder.AppendLine("Chaos-Forschung: " + manager.GetUnlockedCount() + " / " + Mathf.Max(1, manager.unlocks != null ? manager.unlocks.Count : 0) + " Unlocks | neu " + manager.GetNewlyUnlockedThisSessionCount());
        else
            builder.AppendLine("Chaos-Forschung: vorbereitet.");

        if (pathTechnique != null)
            builder.AppendLine("Verbau / Pfadtechnik: Lv " + pathTechnique.pathTechniqueLevel + " | BP " + pathTechnique.blueprints + " | RBP " + pathTechnique.riftBlueprints + " | Loadout " + pathTechnique.GetUsedLoadoutSlots() + "/" + pathTechnique.GetLoadoutSlotCapacity());
        else
            builder.AppendLine("Verbau / Pfadtechnik: Event-Pool, Event-Qualitaet, Rettungsstaerke, Pfadwerkzeuge und Tile-Technik vorbereitet.");

        if (eliteHunt != null)
            builder.AppendLine("Elite-Jagd: Rang " + eliteHunt.eliteRank + " | Siegel " + eliteHunt.eliteSeals + " | Modus " + EliteHuntProgressionManager.GetHuntModeDisplayName(eliteHunt.activeHuntMode) + " | Loadout " + eliteHunt.GetUsedLoadoutSlots() + "/" + eliteHunt.GetLoadoutSlotCapacity());
        else
            builder.AppendLine("Elite-Jagd: " + GetEliteHuntLockState(history));

        builder.AppendLine();
        builder.AppendLine("Regel: Content-Unlocks bleiben dauerhaft. Power-Boni werden spaeter ueber Loadout-Slots begrenzt.");
        return builder.ToString();
    }

    private string GetLoadoutStateText()
    {
        int activeKeystones = GetActiveTowerKeystoneCount(GetTowerMasteryManager());
        return activeKeystones > 0 ? activeKeystones + " Keystone(s) aktiv" : "Read-only Vorschau";
    }

    private string BuildActiveLoadoutText()
    {
        StringBuilder builder = new StringBuilder();
        GameManager currentGameManager = GetGameManager();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();

        builder.AppendLine("<b>Aktives Loadout</b>");
        builder.AppendLine(IsFullMetaHubAvailable(currentGameManager) ? "Voller Hub: Kaeufe und Keystone-Wechsel sind erlaubt." : "Laufender Run: read-only. Keine Kaeufe, keine Keystone-Wechsel, keine Loadout-Aenderungen.");
        builder.AppendLine();
        builder.AppendLine("Allgemeine Boni:");
        if (generalMeta != null)
        {
            builder.AppendLine("- Account Lv " + generalMeta.accountLevel + " | Kernwissen " + generalMeta.kernwissen);
            builder.AppendLine("- Loadout " + generalMeta.GetUsedLoadoutSlots() + " / " + generalMeta.GetLoadoutSlotCapacity() + " Slots");
            AppendActiveGeneralPowerBonuses(builder, generalMeta);
        }
        else
        {
            builder.AppendLine("- Kernwissen / Account-Level: vorbereitet");
            builder.AppendLine("- GameSpeed-QoL: vorbereitet");
            builder.AppendLine("- Startoptionen: vorbereitet, spaeter Slot-Kosten");
        }
        builder.AppendLine();
        builder.AppendLine("Aktive Tower-Keystones:");
        AppendActiveTowerKeystones(builder, towerMastery);
        builder.AppendLine();
        builder.AppendLine("Chaos:");
        if (chaosResearch != null)
        {
            builder.AppendLine("- " + chaosResearch.GetTopBarSummary());
            AppendActiveChaosResearchBonuses(builder, chaosResearch);
        }
        else
        {
            builder.AppendLine("- Risiko-Reroll und kleine Chaos-Konter vorbereitet.");
        }
        builder.AppendLine("Pfadtechnik:");
        if (pathTechnique != null)
        {
            builder.AppendLine("- " + pathTechnique.GetTopBarSummary());
            AppendActivePathTechniqueBonuses(builder, pathTechnique);
        }
        else
        {
            builder.AppendLine("- Verbau-Reroll, Event-Ban und Pfad-Reroll vorbereitet.");
        }
        builder.AppendLine("Elite:");
        if (eliteHunt != null)
        {
            builder.AppendLine("- " + eliteHunt.GetTopBarSummary());
            AppendActiveEliteHuntBonuses(builder, eliteHunt);
        }
        else
        {
            builder.AppendLine("- Elite-Jagd aus / gesperrt, bis Endgame-Bedingung erreicht ist.");
        }

        return builder.ToString();
    }

    private string GetTopBarText()
    {
        GameManager gameManager = GetGameManager();
        string accessMode = gameManager != null && gameManager.gameStarted && !gameManager.isGameOver
            ? "Read-only im laufenden Run"
            : "Voller Meta-Hub";

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaProgressionManager();
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchProgressionManager();
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueProgressionManager();
        EliteHuntProgressionManager eliteHunt = GetEliteHuntProgressionManager();
        int towerProfiles = GetActiveTowerMasteryCount(towerMastery);
        int towerPoints = GetTotalUnspentTowerMasteryPoints(towerMastery);
        int newUnlocks = manager != null ? manager.GetNewlyUnlockedThisSessionCount() : 0;

        return accessMode +
               " | " + (generalMeta != null ? "Kernwissen: " + generalMeta.kernwissen + " | Account Lv " + generalMeta.accountLevel : "Kernwissen: vorbereitet") +
               " | Tower Mastery: " + towerProfiles + " aktive Tower / " + towerPoints + " freie Punkte" +
               " | " + (chaosResearch != null ? "Chaos-Wissen: " + chaosResearch.chaosKnowledge + " | Risskerne: " + chaosResearch.riftCores : "Chaos-Forschung: vorbereitet") +
               " | " + (pathTechnique != null ? "Bauplaene: " + pathTechnique.blueprints + " | Rissbauplaene: " + pathTechnique.riftBlueprints : "Bauplaene: vorbereitet") +
               " | " + (eliteHunt != null ? "Elite-Siegel: " + eliteHunt.eliteSeals + " | Elite Rang " + eliteHunt.eliteRank : "Elite-Siegel: vorbereitet") +
               " | Loadout: " + (generalMeta != null ? generalMeta.GetUsedLoadoutSlots() + "/" + generalMeta.GetLoadoutSlotCapacity() : "vorbereitet") +
               " | Neue Unlocks: " + newUnlocks;
    }

    private GameManager GetGameManager()
    {
        if (manager != null && manager.gameManager != null)
            return manager.gameManager;

        return FindObjectOfType<GameManager>();
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

    private bool IsFullMetaHubAvailable(GameManager currentGameManager)
    {
        return currentGameManager == null || !currentGameManager.gameStarted || currentGameManager.isGameOver;
    }

    private void AppendTowerMasteryLastRunSummary(StringBuilder builder, TowerMasteryManager towerMastery)
    {
        builder.AppendLine("<b>Tower Mastery</b>");

        if (towerMastery == null)
        {
            builder.AppendLine("Tower-Mastery-System vorbereitet.");
            return;
        }

        int totalPoints = 0;
        int totalXP = 0;
        bool anyPayout = false;

        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
            if (profile == null)
                continue;

            totalPoints += Mathf.Max(0, profile.lastRunMasteryPointsGained);
            totalXP += Mathf.Max(0, profile.lastRunMasteryXPGained);

            if (profile.lastRunMasteryPointsGained <= 0 && profile.lastRunMasteryXPGained <= 0)
                continue;

            anyPayout = true;
            builder.AppendLine("- " + TowerMasteryManager.GetTowerDisplayName(role) + ": +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP | Level " + profile.lastRunHighestLevel + " | Impact " + profile.lastRunImpactScore);
        }

        if (!anyPayout)
            builder.AppendLine("Keine Tower-Mastery-Auszahlung im letzten Run gespeichert.");
        else
            builder.AppendLine("Gesamt: +" + totalPoints + " Punkt(e), +" + totalXP + " XP");
    }

    private int GetLastRunTowerMasteryPoints(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null)
            return 0;

        int total = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
            if (profile != null)
                total += Mathf.Max(0, profile.lastRunMasteryPointsGained);
        }

        return total;
    }

    private int GetTotalUnspentTowerMasteryPoints(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null)
            return 0;

        int total = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
            if (profile != null)
                total += Mathf.Max(0, profile.unspentPoints);
        }

        return total;
    }

    private int GetTowerRolesWithUnspentPoints(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null)
            return 0;

        int count = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
            if (profile != null && profile.unspentPoints > 0)
                count++;
        }

        return count;
    }

    private int GetActiveTowerMasteryCount(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null)
            return 0;

        int count = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
            if (profile == null)
                continue;

            if (profile.masteryXP > 0 || profile.unspentPoints > 0 || profile.spentPoints > 0 || profile.bestLevelEver > 1 || !string.IsNullOrEmpty(profile.activeKeystoneId))
                count++;
        }

        return count;
    }

    private int CountUnlockedTowerRoles(GeneralMetaProgressionManager generalMeta)
    {
        int count = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            if (generalMeta == null || generalMeta.IsTowerUnlocked(role))
                count++;
        }

        return count;
    }

    private int GetActiveTowerKeystoneCount(TowerMasteryManager towerMastery)
    {
        if (towerMastery == null)
            return 0;

        int count = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
            if (profile != null && !string.IsNullOrEmpty(profile.activeKeystoneId))
                count++;
        }

        return count;
    }

    private void AppendTowerLevelGoal(StringBuilder builder, TowerMasteryManager towerMastery, TowerRole role, int targetLevel)
    {
        TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
        if (profile == null || profile.bestLevelEver >= targetLevel)
            return;

        builder.AppendLine("- Bring " + TowerMasteryManager.GetTowerDisplayName(role) + " einmal auf Level " + targetLevel + ".");
    }

    private string GetEliteHuntLockState(WaveHistory history)
    {
        if (history != null && (history.GetHighestChaosLevelSeen() >= 5 || history.GetHighestWaveNumberReached() >= 30))
            return "Bedingung teilweise erfuellt, System spaeter aktivierbar.";

        return "Gesperrt, spaeteres Endgame-System.";
    }

    private void AppendActiveTowerKeystones(StringBuilder builder, TowerMasteryManager towerMastery)
    {
        if (towerMastery == null)
        {
            builder.AppendLine("- Tower-Keystone-Loadout vorbereitet.");
            return;
        }

        bool any = false;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
            if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
                continue;

            any = true;
            builder.AppendLine("- " + TowerMasteryManager.GetTowerDisplayName(role) + ": " + FormatKeystoneId(profile.activeKeystoneId));
        }

        if (!any)
            builder.AppendLine("- Keine aktiven Tower-Keystones.");
    }

    private void AppendActiveGeneralPowerBonuses(StringBuilder builder, GeneralMetaProgressionManager generalMeta)
    {
        if (generalMeta == null)
            return;

        bool any = false;

        foreach (GeneralMetaNodeDefinition definition in generalMeta.GetDefinitions())
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || !generalMeta.IsNodeActive(definition.nodeId))
                continue;

            any = true;
            builder.AppendLine("- " + definition.displayName + " [" + definition.slotCost + "]");
        }

        if (!any)
            builder.AppendLine("- Keine allgemeinen Power-Boni aktiv.");
    }

    private void AppendActiveChaosResearchBonuses(StringBuilder builder, ChaosResearchProgressionManager chaosResearch)
    {
        if (chaosResearch == null)
            return;

        bool any = false;

        foreach (ChaosResearchNodeDefinition definition in chaosResearch.GetDefinitions())
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || !chaosResearch.IsNodeActive(definition.nodeId))
                continue;

            any = true;
            builder.AppendLine("- " + definition.displayName + " [" + definition.slotCost + "]");
        }

        if (!any)
            builder.AppendLine("- Keine aktiven Chaos-Konter oder Angebotskontrollen.");
    }

    private void AppendActivePathTechniqueBonuses(StringBuilder builder, PathTechniqueProgressionManager pathTechnique)
    {
        if (pathTechnique == null)
            return;

        bool any = false;

        foreach (PathTechniqueNodeDefinition definition in pathTechnique.GetDefinitions())
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || !pathTechnique.IsNodeActive(definition.nodeId))
                continue;

            any = true;
            builder.AppendLine("- " + definition.displayName + " [" + definition.slotCost + "]");
        }

        if (!any)
            builder.AppendLine("- Keine aktiven Pfadtechnik-Power-Boni.");
    }

    private void AppendActiveEliteHuntBonuses(StringBuilder builder, EliteHuntProgressionManager eliteHunt)
    {
        if (eliteHunt == null)
            return;

        bool any = false;

        foreach (EliteHuntNodeDefinition definition in eliteHunt.GetDefinitions())
        {
            if (definition == null || !definition.RequiresLoadoutSlot() || !eliteHunt.IsNodeActive(definition.nodeId))
                continue;

            any = true;
            builder.AppendLine("- " + definition.displayName + " [" + definition.slotCost + "]");
        }

        if (!any)
            builder.AppendLine("- Keine aktiven Elite-Power-Boni.");
    }

    private string FormatKeystoneId(string keystoneId)
    {
        if (string.IsNullOrEmpty(keystoneId))
            return "Kein Keystone";

        return keystoneId.Replace("_", " ").Replace("-", " ");
    }

    private string GetSectionDisplayName(ChaosUnlockMenuSection section)
    {
        switch (section)
        {
            case ChaosUnlockMenuSection.Overview:
                return "Uebersicht";
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
        if (!autoCreateUIIfMissing)
            return;

        bool hasDashboardLayout = rootPanel != null &&
                                  resourceChipContent != null &&
                                  mainContent != null &&
                                  rightColumnContent != null &&
                                  sectionButtonContent != null;

        if (hasDashboardLayout)
            return;

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (targetCanvas == null)
            return;

        if (rootPanel != null)
        {
            Destroy(rootPanel);
            rootPanel = null;
            titleText = null;
            summaryText = null;
            detailText = null;
            sectionButtonContent = null;
            entryListContent = null;
            resourceChipContent = null;
            mainContent = null;
            rightColumnContent = null;
            activeKeystoneContent = null;
            bottomHintText = null;
            closeButton = null;
            refreshButton = null;
            primaryActionButton = null;
            primaryActionButtonText = null;
            accountXPFillRect = null;
            accountXPBarText = null;
        }

        CreateAutoUI();
    }

    private void CreateAutoUI()
    {
        if (useIndustrialMetaHubTheme)
            ApplyIndustrialMetaHubTheme();

        GameObject overlay = new GameObject("MetaHubOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(targetCanvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;
        rootPanel = overlay;

        GameObject window = CreatePanel(overlay.transform, "MetaHubWindow", windowColor, Vector2.zero);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.02f, 0.04f);
        windowRect.anchorMax = new Vector2(0.98f, 0.96f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;

        AddEdgeFrame(window.transform, new Color32(216, 151, 45, 255), 2f);
        CreateDashboardTopBar(window.transform);
        CreateDashboardSidebar(window.transform);
        CreateDashboardMainContent(window.transform);
        CreateDashboardRightColumn(window.transform);
        CreateDashboardBottomBar(window.transform);
        CreateNotification(overlay.transform);
    }

    private void CreateDashboardTopBar(Transform parent)
    {
        GameObject topBar = CreateAnchoredPanel(parent, "MetaHubTopResourceBar", new Color32(7, 16, 20, 248), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -80f), Vector2.zero);
        AddEdgeFrame(topBar.transform, headerColor, 2f);
        AddBottomLine(topBar.transform, new Color32(48, 34, 16, 255), 3f);

        GameObject chipRow = CreateAnchoredPanel(topBar.transform, "ResourceChipRow", new Color32(0, 0, 0, 0), new Vector2(0f, 0f), new Vector2(0.50f, 1f), new Vector2(318f, 10f), new Vector2(-10f, -10f));
        HorizontalLayoutGroup chipLayout = chipRow.AddComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 6f;
        chipLayout.childAlignment = TextAnchor.MiddleLeft;
        chipLayout.childControlWidth = true;
        chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = false;
        chipLayout.childForceExpandHeight = true;
        resourceChipContent = chipRow.transform;

        titleText = CreateText(topBar.transform, "MetaHubTitle", 28f, TextAlignmentOptions.Center, textColor);
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        titleText.characterSpacing = 8f;
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.43f, 0f);
        titleRect.anchorMax = new Vector2(0.69f, 1f);
        titleRect.offsetMin = new Vector2(0f, 4f);
        titleRect.offsetMax = new Vector2(0f, -4f);

        GameObject accountPanel = CreateAnchoredPanel(topBar.transform, "AccountPanel", new Color32(10, 17, 22, 160), new Vector2(0.69f, 0f), new Vector2(0.93f, 1f), new Vector2(0f, 12f), new Vector2(-10f, -12f));
        AddEdgeFrame(accountPanel.transform, new Color32(73, 55, 28, 255), 1f);

        summaryText = CreateText(accountPanel.transform, "AccountSummary", 14f, TextAlignmentOptions.MidlineLeft, textColor);
        summaryText.enableWordWrapping = false;
        summaryText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(summaryText.rectTransform, 12f, 4f, 12f, 24f);

        GameObject xpBar = CreateAnchoredPanel(accountPanel.transform, "AccountXPBar", new Color32(24, 28, 38, 255), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 10f), new Vector2(-12f, 22f));
        AddEdgeFrame(xpBar.transform, new Color32(76, 88, 116, 255), 1f);
        GameObject xpFill = CreateDecoration(xpBar.transform, "AccountXPFill", headerColor);
        accountXPFillRect = xpFill.GetComponent<RectTransform>();
        accountXPFillRect.anchorMin = Vector2.zero;
        accountXPFillRect.anchorMax = new Vector2(0f, 1f);
        accountXPFillRect.offsetMin = Vector2.zero;
        accountXPFillRect.offsetMax = Vector2.zero;

        accountXPBarText = CreateText(xpBar.transform, "AccountXPText", 10f, TextAlignmentOptions.Center, textColor);
        accountXPBarText.enableWordWrapping = false;
        accountXPBarText.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(accountXPBarText.rectTransform);

        refreshButton = CreateSmallButton(topBar.transform, "OptionsButton", "O", buttonColor, new Vector2(-64f, -18f));
        closeButton = CreateSmallButton(topBar.transform, "CloseButton", "X", closeButtonColor, new Vector2(-18f, -18f));
    }

    private void CreateDashboardSidebar(Transform parent)
    {
        GameObject sidebar = CreateAnchoredPanel(parent, "MetaHubSidebar", new Color32(8, 14, 18, 248), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 64f), new Vector2(306f, -8f));
        AddEdgeFrame(sidebar.transform, headerColor, 2f);

        TextMeshProUGUI sidebarTitle = CreateText(sidebar.transform, "SidebarTitle", 22f, TextAlignmentOptions.Center, headerColor);
        sidebarTitle.text = "META-HUB";
        sidebarTitle.fontStyle = FontStyles.Bold;
        sidebarTitle.enableWordWrapping = false;
        sidebarTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        sidebarTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        sidebarTitle.rectTransform.offsetMin = new Vector2(16f, -66f);
        sidebarTitle.rectTransform.offsetMax = new Vector2(-16f, -18f);

        GameObject navObject = new GameObject("MetaHubSidebarButtons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        navObject.transform.SetParent(sidebar.transform, false);
        sectionButtonContent = navObject.transform;

        RectTransform navRect = navObject.GetComponent<RectTransform>();
        SetOffsets(navRect, 14f, 78f, 14f, 198f);

        VerticalLayoutGroup navLayout = navObject.GetComponent<VerticalLayoutGroup>();
        navLayout.padding = new RectOffset(0, 0, 0, 0);
        navLayout.spacing = 12f;
        navLayout.childAlignment = TextAnchor.UpperCenter;
        navLayout.childControlWidth = true;
        navLayout.childControlHeight = true;
        navLayout.childForceExpandWidth = true;
        navLayout.childForceExpandHeight = true;

        GameObject keystonePanel = CreateAnchoredPanel(sidebar.transform, "ActiveKeystonePanel", new Color32(10, 17, 22, 230), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 18f), new Vector2(-14f, 178f));
        AddEdgeFrame(keystonePanel.transform, new Color32(105, 76, 34, 255), 1f);

        TextMeshProUGUI keystoneTitle = CreateText(keystonePanel.transform, "KeystoneTitle", 16f, TextAlignmentOptions.TopLeft, headerColor);
        keystoneTitle.text = "AKTIVE KEYSTONES";
        keystoneTitle.fontStyle = FontStyles.Bold;
        keystoneTitle.enableWordWrapping = false;
        keystoneTitle.overflowMode = TextOverflowModes.Ellipsis;
        keystoneTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        keystoneTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        keystoneTitle.rectTransform.offsetMin = new Vector2(14f, -48f);
        keystoneTitle.rectTransform.offsetMax = new Vector2(-14f, -14f);

        GameObject keystoneRow = CreateAnchoredPanel(keystonePanel.transform, "KeystoneSlots", new Color32(0, 0, 0, 0), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 16f), new Vector2(-12f, 102f));
        HorizontalLayoutGroup rowLayout = keystoneRow.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        activeKeystoneContent = keystoneRow.transform;
    }

    private void CreateDashboardMainContent(Transform parent)
    {
        GameObject content = CreateAnchoredPanel(parent, "MetaHubMainContent", new Color32(0, 0, 0, 0), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(326f, 64f), new Vector2(-360f, -92f));
        mainContent = content.transform;
    }

    private void CreateDashboardRightColumn(Transform parent)
    {
        GameObject rightColumn = CreateAnchoredPanel(parent, "MetaHubRightColumn", new Color32(0, 0, 0, 0), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-344f, 64f), new Vector2(-16f, -92f));
        rightColumnContent = rightColumn.transform;
    }

    private void CreateDashboardBottomBar(Transform parent)
    {
        GameObject bottom = CreateAnchoredPanel(parent, "MetaHubBottomBar", new Color32(7, 13, 18, 248), new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 58f));
        AddEdgeFrame(bottom.transform, new Color32(73, 55, 28, 255), 1f);

        bottomHintText = CreateText(bottom.transform, "BottomHint", 13f, TextAlignmentOptions.MidlineLeft, mutedGray);
        bottomHintText.enableWordWrapping = false;
        bottomHintText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(bottomHintText.rectTransform, 18f, 8f, 500f, 8f);

        primaryActionButton = CreateAnchoredButton(bottom.transform, "MetaHubPrimaryActionButton", "Kaufen", unlockedButtonColor, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(160f, 38f));
        primaryActionButtonText = primaryActionButton.GetComponentInChildren<TextMeshProUGUI>(true);
        primaryActionButton.gameObject.SetActive(false);

        Button backButton = CreateAnchoredButton(bottom.transform, "MetaHubBottomBackButton", "ZURUECK", buttonColor, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(170f, 38f));
        backButton.onClick.AddListener(CloseFromButton);
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
        AddEdgeFrame(header.transform, new Color32(72, 48, 20, 255), 2f);
        AddBottomLine(header.transform, new Color32(35, 28, 18, 255), 6f);
        AddIndustrialStripe(header.transform, "HeaderHazardStripe", new Color32(45, 34, 18, 255), new Color32(255, 206, 84, 255));

        titleText = CreateText(header.transform, "TitleText", titleFontSize + 2f, TextAlignmentOptions.MidlineLeft, Color.white);
        titleText.fontStyle = FontStyles.Bold;
        SetOffsets(titleText.rectTransform, 30f, 8f, 190f, 12f);

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
        AddEdgeFrame(summaryPanel.transform, new Color32(50, 55, 54, 255), 1f);
        AddVerticalAccent(summaryPanel.transform, headerColor, 5f);

        summaryText = CreateText(summaryPanel.transform, "SummaryText", summaryFontSize, TextAlignmentOptions.MidlineLeft, textColor);
        summaryText.enableWordWrapping = false;
        summaryText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(summaryText.rectTransform, 18f, 3f, 12f, 3f);
    }

    private void CreateNavigation(Transform parent)
    {
        GameObject navigationPanel = CreatePanel(parent, "MetaHubNavigationPanel", listPanelColor, Vector2.zero);
        RectTransform navigationRect = navigationPanel.GetComponent<RectTransform>();
        navigationRect.anchorMin = new Vector2(0f, 0f);
        navigationRect.anchorMax = new Vector2(0f, 1f);
        navigationRect.pivot = new Vector2(0f, 0.5f);
        navigationRect.offsetMin = new Vector2(24f, 82f);
        navigationRect.offsetMax = new Vector2(236f, -146f);
        AddEdgeFrame(navigationPanel.transform, new Color32(54, 60, 58, 255), 2f);

        sectionButtonContent = CreateContentWithLayout(navigationPanel.transform, "MetaHubNavigationContent");
    }

    private void CreateEntryList(Transform parent)
    {
        GameObject listPanel = CreatePanel(parent, "MetaHubEntryListPanel", listPanelColor, Vector2.zero);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0f, 1f);
        listRect.pivot = new Vector2(0f, 0.5f);
        listRect.offsetMin = new Vector2(24f, 82f);
        listRect.offsetMax = new Vector2(400f, -146f);
        AddEdgeFrame(listPanel.transform, new Color32(54, 60, 58, 255), 2f);

        entryListContent = CreateScrollableContentWithLayout(listPanel.transform, "MetaHubEntryListContent");
    }

    private void CreateDetailPanel(Transform parent)
    {
        GameObject detailPanel = CreatePanel(parent, "MetaHubDetailPanel", detailPanelColor, Vector2.zero);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(420f, 82f);
        detailRect.offsetMax = new Vector2(-24f, -146f);
        AddEdgeFrame(detailPanel.transform, new Color32(54, 60, 58, 255), 2f);
        AddVerticalAccent(detailPanel.transform, headerColor, 6f);

        detailScrollRect = detailPanel.AddComponent<ScrollRect>();
        detailScrollRect.horizontal = false;
        detailScrollRect.vertical = true;
        detailScrollRect.movementType = ScrollRect.MovementType.Clamped;
        detailScrollRect.scrollSensitivity = 24f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(detailPanel.transform, false);
        SetOffsets(viewport.GetComponent<RectTransform>(), 18f, 18f, 18f, 18f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 6);
        viewportImage.raycastTarget = true;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("MetaHubDetailContent", typeof(RectTransform));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 2600f);

        detailScrollRect.viewport = viewport.GetComponent<RectTransform>();
        detailScrollRect.content = contentRect;

        detailText = CreateText(contentObject.transform, "DetailText", detailFontSize, TextAlignmentOptions.TopLeft, textColor);
        detailText.enableWordWrapping = true;
        detailText.overflowMode = TextOverflowModes.Overflow;
        detailText.margin = Vector4.zero;
        SetOffsets(detailText.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void CreateFooterHint(Transform parent)
    {
        TextMeshProUGUI footerText = CreateText(parent, "MetaHubFooterHint", 14f, TextAlignmentOptions.Center, new Color32(184, 178, 160, 255));
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
        primaryActionButton = CreateAnchoredButton(parent, "MetaHubPrimaryActionButton", "Kaufen", unlockedButtonColor, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 28f), new Vector2(220f, 46f));
        primaryActionButtonText = primaryActionButton.GetComponentInChildren<TextMeshProUGUI>(true);
        primaryActionButton.gameObject.SetActive(false);
    }

    private void ApplyIndustrialMetaHubTheme()
    {
        overlayColor = new Color32(3, 6, 8, 232);
        windowColor = new Color32(11, 17, 24, 252);
        headerColor = new Color32(214, 164, 65, 255);
        listPanelColor = new Color32(8, 14, 18, 248);
        detailPanelColor = new Color32(16, 25, 35, 248);
        buttonColor = new Color32(16, 25, 35, 255);
        unlockedButtonColor = new Color32(165, 70, 255, 255);
        lockedButtonColor = new Color32(74, 82, 96, 255);
        closeButtonColor = new Color32(225, 75, 75, 255);
        textColor = new Color32(240, 244, 250, 255);
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
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 10f;
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

    private GameObject CreateDecoration(Transform parent, string objectName, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private void AddEdgeFrame(Transform parent, Color color, float thickness)
    {
        if (parent == null || thickness <= 0f)
            return;

        AddEdgeLine(parent, "FrameTop", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        AddEdgeLine(parent, "FrameBottom", color, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        AddEdgeLine(parent, "FrameLeft", color, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        AddEdgeLine(parent, "FrameRight", color, new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
    }

    private void AddEdgeLine(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject line = CreateDecoration(parent, objectName, color);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void AddVerticalAccent(Transform parent, Color color, float width)
    {
        GameObject accent = CreateDecoration(parent, "VerticalAccent", color);
        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(width, 0f);
    }

    private void AddBottomLine(Transform parent, Color color, float height)
    {
        GameObject line = CreateDecoration(parent, "BottomLine", color);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0f, height);
    }

    private void AddIndustrialStripe(Transform parent, string objectName, Color baseColor, Color accentColor)
    {
        GameObject stripe = CreateDecoration(parent, objectName, baseColor);
        RectTransform stripeRect = stripe.GetComponent<RectTransform>();
        stripeRect.anchorMin = new Vector2(0f, 1f);
        stripeRect.anchorMax = new Vector2(1f, 1f);
        stripeRect.offsetMin = new Vector2(0f, -8f);
        stripeRect.offsetMax = Vector2.zero;

        for (int i = 0; i < 16; i++)
        {
            GameObject dash = CreateDecoration(stripe.transform, "HazardDash_" + i, accentColor);
            RectTransform dashRect = dash.GetComponent<RectTransform>();
            dashRect.anchorMin = new Vector2(0f, 0.5f);
            dashRect.anchorMax = new Vector2(0f, 0.5f);
            dashRect.pivot = new Vector2(0.5f, 0.5f);
            dashRect.anchoredPosition = new Vector2(28f + i * 72f, 0f);
            dashRect.sizeDelta = new Vector2(34f, 8f);
            dashRect.localEulerAngles = new Vector3(0f, 0f, -28f);
        }
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
        AddEdgeFrame(buttonObject.transform, new Color32(38, 31, 25, 255), 2f);

        Button button = buttonObject.GetComponent<Button>();
        ApplySelectableColors(button, color, headerColor);
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
        AddEdgeFrame(buttonObject.transform, new Color32(38, 31, 25, 255), 2f);
        AddVerticalAccent(buttonObject.transform, headerColor, 6f);

        Button button = buttonObject.GetComponent<Button>();
        ApplySelectableColors(button, color, headerColor);
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
