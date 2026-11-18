using System;
using System.Windows.Forms;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // DPI до создания любого окна: иначе Windows растягивает картинку и шрифты мылятся.
            Ui.EnableDpiAwareness();
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            UiTheme.SetMode(Settings.Theme());
            // Непойманные ошибки: короткое сообщение пользователю, подробности в журнал; сырой стектрейс не показывается.
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException += (s, e) => Dialogs.Unexpected(null, "поток UI", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ErrorLog.Write("необработанное исключение", e.ExceptionObject as Exception);
            System.Windows.Forms.Application.Run(new MainForm());
        }
    }
}
