using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace GovPortalsApp
{
    public partial class MainWindow : Window
    {
        private WaybillProfile wbProfile;
        private List<string> wbLoadedFiles = new List<string>();
        private WaybillBuildResult wbCurrentResult;

        // UI controls for Waybills tab
        private Border wbDropZone;
        private TextBlock wbDropTitle;
        private TextBlock wbDropSubtitle;
        private Border wbPreviewCard;
        private TextBlock wbSummaryText;
        private StackPanel wbPreviewRowsStack;
        private DatePicker wbDatePicker;
        private CheckBox wbPickingListCheck;
        private StackPanel wbSettingsBody;
        private TextBlock wbSettingsChevron;

        private TextBox wbSenderUnpBox;
        private TextBox wbSenderInfoBox;
        private TextBox wbSignerBox;
        private TextBox wbReceiverUnpBox;
        private TextBox wbReceiverInfoBox;
        private TextBox wbBasisBox;
        private ComboBox wbVatCombo;
        private CheckBox wbSortDescCheck;
        private CheckBox wbUseRulesCheck;
        private TextBox wbRulesBox;
        private TextBox wbOutputFolderBox;

        private void BuildWaybillsTab(StackPanel container)
        {
            wbProfile = WaybillEngine.LoadProfile();

            // CARD 1: DROP ZONE, PREVIEW & ACTIONS
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
                Text = "📦  Накладная ТН-2 из поставки WB / Excel",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E")
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Перетащите один или несколько файлов поставки (.xlsx) — программа сама сгруппирует товары, посчитает суммы и заполнит бланк ТН-2.",
                FontSize = 11.5,
                Foreground = GetBrush("#636366"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            });

            // Drop zone
            wbDropZone = new Border
            {
                Background = GetBrush("#F0F7FF"),
                BorderBrush = GetBrush("#93C5FD"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 16, 14, 16),
                AllowDrop = true,
                Cursor = Cursors.Hand
            };
            var dropStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            wbDropTitle = new TextBlock
            {
                Text = "📥  Перетащите файлы wb-gi-*.xlsx сюда",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#007AFF"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            wbDropSubtitle = new TextBlock
            {
                Text = "или нажмите здесь, чтобы выбрать файлы на компьютере",
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

            // File action row (Choose / Clear)
            var fileBtnRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            fileBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnSelectFiles = CreateCompactActionButton("📂  Выбрать Excel (.xlsx)", "#EBF3FF", "#007AFF");
            btnSelectFiles.Click += delegate { SelectSupplyFilesDialog(); };
            Grid.SetColumn(btnSelectFiles, 0);
            fileBtnRow.Children.Add(btnSelectFiles);

            var btnClearFiles = CreateCompactActionButton("✕ Очистить", "#F2F2F7", "#636366");
            btnClearFiles.Padding = new Thickness(12, 0, 12, 0);
            btnClearFiles.Click += delegate { ClearSupplyFiles(); };
            Grid.SetColumn(btnClearFiles, 2);
            fileBtnRow.Children.Add(btnClearFiles);
            stack.Children.Add(fileBtnRow);

            // Preview card (hidden until files loaded)
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
            wbSummaryText = new TextBlock
            {
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            prevStack.Children.Add(wbSummaryText);

            var tableScroll = new ScrollViewer
            {
                MaxHeight = 155,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            wbPreviewRowsStack = new StackPanel();
            tableScroll.Content = wbPreviewRowsStack;
            prevStack.Children.Add(tableScroll);
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
                Text = "Дата ТН:",
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
                WaybillEngine.SaveProfile(wbProfile);
            };
            Grid.SetColumn(wbPickingListCheck, 3);
            optsGrid.Children.Add(wbPickingListCheck);
            stack.Children.Add(optsGrid);

            // Primary Action Buttons
            var actGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            actGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnSaveOpen = CreateSolidActionButton("💾  Создать и открыть", "#34C759", Brushes.White);
            btnSaveOpen.Click += delegate { RunWaybillExport(false); };
            Grid.SetColumn(btnSaveOpen, 0);
            actGrid.Children.Add(btnSaveOpen);

            var btnPrintNow = CreateSolidActionButton("🖨  Сразу на печать", "#007AFF", Brushes.White);
            btnPrintNow.Click += delegate { RunWaybillExport(true); };
            Grid.SetColumn(btnPrintNow, 2);
            actGrid.Children.Add(btnPrintNow);
            stack.Children.Add(actGrid);

            mainCard.Child = stack;
            container.Children.Add(mainCard);

            // CARD 2: REQUISITES & NORMALIZATION SETTINGS
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
                Text = "⚙️  Реквизиты и правила группировки",
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

            // VAT + SORT
            var vatGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            vatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            vatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            vatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var vatLbl = CreateFieldLabel("Ставка НДС:");
            vatLbl.VerticalAlignment = VerticalAlignment.Center;
            vatLbl.Margin = new Thickness(0, 0, 8, 0);
            vatGrid.Children.Add(vatLbl);

            wbVatCombo = new ComboBox { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            wbVatCombo.Items.Add("Без НДС");
            wbVatCombo.Items.Add("20%");
            wbVatCombo.Items.Add("10%");
            wbVatCombo.Items.Add("0%");
            wbVatCombo.SelectedItem = wbVatCombo.Items.Contains(wbProfile.VatMode) ? wbProfile.VatMode : "Без НДС";
            wbVatCombo.SelectionChanged += delegate { RecomputePreviewIfLoaded(); };
            Grid.SetColumn(wbVatCombo, 1);
            vatGrid.Children.Add(wbVatCombo);

            wbSortDescCheck = new CheckBox
            {
                Content = "Сортировка имён Я → А",
                IsChecked = wbProfile.SortDescendingByName,
                FontSize = 11.5,
                Foreground = GetBrush("#3A3A3C"),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Как в вашем шаблоне: Ручка -> Пленка (Я -> А), затем по возрастанию цены"
            };
            wbSortDescCheck.Click += delegate { RecomputePreviewIfLoaded(); };
            Grid.SetColumn(wbSortDescCheck, 3);
            vatGrid.Children.Add(wbSortDescCheck);
            body.Children.Add(vatGrid);

            // NORMALIZATION RULES
            wbUseRulesCheck = new CheckBox
            {
                Content = "Группировать названия по правилам (если снять — будут оригиналы из Excel)",
                IsChecked = wbProfile.UseNormalizationRules,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 2, 0, 4)
            };
            wbUseRulesCheck.Click += delegate { RecomputePreviewIfLoaded(); };
            body.Children.Add(wbUseRulesCheck);

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
                ToolTip = "Формат: ключевое_слово1; слово2 => Название в ТН. Строка * => Название для всех остальных."
            };
            body.Children.Add(wbRulesBox);
            body.Children.Add(new TextBlock
            {
                Text = "Формат строки: слово1; слово2 => Название в ТН   |   * => Название для прочих",
                FontSize = 10.5,
                Foreground = GetBrush("#8E8E93"),
                Margin = new Thickness(0, 3, 0, 10)
            });

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
                string dir = wbOutputFolderBox.Text.Trim();
                if (!string.IsNullOrEmpty(dir))
                {
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    WaybillEngine.OpenFileWithShell(dir);
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
                WaybillEngine.SaveProfile(wbProfile);
                RecomputePreviewIfLoaded();
                ShowToast("✓ Реквизиты и правила сохранены");
            };
            body.Children.Add(btnSaveProf);
        }

        private void SyncFieldsToProfile()
        {
            if (wbProfile == null) return;
            wbProfile.SenderUnp = (wbSenderUnpBox.Text ?? "").Trim();
            wbProfile.SenderInfo = (wbSenderInfoBox.Text ?? "").Trim();
            wbProfile.Signer = (wbSignerBox.Text ?? "").Trim();
            wbProfile.ReceiverUnp = (wbReceiverUnpBox.Text ?? "").Trim();
            wbProfile.ReceiverInfo = (wbReceiverInfoBox.Text ?? "").Trim();
            wbProfile.BasisPrefix = (wbBasisBox.Text ?? "").Trim();
            wbProfile.VatMode = wbVatCombo.SelectedItem != null ? wbVatCombo.SelectedItem.ToString() : "Без НДС";
            wbProfile.SortDescendingByName = wbSortDescCheck.IsChecked == true;
            wbProfile.UseNormalizationRules = wbUseRulesCheck.IsChecked == true;
            wbProfile.RulesText = wbRulesBox.Text ?? "";
            wbProfile.OutputFolder = (wbOutputFolderBox.Text ?? "").Trim();
            wbProfile.CreatePickingList = wbPickingListCheck.IsChecked == true;
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
                    WaybillEngine.SaveProfile(wbProfile);
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
                ShowToast("⚠️ Нужен файл формата .xlsx (выгрузка поставки WB/Ozon)");
                return;
            }
            LoadSupplyFiles(valid);
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
                LoadSupplyFiles(dlg.FileNames);
            }
        }

        private void ClearSupplyFiles()
        {
            wbLoadedFiles.Clear();
            wbCurrentResult = null;
            wbPreviewCard.Visibility = Visibility.Collapsed;
            wbDropTitle.Text = "📥  Перетащите файлы wb-gi-*.xlsx сюда";
            wbDropTitle.Foreground = GetBrush("#007AFF");
            wbDropSubtitle.Text = "или нажмите здесь, чтобы выбрать файлы на компьютере";
            ResetDropZoneVisual(false);
        }

        private void LoadSupplyFiles(IEnumerable<string> paths)
        {
            wbLoadedFiles = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            RecomputePreviewIfLoaded();
        }

        private void RecomputePreviewIfLoaded()
        {
            if (wbLoadedFiles.Count == 0) return;
            try
            {
                SyncFieldsToProfile();
                wbCurrentResult = WaybillEngine.ParseSupplyExcelFiles(wbLoadedFiles, wbProfile);

                ResetDropZoneVisual(true);
                string supplies = wbCurrentResult.SupplyIds.Count > 0 ? string.Join(", ", wbCurrentResult.SupplyIds) : "из файла";
                wbDropTitle.Text = "✓  Загружено файлов: " + wbLoadedFiles.Count + "  (" + supplies + ")";
                wbDropTitle.Foreground = GetBrush("#16A34A");
                wbDropSubtitle.Text = string.Join(", ", wbLoadedFiles.Select(f => Path.GetFileName(f)));

                wbSummaryText.Text = string.Format(
                    "Позиций в ТН: {0}  •  Товаров: {1} шт  •  Итого: {2} BYN",
                    wbCurrentResult.GroupedItems.Count,
                    wbCurrentResult.TotalQty,
                    wbCurrentResult.TotalWithVat.ToString("0.00", CultureInfo.InvariantCulture));

                wbPreviewRowsStack.Children.Clear();
                for (int i = 0; i < wbCurrentResult.GroupedItems.Count; i++)
                {
                    var it = wbCurrentResult.GroupedItems[i];
                    var rowGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

                    rowGrid.Children.Add(new TextBlock
                    {
                        Text = (i + 1) + ". " + it.Name,
                        FontSize = 11.5,
                        Foreground = GetBrush("#1C1C1E"),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    var qtyTb = new TextBlock
                    {
                        Text = it.Qty + " шт",
                        FontSize = 11.5,
                        Foreground = GetBrush("#636366"),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(qtyTb, 1);
                    rowGrid.Children.Add(qtyTb);

                    var priceTb = new TextBlock
                    {
                        Text = it.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                        FontSize = 11.5,
                        Foreground = GetBrush("#636366"),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(priceTb, 2);
                    rowGrid.Children.Add(priceTb);

                    var sumTb = new TextBlock
                    {
                        Text = it.TotalWithVat.ToString("0.00", CultureInfo.InvariantCulture),
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = GetBrush("#1C1C1E"),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(sumTb, 3);
                    rowGrid.Children.Add(sumTb);

                    wbPreviewRowsStack.Children.Add(rowGrid);
                }

                wbPreviewCard.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                wbCurrentResult = null;
                wbPreviewCard.Visibility = Visibility.Collapsed;
                ShowToast("⚠️ " + ex.Message);
            }
        }

        private void RunWaybillExport(bool sendToPrinter)
        {
            if (wbLoadedFiles.Count == 0 || wbCurrentResult == null || wbCurrentResult.GroupedItems.Count == 0)
            {
                ShowToast("⚠️ Сначала перетащите или выберите файл поставки .xlsx");
                return;
            }

            try
            {
                SyncFieldsToProfile();
                WaybillEngine.SaveProfile(wbProfile);
                wbCurrentResult = WaybillEngine.ParseSupplyExcelFiles(wbLoadedFiles, wbProfile);

                if (string.IsNullOrWhiteSpace(wbProfile.SenderUnp) || string.IsNullOrWhiteSpace(wbProfile.SenderInfo))
                {
                    wbSettingsBody.Visibility = Visibility.Visible;
                    wbSettingsChevron.Text = "▲ Свернуть";
                    ShowToast("⚠️ Заполните УНП и наименование Вашей организации (или нажмите «Подтянуть с ЭЦП»)");
                    return;
                }

                DateTime docDate = wbDatePicker.SelectedDate ?? DateTime.Today;
                string folder = wbProfile.OutputFolder;
                if (string.IsNullOrWhiteSpace(folder))
                    folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Buhly_ТН");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string dateStr = docDate.ToString("dd.MM.yyyy");
                string outFile = Path.Combine(folder, dateStr + ".xlsx");
                if (File.Exists(outFile) && wbCurrentResult.SupplyIds.Count > 0)
                {
                    string safeSupply = string.Join("_", wbCurrentResult.SupplyIds.Take(2)).Replace(":", "");
                    outFile = Path.Combine(folder, dateStr + "_" + safeSupply + ".xlsx");
                }

                WaybillEngine.GenerateWaybillXlsx(wbCurrentResult, wbProfile, docDate, outFile);

                if (wbProfile.CreatePickingList)
                {
                    string pickFile = Path.Combine(folder, "Лист_сборки_" + dateStr + ".xlsx");
                    WaybillEngine.GeneratePickingListXlsx(wbCurrentResult, docDate, pickFile);
                }

                if (sendToPrinter)
                {
                    try
                    {
                        WaybillEngine.PrintFileWithShell(outFile);
                        ShowToast("🖨 Отправлено на печать: " + Path.GetFileName(outFile));
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
                    ShowToast("✓ ТН-2 создана и открыта: " + Path.GetFileName(outFile));
                }
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Ошибка создания ТН: " + ex.Message);
            }
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
            var template = new ControlTemplate(typeof(Button));
            var bf = new FrameworkElementFactory(typeof(Border));
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bf.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp);
            template.VisualTree = bf;
            btn.Template = template;
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
            var template = new ControlTemplate(typeof(Button));
            var bf = new FrameworkElementFactory(typeof(Border));
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bf.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp);
            template.VisualTree = bf;
            btn.Template = template;
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
            var template = new ControlTemplate(typeof(Button));
            var bf = new FrameworkElementFactory(typeof(Border));
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp);
            template.VisualTree = bf;
            btn.Template = template;
            return btn;
        }
    }
}
