using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    public class LevelPlanTests
    {
        private static readonly Dictionary<int, Mechanic[]> Intros = new Dictionary<int, Mechanic[]>
        {
            { 2, new[] { Mechanic.InvisibleWalls, Mechanic.MemoryTiles } },
            { 3, new[] { Mechanic.MovingWalls, Mechanic.DisappearingWalls } },
            { 4, new[] { Mechanic.CollapsingTiles, Mechanic.TriggerWalls } },
            { 5, new[] { Mechanic.Teleporters, Mechanic.RotatingBarriers } },
            { 6, new[] { Mechanic.Patrols, Mechanic.OneWayPaths } },
            { 7, new[] { Mechanic.Decoys } },
            { 8, new[] { Mechanic.Chaser } },
        };

        [Test]
        public void MechanicInfo_IntroLevelsMatchSpec()
        {
            foreach (var kv in Intros)
            {
                foreach (var m in kv.Value)
                {
                    Assert.AreEqual(kv.Key, MechanicInfo.Get(m).IntroLevel, m.ToString());
                }
            }

            Assert.AreEqual(12, MechanicInfo.All.Count);
        }

        [Test]
        public void MechanicInfo_ExplanationsAndColors()
        {
            Assert.AreEqual("Some walls are invisible. Touching one is fatal.", MechanicInfo.Get(Mechanic.InvisibleWalls).Explanation);
            Assert.AreEqual("A shadow follows your trail. Keep moving.", MechanicInfo.Get(Mechanic.Chaser).Explanation);
            var c = MechanicInfo.Get(Mechanic.Teleporters).Color;
            Assert.AreEqual(0xC0 / 255f, c.r, 1e-4f);
            Assert.AreEqual(0x4D / 255f, c.g, 1e-4f);
            Assert.AreEqual(0xFF / 255f, c.b, 1e-4f);
            foreach (var info in MechanicInfo.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(info.DisplayName));
                Assert.IsFalse(string.IsNullOrEmpty(info.Explanation));
            }
        }

        [Test]
        public void Level1_HasNoMechanics()
        {
            Assert.AreEqual(0, LevelPlan.MechanicsFor(1, new System.Random(1)).Count);
        }

        [Test]
        public void IntroLevels_ContainTheirMechanicsPlusOneEarlierAtHalf()
        {
            for (int level = 2; level <= 8; level++)
            {
                for (int seed = 1; seed <= 30; seed++)
                {
                    var list = LevelPlan.MechanicsFor(level, new System.Random(seed));
                    foreach (var m in Intros[level])
                    {
                        CollectionAssert.Contains(list, m);
                        Assert.IsFalse(LevelPlan.IsHalf(m, level));
                    }

                    var extras = list.Where(m => !Intros[level].Contains(m)).ToList();
                    if (level == 2)
                    {
                        Assert.AreEqual(0, extras.Count);
                        continue;
                    }

                    Assert.That(extras.Count, Is.InRange(1, 2), $"level {level} seed {seed}");
                    foreach (var m in extras)
                    {
                        Assert.Less(MechanicInfo.Get(m).IntroLevel, level);
                        Assert.IsTrue(LevelPlan.IsHalf(m, level));
                    }

                    if (extras.Count == 2)
                    {
                        CollectionAssert.AreEquivalent(new[] { Mechanic.InvisibleWalls, Mechanic.MemoryTiles }, extras);
                    }
                }
            }
        }

        [Test]
        public void LateLevels_HaveFourThenFiveDistinctMechanics()
        {
            for (int level = 9; level <= 20; level++)
            {
                for (int seed = 1; seed <= 30; seed++)
                {
                    var list = LevelPlan.MechanicsFor(level, new System.Random(seed));
                    Assert.AreEqual(level >= 12 ? 5 : 4, list.Count, $"level {level}");
                    Assert.AreEqual(list.Count, list.Distinct().Count());
                    foreach (var m in list)
                    {
                        Assert.IsFalse(LevelPlan.IsHalf(m, level));
                    }
                }
            }
        }

        [Test]
        public void InvisibleWallsAndMemoryTiles_AlwaysTogether()
        {
            for (int level = 1; level <= 20; level++)
            {
                for (int seed = 1; seed <= 40; seed++)
                {
                    var list = LevelPlan.MechanicsFor(level, new System.Random(seed));
                    Assert.AreEqual(list.Contains(Mechanic.InvisibleWalls), list.Contains(Mechanic.MemoryTiles));
                }
            }
        }

        [Test]
        public void MechanicsFor_IsDeterministic()
        {
            for (int level = 1; level <= 15; level++)
            {
                var a = LevelPlan.MechanicsFor(level, new System.Random(99));
                var b = LevelPlan.MechanicsFor(level, new System.Random(99));
                CollectionAssert.AreEqual(a, b);
            }
        }

        [Test]
        public void CountFor_FullCountsMatchSpec()
        {
            // N = 10 (level 7), A = 100, W = 90
            Assert.AreEqual(23, LevelPlan.CountFor(Mechanic.InvisibleWalls, 10, 90, false)); // 22.5 -> 23
            Assert.AreEqual(3, LevelPlan.CountFor(Mechanic.MemoryTiles, 10, 90, false));
            Assert.AreEqual(5, LevelPlan.CountFor(Mechanic.MovingWalls, 10, 90, false));
            Assert.AreEqual(5, LevelPlan.CountFor(Mechanic.DisappearingWalls, 10, 90, false));
            Assert.AreEqual(10, LevelPlan.CountFor(Mechanic.CollapsingTiles, 10, 90, false));
            Assert.AreEqual(2, LevelPlan.CountFor(Mechanic.TriggerWalls, 10, 90, false));
            Assert.AreEqual(2, LevelPlan.CountFor(Mechanic.Teleporters, 10, 90, false));
            Assert.AreEqual(3, LevelPlan.CountFor(Mechanic.RotatingBarriers, 10, 90, false));
            Assert.AreEqual(2, LevelPlan.CountFor(Mechanic.Patrols, 10, 90, false));
            Assert.AreEqual(4, LevelPlan.CountFor(Mechanic.OneWayPaths, 10, 90, false));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.Decoys, 10, 90, false));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.Chaser, 10, 90, false));
        }

        [Test]
        public void CountFor_MinimumsAtSmallSizes()
        {
            // N = 4, A = 16
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.MemoryTiles, 4, 9, false));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.MovingWalls, 4, 9, false));
            Assert.AreEqual(2, LevelPlan.CountFor(Mechanic.DisappearingWalls, 4, 9, false));
            Assert.AreEqual(2, LevelPlan.CountFor(Mechanic.CollapsingTiles, 4, 9, false));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.TriggerWalls, 4, 9, false));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.RotatingBarriers, 4, 9, false));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.OneWayPaths, 4, 9, false));
        }

        [Test]
        public void CountFor_DecoysDoubleFromLevel11()
        {
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.Decoys, 13, 150, false)); // level 10
            Assert.AreEqual(2, LevelPlan.CountFor(Mechanic.Decoys, 14, 150, false)); // level 11
        }

        [Test]
        public void CountFor_HalfIsRoundedUpWithMinimumOne()
        {
            Assert.AreEqual(12, LevelPlan.CountFor(Mechanic.InvisibleWalls, 10, 90, true)); // 23 -> 12
            Assert.AreEqual(5, LevelPlan.CountFor(Mechanic.CollapsingTiles, 10, 90, true));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.MovingWalls, 4, 9, true));
            Assert.AreEqual(1, LevelPlan.CountFor(Mechanic.Chaser, 11, 90, true));
        }
    }
}
