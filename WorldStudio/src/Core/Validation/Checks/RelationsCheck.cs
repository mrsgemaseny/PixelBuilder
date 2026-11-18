using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 8. Матрица отношений: значения от -100 до 100, диагональ пустая,
    // порядок государств совпадает с реестром. [CRITICAL]
    public sealed class RelationsCheck : IWorldCheck
    {
        public string Code { get { return "08_relations"; } }
        public string Title { get { return "Матрица отношений"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            RelationsMatrix m = w.Relations;
            if (m == null)
            {
                if (w.States.Count > 0)
                    findings.Add(new Finding(Severity.Critical, Code, "Матрицы отношений нет, а государств в реестре " + w.States.Count + ".", null, null));
                return;
            }
            var registry = new List<string>();
            foreach (StateEntity s in w.States) registry.Add(s.Id);
            if (string.Join(",", registry.ToArray()) != string.Join(",", new List<string>(m.Order).ToArray()))
                findings.Add(new Finding(Severity.Critical, Code, "Порядок государств в матрице (" + string.Join(", ", new List<string>(m.Order).ToArray()) +
                    ") не совпадает с реестром (" + string.Join(", ", registry.ToArray()) + ").", null, null));
            int empty = 0;
            for (int i = 0; i < m.Size; i++)
                for (int j = 0; j < m.Size; j++)
                {
                    int? v = m.Get(i, j);
                    if (i == j)
                    {
                        if (v.HasValue) findings.Add(new Finding(Severity.Critical, Code, "Диагональ не пустая: " + m.Order[i] + " = " + v.Value + ".", null, m.Order[i]));
                        continue;
                    }
                    if (!v.HasValue) { empty++; continue; }
                    if (v.Value < RelationsMatrix.MinValue || v.Value > RelationsMatrix.MaxValue)
                        findings.Add(new Finding(Severity.Critical, Code, m.Order[i] + " -> " + m.Order[j] + " = " + v.Value +
                            ", допустимо от " + RelationsMatrix.MinValue + " до " + RelationsMatrix.MaxValue + ".", null, m.Order[i]));
                }
            if (empty > 0)
                findings.Add(new Finding(Severity.Info, Code, "Не заполнено значений отношений: " + empty + ".", null, null));
        }
    }
}
