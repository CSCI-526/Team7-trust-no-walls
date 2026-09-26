using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class SolvabilityValidatorTests
    {
        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        private static Edge E(Vector2Int a, Vector2Int b) => LevelLayoutBuilder.EdgeBetweenCells(a, b);

        // 3x3 tree. Route: (0,0) (1,0) (2,0) (2,1) (2,2)=D.
        // Side branch off (1,0): (1,1) (0,1) (0,2) (1,2), a dead end.
        private static LevelLayoutBuilder TreeBuilder()
        {
            return new LevelLayoutBuilder(new Maze(3))
                .Carve(C(0, 0), C(1, 0), C(2, 0), C(2, 1), C(2, 2))
                .Carve(C(1, 0), C(1, 1), C(0, 1), C(0, 2), C(1, 2))
                .WithStart(C(0, 0))
                .WithDestination(C(2, 2));
        }

        // 3x3 with a sealed pocket {(0,2),(1,2)}. Route: (0,0) (1,0) (2,0) (2,1) (2,2)=D,
        // branch (1,0) (1,1) (0,1).
        private static LevelLayoutBuilder PocketBuilder()
        {
            return new LevelLayoutBuilder(new Maze(3))
                .Carve(C(0, 0), C(1, 0), C(2, 0), C(2, 1), C(2, 2))
                .Carve(C(1, 0), C(1, 1), C(0, 1))
                .Carve(C(0, 2), C(1, 2))
                .WithStart(C(0, 0))
                .WithDestination(C(2, 2));
        }

        [Test]
        public void PlainTree_IsSolvable()
        {
            Assert.IsTrue(SolvabilityValidator.IsSolvable(TreeBuilder().Build()));
            Assert.IsTrue(SolvabilityValidator.IsSolvable(PocketBuilder().Build()), "unreachable pocket is not a trap");
        }

        [Test]
        public void UnreachableDestination_IsRejected()
        {
            var b = new LevelLayoutBuilder(new Maze(3)).Carve(C(0, 0), C(1, 0), C(2, 0));
            Assert.IsFalse(SolvabilityValidator.IsSolvable(b.Build()));
        }

        [Test]
        public void OneWayIntoDeadEnd_IsRejected()
        {
            var trap = TreeBuilder().Add(new OneWay(E(C(1, 0), C(1, 1)), Dir.Up)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(trap));
        }

        [Test]
        public void OneWayOutOfDeadEnd_OrTowardDestination_IsAccepted()
        {
            Assert.IsTrue(SolvabilityValidator.IsSolvable(TreeBuilder().Add(new OneWay(E(C(1, 0), C(1, 1)), Dir.Down)).Build()));
            Assert.IsTrue(SolvabilityValidator.IsSolvable(TreeBuilder().Add(new OneWay(E(C(2, 0), C(2, 1)), Dir.Up)).Build()));
            Assert.IsFalse(SolvabilityValidator.IsSolvable(TreeBuilder().Add(new OneWay(E(C(2, 0), C(2, 1)), Dir.Down)).Build()));
        }

        [Test]
        public void TeleporterIntoSealedPocket_IsRejected()
        {
            var trap = PocketBuilder().Add(new Teleporter(C(0, 1), C(0, 2))).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(trap));
        }

        [Test]
        public void TeleporterToConnectedCell_IsAccepted()
        {
            Assert.IsTrue(SolvabilityValidator.IsSolvable(PocketBuilder().Add(new Teleporter(C(0, 1), C(2, 1))).Build()));
        }

        [Test]
        public void TeleporterPadBlockingTheOnlyRoute_IsRejected()
        {
            // Pad on the route sends the player back behind itself; the Destination is unreachable.
            var trap = TreeBuilder().Add(new Teleporter(C(2, 1), C(0, 2))).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(trap));
        }

        [Test]
        public void PermanentCollapseOnBridge_IsRejected()
        {
            var trap = TreeBuilder().Add(new CollapseTile(C(2, 1), true)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(trap));
        }

        [Test]
        public void TemporaryCollapseOnBridge_IsAccepted()
        {
            Assert.IsTrue(SolvabilityValidator.IsSolvable(TreeBuilder().Add(new CollapseTile(C(2, 1), false)).Build()));
        }

        [Test]
        public void PermanentCollapseSealingAPocketBehindIt_IsRejected()
        {
            // The player can walk over the intact tile (0,2) into (1,2); once it collapses they are sealed in.
            var trap = TreeBuilder().Add(new CollapseTile(C(0, 2), true)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(trap));
        }

        [Test]
        public void PermanentCollapse_IsAcceptedOnlyWhenNothingIsSealedBehindIt()
        {
            Assert.IsTrue(SolvabilityValidator.IsSolvable(TreeBuilder().Add(new CollapseTile(C(1, 2), true)).Build()));

            // Loop (1,0) (1,1) (2,1) (2,0): a permanent tile on (1,1) leaves every other cell connected.
            var loop = TreeBuilder().Carve(C(1, 1), C(2, 1)).Add(new CollapseTile(C(1, 1), true)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(loop), "(0,1) branch is behind (1,1)");
            var loopTip = TreeBuilder().Carve(C(1, 1), C(2, 1)).Carve(C(0, 1), C(0, 0))
                .Add(new CollapseTile(C(1, 1), true)).Build();
            Assert.IsTrue(SolvabilityValidator.IsSolvable(loopTip));
        }

        [Test]
        public void TriggerEdgeOnBridge_IsRejectedInBothDirections()
        {
            var closes = TreeBuilder()
                .Add(new TriggerPlate(C(0, 1), new Edge[0], new[] { E(C(2, 0), C(2, 1)) }, 0)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(closes));
            var opens = TreeBuilder()
                .Add(new TriggerPlate(C(0, 1), new[] { E(C(2, 0), C(2, 1)) }, new Edge[0], 0)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(opens));
        }

        [Test]
        public void TriggerEdgeIntoDeadEnd_IsRejected()
        {
            // (1,2) is reachable only through the linked edge, which might close behind the player.
            var trap = TreeBuilder()
                .Add(new TriggerPlate(C(0, 1), new Edge[0], new[] { E(C(0, 2), C(1, 2)) }, 0)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(trap));
        }

        [Test]
        public void TriggerOpensIntoSealedPocket_IsRejected()
        {
            // The pocket {(0,2),(1,2)} is only reachable through an "opens" edge: checked as reachable, then a trap.
            var trap = PocketBuilder().Carve(C(0, 1), C(0, 2))
                .Add(new TriggerPlate(C(1, 1), new[] { E(C(0, 1), C(0, 2)) }, new Edge[0], 0)).Build();
            Assert.IsFalse(SolvabilityValidator.IsSolvable(trap));
        }

        [Test]
        public void TriggerEdgeOnALoop_IsAccepted()
        {
            var closes = TreeBuilder().Carve(C(1, 1), C(2, 1))
                .Add(new TriggerPlate(C(0, 2), new Edge[0], new[] { E(C(1, 1), C(2, 1)) }, 0)).Build();
            Assert.IsTrue(SolvabilityValidator.IsSolvable(closes));
            var opens = TreeBuilder().Carve(C(1, 1), C(2, 1))
                .Add(new TriggerPlate(C(0, 2), new[] { E(C(1, 1), C(2, 1)) }, new Edge[0], 0)).Build();
            Assert.IsTrue(SolvabilityValidator.IsSolvable(opens));
        }

        [Test]
        public void PeriodicBlockersOnBridge_AreAccepted()
        {
            var bridge = E(C(2, 0), C(2, 1));
            var b = TreeBuilder()
                .Add(new PhaseWall(bridge, 0f, false))
                .Add(new MovingWall(E(C(1, 0), C(2, 0)), E(C(0, 0), C(1, 0)), 0f))
                .Add(new RotatingGate(C(2, 1), 0f, true))
                .Add(new Patrol(new[] { C(1, 0), C(1, 1) }, 0f))
                .Add(new CollapseTile(C(2, 0), false));
            Assert.IsTrue(SolvabilityValidator.IsSolvable(b.Build()));
        }

        [Test]
        public void Decoy_SendsToStart_IsAccepted()
        {
            Assert.IsTrue(SolvabilityValidator.IsSolvable(TreeBuilder().Add(new Decoy(C(1, 2))).Build()));
        }

        [Test]
        public void InvisibleWallsBlockLikeWalls()
        {
            // An invisible wall is a maze wall; the maze stays solvable if it is not on the route.
            var b = TreeBuilder().Add(new InvisibleWall(E(C(1, 1), C(2, 1))));
            Assert.IsTrue(SolvabilityValidator.IsSolvable(b.Build()));
        }
    }
}
