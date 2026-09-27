using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// Pure math for fitting an orthographic camera to an N x N maze (cell size 1, centered at
    /// the origin) plus a margin, at a given screen aspect ratio, leaving a HUD band across the
    /// top of the screen. Has no dependency on UnityEngine.Camera so it can be unit tested
    /// directly for a range of maze sizes.
    /// </summary>
    public static class CameraFit
    {
        /// <summary>The result of a fit: the camera's half-height and its world-space Y position.</summary>
        public readonly struct Result
        {
            /// <summary>The orthographic camera's half-height (orthographicSize), in world units.</summary>
            public float OrthoSize { get; }

            /// <summary>
            /// The camera's world-space Y position. The maze itself always stays centered at
            /// world (0, 0); moving the camera up by this much pushes the maze down within the
            /// screen, clearing room for the HUD band above it.
            /// </summary>
            public float CameraY { get; }

            public Result(float orthoSize, float cameraY)
            {
                OrthoSize = orthoSize;
                CameraY = cameraY;
            }
        }

        /// <summary>
        /// Computes the orthographic size and vertical camera offset that fit an n x n maze plus
        /// <paramref name="margin"/> world units of breathing room, both horizontally and in the
        /// vertical space left after reserving the top <paramref name="hudFraction"/> of the
        /// screen for the HUD band.
        /// </summary>
        public static Result Compute(int n, float aspect, float margin, float hudFraction)
        {
            float mazeExtent = n + margin;

            // The visible height below the HUD band is 2 * orthoSize * (1 - hudFraction); the
            // visible width is 2 * orthoSize * aspect. Solve for the orthoSize needed for each to
            // fit the maze, then take the larger (binding) one.
            float sizeForHeight = mazeExtent / (2f * (1f - hudFraction));
            float sizeForWidth = mazeExtent / (2f * aspect);
            float orthoSize = Mathf.Max(sizeForHeight, sizeForWidth);

            // With the maze centered at world y = 0, centering it within the strip below the HUD
            // band (world y in [cameraY - orthoSize, cameraY + orthoSize * (1 - 2 * hudFraction)])
            // requires cameraY = orthoSize * hudFraction.
            float cameraY = orthoSize * hudFraction;

            return new Result(orthoSize, cameraY);
        }
    }
}
