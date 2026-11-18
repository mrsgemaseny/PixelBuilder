using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    internal enum MapTool { Brush, Rect, Fill, Pipette, Eraser, Route }

    // Холст глобальной карты (M1). Только отображение и ввод; правила правок - в WorldEdits, правки - командами.
    internal sealed class MapCanvas : Control
    {
        private readonly Dictionary<EditLayer, bool> _visible = new Dictionary<EditLayer, bool>();
        private readonly Dictionary<EditLayer, bool> _locked = new Dictionary<EditLayer, bool>();
        private readonly List<CellCoord> _stroke = new List<CellCoord>();
        private readonly HashSet<CellCoord> _strokeSet = new HashSet<CellCoord>();
        private readonly List<CellCoord> _routeBuffer = new List<CellCoord>();
        private readonly HashSet<CellCoord> _highlight = new HashSet<CellCoord>();

        private WorldDocument _doc;
        private float _cell = 40f;
        private PointF _offset = new PointF(20, 20);
        private bool _painting;
        private bool _panning;
        private Point _panStart;
        private PointF _panOffsetStart;
        private CellCoord? _rectStart;
        private CellCoord? _hover;
        private CellCoord _lastStrokeCell;

        public MapCanvas()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            BackColor = UiTheme.C.CanvasBack;
            foreach (EditLayer l in new[] { EditLayer.Landscape, EditLayer.Political, EditLayer.Wildlands, EditLayer.Objects })
            {
                _visible[l] = true;
                _locked[l] = false;
            }
            ShowGrid = true;
            ActiveLayer = EditLayer.Landscape;
            Tool = MapTool.Brush;
        }

        public event Action<CellCoord?> HoverChanged;
        public event Action<CellCoord> CellSelected;
        public event Action<EditLayer, string> ValuePicked;
        public event Action<string> Message;
        public event Action RouteBufferChanged;

        public WorldDocument Document
        {
            get { return _doc; }
            set
            {
                _doc = value;
                SelectedCell = null;
                _routeBuffer.Clear();
                _highlight.Clear();
                FitToView();
                Invalidate();
            }
        }

        public EditLayer ActiveLayer { get; set; }
        public MapTool Tool { get; set; }
        // id типа ландшафта / государства / группы; для слоя объектов - код типа объекта.
        public string ActiveValue { get; set; }
        public bool ShowGrid { get; set; }
        public CellCoord? SelectedCell { get; private set; }
        public string RouteArmyId { get; set; }
        public IList<CellCoord> RouteBuffer { get { return _routeBuffer.AsReadOnly(); } }

        public bool IsVisible(EditLayer l) { return _visible[l]; }
        public bool IsLocked(EditLayer l) { return _locked[l]; }
        public void SetVisible(EditLayer l, bool v) { _visible[l] = v; Invalidate(); }
        public void SetLocked(EditLayer l, bool v) { _locked[l] = v; }

        public void Select(CellCoord? cell)
        {
            SelectedCell = cell;
            Invalidate();
            if (CellSelected != null && cell.HasValue) CellSelected(cell.Value); // инспектор следует и за программным выбором (находки)
        }

        public void Highlight(IEnumerable<CellCoord> cells)
        {
            _highlight.Clear();
            if (cells != null) foreach (CellCoord c in cells) _highlight.Add(c);
            Invalidate();
        }

        public void StartRoute(string armyId, IEnumerable<CellCoord> existing)
        {
            RouteArmyId = armyId;
            _routeBuffer.Clear();
            if (existing != null) _routeBuffer.AddRange(existing);
            RaiseRoute();
            Invalidate();
        }

        public void RouteBackspace()
        {
            if (_routeBuffer.Count == 0) return;
            _routeBuffer.RemoveAt(_routeBuffer.Count - 1);
            RaiseRoute();
            Invalidate();
        }

        public void ClearRouteBuffer()
        {
            _routeBuffer.Clear();
            RaiseRoute();
            Invalidate();
        }

        public void FitToView()
        {
            if (_doc == null || Width < 50 || Height < 50) return;
            GridSpec g = _doc.World.Grid;
            _cell = Math.Max(8f, Math.Min((Width - 40f) / g.Width, (Height - 40f) / g.Height));
            _offset = new PointF((Width - _cell * g.Width) / 2f, (Height - _cell * g.Height) / 2f);
            if (ZoomChanged != null) ZoomChanged();
            Invalidate();
        }

        public int ZoomPercent { get { return (int)Math.Round(_cell / 40f * 100); } }
        public event Action ZoomChanged;

        public void ZoomBy(float factor)
        {
            var center = new Point(Width / 2, Height / 2);
            ZoomAround(center, factor);
        }

        private void ZoomAround(Point p, float factor)
        {
            float old = _cell;
            _cell = Math.Max(8f, Math.Min(160f, _cell * factor));
            _offset = new PointF(p.X - (p.X - _offset.X) * _cell / old, p.Y - (p.Y - _offset.Y) * _cell / old);
            if (ZoomChanged != null) ZoomChanged();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_doc != null && _cell <= 8.01f) FitToView();
            Invalidate();
        }

        // ---------- координаты ----------

        private CellCoord ToCell(Point p)
        {
            return new CellCoord((int)Math.Floor((p.X - _offset.X) / _cell), (int)Math.Floor((p.Y - _offset.Y) / _cell));
        }

        private RectangleF Rect(CellCoord c)
        {
            return new RectangleF(_offset.X + c.X * _cell, _offset.Y + c.Y * _cell, _cell, _cell);
        }

        private PointF Center(CellCoord c)
        {
            return new PointF(_offset.X + (c.X + 0.5f) * _cell, _offset.Y + (c.Y + 0.5f) * _cell);
        }

        private bool InGrid(CellCoord c) { return _doc != null && _doc.World.Grid.Contains(c); }

        // ---------- ввод ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (_doc == null) return;
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                _panning = true;
                _panStart = e.Location;
                _panOffsetStart = _offset;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            CellCoord cell = ToCell(e.Location);
            if (!InGrid(cell)) return; // невидимая стена: за краем сетки ничего не происходит
            SelectedCell = cell;
            if (CellSelected != null) CellSelected(cell);

            switch (Tool)
            {
                case MapTool.Pipette:
                    if (ValuePicked != null) ValuePicked(ActiveLayer, WorldEdits.ValueAt(_doc.World, ActiveLayer, cell));
                    break;
                case MapTool.Route:
                    if (RouteArmyId == null) { Say("Выберите армию для маршрута слева."); break; }
                    if (_routeBuffer.Count == 0 || _routeBuffer[_routeBuffer.Count - 1] != cell)
                    {
                        _routeBuffer.Add(cell);
                        RaiseRoute();
                    }
                    break;
                case MapTool.Fill:
                    if (!CanEdit()) break;
                    if (ActiveLayer == EditLayer.Objects) { Say("Заливка не применяется к объектам."); break; }
                    if (ActiveValue == null) { Say(Strings.PickValueFirst); break; }
                    Run(WorldEdits.Fill(_doc.World, ActiveLayer, cell, ActiveValue));
                    break;
                case MapTool.Rect:
                    if (!CanEdit()) break;
                    if (ActiveLayer == EditLayer.Objects) { Say("Прямоугольник не применяется к объектам: ставьте их кистью."); break; }
                    _rectStart = cell;
                    _painting = true;
                    break;
                default: // Brush, Eraser
                    if (!CanEdit()) break;
                    if (Tool == MapTool.Brush && ActiveValue == null) { Say(Strings.PickValueFirst); break; }
                    _painting = true;
                    _stroke.Clear();
                    _strokeSet.Clear();
                    AddStroke(cell);
                    _lastStrokeCell = cell;
                    break;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_doc == null) return;
            if (_panning)
            {
                _offset = new PointF(_panOffsetStart.X + e.X - _panStart.X, _panOffsetStart.Y + e.Y - _panStart.Y);
                Invalidate();
                return;
            }
            CellCoord cell = ToCell(e.Location);
            CellCoord? hover = InGrid(cell) ? (CellCoord?)cell : null;
            if (hover != _hover)
            {
                _hover = hover;
                if (HoverChanged != null) HoverChanged(hover);
                Invalidate();
            }
            if (_painting && (Tool == MapTool.Brush || Tool == MapTool.Eraser) && InGrid(cell) && cell != _lastStrokeCell)
            {
                foreach (CellCoord c in Line(_lastStrokeCell, cell)) AddStroke(c);
                _lastStrokeCell = cell;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_panning) { _panning = false; return; }
            if (!_painting || _doc == null) return;
            _painting = false;
            if (Tool == MapTool.Rect && _rectStart.HasValue)
            {
                CellCoord end = _hover ?? _rectStart.Value;
                Run(WorldEdits.Paint(_doc.World, ActiveLayer, ActiveValue, WorldEdits.Rectangle(_rectStart.Value, end), null));
                _rectStart = null;
            }
            else if (_stroke.Count > 0) CommitStroke();
            _stroke.Clear();
            _strokeSet.Clear();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != null)
            {
                _hover = null;
                if (HoverChanged != null) HoverChanged(null);
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomAround(e.Location, e.Delta > 0 ? 1.15f : 1f / 1.15f); // масштаб вокруг курсора
        }

        private void AddStroke(CellCoord c)
        {
            if (InGrid(c) && _strokeSet.Add(c)) _stroke.Add(c);
        }

        private void CommitStroke()
        {
            World w = _doc.World;
            string value = Tool == MapTool.Eraser ? null : ActiveValue;
            if (ActiveLayer != EditLayer.Objects)
            {
                Run(WorldEdits.Paint(w, ActiveLayer, value, _stroke, null));
                return;
            }
            var parts = new List<IWorldCommand>();
            string lastRefusal = null;
            if (value == null)
            {
                foreach (CellCoord c in _stroke)
                {
                    IWorldCommand rm = WorldEdits.RemoveObjectAt(w, c);
                    if (rm != null) parts.Add(rm);
                }
                if (parts.Count > 0) Run(new CompositeCommand("Убрать объекты (" + parts.Count + ")", parts));
                return;
            }
            WorldObjectType type;
            if (!WorldObjectTypes.TryParse(value, out type)) return;
            // Объекты ставятся по одному на клетку; несколько клеток мазка - один шаг истории.
            // Каждый следующий объект создаётся после выполнения предыдущего, чтобы id не совпали.
            var executed = new List<IWorldCommand>();
            foreach (CellCoord c in _stroke)
            {
                string refused;
                IWorldCommand cmd = WorldEdits.PlaceObject(w, c, type, out refused);
                if (refused != null) { lastRefusal = refused; continue; }
                if (cmd == null) continue;
                cmd.Execute(w);
                executed.Add(cmd);
            }
            for (int i = executed.Count - 1; i >= 0; i--) executed[i].Undo(w);
            if (executed.Count > 0) Run(new CompositeCommand("Поставить " + Labels.ObjectName(type) + " (" + executed.Count + ")", executed));
            if (lastRefusal != null) Say(lastRefusal);
        }

        private bool CanEdit()
        {
            string name = LayerName(ActiveLayer);
            if (_locked[ActiveLayer]) { Say(string.Format(Strings.LayerLockedMsg, name)); return false; }
            if (!_visible[ActiveLayer]) { Say(string.Format(Strings.LayerHiddenMsg, name)); return false; }
            return true;
        }

        private void Run(IWorldCommand cmd)
        {
            if (cmd == null) return;
            var cells = cmd as CellsCommand;
            if (cells != null && cells.Count == 0) return;
            string error = _doc.Execute(cmd);
            if (error != null) Say(error);
            Invalidate();
        }

        private void Say(string text) { if (Message != null) Message(text); }
        private void RaiseRoute() { if (RouteBufferChanged != null) RouteBufferChanged(); }

        public static Color ToColor(RgbColor c) { return Color.FromArgb(c.R, c.G, c.B); }

        public static string LayerName(EditLayer l)
        {
            switch (l)
            {
                case EditLayer.Landscape: return Strings.LayerLandscape;
                case EditLayer.Political: return Strings.LayerPolitical;
                case EditLayer.Wildlands: return Strings.LayerWildlands;
                default: return Strings.LayerObjects;
            }
        }

        private static IEnumerable<CellCoord> Line(CellCoord a, CellCoord b)
        {
            int x0 = a.X, y0 = a.Y, x1 = b.X, y1 = b.Y;
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                yield return new CellCoord(x0, y0);
                if (x0 == x1 && y0 == y1) yield break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        // ---------- отрисовка ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            UiPalette pal = UiTheme.C;
            g.Clear(pal.CanvasBack);
            if (_doc == null) return;
            World w = _doc.World;
            GridSpec grid = w.Grid;
            g.SmoothingMode = SmoothingMode.None;

            // Не суша - спокойная штриховка цвета подложки (не синяя: моря в мире нет).
            using (var notLand = new HatchBrush(HatchStyle.LightUpwardDiagonal, Ui.Mix(pal.NotLand, pal.Text, 0.06f), pal.NotLand))
            using (var dim = new SolidBrush(Ui.Mix(pal.MapSurface, pal.Text, 0.25f)))
            {
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var c = new CellCoord(x, y);
                        CellData data = w.FindCell(c);
                        RectangleF r = Rect(c);
                        if (data == null) { g.FillRectangle(notLand, r); continue; }
                        LandscapeType type = data.Landscape != null ? w.Legend.FindLandscape(data.Landscape) : null;
                        if (_visible[EditLayer.Landscape] && type != null)
                            using (var b = new SolidBrush(ToColor(type.Color))) g.FillRectangle(b, r);
                        else g.FillRectangle(dim, r);
                        DrawOwner(g, w, data, r);
                    }
            }
            DrawBorders(g, w);
            if (ShowGrid && _cell >= 12f)
                using (var pen = new Pen(pal.Grid, 1f))
                {
                    for (int x = 0; x <= grid.Width; x++) g.DrawLine(pen, _offset.X + x * _cell, _offset.Y, _offset.X + x * _cell, _offset.Y + grid.Height * _cell);
                    for (int y = 0; y <= grid.Height; y++) g.DrawLine(pen, _offset.X, _offset.Y + y * _cell, _offset.X + grid.Width * _cell, _offset.Y + y * _cell);
                }
            if (_visible[EditLayer.Objects])
            {
                DrawObjects(g, w);
                DrawArmies(g, w);
            }
            DrawPreview(g, w);
            DrawRouteBuffer(g);

            // Невидимая стена: рамка сетки.
            using (var wall = new Pen(Color.FromArgb(140, pal.Accent), Ui.S(1.5f)))
                g.DrawRectangle(wall, _offset.X, _offset.Y, grid.Width * _cell, grid.Height * _cell);
            foreach (CellCoord c in _highlight) Outline(g, c, pal.Warning, Ui.S(3f));
            if (_hover.HasValue) Outline(g, _hover.Value, pal.IsDark ? Color.FromArgb(220, 255, 255, 255) : Color.FromArgb(200, 29, 29, 31), Ui.S(1.5f));
            if (SelectedCell.HasValue)
            {
                Outline(g, SelectedCell.Value, Color.White, Ui.S(4f));
                Outline(g, SelectedCell.Value, pal.Accent, Ui.S(2.5f));
            }
            DrawHoverInfo(g, w, pal);
        }

        // Владелец поверх ландшафта: рамка цвета государства; дикие земли - штриховкой.
        // Без слоя ландшафта владелец заливает клетку целиком.
        private void DrawOwner(Graphics g, World w, CellData data, RectangleF r)
        {
            if (data.Owner == null) return;
            StateEntity s = w.FindState(data.Owner);
            WildlandEntity wild = s == null ? w.FindWildland(data.Owner) : null;
            bool landscape = _visible[EditLayer.Landscape];
            if (s != null && _visible[EditLayer.Political])
            {
                Color c = ToColor(s.Color);
                if (!landscape) using (var b = new SolidBrush(c)) g.FillRectangle(b, r);
                else
                {
                    // Рамка цвета государства с тёмным контуром с обеих сторон: цвет государства может совпадать
                    // с цветом ландшафта под ним (Аскелия 51CC51 на равнинах 51CC51, раздел 5.2).
                    float t = Math.Max(3f, _cell * 0.12f);
                    using (var pen = new Pen(c, t)) g.DrawRectangle(pen, r.X + t / 2, r.Y + t / 2, r.Width - t, r.Height - t);
                    using (var edge = new Pen(Color.FromArgb(200, 12, 12, 14), 1f))
                    {
                        g.DrawRectangle(edge, r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
                        g.DrawRectangle(edge, r.X + t, r.Y + t, r.Width - 2 * t, r.Height - 2 * t);
                    }
                }
            }
            else if (wild != null && _visible[EditLayer.Wildlands])
            {
                Color c = ToColor(wild.Color);
                if (!landscape)
                    using (var b = new HatchBrush(HatchStyle.WideDownwardDiagonal, Color.FromArgb(255, ControlPaint.Light(c)), c)) g.FillRectangle(b, r);
                else
                {
                    using (var b = new HatchBrush(HatchStyle.WideDownwardDiagonal, Color.FromArgb(150, c), Color.Transparent)) g.FillRectangle(b, r);
                    float t = Math.Max(2f, _cell * 0.08f);
                    using (var pen = new Pen(c, t) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pen, r.X + t / 2, r.Y + t / 2, r.Width - t, r.Height - t);
                }
            }
        }

        // Граница между разными владельцами - тёмная линия.
        private void DrawBorders(Graphics g, World w)
        {
            if (!_visible[EditLayer.Political] && !_visible[EditLayer.Wildlands]) return;
            using (var pen = new Pen(Color.FromArgb(220, 10, 10, 12), Math.Max(1.5f, _cell * 0.05f)))
                foreach (CellData d in w.Cells)
                {
                    RectangleF r = Rect(d.Cell);
                    CellData right = w.FindCell(new CellCoord(d.Cell.X + 1, d.Cell.Y));
                    CellData down = w.FindCell(new CellCoord(d.Cell.X, d.Cell.Y + 1));
                    if (right != null && right.Owner != d.Owner) g.DrawLine(pen, r.Right, r.Top, r.Right, r.Bottom);
                    if (down != null && down.Owner != d.Owner) g.DrawLine(pen, r.Left, r.Bottom, r.Right, r.Bottom);
                }
        }

        private void DrawObjects(Graphics g, World w)
        {
            if (_cell < 14f) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var font = new Font(Ui.Family, Math.Max(7f, _cell * 0.3f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var align = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var plate = new SolidBrush(Color.FromArgb(205, 24, 24, 28)))
            using (var ink = new SolidBrush(Color.White))
            using (var markPen = new Pen(Color.FromArgb(230, 240, 240, 240), 1.5f) { DashStyle = DashStyle.Dot })
            using (var armyBrush = new SolidBrush(Color.FromArgb(220, 190, 30, 30)))
            {
                foreach (WorldObject o in w.Objects)
                {
                    RectangleF r = Rect(o.Cell);
                    RectangleF inner = RectangleF.Inflate(r, -r.Width * 0.2f, -r.Height * 0.2f);
                    string symbol = Labels.ObjectSymbol(o.Type);
                    if (o.Type == WorldObjectType.VillageMark)
                    {
                        g.DrawEllipse(markPen, inner);
                        g.DrawString(symbol, font, ink, r, align);
                    }
                    else if (o.Type == WorldObjectType.ArmyMark)
                    {
                        g.FillRectangle(armyBrush, inner);
                        g.DrawString(symbol, font, ink, r, align);
                    }
                    else
                    {
                        g.FillEllipse(plate, inner);
                        g.DrawString(symbol, font, ink, r, align);
                    }
                }
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        private void DrawArmies(Graphics g, World w)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (ArmyEntity a in w.Armies)
            {
                if (a.Id == RouteArmyId && Tool == MapTool.Route) continue; // показывается буфер правки
                Color color = a.Status == ArmyStatus.Roaming ? Color.FromArgb(230, 255, 110, 60) : Color.FromArgb(200, 150, 150, 150);
                DrawPolyline(g, a.Route, color, a.Status == ArmyStatus.Destroyed);
                if (a.Origin.HasValue)
                    using (var b = new SolidBrush(color)) { PointF p = Center(a.Origin.Value); g.FillEllipse(b, p.X - 4, p.Y - 4, 8, 8); }
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        private void DrawPolyline(Graphics g, IList<CellCoord> route, Color color, bool dashed)
        {
            if (route.Count < 2) return;
            var pts = new PointF[route.Count];
            for (int i = 0; i < route.Count; i++) pts[i] = Center(route[i]);
            using (var pen = new Pen(color, Math.Max(2f, _cell * 0.08f)))
            {
                if (dashed) pen.DashStyle = DashStyle.Dash;
                pen.CustomEndCap = new AdjustableArrowCap(4, 4);
                g.DrawLines(pen, pts);
            }
        }

        private void DrawRouteBuffer(Graphics g)
        {
            if (Tool != MapTool.Route || RouteArmyId == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawPolyline(g, _routeBuffer, UiTheme.C.Accent, false);
            using (var b = new SolidBrush(UiTheme.C.Accent))
                foreach (CellCoord c in _routeBuffer) { PointF p = Center(c); g.FillEllipse(b, p.X - 3, p.Y - 3, 6, 6); }
            g.SmoothingMode = SmoothingMode.None;
        }

        private void DrawPreview(Graphics g, World w)
        {
            if (!_painting) return;
            IEnumerable<CellCoord> cells = _stroke;
            if (Tool == MapTool.Rect && _rectStart.HasValue) cells = WorldEdits.Rectangle(_rectStart.Value, _hover ?? _rectStart.Value);
            Color c = Tool == MapTool.Eraser ? Color.FromArgb(140, 220, 60, 60) : Color.FromArgb(140, PreviewColor(w));
            using (var b = new SolidBrush(c))
                foreach (CellCoord cell in cells) g.FillRectangle(b, Rect(cell));
        }

        private Color PreviewColor(World w)
        {
            if (ActiveValue == null) return Color.White;
            if (ActiveLayer == EditLayer.Landscape)
            {
                LandscapeType t = w.Legend.FindLandscape(ActiveValue);
                return t != null ? ToColor(t.Color) : Color.White;
            }
            RgbColor? owner = w.DisplayColorOf(ActiveValue);
            return owner.HasValue ? ToColor(owner.Value) : Color.White;
        }

        // Подсказка о клетке под курсором в левом нижнем углу карты.
        private void DrawHoverInfo(Graphics g, World w, UiPalette pal)
        {
            if (!_hover.HasValue) return;
            CellCoord c = _hover.Value;
            CellData d = w.FindCell(c);
            string text = c.X + ", " + c.Y + "    " + (d == null ? "не суша" : Labels.Landscape(w, d.Landscape) + "  /  " + Labels.Entity(w, d.Owner));
            WorldObject o = w.ObjectAt(c);
            if (o != null) text += "  /  " + Labels.ObjectName(o.Type);
            Size size = TextRenderer.MeasureText(text, Ui.Small);
            var r = new RectangleF(Ui.S(12f), Height - Ui.S(12f) - size.Height - Ui.S(10f), size.Width + Ui.S(20f), size.Height + Ui.S(10f));
            Ui.HighQuality(g);
            Ui.FillRound(g, pal.ToastBack, r, r.Height / 2f);
            Ui.DrawText(g, text, Ui.Small, pal.ToastText, Rectangle.Round(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            g.SmoothingMode = SmoothingMode.None;
        }

        private void Outline(Graphics g, CellCoord c, Color color, float width)
        {
            using (var pen = new Pen(color, width))
            {
                RectangleF r = Rect(c);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }
        }
    }
}
