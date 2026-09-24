using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    // Adds firearms to the authored hospital scene without rebuilding its layout.
    public static class HospitalGlockUpgrade
    {
        private const string ScenePath = BuildingSceneBuilder.ScenePath;
        private const string Request = "InstallHospitalGlocks.request";
        private static readonly HashSet<string> Gunmen = new HashSet<string>
        {
            "Hospital guard / 1", "Hospital guard / 3",
            "Hospital guard / 6", "Hospital guard / 8"
        };

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath(Request);
                if (!File.Exists(request)) return;
                try
                {
                    Install();
                    File.Delete(request);
                    File.WriteAllText(TempPath("InstallHospitalGlocks.result.txt"),
                        "Four hospital guards carry 17-round Glocks; scene saved.");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(TempPath("InstallHospitalGlocks.error.txt"),
                        exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Equip half the hospital guards with Glocks")]
        public static void Install()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the current hospital layout.");

            string backup = TempPath("BuildingParkingScene.before-glocks.unity");
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);

            PistolWeapon source = null;
            var selected = new List<PatrolEnemy>();
            int totalGuards = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PistolWeapon candidate = root.GetComponent<PistolWeapon>();
                if (candidate != null && root.name == "Glock / 17 rounds")
                    source = candidate;
                foreach (PatrolEnemy enemy in root.GetComponentsInChildren<PatrolEnemy>(true))
                {
                    if (!enemy.name.StartsWith("Hospital guard / ",
                            StringComparison.Ordinal)) continue;
                    totalGuards++;
                    if (Gunmen.Contains(enemy.name)) selected.Add(enemy);
                }
            }
            if (source == null || source.Capacity != 17 || source.IsRevolver ||
                totalGuards != 8 || selected.Count != 4)
                throw new InvalidOperationException("Expected one 17-round Glock and eight hospital guards.");

            foreach (PatrolEnemy enemy in selected)
            {
                if (enemy.GetComponent<EnemyGlock>() != null) continue;
                if (enemy.Facing == null)
                    throw new InvalidOperationException("Guard has no facing transform: " + enemy.name);

                GameObject carried = UnityEngine.Object.Instantiate(source.gameObject,
                    enemy.Facing, false);
                carried.name = "Held Glock / drops with remaining rounds";
                carried.transform.localPosition = new Vector3(0.48f, -0.19f, 0f);
                carried.transform.localRotation = Quaternion.identity;
                carried.transform.localScale = Vector3.one;
                carried.GetComponent<Rigidbody2D>().simulated = false;
                carried.GetComponent<BoxCollider2D>().enabled = false;
                foreach (SpriteRenderer renderer in carried.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.name == "Glock side / ground") renderer.enabled = false;
                    if (renderer.name == "Glock top / held") renderer.enabled = true;
                }

                EnemyGlock gunner = enemy.gameObject.AddComponent<EnemyGlock>();
                gunner.Configure(carried.GetComponent<PistolWeapon>());
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the equipped hospital scene.");
            Debug.Log("ALREADY_DEAD_HOSPITAL_GLOCKS_READY: 4 of 8 guards");
        }

        private static string TempPath(string name) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp/" + name));
    }
}
