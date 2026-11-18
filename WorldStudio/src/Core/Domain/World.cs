using System;
using System.Collections.Generic;

namespace MiniLife.WorldStudio.Domain
{
    public sealed class WorldMeta
    {
        public WorldMeta()
        {
            Name = "";
            CreatedAt = "";
            UpdatedAt = "";
            ToolVersion = "";
        }

        public string Name { get; set; }
        // ISO 8601 UTC, хранится строкой: формат времени не должен меняться при круговом проходе.
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string ToolVersion { get; set; }
    }

    // Клетка суши. Клетка есть в мире = клетка является сушей; моря нет (закон 6).
    public sealed class CellData
    {
        public CellData(CellCoord cell)
        {
            Cell = cell;
            Notes = "";
        }

        public CellCoord Cell { get; private set; }
        // id типа ландшафта из легенды (plains, forest, mountains, sands); null если не задан.
        public string Landscape { get; set; }
        // state.* или wild.*; null если владелец не задан.
        public string Owner { get; set; }
        public string Notes { get; set; }
    }

    // Корень модели world.json, раздел 7.
    public sealed class World
    {
        public const int CurrentSchemaVersion = 1;

        private readonly GridSpec _grid;
        private readonly Dictionary<CellCoord, CellData> _cells = new Dictionary<CellCoord, CellData>();
        private readonly List<StateEntity> _states = new List<StateEntity>();
        private readonly List<WildlandEntity> _wildlands = new List<WildlandEntity>();
        private readonly List<ArmyEntity> _armies = new List<ArmyEntity>();
        private readonly List<WorldObject> _objects = new List<WorldObject>();
        // Все id, когда-либо выданные в этом мире. Удалённый id остаётся здесь навсегда (раздел 7).
        private readonly HashSet<string> _issuedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _liveIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, string> _extraRaw = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public World(GridSpec grid)
        {
            if (grid == null) throw new ArgumentNullException("grid");
            _grid = grid;
            SchemaVersion = CurrentSchemaVersion;
            Meta = new WorldMeta();
            Legend = new Legend();
            LoreRaw = "{\"links\":[],\"timeline\":[]}";
            BuildingsRaw = "{}";
        }

        public int SchemaVersion { get; set; }
        // Зарезервировано за игрой: исходный JSON значения или null. Инструмент не меняет.
        public string GeneratorVersionRaw { get; set; }
        public WorldMeta Meta { get; private set; }
        public GridSpec Grid { get { return _grid; } }
        public Legend Legend { get; set; }
        public RelationsMatrix Relations { get; set; }
        // Модули L3/L4 и L1 ещё не сделаны: содержимое хранится исходным JSON и переносится без изменений.
        public string LoreRaw { get; set; }
        public string BuildingsRaw { get; set; }
        // Поля корня world.json, которых эта версия не знает (сохраняются при записи).
        public IDictionary<string, string> ExtraRaw { get { return _extraRaw; } }

        public IList<StateEntity> States { get { return _states.AsReadOnly(); } }
        public IList<WildlandEntity> Wildlands { get { return _wildlands.AsReadOnly(); } }
        public IList<ArmyEntity> Armies { get { return _armies.AsReadOnly(); } }
        public IList<WorldObject> Objects { get { return _objects.AsReadOnly(); } }
        public ICollection<CellData> Cells { get { return _cells.Values; } }
        public int CellCount { get { return _cells.Count; } }

