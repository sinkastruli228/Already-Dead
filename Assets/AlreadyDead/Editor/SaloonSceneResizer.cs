using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace AlreadyDead.Editor
{
    public static class SaloonSceneResizer
    {
        private const float Scale = 1.5f;
        private const string ScenePath = SaloonSceneBuilder.ScenePath;
        private const string RevolverPrefabPath = "Assets/AlreadyDead/SaloonKit/Prefabs/Revolver.prefab";
        private const string MarkerName = "SALOON RESIZED x1.5 / empty interior";
        private static readonly HashSet<string> FurnitureNames = new HashSet<string>
        {
            "RoundTable", "Chair", "BarCounter", "BottleShelf", "PrepTable",
            "Stove", "Toilet", "Crate", "Barrel"
        };

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath("ResizeSaloon.request");
                if (!File.Exists(request)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckRequest;
                    return;
                }
                try
                {
                    ResizeAndInstallRevolver();
                    File.Delete(request);
                    File.WriteAllText(TempPath("ResizeSaloon.result.txt"), "SALOON_RESIZED_OK");
                }
                catch (Exception error)
                {
                    File.WriteAllText(TempPath("ResizeSaloon.error.txt"), error.ToString());
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("Already Dead/Enlarge Saloon x1.5 and empty its interior")]
        public static void ResizeAndInstallRevolver()
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new IOException("Could not save the open scenes before resizing Saloon.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == MarkerName)
                    throw new InvalidOperationException("Saloon has already been enlarged.");

            string backup = TempPath("SaloonScene.before-1.5.unity");
            if (!File.Exists(backup)) File.Copy(Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", ScenePath)), backup);

            Transform grid = FindRoot(scene, "EDITABLE SALOON TILEMAPS / paint from Saloon palette");
            if (grid == null) throw new InvalidOperationException("The editable Saloon tilemaps are missing.");
            foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>())
            {
                if (tilemap.name.StartsWith("04 Furniture", StringComparison.Ordinal))
                    tilemap.ClearAllTiles();
                else EnlargeTilemap(tilemap);
                tilemap.RefreshAllTiles();
                tilemap.CompressBounds();
            }

            Transform furniture = FindRoot(scene, "FURNITURE / move prefab instances freely");
            if (furniture != null)
                for (int index = furniture.childCount - 1; index >= 0; index--)
                    Object.DestroyImmediate(furniture.GetChild(index).gameObject);
            RemoveOtherInteriorFurniture(scene);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.transform == grid || root.transform == furniture) continue;
                if (root.GetComponent<Camera>() != null || root.GetComponent<PrototypeHud>() != null ||
                    root.GetComponent<Light2D>()?.lightType == Light2D.LightType.Global) continue;
                if (root.name.StartsWith("OUTDOORS /", StringComparison.Ordinal) ||
                    root.name.StartsWith("ENEMIES /", StringComparison.Ordinal))
                {
                    foreach (Transform child in root.transform)
                        child.position = new Vector3(child.position.x * Scale,
                            child.position.y * Scale, child.position.z);
                }
                else
                {
                    Vector3 position = root.transform.position;
                    root.transform.position = new Vector3(position.x * Scale,
                        position.y * Scale, position.z);
                }
                Light2D light = root.GetComponent<Light2D>();
                if (light != null && light.lightType == Light2D.LightType.Point)
                {
                    light.pointLightInnerRadius *= Scale;
                    light.pointLightOuterRadius *= Scale;
                }
            }

            TopDownPlayer player = FindRootComponent<TopDownPlayer>(scene);
            if (player == null) throw new InvalidOperationException("The Saloon player is missing.");
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (PatrolEnemy enemy in root.GetComponentsInChildren<PatrolEnemy>(true))
            {
                Vector2[] route = enemy.PatrolRoute;
                if (route == null || route.Length == 0) continue;
                Vector2[] enlarged = new Vector2[route.Length];
                for (int i = 0; i < route.Length; i++) enlarged[i] = route[i] * Scale;
                SaloonEnemySpawn spawn = enemy.GetComponent<SaloonEnemySpawn>();
                if (spawn != null) spawn.SetSceneRoute(player, enlarged);
            }

            PushDoor2D door = FindRootComponent<PushDoor2D>(scene);
            if (door != null)
            {
                door.transform.position = new Vector3(-2.5f, -9f, door.transform.position.z);
                door.transform.localScale = new Vector3(1.5f, 1f, 1f);
            }

            Camera camera = FindRootComponent<Camera>(scene);
            if (camera != null)
                camera.transform.position = player.transform.position + Vector3.back * 10f;

            GameObject revolverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RevolverPrefabPath);
            if (revolverPrefab == null)
            {
                PistolWeapon revolver = PrototypeSceneBuilder.BuildRevolverInOpenScene(
                    new Vector2(0f, -14.5f));
                PrefabUtility.SaveAsPrefabAssetAndConnect(revolver.gameObject, RevolverPrefabPath,
                    InteractionMode.AutomatedAction);
            }
            else
            {
                GameObject revolver = (GameObject)PrefabUtility.InstantiatePrefab(revolverPrefab, scene);
                revolver.transform.position = new Vector3(0f, -14.5f, 0f);
            }

            new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save enlarged SaloonScene.");
            AssetDatabase.SaveAssets();
            Debug.Log("SALOON_RESIZED_OK: x1.5 tile area, empty interior, revolver placed.");
        }

        private static void EnlargeTilemap(Tilemap map)
        {
            BoundsInt original = map.cellBounds;
            var tiles = new Dictionary<Vector3Int, TileBase>();
            foreach (Vector3Int cell in original.allPositionsWithin)
            {
                TileBase tile = map.GetTile(cell);
                if (tile != null) tiles.Add(cell, tile);
            }
            map.ClearAllTiles();
            int minX = Mathf.FloorToInt(original.xMin * Scale);
            int minY = Mathf.FloorToInt(original.yMin * Scale);
            int maxX = Mathf.CeilToInt(original.xMax * Scale);
            int maxY = Mathf.CeilToInt(original.yMax * Scale);
            for (int x = minX; x < maxX; x++)
            for (int y = minY; y < maxY; y++)
            {
                var source = new Vector3Int(Mathf.FloorToInt((x + 0.5f) / Scale),
                    Mathf.FloorToInt((y + 0.5f) / Scale), 0);
                if (tiles.TryGetValue(source, out TileBase tile))
                    map.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        private static void RemoveOtherInteriorFurniture(Scene scene)
        {
            var remove = new HashSet<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject);
                if (instance == null || remove.Contains(instance)) continue;
                Vector3 position = instance.transform.position;
                if (position.x < -15f || position.x > 15f ||
                    position.y < -6f || position.y > 11f) continue;
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                if (source == null) continue;
                string path = AssetDatabase.GetAssetPath(source);
                if (!path.StartsWith("Assets/AlreadyDead/SaloonKit/Prefabs/", StringComparison.Ordinal) ||
                    !FurnitureNames.Contains(Path.GetFileNameWithoutExtension(path))) continue;
                remove.Add(instance);
            }
            foreach (GameObject instance in remove) Object.DestroyImmediate(instance);
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }

        private static T FindRootComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<T>() is T found) return found;
            return null;
        }

        private static string TempPath(string filename) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp", filename));
    }
}
