using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class LevelSimTests
    {
        private const float Dt = 1f / 60f;

        private static LevelLayoutBuilder SmallBuilder(int n = 5)
        {
            return new LevelLayoutBuilder(new Maze(n));
        }

        private static IReadOnlyList<SimEvent> StepUntilStopped(LevelSim sim, Dir held, int maxFrames = 300)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < maxFrames && sim.Status == SimStatus.Playing; i++)
            {
                all.AddRange(sim.Step(Dt, held));
            }

            return all;
        }

        /// <summary>
        /// Steps exactly until the player arrives at <paramref name="target"/> (or gives up after
        /// <paramref name="maxFrames"/>), then stops immediately: unlike <see cref="StepUntilStopped"/>
        /// it does not keep calling Step (and so does not keep advancing Time or risk a trailing bump)
        /// once the destination cell is reached. Used where a test checks timing right after arrival.
        /// </summary>
        private static void StepUntilCellReached(LevelSim sim, Dir held, Vector2Int target, int maxFrames = 60)
        {
            int i = 0;
            while (sim.PlayerCell != target && sim.Status == SimStatus.Playing && i < maxFrames)
            {
                sim.Step(Dt, held);
                i++;
            }
        }

        // ---- Movement timing and chaining ----

        [Test]
        public void Step_TakesPointOneThreeSeconds_ThenChainsWhileHeld()
        {
            var builder = SmallBuilder().Carve(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0));
            var sim = new LevelSim(builder.Build());

            IReadOnlyList<SimEvent> lastEvents = null;
            for (int i = 0; i < 8; i++)
            {
                lastEvents = sim.Step(Dt, Dir.Right);
            }

            // After 8 frames of 1/60s (0.1333s > 0.13s), the first step has completed and
            // chained straight into the second, carrying the small overflow as progress.
            Assert.AreEqual(new Vector2Int(1, 0), sim.PlayerCell);
            Assert.IsTrue(sim.IsMoving);
            Assert.AreEqual(new Vector2Int(1, 0), sim.MoveFrom);
            Assert.AreEqual(new Vector2Int(2, 0), sim.MoveTo);
            Assert.AreEqual(0.0256f, sim.MoveProgress, 1e-2f);
            Assert.IsTrue(lastEvents.Any(e => e.Kind == SimEventKind.Step && e.Cell == new Vector2Int(1, 0)));
        }

        [Test]
        public void Step_ProgressAdvancesLinearly_WhileMidStep()
        {
            var builder = SmallBuilder().Carve(new Vector2Int(0, 0), new Vector2Int(1, 0));
            var sim = new LevelSim(builder.Build());

            sim.Step(Dt, Dir.Right);
            Assert.IsTrue(sim.IsMoving);
            Assert.AreEqual(Dt / LevelSim.StepDuration, sim.MoveProgress, 1e-4f);
            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerOccupiedCell, "still in the first half of the step");
        }

        [Test]
        public void PlayerOccupiedCell_SwitchesAtHalfStep()
        {
            var builder = SmallBuilder().Carve(new Vector2Int(0, 0), new Vector2Int(1, 0));
            var sim = new LevelSim(builder.Build());

            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerOccupiedCell);

            for (int i = 0; i < 20 && sim.PlayerOccupiedCell != new Vector2Int(1, 0); i++)
            {
                sim.Step(Dt, Dir.Right);
            }

            Assert.AreEqual(new Vector2Int(1, 0), sim.PlayerOccupiedCell);
        }

        // ---- Bump on a visible wall ----

        [Test]
        public void Bump_OnVisibleWall_NoMovementAndLockout()
        {
            var sim = new LevelSim(SmallBuilder().Build()); // fresh maze: every wall present

            var events = sim.Step(Dt, Dir.Right);

            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Bump && e.Cell == new Vector2Int(0, 0)));
            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerCell);
            Assert.IsFalse(sim.IsMoving);
            Assert.Greater(sim.BumpTimer, 0f);

            // Still locked out: no new bump this frame.
            var again = sim.Step(Dt, Dir.Right);
            Assert.IsFalse(again.Any(e => e.Kind == SimEventKind.Bump));

            // After the lockout elapses, holding the key bumps again.
            var later = new List<SimEvent>();
            for (int i = 0; i < 10; i++)
            {
                later.AddRange(sim.Step(Dt, Dir.Right));
            }

            Assert.IsTrue(later.Any(e => e.Kind == SimEventKind.Bump));
        }

        // ---- Death on an invisible wall ----

        [Test]
        public void Death_OnInvisibleWall_NoMovementAndCauseText()
        {
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            var builder = SmallBuilder().Add(new InvisibleWall(edge));
            var sim = new LevelSim(builder.Build());

            var events = sim.Step(Dt, Dir.Right);

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Walked into an invisible wall", sim.DeathCause);
            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerCell, "no movement on a fatal step");
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Walked into an invisible wall"));
        }

        // ---- One-way edges ----

        [Test]
        public void OneWay_AllowsForwardBlocksBackward()
        {
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(new OneWay(edge, Dir.Right));
            var sim = new LevelSim(builder.Build());

            Assert.IsFalse(sim.IsEdgeBlocked(edge, new Vector2Int(0, 0), Dir.Right));
            Assert.IsTrue(sim.IsEdgeBlocked(edge, new Vector2Int(1, 0), Dir.Left));

            StepUntilCellReached(sim, Dir.Right, new Vector2Int(1, 0));
            Assert.AreEqual(new Vector2Int(1, 0), sim.PlayerCell);
            sim.Step(0.15f, null); // flush the bump lockout left by chaining into the (unopened) far wall

            var events = sim.Step(Dt, Dir.Left);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Bump));
            Assert.AreEqual(new Vector2Int(1, 0), sim.PlayerCell);
        }

        // ---- Rotating gate ----

        [Test]
        public void Gate_Horizontal_BlocksVerticalEntry_AllowsHorizontalEntry()
        {
            var gate = new RotatingGate(new Vector2Int(2, 2), 0f, startHorizontal: true);
            var vertEdge = Edge.Between(new Vector2Int(2, 1), Dir.Up);
            var horizEdge = Edge.Between(new Vector2Int(1, 2), Dir.Right);

            var builder = SmallBuilder()
                .Open(new Vector2Int(2, 1), Dir.Up)
                .Open(new Vector2Int(1, 2), Dir.Right)
                .Add(gate);
            var sim = new LevelSim(builder.Build());

            Assert.IsTrue(sim.IsEdgeBlocked(vertEdge, new Vector2Int(2, 1), Dir.Up), "horizontal gate blocks vertical entry");
            Assert.IsFalse(sim.IsEdgeBlocked(horizEdge, new Vector2Int(1, 2), Dir.Right), "horizontal gate allows horizontal entry");
        }

        [Test]
        public void Gate_Horizontal_BlocksLeavingVertically()
        {
            var gate = new RotatingGate(new Vector2Int(2, 2), 0f, startHorizontal: true);
            var edge = Edge.Between(new Vector2Int(2, 2), Dir.Up);
            var builder = SmallBuilder().Open(new Vector2Int(2, 2), Dir.Up).Add(gate);
            var sim = new LevelSim(builder.Build());

            Assert.IsTrue(sim.IsEdgeBlocked(edge, new Vector2Int(2, 2), Dir.Up));
        }

        [Test]
        public void Gate_Vertical_AllowsVerticalBlocksHorizontal()
        {
            var gate = new RotatingGate(new Vector2Int(2, 2), 0f, startHorizontal: false);
            var vertEdge = Edge.Between(new Vector2Int(2, 1), Dir.Up);
            var horizEdge = Edge.Between(new Vector2Int(1, 2), Dir.Right);
            var builder = SmallBuilder()
                .Open(new Vector2Int(2, 1), Dir.Up)
                .Open(new Vector2Int(1, 2), Dir.Right)
                .Add(gate);
            var sim = new LevelSim(builder.Build());

            Assert.IsFalse(sim.IsEdgeBlocked(vertEdge, new Vector2Int(2, 1), Dir.Up));
            Assert.IsTrue(sim.IsEdgeBlocked(horizEdge, new Vector2Int(1, 2), Dir.Right));
        }

        // ---- Phase wall: blocks solid, passes open ----

        [Test]
        public void PhaseWall_BlocksWhileSolid_PassesWhileOpen()
        {
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            var wall = new PhaseWall(edge, 0f, onBaseWall: false);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(wall);
            var sim = new LevelSim(builder.Build());

            Assert.IsTrue(sim.IsEdgeBlocked(edge, new Vector2Int(0, 0), Dir.Right), "solid at t=0");

            sim.Step(3.5f, null); // jump into the open window
            Assert.IsFalse(sim.IsEdgeBlocked(edge, new Vector2Int(0, 0), Dir.Right), "open at t=3.5");
        }

        // ---- Moving wall block/pass ----

        [Test]
        public void MovingWall_BlocksItsCurrentEdgeOnly()
        {
            var edgeA = Edge.Between(new Vector2Int(1, 1), Dir.Left);
            var edgeB = Edge.Between(new Vector2Int(1, 1), Dir.Right);
            var wall = new MovingWall(edgeA, edgeB, 0f);
            var builder = SmallBuilder()
                .Open(new Vector2Int(1, 1), Dir.Left)
                .Open(new Vector2Int(1, 1), Dir.Right)
                .Add(wall);
            var sim = new LevelSim(builder.Build());

            Assert.IsTrue(sim.IsEdgeBlocked(edgeA, new Vector2Int(1, 1), Dir.Left), "A blocked during first dwell");
            Assert.IsFalse(sim.IsEdgeBlocked(edgeB, new Vector2Int(1, 1), Dir.Right), "B open during first dwell");

            sim.Step(3.3f, null); // past the slide midpoint (3.2s)
            Assert.IsFalse(sim.IsEdgeBlocked(edgeA, new Vector2Int(1, 1), Dir.Left));
            Assert.IsTrue(sim.IsEdgeBlocked(edgeB, new Vector2Int(1, 1), Dir.Right));
        }

        // ---- Crush by a moving wall ----

        [Test]
        public void Crush_ByMovingWall_WhileCrossing()
        {
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            // Phase chosen so the edge is open at t = 0 and becomes blocked ~0.05s later,
            // partway through a 0.13s step.
            var wall = new MovingWall(edge, Edge.Between(new Vector2Int(0, 0), Dir.Up), 6.55f);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(wall);
            var sim = new LevelSim(builder.Build());

            var events = StepUntilStopped(sim, Dir.Right, 30);

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Crushed by a moving wall", sim.DeathCause);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Crushed by a moving wall"));
        }

        // ---- Crush by a disappearing (phase) wall ----

        [Test]
        public void Crush_ByPhaseWall_WhileCrossing()
        {
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            // Phase chosen so the edge is open at t = 0 and turns solid ~0.05s later.
            var wall = new PhaseWall(edge, 4.95f, onBaseWall: false);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(wall);
            var sim = new LevelSim(builder.Build());

            var events = StepUntilStopped(sim, Dir.Right, 30);

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Crushed by a disappearing wall", sim.DeathCause);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Crushed by a disappearing wall"));
        }

        // ---- Trigger toggle and crush ----

        [Test]
        public void Trigger_TogglesLinkedEdges_OnPlateArrival()
        {
            var opensEdge = Edge.Between(new Vector2Int(0, 0), Dir.Right); // starts closed
            var closesEdge = Edge.Between(new Vector2Int(0, 1), Dir.Right); // starts open
            var plate = new TriggerPlate(new Vector2Int(2, 2),
                new List<Edge> { opensEdge }, new List<Edge> { closesEdge }, colorIndex: 0);

            var builder = SmallBuilder()
                .WithStart(new Vector2Int(2, 1))
                .Open(new Vector2Int(2, 1), Dir.Up)
                .Add(plate);
            var sim = new LevelSim(builder.Build());

            Assert.IsFalse(sim.IsTriggerEdgeOpen(opensEdge));
            Assert.IsTrue(sim.IsTriggerEdgeOpen(closesEdge));

            var events = StepUntilStopped(sim, Dir.Up, 10);

            Assert.AreEqual(new Vector2Int(2, 2), sim.PlayerCell);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.TriggerToggled && e.Cell == new Vector2Int(2, 2)));
            Assert.IsTrue(sim.IsTriggerEdgeOpen(opensEdge), "opened by the press");
            Assert.IsFalse(sim.IsTriggerEdgeOpen(closesEdge), "closed by the press");
        }

        [Test]
        public void Crush_ByTriggerWall_WhileCrossing()
        {
            // A trigger crush cannot arise from normal play in this task's scope (only the player
            // toggles plates, and doing so requires arriving at the plate, never mid-crossing a
            // linked edge). We exercise the crush-detection code path directly via the test hook.
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            var plate = new TriggerPlate(new Vector2Int(4, 4), new List<Edge>(), new List<Edge> { edge }, 0);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(plate);
            var sim = new LevelSim(builder.Build());

            sim.Step(Dt, Dir.Right);
            Assert.IsTrue(sim.IsMoving);

            sim.DebugSetTriggerEdgeOpenForTest(edge, false);
            var events = sim.Step(Dt, null);

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Crushed by a trigger wall", sim.DeathCause);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Crushed by a trigger wall"));
        }

        // ---- Memory reveal timing and radius ----

        [Test]
        public void MemoryTile_RevealsNearbyInvisibleWalls_ForItsDuration()
        {
            var tileCell = new Vector2Int(4, 4);
            var nearEdge = Edge.Between(new Vector2Int(4, 1), Dir.Up); // Chebyshev 2 from the tile
            var farEdge = Edge.Between(new Vector2Int(7, 8), Dir.Left); // Chebyshev 4 from the tile

            var builder = new LevelLayoutBuilder(new Maze(9))
                .WithStart(new Vector2Int(4, 3))
                .Open(new Vector2Int(4, 3), Dir.Up)
                .Add(new MemoryTile(tileCell))
                .Add(new InvisibleWall(nearEdge))
                .Add(new InvisibleWall(farEdge));
            var sim = new LevelSim(builder.Build());

            Assert.AreEqual(0f, sim.MemoryRevealRemaining(nearEdge));

            StepUntilCellReached(sim, Dir.Up, tileCell);
            Assert.AreEqual(new Vector2Int(4, 4), sim.PlayerCell);

            Assert.AreEqual(3.0f, sim.MemoryRevealRemaining(nearEdge), 0.1f);
            Assert.AreEqual(0f, sim.MemoryRevealRemaining(farEdge), "outside the reveal radius");

            sim.Step(1.5f, null);
            Assert.AreEqual(1.5f, sim.MemoryRevealRemaining(nearEdge), 0.1f);

            sim.Step(2f, null); // total > 3.0s since reveal
            Assert.AreEqual(0f, sim.MemoryRevealRemaining(nearEdge), "faded out");

            // Leave and re-enter: refreshes the reveal.
            StepUntilCellReached(sim, Dir.Down, new Vector2Int(4, 3));
            Assert.AreEqual(new Vector2Int(4, 3), sim.PlayerCell);
            StepUntilCellReached(sim, Dir.Up, tileCell);
            Assert.AreEqual(new Vector2Int(4, 4), sim.PlayerCell);
            Assert.AreEqual(3.0f, sim.MemoryRevealRemaining(nearEdge), 0.1f);
        }

        // ---- Warnings ----

        [Test]
        public void Warning_FiresOncePerWindow_OnlyWithinRadius()
        {
            var nearEdge = Edge.Between(new Vector2Int(1, 0), Dir.Right); // near Start (0,0)
            var farEdge = Edge.Between(new Vector2Int(6, 7), Dir.Left); // far from Start (Chebyshev 6)
            var nearWall = new MovingWall(nearEdge, Edge.Between(new Vector2Int(1, 0), Dir.Up), 0f);
            var farWall = new MovingWall(farEdge, Edge.Between(new Vector2Int(6, 6), Dir.Up), 0f);

            var builder = SmallBuilder(8).Add(nearWall).Add(farWall);
            var sim = new LevelSim(builder.Build());

            // Jump straight into the first dwell's warning window (last 0.6s of a 3.0s dwell).
            var events = sim.Step(2.5f, null);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Warning), "near wall should warn");
            Assert.AreEqual(1, events.Count(e => e.Kind == SimEventKind.Warning), "only the near wall is in range");

            // Still within the same window: no repeat.
            var again = sim.Step(0.1f, null);
            Assert.IsFalse(again.Any(e => e.Kind == SimEventKind.Warning));

            // Leave the window, then re-enter the next cycle's warning window: fires again.
            sim.Step(1f, null); // t = 3.6, well past the first window (ends at 3.0)
            var nextWindow = sim.Step(2.75f, null); // t = 6.35, within [Dwell+Slide+... ] second dwell's warning band
            Assert.IsTrue(nextWindow.Any(e => e.Kind == SimEventKind.Warning));
        }

        // ---- Completion ----

        [Test]
        public void Reaching_Destination_Completes()
        {
            var builder = SmallBuilder()
                .WithDestination(new Vector2Int(1, 0))
                .Open(new Vector2Int(0, 0), Dir.Right);
            var sim = new LevelSim(builder.Build());

            var events = StepUntilStopped(sim, Dir.Right, 10);

            Assert.AreEqual(SimStatus.Complete, sim.Status);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Completed && e.Cell == new Vector2Int(1, 0)));
        }

        // ---- Reset ----

        [Test]
        public void Reset_RestoresEverything()
        {
            var opensEdge = Edge.Between(new Vector2Int(3, 3), Dir.Right);
            var plate = new TriggerPlate(new Vector2Int(2, 1), new List<Edge> { opensEdge }, new List<Edge>(), 0);
            var builder = SmallBuilder()
                .Open(new Vector2Int(0, 0), Dir.Right)
                .Add(plate);
            var sim = new LevelSim(builder.Build());

            sim.Step(Dt, Dir.Right); // start moving
            Assert.IsTrue(sim.IsMoving);
            Assert.Greater(sim.Time, 0f);

            sim.Reset();

            Assert.AreEqual(0f, sim.Time);
            Assert.AreEqual(SimStatus.Playing, sim.Status);
            Assert.IsNull(sim.DeathCause);
            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerCell);
            Assert.IsFalse(sim.IsMoving);
            Assert.AreEqual(0f, sim.BumpTimer);
            Assert.IsFalse(sim.IsTriggerEdgeOpen(opensEdge), "trigger state back to its initial closed state");
        }
    }
}
