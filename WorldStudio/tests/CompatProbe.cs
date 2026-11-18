using System;
using System.IO;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Serialization.PixelArchitect;

// Проверка совместимости: файлы, записанные WorldStudio, открывает НАСТОЯЩИЙ старый PixelBuilder
// (его собственный CanvasControl.ImportJson), и число блоков совпадает.
// Собирается против ..\bin\PixelBuilder.exe; репозиторий PixelBuilder не меняется.
// Запуск: WorldStudio.CompatProbe.exe <файл или папка> ...
internal static class CompatProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        int files = 0, failed = 0;
        foreach (string arg in args)
        {
            string[] paths = Directory.Exists(arg) ? Directory.GetFiles(arg, "*.json", SearchOption.AllDirectories) : new[] { arg };
            foreach (string path in paths)
            {
                files++;
                string problem = Probe(path);
                if (problem != null) { failed++; Console.WriteLine("FAIL " + Path.GetFileName(path) + ": " + problem); }
            }
        }
        Console.WriteLine("files=" + files + " failed=" + failed);
        Console.WriteLine(failed == 0 && files > 0 ? "COMPAT_PASS" : "COMPAT_FAIL");
        return failed == 0 && files > 0 ? 0 : 1;
    }

    private static string Probe(string path)
    {
        string original = TextFileStore.ReadAllText(path);
        PixelArchitectReadResult read = PixelArchitectCodec.Read(original);
        if (!read.Ok) return "WorldStudio не прочитал: " + read.Error;
        PixelArchitectDocument doc = read.Document;
        string written = PixelArchitectCodec.Write(doc);

        // Без map_kind старый PixelBuilder открывает файл во вкладке локальной карты (MainForm.Files.cs).
        string kind = doc.MapKind ?? "local";
        var oldOnOriginal = new PixelBuilder.CanvasControl(kind, doc.Width, doc.Height);
        var oldOnWritten = new PixelBuilder.CanvasControl(kind, doc.Width, doc.Height);
        bool okOriginal = oldOnOriginal.ImportJson(original);
        bool okWritten = oldOnWritten.ImportJson(written);
        // Если старый инструмент не открывал и исходник, это не регрессия WorldStudio.
        if (!okOriginal)
        {
            Console.WriteLine("SKIP " + Path.GetFileName(path) + ": старый PixelBuilder не открывает и исходный файл");
            return null;
        }
        if (!okWritten) return "старый PixelBuilder не открыл записанный файл" +
            (string.IsNullOrEmpty(oldOnWritten.LastImportError) ? "" : ": " + oldOnWritten.LastImportError);
        if (oldOnOriginal.TotalBlockCount != oldOnWritten.TotalBlockCount)
            return "блоков в старом PixelBuilder: исходник " + oldOnOriginal.TotalBlockCount + ", запись " + oldOnWritten.TotalBlockCount;
        if (WithoutStamp(oldOnOriginal.ExportJson()) != WithoutStamp(oldOnWritten.ExportJson()))
            return "экспорт старого PixelBuilder после исходника и после записи различается";
        Console.WriteLine("OK   " + Path.GetFileName(path) + " blocks=" + oldOnWritten.TotalBlockCount);
        return null;
    }

    // created_at в экспорте старого инструмента - текущее время; строка убирается перед сравнением.
    private static string WithoutStamp(string json)
    {
        int start = json.IndexOf("\"created_at\"");
        if (start < 0) return json;
        int end = json.IndexOf('\n', start);
        return json.Remove(start, (end < 0 ? json.Length : end) - start);
    }
}
