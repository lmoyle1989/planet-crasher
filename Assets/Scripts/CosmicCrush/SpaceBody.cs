using UnityEngine;

namespace CosmicCrush
{
    /// <summary>
    /// A round celestial body (the player or anything floating around). Movement is driven by GameManager.
    /// </summary>
    public class SpaceBody : MonoBehaviour
    {
        public Vector2 Velocity;
        public bool Dying;
        public float DyingTime;

        public float Radius { get; private set; }
        public BodyKind Kind { get; private set; }
        public Color MainColor { get; private set; }

        SpriteRenderer surface, glow;
        bool customGlow;
        float spin;

        public Vector2 Position
        {
            get => transform.position;
            set => transform.position = new Vector3(value.x, value.y, 0f);
        }

        public static SpaceBody Create(string name, BodyKind kind, int variant, float radius, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<SpaceBody>();
            body.glow = AddLayer(go.transform, "Glow", ProceduralArt.Glow);
            body.surface = AddLayer(go.transform, "Surface", null);
            body.SetKind(kind, variant);
            body.SetRadius(radius);
            return body;
        }

        static SpriteRenderer AddLayer(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = ProceduralArt.SpriteMaterial;
            sr.sprite = sprite;
            return sr;
        }

        public void SetKind(BodyKind kind, int variant)
        {
            Kind = kind;
            surface.sprite = ProceduralArt.Body(kind, variant);
            MainColor = ProceduralArt.BodyColor(kind, variant);

            bool tumbles = kind == BodyKind.Rock || kind == BodyKind.Moon;
            spin = tumbles ? Random.Range(-45f, 45f) : 0f;
            surface.transform.localRotation = tumbles ? Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) : Quaternion.identity;

            if (!customGlow)
            {
                glow.enabled = kind == BodyKind.Star;
                glow.color = new Color(MainColor.r, MainColor.g, MainColor.b, 0.45f);
                glow.transform.localScale = Vector3.one * 2.3f;
            }
        }

        public void SetGlow(Color color, float scale)
        {
            customGlow = true;
            glow.enabled = true;
            glow.color = color;
            glow.transform.localScale = Vector3.one * scale;
        }

        public void SetRadius(float radius)
        {
            Radius = radius;
            transform.localScale = Vector3.one * radius;
        }

        public void SetVisualScale(float scale) => transform.localScale = Vector3.one * scale;

        public void SetSorting(int order)
        {
            glow.sortingOrder = order - 1;
            surface.sortingOrder = order;
        }

        public void Tick(float dt)
        {
            if (spin != 0f) surface.transform.Rotate(0f, 0f, spin * dt);
        }
    }
}
