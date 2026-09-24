using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DepoStok.Data;

namespace DepoStok
{
    public partial class ProductDetailWindow : Window
    {
        private readonly long _typeId;
        private readonly long _productId;
        private readonly bool _isScrap;
        private readonly List<KeyValuePair<string, string>> _details;

        // Düzenleme modunda kullanılanlar
        private List<PropertyDefinition> _properties;
        private PropertyDefinition _serialProperty;
        private Dictionary<long, string> _oldValues;
        private int _oldQuantity;
        private readonly Dictionary<long, Control> _inputs = new Dictionary<long, Control>();
        private TextBox _quantityBox;

        /// <summary>
        /// Pencerede ürün değiştiyse (hurdaya taşındı, geri alındı ya da düzenlendi) doğru olur.
        /// Pencereyi açan ekran, bunu görünce listesini yeniler.
        /// </summary>
        public bool Changed { get; private set; }

        /// <summary>
        /// details: ekranda gösterilecek "başlık - değer" çiftleri.
        /// isScrap: ürün şu an hurdada mı?
        /// </summary>
        public ProductDetailWindow(
            long typeId,
            string typeName,
            long productId,
            bool isScrap,
            List<KeyValuePair<string, string>> details)
        {
            InitializeComponent();

            _typeId = typeId;
            _productId = productId;
            _isScrap = isScrap;
            _details = details;

            Title = "Ürün detayı - " + typeName;
            TitleText.Text = typeName;
            ScrapCheck.IsChecked = isScrap;

            BuildViewMode();
            LoadAssignments();
        }

        // ---------- ZİMMET DURUMU ----------

        /// <summary>
        /// Bu ürünün şu an üzerinde duran zimmetlerini okuyup listeyi tazeler.
        /// </summary>
        private void LoadAssignments()
        {
            AssignmentsPanel.Children.Clear();

            List<Assignment> active;
            int available;

            try
            {
                active = AssignmentRepository.GetActiveForProduct(_productId);
                available = AssignmentRepository.GetAvailableQuantity(_productId);
            }
            catch (Exception ex)
            {
                ShowError("Zimmet bilgisi okunamadı:\n" + ex.Message);
                return;
            }

            NoAssignmentText.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AssignButton.IsEnabled = available > 0;
            AssignButton.Content = active.Count > 0 ? "Ayrıca Zimmetle" : "Zimmetle";

            foreach (var assignment in active)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string info = assignment.PersonName;

                if (!string.IsNullOrEmpty(assignment.RegistryNo) || !string.IsNullOrEmpty(assignment.Department))
                {
                    info += " (" +
                        string.Join(", ", new[] { assignment.RegistryNo, assignment.Department }
                            .Where(s => !string.IsNullOrEmpty(s))) + ")";
                }

                info += " — miktar: " + assignment.Quantity + " — " + assignment.AssignedAtText;

                var text = new TextBlock
                {
                    Text = info,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var buttonsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(8, 0, 0, 0)
                };

                var printButton = new Button
                {
                    Content = "Yazdır",
                    Width = 70,
                    Height = 26,
                    Margin = new Thickness(0, 0, 6, 0)
                };

                var returnButton = new Button
                {
                    Content = "İade Al",
                    Width = 80,
                    Height = 26
                };

                Assignment current = assignment;
                printButton.Click += (s, e) => AssignmentReceiptPrinter.Print(this, current);

                long assignmentId = assignment.Id;
                returnButton.Click += (s, e) => ReturnAssignment(assignmentId);

                buttonsPanel.Children.Add(printButton);
                buttonsPanel.Children.Add(returnButton);
                Grid.SetColumn(buttonsPanel, 1);

                row.Children.Add(text);
                row.Children.Add(buttonsPanel);
                AssignmentsPanel.Children.Add(row);
            }
        }

        private void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            int available;

            try
            {
                available = AssignmentRepository.GetAvailableQuantity(_productId);
            }
            catch (Exception ex)
            {
                ShowError("Zimmet bilgisi okunamadı:\n" + ex.Message);
                return;
            }

            if (available <= 0)
            {
                ShowWarning("Bu ürünün zimmetlenebilecek miktarı kalmadı.");
                return;
            }

            string serialNo = _details.FirstOrDefault(
                d => d.Key == PropertyDefinitionRepository.SerialNumberFieldName).Value;
            string systemName = PropertyDefinitionRepository.GetSystemName(_productId, TitleText.Text);
            string description = systemName + (serialNo != null ? " (Seri Numara: " + serialNo + ")" : "");

