using UnityEngine;

namespace AlreadyDead
{
    public sealed class PlayerVitality : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private AimCamera view;
        private float nextDamageTime;

        public int Health { get; private set; }
        public bool IsAlive => Health > 0;
        public float LastHitTime { get; private set; } = float.NegativeInfinity;

        public void Configure(PrototypeTuning settings, AimCamera camera)
        {
            tuning = settings;
            view = camera;
            Health = 1;
        }

        private void Awake()
        {
            if (tuning != null) Health = 1;
        }

        public bool TakeHit(int damage)
        {
            if (!IsAlive || Time.time < nextDamageTime) return false;
            Health = 0;
            LastHitTime = Time.time;
            nextDamageTime = Time.time + tuning.playerHitInvulnerability;
            if (view != null) view.Kick(tuning.shotShakeStrength, tuning.shotShakeDuration);
            return true;
        }
    }
}
