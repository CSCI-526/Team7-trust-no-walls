using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrustNoWall.Core
{
    /// <summary>
    /// Every level mechanic. The enum order is the spec's intro order and is used as the
    /// canonical listing order for <see cref="LevelLayout.Mechanics"/>.
    /// </summary>
    public enum Mechanic
    {
        InvisibleWalls,
        MemoryTiles,
        MovingWalls,
        DisappearingWalls,
        CollapsingTiles,
        TriggerWalls,
        Teleporters,
        RotatingBarriers,
        Patrols,
        OneWayPaths,
        Decoys,
        Chaser
    }

    /// <summary>
    /// Presentation data for a <see cref="Mechanic"/>: display name, intro card explanation,
    /// intro level and HUD legend swatch color.
    /// </summary>
    public sealed class MechanicInfo
    {
        public Mechanic Mechanic { get; }
        public string DisplayName { get; }
        public string Explanation { get; }
        public int IntroLevel { get; }
        public Color Color { get; }

        private MechanicInfo(Mechanic mechanic, string displayName, string explanation, int introLevel, uint rgb)
        {
            Mechanic = mechanic;
            DisplayName = displayName;
            Explanation = explanation;
            IntroLevel = introLevel;
            Color = Hex(rgb);
        }

        /// <summary>Converts a 0xRRGGBB value to an opaque color.</summary>
        public static Color Hex(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        private static readonly MechanicInfo[] Table =
        {
            new MechanicInfo(Mechanic.InvisibleWalls, "Invisible Walls", "Some walls are invisible. Touching one is fatal.", 2, 0xFF4D5E),
            new MechanicInfo(Mechanic.MemoryTiles, "Memory Tiles", "Blue tiles briefly reveal nearby invisible walls.", 2, 0x4D8BFF),
            new MechanicInfo(Mechanic.MovingWalls, "Moving Walls", "Orange walls slide back and forth. Don't get crushed.", 3, 0xFF8C42),
            new MechanicInfo(Mechanic.DisappearingWalls, "Disappearing Walls", "Cyan walls vanish and return. Cross while they're gone.", 3, 0x7FDBFF),
            new MechanicInfo(Mechanic.CollapsingTiles, "Collapsing Tiles", "Cracked tiles collapse soon after you step on them.", 4, 0x8A8F9E),
            new MechanicInfo(Mechanic.TriggerWalls, "Trigger Walls", "Pressure plates open and close the walls of their color.", 4, 0xFF8C42),
            new MechanicInfo(Mechanic.Teleporters, "Teleport Tiles", "Purple pads teleport you. Not always forward.", 5, 0xC04DFF),
            new MechanicInfo(Mechanic.RotatingBarriers, "Rotating Barriers", "Gates rotate. Enter only from the open sides.", 5, 0xFFB199),
            new MechanicInfo(Mechanic.Patrols, "Patrolling Obstacles", "Red spikes patrol side corridors. Time your crossing.", 6, 0xFF4D5E),
            new MechanicInfo(Mechanic.OneWayPaths, "One-Way Paths", "Arrows can only be crossed one way.", 6, 0x7FDBFF),
            new MechanicInfo(Mechanic.Decoys, "Decoy Destination", "Fake goals send you back to the start. The real star spins.", 7, 0xFFC857),
            new MechanicInfo(Mechanic.Chaser, "Chasing Hazard", "A shadow follows your trail. Keep moving.", 8, 0x6A3DB8),
        };

        /// <summary>Info for every mechanic, in enum order.</summary>
        public static IReadOnlyList<MechanicInfo> All => Table;

        /// <summary>Info for one mechanic.</summary>
        public static MechanicInfo Get(Mechanic m)
        {
            int i = (int)m;
            if (i < 0 || i >= Table.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(m), m, null);
            }

            return Table[i];
        }
    }
}
