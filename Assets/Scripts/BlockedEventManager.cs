using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BlockedEventType
{
    Continue,
    GoldBonus,
    LifeBonus,
    BuildTimeBonus
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

    private void Start()
    {
        CloseSelection();
        SetupButtons();
        CreateDefaultEventsIfEmpty();
    }

    private void Update()
    {
        if (!selectionOpen)
            return;

        if (gameManager != null && gameManager.IsChaosJusticeChoiceOpen())
            return;

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
            optionButton1.onClick.AddListener(() => ChooseOption(0));
        }

        if (optionButton2 != null)
        {
            optionButton2.onClick.RemoveAllListeners();
            optionButton2.onClick.AddListener(() => ChooseOption(1));
        }

        if (optionButton3 != null)
        {
            optionButton3.onClick.RemoveAllListeners();
            optionButton3.onClick.AddListener(() => ChooseOption(2));
        }
    }

    private void CreateDefaultEventsIfEmpty()
    {
        if (possibleEvents != null && possibleEvents.Count > 0)
            return;

        possibleEvents = new List<BlockedEventOption>();

        possibleEvents.Add(new BlockedEventOption
        {
            displayName = "Weiter",
            description = "Der Run läuft weiter. " + GetTimedBuildPhaseText() + ", danach startet automatisch die nächste Wave.",
            eventType = BlockedEventType.Continue
        });

        possibleEvents.Add(new BlockedEventOption
        {
            displayName = "Goldreserve",
            description = "Du erhältst 100 Gold. Danach läuft der Run mit " + FormatBuildPhaseDuration() + " Buildphase weiter.",
            eventType = BlockedEventType.GoldBonus,
            goldAmount = 100
        });

        possibleEvents.Add(new BlockedEventOption
        {
            displayName = "Notfall-Reparatur",
            description = "Du erhältst 3 Leben. Danach läuft der Run mit " + FormatBuildPhaseDuration() + " Buildphase weiter.",
            eventType = BlockedEventType.LifeBonus,
            lifeAmount = 3
        });
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

        if (eventTopBar != null)
            eventTopBar.SetActive(false);
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

        AddFallbackOptionsUntilReady(eventPool);
        return eventPool;
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
            CreateGoldReserveOption(),
            CreateLifeRepairOption(),
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

    private BlockedEventOption CreateGoldReserveOption()
    {
        return new BlockedEventOption
        {
            displayName = "Weiter",
            description = "Der Run läuft weiter. " + GetTimedBuildPhaseText() + ", danach startet automatisch die nächste Wave.",
            eventType = BlockedEventType.Continue
        };
    }

    private string GetTimedBuildPhaseText()
    {
        return "Du hast " + FormatBuildPhaseDuration() + " Buildphase";
    }

    private string FormatBuildPhaseDuration()
    {
        float safeDuration = Mathf.Max(0f, timedBuildPhaseDuration);

        if (Mathf.Approximately(safeDuration, Mathf.Round(safeDuration)))
            return Mathf.RoundToInt(safeDuration) + " Sekunden";

        return safeDuration.ToString("0.0") + " Sekunden";
    }

    private void UpdateUI()
    {
        if (titleText != null)
            titleText.text = "VERBAUT!";

        if (descriptionText != null)
            descriptionText.text = "Du bist verbaut. Wähle eine von drei Hilfen.";

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
        return selectionOpen;
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
                Debug.Log("Blocked Event gewählt: Bauzeit " + FormatBuildPhaseDuration(buildPhaseDuration));
                break;

            case BlockedEventType.Continue:
                Debug.LogWarning("BlockedEventManager: Continue ist keine auswählbare Verbau-Option mehr und wurde nur als Fallback verarbeitet.");
                break;
        }

        gameManager.RegisterBlockedEventChoice(option.displayName, option.eventType.ToString(), appliedGold, appliedLives, buildPhaseDuration);
        gameManager.MarkBlockedEventChosenForCurrentPosition();
        gameManager.StartTimedBuildPhaseAfterBlockedEvent(buildPhaseDuration);
    }


    private float GetBuildPhaseDurationForOption(BlockedEventOption option)
    {
        if (option != null && option.buildPhaseDurationOverride > 0f)
            return option.buildPhaseDurationOverride;

        return timedBuildPhaseDuration;
    }

}