        // Выданные, но уже удалённые id (для записи в world.json).
        public List<string> RetiredIds
        {
            get
            {
                var list = new List<string>();
                foreach (string id in _issuedIds) if (!_liveIds.Contains(id)) list.Add(id);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        public bool IsIdLive(string id) { return _liveIds.Contains(id); }
        public bool WasIdIssued(string id) { return _issuedIds.Contains(id); }

        // ---------- сущности ----------

        public void AddState(StateEntity state) { InsertState(_states.Count, state); }
        public void InsertState(int index, StateEntity state) { Claim(state.Id); _states.Insert(index, state); }
        public void AddWildland(WildlandEntity w) { InsertWildland(_wildlands.Count, w); }
        public void InsertWildland(int index, WildlandEntity w) { Claim(w.Id); _wildlands.Insert(index, w); }
        public void AddArmy(ArmyEntity army) { InsertArmy(_armies.Count, army); }
        public void InsertArmy(int index, ArmyEntity army) { Claim(army.Id); _armies.Insert(index, army); }

        public StateEntity FindState(string id) { return _states.Find(s => s.Id == id); }
        public WildlandEntity FindWildland(string id) { return _wildlands.Find(w => w.Id == id); }
        public ArmyEntity FindArmy(string id) { return _armies.Find(a => a.Id == id); }
        public WorldObject FindObject(string id) { return _objects.Find(o => o.Id == id); }

        public int IndexOfState(string id) { return _states.FindIndex(s => s.Id == id); }
        public int IndexOfWildland(string id) { return _wildlands.FindIndex(w => w.Id == id); }
        public int IndexOfArmy(string id) { return _armies.FindIndex(a => a.Id == id); }
        public int IndexOfObject(string id) { return _objects.FindIndex(o => o.Id == id); }

        // Владелец клетки или объекта: государство или группа диких земель.
        public bool IsOwnerId(string id)
        {
            return id != null && (FindState(id) != null || FindWildland(id) != null);
        }

        // Цвет отображения сущности; null если сущность не найдена. Цвет хранится только в реестре.
        public RgbColor? DisplayColorOf(string id)
        {
            StateEntity s = FindState(id);
            if (s != null) return s.Color;
            WildlandEntity w = FindWildland(id);
            if (w != null) return w.Color;
            return null;
        }

        // Ссылки на сущность из клеток, объектов и матрицы. Удалять сущность со ссылками нельзя.
        public int CountReferences(string id)
        {
            int n = 0;
            foreach (CellData c in _cells.Values) if (c.Owner == id) n++;
            foreach (WorldObject o in _objects) if (o.Owner == id) n++;
            if (Relations != null && Relations.IndexOf(id) >= 0) n++;
            return n;
        }

        public void RemoveEntity(string id)
        {
            if (!_liveIds.Contains(id)) throw new DomainException("Сущность " + id + " не найдена.");
            int i;
            if ((i = IndexOfState(id)) >= 0) _states.RemoveAt(i);
            else if ((i = IndexOfWildland(id)) >= 0) _wildlands.RemoveAt(i);
            else if ((i = IndexOfArmy(id)) >= 0) _armies.RemoveAt(i);
            else if ((i = IndexOfObject(id)) >= 0) _objects.RemoveAt(i);
            else throw new DomainException("Сущность " + id + " не найдена.");
            _liveIds.Remove(id); // в _issuedIds остаётся: повторная выдача запрещена
        }

        // Возврат удалённой сущности (undo). Тот же id разрешён только ей самой.
        public void RestoreEntity(object entity, int index)
        {
            string id = IdOf(entity);
            if (_liveIds.Contains(id)) throw new DomainException("id \"" + id + "\" уже занят.");
            if (!_issuedIds.Contains(id)) throw new DomainException("id \"" + id + "\" не выдавался в этом мире.");
            _liveIds.Add(id);
            if (entity is StateEntity) _states.Insert(Clamp(index, _states.Count), (StateEntity)entity);
            else if (entity is WildlandEntity) _wildlands.Insert(Clamp(index, _wildlands.Count), (WildlandEntity)entity);
            else if (entity is ArmyEntity) _armies.Insert(Clamp(index, _armies.Count), (ArmyEntity)entity);
            else _objects.Insert(Clamp(index, _objects.Count), (WorldObject)entity);
        }

        // Отмена создания (undo добавления): id выдан и остаётся выданным, но сущности больше нет.
        public void RetractEntity(string id) { RemoveEntity(id); }

        private static int Clamp(int index, int count) { return index < 0 || index > count ? count : index; }

        private static string IdOf(object entity)
        {
            if (entity is StateEntity) return ((StateEntity)entity).Id;
            if (entity is WildlandEntity) return ((WildlandEntity)entity).Id;
            if (entity is ArmyEntity) return ((ArmyEntity)entity).Id;
            if (entity is WorldObject) return ((WorldObject)entity).Id;
            throw new ArgumentException("entity");
        }

        // ---------- объекты ----------

        public void AddObject(WorldObject obj) { InsertObject(_objects.Count, obj); }

        public void InsertObject(int index, WorldObject obj)
        {
            if (!_grid.Contains(obj.Cell))
                throw new DomainException("Объект " + obj.Id + " вне сетки " + _grid.Width + " x " + _grid.Height + ": " + obj.Cell + ".");
            Claim(obj.Id);
            _objects.Insert(Clamp(index, _objects.Count), obj);
        }

        public bool RemoveObject(string id)
        {
            if (IndexOfObject(id) < 0) return false;
            RemoveEntity(id);
            return true;
        }

        public WorldObject ObjectAt(CellCoord cell)
        {
            return _objects.Find(o => o.Cell == cell);
        }

        // Восстановление id, выданных ранее и уже удалённых (при чтении world.json).
        public void RestoreRetiredId(string id)
        {
            EntityId.Require(id, null);
            if (_liveIds.Contains(id)) throw new DomainException("id \"" + id + "\" занят живой сущностью.");
            _issuedIds.Add(id);
        }

        // ---------- клетки ----------

        public CellData GetOrCreateCell(CellCoord cell)
        {
            if (!_grid.Contains(cell))
                throw new DomainException("Клетка " + cell + " вне сетки " + _grid.Width + " x " + _grid.Height + ".");
            CellData data;
            if (!_cells.TryGetValue(cell, out data))
            {
                data = new CellData(cell);
                _cells.Add(cell, data);
            }
            return data;
        }

        public CellData FindCell(CellCoord cell)
        {
            CellData data;
            return _cells.TryGetValue(cell, out data) ? data : null;
        }

        public bool IsLand(CellCoord cell) { return _cells.ContainsKey(cell); }

        // Клетка перестаёт быть сушей. Вызывается только командой с undo.
        public bool RemoveCell(CellCoord cell) { return _cells.Remove(cell); }

        public void PutCell(CellData data)
        {
            if (!_grid.Contains(data.Cell)) throw new DomainException("Клетка " + data.Cell + " вне сетки.");
            _cells[data.Cell] = data;
        }

        public List<CellData> SortedCells()
        {
            var list = new List<CellData>(_cells.Values);
            list.Sort((a, b) => a.Cell.CompareTo(b.Cell));
            return list;
        }

        // ---------- id ----------

        // Первый свободный id вида "<kind>.<prefix>_<n>", никогда не выданный раньше.
        public string NextId(string kind, string prefix)
        {
            for (int n = 1; ; n++)
            {
                string id = kind + "." + prefix + "_" + n;
                if (!_issuedIds.Contains(id)) return id;
            }
        }

        public IEnumerable<string> LiveIds { get { return _liveIds; } }

        private void Claim(string id)
        {
            if (_issuedIds.Contains(id))
                throw new DomainException(_liveIds.Contains(id)
                    ? "Дубликат id \"" + id + "\"."
                    : "id \"" + id + "\" уже использовался и был удалён, повторно его выдавать нельзя.");
            _issuedIds.Add(id);
            _liveIds.Add(id);
        }
    }
}
