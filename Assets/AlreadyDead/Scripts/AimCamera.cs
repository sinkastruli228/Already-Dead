using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlreadyDead
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public sealed class AimCamera : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private TopDownPlayer player;
        private Camera view;
        private Vector2 smoothPosition;
        private Vector2 smoothVelocity;
        private float shakeRemaining;
        private float shakeStrength;
        private float shakeDuration;
        private VolumeProfile imageEffectsProfile;
        private LensDistortion lensDistortion;
        private Transform deathFocus;
        private float deathZoomVelocity;
        private const float BaseDistortion = -0.14f;
        private const float ShotDistortion = -0.12f;
        public float ShakeRemaining => shakeRemaining;
        public Camera View => view != null ? view : view = GetComponent<Camera>();

        public void Configure(PrototypeTuning settings, TopDownPlayer target)
        {
            tuning = settings;
            player = target;
        }

        private void Start()
        {
            View.orthographicSize = tuning.cameraSize;
            smoothPosition = player.transform.position;
            transform.position = new Vector3(smoothPosition.x, smoothPosition.y, -10f);
            ConfigureImageEffects();
        }

        private void ConfigureImageEffects()
        {
            UniversalAdditionalCameraData cameraData = View.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask |= 1 << gameObject.layer;
            Volume volume = GetComponent<Volume>();
            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
                imageEffectsProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile = imageEffectsProfile;
            }
            else imageEffectsProfile = volume.profile;
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.enabled = true;

            if (!imageEffectsProfile.TryGet(out FilmGrain grain))
                grain = imageEffectsProfile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.48f);
            grain.response.Override(1f);

            if (!imageEffectsProfile.TryGet(out lensDistortion))
                lensDistortion = imageEffectsProfile.Add<LensDistortion>(true);
            lensDistortion.intensity.Override(BaseDistortion);
            lensDistortion.scale.Override(1f);
        }

        private void OnDestroy()
        {
            if (imageEffectsProfile != null) Destroy(imageEffectsProfile);
        }

        public Vector2 ScreenToWorld(Vector2 pixel)
        {
            return View.ScreenToWorldPoint(new Vector3(pixel.x, pixel.y, -transform.position.z));
        }

        public Vector2 LookOffset(Vector2 playerPosition, Vector2 cursorWorld)
        {
            return Vector2.ClampMagnitude((cursorWorld - playerPosition) * tuning.lookAheadWeight,
                tuning.maxCameraOffset);
        }

        public void FocusOnDeath(Transform killer)
        {
            deathFocus = killer;
            smoothVelocity = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (player == null) return;
            Vector2 center = player.transform.position;
            if (!player.IsAlive)
            {
                Vector2 other = deathFocus != null ? (Vector2)deathFocus.position : center;
                Vector2 focus = (center + other) * 0.5f;
                smoothPosition = Vector2.SmoothDamp(smoothPosition, focus, ref smoothVelocity,
                    0.35f, Mathf.Infinity, Time.deltaTime);
                float halfWidth = Mathf.Abs(center.x - other.x) * 0.5f + 3.4f;
                float halfHeight = Mathf.Abs(center.y - other.y) * 0.5f + 3.4f;
                float targetSize = Mathf.Max(tuning.cameraSize, halfHeight,
                    halfWidth / Mathf.Max(0.5f, View.aspect));
                View.orthographicSize = Mathf.SmoothDamp(View.orthographicSize,
                    targetSize, ref deathZoomVelocity, 0.35f, Mathf.Infinity, Time.deltaTime);
                transform.position = new Vector3(smoothPosition.x, smoothPosition.y, -10f);
                return;
            }
            Vector2 look = player.InputActive && Mouse.current != null
                ? LookOffset(center, ScreenToWorld(Mouse.current.position.ReadValue())) : Vector2.zero;
            smoothPosition = Vector2.SmoothDamp(smoothPosition, center + look, ref smoothVelocity,
                tuning.cameraSmoothTime, Mathf.Infinity, Time.deltaTime);

            Vector2 shake = Vector2.zero;
            if (shakeRemaining > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
                float envelope = shakeRemaining / shakeDuration;
                shake = new Vector2(Mathf.PerlinNoise(Time.time * 55f, 0.1f) - 0.5f,
                    Mathf.PerlinNoise(0.7f, Time.time * 55f) - 0.5f) * (2f * shakeStrength * envelope);
            }
            if (lensDistortion != null)
                lensDistortion.intensity.value = BaseDistortion + ShotDistortion *
                    (shakeRemaining > 0f ? shakeRemaining / shakeDuration : 0f);

            // Clamp the final position, including follow lag and shot shake.
            Vector2 offset = Vector2.ClampMagnitude(smoothPosition + shake - center, tuning.maxCameraOffset);
            transform.position = new Vector3(center.x + offset.x, center.y + offset.y, -10f);
        }

        public void Kick()
        {
            Kick(tuning.shotShakeStrength, tuning.shotShakeDuration);
        }

        public void Kick(float strength, float duration)
        {
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeRemaining = shakeDuration;
            shakeStrength = strength;
        }
    }
}
