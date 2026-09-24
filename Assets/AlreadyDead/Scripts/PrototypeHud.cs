using UnityEngine;

namespace AlreadyDead
{
    // Gameplay shows only ammunition and the current throw charge.
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private TopDownPlayer player;
        private GUIStyle ammoStyle;
        private GUIStyle deathStyle;
        private GUIStyle deadStyle;
        private GUIStyle restartStyle;

        public void Configure(TopDownPlayer target) => player = target;

        private void OnGUI()
        {
            if (player == null || PauseMenuController.IsPaused) return;
            GUI.depth = -9000;
            if (!player.IsAlive)
            {
                DrawDeathPrompt();
                return;
            }
            DrawAmmo();
            DrawThrowCharge();
        }

        private void DrawDeathPrompt()
        {
            if (deathStyle == null)
            {
                deathStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.black }
                };
                Font cupe = Resources.Load<Font>("CUPE");
                if (cupe != null) deathStyle.font = cupe;
                deadStyle = new GUIStyle(deathStyle);
                deadStyle.normal.textColor = new Color(0.82f, 0.08f, 0.09f);
                restartStyle = new GUIStyle(deathStyle);
            }
            float appear = Mathf.Clamp01((Time.time - player.Vitality.LastHitTime - 0.45f) / 0.55f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, appear);
            int size = Mathf.RoundToInt(Mathf.Clamp(
                Mathf.Min(Screen.height / 16f, Screen.width / 20f), 28f, 64f));
            deathStyle.fontSize = size;
            deadStyle.fontSize = size;
            restartStyle.fontSize = Mathf.RoundToInt(size * 0.32f);
            Camera camera = player.View != null ? player.View.View : Camera.main;
            Vector3 point = camera != null
                ? camera.WorldToScreenPoint(player.transform.position)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);
            float x = point.x;
            float y = Screen.height - point.y;
            float gap = size * 0.9f;
            Vector2 youSize = deathStyle.CalcSize(new GUIContent("YOU ARE"));
            Vector2 deadSize = deadStyle.CalcSize(new GUIContent("DEAD"));
            GUI.Label(new Rect(x - gap - youSize.x, y - youSize.y * 0.5f,
                youSize.x, youSize.y), "YOU ARE", deathStyle);
            GUI.Label(new Rect(x + gap, y - deadSize.y * 0.5f,
                deadSize.x, deadSize.y), "DEAD", deadStyle);
            GUI.Label(new Rect(x - size * 2.2f, y + size * 1.45f,
                size * 4.4f, size * 0.5f), "R - RESTART", restartStyle);
            GUI.color = previous;
        }

        private void DrawAmmo()
        {
            bool unlimited = false;
            int remaining;
            int capacity;
            if (player.HeldWeapon != null)
            {
                unlimited = player.HeldWeapon.HasInfiniteAmmo;
                remaining = player.HeldWeapon.RemainingAmmo;
                capacity = player.HeldWeapon.Capacity;
            }
            else if (player.HeldMusket != null)
            {
                remaining = player.HeldMusket.RemainingAmmo;
                capacity = player.HeldMusket.Capacity;
            }
            else return;

            if (ammoStyle == null)
            {
                ammoStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperRight,
                    fontSize = 32,
                    normal = { textColor = Color.white }
                };
                Font cupe = Resources.Load<Font>("CUPE");
                if (cupe != null) ammoStyle.font = cupe;
            }
            GUI.Label(new Rect(Screen.width - 190f, 20f, 160f, 48f),
                unlimited ? "INF/INF" : remaining + "/" + capacity, ammoStyle);
        }

        private void DrawThrowCharge()
        {
            float charge = player.HeldWeapon != null && player.HeldWeapon.IsReloading
                ? player.HeldWeapon.ReloadProgress01
                : player.HeldSpear != null && player.HeldSpear.IsCharging
                ? player.HeldSpear.Charge01
                : player.HeldRock != null && player.HeldRock.IsCharging
                    ? player.HeldRock.Charge01 : -1f;
            if (charge < 0f || Camera.main == null) return;

            Vector3 screen = Camera.main.WorldToScreenPoint(player.transform.position + Vector3.up * 1.1f);
            if (screen.z <= 0f) return;
            const float width = 120f;
            const float height = 24f;
            const float stroke = 4f;
            Rect outer = new Rect(screen.x - width * 0.5f,
                Screen.height - screen.y - height * 0.5f, width, height);
            Fill(outer, new Color(1f, 1f, 1f, 0.25f));
            Fill(new Rect(outer.x + stroke, outer.y + stroke,
                (width - stroke * 2f) * charge, height - stroke * 2f), Color.white);
            Fill(new Rect(outer.x, outer.y, width, stroke), Color.white);
            Fill(new Rect(outer.x, outer.yMax - stroke, width, stroke), Color.white);
            Fill(new Rect(outer.x, outer.y, stroke, height), Color.white);
            Fill(new Rect(outer.xMax - stroke, outer.y, stroke, height), Color.white);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
