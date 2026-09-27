using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class LevelGeneratorTests
    {
        private const int MaxLevel = 15;
        private const int MaxSeed = 20;

        private static Dictionary<(int, int), LevelLayout> _all;
        private static double _allMillis;

        // Generates levels 1..15 x seeds 1..20 once and records how long it took.
        private static Dictionary<(int, int), LevelLayout> All()
        {
            if (_all != null)
            {
                return _all;
            }

            var sw = Stopwatch.StartNew();
            var dict = new Dictionary<(int, int), LevelLayout>();
            for (int level = 1; level <= MaxLevel; level++)
            {
                for (int seed = 1; seed <= MaxSeed; seed++)
                {
                    dict[(level, seed)] = LevelGenerator.Generate(level, seed);
                }
            }

            sw.Stop();
            _allMillis = sw.Elapsed.TotalMilliseconds;
            _all = dict;
            return _all;
        }

        private static readonly Dictionary<int, Mechanic[]> Intros = new Dictionary<int, Mechanic[]>
        {
            { 2, new[] { Mechanic.InvisibleWalls, Mechanic.MemoryTiles } },
            { 3, new[] { Mechanic.MovingWalls, Mechanic.DisappearingWalls } },
            { 4, new[] { Mechanic.CollapsingTiles, Mechanic.TriggerWalls } },
            { 5, new[] { Mechanic.Teleporters, Mechanic.RotatingBarriers } },
            { 6, new[] { Mechanic.Patrols, Mechanic.OneWayPaths } },
            { 7, new[] { Mechanic.Decoys } },
            { 8, new[] { Mechanic.Chaser } },
        };

        private static int CountOf(LevelLayout l, Mechanic m)
        {
            switch (m)
            {
                case Mechanic.InvisibleWalls: return l.InvisibleWalls.Count;
                case Mechanic.MemoryTiles: return l.MemoryTiles.Count;
                case Mechanic.MovingWalls: return l.MovingWalls.Count;
                case Mechanic.DisappearingWalls: return l.PhaseWalls.Count;
                case Mechanic.CollapsingTiles: return l.CollapseTiles.Count;
                case Mechanic.TriggerWalls: return l.TriggerPlates.Count;
                case Mechanic.Teleporters: return l.Teleporters.Count;
                case Mechanic.RotatingBarriers: return l.RotatingGates.Count;
                case Mechanic.Patrols: return l.Patrols.Count;
                case Mechanic.OneWayPaths: return l.OneWays.Count;
                case Mechanic.Decoys: return l.Decoys.Count;
                default: return l.HasChaser ? 1 : 0;
            }
        }

        // Interior walls of the braided maze before any placement: the generator only ever
        // removes walls for base-wall phase walls and trigger "opens" edges.
        private static int BraidedWalls(LevelLayout l)
        {
            return l.Maze.InteriorWalls().Count()
                   + l.PhaseWalls.Count(p => p.OnBaseWall)
                   + l.TriggerPlates.Sum(p => p.Opens.Count);
        }

        private static string Describe(LevelLayout l)
        {
            var sb = new StringBuilder();
            sb.Append($"L{l.Level} N{l.N} S{l.Start} D{l.Destination} chaser {l.HasChaser}\n");
            sb.Append("walls ").Append(string.Join(",", l.Maze.InteriorWalls().Select(e => e.A + "" + e.IsVertical))).Append('\n');
            sb.Append("mech ").Append(string.Join(",", l.Mechanics)).Append(" new ").Append(string.Join(",", l.NewMechanics)).Append('\n');
            foreach (var x in l.InvisibleWalls) sb.Append("I").Append(x.Edge.A).Append(x.Edge.IsVertical);
            foreach (var x in l.MemoryTiles) sb.Append("M").Append(x.Cell);
            foreach (var x in l.MovingWalls) sb.Append("W").Append(x.A.A).Append(x.B.A).Append(x.Phase.ToString("R"));
            foreach (var x in l.PhaseWalls) sb.Append("P").Append(x.Edge.A).Append(x.Phase.ToString("R")).Append(x.OnBaseWall);
            foreach (var x in l.CollapseTiles) sb.Append("C").Append(x.Cell).Append(x.Permanent);
            foreach (var x in l.TriggerPlates) sb.Append("T").Append(x.Plate).Append(string.Join("", x.Opens.Select(e => e.A))).Append("|").Append(string.Join("", x.Closes.Select(e => e.A))).Append(x.ColorIndex);
            foreach (var x in l.Teleporters) sb.Append("X").Append(x.Pad).Append(x.Target);
            foreach (var x in l.RotatingGates) sb.Append("G").Append(x.Cell).Append(x.Phase.ToString("R")).Append(x.StartHorizontal);
            foreach (var x in l.Patrols) sb.Append("R").Append(string.Join("", x.Path)).Append(x.Phase.ToString("R"));
            foreach (var x in l.OneWays) sb.Append("O").Append(x.Edge.A).Append(x.Allowed);
            foreach (var x in l.Decoys) sb.Append("D").Append(x.Cell);
            return sb.ToString();
        }

        [Test]
        public void AllLevels_AreSolvable_WithStartNotDestination()
        {
            foreach (var kv in All())
            {
                var l = kv.Value;
                string id = $"level {kv.Key.Item1} seed {kv.Key.Item2}";
                Assert.AreEqual(kv.Key.Item1, l.Level, id);
                Assert.AreEqual(kv.Key.Item1 + 3, l.N, id);
                Assert.AreEqual(kv.Key.Item2, l.Seed, id);
                Assert.AreEqual(new Vector2Int(0, 0), l.Start, id);
                Assert.AreNotEqual(l.Start, l.Destination, id);
                Assert.IsTrue(SolvabilityValidator.IsSolvable(l), id);
            }
        }

        [Test]
        public void Generation_IsFastEnough()
        {
            All();
            TestContext.Out.WriteLine($"Generated {MaxLevel * MaxSeed} levels in {_allMillis:F0} ms");
            UnityEngine.Debug.Log($"[LevelGeneratorTests] Generated {MaxLevel * MaxSeed} levels in {_allMillis:F0} ms");
            Assert.Less(_allMillis, 20000.0);
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            for (int level = 1; level <= 12; level += 1)
            {
                Assert.AreEqual(Describe(LevelGenerator.Generate(level, 77)), Describe(LevelGenerator.Generate(level, 77)));
            }

            Assert.AreNotEqual(Describe(LevelGenerator.Generate(9, 1)), Describe(LevelGenerator.Generate(9, 2)));
        }

        [Test]
        public void Level1_HasNoMechanics()
        {
            for (int seed = 1; seed <= MaxSeed; seed++)
            {
                var l = All()[(1, seed)];
                Assert.AreEqual(0, l.Mechanics.Count);
                Assert.AreEqual(0, l.NewMechanics.Count);
                foreach (Mechanic m in System.Enum.GetValues(typeof(Mechanic)))
                {
                    Assert.AreEqual(0, CountOf(l, m), m.ToString());
                }
            }
        }

        [Test]
        public void Level2_IntroducesInvisibleWallsAndMemoryTiles()
        {
            for (int seed = 1; seed <= MaxSeed; seed++)
            {
                var l = All()[(2, seed)];
                CollectionAssert.AreEquivalent(new[] { Mechanic.InvisibleWalls, Mechanic.MemoryTiles }, l.NewMechanics);
                CollectionAssert.AreEquivalent(new[] { Mechanic.InvisibleWalls, Mechanic.MemoryTiles }, l.Mechanics);
            }
        }

        [Test]
        public void IntroLevels_IntroduceExactlyTheirMechanics_WithElements()
        {
            for (int level = 2; level <= 8; level++)
            {
                for (int seed = 1; seed <= MaxSeed; seed++)
                {
                    var l = All()[(level, seed)];
                    string id = $"level {level} seed {seed}";
                    CollectionAssert.AreEquivalent(Intros[level], l.NewMechanics, id);
                    foreach (var m in Intros[level])
                    {
                        Assert.GreaterOrEqual(CountOf(l, m), 1, id + " " + m);
                    }
                }
            }

            for (int level = 9; level <= MaxLevel; level++)
            {
                for (int seed = 1; seed <= MaxSeed; seed++)
                {
                    Assert.AreEqual(0, All()[(level, seed)].NewMechanics.Count);
                }
            }
        }

        [Test]
        public void Counts_FollowThePlan()
        {
            foreach (var kv in All())
            {
                var l = kv.Value;
                string id = $"level {kv.Key.Item1} seed {kv.Key.Item2}";
                var planned = LevelPlan.MechanicsFor(l.Level, new System.Random(l.Seed));
                int w = BraidedWalls(l);
                foreach (Mechanic m in System.Enum.GetValues(typeof(Mechanic)))
                {
                    int count = CountOf(l, m);
                    bool present = l.Mechanics.Contains(m);
                    Assert.AreEqual(present, count > 0, id + " listed iff placed: " + m);
                    if (!planned.Contains(m))
                    {
                        Assert.AreEqual(0, count, id + " unplanned " + m);
                        continue;
                    }

                    int plan = LevelPlan.CountFor(m, l.N, w, LevelPlan.IsHalf(m, l.Level));
                    Assert.LessOrEqual(count, plan, id + " " + m);
                    if (m == Mechanic.InvisibleWalls || m == Mechanic.MemoryTiles || m == Mechanic.Chaser)
                    {
                        Assert.AreEqual(plan, count, id + " exact " + m);
                    }
                }

                foreach (var m in l.NewMechanics)
                {
                    CollectionAssert.Contains(l.Mechanics, m, id);
                    Assert.AreEqual(l.Level, MechanicInfo.Get(m).IntroLevel, id);
                }
            }
        }

        [Test]
        public void MostPlannedCounts_AreActuallyPlaced()
        {
            // Placement may skip elements, but on average it should come close to the plan.
            int planned = 0, placed = 0;
            foreach (var kv in All())
            {
                var l = kv.Value;
                var plan = LevelPlan.MechanicsFor(l.Level, new System.Random(l.Seed));
                int w = BraidedWalls(l);
                foreach (var m in plan)
                {
                    planned += LevelPlan.CountFor(m, l.N, w, LevelPlan.IsHalf(m, l.Level));
                    placed += CountOf(l, m);
                }
            }

            TestContext.Out.WriteLine($"placed {placed} of {planned} planned elements");
            UnityEngine.Debug.Log($"[LevelGeneratorTests] placed {placed} of {planned} planned elements");
            Assert.Greater(placed, planned * 0.8);
        }

        [Test]
        public void PlacementRules_Hold()
        {
            foreach (var kv in All())
            {
                CheckPlacementRules(kv.Value, $"level {kv.Key.Item1} seed {kv.Key.Item2}");
            }
        }

        private static void CheckPlacementRules(LevelLayout l, string id)
        {
            var maze = l.Maze;
            var excluded = new HashSet<Vector2Int> { l.Start, l.Destination };
            foreach (var d in DirUtil.All)
            {
                excluded.Add(l.Start + DirUtil.Delta(d));
            }

            // One cell element per cell, none on excluded cells.
            var cells = new List<Vector2Int>();
            cells.AddRange(l.MemoryTiles.Select(x => x.Cell));
            cells.AddRange(l.CollapseTiles.Select(x => x.Cell));
            cells.AddRange(l.TriggerPlates.Select(x => x.Plate));
            cells.AddRange(l.Teleporters.Select(x => x.Pad));
            cells.AddRange(l.Teleporters.Select(x => x.Target));
            cells.AddRange(l.RotatingGates.Select(x => x.Cell));
            cells.AddRange(l.Decoys.Select(x => x.Cell));
            foreach (var p in l.Patrols) cells.AddRange(p.Path);
            Assert.AreEqual(cells.Count, cells.Distinct().Count(), id + " one cell element per cell");
            foreach (var c in cells)
            {
                Assert.IsTrue(maze.InBounds(c), id);
                Assert.IsFalse(excluded.Contains(c), id + " excluded cell " + c);
            }

            // One edge element per edge; never touching Start or Destination.
            var edges = new List<Edge>();
            edges.AddRange(l.InvisibleWalls.Select(x => x.Edge));
            foreach (var w in l.MovingWalls) { edges.Add(w.A); edges.Add(w.B); }
            edges.AddRange(l.PhaseWalls.Select(x => x.Edge));
            foreach (var p in l.TriggerPlates) edges.AddRange(p.LinkedEdges());
            edges.AddRange(l.OneWays.Select(x => x.Edge));
            Assert.AreEqual(edges.Count, edges.Distinct().Count(), id + " one edge element per edge");
            foreach (var e in edges)
            {
                Assert.IsFalse(e.A == l.Start || e.B == l.Start || e.A == l.Destination || e.B == l.Destination, id + " edge touches S/D");
            }

            // Maze invariant: invisible walls are walls, dynamic edges are open.
            foreach (var x in l.InvisibleWalls) Assert.IsTrue(maze.HasWall(x.Edge), id);
            foreach (var w in l.MovingWalls)
            {
                Assert.IsFalse(maze.HasWall(w.A), id);
                Assert.IsFalse(maze.HasWall(w.B), id);
                Assert.AreEqual(w.A.IsVertical, w.B.IsVertical, id + " same orientation");
                // Collinear neighbors along the wall's own line (see the brief's playtest fix):
                // a vertical wall segment (lower cell (x,y), separating (x,y)-(x+1,y)) pairs with
                // the one directly above/below it, same x, adjacent y; a horizontal wall segment
                // pairs with the one directly beside it, same y, adjacent x. Not a full cell apart
                // on opposite sides of a shared middle cell (the pre-fix, unreadable geometry).
                Vector2Int diff = w.B.A - w.A.A;
                Vector2Int expected = w.A.IsVertical ? new Vector2Int(0, 1) : new Vector2Int(1, 0);
                Assert.IsTrue(diff == expected || diff == -expected, id + " collinear neighbor, not diagonal/gapped");
                Assert.That(w.Phase, Is.InRange(0f, MovingWall.Cycle));
            }

            foreach (var p in l.PhaseWalls)
            {
                Assert.IsFalse(maze.HasWall(p.Edge), id);
                Assert.That(p.Phase, Is.InRange(0f, PhaseWall.Cycle));
            }

            if (l.PhaseWalls.Count > 0)
            {
                int plan = LevelPlan.CountFor(Mechanic.DisappearingWalls, l.N, BraidedWalls(l), LevelPlan.IsHalf(Mechanic.DisappearingWalls, l.Level));
                Assert.LessOrEqual(l.PhaseWalls.Count(p => p.OnBaseWall), plan / 2, id + " at most half on base walls");
                Assert.LessOrEqual(l.PhaseWalls.Count(p => !p.OnBaseWall), plan - plan / 2, id + " the rest on the route");
            }

            foreach (var p in l.TriggerPlates)
            {
                Assert.That(p.Opens.Count + p.Closes.Count, Is.InRange(1, 2), id);
                foreach (var e in p.LinkedEdges()) Assert.IsFalse(maze.HasWall(e), id);
            }

            foreach (var o in l.OneWays)
            {
                Assert.IsFalse(maze.HasWall(o.Edge), id);
                Vector2Int step = DirUtil.Delta(o.Allowed);
                Assert.IsTrue(o.Edge.A + step == o.Edge.B || o.Edge.B + step == o.Edge.A, id + " one-way direction crosses its edge");
            }

            foreach (var t in l.Teleporters)
            {
                Assert.AreNotEqual(t.Pad, t.Target, id);
            }

            foreach (var g in l.RotatingGates)
            {
                Assert.That(g.Phase, Is.InRange(0f, RotatingGate.Cycle));
            }

            foreach (var p in l.Patrols)
            {
                Assert.GreaterOrEqual(p.Path.Count, 2, id);
                Assert.LessOrEqual(p.Path.Count, 5, id);
                for (int i = 0; i + 1 < p.Path.Count; i++)
                {
                    var e = LevelLayoutBuilder.EdgeBetweenCells(p.Path[i], p.Path[i + 1]);
                    Assert.IsFalse(maze.HasWall(e), id + " patrol path open");
                    Assert.AreEqual(EdgeElementKind.None, l.EdgeElementAt(e), id + " patrol path edge free");
                }

                Assert.AreEqual(1, maze.OpenNeighbors(p.Path[p.Path.Count - 1]).Count(), id + " patrol ends in a dead end");
                Assert.That(p.Phase, Is.InRange(0f, p.Cycle));
            }

            foreach (var d in l.Decoys)
            {
                Assert.AreEqual(1, maze.OpenNeighbors(d.Cell).Count(), id + " decoy in a dead end");
            }

            // Gates and collapsing tiles never sit on an endpoint of a phase-wall or moving-wall edge.
            var periodic = new List<Edge>(l.PhaseWalls.Select(p => p.Edge));
            foreach (var w in l.MovingWalls) { periodic.Add(w.A); periodic.Add(w.B); }
            var hazards = l.RotatingGates.Select(g => g.Cell).Concat(l.CollapseTiles.Select(c => c.Cell));
            foreach (var c in hazards)
            {
                Assert.IsFalse(periodic.Any(e => e.A == c || e.B == c), id + " hazard next to periodic edge " + c);
            }

            // A trigger's linked edges never touch its own plate.
            foreach (var p in l.TriggerPlates)
            {
                Assert.IsFalse(p.LinkedEdges().Any(e => e.A == p.Plate || e.B == p.Plate), id + " trigger link touches plate");
            }
        }
    }
}
