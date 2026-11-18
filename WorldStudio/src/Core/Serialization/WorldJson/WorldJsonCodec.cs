using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.Json;

namespace MiniLife.WorldStudio.Serialization.WorldJson
{
    public sealed class WorldReadResult
    {
        internal WorldReadResult(World world, string error, int migratedFrom)
        {
            World = world;
            Error = error;
            MigratedFromVersion = migratedFrom;
        }

        public bool Ok { get { return World != null; } }
        public World World { get; private set; }
        public string Error { get; private set; }
        // Версия схемы в файле, если она была старше текущей и файл прошёл миграции; иначе 0.
        public int MigratedFromVersion { get; private set; }
    }

    // world.json - единый экспорт (раздел 7).
    // Запись детерминирована (D-04, Этап 5): ключи отсортированы, клетки по (y, x), объекты по id,
    // LF, без BOM, перевод строки в конце. Одинаковые данные дают байт-в-байт одинаковый файл.
    // Порядок государств, групп и армий - порядок реестра: это данные автора, не сортируется.
    public static class WorldJsonCodec
    {
        public static string Write(World world)
        {
            return JsonWriter.Write(ToJson(world), new JsonWriterOptions { SortKeys = true, NewLine = "\n" });
        }

        public static WorldReadResult Read(string text)
        {
            return Read(text, WorldMigrations.All);
        }

        // Сначала разбор, миграции и проверка, мир возвращается целиком или не возвращается (раздел 9).
        public static WorldReadResult Read(string text, IList<IWorldMigration> migrations)
        {
            try
            {
                var root = JsonRead.Object(JsonParser.Parse(text ?? ""), "world");
                int version = JsonRead.RequiredInt(root, "schema_version", "world");
                int from = 0;
                if (version > World.CurrentSchemaVersion)
                    return Fail("Файл создан более новой версией WorldStudio (схема " + version + ", поддерживается до " +
                                World.CurrentSchemaVersion + "). Обновите инструмент.");
                if (version < World.CurrentSchemaVersion)
                {
                    from = version;
                    WorldMigrations.Upgrade(root, version, migrations);
                }
                return new WorldReadResult(FromJson(root), null, from);
            }
            catch (JsonParseException ex) { return Fail("Файл не является корректным JSON: " + ex.Message); }
            catch (JsonSchemaException ex) { return Fail(ex.Message); }
            catch (DomainException ex) { return Fail(ex.Message); }
        }

        private static WorldReadResult Fail(string error) { return new WorldReadResult(null, error, 0); }

        // ---------- запись ----------

