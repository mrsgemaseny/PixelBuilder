using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelBuilder.Kit;

namespace PixelBuilder
{
    // Раскладка: панель сверху (карта, инструменты, история, файл), карта по центру, боковая панель справа
    // (слои, активный блок, блоки, поселения или дома, цвета). Подсказки и статус - поверх карты.
    public partial class MainForm
    {
        private void InitializeUI()
        {
            UiSurface toolbar = BuildToolbar();
            UiSurface sidebar = BuildSidebar();

            // Вкладки остаются (тесты и логика переключения опираются на TabControl), но их заголовки скрыты:
            // переключает сегмент "Глобальная / Локальная" на панели сверху.
            _mapTabs = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons,
                SizeMode = TabSizeMode.Fixed, ItemSize = new Size(0, 1) };
            _globalCanvas = new CanvasControl("global", 21, 15) { Dock = DockStyle.Fill };
            _localCanvas = new CanvasControl("local", 1000, 1000) { Dock = DockStyle.Fill };
            AddMapPage("Глобальная карта", _globalCanvas);
            AddMapPage("Локальная карта", _localCanvas);
            WireCanvas(_globalCanvas);
            WireCanvas(_localCanvas);
            _canvas = _globalCanvas;
            _mapTabs.SelectedIndexChanged += (s, e) => OnMapChanged();

            BuildOverlays();
            Controls.Add(_mapTabs);
            Controls.Add(sidebar);
            Controls.Add(toolbar);
            Controls.Add(_statusPill);
            Controls.Add(_emptyHint);
            Controls.Add(_toast);
            _statusPill.BringToFront();
            _emptyHint.BringToFront();
            _toast.BringToFront();
            _mapTabs.SizeChanged += (s, e) => PlaceOverlays();
            _mapTabs.LocationChanged += (s, e) => PlaceOverlays();
            Layout += (s, e) => PlaceOverlays();
            Shown += (s, e) => PlaceOverlays();

            SelectBlock("stone_wall");
            SelectTool(ToolMode.Brush);
            UpdateTitle();
            RefreshSidebar();
            ApplyCanvasTheme();
            UiTheme.Apply(this);
            UpdateEmptyHint();
        }

        // ================= панель сверху =================

        private UiSurface BuildToolbar()
        {
            var bar = new UiSurface(SurfaceRole.Toolbar) { Dock = DockStyle.Top, Height = Ui.S(56), LineBottom = true,
                Padding = new Padding(Ui.S(14), Ui.S(10), Ui.S(12), Ui.S(10)) };

            var titleBox = new UiSurface(SurfaceRole.Toolbar) { Dock = DockStyle.Left, Width = Ui.S(170) };
            var title = new UiLabel("Pixel Architect", TextRole.Primary) { Dock = DockStyle.Top, Height = Ui.S(19), Font = Ui.BodyStrong };
            _subtitle = new UiLabel("", TextRole.Secondary) { Dock = DockStyle.Top, Height = Ui.S(17), AutoEllipsis = true };
            titleBox.Controls.Add(_subtitle);
            titleBox.Controls.Add(title);

            _mapSwitch = new UiSegmented("Глобальная", "Локальная") { Dock = DockStyle.Left };
            _mapSwitch.Width = _mapSwitch.PreferredWidth() + Ui.S(20);
            _mapSwitch.SelectedIndexChanged += () => { if (_mapTabs.SelectedIndex != _mapSwitch.SelectedIndex) _mapTabs.SelectedIndex = _mapSwitch.SelectedIndex; };
            Ui.Tip(_mapSwitch, "Глобальная карта 21 x 15 или локальная 1000 x 1000", null);

            var tools = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, BackColor = Color.Transparent,
                Padding = new Padding(Ui.S(18), 0, 0, 0) };
            AddToolBtn(tools, ToolMode.Brush, Glyph.Pencil, "Карандаш", "B");
            AddToolBtn(tools, ToolMode.Line, Glyph.Line, "Линия", "L");
            AddToolBtn(tools, ToolMode.Rect, Glyph.Rect, "Прямоугольник", "R");
            AddToolBtn(tools, ToolMode.Fill, Glyph.Fill, "Заливка", "F");
            AddToolBtn(tools, ToolMode.Eraser, Glyph.Eraser, "Ластик", "E");
            AddToolBtn(tools, ToolMode.Select, Glyph.Select, "Выделение: обвести область, затем вырезать, копировать или удалить", null);
            tools.Controls.Add(Divider());
            _btnCut = IconButton(Glyph.Cut, "Вырезать выделенное", "Ctrl+X", () => _canvas.CutSelection());
            _btnPaste = IconButton(Glyph.Paste, "Вставить скопированное", "Ctrl+V", () => _canvas.StartPaste());
            _btnDeleteSel = IconButton(Glyph.Trash, "Удалить выделенное", "Del", () => _canvas.DeleteSelection());
            _btnCut.Enabled = false;
            _btnPaste.Enabled = false;
            _btnDeleteSel.Enabled = false;
            tools.Controls.Add(_btnCut);
            tools.Controls.Add(_btnPaste);
            tools.Controls.Add(_btnDeleteSel);

