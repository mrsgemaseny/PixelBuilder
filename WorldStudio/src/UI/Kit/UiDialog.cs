using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiniLife.WorldStudio.UI.Kit
{
    // Базовое окно диалога: заголовок, пояснение, содержимое, панель кнопок справа снизу.
    public class UiDialog : Form
    {
        private readonly UiSurface _footer = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Bottom, LineTop = true };
        private readonly FlowLayoutPanel _buttons = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, BackColor = Color.Transparent };
        private readonly UiSurface _header = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Top };

        public UiDialog(string title, string subtitle, Size size)
        {
            Text = title;
            Font = Ui.Body;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = Ui.S(size.Width, size.Height);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;

            _header.Padding = new Padding(Ui.S(20), Ui.S(16), Ui.S(20), Ui.S(4));
            var titleLabel = new UiLabel(title, TextRole.Title) { Dock = DockStyle.Top, Height = Ui.S(26) };
            _header.Controls.Add(titleLabel);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = new UiLabel(subtitle, TextRole.Secondary) { Dock = DockStyle.Top, AutoSize = false };
                sub.Height = MeasureHeight(subtitle, Ui.Small, ClientSize.Width - Ui.S(40)) + Ui.S(6);
                _header.Controls.Add(sub);
                sub.BringToFront();
            }
            int headerHeight = Ui.S(16 + 26 + 8);
            foreach (Control c in _header.Controls) if (c is UiLabel && ((UiLabel)c).Role == TextRole.Secondary) headerHeight += c.Height;
            _header.Height = headerHeight;

            _footer.Height = Ui.S(58);
            _footer.Padding = new Padding(Ui.S(16), Ui.S(13), Ui.S(16), Ui.S(13));
            _footer.Controls.Add(_buttons);

            Body = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Fill, Padding = new Padding(Ui.S(20), Ui.S(8), Ui.S(20), Ui.S(12)) };
            Controls.Add(Body);
            Controls.Add(_header);
            Controls.Add(_footer);
            UiTheme.Apply(this);
        }

        public UiSurface Body { get; private set; }

        public UiButton AddButton(string text, ButtonKind kind, DialogResult result, Action onClick)
        {
            var b = new UiButton(text, kind) { Width = Math.Max(Ui.S(96), TextRenderer.MeasureText(text, Ui.BodyStrong).Width + Ui.S(32)), Margin = new Padding(Ui.S(8), 0, 0, 0) };
            if (onClick != null) b.Click += (s, e) => onClick();
            else b.DialogResult = result;
            if (result == DialogResult.Cancel && onClick == null) CancelButton = b;
            _buttons.Controls.Add(b);
            return b;
        }

        // Кнопка слева в подвале: вспомогательные действия (импорт, экспорт), не закрывающие окно.
        private FlowLayoutPanel _leading;

        public void AddLeading(UiButton b)
        {
            if (_leading == null)
            {
                _leading = new FlowLayoutPanel { Dock = DockStyle.Left, WrapContents = false, AutoSize = true, BackColor = Color.Transparent };
                _footer.Controls.Add(_leading);
            }
            b.Width = Math.Max(Ui.S(96), TextRenderer.MeasureText(b.Text, Ui.Body).Width + Ui.S(b.Icon != Glyph.None ? 56 : 32));
            b.Margin = new Padding(0, 0, Ui.S(8), 0);
            _leading.Controls.Add(b);
        }

        public static int MeasureHeight(string text, Font font, int width)
        {
            return TextRenderer.MeasureText(text, font, new Size(width, 10000), TextFormatFlags.WordBreak).Height;
        }
    }

    // Подтверждение с названными действиями: "Удалить", "Не сохранять", а не OK и Отмена.
    // Возвращает индекс нажатой кнопки или -1 при закрытии окна.
    public static class UiAlert
    {
        public static int Ask(IWin32Window owner, string title, string message, Glyph icon, params string[] buttons)
        {
            return AskStyled(owner, title, message, icon, -1, buttons);
        }

        // destructive - индекс кнопки, которая что-то удаляет или теряет (рисуется красным).
        public static int AskStyled(IWin32Window owner, string title, string message, Glyph icon, int destructive, params string[] buttons)
        {
            int textWidth = Ui.S(340);
            int h = UiDialog.MeasureHeight(message, Ui.Body, textWidth);
            using (var form = new UiDialog(title, null, new Size(440, 0)))
            {
                form.ClientSize = new Size(Ui.S(440), Ui.S(50 + 58 + 24) + h);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                int result = -1;
                var row = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Fill };
                var glyph = new IconView(icon) { Dock = DockStyle.Left, Width = Ui.S(40) };
                var text = new UiLabel(message, TextRole.Primary) { Dock = DockStyle.Fill };
                row.Controls.Add(text);
                row.Controls.Add(glyph);
                form.Body.Controls.Add(row);
                for (int i = buttons.Length - 1; i >= 0; i--)
                {
                    int index = i;
                    ButtonKind kind = i == destructive ? ButtonKind.Danger : i == 0 ? ButtonKind.Primary : ButtonKind.Secondary;
                    UiButton b = form.AddButton(buttons[i], kind, DialogResult.None, () => { result = index; form.Close(); });
                    if (i == 0) form.AcceptButton = b;
                }
                UiTheme.Apply(form);
                form.ShowDialog(owner);
                return result;
            }
        }

        public static void Info(IWin32Window owner, string title, string message)
        {
            Ask(owner, title, message, Glyph.Info, "Готово");
        }

        public static void Error(IWin32Window owner, string title, string message)
        {
            Ask(owner, title, message, Glyph.Error, "Понятно");
        }

        private sealed class IconView : Control
        {
            private readonly Glyph _icon;

            public IconView(Glyph icon)
            {
                _icon = icon;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(UiTheme.BackOf(this, UiTheme.C.Window));
                Color tint = _icon == Glyph.Error || _icon == Glyph.Trash ? UiTheme.C.Danger : _icon == Glyph.Warning ? UiTheme.C.Warning : UiTheme.C.Accent;
                UiIcons.Draw(e.Graphics, _icon, new RectangleF(0, 0, Ui.S(28), Ui.S(28)), tint);
            }
        }
    }
}
