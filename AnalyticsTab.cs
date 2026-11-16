using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private List<string> anLoadedFiles = new List<string>();
        private Dictionary<string, decimal> anSkuCosts;
        private Dictionary<string, decimal> anDemoSkuCostsOverride;
        private AnalyticsSnapshot anCurrentSnapshot;
        private string anLastExportPath;
        private int _anAnalyzeSeq = 0;

        // UI controls for Analytics tab
        private Border anDropZone;
        private TextBlock anDropTitle;
        private TextBlock anDropSubtitle;
        private TextBox anTaxRateBox;
        private TextBlock anSkuCostsInfo;
        private Border anRegimeBanner;
        private TextBlock anRegimeBannerText;
        private Border anLoadingBanner;
        private TextBlock anLoadingText;
        private Border anKpiCard;
        private StackPanel anKpiContent;
        private Border anSkuCard;
        private TextBox anSkuSearchBox;
        private TextBlock anSkuCountText;
        private DataGrid anSkuGrid;
        private ICollectionView anSkuView;

        // Import VAT 20% calculator controls
        private DatePicker anImpDatePicker;
        private TextBox anImpRubBox;
        private TextBox anImpRateBox;
        private TextBlock anImpRateStatus;
        private TextBlock anImpBaseText;
        private TextBlock anImpVatText;
        private TextBlock anImpDueText;

        private void BuildAnalyticsTab(StackPanel container)
        {
            anSkuCosts = AnalyticsEngine.LoadSkuCosts();

            // CARD 1: DROP ZONE, TAX SETTINGS, KPI & SKU DATAGRID
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
                Text = "📊  Маркетплейсы (WB + Ozon), выписка банка, налоги РБ и 1С",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Перетащите отчёты WB / Ozon (.zip / .xlsx) и выписку любого банка РБ (.xlsx / 1CClientBankExchange .txt / .csv) — Бухли сверит выплаты, посчитает налог по профилю компании и подготовит КУДиР / 1С.",
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
                Padding = new Thickness(14, 15, 14, 15),
                AllowDrop = true,
                Cursor = Cursors.Hand
            };
            var dropStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            anDropTitle = new TextBlock
            {
                Text = "📥  Перетащите отчёты WB/Ozon (.zip/.xlsx), выписку (.xlsx/.txt/.csv) или папку",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#0284C7"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            anDropSubtitle = new TextBlock
            {
                Text = "поддерживаются все банки РБ (Excel и 1CClientBankExchange) • дубликаты отсеются автоматически",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
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

            // File action row: Choose / Demo / Clear
            var fileBtnRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnSelect = CreateCompactActionButton("📂  Выбрать файлы (.zip/.xlsx/.txt/.csv)", "#EBF5FF", "#0284C7");
            btnSelect.Click += delegate { SelectAnalyticsFilesDialog(); };
            Grid.SetColumn(btnSelect, 0);
            fileBtnRow.Children.Add(btnSelect);

            var btnDemo = CreateCompactActionButton("⚡ Попробовать на демо-данных", "#FFFBEB", "#B45309");
            btnDemo.Padding = new Thickness(11, 0, 11, 0);
            btnDemo.ToolTip = "Загрузить синтетический пример WB + Ozon + выписка 1С (без личных данных)";
            btnDemo.Click += delegate { LoadDemoAnalyticsFiles(); };
            Grid.SetColumn(btnDemo, 2);
            fileBtnRow.Children.Add(btnDemo);

            var btnClear = CreateCompactActionButton("✕ Очистить", "#F2F2F7", "#636366");
            btnClear.Padding = new Thickness(12, 0, 12, 0);
            btnClear.Click += delegate { ClearAnalyticsFiles(); };
            Grid.SetColumn(btnClear, 4);
            fileBtnRow.Children.Add(btnClear);
            stack.Children.Add(fileBtnRow);

            // Tax rate + SKU cost button row
            var configGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var taxStack = new StackPanel();
            taxStack.Children.Add(CreateFieldLabel("Ставка налога, %"));
            string initRate = ActiveCompanyProfile != null
                ? ActiveCompanyProfile.GetEffectiveTaxRate().ToString("0.##", CultureInfo.InvariantCulture)
                : "6";
            anTaxRateBox = CreateStyledInput(initRate, "Ставка налога по текущему профилю (УСН 6% / ИП 20% / ОСН 20%)");
            anTaxRateBox.Margin = new Thickness(0, 3, 0, 0);
            anTaxRateBox.LostFocus += delegate { RecalculateAnalytics(); };
            anTaxRateBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) RecalculateAnalytics();
            };
            taxStack.Children.Add(anTaxRateBox);
            Grid.SetColumn(taxStack, 0);
            configGrid.Children.Add(taxStack);

            var costStack = new StackPanel();
            costStack.Children.Add(CreateFieldLabel("Закупочная себестоимость (для чистой прибыли и расходов ИП)"));
            var costBtnRow = new Grid { Margin = new Thickness(0, 3, 0, 0) };
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

            // Tax Regime Banner (dynamic per active profile)
            anRegimeBanner = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10, 0, 0)
            };
            anRegimeBannerText = new TextBlock
            {
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            anRegimeBanner.Child = anRegimeBannerText;
            stack.Children.Add(anRegimeBanner);
            UpdateRegimeBanner();

            // Background loading indicator
            anLoadingBanner = new Border
            {
                Background = GetBrush("#EFF6FF"),
                BorderBrush = GetBrush("#BFDBFE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var loadStack = new StackPanel();
            anLoadingText = new TextBlock
            {
                Text = "⏳ Разбор отчётов и банковских выписок...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1D4ED8"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var loadProg = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 5,
                Foreground = GetBrush("#0284C7"),
                Background = GetBrush("#DBEAFE"),
                BorderThickness = new Thickness(0)
            };
            loadStack.Children.Add(anLoadingText);
            loadStack.Children.Add(loadProg);
            anLoadingBanner.Child = loadStack;
            stack.Children.Add(anLoadingBanner);

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

            // SKU DataGrid Card (hidden until reports loaded)
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

            var skuHdrGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            skuHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            skuHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            skuHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            var skuTitleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            skuTitleStack.Children.Add(new TextBlock
            {
                Text = "Юнит-экономика по артикулам (сортировка по клику на колонку)",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            anSkuCountText = new TextBlock
            {
                Text = "",
                FontSize = 10.5,
                Foreground = GetBrush("#8E8E93")
            };
            skuTitleStack.Children.Add(anSkuCountText);
            Grid.SetColumn(skuTitleStack, 0);
            skuHdrGrid.Children.Add(skuTitleStack);

            anSkuSearchBox = CreateStyledInput("", "Поиск по артикулу, баркоду, названию или МП (WB / Ozon)");
            anSkuSearchBox.Padding = new Thickness(8, 4, 8, 4);
            anSkuSearchBox.ToolTip = "Фильтр по артикулу, названию, баркоду или площадке (WB / Ozon)";
            anSkuSearchBox.TextChanged += delegate { ApplySkuSearchFilter(); };
            Grid.SetColumn(anSkuSearchBox, 2);
            skuHdrGrid.Children.Add(anSkuSearchBox);

            skuCardStack.Children.Add(skuHdrGrid);

            anSkuGrid = CreateAnalyticsSkuDataGrid();
            skuCardStack.Children.Add(anSkuGrid);

            anSkuCard.Child = skuCardStack;
            stack.Children.Add(anSkuCard);

            // Export Action buttons (3 export options + folder)
            var actionsGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnExportSummary = CreateSolidActionButton("📊  Excel-сводка", "#0284C7", Brushes.White);
            btnExportSummary.ToolTip = "Сводная Excel-книга для руководителя (с учётом режима налогообложения, WB, Ozon и выписки)";
            btnExportSummary.Click += delegate { ExportAnalyticsReport(); };
            Grid.SetColumn(btnExportSummary, 0);
            actionsGrid.Children.Add(btnExportSummary);

            var btnExportKudir = CreateSolidActionButton("📒  КУДиР (Раздел I)", "#0F766E", Brushes.White);
            btnExportKudir.ToolTip = "Книга учёта доходов и расходов (Раздел I — доходы, и лист расходов для ИП)";
            btnExportKudir.Click += delegate { ExportKudirReport(); };
            Grid.SetColumn(btnExportKudir, 2);
            actionsGrid.Children.Add(btnExportKudir);

            var btnExport1C = CreateSolidActionButton("📤  Выгрузка для 1С", "#5856D6", Brushes.White);
            btnExport1C.ToolTip = "Чистый XML или табличный CSV без объединённых ячеек для загрузки в 1С";
            btnExport1C.Click += delegate { Export1CReport(); };
            Grid.SetColumn(btnExport1C, 4);
            actionsGrid.Children.Add(btnExport1C);

            var btnOpenFolder = CreateCompactActionButton("📁 Папка", "#F2F2F7", "#1C1C1E");
            btnOpenFolder.Height = 38;
            btnOpenFolder.Padding = new Thickness(12, 0, 12, 0);
            btnOpenFolder.Click += delegate { OpenAnalyticsFolder(); };
            Grid.SetColumn(btnOpenFolder, 6);
            actionsGrid.Children.Add(btnOpenFolder);

            stack.Children.Add(actionsGrid);
            mainCard.Child = stack;
            container.Children.Add(mainCard);

            // CARD 2: BUILT-IN IMPORT VAT 20% CALCULATOR (EAEU / RF)
            container.Children.Add(BuildImportVatCalculatorCard());
        }

        private Border BuildImportVatCalculatorCard()
        {
            var card = new Border
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
                Text = "🛃  Калькулятор ввозного НДС 20% (закупки в РФ / ЕАЭС)",
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            stack.Children.Add(new TextBlock
            {
                Text = "При импорте товара из РФ / ЕАЭС подача заявления о ввозе и уплата ввозного НДС 20% по курсу НБРБ на дату принятия товара к учёту обязательны до 20-го числа следующего месяца даже для УСН без НДС и ИП.",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 10)
            });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dateCol = new StackPanel();
            dateCol.Children.Add(CreateFieldLabel("Дата прихода товара"));
            anImpDatePicker = new DatePicker
            {
                SelectedDate = DateTime.Today,
                Margin = new Thickness(0, 3, 0, 0),
                FontSize = 12
            };
            anImpDatePicker.SelectedDateChanged += delegate { FetchImportVatRubRate(); };
            dateCol.Children.Add(anImpDatePicker);
            Grid.SetColumn(dateCol, 0);
            grid.Children.Add(dateCol);

            var rubCol = new StackPanel();
            rubCol.Children.Add(CreateFieldLabel("Сумма по накладной / УПД, RUB"));
            anImpRubBox = CreateStyledInput("100000", "Сумма закупки в российских рублях");
            anImpRubBox.Margin = new Thickness(0, 3, 0, 0);
            anImpRubBox.TextChanged += delegate { RecalculateImportVat(); };
            rubCol.Children.Add(anImpRubBox);
            Grid.SetColumn(rubCol, 2);
            grid.Children.Add(rubCol);

            var rateCol = new StackPanel();
            rateCol.Children.Add(CreateFieldLabel("Курс 100 RUB (НБРБ)"));
            anImpRateBox = CreateStyledInput("", "Официальный курс НБРБ за 100 российских рублей");
            anImpRateBox.Margin = new Thickness(0, 3, 0, 0);
            anImpRateBox.TextChanged += delegate { RecalculateImportVat(); };
            rateCol.Children.Add(anImpRateBox);
            Grid.SetColumn(rateCol, 4);
            grid.Children.Add(rateCol);

            var btnFetchRate = CreateCompactActionButton("🔄 Курс НБРБ", "#EBF5FF", "#0284C7");
            btnFetchRate.VerticalAlignment = VerticalAlignment.Bottom;
            btnFetchRate.Height = 31;
            btnFetchRate.Click += delegate { FetchImportVatRubRate(); };
            Grid.SetColumn(btnFetchRate, 6);
            grid.Children.Add(btnFetchRate);

            stack.Children.Add(grid);

            anImpRateStatus = new TextBlock
            {
                Text = "Загрузка актуального курса 100 RUB НБРБ...",
                FontSize = 10.5,
                Foreground = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 5, 0, 8)
            };
            stack.Children.Add(anImpRateStatus);

            var resBox = new Border
            {
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 9)
            };
            var resGrid = new Grid();
            resGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            resGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            resGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftRes = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            anImpBaseText = new TextBlock
            {
                Text = "Налоговая база: — BYN",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E")
            };
            anImpDueText = new TextBlock
            {
                Text = "Срок заявления о ввозе и уплаты: —",
                FontSize = 11,
                Foreground = GetBrush("#636366"),
                Margin = new Thickness(0, 2, 0, 0)
            };
            leftRes.Children.Add(anImpBaseText);
            leftRes.Children.Add(anImpDueText);

            anImpVatText = new TextBlock
            {
                Text = "НДС 20%: — BYN",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#DC2626"),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(leftRes, 0);
            Grid.SetColumn(anImpVatText, 2);
            resGrid.Children.Add(leftRes);
            resGrid.Children.Add(anImpVatText);
            resBox.Child = resGrid;
            stack.Children.Add(resBox);

            card.Child = stack;

            // Load initial rate asynchronously
            FetchImportVatRubRate();

            return card;
        }

        private void FetchImportVatRubRate()
        {
            if (anImpDatePicker == null || anImpRateStatus == null) return;
            DateTime dt = anImpDatePicker.SelectedDate ?? DateTime.Today;
            anImpRateStatus.Text = string.Format("Запрос курса 100 RUB НБРБ на {0:dd.MM.yyyy}...", dt);
            anImpRateStatus.Foreground = GetBrush("#8E8E93");

            NbrbRatesHelper.FetchRateOnDateAsync(456, dt, delegate(decimal? rate, string err)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (rate.HasValue && rate.Value > 0m)
                    {
                        anImpRateBox.Text = rate.Value.ToString("0.0000", CultureInfo.InvariantCulture);
                        anImpRateStatus.Text = string.Format(
                            CultureInfo.InvariantCulture,
                            "✓ Курс НБРБ (ID 456) на {0:dd.MM.yyyy}: 100 RUB = {1:0.0000} BYN",
                            dt,
                            rate.Value);
                        anImpRateStatus.Foreground = GetBrush("#16A34A");
                        RecalculateImportVat();
                    }
                    else
                    {
                        anImpRateStatus.Text = "⚠️ " + (err ?? "Не удалось получить курс НБРБ — можно ввести вручную");
                        anImpRateStatus.Foreground = GetBrush("#D97706");
                        RecalculateImportVat();
                    }
                }));
            });
        }

        private void RecalculateImportVat()
        {
            if (anImpRubBox == null || anImpRateBox == null || anImpBaseText == null) return;
            DateTime dt = (anImpDatePicker != null && anImpDatePicker.SelectedDate.HasValue)
                ? anImpDatePicker.SelectedDate.Value
                : DateTime.Today;
            DateTime nextM = new DateTime(dt.Year, dt.Month, 1).AddMonths(1);
            DateTime due = new DateTime(nextM.Year, nextM.Month, 20);

            decimal rubAmt = 0m, rate100 = 0m;
            bool okRub = TryParseFlexibleDecimal(anImpRubBox.Text, out rubAmt) && rubAmt >= 0m;
            bool okRate = TryParseFlexibleDecimal(anImpRateBox.Text, out rate100) && rate100 > 0m;

            if (okRub && okRate)
            {
                decimal baseByn = Math.Round(rubAmt * rate100 / 100m, 2, MidpointRounding.AwayFromZero);
                decimal vatByn = Math.Round(baseByn * 0.20m, 2, MidpointRounding.AwayFromZero);
                anImpBaseText.Text = "Налоговая база: " + FmtByn(baseByn) + " BYN";
                anImpVatText.Text = "НДС 20%: " + FmtByn(vatByn) + " BYN";
                anImpDueText.Text = string.Format("Срок заявления о ввозе (ЕАЭС) и уплаты: до {0:dd.MM.yyyy}", due);
            }
            else
            {
                anImpBaseText.Text = "Налоговая база: введите сумму в RUB и курс 100 RUB";
                anImpVatText.Text = "НДС 20%: —";
                anImpDueText.Text = string.Format("Срок заявления о ввозе (ЕАЭС) и уплаты: до {0:dd.MM.yyyy}", due);
            }
        }

        private DataGrid CreateAnalyticsSkuDataGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                CanUserSortColumns = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = GetBrush("#E5E5EA"),
                Background = Brushes.White,
                BorderBrush = GetBrush("#D1D1D6"),
                BorderThickness = new Thickness(1),
                RowHeaderWidth = 0,
                MaxHeight = 270,
                FontSize = 11.5,
                SelectionMode = DataGridSelectionMode.Single
            };

            var rightCellStyle = new Style(typeof(TextBlock));
            rightCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            rightCellStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0)));

            var centerCellStyle = new Style(typeof(TextBlock));
            centerCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "МП",
                Binding = new Binding("Marketplace"),
                Width = 44,
                ElementStyle = centerCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Артикул",
                Binding = new Binding("Article"),
                Width = 112
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Название",
                Binding = new Binding("Title"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 115
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Прод.",
                Binding = new Binding("SaleQty"),
                Width = 48,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Возвр.",
                Binding = new Binding("ReturnQty"),
                Width = 50,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Выкуп %",
                Binding = new Binding("BuyoutPercent") { StringFormat = "0.0", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 58,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "От МП",
                Binding = new Binding("NetFromWb") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 70,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Закуп.",
                Binding = new Binding("TotalCost") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 64,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Налог",
                Binding = new Binding("EstimatedTax") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 62,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Прибыль",
                Binding = new Binding("NetProfit") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 74,
                ElementStyle = rightCellStyle
            });

            grid.LoadingRow += delegate(object sender, DataGridRowEventArgs e)
            {
                var sku = e.Row.Item as SkuUnitStats;
                if (sku == null) return;
                if (sku.NetProfit < 0m)
                {
                    e.Row.Background = GetBrush("#FEE2E2");
                    e.Row.Foreground = GetBrush("#991B1B");
                    e.Row.ToolTip = string.Format("Убыточная позиция! ({0}) Прибыль: {1} BYN", sku.Title, FmtByn(sku.NetProfit));
                }
                else if (sku.TotalCost == 0m && sku.NetQty > 0)
                {
                    e.Row.Background = GetBrush("#FFFBEB");
                    e.Row.Foreground = GetBrush("#1C1C1E");
                    e.Row.ToolTip = string.IsNullOrEmpty(sku.Title)
                        ? "Закупочная себестоимость не задана"
                        : (sku.Title + " — закупочная себестоимость не задана");
                }
                else
                {
                    e.Row.Background = Brushes.White;
                    e.Row.Foreground = GetBrush("#1C1C1E");
                    e.Row.ToolTip = string.IsNullOrEmpty(sku.Title) ? null : sku.Title;
                }
            };

            return grid;
        }

        private void UpdateRegimeBanner()
        {
            if (anRegimeBanner == null || anRegimeBannerText == null) return;
            var prof = ActiveCompanyProfile;
            string regime = prof != null ? prof.TaxRegime : CompanyProfilesManager.RegimeUsn6;

            if (regime == CompanyProfilesManager.RegimeIpPn20)
            {
                anRegimeBanner.Background = GetBrush("#FFFBEB");
                anRegimeBanner.BorderBrush = GetBrush("#FDE68A");
                anRegimeBannerText.Foreground = GetBrush("#92400E");
                anRegimeBannerText.Text = "⚠️ УСН для ИП в РБ отменён с 2023 года. Расчёт ведётся по Подоходному налогу (20%, или 30% при доходе > 500 000 BYN) + ФСЗН 35% с автоматическим сравнением «докум. расходы vs стандартный вычет 20%».";
            }
            else if (regime == CompanyProfilesManager.RegimeOsn20)
            {
                anRegimeBanner.Background = GetBrush("#F5F3FF");
                anRegimeBanner.BorderBrush = GetBrush("#DDD6FE");
                anRegimeBannerText.Foreground = GetBrush("#5B21B6");
                anRegimeBannerText.Text = "🏛 Активный режим: ООО • ОСН (ориентир: исходящий НДС 20% + налог на прибыль 20%). Итоговый вычет входящего НДС сверяется по ЭСЧФ.";
            }
            else if (regime == CompanyProfilesManager.RegimeCustom)
            {
                anRegimeBanner.Background = GetBrush("#F0F9FF");
                anRegimeBanner.BorderBrush = GetBrush("#BAE6FD");
                anRegimeBannerText.Foreground = GetBrush("#075985");
                anRegimeBannerText.Text = string.Format("⚙️ Активный режим: {0}. База: выручка реализации (Пр) + облагаемые поступления по р/с.", prof != null ? prof.GetTaxRegimeTitle() : "Особый режим");
            }
            else
            {
                anRegimeBanner.Background = GetBrush("#F0F9FF");
                anRegimeBanner.BorderBrush = GetBrush("#BAE6FD");
                anRegimeBannerText.Foreground = GetBrush("#075985");
                anRegimeBannerText.Text = "📘 Активный режим: ООО • УСН 6% (без НДС). Налоговая база = реализация на маркетплейсах (Пр) + облагаемые прочие поступления на р/с.";
            }
        }

        private void UpdateSkuCostBadge()
        {
            if (anSkuCostsInfo == null) return;
            if (anDemoSkuCostsOverride != null)
            {
                anSkuCostsInfo.Text = string.Format("Демо-справочник ({0} SKU)", anDemoSkuCostsOverride.Count);
                return;
            }
            int n = anSkuCosts != null ? anSkuCosts.Count : 0;
            anSkuCostsInfo.Text = n > 0
                ? string.Format("Задано для {0} артикул.", n)
                : "не задана (прибыль без закупки)";
        }

        private void RefreshAnalyticsTabFromActiveProfile()
        {
            UpdateRegimeBanner();
            if (anTaxRateBox == null || ActiveCompanyProfile == null) return;
            anTaxRateBox.Text = ActiveCompanyProfile.GetEffectiveTaxRate().ToString("0.##", CultureInfo.InvariantCulture);
            if (anLoadedFiles != null && anLoadedFiles.Count > 0)
            {
                RecalculateAnalytics();
            }
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
            if (dropped != null && dropped.Length > 0)
            {
                anDemoSkuCostsOverride = null;
                UpdateSkuCostBadge();
                AddAnalyticsPaths(dropped);
            }
        }

        private void SelectAnalyticsFilesDialog()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите отчёты WB/Ozon и выписку банка",
                Filter = "Отчёты и выписки (*.zip;*.xlsx;*.txt;*.csv)|*.zip;*.xlsx;*.txt;*.csv|ZIP архивы (*.zip)|*.zip|Excel файлы (*.xlsx)|*.xlsx|Выписки 1С / CSV (*.txt;*.csv)|*.txt;*.csv|Все файлы (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true && dlg.FileNames != null && dlg.FileNames.Length > 0)
            {
                anDemoSkuCostsOverride = null;
                UpdateSkuCostBadge();
                AddAnalyticsPaths(dlg.FileNames);
            }
        }

        private void LoadDemoAnalyticsFiles()
        {
            try
            {
                string demoDir = Path.Combine(Path.GetTempPath(), "Buhly_Demo", "Analytics");
                Dictionary<string, decimal> demoCosts;
                var files = AnalyticsEngine.GenerateDemoAnalyticsFiles(demoDir, out demoCosts);
                anLoadedFiles.Clear();
                anLoadedFiles.AddRange(files);
                anDemoSkuCostsOverride = demoCosts;
                UpdateSkuCostBadge();
                RecalculateAnalytics();
                ShowToast("⚡ Загружен демо-пакет: WB + Ozon + выписка банка (1CClientBankExchange)");
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Не удалось создать демо-данные: " + ex.Message);
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
                        candidateFiles.AddRange(Directory.GetFiles(p, "*.txt", SearchOption.AllDirectories));
                        candidateFiles.AddRange(Directory.GetFiles(p, "*.csv", SearchOption.AllDirectories));
                    }
                    catch { }
                }
                else if (File.Exists(p) && IsSupportedAnalyticsFile(p))
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
                ShowToast("⚠️ Нужны файлы .zip, .xlsx, .txt (1С) или .csv");
                return;
            }

            RecalculateAnalytics();
        }

        private static bool IsSupportedAnalyticsFile(string path)
        {
            return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        }

        private void ClearAnalyticsFiles()
        {
            _anAnalyzeSeq++;
            anLoadedFiles.Clear();
            anDemoSkuCostsOverride = null;
            anCurrentSnapshot = null;
            if (anLoadingBanner != null) anLoadingBanner.Visibility = Visibility.Collapsed;
            UpdateSkuCostBadge();
            anDropTitle.Text = "📥  Перетащите отчёты WB/Ozon (.zip/.xlsx), выписку (.xlsx/.txt/.csv) или папку";
            anDropSubtitle.Text = "поддерживаются все банки РБ (Excel и 1CClientBankExchange) • дубликаты отсеются автоматически";
            anKpiCard.Visibility = Visibility.Collapsed;
            anSkuCard.Visibility = Visibility.Collapsed;
            if (anSkuGrid != null) anSkuGrid.ItemsSource = null;
        }

        private static bool TryParseFlexibleDecimal(string raw, out decimal val)
        {
            val = 0m;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Trim().Replace("\u00A0", "").Replace(" ", "").Replace(",", ".");
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
        }

        private decimal ParseTaxRate()
        {
            decimal val;
            if (anTaxRateBox != null && TryParseFlexibleDecimal(anTaxRateBox.Text, out val) && val >= 0m && val <= 50m)
                return val;
            return ActiveCompanyProfile != null ? ActiveCompanyProfile.GetEffectiveTaxRate() : 6.0m;
        }

        private void RecalculateAnalytics()
        {
            if (anLoadedFiles.Count == 0)
            {
                anKpiCard.Visibility = Visibility.Collapsed;
                anSkuCard.Visibility = Visibility.Collapsed;
                if (anLoadingBanner != null) anLoadingBanner.Visibility = Visibility.Collapsed;
                return;
            }

            int seq = ++_anAnalyzeSeq;
            var filesCopy = new List<string>(anLoadedFiles);
            string regime = ActiveCompanyProfile != null ? ActiveCompanyProfile.TaxRegime : CompanyProfilesManager.RegimeUsn6;
            decimal taxRate = ParseTaxRate();
            var costsToUse = anDemoSkuCostsOverride != null
                ? new Dictionary<string, decimal>(anDemoSkuCostsOverride, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, decimal>(anSkuCosts, StringComparer.OrdinalIgnoreCase);

            if (anLoadingBanner != null)
            {
                anLoadingText.Text = string.Format("⏳ Разбор файлов ({0} шт.) в фоновом потоке...", filesCopy.Count);
                anLoadingBanner.Visibility = Visibility.Visible;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                AnalyticsSnapshot resultSnap = null;
                Exception error = null;
                try
                {
                    resultSnap = AnalyticsEngine.AnalyzeFiles(filesCopy, regime, taxRate, costsToUse);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (seq != _anAnalyzeSeq) return;
                    if (anLoadingBanner != null) anLoadingBanner.Visibility = Visibility.Collapsed;
                    if (error != null)
                    {
                        ShowToast("❌ Ошибка разбора отчётов: " + error.Message);
                        return;
                    }
                    anCurrentSnapshot = resultSnap;
                    RenderAnalyticsPreview();
                }));
            });
        }

        private void RenderAnalyticsPreview()
        {
            if (anCurrentSnapshot == null) return;
            var s = anCurrentSnapshot;

            if (s.Reports.Count == 0 && s.BankTransactions.Count == 0)
            {
                anDropTitle.Text = string.Format("⚠️ Загружено файлов: {0}, но формат отчёта / выписки не распознан", anLoadedFiles.Count);
                anDropSubtitle.Text = "проверьте, что это отчёты реализации WB/Ozon или выписка банка РБ (Excel / 1CClientBankExchange)";
                anKpiCard.Visibility = Visibility.Collapsed;
                anSkuCard.Visibility = Visibility.Collapsed;
                return;
            }

            string demoPrefix = anDemoSkuCostsOverride != null ? "[ДЕМО] " : "";
            anDropTitle.Text = string.Format(
                "{0}✅ Отчётов WB: {1}  |  Ozon: {2}  |  Операций банка: {3}",
                demoPrefix, s.WbReportCount, s.OzonReportCount, s.BankTransactions.Count);
            anDropSubtitle.Text = s.DeduplicatedSkipped > 0
                ? string.Format("файлов: {0} (дубликатов пропущено: {1}) — можно докинуть ещё", anLoadedFiles.Count, s.DeduplicatedSkipped)
                : string.Format("файлов загружено: {0} — нажмите, чтобы добавить ещё", anLoadedFiles.Count);

            anKpiContent.Children.Clear();
            anKpiContent.Children.Add(new TextBlock
            {
                Text = "Ключевые показатели периода • " + s.TaxRegimeTitle,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (s.Reports.Count > 0)
            {
                string revLabel = s.TaxRegime == CompanyProfilesManager.RegimeUsn6
                    ? "Реализация МП (база УСН)"
                    : "Выручка реализации на маркетплейсах (Пр)";
                string compNote = s.TotalCompensations != 0m
                    ? string.Format("  |  Компенс./корр.: {0} BYN", FmtByn(s.TotalCompensations))
                    : "";
                AddKpiPairRow(anKpiContent,
                    revLabel, FmtByn(s.TotalRealizedP), "#1C1C1E",
                    string.Format("Продажи / возвраты: {0} / {1} шт{2}", s.TotalSaleQty, s.TotalReturnQty, compNote));

                decimal wbCosts = s.TotalLogistics + s.TotalStorage + s.TotalPenalties + s.TotalHoldings + s.TotalAcceptance + s.TotalLoyalty;
                AddKpiPairRow(anKpiContent,
                    "Логистика, хранение, штрафы МП", FmtByn(wbCosts), "#B45309",
                    string.Format("Логистика: {0}  |  Хран.: {1}  |  Штрафы/удерж.: {2}",
                        FmtByn(s.TotalLogistics), FmtByn(s.TotalStorage), FmtByn(s.TotalPenalties + s.TotalHoldings + s.TotalAcceptance + s.TotalLoyalty)));

                AddKpiPairRow(anKpiContent,
                    "К перечислению на р/с от МП (WB + Ozon)", FmtByn(s.TotalExpectedPayout), "#0284C7",
                    string.Format("WB: {0} BYN ({1} отч.)  |  Ozon: {2} BYN ({3} отч.)",
                        FmtByn(s.WbExpectedPayout), s.WbReportCount, FmtByn(s.OzonExpectedPayout), s.OzonReportCount));
            }

            // Regime-specific Tax KPI rows (no hardcoded "УСН" for IP/OSN!)
            if (s.TaxRegime == CompanyProfilesManager.RegimeIpPn20)
            {
                if (s.IpHighIncomeRateApplied)
                {
                    AddKpiPairRow(anKpiContent,
                        "Повышенная ставка ИП (> 500 000 BYN)", "30%", "#DC2626",
                        "Доход превысил 500 000 BYN — по НК РБ применяется ставка подоходного налога 30%");
                }

                AddKpiPairRow(anKpiContent,
                    string.Format("Подоходный налог ИП ({0:0.##}%)", s.EffectiveTaxRateApplied),
                    FmtByn(s.EstimatedTaxByRealization), "#DC2626",
                    string.Format(
                        "Доходы: {0} | Докум. расходы: {1} (налог {2}) vs Вычет 20%: {3} (налог {4}) → выгоднее {5}",
                        FmtByn(s.IpGrossIncome),
                        FmtByn(s.IpDocumentedExpenses),
                        FmtByn(s.IpTaxDocumented),
                        FmtByn(s.IpStandardDeduction20),
                        FmtByn(s.IpTaxStandard20),
                        s.IpUsedStandardDeduction ? "вычет 20%" : "докум. расходы"));

                AddKpiPairRow(anKpiContent,
                    "Взнос ФСЗН ИП 35% (ориентир от чистого дохода)",
                    FmtByn(s.IpFszn35), "#B45309",
                    "35% от базы чистого дохода ИП (в году — не менее 35% от МЗП за месяцы деятельности)");

                if (s.IpHasPotentialDoubleGoodsCost)
                {
                    anKpiContent.Children.Add(new TextBlock
                    {
                        Text = string.Format(
                            "⚠️ Заполнена себестоимость SKU ({0} BYN) и есть оплаты за товар с р/с ({1} BYN). Во избежание двойного списания в расходах ИП учтена себестоимость проданных SKU + прочие расходы с р/с ({2} BYN).",
                            FmtByn(s.TotalCostOfGoods), FmtByn(s.IpBankGoodsPayments), FmtByn(s.IpBankDeductibleExpenses)),
                        FontSize = 10.5,
                        Foreground = GetBrush("#B45309"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 5)
                    });
                }
            }
            else if (s.TaxRegime == CompanyProfilesManager.RegimeOsn20)
            {
                AddKpiPairRow(anKpiContent,
                    "ОСН: Исходящий НДС 20% (ориентир 20/120)",
                    FmtByn(s.OsnVat20), "#B45309",
                    "Без учёта входящего вычета НДС — точный расчёт ведётся по ЭСЧФ");
                AddKpiPairRow(anKpiContent,
                    string.Format("ОСН: Налог на прибыль ({0:0.##}%), ориентир", s.EffectiveTaxRateApplied),
                    FmtByn(s.OsnProfitTax20), "#DC2626",
                    string.Format("База прибыли: {0} BYN (выручка без НДС минус комиссии МП, закупка и р/с)", FmtByn(s.OsnProfitTaxBase)));
            }
            else
            {
                string taxLabel = s.TaxRegime == CompanyProfilesManager.RegimeCustom
                    ? string.Format("Ориентир налога ({0:0.##}%)", s.EffectiveTaxRateApplied)
                    : string.Format("Ориентир налога УСН ({0:0.##}%)", s.EffectiveTaxRateApplied);
                AddKpiPairRow(anKpiContent,
                    taxLabel,
                    FmtByn(s.EstimatedTaxByRealization), "#DC2626",
                    string.Format("База: {0} BYN (реализация МП {1} + комп. {2} + облаг. р/с {3})",
                        FmtByn(s.TaxBaseAmount), FmtByn(s.TotalRealizedP), FmtByn(s.TotalCompensations), FmtByn(s.BankOtherCredits)));
            }

            if (s.Reports.Count > 0)
            {
                string profitHex = s.EstimatedNetProfitBeforeFixed >= 0m ? "#16A34A" : "#DC2626";
                AddKpiPairRow(anKpiContent,
                    s.TotalCostOfGoods > 0m ? "Чистая прибыль до ФОТ/аренды (с закупкой)" : "Прибыль до закупки, ФОТ и аренды",
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
                    "Поступило на р/с от маркетплейсов (WB + Ozon)", FmtByn(s.BankMarketplaceCredits), "#0F766E",
                    string.Format("WB: {0}  |  Ozon: {1}  |  Всего приход: {2}  |  Списано: {3}",
                        FmtByn(s.BankWbCredits), FmtByn(s.BankOzonCredits), FmtByn(s.BankTotalCredit), FmtByn(s.BankTotalDebet)));

                if (s.Reports.Count > 0)
                {
                    decimal diff = s.BankMarketplaceCredits - s.TotalExpectedPayout;
                    string diffPrefix = diff > 0m ? "+" : "";
                    string diffHex = Math.Abs(diff) < 1.0m ? "#16A34A" : "#B45309";
                    AddKpiPairRow(anKpiContent,
                        "Сверка р/с vs отчёты МП (разница)", diffPrefix + FmtByn(diff), diffHex,
                        Math.Abs(diff) < 1.0m ? "выплаты сошлись копейка в копейку" : "разница обычно из-за переходящих выплат на границе периода");
                }

                AddKpiPairRow(anKpiContent,
                    "Налоги, ФСЗН и Белгосстрах с р/с", FmtByn(s.BankTaxesDebet), "#636366",
                    string.Format("Поставщики: {0}  |  Зарплата: {1}  |  Комиссия банка: {2}",
                        FmtByn(s.BankVendorsDebet), FmtByn(s.BankPayrollDebet), FmtByn(s.BankFeesDebet)));
            }

            anKpiCard.Visibility = Visibility.Visible;

            // SKU Unit Economics DataGrid
            if (s.Skus.Count > 0)
            {
                anSkuGrid.ItemsSource = s.Skus;
                anSkuView = CollectionViewSource.GetDefaultView(anSkuGrid.ItemsSource);
                ApplySkuSearchFilter();
                anSkuCard.Visibility = Visibility.Visible;
            }
            else
            {
                anSkuGrid.ItemsSource = null;
                anSkuView = null;
                anSkuCard.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplySkuSearchFilter()
        {
            if (anSkuView == null || anCurrentSnapshot == null) return;
            string q = anSkuSearchBox != null ? (anSkuSearchBox.Text ?? "").Trim().ToLowerInvariant() : "";
            if (string.IsNullOrEmpty(q))
            {
                anSkuView.Filter = null;
            }
            else
            {
                anSkuView.Filter = delegate(object obj)
                {
                    var sku = obj as SkuUnitStats;
                    if (sku == null) return false;
                    return (sku.Article ?? "").ToLowerInvariant().Contains(q) ||
                           (sku.Title ?? "").ToLowerInvariant().Contains(q) ||
                           (sku.Barcode ?? "").ToLowerInvariant().Contains(q) ||
                           (sku.Marketplace ?? "").ToLowerInvariant().Contains(q);
                };
            }

            int total = anCurrentSnapshot.Skus.Count;
            int shown = 0;
            foreach (var _ in anSkuView) shown++;
            if (anSkuCountText != null)
            {
                int lossCount = 0;
                foreach (var s in anCurrentSnapshot.Skus) if (s.NetProfit < 0m) lossCount++;
                anSkuCountText.Text = string.Format("Показано: {0} из {1} SKU  •  Убыточных (красным): {2}", shown, total, lossCount);
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
                Foreground = GetBrush("#3A3A3C"),
                TextWrapping = TextWrapping.Wrap
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
                Text = value.EndsWith("%") ? value : (value + " BYN"),
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

        private static string FmtByn(decimal val)
        {
            return val.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private void ExportAnalyticsReport()
        {
            if (anCurrentSnapshot == null ||
                (anCurrentSnapshot.Reports.Count == 0 && anCurrentSnapshot.BankTransactions.Count == 0))
            {
                ShowToast("⚠️ Сначала перетащите отчёты WB/Ozon или выписку банка (либо нажмите «Демо-данные»)");
                return;
            }

            try
            {
                var outDir = GetAnalyticsOutputDirectory();
                Directory.CreateDirectory(outDir);
                string fileName = "Buhly_Сводка_" + DateTime.Now.ToString("dd.MM.yyyy_HH-mm") + ".xlsx";
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

        private void ExportKudirReport()
        {
            if (anCurrentSnapshot == null ||
                (anCurrentSnapshot.Reports.Count == 0 && anCurrentSnapshot.BankTransactions.Count == 0))
            {
                ShowToast("⚠️ Сначала загрузите отчёты WB/Ozon или выписку банка");
                return;
            }

            try
            {
                var outDir = GetAnalyticsOutputDirectory();
                Directory.CreateDirectory(outDir);
                string fileName = "Buhly_КУДиР_Раздел1_" + DateTime.Now.ToString("dd.MM.yyyy_HH-mm") + ".xlsx";
                string outPath = Path.Combine(outDir, fileName);

                AnalyticsEngine.ExportKudirXlsx(anCurrentSnapshot, ActiveCompanyProfile, outPath);
                WaybillEngine.OpenFileWithShell(outPath);
                ShowToast("📒 КУДиР (Раздел I) сформирована и открыта: " + fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сформировать КУДиР:\n" + ex.Message,
                    "Ошибка КУДиР", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export1CReport()
        {
            if (anCurrentSnapshot == null ||
                (anCurrentSnapshot.Reports.Count == 0 && anCurrentSnapshot.BankTransactions.Count == 0))
            {
                ShowToast("⚠️ Сначала загрузите отчёты WB/Ozon или выписку банка");
                return;
            }

            try
            {
                var outDir = GetAnalyticsOutputDirectory();
                Directory.CreateDirectory(outDir);
                var dlg = new SaveFileDialog
                {
                    Title = "Выгрузка данных для 1С (чистый XML или табличный CSV)",
                    InitialDirectory = outDir,
                    FileName = "Buhly_1C_" + DateTime.Now.ToString("yyyy-MM-dd"),
                    Filter = "XML для 1С (*.xml)|*.xml|Таблица CSV для 1С / Excel (; UTF-8) (*.csv)|*.csv"
                };
                if (dlg.ShowDialog() != true) return;

                if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    AnalyticsEngine.Export1CCsv(anCurrentSnapshot, ActiveCompanyProfile, dlg.FileName);
                }
                else
                {
                    AnalyticsEngine.Export1CXml(anCurrentSnapshot, ActiveCompanyProfile, dlg.FileName);
                }
                WaybillEngine.OpenFileWithShell(dlg.FileName);
                ShowToast("📤 Выгрузка для 1С сохранена: " + Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить выгрузку для 1С:\n" + ex.Message,
                    "Ошибка выгрузки 1С", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var activeCosts = anDemoSkuCostsOverride != null ? anDemoSkuCostsOverride : anSkuCosts;
            var inputs = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
            Action populateList = delegate
            {
                listPanel.Children.Clear();
                inputs.Clear();

                var keys = new HashSet<string>(activeCosts.Keys, StringComparer.OrdinalIgnoreCase);
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
                        Text = "Сначала перетащите отчёты WB/Ozon во вкладку — список артикулов появится автоматически, либо нажмите «Импорт».",
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
                    string textVal = activeCosts.TryGetValue(art, out existing) && existing > 0m
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
                    int imported = AnalyticsEngine.ImportSkuCostsFromTextOrCsv(dlg.FileName, activeCosts);
                    if (anDemoSkuCostsOverride == null)
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
                        activeCosts.Remove(kv.Key);
                    }
                    else if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out val) && val >= 0m)
                    {
                        activeCosts[kv.Key] = val;
                    }
                }
                if (anDemoSkuCostsOverride == null)
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
