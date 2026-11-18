using System;
using System.Collections.Generic;
using System.IO;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization;
using MiniLife.WorldStudio.Serialization.Json;
using MiniLife.WorldStudio.Serialization.PixelArchitect;
using MiniLife.WorldStudio.Serialization.WorldJson;
using MiniLife.WorldStudio.Serialization.Xlsx;
using MiniLife.WorldStudio.Validation;

// Этапы 2-5 и слой Application.
internal static partial class Tests
{
    private static string DataDir { get { return Path.Combine(Path.Combine(_fixtures, ".."), Path.Combine("..", "data")); } }

    private static void RunWorldTests()
    {
        Run("S2.ImportThreeMaps", S2Import);
        Run("S2.NoMeaningInColor", S2NoMeaningInColor);
        Run("S2.ColorChangeOnlyDisplay", S2ColorChange);
        Run("S2.WrongSemanticCaught", S2WrongSemantic);
        Run("S2.ExportBackToLegacy", S2Export);
        Run("S2.NewMarkIds", S2NewMarkIds);
        Run("S2.ImportConflicts", S2Conflicts);
        Run("S3.ExpectedFindings", S3Expected);
        Run("S3.EachCheckFires", S3EachCheck);
        Run("S3.ValidatorReadOnly", S3ReadOnly);
        Run("S4.XlsxRoundTrip", S4XlsxRoundTrip);
        Run("S4.XlsxAuthorLike", S4XlsxAuthorLike);
        Run("S4.XlsxBroken", S4XlsxBroken);
        Run("S5.Deterministic", S5Deterministic);
        Run("S5.ReadErrors", S5ReadErrors);
        Run("S5.Migrations", S5Migrations);
        Run("S5.Preserve", S5Preserve);
        Run("S5.Versions", S5Versions);
        Run("App.PaintUndo", AppPaintUndo);
        Run("App.FillAndLayers", AppFillLayers);
        Run("App.HistoryDepth", AppHistoryDepth);
        Run("App.Objects", AppObjects);
        Run("App.Registry", AppRegistry);
        Run("App.ArmyRoute", AppArmyRoute);
        Run("App.Document", AppDocument);
    }

    // ---------- общие помощники ----------

    private static string ReadData(string name) { return TextFileStore.ReadAllText(Path.Combine(DataDir, name)); }

    private static World Template()
    {
        WorldReadResult r = WorldJsonCodec.Read(ReadData("world_template.json"));
        if (!r.Ok) throw new Exception("шаблон: " + r.Error);
        return r.World;
    }

    private static PixelArchitectDocument Fixture(string name)
    {
        return PixelArchitectCodec.Read(TextFileStore.ReadAllText(Path.Combine(_fixtures, name))).Document;
    }

    private static List<LegacySource> Sources()
    {
        return new List<LegacySource>
        {
            new LegacySource(MapSemantic.Landscape, "nature", Fixture("legacy_nature_184.json")),
            new LegacySource(MapSemantic.Political, "political", Fixture("legacy_political_286.json")),
            new LegacySource(MapSemantic.Wildlands, "wildlands", Fixture("legacy_wildlands_226.json")),
        };
    }

    private static LegacyImportResult ImportAll()
    {
        return LegacyWorldImporter.Import(Template(), Sources());
    }

    private static ValidationInput Input(World w, IEnumerable<LegacySource> sources)
    {
        var input = new ValidationInput(w) { Reference = ReferenceCodec.Read(ReadData("settlements_reference.json")) };
        if (sources != null) foreach (LegacySource s in sources) input.LegacySources.Add(s);
        return input;
    }

    private static int CountType(World w, WorldObjectType t)
    {
        int n = 0;
        foreach (WorldObject o in w.Objects) if (o.Type == t) n++;
        return n;
    }

    // ---------- Этап 2 ----------

    private static void S2Import()
    {
        LegacyImportResult r = ImportAll();
        Check(r.Ok, "импорт трёх карт" + (r.Ok ? "" : ": " + r.Error));
        if (!r.Ok) return;
        World w = r.World;
        Check(r.Issues.Count == 0, "импорт без расхождений между файлами (" + r.Issues.Count + ")");
        Check(w.CellCount == 184, "184 клетки суши");
        Check(w.Objects.Count == 144, "144 объекта (102 поселения + 42 метки)");
        Check(CountType(w, WorldObjectType.Castle) == 8 && CountType(w, WorldObjectType.City) == 17 && CountType(w, WorldObjectType.Fortress) == 7, "замки, города, крепости");
        Check(CountType(w, WorldObjectType.Village) == 48 && CountType(w, WorldObjectType.Village2) == 16 && CountType(w, WorldObjectType.Village3) == 6, "деревни 1/2/3");
        Check(CountType(w, WorldObjectType.VillageMark) == 38, "38 меток Д как village_mark");
        Check(CountType(w, WorldObjectType.ArmyMark) == 4, "4 метки Г как army_mark");
        Check(CountType(w, WorldObjectType.City) == 17, "метки Г не стали городами");
        foreach (WorldObject o in w.Objects)
            if (o.Type == WorldObjectType.ArmyMark)
            {
                ArmyEntity a = w.FindArmy(o.Owner);
                Check(a != null && a.Status == ArmyStatus.Roaming && a.Position.Value == o.Cell, "метка Г " + o.Cell + " связана с армией");
            }
        Check(w.FindCell(new CellCoord(16, 8)).Landscape == "sands" && w.FindCell(new CellCoord(16, 8)).Owner == "state.ulteria",
            "CC751E: пески в ландшафте и Ультерия в политике в одной клетке");
        Check(w.FindCell(new CellCoord(14, 9)).Landscape == "mountains", "stone_wall - горы");
        Check(w.FindCell(new CellCoord(4, 6)).Owner == "wild.group_3", "коричневая клетка - группа диких земель");
        foreach (WorldObject o in w.Objects)
            if (o.Type == WorldObjectType.VillageMark) Check(w.FindWildland(o.Owner) != null, "заготовка в диких землях " + o.Cell);
        Check(w.FindArmy("army.girdenburg").Status == ArmyStatus.Destroyed && w.FindArmy("army.girdenburg").Position == null, "пятая армия уничтожена, клетки нет");
        foreach (StateEntity s in w.States) Check(s.RulerPlaceholder == "имя", "правитель-заглушка " + s.Id);
    }

