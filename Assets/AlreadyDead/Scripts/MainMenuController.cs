using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AlreadyDead
{
    public enum MainMenuPage
    {
        Main,
        Settings
    }

    public sealed class MainMenuController : MonoBehaviour
    {
        public const string MenuSceneName = "MainMenu";
        public const string GameplaySceneName = "SampleScene";

        private const string SoundPreferenceKey = "AlreadyDead.SoundEnabled";
        private const float PressedScale = 0.9f;
        private const float PressAnimationSpeed = 9f;

        private static MainMenuController instance;

        private MainMenuAssets assets;
        private GUIStyle invisibleButton;
        private bool soundEnabled;
        private float soundButtonScale = 1f;
        private Rect soundHitRect;

        public static MainMenuController Instance => instance;
        public MainMenuPage Page { get; private set; }
        public bool SoundEnabled => soundEnabled;
        public bool AssetsReady => assets != null && assets.IsComplete;
        public bool QuitRequested { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != MenuSceneName) return;
            if (FindAnyObjectByType<MainMenuController>() != null) return;

            var root = new GameObject("Main menu");
            root.AddComponent<MainMenuController>();
        }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != MenuSceneName)
            {
                Destroy(gameObject);
                return;
            }

            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Page = MainMenuPage.Main;
            assets = Resources.Load<MainMenuAssets>("MainMenuAssets");
            soundEnabled = PlayerPrefs.GetInt(SoundPreferenceKey, 1) != 0;

            Time.timeScale = 1f;
            AudioListener.pause = false;
            ApplySoundVolume();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PrepareTextures();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame &&
                Page == MainMenuPage.Settings)
            {
                ShowMain();
            }

            bool soundHeld = Page == MainMenuPage.Settings &&
                Mouse.current != null && Mouse.current.leftButton.isPressed &&
                soundHitRect.Contains(ScreenToGui(Mouse.current.position.ReadValue()));
            float targetScale = soundHeld ? PressedScale : 1f;
            soundButtonScale = Mathf.MoveTowards(soundButtonScale, targetScale,
                PressAnimationSpeed * Time.unscaledDeltaTime);
        }

        public void ContinueGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(GameplaySceneName);
        }

        public void ShowSettings()
        {
            Page = MainMenuPage.Settings;
            soundButtonScale = 1f;
        }

        public void ShowMain()
        {
            Page = MainMenuPage.Main;
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

        private void ApplySoundVolume() => AudioListener.volume = soundEnabled ? 1f : 0f;

        private void PrepareTextures()
        {
            if (assets == null) return;
            SetPixelPerfect(assets.background);
            SetPixelPerfect(assets.logo);
            SetPixelPerfect(assets.continueButton);
            SetPixelPerfect(assets.settingsButton);
            SetPixelPerfect(assets.exitButton);
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
            GUI.depth = -11000;
            EnsureStyles();

            if (!AssetsReady)
            {
                DrawMissingAssetsWarning();
                return;
            }

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.DrawTexture(screen, assets.background, ScaleMode.ScaleAndCrop, false);
            DrawLogo();

            if (Page == MainMenuPage.Main) DrawMainPage();
            else DrawSettingsPage();
        }

        private void DrawLogo()
        {
            float width = Screen.width * 0.56f;
            float height = width * assets.logo.height / assets.logo.width;
            Rect logoRect = new Rect(Screen.width * 0.045f, Screen.height * 0.075f, width, height);
            GUI.DrawTexture(logoRect, assets.logo, ScaleMode.ScaleToFit, true);
        }

        private void DrawMainPage()
        {
            float width = Screen.width * 0.28f;
            float height = width * assets.continueButton.height / assets.continueButton.width;
            float x = Screen.width * 0.052f;
            float firstY = Screen.height * 0.44f;
            float step = Screen.height * 0.18f;

            Rect continueRect = new Rect(x, firstY, width, height);
            Rect settingsRect = new Rect(x, firstY + step, width, height);
            Rect exitRect = new Rect(x, firstY + step * 2f, width, height);

            if (TextureButton(continueRect, assets.continueButton)) ContinueGame();
            if (TextureButton(settingsRect, assets.settingsButton)) ShowSettings();
            if (TextureButton(exitRect, assets.exitButton)) RequestQuit();
        }

        private void DrawSettingsPage()
        {
            float soundSize = Mathf.Min(Screen.width, Screen.height) * 0.22f;
            soundHitRect = new Rect(Screen.width * 0.095f, Screen.height * 0.47f,
                soundSize, soundSize);
            Rect animatedSound = ScaleAroundCenter(soundHitRect, soundButtonScale);
            GUI.DrawTexture(animatedSound, soundEnabled ? assets.soundOn : assets.soundOff,
                ScaleMode.ScaleToFit, true);
            if (GUI.Button(soundHitRect, GUIContent.none, invisibleButton)) ToggleSound();

            float backSize = Mathf.Min(Screen.width, Screen.height) * 0.105f;
            Rect backRect = new Rect(Screen.width * 0.055f, Screen.height * 0.80f,
                backSize, backSize);
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

        private static Rect ScaleAroundCenter(Rect rect, float scale)
        {
            Vector2 size = rect.size * scale;
            return new Rect(rect.center - size * 0.5f, size);
        }

        private static Vector2 ScreenToGui(Vector2 screenPoint) =>
            new Vector2(screenPoint.x, Screen.height - screenPoint.y);

        private static void DrawMissingAssetsWarning()
        {
            Color oldColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 28f, 18f, 40f)),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height),
                "MAIN MENU ASSETS NOT FOUND", style);
        }
    }
}
