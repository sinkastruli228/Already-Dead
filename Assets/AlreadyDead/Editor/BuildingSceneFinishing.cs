using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace AlreadyDead.Editor
{
    public static class BuildingSceneFinishing
    {
        private const string MarkerName = "FinishBuildingScene.request";
        private const string KitTiles = "Assets/AlreadyDead/BuildingKit/Tiles/";
        private const string KitArt = "Assets/AlreadyDead/BuildingKit/Art/";
        private const int FurnitureLayer = 12;

        private static readonly HashSet<string> Furniture = new HashSet<string>
        {
            "LeatherSofaBrown", "LeatherSofaTan", "Fridge", "Stool", "CoffeeTable",
            "DiningTable", "Cabinet", "PottedPlant", "TrashBin", "ReceptionDesk",
            "IVStand", "HospitalBedCream", "HospitalBedTeal", "MedicalWasteBin"
        };

        private static readonly Dictionary<string, Vector2[]> FirstRoomSamples =
            new Dictionary<string, Vector2[]>
            {
                { "LeatherSofaBrown", new[] { new Vector2(-6.5f, 7.2f) } },
                { "LeatherSofaTan", new[] { new Vector2(-2.8f, 7.2f) } },
                { "CoffeeTable", new[] { new Vector2(-4.8f, 5.3f) } },
                { "Fridge", new[] { new Vector2(7.6f, 8.4f) } },
                { "Cabinet", new[] { new Vector2(6.2f, 5.3f) } },
                { "DiningTable", new[] { new Vector2(3.3f, 7.4f) } },
                { "Stool", new[] { new Vector2(2f, 5.7f), new Vector2(4.3f, 5.7f) } },
                { "PottedPlant", new[] { new Vector2(-8.6f, 9.4f) } },
                { "TrashBin", new[] { new Vector2(8f, 2.7f) } }
            };

        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall += () =>
            {
                string marker = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Temp/" + MarkerName));
                if (!File.Exists(marker)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.delayCall += CheckRequest;
                    return;
                }
                try
                {
                    Apply();
                    File.Delete(marker);
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../Temp/FinishBuildingScene.error.txt")), error.ToString());
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("Already Dead/Finish building furniture and broken lamp")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetSceneByPath(BuildingSceneBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(BuildingSceneBuilder.ScenePath, OpenSceneMode.Additive);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Building scene is not loaded.");

            // Preserve the editor's current, possibly unsaved, hand-painted layout first.
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the current building layout.");
            string backup = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Temp/BuildingParkingScene.before-finishing.unity"));
            File.Copy(BuildingSceneBuilder.ScenePath, backup, true);

            int removedSamples = RemoveFirstRoomSamples(scene);
            int movedTiles = MoveFurnitureTiles(scene);
            int mergedMaps = ConsolidateFurnitureMaps(scene);
            int movedObjects = MoveFurnitureObjects(scene);
            UpdateFurniturePrefabs();
            AttachBrokenLamp(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the finished building scene.");
            Debug.Log($"BUILDING_FINISHED: {movedTiles} furniture tiles, {movedObjects} objects, " +
                $"{removedSamples} first-room examples removed, {mergedMaps} duplicate maps merged, " +
                "broken lamp attached.");
        }

        private static int RemoveFirstRoomSamples(Scene scene)
        {
            Transform samples = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "MOVE OR DUPLICATE / placed prefab examples")
                    samples = root.transform;
            if (samples == null) throw new InvalidOperationException("Sample furniture group is missing.");

            var toRemove = new List<GameObject>();
            foreach (Transform child in samples)
            {
                if (!FirstRoomSamples.TryGetValue(child.name, out Vector2[] positions)) continue;
                Vector2 current = child.localPosition;
                foreach (Vector2 expected in positions)
                    if (Vector2.Distance(current, expected) < 0.05f)
                    {
                        toRemove.Add(child.gameObject);
                        break;
                    }
            }
            if (toRemove.Count != 0 && toRemove.Count != 10)
                throw new InvalidOperationException("Expected 10 original first-room props, found " + toRemove.Count);
            foreach (GameObject item in toRemove) Object.DestroyImmediate(item);
            return toRemove.Count;
        }

        private static int MoveFurnitureTiles(Scene scene)
        {
            int moved = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Tilemap source in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (source.gameObject.layer == FurnitureLayer) continue;
                var cells = new List<Vector3Int>();
                foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
                {
                    TileBase tile = source.GetTile(cell);
                    if (tile == null || !Furniture.Contains(tile.name) ||
                        !AssetDatabase.GetAssetPath(tile).StartsWith(KitTiles, StringComparison.Ordinal))
                        continue;
                    if (source.GetCellCenterWorld(cell).y < 0.5f) continue; // Outdoor parking stays solid to bullets.
                    cells.Add(cell);
                }
                if (cells.Count == 0) continue;

                Tilemap furniture = FindOrCreateFurnitureMap(source);
                foreach (Vector3Int cell in cells)
                {
                    TileBase tile = source.GetTile(cell);
                    Color color = source.GetColor(cell);
                    Matrix4x4 matrix = source.GetTransformMatrix(cell);
                    TileFlags flags = source.GetTileFlags(cell);
                    furniture.SetTile(cell, tile);
                    furniture.SetTileFlags(cell, TileFlags.None);
                    furniture.SetColor(cell, color);
                    furniture.SetTransformMatrix(cell, matrix);
                    furniture.SetTileFlags(cell, flags);
                    source.SetTile(cell, null);
                    moved++;
                }
            }
            return moved;
        }

        private static Tilemap FindOrCreateFurnitureMap(Tilemap source)
        {
            const string name = "04 Furniture / characters collide, bullets pass";
            Transform parent = source.transform.parent;
            foreach (Transform child in parent)
                if (child.name == name && child.GetComponent<Tilemap>() is Tilemap existing)
                    return existing;

            var objectForTiles = new GameObject(name);
            objectForTiles.layer = FurnitureLayer;
            objectForTiles.transform.SetParent(parent, false);
            objectForTiles.transform.localPosition = source.transform.localPosition;
            objectForTiles.transform.localRotation = source.transform.localRotation;
            objectForTiles.transform.localScale = source.transform.localScale;
            Tilemap target = objectForTiles.AddComponent<Tilemap>();
            TilemapRenderer renderer = objectForTiles.AddComponent<TilemapRenderer>();
            TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = Mathf.Max(5, sourceRenderer.sortingOrder + 1);
            objectForTiles.AddComponent<TilemapCollider2D>();
            return target;
        }

        private static int ConsolidateFurnitureMaps(Scene scene)
        {
            Tilemap primary = null;
            int merged = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.gameObject.layer != FurnitureLayer ||
                    map.name != "04 Furniture / characters collide, bullets pass") continue;
                if (primary == null)
                {
                    primary = map;
                    continue;
                }
                bool hasOverlaps = false;
                var nonOverlapping = new List<Vector3Int>();
                foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                {
                    TileBase tile = map.GetTile(cell);
                    if (tile == null) continue;
                    if (primary.HasTile(cell))
                    {
                        hasOverlaps = true;
                        continue;
                    }
                    primary.SetTile(cell, tile);
                    primary.SetTileFlags(cell, TileFlags.None);
                    primary.SetColor(cell, map.GetColor(cell));
                    primary.SetTransformMatrix(cell, map.GetTransformMatrix(cell));
                    primary.SetTileFlags(cell, map.GetTileFlags(cell));
                    nonOverlapping.Add(cell);
                }
                foreach (Vector3Int cell in nonOverlapping) map.SetTile(cell, null);
                if (hasOverlaps)
                {
                    map.name = "05 Furniture / overlapping pieces";
                    map.GetComponent<TilemapRenderer>().sortingOrder =
                        primary.GetComponent<TilemapRenderer>().sortingOrder - 1;
                }
                else Object.DestroyImmediate(map.gameObject);
                merged++;
            }
            return merged;
        }

        private static int MoveFurnitureObjects(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null || !Furniture.Contains(renderer.sprite.name) ||
                    !AssetDatabase.GetAssetPath(renderer.sprite).StartsWith(KitArt, StringComparison.Ordinal) ||
                    renderer.transform.position.y < 0.5f ||
                    Mathf.Abs(renderer.transform.position.x) > 21f) continue;
                GameObject item = renderer.gameObject;
                if (item.GetComponent<Collider2D>() == null)
                {
                    BoxCollider2D hitbox = item.AddComponent<BoxCollider2D>();
                    hitbox.size = renderer.sprite.bounds.size * 0.82f;
                }
                if (item.layer == FurnitureLayer) continue;
                item.layer = FurnitureLayer;
                count++;
            }
            return count;
        }

        private static void UpdateFurniturePrefabs()
        {
            foreach (string name in Furniture)
            {
                string path = $"Assets/AlreadyDead/BuildingKit/Prefabs/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    contents.layer = FurnitureLayer;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
        }

        private static void AttachBrokenLamp(Scene scene)
        {
            var greenRoomLights = new List<Light2D>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Light2D light = root.GetComponent<Light2D>();
                if (light == null || light.lightType == Light2D.LightType.Global) continue;
                Vector2 position = root.transform.position;
                if (position.x > -11f || position.x < -17f ||
                    position.y < 16f || position.y > 21f) continue;
                if (light.color.r < 0.2f && light.color.g > 0.8f)
                    greenRoomLights.Add(light);
            }
            if (greenRoomLights.Count == 0)
                throw new InvalidOperationException("Marked green lamp is missing.");
            BrokenLampFlicker flicker = greenRoomLights[0].GetComponent<BrokenLampFlicker>();
            if (flicker == null) flicker = greenRoomLights[0].gameObject.AddComponent<BrokenLampFlicker>();
            flicker.SetLinkedLights(greenRoomLights.ToArray());
            EditorUtility.SetDirty(flicker);
        }
    }
}
