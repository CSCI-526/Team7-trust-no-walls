using System;
using System.Collections.Generic;

namespace TrustNoWall.Core
{
    /// <summary>
    /// The spec's level progression: which mechanics appear at each level and how many elements
    /// of each. Rounding is "round half away from zero".
    /// </summary>
    public static class LevelPlan
    {
        /// <summary>
        /// Mechanics planned for <paramref name="level"/>, in canonical enum order.
        /// Level 1: none. Levels 2 and 3 contain ONLY the mechanics introduced there (so a level's
        /// first exposure to a mechanic is never muddied by another one). From level 4 on, levels
        /// 4..8 also get one random earlier-introduced mechanic (at half count, see
        /// <see cref="IsHalf"/>). Level 9+: exactly 4 random mechanics (5 from level 12).
        /// InvisibleWalls and MemoryTiles are one unit: picking either adds both (the pair counts
        /// as 2 toward the level 9+ total).
        /// </summary>
        public static IReadOnlyList<Mechanic> MechanicsFor(int level, System.Random rng)
        {
            var result = new List<Mechanic>();
            if (level <= 1)
            {
                return result;
            }

            if (level <= 8)
            {
                foreach (var info in MechanicInfo.All)
                {
                    if (info.IntroLevel == level)
                    {
                        result.Add(info.Mechanic);
                    }
                }

                if (level >= 4)
                {
                    var earlier = Units(m => MechanicInfo.Get(m).IntroLevel < level);
                    result.AddRange(earlier[rng.Next(earlier.Count)]);
                }
            }
            else
            {
                int target = level >= 12 ? 5 : 4;
                var units = Units(m => true);
                Shuffle(units, rng);
                foreach (var unit in units)
                {
                    if (result.Count + unit.Length <= target)
                    {
                        result.AddRange(unit);
                    }

                    if (result.Count == target)
                    {
                        break;
                    }
                }
            }

            result.Sort();
            return result.AsReadOnly();
        }

        /// <summary>
        /// True when <paramref name="m"/> appears at half count on <paramref name="level"/>: the
        /// earlier mechanic added to intro levels 4..8 (levels 2 and 3 never get one).
        /// </summary>
        public static bool IsHalf(Mechanic m, int level)
        {
            return level >= 4 && level <= 8 && MechanicInfo.Get(m).IntroLevel < level;
        }

        /// <summary>
        /// Element count for mechanic <paramref name="m"/> in an n x n maze with
        /// <paramref name="interiorWalls"/> interior walls (after braiding). Half: rounded up, minimum 1.
        /// The level used for the decoy rule is n - 3.
        /// </summary>
        public static int CountFor(Mechanic m, int n, int interiorWalls, bool half)
        {
            int a = n * n;
            int full;
            switch (m)
            {
                case Mechanic.InvisibleWalls: full = Round(0.25 * interiorWalls); break;
                case Mechanic.MemoryTiles: full = Math.Max(1, Round(n / 3.0)); break;
                case Mechanic.MovingWalls: full = Math.Max(1, Round(a / 20.0)); break;
                case Mechanic.DisappearingWalls: full = Math.Max(2, Round(a / 20.0)); break;
                case Mechanic.CollapsingTiles: full = Math.Max(2, Round(a / 10.0)); break;
                case Mechanic.TriggerWalls: full = 1 + a / 60; break;
                case Mechanic.Teleporters: full = 1 + a / 80; break;
                case Mechanic.RotatingBarriers: full = Math.Max(1, Round(a / 30.0)); break;
                case Mechanic.Patrols: full = 1 + a / 60; break;
                case Mechanic.OneWayPaths: full = Math.Max(1, Round(a / 25.0)); break;
                case Mechanic.Decoys: full = n - 3 >= 11 ? 2 : 1; break;
                case Mechanic.Chaser: full = 1; break;
                default: throw new ArgumentOutOfRangeException(nameof(m), m, null);
            }

            if (!half)
            {
                return full;
            }

            return Math.Max(1, (full + 1) / 2);
        }

        private static int Round(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        // Selection units: InvisibleWalls+MemoryTiles form one unit, every other mechanic is its own.
        private static List<Mechanic[]> Units(Func<Mechanic, bool> filter)
        {
            var units = new List<Mechanic[]>();
            foreach (var info in MechanicInfo.All)
            {
                var m = info.Mechanic;
                if (m == Mechanic.MemoryTiles || !filter(m))
                {
                    continue;
                }

                units.Add(m == Mechanic.InvisibleWalls
                    ? new[] { Mechanic.InvisibleWalls, Mechanic.MemoryTiles }
                    : new[] { m });
            }

            return units;
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
