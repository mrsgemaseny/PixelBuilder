using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization;
using MiniLife.WorldStudio.Serialization.PixelArchitect;
using MiniLife.WorldStudio.Serialization.WorldJson;
using MiniLife.WorldStudio.Serialization.Xlsx;
using MiniLife.WorldStudio.Validation;

namespace MiniLife.WorldStudio.Application
{
    // Открытый в редакторе мир: данные, история правок, путь, несохранённые изменения.
    // UI обращается к миру только через этот класс; все правки идут командами.
    public sealed class WorldDocument
    {
        public const string ToolVersion = "WorldStudio 1.0";

        private readonly CommandHistory _history = new CommandHistory();
        private readonly VersionStore _versions;
        private readonly Func<DateTime> _clock;
        private long _savedRevision;
        private long _autosavedRevision;
        private readonly List<LegacySource> _importSources = new List<LegacySource>();

        public WorldDocument(World world, string path, Func<DateTime> clock)
        {
            if (world == null) throw new ArgumentNullException("world");
            World = world;
            FilePath = path;
            _clock = clock ?? (() => DateTime.UtcNow);
            _versions = new VersionStore(_clock);
            _history.Changed += () => { if (Changed != null) Changed(); };
        }

        public event Action Changed;

        public World World { get; private set; }
        public string FilePath { get; private set; }
        public CommandHistory History { get { return _history; } }
        public VersionStore Versions { get { return _versions; } }
        public bool IsDirty { get { return _history.Revision != _savedRevision || FilePath == null; } }
        public IList<LegacySource> ImportSources { get { return _importSources.AsReadOnly(); } }
        public IList<Finding> ImportIssues { get; private set; }
        public SettlementReference Reference { get; set; }

        // Правка. Ошибка домена не меняет мир и возвращается текстом для показа пользователю.
        public string Execute(IWorldCommand command)
        {
            if (command == null) return null;
            try
            {
                _history.Execute(World, command);
                return null;
            }
            catch (DomainException ex)
            {
                return ex.Message;
            }
        }

        public bool Undo() { return _history.Undo(World); }
        public bool Redo() { return _history.Redo(World); }

        public List<Finding> Validate()
        {
            var input = new ValidationInput(World) { Reference = Reference };
            foreach (LegacySource s in _importSources) input.LegacySources.Add(s);
            List<Finding> findings = WorldValidator.Validate(input);
            if (ImportIssues != null) findings.InsertRange(0, ImportIssues);
            return findings;
        }

        // ---------- создание и загрузка ----------

        public static WorldDocument FromImport(string templateText, IList<LegacySource> sources, Func<DateTime> clock, out string error)
        {
            error = null;
            WorldReadResult template = WorldJsonCodec.Read(templateText);
            if (!template.Ok) { error = "Шаблон мира повреждён: " + template.Error; return null; }
            LegacyImportResult imported = LegacyWorldImporter.Import(template.World, sources);
            if (!imported.Ok) { error = imported.Error; return null; }
            var doc = new WorldDocument(imported.World, null, clock);
            string now = Stamp(doc._clock());
            doc.World.Meta.CreatedAt = now;
            doc.World.Meta.UpdatedAt = now;
            doc.World.Meta.ToolVersion = ToolVersion;
            doc._importSources.AddRange(sources);
            doc.ImportIssues = imported.Issues;
            return doc;
        }

        public static WorldDocument Open(string path, Func<DateTime> clock, out string error)
        {
            error = null;
            string text;
            try { text = TextFileStore.ReadAllText(path); }
            catch (IOException ex) { error = "Не удалось прочитать файл: " + ex.Message; return null; }
            catch (UnauthorizedAccessException ex) { error = "Нет доступа к файлу: " + ex.Message; return null; }
            catch (System.Text.DecoderFallbackException) { error = "Файл не в кодировке UTF-8."; return null; }
            WorldReadResult read = WorldJsonCodec.Read(text);
            if (!read.Ok) { error = read.Error; return null; }
            var doc = new WorldDocument(read.World, Path.GetFullPath(path), clock);
            if (read.MigratedFromVersion > 0) doc._savedRevision = -1; // мигрированный файл нужно сохранить
            return doc;
        }

        // ---------- сохранение ----------

