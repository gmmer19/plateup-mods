using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenData;
using KitchenMods;
using MessagePack;
using Shapes;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.PatienceIndicator
{
    public sealed class PatienceIndicatorMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.patienceindicator";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(PatienceIndicatorMod).Assembly);
            Debug.Log("[PatienceIndicator] loaded (patience timers + priority highlight)");
        }

        public void PreInject()
        {
            PatienceSettings.EnsureRegistered();
            Debug.Log("[PatienceIndicator] preferences registered");
        }

        public void PostInject() { }
    }

    internal static class PatienceSettings
    {
        private const string Ns = PatienceIndicatorMod.ModGuid;

        public static readonly Pref Layout = new Pref(Ns, "Layout");
        public static readonly Pref DisplayTimer = new Pref(Ns, "DisplayTimer");
        public static readonly Pref IndicatorColor = new Pref(Ns, "IndicatorColor");
        public static readonly Pref PriorityHighlight = new Pref(Ns, "PriorityHighlight");
        public static readonly Pref PriorityColor = new Pref(Ns, "PriorityColor");
        public static readonly Pref FontSize = new Pref(Ns, "FontSize");

        public static readonly Color[] Palette =
        {
            new Color(0.35f, 1f, 0.18f), // green
            new Color(0.15f, 0.95f, 1f), // cyan
            new Color(0.25f, 0.55f, 1f), // blue
            new Color(1f, 0.85f, 0.15f), // yellow
            new Color(1f, 0.55f, 0.10f), // orange
            new Color(1f, 0.25f, 0.20f), // red
            new Color(1f, 0.40f, 0.75f), // pink
            new Color(0.70f, 0.40f, 1f), // purple
            new Color(1f, 1f, 1f)        // white
        };

        public static readonly string[] ColorNamesEn =
        {
            "Green", "Cyan", "Blue", "Yellow", "Orange", "Red", "Pink", "Purple", "White"
        };

        public static readonly string[] ColorNamesRu =
        {
            "Зелёный", "Голубой", "Синий", "Жёлтый", "Оранжевый", "Красный", "Розовый", "Фиолетовый", "Белый"
        };

        public static readonly float[] FontScales = { 0.8f, 1f, 1.2f, 1.45f };

        public static readonly string[] FontNamesEn = { "Small", "Medium", "Large", "Extra Large" };
        public static readonly string[] FontNamesRu = { "Мелкий", "Средний", "Крупный", "Очень крупный" };

        public static readonly string[] LayoutNamesEn = { "Icon / Timer", "Timer / Icon", "Icon Only", "Timer Only" };
        public static readonly string[] LayoutNamesRu = { "Иконка / Таймер", "Таймер / Иконка", "Только иконка", "Только таймер" };

        public static void EnsureRegistered()
        {
            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PatienceIndicator] Preferences.Load failed: " + e.Message);
            }

            RegisterIfMissing();

            try
            {
                Preferences.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PatienceIndicator] Preferences.Load failed: " + e.Message);
            }
        }

        public static void RegisterIfMissing()
        {
            AddBool(DisplayTimer, true);
            AddBool(PriorityHighlight, true);
            AddInt(Layout, 0);
            AddInt(IndicatorColor, 0);
            AddInt(PriorityColor, 1);
            AddInt(FontSize, 1);
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

        public static int GetInt(Pref pref, int fallback)
        {
            try
            {
                if (Preferences.TryGet<int>(pref, out var value))
                {
                    return value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PatienceIndicator] pref '{pref}' is not int: {e.GetType().Name}");
            }

            EnsureRegistered();

            try
            {
                return Preferences.TryGet<int>(pref, out var value) ? value : fallback;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PatienceIndicator] pref '{pref}' is still not int: {e.GetType().Name}");
                return fallback;
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
            catch (Exception e)
            {
                Debug.LogWarning($"[PatienceIndicator] pref '{pref}' is not bool: {e.GetType().Name}");
            }

            EnsureRegistered();

            try
            {
                return Preferences.TryGet<bool>(pref, out var value) ? value : fallback;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PatienceIndicator] pref '{pref}' is still not bool: {e.GetType().Name}");
                return fallback;
            }
        }

        // Every indicator reads these several times per frame, so the values are
        // taken from Preferences at most once per frame (Invalidate() forces a
        // re-read after a setting was changed in the menu).
        private static int _cacheFrame = -1;
        private static int _layoutIndex;
        private static bool _showTimer;
        private static int _indicatorColorIndex;
        private static bool _highlightPriority;
        private static int _priorityColorIndex;
        private static int _fontSizeIndex;

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
            _layoutIndex = Mathf.Clamp(GetInt(Layout, 0), 0, 3);
            _showTimer = GetBool(DisplayTimer, true);
            _indicatorColorIndex = Mathf.Clamp(GetInt(IndicatorColor, 0), 0, Palette.Length - 1);
            _highlightPriority = GetBool(PriorityHighlight, true);
            _priorityColorIndex = Mathf.Clamp(GetInt(PriorityColor, 1), 0, Palette.Length - 1);
            _fontSizeIndex = Mathf.Clamp(GetInt(FontSize, 1), 0, FontScales.Length - 1);
        }

        public static int LayoutIndex { get { Refresh(); return _layoutIndex; } }
        public static bool ShowTimer { get { Refresh(); return _showTimer; } }
        public static int IndicatorColorIndex { get { Refresh(); return _indicatorColorIndex; } }
        public static bool HighlightPriority { get { Refresh(); return _highlightPriority; } }
        public static int PriorityColorIndex { get { Refresh(); return _priorityColorIndex; } }
        public static int FontSizeIndex { get { Refresh(); return _fontSizeIndex; } }
        public static float FontScale => FontScales[FontSizeIndex];
        public static Color Indicator => Palette[IndicatorColorIndex];
        public static Color Priority => Palette[PriorityColorIndex];
    }

    public sealed class PatienceSettingsMenu : Menu<MenuAction>
    {
        public PatienceSettingsMenu(Transform container, ModuleList moduleList)
            : base(container, moduleList)
        {
        }

        public override void Setup(int playerId)
        {
            Step("prefs", PatienceSettings.EnsureRegistered);
            LabelElement titleLabel = null;
            Step("heading", () => titleLabel = AddLabel(
                L("Patience Indicator", "Индикатор терпения")));

            LabelElement layoutLabel = null;
            Step("layout label", () => layoutLabel = AddLabel(L("Layout", "Макет")));
            Step("title font", () => ApplyTitleFont(titleLabel, layoutLabel));
            Step("layout select", () => AddSelect(Names(PatienceSettings.LayoutNamesEn, PatienceSettings.LayoutNamesRu),
                index =>
                {
                    Preferences.Set(PatienceSettings.Layout, index);
                    PatienceIndicatorView.RefreshAll();
                },
                PatienceSettings.LayoutIndex));

            Step("timer label", () => AddLabel(L("Display Timer", "Показывать таймер")));
            Step("timer select", () => AddBoolSelect(PatienceSettings.DisplayTimer));

            Step("indicator color label", () => AddLabel(L("Indicator Color", "Цвет индикатора")));
            Step("indicator color select", () => AddSelect(ColorNames(),
                index =>
                {
                    Preferences.Set(PatienceSettings.IndicatorColor, index);
                    PatienceIndicatorView.RefreshAll();
                },
                PatienceSettings.IndicatorColorIndex));

            Step("priority label", () => AddLabel(L("Priority Highlight", "Подсветка приоритета")));
            Step("priority select", () => AddBoolSelect(PatienceSettings.PriorityHighlight));

            Step("priority color label", () => AddLabel(L("Priority Color", "Цвет приоритета")));
            Step("priority color select", () => AddSelect(ColorNames(),
                index =>
                {
                    Preferences.Set(PatienceSettings.PriorityColor, index);
                    PatienceIndicatorView.RefreshAll();
                },
                PatienceSettings.PriorityColorIndex));

            Step("font label", () => AddLabel(L("Font Size", "Размер шрифта")));
            Step("font select", () => AddSelect(Names(PatienceSettings.FontNamesEn, PatienceSettings.FontNamesRu),
                index =>
                {
                    Preferences.Set(PatienceSettings.FontSize, index);
                    PatienceIndicatorView.RefreshAll();
                },
                PatienceSettings.FontSizeIndex));

            Step("spacer", () =>
            {
                New<SpacerElement>();
                New<SpacerElement>();
            });

            Step("back", () => AddButton(Localisation["MENU_BACK_SETTINGS"], _ => RequestPreviousMenu()));

            Debug.Log("[PatienceIndicator] settings menu setup done");
        }

        private static readonly FieldInfo LabelField = typeof(LabelElement).GetField(
            "Label", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo CurrentFontSizeField = typeof(TMP_Text).GetField(
            "m_currentFontSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static void ApplyTitleFont(LabelElement title, LabelElement reference)
        {
            if (title == null || reference == null)
            {
                return;
            }

            var titleText = LabelField?.GetValue(title) as TextMeshPro
                            ?? title.GetComponentInChildren<TextMeshPro>();
            var referenceText = LabelField?.GetValue(reference) as TextMeshPro
                                ?? reference.GetComponentInChildren<TextMeshPro>();
            if (titleText == null || referenceText == null)
            {
                return;
            }

            // Auto-sizing keeps the computed size in m_currentFontSize and leaves
            // TMP_Text.fontSize at the base value. Force a layout and copy the
            // actual rendered size of a normal label.
            referenceText.ForceMeshUpdate();
            float size = referenceText.fontSize;
            if (CurrentFontSizeField != null)
            {
                float current = (float)CurrentFontSizeField.GetValue(referenceText);
                if (current > 0f)
                {
                    size = current;
                }
            }

            titleText.enableAutoSizing = false;
            titleText.enableWordWrapping = false;
            titleText.overflowMode = TextOverflowModes.Overflow;
            titleText.fontSize = size;
            titleText.ForceMeshUpdate();

            Debug.Log($"[PatienceIndicator] title font {titleText.fontSize} " +
                      $"(label base {referenceText.fontSize}, rendered {size})");
        }

        private void AddBoolSelect(Pref pref)
        {
            var names = new List<string>
            {
                L("Disabled", "Выключено"),
                L("Enabled", "Включено")
            };

            AddSelect(names,
                index =>
                {
                    Preferences.Set(pref, index != 0);
                    PatienceIndicatorView.RefreshAll();
                },
                PatienceSettings.GetBool(pref, true) ? 1 : 0);
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PatienceIndicator] menu step '{name}' failed: {e}");
            }
        }

        private static string L(string english, string russian)
        {
            return KitchenData.Localisation.CurrentLocale == Locale.Russian ? russian : english;
        }

        private static List<string> Names(string[] english, string[] russian)
        {
            return new List<string>(KitchenData.Localisation.CurrentLocale == Locale.Russian ? russian : english);
        }

        // Each colour name is drawn in its own colour (TextMeshPro rich text), so
        // the selector previews the colour it will apply.
        private static List<string> ColorNames()
        {
            var names = Names(PatienceSettings.ColorNamesEn, PatienceSettings.ColorNamesRu);
            for (int i = 0; i < names.Count && i < PatienceSettings.Palette.Length; i++)
            {
                names[i] = $"<color=#{ColorUtility.ToHtmlStringRGB(PatienceSettings.Palette[i])}>{names[i]}</color>";
            }

            return names;
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
                if (!menus.ContainsKey(typeof(PatienceSettingsMenu)))
                {
                    menus.Add(typeof(PatienceSettingsMenu),
                        new PatienceSettingsMenu(__instance.Container, __instance.ModuleList));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PatienceIndicator] CreateSubmenus patch failed: " + e);
            }
        }
    }

    [Serializable]
    [MessagePackObject(false)]
    public struct PatienceSecondsViewData : ISpecificViewData, IViewData.ICheckForChanges<PatienceSecondsViewData>
    {
        [Key(0)]
        public float Remaining;

        [Key(1)]
        public float Total;

        [Key(2)]
        public bool Active;

        [Key(3)]
        public int Reason;

        public IUpdatableObject GetRelevantSubview(IObjectView view)
        {
            if (view is CustomerIndicatorView indicator)
            {
                var component = indicator.GetComponent<PatienceIndicatorView>();
                if (component == null)
                {
                    component = indicator.gameObject.AddComponent<PatienceIndicatorView>();
                }

                return component;
            }

            return null;
        }

        public bool IsChangedFrom(PatienceSecondsViewData check)
        {
            return Remaining != check.Remaining
                   || Total != check.Total
                   || Active != check.Active
                   || Reason != check.Reason;
        }
    }

    public class PatienceSecondsSystem : IncrementalViewSystemBase<PatienceSecondsViewData>, IModSystem
    {
        private EntityQuery _customers;
        private EntityQuery _queue;

        protected override void Initialise()
        {
            base.Initialise();
            _customers = GetEntityQuery(new QueryHelper()
                .All(typeof(CHasIndicator), typeof(CPatience), typeof(CCustomerSettings)));
            _queue = GetEntityQuery(new QueryHelper()
                .All(typeof(SQueueMarker), typeof(CHasIndicator), typeof(CPatience)));
        }

        protected override void OnUpdate()
        {
            var customers = _customers.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < customers.Length; i++)
                {
                    var entity = customers[i];
                    var indicator = EntityManager.GetComponentData<CHasIndicator>(entity);
                    if (indicator.IndicatorType != ViewType.CustomerIndicator)
                    {
                        continue;
                    }

                    if (!EntityManager.HasComponent<CLinkedView>(indicator.Indicator))
                    {
                        continue;
                    }

                    var patience = EntityManager.GetComponentData<CPatience>(entity);
                    var settings = EntityManager.GetComponentData<CCustomerSettings>(entity);
                    float unit = settings.Patience[patience.Reason];
                    if (unit <= 0f)
                    {
                        continue;
                    }

                    SendUpdate(EntityManager.GetComponentData<CLinkedView>(indicator.Indicator),
                        new PatienceSecondsViewData
                        {
                            Remaining = Mathf.Max(0f, patience.RemainingTime * unit),
                            Total = Mathf.Max(0.01f, patience.StartTime * unit),
                            Active = patience.Active,
                            Reason = (int)patience.Reason
                        }, MessageType.SpecificViewUpdate);
                }
            }
            finally
            {
                customers.Dispose();
            }

            var queue = _queue.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < queue.Length; i++)
                {
                    var entity = queue[i];
                    var indicator = EntityManager.GetComponentData<CHasIndicator>(entity);
                    if (indicator.IndicatorType != ViewType.QueueIndicator)
                    {
                        continue;
                    }

                    if (!EntityManager.HasComponent<CLinkedView>(indicator.Indicator))
                    {
                        continue;
                    }

                    var patience = EntityManager.GetComponentData<CPatience>(entity);
                    float unit = GameData.Main.Difficulty.QueuePatienceTime;
                    if (unit <= 0f)
                    {
                        continue;
                    }

                    SendUpdate(EntityManager.GetComponentData<CLinkedView>(indicator.Indicator),
                        new PatienceSecondsViewData
                        {
                            Remaining = Mathf.Max(0f, patience.RemainingTime * unit),
                            Total = Mathf.Max(0.01f, patience.StartTime * unit),
                            Active = patience.Active,
                            Reason = (int)PatienceReason.Queue
                        }, MessageType.SpecificViewUpdate);
                }
            }
            finally
            {
                queue.Dispose();
            }
        }
    }

    public class PatienceIndicatorView : UpdatableObjectView<PatienceSecondsViewData>
    {
        private static readonly FieldInfo PatienceField = typeof(CustomerIndicatorView).GetField(
            "Patience", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo IconField = typeof(CustomerIndicatorView).GetField(
            "Icon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo PatienceContainerField = typeof(CustomerIndicatorView).GetField(
            "PatienceContainer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly List<PatienceIndicatorView> ActiveViews = new List<PatienceIndicatorView>();

        private CustomerIndicatorView _indicator;
        private Rectangle _bar;
        private TextMeshPro _icon;
        private GameObject _patienceContainer;
        private TextMeshPro _label;
        private GameObject _labelHolder;
        private float _labelScale = 0.05f;
        private float _baseLabelScale = 0.05f;

        private bool _hasData;
        private bool _hasPatience;
        private bool _isHidden;
        private bool _isDecreasing;
        private PatienceReason _reason;
        private float _fraction;

        private bool _hasExact;
        private float _exactRemaining;
        private float _exactTime;

        private float _lastFraction;
        private float _lastFractionTime;
        private float _rate;
        private float _estimate;

        private string _shownText;
        private int _lastPriorityFrame = -1;
        private bool _hasBarExtent;
        private float _barTop;
        private float _barBottom;
        private Vector3 _iconOriginalPosition;
        private bool _hasIconOriginalPosition;
        private bool _iconHidden;

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

        protected override void UpdateData(PatienceSecondsViewData data)
        {
            _hasExact = data.Active;
            _exactRemaining = data.Remaining;
            _exactTime = Time.time;
            _reason = (PatienceReason)data.Reason;
            Apply();
        }

        public void OnIndicatorData(CustomerIndicatorView.ViewData data)
        {
            bool reasonChanged = _hasData && _reason != data.PatienceReason;
            bool increased = _hasData && data.Patience > _lastFraction + 0.0001f;
            float now = Time.time;

            if (_hasData && now > _lastFractionTime && !reasonChanged && !increased)
            {
                float dt = now - _lastFractionTime;
                float delta = _lastFraction - data.Patience;
                if (dt > 0.0001f && delta > 0f && data.IsActivelyDecreasing)
                {
                    float instant = delta / dt;
                    if (instant > 0.000001f && instant < 10f)
                    {
                        _rate = _rate <= 0f ? instant : Mathf.Lerp(_rate, instant, 0.2f);
                    }
                }
            }

            if (reasonChanged || increased)
            {
                _rate = 0f;
                _estimate = -1f;
                _hasExact = false;
            }

            _hasData = true;
            _hasPatience = data.HasPatience;
            _isHidden = data.IsHidden;
            _isDecreasing = data.IsActivelyDecreasing;
            _reason = data.PatienceReason;
            _fraction = data.Patience;
            _lastFraction = data.Patience;
            _lastFractionTime = now;

            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        public void Apply()
        {
            if (_indicator == null)
            {
                _indicator = GetComponent<CustomerIndicatorView>();
            }

            if (_indicator == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            ResolveFields();
            TrackBarExtent();
            UpdateTimer();
            UpdateLayout();

            if (_lastPriorityFrame != Time.frameCount)
            {
                _lastPriorityFrame = Time.frameCount;
                RefreshPriorities();
            }
        }

        private void ResolveFields()
        {
            if (_bar == null && PatienceField != null)
            {
                _bar = PatienceField.GetValue(_indicator) as Rectangle;
            }

            if (_icon == null && IconField != null)
            {
                _icon = IconField.GetValue(_indicator) as TextMeshPro;
            }

            if (_patienceContainer == null && PatienceContainerField != null)
            {
                _patienceContainer = PatienceContainerField.GetValue(_indicator) as GameObject;
            }

            if (_icon != null && !_hasIconOriginalPosition)
            {
                _iconOriginalPosition = _icon.transform.localPosition;
                _hasIconOriginalPosition = true;
            }
        }

        // The fill bar shrinks while patience runs out, so its current top/bottom
        // edges move. The timer is anchored to the extent of the FULL bar instead:
        // the union of every edge position seen while the bar was valid.
        private void TrackBarExtent()
        {
            if (_bar == null || !_hasData || !_hasPatience || _isHidden || _bar.Height <= 0.001f)
            {
                return;
            }

            float centerY = _bar.transform.localPosition.y;
            float half = _bar.Height * 0.5f;
            float top = centerY + half;
            float bottom = centerY - half;

            if (!_hasBarExtent)
            {
                _hasBarExtent = true;
                _barTop = top;
                _barBottom = bottom;
                return;
            }

            _barTop = Mathf.Max(_barTop, top);
            _barBottom = Mathf.Min(_barBottom, bottom);
        }

        private float RemainingSeconds()
        {
            if (_hasExact && Time.time - _exactTime < 0.6f)
            {
                return _exactRemaining;
            }

            if (_rate <= 0.000001f)
            {
                return -1f;
            }

            float estimate = _fraction / _rate;
            if (_estimate < 0f)
            {
                _estimate = estimate;
            }
            else
            {
                _estimate = Mathf.Lerp(_estimate, estimate, 0.25f);
            }

            return _estimate;
        }

        private void UpdateTimer()
        {
            float remaining = RemainingSeconds();
            bool visible = PatienceSettings.ShowTimer
                           && PatienceSettings.LayoutIndex != (int)IndicatorLayout.IconOnly
                           && _hasData
                           && _hasPatience
                           && !_isHidden
                           && remaining >= 0f;

            if (!visible)
            {
                if (_labelHolder != null)
                {
                    _labelHolder.SetActive(false);
                }

                return;
            }

            EnsureLabel();
            if (_label == null)
            {
                return;
            }

            _labelHolder.SetActive(true);

            string text = FormatTime(remaining);
            if (text != _shownText)
            {
                _shownText = text;
                _label.text = text;
            }

            if (_bar != null && _hasBarExtent)
            {
                float y = PatienceSettings.LayoutIndex == (int)IndicatorLayout.TimerIcon
                    ? _barTop - 0.02f
                    : _barBottom + 0.02f;

                _labelHolder.transform.localPosition = new Vector3(
                    _bar.transform.localPosition.x, y, _bar.transform.localPosition.z - 0.32f);
            }
        }

        private void EnsureLabel()
        {
            if (_label != null)
            {
                return;
            }

            var parent = _patienceContainer != null ? _patienceContainer.transform : transform;
            _labelHolder = new GameObject("PatienceTimer");
            _labelHolder.transform.SetParent(parent, false);
            _labelHolder.transform.localRotation = Quaternion.identity;

            _label = _labelHolder.AddComponent<TextMeshPro>();
            _label.font = WorldText.DefaultFont;
            _label.fontSize = 40f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.fontStyle = FontStyles.Bold;
            _label.color = Color.white;
            _label.outlineColor = new Color(0.03f, 0.03f, 0.03f, 1f);
            _label.outlineWidth = 0.22f;
            _label.rectTransform.sizeDelta = new Vector2(2f, 1f);

            // Shapes draws the bar in the transparent queue; give the timer a high
            // sorting order so it always renders over the bar/backing meshes.
            var labelRenderer = _label.renderer;
            if (labelRenderer != null)
            {
                var iconRenderer = _icon != null ? _icon.renderer : null;
                if (iconRenderer != null)
                {
                    labelRenderer.sortingLayerID = iconRenderer.sortingLayerID;
                    labelRenderer.sortingOrder = iconRenderer.sortingOrder + 100;
                }
                else
                {
                    labelRenderer.sortingOrder = 1000;
                }
            }

            float scale = 0.05f;
            if (_icon != null && _icon.transform.localScale.x > 0f && _icon.fontSize > 0f)
            {
                // The icon TMP is authored for this prefab: reuse its world scale,
                // but keep the timer a little smaller than the reason icon.
                scale = _icon.transform.localScale.x * Mathf.Clamp(_icon.fontSize / 70f, 0.5f, 1f);
                _label.fontSize = _icon.fontSize;
            }

            _baseLabelScale = scale;
            _labelScale = scale * PatienceSettings.FontScale;
            _labelHolder.transform.localScale = Vector3.one * _labelScale;
        }

        private void UpdateLayout()
        {
            int layout = PatienceSettings.LayoutIndex;

            if (_icon != null)
            {
                bool hideIcon = layout == (int)IndicatorLayout.TimerOnly;
                if (hideIcon != _iconHidden)
                {
                    _iconHidden = hideIcon;
                    _icon.gameObject.SetActive(!hideIcon);
                }

                if (_hasIconOriginalPosition)
                {
                    _icon.transform.localPosition = _iconOriginalPosition;
                }
            }

            if (_label != null)
            {
                _label.gameObject.SetActive(layout != (int)IndicatorLayout.IconOnly && PatienceSettings.ShowTimer);
                _labelScale = _baseLabelScale * PatienceSettings.FontScale;
                _labelHolder.transform.localScale = Vector3.one * _labelScale;
            }
        }

        private void RefreshPriorities()
        {
            PatienceIndicatorView lowest = null;
            float lowestRemaining = float.MaxValue;

            for (int i = ActiveViews.Count - 1; i >= 0; i--)
            {
                var view = ActiveViews[i];
                if (view == null)
                {
                    ActiveViews.RemoveAt(i);
                    continue;
                }

                if (!view.IsVisibleForPriority())
                {
                    continue;
                }

                float remaining = view.RemainingSeconds();
                if (remaining < 0f)
                {
                    continue;
                }

                if (remaining < lowestRemaining)
                {
                    lowestRemaining = remaining;
                    lowest = view;
                }
            }

            for (int i = 0; i < ActiveViews.Count; i++)
            {
                var view = ActiveViews[i];
                if (view == null)
                {
                    continue;
                }

                Color color = PatienceSettings.Indicator;
                if (PatienceSettings.HighlightPriority && view == lowest)
                {
                    color = PatienceSettings.Priority;
                }

                view.SetBarColor(color);
            }
        }

        private bool IsVisibleForPriority()
        {
            return _hasData && _hasPatience && !_isHidden && gameObject.activeInHierarchy;
        }

        private void SetBarColor(Color color)
        {
            if (_bar == null)
            {
                return;
            }

            // Compare with the colour the bar really has: the game may repaint it
            // on its own, and a remembered value would then hide the mismatch.
            if (_bar.Color == color)
            {
                return;
            }

            _bar.Color = color;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            int hours = total / 3600;
            int minutes = total % 3600 / 60;
            int secs = total % 60;

            if (hours > 0)
            {
                return $"{hours}h {minutes}m";
            }

            if (minutes > 0)
            {
                return $"{minutes}m {secs}s";
            }

            return $"{secs}s";
        }

        public static void RefreshAll()
        {
            PatienceSettings.Invalidate();
            for (int i = 0; i < ActiveViews.Count; i++)
            {
                ActiveViews[i]?.Apply();
            }
        }
    }

    internal enum IndicatorLayout
    {
        IconTimer = 0,
        TimerIcon = 1,
        IconOnly = 2,
        TimerOnly = 3
    }

    [HarmonyPatch(typeof(CustomerIndicatorView), "Initialise")]
    internal static class CustomerIndicatorInitialisePatch
    {
        [HarmonyPostfix]
        private static void Postfix(CustomerIndicatorView __instance)
        {
            try
            {
                if (__instance != null && __instance.gameObject != null
                    && __instance.GetComponent<PatienceIndicatorView>() == null)
                {
                    __instance.gameObject.AddComponent<PatienceIndicatorView>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PatienceIndicator] Initialise patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(CustomerIndicatorView), "UpdateData",
        new Type[] { typeof(CustomerIndicatorView.ViewData) })]
    internal static class CustomerIndicatorUpdateDataPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CustomerIndicatorView __instance, CustomerIndicatorView.ViewData view_data)
        {
            try
            {
                __instance?.GetComponent<PatienceIndicatorView>()?.OnIndicatorData(view_data);
            }
            catch (Exception e)
            {
                Debug.LogError("[PatienceIndicator] UpdateData patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(Preferences), "SetUpPreferences")]
    internal static class PreferencesSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            PatienceSettings.RegisterIfMissing();
        }
    }

    internal static class WorldText
    {
        private static TMP_FontAsset _font;

        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (_font != null)
                {
                    return _font;
                }

                _font = FindFont("Reference - In-world Floating Text")
                        ?? FindFont("Reference - In-world Labels")
                        ?? FindFontWithFallback()
                        ?? TMP_Settings.defaultFontAsset;

                if (_font == null)
                {
                    _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                }

                return _font;
            }
        }

        private static TMP_FontAsset FindFont(string name)
        {
            var main = GameData.Main;
            if (main?.Fonts == null)
            {
                return null;
            }

            foreach (var lookup in main.Fonts)
            {
                if (lookup?.BaseFonts == null)
                {
                    continue;
                }

                foreach (var font in lookup.BaseFonts)
                {
                    if (font != null && font.name == name)
                    {
                        return font;
                    }
                }
            }

            return null;
        }

        private static TMP_FontAsset FindFontWithFallback()
        {
            var main = GameData.Main;
            if (main?.Fonts == null)
            {
                return null;
            }

            foreach (var lookup in main.Fonts)
            {
                if (lookup?.BaseFonts == null)
                {
                    continue;
                }

                foreach (var font in lookup.BaseFonts)
                {
                    if (font != null
                        && font.fallbackFontAssetTable != null
                        && font.fallbackFontAssetTable.Count > 0)
                    {
                        return font;
                    }
                }
            }

            return null;
        }
    }
}
