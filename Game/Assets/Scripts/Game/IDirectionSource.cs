using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Something that can tell <see cref="GameFlow"/> which grid direction is currently held, so
    /// the flow doesn't care whether it came from a keyboard, an autopilot, or anything else.
    /// </summary>
    public interface IDirectionSource
    {
        /// <summary>The currently-held movement direction, or null if none.</summary>
        Dir? GetHeldDirection();
    }
}
