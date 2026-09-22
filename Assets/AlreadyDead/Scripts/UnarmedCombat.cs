using UnityEngine;

namespace AlreadyDead
{
    public interface IPunchReceiver
    {
        void ReceivePunch(Vector2 direction, float force);
    }

    public sealed class UnarmedCombat : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform leftFist;
        [SerializeField] private Transform rightFist;
        [SerializeField] private AimCamera aimCamera;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;
        [SerializeField] private Vector3 weaponArmLocalPosition = new Vector3(0.21f, -0.22f, 0f);
        [SerializeField] private float weaponArmAngle = -90f;

        private Vector3 leftRest;
        private Vector3 rightRest;
        private Quaternion leftRestRotation;
        private Quaternion rightRestRotation;
        private float punchStartedAt = float.NegativeInfinity;
        private float nextPunchTime;
        private bool useLeft = true;
        private bool impactApplied;
        private Vector2 punchDirection = Vector2.right;

        public bool Available { get; private set; } = true;
        public bool IsPunching => Time.time - punchStartedAt < tuning.punchDuration;
        public int PunchCount { get; private set; }
        public int ImpactCount { get; private set; }
        public bool LastPunchUsedLeft { get; private set; }
        public int VisibleArmCount => (leftFist.gameObject.activeSelf ? 1 : 0) +
            (rightFist.gameObject.activeSelf ? 1 : 0);
        public bool WeaponArmRaised => !Available && rightFist.gameObject.activeSelf;
        public Transform LeftArm => leftFist;
        public Transform RightArm => rightFist;

        public void Configure(PrototypeTuning settings, Transform left, Transform right, AimCamera view,
            Sprite sprite, Material material)
        {
            tuning = settings;
            leftFist = left;
            rightFist = right;
            aimCamera = view;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            leftRest = left.localPosition;
            rightRest = right.localPosition;
            leftRestRotation = left.localRotation;
            rightRestRotation = right.localRotation;
            ApplyModePose();
        }

        private void Awake()
        {
            leftRest = leftFist.localPosition;
            rightRest = rightFist.localPosition;
            leftRestRotation = leftFist.localRotation;
            rightRestRotation = rightFist.localRotation;
            ApplyModePose();
        }

        private void Update()
        {
            if (!Available || !IsPunching) return;
            float progress = Mathf.Clamp01((Time.time - punchStartedAt) / tuning.punchDuration);
            float extension = progress < 0.38f
                ? progress / 0.38f
                : 1f - (progress - 0.38f) / 0.62f;
            extension = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(extension));
            Transform active = LastPunchUsedLeft ? leftFist : rightFist;
            Vector3 rest = LastPunchUsedLeft ? leftRest : rightRest;
            Quaternion restRotation = LastPunchUsedLeft ? leftRestRotation : rightRestRotation;
            active.localPosition = rest + Vector3.right * (tuning.punchReach * 0.62f * extension);
            active.localRotation = restRotation *
                Quaternion.Euler(0f, 0f, (LastPunchUsedLeft ? -12f : 12f) * extension);

            if (!impactApplied && progress >= 0.32f)
            {
                impactApplied = true;
                ApplyImpact();
            }

            if (progress >= 1f) ResetPose();
        }

        public bool TryPunch(Vector2 direction)
        {
            if (!Available || Time.time < nextPunchTime) return false;
            punchDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            LastPunchUsedLeft = useLeft;
            useLeft = !useLeft;
            punchStartedAt = Time.time;
            nextPunchTime = Time.time + tuning.punchInterval;
            impactApplied = false;
            PunchCount++;
            aimCamera.Kick(tuning.punchShakeStrength, tuning.punchShakeDuration);
            return true;
        }

        public void SetAvailable(bool value)
        {
            Available = value;
            ResetPose();
        }

        private void ApplyImpact()
        {
            Vector2 origin = transform.position;
            RaycastHit2D hit = Physics2D.CircleCast(origin, tuning.punchRadius, punchDirection,
                tuning.punchReach, tuning.punchMask);
            if (!hit) return;
            ImpactCount++;
            bool hitReceiver = false;
            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
                if (behaviour is IPunchReceiver receiver)
                {
                    receiver.ReceivePunch(punchDirection, tuning.punchForce);
                    hitReceiver = true;
                }
            if (!hitReceiver)
                ShotEffect.Impact(hit.point, hit.normal, primitiveSprite, primitiveMaterial);
        }

        private void ResetPose()
        {
            ApplyModePose();
        }

        private void ApplyModePose()
        {
            if (leftFist == null || rightFist == null) return;
            leftFist.gameObject.SetActive(Available);
            rightFist.gameObject.SetActive(true);
            leftFist.localPosition = leftRest;
            leftFist.localRotation = leftRestRotation;
            if (Available)
            {
                rightFist.localPosition = rightRest;
                rightFist.localRotation = rightRestRotation;
            }
            else
            {
                rightFist.localPosition = weaponArmLocalPosition;
                rightFist.localRotation = Quaternion.Euler(0f, 0f, weaponArmAngle);
            }
        }
    }
}
