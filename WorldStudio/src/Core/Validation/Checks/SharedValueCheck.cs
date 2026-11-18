using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 2. Одно значение в двух семантиках с разным смыслом: напоминание, что смысл
    // берётся по семантике файла, а не по hex (раздел 5.2). [INFO]
    public sealed class SharedValueCheck : IWorldCheck
    {
        public string Code { get { return "02_shared_value"; } }
        public string Title { get { return "Один цвет в двух слоях с разным смыслом"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            Legend legend = input.World.Legend;
            foreach (var pair in legend.LegacyLandscapeValues)
            {
                string owner;
                if (!legend.LegacyOwnerValues.TryGetValue(pair.Key, out owner)) continue;
                findings.Add(new Finding(Severity.Info, Code, "\"" + pair.Key + "\": в карте ландшафтов это " + pair.Value +
                    ", в политической карте - " + WorldValidator.NameOf(input.World, owner) + ". Смысл определяется семантикой файла.", null, owner));
            }
        }
    }
}
