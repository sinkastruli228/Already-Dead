using UnityEngine;

namespace AlreadyDead
{
    [CreateAssetMenu(fileName = "MainMenuAssets", menuName = "Already Dead/Main Menu Assets")]
    public sealed class MainMenuAssets : ScriptableObject
    {
        public Texture2D background;
        public Texture2D logo;
        public Texture2D continueButton;
        public Texture2D settingsButton;
        public Texture2D exitButton;
        public Texture2D backButton;
        public Texture2D soundOn;
        public Texture2D soundOff;

        public bool IsComplete => background != null && logo != null &&
            continueButton != null && settingsButton != null && exitButton != null &&
            backButton != null && soundOn != null && soundOff != null;
    }
}
