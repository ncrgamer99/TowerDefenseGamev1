using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BlockedEventType
{
    Continue,
    GoldBonus,
    LifeBonus
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
            description = "Der Run läuft weiter. Du hast 60 Sekunden Buildphase, danach startet automatisch die nächste Wave.",
            eventType = BlockedEventType.Continue
        });

        possibleEvents.Add(new BlockedEventOption
        {
            displayName = "Goldreserve",
            description = "Du erhältst 100 Gold. Danach läuft der Run mit 60 Sekunden Buildphase weiter.",
            eventType = BlockedEventType.GoldBonus,
            goldAmount = 100
        });

        possibleEvents.Add(new BlockedEventOption
        {
            displayName = "Notfall-Reparatur",
            description = "Du erhältst 3 Leben. Danach läuft der Run mit 60 Sekunden Buildphase weiter.",
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
        // "Weiter" ist immer Option 1.
        currentOptions[0] = CreateContinueOption();

        List<BlockedEventOption> eventPool = new List<BlockedEventOption>();

        foreach (BlockedEventOption option in possibleEvents)
        {
            if (option == null)
                continue;

            if (option.eventType == BlockedEventType.Continue)
                continue;

            eventPool.Add(option);
        }

        currentOptions[1] = GetRandomOptionFromPool(eventPool);
        currentOptions[2] = GetRandomOptionFromPool(eventPool);
    }

    private BlockedEventOption GetRandomOptionFromPool(List<BlockedEventOption> eventPool)
    {
        if (eventPool == null || eventPool.Count == 0)
            return CreateContinueOption();

        int randomIndex = Random.Range(0, eventPool.Count);
        BlockedEventOption option = eventPool[randomIndex];
        eventPool.RemoveAt(randomIndex);

        return option;
    }

    private BlockedEventOption CreateContinueOption()
    {
        return new BlockedEventOption
        {
            displayName = "Weiter",
            description = "Der Run läuft weiter. Du hast 60 Sekunden Buildphase, danach startet automatisch die nächste Wave.",
            eventType = BlockedEventType.Continue
        };
    }

    private void UpdateUI()
    {
        if (titleText != null)
            titleText.text = "VERBAUT!";

        if (descriptionText != null)
            descriptionText.text = "Du kannst den Weg nicht mehr erweitern. Wähle ein Event.";

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

        switch (option.eventType)
        {
            case BlockedEventType.Continue:
                Debug.Log("Blocked Event gewählt: Weiter");
                break;

            case BlockedEventType.GoldBonus:
                gameManager.AddGold(option.goldAmount, true, RunGoldSource.BlockedEvent);
                Debug.Log("Blocked Event gewählt: Gold +" + option.goldAmount);
                break;

            case BlockedEventType.LifeBonus:
                gameManager.AddLives(option.lifeAmount);
                Debug.Log("Blocked Event gewählt: Leben +" + option.lifeAmount);
                break;
        }

        gameManager.MarkBlockedEventChosenForCurrentPosition();
        gameManager.StartTimedBuildPhaseAfterBlockedEvent(timedBuildPhaseDuration);
    }

}