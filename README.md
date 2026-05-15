# FactionColors

Nuclear Option BepInEx mod — shows non-friendly units in their own faction color instead of the generic enemy red. Built for missions that use all three (or more) factions, where the vanilla UI lumps every non-friendly faction together as "enemy".

## What it does

By default Nuclear Option only distinguishes Friendly vs Enemy in its HUD/map coloring. In a 3-faction mission where one team is "third party" to you (neither your faction nor your primary enemy), units of all non-friendly factions show up identical red — you can't visually tell who's fighting whom.

This mod patches the unit color resolution to fall back to each faction's actual color (defined on the `Faction` ScriptableObject) when the unit belongs to a faction other than yours. Friendly units stay friendly green; selected units stay selected; only the generic-red bucket gets split into per-faction colors.

## Affected UI

- **Map icons** — non-friendly units on the tactical map use their faction's color
- **HUD target boxes** — the in-cockpit 3D marker boxes around units use the faction color too (survives capture events via UpdateColor postfix)

## Configuration

`BepInEx/config/com.noms.factioncolors.cfg`:

```ini
[General]
Enabled = true
# Only override when 3+ factions are present in the mission.
# Leave true if you want vanilla coloring in normal 2-faction games.
OnlyIfThreePlusFactions = true

[Targets]
PatchMapIcons = true
PatchHUDMarkers = true
```

## Install

1. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx)
2. Drop `FactionColors.dll` into `BepInEx/plugins/`

## Build

Requires .NET 4.7.2 SDK. Hint paths assume the default Steam install.

```
dotnet build -c Release
```

## Credits

- Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyLib](https://github.com/pardeike/Harmony)

## License

MIT.
