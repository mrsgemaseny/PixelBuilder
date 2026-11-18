using System.Collections.Generic;

namespace MiniLife.WorldStudio.Domain
{
    public enum Severity { Critical, Warning, Info }

    // Находка валидатора или импорта. Текст на русском, клетка или id сущности для перехода к месту.
    // Находка ничего не исправляет (раздел 8).
    public sealed class Finding
    {
        public Finding(Severity severity, string check, string message, CellCoord? cell, string entityId)
        {
            Severity = severity;
            Check = check ?? "";
            Message = message ?? "";
            Cell = cell;
            EntityId = entityId;
        }

        public Severity Severity { get; private set; }
        // Код проверки: "01_unknown_value" ... "14_sea", "import".
        public string Check { get; private set; }
        public string Message { get; private set; }
        public CellCoord? Cell { get; private set; }
        public string EntityId { get; private set; }

        public static string Marker(Severity severity)
        {
            switch (severity)
            {
                case Severity.Critical: return "[CRITICAL]";
                case Severity.Warning: return "[WARNING]";
                default: return "[INFO]";
            }
        }

        public override string ToString()
        {
            string where = Cell.HasValue ? " " + Cell.Value : "";
            string who = string.IsNullOrEmpty(EntityId) ? "" : " " + EntityId;
            return Marker(Severity) + " " + Check + where + who + ": " + Message;
        }

        public static int Count(IEnumerable<Finding> findings, Severity severity)
        {
            int n = 0;
            foreach (Finding f in findings) if (f.Severity == severity) n++;
            return n;
        }
    }
}
