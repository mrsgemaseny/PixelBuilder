using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 5. Метки Г (армии): число меток против числа бродячих армий, связь метки с армией. [WARNING]
    // Уничтоженная армия на карте не отмечается - это напоминание, а не ошибка. [INFO]
    public sealed class ArmyMarksCheck : IWorldCheck
    {
        public string Code { get { return "05_army_marks"; } }
        public string Title { get { return "Метки Г и армии"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            var marks = new List<WorldObject>();
            foreach (WorldObject o in w.Objects) if (o.Type == WorldObjectType.ArmyMark) marks.Add(o);
            int roaming = 0;
            foreach (ArmyEntity a in w.Armies) if (a.Status == ArmyStatus.Roaming) roaming++;

            if (marks.Count != roaming)
                findings.Add(new Finding(Severity.Warning, Code, "Меток Г на карте " + marks.Count + ", армий в статусе \"бродит\" " + roaming + ".", null, null));

            foreach (WorldObject mark in marks)
            {
                ArmyEntity army = mark.Owner != null ? w.FindArmy(mark.Owner) : null;
                if (army == null)
                    findings.Add(new Finding(Severity.Warning, Code, "Метка Г не связана ни с одной армией.", mark.Cell, mark.Id));
                else if (army.Status != ArmyStatus.Roaming)
                    findings.Add(new Finding(Severity.Warning, Code, "Метка Г связана с армией " + army.Id + ", которая не бродит.", mark.Cell, army.Id));
                else if (army.Position.HasValue && army.Position.Value != mark.Cell)
                    findings.Add(new Finding(Severity.Warning, Code, "Метка Г стоит не на клетке армии " + army.Id + " " + army.Position.Value + ".", mark.Cell, army.Id));
            }

            foreach (ArmyEntity a in w.Armies)
            {
                if (a.Status == ArmyStatus.Destroyed)
                {
                    findings.Add(new Finding(Severity.Info, Code, "Армия уничтожена" +
                        (a.Position.HasValue ? "" : ", клетка не задана") + "; на карте меткой Г не отмечается.", a.Position, a.Id));
                    continue;
                }
                bool linked = false;
                foreach (WorldObject mark in marks) if (mark.Owner == a.Id) linked = true;
                if (!linked)
                    findings.Add(new Finding(Severity.Warning, Code, "Бродячая армия без метки Г на карте.", a.Position, a.Id));
            }
        }
    }
}
