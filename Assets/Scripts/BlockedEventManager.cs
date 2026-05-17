using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BlockedEventType
{
    Continue,
    GoldBonus,
    LifeBonus,
    BuildTimeBonus,
    PersistentRewardBonus,
    EvolutionPoint,
    LargeLifeBoost,
    RaiseLowTowersToLevelFive,
    ChaosResetKeepOneRisk,
    RelocateBaseTile,
    TeleporterBase
}

[System.Serializable]
public class BlockedEventOption
{
    public string displayName;
    public string description;
    public BlockedEventType eventType;

    [Header("Event Values")]
    public int goldAmount = 0;
    public int lifeAmount = 0;

    [Tooltip("Optional. Wenn > 0, nutzt diese Option eine eigene Verbau-Buildphase-Dauer.")]
    public float buildPhaseDurationOverride = 0f;
}

public class BlockedEventManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;

    [Header("UI - Same Style As PathBuildManager")]
    public GameObject eventTopBar;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    public Button optionButton1;
    public Button optionButton2;
    public Button optionButton3;

    public TextMeshProUGUI optionText1;
    public TextMeshProUGUI optionText2;
    public TextMeshProUGUI optionText3;

    [Header("Settings")]
    public float timedBuildPhaseDuration = 60f;

    [Header("Possible Events")]
    public List<BlockedEventOption> possibleEvents = new List<BlockedEventOption>();

    private BlockedEventOption[] currentOptions = new BlockedEventOption[3];
    private bool selectionOpen = false;
    private bool riskKeepSelectionOpen = false;
    private float pendingRiskKeepBuildPhaseDuration = 0f;
    private List<string> currentRiskKeepOptions = new List<string>();

    private void Start()
    {
        CloseSelection();
        SetupButtons();
        CreateDefaultEventsIfEmpty();
    }

    private void Update()
    {
        if (!selectionOpen && !riskKeepSelectionOpen)
            return;

        if (gameManager != null && gameManager.IsChaosJusticeChoiceOpen())
            return;

        if (riskKeepSelectionOpen)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                ChooseRiskKeepOption(0);

            if (Input.GetKeyDown(KeyCode.Alpha2))
                ChooseRiskKeepOption(1);

            if (Input.GetKeyDown(KeyCode.Alpha3))
                ChooseRiskKeepOption(2);

            if (Input.GetKeyDown(KeyCode.Alpha4))
                ChooseRiskKeepOption(3);

            if (Input.GetKeyDown(KeyCode.Alpha5))
                ChooseRiskKeepOption(4);

            if (Input.GetKeyDown(KeyCode.Alpha6))
                ChooseRiskKeepOption(5);

            if (Input.GetKeyDown(KeyCode.Alpha7))
                ChooseRiskKeepOption(6);

            if (Input.GetKeyDown(KeyCode.Alpha8))
                ChooseRiskKeepOption(7);

            if (Input.GetKeyDown(KeyCode.Alpha9))
                ChooseRiskKeepOption(8);

            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ChooseOption(0);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            ChooseOption(1);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            ChooseOption(2);
    }

    private void SetupButtons()
    {
        if (optionButton1 != null)
        {
            optionButton1.onClick.RemoveAllListeners();
            optionButton1.onClick.AddListener(() => ChooseCurrentButton(0));
        }

        if (optionButton2 != null)
        {
            optionButton2.onClick.RemoveAllListeners();
            optionButton2.onClick.AddListener(() => ChooseCurrentButton(1));
        }

        if (optionButton3 != null)
        {
            optionButton3.onClick.RemoveAllListeners();
            optionButton3.onClick.AddListener(() => ChooseCurrentButton(2));
        }
    }

    private void CreateDefaultEventsIfEmpty()
    {
        if (possibleEvents != null && possibleEvents.Count > 0)
            return;

        possibleEvents = new List<BlockedEventOption>();

        possibleEvents.Add(CreatePersistentRewardBonusOption());
        possibleEvents.Add(CreateEvolutionPointOption());
        possibleEvents.Add(CreateLargeLifeBoostOption());
        possibleEvents.Add(CreateRaiseLowTowersOption());
        possibleEvents.Add(CreateLongBuildPhaseOption());
    }

    public void OpenBlockedEventSelection()
    {
        if (selectionOpen)
            return;

        if (gameManager != null && gameManager.IsChaosJusticeChoiceOpen())
        {
            Debug.LogWarning("BlockedEventManager: Verbau-Auswahl wird nicht geöffnet, solange Chaos/Gerechtigkeit offen ist.");
            return;
        }

        if (gameManager != null)
            gameManager.ClosePathAndBuildSelectionsForModal();

        CreateDefaultEventsIfEmpty();
        GenerateOptions();
        UpdateUI();

        selectionOpen = true;

        if (eventTopBar != null)
            eventTopBar.SetActive(true);
    }

    public void CloseSelection()
    {
        selectionOpen = false;
        riskKeepSelectionOpen = false;

        if (eventTopBar != null)
            eventTopBar.SetActive(false);
    }

    private void ChooseCurrentButton(int index)
    {
        if (riskKeepSelectionOpen)
            ChooseRiskKeepOption(index);
        else
            ChooseOption(index);
    }

    private void GenerateOptions()
    {
        List<BlockedEventOption> eventPool = CreateSelectableEventPool();

        for (int i = 0; i < currentOptions.Length; i++)
            currentOptions[i] = GetRandomOptionFromPool(eventPool);
    }

    private List<BlockedEventOption> CreateSelectableEventPool()
    {
        List<BlockedEventOption> eventPool = new List<BlockedEventOption>();

        if (possibleEvents != null)
        {
            foreach (BlockedEventOption option in possibleEvents)
            {
                if (!IsSelectableEventOption(option))
                    continue;

                AddOptionIfUnique(eventPool, option);
            }
        }

        AddBuiltInBlockedRewardOptions(eventPool);
        AddFallbackOptionsUntilReady(eventPool);
        return eventPool;
    }

    private void AddBuiltInBlockedRewardOptions(List<BlockedEventOption> eventPool)
    {
        AddOptionIfUnique(eventPool, CreatePersistentRewardBonusOption());
        AddOptionIfUnique(eventPool, CreateEvolutionPointOption());
        AddOptionIfUnique(eventPool, CreateLargeLifeBoostOption());
        AddOptionIfUnique(eventPool, CreateRaiseLowTowersOption());
        AddOptionIfUnique(eventPool, CreateChaosResetKeepOneRiskOption());
        AddOptionIfUnique(eventPool, CreateRelocateBaseTileOption());
        AddOptionIfUnique(eventPool, CreateTeleporterBaseOption());
    }

    private bool IsSelectableEventOption(BlockedEventOption option)
    {
        if (option == null)
            return false;

        // "Weiter" ist in Phase 7 keine auswählbare Verbau-Option mehr.
        return option.eventType != BlockedEventType.Continue;
    }

    private void AddFallbackOptionsUntilReady(List<BlockedEventOption> eventPool)
    {
        List<BlockedEventOption> fallbackOptions = CreateFallbackOptions();

        foreach (BlockedEventOption fallbackOption in fallbackOptions)
        {
            if (eventPool.Count >= currentOptions.Length)
                return;

            AddOptionIfUnique(eventPool, fallbackOption);
        }
    }

    private List<BlockedEventOption> CreateFallbackOptions()
    {
        return new List<BlockedEventOption>
        {
            CreatePersistentRewardBonusOption(),
            CreateRelocateBaseTileOption(),
            CreateLongBuildPhaseOption()
        };
    }

    private void AddOptionIfUnique(List<BlockedEventOption> options, BlockedEventOption option)
    {
        if (options == null || option == null)
            return;

        foreach (BlockedEventOption existingOption in options)
        {
            if (IsSameOption(existingOption, option))
                return;
        }

        options.Add(option);
    }

    private bool IsSameOption(BlockedEventOption first, BlockedEventOption second)
    {
        if (first == null || second == null)
            return false;

        return first.eventType == second.eventType &&
               first.displayName == second.displayName &&
               first.goldAmount == second.goldAmount &&
               first.lifeAmount == second.lifeAmount &&
               Mathf.Approximately(first.buildPhaseDurationOverride, second.buildPhaseDurationOverride);
    }

    private BlockedEventOption GetRandomOptionFromPool(List<BlockedEventOption> eventPool)
    {
        if (eventPool == null || eventPool.Count == 0)
            return CreateLongBuildPhaseOption();

        int randomIndex = Random.Range(0, eventPool.Count);
        BlockedEventOption option = eventPool[randomIndex];
        eventPool.RemoveAt(randomIndex);

        return option;
    }

    private BlockedEventOption CreatePersistentRewardBonusOption()
    {
        return new BlockedEventOption
        {
            displayName = "Weg-Kompensation",
            description = "Ab jetzt geben Gold und XP dauerhaft +1%. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.PersistentRewardBonus
        };
    }

    private BlockedEventOption CreateEvolutionPointOption()
    {
        return new BlockedEventOption
        {
            displayName = "Evolutionspunkt",
            description = "Der nächste angeklickte Tower erhält +50% aktuelle Werte. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.EvolutionPoint
        };
    }

    private BlockedEventOption CreateLargeLifeBoostOption()
    {
        return new BlockedEventOption
        {
            displayName = "Lebensreserve",
            description = "Du erhältst +50 Leben. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.LargeLifeBoost,
            lifeAmount = 50
        };
    }

    private BlockedEventOption CreateRaiseLowTowersOption()
    {
        return new BlockedEventOption
        {
            displayName = "Nachschulung",
            description = "Alle Tower unter Level 5 werden sofort Level 5. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.RaiseLowTowersToLevelFive
        };
    }

    private BlockedEventOption CreateChaosResetKeepOneRiskOption()
    {
        return new BlockedEventOption
        {
            displayName = "Chaos ordnen",
            description = "Chaos wird auf 1 gesetzt. Du behältst genau einen aktiven Risiko-Modifikator.",
            eventType = BlockedEventType.ChaosResetKeepOneRisk
        };
    }

    private BlockedEventOption CreateRelocateBaseTileOption()
    {
        return new BlockedEventOption
        {
            displayName = "Neue Basis",
            description = "Setze die Basis auf ein gültiges Build-Feld am Weg. Der alte Wegschwanz samt nahen Towern wird entfernt.",
            eventType = BlockedEventType.RelocateBaseTile
        };
    }

    private BlockedEventOption CreateTeleporterBaseOption()
    {
        return new BlockedEventOption
        {
            displayName = "Teleporter",
            description = "Eine neue Basis erscheint zufällig nahe am Weg. Ein Teleporter verbindet alte und neue Basis dauerhaft.",
            eventType = BlockedEventType.TeleporterBase
        };
    }

    private BlockedEventOption CreateGoldReserveOption()
    {
        return new BlockedEventOption
        {
            displayName = "Goldreserve",
            description = "Du erhältst 100 Gold. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.GoldBonus,
            goldAmount = 100
        };
    }

    private BlockedEventOption CreateLifeRepairOption()
    {
        return new BlockedEventOption
        {
            displayName = "Notfall-Reparatur",
            description = "Du erhältst 3 Leben. Danach " + GetTimedBuildPhaseText() + ".",
            eventType = BlockedEventType.LifeBonus,
            lifeAmount = 3
        };
    }

    private BlockedEventOption CreateLongBuildPhaseOption()
    {
        float safeDuration = Mathf.Max(timedBuildPhaseDuration, 90f);

        return new BlockedEventOption
        {
            displayName = "Baupause",
            description = "Keine Sofortbelohnung. Du erhältst " + FormatBuildPhaseDurationFor(safeDuration) + " Buildphase.",
            eventType = BlockedEventType.BuildTimeBonus,
            buildPhaseDurationOverride = safeDuration
        };
    }

    private string GetTimedBuildPhaseText()
    {
        return "hast du " + FormatBuildPhaseDuration() + " Buildphase";
    }

    private string FormatBuildPhaseDuration()
    {
        return FormatBuildPhaseDurationFor(timedBuildPhaseDuration);
    }

    private string FormatBuildPhaseDurationFor(float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);

        if (Mathf.Approximately(safeDuration, Mathf.Round(safeDuration)))
            return Mathf.RoundToInt(safeDuration) + " Sekunden";

        return safeDuration.ToString("0.0") + " Sekunden";
    }

    private void UpdateUI()
    {
        if (titleText != null)
            titleText.text = "VERBAUT!";

        if (descriptionText != null)
            descriptionText.text = "Du bist verbaut. Wähle eine von drei zufälligen Optionen.";

        SetOptionText(optionText1, 0);
        SetOptionText(optionText2, 1);
        SetOptionText(optionText3, 2);
    }

    private void SetOptionText(TextMeshProUGUI textField, int index)
    {
        if (textField == null)
            return;

        if (currentOptions[index] == null)
        {
            textField.text = "[" + (index + 1) + "] Fehlerhafte Option";
            return;
        }

        textField.text =
            "[" + (index + 1) + "] " + currentOptions[index].displayName +
            "\n" + currentOptions[index].description;
    }

    private void ChooseOption(int index)
    {
        if (!selectionOpen)
            return;

        if (gameManager != null && (gameManager.isGameOver || gameManager.IsChaosJusticeChoiceOpen()))
            return;

        if (index < 0 || index >= currentOptions.Length)
            return;

        BlockedEventOption option = currentOptions[index];

        if (option == null)
            return;

        CloseSelection();
        ApplyEvent(option);
    }


    public bool IsSelectionOpen()
    {
        return selectionOpen || riskKeepSelectionOpen;
    }

    private void ApplyEvent(BlockedEventOption option)
    {
        if (gameManager == null)
        {
            Debug.LogError("GameManager fehlt im BlockedEventManager!");
            return;
        }

        int appliedGold = 0;
        int appliedLives = 0;
        float buildPhaseDuration = GetBuildPhaseDurationForOption(option);

        switch (option.eventType)
        {
            case BlockedEventType.GoldBonus:
                appliedGold = Mathf.Max(0, option.goldAmount);
                gameManager.AddGold(appliedGold, true, RunGoldSource.BlockedEvent);
                Debug.Log("Blocked Event gewählt: Gold +" + appliedGold);
                break;

            case BlockedEventType.LifeBonus:
                appliedLives = Mathf.Max(0, option.lifeAmount);
                gameManager.AddLives(appliedLives);
                Debug.Log("Blocked Event gewählt: Leben +" + appliedLives);
                break;

            case BlockedEventType.BuildTimeBonus:
                Debug.Log("Blocked Event gewählt: Bauzeit " + FormatBuildPhaseDurationFor(buildPhaseDuration));
                break;

            case BlockedEventType.PersistentRewardBonus:
                gameManager.AddBlockedEventRewardBonusStack();
                Debug.Log("Blocked Event gewählt: dauerhafter Gold/XP-Bonus +1%.");
                break;

            case BlockedEventType.EvolutionPoint:
                gameManager.AddEvolutionTowerBoost();
                Debug.Log("Blocked Event gewählt: Evolutionspunkt.");
                break;

            case BlockedEventType.LargeLifeBoost:
                appliedLives = Mathf.Max(0, option.lifeAmount);
                gameManager.AddLives(appliedLives);
                Debug.Log("Blocked Event gewählt: Leben +" + appliedLives);
                break;

            case BlockedEventType.RaiseLowTowersToLevelFive:
                gameManager.RaiseLowTowersToLevelFive();
                Debug.Log("Blocked Event gewählt: Tower unter Level 5 angehoben.");
                break;

            case BlockedEventType.ChaosResetKeepOneRisk:
                OpenRiskKeepSelection(option, buildPhaseDuration);
                return;

            case BlockedEventType.RelocateBaseTile:
                gameManager.RegisterBlockedEventChoice(option.displayName, option.eventType.ToString(), appliedGold, appliedLives, buildPhaseDuration);
                gameManager.MarkBlockedEventChosenForCurrentPosition();
                gameManager.BeginBlockedBaseRelocation(buildPhaseDuration);
                Debug.Log("Blocked Event gewählt: Neue Basis platzieren.");
                return;

            case BlockedEventType.TeleporterBase:
                if (!gameManager.TryCreateBlockedTeleporterBase())
                {
                    gameManager.AddLives(10);
                    appliedLives = 10;
                    Debug.LogWarning("Teleporter fehlgeschlagen; sichere Ersatzbelohnung +10 Leben.");
                }
                else
                {
                    Debug.Log("Blocked Event gewählt: Teleporter-Basis erstellt.");
                }
                break;

            case BlockedEventType.Continue:
                Debug.LogWarning("BlockedEventManager: Continue ist keine auswählbare Verbau-Option mehr und wurde nur als Fallback verarbeitet.");
                break;
        }

        gameManager.RegisterBlockedEventChoice(option.displayName, option.eventType.ToString(), appliedGold, appliedLives, buildPhaseDuration);
        gameManager.MarkBlockedEventChosenForCurrentPosition();
        gameManager.StartTimedBuildPhaseAfterBlockedEvent(buildPhaseDuration);
    }


    private void OpenRiskKeepSelection(BlockedEventOption sourceOption, float buildPhaseDuration)
    {
        if (gameManager == null)
            return;

        currentRiskKeepOptions = gameManager.GetActiveRiskModifierDisplayNames();

        if (currentRiskKeepOptions == null || currentRiskKeepOptions.Count == 0)
        {
            gameManager.ResetChaosToOneKeepingRiskModifier(-1);
            gameManager.RegisterBlockedEventChoice(sourceOption.displayName, sourceOption.eventType.ToString(), 0, 0, buildPhaseDuration);
            gameManager.MarkBlockedEventChosenForCurrentPosition();
            gameManager.StartTimedBuildPhaseAfterBlockedEvent(buildPhaseDuration);
            return;
        }

        pendingRiskKeepBuildPhaseDuration = buildPhaseDuration;
        riskKeepSelectionOpen = true;
        selectionOpen = false;
        UpdateRiskKeepUI();

        if (eventTopBar != null)
            eventTopBar.SetActive(true);
    }

    private void UpdateRiskKeepUI()
    {
        if (titleText != null)
            titleText.text = "RISIKO BEHALTEN";

        if (descriptionText != null)
            descriptionText.text = BuildRiskKeepDescriptionText();

        SetRiskKeepText(optionText1, 0);
        SetRiskKeepText(optionText2, 1);
        SetRiskKeepText(optionText3, 2);
    }

    private string BuildRiskKeepDescriptionText()
    {
        string text = "Wähle, welcher Risiko-Modifikator aktiv bleibt. Chaos wird auf 1 gesetzt.";

        if (currentRiskKeepOptions == null || currentRiskKeepOptions.Count == 0)
            return text;

        text += "\n";

        for (int i = 0; i < currentRiskKeepOptions.Count; i++)
        {
            if (i >= 9)
            {
                text += "\nWeitere Risiken werden entfernt.";
                break;
            }

            text += "\n[" + (i + 1) + "] " + currentRiskKeepOptions[i];
        }

        return text;
    }

    private void SetRiskKeepText(TextMeshProUGUI textField, int index)
    {
        if (textField == null)
            return;

        if (currentRiskKeepOptions == null || index >= currentRiskKeepOptions.Count)
        {
            textField.text = "[" + (index + 1) + "] Keine weitere Auswahl";
            return;
        }

        textField.text = "[" + (index + 1) + "] " + currentRiskKeepOptions[index];
    }

    private void ChooseRiskKeepOption(int index)
    {
        if (!riskKeepSelectionOpen || gameManager == null)
            return;

        if (currentRiskKeepOptions == null || index < 0 || index >= currentRiskKeepOptions.Count)
            return;

        riskKeepSelectionOpen = false;
        CloseSelection();
        gameManager.ResetChaosToOneKeepingRiskModifier(index);
        gameManager.RegisterBlockedEventChoice("Chaos ordnen", BlockedEventType.ChaosResetKeepOneRisk.ToString(), 0, 0, pendingRiskKeepBuildPhaseDuration);
        gameManager.MarkBlockedEventChosenForCurrentPosition();
        gameManager.StartTimedBuildPhaseAfterBlockedEvent(pendingRiskKeepBuildPhaseDuration);
    }

    private float GetBuildPhaseDurationForOption(BlockedEventOption option)
    {
        if (option != null && option.buildPhaseDurationOverride > 0f)
            return option.buildPhaseDurationOverride;

        return timedBuildPhaseDuration;
    }

}