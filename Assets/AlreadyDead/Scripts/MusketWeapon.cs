using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class MusketWeapon : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform recoilRoot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private SpriteRenderer groundView;
        [SerializeField] private SpriteRenderer heldView;
        [SerializeField] private SpriteRenderer highlight;
        [SerializeField] private SpriteRenderer muzzleFlash;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;

        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private TopDownPlayer owner;
        private PhysicsMaterial2D runtimeMaterial;
        private float nextShotTime;
        private float recoil;
        private float flashUntil;
        private Light2D shotLight;

        public bool IsHeld => owner != null;
        public bool GroundViewVisible => groundView != null && groundView.enabled;
        public bool HeldViewVisible => heldView != null && heldView.enabled;
        public int ShotsFired { get; private set; }
        public int Capacity => 5;
        public int RemainingAmmo => Mathf.Max(0, Capacity - ShotsFired);
        public float ShotInterval => tuning.shotInterval * tuning.musketCooldownMultiplier;
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public BoxCollider2D Hitbox => hitbox != null ? hitbox : hitbox = GetComponent<BoxCollider2D>();

        public void Configure(PrototypeTuning settings, Transform model, Transform barrel,
            SpriteRenderer sideRenderer, SpriteRenderer topRenderer, SpriteRenderer halo,
            SpriteRenderer flash, Sprite sprite, Material material)
        {
            tuning = settings;
            recoilRoot = model;
            muzzle = barrel;
            groundView = sideRenderer;
            heldView = topRenderer;
            highlight = halo;
            muzzleFlash = flash;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            SetHeldView(false);
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
            SetHeldView(false);
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            shotLight = FirearmVisuals.PrepareMuzzle(muzzleFlash, muzzle, 8.4f);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        private void Update()
        {
            recoil = Mathf.MoveTowards(recoil, 0f, tuning.recoilReturnSpeed * Time.deltaTime);
            if (recoilRoot != null) recoilRoot.localPosition = Vector3.left * recoil;
            if (muzzleFlash != null) muzzleFlash.enabled = IsHeld && Time.time < flashUntil;
            FirearmVisuals.UpdateMuzzleLight(shotLight, IsHeld, flashUntil, 0.075f, 14f);
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
            recoilRoot.localPosition = Vector3.zero;
            SetHeldView(true);
            SetHighlighted(false);
        }

        public void Throw(TopDownPlayer player, Vector2 direction)
        {
            Vector2 heading = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 origin = player.transform.position;
            const float clearance = 0.56f;
            const float desiredDistance = 0.85f;
            RaycastHit2D obstruction = Physics2D.CircleCast(origin, clearance, heading,
                desiredDistance, tuning.wallMask);
            float distance = obstruction ? Mathf.Max(0f, obstruction.distance - 0.04f) : desiredDistance;
            transform.SetParent(null, true);
            transform.position = origin + heading * distance;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            owner = null;
            recoil = 0f;
            recoilRoot.localPosition = Vector3.zero;
            SetHeldView(false);
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = transform.eulerAngles.z;
            Body.linearVelocity = heading * tuning.throwSpeed;
            Body.angularVelocity = tuning.throwSpin * 0.55f * (Random.value < 0.5f ? -1f : 1f);
            Body.WakeUp();
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        public void Drop(TopDownPlayer player)
        {
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            owner = null;
            recoil = 0f;
            recoilRoot.localPosition = Vector3.zero;
            flashUntil = 0f;
            SetHeldView(false);
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            SetHighlighted(false);
        }

        public bool TryFire(Vector2 aimDirection, AimCamera camera)
        {
            if (!IsHeld || RemainingAmmo == 0 || Time.time < nextShotTime) return false;
            nextShotTime = Time.time + ShotInterval;
            Vector2 direction = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.right;
            Vector2 origin = owner.transform.position;
            Vector2 barrel = muzzle.position;
            Vector2 barrelDelta = barrel - origin;
            RaycastHit2D blocked = Physics2D.CircleCast(origin, tuning.bulletRadius,
                barrelDelta.normalized, barrelDelta.magnitude, tuning.wallMask);
            if (blocked)
            {
                PushDoor2D door = blocked.collider.GetComponentInParent<PushDoor2D>();
                if (door != null) door.PushFrom(origin, direction, 2.4f);
                ShotEffect.Impact(blocked.point, blocked.normal, primitiveSprite, primitiveMaterial);
            }
            else
                Projectile.Spawn(barrel, direction, tuning, primitiveSprite, primitiveMaterial,
                    tuning.musketDamage, "Musket bullet / lethal");

            ShotsFired++;
            recoil = tuning.musketRecoilDistance;
            recoilRoot.localPosition = Vector3.left * recoil;
            flashUntil = Time.time + 0.075f;
            if (muzzleFlash != null) muzzleFlash.enabled = true;
            if (camera != null)
                camera.Kick(tuning.shotShakeStrength * tuning.musketShakeMultiplier,
                    tuning.shotShakeDuration * 1.35f);
            return true;
        }

        private void SetHeldView(bool held)
        {
            if (groundView != null) groundView.enabled = !held;
            if (heldView != null) heldView.enabled = held;
        }
    }
}
