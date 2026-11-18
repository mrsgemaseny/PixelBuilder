using System;
using System.Collections.Generic;
using System.Globalization;

namespace MiniLife.WorldStudio.Serialization.Json
{
    public enum JsonKind { Object, Array, String, Number, Bool, Null }

    // Дерево JSON с сохранением порядка ключей и исходного текста чисел (DECISIONS D-04).
    // Нужно, чтобы неизвестные поля переживали чтение и запись без изменений.
    public abstract class JsonNode
    {
        public abstract JsonKind Kind { get; }

        // Позиция в исходном тексте (1-based); 0 для узлов, созданных в коде.
        public int Line { get; internal set; }
        public int Column { get; internal set; }

        public string Location
        {
            get { return Line > 0 ? "строка " + Line + ", столбец " + Column : "позиция неизвестна"; }
        }

        public abstract bool DeepEquals(JsonNode other);

        public static string KindName(JsonKind kind)
        {
            switch (kind)
            {
                case JsonKind.Object: return "объект";
                case JsonKind.Array: return "массив";
                case JsonKind.String: return "строка";
                case JsonKind.Number: return "число";
                case JsonKind.Bool: return "логическое значение";
                default: return "null";
            }
        }
    }

    public sealed class JsonObject : JsonNode
    {
        private readonly List<KeyValuePair<string, JsonNode>> _members = new List<KeyValuePair<string, JsonNode>>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        public override JsonKind Kind { get { return JsonKind.Object; } }
        public int Count { get { return _members.Count; } }
        public IList<KeyValuePair<string, JsonNode>> Members { get { return _members.AsReadOnly(); } }

        public bool Contains(string key) { return _index.ContainsKey(key); }

        public JsonNode Get(string key)
        {
            int i;
            return _index.TryGetValue(key, out i) ? _members[i].Value : null;
        }

        // Добавление нового ключа; повтор ключа - ошибка вызывающего кода.
        public void Add(string key, JsonNode value)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (value == null) throw new ArgumentNullException("value");
            if (_index.ContainsKey(key)) throw new InvalidOperationException("Повтор ключа \"" + key + "\".");
            _index.Add(key, _members.Count);
            _members.Add(new KeyValuePair<string, JsonNode>(key, value));
        }

        // Замена значения с сохранением позиции ключа; если ключа нет - добавление в конец.
        public void Set(string key, JsonNode value)
        {
            int i;
            if (_index.TryGetValue(key, out i)) _members[i] = new KeyValuePair<string, JsonNode>(key, value);
            else Add(key, value);
        }

        public override bool DeepEquals(JsonNode other)
        {
            var o = other as JsonObject;
            if (o == null || o.Count != Count) return false;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Key != o._members[i].Key) return false;
                if (!_members[i].Value.DeepEquals(o._members[i].Value)) return false;
            }
            return true;
        }
    }

    public sealed class JsonArray : JsonNode
    {
        private readonly List<JsonNode> _items = new List<JsonNode>();

        public override JsonKind Kind { get { return JsonKind.Array; } }
        public int Count { get { return _items.Count; } }
        public IList<JsonNode> Items { get { return _items.AsReadOnly(); } }
        public JsonNode this[int index] { get { return _items[index]; } }

        public void Add(JsonNode value)
        {
            if (value == null) throw new ArgumentNullException("value");
            _items.Add(value);
        }

        public override bool DeepEquals(JsonNode other)
        {
            var a = other as JsonArray;
            if (a == null || a.Count != Count) return false;
            for (int i = 0; i < _items.Count; i++)
                if (!_items[i].DeepEquals(a._items[i])) return false;
            return true;
        }
    }

    public sealed class JsonString : JsonNode
    {
        public JsonString(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            Value = value;
        }

        public string Value { get; private set; }
        public override JsonKind Kind { get { return JsonKind.String; } }

        public override bool DeepEquals(JsonNode other)
        {
            var s = other as JsonString;
            return s != null && string.Equals(s.Value, Value, StringComparison.Ordinal);
        }
    }

    // Число хранится исходным текстом: "2.50" остаётся "2.50", "1e3" остаётся "1e3".
    public sealed class JsonNumber : JsonNode
    {
        public JsonNumber(string raw)
        {
            if (string.IsNullOrEmpty(raw)) throw new ArgumentException("raw");
            Raw = raw;
        }

        public static JsonNumber FromInt(long value)
        {
            return new JsonNumber(value.ToString(CultureInfo.InvariantCulture));
        }

        public string Raw { get; private set; }
        public override JsonKind Kind { get { return JsonKind.Number; } }

        public bool TryGetInt32(out int value)
        {
            return int.TryParse(Raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        public override bool DeepEquals(JsonNode other)
        {
            var n = other as JsonNumber;
            return n != null && n.Raw == Raw;
        }
    }

    public sealed class JsonBool : JsonNode
    {
        public JsonBool(bool value) { Value = value; }
        public bool Value { get; private set; }
        public override JsonKind Kind { get { return JsonKind.Bool; } }

        public override bool DeepEquals(JsonNode other)
        {
            var b = other as JsonBool;
            return b != null && b.Value == Value;
        }
    }

    public sealed class JsonNull : JsonNode
    {
        public override JsonKind Kind { get { return JsonKind.Null; } }
        public override bool DeepEquals(JsonNode other) { return other is JsonNull; }
    }
}
