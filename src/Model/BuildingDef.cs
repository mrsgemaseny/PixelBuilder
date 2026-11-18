using System.Drawing;

namespace PixelBuilder
{
    // Multi-cell building. Size is defined OUTSIDE (walls included); interior = free floor cells.
    // Only the anchor (top-left outer cell) is stored in the objects layer.
    public class BuildingDef : BlockDef
    {
        public int InteriorW { get; set; }
        public int InteriorH { get; set; }
        public char Ridge { get; set; }          // 'h' = horizontal ridge, 'v' = vertical ridge
        public string WallBlockId { get; set; }
        public string RotatedId { get; set; }

        public BuildingDef()
        {
            WallBlockId = "wood_wall";
        }

        public int OuterW { get { return InteriorW + 2; } }
        public int OuterH { get { return InteriorH + 2; } }
        public int DoorX { get { return (OuterW - 1) / 2; } }
        public int DoorY { get { return OuterH - 1; } }

        public Rectangle OuterRect(Point anchor)
        {
            return new Rectangle(anchor.X, anchor.Y, OuterW, OuterH);
        }
    }
}
