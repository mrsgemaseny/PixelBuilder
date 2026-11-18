using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using MiniLife.WorldStudio.Application;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.PixelArchitect;
using MiniLife.WorldStudio.UI;
using MiniLife.WorldStudio.UI.Kit;

// Проба интерфейса без человека: открывает главное окно с миром из трёх карт, рисует кистью
// через события мыши, проверяет отмену и снимает скриншоты окон.
// Запуск: WorldStudio.UiProbe.exe <папка fixtures> <папка data> <папка для скриншотов>
internal static class UiProbe
{
    private static int _failed;

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        string fixtures = args[0], data = args[1], output = args[2];
        Directory.CreateDirectory(output);
        var sources = new List<LegacySource>
        {
            new LegacySource(MapSemantic.Landscape, "nature", Read(fixtures, "legacy_nature_184.json")),
            new LegacySource(MapSemantic.Political, "political", Read(fixtures, "legacy_political_286.json")),
            new LegacySource(MapSemantic.Wildlands, "wildlands", Read(fixtures, "legacy_wildlands_226.json")),
        };
        string error;
        WorldDocument doc = WorldDocument.FromImport(File.ReadAllText(Path.Combine(data, "world_template.json")), sources, null, out error);
        Check(doc != null, "документ: " + error);

        UiTheme.SetMode(ThemeMode.Light);
        var form = new MainForm();
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(0, 0);
        form.Show();
        Pump();
        Shot(form, Path.Combine(output, "light_welcome.png"));
        form.LoadForProbe(doc);
        Pump();
        form.Canvas.FitToView();
        Pump();
        Shot(form, Path.Combine(output, "light_main.png"));

        string before = doc.ExportWorldJson();
        form.ProbeSelectLayer(EditLayer.Landscape);
        form.Canvas.Tool = MapTool.Brush;
        form.Canvas.ActiveValue = "sands";
        Point a = CellCenter(form.Canvas, doc.World, 9, 0), b = CellCenter(form.Canvas, doc.World, 12, 0);
        Mouse(form.Canvas, "OnMouseDown", a);
        Mouse(form.Canvas, "OnMouseMove", b);
        Mouse(form.Canvas, "OnMouseUp", b);
        Check(doc.World.FindCell(new CellCoord(11, 0)).Landscape == "sands", "мазок кистью по 4 клеткам");
        Check(doc.History.UndoCount == 1, "мазок - один шаг истории");
        form.Canvas.SetLocked(EditLayer.Landscape, true);
        Mouse(form.Canvas, "OnMouseDown", CellCenter(form.Canvas, doc.World, 9, 1));
        Mouse(form.Canvas, "OnMouseUp", CellCenter(form.Canvas, doc.World, 9, 1));
        Check(doc.History.UndoCount == 1, "заблокированный слой не редактируется");
        form.Canvas.SetLocked(EditLayer.Landscape, false);
        Shot(form, Path.Combine(output, "light_after_brush.png"));
        doc.Undo();
        Check(doc.ExportWorldJson() == before, "отмена мазка");

        form.ProbeSelectLayer(EditLayer.Objects);
        form.Canvas.ActiveValue = "castle";
        Point c = CellCenter(form.Canvas, doc.World, 9, 0);
        Mouse(form.Canvas, "OnMouseDown", c);
        Mouse(form.Canvas, "OnMouseUp", c);
        Check(doc.World.ObjectAt(new CellCoord(9, 0)) != null, "объект кистью");
        doc.Undo();

        form.Canvas.SetVisible(EditLayer.Landscape, false);
        Pump();
        Shot(form, Path.Combine(output, "light_political_only.png"));
        form.Canvas.SetVisible(EditLayer.Landscape, true);

        doc.Execute(WorldEdits.SetArmyRoute("army.south", new[] { new CellCoord(12, 3), new CellCoord(12, 4), new CellCoord(12, 5), new CellCoord(12, 6),
            new CellCoord(12, 7), new CellCoord(12, 8), new CellCoord(12, 9), new CellCoord(12, 10), new CellCoord(12, 11) }));
        Pump();
        Shot(form, Path.Combine(output, "light_route.png"));

