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
    public partial class CanvasControl
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            float cx = Width * 0.5f;
            float cy = Height * 0.5f;

            float mapWorldW = MapWidth * CellSize;
            float mapWorldH = MapHeight * CellSize;
            float mapScreenX = PanOffset.X * Zoom + cx;
            float mapScreenY = PanOffset.Y * Zoom + cy;
            float mapScreenW = mapWorldW * Zoom;
            float mapScreenH = mapWorldH * Zoom;

            // 1. Draw 1000x1000 Map Surface Background
            RectangleF mapRect = new RectangleF(mapScreenX, mapScreenY, mapScreenW, mapScreenH);
            using (Brush mapBg = new SolidBrush(Kit.UiTheme.C.MapSurface))
                g.FillRectangle(mapBg, mapRect);
            if (MapKind == "global")
            {
                using (Brush land = new SolidBrush(Kit.UiTheme.C.NotLand))
                {
                    for (int y = 0; y < MapHeight; y++)
                        for (int x = 0; x < MapWidth; x++)
                            if (IsInMap(new Point(x, y))) g.FillRectangle(land, CellToScreenRect(new Point(x, y)));
                }
            }

            // 2. Visible grid lines
            Point tl = ScreenToCell(new Point(0, 0));
            Point br = ScreenToCell(new Point(Width, Height));

            int startX = Math.Max(0, tl.X - 1);
            int endX = Math.Min(MapWidth, br.X + 1);
            int startY = Math.Max(0, tl.Y - 1);
            int endY = Math.Min(MapHeight, br.Y + 1);

            if (ShowGrid && Zoom >= 0.18f)
            {
                using (Pen gridPen = new Pen(Kit.UiTheme.C.Grid, 1f))
                using (Pen majorPen = new Pen(Kit.UiTheme.C.GridMajor, 1.2f))
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        float sx = (x * CellSize + PanOffset.X) * Zoom + cx;
                        float yTop = Math.Max(0, mapScreenY);
                        float yBot = Math.Min(Height, mapScreenY + mapScreenH);
                        Pen p = (x % 10 == 0) ? majorPen : gridPen;
                        g.DrawLine(p, sx, yTop, sx, yBot);
                    }
                    for (int y = startY; y <= endY; y++)
                    {
                        float sy = (y * CellSize + PanOffset.Y) * Zoom + cy;
                        float xLeft = Math.Max(0, mapScreenX);
                        float xRight = Math.Min(Width, mapScreenX + mapScreenW);
                        Pen p = (y % 10 == 0) ? majorPen : gridPen;
                        g.DrawLine(p, xLeft, sy, xRight, sy);
                    }
                }
            }

            // 3. Independent layers: landscape below politics.
            if (ShowLandscapeLayer) DrawLayer(g, _landscapeLayer.Cells, startX, endX, startY, endY, false);
            if (ShowPoliticalLayer) DrawLayer(g, _politicalLayer.Cells, startX, endX, startY, endY, true);
            DrawObjectLabels(g, _objectLayer.Cells, startX, endX, startY, endY);

            // 4. Map boundary
            using (Pen borderPen = new Pen(Color.FromArgb(220, Kit.UiTheme.C.Accent), 2f))
            {
                g.DrawRectangle(borderPen, mapRect.X, mapRect.Y, mapRect.Width, mapRect.Height);
            }

            // 5. EXACT CENTER MARKER (500, 500)
            float centerWorldX = (MapWidth * 0.5f) * CellSize;
            float centerWorldY = (MapHeight * 0.5f) * CellSize;
            float centerScrX = (centerWorldX + PanOffset.X) * Zoom + cx;
            float centerScrY = (centerWorldY + PanOffset.Y) * Zoom + cy;

            if (MapKind == "local")
            {
                using (Pen centerPen = new Pen(Color.FromArgb(220, 230, 235, 240), 2.0f))
                using (Brush centerBrush = new SolidBrush(Color.FromArgb(180, 230, 235, 240)))
                {
                    g.DrawLine(centerPen, centerScrX - 20, centerScrY, centerScrX + 20, centerScrY);
                    g.DrawLine(centerPen, centerScrX, centerScrY - 20, centerScrX, centerScrY + 20);
                    g.DrawEllipse(centerPen, centerScrX - 10, centerScrY - 10, 20, 20);
                    g.FillEllipse(centerBrush, centerScrX - 3, centerScrY - 3, 6, 6);
                }
            }

            // 6. Selection Box (Scissors Cut/Copy Rect)
            if (HasSelection && SelectionBounds.Width > 0 && SelectionBounds.Height > 0)
            {
                Point selTL = new Point(SelectionBounds.Left, SelectionBounds.Top);
                Point selBR = new Point(SelectionBounds.Right, SelectionBounds.Bottom);
                RectangleF rTL = CellToScreenRect(selTL);
                RectangleF rBR = CellToScreenRect(selBR);

                float sx = rTL.X;
                float sy = rTL.Y;
                float sw = rBR.X - rTL.X;
                float sh = rBR.Y - rTL.Y;

                using (Brush selBrush = new SolidBrush(Color.FromArgb(55, 100, 180, 255)))
                {
                    g.FillRectangle(selBrush, sx, sy, sw, sh);
                }
                using (Pen selPen = new Pen(Color.FromArgb(255, 120, 200, 255), 2.0f))
                {
                    selPen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(selPen, sx, sy, sw, sh);
                }
            }

            // 7. Paste Ghost Preview
            if (CurrentTool == ToolMode.Paste && ClipboardBuffer.Count > 0 && _isHoverValid)
            {
                foreach (var kvp in ClipboardBuffer)
                {
                    Point ghostCell = new Point(_currentHoverCell.X + kvp.Key.X, _currentHoverCell.Y + kvp.Key.Y);
                    if (IsInMap(ghostCell) && BlockDefs.ContainsKey(kvp.Value))
                    {
                        var def = BlockDefs[kvp.Value];
                        RectangleF rect = CellToScreenRect(ghostCell);
                        if (IsObject(kvp.Value)) DrawPreviewSymbol(g, kvp.Value, ghostCell);
                        else
                        {
                            using (Brush b = new SolidBrush(Color.FromArgb(170, def.Color)))
                                g.FillRectangle(b, rect);
                            using (Pen p = new Pen(Color.FromArgb(220, def.BorderColor), 1f))
                                g.DrawRectangle(p, rect.X, rect.Y, rect.Width, rect.Height);
                        }
                    }
                }
            }
            else if (_isPainting && (CurrentTool == ToolMode.Line || CurrentTool == ToolMode.Rect))
            {
                var preview = GetToolPreviewCells();
                Color pcol = _isErasing ? Color.FromArgb(180, 220, 50, 50) : Color.FromArgb(160, BlockDefs[ActiveBlockId].Color);
                using (Brush b = new SolidBrush(pcol))
                {
                    foreach (var p in preview)
                    {
                        if (IsInMap(p))
                        {
                            RectangleF rect = CellToScreenRect(p);
                            if (IsObject(ActiveBlockId) && !_isErasing) DrawPreviewSymbol(g, ActiveBlockId, p);
                            else g.FillRectangle(b, rect);
                        }
                    }
                }
            }
            else if (!_isPanning && !_isPainting && _hasHover && CurrentTool == ToolMode.Brush && IsBuildingActive)
            {
                DrawBuildingGhost(g);
            }
            else if (!_isPanning && _isHoverValid && CurrentTool != ToolMode.Select && !IsObject(ActiveBlockId))
            {
                RectangleF hrect = CellToScreenRect(_currentHoverCell);
                Color hcol = (CurrentTool == ToolMode.Eraser) ? Color.FromArgb(100, 220, 60, 60) : Color.FromArgb(35, 235, 240, 245);
                using (Brush b = new SolidBrush(hcol))
                    g.FillRectangle(b, hrect);
                using (Pen p = new Pen(Color.FromArgb(210, 235, 240, 245), 1.5f))
                    g.DrawRectangle(p, hrect.X, hrect.Y, hrect.Width, hrect.Height);
            }
            if (!_isPanning && _hasHover && CurrentTool == ToolMode.Eraser) DrawEraseTarget(g);

            // Origin marker
            float ox = PanOffset.X * Zoom + cx;
            float oy = PanOffset.Y * Zoom + cy;
            using (Brush b = new SolidBrush(Color.FromArgb(220, 255, 140, 50)))
                g.FillEllipse(b, ox - 4, oy - 4, 8, 8);
        }

        private void DrawLayer(Graphics g, Dictionary<Point, string> layer, int startX, int endX, int startY, int endY, bool politics)
        {
            foreach (var kvp in layer)
            {
                Point cell = kvp.Key;
                if (cell.X < startX || cell.X > endX || cell.Y < startY || cell.Y > endY) continue;
                BlockDef def;
                if (!BlockDefs.TryGetValue(kvp.Value, out def)) continue;
                RectangleF rect = CellToScreenRect(cell);
                if (!string.IsNullOrEmpty(def.Symbol)) continue;
                bool overlay = politics && ShowLandscapeLayer && _landscapeLayer.Cells.ContainsKey(cell);
                if (!overlay)
                {
                    using (Brush brush = new SolidBrush(def.Color))
                        g.FillRectangle(brush, rect);
                }
                using (Pen pen = new Pen(overlay ? def.Color : def.BorderColor, overlay ? 2f : 1f))
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                if (overlay && string.IsNullOrEmpty(def.Symbol))
                {
                    using (Brush swatch = new SolidBrush(def.Color))
                        g.FillRectangle(swatch, rect.X + 3f, rect.Y + 3f, Math.Min(6f, rect.Width * 0.2f), Math.Min(6f, rect.Height * 0.2f));
                }
            }
        }

        private void DrawObjectLabels(Graphics g, Dictionary<Point, string> layer, int startX, int endX, int startY, int endY)
        {
            foreach (var kvp in layer)
            {
                Point cell = kvp.Key;
                BlockDef def;
                if (!BlockDefs.TryGetValue(kvp.Value, out def) || string.IsNullOrEmpty(def.Symbol)) continue;
                BuildingDef building = def as BuildingDef;
                if (building != null)
                {
                    // Cull by the whole building rectangle, not by its anchor cell.
                    if (cell.X > endX || cell.X + building.OuterW - 1 < startX ||
                        cell.Y > endY || cell.Y + building.OuterH - 1 < startY) continue;
                    DrawBuilding(g, cell, building, 255, false);
                    continue;
                }
                if (cell.X < startX || cell.X > endX || cell.Y < startY || cell.Y > endY) continue;
                RectangleF rect = CellToScreenRect(cell);
                if (rect.Width < 14f) continue;
                using (Font font = new Font("Segoe UI", Math.Max(8f, Math.Min(13f, rect.Width * 0.42f)), FontStyle.Bold))
                using (Brush ink = new SolidBrush(Color.White))
                using (StringFormat align = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(def.Symbol, font, ink, rect, align);
            }
        }

        private void DrawPreviewSymbol(Graphics g, string id, Point cell)
        {
            RectangleF rect = CellToScreenRect(cell);
            if (rect.Width < 14f) return;
            using (Font font = new Font("Segoe UI", Math.Max(8f, Math.Min(13f, rect.Width * 0.42f)), FontStyle.Bold))
            using (Brush ink = new SolidBrush(Color.FromArgb(180, Color.White)))
            using (StringFormat align = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(BlockDefs[id].Symbol, font, ink, rect, align);
        }
    }
}
