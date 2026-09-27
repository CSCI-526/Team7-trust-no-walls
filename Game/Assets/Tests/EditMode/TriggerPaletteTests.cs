using System.Linq;
using NUnit.Framework;
using TrustNoWall.Game;

namespace TrustNoWall.Tests
{
    public class TriggerPaletteTests
    {
        private const int Purple = 0;
        private const int Green = 1;
        private const int Orange = 2;

        [Test]
        public void OnePlate_WithMovingWallsAndTeleporters_UsesNonClashingGreen()
        {
            int[] indices = Palette.TriggerPaletteIndices(1, true, true);
            Assert.AreEqual(Green, indices[0]);
        }

        [Test]
        public void OnePlate_WithMovingWalls_AvoidsOrange()
        {
            int[] indices = Palette.TriggerPaletteIndices(1, true, false);
            Assert.AreNotEqual(Orange, indices[0]);
        }

        [Test]
        public void OnePlate_WithTeleporters_AvoidsPurple()
        {
            int[] indices = Palette.TriggerPaletteIndices(1, false, true);
            Assert.AreNotEqual(Purple, indices[0]);
        }

        [Test]
        public void TwoPlates_WithMovingWalls_AreDistinctAndAvoidOrange()
        {
            int[] indices = Palette.TriggerPaletteIndices(2, true, false);
            Assert.AreEqual(2, indices.Length);
            Assert.AreNotEqual(indices[0], indices[1]);
            CollectionAssert.DoesNotContain(indices, Orange);
        }

        [TestCase(2, true, true)]
        [TestCase(3, true, true)]
        [TestCase(3, true, false)]
        [TestCase(3, false, true)]
        [TestCase(3, false, false)]
        public void PlatesBeyondTheSubset_StillGetDistinctColors(int plates, bool moving, bool teleport)
        {
            int[] indices = Palette.TriggerPaletteIndices(plates, moving, teleport);
            // Plates use ColorIndex 0..plates-1 (ElementPlacer assigns count % 3).
            var used = Enumerable.Range(0, plates).Select(i => indices[i % indices.Length]).ToArray();
            Assert.AreEqual(plates, used.Distinct().Count());
        }
    }
}
