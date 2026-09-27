using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// A planner that plays a <see cref="LevelSim"/> by returning the direction to hold each frame.
    /// It knows the full layout (including invisible walls) and plans with a time-expanded A* over
    /// states (cell, step, trigger parity) where one step is <see cref="LevelSim.StepDuration"/>
    /// and the actions are "wait one step" or "move one cell". Element timing functions predict
    /// every dynamic edge and hazard at future times; each check is sampled over the action's
    /// window widened by a safety margin, so it never starts a crossing a wall could close mid-step.
    ///
    /// Execution: the plan is a list of cells per step (t0 + k * StepDuration). While moving it
    /// returns the next planned direction only if that move should start right at arrival (so the
    /// sim chains it with no gap); while idle it waits until the next move's scheduled time. It
    /// replans (only while idle) when the plan is exhausted, when the player is off-plan, or when
    /// execution has drifted late. Imperfect by design: it is used for the end-to-end tests and the
    /// demo, and deaths just trigger a fresh plan after the sim is reset.
    /// </summary>
    public sealed class Autopilot
    {
        private const float D = LevelSim.StepDuration;
        private const float BaseMargin = 0.05f;
        private const float BasePatrolMargin = 0.025f; // patrol cells are free for 0.45 s windows; stay tight
        private const float SampleDt = 0.01f;
        private const float LateTolerance = 0.035f;
        private const float EarlyTolerance = 0.004f;
        private const int MaxSteps = 400;
        private const int MaxExpansions = 40000;
        private const int BaseChaserLead = 0;
        private const int MaxCaution = 3;
        private const int MaxPlates = 8;

        // ---- Current plan ----
        private LevelSim _sim;
        private float _lastTime;
        private readonly List<Vector2Int> _cells = new List<Vector2Int>();
        private readonly List<bool> _jump = new List<bool>(); // transition k -> k+1 is a teleport
        private bool _hasPlan;
        private bool _planReachesGoal;
        private float _t0;
        private int _idx;
        private float _retryAt;

        // Caution grows with each death on the same layout, so a repeated plan does not repeat the death.
        private LevelLayout _cautionLayout;
        private int _deathsOnLayout;
        private float _margin = BaseMargin;
        private float _patrolMargin = BasePatrolMargin;
        private int _chaserLead = BaseChaserLead;

        // ---- Per-plan search context ----
        private LevelLayout _layout;
        private int _nn;
        private int[] _h;
        private readonly Dictionary<TriggerPlate, int> _plateBit = new Dictionary<TriggerPlate, int>();
        private readonly Dictionary<Edge, bool> _triggerBase = new Dictionary<Edge, bool>();
        private readonly Dictionary<Vector2Int, Vector2> _collapseBlocked = new Dictionary<Vector2Int, Vector2>();
        private readonly List<Vector2Int> _trail = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> _trailIndex = new Dictionary<Vector2Int, int>();
        private int _chaserIdx0;
        private float _chaserLegStart;

        // Node storage (struct-of-arrays in lists, reused between plans).
        private readonly List<int> _nCell = new List<int>();
        private readonly List<int> _nK = new List<int>();
        private readonly List<int> _nMask = new List<int>();
        private readonly List<int> _nRoute = new List<int>();
        private readonly List<int> _nParent = new List<int>();
        private readonly List<byte> _nFlags = new List<byte>(); // 1 = must move (collapse tile), 2 = on teleporter pad
        private readonly Dictionary<long, int> _visited = new Dictionary<long, int>();
        private List<int>[] _buckets;
        private int _fMin;

        /// <summary>Why the last replan happened and what it produced (diagnostics only).</summary>
        internal string Trace { get; private set; } = string.Empty;

        /// <summary>Plans computed so far (diagnostics).</summary>
        public int PlansMade { get; private set; }

        /// <summary>Total A* expansions so far (diagnostics).</summary>
        public long TotalExpansions { get; private set; }

        /// <summary>The direction to hold this frame (null = hold nothing).</summary>
        public Dir? Decide(LevelSim sim)
        {
            if (sim == null || sim.Status != SimStatus.Playing)
            {
                _hasPlan = false;
                return null;
            }

            if (sim != _sim || sim.Time < _lastTime - 1e-4f)
            {
                if (sim.Layout == _cautionLayout)
                {
                    _deathsOnLayout++; // same layout restarted: the previous attempt died
                }
                else
                {
                    _cautionLayout = sim.Layout;
                    _deathsOnLayout = 0;
                }

                int caution = Mathf.Min(_deathsOnLayout, MaxCaution);
                _margin = BaseMargin + 0.03f * caution;
                _patrolMargin = BasePatrolMargin + 0.03f * caution;
                _chaserLead = BaseChaserLead + caution;
                _sim = sim;
                _hasPlan = false;
                _retryAt = 0f;
            }

            _lastTime = sim.Time;

            if (sim.IsMoving)
            {
                return DecideWhileMoving(sim);
            }

            // Arrived on a teleporter pad: the jump is pending and input is ignored until it fires.
            if (sim.Layout.TeleporterAt(sim.PlayerCell) != null)
            {
                return null;
            }

            Dir? d;
            if (_hasPlan && TryFollowIdle(sim, out d))
            {
                return d;
            }

            if (sim.Time < _retryAt)
            {
                return null;
            }

            Plan(sim);
            if (_hasPlan && TryFollowIdle(sim, out d))
            {
                return d;
            }

            return null;
        }

        private Dir? DecideWhileMoving(LevelSim sim)
        {
            if (!_hasPlan)
            {
                return null;
            }

            int found = -1;
            for (int j = Mathf.Max(0, _idx - 1); j < _cells.Count - 1; j++)
            {
                if (!_jump[j] && _cells[j] == sim.MoveFrom && _cells[j + 1] == sim.MoveTo)
                {
                    found = j + 1;
                    break;
                }
            }

            if (found < 0)
            {
                Trace = "moving mismatch " + sim.MoveFrom + "->" + sim.MoveTo;
                _hasPlan = false;
                return null;
            }

            _idx = found;
            float arrival = sim.Time + (1f - sim.MoveProgress) * D;
            int q = _idx;
            if (q < _cells.Count - 1 && !_jump[q] && _cells[q + 1] != _cells[q] &&
                Mathf.Abs(_t0 + q * D - arrival) <= LateTolerance)
            {
                return DirBetween(_cells[q], _cells[q + 1]);
            }

            return null;
        }

        /// <summary>Follows the plan while idle. Returns false if a replan is needed.</summary>
        private bool TryFollowIdle(LevelSim sim, out Dir? dir)
        {
            dir = null;
            int j = -1;
            int limit = Mathf.Min(_idx + 3, _cells.Count - 1);
            for (int i = _idx; i <= limit; i++)
            {
                if (_cells[i] == sim.PlayerCell)
                {
                    j = i;
                    break;
                }
            }

            if (j < 0)
            {
                Trace = "off-plan at " + sim.PlayerCell;
                return false;
            }

            _idx = j;
            int m = _idx;
            while (m < _cells.Count - 1 && !_jump[m] && _cells[m + 1] == _cells[m])
            {
                m++;
            }

            if (m >= _cells.Count - 1)
            {
                Trace = "exhausted at " + sim.PlayerCell;
                return false; // exhausted
            }

            if (_jump[m])
            {
                Trace = "jump mismatch";
                return false; // should be on the pad (handled earlier); off-plan
            }

            float scheduled = _t0 + m * D;
            if (sim.Time < scheduled - EarlyTolerance)
            {
                return true; // wait
            }

            if (sim.Time > scheduled + LateTolerance)
            {
                Trace = "late at " + sim.PlayerCell + " by " + (sim.Time - scheduled);
                return false;
            }

            dir = DirBetween(_cells[m], _cells[m + 1]);
            return true;
        }

        private static Dir DirBetween(Vector2Int a, Vector2Int b)
        {
            Vector2Int d = b - a;
            if (d.x > 0)
            {
                return Dir.Right;
            }

            if (d.x < 0)
            {
                return Dir.Left;
            }

            return d.y > 0 ? Dir.Up : Dir.Down;
        }

        // ------------------------------------------------------------------ planning

        private void Plan(LevelSim sim)
        {
            PlansMade++;
            _hasPlan = false;
            PrepareContext(sim);

            _t0 = sim.Time;
            _idx = 0;

            _nCell.Clear();
            _nK.Clear();
            _nMask.Clear();
            _nRoute.Clear();
            _nParent.Clear();
            _nFlags.Clear();
            _visited.Clear();
            int maxF = MaxSteps + _nn * 4 + 8;
            if (_buckets == null || _buckets.Length < maxF + 1)
            {
                _buckets = new List<int>[maxF + 1];
                for (int i = 0; i < _buckets.Length; i++)
                {
                    _buckets[i] = new List<int>();
                }
            }
            else
            {
                foreach (var b in _buckets)
                {
                    b.Clear();
                }
            }

            Vector2Int start = sim.PlayerCell;
            int rootRoute = _trail.Count - 1;
            byte rootFlags = (byte)(_layout.CollapseTileAt(start) != null ? 1 : 0);
            int root = AddNode(Idx(start), 0, 0, rootRoute, -1, rootFlags);
            _visited[Key(Idx(start), 0, 0)] = rootRoute;
            _fMin = int.MaxValue;
            Push(root);

            int best = root;
            int goal = -1;
            int expansions = 0;
            int f = 0;
            while (goal < 0 && expansions < MaxExpansions && f < _buckets.Length)
            {
                var bucket = _buckets[f];
                if (bucket.Count == 0)
                {
                    f++;
                    continue;
                }

                int node = bucket[bucket.Count - 1];
                bucket.RemoveAt(bucket.Count - 1);
                expansions++;

                int hNode = _h[_nCell[node]];
                if (hNode < _h[_nCell[best]] && _nFlags[node] == 0)
                {
                    best = node;
                }

                _fMin = f;
                goal = Expand(node);

                // Children may land in a lower bucket if h drops by more than one (teleports).
                f = _fMin;
            }

            TotalExpansions += expansions;
            Trace = Trace.Split('|')[0] + "| plan from " + start + " t=" + _t0.ToString("F2") + " exp=" + expansions +
                     (goal >= 0 ? " goal" : " partial h=" + _h[_nCell[best]]) + " k=" + _nK[goal >= 0 ? goal : best];

            int end = goal >= 0 ? goal : best;
            if (end == root)
            {
                _retryAt = sim.Time + 0.25f;
                return;
            }

            BuildPlan(end);
            _planReachesGoal = goal >= 0;
            _hasPlan = true;
        }

        private void BuildPlan(int end)
        {
            int k = _nK[end];
            _cells.Clear();
            _jump.Clear();
            for (int i = 0; i <= k; i++)
            {
                _cells.Add(default);
                _jump.Add(false);
            }

            int n = end;
            while (n >= 0)
            {
                int p = _nParent[n];
                _cells[_nK[n]] = Cell(_nCell[n]);
                if (p >= 0 && _nK[n] - _nK[p] == 2)
                {
                    // Teleport jump: pad at parent step, still on the pad for one more step, then target.
                    _cells[_nK[p] + 1] = Cell(_nCell[p]);
                    _jump[_nK[p] + 1] = true;
                }

                n = p;
            }
        }

        /// <summary>Expands a node; returns the goal node index if a child reaches the destination, else -1.</summary>
        private int Expand(int node)
        {
            int k = _nK[node];
            if (k >= MaxSteps)
            {
                return -1;
            }

            Vector2Int c = Cell(_nCell[node]);
            int mask = _nMask[node];
            int route = _nRoute[node];
            byte flags = _nFlags[node];
            float ts = _t0 + k * D;

            if ((flags & 2) != 0)
            {
                // On a pad: the only successor is the teleport target, two steps later.
                var tp = _layout.TeleporterAt(c);
                float arrive = ts; // arrived on the pad at this node's time
                if (CellUnsafe(c, arrive, arrive + Teleporter.Delay + 0.03f + _margin))
                {
                    return -1;
                }

                float tNext = _t0 + (k + 2) * D;
                if (CellUnsafe(tp.Target, arrive + Teleporter.Delay, tNext + _margin) ||
                    ChaserDanger(tp.Target, route + 1, tNext))
                {
                    return -1;
                }

                return TryAdd(node, tp.Target, k + 2, mask, route + 1, 0);
            }

            foreach (Dir d in DirUtil.All)
            {
                Vector2Int to = c + DirUtil.Delta(d);
                if (!MoveSafe(c, to, d, ts, mask))
                {
                    continue;
                }

                int newRoute = _layout.HasChaser ? RouteAfterEntering(node, to) : route + 1;
                if (ChaserDanger(c, route, ts) || ChaserDanger(to, newRoute, ts + D))
                {
                    continue;
                }

                int newMask = mask;
                var plate = _layout.TriggerPlateAt(to);
                if (plate != null && _plateBit.TryGetValue(plate, out int bit))
                {
                    newMask ^= 1 << bit;
                }

                if (to == _layout.Destination)
                {
                    return AddNode(Idx(to), k + 1, newMask, newRoute, node, 0);
                }

                byte childFlags = 0;
                var collapse = _layout.CollapseTileAt(to);
                if (collapse != null)
                {
                    if (CrackedEarlierInPlan(node, to, collapse, ts + D))
                    {
                        continue;
                    }

                    childFlags = 1;
                }

                if (_layout.TeleporterAt(to) != null)
                {
                    childFlags = 2;
                }

                int r = TryAdd(node, to, k + 1, newMask, newRoute, childFlags);
                if (r >= 0)
                {
                    return r;
                }
            }

            if ((flags & 1) == 0 && !CellUnsafe(c, ts - _patrolMargin, ts + D + _patrolMargin) && !ChaserDanger(c, route, ts + D))
            {
                TryAdd(node, c, k + 1, mask, route, 0);
            }

            return -1;
        }

        /// <summary>
        /// The player's loop-erased trail position after stepping from <paramref name="node"/> into
        /// <paramref name="cell"/>: re-entering a cell still on the trail (earlier in this plan or on
        /// the sim's live trail) erases the loop back to it; anything else extends the trail by one.
        /// </summary>
        private int RouteAfterEntering(int node, Vector2Int cell)
        {
            int target = Idx(cell);
            int minRoute = _nRoute[node];
            int guard = 0;
            for (int a = node; a >= 0 && guard++ <= MaxSteps + 2; a = _nParent[a])
            {
                int ra = _nRoute[a];
                if (_nCell[a] == target && ra <= minRoute)
                {
                    return ra;
                }

                if (ra < minRoute)
                {
                    minRoute = ra;
                }
            }

            if (_trailIndex.TryGetValue(cell, out int ti) && ti <= minRoute)
            {
                return ti;
            }

            return _nRoute[node] + 1;
        }

        /// <summary>
        /// True if the plan leading to <paramref name="node"/> already stepped on collapse tile
        /// <paramref name="cell"/> such that it will be cracking or collapsed around
        /// <paramref name="arrive"/> (entering starts its timer; the sim-state check only knows the
        /// tiles cracked before the plan began).
        /// </summary>
        private bool CrackedEarlierInPlan(int node, Vector2Int cell, CollapseTile tile, float arrive)
        {
            int target = Idx(cell);
            int guard = 0;
            for (int a = node; a >= 0 && guard++ <= MaxSteps + 2; a = _nParent[a])
            {
                if (_nCell[a] != target)
                {
                    continue;
                }

                float crackedAt = _t0 + _nK[a] * D;
                float restoredAt = tile.Permanent ? float.MaxValue : crackedAt + CollapseTile.CollapseDelay + CollapseTile.RestoreDelay;
                return arrive - _margin <= restoredAt;
            }

            return false;
        }

        private int TryAdd(int parent, Vector2Int cell, int k, int mask, int route, byte flags)
        {
            int ci = Idx(cell);
            long key = Key(ci, k, mask);

            // With a chaser, a state reached again with a longer lead on it is still worth expanding.
            int seen;
            if (_visited.TryGetValue(key, out seen) && (!_layout.HasChaser || route <= seen))
            {
                return -1;
            }

            _visited[key] = route;

            int n = AddNode(ci, k, mask, route, parent, flags);
            Push(n);
            return -1;
        }

        private int AddNode(int cell, int k, int mask, int route, int parent, byte flags)
        {
            _nCell.Add(cell);
            _nK.Add(k);
            _nMask.Add(mask);
            _nRoute.Add(route);
            _nParent.Add(parent);
            _nFlags.Add(flags);
            return _nCell.Count - 1;
        }

        private void Push(int node)
        {
            int f = _nK[node] + _h[_nCell[node]];
            if (f >= _buckets.Length)
            {
                f = _buckets.Length - 1;
            }

            _buckets[f].Add(node);
            if (f < _fMin)
            {
                _fMin = f;
            }
        }

        private long Key(int cell, int k, int mask) => ((long)k * _nn + cell) * 256 + mask;

        private int Idx(Vector2Int c) => c.y * _layout.N + c.x;

        private Vector2Int Cell(int i) => new Vector2Int(i % _layout.N, i / _layout.N);

        private bool MoveSafe(Vector2Int from, Vector2Int to, Dir d, float ts, int mask)
        {
            var maze = _layout.Maze;
            if (!maze.InBounds(to))
            {
                return false;
            }

            Edge e = Edge.Between(from, d);
            if (maze.HasWall(e))
            {
                return false; // static walls, including invisible ones
            }

            var ow = _layout.OneWayAt(e);
            if (ow != null && !ow.AllowsStep(d))
            {
                return false;
            }

            if (_sim.IsDecoyActive(to))
            {
                return false;
            }

            var tp = _layout.TriggerFor(e);
            if (tp != null)
            {
                bool open = _triggerBase[e];
                if (_plateBit.TryGetValue(tp, out int bit) && ((mask >> bit) & 1) != 0)
                {
                    open = !open;
                }

                if (!open)
                {
                    return false;
                }
            }

            float a = ts - _margin;
            float b = ts + D + _margin;

            var gFrom = _layout.GateAt(from);
            var gTo = _layout.GateAt(to);
            if (gFrom != null || gTo != null)
            {
                Dir opp = DirUtil.Opposite(d);
                for (float s = ts - _margin; s <= ts + _margin + 1e-5f; s += SampleDt)
                {
                    if ((gFrom != null && !gFrom.Allows(d, s)) || (gTo != null && !gTo.Allows(opp, s)))
                    {
                        return false;
                    }
                }
            }

            var mw = _layout.MovingWallAt(e);
            var pw = _layout.PhaseWallAt(e);
            if (mw != null || pw != null)
            {
                for (float s = a; s <= b + 1e-5f; s += SampleDt)
                {
                    if ((mw != null && mw.BlockingEdgeAt(s) == e) || (pw != null && pw.IsSolidAt(s)))
                    {
                        return false;
                    }
                }
            }

            if (CollapseBlocked(to, ts, ts + 2f * D + _margin))
            {
                return false;
            }

            return !CellUnsafe(from, ts - _patrolMargin, ts + 0.5f * D + _patrolMargin) &&
                   !CellUnsafe(to, ts + 0.5f * D - _patrolMargin, ts + D + _patrolMargin);
        }

        /// <summary>True if a patrol may occupy <paramref name="cell"/> at any time in [a, b].</summary>
        private bool CellUnsafe(Vector2Int cell, float a, float b)
        {
            var patrols = _layout.Patrols;
            for (int i = 0; i < patrols.Count; i++)
            {
                var p = patrols[i];
                bool onPath = false;
                for (int j = 0; j < p.Path.Count; j++)
                {
                    if (p.Path[j] == cell)
                    {
                        onPath = true;
                        break;
                    }
                }

                if (!onPath)
                {
                    continue;
                }

                for (float s = a; s <= b + 1e-5f; s += SampleDt)
                {
                    if (p.OccupiedCellAt(s) == cell)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CollapseBlocked(Vector2Int cell, float a, float b)
        {
            return _collapseBlocked.TryGetValue(cell, out var iv) && a <= iv.y && b >= iv.x;
        }

        /// <summary>
        /// Predicts the chaser along the player's trail (and then along the planned route) and
        /// reports whether being at <paramref name="cell"/> with loop-erased route position
        /// <paramref name="route"/> at time <paramref name="t"/> is too close to it.
        /// </summary>
        private bool ChaserDanger(Vector2Int cell, int route, float t)
        {
            if (!_layout.HasChaser || t < LevelSim.ChaserSpawnAt - 0.5f * LevelSim.ChaserStepTime)
            {
                return false;
            }

            // Assume the chaser runs slightly early, to cover execution drift.
            float legs = (t + _margin - _chaserLegStart) / LevelSim.ChaserStepTime;
            int r = _chaserIdx0 + Mathf.FloorToInt(Mathf.Max(0f, legs) + 0.5f);
            if (route - r <= _chaserLead)
            {
                return true;
            }

            return r >= 0 && r < _trail.Count && _trail[r] == cell;
        }

        private void PrepareContext(LevelSim sim)
        {
            if (_layout != sim.Layout)
            {
                _layout = sim.Layout;
                _nn = _layout.N * _layout.N;
                ComputeHeuristic();
                _plateBit.Clear();
                for (int i = 0; i < _layout.TriggerPlates.Count && i < MaxPlates; i++)
                {
                    _plateBit[_layout.TriggerPlates[i]] = i;
                }
            }

            _triggerBase.Clear();
            foreach (var plate in _layout.TriggerPlates)
            {
                foreach (var e in plate.LinkedEdges())
                {
                    _triggerBase[e] = sim.IsTriggerEdgeOpen(e);
                }
            }

            _collapseBlocked.Clear();
            foreach (var tile in _layout.CollapseTiles)
            {
                var cell = tile.Cell;
                switch (sim.TileStateAt(cell))
                {
                    case TileState.Collapsed:
                        float rem = sim.TileRestoreRemaining(cell);
                        _collapseBlocked[cell] = new Vector2(sim.Time - 1f, tile.Permanent ? float.MaxValue : sim.Time + rem);
                        break;
                    case TileState.Cracking:
                        float crackStart = sim.Time - sim.TileCrackProgress(cell) * CollapseTile.CollapseDelay;
                        float until = tile.Permanent
                            ? float.MaxValue
                            : crackStart + CollapseTile.CollapseDelay + CollapseTile.RestoreDelay;
                        _collapseBlocked[cell] = new Vector2(sim.Time - 1f, until);
                        break;
                }
            }

            _trail.Clear();
            _trailIndex.Clear();
            var trail = sim.Trail;
            for (int i = 0; i < trail.Count; i++)
            {
                _trail.Add(trail[i]);
                _trailIndex[trail[i]] = i;
            }

            if (_trail.Count == 0)
            {
                _trail.Add(sim.PlayerCell);
                _trailIndex[sim.PlayerCell] = 0;
            }

            _chaserIdx0 = sim.ChaserRouteIndex(out _chaserLegStart);
        }

        /// <summary>Static distance-to-destination (dynamic edges treated as open, one-ways respected, teleports as 3-step jumps).</summary>
        private void ComputeHeuristic()
        {
            int n = _layout.N;
            _h = new int[_nn];
            int unreachable = _nn * 3;
            for (int i = 0; i < _nn; i++)
            {
                _h[i] = unreachable;
            }

            var queue = new Queue<int>();
            _h[Idx(_layout.Destination)] = 0;
            queue.Enqueue(Idx(_layout.Destination));
            int guard = 0;
            int maxIter = _nn * 64;
            while (queue.Count > 0 && guard++ < maxIter)
            {
                int ci = queue.Dequeue();
                Vector2Int c = Cell(ci);
                int dc = _h[ci];

                // Predecessors p that can step into c.
                foreach (Dir d in DirUtil.All)
                {
                    Vector2Int p = c + DirUtil.Delta(d);
                    if (p.x < 0 || p.y < 0 || p.x >= n || p.y >= n)
                    {
                        continue;
                    }

                    Dir stepDir = DirUtil.Opposite(d); // p -> c
                    Edge e = Edge.Between(p, stepDir);
                    if (_layout.Maze.HasWall(e))
                    {
                        continue;
                    }

                    var ow = _layout.OneWayAt(e);
                    if (ow != null && !ow.AllowsStep(stepDir))
                    {
                        continue;
                    }

                    int pi = Idx(p);
                    if (dc + 1 < _h[pi])
                    {
                        _h[pi] = dc + 1;
                        queue.Enqueue(pi);
                    }
                }

                // Pads whose target is c.
                foreach (var tp in _layout.Teleporters)
                {
                    if (tp.Target == c)
                    {
                        int pi = Idx(tp.Pad);
                        if (dc + 2 < _h[pi])
                        {
                            _h[pi] = dc + 2;
                            queue.Enqueue(pi);
                        }
                    }
                }
            }
        }
    }
}
