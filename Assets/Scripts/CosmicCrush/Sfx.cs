using UnityEngine;

namespace CosmicCrush
{
    /// <summary>
    /// Synthesizes all sound effects at startup so the project needs no audio assets.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;

        AudioSource source;
        AudioClip[] crunches;
        AudioClip levelUp, death;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            crunches = new[] { Crunch(0.12f, 0.35f, 1), Crunch(0.17f, 0.22f, 2), Crunch(0.24f, 0.12f, 3) };
            levelUp = LevelUp();
            death = Death();
        }

        /// <param name="relativeSize">Eaten body's radius divided by the player's radius (0..1).</param>
        public void PlayCrunch(float relativeSize)
        {
            int i = Mathf.Clamp((int)(relativeSize * crunches.Length), 0, crunches.Length - 1);
            source.PlayOneShot(crunches[i], 0.3f + 0.4f * relativeSize);
        }

        public void PlayLevelUp() => source.PlayOneShot(levelUp, 0.6f);
        public void PlayDeath() => source.PlayOneShot(death, 0.9f);

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Crunch(float duration, float brightness, int seed)
        {
            var rng = new System.Random(seed);
            var data = new float[(int)(duration * Rate)];
            float lp = 0f, phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate, k = t / duration;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * brightness;
                phase += 2f * Mathf.PI * Mathf.Lerp(220f, 70f, k) / Rate;
                data[i] = (lp * 0.9f + Mathf.Sin(phase) * 0.5f) * (1f - k) * (1f - k);
            }
            return Clip("Crunch", data);
        }

        static AudioClip LevelUp()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float step = 0.1f, tail = 0.35f;
            var data = new float[(int)((step * notes.Length + tail) * Rate)];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < data.Length; i++)
                {
                    float lt = (i - start) / (float)Rate;
                    float w = 2f * Mathf.PI * notes[n] * lt;
                    data[i] += (Mathf.Sin(w) + 0.25f * Mathf.Sin(3f * w)) * Mathf.Exp(-lt * 10f) * 0.35f;
                }
            }
            return Clip("LevelUp", data);
        }

        static AudioClip Death()
        {
            var rng = new System.Random(11);
            const float duration = 1.3f;
            var data = new float[(int)(duration * Rate)];
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate, k = t / duration;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * Mathf.Lerp(0.3f, 0.02f, k);
                data[i] = (lp * 1.4f + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.4f) * Mathf.Pow(1f - k, 1.5f);
            }
            return Clip("Death", data);
        }
    }
}
