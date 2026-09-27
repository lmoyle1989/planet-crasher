using UnityEngine;

namespace CosmicCrush
{
    /// <summary>
    /// Screen-filling parallax starfield. Stars wrap around the view so it never runs out, at any zoom level.
    /// </summary>
    public class StarField : MonoBehaviour
    {
        struct Star
        {
            public Transform T;
            public Vector2 Uv;
            public float Parallax, Size;
        }

        Star[] stars;
        Camera cam;

        public void Init(Camera camera, int count = 220)
        {
            cam = camera;
            stars = new Star[count];
            Color[] tints = { Color.white, new Color(0.75f, 0.85f, 1f), new Color(1f, 0.92f, 0.75f) };

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Star");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ProceduralArt.Dot;
                sr.sharedMaterial = ProceduralArt.SpriteMaterial;
                sr.sortingOrder = -1000;

                float layer = Random.value;
                var tint = tints[Random.Range(0, tints.Length)] * Mathf.Lerp(0.3f, 1f, layer);
                tint.a = 1f;
                sr.color = tint;

                stars[i] = new Star
                {
                    T = go.transform,
                    Uv = new Vector2(Random.value, Random.value),
                    Parallax = Mathf.Lerp(0.02f, 0.25f, layer * layer),
                    Size = Mathf.Lerp(0.7f, 2.2f, layer) * Random.Range(0.8f, 1.2f)
                };
            }
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        void LateUpdate()
        {
            if (cam == null || stars == null) return;
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            Vector2 c = cam.transform.position;

            foreach (var s in stars)
            {
                float fx = Frac(s.Uv.x - c.x * s.Parallax / w);
                float fy = Frac(s.Uv.y - c.y * s.Parallax / h);
                s.T.position = new Vector3(c.x + (fx - 0.5f) * w, c.y + (fy - 0.5f) * h, 0f);
                s.T.localScale = Vector3.one * (h * 0.0025f * s.Size);
            }
        }
    }
}
