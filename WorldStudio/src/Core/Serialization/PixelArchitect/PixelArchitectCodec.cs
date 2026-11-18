using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.Json;

namespace MiniLife.WorldStudio.Serialization.PixelArchitect
{
    public sealed class PixelArchitectReadResult
    {
        internal PixelArchitectReadResult(PixelArchitectDocument document, string error, List<string> warnings)
        {
            Document = document;
            Error = error;
            Warnings = warnings.AsReadOnly();
        }

        public bool Ok { get { return Document != null; } }
        public PixelArchitectDocument Document { get; private set; }
        // Причина отказа на русском, с позицией в файле. null при успехе.
        public string Error { get; private set; }
        // Файл прочитан, но старый PixelBuilder может его не открыть.
        public IList<string> Warnings { get; private set; }
    }

    // Чтение и запись pixel_architect_v1 без потерь (Этап 1).
    // Кодек не знает, что значат цвета и id: семантику задаёт импорт (Этап 2, DECISIONS D-05).
    public static class PixelArchitectCodec
    {
        // Сначала разбор и проверка всего файла, документ возвращается только если ошибок нет (раздел 9).
        public static PixelArchitectReadResult Read(string text)
        {
            var warnings = new List<string>();
            try
            {
                return new PixelArchitectReadResult(ReadDocument(text, warnings), null, warnings);
            }
            catch (JsonParseException ex)
            {
                return Fail("Файл не является корректным JSON: " + ex.Message, warnings);
            }
            catch (FormatError ex)
            {
                return Fail(ex.Message, warnings);
            }
        }

        public static string Write(PixelArchitectDocument document)
        {
            return JsonWriter.Write(ToJson(document), JsonWriterOptions.PixelArchitect());
        }

        // Порядок полей: как в исходном файле; поля модели, которых в файле не было, - в каноническом порядке.
        public static JsonObject ToJson(PixelArchitectDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            var root = new JsonObject();
            foreach (var member in document.Source.Members)
            {
                JsonNode value = IsModelField(member.Key) ? ModelValue(document, member.Key) : member.Value;
                if (value != null) root.Add(member.Key, value);
            }
            foreach (string name in PixelArchitectDocument.CanonicalOrder)
            {
                if (root.Contains(name) || !IsModelField(name)) continue;
                JsonNode value = ModelValue(document, name);
                if (value != null) root.Add(name, value);
            }
            return root;
        }

        private static JsonNode ModelValue(PixelArchitectDocument d, string name)
        {
            switch (name)
            {
                case "format": return new JsonString(PixelArchitectDocument.FormatId);
                case "map_kind": return d.MapKind != null ? new JsonString(d.MapKind) : null;
                case "map_size":
                    var size = new JsonArray();
                    size.Add(JsonNumber.FromInt(d.Width));
                    size.Add(JsonNumber.FromInt(d.Height));
                    return size;
                case "total_blocks":
                    return d.DeclaredTotalBlocks.HasValue ? JsonNumber.FromInt(d.DeclaredTotalBlocks.Value) : null;
                case "layers":
                    if (!d.HasLayersField) return null;
                    var layers = new JsonObject();
                    foreach (var pair in d.Layers) layers.Add(pair.Key, LayerToJson(pair.Value));
                    return layers;
                case "blocks":
                    return d.Blocks != null ? LayerToJson(d.Blocks) : null;
                default:
                    return null;
            }
        }

        private static JsonObject LayerToJson(PixelLayer layer)
        {
            var obj = new JsonObject();
            foreach (PixelCell cell in layer.Cells) obj.Add(cell.Key, new JsonString(cell.Value));
            return obj;
        }

        private static bool IsModelField(string name)
        {
            return Array.IndexOf(PixelArchitectDocument.ModelFields, name) >= 0;
        }

        private static PixelArchitectReadResult Fail(string error, List<string> warnings)
        {
            return new PixelArchitectReadResult(null, error, warnings);
        }

        private sealed class FormatError : Exception
        {
            public FormatError(string message) : base(message) { }
        }

