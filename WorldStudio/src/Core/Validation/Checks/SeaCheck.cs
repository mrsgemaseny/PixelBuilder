using System.Collections.Generic;
using System.Text.RegularExpressions;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 14. Любое упоминание моря в типах данных. Моря в мире нет (закон 6), возможны только озёра. [CRITICAL]
    // Смотрятся идентификаторы и названия типов, а не заметки: в заметках слово может быть частью другого смысла.
    public sealed class SeaCheck : IWorldCheck
    {
        private static readonly Regex Sea = new Regex(@"(^|[^\p{L}])(sea|seas|ocean|море|моря|морской|океан)([^\p{L}]|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public string Code { get { return "14_sea"; } }
        public string Title { get { return "Море в данных"; } }

        public static bool MentionsSea(string text)
        {
            return !string.IsNullOrEmpty(text) && Sea.IsMatch(text.Replace('_', ' '));
        }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            foreach (LandscapeType t in w.Legend.LandscapeTypes)
                if (MentionsSea(t.Id) || MentionsSea(t.Name))
                    findings.Add(new Finding(Severity.Critical, Code, "Тип ландшафта \"" + t.Id + "\" (" + t.Name + ") - море. Моря в мире нет.", null, null));
            foreach (var pair in w.Legend.LegacyLandscapeValues)
                if (MentionsSea(pair.Key) || MentionsSea(pair.Value))
                    findings.Add(new Finding(Severity.Critical, Code, "Легенда: \"" + pair.Key + "\" -> \"" + pair.Value + "\" - море. Моря в мире нет.", null, null));
            foreach (CellData c in w.SortedCells())
                if (MentionsSea(c.Landscape))
                    findings.Add(new Finding(Severity.Critical, Code, "Ландшафт клетки \"" + c.Landscape + "\" - море. Моря в мире нет.", c.Cell, null));
        }
    }
}
