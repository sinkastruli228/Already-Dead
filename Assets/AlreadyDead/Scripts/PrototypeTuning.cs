using UnityEngine;

namespace AlreadyDead
{
    [CreateAssetMenu(menuName = "Already Dead/Prototype tuning")]
    public sealed class PrototypeTuning : ScriptableObject
    {
        [Header("Movement and interaction")]
        [Min(0.1f)] public float moveSpeed = 6f;
        [Min(0.1f)] public float pickupDistance = 1.8f;
        [Min(0.01f)] public float cursorPickupRadius = 0.22f;

        [Header("Unarmed combat")]
        [Min(0.05f)] public float punchInterval = 0.32f;
        [Min(0.05f)] public float punchDuration = 0.24f;
        [Min(0.1f)] public float punchReach = 0.9f;
        [Min(0.01f)] public float punchRadius = 0.17f;
        [Min(0f)] public float punchForce = 6f;
        [Min(0f)] public float punchShakeStrength = 0.045f;
        [Min(0.01f)] public float punchShakeDuration = 0.08f;

        [Header("Camera")]
        [Min(1f)] public float cameraSize = 6.5f;
        [Range(0f, 0.8f)] public float lookAheadWeight = 0.28f;
        [Min(0f)] public float maxCameraOffset = 2.6f;
        [Min(0.01f)] public float cameraSmoothTime = 0.13f;
        [Min(0f)] public float shotShakeStrength = 0.12f;
        [Min(0.01f)] public float shotShakeDuration = 0.14f;

        [Header("Pistol")]
        [Min(0.01f)] public float shotInterval = 0.16f;
        [Range(0f, 20f)] public float spreadHalfAngle = 4f;
        [Min(1f)] public float bulletSpeed = 42f;
        [Min(0.01f)] public float bulletRadius = 0.035f;
        [Min(0.1f)] public float bulletLifetime = 1.3f;
        [Min(0f)] public float recoilDistance = 0.17f;
        [Min(0.01f)] public float recoilReturnSpeed = 1.5f;

        [Header("Musket")]
        [Min(1f)] public float musketCooldownMultiplier = 5f;
        [Min(1)] public int musketDamage = 99;
        [Min(0f)] public float musketRecoilDistance = 0.28f;
        [Min(0f)] public float musketShakeMultiplier = 1.75f;

        [Header("Club")]
        [Min(0.05f)] public float clubSwingInterval = 0.62f;
        [Min(0.05f)] public float clubSwingDuration = 0.42f;
        [Min(0.1f)] public float clubReach = 1.45f;
        [Min(0.01f)] public float clubRadius = 0.3f;
        [Min(1)] public int clubDamage = 2;

        [Header("Magic staffs")]
        [Min(0.05f)] public float fireCastInterval = 0.48f;
        [Min(0.05f)] public float frostCastInterval = 0.38f;
        [Min(0.05f)] public float lightningCastInterval = 0.65f;
        [Min(0.1f)] public float magicProjectileSpeed = 15f;
        [Min(0.01f)] public float magicProjectileRadius = 0.12f;
        [Min(0.1f)] public float magicProjectileLifetime = 1.2f;
        [Range(1, 12)] public int frostShardMin = 4;
        [Range(1, 12)] public int frostShardMax = 6;
        [Range(0f, 45f)] public float frostSpreadHalfAngle = 18f;
        [Range(0.05f, 1f)] public float frostSlowMultiplier = 0.45f;
        [Min(0.1f)] public float frostSlowDuration = 2.2f;
        [Min(0.1f)] public float lightningRange = 8f;
        [Min(0.1f)] public float lightningChainRange = 2.7f;
        [Range(1, 8)] public int lightningMaxTargets = 4;

        [Header("Throw (XY plane, no gravity)")]
        [Min(0f)] public float throwSpeed = 12f;
        [Min(0f)] public float throwSpin = 650f;
        [Min(0f)] public float throwLinearDamping = 1.9f;
        [Min(0f)] public float throwAngularDamping = 2.4f;
        [Range(0f, 1f)] public float throwBounce = 0.55f;

        [Header("Spear")]
        [Min(0.05f)] public float spearStabInterval = 0.42f;
        [Min(0.05f)] public float spearStabDuration = 0.24f;
        [Min(0.1f)] public float spearStabReach = 2.1f;
        [Min(0.01f)] public float spearStabRadius = 0.16f;
        [Min(0f)] public float spearStabForce = 9f;
        [Min(0.1f)] public float spearMaxChargeTime = 1.25f;
        [Min(0.1f)] public float spearMinThrowSpeed = 9f;
        [Min(0.1f)] public float spearMaxThrowSpeed = 27f;
        [Min(0.1f)] public float spearMinThrowRange = 4f;
        [Min(0.1f)] public float spearMaxThrowRange = 13f;
        [Min(0f)] public float spearThrowHeight = 0.7f;
        [Min(0.05f)] public float spearVibrationDuration = 0.42f;
        [Range(0f, 25f)] public float spearVibrationAngle = 9f;
        [Min(1f)] public float spearVibrationFrequency = 28f;

        [Header("Rock")]
        [Min(0.05f)] public float rockStrikeInterval = 0.38f;
        [Min(0.05f)] public float rockStrikeDuration = 0.22f;
        [Min(0.1f)] public float rockStrikeReach = 1.05f;
        [Min(0.01f)] public float rockStrikeRadius = 0.24f;
        [Min(0f)] public float rockStrikeForce = 7f;
        [Min(0.1f)] public float rockThrowSpeed = 15f;
        [Min(0.1f)] public float rockFlightDuration = 0.58f;
        [Min(0f)] public float rockThrowHeight = 0.9f;
        [Range(0.1f, 1f)] public float rockShadowApexScale = 0.52f;

        [Header("Character pixel animation")]
        [Min(0.01f)] public float legStepDistance = 0.16f;
        [Min(0.1f)] public float legStepRate = 7.5f;

        [Header("Player and patrol enemies")]
        [Min(1)] public int playerMaxHealth = 5;
        [Min(0f)] public float playerHitInvulnerability = 0.55f;
        [Min(1)] public int enemyMaxHealth = 3;
        [Min(0.1f)] public float enemyPatrolSpeed = 1.4f;
        [Min(0.1f)] public float enemyChaseSpeed = 2.5f;
        [Min(0.01f)] public float enemyWaypointTolerance = 0.18f;
        [Min(0.1f)] public float enemyVisionRange = 5f;
        [Range(1f, 180f)] public float enemyVisionHalfAngle = 48f;
        [Min(0.1f)] public float enemyAttackRange = 0.9f;
        [Min(0.1f)] public float enemyAttackInterval = 0.85f;

        [Header("Collision layers")]
        public LayerMask wallMask = 1 << 8;
        public LayerMask weaponMask = 1 << 9;
        public LayerMask punchMask = (1 << 8) | (1 << 11);
        public LayerMask enemyMask = 1 << 11;
    }
}
