using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MiniLife.WorldStudio.Application
{
    // Журнал ошибок с контекстом (раздел 9). Пользователь видит короткое сообщение, подробности - здесь.
    // Логирование никогда не бросает исключений: сбой лога не должен ронять инструмент.
    public static class ErrorLog
    {
        private static readonly object Gate = new object();

        public static string Directory
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(baseDir)) baseDir = Path.GetTempPath();
                return Path.Combine(Path.Combine(baseDir, "MiniLife.WorldStudio"), "logs");
            }
        }

        public static string FilePath { get { return Path.Combine(Directory, "worldstudio.log"); } }

        public static void Write(string context, Exception ex)
        {
            Write(context, ex == null ? "" : ex.ToString());
        }

        public static void Write(string context, string details)
        {
            try
            {
                lock (Gate)
                {
                    System.IO.Directory.CreateDirectory(Directory);
                    string line = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) + " " + context +
                                  Environment.NewLine + details + Environment.NewLine;
                    File.AppendAllText(FilePath, line, new UTF8Encoding(false));
                }
            }
            catch (Exception)
            {
                // намеренно: см. комментарий класса
            }
        }
    }
}
