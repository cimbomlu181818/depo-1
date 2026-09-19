using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DepoStok.Data;

namespace DepoStok
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            RefreshTypeList();
            RefreshPropertyList();
            RefreshAssignTab();
            TypeNameBox.Focus();
        }

        private void RefreshTypeList()
        {
            TypeList.ItemsSource = ProductTypeRepository.GetNames();
        }

        private void RefreshPropertyList()
        {
            PropertyList.ItemsSource = PropertyDefinitionRepository.GetAll();
        }

        // ---------- ÜRÜN TİPLERİ SEKMESİ ----------

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = TypeNameBox.Text.Trim();

            if (name.Length == 0)
            {
                ShowWarning("Lütfen ürün tipi adını yazın.");
                return;
            }

            var turkish = new CultureInfo("tr-TR");
            bool alreadyExists = ProductTypeRepository.GetNames()
                .Any(n => string.Compare(n, name, turkish, CompareOptions.IgnoreCase) == 0);

            if (alreadyExists)
            {
                ShowWarning("Bu ürün tipi zaten var.");
                return;
            }

            try
            {
                ProductTypeRepository.Add(name);
            }
            catch (Exception ex)
            {
                ShowError("Ürün tipi eklenemedi:\n" + ex.Message);
                return;
            }

            TypeNameBox.Clear();
            RefreshTypeList();
            RefreshAssignTab();
            TypeNameBox.Focus();
        }

        // ---------- ALAN KÜTÜPHANESİ SEKMESİ ----------

        private void AddPropertyButton_Click(object sender, RoutedEventArgs e)
        {
            string name = PropertyNameBox.Text.Trim();

            if (name.Length == 0)
            {
                ShowWarning("Lütfen alan adını yazın.");
                return;
            }

            var selectedItem = (ComboBoxItem)DataTypeBox.SelectedItem;
            string dataType = (string)selectedItem.Tag;
            bool isSerialNumber = SerialNumberCheck.IsChecked == true;

            var existing = PropertyDefinitionRepository.GetAll();
            var turkish = new CultureInfo("tr-TR");

            bool nameExists = existing
                .Any(p => string.Compare(p.Name, name, turkish, CompareOptions.IgnoreCase) == 0);

            if (nameExists)
            {
                ShowWarning("Bu alan zaten var.");
                return;
            }

            if (isSerialNumber && dataType != "Text")
            {
                ShowWarning("Seri numarası alanının türü Metin olmalı.");
                return;
            }

            if (isSerialNumber && existing.Any(p => p.IsSerialNumber))
            {
                ShowWarning("Seri numarası alanı zaten tanımlı. Birden fazla olamaz.");
                return;
            }

            try
            {
                PropertyDefinitionRepository.Add(name, dataType, isSerialNumber);
            }
            catch (Exception ex)
            {
                ShowError("Alan eklenemedi:\n" + ex.Message);
                return;
            }

            PropertyNameBox.Clear();
            DataTypeBox.SelectedIndex = 0;
            SerialNumberCheck.IsChecked = false;
            RefreshPropertyList();
            RefreshAssignTab();
            PropertyNameBox.Focus();
        }

        // ---------- TİP ALANLARI SEKMESİ ----------

        /// <summary>
        /// Ürün tipi listesini yeniler. Önceden seçili tip varsa seçili kalır.
        /// </summary>
        private void RefreshAssignTab()
        {
            var selected = TypeSelectBox.SelectedItem as ProductType;
            long? selectedId = selected == null ? (long?)null : selected.Id;

            var types = ProductTypeRepository.GetAll();
            TypeSelectBox.ItemsSource = types;

            if (selectedId.HasValue)
            {
                TypeSelectBox.SelectedItem = types.FirstOrDefault(t => t.Id == selectedId.Value);
            }

            RefreshAssignedArea();
        }

        /// <summary>
        /// Seçili tipin alanlarını ve eklenebilecek alanları yeniler.
        /// </summary>
        private void RefreshAssignedArea()
        {
            var type = TypeSelectBox.SelectedItem as ProductType;

            if (type == null)
            {
                AssignedList.ItemsSource = null;
                PropertyToAddBox.ItemsSource = null;
                return;
            }

            List<PropertyDefinition> assigned = TypePropertyRepository.GetForType(type.Id);
            AssignedList.ItemsSource = assigned;

            var assignedIds = new HashSet<long>(assigned.Select(p => p.Id));
            PropertyToAddBox.ItemsSource = PropertyDefinitionRepository.GetAll()
                .Where(p => !assignedIds.Contains(p.Id))
                .ToList();
        }

        private void TypeSelectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshAssignedArea();
        }

        private void AssignPropertyButton_Click(object sender, RoutedEventArgs e)
        {
            var type = TypeSelectBox.SelectedItem as ProductType;
            if (type == null)
            {
                ShowWarning("Önce ürün tipini seçin.");
                return;
            }

            var property = PropertyToAddBox.SelectedItem as PropertyDefinition;
            if (property == null)
            {
                ShowWarning("Eklenecek alanı seçin.");
                return;
            }

            try
            {
                TypePropertyRepository.Add(type.Id, property.Id);
            }
            catch (Exception ex)
            {
                ShowError("Alan tipe eklenemedi:\n" + ex.Message);
                return;
            }

            RefreshAssignedArea();
        }

        // ---------- HURDA SEKMESİ ----------

        /// <summary>
        /// Hurda bölümünü açar. Ürünler hurdadan geri alınırsa, Ayarlar kapanınca ana ekran yenilenir.
        /// </summary>
        private void OpenScrapButton_Click(object sender, RoutedEventArgs e)
        {
            var scrapWindow = new ScrapWindow();
            scrapWindow.Owner = this;
            scrapWindow.ShowDialog();
        }

        // ---------- MESAJ KUTULARI ----------

        private void ShowWarning(string message)
        {
            MessageBox.Show(message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}