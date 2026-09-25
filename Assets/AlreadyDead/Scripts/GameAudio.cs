using UnityEngine;

namespace AlreadyDead
{
    public static class GameAudio
    {
        private static GameAudioAssets assets;
        private static AudioSource source;
        private static int nextRicochet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            assets = null;
            source = null;
            nextRicochet = 0;
        }

        public static void PlayDoor() => Play(Assets != null ? Assets.door : null);
        public static void PlayShot() => Play(Assets != null ? Assets.shot : null);
        public static void PlayReload() => Play(Assets != null ? Assets.reload : null);

        public static void PlayRicochet(Vector2 impact)
        {
            AudioClip[] clips = Assets != null ? Assets.ricochets : null;
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[nextRicochet++ % clips.Length];
            TopDownPlayer player = Object.FindAnyObjectByType<TopDownPlayer>();
            if (player == null) return;
            const float audibleRadius = 14f;
            float distance = Vector2.Distance(impact, player.transform.position);
            float volume = Mathf.Clamp01(1f - distance / audibleRadius);
            if (volume > 0f) Play(clip, volume);
        }

        private static GameAudioAssets Assets => assets != null
            ? assets : assets = Resources.Load<GameAudioAssets>("GameAudioAssets");

        private static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (source == null)
            {
                var root = new GameObject("Game audio / 2D effects");
                Object.DontDestroyOnLoad(root);
                source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.PlayOneShot(clip, volume);
        }
    }
}
