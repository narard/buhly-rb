using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;

namespace GovPortalsApp
{
    public class CompanyProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Unp { get; set; }
        public string OrgInfo { get; set; }
        public string Signer { get; set; }
        public string TaxRegime { get; set; }
        public decimal CustomTaxRate { get; set; }
        public WaybillProfile WaybillProfile { get; set; }

        public CompanyProfile()
        {
            Id = Guid.NewGuid().ToString("N");
            DisplayName = "Моя компания";
            Unp = "";
            OrgInfo = "";
            Signer = "";
            TaxRegime = CompanyProfilesManager.RegimeUsn6;
            CustomTaxRate = 6m;
            WaybillProfile = new WaybillProfile();
        }

        public decimal GetEffectiveTaxRate()
        {
            if (TaxRegime == CompanyProfilesManager.RegimeUsn6) return 6m;
            if (TaxRegime == CompanyProfilesManager.RegimeIpPn20) return 20m;
            if (TaxRegime == CompanyProfilesManager.RegimeOsn20) return 20m;
            return CustomTaxRate > 0m ? CustomTaxRate : 6m;
        }

        public string GetTaxRegimeTitle()
        {
            if (TaxRegime == CompanyProfilesManager.RegimeIpPn20)
                return "ИП • Подоходный 20% + ФСЗН 35%";
            if (TaxRegime == CompanyProfilesManager.RegimeOsn20)
                return "ООО • ОСН (НДС 20% + Прибыль 20%)";
            if (TaxRegime == CompanyProfilesManager.RegimeCustom)
                return string.Format("Особый режим ({0}%)", CustomTaxRate.ToString("0.##", CultureInfo.InvariantCulture));
            return "ООО • УСН 6% (без НДС)";
        }

        public string GetDropdownLabel()
        {
            string name = !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName.Trim() : "Без названия";
            if (!string.IsNullOrWhiteSpace(Unp))
                return name + " • " + Unp.Trim();
            return name;
        }

        public void SyncToWaybillProfile()
        {
            if (WaybillProfile == null) WaybillProfile = new WaybillProfile();
            WaybillProfile.SenderUnp = Unp ?? "";
            WaybillProfile.SenderInfo = OrgInfo ?? "";
            WaybillProfile.Signer = Signer ?? "";
        }

