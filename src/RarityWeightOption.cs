using System.Globalization;
using Game;
using Menus;
using Menus.UI.Rules;
using Obstacles;

namespace RarityWeights;

/// <summary>
/// A rules-menu row for one rarity weight. Unlike the vanilla options it never touches the networked
/// CustomGameRules struct: the value lives in the host's BepInEx config, so clients don't need the mod.
/// </summary>
public class RarityWeightOption : GameRulesOption
{
    public int rarityIndex;

    private int Value => Plugin.GetWeight(rarityIndex);

    private string DisplayName => $"Rarity Weight {rarityIndex + 1} Star{(rarityIndex == 0 ? "" : "s")}";

    public override int CompareToDefaultValue(GameManagerNetworkVariables.CustomGameRules currentRules)
    {
        return Value.CompareTo(Plugin.GetDefaultWeight(rarityIndex));
    }

    public override string ValueLabel(GameManagerNetworkVariables.CustomGameRules currentRules)
    {
        string label = Value.ToString(CultureInfo.InvariantCulture);
        float? chance = ChancePerDraw();
        return chance.HasValue ? $"{label} ({chance.Value * 100f:0.#}%)" : label;
    }

    /// <summary>
    /// Approximate chance that a single offered obstacle has this rarity, given how many obstacles of each
    /// rarity are allowed by the current rules. Ignores per-obstacle caps and placement contexts.
    /// </summary>
    private float? ChancePerDraw()
    {
        if (!ObstacleManager.instance || ObstacleManager.instance.allowedObstacleDescriptions == null) return null;
        ObstacleDescription[] pool = ObstacleManager.FilterDescriptionsWithGameRules(ObstacleManager.instance.allowedObstacleDescriptions);
        long total = 0, mine = 0;
        foreach (ObstacleDescription d in pool)
        {
            if (!d || d.maxQuantity == 0) continue;
            int r = (int)d.rarity;
            int w = r < Plugin.RarityCount ? Plugin.GetWeight(r) : d.RarityWeight;
            total += w;
            if (r == rarityIndex) mine += w;
        }
        if (total <= 0) return null;
        return (float)mine / total;
    }

    // Includes every vanilla weight (110, 60, 37, 25, 12) so the defaults are always reachable.
    private static readonly int[] Ladder =
        { 0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 37, 45, 50, 60, 75, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400, 500, 750, 1000 };

    public override void PreviousValue(ref GameManagerNetworkVariables.CustomGameRules currentRules)
    {
        int next = Ladder[0];
        foreach (int v in Ladder)
            if (v < Value) next = v;
        Plugin.SetWeight(rarityIndex, next);
    }

    public override void NextValue(ref GameManagerNetworkVariables.CustomGameRules currentRules)
    {
        int next = Ladder[Ladder.Length - 1];
        for (int i = Ladder.Length - 1; i >= 0; i--)
            if (Ladder[i] > Value) next = Ladder[i];
        Plugin.SetWeight(rarityIndex, next);
    }

    public override void ValidateValue(ref GameManagerNetworkVariables.CustomGameRules currentRules)
    {
    }

    public override void ApplyValue(ref GameManagerNetworkVariables.CustomGameRules currentRules)
    {
    }

    public override void UpdateRules(GameManagerNetworkVariables.CustomGameRules newRules)
    {
        // Deliberately skip networkVariables.SetRules: nothing here is replicated.
        if (!GameManager.instance.IsServer) return;
        UpdateStyle();
        GameRulesMenu.Refresh();
    }

    public override void UpdateLabels(bool isDefault = true)
    {
        UpdateNameLabel(isDefault ? DisplayName : "• " + DisplayName);
        UpdateValueLabel();
    }
}
