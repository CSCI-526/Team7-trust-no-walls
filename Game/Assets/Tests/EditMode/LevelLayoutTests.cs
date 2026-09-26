using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class LevelLayoutTests
    {
        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        [Test]
        public void Builder_Defaults()
        {
            var layout = new LevelLayoutBuilder(new Maze(5)).Build();
            Assert.AreEqual(5, layout.N);
            Assert.AreEqual(2, layout.Level);
            Assert.AreEqual(C(0, 0), layout.Start);
            Assert.AreEqual(C(4, 4), layout.Destination);
            Assert.AreEqual(0, layout.Mechanics.Count);
            Assert.AreEqual(0, layout.NewMechanics.Count);
            Assert.IsFalse(layout.HasChaser);
        }

        [Test]
        public void Builder_InfersMechanicsFromElements()
        {
            var maze = new Maze(4);
            var e = Edge.Between(C(1, 1), Dir.Right);
            var layout = new LevelLayoutBuilder(maze)
                .Add(new InvisibleWall(e))
                .Add(new Decoy(C(2, 2)))
                .WithChaser()
                .Build();
            CollectionAssert.AreEqual(new[] { Mechanic.InvisibleWalls, Mechanic.Decoys, Mechanic.Chaser }, layout.Mechanics);
            Assert.IsTrue(layout.HasChaser);
        }

        [Test]
        public void Builder_ExplicitMechanicsWin()
        {
            var layout = new LevelLayoutBuilder(new Maze(4))
                .WithMechanics(new[] { Mechanic.Patrols }, new[] { Mechanic.Patrols })
                .WithLevel(6).WithSeed(1234)
                .Build();
            CollectionAssert.AreEqual(new[] { Mechanic.Patrols }, layout.Mechanics);
            CollectionAssert.AreEqual(new[] { Mechanic.Patrols }, layout.NewMechanics);
            Assert.AreEqual(6, layout.Level);
            Assert.AreEqual(1234, layout.Seed);
        }

        [Test]
        public void Build_ClonesTheMaze()
        {
            var b = new LevelLayoutBuilder(new Maze(4)).Carve(C(0, 0), C(1, 0));
            var layout = b.Build();
            b.Carve(C(1, 0), C(2, 0));
            Assert.IsTrue(layout.Maze.HasWall(Edge.Between(C(1, 0), Dir.Right)));
            Assert.IsFalse(layout.Maze.HasWall(Edge.Between(C(0, 0), Dir.Right)));
        }

        [Test]
        public void Lookups_FindEdgeAndCellElements()
        {
            var inv = Edge.Between(C(0, 1), Dir.Right);
            var oneWay = Edge.Between(C(1, 1), Dir.Right);
            var phase = Edge.Between(C(2, 1), Dir.Up);
            var ma = Edge.Between(C(1, 2), Dir.Left);
            var mb = Edge.Between(C(1, 2), Dir.Right);
            var trig = Edge.Between(C(3, 0), Dir.Up);
            var plate = new TriggerPlate(C(3, 3), new[] { trig }, new Edge[0], 1);
            var tp = new Teleporter(C(2, 3), C(0, 3));
            var patrol = new Patrol(new[] { C(3, 1), C(3, 2) }, 0f);
            var layout = new LevelLayoutBuilder(new Maze(5))
                .Add(new InvisibleWall(inv))
                .Add(new OneWay(oneWay, Dir.Right))
                .Add(new PhaseWall(phase, 0f, true))
                .Add(new MovingWall(ma, mb, 0f))
                .Add(plate)
                .Add(tp)
                .Add(new MemoryTile(C(1, 3)))
                .Add(new CollapseTile(C(2, 2), true))
                .Add(new RotatingGate(C(4, 1), 0f, false))
                .Add(new Decoy(C(4, 3)))
                .Add(patrol)
                .Build();

            Assert.IsTrue(layout.IsInvisible(inv));
            Assert.IsFalse(layout.IsInvisible(oneWay));
            Assert.AreEqual(EdgeElementKind.OneWay, layout.EdgeElementAt(oneWay));
            Assert.AreEqual(Dir.Right, layout.OneWayAt(oneWay).Allowed);
            Assert.AreEqual(EdgeElementKind.PhaseWall, layout.EdgeElementAt(phase));
            Assert.IsNotNull(layout.PhaseWallAt(phase));
            Assert.AreEqual(EdgeElementKind.MovingWall, layout.EdgeElementAt(mb));
            Assert.AreSame(layout.MovingWallAt(ma), layout.MovingWallAt(mb));
            Assert.AreEqual(EdgeElementKind.TriggerWall, layout.EdgeElementAt(trig));
            Assert.AreSame(plate, layout.TriggerFor(trig));
            Assert.IsTrue(plate.StartsClosed(trig));
            Assert.AreEqual(EdgeElementKind.None, layout.EdgeElementAt(Edge.Between(C(0, 0), Dir.Up)));

            Assert.AreEqual(CellElementKind.TriggerPlate, layout.CellElementAt(C(3, 3)));
            Assert.AreEqual(CellElementKind.TeleporterPad, layout.CellElementAt(C(2, 3)));
            Assert.AreEqual(CellElementKind.TeleporterTarget, layout.CellElementAt(C(0, 3)));
            Assert.AreSame(tp, layout.TeleporterAt(C(2, 3)));
            Assert.AreEqual(CellElementKind.MemoryTile, layout.CellElementAt(C(1, 3)));
            Assert.AreEqual(CellElementKind.CollapseTile, layout.CellElementAt(C(2, 2)));
            Assert.IsTrue(layout.CollapseTileAt(C(2, 2)).Permanent);
            Assert.AreEqual(CellElementKind.RotatingGate, layout.CellElementAt(C(4, 1)));
            Assert.IsNotNull(layout.GateAt(C(4, 1)));
            Assert.AreEqual(CellElementKind.Decoy, layout.CellElementAt(C(4, 3)));
            Assert.IsNotNull(layout.DecoyAt(C(4, 3)));
            Assert.AreEqual(CellElementKind.PatrolPath, layout.CellElementAt(C(3, 2)));
            Assert.AreEqual(CellElementKind.None, layout.CellElementAt(C(0, 0)));
            Assert.IsNull(layout.MemoryTileAt(C(0, 0)));
        }

        [Test]
        public void VisibleWall_ExcludesInvisible()
        {
            var inv = Edge.Between(C(0, 1), Dir.Right);
            var layout = new LevelLayoutBuilder(new Maze(3)).Add(new InvisibleWall(inv)).Build();
            Assert.IsFalse(layout.IsVisibleWall(inv));
            Assert.IsTrue(layout.Maze.HasWall(inv));
            Assert.IsTrue(layout.IsVisibleWall(Edge.Between(C(1, 1), Dir.Right)));
        }
    }
}
