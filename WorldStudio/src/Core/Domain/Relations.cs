using System.Collections.Generic;

namespace MiniLife.WorldStudio.Domain
{
    // Матрица отношений N x N, не симметрична. Строка - как государство относится к остальным.
    // Диагональ всегда null. Диапазон значений проверяет валидатор (проверка 8), а не сеттер:
    // данные автора должны загружаться даже с ошибками, чтобы их можно было показать.
    public sealed class RelationsMatrix
    {
        public const int MinValue = -100;
        public const int MaxValue = 100;

        private readonly List<string> _order;
        private readonly int?[,] _values;

        public RelationsMatrix(IList<string> order)
        {
            _order = new List<string>(order);
            var seen = new HashSet<string>();
            foreach (string id in _order)
            {
                EntityId.Require(id, EntityId.State);
                if (!seen.Add(id)) throw new DomainException("Государство \"" + id + "\" повторяется в порядке матрицы.");
            }
            _values = new int?[_order.Count, _order.Count];
        }

        public IList<string> Order { get { return _order.AsReadOnly(); } }
        public int Size { get { return _order.Count; } }

        public int? Get(int from, int to)
        {
            return _values[from, to];
        }

        public void Set(int from, int to, int? value)
        {
            if (from == to && value.HasValue)
                throw new DomainException("Диагональ матрицы отношений должна быть пустой.");
            _values[from, to] = value;
        }

        public int IndexOf(string stateId)
        {
            return _order.IndexOf(stateId);
        }

        // Новая матрица с другим порядком или составом государств; значения переносятся по id,
        // для новых государств ячейки пустые. Нужна при добавлении и удалении государства в реестре.
        public RelationsMatrix Reordered(IList<string> newOrder)
        {
            var result = new RelationsMatrix(newOrder);
            for (int i = 0; i < result.Size; i++)
            {
                int oi = IndexOf(result._order[i]);
                if (oi < 0) continue;
                for (int j = 0; j < result.Size; j++)
                {
                    int oj = IndexOf(result._order[j]);
                    if (oj >= 0 && i != j) result._values[i, j] = _values[oi, oj];
                }
            }
            return result;
        }

        public RelationsMatrix Clone() { return Reordered(_order); }

        public bool SameValues(RelationsMatrix other)
        {
            if (other == null || other.Size != Size) return false;
            for (int i = 0; i < Size; i++)
            {
                if (other._order[i] != _order[i]) return false;
                for (int j = 0; j < Size; j++)
                    if (other._values[i, j] != _values[i, j]) return false;
            }
            return true;
        }
    }
}