            var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
            _btnUndo = IconButton(Glyph.Undo, "Отменить", "Ctrl+Z", () => _canvas.Undo());
            _btnRedo = IconButton(Glyph.Redo, "Повторить", "Ctrl+Y", () => _canvas.Redo());
            _btnUndo.Enabled = false;
            _btnRedo.Enabled = false;
            _btnGrid = IconButton(Glyph.Grid, "Сетка", "G", ToggleGrid);
            _btnCenter = IconButton(Glyph.Target, "К центру карты", "Home", () => _canvas.CenterOnMap());
            UiButton more = IconButton(Glyph.More, "Ещё: открыть, PNG, копировать JSON, очистить слой, тема", null, null);
            more.Click += (s, e) => ShowMoreMenu(more);
            var save = new UiButton("Сохранить", ButtonKind.Primary) { Height = Ui.S(34), Width = Ui.S(112), Margin = new Padding(Ui.S(10), 0, 0, 0) };
            save.Click += (s, e) => SaveJsonToFile();
            Ui.Tip(save, "Сохранить карту в JSON", "Ctrl+S");
            right.Controls.AddRange(new Control[] { _btnUndo, _btnRedo, Divider(), _btnGrid, _btnCenter, more, save });

            bar.Controls.Add(tools);
            bar.Controls.Add(_mapSwitch);
            bar.Controls.Add(titleBox);
            bar.Controls.Add(right);
            return bar;
        }

        private static Control Divider()
        {
            return new UiSurface(SurfaceRole.Toolbar) { Size = Ui.S(13, 34), LineLeft = false, Margin = new Padding(0), Padding = new Padding(0), Tag = "divider" };
        }

        private UiButton IconButton(Glyph glyph, string tip, string shortcut, Action action)
        {
            var b = new UiButton("", ButtonKind.Tool) { Icon = glyph, IconOnly = true, Size = Ui.S(34, 34), Margin = new Padding(Ui.S(2), 0, 0, 0) };
            if (action != null) b.Click += (s, e) => action();
            Ui.Tip(b, tip, shortcut);
            return b;
        }

        private void AddToolBtn(FlowLayoutPanel flow, ToolMode mode, Glyph glyph, string tip, string key)
        {
            UiButton b = IconButton(glyph, tip, key, () => SelectTool(mode));
            flow.Controls.Add(b);
            _toolBtns[mode] = b;
        }

        private void ShowMoreMenu(Control anchor)
        {
            ContextMenuStrip menu = UiMenuRenderer.Menu();
            UiMenuRenderer.Item(menu.Items, "Открыть JSON...", "Ctrl+O", OpenJsonFromFile);
            UiMenuRenderer.Item(menu.Items, "Сохранить JSON...", "Ctrl+S", SaveJsonToFile);
            UiMenuRenderer.Item(menu.Items, "Сохранить картинку PNG...", null, SavePngImage);
            UiMenuRenderer.Item(menu.Items, "Копировать JSON в буфер", null, CopyJsonToClipboard);
            menu.Items.Add(new ToolStripSeparator());
            UiMenuRenderer.Item(menu.Items, "Очистить слой...", null, ConfirmClearLayer);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem theme = UiMenuRenderer.Item(menu.Items, "Оформление", null, null);
            theme.DropDown = UiMenuRenderer.Menu();
            AddThemeItem(theme, "Как в Windows", ThemeMode.System);
            AddThemeItem(theme, "Светлое", ThemeMode.Light);
            AddThemeItem(theme, "Тёмное", ThemeMode.Dark);
            UiMenuRenderer.Item(menu.Items, "Во весь экран", "F11", ToggleFullscreen).Checked = _fullscreen;
            menu.Show(anchor, new Point(anchor.Width - menu.PreferredSize.Width, anchor.Height + Ui.S(4)));
        }

        private void AddThemeItem(ToolStripMenuItem parent, string text, ThemeMode mode)
        {
            ToolStripMenuItem item = UiMenuRenderer.Item(parent.DropDownItems, text, null, () => { UiSettings.SaveTheme(mode); UiTheme.SetMode(mode); });
            item.Checked = UiTheme.Mode == mode;
        }

        // ================= боковая панель =================

        private UiSurface BuildSidebar()
        {
            var sidebar = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Right, Width = Ui.S(300), LineLeft = true,
                Padding = new Padding(Ui.S(12), Ui.S(10), Ui.S(10), Ui.S(10)) };
            int inner = Ui.S(300 - 22);

            _modeHeading = new UiLabel("", TextRole.Title) { Dock = DockStyle.Top, Height = Ui.S(26) };
            _modeSubtitle = new UiLabel("", TextRole.Secondary) { Dock = DockStyle.Top, Height = Ui.S(20) };

            var layers = new UiGroup();
            layers.AddRow(LayerRow("Ландшафт", "landscape", out _landscapeButton, out _eyeLandscape));
            layers.AddRow(LayerRow("Политика", "political", out _politicsButton, out _eyePolitics));

            var activeRow = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Top, Height = Ui.S(40), Padding = new Padding(0, Ui.S(8), 0, 0) };
            _activeSwatch = new UiSwatchTile(Color.Gray, null) { Dock = DockStyle.Left, Enabled = false };
            _lblActiveBlock = new UiLabel("", TextRole.Primary) { Dock = DockStyle.Fill, Font = Ui.BodyStrong, AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Ui.S(6), 0, 0, 0) };
            activeRow.Controls.Add(_lblActiveBlock);
            activeRow.Controls.Add(_activeSwatch);

            var blocks = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Ui.S(32), WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            foreach (BlockDef def in CanvasControl.Palette)
            {
                var tile = new UiSwatchTile(def.Color, def.Hotkey) { Margin = new Padding(0, 0, Ui.S(0), 0) };
                tile.Size = new Size(Math.Min(Ui.S(28), inner / CanvasControl.Palette.Count), Ui.S(28));
                Ui.Tip(tile, def.Name, def.Hotkey);
                string bid = def.Id;
                tile.Click += (s, e) => SelectBlock(bid);
                blocks.Controls.Add(tile);
                _paletteBtns[bid] = tile;
            }

            _objectsHeading = new CaptionLabel("") { Dock = DockStyle.Top };
            _objectPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Ui.S(242), AutoScroll = true, WrapContents = true, BackColor = Color.Transparent };
            _buildingTools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Ui.S(40), WrapContents = false, BackColor = Color.Transparent,
                Padding = new Padding(0, Ui.S(6), 0, 0) };
            _btnRotate = new UiButton("Повернуть", ButtonKind.Secondary) { Icon = Glyph.Rotate, Width = Ui.S(134), Height = Ui.S(30), Margin = new Padding(0, 0, Ui.S(6), 0) };
            _btnRotate.Click += (s, e) => RotateBuilding();
            Ui.Tip(_btnRotate, "Повернуть дом", "T");
            _btnRoofs = new UiButton("Крыши", ButtonKind.Secondary) { Icon = Glyph.Roof, Width = Ui.S(134), Height = Ui.S(30), Margin = new Padding(0) };
            _btnRoofs.Click += (s, e) => ToggleRoofs();
            Ui.Tip(_btnRoofs, "Показать или скрыть крыши", "H");
            _buildingTools.Controls.Add(_btnRotate);
            _buildingTools.Controls.Add(_btnRoofs);

            // Цвета: сворачиваются, чтобы на локальной карте было место под дома.
            var colorsHeader = new UiSurface(SurfaceRole.Sidebar) { Dock = DockStyle.Top, Height = Ui.S(34) };
            _colorsToggle = new UiButton("ЦВЕТА", ButtonKind.Ghost) { Icon = Glyph.ChevronDown, Dock = DockStyle.Fill, Font = Ui.Caption };
            _colorsToggle.Click += (s, e) => ToggleColors();
            colorsHeader.Controls.Add(_colorsToggle);
            _recentColors = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Ui.S(32), WrapContents = false, BackColor = Color.Transparent, Visible = false };
            _colors = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent };
            foreach (BlockDef def in CanvasControl.ColorPalette)
            {
                var swatch = new UiSwatchTile(def.Color, null) { Margin = new Padding(0, 0, Ui.S(2), Ui.S(2)) };
                Ui.Tip(swatch, def.Name, null);
                string colorId = def.Id;
                swatch.Click += (s, e) => { SelectBlock(colorId); SelectTool(ToolMode.Brush); };
                _colors.Controls.Add(swatch);
                _paletteBtns[colorId] = swatch;
            }
            var customColor = new UiButton("Свой цвет...", ButtonKind.Secondary) { Icon = Glyph.Plus, Width = Ui.S(130), Height = Ui.S(28), Margin = new Padding(0, Ui.S(6), 0, 0) };
            customColor.Click += (s, e) => PickCustomColor();
            _colors.Controls.Add(customColor);
            _colors.SetFlowBreak(_colors.Controls[_colors.Controls.Count - 2], true);

            sidebar.Controls.Add(_colors);
            sidebar.Controls.Add(_recentColors);
            sidebar.Controls.Add(colorsHeader);
            sidebar.Controls.Add(_buildingTools);
            sidebar.Controls.Add(_objectPanel);
            sidebar.Controls.Add(_objectsHeading);
            sidebar.Controls.Add(blocks);
            sidebar.Controls.Add(new CaptionLabel("Блоки") { Dock = DockStyle.Top });
            sidebar.Controls.Add(activeRow);
            sidebar.Controls.Add(new CaptionLabel("Активный блок") { Dock = DockStyle.Top });
            sidebar.Controls.Add(layers);
            sidebar.Controls.Add(new CaptionLabel("Слои") { Dock = DockStyle.Top });
            sidebar.Controls.Add(_modeSubtitle);
            sidebar.Controls.Add(_modeHeading);
            return sidebar;
        }

        // Строка слоя: имя (клик - сделать активным для рисования) и глаз (виден или скрыт).
        private Control LayerRow(string text, string layer, out Button select, out UiButton eye)
        {
            var row = new UiSurface(SurfaceRole.Content) { Height = Ui.S(38), Padding = new Padding(Ui.S(4)) };
            var name = new UiButton(text, ButtonKind.Row) { Dock = DockStyle.Fill, Icon = Glyph.Layers };
            var eyeButton = new UiButton("", ButtonKind.Tool) { Icon = Glyph.Eye, IconOnly = true, Dock = DockStyle.Right, Width = Ui.S(32) };
            name.Click += (s, e) => { _canvas.SetActiveLayer(layer); RefreshSidebar(); };
            eyeButton.Click += (s, e) =>
            {
                if (layer == "political") _canvas.ShowPoliticalLayer = !_canvas.ShowPoliticalLayer;
                else _canvas.ShowLandscapeLayer = !_canvas.ShowLandscapeLayer;
                _canvas.Invalidate();
                RefreshLayerRows();
            };
            Ui.Tip(name, "Рисовать на слое \"" + text + "\"", null);
            Ui.Tip(eyeButton, "Показать или скрыть слой", null);
            row.Controls.Add(name);
            row.Controls.Add(eyeButton);
            select = name;
            eye = eyeButton;
            return row;
        }

        private void RefreshLayerRows()
        {
            ((UiButton)_landscapeButton).Checked = _canvas.ActiveLayer == "landscape";
            ((UiButton)_politicsButton).Checked = _canvas.ActiveLayer == "political";
            _eyeLandscape.Icon = _canvas.ShowLandscapeLayer ? Glyph.Eye : Glyph.EyeOff;
            _eyePolitics.Icon = _canvas.ShowPoliticalLayer ? Glyph.Eye : Glyph.EyeOff;
            _eyeLandscape.Invalidate();
            _eyePolitics.Invalidate();
        }

        private void ToggleColors()
        {
            _colors.Visible = !_colors.Visible;
            _recentColors.Visible = _colors.Visible && _recentColors.Controls.Count > 0;
            _colorsToggle.Icon = _colors.Visible ? Glyph.ChevronDown : Glyph.ChevronRight;
            _colorsToggle.Invalidate();
        }

        private void PickCustomColor()
        {
            using (ColorDialog dialog = new ColorDialog { FullOpen = true, AnyColor = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SelectBlock(CanvasControl.RegisterColor(dialog.Color).Id);
                SelectTool(ToolMode.Brush);
            }
        }

        // Недавние цвета: последние 9 выбранных, включая свои. Только в памяти, в файл не пишутся.
        private void RememberColor(string id)
        {
            if (!id.StartsWith("color_") || !CanvasControl.BlockDefs.ContainsKey(id)) return;
            _recent.Remove(id);
            _recent.Insert(0, id);
            if (_recent.Count > 9) _recent.RemoveAt(_recent.Count - 1);
            while (_recentColors.Controls.Count > 0) { Control c = _recentColors.Controls[0]; _recentColors.Controls.RemoveAt(0); c.Dispose(); }
            foreach (string rid in _recent)
            {
                BlockDef def = CanvasControl.BlockDefs[rid];
                var tile = new UiSwatchTile(def.Color, null) { Margin = new Padding(0, 0, Ui.S(2), 0), Checked = rid == id };
                Ui.Tip(tile, "Недавний: " + def.Name, null);
                string colorId = rid;
                tile.Click += (s, e) => { SelectBlock(colorId); SelectTool(ToolMode.Brush); };
                _recentColors.Controls.Add(tile);
            }
            _recentColors.Visible = _colors.Visible;
        }

        // ================= карта и поверх неё =================

        private void AddMapPage(string title, CanvasControl canvas)
        {
            TabPage page = new TabPage(title) { Padding = new Padding(0), Margin = new Padding(0) };
            page.Controls.Add(canvas);
            _mapTabs.TabPages.Add(page);
        }

        private void BuildOverlays()
        {
            _statusPill = new UiSurface(SurfaceRole.Content) { Size = Ui.S(300, 30), Padding = new Padding(Ui.S(12), 0, Ui.S(12), 0) };
            _lblCoords = new UiLabel("", TextRole.Primary) { Dock = DockStyle.Left, Width = Ui.S(110), Font = Ui.Mono, TextAlign = ContentAlignment.MiddleLeft };
            _lblStatus = new UiLabel("", TextRole.Secondary) { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            _statusPill.Controls.Add(_lblStatus);
            _statusPill.Controls.Add(_lblCoords);

            _emptyHint = new UiSurface(SurfaceRole.Content) { Size = Ui.S(380, 112), Padding = new Padding(Ui.S(18), Ui.S(16), Ui.S(18), Ui.S(12)) };
            var hintTitle = new UiLabel("Карта пустая", TextRole.Title) { Dock = DockStyle.Top, Height = Ui.S(28), TextAlign = ContentAlignment.MiddleCenter };
            var hintText = new UiLabel("Выберите цвет или блок справа и рисуйте левой кнопкой мыши. Правая кнопка стирает, колесо - масштаб, WASD - движение. Или откройте готовую карту через меню \"...\".",
                TextRole.Secondary) { Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter };
            _emptyHint.Controls.Add(hintText);
            _emptyHint.Controls.Add(hintTitle);

            _toast = new UiToast();
        }

        private void PlaceOverlays()
        {
            if (_statusPill == null || _mapTabs == null) return;
            // Рамка и полоска вкладок обрезаются: видна только страница с картой.
            Rectangle page = _mapTabs.DisplayRectangle;
            if (page.Width > 0 && page.Height > 0) _mapTabs.Region = new Region(page);
            Rectangle area = new Rectangle(_mapTabs.Left + page.X, _mapTabs.Top + page.Y, page.Width, page.Height);
            if (area.Width <= 0 || area.Height <= 0) return;
            _statusPill.Location = new Point(area.X + Ui.S(14), area.Bottom - _statusPill.Height - Ui.S(14));
            RoundRegion(_statusPill, _statusPill.Height / 2f);
            _emptyHint.Location = new Point(area.X + (area.Width - _emptyHint.Width) / 2, area.Y + (area.Height - _emptyHint.Height) / 2);
            RoundRegion(_emptyHint, Ui.S(14f));
            _toast.Area = area;
        }

        private static void RoundRegion(Control c, float radius)
        {
            using (GraphicsPath p = Ui.Round(new RectangleF(0, 0, c.Width, c.Height), radius)) c.Region = new Region(p);
        }

        private void UpdateEmptyHint()
        {
            if (_emptyHint == null) return;
            _emptyHint.Visible = _canvas.TotalBlockCount == 0;
            if (_emptyHint.Visible) _emptyHint.BringToFront();
        }

        private void UpdateTitle()
        {
            bool global = _canvas.MapKind == "global";
            Text = "Pixel Architect - " + (global ? "глобальная карта 21 x 15" : "локальная карта 1000 x 1000");
            _subtitle.Text = global ? "Глобальная 21 x 15" : "Локальная 1000 x 1000";
            UpdateStatus(_canvas.TotalBlockCount, (int)(_canvas.Zoom * 100));
        }

        private void UpdateStatus(int blocks, int zoom)
        {
            _lblStatus.Text = string.Format("{0} блоков   {1}%", blocks, zoom);
        }

        private void OnMapChanged()
        {
            _canvas = _mapTabs.SelectedIndex == 0 ? _globalCanvas : _localCanvas;
            if (_mapSwitch.SelectedIndex != _mapTabs.SelectedIndex) _mapSwitch.SelectedIndex = _mapTabs.SelectedIndex;
            UpdateTitle();
            RefreshSidebar();
            SelectBlock(_canvas.ActiveBlockId);
            _btnUndo.Enabled = _canvas.CanUndo;
            _btnRedo.Enabled = _canvas.CanRedo;
            _btnCut.Enabled = _canvas.HasSelection;
            _btnDeleteSel.Enabled = _canvas.HasSelection;
            ((UiButton)_btnGrid).Checked = _canvas.ShowGrid;
            UpdateEmptyHint();
            _canvas.Focus();
        }

        private void RefreshSidebar()
        {
            bool global = _canvas.MapKind == "global";
            _modeHeading.Text = global ? "Глобальная карта" : "Локальная карта";
            _modeSubtitle.Text = global ? "21 x 15 клеток, одна клетка - регион" : "1000 x 1000 клеток, одна клетка - тайл";
            _objectsHeading.Text = global ? "Поселения (1 × 1)" : "Жильё";
            _buildingTools.Visible = !global;
            _objectPanel.Height = global ? Ui.S(242) : Ui.S(300);
            UpdateRoofsButton();
            RefreshLayerRows();
            while (_objectPanel.Controls.Count > 0)
            {
                Control old = _objectPanel.Controls[0];
                _paletteBtns.Remove(old.Name);
                _objectPanel.Controls.RemoveAt(0);
                old.Dispose();
            }
            int inner = Ui.S(278);
            if (!global)
            {
                foreach (var preset in CanvasControl.BuildingPresets)
                {
                    BuildingDef def = CanvasControl.GetBuildingDef(CanvasControl.BuildingId(preset.Value.X, preset.Value.Y, 'h'));
                    var card = new UiButton(string.Format("{0} {1}x{2}", preset.Key, preset.Value.X, preset.Value.Y), ButtonKind.Card)
                    {
                        Name = def.Id, Width = (inner - SystemInformation.VerticalScrollBarWidth - Ui.S(14)) / 2, Height = Ui.S(72), Margin = new Padding(0, 0, Ui.S(6), Ui.S(6)),
                        Preview = HousePreview(preset.Value.X, preset.Value.Y)
                    };
                    string buildingId = def.Id;
                    card.Click += (s, e) => { SelectBlock(buildingId); SelectTool(ToolMode.Brush); };
                    Ui.Tip(card, string.Format("{0}: внутри {1} x {2}, со стенами {3} x {4}", preset.Key, preset.Value.X, preset.Value.Y, def.OuterW, def.OuterH), null);
                    _objectPanel.Controls.Add(card);
                    _paletteBtns[buildingId] = card;
                }
            }
            else
            {
                foreach (BlockDef def in CanvasControl.GlobalObjects)
                {
                    var row = new UiButton(def.Name, ButtonKind.Row) { Name = def.Id, Swatch = def.Color, SwatchLabel = def.Symbol,
                        Width = inner - Ui.S(4), Height = Ui.S(32), Margin = new Padding(0, 0, 0, Ui.S(2)) };
                    string objectId = def.Id;
                    row.Click += (s, e) => { SelectBlock(objectId); SelectTool(ToolMode.Brush); };
                    _objectPanel.Controls.Add(row);
                    _paletteBtns[objectId] = row;
                }
            }
            UiTheme.Apply(_objectPanel);
            SelectBlock(_canvas.ActiveBlockId);
        }

        // Силуэт дома в пропорциях: стены, пол, дверь снизу по центру.
        private static Action<Graphics, Rectangle> HousePreview(int interiorW, int interiorH)
        {
            return (g, box) =>
            {
                int w = interiorW + 2, h = interiorH + 2;
                float cell = Math.Min(box.Width / (float)w, box.Height / (float)h);
                cell = Math.Min(cell, Ui.S(6f));
                float ox = box.X + (box.Width - cell * w) / 2f, oy = box.Y + (box.Height - cell * h) / 2f;
                using (var wall = new SolidBrush(Color.FromArgb(140, 90, 43)))
                    g.FillRectangle(wall, ox, oy, cell * w, cell * h);
                using (var floor = new SolidBrush(Color.FromArgb(196, 152, 104)))
                    g.FillRectangle(floor, ox + cell, oy + cell, cell * interiorW, cell * interiorH);
                using (var door = new SolidBrush(Color.FromArgb(232, 190, 90)))
                    g.FillRectangle(door, ox + cell * ((w - 1) / 2), oy + cell * (h - 1), cell, cell);
            };
        }

        private void WireCanvas(CanvasControl canvas)
        {
            canvas.HoverChanged += (pt, bid, isValid) =>
            {
                if (canvas != _canvas) return;
                _lblCoords.Text = isValid ? string.Format("{0}, {1}", pt.X, pt.Y) : "вне карты";
                ((UiLabel)_lblCoords).Role = isValid ? TextRole.Primary : TextRole.Tertiary;
            };
            canvas.StatsChanged += (blocks, zoom) =>
            {
                if (canvas != _canvas) return;
                UpdateStatus(blocks, zoom);
                UpdateEmptyHint();
            };
            canvas.HistoryChanged += () =>
            {
                if (canvas == _canvas) { _btnUndo.Enabled = canvas.CanUndo; _btnRedo.Enabled = canvas.CanRedo; UpdateEmptyHint(); }
            };
            canvas.SelectionChanged += () =>
            {
                if (canvas == _canvas) { _btnCut.Enabled = canvas.HasSelection; _btnDeleteSel.Enabled = canvas.HasSelection; }
            };
        }

        // ================= тема =================

        private void ApplyCanvasTheme()
        {
            foreach (CanvasControl c in new[] { _globalCanvas, _localCanvas })
            {
                c.BackColor = UiTheme.C.CanvasBack;
                c.Invalidate();
            }
            foreach (TabPage p in _mapTabs.TabPages) p.BackColor = UiTheme.C.CanvasBack;
        }

        private void OnThemeChanged()
        {
            UiTheme.Apply(this);
            ApplyCanvasTheme();
            RefreshSidebar();
            Invalidate(true);
        }

        // Для пробы интерфейса на Linux.
        internal void ProbeToast(string text) { _toast.Show(text, ToastKind.Success); }
    }

    // Заголовок раздела: текст хранится как есть (тесты сверяют его), рисуется прописными мелким шрифтом.
    internal sealed class CaptionLabel : Label
    {
        public CaptionLabel(string text)
        {
            Text = text;
            Height = Ui.S(32);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.BackOf(this, UiTheme.C.Sidebar));
            Ui.DrawText(e.Graphics, (Text ?? "").ToUpperInvariant(), Ui.Caption, UiTheme.C.TextSecondary,
                new Rectangle(Ui.S(2), 0, Width - Ui.S(4), Height - Ui.S(6)), TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
