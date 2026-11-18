using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Serialization.PixelArchitect
{
    // Старый файл вместе с явно выбранной семантикой (DECISIONS D-05).
    public sealed class LegacySource
    {
        public LegacySource(MapSemantic semantic, string name, PixelArchitectDocument document)
        {
            Semantic = semantic;
            Name = name ?? "";
            Document = document;
        }

        public MapSemantic Semantic { get; private set; }
        public string Name { get; private set; }
        public PixelArchitectDocument Document { get; private set; }
    }

    public sealed class LegacyImportResult
    {
        internal LegacyImportResult(World world, string error, List<Finding> issues)
        {
            World = world;
            Error = error;
            Issues = issues.AsReadOnly();
        }

        public bool Ok { get { return World != null; } }
        public World World { get; private set; }
        public string Error { get; private set; }
        // Расхождения между файлами, найденные при сборке. Неизвестные значения сюда не попадают:
        // их показывает проверка 1 валидатора по тем же исходным файлам.
        public IList<Finding> Issues { get; private set; }
    }

    // Сборка мира из старых файлов (Этап 2). Шаблон даёт реестр, легенду и отношения; файлы - клетки и объекты.
    // Смысл значения берётся только из таблицы своей семантики (раздел 5.2, антипаттерны 1-3).
    public static class LegacyWorldImporter
    {
        public const string CheckCode = "import";

        public static LegacyImportResult Import(World template, IList<LegacySource> sources)
        {
            var issues = new List<Finding>();
            string error = CheckSources(template, sources);
            if (error != null) return new LegacyImportResult(null, error, issues);

            World w = template;
            Legend legend = w.Legend;
            var ordered = new List<LegacySource>(sources);
            // Политическая карта раньше файла диких земель: при расхождении владельца первым остаётся её значение.
            ordered.Sort((a, b) => ((int)a.Semantic).CompareTo((int)b.Semantic));

            var landBySource = new Dictionary<string, HashSet<CellCoord>>();
            foreach (LegacySource src in ordered)
            {
                var land = new HashSet<CellCoord>();
                landBySource[src.Name] = land;
                if (src.Semantic == MapSemantic.Landscape) ImportLandscape(w, legend, src, land, issues);
                else ImportOwners(w, legend, src, land, issues);
            }
            ReportLandDifferences(landBySource, issues);

            foreach (LegacySource src in ordered)
                if (src.Semantic != MapSemantic.Landscape) ImportObjects(w, legend, src, issues);

            return new LegacyImportResult(w, null, issues);
        }

        private static string CheckSources(World template, IList<LegacySource> sources)
        {
            if (sources == null || sources.Count == 0) return "Не выбрано ни одного файла.";
            var seen = new HashSet<MapSemantic>();
            foreach (LegacySource s in sources)
            {
                if (s.Document == null) return "Файл \"" + s.Name + "\" не прочитан.";
                if (!seen.Add(s.Semantic)) return "Два файла с семантикой \"" + Legend.SemanticName(s.Semantic) + "\": оставьте один.";
                if (s.Document.MapKind != "global")
                    return "Файл \"" + s.Name + "\" не глобальная карта (map_kind: " + (s.Document.MapKind ?? "нет") + ").";
                if (s.Document.Width != template.Grid.Width || s.Document.Height != template.Grid.Height)
                    return "Файл \"" + s.Name + "\": размер " + s.Document.Width + " x " + s.Document.Height +
                           ", ожидался " + template.Grid.Width + " x " + template.Grid.Height + ".";
            }
            return null;
        }

        private static void ImportLandscape(World w, Legend legend, LegacySource src, HashSet<CellCoord> land, List<Finding> issues)
        {
            PixelLayer layer = src.Document.GetLayer(PixelArchitectDocument.LayerLandscape);
            if (layer != null)
            {
                foreach (PixelCell pc in layer.Cells)
                {
                    land.Add(pc.Cell);
                    CellData cell = w.GetOrCreateCell(pc.Cell);
                    string type;
                    // Неизвестное значение: клетка остаётся сушей без ландшафта, проверка 1 покажет её.
                    if (legend.LegacyLandscapeValues.TryGetValue(pc.Value, out type)) cell.Landscape = type;
                }
            }
            PixelLayer political = src.Document.GetLayer(PixelArchitectDocument.LayerPolitical);
            if (political != null && political.Count > 0)
                issues.Add(new Finding(Severity.Warning, CheckCode, "В \"" + src.Name + "\" (" + Legend.SemanticName(src.Semantic) +
                    ") слой political не пуст (" + political.Count + " клеток): не импортирован, владельцы берутся из политической карты.", null, null));
            PixelLayer objects = src.Document.GetLayer(PixelArchitectDocument.LayerObjects);
            if (objects != null && objects.Count > 0)
                issues.Add(new Finding(Severity.Warning, CheckCode, "В \"" + src.Name + "\" (" + Legend.SemanticName(src.Semantic) +
                    ") есть объекты (" + objects.Count + "): не импортированы, объекты берутся из политической карты и файла диких земель.", null, null));
        }

        // В политической карте и файле диких земель владельцы нарисованы на слое landscape, слой political пуст.
        // Берутся оба слоя; если в одной клетке они различаются - находка, первым остаётся political.
        private static void ImportOwners(World w, Legend legend, LegacySource src, HashSet<CellCoord> land, List<Finding> issues)
        {
            var values = new Dictionary<CellCoord, string>();
            var cellsInOrder = new List<CellCoord>();
            foreach (string layerName in new[] { PixelArchitectDocument.LayerPolitical, PixelArchitectDocument.LayerLandscape })
            {
                PixelLayer layer = src.Document.GetLayer(layerName);
                if (layer == null) continue;
                foreach (PixelCell pc in layer.Cells)
                {
                    string existing;
                    if (values.TryGetValue(pc.Cell, out existing))
                    {
                        if (existing != pc.Value)
                            issues.Add(new Finding(Severity.Warning, CheckCode, "\"" + src.Name + "\": в слоях political и landscape разные значения (" +
                                existing + " / " + pc.Value + "), взято первое.", pc.Cell, null));
                        continue;
                    }
                    values.Add(pc.Cell, pc.Value);
                    cellsInOrder.Add(pc.Cell);
                }
            }
            foreach (CellCoord c in cellsInOrder)
            {
                land.Add(c);
                CellData cell = w.GetOrCreateCell(c);
                string owner;
                if (!legend.LegacyOwnerValues.TryGetValue(values[c], out owner)) continue; // проверка 1
                if (cell.Owner != null && cell.Owner != owner)
                {
                    issues.Add(new Finding(Severity.Warning, CheckCode, "Владелец клетки различается между файлами: " + cell.Owner + " / " + owner +
                        " (\"" + src.Name + "\"), оставлен " + cell.Owner + ".", c, cell.Owner));
                    continue;
                }
                cell.Owner = owner;
            }
        }

        private static void ImportObjects(World w, Legend legend, LegacySource src, List<Finding> issues)
        {
            PixelLayer layer = src.Document.GetLayer(PixelArchitectDocument.LayerObjects);
            if (layer == null) return;
            IDictionary<string, string> map = legend.ObjectMap(src.Semantic);
            var cells = new List<PixelCell>(layer.Cells);
            // Построчный порядок клеток: id объектов не зависят от порядка ключей в файле.
            cells.Sort((a, b) => a.Cell.CompareTo(b.Cell));
            foreach (PixelCell pc in cells)
            {
                string code;
                WorldObjectType type;
                if (!map.TryGetValue(pc.Value, out code) || !WorldObjectTypes.TryParse(code, out type)) continue; // проверка 1
                if (w.ObjectAt(pc.Cell) != null)
                    issues.Add(new Finding(Severity.Warning, CheckCode, "На клетке уже есть объект, добавлен второй (" + code + ", \"" + src.Name + "\").", pc.Cell, null));
                var obj = new WorldObject(w.NextId(EntityId.Object, code), pc.Cell, type);
                if (type == WorldObjectType.ArmyMark)
                {
                    ArmyEntity army = FindArmyAt(w, pc.Cell);
                    obj.Owner = army != null ? army.Id : null;
                    if (army == null)
                        issues.Add(new Finding(Severity.Warning, CheckCode, "Метка Г без армии в реестре: ни у одной бродячей армии нет этой клетки.", pc.Cell, null));
                }
                else
                {
                    CellData cell = w.FindCell(pc.Cell);
                    obj.Owner = cell != null ? cell.Owner : null;
                }
                w.AddObject(obj);
            }
        }

        private static ArmyEntity FindArmyAt(World w, CellCoord cell)
        {
            foreach (ArmyEntity a in w.Armies)
                if (a.Status == ArmyStatus.Roaming && a.Position.HasValue && a.Position.Value == cell) return a;
            return null;
        }

        private static void ReportLandDifferences(Dictionary<string, HashSet<CellCoord>> landBySource, List<Finding> issues)
        {
            var names = new List<string>(landBySource.Keys);
            if (names.Count < 2) return;
            var union = new HashSet<CellCoord>();
            foreach (var set in landBySource.Values) union.UnionWith(set);
            var missing = new List<CellCoord>();
            foreach (CellCoord c in union)
                foreach (var set in landBySource.Values)
                    if (!set.Contains(c)) { missing.Add(c); break; }
            missing.Sort();
            foreach (CellCoord c in missing)
            {
                var without = new List<string>();
                foreach (string n in names) if (!landBySource[n].Contains(c)) without.Add("\"" + n + "\"");
                issues.Add(new Finding(Severity.Warning, CheckCode, "Клетка есть не во всех картах, нет в " + string.Join(", ", without.ToArray()) + ".", c, null));
            }
        }
    }
}
