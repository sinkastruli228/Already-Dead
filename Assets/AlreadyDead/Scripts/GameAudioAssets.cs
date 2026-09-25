using UnityEngine;

namespace AlreadyDead
{
    [CreateAssetMenu(fileName = "GameAudioAssets", menuName = "Already Dead/Game Audio Assets")]
    public sealed class GameAudioAssets : ScriptableObject
    {
        public AudioClip door;
        public AudioClip shot;
        public AudioClip reload;
        public AudioClip[] ricochets;
    }
}
