using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AlreadyDead.Editor
{
    public static class SaloonInteriorWallsCleanup
    {
        private const string ScenePath = SaloonSceneBuilder.ScenePath;
        private const string MarkerName = "SALOON / open interior + two-tile entrances";

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string request = TempPath("ClearSaloonInteriorWalls.request");
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
                    File.WriteAllText(TempPath("ClearSaloonInteriorWalls.result.txt"),
                        "SALOON_OPEN_INTERIOR_OK");
                }
                catch (Exception error)
                {
                    File.WriteAllText(TempPath("ClearSaloonInteriorWalls.error.txt"), error.ToString());
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("Already Dead/Clear Saloon interior walls and restore two-tile entrances")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new IOException("Could not save open scenes before editing Saloon walls.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == MarkerName)
                    throw new InvalidOperationException("Saloon interior walls have already been cleared.");

            string backup = TempPath("SaloonScene.before-interior-wall-removal.unity");
            if (!File.Exists(backup))
                File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath)), backup);

            Tilemap walls = null;
            PushDoor2D door = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("EDITABLE SALOON TILEMAPS /", StringComparison.Ordinal))
                    foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>())
                        if (map.name.StartsWith("03 Wood walls /", StringComparison.Ordinal))
                            walls = map;
                if (root.GetComponent<PushDoor2D>() is PushDoor2D found) door = found;
            }
            if (walls == null) throw new InvalidOperationException("Saloon wall tilemap is missing.");
            if (door == null) throw new InvalidOperationException("Saloon entrance door is missing.");
            TileBase wall = walls.GetTile(new Vector3Int(-23, 17, 0));
            if (wall == null) throw new InvalidOperationException("Saloon outer wall tile is missing.");

            walls.ClearAllTiles();
            void Put(int x, int y) => walls.SetTile(new Vector3Int(x, y, 0), wall);

            // One-tile-thick exterior walls around the enlarged main building.
            for (int x = -23; x <= 22; x++)
            {
                if (x != -19 && x != -18) Put(x, 17); // North entrance.
                if (x != -1 && x != 0) Put(x, -9);   // Main entrance to the porch.
            }
            for (int y = -8; y <= 16; y++)
            {
                Put(-23, y);
                Put(22, y);
            }

            // The porch keeps its outer shape and a two-tile entry from the sand.
            for (int y = -17; y <= -10; y++)
            {
                Put(-6, y);
                Put(5, y);
            }
            for (int x = -6; x <= 5; x++)
                if (x != -1 && x != 0) Put(x, -18);

            walls.RefreshAllTiles();
            walls.CompressBounds();

            // Match the original two-unit leaf and hinge arrangement to the new entry.
            door.transform.position = new Vector3(-1.5f, -9f, door.transform.position.z);
            door.transform.localScale = Vector3.one;
            door.transform.rotation = Quaternion.identity;

            new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save SaloonScene after removing interior walls.");
            Debug.Log("SALOON_OPEN_INTERIOR_OK: interior walls removed, entrances two tiles, door restored.");
        }

        private static string TempPath(string filename) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Temp", filename));
    }
}
