using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace GovPortalsApp
{
    public static class CertRegistryManager
    {
        private static readonly object _syncLock = new object();

        private static string GetDataDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetRegistryFilePath()
        {
            return Path.Combine(GetDataDir(), "cert_registry.tsv");
        }

        private static string GetHiddenFilePath()
        {
            return Path.Combine(GetDataDir(), "hidden_certs.txt");
        }

        public static HashSet<string> LoadHiddenThumbprints()
        {
            lock (_syncLock)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var path = GetHiddenFilePath();
                    if (File.Exists(path))
                    {
                        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                        {
                            var t = (line ?? "").Trim();
                            if (!string.IsNullOrEmpty(t)) set.Add(t);
                        }
                    }
                }
                catch { }
                return set;
            }
        }

        private static void SaveHiddenThumbprints(HashSet<string> set)
        {
            try
            {
                var arr = new List<string>(set);
                File.WriteAllLines(GetHiddenFilePath(), arr.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        public static int GetHiddenCount()
        {
            return LoadHiddenThumbprints().Count;
        }

        public static void ClearHidden()
        {
            lock (_syncLock)
            {
                try
                {
                    var path = GetHiddenFilePath();
                    if (File.Exists(path)) File.Delete(path);
                }
                catch { }
            }
        }

        public static List<CertInfo> LoadRegistry()
        {
            lock (_syncLock)
            {
                var result = new List<CertInfo>();
                try
                {
                    var hidden = LoadHiddenThumbprints();
                    var path = GetRegistryFilePath();
                    if (!File.Exists(path)) return result;
                    var lines = File.ReadAllLines(path, Encoding.UTF8);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        var parts = line.Split('\t');
                        if (parts.Length < 6) continue;
                        if (hidden.Contains(parts[0])) continue;
                        long ticks;
                        if (!long.TryParse(parts[3], out ticks)) continue;
                        var validTo = new DateTime(ticks);
                        var days = (validTo - DateTime.Now).Days;
                        result.Add(new CertInfo
                        {
                            Thumbprint = parts[0],
                            OrgName = parts[1],
                            PersonName = parts[2],
                            ValidTo = validTo,
                            DaysRemaining = days,
                            IsValid = days > 0,
                            IsCompany = parts[4] == "1",
                            IsPhysicallyConnected = false,
                            LastSeen = parts[5]
                        });
                    }
                }
                catch { }
                return result;
            }
        }

        public static void SaveRegistry(List<CertInfo> items)
        {
            lock (_syncLock)
            {
                try
                {
                    var sb = new StringBuilder();
                    foreach (var c in items)
                    {
                        var thumb = (c.Thumbprint ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", "");
                        var org = (c.OrgName ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", "");
                        var person = (c.PersonName ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", "");
                        var lastSeen = (c.LastSeen ?? "").Replace("\t", " ");
                        sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\r\n",
                            thumb, org, person, c.ValidTo.Ticks, c.IsCompany ? "1" : "0", lastSeen);
                    }
                    File.WriteAllText(GetRegistryFilePath(), sb.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }

        public static List<CertInfo> MergeAndGetAll(List<CertInfo> freshCerts)
        {
            lock (_syncLock)
            {
                var hidden = LoadHiddenThumbprints();
                var map = new Dictionary<string, CertInfo>(StringComparer.OrdinalIgnoreCase);
                var saved = LoadRegistry();
                foreach (var s in saved)
                {
                    if (!string.IsNullOrEmpty(s.Thumbprint) && !hidden.Contains(s.Thumbprint))
                    {
                        s.IsPhysicallyConnected = false;
                        map[s.Thumbprint] = s;
                    }
                }

                if (freshCerts != null)
                {
                    foreach (var f in freshCerts)
                    {
                        var key = !string.IsNullOrEmpty(f.Thumbprint) ? f.Thumbprint : (f.OrgName + "_" + f.ValidTo.Ticks);
                        if (hidden.Contains(key)) continue;
                        f.Thumbprint = key;
                        map[key] = f;
                    }
                }

                var merged = new List<CertInfo>(map.Values);
                merged.Sort(delegate(CertInfo a, CertInfo b)
                {
                    if (a.IsPhysicallyConnected != b.IsPhysicallyConnected)
                    {
                        return a.IsPhysicallyConnected ? -1 : 1;
                    }
                    return a.DaysRemaining.CompareTo(b.DaysRemaining);
                });

                SaveRegistry(merged);
                return merged;
            }
        }

        public static void RemoveCertificate(string thumbprint, bool addToHidden)
        {
            if (string.IsNullOrEmpty(thumbprint)) return;
            RemoveCertificates(new string[] { thumbprint }, addToHidden);
        }

        public static void RemoveCertificates(IEnumerable<string> thumbprints, bool addToHidden)
        {
            if (thumbprints == null) return;
            lock (_syncLock)
            {
                var toRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in thumbprints)
                {
                    if (!string.IsNullOrEmpty(t)) toRemove.Add(t);
                }
                if (toRemove.Count == 0) return;

                if (addToHidden)
                {
                    var hidden = LoadHiddenThumbprints();
                    foreach (var t in toRemove) hidden.Add(t);
                    SaveHiddenThumbprints(hidden);
                }

                var current = LoadRegistry();
                var kept = new List<CertInfo>();
                foreach (var item in current)
                {
                    if (!toRemove.Contains(item.Thumbprint))
                    {
                        kept.Add(item);
                    }
                }
                SaveRegistry(kept);
            }
        }

        private static string NormalizeKey(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var s = text.ToLowerInvariant()
                .Replace("\"", "")
                .Replace("«", "")
                .Replace("»", "")
                .Replace("'", "");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        public static List<CertInfo> FindDuplicateOldCerts(List<CertInfo> items)
        {
            var oldDuplicates = new List<CertInfo>();
            if (items == null || items.Count < 2) return oldDuplicates;

            var byOwner = new Dictionary<string, List<CertInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in items)
            {
                string orgKey = NormalizeKey(c.OrgName);
                if (string.IsNullOrEmpty(orgKey)) continue;
                string personKey = NormalizeKey(c.PersonName);
                string key = orgKey + "|" + personKey;

                List<CertInfo> group;
                if (!byOwner.TryGetValue(key, out group))
                {
                    group = new List<CertInfo>();
                    byOwner[key] = group;
                }
                group.Add(c);
            }

            foreach (var kv in byOwner)
            {
                var group = kv.Value;
                if (group.Count < 2) continue;

                CertInfo newest = null;
                foreach (var c in group)
                {
                    if (newest == null || c.ValidTo > newest.ValidTo)
                    {
                        newest = c;
                    }
                }

                if (newest != null && newest.IsValid)
                {
                    foreach (var c in group)
                    {
                        if (!string.Equals(c.Thumbprint, newest.Thumbprint, StringComparison.OrdinalIgnoreCase) &&
                            c.ValidTo < newest.ValidTo)
                        {
                            oldDuplicates.Add(c);
                        }
                    }
                }
            }

            return oldDuplicates;
        }

        private static string CsvEscape(string s)
        {
            string val = s ?? "";
            if (val.Contains(";") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
            {
                val = "\"" + val.Replace("\"", "\"\"") + "\"";
            }
            return val;
        }

        public static void ExportRegistryToCsv(List<CertInfo> items, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Организация;Владелец / Директор;Статус;Действует до;Осталось дней;USB сейчас;Последняя проверка;Отпечаток");
            if (items != null)
            {
                foreach (var c in items)
                {
                    string status = !c.IsValid ? "Просрочен" : (c.DaysRemaining <= 30 ? "Истекает" : "Действует");
                    sb.AppendFormat("{0};{1};{2};{3};{4};{5};{6};{7}\r\n",
                        CsvEscape(c.OrgName),
                        CsvEscape(c.PersonName),
                        CsvEscape(status),
                        CsvEscape(c.ValidTo.ToString("dd.MM.yyyy")),
                        c.DaysRemaining,
                        c.IsPhysicallyConnected ? "Да" : "Нет",
                        CsvEscape(c.LastSeen),
                        CsvEscape(c.Thumbprint));
                }
            }
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        public static void ExportRegistryToXlsx(List<CertInfo> items, string filePath)
        {
            XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

            var rows = new List<string[]>();
            rows.Add(new string[] { "Организация", "Владелец / Директор", "Статус", "Действует до", "Осталось дней", "USB сейчас", "Последняя проверка", "Отпечаток" });
            if (items != null)
            {
                foreach (var c in items)
                {
                    string status = !c.IsValid ? "Просрочен" : (c.DaysRemaining <= 30 ? "Истекает" : "Действует");
                    rows.Add(new string[]
                    {
                        c.OrgName ?? "",
                        c.PersonName ?? "",
                        status,
                        c.ValidTo.ToString("dd.MM.yyyy"),
                        c.DaysRemaining.ToString(),
                        c.IsPhysicallyConnected ? "Да" : "Нет",
                        c.LastSeen ?? "",
                        c.Thumbprint ?? ""
                    });
                }
            }

            var sheetData = new XElement(mainNs + "sheetData");
            for (int r = 0; r < rows.Count; r++)
            {
                var rowEl = new XElement(mainNs + "row", new XAttribute("r", r + 1));
                var cols = rows[r];
                for (int c = 0; c < cols.Length; c++)
                {
                    string colLetter = ((char)('A' + c)).ToString();
                    string cellRef = colLetter + (r + 1).ToString();
                    if (r > 0 && c == 4)
                    {
                        int daysVal;
                        if (int.TryParse(cols[c], NumberStyles.Integer, CultureInfo.InvariantCulture, out daysVal))
                        {
                            rowEl.Add(new XElement(mainNs + "c",
                                new XAttribute("r", cellRef),
                                new XElement(mainNs + "v", daysVal.ToString(CultureInfo.InvariantCulture))));
                            continue;
                        }
                    }
                    rowEl.Add(new XElement(mainNs + "c",
                        new XAttribute("r", cellRef),
                        new XAttribute("t", "inlineStr"),
                        new XElement(mainNs + "is", new XElement(mainNs + "t", cols[c] ?? ""))));
                }
                sheetData.Add(rowEl);
            }

            var sheetDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(mainNs + "worksheet",
                    new XElement(mainNs + "cols",
                        new XElement(mainNs + "col", new XAttribute("min", 1), new XAttribute("max", 1), new XAttribute("width", 42), new XAttribute("customWidth", 1)),
                        new XElement(mainNs + "col", new XAttribute("min", 2), new XAttribute("max", 2), new XAttribute("width", 26), new XAttribute("customWidth", 1)),
                        new XElement(mainNs + "col", new XAttribute("min", 3), new XAttribute("max", 7), new XAttribute("width", 16), new XAttribute("customWidth", 1)),
                        new XElement(mainNs + "col", new XAttribute("min", 8), new XAttribute("max", 8), new XAttribute("width", 42), new XAttribute("customWidth", 1))
                    ),
                    sheetData));

            var wbDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(mainNs + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", rNs.NamespaceName),
                    new XElement(mainNs + "sheets",
                        new XElement(mainNs + "sheet",
                            new XAttribute("name", "Реестр ЭЦП"),
                            new XAttribute("sheetId", 1),
                            new XAttribute(rNs + "id", "rId1")))));

            XNamespace ctNs = "http://schemas.openxmlformats.org/package/2006/content-types";
            var ctDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(ctNs + "Types",
                    new XElement(ctNs + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(ctNs + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(ctNs + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(ctNs + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));

            XNamespace pkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
            var rootRelsDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(pkgRelNs + "Relationships",
                    new XElement(pkgRelNs + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml"))));

            var wbRelsDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(pkgRelNs + "Relationships",
                    new XElement(pkgRelNs + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml"))));

            if (File.Exists(filePath)) File.Delete(filePath);
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteXmlToZip(zip, "[Content_Types].xml", ctDoc);
                WriteXmlToZip(zip, "_rels/.rels", rootRelsDoc);
                WriteXmlToZip(zip, "xl/workbook.xml", wbDoc);
                WriteXmlToZip(zip, "xl/_rels/workbook.xml.rels", wbRelsDoc);
                WriteXmlToZip(zip, "xl/worksheets/sheet1.xml", sheetDoc);
            }
        }

        private static void WriteXmlToZip(ZipArchive zip, string entryName, XDocument doc)
        {
            var entry = zip.CreateEntry(entryName);
            using (var s = entry.Open())
            using (var sw = new StreamWriter(s, new UTF8Encoding(false)))
            {
                doc.Save(sw);
            }
        }
    }

    public class UnpResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Unp { get; set; }
        public string FullName { get; set; }
        public string ShortName { get; set; }
        public string Address { get; set; }
        public string RegDate { get; set; }
        public string TaxOffice { get; set; }
        public string StatusCode { get; set; }
        public string StatusName { get; set; }
        public string LiqDate { get; set; }
    }

    public static class UnpChecker
    {
        private static string ExtractJsonField(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            if (!m.Success) return "";
            var val = m.Groups[1].Value;
            val = val.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\/", "/");
            return val.Trim();
        }

        public static UnpResult Lookup(string rawUnp)
        {
            var res = new UnpResult { Success = false };
            string unp = Regex.Replace(rawUnp ?? "", "\\D", "");
            if (unp.Length != 9)
            {
                res.ErrorMessage = "Введите ровно 9 цифр УНП (например: 191771556)";
                return res;
            }

            try
            {
                Program.ConfigureSafeTls();
                var url = "https://www.portal.nalog.gov.by/grp/getData?unp=" + unp + "&charset=UTF-8&type=json";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 4500;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

                string body = "";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    body = sr.ReadToEnd();
                }

                var foundUnp = ExtractJsonField(body, "vunp");
                if (string.IsNullOrEmpty(foundUnp))
                {
                    res.ErrorMessage = "УНП " + unp + " не найден в базе МНС ГРП.";
                    return res;
                }

                res.Success = true;
                res.Unp = foundUnp;
                res.FullName = ExtractJsonField(body, "vnaimp");
                res.ShortName = ExtractJsonField(body, "vnaimk");
                res.Address = ExtractJsonField(body, "vpadres");
                res.RegDate = ExtractJsonField(body, "dreg");
                res.TaxOffice = ExtractJsonField(body, "vmns");
                res.StatusCode = ExtractJsonField(body, "ckodsost");
                res.StatusName = ExtractJsonField(body, "vkods");
                res.LiqDate = ExtractJsonField(body, "dlikv");
                return res;
            }
            catch (WebException wex)
            {
                var r = wex.Response as HttpWebResponse;
                if (r != null && (int)r.StatusCode == 400)
                {
                    res.ErrorMessage = "УНП " + unp + " не найден в государственном реестре плательщиков (ГРП МНС).";
                }
                else
                {
                    res.ErrorMessage = "Сервер ГРП МНС временно недоступен: " + wex.Message;
                }
                return res;
            }
            catch (Exception ex)
            {
                res.ErrorMessage = "Ошибка запроса: " + ex.Message;
                return res;
            }
        }
    }

    public class NbrbRatesSnapshot
    {
        public string Usd { get; set; }
        public string Rub100 { get; set; }
        public string Eur { get; set; }
    }

    public static class NbrbRatesHelper
    {
        private static string ExtractRate(string json)
        {
            var m = Regex.Match(json ?? "", "\"Cur_OfficialRate\"\\s*:\\s*([0-9.]+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static void FetchAllRatesAsync(Action<NbrbRatesSnapshot> onLoaded)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Program.ConfigureSafeTls();
                    var snap = new NbrbRatesSnapshot();
                    using (var wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        snap.Usd = ExtractRate(wc.DownloadString("https://www.nbrb.by/api/exrates/rates/431"));
                        snap.Rub100 = ExtractRate(wc.DownloadString("https://www.nbrb.by/api/exrates/rates/456"));
                        snap.Eur = ExtractRate(wc.DownloadString("https://www.nbrb.by/api/exrates/rates/451"));
                    }
                    if (!string.IsNullOrEmpty(snap.Usd) || !string.IsNullOrEmpty(snap.Rub100) || !string.IsNullOrEmpty(snap.Eur))
                    {
                        if (onLoaded != null) onLoaded(snap);
                    }
                }
                catch { }
            });
        }

        public static void FetchRateOnDateAsync(int curId, DateTime date, Action<decimal?, string> onLoaded)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Program.ConfigureSafeTls();
                    string url = string.Format(
                        CultureInfo.InvariantCulture,
                        "https://www.nbrb.by/api/exrates/rates/{0}?ondate={1:yyyy-MM-dd}",
                        curId,
                        date);
                    using (var wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        string json = wc.DownloadString(url);
                        string raw = ExtractRate(json);
                        decimal val;
                        if (!string.IsNullOrEmpty(raw) &&
                            decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out val) &&
                            val > 0m)
                        {
                            if (onLoaded != null) onLoaded(val, null);
                            return;
                        }
                    }
                    if (onLoaded != null) onLoaded(null, "Курс на выбранную дату не найден в ответе НБРБ.");
                }
                catch (Exception ex)
                {
                    if (onLoaded != null) onLoaded(null, "Ошибка загрузки курса НБРБ: " + ex.Message);
                }
            });
        }

        public static void FetchRatesAsync(Action<string> onLoaded)
        {
            FetchAllRatesAsync(delegate(NbrbRatesSnapshot snap)
            {
                if (snap != null && !string.IsNullOrEmpty(snap.Usd) && !string.IsNullOrEmpty(snap.Rub100))
                {
                    string text = "НБРБ: USD " + snap.Usd + " • 100 RUB " + snap.Rub100;
                    if (!string.IsNullOrEmpty(snap.Eur)) text += " • EUR " + snap.Eur;
                    if (onLoaded != null) onLoaded(text);
                }
            });
        }
    }

    public static class UpdateChecker
    {
        private const string WorkerVersionApiUrl = "https://buhly-stats.repinm911.workers.dev/api/version";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/narard/buhly-rb/releases/latest";

        public static string GetCacheFilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "update_check.txt");
        }

        public static string ExtractJsonField(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            if (!m.Success) return "";
            var val = m.Groups[1].Value;
            return val.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\/", "/").Trim();
        }

        public static Version ParseNormalizedVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(1).Trim();
            int dashIdx = s.IndexOfAny(new char[] { '-', '+' });
            if (dashIdx >= 0) s = s.Substring(0, dashIdx);

            var parts = s.Split('.');
            if (parts.Length < 2 || parts.Length > 4) return null;

            int[] nums = new int[4];
            for (int i = 0; i < parts.Length; i++)
            {
                int val;
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out val) || val < 0)
                    return null;
                nums[i] = val;
            }
            return new Version(nums[0], nums[1], nums[2], nums[3]);
        }

        public static bool IsRemoteNewer(string remoteTag, Version currentVersion)
        {
            if (currentVersion == null) return false;
            var remoteVer = ParseNormalizedVersion(remoteTag);
            if (remoteVer == null) return false;

            var normCurrent = new Version(
                Math.Max(0, currentVersion.Major),
                Math.Max(0, currentVersion.Minor),
                Math.Max(0, currentVersion.Build),
                Math.Max(0, currentVersion.Revision));

            return remoteVer > normCurrent;
        }

        public static bool IsValidReleaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.StartsWith("https://github.com/narard/buhly-rb/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSameMinskCalendarDay(DateTime lastUtc, DateTime nowUtc)
        {
            return lastUtc.AddHours(3).Date == nowUtc.AddHours(3).Date;
        }

        private static string HttpGetJson(string url, int timeoutMs)
        {
            Program.ConfigureSafeTls();
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = "Buhly-Desktop";
            req.Accept = "application/vnd.github+json";

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static void SaveCache(string path, string tagName, string htmlUrl)
        {
            try
            {
                var lines = new string[]
                {
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    tagName ?? "",
                    htmlUrl ?? ""
                };
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
            catch { }
        }

        public static void CheckForUpdatesAsync(Action<string, string> onUpdateAvailable)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Version currentVer = Assembly.GetExecutingAssembly().GetName().Version;
                    string cachePath = GetCacheFilePath();

                    if (File.Exists(cachePath))
                    {
                        try
                        {
                            string[] lines = File.ReadAllLines(cachePath, Encoding.UTF8);
                            if (lines.Length >= 1)
                            {
                                DateTime lastCheck;
                                if (DateTime.TryParseExact(
                                    lines[0].Trim(),
                                    "yyyy-MM-ddTHH:mm:ssZ",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                    out lastCheck))
                                {
                                    if (IsSameMinskCalendarDay(lastCheck, DateTime.UtcNow))
                                    {
                                        string cachedTag = lines.Length >= 2 ? lines[1].Trim() : "";
                                        string cachedUrl = lines.Length >= 3 ? lines[2].Trim() : "";
                                        if (IsRemoteNewer(cachedTag, currentVer) && IsValidReleaseUrl(cachedUrl))
                                        {
                                            if (onUpdateAvailable != null) onUpdateAvailable(cachedTag, cachedUrl);
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    string tagName = "";
                    string htmlUrl = "";

                    try
                    {
                        string workerJson = HttpGetJson(WorkerVersionApiUrl, 4000);
                        tagName = ExtractJsonField(workerJson, "tag_name");
                        htmlUrl = ExtractJsonField(workerJson, "html_url");
                    }
                    catch { }

                    if (string.IsNullOrEmpty(tagName) || !IsValidReleaseUrl(htmlUrl))
                    {
                        string ghJson = HttpGetJson(LatestReleaseApiUrl, 5000);
                        tagName = ExtractJsonField(ghJson, "tag_name");
                        htmlUrl = ExtractJsonField(ghJson, "html_url");
                    }

                    SaveCache(cachePath, tagName, htmlUrl);

                    if (IsRemoteNewer(tagName, currentVer) && IsValidReleaseUrl(htmlUrl))
                    {
                        if (onUpdateAvailable != null) onUpdateAvailable(tagName, htmlUrl);
                    }
                }
                catch
                {
                    // Offline or GitHub API unreachable: cache timestamp silently so we don't retry excessively
                    try
                    {
                        string cachePath = GetCacheFilePath();
                        if (!File.Exists(cachePath))
                            SaveCache(cachePath, "", "");
                    }
                    catch { }
                }
            });
        }
    }
}
