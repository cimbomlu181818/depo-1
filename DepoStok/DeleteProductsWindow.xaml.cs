using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DepoStok
{
    /// <summary>
    /// Silme veya hurda penceresinde gösterilecek bir ürün.
    /// </summary>
    public class DeleteItem
    {
        public long ProductId { get; set; }
        public string Text { get; set; }
    }

    public partial class DeleteProductsWindow : Window
    {
        private readonly List<CheckBox> _boxes = new List<CheckBox>();
        private readonly bool _isScrap;

        /// <summary>
        /// Kullanıcı onayladığında, işareti kalkmamış ürünlerin numaraları.
        /// </summary>
        public List<long> SelectedIds { get; private set; }

        /// <summary>
        /// isScrap doğruysa pencere "hurdaya taşı" olarak, değilse "sil" olarak çalışır.
        /// </summary>
        public DeleteProductsWindow(List<DeleteItem> items, bool isScrap)
        {
            InitializeComponent();

            _isScrap = isScrap;
            SelectedIds = new List<long>();

            if (_isScrap)
            {
                Title = "Ürünleri hurdaya taşı";
                ConfirmButton.Content = "Hurdaya Taşı";
                HintText.Text = "Hurdaya taşımak istemediğiniz ürünün işaretini kaldırabilirsiniz. " +
                                "Ürünler hurda bölümüne taşınır, kopyalanmaz.";
            }
            else
            {
                Title = "Ürünleri sil";
                ConfirmButton.Content = "Sil";
                HintText.Text = "Silmek istemediğiniz ürünün işaretini kaldırabilirsiniz. " +
                                "Silinen ürünler arşive alınır, veri yok olmaz.";
            }

            foreach (var item in items)
            {
                var box = new CheckBox
                {
                    Content = new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap },
                    Tag = item.ProductId,
                    IsChecked = true,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                box.Checked += Box_Changed;
                box.Unchecked += Box_Changed;

                _boxes.Add(box);
                ItemsPanel.Children.Add(box);
            }

            UpdateMessage();
        }

        private void Box_Changed(object sender, RoutedEventArgs e)
        {
            UpdateMessage();
        }

        /// <summary>
        /// Üstteki onay yazısını ve onay düğmesini işaretli ürün sayısına göre günceller.
        /// </summary>
        private void UpdateMessage()
        {
            int count = _boxes.Count(b => b.IsChecked == true);

            if (count == 0)
            {
                MessageText.Text = _isScrap
                    ? "Hurdaya taşınacak ürün seçilmedi."
                    : "Silinecek ürün seçilmedi.";
            }
            else
            {
                MessageText.Text = _isScrap
                    ? "Seçili " + count + " ürün hurdaya taşınacak, onaylıyor musun?"
                    : "Seçili " + count + " ürün silinecek, onaylıyor musun?";
            }

            ConfirmButton.IsEnabled = count > 0;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedIds = _boxes
                .Where(b => b.IsChecked == true)
                .Select(b => (long)b.Tag)
                .ToList();

            DialogResult = true;
        }
    }
}