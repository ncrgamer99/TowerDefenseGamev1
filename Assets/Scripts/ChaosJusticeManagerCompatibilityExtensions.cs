using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class ChaosJusticeManagerCompatibilityExtensions
{
    public static List<string> GetSelectedRiskModifierDisplayNames(this ChaosJusticeManager manager)
    {
        List<string> names = new List<string>();

        if (manager == null || manager.runData == null)
            return names;

        if (manager.runData.selectedRiskModifiers != null)
        {
            foreach (WaveModifier modifier in manager.runData.selectedRiskModifiers)
            {
                if (modifier == null)
                    continue;

                string displayName = modifier.GetDisplayNameWithLevel();

                if (string.IsNullOrEmpty(displayName))
                    displayName = modifier.displayName;

                if (!string.IsNullOrEmpty(displayName) && !names.Contains(displayName))
                    names.Add(displayName);
            }
        }

        if (names.Count <= 0 && manager.runData.selectedRiskModifierNames != null)
        {
            foreach (string modifierName in manager.runData.selectedRiskModifierNames)
            {
                if (!string.IsNullOrEmpty(modifierName) && !names.Contains(modifierName))
                    names.Add(modifierName);
            }
        }

        return names;
    }

    public static void ResetChaosToOneKeepingRiskModifierAt(this ChaosJusticeManager manager, int keepIndex)
    {
        if (manager == null || manager.runData == null)
            return;

        ChaosJusticeRunData data = manager.runData;
        WaveModifier keptModifier = null;

        if (data.selectedRiskModifiers != null && keepIndex >= 0 && keepIndex < data.selectedRiskModifiers.Count)
            keptModifier = data.selectedRiskModifiers[keepIndex];

        data.chaosLevel = Mathf.Clamp(1, 0, Mathf.Max(1, data.maxChaosLevel));
        data.highestChaosLevel = Mathf.Max(data.highestChaosLevel, data.chaosLevel);

        if (data.selectedRiskModifiers == null)
            data.selectedRiskModifiers = new List<WaveModifier>();

        data.selectedRiskModifiers.Clear();

        if (keptModifier != null)
            data.selectedRiskModifiers.Add(keptModifier);

        if (data.selectedRiskModifierNames == null)
            data.selectedRiskModifierNames = new List<string>();

        data.selectedRiskModifierNames.Clear();

        if (keptModifier != null && !string.IsNullOrEmpty(keptModifier.displayName))
            data.selectedRiskModifierNames.Add(keptModifier.displayName);

        ResetSelectedRiskModifierKeysIfFieldExists(data, keptModifier);
        InvokePrivateNoArgMethod(manager, "ApplyPreparedModifiersToSpawner");
        InvokePrivateNoArgMethod(manager, "ApplyPreparedRiskModifiersToSpawner");
    }

    private static void ResetSelectedRiskModifierKeysIfFieldExists(ChaosJusticeRunData data, WaveModifier keptModifier)
    {
        FieldInfo keysField = typeof(ChaosJusticeRunData).GetField(
            "selectedRiskModifierKeys",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (keysField == null)
            return;

        List<string> keys = keysField.GetValue(data) as List<string>;

        if (keys == null)
        {
            keys = new List<string>();
            keysField.SetValue(data, keys);
        }

        keys.Clear();

        string key = GetStableModifierKey(keptModifier);
        if (!string.IsNullOrEmpty(key))
            keys.Add(key);
    }

    private static string GetStableModifierKey(WaveModifier modifier)
    {
        if (modifier == null)
            return "";

        MethodInfo getStableId = typeof(WaveModifier).GetMethod(
            "GetStableId",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (getStableId != null)
        {
            object result = getStableId.Invoke(modifier, null);
            string stableId = result as string;

            if (!string.IsNullOrEmpty(stableId))
                return stableId;
        }

        return modifier.displayName;
    }

    private static void InvokePrivateNoArgMethod(ChaosJusticeManager manager, string methodName)
    {
        MethodInfo method = typeof(ChaosJusticeManager).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method == null)
            return;

        method.Invoke(manager, null);
    }
}