        public static JsonObject ToJson(World w)
        {
            var root = new JsonObject();
            root.Add("schema_version", JsonNumber.FromInt(w.SchemaVersion));
            if (w.GeneratorVersionRaw != null) root.Add("generator_version", JsonParser.Parse(w.GeneratorVersionRaw));

            var meta = new JsonObject();
            meta.Add("name", new JsonString(w.Meta.Name));
            meta.Add("created_at", new JsonString(w.Meta.CreatedAt));
            meta.Add("updated_at", new JsonString(w.Meta.UpdatedAt));
            meta.Add("tool_version", new JsonString(w.Meta.ToolVersion));
            root.Add("meta", meta);

            var grid = new JsonObject();
            grid.Add("width", JsonNumber.FromInt(w.Grid.Width));
            grid.Add("height", JsonNumber.FromInt(w.Grid.Height));
            grid.Add("cell_tiles", JsonNumber.FromInt(w.Grid.CellTiles));
            grid.Add("chunk_tiles", JsonNumber.FromInt(w.Grid.ChunkTiles));
            root.Add("grid", grid);

            root.Add("legend", LegendToJson(w.Legend));

            var entities = new JsonObject();
            var states = new JsonArray();
            foreach (StateEntity s in w.States)
            {
                var o = new JsonObject();
                o.Add("id", new JsonString(s.Id));
                o.Add("name", new JsonString(s.Name));
                o.Add("kind", new JsonString(EntityKinds.ToCode(s.Kind)));
                o.Add("ruler_placeholder", new JsonString(s.RulerPlaceholder));
                o.Add("color", new JsonString(s.Color.ToHex()));
                o.Add("notes", new JsonString(s.Notes));
                states.Add(o);
            }
            entities.Add("states", states);
            var wild = new JsonArray();
            foreach (WildlandEntity g in w.Wildlands)
            {
                var o = new JsonObject();
                o.Add("id", new JsonString(g.Id));
                o.Add("color", new JsonString(g.Color.ToHex()));
                o.Add("notes", new JsonString(g.Notes));
                wild.Add(o);
            }
            entities.Add("wildlands", wild);
            var armies = new JsonArray();
            foreach (ArmyEntity a in w.Armies)
            {
                var o = new JsonObject();
                o.Add("id", new JsonString(a.Id));
                o.Add("status", new JsonString(EntityKinds.ToCode(a.Status)));
                o.Add("origin", CellOrNull(a.Origin));
                var route = new JsonArray();
                foreach (CellCoord c in a.Route) route.Add(CellToJson(c));
                o.Add("route", route);
                o.Add("position", CellOrNull(a.Position));
                o.Add("notes", new JsonString(a.Notes));
                armies.Add(o);
            }
            entities.Add("armies", armies);
            var retired = new JsonArray();
            foreach (string id in w.RetiredIds) retired.Add(new JsonString(id));
            entities.Add("retired_ids", retired);
            root.Add("entities", entities);

            var cells = new JsonArray();
            foreach (CellData c in w.SortedCells())
            {
                var o = new JsonObject();
                o.Add("x", JsonNumber.FromInt(c.Cell.X));
                o.Add("y", JsonNumber.FromInt(c.Cell.Y));
                o.Add("landscape", StringOrNull(c.Landscape));
                o.Add("owner", StringOrNull(c.Owner));
                o.Add("notes", new JsonString(c.Notes));
                cells.Add(o);
            }
            root.Add("cells", cells);

            var objects = new List<WorldObject>(w.Objects);
            objects.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var objs = new JsonArray();
            foreach (WorldObject obj in objects)
            {
                var o = new JsonObject();
                o.Add("id", new JsonString(obj.Id));
                o.Add("cell", CellToJson(obj.Cell));
                o.Add("type", new JsonString(WorldObjectTypes.ToCode(obj.Type)));
                o.Add("owner", StringOrNull(obj.Owner));
                o.Add("notes", new JsonString(obj.Notes));
                objs.Add(o);
            }
            root.Add("objects", objs);

            root.Add("relations", w.Relations == null ? (JsonNode)new JsonNull() : RelationsToJson(w.Relations));
            root.Add("lore", JsonParser.Parse(w.LoreRaw));
            root.Add("buildings", JsonParser.Parse(w.BuildingsRaw));
            foreach (var extra in w.ExtraRaw) root.Add(extra.Key, JsonParser.Parse(extra.Value));
            return root;
        }

        public static JsonObject RelationsToJson(RelationsMatrix m)
        {
            var o = new JsonObject();
            var order = new JsonArray();
            foreach (string id in m.Order) order.Add(new JsonString(id));
            o.Add("order", order);
            var matrix = new JsonArray();
            for (int i = 0; i < m.Size; i++)
            {
                var row = new JsonArray();
                for (int j = 0; j < m.Size; j++)
                {
                    int? v = m.Get(i, j);
                    row.Add(v.HasValue ? (JsonNode)JsonNumber.FromInt(v.Value) : new JsonNull());
                }
                matrix.Add(row);
            }
            o.Add("matrix", matrix);
            return o;
        }

