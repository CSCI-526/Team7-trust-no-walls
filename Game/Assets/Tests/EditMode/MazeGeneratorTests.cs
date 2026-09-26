using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class MazeGeneratorTests
    {
        // Hand-built 3x3 maze: a single snake path visiting every cell in order
        // (0,0) (1,0) (2,0) (2,1) (1,1) (0,1) (0,2) (1,2) (2,2)
        // This is a perfect maze (n*n-1 = 8 open edges, a tree, fully connected)
        // with a known distance/shortest-path answer for every cell.
        private static Maze BuildSnakeMaze3x3()
        {
            var maze = new Maze(3);
            maze.SetWall(Edge.Between(new Vector2Int(0, 0), Dir.Right), false); // (0,0)-(1,0)
            maze.SetWall(Edge.Between(new Vector2Int(1, 0), Dir.Right), false); // (1,0)-(2,0)
            maze.SetWall(Edge.Between(new Vector2Int(2, 0), Dir.Up), false);    // (2,0)-(2,1)
            maze.SetWall(Edge.Between(new Vector2Int(2, 1), Dir.Left), false);  // (2,1)-(1,1)
            maze.SetWall(Edge.Between(new Vector2Int(1, 1), Dir.Left), false);  // (1,1)-(0,1)
            maze.SetWall(Edge.Between(new Vector2Int(0, 1), Dir.Up), false);    // (0,1)-(0,2)
            maze.SetWall(Edge.Between(new Vector2Int(0, 2), Dir.Right), false); // (0,2)-(1,2)
            maze.SetWall(Edge.Between(new Vector2Int(1, 2), Dir.Right), false); // (1,2)-(2,2)
            return maze;
        }

        [Test]
        public void GeneratePerfect_HasExactlyNSquaredMinusOneOpenInteriorEdges()
        {
            int n = 8;
            var maze = MazeGenerator.GeneratePerfect(n, new System.Random(1));
            int openCount = maze.InteriorEdges().Count(e => !maze.HasWall(e));
            Assert.AreEqual(n * n - 1, openCount);
        }

        [Test]
        public void GeneratePerfect_IsFullyConnected()
        {
            int n = 8;
            var maze = MazeGenerator.GeneratePerfect(n, new System.Random(2));
            var dist = MazeGenerator.Distances(maze, new Vector2Int(0, 0));
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    Assert.GreaterOrEqual(dist[x, y], 0, $"Cell ({x},{y}) should be reachable");
                }
            }
        }

        [Test]
        public void GeneratePerfect_IsDeterministicForTheSameSeed()
        {
            int n = 10;
            var mazeA = MazeGenerator.GeneratePerfect(n, new System.Random(12345));
            var mazeB = MazeGenerator.GeneratePerfect(n, new System.Random(12345));

            foreach (var edge in mazeA.InteriorEdges())
            {
                Assert.AreEqual(mazeA.HasWall(edge), mazeB.HasWall(edge));
            }
        }

        [Test]
        public void GeneratePerfect_DifferentSeeds_ProduceDifferentLayouts()
        {
            int n = 8;
            var mazeA = MazeGenerator.GeneratePerfect(n, new System.Random(1));
            var mazeB = MazeGenerator.GeneratePerfect(n, new System.Random(2));

            bool anyDifferent = mazeA.InteriorEdges().Any(e => mazeA.HasWall(e) != mazeB.HasWall(e));
            Assert.IsTrue(anyDifferent, "Different seeds should (almost always) produce different layouts");
        }

        [Test]
        public void Braid_RemovesExactlyCountWallsWhenEnoughAvailable()
        {
            int n = 6;
            var maze = MazeGenerator.GeneratePerfect(n, new System.Random(3));
            int wallsBefore = maze.InteriorWalls().Count();
            int openBefore = maze.InteriorEdges().Count() - wallsBefore;

            int removed = MazeGenerator.Braid(maze, 3, new System.Random(4));

            Assert.AreEqual(3, removed);
            Assert.AreEqual(wallsBefore - 3, maze.InteriorWalls().Count());
            Assert.AreEqual(openBefore + 3, maze.InteriorEdges().Count() - maze.InteriorWalls().Count());
        }

        [Test]
        public void Braid_CapsAtAvailableWallCount()
        {
            int n = 4;
            var maze = MazeGenerator.GeneratePerfect(n, new System.Random(5));
            int wallsBefore = maze.InteriorWalls().Count();

            int removed = MazeGenerator.Braid(maze, wallsBefore + 50, new System.Random(6));

            Assert.AreEqual(wallsBefore, removed);
            Assert.AreEqual(0, maze.InteriorWalls().Count());
        }

        [Test]
        public void Distances_MatchesHandBuiltSnakeMaze()
        {
            var maze = BuildSnakeMaze3x3();
            var dist = MazeGenerator.Distances(maze, new Vector2Int(0, 0));

            Assert.AreEqual(0, dist[0, 0]);
            Assert.AreEqual(1, dist[1, 0]);
            Assert.AreEqual(2, dist[2, 0]);
            Assert.AreEqual(3, dist[2, 1]);
            Assert.AreEqual(4, dist[1, 1]);
            Assert.AreEqual(5, dist[0, 1]);
            Assert.AreEqual(6, dist[0, 2]);
            Assert.AreEqual(7, dist[1, 2]);
            Assert.AreEqual(8, dist[2, 2]);
        }

        [Test]
        public void Distances_UnreachableCellsAreNegativeOne()
        {
            var maze = new Maze(3);
            maze.SetWall(Edge.Between(new Vector2Int(0, 0), Dir.Right), false);

            var dist = MazeGenerator.Distances(maze, new Vector2Int(0, 0));

            Assert.AreEqual(0, dist[0, 0]);
            Assert.AreEqual(1, dist[1, 0]);
            Assert.AreEqual(-1, dist[2, 2]);
            Assert.AreEqual(-1, dist[0, 1]);
        }

        [Test]
        public void Farthest_MatchesHandBuiltSnakeMaze()
        {
            var maze = BuildSnakeMaze3x3();
            var farthest = MazeGenerator.Farthest(maze, new Vector2Int(0, 0));
            Assert.AreEqual(new Vector2Int(2, 2), farthest);
        }

        [Test]
        public void Farthest_TiesBrokenByLowestXThenLowestY()
        {
            // Plus-shaped maze: (1,1) connects to all four neighbors, nothing else open.
            var maze = new Maze(3);
            var center = new Vector2Int(1, 1);
            maze.SetWall(Edge.Between(center, Dir.Up), false);
            maze.SetWall(Edge.Between(center, Dir.Down), false);
            maze.SetWall(Edge.Between(center, Dir.Left), false);
            maze.SetWall(Edge.Between(center, Dir.Right), false);

            // All four neighbors are at distance 1: (0,1) (2,1) (1,0) (1,2).
            // Lowest x is 0 -> (0,1).
            var farthest = MazeGenerator.Farthest(maze, center);
            Assert.AreEqual(new Vector2Int(0, 1), farthest);
        }

        [Test]
        public void ShortestPath_MatchesHandBuiltSnakeMaze()
        {
            var maze = BuildSnakeMaze3x3();
            var path = MazeGenerator.ShortestPath(maze, new Vector2Int(0, 0), new Vector2Int(2, 2));

            var expected = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(0, 1),
                new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)
            };
            CollectionAssert.AreEqual(expected, path);
        }

        [Test]
        public void ShortestPath_SameStartAndEnd_ReturnsSingleCell()
        {
            var maze = BuildSnakeMaze3x3();
            var path = MazeGenerator.ShortestPath(maze, new Vector2Int(1, 1), new Vector2Int(1, 1));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 1) }, path);
        }

        [Test]
        public void ShortestPath_Unreachable_ReturnsEmptyList()
        {
            var maze = new Maze(3);
            var path = MazeGenerator.ShortestPath(maze, new Vector2Int(0, 0), new Vector2Int(2, 2));
            Assert.IsEmpty(path);
        }

        [Test]
        public void ShortestPath_OnGeneratedMaze_ConsecutiveCellsAreAdjacentThroughOpenEdges()
        {
            int n = 9;
            var maze = MazeGenerator.GeneratePerfect(n, new System.Random(7));
            var a = new Vector2Int(0, 0);
            var b = MazeGenerator.Farthest(maze, a);

            var path = MazeGenerator.ShortestPath(maze, a, b);

            Assert.IsNotEmpty(path);
            Assert.AreEqual(a, path[0]);
            Assert.AreEqual(b, path[path.Count - 1]);

            for (int i = 0; i < path.Count - 1; i++)
            {
                var cur = path[i];
                var next = path[i + 1];
                int manhattan = Mathf.Abs(next.x - cur.x) + Mathf.Abs(next.y - cur.y);
                Assert.AreEqual(1, manhattan, $"Step {i} from {cur} to {next} is not to an orthogonal neighbor");
                Assert.IsTrue(maze.OpenNeighbors(cur).Contains(next), $"Step {i} from {cur} to {next} crosses a wall");
            }
        }

        [Test]
        public void IsBridge_FalseWhenEdgeIsAWall()
        {
            var maze = new Maze(3);
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            Assert.IsFalse(MazeGenerator.IsBridge(maze, edge));
        }

        [Test]
        public void IsBridge_TrueForEveryOpenEdgeOfAPerfectMaze()
        {
            int n = 6;
            var maze = MazeGenerator.GeneratePerfect(n, new System.Random(8));
            foreach (var edge in maze.InteriorEdges().Where(e => !maze.HasWall(e)))
            {
                Assert.IsTrue(MazeGenerator.IsBridge(maze, edge), $"Every open edge of a perfect maze (a tree) must be a bridge: {edge.A}-{edge.B}");
            }
        }

        [Test]
        public void IsBridge_FalseForAnEdgeOnALoop()
        {
            var maze = BuildSnakeMaze3x3();
            // Adds (1,0)-(1,1), closing a loop with the existing (1,0)-(2,0)-(2,1)-(1,1) path.
            var loopEdge = Edge.Between(new Vector2Int(1, 0), Dir.Up);
            maze.SetWall(loopEdge, false);

            Assert.IsFalse(MazeGenerator.IsBridge(maze, loopEdge));
            foreach (var e in new[]
                     {
                         Edge.Between(new Vector2Int(1, 0), Dir.Right),
                         Edge.Between(new Vector2Int(2, 0), Dir.Up),
                         Edge.Between(new Vector2Int(2, 1), Dir.Left)
                     })
            {
                Assert.IsFalse(MazeGenerator.IsBridge(maze, e), $"Edge on the loop should not be a bridge: {e.A}-{e.B}");
            }

            // Edges outside the loop remain bridges.
            Assert.IsTrue(MazeGenerator.IsBridge(maze, Edge.Between(new Vector2Int(0, 0), Dir.Right)));
            Assert.IsTrue(MazeGenerator.IsBridge(maze, Edge.Between(new Vector2Int(1, 1), Dir.Left)));
            Assert.IsTrue(MazeGenerator.IsBridge(maze, Edge.Between(new Vector2Int(1, 2), Dir.Right)));
        }
    }
}
