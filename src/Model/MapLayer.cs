using System.Collections.Generic;
using System.Drawing;

namespace PixelBuilder
{
    public class MapLayer
    {
        public Dictionary<Point, string> Cells = new Dictionary<Point, string>();
        public readonly Stack<Dictionary<Point, KeyValuePair<string, string>>> Undo =
            new Stack<Dictionary<Point, KeyValuePair<string, string>>>();
        public readonly Stack<Dictionary<Point, KeyValuePair<string, string>>> Redo =
            new Stack<Dictionary<Point, KeyValuePair<string, string>>>();
        public readonly Dictionary<Point, KeyValuePair<string, string>> Stroke =
            new Dictionary<Point, KeyValuePair<string, string>>();
    }
}