        private static JsonObject LegendToJson(Legend legend)
        {
            var o = new JsonObject();
            var types = new JsonArray();
            foreach (LandscapeType t in legend.LandscapeTypes)
            {
                var to = new JsonObject();
                to.Add("id", new JsonString(t.Id));
                to.Add("name", new JsonString(t.Name));
                to.Add("color", new JsonString(t.Color.ToHex()));
                types.Add(to);
            }
            o.Add("landscape_types", types);
            var legacy = new JsonObject();
            legacy.Add("landscape_values", MapToJson(legend.LegacyLandscapeValues));
            legacy.Add("owner_values", MapToJson(legend.LegacyOwnerValues));
            legacy.Add("political_objects", MapToJson(legend.LegacyPoliticalObjects));
            legacy.Add("wildland_objects", MapToJson(legend.LegacyWildlandObjects));
            legacy.Add("export_landscape", MapToJson(legend.LegacyExportLandscape));
            o.Add("legacy", legacy);
            return o;
        }

        private static JsonObject MapToJson(IDictionary<string, string> map)
        {
            var o = new JsonObject();
            foreach (var pair in map) o.Add(pair.Key, new JsonString(pair.Value));
            return o;
        }

        private static JsonNode StringOrNull(string s) { return s == null ? (JsonNode)new JsonNull() : new JsonString(s); }

        private static JsonNode CellOrNull(CellCoord? c) { return c.HasValue ? (JsonNode)CellToJson(c.Value) : new JsonNull(); }

        private static JsonObject CellToJson(CellCoord c)
        {
            var o = new JsonObject();
            o.Add("x", JsonNumber.FromInt(c.X));
            o.Add("y", JsonNumber.FromInt(c.Y));
            return o;
        }

        // ---------- чтение ----------

        private static readonly string[] KnownRootKeys =
            { "schema_version", "generator_version", "meta", "grid", "legend", "entities", "cells", "objects", "relations", "lore", "buildings" };

        public static World FromJson(JsonObject root)
        {
            var gridNode = JsonRead.RequiredObject(root, "grid", "world");
            var grid = new GridSpec(
                JsonRead.RequiredInt(gridNode, "width", "grid"), JsonRead.RequiredInt(gridNode, "height", "grid"),
                JsonRead.RequiredInt(gridNode, "cell_tiles", "grid"), JsonRead.RequiredInt(gridNode, "chunk_tiles", "grid"));
            var w = new World(grid);
            w.SchemaVersion = World.CurrentSchemaVersion;
            JsonNode gen = root.Get("generator_version");
            if (gen != null) w.GeneratorVersionRaw = JsonWriter.Write(gen, new JsonWriterOptions { SortKeys = true }).TrimEnd();

            JsonNode metaNode = root.Get("meta");
            if (metaNode != null)
            {
                var meta = JsonRead.Object(metaNode, "meta");
                w.Meta.Name = JsonRead.OptionalString(meta, "name", "meta", "");
                w.Meta.CreatedAt = JsonRead.OptionalString(meta, "created_at", "meta", "");
                w.Meta.UpdatedAt = JsonRead.OptionalString(meta, "updated_at", "meta", "");
                w.Meta.ToolVersion = JsonRead.OptionalString(meta, "tool_version", "meta", "");
            }

            JsonNode legendNode = root.Get("legend");
            if (legendNode != null) w.Legend = LegendFromJson(JsonRead.Object(legendNode, "legend"));

            ReadEntities(root, w);
            ReadCells(root, w);
            ReadObjects(root, w);

            JsonNode rel = root.Get("relations");
            if (rel != null && rel.Kind != JsonKind.Null) w.Relations = RelationsFromJson(JsonRead.Object(rel, "relations"), "relations");

            JsonNode lore = root.Get("lore");
            if (lore != null) w.LoreRaw = Raw(lore);
            JsonNode buildings = root.Get("buildings");
            if (buildings != null) w.BuildingsRaw = Raw(buildings);
            foreach (var member in root.Members)
                if (System.Array.IndexOf(KnownRootKeys, member.Key) < 0) w.ExtraRaw[member.Key] = Raw(member.Value);
            return w;
        }

