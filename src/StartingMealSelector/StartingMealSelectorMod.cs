using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.StartingMealSelector
{
    public sealed class StartingMealSelectorMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.startingmealselector";

        private const string Ns = ModGuid;

        public static readonly Pref StartWithMeal = new Pref(Ns, "StartWithMeal");

        /// <summary>Dish that should be put on the lobby pedestal on the next system update.</summary>
        public static int PendingDish;

        public void PreInject()
        {
            Settings.EnsureRegistered();
            Debug.Log("[StartingMealSelector] preferences registered");
        }

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(StartingMealSelectorMod).Assembly);
            Debug.Log("[StartingMealSelector] loaded (lobby dish menu)");
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
                if (!Preferences.TryGet<int>(StartingMealSelectorMod.StartWithMeal, out _))
                {
                    Preferences.AddPreference(new IntPreference(StartingMealSelectorMod.StartWithMeal, 0));
                }

                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartingMealSelector] preferences failed: " + e.Message);
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
                Debug.LogWarning("[StartingMealSelector] pref '" + pref + "' is not int: " + e.GetType().Name);
                return fallback;
            }
        }

        public static void SetInt(Pref pref, int value)
        {
            EnsureRegistered();
            try
            {
                Preferences.Set(pref, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartingMealSelector] could not save pref '" + pref + "': " + e.Message);
            }
        }
    }

    /// <summary>
    /// Builds the list of dishes the player has unlocked. Uses the live ECS data
    /// first (CDishUpgrade entities exist in the lobby) and falls back to GameInfo.
    /// </summary>
    internal static class DishCatalog
    {
        public static readonly List<int> Values = new List<int>();
        public static readonly List<string> Names = new List<string>();

        public static void Refresh()
        {
            Values.Clear();
            Names.Clear();

            Values.Add(0);
            Names.Add("Не менять (как в игре)");

            var found = new Dictionary<int, string>();
            CollectFromEntities(found);

            if (found.Count == 0)
            {
                CollectFromGameInfo(found);
            }

            var sorted = new List<KeyValuePair<int, string>>(found);
            sorted.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase));

            foreach (var pair in sorted)
            {
                Values.Add(pair.Key);
                Names.Add(pair.Value);
            }
        }

        public static bool IsValidDish(int dish)
        {
            if (dish == 0 || GameData.Main == null)
            {
                return false;
            }

            try
            {
                return GameData.Main.TryGet<Dish>(dish, out var value) && value != null;
            }
            catch
            {
                return false;
            }
        }

        public static int IndexOf(int dish)
        {
            for (int i = 0; i < Values.Count; i++)
            {
                if (Values[i] == dish)
                {
                    return i;
                }
            }

            return 0;
        }

        public static string NameOf(int dish)
        {
            int index = IndexOf(dish);
            return index > 0 && index < Names.Count ? Names[index] : null;
        }

        /// <summary>Dish currently lying on the primary lobby pedestal.</summary>
        public static int GetCurrentDish()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return 0;
                }

                var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SPrimaryDish>());
                try
                {
                    if (query.CalculateEntityCount() != 1)
                    {
                        return 0;
                    }

                    return world.EntityManager.GetComponentData<SPrimaryDish>(query.GetSingletonEntity()).Dish;
                }
                finally
                {
                    query.Dispose();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartingMealSelector] current dish lookup failed: " + e.Message);
                return 0;
            }
        }

        private static void CollectFromEntities(Dictionary<int, string> found)
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return;
                }

                var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CDishUpgrade>());
                try
                {
                    var data = query.ToComponentDataArray<CDishUpgrade>(Allocator.Temp);
                    try
                    {
                        for (int i = 0; i < data.Length; i++)
                        {
                            int id = data[i].DishID;
                            if (id == 0 || found.ContainsKey(id))
                            {
                                continue;
                            }

                            if (GameData.Main != null && GameData.Main.TryGet<Dish>(id, out var dish) && dish != null)
                            {
                                found[id] = dish.Name;
                            }
                        }
                    }
                    finally
                    {
                        data.Dispose();
                    }
                }
                finally
                {
                    query.Dispose();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartingMealSelector] ECS dish list failed: " + e.Message);
            }
        }

        private static void CollectFromGameInfo(Dictionary<int, string> found)
        {
            try
            {
                foreach (var dish in GameInfo.DishUpgrades)
                {
                    if (dish != null && dish.ID != 0 && !found.ContainsKey(dish.ID) && IsValidDish(dish.ID))
                    {
                        found[dish.ID] = dish.Name;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartingMealSelector] GameInfo dish list failed: " + e.Message);
            }
        }
    }

    public sealed class StartingMealSelectorMenu : Menu<MenuAction>
    {
        private int _selected;
        private ButtonElement _applyButton;
        private ButtonElement _saveButton;
        private InfoBoxElement _savedInfo;

        public StartingMealSelectorMenu(Transform container, ModuleList module_list)
            : base(container, module_list)
        {
        }

        public override void Setup(int player_id)
        {
            DishCatalog.Refresh();
            int currentIndex = DishCatalog.IndexOf(DishCatalog.GetCurrentDish());
            _selected = DishCatalog.Values[currentIndex];
            Debug.Log("[StartingMealSelector] menu opened, dishes=" + (DishCatalog.Values.Count - 1) + ", selected=" + _selected);

            AddLabel("Стартовое блюдо");
            AddInfo("Выбери блюдо, с которым начнётся ресторан. В списке только открытые блюда.");

            if (DishCatalog.Values.Count <= 1)
            {
                AddInfo("Список блюд пуст. Зайди в лобби и открой меню ещё раз.");
            }
            else
            {
                Add(new Option<int>(DishCatalog.Values, _selected, DishCatalog.Names))
                    .OnChanged += delegate (object _, int value)
                    {
                        _selected = value;
                        UpdateButtons();
                    };
            }

            _applyButton = AddButton("Применить к лобби", delegate
            {
                ApplySelection();
                RequestPreviousMenu();
            });

            AddButton("Случайное из открытых", delegate
            {
                if (DishCatalog.Values.Count <= 1)
                {
                    return;
                }

                int index = UnityEngine.Random.Range(1, DishCatalog.Values.Count);
                StartingMealSelectorMod.PendingDish = DishCatalog.Values[index];
                RequestPreviousMenu();
            });

            New<SpacerElement>();

            _saveButton = AddButton("Всегда начинать с этого блюда", delegate
            {
                if (!DishCatalog.IsValidDish(_selected))
                {
                    return;
                }

                Settings.SetInt(StartingMealSelectorMod.StartWithMeal, _selected);
                StartingMealSelectorMod.PendingDish = _selected;
                UpdateButtons();
            });

            AddButton("Сбросить автоматический выбор", delegate
            {
                Settings.SetInt(StartingMealSelectorMod.StartWithMeal, 0);
                UpdateButtons();
            });

            _savedInfo = AddInfo("");
            New<SpacerElement>();
            AddButton(Localisation["MENU_BACK_SETTINGS"], delegate { RequestPreviousMenu(); });

            UpdateButtons();
        }

        private void ApplySelection()
        {
            if (DishCatalog.IsValidDish(_selected))
            {
                StartingMealSelectorMod.PendingDish = _selected;
            }
        }

        private void UpdateButtons()
        {
            bool valid = DishCatalog.IsValidDish(_selected);

            if (_applyButton != null)
            {
                _applyButton.SetSelectable(valid);
            }

            if (_saveButton != null)
            {
                _saveButton.SetSelectable(valid);
            }

            if (_savedInfo == null)
            {
                return;
            }

            int saved = Settings.GetInt(StartingMealSelectorMod.StartWithMeal, 0);
            if (saved == 0)
            {
                _savedInfo.SetLabel("Автоматический выбор блюда выключен.");
                return;
            }

            string name = DishCatalog.NameOf(saved);
            _savedInfo.SetLabel(name != null
                ? "При загрузке лобби будет выбрано: " + name
                : "Сохранённое блюдо сейчас недоступно.");
        }
    }

    /// <summary>
    /// Replaces the dish paper on the primary lobby pedestal with the chosen dish.
    /// </summary>
    public sealed class DishPedestalOverrideSystem : FranchiseSystem, IModSystem
    {
        private bool _appliedDefault;
        private EntityQuery _pedestals;

        protected override void Initialise()
        {
            base.Initialise();
            _pedestals = GetEntityQuery(ComponentType.ReadOnly<SDishPedestal>());
        }

        protected override void OnUpdate()
        {
            if (Has<CSceneFirstFrame>())
            {
                _appliedDefault = false;
            }

            if (!_appliedDefault)
            {
                _appliedDefault = true;
                int saved = Settings.GetInt(StartingMealSelectorMod.StartWithMeal, 0);
                if (saved != 0)
                {
                    if (DishCatalog.IsValidDish(saved))
                    {
                        StartingMealSelectorMod.PendingDish = saved;
                    }
                    else
                    {
                        Settings.SetInt(StartingMealSelectorMod.StartWithMeal, 0);
                        Debug.LogWarning("[StartingMealSelector] saved dish " + saved + " is not available, reset");
                    }
                }
            }

            int dish = StartingMealSelectorMod.PendingDish;
            if (dish == 0)
            {
                return;
            }

            if (!DishCatalog.IsValidDish(dish))
            {
                StartingMealSelectorMod.PendingDish = 0;
                Debug.LogWarning("[StartingMealSelector] requested dish " + dish + " is not available, ignored");
                return;
            }

            if (_pedestals.CalculateEntityCount() != 1)
            {
                return;
            }

            StartingMealSelectorMod.PendingDish = 0;
            Apply(dish);
        }

        private void Apply(int dish)
        {
            Entity pedestal = _pedestals.GetSingletonEntity();
            EntityManager em = EntityManager;

            if (!em.HasComponent<CItemHolder>(pedestal))
            {
                return;
            }

            Entity held = em.GetComponentData<CItemHolder>(pedestal).HeldItem;
            if (held != Entity.Null && em.HasComponent<CDishChoice>(held))
            {
                CDishChoice choice = em.GetComponentData<CDishChoice>(held);
                if (choice.Reason != FixedDishReason.None)
                {
                    // Dish is locked by a speedrun/setting/franchise - do not touch it.
                    return;
                }

                choice.Dish = dish;
                em.SetComponentData(held, choice);
                Debug.Log("[StartingMealSelector] pedestal dish set to " + dish);
                return;
            }

            if (held != Entity.Null)
            {
                return;
            }

            Entity paper = em.CreateEntity(
                typeof(CCreateItem),
                typeof(RebuildKitchen.CNonKitchen),
                typeof(CHeldBy),
                typeof(CHome));

            em.SetComponentData(paper, new CCreateItem { ID = AssetReference.DishPaper });
            em.SetComponentData(paper, new CHeldBy { Holder = pedestal });
            em.SetComponentData(paper, (CHome)pedestal);
            em.SetComponentData(pedestal, new CItemHolder { HeldItem = paper });
            em.AddComponentData(paper, new CDishChoice { Dish = dish, Reason = FixedDishReason.None });

            Debug.Log("[StartingMealSelector] new pedestal paper created for dish " + dish);
        }
    }

    [HarmonyPatch(typeof(MainMenu), "Setup")]
    internal static class MainMenuSetupPatch
    {
        private static bool _logged;
        private static SceneType _lastScene;

        [HarmonyPrefix]
        private static void Prefix(MainMenu __instance)
        {
            // The dish selector only makes sense in the lobby (franchise scene).
            // Do not add the button to the in-run pause menu.
            if (GameInfo.CurrentScene != SceneType.Franchise)
            {
                return;
            }

            if (_lastScene != GameInfo.CurrentScene)
            {
                _lastScene = GameInfo.CurrentScene;
                Debug.Log("[StartingMealSelector] lobby menu detected (scene=" + GameInfo.CurrentScene + ")");
            }

            try
            {
                MethodInfo addSubmenu = typeof(MainMenu).GetMethod(
                    "AddSubmenuButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (addSubmenu == null)
                {
                    Debug.LogWarning("[StartingMealSelector] AddSubmenuButton not found");
                    return;
                }

                addSubmenu.Invoke(__instance, new object[]
                {
                    "Стартовое блюдо",
                    typeof(StartingMealSelectorMenu),
                    false
                });

                if (!_logged)
                {
                    _logged = true;
                    Debug.Log("[StartingMealSelector] menu button added");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartingMealSelector] menu button failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerPauseView), "SetupMenus")]
    internal static class PauseMenuSetupPatch
    {
        private static bool _logged;

        [HarmonyPrefix]
        private static void Prefix(PlayerPauseView __instance)
        {
            try
            {
                FieldInfo moduleListField = FindField(typeof(PlayerPauseView), "ModuleList");
                MethodInfo addMenu = FindMethod(typeof(PlayerPauseView), "AddMenu");
                var moduleList = moduleListField?.GetValue(__instance) as ModuleList;

                if (moduleList == null || addMenu == null)
                {
                    Debug.LogWarning("[StartingMealSelector] pause menu hooks not found");
                    return;
                }

                addMenu.Invoke(__instance, new object[]
                {
                    typeof(StartingMealSelectorMenu),
                    new StartingMealSelectorMenu(__instance.ButtonContainer, moduleList)
                });

                if (!_logged)
                {
                    _logged = true;
                    Debug.Log("[StartingMealSelector] submenu registered");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StartingMealSelector] submenu registration failed: " + e.Message);
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    name,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethod(
                    name,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
