using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game;
using HarmonyLib;
using Obstacles;

namespace RarityWeights;

/// <summary>
/// Obstacle choices are rolled only on the host (GameManager.GenerateObstaclesToSelect) and sent to clients as
/// indices, so swapping the weight table around the roll is enough and clients need nothing.
/// </summary>
[HarmonyPatch(typeof(ObstacleDescription), nameof(ObstacleDescription.GetRandomWeightedObstacleDescription))]
internal static class WeightedRollPatch
{
    private static readonly FieldInfo TotalWeightsField = AccessTools.Field(typeof(ObstacleDescription), "_totalWeights");
    private static bool _inFallback;

    private static void Prefix()
    {
        Plugin.ApplyToTable(custom: !_inFallback);
    }

    private static void Postfix(ObstacleDescription[] descriptions, Dictionary<ObstacleDescription, int> selectionHistory,
        (int, ObstacleDescription)[] currentList, int currentCount, ref ObstacleDescription __result)
    {
        if (_inFallback || descriptions == null || descriptions.Length == 0 || !Plugin.IsCustomized())
            return;
        if ((int)TotalWeightsField.GetValue(null) != 0)
            return;

        // Every candidate got weight 0 with the custom table (e.g. the pool was narrowed to rarities the host set to 0).
        // Vanilla would then always hand back the first obstacle; roll again with vanilla weights instead.
        _inFallback = true;
        try
        {
            __result = ObstacleDescription.GetRandomWeightedObstacleDescription(descriptions, selectionHistory, currentList, currentCount);
        }
        finally
        {
            _inFallback = false;
        }
    }

    private static void Finalizer()
    {
        if (!_inFallback) Plugin.ApplyToTable(custom: false);
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.GenerateObstaclesToSelect))]
internal static class GenerationLogPatch
{
    private static void Postfix(GameManager __instance)
    {
        if (!__instance.IsServer || !Plugin.IsCustomized()) return;
        var indices = __instance.networkVariables.obstaclesToSelectIndices;
        var rolled = new List<string>();
        for (int i = 0; i < indices.Count; i++)
        {
            ObstacleDescription d = ObstacleManager.GetDescriptionFromIndex(indices[i]);
            if (d) rolled.Add($"{(int)d.rarity + 1}*");
        }
        Plugin.Log.LogInfo($"Rolled obstacles with custom weights [{Plugin.DescribeWeights()}]: {string.Join(" ", rolled)}");
    }
}
