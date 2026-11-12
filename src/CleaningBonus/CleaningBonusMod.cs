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

namespace PlateUpMods.CleaningBonus
{
    public sealed class CleaningBonusMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.cleaningbonus";

        private const string Ns = ModGuid;

        public static readonly Pref DishPercent = new Pref(Ns, "DishPercent");
        public static readonly Pref FloorPercent = new Pref(Ns, "FloorPercent");
        public static readonly Pref TrashPercent = new Pref(Ns, "TrashPercent");
        public static readonly Pref BonusSeconds = new Pref(Ns, "BonusSeconds");

        public void PreInject()
        {
            Settings.EnsureRegistered();
            CleaningIds.Validate();
            Debug.Log("[CleaningBonus] preferences registered");
        }

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(CleaningBonusMod).Assembly);
            Debug.Log("[CleaningBonus] loaded (end-of-day cleanliness bonuses)");
        }

        public void PostInject()
        {
        }
    }

    internal static class Settings
    {
        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            try
            {
                AddInt(CleaningBonusMod.DishPercent, 8);
                AddInt(CleaningBonusMod.FloorPercent, 8);
                AddInt(CleaningBonusMod.TrashPercent, 4);
                AddInt(CleaningBonusMod.BonusSeconds, 7);
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CleaningBonus] preferences failed: " + e.Message);
            }
        }

        private static void AddInt(Pref pref, int defaultValue)
        {
            if (!Preferences.TryGet<int>(pref, out _))
            {
                Preferences.AddPreference(new IntPreference(pref, defaultValue));
            }
        }

        public static int GetInt(Pref pref, int fallback)
        {
            EnsureRegistered();
            try
            {
                return Preferences.TryGet<int>(pref, out var value) ? value : fallback;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CleaningBonus] pref '" + pref + "' is not int: " + e.GetType().Name);
                return fallback;
            }
        }
    }

    internal static class CleaningIds
    {
        // Dirty plate / pot items.
        public static readonly HashSet<int> DirtyDishes = new HashSet<int>
        {
            1517992271,  // Plate - Dirty
            -1527669626, // Plate - Dirty with Food
            348289471,   // Plate - Dirty with Bone
            1882569246,  // Plate - Dirty Soaked
            1026000491   // Pot - Dirty
        };

        // Mess appliances.
        public static readonly HashSet<int> Messes = new HashSet<int>
        {
            -1324288299, // Mess - Customer 1
            -374077567,  // Mess - Customer 2
            147181555,   // Mess - Customer 3
            31731938,    // Mess - Kitchen 1
            1419995156,  // Mess - Kitchen 2
            34773971     // Mess - Kitchen 3
        };

        // Trash bag items.
        public static readonly HashSet<int> TrashBags = new HashSet<int>
        {
            -1660145659, // Bin Bag
            895813906    // Flammable Bin Bag
        };

        public const int WheelieBin = 2073091578;
        public const int BookingDesk = 238041352;

        // Money tracker identifiers (also used as the appliance icons in the
        // end-of-day earnings breakdown).
        public const int DishWasher = -823922901;
        public const int FloorBufferStation = 1351951642;
        public const int BinCompactor = 2127051779;

        public static void Validate()
        {
            if (GameData.Main == null)
            {
                Debug.LogWarning("[CleaningBonus] game data is not available yet");
                return;
            }

            foreach (int id in DirtyDishes)
            {
                LogAsset<Item>("dirty dish", id);
            }

            foreach (int id in Messes)
            {
                LogAsset<Appliance>("mess", id);
            }

            foreach (int id in TrashBags)
            {
                LogAsset<Item>("trash bag", id);
            }

            LogAsset<Appliance>("wheelie bin", WheelieBin);
            LogAsset<Appliance>("booking desk", BookingDesk);
            LogAsset<Appliance>("dish washer", DishWasher);
            LogAsset<Appliance>("floor buffer station", FloorBufferStation);
            LogAsset<Appliance>("bin compactor", BinCompactor);

            foreach (var item in GameData.Main.Get<Item>())
            {
                if (item.RequiresCleaning
                    || item.name.IndexOf("Dirty", StringComparison.OrdinalIgnoreCase) >= 0
                    || item.name.IndexOf("Bag", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("[CleaningBonus] catalog item " + item.ID + " = " + item.name
                        + " (RequiresCleaning=" + item.RequiresCleaning + ")");
                }
            }

            foreach (var appliance in GameData.Main.Get<Appliance>())
            {
                if (appliance.name.StartsWith("Mess", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[CleaningBonus] catalog appliance " + appliance.ID + " = " + appliance.name);
                }
            }
        }

        private static void LogAsset<T>(string label, int id) where T : GameDataObject
        {
            if (!GameData.Main.TryGet<T>(id, out var asset))
            {
                Debug.LogWarning("[CleaningBonus] " + label + " not found: " + id);
                return;
            }

            Debug.Log("[CleaningBonus] " + label + ": " + id + " = " + asset.name);
        }
    }

    internal static class CleaningState
    {
        public static int Day = int.MinValue;

        public static bool DishUsed;
        public static bool FloorUsed;
        public static bool TrashUsed;

        public static bool DishesRemaining;
        public static bool MessesRemaining;
        public static bool TrashRemaining;

        public static void EnsureDay(int day)
        {
            if (Day == day)
            {
                return;
            }

            Day = day;
            DishUsed = false;
            FloorUsed = false;
            TrashUsed = false;
            DishesRemaining = false;
            MessesRemaining = false;
            TrashRemaining = false;
        }
    }

    internal static class CleaningCounter
    {
        public static int DirtyDishes(EntityQuery items, EntityQuery providers)
        {
            int count = 0;

            var itemData = items.ToComponentDataArray<CItem>(Allocator.Temp);
            try
            {
                for (int i = 0; i < itemData.Length; i++)
                {
                    if (CleaningIds.DirtyDishes.Contains(itemData[i].ID))
                    {
                        count++;
                    }
                }
            }
            finally
            {
                itemData.Dispose();
            }

            var providerData = providers.ToComponentDataArray<CItemProvider>(Allocator.Temp);
            try
            {
                for (int i = 0; i < providerData.Length; i++)
                {
                    if (CleaningIds.DirtyDishes.Contains(providerData[i].ProvidedItem))
                    {
                        count += Mathf.Max(0, providerData[i].Available);
                    }
                }
            }
            finally
            {
                providerData.Dispose();
            }

            return count;
        }

        public static int Messes(EntityQuery appliances)
        {
            int count = 0;

            var data = appliances.ToComponentDataArray<CAppliance>(Allocator.Temp);
            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (CleaningIds.Messes.Contains(data[i].ID))
                    {
                        count++;
                    }
                }
            }
            finally
            {
                data.Dispose();
            }

            return count;
        }

        public static int Trash(EntityManager em, EntityQuery items, EntityQuery providers, EntityQuery bins)
        {
            int count = 0;

            var binEntities = bins.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < binEntities.Length; i++)
                {
                    Entity entity = binEntities[i];
                    CAppliance appliance = em.GetComponentData<CAppliance>(entity);
                    if (appliance.ID == CleaningIds.WheelieBin)
                    {
                        continue;
                    }

                    if (em.GetComponentData<CApplianceBin>(entity).CurrentAmount > 0)
                    {
                        count++;
                    }
                }
            }
            finally
            {
                binEntities.Dispose();
            }

            var itemData = items.ToComponentDataArray<CItem>(Allocator.Temp);
            try
            {
                for (int i = 0; i < itemData.Length; i++)
                {
                    if (CleaningIds.TrashBags.Contains(itemData[i].ID))
                    {
                        count++;
                    }
                }
            }
            finally
            {
                itemData.Dispose();
            }

            var providerData = providers.ToComponentDataArray<CItemProvider>(Allocator.Temp);
            try
            {
                for (int i = 0; i < providerData.Length; i++)
                {
                    if (CleaningIds.TrashBags.Contains(providerData[i].ProvidedItem))
                    {
                        count += Mathf.Max(0, providerData[i].Available);
                    }
                }
            }
            finally
            {
                providerData.Dispose();
            }

            return count;
        }
    }

    [UpdateBefore(typeof(MoneyTracker))]
    [UpdateBefore(typeof(UpdateMoneyTracker))]
    [UpdateBefore(typeof(DestroyAppliancesAtNight))]
    [UpdateBefore(typeof(EndOfDayProgressionGroup))]
    public sealed class CleaningBonusAwardSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _items;
        private EntityQuery _providers;
        private EntityQuery _appliances;
        private EntityQuery _bins;

        protected override void Initialise()
        {
            base.Initialise();
            _items = GetEntityQuery(ComponentType.ReadOnly<CItem>());
            _providers = GetEntityQuery(ComponentType.ReadOnly<CItemProvider>());
            _appliances = GetEntityQuery(ComponentType.ReadOnly<CAppliance>());
            _bins = GetEntityQuery(
                ComponentType.ReadOnly<CAppliance>(),
                ComponentType.ReadOnly<CApplianceBin>());
        }

        protected override void OnUpdate()
        {
            if (HasSingleton<SDay>())
            {
                CleaningState.EnsureDay(GetSingleton<SDay>().Day);
            }

            if (!HasSingleton<SIsNightFirstUpdate>())
            {
                return;
            }

            AwardBonuses();
        }

        private void AwardBonuses()
        {
            if (!HasSingleton<SMoney>() || !HasSingleton<SMoneyEarningsTracker>())
            {
                return;
            }

            int earned = GetSingleton<SMoney>().Amount - GetSingleton<SMoneyEarningsTracker>().OldAmount;
            if (earned <= 0)
            {
                Debug.Log("[CleaningBonus] no earnings today, skipping bonuses");
                return;
            }

            int dirtyDishes = CleaningCounter.DirtyDishes(_items, _providers);
            int messes = CleaningCounter.Messes(_appliances);
            int trash = CleaningCounter.Trash(EntityManager, _items, _providers, _bins);

            Debug.Log("[CleaningBonus] night: dishes=" + dirtyDishes + " (used=" + CleaningState.DishUsed
                + "), messes=" + messes + " (used=" + CleaningState.FloorUsed
                + "), trash=" + trash + " (used=" + CleaningState.TrashUsed
                + "), earned=" + earned);

            Award("dishes", CleaningState.DishUsed, dirtyDishes == 0, earned,
                Settings.GetInt(CleaningBonusMod.DishPercent, 8), CleaningIds.DishWasher);

            Award("floor", CleaningState.FloorUsed, messes == 0, earned,
                Settings.GetInt(CleaningBonusMod.FloorPercent, 8), CleaningIds.FloorBufferStation);

            Award("trash", CleaningState.TrashUsed, trash == 0, earned,
                Settings.GetInt(CleaningBonusMod.TrashPercent, 4), CleaningIds.BinCompactor);
        }

        private void Award(string label, bool used, bool clean, int earned, int percent, int applianceId)
        {
            if (!used || !clean || percent <= 0)
            {
                return;
            }

            int bonus = Mathf.CeilToInt(earned * (percent / 100f));
            if (bonus <= 0)
            {
                return;
            }

            SMoney money = GetSingleton<SMoney>();
            money.Amount += bonus;
            SetSingleton(money);
            MoneyTracker.AddEvent(new EntityContext(EntityManager), applianceId, bonus);

            Debug.Log("[CleaningBonus] " + label + " bonus +" + bonus + " (" + percent + "% of " + earned + ")");
        }
    }

    [UpdateInGroup(typeof(TimeManagementGroup))]
    [UpdateBefore(typeof(AdvanceTime))]
    public sealed class CleaningBonusTimeSystem : RestaurantSystem, IModSystem
    {
        private EntityQuery _items;
        private EntityQuery _providers;
        private EntityQuery _appliances;
        private EntityQuery _bins;
        private EntityQuery _customers;
        private bool _cleaningTimeActive;
        private float _cleaningTimeStart;
        private int _day = int.MinValue;

        protected override void Initialise()
        {
            base.Initialise();
            _items = GetEntityQuery(ComponentType.ReadOnly<CItem>());
            _providers = GetEntityQuery(ComponentType.ReadOnly<CItemProvider>());
            _appliances = GetEntityQuery(ComponentType.ReadOnly<CAppliance>());
            _bins = GetEntityQuery(
                ComponentType.ReadOnly<CAppliance>(),
                ComponentType.ReadOnly<CApplianceBin>());
            _customers = GetEntityQuery(ComponentType.ReadOnly<CCustomerGroup>());
            RequireSingletonForUpdate<SIsDayTime>();
        }

        protected override void OnUpdate()
        {
            if (HasSingleton<SGameOver>() || HasSingleton<SPracticeMode>())
            {
                return;
            }

            if (HasSingleton<SDay>())
            {
                int day = GetSingleton<SDay>().Day;
                if (day != _day)
                {
                    _day = day;
                    _cleaningTimeActive = false;
                    _cleaningTimeStart = 0f;
                    CleaningState.EnsureDay(day);
                }
            }

            int dirtyDishes = CleaningCounter.DirtyDishes(_items, _providers);
            int messes = CleaningCounter.Messes(_appliances);
            int trash = CleaningCounter.Trash(EntityManager, _items, _providers, _bins);

            CleaningState.DishesRemaining = dirtyDishes > 0;
            CleaningState.MessesRemaining = messes > 0;
            CleaningState.TrashRemaining = trash > 0;

            if (dirtyDishes > 0)
            {
                CleaningState.DishUsed = true;
            }

            if (messes > 0)
            {
                CleaningState.FloorUsed = true;
            }

            if (trash > 0)
            {
                CleaningState.TrashUsed = true;
            }

            STime time = GetSingleton<STime>();
            float dayLength = time.DayLength;
            if (dayLength <= 0f)
            {
                return;
            }

            if (!time.ForcePause)
            {
                time.TimeOfDayUnbounded += Time.DeltaTime / dayLength;
            }

            bool needsTime = NeedsBonusTime();

            if (!_cleaningTimeActive)
            {
                if (time.TimeOfDayUnbounded < 1f || !_customers.IsEmpty || !needsTime)
                {
                    return;
                }

                int seconds = Settings.GetInt(CleaningBonusMod.BonusSeconds, 7);
                if (seconds <= 0)
                {
                    return;
                }

                STime current = GetSingleton<STime>();
                current.DayLength = (time.TimeOfDayUnbounded * dayLength) + seconds;
                current.TimeOfDayUnbounded = current.SecondsSinceDayBegan / current.DayLength;
                SetSingleton(current);

                _cleaningTimeActive = true;
                _cleaningTimeStart = current.SecondsSinceDayBegan;
                BreakBookingDesks();
                Debug.Log("[CleaningBonus] cleaning time started (+" + seconds + "s): dishes="
                    + dirtyDishes + ", messes=" + messes + ", trash=" + trash);
            }
            else if (_customers.IsEmpty && !needsTime
                && time.SecondsSinceDayBegan - _cleaningTimeStart >= 1f)
            {
                STime current = GetSingleton<STime>();
                current.DayLength = current.SecondsSinceDayBegan;
                current.TimeOfDayUnbounded = 1f;
                SetSingleton(current);
                Debug.Log("[CleaningBonus] everything is clean (dishes=" + dirtyDishes
                    + ", messes=" + messes + ", trash=" + trash + "), ending day");
            }
        }

        private bool NeedsBonusTime()
        {
            bool dishes = Settings.GetInt(CleaningBonusMod.DishPercent, 8) > 0
                && CleaningState.DishesRemaining;
            bool floor = Settings.GetInt(CleaningBonusMod.FloorPercent, 8) > 0
                && CleaningState.MessesRemaining;
            bool trash = Settings.GetInt(CleaningBonusMod.TrashPercent, 4) > 0
                && CleaningState.TrashRemaining;
            return dishes || floor || trash;
        }

        private void BreakBookingDesks()
        {
            var entities = _appliances.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    if (EntityManager.GetComponentData<CAppliance>(entity).ID != CleaningIds.BookingDesk)
                    {
                        continue;
                    }

                    if (!EntityManager.HasComponent<CIsBroken>(entity))
                    {
                        EntityManager.AddComponentData(entity, default(CIsBroken));
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
    }

    public sealed class CleaningBonusMenu : Menu<MenuAction>
    {
        private static readonly List<int> PercentValues = new List<int>();
        private static readonly List<string> PercentNames = new List<string>();
        private static readonly List<int> SecondValues = new List<int>();
        private static readonly List<string> SecondNames = new List<string>();

        static CleaningBonusMenu()
        {
            for (int i = 0; i <= 20; i++)
            {
                PercentValues.Add(i);
                PercentNames.Add(i == 0 ? "Выкл." : i + "%");
            }

            for (int i = 0; i <= 15; i++)
            {
                SecondValues.Add(i);
                SecondNames.Add(i == 0 ? "Выкл." : i + " сек");
            }

            for (int i = 20; i <= 60; i += 5)
            {
                SecondValues.Add(i);
                SecondNames.Add(i + " сек");
            }
        }

        public CleaningBonusMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            Settings.EnsureRegistered();

            AddLabel("Бонус за чистоту");
            AddInfo("В конце дня начисляется процент от дневного заработка, если соответствующая уборка выполнена.");

            AddLabel("Чистая посуда");
            Add(new Option<int>(PercentValues, Settings.GetInt(CleaningBonusMod.DishPercent, 8), PercentNames))
                .OnChanged += delegate (object _, int value)
                {
                    Preferences.Set(CleaningBonusMod.DishPercent, value);
                };

            AddLabel("Чистый пол");
            Add(new Option<int>(PercentValues, Settings.GetInt(CleaningBonusMod.FloorPercent, 8), PercentNames))
                .OnChanged += delegate (object _, int value)
                {
                    Preferences.Set(CleaningBonusMod.FloorPercent, value);
                };

            AddLabel("Вынесенный мусор");
            Add(new Option<int>(PercentValues, Settings.GetInt(CleaningBonusMod.TrashPercent, 4), PercentNames))
                .OnChanged += delegate (object _, int value)
                {
                    Preferences.Set(CleaningBonusMod.TrashPercent, value);
                };

            AddLabel("Доп. время на уборку");
            Add(new Option<int>(SecondValues, Settings.GetInt(CleaningBonusMod.BonusSeconds, 7), SecondNames))
                .OnChanged += delegate (object _, int value)
                {
                    Preferences.Set(CleaningBonusMod.BonusSeconds, value);
                };

            New<SpacerElement>();
            AddButton(Localisation["MENU_BACK_SETTINGS"], delegate { RequestPreviousMenu(); });
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
                if (!menus.ContainsKey(typeof(CleaningBonusMenu)))
                {
                    menus.Add(typeof(CleaningBonusMenu),
                        new CleaningBonusMenu(__instance.Container, __instance.ModuleList));
                    Debug.Log("[CleaningBonus] settings menu registered");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CleaningBonus] CreateSubmenus patch failed: " + e);
            }
        }
    }
}
