using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenMods;
using UnityEngine;

namespace PlateUpMods.PlateupPrepGhost
{
    public sealed class PlateupPrepGhostMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.plateupprepghost";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(PlateupPrepGhostMod).Assembly);
            Debug.Log("[PrepGhost] loaded (no collision with walls and furniture during prep)");
        }

        public void PreInject()
        {
            PrepGhostSettings.EnsureRegistered();
            Debug.Log("[PrepGhost] preferences registered");
        }

        public void PostInject() { }
    }

    internal static class PrepGhostText
    {
        public static string L(string english, string russian)
        {
            return Localisation.CurrentLocale == Locale.Russian ? russian : english;
        }
    }

    internal static class PrepGhostSettings
    {
        private const string Ns = PlateupPrepGhostMod.ModGuid;

        public static readonly Pref EnabledPref = new Pref(Ns, "Enabled");
        public static readonly Pref SpeedPref = new Pref(Ns, "SpeedIndex");

        // The original mod offers 1x (boost off) up to 10x in 0.5 steps.
        public static readonly float[] Speeds =
        {
            1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f,
            5.5f, 6f, 6.5f, 7f, 7.5f, 8f, 8.5f, 9f, 9.5f, 10f
        };

        public static void EnsureRegistered()
        {
            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrepGhost] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrepGhost] Preferences.Load failed: " + e.Message);
            }
        }

        public static void RegisterIfMissing()
        {
            AddBool(EnabledPref, true);
            AddInt(SpeedPref, 1);
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
                // Not registered yet.
            }

            EnsureRegistered();

            try
            {
                return Preferences.TryGet<bool>(pref, out var value) ? value : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        public static int GetInt(Pref pref, int fallback)
        {
            try
            {
                if (Preferences.TryGet<int>(pref, out var value))
                {
                    return value;
                }
            }
            catch (Exception)
            {
                // Not registered yet.
            }

            EnsureRegistered();

            try
            {
                return Preferences.TryGet<int>(pref, out var value) ? value : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        // PlayerView.Update asks for these values every frame, so they are read
        // from Preferences at most once per frame.
        private static int _cacheFrame = -1;
        private static bool _enabled = true;
        private static int _speedIndex = 1;

        public static void Invalidate()
        {
            _cacheFrame = -1;
        }

        private static void Refresh()
        {
            int frame = Time.frameCount;
            if (frame == _cacheFrame)
            {
                return;
            }

            _cacheFrame = frame;
            _enabled = GetBool(EnabledPref, true);
            _speedIndex = Mathf.Clamp(GetInt(SpeedPref, 1), 0, Speeds.Length - 1);
        }

        public static bool IsEnabled { get { Refresh(); return _enabled; } }
        public static int SpeedIndex { get { Refresh(); return _speedIndex; } }
        public static float SpeedMultiplier { get { Refresh(); return Speeds[_speedIndex]; } }
    }

    internal static class PrepGhostState
    {
        private const float DefaultPlayerSpeed = 3000f;

        private static readonly FieldInfo RigidbodyField = typeof(PlayerWalkingComponent).GetField(
            "Rigidbody", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        // PlayerView.Speed is the prefab's base speed. Keep the original value so
        // the boost can be removed cleanly without accumulating multipliers.
        private static readonly ConditionalWeakTable<PlayerView, SpeedState> BaseSpeeds =
            new ConditionalWeakTable<PlayerView, SpeedState>();

        private sealed class SpeedState
        {
            public float Base;
        }

        private static bool _appliedGhost;
        private static bool _hasApplied;
        private static bool _loggedError;

        public static bool Active
        {
            get
            {
                return PrepGhostSettings.IsEnabled
                       && GameInfo.CurrentScene == SceneType.Kitchen
                       && GameInfo.IsPreparationTime;
            }
        }

        public static void Apply(PlayerView view, PlayerMovementComponent movement)
        {
            var walking = movement as PlayerWalkingComponent;
            if (walking == null)
            {
                return;
            }

            var body = (RigidbodyField != null ? RigidbodyField.GetValue(walking) : null) as Rigidbody;
            if (body == null)
            {
                body = walking.GetComponent<Rigidbody>();
            }

            if (body == null)
            {
                return;
            }

            bool ghost = Active;

            // detectCollisions == false is exactly what the original mod toggles:
            // the player keeps moving with AddForce but ignores every collider.
            if (body.detectCollisions == ghost)
            {
                body.detectCollisions = !ghost;
            }

            var state = BaseSpeeds.GetValue(view, v => new SpeedState { Base = v.Speed });
            float baseSpeed = state.Base > 0f ? state.Base : DefaultPlayerSpeed;
            view.Speed = baseSpeed * (ghost ? PrepGhostSettings.SpeedMultiplier : 1f);

            if (!_hasApplied || _appliedGhost != ghost)
            {
                _hasApplied = true;
                _appliedGhost = ghost;

                if (ghost)
                {
                    Debug.Log($"[PrepGhost] ghost mode enabled (speed {PrepGhostSettings.SpeedMultiplier:0.0}x)");
                }
                else
                {
                    Debug.Log("[PrepGhost] ghost mode disabled");
                }
            }
        }

        public static void LogError(Exception e)
        {
            if (_loggedError)
            {
                return;
            }

            _loggedError = true;
            Debug.LogError("[PrepGhost] player update patch failed: " + e);
        }
    }

    [HarmonyPatch(typeof(PlayerView), "Update")]
    internal static class PlayerViewUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerView __instance, bool ___IsMyPlayer,
            PlayerMovementComponent ___PlayerMovementComp)
        {
            if (!___IsMyPlayer)
            {
                return;
            }

            try
            {
                PrepGhostState.Apply(__instance, ___PlayerMovementComp);
            }
            catch (Exception e)
            {
                PrepGhostState.LogError(e);
            }
        }
    }

    // The game snaps players that leave the playable bounds back to the front
    // door. With ghost mode that would fight the player, so skip it while the
    // ghost is active. It is restored automatically during service.
    [HarmonyPatch(typeof(EnforcePlayerBounds), "OnUpdate")]
    internal static class EnforcePlayerBoundsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !PrepGhostState.Active;
        }
    }

    public sealed class PrepGhostMenu : Menu<MenuAction>
    {
        public PrepGhostMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            PrepGhostSettings.EnsureRegistered();

            Step("heading", () => AddLabel(
                PrepGhostText.L("Ghost Settings", "Настройки призрака")));

            Step("enabled label", () => AddLabel(
                PrepGhostText.L("Ghost Mode in Prep", "Призрак в подготовке")));
            Step("enabled select", () =>
            {
                var names = new List<string>
                {
                    PrepGhostText.L("Disabled", "Выключен"),
                    PrepGhostText.L("Enabled", "Включён")
                };

                AddSelect(names,
                    index =>
                    {
                        Preferences.Set(PrepGhostSettings.EnabledPref, index != 0);
                        PrepGhostSettings.Invalidate();
                    },
                    PrepGhostSettings.IsEnabled ? 1 : 0);
            });

            Step("speed label", () => AddLabel(
                PrepGhostText.L("Ghost Speed", "Скорость в призраке")));
            Step("speed select", () => AddSelect(SpeedNames(),
                index =>
                {
                    Preferences.Set(PrepGhostSettings.SpeedPref, index);
                    PrepGhostSettings.Invalidate();
                },
                PrepGhostSettings.SpeedIndex));

            Step("spacer", () =>
            {
                New<SpacerElement>();
                New<SpacerElement>();
            });

            Step("back", () => AddButton(Localisation["MENU_BACK_SETTINGS"], _ => RequestPreviousMenu()));

            Debug.Log("[PrepGhost] settings menu setup done");
        }

        private static List<string> SpeedNames()
        {
            var names = new List<string>(PrepGhostSettings.Speeds.Length);
            foreach (float speed in PrepGhostSettings.Speeds)
            {
                names.Add(speed.ToString("0.#", CultureInfo.InvariantCulture) + "x");
            }

            return names;
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PrepGhost] menu step '{name}' failed: {e}");
            }
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
                if (!menus.ContainsKey(typeof(PrepGhostMenu)))
                {
                    menus.Add(typeof(PrepGhostMenu),
                        new PrepGhostMenu(__instance.Container, __instance.ModuleList));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PrepGhost] CreateSubmenus patch failed: " + e);
            }
        }
    }
}
