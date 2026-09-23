using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
            bool fantasy = SceneManager.GetActiveScene().name == "FantasyScene";
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
            Label(new Rect(38, 63, 290, 20), fantasy ? "02 / ЗАЧАРОВАННЫЙ ЛЕС" :
                "01 / WEAPON HANDLING   •   PROTOTYPE", small, accent);

            Fill(new Rect(20, 107, 210, 38), new Color(0.035f, 0.052f, 0.065f, 0.92f));
            Label(new Rect(32, 114, 66, 20), "ЖИЗНЬ", small, Color.white);
            for (int i = 0; i < player.Tuning.playerMaxHealth; i++)
                Fill(new Rect(93 + i * 24, 117, 17, 12),
                    i < player.Vitality.Health ? new Color(0.88f, 0.32f, 0.26f) : new Color(0.23f, 0.17f, 0.16f));

            Fill(new Rect(width - 220, 20, 200, 77), new Color(0.035f, 0.052f, 0.065f, 0.92f));
            Label(new Rect(width - 204, 29, 180, 20), "СНАРЯЖЕНИЕ", small, accent);
            string equipment = player.HeldStaff != null ? player.HeldStaff.DisplayName :
                player.HeldSpear != null ? "КОПЬЁ" :
                player.HeldMusket != null ? "МУШКЕТ / ∞" :
                player.HeldClub != null ? "ДУБИНКА" :
                player.HeldWeapon != null ? "ПИСТОЛЕТ / ∞" :
                player.HeldRock != null ? "КАМЕНЬ" : "КУЛАКИ";
            Label(new Rect(width - 204, 52, 180, 27), equipment, text, Color.white);
            if (player.HeldSpear != null && player.HeldSpear.IsCharging)
            {
                Fill(new Rect(width - 204, 83, 176, 5), new Color(0.12f, 0.16f, 0.19f));
                Fill(new Rect(width - 204, 83, 176 * player.HeldSpear.Charge01, 5), amber);
            }

            Fill(new Rect(20, height - 57, width - 40, 37), new Color(0.035f, 0.052f, 0.065f, 0.92f));
            Label(new Rect(34, height - 49, width - 65, 26),
                fantasy ? "WASD  движение   МЫШЬ  прицел   ЛКМ  заклинание / удар   1/2/3  стихия посоха   ПКМ  взять / положить   R  сброс   ESC  меню" :
                "WASD  движение   МЫШЬ  прицел   ЛКМ  удар / выстрел   ПКМ  взять / заменить / бросить   КОПЬЁ: держать ПКМ   R  сброс   ESC  меню", small, Color.white);

            string hint = !player.IsAlive ? "ТЫ ПОГИБ   •   R — НАЧАТЬ ЗАНОВО"
                : !player.MovementActive ? "Щёлкни по Game; ESC возвращает управление"
                : !player.InputActive ? "Верни курсор в окно Game для прицеливания"
                : player.HoveredWeapon != null ? (player.HasWeapon ? "ПКМ — ЗАМЕНИТЬ НА ПИСТОЛЕТ" : "ПКМ — ПОДНЯТЬ ПИСТОЛЕТ")
                : player.HoveredSpear != null ? (player.HasWeapon ? "ПКМ — ЗАМЕНИТЬ НА КОПЬЁ" : "ПКМ — ПОДНЯТЬ КОПЬЁ")
                : player.HoveredRock != null ? (player.HasWeapon ? "ПКМ — ЗАМЕНИТЬ НА КАМЕНЬ" : "ПКМ — ПОДНЯТЬ КАМЕНЬ")
                : player.HoveredStaff != null ? (player.HasWeapon ? "ПКМ — ЗАМЕНИТЬ НА " : "ПКМ — ПОДНЯТЬ ") + player.HoveredStaff.DisplayName
                : player.HoveredMusket != null ? (player.HasWeapon ? "ПКМ — ЗАМЕНИТЬ НА МУШКЕТ" : "ПКМ — ПОДНЯТЬ МУШКЕТ")
                : player.HoveredClub != null ? (player.HasWeapon ? "ПКМ — ЗАМЕНИТЬ НА ДУБИНКУ" : "ПКМ — ПОДНЯТЬ ДУБИНКУ")
                : player.HeldStaff != null ? "ЛКМ — ЗАКЛИНАНИЕ   •   1 ОГОНЬ   2 ЛЁД   3 МОЛНИЯ   •   ПКМ — ПОЛОЖИТЬ"
                : player.HeldMusket != null ? "ЛКМ — ВЫСТРЕЛ   •   ДОЛГАЯ ПЕРЕЗАРЯДКА   •   ПКМ — БРОСИТЬ"
                : player.HeldClub != null ? "ЛКМ — ЗАМАХ ДУБИНКОЙ   •   ПКМ — БРОСИТЬ"
                : player.HeldSpear != null ? "ЛКМ — УКОЛ   •   УДЕРЖИВАЙ ПКМ И ОТПУСТИ ДЛЯ БРОСКА"
                : player.HeldRock != null ? "ЛКМ — УДАР КАМНЕМ   •   ПКМ — БРОСИТЬ СРАЗУ"
                : !player.HasWeapon ? "Подойди к оружию и наведи на него курсор" : "";
            if (hint.Length > 0)
            {
                var centered = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
                Label(new Rect(0, height - 96, width, 30), hint, centered,
                    HasHoveredWeapon() ? amber : Color.white);
            }

            if (Time.time - player.Vitality.LastHitTime < 0.16f)
                Fill(new Rect(0, 0, width, height), new Color(0.8f, 0.06f, 0.04f, 0.18f));

            GUI.matrix = oldMatrix;
            if (!player.InputActive || Mouse.current == null) return;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 point = new Vector2(mouse.x, Screen.height - mouse.y);
            Color crossColor = HasHoveredWeapon() ? amber : Color.white;
            Cross(point, 3f, 13f, 4f, new Color(0.02f, 0.03f, 0.04f, 0.9f));
            Cross(point, 4f, 12f, 2f, crossColor);
            Fill(new Rect(point.x - 1, point.y - 1, 2, 2), crossColor);
        }

        private bool HasHoveredWeapon() => player.HoveredWeapon != null || player.HoveredSpear != null ||
            player.HoveredRock != null || player.HoveredStaff != null || player.HoveredMusket != null ||
            player.HoveredClub != null;

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
