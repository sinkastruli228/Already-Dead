using UnityEngine;

namespace AlreadyDead
{
    public sealed class Projectile : MonoBehaviour
    {
        private PrototypeTuning tuning;
        private Vector2 direction;
        private float remaining;
        private int damage = 1;
        private Sprite primitiveSprite;
        private Material primitiveMaterial;
        private int maxRicochets;
        private int ricochetsRemaining;
        private bool hostile;
        private Transform attacker;
        public Vector2 Direction => direction;
        public int RicochetsRemaining => ricochetsRemaining;

        public static Projectile Spawn(Vector2 origin, Vector2 heading, PrototypeTuning settings,
            Sprite sprite, Material material, int hitDamage = 1, string projectileName = "Pistol bullet",
            int maxRicochets = 0, bool hostileToPlayer = false, Transform source = null)
        {
            var go = new GameObject(projectileName);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            var renderer = go.AddComponent<SpriteRenderer>();
            FirearmVisuals visuals = FirearmVisuals.Current;
            bool hasBulletArt = visuals != null && visuals.Bullet != null;
            renderer.sprite = hasBulletArt ? visuals.Bullet : sprite;
            renderer.sharedMaterial = hasBulletArt && visuals.BulletMaterial != null
                ? visuals.BulletMaterial : material;
            renderer.color = hasBulletArt ? Color.white : new Color(1f, 0.88f, 0.48f);
            renderer.sortingOrder = 15;
            go.transform.localScale = hasBulletArt
                ? new Vector3(0.42f, 0.2f, 1f)
                : new Vector3(0.38f, 0.065f, 1f);
            Projectile bullet = go.AddComponent<Projectile>();
            bullet.tuning = settings;
            bullet.direction = heading.normalized;
            bullet.maxRicochets = Mathf.Max(0, maxRicochets);
            bullet.ricochetsRemaining = bullet.maxRicochets;
            bullet.hostile = hostileToPlayer;
            bullet.attacker = source;
            bullet.remaining = bullet.maxRicochets > 0
                ? Mathf.Max(12f, settings.bulletLifetime) : settings.bulletLifetime;
            bullet.damage = Mathf.Max(1, hitDamage);
            bullet.primitiveSprite = sprite;
            bullet.primitiveMaterial = material;
            return bullet;
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float seconds)
        {
            if (maxRicochets > 0)
            {
                StepWithRicochets(seconds);
                return;
            }
            float distance = tuning.bulletSpeed * Mathf.Min(seconds, remaining);
            // Swept collision, not just overlaps: fast bullets cannot skip thin walls.
            int mask = (int)tuning.wallMask | (hostile ? 1 << 10 : (int)tuning.enemyMask);
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, tuning.bulletRadius,
                direction, distance, mask);
            if (hit)
            {
                PushDoor2D door = hit.collider.GetComponentInParent<PushDoor2D>();
                if (door != null)
                    door.PushFrom(transform.position, direction, 1.8f);
                if (hostile)
                {
                    TopDownPlayer player = hit.collider.GetComponentInParent<TopDownPlayer>();
                    if (player != null) player.Vitality.TakeHit(damage, attacker);
                    else ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
                }
                else
                {
                    PatrolEnemy enemy = hit.collider.GetComponentInParent<PatrolEnemy>();
                    if (enemy != null) enemy.TakeDamage(damage, direction);
                    else if (hit.collider.GetComponentInParent<FantasyEnemy>() is FantasyEnemy fantasyEnemy)
                        fantasyEnemy.TakeDamage(damage, direction);
                    else ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
                }
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

        private void StepWithRicochets(float seconds)
        {
            float travel = tuning.bulletSpeed * Mathf.Max(0f, Mathf.Min(seconds, remaining));
            Vector2 position = transform.position;
            // A fast bullet can reach more than one wall in a single physics step.
            for (int collision = 0; collision < 8 && travel > 0.001f; collision++)
            {
                RaycastHit2D hit = Physics2D.CircleCast(position, tuning.bulletRadius,
                    direction, travel, tuning.wallMask | tuning.enemyMask);
                if (!hit)
                {
                    position += direction * travel;
                    break;
                }

                PatrolEnemy enemy = hit.collider.GetComponentInParent<PatrolEnemy>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage, direction);
                    Vanish();
                    return;
                }
                FantasyEnemy fantasyEnemy = hit.collider.GetComponentInParent<FantasyEnemy>();
                if (fantasyEnemy != null)
                {
                    fantasyEnemy.TakeDamage(damage, direction);
                    Vanish();
                    return;
                }

                Vector2 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -direction;
                PushDoor2D door = hit.collider.GetComponentInParent<PushDoor2D>();
                if (door != null) door.PushFrom(position, direction, 1.8f);
                ShotEffect.Impact(hit.point, normal, primitiveSprite, primitiveMaterial);
                if (ricochetsRemaining == 0)
                {
                    Vanish();
                    return;
                }

                position += direction * hit.distance;
                position += normal * (tuning.bulletRadius + 0.02f);
                travel = Mathf.Max(0f, travel - hit.distance - 0.02f);
                direction = Vector2.Reflect(direction, normal).normalized;
                ricochetsRemaining--;
                transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
            transform.position = position;
            remaining -= seconds;
            if (remaining <= 0f) Vanish();
        }

        private void Vanish()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