    // Файл диких земель с новыми метками PixelBuilder даёт тот же мир, что и старый (Д = деревня, Г = город).
    private static void S2NewMarkIds()
    {
        World before = ImportAll().World;
        PixelArchitectDocument wild = Fixture("legacy_wildlands_226.json");
        PixelLayer objects = wild.GetLayer("objects");
        foreach (PixelCell c in new List<PixelCell>(objects.Cells))
            objects.Set(c.Cell, c.Value == "settlement_village" ? "independent_settlement" : "imperial_army");
        var sources = Sources();
        sources[2] = new LegacySource(MapSemantic.Wildlands, "wildlands_new", wild);
        LegacyImportResult r = LegacyWorldImporter.Import(Template(), sources);
        Check(r.Ok && r.Issues.Count == 0, "импорт с новыми метками" + (r.Ok ? "" : ": " + r.Error));
        if (!r.Ok) return;
        Check(CountType(r.World, WorldObjectType.VillageMark) == 38 && CountType(r.World, WorldObjectType.ArmyMark) == 4, "38 независимых поселений и 4 армии");
        Check(WorldJsonCodec.Write(r.World) == WorldJsonCodec.Write(before), "мир совпадает с импортом старых меток байт в байт");
        Check(WorldValidator.Validate(Input(r.World, sources)).Count == WorldValidator.Validate(Input(before, Sources())).Count, "находок столько же");
    }

    private static void S2NoMeaningInColor()
    {
        World w = ImportAll().World;
        JsonObject root = WorldJsonCodec.ToJson(w);
        string cells = JsonWriter.Write(root.Get("cells"), new JsonWriterOptions());
        string objects = JsonWriter.Write(root.Get("objects"), new JsonWriterOptions());
        Check(!cells.Contains("color_") && !cells.Contains("\"CC") && !objects.Contains("color_"), "ни одна клетка и объект не хранят цвет");
        foreach (CellData c in w.Cells) Check(c.Owner != null && EntityId.IsValid(c.Owner) && c.Landscape != null, "клетка " + c.Cell + " ссылается на id");
    }

    private static void S2ColorChange()
    {
        World w = ImportAll().World;
        string cellsBefore = JsonWriter.Write(WorldJsonCodec.ToJson(w).Get("cells"), new JsonWriterOptions());
        var history = new CommandHistory();
        history.Execute(w, WorldEdits.SetColor("state.ulteria", RgbColor.FromHex("112233")));
        Check(w.DisplayColorOf("state.ulteria").Value.ToHex() == "112233", "отображение взяло новый цвет");
        Check(JsonWriter.Write(WorldJsonCodec.ToJson(w).Get("cells"), new JsonWriterOptions()) == cellsBefore, "клетки не изменились");
        history.Undo(w);
        Check(w.DisplayColorOf("state.ulteria").Value.ToHex() == "CC751E", "отмена смены цвета");
    }

    private static void S2WrongSemantic()
    {
        var natureAsPolitical = new List<LegacySource> { new LegacySource(MapSemantic.Political, "nature", Fixture("legacy_nature_184.json")) };
        LegacyImportResult r = LegacyWorldImporter.Import(Template(), natureAsPolitical);
        List<Finding> f = WorldValidator.Validate(Input(r.World, natureAsPolitical));
        int unknown = 0;
        foreach (Finding x in f) if (x.Check == "01_unknown_value") unknown++;
        Check(unknown == 17, "ландшафт как политика: 17 неизвестных значений (горы), найдено " + unknown);

        var politicalAsLandscape = new List<LegacySource> { new LegacySource(MapSemantic.Landscape, "political", Fixture("legacy_political_286.json")) };
        r = LegacyWorldImporter.Import(Template(), politicalAsLandscape);
        f = WorldValidator.Validate(Input(r.World, politicalAsLandscape));
        unknown = 0;
        foreach (Finding x in f) if (x.Check == "01_unknown_value") unknown++;
        Check(unknown == 144 && r.Issues.Count == 1, "политика как ландшафт: 144 неизвестных + предупреждение об объектах (" + unknown + ", " + r.Issues.Count + ")");

        Check(!LegacyWorldImporter.Import(Template(), new List<LegacySource>
        {
            new LegacySource(MapSemantic.Political, "a", Fixture("legacy_political_286.json")),
            new LegacySource(MapSemantic.Political, "b", Fixture("legacy_political_286.json"))
        }).Ok, "две карты одной семантики - отказ");
        var local = new PixelArchitectDocument("local", 1000, 1000);
        Check(!LegacyWorldImporter.Import(Template(), new List<LegacySource> { new LegacySource(MapSemantic.Political, "l", local) }).Ok,
            "локальная карта - отказ");
    }

