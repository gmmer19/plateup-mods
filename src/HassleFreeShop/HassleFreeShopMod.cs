using System;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.HassleFreeShop
{
    public sealed class HassleFreeShopMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.hasslefreeshop";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(HassleFreeShopMod).Assembly);
            Debug.Log("[HassleFreeShop] loaded (blueprints and parcels open automatically)");
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    [HarmonyPatch(typeof(PostHelpers), "CreateBlueprintLetter", new Type[]
    {
        typeof(EntityManager), typeof(Vector3), typeof(int), typeof(float), typeof(int), typeof(bool)
    })]
    internal static class CreateBlueprintLetterPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(EntityManager __0, Vector3 __1, int __2, float __3, int __4, bool __5, ref Entity __result)
        {
            if (GameData.Main == null)
            {
                return true;
            }

            int price = 0;
            if (__4 >= 0)
            {
                price = __4;
            }
            else if (GameData.Main.TryGet<Appliance>(__2, out var appliance))
            {
                price = appliance.PurchaseCost;
            }

            Entity entity = __0.CreateEntity();
            __0.AddComponentData(entity, new CCreateAppliance { ID = AssetReference.Blueprint });
            __0.AddComponentData(entity, new CPosition(__1));
            __0.AddComponentData(entity, new CApplianceBlueprint { Appliance = __2 });
            __0.AddComponentData(entity, new CForSale { Price = Mathf.CeilToInt(price * __3) });
            __0.AddComponentData(entity, default(CShopEntity));

            __result = entity;
            return false;
        }
    }

    [HarmonyPatch(typeof(PostHelpers), "CreateApplianceParcel", new Type[]
    {
        typeof(EntityManager), typeof(Vector3), typeof(int)
    })]
    internal static class CreateApplianceParcelPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(EntityManager __0, Vector3 __1, int __2, ref Entity __result)
        {
            Entity entity = __0.CreateEntity();
            __0.AddComponentData(entity, new CCreateAppliance { ID = __2 });
            __0.AddComponentData(entity, new CPosition(__1));

            __result = entity;
            return false;
        }
    }

    [HarmonyPatch(typeof(PostHelpers), "CreateIngredientParcel", new Type[]
    {
        typeof(EntityContext), typeof(Vector3), typeof(int)
    })]
    internal static class CreateIngredientParcelPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(EntityContext __0, Vector3 __1, int __2, ref Entity __result)
        {
            if (GameData.Main == null)
            {
                return true;
            }

            int providerId = 0;
            var defaultProvider = GameData.Main.ReferableObjects.DefaultProvider;
            if (defaultProvider != null)
            {
                providerId = defaultProvider.ID;
            }

            if (GameData.Main.TryGet<Item>(__2, out var item) && item.DedicatedProvider != null)
            {
                providerId = item.DedicatedProvider.ID;
            }

            if (providerId == 0)
            {
                return true;
            }

            Entity entity = __0.CreateEntity();
            __0.Set(entity, new CCreateAppliance { ID = providerId });
            __0.Set(entity, CItemProvider.InfiniteItemProvider(__2));
            __0.Set(entity, new CPosition(__1));

            __result = entity;
            return false;
        }
    }
}
