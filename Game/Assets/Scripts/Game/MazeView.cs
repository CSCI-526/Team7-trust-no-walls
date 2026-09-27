using System.Collections.Generic;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Builds and animates every visual for the current level from <see cref="LevelSim"/> state:
    /// floor, walls (skipping invisible ones), pads, and every mechanic. <see cref="Build"/> tears
    /// down the previous level and creates everything fresh; <see cref="Sync"/> is called every
    /// frame to update animated state. Almost everything is driven directly off
    /// <see cref="LevelSim.Time"/> (a pure function of it) so it automatically freezes whenever the
    /// sim isn't stepping (paused, dying, between levels) with no extra bookkeeping.
    /// </summary>
    public sealed class MazeView : MonoBehaviour
    {
        private const int FloorOrder = 0;
        private const int TileOrder = 1;
        private const int PadOrder = 2;
        private const int WallOrder = 5;
        private const int MovingWallTrackOrder = WallOrder - 1;
        private const int EnemyOrder = 8;

        private const float TriggerTween = 0.3f;
        private const float ChaserFadeIn = 0.5f;

        private struct TriggerAnim
        {
            public bool Open;
            public float ChangedAt;
        }

        private sealed class CollapseVisual
        {
            public Vector2Int Cell;
            public Vector2 Center;
            public Transform ShakeRoot;
            public SpriteRenderer Crack;
            public GameObject Pit;
        }

        private sealed class ChaserVisual
        {
            public GameObject Go;
            public SpriteRenderer Body;
            public SpriteRenderer EyeL;
            public SpriteRenderer EyeR;
        }

        private sealed class InvisibleReveal
        {
            public Edge Edge;
            public GameObject Root;
            public SpriteRenderer[] Dashes;
        }

        private sealed class MovingWallVisual
        {
            public MovingWall Wall;
            public Vector2 PosA;
            public Vector2 PosB;
            public Transform T;
            public SpriteRenderer Renderer;
            public SpriteRenderer GhostA;
            public SpriteRenderer GhostB;
            public Transform ChevronT;
            public SpriteRenderer Chevron;
        }

        private Camera _camera;
        private Transform _root;
        private int _n;
        private bool _hasLevel;
        private Vector3 _startWorldPos;
        private GUIStyle _startLabelStyle;
        private LevelSim _lastSim;

        private Transform _destinationStar;
        private SpriteRenderer _destinationGlow;

        private readonly List<(Vector2Int cell, GameObject go)> _decoys = new List<(Vector2Int, GameObject)>();
        private readonly List<CollapseVisual> _collapseVisuals = new List<CollapseVisual>();
        private readonly Dictionary<Edge, TriggerAnim> _triggerAnim = new Dictionary<Edge, TriggerAnim>();
        private readonly List<(Edge edge, SpriteRenderer renderer)> _triggerWalls = new List<(Edge, SpriteRenderer)>();
        private readonly List<Transform> _teleporterSwirls = new List<Transform>();
        private readonly List<(RotatingGate gate, Transform bar)> _gates = new List<(RotatingGate, Transform)>();
        private readonly List<MovingWallVisual> _movingWalls = new List<MovingWallVisual>();
        private readonly List<(PhaseWall wall, SpriteRenderer renderer)> _phaseWalls = new List<(PhaseWall, SpriteRenderer)>();
        private readonly List<(Patrol patrol, Transform t)> _patrols = new List<(Patrol, Transform)>();
        private readonly List<InvisibleReveal> _invisibleReveals = new List<InvisibleReveal>();

        private ChaserVisual _chaser;
        private bool _chaserWasActive;
        private float _chaserFadeStart;

        public void SetCamera(Camera camera)
        {
            _camera = camera;
        }

        /// <summary>Tears down the previous level (if any) and builds every visual for a fresh one.</summary>
        public void Build(LevelSim sim)
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }

            _root = new GameObject("Level").transform;
            _root.SetParent(transform, false);

            _decoys.Clear();
            _collapseVisuals.Clear();
            _triggerAnim.Clear();
            _triggerWalls.Clear();
            _teleporterSwirls.Clear();
            _gates.Clear();
            _movingWalls.Clear();
            _phaseWalls.Clear();
            _patrols.Clear();
            _invisibleReveals.Clear();
            _chaser = null;
            _chaserWasActive = false;

            LevelLayout layout = sim.Layout;
            _n = layout.N;

            BuildFloor(layout);
            BuildWalls(layout);
            BuildStart(layout);
            BuildDestination(layout);
            BuildDecoys(layout);
            BuildMemoryTiles(layout);
            BuildCollapseTiles(layout);
            BuildTriggerPlates(layout, sim);
            BuildTeleporters(layout);
            BuildGates(layout);
            BuildOneWays(layout);
            BuildMovingWalls(layout);
            BuildPhaseWalls(layout);
            BuildPatrols(layout);
            BuildChaser(layout);
            BuildInvisibleReveals(layout);

            _hasLevel = true;
            Sync(sim);
        }

        private void BuildFloor(LevelLayout layout)
        {
            for (int x = 0; x < _n; x++)
            {
                for (int y = 0; y < _n; y++)
                {
                    Color c = ((x + y) % 2 == 0) ? Palette.FloorBase : Palette.FloorChecker;
                    var tile = CreateSprite(SpriteFactory.Square, c, FloorOrder, _root);
                    tile.transform.position = Edge.CellCenter(new Vector2Int(x, y), _n);
                }
            }
        }

        private void BuildWalls(LevelLayout layout)
        {
            foreach (var e in layout.Maze.InteriorWalls())
            {
                if (layout.IsInvisible(e))
                {
                    continue;
                }

                CreateWallSegment(e.WorldCenter(_n), e.IsVertical, Palette.WallVisible, WallOrder, _root);
            }

            for (int x = 0; x < _n; x++)
            {
                for (int y = 0; y < _n; y++)
                {
                    Vector2 center = Edge.CellCenter(new Vector2Int(x, y), _n);
                    if (x == 0)
                    {
                        CreateWallSegment(center + new Vector2(-0.5f, 0f), true, Palette.WallVisible, WallOrder, _root);
                    }

                    if (x == _n - 1)
                    {
                        CreateWallSegment(center + new Vector2(0.5f, 0f), true, Palette.WallVisible, WallOrder, _root);
                    }

                    if (y == 0)
                    {
                        CreateWallSegment(center + new Vector2(0f, -0.5f), false, Palette.WallVisible, WallOrder, _root);
                    }

                    if (y == _n - 1)
                    {
                        CreateWallSegment(center + new Vector2(0f, 0.5f), false, Palette.WallVisible, WallOrder, _root);
                    }
                }
            }
        }

        private void BuildStart(LevelLayout layout)
        {
            var ring = CreateSprite(SpriteFactory.Ring, Palette.StartRing, PadOrder, _root);
            ring.transform.position = Edge.CellCenter(layout.Start, _n);
            ring.transform.localScale = Vector3.one * 0.75f;
            _startWorldPos = ring.transform.position;
        }

        private void BuildDestination(LevelLayout layout)
        {
            GameObject go = new GameObject("Destination");
            go.transform.SetParent(_root, false);
            go.transform.position = Edge.CellCenter(layout.Destination, _n);

            var glow = CreateSprite(SpriteFactory.Glow, Palette.Destination, PadOrder - 1, go.transform);
            glow.transform.localScale = Vector3.one * 1.5f;
            _destinationGlow = glow;

            var star = CreateSprite(SpriteFactory.Star, Palette.Destination, PadOrder, go.transform);
            star.transform.localScale = Vector3.one * 0.8f;
            _destinationStar = star.transform;
        }

        private void BuildDecoys(LevelLayout layout)
        {
            foreach (var d in layout.Decoys)
            {
                GameObject go = new GameObject("Decoy");
                go.transform.SetParent(_root, false);
                go.transform.position = Edge.CellCenter(d.Cell, _n);

                CreateSprite(SpriteFactory.Glow, Palette.Decoy, PadOrder - 1, go.transform).transform.localScale = Vector3.one * 1.3f;
                CreateSprite(SpriteFactory.Star, Palette.Decoy, PadOrder, go.transform).transform.localScale = Vector3.one * 0.8f;

                _decoys.Add((d.Cell, go));
            }
        }

        private void BuildMemoryTiles(LevelLayout layout)
        {
            foreach (var m in layout.MemoryTiles)
            {
                var tile = CreateSprite(SpriteFactory.Square, Palette.WithAlpha(Palette.MemoryTile, 0.85f), TileOrder, _root);
                tile.transform.position = Edge.CellCenter(m.Cell, _n);
                tile.transform.localScale = Vector3.one * 0.86f;
            }
        }

        private void BuildCollapseTiles(LevelLayout layout)
        {
            foreach (var ct in layout.CollapseTiles)
            {
                Vector2 center = Edge.CellCenter(ct.Cell, _n);

                GameObject shakeRoot = new GameObject("CollapseTile");
                shakeRoot.transform.SetParent(_root, false);
                shakeRoot.transform.position = center;
                CreateSprite(SpriteFactory.Square, new Color(0.28f, 0.29f, 0.34f, 1f), TileOrder, shakeRoot.transform).transform.localScale =
                    Vector3.one * 0.94f;
                var crack = CreateSprite(SpriteFactory.CrackOverlay, Palette.CrackLine, TileOrder + 1, shakeRoot.transform);
                crack.transform.localScale = Vector3.one * 0.94f;

                GameObject pit = new GameObject("Pit");
                pit.transform.SetParent(_root, false);
                pit.transform.position = center;
                CreateSprite(SpriteFactory.Square, Palette.PitFill, TileOrder, pit.transform).transform.localScale = Vector3.one * 0.94f;
                CreateSprite(SpriteFactory.Ring, Palette.PitRim, TileOrder + 1, pit.transform).transform.localScale = Vector3.one * 0.98f;
                pit.SetActive(false);

                _collapseVisuals.Add(new CollapseVisual
                {
                    Cell = ct.Cell,
                    Center = center,
                    ShakeRoot = shakeRoot.transform,
                    Crack = crack,
                    Pit = pit,
                });
            }
        }

        private void BuildTriggerPlates(LevelLayout layout, LevelSim sim)
        {
            foreach (var plate in layout.TriggerPlates)
            {
                Color color = Palette.TriggerPlateColor(plate, layout);

                var plateSprite = CreateSprite(SpriteFactory.Plate, color, PadOrder, _root);
                plateSprite.transform.position = Edge.CellCenter(plate.Plate, _n);
                plateSprite.transform.localScale = Vector3.one * 0.82f;

                foreach (var e in plate.LinkedEdges())
                {
                    var wall = CreateWallSegment(e.WorldCenter(_n), e.IsVertical, color, WallOrder, _root);
                    _triggerWalls.Add((e, wall));
                    _triggerAnim[e] = new TriggerAnim { Open = sim.IsTriggerEdgeOpen(e), ChangedAt = -1000f };
                }
            }
        }

        private void BuildTeleporters(LevelLayout layout)
        {
            foreach (var tp in layout.Teleporters)
            {
                var pad = CreateSprite(SpriteFactory.Swirl, Palette.TeleporterPad, PadOrder, _root);
                pad.transform.position = Edge.CellCenter(tp.Pad, _n);
                pad.transform.localScale = Vector3.one * 0.85f;
                _teleporterSwirls.Add(pad.transform);

                var ring = CreateSprite(SpriteFactory.Ring, Palette.TeleporterPad, PadOrder, _root);
                ring.transform.position = Edge.CellCenter(tp.Target, _n);
                ring.transform.localScale = Vector3.one * 0.7f;
            }
        }

        private void BuildGates(LevelLayout layout)
        {
            foreach (var g in layout.RotatingGates)
            {
                Vector2 center = Edge.CellCenter(g.Cell, _n);
                var hub = CreateSprite(SpriteFactory.Circle, Palette.GateBar, PadOrder, _root);
                hub.transform.position = center;
                hub.transform.localScale = Vector3.one * 0.22f;

                var bar = CreateSprite(SpriteFactory.WallPill, Palette.GateBar, WallOrder, _root);
                bar.transform.position = center;
                bar.transform.localScale = new Vector3(0.9f, 1.3f, 1f);
                _gates.Add((g, bar.transform));
            }
        }

        private void BuildOneWays(LevelLayout layout)
        {
            foreach (var ow in layout.OneWays)
            {
                var arrow = CreateSprite(SpriteFactory.ArrowChevron, Palette.OneWayArrow, WallOrder + 1, _root);
                arrow.transform.position = ow.Edge.WorldCenter(_n);
                arrow.transform.localScale = Vector3.one * 0.5f;
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, ArrowAngle(ow.Allowed));
            }
        }

        private void BuildMovingWalls(LevelLayout layout)
        {
            foreach (var mw in layout.MovingWalls)
            {
                Vector2 posA = mw.A.WorldCenter(_n);
                Vector2 posB = mw.B.WorldCenter(_n);
                bool vertical = mw.A.IsVertical;

                // A thin, low-alpha track spanning both slots, so the two positions read as one
                // sliding-door line rather than two unrelated wall edges.
                var track = CreateSprite(SpriteFactory.WallPill, Palette.WithAlpha(Palette.MovingWall, 0.18f), MovingWallTrackOrder, _root);
                track.transform.position = (posA + posB) / 2f;
                track.transform.rotation = Quaternion.Euler(0f, 0f, vertical ? 90f : 0f);
                track.transform.localScale = new Vector3(2f, 0.3f, 1f);

                // Faint ghost outlines at each slot; SyncMovingWalls fades them in/out opposite the
                // wall's own slide progress so the empty slot is always the one showing.
                var ghostA = CreateWallSegment(posA, vertical, Palette.WithAlpha(Palette.MovingWall, 0f), MovingWallTrackOrder, _root);
                var ghostB = CreateWallSegment(posB, vertical, Palette.WithAlpha(Palette.MovingWall, 0f), MovingWallTrackOrder, _root);

                var wall = CreateWallSegment(posA, vertical, Palette.MovingWall, WallOrder, _root);

                var chevron = CreateSprite(SpriteFactory.ArrowChevron, Palette.Background, WallOrder + 1, _root);
                chevron.transform.localScale = Vector3.one * 0.45f;
                chevron.gameObject.SetActive(false);

                _movingWalls.Add(new MovingWallVisual
                {
                    Wall = mw,
                    PosA = posA,
                    PosB = posB,
                    T = wall.transform,
                    Renderer = wall,
                    GhostA = ghostA,
                    GhostB = ghostB,
                    ChevronT = chevron.transform,
                    Chevron = chevron,
                });
            }
        }

        private void BuildPhaseWalls(LevelLayout layout)
        {
            foreach (var pw in layout.PhaseWalls)
            {
                var wall = CreateWallSegment(pw.Edge.WorldCenter(_n), pw.Edge.IsVertical, Palette.DisappearingWall, WallOrder, _root);
                _phaseWalls.Add((pw, wall));
            }
        }

        private void BuildPatrols(LevelLayout layout)
        {
            foreach (var p in layout.Patrols)
            {
                var orb = CreateSprite(SpriteFactory.SpikeOrb, Palette.Patrol, EnemyOrder, _root);
                orb.transform.localScale = Vector3.one * 0.78f;
                _patrols.Add((p, orb.transform));
            }
        }

        private void BuildChaser(LevelLayout layout)
        {
            if (!layout.HasChaser)
            {
                return;
            }

            GameObject go = new GameObject("Chaser");
            go.transform.SetParent(_root, false);
            var body = CreateSprite(SpriteFactory.Blob, Palette.Chaser, EnemyOrder, go.transform);
            var eyeL = CreateSprite(SpriteFactory.Eye, Color.white, EnemyOrder + 1, go.transform);
            eyeL.transform.localScale = Vector3.one * 0.28f;
            eyeL.transform.localPosition = new Vector3(-0.12f, 0.08f, 0f);
            var eyeR = CreateSprite(SpriteFactory.Eye, Color.white, EnemyOrder + 1, go.transform);
            eyeR.transform.localScale = Vector3.one * 0.28f;
            eyeR.transform.localPosition = new Vector3(0.12f, 0.08f, 0f);

            go.SetActive(false);
            _chaser = new ChaserVisual { Go = go, Body = body, EyeL = eyeL, EyeR = eyeR };
        }

        private void BuildInvisibleReveals(LevelLayout layout)
        {
            float[] offsets = { -0.32f, 0f, 0.32f };
            foreach (var iw in layout.InvisibleWalls)
            {
                GameObject root = new GameObject("InvisibleReveal");
                root.transform.SetParent(_root, false);
                root.transform.position = iw.Edge.WorldCenter(_n);
                root.transform.rotation = Quaternion.Euler(0f, 0f, iw.Edge.IsVertical ? 90f : 0f);

                var dashes = new SpriteRenderer[offsets.Length];
                for (int i = 0; i < offsets.Length; i++)
                {
                    GameObject dashGo = new GameObject("Dash");
                    dashGo.transform.SetParent(root.transform, false);
                    dashGo.transform.localPosition = new Vector3(offsets[i], 0f, 0f);
                    dashGo.transform.localScale = new Vector3(0.22f, 1f, 1f);
                    var r = dashGo.AddComponent<SpriteRenderer>();
                    r.sprite = SpriteFactory.WallPill;
                    r.color = Palette.RevealedInvisibleWall;
                    r.sortingOrder = WallOrder + 1;
                    dashes[i] = r;
                }

                root.SetActive(false);
                _invisibleReveals.Add(new InvisibleReveal { Edge = iw.Edge, Root = root, Dashes = dashes });
            }
        }

        /// <summary>Updates every animated visual from the current sim state. Safe to call every frame.</summary>
        public void Sync(LevelSim sim)
        {
            if (!_hasLevel)
            {
                return;
            }

            _lastSim = sim;
            float t = sim.Time;

            _destinationStar.localRotation = Quaternion.Euler(0f, 0f, t * 20f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2f);
            float revealBoost = sim.RealDestinationRevealRemaining > 0f ? 1f : 0f;
            float fastPulse = 0.5f + 0.5f * Mathf.Sin(t * 10f);
            _destinationGlow.color = Palette.WithAlpha(Palette.Destination, 0.55f + 0.25f * pulse + 0.2f * revealBoost * fastPulse);
            _destinationGlow.transform.localScale = Vector3.one * (1.6f + (revealBoost * 0.35f * fastPulse));

            foreach (var (cell, go) in _decoys)
            {
                go.SetActive(sim.IsDecoyActive(cell));
            }

            SyncCollapseTiles(sim, t);
            SyncTriggerWalls(sim, t);

            foreach (var swirl in _teleporterSwirls)
            {
                swirl.localRotation = Quaternion.Euler(0f, 0f, t * -60f);
            }

            foreach (var (gate, bar) in _gates)
            {
                bar.eulerAngles = new Vector3(0f, 0f, gate.AngleAt(t));
            }

            SyncMovingWalls(sim, t);
            SyncPhaseWalls(t);

            foreach (var (patrol, tr) in _patrols)
            {
                tr.position = patrol.PositionAt(t, _n);
                tr.eulerAngles = new Vector3(0f, 0f, t * 90f);
            }

            SyncChaser(sim, t);
            SyncInvisibleReveals(sim);
        }

        private void SyncCollapseTiles(LevelSim sim, float t)
        {
            foreach (var cv in _collapseVisuals)
            {
                TileState state = sim.TileStateAt(cv.Cell);
                bool collapsed = state == TileState.Collapsed;
                cv.ShakeRoot.gameObject.SetActive(!collapsed);
                cv.Pit.SetActive(collapsed);
                if (collapsed)
                {
                    continue;
                }

                bool cracking = state == TileState.Cracking;
                float progress = cracking ? sim.TileCrackProgress(cv.Cell) : 0f;
                float crackAlpha = cracking ? Mathf.Lerp(0.45f, 1f, progress) : 0.45f;
                cv.Crack.color = Palette.WithAlpha(Palette.CrackLine, crackAlpha);

                Vector2 shake = Vector2.zero;
                if (cracking)
                {
                    float amp = 0.03f * progress;
                    shake = new Vector2(Mathf.Sin(t * 47f) * amp, Mathf.Cos(t * 61f) * amp);
                }

                cv.ShakeRoot.position = cv.Center + shake;
            }
        }

        private void SyncTriggerWalls(LevelSim sim, float t)
        {
            foreach (var (edge, renderer) in _triggerWalls)
            {
                bool open = sim.IsTriggerEdgeOpen(edge);
                TriggerAnim anim = _triggerAnim[edge];
                if (anim.Open != open)
                {
                    anim = new TriggerAnim { Open = open, ChangedAt = t };
                    _triggerAnim[edge] = anim;
                }

                float progress = Mathf.Clamp01((t - anim.ChangedAt) / TriggerTween);
                float from = open ? 1f : 0f;
                float to = open ? 0f : 1f;
                float scale = Mathf.Lerp(from, to, progress);
                renderer.transform.localScale = new Vector3(scale, 1f, 1f);
            }
        }

        private void SyncMovingWalls(LevelSim sim, float t)
        {
            foreach (var v in _movingWalls)
            {
                float progress = v.Wall.SlideProgressAt(t);
                v.T.position = Vector2.Lerp(v.PosA, v.PosB, progress);

                bool warn = v.Wall.IsWarningAt(t);
                v.Renderer.color = warn
                    ? Color.Lerp(Palette.MovingWall, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 30f))
                    : Palette.MovingWall;

                // The empty slot's ghost brightens as the wall slides away from it.
                v.GhostA.color = Palette.WithAlpha(Palette.MovingWall, 0.25f * progress);
                v.GhostB.color = Palette.WithAlpha(Palette.MovingWall, 0.25f * (1f - progress));

                // During the warning flash, show a chevron on the wall pointing where it will slide.
                v.Chevron.gameObject.SetActive(warn);
                if (warn)
                {
                    bool towardB = progress < 0.5f;
                    Vector2 dir = towardB ? (v.PosB - v.PosA) : (v.PosA - v.PosB);
                    v.ChevronT.position = v.T.position;
                    v.ChevronT.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                }
            }
        }

        private void SyncPhaseWalls(float t)
        {
            foreach (var (wall, renderer) in _phaseWalls)
            {
                float opacity = wall.OpacityAt(t);
                renderer.gameObject.SetActive(opacity > 0.001f);
                renderer.color = Palette.WithAlpha(Palette.DisappearingWall, opacity);
            }
        }

        private void SyncChaser(LevelSim sim, float t)
        {
            if (_chaser == null)
            {
                return;
            }

            bool active = sim.ChaserActive;
            if (active && !_chaserWasActive)
            {
                _chaserFadeStart = t;
            }

            _chaserWasActive = active;
            _chaser.Go.SetActive(active);
            if (!active)
            {
                return;
            }

            _chaser.Go.transform.position = sim.ChaserWorldPos;
            float fade = Mathf.Clamp01((t - _chaserFadeStart) / ChaserFadeIn);
            _chaser.Body.color = Palette.WithAlpha(Palette.Chaser, 0.3f + 0.5f * fade);
            _chaser.EyeL.color = Palette.WithAlpha(Color.white, fade);
            _chaser.EyeR.color = Palette.WithAlpha(Color.white, fade);
        }

        private void SyncInvisibleReveals(LevelSim sim)
        {
            foreach (var reveal in _invisibleReveals)
            {
                float remaining = sim.MemoryRevealRemaining(reveal.Edge);
                bool active = remaining > 0f;
                reveal.Root.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float alpha = remaining < 0.5f ? remaining / 0.5f : 1f;
                Color c = Palette.WithAlpha(Palette.RevealedInvisibleWall, alpha);
                foreach (var d in reveal.Dashes)
                {
                    d.color = c;
                }
            }
        }

        private void OnGUI()
        {
            if (!_hasLevel || _camera == null)
            {
                return;
            }

            // Skip the label while the player stands on (or very near) Start so it doesn't read as
            // clutter overlapping the player sprite.
            if (_lastSim != null && Vector2.Distance(_lastSim.PlayerWorldPos, _startWorldPos) < 0.4f)
            {
                return;
            }

            GUI.matrix = Matrix4x4.identity;

            if (_startLabelStyle == null)
            {
                _startLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _startLabelStyle.normal.textColor = Palette.StartRing;
            }

            Vector3 screen = _camera.WorldToScreenPoint(_startWorldPos);
            float guiY = Screen.height - screen.y;
            GUI.Label(new Rect(screen.x - 14f, guiY - 14f, 28f, 28f), "S", _startLabelStyle);
        }

        private static float ArrowAngle(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return 90f;
                case Dir.Left: return 180f;
                case Dir.Down: return -90f;
                default: return 0f;
            }
        }

        private static SpriteRenderer CreateSprite(Sprite sprite, Color color, int order, Transform parent)
        {
            GameObject go = new GameObject("Sprite");
            go.transform.SetParent(parent, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        private static SpriteRenderer CreateWallSegment(Vector2 worldCenter, bool vertical, Color color, int order, Transform parent)
        {
            var r = CreateSprite(SpriteFactory.WallPill, color, order, parent);
            r.transform.position = worldCenter;
            r.transform.rotation = Quaternion.Euler(0f, 0f, vertical ? 90f : 0f);
            return r;
        }
    }
}
