using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    // Отношения N x N (M3). Матрица кружков: цвет - знак и сила, размер - модуль, в углу - обратное значение.
    // Правка выбранной пары - ползунком или числом внизу; каждое изменение - команда с отменой.
    internal sealed class RelationsForm : UiDialog
    {
        private readonly WorldDocument _doc;
        private readonly RelationsMatrixView _matrix;
        private readonly UiLabel _pair = new UiLabel("", TextRole.Primary) { Font = Ui.BodyStrong, AutoSize = false, Height = Ui.S(22), Width = Ui.S(520) };
        private readonly UiLabel _reverse = new UiLabel("", TextRole.Secondary) { AutoSize = false, Height = Ui.S(20), Width = Ui.S(520) };
        private readonly UiLabel _status = new UiLabel("", TextRole.Warning) { AutoSize = false, Height = Ui.S(20), Width = Ui.S(700) };
        private readonly RelationSlider _slider = new RelationSlider { Size = Ui.S(420, 34) };
        private readonly UiTextField _number = new UiTextField(false) { Size = Ui.S(70, 32) };
        private readonly UiButton _clear = new UiButton("Нет значения", ButtonKind.Secondary) { Size = Ui.S(130, 32) };
        private bool _loading;

        public RelationsForm(WorldDocument doc) : base(Strings.RelationsTitle, Strings.RelationsSubtitle, new Size(980, 720))
        {
            _doc = doc;
            _matrix = new RelationsMatrixView(doc) { Dock = DockStyle.Fill };
            _matrix.SelectionChanged += ShowPair;

            var editor = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Bottom, Height = Ui.S(118), LineTop = true, Padding = new Padding(0, Ui.S(10), 0, 0) };
            _pair.Location = new Point(0, Ui.S(12));
            _reverse.Location = new Point(0, Ui.S(34));
            _slider.Location = new Point(0, Ui.S(60));
            _number.Location = new Point(Ui.S(432), Ui.S(61));
            _clear.Location = new Point(Ui.S(512), Ui.S(61));
            _status.Location = new Point(0, Ui.S(98));
            _slider.ValueCommitted += v => Guard(() => SetSelected(v));
            _slider.ValueChanging += v => { _loading = true; _number.Text = v.ToString(CultureInfo.InvariantCulture); _loading = false; };
            _number.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Guard(ApplyNumber); } };
            _number.Box.Leave += (s, e) => Guard(ApplyNumber);
            _clear.Click += (s, e) => Guard(() => SetSelected(null));
            Ui.Tip(_clear, "Пара ещё не решена автором", null);
            editor.Controls.AddRange(new Control[] { _pair, _reverse, _slider, _number, _clear, _status });

            Body.Controls.Add(_matrix);
            Body.Controls.Add(editor);

            var import = new UiButton(Strings.ImportXlsx, ButtonKind.Secondary) { Icon = Glyph.Import };
            import.Click += (s, e) => Guard(Import);
            var export = new UiButton(Strings.ExportXlsx, ButtonKind.Secondary) { Icon = Glyph.Export };
            export.Click += (s, e) => Guard(Export);
            AddLeading(import);
            AddLeading(export);
            AddButton(Strings.Done, ButtonKind.Primary, DialogResult.Cancel, null);
            UiTheme.Apply(this);
            ShowPair();
        }

        private RelationsMatrix M { get { return _doc.World.Relations; } }

        private void ShowPair()
        {
            int i = _matrix.SelRow, j = _matrix.SelCol;
            bool ok = M != null && i >= 0 && j >= 0 && i != j && i < M.Size && j < M.Size;
            _slider.Enabled = _number.Enabled = _clear.Enabled = ok;
            if (!ok)
            {
                _pair.Text = "Выберите клетку матрицы";
                _reverse.Text = "Строка - кто относится, столбец - к кому. Стрелки двигают выбор.";
                _slider.SetValue(null);
                _number.Text = "";
                return;
            }
            int? v = M.Get(i, j), back = M.Get(j, i);
            _pair.Text = _matrix.StateName(i) + "  ->  " + _matrix.StateName(j) + ":  " + Format(v);
            _reverse.Text = "Обратно " + _matrix.StateName(j) + " -> " + _matrix.StateName(i) + ":  " + Format(back) +
                            (v.HasValue && back.HasValue && Math.Sign(v.Value) != Math.Sign(back.Value) && v.Value != 0 && back.Value != 0 ? "   разные знаки" : "");
            _loading = true;
            _slider.SetValue(v);
            _number.Text = v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "";
            _loading = false;
        }

        private static string Format(int? v)
        {
            return v.HasValue ? (v.Value > 0 ? "+" : "") + v.Value.ToString(CultureInfo.InvariantCulture) : "нет значения";
        }

        private void ApplyNumber()
        {
            if (_loading || !_number.Enabled) return;
            string text = _number.Text.Trim();
            if (text.Length == 0) { if (M.Get(_matrix.SelRow, _matrix.SelCol).HasValue) SetSelected(null); return; }
            int v;
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v) || v < RelationsMatrix.MinValue || v > RelationsMatrix.MaxValue)
            {
                _status.Text = Strings.ValueOutOfRange;
                ShowPair();
                return;
            }
            SetSelected(v);
        }

        private void SetSelected(int? v)
        {
            int i = _matrix.SelRow, j = _matrix.SelCol;
            if (M == null || i < 0 || j < 0 || i == j) return;
            _status.Text = "";
            if (v != M.Get(i, j))
            {
                string error = _doc.Execute(WorldEdits.SetRelation(M.Order[i], M.Order[j], v));
                if (error != null) _status.Text = error;
            }
            _matrix.Invalidate();
            ShowPair();
        }

        private void Import()
        {
            using (var ofd = new OpenFileDialog { Filter = Strings.FilterXlsx })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                byte[] data;
                try { data = File.ReadAllBytes(ofd.FileName); }
                catch (Exception ex) { Dialogs.Error(this, "Не удалось прочитать файл: " + ex.Message); return; }
                var warnings = new List<string>();
                string error = _doc.ImportRelationsXlsx(data, warnings);
                if (error != null) { Dialogs.Error(this, error); return; }
                _matrix.Invalidate();
                ShowPair();
                UiAlert.Info(this, "Отношения импортированы",
                    (warnings.Count > 0 ? string.Join("\n", warnings.ToArray()) + "\n\n" : "") + "Отменить импорт можно в главном окне (Ctrl+Z).");
            }
        }

        private void Export()
        {
            using (var sfd = new SaveFileDialog { Filter = Strings.FilterXlsx, FileName = "relations.xlsx", OverwritePrompt = true })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _doc.WriteExternalBytes(sfd.FileName, _doc.ExportRelationsXlsx(), "экспорт отношений");
                    _status.Role = TextRole.Secondary;
                    _status.Text = "Записано: " + sfd.FileName;
                }
                catch (Exception ex) { Dialogs.Unexpected(this, "экспорт отношений " + sfd.FileName, ex); }
            }
        }

        private void Guard(Action action)
        {
            _status.Role = TextRole.Warning;
            try { action(); }
            catch (DomainException ex) { _status.Text = ex.Message; }
            catch (Exception ex) { Dialogs.Unexpected(this, "отношения", ex); }
        }

        // Для пробы интерфейса.
        internal void ProbeSelect(int row, int col) { _matrix.Select(row, col); }
    }

    // Матрица отношений, нарисованная целиком: заголовки строк и столбцов, кружки, выбор, наведение.
    internal sealed class RelationsMatrixView : Control
    {
        private readonly WorldDocument _doc;
        private int _hoverRow = -1, _hoverCol = -1;

        public RelationsMatrixView(WorldDocument doc)
        {
            _doc = doc;
            SelRow = -1;
            SelCol = -1;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            if (M != null && M.Size > 1) { SelRow = 0; SelCol = 1; }
        }

        public event Action SelectionChanged;
        public int SelRow { get; private set; }
        public int SelCol { get; private set; }
        private RelationsMatrix M { get { return _doc.World.Relations; } }

        public string StateName(int index)
        {
            StateEntity s = _doc.World.FindState(M.Order[index]);
            return s != null ? s.Name : M.Order[index];
        }

        private void Geometry(out float head, out float cell, out float ox, out float oy)
        {
            int n = M == null ? 1 : Math.Max(1, M.Size);
            head = Ui.S(118f);
            cell = Math.Max(Ui.S(40f), Math.Min(Ui.S(78f), Math.Min((Width - head - Ui.S(8)) / n, (Height - Ui.S(40f) - Ui.S(8)) / n)));
            ox = Math.Max(0, (Width - head - cell * n) / 2f) + head;
            oy = Ui.S(40f);
        }

        public void Select(int row, int col)
        {
            if (M == null) return;
            SelRow = Math.Max(0, Math.Min(M.Size - 1, row));
            SelCol = Math.Max(0, Math.Min(M.Size - 1, col));
            Invalidate();
            if (SelectionChanged != null) SelectionChanged();
        }

        private bool Hit(Point p, out int row, out int col)
        {
            float head, cell, ox, oy;
            Geometry(out head, out cell, out ox, out oy);
            row = (int)Math.Floor((p.Y - oy) / cell);
            col = (int)Math.Floor((p.X - ox) / cell);
            return M != null && p.X >= ox && p.Y >= oy && row >= 0 && col >= 0 && row < M.Size && col < M.Size;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int r, c;
            if (!Hit(e.Location, out r, out c)) { r = -1; c = -1; }
            if (r != _hoverRow || c != _hoverCol) { _hoverRow = r; _hoverCol = c; Invalidate(); }
            Cursor = r >= 0 && r != c ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hoverRow = _hoverCol = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            int r, c;
            if (Hit(e.Location, out r, out c) && r != c) Select(r, c);
            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (M == null || SelRow < 0) { base.OnKeyDown(e); return; }
            int r = SelRow, c = SelCol;
            if (e.KeyCode == Keys.Left) c--;
            else if (e.KeyCode == Keys.Right) c++;
            else if (e.KeyCode == Keys.Up) r--;
            else if (e.KeyCode == Keys.Down) r++;
            else { base.OnKeyDown(e); return; }
            r = Math.Max(0, Math.Min(M.Size - 1, r));
            c = Math.Max(0, Math.Min(M.Size - 1, c));
            if (r == c) { if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up) { if (c > 0 && e.KeyCode == Keys.Left) c--; else if (r > 0) r--; } else { if (e.KeyCode == Keys.Right && c < M.Size - 1) c++; else if (r < M.Size - 1) r++; } }
            if (r != c) Select(r, c);
            e.Handled = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette p = UiTheme.C;
            Graphics g = e.Graphics;
            g.Clear(UiTheme.BackOf(this, p.Window));
            if (M == null || M.Size == 0) return;
            Ui.HighQuality(g);
            float head, cell, ox, oy;
            Geometry(out head, out cell, out ox, out oy);
            int n = M.Size;
            var board = new RectangleF(ox, oy, cell * n, cell * n);
            Ui.FillRound(g, p.Content, RectangleF.Inflate(board, Ui.S(4f), Ui.S(4f)), Ui.S(12f));

            for (int k = 0; k < n; k++)
            {
                bool rowOn = k == SelRow || k == _hoverRow, colOn = k == SelCol || k == _hoverCol;
                Ui.DrawText(g, Short(StateName(k), cell), k == SelCol ? Ui.BodyStrong : Ui.Small, colOn ? p.Text : p.TextSecondary,
                    Rectangle.Round(new RectangleF(ox + k * cell, oy - Ui.S(30f), cell, Ui.S(26f))), TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
                Ui.DrawText(g, StateName(k), k == SelRow ? Ui.BodyStrong : Ui.Body, rowOn ? p.Text : p.TextSecondary,
                    Rectangle.Round(new RectangleF(ox - head, oy + k * cell, head - Ui.S(10f), cell)), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if (SelRow >= 0)
                using (var b = new SolidBrush(Color.FromArgb(p.IsDark ? 28 : 18, p.Accent)))
                {
                    g.FillRectangle(b, ox, oy + SelRow * cell, cell * n, cell);
                    g.FillRectangle(b, ox + SelCol * cell, oy, cell, cell * n);
                }

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var r = new RectangleF(ox + j * cell, oy + i * cell, cell, cell);
                    if (i == j)
                    {
                        using (var pen = new Pen(p.Separator, 1f)) g.DrawLine(pen, r.Left + cell * 0.3f, r.Bottom - cell * 0.3f, r.Right - cell * 0.3f, r.Top + cell * 0.3f);
                        continue;
                    }
                    DrawCell(g, r, M.Get(i, j), M.Get(j, i), p, i == SelRow && j == SelCol, i == _hoverRow && j == _hoverCol);
                }

            var legend = new Rectangle((int)ox, (int)(oy + cell * n + Ui.S(12f)), (int)(cell * n), Ui.S(18));
            if (legend.Bottom < Height)
                Ui.DrawText(g, "красный - вражда,  серый - нейтрально,  зелёный - союз;  в углу - как относятся в ответ", Ui.Small, p.TextTertiary, legend, TextFormatFlags.HorizontalCenter);
        }

        private static string Short(string name, float cell)
        {
            int max = Math.Max(4, (int)(cell / Ui.S(8f)));
            return name.Length <= max ? name : name.Substring(0, max - 1) + ".";
        }

        private static void DrawCell(Graphics g, RectangleF r, int? v, int? back, UiPalette p, bool selected, bool hover)
        {
            // Кружок смещён влево-вверх: правый нижний угол отдан обратному значению.
            float cx = r.X + r.Width * 0.44f, cy = r.Y + r.Height * 0.44f;
            if (selected || hover)
                Ui.StrokeRound(g, selected ? p.Accent : p.Separator, RectangleF.Inflate(r, -Ui.S(2f), -Ui.S(2f)), Ui.S(8f), selected ? Ui.S(2f) : 1f);
            if (!v.HasValue)
            {
                float d = r.Width * 0.34f;
                using (var pen = new Pen(p.TextTertiary, 1.2f) { DashPattern = new[] { 2f, 2f } }) g.DrawEllipse(pen, cx - d / 2, cy - d / 2, d, d);
                Ui.DrawText(g, "?", Ui.Small, p.TextTertiary, Rectangle.Round(new RectangleF(cx - d / 2, cy - d / 2, d, d)), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                double k = Math.Min(1.0, Math.Abs(v.Value) / 100.0);
                float d = (float)(r.Width * (0.44 + 0.16 * k));
                Color fill = Heat(v.Value, p);
                using (var b = new SolidBrush(fill)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
                Ui.DrawText(g, v.Value.ToString(CultureInfo.InvariantCulture), Ui.Small, Ui.InkFor(fill), Rectangle.Round(new RectangleF(cx - d / 2, cy - d / 2, d, d)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            string backText = back.HasValue ? back.Value.ToString(CultureInfo.InvariantCulture) : "?";
            Ui.DrawText(g, backText, Ui.Caption, p.TextTertiary, Rectangle.Round(new RectangleF(r.Right - Ui.S(40f), r.Bottom - Ui.S(15f), Ui.S(37f), Ui.S(14f))), TextFormatFlags.Right | TextFormatFlags.NoClipping);
        }

        internal static Color Heat(int value, UiPalette p)
        {
            double t = Math.Max(-1, Math.Min(1, value / 100.0));
            Color neutral = p.IsDark ? Color.FromArgb(99, 99, 102) : Color.FromArgb(199, 199, 204);
            Color end = t < 0 ? (p.IsDark ? Color.FromArgb(255, 69, 58) : Color.FromArgb(255, 59, 48)) : (p.IsDark ? Color.FromArgb(48, 209, 88) : Color.FromArgb(52, 199, 89));
            return Ui.Mix(neutral, end, (float)Math.Abs(t));
        }
    }

    // Ползунок -100..100 с нулём посередине. Тянуть мышью, стрелки - шаг 5, Shift - шаг 1, Home/End - края.
    internal sealed class RelationSlider : Control
    {
        private int? _value;
        private bool _drag;

        public RelationSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
        }

        public event Action<int> ValueChanging;
        public event Action<int?> ValueCommitted;

        public void SetValue(int? v) { _value = v; Invalidate(); }

        private float Track0 { get { return Ui.S(12f); } }
        private float Track1 { get { return Width - Ui.S(12f); } }

        private int ValueAt(int x)
        {
            double t = (x - Track0) / (Track1 - Track0);
            int v = (int)Math.Round(RelationsMatrix.MinValue + t * (RelationsMatrix.MaxValue - RelationsMatrix.MinValue));
            return Math.Max(RelationsMatrix.MinValue, Math.Min(RelationsMatrix.MaxValue, v));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Enabled) return;
            Focus();
            _drag = true;
            _value = ValueAt(e.X);
            if (ValueChanging != null) ValueChanging(_value.Value);
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag)
            {
                _value = ValueAt(e.X);
                if (ValueChanging != null) ValueChanging(_value.Value);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_drag) { _drag = false; if (ValueCommitted != null) ValueCommitted(_value); }
            base.OnMouseUp(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            return k == Keys.Left || k == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Enabled) return;
            int v = _value ?? 0, step = e.Shift ? 1 : 5;
            if (e.KeyCode == Keys.Left) v -= step;
            else if (e.KeyCode == Keys.Right) v += step;
            else if (e.KeyCode == Keys.Home) v = RelationsMatrix.MinValue;
            else if (e.KeyCode == Keys.End) v = RelationsMatrix.MaxValue;
            else { base.OnKeyDown(e); return; }
            v = Math.Max(RelationsMatrix.MinValue, Math.Min(RelationsMatrix.MaxValue, v));
            _value = v;
            if (ValueChanging != null) ValueChanging(v);
            if (ValueCommitted != null) ValueCommitted(v);
            Invalidate();
            e.Handled = true;
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette p = UiTheme.C;
            Graphics g = e.Graphics;
            g.Clear(UiTheme.BackOf(this, p.Window));
            Ui.HighQuality(g);
            float cy = Height / 2f, h = Ui.S(6f);
            var track = new RectangleF(Track0, cy - h / 2, Track1 - Track0, h);
            using (var brush = new LinearGradientBrush(track, Color.Black, Color.Black, 0f))
            {
                var blend = new ColorBlend { Positions = new[] { 0f, 0.5f, 1f }, Colors = new[] { RelationsMatrixView.Heat(-100, p), RelationsMatrixView.Heat(0, p), RelationsMatrixView.Heat(100, p) } };
                brush.InterpolationColors = blend;
                using (GraphicsPath path = Ui.Round(track, h / 2)) g.FillPath(brush, path);
            }
            float mid = (Track0 + Track1) / 2f;
            using (var pen = new Pen(p.TextTertiary, 1f)) g.DrawLine(pen, mid, cy - Ui.S(9f), mid, cy + Ui.S(9f));
            if (!Enabled) { using (var b = new SolidBrush(Color.FromArgb(150, UiTheme.BackOf(this, p.Window)))) g.FillRectangle(b, ClientRectangle); return; }
            if (!_value.HasValue) return;
            float x = Track0 + (_value.Value - RelationsMatrix.MinValue) / (float)(RelationsMatrix.MaxValue - RelationsMatrix.MinValue) * (Track1 - Track0);
            float d = Ui.S(22f);
            using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(shadow, x - d / 2, cy - d / 2 + 1.5f, d, d);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x - d / 2, cy - d / 2, d, d);
            using (var pen = new Pen(Focused ? p.Accent : p.Separator, Focused ? 2f : 1f)) g.DrawEllipse(pen, x - d / 2, cy - d / 2, d, d);
        }
    }
}
