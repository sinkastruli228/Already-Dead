using System.Collections.Generic;
using UnityEngine;

namespace AlreadyDead
{
    public sealed class MagicProjectile : MonoBehaviour
    {
        private PrototypeTuning tuning;
        private MagicElement element;
        private Vector2 direction;
        private float remaining;
        private Sprite sprite;
        private Material material;

        public MagicElement Element => element;
        public Vector2 Direction => direction;

        public static MagicProjectile Spawn(Vector2 origin, Vector2 heading, MagicElement kind,
            PrototypeTuning settings, Sprite primitiveSprite, Material primitiveMaterial)
        {
            var go = new GameObject(kind == MagicElement.Fire ? "Pixel fireball" : "Pixel frost shard");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            BuildBoltVisual(go.transform, kind, primitiveSprite, primitiveMaterial);

            MagicProjectile projectile = go.AddComponent<MagicProjectile>();
            projectile.tuning = settings;
            projectile.element = kind;
            projectile.direction = heading.normalized;
            projectile.remaining = settings.magicProjectileLifetime;
            projectile.sprite = primitiveSprite;
            projectile.material = primitiveMaterial;
            return projectile;
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float seconds)
        {
            if (tuning == null) return;
            float distance = tuning.magicProjectileSpeed * Mathf.Min(seconds, remaining);
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, tuning.magicProjectileRadius,
                direction, distance, tuning.wallMask | tuning.enemyMask);
            if (hit)
            {
                IMagicDamageable victim = FindReceiver(hit.collider);
                if (victim != null)
                    victim.TakeMagicDamage(element == MagicElement.Fire ? 2 : 1, element, direction);
                Impact(hit.point, element, sprite, material);
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(direction * distance);
            remaining -= seconds;
            if (remaining <= 0f) Destroy(gameObject);
        }

        public static int CastLightning(Vector2 origin, Vector2 heading, PrototypeTuning settings,
            Sprite primitiveSprite, Material primitiveMaterial)
        {
            Vector2 direction = heading.sqrMagnitude > 0.001f ? heading.normalized : Vector2.right;
            RaycastHit2D hit = Physics2D.CircleCast(origin, settings.magicProjectileRadius,
                direction, settings.lightningRange, settings.wallMask | settings.enemyMask);
            Vector2 end = hit ? hit.point : origin + direction * settings.lightningRange;
            DrawArc(origin, end, primitiveSprite, primitiveMaterial);
            if (!hit)
            {
                Impact(end, MagicElement.Lightning, primitiveSprite, primitiveMaterial);
                return 0;
            }

            IMagicDamageable first = FindReceiver(hit.collider);
            if (first == null)
            {
                Impact(hit.point, MagicElement.Lightning, primitiveSprite, primitiveMaterial);
                return 0;
            }

            first.TakeMagicDamage(1, MagicElement.Lightning, direction);
            Impact(hit.point, MagicElement.Lightning, primitiveSprite, primitiveMaterial);
            var visited = new List<Transform> { hit.collider.transform.root };
            int hitCount = 1;
            Vector2 source = hit.collider.bounds.center;
            while (hitCount < settings.lightningMaxTargets)
            {
                Collider2D next = FindChainTarget(source, visited, settings);
                if (next == null) break;
                Vector2 target = next.bounds.center;
                IMagicDamageable receiver = FindReceiver(next);
                receiver.TakeMagicDamage(1, MagicElement.Lightning, (target - source).normalized);
                DrawArc(source, target, primitiveSprite, primitiveMaterial);
                Impact(target, MagicElement.Lightning, primitiveSprite, primitiveMaterial);
                visited.Add(next.transform.root);
                source = target;
                hitCount++;
            }
            return hitCount;
        }

        private static Collider2D FindChainTarget(Vector2 source, List<Transform> visited,
            PrototypeTuning settings)
        {
            Collider2D[] nearby = Physics2D.OverlapCircleAll(source,
                settings.lightningChainRange, settings.enemyMask);
            Collider2D bestTarget = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < nearby.Length; i++)
            {
                Collider2D candidate = nearby[i];
                if (visited.Contains(candidate.transform.root) || FindReceiver(candidate) == null) continue;
                Vector2 target = candidate.bounds.center;
                float distance = (target - source).sqrMagnitude;
                if (distance >= bestDistance || Physics2D.Linecast(source, target, settings.wallMask)) continue;
                bestDistance = distance;
                bestTarget = candidate;
            }
            return bestTarget;
        }

