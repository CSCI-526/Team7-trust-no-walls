using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    // Level elements. All are immutable. Timing functions are pure functions of
    // t = seconds since the attempt started; each periodic element evaluates at t + Phase.
    //
    // Maze invariant (see LevelLayout): the edge of every dynamic edge element (moving wall
    // edges, phase walls, trigger-linked edges, one-way edges) is OPEN in LevelLayout.Maze.
    // Only static walls (visible and invisible) are walls in the maze.

    /// <summary>
    /// A wall that slides like a sliding door between two collinear neighboring edges of the same
    /// orientation, along its own line (see <see cref="ElementPlacer"/>). Cycle: dwell at A,
    /// slide A to B, dwell at B, slide B to A. The blocking edge switches at a slide's midpoint.
    /// </summary>
    public sealed class MovingWall
    {
        public const float Dwell = 3.0f;
        public const float Slide = 0.4f;
        public const float Warn = 0.6f;
        public const float Cycle = 2f * (Dwell + Slide);

        public Edge A { get; }
        public Edge B { get; }
        public float Phase { get; }

        public MovingWall(Edge a, Edge b, float phase)
        {
            A = a;
            B = b;
            Phase = phase;
        }

        /// <summary>True if <paramref name="e"/> is one of this wall's two positions.</summary>
        public bool Uses(Edge e) => e == A || e == B;

        private float Local(float t) => Mathf.Repeat(t + Phase, Cycle);

        /// <summary>The edge this wall blocks at time t.</summary>
        public Edge BlockingEdgeAt(float t)
        {
            float u = Local(t);
            return u < Dwell + Slide / 2f || u >= 2f * Dwell + 1.5f * Slide ? A : B;
        }

        /// <summary>0 while at A, 1 while at B, animating linearly during slides.</summary>
        public float SlideProgressAt(float t)
        {
            float u = Local(t);
            if (u < Dwell)
            {
                return 0f;
            }

            if (u < Dwell + Slide)
            {
                return (u - Dwell) / Slide;
            }

            if (u < 2f * Dwell + Slide)
            {
                return 1f;
            }

            return 1f - (u - (2f * Dwell + Slide)) / Slide;
        }

        /// <summary>True during the last <see cref="Warn"/> seconds of each dwell.</summary>
        public bool IsWarningAt(float t)
        {
            float u = Local(t);
            return (u >= Dwell - Warn && u < Dwell) || (u >= 2f * Dwell + Slide - Warn && u < 2f * Dwell + Slide);
        }
    }

    /// <summary>
    /// A disappearing wall: solid for <see cref="Solid"/> s, then open for <see cref="Open"/> s,
    /// cycling. It flickers (warning) during the last <see cref="Warn"/> s of the open window.
    /// <see cref="OnBaseWall"/> tells whether it replaced a base-maze wall (a temporary shortcut)
    /// or sits on a route passage.
    /// </summary>
    public sealed class PhaseWall
    {
        public const float Solid = 3.0f;
        public const float Open = 2.0f;
        public const float Warn = 0.6f;
        public const float Cycle = Solid + Open;

        /// <summary>Seconds at the start of the open window over which the drawn wall fades out.</summary>
        public const float FadeOut = 0.2f;

        public Edge Edge { get; }
        public float Phase { get; }
        public bool OnBaseWall { get; }

        public PhaseWall(Edge edge, float phase, bool onBaseWall)
        {
            Edge = edge;
            Phase = phase;
            OnBaseWall = onBaseWall;
        }

        private float Local(float t) => Mathf.Repeat(t + Phase, Cycle);

        public bool IsSolidAt(float t) => Local(t) < Solid;

        public bool IsWarningAt(float t) => Local(t) >= Cycle - Warn;

        /// <summary>
        /// Visual opacity: 1 while solid, fades to 0 over the first <see cref="FadeOut"/> s of the
        /// open window, 0 while open, and flickers between 0.15 and 0.55 (every 0.1 s) during the warning.
        /// </summary>
        public float OpacityAt(float t)
        {
            float u = Local(t);
            if (u < Solid)
            {
                return 1f;
            }

            if (u >= Cycle - Warn)
            {
                int tick = Mathf.FloorToInt((u - (Cycle - Warn)) / 0.1f);
                return tick % 2 == 0 ? 0.55f : 0.15f;
            }

            float open = u - Solid;
            return open < FadeOut ? 1f - open / FadeOut : 0f;
        }
    }

    /// <summary>
    /// A gate cell whose bar rotates 90 degrees every <see cref="Period"/> s. The rotation animates
    /// during the last <see cref="Rotate"/> s of each period and the logical orientation switches at
    /// the end of the rotation. A horizontal bar allows entering/leaving through Left/Right only.
    /// </summary>
    public sealed class RotatingGate
    {
        public const float Period = 2.5f;
        public const float Rotate = 0.3f;
        public const float Cycle = 2f * Period;

        public Vector2Int Cell { get; }
        public float Phase { get; }
        public bool StartHorizontal { get; }

        public RotatingGate(Vector2Int cell, float phase, bool startHorizontal)
        {
            Cell = cell;
            Phase = phase;
            StartHorizontal = startHorizontal;
        }

        private float Local(float t) => Mathf.Repeat(t + Phase, Cycle);

        public bool IsHorizontalAt(float t)
        {
            bool secondHalf = Local(t) >= Period;
            return StartHorizontal != secondHalf;
        }

        /// <summary>Bar angle in degrees in [0, 360): 0 is horizontal, 90 vertical; animates while rotating.</summary>
        public float AngleAt(float t)
        {
            float u = Local(t);
            int k = u >= Period ? 1 : 0;
            float within = u - k * Period;
            float angle = (StartHorizontal ? 0f : 90f) + 90f * k;
            if (within >= Period - Rotate)
            {
                angle += 90f * (within - (Period - Rotate)) / Rotate;
            }

            return Mathf.Repeat(angle, 360f);
        }

        /// <summary>True if the gate cell may be entered or left through side <paramref name="side"/> at t.</summary>
        public bool Allows(Dir side, float t)
        {
            bool horizontalSide = side == Dir.Left || side == Dir.Right;
            return horizontalSide == IsHorizontalAt(t);
        }
    }

    /// <summary>
    /// A spiked orb that ping-pongs along <see cref="Path"/> at one cell per <see cref="StepTime"/> s.
    /// It occupies the cell it is leaving for the first half of a step and the next cell for the second half.
    /// </summary>
    public sealed class Patrol
    {
        public const float StepTime = 0.45f;

        public IReadOnlyList<Vector2Int> Path { get; }
        public float Phase { get; }

        public Patrol(IReadOnlyList<Vector2Int> path, float phase)
        {
            if (path == null || path.Count == 0)
            {
                throw new ArgumentException("Patrol path must contain at least one cell.", nameof(path));
            }

            Path = new List<Vector2Int>(path).AsReadOnly();
            Phase = phase;
        }

        /// <summary>Seconds for a full back-and-forth cycle.</summary>
        public float Cycle => Mathf.Max(1, 2 * (Path.Count - 1)) * StepTime;

        private int Steps => 2 * (Path.Count - 1);

        private int IndexForStep(int k)
        {
            int last = Path.Count - 1;
            k %= Steps;
            return k <= last ? k : Steps - k;
        }

        private void StepAt(float t, out int from, out int to, out float frac)
        {
            if (Path.Count == 1)
            {
                from = to = 0;
                frac = 0f;
                return;
            }

            float s = Mathf.Repeat(t + Phase, Cycle) / StepTime;
            int k = Mathf.FloorToInt(s);
            if (k >= Steps)
            {
                k = Steps - 1;
            }

            frac = Mathf.Clamp01(s - k);
            from = IndexForStep(k);
            to = IndexForStep(k + 1);
        }

        /// <summary>The cell the patrol is leaving (or standing on) at t.</summary>
        public Vector2Int CellAt(float t)
        {
            StepAt(t, out int from, out _, out _);
            return Path[from];
        }

        /// <summary>The cell used for collisions: the leaving cell for the first half of a step, then the next.</summary>
        public Vector2Int OccupiedCellAt(float t)
        {
            StepAt(t, out int from, out int to, out float frac);
            return frac < 0.5f ? Path[from] : Path[to];
        }

        /// <summary>Interpolated world position in an n x n maze.</summary>
        public Vector2 PositionAt(float t, int n)
        {
            StepAt(t, out int from, out int to, out float frac);
            return Vector2.Lerp(Edge.CellCenter(Path[from], n), Edge.CellCenter(Path[to], n), frac);
        }
    }

    /// <summary>An interior wall that is not drawn. Walking into it is fatal. It is a wall in the maze.</summary>
    public sealed class InvisibleWall
    {
        public Edge Edge { get; }

        public InvisibleWall(Edge edge)
        {
            Edge = edge;
        }
    }

    /// <summary>A blue tile that reveals invisible walls within Chebyshev distance 3 for 3.0 s.</summary>
    public sealed class MemoryTile
    {
        public const int RevealRadius = 3;
        public const float RevealDuration = 3.0f;

        public Vector2Int Cell { get; }

        public MemoryTile(Vector2Int cell)
        {
            Cell = cell;
        }
    }

    /// <summary>A cracked tile that collapses <see cref="CollapseDelay"/> s after being entered.</summary>
    public sealed class CollapseTile
    {
        public const float CollapseDelay = 0.8f;
        public const float RestoreDelay = 4.0f;

        public Vector2Int Cell { get; }
        public bool Permanent { get; }

        public CollapseTile(Vector2Int cell, bool permanent)
        {
            Cell = cell;
            Permanent = permanent;
        }
    }

    /// <summary>
    /// A pressure plate. Entering it toggles every linked edge. <see cref="Opens"/> start closed,
    /// <see cref="Closes"/> start open. All linked edges are open in the maze; their state lives in the sim.
    /// </summary>
    public sealed class TriggerPlate
    {
        public Vector2Int Plate { get; }
        public IReadOnlyList<Edge> Opens { get; }
        public IReadOnlyList<Edge> Closes { get; }
        public int ColorIndex { get; }

        public TriggerPlate(Vector2Int plate, IReadOnlyList<Edge> opens, IReadOnlyList<Edge> closes, int colorIndex)
        {
            Plate = plate;
            Opens = new List<Edge>(opens ?? Array.Empty<Edge>()).AsReadOnly();
            Closes = new List<Edge>(closes ?? Array.Empty<Edge>()).AsReadOnly();
            ColorIndex = colorIndex;
        }

        /// <summary>Every edge linked to this plate.</summary>
        public IEnumerable<Edge> LinkedEdges()
        {
            foreach (var e in Opens)
            {
                yield return e;
            }

            foreach (var e in Closes)
            {
                yield return e;
            }
        }

        /// <summary>True if <paramref name="e"/> is initially closed (one of <see cref="Opens"/>).</summary>
        public bool StartsClosed(Edge e)
        {
            foreach (var o in Opens)
            {
                if (o == e)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>A pad that sends the player to <see cref="Target"/>.</summary>
    public sealed class Teleporter
    {
        public const float Delay = 0.15f;

        public Vector2Int Pad { get; }
        public Vector2Int Target { get; }

        public Teleporter(Vector2Int pad, Vector2Int target)
        {
            Pad = pad;
            Target = target;
        }
    }

    /// <summary>An open edge that may only be crossed while travelling in direction <see cref="Allowed"/>.</summary>
    public sealed class OneWay
    {
        public Edge Edge { get; }
        public Dir Allowed { get; }

        public OneWay(Edge edge, Dir allowed)
        {
            Edge = edge;
            Allowed = allowed;
        }

        /// <summary>True if a step in direction <paramref name="d"/> across this edge is allowed.</summary>
        public bool AllowsStep(Dir d) => d == Allowed;
    }

    /// <summary>A fake destination. Reaching it sends the player back to Start.</summary>
    public sealed class Decoy
    {
        public Vector2Int Cell { get; }

        public Decoy(Vector2Int cell)
        {
            Cell = cell;
        }
    }
}
