using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class ElementPlacerTests
    {
        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        private static Edge E(Vector2Int a, Vector2Int b) => LevelLayoutBuilder.EdgeBetweenCells(a, b);

        // A 6x6 maze with every interior edge open.
        private static LevelLayoutBuilder OpenBuilder()
        {
            var maze = new Maze(6);
            foreach (var e in maze.InteriorEdges().ToList())
            {
                maze.SetWall(e, false);
            }

            return new LevelLayoutBuilder(maze);
        }

        private static bool Touches(Edge e, Vector2Int c) => e.A == c || e.B == c;

        [Test]
        public void Gate_IsNeverPlacedNextToAPhaseWall()
        {
            var phase = E(C(2, 2), C(3, 2));
            var b = OpenBuilder().Add(new PhaseWall(phase, 0f, false));
            var placer = new ElementPlacer(b, new System.Random(3));
            Assert.IsFalse(placer.CanHostCellHazard(C(2, 2)));
            Assert.IsFalse(placer.CanHostCellHazard(C(3, 2)));
            Assert.IsTrue(placer.CanHostCellHazard(C(2, 4)));

            placer.Place(Mechanic.RotatingBarriers, 25);
            Assert.Greater(b.RotatingGates.Count, 0);
            Assert.IsFalse(b.RotatingGates.Any(g => Touches(phase, g.Cell)));
        }

        [Test]
        public void CollapseTile_IsNeverPlacedBetweenPhaseWalls()
        {
            var below = E(C(2, 1), C(2, 2));
            var above = E(C(2, 2), C(2, 3));
            var b = OpenBuilder().Add(new PhaseWall(below, 0f, false)).Add(new PhaseWall(above, 1f, false));
            var placer = new ElementPlacer(b, new System.Random(5));
            Assert.IsFalse(placer.CanHostCellHazard(C(2, 2)));

            placer.Place(Mechanic.CollapsingTiles, 25);
            Assert.Greater(b.CollapseTiles.Count, 0);
            Assert.IsFalse(b.CollapseTiles.Any(c => Touches(below, c.Cell) || Touches(above, c.Cell)));
        }

        [Test]
        public void CellHazards_AvoidMovingWallEndpoints()
        {
            var a = E(C(1, 3), C(2, 3));
            var m = E(C(2, 3), C(3, 3));
            var b = OpenBuilder().Add(new MovingWall(a, m, 0f));
            var placer = new ElementPlacer(b, new System.Random(7));
            foreach (var cell in new[] { C(1, 3), C(2, 3), C(3, 3) })
            {
                Assert.IsFalse(placer.CanHostCellHazard(cell), cell.ToString());
            }

            placer.Place(Mechanic.RotatingBarriers, 15);
            placer.Place(Mechanic.CollapsingTiles, 15);
            Assert.IsFalse(b.RotatingGates.Any(g => Touches(a, g.Cell) || Touches(m, g.Cell)));
            Assert.IsFalse(b.CollapseTiles.Any(c => Touches(a, c.Cell) || Touches(m, c.Cell)));
        }

        [Test]
        public void PeriodicEdges_AvoidExistingGatesAndCollapseTiles()
        {
            var b = OpenBuilder()
                .Add(new RotatingGate(C(2, 2), 0f, true))
                .Add(new CollapseTile(C(4, 3), false));
            var placer = new ElementPlacer(b, new System.Random(11));
            Assert.IsFalse(placer.CanHostPeriodicEdge(E(C(2, 2), C(3, 2))));
            Assert.IsFalse(placer.CanHostPeriodicEdge(E(C(4, 3), C(4, 4))));
            Assert.IsTrue(placer.CanHostPeriodicEdge(E(C(3, 4), C(3, 5))));

            placer.Place(Mechanic.DisappearingWalls, 10);
            placer.Place(Mechanic.MovingWalls, 10);
            foreach (var cell in new[] { C(2, 2), C(4, 3) })
            {
                Assert.IsFalse(b.PhaseWalls.Any(p => Touches(p.Edge, cell)));
                Assert.IsFalse(b.MovingWalls.Any(w => Touches(w.A, cell) || Touches(w.B, cell)));
            }
        }

        [Test]
        public void TriggerLinks_NeverTouchTheirPlate()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var b = OpenBuilder();
                new ElementPlacer(b, new System.Random(seed)).Place(Mechanic.TriggerWalls, 3);
                foreach (var p in b.TriggerPlates)
                {
                    Assert.IsFalse(p.LinkedEdges().Any(e => Touches(e, p.Plate)), $"seed {seed}");
                }
            }
        }
    }
}
