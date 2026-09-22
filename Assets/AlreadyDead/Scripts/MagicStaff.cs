using UnityEngine;

namespace AlreadyDead
{
    public enum MagicElement { Fire, Frost, Lightning }

    public interface IMagicDamageable
    {
        void TakeMagicDamage(int amount, MagicElement element, Vector2 direction);
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class MagicStaff : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private MagicElement selectedElement;
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer highlight;
        [SerializeField] private Sprite primitiveSprite;
        [SerializeField] private Material primitiveMaterial;

        private TopDownPlayer owner;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private float nextCastTime;

        public MagicElement Element => selectedElement;
        public MagicElement SelectedElement => selectedElement;
        public bool IsHeld => owner != null;
        public int CastsMade { get; private set; }
        public int LastVolleyCount { get; private set; }
        public int LastLightningTargets { get; private set; }
        public Vector2 LastCastDirection { get; private set; }
        public string DisplayName => selectedElement == MagicElement.Fire ? "ПОСОХ • ОГОНЬ [1]" :
            selectedElement == MagicElement.Frost ? "ПОСОХ • ЛЁД [2]" : "ПОСОХ • МОЛНИЯ [3]";

        public void Configure(PrototypeTuning settings, MagicElement magicElement, Transform model,
            SpriteRenderer halo, Sprite sprite, Material material)
        {
            tuning = settings;
            selectedElement = magicElement;
            visual = model;
            highlight = halo;
            primitiveSprite = sprite;
            primitiveMaterial = material;
            SetHighlighted(false);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        public void SetHighlighted(bool value)
        {
            if (highlight != null) highlight.enabled = value && !IsHeld;
        }

        public void SelectElement(MagicElement value)
        {
            selectedElement = value;
        }

        public void Equip(Transform socket, TopDownPlayer player)
        {
            owner = player;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            hitbox.enabled = false;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (visual != null) visual.localPosition = Vector3.zero;
            SetHighlighted(false);
        }

        public void Drop(TopDownPlayer player)
        {
            transform.SetParent(null, true);
            transform.position = player.transform.position;
            transform.rotation = Quaternion.identity;
            owner = null;
            if (visual != null) visual.localPosition = Vector3.zero;
            hitbox.enabled = true;
            body.simulated = true;
            body.position = transform.position;
            body.linearVelocity = Vector2.zero;
            SetHighlighted(false);
        }

        public bool TryCast(Vector2 aimDirection, AimCamera camera)
        {
            if (!IsHeld || tuning == null || Time.time < nextCastTime) return false;
            Vector2 direction = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
            float interval = selectedElement == MagicElement.Fire ? tuning.fireCastInterval :
                selectedElement == MagicElement.Frost ? tuning.frostCastInterval : tuning.lightningCastInterval;
            nextCastTime = Time.time + interval;

            Vector2 origin = owner.transform.position;
            Vector2 castPoint = origin + direction * 0.65f;
            RaycastHit2D blocked = Physics2D.CircleCast(origin, tuning.magicProjectileRadius,
                direction, 0.65f, tuning.wallMask);
            if (blocked)
            {
                MagicProjectile.Impact(blocked.point, selectedElement, primitiveSprite, primitiveMaterial);
                LastVolleyCount = 0;
                LastLightningTargets = 0;
            }
            else if (selectedElement == MagicElement.Fire)
            {
                MagicProjectile.Spawn(castPoint, direction, MagicElement.Fire,
                    tuning, primitiveSprite, primitiveMaterial);
                LastVolleyCount = 1;
                LastLightningTargets = 0;
            }
            else if (selectedElement == MagicElement.Frost)
            {
                int minimum = Mathf.Min(tuning.frostShardMin, tuning.frostShardMax);
                int maximum = Mathf.Max(tuning.frostShardMin, tuning.frostShardMax);
                LastVolleyCount = Random.Range(minimum, maximum + 1);
                for (int i = 0; i < LastVolleyCount; i++)
                {
                    float normalized = LastVolleyCount <= 1 ? 0f : i / (float)(LastVolleyCount - 1);
                    float angle = Mathf.Lerp(-tuning.frostSpreadHalfAngle,
                        tuning.frostSpreadHalfAngle, normalized) + Random.Range(-2.5f, 2.5f);
                    Vector2 shardDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                    MagicProjectile.Spawn(castPoint, shardDirection, MagicElement.Frost,
                        tuning, primitiveSprite, primitiveMaterial);
                }
                LastLightningTargets = 0;
            }
            else
            {
                LastVolleyCount = 0;
                LastLightningTargets = MagicProjectile.CastLightning(castPoint, direction,
                    tuning, primitiveSprite, primitiveMaterial);
            }

            CastsMade++;
            LastCastDirection = direction;
            if (camera != null) camera.Kick(selectedElement == MagicElement.Lightning ? 0.09f : 0.055f,
                selectedElement == MagicElement.Lightning ? 0.13f : 0.09f);
            return true;
        }
    }
}
