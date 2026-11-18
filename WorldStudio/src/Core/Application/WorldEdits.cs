using System;
using System.Collections.Generic;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Application
{
    // Слои редактора глобальной карты (M1). Политика и дикие земли - это один атрибут клетки (владелец),
    // но разные слои: на каждом можно поставить только свой вид владельца и заблокировать его отдельно.
    public enum EditLayer { Landscape, Political, Wildlands, Objects }

    // Фабрика команд правки. Бизнес-правила здесь, а не в UI (раздел 9).
    public static class WorldEdits
    {
        // Кисть, прямоугольник, заливка: value = id типа ландшафта / государства / группы, null = ластик.
        // Ландшафт на не-суше создаёт сушу. Владельца можно поставить только на сушу.
        // Ластик слоя стирает только значения своего слоя (ластик политики не трогает дикие земли).
        public static CellsCommand Paint(World w, EditLayer layer, string value, IEnumerable<CellCoord> cells, string description)
        {
            if (layer == EditLayer.Objects) throw new ArgumentException("Объекты ставятся через PlaceObject.");
            RequireValue(w, layer, value);
            var changes = new List<CellChange>();
            var seen = new HashSet<CellCoord>();
            foreach (CellCoord cell in cells)
            {
                if (!w.Grid.Contains(cell) || !seen.Add(cell)) continue;
                CellState before = CellState.Of(w.FindCell(cell));
                CellState after = before;
                if (layer == EditLayer.Landscape)
                {
                    if (!before.Exists)
                    {
                        if (value == null) continue;
                        after = new CellState { Exists = true, Notes = "" };
                    }
                    after.Landscape = value;
                }
                else
                {
                    if (!before.Exists) continue;
                    if (value == null)
                    {
                        if (before.Owner == null || !OwnerBelongsTo(w, layer, before.Owner)) continue;
                        after.Owner = null;
                    }
                    else after.Owner = value;
                }
                if (!after.SameAs(before)) changes.Add(new CellChange(cell, before, after));
            }
            return new CellsCommand(description ?? DescribePaint(layer, value), changes);
        }

        // Заливка: связная (по стороне) область суши с тем же значением слоя, что в стартовой клетке.
        public static CellsCommand Fill(World w, EditLayer layer, CellCoord start, string value)
        {
            if (!w.IsLand(start)) return new CellsCommand("Заливка", new CellChange[0]);
            string source = ValueAt(w, layer, start);
            var region = new List<CellCoord>();
            var seen = new HashSet<CellCoord> { start };
            var queue = new Queue<CellCoord>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                CellCoord c = queue.Dequeue();
                region.Add(c);
                foreach (CellCoord n in new[] { new CellCoord(c.X + 1, c.Y), new CellCoord(c.X - 1, c.Y), new CellCoord(c.X, c.Y + 1), new CellCoord(c.X, c.Y - 1) })
                    if (w.IsLand(n) && !seen.Contains(n) && ValueAt(w, layer, n) == source)
                    {
                        seen.Add(n);
                        queue.Enqueue(n);
                    }
            }
            return Paint(w, layer, value, region, "Заливка: " + DescribePaint(layer, value));
        }

        public static IEnumerable<CellCoord> Rectangle(CellCoord a, CellCoord b)
        {
            int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    yield return new CellCoord(x, y);
        }

        // Значение слоя в клетке (для пипетки и заливки). Для политики видны только государства, для диких земель - группы.
        public static string ValueAt(World w, EditLayer layer, CellCoord cell)
        {
            CellData c = w.FindCell(cell);
            if (c == null) return null;
            switch (layer)
            {
                case EditLayer.Landscape: return c.Landscape;
                case EditLayer.Political: return c.Owner != null && w.FindState(c.Owner) != null ? c.Owner : null;
                case EditLayer.Wildlands: return c.Owner != null && w.FindWildland(c.Owner) != null ? c.Owner : null;
                default:
                    WorldObject o = w.ObjectAt(cell);
                    return o != null ? WorldObjectTypes.ToCode(o.Type) : null;
            }
        }

        // Сделать клетку не сушей. Только если на ней нет объектов и армий (иначе их пришлось бы молча удалять).
        public static IWorldCommand RemoveLand(World w, IEnumerable<CellCoord> cells, out string refused)
        {
            refused = null;
            var changes = new List<CellChange>();
            foreach (CellCoord cell in cells)
            {
                CellData data = w.FindCell(cell);
                if (data == null) continue;
                if (w.ObjectAt(cell) != null) { refused = "На клетке " + cell + " есть объект: сначала уберите его."; return null; }
                foreach (ArmyEntity a in w.Armies)
                    if (a.Position.HasValue && a.Position.Value == cell) { refused = "На клетке " + cell + " стоит армия " + a.Id + "."; return null; }
                changes.Add(new CellChange(cell, CellState.Of(data), new CellState()));
            }
            return new CellsCommand("Убрать сушу (" + changes.Count + ")", changes);
        }

        // ---------- объекты ----------

        // Поставить объект на клетку суши. Если на клетке уже есть объект - он заменяется одним шагом истории.
        public static IWorldCommand PlaceObject(World w, CellCoord cell, WorldObjectType type, out string refused)
        {
            refused = null;
            if (!w.Grid.Contains(cell)) { refused = "Клетка вне сетки (невидимая стена)."; return null; }
            CellData data = w.FindCell(cell);
            if (data == null) { refused = "Объект ставится только на сушу."; return null; }
            var parts = new List<IWorldCommand>();
            WorldObject existing = w.ObjectAt(cell);
            if (existing != null)
            {
                if (existing.Type == type) return null;
                parts.Add(new RemoveEntityCommand("Убрать " + existing.Id, existing.Id));
            }
            var obj = new WorldObject(w.NextId(EntityId.Object, WorldObjectTypes.ToCode(type)), cell, type);
            if (type == WorldObjectType.ArmyMark)
            {
                foreach (ArmyEntity a in w.Armies)
                    if (a.Status == ArmyStatus.Roaming && a.Position.HasValue && a.Position.Value == cell) obj.Owner = a.Id;
            }
            else obj.Owner = data.Owner;
            parts.Add(new AddEntityCommand("Поставить " + WorldObjectTypes.ToCode(type), obj, -1));
            return parts.Count == 1 ? parts[0] : new CompositeCommand("Заменить объект на " + WorldObjectTypes.ToCode(type), parts);
        }

        public static IWorldCommand RemoveObjectAt(World w, CellCoord cell)
        {
            WorldObject o = w.ObjectAt(cell);
            return o == null ? null : new RemoveEntityCommand("Убрать объект " + o.Id, o.Id);
        }

        public static IWorldCommand SetObjectOwner(string objectId, string owner)
        {
            return new PropertyCommand<string>("Владелец объекта " + objectId,
                w => Obj(w, objectId).Owner, (w, v) => Obj(w, objectId).Owner = v, owner);
        }

        public static IWorldCommand SetObjectNotes(string objectId, string notes)
        {
            return new PropertyCommand<string>("Заметка объекта " + objectId,
                w => Obj(w, objectId).Notes, (w, v) => Obj(w, objectId).Notes = v ?? "", notes ?? "");
        }

        // ---------- клетки ----------

        public static IWorldCommand SetCellNotes(World w, CellCoord cell, string notes)
        {
            CellData data = w.FindCell(cell);
            if (data == null) return null;
            CellState before = CellState.Of(data);
            CellState after = before;
            after.Notes = notes ?? "";
            if (after.SameAs(before)) return null;
            return new CellsCommand("Заметка клетки " + cell, new[] { new CellChange(cell, before, after) });
        }

        // ---------- армии ----------

        public static IWorldCommand SetArmyRoute(string armyId, IList<CellCoord> route)
        {
            var copy = new List<CellCoord>(route);
            return new PropertyCommand<List<CellCoord>>("Маршрут армии " + armyId,
                w => new List<CellCoord>(Army(w, armyId).Route),
                (w, v) => { ArmyEntity a = Army(w, armyId); a.Route.Clear(); a.Route.AddRange(v); },
                copy);
        }

        public static IWorldCommand SetArmyPosition(string armyId, CellCoord? cell)
        {
            return new PropertyCommand<CellCoord?>("Клетка армии " + armyId, w => Army(w, armyId).Position, (w, v) => Army(w, armyId).Position = v, cell);
        }

        public static IWorldCommand SetArmyOrigin(string armyId, CellCoord? cell)
        {
            return new PropertyCommand<CellCoord?>("Начальная точка армии " + armyId, w => Army(w, armyId).Origin, (w, v) => Army(w, armyId).Origin = v, cell);
        }

        public static IWorldCommand SetArmyStatus(string armyId, ArmyStatus status)
        {
            return new PropertyCommand<ArmyStatus>("Статус армии " + armyId, w => Army(w, armyId).Status, (w, v) => Army(w, armyId).Status = v, status);
        }

        public static IWorldCommand SetArmyNotes(string armyId, string notes)
        {
            return new PropertyCommand<string>("Заметка армии " + armyId, w => Army(w, armyId).Notes, (w, v) => Army(w, armyId).Notes = v ?? "", notes ?? "");
        }

        // ---------- реестр ----------

        public static IWorldCommand AddState(World w, string name, StateKind kind, RgbColor color)
        {
            var s = new StateEntity(w.NextId(EntityId.State, "new"), name, kind, color);
            return new AddEntityCommand("Добавить государство " + name, s, -1);
        }

        public static IWorldCommand AddWildland(World w, RgbColor color)
        {
            return new AddEntityCommand("Добавить группу диких земель", new WildlandEntity(w.NextId(EntityId.Wildland, "group"), color), -1);
        }

        public static IWorldCommand AddArmy(World w, ArmyStatus status)
        {
            return new AddEntityCommand("Добавить армию", new ArmyEntity(w.NextId(EntityId.Army, "new"), status), -1);
        }

        // Удаление сущности, на которую ссылаются клетки или объекты, запрещено: ссылки повисли бы.
        // Строка и столбец государства в матрице отношений удаляются вместе с ним (отменяется одним шагом).
        public static IWorldCommand RemoveEntity(World w, string id, out string refused)
        {
            refused = null;
            int refs = 0;
            foreach (CellData c in w.Cells) if (c.Owner == id) refs++;
            foreach (WorldObject o in w.Objects) if (o.Owner == id) refs++;
            if (refs > 0)
            {
                refused = "На " + id + " ссылаются клетки или объекты (" + refs + "). Сначала перекрасьте их.";
                return null;
            }
            int index;
            if (EntityIds.Find(w, id, out index) == null) { refused = "Сущность " + id + " не найдена."; return null; }
            return new RemoveEntityCommand("Удалить " + id, id);
        }

        public static IWorldCommand SetStateName(string id, string name)
        {
            return new PropertyCommand<string>("Название " + id, w => St(w, id).Name, (w, v) => St(w, id).Name = v ?? "", name ?? "");
        }

        public static IWorldCommand SetStateKind(string id, StateKind kind)
        {
            return new PropertyCommand<StateKind>("Тип " + id, w => St(w, id).Kind, (w, v) => St(w, id).Kind = v, kind);
        }

        public static IWorldCommand SetStateRuler(string id, string ruler)
        {
            return new PropertyCommand<string>("Правитель " + id, w => St(w, id).RulerPlaceholder, (w, v) => St(w, id).RulerPlaceholder = v ?? "", ruler ?? "");
        }

        // Цвет - только отображение: клетки не меняются (приёмка Этапа 2).
        public static IWorldCommand SetColor(string id, RgbColor color)
        {
            return new PropertyCommand<RgbColor>("Цвет " + id,
                w => w.FindState(id) != null ? St(w, id).Color : Wild(w, id).Color,
                (w, v) => { if (w.FindState(id) != null) St(w, id).Color = v; else Wild(w, id).Color = v; },
                color);
        }

        public static IWorldCommand SetEntityNotes(string id, string notes)
        {
            return new PropertyCommand<string>("Заметка " + id,
                w => w.FindState(id) != null ? St(w, id).Notes : Wild(w, id).Notes,
                (w, v) => { if (w.FindState(id) != null) St(w, id).Notes = v ?? ""; else Wild(w, id).Notes = v ?? ""; },
                notes ?? "");
        }

        // ---------- отношения ----------

        public static IWorldCommand SetRelation(string fromId, string toId, int? value)
        {
            return new PropertyCommand<int?>("Отношение " + fromId + " -> " + toId,
                w => w.Relations.Get(w.Relations.IndexOf(fromId), w.Relations.IndexOf(toId)),
                (w, v) => w.Relations.Set(w.Relations.IndexOf(fromId), w.Relations.IndexOf(toId), v),
                value);
        }

        public static IWorldCommand ReplaceRelations(RelationsMatrix matrix)
        {
            RelationsMatrix copy = matrix.Clone();
            return new PropertyCommand<RelationsMatrix>("Импорт отношений", w => w.Relations, (w, v) => w.Relations = v, copy);
        }

        // ---------- помощники ----------

        private static void RequireValue(World w, EditLayer layer, string value)
        {
            if (value == null) return;
            bool ok = layer == EditLayer.Landscape ? w.Legend.FindLandscape(value) != null
                    : layer == EditLayer.Political ? w.FindState(value) != null
                    : w.FindWildland(value) != null;
            if (!ok) throw new DomainException("Значение \"" + value + "\" не подходит для слоя " + layer + ".");
        }

        private static bool OwnerBelongsTo(World w, EditLayer layer, string owner)
        {
            return layer == EditLayer.Political ? w.FindState(owner) != null : w.FindWildland(owner) != null;
        }

        private static string DescribePaint(EditLayer layer, string value)
        {
            string what = layer == EditLayer.Landscape ? "ландшафт" : layer == EditLayer.Political ? "государство" : "дикие земли";
            return value == null ? "Стереть: " + what : what + " = " + value;
        }

        private static WorldObject Obj(World w, string id)
        {
            WorldObject o = w.FindObject(id);
            if (o == null) throw new DomainException("Объект " + id + " не найден.");
            return o;
        }

        private static ArmyEntity Army(World w, string id)
        {
            ArmyEntity a = w.FindArmy(id);
            if (a == null) throw new DomainException("Армия " + id + " не найдена.");
            return a;
        }

        private static StateEntity St(World w, string id)
        {
            StateEntity s = w.FindState(id);
            if (s == null) throw new DomainException("Государство " + id + " не найдено.");
            return s;
        }

        private static WildlandEntity Wild(World w, string id)
        {
            WildlandEntity g = w.FindWildland(id);
            if (g == null) throw new DomainException("Группа " + id + " не найдена.");
            return g;
        }
    }
}
