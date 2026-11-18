using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.PixelArchitect;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 4. total_blocks старого файла не совпадает с суммой клеток и объектов. [WARNING]
    public sealed class TotalBlocksCheck : IWorldCheck
    {
        public string Code { get { return "04_total_blocks"; } }
        public string Title { get { return "Контроль total_blocks"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            foreach (LegacySource src in input.LegacySources)
            {
                int? declared = src.Document.DeclaredTotalBlocks;
                if (!declared.HasValue) continue;
                int actual = src.Document.CountStandardBlocks();
                if (declared.Value != actual)
                    findings.Add(new Finding(Severity.Warning, Code, "\"" + src.Name + "\": total_blocks = " + declared.Value +
                        ", а клеток и объектов " + actual + ".", null, null));
            }
        }
    }
}
