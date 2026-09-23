using UnityEngine;

namespace AlreadyDead
{
    // Positions silhouette copies at a constant four screen pixels regardless of zoom.
    public sealed class ScreenPixelOutline : MonoBehaviour
    {
        private Transform[] copies;

        private void Awake()
        {
            copies = new Transform[transform.childCount];
            for (int i = 0; i < copies.Length; i++) copies[i] = transform.GetChild(i);
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null || copies == null || Screen.height <= 0) return;
            float scale = Mathf.Abs(transform.lossyScale.x);
            if (scale < 0.0001f) return;
            float radius = 4f * camera.orthographicSize * 2f / (Screen.height * scale);
            for (int i = 0; i < copies.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / copies.Length;
                copies[i].localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
        }
    }
}
