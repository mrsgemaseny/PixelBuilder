using System.Collections.Generic;
using MiniLife.WorldStudio.Serialization.Json;

namespace MiniLife.WorldStudio.Serialization.WorldJson
{
    // Миграция схемы world.json с версии From на From + 1 (правило 4: никакого тихого изменения формата).
    // Работает на дереве JSON до построения мира: так миграция видит старую структуру как есть.
    public interface IWorldMigration
    {
        int From { get; }
        string Description { get; }
        void Apply(JsonObject root);
    }

    public static class WorldMigrations
    {
        // Схема 1 - первая; миграций пока нет. Новая версия схемы = новый класс здесь + тест на старый файл.
        public static readonly IList<IWorldMigration> All = new List<IWorldMigration>().AsReadOnly();

        public static void Upgrade(JsonObject root, int version, IList<IWorldMigration> migrations)
        {
            while (version < MiniLife.WorldStudio.Domain.World.CurrentSchemaVersion)
            {
                IWorldMigration step = null;
                foreach (IWorldMigration m in migrations)
                    if (m.From == version) step = m;
                if (step == null)
                    throw new JsonSchemaException("Нет миграции схемы world.json с версии " + version + " на " + (version + 1) + ".");
                step.Apply(root);
                version++;
                root.Set("schema_version", JsonNumber.FromInt(version));
            }
        }
    }
}
