using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using TrustNoWall.Core;

namespace TrustNoWall.Tests
{
    /// <summary>
    /// Task 6: every generated level must be completable. The autopilot plays each (level, seed)
    /// at 60 fps, restarting via Reset() on death, and must finish within
    /// <see cref="MaxSimSeconds"/> simulated seconds with at most <see cref="MaxDeaths"/> deaths.
    /// </summary>
    public class EndToEndTests
    {
        private const float Dt = 1f / 60f;
        private const float MaxSimSeconds = 240f;
        private const int MaxDeaths = 5;

        private static IEnumerable<TestCaseData> Cases()
        {
            for (int level = 1; level <= 12; level++)
            {
                for (int seed = 1; seed <= 5; seed++)
                {
                    yield return new TestCaseData(level, seed).SetName($"Level{level:D2}_Seed{seed}");
                }
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void AutopilotCompletesLevel(int level, int seed)
        {
            var result = Run(level, seed);
            UnityEngine.Debug.Log(
                $"E2E|{level}|{seed}|{(result.Completed ? "yes" : "no")}|{result.Deaths}|{result.SimSeconds:F1}|" +
                $"{result.WallMs}|{result.Plans}|{string.Join(";", result.Causes)}");

            Assert.IsTrue(result.Completed,
                $"level {level} seed {seed} not completed: deaths {result.Deaths}, sim {result.SimSeconds:F1} s, causes: {string.Join("; ", result.Causes)}");
            Assert.LessOrEqual(result.Deaths, MaxDeaths,
                $"level {level} seed {seed}: too many deaths ({string.Join("; ", result.Causes)})");
        }

        /// <summary>
        /// Regression: this seed (a real run seed from the demo's formula) used to place two
        /// 2-cell patrols side by side on the only corridor to the Destination with phases whose
        /// free windows never lined up, so the level was impossible. The generator now keeps
        /// patrols apart; the autopilot must finish it.
        /// </summary>
        [Test]
        public void AdjacentPatrolRegression_IsCompletable()
        {
            var layout = LevelGenerator.Generate(12, -2050743988);
            for (int i = 0; i < layout.Patrols.Count; i++)
            {
                for (int j = i + 1; j < layout.Patrols.Count; j++)
                {
                    foreach (var a in layout.Patrols[i].Path)
                    {
                        foreach (var b in layout.Patrols[j].Path)
                        {
                            Assert.Greater(UnityEngine.Mathf.Abs(a.x - b.x) + UnityEngine.Mathf.Abs(a.y - b.y), 1);
                        }
                    }
                }
            }

            var result = Run(12, -2050743988);
            Assert.IsTrue(result.Completed, string.Join("; ", result.Causes));
        }

        [Test]
        public void AutopilotIsDeterministic()
        {
            var a = Run(6, 3);
            var b = Run(6, 3);
            Assert.AreEqual(a.SimSeconds, b.SimSeconds);
            Assert.AreEqual(a.Deaths, b.Deaths);
        }

        private struct RunResult
        {
            public bool Completed;
            public int Deaths;
            public float SimSeconds;
            public long WallMs;
            public int Plans;
            public List<string> Causes;
        }

        private static RunResult Run(int level, int seed)
        {
            var watch = Stopwatch.StartNew();
            var layout = LevelGenerator.Generate(level, seed);
            var sim = new LevelSim(layout);
            var pilot = new Autopilot();
            var causes = new List<string>();
            float total = 0f;
            int maxFrames = (int)(MaxSimSeconds / Dt);

            for (int frame = 0; frame < maxFrames; frame++)
            {
                sim.Step(Dt, pilot.Decide(sim));
                total += Dt;

                if (sim.Status == SimStatus.Complete)
                {
                    break;
                }

                if (sim.Status == SimStatus.Dead)
                {
                    causes.Add($"{sim.DeathCause} at {sim.PlayerOccupiedCell} t={sim.Time:F2}");
                    if (causes.Count > MaxDeaths)
                    {
                        break;
                    }

                    sim.Reset();
                }
            }

            return new RunResult
            {
                Completed = sim.Status == SimStatus.Complete,
                Deaths = causes.Count,
                SimSeconds = total,
                WallMs = watch.ElapsedMilliseconds,
                Plans = pilot.PlansMade,
                Causes = causes
            };
        }
    }
}
