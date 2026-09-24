using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlreadyDead.Editor
{
    // Small, deterministic sprites: one source pixel remains one crisp pixel in Unity.
    internal static class BuildingPixelArt
    {
        internal const string Folder = "Assets/AlreadyDead/BuildingKit/Art";
        internal static readonly string[] Names =
        {
            "Parquet", "BeigeWall", "Asphalt", "Curb", "ParkingLine", "Doorway",
            "LeatherSofaBrown", "LeatherSofaTan", "Fridge", "Stool", "CoffeeTable",
            "DiningTable", "Cabinet", "PottedPlant", "TrashBin", "CarRed", "CarBlue",
            "Streetlamp", "CeilingLamp", "ReceptionDesk", "IVStand",
            "HospitalBedCream", "HospitalBedTeal", "ScatteredPapers", "MedicalWasteBin"
        };

        internal static Sprite Ensure(string name, bool refreshArt = false)
        {
            Dimensions(name, out int width, out int height);
            Directory.CreateDirectory(Folder);
            string path = Folder + "/" + name + ".png";
            if (refreshArt || !File.Exists(path))
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
            width = 32;
            height = 32;
            switch (name)
            {
                case "LeatherSofaBrown":
                case "LeatherSofaTan": width = 64; height = 40; break;
                case "CarRed":
                case "CarBlue": width = 72; height = 40; break;
                case "Fridge": width = 32; height = 48; break;
                case "ReceptionDesk": width = 96; height = 48; break;
                case "IVStand": width = height = 32; break;
                case "HospitalBedCream":
                case "HospitalBedTeal": width = 48; height = 80; break;
                case "ScatteredPapers": width = 64; height = 48; break;
                case "Stool":
                case "PottedPlant":
                case "TrashBin":
                case "CeilingLamp":
                case "MedicalWasteBin": width = height = 24; break;
                case "CoffeeTable": width = 48; height = 28; break;
                case "DiningTable": width = 56; height = 40; break;
                case "Cabinet": width = 48; height = 28; break;
                case "Streetlamp": width = height = 32; break;
            }
        }

        private static void Paint(string name, Color32[] p, int w, int h)
        {
            switch (name)
            {
                case "Parquet":
                    Fill(p, w, h, C(143, 91, 48));
                    for (int row = 0; row < 4; row++)
                    {
                        int y = row * 8;
                        Rect(p, w, h, 0, y, w, 1, C(77, 45, 30));
                        int seam = (row & 1) == 0 ? 13 : 25;
                        Rect(p, w, h, seam, y, 1, 8, C(82, 48, 31));
                        Rect(p, w, h, 1, y + 1, w - 2, 1, C(184, 124, 69));
                        Rect(p, w, h, 2, y + 5, w - 4, 1, C(111, 67, 39));
                    }
                    for (int i = 0; i < 35; i++)
                    {
                        int x = (int)(Hash(i, 19) % (uint)w);
                        int y = (int)(Hash(i, 31) % (uint)h);
                        Put(p, w, h, x, y, C(172, 110, 58));
                    }
                    break;
                case "BeigeWall":
                    Fill(p, w, h, C(214, 198, 167));
                    Rect(p, w, h, 0, 0, w, 5, C(157, 133, 105));
                    Rect(p, w, h, 0, 5, w, 2, C(241, 225, 190));
                    Rect(p, w, h, 0, 28, w, 4, C(236, 219, 184));
                    Rect(p, w, h, 0, 26, w, 2, C(174, 149, 119));
                    Rect(p, w, h, 0, 8, 1, 18, C(190, 171, 141));
                    break;
                case "Asphalt":
                    Fill(p, w, h, C(38, 43, 54));
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            uint n = Hash(x + y * w, 7) % 23;
                            if (n == 0) Put(p, w, h, x, y, C(58, 62, 72));
                            else if (n == 1) Put(p, w, h, x, y, C(29, 35, 46));
                        }
                    break;
                case "Curb":
                    Fill(p, w, h, C(68, 75, 80));
                    Rect(p, w, h, 0, 7, w, 19, C(132, 139, 134));
                    Rect(p, w, h, 0, 25, w, 3, C(181, 184, 171));
                    Rect(p, w, h, 0, 7, w, 2, C(54, 59, 64));
                    Rect(p, w, h, 15, 9, 2, 16, C(79, 86, 88));
                    break;
                case "ParkingLine":
                    Rect(p, w, h, 14, 0, 4, h, C(216, 206, 153));
                    Rect(p, w, h, 15, 0, 2, h, C(252, 240, 192));
                    break;
                case "Doorway":
                    Rect(p, w, h, 0, 0, 5, h, C(115, 84, 60));
                    Rect(p, w, h, w - 5, 0, 5, h, C(115, 84, 60));
                    Rect(p, w, h, 5, h - 6, w - 10, 6, C(180, 143, 98));
                    Rect(p, w, h, 6, h - 4, w - 12, 2, C(231, 196, 141));
                    break;
                case "LeatherSofaBrown": PaintSofa(p, w, h, C(86, 45, 32), C(139, 76, 48)); break;
                case "LeatherSofaTan": PaintSofa(p, w, h, C(126, 78, 50), C(190, 128, 77)); break;
                case "Fridge":
                    Shadow(p, w, h, 16, 21, 15, 22);
                    Box(p, w, h, 2, 2, 28, 44, C(45, 58, 65), C(191, 207, 207));
                    Box(p, w, h, 5, 5, 22, 38, C(132, 154, 158), C(224, 233, 225));
                    Rect(p, w, h, 8, 35, 16, 4, C(198, 215, 211));
                    Rect(p, w, h, 8, 11, 16, 2, C(136, 158, 160));
                    Rect(p, w, h, 21, 16, 2, 9, C(116, 137, 141));
                    break;
                case "Stool":
                    Shadow(p, w, h, 12, 10, 11, 10);
                    Rect(p, w, h, 2, 2, 3, 3, C(50, 46, 43));
                    Rect(p, w, h, 19, 2, 3, 3, C(50, 46, 43));
                    Rect(p, w, h, 2, 19, 3, 3, C(50, 46, 43));
                    Rect(p, w, h, 19, 19, 3, 3, C(50, 46, 43));
                    Ellipse(p, w, h, 12, 12, 10, 10, C(57, 38, 32));
                    Ellipse(p, w, h, 12, 12, 8, 8, C(155, 99, 58));
                    Ellipse(p, w, h, 11, 14, 5, 4, C(184, 124, 70));
                    break;
                case "CoffeeTable":
                    Shadow(p, w, h, 24, 11, 22, 12);
                    Box(p, w, h, 2, 3, 44, 23, C(49, 34, 31), C(140, 87, 53));
                    Rect(p, w, h, 5, 20, 38, 3, C(192, 132, 80));
                    Rect(p, w, h, 7, 7, 34, 2, C(104, 64, 43));
                    Ellipse(p, w, h, 31, 14, 5, 5, C(61, 46, 39));
                    Ellipse(p, w, h, 31, 14, 3, 3, C(229, 214, 179));
                    break;
                case "DiningTable":
                    Shadow(p, w, h, 28, 14, 26, 15);
                    Box(p, w, h, 3, 4, 50, 32, C(59, 38, 30), C(151, 99, 59));
                    Rect(p, w, h, 6, 30, 44, 3, C(199, 145, 89));
                    Rect(p, w, h, 9, 9, 38, 2, C(113, 69, 43));
                    Ellipse(p, w, h, 16, 20, 6, 5, C(225, 212, 180));
                    Ellipse(p, w, h, 39, 20, 6, 5, C(225, 212, 180));
                    Ellipse(p, w, h, 16, 20, 3, 2, C(174, 183, 161));
                    Ellipse(p, w, h, 39, 20, 3, 2, C(174, 183, 161));
                    break;
                case "Cabinet":
                    Shadow(p, w, h, 24, 11, 22, 11);
                    Box(p, w, h, 2, 3, 44, 23, C(45, 31, 28), C(105, 76, 58));
                    Box(p, w, h, 5, 6, 38, 17, C(67, 46, 34), C(153, 111, 75));
                    Rect(p, w, h, 8, 17, 32, 2, C(191, 148, 101));
                    Rect(p, w, h, 8, 9, 32, 1, C(118, 81, 52));
                    break;
                case "PottedPlant":
                    Shadow(p, w, h, 12, 10, 10, 10);
                    Ellipse(p, w, h, 12, 12, 10, 10, C(91, 55, 38));
                    Ellipse(p, w, h, 12, 12, 8, 8, C(145, 92, 55));
                    Ellipse(p, w, h, 12, 12, 6, 6, C(37, 54, 36));
                    Ellipse(p, w, h, 8, 15, 5, 5, C(49, 113, 61));
                    Ellipse(p, w, h, 16, 15, 5, 5, C(35, 89, 52));
                    Ellipse(p, w, h, 12, 9, 5, 5, C(76, 139, 68));
                    Put(p, w, h, 9, 17, C(139, 188, 93));
                    break;
                case "TrashBin":
                    Shadow(p, w, h, 12, 10, 10, 10);
                    Ellipse(p, w, h, 12, 12, 10, 10, C(33, 45, 50));
                    Ellipse(p, w, h, 12, 12, 8, 8, C(113, 134, 135));
                    Ellipse(p, w, h, 12, 12, 6, 6, C(28, 39, 43));
                    Rect(p, w, h, 8, 16, 7, 1, C(171, 187, 177));
                    break;
                case "CarRed": PaintCar(p, w, h, C(104, 35, 43), C(191, 58, 59)); break;
                case "CarBlue": PaintCar(p, w, h, C(34, 62, 91), C(69, 126, 159)); break;
                case "Streetlamp":
                    Shadow(p, w, h, 16, 14, 13, 13);
                    Ellipse(p, w, h, 16, 16, 11, 11, C(40, 50, 58));
                    Ellipse(p, w, h, 16, 16, 8, 8, C(141, 153, 146));
                    Ellipse(p, w, h, 16, 16, 6, 6, C(255, 221, 143));
                    Ellipse(p, w, h, 15, 18, 3, 3, C(255, 244, 195));
                    break;
                case "CeilingLamp":
                    Ellipse(p, w, h, 12, 12, 10, 10, C(94, 82, 67));
                    Ellipse(p, w, h, 12, 12, 8, 8, C(231, 199, 129));
                    Ellipse(p, w, h, 12, 12, 6, 6, C(255, 244, 191));
                    break;
                case "ReceptionDesk": PaintReception(p, w, h); break;
                case "IVStand": PaintIVStand(p, w, h); break;
                case "HospitalBedCream": PaintHospitalBed(p, w, h, C(215, 212, 195)); break;
                case "HospitalBedTeal": PaintHospitalBed(p, w, h, C(79, 151, 151)); break;
                case "ScatteredPapers": PaintScatteredPapers(p, w, h); break;
                case "MedicalWasteBin":
                    Shadow(p, w, h, 12, 10, 10, 10);
                    Box(p, w, h, 2, 2, 20, 20, C(56, 72, 80), C(204, 217, 216));
                    Box(p, w, h, 5, 5, 14, 14, C(135, 157, 159), C(46, 60, 64));
                    Rect(p, w, h, 7, 18, 10, 2, C(190, 64, 62));
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(name), name, null);
            }
        }

        private static void PaintSofa(Color32[] p, int w, int h, Color32 dark, Color32 leather)
        {
            Shadow(p, w, h, w / 2, 18, 30, 17);
            Box(p, w, h, 2, 3, 60, 34, C(39, 27, 25), dark);
            Box(p, w, h, 8, 8, 48, 22, dark, leather);
            Rect(p, w, h, 9, 30, 46, 5, leather);
            Rect(p, w, h, 31, 10, 2, 18, dark);
            Rect(p, w, h, 3, 8, 7, 23, leather);
            Rect(p, w, h, 54, 8, 7, 23, leather);
            Rect(p, w, h, 12, 22, 17, 2, C(201, 142, 91));
            Rect(p, w, h, 35, 22, 17, 2, C(201, 142, 91));
        }

        private static void PaintReception(Color32[] p, int w, int h)
        {
            Shadow(p, w, h, 48, 17, 45, 20);
            Box(p, w, h, 3, 4, 90, 40, C(42, 34, 32), C(93, 66, 54));
            Rect(p, w, h, 23, 5, 67, 16, C(0, 0, 0, 0));
            Box(p, w, h, 4, 24, 88, 17, C(52, 38, 31), C(178, 132, 87));
            Box(p, w, h, 4, 5, 18, 35, C(52, 38, 31), C(169, 122, 80));
            Rect(p, w, h, 7, 35, 81, 3, C(211, 169, 117));
            Rect(p, w, h, 7, 8, 3, 28, C(203, 156, 103));
            Box(p, w, h, 51, 27, 18, 11, C(39, 48, 55), C(74, 94, 104));
            Rect(p, w, h, 54, 34, 12, 2, C(130, 166, 178));
            Rect(p, w, h, 54, 22, 15, 2, C(211, 204, 177));
        }

        private static void PaintIVStand(Color32[] p, int w, int h)
        {
            Shadow(p, w, h, 16, 14, 13, 12);
            Rect(p, w, h, 15, 5, 2, 22, C(62, 77, 80));
            Rect(p, w, h, 6, 15, 20, 2, C(80, 98, 101));
            Rect(p, w, h, 15, 6, 2, 20, C(174, 192, 190));
            Ellipse(p, w, h, 16, 16, 4, 4, C(62, 80, 85));
            Ellipse(p, w, h, 16, 16, 2, 2, C(216, 225, 219));
            Box(p, w, h, 3, 18, 8, 11, C(97, 126, 135), C(227, 236, 229));
            Box(p, w, h, 21, 18, 8, 11, C(97, 126, 135), C(227, 236, 229));
            Rect(p, w, h, 6, 20, 2, 5, C(147, 191, 199));
            Rect(p, w, h, 24, 20, 2, 5, C(147, 191, 199));
            Put(p, w, h, 6, 17, C(184, 72, 66));
            Put(p, w, h, 25, 17, C(184, 72, 66));
        }

        private static void PaintHospitalBed(Color32[] p, int w, int h, Color32 blanket)
        {
            Shadow(p, w, h, 24, 38, 22, 37);
            Rect(p, w, h, 5, 3, 5, 5, C(43, 52, 57));
            Rect(p, w, h, 38, 3, 5, 5, C(43, 52, 57));
            Rect(p, w, h, 5, 72, 5, 5, C(43, 52, 57));
            Rect(p, w, h, 38, 72, 5, 5, C(43, 52, 57));
            Box(p, w, h, 5, 5, 38, 70, C(53, 74, 82), C(204, 216, 215));
            Box(p, w, h, 9, 9, 30, 62, C(124, 151, 155), C(236, 235, 220));
            Rect(p, w, h, 10, 10, 28, 36, blanket);
            Rect(p, w, h, 11, 44, 26, 3, C(167, 185, 181));
            Box(p, w, h, 12, 53, 24, 13, C(179, 193, 190), C(249, 248, 234));
            Rect(p, w, h, 2, 18, 3, 45, C(189, 210, 210));
            Rect(p, w, h, 43, 18, 3, 45, C(189, 210, 210));
            Rect(p, w, h, 3, 24, 2, 5, C(92, 120, 128));
            Rect(p, w, h, 43, 24, 2, 5, C(92, 120, 128));
            Rect(p, w, h, 7, 69, 34, 3, C(112, 141, 147));
        }

        private static void PaintScatteredPapers(Color32[] p, int w, int h)
        {
            Paper(p, w, h, 15, 16, -17f);
            Paper(p, w, h, 46, 17, 14f);
            Paper(p, w, h, 30, 33, -34f);
        }

        private static void Paper(Color32[] p, int w, int h, int cx, int cy, float angle)
        {
            float cosine = Mathf.Cos(angle * Mathf.Deg2Rad);
            float sine = Mathf.Sin(angle * Mathf.Deg2Rad);
            for (int pass = 0; pass < 2; pass++)
                for (int y = -16; y <= 16; y++)
                    for (int x = -16; x <= 16; x++)
                {
                    float localX = x * cosine + y * sine;
                    float localY = -x * sine + y * cosine;
                    if (Mathf.Abs(localX) > 8f || Mathf.Abs(localY) > 11f) continue;
                    if (pass == 0)
                    {
                        Put(p, w, h, cx + x + 1, cy + y - 1, C(23, 30, 36, 90));
                        continue;
                    }
                    Color32 color = Mathf.Abs(localX) > 7f || Mathf.Abs(localY) > 10f
                        ? C(126, 140, 143) : C(238, 236, 217);
                    if (localX > -5f && localX < 5f &&
                        (Mathf.Abs(localY - 6f) < 0.6f || Mathf.Abs(localY - 2f) < 0.6f ||
                         Mathf.Abs(localY + 2f) < 0.6f || Mathf.Abs(localY + 6f) < 0.6f))
                        color = C(148, 166, 174);
                    Put(p, w, h, cx + x, cy + y, color);
                }
        }

        private static void PaintCar(Color32[] p, int w, int h, Color32 dark, Color32 paint)
        {
            Shadow(p, w, h, 36, 13, 33, 14);
            Rect(p, w, h, 10, 2, 15, 5, C(15, 19, 27));
            Rect(p, w, h, 47, 2, 15, 5, C(15, 19, 27));
            Rect(p, w, h, 10, 33, 15, 5, C(15, 19, 27));
            Rect(p, w, h, 47, 33, 15, 5, C(15, 19, 27));
            Box(p, w, h, 3, 6, 66, 29, C(17, 23, 32), dark);
            Rect(p, w, h, 8, 8, 55, 25, paint);
            Box(p, w, h, 22, 10, 31, 21, dark, C(67, 108, 128));
            Rect(p, w, h, 24, 21, 27, 7, C(111, 157, 169));
            Rect(p, w, h, 34, 11, 2, 18, C(19, 40, 52));
            Rect(p, w, h, 6, 10, 4, 5, C(247, 222, 140));
            Rect(p, w, h, 6, 26, 4, 5, C(247, 222, 140));
            Rect(p, w, h, 62, 10, 4, 5, C(149, 24, 32));
            Rect(p, w, h, 62, 26, 4, 5, C(149, 24, 32));
        }

        private static void Fill(Color32[] p, int w, int h, Color32 color) => Rect(p, w, h, 0, 0, w, h, color);

        private static void Box(Color32[] p, int w, int h, int x, int y, int width, int height,
            Color32 border, Color32 body)
        {
            Rect(p, w, h, x, y, width, height, border);
            Rect(p, w, h, x + 2, y + 2, width - 4, height - 4, body);
        }

        private static void Shadow(Color32[] p, int w, int h, int cx, int cy, int rx, int ry) =>
            Ellipse(p, w, h, cx, cy, rx, ry, C(13, 17, 22, 115));

        private static void Ellipse(Color32[] p, int w, int h, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                    if ((x - cx) * (x - cx) * ry * ry + (y - cy) * (y - cy) * rx * rx <= rx * rx * ry * ry)
                        Put(p, w, h, x, y, color);
        }

        private static void Rect(Color32[] p, int w, int h, int x, int y, int width, int height, Color32 color)
        {
            for (int yy = y; yy < y + height; yy++)
                for (int xx = x; xx < x + width; xx++) Put(p, w, h, xx, yy, color);
        }

        private static void Put(Color32[] p, int w, int h, int x, int y, Color32 color)
        {
            if (x >= 0 && x < w && y >= 0 && y < h) p[y * w + x] = color;
        }

        private static uint Hash(int value, int seed)
        {
            unchecked
            {
                uint n = (uint)(value * 73856093 ^ seed * 19349663);
                n ^= n >> 13;
                n *= 1274126177u;
                return n ^ (n >> 16);
            }
        }

        private static Color32 C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);
    }
}
