using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Serialization.PixelArchitect
{
    // Одна запись слоя: клетка, значение и исходный текст ключа.
    // Исходный ключ хранится, чтобы запись не меняла "09,0" на "9,0" молча.
    public struct PixelCell
    {
        private readonly CellCoord _cell;
        private readonly string _key;
        private readonly string _value;

        public PixelCell(CellCoord cell, string key, string value)
        {
            _cell = cell;
            _key = key;
            _value = value;
        }

        public CellCoord Cell { get { return _cell; } }
        public string Key { get { return _key; } }
        public string Value { get { return _value; } }
    }

    // Слой pixel_architect_v1 без семантики: просто клетка -> строковый id блока.
    // Порядок записей сохраняется как в файле.
    public sealed class PixelLayer
    {
        private readonly List<PixelCell> _cells = new List<PixelCell>();
        private readonly Dictionary<CellCoord, int> _index = new Dictionary<CellCoord, int>();

        public int Count { get { return _cells.Count; } }
        public IList<PixelCell> Cells { get { return _cells.AsReadOnly(); } }

        public bool Contains(CellCoord cell) { return _index.ContainsKey(cell); }

        public string Get(CellCoord cell)
        {
            int i;
            return _index.TryGetValue(cell, out i) ? _cells[i].Value : null;
        }

        // false, если клетка уже есть: две записи на одну клетку - битый файл.
        public bool TryAdd(CellCoord cell, string key, string value)
        {
            if (_index.ContainsKey(cell)) return false;
            _index.Add(cell, _cells.Count);
            _cells.Add(new PixelCell(cell, key, value));
            return true;
        }

        public void Set(CellCoord cell, string value)
        {
            int i;
            if (_index.TryGetValue(cell, out i)) _cells[i] = new PixelCell(cell, _cells[i].Key, value);
            else TryAdd(cell, cell.ToKey(), value);
        }
    }
}
