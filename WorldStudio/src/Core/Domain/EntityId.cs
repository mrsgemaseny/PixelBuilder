using System.Text.RegularExpressions;

namespace MiniLife.WorldStudio.Domain
{
    // Стабильный id вида "<вид>.<имя>", например "state.reinhard", "army.west_1".
    // Только латиница в нижнем регистре, цифры и "_": id попадают в ключи JSON и в diff git.
    public static class EntityId
    {
        public const string State = "state";
        public const string Wildland = "wild";
        public const string Army = "army";
        public const string Object = "obj";

        private static readonly Regex Pattern = new Regex("^[a-z][a-z0-9]*\\.[a-z0-9][a-z0-9_]*$", RegexOptions.CultureInvariant);

        public static bool IsValid(string id)
        {
            return id != null && id.Length <= 64 && Pattern.IsMatch(id);
        }

        public static string KindOf(string id)
        {
            if (!IsValid(id)) return null;
            return id.Substring(0, id.IndexOf('.'));
        }

        public static void Require(string id, string expectedKind)
        {
            if (!IsValid(id)) throw new DomainException("Некорректный id: \"" + (id ?? "null") + "\".");
            if (expectedKind != null && KindOf(id) != expectedKind)
                throw new DomainException("id \"" + id + "\" должен начинаться с \"" + expectedKind + ".\".");
        }
    }
}
