using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 13. Невидимая стена: ничего не расположено за пределами сетки. [CRITICAL]
    public sealed class OutOfGridCheck : IWorldCheck
    {
        public string Code { get { return "13_out_of_grid"; } }
        public string Title { get { return "Невидимая стена"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            GridSpec g = w.Grid;
            string grid = g.Width + " x " + g.Height;
            foreach (CellData c in w.Cells)
                if (!g.Contains(c.Cell)) findings.Add(new Finding(Severity.Critical, Code, "Клетка за пределами сетки " + grid + ".", c.Cell, null));
            foreach (WorldObject o in w.Objects)
                if (!g.Contains(o.Cell)) findings.Add(new Finding(Severity.Critical, Code, "Объект за пределами сетки " + grid + ".", o.Cell, o.Id));
            foreach (ArmyEntity a in w.Armies)
            {
                if (a.Origin.HasValue && !g.Contains(a.Origin.Value))
                    findings.Add(new Finding(Severity.Critical, Code, "Начальная точка армии за пределами сетки " + grid + ".", a.Origin, a.Id));
                if (a.Position.HasValue && !g.Contains(a.Position.Value))
                    findings.Add(new Finding(Severity.Critical, Code, "Клетка армии за пределами сетки " + grid + ".", a.Position, a.Id));
                for (int i = 0; i < a.Route.Count; i++)
                    if (!g.Contains(a.Route[i]))
                        findings.Add(new Finding(Severity.Critical, Code, "Точка маршрута #" + i + " за пределами сетки " + grid + ".", a.Route[i], a.Id));
            }
        }
    }
}
