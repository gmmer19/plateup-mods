using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using Platforms;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlateUpMods.ExtraOptions
{
    public sealed class ExtraOptionsMod : IModInitializer
    {
        public const string ModGuid = "plateup.mods.extraoptions";

        public void PostActivate(Mod mod)
        {
            new Harmony(ModGuid).PatchAll(typeof(ExtraOptionsMod).Assembly);
            Debug.Log("[ExtraOptions] loaded (+2 layout slots, +2 dish slots)");
        }

        public void PreInject() { }

        public void PostInject() { }
    }

    internal static class ExtraOptionsGrid
    {
        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Tile key from a world position (integer grid, x and z only).
        public static long Key(Vector3 v)
        {
            int x = Mathf.RoundToInt(v.x);
            int z = Mathf.RoundToInt(v.z);
            return ((long)x << 32) | (uint)z;
        }

        // Positions of every entity that has the marker component and a CPosition.
        public static List<Vector3> ReadPositions(EntityManager em, ComponentType marker)
        {
            var result = new List<Vector3>();
            var query = em.CreateEntityQuery(marker, ComponentType.ReadOnly<CPosition>());
            if (query.CalculateEntityCount() == 0)
            {
                return result;
            }

            var positions = query.ToComponentDataArray<CPosition>(Allocator.Temp);
            foreach (var p in positions)
            {
                result.Add(p.Position);
            }
            positions.Dispose();
            return result;
        }

        // Every tile that currently holds any entity with a position.
        public static HashSet<long> ReadAllTiles(EntityManager em)
        {
            var result = new HashSet<long>();
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<CPosition>());
            if (query.CalculateEntityCount() == 0)
            {
                return result;
            }

            var positions = query.ToComponentDataArray<CPosition>(Allocator.Temp);
            foreach (var p in positions)
            {
                result.Add(Key(p.Position));
            }
            positions.Dispose();
            return result;
        }

        // The slot in the bottom row (lowest z). Ties are broken by x:
        // leftmost when rightmost == false, rightmost otherwise.
        public static bool TryGetCorner(List<Vector3> slots, bool rightmost, out Vector3 corner)
        {
            corner = Vector3.zero;
            if (slots.Count == 0)
            {
                return false;
            }

            bool first = true;
            int bestX = 0;
            int bestZ = 0;
            foreach (var s in slots)
            {
                int x = Mathf.RoundToInt(s.x);
                int z = Mathf.RoundToInt(s.z);
                bool better = first
                    || z < bestZ
                    || (z == bestZ && (rightmost ? x > bestX : x < bestX));
                if (better)
                {
                    bestX = x;
                    bestZ = z;
                    first = false;
                }
            }

            corner = new Vector3(bestX, 0f, bestZ);
            return true;
        }

        public static MethodInfo FindMethod(Type type, string name)
        {
            MethodInfo fallback = null;
            foreach (var m in type.GetMethods(Flags))
            {
                if (m.Name != name)
                {
                    continue;
                }

                var ps = m.GetParameters();
                if (ps.Length > 0 && ps[0].ParameterType == typeof(Vector3))
                {
                    return m;
                }

                if (fallback == null)
                {
                    fallback = m;
                }
            }
            return fallback;
        }

        // Fills the arguments by parameter type, so a slightly different
        // signature of the vanilla method still gets called correctly.
        public static object[] BuildArgs(MethodInfo method, Vector3 position, Dish dish)
        {
            var ps = method.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var t = ps[i].ParameterType;
                if (t == typeof(Vector3))
                {
                    args[i] = position;
                }
                else if (t == typeof(Dish))
                {
                    args[i] = dish;
                }
                else if (t == typeof(int) && dish != null)
                {
                    args[i] = dish.ID;
                }
                else if (t.IsValueType)
                {
                    args[i] = Activator.CreateInstance(t);
                }
                else
                {
                    args[i] = null;
                }
            }
            return args;
        }

        public static string Describe(MethodInfo method)
        {
            var text = method.Name + "(";
            var ps = method.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0)
                {
                    text += ", ";
                }
                text += ps[i].ParameterType.Name + " " + ps[i].Name;
            }
            return text + ")";
        }
    }

    // Layout slots: the vanilla row runs along the bottom wall, left to right, with the
    // leftmost slot next to the passage from the loading bay. The vanilla slots are
    // reached from the north, so the extra slots go two rows up with a free aisle in
    // between. They start above the rightmost vanilla slot and stop one tile short of
    // the extra dish pedestals, leaving a one-pedestal gap between the two groups.
    [HarmonyPatch(typeof(CreateLayoutSlots), "OnUpdate")]
    internal static class CreateLayoutSlotsPatch
    {
        // x offsets from the rightmost vanilla slot, and how many rows above the bottom row.
        // 0 and 1 put the pair directly above the rightmost slot and one tile right of it.
        private static readonly int[] ExtraDx = { 0, 1 };
        private const int ExtraDz = 2;

        private static MethodInfo createMapSource;
        private static bool failed;
        private static bool reportedBlocked;

        [HarmonyPostfix]
        private static void Postfix(CreateLayoutSlots __instance)
        {
            if (failed || PlatformSettings.DebugQuickLoadLobby || GameData.Main == null)
            {
                return;
            }

            try
            {
                var em = __instance.EntityManager;
                var slots = ExtraOptionsGrid.ReadPositions(
                    em, ComponentType.ReadOnly<CreateLayoutSlots.CLayoutSlot>());

                Vector3 corner;
                if (!ExtraOptionsGrid.TryGetCorner(slots, true, out corner))
                {
                    // Vanilla slots do not exist (yet).
                    return;
                }

                var slotTiles = new HashSet<long>();
                foreach (var s in slots)
                {
                    slotTiles.Add(ExtraOptionsGrid.Key(s));
                }

                var allTiles = ExtraOptionsGrid.ReadAllTiles(em);
                var targets = new List<Vector3>();
                foreach (int dx in ExtraDx)
                {
                    var position = corner + new Vector3(dx, 0f, ExtraDz);
                    long key = ExtraOptionsGrid.Key(position);

                    if (slotTiles.Contains(key))
                    {
                        // Already there (added earlier).
                        continue;
                    }

                    if (allTiles.Contains(key))
                    {
                        if (!reportedBlocked)
                        {
                            reportedBlocked = true;
                            Debug.Log($"[ExtraOptions] layout: tile ({position.x}, {position.z}) is occupied, skipping it");
                        }
                        continue;
                    }

                    targets.Add(position);
                }

                if (targets.Count == 0)
                {
                    return;
                }

                if (createMapSource == null)
                {
                    createMapSource = ExtraOptionsGrid.FindMethod(typeof(CreateLayoutSlots), "CreateMapSource");
                    if (createMapSource == null)
                    {
                        failed = true;
                        Debug.LogError("[ExtraOptions] CreateMapSource not found");
                        return;
                    }
                    Debug.Log("[ExtraOptions] using " + ExtraOptionsGrid.Describe(createMapSource));
                }

                foreach (var position in targets)
                {
                    createMapSource.Invoke(__instance, ExtraOptionsGrid.BuildArgs(createMapSource, position, null));
                }

                Debug.Log($"[ExtraOptions] added {targets.Count} layout slots, rightmost vanilla slot at ({corner.x}, {corner.z})");
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogError("[ExtraOptions] layout patch failed: " + e);
            }
        }
    }

    // Dish slots: vanilla has a row along the bottom wall plus a column going up the right
    // wall from the rightmost slot (the corner). Those slots are reached from the north
    // and from the west, and the doorway to the right-hand area sits just above the column,
    // so the column is left alone. The extra slots go two rows up, with a free aisle
    // below them, above the 3rd and 4th tile counted from the corner.
    [HarmonyPatch(typeof(CreateDishOptions), "OnUpdate")]
    internal static class CreateDishOptionsPatch
    {
        // x offsets from the rightmost vanilla slot, and how many rows above the bottom row.
        private static readonly int[] ExtraDx = { -2, -3 };
        private const int ExtraDz = 2;

        private static MethodInfo createFoodSource;
        private static bool failed;
        private static bool reportedBlocked;
        private static bool reportedNoDishes;

        [HarmonyPostfix]
        private static void Postfix(CreateDishOptions __instance)
        {
            if (failed || PlatformSettings.DebugQuickLoadLobby || PlatformSettings.IsDemoMode || GameData.Main == null)
            {
                return;
            }

            try
            {
                var em = __instance.EntityManager;
                var slots = ExtraOptionsGrid.ReadPositions(em, ComponentType.ReadOnly<CDishSource>());

                Vector3 corner;
                if (!ExtraOptionsGrid.TryGetCorner(slots, true, out corner))
                {
                    // Vanilla slots do not exist (yet).
                    return;
                }

                var slotTiles = new HashSet<long>();
                foreach (var s in slots)
                {
                    slotTiles.Add(ExtraOptionsGrid.Key(s));
                }

                var allTiles = ExtraOptionsGrid.ReadAllTiles(em);
                var targets = new List<Vector3>();
                foreach (int dx in ExtraDx)
                {
                    var position = corner + new Vector3(dx, 0f, ExtraDz);
                    long key = ExtraOptionsGrid.Key(position);

                    if (slotTiles.Contains(key))
                    {
                        continue;
                    }

                    if (allTiles.Contains(key))
                    {
                        if (!reportedBlocked)
                        {
                            reportedBlocked = true;
                            Debug.Log($"[ExtraOptions] dishes: tile ({position.x}, {position.z}) is occupied, skipping it");
                        }
                        continue;
                    }

                    targets.Add(position);
                }

                if (targets.Count == 0)
                {
                    return;
                }

                var alreadyPlaced = new HashSet<int>();
                var choiceQuery = em.CreateEntityQuery(ComponentType.ReadOnly<CDishChoice>());
                var choices = choiceQuery.ToComponentDataArray<CDishChoice>(Allocator.Temp);
                foreach (var choice in choices)
                {
                    alreadyPlaced.Add(choice.Dish);
                }
                choices.Dispose();

                var available = new List<int>();
                var seen = new HashSet<int>();
                var upgradeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<CDishUpgrade>());
                var upgrades = upgradeQuery.ToComponentDataArray<CDishUpgrade>(Allocator.Temp);
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.DishID != 0 && !alreadyPlaced.Contains(upgrade.DishID) && seen.Add(upgrade.DishID))
                    {
                        available.Add(upgrade.DishID);
                    }
                }
                upgrades.Dispose();

                if (available.Count == 0)
                {
                    if (!reportedNoDishes)
                    {
                        reportedNoDishes = true;
                        Debug.Log("[ExtraOptions] dishes: no spare dishes to offer");
                    }
                    return;
                }

                if (createFoodSource == null)
                {
                    createFoodSource = ExtraOptionsGrid.FindMethod(typeof(CreateDishOptions), "CreateFoodSource");
                    if (createFoodSource == null)
                    {
                        failed = true;
                        Debug.LogError("[ExtraOptions] CreateFoodSource not found");
                        return;
                    }
                    Debug.Log("[ExtraOptions] using " + ExtraOptionsGrid.Describe(createFoodSource));
                }

                Shuffle(available);

                int index = 0;
                int added = 0;
                foreach (var position in targets)
                {
                    Dish dish = null;
                    while (index < available.Count)
                    {
                        Dish candidate;
                        if (GameData.Main.TryGet<Dish>(available[index], out candidate))
                        {
                            dish = candidate;
                            index++;
                            break;
                        }
                        index++;
                    }

                    if (dish == null)
                    {
                        break;
                    }

                    createFoodSource.Invoke(__instance, ExtraOptionsGrid.BuildArgs(createFoodSource, position, dish));
                    added++;
                }

                if (added > 0)
                {
                    Debug.Log($"[ExtraOptions] added {added} dish slots, bottom row ends at ({corner.x}, {corner.z})");
                }
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogError("[ExtraOptions] dish patch failed: " + e);
            }
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
