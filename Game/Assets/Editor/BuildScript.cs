using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrustNoWall.EditorTools
{
    /// <summary>
    /// Editor-only build automation, invoked from tools/build-webgl.sh via
    /// -executeMethod. Must not be included in player builds.
    /// </summary>
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                string sceneDir = Path.GetDirectoryName(ScenePath);
                if (!string.IsNullOrEmpty(sceneDir) && !Directory.Exists(sceneDir))
                {
                    Directory.CreateDirectory(sceneDir);
                }

                UnityEngine.SceneManagement.Scene scene =
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }

        public static void BuildWebGL()
        {
            EnsureScene();

            PlayerSettings.productName = "Trust No Wall";
            PlayerSettings.companyName = "TrustNoWall Team";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            string gameDir = Directory.GetParent(Application.dataPath).FullName;
            string repoRoot = Directory.GetParent(gameDir).FullName;
            string outputPath = Path.Combine(repoRoot, "docs");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            stopwatch.Stop();

            File.WriteAllText(Path.Combine(outputPath, ".nojekyll"), string.Empty);

            UnityEngine.Debug.Log(string.Format(
                "WebGL build finished: result={0}, size={1} bytes, duration={2}",
                report.summary.result,
                report.summary.totalSize,
                stopwatch.Elapsed));

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
