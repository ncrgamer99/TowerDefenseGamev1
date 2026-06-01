using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AutopathManager : MonoBehaviour
{
    private const int ForwardIndex = 0;
    private const int LeftIndex = 1;
    private const int RightIndex = 2;

    [Header("References")]
    public PathBuildManager pathBuildManager;
    public TileManager tileManager;
    public BuildManager buildManager;
    public GameManager gameManager;

    [Header("Autopath V1")]
    public KeyCode panelToggleKey = KeyCode.F2;
    public float buildDelay = 2f;
    public int lookaheadDepth = 6;
    public int minimumSafeLookaheadTiles = 2;
    public bool autoChooseSafeChaosAfterBoss = true;
    public bool autoCreateUI = true;
    public Vector2 panelSize = new Vector2(420f, 274f);

    [Header("Direction Randomness")]
    public bool randomizeSafeDirections = true;
    public float forwardDirectionRandomWeight = 0.25f;
    public float sideDirectionRandomWeight = 1.75f;

    private bool autopathEnabled = false;
    private bool panelOpen = false;
    private bool buildPending = false;
    private float buildReadyTime = 0f;
    private Vector2Int pendingTarget;
    private string pendingDirectionLabel = "";
    private int pendingScore = 0;
    private string statusText = "Bereit";
    private string noTargetReason = "Keine Richtung";

    private Canvas autopathCanvas;
    private GameObject panelRoot;
    private TextMeshProUGUI stateText;
    private TextMeshProUGUI statusValueText;
    private TextMeshProUGUI targetValueText;
    private TextMeshProUGUI delayValueText;
    private TextMeshProUGUI ruleValueText;
    private TextMeshProUGUI toggleButtonText;
    private Button toggleButtonControl;
    private Image stateBadgeImage;
    private readonly Dictionary<string, Sprite> panelSpriteCache = new Dictionary<string, Sprite>();

    private struct AutopathCandidate
    {
        public Vector2Int target;
        public string directionLabel;
        public int score;
        public int directionIndex;

        public AutopathCandidate(Vector2Int target, string directionLabel, int score, int directionIndex)
        {
            this.target = target;
            this.directionLabel = directionLabel;
            this.score = score;
            this.directionIndex = directionIndex;
        }
    }

    public static AutopathManager EnsureExists(PathBuildManager manager)
    {
        if (manager == null)
            return null;

        AutopathManager autopath = manager.GetComponent<AutopathManager>();

        if (autopath == null)
            autopath = manager.gameObject.AddComponent<AutopathManager>();

        autopath.Initialize(manager);
        return autopath;
    }

    public void Initialize(PathBuildManager manager)
    {
        pathBuildManager = manager;

        if (manager != null)
        {
            tileManager = manager.tileManager;
            buildManager = manager.buildManager;
            gameManager = manager.gameManager;
        }

        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureAutopathUI();
        RefreshPanel();
    }

    private void Update()
    {
        ResolveReferences();
        HandlePanelToggleInput();
        UpdateAutopath();
        RefreshPanel();
    }

    private void ResolveReferences()
    {
        if (pathBuildManager == null)
            pathBuildManager = GetComponent<PathBuildManager>();

        if (tileManager == null && pathBuildManager != null)
            tileManager = pathBuildManager.tileManager;

        if (buildManager == null && pathBuildManager != null)
            buildManager = pathBuildManager.buildManager;

        if (gameManager == null && pathBuildManager != null)
            gameManager = pathBuildManager.gameManager;

        if (gameManager == null && tileManager != null)
            gameManager = tileManager.gameManager;

        if (gameManager == null && buildManager != null)
            gameManager = buildManager.gameManager;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void HandlePanelToggleInput()
    {
        if (!Input.GetKeyDown(panelToggleKey))
            return;

        TogglePanel();
    }

    public void TogglePanel()
    {
        SetPanelOpen(!panelOpen);
    }

    public void OpenPanel()
    {
        SetPanelOpen(true);
    }

    public void ClosePanel()
    {
        SetPanelOpen(false);
    }

    private void SetPanelOpen(bool open)
    {
        EnsureAutopathUI();
        panelOpen = open;

        if (panelRoot != null)
        {
            panelRoot.SetActive(panelOpen);

            if (panelOpen)
                panelRoot.transform.SetAsLastSibling();
        }
    }

    private void UpdateAutopath()
    {
        if (!autopathEnabled)
        {
            ClearPendingBuild();
            statusText = "Aus";
            return;
        }

        if (TryHandleAutopathBossChoice())
            return;

        if (!CanRunAutopath(out string blockedReason))
        {
            ClearPendingBuild();
            statusText = blockedReason;
            return;
        }

        if (!buildPending)
        {
            if (!TryChooseAutopathTarget(out pendingTarget, out pendingDirectionLabel, out pendingScore))
            {
                statusText = noTargetReason;
                return;
            }

            float safeDelay = Mathf.Max(0.1f, buildDelay);
            buildReadyTime = Time.time + safeDelay;
            buildPending = true;
        }

        if (!tileManager.CanExtendTo(pendingTarget))
        {
            ClearPendingBuild();
            statusText = "Neu pruefen";
            return;
        }

        float remaining = buildReadyTime - Time.time;

        if (remaining > 0f)
        {
            statusText = "Wartet " + remaining.ToString("0.0") + "s";
            return;
        }

        ExecutePendingBuild();
    }

    private bool CanRunAutopath(out string blockedReason)
    {
        blockedReason = "Bereit";

        if (gameManager != null)
        {
            if (!gameManager.gameStarted)
            {
                blockedReason = "Warte auf Run";
                return false;
            }

            if (gameManager.isGameOver)
            {
                blockedReason = "Game Over";
                return false;
            }

            if (gameManager.currentPhase != GamePhase.Build)
            {
                blockedReason = "Wartet auf Wave";
                return false;
            }

            if (gameManager.IsPathInputLockedByModalUI())
            {
                blockedReason = "Wartet auf UI";
                return false;
            }
        }

        if (tileManager == null)
        {
            blockedReason = "TileManager fehlt";
            return false;
        }

        if (!tileManager.IsRunPathInitialized())
        {
            blockedReason = "Warte auf Pfad";
            return false;
        }

        if (tileManager.IsBaseRelocationModeActive())
        {
            blockedReason = "Wartet auf Base";
            return false;
        }

        if (!tileManager.IsBuildAllowed())
        {
            blockedReason = "Bau gesperrt";
            return false;
        }

        if (pathBuildManager != null && pathBuildManager.IsChoiceOpen())
        {
            blockedReason = "Wegwahl offen";
            return false;
        }

        if (IsTowerBuildSelectionActive())
        {
            blockedReason = "Towerbau offen";
            return false;
        }

        return true;
    }

    private bool TryHandleAutopathBossChoice()
    {
        if (!autoChooseSafeChaosAfterBoss)
            return false;

        ChaosJusticeManager chaosJusticeManager = gameManager != null ? gameManager.GetChaosJusticeManager() : FindObjectOfType<ChaosJusticeManager>();

        if (chaosJusticeManager == null || !chaosJusticeManager.IsChoiceOpen)
            return false;

        ClearPendingBuild();

        if (chaosJusticeManager.TryAutoChooseChaosForAutopath(out string actionText))
            statusText = actionText;
        else
            statusText = "Bosswahl offen";

        return true;
    }

    private bool IsTowerBuildSelectionActive()
    {
        return buildManager != null &&
               buildManager.selectedBuildOption != null &&
               buildManager.selectedBuildOption.placementType == PlacementType.BuildTile;
    }

    private bool TryChooseAutopathTarget(out Vector2Int target, out string directionLabel, out int score)
    {
        target = Vector2Int.zero;
        directionLabel = "";
        score = int.MinValue;

        if (tileManager == null)
            return false;

        Vector2Int basePosition = tileManager.GetBasePosition();
        Vector2Int[] candidates = tileManager.GetPossibleExtensionPositions();
        int safeDepth = Mathf.Clamp(lookaheadDepth, 1, 8);
        int requiredSafeLookahead = Mathf.Clamp(minimumSafeLookaheadTiles, 0, 8);
        List<AutopathCandidate> validCandidates = new List<AutopathCandidate>();
        int blockedSelfCandidates = 0;

        noTargetReason = "Keine Richtung";

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2Int candidate = candidates[i];

            if (!tileManager.CanExtendTo(candidate))
                continue;

            HashSet<Vector2Int> simulatedBlocked = new HashSet<Vector2Int>
            {
                basePosition,
                candidate
            };

            Vector2Int direction = candidate - basePosition;
            int candidateScore = EvaluateLookahead(candidate, direction, safeDepth - 1, simulatedBlocked);

            if (!HasSafeContinuation(candidate, direction, requiredSafeLookahead, simulatedBlocked))
            {
                blockedSelfCandidates++;
                continue;
            }

            AutopathCandidate validCandidate = new AutopathCandidate(candidate, GetDirectionLabel(i), candidateScore, i);
            validCandidates.Add(validCandidate);
        }

        if (validCandidates.Count == 0)
        {
            if (blockedSelfCandidates > 0)
                noTargetReason = "Keine sichere Richtung";

            return false;
        }

        AutopathCandidate chosenCandidate = ChooseAutopathCandidate(validCandidates);
        target = chosenCandidate.target;
        directionLabel = chosenCandidate.directionLabel;
        score = chosenCandidate.score;
        return true;
    }

    private bool HasSafeContinuation(Vector2Int simulatedBase, Vector2Int direction, int remainingTiles, HashSet<Vector2Int> simulatedBlocked)
    {
        if (remainingTiles <= 0)
            return true;

        Vector2Int[] nextPositions = GetSimulatedExtensionPositions(simulatedBase, direction);

        for (int i = 0; i < nextPositions.Length; i++)
        {
            Vector2Int nextPosition = nextPositions[i];

            if (!IsSimulatedBasePositionValid(nextPosition, simulatedBlocked))
                continue;

            simulatedBlocked.Add(nextPosition);
            bool hasContinuation = HasSafeContinuation(nextPosition, nextPosition - simulatedBase, remainingTiles - 1, simulatedBlocked);
            simulatedBlocked.Remove(nextPosition);

            if (hasContinuation)
                return true;
        }

        return false;
    }

    private AutopathCandidate ChooseAutopathCandidate(List<AutopathCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return default(AutopathCandidate);

        if (!randomizeSafeDirections || candidates.Count == 1)
            return GetHighestScoringCandidate(candidates);

        List<AutopathCandidate> safePool = BuildNonDeadEndPool(candidates);
        return PickWeightedCandidate(safePool);
    }

    private List<AutopathCandidate> BuildNonDeadEndPool(List<AutopathCandidate> candidates)
    {
        List<AutopathCandidate> safePool = new List<AutopathCandidate>();

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].score > 1)
                safePool.Add(candidates[i]);
        }

        return safePool.Count > 0 ? safePool : candidates;
    }

    private AutopathCandidate GetHighestScoringCandidate(List<AutopathCandidate> candidates)
    {
        AutopathCandidate bestCandidate = candidates[0];

        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].score > bestCandidate.score)
                bestCandidate = candidates[i];
        }

        return bestCandidate;
    }

    private AutopathCandidate PickWeightedCandidate(List<AutopathCandidate> candidates)
    {
        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
            totalWeight += GetCandidateWeight(candidates[i]);

        if (totalWeight <= 0f)
            return candidates[Random.Range(0, candidates.Count)];

        float roll = Random.Range(0f, totalWeight);

        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= GetCandidateWeight(candidates[i]);

            if (roll <= 0f)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }

    private float GetCandidateWeight(AutopathCandidate candidate)
    {
        float directionWeight = candidate.directionIndex == ForwardIndex ? forwardDirectionRandomWeight : sideDirectionRandomWeight;
        return Mathf.Max(0.01f, directionWeight);
    }

    private int EvaluateLookahead(Vector2Int simulatedBase, Vector2Int direction, int depth, HashSet<Vector2Int> simulatedBlocked)
    {
        int score = 1;

        if (depth <= 0)
            return score;

        Vector2Int[] nextPositions = GetSimulatedExtensionPositions(simulatedBase, direction);

        for (int i = 0; i < nextPositions.Length; i++)
        {
            Vector2Int nextPosition = nextPositions[i];

            if (!IsSimulatedBasePositionValid(nextPosition, simulatedBlocked))
                continue;

            simulatedBlocked.Add(nextPosition);
            score += EvaluateLookahead(nextPosition, nextPosition - simulatedBase, depth - 1, simulatedBlocked);
            simulatedBlocked.Remove(nextPosition);
        }

        return score;
    }

    private Vector2Int[] GetSimulatedExtensionPositions(Vector2Int simulatedBase, Vector2Int direction)
    {
        Vector2Int forward = simulatedBase + direction;
        Vector2Int left = simulatedBase + new Vector2Int(-direction.y, direction.x);
        Vector2Int right = simulatedBase + new Vector2Int(direction.y, -direction.x);

        return new Vector2Int[]
        {
            forward,
            left,
            right
        };
    }

    private bool IsSimulatedBasePositionValid(Vector2Int position, HashSet<Vector2Int> simulatedBlocked)
    {
        if (simulatedBlocked != null && simulatedBlocked.Contains(position))
            return false;

        return tileManager == null || !tileManager.IsPositionBlocked(position);
    }

    private void ExecutePendingBuild()
    {
        if (tileManager == null)
        {
            ClearPendingBuild();
            statusText = "TileManager fehlt";
            return;
        }

        if (pathBuildManager != null)
            pathBuildManager.CancelChoice();

        bool success = tileManager.TryExtendPathTo(pendingTarget);
        string builtDirection = pendingDirectionLabel;

        ClearPendingBuild();
        statusText = success ? "Gebaut: " + builtDirection : "Bau fehlgeschlagen";
    }

    private void ClearPendingBuild()
    {
        buildPending = false;
        buildReadyTime = 0f;
        pendingTarget = Vector2Int.zero;
        pendingDirectionLabel = "";
        pendingScore = 0;
    }

    private string GetDirectionLabel(int candidateIndex)
    {
        if (candidateIndex == ForwardIndex)
            return "Vorwaerts";

        if (candidateIndex == LeftIndex)
            return "Links";

        if (candidateIndex == RightIndex)
            return "Rechts";

        return "Unbekannt";
    }

    private void ToggleAutopathEnabled()
    {
        autopathEnabled = !autopathEnabled;
        ClearPendingBuild();
        statusText = autopathEnabled ? "Bereit" : "Aus";
        RefreshPanel();
    }

    private void EnsureAutopathUI()
    {
        if (!autoCreateUI || panelRoot != null)
            return;

        EnsureEventSystem();

        GameObject canvasObject = new GameObject("AutopathCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        autopathCanvas = canvasObject.GetComponent<Canvas>();
        autopathCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        autopathCanvas.sortingOrder = 930;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRoot = new GameObject("AutopathPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(autopathCanvas.transform, false);

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-34f, -130f);
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelRoot.GetComponent<Image>();
        ApplyPanelSprite(panelImage, new Color32(4, 14, 16, 246), new Color32(170, 113, 35, 255));

        CreatePanelBox(panelRoot.transform, "AutopathHeaderLine", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(-40f, 2f), new Color32(170, 113, 35, 180), new Color32(170, 113, 35, 0));

        CreatePanelText(panelRoot.transform, "AutopathTitle", "AUTOPATH", 24f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -17f), new Vector2(210f, 34f), new Color32(244, 198, 98, 255));
        CreatePanelText(panelRoot.transform, "AutopathHint", "F2  |  RUN-AUTOPATH", 11f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(21f, -48f), new Vector2(210f, 18f), new Color32(225, 164, 54, 255));

        Transform stateBadge = CreatePanelBox(panelRoot.transform, "AutopathStateBadge", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(90f, 31f), new Color32(54, 39, 13, 248), new Color32(170, 113, 35, 255));
        stateBadgeImage = stateBadge.GetComponent<Image>();
        stateText = CreatePanelText(stateBadge, "AutopathState", "AUS", 18f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color32(255, 190, 75, 255));

        statusValueText = CreateInfoRow(panelRoot.transform, "AutopathStatusRow", "STATUS", new Vector2(20f, -80f), new Vector2(380f, 36f), new Color32(170, 113, 35, 255));
        targetValueText = CreateInfoRow(panelRoot.transform, "AutopathTargetRow", "ZIEL", new Vector2(20f, -122f), new Vector2(380f, 36f), new Color32(65, 200, 235, 255));
        delayValueText = CreateInfoRow(panelRoot.transform, "AutopathDelayRow", "DELAY", new Vector2(20f, -164f), new Vector2(182f, 36f), new Color32(230, 170, 55, 255));
        ruleValueText = CreateInfoRow(panelRoot.transform, "AutopathRuleRow", "REGEL", new Vector2(218f, -164f), new Vector2(182f, 36f), new Color32(188, 105, 255, 255));
        ruleValueText.text = "2-Step + Boss";

        CreatePanelBox(panelRoot.transform, "AutopathButtonLine", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(-40f, 2f), new Color32(90, 62, 28, 180), new Color32(90, 62, 28, 0));

        Button toggleButton = CreatePanelButton(panelRoot.transform, "AutopathToggleButton", new Vector2(20f, 18f), new Vector2(252f, 42f), "AUTOPATH AUS");
        toggleButton.onClick.AddListener(ToggleAutopathEnabled);
        toggleButtonControl = toggleButton;
        toggleButtonText = toggleButton.GetComponentInChildren<TextMeshProUGUI>();

        Button closeButton = CreatePanelButton(panelRoot.transform, "AutopathCloseButton", new Vector2(-20f, 18f), new Vector2(116f, 42f), "SCHLIESSEN");
        closeButton.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0f);
        closeButton.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0f);
        closeButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
        closeButton.onClick.AddListener(ClosePanel);

        panelRoot.SetActive(panelOpen);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private TextMeshProUGUI CreatePanelText(Transform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        return CreatePanelText(parent, name, text, fontSize, style, alignment, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, new Color32(238, 222, 188, 255));
    }

    private TextMeshProUGUI CreatePanelText(Transform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color32 color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        return label;
    }

    private TextMeshProUGUI CreateInfoRow(Transform parent, string name, string labelText, Vector2 anchoredPosition, Vector2 size, Color32 border)
    {
        Transform row = CreatePanelBox(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size, new Color32(6, 18, 20, 238), border);
        CreatePanelText(row, name + "Label", labelText, 9f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(13f, -6f), new Vector2(120f, 14f), new Color32(170, 146, 103, 255));
        return CreatePanelText(row, name + "Value", "", 13f, FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 5f), new Vector2(-28f, 20f), new Color32(238, 222, 188, 255));
    }

    private Transform CreatePanelBox(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color32 fill, Color32 border)
    {
        GameObject boxObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(parent, false);

        RectTransform rect = boxObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        ApplyPanelSprite(boxObject.GetComponent<Image>(), fill, border);
        return boxObject.transform;
    }

    private void ApplyPanelSprite(Image image, Color32 fill, Color32 border)
    {
        if (image == null)
            return;

        image.sprite = CreatePanelSprite(fill, border);
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    private Sprite CreatePanelSprite(Color32 fill, Color32 border)
    {
        string key = fill.r + "_" + fill.g + "_" + fill.b + "_" + fill.a + "_" + border.r + "_" + border.g + "_" + border.b + "_" + border.a;

        if (panelSpriteCache.TryGetValue(key, out Sprite cachedSprite))
            return cachedSprite;

        const int textureSize = 32;
        const int borderSize = 2;
        const float cornerRadius = 5f;

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;
        texture.hideFlags = HideFlags.HideAndDontSave;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float cornerX = x < cornerRadius ? cornerRadius : x > textureSize - cornerRadius - 1f ? textureSize - cornerRadius - 1f : x;
                float cornerY = y < cornerRadius ? cornerRadius : y > textureSize - cornerRadius - 1f ? textureSize - cornerRadius - 1f : y;
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cornerX, cornerY));

                if (distance > cornerRadius + 0.5f)
                {
                    texture.SetPixel(x, y, new Color32(0, 0, 0, 0));
                    continue;
                }

                bool edge = x < borderSize || y < borderSize || x >= textureSize - borderSize || y >= textureSize - borderSize || distance > cornerRadius - borderSize;

                if (edge)
                {
                    texture.SetPixel(x, y, border);
                    continue;
                }

                byte shade = (byte)Mathf.Clamp(8 + y / 5, 0, 18);
                texture.SetPixel(x, y, new Color32((byte)Mathf.Min(255, fill.r + shade), (byte)Mathf.Min(255, fill.g + shade), (byte)Mathf.Min(255, fill.b + shade), fill.a));
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(7f, 7f, 7f, 7f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        panelSpriteCache[key] = sprite;
        return sprite;
    }

    private Button CreatePanelButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string labelText)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Button button = buttonObject.GetComponent<Button>();
        ApplyButtonColors(button, new Color32(6, 11, 15, 255));

        TextMeshProUGUI label = CreatePanelText(buttonObject.transform, name + "Text", labelText, 15f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private void ApplyButtonColors(Button button, Color32 baseColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();

        if (image != null)
            ApplyPanelSprite(image, baseColor, new Color32(170, 113, 35, 255));

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 244, 196, 255);
        colors.pressedColor = new Color32(220, 170, 85, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(110, 110, 110, 180);
        button.colors = colors;
    }

    private void RefreshPanel()
    {
        if (panelRoot == null)
            return;

        if (stateText != null)
        {
            stateText.text = autopathEnabled ? "AN" : "AUS";
            stateText.color = autopathEnabled ? new Color32(80, 220, 140, 255) : new Color32(255, 190, 75, 255);
        }

        if (stateBadgeImage != null)
        {
            Color32 stateFill = autopathEnabled ? new Color32(13, 58, 35, 248) : new Color32(54, 39, 13, 248);
            Color32 stateBorder = autopathEnabled ? new Color32(82, 210, 129, 255) : new Color32(170, 113, 35, 255);
            ApplyPanelSprite(stateBadgeImage, stateFill, stateBorder);
        }

        if (statusValueText != null)
        {
            statusValueText.text = statusText;
            statusValueText.color = autopathEnabled ? new Color32(236, 226, 200, 255) : new Color32(255, 190, 75, 255);
        }

        if (targetValueText != null)
        {
            targetValueText.text = buildPending ? pendingDirectionLabel + " / Spielraum " + pendingScore : "-";
            targetValueText.color = buildPending ? new Color32(65, 214, 244, 255) : new Color32(170, 160, 135, 255);
        }

        if (delayValueText != null)
        {
            float remaining = buildPending ? Mathf.Max(0f, buildReadyTime - Time.time) : Mathf.Max(0.1f, buildDelay);
            delayValueText.text = buildPending ? "Start " + remaining.ToString("0.0") + "s" : remaining.ToString("0.0") + "s";
            delayValueText.color = buildPending ? new Color32(255, 210, 96, 255) : new Color32(238, 222, 188, 255);
        }

        if (ruleValueText != null)
        {
            ruleValueText.text = "2-Step + Boss";
            ruleValueText.color = new Color32(204, 130, 255, 255);
        }

        if (toggleButtonText != null)
            toggleButtonText.text = autopathEnabled ? "AUTOPATH AN" : "AUTOPATH AUS";

        Color32 toggleColor = autopathEnabled ? new Color32(14, 75, 45, 255) : new Color32(6, 11, 15, 255);
        ApplyButtonColors(toggleButtonControl, toggleColor);
    }
}
