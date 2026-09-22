using UnityEngine;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class ClubWeapon : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform swingRoot;
        [SerializeField] private SpriteRenderer highlight;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;

        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private TopDownPlayer owner;
        private PhysicsMaterial2D runtimeMaterial;
        private float swingStartedAt = float.NegativeInfinity;
        private float nextSwingTime;
        private Vector2 swingDirection = Vector2.right;
        private bool impactApplied;
        private bool trailApplied;

        public bool IsHeld => owner != null;
        public bool IsSwinging => Time.time - swingStartedAt < tuning.clubSwingDuration;
        public int SwingsMade { get; private set; }
        public int ImpactsMade { get; private set; }
        public int TrailBursts { get; private set; }
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public BoxCollider2D Hitbox => hitbox != null ? hitbox : hitbox = GetComponent<BoxCollider2D>();

        public void Configure(PrototypeTuning settings, Transform model, SpriteRenderer halo,
            Sprite sprite, Material material)
        {
            tuning = settings;
            swingRoot = model;
            highlight = halo;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            SetHighlighted(false);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            body.gravityScale = 0f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            runtimeMaterial = Instantiate(hitbox.sharedMaterial);
            runtimeMaterial.bounciness = tuning.throwBounce;
            hitbox.sharedMaterial = runtimeMaterial;
            SetHighlighted(false);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        private void Update()
        {
            if (!IsHeld || !IsSwinging)
            {
                if (swingRoot != null) swingRoot.localRotation = Quaternion.identity;
                return;
            }

            float progress = Mathf.Clamp01((Time.time - swingStartedAt) / tuning.clubSwingDuration);
            float sweep = Mathf.SmoothStep(0f, 1f, progress);
            float angle = Mathf.Lerp(-115f, 70f, sweep);
            swingRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            float radians = angle * Mathf.Deg2Rad;
            owner.Unarmed.SetWeaponArmPose(new Vector3(0.09f + Mathf.Cos(radians) * 0.13f,
                -0.14f + Mathf.Sin(radians) * 0.13f, 0f), -90f + angle);

            if (!trailApplied && progress >= 0.3f)
            {
                trailApplied = true;
                SpawnPixelTrail(angle);
            }
            if (!impactApplied && progress >= 0.55f)
            {
                impactApplied = true;
                ApplyImpact();
            }
            if (progress >= 0.999f)
            {
                swingRoot.localRotation = Quaternion.identity;
                owner.Unarmed.ResetWeaponArmPose();
            }
        }

        public void SetHighlighted(bool value)
        {
            if (highlight != null) highlight.enabled = value && !IsHeld;
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
            swingRoot.localRotation = Quaternion.identity;
            SetHighlighted(false);
        }

        public void Throw(TopDownPlayer player, Vector2 direction)
        {
            Vector2 heading = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            transform.SetParent(null, true);
            transform.position = (Vector2)player.transform.position + heading * 0.75f;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            owner.Unarmed.ResetWeaponArmPose();
            owner = null;
            swingStartedAt = float.NegativeInfinity;
            swingRoot.localRotation = Quaternion.identity;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = transform.eulerAngles.z;
            Body.linearVelocity = heading * tuning.throwSpeed;
            Body.angularVelocity = tuning.throwSpin * (Random.value < 0.5f ? -1f : 1f);
            Body.WakeUp();
        }

        public void Drop(TopDownPlayer player)
        {
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            owner.Unarmed.ResetWeaponArmPose();
            owner = null;
            swingStartedAt = float.NegativeInfinity;
            swingRoot.localRotation = Quaternion.identity;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            SetHighlighted(false);
        }

        public bool TrySwing(Vector2 direction)
        {
            if (!IsHeld || Time.time < nextSwingTime) return false;
            swingDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            swingStartedAt = Time.time;
            nextSwingTime = Time.time + tuning.clubSwingInterval;
            impactApplied = false;
            trailApplied = false;
            SwingsMade++;
            owner.View.Kick(tuning.punchShakeStrength * 1.2f, tuning.punchShakeDuration);
            return true;
        }

        private void ApplyImpact()
        {
            Vector2 origin = owner.transform.position;
            RaycastHit2D hit = Physics2D.CircleCast(origin, tuning.clubRadius, swingDirection,
                tuning.clubReach, tuning.wallMask | tuning.enemyMask);
            if (!hit) return;
            ImpactsMade++;
            PatrolEnemy patrol = hit.collider.GetComponentInParent<PatrolEnemy>();
            if (patrol != null) patrol.TakeDamage(tuning.clubDamage, swingDirection);
            else if (hit.collider.GetComponentInParent<FantasyEnemy>() is FantasyEnemy fantasy)
                fantasy.TakeDamage(tuning.clubDamage, swingDirection);
            else
                ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
        }

        private void SpawnPixelTrail(float currentAngle)
        {
            if (primitiveSprite == null || transform.parent == null) return;
            var trail = new GameObject("Club pixel swing trail");
            trail.transform.SetParent(transform.parent, false);
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float angle = (currentAngle - Mathf.Lerp(82f, 10f, t)) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(0.58f, 1.02f, t);
                var pixel = new GameObject("Trail pixel " + i);
                pixel.transform.SetParent(trail.transform, false);
                pixel.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                pixel.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.18f, t);
                SpriteRenderer renderer = pixel.AddComponent<SpriteRenderer>();
                renderer.sprite = primitiveSprite;
                renderer.sharedMaterial = primitiveMaterial;
                renderer.color = new Color(1f, Mathf.Lerp(0.35f, 0.78f, t), 0.12f,
                    Mathf.Lerp(0.18f, 0.72f, t));
                renderer.sortingOrder = 12;
            }
            TrailBursts++;
            Destroy(trail, 0.13f);
        }
    }
}
