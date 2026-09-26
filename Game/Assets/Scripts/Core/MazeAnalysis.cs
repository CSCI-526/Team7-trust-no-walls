using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// A dead-end branch: a corridor of degree-2 cells ending in a degree-1 cell, hanging off a
    /// junction (a cell of degree 3 or more).
    /// </summary>
    internal sealed class DeadEndBranch
    {
        /// <summary>The junction cell the branch attaches to (not part of <see cref="Cells"/>).</summary>
        public Vector2Int Junction { get; }

        /// <summary>Branch cells ordered from the junction outward; the last one is the dead end.</summary>
        public IReadOnlyList<Vector2Int> Cells { get; }

        public Vector2Int DeadEnd => Cells[Cells.Count - 1];
        public int Length => Cells.Count;

        public DeadEndBranch(Vector2Int junction, List<Vector2Int> cells)
        {
            Junction = junction;
            Cells = cells;
        }
    }

    /// <summary>Graph helpers over a <see cref="Maze"/> used by level generation.</summary>
    internal static class MazeAnalysis
    {
        public static int Degree(Maze m, Vector2Int c)
        {
            int d = 0;
            foreach (var dir in DirUtil.All)
            {
                if (!m.HasWall(c, dir))
                {
                    d++;
                }
            }

            return d;
        }

        /// <summary>
        /// Every dead-end branch of <paramref name="m"/>. A pure path component (no junction) yields none.
        /// </summary>
        public static List<DeadEndBranch> DeadEndBranches(Maze m)
        {
            var result = new List<DeadEndBranch>();
            for (int x = 0; x < m.N; x++)
            {
                for (int y = 0; y < m.N; y++)
                {
                    var end = new Vector2Int(x, y);
                    if (Degree(m, end) != 1)
                    {
                        continue;
                    }

                    var cells = new List<Vector2Int> { end };
                    var prev = end;
                    var cur = FirstNeighborExcept(m, end, end);
                    while (Degree(m, cur) == 2)
                    {
                        cells.Add(cur);
                        var next = FirstNeighborExcept(m, cur, prev);
                        prev = cur;
                        cur = next;
                    }

                    if (Degree(m, cur) >= 3)
                    {
                        cells.Reverse();
                        result.Add(new DeadEndBranch(cur, cells));
                    }
                }
            }

            return result;
        }

        private static Vector2Int FirstNeighborExcept(Maze m, Vector2Int c, Vector2Int except)
        {
            foreach (var nb in m.OpenNeighbors(c))
            {
                if (nb != except)
                {
                    return nb;
                }
            }

            return c;
        }

        /// <summary>The open edges between consecutive cells of a path.</summary>
        public static List<Edge> PathEdges(IReadOnlyList<Vector2Int> path)
        {
            var edges = new List<Edge>();
            for (int i = 0; i + 1 < path.Count; i++)
            {
                edges.Add(LevelLayoutBuilder.EdgeBetweenCells(path[i], path[i + 1]));
            }

            return edges;
        }

        /// <summary>The direction of travel from cell <paramref name="a"/> to adjacent cell <paramref name="b"/>.</summary>
        public static Dir DirTo(Vector2Int a, Vector2Int b)
        {
            foreach (var d in DirUtil.All)
            {
                if (a + DirUtil.Delta(d) == b)
                {
                    return d;
                }
            }

            throw new System.ArgumentException($"Cells {a} and {b} are not adjacent.");
        }
    }
}
