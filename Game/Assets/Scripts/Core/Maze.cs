using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// An N x N grid of cells with walls on the edges between them. A freshly constructed maze
    /// has every wall present, including the boundary (which is always solid and is never
    /// represented as an <see cref="Edge"/>).
    /// </summary>
    public sealed class Maze
    {
        // An edge is "open" (no wall) iff it is present in this set. Absence means walled,
        // which matches the "all walls present" starting state for free.
        private readonly HashSet<Edge> _openEdges;

        public int N { get; }

        public Maze(int n)
        {
            N = n;
            _openEdges = new HashSet<Edge>();
        }

        private Maze(int n, HashSet<Edge> openEdges)
        {
            N = n;
            _openEdges = new HashSet<Edge>(openEdges);
        }

        /// <summary>True when <paramref name="c"/> is within the [0, N) x [0, N) grid.</summary>
        public bool InBounds(Vector2Int c) => c.x >= 0 && c.x < N && c.y >= 0 && c.y < N;

        /// <summary>
        /// True if there is a wall on the edge between <paramref name="c"/> and its neighbor in
        /// direction <paramref name="d"/>. Returns true (a wall) whenever that neighbor would be
        /// out of bounds, since the outer boundary is always solid.
        /// </summary>
        public bool HasWall(Vector2Int c, Dir d)
        {
            Vector2Int neighbor = c + DirUtil.Delta(d);
            if (!InBounds(c) || !InBounds(neighbor))
            {
                return true;
            }

            return HasWall(Edge.Between(c, d));
        }

        /// <summary>True if there is a wall on interior edge <paramref name="e"/>.</summary>
        public bool HasWall(Edge e) => !_openEdges.Contains(e);

        /// <summary>Sets whether interior edge <paramref name="e"/> has a wall.</summary>
        public void SetWall(Edge e, bool hasWall)
        {
            if (hasWall)
            {
                _openEdges.Remove(e);
            }
            else
            {
                _openEdges.Add(e);
            }
        }

        /// <summary>Every interior edge of the maze (not including the boundary).</summary>
        public IEnumerable<Edge> InteriorEdges()
        {
            for (int x = 0; x < N; x++)
            {
                for (int y = 0; y < N; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (x + 1 < N)
                    {
                        yield return Edge.Between(cell, Dir.Right);
                    }

                    if (y + 1 < N)
                    {
                        yield return Edge.Between(cell, Dir.Up);
                    }
                }
            }
        }

        /// <summary>Every interior edge that currently has a wall.</summary>
        public IEnumerable<Edge> InteriorWalls()
        {
            foreach (var edge in InteriorEdges())
            {
                if (HasWall(edge))
                {
                    yield return edge;
                }
            }
        }

        /// <summary>The neighbors of <paramref name="c"/> reachable through an open edge.</summary>
        public IEnumerable<Vector2Int> OpenNeighbors(Vector2Int c)
        {
            foreach (var d in DirUtil.All)
            {
                if (!HasWall(c, d))
                {
                    yield return c + DirUtil.Delta(d);
                }
            }
        }

        /// <summary>An independent deep copy of this maze.</summary>
        public Maze Clone() => new Maze(N, _openEdges);
    }
}
