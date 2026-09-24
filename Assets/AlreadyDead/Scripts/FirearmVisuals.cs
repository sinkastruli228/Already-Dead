using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AlreadyDead
{
    // References the original sprites in Assets/Waepon/Effect without copying the textures.
    public sealed class FirearmVisuals : ScriptableObject
    {
        [SerializeField] private Sprite bullet;
        [SerializeField] private Sprite muzzleFire;
        [SerializeField] private Material bulletMaterial;
        [SerializeField] private Material muzzleFireMaterial;

        private static FirearmVisuals cached;
        public static FirearmVisuals Current => cached != null
            ? cached : cached = Resources.Load<FirearmVisuals>("FirearmVisuals");
        public Sprite Bullet => bullet;
        public Sprite MuzzleFire => muzzleFire;
        public Material BulletMaterial => bulletMaterial;
        public Material MuzzleFireMaterial => muzzleFireMaterial;

        public void Configure(Sprite bulletSprite, Sprite fireSprite,
            Material projectileMaterial, Material fireMaterial)
        {
            bullet = bulletSprite;
            muzzleFire = fireSprite;
            bulletMaterial = projectileMaterial;
            muzzleFireMaterial = fireMaterial;
        }

        public static Light2D PrepareMuzzle(SpriteRenderer flash, Transform muzzle, float radius)
        {
            FirearmVisuals visuals = Current;
            if (flash != null && visuals != null && visuals.muzzleFire != null)
            {
                flash.sprite = visuals.muzzleFire;
                if (visuals.muzzleFireMaterial != null)
                    flash.sharedMaterial = visuals.muzzleFireMaterial;
                flash.color = Color.white;
                // The supplied fire begins at its left edge and shoots along local +X.
                const float scale = 0.2f;
                Vector2 pivot = visuals.muzzleFire.pivot;
                float pixelsPerUnit = visuals.muzzleFire.pixelsPerUnit;
                flash.transform.localRotation = Quaternion.identity;
                flash.transform.localScale = Vector3.one * scale;
                flash.transform.localPosition = new Vector3(
                    pivot.x / pixelsPerUnit * scale,
                    (pivot.y - visuals.muzzleFire.rect.height * 0.5f) /
                    pixelsPerUnit * scale, 0f);
            }
            Light2D light = muzzle.GetComponent<Light2D>();
            if (light == null) light = muzzle.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.72f, 0.35f);
            light.pointLightInnerRadius = 0.6f;
            light.pointLightOuterRadius = radius;
            light.falloffIntensity = 0.75f;
            light.intensity = 0f;
            return light;
        }

        public static void UpdateMuzzleLight(Light2D light, bool held, float flashUntil,
            float flashDuration, float peakIntensity)
        {
            if (light == null) return;
            light.intensity = held
                ? Mathf.Clamp01((flashUntil - Time.time) / flashDuration) * peakIntensity
                : 0f;
        }
    }
}
