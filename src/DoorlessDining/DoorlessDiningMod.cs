using HarmonyLib;
using Kitchen;
using KitchenMods;
using UnityEngine;

namespace PlateUpMods.DoorlessDining
{
    public sealed class DoorlessDiningMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.doorlessdining";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(DoorlessDiningMod).Assembly);
            Debug.Log("[DoorlessDining] loaded (interior doors disabled)");
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    [HarmonyPatch(typeof(LayoutBuilder), "BuildDoorBetween")]
    internal static class LayoutBuilderBuildDoorBetweenPatch
    {
        // Keep exterior doors (front door, garage door) so customer/garage logic stays intact.
        // Skip every interior door, leaving a plain opening instead.
        [HarmonyPrefix]
        private static bool Prefix(bool __2)
        {
            return __2;
        }
    }
}
