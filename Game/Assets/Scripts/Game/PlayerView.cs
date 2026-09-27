using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>
    /// The player's on-screen body: a cyan blob with two eyes that look in the move direction,
    /// squash on each step, a small nudge on a bump, and a shrink-and-flash-red death animation
    /// (driven by <see cref="TriggerDeath"/>, since the sim itself freezes its own clock on death).
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const float DeathDuration = 0.6f;
        private const float EyeForward = 0.15f;
        private const float EyeSpacing = 0.11f;

        private SpriteRenderer _body;
        private SpriteRenderer _eyeL;
        private SpriteRenderer _eyeR;

        private LevelSim _sim;
        private bool _dying;
        private float _deathTimer;

        private void Awake()
        {
            _body = CreateChild("Body", SpriteFactory.Blob, Palette.Player, 10, transform, 1f);
            _eyeL = CreateChild("EyeL", SpriteFactory.Eye, Color.black, 11, transform, 0.32f);
            _eyeR = CreateChild("EyeR", SpriteFactory.Eye, Color.black, 11, transform, 0.32f);
        }

        /// <summary>Rebinds this view to a fresh attempt (new sim or a reset one) and snaps to Start.</summary>
        public void Attach(LevelSim sim)
        {
            _sim = sim;
            _dying = false;
            _deathTimer = 0f;
            transform.localScale = Vector3.one;
            _body.color = Palette.Player;
            transform.position = (Vector2)sim.PlayerWorldPos;
            PositionEyes(sim.Facing);
        }

        public void TriggerDeath()
        {
            _dying = true;
            _deathTimer = 0f;
        }

        /// <summary>
        /// Updates the visual from the current sim state. Called explicitly by <see cref="GameFlow"/>
        /// right after it steps the sim, so the player never renders a stale (one-frame-behind) pose.
        /// </summary>
        public void Sync(float deltaTime)
        {
            if (_sim == null)
            {
                return;
            }

            if (_dying)
            {
                _deathTimer += deltaTime;
                float t = Mathf.Clamp01(_deathTimer / DeathDuration);
                transform.localScale = Vector3.one * (1f - t);
                _body.color = Color.Lerp(Palette.Player, Color.red, Mathf.Min(1f, t * 1.6f));
                return;
            }

            Vector2 basePos = _sim.PlayerWorldPos;

            float squashX = 1f;
            float squashY = 1f;
            if (_sim.IsMoving)
            {
                float wobble = Mathf.Sin(_sim.MoveProgress * Mathf.PI) * 0.16f;
                bool horizontal = _sim.Facing == Dir.Left || _sim.Facing == Dir.Right;
                if (horizontal)
                {
                    squashX = 1f + wobble;
                    squashY = 1f - wobble;
                }
                else
                {
                    squashY = 1f + wobble;
                    squashX = 1f - wobble;
                }
            }

            Vector2 bumpOffset = Vector2.zero;
            if (_sim.BumpTimer > 0f)
            {
                float bt = _sim.BumpTimer / LevelSim.BumpLockout;
                bumpOffset = DirVector(_sim.Facing) * (0.09f * Mathf.Sin(bt * Mathf.PI));
            }

            transform.position = basePos + bumpOffset;
            transform.localScale = new Vector3(squashX, squashY, 1f);
            PositionEyes(_sim.Facing);
        }

        private void PositionEyes(Dir facing)
        {
            Vector2 forward = DirVector(facing) * EyeForward;
            Vector2 side = new Vector2(-forward.y, forward.x).normalized * EyeSpacing;
            _eyeL.transform.localPosition = (Vector3)(forward + side);
            _eyeR.transform.localPosition = (Vector3)(forward - side);
        }

        private static Vector2 DirVector(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return Vector2.up;
                case Dir.Down: return Vector2.down;
                case Dir.Left: return Vector2.left;
                default: return Vector2.right;
            }
        }

        private static SpriteRenderer CreateChild(string name, Sprite sprite, Color color, int order, Transform parent, float scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }
    }
}
