using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DepoStok.Data;

namespace DepoStok
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Tablodaki onay kutusu sütununun adı.
        /// </summary>
        private const string SelectedColumn = "Selected";

        /// <summary>
        /// Ürün tipi sayfasında şu an açık olan tip. Ana sayfadaysak boştur.
        /// </summary>
        private ProductType _currentType;

        /// <summary>
        /// Açık tipin alanları (tablodaki sütunlar).
        /// </summary>
        private List<PropertyDefinition> _currentProperties = new List<PropertyDefinition>();

        /// <summary>
        /// Açık tipin seri numarası sütununun tablodaki adı. Tipte seri numarası alanı yoksa boştur.
        /// </summary>
        private string _serialColumn;

        /// <summary>
        /// Seçili filtreler: alan numarası -> seçilen değerler.
        /// </summary>
        private readonly Dictionary<long, HashSet<string>> _selected =
            new Dictionary<long, HashSet<string>>();

        public MainWindow()
        {
            InitializeComponent();
            ProductGrid.MouseDoubleClick += ProductGrid_MouseDoubleClick;
            ShowHome();
        }

        // ---------- SAYFA GEÇİŞLERİ ----------

        /// <summary>
        /// Ana sayfayı gösterir ve soldaki ürün cinsleri listesini yeniler.
        /// </summary>
        private void ShowHome()
        {
            _currentType = null;
            TypePage.Visibility = Visibility.Collapsed;
            HomePage.Visibility = Visibility.Visible;
            ProductTypeList.ItemsSource = ProductTypeRepository.GetAll();
        }

        /// <summary>
        /// Seçilen ürün tipinin sayfasını açar. Eski arama ve filtreler temizlenir.
        /// </summary>
        private void ShowTypePage(ProductType type)
        {
            _currentType = type;
            HomePage.Visibility = Visibility.Collapsed;
            TypePage.Visibility = Visibility.Visible;
            TypePageTitle.Text = type.Name;

            _selected.Clear();
            TypeSerialSearchBox.Clear();
            ShowNormalBar();
            LoadProducts();
        }

        // ---------- SOL BAR MODLARI ----------

        private void ShowNormalBar()
        {
            FilterLeftPanel.Visibility = Visibility.Collapsed;
            NormalLeftPanel.Visibility = Visibility.Visible;
        }

        private void ShowFilterBar()
        {
            NormalLeftPanel.Visibility = Visibility.Collapsed;
            FilterLeftPanel.Visibility = Visibility.Visible;
        }

        // ---------- EXCEL GÖRÜNÜMÜ ----------

        /// <summary>
        /// Açık olan tipin ürünlerini veritabanından okuyup tabloya doldurur.
        /// </summary>
        private void LoadProducts()
        {
            if (_currentType == null)
            {
                return;
            }

            var properties = TypePropertyRepository.GetForType(_currentType.Id);
            _currentProperties = properties;

            // Seri numarası alanı yoksa ürünler adet bazlıdır: Miktar sütunu gösterilir.
            var serialProperty = properties.FirstOrDefault(p => p.IsSerialNumber);
            bool includeQuantity = serialProperty == null;

            if (serialProperty == null)
            {
                _serialColumn = null;
                TypeSerialSearchBox.IsEnabled = false;
                SerialSearchHint.Text = "Bu tipte seri numarası alanı yok.";
            }
            else
            {
                _serialColumn = ProductListRepository.ColumnNameFor(serialProperty.Id);
                TypeSerialSearchBox.IsEnabled = true;
                SerialSearchHint.Text = "";
            }

            DataTable table = ProductListRepository.GetTable(_currentType.Id, properties, includeQuantity);

            // Onay kutuları için gizli bir Evet/Hayır sütunu eklenir. Başlangıçta hepsi işaretsizdir.
            table.Columns.Add(new DataColumn(SelectedColumn, typeof(bool)));
            foreach (DataRow row in table.Rows)
            {
                row[SelectedColumn] = false;
            }

            ProductGrid.Columns.Clear();

            // İlk sütun: onay kutusu
            var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkFactory.SetBinding(CheckBox.IsCheckedProperty, new Binding(SelectedColumn)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            checkFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            ProductGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "",
                Width = new DataGridLength(36),
                CellTemplate = new DataTemplate { VisualTree = checkFactory }
            });

            ProductGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "No",
                Binding = new Binding(ProductListRepository.NoColumn),
                Width = new DataGridLength(50)
            });

            if (includeQuantity)
            {
                ProductGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Miktar",
                    Binding = new Binding(ProductListRepository.QuantityColumn),
                    Width = new DataGridLength(80)
                });
            }

            foreach (var property in properties)
            {
                ProductGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = property.Name,
                    Binding = new Binding(ProductListRepository.ColumnNameFor(property.Id)),
                    Width = new DataGridLength(140)
                });
            }

            ProductGrid.ItemsSource = table.DefaultView;

            BuildFilterPanel(table, properties);
            ApplyFilters();
        }

        // ---------- ÜRÜN DETAYI ----------

        /// <summary>
        /// Tablodaki bir ürüne çift tıklanınca ürün detay penceresini açar.
        /// Ürünün durumu değiştiyse (hurdaya taşındıysa) liste yenilenir.
        /// </summary>
        private void ProductGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_currentType == null)
            {
                return;
            }

            DataRowView rowView = ProductDetailWindow.GetRowFrom(e.OriginalSource);
            if (rowView == null)
            {
                return;
            }

            var window = new ProductDetailWindow(
                _currentType.Id,
                _currentType.Name,
                (long)rowView[ProductListRepository.IdColumn],
                false,
                ProductDetailWindow.BuildDetails(rowView, _currentProperties));

            window.Owner = this;
            window.ShowDialog();

            if (window.Changed)
            {
                LoadProducts();
            }
        }

        // ---------- SİLME VE HURDAYA TAŞIMA ----------

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            ActOnSelectedProducts(false);
        }

        private void ScrapButton_Click(object sender, RoutedEventArgs e)
        {
            ActOnSelectedProducts(true);
        }

        /// <summary>
        /// İşaretli ürünleri onay penceresinde gösterir, onaylanırsa arşive alır (silme)
        /// ya da hurdaya taşır. Filtre yüzünden ekranda görünmeyen ürünlere dokunulmaz.
        /// </summary>
        private void ActOnSelectedProducts(bool scrap)
        {
            var view = ProductGrid.ItemsSource as DataView;
            if (view == null || _currentType == null)
            {
                return;
            }

            var items = new List<DeleteItem>();

            foreach (DataRowView rowView in view)
            {
                if ((bool)rowView[SelectedColumn])
                {
                    items.Add(new DeleteItem
                    {
                        ProductId = (long)rowView[ProductListRepository.IdColumn],
                        Text = DescribeRow(rowView)
                    });
                }
            }

            if (items.Count == 0)
            {
                string warning = scrap
                    ? "Önce hurdaya taşınacak ürünlerin başındaki kutuyu işaretleyin."
                    : "Önce silinecek ürünlerin başındaki kutuyu işaretleyin.";

                MessageBox.Show(warning, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new DeleteProductsWindow(items, scrap);
            window.Owner = this;

            if (window.ShowDialog() != true)
            {
                return;
            }

            try
            {
                if (scrap)
                {
                    ProductArchiveRepository.MoveToScrap(_currentType.Id, window.SelectedIds);
                }
                else
                {
                    ProductArchiveRepository.Archive(_currentType.Id, window.SelectedIds);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("İşlem yapılamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadProducts();
        }

        /// <summary>
        /// Onay penceresinde görünecek kısa ürün yazısı. Örnek: "No 3 | Miktar: 5 | Marka: Dell".
        /// </summary>
        private string DescribeRow(DataRowView rowView)
        {
            var parts = new List<string>();
            parts.Add("No " + rowView[ProductListRepository.NoColumn]);

            if (rowView.Row.Table.Columns.Contains(ProductListRepository.QuantityColumn))
            {
                parts.Add("Miktar: " + rowView[ProductListRepository.QuantityColumn]);
            }

            int shown = 0;

            foreach (var property in _currentProperties)
            {
                if (shown == 4)
                {
                    break;
                }

                string column = ProductListRepository.ColumnNameFor(property.Id);

                if (rowView.Row.IsNull(column))
                {
                    continue;
                }

                string value = (string)rowView[column];
                if (value.Length == 0)
                {
                    continue;
                }

                parts.Add(property.Name + ": " + value);
                shown++;
            }

            return string.Join("  |  ", parts);
        }

        // ---------- FİLTRE BARI ----------

        /// <summary>
        /// Tipin alanlarından ve ürünlerdeki mevcut değerlerden filtre onay kutularını oluşturur.
        /// </summary>
        private void BuildFilterPanel(DataTable table, List<PropertyDefinition> properties)
        {
            FilterItemsPanel.Children.Clear();

            bool anyProperty = false;

            foreach (var property in properties)
            {
                // Seri No zaten arama kutusuyla aranıyor, filtrede gösterilmez.
                if (property.IsSerialNumber)
                {
                    continue;
                }

                anyProperty = true;
                string column = ProductListRepository.ColumnNameFor(property.Id);

                var distinct = new HashSet<string>();
                foreach (DataRow row in table.Rows)
                {
                    if (row.IsNull(column))
                    {
                        continue;
                    }

                    string value = (string)row[column];
                    if (value.Length > 0)
                    {
                        distinct.Add(value);
                    }
                }

                // Artık var olmayan değerler seçimden düşer (örneğin ürün silindiyse).
                HashSet<string> chosen;
                if (_selected.TryGetValue(property.Id, out chosen))
                {
                    chosen.IntersectWith(distinct);
                    if (chosen.Count == 0)
                    {
                        _selected.Remove(property.Id);
                        chosen = null;
                    }
                }
                else
                {
                    chosen = null;
                }

                var content = new StackPanel { Margin = new Thickness(4, 4, 0, 4) };

                if (distinct.Count == 0)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = "(kayıtlı değer yok)",
                        Foreground = System.Windows.Media.Brushes.Gray
                    });
                }

                foreach (string value in SortValues(distinct, property.DataType))
                {
                    var box = new CheckBox
                    {
                        Content = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap },
                        Tag = Tuple.Create(property.Id, value),
                        Margin = new Thickness(0, 2, 0, 2),
                        IsChecked = chosen != null && chosen.Contains(value)
                    };

                    box.Checked += FilterCheckBox_Changed;
                    box.Unchecked += FilterCheckBox_Changed;
                    content.Children.Add(box);
                }

                FilterItemsPanel.Children.Add(new Expander
                {
                    Header = property.Name,
                    IsExpanded = true,
                    Margin = new Thickness(0, 0, 0, 6),
                    Content = content
                });
            }

            if (!anyProperty)
            {
                FilterItemsPanel.Children.Add(new TextBlock
                {
                    Text = "Bu tipte filtrelenecek alan yok.",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        /// <summary>
        /// Değerleri alanın türüne göre sıralar: sayılar sayı gibi, tarihler tarih gibi, yazılar Türkçe alfabeyle.
        /// </summary>
        private static List<string> SortValues(HashSet<string> values, string dataType)
        {
            var list = values.ToList();
            var turkish = new CultureInfo("tr-TR");

            if (dataType == "Number")
            {
                list.Sort((a, b) =>
                {
                    double x, y;
                    bool okX = double.TryParse(a, NumberStyles.Float, turkish, out x);
                    bool okY = double.TryParse(b, NumberStyles.Float, turkish, out y);

                    if (okX && okY)
                    {
                        return x.CompareTo(y);
                    }

                    return string.Compare(a, b, turkish, CompareOptions.IgnoreCase);
                });
            }
            else if (dataType == "Date")
            {
                list.Sort((a, b) =>
                {
                    DateTime x, y;
                    bool okX = DateTime.TryParseExact(a, "dd.MM.yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out x);
                    bool okY = DateTime.TryParseExact(b, "dd.MM.yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out y);

                    if (okX && okY)
                    {
                        return x.CompareTo(y);
                    }

                    return string.Compare(a, b, turkish, CompareOptions.IgnoreCase);
                });
            }
            else
            {
                list.Sort((a, b) => string.Compare(a, b, turkish, CompareOptions.IgnoreCase));
            }

            return list;
        }

        /// <summary>
        /// Bir onay kutusu işaretlenince ya da işareti kalkınca çalışır.
        /// </summary>
        private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var box = (CheckBox)sender;
            var tag = (Tuple<long, string>)box.Tag;

            HashSet<string> set;

            if (box.IsChecked == true)
            {
                if (!_selected.TryGetValue(tag.Item1, out set))
                {
                    set = new HashSet<string>();
                    _selected[tag.Item1] = set;
                }

                set.Add(tag.Item2);
            }
            else if (_selected.TryGetValue(tag.Item1, out set))
            {
                set.Remove(tag.Item2);

                if (set.Count == 0)
                {
                    _selected.Remove(tag.Item1);
                }
            }

            ApplyFilters();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFilterBar();
        }

        private void FilterBackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNormalBar();
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            _selected.Clear();
            LoadProducts();
        }

        // ---------- ARAMA VE FİLTRELERİ UYGULAMA ----------

        private void TypeSerialSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        /// <summary>
        /// Seri No aramasını ve seçili filtrelerin hepsini birlikte uygular.
        /// Farklı alanlar "ve", aynı alandaki değerler "veya" ile birleşir.
        /// </summary>
        private void ApplyFilters()
        {
            var view = ProductGrid.ItemsSource as DataView;
            if (view == null)
            {
                return;
            }

            var conditions = new List<string>();

            string text = TypeSerialSearchBox.Text.Trim();
            if (_serialColumn != null && text.Length > 0)
            {
                conditions.Add("[" + _serialColumn + "] LIKE '%" + EscapeForLike(text) + "%'");
            }

            foreach (var pair in _selected)
            {
                var quoted = pair.Value.Select(v => "'" + v.Replace("'", "''") + "'");
                conditions.Add("[" + ProductListRepository.ColumnNameFor(pair.Key) + "] IN (" +
                               string.Join(",", quoted) + ")");
            }

            view.RowFilter = string.Join(" AND ", conditions);

            int total = view.Table.Rows.Count;

            if (conditions.Count > 0)
            {
                ProductCountText.Text = view.Count + " / " + total + " ürün";
            }
            else
            {
                ProductCountText.Text = total + " ürün";
            }

            FilterButton.Content = _selected.Count > 0
                ? "Filtrele (" + _selected.Count + ")"
                : "Filtrele";
        }

        /// <summary>
        /// Aramaya yazılan özel karakterlerin (' * % [ ]) süzme ifadesini bozmasını engeller.
        /// </summary>
        private static string EscapeForLike(string text)
        {
            var result = new StringBuilder();

            foreach (char c in text)
            {
                if (c == '\'')
                {
                    result.Append("''");
                }
                else if (c == '*' || c == '%' || c == '[' || c == ']')
                {
                    result.Append('[').Append(c).Append(']');
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        // ---------- ANA SAYFADA SERİ NO ARAMA ----------

        private void HomeSerialSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchSerialFromHome();
            }
        }

        private void HomeSerialSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchSerialFromHome();
        }

        /// <summary>
        /// Ana sayfadaki kutuya yazılan seri numarasını tüm ürün tiplerinde arar.
        /// Bulursa ürünün tipinin sayfasını açar ve seri numarasını o sayfanın arama kutusuna yazar.
        /// Birden fazla eşleşme varsa tam eşleşen öne alınır.
        /// </summary>
        private void SearchSerialFromHome()
        {
            string serial = HomeSerialSearchBox.Text.Trim();

            if (serial.Length == 0)
            {
                MessageBox.Show("Lütfen aranacak seri numarasını yazın.", "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long? typeId;

            try
            {
                typeId = SerialSearchRepository.FindTypeIdBySerial(serial);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Arama yapılamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ProductType type = null;

            if (typeId.HasValue)
            {
                type = ProductTypeRepository.GetAll().FirstOrDefault(t => t.Id == typeId.Value);
            }

            if (type == null)
            {
                MessageBox.Show("Bu seri numarasını içeren ürün bulunamadı.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            HomeSerialSearchBox.Clear();
            ShowTypePage(type);
            TypeSerialSearchBox.Text = serial;
        }

        // ---------- YEDEKLEME ----------

        /// <summary>
        /// Menüden "Yedek al" seçilince: kaydedilecek yeri sorar ve veritabanının yedeğini alır.
        /// </summary>
        private void BackupMenu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Yedek dosyasını kaydet",
                FileName = BackupService.SuggestedFileName(),
                DefaultExt = ".db",
                Filter = "Depo Stok yedeği (*.db)|*.db",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            if (SamePath(dialog.FileName, Database.DatabasePath))
            {
                MessageBox.Show(this,
                    "Yedek, programın kullandığı veritabanı dosyasının üzerine kaydedilemez.\nLütfen başka bir yer veya ad seçin.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BackupService.CreateBackup(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Yedek alınamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(this, "Yedek alındı:\n" + dialog.FileName, "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Menüden "Yedekten geri yükle" seçilince: yedek dosyasını seçtirir, denetler,
        /// onay alır ve tüm veriyi yedektekiyle değiştirir.
        /// </summary>
        private void RestoreMenu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Geri yüklenecek yedek dosyasını seçin",
                Filter = "Depo Stok yedeği (*.db)|*.db",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            if (SamePath(dialog.FileName, Database.DatabasePath))
            {
                MessageBox.Show(this,
                    "Programın şu an kullandığı veritabanı dosyasını seçemezsiniz.\nLütfen aldığınız bir yedek dosyasını seçin.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string problem = BackupService.CheckBackupFile(dialog.FileName);

            if (problem != null)
            {
                MessageBox.Show(this, problem, "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var answer = MessageBox.Show(this,
                "Şu anki tüm veriler, seçilen yedekteki verilerle değiştirilecek.\n\n" +
                "Devam etmeden önce şu anki verinin otomatik bir kopyası alınacak.\n\n" +
                "Onaylıyor musun?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            string safetyCopy;

            try
            {
                safetyCopy = BackupService.RestoreBackup(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Yedek geri yüklenemedi:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ShowHome();

            MessageBox.Show(this,
                "Yedek geri yüklendi.\n\nÖnceki verinizin kopyası:\n" + safetyCopy,
                "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// İki dosya yolu aynı dosyayı gösteriyor mu?
        /// </summary>
        private static bool SamePath(string first, string second)
        {
            return string.Equals(
                System.IO.Path.GetFullPath(first),
                System.IO.Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);
        }

        // ---------- MENÜ VE DÜĞMELER ----------

        private void HomeMenu_Click(object sender, RoutedEventArgs e)
        {
            ShowHome();
        }

        /// <summary>
        /// Menüden Ayarlar'a tıklanınca Ayarlar penceresini açar.
        /// Pencere kapanınca ekran yenilenir.
        /// </summary>
        private void SettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();

            if (_currentType == null)
            {
                ShowHome();
            }
            else
            {
                LoadProducts();
            }
        }

        /// <summary>
        /// Listede bir ürün tipine çift tıklanınca o tipin sayfasını açar.
        /// </summary>
        private void ProductTypeList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var type = ProductTypeList.SelectedItem as ProductType;
            if (type == null)
            {
                return;
            }

            ShowTypePage(type);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHome();
        }

        /// <summary>
        /// Ürün ekleme penceresini açar. Pencere kapanınca liste yenilenir.
        /// </summary>
        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentType == null)
            {
                return;
            }

            var addWindow = new AddProductWindow(_currentType);
            addWindow.Owner = this;
            addWindow.ShowDialog();

            LoadProducts();
        }
    }
}