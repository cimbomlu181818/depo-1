using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using DepoStok.Data;

namespace DepoStok
{
    /// <summary>
    /// Ana sayfadaki bir istatistik kartı için ürün cinsi ve özellik seçtirir.
    /// Hem yeni kart eklerken ("+ Ekle") hem de var olan kartı değiştirirken ("Değiştir")
    /// kullanılır; "Değiştir" akışında şu anki seçim önceden işaretli gelir.
    /// </summary>
    public partial class AddStatisticWindow : Window
    {
        /// <summary>Kullanıcının seçtiği ürün cinsi. Pencere "Ekle" ile kapanınca dolar.</summary>
        public ProductType SelectedType { get; private set; }

        /// <summary>Kullanıcının seçtiği özellik. Pencere "Ekle" ile kapanınca dolar.</summary>
        public PropertyDefinition SelectedProperty { get; private set; }

        /// <summary>Yeni kart ekleme akışı için: hiçbir seçim önceden işaretli gelmez.</summary>
        public AddStatisticWindow()
            : this(0, 0)
        {
        }

        /// <summary>
        /// "Değiştir" akışı için: kartın şu anki ürün cinsi ve özelliği önceden işaretli gelir.
        /// </summary>
        public AddStatisticWindow(long preselectedTypeId, long preselectedPropertyId)
        {
            InitializeComponent();

            var types = ProductTypeRepository.GetAll();
            TypeList.ItemsSource = types;

            if (preselectedTypeId != 0)
            {
                foreach (var type in types)
                {
                    if (type.Id == preselectedTypeId)
                    {
                        TypeList.SelectedItem = type;
                        break;
                    }
                }
            }

            if (preselectedPropertyId != 0)
            {
                foreach (var item in PropertyList.Items)
                {
                    var property = item as PropertyDefinition;
                    if (property != null && property.Id == preselectedPropertyId)
                    {
                        PropertyList.SelectedItem = property;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Ürün cinsi değişince sağdaki özellik listesini o cinsin alanlarıyla doldurur.
        /// </summary>
        private void TypeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var type = TypeList.SelectedItem as ProductType;

            PropertyList.ItemsSource = type == null
                ? new List<PropertyDefinition>()
                : TypePropertyRepository.GetForType(type.Id);

            UpdateOkEnabled();
        }

        private void PropertyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateOkEnabled();
        }

        /// <summary>
        /// "Ekle" düğmesi, hem ürün cinsi hem özellik seçilmeden tıklanamaz.
        /// </summary>
        private void UpdateOkEnabled()
        {
            OkButton.IsEnabled = TypeList.SelectedItem != null && PropertyList.SelectedItem != null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = TypeList.SelectedItem as ProductType;
            SelectedProperty = PropertyList.SelectedItem as PropertyDefinition;

            if (SelectedType == null || SelectedProperty == null)
            {
                return;
            }

            DialogResult = true;
        }
    }
}