using System.Linq;
using Obstacles;
using UnityEngine.InputSystem;

namespace RarityWeights;

/// <summary>Optional self-test: rolls the current obstacle pool many times and logs the rarity split.</summary>
internal static class DebugHistogram
{
    public static void Tick()
    {
        if (!Plugin.DebugKeyEnabled || Keyboard.current == null || !Keyboard.current.f8Key.wasPressedThisFrame) return;
        if (!ObstacleManager.instance)
        {
            Plugin.Log.LogInfo("Histogram: no ObstacleManager yet (open a lobby first).");
            return;
        }
        ObstacleDescription[] pool = ObstacleManager.FilterDescriptionsWithGameRules(ObstacleManager.instance.allowedObstacleDescriptions);
        const int rolls = 10000;
        var counts = new int[6];
        for (int i = 0; i < rolls; i++)
        {
            ObstacleDescription d = ObstacleDescription.GetRandomWeightedObstacleDescription(pool);
            if (d) counts[(int)d.rarity]++;
        }
        string split = string.Join(", ", Enumerable.Range(0, Plugin.RarityCount).Select(r => $"{Plugin.TierName(r)}={counts[r] * 100f / rolls:0.0}%"));
        Plugin.Log.LogInfo($"Histogram of {rolls} rolls over {pool.Length} obstacles, weights [{Plugin.DescribeWeights()}]: {split}");
    }
}
