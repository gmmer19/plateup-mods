using System;
using System.Collections.Generic;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenMods;
using UnityEngine;

namespace PlateUpMods.MoreUpgrades
{
    public sealed class MoreUpgradesMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.moreupgrades";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(MoreUpgradesMod).Assembly);
            Debug.Log("[MoreUpgrades] loaded (cycling appliance upgrades)");
        }

        public void PreInject()
        {
            UpgradeSettings.EnsureRegistered();
            Debug.Log("[MoreUpgrades] preferences registered");
        }

        // Game data is fully loaded at this point (CountUp already scans
        // GameData.Main here), so the upgrade lists can be rewired directly.
        // The blueprint improvement desk picks its result with
        // Appliance.Upgrades.Random(), so adding entries turns dead-end
        // upgrades into bidirectional cycles.
        public void PostInject()
        {
            try
            {
                UpgradeCycles.Apply();
            }
            catch (Exception e)
            {
                Debug.LogError("[MoreUpgrades] failed to apply upgrade cycles: " + e);
            }
        }
    }

    internal static class UpgradeSettings
    {
        private const string Ns = MoreUpgradesMod.ModGuid;

        public static readonly Pref Hobs = new Pref(Ns, "Hobs");
        public static readonly Pref Stations = new Pref(Ns, "Stations");
        public static readonly Pref Tables = new Pref(Ns, "Tables");
        public static readonly Pref Shoes = new Pref(Ns, "Shoes");
        public static readonly Pref Tools = new Pref(Ns, "Tools");
        public static readonly Pref Mops = new Pref(Ns, "Mops");

        // 0 = fixed cycle chain (one successor per member), 1 = every member
        // can become any other member of the group.
        public static readonly Pref Mode = new Pref(Ns, "Mode");

        public static void EnsureRegistered()
        {
            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreUpgrades] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreUpgrades] Preferences.Load failed: " + e.Message);
            }
        }

        public static void RegisterIfMissing()
        {
            AddBool(Hobs, true);
            AddBool(Stations, true);
            AddBool(Tables, true);
            AddBool(Shoes, true);
            AddBool(Tools, true);
            AddBool(Mops, true);
            AddInt(Mode, 0);
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

        private static void AddInt(Pref pref, int defaultValue)
        {
            try
            {
                if (Preferences.TryGet<int>(pref, out _))
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
                Preferences.AddPreference(new IntPreference(pref, defaultValue));
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

        public static int GetMode()
        {
            try
            {
                if (Preferences.TryGet<int>(Mode, out var value))
                {
                    return Mathf.Clamp(value, 0, 1);
                }
            }
            catch (Exception)
            {
                // Wrong type or not registered yet.
            }

            return 0;
        }
    }

    // Cycling groups: appliance asset names joined into a loop. When a group
    // is enabled, every member gets all other members of the group added to
    // its Appliance.Upgrades list, so the blueprint improvement desk (which
    // picks Upgrades.Random()) can turn any blueprint of the group into any
    // other type. Each member lists candidate asset names - the first one
    // found in game data wins.
    internal static class UpgradeCycles
    {
        private class CycleGroup
        {
            public readonly string LogName;
            public readonly Pref Setting;
            public readonly bool Default;
            public readonly string[][] Members;

            public CycleGroup(string logName, Pref setting, bool defaultValue, string[][] members)
            {
                LogName = logName;
                Setting = setting;
                Default = defaultValue;
                Members = members;
            }
        }

        private static readonly CycleGroup[] Groups =
        {
            new CycleGroup("hobs", UpgradeSettings.Hobs, true,
                new[]
                {
                    new[] { "Hob - Danger" },
                    new[] { "Hob - Safe" }
                }),
            new CycleGroup("workstations", UpgradeSettings.Stations, true,
                new[]
                {
                    new[] { "Workstation" },
                    new[] { "Freezer" }
                }),
            new CycleGroup("combiners", UpgradeSettings.Stations, true,
                new[]
                {
                    new[] { "Combiner" },
                    new[] { "Portioner" }
                }),
            new CycleGroup("tables", UpgradeSettings.Tables, true,
                new[]
                {
                    // Chain order matches the vanilla cycle exactly, so chain
                    // mode adds nothing here (the vanilla loop is already
                    // deterministic: Bar -> Basic Cloth -> Metal -> Fancy).
                    new[] { "Table - Bar" },
                    new[] { "Table - Basic Cloth" },
                    new[] { "Table - Cheap Metal" },
                    new[] { "Table - Fancy Cloth" }
                }),
            new CycleGroup("shoes", UpgradeSettings.Shoes, true,
                new[]
                {
                    new[] { "Shoe Rack - Work Boots", "Shoe - Work Boot" },
                    new[] { "Shoe Rack - Trainers", "Shoe - Trainer" },
                    new[] { "Shoe Rack - Wellies", "Shoe - Wellies" }
                }),
            new CycleGroup("tools", UpgradeSettings.Tools, true,
                new[]
                {
                    new[] { "Provider - Rolling Pin", "Rolling Pin Provider", "Rolling Pin" },
                    new[] { "Sharp Knife Provider", "Provider - Knife", "Knife Block", "Knife" },
                    new[] { "Provider - Scrubbing Brush", "Scrubbing Brush Provider", "Scrubbing Brush" }
                }),
            new CycleGroup("mops", UpgradeSettings.Mops, true,
                new[]
                {
                    new[] { "Mop Bucket - Lasting", "Mop - Lasting" },
                    new[] { "Mop Bucket - Fast", "Mop - Fast" },
                    new[] { "Robot Mop" },
                    new[] { "Robot Buffer" }
                })
        };

        private static readonly string[] CalibrationFragments =
        {
            "shoe", "tool", "knife", "pin", "brush", "mop", "robot", "rack", "provider", "block"
        };

        private static readonly Dictionary<string, Appliance> AppliancesByName =
            new Dictionary<string, Appliance>(StringComparer.OrdinalIgnoreCase);

        private static bool _applianceIndexBuilt;

        private static void BuildApplianceIndex()
        {
            if (_applianceIndexBuilt || GameData.Main == null)
            {
                return;
            }

            foreach (Appliance appliance in GameData.Main.Get<Appliance>())
            {
                if (appliance == null || string.IsNullOrWhiteSpace(appliance.name))
                {
                    continue;
                }

                string key = appliance.name.Trim();
                if (!AppliancesByName.ContainsKey(key))
                {
                    AppliancesByName[key] = appliance;
                }
            }

            _applianceIndexBuilt = true;
        }

        private static bool TryResolve(string[] candidates, out Appliance appliance, out string matchedName)
        {
            appliance = null;
            matchedName = null;
            BuildApplianceIndex();

            foreach (string candidate in candidates)
            {
                if (AppliancesByName.TryGetValue(candidate.Trim(), out appliance))
                {
                    matchedName = appliance.name;
                    return true;
                }
            }

            return false;
        }

        public static void Apply()
        {
            BuildApplianceIndex();
            DumpVanillaGraph();
            DumpCalibrationFamilies();

            bool cross = UpgradeSettings.GetMode() == 1;
            Debug.Log($"[MoreUpgrades] mode: {(cross ? "cross (any from group)" : "chain (fixed cycle)")}");

            int totalLinks = 0;
            foreach (CycleGroup group in Groups)
            {
                if (!UpgradeSettings.GetBool(group.Setting, group.Default))
                {
                    Debug.Log($"[MoreUpgrades] group '{group.LogName}' disabled, skipped");
                    continue;
                }

                var resolved = new List<Appliance>();
                foreach (string[] candidates in group.Members)
                {
                    if (TryResolve(candidates, out Appliance appliance, out string matchedName))
                    {
                        if (matchedName != candidates[0].Trim())
                        {
                            Debug.Log($"[MoreUpgrades] group '{group.LogName}': \"{candidates[0]}\" matched as \"{matchedName}\"");
                        }

                        resolved.Add(appliance);
                    }
                    else
                    {
                        Debug.LogWarning($"[MoreUpgrades] group '{group.LogName}': appliance \"{candidates[0]}\" not found in game data");
                    }
                }

                if (resolved.Count < 2)
                {
                    Debug.LogWarning($"[MoreUpgrades] group '{group.LogName}' resolved {resolved.Count} appliance(s), skipped");
                    continue;
                }

                int groupLinks = 0;
                for (int i = 0; i < resolved.Count; i++)
                {
                    Appliance appliance = resolved[i];
                    if (appliance.Upgrades == null)
                    {
                        appliance.Upgrades = new List<Appliance>();
                    }

                    // Chain mode: exactly one successor per member (the loop
                    // closes on itself). Cross mode: every member can become
                    // any other member of the group.
                    Appliance[] successors = cross
                        ? resolved.ToArray()
                        : new[] { resolved[(i + 1) % resolved.Count] };

                    foreach (Appliance other in successors)
                    {
                        if (other == appliance || appliance.Upgrades.Contains(other))
                        {
                            continue;
                        }

                        appliance.Upgrades.Add(other);
                        groupLinks++;
                        totalLinks++;
                    }
                }

                string summary = "";
                foreach (Appliance appliance in resolved)
                {
                    summary += $"\n  {appliance.name} -> [{Names(appliance.Upgrades)}]";
                }

                Debug.Log($"[MoreUpgrades] group '{group.LogName}': +{groupLinks} link(s){summary}");
            }

            Debug.Log($"[MoreUpgrades] applied {totalLinks} upgrade link(s)");
        }

        private static string Names(List<Appliance> upgrades)
        {
            if (upgrades == null || upgrades.Count == 0)
            {
                return "none";
            }

            var parts = new List<string>();
            foreach (Appliance upgrade in upgrades)
            {
                parts.Add(upgrade?.name ?? "?");
            }

            return string.Join(", ", parts);
        }

        // One-time dump of the upgrade lists the game ships with, so the
        // vanilla graph can be checked against the cycles from the log.
        private static void DumpVanillaGraph()
        {
            foreach (Appliance appliance in GameData.Main.Get<Appliance>())
            {
                if (appliance?.Upgrades == null || appliance.Upgrades.Count == 0)
                {
                    continue;
                }

                Debug.Log($"[MoreUpgrades] vanilla: {appliance.name} -> [{Names(appliance.Upgrades)}]");
            }
        }

        // Helps calibrate group member names: logs every appliance whose asset
        // name touches one of the cycling families.
        private static void DumpCalibrationFamilies()
        {
            foreach (Appliance appliance in GameData.Main.Get<Appliance>())
            {
                if (appliance == null || string.IsNullOrWhiteSpace(appliance.name))
                {
                    continue;
                }

                string name = appliance.name.Trim();
                foreach (string fragment in CalibrationFragments)
                {
                    if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log($"[MoreUpgrades] family '{fragment}': {name} (id {appliance.ID})");
                        break;
                    }
                }
            }
        }
    }

    public sealed class MoreUpgradesSettingsMenu : Menu<MenuAction>
    {
        public MoreUpgradesSettingsMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            Step("prefs", UpgradeSettings.EnsureRegistered);

            Step("heading", () => AddLabel(L("More Upgrades", "Больше апгрейдов")));

            Step("info", () => AddInfo(L(
                "Blueprint improvement desks convert blueprints inside these groups. Chain mode gives a fixed cycle, cross mode randomly picks any member of the group. Changes apply on the next game start.",
                "Стол улучшения чертежей превращает чертежи внутри групп. Режим «Цепочка» — фиксированный круг, «Все из группы» — случайный выбор среди всех. Изменения применяются после перезапуска игры.")));

            Step("mode label", () => AddLabel(L("Upgrade mode", "Режим улучшений")));
            Step("mode select", () =>
            {
                var modes = new List<string>
                {
                    L("Chain (fixed cycle)", "Цепочка (фиксированный круг)"),
                    L("All from group (random)", "Все из группы (случайно)")
                };

                AddSelect(modes,
                    index => Preferences.Set(UpgradeSettings.Mode, Mathf.Clamp(index, 0, 1)),
                    UpgradeSettings.GetMode());
            });

            AddGroupOption("hobs", UpgradeSettings.Hobs, L("Hobs (Danger / Safe)", "Плиты (Опасная / Безопасная)"), true);
            AddGroupOption("stations", UpgradeSettings.Stations, L("Workstation / Freezer, Combiner / Portioner", "Рабочая станция / Морозильник, Комбайнер / Порционер"), true);
            AddGroupOption("tables", UpgradeSettings.Tables, L("Tables (Bar / Metal / Cloth)", "Столы (Барный / Металлический / Скатерти)"), true);
            AddGroupOption("shoes", UpgradeSettings.Shoes, L("Shoes (Boots / Trainers / Wellies)", "Обувь (Ботинки / Кроссовки / Сапоги)"), true);
            AddGroupOption("tools", UpgradeSettings.Tools, L("Tools (Rolling pin / Knife / Brush)", "Инструменты (Скалка / Нож / Щётка)"), true);
            AddGroupOption("mops", UpgradeSettings.Mops, L("Mops (Lasting / Fast / Robot)", "Швабры (Долговечная / Быстрая / Робот)"), true);

            Step("spacer", () =>
            {
                New<SpacerElement>();
                New<SpacerElement>();
            });

            Step("back", () => AddButton(Localisation["MENU_BACK_SETTINGS"], _ => RequestPreviousMenu()));

            Debug.Log("[MoreUpgrades] settings menu setup done");
        }

        private void AddGroupOption(string stepName, Pref pref, string label, bool fallback)
        {
            Step(stepName + " label", () => AddLabel(label));
            Step(stepName + " select", () =>
            {
                var names = new List<string>
                {
                    L("Disabled", "Выключено"),
                    L("Enabled", "Включено")
                };

                AddSelect(names,
                    index => Preferences.Set(pref, index != 0),
                    UpgradeSettings.GetBool(pref, fallback) ? 1 : 0);
            });
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MoreUpgrades] menu step '{name}' failed: {e}");
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
                if (!menus.ContainsKey(typeof(MoreUpgradesSettingsMenu)))
                {
                    menus.Add(typeof(MoreUpgradesSettingsMenu),
                        new MoreUpgradesSettingsMenu(__instance.Container, __instance.ModuleList));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[MoreUpgrades] CreateSubmenus patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(Preferences), "SetUpPreferences")]
    internal static class PreferencesSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            UpgradeSettings.RegisterIfMissing();
        }
    }
}
