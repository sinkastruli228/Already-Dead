using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlreadyDead.Editor
{
    public static class FirearmVisualsInstaller
    {
        private const string ResourcePath = "Assets/Resources/FirearmVisuals.asset";
        private const string BulletPath = "Assets/Waepon/Effect/Bullet.png";
        private const string FirePath = "Assets/Waepon/Effect/Gun_Fire.png";
        private const string MaterialFolder = "Assets/AlreadyDead/Art/WeaponEffects";

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath("InstallFirearmEffects.request");
                if (!File.Exists(request)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckRequest;
                    return;
                }
                try
                {
                    InstallAndSaveScenes();
                    File.Delete(request);
                    File.WriteAllText(TempPath("InstallFirearmEffects.result.txt"),
                        "FIREARM_EFFECTS_READY");
                }
                catch (Exception error)
                {
                    File.WriteAllText(TempPath("InstallFirearmEffects.error.txt"), error.ToString());
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("Already Dead/Install firearm effects and save open scenes")]
        public static void InstallAndSaveScenes()
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new IOException("Could not save the current Saloon edits.");
            AssetDatabase.ImportAsset(BulletPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FirePath, ImportAssetOptions.ForceUpdate);
            Sprite bullet = FindSprite(BulletPath, "Bullet_0");
            Sprite fire = FindSprite(FirePath, "Gun_Fire_0");
            Material baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/AlreadyDead/Art/Primitive.mat");
            if (baseMaterial == null)
                throw new InvalidOperationException("The shared primitive material is missing.");

            Directory.CreateDirectory(MaterialFolder);
            Material bulletMaterial = EnsureMaterial(MaterialFolder + "/Bullet.mat", baseMaterial, bullet);
            Material fireMaterial = EnsureMaterial(MaterialFolder + "/GunFire.mat", baseMaterial, fire);
            FirearmVisuals visuals = AssetDatabase.LoadAssetAtPath<FirearmVisuals>(ResourcePath);
            if (visuals == null)
            {
                visuals = ScriptableObject.CreateInstance<FirearmVisuals>();
                AssetDatabase.CreateAsset(visuals, ResourcePath);
            }
            visuals.Configure(bullet, fire, bulletMaterial, fireMaterial);
            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveOpenScenes())
                throw new IOException("Could not save the open scenes after installing effects.");
            Debug.Log("FIREARM_EFFECTS_READY: original bullet and fire sprites linked; scenes saved.");
        }

        private static Sprite FindSprite(string path, string name)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == name) return sprite;
            throw new InvalidOperationException("Missing imported sprite: " + path + " / " + name);
        }

        private static Material EnsureMaterial(string path, Material basis, Sprite sprite)
        {
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(basis) { name = sprite.name + " Unlit" };
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetTexture("_MainTex", sprite.texture);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static string TempPath(string filename) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp", filename));
    }
}
