using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>What occupies a cell (at most one cell element per cell).</summary>
    public enum CellElementKind
    {
        None,
        MemoryTile,
        CollapseTile,
        TriggerPlate,
        TeleporterPad,
        TeleporterTarget,
        RotatingGate,
        PatrolPath,
        Decoy
    }

    /// <summary>What sits on an edge (at most one edge element per edge).</summary>
    public enum EdgeElementKind
    {
        None,
        InvisibleWall,
        MovingWall,
        PhaseWall,
        TriggerWall,
        OneWay
    }

    /// <summary>
    /// A fully generated, immutable level. Build one with <see cref="LevelGenerator.Generate"/> or,
    /// for hand-made tests, with <see cref="LevelLayoutBuilder"/>.
    ///
    /// Maze invariant: <see cref="Maze"/> holds only STATIC walls, visible and invisible
    /// (invisible walls ARE walls in the maze; use <see cref="IsInvisible"/> to tell them apart).
    /// Every dynamic edge element (moving wall positions, phase walls, trigger-linked edges in
    /// either initial state, one-way edges) sits on an edge that is OPEN in the maze; its
    /// passability comes from the element. So for the sim: an edge is blocked if
    /// <c>Maze.HasWall</c>, otherwise ask the element on it (<see cref="EdgeElementAt"/>).
    /// </summary>
    public sealed class LevelLayout
    {
        public int Level { get; }
        public int N { get; }
        public int Seed { get; }
        public Maze Maze { get; }
        public Vector2Int Start { get; }
        public Vector2Int Destination { get; }

        /// <summary>Mechanics present in this level (canonical enum order).</summary>
        public IReadOnlyList<Mechanic> Mechanics { get; }

        /// <summary>Mechanics introduced at this level (shown on the intro card).</summary>
        public IReadOnlyList<Mechanic> NewMechanics { get; }

        public IReadOnlyList<InvisibleWall> InvisibleWalls { get; }
        public IReadOnlyList<MemoryTile> MemoryTiles { get; }
        public IReadOnlyList<MovingWall> MovingWalls { get; }
        public IReadOnlyList<PhaseWall> PhaseWalls { get; }
        public IReadOnlyList<CollapseTile> CollapseTiles { get; }
        public IReadOnlyList<TriggerPlate> TriggerPlates { get; }
        public IReadOnlyList<Teleporter> Teleporters { get; }
        public IReadOnlyList<RotatingGate> RotatingGates { get; }
        public IReadOnlyList<Patrol> Patrols { get; }
        public IReadOnlyList<OneWay> OneWays { get; }
        public IReadOnlyList<Decoy> Decoys { get; }
        public bool HasChaser { get; }

        private readonly Dictionary<Edge, EdgeElementKind> _edgeKinds = new Dictionary<Edge, EdgeElementKind>();
        private readonly Dictionary<Vector2Int, CellElementKind> _cellKinds = new Dictionary<Vector2Int, CellElementKind>();
        private readonly Dictionary<Vector2Int, MemoryTile> _memory = new Dictionary<Vector2Int, MemoryTile>();
        private readonly Dictionary<Vector2Int, CollapseTile> _collapse = new Dictionary<Vector2Int, CollapseTile>();
        private readonly Dictionary<Vector2Int, TriggerPlate> _plates = new Dictionary<Vector2Int, TriggerPlate>();
        private readonly Dictionary<Vector2Int, Teleporter> _pads = new Dictionary<Vector2Int, Teleporter>();
        private readonly Dictionary<Vector2Int, RotatingGate> _gates = new Dictionary<Vector2Int, RotatingGate>();
        private readonly Dictionary<Vector2Int, Decoy> _decoys = new Dictionary<Vector2Int, Decoy>();
        private readonly Dictionary<Edge, MovingWall> _movingByEdge = new Dictionary<Edge, MovingWall>();
        private readonly Dictionary<Edge, PhaseWall> _phaseByEdge = new Dictionary<Edge, PhaseWall>();
        private readonly Dictionary<Edge, TriggerPlate> _triggerByEdge = new Dictionary<Edge, TriggerPlate>();
        private readonly Dictionary<Edge, OneWay> _oneWayByEdge = new Dictionary<Edge, OneWay>();

        /// <summary>
        /// Creates a layout from all its parts. Lists are copied. The maze is used as is (not
        /// cloned); callers must not mutate it afterwards. Prefer <see cref="LevelLayoutBuilder"/>.
        /// If two elements claim the same cell or edge, the later one wins in the lookups.
        /// </summary>
        public LevelLayout(
            int level, int seed, Maze maze, Vector2Int start, Vector2Int destination,
            IEnumerable<Mechanic> mechanics, IEnumerable<Mechanic> newMechanics,
            IEnumerable<InvisibleWall> invisibleWalls, IEnumerable<MemoryTile> memoryTiles,
            IEnumerable<MovingWall> movingWalls, IEnumerable<PhaseWall> phaseWalls,
            IEnumerable<CollapseTile> collapseTiles, IEnumerable<TriggerPlate> triggerPlates,
            IEnumerable<Teleporter> teleporters, IEnumerable<RotatingGate> rotatingGates,
            IEnumerable<Patrol> patrols, IEnumerable<OneWay> oneWays, IEnumerable<Decoy> decoys,
            bool hasChaser)
        {
            Level = level;
            Seed = seed;
            Maze = maze;
            N = maze.N;
            Start = start;
            Destination = destination;
            Mechanics = Copy(mechanics);
            NewMechanics = Copy(newMechanics);
            InvisibleWalls = Copy(invisibleWalls);
            MemoryTiles = Copy(memoryTiles);
            MovingWalls = Copy(movingWalls);
            PhaseWalls = Copy(phaseWalls);
            CollapseTiles = Copy(collapseTiles);
            TriggerPlates = Copy(triggerPlates);
            Teleporters = Copy(teleporters);
            RotatingGates = Copy(rotatingGates);
            Patrols = Copy(patrols);
            OneWays = Copy(oneWays);
            Decoys = Copy(decoys);
            HasChaser = hasChaser;
            Index();
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> items)
        {
            return items == null ? new List<T>().AsReadOnly() : new List<T>(items).AsReadOnly();
        }

        private void Index()
        {
            foreach (var w in InvisibleWalls)
            {
                _edgeKinds[w.Edge] = EdgeElementKind.InvisibleWall;
            }

            foreach (var w in MovingWalls)
            {
                _edgeKinds[w.A] = EdgeElementKind.MovingWall;
                _edgeKinds[w.B] = EdgeElementKind.MovingWall;
                _movingByEdge[w.A] = w;
                _movingByEdge[w.B] = w;
            }

            foreach (var w in PhaseWalls)
            {
                _edgeKinds[w.Edge] = EdgeElementKind.PhaseWall;
                _phaseByEdge[w.Edge] = w;
            }

            foreach (var p in TriggerPlates)
            {
                foreach (var e in p.LinkedEdges())
                {
                    _edgeKinds[e] = EdgeElementKind.TriggerWall;
                    _triggerByEdge[e] = p;
                }
            }

            foreach (var o in OneWays)
            {
                _edgeKinds[o.Edge] = EdgeElementKind.OneWay;
                _oneWayByEdge[o.Edge] = o;
            }

            foreach (var p in Patrols)
            {
                foreach (var c in p.Path)
                {
                    _cellKinds[c] = CellElementKind.PatrolPath;
                }
            }

            foreach (var m in MemoryTiles)
            {
                _cellKinds[m.Cell] = CellElementKind.MemoryTile;
                _memory[m.Cell] = m;
            }

            foreach (var c in CollapseTiles)
            {
                _cellKinds[c.Cell] = CellElementKind.CollapseTile;
                _collapse[c.Cell] = c;
            }

            foreach (var p in TriggerPlates)
            {
                _cellKinds[p.Plate] = CellElementKind.TriggerPlate;
                _plates[p.Plate] = p;
            }

            foreach (var t in Teleporters)
            {
                _cellKinds[t.Target] = CellElementKind.TeleporterTarget;
                _cellKinds[t.Pad] = CellElementKind.TeleporterPad;
                _pads[t.Pad] = t;
            }

            foreach (var g in RotatingGates)
            {
                _cellKinds[g.Cell] = CellElementKind.RotatingGate;
                _gates[g.Cell] = g;
            }

            foreach (var d in Decoys)
            {
                _cellKinds[d.Cell] = CellElementKind.Decoy;
                _decoys[d.Cell] = d;
            }
        }

        /// <summary>True if <paramref name="e"/> is an invisible wall (it is also a wall in <see cref="Maze"/>).</summary>
        public bool IsInvisible(Edge e) => EdgeElementAt(e) == EdgeElementKind.InvisibleWall;

        /// <summary>A static wall that is drawn: a maze wall that is not invisible.</summary>
        public bool IsVisibleWall(Edge e) => Maze.HasWall(e) && !IsInvisible(e);

        public EdgeElementKind EdgeElementAt(Edge e) =>
            _edgeKinds.TryGetValue(e, out var k) ? k : EdgeElementKind.None;

        public CellElementKind CellElementAt(Vector2Int c) =>
            _cellKinds.TryGetValue(c, out var k) ? k : CellElementKind.None;

        public MovingWall MovingWallAt(Edge e) => _movingByEdge.TryGetValue(e, out var v) ? v : null;
        public PhaseWall PhaseWallAt(Edge e) => _phaseByEdge.TryGetValue(e, out var v) ? v : null;
        public TriggerPlate TriggerFor(Edge e) => _triggerByEdge.TryGetValue(e, out var v) ? v : null;
        public OneWay OneWayAt(Edge e) => _oneWayByEdge.TryGetValue(e, out var v) ? v : null;

        public MemoryTile MemoryTileAt(Vector2Int c) => _memory.TryGetValue(c, out var v) ? v : null;
        public CollapseTile CollapseTileAt(Vector2Int c) => _collapse.TryGetValue(c, out var v) ? v : null;
        public TriggerPlate TriggerPlateAt(Vector2Int c) => _plates.TryGetValue(c, out var v) ? v : null;
        public Teleporter TeleporterAt(Vector2Int pad) => _pads.TryGetValue(pad, out var v) ? v : null;
        public RotatingGate GateAt(Vector2Int c) => _gates.TryGetValue(c, out var v) ? v : null;
        public Decoy DecoyAt(Vector2Int c) => _decoys.TryGetValue(c, out var v) ? v : null;
    }
}
