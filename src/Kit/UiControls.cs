using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PixelBuilder.Kit
{
    public enum SurfaceRole { Window, Sidebar, Toolbar, Content, Canvas }

    // Панель с цветом по роли и тонкими разделителями по краям.
    public class UiSurface : Panel
    {
        public UiSurface(SurfaceRole role)
        {
            Role = role;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = RoleColor();
        }

        public SurfaceRole Role { get; set; }
        public bool LineTop { get; set; }
        public bool LineBottom { get; set; }
        public bool LineLeft { get; set; }
        public bool LineRight { get; set; }

        public Color RoleColor()
        {
            UiPalette c = UiTheme.C;
            switch (Role)
            {
                case SurfaceRole.Sidebar: return c.Sidebar;
                case SurfaceRole.Toolbar: return c.Toolbar;
                case SurfaceRole.Content: return c.Content;
                case SurfaceRole.Canvas: return c.CanvasBack;
                default: return c.Window;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(UiTheme.C.Separator, 1f))
            {
                if (LineTop) e.Graphics.DrawLine(pen, 0, 0, Width, 0);
                if (LineBottom) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
                if (LineLeft) e.Graphics.DrawLine(pen, 0, 0, 0, Height);
                if (LineRight) e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            }
        }
    }

    public enum TextRole { Primary, Secondary, Tertiary, Caption, Title, Large, Accent, Danger, Warning }

    public class UiLabel : Label
    {
        private TextRole _role;

        public UiLabel(string text, TextRole role)
        {
            Text = text;
            UseMnemonic = false;
            BackColor = Color.Transparent;
            Role = role;
        }

        public TextRole Role
        {
            get { return _role; }
            set
            {
                _role = value;
                Font = value == TextRole.Caption ? Ui.Caption : value == TextRole.Title ? Ui.Title : value == TextRole.Large ? Ui.Large
                     : value == TextRole.Secondary || value == TextRole.Tertiary ? Ui.Small : Ui.Body;
                ForeColor = RoleColor();
            }
        }

        public Color RoleColor()
        {
            UiPalette c = UiTheme.C;
            switch (_role)
            {
                case TextRole.Secondary: return c.TextSecondary;
                case TextRole.Tertiary:
                case TextRole.Caption: return c.TextTertiary;
                case TextRole.Accent: return c.Accent;
                case TextRole.Danger: return c.Danger;
                case TextRole.Warning: return c.Warning;
                default: return c.Text;
            }
        }
    }

    public enum ButtonKind { Secondary, Primary, Ghost, Danger, Tool, Row, Card }

    // Кнопка, нарисованная целиком: капсула, иконка, цветная плашка, вторая строка справа.
    // Наследник Button: PerformClick, фокус с клавиатуры и тесты PixelBuilder продолжают работать.
    public class UiButton : Button
    {
        private bool _hover;
        private bool _down;
        private bool _checked;

        public UiButton(string text, ButtonKind kind)
        {
            Text = text;
            Kind = kind;
            Radius = kind == ButtonKind.Card ? 10 : 7;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Ui.Body;
            Cursor = Cursors.Hand;
            UseMnemonic = false;
            Height = Ui.S(kind == ButtonKind.Row ? 30 : 30);
        }

        public ButtonKind Kind { get; set; }
        public Glyph Icon { get; set; }
        public bool IconOnly { get; set; }
        public int Radius { get; set; }
        // Строка-значение справа (Row) или подпись под заголовком (Card).
        public string Detail { get; set; }
        public Color? Swatch { get; set; }
        public bool SwatchHatched { get; set; }
        // Короткий знак внутри плашки (символ объекта карты).
        public string SwatchLabel { get; set; }
        // Отрисовка превью внутри карточки (силуэт дома и т.п.).
        public Action<Graphics, Rectangle> Preview { get; set; }

        // Выбранное состояние: инструмент, строка палитры, карточка.
        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            UiPalette c = UiTheme.C;
            Color parent = UiTheme.BackOf(this, c.Window);
            g.Clear(parent);
            Ui.HighQuality(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float radius = Ui.S((float)Radius);

            Color back, ink = c.Text, border = Color.Empty;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    back = _down ? Ui.Mix(c.Accent, Color.Black, 0.18f) : _hover ? Ui.Mix(c.Accent, Color.White, 0.12f) : c.Accent;
                    ink = c.OnAccent;
                    break;
                case ButtonKind.Danger:
                    back = _down ? c.ControlPressed : _hover ? c.ControlHover : c.Control;
                    ink = c.Danger;
                    break;
                case ButtonKind.Ghost:
                case ButtonKind.Tool:
                case ButtonKind.Row:
                    back = _checked ? c.AccentSoft : _down ? c.ControlPressed : _hover ? c.ControlHover : parent;
                    ink = _checked && Kind != ButtonKind.Row ? c.Accent : c.Text;
                    break;
                case ButtonKind.Card:
                    back = _down ? c.ControlPressed : _hover ? c.ControlHover : c.Content;
                    border = _checked ? c.Accent : c.Separator;
                    break;
                default:
                    back = _down ? c.ControlPressed : _hover ? c.ControlHover : c.Control;
                    break;
            }
            if (!Enabled) { ink = c.TextTertiary; if (Kind == ButtonKind.Primary) back = c.Control; }

            Ui.FillRound(g, back, r, radius);
            if (border != Color.Empty) Ui.StrokeRound(g, border, r, radius, _checked ? Ui.S(2f) : 1f);
            if (Focused && ShowFocusCues) Ui.StrokeRound(g, Color.FromArgb(150, c.Accent), r, radius, Ui.S(2f));

            if (Kind == ButtonKind.Card) { PaintCard(g, c, ink); return; }

            int pad = Ui.S(Kind == ButtonKind.Row ? 8 : 10);
            int x = pad;
            int iconSize = Ui.S(18);
            if (Swatch.HasValue)
            {
                int s = Ui.S(string.IsNullOrEmpty(SwatchLabel) ? 16 : 20);
                var sw = new RectangleF(x, (Height - s) / 2f, s, s);
                Ui.FillRound(g, Swatch.Value, sw, Ui.S(4f));
                if (!string.IsNullOrEmpty(SwatchLabel))
                    using (var f = new Font(Ui.Family, 7f, FontStyle.Bold))
                        Ui.DrawText(g, SwatchLabel, f, Ui.InkFor(Swatch.Value), Rectangle.Round(sw), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (SwatchHatched)
                    using (var hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(120, Ui.InkFor(Swatch.Value)), Color.Transparent))
                    using (GraphicsPath p = Ui.Round(sw, Ui.S(4f))) g.FillPath(hatch, p);
                Ui.StrokeRound(g, Color.FromArgb(40, 0, 0, 0), sw, Ui.S(4f), 1f);
                x += s + Ui.S(8);
            }
            if (Icon != Glyph.None)
            {
                if (IconOnly || string.IsNullOrEmpty(Text))
                {
                    UiIcons.Draw(g, Icon, new RectangleF((Width - iconSize) / 2f, (Height - iconSize) / 2f, iconSize, iconSize), ink);
                    return;
                }
                UiIcons.Draw(g, Icon, new RectangleF(x, (Height - iconSize) / 2f, iconSize, iconSize), ink);
                x += iconSize + Ui.S(6);
            }
            bool left = Kind == ButtonKind.Row || Swatch.HasValue || Icon != Glyph.None;
            var textRect = new Rectangle(left ? x : 0, 0, (left ? Width - x - pad : Width), Height);
            if (!string.IsNullOrEmpty(Detail) && Kind == ButtonKind.Row)
            {
                Size ds = TextRenderer.MeasureText(Detail, Ui.Small);
                Ui.DrawText(g, Detail, Ui.Small, c.TextTertiary, new Rectangle(Width - pad - ds.Width, 0, ds.Width, Height),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                textRect.Width -= ds.Width + Ui.S(6);
            }
            Ui.DrawText(g, Text, Kind == ButtonKind.Primary || _checked && Kind != ButtonKind.Row ? Ui.BodyStrong : Font, ink, textRect,
                (left ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void PaintCard(Graphics g, UiPalette c, Color ink)
        {
            int pad = Ui.S(8);
            int textH = Ui.S(string.IsNullOrEmpty(Detail) ? 18 : 32);
            var preview = new Rectangle(pad, pad, Width - 2 * pad, Height - 2 * pad - textH);
            if (Preview != null && preview.Height > 4) Preview(g, preview);
            else if (Icon != Glyph.None)
            {
                // Иконка в круглой подложке, а не на всю карточку: так читается как кнопка-действие.
                int disc = Math.Min(Ui.S(56), Math.Min(preview.Width, preview.Height));
                var circle = new Rectangle(preview.X + (preview.Width - disc) / 2, preview.Y + (preview.Height - disc) / 2, disc, disc);
                using (var b = new SolidBrush(c.AccentSoft)) g.FillEllipse(b, circle);
                int s = disc * 11 / 20;
                UiIcons.Draw(g, Icon, new RectangleF(circle.X + (disc - s) / 2f, circle.Y + (disc - s) / 2f, s, s), c.Accent);
            }
            int ty = Height - pad - textH;
            Ui.DrawText(g, Text, Ui.BodyStrong, ink, new Rectangle(2, ty, Width - 4, Ui.S(17)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            if (!string.IsNullOrEmpty(Detail))
                Ui.DrawText(g, Detail, Ui.Small, c.TextSecondary, new Rectangle(2, ty + Ui.S(16), Width - 4, Ui.S(16)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Сегментный переключатель (вместо вкладок и групп радиокнопок).
    public class UiSegmented : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _selected;
        private int _hover = -1;

        public UiSegmented(params string[] items)
        {
            _items.AddRange(items);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = Ui.Body;
            Height = Ui.S(30);
            Cursor = Cursors.Hand;
            Width = PreferredWidth();
        }

        public event Action SelectedIndexChanged;
        public IList<string> Items { get { return _items.AsReadOnly(); } }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value < 0 || value >= _items.Count || value == _selected) return;
                _selected = value;
                Invalidate();
                Action h = SelectedIndexChanged;
                if (h != null) h();
            }
        }

        public int PreferredWidth()
        {
            int w = Ui.S(6);
            foreach (string s in _items) w += TextRenderer.MeasureText(s, Ui.BodyStrong).Width + Ui.S(22);
            return w;
        }

        private RectangleF Segment(int i)
        {
            float inner = Width - Ui.S(4f);
            float total = 0;
            var widths = new float[_items.Count];
            for (int k = 0; k < _items.Count; k++) { widths[k] = TextRenderer.MeasureText(_items[k], Ui.BodyStrong).Width + Ui.S(22f); total += widths[k]; }
            float x = Ui.S(2f);
            for (int k = 0; k < i; k++) x += widths[k] * inner / total;
            return new RectangleF(x, Ui.S(2f), widths[i] * inner / total, Height - Ui.S(4f));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            UiPalette c = UiTheme.C;
            g.Clear(UiTheme.BackOf(this, c.Window));
            Ui.HighQuality(g);
            Ui.FillRound(g, c.Control, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(8f));
            for (int i = 0; i < _items.Count; i++)
            {
                RectangleF r = Segment(i);
                if (i == _selected)
                {
                    Ui.FillRound(g, c.IsDark ? c.ControlHover : c.Content, r, Ui.S(6f));
                    Ui.StrokeRound(g, Color.FromArgb(c.IsDark ? 0 : 28, 0, 0, 0), r, Ui.S(6f), 1f);
                }
                else if (i == _hover) Ui.FillRound(g, c.ControlHover, r, Ui.S(6f));
                Ui.DrawText(g, _items[i], i == _selected ? Ui.BodyStrong : Ui.Body, i == _selected ? c.Text : c.TextSecondary,
                    Rectangle.Round(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _items.Count; i++) if (Segment(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Location); if (h != _hover) { _hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { int h = HitTest(e.Location); if (h >= 0) SelectedIndex = h; base.OnMouseDown(e); }

        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) SelectedIndex = Math.Max(0, _selected - 1);
            if (e.KeyCode == Keys.Right) SelectedIndex = Math.Min(_items.Count - 1, _selected + 1);
            base.OnKeyDown(e);
        }
    }

    public enum ToastKind { Info, Success, Warning, Error }

    // Короткое сообщение поверх карты, само исчезает. Заменяет MessageBox для некритичных сообщений.
    public class UiToast : Control
    {
        private readonly Timer _timer = new Timer();
        private ToastKind _kind;

        public UiToast()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Visible = false;
            Font = Ui.Body;
            _timer.Tick += (s, e) => { _timer.Stop(); Visible = false; };
        }

        // Где показывать внутри родителя (например, только над картой). Пусто - весь родитель.
        public Rectangle? Area { get; set; }

        public void Show(string text, ToastKind kind)
        {
            if (Parent == null || string.IsNullOrEmpty(text)) return;
            Text = text;
            _kind = kind;
            Size measured = TextRenderer.MeasureText(text, Font);
            Rectangle area = Area.HasValue ? Area.Value : new Rectangle(0, 0, Parent.Width, Parent.Height);
            int w = Math.Min(area.Width - Ui.S(40), measured.Width + Ui.S(64));
            int lines = Math.Max(1, (int)Math.Ceiling(measured.Width / (double)Math.Max(1, w - Ui.S(64))));
            int h = Ui.S(20) + lines * measured.Height + Ui.S(4);
            Bounds = new Rectangle(area.X + (area.Width - w) / 2, area.Bottom - h - Ui.S(18), w, h);
            using (GraphicsPath p = Ui.Round(new RectangleF(0, 0, w, h), h / 2f > Ui.S(18f) ? Ui.S(14f) : h / 2f)) Region = new Region(p);
            Visible = true;
            BringToFront();
            Invalidate();
            _timer.Stop();
            _timer.Interval = Math.Max(2600, Math.Min(7000, text.Length * 70));
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            UiPalette c = UiTheme.C;
            Ui.HighQuality(g);
            g.Clear(Color.FromArgb(255, c.ToastBack));
            Glyph icon = _kind == ToastKind.Error ? Glyph.Error : _kind == ToastKind.Warning ? Glyph.Warning : _kind == ToastKind.Success ? Glyph.Check : Glyph.Info;
            Color tint = _kind == ToastKind.Error ? c.Danger : _kind == ToastKind.Warning ? c.Warning : _kind == ToastKind.Success ? c.Success : Color.FromArgb(200, 255, 255, 255);
            int s = Ui.S(18);
            UiIcons.Draw(g, icon, new RectangleF(Ui.S(16), (Height - s) / 2f, s, s), tint);
            Ui.DrawText(g, Text, Font, c.ToastText, new Rectangle(Ui.S(42), 0, Width - Ui.S(56), Height),
                TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        protected override void OnClick(EventArgs e) { Visible = false; base.OnClick(e); }
    }

    // Поле ввода в скруглённой рамке.
    public class UiTextField : UiSurface
    {
        private readonly TextBox _box;

        public UiTextField(bool multiline) : base(SurfaceRole.Content)
        {
            _box = new TextBox { BorderStyle = BorderStyle.None, Multiline = multiline, Font = Ui.Body, Dock = DockStyle.Fill };
            Padding = new Padding(Ui.S(8), Ui.S(multiline ? 6 : 7), Ui.S(8), Ui.S(4));
            Controls.Add(_box);
            Height = Ui.S(multiline ? 64 : 30);
            _box.GotFocus += (s, e) => Invalidate();
            _box.LostFocus += (s, e) => Invalidate();
        }

        public TextBox Box { get { return _box; } }
        public override string Text { get { return _box.Text; } set { _box.Text = value; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette c = UiTheme.C;
            BackColor = UiTheme.BackOf(this, c.Content);
            _box.BackColor = c.Field;
            _box.ForeColor = c.Text;
            Graphics g = e.Graphics;
            Ui.HighQuality(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Ui.FillRound(g, c.Field, r, Ui.S(7f));
            Ui.StrokeRound(g, _box.Focused ? c.Accent : c.Separator, r, Ui.S(7f), _box.Focused ? Ui.S(1.6f) : 1f);
        }
    }

    // Меню в цветах темы (контекстные меню и меню "...").
    public class UiMenuRenderer : ToolStripProfessionalRenderer
    {
        public UiMenuRenderer() : base(new UiMenuColors()) { RoundedEdges = true; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? UiTheme.C.Text : UiTheme.C.TextTertiary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) { base.OnRenderMenuItemBackground(e); return; }
            Ui.HighQuality(e.Graphics);
            var r = new RectangleF(Ui.S(4f), 1, e.Item.Width - Ui.S(8f), e.Item.Height - 2);
            Ui.FillRound(e.Graphics, UiTheme.C.AccentSoft, r, Ui.S(5f));
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = UiTheme.C.TextSecondary;
            base.OnRenderArrow(e);
        }

        public static ContextMenuStrip Menu()
        {
            var menu = new ContextMenuStrip { Renderer = new UiMenuRenderer(), Font = Ui.Body, ShowImageMargin = false, Padding = new Padding(Ui.S(4)) };
            menu.Opening += (s, e) => { menu.BackColor = UiTheme.C.Content; menu.ForeColor = UiTheme.C.Text; };
            return menu;
        }

        public static ToolStripMenuItem Item(ToolStripItemCollection items, string text, string shortcut, Action action)
        {
            var item = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcut, Padding = new Padding(Ui.S(6), Ui.S(3), Ui.S(6), Ui.S(3)) };
            if (action != null) item.Click += (s, e) => action();
            items.Add(item);
            return item;
        }
    }

    internal sealed class UiMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return UiTheme.C.Content; } }
        public override Color MenuBorder { get { return UiTheme.C.Separator; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return UiTheme.C.AccentSoft; } }
        public override Color ImageMarginGradientBegin { get { return UiTheme.C.Content; } }
        public override Color ImageMarginGradientMiddle { get { return UiTheme.C.Content; } }
        public override Color ImageMarginGradientEnd { get { return UiTheme.C.Content; } }
        public override Color SeparatorDark { get { return UiTheme.C.Separator; } }
        public override Color SeparatorLight { get { return UiTheme.C.Separator; } }
    }

    // Заголовок раздела боковой панели: подпись мелким шрифтом и необязательная кнопка справа.
    public class UiSectionHeader : UiSurface
    {
        private readonly UiLabel _label;

        public UiSectionHeader(string text, SurfaceRole role) : base(role)
        {
            Height = Ui.S(30);
            Dock = DockStyle.Top;
            _label = new UiLabel((text ?? "").ToUpperInvariant(), TextRole.Caption) { Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(Ui.S(4), 0, 0, Ui.S(5)) };
            Controls.Add(_label);
        }

        public override string Text { get { return _label != null ? _label.Text : base.Text; } set { if (_label != null) _label.Text = (value ?? "").ToUpperInvariant(); else base.Text = value; } }

        public void AddTrailing(Control c)
        {
            c.Dock = DockStyle.Right;
            Controls.Add(c);
            c.BringToFront();
            _label.BringToFront();
            _label.SendToBack();
        }
    }
}
