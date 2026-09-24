using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace AlreadyDead.Editor
{
    public static class SaloonSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/SaloonScene.unity";
        private const string Root = "Assets/AlreadyDead/SaloonKit";
        private const string TilesFolder = Root + "/Tiles";
        private const string PrefabsFolder = Root + "/Prefabs";
        private const string PaletteFolder = Root + "/Palette";
        private const string PalettePath = PaletteFolder + "/Saloon.prefab";
        private const string DoorPath = PrefabsFolder + "/SwingDoor.prefab";
        private const string TuningPath = "Assets/AlreadyDead/PrototypeTuning.asset";
        private const string SamplePath = "Assets/Scenes/SampleScene.unity";
        private const string FantasyPath = "Assets/Scenes/FantasyScene.unity";
        private const string BuildingPath = "Assets/Scenes/BuildingParkingScene.unity";
        private const string SandboxPath = "Assets/AlreadyDead/Tests/Scenes/WeaponSandbox.unity";

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Tile> Tiles = new Dictionary<string, Tile>();
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static Material litMaterial;
        private static PrototypeTuning tuning;

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string marker = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Temp/BuildSaloon.request"));
                if (!File.Exists(marker)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckRequest;
                    return;
                }
                try
                {
                    BuildAndInstall();
                    File.Delete(marker);
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../Temp/BuildSaloon.error.txt")), error.ToString());
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("Already Dead/Create Saloon scene and swing doors")]
        public static void BuildAndInstall()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("SaloonScene already exists; it will not be rebuilt over manual edits.");

            Scene original = SceneManager.GetActiveScene();
            bool hasSavedOriginal = original.IsValid() && original.isLoaded &&
                !string.IsNullOrEmpty(original.path);
            if (hasSavedOriginal && !EditorSceneManager.SaveScene(original))
                throw new IOException("Could not save the currently open scene.");

            tuning = AssetDatabase.LoadAssetAtPath<PrototypeTuning>(TuningPath);
            litMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/AlreadyDead/BuildingKit/Pixel Lit.mat");
            if (tuning == null || litMaterial == null)
                throw new InvalidOperationException("Shared tuning or Pixel Lit material is missing.");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(TilesFolder);
            Directory.CreateDirectory(PrefabsFolder);
            Directory.CreateDirectory(PaletteFolder);

            Sprites.Clear();
            Tiles.Clear();
            Prefabs.Clear();
            foreach (string name in SaloonPixelArt.Names)
            {
                Sprite sprite = SaloonPixelArt.Ensure(name);
                Sprites.Add(name, sprite);
                Tiles.Add(name, EnsureTile(name, sprite));
                if (name != "Sand" && name != "BoardFloor" && name != "DoorLeaf" &&
                    name != "Cowboy" && name != "Chef" && name != "Bartender")
                    Prefabs.Add(name, EnsurePropPrefab(name, sprite));
            }
            Prefabs.Add("SwingDoor", EnsureDoorPrefab());
            foreach (string role in new[] { "Cowboy", "Chef", "Bartender" })
                Prefabs.Add(role, EnsureEnemyPrefab(role));
            EnsurePalette();

            Scene saloon = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                hasSavedOriginal ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(saloon);
            BuildSceneContents(saloon);
            if (!EditorSceneManager.SaveScene(saloon, ScenePath))
                throw new IOException("Could not save SaloonScene.");

            InstallDoor(saloon, new Vector2(-1f, -6f), 0f);
            InstallDoorInScene(SamplePath, new Vector2(-13.45f, -2.9f), 90f);
            InstallDoorInScene(FantasyPath, new Vector2(-1.6f, -8.1f), 0f);
            InstallDoorInScene(BuildingPath, new Vector2(-1f, 0f), 0f);
            InstallDoorInScene(SandboxPath, new Vector2(1.5f, 0f), 0f);
            EditorSceneManager.SaveScene(saloon);

            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(entry => entry.path == ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            SceneManager.SetActiveScene(saloon);
            Debug.Log("SALOON_READY: 3 round tables, 13 enemies, spaced palette and doors in 5 gameplay scenes.");
        }

        private static Tile EnsureTile(string name, Sprite sprite)
        {
            string path = TilesFolder + "/" + name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = IsSolid(name) ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static bool IsSolid(string name) => name == "WoodWall" || name == "RoundTable" ||
            name == "BarCounter" || name == "PrepTable" || name == "Toilet" ||
            name == "Cactus" || name == "Crate" || name == "Barrel" ||
            name == "Stove" || name == "Chair" || name == "BottleShelf";

        private static GameObject EnsurePropPrefab(string name, Sprite sprite)
        {
            string path = PrefabsFolder + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(name);
            try
            {
                root.layer = name == "WoodWall" ? 8 : 12;
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = litMaterial;
                renderer.sortingOrder = 5;
                if (IsSolid(name))
                {
                    BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
                    collider.size = sprite.bounds.size * 0.82f;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureDoorPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPath);
            if (existing != null) return existing;
            var door = new GameObject("SwingDoor / push to open");
            try
            {
                door.layer = 8;
                Rigidbody2D body = door.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.mass = 0.45f;
                body.angularDamping = 1.4f;
                body.linearDamping = 5f;
                BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.86f, 0.25f);
                collider.offset = new Vector2(0.93f, 0f);
                HingeJoint2D hinge = door.AddComponent<HingeJoint2D>();
                hinge.autoConfigureConnectedAnchor = false;
                hinge.anchor = Vector2.zero;
                hinge.useLimits = true;
                hinge.limits = new JointAngleLimits2D { min = -105f, max = 105f };
                door.AddComponent<PushDoor2D>();
                var leaf = new GameObject("Top-down wood leaf");
                leaf.transform.SetParent(door.transform, false);
                leaf.transform.localPosition = new Vector3(1f, 0f, 0f);
                SpriteRenderer renderer = leaf.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprites["DoorLeaf"];
                renderer.sharedMaterial = litMaterial;
                renderer.sortingOrder = 8;
                return PrefabUtility.SaveAsPrefabAsset(door, DoorPath);
            }
            finally { Object.DestroyImmediate(door); }
        }

        private static GameObject EnsureEnemyPrefab(string role)
        {
            string path = PrefabsFolder + "/" + role + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(role + " / enemy");
            try
            {
                root.layer = 11;
                Rigidbody2D body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
                collider.radius = 0.38f;
                Transform facing = new GameObject("Facing / vision +X").transform;
                facing.SetParent(root.transform, false);
                Transform visual = new GameObject(role + " top-down pixel art").transform;
                visual.SetParent(facing, false);
                SpriteRenderer renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprites[role];
                renderer.sharedMaterial = litMaterial;
                renderer.sortingOrder = 11;
                PrototypeSceneBuilder.AddPixelOutline(visual, 10);
                root.AddComponent<PatrolEnemy>();
                root.AddComponent<SaloonEnemySpawn>().Configure(tuning, facing);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void EnsurePalette()
        {
            GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            if (palette == null)
                palette = GridPaletteUtility.CreateNewPalette(PaletteFolder, "Saloon",
                    GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Manual,
                    Vector3.one, GridLayout.CellSwizzle.XYZ);
            GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(palette));
            try
            {
                Tilemap map = contents.GetComponentInChildren<Tilemap>();
                if (map == null) throw new InvalidOperationException("Saloon palette has no Tilemap.");
                for (int i = 0; i < SaloonPixelArt.Names.Length; i++)
                {
                    Vector3Int cell = new Vector3Int((i % 4) * 6, -(i / 4) * 6, 0);
                    map.SetTile(cell, Tiles[SaloonPixelArt.Names[i]]);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(palette));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void BuildSceneContents(Scene scene)
        {
            var cameraObject = new GameObject("Main Camera / follow player");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = tuning.cameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.27f, 0.2f, 0.13f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            cameraObject.AddComponent<AudioListener>();
            AimCamera view = cameraObject.AddComponent<AimCamera>();

            var ambient = new GameObject("Warm dusk / URP 2D");
            Light2D global = ambient.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.color = new Color(1f, 0.78f, 0.55f);
            global.intensity = 0.65f;

            var gridObject = new GameObject("EDITABLE SALOON TILEMAPS / paint from Saloon palette");
            Grid grid = gridObject.AddComponent<Grid>();
            Tilemap sand = CreateTilemap(grid.transform, "01 Sand / outdoors", -20);
            Tilemap floor = CreateTilemap(grid.transform, "02 Board floor / interiors", -10);
            Tilemap walls = CreateTilemap(grid.transform, "03 Wood walls / bullet and vision collision", 4, 8);
            CreateTilemap(grid.transform, "04 Furniture / characters collide, bullets pass", 5, 12);
            CreateTilemap(grid.transform, "05 Decorations / no collision", 6);

            for (int x = -21; x <= 20; x++)
            for (int y = -16; y <= 15; y++)
                sand.SetTile(new Vector3Int(x, y, 0), Tiles["Sand"]);
            for (int x = -14; x <= 13; x++)
            for (int y = -5; y <= 10; y++)
                floor.SetTile(new Vector3Int(x, y, 0), Tiles["BoardFloor"]);
            for (int x = -3; x <= 2; x++)
            for (int y = -11; y <= -7; y++)
                floor.SetTile(new Vector3Int(x, y, 0), Tiles["BoardFloor"]);

            BuildWalls(walls);
            BuildFurniture(scene);
            AddPointLight("Lantern / bar", new Vector2(0f, 2.2f), 5.5f, 1.2f);
            AddPointLight("Lantern / west tables", new Vector2(-6f, -2f), 5f, 0.9f);
            AddPointLight("Lantern / kitchen", new Vector2(11f, 6f), 4f, 0.9f);

            TopDownPlayer player = CopyPlayer(scene, view);
            SceneManager.SetActiveScene(scene);
            camera.transform.position = player.transform.position + Vector3.back * 10f;
            PrototypeSceneBuilder.BuildPistolInOpenScene(new Vector2(-1.4f, -9.7f), false);
            PrototypeSceneBuilder.BuildPistolInOpenScene(new Vector2(1.4f, -9.7f), true);
            new GameObject("Ammo + throw charge").AddComponent<PrototypeHud>().Configure(player);
            BuildEnemies(player);
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int order, int layer = 0)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.layer = layer;
            Tilemap map = root.AddComponent<Tilemap>();
            TilemapRenderer renderer = root.AddComponent<TilemapRenderer>();
            renderer.sharedMaterial = litMaterial;
            renderer.sortingOrder = order;
            if (layer == 8 || layer == 12) root.AddComponent<TilemapCollider2D>();
            return map;
        }

        private static void BuildWalls(Tilemap walls)
        {
            void Put(int x, int y) => walls.SetTile(new Vector3Int(x, y, 0), Tiles["WoodWall"]);
            for (int x = -15; x <= 14; x++)
            {
                if (x != -13 && x != -12) Put(x, 11);  // North-west outside doorway.
                if (x != -1 && x != 0) Put(x, -6);    // Entrance from the porch.
            }
            for (int y = -5; y <= 10; y++) { Put(-15, y); Put(14, y); }
            for (int y = -5; y <= 10; y++)
            {
                if (y != -3 && y != -2 && y != 8 && y != 9) Put(-9, y);
                if (y != -4 && y != -3 && y != 8 && y != 9) Put(8, y);
            }
            for (int x = -14; x <= -10; x++)
                if (x != -13 && x != -12) Put(x, 2);
            for (int x = -8; x <= 7; x++) Put(x, 6);
            for (int x = 9; x <= 13; x++)
                if (x != 10 && x != 11) Put(x, 0);
            for (int y = -5; y <= -1; y++)
                if (y != -4 && y != -2) Put(11, y);
            for (int x = 12; x <= 13; x++) Put(x, -3);
            for (int y = -12; y <= -7; y++) { Put(-4, y); Put(3, y); }
            for (int x = -4; x <= 3; x++)
                if (x != -1 && x != 0) Put(x, -12);
        }

        private static void BuildFurniture(Scene scene)
        {
            Transform furniture = new GameObject("FURNITURE / move prefab instances freely").transform;
            Transform outside = new GameObject("OUTDOORS / cactus, brush and cargo").transform;
            foreach (Vector2 position in new[]
                     { new Vector2(-4.3f, 0.2f), new Vector2(-2.2f, -3.1f), new Vector2(4.4f, -0.6f) })
            {
                Place("RoundTable", furniture, position);
                foreach (Vector2 offset in new[]
                         { new Vector2(-1.3f, 0f), new Vector2(1.3f, 0f),
                           new Vector2(0f, -1.3f) })
                    Place("Chair", furniture, position + offset);
            }
            Place("BarCounter", furniture, new Vector2(-1.9f, 3.45f));
            Place("BarCounter", furniture, new Vector2(1.9f, 3.45f));
            Place("BottleShelf", furniture, new Vector2(-4.6f, 5.2f));
            Place("PrepTable", furniture, new Vector2(11f, 5.2f));
            Place("PrepTable", furniture, new Vector2(11f, 2.6f));
            Place("Stove", furniture, new Vector2(11f, 8.2f));
            Place("Toilet", furniture, new Vector2(12.5f, -1.7f));
            Place("Crate", furniture, new Vector2(12.7f, 9.5f));
            Place("Barrel", furniture, new Vector2(-13.6f, 9.4f));
            Place("Barrel", furniture, new Vector2(12.5f, 9.3f));
            Place("Crate", furniture, new Vector2(-12.3f, -4.2f));
            Place("Crate", furniture, new Vector2(-10.6f, -4.2f));
            foreach (Vector2 position in new[]
                     { new Vector2(-19f, 11f), new Vector2(18f, 8f),
                       new Vector2(-17f, -11f), new Vector2(15f, -12f) })
                Place("Cactus", outside, position);
            foreach (Vector2 position in new[]
                     { new Vector2(-18f, 4f), new Vector2(18f, 0f),
                       new Vector2(-9f, -12f), new Vector2(10f, -11f) })
                Place("DryBush", outside, position);
            Place("Barrel", outside, new Vector2(7f, -8.3f));
            Place("Crate", outside, new Vector2(-8f, -9f));
        }

        private static GameObject Place(string name, Transform parent, Vector2 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[name]);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            return instance;
        }

        private static void AddPointLight(string name, Vector2 position, float range, float intensity)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(position.x, position.y, -0.1f);
            Light2D light = root.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.7f, 0.36f);
            light.intensity = intensity;
            light.pointLightInnerRadius = 0.6f;
            light.pointLightOuterRadius = range;
            light.falloffIntensity = 0.55f;
        }

        private static TopDownPlayer CopyPlayer(Scene target, AimCamera view)
        {
            Scene sample = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Additive);
            GameObject playerObject;
            try
            {
                TopDownPlayer original = FindRootComponent<TopDownPlayer>(sample);
                if (original == null) throw new InvalidOperationException("Sample player is missing.");
                playerObject = Object.Instantiate(original.gameObject);
                playerObject.name = original.name;
                playerObject.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(playerObject, target);
            }
            finally { EditorSceneManager.CloseScene(sample, true); }

            playerObject.transform.position = new Vector3(0f, -8.9f, 0f);
            TopDownPlayer player = playerObject.GetComponent<TopDownPlayer>();
            UnarmedCombat unarmed = player.GetComponent<UnarmedCombat>();
            Transform socket = null;
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
                if (child.name == "Weapon socket") socket = child;
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Square.png");
            Material primitive = AssetDatabase.LoadAssetAtPath<Material>("Assets/AlreadyDead/Art/Primitive.mat");
            if (unarmed == null || socket == null || square == null || primitive == null)
                throw new InvalidOperationException("Sample player references are incomplete.");
            unarmed.Configure(tuning, unarmed.LeftArm, unarmed.RightArm, view, square, primitive);
            player.Configure(tuning, player.Facing, socket, view, unarmed);
            player.GetComponent<PlayerVitality>().Configure(tuning, view);
            view.Configure(tuning, player);
            return player;
        }

        private static T FindRootComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<T>() is T found) return found;
            return null;
        }

        private static void BuildEnemies(TopDownPlayer player)
        {
            Transform enemies = new GameObject("ENEMIES / 13 of 26 red markers").transform;
            void Spawn(string role, string label, Vector2 first, Vector2 second)
            {
                GameObject instance = Place(role, enemies, first);
                instance.name = label;
                instance.GetComponent<SaloonEnemySpawn>().SetSceneRoute(player, new[] { first, second });
            }
            Spawn("Cowboy", "Cowboy / north-west 1", new Vector2(-12.5f, 8.6f), new Vector2(-11f, 9.5f));
            Spawn("Cowboy", "Cowboy / north-west 2", new Vector2(-11.2f, 4.5f), new Vector2(-13f, 4.5f));
            Spawn("Cowboy", "Cowboy / upstairs west", new Vector2(-6f, 8.6f), new Vector2(-3.5f, 8.6f));
            Spawn("Cowboy", "Cowboy / upstairs east", new Vector2(4f, 8.6f), new Vector2(6f, 8.6f));
            Spawn("Chef", "Chef / kitchen stove", new Vector2(12.8f, 7.3f), new Vector2(12.8f, 6.4f));
            Spawn("Chef", "Chef / kitchen prep", new Vector2(12.2f, 3.6f), new Vector2(10.2f, 3.6f));
            Spawn("Bartender", "Bartender / behind bar", new Vector2(2f, 5f), new Vector2(3f, 5f));
            Spawn("Cowboy", "Cowboy / west table", new Vector2(-6.3f, 0.3f), new Vector2(-5f, -1f));
            Spawn("Cowboy", "Cowboy / south table", new Vector2(0.5f, -4.4f), new Vector2(1.4f, -3.3f));
            Spawn("Cowboy", "Cowboy / east table", new Vector2(5.7f, -1.7f), new Vector2(6.7f, -3f));
            Spawn("Cowboy", "Cowboy / left store room", new Vector2(-11.5f, -1.2f), new Vector2(-13f, -1.2f));
            Spawn("Chef", "Chef / pantry", new Vector2(12.5f, -4.4f), new Vector2(12.5f, -4.4f));
            Spawn("Cowboy", "Cowboy / porch", new Vector2(-10f, -10f), new Vector2(-8.7f, -10f));
        }

        private static void InstallDoorInScene(string path, Vector2 hinge, float angle)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save scene before placing a door: " + path);
            InstallDoor(scene, hinge, angle);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save scene with the swing door: " + path);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        private static void InstallDoor(Scene scene, Vector2 hinge, float angle)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<PushDoor2D>() != null) return;
            GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs["SwingDoor"], scene);
            door.name = "SwingDoor / push to open / blocks sight while closed";
            door.transform.position = hinge;
            door.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
