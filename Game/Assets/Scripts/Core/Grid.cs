using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// The four grid-aligned movement directions.
    /// </summary>
    public enum Dir
    {
        Up,
        Right,
        Down,
        Left
    }

    /// <summary>
    /// Helpers for working with <see cref="Dir"/> values.
    /// </summary>
    public static class DirUtil
    {
        /// <summary>All four directions, in a stable order.</summary>
        public static readonly IReadOnlyList<Dir> All = new[] { Dir.Up, Dir.Right, Dir.Down, Dir.Left };

        /// <summary>The unit cell-space offset for stepping one cell in direction <paramref name="d"/>.</summary>
        public static Vector2Int Delta(Dir d)
        {
            switch (d)
            {
                case Dir.Up:
                    return new Vector2Int(0, 1);
                case Dir.Right:
                    return new Vector2Int(1, 0);
                case Dir.Down:
                    return new Vector2Int(0, -1);
                case Dir.Left:
                    return new Vector2Int(-1, 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(d), d, null);
            }
        }

        /// <summary>The direction opposite <paramref name="d"/>.</summary>
        public static Dir Opposite(Dir d)
        {
            switch (d)
            {
                case Dir.Up:
                    return Dir.Down;
                case Dir.Right:
                    return Dir.Left;
                case Dir.Down:
                    return Dir.Up;
                case Dir.Left:
                    return Dir.Right;
                default:
                    throw new ArgumentOutOfRangeException(nameof(d), d, null);
            }
        }
    }

    /// <summary>
    /// A wall/passage location between two orthogonally adjacent cells, stored in a canonical
    /// form so the same edge compares equal regardless of which side it was constructed from.
    /// The canonical form is the lower-left cell plus an orientation: <see cref="IsVertical"/> is
    /// true for the edge between (x, y) and (x + 1, y) (a vertical wall segment), and false for
    /// the edge between (x, y) and (x, y + 1) (a horizontal wall segment).
    /// </summary>
    public readonly struct Edge : IEquatable<Edge>
    {
        /// <summary>The lower-left cell of the canonical pair.</summary>
        private readonly Vector2Int _lower;

        /// <summary>True when this edge separates cells that differ in x (a vertical wall segment).</summary>
        public bool IsVertical { get; }

        /// <summary>One of the two cells this edge separates (the canonical lower-left cell).</summary>
        public Vector2Int A => _lower;

        /// <summary>The other cell this edge separates.</summary>
        public Vector2Int B => IsVertical ? _lower + new Vector2Int(1, 0) : _lower + new Vector2Int(0, 1);

        private Edge(Vector2Int lower, bool isVertical)
        {
            _lower = lower;
            IsVertical = isVertical;
        }

        /// <summary>
        /// Builds the canonical <see cref="Edge"/> between cell <paramref name="a"/> and its
        /// neighbor in direction <paramref name="d"/>. Assumes both <paramref name="a"/> and that
        /// neighbor are in bounds; this method has no maze size to check against, so callers must
        /// verify bounds themselves (see <see cref="Maze.HasWall(Vector2Int, Dir)"/>, which checks
        /// bounds before ever constructing an Edge).
        /// </summary>
        public static Edge Between(Vector2Int a, Dir d)
        {
            Vector2Int b = a + DirUtil.Delta(d);
            Vector2Int lower = new Vector2Int(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
            bool isVertical = a.y == b.y; // cells differ only in x -> a vertical wall segment
            return new Edge(lower, isVertical);
        }

        public bool Equals(Edge other) => _lower.Equals(other._lower) && IsVertical == other.IsVertical;

        public override bool Equals(object obj) => obj is Edge other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _lower.GetHashCode();
                hash = (hash * 397) ^ IsVertical.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Edge left, Edge right) => left.Equals(right);

        public static bool operator !=(Edge left, Edge right) => !left.Equals(right);

        /// <summary>The world-space center of cell <paramref name="c"/> in an N x N maze centered at the origin.</summary>
        public static Vector2 CellCenter(Vector2Int c, int n)
        {
            float offset = (n - 1) / 2f;
            return new Vector2(c.x - offset, c.y - offset);
        }

        /// <summary>The world-space midpoint between the two cells this edge separates.</summary>
        public Vector2 WorldCenter(int n)
        {
            return (CellCenter(A, n) + CellCenter(B, n)) / 2f;
        }

        /// <summary>
        /// Chebyshev distance from this edge to <paramref name="cell"/>: the closer of its two
        /// endpoints. The single shared rule for "is this edge within range of that cell", used by
        /// both memory tile reveal (<see cref="LevelSim"/>) and memory tile placement
        /// (<see cref="ElementPlacer"/>) so the two can never disagree, and by warning proximity.
        /// </summary>
        public int ChebyshevDistanceTo(Vector2Int cell)
        {
            return Math.Min(Chebyshev(A, cell), Chebyshev(B, cell));
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b) =>
            Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));
    }
}
