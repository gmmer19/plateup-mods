using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenMods;
using UnityEngine;

namespace PlateUpMods.ModSettingsMenu
{
    public sealed class ModSettingsMenuMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.modsettingsmenu";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(ModSettingsMenuMod).Assembly);
            Debug.Log("[ModSettings] loaded (unified mod settings menu)");
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    internal static class ModSettingsText
    {
        public static string L(string english, string russian)
        {
            return KitchenData.Localisation.CurrentLocale == Locale.Russian ? russian : english;
        }
    }

    // One entry point for every mod that exposes settings. The menu types are
    // resolved by name at runtime, so this mod does not need a hard reference to
    // the other mod assemblies and keeps working if one of them is removed.
    public sealed class ModsMenu : Menu<MenuAction>
    {
        public ModsMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            Step("heading", () => AddLabel(
                L("Mod Settings", "Настройки модов")));

            Step("patience", () => AddMenuButton(
                "PatienceIndicator",
                "PlateUpMods.PatienceIndicator.PatienceSettingsMenu",
                L("Patience Indicator", "Индикатор терпения")));

            Step("prepghost", () => AddMenuButton(
                "PlateupPrepGhost",
                "PlateUpMods.PlateupPrepGhost.PrepGhostMenu",
                L("Prep Ghost", "Призрак в подготовке")));

            Step("sound", () => AddMenuButton(
                "SoundSettingsPlus",
                "PlateUpMods.SoundSettingsPlus.SoundSettingsMenu",
                "SoundSettings+"));

            Step("cleaning", () => AddMenuButton(
                "CleaningBonus",
                "PlateUpMods.CleaningBonus.CleaningBonusMenu",
                L("Cleaning Bonus", "Бонус за чистоту")));

            Step("tables", () => AddMenuButton(
                "BetterTableAssignment",
                "PlateUpMods.BetterTableAssignment.BetterTableAssignmentMenu",
                L("Better Table Assignment", "Приоритет столов")));

            Step("overstocked", () => AddMenuButton(
                "Overstocked",
                "PlateUpMods.Overstocked.OverstockedSettingsMenu",
                "Overstocked"));

            Step("upgrades", () => AddMenuButton(
                "MoreUpgrades",
                "PlateUpMods.MoreUpgrades.MoreUpgradesSettingsMenu",
                L("More Upgrades", "Больше апгрейдов")));

            Step("countup", () => AddToggle(
                "plateup.mods.countup",
                "ShowCounts",
                L("Show portion counts", "Показывать количество порций")));

            Step("colors", () => AddToggle(
                "plateup.mods.randomcustomercolors",
                "GroupColors",
                L("Group colors", "Цвет по группам")));

            Step("spacer", () =>
            {
                New<SpacerElement>();
                New<SpacerElement>();
            });

            Step("back", () => AddButton(
                Localisation["MENU_BACK_SETTINGS"], _ => RequestPreviousMenu()));

            Debug.Log("[ModSettings] menu setup done");
        }

        private void AddMenuButton(string assemblyName, string typeName, string label)
        {
            Type type = FindType(assemblyName, typeName);
            if (type == null)
            {
                Debug.LogWarning($"[ModSettings] menu type '{typeName}' not found, skipping");
                return;
            }

            var addSubmenu = typeof(Menu<MenuAction>).GetMethod(
                "AddSubmenuButton", BindingFlags.NonPublic | BindingFlags.Instance);
            if (addSubmenu == null)
            {
                Debug.LogError("[ModSettings] AddSubmenuButton not found");
                return;
            }

            addSubmenu.Invoke(this, new object[] { label, type, false });
        }

        private void AddToggle(string ns, string key, string label)
        {
            var pref = new Pref(ns, key);

            bool registered;
            try
            {
                registered = Preferences.TryGet<bool>(pref, out _);
            }
            catch (Exception)
            {
                registered = false;
            }

            if (!registered)
            {
                Debug.LogWarning($"[ModSettings] pref '{ns}.{key}' is not registered, skipping");
                return;
            }

            AddLabel(label);
            AddBoolOption(pref);
        }

        private static Type FindType(string assemblyName, string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != assemblyName)
                {
                    continue;
                }

                var type = asm.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ModSettings] menu step '{name}' failed: {e}");
            }
        }

        private static string L(string english, string russian)
        {
            return ModSettingsText.L(english, russian);
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
                if (!menus.ContainsKey(typeof(ModsMenu)))
                {
                    menus.Add(typeof(ModsMenu),
                        new ModsMenu(__instance.Container, __instance.ModuleList));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ModSettings] CreateSubmenus patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(OptionsMenu<MenuAction>), "Setup")]
    internal static class OptionsSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(OptionsMenu<MenuAction> __instance)
        {
            try
            {
                var addSubmenu = typeof(Menu<MenuAction>).GetMethod(
                    "AddSubmenuButton", BindingFlags.NonPublic | BindingFlags.Instance);

                var result = addSubmenu?.Invoke(__instance, new object[]
                {
                    ModSettingsText.L("Mods", "Моды"),
                    typeof(ModsMenu),
                    false
                });

                MoveAfterAccessibility(__instance.ModuleList, result as ButtonElement);
                Debug.Log("[ModSettings] mods button added to options menu");
            }
            catch (Exception e)
            {
                Debug.LogError("[ModSettings] Options menu patch failed: " + e);
            }
        }

        private static void MoveAfterAccessibility(ModuleList list, ButtonElement button)
        {
            if (list == null || button == null)
            {
                return;
            }

            int ourIndex = -1;
            for (int i = 0; i < list.Modules.Count; i++)
            {
                if (ReferenceEquals(list.Modules[i].Module, button))
                {
                    ourIndex = i;
                    break;
                }
            }

            if (ourIndex < 0)
            {
                return;
            }

            var ours = list.Modules[ourIndex];
            list.Modules.RemoveAt(ourIndex);

            string accessibility = GameData.Main.GlobalLocalisation["MENU_ACCESSIBILITY_SETTINGS"];
            int target = -1;
            for (int i = 0; i < list.Modules.Count; i++)
            {
                var candidate = list.Modules[i].Module as ButtonElement;
                if (candidate != null && candidate.GetCurrentLabel() == accessibility)
                {
                    target = i;
                    break;
                }
            }

            if (target < 0)
            {
                target = list.Modules.Count - 1;
            }

            list.Modules.Insert(target + 1, ours);
            Relayout(list);
        }

        private static void Relayout(ModuleList list)
        {
            var temp = new ModuleList
            {
                Direction = list.Direction,
                Padding = list.Padding
            };

            for (int i = 0; i < list.Modules.Count; i++)
            {
                var module = list.Modules[i].Module;
                temp.AddModule(module);
                list.Modules[i].Position = module.Position;
            }
        }
    }
}
