using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FactionColors
{
    [BepInPlugin("com.noms.factioncolors", "Faction Colors", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> cfgEnabled;
        internal static ConfigEntry<bool> cfgOnlyIfThreePlusFactions;
        internal static ConfigEntry<bool> cfgPatchMapIcons;
        internal static ConfigEntry<bool> cfgPatchHUDMarkers;

        void Awake()
        {
            Log = Logger;

            cfgEnabled = Config.Bind("General", "Enabled", true,
                "Master toggle. When on, non-friendly units use their own faction color instead of generic enemy red.");
            cfgOnlyIfThreePlusFactions = Config.Bind("General", "OnlyIfThreePlusFactions", true,
                "Only override colors when 3+ factions are present in the mission. Standard 2-faction games are unaffected.");
            cfgPatchMapIcons = Config.Bind("Targets", "PatchMapIcons", true,
                "Replace red on the map's unit icons.");
            cfgPatchHUDMarkers = Config.Bind("Targets", "PatchHUDMarkers", true,
                "Replace red on the in-flight HUD target boxes around units.");

            try
            {
                var harmony = new Harmony("com.noms.factioncolors");
                harmony.PatchAll();
                Logger.LogInfo($"Faction Colors v1.0.0 patched {harmony.GetPatchedMethods().Count()} methods");
            }
            catch (Exception e)
            {
                Logger.LogError($"Harmony patch failed: {e}");
            }
        }

        // === Helpers ===

        internal static bool ShouldOverride(FactionHQ hq)
        {
            if (!cfgEnabled.Value) return false;
            if (hq == null) return false;
            // The override only applies to units the local player views as "enemy".
            // Friendly and spectator-mode coloring is untouched.
            if (DynamicMap.GetFactionMode(hq, checkNoFactionBeforeSpectator: true) != FactionMode.Enemy)
                return false;
            if (cfgOnlyIfThreePlusFactions.Value && FactionRegistry.HQLookup.Count < 3)
                return false;
            if (hq.faction == null) return false;
            return true;
        }
    }

    // === Map icon patch (DynamicMap unit icons) ===

    [HarmonyPatch(typeof(UnitMapIcon), "GetColor")]
    static class UnitMapIconColorPatch
    {
        static readonly FieldInfo f_isSelected = AccessTools.Field(typeof(MapIcon), "isSelected");

        static void Postfix(UnitMapIcon __instance, ref Color __result)
        {
            try
            {
                if (!Plugin.cfgPatchMapIcons.Value) return;
                if (__instance == null || __instance.unit == null) return;

                var hq = __instance.unit.NetworkHQ;
                if (!Plugin.ShouldOverride(hq)) return;

                bool selected = false;
                try { selected = (bool)f_isSelected.GetValue(__instance); } catch { }

                __result = selected ? hq.faction.selectedColor : hq.faction.color;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"UnitMapIcon color patch error: {e.Message}");
            }
        }
    }

    // === HUD target box patch (in-cockpit 3D markers around units) ===
    //
    // HUDUnitMarker.SetNew stores a private `color` field that's later applied to the
    // image. Postfix overwrites that field when the unit belongs to a third party.

    [HarmonyPatch(typeof(HUDUnitMarker), "SetNew")]
    static class HUDUnitMarkerSetNewPatch
    {
        static readonly FieldInfo f_color = AccessTools.Field(typeof(HUDUnitMarker), "color");

        static void Postfix(HUDUnitMarker __instance)
        {
            try
            {
                if (!Plugin.cfgPatchHUDMarkers.Value) return;
                if (__instance == null || __instance.unit == null) return;

                var hq = __instance.unit.NetworkHQ;
                if (!Plugin.ShouldOverride(hq)) return;

                f_color.SetValue(__instance, hq.faction.color);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"HUDUnitMarker color patch error: {e.Message}");
            }
        }
    }

    // UpdateColor runs after SetNew (and on every faction-change event). It reads the
    // private `color` field and writes to image.color. We Postfix it to re-apply the
    // faction color directly to image.color so the override survives capture events
    // that don't re-run SetNew.

    [HarmonyPatch(typeof(HUDUnitMarker), "UpdateColor")]
    static class HUDUnitMarkerUpdateColorPatch
    {
        static readonly FieldInfo f_opacity   = AccessTools.Field(typeof(HUDUnitMarker), "opacity");
        static readonly FieldInfo f_outdated  = AccessTools.Field(typeof(HUDUnitMarker), "outdated");
        static readonly FieldInfo f_maximized = AccessTools.Field(typeof(HUDUnitMarker), "maximized");

        static void Postfix(HUDUnitMarker __instance)
        {
            try
            {
                if (!Plugin.cfgPatchHUDMarkers.Value) return;
                if (__instance == null || __instance.unit == null || __instance.image == null) return;
                if (__instance.selected) return; // selected stays green

                var hq = __instance.unit.NetworkHQ;
                if (!Plugin.ShouldOverride(hq)) return;

                float opacity = 1f;
                bool outdated = false, maximized = false;
                try { opacity = (float)f_opacity.GetValue(__instance); } catch { }
                try { outdated = (bool)f_outdated.GetValue(__instance); } catch { }
                try { maximized = (bool)f_maximized.GetValue(__instance); } catch { }

                var c = hq.faction.color;
                float alpha = opacity * ((outdated && maximized) ? 0.5f : 1f);
                __instance.image.color = new Color(c.r, c.g, c.b, alpha);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"HUDUnitMarker UpdateColor patch error: {e.Message}");
            }
        }
    }
}
