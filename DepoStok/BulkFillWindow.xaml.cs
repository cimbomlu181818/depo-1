using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DepoStok.Data;

namespace DepoStok
{
    /// <summary>
    /// Seçilen kapsamdaki ürünlerin boş olan bir alanını tek seferde aynı değerle doldurur.
    /// Dolu hücrelere dokunulmaz.
    /// </summary>
    public partial class BulkFillWindow : Window
    {
        private readonly ProductType _type;
        private readonly List<long> _productIds;
        private readonly Func<PropertyDefinition, int> _countEmpty;

        // Değer için o an gösterilen giriş kutusu (alanın türüne göre değişir).
        private FrameworkElement _input;

        /// <summary>
        /// Pencere Doldur ile kapanınca, kaç ürünün doldurulduğu burada olur.
        /// </summary>
        public int FilledCount { get; private set; }

        /// <param name="type">Açık olan ürün tipi.</param>
        /// <param name="scopeText">Üstte görünecek kapsam yazısı. Örnek: "Kapsam: işaretli 3 ürün".</param>
        /// <param name="properties">Doldurulabilecek alanlar (seri numarası hariç).</param>
        /// <param name="productIds">Kapsamdaki ürünlerin numaraları.</param>
        /// <param name="countEmpty">Bir alanın kapsamda kaç üründe boş olduğunu sayar.</param>
        public BulkFillWindow(
            ProductType type,
            string scopeText,
            List<PropertyDefinition> properties,
            List<long> productIds,
            Func<PropertyDefinition, int> countEmpty)
        {
            InitializeComponent();

            _type = type;
            _productIds = productIds;
            _countEmpty = countEmpty;

            ScopeText.Text = scopeText;
            PropertyBox.ItemsSource = properties;

            if (properties.Count > 0)
            {
                PropertyBox.SelectedIndex = 0;
            }
        }

        private PropertyDefinition SelectedProperty
        {
            get { return PropertyBox.SelectedItem as PropertyDefinition; }
        }

        /// <summary>
        /// Alan değişince, alanın türüne uygun giriş kutusunu gösterir.
        /// </summary>
        private void PropertyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PropertyDefinition property = SelectedProperty;

            if (property == null)
            {
                return;
            }

            switch (property.DataType)
            {
                case "Date":
                    _input = new DatePicker();
                    break;

                case "YesNo":
                    _input = new CheckBox { Content = "Evet", Margin = new Thickness(0, 6, 0, 0) };
                    break;

                default:
                    _input = new TextBox
                    {
                        Height = 28,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    break;
            }

            ValueHolder.Content = _input;

            int empty = _countEmpty(property);

            if (empty == 0)
            {
                InfoText.Text = "Bu kapsamda bu alanı boş olan ürün yok.";
            }
            else
            {
                InfoText.Text = "Bu kapsamda bu alanı boş olan " + empty + " ürün var. " +
                                "Sadece onlar doldurulacak, dolu olanlara dokunulmayacak.";
            }

            FillButton.IsEnabled = empty > 0;
        }

        private void FillButton_Click(object sender, RoutedEventArgs e)
        {
            PropertyDefinition property = SelectedProperty;

            if (property == null)
            {
                ShowWarning("Önce doldurulacak alanı seçin.");
                return;
            }

            string value;
            string displayValue;

            if (!TryReadValue(property, out value, out displayValue))
            {
                return;
            }

            int empty = _countEmpty(property);

            MessageBoxResult answer = MessageBox.Show(this,
                empty + " ürünün boş \"" + property.Name + "\" alanına \"" + displayValue +
                "\" yazılacak.\nDolu olanlara dokunulmayacak.\n\nOnaylıyor musunuz?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                FilledCount = ProductRepository.FillEmptyValues(
                    _type.Id, property, value, displayValue, _productIds);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Alanlar doldurulamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
        }

        /// <summary>
        /// Giriş kutusundaki değeri okur ve denetler.
        /// value veritabanına yazılacak hâli, displayValue ise ekranda ve loga yazılacak hâli verir.
        /// Değer geçersizse uyarı gösterir ve false verir.
        /// </summary>
        private bool TryReadValue(PropertyDefinition property, out string value, out string displayValue)
        {
            value = null;
            displayValue = null;

            switch (property.DataType)
            {
                case "Date":
                    var picker = (DatePicker)_input;

                    if (picker.SelectedDate == null)
                    {
                        ShowWarning("Lütfen bir tarih seçin.");
                        return false;
                    }

                    value = picker.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    displayValue = picker.SelectedDate.Value.ToString("dd.MM.yyyy", new CultureInfo("tr-TR"));
                    return true;

                case "YesNo":
                    bool yes = ((CheckBox)_input).IsChecked == true;
                    value = yes ? "1" : "0";
                    displayValue = yes ? "Evet" : "Hayır";
                    return true;

                case "Number":
                    string numberText = ((TextBox)_input).Text.Trim();

                    if (numberText.Length == 0)
                    {
                        ShowWarning("Lütfen bir sayı yazın.");
                        return false;
                    }

                    string normalized = numberText.Replace(',', '.');
                    double number;

                    if (!double.TryParse(normalized, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out number))
                    {
                        ShowWarning("\"" + numberText + "\" geçerli bir sayı değil.");
                        return false;
                    }

                    value = normalized;
                    displayValue = numberText;
                    return true;

                default:
                    string plain = ((TextBox)_input).Text.Trim();

                    if (plain.Length == 0)
                    {
                        ShowWarning("Lütfen doldurulacak değeri yazın.");
                        return false;
                    }

                    value = plain;
                    displayValue = plain;
                    return true;
            }
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(this, message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}