        private static string Raw(JsonNode node)
        {
            return JsonWriter.Write(node, new JsonWriterOptions { SortKeys = true, Indent = "", NewLine = "" });
        }

        private static void ReadEntities(JsonObject root, World w)
        {
            JsonNode node = root.Get("entities");
            if (node == null) return;
            var entities = JsonRead.Object(node, "entities");
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var dups = new List<string>();

            JsonNode statesNode = entities.Get("states");
            if (statesNode != null)
            {
                var arr = JsonRead.Array(statesNode, "entities.states");
                for (int i = 0; i < arr.Count; i++)
                {
                    string p = "entities.states[" + i + "]";
                    var o = JsonRead.Object(arr[i], p);
                    string id = RequireId(o, p, EntityId.State, seen, dups);
                    if (id == null) continue;
                    StateKind kind;
                    string kindCode = JsonRead.RequiredString(o, "kind", p);
                    if (!EntityKinds.TryParseStateKind(kindCode, out kind))
                        throw new JsonSchemaException(p + ".kind: неизвестный тип \"" + kindCode + "\" (kingdom, duchy, county).");
                    var s = new StateEntity(id, JsonRead.RequiredString(o, "name", p), kind, Color(o, p));
                    s.RulerPlaceholder = JsonRead.OptionalString(o, "ruler_placeholder", p, StateEntity.RulerPlaceholderDefault);
                    s.Notes = JsonRead.OptionalString(o, "notes", p, "");
                    w.AddState(s);
                }
            }
            JsonNode wildNode = entities.Get("wildlands");
            if (wildNode != null)
            {
                var arr = JsonRead.Array(wildNode, "entities.wildlands");
                for (int i = 0; i < arr.Count; i++)
                {
                    string p = "entities.wildlands[" + i + "]";
                    var o = JsonRead.Object(arr[i], p);
                    string id = RequireId(o, p, EntityId.Wildland, seen, dups);
                    if (id == null) continue;
                    var g = new WildlandEntity(id, Color(o, p));
                    g.Notes = JsonRead.OptionalString(o, "notes", p, "");
                    w.AddWildland(g);
                }
            }
            JsonNode armiesNode = entities.Get("armies");
            if (armiesNode != null)
            {
                var arr = JsonRead.Array(armiesNode, "entities.armies");
                for (int i = 0; i < arr.Count; i++)
                {
                    string p = "entities.armies[" + i + "]";
                    var o = JsonRead.Object(arr[i], p);
                    string id = RequireId(o, p, EntityId.Army, seen, dups);
                    if (id == null) continue;
                    ArmyStatus status;
                    string code = JsonRead.RequiredString(o, "status", p);
                    if (!EntityKinds.TryParseArmyStatus(code, out status))
                        throw new JsonSchemaException(p + ".status: неизвестный статус \"" + code + "\" (roaming, destroyed).");
                    var a = new ArmyEntity(id, status);
                    a.Origin = NullableCell(o.Get("origin"), p + ".origin");
                    a.Position = NullableCell(o.Get("position"), p + ".position");
                    JsonNode route = o.Get("route");
                    if (route != null)
                    {
                        var ra = JsonRead.Array(route, p + ".route");
                        for (int k = 0; k < ra.Count; k++) a.Route.Add(Cell(ra[k], p + ".route[" + k + "]"));
                    }
                    a.Notes = JsonRead.OptionalString(o, "notes", p, "");
                    w.AddArmy(a);
                }
            }
            JsonNode objectsNode = root.Get("objects");
            if (objectsNode != null)
            {
                // Id объектов тоже участвуют в проверке дубликатов, сами объекты читаются в ReadObjects.
                var arr = JsonRead.Array(objectsNode, "objects");
                for (int i = 0; i < arr.Count; i++)
                    RequireId(JsonRead.Object(arr[i], "objects[" + i + "]"), "objects[" + i + "]", EntityId.Object, seen, dups);
            }
            if (dups.Count > 0)
                throw new JsonSchemaException("Дубликаты id (проверка 12, [CRITICAL]): " + string.Join(", ", dups.ToArray()) +
                                              ". Файл не открыт, исправьте id вручную.");
            JsonNode retired = entities.Get("retired_ids");
            if (retired != null)
            {
                var arr = JsonRead.Array(retired, "entities.retired_ids");
                for (int i = 0; i < arr.Count; i++)
                {
                    string id = JsonRead.String(arr[i], "entities.retired_ids[" + i + "]");
                    if (!EntityId.IsValid(id)) throw new JsonSchemaException("entities.retired_ids[" + i + "]: некорректный id \"" + id + "\".");
                    if (seen.ContainsKey(id))
                        throw new JsonSchemaException("entities.retired_ids: id \"" + id + "\" помечен удалённым, но используется.");
                    w.RestoreRetiredId(id);
                }
            }
        }

