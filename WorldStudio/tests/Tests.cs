using System;
using System.Collections.Generic;
using System.IO;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.Json;
using MiniLife.WorldStudio.Serialization.PixelArchitect;

// Самописный раннер, как в PixelBuilder: без NuGet и тестового фреймворка (DECISIONS D-03).
// Запуск: WorldStudio.Tests.exe <папка fixtures> [папка legacy_bases] [папка для экспорта в старый формат]
internal static partial class Tests
{
    private static int _passed;
    private static int _failed;
    private static int _skipped;
    private static string _fixtures;
    private static string _legacyBases;
    private static string _exportDir;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        _fixtures = args.Length > 0 ? args[0] : Path.Combine("tests", "fixtures");
        _legacyBases = args.Length > 1 ? args[1] : null;
        _exportDir = args.Length > 2 ? args[2] : null;

        Run("Domain.GridSpec", DomainGrid);
        Run("Domain.CellCoord", DomainCellCoord);
        Run("Domain.Ids", DomainIds);
        Run("Domain.ObjectTypes", DomainObjectTypes);
        Run("Domain.Relations", DomainRelations);
        Run("Json.Parse", JsonParse);
        Run("Json.Errors", JsonErrors);
        Run("Json.RoundTrip", JsonRoundTrip);
        Run("Pa.Wildlands226", PaWildlands);
        Run("Pa.Political286", PaPolitical);
        Run("Pa.Nature184", PaNature);
        Run("Pa.EmptyMap", PaEmptyMap);
        Run("Pa.AllObjectTypes", PaAllObjectTypes);
        Run("Pa.BrokenFiles", PaBrokenFiles);
        Run("Pa.UnknownFields", PaUnknownFields);
        Run("Pa.MissingLayersWarn", PaMissingLayers);
        Run("Pa.LegacyBases", PaLegacyBases);
        Run("Store.Atomic", StoreAtomic);
        RunWorldTests();