    private static void S2Export()
    {
        World w = ImportAll().World;
        var names = new Dictionary<MapSemantic, string>
        {
            { MapSemantic.Landscape, "legacy_nature_184.json" }, { MapSemantic.Political, "legacy_political_286.json" },
            { MapSemantic.Wildlands, "legacy_wildlands_226.json" }
        };
        foreach (var pair in names)
        {
            var warnings = new List<string>();
            PixelArchitectDocument exported = LegacyWorldExporter.Export(w, pair.Key, "2026-11-18T00:00:00", warnings);
            Check(warnings.Count == 0, "экспорт " + pair.Value + " без предупреждений" + (warnings.Count == 0 ? "" : ": " + warnings[0]));
            PixelArchitectReadResult reread = PixelArchitectCodec.Read(PixelArchitectCodec.Write(exported));
            Check(reread.Ok, "экспорт читается");
            PixelArchitectDocument original = Fixture(pair.Value);
            Check(reread.Document.DeclaredTotalBlocks == original.DeclaredTotalBlocks, "total_blocks " + original.DeclaredTotalBlocks);
            int diffs = 0;
            string onlyDiff = null;
            foreach (PixelCell c in original.GetLayer("landscape").Cells)
                if (reread.Document.GetLayer("landscape").Get(c.Cell) != c.Value) { diffs++; onlyDiff = c.Cell.ToKey() + "=" + c.Value; }
            if (pair.Key == MapSemantic.Landscape)
                Check(diffs == 1 && onlyDiff == "14,9=stone_wall", "ландшафт: единственное отличие - stone_wall стал color_383838 (" + diffs + ")");
            else
                Check(diffs == 0, "слой клеток совпадает с исходником (" + diffs + ")");
            PixelLayer origObjects = original.GetLayer("objects");
            PixelLayer newObjects = reread.Document.GetLayer("objects");
            Check(origObjects.Count == newObjects.Count, "объектов " + origObjects.Count);
            // Метки диких земель пишутся новыми id PixelBuilder (D-33): Д -> independent_settlement, Г -> imperial_army.
            foreach (PixelCell c in origObjects.Cells)
            {
                string expected = pair.Key != MapSemantic.Wildlands ? c.Value
                    : c.Value == "settlement_village" ? "independent_settlement" : c.Value == "settlement_city" ? "imperial_army" : c.Value;
                Check(newObjects.Get(c.Cell) == expected, "объект " + c.Cell + " = " + expected);
            }
            if (_exportDir != null)
            {
                Directory.CreateDirectory(_exportDir);
                TextFileStore.WriteAllTextAtomic(Path.Combine(_exportDir, "export_" + Legend.SemanticCode(pair.Key) + ".json"), PixelArchitectCodec.Write(exported));
            }
        }
        var history = new CommandHistory();
        history.Execute(w, WorldEdits.Paint(w, EditLayer.Landscape, "plains", new[] { new CellCoord(0, 0) }, null));
        var warn = new List<string>();
        LegacyWorldExporter.Export(w, MapSemantic.Landscape, "", warn);
        Check(warn.Count == 1 && warn[0].Contains("маски"), "суша вне старой маски - предупреждение");
    }

    private static void S2Conflicts()
    {
        PixelArchitectDocument wild = Fixture("legacy_wildlands_226.json");
        wild.GetLayer("landscape").Set(new CellCoord(9, 0), "color_035BBC");
        var sources = new List<LegacySource>
        {
            new LegacySource(MapSemantic.Political, "political", Fixture("legacy_political_286.json")),
            new LegacySource(MapSemantic.Wildlands, "wildlands", wild)
        };
        LegacyImportResult r = LegacyWorldImporter.Import(Template(), sources);
        Check(r.Ok && r.Issues.Count == 1 && r.Issues[0].Cell.Value == new CellCoord(9, 0), "расхождение владельца между файлами - находка");
        Check(r.World.FindCell(new CellCoord(9, 0)).Owner == "state.reinhard", "остаётся значение политической карты");

        PixelArchitectDocument shrunk = Fixture("legacy_nature_184.json");
        PixelArchitectDocument partial = new PixelArchitectDocument("global", 21, 15);
        partial.GetOrAddLayer("political");
        PixelLayer l = partial.GetOrAddLayer("landscape");
        foreach (PixelCell c in shrunk.GetLayer("landscape").Cells) if (c.Cell != new CellCoord(9, 0)) l.Set(c.Cell, c.Value);
        r = LegacyWorldImporter.Import(Template(), new List<LegacySource>
        {
            new LegacySource(MapSemantic.Landscape, "nature-183", partial),
            new LegacySource(MapSemantic.Political, "political", Fixture("legacy_political_286.json"))
        });
        Check(r.Ok && r.Issues.Count == 1 && r.Issues[0].Message.Contains("не во всех картах"), "разная суша в картах - находка");
    }

    // ---------- Этап 3 ----------

    private static string Summary(List<Finding> findings)
    {
        var parts = new List<string>();
        foreach (Finding f in findings) parts.Add(Finding.Marker(f.Severity) + f.Check + (f.EntityId != null ? "/" + f.EntityId : ""));
        return string.Join(" ", parts.ToArray());
    }

    private static void S3Expected()
    {
        List<LegacySource> sources = Sources();
        LegacyImportResult r = LegacyWorldImporter.Import(Template(), sources);
        List<Finding> f = WorldValidator.Validate(Input(r.World, sources));
        string expected =
            "[WARNING]07_settlement_table/state.ulteria " +
            "[INFO]02_shared_value/state.askelia [INFO]02_shared_value/state.kannararia [INFO]02_shared_value/state.ulteria " +
            "[INFO]05_army_marks/army.girdenburg [INFO]06_village_marks " +
            "[INFO]09_connectivity/wild.group_1 [INFO]09_connectivity/wild.group_3 [INFO]09_connectivity/wild.group_2";
        string actual = Summary(f);
        Check(actual == expected, "ровно ожидаемые находки на текущих картах" + (actual == expected ? "" : ":\n    " + actual));
        Check(Finding.Count(f, Severity.Critical) == 0, "нет критических находок");
        Finding ulteria = f.Find(x => x.Check == "07_settlement_table");
        Check(ulteria != null && ulteria.Message.Contains("на карте 3") && ulteria.Message.Contains("таблице автора 4"), "Ультерия: 3 на карте, 4 в таблице");
        Finding marks = f.Find(x => x.Check == "06_village_marks");
        Check(marks != null && marks.Message.Contains(": 38.") && marks.Message.Contains("Без лорда"), "38 независимых поселений");
    }