        private static string RequireId(JsonObject o, string path, string kind, Dictionary<string, string> seen, List<string> dups)
        {
            string id = JsonRead.RequiredString(o, "id", path);
            if (!EntityId.IsValid(id) || EntityId.KindOf(id) != kind)
                throw new JsonSchemaException(path + ".id: некорректный id \"" + id + "\", ожидался вид \"" + kind + ".имя\".");
            if (seen.ContainsKey(id))
            {
                if (!dups.Contains(id)) dups.Add(id);
                return null;
            }
            seen.Add(id, path);
            return id;
        }

        private static void ReadCells(JsonObject root, World w)
        {
            JsonNode node = root.Get("cells");
            if (node == null) return;
            var arr = JsonRead.Array(node, "cells");
            for (int i = 0; i < arr.Count; i++)
            {
                string p = "cells[" + i + "]";
                var o = JsonRead.Object(arr[i], p);
                var cell = new CellCoord(JsonRead.RequiredInt(o, "x", p), JsonRead.RequiredInt(o, "y", p));
                if (!w.Grid.Contains(cell))
                    throw new JsonSchemaException(p + ": клетка " + cell + " вне сетки " + w.Grid.Width + " x " + w.Grid.Height + " (проверка 13).");
                if (w.IsLand(cell)) throw new JsonSchemaException(p + ": клетка " + cell + " записана дважды.");
                CellData data = w.GetOrCreateCell(cell);
                data.Landscape = JsonRead.NullableString(o, "landscape", p);
                data.Owner = JsonRead.NullableString(o, "owner", p);
                data.Notes = JsonRead.OptionalString(o, "notes", p, "");
            }
        }

        private static void ReadObjects(JsonObject root, World w)
        {
            JsonNode node = root.Get("objects");
            if (node == null) return;
            var arr = JsonRead.Array(node, "objects");
            for (int i = 0; i < arr.Count; i++)
            {
                string p = "objects[" + i + "]";
                var o = JsonRead.Object(arr[i], p);
                WorldObjectType type;
                string code = JsonRead.RequiredString(o, "type", p);
                if (!WorldObjectTypes.TryParse(code, out type))
                    throw new JsonSchemaException(p + ".type: неизвестный тип объекта \"" + code + "\".");
                CellCoord cell = Cell(JsonRead.Required(o, "cell", p), p + ".cell");
                if (!w.Grid.Contains(cell))
                    throw new JsonSchemaException(p + ": объект вне сетки " + w.Grid.Width + " x " + w.Grid.Height + ", клетка " + cell + " (проверка 13).");
                var obj = new WorldObject(JsonRead.RequiredString(o, "id", p), cell, type);
                obj.Owner = JsonRead.NullableString(o, "owner", p);
                obj.Notes = JsonRead.OptionalString(o, "notes", p, "");
                w.AddObject(obj);
            }
        }

