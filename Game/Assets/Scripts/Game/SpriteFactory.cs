using UnityEngine;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Generates every sprite the game needs entirely from code (no imported art). Everything is
    /// drawn white with alpha so it can be tinted via SpriteRenderer.color, and every shape is
    /// rasterized once and cached (never per-frame).
    /// </summary>
    public static class SpriteFactory
    {
        private const int Ppu = 128;

        private static Sprite _square;
        private static Sprite _wallPill;
        private static Sprite _circle;
        private static Sprite _glow;
        private static Sprite _ring;
        private static Sprite _star;
        private static Sprite _blob;
        private static Sprite _eye;
        private static Sprite _spikeOrb;
        private static Sprite _arrowChevron;
        private static Sprite _swirl;
        private static Sprite _crackOverlay;
        private static Sprite _plate;

        public static Sprite Square => _square != null ? _square : (_square = CreateSquare());
        public static Sprite WallPill => _wallPill != null ? _wallPill : (_wallPill = CreateWallPill());
        public static Sprite Circle => _circle != null ? _circle : (_circle = CreateCircle());
        public static Sprite Glow => _glow != null ? _glow : (_glow = CreateGlow());
        public static Sprite Ring => _ring != null ? _ring : (_ring = CreateRing());
        public static Sprite Star => _star != null ? _star : (_star = CreateStar());
        public static Sprite Blob => _blob != null ? _blob : (_blob = CreateBlob());
        public static Sprite Eye => _eye != null ? _eye : (_eye = CreateEye());
        public static Sprite SpikeOrb => _spikeOrb != null ? _spikeOrb : (_spikeOrb = CreateSpikeOrb());
        public static Sprite ArrowChevron => _arrowChevron != null ? _arrowChevron : (_arrowChevron = CreateArrowChevron());
        public static Sprite Swirl => _swirl != null ? _swirl : (_swirl = CreateSwirl());
        public static Sprite CrackOverlay => _crackOverlay != null ? _crackOverlay : (_crackOverlay = CreateCrackOverlay());
        public static Sprite Plate => _plate != null ? _plate : (_plate = CreatePlate());

        // ---- Shapes ----

        private static Sprite CreateSquare()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Color.white;
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>
        /// A capsule spanning the whole texture width: placed with localScale (1, 1) between two
        /// lattice points exactly one unit apart, its rounded caps extend half the thickness past
        /// each end, so adjacent wall segments meeting at a corner overlap and close any gap.
        /// </summary>
        private static Sprite CreateWallPill()
        {
            const int w = Ppu;
            const int h = 20; // ~0.156 units tall before AA falloff; visually reads as ~0.12 thick.
            const float thickness = 16f; // px

            Texture2D tex = NewTexture(w, h);
            Vector2 a = new Vector2(0f, h / 2f);
            Vector2 b = new Vector2(w, h / 2f);

            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float alpha = SegmentAlpha(p, a, b, thickness);
                    px[(y * w) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        private static Sprite CreateCircle()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2f;

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float alpha = CircleAlpha(p, center, radius);
                    px[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>A soft radial gradient (full alpha at the center, fading smoothly to 0 at the edge), for halos.</summary>
        private static Sprite CreateGlow()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Clamp01(Vector2.Distance(p, center) / radius);
                    float alpha = 1f - d;
                    alpha *= alpha;
                    px[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        private static Sprite CreateRing()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outer = size / 2f - 2f;
            const float thickness = 10f;
            float inner = outer - thickness;

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = Vector2.Distance(p, center);
                    float outAlpha = Mathf.Clamp01(outer - dist + 0.5f);
                    float inAlpha = Mathf.Clamp01(dist - inner + 0.5f);
                    px[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Min(outAlpha, inAlpha));
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        private static Sprite CreateStar()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outer = size / 2f - 4f;
            float inner = outer * 0.5f;

            Vector2[] verts = StarVertices(center, 5, outer, inner, -90f);
            Color[] px = RasterPolygonSupersampled(size, size, verts);

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>A smooth, blobby rounded square for the player body (a rounded-box SDF).</summary>
        private static Sprite CreateBlob()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            Vector2 halfSize = new Vector2(size / 2f - 6f, size / 2f - 6f);
            float radius = halfSize.x * 0.55f;

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float sdf = RoundedBoxSdf(p, halfSize, radius);
                    float alpha = Mathf.Clamp01(-sdf + 0.5f);
                    px[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        private static Sprite CreateEye()
        {
            const int size = 64;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 4f;

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    px[(y * size) + x] = new Color(1f, 1f, 1f, CircleAlpha(p, center, radius));
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>A round core with sharp thorns radiating outward, for the patrol hazard.</summary>
        private static Sprite CreateSpikeOrb()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float coreRadius = size / 2f * 0.5f;
            float spikeOuter = size / 2f - 3f;
            float spikeInner = coreRadius * 1.05f;

            Vector2[] verts = StarVertices(center, 8, spikeOuter, spikeInner, 0f);
            Color[] spikes = RasterPolygonSupersampled(size, size, verts);

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float core = CircleAlpha(p, center, coreRadius);
                    int i = (y * size) + x;
                    px[i] = new Color(1f, 1f, 1f, Mathf.Max(core, spikes[i].a));
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>A double chevron ">>" pointing along +X by default; rotate the instance to aim it.</summary>
        private static Sprite CreateArrowChevron()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            float mid = size / 2f;
            const float thickness = 12f;

            Vector2 a1 = new Vector2(size * 0.30f, size * 0.78f);
            Vector2 a2 = new Vector2(size * 0.58f, mid);
            Vector2 a3 = new Vector2(size * 0.30f, size * 0.22f);

            Vector2 b1 = new Vector2(size * 0.55f, size * 0.78f);
            Vector2 b2 = new Vector2(size * 0.83f, mid);
            Vector2 b3 = new Vector2(size * 0.55f, size * 0.22f);

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float aAlpha = Mathf.Max(SegmentAlpha(p, a1, a2, thickness), SegmentAlpha(p, a2, a3, thickness));
                    float bAlpha = Mathf.Max(SegmentAlpha(p, b1, b2, thickness), SegmentAlpha(p, b2, b3, thickness));
                    px[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Max(aAlpha, bAlpha));
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>A two-armed spiral, for the teleporter pad.</summary>
        private static Sprite CreateSwirl()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f - 6f;
            const float thickness = 7f;
            const int samples = 48;
            const float turns = 1.35f;

            Color[] px = new Color[size * size];
            for (int arm = 0; arm < 2; arm++)
            {
                float armOffset = arm * Mathf.PI;
                Vector2 prev = center;
                for (int i = 1; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    float angle = armOffset + t * turns * 2f * Mathf.PI;
                    float radius = t * maxRadius;
                    Vector2 cur = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                            float alpha = SegmentAlpha(p, prev, cur, thickness);
                            int idx = (y * size) + x;
                            if (alpha > px[idx].a)
                            {
                                px[idx] = new Color(1f, 1f, 1f, alpha);
                            }
                        }
                    }

                    prev = cur;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>A few jagged crack lines radiating from off-center, for a collapsing tile.</summary>
        private static Sprite CreateCrackOverlay()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);

            // Fixed (not random-per-call) so the sprite is deterministic and cacheable.
            Vector2[][] cracks =
            {
                new[]
                {
                    new Vector2(size * 0.50f, size * 0.50f), new Vector2(size * 0.30f, size * 0.62f),
                    new Vector2(size * 0.12f, size * 0.58f)
                },
                new[]
                {
                    new Vector2(size * 0.50f, size * 0.50f), new Vector2(size * 0.68f, size * 0.30f),
                    new Vector2(size * 0.82f, size * 0.14f)
                },
                new[]
                {
                    new Vector2(size * 0.50f, size * 0.50f), new Vector2(size * 0.62f, size * 0.72f),
                    new Vector2(size * 0.55f, size * 0.92f)
                },
                new[]
                {
                    new Vector2(size * 0.50f, size * 0.50f), new Vector2(size * 0.24f, size * 0.32f),
                    new Vector2(size * 0.08f, size * 0.20f)
                },
            };

            const float thickness = 4.5f;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float alpha = 0f;
                    foreach (var crack in cracks)
                    {
                        for (int i = 0; i + 1 < crack.Length; i++)
                        {
                            alpha = Mathf.Max(alpha, SegmentAlpha(p, crack[i], crack[i + 1], thickness));
                        }
                    }

                    px[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        /// <summary>A rounded plate with an inset bevel ring, for trigger/pressure plates.</summary>
        private static Sprite CreatePlate()
        {
            const int size = Ppu;
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            Vector2 halfSize = new Vector2(size / 2f - 8f, size / 2f - 8f);
            float radius = halfSize.x * 0.35f;

            Vector2 innerHalf = halfSize * 0.62f;
            float innerRadius = innerHalf.x * 0.35f;

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float outerSdf = RoundedBoxSdf(p, halfSize, radius);
                    float outerAlpha = Mathf.Clamp01(-outerSdf + 0.5f);

                    float innerSdf = RoundedBoxSdf(p, innerHalf, innerRadius);
                    float innerAlpha = Mathf.Clamp01(-innerSdf + 0.5f);

                    // Base plate at 0.8, inset bevel brightened to full.
                    float alpha = outerAlpha * (0.8f + 0.2f * innerAlpha);
                    px[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return ToSprite(tex, Ppu);
        }

        // ---- Shared math helpers ----

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
            {
                return Vector2.Distance(p, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 proj = a + (ab * t);
            return Vector2.Distance(p, proj);
        }

        private static float SegmentAlpha(Vector2 p, Vector2 a, Vector2 b, float thickness)
        {
            float dist = DistanceToSegment(p, a, b);
            return Mathf.Clamp01((thickness / 2f) - dist + 0.5f);
        }

        private static float CircleAlpha(Vector2 p, Vector2 center, float radius)
        {
            return Mathf.Clamp01(radius - Vector2.Distance(p, center) + 0.5f);
        }

        /// <summary>Signed distance to a rounded box centered at the origin (p is already local).</summary>
        private static float RoundedBoxSdf(Vector2 p, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x) - halfSize.x + radius, Mathf.Abs(p.y) - halfSize.y + radius);
            float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
            return outside + inside - radius;
        }

        private static Vector2[] StarVertices(Vector2 center, int points, float outerRadius, float innerRadius, float startAngleDeg)
        {
            int n = points * 2;
            Vector2[] verts = new Vector2[n];
            float start = startAngleDeg * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float angle = start + i * Mathf.PI / points;
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                verts[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }

            return verts;
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] verts)
        {
            bool inside = false;
            for (int i = 0, j = verts.Length - 1; i < verts.Length; j = i++)
            {
                Vector2 vi = verts[i];
                Vector2 vj = verts[j];
                bool crosses = (vi.y > p.y) != (vj.y > p.y);
                if (crosses)
                {
                    float xIntersect = vj.x + (p.y - vj.y) / (vi.y - vj.y) * (vi.x - vj.x);
                    if (p.x < xIntersect)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        private static Color[] RasterPolygonSupersampled(int w, int h, Vector2[] verts)
        {
            const int subSamples = 3;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < subSamples; sy++)
                    {
                        for (int sx = 0; sx < subSamples; sx++)
                        {
                            Vector2 p = new Vector2(
                                x + (sx + 0.5f) / subSamples,
                                y + (sy + 0.5f) / subSamples);
                            if (PointInPolygon(p, verts))
                            {
                                hits++;
                            }
                        }
                    }

                    float alpha = hits / (float)(subSamples * subSamples);
                    px[(y * w) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return px;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        private static Sprite ToSprite(Texture2D texture, float pixelsPerUnit)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }
    }
}
