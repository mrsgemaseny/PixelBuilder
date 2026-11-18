using System.Drawing;

namespace PixelBuilder
{
    public class BlockDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Hotkey { get; set; }
        public Color Color { get; set; }
        public Color BorderColor { get; set; }
        public string Symbol { get; set; }
    }
}
