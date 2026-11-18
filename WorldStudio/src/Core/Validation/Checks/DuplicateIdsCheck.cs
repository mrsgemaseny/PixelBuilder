using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Validation.Checks
{
    // Проверка 12. Дубликаты id. [CRITICAL]
    // Мир в памяти дубликатов не допускает (World.Claim), world.json с дубликатами не открывается (D-16);
    // проверка страхует сущности целиком и id, пришедшие извне (ValidationInput.ExternalIds).
    public sealed class DuplicateIdsCheck : IWorldCheck
    {
        public string Code { get { return "12_duplicate_ids"; } }
        public string Title { get { return "Дубликаты id"; } }

        public void Run(ValidationInput input, List<Finding> findings)
        {
            World w = input.World;
            var all = new List<string>();
            foreach (StateEntity s in w.States) all.Add(s.Id);
            foreach (WildlandEntity g in w.Wildlands) all.Add(g.Id);
            foreach (ArmyEntity a in w.Armies) all.Add(a.Id);
            foreach (WorldObject o in w.Objects) all.Add(o.Id);
            all.AddRange(input.ExternalIds);
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in all)
            {
                int n;
                counts.TryGetValue(id, out n);
                counts[id] = n + 1;
            }
            foreach (var pair in counts)
                if (pair.Value > 1)
                    findings.Add(new Finding(Severity.Critical, Code, "id встречается " + pair.Value + " раз.", null, pair.Key));
            if (w.Relations != null)
            {
                var seen = new HashSet<string>();
                foreach (string id in w.Relations.Order)
                    if (!seen.Add(id)) findings.Add(new Finding(Severity.Critical, Code, "id повторяется в порядке матрицы отношений.", null, id));
            }
        }
    }
}
