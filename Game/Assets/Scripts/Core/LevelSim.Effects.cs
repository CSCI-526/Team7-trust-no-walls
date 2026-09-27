using UnityEngine;

namespace TrustNoWall.Core
{
    // Cell-arrival effects (triggers, memory tiles, completion), mid-crossing crush detection,
    // and proximity warnings. Split out of LevelSim.cs to keep files under ~400 lines.
    public sealed partial class LevelSim
    {
        /// <summary>True if trigger-linked edge <paramref name="e"/> currently allows passage (open non-trigger edges read as open).</summary>
        public bool IsTriggerEdgeOpen(Edge e) => !_triggerOpen.TryGetValue(e, out var open) || open;

        /// <summary>Seconds left that invisible wall <paramref name="edge"/> is revealed by a memory tile (0 when hidden).</summary>
        public float MemoryRevealRemaining(Edge edge)
        {
            if (_memoryExpiry.TryGetValue(edge, out var expiry))
            {
                float remaining = expiry - Time;
                return remaining > 0f ? remaining : 0f;
            }

            return 0f;
        }

        /// <summary>Checked every frame while crossing an edge: dies if it has become blocked before arrival.</summary>
        private void CheckCrush()
        {
            var mw = Layout.MovingWallAt(_moveEdge);
            if (mw != null && mw.BlockingEdgeAt(Time) == _moveEdge)
            {
                Die("Crushed by a moving wall");
                return;
            }

            var pw = Layout.PhaseWallAt(_moveEdge);
            if (pw != null && pw.IsSolidAt(Time))
            {
                Die("Crushed by a disappearing wall");
                return;
            }

            var tp = Layout.TriggerFor(_moveEdge);
            if (tp != null && !IsTriggerEdgeOpen(_moveEdge))
            {
                Die("Crushed by a trigger wall");
            }
        }

        /// <summary>
        /// Runs the effects of fully arriving in <paramref name="cell"/>. Task 4 handles trigger
        /// plates, memory tiles and the destination; Task 5 will add collapsing tiles, teleporters
        /// and decoys here.
        /// </summary>
        private void OnEnteredCell(Vector2Int cell)
        {
            var plate = Layout.TriggerPlateAt(cell);
            if (plate != null)
            {
                ToggleTrigger(plate);
            }

            var memory = Layout.MemoryTileAt(cell);
            if (memory != null)
            {
                RevealMemory(memory);
            }

            if (Status == SimStatus.Playing && cell == Layout.Destination)
            {
                Complete();
            }
        }

        /// <summary>Cell-based hazard checks that don't depend on the player just having arrived (Task 5: patrols, chaser).</summary>
        private void CheckHazards()
        {
            // Task 5 will add patrol and chaser collision checks here.
        }

        private void ToggleTrigger(TriggerPlate plate)
        {
            foreach (var e in plate.Opens)
            {
                _triggerOpen[e] = !_triggerOpen[e];
            }

            foreach (var e in plate.Closes)
            {
                _triggerOpen[e] = !_triggerOpen[e];
            }

            _events.Add(new SimEvent(SimEventKind.TriggerToggled, plate.Plate, string.Empty));
        }

        private void RevealMemory(MemoryTile tile)
        {
            foreach (var iw in Layout.InvisibleWalls)
            {
                if (EdgeDistance(iw.Edge, tile.Cell) <= MemoryTile.RevealRadius)
                {
                    _memoryExpiry[iw.Edge] = Time + MemoryTile.RevealDuration;
                }
            }

            _events.Add(new SimEvent(SimEventKind.MemoryRevealed, tile.Cell, string.Empty));
        }

        private void Complete()
        {
            Status = SimStatus.Complete;
            _events.Add(new SimEvent(SimEventKind.Completed, PlayerCell, string.Empty));
        }

        private void UpdateWarnings()
        {
            foreach (var mw in Layout.MovingWalls)
            {
                UpdateWarningFor(mw, mw.IsWarningAt(Time), mw.BlockingEdgeAt(Time));
            }

            foreach (var pw in Layout.PhaseWalls)
            {
                UpdateWarningFor(pw, pw.IsWarningAt(Time), pw.Edge);
            }
        }

        /// <summary>
        /// Emits at most one Warning event per warning window per element, only once the element's
        /// edge comes close enough. The window isn't latched just because it started while the
        /// player was out of range: if the player walks into range later in the same window, it
        /// still fires exactly once, on the first check that finds it in range.
        /// </summary>
        private void UpdateWarningFor(object element, bool warningNow, Edge edge)
        {
            if (!warningNow)
            {
                _warned[element] = false;
                return;
            }

            bool alreadyWarned = _warned.TryGetValue(element, out var w) && w;
            if (alreadyWarned)
            {
                return;
            }

            if (EdgeDistance(edge, PlayerCell) <= WarningRadius)
            {
                _warned[element] = true;
                _events.Add(new SimEvent(SimEventKind.Warning, edge.A, string.Empty));
            }
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private static int EdgeDistance(Edge e, Vector2Int cell) =>
            Mathf.Min(Chebyshev(e.A, cell), Chebyshev(e.B, cell));

        /// <summary>
        /// Test-only hook: forces a trigger-linked edge's open/closed state directly, bypassing plate
        /// arrival. Exercises the crush-detection code path for trigger walls, which normal play
        /// cannot otherwise reach mid-crossing: only the player can toggle a plate, and doing so
        /// requires arriving at the plate cell, which the level invariant keeps off every linked edge.
        /// </summary>
        internal void DebugSetTriggerEdgeOpenForTest(Edge e, bool open)
        {
            _triggerOpen[e] = open;
        }
    }
}
