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
            stack.Children.Add(new TextBlock
            {
                Text = "📦  Мастер настройки для нового ПК",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Если компьютер чистый и программы еще не установлены:",
                FontSize = 12,
                Foreground = GetBrush("#636366"),
                Margin = new Thickness(0, 0, 0, 10)
            });

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
            stack.Children.Add(new TextBlock
            {
                Text = "🛡️  Откат сетевых настроек из резервной копии",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Если нужно вернуть исходный корпоративный прокси (AutoConfigURL / PAC) и файл hosts до изменений Buhly:",
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
            stack.Children.Add(resultLabel);

            Button rollbackBtn = null;
            rollbackBtn = CreateIOSButton("↩️  Откатить прокси (PAC) и hosts из бэкапа", "#636366", delegate
            {
                rollbackBtn.IsEnabled = false;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string details;
                    bool ok = SystemDiagnoser.RestoreProxyAndHostsFromBackup(out details);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        rollbackBtn.IsEnabled = true;
                        resultLabel.Text = details;
                        resultLabel.Foreground = GetBrush(ok ? "#16A34A" : "#DC2626");
                        resultLabel.Visibility = Visibility.Visible;
                        RefreshStatuses();
                        ShowToast(ok ? "✓ Настройки прокси и hosts откачены из бэкапа" : "⚠️ Откат завершён с замечаниями");
                    }));
                });
            });
            rollbackBtn.Height = 38;
            rollbackBtn.FontSize = 13;
            stack.Children.Add(rollbackBtn);

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
                Background = Brushes.Transparent,
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
    }
}
