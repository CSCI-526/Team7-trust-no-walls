using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class MazeTests
    {
        [Test]
        public void Constructor_AllWallsPresentInitially()
        {
            var maze = new Maze(4);
            foreach (var edge in maze.InteriorEdges())
            {
                Assert.IsTrue(maze.HasWall(edge));
            }
        }

        [Test]
        public void N_ReturnsConstructorValue()
        {
            var maze = new Maze(7);
            Assert.AreEqual(7, maze.N);
        }

        [Test]
        public void InBounds_TrueInsideFalseOutside()
        {
            var maze = new Maze(3);
            Assert.IsTrue(maze.InBounds(new Vector2Int(0, 0)));
            Assert.IsTrue(maze.InBounds(new Vector2Int(2, 2)));
            Assert.IsFalse(maze.InBounds(new Vector2Int(-1, 0)));
            Assert.IsFalse(maze.InBounds(new Vector2Int(3, 0)));
            Assert.IsFalse(maze.InBounds(new Vector2Int(0, 3)));
            Assert.IsFalse(maze.InBounds(new Vector2Int(0, -1)));
        }

        [Test]
        public void HasWall_BoundaryDirection_IsTrue()
        {
            var maze = new Maze(3);
            Assert.IsTrue(maze.HasWall(new Vector2Int(0, 0), Dir.Down));
            Assert.IsTrue(maze.HasWall(new Vector2Int(0, 0), Dir.Left));
            Assert.IsTrue(maze.HasWall(new Vector2Int(2, 2), Dir.Up));
            Assert.IsTrue(maze.HasWall(new Vector2Int(2, 2), Dir.Right));
        }

        [Test]
        public void SetWall_OpensAndClosesEdge()
        {
            var maze = new Maze(3);
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);

            Assert.IsTrue(maze.HasWall(edge));

            maze.SetWall(edge, false);
            Assert.IsFalse(maze.HasWall(edge));
            Assert.IsFalse(maze.HasWall(new Vector2Int(0, 0), Dir.Right));
            Assert.IsFalse(maze.HasWall(new Vector2Int(1, 0), Dir.Left));

            maze.SetWall(edge, true);
            Assert.IsTrue(maze.HasWall(edge));
        }

        [Test]
        public void InteriorEdges_CountMatchesFormula()
        {
            int n = 5;
            var maze = new Maze(n);
            int expected = 2 * n * (n - 1);
            Assert.AreEqual(expected, maze.InteriorEdges().Count());
        }

        [Test]
        public void InteriorWalls_InitiallyEqualsAllInteriorEdges()
        {
            var maze = new Maze(4);
            Assert.AreEqual(maze.InteriorEdges().Count(), maze.InteriorWalls().Count());
        }

        [Test]
        public void InteriorWalls_ExcludesOpenedEdges()
        {
            var maze = new Maze(4);
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            maze.SetWall(edge, false);

            Assert.IsFalse(maze.InteriorWalls().Contains(edge));
            Assert.AreEqual(maze.InteriorEdges().Count() - 1, maze.InteriorWalls().Count());
        }

        [Test]
        public void OpenNeighbors_OnlyReturnsCellsThroughOpenEdges()
        {
            var maze = new Maze(3);
            maze.SetWall(Edge.Between(new Vector2Int(1, 1), Dir.Right), false);

            var neighbors = maze.OpenNeighbors(new Vector2Int(1, 1)).ToList();
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(2, 1) }, neighbors);
        }

        [Test]
        public void OpenNeighbors_EmptyWhenAllWallsPresent()
        {
            var maze = new Maze(3);
            Assert.IsEmpty(maze.OpenNeighbors(new Vector2Int(1, 1)));
        }

        [Test]
        public void Clone_IsIndependentCopy()
        {
            var maze = new Maze(3);
            var edge = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            var clone = maze.Clone();

            clone.SetWall(edge, false);

            Assert.IsTrue(maze.HasWall(edge));
            Assert.IsFalse(clone.HasWall(edge));
            Assert.AreEqual(maze.N, clone.N);
        }
    }
}
