using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MiniLife.WorldStudio.Serialization.Json
{
    public sealed class JsonWriterOptions
    {
        public JsonWriterOptions()
        {
            Indent = "  ";
            NewLine = "\n";
        }

        public string Indent { get; set; }
        public string NewLine { get; set; }
        // true - ключи объектов по порядковому сравнению (world.json, Этап 5); false - исходный порядок.
        public bool SortKeys { get; set; }
        // Массивы только из скаляров пишутся в одну строку: "map_size": [21, 15].
        public bool InlineScalarArrays { get; set; }

        public static JsonWriterOptions PixelArchitect()
        {
            // Как старый PixelBuilder: CRLF, отступ 2, короткие массивы в строку.
            return new JsonWriterOptions { NewLine = "\r\n", InlineScalarArrays = true };
        }
    }

    // Детерминированный писатель: один и тот же JsonNode всегда даёт одну и ту же строку.
    public static class JsonWriter
    {
        public static string Write(JsonNode node, JsonWriterOptions options)
        {
            if (node == null) throw new ArgumentNullException("node");
            if (options == null) options = new JsonWriterOptions();
            var sb = new StringBuilder();
            WriteNode(sb, node, options, 0);
            sb.Append(options.NewLine);
            return sb.ToString();
        }

        private static void WriteNode(StringBuilder sb, JsonNode node, JsonWriterOptions o, int level)
        {
            switch (node.Kind)
            {
                case JsonKind.Object: WriteObject(sb, (JsonObject)node, o, level); break;
                case JsonKind.Array: WriteArray(sb, (JsonArray)node, o, level); break;
                case JsonKind.String: WriteString(sb, ((JsonString)node).Value); break;
                case JsonKind.Number: sb.Append(((JsonNumber)node).Raw); break;
                case JsonKind.Bool: sb.Append(((JsonBool)node).Value ? "true" : "false"); break;
                default: sb.Append("null"); break;
            }
        }

        private static void WriteObject(StringBuilder sb, JsonObject obj, JsonWriterOptions o, int level)
        {
            if (obj.Count == 0) { sb.Append("{}"); return; }
            var members = new List<KeyValuePair<string, JsonNode>>(obj.Members);
            if (o.SortKeys) members.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            sb.Append('{').Append(o.NewLine);
            for (int i = 0; i < members.Count; i++)
            {
                AppendIndent(sb, o, level + 1);
                WriteString(sb, members[i].Key);
                sb.Append(": ");
                WriteNode(sb, members[i].Value, o, level + 1);
                if (i < members.Count - 1) sb.Append(',');
                sb.Append(o.NewLine);
            }
            AppendIndent(sb, o, level);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, JsonArray arr, JsonWriterOptions o, int level)
        {
            if (arr.Count == 0) { sb.Append("[]"); return; }
            if (o.InlineScalarArrays && AllScalar(arr))
            {
                sb.Append('[');
                for (int i = 0; i < arr.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    WriteNode(sb, arr[i], o, level);
                }
                sb.Append(']');
                return;
            }
            sb.Append('[').Append(o.NewLine);
            for (int i = 0; i < arr.Count; i++)
            {
                AppendIndent(sb, o, level + 1);
                WriteNode(sb, arr[i], o, level + 1);
                if (i < arr.Count - 1) sb.Append(',');
                sb.Append(o.NewLine);
            }
            AppendIndent(sb, o, level);
            sb.Append(']');
        }

        private static bool AllScalar(JsonArray arr)
        {
            foreach (JsonNode item in arr.Items)
                if (item.Kind == JsonKind.Object || item.Kind == JsonKind.Array) return false;
            return true;
        }

        private static void AppendIndent(StringBuilder sb, JsonWriterOptions o, int level)
        {
            for (int i = 0; i < level; i++) sb.Append(o.Indent);
        }

        // Экранируются только обязательные символы; кириллица пишется как есть (файл в UTF-8).
        public static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
