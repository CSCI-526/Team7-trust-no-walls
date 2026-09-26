using UnityEngine;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Bootstraps the game at runtime, creating the root camera and ensuring
    /// the scene is properly initialized.
    /// </summary>
    public static class GameBootstrap
    {
        private static bool _booted = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Boot()
        {
            if (_booted)
            {
                return;
            }

            _booted = true;

            // Create root TrustNoWall GameObject
            GameObject root = new GameObject("TrustNoWall");

            // Create camera as child of root
            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.055f, 0.063f, 0.088f, 1f); // #0E1016
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Debug.Log("Trust No Wall booted");
        }
    }
}
