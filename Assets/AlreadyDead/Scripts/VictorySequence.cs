using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead
{
    // Shared clear sequence for the cave, hospital, and saloon levels.
    public sealed class VictorySequence : MonoBehaviour
    {
        private const float ClearDelay = 2f;
        private const float ReturnDelay = 3f;

        private static VictorySequence instance;
        private TopDownPlayer player;
        private DeathSceneEffect fade;
        private GUIStyle returnStyle;
        private float clearStartedAt = float.NegativeInfinity;
        private float whiteStartedAt = float.NegativeInfinity;
        private bool hadEnemies;

        public static bool IsFinishing => instance != null && instance.fade != null;
        public bool ReturnVisible => !float.IsNegativeInfinity(whiteStartedAt);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

        private static void Install(Scene scene)
        {
            if (scene.name != "SampleScene" && scene.name != "BuildingParkingScene" &&
                scene.name != "SaloonScene") return;
            if (FindAnyObjectByType<VictorySequence>() != null) return;
            new GameObject("Victory / level clear").AddComponent<VictorySequence>();
        }

        private void Awake() => instance = this;

        private void Start()
        {
            player = FindAnyObjectByType<TopDownPlayer>();
            hadEnemies = LivingEnemyCount() > 0;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (player == null || !player.IsAlive || !hadEnemies) return;
            if (fade == null)
            {
                if (LivingEnemyCount() > 0)
                {
                    clearStartedAt = float.NegativeInfinity;
                    return;
                }
                if (float.IsNegativeInfinity(clearStartedAt)) clearStartedAt = Time.time;
                if (Time.time - clearStartedAt < ClearDelay) return;
                Camera view = player.View != null ? player.View.View : Camera.main;
                fade = DeathSceneEffect.BeginVictory(player.transform, view);
                return;
            }
            if (!fade.FadeComplete) return;
            if (float.IsNegativeInfinity(whiteStartedAt)) whiteStartedAt = Time.time;
            if (Time.time - whiteStartedAt < ReturnDelay) return;
            if (PauseMenuController.IsPaused) PauseMenuController.Instance.ClosePause();
            SceneManager.LoadScene(MainMenuController.MenuSceneName);
        }

        private static int LivingEnemyCount()
        {
            int count = 0;
            foreach (PatrolEnemy enemy in FindObjectsByType<PatrolEnemy>(FindObjectsSortMode.None))
                if (enemy.IsAlive) count++;
            foreach (FantasyEnemy enemy in FindObjectsByType<FantasyEnemy>(FindObjectsSortMode.None))
                if (enemy.IsAlive) count++;
            return count;
        }

        private void OnGUI()
        {
            if (!ReturnVisible || player == null || !player.IsAlive || PauseMenuController.IsPaused)
                return;
            Camera view = player.View != null ? player.View.View : Camera.main;
            if (view == null) return;
            Vector3 point = view.WorldToScreenPoint(player.transform.position + Vector3.up * 1.75f);
            if (point.z <= 0f) return;
            if (returnStyle == null)
            {
                returnStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.black }
                };
                Font cupe = Resources.Load<Font>("CUPE");
                if (cupe != null) returnStyle.font = cupe;
            }
            returnStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 17f, 28f, 64f));
            GUI.depth = -9001;
            GUI.Label(new Rect(point.x - 200f, Screen.height - point.y - 45f,
                400f, 90f), "RETURN", returnStyle);
        }
    }
}
