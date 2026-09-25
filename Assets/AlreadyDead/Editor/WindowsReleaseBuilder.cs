using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlreadyDead.Editor
{
    public static class WindowsReleaseBuilder
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes/FantasyScene.unity",
            "Assets/Scenes/BuildingParkingScene.unity",
            "Assets/Scenes/SaloonScene.unity"
        };

        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string Request => Path.Combine(Root, "Temp", "BuildWindowsRelease.request");
        private static string Running => Path.Combine(Root, "Temp", "BuildWindowsRelease.running");
        private static string Result => Path.Combine(Root, "Temp", "BuildWindowsRelease.result.txt");

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedBuild() => EditorApplication.delayCall += BuildIfRequested;

        private static void BuildIfRequested()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += BuildIfRequested;
                return;
            }

            try { File.Move(Request, Running); }
            catch (IOException) { return; }

            try
            {
                Build();
            }
            catch (Exception error)
            {
                File.WriteAllText(Result, "Failed:\n" + error);
                Debug.LogException(error);
            }
            finally
            {
                if (File.Exists(Running)) File.Delete(Running);
            }
        }

        [MenuItem("Already Dead/Build Windows Release")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new IOException("Could not save open scenes.");
            foreach (string scene in Scenes)
                if (!File.Exists(Path.Combine(Root, scene)))
                    throw new FileNotFoundException("Build scene is missing", scene);

            string executable = Path.Combine(Root, "Build", "WindowsRelease", "Already Dead.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executable));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            File.WriteAllText(Result,
                $"Result: {report.summary.result}\nOutput: {executable}\n" +
                $"Scenes: {string.Join(", ", Scenes)}\nErrors: {report.summary.totalErrors}\n" +
                $"Warnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\n");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
        }
    }
}
