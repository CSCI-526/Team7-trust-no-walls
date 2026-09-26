using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// Convenient construction of a <see cref="LevelLayout"/>, mainly for hand-made test levels:
    /// <c>new LevelLayoutBuilder(maze).WithStart(s).WithDestination(d).Add(new OneWay(e, Dir.Up)).Build()</c>.
    /// Defaults: Start (0, 0), Destination (N-1, N-1), Level N-3, Seed 0, no chaser. Unless
    /// <see cref="WithMechanics"/> is called, Mechanics is inferred from the elements added and
    /// NewMechanics is empty. No placement rules are enforced here. The maze is cloned by
    /// <see cref="Build"/>, so the builder can keep being edited afterwards.
    /// Remember the Maze invariant (see <see cref="LevelLayout"/>): dynamic edge elements belong on open edges.
    /// </summary>
    public sealed class LevelLayoutBuilder
    {
        private readonly Maze _maze;
        private int? _level;
        private int _seed;
        private Vector2Int _start;
        private Vector2Int _destination;
        private List<Mechanic> _mechanics;
        private List<Mechanic> _newMechanics = new List<Mechanic>();
        private bool _hasChaser;

        internal readonly List<InvisibleWall> InvisibleWalls = new List<InvisibleWall>();
        internal readonly List<MemoryTile> MemoryTiles = new List<MemoryTile>();
        internal readonly List<MovingWall> MovingWalls = new List<MovingWall>();
        internal readonly List<PhaseWall> PhaseWalls = new List<PhaseWall>();
        internal readonly List<CollapseTile> CollapseTiles = new List<CollapseTile>();
        internal readonly List<TriggerPlate> TriggerPlates = new List<TriggerPlate>();
        internal readonly List<Teleporter> Teleporters = new List<Teleporter>();
        internal readonly List<RotatingGate> RotatingGates = new List<RotatingGate>();
        internal readonly List<Patrol> Patrols = new List<Patrol>();
        internal readonly List<OneWay> OneWays = new List<OneWay>();
        internal readonly List<Decoy> Decoys = new List<Decoy>();

        public LevelLayoutBuilder(Maze maze)
        {
            _maze = maze ?? throw new ArgumentNullException(nameof(maze));
            _start = new Vector2Int(0, 0);
            _destination = new Vector2Int(maze.N - 1, maze.N - 1);
        }

        /// <summary>The maze being built on (live; edits affect the next Build).</summary>
        public Maze Maze => _maze;
        public Vector2Int Start => _start;
        public Vector2Int Destination => _destination;
        public bool HasChaser => _hasChaser;

        public LevelLayoutBuilder WithLevel(int level) { _level = level; return this; }
        public LevelLayoutBuilder WithSeed(int seed) { _seed = seed; return this; }
        public LevelLayoutBuilder WithStart(Vector2Int start) { _start = start; return this; }
        public LevelLayoutBuilder WithDestination(Vector2Int destination) { _destination = destination; return this; }
        public LevelLayoutBuilder WithChaser(bool hasChaser = true) { _hasChaser = hasChaser; return this; }

        /// <summary>Sets Mechanics and NewMechanics explicitly instead of inferring them.</summary>
        public LevelLayoutBuilder WithMechanics(IEnumerable<Mechanic> present, IEnumerable<Mechanic> introduced = null)
        {
            _mechanics = new List<Mechanic>(present);
            _newMechanics = introduced == null ? new List<Mechanic>() : new List<Mechanic>(introduced);
            return this;
        }

        public LevelLayoutBuilder Add(InvisibleWall e) { InvisibleWalls.Add(e); return this; }
        public LevelLayoutBuilder Add(MemoryTile e) { MemoryTiles.Add(e); return this; }
        public LevelLayoutBuilder Add(MovingWall e) { MovingWalls.Add(e); return this; }
        public LevelLayoutBuilder Add(PhaseWall e) { PhaseWalls.Add(e); return this; }
        public LevelLayoutBuilder Add(CollapseTile e) { CollapseTiles.Add(e); return this; }
        public LevelLayoutBuilder Add(TriggerPlate e) { TriggerPlates.Add(e); return this; }
        public LevelLayoutBuilder Add(Teleporter e) { Teleporters.Add(e); return this; }
        public LevelLayoutBuilder Add(RotatingGate e) { RotatingGates.Add(e); return this; }
        public LevelLayoutBuilder Add(Patrol e) { Patrols.Add(e); return this; }
        public LevelLayoutBuilder Add(OneWay e) { OneWays.Add(e); return this; }
        public LevelLayoutBuilder Add(Decoy e) { Decoys.Add(e); return this; }

        /// <summary>Opens (or closes) the edge between <paramref name="cell"/> and its neighbor in <paramref name="d"/>.</summary>
        public LevelLayoutBuilder Open(Vector2Int cell, Dir d, bool open = true)
        {
            _maze.SetWall(Edge.Between(cell, d), !open);
            return this;
        }

        /// <summary>Opens every edge between consecutive cells of <paramref name="cells"/> (a corridor).</summary>
        public LevelLayoutBuilder Carve(params Vector2Int[] cells)
        {
            for (int i = 0; i + 1 < cells.Length; i++)
            {
                _maze.SetWall(EdgeBetweenCells(cells[i], cells[i + 1]), false);
            }

            return this;
        }

        /// <summary>The edge between two orthogonally adjacent cells.</summary>
        public static Edge EdgeBetweenCells(Vector2Int a, Vector2Int b)
        {
            foreach (var d in DirUtil.All)
            {
                if (a + DirUtil.Delta(d) == b)
                {
                    return Edge.Between(a, d);
                }
            }

            throw new ArgumentException($"Cells {a} and {b} are not adjacent.");
        }

        /// <summary>The mechanics that currently have at least one element (canonical order).</summary>
        public List<Mechanic> InferMechanics()
        {
            var list = new List<Mechanic>();
            if (InvisibleWalls.Count > 0) list.Add(Mechanic.InvisibleWalls);
            if (MemoryTiles.Count > 0) list.Add(Mechanic.MemoryTiles);
            if (MovingWalls.Count > 0) list.Add(Mechanic.MovingWalls);
            if (PhaseWalls.Count > 0) list.Add(Mechanic.DisappearingWalls);
            if (CollapseTiles.Count > 0) list.Add(Mechanic.CollapsingTiles);
            if (TriggerPlates.Count > 0) list.Add(Mechanic.TriggerWalls);
            if (Teleporters.Count > 0) list.Add(Mechanic.Teleporters);
            if (RotatingGates.Count > 0) list.Add(Mechanic.RotatingBarriers);
            if (Patrols.Count > 0) list.Add(Mechanic.Patrols);
            if (OneWays.Count > 0) list.Add(Mechanic.OneWayPaths);
            if (Decoys.Count > 0) list.Add(Mechanic.Decoys);
            if (_hasChaser) list.Add(Mechanic.Chaser);
            return list;
        }

        /// <summary>Number of elements added for a mechanic (1 for the chaser when enabled).</summary>
        public int CountOf(Mechanic m)
        {
            switch (m)
            {
                case Mechanic.InvisibleWalls: return InvisibleWalls.Count;
                case Mechanic.MemoryTiles: return MemoryTiles.Count;
                case Mechanic.MovingWalls: return MovingWalls.Count;
                case Mechanic.DisappearingWalls: return PhaseWalls.Count;
                case Mechanic.CollapsingTiles: return CollapseTiles.Count;
                case Mechanic.TriggerWalls: return TriggerPlates.Count;
                case Mechanic.Teleporters: return Teleporters.Count;
                case Mechanic.RotatingBarriers: return RotatingGates.Count;
                case Mechanic.Patrols: return Patrols.Count;
                case Mechanic.OneWayPaths: return OneWays.Count;
                case Mechanic.Decoys: return Decoys.Count;
                case Mechanic.Chaser: return _hasChaser ? 1 : 0;
                default: throw new ArgumentOutOfRangeException(nameof(m), m, null);
            }
        }

        /// <summary>Builds an immutable layout from the current state (the maze is cloned).</summary>
        public LevelLayout Build()
        {
            return new LevelLayout(
                _level ?? _maze.N - 3, _seed, _maze.Clone(), _start, _destination,
                _mechanics ?? InferMechanics(), _newMechanics,
                InvisibleWalls, MemoryTiles, MovingWalls, PhaseWalls, CollapseTiles, TriggerPlates,
                Teleporters, RotatingGates, Patrols, OneWays, Decoys, _hasChaser);
        }
    }
}
