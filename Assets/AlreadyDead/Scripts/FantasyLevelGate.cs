using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FantasyLevelGate : MonoBehaviour
    {
        [SerializeField] private string destinationScene;

        public string DestinationScene => destinationScene;

        public void Configure(string sceneName) => destinationScene = sceneName;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<TopDownPlayer>() == null || string.IsNullOrEmpty(destinationScene)) return;
            SceneManager.LoadScene(destinationScene);
        }
    }
}
