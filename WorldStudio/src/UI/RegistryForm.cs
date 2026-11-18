using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    // Реестр сущностей (M2): список слева, карточка выбранной сущности справа.
    // Каждая правка - команда с отменой; id показывается, но не меняется: на него ссылаются клетки.
    internal sealed class RegistryForm : UiDialog
    {
        private enum Tab { States, Wildlands, Armies }

        private readonly WorldDocument _doc;
        private readonly Func<CellCoord?> _selectedCell;
        private readonly UiSegmented _tabs = new UiSegmented("Государства", "Дикие земли", "Армии");
        private readonly FlowLayoutPanel _list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
        private readonly UiSurface _detail = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Fill, Padding = new Padding(Ui.S(20), 0, Ui.S(4), 0), AutoScroll = true };
        private readonly UiLabel _status = new UiLabel("", TextRole.Warning) { Dock = DockStyle.Bottom, Height = Ui.S(22) };
        private string _selectedId;

        public RegistryForm(WorldDocument doc, Func<CellCoord?> selectedCell) : base(Strings.RegistryTitle, Strings.RegistrySubtitle, new Size(920, 700))
        {
            _doc = doc;
            _selectedCell = selectedCell;
            var top = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Top, Height = Ui.S(46) };
            _tabs.Location = new Point(0, Ui.S(4));
            _tabs.Width = _tabs.PreferredWidth();
            _tabs.SelectedIndexChanged += () => { _selectedId = null; _status.Text = ""; Reload(); };
            top.Controls.Add(_tabs);

            var left = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Left, Width = Ui.S(300) };
            var listButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = Ui.S(42), BackColor = Color.Transparent, Padding = new Padding(0, Ui.S(8), 0, 0) };
            var add = new UiButton(Strings.Add, ButtonKind.Secondary) { Icon = Glyph.Plus, Width = Ui.S(128), Height = Ui.S(30) };
            add.Click += (s, e) => Guard(AddEntity);
            var remove = new UiButton(Strings.Remove, ButtonKind.Danger) { Icon = Glyph.Trash, Width = Ui.S(128), Height = Ui.S(30) };
            remove.Click += (s, e) => Guard(RemoveSelected);
            listButtons.Controls.Add(add);
            listButtons.Controls.Add(remove);
            left.Controls.Add(_list);
            left.Controls.Add(listButtons);

            Body.Controls.Add(_detail);
            Body.Controls.Add(left);
            Body.Controls.Add(top);
            Body.Controls.Add(_status);
            AddButton(Strings.Done, ButtonKind.Primary, DialogResult.Cancel, null);
            Reload();
        }

        private World W { get { return _doc.World; } }
        private Tab Current { get { return (Tab)_tabs.SelectedIndex; } }

        private void Reload()
        {
            _list.SuspendLayout();
            while (_list.Controls.Count > 0) { Control c = _list.Controls[0]; _list.Controls.RemoveAt(0); c.Dispose(); }
            var ids = new List<string>();
            switch (Current)
            {
                case Tab.States:
                    foreach (StateEntity s in W.States) { AddRow(s.Id, s.Name, Labels.StateKind(s.Kind), MapCanvas.ToColor(s.Color), false); ids.Add(s.Id); }
                    break;
                case Tab.Wildlands:
                    foreach (WildlandEntity g in W.Wildlands) { AddRow(g.Id, Labels.Wild(g.Id), CountCells(g.Id).ToString(), MapCanvas.ToColor(g.Color), true); ids.Add(g.Id); }
                    break;
                default:
                    foreach (ArmyEntity a in W.Armies)
                    {
                        AddRow(a.Id, Labels.ArmyName(a.Id), Labels.ArmyStatus(a.Status), a.Status == ArmyStatus.Roaming ? Color.FromArgb(255, 110, 60) : Color.FromArgb(150, 150, 150), false);
                        ids.Add(a.Id);
                    }
                    break;
            }
            _list.ResumeLayout();
            if (_selectedId == null || !ids.Contains(_selectedId)) _selectedId = ids.Count > 0 ? ids[0] : null;
            MarkSelected();
            ShowDetail();
            UiTheme.Apply(this);
        }

        private int CountCells(string owner)
        {
            int n = 0;
            foreach (CellData c in W.Cells) if (c.Owner == owner) n++;
            return n;
        }

        private void AddRow(string id, string title, string detail, Color swatch, bool hatched)
        {
            var row = new UiButton(title, ButtonKind.Row) { Tag = id, Detail = detail, Swatch = swatch, SwatchHatched = hatched, Width = Ui.S(284), Height = Ui.S(34), Margin = new Padding(0, 0, 0, Ui.S(2)) };
            row.Click += (s, e) => { _selectedId = id; _status.Text = ""; MarkSelected(); ShowDetail(); };
            _list.Controls.Add(row);
        }

        private void MarkSelected()
        {
            foreach (Control c in _list.Controls) { var b = c as UiButton; if (b != null) b.Checked = (string)b.Tag == _selectedId; }
        }

        // ---------- карточка справа ----------

        private void ShowDetail()
        {
            _detail.SuspendLayout();
            while (_detail.Controls.Count > 0) { Control c = _detail.Controls[0]; _detail.Controls.RemoveAt(0); c.Dispose(); }
            var stack = new List<Control>();
            if (_selectedId == null)
                stack.Add(new EmptyState(Glyph.People, "Список пуст", "Нажмите \"Добавить\", чтобы создать запись") { Height = Ui.S(300) });
            else if (Current == Tab.States) StateDetail(stack, W.FindState(_selectedId));
            else if (Current == Tab.Wildlands) WildDetail(stack, W.FindWildland(_selectedId));
            else ArmyDetail(stack, W.FindArmy(_selectedId));
            for (int i = stack.Count - 1; i >= 0; i--) { stack[i].Dock = DockStyle.Top; _detail.Controls.Add(stack[i]); }
            UiTheme.Apply(_detail);
            _detail.ResumeLayout();
        }

        private static UiLabel Caption(string text)
        {
            return new UiLabel(text.ToUpperInvariant(), TextRole.Caption) { Height = Ui.S(30), TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(Ui.S(2), 0, 0, Ui.S(5)) };
        }

        // Значение применяется, когда фокус уходит из поля: одна правка - одна команда в истории.
        private UiTextField Field(string value, bool multiline, Action<string> commit)
        {
            var f = new UiTextField(multiline) { Height = Ui.S(multiline ? 84 : 32) };
            f.Text = value ?? "";
            string original = f.Text;
            f.Box.Leave += (s, e) => { if (f.Text != original) { original = f.Text; Guard(() => commit(f.Text)); } };
            return f;
        }

        private void StateDetail(List<Control> stack, StateEntity s)
        {
            stack.Add(new UiLabel(s.Name, TextRole.Title) { Height = Ui.S(30) });
            stack.Add(new UiLabel(s.Id + "   " + Labels.Plural(CountCells(s.Id), "клетка", "клетки", "клеток"), TextRole.Tertiary) { Height = Ui.S(20) });
            stack.Add(Caption("Название"));
            stack.Add(Field(s.Name, false, v => { Exec(WorldEdits.SetStateName(s.Id, v)); Reload(); }));
            stack.Add(Caption("Тип"));
            var kinds = (StateKind[])Enum.GetValues(typeof(StateKind));
            var names = new string[kinds.Length];
            for (int i = 0; i < kinds.Length; i++) names[i] = Labels.StateKind(kinds[i]);
            var kind = new UiSegmented(names);
            kind.Width = kind.PreferredWidth();
            kind.SelectedIndex = Array.IndexOf(kinds, s.Kind);
            kind.SelectedIndexChanged += () => Guard(() => { Exec(WorldEdits.SetStateKind(s.Id, kinds[kind.SelectedIndex])); Reload(); });
            stack.Add(Holder(kind));
            stack.Add(Caption("Цвет на карте"));
            stack.Add(Holder(ColorButton(s.Id, s.Color)));
            stack.Add(Caption("Правитель"));
            stack.Add(Field(s.RulerPlaceholder, false, v => Exec(WorldEdits.SetStateRuler(s.Id, v))));
            stack.Add(Caption("Заметки"));
            stack.Add(Field(s.Notes, true, v => Exec(WorldEdits.SetEntityNotes(s.Id, v))));
        }

        private void WildDetail(List<Control> stack, WildlandEntity g)
        {
            stack.Add(new UiLabel(Labels.Wild(g.Id), TextRole.Title) { Height = Ui.S(30) });
            stack.Add(new UiLabel(g.Id + "   " + Labels.Plural(CountCells(g.Id), "клетка", "клетки", "клеток") + ", без официального лорда", TextRole.Tertiary) { Height = Ui.S(20) });
            stack.Add(Caption("Цвет на карте"));
            stack.Add(Holder(ColorButton(g.Id, g.Color)));
            stack.Add(Caption("Заметки"));
            stack.Add(Field(g.Notes, true, v => Exec(WorldEdits.SetEntityNotes(g.Id, v))));
        }

        private void ArmyDetail(List<Control> stack, ArmyEntity a)
        {
            stack.Add(new UiLabel(Labels.ArmyName(a.Id), TextRole.Title) { Height = Ui.S(30) });
            stack.Add(new UiLabel(a.Id, TextRole.Tertiary) { Height = Ui.S(20) });
            stack.Add(Caption("Статус"));
            var status = new UiSegmented(Labels.ArmyStatus(ArmyStatus.Roaming), Labels.ArmyStatus(ArmyStatus.Destroyed));
            status.Width = status.PreferredWidth();
            status.SelectedIndex = a.Status == ArmyStatus.Roaming ? 0 : 1;
            status.SelectedIndexChanged += () => Guard(() => { Exec(WorldEdits.SetArmyStatus(a.Id, status.SelectedIndex == 0 ? ArmyStatus.Roaming : ArmyStatus.Destroyed)); Reload(); });
            stack.Add(Holder(status));
            stack.Add(Caption("Где стоит сейчас"));
            stack.Add(CellPicker(a.Position, c => Exec(WorldEdits.SetArmyPosition(a.Id, c))));
            stack.Add(Caption("Откуда вышла"));
            stack.Add(CellPicker(a.Origin, c => Exec(WorldEdits.SetArmyOrigin(a.Id, c))));
            stack.Add(Caption("Маршрут"));
            stack.Add(new UiLabel(a.Route.Count == 0 ? "Не нарисован. Рисуется на карте инструментом \"Маршрут армии\" (M)."
                                                   : Labels.Plural(a.Route.Count, "точка", "точки", "точек") + ". Правится на карте инструментом \"Маршрут армии\" (M).",
                                  TextRole.Secondary) { Height = Ui.S(36) });
            stack.Add(Caption("Заметки"));
            stack.Add(Field(a.Notes, true, v => Exec(WorldEdits.SetArmyNotes(a.Id, v))));
        }

        private Control CellPicker(CellCoord? value, Action<CellCoord?> commit)
        {
            var row = new FlowLayoutPanel { Height = Ui.S(36), BackColor = Color.Transparent, WrapContents = false };
            var label = new UiLabel(value.HasValue ? "Клетка " + Labels.Cell(value) : "Не задана", value.HasValue ? TextRole.Primary : TextRole.Tertiary)
            { Width = Ui.S(120), Height = Ui.S(30), TextAlign = ContentAlignment.MiddleLeft };
            var take = new UiButton("Взять выбранную на карте", ButtonKind.Secondary) { Icon = Glyph.Target, Width = Ui.S(220), Height = Ui.S(30) };
            take.Click += (s, e) => Guard(() =>
            {
                CellCoord? cell = _selectedCell();
                if (!cell.HasValue) { _status.Text = "Сначала выберите клетку на карте, затем откройте реестр"; return; }
                commit(cell);
                Reload();
            });
            var clear = new UiButton("", ButtonKind.Tool) { Icon = Glyph.Close, IconOnly = true, Size = Ui.S(30, 30) };
            clear.Click += (s, e) => Guard(() => { commit(null); Reload(); });
            Ui.Tip(clear, "Очистить", null);
            row.Controls.AddRange(new Control[] { label, take, clear });
            return row;
        }

        private UiButton ColorButton(string id, RgbColor color)
        {
            var b = new UiButton("#" + color.ToHex(), ButtonKind.Secondary) { Swatch = MapCanvas.ToColor(color), Width = Ui.S(150), Height = Ui.S(32) };
            b.Click += (s, e) => Guard(() =>
            {
                using (var dialog = new ColorDialog { FullOpen = true, Color = MapCanvas.ToColor(color) })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    Exec(WorldEdits.SetColor(id, new RgbColor(dialog.Color.R, dialog.Color.G, dialog.Color.B)));
                    Reload();
                }
            });
            Ui.Tip(b, "Цвет только для отображения: клетки ссылаются на id и не меняются", null);
            return b;
        }

        private static Control Holder(Control c)
        {
            var p = new Panel { Height = c.Height + Ui.S(2), BackColor = Color.Transparent };
            c.Location = new Point(0, 0);
            p.Controls.Add(c);
            return p;
        }

        // ---------- добавить и удалить ----------

        private void AddEntity()
        {
            int before = Current == Tab.States ? W.States.Count : Current == Tab.Wildlands ? W.Wildlands.Count : W.Armies.Count;
            if (Current == Tab.States) Exec(WorldEdits.AddState(W, "Новое государство", StateKind.County, new RgbColor(128, 128, 128)));
            else if (Current == Tab.Wildlands) Exec(WorldEdits.AddWildland(W, new RgbColor(107, 80, 50)));
            else Exec(WorldEdits.AddArmy(W, ArmyStatus.Roaming));
            if (Current == Tab.States && W.States.Count > before) _selectedId = W.States[W.States.Count - 1].Id;
            else if (Current == Tab.Wildlands && W.Wildlands.Count > before) _selectedId = W.Wildlands[W.Wildlands.Count - 1].Id;
            else if (Current == Tab.Armies && W.Armies.Count > before) _selectedId = W.Armies[W.Armies.Count - 1].Id;
            Reload();
            if (Current == Tab.States) _status.Text = "Отношения нового государства пока пустые: заполните их в окне отношений";
        }

        private void RemoveSelected()
        {
            if (_selectedId == null) return;
            string refused;
            IWorldCommand cmd = WorldEdits.RemoveEntity(W, _selectedId, out refused);
            if (cmd == null) { _status.Text = refused; return; }
            string name = Labels.Entity(W, _selectedId);
            if (UiAlert.AskStyled(this, "Удалить " + name + "?", "Id " + _selectedId + " больше не будет выдан. Удаление можно отменить в главном окне (Ctrl+Z).",
                    Glyph.Trash, 0, "Удалить", "Отмена") != 0) return;
            Exec(cmd);
            _selectedId = null;
            Reload();
        }

        private void Exec(IWorldCommand cmd)
        {
            string error = _doc.Execute(cmd);
            _status.Text = error ?? "";
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (DomainException ex) { _status.Text = ex.Message; }
            catch (Exception ex) { Dialogs.Unexpected(this, "реестр", ex); }
        }
    }
}
