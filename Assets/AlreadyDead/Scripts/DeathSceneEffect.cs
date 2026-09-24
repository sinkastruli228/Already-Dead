using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AlreadyDead
{
    // On defeat, the stage fades away while the two characters remain fully visible.
    public sealed class DeathSceneEffect : MonoBehaviour
    {
        public const float FadeSeconds = 1.2f;

        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        private readonly List<Color> spriteColors = new List<Color>();
        private readonly List<Tilemap> tilemaps = new List<Tilemap>();
        private readonly List<Color> tileColors = new List<Color>();
        private readonly List<Renderer> otherRenderers = new List<Renderer>();
        private Camera view;
        private Color oldBackground;
        private float startedAt;
        private Material characterMaterial;

        public static void Begin(Transform player, Transform killer, Camera camera)
        {
            var root = new GameObject("Death / fading world");
            root.AddComponent<DeathSceneEffect>().Setup(player, killer, camera);
        }

        private void Setup(Transform player, Transform killer, Camera camera)
        {
            view = camera;
            oldBackground = view != null ? view.backgroundColor : Color.black;
            startedAt = Time.time;
            if (view != null) view.GetComponent<AimCamera>()?.FocusOnDeath(killer);

            Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            if (unlit != null)
            {
                characterMaterial = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave };
                MakeUnlit(player);
                if (killer != null && killer != player) MakeUnlit(killer);
            }

            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (renderer == null || IsCharacter(renderer.transform, player, killer)) continue;
                if (renderer is SpriteRenderer sprite)
                {
                    sprites.Add(sprite);
                    spriteColors.Add(sprite.color);
                }
                else if (renderer.TryGetComponent(out Tilemap tilemap))
                {
                    tilemaps.Add(tilemap);
                    tileColors.Add(tilemap.color);
                }
                else otherRenderers.Add(renderer);
            }
        }

        private static bool IsCharacter(Transform visual, Transform player, Transform killer) =>
            player != null && visual.IsChildOf(player) ||
            killer != null && visual.IsChildOf(killer);

        private void MakeUnlit(Transform character)
        {
            foreach (SpriteRenderer sprite in character.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.sharedMaterial = characterMaterial;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - startedAt) / FadeSeconds);
            float eased = progress * progress * (3f - 2f * progress);
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] == null) continue;
                Color color = spriteColors[i];
                color.a *= 1f - eased;
                sprites[i].color = color;
            }
            for (int i = 0; i < tilemaps.Count; i++)
            {
                if (tilemaps[i] == null) continue;
                Color color = tileColors[i];
                color.a *= 1f - eased;
                tilemaps[i].color = color;
            }
            if (view != null)
                view.backgroundColor = Color.Lerp(oldBackground, Color.white, eased);
            if (progress < 1f) return;
            foreach (Renderer renderer in otherRenderers)
                if (renderer != null) renderer.enabled = false;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (characterMaterial != null) Destroy(characterMaterial);
        }
    }
}
