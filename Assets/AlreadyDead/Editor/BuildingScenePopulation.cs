using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead.Editor
{
    // Adds gameplay objects to the existing level. Never calls BuildingSceneBuilder.BuildScene.
    public static class BuildingScenePopulation
    {
        private const string ScenePath = BuildingSceneBuilder.ScenePath;
        private const string SamplePath = PrototypeSceneBuilder.ScenePath;
        private const string SandboxPath = "Assets/AlreadyDead/Tests/Scenes/WeaponSandbox.unity";
        private const string FantasyPath = FantasySceneBuilder.ScenePath;
        private const string TuningPath = "Assets/AlreadyDead/PrototypeTuning.asset";
        private const string MarkerName = "InstallBuildingLoadout.request";

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
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Save building scene and place player with weapons")]
        public static void Install()
        {
            Scene target = SceneManager.GetSceneByPath(ScenePath);
            if (!target.IsValid() || !target.isLoaded)
                target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!target.IsValid() || !target.isLoaded)
                throw new InvalidOperationException("Building scene could not be opened.");

            // Save the user's current in-memory layout before importing anything.
            if (!EditorSceneManager.SaveScene(target))
                throw new IOException("Building scene could not be saved before placement.");
            SceneManager.SetActiveScene(target);

            PrototypeTuning tuning = AssetDatabase.LoadAssetAtPath<PrototypeTuning>(TuningPath);
            if (tuning == null) throw new InvalidOperationException("PrototypeTuning is missing.");
            Camera camera = FindRootComponent<Camera>(target);
            if (camera == null) throw new InvalidOperationException("Building scene camera is missing.");
            AimCamera view = camera.GetComponent<AimCamera>();
            if (view == null) view = camera.gameObject.AddComponent<AimCamera>();
            camera.orthographicSize = tuning.cameraSize;
            camera.name = "Main Camera / follow player";

            Vector2 center = ChooseOpenPosition();
            TopDownPlayer player = FindRootComponent<TopDownPlayer>(target);
            Scene sample = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Additive);
            try
            {
                if (player == null)
                {
                    TopDownPlayer original = FindRootComponent<TopDownPlayer>(sample);
                    if (original == null) throw new InvalidOperationException("Prototype player is missing.");
                    player = CopyRoot(original.gameObject, target).GetComponent<TopDownPlayer>();
                    player.transform.position = center;
                }

                if (FindRootComponent<MusketWeapon>(target) == null)
                {
                    MusketWeapon original = FindRootComponent<MusketWeapon>(sample);
                    if (original == null) throw new InvalidOperationException("Prototype musket is missing.");
                    GameObject musket = CopyRoot(original.gameObject, target);
                    musket.transform.position = center + new Vector2(1.35f, 1.05f);
                    musket.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                }
            }
            finally { EditorSceneManager.CloseScene(sample, true); }

            // The cloned player's internal visual references survive; camera references
            // are rebound because the source scene has just been unloaded.
            UnarmedCombat unarmed = player.GetComponent<UnarmedCombat>();
            Transform socket = FindChild(player.transform, "Weapon socket");
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AlreadyDead/Art/Square.png");
            Material primitive = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/AlreadyDead/Art/Primitive.mat");
            if (unarmed == null || socket == null || square == null || primitive == null)
                throw new InvalidOperationException("Prototype player references are incomplete.");
            unarmed.Configure(tuning, unarmed.LeftArm, unarmed.RightArm, view, square, primitive);
            player.Configure(tuning, player.Facing, socket, view, unarmed);
            PlayerVitality vitality = player.GetComponent<PlayerVitality>();
            if (vitality != null) vitality.Configure(tuning, view);
            view.Configure(tuning, player);
            camera.transform.position = player.transform.position + Vector3.back * 10f;

            // Place every weapon within the player's pickup reach.
            center = player.transform.position;
            if (FindRootByName(target, "Glock / 17 rounds") == null)
                PrototypeSceneBuilder.BuildPistolInOpenScene(center + new Vector2(-1.35f, 1.05f), false);
            if (FindRootByName(target, "M4 / 25 rounds automatic") == null)
                PrototypeSceneBuilder.BuildPistolInOpenScene(center + new Vector2(0f, 1.65f), true);

            Scene sandbox = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Additive);
            try
            {
                CopyWeaponIfMissing<SpearWeapon>(sandbox, target, center + new Vector2(1.6f, -0.2f));
                CopyWeaponIfMissing<ClubWeapon>(sandbox, target, center + new Vector2(-0.45f, -1.5f));
                if (FindRootComponent<RockWeapon>(target) == null)
                {
                    RockWeapon original = FindRock(sandbox);
                    if (original == null) throw new InvalidOperationException("Prototype stone is missing.");
                    RockWeapon rock = CopyRoot(original.gameObject, target).GetComponent<RockWeapon>();
                    rock.transform.position = center + new Vector2(1.05f, -1.35f);
                    SerializedObject serializedRock = new SerializedObject(rock);
                    serializedRock.FindProperty("isBuried").boolValue = false;
                    serializedRock.ApplyModifiedPropertiesWithoutUndo();
                    rock.Visual.gameObject.SetActive(true);
                    rock.Shadow.gameObject.SetActive(true);
                    rock.BuriedMark.gameObject.SetActive(false);
                    rock.Body.bodyType = RigidbodyType2D.Dynamic;
                }
            }
            finally { EditorSceneManager.CloseScene(sandbox, true); }

            Scene fantasy = EditorSceneManager.OpenScene(FantasyPath, OpenSceneMode.Additive);
            try { CopyWeaponIfMissing<MagicStaff>(fantasy, target, center + new Vector2(-1.65f, -0.25f)); }
            finally { EditorSceneManager.CloseScene(fantasy, true); }

            if (FindRootComponent<PrototypeHud>(target) == null)
            {
                var hud = new GameObject("Ammo + throw charge").AddComponent<PrototypeHud>();
                SceneManager.MoveGameObjectToScene(hud.gameObject, target);
                hud.Configure(player);
            }

            SceneManager.SetActiveScene(target);
            EditorSceneManager.MarkSceneDirty(target);
            if (!EditorSceneManager.SaveScene(target))
                throw new IOException("Building scene could not be saved after placement.");
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_BUILDING_LOADOUT_READY: " + ScenePath);
        }

        private static Vector2 ChooseOpenPosition()
        {
            Vector2[] candidates =
            {
                new Vector2(0f, 3.2f), new Vector2(0f, 5.2f),
                new Vector2(4f, 3.2f), new Vector2(-4f, 3.2f),
                new Vector2(0f, -6f)
            };
            Physics2D.SyncTransforms();
            foreach (Vector2 candidate in candidates)
                if (Physics2D.OverlapCircle(candidate, 2.1f, 1 << 8) == null)
                    return candidate;
            return candidates[0];
        }

        private static T FindRootComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponent<T>();
                if (component != null) return component;
            }
            return null;
        }

        private static RockWeapon FindRock(Scene scene)
        {
            RockWeapon fallback = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (RockWeapon rock in root.GetComponentsInChildren<RockWeapon>(true))
                {
                    if (fallback == null) fallback = rock;
                    if (rock.gameObject.activeInHierarchy && !rock.IsBuried) return rock;
                }
            return fallback;
        }

        private static GameObject FindRootByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static GameObject CopyRoot(GameObject original, Scene target)
        {
            GameObject copy = UnityEngine.Object.Instantiate(original);
            copy.name = original.name;
            copy.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(copy, target);
            return copy;
        }

        private static void CopyWeaponIfMissing<T>(Scene source, Scene target, Vector2 position)
            where T : Component
        {
            if (FindRootComponent<T>(target) != null) return;
            T original = FindRootComponent<T>(source);
            if (original == null) throw new InvalidOperationException(typeof(T).Name + " is missing.");
            GameObject copy = CopyRoot(original.gameObject, target);
            copy.transform.position = position;
            copy.SetActive(true);
        }
    }
}
