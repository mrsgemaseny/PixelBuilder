using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace PixelBuilder.Kit
{
    // Группа строк на скруглённой подложке (как в WorldStudio Views.cs, копия для PixelBuilder).
    public sealed class UiGroup : UiSurface
    {
        public UiGroup() : base(SurfaceRole.Sidebar)
        {
            Dock = DockStyle.Top;
            Padding = new Padding(Ui.S(1));
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

    // Квадратная плашка цвета или блока. Наследует Button: клик, PerformClick и словарь кнопок палитры работают как раньше.
    public sealed class UiSwatchTile : Button
    {
        private bool _hover;
        private bool _checked;

        public UiSwatchTile(Color color, string label)
        {
            Swatch = color;
            Label = label;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Size = Ui.S(28, 28);
            Text = "";
        }

        public Color Swatch { get; set; }
        public string Label { get; set; }

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette c = UiTheme.C;
            Graphics g = e.Graphics;
            g.Clear(UiTheme.BackOf(this, c.Sidebar));
            Ui.HighQuality(g);
            float inset = _checked ? Ui.S(3.5f) : _hover ? Ui.S(1.5f) : Ui.S(2.5f);
            var r = new RectangleF(inset, inset, Width - 2 * inset - 1, Height - 2 * inset - 1);
            Ui.FillRound(g, Swatch, r, Ui.S(6f));
            Ui.StrokeRound(g, Color.FromArgb(c.IsDark ? 60 : 40, c.IsDark ? Color.White : Color.Black), r, Ui.S(6f), 1f);
            if (_checked)
                Ui.StrokeRound(g, c.Accent, new RectangleF(0.75f, 0.75f, Width - 2.5f, Height - 2.5f), Ui.S(8f), Ui.S(2f));
            if (!string.IsNullOrEmpty(Label))
                using (var f = new Font(Ui.Family, 7.5f, FontStyle.Bold))
                    Ui.DrawText(g, Label, f, Ui.InkFor(Swatch), Rectangle.Round(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // Настройки PixelBuilder: только тема, в %APPDATA%\PixelBuilder. Ошибки записи не мешают работе.
    public static class UiSettings
    {
        private static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelBuilder"); }
        }

        public static ThemeMode LoadTheme()
        {
            try
            {
                string path = Path.Combine(Dir, "theme.txt");
                if (!File.Exists(path)) return ThemeMode.System;
                string text = File.ReadAllText(path).Trim();
                if (text == "light") return ThemeMode.Light;
                if (text == "dark") return ThemeMode.Dark;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return ThemeMode.System;
        }

        public static void SaveTheme(ThemeMode mode)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, "theme.txt"), mode == ThemeMode.Light ? "light" : mode == ThemeMode.Dark ? "dark" : "system");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
