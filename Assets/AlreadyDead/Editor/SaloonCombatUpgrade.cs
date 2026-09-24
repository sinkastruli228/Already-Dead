using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    public static class SaloonCombatUpgrade
    {
        private const string ScenePath = SaloonSceneBuilder.ScenePath;
        private const string RevolverPrefab = "Assets/AlreadyDead/SaloonKit/Prefabs/Revolver.prefab";
        private const string RevolverArt = "Assets/Waepon/Revolver/Revolver_UP.png";
        private const string LitMaterial = "Assets/AlreadyDead/BuildingKit/Pixel Lit.mat";
        private const string Tuning = "Assets/AlreadyDead/PrototypeTuning.asset";
        private static readonly HashSet<string> Gunmen = new HashSet<string>
        {
            "Cowboy / sketch marker 01", "Cowboy / sketch marker 08",
            "Cowboy / sketch marker 13", "Cowboy / sketch marker 22",
            "Cowboy / sketch marker 24"
        };

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath("UpgradeSaloonCombat.request");
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
                    File.WriteAllText(TempPath("UpgradeSaloonCombat.result.txt"),
                        "Saloon revolver enlarged, two-second reload, five enemy gunmen; scene saved.");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(TempPath("UpgradeSaloonCombat.error.txt"),
                        exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Upgrade Saloon revolver and enemy gunmen")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the current Saloon layout.");
            string backup = TempPath("SaloonScene.before-combat-upgrade.unity");
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);

            UpgradeRevolverPrefab();
            PrototypeTuning tuning = AssetDatabase.LoadAssetAtPath<PrototypeTuning>(Tuning);
            Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterial);
            Sprite revolverSprite = FindSprite(RevolverArt, "Revolver_UP_0");
            Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Square.png");
            Material fallbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/AlreadyDead/Art/Primitive.mat");
            if (tuning == null || lit == null || revolverSprite == null ||
                fallback == null || fallbackMaterial == null)
                throw new InvalidOperationException("Saloon gun assets are incomplete.");

            int installed = 0;
            bool foundPlayerRevolver = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PistolWeapon playerRevolver = root.GetComponent<PistolWeapon>();
                if (playerRevolver != null && playerRevolver.IsRevolver)
                {
                    playerRevolver.SetRevolverReloadSeconds(2f);
                    playerRevolver.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                    playerRevolver.name = "Revolver / 6 rounds + 2s reload";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(playerRevolver);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(playerRevolver.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(playerRevolver.gameObject);
                    foundPlayerRevolver = true;
                }
                foreach (PatrolEnemy enemy in root.GetComponentsInChildren<PatrolEnemy>(true))
                {
                    if (!Gunmen.Contains(enemy.name)) continue;
                    installed++;
                    if (enemy.GetComponent<EnemyRevolver>() != null) continue;
                    InstallEnemyRevolver(enemy, tuning, lit, revolverSprite,
                        fallback, fallbackMaterial);
                }
            }
            if (!foundPlayerRevolver || installed != Gunmen.Count)
                throw new InvalidOperationException("Expected Saloon revolver or five selected cowboys are missing: " + installed);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the upgraded Saloon scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_SALOON_COMBAT_READY: " + installed + " gunmen");
        }

        private static void UpgradeRevolverPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RevolverPrefab);
            try
            {
                PistolWeapon weapon = root.GetComponent<PistolWeapon>();
                if (weapon == null || !weapon.IsRevolver)
                    throw new InvalidOperationException("The Saloon revolver prefab is missing.");
                root.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                weapon.SetRevolverReloadSeconds(2f);
                PrefabUtility.SaveAsPrefabAsset(root, RevolverPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void InstallEnemyRevolver(PatrolEnemy enemy, PrototypeTuning tuning,
            Material lit, Sprite gunSprite, Sprite fallback, Material fallbackMaterial)
        {
            Transform facing = enemy.Facing;
            if (facing == null) throw new InvalidOperationException("Cowboy facing is missing: " + enemy.name);
            var gun = new GameObject("Held revolver / no ricochet");
            gun.transform.SetParent(facing, false);
            gun.transform.localPosition = new Vector3(0.44f, -0.17f, 0f);
            gun.transform.localScale = new Vector3(0.24f, 0.24f, 1f);
            SpriteRenderer visual = gun.AddComponent<SpriteRenderer>();
            visual.sprite = gunSprite;
            visual.sharedMaterial = lit;
            visual.sortingOrder = 17;

            Transform muzzle = new GameObject("Enemy revolver muzzle").transform;
            muzzle.SetParent(facing, false);
            muzzle.localPosition = new Vector3(0.82f, -0.17f, 0f);
            var flashObject = new GameObject("Enemy muzzle flash");
            flashObject.transform.SetParent(muzzle, false);
            SpriteRenderer flash = flashObject.AddComponent<SpriteRenderer>();
            flash.sprite = fallback;
            flash.sharedMaterial = lit;
            flash.sortingOrder = 19;
            flash.enabled = false;

            EnemyRevolver revolver = enemy.gameObject.AddComponent<EnemyRevolver>();
            revolver.Configure(tuning, muzzle, flash, fallback, fallbackMaterial);
        }

        private static Sprite FindSprite(string path, string name)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == name) return sprite;
            return null;
        }

        private static string TempPath(string name) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp/" + name));
    }
}
