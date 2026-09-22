using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    // Adds a second level without rebuilding or replacing the desert arena.
    public static class FantasySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/FantasyScene.unity";
        private const string ArtPath = "Assets/AlreadyDead/Art/Fantasy";
        private static Material primitiveMaterial;
        private static Sprite square;
        private static Sprite circle;
        private static Sprite ring;
        private static PrototypeTuning tuning;
        private static TopDownPlayer player;
        private static readonly Dictionary<Sprite, Material> pixelMaterials = new Dictionary<Sprite, Material>();

        [MenuItem("Already Dead/Build fantasy level")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Build fantasy level",
                    "Recreate FantasyScene from the current SampleScene? Manual edits in FantasyScene will be replaced. " +
                    "SampleScene keeps its existing objects; only the forest gate is added if missing.",
                    "Build", "Cancel")) return;
            BuildFantasyScene();
        }

        // Called by the prototype builder so a later desert rebuild keeps level access.
        public static void EnsureDesertGate(Scene scene)
        {
            primitiveMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/AlreadyDead/Art/Primitive.mat");
            square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Square.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Circle.png");
            ring = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Ring.png");
            EnsureGate(scene, new Vector2(11.7f, 7.3f), "FantasyScene",
                new Color(0.27f, 0.94f, 0.68f), "FOREST GATE / enter to reach the fantasy level");
        }

        // Batch-mode entry point. Never calls PrototypeSceneBuilder.BuildScene.
        public static void BuildFantasyScene()
        {
            if (!File.Exists(PrototypeSceneBuilder.ScenePath))
                throw new System.InvalidOperationException("Build the existing SampleScene first.");

            tuning = AssetDatabase.LoadAssetAtPath<PrototypeTuning>("Assets/AlreadyDead/PrototypeTuning.asset");
            primitiveMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/AlreadyDead/Art/Primitive.mat");
            square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Square.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Circle.png");
            ring = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Ring.png");
            if (tuning == null || primitiveMaterial == null || square == null || circle == null || ring == null)
                throw new System.InvalidOperationException("Prototype tuning or primitive art is missing.");
            pixelMaterials.Clear();

            // Generate and import art before saving either scene.
            FantasyPixelArt.Grass(0);
            FantasyPixelArt.Path(0);
            FantasyPixelArt.Tree(0);
            FantasyPixelArt.Flowers(0);
            FantasyPixelArt.Stone();
            FantasyPixelArt.Fence();
            FantasyPixelArt.Mage();
            FantasyPixelArt.Knight();
            for (int i = 0; i < 3; i++) FantasyPixelArt.Staff(i);

            Scene source = EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            EnsureDesertGate(source);
            EditorSceneManager.SaveScene(source);
            // Copy through the editor so object references and the new scene GUID remain valid.
            if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                throw new System.InvalidOperationException("Could not save FantasyScene copy.");

            Scene fantasy = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in fantasy.GetRootGameObjects())
            {
                if (root.GetComponent<TopDownPlayer>() != null || root.GetComponent<Camera>() != null ||
                    root.GetComponent<PrototypeHud>() != null) continue;
                Object.DestroyImmediate(root);
            }

            player = Object.FindFirstObjectByType<TopDownPlayer>();
            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (player == null || camera == null)
                throw new System.InvalidOperationException("SampleScene must contain the configured player and camera.");
            player.transform.position = new Vector3(0f, -8f, 0f);
            camera.backgroundColor = Hex(0x183825);
            camera.transform.position = player.transform.position + Vector3.back * 10f;
            DressPlayerAsMage();

            Transform forest = new GameObject("ENCHANTED FOREST / 32 x 24").transform;
            BuildForest(forest);
            BuildStaff(new Vector2(-0.8f, -7.1f), MagicElement.Fire);
            BuildStaff(new Vector2(1.15f, -6.1f), MagicElement.Frost);
            BuildStaff(new Vector2(-1.2f, -4.8f), MagicElement.Lightning);

            BuildEnemy("Knight / lower trail", FantasyEnemyKind.Knight, MagicElement.Fire,
                new Vector2(3f, -2.8f), new Vector2(1.7f, -2.1f));
            BuildEnemy("Frost mage / grove", FantasyEnemyKind.Mage, MagicElement.Frost,
                new Vector2(-3f, 0.5f), new Vector2(-2.1f, 1.5f));
            BuildEnemy("Knight / old road", FantasyEnemyKind.Knight, MagicElement.Fire,
                new Vector2(3.4f, 3.8f), new Vector2(2f, 4.5f));
            BuildEnemy("Storm mage / upper grove", FantasyEnemyKind.Mage, MagicElement.Lightning,
                new Vector2(-3f, 6.8f), new Vector2(-1.9f, 7.6f));
            BuildEnemy("Knight / northern gate", FantasyEnemyKind.Knight, MagicElement.Fire,
                new Vector2(2.5f, 9.1f), new Vector2(0.9f, 9.1f));

            EnsureGate(fantasy, new Vector2(-1.6f, -9.4f), "SampleScene",
                new Color(0.95f, 0.68f, 0.32f), "DESERT GATE / return to the prototype arena");
            EditorSceneManager.SaveScene(fantasy);
            AddBuildScene(PrototypeSceneBuilder.ScenePath);
            AddBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_FANTASY_READY: " + ScenePath);
        }

        private static void AddBuildScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene entry in scenes)
                if (entry.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void DressPlayerAsMage()
        {
            Transform body = null;
            foreach (Transform child in player.Facing)
                if (child.name == "Caveman body / sprite forward is up") { body = child; break; }
            if (body == null) throw new System.InvalidOperationException("Player visual hierarchy changed.");
            SpriteRenderer robe = body.GetComponent<SpriteRenderer>();
            robe.sprite = FantasyPixelArt.Mage();
            robe.sharedMaterial = PixelMaterial(robe.sprite);
            robe.color = Color.white;
            body.localScale = Vector3.one * 0.96f;
            foreach (SpriteRenderer limb in player.Facing.GetComponentsInChildren<SpriteRenderer>())
            {
                if (limb.name.Contains("arm")) limb.color = Hex(0x534075);
                if (limb.name.Contains("leg")) limb.color = Hex(0x28233f);
            }
        }

        private static void BuildForest(Transform parent)
        {
            Transform ground = new GameObject("Grass and winding cobbled path / 20px tiles").transform;
            ground.SetParent(parent, false);
            for (int y = -12; y < 12; y++)
                for (int x = -16; x < 16; x++)
                {
                    int hash = (x * 73856093) ^ (y * 19349663);
                    float center = PathCenter(y + 0.5f);
                    bool isPath = Mathf.Abs(x + 0.5f - center) < 1.75f;
                    Sprite tile = isPath ? FantasyPixelArt.Path((hash & 1)) :
                        FantasyPixelArt.Grass((hash & 0x7fffffff) % 3);
                    Draw(isPath ? "Cobbled path" : "Forest grass", ground,
                        new Vector2(x + 0.5f, y + 0.5f), Vector2.one, Color.white,
                        isPath ? -19 : -20, tile);
                }

            Transform details = new GameObject("Trees, wildflowers, stones and palisade").transform;
            details.SetParent(parent, false);
            var random = new System.Random(1147);
            var treePositions = new List<Vector2>();
            for (int i = 0; i < 160; i++)
            {
                Vector2 position = new Vector2(-14.7f + (float)random.NextDouble() * 29.4f,
                    -10.7f + (float)random.NextDouble() * 20.9f);
                if (Mathf.Abs(position.x - PathCenter(position.y)) < 4.4f) continue;
                bool crowded = false;
                foreach (Vector2 existing in treePositions)
                    if (Vector2.Distance(existing, position) < 2.05f) { crowded = true; break; }
                if (crowded) continue;
                treePositions.Add(position);
                float size = 0.92f + (float)random.NextDouble() * 0.78f;
                BuildTree(details, position, FantasyPixelArt.Tree(random.Next(0, 3)), size);
            }
            // Flower patches spill toward the trail, like the clearing in the visual reference.
            for (int patch = 0; patch < 14; patch++)
            {
                float centerY = -10f + (float)random.NextDouble() * 20f;
                float centerX = (random.Next(0, 2) == 0 ? -1f : 1f) *
                    (3.3f + (float)random.NextDouble() * 10f);
                int variant = random.Next(0, 3);
                for (int flower = 0; flower < 11; flower++)
                {
                    Vector2 position = new Vector2(centerX + ((float)random.NextDouble() - 0.5f) * 2.7f,
                        centerY + ((float)random.NextDouble() - 0.5f) * 2.4f);
                    if (Mathf.Abs(position.x - PathCenter(position.y)) < 1.65f) continue;
                    Draw("Wildflowers", details, position, Vector2.one * 0.9f,
                        Color.white, -12, FantasyPixelArt.Flowers(variant));
                }
            }
            for (int i = 0; i < 25; i++)
            {
                Vector2 position = new Vector2(-14f + (float)random.NextDouble() * 28f,
                    -10.4f + (float)random.NextDouble() * 20.5f);
                if (Mathf.Abs(position.x - PathCenter(position.y)) < 2.25f) continue;
                Draw("Mossy stone", details, position, Vector2.one * 0.8f,
                    Color.white, -12, FantasyPixelArt.Stone());
            }
            for (int x = -15; x <= 15; x++)
            {
                if (Mathf.Abs(x - PathCenter(10.5f)) < 2.3f) continue;
                Draw("North palisade", details, new Vector2(x, 10.7f), Vector2.one,
                    Color.white, 3, FantasyPixelArt.Fence());
            }
            Border(parent, "West forest boundary", new Vector2(-16.35f, 0), new Vector2(0.7f, 24.7f));
            Border(parent, "East forest boundary", new Vector2(16.35f, 0), new Vector2(0.7f, 24.7f));
            Border(parent, "South forest boundary", new Vector2(0, -12.35f), new Vector2(32.7f, 0.7f));
            Border(parent, "North forest boundary", new Vector2(0, 12.35f), new Vector2(32.7f, 0.7f));
        }

        private static float PathCenter(float y) => Mathf.Sin(y * 0.29f) * 1.05f + Mathf.Sin(y * 0.63f) * 0.35f;

        private static void BuildTree(Transform parent, Vector2 position, Sprite sprite, float scale)
        {
            var tree = new GameObject("Oak / solid trunk");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.layer = 8;
            CircleCollider2D trunk = tree.AddComponent<CircleCollider2D>();
            trunk.radius = 0.28f + 0.09f * scale;
            trunk.offset = new Vector2(0f, -0.52f * scale);
            Draw("Canopy and trunk", tree.transform, Vector2.zero, Vector2.one * scale,
                Color.white, 3, sprite);
        }

        private static void Border(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.layer = 8;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static void BuildStaff(Vector2 position, MagicElement element)
        {
            var staff = new GameObject(element + " staff / RMB to equip, LMB to cast");
            staff.layer = 9;
            staff.transform.position = position;
            var body = staff.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearDamping = 4f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D collider = staff.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.95f, 0.25f);
            Draw("Staff shadow", staff.transform, new Vector2(0.06f, -0.13f),
                new Vector2(1.1f, 0.34f), new Color(0f, 0.12f, 0.08f, 0.5f), 8, circle);
            SpriteRenderer halo = Draw("Pickup halo", staff.transform, Vector2.zero,
                new Vector2(1.28f, 0.58f), ElementColor(element), 9, ring);
            halo.enabled = false;
            Transform visual = new GameObject("Runed staff visual").transform;
            visual.SetParent(staff.transform, false);
            Draw("Staff", visual, Vector2.zero, new Vector2(1.25f, 0.8f),
                Color.white, 14, FantasyPixelArt.Staff((int)element));
            MagicStaff magic = staff.AddComponent<MagicStaff>();
            magic.Configure(tuning, element, visual, halo, circle, primitiveMaterial);
        }

        private static void BuildEnemy(string name, FantasyEnemyKind kind, MagicElement element,
            Vector2 first, Vector2 second)
        {
            var enemyObject = new GameObject(name);
            enemyObject.layer = 11;
            enemyObject.transform.position = first;
            var body = enemyObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = enemyObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.38f;
            Transform facing = new GameObject("Facing / vision +X").transform;
            facing.SetParent(enemyObject.transform, false);
            Draw("Shadow", facing, new Vector2(0f, -0.08f), new Vector2(0.9f, 0.9f),
                new Color(0f, 0.1f, 0.05f, 0.48f), 8, circle);
            Transform figure = Draw(kind == FantasyEnemyKind.Knight ? "Armored knight" : "Rival mage", facing,
                Vector2.zero, Vector2.one * 0.98f, Color.white, 11,
                kind == FantasyEnemyKind.Knight ? FantasyPixelArt.Knight() : FantasyPixelArt.Mage()).transform;
            figure.localRotation = Quaternion.Euler(0f, 0f, -90f);
            SpriteRenderer alert = Draw("Alert", facing, new Vector2(0f, 0.65f),
                new Vector2(0.18f, 0.28f), Hex(0xff645a), 20, square);
            alert.enabled = false;
            FantasyEnemy enemy = enemyObject.AddComponent<FantasyEnemy>();
            enemy.Configure(tuning, player, facing, alert, first, second, kind, element, circle, primitiveMaterial);
        }

        private static void EnsureGate(Scene scene, Vector2 position, string destination, Color color, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<FantasyLevelGate>()?.DestinationScene == destination) return;
            var gate = new GameObject(name);
            gate.transform.position = position;
            CircleCollider2D trigger = gate.AddComponent<CircleCollider2D>();
            trigger.radius = 0.62f;
            trigger.isTrigger = true;
            var levelGate = gate.AddComponent<FantasyLevelGate>();
            levelGate.Configure(destination);
            Draw("Gate shadow", gate.transform, Vector2.zero, new Vector2(1.65f, 1.65f),
                new Color(0f, 0.1f, 0.06f, 0.7f), -1, circle);
            Draw("Arcane circle", gate.transform, Vector2.zero, new Vector2(1.4f, 1.4f), color, 5, ring);
            Draw("Gate core", gate.transform, Vector2.zero, new Vector2(0.75f, 0.75f),
                new Color(color.r, color.g, color.b, 0.55f), 6, circle);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Draw("Rune", gate.transform, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.59f,
                    new Vector2(0.14f, 0.14f), Color.white, 7, square);
            }
        }

        private static SpriteRenderer Draw(string name, Transform parent, Vector2 position, Vector2 size,
            Color color, int order, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = sprite == square || sprite == circle || sprite == ring
                ? primitiveMaterial : PixelMaterial(sprite);
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Material PixelMaterial(Sprite sprite)
        {
            if (pixelMaterials.TryGetValue(sprite, out Material existing)) return existing;
            Directory.CreateDirectory(ArtPath);
            string path = ArtPath + "/" + sprite.name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(primitiveMaterial) { name = sprite.name + " Fantasy Pixel" };
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetTexture("_MainTex", sprite.texture);
            EditorUtility.SetDirty(result);
            pixelMaterials.Add(sprite, result);
            return result;
        }

        private static Color ElementColor(MagicElement element) => element == MagicElement.Fire
            ? Hex(0xff783c) : element == MagicElement.Frost ? Hex(0x72dafa) : Hex(0xffe979);

        private static Color Hex(uint rgb) => new Color(((rgb >> 16) & 255) / 255f,
            ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
