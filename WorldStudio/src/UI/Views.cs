using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    // Группа строк на скруглённой подложке, как сгруппированные списки в настройках iOS.
    internal sealed class UiGroup : UiSurface
    {
        public UiGroup() : base(SurfaceRole.Sidebar)
        {
            Dock = DockStyle.Top;
            Padding = new Padding(Ui.S(1));
            AutoSize = false;
        }

        public void AddRow(Control row)
        {
            row.Dock = DockStyle.Top;
            Controls.Add(row);
            row.BringToFront();
            int h = Padding.Vertical;
            foreach (Control c in Controls) h += c.Height;
            Height = h;
        }

        public void ClearRows()
        {
            while (Controls.Count > 0) { Control c = Controls[0]; Controls.RemoveAt(0); c.Dispose(); }
            Height = Padding.Vertical;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            BackColor = UiTheme.BackOf(this, UiTheme.C.Sidebar);
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Ui.HighQuality(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Ui.FillRound(g, UiTheme.C.Content, r, Ui.S(10f));
            Ui.StrokeRound(g, UiTheme.C.Separator, r, Ui.S(10f), 1f);
        }
    }

    // Строка "название - значение" внутри группы; необязательная цветная плашка у значения.
    internal sealed class KeyValueRow : Control
    {
        public KeyValueRow(string key, string value, Color? swatch, bool last)
        {
            Key = key;
            Value = value;
            Swatch = swatch;
            Last = last;
            Height = Ui.S(34);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public string Key { get; set; }
        public string Value { get; set; }
        public Color? Swatch { get; set; }
        public bool Last { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette c = UiTheme.C;
            Graphics g = e.Graphics;
            g.Clear(c.Content);
            Ui.HighQuality(g);
            int pad = Ui.S(12);
            Ui.DrawText(g, Key, Ui.Body, c.TextSecondary, new Rectangle(pad, 0, Width / 2, Height), TextFormatFlags.VerticalCenter);
            Size vs = TextRenderer.MeasureText(Value ?? "", Ui.Body);
            int vx = Width - pad - vs.Width;
            Ui.DrawText(g, Value ?? "", Ui.Body, c.Text, new Rectangle(Math.Max(Width / 3, vx), 0, Width - pad - Math.Max(Width / 3, vx), Height),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
            if (Swatch.HasValue)
            {
                int s = Ui.S(12);
                var r = new RectangleF(Math.Max(Width / 3, vx) - s - Ui.S(6), (Height - s) / 2f, s, s);
                Ui.FillRound(g, Swatch.Value, r, Ui.S(3f));
                Ui.StrokeRound(g, Color.FromArgb(40, 0, 0, 0), r, Ui.S(3f), 1f);
            }
            if (!Last)
                using (var pen = new Pen(c.Separator, 1f)) g.DrawLine(pen, pad, Height - 1, Width, Height - 1);
        }
    }

    // Пустое состояние: иконка, заголовок, пояснение по центру.
    internal sealed class EmptyState : Control
    {
        private readonly Glyph _icon;
        private readonly string _title;
        private readonly string _hint;

        public EmptyState(Glyph icon, string title, string hint)
        {
            _icon = icon;
            _title = title;
            _hint = hint;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public Color? IconTint { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette c = UiTheme.C;
            Graphics g = e.Graphics;
            g.Clear(UiTheme.BackOf(this, c.Sidebar));
            int s = Ui.S(34);
            int y = Math.Max(Ui.S(30), Height / 3 - s);
            UiIcons.Draw(g, _icon, new RectangleF((Width - s) / 2f, y, s, s), IconTint ?? c.TextTertiary);
            y += s + Ui.S(12);
            Ui.DrawText(g, _title, Ui.BodyStrong, c.Text, new Rectangle(Ui.S(16), y, Width - Ui.S(32), Ui.S(20)), TextFormatFlags.HorizontalCenter);
            y += Ui.S(22);
            Ui.DrawText(g, _hint, Ui.Small, c.TextSecondary, new Rectangle(Ui.S(24), y, Width - Ui.S(48), Ui.S(60)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }
    }

    // Список находок проверки: значок уровня, текст в две строки, где - справа. Клик выбирает находку.
    internal sealed class FindingsList : Control
    {
        private readonly List<Finding> _items = new List<Finding>();
        private readonly VScrollBar _scroll = new VScrollBar { Dock = DockStyle.Right, Visible = false };
        private int _hover = -1;
        private int _selected = -1;

        public FindingsList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Controls.Add(_scroll);
            _scroll.Scroll += (s, e) => Invalidate();
        }

        public event Action<Finding> FindingSelected;

        private int RowHeight { get { return Ui.S(58); } }

        public void SetItems(IList<Finding> findings)
        {
            _items.Clear();
            _items.AddRange(findings);
            _selected = -1;
            UpdateScroll();
            Invalidate();
        }

        private void UpdateScroll()
        {
            int total = _items.Count * RowHeight;
            _scroll.Visible = total > Height;
            _scroll.Maximum = Math.Max(0, total);
            _scroll.LargeChange = Math.Max(1, Height);
            _scroll.SmallChange = RowHeight;
            if (!_scroll.Visible) _scroll.Value = 0;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateScroll(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!_scroll.Visible) return;
            int max = Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1);
            _scroll.Value = Math.Max(0, Math.Min(max, _scroll.Value - Math.Sign(e.Delta) * RowHeight));
            Invalidate();
        }

        private int Hit(Point p) { int i = (p.Y + _scroll.Value) / RowHeight; return i >= 0 && i < _items.Count ? i : -1; }

        protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != _hover) { _hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h >= 0) { _selected = h; Invalidate(); if (FindingSelected != null) FindingSelected(_items[h]); }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette c = UiTheme.C;
            Graphics g = e.Graphics;
            g.Clear(c.Sidebar);
            Ui.HighQuality(g);
            int width = Width - (_scroll.Visible ? _scroll.Width : 0);
            for (int i = 0; i < _items.Count; i++)
            {
                int y = i * RowHeight - _scroll.Value;
                if (y + RowHeight < 0 || y > Height) continue;
                Finding f = _items[i];
                var row = new RectangleF(Ui.S(6), y + Ui.S(2), width - Ui.S(12), RowHeight - Ui.S(4));
                if (i == _selected) Ui.FillRound(g, c.AccentSoft, row, Ui.S(8f));
                else if (i == _hover) Ui.FillRound(g, c.ControlHover, row, Ui.S(8f));
                Glyph icon = f.Severity == Severity.Critical ? Glyph.Error : f.Severity == Severity.Warning ? Glyph.Warning : Glyph.Info;
                Color tint = f.Severity == Severity.Critical ? c.Danger : f.Severity == Severity.Warning ? c.Warning : c.Accent;
                int s = Ui.S(18);
                UiIcons.Draw(g, icon, new RectangleF(Ui.S(14), y + Ui.S(10), s, s), tint);
                int tx = Ui.S(40);
                string where = f.Cell.HasValue ? f.Cell.Value.X + ", " + f.Cell.Value.Y : "";
                Size ws = TextRenderer.MeasureText(where, Ui.Small);
                Ui.DrawText(g, where, Ui.Small, c.TextTertiary, new Rectangle(width - Ui.S(14) - ws.Width, y + Ui.S(9), ws.Width, Ui.S(18)), TextFormatFlags.Right);
                Ui.DrawText(g, f.Message, Ui.Body, c.Text, new Rectangle(tx, y + Ui.S(8), width - tx - Ui.S(20) - ws.Width, RowHeight - Ui.S(14)),
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
