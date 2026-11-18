using System;
using System.Globalization;

namespace MiniLife.WorldStudio.Domain
{
    // Цвет только для отображения. Источником правды не является (раздел 7).
    // Своя структура вместо System.Drawing.Color: Core не зависит от GDI+.
    public struct RgbColor : IEquatable<RgbColor>
    {
        private readonly byte _r;
        private readonly byte _g;
        private readonly byte _b;

        public RgbColor(byte r, byte g, byte b)
        {
            _r = r;
            _g = g;
            _b = b;
        }

        public byte R { get { return _r; } }
        public byte G { get { return _g; } }
        public byte B { get { return _b; } }

        // Всегда верхний регистр: "CC751E". Так же пишет PixelBuilder.
        public string ToHex()
        {
            return _r.ToString("X2", CultureInfo.InvariantCulture) + _g.ToString("X2", CultureInfo.InvariantCulture) +
                   _b.ToString("X2", CultureInfo.InvariantCulture);
        }

        public static bool TryParseHex(string hex, out RgbColor color)
        {
            color = new RgbColor(0, 0, 0);
            if (hex == null || hex.Length != 6) return false;
            for (int i = 0; i < 6; i++)
                if (!Uri.IsHexDigit(hex[i])) return false;
            int value = int.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            color = new RgbColor((byte)(value >> 16), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
            return true;
        }

        public static RgbColor FromHex(string hex)
        {
            RgbColor color;
            if (!TryParseHex(hex, out color)) throw new DomainException("Некорректный цвет: \"" + (hex ?? "null") + "\".");
            return color;
        }

        public bool Equals(RgbColor other) { return _r == other._r && _g == other._g && _b == other._b; }
        public override bool Equals(object obj) { return obj is RgbColor && Equals((RgbColor)obj); }
        public override int GetHashCode() { return (_r << 16) | (_g << 8) | _b; }
        public override string ToString() { return "#" + ToHex(); }
    }
}
