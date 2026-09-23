using UnityEngine;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class RockWeapon : MonoBehaviour
    {
        private const string GroundedResourcePath = "Stone_Ground_Runtime";

        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform visual;
        [SerializeField] private Transform shadow;
        [SerializeField] private Transform buriedMark;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private SpriteRenderer renderer;
        private SpriteRenderer shadowRenderer;
        private TopDownPlayer owner;
        private Vector3 visualRest;
        private Vector3 visualRestScale;
        private Vector3 shadowRestScale;
        private Color shadowRestColor;
        private float nextStrikeTime;
        private float strikeStartedAt = float.NegativeInfinity;
        private float flightElapsed;
        private float flightSpeed;
        private float flightDuration;
        private float chargeStartedAt;
        private bool isCharging;
        private Vector2 strikeDirection = Vector2.right;
        private Vector2 flightDirection = Vector2.right;
        private bool impactApplied;
        private bool isFlying;
        [SerializeField] private bool isBuried;

        public bool IsHeld => owner != null;
        public bool IsFlying => isFlying;
        public bool IsCharging => isCharging;
        public float Charge01 => isCharging
            ? Mathf.Clamp01((Time.time - chargeStartedAt) / tuning.rockMaxChargeTime) : 0f;
        public float LastThrowCharge01 { get; private set; }
        public float LastThrowRange { get; private set; }
        public bool IsBuried => isBuried;
        public float FlightHeight { get; private set; }
        public int StrikesMade { get; private set; }
        public int ImpactsMade { get; private set; }
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public CircleCollider2D Hitbox => hitbox != null ? hitbox : hitbox = GetComponent<CircleCollider2D>();
        public Transform Visual => visual;
        public Transform Shadow => shadow;
        public Transform BuriedMark => buriedMark;

        public void Configure(PrototypeTuning settings, Transform model, Transform groundShadow,
            Transform buried, Sprite sprite, Material material)
        {
            tuning = settings;
            visual = model;
            shadow = groundShadow;
            buriedMark = buried;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            CacheVisualState();
            SetLooseGroundState();
        }

        public void PlaceBuried()
        {
            owner = null;
            isFlying = false;
            isBuried = true;
            FlightHeight = 0f;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.rotation = 0f;
            transform.rotation = Quaternion.identity;
            Body.bodyType = RigidbodyType2D.Static;
            Body.Sleep();
            visual.localPosition = visualRest;
            visual.localScale = visualRestScale;
            visual.localRotation = Quaternion.identity;
            visual.gameObject.SetActive(false);
            if (renderer != null) renderer.sortingOrder = 2;
            if (shadow != null) shadow.gameObject.SetActive(false);
            if (buriedMark != null) buriedMark.gameObject.SetActive(true);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CacheVisualState();
            EnsureGroundedSprite();
            if (ShouldStartBuried()) PlaceBuried();
            else SetLooseGroundState();
        }

        private void Update()
        {
            if (isFlying)
            {
                UpdateFlightVisual();
                return;
            }

            if (!IsHeld) return;
            float progress = (Time.time - strikeStartedAt) / tuning.rockStrikeDuration;
            if (progress < 0f || progress >= 1f)
            {
                visual.localPosition = visualRest;
                return;
            }

            float extension = progress < 0.4f ? progress / 0.4f : (1f - progress) / 0.6f;
            visual.localPosition = visualRest + Vector3.right *
                (tuning.rockStrikeReach * 0.55f * Mathf.Clamp01(extension));
            if (!impactApplied && progress >= 0.35f)
            {
                impactApplied = true;
                ApplyStrike();
            }
        }

        private void FixedUpdate()
        {
            if (!isFlying) return;

            float remainingTime = Mathf.Max(0f, flightDuration - flightElapsed);
            float stepTime = Mathf.Min(Time.fixedDeltaTime, remainingTime);
            float step = flightSpeed * stepTime;
            RaycastHit2D obstruction = Physics2D.CircleCast(Body.position, Hitbox.radius,
                flightDirection, step + 0.02f, tuning.wallMask | tuning.enemyMask);
            if (obstruction)
            {
                Body.position = obstruction.centroid - flightDirection * 0.02f;
                transform.position = Body.position;
                if (!HitEnemy(obstruction.collider))
                    ShotEffect.Impact(obstruction.point, obstruction.normal, primitiveSprite, primitiveMaterial);
                Land();
                return;
            }

            flightElapsed += stepTime;
            if (flightElapsed >= flightDuration - 0.0001f)
            {
                Body.position += flightDirection * step;
                transform.position = Body.position;
                Land();
                return;
            }

            Body.linearVelocity = flightDirection * flightSpeed;
            Body.angularVelocity = 0f;
            Body.rotation = 0f;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isFlying || ((tuning.wallMask | tuning.enemyMask) & (1 << collision.gameObject.layer)) == 0) return;
            bool hitEnemy = HitEnemy(collision.collider);
            if (!hitEnemy && collision.contactCount > 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                ShotEffect.Impact(contact.point, contact.normal, primitiveSprite, primitiveMaterial);
            }
            Land();
        }

        public void Equip(Transform socket, TopDownPlayer player)
        {
            owner = player;
            isCharging = false;
            isFlying = false;
            isBuried = false;
            FlightHeight = 0f;
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.rotation = 0f;
            Body.simulated = false;
            Hitbox.enabled = false;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            ResetVisual();
            if (shadow != null) shadow.gameObject.SetActive(false);
            if (buriedMark != null) buriedMark.gameObject.SetActive(false);
            if (renderer != null) renderer.sortingOrder = 13;
            strikeStartedAt = float.NegativeInfinity;
        }

        public bool TryStrike(Vector2 direction)
        {
            if (!IsHeld || isCharging || Time.time < nextStrikeTime) return false;
            strikeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            strikeStartedAt = Time.time;
            nextStrikeTime = Time.time + tuning.rockStrikeInterval;
            impactApplied = false;
            StrikesMade++;
            owner.View.Kick(tuning.punchShakeStrength, tuning.punchShakeDuration);
            return true;
        }

        public bool BeginCharge()
        {
            if (!IsHeld || isCharging || Time.time - strikeStartedAt < tuning.rockStrikeDuration)
                return false;
            isCharging = true;
            chargeStartedAt = Time.time;
            return true;
        }

        public void CancelCharge() => isCharging = false;

        public bool ReleaseThrow(TopDownPlayer player, Vector2 direction)
        {
            if (owner != player || !isCharging) return false;
            LastThrowCharge01 = Charge01;
            LastThrowRange = Mathf.Lerp(tuning.rockMinThrowRange,
                tuning.rockMaxThrowRange, LastThrowCharge01);
            flightSpeed = Mathf.Lerp(tuning.rockMinThrowSpeed,
                tuning.rockThrowSpeed, LastThrowCharge01);
            flightDuration = LastThrowRange / flightSpeed;
            isCharging = false;
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
            isFlying = true;
            isBuried = false;
            flightElapsed = 0f;
            flightDirection = direction;
            strikeStartedAt = float.NegativeInfinity;
            ResetVisual();
            if (shadow != null) shadow.gameObject.SetActive(true);
            if (buriedMark != null) buriedMark.gameObject.SetActive(false);
            if (renderer != null) renderer.sortingOrder = 13;
            Hitbox.enabled = true;
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = 0f;
            Body.linearVelocity = direction * flightSpeed;
            Body.angularVelocity = 0f;
            Body.WakeUp();
            return true;
        }

        // Kept for scripted throws and existing scene tests.
        public void Throw(TopDownPlayer player, Vector2 direction)
        {
            if (!BeginCharge()) return;
            ReleaseThrow(player, direction);
        }

        public void Drop(TopDownPlayer player)
        {
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            transform.rotation = Quaternion.identity;
            owner = null;
            isCharging = false;
            isFlying = false;
            isBuried = false;
            flightElapsed = 0f;
            strikeStartedAt = float.NegativeInfinity;
            Hitbox.enabled = true;
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = 0f;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            SetLooseGroundState();
        }

        private void UpdateFlightVisual()
        {
            float progress = Mathf.Clamp01(flightElapsed / flightDuration);
            float arc = 4f * progress * (1f - progress);
            FlightHeight = arc * tuning.rockThrowHeight;
            visual.localPosition = visualRest + Vector3.up * FlightHeight;
            visual.localScale = visualRestScale;
            visual.localRotation = Quaternion.identity;

            if (shadow == null) return;
            float scale = Mathf.Lerp(1f, tuning.rockShadowApexScale, arc);
            shadow.localScale = shadowRestScale * scale;
            if (shadowRenderer != null)
            {
                Color color = shadowRestColor;
                color.a = Mathf.Lerp(shadowRestColor.a, shadowRestColor.a * 0.38f, arc);
                shadowRenderer.color = color;
            }
        }

        private void Land()
        {
            if (isFlying)
                EnemyAttraction.Emit(Body.position, tuning.rockAttractionRadius,
                    tuning.wallMask, true);
            PlaceBuried();
        }

        private void SetLooseGroundState()
        {
            Body.bodyType = RigidbodyType2D.Dynamic;
            ResetVisual();
            if (renderer != null) renderer.sortingOrder = 1;
            if (shadow != null)
            {
                shadow.gameObject.SetActive(true);
                shadow.localScale = shadowRestScale;
                if (shadowRenderer != null) shadowRenderer.color = shadowRestColor;
            }
            if (buriedMark != null) buriedMark.gameObject.SetActive(false);
        }

        private void ResetVisual()
        {
            FlightHeight = 0f;
            if (visual == null) return;
            visual.gameObject.SetActive(true);
            visual.localPosition = visualRest;
            visual.localScale = visualRestScale;
            visual.localRotation = Quaternion.identity;
        }

        private void CacheVisualState()
        {
            if (visual != null)
            {
                renderer = visual.GetComponentInChildren<SpriteRenderer>();
                visualRest = visual.localPosition;
                visualRestScale = visual.localScale;
            }
            if (shadow != null)
            {
                shadowRenderer = shadow.GetComponentInChildren<SpriteRenderer>();
                shadowRestScale = shadow.localScale;
                if (shadowRenderer != null) shadowRestColor = shadowRenderer.color;
            }
        }

        private bool ShouldStartBuried()
        {
            if (isBuried) return true;
            if (GetComponentInParent<EnemyWeaponLoadout>(true) != null) return false;
            return !name.StartsWith("Carried rock", System.StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureGroundedSprite()
        {
            if (buriedMark == null) return;
            SpriteRenderer groundedRenderer = buriedMark.GetComponentInChildren<SpriteRenderer>(true);
            if (groundedRenderer == null) return;

            Sprite groundedSprite = LoadLargestResourceSprite(GroundedResourcePath);
            if (groundedSprite == null) return;
            groundedRenderer.sprite = groundedSprite;
            groundedRenderer.color = Color.white;
            groundedRenderer.sortingOrder = 3;
            groundedRenderer.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            groundedRenderer.transform.localScale = new Vector3(0.29f, 0.29f, 1f);
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

        private void ApplyStrike()
        {
            RaycastHit2D hit = Physics2D.CircleCast(owner.transform.position, tuning.rockStrikeRadius,
                strikeDirection, tuning.rockStrikeReach, tuning.punchMask);
            if (!hit) return;
            ImpactsMade++;
            bool hitReceiver = false;
            foreach (MonoBehaviour behaviour in hit.collider.GetComponentsInParent<MonoBehaviour>())
                if (behaviour is IPunchReceiver receiver)
                {
                    receiver.ReceivePunch(strikeDirection, tuning.rockStrikeForce);
                    hitReceiver = true;
                }
            if (!hitReceiver)
                ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
        }

        private bool HitEnemy(Collider2D collider)
        {
            PatrolEnemy enemy = collider.GetComponentInParent<PatrolEnemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(1, flightDirection);
                return true;
            }
            FantasyEnemy fantasyEnemy = collider.GetComponentInParent<FantasyEnemy>();
            if (fantasyEnemy == null) return false;
            fantasyEnemy.TakeDamage(1, flightDirection);
            return true;
        }
    }
}
