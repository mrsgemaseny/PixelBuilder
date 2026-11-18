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
        private void PaintCell(Point cell, bool clicked = false)
        {
            if (!IsInMap(cell)) return;
            string target = _isErasing ? "" : ActiveBlockId;
            if (IsObject(target)) _editLayer = _objectLayer;
            // Buildings are placed only by a single click (TryPlaceBuilding), never by dragging.
            if (GetBuildingDef(target) != null) return;
            if (IsObject(target))
            {
                // A 1x1 marker must not be dropped onto a multi-cell building.
                Point owner;
                if (TryGetObjectAnchor(cell, out owner) && GetBuildingDef(_objectLayer.Cells[owner]) != null) return;
            }
            if (clicked && target == "settlement_village" && EditCells.ContainsKey(cell))
            {
                string previous = EditCells[cell];
                if (previous == "settlement_village") target = "settlement_village_2";
                else if (previous == "settlement_village_2") target = "settlement_village_3";
                else if (previous == "settlement_village_3") target = previous;
            }
            SetCellInternal(cell, target);
        }

        private void PaintLineTo(Point target)
        {
            if (_lastPaintedCell == target) return;
            var pts = BresenhamLine(_lastPaintedCell, target);
            foreach (var p in pts)
            {
                if (IsInMap(p)) PaintCell(p);
            }
            _lastPaintedCell = target;
            Invalidate();
        }

        private void SetCellInternal(Point cell, string newBid)
        {
            if (!IsInMap(cell)) return;
            if (string.IsNullOrEmpty(newBid) && _editLayer == _objectLayer)
            {
                // Erasing any cell of a building removes the whole building (stored at its anchor).
                Point owner;
                if (TryGetObjectAnchor(cell, out owner)) cell = owner;
            }
            string oldBid = EditCells.ContainsKey(cell) ? EditCells[cell] : "";
            if (oldBid == newBid) return;

            if (!_currentStrokeDiff.ContainsKey(cell))
            {
                _currentStrokeDiff[cell] = new KeyValuePair<string, string>(oldBid, newBid);
            }
            else
            {
                _currentStrokeDiff[cell] = new KeyValuePair<string, string>(_currentStrokeDiff[cell].Key, newBid);
            }

            if (string.IsNullOrEmpty(newBid))
            {
                EditCells.Remove(cell);
            }
            else
            {
                EditCells[cell] = newBid;
            }
            MarkOccupancyDirty();
            NotifyStats();
        }

        private void CommitStroke()
        {
            if (_currentStrokeDiff.Count == 0) return;
            _undoStack.Push(new Dictionary<Point, KeyValuePair<string, string>>(_currentStrokeDiff));
            _redoStack.Clear();
            if (HistoryChanged != null) HistoryChanged();
            NotifyStats();
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            var diff = _undoStack.Pop();
            _redoStack.Push(diff);
            foreach (var kvp in diff)
            {
                if (string.IsNullOrEmpty(kvp.Value.Key))
                    EditCells.Remove(kvp.Key);
                else
                    EditCells[kvp.Key] = kvp.Value.Key;
            }
            MarkOccupancyDirty();
            if (HistoryChanged != null) HistoryChanged();
            NotifyStats();
            Invalidate();
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            var diff = _redoStack.Pop();
            _undoStack.Push(diff);
            foreach (var kvp in diff)
            {
                if (string.IsNullOrEmpty(kvp.Value.Value))
                    EditCells.Remove(kvp.Key);
                else
                    EditCells[kvp.Key] = kvp.Value.Value;
            }
            MarkOccupancyDirty();
            if (HistoryChanged != null) HistoryChanged();
            NotifyStats();
            Invalidate();
        }

        public bool CanUndo { get { return _undoStack.Count > 0; } }
        public bool CanRedo { get { return _redoStack.Count > 0; } }

        public void ClearAll()
        {
            if (EditCells.Count == 0) return;
            var diff = new Dictionary<Point, KeyValuePair<string, string>>();
            foreach (var kvp in EditCells)
            {
                diff[kvp.Key] = new KeyValuePair<string, string>(kvp.Value, "");
            }
            _undoStack.Push(diff);
            _redoStack.Clear();
            EditCells.Clear();
            MarkOccupancyDirty();
            ClearSelection();
            if (HistoryChanged != null) HistoryChanged();
            NotifyStats();
            Invalidate();
        }

        private void ExecuteFill(Point start, string target)
        {
            if (!IsInMap(start)) return;
            string source = EditCells.ContainsKey(start) ? EditCells[start] : "";
            if (source == target) return;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            if (string.IsNullOrEmpty(source))
            {
                if (EditCells.Count == 0) return;

                foreach (var pt in EditCells.Keys)
                {
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.Y > maxY) maxY = pt.Y;
                }

                if (start.X < minX || start.X > maxX || start.Y < minY || start.Y > maxY)
                {
                    MessageBox.Show("Cannot fill outside! Click inside enclosed walls.", "Pixel Architect", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                minX -= 1; maxX += 1;
                minY -= 1; maxY += 1;
            }

            var queue = new Queue<Point>();
            var visited = new HashSet<Point>();
            var fillPoints = new List<Point>();
            queue.Enqueue(start);
            visited.Add(start);

            bool leaked = false;
            int maxCount = 5000;

            while (queue.Count > 0)
            {
                Point cur = queue.Dequeue();
                fillPoints.Add(cur);

                if (fillPoints.Count > maxCount)
                {
                    leaked = true;
                    break;
                }

                if (string.IsNullOrEmpty(source))
                {
                    if (cur.X <= minX || cur.X >= maxX || cur.Y <= minY || cur.Y >= maxY)
                    {
                        leaked = true;
                        break;
                    }
                }

                Point[] neighbors = {
                    new Point(cur.X + 1, cur.Y),
                    new Point(cur.X - 1, cur.Y),
                    new Point(cur.X, cur.Y + 1),
                    new Point(cur.X, cur.Y - 1)
                };

                foreach (var n in neighbors)
                {
                    if (IsInMap(n) && !visited.Contains(n))
                    {
                        visited.Add(n);
                        string nbBid = EditCells.ContainsKey(n) ? EditCells[n] : "";
                        if (nbBid == source) queue.Enqueue(n);
                    }
                }
            }

            if (!leaked)
            {
                foreach (var p in fillPoints)
                {
                    SetCellInternal(p, target);
                }
            }
            else
            {
                MessageBox.Show("Room is not enclosed (leak detected)!\nClose the wall perimeter before filling.", "Pixel Architect", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private List<Point> GetToolPreviewCells()
        {
            ToolMode effTool = CurrentTool;
            if ((ModifierKeys & Keys.Shift) != 0) effTool = ToolMode.Line;

            if (effTool == ToolMode.Line)
            {
                return BresenhamLine(_toolStartCell, _currentHoverCell);
            }
            else if (effTool == ToolMode.Rect)
            {
                int x0 = Math.Min(_toolStartCell.X, _currentHoverCell.X);
                int x1 = Math.Max(_toolStartCell.X, _currentHoverCell.X);
                int y0 = Math.Min(_toolStartCell.Y, _currentHoverCell.Y);
                int y1 = Math.Max(_toolStartCell.Y, _currentHoverCell.Y);
                var list = new List<Point>();
                for (int x = x0; x <= x1; x++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        if (x == x0 || x == x1 || y == y0 || y == y1)
                            list.Add(new Point(x, y));
                    }
                }
                return list;
            }
            return new List<Point>();
        }

        private List<Point> BresenhamLine(Point p0, Point p1)
        {
            var pts = new List<Point>();
            int x0 = p0.X, y0 = p0.Y;
            int x1 = p1.X, y1 = p1.Y;
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                pts.Add(new Point(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
            return pts;
        }
    }
}
