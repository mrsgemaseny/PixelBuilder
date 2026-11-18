using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PixelBuilder.Kit;

namespace PixelBuilder
{
    public partial class MainForm : Form
    {
        // Поля, на которые опираются тесты (Tests.cs, BuildingTests.cs): имена и базовые типы не меняются.
        private CanvasControl _canvas;
        private CanvasControl _globalCanvas;
        private CanvasControl _localCanvas;
        private TabControl _mapTabs;
        private Label _lblStatus;
        private Label _lblCoords;
        private Label _lblActiveBlock;
        private Button _btnUndo;
        private Button _btnRedo;
        private Button _btnGrid;
        private Button _btnCenter;
        private Button _btnCut;
        private Button _btnPaste;
        private Button _btnDeleteSel;
        private readonly Dictionary<string, Button> _paletteBtns = new Dictionary<string, Button>();
        private readonly Dictionary<ToolMode, Button> _toolBtns = new Dictionary<ToolMode, Button>();
        private Label _modeHeading;
        private Label _objectsHeading;
        private FlowLayoutPanel _objectPanel;
        private FlowLayoutPanel _buildingTools;
        private Button _btnRotate;
        private bool _fullscreen;
        private FormWindowState _stateBeforeFullscreen;
        private Button _btnRoofs;
        private Button _politicsButton;
        private Button _landscapeButton;
        private readonly Timer _wasdTimer = new Timer();
        private readonly HashSet<Keys> _pressedKeys = new HashSet<Keys>();

        // Новый интерфейс.
        private UiButton _eyeLandscape;
        private UiButton _eyePolitics;
        private UiSegmented _mapSwitch;
        private UiLabel _subtitle;
        private UiLabel _modeSubtitle;
        private UiSwatchTile _activeSwatch;
        private UiSurface _statusPill;
        private UiSurface _emptyHint;
        private UiToast _toast;
        private FlowLayoutPanel _colors;
        private FlowLayoutPanel _recentColors;
        private UiButton _colorsToggle;
        private readonly List<string> _recent = new List<string>();

        public MainForm()
        {
            Text = "Pixel Architect";
            AutoScaleMode = AutoScaleMode.None;
            Size = Ui.S(1380, 860);
            MinimumSize = Ui.S(1000, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = Ui.Body;
            KeyPreview = true;

            InitializeUI();
            SetupWasdTimer();
            UiTheme.Changed += OnThemeChanged;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UiTheme.Changed -= OnThemeChanged;
            _wasdTimer.Stop();
            base.OnFormClosed(e);
        }

        private void SetupWasdTimer()
        {
            _wasdTimer.Interval = 16;
            _wasdTimer.Tick += (s, e) =>
            {
                // Камера не двигается, пока печатают в поле ввода.
                if (ActiveControl is TextBox) return;
                float speed = 14f;
                float dx = 0, dy = 0;
                if (_pressedKeys.Contains(Keys.W) || _pressedKeys.Contains(Keys.Up)) dy += speed;
                if (_pressedKeys.Contains(Keys.S) || _pressedKeys.Contains(Keys.Down)) dy -= speed;
                if (_pressedKeys.Contains(Keys.A) || _pressedKeys.Contains(Keys.Left)) dx += speed;
                if (_pressedKeys.Contains(Keys.D) || _pressedKeys.Contains(Keys.Right)) dx -= speed;

                if (dx != 0 || dy != 0)
                {
                    _canvas.MoveCamera(dx, dy);
                }
            };
            _wasdTimer.Start();
        }
    }
}
