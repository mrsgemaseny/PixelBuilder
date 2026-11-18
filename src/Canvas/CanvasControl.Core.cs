using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace PixelBuilder
{
    public partial class CanvasControl : Control
    {
        public const int CellSize = 32;
        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public string MapKind { get; private set; }

        public float Zoom = 0.50f;
        public PointF PanOffset = new PointF(0, 0);
        public bool ShowGrid = true;
        public ToolMode CurrentTool = ToolMode.Brush;
        public string ActiveBlockId = "stone_wall";

        private readonly MapLayer _politicalLayer = new MapLayer();
        private readonly MapLayer _landscapeLayer = new MapLayer();
        private readonly MapLayer _objectLayer = new MapLayer();
        private MapLayer _activeLayer;
        private MapLayer _editLayer;
        public string ActiveLayer { get; private set; }
        public bool ShowPoliticalLayer = true;
        public bool ShowLandscapeLayer = true;
        public Dictionary<Point, string> Cells { get { return _activeLayer.Cells; } private set { _activeLayer.Cells = value; } }
        public Dictionary<Point, string> Objects { get { return _objectLayer.Cells; } }
        public int TotalBlockCount { get { return _politicalLayer.Cells.Count + _landscapeLayer.Cells.Count + _objectLayer.Cells.Count; } }

        public bool HasSelection = false;
        public Rectangle SelectionBounds = Rectangle.Empty;
        public Dictionary<Point, string> ClipboardBuffer = new Dictionary<Point, string>();
        private Point _selectStartCell;
        private bool _isSelecting = false;

        public event Action<Point, string, bool> HoverChanged;
        public event Action<int, int> StatsChanged;
        public event Action HistoryChanged;
        public event Action SelectionChanged;

        private bool _isPanning = false;
        private PointF _panStartMouse;
        private PointF _panStartOffset;

        private bool _isPainting = false;
        private bool _isErasing = false;
        private Point _lastPaintedCell = new Point(-9999, -9999);
        private Point _toolStartCell;
        private Point _currentHoverCell;
        private bool _isHoverValid = false;

        private Stack<Dictionary<Point, KeyValuePair<string, string>>> _undoStack { get { return _editLayer.Undo; } }
        private Stack<Dictionary<Point, KeyValuePair<string, string>>> _redoStack { get { return _editLayer.Redo; } }
        private Dictionary<Point, KeyValuePair<string, string>> _currentStrokeDiff { get { return _editLayer.Stroke; } }
        private Dictionary<Point, string> EditCells { get { return _editLayer.Cells; } }
        private static bool IsObject(string id) { if (GetBuildingDef(id) != null) return true; BlockDef def; return BlockDefs.TryGetValue(id, out def) && !string.IsNullOrEmpty(def.Symbol); }
        private void SetEditLayer(bool eraseObject = false)
        {
            _editLayer = IsObject(ActiveBlockId) || eraseObject ? _objectLayer : _activeLayer;
            if (HistoryChanged != null) HistoryChanged();
        }
        public void RefreshEditingLayer() { SetEditLayer(); }

        public CanvasControl(string mapKind, int width, int height)
        {
            MapKind = mapKind;
            MapWidth = width;
            MapHeight = height;
            _activeLayer = _landscapeLayer;
            _editLayer = _activeLayer;
            ActiveLayer = "landscape";
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Kit.UiTheme.C.CanvasBack; // цвета карты берутся из темы (светлая или тёмная)

            CenterOnMap();
        }

        public void SetActiveLayer(string layer)
        {
            if (layer != "political" && layer != "landscape") return;
            if (layer == ActiveLayer) return;
            ClearSelection();
            ActiveLayer = layer;
            _activeLayer = layer == "political" ? _politicalLayer : _landscapeLayer;
            SetEditLayer();
            if (layer == "political") ShowPoliticalLayer = true;
            else ShowLandscapeLayer = true;
            if (HistoryChanged != null) HistoryChanged();
            NotifyStats();
            Invalidate();
        }

        public bool IsInMap(Point cell)
        {
            if (cell.X < 0 || cell.X >= MapWidth || cell.Y < 0 || cell.Y >= MapHeight) return false;
            return MapKind != "global" || GlobalLandRows[cell.Y][cell.X] != '.';
        }

        public Point ScreenToCell(Point screenPt)
        {
            float cx = Width * 0.5f;
            float cy = Height * 0.5f;
            float wx = (screenPt.X - cx) / Zoom - PanOffset.X;
            float wy = (screenPt.Y - cy) / Zoom - PanOffset.Y;
            return new Point((int)Math.Floor(wx / CellSize), (int)Math.Floor(wy / CellSize));
        }

        public RectangleF CellToScreenRect(Point cell)
        {
            float cx = Width * 0.5f;
            float cy = Height * 0.5f;
            float sx = (cell.X * CellSize + PanOffset.X) * Zoom + cx;
            float sy = (cell.Y * CellSize + PanOffset.Y) * Zoom + cy;
            float sz = CellSize * Zoom;
            return new RectangleF(sx, sy, sz, sz);
        }

        public void MoveCamera(float dx, float dy)
        {
            PanOffset = new PointF(PanOffset.X + dx / Zoom, PanOffset.Y + dy / Zoom);
            Invalidate();
        }

        public void CenterOnMap()
        {
            float targetWorldX = (MapWidth * 0.5f) * CellSize;
            float targetWorldY = (MapHeight * 0.5f) * CellSize;
            PanOffset = new PointF(-targetWorldX, -targetWorldY);
            Zoom = MapKind == "global" ? 1.0f : 0.50f;
            NotifyStats();
            Invalidate();
        }

        public void CenterOnOrigin()
        {
            PanOffset = new PointF(0, 0);
            Zoom = 1.0f;
            NotifyStats();
            Invalidate();
        }

        public void NotifyStats()
        {
            if (StatsChanged != null) StatsChanged(TotalBlockCount, (int)Math.Round(Zoom * 100));
        }
    }
}
