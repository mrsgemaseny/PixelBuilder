using System;
using System.Globalization;

namespace MiniLife.WorldStudio.Domain
{
    // Координата клетки. Текстовая форма "x,y" совпадает с ключами pixel_architect_v1.
    public struct CellCoord : IEquatable<CellCoord>, IComparable<CellCoord>
    {
        private readonly int _x;
        private readonly int _y;

        public CellCoord(int x, int y)
        {
            _x = x;
            _y = y;
        }

        public int X { get { return _x; } }
        public int Y { get { return _y; } }

        public string ToKey()
        {
            return _x.ToString(CultureInfo.InvariantCulture) + "," + _y.ToString(CultureInfo.InvariantCulture);
        }

        // Строгий разбор: два целых через запятую, без пробелов и знака "+".
        // Отрицательные значения допускаются синтаксически, границы проверяет вызывающий код.
        public static bool TryParseKey(string key, out CellCoord cell)
        {
            cell = new CellCoord(0, 0);
            if (string.IsNullOrEmpty(key)) return false;
            int comma = key.IndexOf(',');
            if (comma <= 0 || comma != key.LastIndexOf(',') || comma == key.Length - 1) return false;
            int x, y;
            if (!TryParseStrictInt(key.Substring(0, comma), out x)) return false;
            if (!TryParseStrictInt(key.Substring(comma + 1), out y)) return false;
            cell = new CellCoord(x, y);
            return true;
        }

        private static bool TryParseStrictInt(string text, out int value)
        {
            value = 0;
            if (text.Length == 0) return false;
            int start = text[0] == '-' ? 1 : 0;
            if (start == text.Length) return false;
            for (int i = start; i < text.Length; i++)
                if (text[i] < '0' || text[i] > '9') return false;
            return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        // Соседство по стороне (4-связность). Используется маршрутами армий.
        public bool IsOrthogonalNeighbor(CellCoord other)
        {
            int dx = Math.Abs(_x - other._x);
            int dy = Math.Abs(_y - other._y);
            return dx + dy == 1;
        }

        public bool Equals(CellCoord other) { return _x == other._x && _y == other._y; }
        public override bool Equals(object obj) { return obj is CellCoord && Equals((CellCoord)obj); }
        public override int GetHashCode() { return unchecked(_x * 397) ^ _y; }

        // Порядок: сначала y, затем x (построчно), чтобы вывод совпадал с чтением карты глазами.
        public int CompareTo(CellCoord other)
        {
            if (_y != other._y) return _y.CompareTo(other._y);
            return _x.CompareTo(other._x);
        }

        public static bool operator ==(CellCoord a, CellCoord b) { return a.Equals(b); }
        public static bool operator !=(CellCoord a, CellCoord b) { return !a.Equals(b); }

        public override string ToString() { return "(" + ToKey() + ")"; }
    }
}
