using UnityEngine;

namespace AlreadyDead
{
    [CreateAssetMenu(fileName = "PauseMenuAssets", menuName = "Already Dead/Pause Menu Assets")]
    public sealed class PauseMenuAssets : ScriptableObject
    {
        public Texture2D background;
        public Texture2D continueButton;
        public Texture2D settingsButton;
        public Texture2D exitButton;
        public Texture2D plainButton;
        public Texture2D backButton;
        public Texture2D soundOn;
        public Texture2D soundOff;

        public bool IsComplete => background != null && continueButton != null &&
            settingsButton != null && exitButton != null && plainButton != null &&
            backButton != null && soundOn != null && soundOff != null;
    }
}
