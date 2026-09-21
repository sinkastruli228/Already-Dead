using UnityEngine;

namespace AlreadyDead
{
    public interface ISpearReceiver
    {
        void ReceiveSpear(Vector2 direction, float force);
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class SpearWeapon : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer highlight;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;

        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private TopDownPlayer owner;
        private Vector3 visualRest;
        private Vector2 stabDirection = Vector2.right;
        private Vector2 flightDirection = Vector2.right;
        private float stabStartedAt = float.NegativeInfinity;
        private float nextStabTime;
        private float chargeStartedAt;
        private float travelledDistance;
        private float flightRange;
        private bool stabImpactApplied;
        private bool isCharging;
        private bool isFlying;

        public bool IsHeld => owner != null;
        public bool IsCharging => isCharging;
        public bool IsFlying => isFlying;
        public bool IsStabbing => Time.time - stabStartedAt < tuning.spearStabDuration;
        public float Charge01 => isCharging
            ? Mathf.Clamp01((Time.time - chargeStartedAt) / tuning.spearMaxChargeTime)
            : 0f;
        public int StabsMade { get; private set; }
        public int ImpactsMade { get; private set; }
        public float LastThrowSpeed { get; private set; }
        public float LastThrowRange { get; private set; }
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public BoxCollider2D Hitbox => hitbox != null ? hitbox : hitbox = GetComponent<BoxCollider2D>();

        private float ForwardExtent
        {
            get
            {
                float scale = Mathf.Abs(transform.lossyScale.x);
                return Mathf.Max(0.05f, (Hitbox.size.x * 0.5f + Mathf.Max(0f, Hitbox.offset.x)) * scale);
            }
        }

        public void Configure(PrototypeTuning settings, Transform model, SpriteRenderer halo,
            Sprite sprite, Material material)
        {
            tuning = settings;
            visual = model;
            highlight = halo;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            visualRest = visual.localPosition;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            visualRest = visual.localPosition;
            SetHighlighted(false);
        }

        private void Update()
        {
            if (IsStabbing)
            {
                float progress = Mathf.Clamp01((Time.time - stabStartedAt) / tuning.spearStabDuration);
                float extension = progress < 0.35f
                    ? progress / 0.35f
                    : 1f - (progress - 0.35f) / 0.65f;
                extension = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(extension));
                visual.localPosition = visualRest + Vector3.right * (tuning.spearStabReach * 0.42f * extension);

                if (!stabImpactApplied && progress >= 0.32f)
                {
                    stabImpactApplied = true;
                    ApplyStabImpact();
                }
                return;
            }

            visual.localPosition = isCharging
                ? visualRest + Vector3.left * (0.22f * Charge01)
                : visualRest;
        }

        private void FixedUpdate()
        {
            if (!isFlying) return;

            float step = LastThrowSpeed * Time.fixedDeltaTime;
            float remaining = flightRange - travelledDistance;
            if (remaining <= step)
            {
                Body.position += flightDirection * Mathf.Max(0f, remaining);
                travelledDistance = flightRange;
                StopFlight();
                return;
            }

            RaycastHit2D obstruction = Physics2D.CircleCast(Body.position, tuning.spearStabRadius,
                flightDirection, ForwardExtent + step, tuning.wallMask);
            if (obstruction)
            {
                PlaceBefore(obstruction);
                ShotEffect.Impact(obstruction.point, obstruction.normal, primitiveSprite, primitiveMaterial);
                StopFlight();
                return;
            }

            travelledDistance += step;
            Body.linearVelocity = flightDirection * LastThrowSpeed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isFlying || (tuning.wallMask.value & (1 << collision.gameObject.layer)) == 0) return;
            if (collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                ShotEffect.Impact(contact.point, contact.normal, primitiveSprite, primitiveMaterial);
            }
            StopFlight();
        }

        public void SetHighlighted(bool value)
        {
            if (highlight != null) highlight.enabled = value && !IsHeld && !isFlying;
        }

        public void Equip(Transform socket, TopDownPlayer player)
        {
            StopFlight();
            owner = player;
            isCharging = false;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.simulated = false;
            Hitbox.enabled = false;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            visual.localPosition = visualRest;
            SetHighlighted(false);
        }

        public bool TryStab(Vector2 direction)
        {
            if (!IsHeld || isCharging || Time.time < nextStabTime) return false;
            stabDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            stabStartedAt = Time.time;
            nextStabTime = Time.time + tuning.spearStabInterval;
            stabImpactApplied = false;
            StabsMade++;
            owner.View.Kick(tuning.punchShakeStrength, tuning.punchShakeDuration);
            return true;
        }

        public bool BeginCharge()
        {
            if (!IsHeld || isCharging || IsStabbing) return false;
            isCharging = true;
            chargeStartedAt = Time.time;
            return true;
        }

        public void CancelCharge()
        {
            isCharging = false;
            if (visual != null) visual.localPosition = visualRest;
        }

        public bool ReleaseThrow(TopDownPlayer player, Vector2 direction)
        {
            if (owner != player || !isCharging) return false;

            float charge = Charge01;
            LastThrowSpeed = Mathf.Lerp(tuning.spearMinThrowSpeed, tuning.spearMaxThrowSpeed, charge);
            LastThrowRange = Mathf.Lerp(tuning.spearMinThrowRange, tuning.spearMaxThrowRange, charge);
            flightRange = LastThrowRange;
            travelledDistance = 0f;
            flightDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 origin = player.transform.position;

            isCharging = false;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(origin,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg));
            visual.localPosition = visualRest;
            owner = null;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = origin;
            Body.rotation = transform.eulerAngles.z;
            Body.angularVelocity = 0f;
            Body.linearVelocity = flightDirection * LastThrowSpeed;
            isFlying = true;
            SetHighlighted(false);

            // The spear's tip is well ahead of its centre. Resolve nearby cover now,
            // before the first physics tick can place the long collider through a wall.
            RaycastHit2D obstruction = Physics2D.CircleCast(origin, tuning.spearStabRadius,
                flightDirection, ForwardExtent, tuning.wallMask);
            if (obstruction)
            {
                PlaceBefore(obstruction);
                ShotEffect.Impact(obstruction.point, obstruction.normal, primitiveSprite, primitiveMaterial);
                StopFlight();
            }
            return true;
        }

        public void Drop(TopDownPlayer player)
        {
            CancelCharge();
            StopFlight();
            stabStartedAt = float.NegativeInfinity;
            stabImpactApplied = true;
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            owner = null;
            visual.localPosition = visualRest;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = transform.eulerAngles.z;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            SetHighlighted(false);
        }

        private void ApplyStabImpact()
        {
            Vector2 origin = owner != null ? (Vector2)owner.transform.position : Body.position;
            RaycastHit2D hit = Physics2D.CircleCast(origin, tuning.spearStabRadius, stabDirection,
                tuning.spearStabReach, tuning.wallMask);
            if (!hit) return;

            ImpactsMade++;
            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
                if (behaviour is ISpearReceiver receiver)
                    receiver.ReceiveSpear(stabDirection, tuning.spearStabForce);
            ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
        }

        private void PlaceBefore(RaycastHit2D hit)
        {
            Vector2 centre = hit.centroid == Vector2.zero ? hit.point : hit.centroid;
            Body.position = centre - flightDirection * (ForwardExtent + 0.02f);
            transform.position = Body.position;
        }

        private void StopFlight()
        {
            isFlying = false;
            if (body == null) return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }
}
