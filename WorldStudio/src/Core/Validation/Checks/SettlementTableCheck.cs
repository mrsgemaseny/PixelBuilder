using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 7. Поселения на карте против таблицы автора: замки, города (с крепостями), деревни (с кратностью).
    // Государство поселения - владелец клетки, на которой оно стоит. Расхождение показывается, не исправляется. [WARNING]
    public sealed class SettlementTableCheck : IWorldCheck
    {
        public string Code { get { return "07_settlement_table"; } }
        public string Title { get { return "Сверка таблицы поселений"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            if (input.Reference == null) return;
            World w = input.World;
            foreach (SettlementRow row in input.Reference.Rows)
            {
                if (w.FindState(row.StateId) == null)
                {
                    findings.Add(new Finding(Severity.Warning, Code, "В таблице автора есть государство, которого нет в реестре.", null, row.StateId));
                    continue;
                }
                int castles = 0, cities = 0, villages = 0;
                foreach (WorldObject o in w.Objects)
                {
                    CellData cell = w.FindCell(o.Cell);
                    if (cell == null || cell.Owner != row.StateId) continue;
                    if (o.Type == WorldObjectType.Castle) castles++;
                    if (WorldObjectTypes.IsCityLike(o.Type)) cities++;
                    villages += WorldObjectTypes.VillageMultiplicity(o.Type);
                }
                string name = WorldValidator.NameOf(w, row.StateId);
                Compare(findings, name, row.StateId, "замков", castles, row.Castles);
                Compare(findings, name, row.StateId, "городов (с крепостями)", cities, row.Cities);
                Compare(findings, name, row.StateId, "деревень (с кратностью)", villages, row.Villages);
            }
        }

        private void Compare(List<Finding> findings, string name, string id, string what, int map, int table)
        {
            if (map != table)
                findings.Add(new Finding(Severity.Warning, Code, name + ": " + what + " на карте " + map + ", в таблице автора " + table + ".", null, id));
        }
    }
}
