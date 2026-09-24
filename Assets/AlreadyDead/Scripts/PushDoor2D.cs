using UnityEngine;

namespace AlreadyDead
{
    // A door pivots around its left end. Its collider is on the wall layer, so a
    // closed leaf also blocks enemy sight rays; pushing it rotates the real collider.
    [RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D), typeof(BoxCollider2D))]
    public sealed class PushDoor2D : MonoBehaviour
    {
        [SerializeField] private float closingTorque = 2.8f;
        [SerializeField] private float closingDamping = 1.4f;

        private Rigidbody2D body;
        private float closedAngle;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            closedAngle = body.rotation;
            body.gravityScale = 0f;
            body.mass = 0.45f;
            body.linearDamping = 5f;
            body.angularDamping = 1.4f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            HingeJoint2D hinge = GetComponent<HingeJoint2D>();
            hinge.connectedBody = null;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = transform.position;
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D { min = -105f, max = 105f };
        }

        private void FixedUpdate()
        {
            float fromClosed = Mathf.DeltaAngle(closedAngle, body.rotation);
            body.AddTorque((-fromClosed * closingTorque - body.angularVelocity * closingDamping) *
                Mathf.Deg2Rad);
        }
    }
}
