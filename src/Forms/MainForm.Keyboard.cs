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
    public partial class MainForm
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            _pressedKeys.Add(e.KeyCode);

            if (e.Control && e.KeyCode == Keys.Z) { _canvas.Undo(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.Y) { _canvas.Redo(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.X) { _canvas.CutSelection(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.C) { CopyToClipboardOrSelection(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.V) { _canvas.StartPaste(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.S) { SaveJsonToFile(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.O) { OpenJsonFromFile(); e.Handled = true; return; }

            if (e.KeyCode == Keys.Delete)
            {
                if (_canvas.HasSelection)
                {
                    _canvas.DeleteSelection();
                    e.Handled = true;
                    return;
                }
            }
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (_canvas.CurrentTool == ToolMode.Paste)
                {
                    SelectTool(ToolMode.Brush);
                }
                if (_canvas.HasSelection)
                {
                    _canvas.ClearSelection();
                }
                e.Handled = true;
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.B: SelectTool(ToolMode.Brush); e.Handled = true; break;
                case Keys.L: SelectTool(ToolMode.Line); e.Handled = true; break;
                case Keys.R: SelectTool(ToolMode.Rect); e.Handled = true; break;
                case Keys.F: SelectTool(ToolMode.Fill); e.Handled = true; break;
                case Keys.E: SelectTool(ToolMode.Eraser); e.Handled = true; break;
                case Keys.G: ToggleGrid(); e.Handled = true; break;
                case Keys.T: RotateBuilding(); e.Handled = true; break;
                case Keys.H: ToggleRoofs(); e.Handled = true; break;
                case Keys.Home: _canvas.CenterOnMap(); e.Handled = true; break;

                case Keys.D1: case Keys.NumPad1: SelectBlock("stone_wall"); e.Handled = true; break;
                case Keys.D2: case Keys.NumPad2: SelectBlock("wood_wall"); e.Handled = true; break;
                case Keys.D3: case Keys.NumPad3: SelectBlock("wood_floor"); e.Handled = true; break;
                case Keys.D4: case Keys.NumPad4: SelectBlock("stone_floor"); e.Handled = true; break;
                case Keys.D5: case Keys.NumPad5: SelectBlock("door"); e.Handled = true; break;
                case Keys.D6: case Keys.NumPad6: SelectBlock("water"); e.Handled = true; break;
                case Keys.D7: case Keys.NumPad7: SelectBlock("dirt"); e.Handled = true; break;
                case Keys.D8: case Keys.NumPad8: SelectBlock("pillar"); e.Handled = true; break;
                case Keys.D9: case Keys.NumPad9: SelectBlock("grass"); e.Handled = true; break;
                case Keys.D0: case Keys.NumPad0: SelectBlock("brick"); e.Handled = true; break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            _pressedKeys.Remove(e.KeyCode);
        }
    }
}
