using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicCrush
{
    /// <summary>
    /// Runs the whole game: spawning, physics, crashes, growth, camera and HUD.
    /// Crash into anything smaller than you to absorb it; touching anything bigger is instant death, and it pulls you in with gravity.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        enum State { Title, Playing, Paused, GameOver, Won }

        struct Stage
        {
            public readonly string Name;
            public readonly float Radius;
            public readonly BodyKind Kind;
            public Stage(string name, float radius, BodyKind kind) { Name = name; Radius = radius; Kind = kind; }
        }

        static readonly Stage[] Stages =
        {
            new Stage("Asteroid", 0.5f, BodyKind.Rock),
            new Stage("Moon", 1.5f, BodyKind.Moon),
            new Stage("Planet", 5f, BodyKind.Planet),
            new Stage("Gas Giant", 15f, BodyKind.GasGiant),
            new Stage("Star", 45f, BodyKind.Star),
        };

        const float WinRadius = 135f;
        const float ViewPerRadius = 10f;      // camera orthographic size / player radius
        const float Thrust = 9f;              // acceleration, in player radii per second²
        const float Drag = 0.8f;
        const float GrowthPerArea = 0.5f;     // fraction of an eaten body's area the player gains
        const float GravityStrength = 5f;     // pull at a big body's surface, in player radii per second²
        const float GravityRange = 8f;        // in radii of the pulling body
        const float AttractStrength = 2.5f;   // how strongly the player sucks in nearby small bodies
        const int TargetBodies = 55;
        const float AbsorbDuration = 0.25f;
        const int MaxDebris = 300;
        const string BestKey = "CosmicCrush.Best";

        class FloatingText { public Vector2 Pos; public string Text; public float Age; public Color Color; }

        class Debris
        {
            public Transform T;
            public SpriteRenderer Sr;
            public Vector2 Vel;
            public Color Color;
            public float Life, MaxLife, Size;
        }

        Camera cam;
        Sfx sfx;
        Transform world;
        SpaceBody player;
        float targetRadius;
        readonly List<SpaceBody> bodies = new List<SpaceBody>();
        readonly List<Debris> debris = new List<Debris>();
        readonly List<FloatingText> texts = new List<FloatingText>();

        State state;
        int score, best, stageIndex;
        float shake, bannerTime;
        string banner;
        bool endless;
        Vector3 camBase;

        void Awake()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.015f, 0.05f);

            world = new GameObject("World").transform;
            sfx = gameObject.AddComponent<Sfx>();
            new GameObject("Star Field").AddComponent<StarField>().Init(cam);
            best = PlayerPrefs.GetInt(BestKey, 0);

            ResetWorld();
            state = State.Title;
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            var kb = Keyboard.current;

            switch (state)
            {
                case State.Title:
                    if (Pressed(kb, Key.Space) || Pressed(kb, Key.Enter)) state = State.Playing;
                    SimulateBodies(dt, false);
                    break;

                case State.Playing:
                    if (Pressed(kb, Key.Escape) || Pressed(kb, Key.P))
                    {
                        state = State.Paused;
                        break;
                    }
                    SimulatePlayer(dt);
                    SimulateBodies(dt, true);
                    MaintainPopulation();
                    break;

                case State.Paused:
                    if (Pressed(kb, Key.Escape) || Pressed(kb, Key.P)) state = State.Playing;
                    else if (Pressed(kb, Key.R)) Restart();
                    return; // freeze everything, camera included

                case State.GameOver:
                    SimulateBodies(dt, false);
                    if (Pressed(kb, Key.Space) || Pressed(kb, Key.R)) Restart();
                    break;

                case State.Won:
                    SimulateBodies(dt, false);
                    if (Pressed(kb, Key.C)) ContinueEndless();
                    else if (Pressed(kb, Key.R)) Restart();
                    break;
            }

            UpdateDebris(dt);
            UpdateTexts(dt);
            UpdateCamera(dt);
        }

        static bool Pressed(Keyboard kb, Key key) => kb != null && kb[key].wasPressedThisFrame;

        static Vector2 ReadMove()
        {
            var move = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) move.x -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) move.x += 1f;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) move.y -= 1f;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) move.y += 1f;
            }
            var pad = Gamepad.current;
            if (pad != null) move += pad.leftStick.ReadValue();
            return Vector2.ClampMagnitude(move, 1f);
        }

        void ContinueEndless()
        {
            endless = true;
            state = State.Playing;
        }

        void Restart()
        {
            ResetWorld();
            state = State.Playing;
        }

        void ResetWorld()
        {
            foreach (var b in bodies) Destroy(b.gameObject);
            bodies.Clear();
            foreach (var d in debris) d.T.gameObject.SetActive(false);
            texts.Clear();

            if (player == null)
            {
                player = SpaceBody.Create("Player", Stages[0].Kind, 0, Stages[0].Radius, world);
                player.SetSorting(200);
                player.SetGlow(new Color(0.35f, 0.85f, 1f, 0.35f), 1.7f);
            }
            player.gameObject.SetActive(true);
            stageIndex = 0;
            targetRadius = Stages[0].Radius;
            player.SetKind(Stages[0].Kind, Random.Range(0, ProceduralArt.VariantsPerKind));
            player.SetRadius(targetRadius);
            player.Position = Vector2.zero;
            player.Velocity = Vector2.zero;

            score = 0;
            endless = false;
            banner = null;
            bannerTime = 0f;
            shake = 0f;

            cam.orthographicSize = targetRadius * ViewPerRadius;
            camBase = new Vector3(0f, 0f, -10f);
            cam.transform.position = camBase;

            for (int i = 0; i < TargetBodies; i++) SpawnBody(true);
        }

        // ---------------------------------------------------------------- simulation

        void SimulatePlayer(float dt)
        {
            player.Velocity += ReadMove() * (Thrust * player.Radius * dt);
            player.Velocity *= Mathf.Exp(-Drag * dt);
            player.Position += player.Velocity * dt;
            player.SetRadius(Mathf.Lerp(player.Radius, targetRadius, 1f - Mathf.Exp(-6f * dt)));
            player.Tick(dt);

            bannerTime -= dt;
        }

        void SimulateBodies(float dt, bool interact)
        {
            Vector2 pp = player.Position;
            float pr = player.Radius;

            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                var b = bodies[i];

                if (b.Dying)
                {
                    b.DyingTime += dt;
                    float t = b.DyingTime / AbsorbDuration;
                    if (t >= 1f)
                    {
                        Destroy(b.gameObject);
                        bodies.RemoveAt(i);
                        continue;
                    }
                    b.Position = Vector2.Lerp(b.Position, pp, 1f - Mathf.Exp(-12f * dt));
                    b.SetVisualScale(b.Radius * (1f - t));
                    continue;
                }

                b.Position += b.Velocity * dt;
                b.Tick(dt);

                if (!interact || state != State.Playing) continue;

                Vector2 delta = b.Position - pp;
                float d = delta.magnitude;
                if (d < 1e-4f) continue;
                Vector2 dir = delta / d;

                if (b.Radius >= pr)
                {
                    if (d < b.Radius * GravityRange)
                    {
                        float s = b.Radius / Mathf.Max(d, b.Radius);
                        player.Velocity += dir * (GravityStrength * pr * s * s * dt);
                    }
                    if (d < pr + b.Radius) Die(pp + dir * pr);
                }
                else
                {
                    if (d < pr * 4f)
                    {
                        float s = pr / Mathf.Max(d, pr);
                        b.Velocity -= dir * (AttractStrength * pr * s * s * dt);
                    }
                    if (d < pr + b.Radius * 0.5f) Absorb(b, dir);
                }
            }
        }

        void Absorb(SpaceBody b, Vector2 dir)
        {
            b.Dying = true;
            b.DyingTime = 0f;

            float pr = player.Radius;
            float relative = b.Radius / pr;
            targetRadius = Mathf.Sqrt(targetRadius * targetRadius + b.Radius * b.Radius * GrowthPerArea);

            int pts = Mathf.Max(5, Mathf.RoundToInt(50f * relative * (stageIndex + 1)));
            score += pts;
            AddText(b.Position, $"+{pts}", Color.white);

            SpawnDebris(player.Position + dir * pr, b.MainColor, 6 + (int)(10f * relative), pr * 3f, b.Radius * 0.3f);
            sfx.PlayCrunch(relative);
            shake = Mathf.Max(shake, 0.25f * relative);
            CheckStage();
        }

        void Die(Vector2 impact)
        {
            float pr = player.Radius;
            SpawnDebris(player.Position, player.MainColor, 40, pr * 6f, pr * 0.35f);
            SpawnDebris(impact, player.MainColor, 15, pr * 8f, pr * 0.2f);
            player.gameObject.SetActive(false);
            sfx.PlayDeath();
            shake = 1f;
            state = State.GameOver;
            SaveBest();
        }

        void CheckStage()
        {
            while (stageIndex + 1 < Stages.Length && targetRadius >= Stages[stageIndex + 1].Radius)
            {
                stageIndex++;
                player.SetKind(Stages[stageIndex].Kind, Random.Range(0, ProceduralArt.VariantsPerKind));
                banner = $"You are now a {Stages[stageIndex].Name}!";
                bannerTime = 2.5f;
                score += 1000 * stageIndex;
                sfx.PlayLevelUp();
            }

            if (!endless && targetRadius >= WinRadius)
            {
                state = State.Won;
                SaveBest();
                sfx.PlayLevelUp();
            }
        }

        void SaveBest()
        {
            if (score <= best) return;
            best = score;
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- spawning

        Vector2 ViewHalfExtents(float radius)
        {
            float h = radius * ViewPerRadius;
            return new Vector2(h * cam.aspect, h);
        }

        static BodyKind KindForRadius(float r)
        {
            int k = 0;
            for (int i = 0; i < Stages.Length; i++)
                if (r >= Stages[i].Radius) k = i;
            return Stages[k].Kind;
        }

        void SpawnBody(bool initial)
        {
            float p = targetRadius;
            float r;
            if (Random.value < 0.05f)
                r = p * Random.Range(4f, 8f); // a cosmic giant with a strong pull
            else if (Random.value < 0.68f)
                r = p * Mathf.Lerp(0.18f, 0.92f, Mathf.Pow(Random.value, 0.8f));
            else
                r = p * Mathf.Lerp(1.1f, 3f, Mathf.Pow(Random.value, 1.6f));

            float viewRad = ViewHalfExtents(p).magnitude;
            float dist = initial && r < p
                ? Random.Range(p * 4f, viewRad * 1.6f)
                : Random.Range(viewRad * 1.1f, viewRad * 1.7f) + r;
            Vector2 pos = player.Position + Random.insideUnitCircle.normalized * dist;

            var b = SpaceBody.Create("Body", KindForRadius(r), Random.Range(0, ProceduralArt.VariantsPerKind), r, world);
            b.Position = pos;
            b.Velocity = Random.insideUnitCircle * (p * Random.Range(0.3f, 1.4f) / Mathf.Sqrt(Mathf.Max(1f, r / p)));
            b.SetSorting(Mathf.Clamp(-Mathf.RoundToInt(Mathf.Log(r) * 8f), -190, 140));
            bodies.Add(b);
        }

        void MaintainPopulation()
        {
            Vector2 pp = player.Position;
            float p = targetRadius;
            float viewRad = ViewHalfExtents(p).magnitude;
            int alive = 0;

            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                var b = bodies[i];
                if (b.Dying) continue;
                float d = (b.Position - pp).magnitude;
                bool tooFar = d - b.Radius > viewRad * 2.6f;
                bool tooSmall = b.Radius < p * 0.1f && d > viewRad * 1.1f;
                if (tooFar || tooSmall)
                {
                    Destroy(b.gameObject);
                    bodies.RemoveAt(i);
                    continue;
                }
                alive++;
            }

            for (int i = 0; i < 4 && alive < TargetBodies; i++, alive++) SpawnBody(false);
        }

        // ---------------------------------------------------------------- effects

        void SpawnDebris(Vector2 pos, Color color, int count, float speed, float size)
        {
            for (int i = 0; i < count; i++)
            {
                var d = GetDebris();
                if (d == null) return;
                d.T.position = pos + Random.insideUnitCircle * size;
                d.Vel = Random.insideUnitCircle.normalized * (speed * Random.Range(0.3f, 1f));
                d.MaxLife = d.Life = Random.Range(0.4f, 0.9f);
                d.Size = size * Random.Range(0.5f, 1.2f);
                d.Color = color * Random.Range(0.8f, 1.2f);
                d.Color.a = 1f;
                d.T.gameObject.SetActive(true);
            }
        }

        Debris GetDebris()
        {
            foreach (var d in debris)
                if (!d.T.gameObject.activeSelf) return d;
            if (debris.Count >= MaxDebris) return null;

            var go = new GameObject("Debris");
            go.transform.SetParent(world, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralArt.Dot;
            sr.sharedMaterial = ProceduralArt.SpriteMaterial;
            sr.sortingOrder = 160;
            var created = new Debris { T = go.transform, Sr = sr };
            debris.Add(created);
            return created;
        }

        void UpdateDebris(float dt)
        {
            foreach (var d in debris)
            {
                if (!d.T.gameObject.activeSelf) continue;
                d.Life -= dt;
                if (d.Life <= 0f)
                {
                    d.T.gameObject.SetActive(false);
                    continue;
                }
                d.T.position += (Vector3)(d.Vel * dt);
                d.Vel *= Mathf.Exp(-2f * dt);
                float a = d.Life / d.MaxLife;
                d.Sr.color = new Color(d.Color.r, d.Color.g, d.Color.b, a);
                d.T.localScale = Vector3.one * (d.Size * (0.5f + 0.5f * a));
            }
        }

        void AddText(Vector2 pos, string text, Color color) =>
            texts.Add(new FloatingText { Pos = pos, Text = text, Color = color });

        void UpdateTexts(float dt)
        {
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                texts[i].Age += dt;
                if (texts[i].Age > 1.2f) texts.RemoveAt(i);
            }
        }

        void UpdateCamera(float dt)
        {
            float targetSize = Mathf.Max(player.Radius, 0.01f) * ViewPerRadius;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, 1f - Mathf.Exp(-2.5f * dt));

            Vector2 focus = player.Position;
            if (state == State.Playing) focus += player.Velocity * 0.15f;
            camBase = Vector3.Lerp(camBase, new Vector3(focus.x, focus.y, -10f), 1f - Mathf.Exp(-5f * dt));

            Vector2 jitter = Random.insideUnitCircle * (shake * cam.orthographicSize * 0.04f);
            cam.transform.position = camBase + (Vector3)jitter;
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
        }

        // ---------------------------------------------------------------- HUD

        GUIStyle left, center, big, medium, small, button;
        float guiScale = -1f;

        void EnsureStyles()
        {
            float s = Screen.height / 720f;
            if (left != null && Mathf.Approximately(s, guiScale)) return;
            guiScale = s;

            left = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            left.normal.textColor = Color.white;
            center = new GUIStyle(left) { alignment = TextAnchor.MiddleCenter };
            big = new GUIStyle(center) { fontSize = Mathf.RoundToInt(72 * s) };
            medium = new GUIStyle(center) { fontSize = Mathf.RoundToInt(32 * s) };
            small = new GUIStyle(center) { fontSize = Mathf.RoundToInt(20 * s), fontStyle = FontStyle.Normal };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(26 * s), fontStyle = FontStyle.Bold };
        }

        // Labels switch to their hover colour under the mouse, so every state must share the colour
        // or the shadow copy lights up and the text appears doubled.
        static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = color;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            SetTextColor(style, new Color(0f, 0f, 0f, 0.8f * color.a));
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            SetTextColor(style, color);
            GUI.Label(r, text, style);
        }

        bool MenuButton(float y, string text)
        {
            float s = guiScale;
            return GUI.Button(new Rect(Screen.width / 2f - 130f * s, y, 260f * s, 48f * s), text, button);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Fill(Rect r, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Bar(Rect r, float t, Color color)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.55f));
            Fill(new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * Mathf.Clamp01(t), r.height - 4f), color);
        }

        void OnGUI()
        {
            EnsureStyles();
            float s = guiScale, w = Screen.width, h = Screen.height;

            foreach (var t in texts)
            {
                Vector3 sp = cam.WorldToScreenPoint(t.Pos + Vector2.up * (t.Age * cam.orthographicSize * 0.15f));
                var c = t.Color;
                c.a = 1f - t.Age / 1.2f;
                Shadowed(new Rect(sp.x - 150f * s, h - sp.y - 15f * s, 300f * s, 30f * s), t.Text, small, c);
            }

            if (state != State.Title) DrawHud(s, w, h);

            switch (state)
            {
                case State.Title:
                    Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.35f));
                    Shadowed(new Rect(0, h * 0.18f, w, 90f * s), "COSMIC CRUSH", big, new Color(1f, 0.85f, 0.4f));
                    Shadowed(new Rect(0, h * 0.40f, w, 30f * s), "Crash into anything smaller than you to absorb it and grow.", small, Color.white);
                    Shadowed(new Rect(0, h * 0.40f + 32f * s, w, 30f * s), "Touch anything bigger and you're crushed. Beware of its gravity.", small, Color.white);
                    Shadowed(new Rect(0, h * 0.40f + 64f * s, w, 30f * s), "Grow from asteroid to moon, planet, gas giant and finally a star.", small, Color.white);
                    Shadowed(new Rect(0, h * 0.40f + 112f * s, w, 30f * s), "ARROWS / WASD to move      Esc to pause", small, new Color(0.7f, 0.9f, 1f));
                    if (MenuButton(h * 0.66f, "Play")) state = State.Playing;
                    if (MenuButton(h * 0.66f + 60f * s, "Quit")) Quit();
                    if (best > 0) Shadowed(new Rect(0, h * 0.66f + 120f * s, w, 30f * s), $"Best: {best:N0}", small, Color.gray);
                    break;

                case State.Paused:
                    Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.5f));
                    Shadowed(new Rect(0, h * 0.25f, w, 90f * s), "PAUSED", big, Color.white);
                    if (MenuButton(h * 0.25f + 120f * s, "Resume")) state = State.Playing;
                    if (MenuButton(h * 0.25f + 180f * s, "Restart")) Restart();
                    if (MenuButton(h * 0.25f + 240f * s, "Quit")) Quit();
                    break;

                case State.GameOver:
                    Fill(new Rect(0, 0, w, h), new Color(0.2f, 0f, 0f, 0.35f));
                    Shadowed(new Rect(0, h * 0.3f, w, 90f * s), "CRUSHED", big, new Color(1f, 0.4f, 0.3f));
                    Shadowed(new Rect(0, h * 0.3f + 100f * s, w, 40f * s), $"Score: {score:N0}      Best: {best:N0}", medium, Color.white);
                    Shadowed(new Rect(0, h * 0.3f + 150f * s, w, 30f * s), $"You made it to {Stages[stageIndex].Name}", small, Color.white);
                    if (MenuButton(h * 0.3f + 200f * s, "Try Again")) Restart();
                    if (MenuButton(h * 0.3f + 260f * s, "Quit")) Quit();
                    break;

                case State.Won:
                    Fill(new Rect(0, 0, w, h), new Color(0.2f, 0.15f, 0f, 0.35f));
                    Shadowed(new Rect(0, h * 0.3f, w, 90f * s), "SUPERNOVA!", big, new Color(1f, 0.9f, 0.4f));
                    Shadowed(new Rect(0, h * 0.3f + 100f * s, w, 40f * s), $"Score: {score:N0}      Best: {best:N0}", medium, Color.white);
                    Shadowed(new Rect(0, h * 0.3f + 150f * s, w, 30f * s), "You grew from a speck of rock into a star.", small, Color.white);
                    if (MenuButton(h * 0.3f + 200f * s, "Keep Crushing")) ContinueEndless();
                    if (MenuButton(h * 0.3f + 260f * s, "Restart")) Restart();
                    if (MenuButton(h * 0.3f + 320f * s, "Quit")) Quit();
                    break;
            }
        }

        void DrawHud(float s, float w, float h)
        {
            Shadowed(new Rect(20f * s, 12f * s, 400f * s, 32f * s), $"SCORE  {score:N0}", left, Color.white);
            Shadowed(new Rect(20f * s, 42f * s, 400f * s, 24f * s), $"BEST  {Mathf.Max(best, score):N0}", left, new Color(0.7f, 0.7f, 0.7f));

            bool last = stageIndex + 1 >= Stages.Length;
            float from = Stages[stageIndex].Radius;
            float to = last ? WinRadius : Stages[stageIndex + 1].Radius;
            float progress = Mathf.InverseLerp(Mathf.Log(from), Mathf.Log(to), Mathf.Log(targetRadius));
            Shadowed(new Rect(w / 2f - 200f * s, 10f * s, 400f * s, 36f * s), Stages[stageIndex].Name.ToUpper(), medium, Color.white);
            Bar(new Rect(w / 2f - 150f * s, 50f * s, 300f * s, 14f * s), progress, new Color(0.4f, 0.85f, 1f));
            Shadowed(new Rect(w / 2f - 150f * s, 66f * s, 300f * s, 24f * s),
                endless && last ? "Endless mode" : $"next: {(last ? "Supernova" : Stages[stageIndex + 1].Name)}", small, new Color(0.8f, 0.8f, 0.8f));

            if (bannerTime > 0f && banner != null)
                Shadowed(new Rect(0, h * 0.22f, w, 50f * s), banner, medium, new Color(1f, 0.9f, 0.5f, Mathf.Clamp01(bannerTime)));
        }
    }
}
