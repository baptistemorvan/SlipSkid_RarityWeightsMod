# Rarity Weights for Slip & Skid

A BepInEx plugin that adds a rules-menu setting for each obstacle rarity weight, so the host can control how
often each rarity shows up in the obstacle selection.

- The rows sit in the rules menu (**Règles / Rules** tab, press **R** in the lobby), just under "Obstacles per round":
  `Rarity Weight 1 Star` … `Rarity Weight 5 Stars`, plus `Rarity Weight Unique`. Unique is the game's sixth
  rarity tier (vanilla weight 2), used by the rarest, special-art obstacles.
- The defaults are the game's own weights: 110 / 60 / 37 / 25 / 12, and 2 for Unique. **Reset values** restores them.
- Each row also shows roughly how likely an offered card is to have that rarity, given the obstacles your
  rules currently allow.
- A weight of 0 removes that rarity. If every obstacle still allowed ends up at 0 (for example when a game
  modifier restricts the pool to rarities you zeroed), that roll uses the vanilla weights instead, so the game
  can't get stuck offering the same card.
- Values are saved to `BepInEx/config/slipskid.rarityweights.cfg`, so they carry over to your next session.
  You can also edit them there.

## Multiplayer

**Only the host needs the mod.** Slip & Skid rolls the obstacle choices on the host and sends clients only the
resulting obstacle IDs. The mod changes only the host's roll and never touches the replicated rules data, so
vanilla players can join and play normally. Clients that do have the mod don't see the rows; only the host
can change them.

These weights aren't included in the game's rule presets.

## Install

1. Install BepInEx 6 (Unity Mono) into the game folder. Your copy already has 6.0.0-be.697.
2. Copy `SlipSkid.RarityWeights.dll` to `Slip & Skid/BepInEx/plugins/RarityWeights/`.
3. Start the game. `BepInEx/LogOutput.log` should show `Rarity Weights 1.0.0 loaded`.

To uninstall, delete the `RarityWeights` folder (and the `.cfg` file if you want).

## Build

Requires the .NET SDK (8+). The build references the DLLs from your own install:

```bash
dotnet build -c Release -p:GameDir="D:\Games\Steam\steamapps\common\Slip & Skid"
```

The build copies the DLL into `BepInEx/plugins/RarityWeights/` automatically.

## Debug

Set `[Debug] HistogramKey = true` in the config. Then press **F8** in a lobby as host, and the log gets the
rarity split of 10,000 simulated rolls using the current weights.

## How it works

- `ObstacleDescription.GetRandomWeightedObstacleDescription`: a Harmony prefix writes the custom weights into
  the game's weight table before each roll, and a finalizer puts the vanilla values back afterwards.
- `GameRulesValuesMenu.Awake`: the "obstacles per round" row is cloned for each rarity, and its component is
  replaced with one that reads and writes the plugin config instead of the networked `CustomGameRules`.

## Credits

Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [Harmony](https://github.com/pardeike/Harmony),
with help from Claude (Anthropic). The repository contains no game files.
