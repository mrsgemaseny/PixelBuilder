using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace PixelBuilder
{
    public partial class CanvasControl
    {
        public string ExportJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"format\": \"pixel_architect_v1\",");
            sb.AppendLine(string.Format("  \"map_kind\": \"{0}\",", MapKind));
            sb.AppendLine(string.Format("  \"map_size\": [{0}, {1}],", MapWidth, MapHeight));
            sb.AppendLine(string.Format("  \"created_at\": \"{0}\",", DateTime.UtcNow.ToString("s")));
            sb.AppendLine(string.Format("  \"total_blocks\": {0},", TotalBlockCount));
            sb.AppendLine(string.Format("  \"active_layer\": \"{0}\",", ActiveLayer));
            sb.AppendLine(string.Format("  \"visibility\": {{ \"political\": {0}, \"landscape\": {1} }},",
                ShowPoliticalLayer.ToString().ToLowerInvariant(), ShowLandscapeLayer.ToString().ToLowerInvariant()));
            sb.AppendLine("  \"layers\": {");
            AppendCellObject(sb, "political", _politicalLayer.Cells, true);
            AppendCellObject(sb, "landscape", _landscapeLayer.Cells, true);
            AppendCellObject(sb, "objects", _objectLayer.Cells, false);
            sb.AppendLine("  },");
            AppendBuildingsBlock(sb);
            // The legacy field stays last so older PixelBuilder versions can read the selected layer.
            AppendCellObject(sb, "blocks", Cells, false);
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Optional additive block: geometry of every building type used in the objects layer.
        private void AppendBuildingsBlock(StringBuilder sb)
        {
            var used = new List<BuildingDef>();
            var seen = new HashSet<string>();
            foreach (string id in _objectLayer.Cells.Values)
            {
                BuildingDef def = GetBuildingDef(id);
                if (def != null && seen.Add(id)) used.Add(def);
            }
            used.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (used.Count == 0) return;
            sb.AppendLine("  \"buildings\": {");
            for (int i = 0; i < used.Count; i++)
            {
                BuildingDef def = used[i];
                sb.AppendLine(string.Format(
                    "    \"{0}\": {{ \"outer\": [{1}, {2}], \"interior\": [{3}, {4}], \"ridge\": \"{5}\", \"door\": [{6}, {7}] }}{8}",
                    def.Id, def.OuterW, def.OuterH, def.InteriorW, def.InteriorH, def.Ridge, def.DoorX, def.DoorY,
                    i < used.Count - 1 ? "," : ""));
            }
            sb.AppendLine("  },");
        }

        private static void AppendCellObject(StringBuilder sb, string name, Dictionary<Point, string> cells, bool comma)
        {
            sb.AppendLine(string.Format("    \"{0}\": {{", name));
            int count = 0;
            foreach (var kvp in cells)
            {
                count++;
                sb.AppendLine(string.Format("      \"{0},{1}\": \"{2}\"{3}", kvp.Key.X, kvp.Key.Y,
                    kvp.Value, count < cells.Count ? "," : ""));
            }
            sb.AppendLine(comma ? "    }," : "    }");
        }

        public bool ImportJson(string json)
        {
            LastImportError = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return false;
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("format") || (string)root["format"] != "pixel_architect_v1") return false;
                if (root.ContainsKey("map_kind") && (string)root["map_kind"] != MapKind) return false;
                var size = root.ContainsKey("map_size") ? root["map_size"] as object[] : null;
                if (size == null || size.Length != 2) return false;
                var entries = root.ContainsKey("blocks") ? root["blocks"] as Dictionary<string, object> : null;
                var layers = root.ContainsKey("layers") ? root["layers"] as Dictionary<string, object> : null;
                if (entries == null && layers == null) return false;
                int offsetX = 0, offsetY = 0;
                if (Convert.ToInt32(size[0]) != MapWidth || Convert.ToInt32(size[1]) != MapHeight)
                {
                    // Earlier 184-cell exports used the fixed 1000x1000 editor canvas.
                    var metadata = root.ContainsKey("global_map") ? root["global_map"] as Dictionary<string, object> : null;
                    var grid = metadata != null && metadata.ContainsKey("grid_size") ? metadata["grid_size"] as object[] : null;
                    var offset = metadata != null && metadata.ContainsKey("editor_offset") ? metadata["editor_offset"] as object[] : null;
                    if (MapKind != "global" || grid == null || offset == null || grid.Length != 2 || offset.Length != 2 ||
                        Convert.ToInt32(grid[0]) != MapWidth || Convert.ToInt32(grid[1]) != MapHeight) return false;
                    offsetX = Convert.ToInt32(offset[0]);
                    offsetY = Convert.ToInt32(offset[1]);
                }
                Dictionary<Point, string> politics, landscape, objects;
                if (layers != null)
                {
                    var politicalEntries = layers.ContainsKey("political") ? layers["political"] as Dictionary<string, object> : null;
                    var landscapeEntries = layers.ContainsKey("landscape") ? layers["landscape"] as Dictionary<string, object> : null;
                    var objectEntries = layers.ContainsKey("objects") ? layers["objects"] as Dictionary<string, object> : null;
                    if (!TryDecodeCells(politicalEntries, offsetX, offsetY, out politics) ||
                        !TryDecodeCells(landscapeEntries, offsetX, offsetY, out landscape)) return false;
                    if (objectEntries != null)
                    {
                        if (!TryDecodeCells(objectEntries, offsetX, offsetY, out objects)) return false;
                        foreach (string id in objects.Values) if (!IsObject(id)) return false;
                    }
                    else objects = new Dictionary<Point, string>();
                }
                else
                {
                    Dictionary<Point, string> decoded;
                    if (!TryDecodeCells(entries, offsetX, offsetY, out decoded)) return false;
                    string targetLayer = ActiveLayer;
                    var metadata = root.ContainsKey("global_map") ? root["global_map"] as Dictionary<string, object> : null;
                    var oldCells = metadata != null && metadata.ContainsKey("cells") ? metadata["cells"] as object[] : null;
                    if (oldCells != null && oldCells.Length > 0)
                    {
                        var first = oldCells[0] as Dictionary<string, object>;
                        if (first != null && first.ContainsKey("marker")) targetLayer = "political";
                        else if (first != null && first.ContainsKey("nature_marker")) targetLayer = "landscape";
                    }
                    politics = targetLayer == "political" ? decoded : new Dictionary<Point, string>(_politicalLayer.Cells);
                    landscape = targetLayer == "landscape" ? decoded : new Dictionary<Point, string>(_landscapeLayer.Cells);
                    objects = new Dictionary<Point, string>(_objectLayer.Cells);
                    SetActiveLayer(targetLayer);
                }

                // Migrate labels saved inside a color layer by older versions.
                foreach (var layer in new[] { politics, landscape })
                    foreach (var item in new List<KeyValuePair<Point, string>>(layer))
                        if (IsObject(item.Value))
                        {
                            if (!objects.ContainsKey(item.Key)) objects[item.Key] = item.Value;
                            layer.Remove(item.Key);
                        }

                string buildingError = ValidateBuildings(objects);
                if (buildingError != null)
                {
                    LastImportError = buildingError;
                    return false;
                }

                // Commit only after successful validation.
                _politicalLayer.Cells = politics;
                _landscapeLayer.Cells = landscape;
                _objectLayer.Cells = objects;
                MarkOccupancyDirty();
                foreach (MapLayer layer in new[] { _politicalLayer, _landscapeLayer, _objectLayer })
                {
                    layer.Undo.Clear(); layer.Redo.Clear(); layer.Stroke.Clear();
                }
                if (root.ContainsKey("active_layer")) SetActiveLayer(root["active_layer"] as string);
                var visibility = root.ContainsKey("visibility") ? root["visibility"] as Dictionary<string, object> : null;
                if (visibility != null)
                {
                    if (visibility.ContainsKey("political")) ShowPoliticalLayer = Convert.ToBoolean(visibility["political"]);
                    if (visibility.ContainsKey("landscape")) ShowLandscapeLayer = Convert.ToBoolean(visibility["landscape"]);
                }
                ClearSelection();

                if (HistoryChanged != null) HistoryChanged();
                NotifyStats();
                Invalidate();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryDecodeCells(Dictionary<string, object> entries, int offsetX, int offsetY,
            out Dictionary<Point, string> decoded)
        {
            decoded = new Dictionary<Point, string>();
            if (entries == null) return false;
            foreach (var entry in entries)
            {
                string[] coords = entry.Key.Split(',');
                int x, y;
                if (coords.Length != 2 || !int.TryParse(coords[0], out x) || !int.TryParse(coords[1], out y)) return false;
                Point point = new Point(x - offsetX, y - offsetY);
                string blockId = entry.Value as string;
                if (blockId == null || !IsInMap(point) || !EnsureColorId(blockId)) return false;
                if (MapKind == "global" && blockId.StartsWith("building_")) return false;
                if (MapKind == "local" && blockId.StartsWith("settlement_")) return false;
                if (decoded.ContainsKey(point)) return false;
                decoded.Add(point, blockId);
            }
            return true;
        }
    }
}
