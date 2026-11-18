using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MiniLife.WorldStudio.Application
{
    public sealed class StoredVersion
    {
        public string Path { get; internal set; }
        public DateTime TimestampUtc { get; internal set; }
        public long Size { get; internal set; }
        public string Reason { get; internal set; }
    }

    // Резервные копии и журнал изменений (M6, Этап 5).
    // Рядом с world.json папка "<имя>.history": копия файла перед каждой перезаписью и перед откатом,
    // journal.log - строка на каждое сохранение, копию и откат. Старые копии сами не удаляются (правило 2):
    // чистка истории - решение автора (future consideration: кнопка с подтверждением).
    public sealed class VersionStore
    {
        private readonly Func<DateTime> _clock;

        public VersionStore() : this(() => DateTime.UtcNow) { }

        public VersionStore(Func<DateTime> clock)
        {
            _clock = clock;
        }

        public static string HistoryDir(string worldPath)
        {
            string full = System.IO.Path.GetFullPath(worldPath);
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full), System.IO.Path.GetFileNameWithoutExtension(full) + ".history");
        }

        public static string AutosavePath(string worldPath)
        {
            string full = System.IO.Path.GetFullPath(worldPath);
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full), System.IO.Path.GetFileNameWithoutExtension(full) + ".autosave.json");
        }

        // Сохранение с резервной копией прежней версии. Если содержимое не изменилось, копия не делается.
        public void Save(string worldPath, string text, string reason)
        {
            if (File.Exists(worldPath))
            {
                string current = TextFileStore.ReadAllText(worldPath);
                if (current == text) { Journal(worldPath, "save-unchanged", worldPath, text, reason); return; }
                Backup(worldPath, current, reason);
            }
            TextFileStore.WriteAllTextAtomic(worldPath, text);
            Journal(worldPath, "save", worldPath, text, reason);
        }

        // Двоичный файл (xlsx): прежняя версия копируется в историю как есть.
        public void SaveBytes(string path, byte[] data, string reason)
        {
            string digest = Sha256(data);
            if (File.Exists(path))
            {
                byte[] current = File.ReadAllBytes(path);
                if (Sha256(current) == digest) { JournalLine(path, "save-unchanged", path, digest, reason); return; }
                string dir = HistoryDir(path);
                Directory.CreateDirectory(dir);
                string copy = System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(path) + "." +
                    _clock().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + System.IO.Path.GetExtension(path));
                File.WriteAllBytes(copy, current);
                JournalLine(path, "backup", copy, Sha256(current), reason);
            }
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, data);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            JournalLine(path, "save", path, digest, reason);
        }

        public string Backup(string worldPath, string content, string reason)
        {
            string dir = HistoryDir(worldPath);
            Directory.CreateDirectory(dir);
            string name = System.IO.Path.GetFileNameWithoutExtension(worldPath);
            DateTime now = _clock();
            string path;
            int n = 0;
            do
            {
                string stamp = now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + (n == 0 ? "" : "-" + n);
                path = System.IO.Path.Combine(dir, name + "." + stamp + ".json");
                n++;
            } while (File.Exists(path));
            TextFileStore.WriteAllTextAtomic(path, content);
            Journal(worldPath, "backup", path, content, reason);
            return path;
        }

        public List<StoredVersion> List(string worldPath)
        {
            var result = new List<StoredVersion>();
            string dir = HistoryDir(worldPath);
            if (!Directory.Exists(dir)) return result;
            var reasons = ReadReasons(dir);
            string prefix = System.IO.Path.GetFileNameWithoutExtension(worldPath) + ".";
            foreach (string file in Directory.GetFiles(dir, prefix + "*.json"))
            {
                string stamp = System.IO.Path.GetFileNameWithoutExtension(file).Substring(prefix.Length);
                DateTime ts;
                if (!DateTime.TryParseExact(stamp.Length >= 19 ? stamp.Substring(0, 19) : stamp, "yyyyMMdd-HHmmss-fff",
                        CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out ts)) continue;
                string reason;
                reasons.TryGetValue(System.IO.Path.GetFileName(file), out reason);
                result.Add(new StoredVersion { Path = file, TimestampUtc = ts, Size = new FileInfo(file).Length, Reason = reason ?? "" });
            }
            result.Sort((a, b) => b.TimestampUtc != a.TimestampUtc ? b.TimestampUtc.CompareTo(a.TimestampUtc) : string.CompareOrdinal(b.Path, a.Path));
            return result;
        }

        // Откат: текущий файл сначала копируется в историю, затем заменяется выбранной версией.
        public void Restore(string worldPath, StoredVersion version)
        {
            string content = TextFileStore.ReadAllText(version.Path);
            if (File.Exists(worldPath)) Backup(worldPath, TextFileStore.ReadAllText(worldPath), "перед откатом к " + System.IO.Path.GetFileName(version.Path));
            TextFileStore.WriteAllTextAtomic(worldPath, content);
            Journal(worldPath, "restore", version.Path, content, "");
        }

        private void Journal(string worldPath, string action, string file, string content, string reason)
        {
            JournalLine(worldPath, action, file, Sha256(content), reason);
        }

        private void JournalLine(string worldPath, string action, string file, string digest, string reason)
        {
            string dir = HistoryDir(worldPath);
            Directory.CreateDirectory(dir);
            string line = _clock().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) + "\t" + action + "\t" +
                          System.IO.Path.GetFileName(file) + "\t" + digest + "\t" + (reason ?? "").Replace('\t', ' ').Replace('\n', ' ') + "\n";
            File.AppendAllText(System.IO.Path.Combine(dir, "journal.log"), line, new UTF8Encoding(false));
        }

        private static Dictionary<string, string> ReadReasons(string dir)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string journal = System.IO.Path.Combine(dir, "journal.log");
            if (!File.Exists(journal)) return map;
            foreach (string line in File.ReadAllLines(journal, Encoding.UTF8))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 5 && parts[1] == "backup") map[parts[2]] = parts[4];
            }
            return map;
        }

        public static string Sha256(string text)
        {
            return Sha256(new UTF8Encoding(false).GetBytes(text));
        }

        public static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                var sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }
}
