using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private StackPanel registryListContainer;
        private TextBlock registrySummaryText;
        private Border registryUrgentBanner;
        private TextBlock registryUrgentText;
        private Border registryDuplicateBanner;
        private TextBlock registryDuplicateText;
        private List<CertInfo> registryCurrentDuplicates = new List<CertInfo>();
        private List<CertInfo> registryLastMerged = new List<CertInfo>();
        private ComboBox registryFilterCombo;
        private TextBox registrySearchBox;
        private TextBlock registrySearchPlaceholder;
        private Button registrySearchClearBtn;
        private Button registryRestoreHiddenBtn;

        private void BuildRegistryTab(StackPanel container)
        {
            var headerCard = new Border
            {
                Background = Brushes.White,
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
                Margin = new Thickness(0, 0, 0, 10)
            };
            hStack.Children.Add(registrySummaryText);

            // Row 1: 2 primary buttons
            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var scanBtn = CreateIOSButton("🔄 Обновить", "#007AFF", delegate
            {
                RefreshRegistryTab(true);
                ShowToast("✓ Ключи подтянуты из хранилища Windows");
            });
            scanBtn.Height = 36;
            scanBtn.FontSize = 12.5;

            var exportBtn = CreateIOSButton("📊 Экспорт в Excel/CSV", "#5856D6", delegate
            {
                ExportRegistryDialog();
            });
            exportBtn.Height = 36;
            exportBtn.FontSize = 12.5;

            Grid.SetColumn(scanBtn, 0);
            Grid.SetColumn(exportBtn, 2);
            btnRow.Children.Add(scanBtn);
            btnRow.Children.Add(exportBtn);
            hStack.Children.Add(btnRow);

            // Row 2: Search input + compact filter ComboBox
            var filterRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });

            var searchBorder = new Border
            {
                Background = GetBrush("#F2F2F7"),
                CornerRadius = new CornerRadius(10),
                BorderBrush = GetBrush("#D1D1D6"),
                BorderThickness = new Thickness(1),
                Height = 36,
                Padding = new Thickness(10, 0, 6, 0)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            registrySearchPlaceholder = new TextBlock
            {
                Text = "🔍 Поиск по названию или владельцу...",
                FontSize = 12,
                Foreground = GetBrush("#8E8E93"),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            registrySearchBox = new TextBox
            {
                FontSize = 12.5,
                Foreground = GetBrush("#1C1C1E"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            registrySearchClearBtn = new Button
            {
                Content = "✕",
                FontSize = 11,
                Foreground = GetBrush("#8E8E93"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                ToolTip = "Очистить поиск"
            };
            registrySearchClearBtn.Click += delegate
            {
                registrySearchBox.Text = "";
                registrySearchBox.Focus();
            };

            registrySearchBox.TextChanged += delegate
            {
                bool empty = string.IsNullOrEmpty(registrySearchBox.Text);
                registrySearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                registrySearchClearBtn.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
                RenderRegistryList(registryLastMerged);
            };

            Grid.SetColumn(registrySearchPlaceholder, 0);
            Grid.SetColumn(registrySearchBox, 0);
            Grid.SetColumn(registrySearchClearBtn, 1);
            searchGrid.Children.Add(registrySearchPlaceholder);
            searchGrid.Children.Add(registrySearchBox);
            searchGrid.Children.Add(registrySearchClearBtn);
            searchBorder.Child = searchGrid;

            registryFilterCombo = new ComboBox
            {
                Height = 36,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            registryFilterCombo.Items.Add("Все ключи");
            registryFilterCombo.Items.Add("В USB сейчас");
            registryFilterCombo.Items.Add("Истекающие ≤30 дн.");
            registryFilterCombo.SelectedIndex = 0;
            registryFilterCombo.SelectionChanged += delegate
            {
                RenderRegistryList(registryLastMerged);
            };

            Grid.SetColumn(searchBorder, 0);
            Grid.SetColumn(registryFilterCombo, 2);
            filterRow.Children.Add(searchBorder);
            filterRow.Children.Add(registryFilterCombo);
            hStack.Children.Add(filterRow);

            var subActionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var copyBtn = CreateSmallPillButton("📋 Сводка в буфер", "#F2F2F7", "#1C1C1E");
            copyBtn.Margin = new Thickness(0, 0, 8, 0);
            copyBtn.Click += delegate
            {
                var all = CertRegistryManager.LoadRegistry();
                if (all.Count == 0)
                {
                    ShowToast("Реестр пока пуст — вставьте ключ или нажмите «Обновить»");
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
            };
            subActionsPanel.Children.Add(copyBtn);

            registryRestoreHiddenBtn = CreateSmallPillButton("↩️ Показать скрытые ключи", "#F2F2F7", "#636366");
            registryRestoreHiddenBtn.Visibility = Visibility.Collapsed;
            registryRestoreHiddenBtn.Click += delegate
            {
                CertRegistryManager.ClearHidden();
                RefreshRegistryTab(true);
                ShowToast("✓ Скрытые ключи восстановлены");
            };
            subActionsPanel.Children.Add(registryRestoreHiddenBtn);
            hStack.Children.Add(subActionsPanel);

            headerCard.Child = hStack;
            container.Children.Add(headerCard);

            // Urgent banner for <=14 days or expired certs
            registryUrgentBanner = new Border
            {
                Background = GetBrush("#FEF2F2"),
                BorderBrush = GetBrush("#FECACA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            registryUrgentText = new TextBlock
            {
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#B91C1C"),
                TextWrapping = TextWrapping.Wrap
            };
            registryUrgentBanner.Child = registryUrgentText;
            container.Children.Add(registryUrgentBanner);

            // Auto-detected renewed certificate duplicate offer banner
            registryDuplicateBanner = new Border
            {
                Background = GetBrush("#EFF6FF"),
                BorderBrush = GetBrush("#BFDBFE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            var dupGrid = new Grid();
            dupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            dupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            registryDuplicateText = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1E40AF"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnCleanDups = CreateCompactActionButton("🧹 Убрать старые дубли", "#2563EB", "#FFFFFF");
            btnCleanDups.Click += delegate
            {
                var thumbs = new List<string>();
                foreach (var d in registryCurrentDuplicates) thumbs.Add(d.Thumbprint);
                CertRegistryManager.RemoveCertificates(thumbs, true);
                RefreshRegistryTab(false);
                RefreshOverviewAlerts();
                ShowToast("✓ Старые дубли сертификатов убраны: " + thumbs.Count);
            };
            Grid.SetColumn(registryDuplicateText, 0);
            Grid.SetColumn(btnCleanDups, 2);
            dupGrid.Children.Add(registryDuplicateText);
            dupGrid.Children.Add(btnCleanDups);
            registryDuplicateBanner.Child = dupGrid;
            container.Children.Add(registryDuplicateBanner);

            registryListContainer = new StackPanel();
            container.Children.Add(registryListContainer);
        }

        private void ExportRegistryDialog()
        {
            var all = (registryLastMerged != null && registryLastMerged.Count > 0)
                ? registryLastMerged
                : CertRegistryManager.LoadRegistry();
            if (all.Count == 0)
            {
                ShowToast("⚠️ Реестр пуст — нечего экспортировать");
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Экспорт реестра ЭЦП",
                FileName = "Реестр_ЭЦП_" + DateTime.Today.ToString("yyyy-MM-dd"),
                Filter = "Таблица Excel (*.xlsx)|*.xlsx|Файл CSV для Excel (*.csv)|*.csv"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    if (dlg.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        CertRegistryManager.ExportRegistryToXlsx(all, dlg.FileName);
                    }
                    else
                    {
                        CertRegistryManager.ExportRegistryToCsv(all, dlg.FileName);
                    }
                    WaybillEngine.OpenFileWithShell(dlg.FileName);
                    ShowToast("✓ Реестр экспортирован: " + System.IO.Path.GetFileName(dlg.FileName));
                }
                catch (Exception ex)
                {
                    ShowToast("⚠️ Ошибка экспорта: " + ex.Message);
                }
            }
        }

        private void RefreshRegistryTab(bool scanAllWindowsStore)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                var fresh = SystemDiagnoser.GetAllGovCertificates(!scanAllWindowsStore);
                var merged = CertRegistryManager.MergeAndGetAll(fresh);

                if (Dispatcher != null && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        registryLastMerged = merged;
                        RenderRegistryList(merged);
                        RefreshOverviewAlerts();
                    }));
                }
            });
        }

        private void RenderRegistryList(List<CertInfo> merged)
        {
            if (registryListContainer == null || registrySummaryText == null) return;
            registryListContainer.Children.Clear();
            if (merged == null) merged = new List<CertInfo>();

            int inUsb = 0;
            int expiring30 = 0;
            var urgent14 = new List<CertInfo>();
            foreach (var c in merged)
            {
                if (c.IsPhysicallyConnected) inUsb++;
                if (c.DaysRemaining <= 30) expiring30++;
                if (c.DaysRemaining <= 14) urgent14.Add(c);
            }

            registrySummaryText.Text = string.Format("Всего в базе: {0}  •  В USB сейчас: {1}  •  Истекает ≤30 дн.: {2}",
                merged.Count, inUsb, expiring30);

            int hiddenCount = CertRegistryManager.GetHiddenCount();
            if (registryRestoreHiddenBtn != null)
            {
                registryRestoreHiddenBtn.Visibility = hiddenCount > 0 ? Visibility.Visible : Visibility.Collapsed;
                registryRestoreHiddenBtn.Content = string.Format("↩️ Показать скрытые ключи ({0})", hiddenCount);
            }

            // Urgent <=14 days banner
            if (registryUrgentBanner != null && registryUrgentText != null)
            {
                if (urgent14.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat("🚨 Внимание! Ключи ЭЦП со сроком ≤14 дней или просроченные ({0} шт.):\n", urgent14.Count);
                    int shown = 0;
                    foreach (var u in urgent14)
                    {
                        if (shown >= 3)
                        {
                            sb.AppendFormat("… и ещё {0}.", urgent14.Count - shown);
                            break;
                        }
                        string dText = u.DaysRemaining <= 0 ? "просрочен" : string.Format("осталось {0} дн.", u.DaysRemaining);
                        sb.AppendFormat("• {0} — до {1} ({2})\n", u.OrgName, u.ValidTo.ToString("dd.MM.yyyy"), dText);
                        shown++;
                    }
                    registryUrgentText.Text = sb.ToString().TrimEnd();
                    registryUrgentBanner.Visibility = Visibility.Visible;
                }
                else
                {
                    registryUrgentBanner.Visibility = Visibility.Collapsed;
                }
            }

            // Duplicate detection banner
            registryCurrentDuplicates = CertRegistryManager.FindDuplicateOldCerts(merged);
            if (registryDuplicateBanner != null && registryDuplicateText != null)
            {
                if (registryCurrentDuplicates.Count > 0)
                {
                    var orgs = new List<string>();
                    foreach (var d in registryCurrentDuplicates)
                    {
                        if (!orgs.Contains(d.OrgName)) orgs.Add(d.OrgName);
                    }
                    registryDuplicateText.Text = string.Format(
                        "🔄 Обнаружен продлённый сертификат ({0}). В реестре остались старые дубли ({1} шт.) — можно убрать их в 1 клик.",
                        string.Join(", ", orgs.ToArray()), registryCurrentDuplicates.Count);
                    registryDuplicateBanner.Visibility = Visibility.Visible;
                }
                else
                {
                    registryDuplicateBanner.Visibility = Visibility.Collapsed;
                }
            }

            int filterIdx = registryFilterCombo != null ? registryFilterCombo.SelectedIndex : 0;
            string searchQ = registrySearchBox != null ? (registrySearchBox.Text ?? "").Trim() : "";

            var displayList = new List<CertInfo>();
            foreach (var c in merged)
            {
                if (filterIdx == 1 && !c.IsPhysicallyConnected) continue;
                if (filterIdx == 2 && c.DaysRemaining > 30) continue;
                if (!string.IsNullOrEmpty(searchQ))
                {
                    bool matchOrg = !string.IsNullOrEmpty(c.OrgName) && c.OrgName.IndexOf(searchQ, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchPerson = !string.IsNullOrEmpty(c.PersonName) && c.PersonName.IndexOf(searchQ, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matchOrg && !matchPerson) continue;
                }
                displayList.Add(c);
            }

            if (displayList.Count == 0)
            {
                string emptyMsg;
                if (!string.IsNullOrEmpty(searchQ))
                    emptyMsg = string.Format("По запросу «{0}» ключи не найдены.", searchQ);
                else if (filterIdx == 1)
                    emptyMsg = "Сейчас к ПК не подключен ни один USB-ключ ЭЦП.";
                else if (filterIdx == 2)
                    emptyMsg = "Нет ключей со сроком ≤30 дней — всё в порядке ✓";
                else
                    emptyMsg = "В базе пока нет ни одного ключа.\nВставьте USB-токен Avest или нажмите «🔄 Обновить», чтобы подтянуть уже установленные на этом ПК сертификаты.";

                var emptyCard = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(16),
                    BorderBrush = GetBrush("#E5E5EA"),
                    BorderThickness = new Thickness(1)
                };
                emptyCard.Child = new TextBlock
                {
                    Text = emptyMsg,
                    FontSize = 12,
                    Foreground = GetBrush("#636366"),
                    TextWrapping = TextWrapping.Wrap
                };
                registryListContainer.Children.Add(emptyCard);
                return;
            }

            foreach (var c in displayList)
            {
                var certItem = c;
                var card = new Border
                {
                    Background = Brushes.White,
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
                topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var orgLbl = new TextBlock
                {
                    Text = (c.IsCompany ? "🏢 " : "👤 ") + c.OrgName,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetBrush("#1C1C1E"),
                    TextWrapping = TextWrapping.Wrap
                };

                var daysBg = !c.IsValid ? "#FDE8E8" : (c.DaysRemaining <= 14 ? "#FEE2E2" : (c.DaysRemaining <= 30 ? "#FFF8EB" : "#E8F9EE"));
                var daysFg = !c.IsValid ? "#DC2626" : (c.DaysRemaining <= 14 ? "#B91C1C" : (c.DaysRemaining <= 30 ? "#D97706" : "#16A34A"));
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

                var btnRemove = CreateSmallPillButton("🗑️ Убрать", "#F2F2F7", "#636366");
                btnRemove.ToolTip = "Удалить/скрыть сертификат из реестра";
                btnRemove.VerticalAlignment = VerticalAlignment.Top;
                btnRemove.Click += delegate
                {
                    CertRegistryManager.RemoveCertificate(certItem.Thumbprint, true);
                    RefreshRegistryTab(false);
                    ShowToast("✓ Сертификат убран из реестра");
                };

                Grid.SetColumn(orgLbl, 0);
                Grid.SetColumn(daysPill, 1);
                Grid.SetColumn(btnRemove, 3);
                topGrid.Children.Add(orgLbl);
                topGrid.Children.Add(daysPill);
                topGrid.Children.Add(btnRemove);
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
        }
    }
}
