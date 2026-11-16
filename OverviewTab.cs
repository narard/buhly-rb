using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private TextBlock ovProfileTitle;
        private TextBlock ovProfileMeta;
        private TextBlock ovTokenStatusText;
        private System.Windows.Shapes.Ellipse ovTokenDot;
        private TextBlock ovCertAlertText;
        private Border ovCertAlertBox;
        private StackPanel ovCalendarRowsStack;

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
            // HEADER CARD: Active Profile + Quick Actions
            var topCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var profileStack = new StackPanel();
            ovProfileTitle = new TextBlock
            {
                Text = "🏠 Сводка",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                TextWrapping = TextWrapping.Wrap
            };
            ovProfileMeta = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            profileStack.Children.Add(ovProfileTitle);
            profileStack.Children.Add(ovProfileMeta);

            var btnManageProfiles = CreateSmallPillButton("⚙️ Профили компаний", "#EBF3FF", "#007AFF");
            btnManageProfiles.VerticalAlignment = VerticalAlignment.Center;
            btnManageProfiles.Click += delegate { OpenProfileManagerWindow(); };

            Grid.SetColumn(profileStack, 0);
            Grid.SetColumn(btnManageProfiles, 1);
            topGrid.Children.Add(profileStack);
            topGrid.Children.Add(btnManageProfiles);
            topCard.Child = topGrid;
            container.Children.Add(topCard);

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
                Text = "Ближайшие сроки отчётности и платежей (при выпадении на выходной день срок переносится на следующий рабочий день).",
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

            calCard.Child = calStack;
            container.Children.Add(calCard);

            RefreshOverviewProfileHeader();
            RefreshOverviewCalendar();
            RefreshOverviewAlerts();
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
                ovProfileTitle.Text = "🏠 Сводка";
                ovProfileMeta.Text = "Профиль не выбран";
                return;
            }
            ovProfileTitle.Text = "🏠 " + prof.DisplayName;
            string unpPart = string.IsNullOrWhiteSpace(prof.Unp) ? "УНП не задан" : ("УНП " + prof.Unp);
            ovProfileMeta.Text = unpPart + "  •  " + prof.GetTaxRegimeTitle();
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

            var today = DateTime.Today;
            var curMonth = new DateTime(today.Year, today.Month, 1);
            var nextMonth = curMonth.AddMonths(1);
            var prof = ActiveCompanyProfile;
            string regime = prof != null ? prof.TaxRegime : CompanyProfilesManager.RegimeUsn6;

            var items = BuildRbCalendarItems(today, curMonth, nextMonth, regime);
            foreach (var item in items)
            {
                ovCalendarRowsStack.Children.Add(item);
            }
        }

        private List<UIElement> BuildRbCalendarItems(DateTime today, DateTime curMonth, DateTime nextMonth, string regime)
        {
            var list = new List<UIElement>();
            var months = new DateTime[] { curMonth, nextMonth };

            // Determine nearest quarterly reporting month (Jan, Apr, Jul, Oct)
            bool curOrNextIsQuarterMonth = (curMonth.Month % 3 == 1) || (nextMonth.Month % 3 == 1);

            foreach (var m in months)
            {
                string monthTitle = m.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU"));
                if (monthTitle.Length > 0)
                    monthTitle = char.ToUpper(monthTitle[0]) + monthTitle.Substring(1);

                list.Add(new TextBlock
                {
                    Text = m.Month == curMonth.Month ? (monthTitle + " (текущий месяц)") : (monthTitle + " (следующий месяц)"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetBrush("#007AFF"),
                    Margin = new Thickness(0, list.Count == 0 ? 2 : 10, 0, 6)
                });

                // 15th: Payroll + FSZN
                var d15 = new DateTime(m.Year, m.Month, 15);
                list.Add(CreateCalendarRow(d15, today,
                    "Выплата ЗП и уплата взносов ФСЗН",
                    "Уплата обязательных страховых взносов в ФСЗН (35%) — не позднее установленного дня выплаты зарплаты.",
                    regime == CompanyProfilesManager.RegimeIpPn20 ? "При найме (день ЗП)" : "День выплаты ЗП"));

                // 20th: Import VAT 20% (EAEU / RF) + regular VAT
                var d20 = new DateTime(m.Year, m.Month, 20);
                list.Add(CreateCalendarRow(d20, today,
                    "Ввозной НДС 20% (закупки РФ / ЕАЭС)",
                    "Заявление о ввозе товаров и уплата ввозного НДС 20% — обязательно даже для УСН без НДС и ИП.",
                    "При импорте из ЕАЭС"));

                if (regime == CompanyProfilesManager.RegimeOsn20)
                {
                    list.Add(CreateCalendarRow(d20, today,
                        "Декларация и уплата НДС (20%)",
                        "Ежемесячная/квартальная подача декларации по НДС и уплата налога.",
                        "ОСН"));
                }

                // Quarterly reporting months: Jan, Apr, Jul, Oct
                if (m.Month % 3 == 1)
                {
                    string qLabel = GetPreviousQuarterLabel(m);
                    string declTitle = regime == CompanyProfilesManager.RegimeIpPn20
                        ? "Декларация ИП (подоходный 20%) и 4-Фонд"
                        : (regime == CompanyProfilesManager.RegimeOsn20
                            ? "Декларация на прибыль / 4-Фонд"
                            : "Декларация УСН 6% и отчёт 4-Фонд");
                    list.Add(CreateCalendarRow(d20, today,
                        declTitle + " за " + qLabel,
                        "Сдача налоговой декларации в МНС и отчёта 4-Фонд в ФСЗН.",
                        "Квартально"));

                    var d22 = new DateTime(m.Year, m.Month, 22);
                    string payTitle = regime == CompanyProfilesManager.RegimeIpPn20
                        ? "Уплата подоходного налога ИП"
                        : (regime == CompanyProfilesManager.RegimeOsn20
                            ? "Уплата налога на прибыль"
                            : "Уплата налога при УСН 6%");
                    list.Add(CreateCalendarRow(d22, today,
                        payTitle + " за " + qLabel,
                        "Перечисление рассчитанного квартального налога в бюджет.",
                        "Квартально"));

                    var d25 = new DateTime(m.Year, m.Month, 25);
                    list.Add(CreateCalendarRow(d25, today,
                        "Уплата взносов в Белгосстрах за " + qLabel,
                        "Ежеквартальная уплата страховых взносов по обязательному страхованию от несчастных случаев.",
                        regime == CompanyProfilesManager.RegimeIpPn20 ? "При найме" : "Квартально"));
                }
            }

            if (!curOrNextIsQuarterMonth)
            {
                // Find the next quarter reporting month (Jan/Apr/Jul/Oct)
                var qMonth = nextMonth.AddMonths(1);
                while (qMonth.Month % 3 != 1) qMonth = qMonth.AddMonths(1);
                string qLabel = GetPreviousQuarterLabel(qMonth);

                list.Add(new TextBlock
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
                list.Add(CreateCalendarRow(q20, today,
                    "Декларация (УСН / ПН ИП / Прибыль) и 4-Фонд",
                    "Подача квартальной отчётности за " + qLabel + ".",
                    "Квартально"));
                list.Add(CreateCalendarRow(q22, today,
                    "Уплата налога за " + qLabel,
                    "Уплата налога по итогам квартала (УСН 6%, подоходный ИП 20% или налог на прибыль).",
                    "Квартально"));
                list.Add(CreateCalendarRow(q25, today,
                    "Уплата взносов в Белгосстрах за " + qLabel,
                    "Ежеквартальная уплата страховых взносов от несчастных случаев на производстве.",
                    regime == CompanyProfilesManager.RegimeIpPn20 ? "При найме" : "Квартально"));
            }

            return list;
        }

        private static string GetPreviousQuarterLabel(DateTime reportMonth)
        {
            if (reportMonth.Month == 1) return "IV кв. " + (reportMonth.Year - 1);
            if (reportMonth.Month == 4) return "I кв. " + reportMonth.Year;
            if (reportMonth.Month == 7) return "II кв. " + reportMonth.Year;
            return "III кв. " + reportMonth.Year;
        }

        private static DateTime AdjustWeekendToMonday(DateTime date, out bool shiftedFromWeekend)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                shiftedFromWeekend = true;
                return date.AddDays(2);
            }
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                shiftedFromWeekend = true;
                return date.AddDays(1);
            }
            shiftedFromWeekend = false;
            return date;
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
                badgeText = shifted ? "СЕГОДНЯ (перенос с вых.)" : "СЕГОДНЯ";
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
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
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
