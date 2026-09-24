using UnityEngine;

namespace AlreadyDead
{
    public enum FantasyEnemyKind { Knight, Mage }

    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class FantasyEnemy : MonoBehaviour, IPunchReceiver, ISpearReceiver, IMagicDamageable,
        IEnemyAttractionListener
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private TopDownPlayer player;
        [SerializeField] private Transform facing;
        [SerializeField] private SpriteRenderer alert;
        [SerializeField] private Vector2 patrolA;
        [SerializeField] private Vector2 patrolB;
        [SerializeField] private FantasyEnemyKind kind;
        [SerializeField] private MagicElement mageElement;
        [SerializeField] private Sprite boltSprite;
        [SerializeField] private Material boltMaterial;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private Vector2 movement;
        private Vector2 detourTarget;
        private bool hasDetour;
        private Vector2 investigationTarget;
        private bool hasInvestigation;
        private bool reachedInvestigation;
        private bool deathSearch;
        private Vector2 deathSearchFacing = Vector2.right;
        private float investigationExpiresAt;
        private float investigationEndsAt;
        private bool towardB = true;
        private float nextAttackTime;
        private float slowedUntil;
        private float stunnedUntil;
        private int health;

        public FantasyEnemyKind Kind => kind;
        public bool Alerted { get; private set; }
        public bool IsAlive => health > 0;
        public int Health => health;
        public int AttacksMade { get; private set; }
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public Transform Facing => facing;
        public float SpeedMultiplier => Time.time < slowedUntil ? tuning.frostSlowMultiplier : 1f;
        public bool Investigating => hasInvestigation;
        public Vector2 InvestigationTarget => investigationTarget;
        public bool SearchingAfterDeath => deathSearch;

        public void Configure(PrototypeTuning settings, TopDownPlayer target, Transform visual,
            SpriteRenderer indicator, Vector2 first, Vector2 second, FantasyEnemyKind enemyKind,
            MagicElement element = MagicElement.Fire, Sprite projectileSprite = null,
            Material projectileMaterial = null)
        {
            tuning = settings;
            player = target;
            facing = visual;
            alert = indicator;
            patrolA = first;
            patrolB = second;
            kind = enemyKind;
            mageElement = element;
            boltSprite = projectileSprite;
            boltMaterial = projectileMaterial;
            health = 1;
            nextAttackTime = Time.time;
            Alerted = false;
            hasDetour = false;
            hasInvestigation = false;
            deathSearch = false;
            if (alert != null) alert.enabled = false;
            Face(patrolB - patrolA);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (tuning != null)
                health = 1;
            if (alert != null) alert.enabled = false;
        }

        private void Update()
        {
            if (!IsAlive || tuning == null || player == null || !player.IsAlive)
            {
                movement = Vector2.zero;
                return;
            }

            Vector2 position = Body.position;
            Vector2 toPlayer = (Vector2)player.transform.position - position;
            float distance = toPlayer.magnitude;
            if (!Alerted && CanSeePlayer()) AlertToPlayer();

            if (Time.time < stunnedUntil)
            {
                movement = Vector2.zero;
                return;
            }

            if (Alerted)
            {
                Face(toPlayer);
                bool clearShot = !Physics2D.Linecast(position, player.transform.position, tuning.wallMask);
                float attackRange = kind == FantasyEnemyKind.Knight ? tuning.enemyAttackRange : 5.4f;
                if (distance <= attackRange && clearShot)
                {
                    // Magi reposition when the player gets too close; knights stop to strike.
                    movement = kind == FantasyEnemyKind.Mage && distance < 2f
                        ? -toPlayer.normalized * tuning.enemyPatrolSpeed * SpeedMultiplier
                        : Vector2.zero;
                    if (Time.time >= nextAttackTime) Attack(toPlayer.normalized);
                }
                else
                {
                    movement = PathDirection(position, player.transform.position) * tuning.enemyChaseSpeed
                        * (kind == FantasyEnemyKind.Mage ? 0.85f : 1f) * SpeedMultiplier;
                }
                return;
            }

            if (hasInvestigation)
            {
                Vector2 investigationPath = investigationTarget - position;
                if (investigationPath.magnitude <= Mathf.Max(tuning.enemyInvestigationDistance,
                        tuning.enemyWaypointTolerance))
                {
                    movement = Vector2.zero;
                    Face(investigationPath);
                    if (!reachedInvestigation)
                    {
                        reachedInvestigation = true;
                        investigationEndsAt = Time.time + (deathSearch
                            ? tuning.enemyDeathSearchDuration : tuning.enemyInvestigationDuration);
                    }
                    if (Time.time < investigationEndsAt)
                    {
                        if (deathSearch) ScanForPlayer();
                        return;
                    }
                    hasInvestigation = false;
                    deathSearch = false;
                    hasDetour = false;
                }
                else if (Time.time < investigationExpiresAt)
                {
                    movement = PathDirection(position, investigationTarget) *
                        (deathSearch ? tuning.enemyChaseSpeed * 0.75f : tuning.enemyPatrolSpeed) *
                        SpeedMultiplier;
                    Face(movement);
                    return;
                }
                else
                {
                    hasInvestigation = false;
                    deathSearch = false;
                    hasDetour = false;
                }
            }

            Vector2 waypoint = towardB ? patrolB : patrolA;
            Vector2 path = waypoint - position;
            if (path.magnitude <= tuning.enemyWaypointTolerance)
            {
                towardB = !towardB;
                path = (towardB ? patrolB : patrolA) - position;
            }
            movement = path.sqrMagnitude > 0.0001f
                ? path.normalized * tuning.enemyPatrolSpeed * SpeedMultiplier : Vector2.zero;
            Face(path);
        }

