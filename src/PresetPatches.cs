using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game;
using HarmonyLib;
using Menus;
using Menus.UI.Rules;
using UnityEngine;

namespace RarityWeights;

/// <summary>
/// Stores the weights inside the game's own preset files as an extra "rarityWeights" array. The game reads presets
/// with JsonUtility, which ignores unknown fields, so these files still load fine without the mod.
/// </summary>
internal static class PresetStore
{
    [Serializable]
    private class PresetWeightsJson
    {
#pragma warning disable CS0649 // written by JsonUtility
        public int[] rarityWeights;
#pragma warning restore CS0649
    }

    // Weights captured when a preset is created, before (or without) it being written to disk.
    private static readonly Dictionary<string, int[]> Pending = new Dictionary<string, int[]>();

    public static void Remember(string presetId, int[] weights)
    {
        if (!string.IsNullOrEmpty(presetId)) Pending[presetId] = weights;
    }

    /// <summary>The preset's weights, or null when it has none (built-in presets, presets saved without the mod).</summary>
    public static int[] Get(GameRulesPresetMenu.GameRulesPreset preset)
    {
        if (string.IsNullOrEmpty(preset.id) || preset.isGame) return null;
        if (Pending.TryGetValue(preset.id, out int[] pending)) return pending;
        try
        {
            string path = GameRulesPresetMenu.PresetFilePath(preset.id);
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<PresetWeightsJson>(File.ReadAllText(path))?.rarityWeights;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not read rarity weights of preset '{preset.name}': {e.Message}");
            return null;
        }
    }

    public static void Write(GameRulesPresetMenu.GameRulesPreset preset)
    {
        int[] weights = Pending.TryGetValue(preset.id, out int[] pending) ? pending : Plugin.GetWeights();
        string path = GameRulesPresetMenu.PresetFilePath(preset.id);
        try
        {
            string text = File.ReadAllText(path);
            int end = text.LastIndexOf('}');
            if (end < 0) return;
            string field = $",\n    \"rarityWeights\": [{string.Join(", ", weights)}]\n";
            File.WriteAllText(path, text.Substring(0, end).TrimEnd() + field + text.Substring(end));
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not save rarity weights into preset '{preset.name}': {e.Message}");
        }
    }
}

[HarmonyPatch(typeof(GameRulesPresetMenu), "CreatePreset")]
internal static class CreatePresetPatch
{
    private static void Postfix(GameRulesPresetMenu.GameRulesPreset __result)
    {
        PresetStore.Remember(__result.id, Plugin.GetWeights());
    }
}

[HarmonyPatch(typeof(GameRulesPresetMenu), nameof(GameRulesPresetMenu.SavePreset))]
internal static class SavePresetPatch
{
    private static void Postfix(GameRulesPresetMenu.GameRulesPreset preset, bool __result)
    {
        if (__result) PresetStore.Write(preset);
    }
}

[HarmonyPatch(typeof(GameManagerNetworkVariables), nameof(GameManagerNetworkVariables.LoadPreset))]
internal static class LoadPresetPatch
{
    private static void Postfix(GameManagerNetworkVariables __instance, GameRulesPresetMenu.GameRulesPreset preset)
    {
        if (!__instance.IsServer || string.IsNullOrEmpty(preset.id)) return;
        // Presets without weights (built-in ones, or saved without the mod) mean vanilla weights, like any other rule.
        Plugin.SetWeights(PresetStore.Get(preset));
        GameRulesMenu.Refresh();
    }
}

/// <summary>Lists changed weights in the preset summary, between the vanilla rules and the obstacle counts.</summary>
[HarmonyPatch(typeof(GameRulesRecapDisplay), nameof(GameRulesRecapDisplay.DisplayPreset))]
internal static class RecapPresetPatch
{
    private static readonly AccessTools.FieldRef<GameRulesRecapDisplay, List<GameRulesRecapDisplayElement>> Elements =
        AccessTools.FieldRefAccess<GameRulesRecapDisplay, List<GameRulesRecapDisplayElement>>("_instantiatedElements");
    private static readonly MethodInfo DisplayValue =
        AccessTools.Method(typeof(GameRulesRecapDisplay), "DisplayValue", new[] { typeof(string), typeof(string), typeof(int) });
    private static readonly MethodInfo DisplayObstacles = AccessTools.Method(typeof(GameRulesRecapDisplay), "DisplayObstacles");

    private static void Postfix(GameRulesRecapDisplay __instance, GameRulesPresetMenu.GameRulesPreset preset)
    {
        int[] weights = PresetStore.Get(preset);
        if (weights == null) return;

        int num = 0;
        foreach (GameRulesRecapDisplayElement element in Elements(__instance))
            if (element.gameObject.activeSelf) num++;

        bool added = false;
        for (int i = 0; i < Plugin.RarityCount && i < weights.Length; i++)
        {
            if (weights[i] == Plugin.GetDefaultWeight(i)) continue;
            DisplayValue.Invoke(__instance, new object[] { Plugin.RowName(i), weights[i].ToString(), num++ });
            added = true;
        }
        if (!added) return;
        DisplayObstacles.Invoke(__instance, new object[] { preset, num });
        __instance.noRuleChangedDisplay.SetActive(false);
    }
}
