using UnityEngine;

namespace AlreadyDead
{
    public enum EnemyWeaponKind { Empty, Rock, Spear, Club }

    // Each enemy carries one of the existing pickup objects. The unused choices stay inactive.
    public sealed class EnemyWeaponLoadout : MonoBehaviour
    {
        [SerializeField] private bool guaranteedSpear;
        [SerializeField] private RockWeapon rock;
        [SerializeField] private SpearWeapon spear;
        [SerializeField] private ClubWeapon club;

        private GameObject equipped;
        private Vector3 gripPosition;
        private float attackStartedAt = float.NegativeInfinity;
        private float spearWindup01;

        public EnemyWeaponKind Kind { get; private set; }
        public GameObject Equipped => equipped;
        public bool CanThrowSpear => Kind == EnemyWeaponKind.Spear && equipped != null;
        public float SpearThrowRange => CanThrowSpear ? spear.HalfChargeThrowRange : 0f;
        public float AttackRangeBonus => Kind == EnemyWeaponKind.Spear ? 0.65f :
            Kind == EnemyWeaponKind.Club ? 0.18f : 0f;
        public int AttackDamage => Kind == EnemyWeaponKind.Spear || Kind == EnemyWeaponKind.Club ? 2 : 1;

        public void Configure(bool forceSpear, RockWeapon rockChoice, SpearWeapon spearChoice,
            ClubWeapon clubChoice)
        {
            guaranteedSpear = forceSpear;
            rock = rockChoice;
            spear = spearChoice;
            club = clubChoice;
        }

        private void Awake()
        {
            if (GetComponent<EnemyRevolver>() != null || GetComponent<EnemyGlock>() != null)
            {
                Kind = EnemyWeaponKind.Empty;
                equipped = null;
                return;
            }
            Kind = guaranteedSpear ? EnemyWeaponKind.Spear :
                (EnemyWeaponKind)Random.Range(0, 4);
            equipped = Kind switch
            {
                EnemyWeaponKind.Rock => rock.gameObject,
                EnemyWeaponKind.Spear => spear.gameObject,
                EnemyWeaponKind.Club => club.gameObject,
                _ => null
            };
            if (equipped == null) return;
            equipped.SetActive(true);
            gripPosition = equipped.transform.localPosition;
            Rigidbody2D body = equipped.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            equipped.GetComponent<Collider2D>().enabled = false;
            if (Kind == EnemyWeaponKind.Rock) rock.Shadow.gameObject.SetActive(false);
            if (Kind == EnemyWeaponKind.Spear) spear.Shadow.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (equipped == null) return;
            if (Kind == EnemyWeaponKind.Spear && spearWindup01 > 0f)
            {
                equipped.transform.localPosition = gripPosition + Vector3.left * (0.35f * spearWindup01);
                equipped.transform.localRotation = Quaternion.Euler(0f, 0f, -35f * spearWindup01);
                return;
            }
            float progress = (Time.time - attackStartedAt) / 0.22f;
            float thrust = progress >= 0f && progress < 1f ? Mathf.Sin(progress * Mathf.PI) : 0f;
            equipped.transform.localPosition = gripPosition + Vector3.right * (0.3f * thrust);
            equipped.transform.localRotation = Kind == EnemyWeaponKind.Club
                ? Quaternion.Euler(0f, 0f, -65f * thrust) : Quaternion.identity;
        }

        public void PlayAttack() => attackStartedAt = Time.time;

        public void SetSpearWindup(float progress) => spearWindup01 = Mathf.Clamp01(progress);

        public bool TryThrowSpear(Vector2 origin, Vector2 direction)
        {
            if (!CanThrowSpear || !spear.ThrowFromEnemy(origin, direction, transform)) return false;
            spearWindup01 = 0f;
            equipped = null;
            Kind = EnemyWeaponKind.Empty;
            return true;
        }

        public void DropOnDeath()
        {
            spearWindup01 = 0f;
            if (equipped == null) return;
            GameObject dropped = equipped;
            equipped = null;
            dropped.transform.SetParent(null, true);
            dropped.transform.position = transform.position;
            dropped.transform.rotation = Quaternion.identity;
            if (Kind == EnemyWeaponKind.Rock) rock.Shadow.gameObject.SetActive(true);
            if (Kind == EnemyWeaponKind.Spear) spear.Shadow.gameObject.SetActive(true);
            dropped.GetComponent<Collider2D>().enabled = true;
            Rigidbody2D body = dropped.GetComponent<Rigidbody2D>();
            body.position = transform.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = true;
        }
    }
}
