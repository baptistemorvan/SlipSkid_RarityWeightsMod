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

    private static ConfigEntry<int>[] _weights;
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

        _weights = new ConfigEntry<int>[RarityCount];
        for (int i = 0; i < RarityCount; i++)
        {
            var rarity = (ObstacleDescription.ObstacleRarity)i;
            VanillaWeights[i] = _weightTable[rarity];
            string key = i < 5 ? $"{i + 1}Star_{rarity}" : rarity.ToString();
            string what = i < 5 ? $"{i + 1}-star ({rarity})" : rarity.ToString();
            _weights[i] = Config.Bind("Weights", key, VanillaWeights[i],
                new ConfigDescription(
                    $"Selection weight of {what} obstacles. Vanilla: {VanillaWeights[i]}. " +
                    "Editable in game from the rules menu (host only).",
                    new AcceptableValueRange<int>(MinWeight, MaxWeight)));
        }

        _debugKey = Config.Bind("Debug", "HistogramKey", false,
            "Press F8 (host, in a lobby) to log the rarity split of 10000 simulated rolls to the BepInEx log.");

        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{Name} {Version} loaded. Weights: {DescribeWeights()}");
    }

    private void Update() => DebugHistogram.Tick();

    /// <summary>Short tier label: 1* to 5*, then Unique.</summary>
    public static string TierName(int rarity) => rarity < 5 ? $"{rarity + 1}*" : ((ObstacleDescription.ObstacleRarity)rarity).ToString();

    public static int GetWeight(int rarity) => _weights[rarity].Value;

    public static int GetDefaultWeight(int rarity) => VanillaWeights[rarity];

    public static void SetWeight(int rarity, int value)
    {
        if (value < MinWeight) value = MinWeight;
        if (value > MaxWeight) value = MaxWeight;
        _weights[rarity].Value = value;
    }

    public static bool IsCustomized()
    {
        for (int i = 0; i < RarityCount; i++)
            if (GetWeight(i) != VanillaWeights[i]) return true;
        return false;
    }

    public static void ResetWeights()
    {
        for (int i = 0; i < RarityCount; i++) _weights[i].Value = VanillaWeights[i];
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
