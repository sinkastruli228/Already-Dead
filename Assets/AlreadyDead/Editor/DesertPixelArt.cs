using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlreadyDead.Editor
{
    // Small, reproducible 20 x 20 sprites. Every source pixel is one screen pixel
    // at the reference scale, with point filtering and no compression.
    internal static class DesertPixelArt
    {
        private const int Size = 20;
        private const string Path = "Assets/AlreadyDead/Art/Desert";

        internal static Sprite Sand(int variant) => Ensure("Sand" + variant, variant);
        internal static Sprite Rock(int variant) => Ensure("Rock" + variant, 10 + variant);
        internal static Sprite Bush(int variant) => Ensure("DryBush" + variant, 20 + variant);
        internal static Sprite Grass() => Ensure("DryGrass", 30);
        internal static Sprite RockWall() => Ensure("RockWall", 40);

        private static Sprite Ensure(string name, int kind)
        {
            Directory.CreateDirectory(Path);
            string assetPath = Path + "/" + name + ".png";
            if (!File.Exists(assetPath))
            {
                var pixels = new Color32[Size * Size];
                if (kind < 3) DrawSand(pixels, kind);
                else if (kind < 12) DrawRock(pixels, kind - 10);
                else if (kind < 22) DrawBush(pixels, kind - 20);
                else if (kind == 30) DrawGrass(pixels);
                else DrawRockWall(pixels);
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != Size ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = Size;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void DrawSand(Color32[] pixels, int variant)
        {
            Color32 baseColor = variant == 0 ? C(190, 156, 100) :
                variant == 1 ? C(188, 153, 97) : C(193, 160, 104);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    uint noise = Hash(x, y, variant + 17) % 100;
                    pixels[y * Size + x] = noise < 5 ? C(166, 130, 80) :
                        noise < 12 ? C(210, 179, 119) : baseColor;
                }
            for (int i = 0; i < 3; i++)
            {
                int x = 2 + (int)(Hash(i, variant, 2) % 15);
                int y = 3 + (int)(Hash(i, variant, 7) % 14);
                Put(pixels, x, y, C(147, 115, 76));
                Put(pixels, x + 1, y, C(166, 132, 84));
            }
        }

        private static void DrawRock(Color32[] pixels, int variant)
        {
            Color32 shadow = C(91, 73, 57, 170);
            for (int x = 3; x <= 17; x++)
                for (int y = 2; y <= 4; y++)
                    if ((x + y) % 4 != 0) Put(pixels, x, y, shadow);

            int[] left = variant == 0
                ? new[] { 7, 5, 4, 3, 3, 4, 5, 6, 8, 10, 12 }
                : new[] { 8, 6, 4, 3, 4, 4, 5, 7, 9, 11, 12 };
            int[] right = variant == 0
                ? new[] { 13, 15, 16, 17, 17, 16, 16, 15, 14, 13, 12 }
                : new[] { 12, 14, 16, 17, 17, 16, 16, 15, 14, 13, 12 };
            for (int row = 0; row < left.Length; row++)
            {
                int y = row + 4;
                for (int x = left[row]; x <= right[row]; x++)
                {
                    bool edge = x == left[row] || x == right[row] || row == 0;
                    Color32 color = edge ? C(89, 76, 62) :
                        row > 7 ? C(192, 177, 143) :
                        x < 8 ? C(129, 118, 99) : C(158, 145, 117);
                    Put(pixels, x, y, color);
                }
            }
            Line(pixels, 7, 10, 11, 12, C(220, 202, 158));
            Line(pixels, 11, 12, 14, 11, C(220, 202, 158));
            Put(pixels, 7, 7, C(108, 94, 75));
            Put(pixels, 8, 7, C(108, 94, 75));
        }

        private static void DrawBush(Color32[] pixels, int variant)
        {
            Color32 dark = C(91, 67, 43);
            Color32 branch = C(150, 109, 64);
            Color32 tip = C(202, 159, 91);
            for (int x = 5; x <= 15; x++) Put(pixels, x, 2, C(88, 66, 45, 130));
            int shift = variant == 0 ? 0 : 1;
            Line(pixels, 10, 3, 10, 13, dark);
            Line(pixels, 10, 4, 5 + shift, 14, branch);
            Line(pixels, 9, 6, 3 + shift, 10, branch);
            Line(pixels, 10, 7, 7 + shift, 18, branch);
            Line(pixels, 10, 7, 13 + shift, 18, branch);
            Line(pixels, 10, 5, 17, 12, branch);
            Line(pixels, 11, 7, 16, 8, branch);
            Line(pixels, 5 + shift, 14, 3 + shift, 16, tip);
            Line(pixels, 13 + shift, 18, 15 + shift, 16, tip);
            Put(pixels, 7 + shift, 18, tip);
            Put(pixels, 17, 12, tip);
            Put(pixels, 16, 8, tip);
            Put(pixels, 3 + shift, 10, tip);
            Put(pixels, 10, 14, C(176, 124, 67));
        }

        private static void DrawGrass(Color32[] pixels)
        {
            Color32 straw = C(172, 139, 73);
            Color32 light = C(210, 176, 96);
            for (int x = 5; x <= 15; x++) Put(pixels, x, 3, C(102, 80, 49, 110));
            Line(pixels, 10, 4, 10, 14, straw);
            Line(pixels, 10, 4, 5, 12, light);
            Line(pixels, 10, 4, 7, 15, straw);
            Line(pixels, 10, 4, 14, 16, light);
            Line(pixels, 10, 4, 16, 11, straw);
            Line(pixels, 10, 4, 12, 12, straw);
        }

        private static void DrawRockWall(Color32[] pixels)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    uint noise = Hash(x / 3, y / 3, 44) % 8;
                    pixels[y * Size + x] = noise < 2 ? C(106, 88, 65) :
                        noise > 5 ? C(144, 120, 85) : C(126, 105, 76);
                }
            Line(pixels, 2, 3, 7, 3, C(88, 73, 57));
            Line(pixels, 7, 3, 8, 6, C(88, 73, 57));
            Line(pixels, 13, 11, 18, 11, C(89, 75, 59));
            Put(pixels, 13, 12, C(184, 153, 107));
            Put(pixels, 14, 12, C(184, 153, 107));
        }

        private static void Line(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Put(pixels, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int doubled = 2 * error;
                if (doubled >= dy) { error += dy; x0 += sx; }
                if (doubled <= dx) { error += dx; y0 += sy; }
            }
        }

        private static void Put(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x >= 0 && x < Size && y >= 0 && y < Size) pixels[y * Size + x] = color;
        }

        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 73856093 ^ y * 19349663 ^ seed * 83492791);
                value ^= value >> 13;
                value *= 1274126177u;
                return value ^ (value >> 16);
            }
        }

        private static Color32 C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);
    }
}
