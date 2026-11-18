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
        public static readonly List<BlockDef> Palette = new List<BlockDef>
        {
            new BlockDef { Id = "stone_wall", Name = "Stone Wall", Hotkey = "1", Color = Color.FromArgb(62, 66, 75),  BorderColor = Color.FromArgb(95, 102, 116) },
            new BlockDef { Id = "wood_wall",  Name = "Wood Wall",  Hotkey = "2", Color = Color.FromArgb(118, 80, 48),  BorderColor = Color.FromArgb(152, 106, 65) },
            new BlockDef { Id = "wood_floor", Name = "Wood Floor", Hotkey = "3", Color = Color.FromArgb(154, 114, 72), BorderColor = Color.FromArgb(126, 92, 56) },
            new BlockDef { Id = "stone_floor",Name = "Stone Floor",Hotkey = "4", Color = Color.FromArgb(46, 50, 58),   BorderColor = Color.FromArgb(36, 39, 46) },
            new BlockDef { Id = "door",       Name = "Doorway",    Hotkey = "5", Color = Color.FromArgb(208, 162, 70), BorderColor = Color.FromArgb(238, 192, 88) },
            new BlockDef { Id = "water",      Name = "Water",      Hotkey = "6", Color = Color.FromArgb(45, 92, 138),  BorderColor = Color.FromArgb(64, 122, 182) },
            new BlockDef { Id = "dirt",       Name = "Dirt",       Hotkey = "7", Color = Color.FromArgb(68, 50, 36),   BorderColor = Color.FromArgb(88, 66, 48) },
            new BlockDef { Id = "pillar",     Name = "Pillar",     Hotkey = "8", Color = Color.FromArgb(98, 104, 118), BorderColor = Color.FromArgb(135, 142, 158) },
            new BlockDef { Id = "grass",      Name = "Grass",      Hotkey = "9", Color = Color.FromArgb(52, 92, 48),   BorderColor = Color.FromArgb(70, 118, 65) },
            new BlockDef { Id = "brick",      Name = "Red Brick",  Hotkey = "0", Color = Color.FromArgb(130, 52, 45),  BorderColor = Color.FromArgb(165, 68, 60) }
        };

        public static readonly Dictionary<string, BlockDef> BlockDefs = new Dictionary<string, BlockDef>();
        public static readonly List<BlockDef> ColorPalette = new List<BlockDef>();
        // Ready-made house types shown in the sidebar (interior size, walls are added around it).
        // To add a type, append one line here; any other size can still be typed into the size fields.
        public static readonly List<KeyValuePair<string, Point>> BuildingPresets = new List<KeyValuePair<string, Point>>
        {
            Preset("Хижина", 2, 2), Preset("Домик", 3, 2), Preset("Дом", 3, 3), Preset("Дом", 4, 3),
            Preset("Дом", 4, 4), Preset("Дом", 6, 4), Preset("Длинный дом", 5, 3), Preset("Длинный дом", 6, 3),
            Preset("Длинный дом", 7, 4), Preset("Длинный дом", 8, 4), Preset("Большой дом", 5, 5),
            Preset("Большой дом", 6, 6), Preset("Большой дом", 7, 7), Preset("Барак", 7, 2), Preset("Барак", 9, 3),
            Preset("Амбар", 8, 5), Preset("Амбар", 10, 4), Preset("Казарма", 12, 4), Preset("Склад", 10, 6),
            Preset("Склад", 6, 9), Preset("Зал", 12, 8), Preset("Зал", 6, 11), Preset("Мастерская", 5, 4),
            Preset("Лавка", 4, 2)
        };

        private static KeyValuePair<string, Point> Preset(string name, int w, int h)
        {
            return new KeyValuePair<string, Point>(name, new Point(w, h));
        }

        public static readonly List<BlockDef> GlobalObjects = new List<BlockDef>();
        public static readonly List<BlockDef> LocalObjects = new List<BlockDef>();
        private static readonly string[] GlobalLandRows = {
            ".........RRRRRRR.....", ".........RRRRRRR.....", ".......YYRRRRRRRRG...",
            "....MMYYYRRRRRRRRGGG.", "....MMYYYYYRRRRMMMGG.", "....MMMYYBBBBYMMMMMG.",
            "..MMMMMBBBBBYYYYMMGG.", "..MMMMBBBBBBYYCCGGGG.", "..MMMMBBBBBYYYCCGG...",
            "..MMMMBBBBMMYYMMM....", "..MMYYYYYMMMMMMMM....", "..MMYYYYYMMMM........",
            "....YYCCYMMMM........", "......CCMMMM.........", ".......CMM..........."
        };

        static CanvasControl()
        {
            foreach (var b in Palette)
            {
                BlockDefs[b.Id] = b;
            }
            // Block used by existing bases (fields); not in the quick palette.
            BlockDefs["wheat"] = new BlockDef { Id = "wheat", Name = "Wheat", Symbol = "", Hotkey = "",
                Color = Color.FromArgb(201, 170, 64), BorderColor = Color.FromArgb(160, 134, 48) };
            AddObject(GlobalObjects, "settlement_castle", "Замок", "З", Color.FromArgb(145, 81, 65));
            AddObject(GlobalObjects, "settlement_fortress", "Крепость", "К", Color.FromArgb(125, 105, 92));
            AddObject(GlobalObjects, "settlement_city", "Город", "Г", Color.FromArgb(207, 151, 74));
            AddObject(GlobalObjects, "settlement_village", "Деревня", "Д", Color.FromArgb(171, 140, 88));
            BlockDefs["settlement_village_2"] = new BlockDef { Id = "settlement_village_2", Name = "Деревня, уровень 2",
                Symbol = "2Д", Hotkey = "", Color = Color.FromArgb(171, 140, 88), BorderColor = Color.FromArgb(126, 102, 64) };
            BlockDefs["settlement_village_3"] = new BlockDef { Id = "settlement_village_3", Name = "Деревня, уровень 3",
                Symbol = "3Д", Hotkey = "", Color = Color.FromArgb(171, 140, 88), BorderColor = Color.FromArgb(126, 102, 64) };
            AddObject(GlobalObjects, "settlement_hamlet", "Село", "С", Color.FromArgb(128, 158, 91));
            // Метки диких земель (WorldStudio D-33): поселение без лорда и армия империи. Не поселения государств.
            AddObject(GlobalObjects, "independent_settlement", "Независимое поселение", "Н", Color.FromArgb(120, 120, 128));
            AddObject(GlobalObjects, "imperial_army", "Имперская армия", "А", Color.FromArgb(190, 30, 30));
            AddObject(LocalObjects, "building_large_house", "Большой дом", "Б", Color.FromArgb(174, 112, 74));
            AddObject(LocalObjects, "building_small_house", "Маленький дом", "М", Color.FromArgb(192, 146, 96));
            AddObject(LocalObjects, "building_hut", "Шалаш", "Ш", Color.FromArgb(116, 137, 77));
            for (int hue = 0; hue < 12; hue++)
            {
                for (int shade = 0; shade < 6; shade++)
                {
                    Color color = FromHsv(hue * 30.0, 0.35 + (shade % 3) * 0.25, 0.42 + (shade / 3) * 0.38);
                    BlockDef def = RegisterColor(color);
                    ColorPalette.Add(def);
                }
            }
        }

        private static void AddObject(List<BlockDef> group, string id, string name, string symbol, Color color)
        {
            var def = new BlockDef { Id = id, Name = name, Hotkey = "", Symbol = symbol,
                Color = color, BorderColor = ControlPaint.Dark(color) };
            group.Add(def);
            BlockDefs[id] = def;
        }

        private static Color FromHsv(double hue, double saturation, double value)
        {
            int sector = (int)Math.Floor(hue / 60.0) % 6;
            double chroma = value * saturation;
            double second = chroma * (1.0 - Math.Abs((hue / 60.0) % 2.0 - 1.0));
            double offset = value - chroma;
            double[] rgb = sector == 0 ? new[] { chroma, second, 0.0 } :
                sector == 1 ? new[] { second, chroma, 0.0 } :
                sector == 2 ? new[] { 0.0, chroma, second } :
                sector == 3 ? new[] { 0.0, second, chroma } :
                sector == 4 ? new[] { second, 0.0, chroma } : new[] { chroma, 0.0, second };
            return Color.FromArgb((int)((rgb[0] + offset) * 255), (int)((rgb[1] + offset) * 255), (int)((rgb[2] + offset) * 255));
        }

        public static BlockDef RegisterColor(Color color)
        {
            string id = string.Format("color_{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            BlockDef existing;
            if (BlockDefs.TryGetValue(id, out existing)) return existing;
            var def = new BlockDef { Id = id, Name = string.Format("RGB #{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B),
                Hotkey = "", Color = color, BorderColor = ControlPaint.Dark(color) };
            BlockDefs[id] = def;
            return def;
        }

        public static bool EnsureColorId(string id)
        {
            if (BlockDefs.ContainsKey(id)) return true;
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^color_[0-9A-Fa-f]{6}$")) return false;
            Color color = ColorTranslator.FromHtml("#" + id.Substring(6));
            BlockDefs[id] = RegisterColor(color);
            return true;
        }
    }
}
