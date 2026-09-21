using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DepoStok.Data;

namespace DepoStok
{
    public partial class ArchiveWindow : Window
    {
        /// <summary>
        /// Ekrana en fazla bu kadar silinmiş ürün yüklenir (en son silinenden başlayarak).
        /// </summary>
        private const int MaxRows = 500;

        private List<RecentProductInfo> _items = new List<RecentProductInfo>();

        public ArchiveWindow()
        {
            InitializeComponent();
            LoadArchive();
        }

        /// <summary>
        /// Silinmiş ürünleri veritabanından okuyup tabloya doldurur.
        /// </summary>
        private void LoadArchive()
        {
            try
            {
                _items = RecentProductsRepository.GetArchived(MaxRows);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Arşiv okunamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _items = new List<RecentProductInfo>();
            }

            ArchiveGrid.ItemsSource = _items;
            RestoreButton.IsEnabled = _items.Count > 0;

            if (_items.Count == 0)
            {
                CountText.Text = "Arşivde ürün yok.";
            }
            else if (_items.Count >= MaxRows)
            {
                CountText.Text = "Arşivde çok ürün var, son " + MaxRows + " tanesi gösteriliyor.";
            }
            else
            {
                CountText.Text = "Arşivde " + _items.Count + " ürün var.";
            }
        }

        /// <summary>
        /// Kutusu işaretli ürünleri arşivden geri alır.
        /// </summary>
        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            List<RecentProductInfo> selected = _items.Where(i => i.IsChecked).ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this,
                    "Lütfen geri almak istediğiniz ürünlerin kutusunu işaretleyin.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult answer = MessageBox.Show(this,
                selected.Count + " ürün arşivden geri alınacak. Onaylıyor musunuz?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                foreach (var group in selected.GroupBy(i => i.ProductTypeId))
                {
                    List<long> ids = group.Select(i => i.ProductId).ToList();
                    ProductArchiveRepository.RestoreFromArchive(group.Key, ids);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Ürünler geri alınamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LoadArchive();
                return;
            }

            LoadArchive();

            MessageBox.Show(this, selected.Count + " ürün geri alındı.", "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}