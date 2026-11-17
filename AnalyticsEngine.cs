using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GovPortalsApp
{
    public class WbReportSummary
    {
        public string Marketplace { get; set; }
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
        public decimal Compensations { get; set; }
        public decimal CommissionWithVat { get; set; }
        public decimal Logistics { get; set; }
        public decimal Storage { get; set; }
        public decimal Penalties { get; set; }
        public decimal Holdings { get; set; }
        public decimal Acceptance { get; set; }
        public decimal Loyalty { get; set; }
        public decimal ExpectedPayout { get; set; }

        public WbReportSummary()
        {
            Marketplace = "WB";
        }
    }

    public class SkuUnitStats
    {
        public string Marketplace { get; set; }
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

        public SkuUnitStats()
        {
            Marketplace = "WB";
        }

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
        public string SourceFormat { get; set; }
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
        public int LoadedOzonFiles { get; set; }
        public int DeduplicatedSkipped { get; set; }
        public int LoadedBankFiles { get; set; }

        public string TaxRegime { get; set; }
        public string TaxRegimeTitle { get; set; }
        public decimal TaxRatePercent { get; set; }
        public decimal EffectiveTaxRateApplied { get; set; }
        public decimal TaxBaseAmount { get; set; }

        public List<WbReportSummary> Reports { get; set; }
        public List<SkuUnitStats> Skus { get; set; }
        public List<BankTxItem> BankTransactions { get; set; }
        public List<BankCounterpartyTotal> BankCounterparties { get; set; }

        public int WbReportCount { get; set; }
        public int OzonReportCount { get; set; }
        public decimal WbExpectedPayout { get; set; }
        public decimal OzonExpectedPayout { get; set; }

        public int TotalSaleQty { get; set; }
        public int TotalReturnQty { get; set; }
        public decimal TotalRealizedP { get; set; }
        public decimal TotalCommissionWithVat { get; set; }
        public decimal TotalToPayGoods { get; set; }
        public decimal TotalCompensations { get; set; }
        public decimal TotalLogistics { get; set; }
        public decimal TotalStorage { get; set; }
        public decimal TotalPenalties { get; set; }
        public decimal TotalHoldings { get; set; }
        public decimal TotalAcceptance { get; set; }
        public decimal TotalLoyalty { get; set; }
        public decimal TotalExpectedPayout { get; set; }
        public decimal MpServicesExpenses { get; set; }

        public decimal TotalCostOfGoods { get; set; }
        public decimal EstimatedTaxByRealization { get; set; }
        public decimal EstimatedNetProfitBeforeFixed { get; set; }

        // IP (Подоходный 20%/30% + ФСЗН 35%) breakdown
        public decimal IpGrossIncome { get; set; }
        public decimal IpBankDeductibleExpenses { get; set; }
        public decimal IpBankGoodsPayments { get; set; }
        public decimal IpBankOtherVendorExpenses { get; set; }
        public bool IpHasPotentialDoubleGoodsCost { get; set; }
        public decimal IpDocumentedExpenses { get; set; }
        public decimal IpStandardDeduction20 { get; set; }
        public decimal IpNetBaseDocumented { get; set; }
        public decimal IpNetBaseStandard20 { get; set; }
        public decimal IpTaxDocumented { get; set; }
        public decimal IpTaxStandard20 { get; set; }
        public bool IpUsedStandardDeduction { get; set; }
        public bool IpHighIncomeRateApplied { get; set; }
        public bool IpNear500kLimit { get; set; }
        public decimal IpFsznBase { get; set; }
        public bool IpFsznCapped12Mzp { get; set; }
        public decimal IpFszn35 { get; set; }
        public string PeriodEndDate { get; set; }

        // OSN (НДС 20% + Прибыль 20%) breakdown
        public decimal OsnVat20 { get; set; }
        public decimal OsnProfitTaxBase { get; set; }
        public decimal OsnProfitTax20 { get; set; }

        // Bank aggregates
        public decimal BankWbCredits { get; set; }
        public decimal BankOzonCredits { get; set; }
        public decimal BankMarketplaceCredits { get; set; }
        public decimal BankOtherCredits { get; set; }
        public decimal BankNonTaxableCredits { get; set; }
        public decimal BankTotalCredit { get; set; }
        public decimal BankTaxesDebet { get; set; }
        public decimal BankVendorsDebet { get; set; }
        public decimal BankPayrollDebet { get; set; }
        public decimal BankFeesDebet { get; set; }
        public decimal BankTotalDebet { get; set; }

        public List<string> CalculationAuditDetails { get; set; }

        public AnalyticsSnapshot()
        {
            TaxRegime = CompanyProfilesManager.RegimeUsn6;
            TaxRegimeTitle = "ООО • УСН 6% (без НДС)";
            TaxRatePercent = 6.0m;
            EffectiveTaxRateApplied = 6.0m;
            Reports = new List<WbReportSummary>();
            Skus = new List<SkuUnitStats>();
            BankTransactions = new List<BankTxItem>();
            BankCounterparties = new List<BankCounterpartyTotal>();
            CalculationAuditDetails = new List<string>();
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
            return AnalyzeFiles(paths, CompanyProfilesManager.RegimeUsn6, taxRatePercent, skuCosts);
        }

        public static AnalyticsSnapshot AnalyzeFiles(IEnumerable<string> paths, string taxRegime, decimal taxRatePercent, Dictionary<string, decimal> skuCosts)
        {
            string normRegime = string.IsNullOrWhiteSpace(taxRegime) ? CompanyProfilesManager.RegimeUsn6 : taxRegime;
            var snap = new AnalyticsSnapshot
            {
                TaxRegime = normRegime,
                TaxRegimeTitle = GetRegimeDisplayTitle(normRegime, taxRatePercent),
                TaxRatePercent = taxRatePercent,
                EffectiveTaxRateApplied = taxRatePercent
            };
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
                            string ename = entry.FullName ?? "";
                            if (ename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                            {
                                using (var ms = new MemoryStream())
                                using (var es = entry.Open())
                                {
                                    es.CopyTo(ms);
                                    ms.Position = 0;
                                    ProcessXlsxStream(ms, Path.GetFileName(file) + " / " + entry.Name, snap, seenReports, skuDict, bankTxKeys);
                                }
                            }
                            else if (ename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || ename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                            {
                                using (var ms = new MemoryStream())
                                using (var es = entry.Open())
                                {
                                    es.CopyTo(ms);
                                    ProcessTextBytes(ms.ToArray(), Path.GetFileName(file) + " / " + entry.Name, snap, seenReports, skuDict, bankTxKeys);
                                }
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
                else if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessTextBytes(File.ReadAllBytes(file), Path.GetFileName(file), snap, seenReports, skuDict, bankTxKeys);
                }
            }

            // 1. Aggregate Marketplace Reports (WB + Ozon)
            DateTime maxPeriodDt = DateTime.MinValue;
            foreach (var rep in snap.Reports)
            {
                DateTime rDt = ParseFlexibleDate(rep.DateTo);
                if (rDt == DateTime.MinValue) rDt = ParseFlexibleDate(rep.DateFrom);
                if (rDt > maxPeriodDt) maxPeriodDt = rDt;

                snap.TotalSaleQty += rep.SaleQty;
                snap.TotalReturnQty += rep.ReturnQty;
                snap.TotalRealizedP += rep.RealizedP;
                snap.TotalCommissionWithVat += rep.CommissionWithVat;
                snap.TotalToPayGoods += rep.ToPayGoods;
                snap.TotalCompensations += rep.Compensations;
                snap.TotalLogistics += rep.Logistics;
                snap.TotalStorage += rep.Storage;
                snap.TotalPenalties += rep.Penalties;
                snap.TotalHoldings += rep.Holdings;
                snap.TotalAcceptance += rep.Acceptance;
                snap.TotalLoyalty += rep.Loyalty;
                snap.TotalExpectedPayout += rep.ExpectedPayout;

                if (string.Equals(rep.Marketplace, "Ozon", StringComparison.OrdinalIgnoreCase))
                {
                    snap.OzonReportCount++;
                    snap.OzonExpectedPayout += rep.ExpectedPayout;
                }
                else
                {
                    snap.WbReportCount++;
                    snap.WbExpectedPayout += rep.ExpectedPayout;
                }
            }

            snap.MpServicesExpenses = Math.Max(0m, snap.TotalRealizedP + snap.TotalCompensations - snap.TotalExpectedPayout);

            // 2. Aggregate Bank Transactions
            var cpMap = new Dictionary<string, BankCounterpartyTotal>(StringComparer.OrdinalIgnoreCase);
            foreach (var tx in snap.BankTransactions)
            {
                if (tx.ParsedDate > maxPeriodDt) maxPeriodDt = tx.ParsedDate;

                snap.BankTotalCredit += tx.Credit;
                snap.BankTotalDebet += tx.Debet;

                if (tx.Category == "WB / Маркетплейсы")
                {
                    snap.BankMarketplaceCredits += tx.Credit;
                    if (IsOzonCounterparty(tx.Counterparty, tx.Purpose))
                        snap.BankOzonCredits += tx.Credit;
                    else
                        snap.BankWbCredits += tx.Credit;
                }
                else if (tx.Category == "Необлагаемые поступления")
                {
                    snap.BankNonTaxableCredits += tx.Credit;
                }
                else if (tx.Credit > 0m)
                {
                    snap.BankOtherCredits += tx.Credit;
                }

                if (tx.Category == "Налоги и ФСЗН") snap.BankTaxesDebet += tx.Debet;
                else if (tx.Category == "Зарплата и выплаты") snap.BankPayrollDebet += tx.Debet;
                else if (tx.Category == "Комиссия банка") snap.BankFeesDebet += tx.Debet;
                else if (tx.Debet > 0m)
                {
                    snap.BankVendorsDebet += tx.Debet;
                    if (IsGoodsPurchasePurpose(tx.Purpose))
                        snap.IpBankGoodsPayments += tx.Debet;
                    else
                        snap.IpBankOtherVendorExpenses += tx.Debet;
                }

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

            snap.PeriodEndDate = (maxPeriodDt != DateTime.MinValue ? maxPeriodDt : DateTime.Today).ToString("dd.MM.yyyy");

            snap.BankCounterparties = cpMap.Values
                .OrderByDescending(c => c.Credit + c.Debet)
                .ToList();

            // 3. SKU unit economics & cost of goods
            decimal skuCostTotal = 0m;
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
                skuCostTotal += sku.TotalCost;
            }
            snap.TotalCostOfGoods = skuCostTotal;

            // 4. Tax Regime Calculation (unified base: RealizedP + TotalCompensations + taxable bank credits)
            decimal grossTaxableIncome = snap.TotalRealizedP + snap.TotalCompensations + snap.BankOtherCredits;
            // If no marketplace reports loaded at all, include marketplace bank credits in gross income
            if (snap.Reports.Count == 0 && snap.BankMarketplaceCredits > 0m)
                grossTaxableIncome += snap.BankMarketplaceCredits;

            if (normRegime == CompanyProfilesManager.RegimeIpPn20)
            {
                snap.IpGrossIncome = grossTaxableIncome;
                snap.IpHasPotentialDoubleGoodsCost = (snap.TotalCostOfGoods > 0m && snap.IpBankGoodsPayments > 0m);
                decimal vendorBankForIp = snap.IpHasPotentialDoubleGoodsCost ? snap.IpBankOtherVendorExpenses : snap.BankVendorsDebet;
                snap.IpBankDeductibleExpenses = vendorBankForIp + snap.BankFeesDebet + snap.BankPayrollDebet;
                snap.IpDocumentedExpenses = snap.MpServicesExpenses + snap.TotalCostOfGoods + snap.IpBankDeductibleExpenses;
                snap.IpStandardDeduction20 = Math.Round(grossTaxableIncome * 0.20m, 2, MidpointRounding.AwayFromZero);

                snap.IpNetBaseDocumented = Math.Max(0m, grossTaxableIncome - snap.IpDocumentedExpenses);
                snap.IpNetBaseStandard20 = Math.Max(0m, grossTaxableIncome - snap.IpStandardDeduction20);

                decimal effectiveIpRate = taxRatePercent;
                snap.IpNear500kLimit = (grossTaxableIncome > 400000m && grossTaxableIncome <= 500000m);
                if (grossTaxableIncome > 500000m && Math.Abs(taxRatePercent - 20m) < 0.01m)
                {
                    effectiveIpRate = 30m;
                    snap.IpHighIncomeRateApplied = true;
                }
                else if (effectiveIpRate >= 30m && grossTaxableIncome > 500000m)
                {
                    snap.IpHighIncomeRateApplied = true;
                }
                snap.EffectiveTaxRateApplied = effectiveIpRate;

                decimal mult = Math.Max(0m, effectiveIpRate) / 100m;
                snap.IpTaxDocumented = Math.Round(snap.IpNetBaseDocumented * mult, 2, MidpointRounding.AwayFromZero);
                snap.IpTaxStandard20 = Math.Round(snap.IpNetBaseStandard20 * mult, 2, MidpointRounding.AwayFromZero);
                snap.IpUsedStandardDeduction = snap.IpNetBaseStandard20 < snap.IpNetBaseDocumented;

                snap.TaxBaseAmount = snap.IpUsedStandardDeduction ? snap.IpNetBaseStandard20 : snap.IpNetBaseDocumented;
                snap.EstimatedTaxByRealization = snap.IpUsedStandardDeduction ? snap.IpTaxStandard20 : snap.IpTaxDocumented;
                decimal fsznBase = Math.Min(snap.TaxBaseAmount, 10296m);
                snap.IpFsznBase = fsznBase;
                snap.IpFsznCapped12Mzp = snap.TaxBaseAmount > 10296m;
                snap.IpFszn35 = Math.Round(fsznBase * 0.35m, 2, MidpointRounding.AwayFromZero);
                snap.EstimatedNetProfitBeforeFixed = snap.TotalExpectedPayout - snap.TotalCostOfGoods - snap.EstimatedTaxByRealization - snap.IpFszn35;
            }
            else if (normRegime == CompanyProfilesManager.RegimeOsn20)
            {
                snap.TaxBaseAmount = grossTaxableIncome;
                snap.OsnVat20 = Math.Round(grossTaxableIncome * 20m / 120m, 2, MidpointRounding.AwayFromZero);
                decimal revExVat = grossTaxableIncome - snap.OsnVat20;
                decimal expenses = snap.MpServicesExpenses + snap.TotalCostOfGoods + snap.BankVendorsDebet + snap.BankFeesDebet + snap.BankPayrollDebet;
                snap.OsnProfitTaxBase = Math.Max(0m, revExVat - expenses);
                decimal profitRate = taxRatePercent > 0m ? taxRatePercent : 20m;
                snap.EffectiveTaxRateApplied = profitRate;
                snap.OsnProfitTax20 = Math.Round(snap.OsnProfitTaxBase * (profitRate / 100m), 2, MidpointRounding.AwayFromZero);
                snap.EstimatedTaxByRealization = snap.OsnVat20 + snap.OsnProfitTax20;
                snap.EstimatedNetProfitBeforeFixed = snap.TotalExpectedPayout - snap.TotalCostOfGoods - snap.EstimatedTaxByRealization;
            }
            else
            {
                // ООО • УСН 6% (без НДС) or CUSTOM
                decimal rate = Math.Max(0m, taxRatePercent);
                snap.EffectiveTaxRateApplied = rate;
                snap.TaxBaseAmount = grossTaxableIncome;
                snap.EstimatedTaxByRealization = Math.Round(snap.TaxBaseAmount * (rate / 100m), 2, MidpointRounding.AwayFromZero);
                snap.EstimatedNetProfitBeforeFixed = snap.TotalExpectedPayout - snap.TotalCostOfGoods - snap.EstimatedTaxByRealization;
            }

            // 5. Per-SKU tax & net profit
            decimal skuTaxMult = Math.Max(0m, snap.EffectiveTaxRateApplied) / 100m;
            foreach (var sku in skuDict.Values)
            {
                if (normRegime == CompanyProfilesManager.RegimeIpPn20)
                {
                    decimal skuNet = Math.Max(0m, sku.NetFromWb - sku.TotalCost);
                    sku.EstimatedTax = Math.Round(skuNet * skuTaxMult, 2, MidpointRounding.AwayFromZero);
                    sku.NetProfit = sku.NetFromWb - sku.TotalCost - sku.EstimatedTax;
                }
                else if (normRegime == CompanyProfilesManager.RegimeOsn20)
                {
                    decimal skuVat = Math.Round(Math.Max(0m, sku.RealizedP) * 20m / 120m, 2, MidpointRounding.AwayFromZero);
                    decimal skuProfitBase = Math.Max(0m, sku.NetFromWb - skuVat - sku.TotalCost);
                    decimal skuProfitTax = Math.Round(skuProfitBase * skuTaxMult, 2, MidpointRounding.AwayFromZero);
                    sku.EstimatedTax = skuVat + skuProfitTax;
                    sku.NetProfit = sku.NetFromWb - sku.TotalCost - sku.EstimatedTax;
                }
                else
                {
                    sku.EstimatedTax = Math.Round(Math.Max(0m, sku.RealizedP) * skuTaxMult, 2, MidpointRounding.AwayFromZero);
                    sku.NetProfit = sku.NetFromWb - sku.TotalCost - sku.EstimatedTax;
                }
            }

            snap.Skus = skuDict.Values
                .OrderByDescending(s => s.RealizedP)
                .ThenByDescending(s => s.NetFromWb)
                .ToList();

            snap.Reports = snap.Reports
                .OrderBy(r => r.DateFrom, StringComparer.Ordinal)
                .ThenBy(r => r.Marketplace, StringComparer.Ordinal)
                .ThenBy(r => r.ReportNumber, StringComparer.Ordinal)
                .ToList();

            BuildCalculationAuditDetails(snap, grossTaxableIncome);

            return snap;
        }

        private static string FmtAudit(decimal val)
        {
            return val.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private static void BuildCalculationAuditDetails(AnalyticsSnapshot snap, decimal grossTaxableIncome)
        {
            if (snap == null) return;
            snap.CalculationAuditDetails.Clear();

            snap.CalculationAuditDetails.Add(string.Format(
                CultureInfo.InvariantCulture,
                "1) Источники данных: отчётов WB — {0} (файлов: {1}), отчётов Ozon — {2} (файлов: {3}), дубликатов пропущено — {4}, операций банка — {5} (файлов выписки: {6}).",
                snap.WbReportCount, snap.LoadedWbFiles, snap.OzonReportCount, snap.LoadedOzonFiles,
                snap.DeduplicatedSkipped, snap.BankTransactions.Count, snap.LoadedBankFiles));

            decimal otherMpHoldings = snap.TotalPenalties + snap.TotalHoldings + snap.TotalAcceptance + snap.TotalLoyalty;
            snap.CalculationAuditDetails.Add(string.Format(
                "2) Маркетплейсы (WB + Ozon): продажи — {0} шт., возвраты — {1} шт. Реализация (Пр) = {2} BYN; компенсации/корр. = {3} BYN; комиссия МП (с НДС) = {4} BYN; логистика = {5} BYN; хранение = {6} BYN; штрафы/удержания/приёмка/лояльность = {7} BYN; итого удержано МП = {8} BYN; к перечислению на р/с = {9} BYN (WB: {10}, Ozon: {11}).",
                snap.TotalSaleQty, snap.TotalReturnQty,
                FmtAudit(snap.TotalRealizedP), FmtAudit(snap.TotalCompensations),
                FmtAudit(snap.TotalCommissionWithVat), FmtAudit(snap.TotalLogistics),
                FmtAudit(snap.TotalStorage), FmtAudit(otherMpHoldings),
                FmtAudit(snap.MpServicesExpenses), FmtAudit(snap.TotalExpectedPayout),
                FmtAudit(snap.WbExpectedPayout), FmtAudit(snap.OzonExpectedPayout)));

            if (snap.BankTransactions.Count > 0)
            {
                snap.CalculationAuditDetails.Add(string.Format(
                    "3) Выписка банка: приход всего = {0} BYN, списано всего = {1} BYN. Поступления от МП = {2} BYN (WB: {3}, Ozon: {4}); прочие облагаемые поступления = {5} BYN; необлагаемые поступления = {6} BYN. Списания: поставщики/товары всего = {7} BYN (оплата товара: {8}, прочие: {9}), комиссия банка = {10} BYN, налоги и ФСЗН = {11} BYN, зарплата/выплаты = {12} BYN.",
                    FmtAudit(snap.BankTotalCredit), FmtAudit(snap.BankTotalDebet),
                    FmtAudit(snap.BankMarketplaceCredits), FmtAudit(snap.BankWbCredits), FmtAudit(snap.BankOzonCredits),
                    FmtAudit(snap.BankOtherCredits), FmtAudit(snap.BankNonTaxableCredits),
                    FmtAudit(snap.BankVendorsDebet), FmtAudit(snap.IpBankGoodsPayments), FmtAudit(snap.IpBankOtherVendorExpenses),
                    FmtAudit(snap.BankFeesDebet), FmtAudit(snap.BankTaxesDebet), FmtAudit(snap.BankPayrollDebet)));
            }
            else
            {
                snap.CalculationAuditDetails.Add("3) Выписка банка: файлы выписки не загружены — расчёт выполнен по отчётам маркетплейсов и справочнику себестоимости SKU.");
            }

            string noReportsBankIncomeNote = (snap.Reports.Count == 0 && snap.BankMarketplaceCredits > 0m)
                ? string.Format(" + приход от МП по р/с ({0})", FmtAudit(snap.BankMarketplaceCredits))
                : "";

            if (snap.TaxRegime == CompanyProfilesManager.RegimeIpPn20)
            {
                string doubleGoodsExplain = snap.IpHasPotentialDoubleGoodsCost
                    ? string.Format("защита от двойного списания активна — оплаты за товар с р/с ({0} BYN) исключены, т.к. учтена себестоимость проданных SKU ({1} BYN); с р/с взяты только прочие расходы + комиссии банка + ФОТ = {2} BYN",
                        FmtAudit(snap.IpBankGoodsPayments), FmtAudit(snap.TotalCostOfGoods), FmtAudit(snap.IpBankDeductibleExpenses))
                    : string.Format("с р/с (поставщики + комиссия банка + ФОТ) = {0} BYN", FmtAudit(snap.IpBankDeductibleExpenses));

                string limitNote = snap.IpHighIncomeRateApplied || snap.IpGrossIncome > 500000m
                    ? " Превышен порог 500 000 BYN: ставка 30% и с 01.01 следующего года обязателен переход в юрлицо (Закон № 365-З)."
                    : (snap.IpNear500kLimit ? " Внимание: доход > 400 000 BYN (приближение к лимиту 500 000 BYN, при превышении — ставка 30% и переход в юрлицо)." : "");

                snap.CalculationAuditDetails.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "4а) Подоходный налог ИП ({0:0.##}%): Валовой доход = Реализация ({1}) + Компенсации ({2}) + Облаг. р/с ({3}){4} = {5} BYN.\r\n    • Докум. расходы = Удержания МП ({6}) + Закупка SKU ({7}) + Р/с ({8}: {9}) = {10} BYN → База = {11} BYN → Налог = {12} BYN.\r\n    • Вычет 20% = 20% × {5} = {13} BYN → База = {14} BYN → Налог = {15} BYN.\r\n    • Выбрано: {16} → Подоходный налог = {17} BYN.{18}",
                    snap.EffectiveTaxRateApplied,
                    FmtAudit(snap.TotalRealizedP), FmtAudit(snap.TotalCompensations), FmtAudit(snap.BankOtherCredits),
                    noReportsBankIncomeNote, FmtAudit(snap.IpGrossIncome),
                    FmtAudit(snap.MpServicesExpenses), FmtAudit(snap.TotalCostOfGoods), FmtAudit(snap.IpBankDeductibleExpenses), doubleGoodsExplain, FmtAudit(snap.IpDocumentedExpenses),
                    FmtAudit(snap.IpNetBaseDocumented), FmtAudit(snap.IpTaxDocumented),
                    FmtAudit(snap.IpStandardDeduction20), FmtAudit(snap.IpNetBaseStandard20), FmtAudit(snap.IpTaxStandard20),
                    snap.IpUsedStandardDeduction ? "стандартный вычет 20%" : "документальные расходы",
                    FmtAudit(snap.EstimatedTaxByRealization), limitNote));

                string capNote = snap.IpFsznCapped12Mzp
                    ? "достигнут потолок 12 МЗП (10 296,00 BYN в 2026 г. при МЗП 858 BYN → макс. 3 603,60 BYN/год, Закон № 90-З)"
                    : "ниже потолка 12 МЗП (10 296,00 BYN в 2026 г. при МЗП 858 BYN → макс. 3 603,60 BYN/год, Закон № 90-З)";
                snap.CalculationAuditDetails.Add(string.Format(
                    "4б) ФСЗН ИП 35% «за себя»: база = min(выбранная налоговая база {0}, 10 296,00) = {1} BYN ({2}) → 35% × {1} = {3} BYN.",
                    FmtAudit(snap.TaxBaseAmount), FmtAudit(snap.IpFsznBase), capNote, FmtAudit(snap.IpFszn35)));
            }
            else if (snap.TaxRegime == CompanyProfilesManager.RegimeOsn20)
            {
                decimal revExVat = grossTaxableIncome - snap.OsnVat20;
                decimal osnExpenses = snap.MpServicesExpenses + snap.TotalCostOfGoods + snap.BankVendorsDebet + snap.BankFeesDebet + snap.BankPayrollDebet;
                snap.CalculationAuditDetails.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "4) Налоги ООО • ОСН: Выручка с НДС = Реализация ({0}) + Компенсации ({1}) + Облаг. р/с ({2}){3} = {4} BYN.\r\n    • Исходящий НДС (20/120) = {4} × 20 / 120 = {5} BYN (без учёта входящего вычета по ЭСЧФ).\r\n    • Выручка без НДС = {4} − {5} = {6} BYN; Расходы (МП + закупка SKU + р/с) = {7} BYN.\r\n    • База налога на прибыль = max(0; {6} − {7}) = {8} BYN → Налог на прибыль ({9:0.##}%) = {10} BYN.\r\n    • Итого налоговая нагрузка ОСН = {5} + {10} = {11} BYN.",
                    FmtAudit(snap.TotalRealizedP), FmtAudit(snap.TotalCompensations), FmtAudit(snap.BankOtherCredits),
                    noReportsBankIncomeNote, FmtAudit(grossTaxableIncome),
                    FmtAudit(snap.OsnVat20), FmtAudit(revExVat), FmtAudit(osnExpenses),
                    FmtAudit(snap.OsnProfitTaxBase), snap.EffectiveTaxRateApplied, FmtAudit(snap.OsnProfitTax20),
                    FmtAudit(snap.EstimatedTaxByRealization)));
            }
            else
            {
                string regimePrefix = snap.TaxRegime == CompanyProfilesManager.RegimeCustom
                    ? "Особый режим"
                    : "ООО • УСН";
                snap.CalculationAuditDetails.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "4) Налог ({0} {1:0.##}%): База = Реализация МП ({2}) + Компенсации МП ({3}) + Облагаемые поступления р/с ({4}){5} = {6} BYN → Налог = {6} × {1:0.##}% = {7} BYN.",
                    regimePrefix, snap.EffectiveTaxRateApplied,
                    FmtAudit(snap.TotalRealizedP), FmtAudit(snap.TotalCompensations), FmtAudit(snap.BankOtherCredits),
                    noReportsBankIncomeNote, FmtAudit(snap.TaxBaseAmount), FmtAudit(snap.EstimatedTaxByRealization)));
            }

            string fsznSubtractNote = snap.TaxRegime == CompanyProfilesManager.RegimeIpPn20
                ? (" − ФСЗН (" + FmtAudit(snap.IpFszn35) + ")")
                : "";
            snap.CalculationAuditDetails.Add(string.Format(
                "5) Чистая прибыль до ФОТ и аренды = К перечислению МП ({0}) − Себестоимость SKU ({1}) − Налог ({2}){3} = {4} BYN.",
                FmtAudit(snap.TotalExpectedPayout), FmtAudit(snap.TotalCostOfGoods),
                FmtAudit(snap.EstimatedTaxByRealization), fsznSubtractNote, FmtAudit(snap.EstimatedNetProfitBeforeFixed)));

            if (snap.BankTransactions.Count > 0 && snap.Reports.Count > 0)
            {
                decimal diff = snap.BankMarketplaceCredits - snap.TotalExpectedPayout;
                string diffPrefix = diff > 0m ? "+" : "";
                string diffStatus = Math.Abs(diff) < 1.0m
                    ? "выплаты сошлись"
                    : "расхождение обычно из-за переходящих выплат на границе периода";
                snap.CalculationAuditDetails.Add(string.Format(
                    "6) Сверка р/с vs отчёты МП: Поступило от МП на р/с ({0}) − Ожидалось по отчётам МП ({1}) = {2}{3} BYN ({4}).",
                    FmtAudit(snap.BankMarketplaceCredits), FmtAudit(snap.TotalExpectedPayout),
                    diffPrefix, FmtAudit(diff), diffStatus));
            }
            else
            {
                snap.CalculationAuditDetails.Add("6) Сверка р/с vs отчёты МП: загрузите одновременно отчёты МП и банковскую выписку для формулы сверки (Поступило на р/с − К перечислению).");
            }
        }

        private static string GetRegimeDisplayTitle(string regime, decimal rate)
        {
            if (regime == CompanyProfilesManager.RegimeIpPn20)
                return "ИП • Подоходный налог (20% / 30% > 500 000 BYN) + ФСЗН 35%";
            if (regime == CompanyProfilesManager.RegimeOsn20)
                return "ООО • ОСН (НДС 20% + Налог на прибыль 20%)";
            if (regime == CompanyProfilesManager.RegimeCustom)
                return string.Format(CultureInfo.InvariantCulture, "Особый режим ({0:0.##}%)", rate);
            return "ООО • УСН 6% (без НДС)";
        }

        private static bool IsGoodsPurchasePurpose(string purpose)
        {
            if (string.IsNullOrWhiteSpace(purpose)) return false;
            string pUp = purpose.ToUpperInvariant();
            return pUp.Contains("ЗА ТОВАР") || pUp.Contains("ТОВАР") || pUp.Contains("ЗАКУПК") ||
                   pUp.Contains("НАКЛАДН") || pUp.Contains("ТТН") || pUp.Contains("УПД");
        }

        private static void ProcessTextBytes(
            byte[] rawBytes,
            string sourceLabel,
            AnalyticsSnapshot snap,
            HashSet<string> seenReports,
            Dictionary<string, SkuUnitStats> skuDict,
            HashSet<string> bankTxKeys)
        {
            if (rawBytes == null || rawBytes.Length == 0) return;

            string text = DecodeBankText(rawBytes);
            if (string.IsNullOrWhiteSpace(text)) return;

            if (text.IndexOf("1CClientBankExchange", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("СекцияДокумент=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (Parse1CClientBankExchange(text, snap, bankTxKeys))
                    snap.LoadedBankFiles++;
                return;
            }

            // Try CSV: check WB / Ozon reports before Bank statement
            var csvRows = ParseCsvRows(text);
            if (csvRows.Count >= 2)
            {
                int wbHdrIdx = DetectWbReportHeader(csvRows);
                if (wbHdrIdx >= 0)
                {
                    ParseWbReportRows(csvRows, wbHdrIdx, sourceLabel, snap, seenReports, skuDict);
                    snap.LoadedWbFiles++;
                    return;
                }

                int ozonHdrIdx = DetectOzonReportHeader(csvRows);
                if (ozonHdrIdx >= 0)
                {
                    ParseOzonReportRows(csvRows, ozonHdrIdx, sourceLabel, snap, seenReports, skuDict);
                    snap.LoadedOzonFiles++;
                    return;
                }

                int bankHdrIdx = DetectBankHeader(csvRows);
                if (bankHdrIdx >= 0)
                {
                    ParseBankRows(csvRows, bankHdrIdx, "CSV", snap, bankTxKeys);
                    snap.LoadedBankFiles++;
                }
            }
        }

        private static string DecodeBankText(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

            string asciiProbe = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
            if (asciiProbe.IndexOf("Кодировка=UTF-8", StringComparison.OrdinalIgnoreCase) >= 0 ||
                asciiProbe.IndexOf("UTF-8", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Encoding.UTF8.GetString(bytes);
            }
            if (asciiProbe.IndexOf("Кодировка=DOS", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try { return Encoding.GetEncoding(866).GetString(bytes); } catch { }
            }
            if (asciiProbe.IndexOf("Кодировка=Windows", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try { return Encoding.GetEncoding(1251).GetString(bytes); } catch { }
            }

            // Try strict UTF-8 first; if invalid bytes appear, fall back to Windows-1251
            try
            {
                var strictUtf8 = new UTF8Encoding(false, true);
                return strictUtf8.GetString(bytes);
            }
            catch
            {
                try { return Encoding.GetEncoding(1251).GetString(bytes); }
                catch { return Encoding.Default.GetString(bytes); }
            }
        }

        private static bool Parse1CClientBankExchange(string text, AnalyticsSnapshot snap, HashSet<string> bankTxKeys)
        {
            var lines = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            string ownAccount = "";
            bool inDoc = false;
            var doc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int parsedCount = 0;
            int occCounter = 0;

            foreach (var rawLine in lines)
            {
                string line = (rawLine ?? "").Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("КонецФайла", StringComparison.OrdinalIgnoreCase))
                    break;

                if (line.StartsWith("СекцияДокумент=", StringComparison.OrdinalIgnoreCase))
                {
                    inDoc = true;
                    doc.Clear();
                    continue;
                }

                if (line.Equals("КонецДокумента", StringComparison.OrdinalIgnoreCase))
                {
                    if (inDoc)
                    {
                        if (Add1CDocument(doc, ownAccount, snap, bankTxKeys, ref occCounter))
                            parsedCount++;
                        inDoc = false;
                        doc.Clear();
                    }
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                if (!inDoc)
                {
                    if (string.Equals(key, "РасчСчет", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                        ownAccount = val;
                }
                else
                {
                    string prev;
                    if (doc.TryGetValue(key, out prev) && !string.IsNullOrEmpty(prev))
                        doc[key] = prev + " " + val;
                    else
                        doc[key] = val;
                }
            }

            return parsedCount > 0;
        }

        private static bool Add1CDocument(
            Dictionary<string, string> doc,
            string ownAccount,
            AnalyticsSnapshot snap,
            HashSet<string> bankTxKeys,
            ref int occCounter)
        {
            string sumStr = GetDictVal(doc, "Сумма");
            decimal amount;
            if (!TryParseDec(sumStr, out amount) || amount == 0m) return false;
            amount = Math.Abs(amount);

            string dtIn = GetDictVal(doc, "ДатаПоступило");
            string dtOut = GetDictVal(doc, "ДатаСписано");
            string dtDoc = GetDictVal(doc, "Дата");
            string payerAcc = FirstNonEmpty(GetDictVal(doc, "ПлательщикСчет"), GetDictVal(doc, "ПлательщикРасчСчет"));
            string recAcc = FirstNonEmpty(GetDictVal(doc, "ПолучательСчет"), GetDictVal(doc, "ПолучательРасчСчет"));

            bool isCredit;
            if (!string.IsNullOrWhiteSpace(dtIn) && string.IsNullOrWhiteSpace(dtOut))
                isCredit = true;
            else if (!string.IsNullOrWhiteSpace(dtOut) && string.IsNullOrWhiteSpace(dtIn))
                isCredit = false;
            else if (!string.IsNullOrEmpty(ownAccount) && string.Equals(recAcc, ownAccount, StringComparison.OrdinalIgnoreCase))
                isCredit = true;
            else if (!string.IsNullOrEmpty(ownAccount) && string.Equals(payerAcc, ownAccount, StringComparison.OrdinalIgnoreCase))
                isCredit = false;
            else
                isCredit = false;

            string dtStr = FirstNonEmpty(isCredit ? dtIn : dtOut, dtDoc, dtIn, dtOut);
            if (string.IsNullOrWhiteSpace(dtStr)) return false;

            string num = GetDictVal(doc, "Номер");
            string corr = isCredit
                ? FirstNonEmpty(GetDictVal(doc, "Плательщик"), GetDictVal(doc, "Плательщик1"))
                : FirstNonEmpty(GetDictVal(doc, "Получатель"), GetDictVal(doc, "Получатель1"));
            string unp = isCredit
                ? FirstNonEmpty(GetDictVal(doc, "ПлательщикИНН"), GetDictVal(doc, "ПлательщикУНП"))
                : FirstNonEmpty(GetDictVal(doc, "ПолучательИНН"), GetDictVal(doc, "ПолучательУНП"));

            var purpParts = new List<string>();
            string p0 = GetDictVal(doc, "НазначениеПлатежа");
            if (!string.IsNullOrEmpty(p0)) purpParts.Add(p0);
            for (int k = 1; k <= 6; k++)
            {
                string pk = GetDictVal(doc, "НазначениеПлатежа" + k);
                if (!string.IsNullOrEmpty(pk)) purpParts.Add(pk);
            }
            string purp = string.Join(" ", purpParts);
            string bud = FirstNonEmpty(GetDictVal(doc, "КодНазПлатежа"), GetDictVal(doc, "КодПлатежа"), GetDictVal(doc, "КодБюджКлассиф"));

            decimal deb = isCredit ? 0m : amount;
            decimal cred = isCredit ? amount : 0m;

            occCounter++;
            string txKey = BuildBankTxKey(dtStr, num, deb, cred, unp, corr, purp, occCounter);
            if (!bankTxKeys.Add(txKey)) return true;

            DateTime parsedDt = ParseFlexibleDate(dtStr);
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
                Category = cat,
                SourceFormat = "1C"
            });
            return true;
        }

        private static string GetDictVal(Dictionary<string, string> d, string key)
        {
            string v;
            return d.TryGetValue(key, out v) ? (v ?? "").Trim() : "";
        }

        private static string FirstNonEmpty(params string[] vals)
        {
            foreach (var v in vals)
            {
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            return "";
        }

        private static List<Dictionary<int, string>> ParseCsvRows(string text)
        {
            var result = new List<Dictionary<int, string>>();
            var lines = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return result;

            char sep = ';';
            int semi = 0, tab = 0, comma = 0;
            for (int i = 0; i < Math.Min(10, lines.Length); i++)
            {
                foreach (char ch in lines[i])
                {
                    if (ch == ';') semi++;
                    else if (ch == '\t') tab++;
                    else if (ch == ',') comma++;
                }
            }
            if (tab > semi && tab > comma) sep = '\t';
            else if (comma > semi && semi == 0) sep = ',';

            foreach (var line in lines)
            {
                var fields = SplitCsvLine(line, sep);
                var dict = new Dictionary<int, string>();
                for (int c = 0; c < fields.Count; c++)
                    dict[c + 1] = fields[c];
                result.Add(dict);
            }
            return result;
        }

        private static List<string> SplitCsvLine(string line, char sep)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == sep && !inQuotes)
                {
                    list.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            list.Add(sb.ToString().Trim());
            return list;
        }

        private static void ProcessXlsxStream(
            Stream xlsxStream,
            string sourceLabel,
            AnalyticsSnapshot snap,
            HashSet<string> seenReports,
            Dictionary<string, SkuUnitStats> skuDict,
            HashSet<string> bankTxKeys)
        {
            var allSheets = ReadXlsxAllSheets(xlsxStream);
            foreach (var rows in allSheets)
            {
                if (rows == null || rows.Count < 2) continue;

                // 1. Bank Statement (flexible RB banks)
                int bankHdrIdx = DetectBankHeader(rows);
                if (bankHdrIdx >= 0)
                {
                    ParseBankRows(rows, bankHdrIdx, "Excel", snap, bankTxKeys);
                    snap.LoadedBankFiles++;
                    return;
                }

                // 2. WB Weekly Detailed Report
                int wbHdrIdx = DetectWbReportHeader(rows);
                if (wbHdrIdx >= 0)
                {
                    ParseWbReportRows(rows, wbHdrIdx, sourceLabel, snap, seenReports, skuDict);
                    snap.LoadedWbFiles++;
                    return;
                }

                // 3. Ozon Realization / Accruals Report
                int ozonHdrIdx = DetectOzonReportHeader(rows);
                if (ozonHdrIdx >= 0)
                {
                    ParseOzonReportRows(rows, ozonHdrIdx, sourceLabel, snap, seenReports, skuDict);
                    snap.LoadedOzonFiles++;
                    return;
                }
            }
        }

        private static int DetectBankHeader(List<Dictionary<int, string>> rows)
        {
            int limit = Math.Min(35, rows.Count);
            for (int i = 0; i < limit; i++)
            {
                bool hasDate = false, hasCorrOrPurp = false, hasAmount = false;
                foreach (var v in rows[i].Values)
                {
                    string h = NormalizeHeader(v);
                    if (string.IsNullOrEmpty(h)) continue;
                    if (h.Contains("дата операц") || h.Contains("дата провод") || h.Contains("дата док") ||
                        h.Contains("дата валют") || h == "дата" || h == "date")
                        hasDate = true;
                    if (h.Contains("корреспондент") || h.Contains("контрагент") || h.Contains("получател") ||
                        h.Contains("плательщик") || h.Contains("назначен"))
                        hasCorrOrPurp = true;
                    if (h == "дебет" || h == "кредит" || h.Contains("дебет") || h.Contains("кредит") ||
                        h.Contains("расход") || h.Contains("приход") || h.Contains("списан") ||
                        h.Contains("зачислен") || h == "сумма" || h.Contains("сумма операц") || h.Contains("оборот"))
                        hasAmount = true;
                }
                if (hasDate && hasCorrOrPurp && hasAmount) return i;
            }
            return -1;
        }

        private static int DetectWbReportHeader(List<Dictionary<int, string>> rows)
        {
            int limit = Math.Min(10, rows.Count);
            for (int i = 0; i < limit; i++)
            {
                bool hasReason = false, hasToPay = false;
                foreach (var v in rows[i].Values)
                {
                    string h = NormalizeHeader(v);
                    if (h.Contains("обоснование для оплаты")) hasReason = true;
                    if (h.Contains("к перечислению продавцу")) hasToPay = true;
                }
                if (hasReason && hasToPay) return i;
            }
            return -1;
        }

        private static int DetectOzonReportHeader(List<Dictionary<int, string>> rows)
        {
            int limit = Math.Min(30, rows.Count);
            for (int i = 0; i < limit; i++)
            {
                bool hasItem = false, hasOzonSignal = false;
                foreach (var v in rows[i].Values)
                {
                    string h = NormalizeHeader(v);
                    if (string.IsNullOrEmpty(h)) continue;
                    if (h.Contains("артикул") || h == "sku" || h.Contains("ozon id") || h.Contains("offer id") ||
                        h.Contains("наименование товара") || h == "товар" || h == "наименование")
                        hasItem = true;
                    if (h.Contains("ozon") || h.Contains("озон") || h.Contains("итого к начислению") ||
                        h.Contains("реализовано на сумму") || h.Contains("тип начисления") ||
                        h.Contains("вознаграждение") || h.Contains("к начислению"))
                        hasOzonSignal = true;
                }
                if (hasItem && hasOzonSignal) return i;
            }
            return -1;
        }

        private static string NormalizeHeader(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string clean = Regex.Replace(s.Replace('\r', ' ').Replace('\n', ' '), @"\s+", " ").Trim().ToLowerInvariant();
            return clean;
        }

        private static void ParseBankRows(
            List<Dictionary<int, string>> rows,
            int hdrIdx,
            string sourceFormat,
            AnalyticsSnapshot snap,
            HashSet<string> bankTxKeys)
        {
            var hdr = rows[hdrIdx];
            int cDate = FindCol(hdr, "дата операц", "дата провод", "дата док", "дата валют", "дата", "date");
            int cNum = FindCol(hdr, "док-та", "номер док", "№ док", "номер", "документ");
            int cCorr = FindColExact(hdr, "корреспондент");
            if (cCorr < 0) cCorr = FindCol(hdr, "корреспондент", "наименование контрагента", "контрагент", "плательщик/получатель", "получатель/плательщик", "плательщик", "получатель");
            int cUnp = FindCol(hdr, "унп корреспондента", "унп контрагента", "унп", "инн", "учетный номер");
            int cPurp = FindCol(hdr, "назначение", "детали платежа", "содержание операц", "основание", "примечание", "текст платежа");
            int cBud = FindCol(hdr, "код плат", "кбк", "код бюджет", "код наз");

            int cDeb = FindColExact(hdr, "дебет");
            if (cDeb < 0) cDeb = FindCol(hdr, "дебет", "расход", "списан");
            int cCred = FindColExact(hdr, "кредит");
            if (cCred < 0) cCred = FindCol(hdr, "кредит", "приход", "поступлен", "зачислен");
            int cRate = FindCol(hdr, "курс нбрб", "курс");
            int cEqv = FindCol(hdr, "экв-нт", "эквивалент", "сумма в byn", "сумма byn");

            int cSingleAmt = -1, cDir = -1;
            if (cDeb < 0 && cCred < 0)
            {
                cSingleAmt = FindCol(hdr, "сумма операц", "сумма в валюте счета", "сумма", "оборот");
                cDir = FindCol(hdr, "дт/кт", "вид операц", "тип операц", "направление", "дебет/кредит");
            }

            int occCounter = 0;
            for (int i = hdrIdx + 1; i < rows.Count; i++)
            {
                var r = rows[i];
                string dtStr = GetStr(r, cDate).Trim();
                if (string.IsNullOrWhiteSpace(dtStr)) continue;
                string dtLow = dtStr.ToLowerInvariant();
                if (dtLow.Contains("итого") || dtLow.Contains("всего") || dtLow.Contains("остаток") || dtLow.Contains("оборот"))
                    continue;

                DateTime parsedDt = ParseFlexibleDate(dtStr);
                if (parsedDt == DateTime.MinValue) continue;

                decimal deb = 0m, cred = 0m;
                if (cDeb >= 0 || cCred >= 0)
                {
                    deb = Math.Abs(GetDec(r, cDeb));
                    cred = Math.Abs(GetDec(r, cCred));
                    decimal rate = cRate >= 0 ? GetDec(r, cRate) : 1m;
                    decimal eqv = cEqv >= 0 ? Math.Abs(GetDec(r, cEqv)) : 0m;
                    if (eqv > 0m && rate > 0m && Math.Abs(rate - 1m) > 0.0001m)
                    {
                        if (deb > 0m && cred == 0m) deb = eqv;
                        else if (cred > 0m && deb == 0m) cred = eqv;
                    }
                }
                else if (cSingleAmt >= 0)
                {
                    decimal amt = GetDec(r, cSingleAmt);
                    if (amt == 0m) continue;
                    string dir = GetStr(r, cDir).Trim().ToLowerInvariant();
                    if (dir == "д" || dir == "дт" || dir.Contains("деб") || dir.Contains("расх") || dir.Contains("спис"))
                        deb = Math.Abs(amt);
                    else if (dir == "к" || dir == "кт" || dir.Contains("кред") || dir.Contains("прих") || dir.Contains("зачис") || dir.Contains("пост"))
                        cred = Math.Abs(amt);
                    else if (amt < 0m)
                        deb = Math.Abs(amt);
                    else
                        cred = amt;
                }

                if (deb == 0m && cred == 0m) continue;

                string num = GetStr(r, cNum).Trim();
                string corr = GetStr(r, cCorr).Trim();
                string unp = GetStr(r, cUnp).Trim();
                string purp = GetStr(r, cPurp).Trim();
                string bud = GetStr(r, cBud).Trim();

                occCounter++;
                string txKey = BuildBankTxKey(parsedDt.ToString("dd.MM.yyyy"), num, deb, cred, unp, corr, purp, occCounter);
                if (!bankTxKeys.Add(txKey)) continue;

                string cat = ClassifyBankTx(corr, unp, purp, bud, deb, cred);
                snap.BankTransactions.Add(new BankTxItem
                {
                    Date = parsedDt.ToString("dd.MM.yyyy"),
                    ParsedDate = parsedDt,
                    DocNum = num,
                    Counterparty = corr,
                    Unp = unp,
                    Purpose = purp,
                    BudgetCode = bud,
                    Debet = deb,
                    Credit = cred,
                    Category = cat,
                    SourceFormat = sourceFormat
                });
            }
        }

        private static string BuildBankTxKey(string dt, string num, decimal deb, decimal cred, string unp, string corr, string purp, int occIdx)
        {
            string basePart = dt + "|" + (num ?? "") + "|" +
                deb.ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                cred.ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                (unp ?? "");
            if (string.IsNullOrWhiteSpace(num))
                return basePart + "|" + (corr ?? "") + "|" + (purp ?? "") + "|" + occIdx.ToString(CultureInfo.InvariantCulture);
            return basePart;
        }

        private static DateTime ParseFlexibleDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DateTime.MinValue;
            string s = raw.Trim();
            // Handle Excel serial date numbers (e.g. 46023 or 46023.5)
            double oa;
            if (Regex.IsMatch(s, @"^\d{5}([.,]\d+)?$") &&
                double.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out oa) &&
                oa >= 30000 && oa <= 60000)
            {
                try { return DateTime.FromOADate(oa).Date; } catch { }
            }

            if (s.Length > 10 && s[10] == ' ') s = s.Substring(0, 10);
            if (s.Length > 10 && s[10] == 'T') s = s.Substring(0, 10);

            DateTime dt;
            string[] fmts = { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy" };
            if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt.Date;
            if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out dt))
                return dt.Date;
            return DateTime.MinValue;
        }

        private static bool IsOzonCounterparty(string corr, string purp)
        {
            string cUp = (corr ?? "").ToUpperInvariant();
            string pUp = (purp ?? "").ToUpperInvariant();
            return cUp.Contains("ОЗОН") || cUp.Contains("OZON") || cUp.Contains("ИНТЕРНЕТ РЕШЕНИЯ") ||
                   pUp.Contains("ОЗОН") || pUp.Contains("OZON");
        }

        private static string ClassifyBankTx(string corr, string unp, string purp, string bud, decimal deb, decimal cred)
        {
            string cUp = (corr ?? "").ToUpperInvariant();
            string pUp = (purp ?? "").ToUpperInvariant();

            if (cred > 0m)
            {
                if (cUp.Contains("ИМВБРБ") || unp == "193630204" || cUp.Contains("ВАЙЛДБЕРРИЗ") || cUp.Contains("WILDBERRIES") ||
                    cUp.Contains("ОЗОН") || cUp.Contains("OZON") || cUp.Contains("ИНТЕРНЕТ РЕШЕНИЯ") ||
                    (pUp.Contains("ОПЛАТА ПО ДОГОВОРУ") && (cUp.Contains("МАРКЕТ") || pUp.Contains("WB") || pUp.Contains("OZON"))))
                    return "WB / Маркетплейсы";

                if (pUp.Contains("ВОЗВРАТ НАЛОГ") || pUp.Contains("ИЗЛИШНЕ УПЛАЧ") || pUp.Contains("СОБСТВЕННЫХ СРЕДСТВ") ||
                    pUp.Contains("ПЕРЕВОД СОБСТВ") || pUp.Contains("МЕЖДУ СЧЕТАМИ") || pUp.Contains("ВНЕСЕНИЕ НАЛИЧ") ||
                    pUp.Contains("ЗАЙМ") || pUp.Contains("ВОЗВРАТ ОШИБОЧНО"))
                    return "Необлагаемые поступления";

                return "Прочие поступления";
            }

            if (!string.IsNullOrWhiteSpace(bud) || cUp.Contains("ГЛАВНОЕ УПРАВЛЕНИЕ") || cUp.Contains("МИНИСТЕРСТВА ФИНАНСОВ") ||
                cUp.Contains("БЕЛГОССТРАХ") || cUp.Contains("ФСЗН") || cUp.Contains("ИМНС") || cUp.Contains("УПРАВЛЕНИЕ ФОНДА"))
                return "Налоги и ФСЗН";

            bool isAnyBank = cUp.Contains("БАНК") || cUp.Contains("BANK") || cUp.Contains("БЕЛАРУСБАНК") ||
                cUp.Contains("ПРИОРБАНК") || cUp.Contains("МТБАНК") || cUp.Contains("БЕЛИНВЕСТ") ||
                cUp.Contains("БНБ") || cUp.Contains("СБЕР") || cUp.Contains("АЛЬФА") || cUp.Contains("БЕЛАГРО") ||
                cUp.Contains("БЕЛВЭБ") || cUp.Contains("БСБ") || cUp.Contains("ТЕХНОБАНК") ||
                cUp.Contains("ПАРИТЕТ") || cUp.Contains("ДАБРАБЫТ") || cUp.Contains("ВТБ") ||
                cUp.Contains("СТАТУСБАНК") || cUp.Contains("РРБ") || cUp.Contains("НЕО БАНК") ||
                cUp.Contains("ЦЕПТЕР") || cUp.Contains("БТА");

            if (isAnyBank && (pUp.Contains("КОМИССИ") || pUp.Contains("ВОЗНАГРАЖД") || pUp.Contains("ПАКЕТ") ||
                              pUp.Contains("ТАРИФ") || pUp.Contains("РКО") || pUp.Contains("ОБСЛУЖИВ") ||
                              pUp.Contains("СМС") || pUp.Contains("АБОНЕНТ") || pUp.Contains("ПЛАТА ЗА ") ||
                              pUp.Contains("ЗА ПЕРЕВОД") || pUp.Contains("ВЕДЕНИЕ СЧЕТ")))
                return "Комиссия банка";

            if (pUp.Contains("КОМИССИЯ БАНКА") || pUp.Contains("ВОЗНАГРАЖДЕНИЕ БАНКА") || pUp.Contains("КОМИССИЯ ЗА ПЛАТ") ||
                pUp.Contains("КОМИССИЯ ЗА ПЕРЕВОД") || pUp.Contains("ПЛАТА ЗА ОБСЛУЖ") || pUp.Contains("СМС-ОПОВЕЩ") ||
                pUp.Contains("АБОНЕНТСКАЯ ПЛАТА"))
                return "Комиссия банка";

            if (pUp.Contains("ЗАРАБОТН") || pUp.Contains("ЗАРПЛАТ") || pUp.Contains("АВАНС") || pUp.Contains("ОТПУСК") ||
                pUp.Contains("ДИВИДЕНД") || pUp.Contains("ЛИЧНЫЙ ДОХОД") || pUp.Contains("ДОХОД ИП"))
                return "Зарплата и выплаты";

            return "Поставщики и расходы";
        }

        private static string BuildReportContentSignature(WbReportSummary s)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "SIG|{0}|{1}|{2}|{3}|{4}|{5}|{6:0.00}|{7:0.00}",
                s.Marketplace,
                s.DateFrom ?? "",
                s.DateTo ?? "",
                s.Countries ?? "",
                s.SaleQty,
                s.ReturnQty,
                s.RealizedP,
                s.ExpectedPayout);
        }

        private static bool RegisterReportIfUnique(HashSet<string> seenReports, WbReportSummary summary, string repNum)
        {
            string contentSig = BuildReportContentSignature(summary);
            string repKey = !string.IsNullOrEmpty(repNum) ? (summary.Marketplace + "#" + repNum) : null;

            if (seenReports.Contains(contentSig) || (repKey != null && seenReports.Contains(repKey)))
                return false;

            seenReports.Add(contentSig);
            if (repKey != null) seenReports.Add(repKey);
            return true;
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
            int cDocType = FindCol(hdr, "тип документа");
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

            string repNum = "";
            var mNum = Regex.Match(sourceLabel, @"№\s*(\d+)");
            if (mNum.Success) repNum = mNum.Groups[1].Value;

            var summary = new WbReportSummary
            {
                Marketplace = "WB",
                ReportNumber = string.IsNullOrEmpty(repNum) ? Path.GetFileNameWithoutExtension(sourceLabel) : repNum,
                SourceFile = sourceLabel
            };

            string minDate = "", maxDate = "";
            var countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var localSkus = new List<Action>();

            for (int i = hdrIdx + 1; i < rows.Count; i++)
            {
                var r = rows[i];
                string reason = GetStr(r, cReason).Trim();
                string docType = GetStr(r, cDocType).Trim();
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
                bool isDocReturn = string.Equals(docType, "Возврат", StringComparison.OrdinalIgnoreCase);

                decimal skuDeltaRealP = 0m;
                decimal skuDeltaToPay = 0m;

                if (isSale)
                {
                    summary.SaleQty += qty;
                    summary.RealizedP += realP;
                    summary.RetailAfterDisc += afterDisc * Math.Max(1, qty);
                    summary.ToPayGoods += toPay;
                    summary.CommissionWithVat += vv;
                    skuDeltaRealP = realP;
                    skuDeltaToPay = toPay;
                }
                else if (isReturn)
                {
                    summary.ReturnQty += qty;
                    summary.RealizedP -= realP;
                    summary.RetailAfterDisc -= afterDisc * Math.Max(1, qty);
                    summary.ToPayGoods -= toPay;
                    summary.CommissionWithVat -= vv;
                    skuDeltaRealP = -realP;
                    skuDeltaToPay = -toPay;
                }
                else if (toPay != 0m || realP != 0m)
                {
                    // Compensations (e.g. "Добровольная компенсация при возврате", damaged/lost compensation) & adjustments
                    int sign = isDocReturn ? -1 : 1;
                    decimal signedToPay = toPay * sign;
                    decimal signedRealP = realP * sign;
                    summary.ToPayGoods += signedToPay;
                    summary.Compensations += signedToPay;
                    summary.RealizedP += signedRealP;
                    summary.CommissionWithVat += vv * sign;
                    skuDeltaRealP = signedRealP;
                    skuDeltaToPay = signedToPay;
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
                string rawSkuKey = !string.IsNullOrEmpty(art) ? art : (!string.IsNullOrEmpty(bar) ? bar : nmId);

                if (!string.IsNullOrEmpty(rawSkuKey) &&
                    (isSale || isReturn || toPay != 0m || realP != 0m || log != 0m || pen != 0m || stor != 0m || hold != 0m || acc != 0m || loy != 0m))
                {
                    string dictKey = "WB|" + rawSkuKey;
                    localSkus.Add(delegate
                    {
                        SkuUnitStats st;
                        if (!skuDict.TryGetValue(dictKey, out st))
                        {
                            st = new SkuUnitStats
                            {
                                Marketplace = "WB",
                                Article = !string.IsNullOrEmpty(art) ? art : rawSkuKey,
                                Barcode = bar,
                                Title = title
                            };
                            skuDict[dictKey] = st;
                        }
                        if (string.IsNullOrEmpty(st.Title) && !string.IsNullOrEmpty(title)) st.Title = title;
                        if (string.IsNullOrEmpty(st.Barcode) && !string.IsNullOrEmpty(bar)) st.Barcode = bar;

                        if (isSale) st.SaleQty += qty;
                        else if (isReturn) st.ReturnQty += qty;

                        st.RealizedP += skuDeltaRealP;
                        st.ToPayGoods += skuDeltaToPay;
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

            if (!RegisterReportIfUnique(seenReports, summary, repNum))
            {
                snap.DeduplicatedSkipped++;
                return;
            }

            snap.Reports.Add(summary);
            foreach (var act in localSkus) act();
        }

        private static void ParseOzonReportRows(
            List<Dictionary<int, string>> rows,
            int hdrIdx,
            string sourceLabel,
            AnalyticsSnapshot snap,
            HashSet<string> seenReports,
            Dictionary<string, SkuUnitStats> skuDict)
        {
            // Check if hdrIdx + 1 is a sub-header row (e.g. "Реализовано" over "Кол-во", "Сумма")
            var combinedHdr = new Dictionary<int, string>();
            int dataStartIdx = hdrIdx + 1;

            bool nextIsSubHdr = false;
            if (hdrIdx + 1 < rows.Count)
            {
                int subHits = 0;
                foreach (var v in rows[hdrIdx + 1].Values)
                {
                    string n = NormalizeHeader(v);
                    if (n == "кол-во" || n == "количество" || n == "сумма" || n == "цена" || n == "итого" || n.Contains("кол-во"))
                        subHits++;
                }
                if (subHits >= 2) nextIsSubHdr = true;
            }

            if (nextIsSubHdr)
            {
                dataStartIdx = hdrIdx + 2;
                var topRow = rows[hdrIdx];
                var subRow = rows[hdrIdx + 1];
                int maxCol = 0;
                foreach (var k in topRow.Keys) if (k > maxCol) maxCol = k;
                foreach (var k in subRow.Keys) if (k > maxCol) maxCol = k;

                string lastTop = "";
                for (int c = 1; c <= maxCol; c++)
                {
                    string t = GetStr(topRow, c).Trim();
                    string s = GetStr(subRow, c).Trim();
                    if (!string.IsNullOrEmpty(t)) lastTop = t;
                    if (!string.IsNullOrEmpty(s))
                        combinedHdr[c] = string.IsNullOrEmpty(t) ? (lastTop + " " + s).Trim() : (t + " " + s).Trim();
                    else
                        combinedHdr[c] = t;
                }
            }
            else
            {
                foreach (var kv in rows[hdrIdx])
                    combinedHdr[kv.Key] = (kv.Value ?? "").Trim();
            }

            int cArt = FindCol(combinedHdr, "артикул продавца", "код товара продавца", "offer id", "артикул", "код товара");
            int cSku = FindCol(combinedHdr, "ozon id", "sku");
            if (cArt < 0) cArt = cSku;
            int cName = FindCol(combinedHdr, "наименование товара", "наименование", "название", "товар");
            int cOpType = FindCol(combinedHdr, "тип начисления", "тип операции", "операция", "вид начисления");
            int cDate = FindCol(combinedHdr, "дата начисления", "дата продажи", "дата доставки", "дата заказа", "дата");

            int cSaleQty = FindCol(combinedHdr, "реализовано кол", "реализовано шт", "продано кол", "продано шт");
            if (cSaleQty < 0) cSaleQty = FindCol(combinedHdr, "количество", "кол-во");
            int cSaleSum = FindCol(combinedHdr, "реализовано на сумму", "реализовано сумм", "сумма реализ", "цена продавца", "за продажу", "выручка от реализ");
            if (cSaleSum < 0) cSaleSum = FindCol(combinedHdr, "цена реализации", "сумма");

            int cRetQty = FindCol(combinedHdr, "возвращено кол", "возвращено шт", "возврат кол", "возвраты, шт", "возвраты шт");
            int cRetSum = FindCol(combinedHdr, "возвращено на сумму", "возвращено сумм", "сумма возврат");

            int cComm = FindCol(combinedHdr, "вознаграждение ozon", "комиссия ozon", "вознаграждение", "комиссия");
            int cToPay = FindCol(combinedHdr, "итого к начислению", "к начислению", "к перечислению", "причитается продавцу", "итого руб", "итого byn", "итого, руб", "итого, byn");
            if (cToPay < 0) cToPay = FindColExact(combinedHdr, "итого");

            int cLog = FindCol(combinedHdr, "услуги доставки", "доставка", "логистика", "обработка отправлений");
            int cStor = FindCol(combinedHdr, "размещение", "хранение");
            int cPen = FindCol(combinedHdr, "штраф", "неустой", "удержан");

            string repNum = "";
            var mNum = Regex.Match(sourceLabel, @"№\s*(\d+)");
            if (mNum.Success) repNum = mNum.Groups[1].Value;

            // Try to detect period from top rows above header
            string minDate = "", maxDate = "";
            for (int i = 0; i < hdrIdx; i++)
            {
                foreach (var v in rows[i].Values)
                {
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    var matches = Regex.Matches(v, @"\b(\d{2}\.\d{2}\.\d{4}|\d{4}-\d{2}-\d{2})\b");
                    foreach (Match m in matches)
                    {
                        var dt = ParseFlexibleDate(m.Value);
                        if (dt != DateTime.MinValue)
                        {
                            string ds = dt.ToString("yyyy-MM-dd");
                            if (string.IsNullOrEmpty(minDate) || string.CompareOrdinal(ds, minDate) < 0) minDate = ds;
                            if (string.IsNullOrEmpty(maxDate) || string.CompareOrdinal(ds, maxDate) > 0) maxDate = ds;
                        }
                    }
                }
            }

            var summary = new WbReportSummary
            {
                Marketplace = "Ozon",
                ReportNumber = string.IsNullOrEmpty(repNum) ? Path.GetFileNameWithoutExtension(sourceLabel) : repNum,
                SourceFile = sourceLabel,
                Countries = "Ozon"
            };

            bool isAccrualMode = cOpType >= 0;
            var localSkus = new List<Action>();

            for (int i = dataStartIdx; i < rows.Count; i++)
            {
                var r = rows[i];
                string art = GetStr(r, cArt).Trim();
                string skuCode = GetStr(r, cSku).Trim();
                string title = GetStr(r, cName).Trim();
                string opType = GetStr(r, cOpType).Trim();

                string checkTotal = (art + " " + title + " " + opType).ToLowerInvariant();
                if (checkTotal.StartsWith("итого") || checkTotal.StartsWith("всего")) continue;

                string dtRaw = GetStr(r, cDate).Trim();
                if (!string.IsNullOrEmpty(dtRaw))
                {
                    var dt = ParseFlexibleDate(dtRaw);
                    if (dt != DateTime.MinValue)
                    {
                        string ds = dt.ToString("yyyy-MM-dd");
                        if (string.IsNullOrEmpty(minDate) || string.CompareOrdinal(ds, minDate) < 0) minDate = ds;
                        if (string.IsNullOrEmpty(maxDate) || string.CompareOrdinal(ds, maxDate) > 0) maxDate = ds;
                    }
                }

                int sQty = (int)Math.Round(GetDec(r, cSaleQty), 0);
                decimal sSum = GetDec(r, cSaleSum);
                int rQty = cRetQty >= 0 ? (int)Math.Round(GetDec(r, cRetQty), 0) : 0;
                decimal rSum = cRetSum >= 0 ? GetDec(r, cRetSum) : 0m;
                decimal comm = Math.Abs(GetDec(r, cComm));
                decimal toPay = GetDec(r, cToPay);
                decimal log = Math.Abs(GetDec(r, cLog));
                decimal stor = Math.Abs(GetDec(r, cStor));
                decimal pen = Math.Abs(GetDec(r, cPen));

                if (sQty == 0 && sSum == 0m && rQty == 0 && rSum == 0m && toPay == 0m && log == 0m && stor == 0m && pen == 0m)
                    continue;

                decimal dRealP = 0m, dToPay = 0m, dComp = 0m, dLog = log, dStor = stor, dPen = pen;
                int dSaleQty = 0, dRetQty = 0;

                if (isAccrualMode)
                {
                    string opLow = opType.ToLowerInvariant();
                    bool isBuyerDeliverySale = opLow.Contains("доставка покупател") ||
                                               opLow.Contains("доставка и вручен") ||
                                               opLow == "продажа" ||
                                               opLow.Contains("реализац");
                    bool isLogisticsFee = opLow.Contains("обратная достав") ||
                                          opLow.Contains("услуги достав") ||
                                          opLow.Contains("логист") ||
                                          opLow.Contains("магистрал") ||
                                          opLow.Contains("кросс-док") ||
                                          opLow.Contains("обработ") ||
                                          opLow.Contains("последн") ||
                                          opLow.Contains("эквайринг") ||
                                          (!isBuyerDeliverySale && sQty == 0 && sSum == 0m && toPay < 0m);

                    if (isLogisticsFee)
                    {
                        dLog += Math.Abs(toPay != 0m ? toPay : sSum);
                    }
                    else if (opLow.Contains("возврат"))
                    {
                        dRetQty = Math.Max(1, Math.Abs(sQty));
                        dRealP = -Math.Abs(sSum);
                        dToPay = toPay != 0m ? -Math.Abs(toPay) : dRealP;
                        summary.CommissionWithVat -= comm;
                    }
                    else if (opLow.Contains("компенсац") || opLow.Contains("возмещ"))
                    {
                        dComp = toPay != 0m ? toPay : sSum;
                        dToPay = dComp;
                    }
                    else if (opLow.Contains("размещ") || opLow.Contains("хранен"))
                    {
                        dStor += Math.Abs(toPay != 0m ? toPay : sSum);
                    }
                    else if (opLow.Contains("штраф") || opLow.Contains("неустой") || opLow.Contains("удерж"))
                    {
                        dPen += Math.Abs(toPay != 0m ? toPay : sSum);
                    }
                    else
                    {
                        dSaleQty = Math.Max(0, sQty);
                        dRealP = sSum;
                        if (toPay != 0m)
                        {
                            // If column "Итого" already had row-level logistics subtracted, lift ToPayGoods so ExpectedPayout = ToPayGoods - Logistics equals toPay
                            dToPay = (log > 0m && Math.Abs((sSum - comm - log) - toPay) < 0.05m) ? (toPay + log) : toPay;
                        }
                        else
                        {
                            dToPay = sSum - comm;
                        }
                        summary.CommissionWithVat += comm;
                    }
                }
                else
                {
                    // Flat or 2-row Ozon Realization table (Sale & Return columns on same row, or negative Qty)
                    if (sQty < 0 || sSum < 0m)
                    {
                        dRetQty = Math.Abs(sQty);
                        dRealP = -Math.Abs(sSum);
                        dToPay = toPay != 0m ? -Math.Abs(toPay) : (dRealP + comm);
                        summary.CommissionWithVat -= comm;
                    }
                    else
                    {
                        dSaleQty = sQty;
                        dRetQty = Math.Abs(rQty);
                        dRealP = sSum - Math.Abs(rSum);
                        if (cToPay >= 0)
                            dToPay = toPay;
                        else
                            dToPay = dRealP - comm;
                        summary.CommissionWithVat += comm;
                    }
                }

                summary.SaleQty += dSaleQty;
                summary.ReturnQty += dRetQty;
                summary.RealizedP += dRealP;
                summary.ToPayGoods += dToPay;
                summary.Compensations += dComp;
                summary.Logistics += dLog;
                summary.Storage += dStor;
                summary.Penalties += dPen;

                string rawKey = !string.IsNullOrEmpty(art) ? art : skuCode;
                if (!string.IsNullOrEmpty(rawKey))
                {
                    string dictKey = "OZON|" + rawKey;
                    int capSaleQty = dSaleQty;
                    int capRetQty = dRetQty;
                    decimal capRealP = dRealP;
                    decimal capToPay = dToPay;
                    decimal capLog = dLog;
                    decimal capStor = dStor;
                    decimal capPen = dPen;

                    localSkus.Add(delegate
                    {
                        SkuUnitStats st;
                        if (!skuDict.TryGetValue(dictKey, out st))
                        {
                            st = new SkuUnitStats
                            {
                                Marketplace = "Ozon",
                                Article = !string.IsNullOrEmpty(art) ? art : skuCode,
                                Barcode = skuCode,
                                Title = title
                            };
                            skuDict[dictKey] = st;
                        }
                        if (string.IsNullOrEmpty(st.Title) && !string.IsNullOrEmpty(title)) st.Title = title;
                        if (string.IsNullOrEmpty(st.Barcode) && !string.IsNullOrEmpty(skuCode)) st.Barcode = skuCode;

                        st.SaleQty += capSaleQty;
                        st.ReturnQty += capRetQty;
                        st.RealizedP += capRealP;
                        st.ToPayGoods += capToPay;
                        st.Logistics += capLog;
                        st.Penalties += capPen;
                        st.StorageAndOther += capStor;
                    });
                }
            }

            summary.DateFrom = minDate;
            summary.DateTo = maxDate;
            summary.ExpectedPayout = summary.ToPayGoods - summary.Logistics - summary.Penalties - summary.Storage;

            if (!RegisterReportIfUnique(seenReports, summary, repNum))
            {
                snap.DeduplicatedSkipped++;
                return;
            }

            snap.Reports.Add(summary);
            foreach (var act in localSkus) act();
        }

        private static int FindCol(Dictionary<int, string> hdr, params string[] substrings)
        {
            foreach (var sub in substrings)
            {
                foreach (var kv in hdr)
                {
                    string h = NormalizeHeader(kv.Value);
                    if (!string.IsNullOrEmpty(h) && h.Contains(sub)) return kv.Key;
                }
            }
            return -1;
        }

        private static int FindColExact(Dictionary<int, string> hdr, string exact)
        {
            foreach (var kv in hdr)
            {
                if (string.Equals(NormalizeHeader(kv.Value), exact, StringComparison.OrdinalIgnoreCase))
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
            var all = ReadXlsxAllSheets(stream);
            return all.Count > 0 ? all[0] : new List<Dictionary<int, string>>();
        }

        private static List<List<Dictionary<int, string>>> ReadXlsxAllSheets(Stream stream)
        {
            var allSheets = new List<List<Dictionary<int, string>>>();
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

                var sheetEntries = new List<ZipArchiveEntry>();
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        sheetEntries.Add(e);
                    }
                }
                sheetEntries.Sort(delegate(ZipArchiveEntry a, ZipArchiveEntry b)
                {
                    int na = ExtractSheetNumber(a.FullName);
                    int nb = ExtractSheetNumber(b.FullName);
                    return na.CompareTo(nb);
                });

                foreach (var sheetEntry in sheetEntries)
                {
                    var result = new List<Dictionary<int, string>>();
                    using (var s = sheetEntry.Open())
                    {
                        var doc = XDocument.Load(s);
                        var sheetData = doc.Root.Element(MainNs + "sheetData");
                        if (sheetData == null) continue;

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
                    allSheets.Add(result);
                }
            }
            return allSheets;
        }

        private static int ExtractSheetNumber(string name)
        {
            var m = Regex.Match(name ?? "", @"sheet(\d+)\.xml", RegexOptions.IgnoreCase);
            int n;
            return (m.Success && int.TryParse(m.Groups[1].Value, out n)) ? n : 999;
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

            var sheets = new List<KeyValuePair<string, string>>();
            sheets.Add(new KeyValuePair<string, string>("Сводка и налог", BuildSummarySheetXml(snap)));
            if (snap.Reports.Count > 0)
            {
                sheets.Add(new KeyValuePair<string, string>("По отчетам МП", BuildReportsSheetXml(snap)));
                sheets.Add(new KeyValuePair<string, string>("Юнит-экономика SKU", BuildSkusSheetXml(snap)));
            }
            if (snap.BankTransactions.Count > 0)
            {
                sheets.Add(new KeyValuePair<string, string>("Выписка и сверка", BuildBankSheetXml(snap)));
            }

            WriteXlsxWorkbook(outputPath, sheets);
        }

        public static void ExportKudirXlsx(AnalyticsSnapshot snap, CompanyProfile profile, string outputPath)
        {
            if (snap == null || (snap.Reports.Count == 0 && snap.BankTransactions.Count == 0))
                throw new InvalidOperationException("Нет данных для формирования КУДиР.");

            var sheets = new List<KeyValuePair<string, string>>();
            sheets.Add(new KeyValuePair<string, string>("Раздел I Доходы", BuildKudirIncomeSheetXml(snap, profile)));
            if (snap.TaxRegime == CompanyProfilesManager.RegimeIpPn20)
            {
                sheets.Add(new KeyValuePair<string, string>("Расходы ИП (ориентир)", BuildKudirIpExpensesSheetXml(snap, profile)));
            }
            WriteXlsxWorkbook(outputPath, sheets);
        }

        public static void Export1CXml(AnalyticsSnapshot snap, CompanyProfile profile, string outputPath)
        {
            if (snap == null || (snap.Reports.Count == 0 && snap.BankTransactions.Count == 0))
                throw new InvalidOperationException("Нет данных для выгрузки в 1С.");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string compName = profile != null ? (profile.DisplayName ?? "") : "";
            string compUnp = profile != null ? (profile.Unp ?? "") : "";

            var root = new XElement("BuhlyExport1C",
                new XAttribute("Version", "3.0"),
                new XAttribute("GeneratedAt", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XAttribute("Company", CleanXmlStr(compName)),
                new XAttribute("Unp", CleanXmlStr(compUnp)),
                new XAttribute("TaxRegime", CleanXmlStr(snap.TaxRegime)),
                new XAttribute("TaxRatePercent", snap.EffectiveTaxRateApplied.ToString("0.##", CultureInfo.InvariantCulture)));

            var repRoot = new XElement("MarketplaceReports");
            foreach (var r in snap.Reports)
            {
                repRoot.Add(new XElement("Report",
                    new XAttribute("Marketplace", CleanXmlStr(r.Marketplace)),
                    new XAttribute("Number", CleanXmlStr(r.ReportNumber)),
                    new XAttribute("DateFrom", CleanXmlStr(r.DateFrom)),
                    new XAttribute("DateTo", CleanXmlStr(r.DateTo)),
                    new XAttribute("SaleQty", r.SaleQty.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("ReturnQty", r.ReturnQty.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("RealizedRevenue", FmtInv(r.RealizedP)),
                    new XAttribute("ToPayGoods", FmtInv(r.ToPayGoods)),
                    new XAttribute("Compensations", FmtInv(r.Compensations)),
                    new XAttribute("CommissionWithVat", FmtInv(r.CommissionWithVat)),
                    new XAttribute("Logistics", FmtInv(r.Logistics)),
                    new XAttribute("Storage", FmtInv(r.Storage)),
                    new XAttribute("Penalties", FmtInv(r.Penalties)),
                    new XAttribute("OtherHoldings", FmtInv(r.Holdings + r.Acceptance + r.Loyalty)),
                    new XAttribute("ExpectedPayout", FmtInv(r.ExpectedPayout))));
            }
            root.Add(repRoot);

            var skuRoot = new XElement("SkuTotals");
            foreach (var s in snap.Skus)
            {
                skuRoot.Add(new XElement("Sku",
                    new XAttribute("Marketplace", CleanXmlStr(s.Marketplace)),
                    new XAttribute("Article", CleanXmlStr(s.Article)),
                    new XAttribute("Barcode", CleanXmlStr(s.Barcode)),
                    new XAttribute("Title", CleanXmlStr(s.Title)),
                    new XAttribute("SaleQty", s.SaleQty.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("ReturnQty", s.ReturnQty.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("NetQty", s.NetQty.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("RealizedRevenue", FmtInv(s.RealizedP)),
                    new XAttribute("NetFromMarketplace", FmtInv(s.NetFromWb)),
                    new XAttribute("UnitCost", FmtInv(s.UnitCost)),
                    new XAttribute("TotalCost", FmtInv(s.TotalCost)),
                    new XAttribute("EstimatedTax", FmtInv(s.EstimatedTax)),
                    new XAttribute("NetProfit", FmtInv(s.NetProfit))));
            }
            root.Add(skuRoot);

            var bankRoot = new XElement("BankDocuments");
            foreach (var tx in snap.BankTransactions)
            {
                bankRoot.Add(new XElement("Document",
                    new XAttribute("Date", CleanXmlStr(tx.Date)),
                    new XAttribute("DocNum", CleanXmlStr(tx.DocNum)),
                    new XAttribute("Counterparty", CleanXmlStr(tx.Counterparty)),
                    new XAttribute("Unp", CleanXmlStr(tx.Unp)),
                    new XAttribute("BudgetCode", CleanXmlStr(tx.BudgetCode)),
                    new XAttribute("Category", CleanXmlStr(tx.Category)),
                    new XAttribute("Debet", FmtInv(tx.Debet)),
                    new XAttribute("Credit", FmtInv(tx.Credit)),
                    new XAttribute("Purpose", CleanXmlStr(tx.Purpose))));
            }
            root.Add(bankRoot);

            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
            doc.Save(outputPath);
        }

        public static void Export1CCsv(AnalyticsSnapshot snap, CompanyProfile profile, string outputPath)
        {
            if (snap == null || (snap.Reports.Count == 0 && snap.BankTransactions.Count == 0))
                throw new InvalidOperationException("Нет данных для выгрузки в CSV для 1С.");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("Тип_записи;Источник;Номер_док;Дата;Дата_по;Контрагент;УНП;Категория;Кол_во_шт;Реализация_или_Приход;Комиссия_или_Расход;Логистика_Хран_Штраф;К_перечислению;Примечание");

            foreach (var r in snap.Reports)
            {
                decimal services = r.Logistics + r.Storage + r.Penalties + r.Holdings + r.Acceptance + r.Loyalty;
                string cp = string.Equals(r.Marketplace, "Ozon", StringComparison.OrdinalIgnoreCase)
                    ? "ООО ОЗОН Маркет Бел / Интернет Решения"
                    : "ООО ИМВБРБ (Wildberries)";
                string unp = string.Equals(r.Marketplace, "Ozon", StringComparison.OrdinalIgnoreCase) ? "" : "193630204";
                sb.AppendFormat("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13}\r\n",
                    CsvEsc("Отчет_МП"),
                    CsvEsc(r.Marketplace),
                    CsvEsc(r.ReportNumber),
                    CsvEsc(r.DateFrom),
                    CsvEsc(r.DateTo),
                    CsvEsc(cp),
                    CsvEsc(unp),
                    CsvEsc("Реализация МП"),
                    (r.SaleQty - r.ReturnQty).ToString(CultureInfo.InvariantCulture),
                    FmtInv(r.RealizedP),
                    FmtInv(r.CommissionWithVat),
                    FmtInv(services),
                    FmtInv(r.ExpectedPayout),
                    CsvEsc("Компенсации: " + FmtInv(r.Compensations)));
            }

            foreach (var tx in snap.BankTransactions)
            {
                sb.AppendFormat("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13}\r\n",
                    CsvEsc("Выписка_РБ"),
                    CsvEsc(string.IsNullOrEmpty(tx.SourceFormat) ? "Банк" : tx.SourceFormat),
                    CsvEsc(tx.DocNum),
                    CsvEsc(tx.Date),
                    CsvEsc(tx.Date),
                    CsvEsc(tx.Counterparty),
                    CsvEsc(tx.Unp),
                    CsvEsc(tx.Category),
                    "0",
                    FmtInv(tx.Credit),
                    FmtInv(tx.Debet),
                    "0.00",
                    FmtInv(tx.Credit - tx.Debet),
                    CsvEsc(tx.Purpose));
            }

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(true));
        }

        private static string CsvEsc(string s)
        {
            string val = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            if (val.Contains(";") || val.Contains("\""))
                val = "\"" + val.Replace("\"", "\"\"") + "\"";
            return val;
        }

        private static string FmtInv(decimal val)
        {
            return Math.Round(val, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void WriteXlsxWorkbook(string outputPath, List<KeyValuePair<string, string>> sheets)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(outputPath)) File.Delete(outputPath);

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
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"50\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"46\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">").Append(SC("A1", 1, "Buhly • Финансовый отчёт маркетплейсов (WB + Ozon) и налоги РБ")).Append("</row>");
            sb.Append("<row r=\"2\">").Append(SC("A2", 0, "Режим: " + snap.TaxRegimeTitle + "   |   Сформировано: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"))).Append("</row>");

            int r = 4;
            AddKvHdr(sb, r++, "Показатель (Отчёты WB и Ozon)", "Сумма / Кол-во", "Комментарий для бухгалтера");
            AddKvInt(sb, r++, "Загружено уникальных отчётов (WB / Ozon)", snap.Reports.Count, string.Format("WB: {0}, Ozon: {1}, дубликатов пропущено: {2}", snap.WbReportCount, snap.OzonReportCount, snap.DeduplicatedSkipped));
            AddKvInt(sb, r++, "Продано товара (шт)", snap.TotalSaleQty, "");
            AddKvInt(sb, r++, "Возвраты покупателей (шт)", snap.TotalReturnQty, "");
            AddKvMoney(sb, r++, "Выручка по реализации МП (Пр, нетто), BYN", snap.TotalRealizedP, "Продажа минус возврат (полная стоимость реализации покупателям)");
            AddKvMoney(sb, r++, "К перечислению за товар и компенсации, BYN", snap.TotalToPayGoods, "Включая компенсации/корректировки: " + FmtInv(snap.TotalCompensations) + " BYN");
            AddKvMoney(sb, r++, "Вознаграждение маркетплейсов (с НДС), BYN", snap.TotalCommissionWithVat, "");
            AddKvMoney(sb, r++, "Услуги логистики (доставка), BYN", snap.TotalLogistics, "");
            AddKvMoney(sb, r++, "Хранение / размещение, BYN", snap.TotalStorage, "");
            AddKvMoney(sb, r++, "Штрафы МП, BYN", snap.TotalPenalties, "");
            AddKvMoney(sb, r++, "Прочие удержания / приёмка / лояльность, BYN", snap.TotalHoldings + snap.TotalAcceptance + snap.TotalLoyalty, "");
            AddKvBoldMoney(sb, r++, "ИТОГО расчётная выплата на р/с от МП, BYN", snap.TotalExpectedPayout, string.Format("WB: {0} | Ozon: {1} BYN", FmtInv(snap.WbExpectedPayout), FmtInv(snap.OzonExpectedPayout)));
            AddKvMoney(sb, r++, "Себестоимость реализованных SKU (по справочнику), BYN", snap.TotalCostOfGoods, "По локальному справочнику закупочных цен Buhly");

            r++;
            AddKvHdr(sb, r++, "Налоговый расчёт (" + snap.TaxRegimeTitle + ")", "Сумма, BYN", "Пояснение");
            if (snap.TaxRegime == CompanyProfilesManager.RegimeIpPn20)
            {
                string ipGrossComment;
                if (snap.IpHighIncomeRateApplied || snap.IpGrossIncome > 500000m)
                    ipGrossComment = "КРИТИЧНО (> 500 000 BYN): ставка 30%, а с 01.01 следующего года работа в статусе ИП запрещена (обязателен переход в юрлицо, Закон № 365-З)!";
                else if (snap.IpNear500kLimit)
                    ipGrossComment = "ВНИМАНИЕ (> 400 000 BYN): приближение к годовому лимиту ИП 500 000 BYN (при превышении — ставка 30% и переход в юрлицо с 01.01 по Закону № 365-З)";
                else
                    ipGrossComment = "УСН для ИП в РБ отменён — применяется подоходный налог (годовой лимит ИП — 500 000 BYN)";
                AddKvMoney(sb, r++, "Валовой доход ИП (Реализация МП + комп. + облаг. р/с)", snap.IpGrossIncome, ipGrossComment);
                string expNote = snap.IpHasPotentialDoubleGoodsCost
                    ? string.Format("По р/с учтены только прочие расходы ({0}); оплаты за товар ({1}) не дублированы с себестоимостью SKU", FmtInv(snap.IpBankDeductibleExpenses), FmtInv(snap.IpBankGoodsPayments))
                    : ("По р/с (поставщики, комиссия банка, ФОТ): " + FmtInv(snap.IpBankDeductibleExpenses));
                AddKvMoney(sb, r++, "Докум. расходы (комиссии МП + закупка SKU + р/с)", snap.IpDocumentedExpenses, expNote);
                AddKvMoney(sb, r++, "Стандартный проф. вычет (20% от дохода)", snap.IpStandardDeduction20, "Альтернатива без подтверждения расходов документами");
                AddKvMoney(sb, r++, string.Format(CultureInfo.InvariantCulture, "Подоходный налог при расходах ({0:0.##}%)", snap.EffectiveTaxRateApplied), snap.IpTaxDocumented, "База: " + FmtInv(snap.IpNetBaseDocumented) + " BYN");
                AddKvMoney(sb, r++, string.Format(CultureInfo.InvariantCulture, "Подоходный налог при вычете 20% ({0:0.##}%)", snap.EffectiveTaxRateApplied), snap.IpTaxStandard20, "База: " + FmtInv(snap.IpNetBaseStandard20) + " BYN");
                AddKvBoldMoney(sb, r++, "Рекомендуемый подоходный налог ИП", snap.EstimatedTaxByRealization, snap.IpUsedStandardDeduction ? "Выгоднее стандартный вычет 20%" : "Выгоднее документальные расходы");
                string fsznComment = snap.IpFsznCapped12Mzp
                    ? "Потолок 12 МЗП (10 296 BYN в 2026 при МЗП 858 BYN) -> макс. 3 603.60 BYN/год (Закон № 90-З); ПУ-3 за себя отменена, ФСЗН считает по данным МНС, уплата до 31.03"
                    : string.Format("35% от факт. чистого дохода {0} BYN (< 12 МЗП = 10 296 BYN в 2026 при МЗП 858 BYN, Закон № 90-З); ПУ-3 за себя отменена, ФСЗН считает по данным МНС, уплата до 31.03", FmtInv(snap.IpFsznBase));
                AddKvMoney(sb, r++, "Ориентир взноса ФСЗН ИП (35%, потолок 12 МЗП)", snap.IpFszn35, fsznComment);
            }
            else if (snap.TaxRegime == CompanyProfilesManager.RegimeOsn20)
            {
                AddKvMoney(sb, r++, "База выручки (с НДС)", snap.TaxBaseAmount, "Реализация МП + компенсации МП + облагаемые прочие поступления по р/с");
                AddKvMoney(sb, r++, "Исходящий НДС 20% (расчётный 20/120, ориентир)", snap.OsnVat20, "Без вычета входящего НДС — точный расчёт по ЭСЧФ");
                AddKvMoney(sb, r++, "Расчётная база налога на прибыль", snap.OsnProfitTaxBase, "Выручка без НДС минус комиссии МП, закупка SKU и расходы с р/с");
                AddKvMoney(sb, r++, string.Format(CultureInfo.InvariantCulture, "Налог на прибыль ({0:0.##}%), ориентир", snap.EffectiveTaxRateApplied), snap.OsnProfitTax20, "");
                AddKvBoldMoney(sb, r++, "Итого ориентир налоговой нагрузки ОСН (НДС + Прибыль)", snap.EstimatedTaxByRealization, "");
            }
            else
            {
                AddKvMoney(sb, r++, "Налоговая база (Реализация Пр + комп. + облаг. р/с)", snap.TaxBaseAmount, string.Format("Компенсации МП: {0} | Прочие облаг. поступления р/с: {1} BYN", FmtInv(snap.TotalCompensations), FmtInv(snap.BankOtherCredits)));
                AddKvBoldMoney(sb, r++, string.Format(CultureInfo.InvariantCulture, "Ориентир налога ({0:0.##}%)", snap.EffectiveTaxRateApplied), snap.EstimatedTaxByRealization, "Информативный ориентир, итоговую базу проверяет бухгалтер");
            }
            AddKvBoldMoney(sb, r++, "Чистая прибыль по товарам (до аренды/ФОТ), BYN", snap.EstimatedNetProfitBeforeFixed, "Выплата МП - Себестоимость - Налоги");

            if (snap.BankTransactions.Count > 0)
            {
                r++;
                AddKvHdr(sb, r++, "Банковская выписка (ДДС и сверка)", "Сумма, BYN", "По данным загруженной выписки");
                AddKvBoldMoney(sb, r++, "Поступило на р/с от маркетплейсов (WB + Ozon)", snap.BankMarketplaceCredits, string.Format("WB: {0} | Ozon: {1} | Расхождение с отчётами: {2} BYN", FmtInv(snap.BankWbCredits), FmtInv(snap.BankOzonCredits), FmtInv(snap.BankMarketplaceCredits - snap.TotalExpectedPayout)));
                AddKvMoney(sb, r++, "Прочие облагаемые поступления на р/с", snap.BankOtherCredits, "Необлагаемые (переводы/займы/возвраты): " + FmtInv(snap.BankNonTaxableCredits) + " BYN");
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
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"15\" customWidth=\"1\"/><col min=\"3\" max=\"4\" width=\"12\" customWidth=\"1\"/><col min=\"5\" max=\"5\" width=\"16\" customWidth=\"1\"/><col min=\"6\" max=\"7\" width=\"10\" customWidth=\"1\"/><col min=\"8\" max=\"15\" width=\"14\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            string[] h = { "МП", "№ отчёта", "С даты", "По дату", "Страны", "Прод., шт", "Возвр., шт", "Реал. Пр", "К перечисл.", "Компенс.", "ВВ (с НДС)", "Логистика", "Хранение", "Штрафы", "Удерж./Пр.", "К выплате" };
            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "1", 2, h[c]));
            sb.Append("</row>");

            for (int i = 0; i < snap.Reports.Count; i++)
            {
                int rn = i + 2;
                var x = snap.Reports[i];
                sb.Append("<row r=\"" + rn + "\">");
                sb.Append(SC("A" + rn, 3, x.Marketplace));
                sb.Append(SC("B" + rn, 3, x.ReportNumber));
                sb.Append(SC("C" + rn, 3, x.DateFrom));
                sb.Append(SC("D" + rn, 3, x.DateTo));
                sb.Append(SC("E" + rn, 3, x.Countries));
                sb.Append(NC("F" + rn, 5, x.SaleQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(NC("G" + rn, 5, x.ReturnQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(MC("H" + rn, 4, x.RealizedP));
                sb.Append(MC("I" + rn, 4, x.ToPayGoods));
                sb.Append(MC("J" + rn, 4, x.Compensations));
                sb.Append(MC("K" + rn, 4, x.CommissionWithVat));
                sb.Append(MC("L" + rn, 4, x.Logistics));
                sb.Append(MC("M" + rn, 4, x.Storage));
                sb.Append(MC("N" + rn, 4, x.Penalties));
                sb.Append(MC("O" + rn, 4, x.Holdings + x.Acceptance + x.Loyalty));
                sb.Append(MC("P" + rn, 7, x.ExpectedPayout));
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string BuildSkusSheetXml(AnalyticsSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"8\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"24\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"38\" customWidth=\"1\"/><col min=\"4\" max=\"6\" width=\"10\" customWidth=\"1\"/><col min=\"7\" max=\"15\" width=\"13\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            string[] h = { "МП", "Артикул", "Название", "Прод.", "Возвр.", "Выкуп %", "Реал. Пр", "К перечисл.", "Логистика", "Штрафы/Хран.", "От МП чист.", "Себест./шт", "Себест. итого", "Налог", "Прибыль" };
            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "1", 2, h[c]));
            sb.Append("</row>");

            for (int i = 0; i < snap.Skus.Count; i++)
            {
                int rn = i + 2;
                var s = snap.Skus[i];
                sb.Append("<row r=\"" + rn + "\">");
                sb.Append(SC("A" + rn, 3, s.Marketplace));
                sb.Append(SC("B" + rn, 3, s.Article));
                sb.Append(SC("C" + rn, 3, s.Title));
                sb.Append(NC("D" + rn, 5, s.SaleQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(NC("E" + rn, 5, s.ReturnQty.ToString(CultureInfo.InvariantCulture)));
                sb.Append(MC("F" + rn, 4, s.BuyoutPercent));
                sb.Append(MC("G" + rn, 4, s.RealizedP));
                sb.Append(MC("H" + rn, 4, s.ToPayGoods));
                sb.Append(MC("I" + rn, 4, s.Logistics));
                sb.Append(MC("J" + rn, 4, s.Penalties + s.StorageAndOther));
                sb.Append(MC("K" + rn, 4, s.NetFromWb));
                sb.Append(MC("L" + rn, 4, s.UnitCost));
                sb.Append(MC("M" + rn, 4, s.TotalCost));
                sb.Append(MC("N" + rn, 4, s.EstimatedTax));
                sb.Append(MC("O" + rn, 7, s.NetProfit));
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string BuildBankSheetXml(AnalyticsSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"24\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"42\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"13\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"8\" customWidth=\"1\"/><col min=\"5\" max=\"6\" width=\"15\" customWidth=\"1\"/></cols>");
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

        private static string BuildKudirIncomeSheetXml(AnalyticsSnapshot snap, CompanyProfile profile)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"7\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"13\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"30\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"42\" customWidth=\"1\"/><col min=\"5\" max=\"7\" width=\"18\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            string orgLine = profile != null
                ? string.Format("{0} (УНП {1})", profile.DisplayName ?? "", string.IsNullOrWhiteSpace(profile.Unp) ? "—" : profile.Unp)
                : "Организация / ИП";

            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">").Append(SC("A1", 1, "КУДиР • Раздел I. Учёт доходов (" + snap.TaxRegimeTitle + ")")).Append("</row>");
            sb.Append("<row r=\"2\">").Append(SC("A2", 0, "Плательщик: " + orgLine + "   |   Сформировано: " + DateTime.Now.ToString("dd.MM.yyyy"))).Append("</row>");

            string[] h = { "№", "Дата записи", "Документ (наим., №, дата)", "Содержание операции", "Выручка от реализации, BYN", "Внереализационные / комп., BYN", "Итого доходов (база), BYN" };
            sb.Append("<row r=\"4\" ht=\"28\" customHeight=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "4", 2, h[c]));
            sb.Append("</row>");

            int rowIdx = 5;
            int seq = 1;
            decimal totalRev = 0m, totalNonOp = 0m, totalBase = 0m;

            foreach (var rep in snap.Reports)
            {
                string docInfo = string.Format("Отчёт {0} № {1} ({2}..{3})", rep.Marketplace, rep.ReportNumber, rep.DateFrom, rep.DateTo);
                string recDate = !string.IsNullOrEmpty(rep.DateTo) ? rep.DateTo : rep.DateFrom;
                decimal rev = rep.RealizedP;
                decimal comp = rep.Compensations;
                decimal rowSum = rev + comp;

                sb.Append("<row r=\"" + rowIdx + "\">");
                sb.Append(NC("A" + rowIdx, 5, seq.ToString(CultureInfo.InvariantCulture)));
                sb.Append(SC("B" + rowIdx, 3, recDate));
                sb.Append(SC("C" + rowIdx, 3, docInfo));
                sb.Append(SC("D" + rowIdx, 3, string.Format("Реализация товаров через {0} (прод. {1} шт, возвр. {2} шт)", rep.Marketplace, rep.SaleQty, rep.ReturnQty)));
                sb.Append(MC("E" + rowIdx, 4, rev));
                sb.Append(MC("F" + rowIdx, 4, comp));
                sb.Append(MC("G" + rowIdx, 7, rowSum));
                sb.Append("</row>");

                totalRev += rev;
                totalNonOp += comp;
                totalBase += rowSum;
                rowIdx++;
                seq++;
            }

            foreach (var tx in snap.BankTransactions)
            {
                if (tx.Credit <= 0m) continue;
                if (tx.Category == "Необлагаемые поступления") continue;
                // Avoid double-counting marketplace bank payouts when marketplace realization reports are loaded
                if (tx.Category == "WB / Маркетплейсы" && snap.Reports.Count > 0) continue;

                string docInfo = string.IsNullOrEmpty(tx.DocNum) ? ("Выписка от " + tx.Date) : ("Плат. док. № " + tx.DocNum + " от " + tx.Date);
                sb.Append("<row r=\"" + rowIdx + "\">");
                sb.Append(NC("A" + rowIdx, 5, seq.ToString(CultureInfo.InvariantCulture)));
                sb.Append(SC("B" + rowIdx, 3, tx.Date));
                sb.Append(SC("C" + rowIdx, 3, docInfo));
                sb.Append(SC("D" + rowIdx, 3, tx.Counterparty + (string.IsNullOrEmpty(tx.Purpose) ? "" : (" — " + tx.Purpose))));
                sb.Append(MC("E" + rowIdx, 4, tx.Credit));
                sb.Append(MC("F" + rowIdx, 4, 0m));
                sb.Append(MC("G" + rowIdx, 7, tx.Credit));
                sb.Append("</row>");

                totalRev += tx.Credit;
                totalBase += tx.Credit;
                rowIdx++;
                seq++;
            }

            sb.Append("<row r=\"" + rowIdx + "\">");
            sb.Append(SC("A" + rowIdx, 6, ""));
            sb.Append(SC("B" + rowIdx, 6, ""));
            sb.Append(SC("C" + rowIdx, 6, ""));
            sb.Append(SC("D" + rowIdx, 6, "ИТОГО ЗА ПЕРИОД:"));
            sb.Append(MC("E" + rowIdx, 7, totalRev));
            sb.Append(MC("F" + rowIdx, 7, totalNonOp));
            sb.Append(MC("G" + rowIdx, 7, totalBase));
            sb.Append("</row>");

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string BuildKudirIpExpensesSheetXml(AnalyticsSnapshot snap, CompanyProfile profile)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<cols><col min=\"1\" max=\"1\" width=\"7\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"13\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"30\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"44\" customWidth=\"1\"/><col min=\"5\" max=\"5\" width=\"18\" customWidth=\"1\"/></cols>");
            sb.Append("<sheetData>");

            sb.Append("<row r=\"1\" ht=\"24\" customHeight=\"1\">").Append(SC("A1", 1, "КУДиР ИП • Учёт документально подтверждённых расходов (ориентир)")).Append("</row>");
            string compNote = snap.IpHasPotentialDoubleGoodsCost
                ? string.Format("  |  ВНИМАНИЕ: оплаты за товар по р/с ({0} BYN) исключены во избежание дублирования с себестоимостью SKU", FmtInv(snap.IpBankGoodsPayments))
                : "";
            sb.Append("<row r=\"2\">").Append(SC("A2", 0, "Сравнение: Документальные расходы (" + FmtInv(snap.IpDocumentedExpenses) + " BYN) vs Стандартный вычет 20% (" + FmtInv(snap.IpStandardDeduction20) + " BYN)" + compNote)).Append("</row>");
            string fsznKudirNote = snap.IpFsznCapped12Mzp
                ? string.Format("ФСЗН ИП 35% (Закон № 90-З): {0} BYN — потолок 12 МЗП (10 296 BYN в 2026 при МЗП 858 BYN, макс. 3 603.60 BYN/год). ПУ-3 за себя отменена, ФСЗН считает по данным МНС, уплата до 31.03.", FmtInv(snap.IpFszn35))
                : string.Format("ФСЗН ИП 35% (Закон № 90-З): {0} BYN от факт. чистого дохода {1} BYN (< 12 МЗП = 10 296 BYN в 2026 при МЗП 858 BYN; потолок 3 603.60 BYN/год). ПУ-3 за себя отменена, уплата до 31.03.", FmtInv(snap.IpFszn35), FmtInv(snap.IpFsznBase));
            sb.Append("<row r=\"3\">").Append(SC("A3", 0, fsznKudirNote)).Append("</row>");

            string[] h = { "№", "Дата", "Документ", "Статья / содержание расхода", "Сумма, BYN" };
            sb.Append("<row r=\"4\" ht=\"24\" customHeight=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "4", 2, h[c]));
            sb.Append("</row>");

            int rowIdx = 5;
            int seq = 1;
            decimal total = 0m;

            foreach (var rep in snap.Reports)
            {
                decimal mpExp = Math.Max(0m, rep.RealizedP + rep.Compensations - rep.ExpectedPayout);
                if (mpExp <= 0m) continue;
                sb.Append("<row r=\"" + rowIdx + "\">");
                sb.Append(NC("A" + rowIdx, 5, seq.ToString(CultureInfo.InvariantCulture)));
                sb.Append(SC("B" + rowIdx, 3, !string.IsNullOrEmpty(rep.DateTo) ? rep.DateTo : rep.DateFrom));
                sb.Append(SC("C" + rowIdx, 3, string.Format("Отчёт {0} № {1}", rep.Marketplace, rep.ReportNumber)));
                sb.Append(SC("D" + rowIdx, 3, "Комиссия, логистика, хранение и удержания маркетплейса"));
                sb.Append(MC("E" + rowIdx, 4, mpExp));
                sb.Append("</row>");
                total += mpExp;
                rowIdx++;
                seq++;
            }

            if (snap.TotalCostOfGoods > 0m)
            {
                string periodEnd = !string.IsNullOrEmpty(snap.PeriodEndDate) ? snap.PeriodEndDate : DateTime.Today.ToString("dd.MM.yyyy");
                sb.Append("<row r=\"" + rowIdx + "\">");
                sb.Append(NC("A" + rowIdx, 5, seq.ToString(CultureInfo.InvariantCulture)));
                sb.Append(SC("B" + rowIdx, 3, periodEnd));
                sb.Append(SC("C" + rowIdx, 3, "Справочник закупочных цен Buhly"));
                sb.Append(SC("D" + rowIdx, 3, "Стоимость реализованных товаров (SKU) по приходным документам"));
                sb.Append(MC("E" + rowIdx, 4, snap.TotalCostOfGoods));
                sb.Append("</row>");
                total += snap.TotalCostOfGoods;
                rowIdx++;
                seq++;
            }

            foreach (var tx in snap.BankTransactions)
            {
                if (tx.Debet <= 0m || tx.Category == "Налоги и ФСЗН") continue;
                bool skipDoubleGoods = snap.IpHasPotentialDoubleGoodsCost &&
                                       tx.Category == "Поставщики и расходы" &&
                                       IsGoodsPurchasePurpose(tx.Purpose);
                decimal rowAmt = skipDoubleGoods ? 0m : tx.Debet;
                string desc = tx.Category + ": " + tx.Counterparty + (string.IsNullOrEmpty(tx.Purpose) ? "" : (" — " + tx.Purpose));
                if (skipDoubleGoods)
                    desc += string.Format(" [НЕ ВКЛЮЧЕНО ({0} BYN) — учтено через себестоимость SKU]", FmtInv(tx.Debet));

                sb.Append("<row r=\"" + rowIdx + "\">");
                sb.Append(NC("A" + rowIdx, 5, seq.ToString(CultureInfo.InvariantCulture)));
                sb.Append(SC("B" + rowIdx, 3, tx.Date));
                sb.Append(SC("C" + rowIdx, 3, string.IsNullOrEmpty(tx.DocNum) ? ("Выписка " + tx.Date) : ("Плат. док. № " + tx.DocNum)));
                sb.Append(SC("D" + rowIdx, 3, desc));
                sb.Append(MC("E" + rowIdx, 4, rowAmt));
                sb.Append("</row>");
                total += rowAmt;
                rowIdx++;
                seq++;
            }

            sb.Append("<row r=\"" + rowIdx + "\">");
            sb.Append(SC("A" + rowIdx, 6, ""));
            sb.Append(SC("B" + rowIdx, 6, ""));
            sb.Append(SC("C" + rowIdx, 6, ""));
            sb.Append(SC("D" + rowIdx, 6, "ИТОГО ДОКУМ. РАСХОДОВ:"));
            sb.Append(MC("E" + rowIdx, 7, total));
            sb.Append("</row>");

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        // ==================== DEMO DATA GENERATOR (SYNTHETIC, ZERO PII) ====================

        public static List<string> GenerateDemoAnalyticsFiles(string folder, out Dictionary<string, decimal> demoSkuCosts)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            demoSkuCosts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            demoSkuCosts["DEMO-CASE-15"] = 6.20m;
            demoSkuCosts["DEMO-GLASS-9H"] = 3.10m;
            demoSkuCosts["DEMO-CABLE-1M"] = 4.80m;
            demoSkuCosts["DEMO-STAND-DESK"] = 11.50m;
            demoSkuCosts["DEMO-LOW-MARGIN"] = 22.00m;

            string wbPath = Path.Combine(folder, "WB_Demo_Report_№900101.xlsx");
            WriteDemoWbReport(wbPath);

            string ozonPath = Path.Combine(folder, "Ozon_Demo_Report_2026_09.xlsx");
            WriteDemoOzonReport(ozonPath);

            string bankPath = Path.Combine(folder, "Bank_Demo_1C_kl_to_1c.txt");
            WriteDemo1CBankFile(bankPath);

            return new List<string> { wbPath, ozonPath, bankPath };
        }

        private static void WriteDemoWbReport(string path)
        {
            string[] h =
            {
                "Артикул поставщика", "Название", "Код номенклатуры", "Баркод",
                "Тип документа", "Обоснование для оплаты", "Дата продажи", "Кол-во",
                "Цена розничная с учетом согласованной скидки", "Вайлдберриз реализовал Товар (Пр)",
                "Вознаграждение Вайлдберриз (ВВ), без НДС", "НДС с Вознаграждения Вайлдберриз",
                "К перечислению Продавцу за реализованный Товар", "Услуги по доставке товара покупателю",
                "Общая сумма штрафов", "Хранение", "Удержания", "Операции на приемке",
                "Стоимость участия в программе лояльности", "Страна"
            };

            object[][] rows = new object[][]
            {
                new object[] { "DEMO-CASE-15", "Чехол силиконовый для смартфона", "100101", "4810000000011", "Продажа", "Продажа", "2026-09-02", 18, 22.50m, 405.00m, 48.60m, 9.72m, 346.68m, 34.20m, 0m, 0m, 0m, 0m, 0m, "Беларусь" },
                new object[] { "DEMO-GLASS-9H", "Стекло защитное закалённое 9H", "100102", "4810000000028", "Продажа", "Продажа", "2026-09-03", 25, 14.00m, 350.00m, 42.00m, 8.40m, 299.60m, 37.50m, 0m, 0m, 0m, 0m, 0m, "Россия" },
                new object[] { "DEMO-GLASS-9H", "Стекло защитное закалённое 9H", "100102", "4810000000028", "Возврат", "Возврат", "2026-09-04", 2, 14.00m, 28.00m, 3.36m, 0.67m, 23.97m, 4.00m, 0m, 0m, 0m, 0m, 0m, "Россия" },
                new object[] { "DEMO-LOW-MARGIN", "Держатель авто магнитный (демо-убыточный)", "100105", "4810000000099", "Продажа", "Продажа", "2026-09-05", 6, 26.00m, 156.00m, 22.10m, 4.42m, 129.48m, 18.00m, 5.00m, 0m, 0m, 0m, 0m, "Беларусь" },
                new object[] { "DEMO-CASE-15", "Чехол силиконовый для смартфона", "100101", "4810000000011", "Продажа", "Добровольная компенсация при возврате", "2026-09-06", 1, 0m, 0m, 0m, 0m, 15.20m, 0m, 0m, 0m, 0m, 0m, 0m, "Беларусь" },
                new object[] { "DEMO-CASE-15", "Чехол силиконовый для смартфона", "100101", "4810000000011", "", "Хранение", "2026-09-06", 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 11.40m, 0m, 0m, 0m, "Беларусь" }
            };

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            sb.Append("<row r=\"1\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "1", 2, h[c]));
            sb.Append("</row>");

            for (int i = 0; i < rows.Length; i++)
            {
                int rn = i + 2;
                var r = rows[i];
                sb.Append("<row r=\"" + rn + "\">");
                for (int c = 0; c < r.Length; c++)
                {
                    string coord = ColName(c + 1) + rn;
                    if (r[c] is int) sb.Append(NC(coord, 5, ((int)r[c]).ToString(CultureInfo.InvariantCulture)));
                    else if (r[c] is decimal) sb.Append(MC(coord, 4, (decimal)r[c]));
                    else sb.Append(SC(coord, 3, r[c].ToString()));
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            WriteXlsxWorkbook(path, new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Отчет WB", sb.ToString()) });
        }

        private static void WriteDemoOzonReport(string path)
        {
            string[] h =
            {
                "Артикул продавца", "Ozon ID", "Наименование товара",
                "Реализовано Кол-во", "Реализовано на сумму",
                "Возвращено Кол-во", "Возвращено на сумму",
                "Вознаграждение Ozon", "Услуги доставки", "Итого к начислению"
            };

            object[][] rows = new object[][]
            {
                new object[] { "DEMO-CABLE-1M", "55001122", "Кабель USB-C быстрая зарядка 1 м", 14, 210.00m, 1, 15.00m, 27.30m, 16.80m, 167.70m },
                new object[] { "DEMO-STAND-DESK", "55001133", "Подставка настольная складная", 8, 224.00m, 0, 0m, 29.12m, 14.40m, 194.88m }
            };

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            sb.Append("<row r=\"1\">").Append(SC("A1", 1, "Отчёт о реализации товара Ozon за период с 01.09.2026 по 15.09.2026")).Append("</row>");
            sb.Append("<row r=\"3\">");
            for (int c = 0; c < h.Length; c++) sb.Append(SC(ColName(c + 1) + "3", 2, h[c]));
            sb.Append("</row>");

            for (int i = 0; i < rows.Length; i++)
            {
                int rn = i + 4;
                var r = rows[i];
                sb.Append("<row r=\"" + rn + "\">");
                for (int c = 0; c < r.Length; c++)
                {
                    string coord = ColName(c + 1) + rn;
                    if (r[c] is int) sb.Append(NC(coord, 5, ((int)r[c]).ToString(CultureInfo.InvariantCulture)));
                    else if (r[c] is decimal) sb.Append(MC(coord, 4, (decimal)r[c]));
                    else sb.Append(SC(coord, 3, r[c].ToString()));
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            WriteXlsxWorkbook(path, new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Реализация", sb.ToString()) });
        }

        private static void WriteDemo1CBankFile(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("1CClientBankExchange");
            sb.AppendLine("ВерсияФормата=1.03");
            sb.AppendLine("Кодировка=UTF-8");
            sb.AppendLine("ДатаНачала=01.09.2026");
            sb.AppendLine("ДатаКонца=15.09.2026");
            sb.AppendLine("РасчСчет=BY00DEMO00000000000000000001");
            sb.AppendLine("СекцияДокумент=Платежное поручение");
            sb.AppendLine("Номер=101");
            sb.AppendLine("Дата=08.09.2026");
            sb.AppendLine("ДатаПоступило=08.09.2026");
            sb.AppendLine("Сумма=655.09");
            sb.AppendLine("Плательщик=ООО \"ИМВБРБ\"");
            sb.AppendLine("ПлательщикИНН=193630204");
            sb.AppendLine("ПолучательСчет=BY00DEMO00000000000000000001");
            sb.AppendLine("НазначениеПлатежа=Перечисление денежных средств по договору оферты Wildberries");
            sb.AppendLine("КонецДокумента");

            sb.AppendLine("СекцияДокумент=Платежное поручение");
            sb.AppendLine("Номер=102");
            sb.AppendLine("Дата=10.09.2026");
            sb.AppendLine("ДатаПоступило=10.09.2026");
            sb.AppendLine("Сумма=331.38");
            sb.AppendLine("Плательщик=ООО \"ОЗОН Маркет Бел\"");
            sb.AppendLine("ПлательщикИНН=193602362");
            sb.AppendLine("ПолучательСчет=BY00DEMO00000000000000000001");
            sb.AppendLine("НазначениеПлатежа=Оплата за реализованные товары по договору комиссии Ozon");
            sb.AppendLine("КонецДокумента");

            sb.AppendLine("СекцияДокумент=Платежное поручение");
            sb.AppendLine("Номер=103");
            sb.AppendLine("Дата=12.09.2026");
            sb.AppendLine("ДатаСписано=12.09.2026");
            sb.AppendLine("Сумма=240.00");
            sb.AppendLine("ПлательщикСчет=BY00DEMO00000000000000000001");
            sb.AppendLine("Получатель=ООО \"ОптПоставка Демо\"");
            sb.AppendLine("ПолучательИНН=100000001");
            sb.AppendLine("НазначениеПлатежа=Оплата за товар по счет-фактуре № 45 (аксессуары)");
            sb.AppendLine("КонецДокумента");

            sb.AppendLine("СекцияДокумент=Банковский ордер");
            sb.AppendLine("Номер=104");
            sb.AppendLine("Дата=14.09.2026");
            sb.AppendLine("ДатаСписано=14.09.2026");
            sb.AppendLine("Сумма=19.50");
            sb.AppendLine("ПлательщикСчет=BY00DEMO00000000000000000001");
            sb.AppendLine("Получатель=ОАО \"Белинвестбанк\"");
            sb.AppendLine("ПолучательИНН=807000028");
            sb.AppendLine("НазначениеПлатежа=Плата за пакет РКО и комиссия банка за переводы");
            sb.AppendLine("КонецДокумента");

            sb.AppendLine("СекцияДокумент=Платежное поручение");
            sb.AppendLine("Номер=105");
            sb.AppendLine("Дата=15.09.2026");
            sb.AppendLine("ДатаСписано=15.09.2026");
            sb.AppendLine("Сумма=75.00");
            sb.AppendLine("ПлательщикСчет=BY00DEMO00000000000000000001");
            sb.AppendLine("Получатель=Главное управление Министерства финансов по г. Минску");
            sb.AppendLine("ПолучательИНН=100000000");
            sb.AppendLine("КодНазПлатежа=01001");
            sb.AppendLine("НазначениеПлатежа=Уплата налога в бюджет РБ");
            sb.AppendLine("КонецДокумента");
            sb.AppendLine("КонецФайла");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
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

        private static string CleanXmlStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (ch == '\t' || ch == '\n' || ch == '\r' || (ch >= 0x20 && ch <= 0xD7FF) || (ch >= 0xE000 && ch <= 0xFFFD))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            string clean = CleanXmlStr(s);
            if (clean.Length == 0) return "";
            return clean.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
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

        // ==================== ANONYMIZED DIAGNOSTIC REPORT (SUPPORT) ====================

        public static string GenerateAnonymizedDiagReport(string filePath)
        {
            var sb = new StringBuilder();
            string ver = "unknown";
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null) ver = v.ToString();
            }
            catch { }

            sb.AppendLine("=== Buhly Anonymized File Diagnostic Report ===");
            sb.AppendLine("Buhly Version: " + ver);
            sb.AppendLine("Generated (UTC): " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "Z");

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                sb.AppendLine("Error: File not found.");
                return sb.ToString();
            }

            string ext = (Path.GetExtension(filePath) ?? "").ToLowerInvariant();
            long byteSize = 0;
            try { byteSize = new FileInfo(filePath).Length; } catch { }

            sb.AppendLine("File Extension: " + (string.IsNullOrEmpty(ext) ? "(none)" : ext));
            sb.AppendLine("File Size (bytes): " + byteSize.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();

            if (ext == ".zip")
            {
                try
                {
                    using (var z = ZipFile.OpenRead(filePath))
                    {
                        sb.AppendLine("ZIP Entries Count: " + z.Entries.Count.ToString(CultureInfo.InvariantCulture));
                        int entryIdx = 0;
                        foreach (var entry in z.Entries)
                        {
                            entryIdx++;
                            string eExt = (Path.GetExtension(entry.Name) ?? "").ToLowerInvariant();
                            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                                "--- ZIP Entry #{0} (ext={1}, size={2} bytes) ---",
                                entryIdx, string.IsNullOrEmpty(eExt) ? "(none)" : eExt, entry.Length));

                            if (eExt == ".xlsx")
                            {
                                using (var ms = new MemoryStream())
                                using (var es = entry.Open())
                                {
                                    es.CopyTo(ms);
                                    ms.Position = 0;
                                    AppendXlsxDiag(ms, sb);
                                }
                            }
                            else if (eExt == ".txt" || eExt == ".csv")
                            {
                                using (var ms = new MemoryStream())
                                using (var es = entry.Open())
                                {
                                    es.CopyTo(ms);
                                    AppendTextDiag(ms.ToArray(), sb);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine("ZIP Exception: " + ex.GetType().Name + ": " + SanitizeExceptionMessage(ex.Message, filePath));
                }
            }
            else if (ext == ".xlsx")
            {
                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        AppendXlsxDiag(fs, sb);
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine("XLSX Exception: " + ex.GetType().Name + ": " + SanitizeExceptionMessage(ex.Message, filePath));
                }
            }
            else if (ext == ".txt" || ext == ".csv")
            {
                try
                {
                    byte[] raw = File.ReadAllBytes(filePath);
                    AppendTextDiag(raw, sb);
                }
                catch (Exception ex)
                {
                    sb.AppendLine("Text Exception: " + ex.GetType().Name + ": " + SanitizeExceptionMessage(ex.Message, filePath));
                }
            }
            else
            {
                sb.AppendLine("Detected Parser Type: Unsupported extension (" + ext + ")");
            }

            // Also run full parser dry-run to capture any runtime exception during AnalyzeFiles
            try
            {
                var drySnap = AnalyzeFiles(new string[] { filePath }, CompanyProfilesManager.RegimeUsn6, 6m, new Dictionary<string, decimal>());
                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Parser Dry-Run Summary: WBFiles={0}, OzonFiles={1}, BankFiles={2}, Reports={3}, Skus={4}, BankTx={5}",
                    drySnap.LoadedWbFiles, drySnap.LoadedOzonFiles, drySnap.LoadedBankFiles,
                    drySnap.Reports.Count, drySnap.Skus.Count, drySnap.BankTransactions.Count));
                sb.AppendLine("Parser Exception: (none)");
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine("Parser Exception: " + ex.GetType().Name + ": " + SanitizeExceptionMessage(ex.Message, filePath));
            }

            return sb.ToString();
        }

        private static string DecodeBankTextWithEncoding(byte[] bytes, out string encodingName)
        {
            if (bytes == null || bytes.Length == 0)
            {
                encodingName = "empty";
                return "";
            }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encodingName = "UTF-8 (BOM)";
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            string asciiProbe = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
            if (asciiProbe.IndexOf("Кодировка=UTF-8", StringComparison.OrdinalIgnoreCase) >= 0 ||
                asciiProbe.IndexOf("UTF-8", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                encodingName = "UTF-8 (declared)";
                return Encoding.UTF8.GetString(bytes);
            }
            if (asciiProbe.IndexOf("Кодировка=DOS", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try
                {
                    encodingName = "CP866 (DOS)";
                    return Encoding.GetEncoding(866).GetString(bytes);
                }
                catch { }
            }
            if (asciiProbe.IndexOf("Кодировка=Windows", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try
                {
                    encodingName = "Windows-1251 (declared)";
                    return Encoding.GetEncoding(1251).GetString(bytes);
                }
                catch { }
            }

            try
            {
                var strictUtf8 = new UTF8Encoding(false, true);
                string s = strictUtf8.GetString(bytes);
                encodingName = "UTF-8";
                return s;
            }
            catch
            {
                try
                {
                    encodingName = "Windows-1251";
                    return Encoding.GetEncoding(1251).GetString(bytes);
                }
                catch
                {
                    encodingName = "System Default";
                    return Encoding.Default.GetString(bytes);
                }
            }
        }

        private static List<string> ReadXlsxSheetNames(Stream stream)
        {
            var names = new List<string>();
            long origPos = stream.CanSeek ? stream.Position : 0;
            try
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    var wbEntry = zip.GetEntry("xl/workbook.xml");
                    if (wbEntry != null)
                    {
                        using (var s = wbEntry.Open())
                        {
                            var doc = XDocument.Load(s);
                            foreach (var sh in doc.Descendants(MainNs + "sheet"))
                            {
                                var nameAttr = sh.Attribute("name");
                                if (nameAttr != null && !string.IsNullOrWhiteSpace(nameAttr.Value))
                                    names.Add(nameAttr.Value);
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (stream.CanSeek) stream.Position = origPos;
            }
            return names;
        }

        private static void AppendXlsxDiag(Stream xlsxStream, StringBuilder sb)
        {
            var sheetNames = ReadXlsxSheetNames(xlsxStream);
            sb.AppendLine("XLSX Sheet Names (" + sheetNames.Count + "): " + (sheetNames.Count > 0 ? string.Join(", ", sheetNames.ToArray()) : "(none)"));

            var allSheets = ReadXlsxAllSheets(xlsxStream);
            sb.AppendLine("XLSX Readable Sheets Count: " + allSheets.Count.ToString(CultureInfo.InvariantCulture));

            for (int si = 0; si < allSheets.Count; si++)
            {
                var rows = allSheets[si];
                string sName = si < sheetNames.Count ? sheetNames[si] : ("Sheet #" + (si + 1));
                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[Sheet #{0}: {1}, Rows={2}]", si + 1, sName, rows != null ? rows.Count : 0));

                if (rows == null || rows.Count == 0)
                {
                    sb.AppendLine("Detected Parser Type: Empty sheet");
                    continue;
                }

                string parserType = "Unknown";
                int hdrIdx = DetectBankHeader(rows);
                if (hdrIdx >= 0)
                {
                    parserType = "Bank Statement (Excel)";
                }
                else
                {
                    hdrIdx = DetectWbReportHeader(rows);
                    if (hdrIdx >= 0)
                    {
                        parserType = "WB Report (Excel)";
                    }
                    else
                    {
                        hdrIdx = DetectOzonReportHeader(rows);
                        if (hdrIdx >= 0)
                            parserType = "Ozon Report (Excel)";
                    }
                }

                sb.AppendLine("Detected Parser Type: " + parserType);
                sb.AppendLine("Detected Header Row Index (0-based): " + hdrIdx.ToString(CultureInfo.InvariantCulture));

                if (hdrIdx >= 0)
                {
                    var hdr = rows[hdrIdx];
                    sb.AppendLine("Header Row (unmasked): " + FormatRowUnmasked(hdr));

                    int dataStart = hdrIdx + 1;
                    if (parserType == "Ozon Report (Excel)" && hdrIdx + 1 < rows.Count)
                    {
                        int subHits = 0;
                        foreach (var v in rows[hdrIdx + 1].Values)
                        {
                            string n = NormalizeHeader(v);
                            if (n == "кол-во" || n == "количество" || n == "сумма" || n == "цена" || n == "итого" || n.Contains("кол-во"))
                                subHits++;
                        }
                        if (subHits >= 2)
                        {
                            sb.AppendLine("Sub-Header Row (unmasked): " + FormatRowUnmasked(rows[hdrIdx + 1]));
                            dataStart = hdrIdx + 2;
                        }
                    }

                    AppendMaskedSampleRows(rows, dataStart, hdr, 10, sb);
                }
                else
                {
                    AppendUnknownCandidateAndSampleRows(rows, sb);
                }
            }
        }

        private static void AppendTextDiag(byte[] rawBytes, StringBuilder sb)
        {
            string encName;
            string text = DecodeBankTextWithEncoding(rawBytes, out encName);
            sb.AppendLine("Detected Encoding: " + encName);

            if (string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine("Detected Parser Type: Empty text");
                return;
            }

            if (text.IndexOf("1CClientBankExchange", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("СекцияДокумент=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sb.AppendLine("Detected Parser Type: 1CClientBankExchange");
                sb.AppendLine("Detected Header Row Index (0-based): 0");

                var lines = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var headerPairs = new List<string>();
                var sampleDocs = new List<string>();
                var currentDocParts = new List<string>();
                bool inDoc = false;

                foreach (var rawLine in lines)
                {
                    string line = (rawLine ?? "").Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("КонецФайла", StringComparison.OrdinalIgnoreCase)) break;

                    if (line.StartsWith("СекцияДокумент=", StringComparison.OrdinalIgnoreCase))
                    {
                        inDoc = true;
                        currentDocParts.Clear();
                        allKeys.Add("СекцияДокумент");
                        currentDocParts.Add("СекцияДокумент=" + MaskDiagValue("СекцияДокумент", line.Substring(line.IndexOf('=') + 1)));
                        continue;
                    }
                    if (line.Equals("КонецДокумента", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inDoc && sampleDocs.Count < 10)
                            sampleDocs.Add(string.Join(" | ", currentDocParts.ToArray()));
                        inDoc = false;
                        currentDocParts.Clear();
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        allKeys.Add(k);
                        string masked = MaskDiagValue(k, v);
                        if (!inDoc)
                        {
                            if (string.Equals(k, "ВерсияФормата", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(k, "Кодировка", StringComparison.OrdinalIgnoreCase))
                                headerPairs.Add(k + "=" + v);
                            else
                                headerPairs.Add(k + "=" + masked);
                        }
                        else if (sampleDocs.Count < 10)
                        {
                            currentDocParts.Add(k + "=" + masked);
                        }
                    }
                }

                sb.AppendLine("Header Keys (unmasked): " + string.Join(", ", allKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()));
                if (headerPairs.Count > 0)
                    sb.AppendLine("File Preamble (masked values): " + string.Join(" | ", headerPairs.ToArray()));
                sb.AppendLine("Sample Data Rows (up to 10 documents, strictly masked):");
                for (int i = 0; i < sampleDocs.Count; i++)
                    sb.AppendLine("  Doc #" + (i + 1) + ": " + sampleDocs[i]);
                return;
            }

            var csvRows = ParseCsvRows(text);
            if (csvRows.Count == 0)
            {
                sb.AppendLine("Detected Parser Type: Unknown (fewer than 2 rows)");
                var rawLines = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                int candLimit = Math.Min(3, rawLines.Length);
                for (int i = 0; i < candLimit; i++)
                    sb.AppendLine("  Candidate Line #" + i + ": " + SanitizeCandidateText(rawLines[i]));
                return;
            }

            string parserType = "Unknown";
            int hdrIdx = DetectWbReportHeader(csvRows);
            if (hdrIdx >= 0) parserType = "WB Report (CSV)";
            else
            {
                hdrIdx = DetectOzonReportHeader(csvRows);
                if (hdrIdx >= 0) parserType = "Ozon Report (CSV)";
                else
                {
                    hdrIdx = DetectBankHeader(csvRows);
                    if (hdrIdx >= 0) parserType = "Bank Statement (CSV)";
                }
            }

            sb.AppendLine("Detected Parser Type: " + parserType);
            sb.AppendLine("Detected Header Row Index (0-based): " + hdrIdx.ToString(CultureInfo.InvariantCulture));
            if (hdrIdx >= 0)
            {
                sb.AppendLine("Header Row (unmasked): " + FormatRowUnmasked(csvRows[hdrIdx]));
                AppendMaskedSampleRows(csvRows, hdrIdx + 1, csvRows[hdrIdx], 10, sb);
            }
            else
            {
                AppendUnknownCandidateAndSampleRows(csvRows, sb);
            }
        }

        private static string FormatRowUnmasked(Dictionary<int, string> row)
        {
            if (row == null || row.Count == 0) return "(empty)";
            var parts = new List<string>();
            foreach (var kv in row.OrderBy(k => k.Key))
            {
                string val = CleanXmlStr((kv.Value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim());
                if (!string.IsNullOrEmpty(val))
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "[C{0}] {1}", kv.Key, val));
            }
            return parts.Count > 0 ? string.Join(" | ", parts.ToArray()) : "(empty)";
        }

        private static void AppendMaskedSampleRows(
            List<Dictionary<int, string>> rows,
            int startIdx,
            Dictionary<int, string> hdrRow,
            int maxSamples,
            StringBuilder sb)
        {
            sb.AppendLine("Sample Data Rows (up to " + maxSamples + ", strictly masked):");
            int printed = 0;
            for (int r = startIdx; r < rows.Count && printed < maxSamples; r++)
            {
                var row = rows[r];
                if (row == null || row.Count == 0) continue;
                var parts = new List<string>();
                foreach (var kv in row.OrderBy(k => k.Key))
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    string hdrText = "";
                    if (hdrRow != null) hdrRow.TryGetValue(kv.Key, out hdrText);
                    string masked = MaskDiagValue(hdrText, kv.Value);
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "[C{0}] {1}", kv.Key, masked));
                }
                if (parts.Count == 0) continue;
                printed++;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Row {0}: {1}", r, string.Join(" | ", parts.ToArray())));
            }
            if (printed == 0) sb.AppendLine("  (no non-empty data rows)");
        }

        private static bool RowLooksLikeDataRow(Dictionary<int, string> row)
        {
            if (row == null) return false;
            foreach (var v in row.Values)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                string t = v.Trim();
                string noSpace = Regex.Replace(t, @"\s+", "");
                if (Regex.IsMatch(noSpace, @"^BY\d{2}[A-Za-z0-9]{16,30}$", RegexOptions.IgnoreCase)) return true;
                if (Regex.IsMatch(noSpace, @"^\d{9}$")) return true;
                if (Regex.IsMatch(t, @"^(\d{2}[./-]\d{2}[./-]\d{4}|\d{4}-\d{2}-\d{2})([ T]\d{2}:\d{2}(:\d{2})?)?$")) return true;
                string numCand = t.Replace("\u00A0", "").Replace(" ", "");
                if (Regex.IsMatch(numCand, @"^[+-]?\d+([.,]\d+)?$")) return true;
            }
            return false;
        }

        private static void AppendUnknownCandidateAndSampleRows(List<Dictionary<int, string>> rows, StringBuilder sb)
        {
            sb.AppendLine("Candidate Header Rows (up to 3 non-data rows, sensitive tokens sanitized):");
            int candPrinted = 0;
            int firstDataRowIdx = rows.Count;
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row == null || row.Count == 0) continue;
                bool hasNonEmpty = false;
                foreach (var v in row.Values) { if (!string.IsNullOrWhiteSpace(v)) { hasNonEmpty = true; break; } }
                if (!hasNonEmpty) continue;

                if (RowLooksLikeDataRow(row))
                {
                    firstDataRowIdx = r;
                    break;
                }

                if (candPrinted >= 3)
                {
                    firstDataRowIdx = r;
                    break;
                }

                var parts = new List<string>();
                foreach (var kv in row.OrderBy(k => k.Key))
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "[C{0}] {1}", kv.Key, SanitizeCandidateText(kv.Value)));
                }
                if (parts.Count == 0) continue;
                candPrinted++;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  CandRow {0}: {1}", r, string.Join(" | ", parts.ToArray())));
                firstDataRowIdx = r + 1;
            }
            if (candPrinted == 0) sb.AppendLine("  (none)");

            AppendMaskedSampleRows(rows, firstDataRowIdx, null, 10, sb);
        }

        private static string SanitizeCandidateText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Replace('\r', ' ').Replace('\n', ' ').Trim();
            s = Regex.Replace(s, @"\bBY\d{2}[A-Za-z0-9]{16,30}\b", "BY00XXXX...", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\b\d{9}\b", "190000000");
            s = Regex.Replace(s, @"\b(\d{2}[./-]\d{2}[./-]\d{4}|\d{4}-\d{2}-\d{2})\b", "[DATE:XXXX-XX]");
            s = Regex.Replace(s, @"(?<!\w)[+-]?\d+([.,]\d+)(?!\w)", "[NUM:XXX.XX]");
            return s;
        }

        private static string SanitizeExceptionMessage(string msg, string filePath)
        {
            if (string.IsNullOrEmpty(msg)) return "";
            string s = msg;
            if (!string.IsNullOrEmpty(filePath))
            {
                s = s.Replace(filePath, "<FILE>");
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) s = s.Replace(dir, "<DIR>");
            }
            s = Regex.Replace(s, @"\bBY\d{2}[A-Za-z0-9]{16,30}\b", "BY00XXXX...", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\b\d{9}\b", "190000000");
            return s;
        }

        public static string MaskDiagValue(string colHeaderOrKey, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string t = raw.Trim();
            string noSpace = Regex.Replace(t, @"\s+", "");

            // 1. IBAN (BY...)
            if (Regex.IsMatch(noSpace, @"^BY\d{2}[A-Za-z0-9]{16,30}$", RegexOptions.IgnoreCase))
                return "BY00XXXX...";

            // 2. 9-digit UNP / INN
            if (Regex.IsMatch(noSpace, @"^\d{9}$"))
                return "190000000";

            // 3. Dates -> [DATE:yyyy-MM]
            string hLow = NormalizeHeader(colHeaderOrKey ?? "");
            bool isDateHeader = hLow.Contains("дата") || hLow.Contains("date");
            if (Regex.IsMatch(t, @"^(\d{2}[./-]\d{2}[./-]\d{4}|\d{4}-\d{2}-\d{2})([ T]\d{2}:\d{2}(:\d{2})?)?$"))
            {
                DateTime dt = ParseFlexibleDate(t);
                if (dt != DateTime.MinValue)
                    return string.Format(CultureInfo.InvariantCulture, "[DATE:{0:yyyy-MM}]", dt);
            }
            else if (isDateHeader && Regex.IsMatch(noSpace, @"^\d{5}([.,]\d+)?$"))
            {
                DateTime dt = ParseFlexibleDate(noSpace);
                if (dt != DateTime.MinValue)
                    return string.Format(CultureInfo.InvariantCulture, "[DATE:{0:yyyy-MM}]", dt);
            }

            // 4. Numbers (amounts / quantities)
            string numCand = t.Replace("\u00A0", "").Replace(" ", "");
            var numMatch = Regex.Match(numCand, @"^([+-]?)(\d+)([.,](\d+))?$");
            if (numMatch.Success)
            {
                string signRaw = numMatch.Groups[1].Value;
                string intDigits = numMatch.Groups[2].Value;
                bool hasFrac = numMatch.Groups[3].Success;
                if (hasFrac)
                {
                    string sign = signRaw == "-" ? "-" : "+";
                    string xInt = new string('X', Math.Min(intDigits.Length, 12));
                    string xFrac = new string('X', Math.Min(numMatch.Groups[4].Value.Length, 8));
                    return "[NUM:" + sign + xInt + "." + xFrac + "]";
                }
                else
                {
                    string sign = signRaw == "-" ? "-" : "";
                    return "[INT:" + sign + intDigits.Length.ToString(CultureInfo.InvariantCulture) + "d]";
                }
            }

            // 5. All other text -> [TEXT len=N]
            return string.Format(CultureInfo.InvariantCulture, "[TEXT len={0}]", t.Length);
        }

        // ==================== BUILT-IN SELF-TEST SUITE (--self-test) ====================

        public static bool RunSelfTests()
        {
            return RunSelfTests(Console.Out);
        }

        public static bool RunSelfTests(TextWriter output)
        {
            if (output == null) output = TextWriter.Null;
            string tempRoot = Path.Combine(Path.GetTempPath(), "Buhly_SelfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            int passed = 0;
            int total = 7;

            try
            {
                if (RunSingleSelfTest(output, "1. Demo WB + Ozon + 1C Bank -> USN_6 / IP_PN_20 / OSN_20", delegate
                {
                    string dir = Path.Combine(tempRoot, "t1");
                    Dictionary<string, decimal> demoCosts;
                    var files = GenerateDemoAnalyticsFiles(dir, out demoCosts);

                    var usn = AnalyzeFiles(files, CompanyProfilesManager.RegimeUsn6, 6m, demoCosts);
                    AssertEqDec(usn.TotalRealizedP, 1302.00m, "USN TotalRealizedP");
                    AssertEqDec(usn.TotalCompensations, 15.20m, "USN TotalCompensations");
                    AssertEqDec(usn.TotalExpectedPayout, 988.27m, "USN TotalExpectedPayout");
                    AssertEqDec(usn.MpServicesExpenses, 328.93m, "USN MpServicesExpenses");
                    AssertEqDec(usn.TotalCostOfGoods, 469.30m, "USN TotalCostOfGoods");
                    AssertEqDec(usn.TaxBaseAmount, 1317.20m, "USN TaxBaseAmount");
                    AssertEqDec(usn.EstimatedTaxByRealization, 79.03m, "USN Tax 6%");

                    var ip = AnalyzeFiles(files, CompanyProfilesManager.RegimeIpPn20, 20m, demoCosts);
                    AssertTrue(ip.IpHasPotentialDoubleGoodsCost, "IP double-goods guard active on demo");
                    AssertEqDec(ip.IpBankDeductibleExpenses, 19.50m, "IP bank deductible excludes 240 BYN goods");
                    AssertEqDec(ip.IpDocumentedExpenses, 817.73m, "IP documented expenses");
                    AssertEqDec(ip.TaxBaseAmount, 499.47m, "IP TaxBaseAmount");
                    AssertEqDec(ip.EstimatedTaxByRealization, 99.89m, "IP Tax 20%");
                    AssertEqDec(ip.IpFszn35, 174.81m, "IP FSZN 35%");

                    var osn = AnalyzeFiles(files, CompanyProfilesManager.RegimeOsn20, 20m, demoCosts);
                    AssertEqDec(osn.OsnVat20, 219.53m, "OSN VAT 20%");
                    AssertEqDec(osn.OsnProfitTaxBase, 39.94m, "OSN ProfitTaxBase");
                    AssertEqDec(osn.OsnProfitTax20, 7.99m, "OSN ProfitTax 20%");
                    AssertEqDec(osn.EstimatedTaxByRealization, 227.52m, "OSN TotalTax");
                })) passed++;

                if (RunSingleSelfTest(output, "2. Ozon Accruals + 2-row merged header + Ozon CSV", delegate
                {
                    string dir = Path.Combine(tempRoot, "t2");
                    Directory.CreateDirectory(dir);

                    // 2a. Ozon Accruals ("Доставка покупателю" as sale, not logistics)
                    string accPath = Path.Combine(dir, "ozon_accruals.xlsx");
                    WriteSimpleXlsx(accPath, "Начисления", new string[]
                    {
                        "Тип начисления", "Артикул продавца", "Наименование товара", "Количество", "Цена продавца", "Вознаграждение Ozon", "Итого к начислению"
                    }, new object[][]
                    {
                        new object[] { "Доставка покупателю", "OZ-ART-1", "Товар А", 2, 100.00m, 15.00m, 85.00m },
                        new object[] { "Услуги доставки товара", "OZ-ART-1", "Товар А", 0, 0m, 0m, -12.00m }
                    });
                    var accSnap = AnalyzeFiles(new string[] { accPath }, CompanyProfilesManager.RegimeUsn6, 6m, null);
                    AssertEqInt(accSnap.OzonReportCount, 1, "Ozon Accruals report count");
                    AssertEqInt(accSnap.TotalSaleQty, 2, "Ozon Accruals SaleQty");
                    AssertEqDec(accSnap.TotalRealizedP, 100.00m, "Ozon Accruals RealizedP");
                    AssertEqDec(accSnap.TotalLogistics, 12.00m, "Ozon Accruals Logistics");
                    AssertEqDec(accSnap.TotalExpectedPayout, 73.00m, "Ozon Accruals ExpectedPayout");

                    // 2b. Ozon 2-row merged header
                    string mergedPath = Path.Combine(dir, "ozon_merged.xlsx");
                    WriteCustomRowsXlsx(mergedPath, "Реализация", new object[][]
                    {
                        new object[] { "Отчет о реализации Ozon", "", "", "", "", "", "", "" },
                        new object[] { "Артикул продавца", "Наименование товара", "Реализовано", "", "Возвращено", "", "Вознаграждение Ozon", "Итого" },
                        new object[] { "", "", "Кол-во", "Сумма", "Кол-во", "Сумма", "", "" },
                        new object[] { "OZ-MERG-1", "Товар Б", 5, 250.00m, 1, 50.00m, 30.00m, 170.00m }
                    });
                    var mergedSnap = AnalyzeFiles(new string[] { mergedPath }, CompanyProfilesManager.RegimeUsn6, 6m, null);
                    AssertEqInt(mergedSnap.OzonReportCount, 1, "Ozon 2-row report count");
                    AssertEqInt(mergedSnap.TotalSaleQty, 5, "Ozon 2-row SaleQty");
                    AssertEqInt(mergedSnap.TotalReturnQty, 1, "Ozon 2-row ReturnQty");
                    AssertEqDec(mergedSnap.TotalRealizedP, 200.00m, "Ozon 2-row RealizedP");
                    AssertEqDec(mergedSnap.TotalExpectedPayout, 170.00m, "Ozon 2-row ExpectedPayout");

                    // 2c. Ozon CSV
                    string csvPath = Path.Combine(dir, "ozon_report.csv");
                    string csvText =
                        "Артикул продавца;Наименование товара;Реализовано Кол-во;Реализовано на сумму;Вознаграждение Ozon;Итого к начислению\r\n" +
                        "OZ-CSV-1;Товар С;3;90.00;13.50;76.50\r\n";
                    File.WriteAllText(csvPath, csvText, new UTF8Encoding(true));
                    var csvSnap = AnalyzeFiles(new string[] { csvPath }, CompanyProfilesManager.RegimeUsn6, 6m, null);
                    AssertEqInt(csvSnap.LoadedOzonFiles, 1, "Ozon CSV LoadedOzonFiles");
                    AssertEqInt(csvSnap.TotalSaleQty, 3, "Ozon CSV SaleQty");
                    AssertEqDec(csvSnap.TotalRealizedP, 90.00m, "Ozon CSV RealizedP");
                    AssertEqDec(csvSnap.TotalExpectedPayout, 76.50m, "Ozon CSV ExpectedPayout");
                })) passed++;

                if (RunSingleSelfTest(output, "3. 1CClientBank (Win-1251/UTF-8) + Excel Bank (2-col/1-col) + ClassifyBankTx", delegate
                {
                    string dir = Path.Combine(tempRoot, "t3");
                    Directory.CreateDirectory(dir);

                    // 3a. 1C Win-1251
                    string c1251Path = Path.Combine(dir, "bank_1251.txt");
                    string c1251Text =
                        "1CClientBankExchange\r\nКодировка=Windows\r\nРасчСчет=BY00TEST00000000000000000001\r\n" +
                        "СекцияДокумент=Платежное поручение\r\nНомер=201\r\nДата=05.09.2026\r\nДатаСписано=05.09.2026\r\nСумма=300.00\r\n" +
                        "ПлательщикСчет=BY00TEST00000000000000000001\r\nПолучатель=ПАО Сбербанк\r\nПолучательИНН=190000011\r\n" +
                        "НазначениеПлатежа=Оплата по договору поставки № 15 за оборудование\r\nКонецДокумента\r\nКонецФайла\r\n";
                    File.WriteAllBytes(c1251Path, Encoding.GetEncoding(1251).GetBytes(c1251Text));

                    // 3b. 1C UTF-8
                    string cUtf8Path = Path.Combine(dir, "bank_utf8.txt");
                    string cUtf8Text =
                        "1CClientBankExchange\r\nКодировка=UTF-8\r\nРасчСчет=BY00TEST00000000000000000001\r\n" +
                        "СекцияДокумент=Банковский ордер\r\nНомер=202\r\nДата=06.09.2026\r\nДатаСписано=06.09.2026\r\nСумма=12.50\r\n" +
                        "ПлательщикСчет=BY00TEST00000000000000000001\r\nПолучатель=ПАО Сбербанк\r\nПолучательИНН=190000011\r\n" +
                        "НазначениеПлатежа=Комиссия банка за платежное поручение\r\nКонецДокумента\r\nКонецФайла\r\n";
                    File.WriteAllText(cUtf8Path, cUtf8Text, new UTF8Encoding(false));

                    // 3c. Excel Bank 2-col
                    string xl2Path = Path.Combine(dir, "bank_2col.xlsx");
                    WriteSimpleXlsx(xl2Path, "Выписка", new string[]
                    {
                        "Дата операции", "Номер док.", "Корреспондент", "УНП", "Назначение платежа", "Дебет", "Кредит"
                    }, new object[][]
                    {
                        new object[] { "07.09.2026", "301", "ООО \"ИМВБРБ\"", "193630204", "Выплата по оферте WB", 0m, 500.00m }
                    });

                    // 3d. Excel Bank 1-col
                    string xl1Path = Path.Combine(dir, "bank_1col.xlsx");
                    WriteSimpleXlsx(xl1Path, "Выписка", new string[]
                    {
                        "Дата проводки", "Номер", "Контрагент", "УНП", "Назначение", "Сумма операции", "Дт/Кт"
                    }, new object[][]
                    {
                        new object[] { "08.09.2026", "401", "ООО \"Поставщик\"", "190000022", "За товар по ТН", 120.00m, "Дт" }
                    });

                    var bankSnap = AnalyzeFiles(new string[] { c1251Path, cUtf8Path, xl2Path, xl1Path }, CompanyProfilesManager.RegimeUsn6, 6m, null);
                    AssertEqInt(bankSnap.LoadedBankFiles, 4, "LoadedBankFiles == 4");
                    AssertEqInt(bankSnap.BankTransactions.Count, 4, "BankTransactions.Count == 4");

                    var sberVendorTx = bankSnap.BankTransactions.FirstOrDefault(t => t.DocNum == "201");
                    AssertTrue(sberVendorTx != null, "1C Win-1251 tx parsed");
                    AssertTrue(sberVendorTx.Counterparty.Contains("Сбербанк"), "1C Win-1251 Cyrillic decoded");
                    AssertTrue(sberVendorTx.Category == "Поставщики и расходы", "Sberbank vendor payment != bank fee (got: " + sberVendorTx.Category + ")");

                    var sberFeeTx = bankSnap.BankTransactions.FirstOrDefault(t => t.DocNum == "202");
                    AssertTrue(sberFeeTx != null && sberFeeTx.Category == "Комиссия банка", "Sberbank fee classified as Комиссия банка");

                    AssertEqDec(bankSnap.BankWbCredits, 500.00m, "Excel 2-col WB credit");
                    AssertEqDec(bankSnap.BankVendorsDebet, 420.00m, "Vendors debet (300 + 120)");
                    AssertEqDec(bankSnap.BankFeesDebet, 12.50m, "Bank fees debet");
                })) passed++;

                if (RunSingleSelfTest(output, "4. WB report deduplication when renamed without report number", delegate
                {
                    string dir = Path.Combine(tempRoot, "t4");
                    Dictionary<string, decimal> demoCosts;
                    var files = GenerateDemoAnalyticsFiles(dir, out demoCosts);
                    string wbOrig = files[0];
                    string wbRenamed = Path.Combine(dir, "renamed_report_copy.xlsx");
                    File.Copy(wbOrig, wbRenamed, true);

                    var dupSnap = AnalyzeFiles(new string[] { wbOrig, wbRenamed }, CompanyProfilesManager.RegimeUsn6, 6m, demoCosts);
                    AssertEqInt(dupSnap.Reports.Count, 1, "Reports.Count == 1 after dedup");
                    AssertEqInt(dupSnap.DeduplicatedSkipped, 1, "DeduplicatedSkipped == 1");
                    AssertEqDec(dupSnap.TotalRealizedP, 883.00m, "TotalRealizedP not doubled");
                })) passed++;

                if (RunSingleSelfTest(output, "5. RB 2026 IP Law #90-Z (FSZN 35%, 12 MZP cap, 500k 30%, double-cost guard)", delegate
                {
                    string dir = Path.Combine(tempRoot, "t5");
                    Directory.CreateDirectory(dir);

                    // Case A: net 5000 (gross 10000 - documented logistics 5000 = 5000) -> FSZN 1750.00
                    string pA = Path.Combine(dir, "wb_net5k.xlsx");
                    WriteMinimalWbXlsx(pA, 10000m, 5000m, 0m);
                    var sA = AnalyzeFiles(new string[] { pA }, CompanyProfilesManager.RegimeIpPn20, 20m, null);
                    AssertTrue(!sA.IpUsedStandardDeduction, "Case A uses documented deduction");
                    AssertEqDec(sA.TaxBaseAmount, 5000.00m, "Case A net base 5000");
                    AssertEqDec(sA.IpFszn35, 1750.00m, "Case A FSZN 1750.00");

                    // Case B: net 50000 (gross 100000 - logistics 50000 = 50000) -> FSZN capped 12 MZP = 10296 -> 3603.60
                    string pB = Path.Combine(dir, "wb_net50k.xlsx");
                    WriteMinimalWbXlsx(pB, 100000m, 50000m, 0m);
                    var sB = AnalyzeFiles(new string[] { pB }, CompanyProfilesManager.RegimeIpPn20, 20m, null);
                    AssertEqDec(sB.TaxBaseAmount, 50000.00m, "Case B net base 50000");
                    AssertTrue(sB.IpFsznCapped12Mzp, "Case B FSZN capped at 12 MZP");
                    AssertEqDec(sB.IpFsznBase, 10296.00m, "Case B FSZN base 10296");
                    AssertEqDec(sB.IpFszn35, 3603.60m, "Case B FSZN 3603.60");

                    // Case C: gross 450000 -> IpNear500kLimit = true, rate = 20%
                    string pC = Path.Combine(dir, "wb_gross450k.xlsx");
                    WriteMinimalWbXlsx(pC, 450000m, 450000m, 0m);
                    var sC = AnalyzeFiles(new string[] { pC }, CompanyProfilesManager.RegimeIpPn20, 20m, null);
                    AssertEqDec(sC.IpGrossIncome, 450000.00m, "Case C gross 450000");
                    AssertTrue(sC.IpNear500kLimit, "Case C IpNear500kLimit == true");
                    AssertEqDec(sC.EffectiveTaxRateApplied, 20m, "Case C rate 20%");

                    // Case D: gross 600000 -> rate 30% + FSZN 3603.60
                    string pD = Path.Combine(dir, "wb_gross600k.xlsx");
                    WriteMinimalWbXlsx(pD, 600000m, 300000m, 0m);
                    var sD = AnalyzeFiles(new string[] { pD }, CompanyProfilesManager.RegimeIpPn20, 20m, null);
                    AssertEqDec(sD.IpGrossIncome, 600000.00m, "Case D gross 600000");
                    AssertTrue(sD.IpHighIncomeRateApplied, "Case D IpHighIncomeRateApplied == true");
                    AssertEqDec(sD.EffectiveTaxRateApplied, 30m, "Case D rate 30%");
                    AssertEqDec(sD.IpFszn35, 3603.60m, "Case D FSZN 3603.60");

                    // Case E: double-goods-cost guard active
                    string bE = Path.Combine(dir, "bank_goods.txt");
                    File.WriteAllText(bE,
                        "1CClientBankExchange\r\nКодировка=UTF-8\r\nРасчСчет=BY00TEST00000000000000000002\r\n" +
                        "СекцияДокумент=Платежное поручение\r\nНомер=1\r\nДата=10.09.2026\r\nДатаСписано=10.09.2026\r\nСумма=800.00\r\n" +
                        "ПлательщикСчет=BY00TEST00000000000000000002\r\nПолучатель=ООО Опт\r\nПолучательИНН=190000033\r\n" +
                        "НазначениеПлатежа=Оплата за товар по накладной № 5\r\nКонецДокумента\r\nКонецФайла\r\n",
                        new UTF8Encoding(false));
                    var costsE = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    costsE["TEST-SKU"] = 100.00m;
                    var sE = AnalyzeFiles(new string[] { pA, bE }, CompanyProfilesManager.RegimeIpPn20, 20m, costsE);
                    AssertTrue(sE.IpHasPotentialDoubleGoodsCost, "Case E double-goods-cost guard active");
                    AssertEqDec(sE.IpBankDeductibleExpenses, 0m, "Case E excludes bank goods payment");
                })) passed++;

                if (RunSingleSelfTest(output, "6. Exports (Summary XLSX, KUDIR XLSX, 1C XML/CSV) + Waybill Demo", delegate
                {
                    string dir = Path.Combine(tempRoot, "t6");
                    Dictionary<string, decimal> demoCosts;
                    var files = GenerateDemoAnalyticsFiles(dir, out demoCosts);
                    var snapIp = AnalyzeFiles(files, CompanyProfilesManager.RegimeIpPn20, 20m, demoCosts);

                    var profileIp = new CompanyProfile
                    {
                        DisplayName = "ИП Тестов Т.Т.",
                        OrgInfo = "ИП Тестов Т.Т., УНП 190000099",
                        Unp = "190000099",
                        TaxRegime = CompanyProfilesManager.RegimeIpPn20
                    };

                    string summaryXlsx = Path.Combine(dir, "out_summary.xlsx");
                    ExportAnalyticsXlsx(snapIp, summaryXlsx);
                    ValidateXlsxXmlEntries(summaryXlsx);

                    string kudirXlsx = Path.Combine(dir, "out_kudir.xlsx");
                    ExportKudirXlsx(snapIp, profileIp, kudirXlsx);
                    ValidateXlsxXmlEntries(kudirXlsx);

                    string xml1c = Path.Combine(dir, "out_1c.xml");
                    Export1CXml(snapIp, profileIp, xml1c);
                    using (var fs = File.OpenRead(xml1c))
                        XDocument.Load(fs);

                    string csv1c = Path.Combine(dir, "out_1c.csv");
                    Export1CCsv(snapIp, profileIp, csv1c);
                    string csvContent = File.ReadAllText(csv1c, Encoding.UTF8);
                    AssertTrue(!string.IsNullOrWhiteSpace(csvContent) && csvContent.Contains("Тип_записи") && csvContent.Contains("Отчет_МП") && csvContent.Contains("Выписка_РБ"), "1C CSV contains expected header and rows");

                    string supplyXlsx = Path.Combine(dir, "demo_supply.xlsx");
                    WaybillEngine.GenerateDemoSupplyFile(supplyXlsx);
                    ValidateXlsxXmlEntries(supplyXlsx);
                    var wbRes = WaybillEngine.ParseSupplyExcelFiles(new string[] { supplyXlsx }, new WaybillProfile());
                    AssertTrue(wbRes != null && wbRes.GroupedItems.Count > 0, "Waybill ParseSupplyExcelFiles parsed items");
                    AssertTrue(wbRes.TotalQty > 0 && wbRes.TotalWithVat > 0m, "Waybill totals > 0");
                })) passed++;

                if (RunSingleSelfTest(output, "7. CalculationAuditDetails (USN_6 & IP_PN_20) + CompanyProfileStore UiMode Simple/Pro", delegate
                {
                    string dir = Path.Combine(tempRoot, "t7");
                    Dictionary<string, decimal> demoCosts;
                    var files = GenerateDemoAnalyticsFiles(dir, out demoCosts);

                    var usn = AnalyzeFiles(files, CompanyProfilesManager.RegimeUsn6, 6m, demoCosts);
                    AssertTrue(usn.CalculationAuditDetails != null && usn.CalculationAuditDetails.Count >= 4, "USN_6 CalculationAuditDetails.Count >= 4");
                    string usnJoined = string.Join("\n", usn.CalculationAuditDetails);
                    AssertTrue(usnJoined.Contains("УСН") && usnJoined.Contains("Сверка"), "USN_6 audit contains USN & reconciliation");

                    var ip = AnalyzeFiles(files, CompanyProfilesManager.RegimeIpPn20, 20m, demoCosts);
                    AssertTrue(ip.CalculationAuditDetails != null && ip.CalculationAuditDetails.Count >= 4, "IP_PN_20 CalculationAuditDetails.Count >= 4");
                    string ipJoined = string.Join("\n", ip.CalculationAuditDetails);
                    AssertTrue(ipJoined.Contains("Подоходный") && ipJoined.Contains("ФСЗН"), "IP_PN_20 audit contains Income tax & FSZN");

                    // CompanyProfileStore UiMode defaults, round-trip XML serialization & switching in temp file
                    var store = new CompanyProfileStore();
                    AssertTrue(string.Equals(store.UiMode, "Simple", StringComparison.Ordinal), "Default UiMode == Simple");
                    AssertTrue(!store.IsProMode(), "Default IsProMode() == false");
                    store.Profiles.Add(new CompanyProfile { DisplayName = "Test Co" });
                    store.ActiveProfileId = store.Profiles[0].Id;

                    store.UiMode = "Pro";
                    AssertTrue(store.IsProMode(), "Switched UiMode == Pro -> IsProMode() == true");
                    string storeXmlPath = Path.Combine(dir, "profiles_test.xml");
                    var ser = new System.Xml.Serialization.XmlSerializer(typeof(CompanyProfileStore));
                    using (var fs = File.Create(storeXmlPath)) ser.Serialize(fs, store);

                    CompanyProfileStore loadedPro;
                    using (var fs = File.OpenRead(storeXmlPath)) loadedPro = ser.Deserialize(fs) as CompanyProfileStore;
                    AssertTrue(loadedPro != null && loadedPro.IsProMode(), "Deserialized Pro UiMode preserved");

                    loadedPro.UiMode = "Simple";
                    using (var fs = File.Create(storeXmlPath)) ser.Serialize(fs, loadedPro);
                    CompanyProfileStore loadedSimple;
                    using (var fs = File.OpenRead(storeXmlPath)) loadedSimple = ser.Deserialize(fs) as CompanyProfileStore;
                    AssertTrue(loadedSimple != null && string.Equals(loadedSimple.UiMode, "Simple", StringComparison.Ordinal) && !loadedSimple.IsProMode(), "Deserialized Simple UiMode preserved");

                    // Legacy XML without UiMode or empty UiMode normalizes to Simple
                    loadedSimple.UiMode = "";
                    CompanyProfilesManager.NormalizeStore(loadedSimple);
                    AssertTrue(string.Equals(loadedSimple.UiMode, "Simple", StringComparison.Ordinal) && !loadedSimple.IsProMode(), "Empty UiMode normalized to Simple");
                })) passed++;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
                catch { }
            }

            bool allOk = (passed == total);
            output.WriteLine(string.Format(CultureInfo.InvariantCulture, "SUMMARY: {0}/{1} passed ({2})", passed, total, allOk ? "ALL PASS" : "FAILURES"));
            return allOk;
        }

        private static bool RunSingleSelfTest(TextWriter output, string name, Action body)
        {
            try
            {
                body();
                output.WriteLine("[PASS] " + name);
                return true;
            }
            catch (Exception ex)
            {
                output.WriteLine("[FAIL] " + name + " -> " + ex.Message);
                return false;
            }
        }

        private static void AssertTrue(bool cond, string msg)
        {
            if (!cond) throw new InvalidOperationException("Assertion failed: " + msg);
        }

        private static void AssertEqInt(int actual, int expected, string msg)
        {
            if (actual != expected)
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0}: expected {1}, got {2}", msg, expected, actual));
        }

        private static void AssertEqDec(decimal actual, decimal expected, string msg)
        {
            if (Math.Abs(actual - expected) > 0.005m)
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0}: expected {1:0.00}, got {2:0.00}", msg, expected, actual));
        }

        private static void ValidateXlsxXmlEntries(string xlsxPath)
        {
            AssertTrue(File.Exists(xlsxPath), "XLSX exists: " + Path.GetFileName(xlsxPath));
            int xmlCount = 0;
            bool hasContentTypes = false;
            bool hasWorkbook = false;
            using (var fs = File.OpenRead(xlsxPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (string.Equals(entry.FullName, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                        hasContentTypes = true;
                    if (string.Equals(entry.FullName, "xl/workbook.xml", StringComparison.OrdinalIgnoreCase))
                        hasWorkbook = true;
                    if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var es = entry.Open())
                        {
                            XDocument.Load(es);
                            xmlCount++;
                        }
                    }
                }
            }
            AssertTrue(hasContentTypes && hasWorkbook && xmlCount >= 5, "XLSX structure & XML entries valid (" + xmlCount + " entries)");
        }

        private static void WriteSimpleXlsx(string path, string sheetName, string[] headers, object[][] rows)
        {
            var allRows = new List<object[]>();
            allRows.Add(headers);
            if (rows != null)
            {
                foreach (var r in rows) allRows.Add(r);
            }
            WriteCustomRowsXlsx(path, sheetName, allRows.ToArray());
        }

        private static void WriteCustomRowsXlsx(string path, string sheetName, object[][] rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for (int rIdx = 0; rIdx < rows.Length; rIdx++)
            {
                int rn = rIdx + 1;
                var r = rows[rIdx];
                sb.Append("<row r=\"" + rn + "\">");
                for (int c = 0; c < r.Length; c++)
                {
                    if (r[c] == null) continue;
                    string coord = ColName(c + 1) + rn;
                    if (r[c] is int) sb.Append(NC(coord, 5, ((int)r[c]).ToString(CultureInfo.InvariantCulture)));
                    else if (r[c] is decimal) sb.Append(MC(coord, 4, (decimal)r[c]));
                    else sb.Append(SC(coord, 3, r[c].ToString()));
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            WriteXlsxWorkbook(path, new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(sheetName, sb.ToString()) });
        }

        private static void WriteMinimalWbXlsx(string path, decimal realizedP, decimal toPayGoods, decimal logistics)
        {
            string[] h =
            {
                "Артикул поставщика", "Название", "Баркод", "Тип документа", "Обоснование для оплаты",
                "Дата продажи", "Кол-во", "Вайлдберриз реализовал Товар (Пр)",
                "К перечислению Продавцу за реализованный Товар", "Услуги по доставке товара покупателю"
            };
            object[][] rows = new object[][]
            {
                new object[] { "TEST-SKU", "Тестовый товар", "4810000000999", "Продажа", "Продажа", "2026-09-10", 10, realizedP, toPayGoods, logistics }
            };
            WriteSimpleXlsx(path, "Отчет WB", h, rows);
        }
    }
}
