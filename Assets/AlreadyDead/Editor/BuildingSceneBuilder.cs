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
    // Creates an editable level kit. Rebuilding the scene is an explicit action because
    // the user is expected to rearrange its contents by hand.
    public static class BuildingSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/BuildingParkingScene.unity";
        private const string Root = "Assets/AlreadyDead/BuildingKit";
        private const string TilesFolder = Root + "/Tiles";
        private const string PrefabsFolder = Root + "/Prefabs";
        private const string PaletteFolder = Root + "/Palette";
        private const string PalettePath = PaletteFolder + "/Building and Parking.prefab";
        private const string LitMaterialPath = Root + "/Pixel Lit.mat";

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Tile> Tiles = new Dictionary<string, Tile>();
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static Material litMaterial;

        [MenuItem("Already Dead/Create building and parking kit")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Building and parking scene",
                    "Rebuild the scene? Any manual placement in BuildingParkingScene will be replaced. " +
                    "The palette assets and prefabs will be kept.", "Rebuild", "Cancel")) return;
            BuildScene();
        }

        [InitializeOnLoadMethod]
        private static void CheckPendingArtRefresh()
        {
            EditorApplication.delayCall += () =>
            {
                string marker = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Temp/RefreshBuildingKit.request"));
                if (!File.Exists(marker)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckPendingArtRefresh;
                    return;
                }
                try
                {
                    RefreshArtAndLighting();
                    File.Delete(marker);
                }
                catch (Exception exception) { Debug.LogException(exception); }
            };
        }

        [MenuItem("Already Dead/Refresh top-down building palette and lighting")]
        public static void RefreshArtAndLighting()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the edited building scene before updating art.");

            Directory.CreateDirectory(TilesFolder);
            Directory.CreateDirectory(PrefabsFolder);
            Directory.CreateDirectory(PaletteFolder);
            litMaterial = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (litMaterial == null) throw new InvalidOperationException("Pixel Lit material is missing.");
            Sprites.Clear();
            Tiles.Clear();
            Prefabs.Clear();
            foreach (string name in BuildingPixelArt.Names)
            {
                Sprite sprite = BuildingPixelArt.Ensure(name, true);
                Sprites.Add(name, sprite);
                Tiles.Add(name, EnsureTile(name, sprite));
                Prefabs.Add(name, EnsurePrefab(name, sprite, true));
            }
            EnsurePalette();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<TopDownPlayer>() == null &&
                    root.GetComponent<PistolWeapon>() == null &&
                    root.GetComponent<MusketWeapon>() == null &&
                    root.GetComponent<SpearWeapon>() == null &&
                    root.GetComponent<RockWeapon>() == null &&
                    root.GetComponent<ClubWeapon>() == null &&
                    root.GetComponent<MagicStaff>() == null) continue;
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    renderer.sharedMaterial = litMaterial;
                    EditorUtility.SetDirty(renderer);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the building scene with lit gameplay sprites.");
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_BUILDING_PALETTE_AND_LIGHTING_READY: " + ScenePath);
        }

        public static void BuildScene()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(TilesFolder);
            Directory.CreateDirectory(PrefabsFolder);
            Directory.CreateDirectory(PaletteFolder);
            AssetDatabase.Refresh();

            litMaterial = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (litMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null) throw new InvalidOperationException("URP 2D Sprite-Lit shader is missing.");
                litMaterial = new Material(shader);
                AssetDatabase.CreateAsset(litMaterial, LitMaterialPath);
            }

            Sprites.Clear();
            Tiles.Clear();
            Prefabs.Clear();
            foreach (string name in BuildingPixelArt.Names)
            {
                Sprite sprite = BuildingPixelArt.Ensure(name);
                if (sprite == null) throw new InvalidOperationException("Could not import sprite: " + name);
                Sprites.Add(name, sprite);
                Tiles.Add(name, EnsureTile(name, sprite));
                Prefabs.Add(name, EnsurePrefab(name, sprite));
            }
            EnsurePalette();
            CreateScene();
            EnsureBuildSetting();
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_BUILDING_KIT_READY: " + ScenePath);
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

        private static GameObject EnsurePrefab(string name, Sprite sprite, bool refresh = false)
        {
            string path = PrefabsFolder + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !refresh) return existing;
            GameObject root = existing == null ? new GameObject(name) : PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigurePrefab(root, name, sprite, existing == null);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (existing == null) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePrefab(GameObject root, string name, Sprite sprite, bool isNew)
        {
            if (IsSolid(name)) root.layer = IsFurniture(name) ? 12 : 8;
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = litMaterial;
            if (isNew) renderer.sortingOrder = name == "ParkingLine" ? 1 : IsGround(name) ? 0 : 6;
            if (IsSolid(name))
            {
                BoxCollider2D hitbox = root.GetComponent<BoxCollider2D>();
                if (hitbox == null) hitbox = root.AddComponent<BoxCollider2D>();
                hitbox.size = sprite.bounds.size * 0.82f;
            }
            else if (root.GetComponent<BoxCollider2D>() is BoxCollider2D oldHitbox)
                Object.DestroyImmediate(oldHitbox);
            if (name == "Streetlamp" || name == "CeilingLamp")
            {
                Light2D light = root.GetComponentInChildren<Light2D>(true);
                if (light == null)
                {
                    var lightObject = new GameObject("Warm light / URP 2D");
                    lightObject.transform.SetParent(root.transform, false);
                    light = lightObject.AddComponent<Light2D>();
                }
                light.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                light.lightType = Light2D.LightType.Point;
                light.color = name == "Streetlamp"
                    ? new Color(1f, 0.76f, 0.39f) : new Color(1f, 0.85f, 0.58f);
                light.intensity = name == "Streetlamp" ? 2.2f : 1.6f;
                light.pointLightInnerRadius = 0.55f;
                light.pointLightOuterRadius = name == "Streetlamp" ? 5.3f : 4.1f;
                light.falloffIntensity = 0.55f;
            }
        }

        private static bool IsGround(string name) => name == "Parquet" || name == "Asphalt" ||
            name == "Curb" || name == "ParkingLine";

        private static bool IsSolid(string name) => name != "Parquet" && name != "Asphalt" &&
            name != "ParkingLine" && name != "Doorway" && name != "CeilingLamp" &&
            name != "ScatteredPapers";

        private static bool IsFurniture(string name) =>
            name != "BeigeWall" && name != "Curb" && name != "CarRed" &&
            name != "CarBlue" && name != "Streetlamp";

        private static void EnsurePalette()
        {
            GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            if (palette == null)
                palette = GridPaletteUtility.CreateNewPalette(PaletteFolder, "Building and Parking",
                    GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Manual,
                    Vector3.one, GridLayout.CellSwizzle.XYZ);
            string path = AssetDatabase.GetAssetPath(palette);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Tilemap map = contents.GetComponentInChildren<Tilemap>();
                if (map == null) throw new InvalidOperationException("Tile Palette has no Tilemap.");
                // Keep tiles added to the palette by the level author.
                foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                {
                    TileBase current = map.GetTile(cell);
                    if (current != null && AssetDatabase.GetAssetPath(current).StartsWith(
                            TilesFolder + "/", StringComparison.OrdinalIgnoreCase))
                        map.SetTile(cell, null);
                }
                for (int i = 0; i < BuildingPixelArt.Names.Length; i++)
                {
                    // Six empty cells keep even the widest sprites from touching.
                    Vector3Int cell = new Vector3Int((i % 4) * 6, -(i / 4) * 6, 0);
                    while (map.HasTile(cell)) cell.x += 6;
                    map.SetTile(cell, Tiles[BuildingPixelArt.Names[i]]);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera / framing preview");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 12.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.039f, 0.07f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.transform.position = new Vector3(0f, -1f, -10f);
            cameraObject.AddComponent<AudioListener>();

            var ambient = new GameObject("Night ambient / URP 2D");
            Light2D global = ambient.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.color = new Color(0.42f, 0.55f, 0.82f);
            global.intensity = 0.29f;

            var gridObject = new GameObject("EDITABLE TILEMAPS / select a layer and paint");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            Tilemap ground = CreateTilemap(grid.transform, "01 Ground / asphalt and parquet", 0);
            Tilemap markings = CreateTilemap(grid.transform, "02 Parking lines and curb", 1);
            Tilemap walls = CreateTilemap(grid.transform, "03 Beige walls / collisions", 4, true);

            for (int y = -13; y <= 11; y++)
                for (int x = -16; x <= 15; x++)
                    ground.SetTile(new Vector3Int(x, y, 0), Tiles["Asphalt"]);
            for (int y = 1; y <= 10; y++)
                for (int x = -10; x <= 9; x++)
                    ground.SetTile(new Vector3Int(x, y, 0), Tiles["Parquet"]);
            for (int x = -11; x <= 10; x++)
            {
                walls.SetTile(new Vector3Int(x, 11, 0), Tiles["BeigeWall"]);
                if (x != -1 && x != 0)
                    walls.SetTile(new Vector3Int(x, 0, 0), Tiles["BeigeWall"]);
            }
            for (int y = 1; y <= 10; y++)
            {
                walls.SetTile(new Vector3Int(-11, y, 0), Tiles["BeigeWall"]);
                walls.SetTile(new Vector3Int(10, y, 0), Tiles["BeigeWall"]);
            }
            markings.SetTile(new Vector3Int(-1, 0, 0), Tiles["Doorway"]);
            markings.SetTile(new Vector3Int(0, 0, 0), Tiles["Doorway"]);
            for (int x = -13; x <= 12; x++)
                markings.SetTile(new Vector3Int(x, -2, 0), Tiles["Curb"]);
            for (int x = -12; x <= 12; x += 4)
                for (int y = -11; y <= -5; y++)
                    markings.SetTile(new Vector3Int(x, y, 0), Tiles["ParkingLine"]);

            Transform props = new GameObject("MOVE OR DUPLICATE / placed prefab examples").transform;
            Place(props, "LeatherSofaBrown", -6.5f, 7.2f);
            Place(props, "LeatherSofaTan", -2.8f, 7.2f);
            Place(props, "CoffeeTable", -4.8f, 5.3f);
            Place(props, "Fridge", 7.6f, 8.4f);
            Place(props, "Cabinet", 6.2f, 5.3f);
            Place(props, "DiningTable", 3.3f, 7.4f);
            Place(props, "Stool", 2f, 5.7f);
            Place(props, "Stool", 4.3f, 5.7f);
            Place(props, "PottedPlant", -8.6f, 9.4f);
            Place(props, "TrashBin", 8f, 2.7f);
            Place(props, "CeilingLamp", -4.5f, 8.8f);
            Place(props, "CeilingLamp", 4.5f, 8.8f);
            Place(props, "CarRed", -9f, -8f);
            Place(props, "CarBlue", 3f, -8f);
            Place(props, "Streetlamp", -13f, -4f);
            Place(props, "Streetlamp", 0f, -4f);
            Place(props, "Streetlamp", 13f, -4f);

            // Off-camera showcase keeps all individual prop prefabs easy to inspect in Scene view.
            Transform showcase = new GameObject("PALETTE SHOWCASE / source prefabs are in BuildingKit/Prefabs").transform;
            string[] showcaseNames = { "LeatherSofaBrown", "LeatherSofaTan", "Fridge", "Stool",
                "CoffeeTable", "DiningTable", "Cabinet", "PottedPlant", "TrashBin",
                "CarRed", "CarBlue", "Streetlamp", "CeilingLamp" };
            for (int i = 0; i < showcaseNames.Length; i++)
                Place(showcase, showcaseNames[i], 24f + (i % 3) * 4f, 9f - (i / 3) * 4f);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save building scene.");
        }

        private static Tilemap CreateTilemap(Transform grid, string name, int order, bool collidable = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            Tilemap map = go.AddComponent<Tilemap>();
            TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
            renderer.sharedMaterial = litMaterial;
            renderer.sortingOrder = order;
            if (collidable)
            {
                go.layer = 8;
                go.AddComponent<TilemapCollider2D>();
            }
            return map;
        }

        private static void Place(Transform parent, string name, float x, float y)
        {
            GameObject placed = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[name]);
            placed.transform.SetParent(parent, false);
            placed.transform.localPosition = new Vector3(x, y, 0f);
        }

        private static void EnsureBuildSetting()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(candidate => candidate.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
