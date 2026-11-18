using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using MiniLife.WorldStudio.Domain;

namespace MiniLife.WorldStudio.Serialization.Xlsx
{
    public sealed class XlsxRelationsResult
    {
        internal XlsxRelationsResult(RelationsMatrix matrix, string error, List<string> warnings)
        {
            Matrix = matrix;
            Error = error;
            Warnings = warnings.AsReadOnly();
        }

        public bool Ok { get { return Matrix != null; } }
        public RelationsMatrix Matrix { get; private set; }
        public string Error { get; private set; }
        public IList<string> Warnings { get; private set; }
    }

    // Импорт и экспорт таблицы отношений в .xlsx без NuGet (DECISIONS D-03): zip + SpreadsheetML.
    // Чтение терпимо к расположению таблицы: подписи государств ищутся по имени или id в любой строке и столбце
    // первого листа, порядок строк и столбцов в Excel может отличаться от реестра.
    public static class XlsxRelationsCodec
    {
        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        public const string CornerLabel = "от \\ к";

        // ---------- запись ----------

        public static byte[] Write(World world)
        {
            if (world.Relations == null) throw new DomainException("В мире нет матрицы отношений.");
            RelationsMatrix m = world.Relations;
            var names = new List<string>();
            foreach (string id in m.Order)
            {
                StateEntity s = world.FindState(id);
                names.Add(s != null ? s.Name : id);
            }

            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sheet.Append("<worksheet xmlns=\"").Append(NsMain).Append("\"><sheetData>");
            sheet.Append("<row r=\"1\">");
            InlineCell(sheet, "A1", CornerLabel);
            for (int j = 0; j < m.Size; j++) InlineCell(sheet, Ref(1, j + 2), names[j]);
            sheet.Append("</row>");
            for (int i = 0; i < m.Size; i++)
            {
                int row = i + 2;
                sheet.Append("<row r=\"").Append(row).Append("\">");
                InlineCell(sheet, Ref(row, 1), names[i]);
                for (int j = 0; j < m.Size; j++)
                {
                    int? v = m.Get(i, j);
                    if (!v.HasValue) continue; // пустая ячейка: диагональ и незаполненные значения
                    sheet.Append("<c r=\"").Append(Ref(row, j + 2)).Append("\"><v>")
                         .Append(v.Value.ToString(CultureInfo.InvariantCulture)).Append("</v></c>");
                }
                sheet.Append("</row>");
            }
            sheet.Append("</sheetData></worksheet>");

            using (var buffer = new MemoryStream())
            {
                using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                {
                    Entry(zip, "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                        "</Types>");
                    Entry(zip, "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"" + NsPkgRel + "\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                        "</Relationships>");
                    Entry(zip, "xl/workbook.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<workbook xmlns=\"" + NsMain + "\" xmlns:r=\"" + NsRel + "\">" +
                        "<sheets><sheet name=\"Отношения\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                    Entry(zip, "xl/_rels/workbook.xml.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"" + NsPkgRel + "\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                        "</Relationships>");
                    Entry(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
                }
                return buffer.ToArray();
            }
        }

        private static void InlineCell(StringBuilder sb, string reference, string text)
        {
            sb.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\"><is><t>")
              .Append(SecurityElementEscape(text)).Append("</t></is></c>");
        }

        private static string SecurityElementEscape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static void Entry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream s = entry.Open())
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                s.Write(bytes, 0, bytes.Length);
            }
        }

        // ---------- чтение ----------

        public static XlsxRelationsResult Read(byte[] data, World world)
        {
            var warnings = new List<string>();
            try
            {
                Dictionary<long, string> cells = ReadFirstSheet(data);
                return new XlsxRelationsResult(BuildMatrix(cells, world, warnings), null, warnings);
            }
            catch (InvalidDataException ex) { return new XlsxRelationsResult(null, "Файл не является xlsx (zip): " + ex.Message, warnings); }
            catch (XmlException ex) { return new XlsxRelationsResult(null, "Повреждён XML внутри xlsx: " + ex.Message, warnings); }
            catch (FormatException ex) { return new XlsxRelationsResult(null, ex.Message, warnings); }
            catch (DomainException ex) { return new XlsxRelationsResult(null, ex.Message, warnings); }
        }

