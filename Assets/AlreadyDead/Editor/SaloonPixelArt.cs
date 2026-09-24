using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlreadyDead.Editor
{
    // Every source pixel is displayed sharply at 32 pixels per world unit.
    internal static class SaloonPixelArt
    {
        internal const string Folder = "Assets/AlreadyDead/SaloonKit/Art";
        internal static readonly string[] Names =
        {
            "Sand", "BoardFloor", "WoodWall", "DoorLeaf", "RoundTable",
            "BarCounter", "PrepTable", "Toilet", "Cowboy", "Chef", "Bartender",
            "Cactus", "DryBush", "Crate", "Barrel", "Stove", "Chair", "BottleShelf"
        };

        internal static Sprite Ensure(string name)
        {
            Dimensions(name, out int width, out int height);
            Directory.CreateDirectory(Folder);
            string path = Folder + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var pixels = new Color32[width * height];
                Paint(name, pixels, width, height);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spritePixelsPerUnit != 32 || importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Dimensions(string name, out int width, out int height)
        {
            width = height = 32;
            switch (name)
            {
                case "DoorLeaf": width = 64; height = 10; break;
                case "RoundTable": width = height = 64; break;
                case "BarCounter": width = 128; height = 42; break;
                case "PrepTable": width = 96; height = 48; break;
                case "Cactus": width = 32; height = 48; break;
                case "Stove": width = 64; height = 48; break;
                case "BottleShelf": width = 64; height = 32; break;
            }
        }

        private static void Paint(string name, Color32[] p, int w, int h)
        {
            Color32 ink = C(34, 24, 19);
            Color32 darkWood = C(78, 44, 28);
            Color32 wood = C(132, 77, 42);
            Color32 lightWood = C(181, 113, 61);
            switch (name)
            {
                case "Sand":
                    Fill(p, C(178, 145, 101));
                    for (int i = 0; i < 42; i++)
                    {
                        int x = (i * 17 + i * i * 7) % w;
                        int y = (i * 23 + i * i * 3) % h;
                        Dot(p, w, h, x, y, i % 3 == 0 ? C(125, 105, 77) : C(208, 176, 124));
                    }
                    Rect(p, w, h, 5, 5, 3, 1, C(142, 119, 87));
                    Rect(p, w, h, 23, 23, 4, 1, C(225, 187, 134));
                    break;
                case "BoardFloor":
                    Fill(p, C(120, 68, 39));
                    for (int y = 0; y < 32; y += 8)
                    {
                        Rect(p, w, h, 0, y, 32, 1, ink);
                        Rect(p, w, h, 1, y + 1, 30, 1, C(171, 103, 57));
                        Rect(p, w, h, 2, y + 5, 27, 1, C(99, 55, 32));
                        int seam = (y / 8) % 2 == 0 ? 12 : 25;
                        Rect(p, w, h, seam, y, 1, 8, C(58, 34, 24));
                        Dot(p, w, h, 4, y + 3, C(43, 32, 25));
                        Dot(p, w, h, 29, y + 4, C(43, 32, 25));
                    }
                    break;
                case "WoodWall":
                    Fill(p, ink);
                    Rect(p, w, h, 1, 2, 30, 28, darkWood);
                    Rect(p, w, h, 2, 5, 28, 18, wood);
                    for (int y = 7; y < 24; y += 6)
                    {
                        Rect(p, w, h, 2, y, 28, 1, C(63, 37, 26));
                        Rect(p, w, h, 3, y + 1, 26, 1, lightWood);
                    }
                    Rect(p, w, h, 0, 25, 32, 5, C(54, 34, 26));
                    Rect(p, w, h, 0, 29, 32, 2, C(191, 127, 72));
                    Rect(p, w, h, 0, 0, 32, 2, C(54, 34, 26));
                    break;
                case "DoorLeaf":
                    Rect(p, w, h, 0, 0, w, h, ink);
                    Rect(p, w, h, 2, 2, w - 4, h - 4, wood);
                    for (int x = 13; x < w - 2; x += 13)
                        Rect(p, w, h, x, 2, 1, h - 4, darkWood);
                    Rect(p, w, h, 3, 2, w - 6, 1, lightWood);
                    Rect(p, w, h, 5, 3, 3, 4, C(75, 76, 72));
                    Rect(p, w, h, w - 9, 4, 3, 2, C(216, 174, 86));
                    break;
                case "RoundTable":
                    Disk(p, w, h, 32, 32, 29, ink);
                    Disk(p, w, h, 32, 32, 26, darkWood);
                    Disk(p, w, h, 32, 32, 23, wood);
                    for (int y = 16; y < 50; y += 9)
                        Rect(p, w, h, 13, y, 38, 1, C(103, 57, 32));
                    Disk(p, w, h, 22, 38, 3, C(215, 184, 123));
                    Disk(p, w, h, 43, 26, 3, C(228, 204, 155));
                    Dot(p, w, h, 34, 44, C(244, 230, 183));
                    break;
                case "BarCounter":
                    Rect(p, w, h, 0, 1, w, 40, ink);
                    Rect(p, w, h, 3, 4, w - 6, 34, darkWood);
                    Rect(p, w, h, 5, 17, w - 10, 18, wood);
                    Rect(p, w, h, 5, 32, w - 10, 3, lightWood);
                    Rect(p, w, h, 7, 7, w - 14, 4, C(49, 29, 21));
                    for (int x = 13; x < w - 7; x += 22)
                    {
                        Rect(p, w, h, x, 19, 2, 13, C(92, 52, 30));
                        Disk(p, w, h, x + 7, 26, 3, C(211, 179, 111));
                    }
                    break;
                case "PrepTable":
                    Rect(p, w, h, 1, 2, w - 2, h - 4, ink);
                    Rect(p, w, h, 4, 5, w - 8, h - 10, C(126, 86, 53));
                    Rect(p, w, h, 5, h - 9, w - 10, 2, C(210, 165, 101));
                    Rect(p, w, h, 11, 13, 27, 20, C(194, 142, 84));
                    Rect(p, w, h, 15, 18, 15, 2, C(94, 59, 34));
                    Disk(p, w, h, 67, 23, 10, C(74, 76, 72));
                    Disk(p, w, h, 67, 23, 7, C(183, 190, 178));
                    break;
                case "Toilet":
                    Disk(p, w, h, 16, 15, 14, ink);
                    Disk(p, w, h, 16, 15, 11, C(219, 218, 199));
                    Disk(p, w, h, 16, 15, 7, C(92, 120, 126));
                    Disk(p, w, h, 16, 15, 4, C(40, 68, 75));
                    Rect(p, w, h, 6, 27, 20, 5, C(222, 216, 186));
                    break;
                case "Cowboy":
                case "Chef":
                case "Bartender":
                    Character(name, p, w, h);
                    break;
                case "Cactus":
                    Rect(p, w, h, 12, 4, 11, 38, ink);
                    Rect(p, w, h, 14, 6, 7, 35, C(53, 113, 58));
                    Rect(p, w, h, 16, 7, 2, 32, C(116, 159, 84));
                    Rect(p, w, h, 4, 18, 10, 6, ink);
                    Rect(p, w, h, 5, 21, 7, 8, C(54, 108, 57));
                    Rect(p, w, h, 21, 26, 8, 6, ink);
                    Rect(p, w, h, 24, 27, 4, 10, C(54, 108, 57));
                    for (int y = 9; y < 39; y += 8) Dot(p, w, h, 19, y, C(229, 205, 130));
                    break;
                case "DryBush":
                    Disk(p, w, h, 16, 15, 12, C(65, 62, 39));
                    for (int i = 0; i < 15; i++)
                    {
                        int x = 3 + (i * 11) % 26;
                        int y = 4 + (i * 17) % 25;
                        Rect(p, w, h, x, y, 4, 2, i % 2 == 0 ? C(102, 104, 62) : C(143, 111, 62));
                    }
                    break;
                case "Crate":
                    Rect(p, w, h, 1, 1, 30, 30, ink);
                    Rect(p, w, h, 4, 4, 24, 24, wood);
                    Rect(p, w, h, 6, 6, 20, 20, lightWood);
                    Rect(p, w, h, 6, 15, 20, 2, darkWood);
                    Rect(p, w, h, 15, 6, 2, 20, darkWood);
                    Rect(p, w, h, 3, 3, 4, 4, C(82, 81, 73));
                    Rect(p, w, h, 25, 25, 4, 4, C(82, 81, 73));
                    break;
                case "Barrel":
                    Disk(p, w, h, 16, 16, 15, ink);
                    Disk(p, w, h, 16, 16, 12, darkWood);
                    Disk(p, w, h, 16, 16, 9, wood);
                    Disk(p, w, h, 16, 16, 4, C(109, 62, 34));
                    Rect(p, w, h, 6, 8, 20, 2, C(94, 94, 82));
                    Rect(p, w, h, 6, 22, 20, 2, C(94, 94, 82));
                    break;
                case "Stove":
                    Rect(p, w, h, 1, 1, 62, 46, ink);
                    Rect(p, w, h, 4, 4, 56, 40, C(67, 66, 61));
                    Rect(p, w, h, 7, 31, 50, 9, C(91, 93, 85));
                    foreach (int x in new[] { 18, 45 })
                    foreach (int y in new[] { 14, 30 })
                    {
                        Disk(p, w, h, x, y, 8, C(25, 25, 25));
                        Disk(p, w, h, x, y, 5, C(105, 106, 101));
                        Disk(p, w, h, x, y, 2, C(35, 35, 34));
                    }
                    break;
                case "Chair":
                    Rect(p, w, h, 3, 3, 26, 26, ink);
                    Rect(p, w, h, 6, 6, 20, 20, darkWood);
                    Rect(p, w, h, 8, 8, 16, 15, wood);
                    Rect(p, w, h, 4, 24, 24, 4, lightWood);
                    break;
                case "BottleShelf":
                    Rect(p, w, h, 0, 3, 64, 26, ink);
                    Rect(p, w, h, 3, 6, 58, 20, darkWood);
                    Rect(p, w, h, 4, 14, 56, 2, lightWood);
                    for (int x = 8; x < 60; x += 8)
                    {
                        Rect(p, w, h, x, 7, 4, 9, x % 3 == 0 ? C(48, 115, 91) : C(132, 90, 50));
                        Rect(p, w, h, x + 1, 16, 3, 8, C(170, 132, 66));
                    }
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(name), name, null);
            }
        }

        private static void Character(string role, Color32[] p, int w, int h)
        {
            Color32 coat = role == "Chef" ? C(221, 211, 178) :
                role == "Bartender" ? C(77, 55, 61) : C(103, 66, 43);
            Disk(p, w, h, 16, 16, 13, C(32, 23, 20));
            Disk(p, w, h, 16, 16, 10, coat);
            Rect(p, w, h, 12, 12, 8, 9, C(188, 127, 89));
            if (role == "Cowboy")
            {
                Rect(p, w, h, 3, 21, 26, 5, C(58, 39, 29));
                Rect(p, w, h, 8, 24, 16, 6, C(110, 71, 43));
                Rect(p, w, h, 11, 25, 10, 2, C(154, 103, 56));
            }
            else if (role == "Chef")
            {
                Rect(p, w, h, 6, 23, 20, 6, C(234, 231, 211));
                Rect(p, w, h, 9, 26, 14, 5, C(251, 248, 226));
                Rect(p, w, h, 13, 6, 6, 6, C(241, 237, 215));
            }
            else
            {
                Rect(p, w, h, 9, 21, 14, 6, C(43, 35, 36));
                Rect(p, w, h, 14, 5, 4, 12, C(230, 224, 201));
                Rect(p, w, h, 15, 12, 2, 5, C(148, 54, 45));
            }
            Dot(p, w, h, 13, 18, C(38, 29, 27));
            Dot(p, w, h, 19, 18, C(38, 29, 27));
        }

        private static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        private static void Fill(Color32[] p, Color32 color)
        {
            for (int i = 0; i < p.Length; i++) p[i] = color;
        }
        private static void Dot(Color32[] p, int w, int h, int x, int y, Color32 color)
        {
            if (x >= 0 && x < w && y >= 0 && y < h) p[y * w + x] = color;
        }
        private static void Rect(Color32[] p, int w, int h, int x, int y, int width, int height, Color32 color)
        {
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(h, y + height); yy++)
            for (int xx = Mathf.Max(0, x); xx < Mathf.Min(w, x + width); xx++)
                p[yy * w + xx] = color;
        }
        private static void Disk(Color32[] p, int w, int h, int cx, int cy, int radius, Color32 color)
        {
            for (int y = Mathf.Max(0, cy - radius); y < Mathf.Min(h, cy + radius + 1); y++)
            for (int x = Mathf.Max(0, cx - radius); x < Mathf.Min(w, cx + radius + 1); x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                    p[y * w + x] = color;
        }
    }
}
