using System.Collections.Generic;
using UnityEngine;

namespace CosmicCrush
{
    public enum BodyKind { Rock, Moon, Planet, GasGiant, Star }

    /// <summary>
    /// Generates every sprite in the game at runtime so the project needs no art assets.
    /// Body sprites are 2 world units wide at scale 1, so a transform scale equals the body's radius.
    /// </summary>
    public static class ProceduralArt
    {
        public const int VariantsPerKind = 6;
        const int BodySize = 128;

        struct BodyArt { public Sprite Sprite; public Color Color; }

        static readonly Dictionary<int, BodyArt> bodyCache = new Dictionary<int, BodyArt>();
        static Sprite glow, dot;
        static Material spriteMaterial;

        // The project enters Play mode without a domain reload, so statics survive between sessions
        // while the textures/sprites they point to are destroyed. Start every session with a clean cache.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            bodyCache.Clear();
            glow = dot = null;
            spriteMaterial = null;
        }

        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    spriteMaterial = new Material(shader);
                }
                return spriteMaterial;
            }
        }

        public static Sprite Glow => glow != null ? glow : glow = MakeRadial("Glow", 128, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        public static Sprite Dot => dot != null ? dot : dot = MakeRadial("Dot", 32, d => Mathf.Clamp01((1f - d) * 4f));

        public static Sprite Body(BodyKind kind, int variant) => GetBody(kind, variant).Sprite;
        public static Color BodyColor(BodyKind kind, int variant) => GetBody(kind, variant).Color;

        static BodyArt GetBody(BodyKind kind, int variant)
        {
            variant = ((variant % VariantsPerKind) + VariantsPerKind) % VariantsPerKind;
            int key = (int)kind * 100 + variant;
            if (!bodyCache.TryGetValue(key, out var art) || art.Sprite == null)
            {
                art = MakeBody(kind, variant);
                bodyCache[key] = art;
            }
            return art;
        }

        static Sprite MakeRadial(string name, int size, System.Func<float, float> alphaAt)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                px[y * size + x] = new Color(1f, 1f, 1f, d >= 1f ? 0f : alphaAt(d));
            }
            return ToSprite(name, size, px);
        }

        static Sprite ToSprite(string name, int size, Color32[] px)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f, 0, SpriteMeshType.FullRect);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq, y * freq);
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        static Color Hsv(float h, float s, float v) => Color.HSVToRGB(Mathf.Repeat(h, 1f), Mathf.Clamp01(s), Mathf.Clamp01(v));

        static BodyArt MakeBody(BodyKind kind, int variant)
        {
            var rng = new System.Random((int)kind * 7919 + variant * 104729 + 17);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            float ox = Rand(100f, 1000f), oy = Rand(100f, 1000f);
            int n = BodySize;
            var px = new Color32[n * n];
            var light = new Vector3(-0.55f, 0.6f, 0.58f).normalized;

            // Palette per kind/variant.
            Color a, b, c = Color.white, d = Color.white;
            switch (kind)
            {
                case BodyKind.Rock:
                {
                    float h = Rand(0.04f, 0.11f);
                    a = Hsv(h, Rand(0.15f, 0.35f), Rand(0.5f, 0.65f));
                    b = Hsv(h, Rand(0.2f, 0.4f), Rand(0.25f, 0.35f));
                    break;
                }
                case BodyKind.Moon:
                {
                    float h = Rand(0f, 1f);
                    a = Hsv(h, Rand(0.03f, 0.1f), Rand(0.7f, 0.82f));
                    b = Hsv(h, Rand(0.05f, 0.12f), Rand(0.4f, 0.5f));
                    break;
                }
                case BodyKind.Planet:
                    switch (variant)
                    {
                        case 3: a = Hsv(0.08f, 0.45f, 0.85f); b = Hsv(0.03f, 0.6f, 0.55f); break;          // desert
                        case 4: a = Hsv(0.55f, 0.08f, 0.95f); b = Hsv(0.58f, 0.3f, 0.75f); break;          // ice
                        case 5: a = Hsv(0.02f, 0.3f, 0.2f); b = Hsv(0.06f, 0.95f, 1f); break;              // lava
                        default:                                                                           // earth-like
                            a = Hsv(Rand(0.55f, 0.64f), 0.75f, Rand(0.45f, 0.6f));  // ocean
                            b = Hsv(Rand(0.22f, 0.34f), Rand(0.45f, 0.7f), Rand(0.45f, 0.6f)); // land
                            c = Hsv(Rand(0.07f, 0.12f), 0.45f, 0.5f);               // highlands
                            break;
                    }
                    d = variant == 3 ? Hsv(0.08f, 0.3f, 1f) : variant == 5 ? Hsv(0.05f, 0.8f, 1f) : Hsv(0.58f, 0.5f, 1f); // atmosphere
                    break;
                case BodyKind.GasGiant:
                {
                    float[] hues = { 0.08f, 0.58f, 0.76f, 0.06f, 0.48f, 0.96f };
                    float h = hues[variant];
                    a = Hsv(h, Rand(0.25f, 0.4f), Rand(0.85f, 0.95f));
                    b = Hsv(h + Rand(-0.03f, 0.03f), Rand(0.55f, 0.75f), Rand(0.55f, 0.7f));
                    c = Hsv(h - 0.04f, 0.7f, 0.75f); // storm
                    break;
                }
                default: // Star
                {
                    float[] hues = { 0.13f, 0.12f, 0.14f, 0.07f, 0.02f, 0.6f };
                    float sat = variant == 5 ? 0.25f : 0.75f;
                    a = Hsv(hues[variant], sat * 0.3f, 1f);
                    b = Hsv(hues[variant] - 0.02f, sat, 1f);
                    break;
                }
            }

            int craterCount = kind == BodyKind.Rock ? 5 : kind == BodyKind.Moon ? 10 : 0;
            var craters = new Vector3[craterCount];
            for (int i = 0; i < craterCount; i++)
                craters[i] = new Vector3(Rand(-0.75f, 0.75f), Rand(-0.75f, 0.75f), Rand(0.08f, 0.26f));

            float bandFreq = Rand(9f, 15f);
            bool hasStorm = variant % 2 == 0;
            var storm = new Vector2(Rand(-0.4f, 0.4f), Rand(-0.5f, 0.5f));

            Color sum = Color.clear;
            float weight = 0f;
            float aa = 2f / n;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float dist = Mathf.Sqrt(u * u + v * v);
                float edge = 1f - aa;

                if (kind == BodyKind.Rock)
                {
                    // Lumpy silhouette: sample noise around a closed loop so it is seamless.
                    float ang = Mathf.Atan2(v, u);
                    edge *= 1f - 0.2f * Mathf.PerlinNoise(ox + Mathf.Cos(ang) * 1.3f, oy + Mathf.Sin(ang) * 1.3f);
                }

                float alpha = Mathf.Clamp01((edge - dist) * n * 0.5f);
                if (alpha <= 0f)
                {
                    px[y * n + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float nx = u / edge, ny = v / edge;
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                float lambert = Mathf.Max(0f, nx * light.x + ny * light.y + nz * light.z);
                float shade = 0.16f + 0.95f * lambert;
                float f = Fbm(ox + u * 3f, oy + v * 3f, 5);
                Color col;

                switch (kind)
                {
                    case BodyKind.Rock:
                    case BodyKind.Moon:
                        col = Color.Lerp(b, a, Mathf.SmoothStep(0.2f, 0.8f, f));
                        foreach (var cr in craters)
                        {
                            float cd = Vector2.Distance(new Vector2(u, v), new Vector2(cr.x, cr.y)) / cr.z;
                            if (cd < 1f) col *= 0.7f + 0.25f * cd;
                            else if (cd < 1.2f) col *= 1.12f;
                        }
                        break;

                    case BodyKind.Planet:
                        if (variant == 3)
                            col = Color.Lerp(b, a, Mathf.SmoothStep(0.3f, 0.7f, f));
                        else if (variant == 4)
                            col = Color.Lerp(b, a, Mathf.SmoothStep(0.35f, 0.6f, f));
                        else if (variant == 5)
                            col = Mathf.Abs(f - 0.5f) < 0.025f ? b : Color.Lerp(a, a * 1.6f, f);
                        else
                        {
                            bool land = f > 0.52f;
                            col = land ? Color.Lerp(b, c, Mathf.Clamp01((f - 0.52f) * 5f)) : Color.Lerp(a * 0.7f, a, f * 1.6f);
                            if (Mathf.Abs(ny) > 0.82f - 0.15f * f) col = new Color(0.95f, 0.97f, 1f);
                        }
                        if (variant != 5)
                        {
                            float cloud = Fbm(ox + 50f + u * 4f, oy + v * 6f, 4);
                            if (cloud > 0.58f) col = Color.Lerp(col, Color.white, Mathf.Clamp01((cloud - 0.58f) * 3f));
                        }
                        col += d * Mathf.Pow(1f - nz, 3f) * 0.6f;
                        break;

                    case BodyKind.GasGiant:
                    {
                        float t = ny * bandFreq + 1.2f * Fbm(ox + u * 2f, oy + ny * 5f, 4);
                        float band = 0.5f + 0.5f * Mathf.Sin(t);
                        col = Color.Lerp(b, a, band);
                        if (hasStorm)
                        {
                            float ex = (u - storm.x) / 0.26f, ey = (v - storm.y) / 0.13f;
                            float e = ex * ex + ey * ey;
                            if (e < 1f) col = Color.Lerp(col, c, 1f - e);
                        }
                        break;
                    }

                    default: // Star: emissive, limb-darkened, granulated.
                        col = Color.Lerp(a, b, Mathf.Pow(dist, 1.5f)) * (0.85f + 0.3f * f);
                        shade = 1f - 0.25f * Mathf.Pow(dist, 3f);
                        break;
                }

                col *= shade;
                col.a = alpha;
                px[y * n + x] = col;
                sum += col * alpha;
                weight += alpha;
            }

            Color avg = weight > 0f ? sum / weight : Color.gray;
            avg.a = 1f;
            // Debris should read as the body's lit colour, not its shadowed average.
            avg = Color.Lerp(avg, avg * 1.5f, 0.5f);
            avg.a = 1f;
            return new BodyArt { Sprite = ToSprite($"{kind}{variant}", n, px), Color = avg };
        }
    }
}