        Console.WriteLine();
        Console.WriteLine("passed=" + _passed + " failed=" + _failed + " skipped=" + _skipped);
        Console.WriteLine(_failed == 0 ? "WORLDSTUDIO_TESTS_PASS" : "WORLDSTUDIO_TESTS_FAIL");
        return _failed == 0 ? 0 : 1;
    }

    // ---------- Domain ----------

    private static void DomainGrid()
    {
        GridSpec g = GridSpec.Default();
        Check(g.Width == 21 && g.Height == 15, "сетка 21 x 15");
        Check(g.ChunksPerAxis == 63, "63 чанка на ось");
        Check(g.LastChunkTiles == 8, "последний чанк 8 тайлов");
        Check(g.WorldTilesX == 21000 && g.WorldTilesY == 15000, "мир 21000 x 15000 тайлов");
        Check(g.Contains(new CellCoord(20, 14)) && !g.Contains(new CellCoord(21, 0)) && !g.Contains(new CellCoord(0, -1)), "границы сетки");
    }

    private static void DomainCellCoord()
    {
        CellCoord c;
        Check(CellCoord.TryParseKey("12,11", out c) && c.X == 12 && c.Y == 11, "разбор 12,11");
        Check(CellCoord.TryParseKey("-1,0", out c) && c.X == -1, "отрицательная координата разбирается");
        foreach (string bad in new[] { "", "1", "1,", ",1", "1,2,3", " 1,2", "1, 2", "+1,2", "a,b", "1.5,2" })
            Check(!CellCoord.TryParseKey(bad, out c), "отказ на ключ \"" + bad + "\"");
        Check(new CellCoord(3, 4).ToKey() == "3,4", "ToKey");
        Check(new CellCoord(3, 4).IsOrthogonalNeighbor(new CellCoord(3, 5)) && !new CellCoord(3, 4).IsOrthogonalNeighbor(new CellCoord(4, 5)),
            "соседство по стороне");
    }

    private static void DomainIds()
    {
        Check(EntityId.IsValid("state.reinhard") && EntityId.IsValid("army.west_1"), "валидные id");
        foreach (string bad in new[] { null, "", "state", "State.x", "state.", ".x", "state.Рейнгард", "state.x-y", "state.x.y" })
            Check(!EntityId.IsValid(bad), "невалидный id " + (bad ?? "null"));

        var world = new World(GridSpec.Default());
        world.AddObject(new WorldObject("obj.v_1", new CellCoord(1, 1), WorldObjectType.Village));
        Throws(delegate { world.AddObject(new WorldObject("obj.v_1", new CellCoord(2, 2), WorldObjectType.Village)); }, "дубликат id");
        Check(world.RemoveObject("obj.v_1") && !world.IsIdLive("obj.v_1") && world.WasIdIssued("obj.v_1"), "удаление id");
        Throws(delegate { world.AddObject(new WorldObject("obj.v_1", new CellCoord(2, 2), WorldObjectType.Village)); }, "повторная выдача удалённого id");
        Check(world.NextId(EntityId.Object, "v") == "obj.v_2", "NextId пропускает удалённый");
        Throws(delegate { world.AddObject(new WorldObject("obj.out", new CellCoord(21, 0), WorldObjectType.Village)); }, "объект вне сетки");
        Throws(delegate { world.GetOrCreateCell(new CellCoord(0, 15)); }, "клетка вне сетки");
        Throws(delegate { new StateEntity("wild.x", "x", StateKind.Kingdom, new RgbColor(0, 0, 0)); }, "id государства с чужим видом");
        Check(new StateEntity("state.valden", "Вальден", StateKind.County, RgbColor.FromHex("CCCC1E")).RulerPlaceholder == "имя",
            "правитель по умолчанию \"имя\"");
    }

    private static void DomainObjectTypes()
    {
        var codes = new[] { "castle", "city", "fortress", "village", "village_2", "village_3", "village_mark", "army_mark" };
        foreach (string code in codes)
        {
            WorldObjectType t;
            Check(WorldObjectTypes.TryParse(code, out t) && WorldObjectTypes.ToCode(t) == code, "тип " + code);
        }
        WorldObjectType none;
        Check(!WorldObjectTypes.TryParse("sea", out none) && !WorldObjectTypes.TryParse("hamlet", out none), "неизвестные типы");
        Check(WorldObjectTypes.VillageMultiplicity(WorldObjectType.Village3) == 3 &&
              WorldObjectTypes.VillageMultiplicity(WorldObjectType.VillageMark) == 0, "кратность деревень, метка не деревня");
        Check(WorldObjectTypes.IsCityLike(WorldObjectType.Fortress) && !WorldObjectTypes.IsCityLike(WorldObjectType.ArmyMark),
            "крепость - город, армия - нет");
        RgbColor c;
        Check(RgbColor.TryParseHex("cc751e", out c) && c.ToHex() == "CC751E" && !RgbColor.TryParseHex("CC751", out c), "цвет hex");
    }

    private static void DomainRelations()
    {
        var m = new RelationsMatrix(new[] { "state.a", "state.b" });
        m.Set(0, 1, -100);
        m.Set(1, 0, 40);
        Check(m.Get(0, 1) == -100 && m.Get(1, 0) == 40 && m.Get(0, 0) == null, "несимметричная матрица");
        Throws(delegate { m.Set(1, 1, 0); }, "диагональ пустая");
        Throws(delegate { new RelationsMatrix(new[] { "state.a", "state.a" }); }, "повтор государства");
    }

    // ---------- JSON ----------

    private static void JsonParse()
    {
        var root = (JsonObject)JsonParser.Parse("\uFEFF{ \"b\": 1, \"a\": [2.50, -0, 1e3, true, false, null], \"s\": \"\\u0414\\n\\\"x\\\"\" }");
        Check(root.Members[0].Key == "b" && root.Members[1].Key == "a", "порядок ключей сохраняется");
        var arr = (JsonArray)root.Get("a");
        Check(((JsonNumber)arr[0]).Raw == "2.50" && ((JsonNumber)arr[1]).Raw == "-0" && ((JsonNumber)arr[2]).Raw == "1e3", "исходный текст чисел");
        Check(((JsonString)root.Get("s")).Value == "Д\n\"x\"", "escape-последовательности");
        Check(arr[5].Kind == JsonKind.Null && ((JsonBool)arr[3]).Value, "литералы");
        Check(root.Get("a").Line == 1 && root.Get("a").Column > 1, "позиция узла");
    }

    private static void JsonErrors()
    {
        var bad = new[]
        {
            "", "   ", "{", "{\"a\":}", "{\"a\":1,}", "[1,]", "{\"a\":1 \"b\":2}", "{\"a\":01}", "{\"a\":1.}", "{\"a\":.5}",
            "{\"a\":\"x}", "{\"a\":\"\\q\"}", "{\"a\":tru}", "{\"a\":1} x", "{a:1}", "{\"a\":\"\t\"}", "{\"a\":1,\"a\":2}"
        };
        foreach (string text in bad)
        {
            bool failed = false;
            try { JsonParser.Parse(text); }
            catch (JsonParseException ex) { failed = ex.Line >= 1 && ex.Message.Length > 0; }
            Check(failed, "отказ JSON: " + text.Replace("\t", "\\t"));
        }
        string deep = new string('[', 200) + new string(']', 200);
        Throws(delegate { JsonParser.Parse(deep); }, "ограничение глубины");
        try { JsonParser.Parse("{\n  \"a\": 1,\n  \"a\": 2\n}"); Check(false, "повтор ключа с позицией"); }
        catch (JsonParseException ex) { Check(ex.Line == 3 && ex.Message.Contains("Повтор ключа"), "повтор ключа, строка 3"); }
    }

    private static void JsonRoundTrip()
    {
        string text = "{\"z\":{\"k\":[1,2,{\"x\":\"Рейнгард\"}],\"e\":{},\"f\":[]},\"a\":\"\\u0001\\t\"}";
        JsonNode first = JsonParser.Parse(text);
        string written = JsonWriter.Write(first, new JsonWriterOptions());
        JsonNode second = JsonParser.Parse(written);
        Check(first.DeepEquals(second), "запись и повторное чтение равны");
        Check(JsonWriter.Write(second, new JsonWriterOptions()) == written, "запись детерминирована");
        string sorted = JsonWriter.Write(first, new JsonWriterOptions { SortKeys = true });
        Check(sorted.IndexOf("\"a\"") < sorted.IndexOf("\"z\""), "SortKeys");
        Check(written.Contains("Рейнгард") && written.Contains("\\u0001"), "кириллица как есть, управляющие экранируются");
    }

    // ---------- pixel_architect_v1 ----------

    private static PixelArchitectDocument ReadFixture(string name)
    {
        string path = Path.Combine(_fixtures, name);
        PixelArchitectReadResult r = PixelArchitectCodec.Read(TextFileStore.ReadAllText(path));
        Check(r.Ok, "чтение " + name + (r.Ok ? "" : ": " + r.Error));
        Check(r.Warnings.Count == 0, "без предупреждений " + name);
        return r.Document;
    }

    // Круговой тест: прочитал, записал, прочитал, сравнил; плюс вторая запись равна первой байт в байт.
    private static PixelArchitectDocument RoundTrip(PixelArchitectDocument doc, string name)
    {
        string written = PixelArchitectCodec.Write(doc);
        PixelArchitectReadResult again = PixelArchitectCodec.Read(written);
        Check(again.Ok, "повторное чтение " + name + (again.Ok ? "" : ": " + again.Error));
        if (!again.Ok) return null;
        List<string> diff = PixelArchitectComparer.Diff(doc, again.Document);
        Check(diff.Count == 0, "круговой тест " + name + " без различий" + (diff.Count == 0 ? "" : ": " + string.Join("; ", diff.ToArray())));
        Check(PixelArchitectCodec.Write(again.Document) == written, "повторная запись " + name + " байт в байт");
        // Проверка независимо от писателя: поля корня в записанном тексте те же и в том же порядке, что в исходном.
        if (doc.SourceFieldOrder.Count > 0)
        {
            var writtenKeys = new List<string>();
            foreach (var member in ((JsonObject)JsonParser.Parse(written)).Members) writtenKeys.Add(member.Key);
            string before = string.Join(",", new List<string>(doc.SourceFieldOrder).ToArray());
            string after = string.Join(",", writtenKeys.ToArray());
            Check(before == after, "поля корня не добавлены и не потеряны " + name + (before == after ? "" : ": " + before + " -> " + after));
        }
        return again.Document;
    }

    private static void PaWildlands()
    {
        PixelArchitectDocument doc = ReadFixture("legacy_wildlands_226.json");
        if (doc == null) return;
        Check(doc.MapKind == "global" && doc.Width == 21 && doc.Height == 15, "глобальная 21 x 15");
        Check(doc.DeclaredTotalBlocks == 226 && doc.CountStandardBlocks() == 226, "226 блоков до записи");
        Check(doc.GetLayer("political").Count == 0 && doc.GetLayer("landscape").Count == 184 && doc.GetLayer("objects").Count == 42, "слои 0/184/42");
        Check(doc.Blocks != null && doc.Blocks.Count == 0, "пустое поле blocks сохранено");
        PixelArchitectDocument back = RoundTrip(doc, "wildlands");
        if (back != null) Check(back.DeclaredTotalBlocks == 226 && back.CountStandardBlocks() == 226, "226 блоков после записи");
    }

    private static void PaPolitical()
    {
        PixelArchitectDocument doc = ReadFixture("legacy_political_286.json");
        if (doc == null) return;
        Check(doc.DeclaredTotalBlocks == 286 && doc.CountStandardBlocks() == 286, "286 блоков до записи");
        Check(doc.Blocks.Count == 184, "blocks = копия landscape (184)");
        Check(doc.GetLayer("landscape").Get(new CellCoord(17, 2)) == "color_CC751E", "значение клетки 17,2");
        PixelArchitectDocument back = RoundTrip(doc, "political");
        if (back != null) Check(back.DeclaredTotalBlocks == 286 && back.CountStandardBlocks() == 286, "286 блоков после записи");
    }

    private static void PaNature()
    {
        PixelArchitectDocument doc = ReadFixture("legacy_nature_184.json");
        if (doc == null) return;
        Check(doc.DeclaredTotalBlocks == 184 && doc.CountStandardBlocks() == 184, "184 блока");
        Check(doc.GetLayer("landscape").Get(new CellCoord(14, 9)) == "stone_wall", "stone_wall на 14,9 сохранён");
        Check(doc.GetRawField("created_at") is JsonString && ((JsonString)doc.GetRawField("created_at")).Value == "2026-11-16T10:27:02",
            "created_at не переписан");
        RoundTrip(doc, "nature");
    }

    private static void PaEmptyMap()
    {
        var doc = new PixelArchitectDocument("global", 21, 15);
        doc.DeclaredTotalBlocks = 0;
        doc.GetOrAddLayer("political");
        doc.GetOrAddLayer("landscape");
        doc.GetOrAddLayer("objects");
        doc.Blocks = new PixelLayer();
        string text = PixelArchitectCodec.Write(doc);
        Check(text.IndexOf("\"format\"") < text.IndexOf("\"map_kind\"") && text.IndexOf("\"layers\"") < text.IndexOf("\"blocks\""),
            "канонический порядок полей у нового документа");
        Check(text.Contains("\"map_size\": [21, 15]") && text.Contains("\r\n"), "стиль PixelBuilder: массив в строку, CRLF");
        PixelArchitectDocument back = RoundTrip(doc, "empty");
        if (back != null) Check(back.CountStandardBlocks() == 0 && back.Layers.Count == 3, "пустая карта, три слоя");
    }

    private static void PaAllObjectTypes()
    {
        var doc = new PixelArchitectDocument("global", 21, 15);
        doc.GetOrAddLayer("political");
        PixelLayer land = doc.GetOrAddLayer("landscape");
        PixelLayer objects = doc.GetOrAddLayer("objects");
        string[] ids = { "settlement_castle", "settlement_city", "settlement_fortress", "settlement_village",
                         "settlement_village_2", "settlement_village_3", "settlement_hamlet" };
        for (int i = 0; i < ids.Length; i++)
        {
            land.Set(new CellCoord(9 + i, 5), "color_51CC51");
            objects.Set(new CellCoord(9 + i, 5), ids[i]);
        }
        land.Set(new CellCoord(14, 9), "stone_wall");
        doc.DeclaredTotalBlocks = doc.CountStandardBlocks();
        PixelArchitectDocument back = RoundTrip(doc, "all_types");
        if (back == null) return;
        for (int i = 0; i < ids.Length; i++)
            Check(back.GetLayer("objects").Get(new CellCoord(9 + i, 5)) == ids[i], "объект " + ids[i]);

        var local = new PixelArchitectDocument("local", 1000, 1000);
        local.GetOrAddLayer("political");
        local.GetOrAddLayer("landscape").Set(new CellCoord(999, 999), "grass");
        local.GetOrAddLayer("objects").Set(new CellCoord(500, 500), "building_house_3x2_h");
        RoundTrip(local, "local_building");
    }

    private static void PaBrokenFiles()
    {
        string ok = "\"format\":\"pixel_architect_v1\",\"map_kind\":\"global\",\"map_size\":[21,15]";
        var cases = new Dictionary<string, string>
        {
            { "обрезанный файл", "{" + ok + ",\"layers\":{\"landscape\":{\"1,1\":\"color_51CC51\"" },
            { "не объект", "[1,2]" },
            { "чужой формат", "{\"format\":\"other\",\"map_kind\":\"global\",\"map_size\":[21,15],\"layers\":{}}" },
            { "нет format", "{\"map_kind\":\"global\",\"map_size\":[21,15],\"layers\":{}}" },
            { "неизвестный map_kind", "{\"format\":\"pixel_architect_v1\",\"map_kind\":\"world\",\"map_size\":[21,15],\"layers\":{}}" },
            { "map_size из одного числа", "{\"format\":\"pixel_architect_v1\",\"map_kind\":\"global\",\"map_size\":[21],\"layers\":{}}" },
            { "map_size дробный", "{\"format\":\"pixel_architect_v1\",\"map_kind\":\"global\",\"map_size\":[21.5,15],\"layers\":{}}" },
            { "нет слоёв", "{" + ok + "}" },
            { "ключ не координата", "{" + ok + ",\"layers\":{\"landscape\":{\"a,b\":\"x\"}}}" },
            { "клетка вне карты", "{" + ok + ",\"layers\":{\"landscape\":{\"21,0\":\"x\"}}}" },
            { "отрицательная клетка", "{" + ok + ",\"layers\":{\"landscape\":{\"-1,0\":\"x\"}}}" },
            { "одна клетка дважды", "{" + ok + ",\"layers\":{\"landscape\":{\"1,1\":\"x\",\"01,1\":\"y\"}}}" },
            { "значение не строка", "{" + ok + ",\"layers\":{\"landscape\":{\"1,1\":5}}}" },
            { "пустое значение", "{" + ok + ",\"layers\":{\"landscape\":{\"1,1\":\"\"}}}" },
            { "слой не объект", "{" + ok + ",\"layers\":{\"landscape\":[]}}" },
            { "total_blocks строкой", "{" + ok + ",\"total_blocks\":\"226\",\"layers\":{}}" },
            { "visibility не bool", "{" + ok + ",\"visibility\":{\"political\":1},\"layers\":{}}" },
            { "старый формат global_map", "{" + ok + ",\"global_map\":{},\"blocks\":{}}" },
        };
        foreach (var c in cases)
        {
            PixelArchitectReadResult r = PixelArchitectCodec.Read(c.Value);
            Check(!r.Ok && !string.IsNullOrEmpty(r.Error) && r.Document == null, "отказ: " + c.Key + (r.Ok ? "" : " -> " + r.Error));
        }
        PixelArchitectReadResult located = PixelArchitectCodec.Read("{" + ok + ",\n\"layers\":{\n\"landscape\":{\n\"1,1\":5}}}");
        Check(!located.Ok && located.Error.Contains("строка 4"), "ошибка указывает строку");
    }

    private static void PaUnknownFields()
    {
        string text =
            "{\r\n" +
            "  \"format\": \"pixel_architect_v1\",\r\n" +
            "  \"custom_before\": {\"a\": [1, 2.50, \"x\", null], \"b\": {}},\r\n" +
            "  \"map_kind\": \"global\",\r\n" +
            "  \"map_size\": [21, 15],\r\n" +
            "  \"total_blocks\": 999,\r\n" +
            "  \"visibility\": {\"political\": true, \"landscape\": false, \"rivers\": true},\r\n" +
            "  \"layers\": {\r\n" +
            "    \"political\": {},\r\n" +
            "    \"rivers\": {\"3,3\": \"river_main\"},\r\n" +
            "    \"landscape\": {\"5,5\": \"color_51CC51\", \"4,4\": \"color_106B10\"}\r\n" +
            "  },\r\n" +
            "  \"buildings\": {\"building_house_2x2_h\": {\"outer\": [4, 4]}},\r\n" +
            "  \"custom_after\": 1e-3\r\n" +
            "}";
        PixelArchitectReadResult r = PixelArchitectCodec.Read(text);
        Check(r.Ok, "чтение файла с неизвестными полями" + (r.Ok ? "" : ": " + r.Error));
        if (!r.Ok) return;
        PixelArchitectDocument back = RoundTrip(r.Document, "unknown_fields");
        if (back == null) return;
        var custom = (JsonObject)back.GetRawField("custom_before");
        Check(((JsonNumber)((JsonArray)custom.Get("a"))[1]).Raw == "2.50", "число 2.50 не превратилось в 2.5");
        Check(((JsonNumber)back.GetRawField("custom_after")).Raw == "1e-3", "число 1e-3 сохранено");
        Check(back.GetLayer("rivers").Get(new CellCoord(3, 3)) == "river_main", "неизвестный слой сохранён");
        Check(back.Layers[1].Key == "rivers", "порядок слоёв сохранён");
        Check(back.GetLayer("landscape").Cells[0].Key == "5,5", "порядок клеток сохранён");
        Check(((JsonObject)back.GetRawField("visibility")).Contains("rivers"), "неизвестный ключ visibility сохранён");
        Check(back.DeclaredTotalBlocks == 999 && back.CountStandardBlocks() == 2, "неверный total_blocks не исправлен молча");
        string written = PixelArchitectCodec.Write(back);
        Check(written.IndexOf("custom_before") < written.IndexOf("map_kind") && written.IndexOf("buildings") < written.IndexOf("custom_after"),
            "неизвестные поля на прежних местах");
    }

    private static void PaMissingLayers()
    {
        // Файл только с blocks (старые локальные базы): запись не должна добавлять "layers".
        PixelArchitectReadResult blocksOnly = PixelArchitectCodec.Read(
            "{\"format\":\"pixel_architect_v1\",\"map_size\":[1000,1000],\"total_blocks\":1,\"blocks\":{\"500,500\":\"dirt\"}}");
        Check(blocksOnly.Ok && !blocksOnly.Document.HasLayersField && blocksOnly.Document.MapKind == null, "файл только с blocks");
        if (blocksOnly.Ok)
        {
            string written = PixelArchitectCodec.Write(blocksOnly.Document);
            Check(!written.Contains("\"layers\"") && !written.Contains("\"map_kind\""), "запись не добавляет layers и map_kind");
            RoundTrip(blocksOnly.Document, "blocks_only");
        }

        PixelArchitectReadResult r = PixelArchitectCodec.Read(
            "{\"format\":\"pixel_architect_v1\",\"map_kind\":\"global\",\"map_size\":[21,15],\"layers\":{\"landscape\":{}}}");
        Check(r.Ok && r.Warnings.Count == 1 && r.Warnings[0].Contains("political"), "предупреждение: нет political");
    }

    private static void PaLegacyBases()
    {
        if (_legacyBases == null || !Directory.Exists(_legacyBases))
        {
            Skip("legacy_bases не передана вторым аргументом");
            return;
        }
        int files = 0, cells = 0;
        foreach (string path in Directory.GetFiles(_legacyBases, "*.json", SearchOption.AllDirectories))
        {
            files++;
            string name = Path.GetFileName(path);
            PixelArchitectReadResult r = PixelArchitectCodec.Read(TextFileStore.ReadAllText(path));
            Check(r.Ok, "база " + name + (r.Ok ? "" : ": " + r.Error));
            if (!r.Ok) continue;
            cells += r.Document.CountAllLayerBlocks() + (r.Document.Blocks != null ? r.Document.Blocks.Count : 0);
            Check(r.Document.DeclaredTotalBlocks == null || r.Document.DeclaredTotalBlocks.Value ==
                  (r.Document.Layers.Count > 0 ? r.Document.CountStandardBlocks() : r.Document.Blocks.Count), "total_blocks совпадает: " + name);
            RoundTrip(r.Document, name);
        }
        Check(files > 0, "найдены базы");
        Console.WriteLine("  legacy_bases: files=" + files + " cells=" + cells);
    }

    private static void StoreAtomic()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ws_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "map.json");
            TextFileStore.WriteAllTextAtomic(path, "первый");
            TextFileStore.WriteAllTextAtomic(path, "второй");
            Check(TextFileStore.ReadAllText(path) == "второй", "перезапись");
            byte[] bytes = File.ReadAllBytes(path);
            Check(bytes.Length > 0 && bytes[0] != 0xEF, "UTF-8 без BOM");
            Check(Directory.GetFiles(dir).Length == 1, "временных файлов не осталось");
            Throws(delegate { TextFileStore.WriteAllTextAtomic(Path.Combine(dir, "nope", "x.json"), "x"); }, "нет папки - ошибка");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ---------- раннер ----------

    private delegate void Action0();

    private static void Run(string name, Action0 test)
    {
        int before = _failed;
        try { test(); }
        catch (Exception ex) { Fail("исключение: " + ex.GetType().Name + ": " + ex.Message); }
        Console.WriteLine((_failed == before ? "OK   " : "FAIL ") + name);
    }

    private static void Check(bool condition, string what)
    {
        if (condition) _passed++;
        else Fail(what);
    }

    private static void Fail(string what)
    {
        _failed++;
        Console.WriteLine("  FAIL: " + what);
    }

    private static void Skip(string why)
    {
        _skipped++;
        Console.WriteLine("  SKIP: " + why);
    }

    private static void Throws(Action0 action, string what)
    {
        try { action(); }
        catch (Exception) { _passed++; return; }
        Fail("ожидалось исключение: " + what);
    }
}
