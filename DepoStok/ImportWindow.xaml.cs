using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DepoStok.Data;

namespace DepoStok
{
    public partial class ImportWindow : Window
    {
        private readonly ProductType _type;
        private readonly List<PropertyDefinition> _properties;

        private CsvImporter.Result _data;

        /// <summary>
        /// Pencere kapanınca, en az bir ürün eklendiyse true olur (çağıran taraf listeyi yeniler).
        /// </summary>
        public bool Imported { get; private set; }

        public ImportWindow(ProductType type, List<PropertyDefinition> properties)
        {
            InitializeComponent();

            _type = type;
            _properties = properties;

            SummaryText.Text =
                "\"" + type.Name + "\" tipine ürün eklemek için, daha önce dışa aktardığınız ya da " +
                "aynı sütun başlıklarına sahip bir CSV dosyası seçin.";
        }

        private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "İçe aktarılacak dosyayı seçin",
                Filter = "CSV dosyası (*.csv)|*.csv|Tüm dosyalar (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            FilePathBox.Text = dialog.FileName;
            ErrorList.ItemsSource = null;
            ImportButton.IsEnabled = false;

            try
            {
                _data = CsvImporter.Read(dialog.FileName);
            }
            catch (Exception ex)
            {
                _data = null;
                SummaryText.Text = "Dosya okunamadı:\n" + ex.Message;
                return;
            }

            DescribeMatch();
        }

        /// <summary>
        /// Dosyadaki başlıklarla alanları karşılaştırıp özet yazısını hazırlar.
        /// </summary>
        private void DescribeMatch()
        {
            if (_data.Rows.Count == 0)
            {
                SummaryText.Text = "Dosyada satır bulunamadı.";
                return;
            }

            List<string> matched = _properties
                .Where(p => _data.Headers.Any(h => string.Equals(h, p.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(p => p.Name)
                .ToList();

            List<string> missing = _properties
                .Select(p => p.Name)
                .Except(matched)
                .ToList();

            var text = "Dosyada " + _data.Rows.Count + " satır bulundu.\n" +
                        "Eşleşen alanlar: " + (matched.Count == 0 ? "yok" : string.Join(", ", matched)) + ".";

            if (missing.Count > 0)
            {
                text += "\nDosyada olmayan alanlar (boş bırakılacak): " + string.Join(", ", missing) + ".";
            }

            SummaryText.Text = text;
            ImportButton.IsEnabled = true;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_data == null)
            {
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(this,
                _data.Rows.Count + " satır işlenecek. Hatalı olanlar atlanacak, diğerleri eklenecek.\n\n" +
                "Devam etmek istiyor musunuz?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            ImportButton.IsEnabled = false;

            ImportResult result;

            try
            {
                result = ProductImportRepository.Import(
                    _type.Id, _type.Name, _properties, _data,
                    System.IO.Path.GetFileName(FilePathBox.Text));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "İçe aktarma sırasında bir hata oluştu:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ImportButton.IsEnabled = true;
                return;
            }

            if (result.SuccessCount > 0)
            {
                Imported = true;
            }

            ErrorList.ItemsSource = result.Errors;
            SummaryText.Text = result.SuccessCount + " ürün eklendi. " +
                                result.Errors.Count + " satır atlandı.";

            MessageBox.Show(this,
                result.SuccessCount + " ürün eklendi.\n" + result.Errors.Count + " satır atlandı.",
                "İçe aktarma tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = Imported;
        }
    }
}