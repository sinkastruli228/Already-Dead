using UnityEngine;

namespace AlreadyDead
{
    public sealed class PlayerLimbAnimator : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Transform facing;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;

        private Vector3 leftRest;
        private Vector3 rightRest;
        private float phase;

        public Transform LeftLeg => leftLeg;
        public Transform RightLeg => rightLeg;
        public float LeftExtension { get; private set; }
        public float RightExtension { get; private set; }

        public void Configure(PrototypeTuning settings, Rigidbody2D playerBody, Transform aimRoot,
            Transform left, Transform right)
        {
            tuning = settings;
            body = playerBody;
            facing = aimRoot;
            leftLeg = left;
            rightLeg = right;
            leftRest = left.localPosition;
            rightRest = right.localPosition;
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (leftLeg != null) leftRest = leftLeg.localPosition;
            if (rightLeg != null) rightRest = rightLeg.localPosition;
        }

        private void LateUpdate()
        {
            if (tuning == null || body == null || facing == null || leftLeg == null || rightLeg == null) return;

            Vector2 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude < 0.01f)
            {
                phase = 0f;
                LeftExtension = 0f;
                RightExtension = 0f;
                ResetLeg(leftLeg, leftRest);
                ResetLeg(rightLeg, rightRest);
                return;
            }

            phase += Time.deltaTime * tuning.legStepRate * Mathf.Clamp(velocity.magnitude / tuning.moveSpeed, 0.5f, 1.35f);
            Vector2 localDirection = facing.InverseTransformVector(velocity.normalized);
            float leftPulse = Mathf.Max(0f, Mathf.Sin(phase));
            float rightPulse = Mathf.Max(0f, -Mathf.Sin(phase));
            LeftExtension = leftPulse * tuning.legStepDistance;
            RightExtension = rightPulse * tuning.legStepDistance;
            PoseLeg(leftLeg, leftRest, localDirection, LeftExtension);
            PoseLeg(rightLeg, rightRest, localDirection, RightExtension);
        }

        private static void PoseLeg(Transform leg, Vector3 rest, Vector2 direction, float extension)
        {
            Vector3 localDirection = new Vector3(direction.x, direction.y, 0f);
            leg.localPosition = rest + localDirection * extension;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            leg.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void ResetLeg(Transform leg, Vector3 rest)
        {
            leg.localPosition = rest;
            leg.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }
    }
}
