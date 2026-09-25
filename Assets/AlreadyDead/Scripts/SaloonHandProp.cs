using UnityEngine;

namespace AlreadyDead
{
    // Visual held prop on the enemy's rotating facing transform. No extra collider
    // is needed: the existing PatrolEnemy hitbox and attack logic remain in charge.
    public sealed class SaloonHandProp : MonoBehaviour
    {
        public enum PropKind { Knife, Beer }

        [SerializeField] private SpriteRenderer display;
        [SerializeField] private Sprite knife;
        [SerializeField] private Sprite beer;
        [SerializeField] private bool forceKnife;

        private float swingStartedAt = float.NegativeInfinity;

        public PropKind Kind { get; private set; }

        public void Configure(SpriteRenderer renderer, Sprite knifeSprite, Sprite beerSprite,
            bool alwaysKnife = false)
        {
            display = renderer;
            knife = knifeSprite;
            beer = beerSprite;
            forceKnife = alwaysKnife;
        }

        private void Awake()
        {
            if (GetComponent<EnemyRevolver>() != null)
            {
                if (display != null) display.enabled = false;
                return;
            }
            Equip(forceKnife || Random.Range(0, 2) == 0 ? PropKind.Knife : PropKind.Beer);
        }

        private void Update()
        {
            if (display == null || Kind != PropKind.Knife) return;
            float progress = (Time.time - swingStartedAt) / 0.22f;
            float swing = progress >= 0f && progress < 1f
                ? Mathf.Sin(progress * Mathf.PI) * 65f : 0f;
            display.transform.localRotation = Quaternion.Euler(0f, 0f, -90f + swing);
        }

        public void PlayAttack() => swingStartedAt = Time.time;

        public void Equip(PropKind kind)
        {
            Kind = kind;
            if (display == null) return;
            display.sprite = kind == PropKind.Knife ? knife : beer;
            display.transform.localPosition = kind == PropKind.Knife
                ? new Vector3(0.20f, -0.17f, -0.02f)
                : new Vector3(0.20f, -0.43f, -0.02f);
            display.transform.localRotation = Quaternion.Euler(0f, 0f,
                kind == PropKind.Knife ? -90f : 0f);
            display.transform.localScale = Vector3.one *
                (kind == PropKind.Knife ? 0.182f : 0.14f);
        }
    }
}