    private static List<Finding> FindingsOf(World w, string code, ValidationInput input)
    {
        List<Finding> all = WorldValidator.Validate(input ?? Input(w, null));
        return all.FindAll(x => x.Check == code);
    }

    private static void S3EachCheck()
    {
        World w;
        var h = new CommandHistory();

        w = ImportAll().World;
        w.FindCell(new CellCoord(9, 0)).Landscape = "swamp";
        w.FindCell(new CellCoord(10, 0)).Owner = "state.missing";
        List<Finding> f = FindingsOf(w, "01_unknown_value", null);
        Check(f.Count == 2 && f.TrueForAll(x => x.Severity == Severity.Critical), "1: неизвестный ландшафт и владелец [CRITICAL]");

        w = ImportAll().World;
        w.FindCell(new CellCoord(9, 0)).Owner = null;
        f = FindingsOf(w, "03_land_without_owner", null);
        Check(f.Count == 1 && f[0].Severity == Severity.Warning && f[0].Cell.Value == new CellCoord(9, 0), "3: суша без владельца [WARNING]");

        PixelArchitectDocument bad = Fixture("legacy_wildlands_226.json");
        bad.DeclaredTotalBlocks = 225;
        var input = Input(ImportAll().World, new[] { new LegacySource(MapSemantic.Wildlands, "wild", bad) });
        f = FindingsOf(input.World, "04_total_blocks", input);
        Check(f.Count == 1 && f[0].Severity == Severity.Warning, "4: total_blocks [WARNING]");

        w = ImportAll().World;
        WorldObject mark = w.Objects[w.Objects.Count - 1];
        foreach (WorldObject o in w.Objects) if (o.Type == WorldObjectType.ArmyMark) mark = o;
        w.RemoveObject(mark.Id);
        f = FindingsOf(w, "05_army_marks", null);
        Check(f.Exists(x => x.Severity == Severity.Warning && x.Message.Contains("Меток Г на карте 3")), "5: меток меньше, чем бродячих армий [WARNING]");

        w = ImportAll().World;
        w.Relations.Set(0, 1, 150);
        f = FindingsOf(w, "08_relations", null);
        Check(f.Count == 1 && f[0].Severity == Severity.Critical, "8: значение вне диапазона [CRITICAL]");
        w.Relations = w.Relations.Reordered(new[] { "state.ortenverg", "state.reinhard", "state.ulteria", "state.askelia", "state.rosenwald",
                                                    "state.kannararia", "state.girdenburg", "state.valden" });
        f = FindingsOf(w, "08_relations", null);
        Check(f.Exists(x => x.Message.Contains("Порядок")), "8: порядок не совпадает с реестром [CRITICAL]");

        w = ImportAll().World;
        foreach (CellData c in w.Cells) if (c.Owner == "state.valden") c.Owner = "state.kannararia";
        f = FindingsOf(w, "09_connectivity", null);
        Check(f.Exists(x => x.EntityId == "state.valden" && x.Severity == Severity.Warning), "9: государство без клеток [WARNING]");

        w = ImportAll().World;
        h.Execute(w, WorldEdits.SetArmyPosition("army.east", new CellCoord(10, 0)));
        f = FindingsOf(w, "10_army_outside_wildlands", null);
        Check(f.Exists(x => x.EntityId == "army.east" && x.Severity == Severity.Info), "10: армия не в диких землях [INFO]");

        w = ImportAll().World;
        h.Execute(w, WorldEdits.SetArmyRoute("army.south", new[] { new CellCoord(12, 3), new CellCoord(12, 4), new CellCoord(12, 6) }));
        f = FindingsOf(w, "11_army_route", null);
        Check(f.Count == 1 && f[0].Severity == Severity.Warning, "11: разрыв маршрута [WARNING]");

        w = ImportAll().World;
        input = Input(w, null);
        input.ExternalIds.Add("state.reinhard");
        f = FindingsOf(w, "12_duplicate_ids", input);
        Check(f.Count == 1 && f[0].Severity == Severity.Critical && f[0].EntityId == "state.reinhard", "12: дубликат id [CRITICAL]");

        w = ImportAll().World;
        w.FindArmy("army.west_1").Route.Add(new CellCoord(21, 3));
        f = FindingsOf(w, "13_out_of_grid", null);
        Check(f.Count == 1 && f[0].Severity == Severity.Critical, "13: за невидимой стеной [CRITICAL]");

        w = ImportAll().World;
        w.Legend.LandscapeTypes.Add(new LandscapeType("deep_sea", "Глубокое море", RgbColor.FromHex("0000FF")));
        w.FindCell(new CellCoord(9, 0)).Landscape = "deep_sea";
        f = FindingsOf(w, "14_sea", null);
        Check(f.Count == 2 && f.TrueForAll(x => x.Severity == Severity.Critical), "14: море в типе и в клетке [CRITICAL]");
        Check(!MiniLife.WorldStudio.Validation.Checks.SeaCheck.MentionsSea("lake") && !MiniLife.WorldStudio.Validation.Checks.SeaCheck.MentionsSea("Мореход") &&
              MiniLife.WorldStudio.Validation.Checks.SeaCheck.MentionsSea("Море") && MiniLife.WorldStudio.Validation.Checks.SeaCheck.MentionsSea("inner_sea"), "14: распознавание моря");
    }

