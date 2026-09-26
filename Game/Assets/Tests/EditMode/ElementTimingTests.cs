using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class ElementTimingTests
    {
        private static readonly Edge EdgeA = Edge.Between(new Vector2Int(1, 1), Dir.Left);
        private static readonly Edge EdgeB = Edge.Between(new Vector2Int(1, 1), Dir.Right);

        // ---- Moving wall: dwell A [0,3), slide [3,3.4), dwell B [3.4,6.4), slide back [6.4,6.8)

        [Test]
        public void MovingWall_BlocksA_DuringFirstDwell()
        {
            var w = new MovingWall(EdgeA, EdgeB, 0f);
            Assert.AreEqual(EdgeA, w.BlockingEdgeAt(0f));
            Assert.AreEqual(EdgeA, w.BlockingEdgeAt(2.9f));
            Assert.AreEqual(0f, w.SlideProgressAt(1f), 1e-5f);
        }

        [Test]
        public void MovingWall_SwitchesEdge_AtSlideMidpoint()
        {
            var w = new MovingWall(EdgeA, EdgeB, 0f);
            Assert.AreEqual(EdgeA, w.BlockingEdgeAt(3.19f));
            Assert.AreEqual(EdgeB, w.BlockingEdgeAt(3.21f));
            Assert.AreEqual(EdgeB, w.BlockingEdgeAt(6.59f));
            Assert.AreEqual(EdgeA, w.BlockingEdgeAt(6.61f));
            Assert.AreEqual(EdgeA, w.BlockingEdgeAt(6.8f + 1f), "cycle repeats");
        }

        [Test]
        public void MovingWall_SlideProgressAnimates()
        {
            var w = new MovingWall(EdgeA, EdgeB, 0f);
            Assert.AreEqual(0.5f, w.SlideProgressAt(3.2f), 1e-4f);
            Assert.AreEqual(1f, w.SlideProgressAt(5f), 1e-5f);
            Assert.AreEqual(0.5f, w.SlideProgressAt(6.6f), 1e-4f);
        }

        [Test]
        public void MovingWall_WarnsInLastPointSixOfEachDwell()
        {
            var w = new MovingWall(EdgeA, EdgeB, 0f);
            Assert.IsFalse(w.IsWarningAt(2.3f));
            Assert.IsTrue(w.IsWarningAt(2.5f));
            Assert.IsFalse(w.IsWarningAt(3.1f), "not during the slide");
            Assert.IsFalse(w.IsWarningAt(5.7f));
            Assert.IsTrue(w.IsWarningAt(6.0f));
        }

        [Test]
        public void MovingWall_PhaseShiftsTime()
        {
            var w = new MovingWall(EdgeA, EdgeB, 3.3f);
            Assert.AreEqual(EdgeB, w.BlockingEdgeAt(0f)); // local 3.3 > midpoint 3.2
        }

        // ---- Phase wall: solid [0,3), open [3,5), warning [4.4,5)

        [Test]
        public void PhaseWall_SolidThenOpenWindows()
        {
            var w = new PhaseWall(EdgeA, 0f, false);
            Assert.IsTrue(w.IsSolidAt(0f));
            Assert.IsTrue(w.IsSolidAt(2.99f));
            Assert.IsFalse(w.IsSolidAt(3.01f));
            Assert.IsFalse(w.IsSolidAt(4.99f));
            Assert.IsTrue(w.IsSolidAt(5.01f));
        }

        [Test]
        public void PhaseWall_WarnsInLastPointSixOfOpenWindow()
        {
            var w = new PhaseWall(EdgeA, 0f, false);
            Assert.IsFalse(w.IsWarningAt(1f));
            Assert.IsFalse(w.IsWarningAt(4.3f));
            Assert.IsTrue(w.IsWarningAt(4.5f));
            Assert.IsFalse(w.IsWarningAt(5.1f));
        }

        [Test]
        public void PhaseWall_OpacityFullWhenSolidZeroWhenOpen()
        {
            var w = new PhaseWall(EdgeA, 0f, true);
            Assert.AreEqual(1f, w.OpacityAt(1f), 1e-5f);
            Assert.AreEqual(0f, w.OpacityAt(3.8f), 1e-5f);
            float warn = w.OpacityAt(4.65f);
            Assert.That(warn, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void PhaseWall_PhaseShiftsTime()
        {
            var w = new PhaseWall(EdgeA, 3.5f, false);
            Assert.IsFalse(w.IsSolidAt(0f));
            Assert.IsTrue(w.IsSolidAt(2f));
        }

        // ---- Rotating gate: period 2.5, rotation animates in [2.2, 2.5)

        [Test]
        public void Gate_SwitchesOrientationAtEndOfRotation()
        {
            var g = new RotatingGate(new Vector2Int(2, 2), 0f, true);
            Assert.IsTrue(g.IsHorizontalAt(0f));
            Assert.IsTrue(g.IsHorizontalAt(2.4f), "still horizontal while animating");
            Assert.IsFalse(g.IsHorizontalAt(2.6f));
            Assert.IsFalse(g.IsHorizontalAt(4.9f));
            Assert.IsTrue(g.IsHorizontalAt(5.1f));
        }

        [Test]
        public void Gate_AllowsSidesByOrientation()
        {
            var g = new RotatingGate(new Vector2Int(2, 2), 0f, true);
            Assert.IsTrue(g.Allows(Dir.Left, 1f));
            Assert.IsTrue(g.Allows(Dir.Right, 1f));
            Assert.IsFalse(g.Allows(Dir.Up, 1f));
            Assert.IsFalse(g.Allows(Dir.Down, 1f));
            Assert.IsTrue(g.Allows(Dir.Up, 3f));
            Assert.IsFalse(g.Allows(Dir.Left, 3f));
        }

        [Test]
        public void Gate_AngleAnimatesDuringRotation()
        {
            var g = new RotatingGate(new Vector2Int(2, 2), 0f, true);
            Assert.AreEqual(0f, g.AngleAt(1f), 1e-4f);
            Assert.AreEqual(45f, g.AngleAt(2.35f), 1e-3f);
            Assert.AreEqual(90f, g.AngleAt(3f), 1e-4f);
            var v = new RotatingGate(new Vector2Int(2, 2), 0f, false);
            Assert.IsFalse(v.IsHorizontalAt(0f));
            Assert.AreEqual(90f, v.AngleAt(1f), 1e-4f);
        }

        // ---- Patrol: 0.45 s per cell, ping-pong

        private static readonly List<Vector2Int> Path3 = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0)
        };

        [Test]
        public void Patrol_PingPongsAlongPath()
        {
            var p = new Patrol(Path3, 0f);
            Assert.AreEqual(Path3[0], p.CellAt(0.1f));
            Assert.AreEqual(Path3[1], p.CellAt(0.5f));
            Assert.AreEqual(Path3[2], p.CellAt(1.0f));
            Assert.AreEqual(Path3[1], p.CellAt(1.4f), "coming back");
            Assert.AreEqual(Path3[0], p.CellAt(1.85f), "cycle 1.8 s repeats");
        }

        [Test]
        public void Patrol_OccupiedCellSwitchesAtHalfStep()
        {
            var p = new Patrol(Path3, 0f);
            Assert.AreEqual(Path3[0], p.OccupiedCellAt(0.2f));
            Assert.AreEqual(Path3[1], p.OccupiedCellAt(0.25f));
            // Stepping back from 2 to 1 during [0.9, 1.35)
            Assert.AreEqual(Path3[2], p.OccupiedCellAt(1.1f));
            Assert.AreEqual(Path3[1], p.OccupiedCellAt(1.15f));
        }

        [Test]
        public void Patrol_PositionInterpolates()
        {
            var p = new Patrol(Path3, 0f);
            int n = 4;
            Vector2 mid = (Edge.CellCenter(Path3[0], n) + Edge.CellCenter(Path3[1], n)) / 2f;
            Vector2 pos = p.PositionAt(0.225f, n);
            Assert.AreEqual(mid.x, pos.x, 1e-3f);
            Assert.AreEqual(mid.y, pos.y, 1e-3f);
        }

        [Test]
        public void Patrol_PhaseShiftsTime()
        {
            var p = new Patrol(Path3, 0.9f);
            Assert.AreEqual(Path3[2], p.CellAt(0.1f));
            Assert.AreEqual(1.8f, p.Cycle, 1e-5f);
        }
    }
}
