using UnityEngine;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PistolWeapon : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform visual;
        [SerializeField] private Transform muzzle;
        [SerializeField] private SpriteRenderer highlight;
        [SerializeField] private SpriteRenderer muzzleFlash;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private TopDownPlayer owner;
        private float nextShotTime;
        private float recoil;
        private float flashUntil;
        private PhysicsMaterial2D runtimeMaterial;

        public bool IsHeld => owner != null;
        public float Recoil => recoil;
        public int ShotsFired { get; private set; }
        public Vector2 LastShotDirection { get; private set; }
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public BoxCollider2D Hitbox => hitbox != null ? hitbox : hitbox = GetComponent<BoxCollider2D>();
        public Vector2 MuzzlePosition => muzzle.position;

        public void Configure(PrototypeTuning settings, Transform model, Transform barrel,
            SpriteRenderer halo, SpriteRenderer flash, Sprite sprite, Material material)
        {
            tuning = settings;
            visual = model;
            muzzle = barrel;
            highlight = halo;
            muzzleFlash = flash;
            primitiveSprite = sprite;
            primitiveMaterial = material;
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
            muzzleFlash.enabled = false;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        private void Update()
        {
            recoil = Mathf.MoveTowards(recoil, 0f, tuning.recoilReturnSpeed * Time.deltaTime);
            visual.localPosition = Vector3.left * recoil;
            muzzleFlash.enabled = IsHeld && Time.time < flashUntil;
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
            recoil = 0f;
            visual.localPosition = Vector3.zero;
            SetHighlighted(false);
        }

        public void Throw(TopDownPlayer player, Vector2 direction)
        {
            // Start within the player's free space; sweep the WHOLE gun's radius to
            // prevent a hand or barrel intersecting a wall from spawning it through it.
            Vector2 origin = player.transform.position;
            const float clearance = 0.43f;
            const float desiredDistance = 0.8f;
            RaycastHit2D obstruction = Physics2D.CircleCast(origin, clearance, direction,
                desiredDistance, tuning.wallMask);
            float distance = obstruction ? Mathf.Max(0f, obstruction.distance - 0.04f) : desiredDistance;
            transform.SetParent(null, true);
            transform.position = origin + direction * distance;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            recoil = 0f;
            visual.localPosition = Vector3.zero;
            owner = null;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = transform.eulerAngles.z;
            Body.linearVelocity = direction * tuning.throwSpeed;
            Body.angularVelocity = tuning.throwSpin * (Random.value < 0.5f ? -1f : 1f);
            Body.WakeUp();
            muzzleFlash.enabled = false;
        }

        public bool TryFire(Vector2 aimDirection, AimCamera camera)
        {
            if (!IsHeld || Time.time < nextShotTime) return false;
            nextShotTime = Time.time + tuning.shotInterval;
            float angle = Random.Range(-tuning.spreadHalfAngle, tuning.spreadHalfAngle);
            Vector2 shotDirection = Quaternion.Euler(0, 0, angle) * aimDirection.normalized;
            Vector2 origin = owner.transform.position;
            Vector2 barrel = muzzle.position;
            Vector2 barrelDelta = barrel - origin;
            // A muzzle on the other side of a wall must never fire through it.
            RaycastHit2D blocked = Physics2D.CircleCast(origin, tuning.bulletRadius, barrelDelta.normalized,
                barrelDelta.magnitude, tuning.wallMask);
            if (blocked)
                ShotEffect.Impact(blocked.point, blocked.normal, primitiveSprite, primitiveMaterial);
            else
                Projectile.Spawn(barrel, shotDirection, tuning, primitiveSprite, primitiveMaterial);

            LastShotDirection = shotDirection;
            ShotsFired++;
            recoil = tuning.recoilDistance;
            visual.localPosition = Vector3.left * recoil;
            flashUntil = Time.time + 0.045f;
            muzzleFlash.enabled = true;
            camera.Kick();
            return true;
        }
    }
}
