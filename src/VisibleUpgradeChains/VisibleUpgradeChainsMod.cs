using System.Collections.Generic;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using UnityEngine;

namespace PlateUpMods.VisibleUpgradeChains
{
    public sealed class VisibleUpgradeChainsMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.visibleupgradechains";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(VisibleUpgradeChainsMod).Assembly);
            Debug.Log("[VisibleUpgradeChains] loaded");
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    [HarmonyPatch(typeof(ApplianceInfoView), "UpdateData")]
    internal static class ApplianceInfoViewUpdateDataPatch
    {
        private sealed class PatchState
        {
            public Appliance Appliance;
            public int Index;
        }

        [HarmonyPrefix]
        private static void Prefix(ApplianceInfoView.ViewData __0, out PatchState __state)
        {
            __state = null;

            if (GameData.Main == null || !GameData.Main.TryGet<Appliance>(__0.ID, out var appliance))
            {
                return;
            }

            if (appliance.Sections == null || appliance.Upgrades == null || appliance.Upgrades.Count == 0)
            {
                return;
            }

            var names = new List<string>();
            CollectUpgradeNames(appliance, new HashSet<int>(), names);
            if (names.Count == 0)
            {
                return;
            }

            appliance.Sections.Add(new Appliance.Section
            {
                Title = "Upgrades",
                Description = string.Join(", ", names),
                RangeDescription = ""
            });

            __state = new PatchState
            {
                Appliance = appliance,
                Index = appliance.Sections.Count - 1
            };
        }

        [HarmonyFinalizer]
        private static void Finalizer(PatchState __state)
        {
            if (__state?.Appliance?.Sections == null)
            {
                return;
            }

            if (__state.Index >= 0 && __state.Index < __state.Appliance.Sections.Count)
            {
                __state.Appliance.Sections.RemoveAt(__state.Index);
            }
        }

        private static void CollectUpgradeNames(Appliance appliance, HashSet<int> seen, List<string> names)
        {
            if (appliance.Upgrades == null)
            {
                return;
            }

            foreach (var upgrade in appliance.Upgrades)
            {
                if (upgrade == null || !seen.Add(upgrade.ID))
                {
                    continue;
                }

                names.Add(string.IsNullOrWhiteSpace(upgrade.Name) ? upgrade.ID.ToString() : upgrade.Name);
                CollectUpgradeNames(upgrade, seen, names);
            }
        }
    }
}
