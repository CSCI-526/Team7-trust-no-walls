using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// The pure-C# simulation of one level attempt: grid-step movement plus the wall and edge
    /// mechanics (visible/invisible walls, one-way edges, moving walls, phase walls, trigger
    /// walls, rotating gates, memory tiles) and completion. Drive it with fixed-size <see cref="Step"/>
    /// calls; the Unity layer renders whatever state it exposes and reads/replays its events.
    ///
    /// Cell hazards that need independent actors (collapsing tiles, teleporters, patrols, decoys,
    /// the chaser) are Task 5's job; <see cref="OnEnteredCell"/> and <see cref="CheckHazards"/> are
    /// the seams left for them.
    /// </summary>
    public sealed partial class LevelSim
    {
        /// <summary>Seconds for one grid step to complete.</summary>
        public const float StepDuration = 0.13f;

        /// <summary>Seconds a bump locks out further step attempts.</summary>
        public const float BumpLockout = 0.12f;

        /// <summary>Chebyshev radius (in cells) within which a warning event is raised.</summary>
        public const int WarningRadius = 4;

        public LevelLayout Layout { get; }

        public float Time { get; private set; }
        public SimStatus Status { get; private set; }
        public string DeathCause { get; private set; }

        /// <summary>The cell the player has fully arrived in (updates only when a step completes).</summary>
        public Vector2Int PlayerCell { get; private set; }

        public bool IsMoving { get; private set; }
        public Vector2Int MoveFrom { get; private set; }
        public Vector2Int MoveTo { get; private set; }
        public float MoveProgress { get; private set; }
        public Dir Facing { get; private set; }

        /// <summary>The cell used for collision purposes: <see cref="MoveFrom"/> for the first half of a step, <see cref="MoveTo"/> for the second half.</summary>
        public Vector2Int PlayerOccupiedCell { get; private set; }

        public float BumpTimer { get; private set; }

        private Edge _moveEdge;
        private Dir _moveDir;

        private readonly List<SimEvent> _events = new List<SimEvent>();
        private readonly Dictionary<Edge, bool> _triggerOpen = new Dictionary<Edge, bool>();
        private readonly Dictionary<Edge, float> _memoryExpiry = new Dictionary<Edge, float>();
        private readonly Dictionary<object, bool> _warned = new Dictionary<object, bool>();

        public LevelSim(LevelLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Reset();
        }

        /// <summary>Restarts the attempt on the same layout: t = 0, player back at Start, all dynamic state reset.</summary>
        public void Reset()
        {
            Time = 0f;
            Status = SimStatus.Playing;
            DeathCause = null;
            PlayerCell = Layout.Start;
            PlayerOccupiedCell = Layout.Start;
            IsMoving = false;
            MoveFrom = Layout.Start;
            MoveTo = Layout.Start;
            MoveProgress = 0f;
            _moveEdge = default;
            _moveDir = Dir.Up;
            Facing = Dir.Up;
            BumpTimer = 0f;

            _events.Clear();
            _warned.Clear();
            _memoryExpiry.Clear();

            _triggerOpen.Clear();
            foreach (var plate in Layout.TriggerPlates)
            {
                foreach (var e in plate.Opens)
                {
                    _triggerOpen[e] = false;
                }

                foreach (var e in plate.Closes)
                {
                    _triggerOpen[e] = true;
                }
            }
        }

        /// <summary>The player's current interpolated world position (idle: cell center; moving: lerp from -> to).</summary>
        public Vector2 PlayerWorldPos =>
            IsMoving
                ? Vector2.Lerp(Edge.CellCenter(MoveFrom, Layout.N), Edge.CellCenter(MoveTo, Layout.N), MoveProgress)
                : Edge.CellCenter(PlayerCell, Layout.N);

        /// <summary>
        /// Advances the simulation by <paramref name="dt"/> seconds. <paramref name="held"/> is the
        /// currently-held movement direction (the caller resolves which key wins), or null. Returns
        /// the events raised this call; the list is cleared and reused each call. No-ops (returns an
        /// empty list) unless <see cref="Status"/> is <see cref="SimStatus.Playing"/>.
        /// </summary>
        public IReadOnlyList<SimEvent> Step(float dt, Dir? held)
        {
            _events.Clear();
            if (Status != SimStatus.Playing)
            {
                return _events;
            }

            Time += dt;
            ProcessMovement(dt, held);

            if (Status == SimStatus.Playing)
            {
                CheckHazards();
                UpdateWarnings();
            }

            return _events;
        }

        private void ProcessMovement(float dt, Dir? held)
        {
            if (BumpTimer > 0f)
            {
                BumpTimer = Mathf.Max(0f, BumpTimer - dt);
            }

            float leftover = dt;
            int guard = 0;
            while (leftover > 1e-9f && Status == SimStatus.Playing && guard++ < 100000)
            {
                if (!IsMoving)
                {
                    if (!held.HasValue || BumpTimer > 0f)
                    {
                        break;
                    }

                    TryStartStep(held.Value);
                    if (!IsMoving)
                    {
                        break; // bumped or died: no movement left to apply this frame
                    }
                }
                else
                {
                    float remaining = (1f - MoveProgress) * StepDuration;
                    float use = Mathf.Min(leftover, remaining);
                    MoveProgress += use / StepDuration;
                    leftover -= use;
                    PlayerOccupiedCell = MoveProgress < 0.5f ? MoveFrom : MoveTo;

                    if (MoveProgress >= 1f - 1e-6f)
                    {
                        CompleteStep();
                        if (Status != SimStatus.Playing)
                        {
                            break;
                        }
                    }
                    else
                    {
                        CheckCrush();
                        if (Status != SimStatus.Playing)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private void TryStartStep(Dir dir)
        {
            Vector2Int from = PlayerCell;
            Vector2Int to = from + DirUtil.Delta(dir);
            Edge e = Edge.Between(from, dir);

            if (Layout.IsInvisible(e))
            {
                Die("Walked into an invisible wall");
                return;
            }

            if (IsEdgeBlocked(e, from, dir))
            {
                Bump(from);
                return;
            }

            IsMoving = true;
            MoveFrom = from;
            MoveTo = to;
            _moveEdge = e;
            _moveDir = dir;
            MoveProgress = 0f;
            Facing = dir;
            PlayerOccupiedCell = from;
        }

        /// <summary>
        /// True if a step from <paramref name="from"/> in direction <paramref name="d"/> across edge
        /// <paramref name="e"/> is currently blocked: a static wall (visible or invisible), a gate
        /// side that disallows it, a one-way edge in the wrong direction, a moving wall on the edge,
        /// a solid phase wall, or a closed trigger edge. Does not distinguish a fatal invisible wall
        /// from an ordinary bump; used both to decide bumps and by the autopilot for planning.
        /// </summary>
        public bool IsEdgeBlocked(Edge e, Vector2Int from, Dir d)
        {
            Vector2Int to = from + DirUtil.Delta(d);
            if (!Layout.Maze.InBounds(to))
            {
                return true;
            }

            if (Layout.Maze.HasWall(e))
            {
                return true;
            }

            var gateFrom = Layout.GateAt(from);
            if (gateFrom != null && !gateFrom.Allows(d, Time))
            {
                return true;
            }

            var gateTo = Layout.GateAt(to);
            if (gateTo != null && !gateTo.Allows(DirUtil.Opposite(d), Time))
            {
                return true;
            }

            var ow = Layout.OneWayAt(e);
            if (ow != null && !ow.AllowsStep(d))
            {
                return true;
            }

            var mw = Layout.MovingWallAt(e);
            if (mw != null && mw.BlockingEdgeAt(Time) == e)
            {
                return true;
            }

            var pw = Layout.PhaseWallAt(e);
            if (pw != null && pw.IsSolidAt(Time))
            {
                return true;
            }

            var tp = Layout.TriggerFor(e);
            if (tp != null && !IsTriggerEdgeOpen(e))
            {
                return true;
            }

            return false;
        }

        private void CompleteStep()
        {
            PlayerCell = MoveTo;
            PlayerOccupiedCell = MoveTo;
            IsMoving = false;
            MoveFrom = MoveTo;
            MoveProgress = 0f;
            _events.Add(new SimEvent(SimEventKind.Step, PlayerCell, string.Empty));
            OnEnteredCell(PlayerCell);
        }

        private void Bump(Vector2Int cell)
        {
            BumpTimer = BumpLockout;
            _events.Add(new SimEvent(SimEventKind.Bump, cell, string.Empty));
        }

        private void Die(string cause)
        {
            Status = SimStatus.Dead;
            DeathCause = cause;
            IsMoving = false;
            _events.Add(new SimEvent(SimEventKind.Died, PlayerOccupiedCell, cause));
        }
    }
}
