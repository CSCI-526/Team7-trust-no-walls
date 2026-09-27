using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>The overall state of a level attempt.</summary>
    public enum SimStatus
    {
        Playing,
        Dead,
        Complete
    }

    /// <summary>Every notable thing that can happen during a single <see cref="LevelSim.Step"/> call.</summary>
    public enum SimEventKind
    {
        Step,
        Bump,
        Died,
        Completed,
        Teleported,
        TriggerToggled,
        MemoryRevealed,
        TileCracked,
        TileCollapsed,
        TileRestored,
        DecoyFound,
        Warning,
        ChaserSpawned
    }

    /// <summary>
    /// One thing that happened this frame. <see cref="Cell"/> is the cell most relevant to the
    /// event (e.g. the player's cell for a bump or death, the plate for a trigger toggle).
    /// <see cref="Text"/> carries the death cause for <see cref="SimEventKind.Died"/> and is empty
    /// for events that need no extra text.
    /// </summary>
    public readonly struct SimEvent
    {
        public SimEventKind Kind { get; }
        public Vector2Int Cell { get; }
        public string Text { get; }

        public SimEvent(SimEventKind kind, Vector2Int cell, string text)
        {
            Kind = kind;
            Cell = cell;
            Text = text ?? string.Empty;
        }
    }
}
