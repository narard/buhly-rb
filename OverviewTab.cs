using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private Border ovUpdateBanner;
        private TextBlock ovUpdateBannerText;
        private string ovUpdateReleaseUrl;
        private Border ovOnboardingCard;

        private TextBlock ovProfileTitle;
        private TextBlock ovProfileMeta;
        private TextBlock ovTokenStatusText;
        private System.Windows.Shapes.Ellipse ovTokenDot;
        private TextBlock ovCertAlertText;
        private Border ovCertAlertBox;
        private StackPanel ovCalendarRowsStack;
        private StackPanel ovCalendarFutureStack;
        private Button ovBtnToggleFutureCalendar;
        private bool _ovCalendarFutureExpanded = false;

        private System.Windows.Shapes.Ellipse ovDotMns;
        private TextBlock ovPingMns;
        private System.Windows.Shapes.Ellipse ovDotFszn;
        private TextBlock ovPingFszn;
        private System.Windows.Shapes.Ellipse ovDotVat;
        private TextBlock ovPingVat;
        private System.Windows.Shapes.Ellipse ovDotEpasluga;
        private TextBlock ovPingEpasluga;

        private void BuildOverviewTab(StackPanel container)
        {
            // SILENT UPDATE BANNER (shown only when a newer release exists on GitHub)
            ovUpdateBanner = new Border
            {
                Background = GetBrush("#EFF6FF"),
                BorderBrush = GetBrush("#93C5FD"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            var updGrid = new Grid();
            updGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            updGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            updGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            updGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            updGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            ovUpdateBannerText = new TextBlock
            {
                Text = "Доступна новая версия Buhly",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1E40AF"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            var btnOpenUpdate = CreateSmallPillButton("Скачать обновление →", "#2563EB", "#FFFFFF");
            btnOpenUpdate.VerticalAlignment = VerticalAlignment.Center;
            btnOpenUpdate.Click += delegate
            {
                if (!string.IsNullOrEmpty(ovUpdateReleaseUrl))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(ovUpdateReleaseUrl) { UseShellExecute = true });
                    }
                    catch { }
                }
            };
            var btnDismissUpdate = CreateSmallPillButton("✕", "#DBEAFE", "#1E40AF");
            btnDismissUpdate.VerticalAlignment = VerticalAlignment.Center;
            btnDismissUpdate.ToolTip = "Скрыть уведомление";
            btnDismissUpdate.Click += delegate
            {
                ovUpdateBanner.Visibility = Visibility.Collapsed;
            };

            Grid.SetColumn(ovUpdateBannerText, 0);
            Grid.SetColumn(btnOpenUpdate, 2);
            Grid.SetColumn(btnDismissUpdate, 4);
            updGrid.Children.Add(ovUpdateBannerText);
            updGrid.Children.Add(btnOpenUpdate);
            updGrid.Children.Add(btnDismissUpdate);
            ovUpdateBanner.Child = updGrid;
            container.Children.Add(ovUpdateBanner);

            UpdateChecker.CheckForUpdatesAsync(delegate(string tagName, string htmlUrl)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (ovUpdateBanner == null || ovUpdateBannerText == null) return;
                    string cleanTag = (tagName ?? "").Trim();
                    if (!cleanTag.StartsWith("v", StringComparison.OrdinalIgnoreCase) && cleanTag.Length > 0)
                        cleanTag = "v" + cleanTag;
                    ovUpdateReleaseUrl = htmlUrl;
                    ovUpdateBannerText.Text = "🚀 Доступна новая версия Buhly " + cleanTag + " — с исправлениями и обновлениями РБ.";
                    ovUpdateBanner.Visibility = Visibility.Visible;
                }));
            });

            // HEADER CARD: Compact Regime / UNP Badge + Profile Manager
            var topCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var profileBadgeRow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            var regimePill = new Border
            {
                Background = GetBrush("#EBF3FF"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            ovProfileTitle = new TextBlock
            {
                Text = "ООО • УСН 6% (без НДС)",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#007AFF"),
                TextWrapping = TextWrapping.Wrap
            };
            regimePill.Child = ovProfileTitle;

            ovProfileMeta = new TextBlock
            {
                Text = "",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#636366"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            profileBadgeRow.Children.Add(regimePill);
            profileBadgeRow.Children.Add(ovProfileMeta);

            var btnManageProfiles = CreateSmallPillButton("⚙️ Профили компаний", "#EBF3FF", "#007AFF");
            btnManageProfiles.VerticalAlignment = VerticalAlignment.Center;
            btnManageProfiles.Click += delegate { OpenProfileManagerWindow(); };

            Grid.SetColumn(profileBadgeRow, 0);
            Grid.SetColumn(btnManageProfiles, 2);
            topGrid.Children.Add(profileBadgeRow);
            topGrid.Children.Add(btnManageProfiles);
            topCard.Child = topGrid;
            container.Children.Add(topCard);

            // ONBOARDING CARD (visible in Simple mode unless dismissed, collapsed in Pro mode)
            ovOnboardingCard = new Border
            {
                Background = GetBrush("#F0F9FF"),
                BorderBrush = GetBrush("#BAE6FD"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var onbStack = new StackPanel();
            var onbHdrGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            onbHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            onbHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            onbHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var onbTitle = new TextBlock
            {
                Text = "🚀 Быстрый старт: 3 шага",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#0369A1"),
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnHideOnboarding = CreateSmallPillButton("✕ Скрыть", "#E0F2FE", "#0369A1");
            btnHideOnboarding.VerticalAlignment = VerticalAlignment.Center;
            btnHideOnboarding.ToolTip = "Скрыть блок быстрого старта, чтобы календарь сроков сразу был на первом экране";
            btnHideOnboarding.Click += delegate
            {
                SaveOnboardingHiddenFlag();
                if (ovOnboardingCard != null) ovOnboardingCard.Visibility = Visibility.Collapsed;
                ShowToast("Блок «Быстрый старт» скрыт");
            };
            Grid.SetColumn(onbTitle, 0);
            Grid.SetColumn(btnHideOnboarding, 2);
            onbHdrGrid.Children.Add(onbTitle);
            onbHdrGrid.Children.Add(btnHideOnboarding);
            onbStack.Children.Add(onbHdrGrid);

            onbStack.Children.Add(new TextBlock
            {
                Text = "Базовый режим для селлера: выберите организацию, проверьте налоги по отчётам и создайте ТН-2.",
                FontSize = 11.5,
                Foreground = GetBrush("#075985"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            onbStack.Children.Add(CreateOnboardingStepRow(
                "1. Профиль и ЭЦП",
                "Проверьте организацию и систему налогообложения (ООО УСН 6% / ИП 20%) или заполните с ключа ЭЦП.",
                "Профили →",
                delegate { OpenProfileManagerWindow(); }));
            onbStack.Children.Add(CreateOnboardingStepRow(
                "2. Отчёты WB/Ozon и налоги",
                "Перетащите отчёты маркетплейса и выписку банка — Бухли сверит выплаты и покажет расшифровку налога.",
                "Аналитика →",
                delegate { SwitchTab(2); }));
            onbStack.Children.Add(CreateOnboardingStepRow(
                "3. Накладная ТН-2",
                "Загрузите файл поставки WB FBS/FBO, при необходимости поправьте цены и сформируйте ТН-2.",
                "Накладные →",
                delegate { SwitchTab(3); }));

            ovOnboardingCard.Child = onbStack;
            container.Children.Add(ovOnboardingCard);

            // CARD 1: USB TOKEN + 4 PORTALS PING STRIP + REPAIR ALL
            var statusCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var statusStack = new StackPanel();

            var tokenHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            tokenHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tokenHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            tokenHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tokenLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            tokenLeft.Children.Add(new TextBlock
            {
                Text = "🔑  ЭЦП и доступность госпорталов",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var tokenRow = new StackPanel { Orientation = Orientation.Horizontal };
            ovTokenDot = new System.Windows.Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ovTokenStatusText = new TextBlock
            {
                Text = "Проверка USB-ключа ЭЦП...",
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrush("#3A3A3C"),
                TextWrapping = TextWrapping.Wrap
            };
            tokenRow.Children.Add(ovTokenDot);
            tokenRow.Children.Add(ovTokenStatusText);
            tokenLeft.Children.Add(tokenRow);

            var btnRepairOverview = CreateIOSButton("⚡  Починить всё", "#FF9500", delegate
            {
                TriggerRepairAll();
            });
            btnRepairOverview.Height = 38;
            btnRepairOverview.FontSize = 13;
            btnRepairOverview.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(tokenLeft, 0);
            Grid.SetColumn(btnRepairOverview, 2);
            tokenHeaderGrid.Children.Add(tokenLeft);
            tokenHeaderGrid.Children.Add(btnRepairOverview);
            statusStack.Children.Add(tokenHeaderGrid);

            // Compact 4-portal ping strip
            var pingStrip = new Border
            {
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 10, 12, 10)
            };
            var pingGrid = new Grid();
            for (int c = 0; c < 4; c++)
            {
                pingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            pingGrid.Children.Add(CreateCompactPingCell(0, "МНС", out ovDotMns, out ovPingMns));
            pingGrid.Children.Add(CreateCompactPingCell(1, "ФСЗН 2.0", out ovDotFszn, out ovPingFszn));
            pingGrid.Children.Add(CreateCompactPingCell(2, "ЭСЧФ", out ovDotVat, out ovPingVat));
            pingGrid.Children.Add(CreateCompactPingCell(3, "Е-Паслуга", out ovDotEpasluga, out ovPingEpasluga));

            pingStrip.Child = pingGrid;
            statusStack.Children.Add(pingStrip);

            statusCard.Child = statusStack;
            container.Children.Add(statusCard);

            // CARD 2: QUICK ACTIONS
            var quickCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var quickStack = new StackPanel();
            quickStack.Children.Add(new TextBlock
            {
                Text = "⚡  Быстрые действия",
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var qGrid = new Grid();
            qGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            qGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            qGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            qGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            qGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnGoReports = CreateQuickActionTile("📊  Загрузить отчёты", "WB / выписка банка / 1С", "#E0F2FE", "#0369A1", delegate { SwitchTab(2); });
            var btnGoWaybill = CreateQuickActionTile("📦  Создать накладную", "ТН-2 из файла поставки", "#ECFDF5", "#15803D", delegate { SwitchTab(3); });
            var btnGoUnp = CreateQuickActionTile("🔎  Проверить УНП", "База ГРП МНС Беларуси", "#F3E8FF", "#6D28D9", delegate { SwitchTab(5); });

            Grid.SetColumn(btnGoReports, 0);
            Grid.SetColumn(btnGoWaybill, 2);
            Grid.SetColumn(btnGoUnp, 4);
            qGrid.Children.Add(btnGoReports);
            qGrid.Children.Add(btnGoWaybill);
            qGrid.Children.Add(btnGoUnp);
            quickStack.Children.Add(qGrid);
            quickCard.Child = quickStack;
            container.Children.Add(quickCard);

            // CARD 3: RB TAX CALENDAR & CERT ALERTS
            var calCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var calStack = new StackPanel();
            calStack.Children.Add(new TextBlock
            {
                Text = "📅  Налоговый календарь РБ и контроль ЭЦП",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            calStack.Children.Add(new TextBlock
            {
                Text = "Ближайшие сроки отчётности и платежей (при выпадении на выходной или праздничный день срок переносится на ближайший рабочий день).",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 10)
            });

            // Alert box for expiring certificates <= 30 days
            ovCertAlertBox = new Border
            {
                Background = GetBrush("#FEF2F2"),
                BorderBrush = GetBrush("#FECACA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };
            var alertGrid = new Grid();
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            ovCertAlertText = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#B91C1C"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnToRegistry = CreateSmallPillButton("Открыть реестр →", "#FEE2E2", "#B91C1C");
            btnToRegistry.VerticalAlignment = VerticalAlignment.Center;
            btnToRegistry.Click += delegate { SwitchTab(4); };

            Grid.SetColumn(ovCertAlertText, 0);
            Grid.SetColumn(btnToRegistry, 2);
            alertGrid.Children.Add(ovCertAlertText);
            alertGrid.Children.Add(btnToRegistry);
            ovCertAlertBox.Child = alertGrid;
            calStack.Children.Add(ovCertAlertBox);

            ovCalendarRowsStack = new StackPanel();
            calStack.Children.Add(ovCalendarRowsStack);

            ovCalendarFutureStack = new StackPanel
            {
                Visibility = Visibility.Collapsed
            };
            calStack.Children.Add(ovCalendarFutureStack);

            ovBtnToggleFutureCalendar = CreateSmallPillButton("Показать следующие месяцы и квартал ▼", "#F2F2F7", "#3A3A3C");
            ovBtnToggleFutureCalendar.HorizontalAlignment = HorizontalAlignment.Left;
            ovBtnToggleFutureCalendar.Margin = new Thickness(0, 4, 0, 0);
            ovBtnToggleFutureCalendar.Click += delegate
            {
                _ovCalendarFutureExpanded = !_ovCalendarFutureExpanded;
                if (ovCalendarFutureStack != null)
                    ovCalendarFutureStack.Visibility = _ovCalendarFutureExpanded ? Visibility.Visible : Visibility.Collapsed;
                ovBtnToggleFutureCalendar.Content = _ovCalendarFutureExpanded
                    ? "Скрыть следующие месяцы и квартал ▲"
                    : "Показать следующие месяцы и квартал ▼";
            };
            calStack.Children.Add(ovBtnToggleFutureCalendar);

            calCard.Child = calStack;
            container.Children.Add(calCard);

            RefreshOverviewProfileHeader();
            RefreshOverviewCalendar();
            RefreshOverviewAlerts();
            RefreshOverviewUiMode(IsProUiMode());
        }

        private Border CreateOnboardingStepRow(string title, string desc, string btnText, Action onClick)
        {
            var rowCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = GetBrush("#E0F2FE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            textStack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 0)
            });

            var btn = CreateSmallPillButton(btnText, "#EBF3FF", "#007AFF");
            btn.VerticalAlignment = VerticalAlignment.Center;
            btn.Click += delegate { if (onClick != null) onClick(); };

            Grid.SetColumn(textStack, 0);
            Grid.SetColumn(btn, 2);
            g.Children.Add(textStack);
            g.Children.Add(btn);
            rowCard.Child = g;
            return rowCard;
        }

        private static string GetOnboardingHiddenFlagPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            return Path.Combine(dir, "overview_onboarding_hidden.flag");
        }

        private static bool IsOnboardingHidden()
        {
            try { return File.Exists(GetOnboardingHiddenFlagPath()); }
            catch { return false; }
        }

        private static void SaveOnboardingHiddenFlag()
        {
            try
            {
                var path = GetOnboardingHiddenFlagPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, "hidden", System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private void RefreshOverviewUiMode(bool isPro)
        {
            if (ovOnboardingCard != null)
            {
                ovOnboardingCard.Visibility = (isPro || IsOnboardingHidden()) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private StackPanel CreateCompactPingCell(int colIndex, string title, out System.Windows.Shapes.Ellipse dot, out TextBlock pingText)
        {
            var cell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(cell, colIndex);

            dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            pingText = new TextBlock
            {
                Text = "проверка...",
                FontSize = 11,
                Foreground = GetBrush("#8E8E93")
            };
            textStack.Children.Add(pingText);

            cell.Children.Add(dot);
            cell.Children.Add(textStack);
            return cell;
        }

        private Button CreateQuickActionTile(string title, string sub, string bgHex, string fgHex, Action onClick)
        {
            var btn = new Button
            {
                Background = GetBrush(bgHex),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush(fgHex),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 11,
                Foreground = GetBrush("#3A3A3C"),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            btn.Content = stack;

            var template = new ControlTemplate(typeof(Button));
            var bf = new FrameworkElementFactory(typeof(Border));
            bf.Name = "tileBorder";
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bf.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            bf.AppendChild(cp);
            template.VisualTree = bf;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88, "tileBorder"));
            template.Triggers.Add(hover);

            btn.Template = template;
            btn.Click += delegate { if (onClick != null) onClick(); };
            return btn;
        }

        private void RefreshOverviewProfileHeader()
        {
            if (ovProfileTitle == null || ovProfileMeta == null) return;
            var prof = ActiveCompanyProfile;
            if (prof == null)
            {
                ovProfileTitle.Text = "Профиль не выбран";
                ovProfileMeta.Text = "";
                return;
            }
            ovProfileTitle.Text = prof.GetTaxRegimeTitle();
            ovProfileMeta.Text = string.IsNullOrWhiteSpace(prof.Unp) ? "УНП не задан" : ("УНП " + prof.Unp.Trim());
        }

        private void UpdateOverviewTokenStatus(List<CertInfo> certs, bool avestInstalled)
        {
            if (ovTokenStatusText == null || ovTokenDot == null) return;
            if (!avestInstalled)
            {
                ovTokenDot.Fill = GetBrush("#FF3B30");
                ovTokenStatusText.Text = "Avest CSP не установлен — перейдите в «ГосПорталы» для установки";
                ovTokenStatusText.Foreground = GetBrush("#DC2626");
                return;
            }
            if (certs == null || certs.Count == 0)
            {
                ovTokenDot.Fill = GetBrush("#FF9500");
                ovTokenStatusText.Text = "USB-токен ЭЦП не обнаружен (криптопровайдер Avest активен ✓)";
                ovTokenStatusText.Foreground = GetBrush("#D97706");
                return;
            }
            var first = certs[0];
            if (!first.IsValid)
            {
                ovTokenDot.Fill = GetBrush("#FF3B30");
                ovTokenStatusText.Text = string.Format("Подключён ключ {0} — СРОК ИСТЁК ({1})", first.OrgName, first.ValidTo.ToString("dd.MM.yyyy"));
                ovTokenStatusText.Foreground = GetBrush("#DC2626");
            }
            else if (first.DaysRemaining <= 30)
            {
                ovTokenDot.Fill = GetBrush("#FF9500");
                ovTokenStatusText.Text = string.Format("Подключён: {0} (осталось {1} дн., до {2})", first.OrgName, first.DaysRemaining, first.ValidTo.ToString("dd.MM.yyyy"));
                ovTokenStatusText.Foreground = GetBrush("#D97706");
            }
            else
            {
                ovTokenDot.Fill = GetBrush("#34C759");
                string extra = certs.Count > 1 ? string.Format(" (+ ещё {0})", certs.Count - 1) : "";
                ovTokenStatusText.Text = string.Format("Подключён: {0}{1} • до {2} ({3} дн.)", first.OrgName, extra, first.ValidTo.ToString("dd.MM.yyyy"), first.DaysRemaining);
                ovTokenStatusText.Foreground = GetBrush("#16A34A");
            }
        }

        private void UpdateOverviewPortalPing(int index, bool ok, long ms)
        {
            System.Windows.Shapes.Ellipse dot = null;
            TextBlock lbl = null;
            if (index == 0) { dot = ovDotMns; lbl = ovPingMns; }
            else if (index == 1) { dot = ovDotFszn; lbl = ovPingFszn; }
            else if (index == 2) { dot = ovDotVat; lbl = ovPingVat; }
            else if (index == 3) { dot = ovDotEpasluga; lbl = ovPingEpasluga; }
            if (dot == null || lbl == null) return;

            if (ok)
            {
                dot.Fill = GetBrush("#34C759");
                lbl.Text = string.Format("{0} мс", ms);
                lbl.Foreground = GetBrush("#16A34A");
            }
            else
            {
                dot.Fill = GetBrush("#FF3B30");
                lbl.Text = "сбой";
                lbl.Foreground = GetBrush("#DC2626");
            }
        }

        private void RefreshOverviewAlerts()
        {
            if (ovCertAlertBox == null || ovCertAlertText == null) return;
            var all = CertRegistryManager.LoadRegistry();
            var expiring = new List<CertInfo>();
            foreach (var c in all)
            {
                if (c.DaysRemaining <= 30) expiring.Add(c);
            }

            if (expiring.Count == 0)
            {
                ovCertAlertBox.Visibility = Visibility.Collapsed;
                return;
            }

            expiring.Sort(delegate(CertInfo a, CertInfo b) { return a.DaysRemaining.CompareTo(b.DaysRemaining); });
            var first = expiring[0];
            bool urgent = first.DaysRemaining <= 14;
            ovCertAlertBox.Background = GetBrush(urgent ? "#FEF2F2" : "#FFFBEB");
            ovCertAlertBox.BorderBrush = GetBrush(urgent ? "#FECACA" : "#FDE68A");
            ovCertAlertText.Foreground = GetBrush(urgent ? "#B91C1C" : "#B45309");

            string statusWord = first.DaysRemaining <= 0
                ? "ПРОСРОЧЕН"
                : string.Format("истекает через {0} дн.", first.DaysRemaining);
            ovCertAlertText.Text = expiring.Count == 1
                ? string.Format("⚠️ ЭЦП «{0}» {1} ({2}) — пора продлить в РУЦ ГосСУОК.", first.OrgName, statusWord, first.ValidTo.ToString("dd.MM.yyyy"))
                : string.Format("⚠️ Истекающих/просроченных ключей ЭЦП: {0} шт. Ближайший: «{1}» ({2}).", expiring.Count, first.OrgName, statusWord);
            ovCertAlertBox.Visibility = Visibility.Visible;
        }

        private void RefreshOverviewCalendar()
        {
            if (ovCalendarRowsStack == null) return;
            ovCalendarRowsStack.Children.Clear();
            if (ovCalendarFutureStack != null) ovCalendarFutureStack.Children.Clear();

            var today = DateTime.Today;
            var curMonth = new DateTime(today.Year, today.Month, 1);
            var nextMonth = curMonth.AddMonths(1);
            var prof = ActiveCompanyProfile;
            string regime = prof != null ? prof.TaxRegime : CompanyProfilesManager.RegimeUsn6;

            var futureItems = new List<UIElement>();
            var primaryItems = BuildRbCalendarItems(today, curMonth, nextMonth, regime, futureItems);
            foreach (var item in primaryItems)
            {
                ovCalendarRowsStack.Children.Add(item);
            }
            if (ovCalendarFutureStack != null)
            {
                foreach (var fItem in futureItems)
                {
                    ovCalendarFutureStack.Children.Add(fItem);
                }
                ovCalendarFutureStack.Visibility = (_ovCalendarFutureExpanded && futureItems.Count > 0)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            if (ovBtnToggleFutureCalendar != null)
            {
                ovBtnToggleFutureCalendar.Visibility = futureItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ovBtnToggleFutureCalendar.Content = _ovCalendarFutureExpanded
                    ? "Скрыть следующие месяцы и квартал ▲"
                    : "Показать следующие месяцы и квартал ▼";
            }
        }

        private List<UIElement> BuildRbCalendarItems(DateTime today, DateTime curMonth, DateTime nextMonth, string regime, List<UIElement> futureList)
        {
            var primaryList = new List<UIElement>();
            var months = new DateTime[] { curMonth, nextMonth };

            // Determine nearest quarterly reporting month (Jan, Apr, Jul, Oct)
            bool curOrNextIsQuarterMonth = (curMonth.Month % 3 == 1) || (nextMonth.Month % 3 == 1);

            foreach (var m in months)
            {
                bool isCurrent = (m.Year == curMonth.Year && m.Month == curMonth.Month);
                var target = isCurrent ? primaryList : futureList;

                string monthTitle = m.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU"));
                if (monthTitle.Length > 0)
                    monthTitle = char.ToUpper(monthTitle[0]) + monthTitle.Substring(1);

                target.Add(new TextBlock
                {
                    Text = isCurrent ? (monthTitle + " (текущий месяц)") : (monthTitle + " (следующий месяц)"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetBrush("#007AFF"),
                    Margin = new Thickness(0, target.Count == 0 ? 2 : 10, 0, 6)
                });

                // 15th: Payroll + FSZN + Income tax 13%
                var d15 = new DateTime(m.Year, m.Month, 15);
                string payrollTag = regime == CompanyProfilesManager.RegimeIpPn20 ? "При найме (день ЗП)" : "День выплаты ЗП";
                target.Add(CreateCalendarRow(d15, today,
                    "Выплата ЗП и уплата взносов ФСЗН",
                    "Уплата обязательных страховых взносов в ФСЗН (35%) — не позднее установленного дня выплаты зарплаты.",
                    payrollTag));
                target.Add(CreateCalendarRow(d15, today,
                    "Подоходный налог 13% с ЗП",
                    "Перечисление удержанного подоходного налога (13%) в бюджет — не позднее дня фактической выплаты зарплаты.",
                    payrollTag));

                // 20th: Import VAT 20% (EAEU / RF) + regular VAT
                var d20 = new DateTime(m.Year, m.Month, 20);
                target.Add(CreateCalendarRow(d20, today,
                    "Ввозной НДС 20% (закупки РФ / ЕАЭС)",
                    "Заявление о ввозе товаров и уплата ввозного НДС 20% — обязательно даже для УСН без НДС и ИП.",
                    "При импорте из ЕАЭС"));

                if (regime == CompanyProfilesManager.RegimeOsn20)
                {
                    target.Add(CreateCalendarRow(d20, today,
                        "Декларация и уплата НДС (20%)",
                        "Ежемесячная/квартальная подача декларации по НДС и уплата налога.",
                        "ОСН"));
                }

                // Quarterly reporting months: Jan, Apr, Jul, Oct
                if (m.Month % 3 == 1)
                {
                    string qLabel = GetPreviousQuarterLabel(m);
                    string declTitle = regime == CompanyProfilesManager.RegimeIpPn20
                        ? "Декларация ИП (подоходный 20%) (4-Фонд — только при найме работников)"
                        : (regime == CompanyProfilesManager.RegimeOsn20
                            ? "Декларация на прибыль / 4-Фонд"
                            : "Декларация УСН 6% и отчёт 4-Фонд");
                    string declDesc = regime == CompanyProfilesManager.RegimeIpPn20
                        ? "Сдача декларации по подоходному налогу ИП в МНС. Отчёт 4-Фонд в ФСЗН сдаётся только при наличии наёмных работников."
                        : "Сдача налоговой декларации в МНС и отчёта 4-Фонд в ФСЗН.";
                    target.Add(CreateCalendarRow(d20, today,
                        declTitle + " за " + qLabel,
                        declDesc,
                        "Квартально"));

                    var d22 = new DateTime(m.Year, m.Month, 22);
                    string payTitle = regime == CompanyProfilesManager.RegimeIpPn20
                        ? "Уплата подоходного налога ИП"
                        : (regime == CompanyProfilesManager.RegimeOsn20
                            ? "Уплата налога на прибыль"
                            : "Уплата налога при УСН 6%");
                    target.Add(CreateCalendarRow(d22, today,
                        payTitle + " за " + qLabel,
                        "Перечисление рассчитанного квартального налога в бюджет.",
                        "Квартально"));

                    var d25 = new DateTime(m.Year, m.Month, 25);
                    target.Add(CreateCalendarRow(d25, today,
                        "Уплата взносов в Белгосстрах за " + qLabel,
                        "Ежеквартальная уплата страховых взносов по обязательному страхованию от несчастных случаев.",
                        regime == CompanyProfilesManager.RegimeIpPn20 ? "При найме" : "Квартально"));

                    var dLast = new DateTime(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month));
                    target.Add(CreateCalendarRow(dLast, today,
                        "Квартальная форма ПУ-3 за " + qLabel,
                        "Подача индивидуальных сведений ПУ-3 в ФСЗН по наёмным работникам (только при наличии работников по трудовым договорам/ГПД).",
                        "При найме"));
                }

                // Annual IP FSZN self-employed deadline: March 31
                if (regime == CompanyProfilesManager.RegimeIpPn20 && m.Month == 3)
                {
                    target.Add(CreateIpAnnualFsznRow(m.Year, today));
                }
            }

            if (!curOrNextIsQuarterMonth)
            {
                // Find the next quarter reporting month (Jan/Apr/Jul/Oct)
                var qMonth = nextMonth.AddMonths(1);
                while (qMonth.Month % 3 != 1) qMonth = qMonth.AddMonths(1);
                string qLabel = GetPreviousQuarterLabel(qMonth);

                futureList.Add(new TextBlock
                {
                    Text = "Ближайшие квартальные сроки (" + qLabel + ")",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetBrush("#5856D6"),
                    Margin = new Thickness(0, 10, 0, 6)
                });

                var q20 = new DateTime(qMonth.Year, qMonth.Month, 20);
                var q22 = new DateTime(qMonth.Year, qMonth.Month, 22);
                var q25 = new DateTime(qMonth.Year, qMonth.Month, 25);
                var qLast = new DateTime(qMonth.Year, qMonth.Month, DateTime.DaysInMonth(qMonth.Year, qMonth.Month));
                string qDeclTitle = regime == CompanyProfilesManager.RegimeIpPn20
                    ? "Декларация ИП (подоходный 20%) (4-Фонд — только при найме работников)"
                    : (regime == CompanyProfilesManager.RegimeOsn20
                        ? "Декларация на прибыль / 4-Фонд"
                        : "Декларация УСН 6% и отчёт 4-Фонд");
                string qDeclDesc = regime == CompanyProfilesManager.RegimeIpPn20
                    ? "Подача квартальной декларации ИП в МНС за " + qLabel + ". Отчёт 4-Фонд — только при найме работников."
                    : "Подача квартальной отчётности за " + qLabel + ".";
                futureList.Add(CreateCalendarRow(q20, today,
                    qDeclTitle,
                    qDeclDesc,
                    "Квартально"));
                futureList.Add(CreateCalendarRow(q22, today,
                    "Уплата налога за " + qLabel,
                    "Уплата налога по итогам квартала (УСН 6%, подоходный ИП 20% или налог на прибыль).",
                    "Квартально"));
                futureList.Add(CreateCalendarRow(q25, today,
                    "Уплата взносов в Белгосстрах за " + qLabel,
                    "Ежеквартальная уплата страховых взносов от несчастных случаев на производстве.",
                    regime == CompanyProfilesManager.RegimeIpPn20 ? "При найме" : "Квартально"));
                futureList.Add(CreateCalendarRow(qLast, today,
                    "Квартальная форма ПУ-3 за " + qLabel,
                    "Подача индивидуальных сведений ПУ-3 в ФСЗН по наёмным работникам (только при наличии работников по трудовым договорам/ГПД).",
                    "При найме"));
            }

            if (regime == CompanyProfilesManager.RegimeIpPn20 && curMonth.Month != 3 && nextMonth.Month != 3)
            {
                int fsznDueYear = curMonth.Month < 3 ? curMonth.Year : curMonth.Year + 1;
                futureList.Add(new TextBlock
                {
                    Text = "Ближайший годовой срок ИП (ФСЗН «за себя» за " + (fsznDueYear - 1) + " г.)",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetBrush("#B45309"),
                    Margin = new Thickness(0, 10, 0, 6)
                });
                futureList.Add(CreateIpAnnualFsznRow(fsznDueYear, today));
            }

            return primaryList;
        }

        private Border CreateIpAnnualFsznRow(int fsznDueYear, DateTime today)
        {
            int reportYear = fsznDueYear - 1;
            var d31 = new DateTime(fsznDueYear, 3, 31);
            string limitDesc;
            if (reportYear == 2025)
            {
                limitDesc = "за 2025 г.: МЗП 726 BYN, база 12 МЗП = 8 712 BYN, макс. 3 049.20 BYN";
            }
            else if (reportYear == 2026)
            {
                limitDesc = "за 2026 г.: МЗП 858 BYN, база 12 МЗП = 10 296 BYN, макс. 3 603.60 BYN";
            }
            else if (reportYear == 2024)
            {
                limitDesc = "за 2024 г.: МЗП 626 BYN, база 12 МЗП = 7 512 BYN, макс. 2 629.20 BYN";
            }
            else
            {
                limitDesc = string.Format("за {0} г.: потолок 12 МЗП на {0} г. (для ориентира: за 2026 г. — база 12 МЗП = 10 296 BYN, макс. 3 603.60 BYN)", reportYear);
            }

            return CreateCalendarRow(d31, today,
                string.Format("Взносы ФСЗН ИП «за себя» за {0} г.", reportYear),
                string.Format("Годовая уплата 35% от чистого дохода ({0}, Закон № 90-З). Форма ПУ-3 «за себя» отменена — ФСЗН считает взнос по данным МНС.", limitDesc),
                "Годовой (ИП)");
        }

        private static string GetPreviousQuarterLabel(DateTime reportMonth)
        {
            if (reportMonth.Month == 1) return "IV кв. " + (reportMonth.Year - 1);
            if (reportMonth.Month == 4) return "I кв. " + reportMonth.Year;
            if (reportMonth.Month == 7) return "II кв. " + reportMonth.Year;
            return "III кв. " + reportMonth.Year;
        }

        private static bool IsRbOfficialHolidayOrWeekend(DateTime d)
        {
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                return true;
            int m = d.Month;
            int day = d.Day;
            if (m == 1 && (day == 1 || day == 2 || day == 7)) return true;
            if (m == 3 && day == 8) return true;
            if (m == 5 && (day == 1 || day == 9)) return true;
            if (m == 7 && day == 3) return true;
            if (m == 11 && day == 7) return true;
            if (m == 12 && day == 25) return true;
            return false;
        }

        private static DateTime AdjustWeekendToMonday(DateTime date, out bool shiftedFromWeekend)
        {
            DateTime cur = date;
            while (IsRbOfficialHolidayOrWeekend(cur))
            {
                cur = cur.AddDays(1);
            }
            shiftedFromWeekend = (cur.Date != date.Date);
            return cur;
        }

        private Border CreateCalendarRow(DateTime dueDate, DateTime today, string title, string desc, string tag)
        {
            bool shifted;
            DateTime effectiveDue = AdjustWeekendToMonday(dueDate, out shifted);
            int diff = (effectiveDue - today).Days;
            string badgeText;
            string badgeBg;
            string badgeFg;

            if (diff < 0)
            {
                badgeText = "срок прошёл";
                badgeBg = "#F2F2F7";
                badgeFg = "#8E8E93";
            }
            else if (diff == 0)
            {
                badgeText = shifted ? "СЕГОДНЯ (перенос с вых./праздн.)" : "СЕГОДНЯ";
                badgeBg = "#FEE2E2";
                badgeFg = "#DC2626";
            }
            else if (diff <= 5)
            {
                badgeText = string.Format("через {0} дн.", diff);
                badgeBg = "#FEF3C7";
                badgeFg = "#B45309";
            }
            else
            {
                badgeText = string.Format("через {0} дн.", diff);
                badgeBg = "#EBF3FF";
                badgeFg = "#007AFF";
            }

            var rowCard = new Border
            {
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dateStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            dateStack.Children.Add(new TextBlock
            {
                Text = "до " + effectiveDue.ToString("dd.MM"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            dateStack.Children.Add(new TextBlock
            {
                Text = shifted ? (tag + "\n(с " + dueDate.ToString("dd.MM") + ")") : tag,
                FontSize = 10,
                Foreground = GetBrush("#8E8E93"),
                TextWrapping = TextWrapping.Wrap
            });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                TextWrapping = TextWrapping.Wrap
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });

            var badge = new Border
            {
                Background = GetBrush(badgeBg),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush(badgeFg)
            };

            Grid.SetColumn(dateStack, 0);
            Grid.SetColumn(infoStack, 2);
            Grid.SetColumn(badge, 4);
            g.Children.Add(dateStack);
            g.Children.Add(infoStack);
            g.Children.Add(badge);
            rowCard.Child = g;
            return rowCard;
        }
    }
}
