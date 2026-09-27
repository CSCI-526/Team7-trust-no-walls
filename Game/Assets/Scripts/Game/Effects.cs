using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Simple pooled particle-like bursts built from the same procedural sprites as everything
    /// else (small circles shot outward, fading and shrinking), plus a camera shake. No imported
    /// particle assets; the pool avoids per-effect allocation after warmup.
    /// </summary>
    public sealed class Effects : MonoBehaviour
    {
        private const int SortingOrder = 12;

        private Camera _camera;
        private Vector3 _cameraRestPosition;
        private float _shakeTimer;
        private float _shakeDuration;
        private float _shakeAmplitude;
        private readonly System.Random _rng = new System.Random();

        private readonly Stack<Particle> _pool = new Stack<Particle>();
        private readonly List<Particle> _active = new List<Particle>();

        private sealed class Particle
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float StartScale;
        }

        public void SetCamera(Camera camera)
        {
            _camera = camera;
            _cameraRestPosition = camera.transform.localPosition;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateParticles(dt);
            UpdateShake(dt);
        }

        private void UpdateParticles(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Particle p = _active[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Go.SetActive(false);
                    _pool.Push(p);
                    _active.RemoveAt(i);
                    continue;
                }

                p.Go.transform.position += (Vector3)(p.Velocity * dt);
                float t = 1f - Mathf.Clamp01(p.Life / p.MaxLife);
                float scale = p.StartScale * (1f - 0.5f * t);
                p.Go.transform.localScale = new Vector3(scale, scale, 1f);
                Color c = p.Renderer.color;
                p.Renderer.color = new Color(c.r, c.g, c.b, 1f - t);
            }
        }

        private void UpdateShake(float dt)
        {
            if (_camera == null || _shakeTimer <= 0f)
            {
                return;
            }

            _shakeTimer -= dt;
            if (_shakeTimer <= 0f)
            {
                _shakeTimer = 0f;
                _camera.transform.localPosition = _cameraRestPosition;
                return;
            }

            float amplitude = _shakeAmplitude * (_shakeTimer / _shakeDuration);
            float ox = ((float)_rng.NextDouble() * 2f - 1f) * amplitude;
            float oy = ((float)_rng.NextDouble() * 2f - 1f) * amplitude;
            _camera.transform.localPosition = _cameraRestPosition + new Vector3(ox, oy, 0f);
        }

        /// <summary>Shakes the camera for <paramref name="duration"/> seconds at <paramref name="amplitude"/> world units.</summary>
        public void Shake(float duration, float amplitude)
        {
            _shakeDuration = duration;
            _shakeTimer = duration;
            _shakeAmplitude = amplitude;
        }

        /// <summary>A ring of small sparks shooting outward from <paramref name="worldPos"/>.</summary>
        public void Burst(Vector2 worldPos, Color color, int count = 10, float speed = 3.5f, float life = 0.45f, float scale = 0.16f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + (float)_rng.NextDouble() * 0.3f;
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                SpawnParticle(worldPos, velocity, color, life, scale);
            }
        }

        /// <summary>A tight sparkle at a point (teleport, memory reveal).</summary>
        public void Sparkle(Vector2 worldPos, Color color)
        {
            Burst(worldPos, color, count: 8, speed: 2.2f, life: 0.35f, scale: 0.12f);
        }

        /// <summary>Dust puff for a collapsing tile.</summary>
        public void Dust(Vector2 worldPos)
        {
            Burst(worldPos, new Color(0.55f, 0.55f, 0.6f, 1f), count: 6, speed: 1.6f, life: 0.4f, scale: 0.13f);
        }

        private void SpawnParticle(Vector2 pos, Vector2 velocity, Color color, float life, float scale)
        {
            Particle p = _pool.Count > 0 ? _pool.Pop() : CreateParticle();
            p.Go.transform.position = pos;
            p.Go.transform.localScale = new Vector3(scale, scale, 1f);
            p.Go.SetActive(true);
            p.Renderer.color = color;
            p.Velocity = velocity;
            p.Life = life;
            p.MaxLife = life;
            p.StartScale = scale;
            _active.Add(p);
        }

        private Particle CreateParticle()
        {
            GameObject go = new GameObject("Particle");
            go.transform.SetParent(transform);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = SpriteFactory.Circle;
            r.sortingOrder = SortingOrder;
            go.SetActive(false);
            return new Particle { Go = go, Renderer = r };
        }
    }
}
