using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using DepoStok.Data;

namespace DepoStok
{
    public partial class ColumnOrderWindow : Window
    {
        private readonly ProductType _type;
        private readonly ObservableCollection<PropertyDefinition> _properties;

        /// <summary>
        /// Pencere Kaydet ile kapanınca true olur (çağıran taraf tabloyu yeniden çizer).
        /// </summary>
        public bool Saved { get; private set; }

        public ColumnOrderWindow(ProductType type, List<PropertyDefinition> properties)
        {
            InitializeComponent();

            _type = type;
            _properties = new ObservableCollection<PropertyDefinition>(properties);

            PropertyList.ItemsSource = _properties;

            if (_properties.Count > 0)
            {
                PropertyList.SelectedIndex = 0;
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            int index = PropertyList.SelectedIndex;

            if (index <= 0)
            {
                return;
            }

            _properties.Move(index, index - 1);
            PropertyList.SelectedIndex = index - 1;
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            int index = PropertyList.SelectedIndex;

            if (index < 0 || index >= _properties.Count - 1)
            {
                return;
            }

            _properties.Move(index, index + 1);
            PropertyList.SelectedIndex = index + 1;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TypePropertyRepository.Reorder(_type.Id, _properties.Select(p => p.Id).ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Sıra kaydedilemedi:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                LogRepository.Write("Sütun sırası değiştirildi", "Ürün tipi: " + _type.Name);
            }
            catch (Exception)
            {
                // Sıra zaten kaydedildi; log yazılamaması işlemi engellemez.
            }

            Saved = true;
            DialogResult = true;
        }
    }
}