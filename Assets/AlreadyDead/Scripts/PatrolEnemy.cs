using UnityEngine;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class PatrolEnemy : MonoBehaviour, IPunchReceiver, ISpearReceiver
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private TopDownPlayer player;
        [SerializeField] private Transform facing;
        [SerializeField] private SpriteRenderer alert;
        [SerializeField] private Vector2 patrolA;
        [SerializeField] private Vector2 patrolB;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private Vector2 moveDirection;
        private Vector2 detourTarget;
        private bool hasDetour;
        private bool towardB = true;
        private float nextAttackTime;
        private int health;

        public bool Alerted { get; private set; }
        public bool IsAlive => health > 0;
        public int Health => health;
        public int AttacksMade { get; private set; }
        public Transform Facing => facing;
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();

        public void Configure(PrototypeTuning settings, TopDownPlayer target, Transform visual,
            SpriteRenderer indicator, Vector2 first, Vector2 second)
        {
            tuning = settings;
            player = target;
            facing = visual;
            alert = indicator;
            patrolA = first;
            patrolB = second;
            health = tuning.enemyMaxHealth;
            Alerted = false;
            hasDetour = false;
            alert.enabled = false;
            Face((patrolB - patrolA).normalized);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
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
            if (!Alerted && CanSeePlayer()) Alerted = true;
            if (alert != null) alert.enabled = Alerted;

            if (Alerted)
            {
                if (distance <= tuning.enemyAttackRange && !Physics2D.Linecast(position,
                        player.transform.position, tuning.wallMask))
                {
                    moveDirection = Vector2.zero;
                    Face(toPlayer);
                    if (Time.time >= nextAttackTime)
                    {
                        nextAttackTime = Time.time + tuning.enemyAttackInterval;
                        AttacksMade++;
                        player.Vitality.TakeHit(1);
                    }
                }
                else
                {
                    moveDirection = ChaseDirection(position, toPlayer) * tuning.enemyChaseSpeed;
                    Face(moveDirection);
                }
                return;
            }

            Vector2 waypoint = towardB ? patrolB : patrolA;
            Vector2 path = waypoint - position;
            if (path.magnitude <= tuning.enemyWaypointTolerance)
            {
                towardB = !towardB;
                waypoint = towardB ? patrolB : patrolA;
                path = waypoint - position;
            }
            moveDirection = path.sqrMagnitude > 0.0001f
                ? path.normalized * tuning.enemyPatrolSpeed : Vector2.zero;
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

        public void ReceivePunch(Vector2 direction, float force) => TakeDamage(1);
        public void ReceiveSpear(Vector2 direction, float force) => TakeDamage(2);

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;
            health = Mathf.Max(0, health - Mathf.Max(1, amount));
            if (health > 0)
            {
                Alerted = true;
                return;
            }
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
            if (alert != null) alert.enabled = false;
        }

        private void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f || facing == null) return;
            facing.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private Vector2 ChaseDirection(Vector2 position, Vector2 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 0.0001f) return Vector2.zero;
            Vector2 direct = toPlayer.normalized;
            RaycastHit2D wall = Physics2D.CircleCast(position, hitbox.radius, direct,
                toPlayer.magnitude, tuning.wallMask);
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
                float cost = length + Vector2.Distance(corner, player.transform.position);
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
