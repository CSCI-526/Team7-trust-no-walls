using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    /// <summary>
    /// Task 5: collapsing tiles, teleporters, patrols, decoys and the chaser.
    /// </summary>
    public class LevelSimHazardsTests
    {
        private const float Dt = 1f / 60f;

        private static LevelLayoutBuilder SmallBuilder(int n = 5)
        {
            return new LevelLayoutBuilder(new Maze(n));
        }

        private static void StepUntilCellReached(LevelSim sim, Dir held, Vector2Int target, int maxFrames = 60)
        {
            int i = 0;
            while (sim.PlayerCell != target && sim.Status == SimStatus.Playing && i < maxFrames)
            {
                sim.Step(Dt, held);
                i++;
            }
        }

        private static List<SimEvent> RunUntilStopped(LevelSim sim, Dir? held, int maxFrames)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < maxFrames && sim.Status == SimStatus.Playing; i++)
            {
                all.AddRange(sim.Step(Dt, held));
            }

            return all;
        }

        // ---- Collapsing tiles ----

        [Test]
        public void Collapse_CracksOnEntry_ThenKillsIfPlayerStillOnItAtCollapseTime()
        {
            var cell = new Vector2Int(1, 0);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(new CollapseTile(cell, permanent: false));
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, cell);
            Assert.AreEqual(cell, sim.PlayerCell);
            Assert.AreEqual(TileState.Cracking, sim.TileStateAt(cell));

            sim.Step(Dt, null);
            Assert.Greater(sim.TileCrackProgress(cell), 0f);

            var events = RunUntilStopped(sim, null, 80); // 0.8s collapse delay, player never leaves

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Fell through a collapsing tile", sim.DeathCause);
            // (TileCracked already fired back when the tile was entered, above.)
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.TileCollapsed && e.Cell == cell));
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Fell through a collapsing tile"));
            Assert.AreEqual(TileState.Collapsed, sim.TileStateAt(cell));
        }

        [Test]
        public void Collapse_TemporaryTile_CollapsesSilentlyThenRestores_IfPlayerHasMovedOn()
        {
            var tileCell = new Vector2Int(1, 0);
            var builder = SmallBuilder()
                .Carve(new Vector2Int(0, 0), tileCell, new Vector2Int(2, 0))
                .Add(new CollapseTile(tileCell, permanent: false));
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, new Vector2Int(2, 0), maxFrames: 30);
            Assert.AreEqual(new Vector2Int(2, 0), sim.PlayerCell, "player walked straight through the tile");
            Assert.AreEqual(TileState.Cracking, sim.TileStateAt(tileCell));

            var events = RunUntilStopped(sim, null, 60); // past the 0.8s collapse delay, player safe at (2,0)
            Assert.AreEqual(SimStatus.Playing, sim.Status, "collapsing while unoccupied must not kill");
            Assert.AreEqual(TileState.Collapsed, sim.TileStateAt(tileCell));
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.TileCollapsed && e.Cell == tileCell));
            Assert.Greater(sim.TileRestoreRemaining(tileCell), 0f);

            events.AddRange(RunUntilStopped(sim, null, 300)); // past the 4.0s restore delay
            Assert.AreEqual(TileState.Intact, sim.TileStateAt(tileCell));
            Assert.AreEqual(0f, sim.TileRestoreRemaining(tileCell));
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.TileRestored && e.Cell == tileCell));
        }

        [Test]
        public void Collapse_SteppingIntoAnAlreadyCollapsedTile_FallsIntoAPit()
        {
            var pitCell = new Vector2Int(1, 0);
            var builder = SmallBuilder()
                .Carve(new Vector2Int(0, 0), pitCell, new Vector2Int(2, 0))
                .Add(new CollapseTile(pitCell, permanent: true));
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, new Vector2Int(2, 0), maxFrames: 30);
            RunUntilStopped(sim, null, 60); // let it collapse while the player is safely at (2, 0)
            Assert.AreEqual(SimStatus.Playing, sim.Status);
            Assert.AreEqual(TileState.Collapsed, sim.TileStateAt(pitCell));

            var events = RunUntilStopped(sim, Dir.Left, 20); // walk back into the pit

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Fell into a pit", sim.DeathCause);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Fell into a pit"));
        }

        // ---- Teleporters ----

        [Test]
        public void Teleporter_FreezesInputDuringDelay_ThenMovesToTarget()
        {
            var pad = new Vector2Int(1, 0);
            var target = new Vector2Int(3, 0);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(new Teleporter(pad, target));
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, pad);
            Assert.AreEqual(pad, sim.PlayerCell);

            sim.Step(Dt, Dir.Right); // well within the 0.15s delay
            Assert.AreEqual(pad, sim.PlayerCell, "input is frozen during the teleport delay");
            Assert.IsFalse(sim.IsMoving);

            var events = RunUntilStopped(sim, Dir.Right, 20);

            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Teleported && e.Cell == target));
            Assert.AreEqual(target, sim.PlayerCell);
            Assert.AreEqual(target, sim.PlayerOccupiedCell);
        }

        // ---- Patrols ----

        [Test]
        public void Patrol_DirectCollision_Kills()
        {
            var patrolCell = new Vector2Int(1, 0);
            var patrol = new Patrol(new[] { patrolCell }, 0f);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(patrol);
            var sim = new LevelSim(builder.Build());

            var events = RunUntilStopped(sim, Dir.Right, 30);

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Caught by a patrol", sim.DeathCause);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Caught by a patrol"));
        }

        [Test]
        public void Patrol_SwapCollision_KillsEvenWithoutSharingACell()
        {
            var cellA = new Vector2Int(1, 0);
            var cellB = new Vector2Int(2, 0);
            // Patrol steps B -> A; timed so its half-step lands the same instant the player's does.
            var patrol = new Patrol(new List<Vector2Int> { cellB, cellA }, phase: 0.155f);
            var builder = SmallBuilder().WithStart(cellA).Open(cellA, Dir.Right).Add(patrol);
            var sim = new LevelSim(builder.Build());

            var events = sim.Step(0.08f, Dir.Right);

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Caught by a patrol", sim.DeathCause);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Caught by a patrol"));
        }

        // ---- Decoys ----

        [Test]
        public void Decoy_ArrivalSendsPlayerToStart_AndStartsRevealCountdown()
        {
            var decoyCell = new Vector2Int(1, 0);
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).Add(new Decoy(decoyCell));
            var sim = new LevelSim(builder.Build());

            Assert.IsTrue(sim.IsDecoyActive(decoyCell));

            var events = new List<SimEvent>();
            for (int i = 0; i < 15 && sim.Status == SimStatus.Playing; i++)
            {
                events.AddRange(sim.Step(Dt, Dir.Right));
                if (events.Any(e => e.Kind == SimEventKind.DecoyFound))
                {
                    break;
                }
            }

            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.DecoyFound && e.Cell == decoyCell));
            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerCell);
            Assert.IsFalse(sim.IsDecoyActive(decoyCell), "the decoy is spent for this attempt");
            Assert.AreEqual(2.5f, sim.RealDestinationRevealRemaining, 0.05f);
            Assert.AreEqual(SimStatus.Playing, sim.Status);

            sim.Reset();
            Assert.IsTrue(sim.IsDecoyActive(decoyCell), "a fresh attempt restores the decoy");
        }

        // ---- Chaser ----

        [Test]
        public void Chaser_SpawnsAtFiveSeconds_AndCatchesPlayerWaitingAtStart()
        {
            var builder = SmallBuilder().WithChaser(true);
            var sim = new LevelSim(builder.Build());

            var events = sim.Step(LevelSim.ChaserSpawnAt + 0.1f, null);

            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.ChaserSpawned));
            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Caught by the shadow", sim.DeathCause);
            Assert.IsTrue(events.Any(e => e.Kind == SimEventKind.Died && e.Text == "Caught by the shadow"));
        }

        [Test]
        public void Chaser_FollowsLoopErasedTrail_NeverRevisitsAnErasedCell()
        {
            // A right-angle path where every arrival's "continue straight" direction is either a
            // dead end or exactly the next intended move, so LevelSim's automatic step-chaining
            // (still holding the key when a step completes) never runs the player past a waypoint.
            var a = new Vector2Int(2, 2);
            var b = new Vector2Int(3, 2); // right of a
            var c = new Vector2Int(3, 3); // up from b
            var d = new Vector2Int(3, 1); // down from b: same direction as the b-revisit below
            var builder = SmallBuilder()
                .WithStart(a)
                .Open(a, Dir.Right) // a -> b
                .Open(b, Dir.Up) // b -> c
                .Open(b, Dir.Down) // b -> d
                .WithChaser(true);
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, b);
            StepUntilCellReached(sim, Dir.Up, c);
            StepUntilCellReached(sim, Dir.Down, b); // revisits b: loop-erases c out of the trail
            StepUntilCellReached(sim, Dir.Down, d);
            Assert.AreEqual(d, sim.PlayerCell);

            var visitedByChaser = new HashSet<Vector2Int>();
            int guard = 0;
            while (sim.Status == SimStatus.Playing && guard++ < 2000)
            {
                sim.Step(Dt, null);
                if (sim.ChaserActive)
                {
                    visitedByChaser.Add(sim.ChaserCell);
                }
            }

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Caught by the shadow", sim.DeathCause);
            Assert.IsFalse(visitedByChaser.Contains(c), "the loop-erased cell must never be on the chaser's route");
        }

        [Test]
        public void Chaser_JumpsNonAdjacentTrailGap_AfterATeleport()
        {
            var pad = new Vector2Int(1, 0);
            var target = new Vector2Int(4, 4);
            var builder = new LevelLayoutBuilder(new Maze(6))
                .Open(new Vector2Int(0, 0), Dir.Right)
                .Add(new Teleporter(pad, target))
                .WithChaser(true);
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, pad);
            RunUntilStopped(sim, null, 20); // let the teleport resolve
            Assert.AreEqual(target, sim.PlayerCell);

            int guard = 0;
            while (sim.Status == SimStatus.Playing && guard++ < 3000)
            {
                sim.Step(Dt, null);
            }

            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Caught by the shadow", sim.DeathCause);
        }

        // ---- Reset ----

        [Test]
        public void Reset_ClearsCollapseAndTeleportPendingState()
        {
            var crackCell = new Vector2Int(1, 0);
            var padCell = new Vector2Int(2, 0);
            var target = new Vector2Int(4, 0);
            var builder = SmallBuilder()
                .Carve(new Vector2Int(0, 0), crackCell, padCell)
                .Add(new CollapseTile(crackCell, permanent: true))
                .Add(new Teleporter(padCell, target));
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, padCell, maxFrames: 30);
            Assert.AreEqual(padCell, sim.PlayerCell);
            Assert.AreEqual(TileState.Cracking, sim.TileStateAt(crackCell));

            sim.Step(Dt, Dir.Right); // still inside the teleport delay
            Assert.AreEqual(padCell, sim.PlayerCell, "teleport delay not yet elapsed");

            sim.Reset();

            Assert.AreEqual(SimStatus.Playing, sim.Status);
            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerCell);
            Assert.AreEqual(TileState.Intact, sim.TileStateAt(crackCell));

            sim.Step(Dt, Dir.Right); // no longer frozen by a pending teleport
            Assert.IsTrue(sim.IsMoving);
        }

        [Test]
        public void Reset_ClearsChaserState()
        {
            var builder = SmallBuilder().Open(new Vector2Int(0, 0), Dir.Right).WithChaser(true);
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, new Vector2Int(1, 0));
            sim.Step(LevelSim.ChaserSpawnAt - sim.Time + 0.02f, null); // just past spawn, well under one leg
            Assert.IsTrue(sim.ChaserActive);

            sim.Reset();

            Assert.IsFalse(sim.ChaserActive);
            Assert.AreEqual(new Vector2Int(0, 0), sim.PlayerCell);
        }
    }
}
