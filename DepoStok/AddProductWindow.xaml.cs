using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DepoStok.Data;

namespace DepoStok
{
    public partial class AddProductWindow : Window
    {
        private readonly ProductType _type;
        private readonly List<PropertyDefinition> _properties;
        private readonly PropertyDefinition _serialProperty;
        private readonly Dictionary<long, Control> _inputs = new Dictionary<long, Control>();
        private TextBox _quantityBox;
        private Control _firstInput;

        public AddProductWindow(ProductType type)
        {
            InitializeComponent();

            _type = type;
            _properties = TypePropertyRepository.GetForType(type.Id);
            _serialProperty = _properties.FirstOrDefault(p => p.IsSerialNumber);

            Title = "Ürün ekle - " + type.Name;
            TitleText.Text = type.Name + " - yeni ürün";

            if (_serialProperty != null)
            {
                HintText.Text = "Bu tip seri numaralı takip edilir. Her ürün tek adettir. * işaretli alan zorunludur.";
            }
            else
            {
                HintText.Text = "Bu tipte seri numarası alanı yok, ürün adet olarak takip edilir. Miktarı yazın.";
            }

            BuildInputs();

            Loaded += (s, e) =>
            {
                if (_firstInput != null)
                {
                    _firstInput.Focus();
                }
            };
        }

        /// <summary>
        /// Tipin alanlarına göre giriş kutularını yan yana oluşturur.
        /// </summary>
        private void BuildInputs()
        {
            foreach (var property in _properties)
            {
                Control input;

                switch (property.DataType)
                {
                    case "Date":
                        input = new DatePicker();
                        break;
                    case "YesNo":
                        input = new CheckBox { Content = "Evet", Margin = new Thickness(0, 6, 0, 0) };
                        break;
                    default:
                        input = new TextBox
                        {
                            Height = 28,
                            VerticalContentAlignment = VerticalAlignment.Center
                        };
                        break;
                }

                string title = property.Name + (property.IsSerialNumber ? " *" : "");
                AddColumn(title, input);
                _inputs[property.Id] = input;

                if (_firstInput == null)
                {
                    _firstInput = input;
                }
            }

            // Seri numarası alanı yoksa adet bazlı takip: Miktar kutusu göster.
            if (_serialProperty == null)
            {
                _quantityBox = new TextBox
                {
                    Text = "1",
                    Height = 28,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                AddColumn("Miktar", _quantityBox);

                if (_firstInput == null)
                {
                    _firstInput = _quantityBox;
                }
            }
        }

        private void AddColumn(string title, Control input)
        {
            var column = new StackPanel
            {
                Width = 150,
                Margin = new Thickness(0, 0, 12, 0)
            };

            column.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            });

            column.Children.Add(input);
            FieldsPanel.Children.Add(column);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            int quantity = 1;

            if (_quantityBox != null)
            {
                int parsed;
                if (!int.TryParse(_quantityBox.Text.Trim(), out parsed) || parsed < 1)
                {
                    ShowWarning("Miktar 1 veya daha büyük bir tam sayı olmalı.");
                    _quantityBox.Focus();
                    return;
                }
                quantity = parsed;
            }

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

                    value = number.ToString(CultureInfo.InvariantCulture);
                }

                values[property.Id] = value;
            }

            if (_serialProperty != null)
            {
                string serial = values[_serialProperty.Id];

                if (serial.Length == 0)
                {
                    ShowWarning("Seri numarası boş bırakılamaz.");
                    _inputs[_serialProperty.Id].Focus();
                    return;
                }

                if (ProductRepository.SerialNumberExists(_serialProperty.Id, serial))
                {
                    ShowWarning("Bu seri numarası zaten kayıtlı.");
                    _inputs[_serialProperty.Id].Focus();
                    return;
                }
            }

            try
            {
                ProductRepository.Add(_type.Id, quantity, _properties, values);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ürün kaydedilemedi:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("Ürün kaydedildi.", "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}