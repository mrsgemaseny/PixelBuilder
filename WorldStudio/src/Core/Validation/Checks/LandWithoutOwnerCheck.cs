using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 3. Клетка суши без государства и без группы диких земель. [WARNING]
    public sealed class LandWithoutOwnerCheck : IWorldCheck
    {
        public string Code { get { return "03_land_without_owner"; } }
        public string Title { get { return "Суша без владельца"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            foreach (CellData c in input.World.SortedCells())
                if (c.Owner == null)
                    findings.Add(new Finding(Severity.Warning, Code, "Клетка суши без государства и без группы диких земель.", c.Cell, null));
        }
    }
}
