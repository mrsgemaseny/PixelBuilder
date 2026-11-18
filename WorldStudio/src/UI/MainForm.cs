using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization;
using MiniLife.WorldStudio.Serialization.PixelArchitect;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    // Главное окно. Бизнес-логики здесь нет: правки - командами WorldEdits, файлы - через WorldDocument.
    // Раскладка: приветствие без документа; с документом - панель сверху, палитра слева, карта, инспектор справа.
    internal sealed class MainForm : Form
    {
        private const int AutosaveMinutes = 5;
        private static readonly EditLayer[] LayerOrder = { EditLayer.Landscape, EditLayer.Political, EditLayer.Wildlands, EditLayer.Objects };

        private readonly MapCanvas _canvas = new MapCanvas { Dock = DockStyle.Fill };
        private readonly UiToast _toast = new UiToast();
        private readonly Timer _autosave = new Timer();
        private readonly Timer _validateLater = new Timer { Interval = 350 };

        private UiSurface _welcome, _editor, _canvasHost, _left, _right, _inspector, _findingsPanel, _zoomBar;
        private UiLabel _title, _subtitle, _paletteTitle, _zoomLabel, _findingsSummary;
        private UiSegmented _layers;
        private UiButton _undo, _redo, _check, _save;
        private FlowLayoutPanel _palette, _routePanel, _recent;
        private UiSurface _routeSection;
        private FindingsList _findingsList;
        private readonly Dictionary<MapTool, UiButton> _tools = new Dictionary<MapTool, UiButton>();
        private readonly Dictionary<EditLayer, UiButton> _eyes = new Dictionary<EditLayer, UiButton>();
        private readonly Dictionary<EditLayer, UiButton> _locks = new Dictionary<EditLayer, UiButton>();

        private WorldDocument _doc;
        private List<Finding> _lastFindings = new List<Finding>();
        private bool _busy;
        private bool _findingsOpen;

        public MainForm()
        {
            Text = Strings.AppTitle;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = Ui.S(1440, 880);
            MinimumSize = Ui.S(1100, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Font = Ui.Body;
            KeyPreview = true;
            BuildWelcome();
            BuildEditor();
            Controls.Add(_editor);
            Controls.Add(_welcome);
            UiTheme.Changed += OnThemeChanged;
            UiTheme.Apply(this);
            _autosave.Interval = AutosaveMinutes * 60 * 1000;
            _autosave.Tick += (s, e) => AutosaveNow();
            _autosave.Start();
            _validateLater.Tick += (s, e) => { _validateLater.Stop(); RunValidation(); };
            SetDocument(null);
        }

        // ================= приветствие =================

        private void BuildWelcome()
        {
            _welcome = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Fill };
            var column = new UiSurface(SurfaceRole.Window) { Size = Ui.S(600, 560) };
            var title = new UiLabel(Strings.WelcomeTitle, TextRole.Large) { Dock = DockStyle.Top, Height = Ui.S(44), TextAlign = ContentAlignment.MiddleCenter };
            var subtitle = new UiLabel(Strings.WelcomeSubtitle, TextRole.Secondary) { Dock = DockStyle.Top, Height = Ui.S(30), TextAlign = ContentAlignment.TopCenter };
            var cards = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Ui.S(176), BackColor = Color.Transparent, Padding = new Padding(Ui.S(14), Ui.S(18), 0, 0), WrapContents = false };
            UiButton create = WelcomeCard(Strings.WelcomeNew, Strings.WelcomeNewHint, Glyph.Import, NewFromLegacy);
            UiButton open = WelcomeCard(Strings.WelcomeOpen, Strings.WelcomeOpenHint, Glyph.Open, OpenWorld);
            cards.Controls.Add(create);
            cards.Controls.Add(open);
            var recentHeader = new UiSectionHeader(Strings.WelcomeRecent, SurfaceRole.Window) { Padding = new Padding(Ui.S(18), 0, 0, 0) };
            _recent = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(Ui.S(14), 0, 0, 0) };
            column.Controls.Add(_recent);
            column.Controls.Add(recentHeader);
            column.Controls.Add(cards);
            column.Controls.Add(subtitle);
            column.Controls.Add(title);
            _welcome.Controls.Add(column);
            _welcome.Resize += (s, e) => column.Location = new Point(Math.Max(0, (_welcome.Width - column.Width) / 2), Math.Max(Ui.S(20), (_welcome.Height - column.Height) / 2 - Ui.S(20)));
        }

        private UiButton WelcomeCard(string title, string hint, Glyph icon, Action action)
        {
            var b = new UiButton(title, ButtonKind.Card) { Icon = icon, Detail = hint, Size = Ui.S(270, 150), Margin = new Padding(Ui.S(4), 0, Ui.S(16), 0) };
            b.Click += (s, e) => Guard(title, action);
            return b;
        }

        private void RefreshRecent()
        {
            _recent.Controls.Clear();
            foreach (string path in Settings.Recent())
            {
                string p = path;
                var row = new UiButton(Path.GetFileName(p), ButtonKind.Row) { Icon = Glyph.Map, Detail = ShortDir(p), Width = Ui.S(560), Height = Ui.S(34) };
                row.Click += (s, e) => Guard("открытие", () => OpenPath(p));
                Ui.Tip(row, p, null);
                _recent.Controls.Add(row);
            }
            if (_recent.Controls.Count == 0)
                _recent.Controls.Add(new UiLabel("Здесь появятся миры, которые вы открывали", TextRole.Tertiary) { AutoSize = true, Margin = new Padding(Ui.S(6), Ui.S(6), 0, 0) });
        }

        private static string ShortDir(string path)
        {
            string dir = Path.GetDirectoryName(path) ?? "";
            return dir.Length > 48 ? "..." + dir.Substring(dir.Length - 45) : dir;
        }

        // ================= редактор =================

        private void BuildEditor()
        {
            _editor = new UiSurface(SurfaceRole.Window) { Dock = DockStyle.Fill };
            UiSurface toolbar = BuildToolbar();
            BuildLeft();
            BuildRight();
            _canvasHost = new UiSurface(SurfaceRole.Canvas) { Dock = DockStyle.Fill };
            _canvasHost.Controls.Add(_toast);
            BuildZoomBar();
            _canvasHost.Controls.Add(_canvas);
            _canvasHost.Resize += (s, e) => PlaceOverlays();
            WireCanvas();
            _editor.Controls.Add(_canvasHost);
            _editor.Controls.Add(_left);
            _editor.Controls.Add(_right);
            _editor.Controls.Add(toolbar);
        }

        private UiSurface BuildToolbar()
        {
            var bar = new UiSurface(SurfaceRole.Toolbar) { Dock = DockStyle.Top, Height = Ui.S(56), LineBottom = true, Padding = new Padding(Ui.S(14), Ui.S(10), Ui.S(12), Ui.S(10)) };

            var titleBox = new UiSurface(SurfaceRole.Toolbar) { Dock = DockStyle.Left, Width = Ui.S(210) };
            _title = new UiLabel(Strings.Untitled, TextRole.Primary) { Dock = DockStyle.Top, Height = Ui.S(19), Font = Ui.BodyStrong, AutoEllipsis = true };
            _subtitle = new UiLabel(Strings.StateNew, TextRole.Secondary) { Dock = DockStyle.Top, Height = Ui.S(17), AutoEllipsis = true };
            titleBox.Controls.Add(_subtitle);
            titleBox.Controls.Add(_title);

            _layers = new UiSegmented(Strings.LayerLandscape, Strings.LayerPolitical, Strings.LayerWildlands, Strings.LayerObjects) { Dock = DockStyle.Left };
            _layers.Width = _layers.PreferredWidth();
            _layers.SelectedIndexChanged += () => SetLayer(LayerOrder[_layers.SelectedIndex]);
            Ui.Tip(_layers, "Слой для правки", "1 - 4");

            var tools = new FlowLayoutPanel { Dock = DockStyle.Left, Width = Ui.S(6 * 36 + 24), WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(Ui.S(18), 0, 0, 0) };
            AddTool(tools, MapTool.Brush, Glyph.Brush, Strings.ToolBrush, "B");
            AddTool(tools, MapTool.Rect, Glyph.Rect, Strings.ToolRect, "R");
            AddTool(tools, MapTool.Fill, Glyph.Fill, Strings.ToolFill, "F");
            AddTool(tools, MapTool.Pipette, Glyph.Pipette, Strings.ToolPipette, "I");
            AddTool(tools, MapTool.Eraser, Glyph.Eraser, Strings.ToolEraser, "E");
            AddTool(tools, MapTool.Route, Glyph.Route, Strings.ToolRoute, "M");

            var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Color.Transparent, FlowDirection = FlowDirection.LeftToRight };
            _undo = IconButton(Glyph.Undo, "Отменить", "Ctrl+Z", () => { if (_doc != null) _doc.Undo(); });
            _redo = IconButton(Glyph.Redo, "Повторить", "Ctrl+Y", () => { if (_doc != null) _doc.Redo(); });
            _check = new UiButton(Strings.CheckButton, ButtonKind.Ghost) { Icon = Glyph.Shield, Height = Ui.S(34), Width = Ui.S(176), Margin = new Padding(Ui.S(10), 0, 0, 0) };
            _check.Click += (s, e) => Guard("проверка", ToggleFindings);
            Ui.Tip(_check, "Проверить мир и показать находки", "F5");
            UiButton more = IconButton(Glyph.More, "Ещё: файлы, экспорт, реестр, отношения, тема", null, null);
            more.Click += (s, e) => ShowMoreMenu(more);
            _save = new UiButton("Сохранить", ButtonKind.Primary) { Height = Ui.S(34), Width = Ui.S(112), Margin = new Padding(Ui.S(8), 0, 0, 0) };
            _save.Click += (s, e) => Guard("сохранение", () => SaveWorld(false));
            Ui.Tip(_save, "Сохранить world.json", "Ctrl+S");
            right.Controls.AddRange(new Control[] { _undo, _redo, _check, more, _save });

            bar.Controls.Add(tools);
            bar.Controls.Add(_layers);
            bar.Controls.Add(titleBox);
            bar.Controls.Add(right);
            return bar;
        }

        private UiButton IconButton(Glyph glyph, string tip, string shortcut, Action action)
        {
            var b = new UiButton("", ButtonKind.Tool) { Icon = glyph, IconOnly = true, Size = Ui.S(34, 34), Margin = new Padding(Ui.S(2), 0, 0, 0) };
            if (action != null) b.Click += (s, e) => Guard(tip, action);
            Ui.Tip(b, tip, shortcut);
            return b;
        }

        private void AddTool(FlowLayoutPanel flow, MapTool tool, Glyph glyph, string tip, string key)
        {
            UiButton b = IconButton(glyph, tip, key, () => SetTool(tool));
            flow.Controls.Add(b);
            _tools[tool] = b;
        }

        private void BuildLeft()
        {
            _left = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Left, Width = Ui.S(252), LineRight = true, Padding = new Padding(Ui.S(10), Ui.S(4), Ui.S(10), Ui.S(10)) };

            var layersHeader = new UiSectionHeader(Strings.LayersTitle, SurfaceRole.Sidebar);
            var layersGroup = new UiGroup();
            foreach (EditLayer layer in LayerOrder) layersGroup.AddRow(LayerRow(layer));

            _paletteTitle = new UiLabel("", TextRole.Caption) { Dock = DockStyle.Top, Height = Ui.S(34), TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(Ui.S(4), 0, 0, Ui.S(5)) };
            _palette = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };

            _routeSection = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Bottom, Height = Ui.S(300), Visible = false };
            var routeHeader = new UiSectionHeader(Strings.ToolRoute, SurfaceRole.Sidebar);
            _routePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            var routeHint = new UiLabel(Strings.RouteHint, TextRole.Secondary) { Dock = DockStyle.Bottom, Height = Ui.S(48) };
            var routeButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = Ui.S(40), BackColor = Color.Transparent, WrapContents = false };
            var done = new UiButton(Strings.RouteDone, ButtonKind.Primary) { Width = Ui.S(150), Height = Ui.S(30) };
            done.Click += (s, e) => Guard("маршрут", CommitRoute);
            var clear = new UiButton(Strings.RouteClear, ButtonKind.Secondary) { Width = Ui.S(76), Height = Ui.S(30) };
            clear.Click += (s, e) => _canvas.ClearRouteBuffer();
            routeButtons.Controls.Add(done);
            routeButtons.Controls.Add(clear);
            _routeSection.Controls.Add(_routePanel);
            _routeSection.Controls.Add(routeHeader);
            _routeSection.Controls.Add(routeHint);
            _routeSection.Controls.Add(routeButtons);

            _left.Controls.Add(_palette);
            _left.Controls.Add(_paletteTitle);
            _left.Controls.Add(layersGroup);
            _left.Controls.Add(layersHeader);
            _left.Controls.Add(_routeSection);
        }

        private Control LayerRow(EditLayer layer)
        {
            var row = new UiSurface(SurfaceRole.Content) { Height = Ui.S(36), Padding = new Padding(Ui.S(4), Ui.S(3), Ui.S(4), Ui.S(3)) };
            var eye = new UiButton("", ButtonKind.Tool) { Icon = Glyph.Eye, IconOnly = true, Dock = DockStyle.Left, Width = Ui.S(30) };
            var lockButton = new UiButton("", ButtonKind.Tool) { Icon = Glyph.Unlock, IconOnly = true, Dock = DockStyle.Right, Width = Ui.S(30) };
            var name = new UiLabel(MapCanvas.LayerName(layer), TextRole.Primary) { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Ui.S(4), 0, 0, 0) };
            eye.Click += (s, e) =>
            {
                bool visible = !_canvas.IsVisible(layer);
                _canvas.SetVisible(layer, visible);
                eye.Icon = visible ? Glyph.Eye : Glyph.EyeOff;
                name.Role = visible ? TextRole.Primary : TextRole.Tertiary;
                eye.Invalidate();
            };
            lockButton.Click += (s, e) =>
            {
                bool locked = !_canvas.IsLocked(layer);
                _canvas.SetLocked(layer, locked);
                lockButton.Icon = locked ? Glyph.Lock : Glyph.Unlock;
                lockButton.Checked = locked;
            };
            Ui.Tip(eye, "Показать или скрыть слой \"" + MapCanvas.LayerName(layer) + "\"", null);
            Ui.Tip(lockButton, "Заблокировать слой от случайных правок", null);
            row.Controls.Add(name);
            row.Controls.Add(eye);
            row.Controls.Add(lockButton);
            _eyes[layer] = eye;
            _locks[layer] = lockButton;
            return row;
        }

        private void BuildRight()
        {
            _right = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Right, Width = Ui.S(300), LineLeft = true };
            _inspector = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Fill, Padding = new Padding(Ui.S(12), Ui.S(12), Ui.S(12), Ui.S(12)), AutoScroll = true };

            _findingsPanel = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Fill, Visible = false };
            var header = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Top, Height = Ui.S(92), Padding = new Padding(Ui.S(16), Ui.S(14), Ui.S(10), Ui.S(8)), LineBottom = true };
            var close = new UiButton("", ButtonKind.Tool) { Icon = Glyph.Close, IconOnly = true, Size = Ui.S(30, 30) };
            close.Click += (s, e) => ToggleFindings();
            Ui.Tip(close, "Вернуться к инспектору клетки", null);
            var title = new UiLabel(Strings.FindingsTitle, TextRole.Title) { Location = new Point(Ui.S(16), Ui.S(12)), AutoSize = true };
            _findingsSummary = new UiLabel("", TextRole.Secondary) { Location = new Point(Ui.S(16), Ui.S(40)), AutoSize = true };
            var rerun = new UiButton(Strings.FindingsRerun, ButtonKind.Secondary) { Location = new Point(Ui.S(16), Ui.S(58)), Size = Ui.S(140, 26) };
            rerun.Click += (s, e) => Guard("проверка", () => { RunValidation(); _toast.Show("Проверка выполнена", ToastKind.Success); });
            header.Controls.AddRange(new Control[] { title, _findingsSummary, rerun, close });
            header.Resize += (s, e) => close.Location = new Point(header.Width - close.Width - Ui.S(10), Ui.S(10));
            _findingsList = new FindingsList { Dock = DockStyle.Fill };
            _findingsList.FindingSelected += f => Guard("находка", () => ShowFinding(f));
            _findingsPanel.Controls.Add(_findingsList);
            _findingsPanel.Controls.Add(header);

            _right.Controls.Add(_inspector);
            _right.Controls.Add(_findingsPanel);
        }

        private void BuildZoomBar()
        {
            _zoomBar = new UiSurface(SurfaceRole.Content) { Size = Ui.S(170, 34), Padding = new Padding(Ui.S(3)) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
            UiButton minus = IconButton(Glyph.Minus, "Уменьшить", "Ctrl+-", () => _canvas.ZoomBy(1 / 1.25f));
            UiButton plus = IconButton(Glyph.Plus, "Увеличить", "Ctrl+=", () => _canvas.ZoomBy(1.25f));
            UiButton fit = IconButton(Glyph.Target, "Вписать карту", "Ctrl+0", _canvas.FitToView);
            foreach (UiButton b in new[] { minus, plus, fit }) { b.Size = Ui.S(28, 28); b.Margin = new Padding(0); }
            _zoomLabel = new UiLabel("100%", TextRole.Secondary) { Size = Ui.S(52, 28), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0) };
            flow.Controls.AddRange(new Control[] { minus, _zoomLabel, plus, fit });
            _zoomBar.Controls.Add(flow);
            _canvasHost.Controls.Add(_zoomBar);
            _canvas.ZoomChanged += () => _zoomLabel.Text = _canvas.ZoomPercent + "%";
        }

        private void PlaceOverlays()
        {
            _zoomBar.Location = new Point(_canvasHost.Width - _zoomBar.Width - Ui.S(14), _canvasHost.Height - _zoomBar.Height - Ui.S(14));
            using (var p = Ui.Round(new RectangleF(0, 0, _zoomBar.Width, _zoomBar.Height), Ui.S(9f))) _zoomBar.Region = new Region(p);
            _zoomBar.BringToFront();
            _toast.BringToFront();
        }

        private void WireCanvas()
        {
            _canvas.CellSelected += c => UpdateInspector();
            _canvas.Message += text => _toast.Show(text, ToastKind.Warning);
            _canvas.ValuePicked += (layer, value) =>
            {
                if (value == null) { _toast.Show("В этой клетке нет значения слоя \"" + MapCanvas.LayerName(layer) + "\"", ToastKind.Info); return; }
                SetValue(value);
                SetTool(MapTool.Brush);
                _toast.Show("Взято: " + DescribeValue(value), ToastKind.Info);
            };
            _canvas.RouteBufferChanged += RefreshRoute;
        }

        // ================= документ =================

        private void SetDocument(WorldDocument doc)
        {
            if (_doc != null) _doc.Changed -= OnDocumentChanged;
            _doc = doc;
            _welcome.Visible = doc == null;
            _editor.Visible = doc != null;
            if (doc == null) { RefreshRecent(); Text = Strings.AppTitle; return; }
            _doc.Changed += OnDocumentChanged;
            if (_doc.Reference == null) _doc.Reference = LoadReference();
            _canvas.Document = doc;
            _findingsOpen = false;
            RefreshPalette();
            RefreshRoute();
            SetTool(MapTool.Brush);
            OnDocumentChanged();
            RunValidation();
            BeginInvoke(new Action(() => { PlaceOverlays(); _canvas.FitToView(); }));
            if (_doc.ImportIssues != null && _doc.ImportIssues.Count > 0) { _findingsOpen = true; ApplyFindingsVisibility(); }
        }

        private void OnDocumentChanged()
        {
            if (_doc == null) return;
            string name = _doc.FilePath != null ? Path.GetFileName(_doc.FilePath) : Strings.Untitled;
            Text = name + (_doc.IsDirty ? " - изменён" : "") + " - " + Strings.AppTitle;
            _title.Text = name;
            _subtitle.Text = _doc.FilePath == null ? Strings.StateNew : _doc.IsDirty ? Strings.StateDirty : Strings.StateSaved;
            _subtitle.Role = _doc.IsDirty || _doc.FilePath == null ? TextRole.Warning : TextRole.Secondary;
            _undo.Enabled = _doc.History.CanUndo;
            _redo.Enabled = _doc.History.CanRedo;
            Ui.Tip(_undo, _doc.History.CanUndo ? "Отменить: " + _doc.History.UndoDescription : "Нечего отменять", "Ctrl+Z");
            Ui.Tip(_redo, _doc.History.CanRedo ? "Повторить: " + _doc.History.RedoDescription : "Нечего повторять", "Ctrl+Y");
            UpdateInspector();
            UpdatePaletteCounts();
            _canvas.Invalidate();
            _validateLater.Stop();
            _validateLater.Start();
        }

        private static SettlementReference LoadReference()
        {
            try { return ReferenceCodec.Read(TextFileStore.ReadAllText(AppPaths.ReferencePath)); }
            catch (Exception ex)
            {
                ErrorLog.Write("таблица автора не загружена: " + AppPaths.ReferencePath, ex);
                return null; // проверка 7 пропускается
            }
        }

        // Несохранённые изменения: сохранить, не сохранять или отменить действие. true - можно продолжать.
        private bool ConfirmDiscard()
        {
            if (_doc == null || !_doc.IsDirty) return true;
            int r = UiAlert.AskStyled(this, Strings.AskSaveTitle, Strings.AskSaveText, Glyph.Save, 1, "Сохранить", "Не сохранять", "Отмена");
            if (r == 0) return SaveWorldSync();
            return r == 1;
        }

        private void NewFromLegacy()
        {
            if (!ConfirmDiscard()) return;
            using (var dialog = new ImportForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string error;
                WorldDocument doc = WorldDocument.FromImport(TextFileStore.ReadAllText(AppPaths.TemplatePath), dialog.Sources, null, out error);
                if (doc == null) { Dialogs.Error(this, error); return; }
                SetDocument(doc);
                _toast.Show("Мир собран из " + Labels.Plural(dialog.Sources.Count, "файла", "файлов", "файлов") + ". Сохраните его как world.json", ToastKind.Success);
            }
        }

        private void OpenWorld()
        {
            if (_busy || !ConfirmDiscard()) return;
            using (var ofd = new OpenFileDialog { Filter = Strings.FilterWorld })
                if (ofd.ShowDialog(this) == DialogResult.OK) OpenPath(ofd.FileName);
        }

        private async void OpenPath(string path)
        {
            if (_busy) return;
            if (_doc != null && !ConfirmDiscard()) return;
            string error = null;
            WorldDocument doc = null;
            SetBusy(true);
            try { doc = await Task.Run(() => WorldDocument.Open(path, null, out error)); }
            catch (Exception ex) { Dialogs.Unexpected(this, "открытие " + path, ex); return; }
            finally { SetBusy(false); }
            if (doc == null) { Dialogs.Error(this, "Не удалось открыть " + Path.GetFileName(path) + ".\n" + error); return; }
            Settings.AddRecent(doc.FilePath);
            SetDocument(doc);
        }

        private string AskSavePath()
        {
            using (var sfd = new SaveFileDialog { Filter = Strings.FilterWorld, FileName = "world.json", OverwritePrompt = true })
                return sfd.ShowDialog(this) == DialogResult.OK ? sfd.FileName : null;
        }

        private async void SaveWorld(bool askPath)
        {
            if (_doc == null || _busy) return;
            string path = askPath || _doc.FilePath == null ? AskSavePath() : _doc.FilePath;
            if (path == null) return;
            WorldDocument.SavePlan plan = _doc.PrepareSave(path);
            SetBusy(true);
            bool ok = false;
            try
            {
                WorldDocument doc = _doc;
                await Task.Run(() => doc.WriteSave(plan));
                ok = true;
            }
            catch (Exception ex) { Dialogs.Unexpected(this, "сохранение " + path, ex); }
            finally
            {
                SetBusy(false);
                _doc.CommitSave(plan, ok);
            }
            if (ok) { Settings.AddRecent(plan.Path); _toast.Show(Strings.Saved + ": " + Path.GetFileName(plan.Path), ToastKind.Success); }
        }

        private bool SaveWorldSync()
        {
            string path = _doc.FilePath ?? AskSavePath();
            if (path == null) return false;
            try { _doc.SaveAs(path); Settings.AddRecent(_doc.FilePath); return true; }
            catch (Exception ex) { Dialogs.Unexpected(this, "сохранение " + path, ex); return false; }
        }

        private void AutosaveNow()
        {
            if (_doc == null || _busy) return;
            WorldDocument.SavePlan plan = _doc.PrepareAutosave();
            if (plan == null) return;
            WorldDocument doc = _doc;
            Task.Run(() => WorldDocument.WriteAutosave(plan)).ContinueWith(t =>
            {
                if (t.IsFaulted) { ErrorLog.Write("автосохранение " + plan.Path, t.Exception); return; }
                doc.CommitAutosave(plan);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ExportLegacy(MapSemantic semantic)
        {
            if (_doc == null) return;
            var warnings = new List<string>();
            PixelArchitectDocument legacy = _doc.ExportLegacy(semantic, warnings);
            if (warnings.Count > 0)
            {
                string list = string.Join("\n", warnings.GetRange(0, Math.Min(8, warnings.Count)).ToArray()) + (warnings.Count > 8 ? "\nи ещё " + (warnings.Count - 8) : "");
                if (UiAlert.AskStyled(this, Strings.ExportWarningsTitle, list, Glyph.Warning, -1, "Записать всё равно", "Отмена") != 0) return;
            }
            using (var sfd = new SaveFileDialog { Filter = Strings.FilterLegacy, OverwritePrompt = true, FileName = "MiniLife_global_" + Legend.SemanticCode(semantic) + ".json" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                _doc.WriteExternalText(sfd.FileName, PixelArchitectCodec.Write(legacy), "экспорт: " + Legend.SemanticName(semantic));
                _toast.Show(Strings.ExportDone + ": " + Path.GetFileName(sfd.FileName), ToastKind.Success);
            }
        }

        private void ShowMoreMenu(Control anchor)
        {
            ContextMenuStrip menu = UiMenuRenderer.Menu();
            UiMenuRenderer.Item(menu.Items, "Собрать мир из карт PixelBuilder...", "Ctrl+N", () => Guard("импорт", NewFromLegacy));
            UiMenuRenderer.Item(menu.Items, "Открыть world.json...", "Ctrl+O", () => Guard("открытие", OpenWorld));
            UiMenuRenderer.Item(menu.Items, "Сохранить как...", "Ctrl+Shift+S", () => Guard("сохранение", () => SaveWorld(true)));
            ToolStripMenuItem export = UiMenuRenderer.Item(menu.Items, "Экспорт в PixelBuilder", null, null);
            export.DropDown = UiMenuRenderer.Menu();
            UiMenuRenderer.Item(export.DropDownItems, "Карта ландшафтов...", null, () => Guard("экспорт", () => ExportLegacy(MapSemantic.Landscape)));
            UiMenuRenderer.Item(export.DropDownItems, "Политическая карта...", null, () => Guard("экспорт", () => ExportLegacy(MapSemantic.Political)));
            UiMenuRenderer.Item(export.DropDownItems, "Дикие земли с метками Д и Г...", null, () => Guard("экспорт", () => ExportLegacy(MapSemantic.Wildlands)));
            menu.Items.Add(new ToolStripSeparator());
            UiMenuRenderer.Item(menu.Items, "Реестр: государства, группы, армии...", "Ctrl+R", () => Guard("реестр", ShowRegistry));
            UiMenuRenderer.Item(menu.Items, "Отношения государств...", "Ctrl+T", () => Guard("отношения", ShowRelations));
            UiMenuRenderer.Item(menu.Items, "История версий...", null, () => Guard("история", ShowHistory));
            UiMenuRenderer.Item(menu.Items, "Убрать сушу в выбранной клетке...", null, () => Guard("суша", RemoveLandAtSelection));
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem theme = UiMenuRenderer.Item(menu.Items, "Оформление", null, null);
            theme.DropDown = UiMenuRenderer.Menu();
            AddThemeItem(theme, "Как в Windows", ThemeMode.System);
            AddThemeItem(theme, "Светлое", ThemeMode.Light);
            AddThemeItem(theme, "Тёмное", ThemeMode.Dark);
            UiMenuRenderer.Item(menu.Items, "Показать сетку", "G", () => { _canvas.ShowGrid = !_canvas.ShowGrid; _canvas.Invalidate(); }).Checked = _canvas.ShowGrid;
            UiMenuRenderer.Item(menu.Items, "Папка журнала ошибок", null, OpenLogFolder);
            menu.Show(anchor, new Point(anchor.Width - menu.PreferredSize.Width, anchor.Height + Ui.S(4)));
        }

        private void AddThemeItem(ToolStripMenuItem parent, string text, ThemeMode mode)
        {
            ToolStripMenuItem item = UiMenuRenderer.Item(parent.DropDownItems, text, null, () => { Settings.SaveTheme(mode); UiTheme.SetMode(mode); });
            item.Checked = UiTheme.Mode == mode;
        }

        private void OnThemeChanged()
        {
            UiTheme.Apply(this);
            RefreshPalette();
            UpdateInspector();
            _canvas.Invalidate();
        }

        private void ShowHistory()
        {
            if (_doc == null) return;
            if (_doc.FilePath == null) { _toast.Show(Strings.HistoryNeedSaved, ToastKind.Info); return; }
            using (var form = new HistoryForm(_doc))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                string error;
                WorldDocument reopened = WorldDocument.Open(_doc.FilePath, null, out error);
                if (reopened == null) { Dialogs.Error(this, error); return; }
                SetDocument(reopened);
                _toast.Show("Мир откатился к версии " + form.RestoredName, ToastKind.Success);
            }
        }

        private void ShowRegistry()
        {
            if (_doc == null) return;
            using (var form = new RegistryForm(_doc, () => _canvas.SelectedCell)) form.ShowDialog(this);
            RefreshPalette();
            RefreshRoute();
            OnDocumentChanged();
        }

        private void ShowRelations()
        {
            if (_doc == null) return;
            using (var form = new RelationsForm(_doc)) form.ShowDialog(this);
            OnDocumentChanged();
        }

        private void RemoveLandAtSelection()
        {
            if (_doc == null || !_canvas.SelectedCell.HasValue) { _toast.Show("Сначала выберите клетку на карте", ToastKind.Info); return; }
            CellCoord c = _canvas.SelectedCell.Value;
            if (!_doc.World.IsLand(c)) { _toast.Show("Эта клетка уже не суша", ToastKind.Info); return; }
            if (!Dialogs.Confirm(this, Strings.ConfirmRemoveLandTitle, string.Format(Strings.ConfirmRemoveLand, c.X + ", " + c.Y), "Убрать сушу", true)) return;
            string refused;
            IWorldCommand cmd = WorldEdits.RemoveLand(_doc.World, new[] { c }, out refused);
            if (cmd == null) { _toast.Show(refused, ToastKind.Warning); return; }
            Report(_doc.Execute(cmd));
        }

        private void OpenLogFolder()
        {
            Directory.CreateDirectory(ErrorLog.Directory);
            System.Diagnostics.Process.Start(ErrorLog.Directory);
        }

        // ================= слои, инструменты, палитра =================

        private void SetLayer(EditLayer layer)
        {
            _canvas.ActiveLayer = layer;
            _canvas.ActiveValue = null;
            if (_layers.SelectedIndex != Array.IndexOf(LayerOrder, layer)) _layers.SelectedIndex = Array.IndexOf(LayerOrder, layer);
            if (_canvas.Tool == MapTool.Route) SetTool(MapTool.Brush);
            RefreshPalette();
        }

        private void SetTool(MapTool tool)
        {
            _canvas.Tool = tool;
            foreach (var pair in _tools) pair.Value.Checked = pair.Key == tool;
            bool route = tool == MapTool.Route;
            _routeSection.Visible = route && _doc != null;
            if (route) RefreshRoute();
            _canvas.Invalidate();
        }

        private void SetValue(string value)
        {
            _canvas.ActiveValue = value;
            foreach (Control c in _palette.Controls)
            {
                var b = c as UiButton;
                if (b != null) b.Checked = (string)b.Tag == value;
            }
        }

        private string DescribeValue(string value)
        {
            World w = _doc.World;
            if (_canvas.ActiveLayer == EditLayer.Landscape) return Labels.Landscape(w, value);
            if (_canvas.ActiveLayer == EditLayer.Objects)
            {
                WorldObjectType t;
                return WorldObjectTypes.TryParse(value, out t) ? Labels.ObjectName(t) : value;
            }
            return Labels.Entity(w, value);
        }

        private void RefreshPalette()
        {
            if (_doc == null) return;
            _palette.SuspendLayout();
            while (_palette.Controls.Count > 0) { Control c = _palette.Controls[0]; _palette.Controls.RemoveAt(0); c.Dispose(); }
            World w = _doc.World;
            switch (_canvas.ActiveLayer)
            {
                case EditLayer.Landscape:
                    _paletteTitle.Text = "ТИПЫ ЛАНДШАФТА";
                    foreach (LandscapeType t in w.Legend.LandscapeTypes) AddSwatch(t.Id, t.Name, MapCanvas.ToColor(t.Color), false, null);
                    break;
                case EditLayer.Political:
                    _paletteTitle.Text = "ГОСУДАРСТВА";
                    foreach (StateEntity s in w.States) AddSwatch(s.Id, s.Name, MapCanvas.ToColor(s.Color), false, null);
                    break;
                case EditLayer.Wildlands:
                    _paletteTitle.Text = "ГРУППЫ ДИКИХ ЗЕМЕЛЬ";
                    foreach (WildlandEntity g in w.Wildlands) AddSwatch(g.Id, Labels.Wild(g.Id), MapCanvas.ToColor(g.Color), true, null);
                    break;
                default:
                    _paletteTitle.Text = "ОБЪЕКТЫ КАРТЫ";
                    foreach (WorldObjectType t in (WorldObjectType[])Enum.GetValues(typeof(WorldObjectType)))
                    {
                        Color plate = t == WorldObjectType.ArmyMark ? Color.FromArgb(190, 30, 30) : t == WorldObjectType.VillageMark ? Color.FromArgb(120, 120, 128) : Color.FromArgb(40, 40, 46);
                        AddSwatch(WorldObjectTypes.ToCode(t), Labels.ObjectName(t), plate, false, Labels.ObjectSymbol(t));
                    }
                    break;
            }
            _palette.ResumeLayout();
            UpdatePaletteCounts();
            SetValue(_canvas.ActiveValue);
        }

        private void AddSwatch(string value, string text, Color color, bool hatched, string symbol)
        {
            var b = new UiButton(text, ButtonKind.Row)
            {
                Tag = value, Swatch = color, SwatchHatched = hatched, SwatchLabel = symbol, Width = _left.Width - Ui.S(24), Height = Ui.S(32), Margin = new Padding(0, 0, 0, Ui.S(2))
            };
            b.Click += (s, e) =>
            {
                SetValue(value);
                if (_canvas.Tool == MapTool.Pipette || _canvas.Tool == MapTool.Eraser || _canvas.Tool == MapTool.Route) SetTool(MapTool.Brush);
            };
            WorldObjectType t;
            if (WorldObjectTypes.TryParse(value, out t) && Labels.ObjectHint(t) != null) Ui.Tip(b, Labels.ObjectHint(t), null);
            _palette.Controls.Add(b);
        }

        // Сколько клеток или объектов у каждого значения палитры: видно, что и где используется.
        private void UpdatePaletteCounts()
        {
            if (_doc == null) return;
            World w = _doc.World;
            var counts = new Dictionary<string, int>();
            if (_canvas.ActiveLayer == EditLayer.Objects)
                foreach (WorldObject o in w.Objects) Inc(counts, WorldObjectTypes.ToCode(o.Type));
            else
                foreach (CellData c in w.Cells) Inc(counts, _canvas.ActiveLayer == EditLayer.Landscape ? c.Landscape : c.Owner);
            foreach (Control c in _palette.Controls)
            {
                var b = c as UiButton;
                if (b == null) continue;
                int n;
                counts.TryGetValue((string)b.Tag, out n);
                b.Detail = n == 0 ? "" : n.ToString();
                b.Invalidate();
            }
        }

        private static void Inc(Dictionary<string, int> d, string key)
        {
            if (key == null) return;
            int n;
            d.TryGetValue(key, out n);
            d[key] = n + 1;
        }

        // ================= маршрут =================

        private void RefreshRoute()
        {
            if (_routePanel == null) return;
            string current = _canvas.RouteArmyId;
            _routePanel.SuspendLayout();
            while (_routePanel.Controls.Count > 0) { Control c = _routePanel.Controls[0]; _routePanel.Controls.RemoveAt(0); c.Dispose(); }
            if (_doc != null)
                foreach (ArmyEntity a in _doc.World.Armies)
                {
                    string id = a.Id;
                    int points = id == current ? _canvas.RouteBuffer.Count : a.Route.Count;
                    var row = new UiButton(Labels.ArmyName(id), ButtonKind.Row)
                    {
                        Tag = id, Swatch = a.Status == ArmyStatus.Roaming ? Color.FromArgb(255, 110, 60) : Color.FromArgb(150, 150, 150),
                        Detail = points == 0 ? "нет маршрута" : Labels.Plural(points, "точка", "точки", "точек"),
                        Width = _left.Width - Ui.S(24), Height = Ui.S(30), Checked = id == current, Margin = new Padding(0, 0, 0, Ui.S(2))
                    };
                    row.Click += (s, e) => { ArmyEntity army = _doc.World.FindArmy(id); _canvas.StartRoute(id, army != null ? army.Route : null); };
                    _routePanel.Controls.Add(row);
                }
            _routePanel.ResumeLayout();
        }

        private void CommitRoute()
        {
            if (_doc == null || _canvas.RouteArmyId == null) { _toast.Show("Выберите армию в списке слева", ToastKind.Info); return; }
            Report(_doc.Execute(WorldEdits.SetArmyRoute(_canvas.RouteArmyId, _canvas.RouteBuffer)));
            _toast.Show("Маршрут сохранён: " + Labels.Plural(_canvas.RouteBuffer.Count, "точка", "точки", "точек"), ToastKind.Success);
            RefreshRoute();
        }

        // ================= инспектор =================

        private void UpdateInspector()
        {
            if (_inspector == null) return;
            _inspector.SuspendLayout();
            while (_inspector.Controls.Count > 0) { Control c = _inspector.Controls[0]; _inspector.Controls.RemoveAt(0); c.Dispose(); }
            if (_doc == null || !_canvas.SelectedCell.HasValue)
            {
                _inspector.Controls.Add(new EmptyState(Glyph.Target, Strings.InspectorEmpty, Strings.InspectorEmptyHint) { Dock = DockStyle.Fill });
                _inspector.ResumeLayout();
                return;
            }
            World w = _doc.World;
            CellCoord cell = _canvas.SelectedCell.Value;
            CellData d = w.FindCell(cell);
            var stack = new List<Control>();
            stack.Add(new UiLabel("Клетка " + cell.X + ", " + cell.Y, TextRole.Title) { Height = Ui.S(28) });
            stack.Add(new UiLabel("Тайлы " + cell.X * w.Grid.CellTiles + "-" + ((cell.X + 1) * w.Grid.CellTiles - 1) + " по X, " +
                                  cell.Y * w.Grid.CellTiles + "-" + ((cell.Y + 1) * w.Grid.CellTiles - 1) + " по Y", TextRole.Secondary) { Height = Ui.S(26) });
            var group = new UiGroup();
            var rows = new List<KeyValueRow>();
            if (d == null) rows.Add(new KeyValueRow("Суша", Strings.NotLand, null, false));
            else
            {
                LandscapeType lt = d.Landscape != null ? w.Legend.FindLandscape(d.Landscape) : null;
                rows.Add(new KeyValueRow("Ландшафт", Labels.Landscape(w, d.Landscape), lt != null ? (Color?)MapCanvas.ToColor(lt.Color) : null, false));
                RgbColor? oc = d.Owner != null ? w.DisplayColorOf(d.Owner) : null;
                rows.Add(new KeyValueRow(d.Owner != null && w.FindWildland(d.Owner) != null ? "Дикие земли" : "Государство", Labels.Entity(w, d.Owner),
                    oc.HasValue ? (Color?)MapCanvas.ToColor(oc.Value) : null, false));
            }
            WorldObject o = w.ObjectAt(cell);
            rows.Add(new KeyValueRow("Объект", o == null ? "нет" : Labels.ObjectName(o.Type), null, false));
            foreach (ArmyEntity a in w.Armies)
                if (a.Position.HasValue && a.Position.Value == cell) rows.Add(new KeyValueRow("Армия", Labels.ArmyName(a.Id) + ", " + Labels.ArmyStatus(a.Status).ToLowerInvariant(), null, false));
            rows[rows.Count - 1].Last = true;
            foreach (KeyValueRow r in rows) group.AddRow(r);
            stack.Add(group);
            if (d != null)
            {
                stack.Add(new UiLabel("ЗАМЕТКА", TextRole.Caption) { Height = Ui.S(30), TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(Ui.S(4), 0, 0, Ui.S(5)) });
                var notes = new UiTextField(true) { Height = Ui.S(90) };
                notes.Text = d.Notes;
                notes.Box.Leave += (s, e) => Guard("заметка", () => ApplyNotes(cell, notes.Text));
                stack.Add(notes);
                stack.Add(new UiLabel("Сохраняется, когда вы уходите из поля", TextRole.Tertiary) { Height = Ui.S(22) });
            }
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                stack[i].Dock = DockStyle.Top;
                _inspector.Controls.Add(stack[i]);
            }
            UiTheme.Apply(_inspector);
            _inspector.ResumeLayout();
        }

        private void ApplyNotes(CellCoord cell, string text)
        {
            if (_doc == null) return;
            IWorldCommand cmd = WorldEdits.SetCellNotes(_doc.World, cell, text);
            if (cmd != null) { Report(_doc.Execute(cmd)); _toast.Show("Заметка сохранена", ToastKind.Success); }
        }

        // ================= проверка =================

        private void RunValidation()
        {
            if (_doc == null) return;
            _lastFindings = _doc.Validate();
            int critical = Finding.Count(_lastFindings, Severity.Critical), warning = Finding.Count(_lastFindings, Severity.Warning);
            int info = Finding.Count(_lastFindings, Severity.Info);
            if (critical > 0) { _check.Icon = Glyph.Error; _check.Text = Labels.Plural(critical, "ошибка", "ошибки", "ошибок"); }
            else if (warning > 0) { _check.Icon = Glyph.Warning; _check.Text = Labels.Plural(warning, "предупреждение", "предупреждения", "предупреждений"); }
            else { _check.Icon = Glyph.Shield; _check.Text = Strings.FindingsClean; }
            _check.Invalidate();
            _findingsSummary.Text = Labels.Plural(critical, "ошибка", "ошибки", "ошибок") + ",  " +
                                    Labels.Plural(warning, "предупреждение", "предупреждения", "предупреждений") + ",  " + Labels.Plural(info, "заметка", "заметки", "заметок");
            _findingsList.SetItems(_lastFindings);
        }

        private void ToggleFindings()
        {
            _findingsOpen = !_findingsOpen;
            if (_findingsOpen) RunValidation();
            ApplyFindingsVisibility();
        }

        private void ApplyFindingsVisibility()
        {
            _findingsPanel.Visible = _findingsOpen;
            _inspector.Visible = !_findingsOpen;
            _check.Checked = _findingsOpen;
        }

        private void ShowFinding(Finding f)
        {
            var cells = new List<CellCoord>();
            if (f.Cell.HasValue) cells.Add(f.Cell.Value);
            else if (f.EntityId != null && _doc != null)
                foreach (CellData d in _doc.World.Cells) if (d.Owner == f.EntityId) cells.Add(d.Cell);
            _canvas.Highlight(cells);
            if (f.Cell.HasValue) _canvas.Select(f.Cell.Value);
        }

        // ================= общее =================

        private void Report(string error)
        {
            if (error != null) _toast.Show(error, ToastKind.Warning);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UseWaitCursor = busy;
            if (_save != null) { _save.Enabled = !busy; _save.Text = busy ? Strings.Busy : "Сохранить"; }
        }

        private void Guard(string context, Action action)
        {
            try { action(); }
            catch (DomainException ex) { _toast.Show(ex.Message, ToastKind.Warning); }
            catch (Exception ex) { Dialogs.Unexpected(this, context, ex); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.N: Guard("импорт", NewFromLegacy); return true;
                case Keys.Control | Keys.O: Guard("открытие", OpenWorld); return true;
                case Keys.Control | Keys.S: Guard("сохранение", () => SaveWorld(false)); return true;
                case Keys.Control | Keys.Shift | Keys.S: Guard("сохранение", () => SaveWorld(true)); return true;
            }
            if (_doc == null) return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.Control | Keys.Z: _doc.Undo(); return true;
                case Keys.Control | Keys.Y:
                case Keys.Control | Keys.Shift | Keys.Z: _doc.Redo(); return true;
                case Keys.F5: if (!_findingsOpen) ToggleFindings(); else RunValidation(); return true;
                case Keys.Control | Keys.R: Guard("реестр", ShowRegistry); return true;
                case Keys.Control | Keys.T: Guard("отношения", ShowRelations); return true;
                case Keys.Control | Keys.D0: _canvas.FitToView(); return true;
                case Keys.Control | Keys.Oemplus: _canvas.ZoomBy(1.25f); return true;
                case Keys.Control | Keys.OemMinus: _canvas.ZoomBy(1 / 1.25f); return true;
            }
            if (ActiveControl is TextBox || (ActiveControl != null && ActiveControl.ContainsFocus && ActiveControl is UiTextField))
                return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.B: SetTool(MapTool.Brush); return true;
                case Keys.R: SetTool(MapTool.Rect); return true;
                case Keys.F: SetTool(MapTool.Fill); return true;
                case Keys.I: SetTool(MapTool.Pipette); return true;
                case Keys.E: SetTool(MapTool.Eraser); return true;
                case Keys.M: SetTool(MapTool.Route); return true;
                case Keys.G: _canvas.ShowGrid = !_canvas.ShowGrid; _canvas.Invalidate(); return true;
                case Keys.D1: SetLayer(EditLayer.Landscape); return true;
                case Keys.D2: SetLayer(EditLayer.Political); return true;
                case Keys.D3: SetLayer(EditLayer.Wildlands); return true;
                case Keys.D4: SetLayer(EditLayer.Objects); return true;
                case Keys.Back: if (_canvas.Tool == MapTool.Route) { _canvas.RouteBackspace(); return true; } break;
                case Keys.Enter: if (_canvas.Tool == MapTool.Route) { Guard("маршрут", CommitRoute); return true; } break;
                case Keys.Escape:
                    if (_findingsOpen) { ToggleFindings(); return true; }
                    if (_canvas.Tool == MapTool.Route) { _canvas.ClearRouteBuffer(); return true; }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy || !ConfirmDiscard()) e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UiTheme.Changed -= OnThemeChanged;
            base.OnFormClosed(e);
        }

        // Для пробы интерфейса на Linux без диалогов.
        internal void LoadForProbe(WorldDocument doc) { SetDocument(doc); }
        internal MapCanvas Canvas { get { return _canvas; } }
        internal void ProbeSelectLayer(EditLayer layer) { SetLayer(layer); }
        internal void ProbeToggleFindings() { ToggleFindings(); }
        internal void ProbeTool(MapTool tool) { SetTool(tool); }
        internal void ProbeShowToast(string text) { _toast.Show(text, ToastKind.Warning); }
    }
}
