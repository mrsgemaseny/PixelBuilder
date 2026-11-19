using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.UI.Kit;

namespace MiniLife.WorldStudio.UI
{
    // Где лежат данные инструмента (шаблон мира, таблица автора) и настройки пользователя.
    internal static class AppPaths
    {
        public static string DataDir
        {
            get
            {
                string exeDir = Path.GetDirectoryName(typeof(AppPaths).Assembly.Location);
                string near = Path.Combine(exeDir, "data");
                if (Directory.Exists(near)) return near;
                return Path.GetFullPath(Path.Combine(Path.Combine(exeDir, ".."), "data"));
            }
        }

        public static string TemplatePath { get { return Path.Combine(DataDir, "world_template.json"); } }
        public static string ReferencePath { get { return Path.Combine(DataDir, "settlements_reference.json"); } }
        // Корень WorldStudio: папка над bin\ (exe лежит в bin), иначе папка exe.
        public static string ProjectRoot
        {
            get
            {
                string exeDir = Path.GetDirectoryName(typeof(AppPaths).Assembly.Location);
                return string.Equals(Path.GetFileName(exeDir), "bin", StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(Path.Combine(exeDir, "..")) : exeDir;
            }
        }

        public static string WorldPath { get { return Path.Combine(ProjectRoot, "world.json"); } }
        public static string SourcesPath { get { return Path.Combine(DataDir, "sources.json"); } }

        public static string SettingsDir
        {
            get { return Path.GetDirectoryName(MiniLife.WorldStudio.Application.ErrorLog.Directory); }
        }
    }

    // Последние открытые миры и выбранная тема: маленький текстовый файл в папке настроек.
    internal static class Settings
    {
        private static string RecentPath { get { return Path.Combine(AppPaths.SettingsDir, "recent.txt"); } }
        private static string ThemePath { get { return Path.Combine(AppPaths.SettingsDir, "theme.txt"); } }

        public static List<string> Recent()
        {
            var list = new List<string>();
            try
            {
                if (File.Exists(RecentPath))
                    foreach (string line in File.ReadAllLines(RecentPath))
                        if (line.Trim().Length > 0 && File.Exists(line.Trim()) && !list.Contains(line.Trim())) list.Add(line.Trim());
            }
            catch (Exception) { }
            return list.Count > 6 ? list.GetRange(0, 6) : list;
        }

        public static void AddRecent(string path)
        {
            try
            {
                List<string> list = Recent();
                list.Remove(path);
                list.Insert(0, path);
                Directory.CreateDirectory(AppPaths.SettingsDir);
                File.WriteAllLines(RecentPath, list.Count > 6 ? list.GetRange(0, 6).ToArray() : list.ToArray());
            }
            catch (Exception) { }
        }

        public static ThemeMode Theme()
        {
            try
            {
                if (File.Exists(ThemePath))
                {
                    string v = File.ReadAllText(ThemePath).Trim();
                    if (v == "light") return ThemeMode.Light;
                    if (v == "dark") return ThemeMode.Dark;
                }
            }
            catch (Exception) { }
            return ThemeMode.System;
        }

        public static void SaveTheme(ThemeMode mode)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.SettingsDir);
                File.WriteAllText(ThemePath, mode == ThemeMode.Light ? "light" : mode == ThemeMode.Dark ? "dark" : "system");
            }
            catch (Exception) { }
        }
    }

    // Подписи значений для интерфейса.
    internal static class Labels
    {
        public static string ObjectSymbol(WorldObjectType t)
        {
            switch (t)
            {
                case WorldObjectType.Castle: return "З";
                case WorldObjectType.City: return "Г";
                case WorldObjectType.Fortress: return "К";
                case WorldObjectType.Village: return "Д";
                case WorldObjectType.Village2: return "2Д";
                case WorldObjectType.Village3: return "3Д";
                case WorldObjectType.VillageMark: return "Н";
                default: return "А";
            }
        }

        public static string ObjectName(WorldObjectType t)
        {
            switch (t)
            {
                case WorldObjectType.Castle: return "Замок";
                case WorldObjectType.City: return "Город";
                case WorldObjectType.Fortress: return "Крепость";
                case WorldObjectType.Village: return "Деревня";
                case WorldObjectType.Village2: return "Две деревни";
                case WorldObjectType.Village3: return "Три деревни";
                case WorldObjectType.VillageMark: return "Независимое поселение";
                default: return "Имперская армия";
            }
        }

        public static string ObjectHint(WorldObjectType t)
        {
            switch (t)
            {
                case WorldObjectType.Fortress: return "считается городом";
                case WorldObjectType.VillageMark: return "метка Д: без лорда, не входит в таблицу автора";
                case WorldObjectType.ArmyMark: return "метка Г: армия империи, не город";
                default: return null;
            }
        }

        public static string StateKind(StateKind k)
        {
            return k == Domain.StateKind.Kingdom ? "Королевство" : k == Domain.StateKind.Duchy ? "Герцогство" : "Графство";
        }

        public static string ArmyStatus(ArmyStatus s) { return s == Domain.ArmyStatus.Roaming ? "Бродит" : "Уничтожена"; }

        public static string Wild(string id)
        {
            string tail = id.Substring(id.IndexOf('.') + 1).Replace("group_", "");
            return "Дикие земли " + tail;
        }

        public static string ArmyName(string id)
        {
            switch (id)
            {
                case "army.west_1": return "Западная армия 1";
                case "army.west_2": return "Западная армия 2";
                case "army.south": return "Южная армия";
                case "army.east": return "Восточная армия";
                case "army.girdenburg": return "Армия у Гирденбурга";
                default: return "Армия " + id.Substring(id.IndexOf('.') + 1);
            }
        }

        public static string Entity(World w, string id)
        {
            if (id == null) return "нет";
            StateEntity s = w.FindState(id);
            if (s != null) return s.Name;
            if (w.FindWildland(id) != null) return Wild(id);
            if (w.FindArmy(id) != null) return ArmyName(id);
            return id + " (нет в реестре)";
        }

        public static string Landscape(World w, string id)
        {
            if (id == null) return "не задан";
            LandscapeType t = w.Legend.FindLandscape(id);
            return t != null ? t.Name : id + " (нет в легенде)";
        }

        public static string Cell(CellCoord? c) { return c.HasValue ? c.Value.X + ", " + c.Value.Y : "не задана"; }

        public static string Plural(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            string word = m10 == 1 && m100 != 11 ? one : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? few : many;
            return n + " " + word;
        }
    }

    internal static class Dialogs
    {
        public static void Error(IWin32Window owner, string text) { UiAlert.Error(owner, "Не получилось", text); }
        public static void Info(IWin32Window owner, string text) { UiAlert.Info(owner, Strings.AppTitle, text); }

        public static bool Confirm(IWin32Window owner, string title, string text, string action, bool destructive)
        {
            return UiAlert.AskStyled(owner, title, text, destructive ? Glyph.Warning : Glyph.Info, destructive ? 0 : -1, action, "Отмена") == 0;
        }

        // Ошибка без сырого стектрейса: короткий текст пользователю, подробности - в журнал (раздел 9).
        public static void Unexpected(IWin32Window owner, string context, Exception ex)
        {
            MiniLife.WorldStudio.Application.ErrorLog.Write(context, ex);
            UiAlert.Error(owner, "Внутренняя ошибка", string.Format(Strings.UnexpectedError, MiniLife.WorldStudio.Application.ErrorLog.FilePath));
        }
    }
}
