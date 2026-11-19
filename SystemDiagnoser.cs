using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace GovPortalsApp
{
    public class WinINetHelper
    {
        [DllImport("wininet.dll", SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        public const int INTERNET_OPTION_REFRESH = 37;

        private static string GetBackupDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static void EnablePac(string pacUrl)
        {
            try
            {
                var dir = GetBackupDir();
                var pacBak = Path.Combine(dir, "proxy_backup.txt");
                var flagsBak = Path.Combine(dir, "proxy_flags_backup.txt");

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        var existingPac = key.GetValue("AutoConfigURL") as string;
                        if (!File.Exists(pacBak))
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(existingPac))
                                    File.WriteAllText(pacBak, "__NONE__", Encoding.UTF8);
                                else if (!existingPac.Contains("127.0.0.1:10224"))
                                    File.WriteAllText(pacBak, existingPac, Encoding.UTF8);
                            }
                            catch { }
                        }
                        key.SetValue("AutoConfigURL", pacUrl, RegistryValueKind.String);
                    }
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", true))
                {
                    if (key != null)
                    {
                        var names = new string[] { "DefaultConnectionSettings", "SavedLegacySettings" };
                        if (!File.Exists(flagsBak))
                        {
                            try
                            {
                                var curVal = key.GetValue("DefaultConnectionSettings") as byte[];
                                bool wasPacOn = (curVal != null && curVal.Length > 8 && (curVal[8] & 8) != 0);
                                File.WriteAllText(flagsBak, wasPacOn ? "1" : "0", Encoding.UTF8);
                            }
                            catch { }
                        }

                        foreach (var name in names)
                        {
                            var val = key.GetValue(name) as byte[];
                            if (val != null && val.Length > 8)
                            {
                                val[8] = (byte)(val[8] | 8);
                                key.SetValue(name, val, RegistryValueKind.Binary);
                            }
                        }
                    }
                }

                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }
            catch { }
        }

        public static bool RestorePacFromBackup(out string details)
        {
            try
            {
                var dir = GetBackupDir();
                var pacBak = Path.Combine(dir, "proxy_backup.txt");
                var flagsBak = Path.Combine(dir, "proxy_flags_backup.txt");

                bool hasPacBackup = File.Exists(pacBak);
                string savedPac = hasPacBackup ? File.ReadAllText(pacBak, Encoding.UTF8).Trim() : null;
                bool hadFlagsBackup = File.Exists(flagsBak);
                bool restorePacFlagOn = hadFlagsBackup && File.ReadAllText(flagsBak, Encoding.UTF8).Trim() == "1";

                bool restoredSomething = false;
                bool removedBuhlyPac = false;
                bool hasThirdPartyProxy = false;

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        var currentPac = key.GetValue("AutoConfigURL") as string;
                        if (hasPacBackup)
                        {
                            if (savedPac == "__NONE__" || string.IsNullOrEmpty(savedPac))
                            {
                                if (currentPac != null)
                                {
                                    key.DeleteValue("AutoConfigURL", false);
                                    if (currentPac.Contains("127.0.0.1:10224")) removedBuhlyPac = true;
                                }
                                restoredSomething = true;
                            }
                            else
                            {
                                key.SetValue("AutoConfigURL", savedPac, RegistryValueKind.String);
                                restoredSomething = true;
                            }
                        }
                        else if (!string.IsNullOrEmpty(currentPac) && currentPac.Contains("127.0.0.1:10224"))
                        {
                            key.DeleteValue("AutoConfigURL", false);
                            removedBuhlyPac = true;
                            restoredSomething = true;
                        }

                        var finalPac = key.GetValue("AutoConfigURL") as string;
                        var proxyEnableObj = key.GetValue("ProxyEnable");
                        bool manualProxyOn = (proxyEnableObj is int && (int)proxyEnableObj == 1);
                        bool thirdPartyPac = !string.IsNullOrEmpty(finalPac) && !finalPac.Contains("127.0.0.1:10224");
                        hasThirdPartyProxy = thirdPartyPac || manualProxyOn;
                    }
                }

                // Only touch Connections PAC bit (8) if we actually have a flags backup OR we just removed Buhly's PAC
                // and no third-party proxy/PAC is configured.
                if (hadFlagsBackup || (removedBuhlyPac && !hasThirdPartyProxy))
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", true))
                    {
                        if (key != null)
                        {
                            var names = new string[] { "DefaultConnectionSettings", "SavedLegacySettings" };
                            foreach (var name in names)
                            {
                                var val = key.GetValue(name) as byte[];
                                if (val != null && val.Length > 8)
                                {
                                    if (hadFlagsBackup)
                                    {
                                        if (restorePacFlagOn)
                                            val[8] = (byte)(val[8] | 8);
                                        else if (!hasThirdPartyProxy)
                                            val[8] = (byte)(val[8] & ~8);
                                    }
                                    else if (removedBuhlyPac && !hasThirdPartyProxy)
                                    {
                                        val[8] = (byte)(val[8] & ~8);
                                    }
                                    key.SetValue(name, val, RegistryValueKind.Binary);
                                    restoredSomething = true;
                                }
                            }
                        }
                    }
                }

                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

                if (hasPacBackup)
                {
                    details = (savedPac == "__NONE__" || string.IsNullOrEmpty(savedPac))
                        ? "Прокси (PAC) отключён (восстановлено исходное состояние из бэкапа)"
                        : "Прокси (PAC) восстановлен: " + savedPac;
                }
                else
                {
                    details = restoredSomething
                        ? "PAC-туннель Buhly (127.0.0.1:10224) отключён"
                        : "Бэкап PAC не требовался (настройки не менялись или используется сторонний прокси)";
                }
                return true;
            }
            catch (Exception ex)
            {
                details = "Ошибка отката PAC: " + ex.Message;
                return false;
            }
        }
    }

    public class CertInfo
    {
        public string Thumbprint { get; set; }
        public string OrgName { get; set; }
        public string PersonName { get; set; }
        public DateTime ValidTo { get; set; }
        public int DaysRemaining { get; set; }
        public bool IsValid { get; set; }
        public bool IsCompany { get; set; }
        public bool IsPhysicallyConnected { get; set; }
        public string LastSeen { get; set; }
    }

    public class CryptoHelper
    {
        [DllImport("crypt32.dll", SetLastError = true)]
        public static extern bool CryptAcquireCertificatePrivateKey(
            IntPtr pCert,
            uint dwFlags,
            IntPtr pvParameters,
            out IntPtr phCryptProvOrNCryptKey,
            out uint pdwKeySpec,
            out bool pfCallerFreeProvOrNCryptKey
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool CryptReleaseContext(IntPtr hProv, uint dwFlags);

        public const uint CRYPT_ACQUIRE_SILENT_FLAG = 0x00000040;

        public static bool IsTokenPhysicallyInserted(X509Certificate2 cert)
        {
            try
            {
                IntPtr hProv;
                uint spec;
                bool free;
                bool ok = CryptAcquireCertificatePrivateKey(
                    cert.Handle,
                    CRYPT_ACQUIRE_SILENT_FLAG,
                    IntPtr.Zero,
                    out hProv,
                    out spec,
                    out free
                );
                if (ok && free && hProv != IntPtr.Zero)
                {
                    try { CryptReleaseContext(hProv, 0); } catch { }
                }
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private static string UnquoteDnValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                s = s.Substring(1, s.Length - 2);
            }
            return s.Replace("\"\"", "\"").Trim();
        }

        public static string ExtractDnField(string dn, string key)
        {
            if (string.IsNullOrEmpty(dn) || string.IsNullOrEmpty(key)) return "";
            var m = Regex.Match(dn, @"(?:^|,\s*)" + Regex.Escape(key) + @"=(""(?:[^""]|"""")*""|[^,]+)", RegexOptions.IgnoreCase);
            if (!m.Success) return "";
            return UnquoteDnValue(m.Groups[1].Value);
        }

        public static string ExtractFirstDnField(string dn, string[] keys)
        {
            if (keys == null) return "";
            foreach (var k in keys)
            {
                string val = ExtractDnField(dn, k);
                if (!string.IsNullOrEmpty(val)) return val;
            }
            return "";
        }

        public static string DecodeAsn1String(byte[] raw)
        {
            if (raw == null || raw.Length < 2) return "";
            try
            {
                int idx = 0;
                byte tag = raw[idx++];
                int len = raw[idx++];
                if ((len & 0x80) != 0)
                {
                    int numBytes = len & 0x7F;
                    if (numBytes == 0 || numBytes > 4 || idx + numBytes > raw.Length) return "";
                    len = 0;
                    for (int i = 0; i < numBytes; i++)
                    {
                        len = (len << 8) | raw[idx++];
                    }
                }
                if (len <= 0 || idx + len > raw.Length) return "";

                // Unwrap OCTET STRING if nested
                if (tag == 0x04 && len >= 2)
                {
                    var inner = new byte[len];
                    Buffer.BlockCopy(raw, idx, inner, 0, len);
                    string nested = DecodeAsn1String(inner);
                    if (!string.IsNullOrEmpty(nested)) return nested;
                }

                if (tag == 0x1E) // BMPString (UTF-16 Big Endian)
                    return Encoding.BigEndianUnicode.GetString(raw, idx, len).Trim('\0').Trim();
                if (tag == 0x0C) // UTF8String
                    return Encoding.UTF8.GetString(raw, idx, len).Trim('\0').Trim();
                if (tag == 0x13 || tag == 0x16 || tag == 0x14 || tag == 0x1A) // Printable / IA5 / T61 / Visible
                    return Encoding.UTF8.GetString(raw, idx, len).Trim('\0').Trim();
            }
            catch { }
            return "";
        }

        public static string GetExtensionString(X509Certificate2 cert, string oid)
        {
            if (cert == null || string.IsNullOrEmpty(oid)) return "";
            try
            {
                foreach (var ext in cert.Extensions)
                {
                    if (ext.Oid != null && string.Equals(ext.Oid.Value, oid, StringComparison.OrdinalIgnoreCase))
                    {
                        string decoded = DecodeAsn1String(ext.RawData);
                        if (!string.IsNullOrEmpty(decoded)) return decoded;
                    }
                }
            }
            catch { }
            return "";
        }

        public static void ExtractGovCertDetails(X509Certificate2 cert, out string unp, out string orgName, out string personName, out string title)
        {
            unp = "";
            orgName = "";
            personName = "";
            title = "";
            if (cert == null) return;

            var subj = cert.SubjectName != null ? (cert.SubjectName.Name ?? "") : "";

            // 1. UNP: BelPKI extension OID 1.2.112.1.2.1.1.1.1.2 (primary in Belarus), fallback to SubjectName
            string extUnp = GetExtensionString(cert, "1.2.112.1.2.1.1.1.1.2");
            if (!string.IsNullOrEmpty(extUnp))
            {
                var mExt = Regex.Match(extUnp, @"\b(\d{9})\b");
                if (mExt.Success) unp = mExt.Groups[1].Value;
            }
            if (string.IsNullOrEmpty(unp))
            {
                var mUnp = Regex.Match(subj, @"(?:1\.2\.112\.1\.2\.1\.1\.1\.1\.2\s*=\s*|УНП\s*[:=]?\s*)(\d{9})", RegexOptions.IgnoreCase);
                if (mUnp.Success) unp = mUnp.Groups[1].Value;
                else
                {
                    var mAny9 = Regex.Match(subj, @"\b([1-9]\d{8})\b");
                    if (mAny9.Success) unp = mAny9.Groups[1].Value;
                }
            }

            // 2. Organization name
            orgName = ExtractFirstDnField(subj, new string[] { "O", "CN" });
            if (string.IsNullOrEmpty(orgName))
            {
                orgName = cert.FriendlyName;
                if (string.IsNullOrEmpty(orgName)) orgName = "Сертификат ГосСУОК";
            }

            // 3. Person name (SN + GivenName/OID.2.5.4.41)
            string sn = ExtractDnField(subj, "SN");
            string gn = ExtractFirstDnField(subj, new string[] { "OID.2.5.4.41", "41", "2.5.4.41", "GIVENNAME", "G", "OID.2.5.4.42", "2.5.4.42" });
            personName = (sn + " " + gn).Trim();

            // 4. Title / position (BelPKI extension OID 1.2.112.1.2.1.1.5.1, fallback to Subject T)
            title = GetExtensionString(cert, "1.2.112.1.2.1.1.5.1");
            if (string.IsNullOrEmpty(title))
            {
                title = ExtractFirstDnField(subj, new string[] { "T", "TITLE", "OID.2.5.4.12" });
            }
        }
    }

    public class EdgeHelper
    {
        private static readonly string[] TrackedZone2Flags = new string[] { "1001", "1004", "1200", "1405", "2201" };

        private static string GetBuhlyDataDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetSiteListXmlPath()
        {
            return Path.Combine(GetBuhlyDataDir(), "ie_site_list.xml");
        }

        private static string GetEdgePreferencesPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Preferences");
        }

        public static bool IsEdgeRunning()
        {
            var procs = Process.GetProcessesByName("msedge");
            try
            {
                return procs.Length > 0;
            }
            finally
            {
                foreach (var p in procs)
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }

        public static bool HasVatInEdgePreferences()
        {
            try
            {
                var prefPath = GetEdgePreferencesPath();
                if (!File.Exists(prefPath)) return false;
                var text = File.ReadAllText(prefPath, Encoding.UTF8);
                return text.Contains("vat.gov.by/mainPage") && text.Contains("vat.gov.by/eInvoice");
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureTrustedSitesAndActiveX()
        {
            try
            {
                var domains = new string[]
                {
                    "vat.gov.by",
                    "ssf.gov.by",
                    "portal2.ssf.gov.by",
                    "lk-portal2.ssf.gov.by",
                    "oauth.uas.nces.by",
                    "nalog.gov.by",
                    "e-pasluga.by",
                    "nces.by"
                };
                foreach (var domain in domains)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\ZoneMap\Domains\" + domain))
                    {
                        if (key != null)
                        {
                            key.SetValue("https", 2, RegistryValueKind.DWord);
                        }
                    }
                }

                using (var zone2 = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\2"))
                {
                    if (zone2 != null)
                    {
                        // Save original Zones\2 flags once before modifying
                        var zonesBak = Path.Combine(GetBuhlyDataDir(), "zones2_backup.txt");
                        if (!File.Exists(zonesBak))
                        {
                            try
                            {
                                var bakLines = new List<string>();
                                foreach (var flag in TrackedZone2Flags)
                                {
                                    object rawVal = zone2.GetValue(flag);
                                    bakLines.Add(flag + "=" + (rawVal != null ? rawVal.ToString() : "__MISSING__"));
                                }
                                File.WriteAllLines(zonesBak, bakLines.ToArray(), Encoding.UTF8);
                            }
                            catch { }
                        }

                        // Allow signed ActiveX controls needed by BelPKI/AvCSP without lowering unsafe/unsigned protections
                        var signedFlags = new string[] { "1001", "1200", "1405", "2201" };
                        foreach (var flag in signedFlags)
                        {
                            zone2.SetValue(flag, 0, RegistryValueKind.DWord);
                        }
                        // Prompt for unsigned ActiveX rather than silent allow
                        zone2.SetValue("1004", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        public static bool RestoreZones2FromBackup(out string details)
        {
            try
            {
                var zonesBak = Path.Combine(GetBuhlyDataDir(), "zones2_backup.txt");
                if (!File.Exists(zonesBak))
                {
                    details = "Бэкап зоны IE (Zones\\2) не найден (настройки не менялись)";
                    return true;
                }

                using (var zone2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\2", true))
                {
                    if (zone2 != null)
                    {
                        var lines = File.ReadAllLines(zonesBak, Encoding.UTF8);
                        foreach (var raw in lines)
                        {
                            if (string.IsNullOrEmpty(raw)) continue;
                            int eq = raw.IndexOf('=');
                            if (eq <= 0) continue;
                            string flag = raw.Substring(0, eq).Trim();
                            string val = raw.Substring(eq + 1).Trim();
                            if (Array.IndexOf(TrackedZone2Flags, flag) < 0) continue;

                            if (val == "__MISSING__")
                            {
                                zone2.DeleteValue(flag, false);
                            }
                            else
                            {
                                int num;
                                if (int.TryParse(val, out num))
                                {
                                    zone2.SetValue(flag, num, RegistryValueKind.DWord);
                                }
                            }
                        }
                    }
                }

                details = "Параметры зоны надёжных сайтов IE (Zones\\2) восстановлены из бэкапа";
                return true;
            }
            catch (Exception ex)
            {
                details = "Ошибка отката Zones\\2: " + ex.Message;
                return false;
            }
        }

        public static bool RestoreEdgeFromBackup(out string details)
        {
            var notes = new List<string>();
            bool allOk = true;

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Edge", true))
                {
                    if (key != null)
                    {
                        bool removedPolicy = false;
                        if (key.GetValue("InternetExplorerIntegrationSiteList") != null)
                        {
                            key.DeleteValue("InternetExplorerIntegrationSiteList", false);
                            removedPolicy = true;
                        }
                        if (key.GetValue("InternetExplorerIntegrationLevel") != null)
                        {
                            key.DeleteValue("InternetExplorerIntegrationLevel", false);
                            removedPolicy = true;
                        }
                        if (key.GetValue("InternetExplorerIntegrationReloadInIEModeAllowed") != null)
                        {
                            key.DeleteValue("InternetExplorerIntegrationReloadInIEModeAllowed", false);
                            removedPolicy = true;
                        }
                        notes.Add(removedPolicy
                            ? "Политики режима IE для Edge удалены из реестра"
                            : "Политики режима IE для Edge не были заданы");
                    }
                    else
                    {
                        notes.Add("Политики режима IE для Edge отсутствуют");
                    }
                }
            }
            catch (Exception ex)
            {
                notes.Add("Ошибка очистки политик Edge: " + ex.Message);
                allOk = false;
            }

            try
            {
                var prefPath = GetEdgePreferencesPath();
                var bakPath = prefPath + ".buhly.bak";
                if (File.Exists(bakPath))
                {
                    if (IsEdgeRunning())
                    {
                        notes.Add("⚠️ Microsoft Edge запущен (в т.ч. возможно в фоне/трее) — закройте Edge полностью и повторите откат для восстановления Preferences");
                        allOk = false;
                    }
                    else
                    {
                        File.Copy(bakPath, prefPath, true);
                        notes.Add("Профиль Edge (Preferences) восстановлен из бэкапа");
                    }
                }
                else
                {
                    notes.Add("Бэкап Edge Preferences не найден (файл профиля не изменялся)");
                }
            }
            catch (Exception ex)
            {
                notes.Add("Ошибка восстановления Edge Preferences: " + ex.Message);
                allOk = false;
            }

            details = string.Join("\n", notes.ToArray());
            return allOk;
        }

        public static bool IsVatConfigured()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Edge", false))
                {
                    if (key != null)
                    {
                        var level = key.GetValue("InternetExplorerIntegrationLevel");
                        var siteList = key.GetValue("InternetExplorerIntegrationSiteList") as string;
                        if (level is int && (int)level == 1 && !string.IsNullOrEmpty(siteList))
                        {
                            string localSiteList = siteList;
                            Uri parsedUri;
                            if (Uri.TryCreate(siteList, UriKind.Absolute, out parsedUri) && parsedUri.IsFile)
                            {
                                localSiteList = parsedUri.LocalPath;
                            }
                            if (File.Exists(localSiteList)) return true;
                        }
                    }
                }

                return HasVatInEdgePreferences();
            }
            catch { return false; }
        }

        public static void EnsureVatInEdgeIEMode()
        {
            try
            {
                EnsureTrustedSitesAndActiveX();

                var xmlPath = GetSiteListXmlPath();
                var xmlContent =
                    "<site-list version=\"1\">\r\n" +
                    "  <created-by>\r\n" +
                    "    <tool>Buhly</tool>\r\n" +
                    "    <version>3.1</version>\r\n" +
                    "    <date-created>20261119</date-created>\r\n" +
                    "  </created-by>\r\n" +
                    "  <site url=\"vat.gov.by\">\r\n" +
                    "    <compat-mode>IE11</compat-mode>\r\n" +
                    "    <open-in allow-redirect=\"true\">IE11</open-in>\r\n" +
                    "  </site>\r\n" +
                    "</site-list>\r\n";
                File.WriteAllText(xmlPath, xmlContent, Encoding.UTF8);

                string fileUri = new Uri(xmlPath).AbsoluteUri;
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Edge"))
                {
                    if (key != null)
                    {
                        key.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                        key.SetValue("InternetExplorerIntegrationSiteList", fileUri, RegistryValueKind.String);
                        key.SetValue("InternetExplorerIntegrationReloadInIEModeAllowed", 1, RegistryValueKind.DWord);
                    }
                }

                // Secondary fallback ONLY when Edge is not running (never kill user's open browser!)
                if (!IsEdgeRunning())
                {
                    var prefPath = GetEdgePreferencesPath();
                    if (File.Exists(prefPath))
                    {
                        var text = File.ReadAllText(prefPath, Encoding.UTF8);
                        bool alreadyConfigured = text.Contains("vat.gov.by/mainPage") && text.Contains("vat.gov.by/eInvoice");
                        if (!alreadyConfigured)
                        {
                            string bakPath = prefPath + ".buhly.bak";
                            if (!File.Exists(bakPath))
                            {
                                try { File.Copy(prefPath, bakPath, false); } catch { }
                            }

                            string updated = text;
                            if (updated.Contains("\"consumer_mode\":{\"enabled_state\":0}"))
                            {
                                updated = updated.Replace("\"consumer_mode\":{\"enabled_state\":0}", "\"consumer_mode\":{\"enabled_state\":1}");
                            }
                            else if (!updated.Contains("\"consumer_mode\"") && updated.Contains("\"dual_engine\":{"))
                            {
                                updated = updated.Replace("\"dual_engine\":{", "\"dual_engine\":{\"consumer_mode\":{\"enabled_state\":1},");
                            }

                            var entries =
                                "\"http://vat.gov.by/mainPage/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"http://vat.gov.by/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/mainPage/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/eInvoice\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/eInvoice/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}";

                            if (updated.Contains("\"user_list_data_1\":{}"))
                            {
                                updated = updated.Replace("\"user_list_data_1\":{}", "\"user_list_data_1\":{" + entries + "}");
                            }
                            else if (updated.Contains("\"user_list_data_1\":{"))
                            {
                                updated = updated.Replace("\"user_list_data_1\":{", "\"user_list_data_1\":{" + entries + ",");
                            }
                            else if (updated.Contains("\"dual_engine\":{}"))
                            {
                                updated = updated.Replace("\"dual_engine\":{}", "\"dual_engine\":{\"user_list_data_1\":{" + entries + "}}");
                            }
                            else if (updated.Contains("\"dual_engine\":{"))
                            {
                                updated = updated.Replace("\"dual_engine\":{", "\"dual_engine\":{\"user_list_data_1\":{" + entries + "},");
                            }

                            if (!updated.Contains(",}") && !updated.Contains(",]"))
                            {
                                File.WriteAllText(prefPath, updated, Encoding.UTF8);
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }

    public class SystemDiagnoser
    {
        public static string FindNTClientPath()
        {
            var candidates = new string[]
            {
                @"D:\Program Files\KP\NTClientSoftware\NTClientSoftware.exe",
                @"C:\Program Files\KP\NTClientSoftware\NTClientSoftware.exe",
                @"C:\Program Files (x86)\KP\NTClientSoftware\NTClientSoftware.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"KP\NTClientSoftware\NTClientSoftware.exe")
            };
            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static string GetNTClientVersion()
        {
            var path = FindNTClientPath();
            if (path == null) return "Не установлена";
            try
            {
                var xmlPath = Path.Combine(Path.GetDirectoryName(path), "components.xml");
                if (File.Exists(xmlPath))
                {
                    var lines = File.ReadAllLines(xmlPath);
                    foreach (var line in lines)
                    {
                        if (line.Contains("<ApplicationVersion>"))
                        {
                            var v = line.Replace("<ApplicationVersion>", "").Replace("</ApplicationVersion>", "").Trim();
                            return "v" + v;
                        }
                    }
                }
                var vi = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(vi.FileVersion) && vi.FileVersion != "0.0.0.0") return "v" + vi.FileVersion;
            }
            catch { }
            return "Установлена (версия не определена)";
        }

        public static string FindAvTunProxyPath()
        {
            var candidates = new string[]
            {
                @"C:\Program Files\Avest\AvTunProxy\avtunproxy.exe",
                @"C:\Program Files (x86)\Avest\AvTunProxy\avtunproxy.exe",
                @"D:\Program Files\Avest\AvTunProxy\avtunproxy.exe"
            };
            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static string GetAvTunProxyVersion()
        {
            var path = FindAvTunProxyPath();
            if (path == null) return "Не установлена";
            try
            {
                var vPath = Path.Combine(Path.GetDirectoryName(path), "version");
                if (File.Exists(vPath))
                {
                    var firstLine = File.ReadAllLines(vPath)[0].Trim();
                    return "v" + firstLine;
                }
                var vi = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(vi.FileVersion)) return "v" + vi.FileVersion;
            }
            catch { }
            return "Установлена (версия не определена)";
        }

        public static bool IsAvestInstalled()
        {
            try
            {
                var paths = new string[]
                {
                    @"C:\Program Files (x86)\Avest",
                    @"C:\Program Files\Avest",
                    @"C:\ProgramData\Avest"
                };
                foreach (var p in paths)
                {
                    if (Directory.Exists(p)) return true;
                }

                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Avest\Avest CSP"))
                {
                    if (k != null) return true;
                }
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Avest\Avest CSP"))
                {
                    if (k != null) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsGovDomainLine(string line)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) return false;
            var lower = line.ToLowerInvariant();
            return lower.Contains(".gov.by") || lower.Contains("nces.by") || lower.Contains("e-pasluga.by") || lower.Contains("bgs.by") || lower.Contains("skko.by");
        }

        public static bool CheckHostsFile(out string details)
        {
            try
            {
                var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (!File.Exists(hostsPath))
                {
                    details = "Файл hosts не найден (по умолчанию) ✓";
                    return true;
                }
                var lines = File.ReadAllLines(hostsPath);
                var suspiciousDomains = new List<string>();
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (IsGovDomainLine(line))
                    {
                        var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        var dom = parts.Length >= 2 ? parts[1] : line;
                        if (!suspiciousDomains.Contains(dom)) suspiciousDomains.Add(dom);
                    }
                }
                if (suspiciousDomains.Count > 0)
                {
                    details = "Блокировка в hosts: " + string.Join(", ", suspiciousDomains.ToArray());
                    return false;
                }
                details = "Чист (подмен госдоменов нет) ✓";
                return true;
            }
            catch (Exception ex)
            {
                details = "Ошибка чтения: " + ex.Message;
                return true;
            }
        }

        private static bool FilesHaveSameContent(string pathA, string pathB)
        {
            try
            {
                if (!File.Exists(pathA) || !File.Exists(pathB)) return false;
                var infoA = new FileInfo(pathA);
                var infoB = new FileInfo(pathB);
                if (infoA.Length != infoB.Length) return false;
                byte[] bytesA = File.ReadAllBytes(pathA);
                byte[] bytesB = File.ReadAllBytes(pathB);
                if (bytesA.Length != bytesB.Length) return false;
                for (int i = 0; i < bytesA.Length; i++)
                {
                    if (bytesA[i] != bytesB[i]) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void FlushDnsCache()
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false }))
                {
                    if (p != null) p.WaitForExit(3000);
                }
            }
            catch { }
        }

        public static bool CleanHostsFile()
        {
            try
            {
                var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (!File.Exists(hostsPath)) return true;

                var lines = File.ReadAllLines(hostsPath, Encoding.UTF8);
                var cleanLines = new List<string>();
                bool changed = false;
                foreach (var rawLine in lines)
                {
                    var trimmed = rawLine.Trim();
                    if (IsGovDomainLine(trimmed))
                    {
                        changed = true;
                        continue;
                    }
                    cleanLines.Add(rawLine);
                }
                if (!changed) return true;

                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var hostsBak = Path.Combine(dir, "hosts.buhly.bak");
                if (!File.Exists(hostsBak))
                {
                    try { File.Copy(hostsPath, hostsBak, false); } catch { }
                }
                var cleanPath = Path.Combine(dir, "hosts.clean");
                File.WriteAllLines(cleanPath, cleanLines.ToArray(), new UTF8Encoding(false));

                try
                {
                    File.Copy(cleanPath, hostsPath, true);
                    FlushDnsCache();
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    try
                    {
                        var psi = new ProcessStartInfo("cmd.exe", "/c copy /y \"" + cleanPath + "\" \"" + hostsPath + "\" && ipconfig /flushdns")
                        {
                            UseShellExecute = true,
                            Verb = "runas",
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using (var p = Process.Start(psi))
                        {
                            if (p == null) return false;
                            if (!p.WaitForExit(15000)) return false;
                            if (p.ExitCode != 0) return false;
                        }
                        string dummy;
                        return CheckHostsFile(out dummy);
                    }
                    catch (Win32Exception)
                    {
                        // User declined UAC elevation prompt
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool RestoreHostsFromBackup(out string details)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
                var hostsBak = Path.Combine(dir, "hosts.buhly.bak");
                if (!File.Exists(hostsBak))
                {
                    details = "Бэкап hosts не найден (файл hosts не изменялся)";
                    return true;
                }

                var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                try
                {
                    File.Copy(hostsBak, hostsPath, true);
                    FlushDnsCache();
                    details = "Файл hosts восстановлен из бэкапа";
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    try
                    {
                        var psi = new ProcessStartInfo("cmd.exe", "/c copy /y \"" + hostsBak + "\" \"" + hostsPath + "\" && ipconfig /flushdns")
                        {
                            UseShellExecute = true,
                            Verb = "runas",
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using (var p = Process.Start(psi))
                        {
                            if (p == null || !p.WaitForExit(15000) || p.ExitCode != 0)
                            {
                                details = "Не удалось восстановить hosts (ошибка команды или таймаут)";
                                return false;
                            }
                        }
                        if (!FilesHaveSameContent(hostsBak, hostsPath))
                        {
                            details = "Файл hosts не был восстановлен (нет прав администратора)";
                            return false;
                        }
                        details = "Файл hosts восстановлен из бэкапа";
                        return true;
                    }
                    catch (Win32Exception)
                    {
                        details = "Восстановление hosts отменено пользователем (UAC)";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                details = "Ошибка восстановления hosts: " + ex.Message;
                return false;
            }
        }

        public static bool RestoreProxyAndHostsFromBackup(out string details)
        {
            string pacMsg;
            bool pacOk = WinINetHelper.RestorePacFromBackup(out pacMsg);
            string hostsMsg;
            bool hostsOk = RestoreHostsFromBackup(out hostsMsg);
            string zonesMsg;
            bool zonesOk = EdgeHelper.RestoreZones2FromBackup(out zonesMsg);
            string edgeMsg;
            bool edgeOk = EdgeHelper.RestoreEdgeFromBackup(out edgeMsg);
            details = pacMsg + "\n" + hostsMsg + "\n" + zonesMsg + "\n" + edgeMsg;
            return pacOk && hostsOk && zonesOk && edgeOk;
        }

        public static bool CheckTimeSync(out string details)
        {
            var urls = new string[] { "https://www.nbrb.by/api/exrates/rates/431", "http://vat.gov.by/mainPage/" };
            foreach (var u in urls)
            {
                try
                {
                    Program.ConfigureSafeTls();
                    var req = (HttpWebRequest)WebRequest.Create(u);
                    req.Method = "GET";
                    req.Timeout = 2000;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    {
                        var dateStr = resp.Headers["Date"];
                        if (!string.IsNullOrEmpty(dateStr))
                        {
                            DateTime serverTime;
                            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out serverTime))
                            {
                                var diffSeconds = Math.Abs((DateTime.UtcNow - serverTime).TotalSeconds);
                                if (diffSeconds > 180)
                                {
                                    details = string.Format("Рассинхрон {0} сек! (часы спешат/отстают)", (int)diffSeconds);
                                    return false;
                                }
                                details = string.Format("{0} (рассинхрон {1} с) ✓", DateTime.Now.ToString("HH:mm:ss"), (int)diffSeconds);
                                return true;
                            }
                        }
                    }
                }
                catch { }
            }
            details = "Нет ответа сервера для сверки часов ⚠️";
            return false;
        }

        public static bool IsPort8084Healthy()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect("127.0.0.1", 8084, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    if (!ok) return false;
                    client.EndConnect(ar);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAvTunProxyRunning()
        {
            var procs = Process.GetProcessesByName("avtunproxy");
            try
            {
                return procs.Length > 0;
            }
            finally
            {
                foreach (var p in procs)
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }

        public static bool IsPacEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("DefaultConnectionSettings") as byte[];
                        if (val != null && val.Length > 8)
                        {
                            return (val[8] & 8) != 0;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static List<CertInfo> GetConnectedGovCertificates()
        {
            return GetAllGovCertificates(true);
        }

        public static List<CertInfo> GetAllGovCertificates(bool requirePhysicalUsb)
        {
            var list = new List<CertInfo>();
            X509Store store = null;
            try
            {
                store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                foreach (var cert in store.Certificates)
                {
                    var issuer = cert.Issuer;
                    if (issuer.Contains("ГосСУОК") || issuer.Contains("pki.gov.by") || issuer.Contains("НЦЭУ"))
                    {
                        bool inUsb = CryptoHelper.IsTokenPhysicallyInserted(cert);
                        if (requirePhysicalUsb && !inUsb)
                        {
                            continue;
                        }

                        string certUnp, org, person, certTitle;
                        CryptoHelper.ExtractGovCertDetails(cert, out certUnp, out org, out person, out certTitle);
                        bool isCompany = org.ToLower().Contains("общество") || org.ToLower().Contains("ооо") || org.ToLower().Contains("зао") || org.ToLower().Contains("оао") || org.ToLower().Contains("чуп");

                        var days = (cert.NotAfter - DateTime.Now).Days;
                        list.Add(new CertInfo
                        {
                            Thumbprint = cert.Thumbprint ?? (org + "_" + cert.NotAfter.Ticks),
                            OrgName = org,
                            PersonName = person,
                            ValidTo = cert.NotAfter,
                            DaysRemaining = days,
                            IsValid = days > 0,
                            IsCompany = isCompany,
                            IsPhysicallyConnected = inUsb,
                            LastSeen = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
                        });
                    }
                }
            }
            catch { }
            finally
            {
                if (store != null)
                {
                    try { store.Close(); } catch { }
                    var disp = store as IDisposable;
                    if (disp != null)
                    {
                        try { disp.Dispose(); } catch { }
                    }
                }
            }
            return list;
        }

        // Важно: никогда не вызывать Process.Kill() для EDeclaration (АРМ «Плательщик») и MngCert,
        // чтобы не потерять несохранённые декларации — только предупреждать пользователя.
        public static string GetRunningConflictingApps()
        {
            var found = new List<string>();
            var conflictingNames = new string[] { "EDeclaration", "MngCert" };
            foreach (var name in conflictingNames)
            {
                Process[] procs = null;
                try
                {
                    procs = Process.GetProcessesByName(name);
                    if (procs.Length > 0)
                        found.Add(name == "EDeclaration" ? "АРМ Плательщик" : name);
                }
                catch { }
                finally
                {
                    if (procs != null)
                    {
                        foreach (var p in procs)
                        {
                            try { p.Dispose(); } catch { }
                        }
                    }
                }
            }
            return string.Join(", ", found.ToArray());
        }

        public static bool RepairAll()
        {
            // Gracefully restart only NTClientSoftware (never kill EDeclaration/MngCert)
            var ntProcs = Process.GetProcessesByName("NTClientSoftware");
            try
            {
                foreach (var p in ntProcs)
                {
                    try
                    {
                        if (p.CloseMainWindow())
                        {
                            p.WaitForExit(1200);
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                foreach (var p in ntProcs)
                {
                    try { p.Dispose(); } catch { }
                }
            }
            Thread.Sleep(400);

            // 2. Start NTClient
            var ntPath = FindNTClientPath();
            if (ntPath != null)
            {
                try
                {
                    var psi = new ProcessStartInfo(ntPath)
                    {
                        WorkingDirectory = Path.GetDirectoryName(ntPath)
                    };
                    using (var p = Process.Start(psi)) { }
                }
                catch { }
            }

            // 3. Start AvTunProxy
            if (!IsAvTunProxyRunning())
            {
                var avPath = FindAvTunProxyPath();
                if (avPath != null)
                {
                    try
                    {
                        var psi = new ProcessStartInfo(avPath)
                        {
                            WorkingDirectory = Path.GetDirectoryName(avPath)
                        };
                        using (var p = Process.Start(psi)) { }
                    }
                    catch { }
                }
            }

            // 4. Enable PAC for FSZN only if AvTunProxy is present/running, Trusted Sites for ActiveX, and clean hosts if hijacked
            if (FindAvTunProxyPath() != null || IsAvTunProxyRunning())
            {
                WinINetHelper.EnablePac("http://127.0.0.1:10224/proxy.pac");
            }
            EdgeHelper.EnsureTrustedSitesAndActiveX();
            bool hostsOk = true;
            string hostsDummy;
            if (!CheckHostsFile(out hostsDummy))
            {
                hostsOk = CleanHostsFile();
            }

            // Smart wait for NTClient to bind port 8084 (up to 4 sec)
            for (int i = 0; i < 8; i++)
            {
                if (IsPort8084Healthy()) break;
                Thread.Sleep(500);
            }

            return hostsOk;
        }
    }

    public static class ComponentInstaller
    {
        private const long MaxZipUnpackedBytes = 250L * 1024L * 1024L;
        private const int MaxZipEntries = 2000;

        private static List<string> GetUserSearchDirs()
        {
            var dirs = new List<string>();
            var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(dl)) dirs.Add(dl);
            var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desk)) dirs.Add(desk);
            return dirs;
        }

        private static void ExtractZipSafely(string zipPath, string extractPath)
        {
            Directory.CreateDirectory(extractPath);
            string fullRoot = Path.GetFullPath(extractPath);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullRoot += Path.DirectorySeparatorChar;

            long totalWritten = 0;
            int count = 0;
            byte[] buffer = new byte[81920];

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    count++;
                    if (count > MaxZipEntries)
                        throw new InvalidOperationException("Архив содержит слишком много файлов.");
                    if (entry.Length > MaxZipUnpackedBytes)
                        throw new InvalidOperationException("Размер файла в архиве превышает безопасный лимит 250 МБ.");

                    string destPath = Path.GetFullPath(Path.Combine(fullRoot, entry.FullName));
                    if (!destPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Обнаружен некорректный путь в архиве.");

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    string parentDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                        Directory.CreateDirectory(parentDir);

                    using (var inStream = entry.Open())
                    using (var outStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int read;
                        while ((read = inStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            totalWritten += read;
                            if (totalWritten > MaxZipUnpackedBytes)
                                throw new InvalidOperationException("Суммарный размер распакованных данных превысил лимит 250 МБ (защита от ZIP-бомбы).");
                            outStream.Write(buffer, 0, read);
                        }
                    }
                }
            }
        }

        private static bool VerifyAndConfirmSignedExecutable(string exePath, out string errorMsg)
        {
            errorMsg = null;
            bool hasSignature = false;
            try
            {
                using (var cert = X509Certificate.CreateFromSignedFile(exePath))
                {
                    if (cert != null && !string.IsNullOrEmpty(cert.Subject))
                    {
                        hasSignature = true;
                    }
                }
            }
            catch (CryptographicException)
            {
                hasSignature = false;
            }
            catch
            {
                hasSignature = false;
            }

            if (hasSignature) return true;

            string fileName = Path.GetFileName(exePath);
            string warnText = string.Format(
                "Файл «{0}» не имеет цифровой подписи издателя (Authenticode).\n\n" +
                "Путь: {1}\n\n" +
                "Запуск неподписанного файла может быть небезопасен, если он скачан не с официального сайта.\n" +
                "Вы уверены, что хотите запустить этот установщик?",
                fileName, exePath);

            MessageBoxResult res = MessageBoxResult.No;
            try
            {
                var app = Application.Current;
                if (app != null && app.Dispatcher != null && !app.Dispatcher.HasShutdownStarted)
                {
                    app.Dispatcher.Invoke(new Action(delegate
                    {
                        Window owner = app.MainWindow;
                        if (owner != null && owner.IsVisible)
                            res = MessageBox.Show(owner, warnText, "Buhly — Проверка цифровой подписи", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                        else
                            res = MessageBox.Show(warnText, "Buhly — Проверка цифровой подписи", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    }));
                }
                else
                {
                    res = MessageBox.Show(warnText, "Buhly — Проверка цифровой подписи", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                }
            }
            catch
            {
                res = MessageBoxResult.No;
            }

            if (res == MessageBoxResult.Yes) return true;

            errorMsg = string.Format("Файл «{0}» не имеет цифровой подписи Authenticode — запуск отменён.", fileName);
            return false;
        }

        public static string FindLocalGossuokInstaller()
        {
            foreach (var dir in GetUserSearchDirs())
            {
                try
                {
                    var exes = Directory.GetFiles(dir, "AvPKISetup*.exe", SearchOption.TopDirectoryOnly);
                    if (exes.Length > 0) return exes[0];
                    var zips = Directory.GetFiles(dir, "AvPKISetup*.zip", SearchOption.TopDirectoryOnly);
                    if (zips.Length > 0) return zips[0];
                }
                catch { }
            }
            return null;
        }

        public static string FindLocalAvTunInstaller()
        {
            foreach (var dir in GetUserSearchDirs())
            {
                try
                {
                    var exes = Directory.GetFiles(dir, "*AvTunProxy*.exe", SearchOption.TopDirectoryOnly);
                    if (exes.Length > 0) return exes[0];
                }
                catch { }
            }
            return null;
        }

        public static void DownloadAndInstallGossuok(Action<string, int> onProgress, Action<bool, string> onCompleted)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var localFile = FindLocalGossuokInstaller();
                    if (localFile != null && File.Exists(localFile))
                    {
                        string targetExe = localFile;
                        if (localFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            if (onProgress != null) onProgress("Распаковка загруженного архива ГосСУОК...", 60);
                            var dlDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                            var extractPath = Path.Combine(dlDir, "AvPKISetup_Unpacked");
                            if (Directory.Exists(extractPath))
                            {
                                try { Directory.Delete(extractPath, true); } catch { }
                            }
                            ExtractZipSafely(localFile, extractPath);
                            var exeFiles = Directory.GetFiles(extractPath, "AvPKISetup*.exe", SearchOption.AllDirectories);
                            targetExe = exeFiles.Length > 0 ? exeFiles[0] : null;
                            if (targetExe == null)
                            {
                                var setupExe = Directory.GetFiles(extractPath, "setup*.exe", SearchOption.AllDirectories);
                                if (setupExe.Length > 0) targetExe = setupExe[0];
                            }
                        }

                        if (targetExe != null && File.Exists(targetExe))
                        {
                            if (onProgress != null) onProgress("Проверка цифровой подписи установщика...", 85);
                            string sigErr;
                            if (!VerifyAndConfirmSignedExecutable(targetExe, out sigErr))
                            {
                                if (onCompleted != null) onCompleted(false, sigErr);
                                return;
                            }
                            if (onProgress != null) onProgress("Запуск мастера установки ГосСУОК...", 100);
                            using (var p = Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true })) { }
                            if (onCompleted != null) onCompleted(true, "Установщик запущен! Завершите установку и вставьте ключ.");
                            return;
                        }
                    }

                    if (onProgress != null) onProgress("Проверка официальной ссылки nces.by...", 50);
                    var directUrl = "https://nces.by/wp-content/uploads/gossuok/2026/AvPKISetup(bel).zip";
                    var fallbackUrl = "https://nces.by/service/po/";
                    var urlToOpen = fallbackUrl;
                    try
                    {
                        Program.ConfigureSafeTls();
                        var headReq = (HttpWebRequest)WebRequest.Create(directUrl);
                        headReq.Method = "HEAD";
                        headReq.Timeout = 2500;
                        using (var headResp = (HttpWebResponse)headReq.GetResponse())
                        {
                            if ((int)headResp.StatusCode >= 200 && (int)headResp.StatusCode < 400)
                            {
                                urlToOpen = directUrl;
                            }
                        }
                    }
                    catch { }

                    if (onProgress != null) onProgress("Открытие официальной загрузки nces.by в браузере...", 100);
                    using (var p = Process.Start(new ProcessStartInfo(urlToOpen) { UseShellExecute = true })) { }
                    if (onCompleted != null) onCompleted(true, "Загрузка открыта в вашем браузере. После скачивания нажмите кнопку ещё раз для запуска!");
                }
                catch (Exception ex)
                {
                    if (onCompleted != null) onCompleted(false, "Не удалось открыть загрузку: " + ex.Message);
                }
            });
        }

        public static void DownloadAndInstallAvTunProxy(Action<string, int> onProgress, Action<bool, string> onCompleted)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var localFile = FindLocalAvTunInstaller();
                    if (localFile != null && File.Exists(localFile))
                    {
                        if (onProgress != null) onProgress("Проверка цифровой подписи установщика...", 75);
                        string sigErr;
                        if (!VerifyAndConfirmSignedExecutable(localFile, out sigErr))
                        {
                            if (onCompleted != null) onCompleted(false, sigErr);
                            return;
                        }
                        if (onProgress != null) onProgress("Запуск установщика AvTunProxy...", 100);
                        using (var p = Process.Start(new ProcessStartInfo(localFile) { UseShellExecute = true })) { }
                        if (onCompleted != null) onCompleted(true, "Установщик AvTunProxy запущен! Завершите установку.");
                        return;
                    }

                    if (onProgress != null) onProgress("Открытие официальной загрузки avtunproxy.by в браузере...", 100);
                    var url = "https://avtunproxy.by/download/AvTunProxy-latest.exe";
                    using (var p = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { }
                    if (onCompleted != null) onCompleted(true, "Загрузка открыта в вашем браузере. После скачивания нажмите кнопку ещё раз для запуска!");
                }
                catch (Exception ex)
                {
                    if (onCompleted != null) onCompleted(false, "Не удалось открыть загрузку: " + ex.Message);
                }
            });
        }

        public static string FindLocalNTClientInstaller()
        {
            foreach (var dir in GetUserSearchDirs())
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    var files = Directory.GetFiles(dir, "*NTClient*.exe", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0) return files[0];
                }
                catch { }
            }
            return null;
        }
    }
}
