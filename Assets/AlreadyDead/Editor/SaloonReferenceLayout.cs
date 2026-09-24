using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace AlreadyDead.Editor
{
    // Applies the supplied floor plan to the existing, enlarged SaloonScene.
    public static class SaloonReferenceLayout
    {
        private const string ScenePath = "Assets/Scenes/SaloonScene.unity";
        private const string Kit = "Assets/AlreadyDead/SaloonKit/";
        private const string Marker = "SALOON / reference floor plan / 25 patrol enemies";
        private const string DecorName = "DECOR / barrels, crates and saloon props";

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string decorationRequest = TempPath("DecorateSaloon.request");
                if (File.Exists(decorationRequest))
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorApplication.delayCall += CheckRequest;
                        return;
                    }
                    try
                    {
                        DecorateCurrentSaloon();
                        File.Delete(decorationRequest);
                        File.WriteAllText(TempPath("DecorateSaloon.result.txt"), "SALOON_DECOR_OK");
                    }
                    catch (Exception exception)
                    {
                        File.WriteAllText(TempPath("DecorateSaloon.error.txt"), exception.ToString());
                        Debug.LogException(exception);
                    }
                    return;
                }
                string request = TempPath("ApplySaloonReference.request");
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
                    File.WriteAllText(TempPath("ApplySaloonReference.result.txt"), "SALOON_REFERENCE_OK");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(TempPath("ApplySaloonReference.error.txt"), exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Apply reference layout to SaloonScene")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            if (FindRoot(scene, Marker) != null)
            {
                PlaceStarterWeapons(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            string backup = TempPath("SaloonScene.before-reference-layout.unity");
            if (!File.Exists(backup))
                File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath)), backup);

            Transform grid = FindRoot(scene, "EDITABLE SALOON TILEMAPS / paint from Saloon palette");
            Transform furniture = FindRoot(scene, "FURNITURE / move prefab instances freely");
            Transform enemies = FindRoot(scene, "ENEMIES / 13 of 26 red markers");
            TopDownPlayer player = FindComponent<TopDownPlayer>(scene);
            if (grid == null || furniture == null || enemies == null || player == null)
                throw new InvalidOperationException("The existing Saloon grid, furniture, enemies or player is missing.");

            Tilemap floor = FindMap(grid, "02 Board floor");
            Tilemap walls = FindMap(grid, "03 Wood walls");
            Tilemap furnitureTiles = FindMap(grid, "04 Furniture");
            Tilemap decorations = FindMap(grid, "05 Decorations");
            TileBase board = AssetDatabase.LoadAssetAtPath<TileBase>(Kit + "Tiles/BoardFloor.asset");
            TileBase wood = AssetDatabase.LoadAssetAtPath<TileBase>(Kit + "Tiles/WoodWall.asset");
            if (board == null || wood == null) throw new InvalidOperationException("Saloon palette tiles are missing.");

            floor.ClearAllTiles();
            walls.ClearAllTiles();
            furnitureTiles.ClearAllTiles();
            decorations.ClearAllTiles();
            for (int x = -22; x <= 21; x++)
            for (int y = -8; y <= 16; y++)
                floor.SetTile(new Vector3Int(x, y, 0), board);
            for (int x = -5; x <= 4; x++)
            for (int y = -17; y <= -10; y++)
                floor.SetTile(new Vector3Int(x, y, 0), board);

            void Wall(int x, int y) => walls.SetTile(new Vector3Int(x, y, 0), wood);
            void Horizontal(int left, int right, int y, int gapLeft = 1, int gapRight = 0)
            {
                for (int x = left; x <= right; x++)
                    if (x < gapLeft || x > gapRight) Wall(x, y);
            }
            void Vertical(int x, int bottom, int top, int gap1Bottom = 1, int gap1Top = 0,
                int gap2Bottom = 1, int gap2Top = 0)
            {
                for (int y = bottom; y <= top; y++)
                    if ((y < gap1Bottom || y > gap1Top) && (y < gap2Bottom || y > gap2Top))
                        Wall(x, y);
            }

            // Exterior, the two west rooms, the north room, kitchen and two small east rooms.
            Horizontal(-23, 22, 17, -20, -18);
            Horizontal(-23, 22, -9, -1, 0);
            Vertical(-23, -8, 16);
            Vertical(22, -8, 16);
            Horizontal(-23, -13, 3, -20, -18);
            Vertical(-13, -8, 16, -5, -3, 12, 14);
            Horizontal(-13, 13, 9);
            Vertical(13, -8, 16, -7, -5, 12, 14);
            Horizontal(13, 22, 0, 14, 16);
            Vertical(18, -8, -1, -7, -5, -3, -1);
            Horizontal(18, 22, -4);

            // Porch entrance and outer door.
            Vertical(-6, -17, -10);
            Vertical(5, -17, -10);
            Horizontal(-6, 5, -18, -1, 0);

            foreach (Tilemap map in new[] { floor, walls, furnitureTiles, decorations })
            {
                map.RefreshAllTiles();
                map.CompressBounds();
            }
            walls.gameObject.layer = LayerMask.NameToLayer("Walls");
            furnitureTiles.gameObject.layer = LayerMask.NameToLayer("Furniture");

            ClearChildren(furniture);
            ClearChildren(enemies);
            enemies.name = "ENEMIES / 25 reference markers / PatrolEnemy";

            // The brown symbols in the sketch: three tables and two bar counters.
            Prop("RoundTable", furniture, -7.9f, 1.9f);
            Prop("RoundTable", furniture, -4.6f, -3.9f);
            Prop("RoundTable", furniture, 7.5f, -0.5f);
            for (int i = 0; i < 3; i++) Prop("BarCounter", furniture, -3.1f + 3.9f * i, 5.6f);
            Prop("BarCounter", furniture, 17.6f, 10.3f, 90f);
            Prop("BarCounter", furniture, 17.6f, 6.4f, 90f);

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<PushDoor2D>() != null) Object.DestroyImmediate(root);
            GameObject doorPrefab = Prefab("SwingDoor");
            void Door(string label, float x, float y, float angle)
            {
                GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, scene);
                door.name = "Door / " + label;
                door.transform.position = new Vector3(x, y, 0f);
                door.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                door.layer = LayerMask.NameToLayer("Walls");
            }
            Door("north exterior", -20.5f, 17.5f, 0f);
            Door("west rooms", -20.5f, 3.5f, 0f);
            Door("north room west", -12.5f, 11.5f, 90f);
            Door("north room east", 13.5f, 11.5f, 90f);
            Door("main hall west", -12.5f, -5.5f, 90f);
            Door("main hall east", 13.5f, -7.5f, 90f);
            Door("kitchen south", 13.5f, 0.5f, 0f);
            Door("east upper room", 18.5f, -3.5f, 90f);
            Door("east lower room", 18.5f, -7.5f, 90f);
            Door("porch interior", -1.5f, -8.5f, 0f);
            Door("porch exterior", -1.5f, -17.5f, 0f);

            // Routes stay inside each room. PatrolEnemy already handles sight, pursuit,
            // nearby death, investigation and returning to these waypoints.
            void Enemy(string role, int number, float x, float y, float dx, float dy)
            {
                GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(role), scene);
                enemy.name = role + " / sketch marker " + number.ToString("00");
                enemy.transform.SetParent(enemies, false);
                Vector2 start = new Vector2(x, y);
                enemy.transform.position = start;
                enemy.layer = 11; // The existing enemyMask in PrototypeTuning is 1 << 11.
                enemy.GetComponent<SaloonEnemySpawn>().SetSceneRoute(player,
                    new[] { start, start + new Vector2(dx, dy) });
            }
            Enemy("Cowboy", 1, -20.7f, 15.0f, 1.3f, 0f);
            Enemy("Cowboy", 2, -16.4f, 15.0f, 1.2f, 0f);
            Enemy("Cowboy", 3, -20.9f, 8.7f, 0f, -1.5f);
            Enemy("Cowboy", 4, -21.3f, 1.1f, 1.2f, 0f);
            Enemy("Cowboy", 5, -15.8f, -7.3f, 0f, 1.3f);
            Enemy("Cowboy", 6, -10.8f, 13.2f, 1.5f, 0f);
            Enemy("Cowboy", 7, -2.4f, 13.2f, 1.5f, 0f);
            Enemy("Cowboy", 8, 6.0f, 13.4f, 1.5f, 0f);
            Enemy("Bartender", 9, 0.9f, 7.8f, 1.4f, 0f);
            Enemy("Cowboy", 10, -10.1f, 2.6f, 0f, 1.4f);
            Enemy("Cowboy", 11, -8.7f, -0.4f, -1.2f, -0.7f);
            Enemy("Cowboy", 12, -6.1f, -2.2f, -1.2f, -0.8f);
            Enemy("Cowboy", 13, -3.8f, -6.2f, 1.5f, 0f);
            Enemy("Cowboy", 14, 8.3f, 1.7f, 1.4f, 0f);
            Enemy("Cowboy", 15, 6.1f, -2.6f, 0f, -1.3f);
            Enemy("Chef", 16, 15.1f, 13.8f, 0f, -1.3f);
            Enemy("Chef", 17, 20.0f, 14.0f, 0f, -1.4f);
            Enemy("Chef", 18, 14.8f, 8.1f, 0f, -1.3f);
            Enemy("Chef", 19, 20.5f, 8.1f, 0f, -1.3f);
            Enemy("Chef", 20, 15.1f, 2.4f, 0f, 1.3f);
            Enemy("Chef", 21, 20.3f, 2.4f, 0f, 1.3f);
            Enemy("Cowboy", 22, 19.4f, -2.2f, 0f, 0.9f);
            Enemy("Cowboy", 23, 19.4f, -6.6f, 0f, 0.9f);
            Enemy("Cowboy", 24, -2.9f, -13.1f, 0f, -1.4f);
            Enemy("Cowboy", 25, 2.2f, -13.1f, 0f, -1.4f);

            player.transform.position = new Vector3(0f, -20f, 0f);
            PlaceStarterWeapons(scene);
            Decorate(scene);
            Camera camera = FindComponent<Camera>(scene);
            if (camera != null) camera.transform.position = player.transform.position + Vector3.back * 10f;
            new GameObject(Marker);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save SaloonScene.");
            AssetDatabase.SaveAssets();
            Debug.Log("SALOON_REFERENCE_OK: floor plan, 11 doors, 3 tables, 5 counter pieces and 25 patrol enemies.");
        }

        [MenuItem("Already Dead/Add decoration to SaloonScene")]
        public static void DecorateCurrentSaloon()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            if (FindRoot(scene, Marker) == null)
                throw new InvalidOperationException("Apply the reference layout before adding decoration.");
            string backup = TempPath("SaloonScene.before-decoration.unity");
            if (!File.Exists(backup))
                File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath)), backup);
            Decorate(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save decorated SaloonScene.");
            Debug.Log("SALOON_DECOR_OK: existing saloon props placed around patrol routes and doorways.");
        }

        private static void Decorate(Scene scene)
        {
            Transform furniture = FindRoot(scene, "FURNITURE / move prefab instances freely");
            Transform enemies = FindRoot(scene, "ENEMIES / 25 reference markers / PatrolEnemy");
            TopDownPlayer player = FindComponent<TopDownPlayer>(scene);
            if (furniture == null || enemies == null || player == null)
                throw new InvalidOperationException("The reference Saloon furniture, enemies or player is missing.");
            // The toilets occupy the east end of their rooms. Keep these two guards
            // walking along the clear west side instead of into the new colliders.
            SetRoute(enemies, player, "Cowboy / sketch marker 22",
                new Vector2(19.4f, -2.2f), new Vector2(19.4f, -1.3f));
            SetRoute(enemies, player, "Cowboy / sketch marker 23",
                new Vector2(19.4f, -6.6f), new Vector2(19.4f, -5.7f));
            if (FindChild(furniture, DecorName) != null) return;

            Transform decor = new GameObject(DecorName).transform;
            decor.SetParent(furniture, false);
            decor.gameObject.layer = LayerMask.NameToLayer("Furniture");

            // West storage rooms: leave their central paths and doors open.
            Prop("Barrel", decor, -21f, 5.2f);
            Prop("Crate", decor, -16.5f, 5.3f);
            Prop("Crate", decor, -15.2f, 6.5f);
            Prop("Crate", decor, -21f, -7.3f);
            Prop("Crate", decor, -19.8f, -7.3f);
            Prop("Barrel", decor, -21f, -5.9f);

            // Sparse cargo in the long north room.
            Prop("Crate", decor, -11.2f, 11f);
            Prop("Crate", decor, 10.5f, 11f);
            Prop("Barrel", decor, 11f, 15f);

            // Shelves, seats and barrels around the main hall tables.
            Prop("BottleShelf", decor, -9f, 7.7f);
            Prop("BottleShelf", decor, 5.5f, 7.7f);
            Prop("Chair", decor, -5.7f, 2f);
            Prop("Chair", decor, -7.8f, 4.1f);
            Prop("Chair", decor, -2.4f, -4f);
            Prop("Chair", decor, 10f, -0.6f);
            Prop("Chair", decor, 7.5f, -3f);
            Prop("Barrel", decor, -10.7f, -7.2f);
            Prop("Barrel", decor, 10.6f, -7.2f);

            // Kitchen, side rooms and porch.
            Prop("Stove", decor, 20.3f, 15.5f);
            Prop("Crate", decor, 20.5f, 10.6f);
            Prop("Crate", decor, 20.4f, 5f);
            Prop("Barrel", decor, 14.7f, 5f);
            Prop("Toilet", decor, 20.5f, -2.6f);
            Prop("Toilet", decor, 20.5f, -6.7f);
            Prop("Crate", decor, -4.4f, -16.2f);
            Prop("Crate", decor, 3.5f, -16.2f);
        }

        private static void SetRoute(Transform enemies, TopDownPlayer player, string name,
            Vector2 start, Vector2 end)
        {
            Transform enemy = FindChild(enemies, name);
            if (enemy == null) throw new InvalidOperationException("Saloon patrol missing: " + name);
            SaloonEnemySpawn spawn = enemy.GetComponent<SaloonEnemySpawn>();
            spawn.SetSceneRoute(player, new[] { start, end });
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawn);
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.GetComponent<PatrolEnemy>());
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child;
            return null;
        }

        private static GameObject Prefab(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/" + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Saloon prefab missing: " + name);
            return prefab;
        }

        private static void Prop(string name, Transform parent, float x, float y, float angle = 0f)
        {
            GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(name), parent.gameObject.scene);
            prop.transform.SetParent(parent, false);
            prop.transform.position = new Vector3(x, y, 0f);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            prop.layer = LayerMask.NameToLayer("Furniture");
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static void PlaceStarterWeapons(Scene scene)
        {
            // The old weapon positions were inside the porch patrol routes.
            // Keep all three existing pickups available beside the player spawn.
            MoveRoot(scene, "Glock / 17 rounds", -2f, -20.5f);
            MoveRoot(scene, "M4 / 25 rounds automatic", 2f, -20.5f);
            MoveRoot(scene, "Revolver / 6 rounds + 4s reload", 0f, -22f);
        }

        private static void MoveRoot(Scene scene, string name, float x, float y)
        {
            Transform root = FindRoot(scene, name);
            if (root == null) throw new InvalidOperationException("Existing Saloon weapon missing: " + name);
            root.position = new Vector3(x, y, root.position.z);
        }

        private static Tilemap FindMap(Transform grid, string prefix)
        {
            foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>())
                if (map.name.StartsWith(prefix, StringComparison.Ordinal)) return map;
            throw new InvalidOperationException("Saloon tilemap missing: " + prefix);
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<T>() is T component) return component;
            return null;
        }

        private static string TempPath(string file) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp", file));
    }
}
