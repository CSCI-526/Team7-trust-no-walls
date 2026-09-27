using System.Collections.Generic;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Reads WASD and the arrow keys (Legacy Input Manager) and reports whichever direction was
    /// most recently pressed and is still held, per the spec's movement rule. Releasing that key
    /// falls back to whichever other held key was pressed next-most-recently, if any.
    /// </summary>
    public sealed class KeyboardInput : MonoBehaviour, IDirectionSource
    {
        // Most-recently-pressed at the end of the list.
        private readonly List<Dir> _order = new List<Dir>(4);

        private void Update()
        {
            Track(Dir.Up, KeyCode.W, KeyCode.UpArrow);
            Track(Dir.Down, KeyCode.S, KeyCode.DownArrow);
            Track(Dir.Left, KeyCode.A, KeyCode.LeftArrow);
            Track(Dir.Right, KeyCode.D, KeyCode.RightArrow);
        }

        private void Track(Dir dir, KeyCode primary, KeyCode alt)
        {
            bool pressedThisFrame = Input.GetKeyDown(primary) || Input.GetKeyDown(alt);
            bool held = Input.GetKey(primary) || Input.GetKey(alt);

            if (pressedThisFrame && !_order.Contains(dir))
            {
                _order.Add(dir);
            }
            else if (!held)
            {
                _order.Remove(dir);
            }
        }

        public Dir? GetHeldDirection()
        {
            return _order.Count > 0 ? _order[_order.Count - 1] : (Dir?)null;
        }
    }
}
