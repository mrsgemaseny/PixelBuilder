using System.Collections.Generic;
using MiniLife.WorldStudio.Serialization.Json;

namespace MiniLife.WorldStudio.Serialization.PixelArchitect
{
    // Сравнение двух документов по смыслу (DECISIONS D-09): поля, слои, клетки, значения, порядок.
    // Байты не сравниваются: переводы строк и пустые объекты могут быть записаны иначе.
    public static class PixelArchitectComparer
    {
        public static List<string> Diff(PixelArchitectDocument a, PixelArchitectDocument b)
        {
            var diff = new List<string>();
            if (a.MapKind != b.MapKind) diff.Add("map_kind: " + a.MapKind + " / " + b.MapKind);
            if (a.Width != b.Width || a.Height != b.Height)
                diff.Add("map_size: " + a.Width + "x" + a.Height + " / " + b.Width + "x" + b.Height);
            if (a.DeclaredTotalBlocks != b.DeclaredTotalBlocks)
                diff.Add("total_blocks: " + a.DeclaredTotalBlocks + " / " + b.DeclaredTotalBlocks);

            if (a.HasLayersField != b.HasLayersField) diff.Add("поле layers: есть только в одном документе");
            if (a.Layers.Count != b.Layers.Count) diff.Add("число слоёв: " + a.Layers.Count + " / " + b.Layers.Count);
            for (int i = 0; i < a.Layers.Count && i < b.Layers.Count; i++)
            {
                if (a.Layers[i].Key != b.Layers[i].Key)
                    diff.Add("слой #" + i + ": " + a.Layers[i].Key + " / " + b.Layers[i].Key);
                else
                    DiffLayer("layers." + a.Layers[i].Key, a.Layers[i].Value, b.Layers[i].Value, diff);
            }

            if ((a.Blocks == null) != (b.Blocks == null)) diff.Add("blocks: есть только в одном документе");
            else if (a.Blocks != null) DiffLayer("blocks", a.Blocks, b.Blocks, diff);

            var namesA = new List<string>(a.RawFieldNames);
            var namesB = new List<string>(b.RawFieldNames);
            if (string.Join(",", namesA.ToArray()) != string.Join(",", namesB.ToArray()))
                diff.Add("прочие поля: [" + string.Join(",", namesA.ToArray()) + "] / [" + string.Join(",", namesB.ToArray()) + "]");
            else
                foreach (string name in namesA)
                    if (!a.GetRawField(name).DeepEquals(b.GetRawField(name))) diff.Add("поле " + name + " отличается");

            // Порядок полей в корне тоже часть "без потерь".
            var orderA = KeyOrder(PixelArchitectCodec.ToJson(a));
            var orderB = KeyOrder(PixelArchitectCodec.ToJson(b));
            if (orderA != orderB) diff.Add("порядок полей: " + orderA + " / " + orderB);
            return diff;
        }

        private static void DiffLayer(string name, PixelLayer a, PixelLayer b, List<string> diff)
        {
            if (a.Count != b.Count)
            {
                diff.Add(name + ": клеток " + a.Count + " / " + b.Count);
                return;
            }
            for (int i = 0; i < a.Count; i++)
            {
                PixelCell ca = a.Cells[i], cb = b.Cells[i];
                if (ca.Key != cb.Key || ca.Value != cb.Value)
                {
                    diff.Add(name + "[" + i + "]: " + ca.Key + "=" + ca.Value + " / " + cb.Key + "=" + cb.Value);
                    return; // одной первой разницы достаточно, дальше обычно каскад
                }
            }
        }

        private static string KeyOrder(JsonObject obj)
        {
            var keys = new List<string>();
            foreach (var member in obj.Members) keys.Add(member.Key);
            return string.Join(",", keys.ToArray());
        }
    }
}
