using System.Collections.Generic;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using UnityEngine;

namespace PlateUpMods.LargeLayoutsOnly
{
    public sealed class LargeLayoutsOnlyMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.largelayoutsonly";

        private static readonly HashSet<int> LoggedLayouts = new HashSet<int>();

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(LargeLayoutsOnlyMod).Assembly);
            Debug.Log("[LargeLayoutsOnly] loaded");
        }

        public void PreInject() { }

        public void PostInject() { }

        internal static void LogLayout(LayoutProfile profile)
        {
            if (profile == null || !LoggedLayouts.Add(profile.ID))
            {
                return;
            }

            Debug.Log($"[LargeLayoutsOnly] layout id={profile.ID} name='{profile.Name}' maxTables={profile.MaximumTables} season={profile.FixedRunSeason}");
        }
    }

    [HarmonyPatch(typeof(LayoutSeed), MethodType.Constructor, new System.Type[] { typeof(int), typeof(int[]) })]
    internal static class LayoutSeedConstructorPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref int[] __1)
        {
            if (__1 == null || __1.Length <= 1 || GameData.Main == null)
            {
                return;
            }

            var profiles = new List<LayoutProfile>();
            bool hasSeasonal = false;

            foreach (int id in __1)
            {
                if (!GameData.Main.TryGet<LayoutProfile>(id, out var profile))
                {
                    return;
                }

                LargeLayoutsOnlyMod.LogLayout(profile);

                if (profile.FixedRunSeason != Season.Normal)
                {
                    hasSeasonal = true;
                }

                profiles.Add(profile);
            }

            // Seasonal layout lists are intentional; don't touch them.
            if (hasSeasonal)
            {
                return;
            }

            int maxTables = int.MinValue;
            foreach (var profile in profiles)
            {
                if (profile.MaximumTables > maxTables)
                {
                    maxTables = profile.MaximumTables;
                }
            }

            if (maxTables == int.MinValue)
            {
                return;
            }

            var filtered = new List<int>();
            foreach (var profile in profiles)
            {
                if (profile.MaximumTables == maxTables)
                {
                    filtered.Add(profile.ID);
                }
            }

            if (filtered.Count > 0 && filtered.Count < __1.Length)
            {
                Debug.Log($"[LargeLayoutsOnly] filtered {__1.Length} layouts down to {filtered.Count} (maxTables={maxTables})");
                __1 = filtered.ToArray();
            }
        }
    }
}
