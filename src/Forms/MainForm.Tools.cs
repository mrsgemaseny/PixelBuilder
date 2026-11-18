using System;
using System.Drawing;
using System.Windows.Forms;
using PixelBuilder.Kit;

namespace PixelBuilder
{
    public partial class MainForm
    {
        private void ToggleGrid()
        {
            _canvas.ShowGrid = !_canvas.ShowGrid;
            ((UiButton)_btnGrid).Checked = _canvas.ShowGrid;
            _canvas.Invalidate();
        }

        // F11: borderless full screen over the whole monitor (taskbar covered); F11 again restores the window.
        private void ToggleFullscreen()
        {
            if (!_fullscreen)
            {
                _stateBeforeFullscreen = WindowState;
                WindowState = FormWindowState.Normal;   // a maximized window cannot change its border style reliably
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                _fullscreen = true;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = _stateBeforeFullscreen;
                _fullscreen = false;
            }
        }

        private void RotateBuilding()
        {
            if (_canvas.RotateActiveBuilding()) SelectBlock(_canvas.ActiveBlockId);
        }

        private void ToggleRoofs()
        {
            _canvas.ToggleRoofs();
            UpdateRoofsButton();
        }

        private void UpdateRoofsButton()
        {
            var roofs = _btnRoofs as UiButton;
            if (roofs != null) roofs.Checked = _canvas.ShowRoofs;
        }

        private void SelectTool(ToolMode mode)
        {
            _canvas.CurrentTool = mode;
            if (mode != ToolMode.Select && _canvas.HasSelection)
            {
                _canvas.ClearSelection();
            }
            foreach (var kvp in _toolBtns)
            {
                var b = kvp.Value as UiButton;
                if (b != null) b.Checked = kvp.Key == mode;
            }
        }

        private void SelectBlock(string bid)
        {
            _canvas.ActiveBlockId = bid;
            _canvas.RefreshEditingLayer();
            BuildingDef activeBuilding = CanvasControl.GetBuildingDef(bid);
            if (_btnRotate != null) _btnRotate.Enabled = activeBuilding != null;
            if (CanvasControl.BlockDefs.ContainsKey(bid))
            {
                var def = CanvasControl.BlockDefs[bid];
                _lblActiveBlock.Text = string.IsNullOrEmpty(def.Hotkey) ? def.Name : string.Format("{0} [{1}]", def.Name, def.Hotkey);
                if (_activeSwatch != null)
                {
                    _activeSwatch.Swatch = def.Color;
                    _activeSwatch.Label = def.Symbol;
                    _activeSwatch.Invalidate();
                }
            }
            foreach (var kvp in _paletteBtns)
            {
                bool isSel = kvp.Key == bid;
                var tile = kvp.Value as UiSwatchTile;
                if (tile != null) { tile.Checked = isSel; continue; }
                var button = kvp.Value as UiButton;
                if (button != null) button.Checked = isSel;
            }
            if (_recentColors != null) RememberColor(bid);
        }

        // Очистка слоя стирает всё на активном слое: спрашиваем, хотя Ctrl+Z её и отменяет.
        private void ConfirmClearLayer()
        {
            string layer = _canvas.ActiveLayer == "political" ? "Политика" : "Ландшафт";
            int r = UiAlert.AskStyled(this, "Очистить слой \"" + layer + "\"?",
                "Все клетки этого слоя на текущей карте будут стёрты. Отменить можно через Ctrl+Z.", Glyph.Trash, 0, "Очистить", "Отмена");
            if (r != 0) return;
            _canvas.ClearAll();
            _toast.Show("Слой очищен. Ctrl+Z вернёт", ToastKind.Info);
        }

        private void CopyToClipboardOrSelection()
        {
            if (_canvas.HasSelection)
            {
                _canvas.CopySelection();
                _btnPaste.Enabled = true;
                _toast.Show(string.Format("Скопировано блоков: {0}. Ctrl+V - вставить", _canvas.ClipboardBuffer.Count), ToastKind.Success);
            }
            else CopyJsonToClipboard();
        }

        private void CopyJsonToClipboard()
        {
            Clipboard.SetText(_canvas.ExportJson());
            _toast.Show("JSON карты скопирован в буфер обмена", ToastKind.Success);
        }
    }
}
