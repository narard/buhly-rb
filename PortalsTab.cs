using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private StackPanel certCardsContainer;
        private TextBlock statusNTClient;
        private TextBlock statusAvTun;
        private TextBlock statusPac;
        private TextBlock statusHosts;
        private System.Windows.Shapes.Ellipse dotNTClient;
        private System.Windows.Shapes.Ellipse dotAvTun;
        private System.Windows.Shapes.Ellipse dotPac;
        private System.Windows.Shapes.Ellipse dotHosts;

        private TextBlock statusPortalMNS;
        private System.Windows.Shapes.Ellipse dotPortalMNS;
        private TextBlock statusPortalFSZN;
        private System.Windows.Shapes.Ellipse dotPortalFSZN;
        private TextBlock statusPortalVAT;
        private System.Windows.Shapes.Ellipse dotPortalVAT;
        private TextBlock statusPortalEpasluga;
        private System.Windows.Shapes.Ellipse dotPortalEpasluga;
        private Button portalsFixBtn;
        private bool _isRepairing = false;

        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (Dispatcher != null && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                Dispatcher.BeginInvoke(action);
            }
        }

        private void BuildPortalsTab(StackPanel container)
        {
            certCardsContainer = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            container.Children.Add(certCardsContainer);

            // Grid 2x2 for portal cards on wide window
            var portalsGrid = new Grid();
            portalsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            portalsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            portalsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            portalsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            portalsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Card 1: МНС (Налоговая)
            var cardMNS = CreatePortalCard("🏛️  Личный кабинет МНС (Налоговая)",
                "Авторизация юрлиц и ИП через ЕС ИФЮЛ (nalog.gov.by)",
                "Войти по ЭЦП в МНС", "#007AFF", delegate
                {
                    OpenMNS();
                }, out statusPortalMNS, out dotPortalMNS);
            Grid.SetRow(cardMNS, 0);
            Grid.SetColumn(cardMNS, 0);
            portalsGrid.Children.Add(cardMNS);

            // Card 2: ФСЗН
            var cardFSZN = CreatePortalCard("📑  Портал Фонда (ФСЗН 2.0)",
                "Отчеты ПУ-3, 4-Фонд и извещения (portal2.ssf.gov.by)",
                "Войти по ЭЦП в ФСЗН", "#34C759", delegate
                {
                    OpenFSZN();
                }, out statusPortalFSZN, out dotPortalFSZN);
            Grid.SetRow(cardFSZN, 0);
            Grid.SetColumn(cardFSZN, 2);
            portalsGrid.Children.Add(cardFSZN);

            // Card 3: ЭСЧФ (vat.gov.by) - 1 CLICK VIA IE/EDGE
            var cardVAT = CreatePortalCard("🧾  Портал ЭСЧФ (vat.gov.by)",
                "Электронные счета-фактуры по НДС (режим IE + ActiveX в 1 клик)",
                "Войти на портал ЭСЧФ", "#5856D6", delegate
                {
                    OpenVAT();
                }, out statusPortalVAT, out dotPortalVAT);
            Grid.SetRow(cardVAT, 1);
            Grid.SetColumn(cardVAT, 0);
            portalsGrid.Children.Add(cardVAT);

            // Card 4: Е-Паслуга (НЦЭУ)
            var cardEpasluga = CreatePortalCard("🌐  Портал Е-Паслуга (e-pasluga.by)",
                "Электронные услуги НЦЭУ и справки для бизнеса по ЭЦП",
                "Войти на Е-Паслуга", "#0A84FF", delegate
                {
                    OpenEpasluga();
                }, out statusPortalEpasluga, out dotPortalEpasluga);
            Grid.SetRow(cardEpasluga, 1);
            Grid.SetColumn(cardEpasluga, 2);
            portalsGrid.Children.Add(cardEpasluga);

            container.Children.Add(portalsGrid);

            // Card 5: Диагностика
            var diagCard = CreateDiagnosticsCard();
            container.Children.Add(diagCard);
        }

        private void RenderCertificateCard(List<CertInfo> certs)
        {
            if (certCardsContainer == null) return;
            certCardsContainer.Children.Clear();

            var border = new Border
            {
                Background = Brushes.White,
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

                    boxStack.Children.Add(new TextBlock
                    {
                        Text = "⚠️  На этом ПК не настроена ЭЦП (ПО ГосСУОК)",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = GetBrush("#B45309"),
                        Margin = new Thickness(0, 0, 0, 4)
                    });

                    boxStack.Children.Add(new TextBlock
                    {
                        Text = "Криптопровайдер Avest CSP не обнаружен. Без него система не может прочитать USB-ключ и зайти на госпорталы.",
                        FontSize = 11.5,
                        Foreground = GetBrush("#78350F"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 10)
                    });

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
                                RunOnUi(delegate
                                {
                                    progressBar.Value = pct;
                                    statusLabel.Text = status;
                                });
                            },
                            delegate(bool success, string msg)
                            {
                                RunOnUi(delegate
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
                                });
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
                    warnBox.Child = new TextBlock
                    {
                        Text = "⚠️ Вставьте USB-токен Avest в компьютер\nКлюч не обнаружен в USB-порте (криптопровайдер Avest активен ✓).",
                        FontSize = 12,
                        FontWeight = FontWeights.Medium,
                        Foreground = GetBrush("#D97706"),
                        TextWrapping = TextWrapping.Wrap
                    };
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
                        stack.Children.Add(new System.Windows.Shapes.Rectangle
                        {
                            Height = 1,
                            Fill = GetBrush("#E5E5EA"),
                            Margin = new Thickness(0, 10, 0, 10)
                        });
                    }

                    var itemStack = new StackPanel();
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
                    statusPill.Child = new TextBlock
                    {
                        Text = c.IsValid ? string.Format("Осталось {0} дн.", c.DaysRemaining) : "Просрочен",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = c.IsValid ? GetBrush("#16A34A") : GetBrush("#DC2626")
                    };

                    Grid.SetColumn(orgText, 0);
                    Grid.SetColumn(statusPill, 1);
                    titleGrid.Children.Add(orgText);
                    titleGrid.Children.Add(statusPill);
                    itemStack.Children.Add(titleGrid);

                    if (!string.IsNullOrEmpty(c.PersonName) && !c.OrgName.Contains(c.PersonName))
                    {
                        itemStack.Children.Add(new TextBlock
                        {
                            Text = (c.IsCompany ? "Директор: " : "Владелец: ") + c.PersonName,
                            FontSize = 12,
                            Foreground = GetBrush("#636366"),
                            Margin = new Thickness(20, 2, 0, 2)
                        });
                    }

                    itemStack.Children.Add(new TextBlock
                    {
                        Text = "📅 Действителен до: " + c.ValidTo.ToString("dd.MM.yyyy"),
                        FontSize = 12,
                        Foreground = GetBrush("#8E8E93"),
                        Margin = new Thickness(20, 2, 0, 0)
                    });

                    if (c.DaysRemaining <= 0)
                    {
                        var expBox = new Border
                        {
                            Background = GetBrush("#FDE8E8"),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(10, 6, 10, 6),
                            Margin = new Thickness(0, 8, 0, 0)
                        };
                        expBox.Child = new TextBlock
                        {
                            Text = "🔴 Срок действия ЭЦП истек! Вход на порталы невозможен. Продлите ключ в РУЦ ГосСУОК (nces.by).",
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = GetBrush("#DC2626"),
                            TextWrapping = TextWrapping.Wrap
                        };
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
                        expBox.Child = new TextBlock
                        {
                            Text = string.Format("⚠️ До окончания действия ЭЦП осталось {0} дн.! Пора продлевать сертификат в РУЦ ГосСУОК.", c.DaysRemaining),
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = GetBrush("#D97706"),
                            TextWrapping = TextWrapping.Wrap
                        };
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
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var dock = new DockPanel();
            var btn = CreateIOSButton(btnText, btnColor, onClick);
            DockPanel.SetDock(btn, Dock.Bottom);
            dock.Children.Add(btn);

            var topStack = new StackPanel();
            var headGrid = new Grid();
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var head = new TextBlock
            {
                Text = heading,
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
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
            topStack.Children.Add(headGrid);

            var sub = new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                Margin = new Thickness(0, 4, 0, 14),
                TextWrapping = TextWrapping.Wrap
            };
            topStack.Children.Add(sub);
            dock.Children.Add(topStack);

            border.Child = dock;
            return border;
        }

        private Border CreateDiagnosticsCard()
        {
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
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
                Background = Brushes.Transparent,
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

            stack.Children.Add(CreateStatusRow("Служба NTClient (МНС):", out statusNTClient, out dotNTClient));
            stack.Children.Add(CreateStatusRow("Шлюз AvTunProxy (ФСЗН):", out statusAvTun, out dotAvTun));
            stack.Children.Add(CreateStatusRow("Сетевой туннель PAC:", out statusPac, out dotPac));
            stack.Children.Add(CreateStatusRow("Файл hosts (госдомены):", out statusHosts, out dotHosts));

            var btnGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            portalsFixBtn = CreateIOSButton("⚡  Исправить всё", "#FF9500", delegate
            {
                TriggerRepairAll();
            });

            var detailsBtn = CreateIOSButton("🔍  Отчет", "#8E8E93", delegate
            {
                ShowDetailedReportWindow();
            });

            Grid.SetColumn(portalsFixBtn, 0);
            Grid.SetColumn(detailsBtn, 2);
            btnGrid.Children.Add(portalsFixBtn);
            btnGrid.Children.Add(detailsBtn);
            stack.Children.Add(btnGrid);

            border.Child = stack;
            return border;
        }

        private void TriggerRepairAll()
        {
            if (_isRepairing) return;
            _isRepairing = true;

            if (portalsFixBtn != null)
            {
                portalsFixBtn.IsEnabled = false;
                portalsFixBtn.Content = "⏳ Исправление...";
            }
            if (statusNTClient != null) statusNTClient.Text = "Перезапуск и проверка...";
            if (dotNTClient != null) dotNTClient.Fill = GetBrush("#FF9500");
            ShowToast("⚡ Перезапуск служб ЭЦП и настройка туннеля...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool hostsOk = true;
                try
                {
                    hostsOk = SystemDiagnoser.RepairAll();
                }
                finally
                {
                    if (Dispatcher != null && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    {
                        RunOnUi(delegate
                        {
                            try
                            {
                                if (portalsFixBtn != null)
                                {
                                    portalsFixBtn.IsEnabled = true;
                                    portalsFixBtn.Content = "⚡  Исправить всё";
                                }
                                RefreshStatuses();
                                CheckAllPortalsOnline();
                                ShowToast(hostsOk
                                    ? "✓ Службы перезапущены, порт проверен, туннель активен!"
                                    : "⚠️ Службы перезапущены, но файл hosts не изменён (отменено UAC / нет прав администратора)");
                            }
                            finally
                            {
                                _isRepairing = false;
                            }
                        });
                    }
                    else
                    {
                        _isRepairing = false;
                    }
                }
            });
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

        private void ShowDetailedReportWindow()
        {
            double maxH = SystemParameters.WorkArea.Height * 0.85;
            double maxW = SystemParameters.WorkArea.Width * 0.90;
            var win = new Window
            {
                Title = "Buhly • Детальный отчет системы",
                Width = Math.Min(480, maxW),
                Height = Math.Min(600, maxH),
                MaxHeight = maxH,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = GetBrush("#F2F2F7")
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "🔍 Чек-лист готовности",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var cardStack = new StackPanel();
            cardStack.Children.Add(new TextBlock
            {
                Text = "⏳ Сбор диагностики системы и ЭЦП...",
                FontSize = 13,
                Foreground = GetBrush("#636366")
            });

            string mnsOnline = statusPortalMNS != null ? statusPortalMNS.Text : "—";
            string fsznOnline = statusPortalFSZN != null ? statusPortalFSZN.Text : "—";
            string vatOnline = statusPortalVAT != null ? statusPortalVAT.Text : "—";
            string epaslugaOnline = statusPortalEpasluga != null ? statusPortalEpasluga.Text : "—";

            ThreadPool.QueueUserWorkItem(delegate
            {
                string timeDetails;
                bool timeOk = SystemDiagnoser.CheckTimeSync(out timeDetails);
                string hostsDetails;
                bool hostsOk = SystemDiagnoser.CheckHostsFile(out hostsDetails);
                string ntVer = SystemDiagnoser.GetNTClientVersion();
                bool ntPort = SystemDiagnoser.IsPort8084Healthy();
                string avVer = SystemDiagnoser.GetAvTunProxyVersion();
                bool avRun = SystemDiagnoser.IsAvTunProxyRunning();
                bool pac = SystemDiagnoser.IsPacEnabled();
                var certs = SystemDiagnoser.GetConnectedGovCertificates();

                RunOnUi(delegate
                {
                    cardStack.Children.Clear();
                    cardStack.Children.Add(CreateReportItem("🕒 Системное время:", timeDetails, timeOk));
                    cardStack.Children.Add(CreateReportItem("🛡️ Файл hosts:", hostsDetails, hostsOk));
                    cardStack.Children.Add(CreateReportItem("🏛️ NTClientSoftware (МНС):",
                        ntVer + (ntPort ? " (порт 8084 активен) ✓" : " (порт недоступен) ⚠️"), ntPort));
                    cardStack.Children.Add(CreateReportItem("📑 AvTunProxy (ФСЗН):",
                        avVer + (avRun ? " (работает) ✓" : " (не запущен) ⚠️"), avRun));
                    cardStack.Children.Add(CreateReportItem("🌐 Сетевой туннель:",
                        pac ? "proxy.pac активен ✓" : "proxy.pac выключен ⚠️", pac));
                    cardStack.Children.Add(CreateReportItem("🏛️ Сервер МНС (nalog.gov.by):", mnsOnline, !mnsOnline.Contains("сбой")));
                    cardStack.Children.Add(CreateReportItem("📑 Сервер ФСЗН (ssf.gov.by):", fsznOnline, !fsznOnline.Contains("сбой")));
                    cardStack.Children.Add(CreateReportItem("🧾 Сервер ЭСЧФ (vat.gov.by):", vatOnline, !vatOnline.Contains("сбой")));
                    cardStack.Children.Add(CreateReportItem("🌐 Сервер Е-Паслуга (e-pasluga.by):", epaslugaOnline, !epaslugaOnline.Contains("сбой")));

                    cardStack.Children.Add(new TextBlock
                    {
                        Text = "🔑 Подключенный ключ ГосСУОК:",
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = GetBrush("#1C1C1E"),
                        Margin = new Thickness(0, 10, 0, 4)
                    });

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
                            cardStack.Children.Add(new TextBlock
                            {
                                Text = string.Format("• {0} ({1})\n   Действует до: {2} (осталось {3} дн.)", c.OrgName, c.PersonName, c.ValidTo.ToString("dd.MM.yyyy"), c.DaysRemaining),
                                FontSize = 12,
                                Foreground = c.IsValid ? GetBrush("#34C759") : GetBrush("#FF3B30"),
                                Margin = new Thickness(10, 2, 0, 6)
                            });
                        }
                    }
                });
            });

            card.Child = cardStack;
            stack.Children.Add(card);

            var closeBtn = CreateIOSButton("Понятно", "#007AFF", delegate { win.Close(); });
            closeBtn.Margin = new Thickness(0, 16, 0, 0);
            stack.Children.Add(closeBtn);

            scroll.Content = stack;
            win.Content = scroll;
            win.ShowDialog();
        }

        private UIElement CreateReportItem(string title, string status, bool isOk)
        {
            var p = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            p.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E")
            });
            p.Children.Add(new TextBlock
            {
                Text = status,
                FontSize = 12,
                Foreground = isOk ? GetBrush("#34C759") : GetBrush("#FF9500"),
                Margin = new Thickness(10, 2, 0, 0)
            });
            return p;
        }

        private void OpenMNS()
        {
            string conflict = SystemDiagnoser.GetRunningConflictingApps();
            ShowToast("🏛️ Проверка компонентов и подключение к МНС...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool hasNt = SystemDiagnoser.FindNTClientPath() != null || SystemDiagnoser.IsPort8084Healthy();
                if (!hasNt)
                {
                    RunOnUi(delegate
                    {
                        ShowToast("⚠️ Не найден NTClientSoftware (МНС) — установите во вкладке «УНП и ПК»");
                        var res = MessageBox.Show(
                            this,
                            "Служба NTClientSoftware (МНС) не обнаружена на этом ПК.\n" +
                            "Без неё авторизация по ЭЦП на портале МНС не заработает (откроется пустая страница).\n\n" +
                            "Установите NTClientSoftware во вкладке «УНП и ПК» → «Мастер настройки для нового ПК».\n\n" +
                            "Всё равно открыть страницу МНС в браузере?",
                            "Buhly — Проверка компонентов МНС",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No);
                        if (res == MessageBoxResult.Yes)
                        {
                            OpenBrowser("https://lkfl.portal.nalog.gov.by/esifuyl");
                            RefreshStatuses();
                        }
                    });
                    return;
                }

                string hd;
                if (!SystemDiagnoser.IsPort8084Healthy() || !SystemDiagnoser.CheckHostsFile(out hd))
                {
                    SystemDiagnoser.RepairAll();
                }
                RunOnUi(delegate
                {
                    OpenBrowser("https://lkfl.portal.nalog.gov.by/esifuyl");
                    ShowToast(string.IsNullOrEmpty(conflict)
                        ? "🚀 Открываем Личный кабинет МНС..."
                        : "🚀 Открываем МНС (если ЭЦП занята — закройте " + conflict + ")");
                    RefreshStatuses();
                });
            });
        }

        private void OpenFSZN()
        {
            string conflict = SystemDiagnoser.GetRunningConflictingApps();
            ShowToast("📑 Проверка компонентов и подключение к ФСЗН 2.0...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool hasAvTun = SystemDiagnoser.FindAvTunProxyPath() != null || SystemDiagnoser.IsAvTunProxyRunning();
                if (!hasAvTun)
                {
                    RunOnUi(delegate
                    {
                        ShowToast("⚠️ Не найден AvTunProxy (ФСЗН) — установите во вкладке «УНП и ПК»");
                        var res = MessageBox.Show(
                            this,
                            "Шлюз AvTunProxy (ФСЗН) не обнаружен на этом ПК.\n" +
                            "Без него вход по ЭЦП на корпоративный портал ФСЗН 2.0 не сработает.\n\n" +
                            "Установите AvTunProxy в 1 клик во вкладке «УНП и ПК» → «Мастер настройки для нового ПК».\n\n" +
                            "Всё равно открыть страницу ФСЗН в браузере?",
                            "Buhly — Проверка компонентов ФСЗН",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No);
                        if (res == MessageBoxResult.Yes)
                        {
                            EdgeHelper.EnsureTrustedSitesAndActiveX();
                            OpenBrowser("https://portal2.ssf.gov.by/mainPage/");
                            RefreshStatuses();
                        }
                    });
                    return;
                }

                WinINetHelper.EnablePac("http://127.0.0.1:10224/proxy.pac");
                EdgeHelper.EnsureTrustedSitesAndActiveX();
                if (!SystemDiagnoser.IsAvTunProxyRunning())
                {
                    SystemDiagnoser.RepairAll();
                }
                RunOnUi(delegate
                {
                    OpenBrowser("https://portal2.ssf.gov.by/mainPage/");
                    ShowToast(string.IsNullOrEmpty(conflict)
                        ? "🚀 Открываем Корпоративный портал ФСЗН..."
                        : "🚀 Открываем ФСЗН (если ЭЦП занята — закройте " + conflict + ")");
                    RefreshStatuses();
                });
            });
        }

        private void OpenEpasluga()
        {
            string conflict = SystemDiagnoser.GetRunningConflictingApps();
            ShowToast("🌐 Проверка компонентов и подключение к Е-Паслуга...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool hasNt = SystemDiagnoser.FindNTClientPath() != null || SystemDiagnoser.IsPort8084Healthy();
                if (!hasNt)
                {
                    RunOnUi(delegate
                    {
                        ShowToast("⚠️ Не найден NTClientSoftware — установите во вкладке «УНП и ПК»");
                        var res = MessageBox.Show(
                            this,
                            "Служба NTClientSoftware (МНС / НЦЭУ) не обнаружена на этом ПК.\n" +
                            "Без неё авторизация по ЭЦП на портале Е-Паслуга может не сработать.\n\n" +
                            "Установите NTClientSoftware во вкладке «УНП и ПК» → «Мастер настройки для нового ПК».\n\n" +
                            "Всё равно открыть портал Е-Паслуга в браузере?",
                            "Buhly — Проверка компонентов Е-Паслуга",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No);
                        if (res == MessageBoxResult.Yes)
                        {
                            OpenBrowser("https://e-pasluga.by/");
                            RefreshStatuses();
                        }
                    });
                    return;
                }

                string hd;
                if (!SystemDiagnoser.IsPort8084Healthy() || !SystemDiagnoser.CheckHostsFile(out hd))
                {
                    SystemDiagnoser.RepairAll();
                }
                RunOnUi(delegate
                {
                    OpenBrowser("https://e-pasluga.by/");
                    ShowToast(string.IsNullOrEmpty(conflict)
                        ? "🚀 Открываем портал Е-Паслуга..."
                        : "🚀 Открываем Е-Паслуга (если ЭЦП занята — закройте " + conflict + ")");
                    RefreshStatuses();
                });
            });
        }

        private void OpenVAT()
        {
            string conflict = SystemDiagnoser.GetRunningConflictingApps();
            ShowToast("🚀 Подготовка и запуск ЭСЧФ в Microsoft Edge...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    bool edgeRunning = EdgeHelper.IsEdgeRunning();
                    bool prefsHadVat = EdgeHelper.HasVatInEdgePreferences();
                    EdgeHelper.EnsureVatInEdgeIEMode();
                    bool prefsHasVatAfter = EdgeHelper.HasVatInEdgePreferences();
                    bool needEdgeRestartHint = edgeRunning && !prefsHadVat && !prefsHasVatAfter;

                    if (needEdgeRestartHint)
                    {
                        RunOnUi(delegate
                        {
                            MessageBox.Show(
                                this,
                                "Microsoft Edge сейчас запущен (в т.ч. возможен фоновый режим Startup Boost в системном трее), а сайт vat.gov.by ещё не прописан в его профиле Preferences.\n\n" +
                                "На домашнем ПК политика реестра для режима IE может не примениться без полного перезапуска Edge.\n\n" +
                                "Если портал ЭСЧФ откроется без значка режима IE:\n" +
                                "1. Закройте все окна Microsoft Edge.\n" +
                                "2. Полностью завершите фоновый Edge через значок в трее (возле часов) или Диспетчер задач (msedge.exe).\n" +
                                "3. Нажмите «Войти на портал ЭСЧФ» повторно — Buhly пропишет vat.gov.by в профиль Edge.",
                                "Buhly — Режим IE в Microsoft Edge",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        });
                    }

                    var edgeCandidates = new string[]
                    {
                        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
                    };
                    foreach (var edge in edgeCandidates)
                    {
                        if (File.Exists(edge))
                        {
                            using (var p = Process.Start(edge, "http://vat.gov.by/mainPage/")) { }
                            RunOnUi(delegate
                            {
                                ShowToast(needEdgeRestartHint
                                    ? "⚠️ Закройте Edge полностью (в т.ч. в трее), если режим IE не применился"
                                    : (string.IsNullOrEmpty(conflict)
                                        ? "🚀 ЭСЧФ запущен в Microsoft Edge (режим IE)!"
                                        : "🚀 ЭСЧФ запущен (если ЭЦП занята — закройте " + conflict + ")"));
                                RefreshStatuses();
                            });
                            return;
                        }
                    }

                    using (var p = Process.Start(new ProcessStartInfo("microsoft-edge:http://vat.gov.by/mainPage/") { UseShellExecute = true })) { }
                    RunOnUi(delegate
                    {
                        ShowToast(string.IsNullOrEmpty(conflict)
                            ? "🚀 Запускаем ЭСЧФ в Microsoft Edge..."
                            : "🚀 Запускаем ЭСЧФ (если ЭЦП занята — закройте " + conflict + ")");
                        RefreshStatuses();
                    });
                }
                catch (Exception ex)
                {
                    RunOnUi(delegate
                    {
                        ShowToast("Ошибка запуска: " + ex.Message);
                    });
                }
            });
        }
    }
}
