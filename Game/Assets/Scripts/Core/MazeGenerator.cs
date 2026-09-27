using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// Procedural maze generation and graph queries over a <see cref="Maze"/>.
    /// All randomness is driven by a caller-supplied <see cref="System.Random"/> for determinism.
    /// </summary>
    public static class MazeGenerator
    {
        /// <summary>
        /// Generates a perfect (single-solution, fully connected, no loops) N x N maze using an
        /// iterative recursive-backtracker DFS starting from cell (0, 0).
        /// </summary>
        public static Maze GeneratePerfect(int n, System.Random rng)
        {
            var maze = new Maze(n);
            var visited = new bool[n, n];
            var stack = new Stack<Vector2Int>();
            var candidates = new List<Dir>(4);

            var start = new Vector2Int(0, 0);
            visited[start.x, start.y] = true;
            stack.Push(start);

            while (stack.Count > 0)
            {
                var current = stack.Peek();

                candidates.Clear();
                foreach (var d in DirUtil.All)
                {
                    var next = current + DirUtil.Delta(d);
                    if (maze.InBounds(next) && !visited[next.x, next.y])
                    {
                        candidates.Add(d);
                    }
                }

                if (candidates.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var chosenDir = candidates[rng.Next(candidates.Count)];
                var chosenCell = current + DirUtil.Delta(chosenDir);

                maze.SetWall(Edge.Between(current, chosenDir), false);
                visited[chosenCell.x, chosenCell.y] = true;
                stack.Push(chosenCell);
            }

            return maze;
        }

        /// <summary>
        /// Removes up to <paramref name="count"/> interior walls chosen uniformly at random among
        /// the maze's current interior walls (never the boundary), creating loops. Returns the
        /// number of walls actually removed (less than <paramref name="count"/> if there were
        /// fewer walls available).
        /// </summary>
        public static int Braid(Maze m, int count, System.Random rng)
        {
            var walls = new List<Edge>(m.InteriorWalls());
            int removed = 0;

            while (removed < count && walls.Count > 0)
            {
                int index = rng.Next(walls.Count);
                var edge = walls[index];

                // Swap-remove: order doesn't matter, this keeps removal O(1).
                int lastIndex = walls.Count - 1;
                walls[index] = walls[lastIndex];
                walls.RemoveAt(lastIndex);

                m.SetWall(edge, false);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// BFS distance in steps from <paramref name="from"/> to every cell, traveling only
        /// through open edges. Unreachable cells (including any out-of-bounds indices, which
        /// cannot occur for a valid maze) are -1.
        /// </summary>
        public static int[,] Distances(Maze m, Vector2Int from)
        {
            int n = m.N;
            var dist = new int[n, n];
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    dist[x, y] = -1;
                }
            }

            if (!m.InBounds(from))
            {
                return dist;
            }

            var queue = new Queue<Vector2Int>();
            dist[from.x, from.y] = 0;
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int currentDist = dist[current.x, current.y];

                foreach (var next in m.OpenNeighbors(current))
                {
                    if (dist[next.x, next.y] == -1)
                    {
                        dist[next.x, next.y] = currentDist + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            return dist;
        }

        /// <summary>
        /// The cell with the greatest BFS distance from <paramref name="from"/>, ties broken by
        /// lowest x then lowest y.
        /// </summary>
        public static Vector2Int Farthest(Maze m, Vector2Int from)
        {
            var dist = Distances(m, from);
            int n = m.N;

            var best = from;
            int bestDist = -1;

            // Iterating x ascending outer, y ascending inner means the first cell we see at the
            // maximum distance is already the lowest-x, then lowest-y one, since we only ever
            // move `best` on a strict improvement.
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    if (dist[x, y] > bestDist)
                    {
                        bestDist = dist[x, y];
                        best = new Vector2Int(x, y);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The cell with the greatest BFS distance from <paramref name="from"/> among cells whose
        /// Manhattan distance from it is at least <paramref name="minManhattan"/> (ties: lowest x,
        /// then lowest y). Falls back to <see cref="Farthest"/> if no reachable cell qualifies.
        /// </summary>
        public static Vector2Int FarthestAtLeast(Maze m, Vector2Int from, int minManhattan)
        {
            var dist = Distances(m, from);
            int n = m.N;

            var best = from;
            int bestDist = -1;
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    int manhattan = Mathf.Abs(x - from.x) + Mathf.Abs(y - from.y);
                    if (manhattan >= minManhattan && dist[x, y] > bestDist)
                    {
                        bestDist = dist[x, y];
                        best = new Vector2Int(x, y);
                    }
                }
            }

            return bestDist > 0 ? best : Farthest(m, from);
        }

        /// <summary>
        /// The shortest path from <paramref name="a"/> to <paramref name="b"/> through open
        /// edges, including both endpoints, or an empty list if <paramref name="b"/> is
        /// unreachable from <paramref name="a"/>.
        /// </summary>
        public static List<Vector2Int> ShortestPath(Maze m, Vector2Int a, Vector2Int b)
        {
            var result = new List<Vector2Int>();
            if (!m.InBounds(a) || !m.InBounds(b))
            {
                return result;
            }

            int n = m.N;
            var visited = new bool[n, n];
            var prev = new Vector2Int?[n, n];
            var queue = new Queue<Vector2Int>();

            visited[a.x, a.y] = true;
            queue.Enqueue(a);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == b)
                {
                    break;
                }

                foreach (var next in m.OpenNeighbors(current))
                {
                    if (!visited[next.x, next.y])
                    {
                        visited[next.x, next.y] = true;
                        prev[next.x, next.y] = current;
                        queue.Enqueue(next);
                    }
                }
            }

            if (!visited[b.x, b.y])
            {
                return result;
            }

            var path = new List<Vector2Int>();
            Vector2Int? node = b;
            while (node.HasValue)
            {
                path.Add(node.Value);
                node = prev[node.Value.x, node.Value.y];
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// True if <paramref name="e"/> is an open edge whose removal would disconnect its two
        /// cells from each other. False if <paramref name="e"/> is a wall.
        /// </summary>
        public static bool IsBridge(Maze m, Edge e)
        {
            if (m.HasWall(e))
            {
                return false;
            }

            var clone = m.Clone();
            clone.SetWall(e, true);

            var dist = Distances(clone, e.A);
            return dist[e.B.x, e.B.y] == -1;
        }
    }
}
