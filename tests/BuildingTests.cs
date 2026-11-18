using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using PixelBuilder;

// TASK 01 stage A acceptance checks (buildings with size, rotation and roof).
internal static class BuildingTests
{
    private static int _checks;
    private const BindingFlags NonPublic = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run(string[] args)
    {
        CatalogAndRotation();
        PlacementAndOccupancy();
        MapBoundsAndOverlap();
        MouseFlows();
        UndoRedoAndErase();
        JsonRoundTrip();
        ImportValidation();
        GlobalMapUntouched();
        FixtureImport();
        FormControls();
        RenderSmoke();
        if (args != null && args.Length >= 2 && args[0] == "--render") RenderPreview(args[1]);
        Console.WriteLine("PIXELBUILDER_BUILDING_TESTS_PASS checks=" + _checks);
    }

    // ---------- helpers ----------

    private static void Check(bool condition, string name)
    {
        _checks++;
        if (!condition) throw new Exception("BUILDING TEST FAILED: " + name);
    }

    private static CanvasControl NewLocal()
    {
        var canvas = new CanvasControl("local", 1000, 1000);
        canvas.Size = new Size(1200, 800);
        return canvas;
    }

    private static bool Place(CanvasControl canvas, int x, int y, string id)
    {
        string reason;
        return canvas.TryPlaceBuilding(new Point(x, y), id, out reason);
    }

    private static string PlaceReason(CanvasControl canvas, int x, int y, string id)
    {
        string reason;
        canvas.TryPlaceBuilding(new Point(x, y), id, out reason);
        return reason;
    }

    private static bool Owns(CanvasControl canvas, Point cell, Point expectedAnchor)
    {
        Point anchor;
        return canvas.TryGetObjectAnchor(cell, out anchor) && anchor == expectedAnchor;
    }

    private static bool Free(CanvasControl canvas, Point cell)
    {
        Point anchor;
        return !canvas.TryGetObjectAnchor(cell, out anchor);
    }

    private static MouseEventArgs MouseAt(CanvasControl canvas, MouseButtons button, Point cell)
    {
        RectangleF r = canvas.CellToScreenRect(cell);
        return new MouseEventArgs(button, 1, (int)(r.X + r.Width / 2f), (int)(r.Y + r.Height / 2f), 0);
    }

    private static void Send(CanvasControl canvas, string handler, MouseEventArgs args)
    {
        typeof(CanvasControl).GetMethod(handler, NonPublic).Invoke(canvas, new object[] { args });
    }

    // ---------- checks ----------

