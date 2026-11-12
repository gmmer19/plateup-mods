using System;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.MoreLoadingBayCarts
{
    public sealed class MoreLoadingBayCartsMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.moreloadingbaycarts";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(MoreLoadingBayCartsMod).Assembly);
            Debug.Log("[MoreLoadingBayCarts] loaded (+2 loadout carts)");
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    [HarmonyPatch(typeof(CreateGarage), "OnUpdate")]
    internal static class CreateGaragePatch
    {
        // Vanilla carts sit at Garage + (1,0,-2) and Garage + (0,0,-2), directly in
        // front of the counter line. Everything else on that row is taken:
        //   x = -1 : velvet-rope decoration
        //   x =  2 : the gap in the counter line, the walkway into the garage
        //   x =  3 : the ghost decoration
        // Both vanilla carts are reached only from the south, so x = 0 and x = 1
        // must stay free on row z = -3, and so must x = 2 so the walkway stays open.
        // That leaves the free floor to the left, one row in front of the rope.
        // Total with the two vanilla carts: 4.
        private static readonly Vector3[] ExtraCarts =
        {
            new Vector3(-1f, 0f, -3f),
            new Vector3(-2f, 0f, -3f)
        };

        [HarmonyPostfix]
        private static void Postfix(CreateGarage __instance)
        {
            if (GameData.Main == null)
            {
                return;
            }

            try
            {
                var em = __instance.EntityManager;
                var existingQuery = em.CreateEntityQuery(ComponentType.ReadOnly<CItemPedestal>());
                int existing = existingQuery.CalculateEntityCount();

                // Two vanilla carts are created by CreateGarage before this postfix.
                // If there are more, the extra carts were already added (or another
                // mod added its own) and we must not duplicate them.
                if (existing > 2)
                {
                    return;
                }

                if (!GameData.Main.TryGet<Appliance>(AssetReference.LoadoutPedestal, out var pedestal))
                {
                    Debug.LogError("[MoreLoadingBayCarts] LoadoutPedestal not found");
                    return;
                }

                int added = 0;
                foreach (var offset in ExtraCarts)
                {
                    var position = LobbyPositionAnchors.Garage + offset;

                    var entity = em.CreateEntity(typeof(CCreateAppliance), typeof(CPosition));
                    em.SetComponentData(entity, new CCreateAppliance { ID = pedestal.ID });
                    em.SetComponentData(entity, new CPosition(position));
                    em.AddComponent<CItemPedestal>(entity);
                    added++;
                }

                Debug.Log($"[MoreLoadingBayCarts] added {added} loadout carts (total {existing + added})");
            }
            catch (Exception e)
            {
                Debug.LogError("[MoreLoadingBayCarts] patch failed: " + e);
            }
        }
    }
}
