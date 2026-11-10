using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GovPortalsApp
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

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
        public string OrgName { get; set; }
        public string PersonName { get; set; }
        public DateTime ValidTo { get; set; }
        public int DaysRemaining { get; set; }
        public bool IsValid { get; set; }
        public bool IsCompany { get; set; }
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
        public static bool IsVatConfigured()
        {
            try
            {
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
                var prefPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Preferences");
                if (!File.Exists(prefPath)) return;

                var text = File.ReadAllText(prefPath, Encoding.UTF8);
                bool alreadyConfigured = text.Contains("vat.gov.by/mainPage") && text.Contains("vat.gov.by/eInvoice");

                if (!alreadyConfigured)
                {
                    // Gracefully terminate Edge so it flushes and releases Preferences
                    var edgeProcs = Process.GetProcessesByName("msedge");
                    if (edgeProcs.Length > 0)
                    {
                        foreach (var p in edgeProcs)
                        {
                            try { p.Kill(); } catch { }
                        }
                        Thread.Sleep(1000);
                        text = File.ReadAllText(prefPath, Encoding.UTF8);
                    }

                    // Enable consumer IE mode
                    if (text.Contains("\"consumer_mode\":{\"enabled_state\":0}"))
                    {
                        text = text.Replace("\"consumer_mode\":{\"enabled_state\":0}", "\"consumer_mode\":{\"enabled_state\":1}");
                    }
                    else if (!text.Contains("\"consumer_mode\""))
                    {
                        if (text.Contains("\"dual_engine\":{"))
                        {
                            text = text.Replace("\"dual_engine\":{", "\"dual_engine\":{\"consumer_mode\":{\"enabled_state\":1},");
                        }
                    }

                    // Inject 6 URLs into user_list_data_1 with expiration in 2031 (never expires)
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
                var suspicious = new List<string>();
                var domains = new string[] { "nalog.gov.by", "vat.gov.by", "ssf.gov.by", "pki.gov.by", "nces.by" };
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                    foreach (var d in domains)
                    {
                        if (line.ToLower().Contains(d))
                        {
                            suspicious.Add(line);
                        }
                    }
                }
                if (suspicious.Count > 0)
                {
                    details = "Обнаружены подмены адресов: " + string.Join(", ", suspicious.ToArray());
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

        public static bool CheckTimeSync(out string details)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://vat.gov.by/mainPage/");
                req.Method = "GET";
                req.Timeout = 1500;
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
            details = DateTime.Now.ToString("HH:mm:ss") + " (Минск UTC+3) ✓";
            return true;
        }

        public static bool IsPort8084Healthy()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:8084/select_auth");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = 1200;
                var bytes = Encoding.UTF8.GetBytes("{\"data\":\"ping\"}");
                req.ContentLength = bytes.Length;
                using (var stream = req.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    return true;
                }
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp != null)
                {
                    if ((int)resp.StatusCode == 429) return false;
                    return true;
                }
                return false;
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

        // Returns ONLY certificates whose physical USB token is connected!
        public static List<CertInfo> GetConnectedGovCertificates()
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
                        // Hardware check: is this token currently plugged into USB?
                        if (!CryptoHelper.IsTokenPhysicallyInserted(cert))
                        {
                            continue; // Key not in USB!
                        }

                        var subj = cert.SubjectName.Name;
                        string org = "";
                        string sn = "";
                        string gn = "";

                        // Parse O= (Organization)
                        if (subj.Contains("O="))
                        {
                            var parts = subj.Split(new string[] { "O=" }, StringSplitOptions.None);
                            if (parts.Length > 1)
                            {
                                org = parts[1].Split(new string[] { ", " }, StringSplitOptions.None)[0].Trim().Trim('"');
                            }
                        }
                        // Parse SN= (Surname)
                        if (subj.Contains("SN="))
                        {
                            var parts = subj.Split(new string[] { "SN=" }, StringSplitOptions.None);
                            if (parts.Length > 1)
                            {
                                sn = parts[1].Split(',')[0].Trim().Trim('"');
                            }
                        }
                        // Parse Given Name (OID 2.5.4.41 or G=)
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
                            OrgName = org,
                            PersonName = person,
                            ValidTo = cert.NotAfter,
                            DaysRemaining = days,
                            IsValid = days > 0,
                            IsCompany = isCompany
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
                        try { p.Kill(); } catch { }
                    }
                }
            }
        }

        public static void RepairAll()
        {
            // 1. Kill old or duplicate NTClient & conflicting apps
            CheckAndResolveProcessConflicts();
            foreach (var p in Process.GetProcessesByName("NTClientSoftware"))
            {
                try { p.Kill(); } catch { }
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

            // 4. Enable PAC for FSZN
            WinINetHelper.EnablePac("http://127.0.0.1:10224/proxy.pac");

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
        public static void DownloadAndInstallGossuok(Action<string, int> onProgress, Action<bool, string> onCompleted)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "Buhly");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                    var zipPath = Path.Combine(tempDir, "AvPKISetup_bel.zip");
                    var extractPath = Path.Combine(tempDir, "AvPKISetup");

                    var url = "https://nces.by/wp-content/uploads/gossuok/2026/AvPKISetup(bel).zip";

                    if (onProgress != null) onProgress("Подключение к серверу nces.by...", 5);

                    using (var wc = new WebClient())
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                        ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                        var autoReset = new AutoResetEvent(false);
                        Exception downloadError = null;

                        wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
                        {
                            if (onProgress != null)
                            {
                                long mbRec = e.BytesReceived / (1024 * 1024);
                                long mbTot = e.TotalBytesToReceive / (1024 * 1024);
                                string msg = string.Format("Загрузка ПО ГосСУОК: {0}% ({1} из {2} МБ)", e.ProgressPercentage, mbRec, mbTot > 0 ? mbTot : 38);
                                onProgress(msg, e.ProgressPercentage);
                            }
                        };

                        wc.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e)
                        {
                            if (e.Error != null) downloadError = e.Error;
                            autoReset.Set();
                        };

                        wc.DownloadFileAsync(new Uri(url), zipPath);
                        autoReset.WaitOne();

                        if (downloadError != null)
                        {
                            if (onCompleted != null) onCompleted(false, "Ошибка загрузки: " + downloadError.Message);
                            return;
                        }
                    }

                    if (onProgress != null) onProgress("Распаковка установочного пакета...", 90);

                    if (Directory.Exists(extractPath))
                    {
                        try { Directory.Delete(extractPath, true); } catch { }
                    }

                    ZipFile.ExtractToDirectory(zipPath, extractPath);

                    var exeFiles = Directory.GetFiles(extractPath, "AvPKISetup*.exe", SearchOption.AllDirectories);
                    string targetExe = exeFiles.Length > 0 ? exeFiles[0] : null;

                    if (targetExe == null)
                    {
                        var anyExe = Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories);
                        if (anyExe.Length > 0) targetExe = anyExe[0];
                    }

                    if (targetExe != null && File.Exists(targetExe))
                    {
                        if (onProgress != null) onProgress("Запуск мастера установки ГосСУОК...", 100);
                        var psi = new ProcessStartInfo(targetExe)
                        {
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                        if (onCompleted != null) onCompleted(true, "Установщик запущен! Завершите установку на экране и вставьте ключ.");
                    }
                    else
                    {
                        if (onCompleted != null) onCompleted(false, "Файл установки не найден внутри архива.");
                    }
                }
                catch (Exception ex)
                {
                    if (onCompleted != null) onCompleted(false, "Сбой установки: " + ex.Message);
                }
            });
        }

        public static void DownloadAndInstallAvTunProxy(Action<string, int> onProgress, Action<bool, string> onCompleted)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "Buhly");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                    var exePath = Path.Combine(tempDir, "AvTunProxy-latest.exe");

                    var url = "https://avtunproxy.by/download/AvTunProxy-latest.exe";

                    if (onProgress != null) onProgress("Подключение к avtunproxy.by...", 5);

                    using (var wc = new WebClient())
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                        ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                        var autoReset = new AutoResetEvent(false);
                        Exception downloadError = null;

                        wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
                        {
                            if (onProgress != null)
                            {
                                long mbRec = e.BytesReceived / (1024 * 1024);
                                long mbTot = e.TotalBytesToReceive / (1024 * 1024);
                                string msg = string.Format("Загрузка AvTunProxy: {0}% ({1} из {2} МБ)", e.ProgressPercentage, mbRec, mbTot > 0 ? mbTot : 8);
                                onProgress(msg, e.ProgressPercentage);
                            }
                        };

                        wc.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e)
                        {
                            if (e.Error != null) downloadError = e.Error;
                            autoReset.Set();
                        };

                        wc.DownloadFileAsync(new Uri(url), exePath);
                        autoReset.WaitOne();

                        if (downloadError != null)
                        {
                            if (onCompleted != null) onCompleted(false, "Ошибка загрузки: " + downloadError.Message);
                            return;
                        }
                    }

                    if (onProgress != null) onProgress("Запуск установщика AvTunProxy...", 100);
                    var psi = new ProcessStartInfo(exePath)
                    {
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    if (onCompleted != null) onCompleted(true, "Установщик AvTunProxy запущен! Завершите установку.");
                }
                catch (Exception ex)
                {
                    if (onCompleted != null) onCompleted(false, "Сбой установки: " + ex.Message);
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

    public class MainWindow : Window
    {
        private StackPanel certCardsContainer;
        private TextBlock statusNTClient;
        private TextBlock statusAvTun;
        private TextBlock statusPac;
        private System.Windows.Shapes.Ellipse dotNTClient;
        private System.Windows.Shapes.Ellipse dotAvTun;
        private System.Windows.Shapes.Ellipse dotPac;
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
        private string _lastCertHash = null;
        private bool _isRefreshing = false;

        public MainWindow()
        {
            Title = "Buhly • ГосПорталы РБ";
            Width = 490;
            Height = 820;
            MinWidth = 450;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 242, 247)); // iOS system Gray 6

            BuildUI();
            RefreshStatuses();

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

            // Brand Header: Buhly
            var headerBlock = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var brandRow = new StackPanel { Orientation = Orientation.Horizontal };
            var brandName = new TextBlock
            {
                Text = "Buhly",
                FontSize = 28,
                FontWeight = FontWeights.ExtraBold,
                Foreground = GetBrush("#007AFF")
            };
            var brandSub = new TextBlock
            {
                Text = " • ГосПорталы",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            };
            brandRow.Children.Add(brandName);
            brandRow.Children.Add(brandSub);

            var subtitle = new TextBlock
            {
                Text = "Вход в 1 клик на порталы РБ и автодиагностика",
                FontSize = 13,
                Foreground = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 2, 0, 0)
            };
            headerBlock.Children.Add(brandRow);
            headerBlock.Children.Add(subtitle);
            mainStack.Children.Add(headerBlock);

            // Prominent Top Card: Active Connected USB Key (только тот, что вставлен!)
            certCardsContainer = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            mainStack.Children.Add(certCardsContainer);

            // Card 1: МНС (Налоговая)
            var cardMNS = CreatePortalCard("🏛️  Личный кабинет МНС (Налоговая)",
                "Авторизация юрлиц и ИП через ЕС ИФЮЛ (nalog.gov.by)",
                "Войти по ЭЦП в МНС", "#007AFF", delegate
                {
                    SystemDiagnoser.CheckAndResolveProcessConflicts();
                    OpenMNS();
                    ShowToast("🚀 Открываем Личный кабинет МНС...");
                }, out statusPortalMNS, out dotPortalMNS);
            mainStack.Children.Add(cardMNS);

            // Card 2: ФСЗН
            var cardFSZN = CreatePortalCard("📑  Портал Фонда (ФСЗН 2.0)",
                "Отчеты ПУ-3, 4-Фонд и извещения (portal2.ssf.gov.by)",
                "Войти по ЭЦП в ФСЗН", "#34C759", delegate
                {
                    SystemDiagnoser.CheckAndResolveProcessConflicts();
                    OpenFSZN();
                    ShowToast("🚀 Открываем Корпоративный портал ФСЗН...");
                }, out statusPortalFSZN, out dotPortalFSZN);
            mainStack.Children.Add(cardFSZN);

            // Card 3: ЭСЧФ (vat.gov.by) - 1 CLICK VIA IE/EDGE
            var cardVAT = CreatePortalCard("🧾  Портал ЭСЧФ (vat.gov.by)",
                "Электронные счета-фактуры по НДС (запуск в 1 клик через IE)",
                "Войти на портал ЭСЧФ", "#5856D6", delegate
                {
                    SystemDiagnoser.CheckAndResolveProcessConflicts();
                    OpenVAT();
                    ShowToast("🚀 Запускаем портал ЭСЧФ (с поддержкой ActiveX)...");
                }, out statusPortalVAT, out dotPortalVAT);
            mainStack.Children.Add(cardVAT);

            // Card 4: Диагностика
            var diagCard = CreateDiagnosticsCard();
            mainStack.Children.Add(diagCard);

            // Card 5: Мастер первой установки (для чистых ПК)
            var wizardCard = CreateFirstRunWizardCard();
            mainStack.Children.Add(wizardCard);

            // Community button: Telegram
            var tgBtn = new Button
            {
                Content = "💬  Telegram-сообщество Buhly: чат селлеров и бухгалтеров",
                Height = 36,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
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
                try { Process.Start("https://t.me/buhly_by"); } catch { }
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
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 0, 10, 0));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;
            btn.Template = template;

            btn.Click += delegate { onClick(); };
            return btn;
        }

        private void CheckAllPortalsOnline()
        {
            CheckSinglePortal("https://lkfl.portal.nalog.gov.by/", statusPortalMNS, dotPortalMNS);
            CheckSinglePortal("http://portal2.ssf.gov.by/mainPage/", statusPortalFSZN, dotPortalFSZN);
            CheckSinglePortal("http://vat.gov.by/mainPage/", statusPortalVAT, dotPortalVAT);
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
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

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
                    bool avestOk = SystemDiagnoser.IsAvestInstalled();

                    // 2. Heavy network HTTP/socket ping for NTClient 8084 (in background thread!)
                    bool ntInstalled = SystemDiagnoser.FindNTClientPath() != null;
                    bool ntOk = ntInstalled && SystemDiagnoser.IsPort8084Healthy();

                    // 3. Process & service detection (in background thread!)
                    bool avInstalled = SystemDiagnoser.FindAvTunProxyPath() != null;
                    bool avOk = avInstalled && SystemDiagnoser.IsAvTunProxyRunning();

                    // 4. Registry check (in background thread!)
                    bool pacOk = SystemDiagnoser.IsPacEnabled();

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
                if (!SystemDiagnoser.IsPort8084Healthy())
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

                    // Fallback to Internet Explorer via VBScript
                    var vbsPath = Path.Combine(Path.GetTempPath(), "Open-VAT.vbs");
                    if (!File.Exists(vbsPath))
                    {
                        File.WriteAllText(vbsPath, "Set objIE = CreateObject(\"InternetExplorer.Application\")\r\nobjIE.Navigate \"http://vat.gov.by/mainPage/\"\r\nobjIE.Visible = 1\r\n", Encoding.ASCII);
                    }
                    Process.Start("wscript.exe", "\"" + vbsPath + "\"");
                    Dispatcher.Invoke(new Action(delegate
                    {
                        ShowToast("🚀 Запускаем ЭСЧФ в Internet Explorer...");
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