    private static void CatalogAndRotation()
    {
        // Any size is created on demand from the id; limits are 1..40 inside.
        BuildingDef big = CanvasControl.GetBuildingDef("building_house_6x3_h");
        Check(big != null && big.OuterW == 8 && big.OuterH == 5 && big.DoorX == 3 && big.RotatedId == "building_house_3x6_v", "arbitrary size 6x3 on demand");
        Check(CanvasControl.GetBuildingDef(CanvasControl.BuildingId(40, 1, 'v')) != null, "max interior 40 allowed");
        Check(CanvasControl.GetBuildingDef("building_house_41x2_h") == null && CanvasControl.GetBuildingDef("building_house_0x2_h") == null, "sizes out of range rejected");
        Check(CanvasControl.BlockDefs.ContainsKey("wheat"), "wheat block known (farm bases import)");
        Check(CanvasControl.GetBuildingDef("building_house_2x2_x") == null && CanvasControl.GetBuildingDef("building_house_big") == null, "malformed ids rejected");
        var expected = new Dictionary<string, int[]>
        {
            { "building_house_2x2_h", new[] { 4, 4, 2, 2 } },
            { "building_house_2x2_v", new[] { 4, 4, 2, 2 } },
            { "building_house_3x2_h", new[] { 5, 4, 3, 2 } },
            { "building_house_2x3_v", new[] { 4, 5, 2, 3 } }
        };
        foreach (var pair in expected)
        {
            BuildingDef def = CanvasControl.GetBuildingDef(pair.Key);
            Check(def != null, pair.Key + " registered");
            Check(def.OuterW == pair.Value[0] && def.OuterH == pair.Value[1], pair.Key + " outer size");
            Check(def.InteriorW == pair.Value[2] && def.InteriorH == pair.Value[3], pair.Key + " interior size");
            Check(def.OuterW == def.InteriorW + 2 && def.OuterH == def.InteriorH + 2, pair.Key + " walls add 2");
            Check(def.DoorX == (def.OuterW - 1) / 2 && def.DoorY == def.OuterH - 1, pair.Key + " door south center");
            Check(!string.IsNullOrEmpty(def.Symbol) && def.Id.StartsWith("building_"), pair.Key + " symbol and prefix");
            Check(def.WallBlockId == "wood_wall", pair.Key + " default wall");
            // Ridge rule: vertical when taller than wide; squares follow the chosen variant.
            if (def.OuterH > def.OuterW) Check(def.Ridge == 'v', pair.Key + " tall house has vertical ridge");
            if (def.OuterW > def.OuterH) Check(def.Ridge == 'h', pair.Key + " wide house has horizontal ridge");
        }
        Check(CanvasControl.GetBuildingDef("building_house_2x2_h").DoorX == 1, "door x for width 4");
        Check(CanvasControl.GetBuildingDef("building_house_3x2_h").DoorX == 2, "door x for width 5");
        Check(CanvasControl.GetBuildingDef("building_house_2x2_h").Ridge == 'h' &&
              CanvasControl.GetBuildingDef("building_house_2x2_v").Ridge == 'v', "2x2 variants differ by ridge only");

        var canvas = NewLocal();
        canvas.ActiveBlockId = "building_house_2x2_h";
        Check(canvas.RotateActiveBuilding() && canvas.ActiveBlockId == "building_house_2x2_v", "rotate 2x2_h -> 2x2_v");
        Check(canvas.RotateActiveBuilding() && canvas.ActiveBlockId == "building_house_2x2_h", "rotate 2x2_v -> 2x2_h");
        canvas.ActiveBlockId = "building_house_3x2_h";
        Check(canvas.RotateActiveBuilding() && canvas.ActiveBlockId == "building_house_2x3_v", "rotate 3x2_h -> 2x3_v");
        Check(canvas.RotateActiveBuilding() && canvas.ActiveBlockId == "building_house_3x2_h", "rotate 2x3_v -> 3x2_h");
        canvas.ActiveBlockId = "stone_wall";
        Check(!canvas.RotateActiveBuilding() && canvas.ActiveBlockId == "stone_wall", "rotate ignores non-buildings");

        // Старые 5 объектов глобальной карты на месте и в прежнем порядке; метки диких земель добавлены после них.
        string[] legacyGlobal = { "settlement_castle", "settlement_fortress", "settlement_city", "settlement_village", "settlement_hamlet" };
        bool legacyKept = CanvasControl.GlobalObjects.Count == 7;
        for (int i = 0; legacyKept && i < legacyGlobal.Length; i++) legacyKept = CanvasControl.GlobalObjects[i].Id == legacyGlobal[i];
        Check(CanvasControl.LocalObjects.Count == 3 && legacyKept && CanvasControl.GlobalObjects[5].Id == "independent_settlement"
              && CanvasControl.GlobalObjects[6].Id == "imperial_army", "legacy catalogs unchanged, wildland marks appended");
        Check(CanvasControl.GetBuildingDef("building_hut") == null, "legacy markers are not buildings");
    }

    private static void PlacementAndOccupancy()
    {
        var canvas = NewLocal();
        int x = 100;
        string[] ids = { "building_house_2x2_h", "building_house_2x2_v", "building_house_3x2_h", "building_house_2x3_v", "building_house_12x4_h", "building_house_6x11_v" };
        foreach (string id in ids)
        {
            BuildingDef def = CanvasControl.GetBuildingDef(id);
            Check(Place(canvas, x, 100, def.Id), def.Id + " placed");
            Point anchor = new Point(x, 100);
            Check(canvas.Objects.ContainsKey(anchor) && canvas.Objects[anchor] == def.Id, def.Id + " anchor stored");
            for (int dx = 0; dx < def.OuterW; dx++)
                for (int dy = 0; dy < def.OuterH; dy++)
                    Check(Owns(canvas, new Point(x + dx, 100 + dy), anchor), def.Id + " owns cell");
            Check(Free(canvas, new Point(x + def.OuterW, 100)) && Free(canvas, new Point(x, 100 + def.OuterH)), def.Id + " no spill");
            x += def.OuterW + 2;
        }
        // Only anchors are stored: one record per building.
        Check(canvas.Objects.Count == ids.Length, "one objects record per building");
        Check(canvas.TotalBlockCount == ids.Length, "block count counts anchors only");
    }

