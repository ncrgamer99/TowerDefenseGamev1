using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ChaosJusticeResultDebugUI : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public ChaosJusticeManager chaosJusticeManager;
    public RunStatisticsTracker runStatisticsTracker;
    public ChaosUnlockManager chaosUnlockManager;
    public GameUI gameUI;

    [Header("UI")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultTitleText;
    public TextMeshProUGUI resultText;
    public Button closeButton;
    public Button refreshButton;
    public Button restartButton;

    [Header("Optional Theme Images")]
    public Image overlayBackground;
    public Image windowBackground;
    public Image headerBackground;
    public Image footerBackground;

    [Header("Behaviour")]
    public bool showOnGameOver = true;
    public bool closeOnStart = true;
    public bool allowDebugToggleHotkey = true;
    public KeyCode debugToggleKey = KeyCode.F10;
    public bool closeWithEscape = true;
    public bool suppressLegacyGameOverTextWhenClosingAfterGameOver = true;

    [Header("Result Sections V1")]
    public bool showAtmosphericVerdict = true;
    public bool showHighlights = true;
    public bool showRunCoreStats = true;
    public bool showEconomyProgressionSection = true;
    public bool showTopTowerRecords = true;
    public bool showUnlockSection = true;
    public bool showChaosJusticeSection = true;
    public bool showRiskSection = true;
    public bool showChoiceHistory = true;
    public bool showWaveMilestones = true;
    public bool showRecentWaveHistory = true;
    public bool showLastWaveDetails = true;
    public bool showDebugFooter = true;

    [Header("Display Limits")]
    public int maxDisplayedHighlights = 5;
    public int maxDisplayedTowerRecords = 6;
    public int maxDisplayedChoiceRecords = 12;
    public int maxDisplayedRiskModifiers = 20;
    public int maxDisplayedRecentWaves = 8;
    public int maxDisplayedChaosWaveMoments = 6;
    public int maxDisplayedBossTimelineEntries = 8;

    [Header("Risk Display")]
    public bool showRiskGroups = true;
    public bool showDetailedRiskModifiers = true;

    [Header("Theme")]
    public bool applyThemeOnStart = true;
    public Color overlayColor = new Color32(0, 0, 0, 145);
    public Color windowColor = new Color32(20, 24, 31, 248);
    public Color headerColor = new Color32(90, 185, 226, 255);
    public Color footerColor = new Color32(16, 20, 28, 250);
    public Color textPrimaryColor = new Color32(240, 244, 250, 255);
    public Color textSecondaryColor = new Color32(185, 194, 208, 255);
    public Color cardAccentColor = new Color32(214, 164, 65, 255);
    public Color verdictAccentColor = new Color32(165, 70, 255, 255);
    public Color justiceAccentColor = new Color32(90, 185, 226, 255);
    public Color closeButtonColor = new Color32(200, 75, 75, 255);
    public Color refreshButtonColor = new Color32(65, 125, 245, 255);
    public Color restartButtonColor = new Color32(185, 70, 95, 255);

    [Header("Text Sizes")]
    public float titleFontSize = 28f;
    public float resultFontSize = 16f;
    public float sectionTitleFontSize = 18f;
    public float verdictTitleFontSize = 22f;

    private bool resultOpen = false;

    private void OnEnable()
    {
        WaveEventBus.GameOverTriggered += HandleGameOverTriggered;
    }

    private void OnDisable()
    {
        WaveEventBus.GameOverTriggered -= HandleGameOverTriggered;
    }

    private void Start()
    {
        ResolveReferences();
        SetupButtons();

        if (applyThemeOnStart)
            ApplyStaticTheme();

        if (closeOnStart)
            CloseResult();
        else
            RefreshResultText();
    }

    private void Update()
    {
        if (allowDebugToggleHotkey && Input.GetKeyDown(debugToggleKey))
        {
            ToggleResult();
            return;
        }

        if (resultOpen && closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
            CloseResult();
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

        if (runStatisticsTracker == null)
        {
            if (gameManager != null)
                runStatisticsTracker = gameManager.GetRunStatisticsTracker();

            if (runStatisticsTracker == null)
                runStatisticsTracker = FindObjectOfType<RunStatisticsTracker>();
        }

        if (chaosUnlockManager == null)
        {
            if (gameManager != null)
                chaosUnlockManager = gameManager.GetChaosUnlockManager();

            if (chaosUnlockManager == null)
                chaosUnlockManager = FindObjectOfType<ChaosUnlockManager>();
        }

        if (gameUI == null)
            gameUI = FindObjectOfType<GameUI>();
    }

    private void SetupButtons()
    {
        SetupButton(closeButton, CloseResult, "Schließen");
        SetupButton(refreshButton, RefreshResultText, "Aktualisieren");
        SetupButton(restartButton, RestartCurrentScene, "Neustart");
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action, string label)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (text != null)
        {
            text.text = label;
            text.richText = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontSize = 17f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }
    }

    private void HandleGameOverTriggered()
    {
        if (!showOnGameOver)
            return;

        OpenResult();
    }

    public void ToggleResult()
    {
        if (resultOpen)
            CloseResult();
        else
            OpenResult();
    }

    public void OpenResult()
    {
        ResolveReferences();
        resultOpen = true;

        if (resultPanel != null)
            resultPanel.SetActive(true);
        else if (resultText != null)
            resultText.gameObject.SetActive(true);

        SuppressLegacyGameOverTextIfNeeded();
        RefreshResultText();
    }

    public void CloseResult()
    {
        resultOpen = false;

        if (resultPanel != null)
            resultPanel.SetActive(false);
        else if (resultText != null)
            resultText.gameObject.SetActive(false);

        SuppressLegacyGameOverTextIfNeeded();
    }

    private void SuppressLegacyGameOverTextIfNeeded()
    {
        if (!suppressLegacyGameOverTextWhenClosingAfterGameOver)
            return;

        ResolveReferences();

        if (gameManager == null || !gameManager.isGameOver)
            return;

        if (gameUI != null)
            gameUI.SuppressLegacyGameOverText();
    }

    public void RefreshResultText()
    {
        ResolveReferences();

        if (applyThemeOnStart)
            ApplyStaticTheme();

        if (resultTitleText != null)
            resultTitleText.text = BuildHeaderTitle();

        string text = BuildResultText();

        if (resultText != null)
            resultText.text = text;
        else
            Debug.Log(text);
    }

    private string BuildHeaderTitle()
    {
        if (chaosJusticeManager == null)
            return "CHAOS / GERECHTIGKEIT - RUN-AUSWERTUNG";

        return chaosJusticeManager.GetRunStyleLabel().ToUpperInvariant() + " - RUN-AUSWERTUNG";
    }

    private string BuildResultText()
    {
        string text = "";

        if (showAtmosphericVerdict)
            text += BuildVerdictText();

        if (showHighlights)
            text += SectionTitle("HIGHLIGHTS") + BuildHighlightsText();

        if (showRunCoreStats)
            text += SectionTitle("RUN") + BuildRunText();

        if (showEconomyProgressionSection)
            text += SectionTitle("WIRTSCHAFT / TOWER-PROGRESSION") + BuildEconomyProgressionText();

        if (showUnlockSection)
            text += SectionTitle("FREISCHALTUNGEN") + BuildUnlockText();

        if (showChaosJusticeSection)
            text += SectionTitle("CHAOS / GERECHTIGKEIT") + BuildChaosJusticeText();

        if (showRiskSection && (showRiskGroups || showDetailedRiskModifiers))
            text += SectionTitle("AKTIVE RISIKEN") + BuildRiskModifierText();

        if (showChoiceHistory)
            text += BuildChoiceHistoryText();

        if (showWaveMilestones)
            text += SectionTitle("WICHTIGE WAVE-MOMENTE") + BuildWaveMilestonesText();

        if (showRecentWaveHistory)
            text += SectionTitle("LETZTE WAVES") + BuildRecentWaveHistoryText();

        text += SectionTitle("WAVE-HISTORY") + BuildWaveHistoryText();

        if (showLastWaveDetails)
            text += SectionTitle("LETZTE WAVE - DETAILS") + BuildLastWaveText();

        if (showDebugFooter)
            text += "\n<size=13><color=#" + ColorUtility.ToHtmlStringRGB(textSecondaryColor) + ">Debug-Hotkey: " + debugToggleKey + " öffnet/schließt diese Ansicht. Ergebnisdaten basieren auf gespeicherten Wave-Snapshots.</color></size>";

        return text;
    }

    private string SectionTitle(string title)
    {
        return "\n<size=" + sectionTitleFontSize.ToString("0") + "><color=#" + ColorUtility.ToHtmlStringRGB(cardAccentColor) + "><b>" + title + "</b></color></size>\n";
    }

    private string BuildVerdictText()
    {
        string title = BuildAtmosphericRunTitle();
        string rankLabel = BuildRankLabel();
        string verdictLine = BuildAtmosphericVerdictLine();
        string style = chaosJusticeManager != null ? chaosJusticeManager.GetRunStyleLabel() : "Unbekannter Run";

        return
            "<size=" + verdictTitleFontSize.ToString("0") + "><color=#" + ColorUtility.ToHtmlStringRGB(verdictAccentColor) + "><b>" + title + "</b></color></size>\n" +
            "<b>" + rankLabel + "</b> | Run-Typ: " + style + "\n" +
            "<color=#" + ColorUtility.ToHtmlStringRGB(textSecondaryColor) + ">" + verdictLine + "</color>\n";
    }

    private string BuildAtmosphericRunTitle()
    {
        ChaosJusticeRunData data = chaosJusticeManager != null ? chaosJusticeManager.runData : null;
        WaveHistory history = GetHistory();

        int highestChaos = data != null ? data.highestChaosLevel : history != null ? history.GetHighestChaosLevelSeen() : 0;
        int justice = data != null ? data.goldJusticeLevel + data.xpJusticeLevel : 0;
        int bossKills = history != null ? history.GetBossKills() : 0;
        int wave = gameManager != null ? gameManager.waveNumber : history != null ? history.GetHighestWaveNumberReached() : 0;

        if (highestChaos >= 5)
            return "Vom Riss gezeichneter Pfadbrecher";

        if (highestChaos >= 3 && bossKills >= 2)
            return "Chaosgeprüfter Wegformer";

        if (highestChaos >= 1 && justice >= highestChaos)
            return "Wanderer zwischen Ordnung und Riss";

        if (highestChaos <= 0 && justice > 0)
            return "Standhafter Hüter der Ordnung";

        if (wave >= 30)
            return "Langstrecken-Verteidiger";

        if (bossKills > 0)
            return "Bossbrecher der ersten Ordnung";

        return "Verteidiger der entstehenden Linie";
    }

    private string BuildRankLabel()
    {
        string focus = "Run-Wertung";

        if (chaosJusticeManager != null)
        {
            int chaosChoices = chaosJusticeManager.GetChaosChoiceCount();
            int justiceChoices = chaosJusticeManager.GetJusticeChoiceCount();

            if (chaosChoices > justiceChoices)
                focus = "Chaos-Wertung";
            else if (justiceChoices > chaosChoices)
                focus = "Stabilitätsrang";
        }

        return focus + ": " + CalculateRankLetter();
    }

    private string CalculateRankLetter()
    {
        WaveHistory history = GetHistory();
        ChaosJusticeRunData data = chaosJusticeManager != null ? chaosJusticeManager.runData : null;

        int score = 0;
        int wave = gameManager != null ? gameManager.waveNumber : history != null ? history.GetHighestWaveNumberReached() : 0;
        int lives = gameManager != null ? Mathf.Max(0, gameManager.lives) : 0;

        score += wave * 2;

        if (history != null)
        {
            score += history.GetBossKills() * 18;
            score += history.GetMiniBossKills() * 7;
            score += Mathf.RoundToInt(history.GetOverallKillPercent() * 30f);
            score += history.GetChaosWavesCompleted() * 2;
            score += history.GetChaosWaveBlockWavesCompleted() * 2;
            score += history.GetTotalChaosVariantKills();
            score -= history.GetTotalBaseDamageTaken() * 2;
            score -= history.GetTotalLeaks();
        }

        if (data != null)
        {
            score += data.highestChaosLevel * 8;
            score += (data.goldJusticeLevel + data.xpJusticeLevel) * 4;
            score += chaosJusticeManager.GetHighestRiskModifierLevel() * 3;
        }

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
        {
            score += stats.highestTowerLevelReached * 3;
            score += stats.totalTowerLevelUps * 2;
            score += stats.totalGoldUpgradesPurchased;
            score += stats.totalPointUpgradesPurchased * 2;
            score += Mathf.RoundToInt(stats.economy.totalGoldEarned * 0.02f);
        }

        score += lives;

        if (score >= 170)
            return "S";
        if (score >= 135)
            return "A";
        if (score >= 105)
            return "B+";
        if (score >= 80)
            return "B";
        if (score >= 55)
            return "C+";
        if (score >= 35)
            return "C";
        return "D";
    }

    private string BuildAtmosphericVerdictLine()
    {
        WaveHistory history = GetHistory();
        ChaosJusticeRunData data = chaosJusticeManager != null ? chaosJusticeManager.runData : null;
        int highestChaos = data != null ? data.highestChaosLevel : history != null ? history.GetHighestChaosLevelSeen() : 0;
        int totalJustice = data != null ? data.goldJusticeLevel + data.xpJusticeLevel : 0;
        bool gameOver = gameManager != null && gameManager.isGameOver;

        if (highestChaos >= 5)
            return gameOver ? "Das Chaos erreichte seinen normalen Extremzustand und forderte am Ende seinen Preis." : "Der Run steht noch, obwohl der Riss bereits seinen normalen Extremzustand erreicht hat.";

        if (highestChaos >= 3)
            return "Das Chaos wurde sichtbar: Varianten, Wave-Bausteine und Risiko-Modifikatoren prägten den Verlauf.";

        if (highestChaos > 0)
            return "Du hast das Chaos bewusst geöffnet, aber der Run blieb noch nachvollziehbar kontrollierbar.";

        if (totalJustice > 0)
            return "Die Ordnung wurde gestärkt. Dieser Run setzte auf planbare Sicherheit statt auf Rissdruck.";

        return "Der Run blieb nahe an der stabilen Baseline und liefert vor allem Vergleichsdaten.";
    }

    private string BuildHighlightsText()
    {
        List<string> highlights = CollectHighlights();

        if (highlights.Count == 0)
            return "Keine besonderen Highlights gespeichert.\n";

        int safeMax = Mathf.Max(1, maxDisplayedHighlights);
        string text = "";

        for (int i = 0; i < highlights.Count && i < safeMax; i++)
            text += "- " + highlights[i] + "\n";

        return text;
    }

    private List<string> CollectHighlights()
    {
        List<string> highlights = new List<string>();
        WaveHistory history = GetHistory();
        ChaosJusticeRunData data = chaosJusticeManager != null ? chaosJusticeManager.runData : null;

        if (data != null && data.highestChaosLevel > 0)
            highlights.Add("Höchstes Chaos-Level: " + data.highestChaosLevel + " / " + data.maxChaosLevel);

        if (data != null && data.goldJusticeLevel + data.xpJusticeLevel > 0)
            highlights.Add("Gerechtigkeit aufgebaut: Gold " + data.goldJusticeLevel + " | XP " + data.xpJusticeLevel);

        if (history != null && history.GetBossKills() > 0)
            highlights.Add("Bosskills: " + history.GetBossKills() + " von " + history.GetBossWavesCompleted() + " abgeschlossenen Boss-Waves");

        if (history != null && history.GetChaosWaveBlockWavesCompleted() > 0)
            highlights.Add("Chaos-Wave-Bausteine überstanden: " + history.GetTotalChaosWaveBlocksSeen() + " in " + history.GetChaosWaveBlockWavesCompleted() + " Wave(s)");

        if (history != null && history.GetTotalChaosVariantSpawns() > 0)
            highlights.Add("Chaos-Varianten: " + history.GetTotalChaosVariantKills() + " Kills / " + history.GetTotalChaosVariantSpawns() + " Spawns");

        if (chaosJusticeManager != null && chaosJusticeManager.GetStrongestRiskModifier() != null)
            highlights.Add("Stärkster Risiko-Modifikator: " + chaosJusticeManager.GetStrongestRiskModifier().GetDisplayNameWithLevel());

        if (history != null)
        {
            WaveCompletionResult closeWave = history.GetClosestSurvivedWave();
            if (closeWave != null)
                highlights.Add("Knappste überstandene Wave: " + closeWave.GetShortWaveLabel() + " mit " + closeWave.baseDamageTaken + " Base-Schaden");
        }

        RunStatisticsTracker stats = GetRunStatisticsTracker();
        if (stats != null)
        {
            if (stats.economy.totalGoldEarned > 0)
                highlights.Add("Wirtschaft: " + stats.economy.totalGoldEarned + " Gold verdient / " + stats.economy.totalGoldSpent + " ausgegeben");

            if (stats.totalTowerLevelUps > 0)
                highlights.Add("Tower-Progression: " + stats.totalTowerLevelUps + " Level-Ups | höchste Tower-Stufe " + stats.highestTowerLevelReached);

            RunTowerStatsRecord strongestTower = stats.GetStrongestTowerRecord();
            if (strongestTower != null && (strongestTower.totalKills > 0 || strongestTower.totalDamageDealt > 0f))
                highlights.Add("Stärkster Tower: " + strongestTower.towerName + " | Lvl " + strongestTower.highestLevel + " | Kills " + strongestTower.totalKills);
        }

        if (chaosUnlockManager != null)
        {
            int unlockedCount = chaosUnlockManager.GetUnlockedCount();
            int newlyUnlocked = chaosUnlockManager.GetNewlyUnlockedThisSessionCount();

            if (newlyUnlocked > 0)
                highlights.Add("Neue Freischaltungen: " + newlyUnlocked + " Inhalt(e) im Content-Pool");
            else if (unlockedCount > 0)
                highlights.Add("Freigeschaltete Inhalte: " + unlockedCount + " Content-Pool-Einträge verfügbar");
        }

        if (data != null && data.highestChaosLevel <= 0 && data.goldJusticeLevel + data.xpJusticeLevel <= 0 && history != null)
            highlights.Add("Stabiler Vergleichsrun bis Wave " + history.GetHighestWaveNumberReached());

        return highlights;
    }

    private string BuildRunText()
    {
        if (gameManager == null)
            return "GameManager nicht verbunden.\n";

        WaveHistory history = GetHistory();
        string status = gameManager.isGameOver ? "Game Over" : "Aktiv";
        string text =
            "Status: <b>" + status + "</b>\n" +
            "Wave erreicht: <b>" + gameManager.waveNumber + "</b>\n" +
            "Gold: " + gameManager.gold + " | Lives: " + gameManager.lives + "\n";

        if (history != null)
        {
            text +=
                "Abgeschlossene Waves: " + history.GetCompletedWaveCount() + " | Höchste gespeicherte Wave: " + history.GetHighestWaveNumberReached() + "\n" +
                "Killrate: " + Mathf.RoundToInt(history.GetOverallKillPercent() * 100f) + "% | Spawns: " + history.GetTotalSpawnedEnemies() + "\n";
        }

        return text;
    }

    private string BuildEconomyProgressionText()
    {
        RunStatisticsTracker stats = GetRunStatisticsTracker();

        if (stats == null)
            return "RunStatisticsTracker nicht verbunden.\n";

        string text = stats.GetEconomySummaryText() + "\n" + stats.GetTowerProgressionSummaryText();

        if (showTopTowerRecords)
            text += "\nTop-Tower:\n" + stats.GetTopTowerRecordsText(maxDisplayedTowerRecords);

        return text;
    }

    private string BuildUnlockText()
    {
        ResolveReferences();

        if (chaosUnlockManager == null)
            return "Freischaltungsdaten nicht verfügbar.\n";

        return chaosUnlockManager.GetUnlockSummaryText() + "\n" + chaosUnlockManager.GetDetailedUnlockSummaryText(8) + "\n";
    }

    private string BuildChaosJusticeText()
    {
        if (chaosJusticeManager == null || chaosJusticeManager.runData == null)
            return "ChaosJusticeManager nicht verbunden.\n";

        ChaosJusticeRunData data = chaosJusticeManager.runData;
        ChaosJusticeBalanceSnapshot balance = chaosJusticeManager.GetBalanceSnapshot();

        return
            "Aktuelles Chaos-Level: <b>" + data.chaosLevel + " / " + data.maxChaosLevel + "</b>\n" +
            "Höchstes Chaos-Level: " + data.highestChaosLevel + "\n" +
            "Run-Typ: " + chaosJusticeManager.GetRunStyleLabel() + "\n" +
            "Entscheidungen: " + chaosJusticeManager.GetDecisionMixText() + "\n" +
            "Balance: <b>" + balance.barText + " " + balance.label + "</b>\n" +
            "Sicherheit: " + balance.safetyScore + " (" + balance.safetyPercent + "%)" +
            " | Chaos: " + balance.chaosScore + " (" + balance.chaosPercent + "%)\n" +
            "Gold-Gerechtigkeit: " + data.goldJusticeLevel + " | XP-Gerechtigkeit: " + data.xpJusticeLevel + "\n" +
            "Gold-Reward: x" + chaosJusticeManager.GetGoldRewardMultiplier().ToString("0.00") +
            " | XP-Reward: x" + chaosJusticeManager.GetXPRewardMultiplier().ToString("0.00") + "\n" +
            "Überstandene Chaos-Waves: " + data.chaosWavesSurvived + "\n" +
            "Boss-Entscheidungen geöffnet/gelöst: " + data.bossChoicesOpened + " / " + data.bossChoicesResolved + "\n" +
            "Stärkstes aktives Risiko: " + chaosJusticeManager.GetStrongestRiskModifierText() + "\n";
    }

    private string BuildRiskModifierText()
    {
        if (chaosJusticeManager == null || chaosJusticeManager.runData == null)
            return "ChaosJusticeManager nicht verbunden.\n";

        string text = "";

        if (showRiskGroups)
            text += "Gruppen:\n" + chaosJusticeManager.GetGroupedRiskModifierSummary() + "\n";

        if (showDetailedRiskModifiers)
        {
            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "Details:\n" + chaosJusticeManager.GetDetailedRiskModifierText(maxDisplayedRiskModifiers) + "\n";
        }

        return string.IsNullOrEmpty(text) ? "Keine Risikoanzeige aktiv.\n" : text;
    }

    private string BuildChoiceHistoryText()
    {
        if (chaosJusticeManager == null || chaosJusticeManager.runData == null || chaosJusticeManager.runData.choiceHistory == null)
            return "";

        List<ChaosJusticeChoiceRecord> records = chaosJusticeManager.runData.choiceHistory;

        if (records.Count == 0)
            return SectionTitle("BOSS-ENTSCHEIDUNGEN") + "Keine Entscheidung getroffen.\n";

        int safeMax = Mathf.Max(1, maxDisplayedChoiceRecords);
        int startIndex = Mathf.Max(0, records.Count - safeMax);
        string text = SectionTitle("BOSS-ENTSCHEIDUNGEN");

        for (int i = startIndex; i < records.Count; i++)
        {
            ChaosJusticeChoiceRecord record = records[i];

            if (record == null)
                continue;

            text +=
                "- Wave " + record.afterBossWaveNumber + ": <b>" + record.displayName + "</b>" +
                " | Bosskill " + (record.bossDefeatedBeforeChoice ? "Ja" : "Nein") +
                " | Chaos " + record.chaosLevelAfterChoice +
                " | Gold-G " + record.goldJusticeLevelAfterChoice +
                " | XP-G " + record.xpJusticeLevelAfterChoice;

            if (record.noModifierChosen)
                text += " | Risiko: Kein Modifikator";
            else if (!string.IsNullOrEmpty(record.modifierName))
                text += " | Risiko: " + record.modifierName + " (Stufe " + Mathf.Max(0, record.modifierLevelAfterChoice) + ")";

            text += "\n";
        }

        if (records.Count > safeMax)
            text += "... " + (records.Count - safeMax) + " ältere Entscheidung(en) ausgeblendet.\n";

        return text;
    }

    private string BuildWaveMilestonesText()
    {
        WaveHistory history = GetHistory();

        if (history == null || history.GetCompletedWaveCount() == 0)
            return "Keine Wave-Momente gespeichert.\n";

        string text = "";
        AppendWaveMoment(ref text, "Gefährlichste Wave", history.GetMostDangerousWave(), true);
        AppendWaveMoment(ref text, "Prägendste Wave", history.GetMostImpactfulWave(), false);
        AppendWaveMoment(ref text, "Knappste überstandene Wave", history.GetClosestSurvivedWave(), true);
        AppendWaveMoment(ref text, "Stärkste Chaos-Varianten-Wave", history.GetHighestChaosVariantWave(), false);
        AppendWaveMoment(ref text, "Stärkste Chaos-Baustein-Wave", history.GetHighestChaosWaveBlockWave(), false);

        text += "\nBoss-Timeline:\n" + history.GetBossTimelineSummary(maxDisplayedBossTimelineEntries) + "\n";
        text += "\nChaos-Wave-Momente:\n" + history.GetChaosWaveBlockHistorySummary(maxDisplayedChaosWaveMoments) + "\n";

        return text;
    }

    private void AppendWaveMoment(ref string text, string label, WaveCompletionResult result, bool includeDangerScore)
    {
        if (result == null)
            return;

        text += "- " + label + ": " + result.GetCompactSummaryLine();

        if (includeDangerScore)
            text += " | Gefahr " + result.GetDangerScore();

        text += "\n";
    }

    private string BuildRecentWaveHistoryText()
    {
        WaveHistory history = GetHistory();

        if (history == null)
            return "WaveHistory nicht verfügbar.\n";

        return history.GetRecentWaveSummary(maxDisplayedRecentWaves) + "\n";
    }

    private string BuildWaveHistoryText()
    {
        WaveHistory history = GetHistory();

        if (history == null)
            return "WaveHistory nicht verfügbar.\n";

        return
            "Abgeschlossene Waves: <b>" + history.GetCompletedWaveCount() + "</b>\n" +
            "Kills: " + history.GetTotalKills() + " | Leaks: " + history.GetTotalLeaks() + " | Base Damage: " + history.GetTotalBaseDamageTaken() + "\n" +
            "Bosskills: " + history.GetBossKills() + " | MiniBoss-Kills: " + history.GetMiniBossKills() + " | MiniBoss-Waves: " + history.GetMiniBossWavesCompleted() + "\n" +
            "Chaos-Waves überstanden: " + history.GetChaosWavesCompleted() + " | Höchstes gespeichertes Chaos: " + history.GetHighestChaosLevelSeen() + "\n" +
            "Chaos-Wave-Bausteine: Waves " + history.GetChaosWaveBlockWavesCompleted() +
            " | Bausteine " + history.GetTotalChaosWaveBlocksSeen() + "\n" +
            "Chaos-Varianten: Kills " + history.GetTotalChaosVariantKills() + " / Spawns " + history.GetTotalChaosVariantSpawns() +
            " | Leaks " + history.GetTotalChaosVariantLeaks() + "\n" +
            "Max. Risiko-Snapshot: " + history.GetMaxActiveRiskModifierCountSeen() + " aktive Risiken | Höchste Gold-G " + history.GetHighestGoldJusticeLevelSeen() + " | Höchste XP-G " + history.GetHighestXpJusticeLevelSeen() + "\n";
    }

    private string BuildLastWaveText()
    {
        if (gameManager == null)
            return "GameManager nicht verbunden.\n";

        WaveCompletionResult lastResult = gameManager.GetLastCompletedWaveResult();

        if (lastResult == null)
            return "Keine abgeschlossene Wave.\n";

        string text =
            lastResult.GetShortWaveLabel() + "\n" +
            "Ausgang: <b>" + lastResult.GetOutcomeText() + "</b>\n" +
            "Kills: " + lastResult.enemiesKilled + " / " + lastResult.totalSpawnCount +
            " (" + Mathf.RoundToInt(lastResult.GetKillPercent() * 100f) + "%)" +
            " | Leaks: " + lastResult.enemiesReachedBase +
            " | Base Damage: " + lastResult.baseDamageTaken + "\n" +
            "Boss-Wave: " + lastResult.isBossWave + " | Boss besiegt: " + lastResult.bossDefeated + "\n" +
            "Snapshot: " + lastResult.GetChaosSnapshotText() + "\n" +
            "Chaos-Wave: " + (lastResult.hadChaosWaveBlocksAtWaveStart ? lastResult.chaosWaveNameAtWaveStart : "Keine") + "\n" +
            "Chaos-Wave-Bausteine: " + lastResult.GetChaosWaveBlockDetailsText() + "\n" +
            "Chaos-Varianten: " + lastResult.GetChaosVariantDetailsText() + "\n" +
            "Rollen:\n" + lastResult.GetRoleBreakdownText() + "\n" +
            "Risiken: " + (string.IsNullOrEmpty(lastResult.activeRiskModifierSummary) ? "Keine" : lastResult.activeRiskModifierSummary) + "\n";

        if (lastResult.activeRiskModifierCountAtWaveStart > 0)
            text += "Risiko-Details bei Wave-Start:\n" + lastResult.GetActiveRiskModifierDetailsText(12) + "\n";

        return text;
    }

    private RunStatisticsTracker GetRunStatisticsTracker()
    {
        ResolveReferences();
        return runStatisticsTracker;
    }

    private WaveHistory GetHistory()
    {
        if (gameManager == null)
            return null;

        return gameManager.GetWaveHistory();
    }

    private void ApplyStaticTheme()
    {
        SetImageColor(overlayBackground, overlayColor);
        SetImageColor(windowBackground, windowColor);
        SetImageColor(headerBackground, headerColor);
        SetImageColor(footerBackground, footerColor);

        if (resultTitleText != null)
        {
            resultTitleText.richText = true;
            resultTitleText.enableWordWrapping = false;
            resultTitleText.overflowMode = TextOverflowModes.Ellipsis;
            resultTitleText.fontSize = titleFontSize;
            resultTitleText.alignment = TextAlignmentOptions.Center;
            resultTitleText.color = Color.white;
        }

        if (resultText != null)
        {
            resultText.richText = true;
            resultText.enableWordWrapping = true;
            resultText.overflowMode = TextOverflowModes.Overflow;
            resultText.fontSize = resultFontSize;
            resultText.alignment = TextAlignmentOptions.TopLeft;
            resultText.color = textPrimaryColor;
            resultText.margin = new Vector4(14f, 10f, 14f, 10f);
            resultText.raycastTarget = false;
        }

        ApplyButtonTheme(closeButton, closeButtonColor);
        ApplyButtonTheme(refreshButton, refreshButtonColor);
        ApplyButtonTheme(restartButton, restartButtonColor);
    }

    private void ApplyButtonTheme(Button button, Color baseColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();

        if (image != null)
        {
            image.color = baseColor;
            button.targetGraphic = image;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = LightenColor(baseColor, 1.12f);
        colors.pressedColor = DarkenColor(baseColor, 0.82f);
        colors.selectedColor = baseColor;
        colors.disabledColor = new Color32(55, 64, 80, 255);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private void SetImageColor(Image image, Color color)
    {
        if (image != null)
            image.color = color;
    }

    private Color LightenColor(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }

    private Color DarkenColor(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }

    private void RestartCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
