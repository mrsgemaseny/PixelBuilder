using MiniLife.WorldStudio.Domain;
using MiniLife.WorldStudio.Serialization.Json;

namespace MiniLife.WorldStudio.Serialization
{
    // data/settlements_reference.json: таблица автора для проверки 7.
    public static class ReferenceCodec
    {
        public static SettlementReference Read(string text)
        {
            var root = JsonRead.Object(JsonParser.Parse(text), "reference");
            var reference = new SettlementReference();
            reference.Source = JsonRead.OptionalString(root, "source", "reference", "");
            var rows = JsonRead.RequiredArray(root, "rows", "reference");
            for (int i = 0; i < rows.Count; i++)
            {
                string p = "reference.rows[" + i + "]";
                var o = JsonRead.Object(rows[i], p);
                string state = JsonRead.RequiredString(o, "state", p);
                if (!EntityId.IsValid(state) || EntityId.KindOf(state) != EntityId.State)
                    throw new JsonSchemaException(p + ".state: некорректный id государства \"" + state + "\".");
                if (reference.Find(state) != null) throw new JsonSchemaException(p + ".state: государство " + state + " повторяется.");
                reference.Rows.Add(new SettlementRow
                {
                    StateId = state,
                    Castles = JsonRead.RequiredInt(o, "castles", p),
                    Cities = JsonRead.RequiredInt(o, "cities", p),
                    Villages = JsonRead.RequiredInt(o, "villages", p),
                    Population = JsonRead.RequiredInt(o, "population", p)
                });
            }
            return reference;
        }
    }
}