        private void FixedUpdate() => Body.linearVelocity = IsAlive ? movement : Vector2.zero;

        public bool CanSeePlayer()
        {
            if (!IsAlive || tuning == null || player == null || !player.IsAlive) return false;
            Vector2 from = Body.position;
            Vector2 to = (Vector2)player.transform.position - from;
            float range = tuning.enemyVisionRange;
            if (to.sqrMagnitude > range * range) return false;
            if (to.sqrMagnitude < 0.0001f) return true;
            if (facing != null && Vector2.Angle(facing.right, to) > tuning.enemyVisionHalfAngle)
                return false;
            return !Physics2D.Linecast(from, player.transform.position, tuning.wallMask);
        }

        private void AlertToPlayer()
        {
            if (!IsAlive || player == null || !player.IsAlive) return;
            Alerted = true;
            hasInvestigation = false;
            deathSearch = false;
            hasDetour = false;
        }

        public void ReceivePunch(Vector2 direction, float force) => TakeDamage(1, direction);
        public void ReceiveSpear(Vector2 direction, float force) => TakeDamage(2, direction);
        public void TakeMagicDamage(int amount, MagicElement element, Vector2 direction) =>
            TakeDamage(amount, element, direction);

        public void TakeDamage(int amount, Vector2 direction = default) =>
            TakeDamage(amount, MagicElement.Fire, direction);

        public void TakeDamage(int amount, MagicElement element, Vector2 direction)
        {
            if (!IsAlive) return;
            health = Mathf.Max(0, health - Mathf.Max(1, amount));
            BloodEffect.SpawnHit(Body.position, direction);
            if (health > 0)
            {
                Alerted = true;
                hasInvestigation = false;
                deathSearch = false;
                hasDetour = false;
                if (element == MagicElement.Frost)
                    slowedUntil = Mathf.Max(slowedUntil, Time.time + tuning.frostSlowDuration);
                if (element == MagicElement.Lightning) stunnedUntil = Mathf.Max(stunnedUntil, Time.time + 0.28f);
                return;
            }

            BloodEffect.SpawnKill(Body.position, direction);
            EnemyAttraction.EmitDeath(Body.position, tuning.enemyDeathAttractionRadius,
                tuning.wallMask);
            movement = Vector2.zero;
            if (alert != null) alert.enabled = false;
            if (hitbox != null) hitbox.enabled = false;
            Body.simulated = false;
            gameObject.SetActive(false);
        }

        public bool CanInvestigate(Vector2 position, float radius) =>
            IsAlive && tuning != null && !Alerted && !deathSearch &&
            Vector2.Distance(Body.position, position) <= radius;

        public bool CanReactToDeath(Vector2 position, float radius) =>
            IsAlive && tuning != null && !Alerted &&
            Vector2.Distance(Body.position, position) <= radius;