    private static void MapBoundsAndOverlap()
    {
        var canvas = NewLocal();
        BuildingDef house = CanvasControl.GetBuildingDef("building_house_2x2_h");
        string reason;
        Check(!canvas.CanPlaceBuilding(new Point(997, 10), house, out reason) && reason.Contains("вне карты"), "right edge rejected");
        Check(!canvas.CanPlaceBuilding(new Point(10, 997), house, out reason) && reason.Contains("вне карты"), "bottom edge rejected");
        Check(!canvas.CanPlaceBuilding(new Point(-1, 10), house, out reason), "left edge rejected");
        Check(canvas.CanPlaceBuilding(new Point(996, 996), house, out reason), "exact corner fits");
        Check(Place(canvas, 996, 996, house.Id) && Free(canvas, new Point(995, 996)), "corner placement");
        Check(!Place(canvas, 998, 10, house.Id) && canvas.Objects.Count == 1, "rejected placement stores nothing");

        Check(Place(canvas, 10, 10, house.Id), "first house");
        Check(!Place(canvas, 12, 12, house.Id) && PlaceReason(canvas, 12, 12, house.Id).Contains("пересечение"), "overlap rejected");
        Check(!Place(canvas, 13, 10, house.Id), "single shared column rejected");
        Check(!Place(canvas, 10, 10, house.Id), "same anchor rejected");
        Check(Place(canvas, 14, 10, house.Id), "touching walls allowed (right)");
        Check(Place(canvas, 10, 14, house.Id), "touching walls allowed (below)");
        Check(canvas.Objects.Count == 4, "three houses plus corner house");

        // A legacy 1x1 marker occupies its cell too.
        string markerReason;
        Check(canvas.TryPlaceBuilding(new Point(500, 500), "building_hut", out markerReason) == false, "marker is not a building");
        var legacy = new CanvasControl("local", 1000, 1000);
        legacy.Size = new Size(1200, 800);
        legacy.Objects[new Point(52, 52)] = "building_hut";
        Check(!Place(legacy, 50, 50, house.Id), "building cannot cover a legacy marker");
        Check(Place(legacy, 53, 50, house.Id), "building next to a legacy marker");
    }

