using UnityEngine;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Applies <see cref="TrustNoWall.Core.CameraFit"/>'s pure math to a real camera: fits the
    /// whole N x N maze plus margin at the current screen aspect, leaving a HUD band on top.
    /// </summary>
    public static class CameraFit
    {
        public const float Margin = 0.6f;
        public const float HudFraction = 0.12f;

        /// <summary>
        /// Fits <paramref name="camera"/> to an n x n maze. The vertical offset is applied to
        /// <paramref name="rig"/> (the camera's parent), not the camera itself, so a camera shake
        /// applied as a local offset on the camera never fights with this every-frame call.
        /// </summary>
        public static void Apply(Camera camera, Transform rig, int n)
        {
            float aspect = camera.pixelWidth > 0 && camera.pixelHeight > 0
                ? (float)camera.pixelWidth / camera.pixelHeight
                : 16f / 9f;

            TrustNoWall.Core.CameraFit.Result result =
                TrustNoWall.Core.CameraFit.Compute(n, aspect, Margin, HudFraction);

            camera.orthographicSize = result.OrthoSize;
            Vector3 pos = rig.position;
            rig.position = new Vector3(0f, result.CameraY, pos.z);
        }
    }
}
