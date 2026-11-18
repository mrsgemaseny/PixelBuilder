using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PixelBuilder.Kit
{
    // UiKit: общий набор интерфейса для WorldStudio и PixelBuilder (копии с разным namespace, DECISIONS D-29).
    // Тема по системной настройке Windows, светлая или тёмная; все размеры в "точках" 96 dpi, масштаб - Ui.S().

    public enum ThemeMode { System, Light, Dark }

    public sealed class UiPalette
    {
        public bool IsDark;
        public Color Window, Sidebar, Toolbar, Content, CanvasBack, MapSurface, NotLand, Grid, GridMajor;
        public Color Separator, Text, TextSecondary, TextTertiary;
        public Color Accent, OnAccent, AccentSoft, Control, ControlHover, ControlPressed, Field;
        public Color Danger, Warning, Success, ToastBack, ToastText, Selection;

        public static UiPalette Light()
        {
            return new UiPalette
            {
                IsDark = false,
                Window = Color.FromArgb(245, 245, 247), Sidebar = Color.FromArgb(250, 250, 252), Toolbar = Color.FromArgb(252, 252, 253),
                Content = Color.White, CanvasBack = Color.FromArgb(232, 232, 236), MapSurface = Color.FromArgb(244, 244, 247),
                NotLand = Color.FromArgb(221, 221, 226), Grid = Color.FromArgb(40, 0, 0, 0), GridMajor = Color.FromArgb(70, 0, 0, 0),
                Separator = Color.FromArgb(222, 222, 228), Text = Color.FromArgb(29, 29, 31), TextSecondary = Color.FromArgb(110, 110, 115),
                TextTertiary = Color.FromArgb(160, 160, 166), Accent = Color.FromArgb(0, 122, 255), OnAccent = Color.White,
                AccentSoft = Color.FromArgb(229, 240, 255), Control = Color.FromArgb(239, 239, 243), ControlHover = Color.FromArgb(229, 229, 235),
                ControlPressed = Color.FromArgb(214, 214, 222), Field = Color.White, Danger = Color.FromArgb(255, 59, 48),
                Warning = Color.FromArgb(255, 149, 0), Success = Color.FromArgb(52, 199, 89), ToastBack = Color.FromArgb(242, 29, 29, 31),
                ToastText = Color.White, Selection = Color.FromArgb(0, 122, 255)
            };
        }

        public static UiPalette Dark()
        {
            return new UiPalette
            {
                IsDark = true,
                Window = Color.FromArgb(28, 28, 30), Sidebar = Color.FromArgb(32, 32, 35), Toolbar = Color.FromArgb(38, 38, 41),
                Content = Color.FromArgb(44, 44, 46), CanvasBack = Color.FromArgb(20, 20, 22), MapSurface = Color.FromArgb(30, 30, 33),
                NotLand = Color.FromArgb(36, 36, 40), Grid = Color.FromArgb(46, 255, 255, 255), GridMajor = Color.FromArgb(80, 255, 255, 255),
                Separator = Color.FromArgb(56, 56, 60), Text = Color.FromArgb(245, 245, 247), TextSecondary = Color.FromArgb(152, 152, 157),
                TextTertiary = Color.FromArgb(105, 105, 110), Accent = Color.FromArgb(10, 132, 255), OnAccent = Color.White,
                AccentSoft = Color.FromArgb(22, 54, 92), Control = Color.FromArgb(58, 58, 60), ControlHover = Color.FromArgb(72, 72, 74),
                ControlPressed = Color.FromArgb(88, 88, 92), Field = Color.FromArgb(28, 28, 30), Danger = Color.FromArgb(255, 69, 58),
                Warning = Color.FromArgb(255, 159, 10), Success = Color.FromArgb(48, 209, 88), ToastBack = Color.FromArgb(245, 58, 58, 60),
                ToastText = Color.White, Selection = Color.FromArgb(10, 132, 255)
            };
        }
    }

    public static class UiTheme
    {
        // Фон, на котором лежит контрол: прозрачные панели пропускаются, иначе mono красит угол чёрным.
        public static Color BackOf(Control c, Color fallback)
        {
            for (Control p = c != null ? c.Parent : null; p != null; p = p.Parent)
                if (p.BackColor.A == 255) return p.BackColor;
            return fallback;
        }

        private static ThemeMode _mode = ThemeMode.System;
        private static UiPalette _current = Resolve(ThemeMode.System);

        public static event Action Changed;
        public static ThemeMode Mode { get { return _mode; } }
        public static UiPalette Current { get { return _current; } }
        public static UiPalette C { get { return _current; } }

        public static void SetMode(ThemeMode mode)
        {
            _mode = mode;
            _current = Resolve(mode);
            Action h = Changed;
            if (h != null) h();
        }

        private static UiPalette Resolve(ThemeMode mode)
        {
            bool dark = mode == ThemeMode.Dark || (mode == ThemeMode.System && SystemIsDark());
            return dark ? UiPalette.Dark() : UiPalette.Light();
        }

        // Настройка "Режим приложения" Windows 10/11. Нет ключа (старая Windows, mono) - светлая.
        public static bool SystemIsDark()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    return v is int && (int)v == 0;
                }
            }
            catch (Exception) { return false; }
        }

        // Стандартные элементы получают цвета темы; свои элементы рисуют себя сами из UiTheme.C.
        public static void Apply(Control root)
        {
            UiPalette c = _current;
            var surface = root as UiSurface;
            if (surface != null) surface.BackColor = surface.RoleColor();
            else if (root is UiLabel) { var l = (UiLabel)root; l.ForeColor = l.RoleColor(); l.BackColor = Color.Transparent; }
            else if (root is TextBox) { root.BackColor = c.Field; root.ForeColor = c.Text; }
            else if (root is ComboBox) { root.BackColor = c.Field; root.ForeColor = c.Text; }
            else if (root is ListView) { root.BackColor = c.Content; root.ForeColor = c.Text; }
            else if (root is TabPage) { root.BackColor = c.CanvasBack; }
            else if (root is Form) { root.BackColor = c.Window; root.ForeColor = c.Text; }
            foreach (Control child in root.Controls) Apply(child);
            root.Invalidate();
        }
    }

    public static class Ui
    {
        private static float _scale = -1f;
        private static ToolTip _tips;

        public static float Scale
        {
            get
            {
                if (_scale < 0)
                {
                    try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) _scale = g.DpiX / 96f; }
                    catch (Exception) { _scale = 1f; }
                    if (_scale < 1f) _scale = 1f;
                }
                return _scale;
            }
        }

        public static int S(int px) { return (int)Math.Round(px * Scale); }
        public static float S(float px) { return px * Scale; }
        public static Size S(int w, int h) { return new Size(S(w), S(h)); }

        // Шрифты в пунктах: при включённой DPI-осведомлённости Windows сама масштабирует их.
        public static readonly string Family = PickFamily();
        public static readonly Font Body = new Font(Family, 9f, FontStyle.Regular);
        public static readonly Font BodyStrong = new Font(Family, 9f, FontStyle.Bold);
        public static readonly Font Small = new Font(Family, 8f, FontStyle.Regular);
        public static readonly Font Caption = new Font(Family, 7.75f, FontStyle.Bold);
        public static readonly Font Title = new Font(Family, 12f, FontStyle.Bold);
        public static readonly Font Large = new Font(Family, 18f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 8.5f, FontStyle.Regular);

        private static string PickFamily()
        {
            foreach (string name in new[] { "Segoe UI Variable Text", "Segoe UI" })
                foreach (FontFamily f in FontFamily.Families)
                    if (f.Name == name) return name;
            return SystemFonts.MessageBoxFont != null ? SystemFonts.MessageBoxFont.FontFamily.Name : "Arial";
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        // Чёткий текст на мониторах с масштабом 125-200%. До создания первого окна.
        public static void EnableDpiAwareness()
        {
            try { SetProcessDPIAware(); }
            catch (Exception) { /* нет user32 (mono/Linux) - масштаб 1 */ }
        }

        public static void HighQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Color color, RectangleF r, float radius)
        {
            using (GraphicsPath p = Round(r, radius))
            using (var b = new SolidBrush(color)) g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color color, RectangleF r, float radius, float width)
        {
            using (GraphicsPath p = Round(r, radius))
            using (var pen = new Pen(color, width)) g.DrawPath(pen, p);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        // Чёрный или белый текст поверх произвольного цвета.
        public static Color InkFor(Color back)
        {
            double l = 0.299 * back.R + 0.587 * back.G + 0.114 * back.B;
            return l > 150 ? Color.FromArgb(29, 29, 31) : Color.White;
        }

        public static ToolTip Tips
        {
            get
            {
                if (_tips == null) _tips = new ToolTip { InitialDelay = 450, ReshowDelay = 100, AutoPopDelay = 12000, ShowAlways = true };
                return _tips;
            }
        }

        // Подсказка: что делает элемент и его горячая клавиша.
        public static void Tip(Control c, string text, string shortcut)
        {
            Tips.SetToolTip(c, string.IsNullOrEmpty(shortcut) ? text : text + "   " + shortcut);
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, r, color, flags | TextFormatFlags.NoPrefix);
        }
    }
}
