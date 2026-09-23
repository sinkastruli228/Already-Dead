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
        private const string WeaponSandboxPath = "Assets/AlreadyDead/Tests/Scenes/WeaponSandbox.unity";
        private const string ArtPath = "Assets/AlreadyDead/Art";
        private const string CharacterPath = "Assets/Character";
        private const string WeaponPath = "Assets/Waepon";
        private const string SettingsPath = "Assets/AlreadyDead/PrototypeTuning.asset";
        public const float CharacterVisualScale = 1.33f;
        private static Sprite square;
        private static Sprite circle;
        private static Sprite ring;
        private static Sprite[] sand;
        private static Sprite[] dryBushes;
        private static Sprite dryGrass;
        private static Sprite rockWall;
        private static Sprite cavemanIdle;
        private static Sprite cavemanArm;
        private static Sprite cavemanLeg;
        private static Sprite spearArt;
        private static Sprite spearGroundArt;
        private static Sprite musketSideArt;
        private static Sprite musketTopArt;
        private static Sprite m4SideArt;
        private static Sprite m4TopArt;
        private static Sprite glockSideArt;
        private static Sprite glockTopArt;
        private static Sprite clubArt;
        private static Sprite staffArt;
        private static Sprite thrownRockArt;
        private static Sprite landedRockArt;
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
            dryBushes = new[] { DesertPixelArt.Bush(0), DesertPixelArt.Bush(1) };
            dryGrass = DesertPixelArt.Grass();
            rockWall = DesertPixelArt.RockWall();
            cavemanIdle = LoadSprite(CharacterPath + "/CaveMan_Idle.png");
            cavemanArm = LoadSprite(CharacterPath + "/CaveMan_Arm.png");
            cavemanLeg = LoadSprite(CharacterPath + "/CaveMan_Leg.png");
            spearArt = LoadSprite(WeaponPath + "/Spear/Spear.png");
            spearGroundArt = LoadSprite(WeaponPath + "/Spear/Spear_Ground.png", "Spear_Ground_0");
            musketSideArt = LoadSprite(WeaponPath + "/Mushket/Mushket_Side.png");
            musketTopArt = LoadSprite(WeaponPath + "/Mushket/Mushket_Up.png");
            m4SideArt = LoadSprite(WeaponPath + "/M4/M4_Side.png", "M4_Side_0");
            m4TopArt = LoadSprite(WeaponPath + "/M4/M4_UP.png", "M4_UP_0");
            glockSideArt = LoadSprite(WeaponPath + "/Glock/Glock_Side.png", "Glock_Side_0");
            glockTopArt = LoadSprite(WeaponPath + "/Glock/Glock_UP.png", "Glock_UP_0");
            clubArt = LoadSprite(WeaponPath + "/Dubinka/Dubinka.png");
            staffArt = LoadSprite(WeaponPath + "/Stick/Stick.png");
            thrownRockArt = LoadSprite("Assets/Enviroment/Stone.png", "Stone_0");
            landedRockArt = LoadSprite("Assets/Enviroment/Stone_Ground.png", "Stone_Ground_0");
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
            Transform arena = new GameObject("DESERT MAZE / 28 x 24").transform;
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
            BuildMusket((Vector2)player.transform.position + Vector2.right * 1.2f);
            BuildPistol((Vector2)player.transform.position + Vector2.up * 1.25f, false);
            BuildPistol((Vector2)player.transform.position + new Vector2(1.65f, 1.25f), true);
            // The supplied map is 924 x 794 pixels. Each enemy starts at a red dot;
            // waypoints follow the drawn patrol lines. The closest guard has a spear.
            BuildEnemy("Patrol / first spear guard", player, true,
                Map(473, 703), Map(715, 703), Map(715, 595), Map(473, 595));
            BuildEnemy("Patrol / western flats", player, false,
                Map(160, 430), Map(160, 145));
            BuildEnemy("Patrol / western exit", player, false,
                Map(108, 475), Map(197, 475));
            BuildEnemy("Patrol / north west", player, false,
                Map(342, 107), Map(610, 107));
            BuildEnemy("Patrol / central south", player, false,
                Map(639, 439), Map(384, 439));
            BuildEnemy("Patrol / eastern passage", player, false,
                Map(865, 501), Map(758, 501), Map(758, 74), Map(869, 74), Map(869, 439));
            BuildEnemy("Guard / lower east", player, false, Map(779, 685));
            var hud = new GameObject("Ammo + throw charge").AddComponent<PrototypeHud>();
            hud.Configure(player);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FantasySceneBuilder.ScenePath) != null)
                FantasySceneBuilder.EnsureDesertGate(scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateWeaponSandboxAssets();
            // Keep additional levels in the build when the prototype is rebuilt.
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool hasPrototype = false;
            for (int i = 0; i < buildScenes.Count; i++)
            {
                if (buildScenes[i].path != ScenePath) continue;
                buildScenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                hasPrototype = true;
                break;
            }
            if (!hasPrototype) buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_SCENE_READY: " + ScenePath);
        }

        private static void UpdateWeaponSandboxAssets()
        {
            if (!File.Exists(WeaponSandboxPath)) return;
            Scene sandbox = EditorSceneManager.OpenScene(WeaponSandboxPath, OpenSceneMode.Additive);
            foreach (GameObject root in sandbox.GetRootGameObjects())
            {
                foreach (RockWeapon rock in root.GetComponentsInChildren<RockWeapon>(true))
                {
                    SpriteRenderer thrown = rock.Visual.GetComponentInChildren<SpriteRenderer>(true);
                    thrown.name = "New thrown rock asset";
                    thrown.sprite = thrownRockArt;
                    thrown.sharedMaterial = PixelMaterial(thrownRockArt);
                    thrown.transform.localPosition = Vector3.zero;
                    thrown.transform.localScale = new Vector3(0.28f, 0.28f, 1f);

                    SpriteRenderer grounded = rock.BuriedMark.GetComponentInChildren<SpriteRenderer>(true);
                    grounded.name = "New grounded rock asset";
                    grounded.sprite = landedRockArt;
                    grounded.sharedMaterial = PixelMaterial(landedRockArt);
                    grounded.color = Color.white;
                    grounded.sortingOrder = 3;
                    grounded.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                    grounded.transform.localScale = new Vector3(0.29f, 0.29f, 1f);

                    SpriteRenderer rockShadow = rock.Shadow.GetComponentInChildren<SpriteRenderer>(true);
                    rockShadow.sprite = circle;
                    rockShadow.sharedMaterial = material;
                    rockShadow.transform.localPosition = new Vector3(0f, -0.08f, 0f);
                    rockShadow.transform.localScale = new Vector3(0.72f, 0.34f, 1f);

                    if (rock.GetComponentInParent<EnemyWeaponLoadout>(true) == null)
                        rock.PlaceBuried();
                }
                foreach (SpearWeapon spear in root.GetComponentsInChildren<SpearWeapon>(true))
                {
                    Transform spearRoot = spear.Visual;
                    if (spearRoot == null) continue;
                    Transform airborne = spear.AirborneVisual;
                    if (airborne == null)
                    {
                        SpriteRenderer renderer = spearRoot.GetComponentInChildren<SpriteRenderer>(true);
                        if (renderer != null) airborne = renderer.transform;
                    }

                    Transform grounded = spear.GroundedVisual;
                    if (grounded == null)
                    {
                        grounded = Draw("Spear embedded in ground", spearRoot, Vector2.zero,
                            new Vector2(0.18f, 0.18f), Color.white, 16, spearGroundArt).transform;
                    }
                    else
                    {
                        SpriteRenderer groundedRenderer = grounded.GetComponent<SpriteRenderer>();
                        groundedRenderer.sprite = spearGroundArt;
                        groundedRenderer.sharedMaterial = PixelMaterial(spearGroundArt);
                        grounded.localPosition = Vector3.zero;
                        grounded.localScale = new Vector3(0.18f, 0.18f, 1f);
                    }
                    grounded.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    spear.ConfigureGroundViews(airborne, grounded);
                }
            }
            EditorSceneManager.SaveScene(sandbox);
            EditorSceneManager.CloseScene(sandbox, true);
        }

        public static void BuildWindowsPlayer()
        {
            string destination = Path.GetFullPath("Builds/AlreadyDead/AlreadyDead.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var enabledScenes = new List<string>();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                if (entry.enabled) enabledScenes.Add(entry.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = enabledScenes.ToArray(),
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

        public static void BuildAllScenes()
        {
            BuildScene();
            FantasySceneBuilder.BuildFantasyScene();
        }

        private static void ConfigureLayers()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            layers.GetArrayElementAtIndex(8).stringValue = "Walls";
            layers.GetArrayElementAtIndex(9).stringValue = "Weapons";
            layers.GetArrayElementAtIndex(10).stringValue = "Player";
            layers.GetArrayElementAtIndex(11).stringValue = "Enemies";
            tags.ApplyModifiedPropertiesWithoutUndo();
            Physics2D.IgnoreLayerCollision(9, 10, true);
            Physics2D.IgnoreLayerCollision(9, 9, true);
            Physics2D.IgnoreLayerCollision(11, 11, true);
            // Save the collision matrix into the project, not just this editor session.
            var physics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0]);
            physics.Update();
            EditorUtility.SetDirty(physics.targetObject);
        }

        private static void BuildArena(Transform parent)
        {
            Draw("Sand base", parent, Vector2.zero, new Vector2(28, 24), Hex(0xbe9c64), -20);
            Transform ground = new GameObject("Sand / 20px tiles").transform;
            ground.SetParent(parent, false);
            for (int x = 0; x < 28; x++)
                for (int y = 0; y < 24; y++)
                {
                    int variant = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
                    Draw("Sand tile", ground, new Vector2(x - 13.5f, y - 11.5f), Vector2.one,
                        Color.white, -19, sand[variant % sand.Length]);
                }

            BuildDesertDetails(parent);

            MapWall(parent, "North boundary", 40, 24, 929, 53);
            MapWall(parent, "South boundary", 32, 753, 929, 780);
            MapWall(parent, "West boundary / above exit", 40, 53, 68, 397);
            MapWall(parent, "West boundary / below exit", 32, 528, 61, 753);
            MapWall(parent, "East boundary", 901, 53, 929, 753);
            MapWall(parent, "Lower division / west", 32, 528, 725, 557);
            MapWall(parent, "Lower division / east", 859, 528, 929, 557);
            MapWall(parent, "Lower left post / upper", 362, 557, 389, 607);
            MapWall(parent, "Lower left post / lower", 362, 703, 389, 753);
            MapWall(parent, "Western inner wall", 244, 185, 271, 528);
            MapWall(parent, "Eastern inner wall / upper", 698, 53, 725, 382);
            MapWall(parent, "Eastern inner wall / lower", 698, 456, 725, 528);

            Vector2 mound = Map(489, 272);
            SpriteRenderer rock = Draw("Central round rock formation", parent, mound,
                new Vector2(6.85f, 6.85f), Hex(0x6e5940), 5, circle);
            rock.gameObject.layer = 8;
            CircleCollider2D rockCollider = rock.gameObject.AddComponent<CircleCollider2D>();
            rockCollider.radius = 0.5f;
            rockCollider.sharedMaterial = wallMaterial;
            Draw("Round rock sunlit face", parent, mound + Vector2.up * 0.11f,
                new Vector2(6.48f, 6.48f), Hex(0x9b8059), 6, circle);
        }

        private static Vector2 Map(float x, float y) => new Vector2((x - 481f) / 32f, (402f - y) / 32f);

        private static void MapWall(Transform parent, string name, float left, float top, float right, float bottom)
        {
            Wall(parent, name, Map((left + right) * 0.5f, (top + bottom) * 0.5f),
                new Vector2((right - left) / 32f, (bottom - top) / 32f));
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
                Map(236, 76), Map(400, 367), Map(571, 367),
                Map(778, 179), Map(832, 392), Map(111, 592),
                Map(286, 591), Map(106, 725), Map(595, 631)
            };
            for (int i = 0; i < stonePositions.Length; i++)
                BuildRock(details, stonePositions[i], true);

            Vector2[] bushPositions =
            {
                Map(88, 85), Map(290, 94), Map(448, 79), Map(571, 84),
                Map(96, 337), Map(195, 342), Map(302, 323), Map(658, 324),
                Map(807, 273), Map(756, 420), Map(103, 654), Map(292, 651),
                Map(396, 661), Map(679, 645), Map(734, 734)
            };
            for (int i = 0; i < bushPositions.Length; i++)
                Draw("Dry bush", details, bushPositions[i], Vector2.one, Color.white, 2,
                    dryBushes[i % dryBushes.Length]);

            for (int i = 0; i < 22; i++)
            {
                float x = -12f + ((i * 59) % 25);
                float y = -10.5f + ((i * 37) % 22);
                if (Vector2.Distance(new Vector2(x, y), Map(193, 663)) < 2f) continue;
                Draw("Dry grass", details, new Vector2(x, y), Vector2.one, Color.white, 0, dryGrass);
            }
        }

        private static RockWeapon BuildRock(Transform parent, Vector2 position, bool startsBuried = false)
        {
            var go = new GameObject("Stone / hold RMB to throw");
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
            Transform shadow = new GameObject("Rock ground shadow").transform;
            shadow.SetParent(go.transform, false);
            Draw("Pixel shadow", shadow, new Vector2(0f, -0.08f), new Vector2(0.72f, 0.34f),
                new Color(0.16f, 0.1f, 0.055f, 0.48f), 0, circle);
            Transform buried = new GameObject("Buried rock mark").transform;
            buried.SetParent(go.transform, false);
            Draw("New grounded rock asset", buried, new Vector2(0f, -0.02f), new Vector2(0.29f, 0.29f),
                Color.white, 3, landedRockArt);
            buried.gameObject.SetActive(false);
            Transform visual = new GameObject("Rock pixel art").transform;
            visual.SetParent(go.transform, false);
            Draw("New thrown rock asset", visual, Vector2.zero, new Vector2(0.28f, 0.28f),
                Color.white, 4, thrownRockArt);
            RockWeapon rock = go.AddComponent<RockWeapon>();
            rock.Configure(tuning, visual, shadow, buried, square, material);
            if (startsBuried) rock.PlaceBuried();
            return rock;
        }

        private static TopDownPlayer BuildPlayer(AimCamera camera)
        {
            var go = new GameObject("Player / WASD + mouse aim");
            go.layer = 10;
            go.transform.position = Map(193, 663);
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
            Draw("Shadow", facing, new Vector2(0, -0.07f) * CharacterVisualScale,
                new Vector2(0.94f, 0.94f) * CharacterVisualScale,
                new Color(0.02f, 0.03f, 0.04f, 0.5f), 5, circle);
            Transform leftLeg = Draw("Left leg / alternating step", facing,
                new Vector2(-0.12f, 0.12f) * CharacterVisualScale,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale, Color.white, 7, cavemanLeg).transform;
            Transform rightLeg = Draw("Right leg / alternating step", facing,
                new Vector2(-0.12f, -0.12f) * CharacterVisualScale,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale, Color.white, 7, cavemanLeg).transform;
            leftLeg.localRotation = Quaternion.Euler(0f, 0f, -90f);
            rightLeg.localRotation = Quaternion.Euler(0f, 0f, -90f);
            // Mirror only left/right. flipY would swap the brown forward edge to the rear
            // after the source sprite (+Y forward) is rotated onto gameplay +X.
            rightLeg.GetComponent<SpriteRenderer>().flipX = true;
            Transform cavemanBody = Draw("Caveman body / sprite forward is up", facing, Vector2.zero,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale, Color.white, 11, cavemanIdle).transform;
            cavemanBody.localRotation = Quaternion.Euler(0f, 0f, -90f);
            Transform leftFist = Draw("Left arm / punch", facing,
                new Vector2(0.04f, 0.22f) * CharacterVisualScale,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale, Color.white, 9, cavemanArm).transform;
            Transform rightFist = Draw("Right arm / punch + weapon grip", facing,
                new Vector2(0.04f, -0.22f) * CharacterVisualScale,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale, Color.white, 9, cavemanArm).transform;
            leftFist.localRotation = Quaternion.Euler(0f, 0f, -90f);
            rightFist.localRotation = Quaternion.Euler(0f, 0f, -90f);
            rightFist.GetComponent<SpriteRenderer>().flipX = true;
            AddPixelOutline(leftLeg, 6);
            AddPixelOutline(rightLeg, 6);
            AddPixelOutline(leftFist, 8);
            AddPixelOutline(rightFist, 8);
            AddPixelOutline(cavemanBody, 10);
            Transform socket = new GameObject("Weapon socket").transform;
            socket.SetParent(facing, false);
            socket.localPosition = new Vector3(0.38f, -0.22f, 0f) * CharacterVisualScale;
            UnarmedCombat unarmed = go.AddComponent<UnarmedCombat>();
            unarmed.Configure(tuning, leftFist, rightFist, camera, square, material);
            PlayerLimbAnimator limbs = go.AddComponent<PlayerLimbAnimator>();
            limbs.Configure(tuning, body, facing, leftLeg, rightLeg);
            PlayerVitality vitality = go.AddComponent<PlayerVitality>();
            vitality.Configure(tuning, camera);
            TopDownPlayer player = go.AddComponent<TopDownPlayer>();
            player.Configure(tuning, facing, socket, camera, unarmed);
            return player;
        }

        private static void BuildEnemy(string name, TopDownPlayer player, bool guaranteedSpear,
            params Vector2[] route)
        {
            Vector2 first = route[0];
            var go = new GameObject(name);
            go.layer = 11;
            go.transform.position = first;
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.37f;
            collider.sharedMaterial = wallMaterial;

            Transform facing = new GameObject("Facing / vision forward +X").transform;
            facing.SetParent(go.transform, false);
            Draw("Shadow", facing, new Vector2(0f, -0.07f) * CharacterVisualScale,
                new Vector2(0.88f, 0.88f) * CharacterVisualScale,
                new Color(0.08f, 0.04f, 0.03f, 0.45f), 5, circle);
            Transform bodyVisual = Draw("Raider body", facing, Vector2.zero,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale,
                Hex(0xc78064), 11, cavemanIdle).transform;
            bodyVisual.localRotation = Quaternion.Euler(0f, 0f, -90f);
            Transform leftArm = Draw("Left arm", facing,
                new Vector2(0.02f, 0.23f) * CharacterVisualScale,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale,
                Hex(0xc78064), 9, cavemanArm).transform;
            Transform rightArm = Draw("Right arm", facing,
                new Vector2(0.02f, -0.23f) * CharacterVisualScale,
                new Vector2(0.18f, 0.18f) * CharacterVisualScale,
                Hex(0xc78064), 9, cavemanArm).transform;
            leftArm.localRotation = Quaternion.Euler(0f, 0f, -90f);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, -90f);
            rightArm.GetComponent<SpriteRenderer>().flipX = true;
            AddPixelOutline(leftArm, 8);
            AddPixelOutline(rightArm, 8);
            AddPixelOutline(bodyVisual, 10);
            PatrolEnemy enemy = go.AddComponent<PatrolEnemy>();
            enemy.ConfigureRoute(tuning, player, facing, null, route);

            RockWeapon carriedRock = BuildRock(facing,
                new Vector2(0.48f, -0.19f) * CharacterVisualScale);
            SpearWeapon carriedSpear = BuildSpear(Vector2.zero);
            ClubWeapon carriedClub = BuildClub(Vector2.zero);
            carriedSpear.transform.SetParent(facing, false);
            carriedClub.transform.SetParent(facing, false);
            carriedSpear.transform.localPosition = new Vector3(0.53f, -0.2f, 0f) * CharacterVisualScale;
            carriedClub.transform.localPosition = new Vector3(0.5f, -0.2f, 0f) * CharacterVisualScale;
            carriedRock.name = "Carried rock / drops on defeat";
            carriedSpear.name = "Carried spear / drops on defeat";
            carriedClub.name = "Carried club / drops on defeat";
            carriedRock.gameObject.SetActive(false);
            carriedSpear.gameObject.SetActive(false);
            carriedClub.gameObject.SetActive(false);
            EnemyWeaponLoadout loadout = go.AddComponent<EnemyWeaponLoadout>();
            loadout.Configure(guaranteedSpear, carriedRock, carriedSpear, carriedClub);
        }

        private static void BuildPistol(Vector2 position, bool m4)
        {
            var go = new GameObject(m4 ? "M4 / 25 rounds automatic" : "Glock / 17 rounds");
            go.layer = 9;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, 0, 25);
            if (m4) go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = m4 ? 1.2f : 0.75f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = m4 ? new Vector2(1.55f, 0.42f) : new Vector2(0.85f, 0.5f);
            collider.sharedMaterial = gunMaterial;
            SpriteRenderer halo = Draw("Pickup highlight", go.transform, Vector2.zero,
                m4 ? new Vector2(1.8f, 0.65f) : new Vector2(1.1f, 0.7f),
                Hex(0xffcf71), 9, ring);
            halo.enabled = false;
            Transform visual = new GameObject("Visual / recoil").transform;
            visual.SetParent(go.transform, false);
            SpriteRenderer ground = Draw(m4 ? "M4 side / ground" : "Glock side / ground",
                visual, Vector2.zero, Vector2.one * (m4 ? 0.16f : 0.18f), Color.white,
                15, m4 ? m4SideArt : glockSideArt);
            SpriteRenderer held = Draw(m4 ? "M4 top / held" : "Glock top / held",
                visual, Vector2.zero, Vector2.one * (m4 ? 0.16f : 0.18f), Color.white,
                15, m4 ? m4TopArt : glockTopArt);
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(visual, false);
            muzzle.localPosition = new Vector3(m4 ? 0.8f : 0.43f, 0, 0);
            SpriteRenderer flash = Draw("Muzzle flash", muzzle, new Vector2(0.14f, 0),
                new Vector2(0.36f, 0.22f), Hex(0xffecad), 18, circle);
            flash.enabled = false;
            PistolWeapon pistol = go.AddComponent<PistolWeapon>();
            pistol.Configure(tuning, visual, muzzle, halo, flash, square, material);
            pistol.ConfigureFirearm(m4 ? 25 : 17, m4, ground, held);
        }

        private static SpearWeapon BuildSpear(Vector2 position)
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
            Transform shadow = new GameObject("Pixel spear shadow").transform;
            shadow.SetParent(go.transform, false);
            shadow.localPosition = new Vector3(0f, -0.075f, 0f);
            Transform shadowSprite = Draw("Spear shadow sprite", shadow, Vector2.zero,
                new Vector2(0.18f, 0.18f), new Color(0.11f, 0.075f, 0.045f, 0.32f), 10, spearArt).transform;
            shadowSprite.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Transform visual = new GameObject("Visual / stab and charge").transform;
            visual.SetParent(go.transform, false);
            Transform spearSprite = Draw("New spear asset", visual, Vector2.zero,
                new Vector2(0.18f, 0.18f), Color.white, 16, spearArt).transform;
            spearSprite.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Transform groundedSprite = Draw("Spear embedded in ground", visual, Vector2.zero,
                new Vector2(0.18f, 0.18f), Color.white, 16, spearGroundArt).transform;
            groundedSprite.localRotation = Quaternion.Euler(0f, 0f, -45f);
            groundedSprite.gameObject.SetActive(false);
            SpearWeapon spear = go.AddComponent<SpearWeapon>();
            spear.Configure(tuning, visual, shadow, halo, square, material);
            spear.ConfigureGroundViews(spearSprite, groundedSprite);
            return spear;
        }

        private static void BuildMusket(Vector2 position)
        {
            var go = new GameObject("Musket / side on ground + top in hands");
            go.layer = 9;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 1.2f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 0.28f);
            collider.sharedMaterial = gunMaterial;
            SpriteRenderer halo = Draw("Pickup highlight", go.transform, Vector2.zero,
                new Vector2(1.55f, 0.68f), Hex(0xffcf71), 9, ring);
            halo.enabled = false;
            Transform recoil = new GameObject("Visual / musket recoil").transform;
            recoil.SetParent(go.transform, false);
            SpriteRenderer side = Draw("Musket side / ground", recoil, Vector2.zero,
                new Vector2(0.23f, 0.23f), Color.white, 15, musketSideArt);
            SpriteRenderer top = Draw("Musket top / held", recoil, Vector2.zero,
                new Vector2(0.23f, 0.23f), Color.white, 15, musketTopArt);
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(recoil, false);
            muzzle.localPosition = new Vector3(0.63f, 0f, 0f);
            SpriteRenderer flash = Draw("Musket pixel muzzle flash", muzzle, new Vector2(0.15f, 0f),
                new Vector2(0.42f, 0.26f), Hex(0xffdf74), 19, circle);
            flash.enabled = false;
            MusketWeapon musket = go.AddComponent<MusketWeapon>();
            musket.Configure(tuning, recoil, muzzle, side, top, halo, flash, square, material);
        }

        private static ClubWeapon BuildClub(Vector2 position)
        {
            var go = new GameObject("Club / shoulder arc swing");
            go.layer = 9;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -18f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 1.1f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 0.34f);
            collider.sharedMaterial = gunMaterial;
            SpriteRenderer halo = Draw("Pickup highlight", go.transform, Vector2.zero,
                new Vector2(1.6f, 0.72f), Hex(0xffcf71), 9, ring);
            halo.enabled = false;
            Transform swing = new GameObject("Visual / shoulder swing root").transform;
            swing.SetParent(go.transform, false);
            Transform clubSprite = Draw("Club asset / handle at shoulder", swing, new Vector2(0.54f, 0f),
                new Vector2(0.27f, 0.27f), Color.white, 16, clubArt).transform;
            clubSprite.localRotation = Quaternion.Euler(0f, 0f, -90f);
            ClubWeapon club = go.AddComponent<ClubWeapon>();
            club.Configure(tuning, swing, halo, square, material);
            return club;
        }

        private static void BuildStaff(Vector2 position)
        {
            var go = new GameObject("Universal staff / 1 fire + 2 frost + 3 lightning");
            go.layer = 9;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -15f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.25f, 0.3f);
            collider.sharedMaterial = gunMaterial;
            SpriteRenderer halo = Draw("Pickup highlight", go.transform, Vector2.zero,
                new Vector2(1.55f, 0.74f), Hex(0xb990ff), 9, ring);
            halo.enabled = false;
            Transform visual = new GameObject("Visual / universal staff").transform;
            visual.SetParent(go.transform, false);
            Transform staffSprite = Draw("New staff asset", visual, Vector2.zero,
                new Vector2(0.23f, 0.23f), Color.white, 16, staffArt).transform;
            staffSprite.localRotation = Quaternion.Euler(0f, 0f, -45f);
            MagicStaff staff = go.AddComponent<MagicStaff>();
            staff.Configure(tuning, MagicElement.Fire, visual, halo, square, material);
        }

        private static Sprite LoadSprite(string path, string expectedName = null)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && (expectedName == null || sprite.name == expectedName)) return sprite;
            throw new System.InvalidOperationException("Sprite is missing or not imported: " + path +
                (expectedName == null ? string.Empty : " / " + expectedName));
        }

        internal static void AddPixelOutline(Transform source, int order)
        {
            SpriteRenderer sourceRenderer = source.GetComponent<SpriteRenderer>();
            for (int index = 0; index < 32; index++)
            {
                float angle = index * Mathf.PI * 2f / 32f;
                var outline = new GameObject("Black pixel outline " + index);
                outline.transform.SetParent(source, false);
                outline.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.24f;
                SpriteRenderer renderer = outline.AddComponent<SpriteRenderer>();
                renderer.sprite = sourceRenderer.sprite;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.color = new Color(0.025f, 0.018f, 0.016f, 1f);
                renderer.sortingOrder = order;
                renderer.flipX = sourceRenderer.flipX;
                renderer.flipY = sourceRenderer.flipY;
            }
            source.gameObject.AddComponent<ScreenPixelOutline>();
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
