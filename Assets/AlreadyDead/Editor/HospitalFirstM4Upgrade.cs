using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    public static class HospitalFirstM4Upgrade
    {
        private const string ScenePath = BuildingSceneBuilder.ScenePath;

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath("InstallHospitalFirstM4.request");
                if (!File.Exists(request)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckRequest;
                    return;
                }
                try
                {
                    Install();
                    File.Delete(request);
                    File.WriteAllText(TempPath("InstallHospitalFirstM4.result.txt"),
                        "Nearest hospital guard has M4; four other guards retain Glocks; scene saved.");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(TempPath("InstallHospitalFirstM4.error.txt"),
                        exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Give the first hospital guard an M4")]
        public static void Install()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the hospital layout before upgrading weapons.");
            string backup = TempPath("BuildingParkingScene.before-first-m4.unity");
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);

            PatrolEnemy first = null;
            PatrolEnemy replacementGlock = null;
            PistolWeapon m4 = null;
            PistolWeapon glock = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PistolWeapon candidate in root.GetComponentsInChildren<PistolWeapon>(true))
                    if (candidate.Automatic && candidate.Capacity == 25 &&
                        !candidate.HasInfiniteAmmo) m4 = candidate;
                if (root.name == "Glock / 17 rounds") glock = root.GetComponent<PistolWeapon>();
                foreach (PatrolEnemy enemy in root.GetComponentsInChildren<PatrolEnemy>(true))
                {
                    if (enemy.name == "Hospital guard / 7") first = enemy;
                    if (enemy.name == "Hospital guard / 6") replacementGlock = enemy;
                }
            }
            if (first == null || replacementGlock == null || m4 == null || glock == null ||
                !m4.Automatic || m4.Capacity != 25 || m4.HasInfiniteAmmo ||
                glock.Automatic || glock.Capacity != 17)
                throw new InvalidOperationException("Expected hospital guards 6 and 7, the 25-round M4, and the 17-round Glock.");

            EnemyGlock firstGun = first.GetComponent<EnemyGlock>();
            if (firstGun == null || firstGun.Weapon == null)
                throw new InvalidOperationException("The nearest guard's Glock is missing.");

            if (replacementGlock.GetComponent<EnemyGlock>() == null)
            {
                PistolWeapon carriedGlock = Attach(glock, replacementGlock,
                    "Held Glock / drops with remaining rounds",
                    "Glock side / ground", "Glock top / held");
                EnemyGlock gunner = replacementGlock.gameObject.AddComponent<EnemyGlock>();
                gunner.Configure(carriedGlock);
            }

            if (!firstGun.Weapon.Automatic)
            {
                if (!firstGun.Weapon.transform.IsChildOf(first.transform))
                    throw new InvalidOperationException("The nearest guard's Glock is not carried by that guard.");
                UnityEngine.Object.DestroyImmediate(firstGun.Weapon.gameObject);
                PistolWeapon carriedM4 = Attach(m4, first,
                    "Held M4 / drops with remaining rounds",
                    "M4 side / ground", "M4 top / held");
                firstGun.Configure(carriedM4);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the hospital scene with the M4 guard.");
            Debug.Log("ALREADY_DEAD_HOSPITAL_FIRST_M4_READY");
        }

        private static PistolWeapon Attach(PistolWeapon source, PatrolEnemy enemy,
            string name, string groundName, string heldName)
        {
            if (enemy.Facing == null)
                throw new InvalidOperationException("Guard has no facing transform: " + enemy.name);
            GameObject carried = UnityEngine.Object.Instantiate(source.gameObject, enemy.Facing, false);
            carried.name = name;
            carried.transform.localPosition = new Vector3(0.48f, -0.19f, 0f);
            carried.transform.localRotation = Quaternion.identity;
            carried.transform.localScale = source.transform.localScale;
            carried.GetComponent<Rigidbody2D>().simulated = false;
            carried.GetComponent<BoxCollider2D>().enabled = false;
            foreach (SpriteRenderer renderer in carried.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == groundName) renderer.enabled = false;
                if (renderer.name == heldName) renderer.enabled = true;
            }
            return carried.GetComponent<PistolWeapon>();
        }

        private static string TempPath(string name) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp/" + name));
    }
}
