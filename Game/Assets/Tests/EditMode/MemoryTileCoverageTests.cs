using System.Linq;
using NUnit.Framework;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    /// <summary>
    /// Every invisible wall must be reachable by a memory tile: if it can never be revealed, a
    /// hidden wall can kill a player with no way to see it coming (see ElementPlacer.PlaceMemoryTiles).
    /// </summary>
    public class MemoryTileCoverageTests
    {
        private const int MinLevel = 2;
        private const int MaxLevel = 15;
        private const int MaxSeed = 40;

        [Test]
        public void EveryInvisibleWall_IsWithinRevealRangeOfAMemoryTile()
        {
            int checkedWalls = 0;
            for (int level = MinLevel; level <= MaxLevel; level++)
            {
                for (int seed = 1; seed <= MaxSeed; seed++)
                {
                    var l = LevelGenerator.Generate(level, seed);
                    string id = $"level {level} seed {seed}";
                    foreach (var iw in l.InvisibleWalls)
                    {
                        checkedWalls++;
                        bool covered = l.MemoryTiles.Any(
                            t => iw.Edge.ChebyshevDistanceTo(t.Cell) <= MemoryTile.RevealRadius);
                        Assert.IsTrue(covered, $"{id}: invisible wall at {iw.Edge.A}/{iw.Edge.B} is not " +
                            "within reveal range of any memory tile");
                    }
                }
            }

            // Sanity: the sweep actually exercised invisible walls (otherwise the assertion above
            // would pass vacuously and hide a regression that stops placing them at all).
            Assert.Greater(checkedWalls, 0);
            UnityEngine.Debug.Log($"[MemoryTileCoverageTests] checked {checkedWalls} invisible walls across "
                + $"levels {MinLevel}..{MaxLevel} x seeds 1..{MaxSeed}, all covered");
        }
    }
}