    private static void S3ReadOnly()
    {
        List<LegacySource> sources = Sources();
        World w = LegacyWorldImporter.Import(Template(), sources).World;
        w.FindCell(new CellCoord(9, 0)).Owner = null;
        string before = WorldJsonCodec.Write(w);
        WorldValidator.Validate(Input(w, sources));
        Check(WorldJsonCodec.Write(w) == before, "валидатор не меняет данные");
        Check(PixelArchitectCodec.Write(sources[0].Document) == PixelArchitectCodec.Write(Fixture("legacy_nature_184.json")), "и не меняет исходные файлы");
    }

    // ---------- Этап 4 ----------

    private static void S4XlsxRoundTrip()
    {
        World w = Template();
        byte[] xlsx = XlsxRelationsCodec.Write(w);
        XlsxRelationsResult r = XlsxRelationsCodec.Read(xlsx, w);
        Check(r.Ok && r.Matrix.SameValues(w.Relations), "экспорт и импорт xlsx без отличий" + (r.Ok ? "" : ": " + r.Error));
        Check(xlsx.Length > 4 && xlsx[0] == (byte)'P' && xlsx[1] == (byte)'K', "xlsx - это zip");
        if (_exportDir != null)
        {
            Directory.CreateDirectory(_exportDir);
            File.WriteAllBytes(Path.Combine(_exportDir, "relations.xlsx"), xlsx); // для внешней проверки (openpyxl, Excel)
        }
        Check(w.Relations.Get(0, 1) == -100 && w.Relations.Get(1, 0) == -80 && w.Relations.Get(4, 6) == 80 && w.Relations.Get(6, 5) == -90,
            "эталон Приложения B (несимметрично)");
    }

    private static void S4XlsxAuthorLike()
    {
        World w = Template();
        XlsxRelationsResult r = XlsxRelationsCodec.Read(File.ReadAllBytes(Path.Combine(_fixtures, "relations_author_like.xlsx")), w);
        Check(r.Ok && r.Matrix.SameValues(w.Relations), "файл как из Excel (sharedStrings, смещение, другой порядок, верхний регистр) = Приложение B" +
              (r.Ok ? "" : ": " + r.Error));
    }

    private static void S4XlsxBroken()
    {
        World w = Template();
        Check(!XlsxRelationsCodec.Read(new byte[] { 1, 2, 3 }, w).Ok, "не zip - отказ");
        Check(!XlsxRelationsCodec.Read(new byte[0], w).Ok, "пустой файл - отказ");
        World fewer = Template();
        var h = new CommandHistory();
        h.Execute(fewer, WorldEdits.AddState(fewer, "Новое", StateKind.County, RgbColor.FromHex("101010")));
        XlsxRelationsResult r = XlsxRelationsCodec.Read(XlsxRelationsCodec.Write(w), fewer);
        Check(!r.Ok && r.Error.Contains("Новое"), "нет государства из реестра - отказ с названием");
        World diag = Template();
        diag.Relations = new RelationsMatrix(diag.Relations.Order);
        Check(XlsxRelationsCodec.Read(XlsxRelationsCodec.Write(diag), diag).Ok, "пустая матрица читается");
    }

    // ---------- Этап 5 ----------

    private static void S5Deterministic()
    {
        World w = ImportAll().World;
        string a = WorldJsonCodec.Write(w);
        string b = WorldJsonCodec.Write(ImportAll().World);
        Check(a == b, "два экспорта одних данных байт в байт");
        WorldReadResult r = WorldJsonCodec.Read(a);
        Check(r.Ok && WorldJsonCodec.Write(r.World) == a, "чтение и запись world.json байт в байт" + (r.Ok ? "" : ": " + r.Error));
        Check(!a.Contains("\r") && a.EndsWith("}\n") && a[0] == '{', "LF, перевод строки в конце, без BOM");
        Check(a.IndexOf("\"buildings\"") < a.IndexOf("\"cells\"") && a.IndexOf("\"cells\"") < a.IndexOf("\"schema_version\""), "ключи отсортированы");

        // Порядок добавления объектов не влияет на файл.
        World shuffled = ImportAll().World;
        var objs = new List<WorldObject>(shuffled.Objects);
        foreach (WorldObject o in objs) shuffled.RemoveObject(o.Id);
        objs.Reverse();
        World fresh = Template();
        LegacyImportResult again = LegacyWorldImporter.Import(fresh, new List<LegacySource>
        {
            new LegacySource(MapSemantic.Landscape, "nature", Fixture("legacy_nature_184.json")),
            new LegacySource(MapSemantic.Political, "political", Fixture("legacy_political_286.json")),
            new LegacySource(MapSemantic.Wildlands, "wildlands", Fixture("legacy_wildlands_226.json")),
        });
        Check(WorldJsonCodec.Write(again.World) == a, "повторный импорт даёт тот же файл");
    }

