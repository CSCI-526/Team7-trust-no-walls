using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>The exact colors from the spec's Visual style section, as reusable constants.</summary>
    public static class Palette
    {
        public static readonly Color Background = MechanicInfo.Hex(0x0E1016);
        public static readonly Color FloorBase = MechanicInfo.Hex(0x1A1D27);
        public static readonly Color FloorChecker = MechanicInfo.Hex(0x1E2230);
        public static readonly Color WallVisible = MechanicInfo.Hex(0xD8DCE8);

        public static readonly Color Player = MechanicInfo.Hex(0x4DE1FF);
        public static readonly Color StartRing = MechanicInfo.Hex(0x3DDC84);
        public static readonly Color Destination = MechanicInfo.Hex(0xFFC857);
        public static readonly Color Decoy = MechanicInfo.Hex(0xFFC857);

        public static readonly Color MemoryTile = MechanicInfo.Hex(0x4D8BFF);

        public static readonly Color PitFill = MechanicInfo.Hex(0x050608);
        public static readonly Color PitRim = MechanicInfo.Hex(0x000000);
        public static readonly Color CrackLine = MechanicInfo.Hex(0x0A0B10);

        /// <summary>
        /// Trigger plate colors in the order plates use them. <see cref="TriggerPaletteIndices"/> picks the
        /// ones a level uses.
        /// </summary>
        public static readonly Color[] TriggerColors =
        {
            MechanicInfo.Hex(0xB084F5),
            MechanicInfo.Hex(0x3DDC84),
            MechanicInfo.Hex(0xFF8C42),
        };

        /// <summary>
        /// Indices into <see cref="TriggerColors"/> that trigger plates use, in plate order. Prefers
        /// the colors that do not clash with the level's other mechanics (no orange next to moving
        /// walls, no purple next to teleporters); if the level has more plates than that subset
        /// holds, falls back to the full palette so distinct plates keep distinct colors.
        /// </summary>
        public static int[] TriggerPaletteIndices(int plateCount, bool movingWalls, bool teleporters)
        {
            var subset = new System.Collections.Generic.List<int>(3);
            if (!teleporters)
            {
                subset.Add(0);
            }

            subset.Add(1);
            if (!movingWalls)
            {
                subset.Add(2);
            }

            return plateCount > subset.Count ? new[] { 0, 1, 2 } : subset.ToArray();
        }

        /// <summary>The color of <paramref name="plate"/> and its linked walls in <paramref name="layout"/>.</summary>
        public static Color TriggerPlateColor(TriggerPlate plate, LevelLayout layout)
        {
            return TriggerColorFor(plate.ColorIndex, layout);
        }

        private static Color TriggerColorFor(int colorIndex, LevelLayout layout)
        {
            int[] indices = TriggerPaletteIndices(layout.TriggerPlates.Count, layout.MovingWalls.Count > 0, layout.Teleporters.Count > 0);
            return TriggerColors[indices[colorIndex % indices.Length]];
        }

        /// <summary>
        /// The color that represents <paramref name="m"/> in this level's HUD legend and intro card;
        /// for trigger walls it is the first plate's actual color.
        /// </summary>
        public static Color MechanicColor(Mechanic m, LevelLayout layout)
        {
            if (m != Mechanic.TriggerWalls)
            {
                return MechanicInfo.Get(m).Color;
            }

            return layout.TriggerPlates.Count > 0
                ? TriggerPlateColor(layout.TriggerPlates[0], layout)
                : TriggerColorFor(0, layout);
        }

        public static readonly Color TeleporterPad = MechanicInfo.Hex(0xC04DFF);
        public static readonly Color GateBar = MechanicInfo.Hex(0xFFB199);
        public static readonly Color Patrol = MechanicInfo.Hex(0xFF4D5E);
        public static readonly Color Chaser = MechanicInfo.Hex(0x6A3DB8);
        public static readonly Color OneWayArrow = MechanicInfo.Hex(0x7FDBFF);
        public static readonly Color MovingWall = MechanicInfo.Hex(0xFF8C42);
        public static readonly Color DisappearingWall = MechanicInfo.Hex(0x7FDBFF);
        public static readonly Color RevealedInvisibleWall = MechanicInfo.Hex(0xFF3B47);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