        public static RelationsMatrix RelationsFromJson(JsonObject o, string path)
        {
            var orderArr = JsonRead.RequiredArray(o, "order", path);
            var order = new List<string>();
            for (int i = 0; i < orderArr.Count; i++) order.Add(JsonRead.String(orderArr[i], path + ".order[" + i + "]"));
            RelationsMatrix m;
            try { m = new RelationsMatrix(order); }
            catch (DomainException ex) { throw new JsonSchemaException(path + ".order: " + ex.Message); }
            var matrix = JsonRead.RequiredArray(o, "matrix", path);
            if (matrix.Count != order.Count)
                throw new JsonSchemaException(path + ".matrix: строк " + matrix.Count + ", а государств " + order.Count + ".");
            for (int i = 0; i < matrix.Count; i++)
            {
                var row = JsonRead.Array(matrix[i], path + ".matrix[" + i + "]");
                if (row.Count != order.Count)
                    throw new JsonSchemaException(path + ".matrix[" + i + "]: значений " + row.Count + ", а государств " + order.Count + ".");
                for (int j = 0; j < row.Count; j++)
                {
                    int? v = JsonRead.NullableInt(row[j], path + ".matrix[" + i + "][" + j + "]");
                    if (i == j && v.HasValue)
                        throw new JsonSchemaException(path + ".matrix[" + i + "][" + j + "]: диагональ должна быть пустой (проверка 8).");
                    m.Set(i, j, v);
                }
            }
            return m;
        }

        public static Legend LegendFromJson(JsonObject o)
        {
            var legend = new Legend();
            JsonNode typesNode = o.Get("landscape_types");
            if (typesNode != null)
            {
                var arr = JsonRead.Array(typesNode, "legend.landscape_types");
                for (int i = 0; i < arr.Count; i++)
                {
                    string p = "legend.landscape_types[" + i + "]";
                    var t = JsonRead.Object(arr[i], p);
                    legend.LandscapeTypes.Add(new LandscapeType(JsonRead.RequiredString(t, "id", p),
                        JsonRead.OptionalString(t, "name", p, ""), Color(t, p)));
                }
            }
            JsonNode legacyNode = o.Get("legacy");
            if (legacyNode != null)
            {
                var legacy = JsonRead.Object(legacyNode, "legend.legacy");
                ReadMap(legacy, "landscape_values", legend.LegacyLandscapeValues);
                ReadMap(legacy, "owner_values", legend.LegacyOwnerValues);
                ReadMap(legacy, "political_objects", legend.LegacyPoliticalObjects);
                ReadMap(legacy, "wildland_objects", legend.LegacyWildlandObjects);
                ReadMap(legacy, "export_landscape", legend.LegacyExportLandscape);
            }
            return legend;
        }

        private static void ReadMap(JsonObject parent, string key, IDictionary<string, string> target)
        {
            JsonNode node = parent.Get(key);
            if (node == null) return;
            var o = JsonRead.Object(node, "legend.legacy." + key);
            foreach (var m in o.Members) target[m.Key] = JsonRead.String(m.Value, "legend.legacy." + key + "." + m.Key);
        }

        private static RgbColor Color(JsonObject o, string path)
        {
            string hex = JsonRead.RequiredString(o, "color", path);
            RgbColor c;
            if (!RgbColor.TryParseHex(hex, out c)) throw new JsonSchemaException(path + ".color: некорректный цвет \"" + hex + "\" (RRGGBB).");
            return c;
        }

        private static CellCoord Cell(JsonNode node, string path)
        {
            var o = JsonRead.Object(node, path);
            return new CellCoord(JsonRead.RequiredInt(o, "x", path), JsonRead.RequiredInt(o, "y", path));
        }

        private static CellCoord? NullableCell(JsonNode node, string path)
        {
            if (node == null || node.Kind == JsonKind.Null) return null;
            return Cell(node, path);
        }
    }
}
