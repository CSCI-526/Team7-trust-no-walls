using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    // Cell hazards that need independent actors: collapsing tiles, teleporters, patrols, decoys
    // and the chaser. Split out of LevelSim.cs / LevelSim.Effects.cs (Task 4's seams:
    // OnEnteredCell and CheckHazards) to keep those files focused.
    //
    // Collision granularity: patrol and chaser collisions are checked once per internal sub-step
    // (see LevelSim.MaxSubStepDt), against PlayerOccupiedCell, exactly like CheckHazards already
    // did for Task 4. That is coarser than the per-increment edge crush checks (CheckCrush), which
    // is fine here: cell occupancy (not an edge state) is what matters for these hazards.
    public sealed partial class LevelSim
    {
        public const float ChaserSpawnAt = 5.0f;
        public const float ChaserStepTime = 0.32f;
        public const float DecoyRevealDuration = 2.5f;

        // Defensive cap on how many chaser legs UpdateChaser will catch up on in one call (see the
        // termination comment there). Far larger than any realistic per-frame catch-up.
        private const int MaxCatchUpSteps = 10000;

        // ---- Collapsing tiles ----
        private readonly Dictionary<Vector2Int, float> _crackStart = new Dictionary<Vector2Int, float>();
        private readonly HashSet<Vector2Int> _collapsedTiles = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, float> _restoreAt = new Dictionary<Vector2Int, float>();

        // ---- Teleporter ----
        private bool _teleportPending;
        private Vector2Int _teleportTarget;
        private float _teleportAt;

        // ---- Decoy ----
        private readonly HashSet<Vector2Int> _decoyFound = new HashSet<Vector2Int>();
        private float _decoyRevealUntil;

        // ---- Patrol / chaser collision (swap detection needs the previous sub-step's cells) ----
        private Vector2Int? _prevPlayerOccupied;
        private readonly Dictionary<Patrol, Vector2Int> _prevPatrolOccupied = new Dictionary<Patrol, Vector2Int>();
        private Vector2Int? _prevChaserOccupied;

        // ---- Chaser ----
        // The loop-erased trail of cells the player has entered, starting with Start. Teleports and
        // decoy sends also append their destination cell (see BeginTeleport/TriggerDecoy), so two
        // consecutive trail cells are not guaranteed to be adjacent; the chaser does not path-find
        // that gap; it simply treats the trail as its route and interpolates straight across it,
        // "jumping" the gap over one normal ChaserStepTime leg like any other (this jump rule stays;
        // it is not the same thing as the retrace below).
        //
        // The chaser also keeps its own recorded path: the cells it has actually stood on, in visit
        // order, starting at Start (see _chaserPath). This is NOT the same list as the player's
        // trail above and is never loop-erased by the player's movement. Every tick it checks
        // whether the cell it currently occupies (the last entry of _chaserPath) is still on the
        // live trail: if so it keeps following the trail forward one cell per ChaserStepTime as
        // before; if the player's trail was loop-erased out from under it (a revisit, or a decoy
        // sending the player back to Start), its current cell is no longer on the trail, and it
        // instead walks back along its own recorded path, one cell per ChaserStepTime, until it
        // reaches a cell that is on the live trail again, then resumes following from there. This
        // is a real, regularly-reachable case (a player doubling back, or any decoy after the
        // chaser has moved), not a rare edge case, and it never produces a larger-than-one-cell
        // position jump.
        private readonly List<Vector2Int> _trail = new List<Vector2Int>();
        private readonly List<Vector2Int> _chaserPath = new List<Vector2Int>();
        private float _chaserStepStart;
        private bool _chaserSpawned;
        private bool _chaserWasRetracing;

        // The time the chaser will next spawn at Start. Starts at ChaserSpawnAt each attempt, but a
        // decoy found while Layout.HasChaser despawns an already-spawned chaser and pushes this out
        // to (decoy time + ChaserSpawnAt), restarting the countdown from when the decoy was found
        // rather than from the attempt's start; see TriggerDecoy.
        private float _chaserSpawnAt;

        public bool ChaserActive => _chaserSpawned;

        /// <summary>The cell used for collision purposes: half-step, like <see cref="PlayerOccupiedCell"/>.</summary>
        public Vector2Int ChaserCell
        {
            get
            {
                if (!_chaserSpawned)
                {
                    return Layout.Start;
                }

                Vector2Int current = _chaserPath[_chaserPath.Count - 1];
                if (!ChaserNextCell(out var next))
                {
                    return current;
                }

                float progress = Mathf.Clamp01((Time - _chaserStepStart) / ChaserStepTime);
                return progress < 0.5f ? current : next;
            }
        }

        /// <summary>Interpolated world position, for rendering.</summary>
        public Vector2 ChaserWorldPos
        {
            get
            {
                if (!_chaserSpawned)
                {
                    return Edge.CellCenter(Layout.Start, Layout.N);
                }

                Vector2Int current = _chaserPath[_chaserPath.Count - 1];
                if (!ChaserNextCell(out var next))
                {
                    return Edge.CellCenter(current, Layout.N);
                }

                float progress = Mathf.Clamp01((Time - _chaserStepStart) / ChaserStepTime);
                return Vector2.Lerp(Edge.CellCenter(current, Layout.N), Edge.CellCenter(next, Layout.N), progress);
            }
        }

        /// <summary>
        /// True if the chaser has anywhere to move from its current cell (the last entry of
        /// <see cref="_chaserPath"/>): forward along the live trail if the current cell is on it and
        /// is not yet the trail's last cell, or one cell back along its own recorded path if the
        /// current cell has fallen off the live trail (loop-erased away). Outputs that next cell.
        /// </summary>
        private bool ChaserNextCell(out Vector2Int next)
        {
            Vector2Int current = _chaserPath[_chaserPath.Count - 1];
            int trailIndex = _trail.IndexOf(current);
            if (trailIndex < 0)
            {
                if (_chaserPath.Count > 1)
                {
                    next = _chaserPath[_chaserPath.Count - 2];
                    return true;
                }

                next = current;
                return false;
            }

            if (trailIndex < _trail.Count - 1)
            {
                next = _trail[trailIndex + 1];
                return true;
            }

            next = current;
            return false;
        }

        /// <summary>
        /// Commits the chaser's move to <see cref="ChaserNextCell"/>: pushes the next live-trail
        /// cell onto its recorded path while following, or pops its own last cell while retracing.
        /// </summary>
        private void AdvanceChaserPath()
        {
            Vector2Int current = _chaserPath[_chaserPath.Count - 1];
            if (_trail.IndexOf(current) < 0)
            {
                _chaserPath.RemoveAt(_chaserPath.Count - 1);
            }
            else
            {
                ChaserNextCell(out var next);
                _chaserPath.Add(next);
            }
        }

        /// <summary>The player's live loop-erased trail (read-only view, for the autopilot's chaser prediction).</summary>
        internal IReadOnlyList<Vector2Int> Trail => _trail;

        /// <summary>
        /// The chaser's effective position along <see cref="Trail"/>, for planning: its trail index
        /// while following, or (index of the trail cell it will rejoin) minus (cells left to retrace)
        /// while retracing, so it may be negative. Before spawning it reads 0 with
        /// <paramref name="legStart"/> = <see cref="ChaserSpawnAt"/>. <paramref name="legStart"/> is
        /// the time its current leg began.
        /// </summary>
        internal int ChaserRouteIndex(out float legStart)
        {
            if (!_chaserSpawned)
            {
                legStart = _chaserSpawnAt;
                return 0;
            }

            legStart = _chaserStepStart;
            for (int back = 0; back < _chaserPath.Count; back++)
            {
                int ti = _trail.IndexOf(_chaserPath[_chaserPath.Count - 1 - back]);
                if (ti >= 0)
                {
                    return ti - back;
                }
            }

            return -_chaserPath.Count;
        }

        /// <summary>True if the chaser's current cell (the last entry of <see cref="_chaserPath"/>) has fallen off the live trail.</summary>
        private bool ChaserIsRetracing() => _trail.IndexOf(_chaserPath[_chaserPath.Count - 1]) < 0;

        /// <summary>The state of the collapsing tile at <paramref name="cell"/> (Intact if there is none).</summary>
        public TileState TileStateAt(Vector2Int cell)
        {
            if (_collapsedTiles.Contains(cell))
            {
                return TileState.Collapsed;
            }

            return _crackStart.ContainsKey(cell) ? TileState.Cracking : TileState.Intact;
        }

        /// <summary>0 to 1 progress toward collapse while <see cref="TileStateAt"/> is Cracking; 0 otherwise.</summary>
        public float TileCrackProgress(Vector2Int cell) =>
            _crackStart.TryGetValue(cell, out var start)
                ? Mathf.Clamp01((Time - start) / CollapseTile.CollapseDelay)
                : 0f;

        /// <summary>Seconds until a temporarily-collapsed tile restores; 0 if not collapsed or permanent.</summary>
        public float TileRestoreRemaining(Vector2Int cell)
        {
            if (_restoreAt.TryGetValue(cell, out var at))
            {
                float remaining = at - Time;
                return remaining > 0f ? remaining : 0f;
            }

            return 0f;
        }

        /// <summary>True if the decoy at <paramref name="cell"/> has not yet been found this attempt.</summary>
        public bool IsDecoyActive(Vector2Int cell) => Layout.DecoyAt(cell) != null && !_decoyFound.Contains(cell);

        /// <summary>Seconds left that the real Destination should pulse after a decoy was found (0 otherwise).</summary>
        public float RealDestinationRevealRemaining
        {
            get
            {
                float remaining = _decoyRevealUntil - Time;
                return remaining > 0f ? remaining : 0f;
            }
        }

        /// <summary>Starts a tile's collapse timer on arrival; a no-op if it is already cracking or collapsed.</summary>
        private void StartCrack(Vector2Int cell)
        {
            if (_collapsedTiles.Contains(cell) || _crackStart.ContainsKey(cell))
            {
                return;
            }

            _crackStart[cell] = Time;
            _events.Add(new SimEvent(SimEventKind.TileCracked, cell, string.Empty));
        }

        /// <summary>Begins the teleport delay; the actual move happens in <see cref="UpdateTeleport"/>.</summary>
        private void BeginTeleport(Teleporter teleporter)
        {
            if (_teleportPending)
            {
                return;
            }

            _teleportPending = true;
            _teleportTarget = teleporter.Target;
            _teleportAt = Time + Teleporter.Delay;
        }

        /// <summary>
        /// Moves the player to the pending teleport target once its delay elapses. No arrival
        /// effects run at the target (per design: targets are never pads/plates/tiles anyway), but
        /// the chaser trail still continues from the new cell.
        /// </summary>
        private void UpdateTeleport()
        {
            if (!_teleportPending || Time < _teleportAt)
            {
                return;
            }

            _teleportPending = false;
            PlayerCell = _teleportTarget;
            PlayerOccupiedCell = _teleportTarget;
            IsMoving = false;
            MoveFrom = _teleportTarget;
            MoveTo = _teleportTarget;
            MoveProgress = 0f;
            _events.Add(new SimEvent(SimEventKind.Teleported, _teleportTarget, string.Empty));

            if (Layout.HasChaser)
            {
                AppendToTrail(_teleportTarget);
            }
        }

        /// <summary>
        /// Handles arriving on a decoy: reveals it, sends the player to Start, starts the reveal
        /// countdown. When the chaser is in this level, finding a decoy is also a hard reset for it:
        /// an already-spawned chaser despawns immediately (rather than retracing to hunt down the
        /// player at Start) and its recorded path is cleared back to just Start; either way its
        /// spawn countdown restarts from now, not from the fixed attempt-start ChaserSpawnAt.
        /// </summary>
        private void TriggerDecoy(Vector2Int cell)
        {
            _decoyFound.Add(cell);
            _events.Add(new SimEvent(SimEventKind.DecoyFound, cell, string.Empty));

            PlayerCell = Layout.Start;
            PlayerOccupiedCell = Layout.Start;
            IsMoving = false;
            MoveFrom = Layout.Start;
            MoveTo = Layout.Start;
            MoveProgress = 0f;

            _decoyRevealUntil = Time + DecoyRevealDuration;

            if (Layout.HasChaser)
            {
                AppendToTrail(Layout.Start);

                _chaserSpawned = false;
                _chaserPath.Clear();
                _chaserPath.Add(Layout.Start);
                _chaserWasRetracing = false;
                _prevChaserOccupied = null;
                _chaserSpawnAt = Time + ChaserSpawnAt;
            }
        }

        /// <summary>
        /// Appends a newly-entered cell to the player's trail, loop-erasing back to it if it is
        /// already present. This can leave the chaser's current cell (the end of its own
        /// _chaserPath) off the live trail; that is discovered and handled by <see cref="ChaserNextCell"/>
        /// on the next <see cref="UpdateChaser"/> tick (it starts retracing), not here.
        /// </summary>
        private void AppendToTrail(Vector2Int cell)
        {
            int existing = _trail.IndexOf(cell);
            if (existing >= 0)
            {
                int removeFrom = existing + 1;
                if (removeFrom < _trail.Count)
                {
                    _trail.RemoveRange(removeFrom, _trail.Count - removeFrom);
                }
            }
            else
            {
                _trail.Add(cell);
            }
        }

        /// <summary>Advances the collapse-tile state machine: crack -> collapse (with a fall-through death), collapse -> restore.</summary>
        private void UpdateCollapseTiles()
        {
            foreach (var tile in Layout.CollapseTiles)
            {
                var cell = tile.Cell;
                if (_collapsedTiles.Contains(cell))
                {
                    if (!tile.Permanent && _restoreAt.TryGetValue(cell, out var restoreTime) && Time >= restoreTime)
                    {
                        _collapsedTiles.Remove(cell);
                        _restoreAt.Remove(cell);
                        _events.Add(new SimEvent(SimEventKind.TileRestored, cell, string.Empty));
                    }

                    continue;
                }

                if (_crackStart.TryGetValue(cell, out var startTime) && Time - startTime >= CollapseTile.CollapseDelay)
                {
                    _crackStart.Remove(cell);
                    _collapsedTiles.Add(cell);
                    if (!tile.Permanent)
                    {
                        _restoreAt[cell] = Time + CollapseTile.RestoreDelay;
                    }

                    _events.Add(new SimEvent(SimEventKind.TileCollapsed, cell, string.Empty));

                    if (PlayerOccupiedCell == cell)
                    {
                        Die("Fell through a collapsing tile");
                        return;
                    }
                }
            }
        }

        /// <summary>A collapsed tile is a pit: stepping into it (PlayerOccupiedCell becoming it) kills.</summary>
        private void CheckPitDeath()
        {
            if (_collapsedTiles.Contains(PlayerOccupiedCell))
            {
                Die("Fell into a pit");
            }
        }

        /// <summary>Direct occupied-cell collision, or a swap with the player between consecutive sub-steps.</summary>
        private void CheckPatrolCollision()
        {
            foreach (var patrol in Layout.Patrols)
            {
                var patrolCell = patrol.OccupiedCellAt(Time);
                if (PlayerOccupiedCell == patrolCell)
                {
                    Die("Caught by a patrol");
                    return;
                }

                if (_prevPlayerOccupied.HasValue &&
                    _prevPatrolOccupied.TryGetValue(patrol, out var prevPatrolCell) &&
                    PlayerOccupiedCell == prevPatrolCell && patrolCell == _prevPlayerOccupied.Value)
                {
                    Die("Caught by a patrol");
                    return;
                }

                _prevPatrolOccupied[patrol] = patrolCell;
            }
        }

        /// <summary>Spawns the chaser at Start at <see cref="ChaserSpawnAt"/>, then advances it one trail cell per <see cref="ChaserStepTime"/>.</summary>
        private void UpdateChaser()
        {
            if (!Layout.HasChaser)
            {
                return;
            }

            if (!_chaserSpawned)
            {
                if (Time >= _chaserSpawnAt)
                {
                    _chaserSpawned = true;
                    _chaserPath.Clear();
                    _chaserPath.Add(Layout.Start);
                    _chaserStepStart = Time;
                    _chaserWasRetracing = ChaserIsRetracing();
                    _events.Add(new SimEvent(SimEventKind.ChaserSpawned, Layout.Start, string.Empty));
                }

                return;
            }

            // If the player's trail changed under the chaser's feet mid-leg (a loop-erasure or a
            // decoy send) such that following flips to retracing (or back), the leg's target is no
            // longer meaningful: restart its clock from now, at the current cell, with progress 0.
            // Without this, ChaserCell's half-step preview (below) could keep pointing at the old,
            // now-abandoned target for the rest of the leg, then jump straight to the new target
            // when the leg completes - possibly more than one cell away. Resetting here means any
            // such switch is only ever seen as reverting to the current cell, never a jump.
            bool retracingNow = ChaserIsRetracing();
            if (retracingNow != _chaserWasRetracing)
            {
                _chaserStepStart = Time;
                _chaserWasRetracing = retracingNow;
            }

            // Catch up however many legs the elapsed time covers (usually zero or one at this
            // sub-step granularity): forward along the live trail, or one cell back along its own
            // recorded path if it has fallen off the trail. Then pin the timer at the current time
            // once there is nowhere left to go, so waiting doesn't bank up a burst of steps.
            // Termination: _chaserStepStart strictly increases by ChaserStepTime each iteration
            // while Time (a field, not touched here) is fixed for the whole call, so the loop
            // condition's left side strictly decreases; it cannot run more than
            // (Time - starting _chaserStepStart) / ChaserStepTime times. MaxCatchUpSteps is a
            // defensive cap on top of that proof, in case of a future change to this method.
            int catchUpGuard = 0;
            while (Time - _chaserStepStart >= ChaserStepTime && ChaserNextCell(out _) && catchUpGuard++ < MaxCatchUpSteps)
            {
                _chaserStepStart += ChaserStepTime;
                AdvanceChaserPath();
                _chaserWasRetracing = ChaserIsRetracing();
            }

            if (!ChaserNextCell(out _))
            {
                _chaserStepStart = Time;
                _chaserWasRetracing = ChaserIsRetracing();
            }
        }

        /// <summary>Direct occupied-cell collision, or a swap with the player between consecutive sub-steps.</summary>
        private void CheckChaserCollision()
        {
            if (!_chaserSpawned)
            {
                return;
            }

            var chaserCell = ChaserCell;
            if (PlayerOccupiedCell == chaserCell)
            {
                Die("Caught by the shadow");
                return;
            }

            if (_prevPlayerOccupied.HasValue && _prevChaserOccupied.HasValue &&
                PlayerOccupiedCell == _prevChaserOccupied.Value && chaserCell == _prevPlayerOccupied.Value)
            {
                Die("Caught by the shadow");
                return;
            }

            _prevChaserOccupied = chaserCell;
        }

        /// <summary>Resets all Task 5 hazard state: called by <see cref="Reset"/>.</summary>
        private void ResetHazards()
        {
            _crackStart.Clear();
            _collapsedTiles.Clear();
            _restoreAt.Clear();

            _teleportPending = false;
            _teleportTarget = default;
            _teleportAt = 0f;

            _decoyFound.Clear();
            _decoyRevealUntil = 0f;

            _prevPlayerOccupied = null;
            _prevPatrolOccupied.Clear();
            _prevChaserOccupied = null;

            _trail.Clear();
            _trail.Add(Layout.Start);
            _chaserPath.Clear();
            _chaserPath.Add(Layout.Start);
            _chaserStepStart = 0f;
            _chaserSpawned = false;
            _chaserWasRetracing = false;
            _chaserSpawnAt = ChaserSpawnAt;
        }
    }
}
