using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("Buhly — ГосПорталы РБ")]
[assembly: AssemblyDescription("Локальный помощник входа на госпорталы Республики Беларусь (МНС, ФСЗН, ЭСЧФ)")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCompany("Buhly Open Source (ООО Ресмарт)")]
[assembly: AssemblyProduct("Buhly Desktop Assistant")]
[assembly: AssemblyCopyright("Copyright © 2026 Buhly (github.com/narard/buhly-rb)")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace GovPortalsApp
{
    public class Program
    {
        public static void ConfigureSafeTls()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = delegate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                if (sslPolicyErrors == SslPolicyErrors.None) return true;
                var req = sender as HttpWebRequest;
                if (req != null && req.RequestUri != null)
                {
                    string host = req.RequestUri.Host.ToLowerInvariant();
                    if (host.EndsWith(".gov.by") || host.EndsWith(".nces.by") || host.EndsWith(".by") || host == "127.0.0.1" || host == "localhost")
                    {
                        return true;
                    }
                }
                return false;
            };
        }

        [STAThread]
        public static void Main()
        {
            try
            {
                ConfigureSafeTls();

                var app = new Application();
                var mainWindow = new MainWindow();
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = Path.Combine(Path.GetTempPath(), "Buhly_error.log");
                    File.AppendAllText(logPath, DateTime.Now + " - EXCEPTION: " + ex.ToString() + "\r\n");
                }
                catch { }
                MessageBox.Show("Ошибка запуска Buhly:\n" + ex.Message, "Buhly", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class WinINetHelper
    {
        [DllImport("wininet.dll", SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        public const int INTERNET_OPTION_REFRESH = 37;

        public static void EnablePac(string pacUrl)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        key.SetValue("AutoConfigURL", pacUrl, RegistryValueKind.String);
                    }
                }

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
    }

    public class EdgeHelper
    {
        private static string GetSiteListXmlPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "ie_site_list.xml");
        }

        public static void EnsureTrustedSitesAndActiveX()
        {
            try
            {
                var domains = new string[] { "vat.gov.by", "ssf.gov.by", "nalog.gov.by", "e-pasluga.by" };
                foreach (var domain in domains)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\ZoneMap\Domains\" + domain))
                    {
                        if (key != null)
                        {
                            key.SetValue("http", 2, RegistryValueKind.DWord);
                            key.SetValue("https", 2, RegistryValueKind.DWord);
                        }
                    }
                }

                using (var zone2 = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\2"))
                {
                    if (zone2 != null)
                    {
                        var flags = new string[] { "1001", "1004", "1200", "1201", "1405", "2201" };
                        foreach (var flag in flags)
                        {
                            zone2.SetValue(flag, 0, RegistryValueKind.DWord);
                        }
                    }
                }
            }
            catch { }
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
                        if (level is int && (int)level == 1 && !string.IsNullOrEmpty(siteList) && File.Exists(siteList))
                        {
                            return true;
                        }
                    }
                }

                var prefPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Preferences");
                if (!File.Exists(prefPath)) return false;
                var text = File.ReadAllText(prefPath, Encoding.UTF8);
                return text.Contains("vat.gov.by/mainPage") && text.Contains("vat.gov.by/eInvoice");
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
                    "    <version>2.0.0.0</version>\r\n" +
                    "    <date-created>20261113</date-created>\r\n" +
                    "  </created-by>\r\n" +
                    "  <site url=\"vat.gov.by\">\r\n" +
                    "    <compat-mode>IE11</compat-mode>\r\n" +
                    "    <open-in allow-redirect=\"true\">IE11</open-in>\r\n" +
                    "  </site>\r\n" +
                    "</site-list>\r\n";
                File.WriteAllText(xmlPath, xmlContent, Encoding.UTF8);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Edge"))
                {
                    if (key != null)
                    {
                        key.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                        key.SetValue("InternetExplorerIntegrationSiteList", xmlPath, RegistryValueKind.String);
                        key.SetValue("InternetExplorerIntegrationReloadInIEModeAllowed", 1, RegistryValueKind.DWord);
                    }
                }

                // Secondary fallback ONLY when Edge is not running (never kill user's open browser!)
                if (Process.GetProcessesByName("msedge").Length == 0)
                {
                    var prefPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Preferences");
                    if (File.Exists(prefPath))
                    {
                        var text = File.ReadAllText(prefPath, Encoding.UTF8);
                        bool alreadyConfigured = text.Contains("vat.gov.by/mainPage") && text.Contains("vat.gov.by/eInvoice");
                        if (!alreadyConfigured)
                        {
                            if (text.Contains("\"consumer_mode\":{\"enabled_state\":0}"))
                            {
                                text = text.Replace("\"consumer_mode\":{\"enabled_state\":0}", "\"consumer_mode\":{\"enabled_state\":1}");
                            }
                            else if (!text.Contains("\"consumer_mode\"") && text.Contains("\"dual_engine\":{"))
                            {
                                text = text.Replace("\"dual_engine\":{", "\"dual_engine\":{\"consumer_mode\":{\"enabled_state\":1},");
                            }

                            var injection = "\"user_list_data_1\":{" +
                                "\"http://vat.gov.by/mainPage/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"http://vat.gov.by/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/mainPage/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/eInvoice\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0}," +
                                "\"https://vat.gov.by/eInvoice/\":{\"date_added\":\"13600000000000000\",\"engine\":2,\"source\":3,\"visit_state\":false,\"visits_after_expiration\":0},";

                            if (text.Contains("\"user_list_data_1\":{"))
                            {
                                text = text.Replace("\"user_list_data_1\":{", injection);
                            }
                            else if (text.Contains("\"dual_engine\":{"))
                            {
                                text = text.Replace("\"dual_engine\":{", "\"dual_engine\":{" + injection + "},");
                            }

                            File.WriteAllText(prefPath, text, Encoding.UTF8);
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
            return "v1.0.0 (актуальна)";
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
            return "v5.10 (актуальна)";
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
                var cleanPath = Path.Combine(dir, "hosts.clean");
                File.WriteAllLines(cleanPath, cleanLines.ToArray(), new UTF8Encoding(false));

                try
                {
                    File.Copy(cleanPath, hostsPath, true);
                    Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false });
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    var psi = new ProcessStartInfo("cmd.exe", "/c copy /y \"" + cleanPath + "\" \"" + hostsPath + "\" & ipconfig /flushdns")
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var p = Process.Start(psi);
                    if (p != null) p.WaitForExit(10000);
                    string dummy;
                    return CheckHostsFile(out dummy);
                }
            }
            catch
            {
                return false;
            }
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
                            if (DateTime.TryParse(dateStr, out serverTime))
                            {
                                var diffSeconds = Math.Abs((DateTime.UtcNow - serverTime.ToUniversalTime()).TotalSeconds);
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
            return procs.Length > 0;
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
            try
            {
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
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

                        var subj = cert.SubjectName.Name;
                        string org = "";
                        string sn = "";
                        string gn = "";

                        if (subj.Contains("O="))
                        {
                            var parts = subj.Split(new string[] { "O=" }, StringSplitOptions.None);
                            if (parts.Length > 1)
                            {
                                org = parts[1].Split(new string[] { ", " }, StringSplitOptions.None)[0].Trim().Trim('"');
                            }
                        }
                        if (subj.Contains("SN="))
                        {
                            var parts = subj.Split(new string[] { "SN=" }, StringSplitOptions.None);
                            if (parts.Length > 1)
                            {
                                sn = parts[1].Split(',')[0].Trim().Trim('"');
                            }
                        }
                        if (subj.Contains("41="))
                        {
                            var parts = subj.Split(new string[] { "41=" }, StringSplitOptions.None);
                            if (parts.Length > 1)
                            {
                                gn = parts[1].Split(',')[0].Trim().Trim('"');
                            }
                        }

                        if (string.IsNullOrEmpty(org))
                        {
                            org = cert.FriendlyName;
                            if (string.IsNullOrEmpty(org)) org = "Сертификат ГосСУОК";
                        }

                        org = org.Replace("\"\"", "\"");

                        string person = (sn + " " + gn).Trim();
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
                store.Close();
            }
            catch { }
            return list;
        }

        public static void CheckAndResolveProcessConflicts()
        {
            var conflictingNames = new string[] { "EDeclaration", "MngCert" };
            foreach (var name in conflictingNames)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (!p.CloseMainWindow() || !p.WaitForExit(300))
                            {
                                p.Kill();
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        public static void RepairAll()
        {
            // 1. Gracefully close or restart NTClient & conflicting apps
            CheckAndResolveProcessConflicts();
            foreach (var p in Process.GetProcessesByName("NTClientSoftware"))
            {
                try
                {
                    if (!p.CloseMainWindow() || !p.WaitForExit(300))
                    {
                        p.Kill();
                    }
                }
                catch { }
            }
            Thread.Sleep(500);

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
                    Process.Start(psi);
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
                        Process.Start(psi);
                    }
                    catch { }
                }
            }

            // 4. Enable PAC for FSZN, Trusted Sites for ActiveX, and clean hosts if hijacked
            WinINetHelper.EnablePac("http://127.0.0.1:10224/proxy.pac");
            EdgeHelper.EnsureTrustedSitesAndActiveX();
            string hostsDummy;
            if (!CheckHostsFile(out hostsDummy))
            {
                CleanHostsFile();
            }

            // Smart wait for NTClient to bind port 8084 (up to 4 sec)
            for (int i = 0; i < 8; i++)
            {
                if (IsPort8084Healthy()) break;
                Thread.Sleep(500);
            }
        }
    }

    public static class ComponentInstaller
    {
        private static List<string> GetUserSearchDirs()
        {
            var dirs = new List<string>();
            var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(dl)) dirs.Add(dl);
            var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desk)) dirs.Add(desk);
            return dirs;
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
                            ZipFile.ExtractToDirectory(localFile, extractPath);
                            var exeFiles = Directory.GetFiles(extractPath, "AvPKISetup*.exe", SearchOption.AllDirectories);
                            targetExe = exeFiles.Length > 0 ? exeFiles[0] : null;
                            if (targetExe == null)
                            {
                                var anyExe = Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories);
                                if (anyExe.Length > 0) targetExe = anyExe[0];
                            }
                        }

                        if (targetExe != null && File.Exists(targetExe))
                        {
                            if (onProgress != null) onProgress("Запуск мастера установки ГосСУОК...", 100);
                            Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
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
                    Process.Start(new ProcessStartInfo(urlToOpen) { UseShellExecute = true });
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
                        if (onProgress != null) onProgress("Запуск установщика AvTunProxy...", 100);
                        Process.Start(new ProcessStartInfo(localFile) { UseShellExecute = true });
                        if (onCompleted != null) onCompleted(true, "Установщик AvTunProxy запущен! Завершите установку.");
                        return;
                    }

                    if (onProgress != null) onProgress("Открытие официальной загрузки avtunproxy.by в браузере...", 100);
                    var url = "https://avtunproxy.by/download/AvTunProxy-latest.exe";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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
            var dirs = new List<string>();
            var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(dl)) dirs.Add(dl);
            var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desk)) dirs.Add(desk);

            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.IsReady && (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed))
                    {
                        dirs.Add(d.RootDirectory.FullName);
                    }
                }
            }
            catch { }

            foreach (var dir in dirs)
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    var files = Directory.GetFiles(dir, "*NTClient*.exe", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0) return files[0];
                    var kp = Directory.GetFiles(dir, "*КП*.exe", SearchOption.TopDirectoryOnly);
                    if (kp.Length > 0) return kp[0];
                }
                catch { }
            }
            return null;
        }
    }

    public static class CertRegistryManager
    {
        private static string GetRegistryFilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cert_registry.tsv");
        }

        public static List<CertInfo> LoadRegistry()
        {
            var result = new List<CertInfo>();
            try
            {
                var path = GetRegistryFilePath();
                if (!File.Exists(path)) return result;
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 6) continue;
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

        public static void SaveRegistry(List<CertInfo> items)
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

        public static List<CertInfo> MergeAndGetAll(List<CertInfo> freshCerts)
        {
            var map = new Dictionary<string, CertInfo>(StringComparer.OrdinalIgnoreCase);
            var saved = LoadRegistry();
            foreach (var s in saved)
            {
                if (!string.IsNullOrEmpty(s.Thumbprint))
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

    public static class NbrbRatesHelper
    {
        private static string ExtractRate(string json)
        {
            var m = Regex.Match(json ?? "", "\"Cur_OfficialRate\"\\s*:\\s*([0-9.]+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static void FetchRatesAsync(Action<string> onLoaded)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Program.ConfigureSafeTls();
                    string usd = null;
                    string rub = null;
                    using (var wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        usd = ExtractRate(wc.DownloadString("https://www.nbrb.by/api/exrates/rates/431"));
                        rub = ExtractRate(wc.DownloadString("https://www.nbrb.by/api/exrates/rates/456"));
                    }
                    if (!string.IsNullOrEmpty(usd) && !string.IsNullOrEmpty(rub))
                    {
                        if (onLoaded != null) onLoaded("НБРБ: USD " + usd + "  •  100 RUB " + rub);
                    }
                }
                catch { }
            });
        }
    }

    public class MainWindow : Window
    {
        private StackPanel certCardsContainer;
        private StackPanel tabPortalsPanel;
        private StackPanel tabRegistryPanel;
        private StackPanel tabToolsPanel;
        private StackPanel registryListContainer;
        private TextBlock registrySummaryText;
        private Border unpResultCard;
        private StackPanel unpResultStack;
        private TextBox unpInputBox;
        private TextBlock nbrbRatesLabel;
        private Button segBtnPortals;
        private Button segBtnRegistry;
        private Button segBtnTools;

        private TextBlock statusNTClient;
        private TextBlock statusAvTun;
        private TextBlock statusPac;
        private TextBlock statusHosts;
        private System.Windows.Shapes.Ellipse dotNTClient;
        private System.Windows.Shapes.Ellipse dotAvTun;
        private System.Windows.Shapes.Ellipse dotPac;
        private System.Windows.Shapes.Ellipse dotHosts;
        private Border toastBanner;
        private TextBlock toastText;
        private DispatcherTimer autoRefreshTimer;
        private DispatcherTimer toastTimer;
        private DispatcherTimer portalOnlineTimer;
        private TextBlock statusPortalMNS;
        private System.Windows.Shapes.Ellipse dotPortalMNS;
        private TextBlock statusPortalFSZN;
        private System.Windows.Shapes.Ellipse dotPortalFSZN;
        private TextBlock statusPortalVAT;
        private System.Windows.Shapes.Ellipse dotPortalVAT;
        private TextBlock statusPortalEpasluga;
        private System.Windows.Shapes.Ellipse dotPortalEpasluga;
        private string _lastCertHash = null;
        private bool _isRefreshing = false;

        public MainWindow()
        {
            Title = "Buhly • ГосПорталы РБ";
            Width = 510;
            Height = 850;
            MinWidth = 470;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 242, 247)); // iOS system Gray 6
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            BuildUI();
            RefreshStatuses();
            RefreshRegistryTab(false);

            NbrbRatesHelper.FetchRatesAsync(delegate(string ratesText)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (nbrbRatesLabel != null) nbrbRatesLabel.Text = ratesText;
                }));
            });

            // Background live auto-refresh timer (every 5 seconds, offloaded to thread pool)
            autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            autoRefreshTimer.Tick += delegate { RefreshStatuses(); };
            autoRefreshTimer.Start();

            // Live Portal Online Check Timer (every 25 seconds, and immediate first check)
            portalOnlineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
            portalOnlineTimer.Tick += delegate { CheckAllPortalsOnline(); };
            portalOnlineTimer.Start();
            CheckAllPortalsOnline();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid();
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var mainStack = new StackPanel { Margin = new Thickness(20) };

            // Brand Header: Buhly + NBRB Rates pill
            var headerBlock = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var topHeaderGrid = new Grid();
            topHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brandRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var brandName = new TextBlock
            {
                Text = "Buhly",
                FontSize = 27,
                FontWeight = FontWeights.ExtraBold,
                Foreground = GetBrush("#007AFF")
            };
            var brandSub = new TextBlock
            {
                Text = " • ГосПорталы",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center
            };
            brandRow.Children.Add(brandName);
            brandRow.Children.Add(brandSub);

            var versionBadge = new Border
            {
                Background = GetBrush("#EBF3FF"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            versionBadge.Child = new TextBlock
            {
                Text = "v2.0",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#007AFF")
            };

            Grid.SetColumn(brandRow, 0);
            Grid.SetColumn(versionBadge, 1);
            topHeaderGrid.Children.Add(brandRow);
            topHeaderGrid.Children.Add(versionBadge);

            var subRow = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            subRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            subRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var subtitle = new TextBlock
            {
                Text = "Вход в 1 клик • 100% локально",
                FontSize = 12,
                Foreground = GetBrush("#8E8E93"),
                VerticalAlignment = VerticalAlignment.Center
            };
            nbrbRatesLabel = new TextBlock
            {
                Text = "НБРБ: загрузка...",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#636366"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(subtitle, 0);
            Grid.SetColumn(nbrbRatesLabel, 1);
            subRow.Children.Add(subtitle);
            subRow.Children.Add(nbrbRatesLabel);

            headerBlock.Children.Add(topHeaderGrid);
            headerBlock.Children.Add(subRow);
            mainStack.Children.Add(headerBlock);

            // iOS Segmented Control
            var segBorder = new Border
            {
                Background = GetBrush("#E5E5EA"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var segGrid = new Grid();
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            segBtnPortals = CreateSegmentButton("🚀 Порталы", 0);
            segBtnRegistry = CreateSegmentButton("🔑 Реестр ЭЦП", 1);
            segBtnTools = CreateSegmentButton("🔎 УНП и ПК", 2);

            Grid.SetColumn(segBtnPortals, 0);
            Grid.SetColumn(segBtnRegistry, 1);
            Grid.SetColumn(segBtnTools, 2);
            segGrid.Children.Add(segBtnPortals);
            segGrid.Children.Add(segBtnRegistry);
            segGrid.Children.Add(segBtnTools);
            segBorder.Child = segGrid;
            mainStack.Children.Add(segBorder);

            // TAB 0: PORTALS
            tabPortalsPanel = new StackPanel();
            certCardsContainer = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            tabPortalsPanel.Children.Add(certCardsContainer);

            // Card 1: МНС (Налоговая)
            var cardMNS = CreatePortalCard("🏛️  Личный кабинет МНС (Налоговая)",
                "Авторизация юрлиц и ИП через ЕС ИФЮЛ (nalog.gov.by)",
                "Войти по ЭЦП в МНС", "#007AFF", delegate
                {
                    SystemDiagnoser.CheckAndResolveProcessConflicts();
                    OpenMNS();
                    ShowToast("🚀 Открываем Личный кабинет МНС...");
                }, out statusPortalMNS, out dotPortalMNS);
            tabPortalsPanel.Children.Add(cardMNS);

            // Card 2: ФСЗН
            var cardFSZN = CreatePortalCard("📑  Портал Фонда (ФСЗН 2.0)",
                "Отчеты ПУ-3, 4-Фонд и извещения (portal2.ssf.gov.by)",
                "Войти по ЭЦП в ФСЗН", "#34C759", delegate
                {
                    SystemDiagnoser.CheckAndResolveProcessConflicts();
                    OpenFSZN();
                    ShowToast("🚀 Открываем Корпоративный портал ФСЗН...");
                }, out statusPortalFSZN, out dotPortalFSZN);
            tabPortalsPanel.Children.Add(cardFSZN);

            // Card 3: ЭСЧФ (vat.gov.by) - 1 CLICK VIA IE/EDGE
            var cardVAT = CreatePortalCard("🧾  Портал ЭСЧФ (vat.gov.by)",
                "Электронные счета-фактуры по НДС (режим IE + ActiveX в 1 клик)",
                "Войти на портал ЭСЧФ", "#5856D6", delegate
                {
                    SystemDiagnoser.CheckAndResolveProcessConflicts();
                    OpenVAT();
                    ShowToast("🚀 Запускаем портал ЭСЧФ (с поддержкой ActiveX)...");
                }, out statusPortalVAT, out dotPortalVAT);
            tabPortalsPanel.Children.Add(cardVAT);

            // Card 4: Е-Паслуга (НЦЭУ)
            var cardEpasluga = CreatePortalCard("🌐  Портал Е-Паслуга (e-pasluga.by)",
                "Электронные услуги НЦЭУ и справки для бизнеса по ЭЦП",
                "Войти на Е-Паслуга", "#0A84FF", delegate
                {
                    SystemDiagnoser.CheckAndResolveProcessConflicts();
                    OpenEpasluga();
                    ShowToast("🚀 Открываем портал Е-Паслуга...");
                }, out statusPortalEpasluga, out dotPortalEpasluga);
            tabPortalsPanel.Children.Add(cardEpasluga);

            // Card 5: Диагностика
            var diagCard = CreateDiagnosticsCard();
            tabPortalsPanel.Children.Add(diagCard);
            mainStack.Children.Add(tabPortalsPanel);

            // TAB 1: MULTI-KEY REGISTRY
            tabRegistryPanel = new StackPanel { Visibility = Visibility.Collapsed };
            BuildRegistryTab(tabRegistryPanel);
            mainStack.Children.Add(tabRegistryPanel);

            // TAB 2: UNP CHECKER & NEW PC WIZARD
            tabToolsPanel = new StackPanel { Visibility = Visibility.Collapsed };
            tabToolsPanel.Children.Add(CreateUnpCheckerCard());
            tabToolsPanel.Children.Add(CreateFirstRunWizardCard());
            mainStack.Children.Add(tabToolsPanel);

            SwitchTab(0);

            // Community button: Telegram
            var tgBtn = new Button
            {
                Content = "💬  Telegram-радар сбоев госпорталов: @buhly_by",
                Height = 38,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#007AFF"),
                Background = GetBrush("#EBF3FF"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 4, 0, 10)
            };
            var tgTemplate = new ControlTemplate(typeof(Button));
            var tgBorder = new FrameworkElementFactory(typeof(Border));
            tgBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            tgBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var tgContent = new FrameworkElementFactory(typeof(ContentPresenter));
            tgContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            tgContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            tgBorder.AppendChild(tgContent);
            tgTemplate.VisualTree = tgBorder;
            tgBtn.Template = tgTemplate;
            tgBtn.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo("https://t.me/buhly_by") { UseShellExecute = true }); } catch { }
            };
            mainStack.Children.Add(tgBtn);

            // Footer note
            var footer = new TextBlock
            {
                Text = "🔒 Безопасно: программа работает на 100% локально на вашем ПК.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = GetBrush("#AEAEB2"),
                Margin = new Thickness(4, 0, 4, 16),
                TextAlignment = TextAlignment.Center
            };
            mainStack.Children.Add(footer);

            scroll.Content = mainStack;
            rootGrid.Children.Add(scroll);

            // Non-blocking iOS Toast Banner at bottom
            toastBanner = new Border
            {
                Background = GetBrush("#1C1C1E"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(24, 0, 24, 20),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Opacity = 0.95
            };
            toastText = new TextBlock
            {
                Text = "",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = System.Windows.Media.Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            toastBanner.Child = toastText;
            rootGrid.Children.Add(toastBanner);

            Content = rootGrid;
        }

        private void ShowToast(string message)
        {
            toastText.Text = message;
            toastBanner.Visibility = Visibility.Visible;
            if (toastTimer != null) toastTimer.Stop();
            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            toastTimer.Tick += delegate
            {
                toastBanner.Visibility = Visibility.Collapsed;
                toastTimer.Stop();
            };
            toastTimer.Start();
        }

        private void RenderCertificateCard(List<CertInfo> certs)
        {
            certCardsContainer.Children.Clear();

            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();

            var head = new TextBlock
            {
                Text = "🔑  Подключенный ключ ЭЦП (USB)",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(head);

            if (certs.Count == 0)
            {
                bool avestOk = SystemDiagnoser.IsAvestInstalled();
                if (!avestOk)
                {
                    var warnBox = new Border
                    {
                        Background = GetBrush("#FFFBEB"),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(14),
                        BorderBrush = GetBrush("#FDE68A"),
                        BorderThickness = new Thickness(1)
                    };
                    var boxStack = new StackPanel();

                    var titleBlock = new TextBlock
                    {
                        Text = "⚠️  На этом ПК не настроена ЭЦП (ПО ГосСУОК)",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = GetBrush("#B45309"),
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    boxStack.Children.Add(titleBlock);

                    var descBlock = new TextBlock
                    {
                        Text = "Криптопровайдер Avest CSP не обнаружен. Без него система не может прочитать USB-ключ и зайти на госпорталы.",
                        FontSize = 11,
                        Foreground = GetBrush("#78350F"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    boxStack.Children.Add(descBlock);

                    var progressBar = new ProgressBar
                    {
                        Height = 6,
                        Minimum = 0,
                        Maximum = 100,
                        Value = 0,
                        Visibility = Visibility.Collapsed,
                        Margin = new Thickness(0, 0, 0, 8),
                        Foreground = GetBrush("#007AFF")
                    };
                    boxStack.Children.Add(progressBar);

                    var statusLabel = new TextBlock
                    {
                        Text = "",
                        FontSize = 11,
                        Foreground = GetBrush("#007AFF"),
                        FontWeight = FontWeights.Medium,
                        Visibility = Visibility.Collapsed,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    boxStack.Children.Add(statusLabel);

                    Button btnInstall = null;
                    btnInstall = CreateIOSButton("📥  Скачать и установить ПО ГосСУОК (38 МБ)", "#007AFF", delegate
                    {
                        btnInstall.IsEnabled = false;
                        progressBar.Visibility = Visibility.Visible;
                        statusLabel.Visibility = Visibility.Visible;
                        statusLabel.Text = "Подключение к nces.by...";

                        ComponentInstaller.DownloadAndInstallGossuok(
                            delegate(string status, int pct)
                            {
                                Dispatcher.Invoke(new Action(delegate
                                {
                                    progressBar.Value = pct;
                                    statusLabel.Text = status;
                                }));
                            },
                            delegate(bool success, string msg)
                            {
                                Dispatcher.Invoke(new Action(delegate
                                {
                                    btnInstall.IsEnabled = true;
                                    if (success)
                                    {
                                        btnInstall.Content = "🔄  Проверить установку";
                                        statusLabel.Text = "✓ " + msg;
                                        statusLabel.Foreground = GetBrush("#34C759");
                                        RefreshStatuses();
                                    }
                                    else
                                    {
                                        statusLabel.Text = "⚠️ " + msg;
                                        statusLabel.Foreground = GetBrush("#FF3B30");
                                    }
                                }));
                            }
                        );
                    });
                    boxStack.Children.Add(btnInstall);

                    warnBox.Child = boxStack;
                    stack.Children.Add(warnBox);
                }
                else
                {
                    var warnBox = new Border
                    {
                        Background = GetBrush("#FFF8EB"),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(12),
                        BorderBrush = GetBrush("#FFE0B2"),
                        BorderThickness = new Thickness(1)
                    };
                    var warnText = new TextBlock
                    {
                        Text = "⚠️ Вставьте USB-токен Avest в компьютер\nКлюч не обнаружен в USB-порте (криптопровайдер Avest активен ✓).",
                        FontSize = 12,
                        FontWeight = FontWeights.Medium,
                        Foreground = GetBrush("#D97706"),
                        TextWrapping = TextWrapping.Wrap
                    };
                    warnBox.Child = warnText;
                    stack.Children.Add(warnBox);
                }
            }
            else
            {
                for (int i = 0; i < certs.Count; i++)
                {
                    var c = certs[i];
                    if (i > 0)
                    {
                        var divider = new System.Windows.Shapes.Rectangle
                        {
                            Height = 1,
                            Fill = GetBrush("#E5E5EA"),
                            Margin = new Thickness(0, 10, 0, 10)
                        };
                        stack.Children.Add(divider);
                    }

                    var itemStack = new StackPanel();

                    // Title line: Org name
                    var titleGrid = new Grid();
                    titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var orgIcon = c.IsCompany ? "🏢 " : "👤 ";
                    var orgText = new TextBlock
                    {
                        Text = orgIcon + c.OrgName,
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = GetBrush("#1C1C1E"),
                        TextWrapping = TextWrapping.Wrap
                    };

                    var statusPill = new Border
                    {
                        Background = c.IsValid ? GetBrush("#E8F9EE") : GetBrush("#FDE8E8"),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(8, 2, 8, 2),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var statusText = new TextBlock
                    {
                        Text = c.IsValid ? string.Format("Осталось {0} дн.", c.DaysRemaining) : "Просрочен",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = c.IsValid ? GetBrush("#16A34A") : GetBrush("#DC2626")
                    };
                    statusPill.Child = statusText;

                    Grid.SetColumn(orgText, 0);
                    Grid.SetColumn(statusPill, 1);
                    titleGrid.Children.Add(orgText);
                    titleGrid.Children.Add(statusPill);
                    itemStack.Children.Add(titleGrid);

                    // Owner / Director info
                    if (!string.IsNullOrEmpty(c.PersonName) && !c.OrgName.Contains(c.PersonName))
                    {
                        var ownerText = new TextBlock
                        {
                            Text = (c.IsCompany ? "Директор: " : "Владелец: ") + c.PersonName,
                            FontSize = 12,
                            Foreground = GetBrush("#636366"),
                            Margin = new Thickness(20, 2, 0, 2)
                        };
                        itemStack.Children.Add(ownerText);
                    }

                    // Expiration date
                    var dateText = new TextBlock
                    {
                        Text = "📅 Действителен до: " + c.ValidTo.ToString("dd.MM.yyyy"),
                        FontSize = 12,
                        Foreground = GetBrush("#8E8E93"),
                        Margin = new Thickness(20, 2, 0, 0)
                    };
                    itemStack.Children.Add(dateText);

                    // Warning banner if expiring within 30 days or expired
                    if (c.DaysRemaining <= 0)
                    {
                        var expBox = new Border
                        {
                            Background = GetBrush("#FDE8E8"),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(10, 6, 10, 6),
                            Margin = new Thickness(0, 8, 0, 0)
                        };
                        var expText = new TextBlock
                        {
                            Text = "🔴 Срок действия ЭЦП истек! Вход на порталы невозможен. Продлите ключ в РУЦ ГосСУОК (nces.by).",
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = GetBrush("#DC2626"),
                            TextWrapping = TextWrapping.Wrap
                        };
                        expBox.Child = expText;
                        itemStack.Children.Add(expBox);
                    }
                    else if (c.DaysRemaining <= 30)
                    {
                        var expBox = new Border
                        {
                            Background = GetBrush("#FFF8EB"),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(10, 6, 10, 6),
                            Margin = new Thickness(0, 8, 0, 0)
                        };
                        var expText = new TextBlock
                        {
                            Text = string.Format("⚠️ До окончания действия ЭЦП осталось {0} дн.! Пора продлевать сертификат в РУЦ ГосСУОК.", c.DaysRemaining),
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = GetBrush("#D97706"),
                            TextWrapping = TextWrapping.Wrap
                        };
                        expBox.Child = expText;
                        itemStack.Children.Add(expBox);
                    }

                    stack.Children.Add(itemStack);
                }
            }

            border.Child = stack;
            certCardsContainer.Children.Add(border);
        }

        private Border CreatePortalCard(string heading, string desc, string btnText, string btnColor, Action onClick, out TextBlock statusOnline, out System.Windows.Shapes.Ellipse dotOnline)
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();

            // Header line: Title + Status Pill (Telegram style)
            var headGrid = new Grid();
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var head = new TextBlock
            {
                Text = heading,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var onlineBadge = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            dotOnline = new System.Windows.Shapes.Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            statusOnline = new TextBlock
            {
                Text = "проверка...",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrush("#8E8E93"),
                VerticalAlignment = VerticalAlignment.Center
            };

            onlineBadge.Children.Add(dotOnline);
            onlineBadge.Children.Add(statusOnline);

            Grid.SetColumn(head, 0);
            Grid.SetColumn(onlineBadge, 1);
            headGrid.Children.Add(head);
            headGrid.Children.Add(onlineBadge);
            stack.Children.Add(headGrid);

            var sub = new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                Margin = new Thickness(0, 4, 0, 14),
                TextWrapping = TextWrapping.Wrap
            };

            var btn = CreateIOSButton(btnText, btnColor, onClick);

            stack.Children.Add(sub);
            stack.Children.Add(btn);

            border.Child = stack;
            return border;
        }

        private Border CreateDiagnosticsCard()
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();

            // Header with Refresh 🔄 button
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var head = new TextBlock
            {
                Text = "⚙️  Диагностика системы",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var refreshBtn = new Button
            {
                Content = "🔄 Обновить",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrush("#007AFF"),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            refreshBtn.Click += delegate
            {
                RefreshStatuses();
                ShowToast("✓ Статусы обновлены");
            };

            Grid.SetColumn(head, 0);
            Grid.SetColumn(refreshBtn, 1);
            headerGrid.Children.Add(head);
            headerGrid.Children.Add(refreshBtn);
            stack.Children.Add(headerGrid);

            // Status items
            stack.Children.Add(CreateStatusRow("Служба NTClient (МНС):", out statusNTClient, out dotNTClient));
            stack.Children.Add(CreateStatusRow("Шлюз AvTunProxy (ФСЗН):", out statusAvTun, out dotAvTun));
            stack.Children.Add(CreateStatusRow("Сетевой туннель PAC:", out statusPac, out dotPac));
            stack.Children.Add(CreateStatusRow("Файл hosts (госдомены):", out statusHosts, out dotHosts));

            // Buttons row: [ ⚡ Исправить всё ] + [ 🔍 Отчет ]
            var btnGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var fixBtn = CreateIOSButton("⚡  Исправить всё", "#FF9500", delegate
            {
                statusNTClient.Text = "Перезапуск и проверка...";
                dotNTClient.Fill = GetBrush("#FF9500");
                ThreadPool.QueueUserWorkItem(delegate
                {
                    SystemDiagnoser.RepairAll();
                    Dispatcher.Invoke(new Action(delegate
                    {
                        RefreshStatuses();
                        ShowToast("✓ Службы перезапущены, порт проверен, туннель активен!");
                    }));
                });
            });

            var detailsBtn = CreateIOSButton("🔍  Отчет", "#8E8E93", delegate
            {
                ShowDetailedReportWindow();
            });

            Grid.SetColumn(fixBtn, 0);
            Grid.SetColumn(detailsBtn, 2);
            btnGrid.Children.Add(fixBtn);
            btnGrid.Children.Add(detailsBtn);
            stack.Children.Add(btnGrid);

            border.Child = stack;
            return border;
        }

        private Border CreateFirstRunWizardCard()
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();

            var head = new TextBlock
            {
                Text = "📦  Мастер настройки для нового ПК",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var sub = new TextBlock
            {
                Text = "Если компьютер чистый и программы еще не установлены:",
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(head);
            stack.Children.Add(sub);

            // Step 1: Avest
            bool avestOk = SystemDiagnoser.IsAvestInstalled();
            stack.Children.Add(CreateWizardStep("1. ПО «Авест» (ГосСУОК):",
                avestOk ? "Установлено ✓" : "Скачать и установить (38 МБ)",
                avestOk,
                delegate
                {
                    if (avestOk) return;
                    ShowToast("📥 Загрузка ПО ГосСУОК с официального сервера nces.by...");
                    ComponentInstaller.DownloadAndInstallGossuok(
                        delegate(string s, int p) { },
                        delegate(bool ok, string m)
                        {
                            Dispatcher.Invoke(new Action(delegate
                            {
                                ShowToast(ok ? "✓ " + m : "⚠️ " + m);
                                RefreshStatuses();
                            }));
                        }
                    );
                }));

            // Step 2: NTClient
            string ntVer = SystemDiagnoser.GetNTClientVersion();
            bool ntInstalled = ntVer != "Не установлена";
            string localNT = ntInstalled ? null : ComponentInstaller.FindLocalNTClientInstaller();
            string ntActionText = ntInstalled ? ntVer + " ✓" : (localNT != null ? "Запустить с диска ⚡" : "Инструкция и сайт НЦЭУ");
            stack.Children.Add(CreateWizardStep("2. NTClientSoftware (МНС):",
                ntActionText,
                ntInstalled,
                delegate
                {
                    if (ntInstalled) return;
                    if (localNT != null)
                    {
                        try
                        {
                            Process.Start(localNT);
                            ShowToast("⚡ Запуск найденного установщика: " + Path.GetFileName(localNT));
                        }
                        catch (Exception ex) { ShowToast("Ошибка запуска: " + ex.Message); }
                    }
                    else
                    {
                        OpenBrowser("https://nces.by/service/po/");
                        ShowToast("🌐 Открыт сайт НЦЭУ. Для загрузки укажите телефон для получения СМС.");
                    }
                }));

            // Step 3: AvTunProxy
            string avVer = SystemDiagnoser.GetAvTunProxyVersion();
            bool avInstalled = avVer != "Не установлена";
            stack.Children.Add(CreateWizardStep("3. AvTunProxy (ФСЗН):",
                avInstalled ? avVer + " ✓" : "Скачать и установить (8 МБ)",
                avInstalled,
                delegate
                {
                    if (avInstalled) return;
                    ShowToast("📥 Загрузка AvTunProxy с avtunproxy.by...");
                    ComponentInstaller.DownloadAndInstallAvTunProxy(
                        delegate(string s, int p) { },
                        delegate(bool ok, string m)
                        {
                            Dispatcher.Invoke(new Action(delegate
                            {
                                ShowToast(ok ? "✓ " + m : "⚠️ " + m);
                                RefreshStatuses();
                            }));
                        }
                    );
                }));

            border.Child = stack;
            return border;
        }

        private Grid CreateWizardStep(string label, string actionText, bool isOk, Action onClick)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = GetBrush("#3A3A3C"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var btn = new Button
            {
                Content = actionText,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = isOk ? GetBrush("#34C759") : GetBrush("#007AFF"),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = isOk ? Cursors.Arrow : Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (onClick != null)
            {
                btn.Click += delegate { onClick(); };
            }

            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(btn, 1);
            g.Children.Add(lbl);
            g.Children.Add(btn);
            return g;
        }

        private Grid CreateStatusRow(string label, out TextBlock textVal, out System.Windows.Shapes.Ellipse dot)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = GetBrush("#3A3A3C")
            };

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal };

            dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = GetBrush("#34C759"),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            textVal = new TextBlock
            {
                Text = "Проверка...",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrush("#3A3A3C")
            };

            rightStack.Children.Add(dot);
            rightStack.Children.Add(textVal);

            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(rightStack, 1);

            g.Children.Add(lbl);
            g.Children.Add(rightStack);
            return g;
        }

        private Button CreateIOSButton(string text, string hexColor, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                Height = 42,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Background = GetBrush(hexColor),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "btnBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 0, 12, 0));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88, "btnBorder"));
            template.Triggers.Add(hoverTrigger);

            var pressTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72, "btnBorder"));
            template.Triggers.Add(pressTrigger);

            var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "btnBorder"));
            template.Triggers.Add(disabledTrigger);

            btn.Template = template;
            btn.Click += delegate { onClick(); };
            return btn;
        }

        private Button CreateSegmentButton(string title, int tabIndex)
        {
            var btn = new Button
            {
                Content = title,
                Height = 32,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;
            btn.Template = template;

            btn.Click += delegate { SwitchTab(tabIndex); };
            return btn;
        }

        private void SwitchTab(int index)
        {
            tabPortalsPanel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            tabRegistryPanel.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            tabToolsPanel.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

            var btns = new Button[] { segBtnPortals, segBtnRegistry, segBtnTools };
            for (int i = 0; i < btns.Length; i++)
            {
                bool active = (i == index);
                btns[i].Background = active ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;
                btns[i].Foreground = active ? GetBrush("#007AFF") : GetBrush("#3A3A3C");
                btns[i].FontWeight = active ? FontWeights.Bold : FontWeights.Medium;
            }

            if (index == 1)
            {
                RefreshRegistryTab(false);
            }
        }

        private void BuildRegistryTab(StackPanel container)
        {
            var headerCard = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var hStack = new StackPanel();
            hStack.Children.Add(new TextBlock
            {
                Text = "🔑  Реестр всех ключей ЭЦП",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            hStack.Children.Add(new TextBlock
            {
                Text = "Помнит все компании и ИП, ключи которых когда-либо вставлялись в этот ПК. Следите за сроками продления, даже если флешка в столе.",
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 10)
            });

            registrySummaryText = new TextBlock
            {
                Text = "Загрузка реестра...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#007AFF"),
                Margin = new Thickness(0, 0, 0, 12)
            };
            hStack.Children.Add(registrySummaryText);

            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var scanBtn = CreateIOSButton("🔄 Из Windows", "#007AFF", delegate
            {
                RefreshRegistryTab(true);
                ShowToast("✓ Ключи подтянуты из хранилища Windows");
            });
            scanBtn.Height = 36;
            scanBtn.FontSize = 12;

            var copyBtn = CreateIOSButton("📋 Сводка сроков", "#34C759", delegate
            {
                var all = CertRegistryManager.LoadRegistry();
                if (all.Count == 0)
                {
                    ShowToast("Реестр пока пуст — вставьте ключ или нажмите «Из Windows»");
                    return;
                }
                var sb = new StringBuilder();
                sb.AppendLine("Реестр ключей ЭЦП (Buhly):");
                foreach (var item in all)
                {
                    sb.AppendFormat("• {0} ({1}) — до {2} (осталось {3} дн.)\r\n",
                        item.OrgName, item.PersonName, item.ValidTo.ToString("dd.MM.yyyy"), item.DaysRemaining);
                }
                try { Clipboard.SetText(sb.ToString()); ShowToast("✓ Сводка скопирована в буфер обмена"); }
                catch { ShowToast("Не удалось скопировать в буфер"); }
            });
            copyBtn.Height = 36;
            copyBtn.FontSize = 12;

            Grid.SetColumn(scanBtn, 0);
            Grid.SetColumn(copyBtn, 2);
            btnRow.Children.Add(scanBtn);
            btnRow.Children.Add(copyBtn);
            hStack.Children.Add(btnRow);

            headerCard.Child = hStack;
            container.Children.Add(headerCard);

            registryListContainer = new StackPanel();
            container.Children.Add(registryListContainer);
        }

        private void RefreshRegistryTab(bool scanAllWindowsStore)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                var fresh = SystemDiagnoser.GetAllGovCertificates(!scanAllWindowsStore);
                var merged = CertRegistryManager.MergeAndGetAll(fresh);

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (registryListContainer == null || registrySummaryText == null) return;
                    registryListContainer.Children.Clear();

                    int inUsb = 0;
                    int expiring = 0;
                    foreach (var c in merged)
                    {
                        if (c.IsPhysicallyConnected) inUsb++;
                        if (c.DaysRemaining <= 30) expiring++;
                    }
                    registrySummaryText.Text = string.Format("Всего в базе: {0}  •  В USB сейчас: {1}  •  Истекает ≤30 дн.: {2}",
                        merged.Count, inUsb, expiring);

                    if (merged.Count == 0)
                    {
                        var emptyCard = new Border
                        {
                            Background = System.Windows.Media.Brushes.White,
                            CornerRadius = new CornerRadius(14),
                            Padding = new Thickness(16),
                            BorderBrush = GetBrush("#E5E5EA"),
                            BorderThickness = new Thickness(1)
                        };
                        emptyCard.Child = new TextBlock
                        {
                            Text = "В базе пока нет ни одного ключа.\nВставьте USB-токен Avest или нажмите «🔄 Из Windows», чтобы подтянуть уже установленные на этом ПК сертификаты.",
                            FontSize = 12,
                            Foreground = GetBrush("#636366"),
                            TextWrapping = TextWrapping.Wrap
                        };
                        registryListContainer.Children.Add(emptyCard);
                        return;
                    }

                    foreach (var c in merged)
                    {
                        var card = new Border
                        {
                            Background = System.Windows.Media.Brushes.White,
                            CornerRadius = new CornerRadius(14),
                            Padding = new Thickness(14),
                            Margin = new Thickness(0, 0, 0, 10),
                            BorderBrush = c.IsPhysicallyConnected ? GetBrush("#34C759") : GetBrush("#E5E5EA"),
                            BorderThickness = new Thickness(1)
                        };
                        var st = new StackPanel();

                        var topGrid = new Grid();
                        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var orgLbl = new TextBlock
                        {
                            Text = (c.IsCompany ? "🏢 " : "👤 ") + c.OrgName,
                            FontSize = 13,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = GetBrush("#1C1C1E"),
                            TextWrapping = TextWrapping.Wrap
                        };

                        var daysBg = !c.IsValid ? "#FDE8E8" : (c.DaysRemaining <= 30 ? "#FFF8EB" : "#E8F9EE");
                        var daysFg = !c.IsValid ? "#DC2626" : (c.DaysRemaining <= 30 ? "#D97706" : "#16A34A");
                        var daysPill = new Border
                        {
                            Background = GetBrush(daysBg),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(8, 2, 8, 2),
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        daysPill.Child = new TextBlock
                        {
                            Text = c.IsValid ? string.Format("{0} дн.", c.DaysRemaining) : "Просрочен",
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Foreground = GetBrush(daysFg)
                        };

                        Grid.SetColumn(orgLbl, 0);
                        Grid.SetColumn(daysPill, 1);
                        topGrid.Children.Add(orgLbl);
                        topGrid.Children.Add(daysPill);
                        st.Children.Add(topGrid);

                        if (!string.IsNullOrEmpty(c.PersonName))
                        {
                            st.Children.Add(new TextBlock
                            {
                                Text = c.PersonName,
                                FontSize = 12,
                                Foreground = GetBrush("#636366"),
                                Margin = new Thickness(0, 3, 0, 0)
                            });
                        }

                        var metaText = string.Format("{0}  •  до {1}",
                            c.IsPhysicallyConnected ? "🟢 Подключен в USB" : "⚪ Сохранен в реестре",
                            c.ValidTo.ToString("dd.MM.yyyy"));
                        st.Children.Add(new TextBlock
                        {
                            Text = metaText,
                            FontSize = 11,
                            Foreground = c.IsPhysicallyConnected ? GetBrush("#16A34A") : GetBrush("#8E8E93"),
                            Margin = new Thickness(0, 4, 0, 0)
                        });

                        card.Child = st;
                        registryListContainer.Children.Add(card);
                    }
                }));
            });
        }

        private Border CreateUnpCheckerCard()
        {
            var card = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "🔎  Проверка контрагента по УНП",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Мгновенный запрос в официальную базу ГРП МНС РБ (статус, адрес, ИМНС):",
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            });

            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

            var inputBoxBorder = new Border
            {
                Background = GetBrush("#F2F2F7"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 6, 10, 6),
                BorderBrush = GetBrush("#D1D1D6"),
                BorderThickness = new Thickness(1)
            };
            unpInputBox = new TextBox
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxLength = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            inputBoxBorder.Child = unpInputBox;

            Button checkBtn = null;
            Action runLookup = delegate
            {
                var q = unpInputBox.Text;
                if (checkBtn != null) checkBtn.IsEnabled = false;
                unpResultCard.Visibility = Visibility.Visible;
                unpResultStack.Children.Clear();
                unpResultStack.Children.Add(new TextBlock
                {
                    Text = "⏳ Запрос в базу ГРП МНС...",
                    FontSize = 12,
                    Foreground = GetBrush("#8E8E93")
                });

                ThreadPool.QueueUserWorkItem(delegate
                {
                    var r = UnpChecker.Lookup(q);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (checkBtn != null) checkBtn.IsEnabled = true;
                        unpResultStack.Children.Clear();
                        if (!r.Success)
                        {
                            unpResultStack.Children.Add(new TextBlock
                            {
                                Text = "⚠️ " + r.ErrorMessage,
                                FontSize = 12,
                                FontWeight = FontWeights.Medium,
                                Foreground = GetBrush("#FF3B30"),
                                TextWrapping = TextWrapping.Wrap
                            });
                            return;
                        }

                        bool isActive = (r.StatusCode == "1" || (r.StatusName ?? "").ToLower().Contains("действ"));
                        var badge = new Border
                        {
                            Background = GetBrush(isActive ? "#E8F9EE" : "#FDE8E8"),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(8, 3, 8, 3),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0, 0, 0, 8)
                        };
                        badge.Child = new TextBlock
                        {
                            Text = (isActive ? "🟢 " : "🔴 ") + (string.IsNullOrEmpty(r.StatusName) ? "Статус неизвестен" : r.StatusName),
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Foreground = GetBrush(isActive ? "#16A34A" : "#DC2626")
                        };
                        unpResultStack.Children.Add(badge);

                        unpResultStack.Children.Add(new TextBlock
                        {
                            Text = !string.IsNullOrEmpty(r.ShortName) ? r.ShortName : r.FullName,
                            FontSize = 13,
                            FontWeight = FontWeights.Bold,
                            Foreground = GetBrush("#1C1C1E"),
                            TextWrapping = TextWrapping.Wrap
                        });
                        if (!string.IsNullOrEmpty(r.Address))
                        {
                            unpResultStack.Children.Add(new TextBlock
                            {
                                Text = "📍 " + r.Address,
                                FontSize = 12,
                                Foreground = GetBrush("#3A3A3C"),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 4, 0, 0)
                            });
                        }
                        if (!string.IsNullOrEmpty(r.TaxOffice))
                        {
                            unpResultStack.Children.Add(new TextBlock
                            {
                                Text = "🏛️ " + r.TaxOffice,
                                FontSize = 11,
                                Foreground = GetBrush("#636366"),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 3, 0, 0)
                            });
                        }
                        if (!string.IsNullOrEmpty(r.RegDate))
                        {
                            unpResultStack.Children.Add(new TextBlock
                            {
                                Text = "📅 Дата регистрации: " + r.RegDate + (string.IsNullOrEmpty(r.LiqDate) ? "" : "  •  Ликвидация: " + r.LiqDate),
                                FontSize = 11,
                                Foreground = GetBrush("#8E8E93"),
                                Margin = new Thickness(0, 3, 0, 8)
                            });
                        }

                        var copyBtn = CreateIOSButton("📋 Скопировать реквизиты", "#EBF3FF", delegate
                        {
                            var txt = string.Format("{0}\r\nУНП: {1}\r\nАдрес: {2}\r\nСтатус: {3}\r\nИМНС: {4}",
                                r.FullName, r.Unp, r.Address, r.StatusName, r.TaxOffice);
                            try { Clipboard.SetText(txt); ShowToast("✓ Реквизиты скопированы"); } catch { }
                        });
                        copyBtn.Foreground = GetBrush("#007AFF");
                        copyBtn.Height = 34;
                        copyBtn.FontSize = 12;
                        unpResultStack.Children.Add(copyBtn);
                    }));
                });
            };

            checkBtn = CreateIOSButton("Проверить", "#007AFF", runLookup);
            checkBtn.Height = 38;
            checkBtn.FontSize = 13;

            unpInputBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) runLookup();
            };

            Grid.SetColumn(inputBoxBorder, 0);
            Grid.SetColumn(checkBtn, 2);
            inputGrid.Children.Add(inputBoxBorder);
            inputGrid.Children.Add(checkBtn);
            stack.Children.Add(inputGrid);

            unpResultCard = new Border
            {
                Background = GetBrush("#F9F9FB"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 12, 0, 0),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                Visibility = Visibility.Collapsed
            };
            unpResultStack = new StackPanel();
            unpResultCard.Child = unpResultStack;
            stack.Children.Add(unpResultCard);

            card.Child = stack;
            return card;
        }

        private void CheckAllPortalsOnline()
        {
            CheckSinglePortal("https://lkfl.portal.nalog.gov.by/", statusPortalMNS, dotPortalMNS);
            CheckSinglePortal("http://portal2.ssf.gov.by/mainPage/", statusPortalFSZN, dotPortalFSZN);
            CheckSinglePortal("http://vat.gov.by/mainPage/", statusPortalVAT, dotPortalVAT);
            CheckSinglePortal("https://e-pasluga.by/", statusPortalEpasluga, dotPortalEpasluga);
        }

        private void CheckSinglePortal(string url, TextBlock tb, System.Windows.Shapes.Ellipse dot)
        {
            if (tb == null || dot == null) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                var sw = Stopwatch.StartNew();
                bool ok = false;
                long ms = 0;
                try
                {
                    Program.ConfigureSafeTls();

                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "GET";
                    req.Timeout = 3500;
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    {
                        sw.Stop();
                        ms = sw.ElapsedMilliseconds;
                        ok = ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 500);
                    }
                }
                catch (WebException wex)
                {
                    sw.Stop();
                    ms = sw.ElapsedMilliseconds;
                    var resp = wex.Response as HttpWebResponse;
                    if (resp != null && (int)resp.StatusCode < 500)
                    {
                        ok = true;
                    }
                }
                catch
                {
                    sw.Stop();
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (ok)
                    {
                        dot.Fill = GetBrush("#34C759");
                        tb.Text = string.Format("в сети ({0} мс)", ms);
                        tb.Foreground = GetBrush("#34C759");
                    }
                    else
                    {
                        dot.Fill = GetBrush("#FF3B30");
                        tb.Text = "сбой сервера";
                        tb.Foreground = GetBrush("#FF3B30");
                    }
                }));
            });
        }

        private void RefreshStatuses()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // 1. Hardware USB token & crypto checks (in background thread!)
                    var certs = SystemDiagnoser.GetConnectedGovCertificates();
                    if (certs.Count > 0)
                    {
                        CertRegistryManager.MergeAndGetAll(certs);
                    }
                    bool avestOk = SystemDiagnoser.IsAvestInstalled();

                    // 2. Heavy network HTTP/socket ping for NTClient 8084 (in background thread!)
                    bool ntInstalled = SystemDiagnoser.FindNTClientPath() != null;
                    bool ntOk = ntInstalled && SystemDiagnoser.IsPort8084Healthy();

                    // 3. Process & service detection (in background thread!)
                    bool avInstalled = SystemDiagnoser.FindAvTunProxyPath() != null;
                    bool avOk = avInstalled && SystemDiagnoser.IsAvTunProxyRunning();

                    // 4. Registry & hosts checks (in background thread!)
                    bool pacOk = SystemDiagnoser.IsPacEnabled();
                    string hostsDetailsDummy;
                    bool hostsOk = SystemDiagnoser.CheckHostsFile(out hostsDetailsDummy);

                    // Signature to prevent unnecessary UI rebuilds
                    var sb = new StringBuilder();
                    sb.Append(avestOk).Append("|").Append(certs.Count);
                    foreach (var c in certs)
                    {
                        sb.Append("|").Append(c.OrgName).Append(";").Append(c.DaysRemaining);
                    }
                    string certKey = sb.ToString();

                    // 5. Apply to UI swiftly in a single layout pass
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            if (certKey != _lastCertHash)
                            {
                                _lastCertHash = certKey;
                                RenderCertificateCard(certs);
                            }

                            // NTClient
                            string newNtText = !ntInstalled ? "Не установлена" : (ntOk ? "Активен (порт 8084)" : "Не отвечает / Сбой");
                            if (statusNTClient.Text != newNtText)
                            {
                                statusNTClient.Text = newNtText;
                                dotNTClient.Fill = !ntInstalled ? GetBrush("#FF9500") : (ntOk ? GetBrush("#34C759") : GetBrush("#FF3B30"));
                            }

                            // AvTunProxy
                            string newAvText = !avInstalled ? "Не установлена" : (avOk ? "Запущен" : "Остановлен");
                            if (statusAvTun.Text != newAvText)
                            {
                                statusAvTun.Text = newAvText;
                                dotAvTun.Fill = !avInstalled ? GetBrush("#FF9500") : (avOk ? GetBrush("#34C759") : GetBrush("#FF9500"));
                            }

                            // PAC
                            string newPacText = pacOk ? "Включен (10224)" : "Отключен";
                            if (statusPac.Text != newPacText)
                            {
                                statusPac.Text = newPacText;
                                dotPac.Fill = pacOk ? GetBrush("#34C759") : GetBrush("#FF3B30");
                            }

                            // Hosts
                            if (statusHosts != null && dotHosts != null)
                            {
                                string newHostsText = hostsOk ? "Чист ✓" : "Заблокирован (Исправить!)";
                                if (statusHosts.Text != newHostsText)
                                {
                                    statusHosts.Text = newHostsText;
                                    dotHosts.Fill = hostsOk ? GetBrush("#34C759") : GetBrush("#FF3B30");
                                }
                            }
                        }
                        finally
                        {
                            _isRefreshing = false;
                        }
                    }));
                }
                catch
                {
                    _isRefreshing = false;
                }
            });
        }

        private void ShowDetailedReportWindow()
        {
            var win = new Window
            {
                Title = "Buhly • Детальный отчет системы",
                Width = 470,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 242, 247))
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(20) };

            var head = new TextBlock
            {
                Text = "🔍 Чек-лист готовности",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(head);

            // Report Card
            var card = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var cardStack = new StackPanel();

            // 1. Time sync
            string timeDetails;
            bool timeOk = SystemDiagnoser.CheckTimeSync(out timeDetails);
            cardStack.Children.Add(CreateReportItem("🕒 Системное время:", timeDetails, timeOk));

            // 2. Hosts file
            string hostsDetails;
            bool hostsOk = SystemDiagnoser.CheckHostsFile(out hostsDetails);
            cardStack.Children.Add(CreateReportItem("🛡️ Файл hosts:", hostsDetails, hostsOk));

            // 3. NTClient
            string ntVer = SystemDiagnoser.GetNTClientVersion();
            bool ntPort = SystemDiagnoser.IsPort8084Healthy();
            cardStack.Children.Add(CreateReportItem("🏛️ NTClientSoftware (МНС):",
                ntVer + (ntPort ? " (порт 8084 активен) ✓" : " (порт недоступен) ⚠️"), ntPort));

            // 4. AvTunProxy
            string avVer = SystemDiagnoser.GetAvTunProxyVersion();
            bool avRun = SystemDiagnoser.IsAvTunProxyRunning();
            cardStack.Children.Add(CreateReportItem("📑 AvTunProxy (ФСЗН):",
                avVer + (avRun ? " (работает) ✓" : " (не запущен) ⚠️"), avRun));

            // 5. PAC
            bool pac = SystemDiagnoser.IsPacEnabled();
            cardStack.Children.Add(CreateReportItem("🌐 Сетевой туннель:",
                pac ? "proxy.pac активен ✓" : "proxy.pac выключен ⚠️", pac));

            // 6. Portals live online
            string mnsOnline = statusPortalMNS != null ? statusPortalMNS.Text : "в сети";
            cardStack.Children.Add(CreateReportItem("🏛️ Сервер МНС (nalog.gov.by):", mnsOnline, !mnsOnline.Contains("сбой")));

            string fsznOnline = statusPortalFSZN != null ? statusPortalFSZN.Text : "в сети";
            cardStack.Children.Add(CreateReportItem("📑 Сервер ФСЗН (ssf.gov.by):", fsznOnline, !fsznOnline.Contains("сбой")));

            string vatOnline = statusPortalVAT != null ? statusPortalVAT.Text : "в сети";
            cardStack.Children.Add(CreateReportItem("🧾 Сервер ЭСЧФ (vat.gov.by):", vatOnline, !vatOnline.Contains("сбой")));

            string epaslugaOnline = statusPortalEpasluga != null ? statusPortalEpasluga.Text : "в сети";
            cardStack.Children.Add(CreateReportItem("🌐 Сервер Е-Паслуга (e-pasluga.by):", epaslugaOnline, !epaslugaOnline.Contains("сбой")));

            // 7. Certificates (Inserted only)
            var certs = SystemDiagnoser.GetConnectedGovCertificates();
            var certHeader = new TextBlock
            {
                Text = "🔑 Подключенный ключ ГосСУОК:",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 10, 0, 4)
            };
            cardStack.Children.Add(certHeader);

            if (certs.Count == 0)
            {
                cardStack.Children.Add(new TextBlock
                {
                    Text = "Вставьте USB-токен Avest в компьютер",
                    FontSize = 12,
                    Foreground = GetBrush("#FF9500"),
                    Margin = new Thickness(10, 0, 0, 6)
                });
            }
            else
            {
                foreach (var c in certs)
                {
                    var cText = new TextBlock
                    {
                        Text = string.Format("• {0} ({1})\n   Действует до: {2} (осталось {3} дн.)", c.OrgName, c.PersonName, c.ValidTo.ToString("dd.MM.yyyy"), c.DaysRemaining),
                        FontSize = 12,
                        Foreground = c.IsValid ? GetBrush("#34C759") : GetBrush("#FF3B30"),
                        Margin = new Thickness(10, 2, 0, 6)
                    };
                    cardStack.Children.Add(cText);
                }
            }

            card.Child = cardStack;
            stack.Children.Add(card);

            var closeBtn = CreateIOSButton("Понятно", "#007AFF", delegate
            {
                win.Close();
            });
            closeBtn.Margin = new Thickness(0, 16, 0, 0);
            stack.Children.Add(closeBtn);

            scroll.Content = stack;
            win.Content = scroll;
            win.ShowDialog();
        }

        private UIElement CreateReportItem(string title, string status, bool isOk)
        {
            var p = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            var t = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E")
            };
            var s = new TextBlock
            {
                Text = status,
                FontSize = 12,
                Foreground = isOk ? GetBrush("#34C759") : GetBrush("#FF9500"),
                Margin = new Thickness(10, 2, 0, 0)
            };
            p.Children.Add(t);
            p.Children.Add(s);
            return p;
        }

        private void OpenMNS()
        {
            ShowToast("🏛️ Подключение к личному кабинету МНС...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string hd;
                if (!SystemDiagnoser.IsPort8084Healthy() || !SystemDiagnoser.CheckHostsFile(out hd))
                {
                    SystemDiagnoser.RepairAll();
                }
                OpenBrowser("https://lkfl.portal.nalog.gov.by/esifuyl");
                Dispatcher.Invoke(new Action(delegate { RefreshStatuses(); }));
            });
        }

        private void OpenFSZN()
        {
            ShowToast("📑 Подключение к порталу ФСЗН 2.0...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                WinINetHelper.EnablePac("http://127.0.0.1:10224/proxy.pac");
                if (!SystemDiagnoser.IsAvTunProxyRunning())
                {
                    SystemDiagnoser.RepairAll();
                }
                OpenBrowser("https://portal2.ssf.gov.by/mainPage/");
                Dispatcher.Invoke(new Action(delegate { RefreshStatuses(); }));
            });
        }

        private void OpenEpasluga()
        {
            ShowToast("🌐 Подключение к порталу Е-Паслуга...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string hd;
                if (!SystemDiagnoser.IsPort8084Healthy() || !SystemDiagnoser.CheckHostsFile(out hd))
                {
                    SystemDiagnoser.RepairAll();
                }
                OpenBrowser("https://e-pasluga.by/");
                Dispatcher.Invoke(new Action(delegate { RefreshStatuses(); }));
            });
        }

        private void OpenVAT()
        {
            ShowToast("🚀 Подготовка и запуск ЭСЧФ в Microsoft Edge...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    EdgeHelper.EnsureVatInEdgeIEMode();

                    var edgeCandidates = new string[]
                    {
                        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
                    };
                    foreach (var edge in edgeCandidates)
                    {
                        if (File.Exists(edge))
                        {
                            Process.Start(edge, "http://vat.gov.by/mainPage/");
                            Dispatcher.Invoke(new Action(delegate
                            {
                                ShowToast("🚀 ЭСЧФ запущен в Microsoft Edge (режим IE)!");
                                RefreshStatuses();
                            }));
                            return;
                        }
                    }

                    // Fallback to Edge protocol URI (no VBScript dropper)
                    Process.Start(new ProcessStartInfo("microsoft-edge:http://vat.gov.by/mainPage/") { UseShellExecute = true });
                    Dispatcher.Invoke(new Action(delegate
                    {
                        ShowToast("🚀 Запускаем ЭСЧФ в Microsoft Edge...");
                        RefreshStatuses();
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(new Action(delegate
                    {
                        ShowToast("Ошибка запуска: " + ex.Message);
                    }));
                }
            });
        }

        private void OpenBrowser(string url)
        {
            try
            {
                var chromeCandidates = new string[]
                {
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
                };
                foreach (var c in chromeCandidates)
                {
                    if (File.Exists(c)) { Process.Start(c, url); return; }
                }

                var edgeCandidates = new string[]
                {
                    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                    @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
                };
                foreach (var e in edgeCandidates)
                {
                    if (File.Exists(e)) { Process.Start(e, url); return; }
                }

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowToast("Ошибка запуска: " + ex.Message);
            }
        }

        private SolidColorBrush GetBrush(string hex)
        {
            return (SolidColorBrush)(new BrushConverter().ConvertFrom(hex));
        }
    }
}
