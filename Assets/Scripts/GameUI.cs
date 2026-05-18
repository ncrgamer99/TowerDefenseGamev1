using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public TileManager tileManager;
    public ChaosJusticeManager chaosJusticeManager;
    public TextMeshProUGUI statsText;
    public GameObject gameOverText;

    [Header("Game Over Display")]
    public bool showLegacyGameOverText = false;
    public bool suppressLegacyGameOverTextAfterResultClose = true;

    [Header("Main HUD - Top Left")]
    public bool forceMinimalStatsText = true;
    public bool applyStatsTextDefaultsOnStart = true;
    public bool applyStatsRectDefaultsOnStart = true;
    public float statsFontSize = 18f;
    public Vector2 statsAnchoredPosition = new Vector2(12f, -12f);
    public Vector2 statsSize = new Vector2(320f, 92f);
    public bool showPhaseInMainHud = false;
    public bool showTemporaryWaveMessageInStats = false;
    public bool showBlockedInfoInStats = false;
    public bool showBuildWarningsInStats = false;

    [Header("Auto-Created HUD Panels")]
    public bool autoCreateMissingHudPanels = true;
    public bool autoCreateChaosJusticeHudPanel = true;
    public bool autoCreateNextWavePreviewPanel = true;
    public Color hudPanelColor = new Color32(20, 24, 31, 225);
    public Color previewPanelColor = new Color32(22, 39, 60, 230);

    [Header("Chaos/Gerechtigkeit HUD - Optional Separate Section")]
    public GameObject chaosJusticeHudPanel;
    public TextMeshProUGUI chaosJusticeHudText;
    public bool showCompactChaosJusticeSummary = true;
    public bool showChaosJusticeOnlyDuringBossChoice = true;
    public bool appendChaosJusticeToStatsWhenNoSeparateText = true;
    public bool showChaosBalanceBarInStats = true;
    public bool showRiskGroupsInCompactSummary = true;
    public bool showDetailedRiskListInStats = false;
    public int maxHudRiskDetails = 3;
    public float chaosJusticeHudFontSize = 16f;
    public bool showFullChaosJusticeDebug = false;
    public bool showChaosChoiceOptionsInStatsText = false;

    [Header("Next Wave Preview - Optional")]
    public GameObject nextWavePreviewPanel;
    public TextMeshProUGUI nextWavePreviewText;
    public bool showNextWavePreviewInStats = false;
    public bool showNextWavePreviewInSeparateText = true;
    public bool showBlockedInfoInPreview = true;
    public bool showBuildWarningsInPreview = true;
    public bool hideNextWavePreviewDuringPathChoice = true;
    public float nextWavePreviewFontSize = 15f;
    public Vector2 nextWavePreviewPanelSize = new Vector2(390f, 250f);
    public bool autoCompactNextWavePreviewPanel = true;
    public bool autoResizeNextWavePreviewHeight = true;
    public Vector2 compactNextWavePreviewPanelSize = new Vector2(320f, 140f);
    public float nextWavePreviewMinHeight = 120f;
    public float nextWavePreviewMaxHeight = 420f;
    public float nextWavePreviewHeightPadding = 28f;

    [Header("Wave Messages")]
    public float waveMessageDuration = 3f;
    public float specialWaveMessageDuration = 5f;

    [Header("Optional Big Wave Alert")]
    public GameObject waveAlertPanel;
    public TextMeshProUGUI waveAlertText;

    [Header("Alert Colors")]
    public Color normalWaveColor = Color.white;
    public Color miniBossWaveColor = new Color32(255, 170, 60, 255);
    public Color bossWaveColor = new Color32(255, 70, 70, 255);
    public Color blockedWaveColor = new Color32(255, 90, 90, 255);

    private int lastWaveNumber = -1;
    private GamePhase lastPhase;
    private bool hasInitialState = false;

    private string temporaryWaveMessage = "";
    private float temporaryWaveMessageTimer = 0f;

    private string bigAlertMessage = "";
    private float bigAlertTimer = 0f;
    private Color currentAlertColor = Color.white;
    private bool legacyGameOverTextSuppressed = false;

    private void Start()
    {
        ResolveReferences();

        if (applyStatsTextDefaultsOnStart)
            ApplyStatsTextDefaults();

        EnsureAutoHudPanelsIfNeeded();
        ApplyOptionalHudTextDefaults();

        if (gameOverText != null && !showLegacyGameOverText)
            gameOverText.SetActive(false);

        HideOptionalPanel(chaosJusticeHudPanel, chaosJusticeHudText);
        HideOptionalPanel(nextWavePreviewPanel, nextWavePreviewText);
    }

    private void Update()
    {
        if (gameManager == null)
            ResolveReferences();

        if (gameManager == null)
            return;

        DetectWaveStateChanges();
        UpdateMessageTimers();
        UpdateStatsText();
        UpdateChaosJusticeHud();
        UpdateNextWavePreviewHud();
        UpdateBigAlertUI();
        UpdateGameOverText();
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (tileManager == null && gameManager != null)
            tileManager = gameManager.tileManager;

        if (chaosJusticeManager == null && gameManager != null)
            chaosJusticeManager = gameManager.GetChaosJusticeManager();

        if (chaosJusticeManager == null)
            chaosJusticeManager = FindObjectOfType<ChaosJusticeManager>();
    }

    private void ApplyStatsTextDefaults()
    {
        if (statsText == null)
            return;

        statsText.richText = true;
        statsText.enableWordWrapping = false;
        statsText.overflowMode = TextOverflowModes.Overflow;
        statsText.fontSize = statsFontSize;
        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.color = Color.white;
        statsText.margin = new Vector4(8f, 8f, 8f, 8f);
        statsText.raycastTarget = false;

        if (applyStatsRectDefaultsOnStart)
            ApplyTopLeftRectDefaults(statsText.rectTransform, statsAnchoredPosition, statsSize);
    }

    private void EnsureAutoHudPanelsIfNeeded()
    {
        if (!autoCreateMissingHudPanels || statsText == null)
            return;

        Transform parent = statsText.transform.parent;

        if (parent == null)
            return;

        if (autoCreateChaosJusticeHudPanel && chaosJusticeHudText == null)
        {
            GameObject panel = CreateHudPanel(parent, "ChaosJusticeMiniPanel", new Vector2(12f, -112f), new Vector2(460f, 190f), hudPanelColor);
            TextMeshProUGUI text = CreateHudText(panel.transform, "ChaosJusticeMiniText", chaosJusticeHudFontSize);
            chaosJusticeHudPanel = panel;
            chaosJusticeHudText = text;
        }

        if (autoCreateNextWavePreviewPanel && nextWavePreviewText == null)
        {
            GameObject panel = CreateHudPanel(parent, "NextWavePreviewPanel", new Vector2(12f, -112f), nextWavePreviewPanelSize, previewPanelColor);
            TextMeshProUGUI text = CreateHudText(panel.transform, "NextWavePreviewText", nextWavePreviewFontSize);
            nextWavePreviewPanel = panel;
            nextWavePreviewText = text;
        }
    }

    private GameObject CreateHudPanel(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return panel;
    }

    private TextMeshProUGUI CreateHudText(Transform parent, string objectName, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 8f);
        rect.offsetMax = new Vector2(-10f, -8f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        text.text = "";

        return text;
    }

    private void ApplyOptionalHudTextDefaults()
    {
        ApplyNextWavePreviewPanelLayout();
        DisableRaycastBlocking(nextWavePreviewPanel);

        if (chaosJusticeHudText != null)
        {
            chaosJusticeHudText.richText = true;
            chaosJusticeHudText.enableWordWrapping = true;
            chaosJusticeHudText.overflowMode = TextOverflowModes.Overflow;
            chaosJusticeHudText.fontSize = chaosJusticeHudFontSize;
            chaosJusticeHudText.alignment = TextAlignmentOptions.TopLeft;
            chaosJusticeHudText.color = Color.white;
            chaosJusticeHudText.margin = new Vector4(10f, 8f, 10f, 8f);
            chaosJusticeHudText.raycastTarget = false;
        }

        if (nextWavePreviewText != null)
        {
            nextWavePreviewText.richText = true;
            nextWavePreviewText.enableWordWrapping = true;
            nextWavePreviewText.overflowMode = TextOverflowModes.Overflow;
            nextWavePreviewText.fontSize = nextWavePreviewFontSize;
            nextWavePreviewText.alignment = TextAlignmentOptions.TopLeft;
            nextWavePreviewText.color = Color.white;
            nextWavePreviewText.margin = new Vector4(10f, 8f, 10f, 8f);
            nextWavePreviewText.raycastTarget = false;
        }
    }

    private void ApplyTopLeftRectDefaults(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void HideOptionalPanel(GameObject panel, TextMeshProUGUI fallbackText)
    {
        if (panel != null)
            panel.SetActive(false);
        else if (fallbackText != null)
            fallbackText.gameObject.SetActive(false);
    }

    private void SetOptionalPanelVisible(GameObject panel, TextMeshProUGUI fallbackText, bool visible)
    {
        if (panel != null)
        {
            DisableRaycastBlocking(panel);
            panel.SetActive(visible);
        }
        else if (fallbackText != null)
        {
            fallbackText.raycastTarget = false;
            fallbackText.gameObject.SetActive(visible);
        }
    }

    private void DisableRaycastBlocking(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        Graphic[] graphics = rootObject.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic != null)
                graphic.raycastTarget = false;
        }

        CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = rootObject.AddComponent<CanvasGroup>();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void DetectWaveStateChanges()
    {
        if (!hasInitialState)
        {
            hasInitialState = true;
            lastWaveNumber = gameManager.waveNumber;
            lastPhase = gameManager.currentPhase;
            return;
        }

        bool waveNumberChanged = gameManager.waveNumber != lastWaveNumber;
        bool phaseChanged = gameManager.currentPhase != lastPhase;

        if (gameManager.isGameOver)
        {
            lastWaveNumber = gameManager.waveNumber;
            lastPhase = gameManager.currentPhase;
            return;
        }

        if (gameManager.currentPhase == GamePhase.Wave && (phaseChanged || waveNumberChanged))
            ShowWaveStartedMessage(gameManager.waveNumber);

        if (lastPhase == GamePhase.Wave && gameManager.currentPhase == GamePhase.Build)
            ShowWaveFinishedMessage(lastWaveNumber);

        lastWaveNumber = gameManager.waveNumber;
        lastPhase = gameManager.currentPhase;
    }

    private void ShowWaveStartedMessage(int waveNumber)
    {
        string scenarioName = "Wave gestartet";

        if (gameManager.enemySpawner != null)
            scenarioName = gameManager.enemySpawner.GetScenarioNameForWave(waveNumber);

        if (IsBossWave(waveNumber))
        {
            temporaryWaveMessage = "!!! BOSS WAVE !!!\nWave " + waveNumber + " gestartet: " + scenarioName;
            ShowBigAlert("!!! BOSS WAVE !!!\nWave " + waveNumber + "\n" + scenarioName, bossWaveColor, specialWaveMessageDuration);
            return;
        }

        if (IsMiniBossWave(waveNumber))
        {
            temporaryWaveMessage = "!!! MINIBOSS WAVE !!!\nWave " + waveNumber + " gestartet: " + scenarioName;
            ShowBigAlert("!!! MINIBOSS WAVE !!!\nWave " + waveNumber + "\n" + scenarioName, miniBossWaveColor, specialWaveMessageDuration);
            return;
        }

        temporaryWaveMessage = "Wave " + waveNumber + " gestartet: " + scenarioName;
        ShowBigAlert(temporaryWaveMessage, normalWaveColor, waveMessageDuration);
    }

    private void ShowWaveFinishedMessage(int finishedWaveNumber)
    {
        if (gameManager.IsPlayerBlocked())
        {
            temporaryWaveMessage = "Wave " + finishedWaveNumber + " geschafft - VERBAUT!";
            ShowBigAlert("VERBAUT!\nWave " + finishedWaveNumber + " geschafft.", blockedWaveColor, specialWaveMessageDuration);
        }
        else if (IsBossWave(finishedWaveNumber))
        {
            ChaosJusticeManager currentChaosJusticeManager = GetChaosJusticeManager();
            bool choiceOpen = currentChaosJusticeManager != null && currentChaosJusticeManager.IsChoiceOpen;
            string bossOutcomeText = GetBossWaveOutcomeText(finishedWaveNumber);

            temporaryWaveMessage = choiceOpen
                ? bossOutcomeText + "! Wähle Chaos oder Gerechtigkeit."
                : bossOutcomeText + "! Buildphase gestartet.";

            ShowBigAlert(
                choiceOpen
                    ? bossOutcomeText.ToUpperInvariant() + "!\nWähle Chaos oder Gerechtigkeit."
                    : bossOutcomeText.ToUpperInvariant() + "!\nBuildphase gestartet.",
                bossWaveColor,
                specialWaveMessageDuration
            );
        }
        else if (IsMiniBossWave(finishedWaveNumber))
        {
            temporaryWaveMessage = "MiniBoss-Wave geschafft! Buildphase gestartet.";
            ShowBigAlert("MINIBOSS GESCHAFFT!\nBuildphase gestartet.", miniBossWaveColor, specialWaveMessageDuration);
        }
        else
        {
            temporaryWaveMessage = "Wave " + finishedWaveNumber + " geschafft! Buildphase gestartet.";
            ShowBigAlert(temporaryWaveMessage, normalWaveColor, waveMessageDuration);
        }

        temporaryWaveMessageTimer = waveMessageDuration;
    }

    private void ShowBigAlert(string message, Color color, float duration)
    {
        temporaryWaveMessageTimer = duration;
        bigAlertMessage = message;
        currentAlertColor = color;
        bigAlertTimer = duration;
    }

    private void UpdateMessageTimers()
    {
        if (temporaryWaveMessageTimer > 0f)
        {
            temporaryWaveMessageTimer -= Time.deltaTime;

            if (temporaryWaveMessageTimer <= 0f)
            {
                temporaryWaveMessageTimer = 0f;
                temporaryWaveMessage = "";
            }
        }

        if (bigAlertTimer > 0f)
        {
            bigAlertTimer -= Time.deltaTime;

            if (bigAlertTimer <= 0f)
            {
                bigAlertTimer = 0f;
                bigAlertMessage = "";
            }
        }
    }

    private void UpdateStatsText()
    {
        if (statsText == null)
            return;

        ChaosJusticeManager currentChaosJusticeManager = GetChaosJusticeManager();
        bool chaosChoiceOpen = currentChaosJusticeManager != null && currentChaosJusticeManager.IsChoiceOpen;

        string text =
            "<b>Gold:</b> " + gameManager.gold +
            "\n<b>Lives:</b> " + gameManager.lives +
            "\n<b>Wave:</b> " + gameManager.waveNumber;

        if (showPhaseInMainHud)
            text += "\n<b>Phase:</b> " + (chaosChoiceOpen ? "Entscheidung" : gameManager.currentPhase.ToString());

        if (!forceMinimalStatsText && showTemporaryWaveMessageInStats && !string.IsNullOrEmpty(temporaryWaveMessage))
            text += "\n\n" + temporaryWaveMessage;

        bool shouldAppendChaos =
            showCompactChaosJusticeSummary &&
            appendChaosJusticeToStatsWhenNoSeparateText &&
            chaosJusticeHudText == null &&
            currentChaosJusticeManager != null &&
            ShouldShowChaosJusticeHud(currentChaosJusticeManager, chaosChoiceOpen);

        if (shouldAppendChaos)
            text += "\n\n" + BuildCompactChaosJusticeSummary(currentChaosJusticeManager, true);

        if (!forceMinimalStatsText && currentChaosJusticeManager != null && showFullChaosJusticeDebug)
            text += "\n\n<b>Chaos/Gerechtigkeit Debug:</b>\n" + currentChaosJusticeManager.GetBalanceDebugText();

        if (!forceMinimalStatsText && currentChaosJusticeManager != null && chaosChoiceOpen && showChaosChoiceOptionsInStatsText)
            text += "\n\n<b>AUSWAHL OFFEN:</b>\n" + currentChaosJusticeManager.GetCurrentChoiceDebugText();

        if (!forceMinimalStatsText && showNextWavePreviewInStats && ShouldShowNextWavePreview(chaosChoiceOpen))
            text += "\n\n<b>Nächste Wave:</b>\n" + gameManager.GetNextWavePreviewText();

        if (showBlockedInfoInStats && gameManager.isTimedBlockedBuildPhase)
            text += "\n\n<b>VERBAUT:</b> " + Mathf.CeilToInt(gameManager.blockedBuildTimeRemaining) + "s";

        if (showBuildWarningsInStats)
        {
            string warningText = GetBuildWarningText();

            if (!string.IsNullOrEmpty(warningText))
                text += "\n\n<b>WARNUNG:</b>\n" + warningText;
        }

        statsText.text = text;
    }

    private void UpdateChaosJusticeHud()
    {
        ChaosJusticeManager currentChaosJusticeManager = GetChaosJusticeManager();
        bool chaosChoiceOpen = currentChaosJusticeManager != null && currentChaosJusticeManager.IsChoiceOpen;
        bool shouldShow = currentChaosJusticeManager != null && showCompactChaosJusticeSummary && ShouldShowChaosJusticeHud(currentChaosJusticeManager, chaosChoiceOpen);

        if (chaosJusticeHudText != null)
            chaosJusticeHudText.text = shouldShow ? BuildCompactChaosJusticeSummary(currentChaosJusticeManager, true) : "";

        SetOptionalPanelVisible(chaosJusticeHudPanel, chaosJusticeHudText, shouldShow && chaosJusticeHudText != null);
    }

    private void ApplyNextWavePreviewPanelLayout()
    {
        RectTransform rect = nextWavePreviewPanel != null ? nextWavePreviewPanel.GetComponent<RectTransform>() : null;
        if (rect != null)
            rect.sizeDelta = autoCompactNextWavePreviewPanel ? compactNextWavePreviewPanelSize : nextWavePreviewPanelSize;
    }

    private bool ShouldShowChaosJusticeHud(ChaosJusticeManager currentChaosJusticeManager, bool chaosChoiceOpen)
    {
        if (currentChaosJusticeManager == null)
            return false;

        if (showChaosJusticeOnlyDuringBossChoice)
            return chaosChoiceOpen;

        return true;
    }

    private void UpdateNextWavePreviewHud()
    {
        bool chaosChoiceOpen = GetChaosJusticeManager() != null && GetChaosJusticeManager().IsChoiceOpen;
        bool shouldShow = showNextWavePreviewInSeparateText && ShouldShowNextWavePreview(chaosChoiceOpen);

        if (nextWavePreviewText != null)
            nextWavePreviewText.text = shouldShow ? BuildNextWavePreviewHudText() : "";

        ApplyNextWavePreviewDynamicHeight(shouldShow);
        SetOptionalPanelVisible(nextWavePreviewPanel, nextWavePreviewText, shouldShow && nextWavePreviewText != null);
    }

    private void ApplyNextWavePreviewDynamicHeight(bool shouldShow)
    {
        if (!autoResizeNextWavePreviewHeight || !shouldShow || nextWavePreviewPanel == null || nextWavePreviewText == null)
            return;

        RectTransform rect = nextWavePreviewPanel.GetComponent<RectTransform>();
        if (rect == null)
            return;

        Vector2 baseSize = autoCompactNextWavePreviewPanel ? compactNextWavePreviewPanelSize : nextWavePreviewPanelSize;
        Vector2 preferred = nextWavePreviewText.GetPreferredValues(nextWavePreviewText.text, baseSize.x - 20f, 0f);
        float height = Mathf.Clamp(preferred.y + nextWavePreviewHeightPadding, nextWavePreviewMinHeight, nextWavePreviewMaxHeight);
        rect.sizeDelta = new Vector2(baseSize.x, height);
    }

    private bool ShouldShowNextWavePreview(bool chaosChoiceOpen)
    {
        if (gameManager == null || gameManager.currentPhase != GamePhase.Build || gameManager.isGameOver || chaosChoiceOpen)
            return false;

        return !hideNextWavePreviewDuringPathChoice || !gameManager.IsPathBuildChoiceOpen();
    }

    private string BuildNextWavePreviewHudText()
    {
        if (gameManager == null)
            return "Keine Wave Preview verfügbar.";

        string text = "<b>Nächste Wave</b>\n" + gameManager.GetNextWavePreviewText();

        if (showBlockedInfoInPreview && gameManager.isTimedBlockedBuildPhase)
            text += "\n\n<b>VERBAUT:</b> Nächste Wave in " + Mathf.CeilToInt(gameManager.blockedBuildTimeRemaining) + "s";

        if (showBuildWarningsInPreview)
        {
            string warningText = GetBuildWarningText();

            if (!string.IsNullOrEmpty(warningText))
                text += "\n\n<b>WARNUNG:</b>\n" + warningText;
        }

        return text;
    }

    private string BuildCompactChaosJusticeSummary(ChaosJusticeManager currentChaosJusticeManager, bool includeHeader)
    {
        if (currentChaosJusticeManager == null || currentChaosJusticeManager.runData == null)
            return "";

        ChaosJusticeRunData data = currentChaosJusticeManager.runData;
        string text = includeHeader ? "<b>Chaos / Gerechtigkeit</b>" : "";

        if (showChaosBalanceBarInStats)
        {
            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += currentChaosJusticeManager.GetBalanceStatusLine();
        }

        if (!string.IsNullOrEmpty(text))
            text += "\n";

        int generalJustice = Mathf.Max(0, data.goldJusticeLevel + data.xpJusticeLevel);

        text +=
            "Chaos " + data.chaosLevel + "/" + data.maxChaosLevel +
            " | Gerechtigkeit " + generalJustice +
            "\nGold x" + currentChaosJusticeManager.GetGoldRewardMultiplier().ToString("0.00") +
            " | XP x" + currentChaosJusticeManager.GetXPRewardMultiplier().ToString("0.00");

        if (showRiskGroupsInCompactSummary)
            text += "\nRisiko-Gruppen:\n" + currentChaosJusticeManager.GetGroupedRiskModifierSummary();
        else
            text += "\nRisiken: " + currentChaosJusticeManager.GetActiveRiskModifierCount() + " aktiv";

        if (showDetailedRiskListInStats)
            text += "\nAktive Risiken:\n" + currentChaosJusticeManager.GetDetailedRiskModifierText(maxHudRiskDetails);

        return text;
    }

    private string GetBuildWarningText()
    {
        TileManager warningSource = tileManager;

        if (warningSource == null && gameManager != null)
            warningSource = gameManager.tileManager;

        if (warningSource == null)
            return "";

        return warningSource.GetBuildRestrictionWarningText();
    }

    private void UpdateBigAlertUI()
    {
        if (waveAlertText == null)
        {
            if (waveAlertPanel != null)
                waveAlertPanel.SetActive(false);

            return;
        }

        GameObject targetObject = waveAlertPanel != null
            ? waveAlertPanel
            : waveAlertText.gameObject;

        bool shouldShow = bigAlertTimer > 0f && !string.IsNullOrEmpty(bigAlertMessage);

        targetObject.SetActive(shouldShow);

        if (!shouldShow)
            return;

        waveAlertText.text = bigAlertMessage;
        waveAlertText.color = currentAlertColor;
    }

    private void UpdateGameOverText()
    {
        if (gameOverText == null)
            return;

        bool shouldShow =
            showLegacyGameOverText &&
            !legacyGameOverTextSuppressed &&
            gameManager != null &&
            gameManager.isGameOver;

        gameOverText.SetActive(shouldShow);
    }

    public void SuppressLegacyGameOverText()
    {
        if (!suppressLegacyGameOverTextAfterResultClose)
            return;

        legacyGameOverTextSuppressed = true;

        if (gameOverText != null)
            gameOverText.SetActive(false);
    }

    public void ResetLegacyGameOverTextSuppression()
    {
        legacyGameOverTextSuppressed = false;
    }

    private string GetBossWaveOutcomeText(int finishedWaveNumber)
    {
        if (gameManager == null)
            return "Boss-Wave abgeschlossen";

        WaveCompletionResult lastResult = gameManager.GetLastCompletedWaveResult();

        if (lastResult != null && lastResult.waveNumber == finishedWaveNumber)
        {
            if (lastResult.bossDefeated)
                return "Boss besiegt";

            return "Boss-Wave überstanden";
        }

        return "Boss-Wave abgeschlossen";
    }

    private ChaosJusticeManager GetChaosJusticeManager()
    {
        if (chaosJusticeManager != null)
            return chaosJusticeManager;

        if (gameManager != null)
        {
            chaosJusticeManager = gameManager.GetChaosJusticeManager();

            if (chaosJusticeManager != null)
                return chaosJusticeManager;
        }

        chaosJusticeManager = FindObjectOfType<ChaosJusticeManager>();
        return chaosJusticeManager;
    }

    private bool IsMiniBossWave(int waveNumber)
    {
        return waveNumber > 0 && waveNumber % 5 == 0 && waveNumber % 10 != 0;
    }

    private bool IsBossWave(int waveNumber)
    {
        return waveNumber > 0 && waveNumber % 10 == 0;
    }
}
