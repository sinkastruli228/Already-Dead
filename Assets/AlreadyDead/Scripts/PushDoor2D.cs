using UnityEngine;

namespace AlreadyDead
{
    // A door pivots around its left end. Its collider is on the wall layer, so a
    // closed leaf also blocks enemy sight rays; pushing it rotates the real collider.
    [RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D), typeof(BoxCollider2D))]
    public sealed class PushDoor2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private BoxCollider2D leaf;
        private float lastPushAt = float.NegativeInfinity;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            leaf = GetComponent<BoxCollider2D>();
            body.gravityScale = 0f;
            body.mass = 0.45f;
            body.linearDamping = 5f;
            body.angularDamping = 4f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            HingeJoint2D hinge = GetComponent<HingeJoint2D>();
            hinge.connectedBody = null;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = transform.position;
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D { min = -105f, max = 105f };
        }

        public void PushFrom(Vector2 source, Vector2 direction, float impulse)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            if (Time.time - lastPushAt > 0.65f) GameAudio.PlayDoor();
            lastPushAt = Time.time;
            Vector2 tip = transform.TransformPoint(leaf.offset +
                new Vector2(leaf.size.x * 0.42f, 0f));
            Vector2 force = direction.normalized;
            Vector2 lever = tip - body.position;
            if (Mathf.Abs(lever.x * force.y - lever.y * force.x) < 0.08f)
            {
                Vector2 normal = new Vector2(-lever.y, lever.x).normalized;
                force = normal * (Vector2.Dot(source - body.position, normal) > 0f ? -1f : 1f);
            }
            body.AddForceAtPosition(force * impulse, tip, ForceMode2D.Impulse);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TopDownPlayer player = collision.gameObject.GetComponentInParent<TopDownPlayer>();
            if (player != null)
            {
                Vector2 direction = player.Body.linearVelocity;
                if (direction.sqrMagnitude > 0.01f &&
                    Vector2.Dot(direction, body.position - player.Body.position) > 0.02f)
                    PushFrom(player.Body.position, direction, 0.08f);
                return;
            }
            if (collision.gameObject.GetComponentInParent<PatrolEnemy>() == null &&
                collision.gameObject.GetComponentInParent<FantasyEnemy>() == null) return;
            Vector2 source = collision.transform.position;
            PushFrom(source, (Vector2)transform.position - source, 0.08f);
        }
    }
}
