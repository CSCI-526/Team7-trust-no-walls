using UnityEngine;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Bootstraps the whole game at runtime: everything is created from code, with no prefabs or
    /// scene wiring. Builds the camera, the maze/player views, effects, audio, input and the
    /// GameFlow state machine that ties them all together, plus the IMGUI HUD that reads it.
    /// </summary>
    public static class GameBootstrap
    {
        private static bool _booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Boot()
        {
            if (_booted)
            {
                return;
            }

            _booted = true;

            GameObject root = new GameObject("TrustNoWall");

            // The rig is what CameraFit repositions each level; the camera sits at a fixed local
            // offset under it, so CameraShake can perturb the camera's own local position without
            // ever fighting CameraFit's every-frame call.
            GameObject rigObject = new GameObject("CameraRig");
            rigObject.transform.SetParent(root.transform);

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(rigObject.transform);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            GameObject effectsObject = new GameObject("Effects");
            effectsObject.transform.SetParent(root.transform);
            Effects effects = effectsObject.AddComponent<Effects>();

            GameObject mazeObject = new GameObject("MazeView");
            mazeObject.transform.SetParent(root.transform);
            MazeView mazeView = mazeObject.AddComponent<MazeView>();

            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(root.transform);
            PlayerView playerView = playerObject.AddComponent<PlayerView>();

            GameObject flowObject = new GameObject("Flow");
            flowObject.transform.SetParent(root.transform);
            KeyboardInput keyboard = flowObject.AddComponent<KeyboardInput>();
            Sfx sfx = flowObject.AddComponent<Sfx>();
            Hud hud = flowObject.AddComponent<Hud>();
            GameFlow flow = flowObject.AddComponent<GameFlow>();

            flow.Configure(camera, rigObject.transform, mazeView, playerView, effects, sfx, keyboard);
            hud.Flow = flow;

            DemoMode demo = flowObject.AddComponent<DemoMode>();
            demo.Configure(flow, keyboard, new AutopilotSource(() => flow.Sim));

            Debug.Log("Trust No Wall booted");
        }
    }
}
