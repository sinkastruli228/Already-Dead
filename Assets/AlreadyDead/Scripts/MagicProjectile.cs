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
            var go = new GameObject(kind + " magic bolt");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = primitiveSprite;
            renderer.sharedMaterial = primitiveMaterial;
            renderer.color = ColorFor(kind);
            renderer.sortingOrder = 30;
            go.transform.localScale = kind == MagicElement.Lightning
                ? new Vector3(0.48f, 0.12f, 1f) : new Vector3(0.26f, 0.26f, 1f);

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
                {
                    int damage = element == MagicElement.Fire ? 2 : 1;
                    victim.TakeMagicDamage(damage, element, direction);
                    if (element == MagicElement.Lightning)
                        Chain(hit.collider, hit.point);
                }
                else
                {
                    PatrolEnemy desertEnemy = hit.collider.GetComponentInParent<PatrolEnemy>();
                    if (desertEnemy != null) desertEnemy.TakeDamage(element == MagicElement.Fire ? 2 : 1, direction);
                }
                Impact(hit.point, element, sprite, material);
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(direction * distance);
            remaining -= seconds;
            if (remaining <= 0f) Destroy(gameObject);
        }

        private void Chain(Collider2D first, Vector2 source)
        {
            Collider2D[] nearby = Physics2D.OverlapCircleAll(source, tuning.lightningChainRange, tuning.enemyMask);
            Collider2D target = null;
            IMagicDamageable receiver = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < nearby.Length; i++)
            {
                Collider2D candidate = nearby[i];
                if (candidate == first || candidate.transform.root == first.transform.root) continue;
                IMagicDamageable possible = FindReceiver(candidate);
                if (possible == null) continue;
                Vector2 delta = (Vector2)candidate.bounds.center - source;
                float squared = delta.sqrMagnitude;
                if (squared >= best || Physics2D.Linecast(source, candidate.bounds.center, tuning.wallMask))
                    continue;
                best = squared;
                target = candidate;
                receiver = possible;
            }
            if (target == null) return;
            Vector2 end = target.bounds.center;
            receiver.TakeMagicDamage(1, MagicElement.Lightning, (end - source).normalized);
            DrawArc(source, end, sprite, material);
            Impact(end, MagicElement.Lightning, sprite, material);
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
            var burst = new GameObject(kind + " magic impact");
            burst.transform.position = position;
            burst.transform.localScale = kind == MagicElement.Fire
                ? new Vector3(0.55f, 0.55f, 1f) : new Vector3(0.42f, 0.42f, 1f);
            var renderer = burst.AddComponent<SpriteRenderer>();
            renderer.sprite = primitiveSprite;
            renderer.sharedMaterial = primitiveMaterial;
            renderer.color = ColorFor(kind);
            renderer.sortingOrder = 31;
            Destroy(burst, 0.14f);
        }

        private static void DrawArc(Vector2 start, Vector2 end, Sprite primitiveSprite,
            Material primitiveMaterial)
        {
            if (primitiveSprite == null) return;
            var arc = new GameObject("Lightning chain");
            Vector2 delta = end - start;
            arc.transform.position = (start + end) * 0.5f;
            arc.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            arc.transform.localScale = new Vector3(delta.magnitude, 0.07f, 1f);
            var renderer = arc.AddComponent<SpriteRenderer>();
            renderer.sprite = primitiveSprite;
            renderer.sharedMaterial = primitiveMaterial;
            renderer.color = ColorFor(MagicElement.Lightning);
            renderer.sortingOrder = 32;
            Destroy(arc, 0.12f);
        }

        public static Color ColorFor(MagicElement kind)
        {
            return kind == MagicElement.Fire ? new Color(1f, 0.37f, 0.12f) :
                kind == MagicElement.Frost ? new Color(0.41f, 0.9f, 1f) :
                new Color(1f, 0.92f, 0.38f);
        }
    }
}
