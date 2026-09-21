using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ArtPath = "Assets/AlreadyDead/Art";
        private const string SettingsPath = "Assets/AlreadyDead/PrototypeTuning.asset";
        private static Sprite square;
        private static Sprite circle;
        private static Sprite ring;
        private static Sprite[] sand;
        private static Sprite[] rocks;
        private static Sprite[] dryBushes;
        private static Sprite dryGrass;
        private static Sprite rockWall;
        private static Dictionary<Sprite, Material> pixelMaterials;
        private static Material material;
        private static PhysicsMaterial2D wallMaterial;
        private static PhysicsMaterial2D gunMaterial;
        private static PrototypeTuning tuning;

        [MenuItem("Already Dead/Rebuild prototype scene")]
        public static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EditorUtility.DisplayDialog("Rebuild prototype", "Recreate SampleScene from primitives? " +
                "Manual edits to this scene will be replaced. Tuning values are preserved.", "Rebuild", "Cancel")) return;
            BuildScene();
        }

        // Command-line entry point, also used to create the checked-in scene.
        public static void BuildScene()
        {
            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            ConfigureLayers();
            tuning = AssetDatabase.LoadAssetAtPath<PrototypeTuning>(SettingsPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<PrototypeTuning>();
                AssetDatabase.CreateAsset(tuning, SettingsPath);
            }
            // Persists defaults of newly added tuning fields when an older asset is upgraded.
            EditorUtility.SetDirty(tuning);
            square = CreateSprite("Square", 0);
            circle = CreateSprite("Circle", 1);
            ring = CreateSprite("Ring", 2);
            sand = new[] { DesertPixelArt.Sand(0), DesertPixelArt.Sand(1), DesertPixelArt.Sand(2) };
            rocks = new[] { DesertPixelArt.Rock(0), DesertPixelArt.Rock(1) };
            dryBushes = new[] { DesertPixelArt.Bush(0), DesertPixelArt.Bush(1) };
            dryGrass = DesertPixelArt.Grass();
            rockWall = DesertPixelArt.RockWall();
            material = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/Primitive.mat");
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new System.InvalidOperationException("URP 2D unlit shader is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ArtPath + "/Primitive.mat");
            }
            pixelMaterials = new Dictionary<Sprite, Material>();
            wallMaterial = GetPhysicsMaterial("Walls", 0f, 0f);
            gunMaterial = GetPhysicsMaterial("ThrownPistol", 0.35f, tuning.throwBounce);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform arena = new GameObject("ARENA / 28 x 18").transform;
            BuildArena(arena);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = tuning.cameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex(0x342b20);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            cameraObject.AddComponent<AudioListener>();
            AimCamera view = cameraObject.AddComponent<AimCamera>();

            TopDownPlayer player = BuildPlayer(view);
            view.Configure(tuning, player);
            camera.transform.position = player.transform.position + Vector3.back * 10f;
            BuildPistol(new Vector2(-6.5f, -3.8f));
            BuildSpear(new Vector2(-9.5f, -4f));
            var hud = new GameObject("HUD + crosshair").AddComponent<PrototypeHud>();
            hud.Configure(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_SCENE_READY: " + ScenePath);
        }

        public static void BuildWindowsPlayer()
        {
            string destination = Path.GetFullPath("Builds/AlreadyDead/AlreadyDead.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.InvalidOperationException("Prototype player build failed: " + report.summary.result);
            Debug.Log("ALREADY_DEAD_BUILD_READY: " + destination);
        }

        public static void BuildFreshPrototype()
        {
            BuildScene();
            BuildWindowsPlayer();
        }

        private static void ConfigureLayers()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            layers.GetArrayElementAtIndex(8).stringValue = "Walls";
            layers.GetArrayElementAtIndex(9).stringValue = "Weapons";
            layers.GetArrayElementAtIndex(10).stringValue = "Player";
            tags.ApplyModifiedPropertiesWithoutUndo();
            Physics2D.IgnoreLayerCollision(9, 10, true);
            Physics2D.IgnoreLayerCollision(9, 9, true);
            // Save the collision matrix into the project, not just this editor session.
            var physics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0]);
            physics.Update();
            EditorUtility.SetDirty(physics.targetObject);
        }

        private static void BuildArena(Transform parent)
        {
            Draw("Sand base", parent, Vector2.zero, new Vector2(28, 18), Hex(0xbe9c64), -20);
            Transform ground = new GameObject("Sand / 20px tiles").transform;
            ground.SetParent(parent, false);
            for (int x = 0; x < 28; x++)
                for (int y = 0; y < 18; y++)
                {
                    int variant = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
                    Draw("Sand tile", ground, new Vector2(x - 13.5f, y - 8.5f), Vector2.one,
                        Color.white, -19, sand[variant % sand.Length]);
                }

            BuildDesertDetails(parent);

            Wall(parent, "North wall", new Vector2(0, 9.3f), new Vector2(29.2f, 0.6f));
            Wall(parent, "South wall", new Vector2(0, -9.3f), new Vector2(29.2f, 0.6f));
            Wall(parent, "West wall", new Vector2(-14.3f, 0), new Vector2(0.6f, 18));
            Wall(parent, "East wall", new Vector2(14.3f, 0), new Vector2(0.6f, 18));
            Wall(parent, "Cover A / horizontal", new Vector2(-6, 2.4f), new Vector2(5.2f, 0.65f));
            Wall(parent, "Cover B / vertical", new Vector2(4, 3f), new Vector2(0.65f, 5f));
            Wall(parent, "Cover C / horizontal", new Vector2(7, -4), new Vector2(4.4f, 0.65f));
            Pillar(parent, new Vector2(-2.3f, -1.2f));
            Pillar(parent, new Vector2(0, 4.8f));
            Pillar(parent, new Vector2(9.4f, 2.6f));

            // Painted starting bay and weapon pad are flat, non-colliding primitives.
            for (int i = 0; i < 8; i++)
                Draw("Spawn bay stripe", parent, new Vector2(-10f + i * 0.7f, -6.1f),
                    new Vector2(0.34f, 0.07f), Hex(0x9b8159), -15);
            Draw("Pistol pad", parent, new Vector2(-6.5f, -3.8f), new Vector2(1.3f, 1.3f),
                new Color(1f, 0.76f, 0.34f, 0.3f), -12, ring);
            for (int i = 0; i < 3; i++)
            {
                Vector2 target = new Vector2(12.8f, -2.5f + i * 2.5f);
                Wall(parent, "Ballistic test block", target, new Vector2(0.4f, 1.1f));
                Draw("Target marking", parent, target, new Vector2(0.27f, 0.5f), Hex(0xdd8a62), 7, ring);
            }
        }

        private static void Pillar(Transform parent, Vector2 position)
        {
            Wall(parent, "Pillar", position, new Vector2(1.5f, 1.5f));
            Draw("Pillar inset", parent, position, new Vector2(0.85f, 0.85f), Hex(0x9d815c), 6);
            Draw("Pillar highlight", parent, position, new Vector2(0.15f, 0.15f), Hex(0xd5bd89), 7, circle);
        }

        private static void Wall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Draw(name + " shadow", parent, position + new Vector2(0.13f, -0.19f), size + Vector2.one * 0.13f,
                new Color(0.16f, 0.11f, 0.07f, 0.55f), -10);
            SpriteRenderer wall = Draw(name, parent, position, Vector2.one, Color.white, 5, rockWall);
            wall.drawMode = SpriteDrawMode.Tiled;
            wall.size = size;
            wall.gameObject.layer = 8;
            BoxCollider2D collider = wall.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = wallMaterial;
            Draw(name + " top edge", parent, position + Vector2.up * (size.y * 0.5f - 0.055f),
                new Vector2(size.x, 0.11f), Hex(0xb89a6e), 6);
        }

        private static void BuildDesertDetails(Transform parent)
        {
            Transform details = new GameObject("Desert details / stones and dry shrubs").transform;
            details.SetParent(parent, false);
            Vector2[] stonePositions =
            {
                new Vector2(-12.3f, 6.4f), new Vector2(-10.8f, 4.5f),
                new Vector2(-9.9f, 0.8f), new Vector2(-12f, -1.4f),
                new Vector2(-8.2f, -5.3f), new Vector2(-5.5f, 7.2f),
                new Vector2(-3.8f, 5.9f), new Vector2(-1.2f, 7.2f),
                new Vector2(2.4f, 7.3f), new Vector2(6.8f, 6.8f),
                new Vector2(11.4f, 7f), new Vector2(12.1f, 5f),
                new Vector2(10f, 0.1f), new Vector2(6.5f, 0.3f),
                new Vector2(1.2f, -2.2f), new Vector2(-3f, -4.6f),
                new Vector2(-11.5f, -7f), new Vector2(-4f, -7.3f),
                new Vector2(1f, -7f), new Vector2(9f, -6.7f),
                new Vector2(12f, -6f)
            };
            for (int i = 0; i < stonePositions.Length; i++)
                BuildRock(details, stonePositions[i], rocks[i % rocks.Length]);

            Vector2[] bushPositions =
            {
                new Vector2(-12f, 5.4f), new Vector2(-11f, 2.3f),
                new Vector2(-8.5f, 6.6f), new Vector2(-4.2f, 4f),
                new Vector2(-1.5f, 5.8f), new Vector2(2.2f, 6f),
                new Vector2(5.4f, 7f), new Vector2(11.2f, 3.8f),
                new Vector2(8.7f, 0.8f), new Vector2(5.4f, -1.3f),
                new Vector2(1.6f, -0.3f), new Vector2(-3.2f, -0.6f),
                new Vector2(-6.8f, -1.7f), new Vector2(-11.2f, -2.3f),
                new Vector2(-11.2f, -5.2f), new Vector2(-5.1f, -5.5f),
                new Vector2(0.1f, -5.8f), new Vector2(4f, -7.3f),
                new Vector2(11f, -4.5f)
            };
            for (int i = 0; i < bushPositions.Length; i++)
                Draw("Dry bush", details, bushPositions[i], Vector2.one, Color.white, 2,
                    dryBushes[i % dryBushes.Length]);

            for (int i = 0; i < 22; i++)
            {
                float x = -12f + ((i * 59) % 25);
                float y = -7.5f + ((i * 37) % 16);
                if (Vector2.Distance(new Vector2(x, y), new Vector2(-8f, -4f)) < 2f) continue;
                Draw("Dry grass", details, new Vector2(x, y), Vector2.one, Color.white, 0, dryGrass);
            }
        }

        private static void BuildRock(Transform parent, Vector2 position, Sprite sprite)
        {
            var go = new GameObject("Stone / RMB pickup + instant throw");
            go.layer = 9;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 0.55f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.31f;
            collider.sharedMaterial = gunMaterial;
            Transform visual = new GameObject("Rock pixel art").transform;
            visual.SetParent(go.transform, false);
            Draw("Rock", visual, Vector2.zero, Vector2.one, Color.white, 1, sprite);
            RockWeapon rock = go.AddComponent<RockWeapon>();
            rock.Configure(tuning, visual, square, material);
        }

        private static TopDownPlayer BuildPlayer(AimCamera camera)
        {
            var go = new GameObject("Player / WASD + mouse aim");
            go.layer = 10;
            go.transform.position = new Vector3(-8f, -4f, 0f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.38f;
            collider.sharedMaterial = wallMaterial;
            Transform facing = new GameObject("Facing / local +X is forward").transform;
            facing.SetParent(go.transform, false);
            Draw("Shadow", facing, new Vector2(0, -0.07f), new Vector2(0.94f, 0.94f),
                new Color(0.02f, 0.03f, 0.04f, 0.5f), 8, circle);
            Draw("Jacket", facing, Vector2.zero, new Vector2(0.7f, 0.82f), Hex(0x55c9b0), 10, circle);
            Draw("Left shoulder", facing, new Vector2(0.1f, 0.31f), new Vector2(0.38f, 0.3f), Hex(0x3c9386), 11, circle);
            Draw("Right shoulder", facing, new Vector2(0.12f, -0.3f), new Vector2(0.38f, 0.3f), Hex(0x3c9386), 11, circle);
            Draw("Head", facing, new Vector2(0.1f, 0), new Vector2(0.47f, 0.47f), Hex(0xe0cab1), 12, circle);
            Draw("Forward visor", facing, new Vector2(0.28f, 0), new Vector2(0.12f, 0.32f), Hex(0x1d323b), 13);
            Transform leftFist = Draw("Left fist / punch", facing, new Vector2(0.22f, 0.42f),
                new Vector2(0.24f, 0.24f), Hex(0xe0cab1), 14, circle).transform;
            Transform rightFist = Draw("Right fist / punch", facing, new Vector2(0.22f, -0.42f),
                new Vector2(0.24f, 0.24f), Hex(0xe0cab1), 14, circle).transform;
            Transform socket = new GameObject("Weapon socket").transform;
            socket.SetParent(facing, false);
            socket.localPosition = new Vector3(0.38f, -0.22f, 0f);
            UnarmedCombat unarmed = go.AddComponent<UnarmedCombat>();
            unarmed.Configure(tuning, leftFist, rightFist, camera, square, material);
            TopDownPlayer player = go.AddComponent<TopDownPlayer>();
            player.Configure(tuning, facing, socket, camera, unarmed);
            return player;
        }

        private static void BuildPistol(Vector2 position)
        {
            var go = new GameObject("Pistol / aim + RMB to pick up");
            go.layer = 9;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, 0, 25);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 0.75f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.68f, 0.34f);
            collider.sharedMaterial = gunMaterial;
            SpriteRenderer halo = Draw("Pickup highlight", go.transform, Vector2.zero,
                new Vector2(1.15f, 1.15f), Hex(0xffcf71), 9, ring);
            halo.enabled = false;
            Transform visual = new GameObject("Visual / recoil").transform;
            visual.SetParent(go.transform, false);
            Draw("Grip", visual, new Vector2(-0.19f, -0.12f), new Vector2(0.18f, 0.3f), Hex(0x202a31), 13);
            Draw("Slide", visual, Vector2.zero, new Vector2(0.68f, 0.2f), Hex(0xf4c16b), 14);
            Draw("Barrel", visual, new Vector2(0.26f, 0), new Vector2(0.15f, 0.15f), Hex(0xe4e9dc), 15);
            Draw("Rear sight", visual, new Vector2(-0.2f, 0), new Vector2(0.06f, 0.12f), Hex(0x3a4042), 15);
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(visual, false);
            muzzle.localPosition = new Vector3(0.4f, 0, 0);
            SpriteRenderer flash = Draw("Muzzle flash", muzzle, new Vector2(0.14f, 0),
                new Vector2(0.36f, 0.22f), Hex(0xffecad), 18, circle);
            flash.enabled = false;
            PistolWeapon pistol = go.AddComponent<PistolWeapon>();
            pistol.Configure(tuning, visual, muzzle, halo, flash, square, material);
        }

        private static void BuildSpear(Vector2 position)
        {
            var go = new GameObject("Spear / hold RMB to throw");
            go.layer = 9;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -20f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearDamping = 4f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.55f, 0.17f);
            collider.sharedMaterial = gunMaterial;
            SpriteRenderer halo = Draw("Pickup highlight", go.transform, Vector2.zero,
                new Vector2(1.9f, 0.75f), Hex(0xffcf71), 9, ring);
            halo.enabled = false;
            Transform visual = new GameObject("Visual / stab and charge").transform;
            visual.SetParent(go.transform, false);
            Draw("Shaft", visual, new Vector2(-0.08f, 0f), new Vector2(1.32f, 0.12f),
                Hex(0x8c674a), 13);
            Draw("Shaft highlight", visual, new Vector2(-0.08f, 0.035f), new Vector2(1.28f, 0.035f),
                Hex(0xc49a66), 14);
            Draw("Binding", visual, new Vector2(0.52f, 0f), new Vector2(0.15f, 0.19f),
                Hex(0x36464d), 15);
            Draw("Spearhead", visual, new Vector2(0.77f, 0f), new Vector2(0.4f, 0.23f),
                Hex(0xd6e0d8), 16);
            Draw("Spear tip", visual, new Vector2(0.99f, 0f), new Vector2(0.17f, 0.11f),
                Hex(0xf7e5b2), 17);
            SpearWeapon spear = go.AddComponent<SpearWeapon>();
            spear.Configure(tuning, visual, halo, square, material);
        }

        private static SpriteRenderer Draw(string name, Transform parent, Vector2 position, Vector2 size,
            Color color, int order, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : square;
            renderer.sharedMaterial = sprite == null || sprite == square || sprite == circle || sprite == ring
                ? material : PixelMaterial(sprite);
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Material PixelMaterial(Sprite sprite)
        {
            if (pixelMaterials.TryGetValue(sprite, out Material result)) return result;
            string path = ArtPath + "/Desert/" + sprite.name + ".mat";
            result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(material) { name = sprite.name + " Pixel" };
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetTexture("_MainTex", sprite.texture);
            EditorUtility.SetDirty(result);
            pixelMaterials.Add(sprite, result);
            return result;
        }

        private static Sprite CreateSprite(string name, int shape)
        {
            string path = ArtPath + "/" + name + ".png";
            if (!File.Exists(path))
            {
                const int resolution = 64;
                var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                var pixels = new Color[resolution * resolution];
                for (int y = 0; y < resolution; y++)
                    for (int x = 0; x < resolution; x++)
                    {
                        float distance = new Vector2(x - 31.5f, y - 31.5f).magnitude;
                        float alpha = shape == 0 ? 1f : Mathf.Clamp01(31.5f - distance);
                        if (shape == 2) alpha *= Mathf.Clamp01(distance - 27.5f);
                        pixels[y * resolution + x] = new Color(1, 1, 1, alpha);
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static PhysicsMaterial2D GetPhysicsMaterial(string name, float friction, float bounce)
        {
            string path = ArtPath + "/" + name + ".physicsMaterial2D";
            PhysicsMaterial2D result = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (result == null)
            {
                result = new PhysicsMaterial2D(name);
                AssetDatabase.CreateAsset(result, path);
            }
            result.friction = friction;
            result.bounciness = bounce;
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Color Hex(uint rgb) => new Color(((rgb >> 16) & 255) / 255f,
            ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
