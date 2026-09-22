using System;
using System.Windows;
using DepoStok.Data;

namespace DepoStok
{
    /// <summary>
    /// Bir ürünü (ya da adetli üründen bir miktarını) bir kişiye zimmetlemek için açılan pencere.
    /// Kaydet'e basınca doğrudan veritabanına yazar; başarılıysa DialogResult true olur.
    /// </summary>
    public partial class AssignWindow : Window
    {
        private readonly long _productId;
        private readonly long _productTypeId;
        private readonly int _available;

        public AssignWindow(long productId, long productTypeId, string productDescription, int availableQuantity)
        {
            InitializeComponent();

            _productId = productId;
            _productTypeId = productTypeId;
            _available = availableQuantity;

            ProductText.Text = productDescription;
            AvailableText.Text = "Zimmetlenebilir miktar: " + availableQuantity;

            QuantityBox.Text = "1";

            if (availableQuantity <= 1)
            {
                QuantityBox.IsEnabled = false;
                QuantityLabel.Foreground = System.Windows.Media.Brushes.Gray;
            }

            Loaded += (s, e) => PersonNameBox.Focus();
        }

        private void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            string personName = PersonNameBox.Text.Trim();

            if (personName.Length == 0)
            {
                ShowWarning("Kişi adı boş bırakılamaz.");
                PersonNameBox.Focus();
                return;
            }

            int quantity;
            if (!int.TryParse(QuantityBox.Text.Trim(), out quantity) || quantity < 1)
            {
                ShowWarning("Miktar 1 veya daha büyük bir tam sayı olmalı.");
                QuantityBox.Focus();
                return;
            }

            if (quantity > _available)
            {
                ShowWarning("Bu üründen zimmetlenebilecek miktar en fazla " + _available + ".");
                QuantityBox.Focus();
                return;
            }

            try
            {
                AssignmentRepository.Assign(
                    _productId,
                    _productTypeId,
                    quantity,
                    personName,
                    RegistryNoBox.Text.Trim(),
                    DepartmentBox.Text.Trim(),
                    NoteBox.Text.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Zimmetlenemedi:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(this, "Ürün zimmetlendi.", "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(this, message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}