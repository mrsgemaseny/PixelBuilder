using System.Collections.Generic;

namespace MiniLife.WorldStudio.Domain
{
    // Таблица автора (раздел 5.6): сколько поселений и жителей должно быть у государства.
    // С ней сверяется карта (проверка 7). Числа не исправляются ни картой, ни валидатором.
    public sealed class SettlementRow
    {
        public string StateId { get; set; }
        public int Castles { get; set; }
        // Города вместе с крепостями.
        public int Cities { get; set; }
        // Деревни с кратностью (village_2 = 2, village_3 = 3).
        public int Villages { get; set; }
        public int Population { get; set; }
    }

    public sealed class SettlementReference
    {
        private readonly List<SettlementRow> _rows = new List<SettlementRow>();

        public SettlementReference()
        {
            Source = "";
        }

        public string Source { get; set; }
        public IList<SettlementRow> Rows { get { return _rows; } }

        public SettlementRow Find(string stateId)
        {
            foreach (SettlementRow row in _rows)
                if (row.StateId == stateId) return row;
            return null;
        }
    }
}
