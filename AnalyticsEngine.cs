using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GovPortalsApp
{
    public class WbReportSummary
    {
        public string ReportNumber { get; set; }
        public string SourceFile { get; set; }
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public string Countries { get; set; }
        public int SaleQty { get; set; }
        public int ReturnQty { get; set; }
        public decimal RealizedP { get; set; }
        public decimal RetailAfterDisc { get; set; }
        public decimal ToPayGoods { get; set; }
        public decimal CommissionWithVat { get; set; }
        public decimal Logistics { get; set; }
        public decimal Storage { get; set; }
        public decimal Penalties { get; set; }
        public decimal Holdings { get; set; }
        public decimal Acceptance { get; set; }
        public decimal Loyalty { get; set; }
        public decimal ExpectedPayout { get; set; }
    }

    public class SkuUnitStats
    {
        public string Article { get; set; }
        public string Barcode { get; set; }
        public string Title { get; set; }
        public int SaleQty { get; set; }
        public int ReturnQty { get; set; }
        public decimal RealizedP { get; set; }
        public decimal ToPayGoods { get; set; }
        public decimal Logistics { get; set; }
        public decimal Penalties { get; set; }
        public decimal StorageAndOther { get; set; }
        public decimal NetFromWb { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal EstimatedTax { get; set; }
        public decimal NetProfit { get; set; }

        public int NetQty { get { return Math.Max(0, SaleQty - ReturnQty); } }
        public decimal BuyoutPercent
        {
            get
            {
                int tot = SaleQty + ReturnQty;
                return tot > 0 ? Math.Round((decimal)SaleQty * 100m / tot, 1) : 0m;
            }
        }
    }

    public class BankTxItem
    {
        public string Date { get; set; }
        public DateTime ParsedDate { get; set; }
        public string DocNum { get; set; }
        public string Counterparty { get; set; }
        public string Unp { get; set; }
        public string Purpose { get; set; }
        public string BudgetCode { get; set; }
        public decimal Debet { get; set; }
        public decimal Credit { get; set; }
        public string Category { get; set; }
    }

    public class BankCounterpartyTotal
    {
        public string Category { get; set; }
        public string Counterparty { get; set; }
        public string Unp { get; set; }
        public int Count { get; set; }
        public decimal Debet { get; set; }
        public decimal Credit { get; set; }
    }

    public class AnalyticsSnapshot
    {
        public int LoadedWbFiles { get; set; }
        public int DeduplicatedSkipped { get; set; }
        public int LoadedBankFiles { get; set; }
        public decimal TaxRatePercent { get; set; }

        public List<WbReportSummary> Reports { get; set; }
        public List<SkuUnitStats> Skus { get; set; }
        public List<BankTxItem> BankTransactions { get; set; }
        public List<BankCounterpartyTotal> BankCounterparties { get; set; }

        public int TotalSaleQty { get; set; }
        public int TotalReturnQty { get; set; }
        public decimal TotalRealizedP { get; set; }
        public decimal TotalCommissionWithVat { get; set; }
        public decimal TotalToPayGoods { get; set; }
        public decimal TotalLogistics { get; set; }
        public decimal TotalStorage { get; set; }
        public decimal TotalPenalties { get; set; }
        public decimal TotalHoldings { get; set; }
        public decimal TotalAcceptance { get; set; }
        public decimal TotalLoyalty { get; set; }
        public decimal TotalExpectedPayout { get; set; }

        public decimal TotalCostOfGoods { get; set; }
        public decimal EstimatedTaxByRealization { get; set; }
        public decimal EstimatedNetProfitBeforeFixed { get; set; }

        // Bank aggregates
        public decimal BankWbCredits { get; set; }
        public decimal BankOtherCredits { get; set; }
        public decimal BankTotalCredit { get; set; }
        public decimal BankTaxesDebet { get; set; }
        public decimal BankVendorsDebet { get; set; }
        public decimal BankPayrollDebet { get; set; }
        public decimal BankFeesDebet { get; set; }
        public decimal BankTotalDebet { get; set; }

        public AnalyticsSnapshot()
        {
            TaxRatePercent = 6.0m;
            Reports = new List<WbReportSummary>();
            Skus = new List<SkuUnitStats>();
            BankTransactions = new List<BankTxItem>();
            BankCounterparties = new List<BankCounterpartyTotal>();
        }
    }

    public static class AnalyticsEngine
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public static string GetSkuCostFilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "sku_costs.tsv");
        }

        public static Dictionary<string, decimal> LoadSkuCosts()
        {
            var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var path = GetSkuCostFilePath();
                if (!File.Exists(path)) return dict;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 2) continue;
                    decimal val;
                    if (TryParseDec(parts[1], out val) && val >= 0m)
                        dict[parts[0].Trim()] = val;
                }
            }
            catch { }
            return dict;
        }

        public static void SaveSkuCosts(Dictionary<string, decimal> costs)
        {
            if (costs == null) return;
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in costs.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}\t{1:0.00}\r\n", kv.Key, kv.Value);
                }
                File.WriteAllText(GetSkuCostFilePath(), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static int ImportSkuCostsFromTextOrCsv(string path, Dictionary<string, decimal> target)
        {
            int count = 0;
            if (!File.Exists(path)) return 0;

            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                using (var fs = File.OpenRead(path))
                {
                    var rows = ReadXlsxFirstSheet(fs);
                    int colArt = -1, colCost = -1;
                    int start = 0;
                    for (int i = 0; i < Math.Min(5, rows.Count); i++)
                    {
                        foreach (var kv in rows[i])
                        {
                            string h = (kv.Value ?? "").Trim().ToLowerInvariant();
                            if (colArt < 0 && (h.Contains("артикул") || h.Contains("sku") || h.Contains("баркод"))) colArt = kv.Key;
                            if (colCost < 0 && (h.Contains("себест") || h.Contains("закуп") || h.Contains("цена"))) colCost = kv.Key;
                        }
                        if (colArt >= 0 && colCost >= 0) { start = i + 1; break; }
                    }
                    if (colArt < 0 || colCost < 0) { colArt = 1; colCost = 2; start = 0; }
                    for (int i = start; i < rows.Count; i++)
                    {
                        string art, cStr;
                        rows[i].TryGetValue(colArt, out art);
                        rows[i].TryGetValue(colCost, out cStr);
                        decimal cost;
                        if (!string.IsNullOrWhiteSpace(art) && TryParseDec(cStr, out cost) && cost >= 0m)
                        {
                            target[art.Trim()] = cost;
                            count++;
                        }
                    }
                }
                return count;
            }

            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var parts = raw.Split(new char[] { ';', '\t', '=' });
                if (parts.Length < 2) continue;
                decimal cost;
                if (!string.IsNullOrWhiteSpace(parts[0]) && TryParseDec(parts[parts.Length - 1], out cost) && cost >= 0m)
                {
                    target[parts[0].Trim()] = cost;
                    count++;
                }
            }
            return count;
        }

        public static AnalyticsSnapshot AnalyzeFiles(IEnumerable<string> paths, decimal taxRatePercent, Dictionary<string, decimal> skuCosts)
        {
            var snap = new AnalyticsSnapshot { TaxRatePercent = taxRatePercent };
            if (paths == null) return snap;

            var seenReports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skuDict = new Dictionary<string, SkuUnitStats>(StringComparer.OrdinalIgnoreCase);
            var bankTxKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in paths)
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) continue;

                if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using (var z = ZipFile.OpenRead(file))
                    {
                        foreach (var entry in z.Entries)
                        {
                            if (!entry.FullName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) continue;
                            using (var ms = new MemoryStream())
                            using (var es = entry.Open())
                            {
                                es.CopyTo(ms);
                                ms.Position = 0;
                                ProcessXlsxStream(ms, Path.GetFileName(file) + " / " + entry.Name, snap, seenReports, skuDict, bankTxKeys);
                            }
                        }
                    }
                }
                else if (file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using (var fs = File.OpenRead(file))
                    {
                        ProcessXlsxStream(fs, Path.GetFileName(file), snap, seenReports, skuDict, bankTxKeys);
                    }
                }
            }

            // Compute aggregate WB totals & SKU unit economics
            decimal taxMult = Math.Max(0m, taxRatePercent) / 100m;
            foreach (var rep in snap.Reports)
            {
                snap.TotalSaleQty += rep.SaleQty;
                snap.TotalReturnQty += rep.ReturnQty;
                snap.TotalRealizedP += rep.RealizedP;
                snap.TotalCommissionWithVat += rep.CommissionWithVat;
                snap.TotalToPayGoods += rep.ToPayGoods;
                snap.TotalLogistics += rep.Logistics;
                snap.TotalStorage += rep.Storage;
                snap.TotalPenalties += rep.Penalties;
                snap.TotalHoldings += rep.Holdings;
                snap.TotalAcceptance += rep.Acceptance;
                snap.TotalLoyalty += rep.Loyalty;
                snap.TotalExpectedPayout += rep.ExpectedPayout;
            }

            foreach (var sku in skuDict.Values)
            {
                sku.NetFromWb = sku.ToPayGoods - sku.Logistics - sku.Penalties - sku.StorageAndOther;
                decimal cost = 0m;
                if (skuCosts != null)
                {
                    if (!skuCosts.TryGetValue(sku.Article, out cost) && !string.IsNullOrEmpty(sku.Barcode))
                        skuCosts.TryGetValue(sku.Barcode, out cost);
                }
                sku.UnitCost = cost;
                sku.TotalCost = Math.Round(cost * sku.NetQty, 2, MidpointRounding.AwayFromZero);
                sku.EstimatedTax = Math.Round(sku.RealizedP * taxMult, 2, MidpointRounding.AwayFromZero);
                sku.NetProfit = sku.NetFromWb - sku.TotalCost - sku.EstimatedTax;
                snap.TotalCostOfGoods += sku.TotalCost;
            }

            snap.Skus = skuDict.Values
                .OrderByDescending(s => s.RealizedP)
                .ThenByDescending(s => s.NetFromWb)
                .ToList();

            snap.EstimatedTaxByRealization = Math.Round(snap.TotalRealizedP * taxMult, 2, MidpointRounding.AwayFromZero);
            snap.EstimatedNetProfitBeforeFixed = snap.TotalExpectedPayout - snap.TotalCostOfGoods - snap.EstimatedTaxByRealization;

            // Aggregate Bank counterparties
            var cpMap = new Dictionary<string, BankCounterpartyTotal>(StringComparer.OrdinalIgnoreCase);
            foreach (var tx in snap.BankTransactions)
            {
                snap.BankTotalCredit += tx.Credit;
                snap.BankTotalDebet += tx.Debet;

                if (tx.Category == "WB / Маркетплейсы") snap.BankWbCredits += tx.Credit;
                else if (tx.Credit > 0m) snap.BankOtherCredits += tx.Credit;

                if (tx.Category == "Налоги и ФСЗН") snap.BankTaxesDebet += tx.Debet;
                else if (tx.Category == "Зарплата и выплаты") snap.BankPayrollDebet += tx.Debet;
                else if (tx.Category == "Комиссия банка") snap.BankFeesDebet += tx.Debet;
                else if (tx.Debet > 0m) snap.BankVendorsDebet += tx.Debet;

                string cpKey = tx.Category + "|" + (string.IsNullOrEmpty(tx.Unp) ? tx.Counterparty : tx.Unp);
                BankCounterpartyTotal cp;
                if (!cpMap.TryGetValue(cpKey, out cp))
                {
                    cp = new BankCounterpartyTotal
                    {
                        Category = tx.Category,
                        Counterparty = tx.Counterparty,
                        Unp = tx.Unp
                    };
                    cpMap[cpKey] = cp;
                }
                cp.Count++;
                cp.Debet += tx.Debet;
                cp.Credit += tx.Credit;
            }

            snap.BankCounterparties = cpMap.Values
                .OrderByDescending(c => c.Credit + c.Debet)
                .ToList();

            snap.Reports = snap.Reports
                .OrderBy(r => r.DateFrom, StringComparer.Ordinal)
                .ThenBy(r => r.ReportNumber, StringComparer.Ordinal)
                .ToList();

            return snap;
        }

        private static void ProcessXlsxStream(
            Stream xlsxStream,
            string sourceLabel,
            AnalyticsSnapshot snap,
            HashSet<string> seenReports,
            Dictionary<string, SkuUnitStats> skuDict,
            HashSet<string> bankTxKeys)
        {
            var rows = ReadXlsxFirstSheet(xlsxStream);
            if (rows.Count < 2) return;

            // 1. Check if Bank Statement (look in first 25 rows for "Дата операции" & "Корреспондент" & "Дебет")
            int bankHdrIdx = DetectBankHeader(rows);
            if (bankHdrIdx >= 0)
            {
                ParseBankRows(rows, bankHdrIdx, snap, bankTxKeys);
                snap.LoadedBankFiles++;
                return;
            }

            // 2. Check if WB Weekly Detailed Report (header usually row 0)
            int wbHdrIdx = DetectWbReportHeader(rows);
            if (wbHdrIdx >= 0)
            {
                ParseWbReportRows(rows, wbHdrIdx, sourceLabel, snap, seenReports, skuDict);
                snap.LoadedWbFiles++;
            }
        }

        private static int DetectBankHeader(List<Dictionary<int, string>> rows)
        {
            int limit = Math.Min(25, rows.Count);
            for (int i = 0; i < limit; i++)
            {
                bool hasDate = false, hasCorr = false, hasDeb = false;
                foreach (var v in rows[i].Values)
                {
                    string h = (v ?? "").Trim().ToLowerInvariant();
                    if (h.Contains("дата операц")) hasDate = true;
                    if (h.Contains("корреспондент")) hasCorr = true;
                    if (h == "дебет" || h.Contains("кредит")) hasDeb = true;
                }
                if (hasDate && hasCorr && hasDeb) return i;
            }
            return -1;
        }

        private static int DetectWbReportHeader(List<Dictionary<int, string>> rows)
        {
            int limit = Math.Min(5, rows.Count);
            for (int i = 0; i < limit; i++)
            {
                bool hasReason = false, hasToPay = false;
                foreach (var v in rows[i].Values)
                {
                    string h = (v ?? "").Trim().ToLowerInvariant();
                    if (h.Contains("обоснование для оплаты")) hasReason = true;
                    if (h.Contains("к перечислению продавцу")) hasToPay = true;
                }
                if (hasReason && hasToPay) return i;
            }
            return -1;
        }

        private static void ParseBankRows(List<Dictionary<int, string>> rows, int hdrIdx, AnalyticsSnapshot snap, HashSet<string> bankTxKeys)
        {
            var hdr = rows[hdrIdx];
            int cDate = FindCol(hdr, "дата операц");
            int cNum = FindCol(hdr, "док-та", "номер док");
            int cCorr = FindColExact(hdr, "корреспондент");
            int cUnp = FindCol(hdr, "унп корреспондента");
            int cPurp = FindCol(hdr, "назначение");
            int cBud = FindCol(hdr, "код плат");
            int cDeb = FindColExact(hdr, "дебет");
            int cCred = FindColExact(hdr, "кредит");

            for (int i = hdrIdx + 1; i < rows.Count; i++)
            {
                var r = rows[i];
                string dtStr = GetStr(r, cDate);
                if (string.IsNullOrWhiteSpace(dtStr)) continue;

                decimal deb = GetDec(r, cDeb);
                decimal cred = GetDec(r, cCred);
                if (deb == 0m && cred == 0m) continue;

                string num = GetStr(r, cNum);
                string corr = GetStr(r, cCorr);
                string unp = GetStr(r, cUnp);
                string purp = GetStr(r, cPurp);
                string bud = GetStr(r, cBud);

                string txKey = dtStr + "|" + num + "|" + deb.ToString("0.00", CultureInfo.InvariantCulture) + "|" + cred.ToString("0.00", CultureInfo.InvariantCulture) + "|" + unp;
                if (!bankTxKeys.Add(txKey)) continue;

                DateTime parsedDt;
                DateTime.TryParseExact(dtStr.Trim(), new string[] { "dd.MM.yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDt);

                string cat = ClassifyBankTx(corr, unp, purp, bud, deb, cred);
                snap.BankTransactions.Add(new BankTxItem
                {
                    Date = dtStr.Trim(),
                    ParsedDate = parsedDt,
                    DocNum = num,
                    Counterparty = corr,
                    Unp = unp,
                    Purpose = purp,
                    BudgetCode = bud,
                    Debet = deb,
                    Credit = cred,
                    Category = cat
                });
            }
        }

        private static string ClassifyBankTx(string corr, string unp, string purp, string bud, decimal deb, decimal cred)
        {
            string cUp = (corr ?? "").ToUpperInvariant();
            string pUp = (purp ?? "").ToUpperInvariant();

            if (cred > 0m)
            {
                if (cUp.Contains("ИМВБРБ") || unp == "193630204" || cUp.Contains("ВАЙЛДБЕРРИЗ") || cUp.Contains("ОЗОН") || pUp.Contains("ОПЛАТА ПО ДОГОВОРУ") && cUp.Contains("МАРКЕТ"))
                    return "WB / Маркетплейсы";
                return "Прочие поступления";
            }

            if (!string.IsNullOrWhiteSpace(bud) || cUp.Contains("ГЛАВНОЕ УПРАВЛЕНИЕ") || cUp.Contains("МИНИСТЕРСТВА ФИНАНСОВ") || cUp.Contains("БЕЛГОССТРАХ") || cUp.Contains("ФСЗН"))
                return "Налоги и ФСЗН";

            if (cUp.Contains("АЛЬФА-БАНК") && (pUp.Contains("КОМИССИ") || pUp.Contains("ВОЗНАГРАЖД") || pUp.Contains("ПАКЕТ") || pUp.Contains("ПЛАТА")))
                return "Комиссия банка";
            if (pUp.Contains("КОМИССИЯ ЗА ПЛАТ") || pUp.Contains("СМС") || pUp.Contains("АБОНЕНТСКАЯ ПЛАТА"))
                return "Комиссия банка";

            if (pUp.Contains("ЗАРАБОТН") || pUp.Contains("ЗАРПЛАТ") || pUp.Contains("АВАНС") || pUp.Contains("ОТПУСК") || pUp.Contains("ДИВИДЕНД") || pUp.Contains("ЗАЙМ") || pUp.Contains("ЛИЧНЫЙ ДОХОД"))
                return "Зарплата и выплаты";

            return "Поставщики и расходы";
        }

        private static void ParseWbReportRows(
            List<Dictionary<int, string>> rows,
            int hdrIdx,
            string sourceLabel,
            AnalyticsSnapshot snap,
            HashSet<string> seenReports,
            Dictionary<string, SkuUnitStats> skuDict)
        {
            var hdr = rows[hdrIdx];
            int cArticle = FindCol(hdr, "артикул поставщика");
            int cName = FindColExact(hdr, "название");
            int cNmId = FindCol(hdr, "код номенклатуры");
            int cBarcode = FindColExact(hdr, "баркод");
            int cReason = FindCol(hdr, "обоснование для оплаты");
            int cSaleDate = FindCol(hdr, "дата продажи");
            int cQty = FindColExact(hdr, "кол-во");
            int cAfterDisc = FindCol(hdr, "цена розничная с учетом согласованной скидки");
            int cRealizedP = FindCol(hdr, "вайлдберриз реализовал товар");
            int cVv = FindCol(hdr, "вознаграждение вайлдберриз (вв), без ндс");
            int cVvVat = FindCol(hdr, "ндс с вознаграждения вайлдберриз");
            int cToPay = FindCol(hdr, "к перечислению продавцу");
            int cLog = FindCol(hdr, "услуги по доставке товара");
            int cPen = FindCol(hdr, "общая сумма штрафов");
            int cStor = FindColExact(hdr, "хранение");
            int cHold = FindColExact(hdr, "удержания");
            int cAcc = FindCol(hdr, "операции на приемке");
            int cLoy = FindCol(hdr, "стоимость участия в программе лояльности");
            int cCountry = FindColExact(hdr, "страна");

            // Extract report number for deduplication
            string repNum = "";
            var mNum = Regex.Match(sourceLabel, @"№\s*(\d+)");
            if (mNum.Success) repNum = mNum.Groups[1].Value;

            var summary = new WbReportSummary
            {
                ReportNumber = string.IsNullOrEmpty(repNum) ? Path.GetFileNameWithoutExtension(sourceLabel) : repNum,
                SourceFile = sourceLabel
            };

            string minDate = "", maxDate = "";
            var countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Accumulate temporary SKU deltas for this report so duplicates skipped cleanly BEFORE mutating skuDict
            var localSkus = new List<Action>();

            for (int i = hdrIdx + 1; i < rows.Count; i++)
            {
                var r = rows[i];
                string reason = GetStr(r, cReason).Trim();
                if (string.IsNullOrEmpty(reason) && cReason < 0) continue;

                string sDate = GetStr(r, cSaleDate).Trim();
                if (!string.IsNullOrEmpty(sDate))
                {
                    if (string.IsNullOrEmpty(minDate) || string.CompareOrdinal(sDate, minDate) < 0) minDate = sDate;
                    if (string.IsNullOrEmpty(maxDate) || string.CompareOrdinal(sDate, maxDate) > 0) maxDate = sDate;
                }

                string ctry = GetStr(r, cCountry).Trim();
                if (!string.IsNullOrEmpty(ctry)) countries.Add(ctry);

                int qty = (int)Math.Round(GetDec(r, cQty), 0);
                decimal realP = GetDec(r, cRealizedP);
                decimal afterDisc = GetDec(r, cAfterDisc);
                decimal toPay = GetDec(r, cToPay);
                decimal vv = GetDec(r, cVv) + GetDec(r, cVvVat);
                decimal log = GetDec(r, cLog);
                decimal pen = GetDec(r, cPen);
                decimal stor = GetDec(r, cStor);
                decimal hold = GetDec(r, cHold);
                decimal acc = GetDec(r, cAcc);
                decimal loy = GetDec(r, cLoy);

                bool isSale = string.Equals(reason, "Продажа", StringComparison.OrdinalIgnoreCase);
                bool isReturn = string.Equals(reason, "Возврат", StringComparison.OrdinalIgnoreCase);

                if (isSale)
                {
                    summary.SaleQty += qty;
                    summary.RealizedP += realP;
                    summary.RetailAfterDisc += afterDisc * Math.Max(1, qty);
                    summary.ToPayGoods += toPay;
                    summary.CommissionWithVat += vv;
                }
                else if (isReturn)
                {
                    summary.ReturnQty += qty;
                    summary.RealizedP -= realP;
                    summary.RetailAfterDisc -= afterDisc * Math.Max(1, qty);
                    summary.ToPayGoods -= toPay;
                    summary.CommissionWithVat -= vv;
                }

                summary.Logistics += log;
                summary.Penalties += pen;
                summary.Storage += stor;
                summary.Holdings += hold;
                summary.Acceptance += acc;
                summary.Loyalty += loy;

                string art = GetStr(r, cArticle).Trim();
                string bar = GetStr(r, cBarcode).Trim();
                string nmId = GetStr(r, cNmId).Trim();
                string title = GetStr(r, cName).Trim();
                string skuKey = !string.IsNullOrEmpty(art) ? art : (!string.IsNullOrEmpty(bar) ? bar : nmId);

                if (!string.IsNullOrEmpty(skuKey) && (isSale || isReturn || log != 0m || pen != 0m || stor != 0m || hold != 0m || acc != 0m || loy != 0m))
                {
                    localSkus.Add(delegate
                    {
                        SkuUnitStats st;
                        if (!skuDict.TryGetValue(skuKey, out st))
                        {
                            st = new SkuUnitStats
                            {
                                Article = !string.IsNullOrEmpty(art) ? art : skuKey,
                                Barcode = bar,
                                Title = title
                            };
                            skuDict[skuKey] = st;
                        }
                        if (string.IsNullOrEmpty(st.Title) && !string.IsNullOrEmpty(title)) st.Title = title;
                        if (string.IsNullOrEmpty(st.Barcode) && !string.IsNullOrEmpty(bar)) st.Barcode = bar;

                        if (isSale)
                        {
                            st.SaleQty += qty;
                            st.RealizedP += realP;
                            st.ToPayGoods += toPay;
                        }
                        else if (isReturn)
                        {
                            st.ReturnQty += qty;
                            st.RealizedP -= realP;
                            st.ToPayGoods -= toPay;
                        }
                        st.Logistics += log;
                        st.Penalties += pen;
                        st.StorageAndOther += stor + hold + acc + loy;
                    });
                }
            }

            summary.DateFrom = minDate;
            summary.DateTo = maxDate;
            summary.Countries = string.Join(", ", countries.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray());
            summary.ExpectedPayout =
                summary.ToPayGoods
                - summary.Logistics
                - summary.Penalties
                - summary.Storage
                - summary.Holdings
                - summary.Acceptance
                - summary.Loyalty;

            string dedupKey = !string.IsNullOrEmpty(repNum)
                ? ("WB#" + repNum)
                : (summary.DateFrom + "|" + summary.DateTo + "|" + summary.SaleQty + "|" + summary.ExpectedPayout.ToString("0.00", CultureInfo.InvariantCulture));

            if (!seenReports.Add(dedupKey))
            {
                snap.DeduplicatedSkipped++;
                return;
            }

            snap.Reports.Add(summary);
            foreach (var act in localSkus) act();
        }

        private static int FindCol(Dictionary<int, string> hdr, params string[] substrings)
        {
            foreach (var kv in hdr)
            {
                string h = (kv.Value ?? "").Trim().ToLowerInvariant();
                foreach (var sub in substrings)
                {
                    if (h.Contains(sub)) return kv.Key;
                }
            }
            return -1;
        }

        private static int FindColExact(Dictionary<int, string> hdr, string exact)
        {
            foreach (var kv in hdr)
            {
                if (string.Equals((kv.Value ?? "").Trim(), exact, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            }
            return -1;
        }

        private static string GetStr(Dictionary<int, string> row, int col)
        {
            if (col < 0) return "";
            string v;
            return row.TryGetValue(col, out v) ? (v ?? "") : "";
        }

        private static decimal GetDec(Dictionary<int, string> row, int col)
        {
            if (col < 0) return 0m;
            string raw;
            if (!row.TryGetValue(col, out raw)) return 0m;
            decimal val;
            return TryParseDec(raw, out val) ? val : 0m;
        }

        private static bool TryParseDec(string raw, out decimal val)
        {
            val = 0m;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Trim().Replace("\u00A0", "").Replace(" ", "").Replace(",", ".");
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
        }

        private static List<Dictionary<int, string>> ReadXlsxFirstSheet(Stream stream)
        {
            var result = new List<Dictionary<int, string>>();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                var sharedStrings = new List<string>();
                var ssEntry = zip.GetEntry("xl/sharedStrings.xml");
                if (ssEntry != null)
                {
                    using (var s = ssEntry.Open())
                    {
                        var doc = XDocument.Load(s);
                        foreach (var si in doc.Root.Elements(MainNs + "si"))
                        {
                            var sb = new StringBuilder();
                            foreach (var t in si.Descendants(MainNs + "t"))
                                sb.Append(t.Value);
                            sharedStrings.Add(sb.ToString());
                        }
                    }
                }

                ZipArchiveEntry sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null)
                {
                    foreach (var e in zip.Entries)
                    {
                        if (e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            sheetEntry = e;
                            break;
                        }
                    }
                }
                if (sheetEntry == null) return result;

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
                            var rAttr = cEl.Attribute("r");
                            int colIdx = rAttr != null ? ColIndex(rAttr.Value) : autoCol;
                            autoCol = colIdx + 1;

                            var tAttr = cEl.Attribute("t");
                            string t = tAttr != null ? tAttr.Value : "";
                            string val = "";
                            if (t == "s")
                            {
                                var v = cEl.Element(MainNs + "v");
                                int idx;
                                if (v != null && int.TryParse(v.Value, out idx) && idx >= 0 && idx < sharedStrings.Count)
                                    val = sharedStrings[idx];
                            }
                            else if (t == "inlineStr")
                            {
                                var isEl = cEl.Element(MainNs + "is");
                                if (isEl != null)
                                {
                                    var sb = new StringBuilder();
                                    foreach (var txt in isEl.Descendants(MainNs + "t")) sb.Append(txt.Value);
                                    val = sb.ToString();
                                }
                            }
                            else
                            {
                                var v = cEl.Element(MainNs + "v");
                                if (v != null) val = v.Value;
                            }
                            rowDict[colIdx] = val;
                        }
                        result.Add(rowDict);
                    }
                }
            }
            return result;
        }

        private static int ColIndex(string cellRef)
        {
            int col = 0;
            foreach (char ch in cellRef)
            {
                if (char.IsLetter(ch)) col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
                else break;
            }
            return col;
        }

        // ==================== MULTI-SHEET EXCEL EXPORT ====================

        public static void ExportAnalyticsXlsx(AnalyticsSnapshot snap, string outputPath)
        {
            if (snap == null || (snap.Reports.Count == 0 && snap.BankTransactions.Count == 0))
                throw new InvalidOperationException("Нет данных для выгрузки.");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var sheets = new List<KeyValuePair<string, string>>();
            sheets.Add(new KeyValuePair<string, string>("Сводка и налог", BuildSummarySheetXml(snap)));
            if (snap.Reports.Count > 0)
            {
                sheets.Add(new KeyValuePair<string, string>("По отчетам WB", BuildReportsSheetXml(snap)));
                sheets.Add(new KeyValuePair<string, string>("Юнит-экономика SKU", BuildSkusSheetXml(snap)));
            }
            if (snap.BankTransactions.Count > 0)
            {
                sheets.Add(new KeyValuePair<string, string>("Выписка и сверка", BuildBankSheetXml(snap)));
            }

            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var ctOverrides = new StringBuilder();
                var wbSheets = new StringBuilder();
                var wbRels = new StringBuilder();

                for (int i = 0; i < sheets.Count; i++)
                {
                    int idx = i + 1;
                    ctOverrides.AppendFormat("<Override PartName=\"/xl/worksheets/sheet{0}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>", idx);
                    wbSheets.AppendFormat("<sheet name=\"{0}\" sheetId=\"{1}\" r:id=\"rId{1}\"/>", Esc(sheets[i].Key), idx);
                    wbRels.AppendFormat("<Relationship Id=\"rId{0}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{0}.xml\"/>", idx);
                    WriteZip(zip, "xl/worksheets/sheet" + idx + ".xml", sheets[i].Value);
                }

                WriteZip(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    ctOverrides.ToString() +
                    "</Types>");

                WriteZip(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                WriteZip(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets>" + wbSheets.ToString() + "</sheets></workbook>");

                WriteZip(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    wbRels.ToString() +
                    "<Relationship Id=\"rId" + (sheets.Count + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");

                // Styles: 0=norm, 1=title, 2=hdr, 3=cellText, 4=cellMoney, 5=cellInt, 6=boldText, 7=boldMoney
                WriteZip(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/></numFmts>" +
                    "<fonts count=\"3\">" +
                    "<font><sz val=\"10\"/><name val=\"Calibri\"/></font>" +
                    "<font><b/><sz val=\"13\"/><name val=\"Calibri\"/></font>" +
                    "<font><b/><sz val=\"10\"/><name val=\"Calibri\"/></font>" +
                    "</fonts>" +
                    "<fills count=\"4\">" +
                    "<fill><patternFill patternType=\"none\"/></fill>" +
                    "<fill><patternFill patternType=\"gray125\"/></fill>" +
                    "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEBF3FF\"/></patternFill></fill>" +
                    "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF2F2F7\"/></patternFill></fill>" +
                    "</fills>" +
                    "<borders count=\"2\">" +
                    "<border><left/><right/><top/><bottom/><diagonal/></border>" +
                    "<border><left style=\"thin\"><color rgb=\"FFD1D1D6\"/></left><right style=\"thin\"><color rgb=\"FFD1D1D6\"/></right><top style=\"thin\"><color rgb=\"FFD1D1D6\"/></top><bottom style=\"thin\"><color rgb=\"FFD1D1D6\"/></bottom><diagonal/></border>" +
                    "</borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"8\">" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
                    "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
                    "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
                    "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"><alignment vertical=\"center\"/></xf>" +
                    "<xf numFmtId=\"164\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
                    "</cellXfs></styleSheet>");
            }
        }

        private static string BuildSummarySheetXml(AnalyticsSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"46\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"38\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">").Append(SC("A1", 1, "Buhly • Финансовый отчёт WB и сверка для 1С / УСН")).Append("</row>");
            sb.Append("<row r=\"2\">").Append(SC("A2", 0, "Сформировано: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"))).Append("</row>");

            int r = 4;
            AddKvHdr(sb, r++, "Показатель (Отчёты WB)", "Сумма / Кол-во", "Комментарий для бухгалтера");
            AddKvInt(sb, r++, "Загружено уникальных отчётов WB", snap.Reports.Count, "Дубликаты пропущены: " + snap.DeduplicatedSkipped);
            AddKvInt(sb, r++, "Продано товара (шт)", snap.TotalSaleQty, "");
            AddKvInt(sb, r++, "Возвраты покупателей (шт)", snap.TotalReturnQty, "");
            AddKvMoney(sb, r++, "Выручка по реализации WB (Пр, нетто), BYN", snap.TotalRealizedP, "Продажа минус возврат (база для расчёта УСН по договору комиссии)");
            AddKvMoney(sb, r++, "К перечислению продавцу (AH, нетто), BYN", snap.TotalToPayGoods, "Выручка к перечислению после удержания комиссий WB");
            AddKvMoney(sb, r++, "Вознаграждение WB (с НДС), BYN", snap.TotalCommissionWithVat, "");
            AddKvMoney(sb, r++, "Услуги логистики (доставка), BYN", snap.TotalLogistics, "");
            AddKvMoney(sb, r++, "Хранение, BYN", snap.TotalStorage, "");
            AddKvMoney(sb, r++, "Штрафы WB, BYN", snap.TotalPenalties, "");
            AddKvMoney(sb, r++, "Прочие удержания / приёмка / лояльность, BYN", snap.TotalHoldings + snap.TotalAcceptance + snap.TotalLoyalty, "");
            AddKvBoldMoney(sb, r++, "ИТОГО расчётная выплата на р/с от WB, BYN", snap.TotalExpectedPayout, "AH(нетто) - Логистика - Штрафы - Хранение - Удержания");
            AddKvMoney(sb, r++, "Себестоимость реализованных SKU (по справочнику), BYN", snap.TotalCostOfGoods, "Заполните цены закупки в Buhly для точной чистой прибыли");
            AddKvMoney(sb, r++, string.Format(CultureInfo.InvariantCulture, "Ориентир налога УСН ({0:0.##}% от реализации Пр), BYN", snap.TaxRatePercent), snap.EstimatedTaxByRealization, "Информативный ориентир, итоговую базу проверяет бухгалтер");
            AddKvBoldMoney(sb, r++, "Чистая прибыль по товарам (до аренды/ФОТ), BYN", snap.EstimatedNetProfitBeforeFixed, "Выплата WB - Себестоимость - УСН");

            if (snap.BankTransactions.Count > 0)
            {
                r++;
                AddKvHdr(sb, r++, "Банковская выписка (ДДС и сверка)", "Сумма, BYN", "По данным загруженной выписки");
                AddKvBoldMoney(sb, r++, "Поступило на р/с от ООО «ИМВБРБ» / WB", snap.BankWbCredits, "Расхождение с расчётом по отчётам WB: " + (snap.BankWbCredits - snap.TotalExpectedPayout).ToString("0.00", CultureInfo.InvariantCulture) + " BYN");
                AddKvMoney(sb, r++, "Прочие поступления на р/с", snap.BankOtherCredits, "");
                AddKvMoney(sb, r++, "Уплачено налогов, ФСЗН и Белгосстрах", snap.BankTaxesDebet, "");
                AddKvMoney(sb, r++, "Оплачено поставщикам и прочие расходы", snap.BankVendorsDebet, "");
                AddKvMoney(sb, r++, "Выплаты физлицам / зарплата", snap.BankPayrollDebet, "");
                AddKvMoney(sb, r++, "Комиссии банка", snap.BankFeesDebet, "");
                AddKvBoldMoney(sb, r++, "Чистый денежный поток (Приход - Расход)", snap.BankTotalCredit - snap.BankTotalDebet, "Всего операций в выписке: " + snap.BankTransactions.Count);
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string BuildReportsSheetXml(AnalyticsSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"14\" customWidth=\"1\"/><col min=\"2\" max=\"3\" width=\"12\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"18\" customWidth=\"1\"/><col min=\"5\" max=\"6\" width=\"10\" customWidth=\"1\"/><col min=\"7\" max=\"13\" width=\"14\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            string[] h = { "№ отчёта", "С даты", "По дату", "Страны", "Прод., шт", "Возвр., шт", "Реал. Пр", "К перечисл.", "ВВ (с НДС)", "Логистика", "Хранение", "Штрафы", "Удерж./Пр.", "К выплате" };
            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "1", 2, h[c]));
            sb.Append("</row>");

            for (int i = 0; i < snap.Reports.Count; i++)
            {
                int rn = i + 2;
                var x = snap.Reports[i];
                sb.Append("<row r=\"" + rn + "\">");
                sb.Append(SC("A" + rn, 3, x.ReportNumber));
                sb.Append(SC("B" + rn, 3, x.DateFrom));
                sb.Append(SC("C" + rn, 3, x.DateTo));
                sb.Append(SC("D" + rn, 3, x.Countries));
                sb.Append(NC("E" + rn, 5, x.SaleQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(NC("F" + rn, 5, x.ReturnQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(MC("G" + rn, 4, x.RealizedP));
                sb.Append(MC("H" + rn, 4, x.ToPayGoods));
                sb.Append(MC("I" + rn, 4, x.CommissionWithVat));
                sb.Append(MC("J" + rn, 4, x.Logistics));
                sb.Append(MC("K" + rn, 4, x.Storage));
                sb.Append(MC("L" + rn, 4, x.Penalties));
                sb.Append(MC("M" + rn, 4, x.Holdings + x.Acceptance + x.Loyalty));
                sb.Append(MC("N" + rn, 7, x.ExpectedPayout));
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string BuildSkusSheetXml(AnalyticsSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"24\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"38\" customWidth=\"1\"/><col min=\"3\" max=\"5\" width=\"10\" customWidth=\"1\"/><col min=\"6\" max=\"13\" width=\"13\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            string[] h = { "Артикул", "Название", "Прод.", "Возвр.", "Выкуп %", "Реал. Пр", "К перечисл.", "Логистика", "Штрафы/Хран.", "От WB чист.", "Себест./шт", "Себест. итого", "Налог УСН", "Прибыль" };
            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "1", 2, h[c]));
            sb.Append("</row>");

            for (int i = 0; i < snap.Skus.Count; i++)
            {
                int rn = i + 2;
                var s = snap.Skus[i];
                sb.Append("<row r=\"" + rn + "\">");
                sb.Append(SC("A" + rn, 3, s.Article));
                sb.Append(SC("B" + rn, 3, s.Title));
                sb.Append(NC("C" + rn, 5, s.SaleQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(NC("D" + rn, 5, s.ReturnQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(MC("E" + rn, 4, s.BuyoutPercent));
                sb.Append(MC("F" + rn, 4, s.RealizedP));
                sb.Append(MC("G" + rn, 4, s.ToPayGoods));
                sb.Append(MC("H" + rn, 4, s.Logistics));
                sb.Append(MC("I" + rn, 4, s.Penalties + s.StorageAndOther));
                sb.Append(MC("J" + rn, 4, s.NetFromWb));
                sb.Append(MC("K" + rn, 4, s.UnitCost));
                sb.Append(MC("L" + rn, 4, s.TotalCost));
                sb.Append(MC("M" + rn, 4, s.EstimatedTax));
                sb.Append(MC("N" + rn, 7, s.NetProfit));
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string BuildBankSheetXml(AnalyticsSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"22\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"42\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"13\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"8\" customWidth=\"1\"/><col min=\"5\" max=\"6\" width=\"15\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            string[] h = { "Категория", "Контрагент", "УНП", "Опер.", "Расход (Дебет)", "Приход (Кредит)" };
            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "1", 2, h[c]));
            sb.Append("</row>");

            for (int i = 0; i < snap.BankCounterparties.Count; i++)
            {
                int rn = i + 2;
                var cp = snap.BankCounterparties[i];
                sb.Append("<row r=\"" + rn + "\">");
                sb.Append(SC("A" + rn, 3, cp.Category));
                sb.Append(SC("B" + rn, 3, cp.Counterparty));
                sb.Append(SC("C" + rn, 3, cp.Unp));
                sb.Append(NC("D" + rn, 5, cp.Count.ToString(CultureInfo.InvariantCulture)));
                sb.Append(MC("E" + rn, 4, cp.Debet));
                sb.Append(MC("F" + rn, 4, cp.Credit));
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static void AddKvHdr(StringBuilder sb, int r, string a, string b, string c)
        {
            sb.Append("<row r=\"" + r + "\" ht=\"20\" customHeight=\"1\">")
                .Append(SC("A" + r, 2, a)).Append(SC("B" + r, 2, b)).Append(SC("C" + r, 2, c)).Append("</row>");
        }
        private static void AddKvInt(StringBuilder sb, int r, string a, int val, string c)
        {
            sb.Append("<row r=\"" + r + "\">").Append(SC("A" + r, 3, a)).Append(NC("B" + r, 5, val.ToString(CultureInfo.InvariantCulture))).Append(SC("C" + r, 3, c)).Append("</row>");
        }
        private static void AddKvMoney(StringBuilder sb, int r, string a, decimal val, string c)
        {
            sb.Append("<row r=\"" + r + "\">").Append(SC("A" + r, 3, a)).Append(MC("B" + r, 4, val)).Append(SC("C" + r, 3, c)).Append("</row>");
        }
        private static void AddKvBoldMoney(StringBuilder sb, int r, string a, decimal val, string c)
        {
            sb.Append("<row r=\"" + r + "\">").Append(SC("A" + r, 6, a)).Append(MC("B" + r, 7, val)).Append(SC("C" + r, 6, c)).Append("</row>");
        }

        private static string ColName(int idx)
        {
            string res = "";
            while (idx > 0)
            {
                int rem = (idx - 1) % 26;
                res = (char)('A' + rem) + res;
                idx = (idx - 1) / 26;
            }
            return res;
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private static string SC(string r, int s, string val)
        {
            return "<c r=\"" + r + "\" s=\"" + s + "\" t=\"inlineStr\"><is><t>" + Esc(val) + "</t></is></c>";
        }

        private static string NC(string r, int s, string num)
        {
            return "<c r=\"" + r + "\" s=\"" + s + "\"><v>" + num + "</v></c>";
        }

        private static string MC(string r, int s, decimal val)
        {
            return "<c r=\"" + r + "\" s=\"" + s + "\"><v>" + Math.Round(val, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture) + "</v></c>";
        }

        private static void WriteZip(ZipArchive zip, string path, string content)
        {
            var e = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var sw = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                sw.Write(content);
        }
    }
}
