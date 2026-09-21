using UnityEngine;
using UnityEngine.InputSystem;

namespace AlreadyDead
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private TopDownPlayer player;
        private GUIStyle title;
        private GUIStyle small;
        private GUIStyle text;
        private readonly Color accent = new Color(0.39f, 0.93f, 0.82f);
        private readonly Color amber = new Color(1f, 0.77f, 0.36f);

        public void Configure(TopDownPlayer target) => player = target;

        private void OnGUI()
        {
            if (player == null) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                text = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            }
            float scale = Mathf.Clamp(Screen.height / 800f, 0.7f, 1.6f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            Fill(new Rect(20, 20, 320, 77), new Color(0.035f, 0.052f, 0.065f, 0.92f));
            Fill(new Rect(20, 20, 3, 77), accent);
            Label(new Rect(37, 29, 290, 31), "ALREADY DEAD", title, Color.white);
            Label(new Rect(38, 63, 290, 20), "01 / WEAPON HANDLING   •   PROTOTYPE", small, accent);

            Fill(new Rect(width - 220, 20, 200, 77), new Color(0.035f, 0.052f, 0.065f, 0.92f));
            Label(new Rect(width - 204, 29, 180, 20), "СНАРЯЖЕНИЕ", small, accent);
            Label(new Rect(width - 204, 52, 180, 27), player.HeldWeapon != null ? "ПИСТОЛЕТ / ∞" : "КУЛАКИ", text, Color.white);

            Fill(new Rect(20, height - 57, width - 40, 37), new Color(0.035f, 0.052f, 0.065f, 0.92f));
            Label(new Rect(34, height - 49, width - 65, 26),
                "WASD  движение     МЫШЬ  прицел     ЛКМ  удар / выстрел     ПКМ  взять / бросить     R  сброс     ESC  курсор", small, Color.white);

            string hint = !player.InputActive ? "Щёлкни по Game; ESC возвращает управление"
                : player.HoveredWeapon != null ? "ПКМ — ПОДНЯТЬ ПИСТОЛЕТ"
                : player.HeldWeapon == null ? "Подойди к пистолету и наведи на него курсор" : "";
            if (hint.Length > 0)
            {
                var centered = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
                Label(new Rect(0, height - 96, width, 30), hint, centered,
                    player.HoveredWeapon != null ? amber : Color.white);
            }

            GUI.matrix = oldMatrix;
            if (!player.InputActive || Mouse.current == null) return;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 point = new Vector2(mouse.x, Screen.height - mouse.y);
            Color crossColor = player.HoveredWeapon != null ? amber : Color.white;
            Cross(point, 3f, 13f, 4f, new Color(0.02f, 0.03f, 0.04f, 0.9f));
            Cross(point, 4f, 12f, 2f, crossColor);
            Fill(new Rect(point.x - 1, point.y - 1, 2, 2), crossColor);
        }

        private static void Cross(Vector2 p, float gap, float length, float thickness, Color color)
        {
            Fill(new Rect(p.x - length, p.y - thickness / 2, length - gap, thickness), color);
            Fill(new Rect(p.x + gap, p.y - thickness / 2, length - gap, thickness), color);
            Fill(new Rect(p.x - thickness / 2, p.y - length, thickness, length - gap), color);
            Fill(new Rect(p.x - thickness / 2, p.y + gap, thickness, length - gap), color);
        }

        private static void Label(Rect rect, string value, GUIStyle style, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.Label(rect, value, style);
            GUI.color = old;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
