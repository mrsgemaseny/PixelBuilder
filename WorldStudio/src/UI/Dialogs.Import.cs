using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.Json;
using MiniLife.WorldStudio.Serialization.PixelArchitect;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    // Сборка мира из старых файлов. Три карточки: смысл файла задаёт карточка, а не цвет (DECISIONS D-05).
    // Файл читается сразу при выборе и показывается превью, чтобы глазами проверить, что он тот.
    internal sealed class ImportForm : UiDialog
    {
        private readonly List<SourceCard> _cards = new List<SourceCard>();
        private readonly List<LegacySource> _sources = new List<LegacySource>();
        private readonly UiButton _build;

        public ImportForm() : base(Strings.ImportTitle, Strings.ImportExplain, new Size(780, 520))
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
            row.Controls.Add(Card(MapSemantic.Landscape, "Карта ландшафтов", "Равнины, лес, горы, пески"));
            row.Controls.Add(Card(MapSemantic.Political, "Политическая карта", "Государства и их поселения"));
            row.Controls.Add(Card(MapSemantic.Wildlands, "Дикие земли", "Д - независимые поселения, Г - армии"));
            Body.Controls.Add(row);
            AddButton(Strings.Cancel, ButtonKind.Secondary, DialogResult.Cancel, null);
            _build = AddButton(Strings.ImportBuild, ButtonKind.Primary, DialogResult.None, Accept);
            AcceptButton = _build;
            LoadDefaultSources();
            UpdateBuild();
            UiTheme.Apply(this);
        }

        // Основные карты из data/sources.json подставляются сразу; карточку можно заменить или очистить.
        // Нет файла настроек или карты на диске - карточка остаётся пустой, без ошибки.
        private void LoadDefaultSources()
        {
            string path = AppPaths.SourcesPath;
            if (!File.Exists(path)) return;
            try
            {
                JsonObject root = JsonRead.Object(JsonParser.Parse(TextFileStore.ReadAllText(path)), "sources.json");
                foreach (JsonNode item in JsonRead.RequiredArray(root, "sources", "sources.json").Items)
                {
                    JsonObject o = JsonRead.Object(item, "sources[]");
                    string semantic = JsonRead.RequiredString(o, "semantic", "sources[].semantic");
                    string file = JsonRead.RequiredString(o, "path", "sources[].path");
                    foreach (SourceCard card in _cards)
                        if (Legend.SemanticCode(card.Semantic) == semantic && File.Exists(file)) card.LoadPath(file);
                }
            }
            catch (JsonParseException ex) { ErrorLog.Write("data/sources.json не прочитан", ex); }
            catch (JsonSchemaException ex) { ErrorLog.Write("data/sources.json не прочитан", ex); }
        }

        public IList<LegacySource> Sources { get { return _sources.AsReadOnly(); } }

        private SourceCard Card(MapSemantic semantic, string title, string hint)
        {
            var card = new SourceCard(semantic, title, hint) { Size = Ui.S(236, 330), Margin = new Padding(0, Ui.S(4), Ui.S(10), 0) };
            card.Changed += UpdateBuild;
            _cards.Add(card);
            return card;
        }

        private void UpdateBuild()
        {
            int ready = 0;
            foreach (SourceCard c in _cards) if (c.Document != null) ready++;
            _build.Text = ready == 0 ? Strings.ImportBuild : Strings.ImportBuild + " (" + ready + ")";
            _build.Invalidate();
        }

        private void Accept()
        {
            _sources.Clear();
            foreach (SourceCard c in _cards)
                if (c.Document != null) _sources.Add(new LegacySource(c.Semantic, c.FileName, c.Document));
            if (_sources.Count == 0) { UiAlert.Info(this, Strings.ImportTitle, Strings.ImportNeedOne); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // Карточка одного файла: заголовок, превью или зона перетаскивания, имя файла, кнопки.
    internal sealed class SourceCard : Control
    {
        private readonly string _title;
        private readonly string _hint;
        private readonly UiButton _choose;
        private readonly UiButton _clear;
        private string _error;
        private bool _dragOver;

        public SourceCard(MapSemantic semantic, string title, string hint)
        {
            Semantic = semantic;
            _title = title;
            _hint = hint;
            AllowDrop = true;
            BackColor = UiTheme.C.Content; // фон для углов кнопок внутри карточки
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _choose = new UiButton("Выбрать файл", ButtonKind.Secondary) { Height = Ui.S(30) };
            _clear = new UiButton("", ButtonKind.Tool) { Icon = Glyph.Close, IconOnly = true, Size = Ui.S(30, 30), Visible = false };
            _choose.Click += (s, e) => Choose();
            _clear.Click += (s, e) => { Document = null; FileName = null; _error = null; _clear.Visible = false; _choose.Text = "Выбрать файл"; Raise(); };
            Ui.Tip(_clear, "Убрать файл", null);
            Controls.Add(_choose);
            Controls.Add(_clear);
        }

        public event Action Changed;
        public MapSemantic Semantic { get; private set; }
        public PixelArchitectDocument Document { get; private set; }
        public string FileName { get; private set; }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int pad = Ui.S(12);
            _clear.Location = new Point(Width - pad - _clear.Width, Height - pad - _clear.Height);
            _choose.Bounds = new Rectangle(pad, Height - pad - _choose.Height, Width - 2 * pad - (_clear.Visible ? _clear.Width + Ui.S(6) : 0), _choose.Height);
        }

        private void Choose()
        {
            using (var ofd = new OpenFileDialog { Filter = Strings.FilterLegacy })
                if (ofd.ShowDialog(FindForm()) == DialogResult.OK) Load(ofd.FileName);
        }

        // Файл читается целиком и проверяется сразу: битый файл не попадает в сборку (раздел 9).
        internal void LoadPath(string path) { Load(path); }

        private void Load(string path)
        {
            _error = null;
            try
            {
                PixelArchitectReadResult r = PixelArchitectCodec.Read(TextFileStore.ReadAllText(path));
                if (!r.Ok) { _error = r.Error; Document = null; }
                else if (r.Document.MapKind != "global") { _error = "Это не глобальная карта"; Document = null; }
                else Document = r.Document;
            }
            catch (Exception ex) { _error = "Не удалось прочитать: " + ex.Message; Document = null; }
            FileName = Path.GetFileName(path);
            _clear.Visible = true;
            _choose.Text = "Заменить";
            OnResize(EventArgs.Empty);
            Raise();
        }

        private void Raise()
        {
            Invalidate();
            if (Changed != null) Changed();
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; _dragOver = true; Invalidate(); }
            base.OnDragEnter(e);
        }

        protected override void OnDragLeave(EventArgs e) { _dragOver = false; Invalidate(); base.OnDragLeave(e); }

        protected override void OnDragDrop(DragEventArgs e)
        {
            _dragOver = false;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) Load(files[0]);
            base.OnDragDrop(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiPalette c = UiTheme.C;
            Graphics g = e.Graphics;
            g.Clear(UiTheme.BackOf(this, c.Window));
            Ui.HighQuality(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Ui.FillRound(g, c.Content, r, Ui.S(12f));
            Ui.StrokeRound(g, Document != null ? c.Accent : _dragOver ? c.Accent : c.Separator, r, Ui.S(12f), Document != null || _dragOver ? Ui.S(2f) : 1f);
            int pad = Ui.S(14);
            Ui.DrawText(g, _title, Ui.BodyStrong, c.Text, new Rectangle(pad, pad, Width - 2 * pad, Ui.S(20)), TextFormatFlags.EndEllipsis);
            Ui.DrawText(g, _hint, Ui.Small, c.TextSecondary, new Rectangle(pad, pad + Ui.S(20), Width - 2 * pad, Ui.S(18)), TextFormatFlags.EndEllipsis);

            var preview = new Rectangle(pad, pad + Ui.S(48), Width - 2 * pad, Ui.S(170));
            if (Document != null) DrawPreview(g, preview, c);
            else
            {
                using (var pen = new Pen(_dragOver ? c.Accent : c.TextTertiary, 1f) { DashPattern = new[] { 4f, 3f } })
                using (GraphicsPath path = Ui.Round(preview, Ui.S(10f))) g.DrawPath(pen, path);
                int s = Ui.S(30);
                UiIcons.Draw(g, Glyph.Import, new RectangleF(preview.X + (preview.Width - s) / 2f, preview.Y + Ui.S(46), s, s), _dragOver ? c.Accent : c.TextTertiary);
                Ui.DrawText(g, "Перетащите файл сюда", Ui.Small, c.TextSecondary, new Rectangle(preview.X, preview.Y + Ui.S(86), preview.Width, Ui.S(18)), TextFormatFlags.HorizontalCenter);
            }

            int infoY = preview.Bottom + Ui.S(10);
            if (_error != null)
                Ui.DrawText(g, _error, Ui.Small, c.Danger, new Rectangle(pad, infoY, Width - 2 * pad, Ui.S(54)), TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            else if (Document != null)
            {
                Ui.DrawText(g, FileName, Ui.Body, c.Text, new Rectangle(pad, infoY, Width - 2 * pad, Ui.S(18)), TextFormatFlags.EndEllipsis);
                PixelLayer objects = Document.GetLayer(PixelArchitectDocument.LayerObjects);
                string stats = Labels.Plural(Document.CountStandardBlocks() - (objects != null ? objects.Count : 0), "клетка", "клетки", "клеток") +
                               (objects != null && objects.Count > 0 ? ", " + Labels.Plural(objects.Count, "объект", "объекта", "объектов") : "");
                Ui.DrawText(g, stats, Ui.Small, c.TextSecondary, new Rectangle(pad, infoY + Ui.S(19), Width - 2 * pad, Ui.S(18)), TextFormatFlags.EndEllipsis);
            }
            else Ui.DrawText(g, "Файл не выбран", Ui.Small, c.TextTertiary, new Rectangle(pad, infoY, Width - 2 * pad, Ui.S(18)), 0);
        }

        // Превью только для глаз: цвета берутся из значений файла как есть.
        private void DrawPreview(Graphics g, Rectangle box, UiPalette c)
        {
            Ui.FillRound(g, c.NotLand, box, Ui.S(10f));
            int w = Document.Width, h = Document.Height;
            float cell = Math.Min((box.Width - Ui.S(12)) / (float)w, (box.Height - Ui.S(12)) / (float)h);
            float ox = box.X + (box.Width - cell * w) / 2f, oy = box.Y + (box.Height - cell * h) / 2f;
            g.SmoothingMode = SmoothingMode.None;
            foreach (string layerName in new[] { PixelArchitectDocument.LayerLandscape, PixelArchitectDocument.LayerPolitical })
            {
                PixelLayer layer = Document.GetLayer(layerName);
                if (layer == null) continue;
                foreach (PixelCell pc in layer.Cells)
                    using (var b = new SolidBrush(ColorOf(pc.Value))) g.FillRectangle(b, ox + pc.Cell.X * cell, oy + pc.Cell.Y * cell, cell - 1, cell - 1);
            }
            PixelLayer objects = Document.GetLayer(PixelArchitectDocument.LayerObjects);
            if (objects == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float d = Math.Max(3f, cell * 0.42f);
            foreach (PixelCell pc in objects.Cells)
            {
                bool army = pc.Value == "imperial_army" || (pc.Value == "settlement_city" && Semantic == MapSemantic.Wildlands);
                Color dot = army ? Color.FromArgb(220, 40, 40) : Color.White;
                using (var b = new SolidBrush(dot))
                    g.FillEllipse(b, ox + pc.Cell.X * cell + (cell - d) / 2f, oy + pc.Cell.Y * cell + (cell - d) / 2f, d, d);
                using (var pen = new Pen(Color.FromArgb(120, 0, 0, 0), 1f))
                    g.DrawEllipse(pen, ox + pc.Cell.X * cell + (cell - d) / 2f, oy + pc.Cell.Y * cell + (cell - d) / 2f, d, d);
            }
        }

        private static Color ColorOf(string value)
        {
            if (value.StartsWith("color_") && value.Length == 12)
            {
                int rgb;
                if (int.TryParse(value.Substring(6), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb))
                    return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            }
            return value == "stone_wall" ? Color.FromArgb(70, 72, 78) : Color.FromArgb(150, 150, 155);
        }
    }

    // История версий world.json и откат (M6).
    internal sealed class HistoryForm : UiDialog
    {
        private readonly WorldDocument _doc;
        private readonly FlowLayoutPanel _list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
        private readonly UiButton _restore;
        private StoredVersion _selected;

        public HistoryForm(WorldDocument doc) : base(Strings.HistoryTitle, Strings.HistorySubtitle, new Size(620, 480))
        {
            _doc = doc;
            Body.Controls.Add(_list);
            List<StoredVersion> versions = doc.Versions.List(doc.FilePath);
            foreach (StoredVersion v in versions)
            {
                StoredVersion version = v;
                string when = v.TimestampUtc.ToLocalTime().ToString("d MMMM yyyy, HH:mm:ss", new CultureInfo("ru-RU"));
                var row = new UiButton(when, ButtonKind.Row)
                {
                    Icon = Glyph.Clock, Detail = (v.Size / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " КБ" + (string.IsNullOrEmpty(v.Reason) ? "" : "  " + v.Reason),
                    Width = Ui.S(570), Height = Ui.S(36), Margin = new Padding(0, 0, 0, Ui.S(2))
                };
                row.Click += (s, e) => Select(row, version);
                _list.Controls.Add(row);
            }
            if (versions.Count == 0)
                _list.Controls.Add(new EmptyState(Glyph.Clock, "Пока нет копий", "Копия появляется при каждом сохранении, когда файл меняется") { Size = Ui.S(570, 240) });
            AddButton(Strings.Done, ButtonKind.Secondary, DialogResult.Cancel, null);
            _restore = AddButton(Strings.HistoryRestore, ButtonKind.Primary, DialogResult.None, Restore);
            _restore.Enabled = false;
            UiTheme.Apply(this);
        }

        public string RestoredName { get; private set; }

        private void Select(UiButton row, StoredVersion v)
        {
            foreach (Control c in _list.Controls) { var b = c as UiButton; if (b != null) b.Checked = b == row; }
            _selected = v;
            _restore.Enabled = true;
        }

        private void Restore()
        {
            if (_selected == null) return;
            string text = "Текущий файл сначала будет скопирован в историю, затем заменён выбранной версией." +
                          (_doc.IsDirty ? "\n\nНесохранённые изменения в редакторе пропадут." : "");
            if (UiAlert.AskStyled(this, "Откатить мир?", text, Glyph.Clock, _doc.IsDirty ? 0 : -1, "Откатить", "Отмена") != 0) return;
            try { _doc.Versions.Restore(_doc.FilePath, _selected); }
            catch (Exception ex) { Dialogs.Unexpected(this, "откат к " + _selected.Path, ex); return; }
            RestoredName = Path.GetFileName(_selected.Path);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
