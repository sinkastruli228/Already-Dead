using UnityEngine;

namespace AlreadyDead
{
    // Keeps palette enemy prefabs playable when dropped into a scene by hand.
    [RequireComponent(typeof(PatrolEnemy))]
    public sealed class SaloonEnemySpawn : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform facing;
        [SerializeField] private bool hasSceneRoute;

        public void Configure(PrototypeTuning settings, Transform visual)
        {
            tuning = settings;
            facing = visual;
        }

        public void SetSceneRoute(TopDownPlayer player, Vector2[] route)
        {
            hasSceneRoute = true;
            GetComponent<PatrolEnemy>().ConfigureRoute(tuning, player, facing, null, route);
        }

        private void Start()
        {
            PatrolEnemy enemy = GetComponent<PatrolEnemy>();
            if (!hasSceneRoute)
            {
                TopDownPlayer player = FindFirstObjectByType<TopDownPlayer>();
                Vector2 first = transform.position;
                enemy.ConfigureRoute(tuning, player, facing, null,
                    new[] { first, first + Vector2.right * 1.2f });
            }
            enemy.EnableWandering();
        }
    }
}
