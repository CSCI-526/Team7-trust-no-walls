using UnityEngine;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Synthesizes every sound the game needs at startup (no imported audio) and exposes one
    /// Play* method per cue in the spec's Audio section. <see cref="GameFlow"/> calls these off
    /// SimEvents and its own state transitions; this class owns no gameplay logic. M mutes via
    /// AudioListener.volume, matching the rest of the game's global keys.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class Sfx : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _source;
        private AudioClip _step;
        private AudioClip _bump;
        private AudioClip _death;
        private AudioClip _complete;
        private AudioClip _teleport;
        private AudioClip _trigger;
        private AudioClip _memory;
        private AudioClip _warningTick;
        private AudioClip _chaserSpawn;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _step = BuildStep();
            _bump = BuildBump();
            _death = BuildDeath();
            _complete = BuildComplete();
            _teleport = BuildTeleport();
            _trigger = BuildTrigger();
            _memory = BuildMemory();
            _warningTick = BuildWarningTick();
            _chaserSpawn = BuildChaserSpawn();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                AudioListener.volume = AudioListener.volume > 0.5f ? 0f : 1f;
            }
        }

        public void PlayStep() => _source.PlayOneShot(_step, 0.5f);
        public void PlayBump() => _source.PlayOneShot(_bump, 0.7f);
        public void PlayDeath() => _source.PlayOneShot(_death, 0.8f);
        public void PlayComplete() => _source.PlayOneShot(_complete, 0.8f);
        public void PlayTeleport() => _source.PlayOneShot(_teleport, 0.6f);
        public void PlayTrigger() => _source.PlayOneShot(_trigger, 0.7f);
        public void PlayMemory() => _source.PlayOneShot(_memory, 0.5f);
        public void PlayWarning() => _source.PlayOneShot(_warningTick, 0.45f);
        public void PlayChaserSpawn() => _source.PlayOneShot(_chaserSpawn, 0.7f);

        private static AudioClip BuildStep()
        {
            int count = Seconds(0.045f);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-60f * t);
                data[i] = envelope * Mathf.Sin(2f * Mathf.PI * 720f * t) * 0.5f;
            }

            return CreateClip("Step", data);
        }

        private static AudioClip BuildBump()
        {
            int count = Seconds(0.12f);
            float[] data = new float[count];
            System.Random rng = new System.Random(2);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-30f * t);
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                float tone = Mathf.Sin(2f * Mathf.PI * 100f * t);
                data[i] = envelope * ((noise * 0.5f) + (tone * 0.5f));
            }

            return CreateClip("Bump", data);
        }

        private static AudioClip BuildDeath()
        {
            const float duration = 0.55f;
            int count = Seconds(duration);
            float[] data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float freq = Mathf.Lerp(420f, 70f, t / duration);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float envelope = Mathf.Clamp01((duration - t) / 0.15f);
                data[i] = 0.55f * envelope * Mathf.Sin(phase);
            }

            return CreateClip("Death", data);
        }

        private static AudioClip BuildComplete()
        {
            float[] freqs = { 523f, 659f, 784f, 1046f };
            int per = Seconds(0.11f);
            float[] data = new float[per * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
            {
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)SampleRate;
                    float envelope = Mathf.Exp(-8f * t);
                    data[(n * per) + i] = 0.45f * envelope * Mathf.Sin(2f * Mathf.PI * freqs[n] * t);
                }
            }

            return CreateClip("Complete", data);
        }

        private static AudioClip BuildTeleport()
        {
            const float duration = 0.28f;
            int count = Seconds(duration);
            float[] data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float freq = Mathf.Lerp(300f, 1400f, t / duration);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * (t / duration));
                data[i] = 0.4f * envelope * Mathf.Sin(phase);
            }

            return CreateClip("Teleport", data);
        }

        private static AudioClip BuildTrigger()
        {
            int count = Seconds(0.11f);
            float[] data = new float[count];
            System.Random rng = new System.Random(3);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-40f * t);
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                float tone = Mathf.Sin(2f * Mathf.PI * 180f * t);
                data[i] = envelope * ((noise * 0.35f) + (tone * 0.65f)) * 0.6f;
            }

            return CreateClip("Trigger", data);
        }

        private static AudioClip BuildMemory()
        {
            const float duration = 0.32f;
            int count = Seconds(duration);
            float[] data = new float[count];
            float[] freqs = { 1200f, 1500f, 1800f };
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-6f * t) * Mathf.Clamp01(t / 0.02f);
                float sample = 0f;
                foreach (var f in freqs)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * f * t);
                }

                data[i] = envelope * sample * 0.15f;
            }

            return CreateClip("Memory", data);
        }

        private static AudioClip BuildWarningTick()
        {
            int count = Seconds(0.035f);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float phase = Mathf.Repeat(1400f * t, 1f);
                float envelope = Mathf.Exp(-40f * t);
                data[i] = envelope * (phase < 0.5f ? 0.3f : -0.3f);
            }

            return CreateClip("WarningTick", data);
        }

        private static AudioClip BuildChaserSpawn()
        {
            const float duration = 0.5f;
            int count = Seconds(duration);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Clamp01(t / 0.08f);
                float release = Mathf.Clamp01((duration - t) / 0.2f);
                float envelope = attack * release;
                float wobble = Mathf.Sin(2f * Mathf.PI * 4f * t) * 4f;
                data[i] = 0.5f * envelope * Mathf.Sin(2f * Mathf.PI * (60f + wobble) * t);
            }

            return CreateClip("ChaserSpawn", data);
        }

        private static int Seconds(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));

        private static AudioClip CreateClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
