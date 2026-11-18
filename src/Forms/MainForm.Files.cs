using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using PixelBuilder.Kit;

namespace PixelBuilder
{
    public partial class MainForm
    {
        private void SaveJsonToFile()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Blueprint JSON (*.json)|*.json";
                sfd.FileName = string.Format("{0}_{1}.json", _canvas.MapKind, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, _canvas.ExportJson(), Encoding.UTF8);
                    _toast.Show("Сохранено: " + Path.GetFileName(sfd.FileName), ToastKind.Success);
                }
            }
        }

        private void OpenJsonFromFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Blueprint JSON (*.json)|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string text = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                    bool globalFile = System.Text.RegularExpressions.Regex.IsMatch(text,
                        "\"map_kind\"\\s*:\\s*\"global\"|\"global_map\"\\s*:");
                    CanvasControl target = globalFile ? _globalCanvas : _localCanvas;
                    if (target.ImportJson(text))
                    {
                        _mapTabs.SelectedIndex = globalFile ? 0 : 1;
                        RefreshSidebar();
                        _toast.Show("Открыто: " + Path.GetFileName(ofd.FileName), ToastKind.Success);
                        UpdateEmptyHint();
                    }
                    else
                    {
                        string detail = string.IsNullOrEmpty(target.LastImportError) ? "" : "\n" + target.LastImportError;
                        UiAlert.Error(this, "Не удалось открыть файл", "Это не похоже на карту PixelBuilder." + detail);
                    }
                }
            }
        }

        private void SavePngImage()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PNG Image (*.png)|*.png";
                sfd.FileName = string.Format("blueprint_{0}.png", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    using (Bitmap bmp = new Bitmap(_canvas.Width, _canvas.Height))
                    {
                        _canvas.DrawToBitmap(bmp, new Rectangle(0, 0, _canvas.Width, _canvas.Height));
                        bmp.Save(sfd.FileName, ImageFormat.Png);
                    }
                    _toast.Show("Картинка сохранена: " + Path.GetFileName(sfd.FileName), ToastKind.Success);
                }
            }
        }
    }
}
