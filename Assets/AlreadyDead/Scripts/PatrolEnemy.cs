using UnityEngine;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class PatrolEnemy : MonoBehaviour, IPunchReceiver, ISpearReceiver, IMagicDamageable,
        IEnemyAttractionListener
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private TopDownPlayer player;
        [SerializeField] private Transform facing;
        [SerializeField] private SpriteRenderer alert;
        [SerializeField] private Vector2 patrolA;
        [SerializeField] private Vector2 patrolB;
        [SerializeField] private Vector2[] patrolRoute;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private EnemyWeaponLoadout loadout;
        private Vector2 moveDirection;
        private Vector2 detourTarget;
        private bool hasDetour;
        private Vector2 investigationTarget;
        private bool hasInvestigation;
        private bool reachedInvestigation;
        private bool deathSearch;
        private Vector2 deathSearchFacing = Vector2.right;
        private float investigationExpiresAt;
        private float investigationEndsAt;
        private int nextWaypoint = 1;
        private float nextAttackTime;
        private float slowedUntil;
        private int health;

        public bool Alerted { get; private set; }
        public bool IsAlive => health > 0;
        public int Health => health;
        public int AttacksMade { get; private set; }
        public Transform Facing => facing;
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public float SpeedMultiplier => Time.time < slowedUntil ? tuning.frostSlowMultiplier : 1f;
        public Vector2[] PatrolRoute => patrolRoute;
        public bool Investigating => hasInvestigation;
        public Vector2 InvestigationTarget => investigationTarget;
        public bool SearchingAfterDeath => deathSearch;

        public void Configure(PrototypeTuning settings, TopDownPlayer target, Transform visual,
            SpriteRenderer indicator, Vector2 first, Vector2 second)
        {
            ConfigureRoute(settings, target, visual, indicator, new[] { first, second });
        }

        public void ConfigureRoute(PrototypeTuning settings, TopDownPlayer target, Transform visual,
            SpriteRenderer indicator, Vector2[] waypoints)
        {
            if (waypoints == null || waypoints.Length == 0)
                throw new System.ArgumentException("A patrol needs at least one waypoint.", nameof(waypoints));
            tuning = settings;
            player = target;
            facing = visual;
            alert = indicator;
            patrolRoute = (Vector2[])waypoints.Clone();
            patrolA = patrolRoute[0];
            patrolB = patrolRoute[patrolRoute.Length > 1 ? 1 : 0];
            nextWaypoint = patrolRoute.Length > 1 ? 1 : 0;
            health = tuning.enemyMaxHealth;
            Alerted = false;
            hasDetour = false;
            hasInvestigation = false;
            deathSearch = false;
            alert.enabled = false;
            Face((patrolB - patrolA).normalized);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
            loadout = GetComponent<EnemyWeaponLoadout>();
            nextWaypoint = patrolRoute != null && patrolRoute.Length > 1 ? 1 : 0;
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (tuning != null) health = tuning.enemyMaxHealth;
        }

        private void Update()
        {
            if (!IsAlive || player == null || !player.IsAlive) { Stop(); return; }

            Vector2 position = Body.position;
            Vector2 toPlayer = (Vector2)player.transform.position - position;
            float distance = toPlayer.magnitude;
            if (!Alerted && CanSeePlayer())
            {
                Alerted = true;
                hasInvestigation = false;
                deathSearch = false;
                hasDetour = false;
            }
            if (alert != null) alert.enabled = Alerted || deathSearch;

            if (Alerted)
            {
                float attackRange = tuning.enemyAttackRange + (loadout != null ? loadout.AttackRangeBonus : 0f);
                if (distance <= attackRange && !Physics2D.Linecast(position,
                        player.transform.position, tuning.wallMask))
                {
                    moveDirection = Vector2.zero;
                    Face(toPlayer);
                    if (Time.time >= nextAttackTime)
                    {
                        nextAttackTime = Time.time + tuning.enemyAttackInterval;
                        AttacksMade++;
                        loadout?.PlayAttack();
                        player.Vitality.TakeHit(loadout != null ? loadout.AttackDamage : 1);
                    }
                }
                else
                {
                    moveDirection = PathDirection(position, player.transform.position) *
                        tuning.enemyChaseSpeed * SpeedMultiplier;
                    Face(moveDirection);
                }
                return;
            }

            if (hasInvestigation)
            {
                Vector2 investigationPath = investigationTarget - position;
                if (investigationPath.magnitude <= Mathf.Max(tuning.enemyInvestigationDistance,
                        tuning.enemyWaypointTolerance))
                {
                    moveDirection = Vector2.zero;
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
                    moveDirection = PathDirection(position, investigationTarget) *
                        (deathSearch ? tuning.enemyChaseSpeed * 0.75f : tuning.enemyPatrolSpeed) *
                        SpeedMultiplier;
                    Face(moveDirection);
                    return;
                }
                else
                {
                    hasInvestigation = false;
                    deathSearch = false;
                    hasDetour = false;
                }
            }

            if (patrolRoute == null || patrolRoute.Length == 0)
                patrolRoute = new[] { patrolA, patrolB };
            Vector2 waypoint = patrolRoute[nextWaypoint];
            Vector2 path = waypoint - position;
            if (path.magnitude <= tuning.enemyWaypointTolerance)
            {
                nextWaypoint = (nextWaypoint + 1) % patrolRoute.Length;
                waypoint = patrolRoute[nextWaypoint];
                path = waypoint - position;
            }
            moveDirection = path.sqrMagnitude > 0.0001f
                ? path.normalized * tuning.enemyPatrolSpeed * SpeedMultiplier : Vector2.zero;
            Face(path);
        }

        private void FixedUpdate()
        {
            Body.linearVelocity = IsAlive ? moveDirection : Vector2.zero;
        }

        public bool CanSeePlayer()
        {
            if (!IsAlive || player == null || !player.IsAlive) return false;
            Vector2 from = Body.position;
            Vector2 to = (Vector2)player.transform.position - from;
            if (to.sqrMagnitude > tuning.enemyVisionRange * tuning.enemyVisionRange) return false;
            if (to.sqrMagnitude < 0.0001f) return true;
            if (Vector2.Angle(facing.right, to) > tuning.enemyVisionHalfAngle) return false;
            return !Physics2D.Linecast(from, player.transform.position, tuning.wallMask);
        }

        public void ReceivePunch(Vector2 direction, float force) => TakeDamage(1, direction);
        public void ReceiveSpear(Vector2 direction, float force) => TakeDamage(2, direction);
        public void TakeMagicDamage(int amount, MagicElement element, Vector2 direction)
        {
            if (element == MagicElement.Frost)
                slowedUntil = Mathf.Max(slowedUntil, Time.time + tuning.frostSlowDuration);
            TakeDamage(amount, direction);
        }

        public void TakeDamage(int amount, Vector2 direction = default)
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
                return;
            }
            BloodEffect.SpawnKill(Body.position, direction);
            EnemyAttraction.EmitDeath(Body.position, tuning.enemyDeathAttractionRadius,
                tuning.wallMask);
            loadout?.DropOnDeath();
            Stop();
            hitbox.enabled = false;
            Body.simulated = false;
            gameObject.SetActive(false);
        }

        private void Stop()
        {
            moveDirection = Vector2.zero;
            Alerted = false;
            hasDetour = false;
            hasInvestigation = false;
            deathSearch = false;
            if (alert != null) alert.enabled = false;
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

        private void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f || facing == null) return;
            facing.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private Vector2 PathDirection(Vector2 position, Vector2 target)
        {
            Vector2 toTarget = target - position;
            if (toTarget.sqrMagnitude < 0.0001f) return Vector2.zero;
            Vector2 direct = toTarget.normalized;
            RaycastHit2D wall = Physics2D.CircleCast(position, hitbox.radius, direct,
                toTarget.magnitude, tuning.wallMask);
            if (!wall)
            {
                hasDetour = false;
                return direct;
            }

            if (hasDetour)
            {
                Vector2 path = detourTarget - position;
                float length = path.magnitude;
                if (length > 0.2f && !Physics2D.CircleCast(position, hitbox.radius,
                        path / length, length, tuning.wallMask))
                    return path / length;
                hasDetour = false;
            }

            Bounds bounds = wall.collider.bounds;
            float clearance = hitbox.radius + 0.2f;
            Vector2[] corners =
            {
                new Vector2(bounds.min.x - clearance, bounds.min.y - clearance),
                new Vector2(bounds.min.x - clearance, bounds.max.y + clearance),
                new Vector2(bounds.max.x + clearance, bounds.min.y - clearance),
                new Vector2(bounds.max.x + clearance, bounds.max.y + clearance)
            };
            Vector2 best = Vector2.zero;
            float bestCost = float.PositiveInfinity;
            foreach (Vector2 corner in corners)
            {
                Vector2 path = corner - position;
                float length = path.magnitude;
                if (length < 0.3f) continue;
                if (Physics2D.CircleCast(position, hitbox.radius, path / length,
                        length, tuning.wallMask)) continue;
                float cost = length + Vector2.Distance(corner, target);
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = path / length;
                detourTarget = corner;
            }
            hasDetour = best != Vector2.zero;
            return hasDetour ? best : direct;
        }
    }
}
