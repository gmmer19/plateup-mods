using System;
using System.Collections.Generic;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.Overstocked
{
    public sealed class OverstockedMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.overstocked";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(OverstockedMod).Assembly);
            Debug.Log("[Overstocked] loaded (garage shelf restocking)");
        }

        public void PreInject()
        {
            OverstockedSettings.EnsureRegistered();
            Debug.Log("[Overstocked] preferences registered");
        }

        public void PostInject() { }
    }

    internal static class OverstockedSettings
    {
        private const string Ns = OverstockedMod.ModGuid;

        public static readonly Pref AutoRestockPref = new Pref(Ns, "AutoRestock");
        public static readonly Pref DestroyCratesPref = new Pref(Ns, "DestroyCrates");

        public static bool AutoRestock => GetBool(AutoRestockPref, true);
        public static bool DestroyCrates => GetBool(DestroyCratesPref, false);

        public static void EnsureRegistered()
        {
            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overstocked] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overstocked] Preferences.Load failed: " + e.Message);
            }
        }

        public static void RegisterIfMissing()
        {
            AddBool(AutoRestockPref, true);
            AddBool(DestroyCratesPref, false);
        }

        private static void AddBool(Pref pref, bool defaultValue)
        {
            try
            {
                if (Preferences.TryGet<bool>(pref, out _))
                {
                    return;
                }
            }
            catch (Exception)
            {
                // Not registered yet.
            }

            try
            {
                Preferences.AddPreference(new BoolPreference(pref, defaultValue));
            }
            catch (ArgumentException)
            {
                // Already registered by another call.
            }
        }

        public static bool GetBool(Pref pref, bool fallback)
        {
            try
            {
                if (Preferences.TryGet<bool>(pref, out var value))
                {
                    return value;
                }
            }
            catch (Exception)
            {
                // Wrong type or not registered yet.
            }

            return fallback;
        }
    }

    // Marks a garage shelf that should spawn a replacement crate once its
    // holder empties (the crate that was standing there is taken).
    internal struct CCrateItemProvider : IComponentData
    {
        public int ApplianceId;
    }

    // Tags shelves that hold crates with a provider so they restock later.
    // Only plain garage shelves are marked: the inbox (286909233) already
    // replenishes itself in vanilla, workshop outputs use other appliances,
    // and the greenhouse keeps plants under different storage types.
    public class MarkCrateShelvesSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery ShelvesWithoutProvider;

        protected override void Initialise()
        {
            base.Initialise();
            ShelvesWithoutProvider = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[3]
                {
                    ComponentType.ReadOnly<CPersistentItemStorageLocation>(),
                    ComponentType.ReadOnly<CItemHolder>(),
                    ComponentType.ReadOnly<CAppliance>()
                },
                None = new ComponentType[1]
                {
                    ComponentType.ReadOnly<CCrateItemProvider>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!OverstockedSettings.AutoRestock || ShelvesWithoutProvider.IsEmpty)
            {
                return;
            }

            int marked = 0;
            using NativeArray<Entity> shelves = ShelvesWithoutProvider.ToEntityArray(Allocator.Temp);
            foreach (Entity shelf in shelves)
            {
                CAppliance appliance = EntityManager.GetComponentData<CAppliance>(shelf);
                if (appliance.ID != AssetReference.GarageShelf)
                {
                    continue;
                }

                CPersistentItemStorageLocation storage = EntityManager.GetComponentData<CPersistentItemStorageLocation>(shelf);
                if (storage.Type != PersistentStorageType.Crate)
                {
                    continue;
                }

                CItemHolder holder = EntityManager.GetComponentData<CItemHolder>(shelf);
                if (holder.HeldItem == default(Entity)
                    || !EntityManager.HasComponent<CUpgrade>(holder.HeldItem))
                {
                    continue;
                }

                CUpgrade upgrade = EntityManager.GetComponentData<CUpgrade>(holder.HeldItem);
                if (upgrade.ID == 0)
                {
                    continue;
                }

                EntityManager.AddComponentData(shelf, new CCrateItemProvider
                {
                    ApplianceId = upgrade.ID
                });
                marked++;
            }

            if (marked > 0)
            {
                Debug.Log($"[Overstocked] marked {marked} shelf(ves) for restocking");
            }
        }
    }

    // Spawns a replacement crate into any marked shelf whose holder emptied.
    // The crate is built the same way GrantUpgrades builds granted crates:
    // item from the appliance's crate item, CCrate + CPersistentItem +
    // CCrateAppliance for the view, CUpgrade for the contents.
    public class RespawnCratesSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery EmptyProviderShelves;

        protected override void Initialise()
        {
            base.Initialise();
            EmptyProviderShelves = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[2]
                {
                    ComponentType.ReadOnly<CItemHolder>(),
                    ComponentType.ReadOnly<CCrateItemProvider>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!OverstockedSettings.AutoRestock || EmptyProviderShelves.IsEmpty)
            {
                return;
            }

            using NativeArray<Entity> shelves = EmptyProviderShelves.ToEntityArray(Allocator.Temp);
            foreach (Entity shelf in shelves)
            {
                CItemHolder holder = EntityManager.GetComponentData<CItemHolder>(shelf);
                if (holder.HeldItem != default(Entity))
                {
                    continue;
                }

                CCrateItemProvider provider = EntityManager.GetComponentData<CCrateItemProvider>(shelf);
                if (provider.ApplianceId == 0)
                {
                    continue;
                }

                SpawnCrate(shelf, provider.ApplianceId);
            }
        }

        private void SpawnCrate(Entity shelf, int applianceId)
        {
            int itemId = AssetReference.ApplianceCrate;
            if (GameData.Main.TryGet<Appliance>(applianceId, out Appliance appliance) && appliance?.CrateItem != null)
            {
                itemId = appliance.CrateItem.ID;
            }

            EntityContext ctx = new EntityContext(EntityManager);
            Entity crate = ctx.CreateItem(itemId);
            if (crate == default(Entity))
            {
                Debug.LogWarning($"[Overstocked] failed to create crate item {itemId} for appliance {applianceId}");
                return;
            }

            ctx.Set(crate, default(CCrate));
            ctx.Set(crate, new CPersistentItem
            {
                Type = PersistentStorageType.Crate,
                ItemID = itemId
            });
            ctx.Set(crate, new CCrateAppliance
            {
                Appliance = applianceId
            });
            ctx.Set(crate, new CUpgrade
            {
                ID = applianceId,
                IsFromLevel = false
            });
            ctx.Set(crate, default(CPersistThroughSceneChanges));
            ctx.UpdateHolder(crate, shelf);
            ctx.Set(ctx.CreateEntity(), new CRequestSave
            {
                SaveType = SaveType.Auto
            });

            Debug.Log($"[Overstocked] restocked crate ({applianceId})");
        }
    }

    // When restocking is turned off the shelves stop being infinite sources,
    // so the garage can be rearranged normally.
    public class UnmarkCrateShelvesSystem : FranchiseSystem, IModSystem
    {
        private EntityQuery ProviderShelves;

        protected override void Initialise()
        {
            base.Initialise();
            ProviderShelves = GetEntityQuery(typeof(CCrateItemProvider));
        }

        protected override void OnUpdate()
        {
            if (OverstockedSettings.AutoRestock || ProviderShelves.IsEmpty)
            {
                return;
            }

            using NativeArray<Entity> shelves = ProviderShelves.ToEntityArray(Allocator.Temp);
            foreach (Entity shelf in shelves)
            {
                EntityManager.RemoveComponent<CCrateItemProvider>(shelf);
            }

            Debug.Log("[Overstocked] restocking disabled, shelf providers removed");
        }
    }

    // Act on a crate standing on a garage shelf destroys it. The provider is
    // removed first, otherwise the shelf would instantly respawn the crate.
    public class DestroyCrateInteractionSystem : ItemInteractionSystem, IModSystem
    {
        protected override InteractionType RequiredType => InteractionType.Act;

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<SFranchiseMarker>();
        }

        protected override bool IsPossible(ref InteractionData data)
        {
            if (!OverstockedSettings.DestroyCrates)
            {
                return false;
            }

            if (!EntityManager.RequireComponent<CPersistentItemStorageLocation>(data.Target, out CPersistentItemStorageLocation storage)
                || storage.Type != PersistentStorageType.Crate)
            {
                return false;
            }

            if (!EntityManager.RequireComponent<CAppliance>(data.Target, out CAppliance appliance)
                || appliance.ID != AssetReference.GarageShelf)
            {
                return false;
            }

            if (!EntityManager.RequireComponent<CItemHolder>(data.Target, out CItemHolder holder)
                || holder.HeldItem == default(Entity))
            {
                return false;
            }

            return true;
        }

        protected override void Perform(ref InteractionData data)
        {
            CItemHolder holder = EntityManager.GetComponentData<CItemHolder>(data.Target);
            if (holder.HeldItem == default(Entity))
            {
                return;
            }

            int applianceId = 0;
            if (EntityManager.RequireComponent<CUpgrade>(holder.HeldItem, out CUpgrade upgrade))
            {
                applianceId = upgrade.ID;
            }

            if (EntityManager.HasComponent<CCrateItemProvider>(data.Target))
            {
                EntityManager.RemoveComponent<CCrateItemProvider>(data.Target);
            }

            data.Context.Destroy(holder.HeldItem);
            data.Context.Set(data.Context.CreateEntity(), new CRequestSave
            {
                SaveType = SaveType.Auto
            });

            Debug.Log($"[Overstocked] destroyed crate ({applianceId})");
        }
    }

    // Putting the same crate back onto a shelf that already respawned one
    // would fail (the holder is full again); instead the held crate is
    // absorbed so the garage never ends up with a stuck duplicate in hand.
    public class InsertCrateIntoProviderSystem : ItemInteractionSystem, IModSystem
    {
        private Entity TargetItem;

        protected override bool AllowActOrGrab => true;

        protected override void Initialise()
        {
            base.Initialise();
            RequireSingletonForUpdate<SFranchiseMarker>();
        }

        protected override bool IsPossible(ref InteractionData data)
        {
            if (!OverstockedSettings.AutoRestock
                || !EntityManager.RequireComponent<CCrateItemProvider>(data.Target, out CCrateItemProvider provider))
            {
                return false;
            }

            if (!EntityManager.RequireComponent<CItemHolder>(data.Interactor, out CItemHolder held)
                || held.HeldItem == default(Entity))
            {
                return false;
            }

            if (!EntityManager.RequireComponent<CUpgrade>(held.HeldItem, out CUpgrade upgrade)
                || upgrade.ID != provider.ApplianceId)
            {
                return false;
            }

            TargetItem = held.HeldItem;
            return true;
        }

        protected override void Perform(ref InteractionData data)
        {
            data.Context.Destroy(TargetItem);
            Debug.Log("[Overstocked] absorbed returned crate into shelf provider");
        }
    }

    public sealed class OverstockedSettingsMenu : Menu<MenuAction>
    {
        public OverstockedSettingsMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            Step("prefs", OverstockedSettings.EnsureRegistered);

            Step("heading", () => AddLabel("Overstocked"));

            Step("info", () => AddInfo(L(
                "Garage shelves respawn a crate when one is taken. While restocking is on, a shelf stays bound to its crate type - turn it off to rearrange the garage.",
                "Полки гаража выдают новый ящик вместо взятого. Пока пополнение включено, полка привязана к типу своего ящика - чтобы переставлять ящики, временно выключите пополнение.")));

            Step("restock label", () => AddLabel(L("Auto restock shelves", "Автопополнение полок")));
            Step("restock select", () => AddBoolSelect(OverstockedSettings.AutoRestockPref, true));

            Step("destroy info", () => AddInfo(L(
                "Destroy crates with the Act key while looking at a shelf. Crates are removed from storage.",
                "Клавиша Act уничтожает ящик на полке, на которую смотрите. Ящик удаляется из хранилища.")));

            Step("destroy label", () => AddLabel(L("Destroy crates with Act", "Ломать ящики клавишей Act")));
            Step("destroy select", () => AddBoolSelect(OverstockedSettings.DestroyCratesPref, false));

            Step("spacer", () =>
            {
                New<SpacerElement>();
                New<SpacerElement>();
            });

            Step("back", () => AddButton(Localisation["MENU_BACK_SETTINGS"], _ => RequestPreviousMenu()));

            Debug.Log("[Overstocked] settings menu setup done");
        }

        private void AddBoolSelect(Pref pref, bool fallback)
        {
            var names = new List<string>
            {
                L("Disabled", "Выключено"),
                L("Enabled", "Включено")
            };

            AddSelect(names,
                index => Preferences.Set(pref, index != 0),
                OverstockedSettings.GetBool(pref, fallback) ? 1 : 0);
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Overstocked] menu step '{name}' failed: {e}");
            }
        }

        private static string L(string english, string russian)
        {
            return KitchenData.Localisation.CurrentLocale == Locale.Russian ? russian : english;
        }
    }

    [HarmonyPatch(typeof(OptionsMenu<MenuAction>), "CreateSubmenus")]
    internal static class OptionsCreateSubmenusPatch
    {
        [HarmonyPostfix]
        private static void Postfix(OptionsMenu<MenuAction> __instance,
            ref Dictionary<Type, Menu<MenuAction>> menus)
        {
            try
            {
                if (!menus.ContainsKey(typeof(OverstockedSettingsMenu)))
                {
                    menus.Add(typeof(OverstockedSettingsMenu),
                        new OverstockedSettingsMenu(__instance.Container, __instance.ModuleList));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Overstocked] CreateSubmenus patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(Preferences), "SetUpPreferences")]
    internal static class PreferencesSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            OverstockedSettings.RegisterIfMissing();
        }
    }
}
