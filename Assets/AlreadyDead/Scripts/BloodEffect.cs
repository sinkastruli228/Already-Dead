using UnityEngine;

namespace AlreadyDead
{
    public sealed class BloodEffect : MonoBehaviour
    {
        public enum BloodKind { Hit, Kill }

        private const int Size = 20;
        private static readonly Sprite[] hitSprites = new Sprite[3];
        private static readonly Sprite[] killSprites = new Sprite[3];
        private static Sprite dropSprite;
        private static Material bloodMaterial;
        private static int nextHitVariant;
        private static int nextKillVariant;

        private Transform[] drops;
        private SpriteRenderer[] dropRenderers;
        private Vector2[] dropVelocities;
        private SpriteRenderer decal;
        private float age;
        private float lifetime;

        public BloodKind Kind { get; private set; }
        public int Variant { get; private set; }

        public static BloodEffect SpawnHit(Vector2 position, Vector2 direction)
        {
            return Spawn(position, direction, BloodKind.Hit, nextHitVariant++ % hitSprites.Length);
        }

        public static BloodEffect SpawnKill(Vector2 position, Vector2 direction)
        {
            return Spawn(position, direction, BloodKind.Kill, nextKillVariant++ % killSprites.Length);
        }

        private static BloodEffect Spawn(Vector2 position, Vector2 direction, BloodKind kind, int variant)
        {
            EnsureAssets();
            var go = new GameObject(kind == BloodKind.Kill ? "Blood / death puddle" : "Blood / hit splash");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, variant * 90f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = kind == BloodKind.Kill ? killSprites[variant] : hitSprites[variant];
            renderer.sharedMaterial = bloodMaterial;
            renderer.sortingOrder = kind == BloodKind.Kill ? 7 : 9;
            renderer.color = Color.white;
            go.transform.localScale = kind == BloodKind.Kill ? Vector3.one * 1.3f : Vector3.one * 0.8f;

            BloodEffect effect = go.AddComponent<BloodEffect>();
            effect.Kind = kind;
            effect.Variant = variant;
            effect.decal = renderer;
            effect.lifetime = kind == BloodKind.Kill ? 30f : 10f;
            effect.CreateDrops(direction, kind == BloodKind.Kill ? 8 : 4);
            return effect;
        }

        private void CreateDrops(Vector2 direction, int count)
        {
            drops = new Transform[count];
            dropRenderers = new SpriteRenderer[count];
            dropVelocities = new Vector2[count];
            Vector2 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            for (int i = 0; i < count; i++)
            {
                var drop = new GameObject("Pixel blood drop");
                drop.transform.SetParent(transform, false);
                drop.transform.localPosition = Vector3.zero;
                drop.transform.localScale = Vector3.one * (i % 3 == 0 ? 0.09f : 0.06f);
                SpriteRenderer renderer = drop.AddComponent<SpriteRenderer>();
                renderer.sprite = dropSprite;
                renderer.sharedMaterial = bloodMaterial;
                renderer.sortingOrder = 16;
                renderer.color = i % 2 == 0 ? new Color(0.78f, 0.08f, 0.08f) :
                    new Color(0.46f, 0.035f, 0.04f);
                float angle = Mathf.Lerp(-85f, 85f, (i + 0.5f) / count) + Variant * 18f;
                dropVelocities[i] = (Quaternion.Euler(0f, 0f, angle) * forward) *
                    (Kind == BloodKind.Kill ? 2.4f : 1.5f);
                drops[i] = drop.transform;
                dropRenderers[i] = renderer;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < 0.34f)
            {
                float fade = 1f - age / 0.34f;
                for (int i = 0; i < drops.Length; i++)
                {
                    drops[i].position += (Vector3)(dropVelocities[i] * Time.deltaTime * fade);
                    Color color = dropRenderers[i].color;
                    color.a = fade;
                    dropRenderers[i].color = color;
                }
            }
            else if (drops.Length > 0)
            {
                for (int i = 0; i < drops.Length; i++) drops[i].gameObject.SetActive(false);
                drops = new Transform[0];
            }

            if (age > lifetime - 2f)
            {
                Color color = decal.color;
                color.a = Mathf.Clamp01((lifetime - age) / 2f);
                decal.color = color;
            }
            if (age >= lifetime) Destroy(gameObject);
        }

        private static void EnsureAssets()
        {
            if (bloodMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                bloodMaterial = new Material(shader);
            }
            for (int i = 0; i < 3; i++)
            {
                if (hitSprites[i] == null) hitSprites[i] = MakeSprite(false, i);
                if (killSprites[i] == null) killSprites[i] = MakeSprite(true, i);
            }
            if (dropSprite == null)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                dropSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2),
                    new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
            }
        }

        private static Sprite MakeSprite(bool kill, int variant)
        {
            var pixels = new Color32[Size * Size];
            Color32 dark = new Color32(96, 18, 22, 220);
            Color32 red = new Color32(163, 27, 29, 235);
            Color32 bright = new Color32(215, 51, 43, 240);
            int cx = variant == 1 ? 11 : 9;
            int cy = variant == 2 ? 9 : 10;
            int radiusX = kill ? (variant == 1 ? 8 : 7) : 3;
            int radiusY = kill ? (variant == 2 ? 5 : 6) : 2;
            for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    int noise = (x * 17 + y * 29 + variant * 37) % 7;
                    float shape = dx * dx / (float)(radiusX * radiusX) +
                        dy * dy / (float)(radiusY * radiusY);
                    if (shape > (kill ? 0.9f + noise * 0.035f : 1f)) continue;
                    pixels[y * Size + x] = shape > 0.72f ? dark :
                        (x + y + variant) % 5 == 0 ? bright : red;
                }

            if (kill)
            {
                Dot(pixels, variant == 0 ? 2 : 17, 5, red);
                Dot(pixels, variant == 2 ? 17 : 4, 16, dark);
                Dot(pixels, 15, variant == 1 ? 2 : 17, bright);
                if (variant == 2) { Dot(pixels, 2, 11, red); Dot(pixels, 18, 11, dark); }
            }
            else
            {
                Dot(pixels, variant == 1 ? 3 : 16, 12, bright);
                Dot(pixels, variant == 2 ? 16 : 5, 5, red);
                Dot(pixels, 11, variant == 0 ? 17 : 2, dark);
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size),
                new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
        }

        private static void Dot(Color32[] pixels, int x, int y, Color32 color)
        {
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                    pixels[(y + dy) * Size + x + dx] = color;
        }
    }
}
