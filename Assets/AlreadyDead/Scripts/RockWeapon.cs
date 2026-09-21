using UnityEngine;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class RockWeapon : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform visual;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer renderer;
        private TopDownPlayer owner;
        private float nextStrikeTime;
        private float strikeStartedAt = float.NegativeInfinity;
        private Vector2 strikeDirection = Vector2.right;
        private bool impactApplied;

        public bool IsHeld => owner != null;
        public int StrikesMade { get; private set; }
        public int ImpactsMade { get; private set; }
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public CircleCollider2D Hitbox => hitbox != null ? hitbox : hitbox = GetComponent<CircleCollider2D>();

        public void Configure(PrototypeTuning settings, Transform model, Sprite sprite, Material material)
        {
            tuning = settings;
            visual = model;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            renderer = visual.GetComponentInChildren<SpriteRenderer>();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
            if (visual != null) renderer = visual.GetComponentInChildren<SpriteRenderer>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            if (!IsHeld) return;
            float progress = (Time.time - strikeStartedAt) / tuning.rockStrikeDuration;
            if (progress < 0f || progress >= 1f)
            {
                visual.localPosition = Vector3.zero;
                return;
            }

            float extension = progress < 0.4f ? progress / 0.4f : (1f - progress) / 0.6f;
            visual.localPosition = Vector3.right * (tuning.rockStrikeReach * 0.55f * Mathf.Clamp01(extension));
            if (!impactApplied && progress >= 0.35f)
            {
                impactApplied = true;
                ApplyStrike();
            }
        }

        public void Equip(Transform socket, TopDownPlayer player)
        {
            owner = player;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.simulated = false;
            Hitbox.enabled = false;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            visual.localPosition = Vector3.zero;
            if (renderer != null) renderer.sortingOrder = 13;
            strikeStartedAt = float.NegativeInfinity;
        }

        public bool TryStrike(Vector2 direction)
        {
            if (!IsHeld || Time.time < nextStrikeTime) return false;
            strikeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            strikeStartedAt = Time.time;
            nextStrikeTime = Time.time + tuning.rockStrikeInterval;
            impactApplied = false;
            StrikesMade++;
            owner.View.Kick(tuning.punchShakeStrength, tuning.punchShakeDuration);
            return true;
        }

        public void Throw(TopDownPlayer player, Vector2 direction)
        {
            if (owner != player) return;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 origin = player.transform.position;
            float clearance = Hitbox.radius + 0.04f;
            const float desiredDistance = 0.7f;
            RaycastHit2D obstruction = Physics2D.CircleCast(origin, clearance, direction,
                desiredDistance, tuning.wallMask);
            float distance = obstruction ? Mathf.Max(0f, obstruction.distance - 0.04f) : desiredDistance;
            transform.SetParent(null, true);
            transform.position = origin + direction * distance;
            transform.rotation = Quaternion.identity;
            owner = null;
            visual.localPosition = Vector3.zero;
            if (renderer != null) renderer.sortingOrder = 1;
            strikeStartedAt = float.NegativeInfinity;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = 0f;
            Body.linearVelocity = direction * tuning.rockThrowSpeed;
            Body.angularVelocity = tuning.throwSpin * 0.45f;
            Body.WakeUp();
        }

        public void Drop(TopDownPlayer player)
        {
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            transform.rotation = Quaternion.identity;
            owner = null;
            visual.localPosition = Vector3.zero;
            if (renderer != null) renderer.sortingOrder = 1;
            strikeStartedAt = float.NegativeInfinity;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = 0f;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
        }

        private void ApplyStrike()
        {
            RaycastHit2D hit = Physics2D.CircleCast(owner.transform.position, tuning.rockStrikeRadius,
                strikeDirection, tuning.rockStrikeReach, tuning.punchMask);
            if (!hit) return;
            ImpactsMade++;
            foreach (MonoBehaviour behaviour in hit.collider.GetComponentsInParent<MonoBehaviour>())
                if (behaviour is IPunchReceiver receiver)
                    receiver.ReceivePunch(strikeDirection, tuning.rockStrikeForce);
            ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
        }
    }
}
