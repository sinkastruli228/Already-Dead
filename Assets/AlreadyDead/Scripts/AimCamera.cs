using UnityEngine;
using UnityEngine.InputSystem;

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

        private void LateUpdate()
        {
            if (player == null) return;
            Vector2 center = player.transform.position;
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
