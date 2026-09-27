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
        public static readonly Color PitRim = MechanicInfo.Hex(0x22242E);
        public static readonly Color CrackLine = MechanicInfo.Hex(0x0A0B10);

        public static readonly Color[] TriggerColors =
        {
            MechanicInfo.Hex(0xFF8C42),
            MechanicInfo.Hex(0xB084F5),
            MechanicInfo.Hex(0x3DDC84),
        };

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
