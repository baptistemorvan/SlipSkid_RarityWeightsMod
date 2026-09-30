using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Obstacles;

namespace RarityWeights;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "slipskid.rarityweights";
    public const string Name = "Rarity Weights";
    public const string Version = "1.0.0";

    /// <summary>Rarities exposed in the rules menu: 1 star (Common) to 5 stars (Legendary), plus Unique.</summary>
    public const int RarityCount = 6;

    internal static ManualLogSource Log;

    // Session state, like the vanilla rules: reset whenever the game resets its rules (new lobby, reset button).
    private static readonly int[] _weights = new int[RarityCount];
    private static readonly int[] VanillaWeights = new int[RarityCount];
    private static Dictionary<ObstacleDescription.ObstacleRarity, int> _weightTable;

    private static ConfigEntry<bool> _debugKey;
    public static bool DebugKeyEnabled => _debugKey.Value;

    public const int MinWeight = 0;
    public const int MaxWeight = 1000;

    private void Awake()
    {
        Log = Logger;

        // The game keeps its weights in a private static table; read the vanilla values from it so the
        // defaults follow the game if a patch ever rebalances them.
        FieldInfo field = AccessTools.Field(typeof(ObstacleDescription), "obstacleRarityWeightTable");
        _weightTable = (Dictionary<ObstacleDescription.ObstacleRarity, int>)field.GetValue(null);

        for (int i = 0; i < RarityCount; i++)
            VanillaWeights[i] = _weightTable[(ObstacleDescription.ObstacleRarity)i];
        ResetWeights();

        _debugKey = Config.Bind("Debug", "HistogramKey", false,
            "Press F8 (host, in a lobby) to log the rarity split of 10000 simulated rolls to the BepInEx log.");

        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{Name} {Version} loaded. Weights: {DescribeWeights()}");
    }

    private void Update() => DebugHistogram.Tick();

    /// <summary>Short tier label: 1* to 5*, then Unique.</summary>
    public static string TierName(int rarity) => rarity < 5 ? $"{rarity + 1}*" : ((ObstacleDescription.ObstacleRarity)rarity).ToString();

    /// <summary>Label used for the rules-menu row and the preset summary.</summary>
    public static string RowName(int rarity) => rarity < 5
        ? $"Rarity Weight {rarity + 1} Star{(rarity == 0 ? "" : "s")}"
        : "Rarity Weight Unique";

    public static int GetWeight(int rarity) => _weights[rarity];

    public static int[] GetWeights() => (int[])_weights.Clone();

    public static int GetDefaultWeight(int rarity) => VanillaWeights[rarity];

    public static void SetWeight(int rarity, int value)
    {
        if (value < MinWeight) value = MinWeight;
        if (value > MaxWeight) value = MaxWeight;
        _weights[rarity] = value;
    }

    public static bool IsCustomized()
    {
        for (int i = 0; i < RarityCount; i++)
            if (GetWeight(i) != VanillaWeights[i]) return true;
        return false;
    }

    public static void ResetWeights()
    {
        for (int i = 0; i < RarityCount; i++) _weights[i] = VanillaWeights[i];
    }

    /// <summary>Sets all weights; tiers missing from <paramref name="weights"/> (or a null array) get vanilla values.</summary>
    public static void SetWeights(int[] weights)
    {
        for (int i = 0; i < RarityCount; i++)
            SetWeight(i, weights != null && i < weights.Length ? weights[i] : VanillaWeights[i]);
    }

    /// <summary>Writes either the custom or the vanilla weights into the game's weight table.</summary>
    internal static void ApplyToTable(bool custom)
    {
        for (int i = 0; i < RarityCount; i++)
            _weightTable[(ObstacleDescription.ObstacleRarity)i] = custom ? GetWeight(i) : VanillaWeights[i];
    }

    internal static string DescribeWeights()
    {
        var parts = new string[RarityCount];
        for (int i = 0; i < RarityCount; i++) parts[i] = $"{TierName(i)}={GetWeight(i)}";
        return string.Join(", ", parts);
    }
}