        // Сохранение в три шага, чтобы UI писал файл в фоне (раздел 9):
        // Prepare (поток UI: снимок текста) -> Write (любой поток: резервная копия и атомарная запись) -> Commit (поток UI).
        // meta.updated_at обновляется только при сохранении, поэтому два экспорта без сохранения дают одинаковые байты.
        public sealed class SavePlan
        {
            internal SavePlan() { }
            public string Path { get; internal set; }
            public string Text { get; internal set; }
            internal long Revision { get; set; }
            internal string PreviousUpdatedAt { get; set; }
        }

        public SavePlan PrepareSave(string path)
        {
            var plan = new SavePlan { Path = Path.GetFullPath(path), Revision = _history.Revision, PreviousUpdatedAt = World.Meta.UpdatedAt };
            World.Meta.UpdatedAt = Stamp(_clock());
            World.Meta.ToolVersion = ToolVersion;
            plan.Text = WorldJsonCodec.Write(World);
            return plan;
        }

        public void WriteSave(SavePlan plan)
        {
            _versions.Save(plan.Path, plan.Text, "сохранение");
        }

        public void CommitSave(SavePlan plan, bool succeeded)
        {
            if (!succeeded)
            {
                World.Meta.UpdatedAt = plan.PreviousUpdatedAt;
                return;
            }
            FilePath = plan.Path;
            _savedRevision = plan.Revision;
            _autosavedRevision = plan.Revision;
            // Мир сохранён в собственном формате: проверки по старым файлам больше не относятся к нему.
            _importSources.Clear();
            ImportIssues = null;
            if (Changed != null) Changed();
        }

        public void SaveAs(string path)
        {
            SavePlan plan = PrepareSave(path);
            try
            {
                WriteSave(plan);
            }
            catch
            {
                CommitSave(plan, false);
                throw;
            }
            CommitSave(plan, true);
        }

        public void Save()
        {
            if (FilePath == null) throw new InvalidOperationException("Путь не задан: используйте SaveAs.");
            SaveAs(FilePath);
        }

        // Автосохранение в отдельный файл рядом (<имя>.autosave.json): основной файл не трогается.
        // Для нового мира без пути - в папку данных инструмента. null, если с прошлого раза правок не было.
        public SavePlan PrepareAutosave()
        {
            if (_history.Revision == _autosavedRevision && FilePath != null) return null;
            string path = FilePath != null ? VersionStore.AutosavePath(FilePath)
                                           : Path.Combine(Path.GetDirectoryName(ErrorLog.Directory), "untitled.autosave.json");
            return new SavePlan { Path = Path.GetFullPath(path), Text = WorldJsonCodec.Write(World), Revision = _history.Revision };
        }

        public static void WriteAutosave(SavePlan plan)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(plan.Path));
            TextFileStore.WriteAllTextAtomic(plan.Path, plan.Text);
        }

        public void CommitAutosave(SavePlan plan) { _autosavedRevision = plan.Revision; }

        public string Autosave()
        {
            SavePlan plan = PrepareAutosave();
            if (plan == null) return null;
            WriteAutosave(plan);
            CommitAutosave(plan);
            return plan.Path;
        }

        // Произвольный файл (экспорт в старый формат и т.п.): копия прежнего содержимого в историю, затем атомарная запись.
        public void WriteExternalText(string path, string text, string reason)
        {
            _versions.Save(Path.GetFullPath(path), text, reason);
        }

        public void WriteExternalBytes(string path, byte[] data, string reason)
        {
            _versions.SaveBytes(Path.GetFullPath(path), data, reason);
        }

        public string ExportWorldJson() { return WorldJsonCodec.Write(World); }

        public PixelArchitectDocument ExportLegacy(MapSemantic semantic, List<string> warnings)
        {
            return LegacyWorldExporter.Export(World, semantic, _clock().ToString("s", CultureInfo.InvariantCulture), warnings);
        }

        public byte[] ExportRelationsXlsx() { return XlsxRelationsCodec.Write(World); }

        public string ImportRelationsXlsx(byte[] data, List<string> warnings)
        {
            XlsxRelationsResult r = XlsxRelationsCodec.Read(data, World);
            if (!r.Ok) return r.Error;
            if (warnings != null) warnings.AddRange(r.Warnings);
            return Execute(WorldEdits.ReplaceRelations(r.Matrix));
        }

        public static string Stamp(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }
}
