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
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _currentHoverCell = ScreenToCell(e.Location);
            _isHoverValid = IsInMap(_currentHoverCell);
            _hasHover = true;

            string bid = (_isHoverValid && Cells.ContainsKey(_currentHoverCell)) ? Cells[_currentHoverCell] : "Empty";
            if (HoverChanged != null) HoverChanged(_currentHoverCell, bid, _isHoverValid);

            if (_isPanning)
            {
                PanOffset = new PointF(_panStartOffset.X + (e.X - _panStartMouse.X) / Zoom,
                                       _panStartOffset.Y + (e.Y - _panStartMouse.Y) / Zoom);
                Invalidate();
            }
            else if (_isSelecting)
            {
                int x0 = Math.Min(_selectStartCell.X, _currentHoverCell.X);
                int x1 = Math.Max(_selectStartCell.X, _currentHoverCell.X);
                int y0 = Math.Min(_selectStartCell.Y, _currentHoverCell.Y);
                int y1 = Math.Max(_selectStartCell.Y, _currentHoverCell.Y);
                SelectionBounds = new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                HasSelection = true;
                if (SelectionChanged != null) SelectionChanged();
                Invalidate();
            }
            else if (_isPainting)
            {
                if (CurrentTool == ToolMode.Brush || CurrentTool == ToolMode.Eraser)
                {
                    if (_isHoverValid) PaintLineTo(_currentHoverCell);
                }
                else
                {
                    Invalidate();
                }
            }
            else
            {
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Space) != 0))
            {
                _isPanning = true;
                _panStartMouse = e.Location;
                _panStartOffset = PanOffset;
                Cursor = Cursors.SizeAll;
                return;
            }

            Point clickedCell = ScreenToCell(e.Location);

            if (e.Button == MouseButtons.Right)
            {
                if (CurrentTool == ToolMode.Paste)
                {
                    CurrentTool = ToolMode.Brush;
                    Invalidate();
                    return;
                }
                if (HasSelection)
                {
                    ClearSelection();
                    return;
                }
            }

            if (CurrentTool == ToolMode.Paste)
            {
                if (e.Button == MouseButtons.Left && _isHoverValid)
                {
                    CommitPasteAt(clickedCell);
                }
                return;
            }

            if (CurrentTool == ToolMode.Select)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _isSelecting = true;
                    _selectStartCell = clickedCell;
                    SelectionBounds = new Rectangle(clickedCell.X, clickedCell.Y, 1, 1);
                    HasSelection = true;
                    if (SelectionChanged != null) SelectionChanged();
                    Invalidate();
                }
                return;
            }

            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                if (!IsInMap(clickedCell)) return;

                // Active building: one click = one building. Line/Rect/Fill/Shift are ignored.
                if (e.Button == MouseButtons.Left && CurrentTool != ToolMode.Eraser && IsBuildingActive)
                {
                    if (CurrentTool == ToolMode.Brush && (ModifierKeys & Keys.Shift) == 0)
                    {
                        string reason;
                        TryPlaceBuilding(clickedCell, ActiveBlockId, out reason);
                    }
                    return;
                }

                _isPainting = true;
                _isErasing = (e.Button == MouseButtons.Right) || (CurrentTool == ToolMode.Eraser);
                _toolStartCell = clickedCell;
                Point objectAnchor;
                SetEditLayer(_isErasing && TryGetObjectAnchor(clickedCell, out objectAnchor));
                _currentStrokeDiff.Clear();

                ToolMode effTool = CurrentTool;
                if ((ModifierKeys & Keys.Shift) != 0) effTool = ToolMode.Line;

                if (effTool == ToolMode.Fill)
                {
                    ExecuteFill(_toolStartCell, _isErasing ? "" : ActiveBlockId);
                    CommitStroke();
                    _isPainting = false;
                }
                else if (effTool == ToolMode.Brush || effTool == ToolMode.Eraser)
                {
                    PaintCell(_toolStartCell, true);
                    _lastPaintedCell = _toolStartCell;
                }
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPanning && (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left))
            {
                _isPanning = false;
                Cursor = Cursors.Default;
                return;
            }

            if (_isSelecting)
            {
                _isSelecting = false;
                Invalidate();
                return;
            }

            if (_isPainting)
            {
                ToolMode effTool = CurrentTool;
                if ((ModifierKeys & Keys.Shift) != 0) effTool = ToolMode.Line;

                if (effTool == ToolMode.Line || effTool == ToolMode.Rect)
                {
                    var preview = GetToolPreviewCells();
                    string target = _isErasing ? "" : ActiveBlockId;
                    foreach (var c in preview)
                    {
                        if (IsInMap(c)) SetCellInternal(c, target);
                    }
                }

                CommitStroke();
                _isPainting = false;
                _lastPaintedCell = new Point(-9999, -9999);
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float factor = e.Delta > 0 ? 1.15f : (1.0f / 1.15f);
            float newZoom = Math.Max(0.08f, Math.Min(8.0f, Zoom * factor));
            if (Math.Abs(newZoom - Zoom) < 0.001f) return;

            float cx = Width * 0.5f;
            float cy = Height * 0.5f;
            float wx = (e.X - cx) / Zoom - PanOffset.X;
            float wy = (e.Y - cy) / Zoom - PanOffset.Y;

            Zoom = newZoom;
            PanOffset = new PointF((e.X - cx) / Zoom - wx,
                                   (e.Y - cy) / Zoom - wy);

            NotifyStats();
            Invalidate();
        }
    }
}
