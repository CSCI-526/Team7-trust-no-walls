using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// Generates a solvable level from (level, seed). Deterministic: one System.Random seeded with
    /// <c>seed</c> drives everything, and its first use is <see cref="LevelPlan.MechanicsFor"/>, so
    /// <c>LevelPlan.MechanicsFor(level, new System.Random(seed))</c> reproduces the plan.
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>The order in which mechanics are placed (edge-heavy, topology-changing ones first).</summary>
        public static readonly IReadOnlyList<Mechanic> PlacementOrder = new[]
        {
            Mechanic.TriggerWalls, Mechanic.OneWayPaths, Mechanic.MovingWalls, Mechanic.DisappearingWalls,
            Mechanic.InvisibleWalls, Mechanic.MemoryTiles, Mechanic.CollapsingTiles, Mechanic.Teleporters,
            Mechanic.RotatingBarriers, Mechanic.Patrols, Mechanic.Decoys, Mechanic.Chaser
        };

        public static LevelLayout Generate(int level, int seed)
        {
            var rng = new System.Random(seed);
            var planned = LevelPlan.MechanicsFor(level, rng);

            int n = level + 3;
            var maze = MazeGenerator.GeneratePerfect(n, rng);
            var start = new Vector2Int(0, 0);
            var destination = MazeGenerator.Farthest(maze, start);
            if (level >= 4)
            {
                MazeGenerator.Braid(maze, (int)(0.06 * n * n), rng);
            }

            int interiorWalls = 0;
            foreach (var unused in maze.InteriorWalls())
            {
                interiorWalls++;
            }

            var builder = new LevelLayoutBuilder(maze)
                .WithLevel(level)
                .WithSeed(seed)
                .WithStart(start)
                .WithDestination(destination);
            var placer = new ElementPlacer(builder, rng);

            foreach (var m in PlacementOrder)
            {
                if (!Contains(planned, m))
                {
                    continue;
                }

                int count = LevelPlan.CountFor(m, n, interiorWalls, LevelPlan.IsHalf(m, level));
                placer.Place(m, count);
            }

            var present = builder.InferMechanics();
            var introduced = new List<Mechanic>();
            foreach (var m in present)
            {
                if (MechanicInfo.Get(m).IntroLevel == level)
                {
                    introduced.Add(m);
                }
            }

            builder.WithMechanics(present, introduced);
            return builder.Build();
        }

        private static bool Contains(IReadOnlyList<Mechanic> list, Mechanic m)
        {
            foreach (var x in list)
            {
                if (x == m)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
