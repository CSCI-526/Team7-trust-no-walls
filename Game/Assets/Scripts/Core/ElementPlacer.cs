using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// Places level elements onto a <see cref="LevelLayoutBuilder"/> following the spec's rules:
    /// nothing on Start, Destination or Start's 4 neighbors; edge elements never touch Start or
    /// Destination; one cell element per cell (patrol paths and teleporter targets count); one
    /// edge element per edge. Every element is validated with the solvability check right after
    /// placement and undone if it fails; each element gets up to <see cref="TriesPerElement"/> tries.
    /// Extra rules: rotating gates and collapsing tiles never sit on an endpoint of a phase-wall or
    /// moving-wall edge (and vice versa), and a trigger's linked edges never touch its plate.
    /// </summary>
    internal sealed class ElementPlacer
    {
        public const int TriesPerElement = 40;

        private readonly LevelLayoutBuilder _b;
        private readonly System.Random _rng;
        private readonly Maze _maze;
        private readonly HashSet<Vector2Int> _excluded = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>();
        private readonly HashSet<Edge> _elementEdges = new HashSet<Edge>();
        private readonly HashSet<Edge> _triggerEdges = new HashSet<Edge>();

        public ElementPlacer(LevelLayoutBuilder builder, System.Random rng)
        {
            _b = builder;
            _rng = rng;
            _maze = builder.Maze;
            _excluded.Add(builder.Start);
            _excluded.Add(builder.Destination);
            foreach (var d in DirUtil.All)
            {
                _excluded.Add(builder.Start + DirUtil.Delta(d));
            }

            // Account for elements already on the builder (hand-built tests).
            foreach (var x in builder.MemoryTiles) _occupied.Add(x.Cell);
            foreach (var x in builder.CollapseTiles) _occupied.Add(x.Cell);
            foreach (var x in builder.TriggerPlates)
            {
                _occupied.Add(x.Plate);
                foreach (var e in x.LinkedEdges()) { _elementEdges.Add(e); _triggerEdges.Add(e); }
            }

            foreach (var x in builder.Teleporters) { _occupied.Add(x.Pad); _occupied.Add(x.Target); }
            foreach (var x in builder.RotatingGates) _occupied.Add(x.Cell);
            foreach (var x in builder.Patrols) foreach (var c in x.Path) _occupied.Add(c);
            foreach (var x in builder.Decoys) _occupied.Add(x.Cell);
            foreach (var x in builder.InvisibleWalls) _elementEdges.Add(x.Edge);
            foreach (var x in builder.PhaseWalls) _elementEdges.Add(x.Edge);
            foreach (var x in builder.OneWays) _elementEdges.Add(x.Edge);
            foreach (var x in builder.MovingWalls) { _elementEdges.Add(x.A); _elementEdges.Add(x.B); }
        }

        /// <summary>
        /// True if a rotating gate or collapsing tile may go on <paramref name="c"/>: the cell is free
        /// and is not an endpoint of any phase-wall or moving-wall edge. Two periodic constraints on
        /// one crossing can have windows that never line up (same 5 s cycles), so they are kept apart.
        /// </summary>
        internal bool CanHostCellHazard(Vector2Int c)
        {
            if (!IsFreeCell(c))
            {
                return false;
            }

            foreach (var w in _b.PhaseWalls)
            {
                if (w.Edge.A == c || w.Edge.B == c) return false;
            }

            foreach (var w in _b.MovingWalls)
            {
                if (w.A.A == c || w.A.B == c || w.B.A == c || w.B.B == c) return false;
            }

            return true;
        }

        /// <summary>True if a phase wall or moving-wall position may use <paramref name="e"/>: it touches no gate or collapsing tile.</summary>
        internal bool CanHostPeriodicEdge(Edge e)
        {
            foreach (var g in _b.RotatingGates)
            {
                if (g.Cell == e.A || g.Cell == e.B) return false;
            }

            foreach (var c in _b.CollapseTiles)
            {
                if (c.Cell == e.A || c.Cell == e.B) return false;
            }

            return true;
        }

        /// <summary>Places up to <paramref name="count"/> elements of <paramref name="m"/>; returns how many were placed.</summary>
        public int Place(Mechanic m, int count)
        {
            switch (m)
            {
                case Mechanic.TriggerWalls: return Repeat(count, _ => TryTrigger());
                case Mechanic.OneWayPaths: return Repeat(count, _ => TryOneWay());
                case Mechanic.MovingWalls: return Repeat(count, _ => TryMovingWall());
                case Mechanic.DisappearingWalls:
                {
                    int onBase = count / 2;
                    return Repeat(count - onBase, _ => TryRoutePhaseWall()) + Repeat(onBase, _ => TryBasePhaseWall());
                }
                case Mechanic.InvisibleWalls: return Repeat(count, _ => TryInvisibleWall());
                case Mechanic.MemoryTiles: return Repeat(count, _ => TryMemoryTile());
                case Mechanic.CollapsingTiles: return Repeat(count, i => TryCollapseTile(i % 2 == 1));
                case Mechanic.Teleporters: return Repeat(count, _ => TryTeleporter());
                case Mechanic.RotatingBarriers: return Repeat(count, _ => TryGate());
                case Mechanic.Patrols: return Repeat(count, _ => TryPatrol());
                case Mechanic.Decoys: return PlaceDecoys(count);
                case Mechanic.Chaser:
                    _b.WithChaser();
                    return 1;
                default: throw new ArgumentOutOfRangeException(nameof(m), m, null);
            }
        }

        // ---------------------------------------------------------------- framework

        private int Repeat(int count, Func<int, bool> tryOnce)
        {
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                for (int t = 0; t < TriesPerElement; t++)
                {
                    if (tryOnce(i))
                    {
                        placed++;
                        break;
                    }
                }
            }

            return placed;
        }

        /// <summary>Applies a placement, keeps it if the level stays solvable, otherwise undoes it.</summary>
        private bool Commit(Action apply, Action undo)
        {
            apply();
            if (SolvabilityValidator.IsSolvable(_b))
            {
                return true;
            }

            undo();
            return false;
        }

        private bool IsFreeCell(Vector2Int c) => _maze.InBounds(c) && !_excluded.Contains(c) && !_occupied.Contains(c);

        private bool IsFreeEdge(Edge e) =>
            !_elementEdges.Contains(e)
            && e.A != _b.Start && e.B != _b.Start
            && e.A != _b.Destination && e.B != _b.Destination;

        /// <summary>An open maze edge that is not trigger-linked (trigger edges are treated as closed).</summary>
        private bool IsPassage(Edge e) => !_maze.HasWall(e) && !_triggerEdges.Contains(e);

        /// <summary>The maze with every trigger-linked edge closed (the validator's worst case).</summary>
        private Maze PlanningMaze()
        {
            var m = _maze.Clone();
            foreach (var e in _triggerEdges)
            {
                m.SetWall(e, true);
            }

            return m;
        }

        /// <summary>The current Start-to-Destination shortest path in the planning maze.</summary>
        private List<Vector2Int> Route() => MazeGenerator.ShortestPath(PlanningMaze(), _b.Start, _b.Destination);

        private T Pick<T>(List<T> list) => list[_rng.Next(list.Count)];

        private float RandomPhase(float cycle) => (float)(_rng.NextDouble() * cycle);

        private List<Vector2Int> FreeCells()
        {
            var cells = new List<Vector2Int>();
            for (int x = 0; x < _maze.N; x++)
            {
                for (int y = 0; y < _maze.N; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (IsFreeCell(c))
                    {
                        cells.Add(c);
                    }
                }
            }

            return cells;
        }

        private List<Edge> FreeStaticWalls()
        {
            var list = new List<Edge>();
            foreach (var e in _maze.InteriorWalls())
            {
                if (IsFreeEdge(e))
                {
                    list.Add(e);
                }
            }

            return list;
        }

        private List<Edge> FreePassages()
        {
            var list = new List<Edge>();
            foreach (var e in _maze.InteriorEdges())
            {
                if (IsPassage(e) && IsFreeEdge(e))
                {
                    list.Add(e);
                }
            }

            return list;
        }

        private void Occupy(Vector2Int c) => _occupied.Add(c);
        private void Release(Vector2Int c) => _occupied.Remove(c);

        // ---------------------------------------------------------------- trigger walls

        private bool TryTrigger()
        {
            var cells = FreeCells();
            if (cells.Count == 0)
            {
                return false;
            }

            var plate = Pick(cells);
            int links = 1 + _rng.Next(2);
            var opens = new List<Edge>();
            var closes = new List<Edge>();
            for (int j = 0; j < links; j++)
            {
                bool wantOpens = _rng.Next(2) == 0;
                if (!(wantOpens ? TryPickOpens(plate, opens, closes) : TryPickCloses(plate, opens, closes)))
                {
                    if (!(wantOpens ? TryPickCloses(plate, opens, closes) : TryPickOpens(plate, opens, closes)))
                    {
                        break;
                    }
                }
            }

            if (opens.Count + closes.Count == 0)
            {
                return false;
            }

            var element = new TriggerPlate(plate, opens, closes, _b.TriggerPlates.Count % 3);
            return Commit(
                () =>
                {
                    foreach (var e in opens) _maze.SetWall(e, false);
                    foreach (var e in element.LinkedEdges()) { _elementEdges.Add(e); _triggerEdges.Add(e); }
                    Occupy(plate);
                    _b.TriggerPlates.Add(element);
                },
                () =>
                {
                    foreach (var e in opens) _maze.SetWall(e, true);
                    foreach (var e in element.LinkedEdges()) { _elementEdges.Remove(e); _triggerEdges.Remove(e); }
                    Release(plate);
                    _b.TriggerPlates.Remove(element);
                });
        }

        // Linked edges never touch the plate cell itself (entering the plate would toggle the edge
        // being crossed). "Opens" edges are interior (static) walls: hidden corridors revealed by the plate.
        private bool TryPickOpens(Vector2Int plate, List<Edge> opens, List<Edge> closes)
        {
            var walls = FreeStaticWalls();
            walls.RemoveAll(e => e.A == plate || e.B == plate || opens.Contains(e) || closes.Contains(e));
            if (walls.Count == 0)
            {
                return false;
            }

            opens.Add(Pick(walls));
            return true;
        }

        // "Closes" edges are open passages that are not bridges (with other trigger edges closed).
        private bool TryPickCloses(Vector2Int plate, List<Edge> opens, List<Edge> closes)
        {
            var passages = FreePassages();
            passages.RemoveAll(e => e.A == plate || e.B == plate || opens.Contains(e) || closes.Contains(e));
            if (passages.Count == 0)
            {
                return false;
            }

            var planning = PlanningMaze();
            foreach (var e in closes)
            {
                planning.SetWall(e, true);
            }

            var candidate = Pick(passages);
            if (MazeGenerator.IsBridge(planning, candidate))
            {
                return false;
            }

            closes.Add(candidate);
            return true;
        }

        // ---------------------------------------------------------------- one-way paths

        private bool TryOneWay()
        {
            Edge edge;
            Dir allowed;
            if (_rng.Next(2) == 0)
            {
                // A route edge pointing toward the Destination.
                var route = Route();
                var options = new List<int>();
                for (int i = 0; i + 1 < route.Count; i++)
                {
                    var e = LevelLayoutBuilder.EdgeBetweenCells(route[i], route[i + 1]);
                    if (IsFreeEdge(e) && IsPassage(e))
                    {
                        options.Add(i);
                    }
                }

                if (options.Count == 0)
                {
                    return false;
                }

                int k = Pick(options);
                edge = LevelLayoutBuilder.EdgeBetweenCells(route[k], route[k + 1]);
                allowed = MazeAnalysis.DirTo(route[k], route[k + 1]);
            }
            else
            {
                // Any non-bridge open edge, either direction.
                var passages = FreePassages();
                if (passages.Count == 0)
                {
                    return false;
                }

                edge = Pick(passages);
                if (MazeGenerator.IsBridge(PlanningMaze(), edge))
                {
                    return false;
                }

                allowed = MazeAnalysis.DirTo(edge.A, edge.B);
                if (_rng.Next(2) == 0)
                {
                    allowed = DirUtil.Opposite(allowed);
                }
            }

            var element = new OneWay(edge, allowed);
            return Commit(
                () => { _elementEdges.Add(edge); _b.OneWays.Add(element); },
                () => { _elementEdges.Remove(edge); _b.OneWays.Remove(element); });
        }

        // ---------------------------------------------------------------- moving walls

        private bool TryMovingWall()
        {
            // The wall slides across a middle cell between two opposite (parallel) edges of it.
            var options = new List<(Edge, Edge)>();
            for (int x = 0; x < _maze.N; x++)
            {
                for (int y = 0; y < _maze.N; y++)
                {
                    var middle = new Vector2Int(x, y);
                    AddMovingWallOption(options, middle, Dir.Left);
                    AddMovingWallOption(options, middle, Dir.Down);
                }
            }

            if (options.Count == 0)
            {
                return false;
            }

            var (a, b) = Pick(options);
            if (_rng.Next(2) == 0)
            {
                (a, b) = (b, a);
            }

            var element = new MovingWall(a, b, RandomPhase(MovingWall.Cycle));
            return Commit(
                () => { _elementEdges.Add(a); _elementEdges.Add(b); _b.MovingWalls.Add(element); },
                () => { _elementEdges.Remove(a); _elementEdges.Remove(b); _b.MovingWalls.Remove(element); });
        }

        private void AddMovingWallOption(List<(Edge, Edge)> options, Vector2Int middle, Dir first)
        {
            Dir second = DirUtil.Opposite(first);
            if (!_maze.InBounds(middle + DirUtil.Delta(first)) || !_maze.InBounds(middle + DirUtil.Delta(second)))
            {
                return;
            }

            var a = Edge.Between(middle, first);
            var b = Edge.Between(middle, second);
            if (IsFreeEdge(a) && IsFreeEdge(b) && IsPassage(a) && IsPassage(b)
                && CanHostPeriodicEdge(a) && CanHostPeriodicEdge(b))
            {
                options.Add((a, b));
            }
        }

        // ---------------------------------------------------------------- disappearing walls

        private bool TryRoutePhaseWall()
        {
            var route = Route();
            var options = new List<Edge>();
            foreach (var e in MazeAnalysis.PathEdges(route))
            {
                if (IsFreeEdge(e) && IsPassage(e) && CanHostPeriodicEdge(e))
                {
                    options.Add(e);
                }
            }

            if (options.Count == 0)
            {
                return false;
            }

            var edge = Pick(options);
            var element = new PhaseWall(edge, RandomPhase(PhaseWall.Cycle), false);
            return Commit(
                () => { _elementEdges.Add(edge); _b.PhaseWalls.Add(element); },
                () => { _elementEdges.Remove(edge); _b.PhaseWalls.Remove(element); });
        }

        private bool TryBasePhaseWall()
        {
            var walls = FreeStaticWalls();
            walls.RemoveAll(e => !CanHostPeriodicEdge(e));
            if (walls.Count == 0)
            {
                return false;
            }

            var edge = Pick(walls);
            var element = new PhaseWall(edge, RandomPhase(PhaseWall.Cycle), true);
            return Commit(
                () => { _maze.SetWall(edge, false); _elementEdges.Add(edge); _b.PhaseWalls.Add(element); },
                () => { _maze.SetWall(edge, true); _elementEdges.Remove(edge); _b.PhaseWalls.Remove(element); });
        }

        // ---------------------------------------------------------------- invisible walls and memory tiles

        private bool TryInvisibleWall()
        {
            var walls = FreeStaticWalls();
            if (walls.Count == 0)
            {
                return false;
            }

            var edge = Pick(walls);
            var element = new InvisibleWall(edge);
            return Commit(
                () => { _elementEdges.Add(edge); _b.InvisibleWalls.Add(element); },
                () => { _elementEdges.Remove(edge); _b.InvisibleWalls.Remove(element); });
        }

        private bool TryMemoryTile()
        {
            var cells = FreeCells();
            if (cells.Count == 0)
            {
                return false;
            }

            // Prefer tiles that would actually reveal something.
            var useful = cells.FindAll(c => _b.InvisibleWalls.Exists(w => Near(c, w.Edge, MemoryTile.RevealRadius)));
            var cell = Pick(useful.Count > 0 ? useful : cells);
            var element = new MemoryTile(cell);
            return Commit(
                () => { Occupy(cell); _b.MemoryTiles.Add(element); },
                () => { Release(cell); _b.MemoryTiles.Remove(element); });
        }

        private static bool Near(Vector2Int c, Edge e, int radius)
        {
            return Chebyshev(c, e.A) <= radius && Chebyshev(c, e.B) <= radius;
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b) => Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));

        // ---------------------------------------------------------------- cell hazards

        private bool TryCollapseTile(bool permanent)
        {
            var cells = FreeCells();
            cells.RemoveAll(c => !CanHostCellHazard(c));
            if (cells.Count == 0)
            {
                return false;
            }

            var cell = Pick(cells);
            var element = new CollapseTile(cell, permanent);
            return Commit(
                () => { Occupy(cell); _b.CollapseTiles.Add(element); },
                () => { Release(cell); _b.CollapseTiles.Remove(element); });
        }

        private bool TryTeleporter()
        {
            var cells = FreeCells();
            if (cells.Count < 2)
            {
                return false;
            }

            var pad = Pick(cells);
            cells.Remove(pad);
            var target = Pick(cells);
            var element = new Teleporter(pad, target);
            return Commit(
                () => { Occupy(pad); Occupy(target); _b.Teleporters.Add(element); },
                () => { Release(pad); Release(target); _b.Teleporters.Remove(element); });
        }

        private bool TryGate()
        {
            var cells = FreeCells();
            cells.RemoveAll(c => !CanHostCellHazard(c));
            if (cells.Count == 0)
            {
                return false;
            }

            var cell = Pick(cells);
            var element = new RotatingGate(cell, RandomPhase(RotatingGate.Cycle), _rng.Next(2) == 0);
            return Commit(
                () => { Occupy(cell); _b.RotatingGates.Add(element); },
                () => { Release(cell); _b.RotatingGates.Remove(element); });
        }

        // ---------------------------------------------------------------- patrols

        private bool TryPatrol()
        {
            // Tiers, best first: (0) branch of length 2..4 hanging off the route; (1) length 1 off
            // the route; (2) length 2..4 anywhere; (3) length 1 anywhere. Lower tiers only matter
            // in small mazes with short routes, so intro levels always get a patrol.
            var routeCells = new HashSet<Vector2Int>(Route());
            var tiers = new List<List<Vector2Int>>[4];
            for (int i = 0; i < tiers.Length; i++)
            {
                tiers[i] = new List<List<Vector2Int>>();
            }

            foreach (var branch in MazeAnalysis.DeadEndBranches(_maze))
            {
                if (branch.Length > 4)
                {
                    continue;
                }

                var path = new List<Vector2Int> { branch.Junction };
                path.AddRange(branch.Cells);
                if (!path.TrueForAll(IsFreeCell))
                {
                    continue;
                }

                bool edgesFree = true;
                foreach (var e in MazeAnalysis.PathEdges(path))
                {
                    if (!IsFreeEdge(e) || !IsPassage(e))
                    {
                        edgesFree = false;
                        break;
                    }
                }

                if (!edgesFree)
                {
                    continue;
                }

                int tier = (routeCells.Contains(branch.Junction) ? 0 : 2) + (branch.Length >= 2 ? 0 : 1);
                tiers[tier].Add(path);
            }

            List<List<Vector2Int>> pool = null;
            foreach (var t in tiers)
            {
                if (t.Count > 0)
                {
                    pool = t;
                    break;
                }
            }

            if (pool == null)
            {
                return false;
            }

            var chosen = Pick(pool);
            var element = new Patrol(chosen, 0f);
            element = new Patrol(chosen, RandomPhase(element.Cycle));
            return Commit(
                () => { chosen.ForEach(Occupy); _b.Patrols.Add(element); },
                () => { chosen.ForEach(Release); _b.Patrols.Remove(element); });
        }

        // ---------------------------------------------------------------- decoys

        private int PlaceDecoys(int count)
        {
            // Longest dead-end branches first (not containing Start or the Destination). The spec
            // asks for length >= 3; shorter branches (down to 2) are used only after every
            // longer one was unusable (e.g. its end cell is already occupied), and length 1 only
            // as a last resort (heavily braided mazes can lack longer dead ends).
            var branches = MazeAnalysis.DeadEndBranches(_maze);
            branches.RemoveAll(br => ContainsCell(br, _b.Start) || ContainsCell(br, _b.Destination));

            // Stable sort by length, descending (ties keep scan order).
            var ordered = new List<DeadEndBranch>();
            for (int len = _maze.N * _maze.N; len >= 1 && ordered.Count < branches.Count; len--)
            {
                ordered.AddRange(branches.FindAll(br => br.Length == len));
            }

            int next = 0;
            return Repeat(count, _ =>
            {
                if (next >= ordered.Count)
                {
                    return false;
                }

                var cell = ordered[next++].DeadEnd;
                if (!IsFreeCell(cell))
                {
                    return false;
                }

                var element = new Decoy(cell);
                return Commit(
                    () => { Occupy(cell); _b.Decoys.Add(element); },
                    () => { Release(cell); _b.Decoys.Remove(element); });
            });
        }

        private static bool ContainsCell(DeadEndBranch br, Vector2Int c)
        {
            foreach (var x in br.Cells)
            {
                if (x == c)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
