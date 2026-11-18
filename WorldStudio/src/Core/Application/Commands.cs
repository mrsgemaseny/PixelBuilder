using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Application
{
    // Обратимая правка мира (раздел 9: паттерн Command, каждая команда обратима).
    public interface IWorldCommand
    {
        string Description { get; }
        void Execute(World world);
        void Undo(World world);
    }

    // История правок. Глубина 500 шагов (требование - не меньше 200).
    public sealed class CommandHistory
    {
        public const int DefaultLimit = 500;

        private readonly LinkedList<IWorldCommand> _undo = new LinkedList<IWorldCommand>();
        private readonly Stack<IWorldCommand> _redo = new Stack<IWorldCommand>();
        private readonly int _limit;

        public CommandHistory() : this(DefaultLimit) { }

        public CommandHistory(int limit)
        {
            if (limit < 1) throw new ArgumentOutOfRangeException("limit");
            _limit = limit;
        }

        public event Action Changed;

        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }
        public int UndoCount { get { return _undo.Count; } }
        public string UndoDescription { get { return CanUndo ? _undo.Last.Value.Description : null; } }
        public string RedoDescription { get { return CanRedo ? _redo.Peek().Description : null; } }
        // Растёт с каждой правкой, отменой и повтором: по нему документ понимает, есть ли несохранённое.
        public long Revision { get; private set; }

        public void Execute(World world, IWorldCommand command)
        {
            if (command == null) return;
            command.Execute(world);
            _undo.AddLast(command);
            if (_undo.Count > _limit) _undo.RemoveFirst();
            _redo.Clear();
            Revision++;
            Raise();
        }

        public bool Undo(World world)
        {
            if (!CanUndo) return false;
            IWorldCommand command = _undo.Last.Value;
            _undo.RemoveLast();
            command.Undo(world);
            _redo.Push(command);
            Revision++;
            Raise();
            return true;
        }

        public bool Redo(World world)
        {
            if (!CanRedo) return false;
            IWorldCommand command = _redo.Pop();
            command.Execute(world);
            _undo.AddLast(command);
            Revision++;
            Raise();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            Raise();
        }

        private void Raise()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }
    }

    public sealed class CompositeCommand : IWorldCommand
    {
        private readonly List<IWorldCommand> _parts;

        public CompositeCommand(string description, IEnumerable<IWorldCommand> parts)
        {
            Description = description;
            _parts = new List<IWorldCommand>(parts);
        }

        public string Description { get; private set; }
        public int Count { get { return _parts.Count; } }

        public void Execute(World world)
        {
            foreach (IWorldCommand c in _parts) c.Execute(world);
        }

        public void Undo(World world)
        {
            for (int i = _parts.Count - 1; i >= 0; i--) _parts[i].Undo(world);
        }
    }

    // Замена одного значения: что было - запоминается при создании команды.
    public sealed class PropertyCommand<T> : IWorldCommand
    {
        private readonly Func<World, T> _get;
        private readonly Action<World, T> _set;
        private readonly T _after;
        private T _before;
        private bool _captured;

        public PropertyCommand(string description, Func<World, T> get, Action<World, T> set, T after)
        {
            Description = description;
            _get = get;
            _set = set;
            _after = after;
        }

        public string Description { get; private set; }

        public void Execute(World world)
        {
            if (!_captured) { _before = _get(world); _captured = true; }
            _set(world, _after);
        }

        public void Undo(World world) { _set(world, _before); }
    }

    // Снимок клетки: Exists = false значит "не суша".
    public struct CellState
    {
        public bool Exists;
        public string Landscape;
        public string Owner;
        public string Notes;

        public static CellState Of(CellData data)
        {
            if (data == null) return new CellState();
            return new CellState { Exists = true, Landscape = data.Landscape, Owner = data.Owner, Notes = data.Notes };
        }

        public bool SameAs(CellState o)
        {
            return Exists == o.Exists && (!Exists || (Landscape == o.Landscape && Owner == o.Owner && Notes == o.Notes));
        }
    }

    public sealed class CellChange
    {
        public CellChange(CellCoord cell, CellState before, CellState after)
        {
            Cell = cell;
            Before = before;
            After = after;
        }

        public CellCoord Cell { get; private set; }
        public CellState Before { get; private set; }
        public CellState After { get; private set; }
    }

    // Изменение набора клеток одним шагом истории (мазок кистью, прямоугольник, заливка).
    public sealed class CellsCommand : IWorldCommand
    {
        private readonly List<CellChange> _changes;

        public CellsCommand(string description, IEnumerable<CellChange> changes)
        {
            Description = description;
            _changes = new List<CellChange>(changes);
        }

        public string Description { get; private set; }
        public int Count { get { return _changes.Count; } }
        public IList<CellChange> Changes { get { return _changes.AsReadOnly(); } }

        public void Execute(World world) { foreach (CellChange c in _changes) Apply(world, c.Cell, c.After); }

        public void Undo(World world)
        {
            for (int i = _changes.Count - 1; i >= 0; i--) Apply(world, _changes[i].Cell, _changes[i].Before);
        }

        private static void Apply(World world, CellCoord cell, CellState state)
        {
            if (!state.Exists) { world.RemoveCell(cell); return; }
            CellData data = world.GetOrCreateCell(cell);
            data.Landscape = state.Landscape;
            data.Owner = state.Owner;
            data.Notes = state.Notes ?? "";
        }
    }

    // Добавление сущности; undo убирает её, но id остаётся выданным (раздел 7: id не переиспользуются).
    public sealed class AddEntityCommand : IWorldCommand
    {
        private readonly object _entity;
        private readonly int _index;
        private RelationsMatrix _relationsBefore;
        private bool _first = true;

        public AddEntityCommand(string description, object entity, int index)
        {
            Description = description;
            _entity = entity;
            _index = index;
        }

        public string Description { get; private set; }

        public void Execute(World world)
        {
            if (_first)
            {
                _relationsBefore = world.Relations;
                Insert(world);
                _first = false;
            }
            else world.RestoreEntity(_entity, _index); // redo: тот же объект с тем же id
            if (_entity is StateEntity) world.Relations = RelationsFor(world, _relationsBefore);
        }

        public void Undo(World world)
        {
            world.RemoveEntity(EntityIds.Of(_entity));
            if (_entity is StateEntity) world.Relations = _relationsBefore;
        }

        private void Insert(World world)
        {
            if (_entity is StateEntity) world.InsertState(Index(_index, world.States.Count), (StateEntity)_entity);
            else if (_entity is WildlandEntity) world.InsertWildland(Index(_index, world.Wildlands.Count), (WildlandEntity)_entity);
            else if (_entity is ArmyEntity) world.InsertArmy(Index(_index, world.Armies.Count), (ArmyEntity)_entity);
            else world.InsertObject(Index(_index, world.Objects.Count), (WorldObject)_entity);
        }

        private static int Index(int i, int count) { return i < 0 || i > count ? count : i; }

        // Матрица следует за реестром (проверка 8): порядок и состав - как список государств.
        internal static RelationsMatrix RelationsFor(World world, RelationsMatrix basis)
        {
            var order = new List<string>();
            foreach (StateEntity s in world.States) order.Add(s.Id);
            if (basis == null) return new RelationsMatrix(order);
            return basis.Reordered(order);
        }
    }

    // Удаление сущности без ссылок на неё. Состояние для отмены запоминается при выполнении.
    public sealed class RemoveEntityCommand : IWorldCommand
    {
        private readonly string _id;
        private object _entity;
        private int _index;
        private RelationsMatrix _relationsBefore;

        public RemoveEntityCommand(string description, string id)
        {
            Description = description;
            _id = id;
        }

        public string Description { get; private set; }

        public void Execute(World world)
        {
            _relationsBefore = world.Relations;
            _entity = EntityIds.Find(world, _id, out _index);
            if (_entity == null) throw new DomainException("Сущность " + _id + " не найдена.");
            world.RemoveEntity(_id);
            if (_entity is StateEntity) world.Relations = AddEntityCommand.RelationsFor(world, _relationsBefore);
        }

        public void Undo(World world)
        {
            world.RestoreEntity(_entity, _index);
            if (_entity is StateEntity) world.Relations = _relationsBefore;
        }
    }

    public static class EntityIds
    {
        public static string Of(object entity)
        {
            if (entity is StateEntity) return ((StateEntity)entity).Id;
            if (entity is WildlandEntity) return ((WildlandEntity)entity).Id;
            if (entity is ArmyEntity) return ((ArmyEntity)entity).Id;
            return ((WorldObject)entity).Id;
        }

        public static object Find(World world, string id, out int index)
        {
            if ((index = world.IndexOfState(id)) >= 0) return world.States[index];
            if ((index = world.IndexOfWildland(id)) >= 0) return world.Wildlands[index];
            if ((index = world.IndexOfArmy(id)) >= 0) return world.Armies[index];
            if ((index = world.IndexOfObject(id)) >= 0) return world.Objects[index];
            return null;
        }
    }
}