        foreach (ThemeMode mode in new[] { ThemeMode.Light, ThemeMode.Dark })
        {
            UiTheme.SetMode(mode);
            string t = mode == ThemeMode.Light ? "light_" : "dark_";
            form.ProbeSelectLayer(EditLayer.Political);
            form.ProbeTool(MapTool.Brush);
            form.Canvas.Select(new CellCoord(12, 6));
            Pump();
            Shot(form, Path.Combine(output, t + "inspector.png"));
            form.ProbeToggleFindings();
            Pump();
            Shot(form, Path.Combine(output, t + "findings.png"));
            form.ProbeToggleFindings();
            form.ProbeSelectLayer(EditLayer.Objects);
            form.ProbeShowToast("Слой \"Ландшафт\" заблокирован");
            Pump();
            Shot(form, Path.Combine(output, t + "objects_toast.png"));
            form.ProbeSelectLayer(EditLayer.Landscape);
            using (var rel = new RelationsForm(doc)) { rel.StartPosition = FormStartPosition.Manual; rel.Location = new Point(0, 0); rel.Show(); rel.ProbeSelect(0, 3); Pump(); Shot(rel, Path.Combine(output, t + "relations.png")); rel.Close(); }
            using (var reg = new RegistryForm(doc, () => null)) { reg.StartPosition = FormStartPosition.Manual; reg.Location = new Point(0, 0); reg.Show(); Pump(); Shot(reg, Path.Combine(output, t + "registry.png")); reg.Close(); }
            using (var imp = new ImportForm()) { imp.StartPosition = FormStartPosition.Manual; imp.Location = new Point(0, 0); imp.Show(); Pump(); Shot(imp, Path.Combine(output, t + "import.png")); imp.Close(); }
        }

        form.Hide();
        Console.WriteLine(_failed == 0 ? "UI_PROBE_PASS" : "UI_PROBE_FAIL failed=" + _failed);
        Environment.Exit(_failed == 0 ? 0 : 1); // без диалога "сохранить изменения?" при закрытии
        return 0;
    }

    private static PixelArchitectDocument Read(string dir, string name)
    {
        return PixelArchitectCodec.Read(File.ReadAllText(Path.Combine(dir, name))).Document;
    }

    private static void Pump()
    {
        for (int i = 0; i < 5; i++) System.Windows.Forms.Application.DoEvents();
    }

    private static Point CellCenter(MapCanvas canvas, World w, int x, int y)
    {
        var offset = (PointF)typeof(MapCanvas).GetField("_offset", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(canvas);
        var cell = (float)typeof(MapCanvas).GetField("_cell", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(canvas);
        return new Point((int)(offset.X + (x + 0.5f) * cell), (int)(offset.Y + (y + 0.5f) * cell));
    }

    private static void Mouse(Control c, string method, Point p)
    {
        MethodInfo m = typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(c, new object[] { new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0) });
    }

    private static void Shot(Control c, string path)
    {
        // DrawToBitmap в mono не рисует дочерние элементы формы: снимается экран X-сервера.
        Form f = c as Form;
        if (f != null) { f.Activate(); f.BringToFront(); }
        Pump();
        System.Threading.Thread.Sleep(300);
        Pump();
        // CopyFromScreen под Xvfb падает (BadMatch), поэтому снимок делает ImageMagick import.
        Point origin = c.PointToScreen(Point.Empty);
        string geometry = c.ClientSize.Width + "x" + c.ClientSize.Height + "+" + Math.Max(0, origin.X) + "+" + Math.Max(0, origin.Y);
        var psi = new System.Diagnostics.ProcessStartInfo("import", "-window root -crop " + geometry + " +repage \"" + path + "\"") { UseShellExecute = false };
        using (var p = System.Diagnostics.Process.Start(psi)) p.WaitForExit();
        Console.WriteLine("shot " + path);
    }

    private static void Check(bool ok, string what)
    {
        if (ok) return;
        _failed++;
        Console.WriteLine("FAIL " + what);
    }
}
