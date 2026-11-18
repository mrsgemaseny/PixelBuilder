using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.PixelArchitect;

namespace MiniLife.WorldStudio.Validation
{
    // Что проверяется. Старые файлы передаются, когда мир собран из них (проверки 1 и 4 смотрят и на них).
    public sealed class ValidationInput
    {
        private readonly List<LegacySource> _legacy = new List<LegacySource>();
        private readonly List<string> _externalIds = new List<string>();

        public ValidationInput(World world)
        {
            if (world == null) throw new ArgumentNullException("world");
            World = world;
        }

        public World World { get; private set; }
        public SettlementReference Reference { get; set; }
        public IList<LegacySource> LegacySources { get { return _legacy; } }
        // id, пришедшие извне мира (например, из второго файла при слиянии), для проверки 12.
        public IList<string> ExternalIds { get { return _externalIds; } }
    }

    // Одна проверка раздела 8 = один класс. Проверка только читает данные (валидатор ничего не исправляет).
    public interface IWorldCheck
    {
        string Code { get; }
        string Title { get; }
        void Run(ValidationInput input, List<Finding> findings);
    }

    public static class WorldValidator
    {
        public static IList<IWorldCheck> AllChecks()
        {
            return new List<IWorldCheck>
            {
                new Checks.UnknownValueCheck(), new Checks.SharedValueCheck(), new Checks.LandWithoutOwnerCheck(),
                new Checks.TotalBlocksCheck(), new Checks.ArmyMarksCheck(), new Checks.VillageMarksCheck(),
                new Checks.SettlementTableCheck(), new Checks.RelationsCheck(), new Checks.ConnectivityCheck(),
                new Checks.ArmyOutsideWildlandsCheck(), new Checks.ArmyRouteCheck(), new Checks.DuplicateIdsCheck(),
                new Checks.OutOfGridCheck(), new Checks.SeaCheck()
            }.AsReadOnly();
        }

        public static List<Finding> Validate(ValidationInput input)
        {
            var findings = new List<Finding>();
            foreach (IWorldCheck check in AllChecks())
            {
                try
                {
                    check.Run(input, findings);
                }
                catch (Exception ex)
                {
                    // Сбой одной проверки не должен прятать остальные находки.
                    findings.Add(new Finding(Severity.Critical, check.Code, "Проверка \"" + check.Title + "\" завершилась ошибкой: " + ex.Message, null, null));
                }
            }
            findings.Sort(Compare);
            return findings;
        }

        private static int Compare(Finding a, Finding b)
        {
            int c = ((int)a.Severity).CompareTo((int)b.Severity);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Check, b.Check);
            if (c != 0) return c;
            if (a.Cell.HasValue != b.Cell.HasValue) return a.Cell.HasValue ? 1 : -1;
            if (a.Cell.HasValue) { c = a.Cell.Value.CompareTo(b.Cell.Value); if (c != 0) return c; }
            c = string.CompareOrdinal(a.EntityId ?? "", b.EntityId ?? "");
            return c != 0 ? c : string.CompareOrdinal(a.Message, b.Message);
        }

        // Компоненты 4-связности: клетки, соединённые по стороне.
        public static List<List<CellCoord>> Components(IEnumerable<CellCoord> cells)
        {
            var set = new HashSet<CellCoord>(cells);
            var sorted = new List<CellCoord>(set);
            sorted.Sort();
            var seen = new HashSet<CellCoord>();
            var result = new List<List<CellCoord>>();
            foreach (CellCoord start in sorted)
            {
                if (seen.Contains(start)) continue;
                var comp = new List<CellCoord>();
                var stack = new Stack<CellCoord>();
                stack.Push(start);
                seen.Add(start);
                while (stack.Count > 0)
                {
                    CellCoord c = stack.Pop();
                    comp.Add(c);
                    foreach (CellCoord n in new[] { new CellCoord(c.X + 1, c.Y), new CellCoord(c.X - 1, c.Y), new CellCoord(c.X, c.Y + 1), new CellCoord(c.X, c.Y - 1) })
                        if (set.Contains(n) && seen.Add(n)) stack.Push(n);
                }
                comp.Sort();
                result.Add(comp);
            }
            return result;
        }

        public static string NameOf(World w, string id)
        {
            StateEntity s = w.FindState(id);
            return s != null ? s.Name + " (" + id + ")" : id;
        }
    }
}
