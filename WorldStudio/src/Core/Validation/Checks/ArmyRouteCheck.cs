using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 11. Соседние точки маршрута армии должны быть соседними клетками (по стороне). [WARNING]
    public sealed class ArmyRouteCheck : IWorldCheck
    {
        public string Code { get { return "11_army_route"; } }
        public string Title { get { return "Маршрут армии"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            foreach (ArmyEntity a in input.World.Armies)
                for (int i = 1; i < a.Route.Count; i++)
                    if (!a.Route[i - 1].IsOrthogonalNeighbor(a.Route[i]))
                        findings.Add(new Finding(Severity.Warning, Code, "Точки маршрута #" + (i - 1) + " " + a.Route[i - 1] + " и #" + i + " " +
                            a.Route[i] + " не соседние клетки.", a.Route[i], a.Id));
        }
    }
}