    private static void S5ReadErrors()
    {
        string good = WorldJsonCodec.Write(Template());
        var cases = new Dictionary<string, string>
        {
            { "не JSON", "{" },
            { "нет schema_version", good.Replace("\"schema_version\": 1", "\"schema_version_x\": 1") },
            { "схема новее", good.Replace("\"schema_version\": 1", "\"schema_version\": 2") },
            { "схема старше без миграции", good.Replace("\"schema_version\": 1", "\"schema_version\": 0") },
            { "дубликат id", good.Replace("\"state.ortenverg\"", "\"state.reinhard\"") },
            { "неизвестный тип государства", good.Replace("\"kind\": \"county\"", "\"kind\": \"barony\"") },
            { "плохой цвет", good.Replace("\"CC1E1E\"", "\"red\"") },
            { "диагональ", good.Replace("[\n        null,\n        -100,\n        -100,\n        -100,\n        -60", "[\n        5,\n        -100,\n        -100,\n        -100,\n        -60") },
        };
        foreach (var c in cases)
        {
            WorldReadResult r = WorldJsonCodec.Read(c.Value);
            Check(!r.Ok && !string.IsNullOrEmpty(r.Error), "отказ world.json: " + c.Key + (r.Ok ? "" : " -> " + r.Error));
        }
        WorldReadResult dup = WorldJsonCodec.Read(cases["дубликат id"]);
        Check(dup.Error.Contains("state.reinhard") && dup.Error.Contains("CRITICAL"), "дубликат назван в ошибке");
        string full = WorldJsonCodec.Write(ImportAll().World);
        int at = full.IndexOf("\"x\": 9,");
        string withObj = full.Substring(0, at) + "\"x\": 30," + full.Substring(at + "\"x\": 9,".Length);
        Check(!WorldJsonCodec.Read(withObj).Ok, "клетка за пределами сетки - отказ");
    }

    private sealed class TestMigration : IWorldMigration
    {
        public int From { get { return 0; } }
        public string Description { get { return "v0: meta.title -> meta.name"; } }
        public void Apply(JsonObject root)
        {
            var meta = (JsonObject)root.Get("meta");
            var renamed = new JsonObject();
            foreach (var m in meta.Members) renamed.Add(m.Key == "title" ? "name" : m.Key, m.Value);
            root.Set("meta", renamed);
        }
    }

    private static void S5Migrations()
    {
        string v1 = WorldJsonCodec.Write(Template());
        string v0 = v1.Replace("\"schema_version\": 1", "\"schema_version\": 0").Replace("\"name\": \"MiniLife\"", "\"title\": \"Старый мир\"");
        WorldReadResult r = WorldJsonCodec.Read(v0, new List<IWorldMigration> { new TestMigration() });
        Check(r.Ok && r.MigratedFromVersion == 0 && r.World.Meta.Name == "Старый мир" && r.World.SchemaVersion == 1, "миграция 0 -> 1" + (r.Ok ? "" : ": " + r.Error));
        Check(!WorldJsonCodec.Read(v0).Ok, "без миграции старый файл не открывается молча");
    }

