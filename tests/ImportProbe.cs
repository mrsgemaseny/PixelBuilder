using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PixelBuilder;

// Differential probe: prints how every *.json in a folder imports.
// Run it against the old and the new PixelBuilder.exe and diff the output.
internal static class ImportProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : "legacy_bases";
        string[] files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            bool globalFile = Regex.IsMatch(text, "\"map_kind\"\\s*:\\s*\"global\"|\"global_map\"\\s*:");
            CanvasControl canvas = globalFile ? new CanvasControl("global", 21, 15) : new CanvasControl("local", 1000, 1000);
            bool ok = canvas.ImportJson(text);
            string rel = file.Substring(root.Length).TrimStart('/', '\\');
            Console.WriteLine("{0}|{1}|{2}|blocks={3}|objects={4}|layer={5}", rel, canvas.MapKind, ok,
                canvas.TotalBlockCount, canvas.Objects.Count, canvas.ActiveLayer);
        }
        return 0;
    }
}
