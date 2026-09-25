using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    public static class HospitalKnifeUpgrade
    {
        private const string ScenePath = "Assets/Scenes/BuildingParkingScene.unity";
        private const string KnifePath = "Assets/Waepon/Knife/Knife.png";
        private const string LitMaterialPath = "Assets/AlreadyDead/BuildingKit/Pixel Lit.mat";

        [InitializeOnLoadMethod]
        private static void CheckPendingRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath("InstallHospitalKnives.request");
                if (!File.Exists(request)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                try
                {
                    Install();
                    File.Delete(request);
                    File.WriteAllText(TempPath("InstallHospitalKnives.result.txt"),
                        "Hospital guards 3, 6, and 9 now carry knives; scene saved.");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(TempPath("InstallHospitalKnives.error.txt"),
                        exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Give marked hospital guards knives")]
        public static void Install()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the current hospital scene.");
            string backup = TempPath("BuildingParkingScene.before-knives.unity");
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);

            Sprite knife = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(KnifePath))
                if (asset is Sprite sprite && sprite.name == "Knife_0") knife = sprite;
            Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (knife == null || lit == null)
                throw new InvalidOperationException("Hospital knife sprite or lit material is missing.");

            foreach (string name in new[]
                { "Hospital guard / 3", "Hospital guard / 6", "Hospital guard / 9 / knife" })
            {
                PatrolEnemy enemy = FindGuard(scene, name);
                if (enemy == null || enemy.Facing == null)
                    throw new InvalidOperationException("Missing marked hospital guard: " + name);
                Convert(enemy, knife, lit);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the hospital scene with knife guards.");
            Debug.Log("ALREADY_DEAD_HOSPITAL_KNIVES_READY");
        }

        private static void Convert(PatrolEnemy enemy, Sprite knife, Material lit)
        {
            foreach (PistolWeapon weapon in enemy.GetComponentsInChildren<PistolWeapon>(true))
                if (weapon.transform != enemy.transform)
                    UnityEngine.Object.DestroyImmediate(weapon.gameObject);
            foreach (MusketWeapon weapon in enemy.GetComponentsInChildren<MusketWeapon>(true))
                UnityEngine.Object.DestroyImmediate(weapon.gameObject);
            foreach (RockWeapon weapon in enemy.GetComponentsInChildren<RockWeapon>(true))
                UnityEngine.Object.DestroyImmediate(weapon.gameObject);
            foreach (SpearWeapon weapon in enemy.GetComponentsInChildren<SpearWeapon>(true))
                UnityEngine.Object.DestroyImmediate(weapon.gameObject);
            foreach (ClubWeapon weapon in enemy.GetComponentsInChildren<ClubWeapon>(true))
                UnityEngine.Object.DestroyImmediate(weapon.gameObject);

            EnemyGlock glock = enemy.GetComponent<EnemyGlock>();
            if (glock != null) UnityEngine.Object.DestroyImmediate(glock);
            EnemyRevolver revolver = enemy.GetComponent<EnemyRevolver>();
            if (revolver != null) UnityEngine.Object.DestroyImmediate(revolver);
            EnemyWeaponLoadout loadout = enemy.GetComponent<EnemyWeaponLoadout>();
            if (loadout != null) UnityEngine.Object.DestroyImmediate(loadout);

            Transform hand = enemy.Facing.Find("Held knife / 1.4x");
            if (hand == null)
            {
                hand = new GameObject("Held knife / 1.4x").transform;
                hand.SetParent(enemy.Facing, false);
            }
            SpriteRenderer renderer = hand.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = hand.gameObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = lit;
            renderer.sortingOrder = 16;
            renderer.color = Color.white;

            SaloonHandProp held = enemy.GetComponent<SaloonHandProp>();
            if (held == null) held = enemy.gameObject.AddComponent<SaloonHandProp>();
            held.Configure(renderer, knife, null, true);
            held.Equip(SaloonHandProp.PropKind.Knife);
        }

        private static PatrolEnemy FindGuard(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.GetComponent<PatrolEnemy>();
            return null;
        }

        private static string TempPath(string name) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp", name));
    }
}
