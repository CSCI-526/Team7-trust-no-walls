using System;
using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>
    /// An <see cref="IDirectionSource"/> backed by <see cref="Core.Autopilot"/>: the demo drives the
    /// game through exactly the same input path as the keyboard. Asks the autopilot once per call,
    /// so <see cref="GameFlow"/> calls it once per sim step chunk.
    /// </summary>
    public sealed class AutopilotSource : IDirectionSource
    {
        private readonly Func<LevelSim> _sim;
        private readonly Autopilot _autopilot = new Autopilot();

        public AutopilotSource(Func<LevelSim> sim)
        {
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
        }

        public Dir? GetHeldDirection()
        {
            LevelSim sim = _sim();
            return sim == null ? (Dir?)null : _autopilot.Decide(sim);
        }
    }
}
