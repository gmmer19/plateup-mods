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

namespace PlateUpMods.RandomCustomerColors
{
    public sealed class RandomCustomerColorsMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.randomcustomercolors";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(RandomCustomerColorsMod).Assembly);
            Debug.Log("[RandomCustomerColors] loaded (one color per customer group)");
        }

        public void PreInject()
        {
            ColorSettings.EnsureRegistered();
            Debug.Log("[RandomCustomerColors] preferences registered");
        }

        public void PostInject() { }
    }

    internal static class ColorSettings
    {
        public static readonly Pref GroupColors = new Pref(RandomCustomerColorsMod.ModGuid, "GroupColors");

        // Preferences.Load() re-applies every preference; only react when the value
        // actually changes, otherwise opening the settings menu would recolour all
        // customers again.
        private static bool? _lastApplied;

        public static void EnsureRegistered()
        {
            // Load first so SetUpPreferences() has already populated the vanilla
            // preference map; otherwise AddPreference() would make it skip setup.
            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RandomCustomerColors] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RandomCustomerColors] Preferences.Load failed: " + e.Message);
            }
        }

        public static void RegisterIfMissing()
        {
            if (Preferences.TryGet<bool>(GroupColors, out _))
            {
                return;
            }

            try
            {
                Preferences.AddPreference(new BoolPreference(
                    GroupColors,
                    true,
                    OnGroupColorsChanged));
            }
            catch (ArgumentException)
            {
                // Already registered by another call; nothing to do.
            }
        }

        private static void OnGroupColorsChanged(bool value)
        {
            if (_lastApplied.HasValue && _lastApplied.Value == value)
            {
                return;
            }

            _lastApplied = value;
            CustomerColorApplier.RefreshAll();
        }

        public static bool GroupColorsEnabled
        {
            get
            {
                if (Preferences.TryGet<bool>(GroupColors, out var value))
                {
                    return value;
                }

                EnsureRegistered();

                if (Preferences.TryGet<bool>(GroupColors, out value))
                {
                    return value;
                }

                return true;
            }
        }
    }

    // Resolves the customer group for a view. The group is stored on the customer
    // entity (CBelongsToGroup) and the view is linked to that entity through
    // CLinkedView.Identifier, so the host/single-player simulation can give every
    // member of a group the same color.
    internal static class GroupColors
    {
        private static EntityViewManager _manager;

        public static bool TryGet(CustomerView view, out Color color)
        {
            color = default;

            var manager = GetManager(view);
            if (manager == null)
            {
                return false;
            }

            if (!TryGetViewId(manager, view, out var id))
            {
                return false;
            }

            var world = manager.World;
            if (world == null || !world.IsCreated)
            {
                _manager = null;
                return false;
            }

            Entity group = Entity.Null;
            var em = world.EntityManager;

            using (var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CLinkedView>(),
                ComponentType.ReadOnly<CBelongsToGroup>()))
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                var views = query.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                var groups = query.ToComponentDataArray<CBelongsToGroup>(Allocator.Temp);
                try
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        if (views[i].Identifier == id)
                        {
                            group = groups[i].Group;
                            break;
                        }
                    }
                }
                finally
                {
                    entities.Dispose();
                    views.Dispose();
                    groups.Dispose();
                }
            }

            if (group == Entity.Null)
            {
                return false;
            }

            color = ColorForGroup(group);
            return true;
        }

        public static Color RandomColor()
        {
            return UnityEngine.Random.ColorHSV(0f, 1f, 0f, 1f, 0.5f, 1f, 1f, 1f);
        }

        private static EntityViewManager GetManager(CustomerView view)
        {
            if (_manager != null)
            {
                var world = _manager.World;
                if (world != null && world.IsCreated && _manager.EntityViews.ContainsValue(view))
                {
                    return _manager;
                }

                _manager = null;
            }

            foreach (var world in World.All)
            {
                if (!world.IsCreated)
                {
                    continue;
                }

                var manager = world.GetExistingSystem<EntityViewManager>();
                if (manager != null && manager.EntityViews.ContainsValue(view))
                {
                    _manager = manager;
                    return manager;
                }
            }

            return null;
        }

        private static bool TryGetViewId(EntityViewManager manager, CustomerView view, out ViewIdentifier id)
        {
            foreach (var pair in manager.EntityViews)
            {
                if (ReferenceEquals(pair.Value, view))
                {
                    id = pair.Key;
                    return true;
                }
            }

            id = default;
            return false;
        }

        // Stable per-group color: the group entity index/version is hashed, so the
        // same group keeps its color for the whole run (and after view recreation).
        private static Color ColorForGroup(Entity group)
        {
            unchecked
            {
                uint hash = (uint)group.Index * 2654435761u;
                hash ^= (uint)group.Version * 2246822519u;
                hash ^= hash >> 13;

                float hue = (hash & 0xFFFF) / 65535f;
                float saturation = 0.6f + ((hash >> 16) & 0xFF) / 255f * 0.4f;
                return Color.HSVToRGB(hue, saturation, 1f);
            }
        }
    }

    [HarmonyPatch(typeof(CustomerView), "UpdateData", new Type[] { typeof(CustomerView.ViewData) })]
    internal static class CustomerViewUpdateDataPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CustomerView __instance)
        {
            try
            {
                if (__instance == null || __instance.gameObject == null)
                {
                    return;
                }

                if (__instance.GetComponent<CustomerColorApplier>() != null)
                {
                    return;
                }

                var applier = __instance.gameObject.AddComponent<CustomerColorApplier>();
                applier.Initialize(__instance);
            }
            catch (Exception e)
            {
                Debug.LogError("[RandomCustomerColors] customer patch failed: " + e);
            }
        }
    }

    // The vanilla customer prefab may carry a Recolour component which applies a
    // weak random tint in Start(). Re-apply our stronger color afterwards.
    [HarmonyPatch(typeof(Recolour), "Start")]
    internal static class RecolourStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Recolour __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                var customer = __instance.GetComponentInParent<CustomerView>();
                if (customer == null)
                {
                    return;
                }

                var applier = customer.GetComponent<CustomerColorApplier>();
                if (applier != null)
                {
                    applier.Reapply();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[RandomCustomerColors] recolour patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(Preferences), "SetUpPreferences")]
    internal static class PreferencesSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ColorSettings.RegisterIfMissing();
        }
    }

    public sealed class CustomerColorApplier : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color0");
        private static readonly List<CustomerColorApplier> Active = new List<CustomerColorApplier>();

        // Give the simulation a couple of seconds to expose the group. On a
        // multiplayer client there is no customer simulation, so we fall back to
        // the old per-customer random color.
        private const float ResolveTimeout = 2f;

        private static int _logged;

        private CustomerView _view;
        private Color _color;
        private bool _hasColor;
        private bool _applied;
        private float _deadline;

        public static void RefreshAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var applier = Active[i];
                if (applier == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                applier.ResetColor();
            }
        }

        public void Initialize(CustomerView view)
        {
            _view = view;
            _deadline = Time.unscaledTime + ResolveTimeout;
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        public void Apply()
        {
            if (_applied || !_hasColor)
            {
                return;
            }

            bool found = false;
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                ApplyTo(renderer);
                found = true;
            }

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                ApplyTo(renderer);
                found = true;
            }

            if (found)
            {
                _applied = true;
            }
        }

        public void Reapply()
        {
            _applied = false;
            Apply();
        }

        private void ResetColor()
        {
            _hasColor = false;
            _applied = false;
            _deadline = Time.unscaledTime + ResolveTimeout;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        private void LateUpdate()
        {
            if (_applied)
            {
                return;
            }

            if (!_hasColor)
            {
                bool fromGroup = false;

                if (ColorSettings.GroupColorsEnabled)
                {
                    if (_view != null && GroupColors.TryGet(_view, out _color))
                    {
                        _hasColor = true;
                        fromGroup = true;
                    }
                    else if (Time.unscaledTime < _deadline)
                    {
                        return;
                    }
                }

                if (!_hasColor)
                {
                    _color = GroupColors.RandomColor();
                    _hasColor = true;
                }

                if (_logged < 5)
                {
                    _logged++;
                    Debug.Log($"[RandomCustomerColors] colored customer #{_logged} ({(fromGroup ? "group" : "random")})");
                }
            }

            Apply();
        }

        private void ApplyTo(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var material = renderer.material;
            if (material != null && material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, _color);
            }
        }
    }
}
