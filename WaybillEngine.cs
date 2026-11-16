using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace GovPortalsApp
{
    public class WaybillRule
    {
        public string Keywords { get; set; }
        public string TargetName { get; set; }
        public bool IsWildcard { get; set; }
    }

    public class WbSourceItem
    {
        public string SourceFile { get; set; }
        public string SupplyId { get; set; }
        public string Sticker { get; set; }
        public string Article { get; set; }
        public string RawName { get; set; }
        public string NormalizedName { get; set; }
        public decimal Price { get; set; }
        public int Qty { get; set; }
    }

    public class GroupedWaybillItem
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalNoVat { get; set; }
        public string VatRateLabel { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalWithVat { get; set; }
    }

    public class WaybillBuildResult
    {
        public List<string> InputFiles { get; set; }
        public List<string> SupplyIds { get; set; }
        public List<WbSourceItem> SourceItems { get; set; }
        public List<GroupedWaybillItem> GroupedItems { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalNoVat { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalWithVat { get; set; }

        public WaybillBuildResult()
        {
            InputFiles = new List<string>();
            SupplyIds = new List<string>();
            SourceItems = new List<WbSourceItem>();
            GroupedItems = new List<GroupedWaybillItem>();
        }
    }

    public class WaybillProfile
    {
        public string SenderUnp { get; set; }
        public string SenderInfo { get; set; }
        public string Signer { get; set; }
        public string ReceiverPreset { get; set; }
        public string ReceiverUnp { get; set; }
        public string ReceiverInfo { get; set; }
        public string BasisPrefix { get; set; }
        public string UnitName { get; set; }
        public string VatMode { get; set; }
        public bool UseNormalizationRules { get; set; }
        public bool SortDescendingByName { get; set; }
        public string RulesText { get; set; }
        public string OutputFolder { get; set; }
        public bool CreatePickingList { get; set; }

        public WaybillProfile()
        {
            SenderUnp = "";
            SenderInfo = "";
            Signer = "";
            ReceiverPreset = "WB";
            ReceiverUnp = "193630204";
            ReceiverInfo = "Общество с ограниченной ответственностью \"ИМВБРБ\", 220036, г. Минск, ул. Грушевская 21, помещение 217";
            BasisPrefix = "оферта, акц. 01.02.2023";
            UnitName = "шт";
            VatMode = "Без НДС";
            UseNormalizationRules = true;
            SortDescendingByName = true;
            RulesText = "ручк; pen => Ручка шариковая\r\n* => Пленка защитная";
            OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Buhly_ТН");
            CreatePickingList = false;
        }
    }

    public static class WaybillEngine
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace XmlNs = XNamespace.Xml;

        private static readonly string[] GenitiveMonthsRu = new string[]
        {
            "", "января", "февраля", "марта", "апреля", "мая", "июня",
            "июля", "августа", "сентября", "октября", "ноября", "декабря"
        };

        public static string GetProfilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "waybill_profile.xml");
        }

        public static WaybillProfile LoadProfile()
        {
            try
            {
                var path = GetProfilePath();
                if (File.Exists(path))
                {
                    var ser = new XmlSerializer(typeof(WaybillProfile));
                    using (var fs = File.OpenRead(path))
                    {
                        var loaded = ser.Deserialize(fs) as WaybillProfile;
                        if (loaded != null)
                        {
                            if (string.IsNullOrEmpty(loaded.OutputFolder))
                                loaded.OutputFolder = new WaybillProfile().OutputFolder;
                            if (string.IsNullOrEmpty(loaded.UnitName))
                                loaded.UnitName = "шт";
                            if (string.IsNullOrEmpty(loaded.VatMode))
                                loaded.VatMode = "Без НДС";
                            return loaded;
                        }
                    }
                }
            }
            catch { }
            return new WaybillProfile();
        }

        public static void SaveProfile(WaybillProfile profile)
        {
            if (profile == null) return;
            try
            {
                var path = GetProfilePath();
                var ser = new XmlSerializer(typeof(WaybillProfile));
                using (var fs = File.Create(path))
                {
                    ser.Serialize(fs, profile);
                }
            }
            catch { }
        }

        public static void ApplyWbPreset(WaybillProfile p)
        {
            if (p == null) return;
            p.ReceiverPreset = "WB";
            p.ReceiverUnp = "193630204";
            p.ReceiverInfo = "Общество с ограниченной ответственностью \"ИМВБРБ\", 220036, г. Минск, ул. Грушевская 21, помещение 217";
            if (string.IsNullOrEmpty(p.BasisPrefix))
                p.BasisPrefix = "оферта, акц. 01.02.2023";
        }

        public static bool TryExtractFromCertStore(out string unp, out string orgName, out string signerLine)
        {
            unp = "";
            orgName = "";
            signerLine = "";
            try
            {
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                X509Certificate2 bestCert = null;
                foreach (var cert in store.Certificates)
                {
                    var issuer = cert.Issuer ?? "";
                    if (!(issuer.Contains("ГосСУОК") || issuer.Contains("pki.gov.by") || issuer.Contains("НЦЭУ")))
                        continue;
                    if (cert.NotAfter <= DateTime.Now) continue;

                    bool inUsb = CryptoHelper.IsTokenPhysicallyInserted(cert);
                    if (inUsb)
                    {
                        bestCert = cert;
                        break;
                    }
                    if (bestCert == null)
                        bestCert = cert;
                }

                if (bestCert == null) return false;

                var subj = bestCert.SubjectName != null ? (bestCert.SubjectName.Name ?? "") : "";
                var mUnp = Regex.Match(subj, @"(?:1\.2\.112\.1\.2\.1\.1\.1\.1\.2\s*=\s*|УНП\s*[:=]?\s*)(\d{9})", RegexOptions.IgnoreCase);
                if (!mUnp.Success)
                {
                    var mAny9 = Regex.Match(subj, @"\b([1-9]\d{8})\b");
                    if (mAny9.Success) unp = mAny9.Groups[1].Value;
                }
                else
                {
                    unp = mUnp.Groups[1].Value;
                }

                orgName = ExtractDnField(subj, "O");
                string sn = ExtractDnField(subj, "SN");
                string gn = ExtractDnField(subj, "41");
                string title = ExtractDnField(subj, "T");
                string fullPerson = (sn + " " + gn).Trim();
                if (!string.IsNullOrEmpty(fullPerson))
                {
                    signerLine = string.IsNullOrEmpty(title) ? fullPerson : (title + " " + fullPerson);
                }

                if (!string.IsNullOrEmpty(unp))
                {
                    var grp = UnpChecker.Lookup(unp);
                    if (grp != null && grp.Success)
                    {
                        string namePart = !string.IsNullOrEmpty(grp.ShortName) ? grp.ShortName : grp.FullName;
                        if (!string.IsNullOrEmpty(namePart) && !string.IsNullOrEmpty(grp.Address))
                            orgName = namePart + ", " + grp.Address;
                        else if (!string.IsNullOrEmpty(namePart))
                            orgName = namePart;
                    }
                }

                return !string.IsNullOrEmpty(unp) || !string.IsNullOrEmpty(orgName) || !string.IsNullOrEmpty(signerLine);
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractDnField(string dn, string key)
        {
            if (string.IsNullOrEmpty(dn)) return "";
            var m = Regex.Match(dn, @"(?:^|,\s*)" + Regex.Escape(key) + @"=(""(?:[^""]|"""")*""|[^,]+)");
            if (!m.Success) return "";
            return m.Groups[1].Value.Trim().Trim('"').Replace("\"\"", "\"");
        }

        public static List<WaybillRule> ParseRules(string rulesText)
        {
            var rules = new List<WaybillRule>();
            if (string.IsNullOrEmpty(rulesText)) return rules;
            var lines = rulesText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                int arrowIdx = line.IndexOf("=>", StringComparison.Ordinal);
                if (arrowIdx < 0) continue;
                string left = line.Substring(0, arrowIdx).Trim();
                string right = line.Substring(arrowIdx + 2).Trim();
                if (string.IsNullOrEmpty(right)) continue;
                rules.Add(new WaybillRule
                {
                    Keywords = left,
                    TargetName = right,
                    IsWildcard = (left == "*" || string.Equals(left, "иначе", StringComparison.OrdinalIgnoreCase))
                });
            }
            return rules;
        }

        public static string NormalizeItemName(string rawName, List<WaybillRule> rules, bool applyRules)
        {
            string clean = (rawName ?? "").Trim();
            if (!applyRules || rules == null || rules.Count == 0)
                return clean;

            string lower = clean.ToLowerInvariant();
            string wildcardTarget = null;
            foreach (var rule in rules)
            {
                if (rule.IsWildcard)
                {
                    if (wildcardTarget == null) wildcardTarget = rule.TargetName;
                    continue;
                }
                var kws = (rule.Keywords ?? "").Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var kw in kws)
                {
                    string k = kw.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(k) && lower.Contains(k))
                        return rule.TargetName;
                }
            }
            return wildcardTarget != null ? wildcardTarget : clean;
        }

        public static decimal ParseVatRate(string vatMode)
        {
            if (string.IsNullOrEmpty(vatMode)) return 0m;
            if (vatMode.Contains("20")) return 0.20m;
            if (vatMode.Contains("10")) return 0.10m;
            return 0m;
        }

        public static WaybillBuildResult ParseSupplyExcelFiles(IEnumerable<string> paths, WaybillProfile profile)
        {
            var result = new WaybillBuildResult();
            if (paths == null) return result;
            var rules = ParseRules(profile != null ? profile.RulesText : "");
            bool applyRules = profile == null || profile.UseNormalizationRules;
            var supplySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in paths)
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) continue;
                result.InputFiles.Add(file);

                string fileNameNoExt = Path.GetFileNameWithoutExtension(file) ?? "";
                string fallbackSupplyId = ExtractSupplyNumber(fileNameNoExt);
                if (string.IsNullOrEmpty(fallbackSupplyId))
                    fallbackSupplyId = fileNameNoExt.ToUpperInvariant();

                var rows = ReadFirstSheetRows(file);
                if (rows.Count == 0) continue;

                int headerIdx = -1;
                int colName = -1, colPrice = -1, colSupply = -1, colSticker = -1, colArticle = -1, colQty = -1;

                int scanLimit = Math.Min(10, rows.Count);
                for (int r = 0; r < scanLimit; r++)
                {
                    var row = rows[r];
                    int foundName = -1, foundPrice = -1;
                    foreach (var kv in row)
                    {
                        string h = (kv.Value ?? "").Trim().ToLowerInvariant();
                        if (foundName < 0 && (h == "наименование" || h.Contains("наименование товара") || h == "название" || h == "товар"))
                            foundName = kv.Key;
                        if (foundPrice < 0 && (h.Contains("стоимость") || h == "цена" || h.StartsWith("цена ") || h.StartsWith("цена,")))
                            foundPrice = kv.Key;
                        if (colSupply < 0 && (h.Contains("qr-код поставки") || h.Contains("номер поставки") || h == "поставка" || h.Contains("№ поставки")))
                            colSupply = kv.Key;
                        if (colSticker < 0 && (h == "стикер" || h.Contains("стикер")))
                            colSticker = kv.Key;
                        if (colArticle < 0 && (h.Contains("артикул продавца") || h == "артикул"))
                            colArticle = kv.Key;
                        if (colQty < 0 && (h == "количество" || h == "кол-во" || h == "шт"))
                            colQty = kv.Key;
                    }
                    if (foundName >= 0 && foundPrice >= 0)
                    {
                        headerIdx = r;
                        colName = foundName;
                        colPrice = foundPrice;
                        break;
                    }
                    colSupply = colSticker = colArticle = colQty = -1;
                }

                if (headerIdx < 0)
                    throw new InvalidOperationException("В файле «" + Path.GetFileName(file) + "» не найдены колонки «Наименование» и «Стоимость» / «Цена».");

                bool foundSupplyInFile = false;
                for (int r = headerIdx + 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    string rawName;
                    row.TryGetValue(colName, out rawName);
                    string rawPrice;
                    row.TryGetValue(colPrice, out rawPrice);

                    if (string.IsNullOrWhiteSpace(rawName) && string.IsNullOrWhiteSpace(rawPrice))
                        continue;

                    decimal price;
                    if (!TryParseMoney(rawPrice, out price))
                        continue;

                    int qty = 1;
                    if (colQty >= 0)
                    {
                        string rawQty;
                        if (row.TryGetValue(colQty, out rawQty))
                        {
                            int parsedQty;
                            if (int.TryParse(Regex.Replace(rawQty ?? "", @"\D", ""), out parsedQty) && parsedQty > 0)
                                qty = parsedQty;
                        }
                    }

                    string rawSupply = "";
                    if (colSupply >= 0) row.TryGetValue(colSupply, out rawSupply);
                    string supplyId = ExtractSupplyNumber(rawSupply);
                    if (string.IsNullOrEmpty(supplyId))
                        supplyId = !string.IsNullOrWhiteSpace(rawSupply) ? rawSupply.Trim() : fallbackSupplyId;
                    if (!string.IsNullOrEmpty(supplyId))
                    {
                        supplySet.Add(supplyId);
                        foundSupplyInFile = true;
                    }

                    string sticker = "";
                    if (colSticker >= 0) row.TryGetValue(colSticker, out sticker);
                    string article = "";
                    if (colArticle >= 0) row.TryGetValue(colArticle, out article);

                    result.SourceItems.Add(new WbSourceItem
                    {
                        SourceFile = Path.GetFileName(file),
                        SupplyId = supplyId,
                        Sticker = (sticker ?? "").Trim(),
                        Article = (article ?? "").Trim(),
                        RawName = (rawName ?? "").Trim(),
                        NormalizedName = NormalizeItemName(rawName, rules, applyRules),
                        Price = price,
                        Qty = qty
                    });
                }

                if (!foundSupplyInFile && !string.IsNullOrEmpty(fallbackSupplyId))
                    supplySet.Add(fallbackSupplyId);
            }

            result.SupplyIds = supplySet.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

            // Group by (NormalizedName, Price)
            var groupsDict = new Dictionary<string, GroupedWaybillItem>();
            string vatMode = profile != null ? (profile.VatMode ?? "Без НДС") : "Без НДС";
            string unitName = profile != null && !string.IsNullOrEmpty(profile.UnitName) ? profile.UnitName : "шт";
            decimal vatRate = ParseVatRate(vatMode);

            foreach (var item in result.SourceItems)
            {
                string key = item.NormalizedName + "|" + item.Price.ToString("0.00", CultureInfo.InvariantCulture);
                GroupedWaybillItem grp;
                if (!groupsDict.TryGetValue(key, out grp))
                {
                    grp = new GroupedWaybillItem
                    {
                        Name = item.NormalizedName,
                        Unit = unitName,
                        Qty = 0,
                        UnitPrice = item.Price,
                        VatRateLabel = vatMode
                    };
                    groupsDict[key] = grp;
                }
                grp.Qty += item.Qty;
            }

            var groupedList = groupsDict.Values.ToList();
            bool sortDesc = profile == null || profile.SortDescendingByName;
            if (sortDesc)
            {
                groupedList = groupedList
                    .OrderByDescending(g => g.Name, StringComparer.Create(new CultureInfo("ru-RU"), true))
                    .ThenBy(g => g.UnitPrice)
                    .ToList();
            }
            else
            {
                groupedList = groupedList
                    .OrderBy(g => g.Name, StringComparer.Create(new CultureInfo("ru-RU"), true))
                    .ThenBy(g => g.UnitPrice)
                    .ToList();
            }

            foreach (var grp in groupedList)
            {
                decimal lineWithVat = Math.Round(grp.UnitPrice * grp.Qty, 2, MidpointRounding.AwayFromZero);
                if (vatRate > 0m)
                {
                    decimal unitNoVat = Math.Round(grp.UnitPrice / (1m + vatRate), 2, MidpointRounding.AwayFromZero);
                    decimal lineNoVat = Math.Round(lineWithVat / (1m + vatRate), 2, MidpointRounding.AwayFromZero);
                    decimal lineVat = lineWithVat - lineNoVat;
                    grp.UnitPrice = unitNoVat;
                    grp.TotalNoVat = lineNoVat;
                    grp.VatAmount = lineVat;
                    grp.TotalWithVat = lineWithVat;
                }
                else
                {
                    grp.TotalNoVat = lineWithVat;
                    grp.VatAmount = 0m;
                    grp.TotalWithVat = lineWithVat;
                }

                result.TotalQty += grp.Qty;
                result.TotalNoVat += grp.TotalNoVat;
                result.TotalVat += grp.VatAmount;
                result.TotalWithVat += grp.TotalWithVat;
            }

            result.GroupedItems = groupedList;
            return result;
        }

        private static string ExtractSupplyNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var m = Regex.Match(text, @"(?:WB-)?GI-\d+", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string val = m.Value.ToUpperInvariant();
                return val.StartsWith("WB-") ? val : "WB-" + val;
            }
            return "";
        }

        private static bool TryParseMoney(string raw, out decimal val)
        {
            val = 0m;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string cleaned = raw.Trim()
                .Replace("\u00A0", "")
                .Replace(" ", "")
                .Replace(",", ".");
            cleaned = Regex.Replace(cleaned, @"[^0-9.\-]", "");
            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
        }

        private static List<Dictionary<int, string>> ReadFirstSheetRows(string xlsxPath)
        {
            var result = new List<Dictionary<int, string>>();
            using (var zip = ZipFile.OpenRead(xlsxPath))
            {
                var sharedStrings = new List<string>();
                var ssEntry = zip.GetEntry("xl/sharedStrings.xml");
                if (ssEntry != null)
                {
                    using (var s = ssEntry.Open())
                    {
                        var ssDoc = XDocument.Load(s);
                        foreach (var si in ssDoc.Root.Elements(MainNs + "si"))
                        {
                            var sb = new StringBuilder();
                            foreach (var t in si.Descendants(MainNs + "t"))
                                sb.Append(t.Value);
                            sharedStrings.Add(sb.ToString());
                        }
                    }
                }

                string sheetPath = ResolveFirstSheetPath(zip);
                var sheetEntry = zip.GetEntry(sheetPath);
                if (sheetEntry == null)
                    throw new InvalidDataException("Не найден лист в Excel файле: " + Path.GetFileName(xlsxPath));

                using (var s = sheetEntry.Open())
                {
                    var doc = XDocument.Load(s);
                    var sheetData = doc.Root.Element(MainNs + "sheetData");
                    if (sheetData == null) return result;

                    foreach (var rowEl in sheetData.Elements(MainNs + "row"))
                    {
                        var rowDict = new Dictionary<int, string>();
                        int autoCol = 1;
                        foreach (var cEl in rowEl.Elements(MainNs + "c"))
                        {
                            var refAttr = cEl.Attribute("r");
                            int colIdx = refAttr != null ? ColumnLettersToIndex(refAttr.Value) : autoCol;
                            autoCol = colIdx + 1;

                            string val = ReadCellText(cEl, sharedStrings);
                            rowDict[colIdx] = val;
                        }
                        result.Add(rowDict);
                    }
                }
            }
            return result;
        }

        private static string ResolveFirstSheetPath(ZipArchive zip)
        {
            if (zip.GetEntry("xl/worksheets/sheet1.xml") != null)
                return "xl/worksheets/sheet1.xml";
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                    entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    return entry.FullName;
            }
            return "xl/worksheets/sheet1.xml";
        }

        private static string ReadCellText(XElement cEl, List<string> sharedStrings)
        {
            var tAttr = cEl.Attribute("t");
            string t = tAttr != null ? tAttr.Value : "";
            if (t == "s")
            {
                var vEl = cEl.Element(MainNs + "v");
                int idx;
                if (vEl != null && int.TryParse(vEl.Value, out idx) && idx >= 0 && idx < sharedStrings.Count)
                    return sharedStrings[idx];
                return "";
            }
            if (t == "inlineStr")
            {
                var isEl = cEl.Element(MainNs + "is");
                if (isEl == null) return "";
                var sb = new StringBuilder();
                foreach (var txt in isEl.Descendants(MainNs + "t"))
                    sb.Append(txt.Value);
                return sb.ToString();
            }
            var valEl = cEl.Element(MainNs + "v");
            return valEl != null ? valEl.Value : "";
        }

        private static int ColumnLettersToIndex(string cellRef)
        {
            int col = 0;
            foreach (char ch in cellRef)
            {
                if (char.IsLetter(ch))
                {
                    col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
                }
                else
                {
                    break;
                }
            }
            return col;
        }

        public static void GenerateWaybillXlsx(WaybillBuildResult data, WaybillProfile profile, DateTime docDate, string outputPath)
        {
            if (data == null || data.GroupedItems.Count == 0)
                throw new InvalidOperationException("Нет позиций для формирования накладной.");
            if (data.GroupedItems.Count > 178)
                throw new InvalidOperationException("Позиций после группировки (" + data.GroupedItems.Count + ") больше максимума шаблона ТН-2 (178 строк).");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tempPath = outputPath + ".tmp";
            using (var tplStream = OpenTemplateStream())
            using (var mem = new MemoryStream())
            {
                tplStream.CopyTo(mem);
                mem.Position = 0;

                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite))
                {
                    mem.CopyTo(fs);
                }
            }

            using (var zip = ZipFile.Open(tempPath, ZipArchiveMode.Update))
            {
                var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null)
                    throw new InvalidDataException("Повреждён шаблон ТН-2 (отсутствует sheet1.xml).");

                XDocument doc;
                using (var s = sheetEntry.Open())
                {
                    doc = XDocument.Load(s, LoadOptions.PreserveWhitespace);
                }

                var sheetData = doc.Root.Element(MainNs + "sheetData");
                var rowsByNum = new Dictionary<int, XElement>();
                foreach (var rowEl in sheetData.Elements(MainNs + "row"))
                {
                    var rAttr = rowEl.Attribute("r");
                    int rn;
                    if (rAttr != null && int.TryParse(rAttr.Value, out rn))
                        rowsByNum[rn] = rowEl;
                }

                // Capture clean visible style map from row 22 before writing
                var row22Styles = CaptureRowStyles(rowsByNum[22]);

                // 1. Header fields
                SetCellString(rowsByNum, 4, "Z", (profile.SenderUnp ?? "").Trim());
                SetCellString(rowsByNum, 4, "AS", (profile.ReceiverUnp ?? "").Trim());

                SetCellString(rowsByNum, 12, "AB", docDate.ToString("dd"));
                SetCellString(rowsByNum, 12, "AI", GenitiveMonthsRu[docDate.Month]);
                SetCellString(rowsByNum, 12, "BI", docDate.ToString("yy"));

                SetCellString(rowsByNum, 13, "R", (profile.SenderInfo ?? "").Trim());
                SetCellString(rowsByNum, 15, "R", (profile.ReceiverInfo ?? "").Trim());

                string suppliesStr = data.SupplyIds.Count > 0 ? string.Join(", ", data.SupplyIds) : "";
                string basisPrefix = (profile.BasisPrefix ?? "").Trim();
                string basisText = string.IsNullOrEmpty(suppliesStr)
                    ? basisPrefix
                    : (string.IsNullOrEmpty(basisPrefix) ? "сборочные задания №: " + suppliesStr : basisPrefix + ", сборочные задания №: " + suppliesStr);
                SetCellString(rowsByNum, 17, "T", basisText);

                // 2. Item rows 22 .. 199
                int count = data.GroupedItems.Count;
                bool hasVat = data.TotalVat > 0m;
                for (int i = 0; i < count; i++)
                {
                    int rowNum = 22 + i;
                    var item = data.GroupedItems[i];
                    var rowEl = rowsByNum[rowNum];
                    rowEl.SetAttributeValue("hidden", null);
                    rowEl.SetAttributeValue("ht", "11.5");
                    rowEl.SetAttributeValue("customHeight", "1");
                    ApplyRowStyles(rowEl, rowNum, row22Styles);

                    SetCellString(rowsByNum, rowNum, "A", item.Name);
                    SetCellString(rowsByNum, rowNum, "W", item.Unit);
                    SetCellNumber(rowsByNum, rowNum, "AG", item.Qty);
                    SetCellDecimal(rowsByNum, rowNum, "AP", item.UnitPrice);
                    SetCellDecimal(rowsByNum, rowNum, "AZ", item.TotalNoVat);
                    SetCellString(rowsByNum, rowNum, "BK", item.VatRateLabel);
                    if (hasVat)
                        SetCellDecimal(rowsByNum, rowNum, "BT", item.VatAmount);
                    else
                        SetCellString(rowsByNum, rowNum, "BT", "-");
                    SetCellDecimal(rowsByNum, rowNum, "CC", item.TotalWithVat);
                }

                // Clear & hide unused rows
                for (int rowNum = 22 + count; rowNum <= 199; rowNum++)
                {
                    XElement rowEl;
                    if (!rowsByNum.TryGetValue(rowNum, out rowEl)) continue;
                    rowEl.SetAttributeValue("hidden", "1");
                    foreach (var c in rowEl.Elements(MainNs + "c"))
                        ClearCellValue(c);
                }

                // 3. Totals & Footer (rows 200..207)
                SetCellNumber(rowsByNum, 200, "AG", data.TotalQty);
                SetCellDecimal(rowsByNum, 200, "AZ", data.TotalNoVat);
                if (hasVat)
                    SetCellDecimal(rowsByNum, 200, "BT", data.TotalVat);
                else
                    SetCellString(rowsByNum, 200, "BT", "-");
                SetCellDecimal(rowsByNum, 200, "CC", data.TotalWithVat);

                // VAT in words (S201 / BX201)
                if (!hasVat)
                {
                    SetCellString(rowsByNum, 201, "S", "Ноль");
                    SetCellString(rowsByNum, 201, "BX", "00");
                }
                else
                {
                    string vatRubWords, vatKop;
                    SplitMoneyToWordsRu(data.TotalVat, out vatRubWords, out vatKop);
                    SetCellString(rowsByNum, 201, "S", vatRubWords + " руб.");
                    SetCellString(rowsByNum, 201, "BX", vatKop);
                }

                // Total in words (W203 / BX203)
                string totalRubWords, totalKop;
                SplitMoneyToWordsRu(data.TotalWithVat, out totalRubWords, out totalKop);
                SetCellString(rowsByNum, 203, "W", totalRubWords + " руб.");
                SetCellString(rowsByNum, 203, "BX", totalKop);

                // Signatures
                SetCellString(rowsByNum, 205, "R", (profile.Signer ?? "").Trim());
                SetCellString(rowsByNum, 207, "V", (profile.Signer ?? "").Trim());

                // Ensure all mc:Ignorable prefixes remain declared on root
                if (doc.Root != null)
                {
                    if (doc.Root.Attribute(XNamespace.Xmlns + "xr2") == null)
                        doc.Root.SetAttributeValue(XNamespace.Xmlns + "xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
                    if (doc.Root.Attribute(XNamespace.Xmlns + "xr3") == null)
                        doc.Root.SetAttributeValue(XNamespace.Xmlns + "xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");
                }

                sheetEntry.Delete();
                var newEntry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
                using (var sw = new StreamWriter(newEntry.Open(), new UTF8Encoding(false)))
                {
                    doc.Save(sw, SaveOptions.DisableFormatting);
                }
            }

            if (File.Exists(outputPath)) File.Delete(outputPath);
            File.Move(tempPath, outputPath);
        }

        private static Stream OpenTemplateStream()
        {
            string localCustom = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Шаблон_ТН.xlsx");
            if (File.Exists(localCustom))
                return File.OpenRead(localCustom);

            var asm = Assembly.GetExecutingAssembly();
            var res = asm.GetManifestResourceStream("Buhly.Template_TN2.xlsx");
            if (res == null)
                throw new InvalidOperationException("Встроенный шаблон Template_TN2.xlsx не найден в ресурсах программы.");
            return res;
        }

        private static Dictionary<string, string> CaptureRowStyles(XElement row22)
        {
            var map = new Dictionary<string, string>();
            if (row22 == null) return map;
            foreach (var c in row22.Elements(MainNs + "c"))
            {
                var rAttr = c.Attribute("r");
                var sAttr = c.Attribute("s");
                if (rAttr == null || sAttr == null) continue;
                string col = Regex.Replace(rAttr.Value, @"\d+", "");
                map[col] = sAttr.Value;
            }
            return map;
        }

        private static void ApplyRowStyles(XElement rowEl, int rowNum, Dictionary<string, string> styles)
        {
            if (rowEl == null || styles.Count == 0) return;
            foreach (var c in rowEl.Elements(MainNs + "c"))
            {
                var rAttr = c.Attribute("r");
                if (rAttr == null) continue;
                string col = Regex.Replace(rAttr.Value, @"\d+", "");
                string s;
                if (styles.TryGetValue(col, out s))
                    c.SetAttributeValue("s", s);
            }
        }

        private static XElement EnsureCell(Dictionary<int, XElement> rows, int rowNum, string col)
        {
            XElement rowEl;
            if (!rows.TryGetValue(rowNum, out rowEl))
                throw new InvalidOperationException("Строка " + rowNum + " отсутствует в шаблоне.");
            string targetRef = col + rowNum;
            int targetCol = ColumnLettersToIndex(targetRef);
            XElement insertBefore = null;
            foreach (var c in rowEl.Elements(MainNs + "c"))
            {
                var rAttr = c.Attribute("r");
                if (rAttr == null) continue;
                if (rAttr.Value == targetRef) return c;
                if (insertBefore == null && ColumnLettersToIndex(rAttr.Value) > targetCol)
                    insertBefore = c;
            }
            var newCell = new XElement(MainNs + "c", new XAttribute("r", targetRef));
            if (insertBefore != null)
                insertBefore.AddBeforeSelf(newCell);
            else
                rowEl.Add(newCell);
            return newCell;
        }

        private static string StripInvalidXmlChars(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (ch == 0x9 || ch == 0xA || ch == 0xD || (ch >= 0x20 && ch <= 0xD7FF) || (ch >= 0xE000 && ch <= 0xFFFD))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private static void ClearCellValue(XElement c)
        {
            c.SetAttributeValue("t", null);
            var f = c.Element(MainNs + "f"); if (f != null) f.Remove();
            var v = c.Element(MainNs + "v"); if (v != null) v.Remove();
            var isEl = c.Element(MainNs + "is"); if (isEl != null) isEl.Remove();
        }

        private static void SetCellString(Dictionary<int, XElement> rows, int rowNum, string col, string text)
        {
            var c = EnsureCell(rows, rowNum, col);
            ClearCellValue(c);
            string clean = StripInvalidXmlChars(text);
            c.SetAttributeValue("t", "inlineStr");
            var tEl = new XElement(MainNs + "t", clean);
            if (!string.IsNullOrEmpty(clean) && (clean.StartsWith(" ") || clean.EndsWith(" ")))
                tEl.SetAttributeValue(XmlNs + "space", "preserve");
            c.Add(new XElement(MainNs + "is", tEl));
        }

        private static void SetCellNumber(Dictionary<int, XElement> rows, int rowNum, string col, int value)
        {
            var c = EnsureCell(rows, rowNum, col);
            ClearCellValue(c);
            c.Add(new XElement(MainNs + "v", value.ToString(CultureInfo.InvariantCulture)));
        }

        private static void SetCellDecimal(Dictionary<int, XElement> rows, int rowNum, string col, decimal value)
        {
            var c = EnsureCell(rows, rowNum, col);
            ClearCellValue(c);
            c.Add(new XElement(MainNs + "v", Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture)));
        }

        public static void SplitMoneyToWordsRu(decimal amount, out string rublesWords, out string kopecksStr)
        {
            decimal rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            long rubles = (long)Math.Floor(rounded);
            int kopecks = (int)Math.Round((rounded - rubles) * 100m, 0, MidpointRounding.AwayFromZero);
            if (kopecks >= 100)
            {
                rubles += 1;
                kopecks -= 100;
            }
            rublesWords = NumberToWordsRu(rubles);
            if (rublesWords.Length > 0)
                rublesWords = char.ToUpper(rublesWords[0], new CultureInfo("ru-RU")) + rublesWords.Substring(1);
            kopecksStr = kopecks.ToString("00");
        }

        public static string NumberToWordsRu(long number)
        {
            if (number == 0) return "ноль";
            if (number < 0) return "минус " + NumberToWordsRu(-number);

            string[] unitsMasc = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
            string[] unitsFem = { "", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
            string[] teens = { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
            string[] tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
            string[] hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

            var parts = new List<string>();

            long millions = (number / 1000000L) % 1000L;
            if (millions > 0)
            {
                AppendTriad(parts, (int)millions, false, unitsMasc, unitsFem, teens, tens, hundreds);
                parts.Add(PluralRu(millions, "миллион", "миллиона", "миллионов"));
            }

            long thousands = (number / 1000L) % 1000L;
            if (thousands > 0)
            {
                AppendTriad(parts, (int)thousands, true, unitsMasc, unitsFem, teens, tens, hundreds);
                parts.Add(PluralRu(thousands, "тысяча", "тысячи", "тысяч"));
            }

            long rest = number % 1000L;
            if (rest > 0)
            {
                AppendTriad(parts, (int)rest, false, unitsMasc, unitsFem, teens, tens, hundreds);
            }

            return string.Join(" ", parts);
        }

        private static void AppendTriad(List<string> parts, int n, bool feminine, string[] uM, string[] uF, string[] teens, string[] tens, string[] hundreds)
        {
            int h = n / 100;
            int t = (n % 100) / 10;
            int u = n % 10;
            if (h > 0) parts.Add(hundreds[h]);
            if (t == 1)
            {
                parts.Add(teens[u]);
            }
            else
            {
                if (t > 1) parts.Add(tens[t]);
                if (u > 0) parts.Add(feminine ? uF[u] : uM[u]);
            }
        }

        private static string PluralRu(long n, string one, string few, string many)
        {
            long n100 = n % 100;
            long n10 = n % 10;
            if (n100 >= 11 && n100 <= 19) return many;
            if (n10 == 1) return one;
            if (n10 >= 2 && n10 <= 4) return few;
            return many;
        }

        public static void GeneratePickingListXlsx(WaybillBuildResult data, DateTime docDate, string outputPath)
        {
            if (data == null || data.SourceItems.Count == 0)
                throw new InvalidOperationException("Нет товаров для формирования листа сборки.");

            var ordered = data.SourceItems
                .OrderBy(i => i.SupplyId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Article, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.RawName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Price)
                .ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteZipText(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");

                WriteZipText(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                WriteZipText(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"Лист сборки\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

                WriteZipText(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");

                // Styles: 0=normal, 1=title bold 12, 2=header bold bordered fill, 3=cell bordered, 4=cell bordered right decimal
                WriteZipText(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"0.00\"/></numFmts>" +
                    "<fonts count=\"3\">" +
                    "<font><sz val=\"10\"/><name val=\"Calibri\"/></font>" +
                    "<font><b/><sz val=\"12\"/><name val=\"Calibri\"/></font>" +
                    "<font><b/><sz val=\"10\"/><name val=\"Calibri\"/></font>" +
                    "</fonts>" +
                    "<fills count=\"3\">" +
                    "<fill><patternFill patternType=\"none\"/></fill>" +
                    "<fill><patternFill patternType=\"gray125\"/></fill>" +
                    "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEBF3FF\"/></patternFill></fill>" +
                    "</fills>" +
                    "<borders count=\"2\">" +
                    "<border><left/><right/><top/><bottom/><diagonal/></border>" +
                    "<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right><top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border>" +
                    "</borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"5\">" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
                    "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
                    "</cellXfs></styleSheet>");

                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>");
                sb.Append("<cols>");
                sb.Append("<col min=\"1\" max=\"1\" width=\"5\" customWidth=\"1\"/>");
                sb.Append("<col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/>");
                sb.Append("<col min=\"3\" max=\"3\" width=\"14\" customWidth=\"1\"/>");
                sb.Append("<col min=\"4\" max=\"4\" width=\"20\" customWidth=\"1\"/>");
                sb.Append("<col min=\"5\" max=\"5\" width=\"46\" customWidth=\"1\"/>");
                sb.Append("<col min=\"6\" max=\"6\" width=\"7\" customWidth=\"1\"/>");
                sb.Append("<col min=\"7\" max=\"7\" width=\"11\" customWidth=\"1\"/>");
                sb.Append("<col min=\"8\" max=\"8\" width=\"8\" customWidth=\"1\"/>");
                sb.Append("</cols>");
                sb.Append("<sheetData>");

                string title = "Лист сборки от " + docDate.ToString("dd.MM.yyyy") + "  |  Поставки: " + string.Join(", ", data.SupplyIds) + "  |  Всего: " + data.TotalQty + " шт на " + data.TotalWithVat.ToString("0.00", CultureInfo.InvariantCulture) + " BYN";
                sb.Append("<row r=\"1\" ht=\"22\" customHeight=\"1\">").Append(InlineCell("A1", 1, title)).Append("</row>");

                string[] headers = { "№", "Поставка", "Стикер", "Артикул продавца", "Наименование", "Кол-во", "Цена, BYN", "ОК" };
                string[] cols = { "A", "B", "C", "D", "E", "F", "G", "H" };
                sb.Append("<row r=\"3\" ht=\"22\" customHeight=\"1\">");
                for (int c = 0; c < headers.Length; c++)
                    sb.Append(InlineCell(cols[c] + "3", 2, headers[c]));
                sb.Append("</row>");

                for (int i = 0; i < ordered.Count; i++)
                {
                    int rn = i + 4;
                    var it = ordered[i];
                    sb.Append("<row r=\"" + rn + "\">");
                    sb.Append(NumericCell("A" + rn, 3, (i + 1).ToString(CultureInfo.InvariantCulture)));
                    sb.Append(InlineCell("B" + rn, 3, it.SupplyId));
                    sb.Append(InlineCell("C" + rn, 3, it.Sticker));
                    sb.Append(InlineCell("D" + rn, 3, it.Article));
                    sb.Append(InlineCell("E" + rn, 3, it.RawName));
                    sb.Append(NumericCell("F" + rn, 3, it.Qty.ToString(CultureInfo.InvariantCulture)));
                    sb.Append(NumericCell("G" + rn, 4, it.Price.ToString("0.00", CultureInfo.InvariantCulture)));
                    sb.Append(InlineCell("H" + rn, 3, "[   ]"));
                    sb.Append("</row>");
                }

                sb.Append("</sheetData>");
                sb.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.5\" header=\"0.2\" footer=\"0.2\"/>");
                sb.Append("<pageSetup paperSize=\"9\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
                sb.Append("</worksheet>");

                WriteZipText(zip, "xl/worksheets/sheet1.xml", sb.ToString());
            }
        }

        private static string EscapeXml(string s)
        {
            string clean = StripInvalidXmlChars(s);
            if (clean.Length == 0) return "";
            return clean.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private static string InlineCell(string refCoord, int styleIdx, string val)
        {
            return "<c r=\"" + refCoord + "\" s=\"" + styleIdx + "\" t=\"inlineStr\"><is><t>" + EscapeXml(val) + "</t></is></c>";
        }

        private static string NumericCell(string refCoord, int styleIdx, string numStr)
        {
            return "<c r=\"" + refCoord + "\" s=\"" + styleIdx + "\"><v>" + numStr + "</v></c>";
        }

        private static void WriteZipText(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var sw = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                sw.Write(content);
        }

        public static void OpenFileWithShell(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public static void PrintFileWithShell(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "print" });
        }
    }
}
