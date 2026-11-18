namespace MiniLife.WorldStudio.Domain
{
    // Типы объектов глобальной карты, раздел 7.
    // VillageMark - независимое поселение без лорда (метка Д файла диких земель или independent_settlement PixelBuilder).
    //   Код в world.json остаётся village_mark (D-33). В сверку с таблицей автора не входит: таблица - по государствам.
    // ArmyMark - имперская армия (метка Г файла диких земель или imperial_army PixelBuilder), не город.
    public enum WorldObjectType { Castle, City, Fortress, Village, Village2, Village3, VillageMark, ArmyMark }

    public sealed class WorldObject
    {
        public WorldObject(string id, CellCoord cell, WorldObjectType type)
        {
            EntityId.Require(id, EntityId.Object);
            Id = id;
            Cell = cell;
            Type = type;
            Notes = "";
        }

        public string Id { get; private set; }
        public CellCoord Cell { get; set; }
        public WorldObjectType Type { get; set; }
        // state.* или wild.*; для ArmyMark - army.*; null если владелец не задан.
        public string Owner { get; set; }
        public string Notes { get; set; }
    }

    public static class WorldObjectTypes
    {
        private static readonly string[] Codes =
            { "castle", "city", "fortress", "village", "village_2", "village_3", "village_mark", "army_mark" };

        public static string ToCode(WorldObjectType type)
        {
            return Codes[(int)type];
        }

        public static bool TryParse(string code, out WorldObjectType type)
        {
            type = WorldObjectType.Castle;
            for (int i = 0; i < Codes.Length; i++)
            {
                if (Codes[i] == code)
                {
                    type = (WorldObjectType)i;
                    return true;
                }
            }
            return false;
        }

        // Сколько поселений даёт объект при сверке с таблицей автора (DECISIONS D-07).
        // Метки не считаются поселениями (антипаттерн 3).
        public static int VillageMultiplicity(WorldObjectType type)
        {
            switch (type)
            {
                case WorldObjectType.Village: return 1;
                case WorldObjectType.Village2: return 2;
                case WorldObjectType.Village3: return 3;
                default: return 0;
            }
        }

        // Крепость считается городом (раздел 5.3).
        public static bool IsCityLike(WorldObjectType type)
        {
            return type == WorldObjectType.City || type == WorldObjectType.Fortress;
        }

        public static bool IsMark(WorldObjectType type)
        {
            return type == WorldObjectType.VillageMark || type == WorldObjectType.ArmyMark;
        }
    }
}
