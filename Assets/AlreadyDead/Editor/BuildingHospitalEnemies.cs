using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AlreadyDead.Editor
{
    // Adds guards to the authored hospital layout without rebuilding its tilemaps or props.
    public static class BuildingHospitalEnemies
    {
        private const string ScenePath = BuildingSceneBuilder.ScenePath;
        private const string SamplePath = PrototypeSceneBuilder.ScenePath;
        private const string TuningPath = "Assets/AlreadyDead/PrototypeTuning.asset";
        private const string LitMaterialPath = "Assets/AlreadyDead/BuildingKit/Pixel Lit.mat";
        private const string MarkerName = "InstallHospitalEnemies.request";
        private static readonly Vector2[] Positions =
        {
            new Vector2(-15f, 36f), new Vector2(0f, 38f),
            new Vector2(15f, 36f), new Vector2(-15f, 28f),
            new Vector2(0f, 27f), new Vector2(15f, 26f),
            new Vector2(-13f, 17f), new Vector2(14f, 18f)
        };

        [InitializeOnLoadMethod]
        private static void CheckPendingRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string marker = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Temp/" + MarkerName));
                if (!File.Exists(marker)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckPendingRequest;
                    return;
                }
                try
                {
                    Install();
                    File.Delete(marker);
                    File.WriteAllText(Path.Combine(Application.dataPath,
                        "../Temp/InstallHospitalEnemies.result.txt"), "Hospital enemies saved in " + ScenePath);
                }
                catch (Exception exception)
                {
                    File.WriteAllText(Path.Combine(Application.dataPath,
                        "../Temp/InstallHospitalEnemies.error.txt"), exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Add enemies to existing hospital scene")]
        public static void Install()
        {
            Scene target = SceneManager.GetSceneByPath(ScenePath);
            if (!target.IsValid() || !target.isLoaded)
                target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(target))
                throw new IOException("Could not save the current hospital layout.");

            string backup = Path.Combine(Application.dataPath,
                "../Temp/BuildingParkingScene.before-enemies.unity");
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);

            TopDownPlayer player = FindRoot<TopDownPlayer>(target);
            PrototypeTuning tuning = AssetDatabase.LoadAssetAtPath<PrototypeTuning>(TuningPath);
            Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (player == null || tuning == null || lit == null)
                throw new InvalidOperationException("Hospital player, tuning, or lit material is missing.");

            var tilemaps = new List<Tilemap>();
            foreach (GameObject root in target.GetRootGameObjects())
                tilemaps.AddRange(root.GetComponentsInChildren<Tilemap>(true));
            var occupied = new List<Vector2>();
            Physics2D.SyncTransforms();
            Scene sample = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Additive);
            try
            {
                PatrolEnemy source = FindRoot<PatrolEnemy>(sample);
                if (source == null) throw new InvalidOperationException("Prototype enemy is missing.");
                SceneManager.SetActiveScene(target);
                for (int i = 0; i < Positions.Length; i++)
                {
                    string name = "Hospital guard / " + (i + 1);
                    if (FindNamedRoot(target, name) != null) continue;
                    Vector2 start = FindOpenSpot(Positions[i], tilemaps, occupied);
                    occupied.Add(start);
                    Vector2 end = FindNearbySpot(start, tilemaps);

                    GameObject copy = UnityEngine.Object.Instantiate(source.gameObject);
                    copy.name = name;
                    copy.transform.SetParent(null);
                    SceneManager.MoveGameObjectToScene(copy, target);
                    copy.transform.position = start;
                    foreach (SpriteRenderer renderer in copy.GetComponentsInChildren<SpriteRenderer>(true))
                        renderer.sharedMaterial = lit;
                    PatrolEnemy enemy = copy.GetComponent<PatrolEnemy>();
                    enemy.ConfigureRoute(tuning, player, enemy.Facing, null,
                        new[] { start, end });
                }
            }
            finally { EditorSceneManager.CloseScene(sample, true); }

            SceneManager.SetActiveScene(target);
            EditorSceneManager.MarkSceneDirty(target);
            if (!EditorSceneManager.SaveScene(target))
                throw new IOException("Could not save hospital enemies.");
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_HOSPITAL_ENEMIES_READY: " + ScenePath);
        }

        private static Vector2 FindOpenSpot(Vector2 anchor, List<Tilemap> maps,
            List<Vector2> occupied)
        {
            for (float radius = 0f; radius <= 4f; radius += 0.5f)
                for (int direction = 0; direction < 16; direction++)
                {
                    float angle = direction * Mathf.PI / 8f;
                    Vector2 point = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (IsOpen(point, maps) && !occupied.Exists(other =>
                            Vector2.Distance(other, point) < 3f)) return point;
                }
            throw new InvalidOperationException("No clear hospital floor near " + anchor);
        }

        private static Vector2 FindNearbySpot(Vector2 start, List<Tilemap> maps)
        {
            Vector2[] offsets = { Vector2.right * 1.5f, Vector2.left * 1.5f,
                Vector2.up * 1.5f, Vector2.down * 1.5f };
            foreach (Vector2 offset in offsets)
            {
                Vector2 end = start + offset;
                if (IsOpen(end, maps) &&
                    Physics2D.Linecast(start, end, (1 << 8) | (1 << 12)).collider == null)
                    return end;
            }
            return start;
        }

        private static bool IsOpen(Vector2 point, List<Tilemap> maps)
        {
            if (Physics2D.OverlapCircle(point, 0.65f, (1 << 8) | (1 << 12)) != null)
                return false;
            foreach (Tilemap map in maps)
            {
                TileBase tile = map.GetTile(map.WorldToCell(point));
                if (tile != null && tile.name == "Parquet") return true;
            }
            return false;
        }

        private static T FindRoot<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponent<T>();
                if (component != null) return component;
            }
            return null;
        }

        private static GameObject FindNamedRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
