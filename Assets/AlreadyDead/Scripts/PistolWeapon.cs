using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        [SerializeField] private SpriteRenderer groundView;
        [SerializeField] private SpriteRenderer heldView;
        [SerializeField] private bool automatic;
        [SerializeField] private int capacity = 17;
        [SerializeField] private bool revolver;
        [SerializeField] private float revolverReloadSeconds = 4f;
        [SerializeField] private bool infiniteAmmo;
        [SerializeField, Min(0)] private int extraRicochets;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private TopDownPlayer owner;
        private float nextShotTime;
        private float recoil;
        private float flashUntil;
        private PhysicsMaterial2D runtimeMaterial;
        private Light2D shotLight;
        private int cylinderAmmo;
        private float reloadStartedAt;
        private bool reloading;

        public bool IsHeld => owner != null;
        public float Recoil => recoil;
        public int ShotsFired { get; private set; }
        public int Capacity => capacity;
        public int RemainingAmmo => infiniteAmmo ? int.MaxValue :
            revolver ? cylinderAmmo : Mathf.Max(0, capacity - ShotsFired);
        public bool Automatic => automatic;
        public bool IsRevolver => revolver;
        public bool HasInfiniteAmmo => infiniteAmmo;
        public int RicochetCount => revolver ? 5 : extraRicochets;
        public bool IsReloading => reloading;
        public float ReloadProgress01 => reloading
            ? Mathf.Clamp01((Time.time - reloadStartedAt) / revolverReloadSeconds) : 0f;
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

        public void ConfigureFirearm(int rounds, bool firesAutomatically,
            SpriteRenderer groundSprite, SpriteRenderer heldSprite)
        {
            capacity = Mathf.Max(1, rounds);
            automatic = firesAutomatically;
            cylinderAmmo = capacity;
            groundView = groundSprite;
            heldView = heldSprite;
            SetHeldView(false);
        }

        public void ConfigureRevolver(SpriteRenderer groundSprite, SpriteRenderer heldSprite)
        {
            ConfigureFirearm(6, false, groundSprite, heldSprite);
            revolver = true;
            revolverReloadSeconds = 4f;
        }

        public void ConfigureSpecialAmmo(bool unlimited, int ricochets)
        {
            infiniteAmmo = unlimited;
            extraRicochets = Mathf.Max(0, ricochets);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            if (revolver) cylinderAmmo = capacity;
            body.gravityScale = 0f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            runtimeMaterial = Instantiate(hitbox.sharedMaterial);
            runtimeMaterial.bounciness = tuning.throwBounce;
            hitbox.sharedMaterial = runtimeMaterial;
            SetHighlighted(false);
            muzzleFlash.enabled = false;
            shotLight = FirearmVisuals.PrepareMuzzle(muzzleFlash, muzzle, 6.3f);
            SetHeldView(false);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        private void Update()
        {
            if (reloading && Time.time - reloadStartedAt >= revolverReloadSeconds)
            {
                cylinderAmmo = capacity;
                reloading = false;
            }
            recoil = Mathf.MoveTowards(recoil, 0f, tuning.recoilReturnSpeed * Time.deltaTime);
            visual.localPosition = Vector3.left * recoil;
            muzzleFlash.enabled = IsHeld && Time.time < flashUntil;
            FirearmVisuals.UpdateMuzzleLight(shotLight, IsHeld, flashUntil, 0.045f, 11.2f);
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
            SetHeldView(true);
            SetHighlighted(false);
        }

        public void Throw(TopDownPlayer player, Vector2 direction)
        {
            // Start within the player's free space; sweep the WHOLE gun's radius to
            // prevent a hand or barrel intersecting a wall from spawning it through it.
            Vector2 origin = player.transform.position;
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            float clearance = Mathf.Max(0.43f, Hitbox.size.magnitude * scale * 0.5f);
            float desiredDistance = Mathf.Max(0.8f, clearance + 0.1f);
            RaycastHit2D obstruction = Physics2D.CircleCast(origin, clearance, direction,
                desiredDistance, tuning.wallMask);
            float distance = obstruction ? Mathf.Max(0f, obstruction.distance - 0.04f) : desiredDistance;
            transform.SetParent(null, true);
            transform.position = origin + direction * distance;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            recoil = 0f;
            visual.localPosition = Vector3.zero;
            owner = null;
            SetHeldView(false);
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = transform.eulerAngles.z;
            Body.linearVelocity = direction * tuning.throwSpeed;
            Body.angularVelocity = tuning.throwSpin * (Random.value < 0.5f ? -1f : 1f);
            Body.WakeUp();
            muzzleFlash.enabled = false;
        }

        public void Drop(TopDownPlayer player)
        {
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            owner = null;
            SetHeldView(false);
            recoil = 0f;
            visual.localPosition = Vector3.zero;
            flashUntil = 0f;
            muzzleFlash.enabled = false;
            Hitbox.enabled = true;
            Body.simulated = true;
            Body.position = transform.position;
            Body.rotation = transform.eulerAngles.z;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            SetHighlighted(false);
        }

        public bool TryFire(Vector2 aimDirection, AimCamera camera)
        {
            if (!IsHeld || reloading || Time.time < nextShotTime) return false;
            if (RemainingAmmo == 0)
            {
                TryReload();
                return false;
            }
            nextShotTime = Time.time + (automatic ? tuning.m4ShotInterval : tuning.shotInterval);
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
                Projectile.Spawn(barrel, shotDirection, tuning, primitiveSprite, primitiveMaterial,
                    projectileName: revolver ? "Revolver bullet / five ricochets" :
                        extraRicochets > 0 ? "M4 ricochet bullet" : "Pistol bullet",
                    maxRicochets: RicochetCount);

            LastShotDirection = shotDirection;
            ShotsFired++;
            if (revolver && --cylinderAmmo == 0) TryReload();
            recoil = tuning.recoilDistance;
            visual.localPosition = Vector3.left * recoil;
            flashUntil = Time.time + 0.045f;
            muzzleFlash.enabled = true;
            camera.Kick();
            return true;
        }

        public bool TryReload()
        {
            if (!revolver || !IsHeld || reloading || cylinderAmmo >= capacity) return false;
            reloadStartedAt = Time.time;
            reloading = true;
            return true;
        }

        private void SetHeldView(bool held)
        {
            if (groundView != null) groundView.enabled = !held;
            if (heldView != null) heldView.enabled = held;
        }
    }
}
