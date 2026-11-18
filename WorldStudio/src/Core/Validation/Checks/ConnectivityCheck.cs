using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 9. У каждого государства есть хотя бы одна клетка [WARNING];
    // несвязанные части государств и групп диких земель [INFO].
    public sealed class ConnectivityCheck : IWorldCheck
    {
        public string Code { get { return "09_connectivity"; } }
        public string Title { get { return "Связность государств"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            var byOwner = new Dictionary<string, List<CellCoord>>();
            foreach (CellData c in w.Cells)
            {
                if (c.Owner == null) continue;
                List<CellCoord> list;
                if (!byOwner.TryGetValue(c.Owner, out list)) byOwner[c.Owner] = list = new List<CellCoord>();
                list.Add(c.Cell);
            }
            var owners = new List<string>();
            foreach (StateEntity s in w.States) owners.Add(s.Id);
            foreach (WildlandEntity g in w.Wildlands) owners.Add(g.Id);
            foreach (string id in owners)
            {
                List<CellCoord> cells;
                if (!byOwner.TryGetValue(id, out cells))
                {
                    if (w.FindState(id) != null)
                        findings.Add(new Finding(Severity.Warning, Code, "У государства нет ни одной клетки.", null, id));
                    continue;
                }
                List<List<CellCoord>> parts = WorldValidator.Components(cells);
                if (parts.Count < 2) continue;
                var sizes = new List<string>();
                foreach (List<CellCoord> p in parts) sizes.Add(p.Count + " (с " + p[0] + ")");
                findings.Add(new Finding(Severity.Info, Code, WorldValidator.NameOf(w, id) + ": " + parts.Count +
                    " несвязанные части: " + string.Join(", ", sizes.ToArray()) + ".", parts[0][0], id));
            }
        }
    }
}
