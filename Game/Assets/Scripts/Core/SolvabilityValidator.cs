using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// Conservative solvability check. Two directed graphs over cells share these rules:
    /// maze walls (visible and invisible) block; periodic blockers (moving walls, phase walls,
    /// rotating gates, patrols, temporary collapsing tiles) are passable, since waiting works;
    /// one-way edges are directed; a teleporter pad's only exit is its target (entering the pad =
    /// arriving at the target); a decoy's only exit is Start; the Destination is a sink.
    /// They differ on the two state-dependent features:
    /// - R (cells the player might reach from Start) is computed OPTIMISTICALLY: trigger-linked
    ///   edges and intact permanent collapsing tiles are passable;
    /// - D (cells that can surely reach the Destination) is computed PESSIMISTICALLY: trigger-linked
    ///   edges are blocked in both states and permanent collapsing tiles are removed.
    /// The level is solvable iff Destination is in R and every cell of R except the permanent
    /// collapsing tiles themselves is in D. This rejects pockets behind a permanent tile or behind a
    /// trigger edge that could seal the player in. (This deliberately strengthens the spec's
    /// literal model to meet its goal: no traps anywhere reachable.)
    /// </summary>
    public static class SolvabilityValidator
    {
        public static bool IsSolvable(LevelLayout layout)
        {
            return Check(layout.Maze, layout.Start, layout.Destination, layout.TriggerPlates,
                layout.OneWays, layout.CollapseTiles, layout.Teleporters, layout.Decoys);
        }

        /// <summary>Same check on an unfinished builder (avoids building a layout per try).</summary>
        internal static bool IsSolvable(LevelLayoutBuilder b)
        {
            return Check(b.Maze, b.Start, b.Destination, b.TriggerPlates, b.OneWays, b.CollapseTiles,
                b.Teleporters, b.Decoys);
        }

        private static bool Check(
            Maze maze, Vector2Int start, Vector2Int destination,
            IEnumerable<TriggerPlate> triggers, IEnumerable<OneWay> oneWays,
            IEnumerable<CollapseTile> collapses, IEnumerable<Teleporter> teleporters,
            IEnumerable<Decoy> decoys)
        {
            int n = maze.N;
            if (!maze.InBounds(start) || !maze.InBounds(destination))
            {
                return false;
            }

            var blocked = new HashSet<Edge>();
            foreach (var p in triggers)
            {
                foreach (var e in p.LinkedEdges())
                {
                    blocked.Add(e);
                }
            }

            var oneWay = new Dictionary<Edge, Dir>();
            foreach (var o in oneWays)
            {
                oneWay[o.Edge] = o.Allowed;
            }

            var removed = new bool[n, n];
            foreach (var c in collapses)
            {
                if (c.Permanent)
                {
                    removed[c.Cell.x, c.Cell.y] = true;
                }
            }

            // Cells whose only exit is a forced jump (teleporter pad -> target, decoy -> Start).
            var jump = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var t in teleporters)
            {
                jump[t.Pad] = t.Target;
            }

            foreach (var d in decoys)
            {
                jump[d.Cell] = start;
            }

            if (removed[start.x, start.y] || removed[destination.x, destination.y])
            {
                return false;
            }

            // Arcs as parallel arrays. "Opt" arcs build R, "Pes" arcs build D.
            int count = n * n;
            var optFrom = new List<int>(count * 4);
            var optTo = new List<int>(count * 4);
            var pesFrom = new List<int>(count * 4);
            var pesTo = new List<int>(count * 4);

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    var cell = new Vector2Int(x, y);

                    // Arriving on the Destination completes the level: it is a sink.
                    if (cell == destination)
                    {
                        continue;
                    }

                    int from = x * n + y;
                    bool fromRemoved = removed[x, y];
                    if (jump.TryGetValue(cell, out var to))
                    {
                        if (maze.InBounds(to))
                        {
                            int toIndex = to.x * n + to.y;
                            optFrom.Add(from);
                            optTo.Add(toIndex);
                            if (!fromRemoved && !removed[to.x, to.y])
                            {
                                pesFrom.Add(from);
                                pesTo.Add(toIndex);
                            }
                        }

                        continue;
                    }

                    foreach (var d in DirUtil.All)
                    {
                        if (maze.HasWall(cell, d))
                        {
                            continue;
                        }

                        var edge = Edge.Between(cell, d);
                        if (oneWay.TryGetValue(edge, out var allowed) && allowed != d)
                        {
                            continue;
                        }

                        var next = cell + DirUtil.Delta(d);
                        int toIndex = next.x * n + next.y;
                        optFrom.Add(from);
                        optTo.Add(toIndex);
                        if (!fromRemoved && !removed[next.x, next.y] && !blocked.Contains(edge))
                        {
                            pesFrom.Add(from);
                            pesTo.Add(toIndex);
                        }
                    }
                }
            }

            var reach = Flood(optFrom, optTo, start.x * n + start.y, count);
            int destIndex = destination.x * n + destination.y;
            if (!reach[destIndex])
            {
                return false;
            }

            var canReachDest = Flood(pesTo, pesFrom, destIndex, count);
            for (int i = 0; i < count; i++)
            {
                if (reach[i] && !canReachDest[i] && !removed[i / n, i % n])
                {
                    return false;
                }
            }

            return true;
        }

        // Flood fill over arcs tail -> head (pass them swapped to flood the reverse graph).
        private static bool[] Flood(List<int> tails, List<int> heads, int from, int count)
        {
            var offsets = new int[count + 1];
            foreach (int t in tails)
            {
                offsets[t + 1]++;
            }

            for (int i = 0; i < count; i++)
            {
                offsets[i + 1] += offsets[i];
            }

            var fill = new int[count];
            var targets = new int[tails.Count];
            for (int k = 0; k < tails.Count; k++)
            {
                int t = tails[k];
                targets[offsets[t] + fill[t]++] = heads[k];
            }

            var seen = new bool[count];
            var stack = new int[count];
            int top = 0;
            seen[from] = true;
            stack[top++] = from;
            while (top > 0)
            {
                int cur = stack[--top];
                for (int k = offsets[cur]; k < offsets[cur + 1]; k++)
                {
                    int next = targets[k];
                    if (!seen[next])
                    {
                        seen[next] = true;
                        stack[top++] = next;
                    }
                }
            }

            return seen;
        }
    }
}
