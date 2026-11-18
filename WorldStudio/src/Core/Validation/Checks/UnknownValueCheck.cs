using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.PixelArchitect;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 1. Неизвестное значение: в старом файле - значения нет в таблице его семантики;
    // в мире - ссылка на тип ландшафта или сущность, которых нет в легенде и реестре. [CRITICAL]
    public sealed class UnknownValueCheck : IWorldCheck
    {
        public string Code { get { return "01_unknown_value"; } }
        public string Title { get { return "Неизвестный цвет или значение"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            Legend legend = w.Legend;
            foreach (LegacySource src in input.LegacySources)
            {
                IDictionary<string, string> cellMap = legend.CellMap(src.Semantic);
                string[] layers = src.Semantic == MapSemantic.Landscape
                    ? new[] { PixelArchitectDocument.LayerLandscape }
                    : new[] { PixelArchitectDocument.LayerPolitical, PixelArchitectDocument.LayerLandscape };
                foreach (string layerName in layers)
                {
                    PixelLayer layer = src.Document.GetLayer(layerName);
                    if (layer == null) continue;
                    foreach (PixelCell c in layer.Cells)
                        if (!cellMap.ContainsKey(c.Value))
                            findings.Add(new Finding(Severity.Critical, Code, "\"" + src.Name + "\" (" + Legend.SemanticName(src.Semantic) +
                                "), слой " + layerName + ": значения \"" + c.Value + "\" нет в легенде этой семантики.", c.Cell, null));
                }
                if (src.Semantic == MapSemantic.Landscape) continue;
                PixelLayer objects = src.Document.GetLayer(PixelArchitectDocument.LayerObjects);
                if (objects == null) continue;
                IDictionary<string, string> objMap = legend.ObjectMap(src.Semantic);
                foreach (PixelCell c in objects.Cells)
                    if (!objMap.ContainsKey(c.Value))
                        findings.Add(new Finding(Severity.Critical, Code, "\"" + src.Name + "\" (" + Legend.SemanticName(src.Semantic) +
                            "): объект \"" + c.Value + "\" не описан в легенде этой семантики, в мир не импортирован.", c.Cell, null));
            }

            foreach (CellData c in w.SortedCells())
            {
                if (c.Landscape != null && legend.FindLandscape(c.Landscape) == null)
                    findings.Add(new Finding(Severity.Critical, Code, "Тип ландшафта \"" + c.Landscape + "\" не описан в легенде.", c.Cell, null));
                if (c.Owner != null && !w.IsOwnerId(c.Owner))
                    findings.Add(new Finding(Severity.Critical, Code, "Владелец \"" + c.Owner + "\" не найден в реестре.", c.Cell, c.Owner));
            }
            foreach (WorldObject o in w.Objects)
            {
                if (o.Owner == null) continue;
                bool ok = o.Type == WorldObjectType.ArmyMark ? w.FindArmy(o.Owner) != null : w.IsOwnerId(o.Owner);
                if (!ok)
                    findings.Add(new Finding(Severity.Critical, Code, "Объект ссылается на несуществующую сущность \"" + o.Owner + "\".", o.Cell, o.Id));
            }
            foreach (var pair in legend.LegacyLandscapeValues)
                if (legend.FindLandscape(pair.Value) == null)
                    findings.Add(new Finding(Severity.Critical, Code, "Легенда: \"" + pair.Key + "\" ведёт на неизвестный тип ландшафта \"" + pair.Value + "\".", null, null));
            foreach (var pair in legend.LegacyOwnerValues)
                if (!w.IsOwnerId(pair.Value))
                    findings.Add(new Finding(Severity.Critical, Code, "Легенда: \"" + pair.Key + "\" ведёт на сущность \"" + pair.Value + "\", которой нет в реестре.", null, pair.Value));
        }
    }
}
