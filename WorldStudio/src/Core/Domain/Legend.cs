using System;
using System.Collections.Generic;

namespace MiniLife.WorldStudio.Domain
{
    // Семантика старого файла pixel_architect_v1. Выбирается автором явно при импорте (DECISIONS D-05).
    // Landscape - карта ландшафтов; Political - политическая карта; Wildlands - файл диких земель
    // (те же владельцы клеток, что в политической карте, но объекты - метки Д и Г).
    public enum MapSemantic { Landscape, Political, Wildlands }

    public sealed class LandscapeType
    {
        public LandscapeType(string id, string name, RgbColor color)
        {
            if (string.IsNullOrEmpty(id)) throw new DomainException("Пустой id типа ландшафта.");
            Id = id;
            Name = name ?? "";
            Color = color;
        }

        public string Id { get; private set; }
        public string Name { get; set; }
        public RgbColor Color { get; set; }
    }

    // Легенда хранится в world.json (DECISIONS D-15). Смысл значения старого файла берётся
    // только через таблицу своей семантики: один и тот же hex в разных таблицах означает разное.
    public sealed class Legend
    {
        private readonly List<LandscapeType> _landscapeTypes = new List<LandscapeType>();

        public Legend()
        {
            LegacyLandscapeValues = new SortedDictionary<string, string>(StringComparer.Ordinal);
            LegacyOwnerValues = new SortedDictionary<string, string>(StringComparer.Ordinal);
            LegacyPoliticalObjects = new SortedDictionary<string, string>(StringComparer.Ordinal);
            LegacyWildlandObjects = new SortedDictionary<string, string>(StringComparer.Ordinal);
            LegacyExportLandscape = new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        public IList<LandscapeType> LandscapeTypes { get { return _landscapeTypes; } }

        // Значение клетки карты ландшафтов -> id типа ландшафта ("color_51CC51" -> "plains").
        public SortedDictionary<string, string> LegacyLandscapeValues { get; private set; }
        // Значение клетки политической карты и файла диких земель -> id владельца ("color_CC751E" -> "state.ulteria").
        public SortedDictionary<string, string> LegacyOwnerValues { get; private set; }
        // Объект политической карты -> тип объекта мира ("settlement_city" -> "city").
        public SortedDictionary<string, string> LegacyPoliticalObjects { get; private set; }
        // Объект файла диких земель -> тип объекта мира ("settlement_city" -> "army_mark").
        public SortedDictionary<string, string> LegacyWildlandObjects { get; private set; }
        // При экспорте в старый формат: тип ландшафта -> значение клетки. Нужно там, где одному типу
        // соответствует несколько старых значений (горы: color_383838 и stone_wall).
        public SortedDictionary<string, string> LegacyExportLandscape { get; private set; }

        public LandscapeType FindLandscape(string id)
        {
            foreach (LandscapeType t in _landscapeTypes)
                if (t.Id == id) return t;
            return null;
        }

        public IDictionary<string, string> ObjectMap(MapSemantic semantic)
        {
            if (semantic == MapSemantic.Political) return LegacyPoliticalObjects;
            if (semantic == MapSemantic.Wildlands) return LegacyWildlandObjects;
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        public IDictionary<string, string> CellMap(MapSemantic semantic)
        {
            return semantic == MapSemantic.Landscape ? LegacyLandscapeValues : LegacyOwnerValues;
        }

        public static string SemanticCode(MapSemantic semantic)
        {
            switch (semantic)
            {
                case MapSemantic.Landscape: return "landscape";
                case MapSemantic.Political: return "political";
                default: return "wildlands";
            }
        }

        public static string SemanticName(MapSemantic semantic)
        {
            switch (semantic)
            {
                case MapSemantic.Landscape: return "карта ландшафтов";
                case MapSemantic.Political: return "политическая карта";
                default: return "файл диких земель";
            }
        }
    }
}
