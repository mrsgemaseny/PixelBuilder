using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Serialization.Json;

namespace MiniLife.WorldStudio.Serialization.PixelArchitect
{
    // Документ pixel_architect_v1 в том виде, в каком он лежит в файле.
    // Разобранные поля: map_kind, map_size, total_blocks, layers, blocks.
    // Всё остальное (created_at, active_layer, visibility, buildings, неизвестные поля) хранится
    // как исходный JSON и пишется обратно без изменений и на прежнем месте.
    public sealed class PixelArchitectDocument
    {
        public const string FormatId = "pixel_architect_v1";
        public const string LayerPolitical = "political";
        public const string LayerLandscape = "landscape";
        public const string LayerObjects = "objects";

        // Поля, которые кодек пишет из модели. Порядок - как у старого PixelBuilder, для новых документов.
        internal static readonly string[] ModelFields = { "format", "map_kind", "map_size", "total_blocks", "layers", "blocks" };
        internal static readonly string[] CanonicalOrder =
            { "format", "map_kind", "map_size", "created_at", "total_blocks", "active_layer", "visibility", "layers", "blocks" };

        private readonly List<KeyValuePair<string, PixelLayer>> _layers = new List<KeyValuePair<string, PixelLayer>>();
        private readonly JsonObject _source;

        public PixelArchitectDocument(string mapKind, int width, int height)
            : this(mapKind, width, height, new JsonObject())
        {
        }

        internal PixelArchitectDocument(string mapKind, int width, int height, JsonObject source)
        {
            // null допустим: в старых локальных базах (до слоёв) поля map_kind нет вовсе.
            if (mapKind != null && mapKind != "global" && mapKind != "local") throw new ArgumentException("mapKind");
            if (width <= 0 || height <= 0) throw new ArgumentException("size");
            MapKind = mapKind;
            Width = width;
            Height = height;
            _source = source;
        }

        // "global", "local" или null (поля нет в файле; старый PixelBuilder тогда открывает файл как локальный).
        public string MapKind { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        // Значение из файла как есть. Не пересчитывается: расхождение показывает валидатор (проверка 4).
        public int? DeclaredTotalBlocks { get; set; }

        // Поле blocks старого формата: копия активного слоя. null, если в файле его нет.
        public PixelLayer Blocks { get; set; }

        public IList<KeyValuePair<string, PixelLayer>> Layers { get { return _layers.AsReadOnly(); } }

        // Есть ли поле layers. Старые локальные базы содержат только blocks; если записать им пустое
        // "layers": {}, старый PixelBuilder перестанет их открывать (требует внутри слой political).
        public bool HasLayersField { get; set; }

        // Исходный корневой объект: источник порядка полей и значений, которые кодек не разбирает.
        internal JsonObject Source { get { return _source; } }

        // Поля корня в том порядке, в каком они были в прочитанном файле (пусто у нового документа).
        public IList<string> SourceFieldOrder
        {
            get
            {
                var keys = new List<string>();
                foreach (var member in _source.Members) keys.Add(member.Key);
                return keys.AsReadOnly();
            }
        }

        public PixelLayer GetLayer(string name)
        {
            foreach (var pair in _layers)
                if (pair.Key == name) return pair.Value;
            return null;
        }

        public PixelLayer GetOrAddLayer(string name)
        {
            PixelLayer layer = GetLayer(name);
            if (layer != null) return layer;
            layer = new PixelLayer();
            _layers.Add(new KeyValuePair<string, PixelLayer>(name, layer));
            HasLayersField = true;
            return layer;
        }

        // Значение поля, которое кодек не разбирает (created_at, visibility, неизвестные поля).
        public JsonNode GetRawField(string name)
        {
            foreach (string model in ModelFields)
                if (model == name) return null;
            return _source.Get(name);
        }

        public void SetRawField(string name, JsonNode value)
        {
            foreach (string model in ModelFields)
                if (model == name) throw new InvalidOperationException("Поле \"" + name + "\" задаётся через модель.");
            _source.Set(name, value);
        }

        // Задаёт порядок полей нового документа. Значения полей модели берутся из модели при записи,
        // поэтому для них в исходнике хранится только место (null-заглушка).
        internal void ReserveFieldOrder(string[] names)
        {
            foreach (string name in names)
                if (!_source.Contains(name)) _source.Add(name, new JsonNull());
        }

        public IEnumerable<string> RawFieldNames
        {
            get
            {
                foreach (var member in _source.Members)
                {
                    bool model = false;
                    foreach (string m in ModelFields) if (m == member.Key) model = true;
                    if (!model) yield return member.Key;
                }
            }
        }

        // Счёт как у старого PixelBuilder (TotalBlockCount): political + landscape + objects.
        public int CountStandardBlocks()
        {
            int total = 0;
            foreach (string name in new[] { LayerPolitical, LayerLandscape, LayerObjects })
            {
                PixelLayer layer = GetLayer(name);
                if (layer != null) total += layer.Count;
            }
            return total;
        }

        // Все записи во всех слоях, включая нестандартные.
        public int CountAllLayerBlocks()
        {
            int total = 0;
            foreach (var pair in _layers) total += pair.Value.Count;
            return total;
        }
    }
}