        public void Investigate(Vector2 position, float radius)
        {
            if (!CanInvestigate(position, radius)) return;

            investigationTarget = position;
            hasInvestigation = true;
            reachedInvestigation = false;
            deathSearch = false;
            hasDetour = false;
            Face(position - Body.position);
            float travelTime = Vector2.Distance(Body.position, position) /
                Mathf.Max(0.1f, tuning.enemyPatrolSpeed);
            investigationExpiresAt = Time.time + travelTime * 1.5f +
                tuning.enemyInvestigationDuration + 1f;
        }

        public void InvestigateDeath(Vector2 position, float radius)
        {
            if (!CanReactToDeath(position, radius)) return;

            investigationTarget = position;
            hasInvestigation = true;
            reachedInvestigation = false;
            deathSearch = true;
            hasDetour = false;
            deathSearchFacing = position - Body.position;
            if (deathSearchFacing.sqrMagnitude < 0.0001f) deathSearchFacing = facing.right;
            deathSearchFacing.Normalize();
            Face(deathSearchFacing);
            float travelTime = Vector2.Distance(Body.position, position) /
                Mathf.Max(0.1f, tuning.enemyChaseSpeed * 0.75f);
            investigationExpiresAt = Time.time + travelTime * 1.5f +
                tuning.enemyDeathSearchDuration + 1f;
        }

        private void ScanForPlayer()
        {
            float elapsed = tuning.enemyDeathSearchDuration -
                Mathf.Max(0f, investigationEndsAt - Time.time);
            float sweep = Mathf.Sin(elapsed * 2.4f) * 78f;
            Face(Quaternion.Euler(0f, 0f, sweep) * deathSearchFacing);
        }

        private void Attack(Vector2 direction)
        {
            nextAttackTime = Time.time + (kind == FantasyEnemyKind.Knight
                ? tuning.enemyAttackInterval : Mathf.Max(1.25f, tuning.enemyAttackInterval * 1.6f));
            AttacksMade++;
            if (kind == FantasyEnemyKind.Knight)
            {
                player.Vitality.TakeHit(1, transform);
            }
            else
            {
                FantasyEnemyBolt.Spawn(Body.position + direction * 0.48f, direction,
                    tuning, player, mageElement, boltSprite, boltMaterial, transform);
            }
        }

        private void Face(Vector2 direction)
        {
            if (facing == null || direction.sqrMagnitude < 0.0001f) return;
            facing.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private Vector2 PathDirection(Vector2 position, Vector2 target)
        {
            int mask = (int)tuning.wallMask | (1 << 12);
            return EnemyNavigation2D.Direction(position, target, hitbox.radius,
                mask, ref hasDetour, ref detourTarget);
        }
    }

    public sealed class FantasyEnemyBolt : MonoBehaviour
    {
        private PrototypeTuning tuning;
        private TopDownPlayer target;
        private Vector2 direction;
        private float remaining;
        private Transform attacker;

        public static FantasyEnemyBolt Spawn(Vector2 origin, Vector2 heading, PrototypeTuning settings,
            TopDownPlayer player, MagicElement element, Sprite sprite, Material material,
            Transform source = null)
        {
            var go = new GameObject("Mage bolt / " + element);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(0.32f, 0.32f, 1f);
            var visual = go.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;
            visual.sharedMaterial = material;
            visual.sortingOrder = 16;
            visual.color = element == MagicElement.Fire ? new Color(1f, 0.38f, 0.12f)
                : element == MagicElement.Frost ? new Color(0.28f, 0.9f, 1f)
                : new Color(1f, 0.95f, 0.25f);
            FantasyEnemyBolt bolt = go.AddComponent<FantasyEnemyBolt>();
            bolt.tuning = settings;
            bolt.target = player;
            bolt.attacker = source;
            bolt.direction = heading.normalized;
            bolt.remaining = 1.6f;
            return bolt;
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float seconds)
        {
            if (tuning == null || target == null || !target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }
            float distance = 8.5f * Mathf.Min(seconds, remaining);
            int mask = (int)tuning.wallMask | (1 << target.gameObject.layer);
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, 0.11f,
                direction, distance, mask);
            if (hit)
            {
                if (hit.collider.GetComponentInParent<TopDownPlayer>() == target)
                    target.Vitality.TakeHit(1, attacker);
                Destroy(gameObject);
                return;
            }
            transform.position += (Vector3)(direction * distance);
            remaining -= seconds;
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
