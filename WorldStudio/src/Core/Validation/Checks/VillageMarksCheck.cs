using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 6. Метки Д - независимые поселения без лорда: в сверку с таблицей автора (по государствам) не входят. [INFO]
    public sealed class VillageMarksCheck : IWorldCheck
    {
        public string Code { get { return "06_village_marks"; } }
        public string Title { get { return "Независимые поселения"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            int count = 0;
            foreach (WorldObject o in input.World.Objects) if (o.Type == WorldObjectType.VillageMark) count++;
            if (count > 0)
                findings.Add(new Finding(Severity.Info, Code, "Независимых поселений (метки Д): " + count + ". Без лорда, в сверку с таблицей автора не входят.", null, null));
        }
    }
}
