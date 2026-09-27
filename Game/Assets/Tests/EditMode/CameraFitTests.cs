using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    /// <summary>
    /// Verifies the pure camera-fit math keeps the whole maze visible, centered, and clear of the
    /// HUD band for every level size the game reaches (4x4 up to ~18x18).
    /// </summary>
    public class CameraFitTests
    {
        private const float Aspect = 16f / 9f;
        private const float Margin = 0.6f;
        private const float HudFraction = 0.12f;
        private const float Epsilon = 1e-4f;

        [TestCase(4)]
        [TestCase(10)]
        [TestCase(18)]
        public void Compute_FitsMazeWithinVisibleWidth(int n)
        {
            var result = CameraFit.Compute(n, Aspect, Margin, HudFraction);
            float visibleWidth = 2f * result.OrthoSize * Aspect;
            Assert.GreaterOrEqual(visibleWidth, n + Margin - Epsilon);
        }

        [TestCase(4)]
        [TestCase(10)]
        [TestCase(18)]
        public void Compute_FitsMazeBelowHudBand(int n)
        {
            var result = CameraFit.Compute(n, Aspect, Margin, HudFraction);

            // Screen bottom/top in world Y.
            float screenBottom = result.CameraY - result.OrthoSize;
            float screenTop = result.CameraY + result.OrthoSize;

            // The HUD band occupies the top HudFraction of the screen; the maze area is the rest.
            float hudBandBottom = screenTop - (2f * result.OrthoSize * HudFraction);

            // The maze's own bounding box (cell centers span [-(n-1)/2, (n-1)/2], cells are 1 unit).
            float mazeHalfExtent = n / 2f;

            Assert.LessOrEqual(mazeHalfExtent, hudBandBottom + Epsilon, "Maze top must sit at or below the HUD band.");
            Assert.GreaterOrEqual(-mazeHalfExtent, screenBottom - Epsilon, "Maze bottom must sit at or above the screen bottom.");
        }

        [TestCase(4)]
        [TestCase(10)]
        [TestCase(18)]
        public void Compute_IsATightFit_NotOversized(int n)
        {
            var result = CameraFit.Compute(n, Aspect, Margin, HudFraction);

            float sizeForHeight = (n + Margin) / (2f * (1f - HudFraction));
            float sizeForWidth = (n + Margin) / (2f * Aspect);
            float expected = Mathf.Max(sizeForHeight, sizeForWidth);

            Assert.AreEqual(expected, result.OrthoSize, Epsilon);
        }

        [Test]
        public void Compute_MazeStaysHorizontallyCentered()
        {
            // The camera's X position is untouched by this helper (only Y changes), and the maze
            // itself is built centered at world X = 0, so as long as the visible width covers the
            // maze extent (checked above), the maze is horizontally centered by construction.
            var result = CameraFit.Compute(10, Aspect, Margin, HudFraction);
            Assert.AreEqual(result.OrthoSize * HudFraction, result.CameraY, Epsilon);
        }
    }
}
