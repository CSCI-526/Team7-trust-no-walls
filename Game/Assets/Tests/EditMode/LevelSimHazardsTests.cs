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

        /// <summary>
        /// Steps until sim.Time reaches targetTime, bounded by both a frame guard and Status: since
        /// LevelSim.Step is a no-op once the sim is no longer Playing (Time never advances after
        /// death), a plain "while (sim.Time &lt; targetTime)" loop would spin forever if the sim died
        /// before reaching that time. Always terminates.
        /// </summary>
        private static void StepUntilTime(LevelSim sim, float targetTime, Dir? held, int maxFrames = 2000)
        {
            int i = 0;
            while (sim.Time < targetTime && sim.Status == SimStatus.Playing && i < maxFrames)
            {
                sim.Step(Dt, held);
                i++;
            }
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

        [Test]
        public void Chaser_PlayerLoopsBackPastChaser_RetracesCellByCell()
        {
            // A stem into a small square loop: a -> b -> c1 -> c2 -> c3 -> b. The player walks the
            // loop and pauses at c3, then closes it by re-entering b from c3 (a fresh direction,
            // never walking back over c1/c2, so it never collides with the chaser waiting inside the
            // loop). Every turn here is chosen so continuing straight past it is a wall - see
            // StepUntilCellReached's note on LevelSim's "continue in the held direction" chaining:
            // without that, the player could silently overshoot the intended pause cell.
            // Closing the loop loop-erases c1/c2 out of the live trail while the chaser is still
            // standing on c2, two cells deep - it must retrace c2 -> c1 -> b one cell at a time.
            var a = new Vector2Int(0, 0);
            var b = new Vector2Int(1, 0);
            var c1 = new Vector2Int(2, 0);
            var c2 = new Vector2Int(2, 1);
            var c3 = new Vector2Int(1, 1);
            var builder = new LevelLayoutBuilder(new Maze(4))
                .Carve(a, b, c1, c2, c3, b)
                .WithChaser(true);
            var sim = new LevelSim(builder.Build());

            StepUntilCellReached(sim, Dir.Right, b);
            StepUntilCellReached(sim, Dir.Right, c1);
            StepUntilCellReached(sim, Dir.Up, c2);
            StepUntilCellReached(sim, Dir.Left, c3); // continuing Left from c3 is a wall: no overshoot
            Assert.AreEqual(c3, sim.PlayerCell);

            // Idle until the chaser has advanced through 3 legs (a -> b -> c1 -> c2), landing
            // discretely on c2, two cells into the loop, well before it would reach c3.
            StepUntilTime(sim, LevelSim.ChaserSpawnAt + 3f * LevelSim.ChaserStepTime + 0.03f, null);
            Assert.AreEqual(SimStatus.Playing, sim.Status, "chaser must not have caught the idle player yet");
            Assert.IsTrue(sim.ChaserActive);
            Assert.AreEqual(c2, sim.ChaserCell, "chaser should be two cells into the loop");

            // Close the loop: c3 -> b (a fresh direction; the player was genuinely stationary at c3,
            // so this cannot silently chain any further). This never puts the player on c1/c2, so no
            // collision with the chaser waiting inside the loop.
            var lastChaserCell = sim.ChaserCell;
            int guard = 0;
            while (sim.PlayerCell != b && guard++ < 60)
            {
                sim.Step(Dt, Dir.Down);
                Assert.LessOrEqual(CellDistance(sim.ChaserCell, lastChaserCell), 1, "chaser jumped more than one cell in a step");
                lastChaserCell = sim.ChaserCell;
            }

            Assert.AreEqual(b, sim.PlayerCell);

            // The chaser now retraces c2 -> c1 -> b, one cell per ChaserStepTime, until it catches
            // the stationary player at b - never jumping straight from c2 to b.
            bool sawC1 = false;
            guard = 0;
            while (sim.Status == SimStatus.Playing && guard++ < 400)
            {
                sim.Step(Dt, null);
                var cur = sim.ChaserCell;
                Assert.LessOrEqual(CellDistance(cur, lastChaserCell), 1, "chaser jumped more than one cell in a step");
                if (cur == c1)
                {
                    sawC1 = true;
                }

                lastChaserCell = cur;
            }

            Assert.IsTrue(sawC1, "the chaser must retrace through c1, not snap straight to b");
            Assert.AreEqual(SimStatus.Dead, sim.Status);
            Assert.AreEqual("Caught by the shadow", sim.DeathCause);
        }

        [Test]
        public void Chaser_DecoyDespawnsChaser_AndRespawnsFiveSecondsAfterTheDecoy()
        {
            // RULING: when the level has a chaser, finding a decoy despawns an already-spawned
            // chaser immediately (rather than letting it retrace and hunt the player down at
            // Start) and restarts its 5.0s spawn countdown from the moment the decoy was found,
            // not from the fixed attempt-start ChaserSpawnAt.
            //
            // a -> b -> c -> f -> decoyCell, with a turn at f so continuing the arrival direction
            // (Up) is a wall - the player genuinely stops at f rather than silently chaining on
            // toward the decoy (see the note on StepUntilCellReached / held-direction chaining).
            var a = new Vector2Int(0, 0);
            var b = new Vector2Int(1, 0);
            var c = new Vector2Int(2, 0);
            var f = new Vector2Int(2, 1);
            var decoyCell = new Vector2Int(1, 1);
            var builder = new LevelLayoutBuilder(new Maze(4))
                .Carve(a, b, c, f, decoyCell)
                .Add(new Decoy(decoyCell))
                .WithChaser(true);
            var sim = new LevelSim(builder.Build());

            // Walk to f (one turn short of the decoy) first, so the player is not standing where we
            // freeze the chaser below - it has somewhere left to go and won't reach (and catch) the
            // stationary player during the wait.
            StepUntilCellReached(sim, Dir.Right, b);
            StepUntilCellReached(sim, Dir.Right, c);
            StepUntilCellReached(sim, Dir.Up, f); // continuing Up from f is a wall: no overshoot
            Assert.AreEqual(f, sim.PlayerCell);

            // Idle until the chaser has advanced through 2 legs (a -> b -> c), confirming it is
            // active and away from Start before the decoy is found.
            StepUntilTime(sim, LevelSim.ChaserSpawnAt + 2f * LevelSim.ChaserStepTime + 0.03f, null);
            Assert.AreEqual(SimStatus.Playing, sim.Status, "chaser must not have caught the idle player yet");
            Assert.IsTrue(sim.ChaserActive, "chaser must be active before the decoy is found");
            Assert.AreEqual(c, sim.ChaserCell, "chaser should be two cells from Start, one short of the player");

            // Walk on to the decoy: it sends the player back to Start.
            var events = new List<SimEvent>();
            int guard = 0;
            while (!events.Any(ev => ev.Kind == SimEventKind.DecoyFound) && guard++ < 60)
            {
                events.AddRange(sim.Step(Dt, Dir.Left));
            }

            Assert.IsTrue(events.Any(ev => ev.Kind == SimEventKind.DecoyFound));
            Assert.AreEqual(a, sim.PlayerCell, "the decoy sends the player back to Start");

            // The chaser must be inactive the instant the decoy is found: no retracing hunt.
            Assert.IsFalse(sim.ChaserActive, "the chaser must despawn the instant the decoy is found");
            float decoyTime = sim.Time;

            // It stays despawned right up to just before 5.0s after the decoy has elapsed.
            StepUntilTime(sim, decoyTime + LevelSim.ChaserSpawnAt - 0.05f, null);
            Assert.IsFalse(sim.ChaserActive, "the chaser must not respawn before 5.0s after the decoy");
            Assert.AreEqual(SimStatus.Playing, sim.Status);

            // It respawns at Start once 5.0s after the decoy (not the fixed attempt-start
            // ChaserSpawnAt) have passed.
            var respawnEvents = new List<SimEvent>();
            guard = 0;
            while (!sim.ChaserActive && sim.Status == SimStatus.Playing && guard++ < 20)
            {
                respawnEvents.AddRange(sim.Step(Dt, null));
            }

            Assert.IsTrue(sim.ChaserActive, "the chaser must respawn 5.0s after the decoy");
            Assert.AreEqual(a, sim.ChaserCell, "it respawns at Start");
            Assert.IsTrue(respawnEvents.Any(ev => ev.Kind == SimEventKind.ChaserSpawned));
        }

        private static int CellDistance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

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
