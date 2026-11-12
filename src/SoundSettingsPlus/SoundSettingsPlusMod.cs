using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using Kitchen.Components;
using Kitchen.Modules;
using KitchenMods;
using UnityEngine;

namespace PlateUpMods.SoundSettingsPlus
{
    public sealed class SoundSettingsPlusMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.soundsettingsplus";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(SoundSettingsPlusMod).Assembly);
            Debug.Log("[SoundSettings+] loaded (per-sound volume settings)");
        }

        public void PreInject()
        {
            SoundPrefs.EnsureRegistered();
            Debug.Log("[SoundSettings+] preferences registered");
        }

        public void PostInject() { }
    }

    internal static class SoundPrefs
    {
        private const string Namespace = SoundSettingsPlusMod.ModGuid;

        public static readonly Pref DishWasher = new Pref(Namespace, "DishWasherVolume");
        public static readonly Pref Microwave = new Pref(Namespace, "MicrowaveVolume");
        public static readonly Pref Mess = new Pref(Namespace, "MessVolume");
        public static readonly Pref Ding = new Pref(Namespace, "DingVolume");
        public static readonly Pref ProcessComplete = new Pref(Namespace, "ProcessCompleteVolume");
        public static readonly Pref PickupDrop = new Pref(Namespace, "PickupDropVolume");
        public static readonly Pref Footsteps = new Pref(Namespace, "FootstepsVolume");
        public static readonly Pref Door = new Pref(Namespace, "DoorVolume");

        private static readonly Dictionary<Pref, float> Defaults = new Dictionary<Pref, float>
        {
            { DishWasher, 0.25f },
            { Microwave, 0.25f },
            { Mess, 1f },
            { Ding, 1f },
            { ProcessComplete, 1f },
            { PickupDrop, 1f },
            { Footsteps, 1f },
            { Door, 1f }
        };

        private static readonly Dictionary<string, Pref> ClipToPref = new Dictionary<string, Pref>
        {
            { "dishwasher", DishWasher },
            { "Microwave_close", Microwave },
            { "Microwave_open", Microwave },
            { "Microwave_active", Microwave },
            { "mess_created", Mess },
            { "process_complete_special", Ding },
            { "process_complete0", ProcessComplete },
            { "process_complete1", ProcessComplete },
            { "process_complete2", ProcessComplete },
            { "process_complete3", ProcessComplete },
            { "impactWood_light_004", PickupDrop },
            { "impactWood_heavy_003", PickupDrop },
            { "Footsteps_mixdown2", Footsteps }
        };

        public static void RegisterIfMissing()
        {
            foreach (var pair in Defaults)
            {
                bool exists;
                try
                {
                    exists = Preferences.TryGet<float>(pair.Key, out _);
                }
                catch (Exception)
                {
                    exists = false;
                }

                if (exists)
                {
                    continue;
                }

                try
                {
                    Preferences.AddPreference(new FloatPreference(pair.Key, pair.Value));
                }
                catch (ArgumentException)
                {
                    // Already registered by another call; nothing to do.
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
                Debug.LogWarning("[SoundSettings+] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoundSettings+] Preferences.Load failed: " + e.Message);
            }
        }

        public static float Get(Pref pref)
        {
            if (Preferences.TryGet<float>(pref, out var value))
            {
                return value;
            }

            EnsureRegistered();

            if (Preferences.TryGet<float>(pref, out value))
            {
                return value;
            }

            return Defaults.TryGetValue(pref, out var fallback) ? fallback : 1f;
        }

        public static void Set(Pref pref, float value)
        {
            if (!Preferences.TryGet<float>(pref, out _))
            {
                EnsureRegistered();
            }

            Preferences.Set(pref, value);
        }

        public static float FactorForClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                return 1f;
            }

            return ClipToPref.TryGetValue(clipName, out var pref) ? Get(pref) : 1f;
        }
    }

    public sealed class SoundSettingsMenu : Menu<MenuAction>
    {
        public SoundSettingsMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            SoundPrefs.EnsureRegistered();

            AddFloat("Dish Washer", SoundPrefs.DishWasher);
            AddFloat("Microwave", SoundPrefs.Microwave);
            AddFloat("Mess", SoundPrefs.Mess);
            AddFloat("Ding", SoundPrefs.Ding);
            AddFloat("Process Complete", SoundPrefs.ProcessComplete);
            AddFloat("Pickup / Drop", SoundPrefs.PickupDrop);
            AddFloat("Footsteps", SoundPrefs.Footsteps);
            AddFloat("Doors", SoundPrefs.Door);

            New<SpacerElement>();
            New<SpacerElement>();

            AddButton(Localisation["MENU_BACK_SETTINGS"], _ => RequestPreviousMenu());
        }

        private void AddFloat(string label, Pref pref)
        {
            AddLabel(label);

            var option = new Option<float>(
                VolumeManagement.VolumeValues,
                SoundPrefs.Get(pref),
                VolumeManagement.VolumeLevels,
                (a, b) => Math.Abs(a - b));

            AddSelect(option);
            option.OnChanged += (_, value) => SoundPrefs.Set(pref, value);
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
                if (!menus.ContainsKey(typeof(SoundSettingsMenu)))
                {
                    menus.Add(typeof(SoundSettingsMenu),
                        new SoundSettingsMenu(__instance.Container, __instance.ModuleList));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SoundSettings+] CreateSubmenus patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(SoundSource), "Update")]
    internal static class SoundSourcePatch
    {
        private static bool _loggedError;

        [HarmonyPostfix]
        private static void Postfix(SoundSource __instance)
        {
            try
            {
                var audio = __instance.GetComponent<AudioSource>();
                var clip = __instance.Clip;
                if (audio == null || clip == null)
                {
                    return;
                }

                float factor = SoundPrefs.FactorForClip(clip.name);
                if (!Mathf.Approximately(factor, 1f))
                {
                    audio.volume *= factor;
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Debug.LogError("[SoundSettings+] SoundSource patch failed: " + e);
                }
            }
        }
    }

    [HarmonyPatch(typeof(HingeSoundSource), "Update")]
    internal static class HingeSoundSourcePatch
    {
        private static bool _loggedError;

        [HarmonyPostfix]
        private static void Postfix(HingeSoundSource __instance)
        {
            try
            {
                var audio = __instance.GetComponent<AudioSource>();
                if (audio != null)
                {
                    audio.volume *= SoundPrefs.Get(SoundPrefs.Door);
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Debug.LogError("[SoundSettings+] HingeSoundSource patch failed: " + e);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Preferences), "SetUpPreferences")]
    internal static class PreferencesSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            SoundPrefs.RegisterIfMissing();
        }
    }
}
