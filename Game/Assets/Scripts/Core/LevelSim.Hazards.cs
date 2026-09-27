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
        // "jumping" the gap over one normal ChaserStepTime leg like any other.
        private readonly List<Vector2Int> _trail = new List<Vector2Int>();
        private int _chaserFromIndex;
        private float _chaserStepStart;
        private bool _chaserSpawned;

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

                if (_chaserFromIndex >= _trail.Count - 1)
                {
                    return _trail[_chaserFromIndex];
                }

                float progress = Mathf.Clamp01((Time - _chaserStepStart) / ChaserStepTime);
                return progress < 0.5f ? _trail[_chaserFromIndex] : _trail[_chaserFromIndex + 1];
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

                if (_chaserFromIndex >= _trail.Count - 1)
                {
                    return Edge.CellCenter(_trail[_chaserFromIndex], Layout.N);
                }

                float progress = Mathf.Clamp01((Time - _chaserStepStart) / ChaserStepTime);
                return Vector2.Lerp(
                    Edge.CellCenter(_trail[_chaserFromIndex], Layout.N),
                    Edge.CellCenter(_trail[_chaserFromIndex + 1], Layout.N),
                    progress);
            }
        }

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

        /// <summary>Handles arriving on a decoy: reveals it, sends the player to Start, starts the reveal countdown.</summary>
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
            }
        }

        /// <summary>
        /// Appends a newly-entered cell to the chaser's trail, loop-erasing back to it if it is
        /// already present. Clamps the chaser's own position back into the (possibly shortened)
        /// trail if truncation removed the cell it was standing on or heading to.
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

            if (_chaserFromIndex > _trail.Count - 1)
            {
                _chaserFromIndex = _trail.Count - 1;
                _chaserStepStart = Time;
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
                if (Time >= ChaserSpawnAt)
                {
                    _chaserSpawned = true;
                    _chaserFromIndex = 0;
                    _chaserStepStart = Time;
                    _events.Add(new SimEvent(SimEventKind.ChaserSpawned, Layout.Start, string.Empty));
                }

                return;
            }

            // Catch up however many legs the elapsed time covers (usually zero or one at this
            // sub-step granularity), then pin the timer at the trail's current end so waiting for
            // the player to extend it doesn't bank up a burst of steps.
            while (_chaserFromIndex < _trail.Count - 1 && Time - _chaserStepStart >= ChaserStepTime)
            {
                _chaserStepStart += ChaserStepTime;
                _chaserFromIndex++;
            }

            if (_chaserFromIndex >= _trail.Count - 1)
            {
                _chaserStepStart = Time;
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
            _chaserFromIndex = 0;
            _chaserStepStart = 0f;
            _chaserSpawned = false;
        }
    }
}
