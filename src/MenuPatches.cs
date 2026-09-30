using System.Collections.Generic;
using System.Linq;
using Game;
using HarmonyLib;
using Menus;
using Menus.UI.Rules;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace RarityWeights;

/// <summary>Holds the injected rows on the rules menu so they can be shown to the host and hidden from clients.</summary>
internal class RarityWeightRows : MonoBehaviour
{
    public int listIndex;
    public GameRulesOption anchor;
    public List<RarityWeightOption> rows = new List<RarityWeightOption>();
}

[HarmonyPatch(typeof(GameRulesValuesMenu), "Awake")]
internal static class InjectRowsPatch
{
    private static void Postfix(GameRulesValuesMenu __instance)
    {
        if (__instance.GetComponent<RarityWeightRows>()) return;
        try
        {
            Inject(__instance);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"Could not add rarity weight rows to the rules menu: {e}");
        }
    }

    private static void Inject(GameRulesValuesMenu menu)
    {
        // Clone the "obstacles per round" row: it's a plain numeric option and sits with the obstacle rules.
        int listIndex = -1, anchorIndex = -1;
        for (int i = 0; i < menu.options.Length && listIndex < 0; i++)
            for (int j = 0; j < menu.options[i].Length; j++)
                if (menu.options[i][j] is GameRulesOptionObstacleCountPerRound)
                {
                    listIndex = i;
                    anchorIndex = j;
                    break;
                }
        if (listIndex < 0)
        {
            listIndex = 0;
            anchorIndex = menu.options[0].Length - 1;
        }

        GameRulesOption template = menu.options[listIndex][anchorIndex];
        GameObject templateGo = template.gameObject;
        bool wasActive = templateGo.activeSelf;
        // Clone while inactive so the copied vanilla component never runs OnEnable.
        templateGo.SetActive(false);

        var holder = menu.gameObject.AddComponent<RarityWeightRows>();
        holder.listIndex = listIndex;
        holder.anchor = template;

        int sibling = templateGo.transform.GetSiblingIndex();
        for (int r = 0; r < Plugin.RarityCount; r++)
        {
            GameObject go = Object.Instantiate(templateGo, templateGo.transform.parent);
            go.name = $"RarityWeight{r + 1}Star";
            go.transform.SetSiblingIndex(sibling + 1 + r);

            GameRulesOption old = go.GetComponent<GameRulesOption>();
            RarityWeightOption row = go.AddComponent<RarityWeightOption>();
            row.rarityIndex = r;
            row.optionDisplayName = null;
            row.mainContainer = old.mainContainer;
            row.customButton = old.customButton;
            row.nameLabel = old.nameLabel;
            row.valueLabel = old.valueLabel;
            row.previousButton = old.previousButton;
            row.nextButton = old.nextButton;
            row.scrollRect = old.scrollRect;
            row.topScrollGradient = old.topScrollGradient;
            row.bottomScrollGradient = old.bottomScrollGradient;
            row.enabledStyle = old.enabledStyle;
            row.disabledStyle = old.disabledStyle;
            Object.DestroyImmediate(old);

            // Stop localization components from writing the template's name back over ours.
            foreach (LocalizeStringEvent loc in go.GetComponentsInChildren<LocalizeStringEvent>(true))
                Object.DestroyImmediate(loc);
            if (row.customButton)
            {
                row.customButton.useLocalizationString = false;
                row.customButton.labelContent = null;
            }

            go.SetActive(wasActive);
            holder.rows.Add(row);
        }
        templateGo.SetActive(wasActive);
        Plugin.Log.LogInfo($"Added {Plugin.RarityCount} rarity weight rows to rules list {listIndex} after '{templateGo.name}'.");
    }
}

/// <summary>Before each (re)build, list the rows for the host only, so clients never navigate onto them.</summary>
[HarmonyPatch(typeof(GameRulesValuesMenu), "BuildUI")]
internal static class ShowRowsForHostPatch
{
    private static void Prefix(GameRulesValuesMenu __instance)
    {
        var holder = __instance.GetComponent<RarityWeightRows>();
        if (!holder) return;
        bool isHost = GameManager.instance && GameManager.instance.IsServer;

        var list = __instance.options[holder.listIndex].gameRulesOptionList.Where(o => !(o is RarityWeightOption)).ToList();
        if (isHost)
        {
            int at = list.IndexOf(holder.anchor);
            list.InsertRange(at < 0 ? list.Count : at + 1, holder.rows);
        }
        __instance.options[holder.listIndex].gameRulesOptionList = list.ToArray();
        foreach (RarityWeightOption row in holder.rows)
            row.gameObject.SetActive(isHost && holder.anchor.gameObject.activeSelf);
    }
}

[HarmonyPatch(typeof(GameRulesMenu), nameof(GameRulesMenu.ResetRules))]
internal static class ResetRowsPatch
{
    private static void Postfix()
    {
        if (!GameManager.instance || !GameManager.instance.IsServer) return;
        Plugin.ResetWeights();
        GameRulesMenu.Refresh();
    }
}
