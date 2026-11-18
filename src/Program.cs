using System;
using System.Windows.Forms;
using PixelBuilder.Kit;

namespace PixelBuilder
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // DPI до создания окон, иначе Windows растягивает картинку и шрифты мылятся.
            Ui.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            UiTheme.SetMode(UiSettings.LoadTheme());
            Application.Run(new MainForm());
        }
    }
}
