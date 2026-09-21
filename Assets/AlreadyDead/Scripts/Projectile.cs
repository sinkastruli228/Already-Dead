using UnityEngine;

namespace AlreadyDead
{
    public sealed class Projectile : MonoBehaviour
    {
        private PrototypeTuning tuning;
        private Vector2 direction;
        private float remaining;
        private Sprite primitiveSprite;
        private Material primitiveMaterial;
        public Vector2 Direction => direction;

        public static Projectile Spawn(Vector2 origin, Vector2 heading, PrototypeTuning settings,
            Sprite sprite, Material material)
        {
            var go = new GameObject("Pistol bullet");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = new Color(1f, 0.88f, 0.48f);
            renderer.sortingOrder = 15;
            go.transform.localScale = new Vector3(0.38f, 0.065f, 1f);
            Projectile bullet = go.AddComponent<Projectile>();
            bullet.tuning = settings;
            bullet.direction = heading.normalized;
            bullet.remaining = settings.bulletLifetime;
            bullet.primitiveSprite = sprite;
            bullet.primitiveMaterial = material;
            return bullet;
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float seconds)
        {
            float distance = tuning.bulletSpeed * Mathf.Min(seconds, remaining);
            // Swept collision, not just overlaps: fast bullets cannot skip thin walls.
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, tuning.bulletRadius,
                direction, distance, tuning.wallMask | tuning.enemyMask);
            if (hit)
            {
                PatrolEnemy enemy = hit.collider.GetComponentInParent<PatrolEnemy>();
                if (enemy != null) enemy.TakeDamage(1, direction);
                else ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            transform.position += (Vector3)(direction * distance);
            remaining -= seconds;
            if (remaining <= 0f)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
    }
}
