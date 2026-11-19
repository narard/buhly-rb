using System;
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
        private Border unpResultCard;
        private StackPanel unpResultStack;
        private TextBox unpInputBox;

        private StackPanel wizardBodyPanel;
        private TextBlock wizardHeaderTitle;
        private TextBlock wizardHeaderBadge;
        private bool wizardExpanded = true;
        private bool wizardUserToggled = false;
        private bool wizardChecking = false;
        private Button wizardStepAvestBtn;
        private Button wizardStepNtBtn;
        private Button wizardStepAvTunBtn;
        private Action wizardStepAvestAction;
        private Action wizardStepNtAction;
        private Action wizardStepAvTunAction;
        private ProgressBar wizardProgressBar;
        private TextBlock wizardStatusText;
        private Button wizardRecheckBtn;

        private void SafeToolsUi(Action action)
        {
            if (action == null) return;
            if (Dispatcher != null && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                Dispatcher.BeginInvoke(action);
            }
        }

        private void BuildToolsTab(StackPanel container)
        {
            container.Children.Add(CreateUnpCheckerCard());
            container.Children.Add(CreateFirstRunWizardCard());
            container.Children.Add(CreateNetworkRollbackCard());
        }

        private Border CreateUnpCheckerCard()
        {
            var card = new Border
            {
                Background = Brushes.White,
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
                Background = Brushes.Transparent,
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
                    SafeToolsUi(delegate
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
                    });
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

        private Border CreateFirstRunWizardCard()
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

            var headerGrid = new Grid { Cursor = Cursors.Hand };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            wizardHeaderTitle = new TextBlock
            {
                Text = "▼  Мастер настройки для нового ПК",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center
            };

            wizardHeaderBadge = new TextBlock
            {
                Text = "⏳ Проверка...",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#8E8E93"),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(wizardHeaderTitle, 0);
            Grid.SetColumn(wizardHeaderBadge, 1);
            headerGrid.Children.Add(wizardHeaderTitle);
            headerGrid.Children.Add(wizardHeaderBadge);

            headerGrid.MouseLeftButtonUp += delegate
            {
                wizardUserToggled = true;
                SetWizardExpanded(!wizardExpanded);
            };
            stack.Children.Add(headerGrid);

            wizardBodyPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            wizardBodyPanel.Children.Add(new TextBlock
            {
                Text = "Если компьютер чистый и программы еще не установлены:",
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            wizardBodyPanel.Children.Add(CreateWizardStep("1. ПО «Авест» (ГосСУОК):", "⏳ Проверка...", false, out wizardStepAvestBtn, delegate
            {
                if (wizardStepAvestAction != null) wizardStepAvestAction();
            }));
            wizardBodyPanel.Children.Add(CreateWizardStep("2. NTClientSoftware (МНС):", "⏳ Проверка...", false, out wizardStepNtBtn, delegate
            {
                if (wizardStepNtAction != null) wizardStepNtAction();
            }));
            wizardBodyPanel.Children.Add(CreateWizardStep("3. AvTunProxy (ФСЗН):", "⏳ Проверка...", false, out wizardStepAvTunBtn, delegate
            {
                if (wizardStepAvTunAction != null) wizardStepAvTunAction();
            }));

            wizardProgressBar = new ProgressBar
            {
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 4),
                Foreground = GetBrush("#007AFF")
            };
            wizardBodyPanel.Children.Add(wizardProgressBar);

            wizardStatusText = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrush("#007AFF"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 6)
            };
            wizardBodyPanel.Children.Add(wizardStatusText);

            wizardRecheckBtn = CreateSmallPillButton("🔄 Проверить компоненты заново", "#EBF3FF", "#007AFF");
            wizardRecheckBtn.HorizontalAlignment = HorizontalAlignment.Left;
            wizardRecheckBtn.Margin = new Thickness(0, 8, 0, 0);
            wizardRecheckBtn.Click += delegate
            {
                RefreshToolsComponentsAsync(true);
            };
            wizardBodyPanel.Children.Add(wizardRecheckBtn);

            stack.Children.Add(wizardBodyPanel);
            border.Child = stack;

            // Run component detection asynchronously so MainWindow constructor stays instant
            RefreshToolsComponentsAsync(false);

            return border;
        }

        private void SetWizardExpanded(bool expanded)
        {
            wizardExpanded = expanded;
            if (wizardBodyPanel != null)
            {
                wizardBodyPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            }
            if (wizardHeaderTitle != null)
            {
                wizardHeaderTitle.Text = (expanded ? "▼" : "▶") + "  Мастер настройки для нового ПК";
            }
        }

        private void RefreshToolsComponentsAsync(bool keepCurrentExpandState)
        {
            if (wizardChecking) return;
            wizardChecking = true;
            if (wizardRecheckBtn != null) wizardRecheckBtn.IsEnabled = false;
            if (wizardHeaderBadge != null)
            {
                wizardHeaderBadge.Text = "⏳ Проверка...";
                wizardHeaderBadge.Foreground = GetBrush("#8E8E93");
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                bool avestOk = false;
                string ntVer = "Не установлена";
                bool ntInstalled = false;
                string localNT = null;
                string avVer = "Не установлена";
                bool avInstalled = false;

                try
                {
                    avestOk = SystemDiagnoser.IsAvestInstalled();
                    ntVer = SystemDiagnoser.GetNTClientVersion();
                    ntInstalled = ntVer != "Не установлена";
                    localNT = ntInstalled ? null : ComponentInstaller.FindLocalNTClientInstaller();
                    avVer = SystemDiagnoser.GetAvTunProxyVersion();
                    avInstalled = avVer != "Не установлена";
                }
                catch { }

                SafeToolsUi(delegate
                {
                    try
                    {
                        ApplyWizardStepState(wizardStepAvestBtn, avestOk ? "Установлено ✓" : "Скачать и установить (38 МБ)", avestOk);
                        wizardStepAvestAction = delegate
                        {
                            if (avestOk) return;
                            if (wizardStepAvestBtn != null) wizardStepAvestBtn.IsEnabled = false;
                            if (wizardProgressBar != null)
                            {
                                wizardProgressBar.Value = 0;
                                wizardProgressBar.Visibility = Visibility.Visible;
                            }
                            if (wizardStatusText != null)
                            {
                                wizardStatusText.Text = "Подключение к nces.by...";
                                wizardStatusText.Foreground = GetBrush("#007AFF");
                                wizardStatusText.Visibility = Visibility.Visible;
                            }
                            ShowToast("📥 Загрузка ПО ГосСУОК с официального сервера nces.by...");
                            ComponentInstaller.DownloadAndInstallGossuok(
                                delegate(string s, int p)
                                {
                                    SafeToolsUi(delegate
                                    {
                                        if (wizardProgressBar != null) wizardProgressBar.Value = p;
                                        if (wizardStatusText != null) wizardStatusText.Text = s;
                                    });
                                },
                                delegate(bool ok, string m)
                                {
                                    SafeToolsUi(delegate
                                    {
                                        if (wizardStepAvestBtn != null) wizardStepAvestBtn.IsEnabled = true;
                                        if (wizardStatusText != null)
                                        {
                                            wizardStatusText.Text = (ok ? "✓ " : "⚠️ ") + m;
                                            wizardStatusText.Foreground = GetBrush(ok ? "#16A34A" : "#FF3B30");
                                        }
                                        ShowToast(ok ? "✓ " + m : "⚠️ " + m);
                                        RefreshStatuses();
                                        RefreshToolsComponentsAsync(true);
                                    });
                                }
                            );
                        };

                        string ntActionText = ntInstalled ? ntVer + " ✓" : (localNT != null ? "Запустить с диска ⚡" : "Инструкция и сайт НЦЭУ");
                        ApplyWizardStepState(wizardStepNtBtn, ntActionText, ntInstalled);
                        wizardStepNtAction = delegate
                        {
                            if (ntInstalled) return;
                            if (localNT != null)
                            {
                                try
                                {
                                    using (var p = Process.Start(localNT)) { }
                                    ShowToast("⚡ Запуск найденного установщика: " + Path.GetFileName(localNT));
                                }
                                catch (Exception ex) { ShowToast("Ошибка запуска: " + ex.Message); }
                            }
                            else
                            {
                                OpenBrowser("https://nces.by/service/po/");
                                ShowToast("🌐 Открыт сайт НЦЭУ. Для загрузки укажите телефон для получения СМС.");
                            }
                        };

                        ApplyWizardStepState(wizardStepAvTunBtn, avInstalled ? avVer + " ✓" : "Скачать и установить (8 МБ)", avInstalled);
                        wizardStepAvTunAction = delegate
                        {
                            if (avInstalled) return;
                            if (wizardStepAvTunBtn != null) wizardStepAvTunBtn.IsEnabled = false;
                            if (wizardProgressBar != null)
                            {
                                wizardProgressBar.Value = 0;
                                wizardProgressBar.Visibility = Visibility.Visible;
                            }
                            if (wizardStatusText != null)
                            {
                                wizardStatusText.Text = "Подключение к avtunproxy.by...";
                                wizardStatusText.Foreground = GetBrush("#007AFF");
                                wizardStatusText.Visibility = Visibility.Visible;
                            }
                            ShowToast("📥 Загрузка AvTunProxy с avtunproxy.by...");
                            ComponentInstaller.DownloadAndInstallAvTunProxy(
                                delegate(string s, int p)
                                {
                                    SafeToolsUi(delegate
                                    {
                                        if (wizardProgressBar != null) wizardProgressBar.Value = p;
                                        if (wizardStatusText != null) wizardStatusText.Text = s;
                                    });
                                },
                                delegate(bool ok, string m)
                                {
                                    SafeToolsUi(delegate
                                    {
                                        if (wizardStepAvTunBtn != null) wizardStepAvTunBtn.IsEnabled = true;
                                        if (wizardStatusText != null)
                                        {
                                            wizardStatusText.Text = (ok ? "✓ " : "⚠️ ") + m;
                                            wizardStatusText.Foreground = GetBrush(ok ? "#16A34A" : "#FF3B30");
                                        }
                                        ShowToast(ok ? "✓ " + m : "⚠️ " + m);
                                        RefreshStatuses();
                                        RefreshToolsComponentsAsync(true);
                                    });
                                }
                            );
                        };

                        bool allInstalled = avestOk && ntInstalled && avInstalled;
                        if (wizardHeaderBadge != null)
                        {
                            wizardHeaderBadge.Text = allInstalled ? "Все 3 установлены ✓" : "Требуется настройка";
                            wizardHeaderBadge.Foreground = GetBrush(allInstalled ? "#16A34A" : "#D97706");
                        }

                        if (!keepCurrentExpandState && !wizardUserToggled)
                        {
                            SetWizardExpanded(!allInstalled);
                        }
                    }
                    finally
                    {
                        wizardChecking = false;
                        if (wizardRecheckBtn != null) wizardRecheckBtn.IsEnabled = true;
                    }
                });
            });
        }

        private void ApplyWizardStepState(Button btn, string actionText, bool isOk)
        {
            if (btn == null) return;
            btn.Content = actionText;
            btn.Foreground = isOk ? GetBrush("#34C759") : GetBrush("#007AFF");
            btn.Cursor = isOk ? Cursors.Arrow : Cursors.Hand;
        }

        private Border CreateNetworkRollbackCard()
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

            bool rollbackExpanded = false;
            var headerGrid = new Grid { Cursor = Cursors.Hand };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerTitle = new TextBlock
            {
                Text = "▼ Откат сетевых настроек и Edge из бэкапа",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center
            };
            var toggleHint = new TextBlock
            {
                Text = "Развернуть",
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = GetBrush("#007AFF"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerTitle, 0);
            Grid.SetColumn(toggleHint, 1);
            headerGrid.Children.Add(headerTitle);
            headerGrid.Children.Add(toggleHint);
            stack.Children.Add(headerGrid);

            var bodyStack = new StackPanel
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0)
            };

            headerGrid.MouseLeftButtonUp += delegate
            {
                rollbackExpanded = !rollbackExpanded;
                bodyStack.Visibility = rollbackExpanded ? Visibility.Visible : Visibility.Collapsed;
                headerTitle.Text = (rollbackExpanded ? "▲" : "▼") + " Откат сетевых настроек и Edge из бэкапа";
                toggleHint.Text = rollbackExpanded ? "Свернуть" : "Развернуть";
            };

            bodyStack.Children.Add(new TextBlock
            {
                Text = "Восстанавливает исходные настройки до изменений Buhly: корпоративный прокси (AutoConfigURL / PAC), файл hosts, параметры зоны надёжных сайтов IE (Zones\\2) и настройки режима IE в Microsoft Edge (Preferences и политики реестра):",
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var resultLabel = new TextBlock
            {
                FontSize = 11.5,
                Foreground = GetBrush("#3A3A3C"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 10)
            };
            bodyStack.Children.Add(resultLabel);

            Button rollbackBtn = null;
            rollbackBtn = CreateIOSButton("↩️  Откатить PAC, hosts, Zones\\2 и Edge из бэкапа", "#636366", delegate
            {
                rollbackBtn.IsEnabled = false;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string details;
                    bool ok = SystemDiagnoser.RestoreProxyAndHostsFromBackup(out details);
                    SafeToolsUi(delegate
                    {
                        rollbackBtn.IsEnabled = true;
                        resultLabel.Text = details;
                        resultLabel.Foreground = GetBrush(ok ? "#16A34A" : "#DC2626");
                        resultLabel.Visibility = Visibility.Visible;
                        RefreshStatuses();
                        ShowToast(ok ? "✓ Настройки сети и Edge откачены из бэкапа" : "⚠️ Откат завершён с замечаниями");
                    });
                });
            });
            rollbackBtn.Height = 38;
            rollbackBtn.FontSize = 13;
            bodyStack.Children.Add(rollbackBtn);

            stack.Children.Add(bodyStack);
            border.Child = stack;
            return border;
        }

        private Grid CreateWizardStep(string label, string actionText, bool isOk, out Button actionBtn, Action onClick)
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

            actionBtn = new Button
            {
                Content = actionText,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = isOk ? GetBrush("#34C759") : GetBrush("#007AFF"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = isOk ? Cursors.Arrow : Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (onClick != null)
            {
                actionBtn.Click += delegate { onClick(); };
            }

            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(actionBtn, 1);
            g.Children.Add(lbl);
            g.Children.Add(actionBtn);
            return g;
        }
    }
}
