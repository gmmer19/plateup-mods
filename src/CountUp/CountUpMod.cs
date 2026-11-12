using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenMods;
using MessagePack;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.CountUp
{
    public sealed class CountUpMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.countup";

        public void PostActivate(Mod mod)
        {
            var harmony = new Harmony(ModGuid);
            harmony.PatchAll(typeof(CountUpMod).Assembly);
            Debug.Log("[CountUp] loaded (portion counts above food)");
        }

        public void PreInject()
        {
            CountUpSettings.EnsureRegistered();
            Debug.Log("[CountUp] preferences registered");
        }

        public void PostInject()
        {
            try
            {
                int count = 0;
                if (GameData.Main != null)
                {
                    foreach (var item in GameData.Main.Get<Item>())
                    {
                        if (item.IsSplittable)
                        {
                            count++;
                        }
                    }
                }
                Debug.Log($"[CountUp] indexed {count} splittable food items");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CountUp] PostInject item scan failed: " + e.Message);
            }
        }
    }

    internal static class CountUpSettings
    {
        public static readonly Pref ShowCounts = new Pref(CountUpMod.ModGuid, "ShowCounts");

        private static bool? _lastApplied;

        public static void EnsureRegistered()
        {
            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CountUp] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CountUp] Preferences.Load failed: " + e.Message);
            }
        }

        public static void RegisterIfMissing()
        {
            if (Preferences.TryGet<bool>(ShowCounts, out _))
            {
                return;
            }

            try
            {
                Preferences.AddPreference(new BoolPreference(
                    ShowCounts,
                    true,
                    OnChanged));
            }
            catch (ArgumentException)
            {
                // Already registered by another call.
            }
        }

        private static void OnChanged(bool value)
        {
            if (_lastApplied.HasValue && _lastApplied.Value == value)
            {
                return;
            }

            _lastApplied = value;
            CountUpItemView.RefreshAll();
        }

        public static bool Enabled
        {
            get
            {
                if (Preferences.TryGet<bool>(ShowCounts, out var value))
                {
                    return value;
                }

                EnsureRegistered();

                if (Preferences.TryGet<bool>(ShowCounts, out value))
                {
                    return value;
                }

                return true;
            }
        }
    }

    [Serializable]
    [MessagePackObject(false)]
    public struct CountUpViewData : ISpecificViewData, IViewData.ICheckForChanges<CountUpViewData>
    {
        [Key(0)]
        public int Remaining;

        [Key(1)]
        public int Total;

        [Key(2)]
        public int ItemID;

        public IUpdatableObject GetRelevantSubview(IObjectView view)
        {
            if (view is ItemView itemView)
            {
                var comp = itemView.GetComponent<CountUpItemView>();
                if (comp == null && itemView.gameObject != null)
                {
                    comp = itemView.gameObject.AddComponent<CountUpItemView>();
                }
                return comp;
            }
            return null;
        }

        public bool IsChangedFrom(CountUpViewData check)
        {
            return Remaining != check.Remaining || Total != check.Total || ItemID != check.ItemID;
        }
    }

    public class CountUpViewSystem : IncrementalViewSystemBase<CountUpViewData>, IModSystem
    {
        private EntityQuery _query;

        protected override void Initialise()
        {
            base.Initialise();
            _query = GetEntityQuery(new QueryHelper()
                .All(typeof(CLinkedView), typeof(CItem), typeof(CSplittableItem)));
        }

        protected override void OnUpdate()
        {
            var views = _query.ToComponentDataArray<CLinkedView>(Allocator.Temp);
            var splittables = _query.ToComponentDataArray<CSplittableItem>(Allocator.Temp);
            var items = _query.ToComponentDataArray<CItem>(Allocator.Temp);

            try
            {
                for (int i = 0; i < views.Length; i++)
                {
                    SendUpdate(views[i], new CountUpViewData
                    {
                        Remaining = splittables[i].RemainingCount,
                        Total = splittables[i].TotalCount,
                        ItemID = items[i].ID
                    }, MessageType.SpecificViewUpdate);
                }
            }
            finally
            {
                views.Dispose();
                splittables.Dispose();
                items.Dispose();
            }
        }
    }

    public class CountUpItemView : UpdatableObjectView<CountUpViewData>
    {
        private static readonly FieldInfo ProcessIconsField = typeof(ItemView).GetField(
            "ProcessIcons", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly List<CountUpItemView> ActiveViews = new List<CountUpItemView>();

        private ItemView _itemView;
        private TextMeshPro _processIcons;
        private TextMeshPro _fallbackText;
        private int _remaining;
        private int _total;
        private int _itemId;
        private bool _hasData;

        private void Awake()
        {
            if (!ActiveViews.Contains(this))
            {
                ActiveViews.Add(this);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ActiveViews.Remove(this);
        }

        protected override void UpdateData(CountUpViewData data)
        {
            _remaining = data.Remaining;
            _total = data.Total;
            _itemId = data.ItemID;
            _hasData = true;
            Apply();
        }

        public void OnItemViewUpdated()
        {
            Apply();
        }

        public void Apply()
        {
            if (_itemView == null)
            {
                _itemView = GetComponent<ItemView>();
            }

            if (_itemView == null || !_itemView.gameObject.activeInHierarchy)
            {
                return;
            }

            if (_processIcons == null && ProcessIconsField != null)
            {
                _processIcons = (TextMeshPro)ProcessIconsField.GetValue(_itemView);
            }

            const string splitIcon = "<sprite name=\"split\">";

            if (_processIcons != null)
            {
                _processIcons.overflowMode = TextOverflowModes.Overflow;
                _processIcons.enableWordWrapping = false;

                string current = _processIcons.text ?? "";

                if (!CountUpSettings.Enabled)
                {
                    if (current.Contains(splitIcon))
                    {
                        _processIcons.text = Regex.Replace(current, @"<sprite name=""split"">( \d+)?", splitIcon);
                    }
                    return;
                }

                bool shouldShow = _hasData && _remaining > 0 && (_total > 1 || _remaining > 1) && _remaining < 100;

                if (!shouldShow)
                {
                    if (current.Contains(splitIcon))
                    {
                        _processIcons.text = Regex.Replace(current, @"<sprite name=""split"">( \d+)?", splitIcon);
                    }
                    return;
                }

                string target = $"{splitIcon} {_remaining}";
                if (current.Contains(splitIcon))
                {
                    _processIcons.text = Regex.Replace(current, @"<sprite name=""split"">( \d+)?", target);
                }
                else if (string.IsNullOrEmpty(current))
                {
                    _processIcons.text = target;
                }
                return;
            }

            // Fallback for custom views without ProcessIcons
            ApplyFallback();
        }

        private void ApplyFallback()
        {
            bool shouldShow = CountUpSettings.Enabled
                && _hasData
                && _remaining > 0
                && (_total > 1 || _remaining > 1)
                && _remaining < 100;

            if (!shouldShow)
            {
                if (_fallbackText != null)
                {
                    _fallbackText.gameObject.SetActive(false);
                }
                return;
            }

            if (_fallbackText == null)
            {
                var holder = new GameObject("CountUpFallback");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                holder.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
                holder.transform.localScale = Vector3.one;

                _fallbackText = holder.AddComponent<TextMeshPro>();
                _fallbackText.font = WorldText.DefaultFont;
                _fallbackText.fontSize = 3f;
                _fallbackText.alignment = TextAlignmentOptions.Center;
                _fallbackText.enableWordWrapping = false;
                _fallbackText.overflowMode = TextOverflowModes.Overflow;
                _fallbackText.fontStyle = FontStyles.Bold;
                _fallbackText.color = Color.white;
                _fallbackText.outlineColor = new Color(0f, 0f, 0f, 0.9f);
                _fallbackText.outlineWidth = 0.3f;
            }

            _fallbackText.gameObject.SetActive(true);
            _fallbackText.text = $"<sprite name=\"split\"> {_remaining}";
        }

        public static void RefreshAll()
        {
            for (int i = ActiveViews.Count - 1; i >= 0; i--)
            {
                var view = ActiveViews[i];
                if (view == null)
                {
                    ActiveViews.RemoveAt(i);
                    continue;
                }
                view.Apply();
            }
        }
    }

    [HarmonyPatch(typeof(ItemView), "Initialise")]
    internal static class ItemViewInitialisePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemView __instance)
        {
            try
            {
                if (__instance != null && __instance.gameObject != null && __instance.GetComponent<CountUpItemView>() == null)
                {
                    __instance.gameObject.AddComponent<CountUpItemView>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CountUp] ItemViewInitialisePatch error: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ItemView), "UpdateData", new Type[] { typeof(ItemView.ViewData) })]
    internal static class ItemViewUpdateDataPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemView __instance, ItemView.ViewData view_data)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                var countView = __instance.GetComponent<CountUpItemView>();
                if (countView != null)
                {
                    countView.OnItemViewUpdated();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CountUp] ItemViewUpdateDataPatch error: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(Preferences), "SetUpPreferences")]
    internal static class PreferencesSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            CountUpSettings.RegisterIfMissing();
        }
    }

    internal static class WorldText
    {
        private static TMP_FontAsset _font;

        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = TMP_Settings.defaultFontAsset;
                    if (_font == null)
                    {
                        _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                    }
                }

                return _font;
            }
        }
    }
}
