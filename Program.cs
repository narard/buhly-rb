using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

[assembly: AssemblyTitle("Buhly — Помощник бизнеса и ЭЦП РБ")]
[assembly: AssemblyDescription("Локальный помощник селлера и малого бизнеса РБ: госпорталы, ЭЦП, накладные ТН-2/ТТН-1, аналитика WB/Ozon и налоги")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCompany("Maxim Repin (@narard)")]
[assembly: AssemblyProduct("Buhly Desktop Assistant")]
[assembly: AssemblyCopyright("Copyright © 2026 Maxim Repin (@narard)")]
[assembly: AssemblyVersion("3.1.1.0")]
[assembly: AssemblyFileVersion("3.1.1.0")]

namespace GovPortalsApp
{
    public class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        public static void ConfigureSafeTls()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool isSelfTest = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--self-test", StringComparison.OrdinalIgnoreCase))
                    {
                        isSelfTest = true;
                        break;
                    }
                }
            }

            if (isSelfTest)
            {
                try
                {
                    if (!Console.IsOutputRedirected) AttachConsole(-1);
                }
                catch { }
                try
                {
                    ConfigureSafeTls();
                    bool allPassed = AnalyticsEngine.RunSelfTests(Console.Out);
                    Console.Out.Flush();
                    Environment.Exit(allPassed ? 0 : 1);
                }
                catch (Exception ex)
                {
                    try
                    {
                        Console.Error.WriteLine("[FAIL] Self-test runner crashed: " + ex.Message);
                    }
                    catch { }
                    Environment.Exit(1);
                }
                return;
            }

            Mutex singleInstanceMutex = null;
            bool createdNew = false;
            try
            {
                singleInstanceMutex = new Mutex(true, "Local\\BuhlyDesktopMutex_v3", out createdNew);
            }
            catch
            {
                createdNew = true;
            }

            if (singleInstanceMutex != null && !createdNew)
            {
                try { singleInstanceMutex.Dispose(); } catch { }
                MessageBox.Show(
                    "Buhly уже запущен.\n\nЧтобы не повредить профили компаний и настройки, закройте открытое окно программы перед повторным запуском.",
                    "Buhly",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                ConfigureSafeTls();

                var app = new Application();
                app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    try
                    {
                        var logPath = Path.Combine(Path.GetTempPath(), "Buhly_error.log");
                        File.AppendAllText(logPath, DateTime.Now + " - UI EXCEPTION: " + e.Exception.ToString() + "\r\n");
                    }
                    catch { }
                    MessageBox.Show(
                        "Произошла непредвиденная ошибка интерфейса:\n" + e.Exception.Message + "\n\nПодробности сохранены в %TEMP%\\Buhly_error.log.",
                        "Buhly",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    e.Handled = true;
                };

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
            finally
            {
                if (singleInstanceMutex != null)
                {
                    try
                    {
                        if (createdNew) singleInstanceMutex.ReleaseMutex();
                    }
                    catch { }
                    try { singleInstanceMutex.Dispose(); } catch { }
                }
            }
        }
    }

    public partial class MainWindow : Window
    {
        private StackPanel tabOverviewPanel;
        private StackPanel tabPortalsPanel;
        private StackPanel tabAnalyticsPanel;
        private StackPanel tabWaybillsPanel;
        private StackPanel tabRegistryPanel;
        private StackPanel tabToolsPanel;

        private ComboBox sidebarProfileCombo;
        private bool _suppressProfileSelection = false;
        private Button btnUiModeSimple;
        private Button btnUiModePro;

        private Button navBtnOverview;
        private Button navBtnPortals;
        private Button navBtnAnalytics;
        private Button navBtnWaybills;
        private Button navBtnRegistry;
        private Button navBtnTools;

        private TextBlock nbrbUsdLabel;
        private TextBlock nbrbRubLabel;
        private TextBlock nbrbEurLabel;

        private Border toastBanner;
        private TextBlock toastText;
        private DispatcherTimer autoRefreshTimer;
        private DispatcherTimer toastTimer;
        private DispatcherTimer portalOnlineTimer;
        private string _lastCertHash = null;
        private bool _isRefreshing = false;

        public MainWindow()
        {
            Title = "Buhly — Помощник для бизнеса РБ";
            double waW = SystemParameters.WorkArea.Width > 0 ? SystemParameters.WorkArea.Width : 1280;
            double waH = SystemParameters.WorkArea.Height > 0 ? SystemParameters.WorkArea.Height : 768;
            MinWidth = Math.Min(760, Math.Max(640, waW * 0.90));
            MinHeight = Math.Min(480, Math.Max(420, waH * 0.85));
            Width = Math.Min(1020, Math.Max(MinWidth, waW * 0.92));
            Height = Math.Min(740, Math.Max(MinHeight, waH * 0.90));
            SizeToContent = SizeToContent.Manual;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = GetBrush("#F2F2F7");
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            companyStore = CompanyProfilesManager.LoadStore();
            if (CompanyProfilesManager.LastLoadHadCorruptFile)
            {
                MessageBox.Show(
                    "Не удалось прочитать файл профилей организаций (profiles.xml). Резервная копия сохранена как:\n" +
                    CompanyProfilesManager.LastCorruptBackupPath +
                    "\n\nЧтобы не затереть ваши данные, создан временный профиль. Проверьте или пересохраните настройки.",
                    "Buhly — Восстановление профилей",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            BuildUI();
            RefreshStatuses();
            RefreshRegistryTab(false);

            NbrbRatesHelper.FetchAllRatesAsync(delegate(NbrbRatesSnapshot snap)
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (snap == null) return;
                    if (nbrbUsdLabel != null && !string.IsNullOrEmpty(snap.Usd))
                        nbrbUsdLabel.Text = "USD (431): " + snap.Usd;
                    if (nbrbRubLabel != null && !string.IsNullOrEmpty(snap.Rub100))
                        nbrbRubLabel.Text = "100 RUB (456): " + snap.Rub100;
                    if (nbrbEurLabel != null && !string.IsNullOrEmpty(snap.Eur))
                        nbrbEurLabel.Text = "EUR (451): " + snap.Eur;
                }));
            });

            autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            autoRefreshTimer.Tick += delegate { RefreshStatuses(); };
            autoRefreshTimer.Start();

            portalOnlineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
            portalOnlineTimer.Tick += delegate { CheckAllPortalsOnline(); };
            portalOnlineTimer.Start();
            CheckAllPortalsOnline();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (autoRefreshTimer != null) autoRefreshTimer.Stop();
            if (portalOnlineTimer != null) portalOnlineTimer.Stop();
            if (toastTimer != null) toastTimer.Stop();
            base.OnClosed(e);
        }

        private void BuildUI()
        {
            var outerGrid = new Grid();
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(246) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LEFT SIDEBAR (#F5F5F7)
            var sidebarBorder = new Border
            {
                Background = GetBrush("#F5F5F7"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(14, 16, 14, 14)
            };
            var sidebarDock = new DockPanel();

            // Sidebar Top: Brand + Active Profile selector
            var sidebarTop = new StackPanel();
            var brandGrid = new Grid { Margin = new Thickness(2, 0, 2, 2) };
            brandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            brandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brandName = new TextBlock
            {
                Text = "Buhly",
                FontSize = 24,
                FontWeight = FontWeights.ExtraBold,
                Foreground = GetBrush("#007AFF"),
                VerticalAlignment = VerticalAlignment.Center
            };
            var verBadge = new Border
            {
                Background = GetBrush("#EBF3FF"),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(7, 2, 7, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            verBadge.Child = new TextBlock
            {
                Text = "BY 3.1",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#007AFF")
            };
            Grid.SetColumn(brandName, 0);
            Grid.SetColumn(verBadge, 1);
            brandGrid.Children.Add(brandName);
            brandGrid.Children.Add(verBadge);
            sidebarTop.Children.Add(brandGrid);

            sidebarTop.Children.Add(new TextBlock
            {
                Text = "100% локально на вашем ПК",
                FontSize = 11,
                Foreground = GetBrush("#8E8E93"),
                Margin = new Thickness(2, 0, 2, 12)
            });

            // Active company profile card in sidebar header
            var profileBox = new Border
            {
                Background = Brushes.White,
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(9, 8, 9, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var profStack = new StackPanel();
            var profTop = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            profTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            profTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            profTop.Children.Add(new TextBlock
            {
                Text = "Активная компания",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#8E8E93"),
                VerticalAlignment = VerticalAlignment.Center
            });

            var btnManageProf = new Button
            {
                Content = "⚙️",
                FontSize = 12,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(4, 0, 4, 0),
                ToolTip = "Управление профилями организаций (Создать / Редактировать / Удалить)"
            };
            btnManageProf.Click += delegate { OpenProfileManagerWindow(); };
            Grid.SetColumn(btnManageProf, 1);
            profTop.Children.Add(btnManageProf);
            profStack.Children.Add(profTop);

            sidebarProfileCombo = new ComboBox
            {
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold
            };
            sidebarProfileCombo.SelectionChanged += delegate
            {
                if (_suppressProfileSelection) return;
                var selected = sidebarProfileCombo.SelectedItem as CompanyProfile;
                if (selected != null)
                {
                    CompanyProfilesManager.SetActiveProfile(companyStore, selected.Id);
                    RefreshAfterProfileChange();
                    ShowToast("✓ Профиль: " + selected.DisplayName);
                }
            };
            profStack.Children.Add(sidebarProfileCombo);

            profileBox.Child = profStack;
            sidebarTop.Children.Add(profileBox);

            // UI Mode 2-button pill switcher: Базовый / Бухгалтер PRO
            var modeSwitchWrap = new Border
            {
                Background = GetBrush("#E5E5EA"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var modeSwitchGrid = new Grid();
            modeSwitchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modeSwitchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
            modeSwitchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });

            btnUiModeSimple = CreateSidebarPillSwitchButton("Базовый", "Упрощённый режим для селлера: быстрый старт и только нужные поля");
            btnUiModeSimple.Click += delegate
            {
                if (companyStore == null) return;
                companyStore.UiMode = "Simple";
                CompanyProfilesManager.Save(companyStore);
                RefreshUiModeAcrossTabs();
                ShowToast("Режим интерфейса: Базовый");
            };
            Grid.SetColumn(btnUiModeSimple, 0);
            modeSwitchGrid.Children.Add(btnUiModeSimple);

            btnUiModePro = CreateSidebarPillSwitchButton("Бухгалтер PRO", "Полный режим: выгрузка 1С, БСО в мм, ТТН-1 и правила группировки");
            btnUiModePro.Click += delegate
            {
                if (companyStore == null) return;
                companyStore.UiMode = "Pro";
                CompanyProfilesManager.Save(companyStore);
                RefreshUiModeAcrossTabs();
                ShowToast("Режим интерфейса: Бухгалтер PRO");
            };
            Grid.SetColumn(btnUiModePro, 2);
            modeSwitchGrid.Children.Add(btnUiModePro);

            modeSwitchWrap.Child = modeSwitchGrid;
            sidebarTop.Children.Add(modeSwitchWrap);

            DockPanel.SetDock(sidebarTop, Dock.Top);
            sidebarDock.Children.Add(sidebarTop);

            // Sidebar Bottom: NBRB rates + Telegram @buhly_by_chat
            var sidebarBottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            var nbrbCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var nbrbStack = new StackPanel();
            nbrbStack.Children.Add(new TextBlock
            {
                Text = "💱 Курсы НБРБ",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            nbrbUsdLabel = new TextBlock
            {
                Text = "USD (431): загрузка...",
                FontSize = 11,
                Foreground = GetBrush("#3A3A3C")
            };
            nbrbRubLabel = new TextBlock
            {
                Text = "100 RUB (456): ...",
                FontSize = 11,
                Foreground = GetBrush("#3A3A3C"),
                Margin = new Thickness(0, 1, 0, 1)
            };
            nbrbEurLabel = new TextBlock
            {
                Text = "EUR (451): ...",
                FontSize = 11,
                Foreground = GetBrush("#3A3A3C")
            };
            nbrbStack.Children.Add(nbrbUsdLabel);
            nbrbStack.Children.Add(nbrbRubLabel);
            nbrbStack.Children.Add(nbrbEurLabel);
            nbrbCard.Child = nbrbStack;
            sidebarBottom.Children.Add(nbrbCard);

            var tgBtn = CreateCompactActionButton("💬 Чат @buhly_by_chat", "#EBF3FF", "#007AFF");
            tgBtn.Height = 32;
            tgBtn.FontSize = 11.5;
            tgBtn.ToolTip = "Открыть Telegram-чат сообщества и поддержки Buhly";
            tgBtn.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo("https://t.me/buhly_by_chat") { UseShellExecute = true }); } catch { }
            };
            sidebarBottom.Children.Add(tgBtn);

            var aboutBtn = new Button
            {
                Content = "ℹ️ О программе и дисклеймер",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = GetBrush("#636366"),
                FontSize = 10.5,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            aboutBtn.Click += delegate { OpenAboutDisclaimerDialog(); };
            sidebarBottom.Children.Add(aboutBtn);

            DockPanel.SetDock(sidebarBottom, Dock.Bottom);
            sidebarDock.Children.Add(sidebarBottom);

            // Sidebar Center: 6 Navigation Tabs
            var navStack = new StackPanel();
            navBtnOverview = CreateSidebarNavButton("🏠  Сводка и сроки", 0);
            navBtnPortals = CreateSidebarNavButton("🚀  ГосПорталы", 1);
            navBtnAnalytics = CreateSidebarNavButton("📊  Маркетплейсы и налоги", 2);
            navBtnWaybills = CreateSidebarNavButton("📦  Накладные", 3);
            navBtnRegistry = CreateSidebarNavButton("🔑  Реестр ЭЦП", 4);
            navBtnTools = CreateSidebarNavButton("🔎  УНП и ПК", 5);

            navStack.Children.Add(navBtnOverview);
            navStack.Children.Add(navBtnPortals);
            navStack.Children.Add(navBtnAnalytics);
            navStack.Children.Add(navBtnWaybills);
            navStack.Children.Add(navBtnRegistry);
            navStack.Children.Add(navBtnTools);
            sidebarDock.Children.Add(navStack);

            sidebarBorder.Child = sidebarDock;
            Grid.SetColumn(sidebarBorder, 0);
            outerGrid.Children.Add(sidebarBorder);

            // RIGHT CONTENT AREA (#F2F2F7)
            var rightGrid = new Grid { Background = GetBrush("#F2F2F7") };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var mainStack = new StackPanel { Margin = new Thickness(22, 18, 22, 24) };

            // TAB 0: OVERVIEW
            tabOverviewPanel = new StackPanel();
            BuildOverviewTab(tabOverviewPanel);
            mainStack.Children.Add(tabOverviewPanel);

            // TAB 1: PORTALS
            tabPortalsPanel = new StackPanel { Visibility = Visibility.Collapsed };
            BuildPortalsTab(tabPortalsPanel);
            mainStack.Children.Add(tabPortalsPanel);

            // TAB 2: ANALYTICS & TAXES
            tabAnalyticsPanel = new StackPanel { Visibility = Visibility.Collapsed };
            BuildAnalyticsTab(tabAnalyticsPanel);
            mainStack.Children.Add(tabAnalyticsPanel);

            // TAB 3: WAYBILLS
            tabWaybillsPanel = new StackPanel { Visibility = Visibility.Collapsed };
            BuildWaybillsTab(tabWaybillsPanel);
            mainStack.Children.Add(tabWaybillsPanel);

            // TAB 4: REGISTRY
            tabRegistryPanel = new StackPanel { Visibility = Visibility.Collapsed };
            BuildRegistryTab(tabRegistryPanel);
            mainStack.Children.Add(tabRegistryPanel);

            // TAB 5: TOOLS (UNP & PC)
            tabToolsPanel = new StackPanel { Visibility = Visibility.Collapsed };
            BuildToolsTab(tabToolsPanel);
            mainStack.Children.Add(tabToolsPanel);

            scroll.Content = mainStack;
            rightGrid.Children.Add(scroll);

            // Non-blocking iOS Toast Banner at bottom of content area
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
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            toastBanner.Child = toastText;
            rightGrid.Children.Add(toastBanner);

            Grid.SetColumn(rightGrid, 1);
            outerGrid.Children.Add(rightGrid);

            RefreshSidebarProfileCombo();
            RefreshUiModeAcrossTabs();
            SwitchTab(0);

            Content = outerGrid;
        }

        private Button CreateSidebarPillSwitchButton(string text, string tooltip)
        {
            var btn = new Button
            {
                Content = text,
                Height = 26,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Padding = new Thickness(6, 0, 6, 0)
            };
            var template = new ControlTemplate(typeof(Button));
            var bf = new FrameworkElementFactory(typeof(Border));
            bf.Name = "pillBorder";
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bf.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp);
            template.VisualTree = bf;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.86, "pillBorder"));
            template.Triggers.Add(hoverTrigger);

            var pressTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72, "pillBorder"));
            template.Triggers.Add(pressTrigger);

            btn.Template = template;
            return btn;
        }

        private void UpdateSidebarUiModeButtons(bool isPro)
        {
            if (btnUiModeSimple != null)
            {
                btnUiModeSimple.Background = isPro ? Brushes.Transparent : Brushes.White;
                btnUiModeSimple.Foreground = GetBrush(isPro ? "#636366" : "#007AFF");
                btnUiModeSimple.FontWeight = isPro ? FontWeights.Medium : FontWeights.Bold;
            }
            if (btnUiModePro != null)
            {
                btnUiModePro.Background = isPro ? GetBrush("#007AFF") : Brushes.Transparent;
                btnUiModePro.Foreground = isPro ? Brushes.White : GetBrush("#636366");
                btnUiModePro.FontWeight = isPro ? FontWeights.Bold : FontWeights.Medium;
            }
        }

        private void RefreshUiModeAcrossTabs()
        {
            bool isPro = IsProUiMode();
            UpdateSidebarUiModeButtons(isPro);
            RefreshOverviewUiMode(isPro);
            RefreshAnalyticsUiMode(isPro);
            RefreshWaybillUiMode(isPro);
        }

        private void RefreshSidebarProfileCombo()
        {
            if (sidebarProfileCombo == null || companyStore == null) return;
            _suppressProfileSelection = true;
            try
            {
                sidebarProfileCombo.Items.Clear();
                foreach (var p in companyStore.Profiles)
                {
                    sidebarProfileCombo.Items.Add(p);
                }
                var active = ActiveCompanyProfile;
                if (active != null)
                {
                    sidebarProfileCombo.SelectedItem = active;
                    sidebarProfileCombo.ToolTip = string.Format("{0}\nУНП: {1} • Режим: {2}",
                        active.DisplayName ?? "",
                        string.IsNullOrEmpty(active.Unp) ? "не указан" : active.Unp,
                        active.GetTaxRegimeTitle());
                }
            }
            finally
            {
                _suppressProfileSelection = false;
            }
        }

        private void RefreshAfterProfileChange()
        {
            RefreshSidebarProfileCombo();
            RefreshOverviewProfileHeader();
            RefreshOverviewCalendar();
            RefreshWaybillTabFromActiveProfile();
            RefreshAnalyticsTabFromActiveProfile();
            RefreshUiModeAcrossTabs();
        }

        private Button CreateSidebarNavButton(string title, int tabIndex)
        {
            var btn = new Button
            {
                Content = title,
                Height = 36,
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrush("#1C1C1E"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 0, 8, 0),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "navBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85, "navBorder"));
            template.Triggers.Add(hoverTrigger);

            var pressTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.70, "navBorder"));
            template.Triggers.Add(pressTrigger);

            btn.Template = template;
            btn.Click += delegate { SwitchTab(tabIndex); };
            return btn;
        }

        private void SwitchTab(int index)
        {
            if (tabOverviewPanel != null) tabOverviewPanel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (tabPortalsPanel != null) tabPortalsPanel.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (tabAnalyticsPanel != null) tabAnalyticsPanel.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (tabWaybillsPanel != null) tabWaybillsPanel.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            if (tabRegistryPanel != null) tabRegistryPanel.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
            if (tabToolsPanel != null) tabToolsPanel.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;

            var btns = new Button[] { navBtnOverview, navBtnPortals, navBtnAnalytics, navBtnWaybills, navBtnRegistry, navBtnTools };
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] == null) continue;
                bool active = (i == index);
                btns[i].Background = active ? Brushes.White : Brushes.Transparent;
                btns[i].Foreground = active ? GetBrush("#007AFF") : GetBrush("#3A3A3C");
                btns[i].FontWeight = active ? FontWeights.Bold : FontWeights.Medium;
            }

            if (index == 0)
            {
                RefreshOverviewProfileHeader();
                RefreshOverviewAlerts();
            }
            else if (index == 4)
            {
                RefreshRegistryTab(false);
            }
        }

        private void ShowToast(string message)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { ShowToast(message); }));
                return;
            }
            if (toastText == null || toastBanner == null) return;
            toastText.Text = message;
            toastBanner.Visibility = Visibility.Visible;
            try
            {
                var fade = new DoubleAnimation(0.0, 0.95, TimeSpan.FromMilliseconds(160));
                toastBanner.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            catch
            {
                toastBanner.Opacity = 0.95;
            }
            if (toastTimer != null) toastTimer.Stop();
            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            toastTimer.Tick += delegate
            {
                toastBanner.Visibility = Visibility.Collapsed;
                toastTimer.Stop();
            };
            toastTimer.Start();
        }

        private void OpenAboutDisclaimerDialog()
        {
            var win = new Window
            {
                Title = "О программе Buhly v3.1.1",
                Width = 520,
                Height = 410,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = GetBrush("#F2F2F7")
            };
            var root = new StackPanel { Margin = new Thickness(18) };
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Buhly v3.1.1 — открытая локальная утилита (MIT)",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Автор: Максим Репин (@narard / @max_repin), г. Минск.\n" +
                       "Лицензия: MIT Open Source (Copyright © 2026 Maxim Repin).\n\n" +
                       "Отказ от ответственности (дисклеймер):\n" +
                       "• Программа предоставляется «как есть» (AS IS) без каких-либо гарантий.\n" +
                       "• Все расчёты налогов, ФСЗН, КУДиР, 1С-выгрузки и печатные формы ТН-2/ТТН-1 носят справочный характер. Перед подачей деклараций и отгрузкой товара сверяйте цифры с первичными документами, НК РБ и актуальным перечнем ЭТН (Пост. № 250 / № 528).\n\n" +
                       "Приватность и сеть:\n" +
                       "• Ваши отчёты, выписки и PIN ЭЦП не покидают компьютер.\n" +
                       "• 1 раз в сутки выполняется анонимная проверка новой версии (без ПДн/УНП/сумм), а также загрузка курсов НБРБ и пинг доступности госпорталов.",
                FontSize = 12,
                Foreground = GetBrush("#3A3A3C"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            });
            var linksRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            var btnSite = CreateCompactActionButton("🌐 Сайт buhly.by", "#EBF3FF", "#007AFF");
            btnSite.Margin = new Thickness(0, 0, 8, 0);
            btnSite.Click += delegate { try { Process.Start(new ProcessStartInfo("https://buhly.by/privacy.html") { UseShellExecute = true }); } catch { } };
            var btnGit = CreateCompactActionButton("💻 GitHub (MIT)", "#F2F2F7", "#1C1C1E");
            btnGit.Margin = new Thickness(0, 0, 8, 0);
            btnGit.Click += delegate { try { Process.Start(new ProcessStartInfo("https://github.com/narard/buhly-rb") { UseShellExecute = true }); } catch { } };
            var btnClose = CreateCompactActionButton("Закрыть", "#007AFF", "#FFFFFF");
            btnClose.Click += delegate { win.Close(); };
            linksRow.Children.Add(btnSite);
            linksRow.Children.Add(btnGit);
            linksRow.Children.Add(btnClose);
            stack.Children.Add(linksRow);
            card.Child = stack;
            root.Children.Add(card);
            win.Content = root;
            win.ShowDialog();
        }

        private Button CreateIOSButton(string text, string hexColor, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                Height = 42,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
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

        private void CheckAllPortalsOnline()
        {
            CheckSinglePortal(0, "https://lkfl.portal.nalog.gov.by/", statusPortalMNS, dotPortalMNS);
            CheckSinglePortal(1, "http://portal2.ssf.gov.by/mainPage/", statusPortalFSZN, dotPortalFSZN);
            CheckSinglePortal(2, "http://vat.gov.by/mainPage/", statusPortalVAT, dotPortalVAT);
            CheckSinglePortal(3, "https://e-pasluga.by/", statusPortalEpasluga, dotPortalEpasluga);
        }

        private void CheckSinglePortal(int portalIndex, string url, TextBlock tb, System.Windows.Shapes.Ellipse dot)
        {
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
                        ok = ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 400);
                    }
                }
                catch (WebException wex)
                {
                    sw.Stop();
                    ms = sw.ElapsedMilliseconds;
                    using (var resp = wex.Response as HttpWebResponse)
                    {
                        if (resp != null && (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 400)
                        {
                            ok = true;
                        }
                    }
                }
                catch
                {
                    sw.Stop();
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (dot != null && tb != null)
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
                    }
                    UpdateOverviewPortalPing(portalIndex, ok, ms);
                }));
            });
        }

        private void RefreshStatuses()
        {
            if (_isRefreshing) return;
            if ((tabWaybillsPanel != null && tabWaybillsPanel.Visibility == Visibility.Visible) ||
                (tabAnalyticsPanel != null && tabAnalyticsPanel.Visibility == Visibility.Visible)) return;
            _isRefreshing = true;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var certs = SystemDiagnoser.GetConnectedGovCertificates();
                    if (certs.Count > 0)
                    {
                        CertRegistryManager.MergeAndGetAll(certs);
                    }
                    bool avestOk = SystemDiagnoser.IsAvestInstalled();

                    bool ntInstalled = SystemDiagnoser.FindNTClientPath() != null;
                    bool ntOk = ntInstalled && SystemDiagnoser.IsPort8084Healthy();

                    bool avInstalled = SystemDiagnoser.FindAvTunProxyPath() != null;
                    bool avOk = avInstalled && SystemDiagnoser.IsAvTunProxyRunning();

                    bool pacOk = SystemDiagnoser.IsPacEnabled();
                    string hostsDetailsDummy;
                    bool hostsOk = SystemDiagnoser.CheckHostsFile(out hostsDetailsDummy);

                    var sb = new StringBuilder();
                    sb.Append(avestOk).Append("|").Append(certs.Count);
                    foreach (var c in certs)
                    {
                        sb.Append("|").Append(c.OrgName).Append(";").Append(c.DaysRemaining);
                    }
                    string certKey = sb.ToString();

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            if (certKey != _lastCertHash)
                            {
                                _lastCertHash = certKey;
                                RenderCertificateCard(certs);
                                UpdateOverviewTokenStatus(certs, avestOk);
                                RefreshOverviewAlerts();
                            }

                            if (statusNTClient != null && dotNTClient != null)
                            {
                                string newNtText = !ntInstalled ? "Не установлена" : (ntOk ? "Активен (порт 8084)" : "Не отвечает / Сбой");
                                if (statusNTClient.Text != newNtText)
                                {
                                    statusNTClient.Text = newNtText;
                                    dotNTClient.Fill = !ntInstalled ? GetBrush("#FF9500") : (ntOk ? GetBrush("#34C759") : GetBrush("#FF3B30"));
                                }
                            }

                            if (statusAvTun != null && dotAvTun != null)
                            {
                                string newAvText = !avInstalled ? "Не установлена" : (avOk ? "Запущен" : "Остановлен");
                                if (statusAvTun.Text != newAvText)
                                {
                                    statusAvTun.Text = newAvText;
                                    dotAvTun.Fill = !avInstalled ? GetBrush("#FF9500") : (avOk ? GetBrush("#34C759") : GetBrush("#FF9500"));
                                }
                            }

                            if (statusPac != null && dotPac != null)
                            {
                                string newPacText = pacOk ? "Включен (10224)" : "Отключен";
                                if (statusPac.Text != newPacText)
                                {
                                    statusPac.Text = newPacText;
                                    dotPac.Fill = pacOk ? GetBrush("#34C759") : GetBrush("#FF3B30");
                                }
                            }

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
