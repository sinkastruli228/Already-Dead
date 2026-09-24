using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AlreadyDead
{
    // A six-shot enemy gun. Its rounds damage the player and never ricochet.
    public sealed class EnemyRevolver : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform muzzle;
        [SerializeField] private SpriteRenderer muzzleFlash;
        [SerializeField] private Sprite fallbackSprite;
        [SerializeField] private Material fallbackMaterial;

        private Light2D shotLight;
        private float nextShotTime;
        private float flashUntil;
        private float reloadUntil;
        private int rounds = 6;

        public const float FireRange = 8f;
        public int Rounds => rounds;
        public bool Reloading => Time.time < reloadUntil;
        public int ShotsFired { get; private set; }

        public void Configure(PrototypeTuning settings, Transform barrel,
            SpriteRenderer flash, Sprite sprite, Material material)
        {
            tuning = settings;
            muzzle = barrel;
            muzzleFlash = flash;
            fallbackSprite = sprite;
            fallbackMaterial = material;
        }

        private void Awake()
        {
            shotLight = FirearmVisuals.PrepareMuzzle(muzzleFlash, muzzle, 6.3f);
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        private void Update()
        {
            if (rounds == 0 && Time.time >= reloadUntil) rounds = 6;
            if (muzzleFlash != null) muzzleFlash.enabled = Time.time < flashUntil;
            FirearmVisuals.UpdateMuzzleLight(shotLight, true, flashUntil, 0.045f, 11.2f);
        }

        public bool TryFire(Vector2 direction)
        {
            if (tuning == null || muzzle == null || rounds == 0 ||
                Time.time < nextShotTime || Time.time < reloadUntil) return false;

            Vector2 heading = Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f)) * direction.normalized;
            Vector2 origin = transform.position;
            Vector2 barrel = muzzle.position;
            Vector2 reach = barrel - origin;
            RaycastHit2D blocked = Physics2D.CircleCast(origin, tuning.bulletRadius,
                reach.normalized, reach.magnitude, tuning.wallMask);
            if (blocked)
            {
                PushDoor2D door = blocked.collider.GetComponentInParent<PushDoor2D>();
                if (door != null) door.PushFrom(origin, heading, 1.8f);
                ShotEffect.Impact(blocked.point, blocked.normal, fallbackSprite, fallbackMaterial);
            }
            else
                Projectile.Spawn(barrel, heading, tuning, fallbackSprite, fallbackMaterial,
                    projectileName: "Enemy revolver bullet", hostileToPlayer: true,
                    source: transform);

            rounds--;
            ShotsFired++;
            nextShotTime = Time.time + 0.65f;
            if (rounds == 0) reloadUntil = Time.time + 2f;
            flashUntil = Time.time + 0.045f;
            if (muzzleFlash != null) muzzleFlash.enabled = true;
            return true;
        }
    }
}