        public override string ToString()
        {
            return GetDropdownLabel();
        }
    }

    public class CompanyProfileStore
    {
        public string ActiveProfileId { get; set; }
        public string UiMode { get; set; }
        public List<CompanyProfile> Profiles { get; set; }

        public CompanyProfileStore()
        {
            ActiveProfileId = "";
            UiMode = "Simple";
            Profiles = new List<CompanyProfile>();
        }

        public bool IsProMode()
        {
            return string.Equals(UiMode, "Pro", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class CompanyProfilesManager
    {
        public const string RegimeUsn6 = "USN_6";
        public const string RegimeIpPn20 = "IP_PN_20";
        public const string RegimeOsn20 = "OSN_20";
        public const string RegimeCustom = "CUSTOM";

        public static string GetProfilesPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buhly");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "profiles.xml");
        }

        public static string GuessDisplayName(string orgInfo, string fallback)
        {
            if (string.IsNullOrWhiteSpace(orgInfo)) return fallback;
            string s = orgInfo.Trim();
            int commaIdx = s.IndexOf(',');
            if (commaIdx > 0) s = s.Substring(0, commaIdx).Trim();
            s = s.Replace("Общество с ограниченной ответственностью", "ООО")
                 .Replace("Частное торговое унитарное предприятие", "ЧТУП")
                 .Replace("Частное унитарное предприятие", "ЧУП")
                 .Replace("Индивидуальный предприниматель", "ИП");
            if (s.Length > 34) s = s.Substring(0, 34).Trim() + "…";
            return string.IsNullOrWhiteSpace(s) ? fallback : s;
        }

        public static string GuessRegime(string orgInfo)
        {
            string lower = (orgInfo ?? "").ToLowerInvariant();
            if (lower.Contains("индивидуальный предприниматель") || lower.StartsWith("ип ") || lower.Contains(" ип "))
                return RegimeIpPn20;
            return RegimeUsn6;
        }

        public static CompanyProfileStore LoadStore()
        {
            var path = GetProfilesPath();
            if (File.Exists(path))
            {
                try
                {
                    var ser = new XmlSerializer(typeof(CompanyProfileStore));
                    using (var fs = File.OpenRead(path))
                    {
                        var store = ser.Deserialize(fs) as CompanyProfileStore;
                        if (store != null && store.Profiles != null && store.Profiles.Count > 0)
                        {
                            NormalizeStore(store);
                            return store;
                        }
                    }
                }
                catch { }
            }

            // Migrate existing waybill_profile.xml on first launch
            var newStore = new CompanyProfileStore();
            var legacyPath = WaybillEngine.GetProfilePath();
            if (File.Exists(legacyPath))
            {
                var wp = WaybillEngine.LoadProfile();
                var p = new CompanyProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Unp = wp.SenderUnp ?? "",
                    OrgInfo = wp.SenderInfo ?? "",
                    Signer = wp.Signer ?? "",
                    DisplayName = GuessDisplayName(wp.SenderInfo, "Основная организация"),
                    TaxRegime = GuessRegime(wp.SenderInfo),
                    WaybillProfile = wp
                };
                p.CustomTaxRate = p.TaxRegime == RegimeIpPn20 ? 20m : 6m;
                p.SyncToWaybillProfile();
                newStore.Profiles.Add(p);
                newStore.ActiveProfileId = p.Id;
            }
            else
            {
                var def = new CompanyProfile();
                newStore.Profiles.Add(def);
                newStore.ActiveProfileId = def.Id;
            }

            SaveStore(newStore);
            return newStore;
        }

        internal static void NormalizeStore(CompanyProfileStore store)
        {
            if (store == null) return;
            if (string.Equals(store.UiMode, "Pro", StringComparison.OrdinalIgnoreCase))
                store.UiMode = "Pro";
            else
                store.UiMode = "Simple";

            if (store.Profiles == null) store.Profiles = new List<CompanyProfile>();
            foreach (var p in store.Profiles)
            {
                if (string.IsNullOrWhiteSpace(p.Id)) p.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(p.DisplayName)) p.DisplayName = GuessDisplayName(p.OrgInfo, "Компания");
                if (string.IsNullOrWhiteSpace(p.TaxRegime)) p.TaxRegime = RegimeUsn6;
                if (p.WaybillProfile == null) p.WaybillProfile = new WaybillProfile();
                WaybillEngine.NormalizeProfileDefaults(p.WaybillProfile);
                if (string.IsNullOrWhiteSpace(p.Unp) && !string.IsNullOrWhiteSpace(p.WaybillProfile.SenderUnp))
                    p.Unp = p.WaybillProfile.SenderUnp;
                if (string.IsNullOrWhiteSpace(p.OrgInfo) && !string.IsNullOrWhiteSpace(p.WaybillProfile.SenderInfo))
                    p.OrgInfo = p.WaybillProfile.SenderInfo;
                if (string.IsNullOrWhiteSpace(p.Signer) && !string.IsNullOrWhiteSpace(p.WaybillProfile.Signer))
                    p.Signer = p.WaybillProfile.Signer;
                p.SyncToWaybillProfile();
            }

            if (GetActiveProfile(store) == null && store.Profiles.Count > 0)
            {
                store.ActiveProfileId = store.Profiles[0].Id;
            }
        }

        public static void Save(CompanyProfileStore store)
        {
            SaveStore(store);
        }

        public static void SaveStore(CompanyProfileStore store)
        {
            if (store == null) return;
            try
            {
                var path = GetProfilesPath();
                var ser = new XmlSerializer(typeof(CompanyProfileStore));
                using (var fs = File.Create(path))
                {
                    ser.Serialize(fs, store);
                }
            }
            catch { }
        }

        public static CompanyProfile GetActiveProfile(CompanyProfileStore store)
        {
            if (store == null || store.Profiles == null || store.Profiles.Count == 0) return null;
            foreach (var p in store.Profiles)
            {
                if (string.Equals(p.Id, store.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return store.Profiles[0];
        }

        public static void SetActiveProfile(CompanyProfileStore store, string id)
        {
            if (store == null || string.IsNullOrEmpty(id)) return;
            store.ActiveProfileId = id;
            SaveStore(store);
        }
    }

    public partial class MainWindow : Window
    {
        private CompanyProfileStore companyStore;

        public CompanyProfile ActiveCompanyProfile
        {
            get
            {
                if (companyStore == null) companyStore = CompanyProfilesManager.LoadStore();
                return CompanyProfilesManager.GetActiveProfile(companyStore);
            }
        }

        public void SaveActiveCompanyStore()
        {
            if (companyStore != null)
            {
                CompanyProfilesManager.SaveStore(companyStore);
            }
        }

        public bool IsProUiMode()
        {
            if (companyStore == null) companyStore = CompanyProfilesManager.LoadStore();
            return companyStore != null && companyStore.IsProMode();
        }

        private void OpenProfileManagerWindow()
        {
            if (companyStore == null) companyStore = CompanyProfilesManager.LoadStore();

            var win = new Window
            {
                Title = "Buhly • Профили организаций",
                Width = 780,
                Height = 560,
                MinWidth = 680,
                MinHeight = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = GetBrush("#F2F2F7"),
                FontFamily = this.FontFamily
            };

            var rootGrid = new Grid { Margin = new Thickness(16) };
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LEFT PANEL: Profiles list
            var leftCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };
            var leftDock = new DockPanel();

            var leftHeader = new TextBlock
            {
                Text = "Организации и ИП",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(leftHeader, Dock.Top);
            leftDock.Children.Add(leftHeader);

            var leftBtnRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            leftBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            leftBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            leftBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnNew = CreateCompactActionButton("➕ Создать", "#EBF3FF", "#007AFF");
            var btnDelete = CreateCompactActionButton("🗑️", "#FDE8E8", "#DC2626");
            btnDelete.Padding = new Thickness(10, 0, 10, 0);
            btnDelete.ToolTip = "Удалить выбранный профиль";
            Grid.SetColumn(btnNew, 0);
            Grid.SetColumn(btnDelete, 2);
            leftBtnRow.Children.Add(btnNew);
            leftBtnRow.Children.Add(btnDelete);
            DockPanel.SetDock(leftBtnRow, Dock.Bottom);
            leftDock.Children.Add(leftBtnRow);

            var listBox = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent
            };
            leftDock.Children.Add(listBox);
            leftCard.Child = leftDock;

            // RIGHT PANEL: Editor form
            var rightCard = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18),
                BorderBrush = GetBrush("#E5E5EA"),
                BorderThickness = new Thickness(1)
            };

            var rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var form = new StackPanel();

            var topTitleRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            topTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            topTitleRow.Children.Add(new TextBlock
            {
                Text = "Карточка профиля",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("#1C1C1E"),
                VerticalAlignment = VerticalAlignment.Center
            });

            var btnFillCert = CreateSmallPillButton("🔑 Автозаполнить с ЭЦП + МНС", "#EBF3FF", "#007AFF");
            Grid.SetColumn(btnFillCert, 1);
            topTitleRow.Children.Add(btnFillCert);
            form.Children.Add(topTitleRow);

            form.Children.Add(CreateFieldLabel("Короткое название (для списка в меню)"));
            var tbDisplayName = CreateStyledInput("", "Напр.: ООО «АльфаТрейд» или ИП Иванов");
            tbDisplayName.Margin = new Thickness(0, 4, 0, 10);
            form.Children.Add(tbDisplayName);

            form.Children.Add(CreateFieldLabel("УНП (9 цифр)"));
            var unpGrid = new Grid { Margin = new Thickness(0, 4, 0, 10) };
            unpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            unpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            unpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tbUnp = CreateStyledInput("", "9 цифр УНП");
            Grid.SetColumn(tbUnp, 0);
            unpGrid.Children.Add(tbUnp);

            var btnLookupUnp = CreateSmallPillButton("🔎 Заполнить из ГРП МНС", "#F2F2F7", "#1C1C1E");
            Grid.SetColumn(btnLookupUnp, 2);
            unpGrid.Children.Add(btnLookupUnp);
            form.Children.Add(unpGrid);

            form.Children.Add(CreateFieldLabel("Полное наименование и юридический адрес"));
            var tbOrgInfo = CreateStyledInput("", "ООО \"...\", г. Минск, ул. ...");
            tbOrgInfo.TextWrapping = TextWrapping.Wrap;
            tbOrgInfo.AcceptsReturn = true;
            tbOrgInfo.Height = 52;
            tbOrgInfo.Margin = new Thickness(0, 4, 0, 10);
            form.Children.Add(tbOrgInfo);

            form.Children.Add(CreateFieldLabel("Подписант (должность и ФИО для ТН-2)"));
            var tbSigner = CreateStyledInput("", "Напр.: Директор Иванов Иван Иванович");
            tbSigner.Margin = new Thickness(0, 4, 0, 10);
            form.Children.Add(tbSigner);

            form.Children.Add(CreateFieldLabel("Система налогообложения (РБ, 2026)"));
            var regimeGrid = new Grid { Margin = new Thickness(0, 4, 0, 6) };
            regimeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            regimeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            regimeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

            var cbRegime = new ComboBox { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            cbRegime.Items.Add("ООО • УСН 6% (без НДС)");
            cbRegime.Items.Add("ИП • Подоходный 20% + ФСЗН 35%");
            cbRegime.Items.Add("ООО • ОСН (НДС 20% + Прибыль 20%)");
            cbRegime.Items.Add("Другая ставка (%)");

            var tbCustomRate = CreateStyledInput("6", "Ставка налога, %");
            Grid.SetColumn(cbRegime, 0);
            Grid.SetColumn(tbCustomRate, 2);
            regimeGrid.Children.Add(cbRegime);
            regimeGrid.Children.Add(tbCustomRate);
            form.Children.Add(regimeGrid);

            var regimeHint = new TextBlock
            {
                Text = "Напоминание: ИП в РБ с 2023 года применять УСН не могут (используется подоходный налог 20% / 30% + ФСЗН 35%).",
                FontSize = 11,
                Foreground = GetBrush("#8E8E93"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            form.Children.Add(regimeHint);

            var dialogStatus = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                Foreground = GetBrush("#007AFF"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            form.Children.Add(dialogStatus);

            var actionRow = new Grid();
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnSaveActivate = CreateSolidActionButton("💾  Сохранить и сделать активным", "#34C759", Brushes.White);
            var btnClose = CreateCompactActionButton("Закрыть", "#F2F2F7", "#1C1C1E");
            btnClose.Height = 38;
            btnClose.Padding = new Thickness(14, 0, 14, 0);
            Grid.SetColumn(btnSaveActivate, 0);
            Grid.SetColumn(btnClose, 2);
            actionRow.Children.Add(btnSaveActivate);
            actionRow.Children.Add(btnClose);
            form.Children.Add(actionRow);

            rightScroll.Content = form;
            rightCard.Child = rightScroll;

            Grid.SetColumn(leftCard, 0);
            Grid.SetColumn(rightCard, 2);
            rootGrid.Children.Add(leftCard);
            rootGrid.Children.Add(rightCard);

            CompanyProfile selectedProfile = ActiveCompanyProfile;

            Action<CompanyProfile> loadIntoForm = delegate(CompanyProfile prof)
            {
                if (prof == null) return;
                selectedProfile = prof;
                tbDisplayName.Text = prof.DisplayName ?? "";
                tbUnp.Text = prof.Unp ?? "";
                tbOrgInfo.Text = prof.OrgInfo ?? "";
                tbSigner.Text = prof.Signer ?? "";
                if (prof.TaxRegime == CompanyProfilesManager.RegimeIpPn20) cbRegime.SelectedIndex = 1;
                else if (prof.TaxRegime == CompanyProfilesManager.RegimeOsn20) cbRegime.SelectedIndex = 2;
                else if (prof.TaxRegime == CompanyProfilesManager.RegimeCustom) cbRegime.SelectedIndex = 3;
                else cbRegime.SelectedIndex = 0;

                decimal rate = prof.TaxRegime == CompanyProfilesManager.RegimeCustom ? prof.CustomTaxRate : prof.GetEffectiveTaxRate();
                tbCustomRate.Text = rate.ToString("0.##", CultureInfo.InvariantCulture);
                tbCustomRate.IsEnabled = (cbRegime.SelectedIndex == 3);
                dialogStatus.Text = "";
            };

            Action refreshList = delegate
            {
                listBox.Items.Clear();
                foreach (var p in companyStore.Profiles)
                {
                    listBox.Items.Add(p);
                }
                if (selectedProfile != null)
                {
                    listBox.SelectedItem = selectedProfile;
                }
                else if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }
            };

            cbRegime.SelectionChanged += delegate
            {
                int idx = cbRegime.SelectedIndex;
                tbCustomRate.IsEnabled = (idx == 3);
                if (idx == 0) tbCustomRate.Text = "6";
                else if (idx == 1 || idx == 2) tbCustomRate.Text = "20";
            };

            listBox.SelectionChanged += delegate
            {
                var p = listBox.SelectedItem as CompanyProfile;
                if (p != null) loadIntoForm(p);
            };

            btnNew.Click += delegate
            {
                var np = new CompanyProfile
                {
                    DisplayName = "Новая организация " + (companyStore.Profiles.Count + 1),
                    TaxRegime = CompanyProfilesManager.RegimeUsn6,
                    CustomTaxRate = 6m
                };
                companyStore.Profiles.Add(np);
                selectedProfile = np;
                refreshList();
                loadIntoForm(np);
                dialogStatus.Text = "Заполните реквизиты или нажмите «Автозаполнить с ЭЦП + МНС».";
            };

            btnDelete.Click += delegate
            {
                var p = listBox.SelectedItem as CompanyProfile;
                if (p == null) return;
                if (companyStore.Profiles.Count <= 1)
                {
                    dialogStatus.Foreground = GetBrush("#FF9500");
                    dialogStatus.Text = "Нельзя удалить единственный профиль. Отредактируйте его вместо удаления.";
                    return;
                }
                companyStore.Profiles.Remove(p);
                if (string.Equals(companyStore.ActiveProfileId, p.Id, StringComparison.OrdinalIgnoreCase))
                {
                    companyStore.ActiveProfileId = companyStore.Profiles[0].Id;
                }
                selectedProfile = companyStore.Profiles[0];
                CompanyProfilesManager.SaveStore(companyStore);
                refreshList();
                loadIntoForm(selectedProfile);
                RefreshAfterProfileChange();
                dialogStatus.Foreground = GetBrush("#16A34A");
                dialogStatus.Text = "✓ Профиль удалён.";
            };

            btnLookupUnp.Click += delegate
            {
                string rawUnp = (tbUnp.Text ?? "").Trim();
                dialogStatus.Foreground = GetBrush("#007AFF");
                dialogStatus.Text = "⏳ Запрос в ГРП МНС по УНП " + rawUnp + "...";
                btnLookupUnp.IsEnabled = false;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    var res = UnpChecker.Lookup(rawUnp);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        btnLookupUnp.IsEnabled = true;
                        if (res == null || !res.Success)
                        {
                            dialogStatus.Foreground = GetBrush("#DC2626");
                            dialogStatus.Text = "⚠️ " + (res != null ? res.ErrorMessage : "Ошибка запроса");
                            return;
                        }
                        tbUnp.Text = res.Unp;
                        string namePart = !string.IsNullOrEmpty(res.ShortName) ? res.ShortName : res.FullName;
                        string fullInfo = string.IsNullOrEmpty(res.Address) ? namePart : (namePart + ", " + res.Address);
                        tbOrgInfo.Text = fullInfo;
                        if (string.IsNullOrWhiteSpace(tbDisplayName.Text) || tbDisplayName.Text.StartsWith("Новая организация") || tbDisplayName.Text == "Моя компания")
                        {
                            tbDisplayName.Text = CompanyProfilesManager.GuessDisplayName(fullInfo, namePart);
                        }
                        string guessedRegime = CompanyProfilesManager.GuessRegime(fullInfo);
                        if (guessedRegime == CompanyProfilesManager.RegimeIpPn20) cbRegime.SelectedIndex = 1;
                        dialogStatus.Foreground = GetBrush("#16A34A");
                        dialogStatus.Text = "✓ Заполнено из базы ГРП МНС: " + namePart;
                    }));
                });
            };

            btnFillCert.Click += delegate
            {
                dialogStatus.Foreground = GetBrush("#007AFF");
                dialogStatus.Text = "🔑 Поиск вставленного токена ЭЦП и запрос в МНС...";
                btnFillCert.IsEnabled = false;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string unp, org, signer;
                    bool ok = WaybillEngine.TryExtractFromCertStore(out unp, out org, out signer);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        btnFillCert.IsEnabled = true;
                        if (!ok)
                        {
                            dialogStatus.Foreground = GetBrush("#FF9500");
                            dialogStatus.Text = "⚠️ Действующий ключ ЭЦП не найден. Вставьте токен или введите УНП вручную.";
                            return;
                        }
                        if (!string.IsNullOrEmpty(unp)) tbUnp.Text = unp;
                        if (!string.IsNullOrEmpty(org)) tbOrgInfo.Text = org;
                        if (!string.IsNullOrEmpty(signer)) tbSigner.Text = signer;
                        if (!string.IsNullOrEmpty(org))
                        {
                            tbDisplayName.Text = CompanyProfilesManager.GuessDisplayName(org, "Организация");
                            if (CompanyProfilesManager.GuessRegime(org) == CompanyProfilesManager.RegimeIpPn20)
                                cbRegime.SelectedIndex = 1;
                        }
                        dialogStatus.Foreground = GetBrush("#16A34A");
                        dialogStatus.Text = "✓ Данные успешно подтянуты с ключа ЭЦП и ГРП МНС!";
                    }));
                });
            };

            btnSaveActivate.Click += delegate
            {
                if (selectedProfile == null) return;
                selectedProfile.DisplayName = string.IsNullOrWhiteSpace(tbDisplayName.Text)
                    ? CompanyProfilesManager.GuessDisplayName(tbOrgInfo.Text, "Компания")
                    : tbDisplayName.Text.Trim();
                selectedProfile.Unp = (tbUnp.Text ?? "").Trim();
                selectedProfile.OrgInfo = (tbOrgInfo.Text ?? "").Trim();
                selectedProfile.Signer = (tbSigner.Text ?? "").Trim();

                if (cbRegime.SelectedIndex == 1) selectedProfile.TaxRegime = CompanyProfilesManager.RegimeIpPn20;
                else if (cbRegime.SelectedIndex == 2) selectedProfile.TaxRegime = CompanyProfilesManager.RegimeOsn20;
                else if (cbRegime.SelectedIndex == 3) selectedProfile.TaxRegime = CompanyProfilesManager.RegimeCustom;
                else selectedProfile.TaxRegime = CompanyProfilesManager.RegimeUsn6;

                decimal parsedRate;
                string rawRate = (tbCustomRate.Text ?? "").Trim().Replace(",", ".");
                if (decimal.TryParse(rawRate, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedRate) && parsedRate >= 0m && parsedRate <= 100m)
                {
                    selectedProfile.CustomTaxRate = parsedRate;
                }
                else
                {
                    selectedProfile.CustomTaxRate = selectedProfile.GetEffectiveTaxRate();
                }

                selectedProfile.SyncToWaybillProfile();
                companyStore.ActiveProfileId = selectedProfile.Id;
                CompanyProfilesManager.SaveStore(companyStore);
                WaybillEngine.SaveProfile(selectedProfile.WaybillProfile);

                refreshList();
                RefreshAfterProfileChange();
                ShowToast("✓ Активный профиль: " + selectedProfile.DisplayName);
                win.Close();
            };

            btnClose.Click += delegate { win.Close(); };

            refreshList();
            loadIntoForm(selectedProfile);

            win.Content = rootGrid;
            win.ShowDialog();
        }
    }
}
