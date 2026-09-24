using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    public static class SaloonM4Upgrade
    {
        private const string ScenePath = SaloonSceneBuilder.ScenePath;
        private const string RequestName = "UpgradeSaloonM4.request";

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath(RequestName);
                if (!File.Exists(request)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckRequest;
                    return;
                }
                try
                {
                    Apply();
                    File.Delete(request);
                    File.WriteAllText(TempPath("UpgradeSaloonM4.result.txt"),
                        "Saloon M4: infinite ammo, five ricochets; scene saved.");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(TempPath("UpgradeSaloonM4.error.txt"),
                        exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Give Saloon M4 infinite ammo and five ricochets")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the current Saloon layout.");

            string backup = TempPath("SaloonScene.before-m4-ricochet.unity");
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);

            PistolWeapon m4 = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "M4 / 25 rounds automatic")
                {
                    m4 = root.GetComponent<PistolWeapon>();
                    break;
                }
            if (m4 == null || !m4.Automatic || m4.IsRevolver)
                throw new InvalidOperationException("The Saloon M4 was not found or has unexpected settings.");

            m4.ConfigureSpecialAmmo(true, 5);
            EditorUtility.SetDirty(m4);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the upgraded Saloon M4.");
            Debug.Log("ALREADY_DEAD_SALOON_M4_UPGRADED: " + ScenePath);
        }

        private static string TempPath(string filename) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp/" + filename));
    }
}