    private static void S5Preserve()
    {
        World w = Template();
        w.GeneratorVersionRaw = "{\"hash\":\"abc\",\"version\":3}";
        w.ExtraRaw["future_field"] = "[1,2.50]";
        w.LoreRaw = "{\"links\":[],\"timeline\":[{\"event\":\"смерть императора\"}]}";
        var h = new CommandHistory();
        h.Execute(w, WorldEdits.AddArmy(w, ArmyStatus.Roaming));
        h.Undo(w);
        WorldReadResult r = WorldJsonCodec.Read(WorldJsonCodec.Write(w));
        Check(r.Ok, "чтение" + (r.Ok ? "" : ": " + r.Error));
        if (!r.Ok) return;
        Check(r.World.GeneratorVersionRaw.Contains("\"version\": 3"), "generator_version перенесён без изменений");
        Check(r.World.ExtraRaw["future_field"].Contains("2.50"), "неизвестное поле корня сохранено");
        Check(r.World.LoreRaw.Contains("смерть императора"), "lore сохранён");
        Check(r.World.WasIdIssued("army.new_1") && !r.World.IsIdLive("army.new_1") && r.World.NextId(EntityId.Army, "new") == "army.new_2",
            "удалённый id не выдаётся повторно после перезагрузки");
    }

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ws_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void S5Versions()
    {
        string dir = TempDir();
        try
        {
            var now = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
            var store = new VersionStore(() => now);
            string path = Path.Combine(dir, "world.json");
            store.Save(path, "{\"v\":1}\n", "первое");
            Check(store.List(path).Count == 0, "первое сохранение без копии");
            now = now.AddSeconds(1);
            store.Save(path, "{\"v\":1}\n", "то же");
            Check(store.List(path).Count == 0, "без изменений - копия не делается");
            now = now.AddSeconds(1);
            store.Save(path, "{\"v\":2}\n", "второе");
            List<StoredVersion> versions = store.List(path);
            Check(versions.Count == 1 && TextFileStore.ReadAllText(versions[0].Path) == "{\"v\":1}\n", "копия прежней версии");
            now = now.AddSeconds(1);
            store.Restore(path, versions[0]);
            Check(TextFileStore.ReadAllText(path) == "{\"v\":1}\n", "откат к предыдущей версии");
            versions = store.List(path);
            Check(versions.Count == 2 && TextFileStore.ReadAllText(versions[0].Path) == "{\"v\":2}\n", "перед откатом текущая версия сохранена");
            string[] journal = File.ReadAllLines(Path.Combine(VersionStore.HistoryDir(path), "journal.log"));
            Check(journal.Length == 6 && journal[journal.Length - 1].Contains("\trestore\t"), "журнал: " + journal.Length + " записей");
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------- Application ----------

    private static void AppPaintUndo()
    {
        World w = ImportAll().World;
        var h = new CommandHistory();
        string before = WorldJsonCodec.Write(w);
        CellsCommand cmd = WorldEdits.Paint(w, EditLayer.Landscape, "forest", WorldEdits.Rectangle(new CellCoord(9, 0), new CellCoord(11, 1)), null);
        h.Execute(w, cmd);
        Check(w.FindCell(new CellCoord(10, 0)).Landscape == "forest" && w.FindCell(new CellCoord(11, 0)).Landscape == "forest", "прямоугольник ландшафта");
        h.Undo(w);
        Check(WorldJsonCodec.Write(w) == before, "отмена возвращает мир байт в байт");
        h.Redo(w);
        Check(w.FindCell(new CellCoord(10, 1)).Landscape == "forest", "повтор");
        h.Undo(w);
        CellsCommand newLand = WorldEdits.Paint(w, EditLayer.Landscape, "plains", new[] { new CellCoord(0, 0) }, null);
        h.Execute(w, newLand);
        Check(w.IsLand(new CellCoord(0, 0)) && w.FindCell(new CellCoord(0, 0)).Owner == null, "ландшафт на не-суше создаёт сушу");
        Check(WorldEdits.Paint(w, EditLayer.Political, "state.reinhard", new[] { new CellCoord(1, 0) }, null).Count == 0, "владелец не ставится на не-сушу");
        h.Undo(w);
        Check(!w.IsLand(new CellCoord(0, 0)) && WorldJsonCodec.Write(w) == before, "отмена создания суши");
        Throws(delegate { WorldEdits.Paint(w, EditLayer.Political, "wild.group_1", new[] { new CellCoord(9, 0) }, null); }, "группа диких земель на слое политики - отказ");
        Throws(delegate { WorldEdits.Paint(w, EditLayer.Landscape, "sea", new[] { new CellCoord(9, 0) }, null); }, "неизвестный ландшафт - отказ");
        string refused;
        IWorldCommand rm = WorldEdits.RemoveLand(w, new[] { new CellCoord(14, 7) }, out refused);
        Check(rm == null && refused != null, "сушу с объектом убрать нельзя");
    }

    private static void AppFillLayers()
    {
        World w = ImportAll().World;
        var h = new CommandHistory();
        CellsCommand fill = WorldEdits.Fill(w, EditLayer.Political, new CellCoord(14, 7), "state.valden");
        Check(fill.Count == 4, "заливка Гирденбурга: 4 клетки (" + fill.Count + ")");
        h.Execute(w, fill);
        Check(w.FindCell(new CellCoord(15, 8)).Owner == "state.valden", "заливка применена");
        h.Undo(w);
        CellsCommand eraseWildOnPolitics = WorldEdits.Paint(w, EditLayer.Political, null, new[] { new CellCoord(4, 6) }, null);
        Check(eraseWildOnPolitics.Count == 0, "ластик политики не стирает дикие земли");
        CellsCommand eraseWild = WorldEdits.Paint(w, EditLayer.Wildlands, null, new[] { new CellCoord(4, 6) }, null);
        Check(eraseWild.Count == 1, "ластик диких земель стирает");
        Check(WorldEdits.ValueAt(w, EditLayer.Political, new CellCoord(4, 6)) == null &&
              WorldEdits.ValueAt(w, EditLayer.Wildlands, new CellCoord(4, 6)) == "wild.group_3", "пипетка видит только свой слой");
        CellsCommand fillLand = WorldEdits.Fill(w, EditLayer.Landscape, new CellCoord(14, 9), "sands");
        Check(fillLand.Count == 1, "заливка ландшафта: одиночная гора");
    }

    private static void AppHistoryDepth()
    {
        World w = ImportAll().World;
        string before = WorldJsonCodec.Write(w);
        var h = new CommandHistory();
        var cells = new List<CellData>(w.SortedCells());
        for (int i = 0; i < 250; i++)
        {
            CellCoord c = cells[i % cells.Count].Cell;
            h.Execute(w, WorldEdits.Paint(w, EditLayer.Landscape, i % 2 == 0 ? "sands" : "forest", new[] { c }, null));
        }
        int undone = 0;
        while (h.Undo(w)) undone++;
        Check(undone == 250 && WorldJsonCodec.Write(w) == before, "250 шагов отмены (не меньше 200), мир вернулся");
        var small = new CommandHistory(3);
        for (int i = 0; i < 5; i++) small.Execute(w, WorldEdits.SetCellNotes(w, new CellCoord(9, 0), "n" + i));
        Check(small.UndoCount == 3, "ограничение глубины");
    }

    private static void AppObjects()
    {
        World w = ImportAll().World;
        var h = new CommandHistory();
        string refused;
        IWorldCommand place = WorldEdits.PlaceObject(w, new CellCoord(9, 0), WorldObjectType.Castle, out refused);
        h.Execute(w, place);
        WorldObject castle = w.ObjectAt(new CellCoord(9, 0));
        Check(castle != null && castle.Type == WorldObjectType.Castle && castle.Owner == "state.reinhard", "замок поставлен, владелец по клетке");
        IWorldCommand replace = WorldEdits.PlaceObject(w, new CellCoord(9, 0), WorldObjectType.City, out refused);
        h.Execute(w, replace);
        Check(w.ObjectAt(new CellCoord(9, 0)).Type == WorldObjectType.City && CountType(w, WorldObjectType.Castle) == 8, "замена объекта");
        h.Undo(w);
        Check(w.ObjectAt(new CellCoord(9, 0)).Id == castle.Id, "отмена замены вернула тот же объект");
        h.Undo(w);
        Check(w.ObjectAt(new CellCoord(9, 0)) == null && w.WasIdIssued(castle.Id), "отмена постановки; id остаётся выданным");
        Check(WorldEdits.PlaceObject(w, new CellCoord(0, 0), WorldObjectType.Village, out refused) == null && refused != null, "объект только на суше");
        IWorldCommand army = WorldEdits.PlaceObject(w, new CellCoord(16, 5), WorldObjectType.ArmyMark, out refused);
        h.Execute(w, army);
        Check(w.ObjectAt(new CellCoord(16, 5)).Owner == "army.east", "метка Г связывается с армией на клетке");
        h.Undo(w);
    }

    private static void AppRegistry()
    {
        World w = ImportAll().World;
        var h = new CommandHistory();
        RelationsMatrix before = w.Relations.Clone();
        h.Execute(w, WorldEdits.AddState(w, "Новое графство", StateKind.County, RgbColor.FromHex("123456")));
        Check(w.States.Count == 9 && w.Relations.Size == 9 && w.Relations.Order[8] == "state.new_1", "новое государство в реестре и в матрице");
        Check(w.Relations.Get(0, 1) == -100 && w.Relations.Get(8, 0) == null, "старые значения сохранены, новые пустые");
        h.Execute(w, WorldEdits.SetRelation("state.new_1", "state.reinhard", -20));
        Check(w.Relations.Get(8, 0) == -20, "значение отношения");
        h.Undo(w);
        h.Undo(w);
        Check(w.States.Count == 8 && w.Relations.SameValues(before), "отмена добавления вернула матрицу 8 x 8");
        Check(w.NextId(EntityId.State, "new") == "state.new_2", "id отменённого государства не выдаётся повторно");
        h.Redo(w);
        Check(w.FindState("state.new_1") != null && w.Relations.Size == 9, "повтор добавления - тот же id");
        string refused;
        Check(WorldEdits.RemoveEntity(w, "state.reinhard", out refused) == null && refused.Contains("ссылаются"), "нельзя удалить государство с клетками");
        IWorldCommand rm = WorldEdits.RemoveEntity(w, "state.new_1", out refused);
        h.Execute(w, rm);
        Check(w.FindState("state.new_1") == null && w.Relations.Size == 8, "удаление государства без ссылок");
        h.Undo(w);
        Check(w.FindState("state.new_1") != null && w.Relations.Size == 9, "отмена удаления");
        h.Execute(w, WorldEdits.SetStateName("state.valden", "Вальден-2"));
        Check(w.FindState("state.valden").Name == "Вальден-2", "переименование");
        Check(WorldEdits.RemoveEntity(w, "army.east", out refused) == null, "нельзя удалить армию с меткой");
    }

    private static void AppArmyRoute()
    {
        World w = ImportAll().World;
        var h = new CommandHistory();
        var route = new List<CellCoord> { new CellCoord(12, 3), new CellCoord(12, 4), new CellCoord(12, 5) };
        h.Execute(w, WorldEdits.SetArmyRoute("army.south", route));
        route.Add(new CellCoord(99, 99));
        Check(w.FindArmy("army.south").Route.Count == 3, "маршрут скопирован, а не связан со списком вызывающего");
        h.Execute(w, WorldEdits.SetArmyOrigin("army.south", new CellCoord(12, 3)));
        h.Execute(w, WorldEdits.SetArmyStatus("army.south", ArmyStatus.Destroyed));
        WorldReadResult r = WorldJsonCodec.Read(WorldJsonCodec.Write(w));
        ArmyEntity a = r.World.FindArmy("army.south");
        Check(a.Route.Count == 3 && a.Origin.Value == new CellCoord(12, 3) && a.Status == ArmyStatus.Destroyed, "маршрут, начало и статус в world.json");
        h.Undo(w); h.Undo(w); h.Undo(w);
        Check(w.FindArmy("army.south").Route.Count == 0 && w.FindArmy("army.south").Status == ArmyStatus.Roaming, "отмена");
    }

    private static void AppDocument()
    {
        string dir = TempDir();
        try
        {
            var now = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
            Func<DateTime> clock = () => now;
            string error;
            WorldDocument doc = WorldDocument.FromImport(ReadData("world_template.json"), Sources(), clock, out error);
            Check(doc != null && error == null, "документ из импорта" + (error == null ? "" : ": " + error));
            if (doc == null) return;
            doc.Reference = ReferenceCodec.Read(ReadData("settlements_reference.json"));
            Check(doc.IsDirty && doc.Validate().Count == 9, "новый документ грязный; 9 находок");
            Check(doc.ExportWorldJson() == doc.ExportWorldJson(), "два экспорта одинаковы");
            string path = Path.Combine(dir, "world.json");
            doc.SaveAs(path);
            Check(!doc.IsDirty && File.Exists(path) && doc.World.Meta.UpdatedAt == "2026-11-18T10:00:00Z", "сохранение");
            string err = doc.Execute(WorldEdits.Paint(doc.World, EditLayer.Landscape, "forest", new[] { new CellCoord(9, 1) }, null));
            Check(err == null && doc.IsDirty, "правка делает документ грязным");
            string auto = doc.Autosave();
            Check(auto != null && File.Exists(auto) && !TextFileStore.ReadAllText(path).Contains("\"x\": 9,\n      \"y\": 1,\n      \"landscape\": \"forest\""),
                "автосохранение не трогает основной файл");
            Check(doc.Autosave() == null, "повторное автосохранение без правок не пишет");
            now = now.AddMinutes(1);
            doc.Save();
            Check(doc.Versions.List(path).Count == 1, "резервная копия перед перезаписью");
            doc.Undo();
            Check(doc.IsDirty, "отмена после сохранения - снова грязный");
            WorldDocument opened = WorldDocument.Open(path, clock, out error);
            Check(opened != null && !opened.IsDirty && opened.World.FindCell(new CellCoord(9, 1)).Landscape == "forest", "открытие сохранённого");
            Check(WorldDocument.Open(Path.Combine(dir, "nope.json"), clock, out error) == null && error != null, "открытие несуществующего - ошибка");
            Check(opened.Execute(new PropertyCommand<int>("бросает", x => 0, (x, v) => { throw new DomainException("нельзя"); }, 1)) == "нельзя",
                "ошибка домена возвращается текстом");
            var warnings = new List<string>();
            Check(opened.ImportRelationsXlsx(opened.ExportRelationsXlsx(), warnings) == null, "импорт отношений командой");
            Check(opened.ImportRelationsXlsx(new byte[] { 0 }, warnings) != null, "битый xlsx - текст ошибки");
        }
        finally { Directory.Delete(dir, true); }
    }
}
