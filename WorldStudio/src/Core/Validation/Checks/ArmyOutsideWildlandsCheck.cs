using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 10. Армия (её клетка или метка Г) стоит не в диких землях. [INFO]
    public sealed class ArmyOutsideWildlandsCheck : IWorldCheck
    {
        public string Code { get { return "10_army_outside_wildlands"; } }
        public string Title { get { return "Армия вне диких земель"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            var reported = new HashSet<string>();
            foreach (ArmyEntity a in w.Armies)
                if (a.Status == ArmyStatus.Roaming && a.Position.HasValue && !IsWild(w, a.Position.Value))
                {
                    findings.Add(new Finding(Severity.Info, Code, "Бродячая армия стоит не в диких землях.", a.Position, a.Id));
                    reported.Add(a.Id + "@" + a.Position.Value.ToKey());
                }
            foreach (WorldObject o in w.Objects)
                if (o.Type == WorldObjectType.ArmyMark && !IsWild(w, o.Cell) && !reported.Contains((o.Owner ?? "") + "@" + o.Cell.ToKey()))
                    findings.Add(new Finding(Severity.Info, Code, "Метка Г стоит не в диких землях.", o.Cell, o.Id));
        }

        private static bool IsWild(World w, CellCoord cell)
        {
            CellData c = w.FindCell(cell);
            return c != null && c.Owner != null && w.FindWildland(c.Owner) != null;
        }
    }
}
