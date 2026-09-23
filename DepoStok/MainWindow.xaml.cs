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
using System.Windows.Documents;
using System.Windows.Media;
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
        private readonly Dictionary<string, PropertyDefinition> _columnProperties =
            new Dictionary<string, PropertyDefinition>();

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
            LoadRecentProducts();
        }

        /// <summary>
        /// Ana sayfanın ortasındaki "Son işlem yapılan cihazlar" tablosunu doldurur.
        /// </summary>
        private void LoadRecentProducts()
        {
            var recent = RecentProductsRepository.GetRecent(20);

            RecentGrid.ItemsSource = recent;
            RecentEmptyText.Visibility = recent.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                Width = new DataGridLength(50),
                IsReadOnly = true
            });

            _columnProperties.Clear();

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
                string columnName = ProductListRepository.ColumnNameFor(property.Id);
                _columnProperties[columnName] = property;

                ProductGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = property.Name,
                    Binding = new Binding(columnName),
                    Width = new DataGridLength(140),
                    CellStyle = CreateEmptyCellStyle(columnName)
                });
            }

            ProductGrid.ItemsSource = table.DefaultView;

            BuildFilterPanel(table, properties);
            ApplyFilters();
        }

        /// <summary>
        /// Boş hücreleri açık sarıya boyayan bir hücre stili oluşturur.
        /// Sonradan eklenen bir alanın eski ürünlerde boş kaldığı yerler böylece kolayca görülür.
        /// </summary>
        private static Style CreateEmptyCellStyle(string columnName)
        {
            var style = new Style(typeof(DataGridCell));

            var emptyTrigger = new DataTrigger
            {
                Binding = new Binding(columnName) { Converter = new EmptyToBooleanConverter() },
                Value = true
            };
            emptyTrigger.Setters.Add(new Setter(
                Control.BackgroundProperty,
                new SolidColorBrush(Color.FromRgb(255, 248, 210))));
            style.Triggers.Add(emptyTrigger);

            // Satır seçilince normal seçim rengi görünsün.
            var selectedTrigger = new Trigger
            {
                Property = DataGridCell.IsSelectedProperty,
                Value = true
            };
            selectedTrigger.Setters.Add(new Setter(
                Control.BackgroundProperty, SystemColors.HighlightBrush));
            selectedTrigger.Setters.Add(new Setter(
                Control.ForegroundProperty, SystemColors.HighlightTextBrush));
            style.Triggers.Add(selectedTrigger);

            return style;
        }

        /// <summary>
        /// Hücre boşsa (hiç değer yoksa ya da yazı boşsa) "doğru" verir.
        /// </summary>
        private class EmptyToBooleanConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null || value == DBNull.Value)
                {
                    return true;
                }

                var text = value as string;
                return text != null && text.Length == 0;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
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

        // ---------- İŞLEM GEÇMİŞİ ----------

        /// <summary>
        /// Menüden İşlem Geçmişi'ne tıklanınca işlem geçmişi penceresini açar.
        /// </summary>
        private void HistoryMenu_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new HistoryWindow();
            historyWindow.Owner = this;
            historyWindow.ShowDialog();
        }

        // ---------- ARŞİV ----------

        /// <summary>
        /// Menüden Arşiv'e tıklanınca silinen ürünlerin penceresini açar.
        /// Pencere kapanınca, geri alınan ürünler görünsün diye açık sayfa yenilenir.
        /// </summary>
        private void ArchiveMenu_Click(object sender, RoutedEventArgs e)
        {
            var archiveWindow = new ArchiveWindow();
            archiveWindow.Owner = this;
            archiveWindow.ShowDialog();

            RefreshCurrentTypeAndPage();
        }

        // ---------- RAPORLAR ----------

        /// <summary>
        /// Menüden Raporlar'a tıklanınca özet rapor penceresini açar.
        /// </summary>
        private void ReportMenu_Click(object sender, RoutedEventArgs e)
        {
            var reportWindow = new ReportWindow();
            reportWindow.Owner = this;
            reportWindow.ShowDialog();
        }

        // ---------- ZİMMETLER ----------

        /// <summary>
        /// Menüden Zimmetler'e tıklanınca kimde ne var listesini açar.
        /// </summary>
        private void AssignmentsMenu_Click(object sender, RoutedEventArgs e)
        {
            var assignmentsWindow = new AssignmentsWindow();
            assignmentsWindow.Owner = this;
            assignmentsWindow.ShowDialog();

            RefreshCurrentTypeAndPage();
        }
        private void HandoverMenu_Click(object sender, RoutedEventArgs e)
        {
            var handoverWindow = new HandoverWindow();
            handoverWindow.Owner = this;
            handoverWindow.ShowDialog();
        }
        // ---------- HÜCREYİ DOĞRUDAN DÜZENLEME ----------

        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        /// <summary>
        /// Excel görünümünde bir hücrenin düzenlenmesi bitince çalışır.
        /// Değer geçerliyse anında veritabanına kaydeder ve hücreyi düzgün biçimde gösterir.
        /// Geçersizse düzenlemeyi iptal edip eski değere döner ve nedenini söyler.
        /// </summary>
        private void ProductGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            var column = e.Column as DataGridBoundColumn;
            var textBox = e.EditingElement as TextBox;

            if (column == null || textBox == null)
            {
                return;
            }

            string columnName = ((Binding)column.Binding).Path.Path;
            var rowView = (DataRowView)e.Row.Item;
            long productId = (long)rowView[ProductListRepository.IdColumn];

            string oldText = Convert.ToString(rowView[columnName]) ?? "";
            string newText = textBox.Text.Trim();

            if (newText == oldText)
            {
                return;
            }

            if (columnName == ProductListRepository.QuantityColumn)
            {
                SaveQuantityEdit(e, rowView, productId, oldText, newText);
                return;
            }

            PropertyDefinition property;
            if (!_columnProperties.TryGetValue(columnName, out property))
            {
                return;
            }

            SavePropertyEdit(e, rowView, productId, columnName, property, oldText, newText);
        }

        /// <summary>
        /// "Miktar" hücresi düzenlenince çalışır.
        /// </summary>
        private void SaveQuantityEdit(
            DataGridCellEditEndingEventArgs e, DataRowView rowView, long productId,
            string oldText, string newText)
        {
            int parsed;

            if (!int.TryParse(newText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                || parsed < 1)
            {
                e.Cancel = true;
                ShowCellWarning("Miktar 1 veya daha büyük bir tam sayı olmalı.");
                return;
            }

            try
            {
                int oldQuantity = ProductRepository.GetQuantity(productId);
                ProductRepository.Update(
                    _currentType.Id, productId, oldQuantity, parsed, new List<ProductChange>());
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                ShowCellWarning("Kaydedilemedi:\n" + ex.Message);
                return;
            }

            rowView[ProductListRepository.QuantityColumn] = parsed;
        }

        /// <summary>
        /// Bir alan hücresi (Metin, Sayı, Tarih, Evet/Hayır veya Seri No) düzenlenince çalışır.
        /// </summary>
        private void SavePropertyEdit(
            DataGridCellEditEndingEventArgs e, DataRowView rowView, long productId,
            string columnName, PropertyDefinition property,
            string oldText, string newText)
        {
            string storageValue;
            string displayValue;

            if (!TryConvertCellValue(property, newText, out storageValue, out displayValue))
            {
                e.Cancel = true;
                ShowCellWarning(CellConversionError);
                return;
            }

            if (property.IsSerialNumber)
            {
                if (storageValue.Length == 0 && oldText.Length > 0)
                {
                    e.Cancel = true;
                    ShowCellWarning("Seri numarası dolu olan bir üründe boşaltılamaz.");
                    return;
                }

                if (storageValue.Length > 0 &&
                    ProductRepository.SerialNumberExists(property.Id, storageValue, productId))
                {
                    e.Cancel = true;
                    ShowCellWarning("Bu seri numarası başka bir üründe kayıtlı.");
                    return;
                }
            }

            var change = new ProductChange
            {
                Property = property,
                OldValue = ConvertOldTextToStorage(property, oldText),
                NewValue = storageValue
            };

            try
            {
                int oldQuantity = ProductRepository.GetQuantity(productId);
                ProductRepository.Update(
                    _currentType.Id, productId, oldQuantity, null,
                    new List<ProductChange> { change });
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                ShowCellWarning("Kaydedilemedi:\n" + ex.Message);
                return;
            }

            rowView[columnName] = displayValue;
        }

        /// <summary>
        /// Ekranda gösterilen eski yazıyı (örn. "12,5" ya da "Evet"), veritabanında tutulan
        /// biçime çevirir (örn. "12.5" ya da "1"). Sadece değişikliği loga doğru yazmak için kullanılır.
        /// </summary>
        private string ConvertOldTextToStorage(PropertyDefinition property, string oldText)
        {
            string storage;
            string display;

            if (oldText.Length > 0 && TryConvertCellValue(property, oldText, out storage, out display))
            {
                return storage;
            }

            return oldText;
        }

        private string CellConversionError = "";

        /// <summary>
        /// Kullanıcının hücreye yazdığı yazıyı alanın türüne göre denetler ve çevirir.
        /// Başarısızsa false verir ve CellConversionError alanına nedenini yazar.
        /// </summary>
        private bool TryConvertCellValue(
            PropertyDefinition property, string text, out string storageValue, out string displayValue)
        {
            storageValue = "";
            displayValue = "";

            if (text.Length == 0)
            {
                return true;
            }

            switch (property.DataType)
            {
                case "Number":
                    double number;
                    string normalized = text.Replace(',', '.');

                    if (!double.TryParse(normalized, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out number))
                    {
                        CellConversionError = "\"" + text + "\" geçerli bir sayı değil.";
                        return false;
                    }

                    storageValue = normalized;
                    displayValue = number.ToString("0.######", Turkish);
                    return true;

                case "Date":
                    DateTime date;

                    if (!DateTime.TryParseExact(text, "dd.MM.yyyy", Turkish,
                        DateTimeStyles.None, out date))
                    {
                        CellConversionError =
                            "\"" + text + "\" geçerli bir tarih değil. Örnek: 20.09.2026";
                        return false;
                    }

                    storageValue = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    displayValue = date.ToString("dd.MM.yyyy", Turkish);
                    return true;

                case "YesNo":
                    string lower = text.ToLower(Turkish);

                    if (lower == "evet")
                    {
                        storageValue = "1";
                        displayValue = "Evet";
                        return true;
                    }

                    if (lower == "hayır" || lower == "hayir")
                    {
                        storageValue = "0";
                        displayValue = "Hayır";
                        return true;
                    }

                    CellConversionError = "Bu alana yalnızca \"Evet\" veya \"Hayır\" yazılabilir.";
                    return false;

                default:
                    storageValue = text;
                    displayValue = text;
                    return true;
            }
        }

        private void ShowCellWarning(string message)
        {
            MessageBox.Show(this, message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Bir CSV dosyasından bu ürün tipine toplu ürün ekler.
        /// </summary>
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentType == null)
            {
                return;
            }

            var window = new ImportWindow(_currentType, _currentProperties);
            window.Owner = this;
            window.ShowDialog();

            if (window.Imported)
            {
                LoadProducts();
            }
        }

        /// <summary>
        /// Ürün tipi sayfasındaki alan sütunlarının sırasını değiştirmek için pencereyi açar.
        /// </summary>
        private void ColumnOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentType == null)
            {
                return;
            }

            if (_currentProperties.Count < 2)
            {
                MessageBox.Show(this, "Sırası değiştirilecek en az iki alan olmalı.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new ColumnOrderWindow(_currentType, _currentProperties);
            window.Owner = this;
            window.ShowDialog();

            if (window.Saved)
            {
                LoadProducts();
            }
        }

        // ---------- BOŞ ALANLARI TOPLU DOLDURMA ----------

        /// <summary>
        /// "Boş Alanları Doldur" düğmesi: kapsamdaki ürünlerin boş bir alanını aynı değerle doldurur.
        /// Kapsam: onay kutusu işaretli ürünler varsa onlar, yoksa ekranda görünen (filtrelenmiş) tüm ürünler.
        /// </summary>
        private void BulkFillButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentType == null)
            {
                return;
            }

            var view = ProductGrid.ItemsSource as DataView;

            if (view == null || view.Count == 0)
            {
                MessageBox.Show(this, "Listede ürün yok.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Seri numarası alanı toplu doldurulamaz, çünkü her ürünün seri numarası farklı olmalı.
            List<PropertyDefinition> fillable = _currentProperties
                .Where(p => !p.IsSerialNumber)
                .ToList();

            if (fillable.Count == 0)
            {
                MessageBox.Show(this, "Bu tipte toplu doldurulabilecek alan yok.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var visibleRows = new List<DataRowView>();
            var checkedRows = new List<DataRowView>();

            foreach (DataRowView rowView in view)
            {
                visibleRows.Add(rowView);

                if ((bool)rowView[SelectedColumn])
                {
                    checkedRows.Add(rowView);
                }
            }

            List<DataRowView> scope = checkedRows.Count > 0 ? checkedRows : visibleRows;

            string scopeText = checkedRows.Count > 0
                ? "Kapsam: işaretli " + checkedRows.Count + " ürün"
                : "Kapsam: ekranda görünen " + visibleRows.Count + " ürün (işaretli ürün yok)";

            List<long> productIds = scope
                .Select(r => (long)r[ProductListRepository.IdColumn])
                .ToList();

            // Bir alanın kapsamda kaç üründe boş olduğunu sayar.
            Func<PropertyDefinition, int> countEmpty = property =>
            {
                string column = ProductListRepository.ColumnNameFor(property.Id);
                return scope.Count(r => string.IsNullOrEmpty(Convert.ToString(r[column])));
            };

            var window = new BulkFillWindow(_currentType, scopeText, fillable, productIds, countEmpty);
            window.Owner = this;

            if (window.ShowDialog() == true)
            {
                LoadProducts();

                MessageBox.Show(this,
                    window.FilledCount + " ürünün boş alanı dolduruldu.",
                    "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ---------- YAZDIRMA ----------

        /// <summary>
        /// Ürün tipi sayfasında şu an ekranda görünen ürünleri (filtre varsa filtrelenmiş halini)
        /// bir tablo olarak yazıcıya gönderir.
        /// </summary>
        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentType == null)
            {
                return;
            }

            var view = ProductGrid.ItemsSource as DataView;

            if (view == null || view.Count == 0)
            {
                MessageBox.Show(this, "Yazdırılacak ürün yok.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var headers = new List<string>();
            var paths = new List<string>();

            foreach (DataGridBoundColumn column in ProductGrid.Columns.OfType<DataGridBoundColumn>())
            {
                var binding = column.Binding as Binding;

                if (binding == null)
                {
                    continue;
                }

                headers.Add(Convert.ToString(column.Header));
                paths.Add(binding.Path.Path);
            }

            var printDialog = new System.Windows.Controls.PrintDialog();

            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            FlowDocument document = BuildPrintDocument(headers, paths, view, printDialog.PrintableAreaWidth);
            IDocumentPaginatorSource paginatorSource = document;

            try
            {
                printDialog.PrintDocument(paginatorSource.DocumentPaginator, _currentType.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Yazdırılamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                LogRepository.Write("Yazdırıldı",
                    "Ürün tipi: " + _currentType.Name + ", " + view.Count + " ürün");
            }
            catch (Exception)
            {
                // Yazdırma zaten tamamlandı; log yazılamaması yazdırmayı engellemez.
            }
        }

        /// <summary>
        /// Başlık, tarih ve bir tablodan oluşan basit bir yazdırma sayfası hazırlar.
        /// </summary>
        private FlowDocument BuildPrintDocument(
            List<string> headers, List<string> paths, DataView view, double pageWidth)
        {
            var document = new FlowDocument
            {
                PageWidth = pageWidth > 0 ? pageWidth : 750,
                FontSize = 11,
                PagePadding = new Thickness(20)
            };

            document.Blocks.Add(new Paragraph(new Run(_currentType.Name))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            document.Blocks.Add(new Paragraph(new Run(
                "Yazdırma tarihi: " +
                DateTime.Now.ToString("dd.MM.yyyy HH:mm", Turkish) +
                "  —  " + view.Count + " ürün"))
            {
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var table = new Table();

            for (int i = 0; i < headers.Count; i++)
            {
                table.Columns.Add(new TableColumn());
            }

            var rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);

            var headerRow = new TableRow { Background = Brushes.LightGray };
            foreach (string header in headers)
            {
                headerRow.Cells.Add(MakePrintCell(header, true));
            }
            rowGroup.Rows.Add(headerRow);

            foreach (DataRowView rowView in view)
            {
                var row = new TableRow();

                foreach (string path in paths)
                {
                    object value = rowView[path];
                    string text = value == null || value == DBNull.Value ? "" : Convert.ToString(value);
                    row.Cells.Add(MakePrintCell(text, false));
                }

                rowGroup.Rows.Add(row);
            }

            document.Blocks.Add(table);
            return document;
        }

        private static TableCell MakePrintCell(string text, bool bold)
        {
            var paragraph = new Paragraph(new Run(text)) { Margin = new Thickness(0) };

            if (bold)
            {
                paragraph.FontWeight = FontWeights.Bold;
            }

            return new TableCell(paragraph)
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 3, 4, 3)
            };
        }

        // ---------- DIŞA AKTARMA ----------

        /// <summary>
        /// Ürün tipi sayfasında şu an ekranda görünen ürünleri (filtre varsa filtrelenmiş halini)
        /// Excel'de açılabilen bir CSV dosyasına yazar.
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentType == null)
            {
                return;
            }

            var view = ProductGrid.ItemsSource as DataView;

            if (view == null || view.Count == 0)
            {
                MessageBox.Show(this, "Dışa aktarılacak ürün yok.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Onay kutusu sütunu hariç, tablodaki sütunların başlıklarını ve veri adlarını topla.
            var headers = new List<string>();
            var paths = new List<string>();

            foreach (DataGridBoundColumn column in ProductGrid.Columns.OfType<DataGridBoundColumn>())
            {
                var binding = column.Binding as Binding;

                if (binding == null)
                {
                    continue;
                }

                headers.Add(Convert.ToString(column.Header));
                paths.Add(binding.Path.Path);
            }

            var rows = new List<IList<string>>();

            foreach (DataRowView rowView in view)
            {
                var cells = new List<string>();

                foreach (string path in paths)
                {
                    object value = rowView[path];
                    cells.Add(value == null || value == DBNull.Value ? "" : Convert.ToString(value));
                }

                rows.Add(cells);
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Dışa aktarılacak dosyayı kaydet",
                FileName = MakeSafeFileName(_currentType.Name) + "-" +
                           DateTime.Now.ToString("yyyy-MM-dd") + ".csv",
                DefaultExt = ".csv",
                Filter = "Excel için CSV dosyası (*.csv)|*.csv",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                CsvExporter.Write(dialog.FileName, headers, rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Dosya yazılamadı:\n" + ex.Message +
                    "\n\nDosya Excel'de açıksa önce onu kapatın.",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string logWarning = "";

            try
            {
                LogRepository.Write("Dışa aktarıldı",
                    "Ürün tipi: " + _currentType.Name + ", " + rows.Count + " ürün, dosya: " +
                    System.IO.Path.GetFileName(dialog.FileName));
            }
            catch (Exception ex)
            {
                logWarning = "\n\n(İşlem loguna yazılamadı: " + ex.Message + ")";
            }

            MessageBox.Show(this,
                rows.Count + " ürün dışa aktarıldı:\n" + dialog.FileName + logWarning,
                "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Dosya adında kullanılamayan karakterleri "_" ile değiştirir.
        /// </summary>
        private static string MakeSafeFileName(string name)
        {
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
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

            RefreshCurrentTypeAndPage();
        }

        /// <summary>
        /// Ayarlar veya Arşiv penceresi kapanınca çağrılır. Açık olan ürün tipi hâlâ varsa
        /// adını ve tablosunu tazeler (ad değişmiş olabilir); tip arşivlendiyse ana sayfaya döner.
        /// </summary>
        private void RefreshCurrentTypeAndPage()
        {
            if (_currentType == null)
            {
                ShowHome();
                return;
            }

            ProductType refreshed = ProductTypeRepository.GetAll()
                .FirstOrDefault(t => t.Id == _currentType.Id);

            if (refreshed == null)
            {
                ShowHome();
                return;
            }

            _currentType = refreshed;
            TypePageTitle.Text = refreshed.Name;
            LoadProducts();
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
/**/
            var addWindow = new AddProductWindow(_currentType);
            addWindow.Owner = this;
            addWindow.ShowDialog();

            LoadProducts();
        }
    }
}/**/