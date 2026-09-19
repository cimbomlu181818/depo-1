using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DepoStok.Data;

namespace DepoStok
{
    public partial class ScrapWindow : Window
    {
        /// <summary>
        /// Tablodaki onay kutusu sütununun adı.
        /// </summary>
        private const string SelectedColumn = "Selected";

        /// <summary>
        /// Soldaki listede şu an seçili olan ürün cinsi.
        /// </summary>
        private ScrapTypeInfo _currentType;

        /// <summary>
        /// Seçili cinsin alanları (tablodaki sütunlar).
        /// </summary>
        private List<PropertyDefinition> _currentProperties = new List<PropertyDefinition>();

        public ScrapWindow()
        {
            InitializeComponent();
            ProductGrid.MouseDoubleClick += ProductGrid_MouseDoubleClick;
            LoadTypes();
        }

        /// <summary>
        /// Soldaki listeyi yeniler. Önceden seçili cins hâlâ hurdada ürünü varsa seçili kalır.
        /// </summary>
        private void LoadTypes()
        {
            long? previousId = _currentType == null ? (long?)null : _currentType.Id;

            var types = ProductListRepository.GetScrapTypes();
            ScrapTypeList.ItemsSource = types;

            if (types.Count == 0)
            {
                _currentType = null;
                LoadProducts();
                return;
            }

            ScrapTypeInfo toSelect = null;

            if (previousId.HasValue)
            {
                toSelect = types.FirstOrDefault(t => t.Id == previousId.Value);
            }

            if (toSelect == null)
            {
                toSelect = types[0];
            }

            ScrapTypeList.SelectedItem = toSelect;
        }

        private void ScrapTypeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentType = ScrapTypeList.SelectedItem as ScrapTypeInfo;
            LoadProducts();
        }

        /// <summary>
        /// Seçili cinsin hurda ürünlerini tabloya doldurur.
        /// </summary>
        private void LoadProducts()
        {
            ProductGrid.Columns.Clear();

            if (_currentType == null)
            {
                ProductGrid.ItemsSource = null;
                TitleText.Text = "Hurdada ürün yok.";
                CountText.Text = "";
                RestoreButton.IsEnabled = false;
                return;
            }

            RestoreButton.IsEnabled = true;
            TitleText.Text = _currentType.Name + " - hurda";

            var properties = TypePropertyRepository.GetForType(_currentType.Id);
            _currentProperties = properties;

            // Seri numarası alanı yoksa ürünler adet bazlıdır: Miktar sütunu gösterilir.
            bool includeQuantity = !properties.Any(p => p.IsSerialNumber);

            DataTable table = ProductListRepository.GetTable(
                _currentType.Id, properties, includeQuantity, true);

            // Onay kutuları için gizli bir Evet/Hayır sütunu eklenir. Başlangıçta hepsi işaretsizdir.
            table.Columns.Add(new DataColumn(SelectedColumn, typeof(bool)));
            foreach (DataRow row in table.Rows)
            {
                row[SelectedColumn] = false;
            }

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
            CountText.Text = table.Rows.Count + " ürün";
        }

        /// <summary>
        /// Tablodaki bir hurda ürüne çift tıklanınca ürün detay penceresini açar.
        /// Ürün normal listeye geri alındıysa hurda listesi yenilenir.
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
                true,
                ProductDetailWindow.BuildDetails(rowView, _currentProperties));

            window.Owner = this;
            window.ShowDialog();

            if (window.Changed)
            {
                LoadTypes();
            }
        }

        /// <summary>
        /// İşaretli ürünleri onaydan sonra hurdadan çıkarıp normal listeye geri alır.
        /// </summary>
        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var view = ProductGrid.ItemsSource as DataView;
            if (view == null || _currentType == null)
            {
                return;
            }

            var ids = new List<long>();

            foreach (DataRowView rowView in view)
            {
                if ((bool)rowView[SelectedColumn])
                {
                    ids.Add((long)rowView[ProductListRepository.IdColumn]);
                }
            }

            if (ids.Count == 0)
            {
                MessageBox.Show("Önce geri alınacak ürünlerin başındaki kutuyu işaretleyin.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var answer = MessageBox.Show(
                "Seçili " + ids.Count + " ürün hurdadan çıkarılıp normal listeye geri alınacak, onaylıyor musun?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ProductArchiveRepository.RestoreFromScrap(_currentType.Id, ids);
            }
            catch (Exception ex)
            {
                MessageBox.Show("İşlem yapılamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadTypes();
        }
    }
}