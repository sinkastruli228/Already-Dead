using UnityEngine;
using UnityEngine.InputSystem;

namespace AlreadyDead
{
    public enum PauseMenuPage
    {
        Main,
        Settings
    }

    public sealed class PauseMenuController : MonoBehaviour
    {
        private const string SoundPreferenceKey = "AlreadyDead.SoundEnabled";
        private const float SoundPressedScale = 0.9f;
        private const float SoundAnimationSpeed = 8f;

        private static PauseMenuController instance;
        private PauseMenuAssets assets;
        private GUIStyle invisibleButton;
        private bool paused;
        private bool soundEnabled;
        private bool previousAudioPause;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private float previousTimeScale = 1f;
        private float soundButtonScale = 1f;
        private Rect soundHitRect;

        public static PauseMenuController Instance => instance;
        public static bool IsPaused => instance != null && instance.paused;
        public bool Paused => paused;
        public bool SoundEnabled => soundEnabled;
        public bool BackButtonVisible => paused && Page == PauseMenuPage.Settings;
        public bool AssetsReady => assets != null && assets.IsComplete;
        public bool QuitRequested { get; private set; }
        public PauseMenuPage Page { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureExists();

        public static PauseMenuController EnsureExists()
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<PauseMenuController>();
            if (instance != null) return instance;
            var root = new GameObject("Pause menu / persistent");
            DontDestroyOnLoad(root);
            return root.AddComponent<PauseMenuController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            assets = Resources.Load<PauseMenuAssets>("PauseMenuAssets");
            soundEnabled = PlayerPrefs.GetInt(SoundPreferenceKey, 1) != 0;
            ApplySoundVolume();
            PrepareTextures();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (paused) RestoreGameplayState();
            instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (paused && Page == PauseMenuPage.Settings) ShowMain();
                else if (paused) ClosePause();
                else OpenPause();
            }

            bool soundHeld = paused && Page == PauseMenuPage.Settings &&
                Mouse.current != null && Mouse.current.leftButton.isPressed &&
                soundHitRect.Contains(ScreenToGui(Mouse.current.position.ReadValue()));
            float target = soundHeld ? SoundPressedScale : 1f;
            soundButtonScale = Mathf.MoveTowards(soundButtonScale, target,
                SoundAnimationSpeed * Time.unscaledDeltaTime);
        }

        public void OpenPause()
        {
            if (paused) return;
            paused = true;
            Page = PauseMenuPage.Main;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ClosePause()
        {
            if (!paused) return;
            paused = false;
            Page = PauseMenuPage.Main;
            soundButtonScale = 1f;
            RestoreGameplayState();
        }

        public void ShowSettings()
        {
            if (!paused) return;
            Page = PauseMenuPage.Settings;
        }

        public void ShowMain()
        {
            if (!paused) return;
            Page = PauseMenuPage.Main;
            soundButtonScale = 1f;
        }

        public void ToggleSound()
        {
            soundEnabled = !soundEnabled;
            ApplySoundVolume();
            PlayerPrefs.SetInt(SoundPreferenceKey, soundEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void RequestQuit()
        {
            QuitRequested = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RestoreGameplayState()
        {
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void ApplySoundVolume() => AudioListener.volume = soundEnabled ? 1f : 0f;

        private void PrepareTextures()
        {
            if (assets == null) return;
            SetPixelPerfect(assets.background);
            SetPixelPerfect(assets.continueButton);
            SetPixelPerfect(assets.settingsButton);
            SetPixelPerfect(assets.exitButton);
            SetPixelPerfect(assets.plainButton);
            SetPixelPerfect(assets.backButton);
            SetPixelPerfect(assets.soundOn);
            SetPixelPerfect(assets.soundOff);
        }

        private static void SetPixelPerfect(Texture2D texture)
        {
            if (texture == null) return;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
        }

        private void OnGUI()
        {
            if (!paused) return;
            GUI.depth = -10000;
            EnsureStyles();
            DrawDimmer();
            if (!AssetsReady)
            {
                DrawMissingAssetsWarning();
                return;
            }

            Rect panel = CalculatePanelRect();
            GUI.DrawTexture(panel, assets.background, ScaleMode.StretchToFill, true);
            if (Page == PauseMenuPage.Main) DrawMainPage(panel);
            else DrawSettingsPage(panel);
        }

        private void DrawMainPage(Rect panel)
        {
            float buttonWidth = panel.width * 0.69f;
            float buttonHeight = buttonWidth * 0.313f;
            float x = panel.center.x - buttonWidth * 0.5f;
            Rect continueRect = new Rect(x, panel.y + panel.height * 0.10f, buttonWidth, buttonHeight);
            Rect settingsRect = new Rect(x, panel.y + panel.height * 0.40f, buttonWidth, buttonHeight);
            Rect exitRect = new Rect(x, panel.y + panel.height * 0.70f, buttonWidth, buttonHeight);

            if (TextureButton(continueRect, assets.continueButton)) ClosePause();
            if (TextureButton(settingsRect, assets.settingsButton)) ShowSettings();
            if (TextureButton(exitRect, assets.exitButton)) RequestQuit();
        }

        private void DrawSettingsPage(Rect panel)
        {
            float buttonWidth = panel.width * 0.69f;
            float buttonHeight = buttonWidth * 0.313f;
            soundHitRect = new Rect(panel.center.x - buttonWidth * 0.5f,
                panel.center.y - buttonHeight * 0.5f, buttonWidth, buttonHeight);
            Rect animatedButton = ScaleAroundCenter(soundHitRect, soundButtonScale);
            GUI.DrawTexture(animatedButton, assets.plainButton, ScaleMode.StretchToFill, true);

            float iconSize = animatedButton.height * 0.88f;
            Rect icon = new Rect(animatedButton.center.x - iconSize * 0.5f,
                animatedButton.center.y - iconSize * 0.5f, iconSize, iconSize);
            GUI.DrawTexture(icon, soundEnabled ? assets.soundOn : assets.soundOff,
                ScaleMode.ScaleToFit, true);
            if (GUI.Button(soundHitRect, GUIContent.none, invisibleButton)) ToggleSound();

            float backSize = panel.width * 0.15f;
            float backX = Mathf.Max(18f, panel.x - backSize - 18f);
            Rect backRect = new Rect(backX, panel.y + panel.height * 0.025f, backSize, backSize);
            if (TextureButton(backRect, assets.backButton)) ShowMain();
        }

        private bool TextureButton(Rect rect, Texture2D texture)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            return GUI.Button(rect, GUIContent.none, invisibleButton);
        }

        private void EnsureStyles()
        {
            if (invisibleButton != null) return;
            invisibleButton = new GUIStyle(GUIStyle.none)
            {
                margin = new RectOffset(),
                padding = new RectOffset(),
                border = new RectOffset()
            };
        }

        private static Rect CalculatePanelRect()
        {
            float size = Mathf.Min(Screen.height * 0.92f, Screen.width * 0.82f);
            return new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);
        }

        private static Rect ScaleAroundCenter(Rect rect, float scale)
        {
            Vector2 size = rect.size * scale;
            return new Rect(rect.center - size * 0.5f, size);
        }

        private static Vector2 ScreenToGui(Vector2 screenPoint) =>
            new Vector2(screenPoint.x, Screen.height - screenPoint.y);

        private static void DrawDimmer()
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawMissingAssetsWarning()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 28f, 18f, 40f)),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height),
                "PAUSE MENU ASSETS NOT FOUND", style);
        }
    }
}