        private static RelationsMatrix BuildMatrix(Dictionary<long, string> cells, World world, List<string> warnings)
        {
            var states = new List<StateEntity>(world.States);
            if (states.Count == 0) throw new FormatException("В реестре нет государств.");
            var byLabel = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (StateEntity s in states)
            {
                byLabel[Normalize(s.Name)] = s.Id;
                byLabel[Normalize(s.Id)] = s.Id;
            }

            // Ищем строку подписей (столбцы) и столбец подписей (строки): там, где совпадений больше всего.
            var rowHits = new Dictionary<int, Dictionary<string, int>>();
            var colHits = new Dictionary<int, Dictionary<string, int>>();
            foreach (var pair in cells)
            {
                string id;
                if (!byLabel.TryGetValue(Normalize(pair.Value), out id)) continue;
                int r = (int)(pair.Key >> 20), c = (int)(pair.Key & 0xFFFFF);
                AddHit(rowHits, r, id, c);
                AddHit(colHits, c, id, r);
            }
            int headerRow = Best(rowHits), labelCol = Best(colHits);
            if (headerRow < 0 || labelCol < 0) throw new FormatException("Не найдена таблица: нет строки и столбца с названиями государств.");
            Dictionary<string, int> colOf = rowHits[headerRow];
            Dictionary<string, int> rowOf = colHits[labelCol];

            var missing = new List<string>();
            foreach (StateEntity s in states)
                if (!colOf.ContainsKey(s.Id) || !rowOf.ContainsKey(s.Id)) missing.Add(s.Name);
            if (missing.Count > 0)
                throw new FormatException("В таблице нет государств: " + string.Join(", ", missing.ToArray()) + ".");

            var order = new List<string>();
            foreach (StateEntity s in states) order.Add(s.Id);
            var m = new RelationsMatrix(order);
            for (int i = 0; i < order.Count; i++)
                for (int j = 0; j < order.Count; j++)
                {
                    int r = rowOf[order[i]], c = colOf[order[j]];
                    string raw;
                    if (!cells.TryGetValue(Key(r, c), out raw) || raw.Trim().Length == 0) continue;
                    if (i == j)
                    {
                        if (raw.Trim() == "-" || raw.Trim() == "—") continue; // прочерк на диагонали
                        throw new FormatException("Ячейка " + Ref(r, c) + ": диагональ должна быть пустой, в файле \"" + raw + "\".");
                    }
                    double d;
                    if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) || d != Math.Floor(d) ||
                        d < int.MinValue || d > int.MaxValue)
                        throw new FormatException("Ячейка " + Ref(r, c) + ": ожидалось целое число, в файле \"" + raw + "\".");
                    // Диапазон не обрезается: значение вне -100..100 покажет проверка 8.
                    m.Set(i, j, (int)d);
                }
            foreach (var pair in rowHits[headerRow])
                if (pair.Value == labelCol) warnings.Add("Подпись в углу таблицы совпала с названием государства: " + pair.Key + ".");
            return m;
        }

        private static void AddHit(Dictionary<int, Dictionary<string, int>> hits, int line, string id, int pos)
        {
            Dictionary<string, int> map;
            if (!hits.TryGetValue(line, out map)) hits[line] = map = new Dictionary<string, int>();
            if (!map.ContainsKey(id)) map[id] = pos;
        }

        private static int Best(Dictionary<int, Dictionary<string, int>> hits)
        {
            int best = -1, bestCount = 1; // одной подписи мало: таблица - это строка подписей
            var keys = new List<int>(hits.Keys);
            keys.Sort();
            foreach (int k in keys)
                if (hits[k].Count > bestCount) { best = k; bestCount = hits[k].Count; }
            return best;
        }

        private static string Normalize(string s)
        {
            return (s ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
        }

        private static long Key(int row, int col) { return ((long)row << 20) | (uint)col; }

        public static string Ref(int row, int col)
        {
            var letters = new StringBuilder();
            int c = col;
            while (c > 0)
            {
                int rem = (c - 1) % 26;
                letters.Insert(0, (char)('A' + rem));
                c = (c - 1) / 26;
            }
            return letters + row.ToString(CultureInfo.InvariantCulture);
        }

        private static bool ParseRef(string reference, out int row, out int col)
        {
            row = 0; col = 0;
            int i = 0;
            while (i < reference.Length && char.IsLetter(reference[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
                i++;
            }
            return i > 0 && int.TryParse(reference.Substring(i), NumberStyles.None, CultureInfo.InvariantCulture, out row) && row > 0;
        }

        private static Dictionary<long, string> ReadFirstSheet(byte[] data)
        {
            using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
            {
                XmlDocument workbook = LoadXml(zip, "xl/workbook.xml");
                var ns = new XmlNamespaceManager(workbook.NameTable);
                ns.AddNamespace("m", NsMain);
                ns.AddNamespace("r", NsRel);
                XmlElement sheet = workbook.SelectSingleNode("/m:workbook/m:sheets/m:sheet", ns) as XmlElement;
                if (sheet == null) throw new FormatException("В книге нет листов.");
                string relId = sheet.GetAttribute("id", NsRel);

                XmlDocument rels = LoadXml(zip, "xl/_rels/workbook.xml.rels");
                var rns = new XmlNamespaceManager(rels.NameTable);
                rns.AddNamespace("p", NsPkgRel);
                string target = null;
                foreach (XmlElement rel in rels.SelectNodes("/p:Relationships/p:Relationship", rns))
                    if (rel.GetAttribute("Id") == relId) target = rel.GetAttribute("Target");
                if (target == null) throw new FormatException("Не найден файл первого листа.");
                string sheetPath = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;

                var shared = new List<string>();
                if (zip.GetEntry("xl/sharedStrings.xml") != null)
                {
                    XmlDocument sst = LoadXml(zip, "xl/sharedStrings.xml");
                    var sns = new XmlNamespaceManager(sst.NameTable);
                    sns.AddNamespace("m", NsMain);
                    foreach (XmlElement si in sst.SelectNodes("/m:sst/m:si", sns)) shared.Add(TextOf(si, sns));
                }

                XmlDocument ws = LoadXml(zip, sheetPath);
                var wns = new XmlNamespaceManager(ws.NameTable);
                wns.AddNamespace("m", NsMain);
                var cells = new Dictionary<long, string>();
                foreach (XmlElement c in ws.SelectNodes("/m:worksheet/m:sheetData/m:row/m:c", wns))
                {
                    int row, col;
                    if (!ParseRef(c.GetAttribute("r"), out row, out col)) continue;
                    string type = c.GetAttribute("t");
                    string value;
                    if (type == "inlineStr")
                    {
                        XmlElement isNode = c.SelectSingleNode("m:is", wns) as XmlElement;
                        value = isNode != null ? TextOf(isNode, wns) : "";
                    }
                    else
                    {
                        XmlNode v = c.SelectSingleNode("m:v", wns);
                        value = v != null ? v.InnerText : "";
                        if (type == "s")
                        {
                            int idx;
                            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out idx) || idx >= shared.Count)
                                throw new FormatException("Ячейка " + Ref(row, col) + ": ссылка на несуществующую строку.");
                            value = shared[idx];
                        }
                    }
                    cells[Key(row, col)] = value;
                }
                return cells;
            }
        }

        // Текст строки, включая форматированный (несколько <r><t>), без фонетики (<rPh>).
        private static string TextOf(XmlElement node, XmlNamespaceManager ns)
        {
            var sb = new StringBuilder();
            foreach (XmlNode t in node.SelectNodes("m:t | m:r/m:t", ns)) sb.Append(t.InnerText);
            return sb.ToString();
        }

        private static XmlDocument LoadXml(ZipArchive zip, string path)
        {
            ZipArchiveEntry entry = zip.GetEntry(path);
            if (entry == null) throw new FormatException("В xlsx нет части " + path + ".");
            var doc = new XmlDocument { XmlResolver = null }; // без внешних сущностей (XXE)
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (Stream s = entry.Open())
            using (XmlReader reader = XmlReader.Create(s, settings))
                doc.Load(reader);
            return doc;
        }
    }
}
