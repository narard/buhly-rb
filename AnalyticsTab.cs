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
        private int _importVatSeq = 0;
        private int _anBgTaskCount = 0;
        private bool _anExportBusy = false;

        // UI controls for Analytics tab
        private Border anDropZone;
        private TextBlock anDropTitle;
        private TextBlock anDropSubtitle;
        private TextBox anTaxRateBox;
        private TextBlock anSkuCostsInfo;
        private Border anRegimeBanner;
        private Border anRegimeExpBadge;
        private TextBlock anRegimeBannerText;
        private TextBlock anRegimeDisclaimerText;
        private Border anLoadingBanner;
        private TextBlock anLoadingText;
        private Border anWarningsBanner;
        private TextBlock anWarningsTitle;
        private StackPanel anWarningsList;
        private TextBlock anWarningsHintText;
        private StackPanel anWarningsActionsRow;
        private Border anKpiCard;
        private StackPanel anKpiContent;
        private ColumnDefinition anCol1CGap;
        private ColumnDefinition anCol1C;
        private Button anBtnExportSummary;
        private Button anBtnExportKudir;
        private Button anBtnExport1C;
        private Button anBtnDiag;
        private TextBlock anPro1CHint;
        private Border anSkuCard;
        private TextBox anSkuSearchBox;
        private TextBlock anSkuCountText;
        private DataGrid anSkuGrid;
        private ICollectionView anSkuView;

        // Import VAT 20%/10% multi-currency calculator controls
        private bool _anImpExpanded = false;
        private Button anImpAccordionBtn;
        private StackPanel anImpCalcBody;
        private ComboBox anImpCurrencyCombo;
        private ComboBox anImpVatRateCombo;
        private DatePicker anImpDatePicker;
        private TextBlock anImpAmountLabel;
        private TextBox anImpRubBox;
        private TextBlock anImpRateLabel;
        private TextBox anImpRateBox;
        private TextBlock anImpRateStatus;
        private TextBlock anImpBaseText;
        private TextBlock anImpVatText;
        private TextBlock anImpDueText;
        private TextBlock anImpAccountingHint;

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

            // File action row: Choose / Demo / Diagnostics / Clear
            var fileBtnRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnSelect = CreateCompactActionButton("📂  Выбрать файлы (.zip/.xlsx/.txt/.csv)", "#EBF5FF", "#0284C7");
            btnSelect.Click += delegate { SelectAnalyticsFilesDialog(); };
            Grid.SetColumn(btnSelect, 0);
            fileBtnRow.Children.Add(btnSelect);

            var btnDemo = CreateCompactActionButton("⚡ Демо", "#FFFBEB", "#B45309");
            btnDemo.Padding = new Thickness(10, 0, 10, 0);
            btnDemo.ToolTip = "Загрузить синтетический пример WB + Ozon + выписка 1С (без личных данных)";
            btnDemo.Click += delegate { LoadDemoAnalyticsFiles(); };
            Grid.SetColumn(btnDemo, 2);
            fileBtnRow.Children.Add(btnDemo);

            anBtnDiag = CreateCompactActionButton("🛟 Диагностика файла", "#F8FAFC", "#475569");
            anBtnDiag.Padding = new Thickness(10, 0, 10, 0);
            anBtnDiag.ToolTip = "Создаёт обезличенный отчёт о структуре файла (без сумм, УНП, IBAN и названий)";
            anBtnDiag.Click += delegate { RunAnonymizedFileDiagnostics(); };
            Grid.SetColumn(anBtnDiag, 4);
            fileBtnRow.Children.Add(anBtnDiag);

            var btnClear = CreateCompactActionButton("✕ Очистить", "#F2F2F7", "#636366");
            btnClear.Padding = new Thickness(10, 0, 10, 0);
            btnClear.Click += delegate { ClearAnalyticsFiles(); };
            Grid.SetColumn(btnClear, 6);
            fileBtnRow.Children.Add(btnClear);
            stack.Children.Add(fileBtnRow);

            // Tax rate + SKU cost button row
            var configGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
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

            // Tax Regime Banner (unified status + experimental OSN badge + short disclaimer)
            anRegimeBanner = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10, 0, 0)
            };
            var regStack = new StackPanel();
            var regTopRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            anRegimeExpBadge = new Border
            {
                Background = GetBrush("#7C3AED"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            anRegimeExpBadge.Child = new TextBlock
            {
                Text = "ЭКСПЕРИМЕНТАЛЬНЫЙ РЕЖИМ",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            regTopRow.Children.Add(anRegimeExpBadge);
            regStack.Children.Add(regTopRow);

            anRegimeBannerText = new TextBlock
            {
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            regStack.Children.Add(anRegimeBannerText);

            anRegimeDisclaimerText = new TextBlock
            {
                Text = "ℹ️ Расчёт исходит из стандартных договоров с резидентами РБ (ООО «ИМВБРБ» / ООО «ОЗОН Маркет Бел»).",
                FontSize = 10,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            };
            regStack.Children.Add(anRegimeDisclaimerText);

            anRegimeBanner.Child = regStack;
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

            // Warnings / Error Banner (visible when Snapshot.Warnings has entries or files could not be parsed)
            anWarningsBanner = new Border
            {
                Background = GetBrush("#FFFBEB"),
                BorderBrush = GetBrush("#FDE68A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var warnStack = new StackPanel();
            anWarningsTitle = new TextBlock
            {
                Text = "⚠️ Предупреждения при разборе и расчёте",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#B45309"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            warnStack.Children.Add(anWarningsTitle);

            anWarningsList = new StackPanel();
            warnStack.Children.Add(anWarningsList);

            anWarningsHintText = new TextBlock
            {
                FontSize = 11,
                Foreground = GetBrush("#92400E"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0),
                Visibility = Visibility.Collapsed
            };
            warnStack.Children.Add(anWarningsHintText);

            anWarningsActionsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 7, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var btnWarnClear = CreateSmallPillButton("✕ Очистить список", "#FEE2E2", "#B91C1C");
            btnWarnClear.Margin = new Thickness(0, 0, 8, 0);
            btnWarnClear.Click += delegate { ClearAnalyticsFiles(); };
            var btnWarnDiag = CreateSmallPillButton("🛟 Диагностика файла", "#F1F5F9", "#334155");
            btnWarnDiag.Click += delegate { RunAnonymizedFileDiagnostics(); };
            anWarningsActionsRow.Children.Add(btnWarnClear);
            anWarningsActionsRow.Children.Add(btnWarnDiag);
            warnStack.Children.Add(anWarningsActionsRow);

            anWarningsBanner.Child = warnStack;
            stack.Children.Add(anWarningsBanner);

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
            skuHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });

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
            anCol1CGap = new ColumnDefinition { Width = new GridLength(8) };
            anCol1C = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
            actionsGrid.ColumnDefinitions.Add(anCol1CGap);
            actionsGrid.ColumnDefinitions.Add(anCol1C);
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            anBtnExportSummary = CreateSolidActionButton("📊  Excel-сводка", "#0284C7", Brushes.White);
            anBtnExportSummary.ToolTip = "Сводная Excel-книга для руководителя (с учётом режима налогообложения, WB, Ozon и выписки)";
            anBtnExportSummary.Click += delegate { ExportAnalyticsReport(); };
            Grid.SetColumn(anBtnExportSummary, 0);
            actionsGrid.Children.Add(anBtnExportSummary);

            anBtnExportKudir = CreateSolidActionButton("📒  КУДиР (Раздел I)", "#0F766E", Brushes.White);
            anBtnExportKudir.ToolTip = "Книга учёта доходов и расходов (Раздел I — доходы, и лист расходов для ИП)";
            anBtnExportKudir.Click += delegate { ExportKudirReport(); };
            Grid.SetColumn(anBtnExportKudir, 2);
            actionsGrid.Children.Add(anBtnExportKudir);

            anBtnExport1C = CreateSolidActionButton("📤  Выгрузка для 1С", "#5856D6", Brushes.White);
            anBtnExport1C.ToolTip = "Чистый XML или табличный CSV без объединённых ячеек для загрузки в 1С";
            anBtnExport1C.Click += delegate { Export1CReport(); };
            Grid.SetColumn(anBtnExport1C, 4);
            actionsGrid.Children.Add(anBtnExport1C);

            var btnOpenFolder = CreateCompactActionButton("📁 Папка", "#F2F2F7", "#1C1C1E");
            btnOpenFolder.Height = 38;
            btnOpenFolder.Padding = new Thickness(12, 0, 12, 0);
            btnOpenFolder.Click += delegate { OpenAnalyticsFolder(); };
            Grid.SetColumn(btnOpenFolder, 6);
            actionsGrid.Children.Add(btnOpenFolder);

            stack.Children.Add(actionsGrid);

            anPro1CHint = new TextBlock
            {
                Text = "ℹ️ Выгрузка для 1С (XML / CSV) доступна в режиме «Бухгалтер PRO» (переключатель в меню слева).",
                FontSize = 10.5,
                Foreground = GetBrush("#8E8E93"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 6, 2, 0)
            };
            stack.Children.Add(anPro1CHint);

            mainCard.Child = stack;
            container.Children.Add(mainCard);

            // CARD 2: BUILT-IN IMPORT VAT 20% CALCULATOR (EAEU / RF)
            container.Children.Add(BuildImportVatCalculatorCard());

            RefreshAnalyticsUiMode(IsProUiMode());
        }

        private void RefreshAnalyticsUiMode(bool isPro)
        {
            if (anCol1CGap != null) anCol1CGap.Width = new GridLength(isPro ? 8 : 0);
            if (anCol1C != null) anCol1C.Width = isPro ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            if (anBtnExport1C != null) anBtnExport1C.Visibility = isPro ? Visibility.Visible : Visibility.Collapsed;
            if (anPro1CHint != null) anPro1CHint.Visibility = isPro ? Visibility.Collapsed : Visibility.Visible;
            UpdateRegimeBanner();
            if (anCurrentSnapshot != null) RenderAnalyticsPreview();
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
            _anImpExpanded = false;

            anImpAccordionBtn = new Button
            {
                Content = "🛃 Калькулятор ввозного НДС 20% / 10% (импорт РФ / ЕАЭС: RUB / USD / EUR) ▼ Открыть",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            var accTpl = new ControlTemplate(typeof(Button));
            var accBorder = new FrameworkElementFactory(typeof(Border));
            accBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var accCp = new FrameworkElementFactory(typeof(ContentPresenter));
            accCp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            accCp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            accBorder.AppendChild(accCp);
            accTpl.VisualTree = accBorder;
            anImpAccordionBtn.Template = accTpl;
            anImpAccordionBtn.Click += delegate
            {
                _anImpExpanded = !_anImpExpanded;
                if (anImpCalcBody != null)
                    anImpCalcBody.Visibility = _anImpExpanded ? Visibility.Visible : Visibility.Collapsed;
                anImpAccordionBtn.Content = _anImpExpanded
                    ? "🛃 Калькулятор ввозного НДС 20% / 10% (импорт РФ / ЕАЭС: RUB / USD / EUR) ▲ Свернуть"
                    : "🛃 Калькулятор ввозного НДС 20% / 10% (импорт РФ / ЕАЭС: RUB / USD / EUR) ▼ Открыть";
            };
            stack.Children.Add(anImpAccordionBtn);

            anImpCalcBody = new StackPanel
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 8, 0, 0)
            };
            anImpCalcBody.Children.Add(new TextBlock
            {
                Text = "При импорте товара из РФ / ЕАЭС подача заявления о ввозе и уплата ввозного НДС (20% или 10%) по курсу НБРБ на дату принятия товара к учёту обязательны до 20-го числа следующего месяца даже для УСН без НДС и ИП.",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Currency & VAT rate selectors row
            var selGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            selGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            selGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var curCol = new StackPanel();
            curCol.Children.Add(CreateFieldLabel("Валюта накладной / контракта (НБРБ)"));
            anImpCurrencyCombo = new ComboBox { FontSize = 12, Margin = new Thickness(0, 3, 0, 0) };
            anImpCurrencyCombo.Items.Add("RUB (100 RUB • 456)");
            anImpCurrencyCombo.Items.Add("USD (1 USD • 431)");
            anImpCurrencyCombo.Items.Add("EUR (1 EUR • 451)");
            anImpCurrencyCombo.SelectedIndex = 0;
            anImpCurrencyCombo.SelectionChanged += delegate
            {
                UpdateImportVatCurrencyLabels();
                FetchImportVatRubRate();
            };
            curCol.Children.Add(anImpCurrencyCombo);
            Grid.SetColumn(curCol, 0);
            selGrid.Children.Add(curCol);

            var vatCol = new StackPanel();
            vatCol.Children.Add(CreateFieldLabel("Ставка ввозного НДС"));
            anImpVatRateCombo = new ComboBox { FontSize = 12, Margin = new Thickness(0, 3, 0, 0) };
            anImpVatRateCombo.Items.Add("20% (стандарт)");
            anImpVatRateCombo.Items.Add("10% (детские / продтовары)");
            anImpVatRateCombo.SelectedIndex = 0;
            anImpVatRateCombo.SelectionChanged += delegate { RecalculateImportVat(); };
            vatCol.Children.Add(anImpVatRateCombo);
            Grid.SetColumn(vatCol, 2);
            selGrid.Children.Add(vatCol);

            anImpCalcBody.Children.Add(selGrid);

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
            anImpAmountLabel = CreateFieldLabel("Сумма по накладной / УПД, RUB");
            rubCol.Children.Add(anImpAmountLabel);
            anImpRubBox = CreateStyledInput("100000", "Сумма закупки в валюте контракта / УПД");
            anImpRubBox.Margin = new Thickness(0, 3, 0, 0);
            anImpRubBox.TextChanged += delegate { RecalculateImportVat(); };
            rubCol.Children.Add(anImpRubBox);
            Grid.SetColumn(rubCol, 2);
            grid.Children.Add(rubCol);

            var rateCol = new StackPanel();
            anImpRateLabel = CreateFieldLabel("Курс 100 RUB (НБРБ)");
            rateCol.Children.Add(anImpRateLabel);
            anImpRateBox = CreateStyledInput("", "Официальный курс НБРБ на дату прихода товара");
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

            anImpCalcBody.Children.Add(grid);

            anImpRateStatus = new TextBlock
            {
                Text = "Загрузка актуального курса 100 RUB НБРБ...",
                FontSize = 10.5,
                Foreground = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 5, 0, 8)
            };
            anImpCalcBody.Children.Add(anImpRateStatus);

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
            anImpCalcBody.Children.Add(resBox);

            anImpAccountingHint = new TextBlock
            {
                Text = "💡 Учёт ввозного НДС: при УСН / ИП включается в стоимость товара; при ОСН — принимается к вычету после уплаты в бюджет и выставления ЭСЧФ.",
                FontSize = 11,
                Foreground = GetBrush("#075985"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
            anImpCalcBody.Children.Add(anImpAccountingHint);

            anImpCalcBody.Children.Add(new TextBlock
            {
                Text = "⚠️ При работе по договору с РФ-площадкой (напр. ООО «Интернет Решения» / Ozon РФ) дополнительно возникает НДС 20% налогового агента с услуг нерезидента (даже для УСН без НДС и ИП).",
                FontSize = 11,
                Foreground = GetBrush("#7C2D12"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            });

            anImpCalcBody.Children.Add(new TextBlock
            {
                Text = "📌 При импорте из РФ / ЕАЭС от 3 000 EUR по контракту/месяцу дополнительно подаётся статистическая декларация (статдекларация) — в течение 7 рабочих дней с даты поступления товара.",
                FontSize = 11,
                Foreground = GetBrush("#B45309"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            });

            stack.Children.Add(anImpCalcBody);
            card.Child = stack;

            // Load initial rate asynchronously
            FetchImportVatRubRate();

            return card;
        }

        private void GetSelectedImportVatCurrency(out int curId, out int curScale, out string curCode)
        {
            int idx = anImpCurrencyCombo != null ? anImpCurrencyCombo.SelectedIndex : 0;
            if (idx == 1)
            {
                curId = NbrbRatesHelper.CurIdUsd;
                curScale = 1;
                curCode = "USD";
            }
            else if (idx == 2)
            {
                curId = NbrbRatesHelper.CurIdEur;
                curScale = 1;
                curCode = "EUR";
            }
            else
            {
                curId = NbrbRatesHelper.CurIdRub100;
                curScale = 100;
                curCode = "RUB";
            }
        }

        private decimal GetSelectedImportVatRatePercent()
        {
            return (anImpVatRateCombo != null && anImpVatRateCombo.SelectedIndex == 1) ? 10m : 20m;
        }

        private void UpdateImportVatCurrencyLabels()
        {
            int curId, curScale;
            string curCode;
            GetSelectedImportVatCurrency(out curId, out curScale, out curCode);
            if (anImpAmountLabel != null)
                anImpAmountLabel.Text = "Сумма по накладной / УПД, " + curCode;
            if (anImpRateLabel != null)
                anImpRateLabel.Text = string.Format(CultureInfo.InvariantCulture, "Курс {0} {1} (НБРБ)", curScale, curCode);
        }

        private void FetchImportVatRubRate()
        {
            if (anImpDatePicker == null || anImpRateStatus == null) return;
            DateTime dt = anImpDatePicker.SelectedDate ?? DateTime.Today;
            int curId, curScale;
            string curCode;
            GetSelectedImportVatCurrency(out curId, out curScale, out curCode);

            int seq = ++_importVatSeq;
            anImpRateStatus.Text = string.Format(CultureInfo.InvariantCulture, "Запрос курса {0} {1} НБРБ на {2:dd.MM.yyyy}...", curScale, curCode, dt);
            anImpRateStatus.Foreground = GetBrush("#8E8E93");

            NbrbRatesHelper.FetchRateOnDateAsync(curId, dt, delegate(NbrbRateOnDateResult res)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (seq != _importVatSeq) return;
                    if (res != null && res.Success && res.Rate > 0m)
                    {
                        anImpRateBox.Text = res.Rate.ToString("0.0000", CultureInfo.InvariantCulture);
                        anImpRateStatus.Text = string.Format(
                            CultureInfo.InvariantCulture,
                            "✓ Курс НБРБ (ID {0}) на {1:dd.MM.yyyy}: {2} {3} = {4:0.0000} BYN",
                            res.CurId,
                            dt,
                            res.CurScale,
                            string.IsNullOrEmpty(res.CurAbbreviation) ? curCode : res.CurAbbreviation,
                            res.Rate);
                        anImpRateStatus.Foreground = GetBrush("#16A34A");
                        RecalculateImportVat();
                    }
                    else
                    {
                        string errMsg = (res != null && !string.IsNullOrEmpty(res.ErrorMessage))
                            ? res.ErrorMessage
                            : "Не удалось получить курс НБРБ — можно ввести вручную";
                        anImpRateStatus.Text = "⚠️ " + errMsg;
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

            int curId, curScale;
            string curCode;
            GetSelectedImportVatCurrency(out curId, out curScale, out curCode);
            decimal vatRate = GetSelectedImportVatRatePercent();

            decimal foreignAmt = 0m, rateVal = 0m;
            bool okAmt = TryParseFlexibleDecimal(anImpRubBox.Text, out foreignAmt) && foreignAmt >= 0m;
            bool okRate = TryParseFlexibleDecimal(anImpRateBox.Text, out rateVal) && rateVal > 0m;

            if (okAmt && okRate)
            {
                var calc = new NbrbRateOnDateResult
                {
                    Success = true,
                    CurId = curId,
                    CurAbbreviation = curCode,
                    CurScale = curScale,
                    Date = dt,
                    Rate = rateVal
                };
                decimal baseByn = calc.ConvertToByn(foreignAmt);
                decimal vatByn = calc.CalculateImportVat(foreignAmt, vatRate);
                anImpBaseText.Text = string.Format("Налоговая база ({0} {1}): {2} BYN", FmtByn(foreignAmt), curCode, FmtByn(baseByn));
                anImpVatText.Text = string.Format(CultureInfo.InvariantCulture, "НДС {0:0}%: {1} BYN", vatRate, FmtByn(vatByn));
                anImpDueText.Text = string.Format("Срок заявления о ввозе (ЕАЭС) и уплаты: до {0:dd.MM.yyyy}", due);
            }
            else
            {
                anImpBaseText.Text = string.Format(CultureInfo.InvariantCulture, "Налоговая база: введите сумму в {0} и курс {1} {0}", curCode, curScale);
                anImpVatText.Text = string.Format(CultureInfo.InvariantCulture, "НДС {0:0}%: —", vatRate);
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
                FontSize = 11,
                SelectionMode = DataGridSelectionMode.Single
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Auto);

            var rightCellStyle = new Style(typeof(TextBlock));
            rightCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            rightCellStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(2, 0, 2, 0)));

            var centerCellStyle = new Style(typeof(TextBlock));
            centerCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "МП",
                Binding = new Binding("Marketplace"),
                Width = 36,
                MinWidth = 34,
                ElementStyle = centerCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Артикул",
                Binding = new Binding("Article"),
                Width = new DataGridLength(0.95, DataGridLengthUnitType.Star),
                MinWidth = 64
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Название",
                Binding = new Binding("Title"),
                Width = new DataGridLength(1.45, DataGridLengthUnitType.Star),
                MinWidth = 76
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Прод.",
                Binding = new Binding("SaleQty"),
                Width = 40,
                MinWidth = 36,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Возвр.",
                Binding = new Binding("ReturnQty"),
                Width = 44,
                MinWidth = 40,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Выкуп %",
                Binding = new Binding("BuyoutPercent") { StringFormat = "0.0", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 50,
                MinWidth = 46,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "От МП",
                Binding = new Binding("NetFromWb") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = new DataGridLength(0.65, DataGridLengthUnitType.Star),
                MinWidth = 50,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Закуп.",
                Binding = new Binding("TotalCost") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = new DataGridLength(0.55, DataGridLengthUnitType.Star),
                MinWidth = 46,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Налог",
                Binding = new Binding("EstimatedTax") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = new DataGridLength(0.55, DataGridLengthUnitType.Star),
                MinWidth = 46,
                ElementStyle = rightCellStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Прибыль",
                Binding = new Binding("NetProfit") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = new DataGridLength(0.7, DataGridLengthUnitType.Star),
                MinWidth = 54,
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
            bool isPro = IsProUiMode();

            if (anRegimeExpBadge != null)
                anRegimeExpBadge.Visibility = regime == CompanyProfilesManager.RegimeOsn20 ? Visibility.Visible : Visibility.Collapsed;

            if (regime == CompanyProfilesManager.RegimeIpPn20)
            {
                decimal ipNorm = (prof != null && (prof.IpExpenseNormPercent == 20m || prof.IpExpenseNormPercent == 30m))
                    ? prof.IpExpenseNormPercent
                    : 30m;
                string normKind = ipNorm == 30m ? "30% розница" : "20% общий норматив";
                anRegimeBanner.Background = GetBrush("#FFFBEB");
                anRegimeBanner.BorderBrush = GetBrush("#FDE68A");
                anRegimeBannerText.Foreground = GetBrush("#92400E");
                anRegimeBannerText.Text = string.Format(
                    "⚠️ УСН для ИП отменён с 2023 г. Подоходный 20% (при доходе > 500 000 BYN — 30% и переход в юрлицо по Закону № 365-З) + ФСЗН 35% (для 2026 г.: база 12 МЗП = 10 296 BYN, макс. взнос 35% = 3 603.60 BYN). Авто-сравнение «докум. расходы vs норматив {0}».",
                    normKind);
            }
            else if (regime == CompanyProfilesManager.RegimeOsn20)
            {
                anRegimeBanner.Background = GetBrush("#F5F3FF");
                anRegimeBanner.BorderBrush = GetBrush("#DDD6FE");
                anRegimeBannerText.Foreground = GetBrush("#5B21B6");
                string simpleNote = !isPro
                    ? " В Базовом режиме ОСН показывает только ориентир — лучше проверять итоговый НДС, ЭСЧФ и налог на прибыль с бухгалтером."
                    : "";
                anRegimeBannerText.Text = "🏛 [ЭКСПЕРИМЕНТАЛЬНЫЙ РЕЖИМ] ООО • ОСН (ориентир: исходящий НДС 20% + налог на прибыль 20%). Итоговый вычет входящего НДС сверяется по ЭСЧФ." + simpleNote;
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
                bool isRealMethod = prof != null && string.Equals(prof.UsnMethod, CompanyProfilesManager.UsnMethodRealization, StringComparison.OrdinalIgnoreCase);
                anRegimeBanner.Background = GetBrush("#F0F9FF");
                anRegimeBanner.BorderBrush = GetBrush("#BAE6FD");
                anRegimeBannerText.Foreground = GetBrush("#075985");
                anRegimeBannerText.Text = isRealMethod
                    ? "📘 Активный режим: ООО • УСН 6% (без НДС) • По реализации отчётов МП (переключить на кассовый метод по ст. 328 НК РБ можно в «Профилях компаний»)."
                    : "📘 Активный режим: ООО • УСН 6% (без НДС) • Кассовый метод (ст. 328 НК РБ: зачисления на р/с + взаимозачёт удержаний МП; без выписки — авторасчёт по реализации).";
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

        private void BeginAnalyticsLoading(string text)
        {
            _anBgTaskCount++;
            if (anLoadingText != null) anLoadingText.Text = text;
            if (anLoadingBanner != null) anLoadingBanner.Visibility = Visibility.Visible;
        }

        private void EndAnalyticsLoading()
        {
            if (_anBgTaskCount > 0) _anBgTaskCount--;
            if (_anBgTaskCount == 0 && anLoadingBanner != null)
                anLoadingBanner.Visibility = Visibility.Collapsed;
        }

        private void SetAnalyticsExportButtonsEnabled(bool enabled)
        {
            if (anBtnExportSummary != null) anBtnExportSummary.IsEnabled = enabled;
            if (anBtnExportKudir != null) anBtnExportKudir.IsEnabled = enabled;
            if (anBtnExport1C != null) anBtnExport1C.IsEnabled = enabled;
            if (anBtnDiag != null) anBtnDiag.IsEnabled = enabled;
        }

        private void ClearAnalyticsFiles()
        {
            _anAnalyzeSeq++;
            anLoadedFiles.Clear();
            anDemoSkuCostsOverride = null;
            anCurrentSnapshot = null;
            if (_anBgTaskCount == 0 && anLoadingBanner != null) anLoadingBanner.Visibility = Visibility.Collapsed;
            if (anWarningsBanner != null) anWarningsBanner.Visibility = Visibility.Collapsed;
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
                if (anWarningsBanner != null) anWarningsBanner.Visibility = Visibility.Collapsed;
                if (_anBgTaskCount == 0 && anLoadingBanner != null) anLoadingBanner.Visibility = Visibility.Collapsed;
                return;
            }

            int seq = ++_anAnalyzeSeq;
            var filesCopy = new List<string>(anLoadedFiles);
            var prof = ActiveCompanyProfile;
            string regime = prof != null ? prof.TaxRegime : CompanyProfilesManager.RegimeUsn6;
            decimal taxRate = ParseTaxRate();
            decimal ipNorm = prof != null ? prof.IpExpenseNormPercent : 30m;
            string usnMethod = (prof != null && !string.IsNullOrWhiteSpace(prof.UsnMethod))
                ? prof.UsnMethod
                : CompanyProfilesManager.UsnMethodCashBasis;
            var costsToUse = anDemoSkuCostsOverride != null
                ? new Dictionary<string, decimal>(anDemoSkuCostsOverride, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, decimal>(anSkuCosts, StringComparer.OrdinalIgnoreCase);

            BeginAnalyticsLoading(string.Format("⏳ Разбор файлов ({0} шт.) в фоновом потоке...", filesCopy.Count));

            ThreadPool.QueueUserWorkItem(delegate
            {
                AnalyticsSnapshot resultSnap = null;
                Exception error = null;
                try
                {
                    resultSnap = AnalyticsEngine.AnalyzeFiles(filesCopy, regime, taxRate, costsToUse, ipNorm, usnMethod);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    EndAnalyticsLoading();
                    if (seq != _anAnalyzeSeq) return;
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

        private void RenderWarningsBanner(AnalyticsSnapshot s, bool unparsedAll)
        {
            if (anWarningsBanner == null || anWarningsList == null) return;
            anWarningsList.Children.Clear();

            var warnings = s != null && s.Warnings != null ? s.Warnings : new List<string>();
            if (!unparsedAll && warnings.Count == 0)
            {
                anWarningsBanner.Visibility = Visibility.Collapsed;
                return;
            }

            if (unparsedAll)
            {
                anWarningsBanner.Background = GetBrush("#FEF2F2");
                anWarningsBanner.BorderBrush = GetBrush("#FECACA");
                anWarningsTitle.Foreground = GetBrush("#B91C1C");
                anWarningsTitle.Text = "❌ Не удалось распознать отчёт или выписку";
            }
            else
            {
                anWarningsBanner.Background = GetBrush("#FFFBEB");
                anWarningsBanner.BorderBrush = GetBrush("#FDE68A");
                anWarningsTitle.Foreground = GetBrush("#B45309");
                anWarningsTitle.Text = string.Format("⚠️ Замечания и предупреждения ({0})", warnings.Count);
            }

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    anWarningsList.Children.Add(new TextBlock
                    {
                        Text = "• " + w,
                        FontSize = 11,
                        Foreground = GetBrush(unparsedAll ? "#991B1B" : "#78350F"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 1, 0, 2)
                    });
                }
            }
            else if (unparsedAll)
            {
                anWarningsList.Children.Add(new TextBlock
                {
                    Text = "• Файлы не содержат распознанных таблиц WB/Ozon или выписок банка РБ (.xlsx / 1CClientBankExchange .txt / .csv).",
                    FontSize = 11,
                    Foreground = GetBrush("#991B1B"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 2)
                });
            }

            if (unparsedAll)
            {
                anWarningsHintText.Text = anLoadedFiles.Count == 1
                    ? "Нечитаемый файл остался в очереди. Нажмите «✕ Очистить список», чтобы убрать его перед следующей загрузкой, или проверьте структуру через «🛟 Диагностика файла»."
                    : string.Format("Нераспознанные файлы ({0} шт.) остаются в очереди до сброса. Нажмите «✕ Очистить список» или «🛟 Диагностика файла».", anLoadedFiles.Count);
                anWarningsHintText.Foreground = GetBrush("#7F1D1D");
                anWarningsHintText.Visibility = Visibility.Visible;
                anWarningsActionsRow.Visibility = Visibility.Visible;
            }
            else
            {
                anWarningsHintText.Visibility = Visibility.Collapsed;
                anWarningsActionsRow.Visibility = Visibility.Collapsed;
            }

            anWarningsBanner.Visibility = Visibility.Visible;
        }

        private void RenderAnalyticsPreview()
        {
            if (anCurrentSnapshot == null) return;
            var s = anCurrentSnapshot;

            if (s.Reports.Count == 0 && s.BankTransactions.Count == 0)
            {
                anDropTitle.Text = string.Format("⚠️ Загружено файлов: {0}, но формат отчёта / выписки не распознан", anLoadedFiles.Count);
                anDropSubtitle.Text = "нажмите «✕ Очистить», чтобы сбросить очередь, или проверьте структуру через «🛟 Диагностика файла»";
                RenderWarningsBanner(s, true);
                anKpiCard.Visibility = Visibility.Collapsed;
                anSkuCard.Visibility = Visibility.Collapsed;
                return;
            }

            RenderWarningsBanner(s, false);

            string demoPrefix = anDemoSkuCostsOverride != null ? "[ДЕМО] " : "";
            anDropTitle.Text = string.Format(
                "{0}✅ Отчётов WB: {1}  |  Ozon: {2}  |  Операций банка: {3}",
                demoPrefix, s.WbReportCount, s.OzonReportCount, s.BankTransactions.Count);
            anDropSubtitle.Text = s.DeduplicatedSkipped > 0
                ? string.Format("файлов: {0} (дубликатов пропущено: {1}) — можно докинуть ещё", anLoadedFiles.Count, s.DeduplicatedSkipped)
                : string.Format("файлов загружено: {0} — нажмите, чтобы добавить ещё", anLoadedFiles.Count);

            anKpiContent.Children.Clear();
            var kpiHdrGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            kpiHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            kpiHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            kpiHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var kpiHdrStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (s.TaxRegime == CompanyProfilesManager.RegimeOsn20)
            {
                var expPill = new Border
                {
                    Background = GetBrush("#7C3AED"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                expPill.Child = new TextBlock
                {
                    Text = "ЭКСПЕРИМЕНТАЛЬНЫЙ РЕЖИМ",
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                kpiHdrStack.Children.Add(expPill);
            }
            var kpiHdrText = new TextBlock
            {
                Text = "Ключевые показатели периода • " + s.TaxRegimeTitle,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            kpiHdrStack.Children.Add(kpiHdrText);
            Grid.SetColumn(kpiHdrStack, 0);
            kpiHdrGrid.Children.Add(kpiHdrStack);

            var btnExplainAudit = CreateSmallPillButton("❓ Как посчитано?", "#EBF3FF", "#007AFF");
            btnExplainAudit.ToolTip = "Показать пошаговую расшифровку формул и чисел расчёта";
            btnExplainAudit.VerticalAlignment = VerticalAlignment.Center;
            btnExplainAudit.Click += delegate { OpenCalculationAuditWindow(s); };
            Grid.SetColumn(btnExplainAudit, 2);
            kpiHdrGrid.Children.Add(btnExplainAudit);

            anKpiContent.Children.Add(kpiHdrGrid);

            if (s.TaxRegime == CompanyProfilesManager.RegimeOsn20 && !IsProUiMode())
            {
                var simpleOsnWarn = new Border
                {
                    Background = GetBrush("#F5F3FF"),
                    BorderBrush = GetBrush("#DDD6FE"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(9, 6, 9, 6),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                simpleOsnWarn.Child = new TextBlock
                {
                    Text = "⚠️ ОСН — экспериментальный режим: в Базовом режиме цифры являются ориентиром, итоговый НДС, ЭСЧФ и налог на прибыль рекомендуется проверять с бухгалтером.",
                    FontSize = 10.5,
                    Foreground = GetBrush("#5B21B6"),
                    TextWrapping = TextWrapping.Wrap
                };
                anKpiContent.Children.Add(simpleOsnWarn);
            }

            if (s.Reports.Count > 0)
            {
                string revLabel = s.TaxRegime == CompanyProfilesManager.RegimeUsn6
                    ? (s.UsnMethodUsed == CompanyProfilesManager.UsnMethodCashBasis
                        ? "Реализация МП (справочно; УСН по кассовому методу)"
                        : "Реализация МП (база УСН)")
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
                if (s.IpHighIncomeRateApplied || s.IpGrossIncome > 500000m)
                {
                    AddKpiPairRow(anKpiContent,
                        "Порог ИП превышен (> 500 000 BYN)", "30% + юрлицо", "#DC2626",
                        "Доход > 500 000 BYN: применяется ставка подоходного налога 30%, а с 1 января следующего года работа в статусе ИП запрещена — обязателен переход в юрлицо (Закон № 365-З)");
                }
                else if (s.IpNear500kLimit)
                {
                    AddKpiPairRow(anKpiContent,
                        "Приближение к порогу ИП (> 400 000 BYN)", FmtByn(s.IpGrossIncome), "#D97706",
                        "Доход приближается к годовому лимиту ИП 500 000 BYN: при превышении применяется ставка 30% и с 1 января следующего года работа как ИП запрещена (переход в юрлицо, Закон № 365-З)");
                }

                decimal ipNormPct = (s.IpExpenseNormPercent == 20m || s.IpExpenseNormPercent == 30m) ? s.IpExpenseNormPercent : 30m;
                string ipNormKind = ipNormPct == 30m ? "30% розница" : "20% общий";
                AddKpiPairRow(anKpiContent,
                    string.Format("Подоходный налог ИП ({0:0.##}%)", s.EffectiveTaxRateApplied),
                    FmtByn(s.EstimatedTaxByRealization), "#DC2626",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Доходы: {0} | Докум. расходы: {1} (налог {2}) vs Норматив {3}: {4} (налог {5}) → выгоднее {6}",
                        FmtByn(s.IpGrossIncome),
                        FmtByn(s.IpDocumentedExpenses),
                        FmtByn(s.IpTaxDocumented),
                        ipNormKind,
                        FmtByn(s.IpStandardDeduction20),
                        FmtByn(s.IpTaxStandard20),
                        s.IpUsedStandardDeduction ? ("норматив " + ipNormKind) : "докум. расходы"));

                string fsznCeilingNote = "для 2026 г.: база 12 МЗП = 10 296 BYN, макс. взнос 35% = 3 603.60 BYN";
                if (s.IpYearlyBreakdown != null && s.IpYearlyBreakdown.Count == 1)
                {
                    var y0 = s.IpYearlyBreakdown[0];
                    decimal y0Max = Math.Round(y0.FsznCap12Mzp * 0.35m, 2, MidpointRounding.AwayFromZero);
                    fsznCeilingNote = string.Format(
                        CultureInfo.InvariantCulture,
                        "для {0} г.: МЗП {1:0.##} BYN, база 12 МЗП = {2} BYN, макс. взнос 35% = {3} BYN",
                        y0.Year, y0.MzpMonthly, FmtByn(y0.FsznCap12Mzp), FmtByn(y0Max));
                }

                string fsznHint = s.IpFsznCapped12Mzp
                    ? string.Format("Достигнут потолок ({0}, Закон № 90-З). ПУ-3 «за себя» отменена: ФСЗН считает по данным МНС, уплата до 31.03 след. года", fsznCeilingNote)
                    : string.Format("35% от факт. чистого дохода {0} BYN ({1}, Закон № 90-З). ПУ-3 «за себя» отменена: ФСЗН считает по данным МНС, уплата до 31.03 след. года", FmtByn(s.IpFsznBase), fsznCeilingNote);
                AddKpiPairRow(anKpiContent,
                    s.IpFsznCapped12Mzp ? "Взнос ФСЗН ИП 35% (потолок 12 МЗП)" : "Взнос ФСЗН ИП 35% (от фактического чистого дохода)",
                    FmtByn(s.IpFszn35), "#B45309",
                    fsznHint);

                if (s.BankTransactions.Count > 0 || s.BankFsznDebet > 0m)
                {
                    AddKpiPairRow(anKpiContent,
                        "Учтённые в расходах взносы ФСЗН / БГС (с р/с)",
                        FmtByn(s.BankFsznDebet), "#0F766E",
                        s.BankFsznDebet > 0m
                            ? "Уплаченные по р/с взносы ФСЗН/Белгосстрах включены в документальные расходы ИП"
                            : "Уплат ФСЗН/Белгосстраха в загруженной выписке не найдено");
                }

                if (s.IpBankPrepaymentsExcluded > 0m)
                {
                    AddKpiPairRow(anKpiContent,
                        "Исключённые авансы/оплаты поставщикам с р/с",
                        FmtByn(s.IpBankPrepaymentsExcluded), "#B45309",
                        "Не включены в расходы ИП автоматически (без накладной списания в расход не принимаются и не дублируют себестоимость SKU)");
                }

                if (s.IpYearlyBreakdown != null && s.IpYearlyBreakdown.Count > 1)
                {
                    var yrBox = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = GetBrush("#E5E5EA"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 7, 10, 7),
                        Margin = new Thickness(0, 4, 0, 6)
                    };
                    var yrStack = new StackPanel();
                    yrStack.Children.Add(new TextBlock
                    {
                        Text = "📅 По-годовая разбивка ИП (подоходный налог и потолок ФСЗН по годам)",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = GetBrush("#1C1C1E"),
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    foreach (var yb in s.IpYearlyBreakdown)
                    {
                        decimal maxYearFszn = Math.Round(yb.FsznCap12Mzp * 0.35m, 2, MidpointRounding.AwayFromZero);
                        string yrMethod = yb.UsedStandardDeduction
                            ? string.Format(CultureInfo.InvariantCulture, "норматив {0:0.##}%", ipNormPct)
                            : "докум. расходы";
                        string fbNote = yb.UsesFallbackMzp ? " (ориентир по известной МЗП)" : "";
                        yrStack.Children.Add(new TextBlock
                        {
                            Text = string.Format(
                                CultureInfo.InvariantCulture,
                                "• {0} г.: доход {1} BYN → налог ({2:0.##}%, {3}) = {4} BYN | ФСЗН 35% = {5} BYN (база {6} при потолке 12 МЗП = {7}, макс. {8} BYN){9}",
                                yb.Year, FmtByn(yb.GrossIncome), yb.EffectiveRatePercent, yrMethod,
                                FmtByn(yb.IncomeTax), FmtByn(yb.Fszn35), FmtByn(yb.FsznBase),
                                FmtByn(yb.FsznCap12Mzp), FmtByn(maxYearFszn), fbNote),
                            FontSize = 10.5,
                            Foreground = GetBrush("#3A3A3C"),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 1, 0, 2)
                        });
                    }
                    yrBox.Child = yrStack;
                    anKpiContent.Children.Add(yrBox);
                }

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
            else if (s.TaxRegime == CompanyProfilesManager.RegimeUsn6)
            {
                decimal baseDelta = s.UsnCashBasisIncome - s.UsnRealizationBase;
                decimal taxDelta = s.UsnCashBasisTax - s.UsnRealizationTax;
                string signBase = baseDelta > 0m ? "+" : "";
                string signTax = taxDelta > 0m ? "+" : "";

                if (s.UsnMethodUsed == CompanyProfilesManager.UsnMethodCashBasis)
                {
                    AddKpiPairRow(anKpiContent,
                        string.Format(CultureInfo.InvariantCulture, "Налог УСН ({0:0.##}%) — кассовый метод (ст. 328 НК РБ)", s.EffectiveTaxRateApplied),
                        FmtByn(s.UsnCashBasisTax), "#DC2626",
                        string.Format("Кассовая база: {0} BYN = р/с от МП ({1}) + взаимозачёт МП ({2}) + облаг. р/с ({3})",
                            FmtByn(s.UsnCashBasisIncome), FmtByn(s.UsnCashMpBankCredits), FmtByn(s.UsnCashMpOffset), FmtByn(s.BankOtherCredits)));

                    AddKpiPairRow(anKpiContent,
                        "Сравнение: Кассовый метод (ст. 328 НК РБ: р/с + взаимозачёт) vs По реализации отчётов МП",
                        FmtByn(s.UsnRealizationTax), "#636366",
                        string.Format("По реализации отчётов МП: база {0} BYN → налог {1} BYN  |  Дельта переходящих выплат: база {2}{3} BYN (налог {4}{5} BYN)",
                            FmtByn(s.UsnRealizationBase), FmtByn(s.UsnRealizationTax), signBase, FmtByn(baseDelta), signTax, FmtByn(taxDelta)));
                }
                else
                {
                    string noBankHint = s.UsnCashBasisIncome <= 0m
                        ? " • для кассового метода (ст. 328 НК РБ) загрузите выписку банка"
                        : "";
                    AddKpiPairRow(anKpiContent,
                        string.Format(CultureInfo.InvariantCulture, "Налог УСН ({0:0.##}%) — по реализации отчётов МП", s.EffectiveTaxRateApplied),
                        FmtByn(s.UsnRealizationTax), "#DC2626",
                        string.Format("База: {0} BYN (реализация МП {1} + комп. {2} + облаг. р/с {3}){4}",
                            FmtByn(s.UsnRealizationBase), FmtByn(s.TotalRealizedP), FmtByn(s.TotalCompensations), FmtByn(s.BankOtherCredits), noBankHint));

                    if (s.UsnCashBasisIncome > 0m)
                    {
                        AddKpiPairRow(anKpiContent,
                            "Сравнение: Кассовый метод (ст. 328 НК РБ: р/с + взаимозачёт) vs По реализации отчётов МП",
                            FmtByn(s.UsnCashBasisTax), "#0F766E",
                            string.Format("Кассовый метод (р/с {0} + взаимозачёт {1} + облаг. {2} = {3} BYN → налог {4} BYN)  |  Дельта переходящих выплат: база {5}{6} BYN (налог {7}{8} BYN)",
                                FmtByn(s.UsnCashMpBankCredits), FmtByn(s.UsnCashMpOffset), FmtByn(s.BankOtherCredits),
                                FmtByn(s.UsnCashBasisIncome), FmtByn(s.UsnCashBasisTax),
                                signBase, FmtByn(baseDelta), signTax, FmtByn(taxDelta)));
                    }
                }
            }
            else
            {
                AddKpiPairRow(anKpiContent,
                    string.Format(CultureInfo.InvariantCulture, "Ориентир налога ({0:0.##}%)", s.EffectiveTaxRateApplied),
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
            if (_anExportBusy) return;

            var snap = anCurrentSnapshot;
            string outDir = GetAnalyticsOutputDirectory();
            string fileName = "Buhly_Сводка_" + DateTime.Now.ToString("dd.MM.yyyy_HH-mm") + ".xlsx";
            string outPath = Path.Combine(outDir, fileName);

            _anExportBusy = true;
            SetAnalyticsExportButtonsEnabled(false);
            BeginAnalyticsLoading("⏳ Формирование Excel-сводки...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception err = null;
                try
                {
                    Directory.CreateDirectory(outDir);
                    AnalyticsEngine.ExportAnalyticsXlsx(snap, outPath);
                }
                catch (Exception ex)
                {
                    err = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _anExportBusy = false;
                    SetAnalyticsExportButtonsEnabled(true);
                    EndAnalyticsLoading();
                    if (err != null)
                    {
                        MessageBox.Show("Не удалось создать Excel-сводку:\n" + err.Message,
                            "Ошибка выгрузки", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    anLastExportPath = outPath;
                    try { WaybillEngine.OpenFileWithShell(outPath); } catch { }
                    ShowToast("✅ Excel-сводка создана и открыта: " + fileName);
                }));
            });
        }

        private void ExportKudirReport()
        {
            if (anCurrentSnapshot == null ||
                (anCurrentSnapshot.Reports.Count == 0 && anCurrentSnapshot.BankTransactions.Count == 0))
            {
                ShowToast("⚠️ Сначала загрузите отчёты WB/Ozon или выписку банка");
                return;
            }
            if (_anExportBusy) return;

            var snap = anCurrentSnapshot;
            var prof = ActiveCompanyProfile;
            string outDir = GetAnalyticsOutputDirectory();
            string fileName = "Buhly_КУДиР_Раздел1_" + DateTime.Now.ToString("dd.MM.yyyy_HH-mm") + ".xlsx";
            string outPath = Path.Combine(outDir, fileName);

            _anExportBusy = true;
            SetAnalyticsExportButtonsEnabled(false);
            BeginAnalyticsLoading("⏳ Формирование КУДиР (Раздел I)...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception err = null;
                try
                {
                    Directory.CreateDirectory(outDir);
                    AnalyticsEngine.ExportKudirXlsx(snap, prof, outPath);
                }
                catch (Exception ex)
                {
                    err = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _anExportBusy = false;
                    SetAnalyticsExportButtonsEnabled(true);
                    EndAnalyticsLoading();
                    if (err != null)
                    {
                        MessageBox.Show("Не удалось сформировать КУДиР:\n" + err.Message,
                            "Ошибка КУДиР", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    try { WaybillEngine.OpenFileWithShell(outPath); } catch { }
                    ShowToast("📒 КУДиР (Раздел I) сформирована и открыта: " + fileName);
                }));
            });
        }

        private void Export1CReport()
        {
            if (anCurrentSnapshot == null ||
                (anCurrentSnapshot.Reports.Count == 0 && anCurrentSnapshot.BankTransactions.Count == 0))
            {
                ShowToast("⚠️ Сначала загрузите отчёты WB/Ozon или выписку банка");
                return;
            }
            if (_anExportBusy) return;

            string targetPath;
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
                targetPath = dlg.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть диалог сохранения 1С:\n" + ex.Message,
                    "Ошибка выгрузки 1С", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var snap = anCurrentSnapshot;
            var prof = ActiveCompanyProfile;
            bool isCsv = targetPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

            _anExportBusy = true;
            SetAnalyticsExportButtonsEnabled(false);
            BeginAnalyticsLoading(isCsv ? "⏳ Формирование CSV для 1С..." : "⏳ Формирование XML для 1С...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception err = null;
                try
                {
                    if (isCsv)
                        AnalyticsEngine.Export1CCsv(snap, prof, targetPath);
                    else
                        AnalyticsEngine.Export1CXml(snap, prof, targetPath);
                }
                catch (Exception ex)
                {
                    err = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _anExportBusy = false;
                    SetAnalyticsExportButtonsEnabled(true);
                    EndAnalyticsLoading();
                    if (err != null)
                    {
                        MessageBox.Show("Не удалось сохранить выгрузку для 1С:\n" + err.Message,
                            "Ошибка выгрузки 1С", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    try { WaybillEngine.OpenFileWithShell(targetPath); } catch { }
                    ShowToast("📤 Выгрузка для 1С сохранена: " + Path.GetFileName(targetPath));
                }));
            });
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

        private void RunAnonymizedFileDiagnostics()
        {
            if (_anExportBusy) return;
            var dlg = new OpenFileDialog
            {
                Title = "Выберите файл для обезличенной диагностики",
                Filter = "Файлы отчётов и выписок (*.xlsx;*.zip;*.txt;*.csv)|*.xlsx;*.zip;*.txt;*.csv|Все файлы (*.*)|*.*",
                Multiselect = false
            };
            if (dlg.ShowDialog() != true) return;

            string srcPath = dlg.FileName;
            _anExportBusy = true;
            SetAnalyticsExportButtonsEnabled(false);
            BeginAnalyticsLoading("⏳ Создание обезличенного диагностического отчёта...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string outPath = null;
                Exception err = null;
                try
                {
                    string reportText = AnalyticsEngine.GenerateAnonymizedDiagReport(srcPath);
                    string fileName = "Buhly_Diag_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt";

                    string targetDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
                        targetDir = Path.GetTempPath();

                    outPath = Path.Combine(targetDir, fileName);
                    try
                    {
                        File.WriteAllText(outPath, reportText, new System.Text.UTF8Encoding(false));
                    }
                    catch
                    {
                        outPath = Path.Combine(Path.GetTempPath(), fileName);
                        File.WriteAllText(outPath, reportText, new System.Text.UTF8Encoding(false));
                    }
                }
                catch (Exception ex)
                {
                    err = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _anExportBusy = false;
                    SetAnalyticsExportButtonsEnabled(true);
                    EndAnalyticsLoading();
                    if (err != null)
                    {
                        MessageBox.Show("Не удалось создать диагностический отчёт:\n" + err.Message,
                            "Диагностика файла", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    try { WaybillEngine.OpenFileWithShell(outPath); } catch { }
                    ShowToast("🛟 Обезличенный отчёт сохранён: " + Path.GetFileName(outPath));
                }));
            });
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
                    try
                    {
                        int imported = AnalyticsEngine.ImportSkuCostsFromTextOrCsv(dlg.FileName, activeCosts);
                        if (anDemoSkuCostsOverride == null)
                            AnalyticsEngine.SaveSkuCosts(anSkuCosts);
                        populateList();
                        if (imported > 0)
                            ShowToast(string.Format("📥 Загружено цен себестоимости: {0}", imported));
                        else
                            ShowToast("⚠️ Строки вида «Артикул; Цена» в файле не найдены");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Не удалось импортировать файл себестоимости.\n\nВозможные причины:\n• файл сейчас открыт в Excel — закройте его и повторите попытку;\n• файл сохранён в устаревшем формате .xls или защищён паролем (пересохраните в .xlsx или .csv);\n• файл повреждён.\n\nДетали: " + ex.Message,
                            "Ошибка импорта себестоимости", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
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

        private void OpenCalculationAuditWindow(AnalyticsSnapshot snap)
        {
            if (snap == null) return;

            var win = new Window
            {
                Title = "Buhly • Как посчитано?",
                Width = 680,
                Height = 520,
                MinWidth = 500,
                MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = GetBrush("#F2F2F7"),
                FontFamily = this.FontFamily
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            headerStack.Children.Add(new TextBlock
            {
                Text = "❓ Как посчитаны показатели",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Пошаговая расшифровка источников, налоговой базы, формул и сверки с банком (" + snap.TaxRegimeTitle + ")",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetRow(headerStack, 0);
            root.Children.Add(headerStack);

            string auditText = (snap.CalculationAuditDetails != null && snap.CalculationAuditDetails.Count > 0)
                ? string.Join("\r\n\r\n", snap.CalculationAuditDetails)
                : "Нет данных для расшифровки.";

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var cardsPanel = new StackPanel();

            if (snap.CalculationAuditDetails != null && snap.CalculationAuditDetails.Count > 0)
            {
                foreach (var section in snap.CalculationAuditDetails)
                {
                    if (string.IsNullOrWhiteSpace(section)) continue;
                    bool isWarnSection = section.TrimStart().StartsWith("7)") || section.IndexOf("Предупреждения", StringComparison.OrdinalIgnoreCase) >= 0;

                    var secCard = new Border
                    {
                        Background = isWarnSection ? GetBrush("#FFFBEB") : Brushes.White,
                        BorderBrush = isWarnSection ? GetBrush("#FDE68A") : GetBrush("#E5E5EA"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(14, 10, 14, 10),
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    var secStack = new StackPanel();
                    var lines = section.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        string firstLine = lines[0].Trim();
                        string titlePart = firstLine;
                        string restFirst = "";
                        int colonIdx = firstLine.IndexOf(':');
                        if (colonIdx > 0 && colonIdx < 90)
                        {
                            titlePart = firstLine.Substring(0, colonIdx).Trim();
                            restFirst = firstLine.Substring(colonIdx + 1).Trim();
                        }

                        secStack.Children.Add(new TextBlock
                        {
                            Text = titlePart,
                            FontSize = 12.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = GetBrush(isWarnSection ? "#B45309" : "#0284C7"),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 4)
                        });

                        if (!string.IsNullOrEmpty(restFirst))
                        {
                            secStack.Children.Add(new TextBlock
                            {
                                Text = restFirst,
                                FontSize = 11.5,
                                Foreground = GetBrush("#1C1C1E"),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 0, 0, lines.Length > 1 ? 3 : 0)
                            });
                        }

                        for (int li = 1; li < lines.Length; li++)
                        {
                            string ln = lines[li].Trim();
                            if (string.IsNullOrEmpty(ln)) continue;
                            bool isBullet = ln.StartsWith("•") || ln.StartsWith("-");
                            secStack.Children.Add(new TextBlock
                            {
                                Text = ln,
                                FontSize = 11.5,
                                Foreground = GetBrush(isWarnSection ? "#78350F" : "#3A3A3C"),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(isBullet ? 8 : 0, 2, 0, 1)
                            });
                        }
                    }
                    secCard.Child = secStack;
                    cardsPanel.Children.Add(secCard);
                }
            }
            else
            {
                var emptyCard = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = GetBrush("#E5E5EA"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(14)
                };
                emptyCard.Child = new TextBlock
                {
                    Text = "Нет данных для расшифровки.",
                    FontSize = 12,
                    Foreground = GetBrush("#636366")
                };
                cardsPanel.Children.Add(emptyCard);
            }

            scroll.Content = cardsPanel;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnCopy = CreateSolidActionButton("📋 Копировать всё", "#007AFF", Brushes.White);
            btnCopy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(auditText);
                    ShowToast("✓ Расшифровка расчёта скопирована в буфер обмена");
                }
                catch (Exception ex)
                {
                    ShowToast("⚠️ Не удалось скопировать: " + ex.Message);
                }
            };
            Grid.SetColumn(btnCopy, 0);
            footer.Children.Add(btnCopy);

            var btnClose = CreateCompactActionButton("Закрыть", "#E5E5EA", "#1C1C1E");
            btnClose.Height = 38;
            btnClose.Padding = new Thickness(14, 0, 14, 0);
            btnClose.Click += delegate { win.Close(); };
            Grid.SetColumn(btnClose, 2);
            footer.Children.Add(btnClose);

            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            win.Content = root;
            win.ShowDialog();
        }
    }
}
