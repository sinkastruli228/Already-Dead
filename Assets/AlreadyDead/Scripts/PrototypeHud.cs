using UnityEngine;

namespace AlreadyDead
{
    // Gameplay shows only ammunition and the current throw charge.
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private TopDownPlayer player;
        private GUIStyle ammoStyle;

        public void Configure(TopDownPlayer target) => player = target;

        private void OnGUI()
        {
            if (player == null || !player.IsAlive || PauseMenuController.IsPaused) return;
            GUI.depth = -9000;
            DrawAmmo();
            DrawThrowCharge();
        }

        private void DrawAmmo()
        {
            int remaining;
            int capacity;
            if (player.HeldWeapon != null)
            {
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
                remaining + "/" + capacity, ammoStyle);
        }

        private void DrawThrowCharge()
        {
            float charge = player.HeldSpear != null && player.HeldSpear.IsCharging
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
