using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private List<string> anLoadedFiles = new List<string>();
        private Dictionary<string, decimal> anSkuCosts;
        private AnalyticsSnapshot anCurrentSnapshot;
        private string anLastExportPath;

        // UI controls for Analytics tab
        private Border anDropZone;
        private TextBlock anDropTitle;
        private TextBlock anDropSubtitle;
        private TextBox anTaxRateBox;
        private TextBlock anSkuCostsInfo;
        private Border anKpiCard;
        private StackPanel anKpiContent;
        private Border anSkuCard;
        private StackPanel anSkuRowsStack;
        private Button anBtnExport;
        private Button anBtnOpenFolder;

        private void BuildAnalyticsTab(StackPanel container)
        {
            anSkuCosts = AnalyticsEngine.LoadSkuCosts();

            // CARD 1: DROP ZONE, TAX SETTINGS & SKU COSTS
            var mainCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "📊  Аналитика WB, налог УСН и выписка банка (для 1С)",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Перетащите еженедельные отчёты WB (.zip / .xlsx), банковскую выписку (.xlsx) или целую папку за квартал — Бухли сверит выплаты, посчитает УСН и соберёт Excel-книгу для 1С и директора.",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            });

            anDropZone = new Border
            {
                Background = GetBrush("#F0F9FF"),
                BorderBrush = GetBrush("#7DD3FC"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 16, 14, 16),
                AllowDrop = true,
                Cursor = Cursors.Hand
            };
            var dropStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            anDropTitle = new TextBlock
            {
                Text = "📥  Перетащите отчёты WB (.zip/.xlsx), выписку или папку сюда",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#0284C7"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            anDropSubtitle = new TextBlock
            {
                Text = "дубликаты отчётов отсеются автоматически по номеру отчёта",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            dropStack.Children.Add(anDropTitle);
            dropStack.Children.Add(anDropSubtitle);
            anDropZone.Child = dropStack;

            anDropZone.MouseLeftButtonUp += delegate { SelectAnalyticsFilesDialog(); };
            anDropZone.DragEnter += OnAnalyticsDragOver;
            anDropZone.DragOver += OnAnalyticsDragOver;
            anDropZone.DragLeave += delegate { ResetAnalyticsDropVisual(false); };
            anDropZone.Drop += OnAnalyticsDrop;
            stack.Children.Add(anDropZone);

            // File action row
            var fileBtnRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnSelect = CreateCompactActionButton("📂  Выбрать файлы (.zip / .xlsx)", "#EBF5FF", "#0284C7");
            btnSelect.Click += delegate { SelectAnalyticsFilesDialog(); };
            Grid.SetColumn(btnSelect, 0);
            fileBtnRow.Children.Add(btnSelect);

            var btnClear = CreateCompactActionButton("✕ Очистить", "#F2F2F7", "#636366");
            btnClear.Padding = new Thickness(12, 0, 12, 0);
            btnClear.Click += delegate { ClearAnalyticsFiles(); };
            Grid.SetColumn(btnClear, 2);
            fileBtnRow.Children.Add(btnClear);
            stack.Children.Add(fileBtnRow);

            // Tax rate + SKU cost button row
            var configGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var taxStack = new StackPanel();
            taxStack.Children.Add(CreateFieldLabel("Ставка УСН, %"));
            anTaxRateBox = CreateStyledInput("6", "Ставка налога УСН от реализации Пр (обычно 6%)");
            anTaxRateBox.LostFocus += delegate { RecalculateAnalytics(); };
            anTaxRateBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) RecalculateAnalytics();
            };
            taxStack.Children.Add(anTaxRateBox);
            Grid.SetColumn(taxStack, 0);
            configGrid.Children.Add(taxStack);

            var costStack = new StackPanel();
            costStack.Children.Add(CreateFieldLabel("Закупочная себестоимость (для расчёта чистой прибыли)"));
            var costBtnRow = new Grid();
            costBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            costBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            costBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnSkuCosts = CreateCompactActionButton("💰  Себестоимость SKU", "#F3E8FF", "#7C3AED");
            btnSkuCosts.Click += delegate { OpenSkuCostsWindow(); };
            Grid.SetColumn(btnSkuCosts, 0);
            costBtnRow.Children.Add(btnSkuCosts);

            anSkuCostsInfo = new TextBlock
            {
                FontSize = 11,
                Foreground = GetBrush("#636366"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            UpdateSkuCostBadge();
            Grid.SetColumn(anSkuCostsInfo, 2);
            costBtnRow.Children.Add(anSkuCostsInfo);

            costStack.Children.Add(costBtnRow);
            Grid.SetColumn(costStack, 2);
            configGrid.Children.Add(costStack);
            stack.Children.Add(configGrid);

            // KPI Card (hidden until loaded)
            anKpiCard = new Border
            {
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = Visibility.Collapsed
            };
            anKpiContent = new StackPanel();
            anKpiCard.Child = anKpiContent;
            stack.Children.Add(anKpiCard);

            // SKU Table Card (hidden until WB reports loaded)
            anSkuCard = new Border
            {
                Background = GetBrush("#FFFFFF"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var skuCardStack = new StackPanel();
            skuCardStack.Children.Add(new TextBlock
            {
                Text = "Юнит-экономика по артикулам (топ по продажам)",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var skuScroll = new ScrollViewer
            {
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            anSkuRowsStack = new StackPanel();
            skuScroll.Content = anSkuRowsStack;
            skuCardStack.Children.Add(skuScroll);
            anSkuCard.Child = skuCardStack;
            stack.Children.Add(anSkuCard);

            // Action buttons
            var actionsGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            anBtnExport = CreateSolidActionButton("📊  Выгрузить Excel-сводку (для 1С и директора)", "#0284C7", Brushes.White);
            anBtnExport.Click += delegate { ExportAnalyticsReport(); };
            Grid.SetColumn(anBtnExport, 0);
            actionsGrid.Children.Add(anBtnExport);

            anBtnOpenFolder = CreateCompactActionButton("📁 Папка", "#F2F2F7", "#1C1C1E");
            anBtnOpenFolder.Height = 38;
            anBtnOpenFolder.Padding = new Thickness(12, 0, 12, 0);
            anBtnOpenFolder.Click += delegate { OpenAnalyticsFolder(); };
            Grid.SetColumn(anBtnOpenFolder, 2);
            actionsGrid.Children.Add(anBtnOpenFolder);

            stack.Children.Add(actionsGrid);
            mainCard.Child = stack;
            container.Children.Add(mainCard);
        }

        private void UpdateSkuCostBadge()
        {
            if (anSkuCostsInfo == null) return;
            int n = anSkuCosts != null ? anSkuCosts.Count : 0;
            anSkuCostsInfo.Text = n > 0
                ? string.Format("Задано для {0} артикул.", n)
                : "не задана (прибыль без закупки)";
        }

        private void OnAnalyticsDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                ResetAnalyticsDropVisual(true);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ResetAnalyticsDropVisual(bool active)
        {
            if (anDropZone == null) return;
            anDropZone.Background = GetBrush(active ? "#E0F2FE" : "#F0F9FF");
            anDropZone.BorderBrush = GetBrush(active ? "#0284C7" : "#7DD3FC");
        }

        private void OnAnalyticsDrop(object sender, DragEventArgs e)
        {
            ResetAnalyticsDropVisual(false);
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped != null && dropped.Length > 0) AddAnalyticsPaths(dropped);
        }

        private void SelectAnalyticsFilesDialog()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите отчёты WB (.zip / .xlsx) и выписку банка (.xlsx)",
                Filter = "Отчёты и выписки (*.zip;*.xlsx)|*.zip;*.xlsx|ZIP архивы (*.zip)|*.zip|Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true && dlg.FileNames != null && dlg.FileNames.Length > 0)
            {
                AddAnalyticsPaths(dlg.FileNames);
            }
        }

        private void AddAnalyticsPaths(IEnumerable<string> paths)
        {
            int added = 0;
            var candidateFiles = new List<string>();
            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (Directory.Exists(p))
                {
                    try
                    {
                        candidateFiles.AddRange(Directory.GetFiles(p, "*.zip", SearchOption.AllDirectories));
                        candidateFiles.AddRange(Directory.GetFiles(p, "*.xlsx", SearchOption.AllDirectories));
                    }
                    catch { }
                }
                else if (File.Exists(p) &&
                    (p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
                {
                    candidateFiles.Add(p);
                }
            }

            foreach (var f in candidateFiles)
            {
                string fn = Path.GetFileName(f);
                if (fn.StartsWith("~$")) continue;
                if (!anLoadedFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                {
                    anLoadedFiles.Add(f);
                    added++;
                }
            }

            if (added == 0 && anLoadedFiles.Count == 0)
            {
                ShowToast("⚠️ Нужны файлы .zip или .xlsx (отчёты WB или выписка банка)");
                return;
            }

            RecalculateAnalytics();
        }

        private void ClearAnalyticsFiles()
        {
            anLoadedFiles.Clear();
            anCurrentSnapshot = null;
            anDropTitle.Text = "📥  Перетащите отчёты WB (.zip/.xlsx), выписку или папку сюда";
            anDropSubtitle.Text = "дубликаты отчётов отсеются автоматически по номеру отчёта";
            anKpiCard.Visibility = Visibility.Collapsed;
            anSkuCard.Visibility = Visibility.Collapsed;
        }

        private decimal ParseTaxRate()
        {
            decimal val;
            if (anTaxRateBox != null)
            {
                var s = (anTaxRateBox.Text ?? "").Trim().Replace(",", ".");
                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val) && val >= 0m && val <= 50m)
                    return val;
            }
            return 6.0m;
        }

        private void RecalculateAnalytics()
        {
            if (anLoadedFiles.Count == 0)
            {
                anKpiCard.Visibility = Visibility.Collapsed;
                anSkuCard.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                decimal taxRate = ParseTaxRate();
                anCurrentSnapshot = AnalyticsEngine.AnalyzeFiles(anLoadedFiles, taxRate, anSkuCosts);
                RenderAnalyticsPreview();
            }
            catch (Exception ex)
            {
                ShowToast("❌ Ошибка разбора отчётов: " + ex.Message);
            }
        }

        private void RenderAnalyticsPreview()
        {
            if (anCurrentSnapshot == null) return;
            var s = anCurrentSnapshot;

            if (s.Reports.Count == 0 && s.BankTransactions.Count == 0)
            {
                anDropTitle.Text = string.Format("⚠️ Загружено файлов: {0}, но формат WB / банка не распознан", anLoadedFiles.Count);
                anDropSubtitle.Text = "проверьте, что это детальные отчёты реализации WB или выписка банка";
                anKpiCard.Visibility = Visibility.Collapsed;
                anSkuCard.Visibility = Visibility.Collapsed;
                return;
            }

            anDropTitle.Text = string.Format("✅ Отчётов WB: {0}  |  Операций банка: {1}", s.Reports.Count, s.BankTransactions.Count);
            anDropSubtitle.Text = s.DeduplicatedSkipped > 0
                ? string.Format("файлов: {0} (дубликатов пропущено: {1}) — можно докинуть ещё", anLoadedFiles.Count, s.DeduplicatedSkipped)
                : string.Format("файлов загружено: {0} — нажмите, чтобы добавить ещё", anLoadedFiles.Count);

            anKpiContent.Children.Clear();
            anKpiContent.Children.Add(new TextBlock
            {
                Text = "Ключевые показатели периода (BYN)",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (s.Reports.Count > 0)
            {
                AddKpiPairRow(anKpiContent,
                    "Реализация Пр (база УСН)", FmtByn(s.TotalRealizedP), "#1C1C1E",
                    string.Format("Продажи / возвраты: {0} / {1} шт", s.TotalSaleQty, s.TotalReturnQty));

                decimal wbCosts = s.TotalLogistics + s.TotalStorage + s.TotalPenalties + s.TotalHoldings + s.TotalAcceptance + s.TotalLoyalty;
                AddKpiPairRow(anKpiContent,
                    "Логистика, хранение, штрафы WB", FmtByn(wbCosts), "#B45309",
                    string.Format("Логистика: {0}  |  Хран.: {1}  |  Штрафы/удерж.: {2}",
                        FmtByn(s.TotalLogistics), FmtByn(s.TotalStorage), FmtByn(s.TotalPenalties + s.TotalHoldings)));

                AddKpiPairRow(anKpiContent,
                    "К перечислению на р/с от WB", FmtByn(s.TotalExpectedPayout), "#0284C7",
                    "точная формула: к перечислению за товар минус логистика, штрафы, хранение и удержания");

                AddKpiPairRow(anKpiContent,
                    string.Format("Ориентир налога УСН ({0:0.##}% от Пр)", s.TaxRatePercent),
                    FmtByn(s.EstimatedTaxByRealization), "#DC2626",
                    "база по отчётам реализации без учёта иных поступлений");

                string profitHex = s.EstimatedNetProfitBeforeFixed >= 0m ? "#16A34A" : "#DC2626";
                AddKpiPairRow(anKpiContent,
                    s.TotalCostOfGoods > 0m ? "Прибыль до ФОТ/аренды (с закупкой)" : "Прибыль до закупки, ФОТ и аренды",
                    FmtByn(s.EstimatedNetProfitBeforeFixed), profitHex,
                    s.TotalCostOfGoods > 0m ? ("Себестоимость проданных SKU: " + FmtByn(s.TotalCostOfGoods)) : "укажите себестоимость SKU для точной маржи");
            }

            if (s.BankTransactions.Count > 0)
            {
                anKpiContent.Children.Add(new Border
                {
                    Height = 1,
                    Background = GetBrush("#E5E5EA"),
                    Margin = new Thickness(0, 6, 0, 8)
                });

                AddKpiPairRow(anKpiContent,
                    "Поступило от WB (ИМВБРБ) на р/с", FmtByn(s.BankWbCredits), "#0F766E",
                    string.Format("Всего приход на р/с: {0}  |  Списано с р/с: {1}", FmtByn(s.BankTotalCredit), FmtByn(s.BankTotalDebet)));

                if (s.Reports.Count > 0)
                {
                    decimal diff = s.BankWbCredits - s.TotalExpectedPayout;
                    string diffPrefix = diff > 0m ? "+" : "";
                    string diffHex = Math.Abs(diff) < 1.0m ? "#16A34A" : "#B45309";
                    AddKpiPairRow(anKpiContent,
                        "Сверка р/с vs отчёты WB (разница)", diffPrefix + FmtByn(diff), diffHex,
                        Math.Abs(diff) < 1.0m ? "выплаты сошлись копейка в копейку" : "разница обычно из-за переходящих недель на границе периода");
                }

                AddKpiPairRow(anKpiContent,
                    "Налоги, ФСЗН и Белгосстрах с р/с", FmtByn(s.BankTaxesDebet), "#636366",
                    string.Format("Поставщики: {0}  |  Зарплата: {1}  |  Комиссия банка: {2}",
                        FmtByn(s.BankVendorsDebet), FmtByn(s.BankPayrollDebet), FmtByn(s.BankFeesDebet)));
            }

            anKpiCard.Visibility = Visibility.Visible;

            // SKU Unit Economics preview
            anSkuRowsStack.Children.Clear();
            if (s.Skus.Count > 0)
            {
                AddAnalyticsSkuHeader();
                int shown = 0;
                foreach (var sku in s.Skus)
                {
                    if (shown++ >= 60) break;
                    AddAnalyticsSkuRow(sku);
                }
                if (s.Skus.Count > 60)
                {
                    anSkuRowsStack.Children.Add(new TextBlock
                    {
                        Text = string.Format("… и ещё {0} артикулов (все выгрузятся в Excel)", s.Skus.Count - 60),
                        FontSize = 11,
                        Foreground = GetBrush("#8E8E93"),
                        Margin = new Thickness(4, 6, 4, 2)
                    });
                }
                anSkuCard.Visibility = Visibility.Visible;
            }
            else
            {
                anSkuCard.Visibility = Visibility.Collapsed;
            }
        }

        private void AddKpiPairRow(StackPanel parent, string label, string value, string valueHex, string subHint)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#3A3A3C")
            });
            if (!string.IsNullOrEmpty(subHint))
            {
                left.Children.Add(new TextBlock
                {
                    Text = subHint,
                    FontSize = 10,
                    Foreground = GetBrush("#8E8E93"),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            Grid.SetColumn(left, 0);
            row.Children.Add(left);

            var right = new TextBlock
            {
                Text = value + " BYN",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush(valueHex),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(right, 1);
            row.Children.Add(right);

            parent.Children.Add(row);
        }

        private Grid CreateSkuTableGrid()
        {
            var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            return g;
        }

        private void AddAnalyticsSkuHeader()
        {
            var hdr = CreateSkuTableGrid();
            hdr.Margin = new Thickness(0, 0, 0, 4);
            AddSkuCell(hdr, 0, "Артикул", FontWeights.Bold, "#8E8E93", HorizontalAlignment.Left);
            AddSkuCell(hdr, 1, "Пр/Вз", FontWeights.Bold, "#8E8E93", HorizontalAlignment.Center);
            AddSkuCell(hdr, 2, "От WB", FontWeights.Bold, "#8E8E93", HorizontalAlignment.Right);
            AddSkuCell(hdr, 3, "Закуп.", FontWeights.Bold, "#8E8E93", HorizontalAlignment.Right);
            AddSkuCell(hdr, 4, "Прибыль", FontWeights.Bold, "#8E8E93", HorizontalAlignment.Right);
            anSkuRowsStack.Children.Add(hdr);
        }

        private void AddAnalyticsSkuRow(SkuUnitStats sku)
        {
            var b = new Border
            {
                Background = GetBrush("#F9F9FB"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 3)
            };
            var row = CreateSkuTableGrid();

            string artText = string.IsNullOrEmpty(sku.Article) ? sku.Barcode : sku.Article;
            if (!string.IsNullOrEmpty(sku.Title)) b.ToolTip = sku.Title;

            AddSkuCell(row, 0, artText, FontWeights.SemiBold, "#1C1C1E", HorizontalAlignment.Left);
            AddSkuCell(row, 1, string.Format("{0}/{1}", sku.SaleQty, sku.ReturnQty), FontWeights.Normal, "#636366", HorizontalAlignment.Center);
            AddSkuCell(row, 2, FmtByn(sku.NetFromWb), FontWeights.Normal, "#0284C7", HorizontalAlignment.Right);
            AddSkuCell(row, 3, sku.TotalCost > 0m ? FmtByn(sku.TotalCost) : "—", FontWeights.Normal, "#636366", HorizontalAlignment.Right);
            AddSkuCell(row, 4, FmtByn(sku.NetProfit), FontWeights.Bold, sku.NetProfit >= 0m ? "#16A34A" : "#DC2626", HorizontalAlignment.Right);

            b.Child = row;
            anSkuRowsStack.Children.Add(b);
        }

        private static void AddSkuCell(Grid g, int col, string text, FontWeight fw, string hex, HorizontalAlignment align)
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = 11,
                FontWeight = fw,
                Foreground = new BrushConverter().ConvertFromString(hex) as Brush,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(tb, col);
            g.Children.Add(tb);
        }

        private static string FmtByn(decimal val)
        {
            return val.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private void ExportAnalyticsReport()
        {
            if (anCurrentSnapshot == null ||
                (anCurrentSnapshot.Reports.Count == 0 && anCurrentSnapshot.BankTransactions.Count == 0))
            {
                ShowToast("⚠️ Сначала перетащите отчёты WB (.zip/.xlsx) или выписку");
                return;
            }

            try
            {
                var outDir = GetAnalyticsOutputDirectory();
                Directory.CreateDirectory(outDir);
                string fileName = "Buhly_Отчет_WB_" + DateTime.Now.ToString("dd.MM.yyyy_HH-mm") + ".xlsx";
                string outPath = Path.Combine(outDir, fileName);

                AnalyticsEngine.ExportAnalyticsXlsx(anCurrentSnapshot, outPath);
                anLastExportPath = outPath;
                WaybillEngine.OpenFileWithShell(outPath);
                ShowToast("✅ Excel-сводка создана и открыта: " + fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать Excel-сводку:\n" + ex.Message,
                    "Ошибка выгрузки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAnalyticsFolder()
        {
            try
            {
                var outDir = GetAnalyticsOutputDirectory();
                Directory.CreateDirectory(outDir);
                Process.Start("explorer.exe", "\"" + outDir + "\"");
            }
            catch (Exception ex)
            {
                ShowToast("Ошибка открытия папки: " + ex.Message);
            }
        }

        private string GetAnalyticsOutputDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Buhly_Аналитика");
        }

        private void OpenSkuCostsWindow()
        {
            var win = new Window
            {
                Title = "Себестоимость товаров (BYN за 1 шт.)",
                Width = 500,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = GetBrush("#F2F2F7")
            };

            var root = new Grid { Margin = new Thickness(14) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hint = new TextBlock
            {
                Text = "Введите закупочную цену 1 единицы товара в BYN — Бухли сохранит её локально и вычтет при расчёте чистой прибыли. Либо импортируйте файл CSV/Excel (столбцы: Артикул; Цена).",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(hint, 0);
            root.Children.Add(hint);

            var importRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var btnImport = CreateSmallPillButton("📥  Импорт из CSV / TXT / Excel", "#EBF5FF", "#0284C7");
            importRow.Children.Add(btnImport);
            Grid.SetRow(importRow, 1);
            root.Children.Add(importRow);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White,
                Padding = new Thickness(8),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var listPanel = new StackPanel();
            scroll.Content = listPanel;
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);

            var inputs = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
            Action populateList = delegate
            {
                listPanel.Children.Clear();
                inputs.Clear();

                var keys = new HashSet<string>(anSkuCosts.Keys, StringComparer.OrdinalIgnoreCase);
                if (anCurrentSnapshot != null)
                {
                    foreach (var sku in anCurrentSnapshot.Skus)
                    {
                        var k = !string.IsNullOrEmpty(sku.Article) ? sku.Article : sku.Barcode;
                        if (!string.IsNullOrEmpty(k)) keys.Add(k);
                    }
                }

                if (keys.Count == 0)
                {
                    listPanel.Children.Add(new TextBlock
                    {
                        Text = "Сначала перетащите отчёты WB во вкладку — список артикулов появится автоматически, либо нажмите «Импорт».",
                        FontSize = 11.5,
                        Foreground = GetBrush("#8E8E93"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(4)
                    });
                    return;
                }

                foreach (var art in keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

                    var lbl = new TextBlock
                    {
                        Text = art,
                        FontSize = 12,
                        Foreground = GetBrush("#1C1C1E"),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(lbl, 0);
                    row.Children.Add(lbl);

                    decimal existing;
                    string textVal = anSkuCosts.TryGetValue(art, out existing) && existing > 0m
                        ? existing.ToString("0.00", CultureInfo.InvariantCulture)
                        : "";
                    var tb = CreateStyledInput(textVal, "BYN за 1 шт.");
                    tb.Padding = new Thickness(6, 4, 6, 4);
                    Grid.SetColumn(tb, 1);
                    row.Children.Add(tb);

                    inputs[art] = tb;
                    listPanel.Children.Add(row);
                }
            };

            populateList();

            btnImport.Click += delegate
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Импорт себестоимости SKU",
                    Filter = "Таблицы и текст (*.xlsx;*.csv;*.txt)|*.xlsx;*.csv;*.txt|Все файлы (*.*)|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    int imported = AnalyticsEngine.ImportSkuCostsFromTextOrCsv(dlg.FileName, anSkuCosts);
                    AnalyticsEngine.SaveSkuCosts(anSkuCosts);
                    populateList();
                    ShowToast(string.Format("📥 Загружено цен себестоимости: {0}", imported));
                }
            };

            var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnSave = CreateSolidActionButton("💾  Сохранить и пересчитать прибыль", "#7C3AED", Brushes.White);
            btnSave.Click += delegate
            {
                foreach (var kv in inputs)
                {
                    var raw = (kv.Value.Text ?? "").Trim().Replace(",", ".");
                    decimal val;
                    if (string.IsNullOrEmpty(raw))
                    {
                        anSkuCosts.Remove(kv.Key);
                    }
                    else if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out val) && val >= 0m)
                    {
                        anSkuCosts[kv.Key] = val;
                    }
                }
                AnalyticsEngine.SaveSkuCosts(anSkuCosts);
                UpdateSkuCostBadge();
                RecalculateAnalytics();
                win.Close();
                ShowToast("✅ Себестоимость сохранена");
            };
            Grid.SetColumn(btnSave, 0);
            footer.Children.Add(btnSave);

            var btnCancel = CreateCompactActionButton("Закрыть", "#E5E5EA", "#1C1C1E");
            btnCancel.Height = 38;
            btnCancel.Click += delegate { win.Close(); };
            Grid.SetColumn(btnCancel, 2);
            footer.Children.Add(btnCancel);

            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            win.Content = root;
            win.ShowDialog();
        }
    }
}
