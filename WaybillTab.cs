using System;
using System.Collections.Generic;
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
using System.Windows.Threading;
using Microsoft.Win32;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private WaybillProfile wbProfile;
        private List<string> wbLoadedFiles = new List<string>();
        private WaybillBuildResult wbCurrentResult;
        private bool _suppressWaybillEvents = false;
        private int _wbLoadSeq = 0;
        private bool _wbIsGenerating = false;
        private bool _wbIsDemoLoaded = false;

        // UI controls for Waybills tab
        private ComboBox wbDocTypeCombo;
        private ComboBox wbPrintModeCombo;
        private StackPanel wbBsoMarginsPanel;
        private TextBox wbTopMarginBox;
        private TextBox wbLeftMarginBox;
        private StackPanel wbAppendixMetaPanel;
        private TextBox wbBsoSeriesBox;
        private TextBox wbBsoNumberBox;
        private Border wbTtn1FieldsPanel;
        private TextBox wbVehicleBox;
        private TextBox wbDriverBox;
        private TextBox wbLoadingPointBox;
        private TextBox wbUnloadingPointBox;
        private TextBox wbTransportCustomerBox;

        private Border wbDropZone;
        private TextBlock wbDropTitle;
        private TextBlock wbDropSubtitle;
        private Border wbLoadingBanner;
        private TextBlock wbLoadingText;
        private Border wbPreviewCard;
        private TextBlock wbSummaryText;
        private DataGrid wbPreviewGrid;
        private DataGridTextColumn wbColCargoPlaces;
        private DataGridTextColumn wbColCargoMass;
        private Border wbPreviewAlertBanner;
        private TextBlock wbPreviewAlertText;
        private DatePicker wbDatePicker;
        private CheckBox wbPickingListCheck;
        private Button wbBtnSaveOpen;
        private Button wbBtnPrintNow;
        private StackPanel wbSettingsBody;
        private TextBlock wbSettingsChevron;
        private StackPanel wbMemoBody;
        private TextBlock wbMemoChevron;

        private TextBox wbSenderUnpBox;
        private TextBox wbSenderInfoBox;
        private TextBox wbSignerBox;
        private TextBox wbReceiverUnpBox;
        private TextBox wbReceiverInfoBox;
        private TextBox wbBasisBox;
        private ComboBox wbVatCombo;
        private ComboBox wbPriceInputModeCombo;
        private CheckBox wbSortDescCheck;
        private CheckBox wbUseRulesCheck;
        private TextBox wbRulesBox;
        private StackPanel wbRulesProSection;
        private TextBlock wbProModeHint;
        private TextBox wbOutputFolderBox;
        private Border wbNamesStatusBorder;
        private TextBlock wbNamesStatusText;

        private void BuildWaybillsTab(StackPanel container)
        {
            var actProf = ActiveCompanyProfile;
            wbProfile = (actProf != null && actProf.WaybillProfile != null)
                ? actProf.WaybillProfile
                : WaybillEngine.LoadProfile();
            WaybillEngine.NormalizeProfileDefaults(wbProfile);
            if (actProf != null) actProf.SyncToWaybillProfile();

            // CARD 1: MODE, DROP ZONE, EDITABLE PREVIEW & ACTIONS
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
                Text = "📦  Накладные ТН-2 / ТТН-1 (WB FBS, WB FBO, БСО и Приложение)",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Перетащите файлы поставки WB FBS/FBO (.xlsx) — проверьте или поправьте цены и названия прямо в предпросмотре, выберите режим бланка и сформируйте накладную.",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            });

            // DOC TYPE & PRINT MODE SELECTORS
            var modeGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var docTypeStack = new StackPanel();
            docTypeStack.Children.Add(CreateFieldLabel("Тип накладной"));
            wbDocTypeCombo = new ComboBox { FontSize = 12, Margin = new Thickness(0, 3, 0, 0) };
            wbDocTypeCombo.Items.Add(new ComboBoxItem { Content = "ТН-2 (Товарная накладная)", Tag = WaybillEngine.DocTypeTn2 });
            wbDocTypeCombo.Items.Add(new ComboBoxItem { Content = "ТТН-1 (Товарно-транспортная)", Tag = WaybillEngine.DocTypeTtn1 });
            SelectComboByTag(wbDocTypeCombo, wbProfile.DocType, 0);
            wbDocTypeCombo.SelectionChanged += delegate
            {
                if (_suppressWaybillEvents) return;
                wbProfile.DocType = GetSelectedComboTag(wbDocTypeCombo, WaybillEngine.DocTypeTn2);
                UpdateModePanelsVisibility();
                SaveWaybillProfileEverywhere();
            };
            docTypeStack.Children.Add(wbDocTypeCombo);
            Grid.SetColumn(docTypeStack, 0);
            modeGrid.Children.Add(docTypeStack);

            var printModeStack = new StackPanel();
            printModeStack.Children.Add(CreateFieldLabel("Режим печати / бланка"));
            wbPrintModeCombo = new ComboBox { FontSize = 12, Margin = new Thickness(0, 3, 0, 0) };
            wbPrintModeCombo.Items.Add(new ComboBoxItem { Content = "Полный бланк А4 (черновик / согласование)", Tag = WaybillEngine.PrintModeFull });
            wbPrintModeCombo.Items.Add(new ComboBoxItem { Content = "Печать данных на бланк БСО (без сетки и шапки)", Tag = WaybillEngine.PrintModeBso });
            wbPrintModeCombo.Items.Add(new ComboBoxItem { Content = "Приложение к накладной (реестр А4)", Tag = WaybillEngine.PrintModeAppendix });
            SelectComboByTag(wbPrintModeCombo, wbProfile.PrintMode, 0);
            wbPrintModeCombo.SelectionChanged += delegate
            {
                if (_suppressWaybillEvents) return;
                wbProfile.PrintMode = GetSelectedComboTag(wbPrintModeCombo, WaybillEngine.PrintModeFull);
                UpdateModePanelsVisibility();
                SaveWaybillProfileEverywhere();
            };
            printModeStack.Children.Add(wbPrintModeCombo);
            Grid.SetColumn(printModeStack, 2);
            modeGrid.Children.Add(printModeStack);
            stack.Children.Add(modeGrid);

            wbProModeHint = new TextBlock
            {
                Text = "ℹ️ Расширенные правила группировки названий доступны в режиме «Бухгалтер PRO» (переключатель в меню слева).",
                FontSize = 10.5,
                Foreground = GetBrush("#8E8E93"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -4, 0, 8)
            };
            stack.Children.Add(wbProModeHint);

            // BSO MARGINS PANEL
            wbBsoMarginsPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };
            var bsoBorder = new Border
            {
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var bsoGrid = new Grid();
            bsoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bsoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            bsoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            bsoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bsoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            bsoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            bsoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblTopMm = CreateFieldLabel("Отступ сверху (мм):");
            lblTopMm.VerticalAlignment = VerticalAlignment.Center;
            lblTopMm.Margin = new Thickness(0, 0, 6, 0);
            bsoGrid.Children.Add(lblTopMm);

            wbTopMarginBox = CreateStyledInput(wbProfile.TopMarginMm.ToString("0.#", CultureInfo.InvariantCulture), "Поле сверху при печати на БСО (по умолч. 10 мм)");
            wbTopMarginBox.Padding = new Thickness(6, 4, 6, 4);
            Grid.SetColumn(wbTopMarginBox, 1);
            bsoGrid.Children.Add(wbTopMarginBox);

            var lblLeftMm = CreateFieldLabel("Отступ слева (мм):");
            lblLeftMm.VerticalAlignment = VerticalAlignment.Center;
            lblLeftMm.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(lblLeftMm, 3);
            bsoGrid.Children.Add(lblLeftMm);

            wbLeftMarginBox = CreateStyledInput(wbProfile.LeftMarginMm.ToString("0.#", CultureInfo.InvariantCulture), "Поле слева при печати на БСО (по умолч. 15 мм)");
            wbLeftMarginBox.Padding = new Thickness(6, 4, 6, 4);
            Grid.SetColumn(wbLeftMarginBox, 4);
            bsoGrid.Children.Add(wbLeftMarginBox);

            var bsoHint = new TextBlock
            {
                Text = "Печать только данных в поля БСО (без сетки и шапки). Шаблон подгоняет масштаб под ширину листа — меняйте отступы плавно (±1–3 мм).",
                FontSize = 10.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(bsoHint, 6);
            bsoGrid.Children.Add(bsoHint);
            bsoBorder.Child = bsoGrid;
            wbBsoMarginsPanel.Children.Add(bsoBorder);
            stack.Children.Add(wbBsoMarginsPanel);

            // APPENDIX SERIES & NUMBER PANEL
            wbAppendixMetaPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };
            var appBorder = new Border
            {
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var appGrid = new Grid();
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblSer = CreateFieldLabel("Серия БСО:");
            lblSer.VerticalAlignment = VerticalAlignment.Center;
            lblSer.Margin = new Thickness(0, 0, 6, 0);
            appGrid.Children.Add(lblSer);

            wbBsoSeriesBox = CreateStyledInput(wbProfile.BsoSeries, "Например: АА");
            wbBsoSeriesBox.Padding = new Thickness(6, 4, 6, 4);
            Grid.SetColumn(wbBsoSeriesBox, 1);
            appGrid.Children.Add(wbBsoSeriesBox);

            var lblNum = CreateFieldLabel("Номер БСО:");
            lblNum.VerticalAlignment = VerticalAlignment.Center;
            lblNum.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(lblNum, 3);
            appGrid.Children.Add(lblNum);

            wbBsoNumberBox = CreateStyledInput(wbProfile.BsoNumber, "Например: 1234567");
            wbBsoNumberBox.Padding = new Thickness(6, 4, 6, 4);
            Grid.SetColumn(wbBsoNumberBox, 4);
            appGrid.Children.Add(wbBsoNumberBox);

            var appHint = new TextBlock
            {
                Text = "Реестр А4 без ограничения в 178 строк с привязкой к серии и номеру основного БСО.",
                FontSize = 10.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(appHint, 6);
            appGrid.Children.Add(appHint);
            appBorder.Child = appGrid;
            wbAppendixMetaPanel.Children.Add(appBorder);
            stack.Children.Add(wbAppendixMetaPanel);

            // TTN-1 TRANSPORT FIELDS PANEL
            wbTtn1FieldsPanel = new Border
            {
                Background = GetBrush("#F0F7FF"),
                BorderBrush = GetBrush("#BFDBFE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 10),
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };
            var ttnStack = new StackPanel();
            ttnStack.Children.Add(new TextBlock
            {
                Text = "🚚  Транспортные реквизиты ТТН-1",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1D4ED8"),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var ttnRow1 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            ttnRow1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ttnRow1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            ttnRow1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var vehCol = new StackPanel();
            vehCol.Children.Add(CreateFieldLabel("Автомобиль (марка, гос. номер, прицеп)"));
            wbVehicleBox = CreateStyledInput(wbProfile.VehicleInfo, "Напр.: ГАЗель Next, 1234 AB-7");
            wbVehicleBox.Background = Brushes.White;
            wbVehicleBox.Margin = new Thickness(0, 2, 0, 0);
            vehCol.Children.Add(wbVehicleBox);
            Grid.SetColumn(vehCol, 0);
            ttnRow1.Children.Add(vehCol);

            var drvCol = new StackPanel();
            drvCol.Children.Add(CreateFieldLabel("Водитель (ФИО)"));
            wbDriverBox = CreateStyledInput(wbProfile.DriverInfo, "Напр.: Петров Пётр Петрович");
            wbDriverBox.Background = Brushes.White;
            wbDriverBox.Margin = new Thickness(0, 2, 0, 0);
            drvCol.Children.Add(wbDriverBox);
            Grid.SetColumn(drvCol, 2);
            ttnRow1.Children.Add(drvCol);
            ttnStack.Children.Add(ttnRow1);

            var ttnRow2 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            ttnRow2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ttnRow2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            ttnRow2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var loadCol = new StackPanel();
            loadCol.Children.Add(CreateFieldLabel("Пункт погрузки (если пусто — адрес отправителя)"));
            wbLoadingPointBox = CreateStyledInput(wbProfile.LoadingPoint, "Напр.: г. Минск, ул. ...");
            wbLoadingPointBox.Background = Brushes.White;
            wbLoadingPointBox.Margin = new Thickness(0, 2, 0, 0);
            loadCol.Children.Add(wbLoadingPointBox);
            Grid.SetColumn(loadCol, 0);
            ttnRow2.Children.Add(loadCol);

            var unloadCol = new StackPanel();
            unloadCol.Children.Add(CreateFieldLabel("Пункт разгрузки (если пусто — адрес получателя)"));
            wbUnloadingPointBox = CreateStyledInput(wbProfile.UnloadingPoint, "Напр.: г. Минск, СЦ WB ...");
            wbUnloadingPointBox.Background = Brushes.White;
            wbUnloadingPointBox.Margin = new Thickness(0, 2, 0, 0);
            unloadCol.Children.Add(wbUnloadingPointBox);
            Grid.SetColumn(unloadCol, 2);
            ttnRow2.Children.Add(unloadCol);
            ttnStack.Children.Add(ttnRow2);

            var custCol = new StackPanel();
            custCol.Children.Add(CreateFieldLabel("Заказчик автомобильной перевозки (плательщик)"));
            wbTransportCustomerBox = CreateStyledInput(wbProfile.TransportCustomer, "Напр.: ООО «ИМВБРБ», УНП 193630204 (если отличается от отправителя/получателя)");
            wbTransportCustomerBox.Background = Brushes.White;
            wbTransportCustomerBox.Margin = new Thickness(0, 2, 0, 0);
            custCol.Children.Add(wbTransportCustomerBox);
            ttnStack.Children.Add(custCol);

            wbTtn1FieldsPanel.Child = ttnStack;
            stack.Children.Add(wbTtn1FieldsPanel);

            // Drop zone
            wbDropZone = new Border
            {
                Background = GetBrush("#F0F7FF"),
                BorderBrush = GetBrush("#93C5FD"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 14, 14, 14),
                AllowDrop = true,
                Cursor = Cursors.Hand
            };
            var dropStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            wbDropTitle = new TextBlock
            {
                Text = "📥  Перетащите файлы wb-gi-*.xlsx или отгрузку WB FBO сюда",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#007AFF"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            wbDropSubtitle = new TextBlock
            {
                Text = "или нажмите здесь, чтобы выбрать файлы .xlsx на компьютере",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            dropStack.Children.Add(wbDropTitle);
            dropStack.Children.Add(wbDropSubtitle);
            wbDropZone.Child = dropStack;

            wbDropZone.MouseLeftButtonUp += delegate { SelectSupplyFilesDialog(); };
            wbDropZone.DragEnter += OnWaybillDragOver;
            wbDropZone.DragOver += OnWaybillDragOver;
            wbDropZone.DragLeave += delegate { ResetDropZoneVisual(false); };
            wbDropZone.Drop += OnWaybillDrop;

            stack.Children.Add(wbDropZone);

            // File action row (Choose / Demo / Folder / Clear)
            var fileBtnRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnSelectFiles = CreateCompactActionButton("📂  Выбрать файлы", "#EBF5FF", "#007AFF");
            btnSelectFiles.Click += delegate { SelectSupplyFilesDialog(); };
            Grid.SetColumn(btnSelectFiles, 0);
            fileBtnRow.Children.Add(btnSelectFiles);

            var btnDemoSupply = CreateCompactActionButton("⚡ Попробовать на демо-данных", "#FFFBEB", "#B45309");
            btnDemoSupply.Padding = new Thickness(11, 0, 11, 0);
            btnDemoSupply.ToolTip = "Загрузить синтетический пример поставки WB FBS (без личных данных)";
            btnDemoSupply.Click += delegate { LoadDemoSupplyFile(); };
            Grid.SetColumn(btnDemoSupply, 2);
            fileBtnRow.Children.Add(btnDemoSupply);

            var btnOpenFolder = CreateCompactActionButton("📁 Папка с ТН", "#F2F2F7", "#1C1C1E");
            btnOpenFolder.Padding = new Thickness(12, 0, 12, 0);
            btnOpenFolder.Click += delegate
            {
                try
                {
                    string folder = !string.IsNullOrWhiteSpace(wbProfile.OutputFolder)
                        ? wbProfile.OutputFolder
                        : new WaybillProfile().OutputFolder;
                    Directory.CreateDirectory(folder);
                    Process.Start("explorer.exe", "\"" + folder + "\"");
                }
                catch (Exception ex)
                {
                    ShowToast("Ошибка открытия папки: " + ex.Message);
                }
            };
            Grid.SetColumn(btnOpenFolder, 4);
            fileBtnRow.Children.Add(btnOpenFolder);

            var btnClearFiles = CreateCompactActionButton("✕ Очистить", "#F2F2F7", "#636366");
            btnClearFiles.Padding = new Thickness(12, 0, 12, 0);
            btnClearFiles.Click += delegate { ClearSupplyFiles(); };
            Grid.SetColumn(btnClearFiles, 6);
            fileBtnRow.Children.Add(btnClearFiles);
            stack.Children.Add(fileBtnRow);

            // Background loading / generation indicator
            wbLoadingBanner = new Border
            {
                Background = GetBrush("#EFF6FF"),
                BorderBrush = GetBrush("#BFDBFE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var wbLoadStack = new StackPanel();
            wbLoadingText = new TextBlock
            {
                Text = "⏳ Обработка файлов...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1D4ED8"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var wbLoadProg = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 5,
                Foreground = GetBrush("#0284C7"),
                Background = GetBrush("#DBEAFE"),
                BorderThickness = new Thickness(0)
            };
            wbLoadStack.Children.Add(wbLoadingText);
            wbLoadStack.Children.Add(wbLoadProg);
            wbLoadingBanner.Child = wbLoadStack;
            stack.Children.Add(wbLoadingBanner);

            // Editable Preview card (hidden until files loaded)
            wbPreviewCard = new Border
            {
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var prevStack = new StackPanel();

            var prevHeaderStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            wbSummaryText = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            prevHeaderStack.Children.Add(wbSummaryText);

            var prevActions = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            var btnApplyCat = CreateSmallPillButton("📖 Подставить цены", "#EBF3FF", "#007AFF");
            btnApplyCat.Margin = new Thickness(0, 0, 6, 4);
            btnApplyCat.ToolTip = "Заполнить нулевые цены из локального справочника цен по артикулу/названию";
            btnApplyCat.Click += delegate { ApplyCatalogToCurrentPreview(); };
            prevActions.Children.Add(btnApplyCat);

            var btnSaveCat = CreateSmallPillButton("💾 Запомнить цены", "#ECFDF5", "#15803D");
            btnSaveCat.Margin = new Thickness(0, 0, 6, 4);
            btnSaveCat.ToolTip = "Сохранить текущие цены из таблицы в справочник цен (для будущих отгрузок FBO)";
            btnSaveCat.Click += delegate { RememberCurrentPrices(); };
            prevActions.Children.Add(btnSaveCat);

            var btnResetEdits = CreateSmallPillButton("↻ Сброс", "#F2F2F7", "#3A3A3C");
            btnResetEdits.Margin = new Thickness(0, 0, 0, 4);
            btnResetEdits.ToolTip = "Перечитать исходные файлы и отменить ручные правки в предпросмотре";
            btnResetEdits.Click += delegate { RecomputePreviewIfLoaded(); ShowToast("↻ Предпросмотр обновляется из файлов..."); };
            prevActions.Children.Add(btnResetEdits);

            prevHeaderStack.Children.Add(prevActions);
            prevStack.Children.Add(prevHeaderStack);

            wbPreviewGrid = CreateEditablePreviewGrid();
            prevStack.Children.Add(wbPreviewGrid);

            wbPreviewAlertBanner = new Border
            {
                Background = GetBrush("#FFFBEB"),
                BorderBrush = GetBrush("#FDE68A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 6, 9, 6),
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed
            };
            wbPreviewAlertText = new TextBlock
            {
                FontSize = 11,
                Foreground = GetBrush("#92400E"),
                TextWrapping = TextWrapping.Wrap
            };
            wbPreviewAlertBanner.Child = wbPreviewAlertText;
            prevStack.Children.Add(wbPreviewAlertBanner);

            wbPreviewCard.Child = prevStack;
            stack.Children.Add(wbPreviewCard);

            // Date & Picking List row
            var optsGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            optsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            optsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            optsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            optsGrid.Children.Add(new TextBlock
            {
                Text = "Дата накладной:",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#3A3A3C"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            wbDatePicker = new DatePicker
            {
                SelectedDate = DateTime.Today,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12
            };
            Grid.SetColumn(wbDatePicker, 1);
            optsGrid.Children.Add(wbDatePicker);

            wbPickingListCheck = new CheckBox
            {
                Content = "📋 + Лист сборки",
                IsChecked = wbProfile.CreatePickingList,
                FontSize = 12,
                Foreground = GetBrush("#3A3A3C"),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Создаст рядом отдельный удобный файл Лист сборки со стикерами и артикулами"
            };
            wbPickingListCheck.Click += delegate
            {
                wbProfile.CreatePickingList = wbPickingListCheck.IsChecked == true;
                SaveWaybillProfileEverywhere();
            };
            Grid.SetColumn(wbPickingListCheck, 3);
            optsGrid.Children.Add(wbPickingListCheck);
            stack.Children.Add(optsGrid);

            // Visible names-mode status bar above creation buttons
            wbNamesStatusBorder = new Border
            {
                Background = GetBrush("#ECFDF5"),
                BorderBrush = GetBrush("#A7F3D0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 12, 0, 0)
            };
            wbNamesStatusText = new TextBlock
            {
                Text = "Названия: Оригинальные из файла",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#15803D")
            };
            wbNamesStatusBorder.Child = wbNamesStatusText;
            stack.Children.Add(wbNamesStatusBorder);

            // Primary Action Buttons
            var actGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            actGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            wbBtnSaveOpen = CreateSolidActionButton("💾  Создать и открыть", "#34C759", Brushes.White);
            wbBtnSaveOpen.Click += delegate { RunWaybillExport(false); };
            Grid.SetColumn(wbBtnSaveOpen, 0);
            actGrid.Children.Add(wbBtnSaveOpen);

            wbBtnPrintNow = CreateSolidActionButton("🖨  Сразу на печать", "#007AFF", Brushes.White);
            wbBtnPrintNow.Click += delegate { RunWaybillExport(true); };
            Grid.SetColumn(wbBtnPrintNow, 2);
            actGrid.Children.Add(wbBtnPrintNow);
            stack.Children.Add(actGrid);

            mainCard.Child = stack;
            container.Children.Add(mainCard);

            // CARD 2: REQUISITES, VAT & NORMALIZATION SETTINGS
            var settingsCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var settingsRoot = new StackPanel();

            bool startExpanded = string.IsNullOrWhiteSpace(wbProfile.SenderUnp) || string.IsNullOrWhiteSpace(wbProfile.SenderInfo);

            var headerBtn = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(new TextBlock
            {
                Text = "⚙️  Реквизиты, НДС и правила группировки",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            wbSettingsChevron = new TextBlock
            {
                Text = startExpanded ? "▲ Свернуть" : "▼ Настроить",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#007AFF"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wbSettingsChevron, 1);
            headerGrid.Children.Add(wbSettingsChevron);
            headerBtn.Content = headerGrid;
            settingsRoot.Children.Add(headerBtn);

            wbSettingsBody = new StackPanel
            {
                Visibility = startExpanded ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 12, 0, 0)
            };

            headerBtn.Click += delegate
            {
                bool isVis = wbSettingsBody.Visibility == Visibility.Visible;
                wbSettingsBody.Visibility = isVis ? Visibility.Collapsed : Visibility.Visible;
                wbSettingsChevron.Text = isVis ? "▼ Настроить" : "▲ Свернуть";
            };

            BuildWaybillSettingsBody(wbSettingsBody);
            settingsRoot.Children.Add(wbSettingsBody);
            settingsCard.Child = settingsRoot;
            container.Children.Add(settingsCard);

            // CARD 3: RB BSO & ETN COMPLIANCE MEMO
            container.Children.Add(BuildWaybillMemoCard());

            UpdateModePanelsVisibility();
        }

        private DataGrid CreateEditablePreviewGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = GetBrush("#E5E5EA"),
                Background = Brushes.White,
                BorderBrush = GetBrush("#D1D1D6"),
                BorderThickness = new Thickness(1),
                RowHeaderWidth = 0,
                MaxHeight = 230,
                FontSize = 11.5,
                SelectionMode = DataGridSelectionMode.Single
            };

            var colName = new DataGridTextColumn
            {
                Header = "Наименование (редактир.)",
                Binding = new Binding("Name") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 220
            };
            grid.Columns.Add(colName);

            var colUnit = new DataGridTextColumn
            {
                Header = "Ед.",
                Binding = new Binding("Unit") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = 34
            };
            grid.Columns.Add(colUnit);

            var colQty = new DataGridTextColumn
            {
                Header = "Кол-во",
                Binding = new Binding("Qty") { Converter = new FlexibleIntConverter(), UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = 46
            };
            grid.Columns.Add(colQty);

            var colInPrice = new DataGridTextColumn
            {
                Header = "Цена исх.",
                Binding = new Binding("InputUnitPrice") { Converter = new FlexibleDecimalConverter(), UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = 62
            };
            grid.Columns.Add(colInPrice);

            var colNetPrice = new DataGridTextColumn
            {
                Header = "Цена б/НДС",
                Binding = new Binding("UnitPrice") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 64,
                IsReadOnly = true
            };
            grid.Columns.Add(colNetPrice);

            var colNoVat = new DataGridTextColumn
            {
                Header = "Без НДС",
                Binding = new Binding("TotalNoVat") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 64,
                IsReadOnly = true
            };
            grid.Columns.Add(colNoVat);

            var colVat = new DataGridTextColumn
            {
                Header = "НДС",
                Binding = new Binding("VatAmount") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 50,
                IsReadOnly = true
            };
            grid.Columns.Add(colVat);

            var colWithVat = new DataGridTextColumn
            {
                Header = "Итого с НДС",
                Binding = new Binding("TotalWithVat") { StringFormat = "0.00", ConverterCulture = CultureInfo.InvariantCulture },
                Width = 68,
                IsReadOnly = true
            };
            grid.Columns.Add(colWithVat);

            wbColCargoPlaces = new DataGridTextColumn
            {
                Header = "Мест",
                Binding = new Binding("CargoPlaces") { Converter = new FlexibleNonNegIntConverter(), UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = 44
            };
            grid.Columns.Add(wbColCargoPlaces);

            wbColCargoMass = new DataGridTextColumn
            {
                Header = "Масса (кг)",
                Binding = new Binding("CargoMassKg") { Converter = new FlexibleDecimalConverter(), UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = 66
            };
            grid.Columns.Add(wbColCargoMass);

            var colWarn = new DataGridTextColumn
            {
                Header = "!",
                Binding = new Binding("StatusIcon"),
                Width = 24,
                IsReadOnly = true
            };
            grid.Columns.Add(colWarn);

            UpdatePreviewColumnsVisibility();

            grid.LoadingRow += delegate(object sender, DataGridRowEventArgs e)
            {
                var item = e.Row.Item as GroupedWaybillItem;
                ApplyRowHighlight(e.Row, item);
            };

            grid.CellEditEnding += delegate
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                {
                    if (wbCurrentResult == null) return;
                    SyncFieldsToProfile();
                    WaybillEngine.RecalculateBuildResult(wbCurrentResult, wbProfile);
                    RefreshPreviewVisualsOnly();
                }));
            };

            return grid;
        }

        private void ApplyRowHighlight(DataGridRow row, GroupedWaybillItem item)
        {
            if (row == null || item == null) return;
            if (item.InputUnitPrice <= 0m)
            {
                row.Background = GetBrush("#FEE2E2");
                row.ToolTip = "Нет цены! Введите цену в колонку «Цена исх.» или нажмите «Подставить цены».";
            }
            else if (item.HasRoundingDiff)
            {
                row.Background = GetBrush("#FEF3C7");
                row.ToolTip = item.RoundingNote;
            }
            else if (item.PriceFromCatalog)
            {
                row.Background = GetBrush("#EFF6FF");
                row.ToolTip = "Цена подставлена из локального справочника цен.";
            }
            else
            {
                row.Background = Brushes.White;
                row.ToolTip = null;
            }
        }

        private Border BuildWaybillMemoCard()
        {
            var memoCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var memoRoot = new StackPanel();

            var hdrBtn = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0)
            };
            var hdrGrid = new Grid();
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrGrid.Children.Add(new TextBlock
            {
                Text = "ℹ️  Памятка РБ: БСО, Приложение и ЭТН",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var etnBadge = new Border
            {
                Background = GetBrush("#FEF3C7"),
                BorderBrush = GetBrush("#F59E0B"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(8, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            etnBadge.Child = new TextBlock
            {
                Text = "ЭТН с 01.03.2026",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#92400E")
            };
            Grid.SetColumn(etnBadge, 1);
            hdrGrid.Children.Add(etnBadge);

            wbMemoChevron = new TextBlock
            {
                Text = "▼ Открыть",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#007AFF"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wbMemoChevron, 2);
            hdrGrid.Children.Add(wbMemoChevron);
            hdrBtn.Content = hdrGrid;
            memoRoot.Children.Add(hdrBtn);

            wbMemoBody = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
            hdrBtn.Click += delegate
            {
                bool isVis = wbMemoBody.Visibility == Visibility.Visible;
                wbMemoBody.Visibility = isVis ? Visibility.Collapsed : Visibility.Visible;
                wbMemoChevron.Text = isVis ? "▼ Открыть" : "▲ Скрыть";
            };

            var etnAlertBanner = new Border
            {
                Background = GetBrush("#FEF3C7"),
                BorderBrush = GetBrush("#F59E0B"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(11, 8, 11, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            etnAlertBanner.Child = new TextBlock
            {
                Text = "⚠️ Обязательные ЭТН с 01.03.2026 (Постановления Совмина № 250 и № 528): электронная накладная через аттестованного EDI-провайдера обязательна (бумажные ТН-2 / ТТН-1 / БСО запрещены!) для прослеживаемых товаров по кодам ТН ВЭД ЕАЭС (бытовая техника и электроника: чайники, утюги, фены, пылесосы, мультиварки, дрели, холодильники, шины, велосипеды и др.) и товаров, подлежащих маркировке УКЗ и СИ (одежда, обувь, парфюмерия, часы и др.).",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#92400E"),
                TextWrapping = TextWrapping.Wrap
            };
            wbMemoBody.Children.Add(etnAlertBanner);

            string[] bullets =
            {
                "• БСО («Белбланкавыд»): в РБ оригиналы ТН-2 и ТТН-1 оформляются на типографских бланках строгой отчётности. Режим «Полный бланк А4» предназначен для черновика и согласования; для печати на сам бланк выбирайте режим «Печать данных на бланк БСО».",
                "• Приложение к накладной: если строк больше, чем помещается на одном бланке (для БСО обычно до 20–30 строк, максимум шаблона — 178), выберите режим «Приложение к накладной (реестр А4)» и укажите серию/номер основного БСО.",
                "• Точность НДС в строках: в ТН-2/ТТН-1 и ЭСЧФ произведение «Цена без НДС × Кол-во» обязано строго равняться «Стоимости без НДС». При выделении НДС из розничной цены возможны копеечные отклонения итоговой суммы — такие строки программа подсвечивает жёлтым."
            };
            foreach (var b in bullets)
            {
                wbMemoBody.Children.Add(new TextBlock
                {
                    Text = b,
                    FontSize = 11.5,
                    Foreground = GetBrush("#3A3A3C"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }

            memoRoot.Children.Add(wbMemoBody);
            memoCard.Child = memoRoot;
            return memoCard;
        }

        private void BuildWaybillSettingsBody(StackPanel body)
        {
            // SENDER HEADER + AUTOFILL FROM CERT
            var senderTop = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            senderTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            senderTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            senderTop.Children.Add(CreateFieldLabel("Грузоотправитель (Ваша компания)"));

            var btnFillCert = CreateSmallPillButton("🔑 Подтянуть с ЭЦП", "#EBF3FF", "#007AFF");
            btnFillCert.Click += delegate { AutoFillFromCert(); };
            Grid.SetColumn(btnFillCert, 1);
            senderTop.Children.Add(btnFillCert);
            body.Children.Add(senderTop);

            // Sender UNP + Lookup
            var senderUnpGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            senderUnpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            senderUnpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            senderUnpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wbSenderUnpBox = CreateStyledInput(wbProfile.SenderUnp, "УНП (9 цифр)");
            Grid.SetColumn(wbSenderUnpBox, 0);
            senderUnpGrid.Children.Add(wbSenderUnpBox);

            var btnLookupSender = CreateSmallPillButton("🔎 Найти по УНП в МНС", "#F2F2F7", "#1C1C1E");
            btnLookupSender.Click += delegate { LookupUnpIntoFields(wbSenderUnpBox, wbSenderInfoBox); };
            Grid.SetColumn(btnLookupSender, 2);
            senderUnpGrid.Children.Add(btnLookupSender);
            body.Children.Add(senderUnpGrid);

            wbSenderInfoBox = CreateStyledInput(wbProfile.SenderInfo, "Название и юр. адрес (напр.: ООО \"...\", г. Минск, ...)");
            wbSenderInfoBox.TextWrapping = TextWrapping.Wrap;
            wbSenderInfoBox.Margin = new Thickness(0, 0, 0, 6);
            body.Children.Add(wbSenderInfoBox);

            body.Children.Add(CreateFieldLabel("Отпуск разрешил / сдал (должность и ФИО)"));
            wbSignerBox = CreateStyledInput(wbProfile.Signer, "Напр.: Директор Иванов Иван Иванович");
            wbSignerBox.Margin = new Thickness(0, 3, 0, 10);
            body.Children.Add(wbSignerBox);

            // RECEIVER HEADER + PRESETS
            var recTop = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            recTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            recTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            recTop.Children.Add(CreateFieldLabel("Грузополучатель"));

            var btnWbPreset = CreateSmallPillButton("WB • ИМВБРБ", "#F3E8FF", "#7C3AED");
            btnWbPreset.Click += delegate
            {
                WaybillEngine.ApplyWbPreset(wbProfile);
                wbReceiverUnpBox.Text = wbProfile.ReceiverUnp;
                wbReceiverInfoBox.Text = wbProfile.ReceiverInfo;
                wbBasisBox.Text = wbProfile.BasisPrefix;
                ShowToast("✓ Подставлены реквизиты WB (ООО «ИМВБРБ»)");
            };
            Grid.SetColumn(btnWbPreset, 1);
            recTop.Children.Add(btnWbPreset);
            body.Children.Add(recTop);

            var recUnpGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            recUnpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            recUnpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            recUnpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wbReceiverUnpBox = CreateStyledInput(wbProfile.ReceiverUnp, "УНП получателя");
            Grid.SetColumn(wbReceiverUnpBox, 0);
            recUnpGrid.Children.Add(wbReceiverUnpBox);

            var btnLookupRec = CreateSmallPillButton("🔎 Найти по УНП в МНС", "#F2F2F7", "#1C1C1E");
            btnLookupRec.Click += delegate { LookupUnpIntoFields(wbReceiverUnpBox, wbReceiverInfoBox); };
            Grid.SetColumn(btnLookupRec, 2);
            recUnpGrid.Children.Add(btnLookupRec);
            body.Children.Add(recUnpGrid);

            wbReceiverInfoBox = CreateStyledInput(wbProfile.ReceiverInfo, "Наименование и адрес грузополучателя");
            wbReceiverInfoBox.TextWrapping = TextWrapping.Wrap;
            wbReceiverInfoBox.Margin = new Thickness(0, 0, 0, 6);
            body.Children.Add(wbReceiverInfoBox);

            body.Children.Add(CreateFieldLabel("Основание отпуска (договор/оферта до номера поставки)"));
            wbBasisBox = CreateStyledInput(wbProfile.BasisPrefix, "оферта, акц. 01.02.2023");
            wbBasisBox.Margin = new Thickness(0, 3, 0, 10);
            body.Children.Add(wbBasisBox);

            // VAT + PRICE INPUT MODE + SORT (adaptive WrapPanel so "Имена Я → А" never clips at 860 px)
            var vatGrid = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            var vatPair = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 6) };
            var vatLbl = CreateFieldLabel("Ставка НДС:");
            vatLbl.VerticalAlignment = VerticalAlignment.Center;
            vatLbl.Margin = new Thickness(0, 0, 6, 0);
            vatPair.Children.Add(vatLbl);

            wbVatCombo = new ComboBox { Width = 96, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            wbVatCombo.Items.Add("Без НДС");
            wbVatCombo.Items.Add("20%");
            wbVatCombo.Items.Add("10%");
            wbVatCombo.Items.Add("0%");
            wbVatCombo.SelectedItem = wbVatCombo.Items.Contains(wbProfile.VatMode) ? wbProfile.VatMode : "Без НДС";
            wbVatCombo.SelectionChanged += delegate
            {
                if (_suppressWaybillEvents) return;
                RecalculateExistingPreviewPreserveEdits();
            };
            vatPair.Children.Add(wbVatCombo);
            vatGrid.Children.Add(vatPair);

            var modePair = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 6) };
            var modeLbl = CreateFieldLabel("Цена в файле:");
            modeLbl.VerticalAlignment = VerticalAlignment.Center;
            modeLbl.Margin = new Thickness(0, 0, 6, 0);
            modePair.Children.Add(modeLbl);

            wbPriceInputModeCombo = new ComboBox { Width = 175, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
            wbPriceInputModeCombo.Items.Add(new ComboBoxItem { Content = "С НДС (выделить)", Tag = WaybillEngine.PriceModeExtractVat });
            wbPriceInputModeCombo.Items.Add(new ComboBoxItem { Content = "Без НДС (начислить)", Tag = WaybillEngine.PriceModeAddVat });
            SelectComboByTag(wbPriceInputModeCombo, wbProfile.PriceInputMode, 0);
            wbPriceInputModeCombo.SelectionChanged += delegate
            {
                if (_suppressWaybillEvents) return;
                RecalculateExistingPreviewPreserveEdits();
            };
            modePair.Children.Add(wbPriceInputModeCombo);
            vatGrid.Children.Add(modePair);

            wbSortDescCheck = new CheckBox
            {
                Content = "Имена Я → А",
                IsChecked = wbProfile.SortDescendingByName,
                FontSize = 11.5,
                Foreground = GetBrush("#3A3A3C"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = "Сортировка Я -> А, затем по возрастанию цены"
            };
            wbSortDescCheck.Click += delegate { RecomputePreviewIfLoaded(); };
            vatGrid.Children.Add(wbSortDescCheck);
            body.Children.Add(vatGrid);

            // NORMALIZATION RULES (PRO mode only)
            wbRulesProSection = new StackPanel();
            wbUseRulesCheck = new CheckBox
            {
                Content = "Группировать названия по правилам (если снять — будут оригиналы из Excel)",
                IsChecked = wbProfile.UseNormalizationRules,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 2, 0, 4)
            };
            wbUseRulesCheck.Click += delegate
            {
                UpdateWaybillNamesStatusBadge();
                RecomputePreviewIfLoaded();
            };
            wbRulesProSection.Children.Add(wbUseRulesCheck);

            wbRulesBox = new TextBox
            {
                Text = wbProfile.RulesText ?? "",
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 58,
                FontSize = 11.5,
                Padding = new Thickness(8, 6, 8, 6),
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#D1D1D6"),
                BorderThickness = new Thickness(1),
                ToolTip = "Пример строки: ручк; pen => Ручка шариковая (строки с # игнорируются)"
            };
            wbRulesBox.TextChanged += delegate { UpdateWaybillNamesStatusBadge(); };
            wbRulesProSection.Children.Add(wbRulesBox);
            wbRulesProSection.Children.Add(new TextBlock
            {
                Text = "Пример правила: ручк; pen => Ручка шариковая   |   Комментарий: # текст",
                FontSize = 10.5,
                Foreground = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 3, 0, 10)
            });
            body.Children.Add(wbRulesProSection);

            UpdateWaybillNamesStatusBadge();

            // OUTPUT FOLDER
            body.Children.Add(CreateFieldLabel("Папка для готовых накладных (.xlsx)"));
            var outGrid = new Grid { Margin = new Thickness(0, 3, 0, 10) };
            outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wbOutputFolderBox = CreateStyledInput(wbProfile.OutputFolder, "Путь к папке");
            Grid.SetColumn(wbOutputFolderBox, 0);
            outGrid.Children.Add(wbOutputFolderBox);

            var btnOpenDir = CreateSmallPillButton("📂 Открыть", "#F2F2F7", "#1C1C1E");
            btnOpenDir.Click += delegate
            {
                try
                {
                    string dir = (wbOutputFolderBox.Text ?? "").Trim();
                    if (string.IsNullOrEmpty(dir))
                        dir = new WaybillProfile().OutputFolder;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    WaybillEngine.OpenFileWithShell(dir);
                }
                catch (Exception ex)
                {
                    ShowToast("⚠️ Не удалось открыть папку: " + ex.Message);
                }
            };
            Grid.SetColumn(btnOpenDir, 2);
            outGrid.Children.Add(btnOpenDir);
            body.Children.Add(outGrid);

            var btnSaveProf = CreateSolidActionButton("✓  Сохранить настройки и обновить расчёт", "#1C1C1E", Brushes.White);
            btnSaveProf.Height = 34;
            btnSaveProf.Click += delegate
            {
                SyncFieldsToProfile();
                SaveWaybillProfileEverywhere();
                UpdateWaybillNamesStatusBadge();
                RecalculateExistingPreviewPreserveEdits();
                ShowToast("✓ Реквизиты и настройки накладных сохранены");
            };
            body.Children.Add(btnSaveProf);
        }

        private void RefreshWaybillUiMode(bool isPro)
        {
            UpdateModePanelsVisibility();
        }

        private void UpdateModePanelsVisibility()
        {
            if (wbProfile == null) return;
            bool isPro = IsProUiMode();
            if (wbBsoMarginsPanel != null)
            {
                wbBsoMarginsPanel.Visibility = string.Equals(wbProfile.PrintMode, WaybillEngine.PrintModeBso, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            if (wbAppendixMetaPanel != null)
            {
                wbAppendixMetaPanel.Visibility = string.Equals(wbProfile.PrintMode, WaybillEngine.PrintModeAppendix, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            if (wbTtn1FieldsPanel != null)
            {
                wbTtn1FieldsPanel.Visibility = string.Equals(wbProfile.DocType, WaybillEngine.DocTypeTtn1, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            if (wbRulesProSection != null)
            {
                wbRulesProSection.Visibility = isPro ? Visibility.Visible : Visibility.Collapsed;
            }
            if (wbProModeHint != null)
            {
                wbProModeHint.Visibility = isPro ? Visibility.Collapsed : Visibility.Visible;
            }
            UpdatePreviewColumnsVisibility();
            UpdateWaybillNamesStatusBadge();
            if (wbCurrentResult != null)
            {
                RefreshPreviewVisualsOnly();
            }
        }

        private void UpdatePreviewColumnsVisibility()
        {
            bool isTtn1 = wbProfile != null && string.Equals(wbProfile.DocType, WaybillEngine.DocTypeTtn1, StringComparison.OrdinalIgnoreCase);
            var vis = isTtn1 ? Visibility.Visible : Visibility.Collapsed;
            if (wbColCargoPlaces != null) wbColCargoPlaces.Visibility = vis;
            if (wbColCargoMass != null) wbColCargoMass.Visibility = vis;
        }

        private static void SelectComboByTag(ComboBox combo, string tagValue, int fallbackIndex)
        {
            if (combo == null || combo.Items.Count == 0) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(item.Tag as string, tagValue, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (fallbackIndex >= 0 && fallbackIndex < combo.Items.Count)
                combo.SelectedIndex = fallbackIndex;
        }

        private static string GetSelectedComboTag(ComboBox combo, string fallback)
        {
            if (combo == null) return fallback;
            var item = combo.SelectedItem as ComboBoxItem;
            return (item != null && item.Tag != null) ? item.Tag.ToString() : fallback;
        }

        private void UpdateWaybillNamesStatusBadge()
        {
            if (wbNamesStatusBorder == null || wbNamesStatusText == null || wbUseRulesCheck == null || wbRulesBox == null) return;
            bool isPro = IsProUiMode();
            bool useRules = wbUseRulesCheck.IsChecked == true;
            int activeCount = WaybillEngine.ParseRules(wbRulesBox.Text ?? "").Count;

            if (!isPro && !useRules)
            {
                wbNamesStatusBorder.Visibility = Visibility.Collapsed;
                return;
            }

            wbNamesStatusBorder.Visibility = Visibility.Visible;
            if (useRules)
            {
                wbNamesStatusText.Text = string.Format("Названия: По правилам группировки (активно {0})", activeCount);
                wbNamesStatusBorder.Background = GetBrush("#FFFBEB");
                wbNamesStatusBorder.BorderBrush = GetBrush("#FDE68A");
                wbNamesStatusText.Foreground = GetBrush("#B45309");
            }
            else
            {
                wbNamesStatusText.Text = "Названия: Оригинальные из файла";
                wbNamesStatusBorder.Background = GetBrush("#ECFDF5");
                wbNamesStatusBorder.BorderBrush = GetBrush("#A7F3D0");
                wbNamesStatusText.Foreground = GetBrush("#15803D");
            }
        }

        private void SaveWaybillProfileEverywhere()
        {
            if (wbProfile == null) return;
            WaybillEngine.SaveProfile(wbProfile);
            var act = ActiveCompanyProfile;
            if (act != null)
            {
                act.Unp = wbProfile.SenderUnp ?? "";
                act.OrgInfo = wbProfile.SenderInfo ?? "";
                act.Signer = wbProfile.Signer ?? "";
                act.WaybillProfile = wbProfile;
                SaveActiveCompanyStore();
                RefreshSidebarProfileCombo();
                RefreshOverviewProfileHeader();
            }
        }

        private void RefreshWaybillTabFromActiveProfile()
        {
            var act = ActiveCompanyProfile;
            if (act == null) return;
            act.SyncToWaybillProfile();
            wbProfile = act.WaybillProfile;
            WaybillEngine.NormalizeProfileDefaults(wbProfile);
            if (wbSenderUnpBox == null) return;

            _suppressWaybillEvents = true;
            try
            {
                wbSenderUnpBox.Text = wbProfile.SenderUnp ?? "";
                wbSenderInfoBox.Text = wbProfile.SenderInfo ?? "";
                wbSignerBox.Text = wbProfile.Signer ?? "";
                wbReceiverUnpBox.Text = wbProfile.ReceiverUnp ?? "";
                wbReceiverInfoBox.Text = wbProfile.ReceiverInfo ?? "";
                wbBasisBox.Text = wbProfile.BasisPrefix ?? "";
                wbVatCombo.SelectedItem = wbVatCombo.Items.Contains(wbProfile.VatMode) ? wbProfile.VatMode : "Без НДС";
                SelectComboByTag(wbPriceInputModeCombo, wbProfile.PriceInputMode, 0);
                SelectComboByTag(wbDocTypeCombo, wbProfile.DocType, 0);
                SelectComboByTag(wbPrintModeCombo, wbProfile.PrintMode, 0);
                wbTopMarginBox.Text = wbProfile.TopMarginMm.ToString("0.#", CultureInfo.InvariantCulture);
                wbLeftMarginBox.Text = wbProfile.LeftMarginMm.ToString("0.#", CultureInfo.InvariantCulture);
                wbBsoSeriesBox.Text = wbProfile.BsoSeries ?? "";
                wbBsoNumberBox.Text = wbProfile.BsoNumber ?? "";
                wbVehicleBox.Text = wbProfile.VehicleInfo ?? "";
                wbDriverBox.Text = wbProfile.DriverInfo ?? "";
                wbLoadingPointBox.Text = wbProfile.LoadingPoint ?? "";
                wbUnloadingPointBox.Text = wbProfile.UnloadingPoint ?? "";
                if (wbTransportCustomerBox != null) wbTransportCustomerBox.Text = wbProfile.TransportCustomer ?? "";

                wbSortDescCheck.IsChecked = wbProfile.SortDescendingByName;
                wbUseRulesCheck.IsChecked = wbProfile.UseNormalizationRules;
                wbRulesBox.Text = wbProfile.RulesText ?? "";
                wbOutputFolderBox.Text = wbProfile.OutputFolder ?? "";
                wbPickingListCheck.IsChecked = wbProfile.CreatePickingList;
            }
            finally
            {
                _suppressWaybillEvents = false;
            }

            UpdateModePanelsVisibility();

            if (wbSettingsBody != null && wbSettingsChevron != null)
            {
                bool needExpand = string.IsNullOrWhiteSpace(wbProfile.SenderUnp) || string.IsNullOrWhiteSpace(wbProfile.SenderInfo);
                if (needExpand)
                {
                    wbSettingsBody.Visibility = Visibility.Visible;
                    wbSettingsChevron.Text = "▲ Свернуть";
                }
            }

            UpdateWaybillNamesStatusBadge();
            RecalculateExistingPreviewPreserveEdits();
        }

        private void SyncFieldsToProfile()
        {
            if (wbProfile == null) return;
            wbProfile.DocType = GetSelectedComboTag(wbDocTypeCombo, WaybillEngine.DocTypeTn2);
            wbProfile.PrintMode = GetSelectedComboTag(wbPrintModeCombo, WaybillEngine.PrintModeFull);
            wbProfile.TopMarginMm = ParseMarginMm(wbTopMarginBox != null ? wbTopMarginBox.Text : "10", 10.0);
            wbProfile.LeftMarginMm = ParseMarginMm(wbLeftMarginBox != null ? wbLeftMarginBox.Text : "15", 15.0);
            wbProfile.BsoSeries = wbBsoSeriesBox != null ? (wbBsoSeriesBox.Text ?? "").Trim() : "";
            wbProfile.BsoNumber = wbBsoNumberBox != null ? (wbBsoNumberBox.Text ?? "").Trim() : "";
            wbProfile.VehicleInfo = wbVehicleBox != null ? (wbVehicleBox.Text ?? "").Trim() : "";
            wbProfile.DriverInfo = wbDriverBox != null ? (wbDriverBox.Text ?? "").Trim() : "";
            wbProfile.LoadingPoint = wbLoadingPointBox != null ? (wbLoadingPointBox.Text ?? "").Trim() : "";
            wbProfile.UnloadingPoint = wbUnloadingPointBox != null ? (wbUnloadingPointBox.Text ?? "").Trim() : "";
            wbProfile.TransportCustomer = wbTransportCustomerBox != null ? (wbTransportCustomerBox.Text ?? "").Trim() : "";

            wbProfile.SenderUnp = (wbSenderUnpBox.Text ?? "").Trim();
            wbProfile.SenderInfo = (wbSenderInfoBox.Text ?? "").Trim();
            wbProfile.Signer = (wbSignerBox.Text ?? "").Trim();
            wbProfile.ReceiverUnp = (wbReceiverUnpBox.Text ?? "").Trim();
            wbProfile.ReceiverInfo = (wbReceiverInfoBox.Text ?? "").Trim();
            wbProfile.BasisPrefix = (wbBasisBox.Text ?? "").Trim();
            wbProfile.VatMode = wbVatCombo.SelectedItem != null ? wbVatCombo.SelectedItem.ToString() : "Без НДС";
            wbProfile.PriceInputMode = GetSelectedComboTag(wbPriceInputModeCombo, WaybillEngine.PriceModeExtractVat);
            wbProfile.SortDescendingByName = wbSortDescCheck.IsChecked == true;
            wbProfile.UseNormalizationRules = wbUseRulesCheck.IsChecked == true;
            wbProfile.RulesText = wbRulesBox.Text ?? "";
            wbProfile.OutputFolder = (wbOutputFolderBox.Text ?? "").Trim();
            wbProfile.CreatePickingList = wbPickingListCheck.IsChecked == true;
        }

        private static double ParseMarginMm(string raw, double fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            string s = raw.Trim().Replace(',', '.');
            double val;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
            {
                if (val < 0.0) return 0.0;
                if (val > 80.0) return 80.0;
                return val;
            }
            return fallback;
        }

        private void AutoFillFromCert()
        {
            ShowToast("🔑 Поиск сертификата ЭЦП и запрос в МНС...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string unp, org, signer;
                bool ok = WaybillEngine.TryExtractFromCertStore(out unp, out org, out signer);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!ok)
                    {
                        ShowToast("⚠️ Действующий ключ ЭЦП не найден — введите УНП и нажмите «Найти по УНП»");
                        return;
                    }
                    if (!string.IsNullOrEmpty(unp)) wbSenderUnpBox.Text = unp;
                    if (!string.IsNullOrEmpty(org)) wbSenderInfoBox.Text = org;
                    if (!string.IsNullOrEmpty(signer)) wbSignerBox.Text = signer;
                    SyncFieldsToProfile();
                    SaveWaybillProfileEverywhere();
                    ShowToast("✓ Реквизиты подтянуты из ЭЦП и базы МНС!");
                }));
            });
        }

        private void LookupUnpIntoFields(TextBox unpBox, TextBox infoBox)
        {
            string raw = (unpBox.Text ?? "").Trim();
            ShowToast("🔎 Запрос в реестр МНС по УНП " + raw + "...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                var res = UnpChecker.Lookup(raw);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (res == null || !res.Success)
                    {
                        ShowToast("⚠️ " + (res != null ? res.ErrorMessage : "Ошибка запроса"));
                        return;
                    }
                    unpBox.Text = res.Unp;
                    string name = !string.IsNullOrEmpty(res.ShortName) ? res.ShortName : res.FullName;
                    infoBox.Text = string.IsNullOrEmpty(res.Address) ? name : (name + ", " + res.Address);
                    ShowToast("✓ Заполнено из МНС: " + name);
                }));
            });
        }

        private void OnWaybillDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                wbDropZone.Background = GetBrush("#DBEAFE");
                wbDropZone.BorderBrush = GetBrush("#007AFF");
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ResetDropZoneVisual(bool hasFiles)
        {
            wbDropZone.Background = hasFiles ? GetBrush("#ECFDF5") : GetBrush("#F0F7FF");
            wbDropZone.BorderBrush = hasFiles ? GetBrush("#34C759") : GetBrush("#93C5FD");
        }

        private void OnWaybillDrop(object sender, DragEventArgs e)
        {
            ResetDropZoneVisual(wbLoadedFiles.Count > 0);
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped == null || dropped.Length == 0) return;

            var valid = dropped.Where(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)).ToList();
            if (valid.Count == 0)
            {
                ShowToast("⚠️ Нужен файл формата .xlsx (выгрузка поставки WB FBS/FBO)");
                return;
            }
            _wbIsDemoLoaded = false;
            LoadSupplyFiles(valid, null);
        }

        private void SelectSupplyFilesDialog()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите файлы поставки Excel (.xlsx)",
                Filter = "Файлы Excel (*.xlsx)|*.xlsx",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                _wbIsDemoLoaded = false;
                LoadSupplyFiles(dlg.FileNames, null);
            }
        }

        private void LoadDemoSupplyFile()
        {
            try
            {
                string demoDir = Path.Combine(Path.GetTempPath(), "Buhly_Demo");
                string demoPath = Path.Combine(demoDir, "wb-gi-demo-9900123.xlsx");
                WaybillEngine.GenerateDemoSupplyFile(demoPath);
                _wbIsDemoLoaded = true;
                LoadSupplyFiles(new string[] { demoPath }, "⚡ Загружена демо-поставка (5 позиций) — можно редактировать таблицу и создать ТН-2");
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Не удалось создать демо-файл: " + ex.Message);
            }
        }

        private void ClearSupplyFiles()
        {
            _wbLoadSeq++;
            _wbIsDemoLoaded = false;
            wbLoadedFiles.Clear();
            wbCurrentResult = null;
            wbPreviewGrid.ItemsSource = null;
            wbPreviewCard.Visibility = Visibility.Collapsed;
            if (wbLoadingBanner != null && !_wbIsGenerating)
                wbLoadingBanner.Visibility = Visibility.Collapsed;
            wbDropTitle.Text = "📥  Перетащите файлы wb-gi-*.xlsx или отгрузку WB FBO сюда";
            wbDropTitle.Foreground = GetBrush("#007AFF");
            wbDropSubtitle.Text = "или нажмите здесь, чтобы выбрать файлы .xlsx на компьютере";
            ResetDropZoneVisual(false);
            UpdateExportButtonsState();
        }

        private void LoadSupplyFiles(IEnumerable<string> paths, string completionToast)
        {
            wbLoadedFiles = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            RecomputePreviewIfLoaded(completionToast);
        }

        private void RecomputePreviewIfLoaded()
        {
            RecomputePreviewIfLoaded(null);
        }

        private void RecomputePreviewIfLoaded(string completionToast)
        {
            if (wbLoadedFiles.Count == 0) return;
            SyncFieldsToProfile();

            int seq = ++_wbLoadSeq;
            var filesCopy = new List<string>(wbLoadedFiles);
            var profileCopy = CloneWaybillProfile(wbProfile);
            bool isDemoRun = _wbIsDemoLoaded;

            if (wbLoadingBanner != null)
            {
                wbLoadingText.Text = string.Format("⏳ Чтение и группировка файлов поставки ({0} шт.)...", filesCopy.Count);
                wbLoadingBanner.Visibility = Visibility.Visible;
            }
            UpdateExportButtonsState();

            ThreadPool.QueueUserWorkItem(delegate
            {
                WaybillBuildResult parsed = null;
                Exception error = null;
                try
                {
                    parsed = WaybillEngine.ParseSupplyExcelFiles(filesCopy, profileCopy);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (seq != _wbLoadSeq) return;
                    if (wbLoadingBanner != null && !_wbIsGenerating)
                        wbLoadingBanner.Visibility = Visibility.Collapsed;
                    UpdateExportButtonsState();

                    if (error != null)
                    {
                        wbCurrentResult = null;
                        wbPreviewGrid.ItemsSource = null;
                        wbPreviewCard.Visibility = Visibility.Collapsed;
                        ShowToast("⚠️ " + error.Message);
                        return;
                    }

                    wbCurrentResult = parsed;
                    ResetDropZoneVisual(true);
                    string supplies = wbCurrentResult.SupplyIds.Count > 0 ? string.Join(", ", wbCurrentResult.SupplyIds) : "из файла";
                    string fboTag = wbCurrentResult.IsFboDetected ? "  [WB FBO]" : "";
                    string demoPrefix = isDemoRun ? "[ДЕМО] " : "";
                    wbDropTitle.Text = "✓  " + demoPrefix + "Загружено файлов: " + filesCopy.Count + "  (" + supplies + ")" + fboTag;
                    wbDropTitle.Foreground = GetBrush("#16A34A");
                    wbDropSubtitle.Text = string.Join(", ", filesCopy.Select(f => Path.GetFileName(f)));

                    wbPreviewGrid.ItemsSource = wbCurrentResult.GroupedItems;
                    RefreshPreviewVisualsOnly();
                    wbPreviewCard.Visibility = Visibility.Visible;
                    UpdateExportButtonsState();

                    if (!string.IsNullOrEmpty(completionToast))
                        ShowToast(completionToast);
                }));
            });
        }

        private void UpdateExportButtonsState()
        {
            bool busy = _wbIsGenerating || (wbLoadingBanner != null && wbLoadingBanner.Visibility == Visibility.Visible);
            if (wbBtnSaveOpen != null) wbBtnSaveOpen.IsEnabled = !busy;
            if (wbBtnPrintNow != null) wbBtnPrintNow.IsEnabled = !busy;
        }

        private void RecalculateExistingPreviewPreserveEdits()
        {
            if (wbCurrentResult == null) return;
            SyncFieldsToProfile();
            WaybillEngine.RecalculateBuildResult(wbCurrentResult, wbProfile);
            RefreshPreviewVisualsOnly();
        }

        private void RefreshPreviewVisualsOnly()
        {
            if (wbCurrentResult == null) return;

            bool isTtn1 = wbProfile != null && string.Equals(wbProfile.DocType, WaybillEngine.DocTypeTtn1, StringComparison.OrdinalIgnoreCase);
            bool isBso = wbProfile != null && string.Equals(wbProfile.PrintMode, WaybillEngine.PrintModeBso, StringComparison.OrdinalIgnoreCase);
            bool isAppendix = wbProfile != null && string.Equals(wbProfile.PrintMode, WaybillEngine.PrintModeAppendix, StringComparison.OrdinalIgnoreCase);

            string cargoSummary = isTtn1
                ? string.Format(CultureInfo.InvariantCulture, "  •  Мест: {0}  •  Масса: {1:0.00} кг", wbCurrentResult.TotalCargoPlaces, wbCurrentResult.TotalCargoMassKg)
                : "";

            wbSummaryText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Строк: {0}  •  Товаров: {1} шт  •  Без НДС: {2:0.00}  •  НДС: {3:0.00}  •  Итого: {4:0.00} BYN{5}",
                wbCurrentResult.GroupedItems.Count,
                wbCurrentResult.TotalQty,
                wbCurrentResult.TotalNoVat,
                wbCurrentResult.TotalVat,
                wbCurrentResult.TotalWithVat,
                cargoSummary);

            wbPreviewGrid.Items.Refresh();
            for (int i = 0; i < wbPreviewGrid.Items.Count; i++)
            {
                var row = wbPreviewGrid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                if (row != null)
                    ApplyRowHighlight(row, wbPreviewGrid.Items[i] as GroupedWaybillItem);
            }

            var notes = new List<string>();
            if (wbCurrentResult.CatalogFilledCount > 0)
            {
                notes.Add(string.Format("📖 Автоподставлено цен из справочника: {0} поз. (синий фон).", wbCurrentResult.CatalogFilledCount));
            }
            if (wbCurrentResult.MissingPriceCount > 0)
            {
                notes.Add(string.Format("❗ У {0} поз. отсутствует цена (красный фон) — кликните по ячейке «Цена исх.» и введите цену, либо подставьте из справочника.", wbCurrentResult.MissingPriceCount));
            }
            if (wbCurrentResult.RoundingWarningCount > 0)
            {
                notes.Add(string.Format("⚠️ В {0} строках возникла копеечная разница округления НДС (жёлтый фон). Равенство «Цена б/НДС × Кол-во = Без НДС» сохранено строго.", wbCurrentResult.RoundingWarningCount));
            }
            if (wbCurrentResult.Warnings != null && wbCurrentResult.Warnings.Count > 0)
            {
                foreach (var w in wbCurrentResult.Warnings)
                {
                    if (!string.IsNullOrWhiteSpace(w))
                        notes.Add("⚠️ " + w.Trim());
                }
            }
            var traceableHits = WaybillEngine.DetectTraceableKeywords(wbCurrentResult.GroupedItems);
            if (traceableHits.Count > 0)
            {
                string sample = string.Join(", ", traceableHits.Take(3));
                string tail = traceableHits.Count > 3 ? string.Format(" и ещё {0}", traceableHits.Count - 3) : "";
                notes.Add(string.Format("🔔 Возможен обязательный ЭТН (Пост. № 250 / № 528): {0}{1}. Проверьте необходимость электронной накладной через EDI-провайдера.", sample, tail));
            }
            if (isBso && wbCurrentResult.GroupedItems.Count > 30 && wbCurrentResult.GroupedItems.Count <= 178)
            {
                notes.Add(string.Format("📄 В режиме БСО строк ({0}) больше 30 — проверьте, помещаются ли все позиции на ваш типографский бланк БСО (иначе используйте «Приложение к накладной»).", wbCurrentResult.GroupedItems.Count));
            }
            if (wbCurrentResult.GroupedItems.Count > 178 && !isAppendix)
            {
                notes.Add(string.Format("📄 Строк ({0}) больше 178 — переключите режим печати на «Приложение к накладной (реестр А4)».", wbCurrentResult.GroupedItems.Count));
            }

            if (notes.Count > 0)
            {
                wbPreviewAlertText.Text = string.Join("\n", notes);
                wbPreviewAlertBanner.Visibility = Visibility.Visible;
            }
            else
            {
                wbPreviewAlertBanner.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyCatalogToCurrentPreview()
        {
            if (wbCurrentResult == null || wbCurrentResult.GroupedItems.Count == 0 || _wbIsGenerating) return;
            wbPreviewGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            wbPreviewGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int filled = WaybillEngine.ApplyCatalogPrices(wbCurrentResult);
            wbCurrentResult.CatalogFilledCount += filled;
            SyncFieldsToProfile();
            WaybillEngine.RecalculateBuildResult(wbCurrentResult, wbProfile);
            RefreshPreviewVisualsOnly();
            ShowToast(filled > 0
                ? string.Format("✓ Подставлено цен из справочника: {0}", filled)
                : "ℹ️ Подходящих цен в справочнике не найдено — введите цены в таблицу и нажмите «Запомнить цены»");
        }

        private void RememberCurrentPrices()
        {
            if (wbCurrentResult == null || wbCurrentResult.GroupedItems.Count == 0 || _wbIsGenerating) return;
            wbPreviewGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            wbPreviewGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int saved = WaybillEngine.RememberPricesInCatalog(wbCurrentResult.GroupedItems);
            ShowToast(saved > 0
                ? "✓ Цены сохранены в локальный справочник для будущих FBO-поставок"
                : "⚠️ Нет строк с ценой больше 0 для сохранения");
        }

        private void RunWaybillExport(bool sendToPrinter)
        {
            if (_wbIsGenerating) return;
            if (wbLoadedFiles.Count == 0 || wbCurrentResult == null || wbCurrentResult.GroupedItems.Count == 0)
            {
                ShowToast("⚠️ Сначала перетащите или выберите файл поставки .xlsx");
                return;
            }

            try
            {
                wbPreviewGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                wbPreviewGrid.CommitEdit(DataGridEditingUnit.Row, true);

                SyncFieldsToProfile();
                SaveWaybillProfileEverywhere();
                WaybillEngine.RecalculateBuildResult(wbCurrentResult, wbProfile);
                RefreshPreviewVisualsOnly();

                var exportProfile = CloneWaybillProfile(wbProfile);
                bool isDemo = _wbIsDemoLoaded || (wbLoadedFiles.Count == 1 &&
                    string.Equals(Path.GetFileName(wbLoadedFiles[0]), "wb-gi-demo-9900123.xlsx", StringComparison.OrdinalIgnoreCase));

                if (isDemo && (string.IsNullOrWhiteSpace(exportProfile.SenderUnp) || string.IsNullOrWhiteSpace(exportProfile.SenderInfo)))
                {
                    if (string.IsNullOrWhiteSpace(exportProfile.SenderUnp))
                        exportProfile.SenderUnp = "190000000";
                    if (string.IsNullOrWhiteSpace(exportProfile.SenderInfo))
                        exportProfile.SenderInfo = "ООО «БелДемоМаркет» (ДЕМО), г. Минск, ул. Демонстрационная, 1";
                    if (string.IsNullOrWhiteSpace(exportProfile.Signer))
                        exportProfile.Signer = "Директор Демо И.И.";
                }

                if (string.IsNullOrWhiteSpace(exportProfile.SenderUnp) || string.IsNullOrWhiteSpace(exportProfile.SenderInfo))
                {
                    wbSettingsBody.Visibility = Visibility.Visible;
                    wbSettingsChevron.Text = "▲ Свернуть";
                    ShowToast("⚠️ Заполните УНП и наименование Вашей организации (или нажмите «Подтянуть с ЭЦП»)");
                    return;
                }

                bool isTtn1 = string.Equals(exportProfile.DocType, WaybillEngine.DocTypeTtn1, StringComparison.OrdinalIgnoreCase);
                bool isAppendix = string.Equals(exportProfile.PrintMode, WaybillEngine.PrintModeAppendix, StringComparison.OrdinalIgnoreCase);
                bool isBso = string.Equals(exportProfile.PrintMode, WaybillEngine.PrintModeBso, StringComparison.OrdinalIgnoreCase);

                var traceableHits = WaybillEngine.DetectTraceableKeywords(wbCurrentResult.GroupedItems);
                if (traceableHits.Count > 0)
                {
                    int showCount = Math.Min(6, traceableHits.Count);
                    string listText = "• " + string.Join("\n• ", traceableHits.Take(showCount));
                    if (traceableHits.Count > showCount)
                        listText += string.Format("\n• ... и ещё {0} поз.", traceableHits.Count - showCount);

                    string warnMsg =
                        "Обнаружены позиции, похожие на прослеживаемые или подлежащие маркировке товары (Постановления Совмина РБ № 250 / № 528):\n\n" +
                        listText + "\n\n" +
                        "Для таких товаров с 01.03.2026 в РБ может быть обязательна электронная накладная (ЭТН) через EDI-провайдера, а бумажные ТН-2/ТТН-1/БСО не допускаются.\n\n" +
                        "Создать бумажный документ в Excel всё равно?";

                    if (!ConfirmWaybillWarningDialog("Внимание: возможен обязательный ЭТН", warnMsg, "Всё равно создать", "Отмена"))
                        return;
                }

                if (isBso && wbCurrentResult.GroupedItems.Count > 30)
                {
                    string bsoMsg = string.Format(
                        "В накладной {0} поз., а на одном типографском бланке БСО обычно помещается до 20–30 строк.\n\n" +
                        "Чтобы не испортить бланк БСО, рекомендуется переключить режим печати на «Приложение к накладной (реестр А4)».\n\n" +
                        "Всё равно сформировать файл для печати на БСО?",
                        wbCurrentResult.GroupedItems.Count);
                    if (!ConfirmWaybillWarningDialog("Больше 30 строк для БСО", bsoMsg, "Всё равно создать", "Отмена"))
                        return;
                }

                DateTime docDate = wbDatePicker.SelectedDate ?? DateTime.Today;
                string folder = exportProfile.OutputFolder;
                if (string.IsNullOrWhiteSpace(folder))
                    folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Buhly_ТН");

                string dateStr = docDate.ToString("dd.MM.yyyy");
                string safeSupply = wbCurrentResult.SupplyIds.Count > 0
                    ? string.Join("_", wbCurrentResult.SupplyIds.Take(2)).Replace(":", "").Replace("/", "_").Replace("\\", "_")
                    : "";

                string basePrefix = isAppendix
                    ? ("Приложение_" + (isTtn1 ? "ТТН1_" : "ТН2_"))
                    : (isTtn1 ? "ТТН1_" : (isBso ? "БСО_" : ""));

                string outFile = Path.Combine(folder, basePrefix + dateStr + ".xlsx");
                if (File.Exists(outFile) && !string.IsNullOrEmpty(safeSupply))
                {
                    outFile = Path.Combine(folder, basePrefix + dateStr + "_" + safeSupply + ".xlsx");
                }

                string pickFile = null;
                if (exportProfile.CreatePickingList)
                {
                    pickFile = Path.Combine(folder, "Лист_сборки_" + dateStr + ".xlsx");
                    if (File.Exists(pickFile) && !string.IsNullOrEmpty(safeSupply))
                    {
                        pickFile = Path.Combine(folder, "Лист_сборки_" + dateStr + "_" + safeSupply + ".xlsx");
                    }
                }

                var resultSnapshot = CloneBuildResultForExport(wbCurrentResult);
                string docLabel = isAppendix ? "Приложение" : (isTtn1 ? "ТТН-1" : "ТН-2");

                _wbIsGenerating = true;
                UpdateExportButtonsState();
                if (wbBtnSaveOpen != null && !sendToPrinter) wbBtnSaveOpen.Content = "⏳ Формирование...";
                if (wbBtnPrintNow != null && sendToPrinter) wbBtnPrintNow.Content = "⏳ Подготовка печати...";
                if (wbLoadingBanner != null)
                {
                    wbLoadingText.Text = string.Format("⏳ Формирование документа ({0})...", docLabel);
                    wbLoadingBanner.Visibility = Visibility.Visible;
                }

                ThreadPool.QueueUserWorkItem(delegate
                {
                    Exception error = null;
                    try
                    {
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                        WaybillEngine.GenerateWaybillXlsx(resultSnapshot, exportProfile, docDate, outFile);
                        if (exportProfile.CreatePickingList && !string.IsNullOrEmpty(pickFile))
                        {
                            WaybillEngine.GeneratePickingListXlsx(resultSnapshot, docDate, pickFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _wbIsGenerating = false;
                        if (wbBtnSaveOpen != null) wbBtnSaveOpen.Content = "💾  Создать и открыть";
                        if (wbBtnPrintNow != null) wbBtnPrintNow.Content = "🖨  Сразу на печать";
                        if (wbLoadingBanner != null) wbLoadingBanner.Visibility = Visibility.Collapsed;
                        UpdateExportButtonsState();

                        if (error != null)
                        {
                            ShowToast("⚠️ Ошибка создания документа: " + error.Message);
                            return;
                        }

                        if (sendToPrinter)
                        {
                            try
                            {
                                WaybillEngine.PrintFileWithShell(outFile);
                                ShowToast("🖨 Отправлено на печать (" + docLabel + "): " + Path.GetFileName(outFile));
                            }
                            catch
                            {
                                WaybillEngine.OpenFileWithShell(outFile);
                                ShowToast("⚠️ Прямая печать недоступна — файл открыт в Excel для печати (Ctrl+P)");
                            }
                        }
                        else
                        {
                            WaybillEngine.OpenFileWithShell(outFile);
                            ShowToast("✓ " + docLabel + " создана и открыта: " + Path.GetFileName(outFile));
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                _wbIsGenerating = false;
                if (wbBtnSaveOpen != null) wbBtnSaveOpen.Content = "💾  Создать и открыть";
                if (wbBtnPrintNow != null) wbBtnPrintNow.Content = "🖨  Сразу на печать";
                if (wbLoadingBanner != null) wbLoadingBanner.Visibility = Visibility.Collapsed;
                UpdateExportButtonsState();
                ShowToast("⚠️ Ошибка создания документа: " + ex.Message);
            }
        }

        private bool ConfirmWaybillWarningDialog(string title, string message, string proceedText, string cancelText)
        {
            bool confirmed = false;
            var dlg = new Window
            {
                Title = title,
                Width = 470,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = Brushes.White
            };

            var root = new StackPanel { Margin = new Thickness(18) };
            root.Children.Add(new TextBlock
            {
                Text = "⚠️  " + title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#92400E"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            root.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = GetBrush("#1C1C1E"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnCancel = CreateCompactActionButton(cancelText, "#F2F2F7", "#1C1C1E");
            btnCancel.MinWidth = 100;
            btnCancel.IsCancel = true;
            btnCancel.Margin = new Thickness(0, 0, 8, 0);
            btnCancel.Click += delegate { confirmed = false; dlg.Close(); };
            btnRow.Children.Add(btnCancel);

            var btnProceed = CreateCompactActionButton(proceedText, "#FEF3C7", "#92400E");
            btnProceed.MinWidth = 150;
            btnProceed.Click += delegate { confirmed = true; dlg.Close(); };
            btnRow.Children.Add(btnProceed);

            root.Children.Add(btnRow);
            dlg.Content = root;
            dlg.ShowDialog();
            return confirmed;
        }

        private static WaybillProfile CloneWaybillProfile(WaybillProfile src)
        {
            if (src == null) return new WaybillProfile();
            return new WaybillProfile
            {
                SenderUnp = src.SenderUnp,
                SenderInfo = src.SenderInfo,
                Signer = src.Signer,
                ReceiverPreset = src.ReceiverPreset,
                ReceiverUnp = src.ReceiverUnp,
                ReceiverInfo = src.ReceiverInfo,
                BasisPrefix = src.BasisPrefix,
                UnitName = src.UnitName,
                VatMode = src.VatMode,
                PriceInputMode = src.PriceInputMode,
                DocType = src.DocType,
                PrintMode = src.PrintMode,
                TopMarginMm = src.TopMarginMm,
                LeftMarginMm = src.LeftMarginMm,
                BsoSeries = src.BsoSeries,
                BsoNumber = src.BsoNumber,
                VehicleInfo = src.VehicleInfo,
                DriverInfo = src.DriverInfo,
                LoadingPoint = src.LoadingPoint,
                UnloadingPoint = src.UnloadingPoint,
                TransportCustomer = src.TransportCustomer,
                UseNormalizationRules = src.UseNormalizationRules,
                SortDescendingByName = src.SortDescendingByName,
                RulesText = src.RulesText,
                OutputFolder = src.OutputFolder,
                CreatePickingList = src.CreatePickingList
            };
        }

        private static WaybillBuildResult CloneBuildResultForExport(WaybillBuildResult src)
        {
            var copy = new WaybillBuildResult
            {
                TotalQty = src.TotalQty,
                TotalNoVat = src.TotalNoVat,
                TotalVat = src.TotalVat,
                TotalWithVat = src.TotalWithVat,
                TotalCargoPlaces = src.TotalCargoPlaces,
                TotalCargoMassKg = src.TotalCargoMassKg,
                IsFboDetected = src.IsFboDetected,
                MissingPriceCount = src.MissingPriceCount,
                RoundingWarningCount = src.RoundingWarningCount,
                CatalogFilledCount = src.CatalogFilledCount
            };
            if (src.InputFiles != null) copy.InputFiles.AddRange(src.InputFiles);
            if (src.SupplyIds != null) copy.SupplyIds.AddRange(src.SupplyIds);
            if (src.Warnings != null) copy.Warnings.AddRange(src.Warnings);
            if (src.SourceItems != null) copy.SourceItems.AddRange(src.SourceItems);
            if (src.GroupedItems != null)
            {
                foreach (var g in src.GroupedItems)
                {
                    if (g == null) continue;
                    copy.GroupedItems.Add(new GroupedWaybillItem
                    {
                        Name = g.Name,
                        Unit = g.Unit,
                        Qty = g.Qty,
                        InputUnitPrice = g.InputUnitPrice,
                        UnitPrice = g.UnitPrice,
                        TotalNoVat = g.TotalNoVat,
                        VatRateLabel = g.VatRateLabel,
                        VatAmount = g.VatAmount,
                        TotalWithVat = g.TotalWithVat,
                        CargoPlaces = g.CargoPlaces,
                        CargoMassKg = g.CargoMassKg,
                        Article = g.Article,
                        Note = g.Note,
                        HasRoundingDiff = g.HasRoundingDiff,
                        RoundingDiff = g.RoundingDiff,
                        RoundingNote = g.RoundingNote,
                        PriceFromCatalog = g.PriceFromCatalog
                    });
                }
            }
            return copy;
        }

        private TextBlock CreateFieldLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#636366")
            };
        }

        private TextBox CreateStyledInput(string value, string tooltip)
        {
            return new TextBox
            {
                Text = value ?? "",
                ToolTip = tooltip,
                FontSize = 12,
                Padding = new Thickness(8, 6, 8, 6),
                Background = GetBrush("#F9F9FB"),
                BorderBrush = GetBrush("#D1D1D6"),
                BorderThickness = new Thickness(1)
            };
        }

        private void ApplyButtonTemplateWithTriggers(Button btn, double cornerRadius)
        {
            var template = new ControlTemplate(typeof(Button));
            var bf = new FrameworkElementFactory(typeof(Border));
            bf.Name = "btnBorder";
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bf.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp);
            template.VisualTree = bf;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85, "btnBorder"));
            template.Triggers.Add(hoverTrigger);

            var pressTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.70, "btnBorder"));
            template.Triggers.Add(pressTrigger);

            var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "btnBorder"));
            template.Triggers.Add(disabledTrigger);

            btn.Template = template;
        }

        private Button CreateSmallPillButton(string text, string bgHex, string fgHex)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush(fgHex),
                Background = GetBrush(bgHex),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand
            };
            ApplyButtonTemplateWithTriggers(btn, 7);
            return btn;
        }

        private Button CreateCompactActionButton(string text, string bgHex, string fgHex)
        {
            var btn = new Button
            {
                Content = text,
                Height = 32,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush(fgHex),
                Background = GetBrush(bgHex),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            ApplyButtonTemplateWithTriggers(btn, 9);
            return btn;
        }

        private Button CreateSolidActionButton(string text, string bgHex, Brush fg)
        {
            var btn = new Button
            {
                Content = text,
                Height = 38,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = fg,
                Background = GetBrush(bgHex),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            ApplyButtonTemplateWithTriggers(btn, 10);
            return btn;
        }
    }

    public class FlexibleDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal)
                return ((decimal)value).ToString("0.00", CultureInfo.InvariantCulture);
            return "0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            string cleaned = s.Trim().Replace("\u00A0", "").Replace(" ", "").Replace(",", ".");
            decimal d;
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out d) && d >= 0m)
                return Math.Round(d, 2, MidpointRounding.AwayFromZero);
            return Binding.DoNothing;
        }
    }

    public class FlexibleIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int)
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            return "1";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return Binding.DoNothing;
            string cleaned = s.Trim().Replace("\u00A0", "").Replace(" ", "");
            int n;
            if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0)
                return n;
            return Binding.DoNothing;
        }
    }

    public class FlexibleNonNegIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int)
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return 0;
            string cleaned = s.Trim().Replace("\u00A0", "").Replace(" ", "");
            int n;
            if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= 0)
                return n;
            return Binding.DoNothing;
        }
    }
}
