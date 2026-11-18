using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.Json;

namespace MiniLife.WorldStudio.Serialization.PixelArchitect
{
    // Экспорт мира в старый формат для PixelBuilder (раздел 5.1: старый инструмент должен открывать файлы).
    // Каждая семантика - отдельный файл, как у автора сейчас. Источник правды остаётся world.json.
    public static class LegacyWorldExporter
    {
        public static PixelArchitectDocument Export(World w, MapSemantic semantic, string createdAt, List<string> warnings)
        {
            if (w == null) throw new ArgumentNullException("w");
            if (warnings == null) warnings = new List<string>();
            var doc = new PixelArchitectDocument("global", w.Grid.Width, w.Grid.Height);
            // Поля в порядке старого PixelBuilder (ExportJson): сначала занимаются места, потом задаются значения.
            doc.ReserveFieldOrder(PixelArchitectDocument.CanonicalOrder);
            doc.SetRawField("created_at", new JsonString(createdAt ?? ""));
            doc.SetRawField("active_layer", new JsonString(PixelArchitectDocument.LayerLandscape));
            var visibility = new JsonObject();
            visibility.Add("political", new JsonBool(true));
            visibility.Add("landscape", new JsonBool(true));
            doc.SetRawField("visibility", visibility);

            doc.GetOrAddLayer(PixelArchitectDocument.LayerPolitical);
            PixelLayer land = doc.GetOrAddLayer(PixelArchitectDocument.LayerLandscape);
            PixelLayer objects = doc.GetOrAddLayer(PixelArchitectDocument.LayerObjects);

            Dictionary<string, string> ownerToValue = Reverse(w.Legend.LegacyOwnerValues);
            foreach (CellData cell in w.SortedCells())
            {
                if (!LegacyLandMask.IsLand(cell.Cell))
                    warnings.Add("Клетка " + cell.Cell + " вне маски суши старого PixelBuilder: он не откроет этот файл.");
                string value;
                if (semantic == MapSemantic.Landscape)
                {
                    if (cell.Landscape == null) { warnings.Add("Клетка " + cell.Cell + ": ландшафт не задан, в файл не попадёт."); continue; }
                    if (!w.Legend.LegacyExportLandscape.TryGetValue(cell.Landscape, out value))
                    {
                        warnings.Add("Клетка " + cell.Cell + ": для ландшафта \"" + cell.Landscape + "\" нет значения экспорта в легенде, в файл не попадёт.");
                        continue;
                    }
                }
                else
                {
                    if (cell.Owner == null) { warnings.Add("Клетка " + cell.Cell + ": владелец не задан, в файл не попадёт."); continue; }
                    if (!ownerToValue.TryGetValue(cell.Owner, out value))
                    {
                        warnings.Add("Клетка " + cell.Cell + ": для владельца " + cell.Owner + " нет значения в легенде, в файл не попадёт.");
                        continue;
                    }
                }
                land.Set(cell.Cell, value);
            }

            if (semantic != MapSemantic.Landscape)
            {
                Dictionary<string, string> typeToValue = Reverse(w.Legend.ObjectMap(semantic));
                var list = new List<WorldObject>(w.Objects);
                list.Sort((a, b) => a.Cell != b.Cell ? a.Cell.CompareTo(b.Cell) : string.CompareOrdinal(a.Id, b.Id));
                foreach (WorldObject obj in list)
                {
                    // Метки пишутся только в файл диких земель, поселения - только в политическую карту.
                    bool mark = WorldObjectTypes.IsMark(obj.Type);
                    if (mark != (semantic == MapSemantic.Wildlands)) continue;
                    string value;
                    if (!typeToValue.TryGetValue(WorldObjectTypes.ToCode(obj.Type), out value))
                    {
                        warnings.Add("Объект " + obj.Id + ": тип " + WorldObjectTypes.ToCode(obj.Type) + " нельзя записать в " + Legend.SemanticName(semantic) + ".");
                        continue;
                    }
                    if (objects.Contains(obj.Cell))
                    {
                        warnings.Add("Клетка " + obj.Cell + ": в старом формате один объект на клетку, " + obj.Id + " не записан.");
                        continue;
                    }
                    objects.Set(obj.Cell, value);
                }
            }

            var blocks = new PixelLayer();
            foreach (PixelCell c in land.Cells) blocks.Set(c.Cell, c.Value);
            doc.Blocks = blocks;
            doc.DeclaredTotalBlocks = doc.CountStandardBlocks();
            return doc;
        }

        // Обратная таблица; если на один смысл несколько значений, берётся первое по порядку ключей (детерминированно).
        private static Dictionary<string, string> Reverse(IDictionary<string, string> map)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
                if (!result.ContainsKey(map[key])) result.Add(map[key], key);
            return result;
        }
    }
}