        private static IMagicDamageable FindReceiver(Collider2D collider)
        {
            MonoBehaviour[] components = collider.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
                if (components[i] is IMagicDamageable receiver) return receiver;
            return null;
        }

        public static void Impact(Vector2 position, MagicElement kind, Sprite primitiveSprite,
            Material primitiveMaterial)
        {
            if (primitiveSprite == null) return;
            var burst = new GameObject(kind + " pixel impact");
            burst.transform.position = position;
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.12f + (i % 2) * 0.09f);
                AddPixel(burst.transform, "Impact pixel " + i, offset,
                    Vector2.one * (i % 2 == 0 ? 0.13f : 0.09f),
                    i % 3 == 0 ? Color.white : ColorFor(kind), 31, primitiveSprite, primitiveMaterial);
            }
            Destroy(burst, 0.14f);
        }

        private static void BuildBoltVisual(Transform parent, MagicElement kind, Sprite primitiveSprite,
            Material primitiveMaterial)
        {
            if (primitiveSprite == null) return;
            if (kind == MagicElement.Fire)
            {
                AddPixel(parent, "Fire core", Vector2.zero, new Vector2(0.24f, 0.2f),
                    new Color(1f, 0.82f, 0.16f), 30, primitiveSprite, primitiveMaterial);
                AddPixel(parent, "Fire hot pixel", new Vector2(0.13f, 0f), new Vector2(0.13f, 0.14f),
                    Color.white, 31, primitiveSprite, primitiveMaterial);
                AddPixel(parent, "Fire tail A", new Vector2(-0.16f, 0.07f), new Vector2(0.16f, 0.09f),
                    new Color(1f, 0.35f, 0.06f), 29, primitiveSprite, primitiveMaterial);
                AddPixel(parent, "Fire tail B", new Vector2(-0.27f, -0.05f), new Vector2(0.12f, 0.08f),
                    new Color(0.85f, 0.12f, 0.03f), 29, primitiveSprite, primitiveMaterial);
            }
            else
            {
                Transform shard = AddPixel(parent, "Ice crystal", Vector2.zero, new Vector2(0.28f, 0.1f),
                    new Color(0.38f, 0.9f, 1f), 30, primitiveSprite, primitiveMaterial).transform;
                shard.localRotation = Quaternion.Euler(0f, 0f, 45f);
                AddPixel(parent, "Ice glint", new Vector2(0.07f, 0.07f), new Vector2(0.09f, 0.07f),
                    Color.white, 31, primitiveSprite, primitiveMaterial);
            }
        }

        private static void DrawArc(Vector2 start, Vector2 end, Sprite primitiveSprite,
            Material primitiveMaterial)
        {
            if (primitiveSprite == null) return;
            var arc = new GameObject("Pixel lightning chain");
            Vector2 delta = end - start;
            int segments = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / 0.32f), 6, 22);
            Vector2 normal = delta.sqrMagnitude > 0.001f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
            Vector2 previous = start;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 point = Vector2.Lerp(start, end, t);
                if (i < segments) point += normal * ((i % 2 == 0 ? -1f : 1f) * 0.09f);
                Vector2 segment = point - previous;
                GameObject pixel = AddPixel(arc.transform, "Lightning pixel " + i,
                    (previous + point) * 0.5f, new Vector2(segment.magnitude + 0.035f, 0.065f),
                    i % 3 == 0 ? Color.white : ColorFor(MagicElement.Lightning), 32,
                    primitiveSprite, primitiveMaterial);
                pixel.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                previous = point;
            }
            Destroy(arc, 0.16f);
        }

        private static GameObject AddPixel(Transform parent, string name, Vector2 position, Vector2 scale,
            Color color, int order, Sprite primitiveSprite, Material primitiveMaterial)
        {
            var pixel = new GameObject(name);
            pixel.transform.SetParent(parent, false);
            pixel.transform.localPosition = position;
            pixel.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = pixel.AddComponent<SpriteRenderer>();
            renderer.sprite = primitiveSprite;
            renderer.sharedMaterial = primitiveMaterial;
            renderer.color = color;
            renderer.sortingOrder = order;
            return pixel;
        }

        public static Color ColorFor(MagicElement kind)
        {
            return kind == MagicElement.Fire ? new Color(1f, 0.37f, 0.12f) :
                kind == MagicElement.Frost ? new Color(0.41f, 0.9f, 1f) :
                new Color(1f, 0.92f, 0.28f);
        }
    }
}
