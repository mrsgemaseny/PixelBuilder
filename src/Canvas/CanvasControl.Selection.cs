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
        public void CopySelection()
        {
            if (!HasSelection || SelectionBounds.Width <= 0 || SelectionBounds.Height <= 0) return;

            ClipboardBuffer.Clear();
            for (int x = SelectionBounds.Left; x < SelectionBounds.Right; x++)
            {
                for (int y = SelectionBounds.Top; y < SelectionBounds.Bottom; y++)
                {
                    Point pt = new Point(x, y);
                    if (Cells.ContainsKey(pt))
                    {
                        Point rel = new Point(x - SelectionBounds.Left, y - SelectionBounds.Top);
                        ClipboardBuffer[rel] = Cells[pt];
                    }
                }
            }
        }

        public void CutSelection()
        {
            if (!HasSelection || SelectionBounds.Width <= 0 || SelectionBounds.Height <= 0) return;

            CopySelection();

            _editLayer = _activeLayer;
            _currentStrokeDiff.Clear();
            for (int x = SelectionBounds.Left; x < SelectionBounds.Right; x++)
            {
                for (int y = SelectionBounds.Top; y < SelectionBounds.Bottom; y++)
                {
                    Point pt = new Point(x, y);
                    if (Cells.ContainsKey(pt))
                    {
                        SetCellInternal(pt, "");
                    }
                }
            }
            CommitStroke();
            ClearSelection();
            Invalidate();
        }

        public void DeleteSelection()
        {
            if (!HasSelection || SelectionBounds.Width <= 0 || SelectionBounds.Height <= 0) return;

            _editLayer = _activeLayer;
            _currentStrokeDiff.Clear();
            for (int x = SelectionBounds.Left; x < SelectionBounds.Right; x++)
            {
                for (int y = SelectionBounds.Top; y < SelectionBounds.Bottom; y++)
                {
                    Point pt = new Point(x, y);
                    if (Cells.ContainsKey(pt))
                    {
                        SetCellInternal(pt, "");
                    }
                }
            }
            CommitStroke();
            ClearSelection();
            Invalidate();
        }

        public void StartPaste()
        {
            if (ClipboardBuffer.Count == 0)
            {
                MessageBox.Show("Clipboard is empty! Select an area and click Copy or Cut first.", "Pixel Architect", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            CurrentTool = ToolMode.Paste;
            ClearSelection();
            Invalidate();
        }

        public void CommitPasteAt(Point targetOrigin)
        {
            if (ClipboardBuffer.Count == 0) return;

            _editLayer = _activeLayer;
            _currentStrokeDiff.Clear();
            foreach (var kvp in ClipboardBuffer)
            {
                Point targetCell = new Point(targetOrigin.X + kvp.Key.X, targetOrigin.Y + kvp.Key.Y);
                if (IsInMap(targetCell))
                {
                    SetCellInternal(targetCell, kvp.Value);
                }
            }
            CommitStroke();
            Invalidate();
        }

        public void ClearSelection()
        {
            HasSelection = false;
            SelectionBounds = Rectangle.Empty;
            if (SelectionChanged != null) SelectionChanged();
            Invalidate();
        }
    }
}