            var window = new AssignWindow(_productId, _typeId, description, available);
            window.Owner = this;

            if (window.ShowDialog() == true)
            {
                Changed = true;
                LoadAssignments();
            }
        }

        private void HandoverButton_Click(object sender, RoutedEventArgs e)
        {
            string serialNo = _details.FirstOrDefault(
                d => d.Key == PropertyDefinitionRepository.SerialNumberFieldName).Value;
            string systemName = PropertyDefinitionRepository.GetSystemName(_productId, TitleText.Text);

            var window = new HandoverWindow(serialNo, systemName);
            window.Owner = this;
            window.ShowDialog();
        }

        private void ReturnAssignment(long assignmentId)
        {
            var answer = MessageBox.Show(this, "Bu zimmet iade alınacak, onaylıyor musun?", "Onay",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                AssignmentRepository.Return(assignmentId, null);
            }
            catch (Exception ex)
            {
                ShowError("İade alınamadı:\n" + ex.Message);
                return;
            }

            Changed = true;
            LoadAssignments();
        }

        // ---------- GÖRÜNTÜLEME MODU ----------

        /// <summary>
        /// Ürünün bilgilerini sadece okunur olarak gösterir.
        /// </summary>
        private void BuildViewMode()
        {
            StatusText.Text = _isScrap ? "Bu ürün hurdada." : "Normal ürün.";
            DetailsPanel.Children.Clear();

            foreach (var item in _details)
            {
                bool empty = string.IsNullOrEmpty(item.Value);

                AddRow(item.Key, new TextBlock
                {
                    Text = empty ? "(boş)" : item.Value,
                    Foreground = empty ? Brushes.Gray : Brushes.Black,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            SetEditMode(false);
        }

        /// <summary>
        /// Solda başlık, sağda değer (ya da giriş kutusu) olan bir satır ekler.
        /// </summary>
        private void AddRow(string title, FrameworkElement content)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            content.Margin = new Thickness(8, 0, 0, 0);
            content.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(content, 1);

            row.Children.Add(label);
            row.Children.Add(content);
            DetailsPanel.Children.Add(row);
        }

        /// <summary>
        /// Düzenleme moduna göre düğmeleri gösterir ya da gizler.
        /// </summary>
        private void SetEditMode(bool editing)
        {
            EditButton.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
            CloseButton.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
            SaveButton.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            CancelEditButton.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            ScrapCheck.IsEnabled = !editing;
            AssignButton.IsEnabled = !editing && AssignmentRepository.GetAvailableQuantity(_productId) > 0;
            AssignmentsPanel.IsEnabled = !editing;

            if (editing)
            {
                StatusText.Text = "Düzenleme modu: değerleri değiştirip Kaydet'e basın.";
            }
        }

        // ---------- DÜZENLEME MODU ----------

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _properties = TypePropertyRepository.GetForType(_typeId);
                _serialProperty = _properties.FirstOrDefault(p => p.IsSerialNumber);
                _oldValues = ProductRepository.GetValues(_productId);
                _oldQuantity = ProductRepository.GetQuantity(_productId);
            }
            catch (Exception ex)
            {
                ShowError("Ürün bilgileri okunamadı:\n" + ex.Message);
                return;
            }

            BuildEditMode();
            SetEditMode(true);
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            BuildViewMode();
        }

        /// <summary>
        /// Ürünün mevcut değerleriyle dolu giriş kutularını oluşturur.
        /// </summary>
        private void BuildEditMode()
        {
            DetailsPanel.Children.Clear();
            _inputs.Clear();
            _quantityBox = null;

            var turkish = new CultureInfo("tr-TR");

            // Sıra no sadece gösterilir, değiştirilemez.
            var numberRow = _details.FirstOrDefault(d => d.Key == "Sıra no");
            if (numberRow.Key != null)
            {
                AddRow(numberRow.Key, new TextBlock { Text = numberRow.Value });
            }

            Control firstInput = null;

            // Seri numarası alanı yoksa ürün adet bazlıdır: Miktar düzenlenebilir.
            if (_serialProperty == null)
            {
                _quantityBox = new TextBox
                {
                    Text = _oldQuantity.ToString(CultureInfo.InvariantCulture),
                    Height = 28,
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                AddRow("Miktar", _quantityBox);
                firstInput = _quantityBox;
            }

            foreach (var property in _properties)
            {
                string raw;
                if (!_oldValues.TryGetValue(property.Id, out raw))
                {
                    raw = "";
                }

                Control input;

                switch (property.DataType)
                {
                    case "Date":
                        {
                            var picker = new DatePicker();
                            DateTime date;

                            if (DateTime.TryParseExact(raw, "yyyy-MM-dd",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                            {
                                picker.SelectedDate = date;
                            }

                            input = picker;
                            break;
                        }

                    case "YesNo":
                        {
                            input = new CheckBox { Content = "Evet", IsChecked = raw == "1" };
                            break;
                        }

                    case "Number":
                        {
                            string text = raw;
                            double number;

                            if (raw.Length > 0 &&
                                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                            {
                                text = number.ToString("0.######", turkish);
                            }

                            input = new TextBox
                            {
                                Text = text,
                                Height = 28,
                                VerticalContentAlignment = VerticalAlignment.Center
                            };
                            break;
                        }

                    default:
                        {
                            input = new TextBox
                            {
                                Text = raw,
                                Height = 28,
                                VerticalContentAlignment = VerticalAlignment.Center
                            };
                            break;
                        }
                }

                // Seri numarası doluysa boşaltılamaz: başlığına * konur.
                string title = property.Name + (property.IsSerialNumber && raw.Length > 0 ? " *" : "");

                AddRow(title, input);
                _inputs[property.Id] = input;

                if (firstInput == null)
                {
                    firstInput = input;
                }
            }

            if (firstInput != null)
            {
                Control toFocus = firstInput;
                Dispatcher.BeginInvoke(new Action(() => toFocus.Focus()));
            }
        }

        /// <summary>
        /// Kaydet: girilen değerleri denetler, değişenleri kaydeder ve pencereyi kapatır.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            int? newQuantity = null;

            if (_quantityBox != null)
            {
                int parsed;
                if (!int.TryParse(_quantityBox.Text.Trim(), out parsed) || parsed < 1)
                {
                    ShowWarning("Miktar 1 veya daha büyük bir tam sayı olmalı.");
                    _quantityBox.Focus();
                    return;
                }

                newQuantity = parsed;
            }

            // Kutulardaki değerleri, kaydedilecek biçimde topla.
            var values = new Dictionary<long, string>();

            foreach (var property in _properties)
            {
                Control input = _inputs[property.Id];
                string value = "";

                if (property.DataType == "YesNo")
                {
                    value = ((CheckBox)input).IsChecked == true ? "1" : "0";
                }
                else if (property.DataType == "Date")
                {
                    var picker = (DatePicker)input;
                    if (picker.SelectedDate.HasValue)
                    {
                        value = picker.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    value = ((TextBox)input).Text.Trim();
                }

                if (property.DataType == "Number" && value.Length > 0)
                {
                    double number;
                    string normalized = value.Replace(',', '.');

                    if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    {
                        ShowWarning("\"" + property.Name + "\" alanına sayı yazmalısınız.");
                        input.Focus();
                        return;
                    }

                    value = ProductRepository.NormalizeNumber(number);
                }

                values[property.Id] = value;
            }

            // Seri numarası denetimi
            if (_serialProperty != null)
            {
                string oldSerial;
                if (!_oldValues.TryGetValue(_serialProperty.Id, out oldSerial))
                {
                    oldSerial = "";
                }

                string newSerial = values[_serialProperty.Id];

                if (newSerial.Length == 0 && oldSerial.Length > 0)
                {
                    ShowWarning("Seri numarası boş bırakılamaz.");
                    _inputs[_serialProperty.Id].Focus();
                    return;
                }

                bool serialChanged = !string.Equals(newSerial, oldSerial, StringComparison.OrdinalIgnoreCase);

                if (newSerial.Length > 0 && serialChanged &&
                    ProductRepository.SerialNumberExists(_serialProperty.Id, newSerial, _productId))
                {
                    ShowWarning("Bu seri numarası başka bir üründe zaten kayıtlı.");
                    _inputs[_serialProperty.Id].Focus();
                    return;
                }
            }

            // Sadece değişen alanları bul.
            var changes = new List<ProductChange>();

            foreach (var property in _properties)
            {
                string oldValue;
                if (!_oldValues.TryGetValue(property.Id, out oldValue))
                {
                    oldValue = "";
                }

                string newValue = values[property.Id];

                // Hiç girilmemiş Evet/Hayır alanı, işaretsiz kalırsa değişmiş sayılmaz.
                if (property.DataType == "YesNo" && oldValue.Length == 0 && newValue == "0")
                {
                    continue;
                }

                if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                {
                    changes.Add(new ProductChange
                    {
                        Property = property,
                        OldValue = oldValue,
                        NewValue = newValue
                    });
                }
            }

            bool quantityChanged = newQuantity.HasValue && newQuantity.Value != _oldQuantity;

            if (!quantityChanged && changes.Count == 0)
            {
                MessageBox.Show(this, "Değişiklik yapılmadı.", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                BuildViewMode();
                return;
            }

            try
            {
                ProductRepository.Update(_typeId, _productId, _oldQuantity, newQuantity, changes);
            }
            catch (Exception ex)
            {
                ShowError("Ürün kaydedilemedi:\n" + ex.Message);
                return;
            }

            MessageBox.Show(this, "Ürün güncellendi.", "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);

            Changed = true;
            DialogResult = true;
        }

        // ---------- HURDAYA AYIR ----------

        /// <summary>
        /// "Hurdaya ayır" kutusuna tıklanınca: onay sorar, onaylanırsa ürünü taşır ve pencereyi kapatır.
        /// Onaylanmazsa kutu eski haline döner.
        /// </summary>
        private void ScrapCheck_Click(object sender, RoutedEventArgs e)
        {
            bool wantScrap = ScrapCheck.IsChecked == true;

            string question = wantScrap
                ? "Bu ürün hurdaya taşınacak, onaylıyor musun?"
                : "Bu ürün hurdadan çıkarılıp normal listeye geri alınacak, onaylıyor musun?";

            var answer = MessageBox.Show(this, question, "Onay",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                ScrapCheck.IsChecked = !wantScrap;
                return;
            }

            try
            {
                var ids = new List<long> { _productId };

                if (wantScrap)
                {
                    ProductArchiveRepository.MoveToScrap(_typeId, ids);
                }
                else
                {
                    ProductArchiveRepository.RestoreFromScrap(_typeId, ids);
                }
            }
            catch (Exception ex)
            {
                ShowError("İşlem yapılamadı:\n" + ex.Message);
                ScrapCheck.IsChecked = !wantScrap;
                return;
            }

            Changed = true;
            DialogResult = true;
        }

        // ---------- MESAJ KUTULARI ----------

        private void ShowWarning(string message)
        {
            MessageBox.Show(this, message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ---------- YARDIMCI İŞLEVLER (ana pencere ve hurda penceresi kullanır) ----------

        /// <summary>
        /// Tablodaki bir satırdan, pencerede gösterilecek "başlık - değer" çiftlerini hazırlar.
        /// </summary>
        public static List<KeyValuePair<string, string>> BuildDetails(
            DataRowView rowView,
            List<PropertyDefinition> properties)
        {
            var list = new List<KeyValuePair<string, string>>();

            list.Add(new KeyValuePair<string, string>(
                "Sıra no", Convert.ToString(rowView[ProductListRepository.NoColumn])));

            if (rowView.Row.Table.Columns.Contains(ProductListRepository.QuantityColumn))
            {
                list.Add(new KeyValuePair<string, string>(
                    "Miktar", Convert.ToString(rowView[ProductListRepository.QuantityColumn])));
            }

            foreach (var property in properties)
            {
                string column = ProductListRepository.ColumnNameFor(property.Id);
                string value = rowView.Row.IsNull(column) ? "" : (string)rowView[column];

                list.Add(new KeyValuePair<string, string>(property.Name, value));
            }

            return list;
        }

        /// <summary>
        /// Çift tıklanan yerin hangi tablo satırına ait olduğunu bulur.
        /// Başlığa ya da onay kutusuna tıklandıysa boş verir.
        /// </summary>
        public static DataRowView GetRowFrom(object originalSource)
        {
            var element = originalSource as DependencyObject;

            while (element != null)
            {
                if (element is CheckBox)
                {
                    return null;
                }

                var row = element as DataGridRow;
                if (row != null)
                {
                    return row.Item as DataRowView;
                }

                if (element is Visual || element is Visual3D)
                {
                    element = VisualTreeHelper.GetParent(element);
                }
                else
                {
                    element = LogicalTreeHelper.GetParent(element);
                }
            }

            return null;
        }
    }
}