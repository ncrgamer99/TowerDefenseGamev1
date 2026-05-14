using System.Collections.Generic;
using UnityEngine;

public enum ChaosUnlockCategory
{
    Grundlagen,
    RisikoPool,
    ChaosVarianten,
    ChaosWaves,
    Gerechtigkeit,
    Auswertung,
    Zukunft
}

public enum ChaosUnlockConditionType
{
    AlwaysUnlocked,
    ReachChaosLevel,
    SurviveChaosWaves,
    TotalBossKills,
    BossKillsAtOrAboveChaos,
    TotalChaosChoices,
    TotalJusticeChoices,
    HighestGoldJustice,
    HighestXpJustice,
    ChaosVariantKills,
    ChaosWaveBlockWaves
}

[System.Serializable]
public class ChaosUnlockEntry
{
    [Header("Identity")]
    public string unlockId = "unlock.id";
    public string title = "Freischaltung";
    public ChaosUnlockCategory category = ChaosUnlockCategory.RisikoPool;
    public int sortOrder = 0;

    [Header("Text")]
    [TextArea(2, 4)] public string description = "";
    [TextArea(2, 4)] public string lockedHint = "Ein unbekannter Inhalt wartet im Chaos-Pool.";

    [Header("Visibility")]
    public bool unlockedByDefault = false;
    public bool unlocked = false;
    public bool showAsLockedTeaser = true;
    public bool futureOnly = false;

    [Header("Condition")]
    public ChaosUnlockConditionType conditionType = ChaosUnlockConditionType.ReachChaosLevel;
    public int requiredValue = 1;
    public int requiredChaosLevel = 0;

    [Header("Content Pool Unlocks")]
    public List<string> unlockedRiskModifierNames = new List<string>();
    public List<string> unlockedLexiconEntryIds = new List<string>();

    [Header("Runtime Debug")]
    public int lastObservedProgress = 0;
    public string sourceTag = "Step13Default";

    public bool IsUnlocked()
    {
        return unlockedByDefault || unlocked || conditionType == ChaosUnlockConditionType.AlwaysUnlocked;
    }

    public bool IsVisible(bool showLockedTeasers)
    {
        if (IsUnlocked())
            return true;

        return showLockedTeasers && showAsLockedTeaser;
    }

    public bool UnlocksRiskModifier(string modifierName)
    {
        if (string.IsNullOrEmpty(modifierName) || unlockedRiskModifierNames == null)
            return false;

        foreach (string riskName in unlockedRiskModifierNames)
        {
            if (!string.IsNullOrEmpty(riskName) && riskName == modifierName)
                return true;
        }

        return false;
    }

    public string GetDisplayTitle(bool revealLockedTitle)
    {
        if (IsUnlocked() || revealLockedTitle)
            return title;

        switch (category)
        {
            case ChaosUnlockCategory.RisikoPool: return "??? Risiko-Inhalt";
            case ChaosUnlockCategory.ChaosVarianten: return "??? Chaos-Variante";
            case ChaosUnlockCategory.ChaosWaves: return "??? Chaos-Wave";
            case ChaosUnlockCategory.Gerechtigkeit: return "??? Gerechtigkeit";
            case ChaosUnlockCategory.Auswertung: return "??? Auswertung";
            case ChaosUnlockCategory.Zukunft: return "??? Später";
            default: return "??? Freischaltung";
        }
    }

    public string GetConditionText()
    {
        int value = Mathf.Max(1, requiredValue);
        int chaos = Mathf.Max(0, requiredChaosLevel);

        switch (conditionType)
        {
            case ChaosUnlockConditionType.AlwaysUnlocked: return "Von Anfang an verfügbar.";
            case ChaosUnlockConditionType.ReachChaosLevel: return "Erreiche Chaos-Level " + value + ".";
            case ChaosUnlockConditionType.SurviveChaosWaves: return "Überstehe " + value + " Chaos-Wave(s).";
            case ChaosUnlockConditionType.TotalBossKills: return "Besiege " + value + " Boss(e).";
            case ChaosUnlockConditionType.BossKillsAtOrAboveChaos: return "Besiege " + value + " Boss(e) bei Chaos " + chaos + " oder höher.";
            case ChaosUnlockConditionType.TotalChaosChoices: return "Wähle " + value + "x Chaos.";
            case ChaosUnlockConditionType.TotalJusticeChoices: return "Wähle " + value + "x Gerechtigkeit.";
            case ChaosUnlockConditionType.HighestGoldJustice: return "Erreiche Gold-Gerechtigkeit Stufe " + value + ".";
            case ChaosUnlockConditionType.HighestXpJustice: return "Erreiche XP-Gerechtigkeit Stufe " + value + ".";
            case ChaosUnlockConditionType.ChaosVariantKills: return "Töte " + value + " Chaos-Variante(n).";
            case ChaosUnlockConditionType.ChaosWaveBlockWaves: return "Überstehe " + value + " Wave(s) mit Chaos-Wave-Bausteinen.";
            default: return "Unbekannte Bedingung.";
        }
    }

    public string GetUnlockedContentText()
    {
        string text = "";

        if (unlockedRiskModifierNames != null && unlockedRiskModifierNames.Count > 0)
        {
            text += "Risiko-Pool: ";

            for (int i = 0; i < unlockedRiskModifierNames.Count; i++)
            {
                if (i > 0)
                    text += ", ";

                text += unlockedRiskModifierNames[i];
            }
        }

        if (unlockedLexiconEntryIds != null && unlockedLexiconEntryIds.Count > 0)
        {
            if (!string.IsNullOrEmpty(text))
                text += "\n";

            text += "Lexikon-Einträge: " + unlockedLexiconEntryIds.Count;
        }

        if (string.IsNullOrEmpty(text))
            text = "Keine direkte Pool-Erweiterung. Dieser Eintrag dient als Fortschrittsmarker.";

        return text;
    }
}
