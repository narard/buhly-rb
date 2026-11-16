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
        private bool registryFilterExpiringOnly = false;
        private Button registryFilterBtn;
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

            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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

            var exportBtn = CreateIOSButton("📥 Экспорт CSV/Excel", "#5856D6", delegate
            {
                ExportRegistryDialog();
            });
            exportBtn.Height = 36;
            exportBtn.FontSize = 12;

            registryFilterBtn = CreateCompactActionButton("⚠️ Истекающие ≤30 дн.", "#F2F2F7", "#1C1C1E");
            registryFilterBtn.Height = 36;
            registryFilterBtn.Padding = new Thickness(12, 0, 12, 0);
            registryFilterBtn.Click += delegate
            {
                registryFilterExpiringOnly = !registryFilterExpiringOnly;
                registryFilterBtn.Background = GetBrush(registryFilterExpiringOnly ? "#FFF3CD" : "#F2F2F7");
                registryFilterBtn.Foreground = GetBrush(registryFilterExpiringOnly ? "#B45309" : "#1C1C1E");
                RenderRegistryList(registryLastMerged);
            };

            Grid.SetColumn(scanBtn, 0);
            Grid.SetColumn(copyBtn, 2);
            Grid.SetColumn(exportBtn, 4);
            Grid.SetColumn(registryFilterBtn, 6);
            btnRow.Children.Add(scanBtn);
            btnRow.Children.Add(copyBtn);
            btnRow.Children.Add(exportBtn);
            btnRow.Children.Add(registryFilterBtn);
            hStack.Children.Add(btnRow);

            registryRestoreHiddenBtn = CreateSmallPillButton("↩️ Показать скрытые ключи", "#F2F2F7", "#636366");
            registryRestoreHiddenBtn.Margin = new Thickness(0, 8, 0, 0);
            registryRestoreHiddenBtn.HorizontalAlignment = HorizontalAlignment.Left;
            registryRestoreHiddenBtn.Visibility = Visibility.Collapsed;
            registryRestoreHiddenBtn.Click += delegate
            {
                CertRegistryManager.ClearHidden();
                RefreshRegistryTab(true);
                ShowToast("✓ Скрытые ключи восстановлены");
            };
            hStack.Children.Add(registryRestoreHiddenBtn);

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

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    registryLastMerged = merged;
                    RenderRegistryList(merged);
                    RefreshOverviewAlerts();
                }));
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

            var displayList = new List<CertInfo>();
            foreach (var c in merged)
            {
                if (registryFilterExpiringOnly && c.DaysRemaining > 30) continue;
                displayList.Add(c);
            }

            if (displayList.Count == 0)
            {
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
                    Text = registryFilterExpiringOnly
                        ? "Нет ключей со сроком ≤30 дней — всё в порядке ✓"
                        : "В базе пока нет ни одного ключа.\nВставьте USB-токен Avest или нажмите «🔄 Из Windows», чтобы подтянуть уже установленные на этом ПК сертификаты.",
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
