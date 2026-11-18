using System.Collections.Generic;

namespace MiniLife.WorldStudio.Domain
{
    public enum StateKind { Kingdom, Duchy, County }

    public enum ArmyStatus { Roaming, Destroyed }

    // Государство. Правитель пока заглушка "имя" (решение автора, антипаттерн 7).
    public sealed class StateEntity
    {
        public const string RulerPlaceholderDefault = "имя";

        public StateEntity(string id, string name, StateKind kind, RgbColor color)
        {
            EntityId.Require(id, EntityId.State);
            Id = id;
            Name = name ?? "";
            Kind = kind;
            Color = color;
            RulerPlaceholder = RulerPlaceholderDefault;
            Notes = "";
        }

        public string Id { get; private set; }
        public string Name { get; set; }
        public StateKind Kind { get; set; }
        public RgbColor Color { get; set; }
        public string RulerPlaceholder { get; set; }
        public string Notes { get; set; }
    }

    // Группа диких земель. Без правителя: независимые поселения, бандиты, лагеря.
    public sealed class WildlandEntity
    {
        public WildlandEntity(string id, RgbColor color)
        {
            EntityId.Require(id, EntityId.Wildland);
            Id = id;
            Color = color;
            Notes = "";
        }

        public string Id { get; private set; }
        public RgbColor Color { get; set; }
        public string Notes { get; set; }
    }

    // Имперская армия. Origin и Position допускают null: точных клеток у автора нет (OPEN_ITEMS I-04).
    public sealed class ArmyEntity
    {
        private readonly List<CellCoord> _route = new List<CellCoord>();

        public ArmyEntity(string id, ArmyStatus status)
        {
            EntityId.Require(id, EntityId.Army);
            Id = id;
            Status = status;
            Notes = "";
        }

        public string Id { get; private set; }
        public ArmyStatus Status { get; set; }
        public CellCoord? Origin { get; set; }
        public CellCoord? Position { get; set; }
        public List<CellCoord> Route { get { return _route; } }
        public string Notes { get; set; }
    }

    public static class EntityKinds
    {
        public static string ToCode(StateKind kind)
        {
            switch (kind)
            {
                case StateKind.Kingdom: return "kingdom";
                case StateKind.Duchy: return "duchy";
                default: return "county";
            }
        }

        public static bool TryParseStateKind(string code, out StateKind kind)
        {
            kind = StateKind.Kingdom;
            if (code == "kingdom") { kind = StateKind.Kingdom; return true; }
            if (code == "duchy") { kind = StateKind.Duchy; return true; }
            if (code == "county") { kind = StateKind.County; return true; }
            return false;
        }

        public static string ToCode(ArmyStatus status)
        {
            return status == ArmyStatus.Roaming ? "roaming" : "destroyed";
        }

        public static bool TryParseArmyStatus(string code, out ArmyStatus status)
        {
            status = ArmyStatus.Roaming;
            if (code == "roaming") { status = ArmyStatus.Roaming; return true; }
            if (code == "destroyed") { status = ArmyStatus.Destroyed; return true; }
            return false;
        }
    }
}
