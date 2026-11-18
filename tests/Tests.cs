using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using PixelBuilder;

internal static class Tests
{
    [STAThread]
    private static int Main(string[] args)
    {
        var global = new CanvasControl("global", 21, 15);
        var local = new CanvasControl("local", 1000, 1000);
        Check(global.IsInMap(new Point(9, 14)) && !global.IsInMap(new Point(0, 0)), "global contour");
        Check(local.IsInMap(new Point(0, 0)) && local.IsInMap(new Point(999, 999)), "local dimensions");
        Check(CanvasControl.ColorPalette.Count == 72, "extended color palette");
        Check(CanvasControl.GlobalObjects.Count == 7 && CanvasControl.LocalObjects.Count == 3, "object catalogs");

        string politicalPath = FindFixture("MiniLife_global_map_pixelbuilder.json");
        string naturePath = FindFixture("MiniLife_global_nature_pixelbuilder.json");
        bool haveLegacyFixtures = politicalPath != null && naturePath != null;
        if (haveLegacyFixtures)
        {
            Check(global.ImportJson(File.ReadAllText(politicalPath)), "legacy global political import");
            Check(global.ActiveLayer == "political" && global.TotalBlockCount == 184, "political layer import");
            Check(global.ImportJson(File.ReadAllText(naturePath)), "legacy global nature import");
            Check(global.ActiveLayer == "landscape" && global.TotalBlockCount == 368, "two independent layers");
        }
        else
        {
            Console.WriteLine("SKIP legacy global fixtures MiniLife_global_*_pixelbuilder.json not found; seeding synthetic cells");
            global.SetActiveLayer("political");
            global.Cells[new Point(8, 14)] = "stone_wall";
            global.SetActiveLayer("landscape");
            global.Cells[new Point(8, 14)] = "grass";
        }
        global.SetActiveLayer("political");
        global.Cells[new Point(9, 14)] = "settlement_village";
        global.SetActiveLayer("landscape");
        global.Cells[new Point(9, 14)] = CanvasControl.RegisterColor(Color.FromArgb(12, 34, 56)).Id;
        string saved = global.ExportJson();
        var reloaded = new CanvasControl("global", 21, 15);
        Check(reloaded.ImportJson(saved), "layered import");
        if (haveLegacyFixtures) Check(reloaded.TotalBlockCount == 368, "layered count");
        reloaded.SetActiveLayer("political");
        Check(reloaded.Objects[new Point(9, 14)] == "settlement_village", "legacy political object migration");
        reloaded.SetActiveLayer("landscape");
        Check(reloaded.Cells[new Point(9, 14)] == "color_0C2238", "custom color round trip");
        Check(!local.ImportJson(saved), "map mode isolation");
        local.SetActiveLayer("political");
        local.Cells[new Point(500, 500)] = "building_large_house";
        local.SetActiveLayer("landscape");
        local.Cells[new Point(500, 500)] = "grass";
        var localReloaded = new CanvasControl("local", 1000, 1000);
        Check(localReloaded.ImportJson(local.ExportJson()), "local layered import");
        Check(localReloaded.TotalBlockCount == 2, "local layer count");
        localReloaded.SetActiveLayer("political");
        Check(localReloaded.Objects[new Point(500, 500)] == "building_large_house", "local building round trip");
        localReloaded.SetActiveLayer("landscape");
        Check(localReloaded.Cells[new Point(500, 500)] == "grass", "local terrain round trip");
        global.SetActiveLayer("political");
        global.ActiveBlockId = "settlement_village";
        var paint = typeof(CanvasControl).GetMethod("PaintCell", BindingFlags.NonPublic | BindingFlags.Instance);
        Point villageCell = new Point(8, 14);
        string politicalColor = global.Cells[villageCell];
        global.SetActiveLayer("landscape");
        string terrainColor = global.Cells[villageCell];
        paint.Invoke(global, new object[] { villageCell, true });
        Check(global.Objects[villageCell] == "settlement_village", "first village click");
        paint.Invoke(global, new object[] { villageCell, true });
        Check(global.Objects[villageCell] == "settlement_village_2", "second village click");
        paint.Invoke(global, new object[] { villageCell, true });
        Check(global.Objects[villageCell] == "settlement_village_3", "third village click");
        paint.Invoke(global, new object[] { villageCell, true });
        Check(global.Objects[villageCell] == "settlement_village_3", "village cap");
        Check(global.Cells[villageCell] == terrainColor, "village preserves terrain");
        global.SetActiveLayer("political");
        Check(global.Cells[villageCell] == politicalColor, "village preserves politics");
        foreach (BlockDef settlement in CanvasControl.GlobalObjects)
        {
            global.ActiveBlockId = settlement.Id;
            paint.Invoke(global, new object[] { villageCell, true });
            Check(global.Objects[villageCell] == settlement.Id, settlement.Id + " text marker");
            Check(global.Cells[villageCell] == politicalColor, settlement.Id + " preserves politics");
            global.SetActiveLayer("landscape");
            Check(global.Cells[villageCell] == terrainColor, settlement.Id + " preserves terrain");
            global.SetActiveLayer("political");
        }
        global.ActiveBlockId = "settlement_village";
        paint.Invoke(global, new object[] { villageCell, true });
        paint.Invoke(global, new object[] { villageCell, true });
        paint.Invoke(global, new object[] { villageCell, true });
        var villageReloaded = new CanvasControl("global", 21, 15);
        Check(villageReloaded.ImportJson(global.ExportJson()), "village level import");
        Check(villageReloaded.Objects[villageCell] == "settlement_village_3", "village level saved");
        // Метки диких земель: независимое поселение и имперская армия сохраняются и читаются как есть.
        var landCells = new System.Collections.Generic.List<Point>();
        for (int y = 0; y < 15 && landCells.Count < 2; y++)
            for (int x = 0; x < 21 && landCells.Count < 2; x++)
                if (global.IsInMap(new Point(x, y)) && new Point(x, y) != villageCell) landCells.Add(new Point(x, y));
        var markCell = landCells[0];
        var armyCell = landCells[1];
        global.ActiveBlockId = "independent_settlement";
        paint.Invoke(global, new object[] { markCell, true });
        global.ActiveBlockId = "imperial_army";
        paint.Invoke(global, new object[] { armyCell, true });
        var marksReloaded = new CanvasControl("global", 21, 15);
        Check(marksReloaded.ImportJson(global.ExportJson()), "marks import");
        Check(marksReloaded.Objects[markCell] == "independent_settlement" && marksReloaded.Objects[armyCell] == "imperial_army", "marks saved");
        using (var form = new MainForm())
        {
            TabControl tabs = null;
            foreach (Control control in form.Controls)
                if (control is TabControl) tabs = (TabControl)control;
            Check(tabs != null && tabs.TabPages.Count == 2, "two map tabs");
            Check(tabs.TabPages[0].Text.Contains("Глобальная") && tabs.TabPages[1].Text.Contains("Локальная"), "map tab labels");
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-3000, -3000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            var objectsField = typeof(MainForm).GetField("_objectPanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var objects = (FlowLayoutPanel)objectsField.GetValue(form);
            Check(objects.Controls.Count == CanvasControl.GlobalObjects.Count && CanvasControl.GlobalObjects.Count == 7, "global objects only");
            tabs.SelectedIndex = 1;
            Application.DoEvents();
            Check(objects.Visible && objects.Controls.Count == CanvasControl.BuildingPresets.Count, "local: house presets");
            tabs.SelectedIndex = 0;
            Application.DoEvents();
            Check(objects.Controls.Count == CanvasControl.GlobalObjects.Count, "global objects restored");
        }
        Console.WriteLine("PIXELBUILDER_TESTS_PASS");
        BuildingTests.Run(args);
        return 0;
    }

    // tests/fixtures first, then the old Downloads location.
    private static string FindFixture(string name)
    {
        var roots = new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory };
        foreach (string root in roots)
        {
            string dir = root;
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = Path.Combine(Path.Combine(Path.Combine(dir, "tests"), "fixtures"), name);
                if (File.Exists(candidate)) return candidate;
                candidate = Path.Combine(Path.Combine(dir, "fixtures"), name);
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
        }
        string downloads = Path.Combine(@"C:\Users\murat\Downloads", name);
        return File.Exists(downloads) ? downloads : null;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }
}
