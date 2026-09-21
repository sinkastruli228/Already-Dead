using UnityEngine;

namespace AlreadyDead
{
    public sealed class ShotEffect : MonoBehaviour
    {
        private Vector2 velocity;
        private float remaining = 0.22f;
        private SpriteRenderer visual;

        public static void Impact(Vector2 position, Vector2 normal, Sprite sprite, Material material)
        {
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject("Impact spark");
                go.transform.position = position + normal * 0.04f;
                go.transform.localScale = new Vector3(0.08f, 0.035f, 1f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 20;
                renderer.color = new Color(1f, 0.69f, 0.28f);
                ShotEffect effect = go.AddComponent<ShotEffect>();
                effect.visual = renderer;
                effect.velocity = (Vector2)(Quaternion.Euler(0, 0, Random.Range(-65f, 65f)) * normal)
                    * Random.Range(1.5f, 4f);
                go.transform.rotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(effect.velocity.y, effect.velocity.x) * Mathf.Rad2Deg);
            }
        }

        private void Update()
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
            remaining -= Time.deltaTime;
            Color color = visual.color;
            color.a = Mathf.Clamp01(remaining / 0.22f);
            visual.color = color;
            if (remaining <= 0) Destroy(gameObject);
        }
    }
}