    private static void MouseFlows()
    {
        var canvas = NewLocal();
        canvas.ActiveBlockId = "building_house_3x2_h";
        canvas.CurrentTool = ToolMode.Brush;
        canvas.RefreshEditingLayer();

        Point a = new Point(500, 500);
        Send(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Left, a));
        Send(canvas, "OnMouseMove", MouseAt(canvas, MouseButtons.Left, new Point(510, 500)));
        Send(canvas, "OnMouseMove", MouseAt(canvas, MouseButtons.Left, new Point(520, 505)));
        Send(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Left, new Point(520, 505)));
        Check(canvas.Objects.Count == 1 && canvas.Objects.ContainsKey(a), "click places one, drag does not place a series");

        foreach (ToolMode tool in new[] { ToolMode.Line, ToolMode.Rect, ToolMode.Fill })
        {
            canvas.CurrentTool = tool;
            Point p = new Point(520, 500);
            Send(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Left, p));
            Send(canvas, "OnMouseMove", MouseAt(canvas, MouseButtons.Left, new Point(530, 505)));
            Send(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Left, new Point(530, 505)));
            Check(canvas.Objects.Count == 1, tool + " ignored while a building is active");
        }
        canvas.CurrentTool = ToolMode.Brush;

        // Right click on a NON-anchor cell removes the whole building.
        Send(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Right, new Point(503, 502)));
        Send(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Right, new Point(503, 502)));
        Check(canvas.Objects.Count == 0 && Free(canvas, a) && Free(canvas, new Point(503, 502)), "RMB on interior cell erases building");

        // Eraser on a wall cell, then drag across two buildings in one stroke.
        Check(Place(canvas, 500, 500, "building_house_2x2_h") && Place(canvas, 506, 500, "building_house_2x2_v"), "two houses");
        canvas.CurrentTool = ToolMode.Eraser;
        Send(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Left, new Point(503, 503)));
        Check(canvas.Objects.Count == 1 && canvas.Objects.ContainsKey(new Point(506, 500)), "eraser on far corner removes first house only");
        Send(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Left, new Point(503, 503)));
        Send(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Left, new Point(507, 501)));
        Send(canvas, "OnMouseMove", MouseAt(canvas, MouseButtons.Left, new Point(508, 502)));
        Send(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Left, new Point(508, 502)));
        Check(canvas.Objects.Count == 0, "eraser drag removes building once");
        canvas.CurrentTool = ToolMode.Brush;

        // Legacy marker over a building is refused; marker elsewhere still works.
        Check(Place(canvas, 600, 600, "building_house_2x2_h"), "house for marker test");
        canvas.ActiveBlockId = "building_hut";
        canvas.RefreshEditingLayer();
        Send(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Left, new Point(601, 601)));
        Send(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Left, new Point(601, 601)));
        Check(canvas.Objects.Count == 1, "marker not dropped on a building");
        Send(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Left, new Point(700, 700)));
        Send(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Left, new Point(700, 700)));
        Check(canvas.Objects.Count == 2 && canvas.Objects[new Point(700, 700)] == "building_hut", "legacy marker still paints");
    }

    private static void UndoRedoAndErase()
    {
        var canvas = NewLocal();
        canvas.ActiveBlockId = "building_house_2x3_v";
        canvas.RefreshEditingLayer();
        Check(Place(canvas, 40, 40, canvas.ActiveBlockId), "placed for undo");
        Check(canvas.CanUndo && Owns(canvas, new Point(43, 44), new Point(40, 40)), "undo available after place");
        canvas.Undo();
        Check(canvas.Objects.Count == 0 && Free(canvas, new Point(43, 44)), "undo place frees occupancy");
        canvas.Redo();
        Check(canvas.Objects.Count == 1 && Owns(canvas, new Point(43, 44), new Point(40, 40)), "redo place restores occupancy");

        Check(canvas.RemoveObjectAt(new Point(42, 43)), "remove via interior cell");
        Check(canvas.Objects.Count == 0 && Free(canvas, new Point(40, 40)), "removed whole building");
        canvas.Undo();
        Check(canvas.Objects.Count == 1 && Owns(canvas, new Point(43, 44), new Point(40, 40)), "undo removal restores building");
        canvas.Redo();
        Check(canvas.Objects.Count == 0 && Free(canvas, new Point(43, 44)), "redo removal");
        Check(!canvas.RemoveObjectAt(new Point(1, 1)), "remove on empty cell is a no-op");

        Check(Place(canvas, 40, 40, canvas.ActiveBlockId) && Place(canvas, 60, 40, canvas.ActiveBlockId), "two for clear");
        canvas.ClearAll();
        Check(canvas.Objects.Count == 0 && Free(canvas, new Point(41, 41)), "clear layer resets occupancy");
        canvas.Undo();
        Check(canvas.Objects.Count == 2 && Owns(canvas, new Point(61, 41), new Point(60, 40)), "undo clear restores buildings");
    }

    private static string StripTimestamp(string json)
    {
        var sb = new StringBuilder();
        foreach (string line in json.Split('\n'))
            if (!line.Contains("\"created_at\"")) sb.AppendLine(line.TrimEnd('\r'));
        return sb.ToString();
    }

    private static void JsonRoundTrip()
    {
        var canvas = NewLocal();
        Check(Place(canvas, 100, 100, "building_house_2x2_h"), "rt a");
        Check(Place(canvas, 110, 100, "building_house_2x2_v"), "rt b");
        Check(Place(canvas, 120, 100, "building_house_3x2_h"), "rt c");
        Check(Place(canvas, 130, 100, "building_house_2x3_v"), "rt d");
        canvas.Objects[new Point(300, 300)] = "building_hut";
        string json = canvas.ExportJson();

        Check(json.Contains("\"format\": \"pixel_architect_v1\""), "format unchanged");
        Check(json.Contains("\"buildings\": {"), "buildings block present");
        Check(json.Contains("\"building_house_3x2_h\": { \"outer\": [5, 4], \"interior\": [3, 2], \"ridge\": \"h\", \"door\": [2, 3] }"), "3x2 geometry");
        Check(json.Contains("\"building_house_2x3_v\": { \"outer\": [4, 5], \"interior\": [2, 3], \"ridge\": \"v\", \"door\": [1, 4] }"), "2x3 geometry");
        Check(json.Contains("\"building_house_2x2_h\": { \"outer\": [4, 4], \"interior\": [2, 2], \"ridge\": \"h\", \"door\": [1, 3] }"), "2x2 geometry");
        Check(!json.Contains("\"building_hut\": {"), "legacy marker has no geometry entry");

        // objects layer: exactly one record per building (+ the legacy marker).
        int idx = json.IndexOf("\"objects\": {");
        string objectsBlock = json.Substring(idx, json.IndexOf("}", idx) - idx);
        int records = objectsBlock.Split(new[] { "\":" }, StringSplitOptions.None).Length - 2;
        Check(records == 5, "objects layer has one record per building, got " + records);

        var reloaded = NewLocal();
        Check(reloaded.ImportJson(json), "import own export");
        Check(reloaded.Objects.Count == 5, "reloaded object count");
        foreach (var kvp in canvas.Objects) Check(reloaded.Objects.ContainsKey(kvp.Key) && reloaded.Objects[kvp.Key] == kvp.Value, "reloaded " + kvp.Value);
        Check(Owns(reloaded, new Point(124, 103), new Point(120, 100)), "occupancy rebuilt after import");
        Check(StripTimestamp(reloaded.ExportJson()) == StripTimestamp(json), "second export identical");

        // Old readers ignore unknown keys: the new block is purely additive.
        string withoutBuildings = json.Substring(0, json.IndexOf("  \"buildings\": {")) +
            json.Substring(json.IndexOf("  \"blocks\": {"));
        var oldStyle = NewLocal();
        Check(oldStyle.ImportJson(withoutBuildings) && oldStyle.Objects.Count == 5, "import without buildings block");
    }

    private static string BuildJson(params string[] objectEntries)
    {
        return "{ \"format\": \"pixel_architect_v1\", \"map_kind\": \"local\", \"map_size\": [1000, 1000]," +
            " \"layers\": { \"political\": {}, \"landscape\": {}, \"objects\": { " + string.Join(", ", objectEntries) + " } } }";
    }

    private static void ImportValidation()
    {
        var canvas = NewLocal();
        Check(canvas.ImportJson(BuildJson("\"10,10\": \"building_house_2x2_h\"", "\"14,10\": \"building_house_3x2_h\"")), "touching buildings import");
        Check(canvas.LastImportError == null, "no error on success");

        Check(!canvas.ImportJson(BuildJson("\"997,10\": \"building_house_2x2_h\"")), "building outside map rejected");
        Check(canvas.LastImportError != null && canvas.LastImportError.Contains("вне карты") && canvas.LastImportError.Contains("997,10"),
            "outside message names coordinates: " + canvas.LastImportError);
        Check(canvas.Objects.Count == 2, "failed import keeps previous state");

        Check(!canvas.ImportJson(BuildJson("\"10,10\": \"building_house_2x2_h\"", "\"12,12\": \"building_house_3x2_h\"")), "overlap rejected");
        Check(canvas.LastImportError != null && canvas.LastImportError.Contains("пересека"), "overlap message: " + canvas.LastImportError);

        Check(!canvas.ImportJson(BuildJson("\"10,10\": \"building_house_9x9_h\"")), "unknown building id rejected");
        Check(canvas.ImportJson(BuildJson("\"10,10\": \"building_hut\"", "\"11,10\": \"building_hut\"")), "adjacent legacy markers import");
    }

    private static void GlobalMapUntouched()
    {
        var global = new CanvasControl("global", 21, 15);
        string json = "{ \"format\": \"pixel_architect_v1\", \"map_kind\": \"global\", \"map_size\": [21, 15]," +
            " \"layers\": { \"political\": {}, \"landscape\": {}, \"objects\": { \"9,5\": \"building_house_2x2_h\" } } }";
        Check(!global.ImportJson(json), "global rejects building_ ids");
        string reason;
        Check(!global.TryPlaceBuilding(new Point(9, 5), "building_house_2x2_h", out reason), "global cannot place buildings");
        Check(global.Objects.Count == 0, "global map stays empty");
        bool noBuildings = true;
        foreach (BlockDef def in CanvasControl.GlobalObjects) if (def.Id.StartsWith("building_")) noBuildings = false;
        Check(CanvasControl.GlobalObjects.Count == 7 && noBuildings, "global catalog has no buildings");
        Check(!global.ExportJson().Contains("\"buildings\""), "global export has no buildings block");
    }

    private static string FindFixture(string name)
    {
        string dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
        {
            string candidate = Path.Combine(Path.Combine(Path.Combine(dir, "tests"), "fixtures"), name);
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(Path.Combine(dir, "fixtures"), name);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static void FixtureImport()
    {
        string path = FindFixture("global_layers_286.json");
        if (path == null) { Console.WriteLine("SKIP fixture global_layers_286.json not found"); return; }
        var global = new CanvasControl("global", 21, 15);
        Check(global.ImportJson(File.ReadAllText(path, Encoding.UTF8)), "user fixture imports into global map");
        Check(global.TotalBlockCount == 286, "user fixture block count " + global.TotalBlockCount);
        Check(global.ActiveLayer == "landscape", "user fixture active layer");
    }

    private static void FormControls()
    {
        using (var form = new MainForm())
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-3000, -3000);
            form.ShowInTaskbar = false;
            form.Show();

            TabControl tabs = null;
            foreach (Control control in form.Controls) if (control is TabControl) tabs = (TabControl)control;
            var panel = (FlowLayoutPanel)typeof(MainForm).GetField("_objectPanel", NonPublic).GetValue(form);
            var rotate = (Button)typeof(MainForm).GetField("_btnRotate", NonPublic).GetValue(form);
            var roofs = (Button)typeof(MainForm).GetField("_btnRoofs", NonPublic).GetValue(form);
            var heading = (Label)typeof(MainForm).GetField("_objectsHeading", NonPublic).GetValue(form);
            var active = (Label)typeof(MainForm).GetField("_lblActiveBlock", NonPublic).GetValue(form);
            var canvasField = typeof(MainForm).GetField("_canvas", NonPublic);

            Check(heading.Text.StartsWith("Поселения"), "global heading");
            tabs.SelectedIndex = 1;
            var local = (CanvasControl)canvasField.GetValue(form);
            Check(local.MapKind == "local", "local tab active");
            Check(heading.Text == "Жильё", "local heading is Жильё");
            Check(panel.Visible && panel.Controls.Count == CanvasControl.BuildingPresets.Count, "local: house presets listed");
            Check(rotate.Visible && roofs.Visible && !rotate.Enabled, "rotate disabled until a building is active");
            Check(CanvasControl.BuildingPresets.Count >= 20, "many house types offered");
            Button preset = null;
            foreach (Control c in panel.Controls) if (c.Name == "building_house_8x4_h") preset = (Button)c;
            Check(preset != null && preset.Text.Contains("8x4"), "preset button shows name and size");
            preset.PerformClick();
            Check(local.ActiveBlockId == "building_house_8x4_h" && local.CurrentTool == ToolMode.Brush, "preset selects the house with Brush");

            foreach (Control c in panel.Controls) if (c.Name == "building_house_2x2_h") ((Button)c).PerformClick();
            Check(local.ActiveBlockId == "building_house_2x2_h", "2x2 preset selected");
            Check(rotate.Enabled, "rotate enabled for a building");
            Check(active.Text.Contains("гориз."), "active label shows variant");

            KeyDown(form, Keys.T);
            Check(local.ActiveBlockId == "building_house_2x2_v" && active.Text.Contains("верт."), "T rotates 2x2 and updates label");
            KeyUp(form, Keys.T);
            rotate.PerformClick();
            Check(local.ActiveBlockId == "building_house_2x2_h", "rotate button toggles back");

            foreach (Control c in panel.Controls) if (c.Name == "building_house_6x3_h") ((Button)c).PerformClick();
            KeyDown(form, Keys.T);
            Check(local.ActiveBlockId == "building_house_3x6_v", "T rotates 6x3 -> 3x6");
            KeyUp(form, Keys.T);

            bool before = local.ShowRoofs;
            KeyDown(form, Keys.H);
            Check(local.ShowRoofs == !before, "H toggles roofs");
            KeyUp(form, Keys.H);
            roofs.PerformClick();
            Check(local.ShowRoofs == before, "roof button toggles");

            KeyDown(form, Keys.F11);
            Check(form.FormBorderStyle == FormBorderStyle.None, "F11 enters borderless full screen");
            KeyUp(form, Keys.F11);
            KeyDown(form, Keys.F11);
            Check(form.FormBorderStyle == FormBorderStyle.Sizable, "F11 again restores the window");
            KeyUp(form, Keys.F11);

            tabs.SelectedIndex = 0;
            Check(panel.Controls.Count == CanvasControl.GlobalObjects.Count && panel.Visible && !rotate.Visible, "global tab hides building tools");
        }
    }

    private static void KeyDown(MainForm form, Keys key)
    {
        typeof(MainForm).GetMethod("OnKeyDown", NonPublic).Invoke(form, new object[] { new KeyEventArgs(key) });
    }

    private static void KeyUp(MainForm form, Keys key)
    {
        typeof(MainForm).GetMethod("OnKeyUp", NonPublic).Invoke(form, new object[] { new KeyEventArgs(key) });
    }

    private static void Paint(CanvasControl canvas, Bitmap bmp)
    {
        using (Graphics g = Graphics.FromImage(bmp))
            typeof(CanvasControl).GetMethod("OnPaint", NonPublic).Invoke(canvas, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, bmp.Width, bmp.Height)) });
    }

    private static void RenderSmoke()
    {
        var canvas = NewLocal();
        canvas.Zoom = 1.0f;
        canvas.PanOffset = new PointF(-520 * CanvasControl.CellSize, -500 * CanvasControl.CellSize);
        Place(canvas, 500, 500, "building_house_2x2_h");
        Place(canvas, 510, 500, "building_house_3x2_h");
        using (Bitmap bmp = new Bitmap(1200, 800))
        {
            foreach (bool roofs in new[] { true, false })
            {
                canvas.ShowRoofs = roofs;
                foreach (float zoom in new[] { 2.0f, 1.0f, 0.5f, 0.17f, 0.08f })
                {
                    canvas.Zoom = zoom;
                    Paint(canvas, bmp);
                }
            }
        }
        Check(true, "rendering at all zoom levels does not throw");
    }

    // Writes PNG previews for manual inspection (stage A visual check).
    private static void RenderPreview(string dir)
    {
        Directory.CreateDirectory(dir);
        var canvas = NewLocal();
        canvas.Size = new Size(900, 520);
        canvas.Zoom = 1.2f;
        canvas.PanOffset = new PointF(-509 * CanvasControl.CellSize, -503 * CanvasControl.CellSize);
        Place(canvas, 500, 500, "building_house_2x2_h");
        Place(canvas, 505, 500, "building_house_2x2_v");
        Place(canvas, 510, 500, "building_house_3x2_h");
        Place(canvas, 516, 500, "building_house_2x3_v");
        using (Bitmap bmp = new Bitmap(900, 520))
        {
            canvas.ShowRoofs = true;
            Paint(canvas, bmp);
            bmp.Save(Path.Combine(dir, "buildings_roofs.png"), ImageFormat.Png);
            canvas.ShowRoofs = false;
            Paint(canvas, bmp);
            bmp.Save(Path.Combine(dir, "buildings_interior.png"), ImageFormat.Png);
            canvas.ShowRoofs = true;
            canvas.Zoom = 0.12f;
            canvas.PanOffset = new PointF(-510 * CanvasControl.CellSize, -502 * CanvasControl.CellSize);
            Paint(canvas, bmp);
            bmp.Save(Path.Combine(dir, "buildings_far.png"), ImageFormat.Png);

            // Ghost: valid, overlapping, and outside the map.
            var ghost = NewLocal();
            ghost.Size = new Size(900, 520);
            ghost.Zoom = 1.5f;
            ghost.PanOffset = new PointF(-8 * CanvasControl.CellSize, -5 * CanvasControl.CellSize);
            Place(ghost, 2, 2, "building_house_2x2_h");
            ghost.ActiveBlockId = "building_house_3x2_h";
            ghost.CurrentTool = ToolMode.Brush;
            ghost.RefreshEditingLayer();
            Send(ghost, "OnMouseMove", MouseAt(ghost, MouseButtons.None, new Point(10, 3)));
            Paint(ghost, bmp);
            bmp.Save(Path.Combine(dir, "ghost_valid.png"), ImageFormat.Png);
            Send(ghost, "OnMouseMove", MouseAt(ghost, MouseButtons.None, new Point(4, 3)));
            Paint(ghost, bmp);
            bmp.Save(Path.Combine(dir, "ghost_overlap.png"), ImageFormat.Png);
            ghost.PanOffset = new PointF(-994 * CanvasControl.CellSize, -5 * CanvasControl.CellSize);
            Send(ghost, "OnMouseMove", MouseAt(ghost, MouseButtons.None, new Point(998, 3)));
            Paint(ghost, bmp);
            bmp.Save(Path.Combine(dir, "ghost_outside.png"), ImageFormat.Png);
        }
    }
}
