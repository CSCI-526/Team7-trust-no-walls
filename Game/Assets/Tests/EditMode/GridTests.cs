using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class GridTests
    {
        [Test]
        public void Delta_ReturnsCorrectVectorForEachDirection()
        {
            Assert.AreEqual(new Vector2Int(0, 1), DirUtil.Delta(Dir.Up));
            Assert.AreEqual(new Vector2Int(1, 0), DirUtil.Delta(Dir.Right));
            Assert.AreEqual(new Vector2Int(0, -1), DirUtil.Delta(Dir.Down));
            Assert.AreEqual(new Vector2Int(-1, 0), DirUtil.Delta(Dir.Left));
        }

        [Test]
        public void Opposite_ReturnsOppositeDirection()
        {
            Assert.AreEqual(Dir.Down, DirUtil.Opposite(Dir.Up));
            Assert.AreEqual(Dir.Up, DirUtil.Opposite(Dir.Down));
            Assert.AreEqual(Dir.Left, DirUtil.Opposite(Dir.Right));
            Assert.AreEqual(Dir.Right, DirUtil.Opposite(Dir.Left));
        }

        [Test]
        public void All_ContainsAllFourDirectionsExactlyOnce()
        {
            var set = new HashSet<Dir>(DirUtil.All);
            Assert.AreEqual(4, set.Count);
            Assert.AreEqual(4, DirUtil.All.Count);
        }

        [Test]
        public void Between_SameEdgeFromEitherSide_IsEqual()
        {
            var a = new Vector2Int(2, 2);
            var b = new Vector2Int(3, 2);
            var edgeFromA = Edge.Between(a, Dir.Right);
            var edgeFromB = Edge.Between(b, Dir.Left);

            Assert.AreEqual(edgeFromA, edgeFromB);
            Assert.IsTrue(edgeFromA.Equals(edgeFromB));
            Assert.IsTrue(edgeFromA == edgeFromB);
            Assert.AreEqual(edgeFromA.GetHashCode(), edgeFromB.GetHashCode());
        }

        [Test]
        public void Between_HorizontalNeighbors_IsVertical()
        {
            var edge = Edge.Between(new Vector2Int(1, 1), Dir.Right);
            Assert.IsTrue(edge.IsVertical);
        }

        [Test]
        public void Between_VerticalNeighbors_IsHorizontal()
        {
            var edge = Edge.Between(new Vector2Int(1, 1), Dir.Up);
            Assert.IsFalse(edge.IsVertical);
        }

        [Test]
        public void Between_UpThenDown_ProducesSameCanonicalEdge()
        {
            var a = new Vector2Int(4, 4);
            var b = new Vector2Int(4, 5);
            var edgeFromA = Edge.Between(a, Dir.Up);
            var edgeFromB = Edge.Between(b, Dir.Down);
            Assert.AreEqual(edgeFromA, edgeFromB);
        }

        [Test]
        public void Edge_EndpointsAreTheTwoAdjacentCells()
        {
            var a = new Vector2Int(2, 3);
            var edge = Edge.Between(a, Dir.Right);
            var endpoints = new HashSet<Vector2Int> { edge.A, edge.B };
            Assert.IsTrue(endpoints.Contains(a));
            Assert.IsTrue(endpoints.Contains(new Vector2Int(3, 3)));
        }

        [Test]
        public void Edge_DifferentEdges_AreNotEqual()
        {
            var e1 = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            var e2 = Edge.Between(new Vector2Int(0, 0), Dir.Up);
            var e3 = Edge.Between(new Vector2Int(1, 0), Dir.Right);
            Assert.AreNotEqual(e1, e2);
            Assert.AreNotEqual(e1, e3);
        }

        [Test]
        public void Edge_UsableAsDictionaryKey()
        {
            var dict = new Dictionary<Edge, int>();
            var e1 = Edge.Between(new Vector2Int(0, 0), Dir.Right);
            var e2 = Edge.Between(new Vector2Int(1, 0), Dir.Left);
            dict[e1] = 42;
            Assert.AreEqual(42, dict[e2]);
        }

        [Test]
        public void CellCenter_CentersMazeAtWorldOrigin()
        {
            int n = 5;
            var center = Edge.CellCenter(new Vector2Int(2, 2), n);
            Assert.AreEqual(Vector2.zero, center);
        }

        [Test]
        public void CellCenter_CornerCellsAreSymmetricAboutOrigin()
        {
            int n = 4;
            var c00 = Edge.CellCenter(new Vector2Int(0, 0), n);
            var c33 = Edge.CellCenter(new Vector2Int(3, 3), n);
            Assert.AreEqual(-c00.x, c33.x, 0.0001f);
            Assert.AreEqual(-c00.y, c33.y, 0.0001f);
        }

        [Test]
        public void WorldCenter_IsMidpointOfTheTwoCellCenters()
        {
            int n = 5;
            var a = new Vector2Int(1, 1);
            var edge = Edge.Between(a, Dir.Right);
            var expected = (Edge.CellCenter(a, n) + Edge.CellCenter(new Vector2Int(2, 1), n)) / 2f;
            Assert.AreEqual(expected, edge.WorldCenter(n));
        }
    }
}
