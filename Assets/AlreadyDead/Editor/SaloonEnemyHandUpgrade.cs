using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlreadyDead.Editor
{
    public static class SaloonEnemyHandUpgrade
    {
        private const string Prefabs = "Assets/AlreadyDead/SaloonKit/Prefabs/";
        private const string KnifePath = "Assets/Waepon/Knife/Knife.png";
        private const string BeerPath = "Assets/Waepon/Beer/Beer.png";

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath("UpgradeSaloonHands.request");
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
                    File.WriteAllText(TempPath("UpgradeSaloonHands.result.txt"),
                        "SALOON_HANDS_OK");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(TempPath("UpgradeSaloonHands.error.txt"),
                        exception.ToString());
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Already Dead/Add knife and beer to Saloon enemies")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            PrepareTexture(KnifePath);
            PrepareTexture(BeerPath);
            Sprite knife = SpriteAt(KnifePath, "Knife_0");
            Sprite beer = SpriteAt(BeerPath, "Beer_0");
            foreach (string role in new[] { "Cowboy", "Chef", "Bartender" })
                UpgradePrefab(role, knife, beer);
            AssetDatabase.SaveAssets();
            Debug.Log("SALOON_HANDS_OK: knife and beer assigned to all three Saloon enemy prefabs.");
        }

        private static void PrepareTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Sprite missing: " + path);
            if (importer.filterMode == FilterMode.Point &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                !importer.mipmapEnabled) return;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Sprite SpriteAt(string path, string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == name) return sprite;
            throw new InvalidOperationException("Sprite subasset missing: " + path + " / " + name);
        }

        private static void UpgradePrefab(string role, Sprite knife, Sprite beer)
        {
            string path = Prefabs + role + ".prefab";
            if (!File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path))))
                throw new InvalidOperationException("Enemy prefab missing: " + path);
            string backup = TempPath("Saloon" + role + ".before-hand-prop.prefab");
            if (!File.Exists(backup)) File.Copy(Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", path)), backup);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform facing = FindChild(root.transform, "Facing / vision +X");
                Transform visual = facing != null
                    ? FindChild(facing, role + " top-down pixel art") : null;
                if (visual == null) throw new InvalidOperationException("Enemy facing missing: " + role);
                Transform hand = FindChild(facing, "Held item / knife or beer");
                if (hand == null)
                {
                    hand = new GameObject("Held item / knife or beer").transform;
                    hand.SetParent(facing, false);
                }
                SpriteRenderer renderer = hand.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = hand.gameObject.AddComponent<SpriteRenderer>();
                SpriteRenderer body = visual.GetComponent<SpriteRenderer>();
                renderer.sharedMaterial = body.sharedMaterial;
                renderer.sortingOrder = body.sortingOrder + 1;
                renderer.color = Color.white;

                SaloonHandProp held = root.GetComponent<SaloonHandProp>();
                if (held == null) held = root.AddComponent<SaloonHandProp>();
                held.Configure(renderer, knife, beer);
                held.Equip(role == "Cowboy"
                    ? SaloonHandProp.PropKind.Knife : SaloonHandProp.PropKind.Beer);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child;
            return null;
        }

        private static string TempPath(string file) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp", file));
    }
}
