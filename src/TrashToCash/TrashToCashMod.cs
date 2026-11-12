using System;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using MessagePack;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.TrashToCash
{
    public sealed class TrashToCashMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.trashtocash";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(TrashToCashMod).Assembly);
            Debug.Log("[TrashToCash] loaded (50% refund for appliances left outside)");
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    // Stores the cheapest price ever seen for an appliance in the shop. The
    // marker entities deliberately have no view: they survive saves and do not
    // interfere with ApplianceInfoView.
    public class PriceUpdater : NightSystem, IModSystem
    {
        private EntityQuery _blueprints;
        private EntityQuery _markers;

        protected override void Initialise()
        {
            base.Initialise();
            _blueprints = GetEntityQuery(new QueryHelper().All(
                typeof(CApplianceBlueprint), typeof(CForSale), typeof(CLinkedView)));
            _markers = GetEntityQuery(new QueryHelper()
                .All(typeof(CApplianceInfo), typeof(CImmovable))
                .None(typeof(CLinkedView), typeof(CRequiresView)));
        }

        protected override void OnUpdate()
        {
            var entities = _blueprints.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var blueprint = EntityManager.GetComponentData<CApplianceBlueprint>(entities[i]);
                    var sale = EntityManager.GetComponentData<CForSale>(entities[i]);
                    TrackPrice(EntityManager, _markers, blueprint.Appliance, sale.Price);
                }
            }
            finally
            {
                entities.Dispose();
            }
        }

        // The queries are created once per system (not on every call): these
        // helpers are used every frame by the sell-price labels.
        public static int GetSellPrice(EntityQuery markers, int applianceId)
        {
            return (int)Math.Floor(GetKnownPrice(markers, applianceId) / 2d);
        }

        public static int GetKnownPrice(EntityQuery markers, int applianceId)
        {
            int buyPrice = 0;
            if (GameData.Main != null && GameData.Main.TryGet<Appliance>(applianceId, out var appliance))
            {
                buyPrice = appliance.PurchaseCost;
            }

            var tracked = markers.ToComponentDataArray<CApplianceInfo>(Allocator.Temp);
            try
            {
                for (int i = 0; i < tracked.Length; i++)
                {
                    if (tracked[i].ID == applianceId && tracked[i].Price > 0)
                    {
                        buyPrice = Math.Min(buyPrice == 0 ? tracked[i].Price : buyPrice, tracked[i].Price);
                    }
                }
            }
            finally
            {
                tracked.Dispose();
            }

            return buyPrice;
        }

        public static void TrackPrice(EntityManager entityManager, EntityQuery markers, int applianceId, int amount)
        {
            if (applianceId == 0 || amount <= 0)
            {
                return;
            }

            var entities = markers.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var tracked = entityManager.GetComponentData<CApplianceInfo>(entities[i]);
                    if (tracked.ID != applianceId)
                    {
                        continue;
                    }

                    if (amount < tracked.Price || tracked.Price <= 0)
                    {
                        entityManager.SetComponentData(entities[i], new CApplianceInfo
                        {
                            ID = applianceId,
                            Price = amount
                        });
                    }

                    return;
                }
            }
            finally
            {
                entities.Dispose();
            }

            var entity = entityManager.CreateEntity(typeof(CApplianceInfo), typeof(CImmovable));
            entityManager.SetComponentData(entity, new CApplianceInfo
            {
                ID = applianceId,
                Price = amount
            });
        }
    }

    // Runs before the vanilla DestroyAppliancesAtDay, so marked appliances are
    // sold instead of simply disappearing.
    //
    // Only appliances that the player can SEE as marked for deletion are sold:
    // the same filter as the "$" label (not HideBin, not held, occupies a tile).
    // The game also puts CDestroyApplianceAtDay on appliances it removes by
    // itself (HideBin = true); paying for those was the source of the
    // unexplained money at the start of the day. Such appliances are left
    // alone for the vanilla system to destroy.
    [UpdateBefore(typeof(DestroyAppliancesAtDay))]
    public class SellAtStartOfDay : StartOfDaySystem, IModSystem
    {
        private EntityQuery _appliances;
        private EntityQuery _everything;
        private EntityQuery _markers;

        protected override void Initialise()
        {
            base.Initialise();
            _everything = GetEntityQuery(new QueryHelper()
                .All(typeof(CDestroyApplianceAtDay), typeof(CAppliance)));
            _appliances = GetEntityQuery(new QueryHelper()
                .All(typeof(CDestroyApplianceAtDay), typeof(CAppliance))
                .None(typeof(CApplyDecor), typeof(CUnsellableAppliance),
                      typeof(CHeldAppliance), typeof(CDoesNotOccupy)));
            _markers = GetEntityQuery(new QueryHelper()
                .All(typeof(CApplianceInfo), typeof(CImmovable))
                .None(typeof(CLinkedView), typeof(CRequiresView)));
        }

        protected override void OnUpdate()
        {
            int totalSold = 0;

            // Diagnostics: everything the game marked for removal today, with the
            // flags that decide whether the mod sells it.
            var everything = _everything.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < everything.Length; i++)
                {
                    var e = everything[i];
                    var id = EntityManager.GetComponentData<CAppliance>(e).ID;
                    Debug.Log($"[TrashToCash] marked: {NameOf(id)} (id {id}) "
                        + $"hideBin={EntityManager.GetComponentData<CDestroyApplianceAtDay>(e).HideBin} "
                        + $"held={EntityManager.HasComponent<CHeldAppliance>(e)} "
                        + $"noOccupy={EntityManager.HasComponent<CDoesNotOccupy>(e)} "
                        + $"decor={EntityManager.HasComponent<CApplyDecor>(e)} "
                        + $"unsellable={EntityManager.HasComponent<CUnsellableAppliance>(e)}");
                }
            }
            finally
            {
                everything.Dispose();
            }

            var entities = _appliances.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];

                    if (EntityManager.GetComponentData<CDestroyApplianceAtDay>(entity).HideBin)
                    {
                        continue;
                    }

                    var appliance = EntityManager.GetComponentData<CAppliance>(entity);
                    int price = PriceUpdater.GetSellPrice(_markers, appliance.ID);
                    if (price <= 0)
                    {
                        continue;
                    }

                    totalSold += price;

                    Debug.Log($"[TrashToCash] sold appliance {NameOf(appliance.ID)} (id {appliance.ID}) for {price}");
                    EntityManager.DestroyEntity(entity);
                }
            }
            finally
            {
                entities.Dispose();
            }

            if (totalSold <= 0)
            {
                return;
            }

            SetSingleton<SMoney>(GetSingleton<SMoney>() + totalSold);

            int labelId = TrashLabel.Id;
            if (labelId != 0)
            {
                using (var context = new EntityContext(EntityManager))
                {
                    MoneyTracker.AddEvent(context, labelId, totalSold);
                }
            }

            Debug.Log($"[TrashToCash] refunded {totalSold} at start of day");
        }

        private static string NameOf(int applianceId)
        {
            if (GameData.Main != null && GameData.Main.TryGet<Appliance>(applianceId, out var appliance))
            {
                return appliance.name;
            }

            return "?";
        }
    }

    // A runtime MoneyTrackerText gives the end-of-day popup a proper localized
    // row without patching AddRow and without depending on the Bin's ID.
    internal static class TrashLabel
    {
        private static MoneyTrackerText _text;
        private static int _id;

        // 0 means "no label available": callers must not use it as an ID.
        public static int Id
        {
            get
            {
                Ensure();
                return _id;
            }
        }

        private static void Ensure()
        {
            try
            {
                var main = GameData.Main;
                if (main == null || main.Objects == null)
                {
                    return;
                }

                // GameData can be rebuilt (new save / back to the menu), so the
                // registration is re-checked instead of being done only once.
                if (_id != 0 && main.Objects.ContainsKey(_id))
                {
                    return;
                }

                if (_text == null)
                {
                    _text = ScriptableObject.CreateInstance<MoneyTrackerText>();
                    _text.name = "TrashToCash";
                    _text.Name = Localisation.CurrentLocale == Locale.Russian
                        ? "Мусор в деньги"
                        : "Trash to Cash";
                }

                int id = unchecked((int)0x54524348); // "TRCH"
                while (main.Objects.ContainsKey(id))
                {
                    id++;
                }

                _text.ID = id;
                main.Objects[id] = _text;
                _id = id;
            }
            catch (Exception e)
            {
                _id = 0;
                Debug.LogError("[TrashToCash] failed to create popup label: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ApplianceView), "Initialise")]
    internal static class ApplianceViewInitialisePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ApplianceView __instance)
        {
            try
            {
                if (__instance == null || __instance.gameObject == null)
                {
                    return;
                }

                if (__instance.GetComponent<ApplianceSellPriceView>() == null)
                {
                    __instance.gameObject.AddComponent<ApplianceSellPriceView>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[TrashToCash] appliance view patch failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ApplianceView), "UpdateData", new Type[] { typeof(ApplianceView.ViewData) })]
    internal static class ApplianceViewUpdateDataPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ApplianceView __instance, ApplianceView.ViewData view_data)
        {
            try
            {
                var label = __instance != null ? __instance.GetComponent<ApplianceSellPriceView>() : null;
                if (label != null)
                {
                    label.SetMarkedForDeletion(view_data.MarkedForDeletion);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[TrashToCash] appliance data patch failed: " + e);
            }
        }
    }

    public class ApplianceSellPriceView : UpdatableObjectView<ApplianceSellPriceView.ViewData>
    {
        public class UpdateView : IncrementalViewSystemBase<ViewData>, IModSystem
        {
            private EntityQuery _views;
            private EntityQuery _markers;

            protected override void Initialise()
            {
                base.Initialise();
                _views = GetEntityQuery(new QueryHelper()
                    .All(typeof(CLinkedView), typeof(CAppliance), typeof(CDestroyApplianceAtDay))
                    .None(typeof(CHeldAppliance), typeof(CDoesNotOccupy), typeof(CApplyDecor)));
                _markers = GetEntityQuery(new QueryHelper()
                    .All(typeof(CApplianceInfo), typeof(CImmovable))
                    .None(typeof(CLinkedView), typeof(CRequiresView)));
            }

            protected override void OnUpdate()
            {
                var entities = _views.ToEntityArray(Allocator.Temp);
                var views = _views.ToComponentDataArray<CLinkedView>(Allocator.Temp);
                var appliances = _views.ToComponentDataArray<CAppliance>(Allocator.Temp);
                var destroy = _views.ToComponentDataArray<CDestroyApplianceAtDay>(Allocator.Temp);

                try
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        if (destroy[i].HideBin)
                        {
                            continue;
                        }

                        SendUpdate(views[i], new ViewData
                        {
                            SellPrice = PriceUpdater.GetSellPrice(_markers, appliances[i].ID)
                        }, MessageType.SpecificViewUpdate);
                    }
                }
                finally
                {
                    entities.Dispose();
                    views.Dispose();
                    appliances.Dispose();
                    destroy.Dispose();
                }
            }
        }

        [MessagePackObject]
        public struct ViewData : ISpecificViewData, IViewData.ICheckForChanges<ViewData>
        {
            [Key(0)]
            public int SellPrice;

            public IUpdatableObject GetRelevantSubview(IObjectView view)
            {
                return view.GetSubView<ApplianceSellPriceView>();
            }

            public bool IsChangedFrom(ViewData check)
            {
                return SellPrice != check.SellPrice;
            }
        }

        private TextMeshPro _label;
        private int _price;
        private bool _marked;

        protected override void UpdateData(ViewData data)
        {
            _price = data.SellPrice;
            Apply();
        }

        public void SetMarkedForDeletion(bool marked)
        {
            _marked = marked;
            Apply();
        }

        private void Apply()
        {
            bool show = _marked && _price > 0;
            if (!show)
            {
                if (_label != null)
                {
                    _label.gameObject.SetActive(false);
                }

                return;
            }

            // Views can be created inactive; TextMeshPro defers Awake() there and
            // setting outline/material properties would throw. Build the label
            // on the first active frame instead (LateUpdate retries).
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            EnsureLabel();
            if (_label == null)
            {
                return;
            }

            _label.gameObject.SetActive(true);
            _label.text = "$" + _price;
        }

        private void EnsureLabel()
        {
            if (_label != null)
            {
                return;
            }

            var holder = new GameObject("SellPrice");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, TopOf(gameObject) + 0.25f, 0f);
            holder.transform.localScale = Vector3.one * 0.05f;

            _label = holder.AddComponent<TextMeshPro>();
            _label.font = WorldText.DefaultFont;
            _label.fontSize = 36f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.fontStyle = FontStyles.Bold;
            _label.color = new Color(1f, 0.88f, 0.2f, 1f);
            _label.outlineColor = new Color(0.05f, 0.05f, 0.05f, 1f);
            _label.outlineWidth = 0.2f;

            if (_label.font == null)
            {
                Debug.LogError("[TrashToCash] no TextMeshPro font available");
            }
        }

        private void LateUpdate()
        {
            if (_label == null && _marked && _price > 0)
            {
                Apply();
            }

            if (_label == null)
            {
                return;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                _label.transform.rotation = camera.transform.rotation;
            }
        }

        internal static float TopOf(GameObject root)
        {
            float top = 0f;
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.GetComponent<TextMeshPro>() != null)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                var local = root.transform.InverseTransformPoint(
                    new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                top = Mathf.Max(top, local.y);
                found = true;
            }

            return found ? top : 1f;
        }
    }

    internal static class WorldText
    {
        private static TMP_FontAsset _font;

        // TMP_Settings.defaultFontAsset is a "Reference - ..." placeholder with no
        // glyphs of its own; using it for world text renders black/invisible.
        // The game has dedicated in-world fonts that rely on locale fallbacks.
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
