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
        private EnemyRevolver revolver;
        private EnemyGlock glock;
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
        private bool wandering;
        private bool hasWanderTarget;
        private Vector2 wanderTarget;
        private Vector2 previousWanderPosition;
        private float wanderDecisionAt;
        private float wanderStuckFor;
        private float wanderSpeedFactor = 1f;
        private int wanderObstacleMask;
        private float nextAttackTime;
        private float alertedAt = float.NegativeInfinity;
        private float spearWindupStartedAt;
        private bool windingUpSpear;
        private float slowedUntil;
        private int health;

        public bool Alerted { get; private set; }
        public bool IsAlive => health > 0;
        public int Health => health;
        public int AttacksMade { get; private set; }
        public bool IsWindingUpSpear => windingUpSpear;
        public Transform Facing => facing;
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public float SpeedMultiplier => Time.time < slowedUntil ? tuning.frostSlowMultiplier : 1f;
        public Vector2[] PatrolRoute => patrolRoute;
        public bool Investigating => hasInvestigation;
        public Vector2 InvestigationTarget => investigationTarget;
        public bool SearchingAfterDeath => deathSearch;
        public bool Wandering => wandering;
        public Vector2 WanderTarget => wanderTarget;

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
            health = 1;
            Alerted = false;
            alertedAt = float.NegativeInfinity;
            windingUpSpear = false;
            hasDetour = false;
            hasInvestigation = false;
            deathSearch = false;
            wandering = false;
            hasWanderTarget = false;
            if (alert != null) alert.enabled = false;
            Face((patrolB - patrolA).normalized);
        }

        // Saloon enemies roam through open space instead of repeating their scene route.
        // The route is retained for enemies in other scenes and for manual placement.
        public void EnableWandering()
        {
            wandering = true;
            hasWanderTarget = false;
            wanderDecisionAt = Time.time + Random.Range(0.15f, 1.2f);
            wanderStuckFor = 0f;
            previousWanderPosition = Body.position;
            wanderObstacleMask = tuning.wallMask.value;
            int furnitureLayer = LayerMask.NameToLayer("Furniture");
            if (furnitureLayer >= 0) wanderObstacleMask |= 1 << furnitureLayer;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
            loadout = GetComponent<EnemyWeaponLoadout>();
            revolver = GetComponent<EnemyRevolver>();
            glock = GetComponent<EnemyGlock>();
            nextWaypoint = patrolRoute != null && patrolRoute.Length > 1 ? 1 : 0;
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (tuning != null) health = 1;
            if (alert != null) alert.enabled = false;
        }

        private void Update()
        {
            if (!IsAlive || player == null || !player.IsAlive) { Stop(); return; }

            Vector2 position = Body.position;
            Vector2 toPlayer = (Vector2)player.transform.position - position;
            float distance = toPlayer.magnitude;
            if (!Alerted && CanSeePlayer()) AlertToPlayer();

            if (Alerted)
            {
                if (glock != null)
                {
                    bool clearGlockShot = !Physics2D.Linecast(position,
                        player.transform.position, tuning.wallMask);
                    moveDirection = PathDirection(position, player.transform.position) *
                        tuning.enemyChaseSpeed * SpeedMultiplier;
                    Face(toPlayer);
                    if (distance <= EnemyGlock.FireRange && clearGlockShot &&
                        Time.time - alertedAt >= 0.2f)
                    {
                        if (glock.TryFire(toPlayer)) AttacksMade++;
                    }
                    return;
                }
                if (revolver != null)
                {
                    bool revolverShotClear = !Physics2D.Linecast(position,
                        player.transform.position, tuning.wallMask);
                    if (distance <= EnemyRevolver.FireRange && revolverShotClear)
                    {
                        moveDirection = Vector2.zero;
                        Face(toPlayer);
                        if (revolver.TryFire(toPlayer)) AttacksMade++;
                    }
                    else
                    {
                        moveDirection = PathDirection(position, player.transform.position) *
                            tuning.enemyChaseSpeed * SpeedMultiplier;
                        Face(moveDirection);
                    }
                    return;
                }
                float attackRange = tuning.enemyAttackRange + (loadout != null ? loadout.AttackRangeBonus : 0f);
                bool clearShot = !Physics2D.Linecast(position, player.transform.position, tuning.wallMask);
                if (windingUpSpear)
                {
                    if (!clearShot || loadout == null || !loadout.CanThrowSpear ||
                        distance > loadout.SpearThrowRange)
                    {
                        CancelSpearWindup();
                    }
                    else
                    {
                        float duration = tuning.spearMaxChargeTime * 0.5f;
                        float progress = Mathf.Clamp01((Time.time - spearWindupStartedAt) / duration);
                        loadout.SetSpearWindup(progress);
                        Face(toPlayer);
                        if (progress >= 1f)
                        {
                            moveDirection = Vector2.zero;
                            windingUpSpear = false;
                            if (loadout.TryThrowSpear(position, toPlayer))
                            {
                                nextAttackTime = Time.time + tuning.enemyAttackInterval;
                                AttacksMade++;
                            }
                            else loadout.SetSpearWindup(0f);
                        }
                        else
                        {
                            moveDirection = PathDirection(position, player.transform.position) *
                                tuning.enemyPatrolSpeed * SpeedMultiplier;
                        }
                        return;
                    }
                }
                if (distance <= attackRange && clearShot)
                {
                    moveDirection = Vector2.zero;
                    Face(toPlayer);
                    if (Time.time >= nextAttackTime)
                    {
                        nextAttackTime = Time.time + tuning.enemyAttackInterval;
                        AttacksMade++;
                        loadout?.PlayAttack();
                        player.Vitality.TakeHit(loadout != null ? loadout.AttackDamage : 1,
                            transform);
                    }
                }
                else if (loadout != null && loadout.CanThrowSpear &&
                         distance > tuning.spearMinThrowRange &&
                         distance <= loadout.SpearThrowRange && clearShot &&
                         Time.time >= nextAttackTime)
                {
                    windingUpSpear = true;
                    spearWindupStartedAt = Time.time;
                    loadout.SetSpearWindup(0f);
                    moveDirection = PathDirection(position, player.transform.position) *
                        tuning.enemyPatrolSpeed * SpeedMultiplier;
                    Face(toPlayer);
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

            if (wandering)
            {
                UpdateWandering(position);
                return;
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

        private void UpdateWandering(Vector2 position)
        {
            if (!hasWanderTarget)
            {
                moveDirection = Vector2.zero;
                if (Time.time >= wanderDecisionAt) ChooseWanderTarget(position);
                return;
            }

            Vector2 path = wanderTarget - position;
            float distance = path.magnitude;
            if (distance <= Mathf.Max(0.28f, tuning.enemyWaypointTolerance))
            {
                PauseWandering();
                return;
            }
            Vector2 direction = path / distance;
            RaycastHit2D wanderObstacle = Physics2D.CircleCast(position, hitbox.radius * 0.9f,
                direction, distance, wanderObstacleMask);
            if (wanderObstacle)
            {
                PushDoor2D door = wanderObstacle.collider.GetComponentInParent<PushDoor2D>();
                if (door == null)
                {
                    PauseWandering();
                    return;
                }
                if (wanderObstacle.distance < 1.3f)
                    door.PushFrom(position, direction, 0.16f);
            }

            wanderStuckFor = Vector2.Distance(position, previousWanderPosition) < 0.004f
                ? wanderStuckFor + Time.deltaTime : 0f;
            previousWanderPosition = position;
            if (wanderStuckFor >= 0.7f)
            {
                PauseWandering();
                return;
            }
            moveDirection = direction * (tuning.enemyPatrolSpeed * wanderSpeedFactor * SpeedMultiplier);
            Face(direction);
        }

        private void ChooseWanderTarget(Vector2 position)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.5f) continue;
                float distance = Random.Range(1.4f, 4.8f);
                Vector2 target = position + direction * distance;
                if (Physics2D.OverlapCircle(target, hitbox.radius + 0.12f,
                        wanderObstacleMask)) continue;
                RaycastHit2D obstacle = Physics2D.CircleCast(position,
                    hitbox.radius * 0.9f, direction, distance, wanderObstacleMask);
                if (obstacle && obstacle.collider.GetComponentInParent<PushDoor2D>() == null)
                    continue;
                wanderTarget = target;
                hasWanderTarget = true;
                wanderStuckFor = 0f;
                wanderSpeedFactor = Random.Range(0.6f, 0.95f);
                previousWanderPosition = position;
                return;
            }
            wanderDecisionAt = Time.time + Random.Range(0.5f, 1.3f);
        }

        private void PauseWandering()
        {
            hasWanderTarget = false;
            moveDirection = Vector2.zero;
            wanderStuckFor = 0f;
            wanderDecisionAt = Time.time + Random.Range(0.6f, 2.2f);
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

        private void AlertToPlayer()
        {
            if (!IsAlive || player == null || !player.IsAlive) return;
            Alerted = true;
            alertedAt = Time.time;
            hasInvestigation = false;
            deathSearch = false;
            hasDetour = false;
            hasWanderTarget = false;
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
                hasWanderTarget = false;
                return;
            }
            BloodEffect.SpawnKill(Body.position, direction);
            EnemyAttraction.EmitDeath(Body.position, tuning.enemyDeathAttractionRadius,
                tuning.wallMask);
            loadout?.DropOnDeath();
            glock?.DropOnDeath();
            Stop();
            hitbox.enabled = false;
            Body.simulated = false;
            gameObject.SetActive(false);
        }

        private void Stop()
        {
            CancelSpearWindup();
            moveDirection = Vector2.zero;
            Alerted = false;
            hasDetour = false;
            hasInvestigation = false;
            deathSearch = false;
            if (alert != null) alert.enabled = false;
        }

        private void CancelSpearWindup()
        {
            windingUpSpear = false;
            loadout?.SetSpearWindup(0f);
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
            hasWanderTarget = false;
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
            hasWanderTarget = false;
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
            int mask = (int)tuning.wallMask | (1 << 12);
            return EnemyNavigation2D.Direction(position, target, hitbox.radius,
                mask, ref hasDetour, ref detourTarget);
        }
    }
}