        private static PixelArchitectDocument ReadDocument(string text, List<string> warnings)
        {
            if (text == null) throw new FormatError("Файл пуст.");
            var root = JsonParser.Parse(text) as JsonObject;
            if (root == null) throw new FormatError("Корень файла должен быть JSON-объектом.");

            string format = RequireString(root, "format");
            if (format != PixelArchitectDocument.FormatId)
                throw new FormatError("Неизвестный формат \"" + format + "\", ожидался " + PixelArchitectDocument.FormatId + ".");

            if (root.Contains("global_map"))
                // Формат до разделения на слои, со смещением на холсте 1000 x 1000 (DECISIONS D-10).
                throw new FormatError("Файл старого формата с полем global_map не поддерживается. Откройте его в PixelBuilder и сохраните заново.");

            // map_kind необязателен, как и в старом PixelBuilder: в 32 локальных базах его нет.
            CheckOptionalString(root, "map_kind");
            string mapKind = root.Contains("map_kind") ? ((JsonString)root.Get("map_kind")).Value : null;
            if (mapKind != null && mapKind != "global" && mapKind != "local")
                throw new FormatError("map_kind должен быть \"global\" или \"local\", в файле \"" + mapKind + "\".");

            var sizeNode = root.Get("map_size") as JsonArray;
            if (sizeNode == null || sizeNode.Count != 2)
                throw new FormatError("map_size должен быть массивом из двух целых чисел" + Where(root.Get("map_size"), root) + ".");
            int width = RequirePositiveInt(sizeNode[0], "map_size[0]");
            int height = RequirePositiveInt(sizeNode[1], "map_size[1]");

            var document = new PixelArchitectDocument(mapKind, width, height, root);

            JsonNode total = root.Get("total_blocks");
            if (total != null)
            {
                var number = total as JsonNumber;
                int value;
                if (number == null || !number.TryGetInt32(out value) || value < 0)
                    throw new FormatError("total_blocks должен быть неотрицательным целым" + Where(total, root) + ".");
                document.DeclaredTotalBlocks = value;
            }

            CheckOptionalString(root, "created_at");
            CheckOptionalString(root, "active_layer");
            JsonNode visibility = root.Get("visibility");
            if (visibility != null)
            {
                var vis = visibility as JsonObject;
                if (vis == null) throw new FormatError("visibility должен быть объектом" + Where(visibility, root) + ".");
                foreach (var member in vis.Members)
                    if (member.Value.Kind != JsonKind.Bool)
                        throw new FormatError("visibility." + member.Key + " должен быть true или false (" + member.Value.Location + ").");
            }

            JsonNode layersNode = root.Get("layers");
            JsonNode blocksNode = root.Get("blocks");
            if (layersNode == null && blocksNode == null)
                throw new FormatError("В файле нет ни layers, ни blocks: нечего читать.");

            if (layersNode != null)
            {
                var layers = layersNode as JsonObject;
                if (layers == null) throw new FormatError("layers должен быть объектом" + Where(layersNode, root) + ".");
                document.HasLayersField = true;
                foreach (var member in layers.Members)
                    ReadLayer(member.Value, "layers." + member.Key, width, height, document.GetOrAddLayer(member.Key));
            }
            if (blocksNode != null)
            {
                var blocks = new PixelLayer();
                ReadLayer(blocksNode, "blocks", width, height, blocks);
                document.Blocks = blocks;
            }

            // Старый PixelBuilder отказывается открывать файл без этих слоёв (REPORT_00, раздел 5).
            if (layersNode != null)
            {
                if (document.GetLayer(PixelArchitectDocument.LayerPolitical) == null)
                    warnings.Add("Нет слоя political: старый PixelBuilder этот файл не откроет.");
                if (document.GetLayer(PixelArchitectDocument.LayerLandscape) == null)
                    warnings.Add("Нет слоя landscape: старый PixelBuilder этот файл не откроет.");
            }
            return document;
        }

        private static void ReadLayer(JsonNode node, string path, int width, int height, PixelLayer target)
        {
            var obj = node as JsonObject;
            if (obj == null) throw new FormatError(path + " должен быть объектом клеток (" + node.Location + ").");
            foreach (var member in obj.Members)
            {
                CellCoord cell;
                if (!CellCoord.TryParseKey(member.Key, out cell))
                    throw new FormatError(path + ": ключ \"" + member.Key + "\" не является координатой \"x,y\" (" + member.Value.Location + ").");
                if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
                    throw new FormatError(path + ": клетка " + member.Key + " вне карты " + width + " x " + height + " (" + member.Value.Location + ").");
                var value = member.Value as JsonString;
                if (value == null || value.Value.Length == 0)
                    throw new FormatError(path + "." + member.Key + ": значение должно быть непустой строкой (" + member.Value.Location + ").");
                if (!target.TryAdd(cell, member.Key, value.Value))
                    throw new FormatError(path + ": клетка " + cell.ToKey() + " записана дважды (" + member.Value.Location + ").");
            }
        }

        private static string RequireString(JsonObject root, string name)
        {
            JsonNode node = root.Get(name);
            if (node == null) throw new FormatError("Нет обязательного поля " + name + ".");
            var s = node as JsonString;
            if (s == null) throw new FormatError(name + " должен быть строкой (" + node.Location + ").");
            return s.Value;
        }

        private static void CheckOptionalString(JsonObject root, string name)
        {
            JsonNode node = root.Get(name);
            if (node != null && node.Kind != JsonKind.String)
                throw new FormatError(name + " должен быть строкой (" + node.Location + ").");
        }

        private static int RequirePositiveInt(JsonNode node, string name)
        {
            var number = node as JsonNumber;
            int value;
            if (number == null || !number.TryGetInt32(out value) || value <= 0)
                throw new FormatError(name + " должен быть положительным целым (" + node.Location + ").");
            return value;
        }

        private static string Where(JsonNode node, JsonNode fallback)
        {
            JsonNode n = node ?? fallback;
            return " (" + n.Location + ")";
        }
    }
}
