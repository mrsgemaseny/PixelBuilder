using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PixelBuilder
{
    public partial class CanvasControl
    {
        // Below this zoom a building is drawn as a filled rectangle with a ridge line.
        private const float SimpleBuildingZoom = 0.18f;

        // Colors taken from the game (settlement_roofs.gd, structures.json); see GAME_BUILDING_REFERENCE.md.
        // One fixed timber roof color (TIMBER_ROOFS[1] in the game); no per-house randomness.
        private static readonly Color RoofBase = Color.FromArgb(0x8b, 0x6b, 0x46);
        private static readonly Color GameWoodWall = Color.FromArgb(0x8c, 0x5a, 0x2b);
        private static readonly Color GameFloor = Color.FromArgb(0x8c, 0x62, 0x39);
        private static readonly Color GameDoor = Color.FromArgb(0x9c, 0x66, 0x44);

        private static Color Shade(Color c, float amount)
        {
            // amount > 0 lightens toward white, < 0 darkens toward black (same idea as Godot lightened/darkened).
            float k = Math.Abs(amount);
            int target = amount >= 0 ? 255 : 0;
            return Color.FromArgb(c.A,
                (int)(c.R + (target - c.R) * k), (int)(c.G + (target - c.G) * k), (int)(c.B + (target - c.B) * k));
        }

        public bool ShowRoofs = true;
        public string LastImportError { get; private set; }

        // cell -> anchor; derived from the objects layer (single source of truth), rebuilt lazily.
        private readonly Dictionary<Point, Point> _occupancy = new Dictionary<Point, Point>();
        private bool _occupancyDirty = true;
        private bool _hasHover = false;

        public const int MinBuildingInterior = 1;
        public const int MaxBuildingInterior = 40;
        private static readonly Regex BuildingIdPattern = new Regex(@"^building_house_(\d{1,2})x(\d{1,2})_([hv])$", RegexOptions.Compiled);

        // Any size is allowed: the id carries it (building_house_<interiorW>x<interiorH>_<h|v>).
        // Definitions are created on first use, so files with arbitrary sizes import without a fixed catalog.
        public static string BuildingId(int interiorW, int interiorH, char ridge)
        {
            return string.Format("building_house_{0}x{1}_{2}", interiorW, interiorH, ridge);
        }

        public static BuildingDef GetBuildingDef(string id)
        {
            if (id == null) return null;
            BlockDef known;
            if (BlockDefs.TryGetValue(id, out known)) return known as BuildingDef;
            Match m = BuildingIdPattern.Match(id);
            if (!m.Success) return null;
            int w = int.Parse(m.Groups[1].Value), h = int.Parse(m.Groups[2].Value);
            if (w < MinBuildingInterior || h < MinBuildingInterior || w > MaxBuildingInterior || h > MaxBuildingInterior) return null;
            char ridge = m.Groups[3].Value[0];
            Color color = Color.FromArgb(160, 112, 72);
            var def = new BuildingDef
            {
                Id = id, Hotkey = "", Symbol = "Д", Color = color, BorderColor = ControlPaint.Dark(color),
                Name = string.Format("Дом {0}x{1}, конёк {2}", w, h, ridge == 'h' ? "гориз." : "верт."),
                InteriorW = w, InteriorH = h, Ridge = ridge,
                // Rotation swaps the dimensions and flips the ridge.
                RotatedId = BuildingId(h, w, ridge == 'h' ? 'v' : 'h')
            };
            BlockDefs[id] = def;
            return def;
        }

        public bool IsBuildingActive { get { return GetBuildingDef(ActiveBlockId) != null; } }

        private void MarkOccupancyDirty() { _occupancyDirty = true; }

        private void EnsureOccupancy()
        {
            if (!_occupancyDirty) return;
            _occupancy.Clear();
            foreach (var kvp in _objectLayer.Cells)
            {
                BuildingDef def = GetBuildingDef(kvp.Value);
                int w = def != null ? def.OuterW : 1;
                int h = def != null ? def.OuterH : 1;
                for (int dx = 0; dx < w; dx++)
                {
                    for (int dy = 0; dy < h; dy++)
                    {
                        Point cell = new Point(kvp.Key.X + dx, kvp.Key.Y + dy);
                        if (!_occupancy.ContainsKey(cell)) _occupancy[cell] = kvp.Key;
                    }
                }
            }
            _occupancyDirty = false;
        }

        // Any object (building or legacy 1x1 marker) covering the cell: returns its anchor.
        public bool TryGetObjectAnchor(Point cell, out Point anchor)
        {
            EnsureOccupancy();
            return _occupancy.TryGetValue(cell, out anchor);
        }

        public bool CanPlaceBuilding(Point anchor, BuildingDef def, out string reason)
        {
            reason = null;
            EnsureOccupancy();
            for (int dx = 0; dx < def.OuterW; dx++)
            {
                for (int dy = 0; dy < def.OuterH; dy++)
                {
                    Point cell = new Point(anchor.X + dx, anchor.Y + dy);
                    if (!IsInMap(cell))
                    {
                        reason = string.Format("клетка ({0},{1}) вне карты", cell.X, cell.Y);
                        return false;
                    }
                    Point other;
                    if (_occupancy.TryGetValue(cell, out other))
                    {
                        reason = string.Format("пересечение с объектом в ({0},{1})", other.X, other.Y);
                        return false;
                    }
                }
            }
            return true;
        }

        public bool TryPlaceBuilding(Point anchor, string id, out string reason)
        {
            reason = null;
            BuildingDef def = GetBuildingDef(id);
            if (def == null) { reason = "неизвестное здание"; return false; }
            if (MapKind != "local") { reason = "здания ставятся только на локальной карте"; return false; }
            if (!CanPlaceBuilding(anchor, def, out reason)) return false;
            _editLayer = _objectLayer;
            _currentStrokeDiff.Clear();
            SetCellInternal(anchor, id);
            CommitStroke();
            Invalidate();
            return true;
        }

        // Removes the whole building (or legacy marker) covering the cell, as one undoable stroke.
        public bool RemoveObjectAt(Point cell)
        {
            Point anchor;
            if (!TryGetObjectAnchor(cell, out anchor)) return false;
            _editLayer = _objectLayer;
            _currentStrokeDiff.Clear();
            SetCellInternal(anchor, "");
            CommitStroke();
            Invalidate();
            return true;
        }

        public bool RotateActiveBuilding()
        {
            BuildingDef def = GetBuildingDef(ActiveBlockId);
            if (def == null || string.IsNullOrEmpty(def.RotatedId)) return false;
            ActiveBlockId = def.RotatedId;
            RefreshEditingLayer();
            Invalidate();
            return true;
        }

        public void ToggleRoofs()
        {
            ShowRoofs = !ShowRoofs;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hasHover = false;
            Invalidate();
        }

        // Returns null when valid, otherwise the first problem found (used by import).
        private string ValidateBuildings(Dictionary<Point, string> objects)
        {
            var claimed = new Dictionary<Point, Point>();
            foreach (var kvp in objects)
            {
                BuildingDef def = GetBuildingDef(kvp.Value);
                int w = def != null ? def.OuterW : 1;
                int h = def != null ? def.OuterH : 1;
                for (int dx = 0; dx < w; dx++)
                {
                    for (int dy = 0; dy < h; dy++)
                    {
                        Point cell = new Point(kvp.Key.X + dx, kvp.Key.Y + dy);
                        if (!IsInMap(cell))
                            return string.Format("Объект {0} с якорем ({1},{2}): клетка ({3},{4}) вне карты.",
                                kvp.Value, kvp.Key.X, kvp.Key.Y, cell.X, cell.Y);
                        Point other;
                        if (claimed.TryGetValue(cell, out other))
                            return string.Format("Объект {0} с якорем ({1},{2}) пересекается с объектом в ({3},{4}), клетка ({5},{6}).",
                                kvp.Value, kvp.Key.X, kvp.Key.Y, other.X, other.Y, cell.X, cell.Y);
                        claimed[cell] = kvp.Key;
                    }
                }
            }
            return null;
        }

        private static Color WithAlpha(Color color, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha * color.A / 255)), color);
        }

        private void DrawBuilding(Graphics g, Point anchor, BuildingDef def, int alpha, bool forceRoof)
        {
            RectangleF first = CellToScreenRect(anchor);
            float cs = first.Width;
            RectangleF outer = new RectangleF(first.X, first.Y, def.OuterW * cs, def.OuterH * cs);
            bool roof = forceRoof || ShowRoofs;
            BlockDef wall = BlockDefs[def.WallBlockId];
            Color roofColor = RoofBase;
            Color roofLight = Shade(roofColor, 0.12f);
            Color wallColor = def.WallBlockId == "wood_wall" ? GameWoodWall : wall.Color;
            Color floorColor = GameFloor;
            Color doorColor = GameDoor;

            if (Zoom < SimpleBuildingZoom)
            {
                using (Brush fill = new SolidBrush(WithAlpha(roof ? roofLight : wallColor, alpha)))
                    g.FillRectangle(fill, outer);
                DrawRidge(g, outer, def, roofColor, alpha, 1f);
                return;
            }

            // Walls, floor, door.
            using (Brush wallBrush = new SolidBrush(WithAlpha(wallColor, alpha)))
            using (Brush floorBrush = new SolidBrush(WithAlpha(floorColor, alpha)))
            using (Brush doorBrush = new SolidBrush(WithAlpha(doorColor, alpha)))
            using (Pen wallPen = new Pen(WithAlpha(Shade(wallColor, -0.33f), alpha), 1f))
            using (Pen floorPen = new Pen(WithAlpha(Shade(floorColor, -0.25f), alpha), 1f))
            {
                for (int dx = 0; dx < def.OuterW; dx++)
                {
                    for (int dy = 0; dy < def.OuterH; dy++)
                    {
                        RectangleF rect = new RectangleF(outer.X + dx * cs, outer.Y + dy * cs, cs, cs);
                        bool border = dx == 0 || dy == 0 || dx == def.OuterW - 1 || dy == def.OuterH - 1;
                        bool isDoor = dx == def.DoorX && dy == def.DoorY;
                        if (border && !isDoor)
                        {
                            g.FillRectangle(wallBrush, rect);
                            g.DrawRectangle(wallPen, rect.X, rect.Y, rect.Width, rect.Height);
                        }
                        else
                        {
                            g.FillRectangle(floorBrush, rect);
                            g.DrawRectangle(floorPen, rect.X, rect.Y, rect.Width, rect.Height);
                            if (isDoor)
                                g.FillRectangle(doorBrush, rect.X + cs * 0.15f, rect.Y + cs * 0.15f, cs * 0.7f, cs * 0.7f);
                        }
                    }
                }
            }

            if (roof)
            {
                RectangleF slopeA, slopeB;
                if (def.Ridge == 'v')
                {
                    float half = outer.Width / 2f;
                    slopeA = new RectangleF(outer.X, outer.Y, half, outer.Height);
                    slopeB = new RectangleF(outer.X + half, outer.Y, half, outer.Height);
                }
                else
                {
                    float half = outer.Height / 2f;
                    slopeA = new RectangleF(outer.X, outer.Y, outer.Width, half);
                    slopeB = new RectangleF(outer.X, outer.Y + half, outer.Width, half);
                }
                // Drop shadow like in the game: shifted copy of the roof, 25% opaque.
                using (Brush shadow = new SolidBrush(WithAlpha(Color.FromArgb(64, 26, 20, 13), alpha)))
                    g.FillRectangle(shadow, outer.X + cs * 0.25f, outer.Y + cs * 0.35f, outer.Width, outer.Height);
                using (Brush lit = new SolidBrush(WithAlpha(roofColor, alpha)))
                using (Brush shade = new SolidBrush(WithAlpha(Color.FromArgb(roofColor.A,
                    roofColor.R * 0xbd / 255, roofColor.G * 0xb5 / 255, roofColor.B * 0xaa / 255), alpha)))
                {
                    g.FillRectangle(lit, slopeA);
                    g.FillRectangle(shade, slopeB);
                }
                if (cs >= 10f)
                {
                    // Shingle courses: lines across the slopes, one per cell.
                    using (Pen course = new Pen(WithAlpha(Shade(roofColor, -0.55f), alpha / 3), 1f))
                    {
                        if (def.Ridge == 'v')
                            for (int dy = 1; dy < def.OuterH; dy++)
                                g.DrawLine(course, outer.X, outer.Y + dy * cs, outer.Right, outer.Y + dy * cs);
                        else
                            for (int dx = 1; dx < def.OuterW; dx++)
                                g.DrawLine(course, outer.X + dx * cs, outer.Y, outer.X + dx * cs, outer.Bottom);
                    }
                }
                DrawRidge(g, outer, def, roofColor, alpha, Math.Max(1.5f, cs * 0.12f));
                using (Pen edge = new Pen(WithAlpha(Shade(roofColor, -0.55f), alpha), 1.5f))
                    g.DrawRectangle(edge, outer.X, outer.Y, outer.Width, outer.Height);

                // Keep the entrance readable under the roof.
                using (Brush doorBrush = new SolidBrush(WithAlpha(doorColor, alpha)))
                    g.FillRectangle(doorBrush, outer.X + def.DoorX * cs + cs * 0.2f,
                        outer.Y + def.DoorY * cs + cs * 0.45f, cs * 0.6f, cs * 0.55f);
            }
            else
            {
                using (Pen edge = new Pen(WithAlpha(Shade(wallColor, -0.33f), alpha), 1.5f))
                    g.DrawRectangle(edge, outer.X, outer.Y, outer.Width, outer.Height);
                DrawWindows(g, outer, def, alpha);
            }
        }

        private void DrawRidge(Graphics g, RectangleF outer, BuildingDef def, Color roofColor, int alpha, float width)
        {
            // Dark ridge line with a light highlight shifted by (-2,-2), as in the game.
            using (Pen ridge = new Pen(WithAlpha(Shade(roofColor, -0.38f), alpha), width))
            using (Pen glint = new Pen(WithAlpha(Shade(roofColor, 0.30f), alpha), Math.Max(1f, width * 0.43f)))
            {
                float sh = Math.Min(2f, width * 0.3f);
                if (def.Ridge == 'v')
                {
                    float x = outer.X + outer.Width / 2f;
                    g.DrawLine(ridge, x, outer.Y, x, outer.Bottom);
                    g.DrawLine(glint, x - sh, outer.Y, x - sh, outer.Bottom);
                }
                else
                {
                    float y = outer.Y + outer.Height / 2f;
                    g.DrawLine(ridge, outer.X, y, outer.Right, y);
                    g.DrawLine(glint, outer.X, y - sh, outer.Right, y - sh);
                }
            }
        }

        // Windows on the south wall (like the facade windows in the game), only without a roof.
        private void DrawWindows(Graphics g, RectangleF outer, BuildingDef def, int alpha)
        {
            float cs = outer.Width / def.OuterW;
            if (cs < 10f) return;
            using (Brush glass = new SolidBrush(WithAlpha(Color.FromArgb(0x38, 0x3c, 0x3a), alpha)))
            using (Brush frame = new SolidBrush(WithAlpha(Shade(GameWoodWall, -0.54f), alpha)))
            {
                for (int dx = 1; dx < def.OuterW - 1; dx++)
                {
                    if (dx == def.DoorX) continue;
                    RectangleF win = new RectangleF(outer.X + dx * cs + cs * 0.25f, outer.Y + (def.OuterH - 1) * cs + cs * 0.28f, cs * 0.5f, cs * 0.44f);
                    g.FillRectangle(frame, win.X - 1, win.Y - 1, win.Width + 2, win.Height + 2);
                    g.FillRectangle(glass, win);
                }
            }
        }

        private void DrawBuildingGhost(Graphics g)
        {
            BuildingDef def = GetBuildingDef(ActiveBlockId);
            if (def == null) return;
            string reason;
            bool ok = CanPlaceBuilding(_currentHoverCell, def, out reason);
            DrawBuilding(g, _currentHoverCell, def, 150, true);
            RectangleF first = CellToScreenRect(_currentHoverCell);
            RectangleF outer = new RectangleF(first.X, first.Y, def.OuterW * first.Width, def.OuterH * first.Height);
            Color tone = ok ? Color.FromArgb(60, 210, 100) : Color.FromArgb(230, 60, 60);
            using (Brush tint = new SolidBrush(Color.FromArgb(70, tone)))
                g.FillRectangle(tint, outer);
            using (Pen pen = new Pen(Color.FromArgb(230, tone), 1f))
                g.DrawRectangle(pen, outer.X, outer.Y, outer.Width, outer.Height);
            if (!ok && outer.Width >= 40f)
            {
                using (Font font = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (Brush ink = new SolidBrush(Color.FromArgb(240, 230, 90, 90)))
                    g.DrawString(reason, font, ink, outer.X, outer.Bottom + 3f);
            }
        }

        // Eraser hint: outline the whole building under the cursor.
        private void DrawEraseTarget(Graphics g)
        {
            Point anchor;
            if (!TryGetObjectAnchor(_currentHoverCell, out anchor)) return;
            string id;
            if (!_objectLayer.Cells.TryGetValue(anchor, out id)) return;
            BuildingDef def = GetBuildingDef(id);
            int w = def != null ? def.OuterW : 1;
            int h = def != null ? def.OuterH : 1;
            RectangleF first = CellToScreenRect(anchor);
            using (Pen pen = new Pen(Color.FromArgb(230, 230, 60, 60), 1f))
                g.DrawRectangle(pen, first.X, first.Y, w * first.Width, h * first.Height);
        }
    }
}
