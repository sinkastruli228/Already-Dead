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
        private const string GroundedResourcePath = "Spear_Ground_Runtime";

        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform visual;
        [SerializeField] private Transform airborneVisual;
        [SerializeField] private Transform groundedVisual;
        [SerializeField] private Transform shadow;
        [SerializeField] private SpriteRenderer highlight;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;

        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private TopDownPlayer owner;
        private Vector3 visualRest;
        private Vector3 shadowRestScale;
        private SpriteRenderer[] shadowRenderers;
        private Color[] shadowRestColors;
        private Vector2 stabDirection = Vector2.right;
        private Vector2 flightDirection = Vector2.right;
        private float stabStartedAt = float.NegativeInfinity;
        private float nextStabTime;
        private float chargeStartedAt;
        private float travelledDistance;
        private float flightRange;
        private float vibrationStartedAt = float.NegativeInfinity;
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
        public float LastThrowCharge01 { get; private set; }
        public float FlightHeight { get; private set; }
        public float VibrationAmount { get; private set; }
        public Transform Visual => visual;
        public Transform AirborneVisual => airborneVisual;
        public Transform GroundedVisual => groundedVisual;
        public bool AirborneViewVisible => airborneVisual != null && airborneVisual.gameObject.activeSelf;
        public bool GroundedViewVisible => groundedVisual != null && groundedVisual.gameObject.activeSelf;
        public Transform Shadow => shadow;
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

        public void Configure(PrototypeTuning settings, Transform model, Transform groundShadow, SpriteRenderer halo,
            Sprite sprite, Material material)
        {
            tuning = settings;
            visual = model;
            shadow = groundShadow;
            highlight = halo;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            visualRest = visual.localPosition;
            CacheShadow();
            ResetShadow();
        }

        public void ConfigureGroundViews(Transform airborneModel, Transform groundedModel)
        {
            airborneVisual = airborneModel;
            groundedVisual = groundedModel;
            EnsureGroundedVisual();
            SetGroundedView(false);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            visualRest = visual.localPosition;
            if (airborneVisual == null && visual != null)
            {
                SpriteRenderer firstRenderer = visual.GetComponentInChildren<SpriteRenderer>(true);
                if (firstRenderer != null) airborneVisual = firstRenderer.transform;
            }
            EnsureGroundedVisual();
            SetGroundedView(false);
            CacheShadow();
            ResetShadow();
            SetHighlighted(false);
        }

        private void Update()
        {
            if (isFlying)
            {
                UpdateFlightVisual();
                return;
            }

            if (Time.time - vibrationStartedAt < tuning.spearVibrationDuration)
            {
                UpdateVibration();
                return;
            }

            VibrationAmount = 0f;
            visual.localRotation = Quaternion.identity;
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
                StopFlight(true);
                return;
            }

            RaycastHit2D obstruction = Physics2D.CircleCast(Body.position, tuning.spearStabRadius,
                flightDirection, ForwardExtent + step, tuning.wallMask | tuning.enemyMask);
            if (obstruction)
            {
                PlaceBefore(obstruction);
                bool hitEnemy = HitEnemy(obstruction.collider);
                if (!hitEnemy)
                {
                    ShotEffect.Impact(obstruction.point, obstruction.normal, primitiveSprite, primitiveMaterial);
                    AttractEnemiesIfWall(obstruction.collider);
                }
                StopFlight(true);
                return;
            }

            travelledDistance += step;
            Body.linearVelocity = flightDirection * LastThrowSpeed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isFlying || ((tuning.wallMask | tuning.enemyMask) & (1 << collision.gameObject.layer)) == 0) return;
            bool hitEnemy = HitEnemy(collision.collider);
            if (!hitEnemy && collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                ShotEffect.Impact(contact.point, contact.normal, primitiveSprite, primitiveMaterial);
                AttractEnemiesIfWall(collision.collider);
            }
            StopFlight(true);
        }

        public void SetHighlighted(bool value)
        {
            if (highlight != null) highlight.enabled = value && !IsHeld && !isFlying;
        }

        public void Equip(Transform socket, TopDownPlayer player)
        {
            StopFlight(false);
            owner = player;
            isCharging = false;
            vibrationStartedAt = float.NegativeInfinity;
            VibrationAmount = 0f;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.simulated = false;
            Hitbox.enabled = false;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            visual.localPosition = visualRest;
            visual.localRotation = Quaternion.identity;
            SetGroundedView(false);
            ResetShadow();
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
            LastThrowCharge01 = charge;
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
            visual.localRotation = Quaternion.identity;
            SetGroundedView(false);
            owner = null;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = origin;
            Body.rotation = transform.eulerAngles.z;
            Body.angularVelocity = 0f;
            Body.linearVelocity = flightDirection * LastThrowSpeed;
            isFlying = true;
            FlightHeight = 0f;
            vibrationStartedAt = float.NegativeInfinity;
            VibrationAmount = 0f;
            ResetShadow();
            SetHighlighted(false);

            // The spear's tip is well ahead of its centre. Resolve nearby cover now,
            // before the first physics tick can place the long collider through a wall.
            RaycastHit2D obstruction = Physics2D.CircleCast(origin, tuning.spearStabRadius,
                flightDirection, ForwardExtent, tuning.wallMask | tuning.enemyMask);
            if (obstruction)
            {
                PlaceBefore(obstruction);
                bool hitEnemy = HitEnemy(obstruction.collider);
                if (!hitEnemy)
                {
                    ShotEffect.Impact(obstruction.point, obstruction.normal, primitiveSprite, primitiveMaterial);
                    AttractEnemiesIfWall(obstruction.collider);
                }
                StopFlight(true);
            }
            return true;
        }

        public void Drop(TopDownPlayer player)
        {
            CancelCharge();
            StopFlight(false);
            stabStartedAt = float.NegativeInfinity;
            stabImpactApplied = true;
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            owner = null;
            visual.localPosition = visualRest;
            visual.localRotation = Quaternion.identity;
            SetGroundedView(false);
            vibrationStartedAt = float.NegativeInfinity;
            VibrationAmount = 0f;
            ResetShadow();
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
                tuning.spearStabReach, tuning.wallMask | tuning.enemyMask);
            if (!hit) return;

            ImpactsMade++;
            bool hitReceiver = false;
            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
                if (behaviour is ISpearReceiver receiver)
                {
                    receiver.ReceiveSpear(stabDirection, tuning.spearStabForce);
                    hitReceiver = true;
                }
            if (!hitReceiver)
                ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
        }

        private void PlaceBefore(RaycastHit2D hit)
        {
            Vector2 centre = hit.centroid == Vector2.zero ? hit.point : hit.centroid;
            Body.position = centre - flightDirection * (ForwardExtent + 0.02f);
            transform.position = Body.position;
        }

        private bool HitEnemy(Collider2D collider)
        {
            PatrolEnemy enemy = collider.GetComponentInParent<PatrolEnemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(LastThrowCharge01 >= 0.999f ? enemy.Health : 2, flightDirection);
                return true;
            }
            FantasyEnemy fantasyEnemy = collider.GetComponentInParent<FantasyEnemy>();
            if (fantasyEnemy != null)
            {
                fantasyEnemy.TakeDamage(LastThrowCharge01 >= 0.999f ? fantasyEnemy.Health : 2, flightDirection);
                return true;
            }
            return false;
        }

        private void AttractEnemiesIfWall(Collider2D collider)
        {
            if (collider == null || (tuning.wallMask.value & (1 << collider.gameObject.layer)) == 0) return;
            EnemyAttraction.Emit(Body.position, tuning.embeddedSpearAttractionRadius,
                tuning.wallMask, true);
        }

        private void UpdateFlightVisual()
        {
            float progress = flightRange > 0.001f ? Mathf.Clamp01(travelledDistance / flightRange) : 1f;
            float arc = 4f * progress * (1f - progress);
            FlightHeight = arc * tuning.spearThrowHeight;
            Vector3 localLift = transform.InverseTransformVector(Vector3.up * FlightHeight);
            visual.localPosition = visualRest + localLift;
            visual.localRotation = Quaternion.identity;
            ApplyShadow(progress);
        }

        private void UpdateVibration()
        {
            float elapsed = Time.time - vibrationStartedAt;
            float progress = Mathf.Clamp01(elapsed / tuning.spearVibrationDuration);
            float envelope = (1f - progress) * (1f - progress);
            float angle = Mathf.Sin(elapsed * tuning.spearVibrationFrequency * Mathf.PI * 2f) *
                tuning.spearVibrationAngle * envelope;
            // Expose the decaying strength rather than the instantaneous sine sample.
            // This also makes the effect stable for gameplay/UI checks at each zero crossing.
            VibrationAmount = tuning.spearVibrationAngle * envelope;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector3 tipPivot = Vector3.right * (ForwardExtent + 0.2f);
            visual.localRotation = rotation;
            visual.localPosition = visualRest + tipPivot - rotation * tipPivot;
            if (progress >= 1f)
            {
                VibrationAmount = 0f;
                visual.localPosition = visualRest;
                visual.localRotation = Quaternion.identity;
            }
        }

        private void CacheShadow()
        {
            if (shadow == null) return;
            shadowRestScale = shadow.localScale;
            shadowRenderers = shadow.GetComponentsInChildren<SpriteRenderer>(true);
            shadowRestColors = new Color[shadowRenderers.Length];
            for (int i = 0; i < shadowRenderers.Length; i++)
                shadowRestColors[i] = shadowRenderers[i].color;
        }

        private void ResetShadow()
        {
            if (shadow == null) return;
            shadow.gameObject.SetActive(true);
            shadow.localScale = shadowRestScale;
            if (shadowRenderers == null || shadowRestColors == null) return;
            for (int i = 0; i < shadowRenderers.Length; i++)
                shadowRenderers[i].color = shadowRestColors[i];
        }

        private void ApplyShadow(float landingProgress)
        {
            if (shadow == null) return;
            shadow.localScale = shadowRestScale * Mathf.Lerp(1f, 0.42f, landingProgress);
            if (shadowRenderers == null) return;
            Color landed = new Color(0.055f, 0.035f, 0.018f, 0.72f);
            for (int i = 0; i < shadowRenderers.Length; i++)
                shadowRenderers[i].color = Color.Lerp(shadowRestColors[i], landed, landingProgress);
        }

        private void StopFlight(bool vibrate)
        {
            bool wasFlying = isFlying;
            isFlying = false;
            FlightHeight = 0f;
            if (!vibrate) SetGroundedView(false);
            if (body == null) return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            if (!wasFlying) return;
            SetGroundedView(vibrate);
            visual.localPosition = visualRest;
            ApplyShadow(1f);
            if (!vibrate) return;
            vibrationStartedAt = Time.time;
            VibrationAmount = tuning.spearVibrationAngle;
        }

        private void SetGroundedView(bool grounded)
        {
            bool useGrounded = grounded && groundedVisual != null;
            if (airborneVisual != null) airborneVisual.gameObject.SetActive(!useGrounded);
            if (groundedVisual != null) groundedVisual.gameObject.SetActive(useGrounded);
        }

        private void EnsureGroundedVisual()
        {
            if (groundedVisual != null || visual == null || airborneVisual == null) return;

            Sprite groundedSprite = LoadLargestResourceSprite(GroundedResourcePath);
            if (groundedSprite == null) return;

            SpriteRenderer source = airborneVisual.GetComponent<SpriteRenderer>();
            var groundedObject = new GameObject("Spear embedded in ground / runtime fallback");
            groundedVisual = groundedObject.transform;
            groundedVisual.SetParent(visual, false);
            groundedVisual.localPosition = airborneVisual.localPosition;
            groundedVisual.localRotation = airborneVisual.localRotation;
            groundedVisual.localScale = airborneVisual.localScale;
            SpriteRenderer renderer = groundedObject.AddComponent<SpriteRenderer>();
            renderer.sprite = groundedSprite;
            if (source != null)
            {
                renderer.sharedMaterial = source.sharedMaterial;
                renderer.color = source.color;
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder;
            }
            groundedObject.SetActive(false);
        }

        private static Sprite LoadLargestResourceSprite(string path)
        {
            Sprite best = null;
            float bestArea = -1f;
            foreach (Sprite candidate in Resources.LoadAll<Sprite>(path))
            {
                float area = candidate.rect.width * candidate.rect.height;
                if (area <= bestArea) continue;
                best = candidate;
                bestArea = area;
            }
            return best;
        }
    }
}
