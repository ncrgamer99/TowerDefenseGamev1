using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChaosUnlockRuntimeUI : MonoBehaviour
{
    private enum RuntimeSection
    {
        Overview,
        Allgemein,
        TowerMastery,
        ChaosForschung,
        Pfadtechnik,
        EliteJagd,
        Grundlagen,
        RisikoPool,
        ChaosVarianten,
        ChaosWaves,
        Gerechtigkeit,
        Auswertung,
        Zukunft
    }

    [Header("References")]
    public ChaosUnlockManager manager;
    public Canvas targetCanvas;

    [Header("UI")]
    public GameObject rootPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI summaryText;
    public TextMeshProUGUI detailText;
    public Transform entryListContent;
    public Transform resourceChipContent;
    public Transform sidebarContent;
    public Transform mainContent;
    public Transform rightColumnContent;
    public Button closeButton;
    public Button refreshButton;
    public Button mainMenuButton;
    public MetaHubController metaHubController;
    public bool redirectGameplayToMetaHub = true;

    [Header("Notification")]
    public GameObject notificationPanel;
    public TextMeshProUGUI notificationText;
    public float notificationDuration = 3f;

    [Header("Auto Create")]
    public bool autoCreateUIIfMissing = true;

    [Header("Theme")]
    public Color overlayColor = new Color32(3, 6, 8, 226);
    public Color windowColor = new Color32(7, 13, 18, 252);
    public Color panelColor = new Color32(11, 17, 24, 248);
    public Color cardColor = new Color32(16, 25, 35, 248);
    public Color selectedCardColor = new Color32(52, 38, 18, 255);
    public Color accentColor = new Color32(214, 164, 65, 255);
    public Color lockedAccentColor = new Color32(74, 82, 96, 255);
    public Color closeButtonColor = new Color32(225, 75, 75, 255);
    public Color textColor = new Color32(240, 244, 250, 255);
    public Color mutedTextColor = new Color32(185, 194, 208, 255);
    public Color chaosAccentColor = new Color32(185, 70, 95, 255);
    public Color purpleAccentColor = new Color32(165, 70, 255, 255);
    public Color pathAccentColor = new Color32(58, 175, 234, 255);
    public Color successAccentColor = new Color32(74, 219, 123, 255);

    private readonly List<Button> generatedButtons = new List<Button>();
    private readonly List<Button> generatedSidebarButtons = new List<Button>();
    private RuntimeSection selectedSection = RuntimeSection.Overview;
    private string selectedUnlockId = "";
    private float notificationTimer = 0f;

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private void Awake()
    {
        redirectGameplayToMetaHub = true;
    }

    private void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<ChaosUnlockManager>();

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (manager != null)
            manager.gameplayUnlockUI = this;

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

        if (manager != null && targetCanvas == null)
            targetCanvas = manager.targetCanvas;

        EnsureUI();
        SetupButtons();
        CloseUnlocks();
    }

    public void OpenUnlocks()
    {
        if (ShouldRedirectGameplayToMetaHub())
        {
            OpenMetaHubInstead();
            return;
        }

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

        if (metaHubController != null)
            metaHubController.CloseFromUnlockManager();
    }

    private bool ShouldRedirectGameplayToMetaHub()
    {
        if (!redirectGameplayToMetaHub)
            return false;

        GameManager gameManager = GetGameManager();
        return gameManager != null && !gameManager.startMenuOpen && !gameManager.isGameOver;
    }

    private void OpenMetaHubInstead()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);

        GameManager gameManager = GetGameManager();
        metaHubController = MetaHubController.CreateRuntimeInstance(gameManager);

        if (manager != null)
            manager.metaHubController = metaHubController;

        if (metaHubController == null)
        {
            Debug.LogError("ChaosUnlockRuntimeUI: MetaHubController konnte nicht erstellt werden.");
            return;
        }

        metaHubController.OpenFromUnlockManager(manager, gameManager);
    }

    public void RefreshAll()
    {
        EnsureUI();
        RefreshTopBar();
        RefreshSidebar();
        RefreshContent();
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
            refreshButton.gameObject.SetActive(false);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(ReturnToMainMenuFromButton);
        }
    }

    private void CloseFromButton()
    {
        if (manager != null)
            manager.CloseUnlocks();
        else
            CloseUnlocks();
    }

    private void ReturnToMainMenuFromButton()
    {
        GameManager gameManager = manager != null && manager.gameManager != null
            ? manager.gameManager
            : FindObjectOfType<GameManager>();

        if (gameManager != null)
        {
            gameManager.AbortRunAndReturnToStartMenu();
            return;
        }

        CloseFromButton();
    }

    private void RefreshTopBar()
    {
        if (resourceChipContent != null)
        {
            ClearChildren(resourceChipContent);
            int total = manager != null && manager.unlocks != null ? manager.unlocks.Count : 0;
            int unlocked = GetUnlockedCount();
            GeneralMetaProgressionManager generalMeta = GetGeneralMetaManager();
            ChaosResearchProgressionManager chaosResearch = GetChaosResearchManager();
            PathTechniqueProgressionManager pathTechnique = GetPathTechniqueManager();
            EliteHuntProgressionManager eliteHunt = GetEliteHuntManager();

            CreateResourceChip(resourceChipContent, "K", "Kernwissen", FormatNumber(generalMeta != null ? generalMeta.kernwissen : unlocked), accentColor);
            CreateResourceChip(resourceChipContent, "XP", "Account-XP", FormatNumber(generalMeta != null ? generalMeta.accountXP : total), pathAccentColor);
            CreateResourceChip(resourceChipContent, "C", "Chaos-Wissen", FormatNumber(chaosResearch != null ? chaosResearch.chaosKnowledge : GetCurrentChaosLevel()), purpleAccentColor);
            CreateResourceChip(resourceChipContent, "R", "Risskerne", FormatNumber(chaosResearch != null ? chaosResearch.riftCores : 0), chaosAccentColor);
            CreateResourceChip(resourceChipContent, "B", "Bauplaene", FormatNumber(pathTechnique != null ? pathTechnique.blueprints : 0), pathAccentColor);
            CreateResourceChip(resourceChipContent, "E", "Elite-Siegel", FormatNumber(eliteHunt != null ? eliteHunt.eliteSeals : 0), chaosAccentColor);
        }

        if (titleText != null)
        {
            titleText.text = "TOWER DEFENSE\n<size=45%><color=#D6A441>FREISCHALTUNGEN / RUN-ANSICHT</color></size>";
        }

        if (summaryText != null)
        {
            string phase = manager != null && manager.gameManager != null ? manager.gameManager.currentPhase.ToString() : "Read-only";
            int wave = GetCurrentWaveNumber();
            summaryText.text = "Ingame Esc\n<size=70%><color=#B9C2D0>Wave " + wave + " | " + phase + "</color></size>";
        }
    }

    private void RefreshSidebar()
    {
        if (sidebarContent == null)
            return;

        ClearChildren(sidebarContent);
        generatedSidebarButtons.Clear();

        CreateSidebarSectionLabel("META-PROGRESSION");
        generatedSidebarButtons.Add(CreateSidebarButton(RuntimeSection.Overview, "U", "UEBERSICHT", accentColor));
        generatedSidebarButtons.Add(CreateSidebarButton(RuntimeSection.Allgemein, "A", "ALLGEMEIN", accentColor));
        generatedSidebarButtons.Add(CreateSidebarButton(RuntimeSection.TowerMastery, "T", "TOWER MASTERY", purpleAccentColor));
        generatedSidebarButtons.Add(CreateSidebarButton(RuntimeSection.ChaosForschung, "C", "CHAOS-FORSCHUNG", chaosAccentColor));
        generatedSidebarButtons.Add(CreateSidebarButton(RuntimeSection.Pfadtechnik, "P", "PFADTECHNIK", pathAccentColor));
        generatedSidebarButtons.Add(CreateSidebarButton(RuntimeSection.EliteJagd, "E", "ELITE-JAGD", chaosAccentColor));
    }

    private void RefreshContent()
    {
        if (mainContent == null || rightColumnContent == null)
            return;

        ClearChildren(mainContent);
        ClearChildren(rightColumnContent);
        generatedButtons.Clear();

        switch (selectedSection)
        {
            case RuntimeSection.Overview:
                RenderOverview();
                break;
            case RuntimeSection.Allgemein:
            case RuntimeSection.TowerMastery:
            case RuntimeSection.ChaosForschung:
            case RuntimeSection.Pfadtechnik:
            case RuntimeSection.EliteJagd:
                RenderMetaSectionView(selectedSection);
                break;
            default:
                RenderCategoryView(selectedSection);
                break;
        }
    }

    private void RenderOverview()
    {
        AddSectionHeading(mainContent, "UEBERSICHT", "Read-only Freischaltungen, Chaoswissen und Run-Ziele", accentColor);

        GameObject cardsRow = CreateAnchoredPanel(mainContent, "MetricCards", new Color32(0, 0, 0, 0), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -224f), new Vector2(0f, -90f));
        HorizontalLayoutGroup cardLayout = cardsRow.AddComponent<HorizontalLayoutGroup>();
        cardLayout.spacing = 14f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = true;

        int towerActive = GetTowerRoleCount();
        int towerMastered = GetTowerMilestoneCount();
        CreateMetricCard(cardsRow.transform, "TOWER MASTERY", towerActive.ToString(), "Aktive Tower", towerMastered + " / " + Mathf.Max(1, towerActive) + " Mastery", towerActive > 0 ? towerMastered / (float)towerActive : 0f, purpleAccentColor, "T");

        ChaosResearchProgressionManager chaosResearch = GetChaosResearchManager();
        int chaosPurchased = chaosResearch != null ? GetPurchasedChaosResearchCount() : CountUnlockedByCategory(ChaosUnlockCategory.RisikoPool);
        int chaosTotal = chaosResearch != null ? GetChaosResearchDefinitionCount() : Mathf.Max(1, GetCategoryTotal(ChaosUnlockCategory.RisikoPool));
        CreateMetricCard(cardsRow.transform, "CHAOS-WISSEN", (chaosResearch != null ? chaosResearch.chaosKnowledge : 0).ToString(), "Forschungsstufen", chaosPurchased + " / " + chaosTotal, chaosTotal > 0 ? chaosPurchased / (float)chaosTotal : 0f, purpleAccentColor, "C");

        int riftCores = chaosResearch != null ? chaosResearch.riftCores : 0;
        CreateMetricCard(cardsRow.transform, "RISIKOKERNE", riftCores.ToString(), "Aktive Kerne", GetHighestChaosLevel() + " / 5 Chaos", Mathf.Clamp01(GetHighestChaosLevel() / 5f), chaosAccentColor, "R");

        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueManager();
        int pathPurchased = pathTechnique != null ? GetPurchasedPathTechniqueCount() : 0;
        int pathTotal = pathTechnique != null ? GetPathTechniqueDefinitionCount() : 1;
        CreateMetricCard(cardsRow.transform, "BAUPLAENE", (pathTechnique != null ? pathTechnique.blueprints : 0).ToString(), "Freigeschaltet", pathPurchased + " / " + pathTotal, pathTotal > 0 ? pathPurchased / (float)pathTotal : 0f, pathAccentColor, "B");

        EliteHuntProgressionManager eliteHunt = GetEliteHuntManager();
        WaveHistory history = GetWaveHistory();
        int eliteKills = eliteHunt != null ? eliteHunt.totalEliteKillsEver : history != null ? history.GetEliteKills() : 0;
        int eliteRank = eliteHunt != null ? eliteHunt.eliteRank : 1;
        CreateMetricCard(cardsRow.transform, "ELITE-JAGD", eliteKills.ToString(), "Elite besiegt", "Rang " + eliteRank, Mathf.Clamp01(eliteRank / 35f), chaosAccentColor, "E");

        GameObject progressPanel = CreateDashboardPanel(mainContent, "ProgressPanel", "FORTSCHRITT", accentColor, new Vector2(0f, 0.32f), new Vector2(0.42f, 0.70f), Vector2.zero, new Vector2(-8f, -10f));
        RenderProgressPanel(progressPanel.transform);

        GameObject balancePanel = CreateDashboardPanel(mainContent, "BalancePanel", "CHAOS / GERECHTIGKEIT", accentColor, new Vector2(0.43f, 0.32f), new Vector2(1f, 0.70f), new Vector2(8f, 0f), new Vector2(0f, -10f));
        RenderChaosJusticePanel(balancePanel.transform);

        GameObject buffPanel = CreateDashboardPanel(mainContent, "BuffPanel", "AKTIVE BUFFS", successAccentColor, new Vector2(0f, 0f), new Vector2(0.49f, 0.29f), Vector2.zero, new Vector2(-8f, 0f));
        CreateStatusRow(buffPanel.transform, "Gold-Gerechtigkeit", "Reward x" + GetGoldRewardMultiplierText(), GetHighestGoldJustice() + " Stufen", successAccentColor);
        CreateStatusRow(buffPanel.transform, "XP-Gerechtigkeit", "Reward x" + GetXpRewardMultiplierText(), GetHighestXpJustice() + " Stufen", successAccentColor);

        GameObject riskPanel = CreateDashboardPanel(mainContent, "RiskPanel", "AKTIVE RISIKEN", chaosAccentColor, new Vector2(0.50f, 0f), new Vector2(1f, 0.29f), new Vector2(8f, 0f), Vector2.zero);
        RenderActiveRiskRows(riskPanel.transform);

        RenderRightColumnOverview();
    }

    private void RenderProgressPanel(Transform parent)
    {
        GeneralMetaProgressionManager generalMeta = GetGeneralMetaManager();
        int accountLevel = generalMeta != null ? Mathf.Max(1, generalMeta.accountLevel) : 1;
        int xpIntoLevel = generalMeta != null ? generalMeta.GetAccountXPIntoCurrentLevel() : 0;
        int xpToNext = generalMeta != null ? generalMeta.GetXPToNextAccountLevel() : 100;
        float progress = xpToNext > 0 ? xpIntoLevel / (float)xpToNext : 0f;

        GameObject circle = CreateAnchoredPanel(parent, "ProgressCore", new Color32(8, 14, 18, 230), new Vector2(0f, 0f), new Vector2(0.52f, 0.82f), new Vector2(16f, 18f), new Vector2(-12f, -48f));
        AddFrame(circle.transform, new Color32(58, 84, 90, 255), 2f);

        TextMeshProUGUI coreText = CreateText(circle.transform, "CoreText", 42f, TextAlignmentOptions.Center, textColor);
        coreText.text = accountLevel + "\n<size=35%><color=#D6A441>ACCOUNT LEVEL</color></size>";
        coreText.fontStyle = FontStyles.Bold;
        Stretch(coreText.rectTransform);

        CreateProgressBar(parent, new Vector2(0f, 0f), new Vector2(0.52f, 0f), new Vector2(28f, 18f), new Vector2(-28f, 34f), progress, pathAccentColor, FormatNumber(xpIntoLevel) + " / " + FormatNumber(Mathf.Max(1, xpToNext)) + " XP");
        CreateMiniStat(parent, "Freie Punkte", GetTotalUnspentTowerPoints().ToString(), purpleAccentColor, new Vector2(0.55f, 0.58f), new Vector2(1f, 0.80f));
        CreateMiniStat(parent, "Chaos-Level", GetCurrentChaosLevel().ToString(), chaosAccentColor, new Vector2(0.55f, 0.38f), new Vector2(1f, 0.56f));
        CreateMiniStat(parent, "Gold-Ordnung", GetHighestGoldJustice().ToString(), accentColor, new Vector2(0.55f, 0.18f), new Vector2(1f, 0.36f));
        CreateMiniStat(parent, "XP-Ordnung", GetHighestXpJustice().ToString(), pathAccentColor, new Vector2(0.55f, 0f), new Vector2(1f, 0.16f));
    }

    private void RenderChaosJusticePanel(Transform parent)
    {
        ChaosJusticeBalanceSnapshot snapshot = manager != null && manager.chaosJusticeManager != null
            ? manager.chaosJusticeManager.GetBalanceSnapshot()
            : null;
        int baseSafetyScore = manager != null && manager.chaosJusticeManager != null
            ? Mathf.Max(0, manager.chaosJusticeManager.baseSafetyScore)
            : 0;
        int safetyScore = snapshot != null ? Mathf.Max(0, snapshot.safetyScore - baseSafetyScore) : Mathf.Max(0, GetHighestGoldJustice() + GetHighestXpJustice());
        int chaosScore = snapshot != null ? snapshot.chaosScore : GetCurrentChaosLevel();
        int totalScore = safetyScore + chaosScore;
        int safetyPercent = totalScore > 0 ? Mathf.RoundToInt(safetyScore / (float)totalScore * 100f) : 100;
        int chaosPercent = totalScore > 0 ? Mathf.RoundToInt(chaosScore / (float)totalScore * 100f) : 0;
        string status = GetRuntimeBalanceLabel(chaosPercent);

        TextMeshProUGUI label = CreateText(parent, "BalanceLabels", 15f, TextAlignmentOptions.Center, textColor);
        label.text = "<color=#D6A441>GERECHTIGKEIT " + safetyPercent + "%</color>        BALANCE        <color=#E14B4B>CHAOS " + chaosPercent + "%</color>";
        label.enableWordWrapping = false;
        SetOffsets(label.rectTransform, 26f, 62f, 26f, 156f);

        float safetyFill = Mathf.Clamp01(safetyPercent / 100f);
        float chaosFill = Mathf.Clamp01(chaosPercent / 100f);
        GameObject balanceBar = CreateAnchoredPanel(parent, "LiveBalanceBar", new Color32(23, 27, 34, 255), new Vector2(0.13f, 0.47f), new Vector2(0.87f, 0.58f), Vector2.zero, Vector2.zero);
        AddFrame(balanceBar.transform, new Color32(75, 58, 31, 255), 1f);

        GameObject safetyFillObject = CreateDecoration(balanceBar.transform, "SafetyFill", accentColor);
        RectTransform safetyRect = safetyFillObject.GetComponent<RectTransform>();
        safetyRect.anchorMin = Vector2.zero;
        safetyRect.anchorMax = new Vector2(safetyFill, 1f);
        safetyRect.offsetMin = new Vector2(2f, 2f);
        safetyRect.offsetMax = new Vector2(-2f, -2f);

        GameObject chaosFillObject = CreateDecoration(balanceBar.transform, "ChaosFill", new Color32(120, 35, 35, 230));
        RectTransform chaosRect = chaosFillObject.GetComponent<RectTransform>();
        chaosRect.anchorMin = new Vector2(1f - chaosFill, 0f);
        chaosRect.anchorMax = Vector2.one;
        chaosRect.offsetMin = new Vector2(2f, 2f);
        chaosRect.offsetMax = new Vector2(-2f, -2f);

        GameObject marker = CreateDecoration(balanceBar.transform, "BalanceMarker", textColor);
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(safetyFill, 0f);
        markerRect.anchorMax = new Vector2(safetyFill, 1f);
        markerRect.offsetMin = new Vector2(-1.5f, -6f);
        markerRect.offsetMax = new Vector2(1.5f, 6f);

        CreateScoreBox(parent, "SAFETY SCORE", safetyScore.ToString(), accentColor, new Vector2(0.04f, 0.15f), new Vector2(0.30f, 0.36f));
        CreateScoreBox(parent, "STABILITAET", status, accentColor, new Vector2(0.36f, 0.13f), new Vector2(0.64f, 0.38f));
        CreateScoreBox(parent, "CHAOS SCORE", chaosScore.ToString(), chaosAccentColor, new Vector2(0.70f, 0.15f), new Vector2(0.96f, 0.36f));
    }

    private string GetRuntimeBalanceLabel(int chaosPercent)
    {
        if (chaosPercent <= 20)
            return "Stabil";

        if (chaosPercent <= 40)
            return "Leicht riskant";

        if (chaosPercent <= 60)
            return "Riskant";

        if (chaosPercent <= 80)
            return "Chaos-dominiert";

        return "Kritisch";
    }

    private void RenderActiveRiskRows(Transform parent)
    {
        List<string> riskNames = manager != null && manager.chaosJusticeManager != null
            ? manager.chaosJusticeManager.GetSelectedRiskModifierDisplayNames()
            : null;

        if (riskNames == null || riskNames.Count == 0)
        {
            CreateStatusRow(parent, "Keine aktiven Risiken", "Chaos ist aktuell ruhig", "0 Wellen", chaosAccentColor);
            CreateStatusRow(parent, "Naechster Hinweis", GetNextLockedTitle(), "Ziel", chaosAccentColor);
            return;
        }

        CreateStatusRow(parent, riskNames[0], "Aktiver Risiko-Modifikator", "Run", chaosAccentColor);

        if (riskNames.Count > 1)
            CreateStatusRow(parent, "Weitere Risiken", "+" + (riskNames.Count - 1) + " aktiv", "Details", chaosAccentColor);
    }

    private void RenderRightColumnOverview()
    {
        GameObject goalsPanel = CreateDashboardPanel(rightColumnContent, "GoalsPanel", "NAECHSTE ZIELE", accentColor, new Vector2(0f, 0.40f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        CreateGoalRow(goalsPanel.transform, "Naechster Boss", GetBossWaveProgressText(), accentColor);
        CreateGoalRow(goalsPanel.transform, "Chaos-Level", GetCurrentChaosLevel() + " / 5", purpleAccentColor);
        CreateGoalRow(goalsPanel.transform, "Aktive Risiken", GetActiveRiskModifierCount() + " aktiv", chaosAccentColor);
        CreateGoalRow(goalsPanel.transform, "Naechste Freischaltung", GetNextLockedTitle(), pathAccentColor);
        CreateGoalRow(goalsPanel.transform, "Neue Freischaltungen", manager != null ? manager.GetNewlyUnlockedThisSessionCount() + " neu" : "0 neu", successAccentColor);
        CreateAnchoredButton(goalsPanel.transform, "AllGoalsButton", "ALLE ZIELE ANZEIGEN", cardColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(230f, 34f));

        GameObject lastRunPanel = CreateDashboardPanel(rightColumnContent, "LastRunPanel", "RUN-STATUS", mutedTextColor, new Vector2(0f, 0f), new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero);
        CreateInfoLine(lastRunPanel.transform, "Aktuelle Wave", GetCurrentWaveNumber().ToString(), 0.72f);
        CreateInfoLine(lastRunPanel.transform, "Gold", GetCurrentGold().ToString(), 0.61f);
        CreateInfoLine(lastRunPanel.transform, "Leben", GetCurrentLives().ToString(), 0.50f);
        CreateInfoLine(lastRunPanel.transform, "Chaos-Level", GetCurrentChaosLevel().ToString(), 0.39f);
        CreateInfoLine(lastRunPanel.transform, "Boss-Kills", GetBossKills().ToString(), 0.28f);
        CreateInfoLine(lastRunPanel.transform, "Aktive Risiken", GetActiveRiskModifierCount().ToString(), 0.17f);
        CreateAnchoredButton(lastRunPanel.transform, "RunStatsButton", "RUN-STATISTIKEN", cardColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(220f, 34f));
    }

    private void RenderMetaSectionView(RuntimeSection section)
    {
        Color accent = GetSectionAccent(section);
        AddSectionHeading(mainContent, GetSectionTitle(section), GetSectionSubtitle(section), accent);

        GameObject overviewPanel = CreateDashboardPanel(mainContent, "MetaSectionOverview", "UEBERSICHT", accent, new Vector2(0f, 0.52f), new Vector2(0.48f, 0.84f), Vector2.zero, new Vector2(-8f, -8f));
        GameObject detailPanel = CreateDashboardPanel(mainContent, "MetaSectionDetails", "DETAILS", accent, new Vector2(0.50f, 0.52f), new Vector2(1f, 0.84f), new Vector2(8f, 0f), new Vector2(0f, -8f));
        GameObject listPanel = CreateDashboardPanel(mainContent, "MetaSectionList", "AKTUELLE WERTE", accent, new Vector2(0f, 0f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);

        RenderMetaOverviewRows(section, overviewPanel.transform);
        RenderMetaDetailText(section, detailPanel.transform);
        RenderMetaValueRows(section, listPanel.transform);

        RenderRightColumnOverview();
    }

    private void RenderMetaOverviewRows(RuntimeSection section, Transform parent)
    {
        switch (section)
        {
            case RuntimeSection.Allgemein:
                GeneralMetaProgressionManager generalMeta = GetGeneralMetaManager();
                CreateStatusRow(parent, "Account-Level", (generalMeta != null ? generalMeta.GetAccountXPIntoCurrentLevel() : 0) + " XP im Level", generalMeta != null ? generalMeta.accountLevel.ToString() : "1", accentColor);
                CreateStatusRow(parent, "Kernwissen", "Spendbare allgemeine Meta-Waehrung", generalMeta != null ? FormatNumber(generalMeta.kernwissen) : "0", accentColor);
                CreateStatusRow(parent, "Loadout", "Aktive Power-Slots", generalMeta != null ? generalMeta.GetUsedLoadoutSlots() + "/" + generalMeta.GetLoadoutSlotCapacity() : "0/0", accentColor);
                break;
            case RuntimeSection.TowerMastery:
                CreateStatusRow(parent, "Tower-Rollen", "Bekannte Mastery-Rollen", GetTowerRoleCount().ToString(), purpleAccentColor);
                CreateStatusRow(parent, "Freie Punkte", "Nicht ausgegebene Tower-Punkte", GetTotalUnspentTowerPoints().ToString(), purpleAccentColor);
                CreateStatusRow(parent, "Milestones", "Freigeschaltete Stufen", GetTowerMilestoneCount().ToString(), purpleAccentColor);
                break;
            case RuntimeSection.ChaosForschung:
                ChaosResearchProgressionManager chaosResearch = GetChaosResearchManager();
                CreateStatusRow(parent, "Chaos-Wissen", "Forschungswaehrung", chaosResearch != null ? FormatNumber(chaosResearch.chaosKnowledge) : "0", purpleAccentColor);
                CreateStatusRow(parent, "Risskerne", "Chaos-5-Endgame", chaosResearch != null ? chaosResearch.riftCores.ToString() : "0", chaosAccentColor);
                CreateStatusRow(parent, "Aktive Konter", "Loadout-relevante Forschung", chaosResearch != null ? chaosResearch.GetActiveCounterCount().ToString() : "0", chaosAccentColor);
                break;
            case RuntimeSection.Pfadtechnik:
                PathTechniqueProgressionManager pathTechnique = GetPathTechniqueManager();
                CreateStatusRow(parent, "Pfadtechnik-Level", "Rettung nach Verbau", pathTechnique != null ? pathTechnique.pathTechniqueLevel.ToString() : "1", pathAccentColor);
                CreateStatusRow(parent, "Bauplaene", "Normale Pfadtechnik-Punkte", pathTechnique != null ? pathTechnique.blueprints.ToString() : "0", pathAccentColor);
                CreateStatusRow(parent, "Rissbauplaene", "Chaos-Verbau-Endgame", pathTechnique != null ? pathTechnique.riftBlueprints.ToString() : "0", chaosAccentColor);
                break;
            case RuntimeSection.EliteJagd:
                EliteHuntProgressionManager eliteHunt = GetEliteHuntManager();
                CreateStatusRow(parent, "Elite-Rang", "Dauerhafter Jagd-Fortschritt", eliteHunt != null ? eliteHunt.eliteRank.ToString() : "1", chaosAccentColor);
                CreateStatusRow(parent, "Elite-Siegel", "Ausgebbare Elite-Waehrung", eliteHunt != null ? eliteHunt.eliteSeals.ToString() : "0", chaosAccentColor);
                CreateStatusRow(parent, "Elite besiegt", "Gesamte Elite-Kills", eliteHunt != null ? eliteHunt.totalEliteKillsEver.ToString() : "0", chaosAccentColor);
                break;
        }
    }

    private void RenderMetaDetailText(RuntimeSection section, Transform parent)
    {
        TextMeshProUGUI text = CreateText(parent, "MetaDetailBody", 15f, TextAlignmentOptions.TopLeft, textColor);
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(text.rectTransform, 18f, 58f, 18f, 18f);

        switch (section)
        {
            case RuntimeSection.Allgemein:
                text.text = "Allgemein zeigt Account-Fortschritt, Content-Freischaltungen, QoL, Startoptionen und Loadout. Im Run bleibt diese Ansicht read-only.";
                break;
            case RuntimeSection.TowerMastery:
                text.text = "Tower Mastery entsteht am Run-Ende durch Tower-Level, Run-Tiefe und echten Impact. In F1 werden nur Fortschritt und Ziele angezeigt.";
                break;
            case RuntimeSection.ChaosForschung:
                text.text = "Chaos-Forschung erweitert Risiko-Verstaendnis, Vorschau und kleine Konter. Chaos bleibt gefaehrlich und wird nicht passiv trivialisiert.";
                break;
            case RuntimeSection.Pfadtechnik:
                text.text = "Pfadtechnik belohnt die Rettung nach Verbau, nicht das Verbauen selbst. Bauplaene entstehen erst durch echtes Weiterkommen.";
                break;
            case RuntimeSection.EliteJagd:
                text.text = "Elite-Jagd ist ein spaeter opt-in Endgame-Bereich. Dieser Run-Screen zeigt nur Teaser und gespeicherte Werte, falls das System aktiv ist.";
                break;
        }
    }

    private void RenderMetaValueRows(RuntimeSection section, Transform parent)
    {
        switch (section)
        {
            case RuntimeSection.Allgemein:
                GeneralMetaProgressionManager generalMeta = GetGeneralMetaManager();
                CreateStatusRow(parent, "Startgold-Bonus", "Aktiv im naechsten Normal-Run", generalMeta != null ? "+" + generalMeta.GetActiveStartGoldBonus() : "+0", successAccentColor);
                CreateStatusRow(parent, "Startleben-Bonus", "Aktiv im naechsten Normal-Run", generalMeta != null ? "+" + generalMeta.GetActiveStartLifeBonus() : "+0", successAccentColor);
                CreateStatusRow(parent, "Hoechste Wave", "Profil-Bestleistung", generalMeta != null ? generalMeta.highestWaveEver.ToString() : GetHighestWave().ToString(), accentColor);
                break;
            case RuntimeSection.TowerMastery:
                CreateStatusRow(parent, "Basic", GetTowerRoleLine(TowerRole.Basic), GetTowerRolePoints(TowerRole.Basic), purpleAccentColor);
                CreateStatusRow(parent, "Rapid", GetTowerRoleLine(TowerRole.Rapid), GetTowerRolePoints(TowerRole.Rapid), purpleAccentColor);
                CreateStatusRow(parent, "Heavy", GetTowerRoleLine(TowerRole.Heavy), GetTowerRolePoints(TowerRole.Heavy), purpleAccentColor);
                CreateStatusRow(parent, "Fire", GetTowerRoleLine(TowerRole.Fire), GetTowerRolePoints(TowerRole.Fire), purpleAccentColor);
                break;
            case RuntimeSection.ChaosForschung:
                CreateStatusRow(parent, "Chaos-Level", "Aktueller Run", GetCurrentChaosLevel() + " / 5", chaosAccentColor);
                CreateStatusRow(parent, "Chaos-Waves", "Ueberstanden", GetChaosWavesCompleted().ToString(), purpleAccentColor);
                CreateStatusRow(parent, "Block-Waves", "Chaos-Wave-Bausteine", GetChaosBlockWavesCompleted().ToString(), pathAccentColor);
                break;
            case RuntimeSection.Pfadtechnik:
                PathTechniqueProgressionManager pathTechnique = GetPathTechniqueManager();
                CreateStatusRow(parent, "Verbau ueberlebt", "Profil", pathTechnique != null ? pathTechnique.totalBlockedRecoveriesEver.ToString() : "0", pathAccentColor);
                CreateStatusRow(parent, "Boss nach Verbau", "Profil", pathTechnique != null ? pathTechnique.totalBossKillsAfterBlockEver.ToString() : "0", pathAccentColor);
                CreateStatusRow(parent, "Letzter Run", "Bauplaene / Rissbauplaene", pathTechnique != null ? pathTechnique.lastRunBlueprintsGained + " / " + pathTechnique.lastRunRiftBlueprintsGained : "0 / 0", pathAccentColor);
                break;
            case RuntimeSection.EliteJagd:
                EliteHuntProgressionManager eliteHunt = GetEliteHuntManager();
                CreateStatusRow(parent, "Elite sichtbar", "Freischaltstatus", eliteHunt != null && eliteHunt.IsEliteHuntUnlocked() ? "Ja" : "Gesperrt", chaosAccentColor);
                CreateStatusRow(parent, "Elite-Bosse", "Besiegt", eliteHunt != null ? eliteHunt.totalEliteBossKillsEver.ToString() : "0", chaosAccentColor);
                CreateStatusRow(parent, "Riss-Elite", "Endgame sichtbar", eliteHunt != null && eliteHunt.IsRiftEliteVisible() ? "Ja" : "Nein", chaosAccentColor);
                break;
        }
    }

    private void RenderCategoryView(RuntimeSection section)
    {
        Color accent = GetSectionAccent(section);
        List<ChaosUnlockEntry> entries = GetEntriesForSection(section);
        EnsureSelectedEntry(entries);

        AddSectionHeading(mainContent, GetSectionTitle(section), GetSectionSubtitle(section), accent);

        GameObject listPanel = CreateAnchoredPanel(mainContent, "CategoryGridPanel", new Color32(0, 0, 0, 0), new Vector2(0f, 0f), new Vector2(1f, 0.88f), Vector2.zero, Vector2.zero);
        entryListContent = CreateScrollableGridContent(listPanel.transform, "CategoryGridContent", new Vector2(245f, 126f), 3);

        foreach (ChaosUnlockEntry entry in entries)
            generatedButtons.Add(CreateUnlockCard(entry, accent));

        RenderSelectedDetail(entries, section);
    }

    private Button CreateUnlockCard(ChaosUnlockEntry entry, Color accent)
    {
        bool selected = entry != null && entry.unlockId == selectedUnlockId;
        bool unlocked = entry != null && entry.IsUnlocked();
        Color edgeColor = unlocked ? accent : lockedAccentColor;

        GameObject card = CreatePanel(entryListContent, "RuntimeUnlockCard_" + (entry != null ? entry.unlockId : "empty"), selected ? selectedCardColor : cardColor, Vector2.zero);
        AddFrame(card.transform, selected ? accent : new Color32(58, 64, 72, 255), selected ? 3f : 1f);
        AddVerticalAccent(card.transform, edgeColor, selected ? 8f : 5f);

        Button button = card.AddComponent<Button>();
        string id = entry != null ? entry.unlockId : "";
        button.onClick.AddListener(() => SelectEntry(id));
        ApplySelectableColors(button, card.GetComponent<Image>().color, accent);

        TextMeshProUGUI title = CreateText(card.transform, "Title", 15f, TextAlignmentOptions.TopLeft, textColor);
        title.text = entry != null && manager != null ? manager.GetUnlockDisplayTitle(entry) : "Freischaltung";
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(title.rectTransform, 18f, 14f, 14f, 78f);

        string category = entry != null && manager != null ? manager.GetCategoryDisplayName(entry.category) : "Read-only";
        string state = unlocked ? "Freigeschaltet" : "Gesperrt";
        TextMeshProUGUI subtitle = CreateText(card.transform, "Subtitle", 12f, TextAlignmentOptions.TopLeft, unlocked ? accent : mutedTextColor);
        subtitle.text = category + "\n<size=85%>" + state + "</size>";
        subtitle.enableWordWrapping = false;
        subtitle.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(subtitle.rectTransform, 18f, 52f, 14f, 18f);

        return button;
    }

    private void SelectEntry(string unlockId)
    {
        selectedUnlockId = unlockId;
        RefreshContent();
    }

    private void RenderSelectedDetail(List<ChaosUnlockEntry> entries, RuntimeSection section)
    {
        ChaosUnlockEntry entry = GetSelectedEntry(entries);
        Color accent = GetSectionAccent(section);
        GameObject detailPanel = CreateDashboardPanel(rightColumnContent, "DetailPanel", "DETAILS", accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI detailTitle = CreateText(detailPanel.transform, "DetailTitle", 19f, TextAlignmentOptions.TopLeft, textColor);
        detailTitle.text = entry != null && manager != null ? manager.GetUnlockDisplayTitle(entry) : "Keine Freischaltung";
        detailTitle.fontStyle = FontStyles.Bold;
        detailTitle.enableWordWrapping = false;
        detailTitle.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(detailTitle.rectTransform, 18f, 50f, 18f, 360f);

        TextMeshProUGUI state = CreateText(detailPanel.transform, "DetailState", 13f, TextAlignmentOptions.TopLeft, entry != null && entry.IsUnlocked() ? successAccentColor : mutedTextColor);
        state.text = entry != null && entry.IsUnlocked() ? "Freigeschaltet" : "Read-only / gesperrt";
        state.enableWordWrapping = false;
        state.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(state.rectTransform, 18f, 82f, 18f, 330f);

        detailText = CreateText(detailPanel.transform, "DetailBody", 15f, TextAlignmentOptions.TopLeft, textColor);
        detailText.text = manager != null ? manager.GetUnlockDetailText(entry) : "Runtime-Freischaltungen sind noch nicht angebunden.";
        detailText.enableWordWrapping = true;
        detailText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(detailText.rectTransform, 18f, 116f, 18f, 18f);
    }

    private void RefreshEntryList()
    {
        RefreshContent();
    }

    private Button CreateEntryButton(ChaosUnlockEntry entry)
    {
        return CreateUnlockCard(entry, GetSectionAccent(selectedSection));
    }

    private void RefreshDetail()
    {
        RefreshContent();
    }

    private void EnsureSelectedEntry(List<ChaosUnlockEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            selectedUnlockId = "";
            return;
        }

        foreach (ChaosUnlockEntry entry in entries)
        {
            if (entry != null && entry.unlockId == selectedUnlockId)
                return;
        }

        selectedUnlockId = entries[0] != null ? entries[0].unlockId : "";
    }

    private ChaosUnlockEntry GetSelectedEntry(List<ChaosUnlockEntry> entries = null)
    {
        List<ChaosUnlockEntry> source = entries ?? GetEntriesForSection(selectedSection);
        EnsureSelectedEntry(source);

        foreach (ChaosUnlockEntry entry in source)
        {
            if (entry != null && entry.unlockId == selectedUnlockId)
                return entry;
        }

        return source.Count > 0 ? source[0] : null;
    }

    private void ClearGeneratedButtons()
    {
        foreach (Button button in generatedButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedButtons.Clear();
    }

    private void EnsureUI()
    {
        if (!autoCreateUIIfMissing)
            return;

        bool hasDashboard = rootPanel != null && resourceChipContent != null && sidebarContent != null && mainContent != null && rightColumnContent != null;
        if (hasDashboard)
            return;

        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        if (targetCanvas == null)
            return;

        if (rootPanel != null)
        {
            Destroy(rootPanel);
            rootPanel = null;
        }

        CreateAutoUI();
    }

    private void CreateAutoUI()
    {
        GameObject overlay = new GameObject("RuntimeUnlockOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(targetCanvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = overlayColor;
        overlayImage.raycastTarget = true;
        rootPanel = overlay;

        GameObject window = CreatePanel(overlay.transform, "RuntimeUnlockDashboard", windowColor, Vector2.zero);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.0075f, 0.015f);
        windowRect.anchorMax = new Vector2(0.9925f, 0.98f);
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;
        AddFrame(window.transform, accentColor, 2f);
        AddCornerBrackets(window.transform, accentColor, 34f, 2f);

        CreateTopBar(window.transform);
        CreateSidebar(window.transform);
        CreateMainArea(window.transform);
        CreateRightColumn(window.transform);
        CreateBottomBar(window.transform);
        CreateNotification(overlay.transform);
    }

    private void CreateTopBar(Transform parent)
    {
        GameObject topBar = CreateAnchoredPanel(parent, "TopResourceBar", new Color32(7, 16, 20, 248), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, -58f), new Vector2(-2f, 0f));
        AddFrame(topBar.transform, accentColor, 2f);
        AddBottomLine(topBar.transform, new Color32(68, 48, 20, 255), 3f);
        AddCornerBrackets(topBar.transform, new Color32(146, 99, 34, 255), 24f, 1f);

        GameObject chips = CreateAnchoredPanel(topBar.transform, "ResourceChips", new Color32(0, 0, 0, 0), new Vector2(0f, 0f), new Vector2(0.42f, 1f), new Vector2(18f, 10f), new Vector2(-6f, -10f));
        HorizontalLayoutGroup chipLayout = chips.AddComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 8f;
        chipLayout.childAlignment = TextAnchor.MiddleLeft;
        chipLayout.childControlWidth = true;
        chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = false;
        chipLayout.childForceExpandHeight = true;
        resourceChipContent = chips.transform;

        titleText = CreateText(topBar.transform, "Title", 30f, TextAlignmentOptions.Center, textColor);
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        titleText.characterSpacing = 6f;
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.43f, 0f);
        titleRect.anchorMax = new Vector2(0.64f, 1f);
        titleRect.offsetMin = new Vector2(0f, 4f);
        titleRect.offsetMax = new Vector2(0f, -4f);

        TextMeshProUGUI emblem = CreateText(topBar.transform, "CenterEmblem", 22f, TextAlignmentOptions.Center, accentColor);
        emblem.text = "V";
        emblem.fontStyle = FontStyles.Bold;
        RectTransform emblemRect = emblem.rectTransform;
        emblemRect.anchorMin = new Vector2(0.5f, 0f);
        emblemRect.anchorMax = new Vector2(0.5f, 0f);
        emblemRect.pivot = new Vector2(0.5f, 0f);
        emblemRect.anchoredPosition = new Vector2(0f, -12f);
        emblemRect.sizeDelta = new Vector2(44f, 30f);

        GameObject account = CreateAnchoredPanel(topBar.transform, "RunInfo", new Color32(10, 17, 22, 170), new Vector2(0.70f, 0f), new Vector2(0.93f, 1f), new Vector2(0f, 8f), new Vector2(-10f, -8f));
        AddFrame(account.transform, new Color32(73, 55, 28, 255), 1f);
        summaryText = CreateText(account.transform, "RunSummary", 14f, TextAlignmentOptions.MidlineLeft, textColor);
        summaryText.enableWordWrapping = false;
        summaryText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(summaryText.rectTransform, 12f, 4f, 12f, 4f);

        refreshButton = null;
        closeButton = CreateSmallButton(topBar.transform, "CloseButton", "X", closeButtonColor, new Vector2(-18f, -18f));
    }

    private void CreateSidebar(Transform parent)
    {
        GameObject sidebar = CreateAnchoredPanel(parent, "Sidebar", new Color32(8, 14, 18, 248), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 58f), new Vector2(268f, -78f));
        AddFrame(sidebar.transform, accentColor, 2f);
        AddCornerBrackets(sidebar.transform, accentColor, 24f, 2f);

        TextMeshProUGUI title = CreateText(sidebar.transform, "SidebarTitle", 22f, TextAlignmentOptions.Center, accentColor);
        title.text = "META-HUB";
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(12f, -48f);
        title.rectTransform.offsetMax = new Vector2(-12f, -12f);

        sidebarContent = CreateContentWithLayout(sidebar.transform, "SidebarContent");
        SetOffsets(sidebarContent.GetComponent<RectTransform>(), 16f, 52f, 16f, 16f);
        VerticalLayoutGroup layout = sidebarContent.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 7f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
    }

    private void CreateMainArea(Transform parent)
    {
        GameObject main = CreateAnchoredPanel(parent, "MainArea", new Color32(7, 13, 18, 120), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(302f, 58f), new Vector2(-358f, -78f));
        AddFrame(main.transform, new Color32(58, 64, 72, 180), 1f);
        AddCornerBrackets(main.transform, new Color32(88, 64, 28, 210), 22f, 1f);
        mainContent = main.transform;
    }

    private void CreateRightColumn(Transform parent)
    {
        GameObject right = CreateAnchoredPanel(parent, "RightColumn", new Color32(0, 0, 0, 0), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-342f, 58f), new Vector2(-14f, -78f));
        rightColumnContent = right.transform;
    }

    private void CreateBottomBar(Transform parent)
    {
        GameObject bottom = CreateAnchoredPanel(parent, "BottomBar", new Color32(7, 13, 18, 248), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f), new Vector2(-2f, 44f));
        AddFrame(bottom.transform, new Color32(73, 55, 28, 255), 1f);

        TextMeshProUGUI tip = CreateText(bottom.transform, "Tip", 13f, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        tip.text = "Tipp: Esc oeffnet die Run-Ansicht. Hauptmenue bricht den aktuellen Run ab.";
        tip.enableWordWrapping = false;
        tip.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(tip.rectTransform, 18f, 8f, 640f, 8f);

        mainMenuButton = CreateAnchoredButton(bottom.transform, "MainMenuButton", "HAUPTMENUE", new Color32(80, 45, 50, 245), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-420f, 0f), new Vector2(170f, 36f));
        mainMenuButton.onClick.AddListener(ReturnToMainMenuFromButton);
        CreateAnchoredButton(bottom.transform, "OptionsButtonBottom", "OPTIONEN", cardColor, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(170f, 36f));
        Button back = CreateAnchoredButton(bottom.transform, "BackButton", "ZURUECK", cardColor, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(180f, 36f));
        back.onClick.AddListener(CloseFromButton);
    }

    private void CreateNotification(Transform parent)
    {
        notificationPanel = CreatePanel(parent, "Notification", accentColor, new Vector2(480f, 58f));
        RectTransform rect = notificationPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);

        notificationText = CreateText(notificationPanel.transform, "NotificationText", 17f, TextAlignmentOptions.Center, Color.white);
        notificationText.enableWordWrapping = false;
        Stretch(notificationText.rectTransform);
        notificationPanel.SetActive(false);
    }

    private void CreateSidebarSectionLabel(string label)
    {
        GameObject labelObject = new GameObject("SidebarSection_" + label, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(sidebarContent, false);

        LayoutElement layoutElement = labelObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 20f;
        layoutElement.preferredHeight = 20f;
        layoutElement.flexibleWidth = 1f;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 11f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = mutedTextColor;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private void CreateSidebarSpacer(float height)
    {
        GameObject spacer = new GameObject("SidebarSpacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(sidebarContent, false);
        LayoutElement layoutElement = spacer.GetComponent<LayoutElement>();
        layoutElement.minHeight = Mathf.Max(0f, height);
        layoutElement.preferredHeight = Mathf.Max(0f, height);
        layoutElement.flexibleWidth = 1f;
    }

    private Button CreateSidebarButton(RuntimeSection section, string icon, string label, Color accent)
    {
        bool selected = selectedSection == section;
        GameObject buttonObject = CreatePanel(sidebarContent, "RuntimeSection_" + section, selected ? Color.Lerp(cardColor, accent, 0.28f) : cardColor, Vector2.zero);
        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 46f;
        layoutElement.minHeight = 44f;
        layoutElement.flexibleHeight = 0f;
        layoutElement.flexibleWidth = 1f;
        AddFrame(buttonObject.transform, selected ? accent : new Color32(48, 53, 52, 255), selected ? 2f : 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => SelectSection(section));
        ApplySelectableColors(button, buttonObject.GetComponent<Image>().color, accent);

        GameObject iconWell = CreateAnchoredPanel(buttonObject.transform, "IconWell", new Color32(8, 14, 18, 235), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, -14f), new Vector2(40f, 14f));
        AddFrame(iconWell.transform, accent, 1f);

        TextMeshProUGUI iconText = CreateText(iconWell.transform, "Icon", 14f, TextAlignmentOptions.Center, selected ? Color.white : accent);
        iconText.text = icon;
        iconText.fontStyle = FontStyles.Bold;
        Stretch(iconText.rectTransform);

        TextMeshProUGUI labelText = CreateText(buttonObject.transform, "Label", 13f, TextAlignmentOptions.MidlineLeft, Color.white);
        labelText.text = label;
        labelText.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(labelText.rectTransform, 52f, 4f, 12f, 4f);
        return button;
    }

    private void CreateSidebarSlot(Transform parent, string icon, string label, Color accent)
    {
        GameObject slot = CreatePanel(parent, "SidebarSlot_" + label, cardColor, Vector2.zero);
        slot.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddFrame(slot.transform, accent, 1f);
        AddCornerBrackets(slot.transform, accent, 12f, 1f);

        TextMeshProUGUI iconText = CreateText(slot.transform, "Icon", 22f, TextAlignmentOptions.Center, accent);
        iconText.text = icon;
        iconText.fontStyle = FontStyles.Bold;
        SetOffsets(iconText.rectTransform, 2f, 4f, 2f, 24f);

        TextMeshProUGUI labelText = CreateText(slot.transform, "Label", 9f, TextAlignmentOptions.Center, textColor);
        labelText.text = label;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(labelText.rectTransform, 2f, 50f, 2f, 4f);
    }

    private void SelectSection(RuntimeSection section)
    {
        selectedSection = section;
        selectedUnlockId = "";
        RefreshAll();
    }

    private void AddSectionHeading(Transform parent, string title, string subtitle, Color accent)
    {
        GameObject heading = CreateAnchoredPanel(parent, "SectionHeading", new Color32(0, 0, 0, 0), new Vector2(0f, 0.88f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI titleText = CreateText(heading.transform, "Title", 28f, TextAlignmentOptions.TopLeft, accent);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(titleText.rectTransform, 4f, 0f, 12f, 38f);

        TextMeshProUGUI subtitleText = CreateText(heading.transform, "Subtitle", 14f, TextAlignmentOptions.TopLeft, mutedTextColor);
        subtitleText.text = subtitle;
        subtitleText.enableWordWrapping = false;
        subtitleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(subtitleText.rectTransform, 6f, 40f, 12f, 8f);
    }

    private void CreateResourceChip(Transform parent, string icon, string label, string value, Color accent)
    {
        GameObject chip = CreatePanel(parent, "Chip_" + label, new Color32(12, 22, 28, 230), Vector2.zero);
        LayoutElement layoutElement = chip.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 100f;
        layoutElement.minWidth = 90f;
        layoutElement.flexibleWidth = 0f;
        AddFrame(chip.transform, new Color32(78, 60, 32, 255), 1f);
        AddCornerBrackets(chip.transform, new Color32(102, 74, 34, 255), 10f, 1f);

        GameObject iconBox = CreateAnchoredPanel(chip.transform, "IconBox", new Color32(18, 28, 38, 255), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -15f), new Vector2(36f, 15f));
        AddFrame(iconBox.transform, accent, 1f);
        TextMeshProUGUI iconText = CreateText(iconBox.transform, "Icon", 12f, TextAlignmentOptions.Center, accent);
        iconText.text = icon;
        iconText.fontStyle = FontStyles.Bold;
        Stretch(iconText.rectTransform);

        TextMeshProUGUI valueText = CreateText(chip.transform, "Value", 14f, TextAlignmentOptions.MidlineLeft, textColor);
        valueText.text = value;
        valueText.fontStyle = FontStyles.Bold;
        valueText.enableWordWrapping = false;
        valueText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(valueText.rectTransform, 42f, 4f, 6f, 16f);

        TextMeshProUGUI labelText = CreateText(chip.transform, "Label", 9f, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        labelText.text = label;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(labelText.rectTransform, 42f, 22f, 6f, 4f);
    }

    private void CreateMetricCard(Transform parent, string title, string value, string subtitle, string progressText, float progress, Color accent, string icon)
    {
        GameObject card = CreatePanel(parent, "Metric_" + title, cardColor, Vector2.zero);
        card.AddComponent<LayoutElement>().flexibleWidth = 1f;
        AddFrame(card.transform, accent, 1f);
        AddCornerBrackets(card.transform, accent, 18f, 1f);

        TextMeshProUGUI titleText = CreateText(card.transform, "Title", 15f, TextAlignmentOptions.Top, textColor);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(titleText.rectTransform, 10f, 14f, 10f, 96f);

        GameObject iconBox = CreateAnchoredPanel(card.transform, "Icon", new Color32(8, 14, 18, 215), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 40f), new Vector2(76f, 98f));
        AddFrame(iconBox.transform, accent, 1f);
        TextMeshProUGUI iconText = CreateText(iconBox.transform, "IconText", 25f, TextAlignmentOptions.Center, accent);
        iconText.text = icon;
        iconText.fontStyle = FontStyles.Bold;
        Stretch(iconText.rectTransform);

        TextMeshProUGUI valueText = CreateText(card.transform, "Value", 31f, TextAlignmentOptions.TopLeft, accent);
        valueText.text = value;
        valueText.fontStyle = FontStyles.Bold;
        valueText.enableWordWrapping = false;
        valueText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(valueText.rectTransform, 92f, 58f, 14f, 48f);

        TextMeshProUGUI subtitleText = CreateText(card.transform, "Subtitle", 12f, TextAlignmentOptions.TopLeft, mutedTextColor);
        subtitleText.text = subtitle;
        subtitleText.enableWordWrapping = false;
        subtitleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(subtitleText.rectTransform, 92f, 96f, 14f, 26f);

        CreateProgressBar(card.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(92f, 16f), new Vector2(-18f, 26f), progress, accent, progressText);
    }

    private GameObject CreateDashboardPanel(Transform parent, string objectName, string title, Color accent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject panel = CreateAnchoredPanel(parent, objectName, panelColor, anchorMin, anchorMax, offsetMin, offsetMax);
        AddFrame(panel.transform, new Color32(58, 64, 72, 255), 1f);
        AddCornerBrackets(panel.transform, new Color32(88, 64, 28, 220), 16f, 1f);
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

    private void CreateStatusRow(Transform parent, string title, string subtitle, string trailing, Color accent)
    {
        int index = CountChildrenWithPrefix(parent, "StatusRow_");
        float top = 58f + index * 60f;
        GameObject row = CreateAnchoredPanel(parent, "StatusRow_" + index, new Color32(12, 22, 28, 220), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -top - 48f), new Vector2(-16f, -top));
        AddFrame(row.transform, accent, 1f);
        AddCornerBrackets(row.transform, accent, 10f, 1f);

        TextMeshProUGUI titleText = CreateText(row.transform, "Title", 14f, TextAlignmentOptions.TopLeft, accent);
        titleText.text = title;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(titleText.rectTransform, 14f, 7f, 90f, 23f);

        TextMeshProUGUI subtitleText = CreateText(row.transform, "Subtitle", 12f, TextAlignmentOptions.BottomLeft, mutedTextColor);
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

        TextMeshProUGUI progressText = CreateText(row.transform, "GoalProgress", 12f, TextAlignmentOptions.MidlineRight, mutedTextColor);
        progressText.text = progress;
        progressText.enableWordWrapping = false;
        progressText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(progressText.rectTransform, 8f, 4f, 10f, 4f);
    }

    private void CreateInfoLine(Transform parent, string label, string value, float y)
    {
        TextMeshProUGUI line = CreateText(parent, "InfoLine_" + label, 12f, TextAlignmentOptions.MidlineLeft, mutedTextColor);
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
        AddFrame(bar.transform, new Color32(75, 58, 31, 255), 1f);

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
        AddFrame(stat.transform, new Color32(58, 64, 72, 255), 1f);

        TextMeshProUGUI statText = CreateText(stat.transform, "Text", 13f, TextAlignmentOptions.MidlineLeft, textColor);
        statText.text = label + " <align=right><color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">" + value + "</color>";
        statText.enableWordWrapping = false;
        statText.overflowMode = TextOverflowModes.Ellipsis;
        SetOffsets(statText.rectTransform, 14f, 6f, 14f, 6f);
    }

    private void CreateScoreBox(Transform parent, string label, string value, Color accent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject box = CreateAnchoredPanel(parent, "ScoreBox_" + label, new Color32(12, 22, 28, 230), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        AddFrame(box.transform, new Color32(58, 64, 72, 255), 1f);

        TextMeshProUGUI valueText = CreateText(box.transform, "Value", 23f, TextAlignmentOptions.Center, accent);
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
        viewport.GetComponent<Image>().color = new Color32(255, 255, 255, 4);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject(objectName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 900f);

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(2, 12, 2, 12);
        grid.spacing = new Vector2(12f, 12f);
        grid.cellSize = cellSize;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columnCount);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        return content.transform;
    }

    private Transform CreateContentWithLayout(Transform parent, string objectName)
    {
        GameObject content = new GameObject(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup));
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
        return content.transform;
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
        AddFrame(buttonObject.transform, new Color32(38, 31, 25, 255), 2f);

        Button button = buttonObject.GetComponent<Button>();
        ApplySelectableColors(button, color, accentColor);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", 17f, TextAlignmentOptions.Center, Color.white);
        text.text = label;
        text.enableWordWrapping = false;
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
        AddFrame(buttonObject.transform, new Color32(38, 31, 25, 255), 2f);
        AddVerticalAccent(buttonObject.transform, accentColor, 6f);

        Button button = buttonObject.GetComponent<Button>();
        ApplySelectableColors(button, color, accentColor);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", 15f, TextAlignmentOptions.Center, Color.white);
        text.text = label;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(text.rectTransform);
        return button;
    }

    private GameObject CreatePanel(Transform parent, string objectName, Color color, Vector2 size)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<RectTransform>().sizeDelta = size;
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

    private void AddFrame(Transform parent, Color color, float thickness)
    {
        if (parent == null || thickness <= 0f)
            return;

        AddLine(parent, "FrameTop", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        AddLine(parent, "FrameBottom", color, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        AddLine(parent, "FrameLeft", color, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        AddLine(parent, "FrameRight", color, new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
    }

    private void AddCornerBrackets(Transform parent, Color color, float length, float thickness)
    {
        if (parent == null || length <= 0f || thickness <= 0f)
            return;

        AddLine(parent, "CornerTL_H", color, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -thickness), new Vector2(length, 0f));
        AddLine(parent, "CornerTL_V", color, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -length), new Vector2(thickness, 0f));

        AddLine(parent, "CornerTR_H", color, Vector2.one, Vector2.one, new Vector2(-length, -thickness), Vector2.zero);
        AddLine(parent, "CornerTR_V", color, Vector2.one, Vector2.one, new Vector2(-thickness, -length), Vector2.zero);

        AddLine(parent, "CornerBL_H", color, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(length, thickness));
        AddLine(parent, "CornerBL_V", color, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(thickness, length));

        AddLine(parent, "CornerBR_H", color, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-length, 0f), new Vector2(0f, thickness));
        AddLine(parent, "CornerBR_V", color, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-thickness, 0f), new Vector2(0f, length));
    }

    private void AddLine(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
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

    private void ApplySelectableColors(Button button, Color baseColor, Color highlightColor)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, highlightColor, 0.28f);
        colors.pressedColor = Color.Lerp(baseColor, highlightColor, 0.45f);
        colors.selectedColor = baseColor;
        colors.disabledColor = new Color32(34, 34, 34, 180);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private string FormatNumber(int value)
    {
        return Mathf.Max(0, value).ToString();
    }

    private GameManager GetGameManager()
    {
        if (manager != null && manager.gameManager != null)
            return manager.gameManager;

        return FindObjectOfType<GameManager>();
    }

    private GeneralMetaProgressionManager GetGeneralMetaManager()
    {
        GameManager gameManager = GetGameManager();
        if (gameManager != null)
            return gameManager.GetGeneralMetaProgressionManager();

        return FindObjectOfType<GeneralMetaProgressionManager>();
    }

    private ChaosResearchProgressionManager GetChaosResearchManager()
    {
        GameManager gameManager = GetGameManager();
        if (gameManager != null)
            return gameManager.GetChaosResearchProgressionManager();

        return FindObjectOfType<ChaosResearchProgressionManager>();
    }

    private PathTechniqueProgressionManager GetPathTechniqueManager()
    {
        GameManager gameManager = GetGameManager();
        if (gameManager != null)
            return gameManager.GetPathTechniqueProgressionManager();

        return FindObjectOfType<PathTechniqueProgressionManager>();
    }

    private EliteHuntProgressionManager GetEliteHuntManager()
    {
        GameManager gameManager = GetGameManager();
        if (gameManager != null)
            return gameManager.GetEliteHuntProgressionManager();

        return FindObjectOfType<EliteHuntProgressionManager>();
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        GameManager gameManager = GetGameManager();
        if (gameManager != null)
            return gameManager.GetTowerMasteryManager();

        return FindObjectOfType<TowerMasteryManager>();
    }

    private int GetTowerRoleCount()
    {
        return TowerMasteryManager.GetOrderedTowerRoles().Length;
    }

    private int GetTowerMilestoneCount()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery == null)
            return 0;

        int count = 0;
        foreach (TowerRole role in TowerMasteryManager.GetOrderedTowerRoles())
        {
            if (towerMastery.GetMasteryVisualTier(role) > 0)
                count++;
        }

        return count;
    }

    private int GetTotalUnspentTowerPoints()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
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

    private string GetTowerRoleLine(TowerRole role)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery == null)
            return "Keine Mastery-Daten";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
        int level = towerMastery.GetMasteryLevel(role);
        int xp = towerMastery.GetMasteryXPIntoCurrentLevel(role);
        int next = towerMastery.GetXPToNextMasteryLevel(level);
        int bestLevel = profile != null ? profile.bestLevelEver : 1;
        return "Mastery Lv " + level + " | XP " + xp + "/" + next + " | Bester Run " + bestLevel;
    }

    private string GetTowerRolePoints(TowerRole role)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery == null)
            return "0 frei";

        TowerMasteryRoleProfile profile = towerMastery.GetProfile(role);
        return profile != null ? profile.unspentPoints + " frei" : "0 frei";
    }

    private int GetPurchasedChaosResearchCount()
    {
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchManager();
        if (chaosResearch == null)
            return 0;

        int count = 0;
        foreach (ChaosResearchNodeDefinition definition in chaosResearch.GetDefinitions())
        {
            if (definition != null && chaosResearch.IsNodePurchased(definition.nodeId))
                count++;
        }

        return count;
    }

    private int GetChaosResearchDefinitionCount()
    {
        ChaosResearchProgressionManager chaosResearch = GetChaosResearchManager();
        return chaosResearch != null ? chaosResearch.GetDefinitions().Count : 0;
    }

    private int GetPurchasedPathTechniqueCount()
    {
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueManager();
        if (pathTechnique == null)
            return 0;

        int count = 0;
        foreach (PathTechniqueNodeDefinition definition in pathTechnique.GetDefinitions())
        {
            if (definition != null && pathTechnique.IsNodePurchased(definition.nodeId))
                count++;
        }

        return count;
    }

    private int GetPathTechniqueDefinitionCount()
    {
        PathTechniqueProgressionManager pathTechnique = GetPathTechniqueManager();
        return pathTechnique != null ? pathTechnique.GetDefinitions().Count : 0;
    }

    private int GetCategoryTotal(ChaosUnlockCategory category)
    {
        if (manager == null || manager.unlocks == null)
            return 0;

        int count = 0;
        foreach (ChaosUnlockEntry entry in manager.unlocks)
        {
            if (entry != null && entry.category == category)
                count++;
        }

        return count;
    }

    private List<ChaosUnlockEntry> GetVisibleEntries()
    {
        return manager != null ? manager.GetVisibleUnlocks() : new List<ChaosUnlockEntry>();
    }

    private List<ChaosUnlockEntry> GetEntriesForSection(RuntimeSection section)
    {
        List<ChaosUnlockEntry> visible = GetVisibleEntries();
        if (section == RuntimeSection.Overview)
            return visible;

        ChaosUnlockCategory category = GetCategoryForSection(section);
        List<ChaosUnlockEntry> filtered = new List<ChaosUnlockEntry>();
        foreach (ChaosUnlockEntry entry in visible)
        {
            if (entry != null && entry.category == category)
                filtered.Add(entry);
        }

        return filtered;
    }

    private float GetSectionProgress(RuntimeSection section, out int unlocked, out int total)
    {
        unlocked = 0;
        total = 0;

        if (manager == null || manager.unlocks == null)
            return 0f;

        bool useCategory = section != RuntimeSection.Overview;
        ChaosUnlockCategory category = useCategory ? GetCategoryForSection(section) : ChaosUnlockCategory.Grundlagen;

        foreach (ChaosUnlockEntry entry in manager.unlocks)
        {
            if (entry == null)
                continue;

            if (useCategory && entry.category != category)
                continue;

            total++;

            if (entry.IsUnlocked())
                unlocked++;
        }

        return total > 0 ? Mathf.Clamp01(unlocked / (float)total) : 0f;
    }

    private ChaosUnlockCategory GetCategoryForSection(RuntimeSection section)
    {
        switch (section)
        {
            case RuntimeSection.Grundlagen:
                return ChaosUnlockCategory.Grundlagen;
            case RuntimeSection.RisikoPool:
                return ChaosUnlockCategory.RisikoPool;
            case RuntimeSection.ChaosVarianten:
                return ChaosUnlockCategory.ChaosVarianten;
            case RuntimeSection.ChaosWaves:
                return ChaosUnlockCategory.ChaosWaves;
            case RuntimeSection.Gerechtigkeit:
                return ChaosUnlockCategory.Gerechtigkeit;
            case RuntimeSection.Auswertung:
                return ChaosUnlockCategory.Auswertung;
            case RuntimeSection.Zukunft:
                return ChaosUnlockCategory.Zukunft;
            default:
                return ChaosUnlockCategory.Grundlagen;
        }
    }

    private string GetSectionTitle(RuntimeSection section)
    {
        switch (section)
        {
            case RuntimeSection.Allgemein:
                return "ALLGEMEIN";
            case RuntimeSection.TowerMastery:
                return "TOWER MASTERY";
            case RuntimeSection.ChaosForschung:
                return "CHAOS-FORSCHUNG";
            case RuntimeSection.Pfadtechnik:
                return "PFADTECHNIK";
            case RuntimeSection.EliteJagd:
                return "ELITE-JAGD";
            case RuntimeSection.Grundlagen:
                return "GRUNDLAGEN";
            case RuntimeSection.RisikoPool:
                return "RISIKO-POOL";
            case RuntimeSection.ChaosVarianten:
                return "CHAOS-VARIANTEN";
            case RuntimeSection.ChaosWaves:
                return "CHAOS-WAVES";
            case RuntimeSection.Gerechtigkeit:
                return "GERECHTIGKEIT";
            case RuntimeSection.Auswertung:
                return "AUSWERTUNG";
            case RuntimeSection.Zukunft:
                return "ZUKUNFT";
            default:
                return "UEBERSICHT";
        }
    }

    private string GetSectionSubtitle(RuntimeSection section)
    {
        switch (section)
        {
            case RuntimeSection.Allgemein:
                return "Account, Kernwissen, Content-Unlocks und Loadout als read-only Run-Ansicht.";
            case RuntimeSection.TowerMastery:
                return "Tower-Mastery-Fortschritt, freie Punkte, Milestones und naechste Ziele.";
            case RuntimeSection.ChaosForschung:
                return "Chaos-Wissen, Risskerne, Risiko-Verstaendnis und aktuelle Chaos-Route.";
            case RuntimeSection.Pfadtechnik:
                return "Bauplaene, Verbau-Rettung und Pfadtechnik-Fortschritt.";
            case RuntimeSection.EliteJagd:
                return "Spaeter opt-in Endgame-Bereich mit Elite-Siegeln und Jagdzielen.";
            case RuntimeSection.RisikoPool:
                return "Welche Risiko-Modifikatoren und Chaos-Angebote bereits bekannt sind.";
            case RuntimeSection.ChaosVarianten:
                return "Sichtbare Hinweise zu Chaos-Gegnern und Varianten.";
            case RuntimeSection.ChaosWaves:
                return "Freigeschaltete Bausteine fuer Chaos-Waves und Preview-Hilfen.";
            case RuntimeSection.Gerechtigkeit:
                return "Sichere Ordnungspfade, Gold-/XP-Gerechtigkeit und Auswertungen.";
            case RuntimeSection.Zukunft:
                return "Spaetere Systeme und gesperrte Teaser.";
            default:
                return "Bekannte Inhalte im laufenden Run. Keine Kaeufe, keine Loadout-Aenderungen.";
        }
    }

    private Color GetSectionAccent(RuntimeSection section)
    {
        switch (section)
        {
            case RuntimeSection.TowerMastery:
                return purpleAccentColor;
            case RuntimeSection.ChaosForschung:
                return purpleAccentColor;
            case RuntimeSection.Pfadtechnik:
                return pathAccentColor;
            case RuntimeSection.EliteJagd:
                return chaosAccentColor;
            case RuntimeSection.RisikoPool:
                return purpleAccentColor;
            case RuntimeSection.ChaosVarianten:
                return chaosAccentColor;
            case RuntimeSection.ChaosWaves:
                return pathAccentColor;
            case RuntimeSection.Zukunft:
                return lockedAccentColor;
            default:
                return accentColor;
        }
    }

    private int GetUnlockedCount()
    {
        int count = 0;
        if (manager == null || manager.unlocks == null)
            return count;

        foreach (ChaosUnlockEntry entry in manager.unlocks)
        {
            if (entry != null && entry.IsUnlocked())
                count++;
        }

        return count;
    }

    private int CountUnlockedByCategory(ChaosUnlockCategory category)
    {
        int count = 0;
        if (manager == null || manager.unlocks == null)
            return count;

        foreach (ChaosUnlockEntry entry in manager.unlocks)
        {
            if (entry != null && entry.category == category && entry.IsUnlocked())
                count++;
        }

        return count;
    }

    private int CountLockedVisibleEntries()
    {
        int count = 0;
        foreach (ChaosUnlockEntry entry in GetVisibleEntries())
        {
            if (entry != null && !entry.IsUnlocked())
                count++;
        }

        return count;
    }

    private int GetUnlockedRiskModifierCount()
    {
        int count = 0;
        if (manager == null || manager.unlocks == null)
            return count;

        foreach (ChaosUnlockEntry entry in manager.unlocks)
        {
            if (entry == null || !entry.IsUnlocked() || entry.unlockedRiskModifierNames == null)
                continue;

            count += entry.unlockedRiskModifierNames.Count;
        }

        return count;
    }

    private string GetNextLockedTitle()
    {
        foreach (ChaosUnlockEntry entry in GetVisibleEntries())
        {
            if (entry != null && !entry.IsUnlocked())
                return manager != null ? manager.GetUnlockDisplayTitle(entry) : entry.title;
        }

        return "Keine offenen Teaser";
    }

    private WaveHistory GetWaveHistory()
    {
        return manager != null && manager.gameManager != null ? manager.gameManager.GetWaveHistory() : null;
    }

    private int GetCurrentChaosLevel()
    {
        return manager != null && manager.chaosJusticeManager != null ? manager.chaosJusticeManager.GetChaosLevel() : GetHighestChaosLevel();
    }

    private int GetHighestChaosLevel()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetHighestChaosLevelSeen() : 0;
    }

    private int GetHighestWave()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetHighestWaveNumberReached() : 0;
    }

    private int GetCurrentWaveNumber()
    {
        if (manager != null && manager.gameManager != null)
            return Mathf.Max(0, manager.gameManager.waveNumber);

        return GetHighestWave();
    }

    private int GetCurrentGold()
    {
        return manager != null && manager.gameManager != null ? Mathf.Max(0, manager.gameManager.gold) : 0;
    }

    private int GetCurrentLives()
    {
        return manager != null && manager.gameManager != null ? Mathf.Max(0, manager.gameManager.lives) : 0;
    }

    private int GetActiveRiskModifierCount()
    {
        return manager != null && manager.chaosJusticeManager != null ? manager.chaosJusticeManager.GetActiveRiskModifierCount() : 0;
    }

    private string GetBossWaveProgressText()
    {
        int currentWave = GetCurrentWaveNumber();
        int progress = currentWave % 10;

        if (currentWave > 0 && progress == 0)
            progress = 10;

        return progress + " / 10";
    }

    private int GetBossKills()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetBossKills() : 0;
    }

    private int GetChaosWavesCompleted()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetChaosWavesCompleted() : 0;
    }

    private int GetChaosBlockWavesCompleted()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetChaosWaveBlockWavesCompleted() : 0;
    }

    private int GetHighestGoldJustice()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetHighestGoldJusticeLevelSeen() : 0;
    }

    private int GetHighestXpJustice()
    {
        WaveHistory history = GetWaveHistory();
        return history != null ? history.GetHighestXpJusticeLevelSeen() : 0;
    }

    private string GetGoldRewardMultiplierText()
    {
        if (manager != null && manager.chaosJusticeManager != null)
            return manager.chaosJusticeManager.GetGoldRewardMultiplier().ToString("0.00");

        return "1.00";
    }

    private string GetXpRewardMultiplierText()
    {
        if (manager != null && manager.chaosJusticeManager != null)
            return manager.chaosJusticeManager.GetXPRewardMultiplier().ToString("0.00");

        return "1.00";
    }
}
