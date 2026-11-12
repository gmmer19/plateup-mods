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

namespace PlateUpMods.BetterTableAssignment
{
    public sealed class BetterTableAssignmentMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.bettertableassignment";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(BetterTableAssignmentMod).Assembly);
            Debug.Log("[BetterTableAssignment] loaded (table priorities for customer seating)");
        }

        public void PreInject()
        {
            TableAssignmentSettings.EnsureRegistered();
            Debug.Log("[BetterTableAssignment] preferences registered");
        }

        public void PostInject() { }
    }

    internal static class TableAssignmentSettings
    {
        // int.MaxValue mirrors the original mod: "Ignore" sorts after every
        // real priority, so unconfigured tables lose to configured ones.
        public const int IgnorePriority = int.MaxValue;

        private const string Ns = BetterTableAssignmentMod.ModGuid;

        public static readonly Pref EnabledPref = new Pref(Ns, "Enabled");

        private static readonly Dictionary<int, Pref> TablePrefs = new Dictionary<int, Pref>();

        public static bool Enabled
        {
            get
            {
                try
                {
                    return Preferences.TryGet<bool>(EnabledPref, out var value) ? value : true;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        public static void EnsureRegistered()
        {
            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BetterTableAssignment] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BetterTableAssignment] Preferences.Load failed: " + e.Message);
            }
        }

        public static void RegisterIfMissing()
        {
            AddBool(EnabledPref, true);
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

        private static bool RegisterIntPref(Pref pref, int defaultValue)
        {
            try
            {
                if (Preferences.TryGet<int>(pref, out _))
                {
                    return false;
                }
            }
            catch (Exception)
            {
                // Wrong type or not registered yet.
            }

            try
            {
                Preferences.AddPreference(new IntPreference(pref, defaultValue));
                return true;
            }
            catch (ArgumentException)
            {
                // Already registered by another call.
                return false;
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

        public static Pref TablePref(int tableId)
        {
            if (!TablePrefs.TryGetValue(tableId, out var pref))
            {
                pref = new Pref(Ns, tableId.ToString());
                TablePrefs[tableId] = pref;
            }

            return pref;
        }

        // Hot path (runs while the game sorts free tables every frame): a plain
        // dictionary lookup unless the table type was never seen before.
        public static int GetPriority(int tableId)
        {
            Pref pref = TablePref(tableId);
            try
            {
                if (Preferences.TryGet<int>(pref, out var value))
                {
                    return value;
                }
            }
            catch (Exception)
            {
                return IgnorePriority;
            }

            if (RegisterIntPref(pref, IgnorePriority))
            {
                // Apply the saved value from disk to the just-registered pref.
                try
                {
                    Preferences.Load();
                }
                catch (Exception)
                {
                    // Fall through to the default.
                }
            }

            try
            {
                return Preferences.TryGet<int>(pref, out var value) ? value : IgnorePriority;
            }
            catch (Exception)
            {
                return IgnorePriority;
            }
        }

        public static void SetPriority(int tableId, int value)
        {
            Preferences.Set(TablePref(tableId), value);
        }

        public static List<string> PriorityNames(int tableCount)
        {
            var names = new List<string>
            {
                KitchenData.Localisation.CurrentLocale == Locale.Russian ? "Игнор" : "Ignore"
            };

            for (int i = 1; i <= tableCount; i++)
            {
                names.Add(i.ToString());
            }

            return names;
        }

        public static int GetPriorityIndex(int tableId, int tableCount)
        {
            int value = GetPriority(tableId);
            return value >= 1 && value <= tableCount ? value : 0;
        }

        public static void SetPriorityFromIndex(int tableId, int index, int tableCount)
        {
            int value = index < 1 ? IgnorePriority : Math.Min(index, tableCount);
            SetPriority(tableId, value);
        }

        // Localized display name first; some table appliances have no
        // localization at all, so fall back to the Unity asset name (what the
        // original mod shows), then the English localization, then the prefab.
        public static string DisplayName(Appliance table)
        {
            string name = table?.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            try
            {
                name = table?.name; // Unity asset name of the GDO
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }

                name = table?.Info?.Get(Locale.English)?.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }

                name = table?.Prefab?.name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch (Exception)
            {
                // No localization available at all.
            }

            return "(ID: " + (table?.ID ?? 0) + ")";
        }

        // Appliances that carry a dining table component but are never used by
        // customer groups (the pet bowl reuses the table system). IDs are stable
        // for this game build, so they are excluded from the menu by ID.
        private static readonly HashSet<int> NonCustomerTableIds = new HashSet<int>
        {
            871235082, // "Animal Bowl" - pet food bowl
            638034870  // "Service Counter" - waiting/reservation desk, nobody eats there
        };

        // Every appliance with a dining table property (waiting tables in the
        // queue area are excluded), sorted by localized display name.
        public static List<Appliance> GetTables()
        {
            var tables = new List<Appliance>();
            if (GameData.Main == null)
            {
                return tables;
            }

            foreach (Appliance appliance in GameData.Main.Get<Appliance>())
            {
                if (appliance?.Properties == null)
                {
                    continue;
                }

                foreach (IApplianceProperty prop in appliance.Properties)
                {
                    if (prop is CApplianceTable table && !table.IsWaitingTable)
                    {
                        // FindTablePlaces only creates seat places where the
                        // table allows sitting, so an appliance with no allowed
                        // direction can never receive customers.
                        if (table.MaxSeats <= 0)
                        {
                            if (!_tablesLogged)
                            {
                                Debug.Log($"[BetterTableAssignment] skipped seatless table: {appliance.ID} \"{DisplayName(appliance)}\"");
                            }
                        }
                        else if (NonCustomerTableIds.Contains(appliance.ID))
                        {
                            if (!_tablesLogged)
                            {
                                Debug.Log($"[BetterTableAssignment] skipped non-customer table: {appliance.ID} \"{DisplayName(appliance)}\"");
                            }
                        }
                        else
                        {
                            tables.Add(appliance);
                        }

                        break;
                    }
                }
            }

            tables.Sort((a, b) => string.Compare(DisplayName(a), DisplayName(b), StringComparison.InvariantCulture));
            return tables;
        }

        // Registers a priority preference for every table type found in the
        // game data and dumps the resulting list once per session.
        public static void RegisterTablePrefs()
        {
            List<Appliance> tables = GetTables();
            if (tables.Count == 0)
            {
                return;
            }

            bool added = false;
            foreach (Appliance table in tables)
            {
                added |= RegisterIntPref(TablePref(table.ID), IgnorePriority);
            }

            if (added)
            {
                try
                {
                    Preferences.Load();
                }
                catch (Exception)
                {
                    // Fall through to the defaults.
                }
            }

            if (_tablesLogged)
            {
                return;
            }

            _tablesLogged = true;
            foreach (Appliance table in tables)
            {
                int priority = GetPriority(table.ID);
                string shown = priority == IgnorePriority ? "Ignore" : priority.ToString();
                string asset;
                try
                {
                    asset = table.name;
                }
                catch (Exception)
                {
                    asset = "";
                }

                Debug.Log($"[BetterTableAssignment] table: {table.ID} \"{DisplayName(table)}\" asset=\"{asset}\" priority={shown}");
            }
        }

        private static bool _tablesLogged;
    }

    // The vanilla comparer is only invoked by ResolveCustomerAssignments.OnUpdate
    // while it sorts free locations, so the running simulation world is
    // remembered here for the duration of that call.
    internal static class AssignmentSortContext
    {
        public static ResolveCustomerAssignments Current;
    }

    [HarmonyPatch(typeof(ResolveCustomerAssignments), "OnUpdate")]
    internal static class ResolveCustomerAssignmentsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ResolveCustomerAssignments __instance)
        {
            AssignmentSortContext.Current = __instance;

            try
            {
                if (!TableAssignmentSettings.Enabled)
                {
                    return;
                }

                // Vanilla gives appliances with CTablePrioritiseCorrectGroups
                // (bar tables) a first-pick pass that bypasses the priorities.
                // The original mod removes the property from the bar table
                // appliance; zeroing it here covers every appliance at once.
                EntityManager em = __instance.EntityManager;
                EntityQuery assignments = em.CreateEntityQuery(ComponentType.ReadOnly<CAvailableAssignment>());
                using NativeArray<CAvailableAssignment> list = assignments.ToComponentDataArray<CAvailableAssignment>(Allocator.Temp);
                for (int i = 0; i < list.Length; i++)
                {
                    CAvailableAssignment assignment = list[i];
                    if (assignment.PrioritiseExactSize)
                    {
                        assignment.PrioritiseExactSize = false;
                        em.SetComponentData(assignment.Entity, assignment);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BetterTableAssignment] exact-size override failed: " + e);
            }
        }

        [HarmonyFinalizer]
        public static Exception Finalizer(Exception __exception)
        {
            AssignmentSortContext.Current = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LocationComparer), "Compare")]
    internal static class LocationComparerPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref int __result, CAvailableAssignment x, CAvailableAssignment y)
        {
            try
            {
                if (!TableAssignmentSettings.Enabled)
                {
                    return true; // vanilla: state, then attractiveness
                }

                int state = (int)x.State;
                __result = -state.CompareTo((int)y.State);
                if (__result != 0)
                {
                    return false;
                }

                if (TryGetTablePriority(x, out int px) && TryGetTablePriority(y, out int py) && px != py)
                {
                    __result = px.CompareTo(py);
                    return false;
                }

                __result = x.MaxCapacity.CompareTo(y.MaxCapacity);
                if (__result != 0)
                {
                    return false;
                }

                return true; // tie: vanilla attractiveness tiebreak
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BetterTableAssignment] comparer fell back to vanilla: " + e);
                return true;
            }
        }

        private static bool TryGetTablePriority(CAvailableAssignment assignment, out int priority)
        {
            priority = TableAssignmentSettings.IgnorePriority;
            if (AssignmentSortContext.Current == null)
            {
                return false;
            }

            EntityManager em = AssignmentSortContext.Current.EntityManager;
            Entity set = assignment.Entity;
            if (!em.HasComponent<CTableSetParts>(set))
            {
                return false; // host stands and other non-table assignments
            }

            DynamicBuffer<CTableSetParts> parts = em.GetBuffer<CTableSetParts>(set);
            if (parts.Length == 0 || !em.HasComponent<CAppliance>(parts[0].Entity))
            {
                return false;
            }

            priority = TableAssignmentSettings.GetPriority(em.GetComponentData<CAppliance>(parts[0].Entity).ID);
            return true;
        }
    }

    [HarmonyPatch(typeof(Preferences), "SetUpPreferences")]
    internal static class PreferencesSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            TableAssignmentSettings.RegisterIfMissing();
        }
    }

    public sealed class BetterTableAssignmentMenu : Menu<MenuAction>
    {
        private const int PageSize = 4;

        private static int _page;

        // The page selector of the current render, kept so focus can return to
        // it after the menu is redrawn (same trick as OptionsMenu's language
        // selector).
        private SelectElement _pageSelect;

        public BetterTableAssignmentMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            Step("prefs", () =>
            {
                TableAssignmentSettings.EnsureRegistered();
                TableAssignmentSettings.RegisterTablePrefs();
            });

            Step("heading", () => AddLabel(
                L("Better Table Assignment", "Приоритет столов")));

            Step("info", () => AddInfo(L(
                "Customers prefer tables with a higher priority. 1 is the highest. \"Ignore\" tables are picked last, smallest first.",
                "Посетители предпочитают столы с большим приоритетом. 1 — самый высокий. «Игнор» выбирается последним, сначала меньшие столы.")));

            Step("enabled label", () => AddLabel(L("Enabled", "Включено")));
            Step("enabled select", () => AddBoolSelect(TableAssignmentSettings.EnabledPref));

            List<Appliance> tables = TableAssignmentSettings.GetTables();
            if (tables.Count == 0)
            {
                Step("no tables", () => AddInfo(L("No table appliances found.", "Столы не найдены.")));
            }
            else
            {
                int pages = (tables.Count + PageSize - 1) / PageSize;
                if (_page >= pages)
                {
                    _page = 0;
                }

                if (pages > 1)
                {
                    var pageNames = new List<string>();
                    for (int i = 1; i <= pages; i++)
                    {
                        pageNames.Add(i + " / " + pages);
                    }

                    int current = _page;
                    Step("page select", () => _pageSelect = AddSelect(pageNames, index =>
                    {
                        _page = index;
                        ModuleList.Clear();
                        Setup(playerId);
                        if (_pageSelect != null)
                        {
                            try
                            {
                                ModuleList.Select(_pageSelect);
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("[BetterTableAssignment] page focus restore failed: " + e.Message);
                            }
                        }
                    }, current));
                }

                int start = _page * PageSize;
                for (int i = start; i < tables.Count && i < start + PageSize; i++)
                {
                    Appliance table = tables[i];
                    int tableCount = tables.Count;
                    Step("table " + table.ID, () =>
                    {
                        AddLabel(TableAssignmentSettings.DisplayName(table));
                        AddSelect(TableAssignmentSettings.PriorityNames(tableCount),
                            index => TableAssignmentSettings.SetPriorityFromIndex(table.ID, index, tableCount),
                            TableAssignmentSettings.GetPriorityIndex(table.ID, tableCount));
                    });
                }
            }

            Step("spacer", () =>
            {
                New<SpacerElement>();
                New<SpacerElement>();
            });

            Step("back", () => AddButton(Localisation["MENU_BACK_SETTINGS"], _ => RequestPreviousMenu()));

            Debug.Log("[BetterTableAssignment] settings menu setup done");
        }

        private void AddBoolSelect(Pref pref)
        {
            var names = new List<string>
            {
                L("Disabled", "Выключено"),
                L("Enabled", "Включено")
            };

            AddSelect(names,
                index => Preferences.Set(pref, index != 0),
                TableAssignmentSettings.GetBool(TableAssignmentSettings.EnabledPref, true) ? 1 : 0);
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BetterTableAssignment] menu step '{name}' failed: {e}");
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
                if (!menus.ContainsKey(typeof(BetterTableAssignmentMenu)))
                {
                    menus.Add(typeof(BetterTableAssignmentMenu),
                        new BetterTableAssignmentMenu(__instance.Container, __instance.ModuleList));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[BetterTableAssignment] CreateSubmenus patch failed: " + e);
            }
        }
    }
}
