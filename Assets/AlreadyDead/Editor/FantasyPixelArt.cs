using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlreadyDead.Editor
{
    // Reproducible forest sprites for the second level. Geometry stays crisp at any camera scale.
    public static class FantasyPixelArt
    {
        private const string ArtDirectory = "Assets/AlreadyDead/Art/Fantasy";

        internal static Sprite Grass(int variant) => Ensure("Grass" + variant, 20, 20, 0, variant);
        internal static Sprite Path(int variant) => Ensure("Path" + variant, 20, 20, 1, variant);
        internal static Sprite Tree(int variant) => Ensure("Tree" + variant, 64, 72, 2, variant);
        internal static Sprite Flowers(int variant) => Ensure("Flowers" + variant, 20, 20, 3, variant);
        internal static Sprite Stone() => Ensure("Stone", 20, 20, 4, 0);
        internal static Sprite Fence() => Ensure("Fence", 20, 20, 5, 0);
        internal static Sprite Mage() => Ensure("Mage", 32, 32, 6, 0);
        internal static Sprite Knight() => Ensure("Knight", 32, 32, 7, 0);

        // Batch entry point for preparing the checked-in sprite library in an isolated Unity copy.
        public static void GenerateAll()
        {
            for (int i = 0; i < 3; i++)
            {
                Grass(i);
                Tree(i);
                Flowers(i);
            }
            for (int i = 0; i < 2; i++) Path(i);
            Stone();
            Fence();
            Mage();
            Knight();
            AssetDatabase.SaveAssets();
            Debug.Log("ALREADY_DEAD_FANTASY_ART_READY");
        }

        private static Sprite Ensure(string name, int width, int height, int kind, int variant)
        {
            Directory.CreateDirectory(ArtDirectory);
            string assetPath = ArtDirectory + "/" + name + ".png";
            if (!File.Exists(assetPath))
            {
                var pixels = new Color32[width * height];
                switch (kind)
                {
                    case 0: DrawGrass(pixels, width, height, variant); break;
                    case 1: DrawPath(pixels, width, height, variant); break;
                    case 2: DrawTree(pixels, width, height, variant); break;
                    case 3: DrawFlowers(pixels, width, height, variant); break;
                    case 4: DrawStone(pixels, width, height); break;
                    case 5: DrawFence(pixels, width, height); break;
                    case 6: DrawMage(pixels, width, height); break;
                    case 7: DrawKnight(pixels, width, height); break;
                }
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            int pixelsPerUnit = kind == 2 ? 32 : kind == 6 || kind == 7 ? 32 : 20;
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spritePixelsPerUnit != pixelsPerUnit ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = pixelsPerUnit;
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

        private static void DrawGrass(Color32[] p, int w, int h, int variant)
        {
            Color32[] greens = { C(111, 174, 74), C(107, 169, 70), C(116, 177, 77) };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    uint n = Hash(x, y, variant + 3) % 100;
                    p[y * w + x] = n < 8 ? C(83, 147, 58) : n < 17 ? C(139, 195, 91) : greens[variant % 3];
                }
            for (int i = 0; i < 4; i++)
            {
                int x = 2 + (int)(Hash(i, variant, 51) % 16);
                int y = 2 + (int)(Hash(i, variant, 81) % 16);
                Put(p, w, h, x, y, C(64, 131, 54));
                Put(p, w, h, x + 1, y + 1, C(155, 205, 95));
            }
        }

        private static void DrawPath(Color32[] p, int w, int h, int variant)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    uint n = Hash(x, y, variant + 41) % 100;
                    p[y * w + x] = n < 7 ? C(141, 157, 120) : n < 25 ? C(179, 190, 154) : C(160, 175, 139);
                }
            // Loose polygonal cobbles with moss in the mortar. The edges tile seamlessly.
            for (int row = -1; row < 4; row++)
                for (int col = -1; col < 4; col++)
                {
                    int cx = col * 8 + ((row & 1) == 0 ? 2 : 6) + variant * 2;
                    int cy = row * 7 + 3 + variant;
                    int radius = 3 + (int)(Hash(col, row, variant) % 2);
                    for (int dy = -radius; dy <= radius; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (Mathf.Abs(dx) + Mathf.Abs(dy) > radius + 2) continue;
                            uint noise = Hash(cx + dx, cy + dy, 93);
                            Color32 color = noise % 7 == 0 ? C(198, 205, 172) :
                                noise % 5 == 0 ? C(161, 172, 145) : C(185, 194, 163);
                            Put(p, w, h, cx + dx, cy + dy, color);
                        }
                }
        }

        private static void DrawTree(Color32[] p, int w, int h, int variant)
        {
            // Trunk and cast shadow sit below a cluster of overlapping leafy crowns.
            Ellipse(p, w, h, 32, 15, 20, 7, C(37, 76, 34, 115));
            Rect(p, w, h, 28, 10, 8, 29, C(71, 65, 44));
            Rect(p, w, h, 30, 12, 3, 27, C(111, 94, 61));
            Line(p, w, h, 29, 17, 22, 8, C(79, 68, 45));
            Line(p, w, h, 34, 17, 40, 9, C(75, 66, 44));
            int shift = variant == 0 ? 0 : variant == 1 ? -3 : 3;
            Ellipse(p, w, h, 31 + shift, 48, 25, 20, C(17, 65, 42));
            Ellipse(p, w, h, 19 + shift, 44, 15, 17, C(22, 78, 46));
            Ellipse(p, w, h, 44 + shift, 44, 16, 17, C(19, 70, 44));
            Ellipse(p, w, h, 32 + shift, 58, 18, 12, C(28, 89, 51));
            Ellipse(p, w, h, 20 + shift, 52, 10, 11, C(32, 96, 55));
            Ellipse(p, w, h, 44 + shift, 53, 11, 11, C(25, 83, 51));
            for (int i = 0; i < 210; i++)
            {
                int x = 6 + (int)(Hash(i, variant, 121) % 52);
                int y = 30 + (int)(Hash(i, variant, 133) % 38);
                if (Get(p, w, h, x, y).a == 0) continue;
                uint n = Hash(x, y, variant + 211);
                if (n % 5 == 0) Put(p, w, h, x, y, C(76, 140, 72));
                else if (n % 3 == 0) Put(p, w, h, x, y, C(47, 111, 59));
                else Put(p, w, h, x, y, C(34, 96, 56));
            }
            for (int i = 0; i < 14; i++)
            {
                int x = 10 + (int)(Hash(i, variant, 221) % 43);
                int y = 39 + (int)(Hash(i, variant, 231) % 25);
                if (Get(p, w, h, x, y).a == 0) continue;
                Rect(p, w, h, x, y, 2, 2, C(83, 148, 77));
            }
        }

        private static void DrawFlowers(Color32[] p, int w, int h, int variant)
        {
            Color32 bloom = variant == 0 ? C(249, 202, 224) :
                variant == 1 ? C(94, 130, 228) : C(247, 212, 74);
            for (int i = 0; i < 5; i++)
            {
                int x = 3 + (int)(Hash(i, variant, 310) % 14);
                int y = 4 + (int)(Hash(i, variant, 320) % 11);
                Put(p, w, h, x, y - 2, C(39, 107, 42));
                Put(p, w, h, x - 1, y - 1, C(49, 133, 50));
                Put(p, w, h, x + 1, y - 1, C(49, 133, 50));
                Put(p, w, h, x, y, C(250, 236, 171));
                Put(p, w, h, x - 1, y, bloom);
                Put(p, w, h, x + 1, y, bloom);
                Put(p, w, h, x, y - 1, bloom);
                Put(p, w, h, x, y + 1, bloom);
            }
        }

        private static void DrawStone(Color32[] p, int w, int h)
        {
            Ellipse(p, w, h, 10, 4, 8, 3, C(52, 78, 52, 125));
            Ellipse(p, w, h, 10, 9, 7, 6, C(80, 89, 70));
            Ellipse(p, w, h, 10, 11, 6, 5, C(151, 154, 127));
            Line(p, w, h, 5, 10, 8, 14, C(193, 190, 155));
            Line(p, w, h, 9, 15, 13, 14, C(206, 201, 169));
            Line(p, w, h, 13, 8, 15, 11, C(105, 114, 93));
        }

        private static void DrawFence(Color32[] p, int w, int h)
        {
            Rect(p, w, h, 0, 7, 20, 3, C(96, 73, 48));
            Rect(p, w, h, 0, 8, 20, 1, C(160, 128, 82));
            Rect(p, w, h, 0, 2, 20, 2, C(84, 64, 46));
            for (int x = 3; x < w; x += 10)
            {
                Rect(p, w, h, x, 3, 4, 14, C(94, 72, 48));
                Rect(p, w, h, x + 1, 4, 1, 12, C(150, 116, 72));
                Put(p, w, h, x + 1, 17, C(185, 151, 105));
                Put(p, w, h, x + 2, 18, C(169, 136, 92));
            }
        }

        private static void DrawMage(Color32[] p, int w, int h)
        {
            Ellipse(p, w, h, 16, 7, 11, 4, C(18, 34, 23, 115));
            // Hooded robe viewed from slightly above; the dark face aperture reads at game scale.
            Ellipse(p, w, h, 16, 12, 10, 8, C(37, 25, 55));
            Ellipse(p, w, h, 16, 17, 9, 11, C(71, 37, 93));
            Rect(p, w, h, 10, 8, 12, 11, C(51, 29, 73));
            Ellipse(p, w, h, 16, 22, 8, 8, C(102, 58, 128));
            Ellipse(p, w, h, 16, 20, 5, 5, C(23, 22, 34));
            Rect(p, w, h, 13, 24, 6, 2, C(143, 86, 165));
            Rect(p, w, h, 9, 14, 3, 9, C(98, 54, 117));
            Rect(p, w, h, 21, 14, 3, 9, C(89, 49, 110));
            Put(p, w, h, 14, 19, C(162, 177, 181));
            Put(p, w, h, 18, 19, C(162, 177, 181));
        }

        private static void DrawKnight(Color32[] p, int w, int h)
        {
            Ellipse(p, w, h, 16, 7, 11, 4, C(24, 33, 35, 115));
            Rect(p, w, h, 12, 5, 8, 6, C(54, 57, 66));
            Ellipse(p, w, h, 16, 16, 10, 10, C(55, 67, 75));
            Rect(p, w, h, 10, 12, 12, 10, C(125, 143, 152));
            Rect(p, w, h, 12, 13, 8, 9, C(190, 201, 201));
            Rect(p, w, h, 15, 13, 2, 8, C(63, 90, 109));
            Ellipse(p, w, h, 16, 24, 7, 6, C(95, 117, 130));
            Rect(p, w, h, 12, 23, 8, 4, C(181, 192, 194));
            Rect(p, w, h, 12, 21, 8, 2, C(35, 46, 52));
            Rect(p, w, h, 15, 27, 2, 3, C(182, 43, 49));
            Rect(p, w, h, 8, 14, 3, 9, C(109, 129, 141));
            Rect(p, w, h, 22, 14, 3, 9, C(109, 129, 141));
        }

        private static void Rect(Color32[] p, int w, int h, int x, int y, int width, int height, Color32 color)
        {
            for (int yy = y; yy < y + height; yy++)
                for (int xx = x; xx < x + width; xx++) Put(p, w, h, xx, yy, color);
        }

        private static void Ellipse(Color32[] p, int w, int h, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                    if ((x - cx) * (x - cx) * ry * ry + (y - cy) * (y - cy) * rx * rx <= rx * rx * ry * ry)
                        Put(p, w, h, x, y, color);
        }

        private static void Line(Color32[] p, int w, int h, int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Put(p, w, h, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int doubled = 2 * error;
                if (doubled >= dy) { error += dy; x0 += sx; }
                if (doubled <= dx) { error += dx; y0 += sy; }
            }
        }

        private static void Put(Color32[] p, int w, int h, int x, int y, Color32 color)
        {
            if (x >= 0 && x < w && y >= 0 && y < h) p[y * w + x] = color;
        }

        private static Color32 Get(Color32[] p, int w, int h, int x, int y)
        {
            return x >= 0 && x < w && y >= 0 && y < h ? p[y * w + x] : C(0, 0, 0, 0);
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
