using System;
using System.Globalization;

namespace MiniLife.WorldStudio.Serialization.Json
{
    // Нарушение ожидаемой структуры файла. Сообщение на русском с путём поля и позицией.
    public sealed class JsonSchemaException : Exception
    {
        public JsonSchemaException(string message) : base(message) { }
    }

    // Строгие помощники чтения: каждая ошибка называет путь поля и строку файла.
    public static class JsonRead
    {
        public static JsonObject Object(JsonNode node, string path)
        {
            var o = node as JsonObject;
            if (o == null) throw Error(path, "должен быть объектом", node);
            return o;
        }

        public static JsonArray Array(JsonNode node, string path)
        {
            var a = node as JsonArray;
            if (a == null) throw Error(path, "должен быть массивом", node);
            return a;
        }

        public static JsonNode Required(JsonObject obj, string key, string path)
        {
            JsonNode node = obj.Get(key);
            if (node == null) throw new JsonSchemaException(path + "." + key + ": нет обязательного поля (" + obj.Location + ").");
            return node;
        }

        public static JsonObject RequiredObject(JsonObject obj, string key, string path)
        {
            return Object(Required(obj, key, path), path + "." + key);
        }

        public static JsonArray RequiredArray(JsonObject obj, string key, string path)
        {
            return Array(Required(obj, key, path), path + "." + key);
        }

        public static string String(JsonNode node, string path)
        {
            var s = node as JsonString;
            if (s == null) throw Error(path, "должен быть строкой", node);
            return s.Value;
        }

        public static string RequiredString(JsonObject obj, string key, string path)
        {
            return String(Required(obj, key, path), path + "." + key);
        }

        // Строка или null; отсутствие поля тоже null.
        public static string NullableString(JsonObject obj, string key, string path)
        {
            JsonNode node = obj.Get(key);
            if (node == null || node.Kind == JsonKind.Null) return null;
            return String(node, path + "." + key);
        }

        public static string OptionalString(JsonObject obj, string key, string path, string fallback)
        {
            JsonNode node = obj.Get(key);
            return node == null ? fallback : String(node, path + "." + key);
        }

        public static int Int(JsonNode node, string path)
        {
            var n = node as JsonNumber;
            int value;
            if (n == null || !n.TryGetInt32(out value)) throw Error(path, "должен быть целым числом", node);
            return value;
        }

        public static int? NullableInt(JsonNode node, string path)
        {
            if (node == null || node.Kind == JsonKind.Null) return null;
            return Int(node, path);
        }

        public static int RequiredInt(JsonObject obj, string key, string path)
        {
            return Int(Required(obj, key, path), path + "." + key);
        }

        public static JsonSchemaException Error(string path, string what, JsonNode node)
        {
            string kind = node == null ? "нет значения" : JsonNode.KindName(node.Kind);
            string where = node == null ? "" : ", " + node.Location;
            return new JsonSchemaException(path + ": " + what + " (в файле: " + kind + where + ").");
        }

        public static string IntText(int value) { return value.ToString(CultureInfo.InvariantCulture); }
    }
}
