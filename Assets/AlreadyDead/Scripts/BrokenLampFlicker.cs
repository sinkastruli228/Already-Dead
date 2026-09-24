using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AlreadyDead
{
    [RequireComponent(typeof(Light2D))]
    public sealed class BrokenLampFlicker : MonoBehaviour
    {
        [SerializeField] private Light2D[] linkedLights;
        [SerializeField] private float steadyOnMin = 2.5f;
        [SerializeField] private float steadyOnMax = 5.5f;
        [SerializeField] private float darkMin = 0.4f;
        [SerializeField] private float darkMax = 1.5f;
        [SerializeField] private float rapidBlinkMin = 0.045f;
        [SerializeField] private float rapidBlinkMax = 0.13f;

        private float nextChange;
        private int rapidChangesRemaining;
        private bool lit;

        public void SetLinkedLights(Light2D[] lights) => linkedLights = lights;

        private void Awake()
        {
            lit = true;
            SetLight(true);
            nextChange = Time.time + Random.Range(steadyOnMin, steadyOnMax);
        }

        private void OnEnable()
        {
            lit = true;
            SetLight(true);
            nextChange = Time.time + Random.Range(steadyOnMin, steadyOnMax);
        }

        private void Update()
        {
            if (Time.time < nextChange) return;

            lit = !lit;
            SetLight(lit); // Broken switch: full light or complete darkness, no fade.

            if (rapidChangesRemaining > 0)
            {
                rapidChangesRemaining--;
                nextChange = Time.time + Random.Range(rapidBlinkMin, rapidBlinkMax);
                return;
            }

            if (!lit)
            {
                // Most failures are short blackouts; occasional bursts stutter rapidly.
                if (Random.value < 0.45f)
                {
                    rapidChangesRemaining = Random.Range(2, 7);
                    nextChange = Time.time + Random.Range(rapidBlinkMin, rapidBlinkMax);
                }
                else nextChange = Time.time + Random.Range(darkMin, darkMax);
            }
            else nextChange = Time.time + Random.Range(steadyOnMin, steadyOnMax);
        }

        private void OnDisable()
        {
            SetLight(true);
        }

        private void SetLight(bool enabled)
        {
            if (linkedLights == null || linkedLights.Length == 0)
            {
                GetComponent<Light2D>().enabled = enabled;
                return;
            }
            foreach (Light2D light in linkedLights)
                if (light != null) light.enabled = enabled;
        }
    }
}
