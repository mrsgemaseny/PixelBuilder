using System;
using System.IO;
using System.Text;

namespace MiniLife.WorldStudio.Application
{
    // Чтение и атомарная запись текстовых файлов (раздел 9).
    // Запись: временный файл рядом с целевым, затем замена. При сбое целевой файл остаётся прежним.
    // Резервные копии и журнал версий - Этап 5.
    public static class TextFileStore
    {
        // UTF-8 без BOM: так лежат текущие файлы автора (REPORT_00, раздел 5).
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

        public static string ReadAllText(string path)
        {
            // Декодер с throwOnInvalidBytes: битая кодировка - ошибка, а не тихие знаки вопроса.
            byte[] bytes = File.ReadAllBytes(path);
            int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            return Utf8NoBom.GetString(bytes, offset, bytes.Length - offset);
        }

        public static void WriteAllTextAtomic(string path, string text)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (text == null) throw new ArgumentNullException("text");
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                throw new DirectoryNotFoundException("Папка не найдена: " + dir);
            string temp = Path.Combine(dir, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                byte[] bytes = Utf8NoBom.GetBytes(text);
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(full)) File.Replace(temp, full, null);
                else File.Move(temp, full);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}
