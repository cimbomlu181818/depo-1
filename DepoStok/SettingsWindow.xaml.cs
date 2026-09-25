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
            RefreshArchivedTypeList();
            RefreshPropertyList();
            RefreshDeletedPropertyList();
            RefreshAssignTab();
            TypeNameBox.Focus();
        }

        private void RefreshTypeList()
        {
            TypeList.ItemsSource = ProductTypeRepository.GetAll();
        }

        private void RefreshArchivedTypeList()
        {
            ArchivedTypeList.ItemsSource = ProductTypeRepository.GetArchived();
        }

        private void RefreshPropertyList()
        {
            PropertyList.ItemsSource = PropertyDefinitionRepository.GetAll();
        }

        private void RefreshDeletedPropertyList()
        {
            DeletedPropertyList.ItemsSource = PropertyDefinitionRepository.GetArchived();
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

            string closeType = SimilarityHelper.FindClosestMatch(name, ProductTypeRepository.GetNames());

            if (closeType != null)
            {
                MessageBoxResult confirm = MessageBox.Show(
                    "\"" + closeType + "\" mi demek istediniz?\n\n" +
                    "Yine de \"" + name + "\" adında yeni bir ürün tipi eklemek istiyor musunuz?",
                    "Şunu mu demek istediniz?", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
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

            WriteLog("Ürün tipi eklendi", "Ad: " + name);

            TypeNameBox.Clear();
            RefreshTypeList();
            RefreshAssignTab();
            TypeNameBox.Focus();
        }

        /// <summary>
        /// Listede seçili ürün tipini arşive alır. Ürünlerine ve ayarlarına dokunulmaz,
        /// yalnızca listelerde görünmez olur.
        /// </summary>
        private void ArchiveTypeButton_Click(object sender, RoutedEventArgs e)
        {
            var type = TypeList.SelectedItem as ProductType;

            if (type == null)
            {
                ShowWarning("Listeden arşivlenecek ürün tipini seçin.");
                return;
            }

            int activeProducts = ProductTypeRepository.CountActiveProducts(type.Id);

            string message = "\"" + type.Name + "\" arşive alınacak. Listelerde görünmeyecek, " +
                              "ama hiçbir şey silinmeyecek.";

            if (activeProducts > 0)
            {
                message += "\n\nBu tipte " + activeProducts + " ürün var, onlar da birlikte gizlenir.";
            }

            message += "\n\nOnaylıyor musunuz?";

            MessageBoxResult answer = MessageBox.Show(message, "Onay",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ProductTypeRepository.Archive(type.Id);
            }
            catch (Exception ex)
            {
                ShowError("Ürün tipi arşivlenemedi:\n" + ex.Message);
                return;
            }

            WriteLog("Ürün tipi arşivlendi", "Ad: " + type.Name);

            RefreshTypeList();
            RefreshArchivedTypeList();
            RefreshAssignTab();
        }

        /// <summary>
        /// Arşivdeki listede seçili ürün tipini geri getirir.
        /// </summary>
        private void RestoreTypeButton_Click(object sender, RoutedEventArgs e)
        {
            var type = ArchivedTypeList.SelectedItem as ProductType;

            if (type == null)
            {
                ShowWarning("Listeden geri alınacak ürün tipini seçin.");
                return;
            }

            try
            {
                ProductTypeRepository.Restore(type.Id);
            }
            catch (Exception ex)
            {
                ShowError("Ürün tipi geri alınamadı:\n" + ex.Message);
                return;
            }

            WriteLog("Ürün tipi arşivden geri alındı", "Ad: " + type.Name);

            RefreshTypeList();
            RefreshArchivedTypeList();
            RefreshAssignTab();
        }

        /// <summary>
        /// Arşivdeki listede seçili ürün tipini kalıcı olarak siler. Bu tipte (arşivde de
        /// olsa) hâlâ ürün varsa engellenir; bu geri alınamaz bir işlemdir.
        /// </summary>
        private void DeleteTypeButton_Click(object sender, RoutedEventArgs e)
        {
            var type = ArchivedTypeList.SelectedItem as ProductType;

            if (type == null)
            {
                ShowWarning("Listeden kalıcı olarak silinecek ürün tipini seçin.");
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                "\"" + type.Name + "\" tipi kalıcı olarak silinecek. Bu işlem geri alınamaz.\n\n" +
                "Onaylıyor musunuz?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ProductTypeRepository.Delete(type.Id);
            }
            catch (InvalidOperationException ex)
            {
                ShowWarning(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                ShowError("Ürün tipi kalıcı olarak silinemedi:\n" + ex.Message);
                return;
            }

            WriteLog("Ürün tipi kalıcı olarak silindi", "Ad: " + type.Name);

            RefreshArchivedTypeList();
            RefreshAssignTab();
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

            var existing = PropertyDefinitionRepository.GetAll();
            var turkish = new CultureInfo("tr-TR");

            bool nameExists = existing
                .Any(p => string.Compare(p.Name, name, turkish, CompareOptions.IgnoreCase) == 0);

            if (nameExists)
            {
                ShowWarning("Bu alan zaten var.");
                return;
            }

            string closeProperty = SimilarityHelper.FindClosestMatch(
                name, existing.Select(p => p.Name));

            if (closeProperty != null)
            {
                MessageBoxResult confirm = MessageBox.Show(
                    "\"" + closeProperty + "\" mi demek istediniz?\n\n" +
                    "Yine de \"" + name + "\" adında yeni bir alan eklemek istiyor musunuz?",
                    "Şunu mu demek istediniz?", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            try
            {
                PropertyDefinitionRepository.Add(name, dataType, false);
            }
            catch (Exception ex)
            {
                ShowError("Alan eklenemedi:\n" + ex.Message);
                return;
            }

            WriteLog("Alan eklendi",
                "Ad: " + name +
                ", tür: " + Convert.ToString(selectedItem.Content));

            PropertyNameBox.Clear();
            DataTypeBox.SelectedIndex = 0;
            RefreshPropertyList();
            RefreshAssignTab();
            PropertyNameBox.Focus();
        }

        /// <summary>
        /// Listede seçili alanı kütüphaneden siler (arşive alır). Hâlâ bir ürün tipinde
        /// kullanılıyorsa engellenir ve hangi işlemi önce yapman gerektiğini söyler.
        /// </summary>
        private void DeletePropertyButton_Click(object sender, RoutedEventArgs e)
        {
            var property = PropertyList.SelectedItem as PropertyDefinition;

            if (property == null)
            {
                ShowWarning("Listeden silinecek alanı seçin.");
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                "\"" + property.Name + "\" alanı kütüphaneden silinecek. " +
                "Daha önce girilmiş değerler kaybolmaz, sadece görünmez olur.\n\nOnaylıyor musunuz?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                PropertyDefinitionRepository.Archive(property.Id);
            }
            catch (InvalidOperationException ex)
            {
                ShowWarning(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                ShowError("Alan silinemedi:\n" + ex.Message);
                return;
            }

            WriteLog("Alan kütüphaneden silindi", "Ad: " + property.Name);

            RefreshPropertyList();
            RefreshDeletedPropertyList();
        }

        /// <summary>
        /// Silinmiş listede seçili alanı kütüphaneye geri getirir.
        /// </summary>
        private void RestorePropertyButton_Click(object sender, RoutedEventArgs e)
        {
            var property = DeletedPropertyList.SelectedItem as PropertyDefinition;

            if (property == null)
            {
                ShowWarning("Listeden geri alınacak alanı seçin.");
                return;
            }

            try
            {
                PropertyDefinitionRepository.Restore(property.Id);
            }
            catch (Exception ex)
            {
                ShowError("Alan geri alınamadı:\n" + ex.Message);
                return;
            }

            WriteLog("Alan kütüphaneye geri alındı", "Ad: " + property.Name);

            RefreshPropertyList();
            RefreshDeletedPropertyList();
        }

        /// <summary>
        /// Silinmiş listede seçili alanı kütüphaneden kalıcı olarak siler. Herhangi bir
        /// üründe bu alana değer girilmişse engellenir; bu geri alınamaz bir işlemdir.
        /// </summary>
        private void DeletePropertyPermanentlyButton_Click(object sender, RoutedEventArgs e)
        {
            var property = DeletedPropertyList.SelectedItem as PropertyDefinition;

            if (property == null)
            {
                ShowWarning("Listeden kalıcı olarak silinecek alanı seçin.");
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                "\"" + property.Name + "\" alanı kalıcı olarak silinecek. Bu işlem geri alınamaz.\n\n" +
                "Onaylıyor musunuz?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                PropertyDefinitionRepository.Delete(property.Id);
            }
            catch (InvalidOperationException ex)
            {
                ShowWarning(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                ShowError("Alan kalıcı olarak silinemedi:\n" + ex.Message);
                return;
            }

            WriteLog("Alan kütüphaneden kalıcı olarak silindi", "Ad: " + property.Name);

            RefreshDeletedPropertyList();
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

            WriteLog("Alan tipe eklendi",
                "Ürün tipi: " + type.Name + ", alan: " + property.Name);

            RefreshAssignedArea();
        }

        /// <summary>
        /// Listede seçili alanı, seçili ürün tipinden çıkarır (arşive alır).
        /// Ürünlerdeki değerler silinmez; alan tipe tekrar eklenirse geri gelir.
        /// </summary>
        private void RemoveAssignedButton_Click(object sender, RoutedEventArgs e)
        {
            var type = TypeSelectBox.SelectedItem as ProductType;
            if (type == null)
            {
                ShowWarning("Önce ürün tipini seçin.");
                return;
            }

            var property = AssignedList.SelectedItem as PropertyDefinition;
            if (property == null)
            {
                ShowWarning("Listeden tipten çıkarılacak alanı seçin.");
                return;
            }

            string message =
                "\"" + property.Name + "\" alanı \"" + type.Name + "\" tipinden çıkarılacak.\n\n" +
                "Ürünlerdeki değerler silinmez, sadece gizlenir. " +
                "Alanı tipe tekrar eklerseniz değerler geri gelir.\n\n";

            if (property.IsSerialNumber)
            {
                message +=
                    "Dikkat: Bu bir seri numarası alanı. Çıkarırsanız bu tipteki ürünler " +
                    "seri numarasız (adet bazlı) davranır.\n\n";
            }

            message += "Onaylıyor musunuz?";

            MessageBoxResult answer = MessageBox.Show(message, "Onay",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                TypePropertyRepository.RemoveFromType(type.Id, property.Id);
            }
            catch (Exception ex)
            {
                ShowError("Alan tipten çıkarılamadı:\n" + ex.Message);
                return;
            }

            WriteLog("Alan tipten çıkarıldı",
                "Ürün tipi: " + type.Name + ", alan: " + property.Name);

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

        // ---------- ADI DEĞİŞTİRME ----------

        /// <summary>
        /// Ürün tipleri listesinde seçili tipin adını değiştirir.
        /// </summary>
        private void RenameTypeButton_Click(object sender, RoutedEventArgs e)
        {
            var type = TypeList.SelectedItem as ProductType;

            if (type == null)
            {
                ShowWarning("Listeden adını değiştireceğiniz ürün tipini seçin.");
                return;
            }

            var turkish = new CultureInfo("tr-TR");

            var window = new RenameWindow(
                "Ürün tipi: " + type.Name,
                type.Name,
                newName =>
                {
                    bool taken = ProductTypeRepository.GetAll().Any(t =>
                        t.Id != type.Id &&
                        string.Compare(t.Name, newName, turkish, CompareOptions.IgnoreCase) == 0);

                    return taken ? "Bu adda başka bir ürün tipi zaten var." : null;
                });
            window.Owner = this;

            if (window.ShowDialog() != true || window.NewName == type.Name)
            {
                return;
            }

            try
            {
                RenameRepository.RenameProductType(type.Id, window.NewName);
            }
            catch (Exception ex)
            {
                ShowError("Ad değiştirilemedi:\n" + ex.Message);
                return;
            }

            WriteLog("Ürün tipi adı değiştirildi", type.Name + " → " + window.NewName);

            RefreshTypeList();
            RefreshAssignTab();
        }

        /// <summary>
        /// Alan kütüphanesinde seçili alanın adını değiştirir.
        /// </summary>
        private void RenamePropertyButton_Click(object sender, RoutedEventArgs e)
        {
            var property = PropertyList.SelectedItem as PropertyDefinition;

            if (property == null)
            {
                ShowWarning("Listeden adını değiştireceğiniz alanı seçin.");
                return;
            }

            if (PropertyDefinitionRepository.IsProtected(property.Name))
            {
                ShowWarning("\"" + property.Name + "\" sistemin sabit bir alanıdır, adı değiştirilemez.");
                return;
            }

            var turkish = new CultureInfo("tr-TR");

            var window = new RenameWindow(
                "Alan: " + property.Name,
                property.Name,
                newName =>
                {
                    bool taken = PropertyDefinitionRepository.GetAll().Any(p =>
                        p.Id != property.Id &&
                        string.Compare(p.Name, newName, turkish, CompareOptions.IgnoreCase) == 0);

                    return taken ? "Bu adda başka bir alan zaten var." : null;
                });
            window.Owner = this;

            if (window.ShowDialog() != true || window.NewName == property.Name)
            {
                return;
            }

            try
            {
                RenameRepository.RenameProperty(property.Id, window.NewName);
            }
            catch (Exception ex)
            {
                ShowError("Ad değiştirilemedi:\n" + ex.Message);
                return;
            }

            WriteLog("Alan adı değiştirildi", property.Name + " → " + window.NewName);

            RefreshPropertyList();
            RefreshAssignTab();
        }

        // ---------- İŞLEM LOGU ----------

        /// <summary>
        /// İşlemi loga yazar. Log yazılamazsa yapılan işlem geri alınmaz, sadece uyarı verilir.
        /// </summary>
        private void WriteLog(string action, string details)
        {
            try
            {
                LogRepository.Write(action, details);
            }
            catch (Exception ex)
            {
                ShowWarning("İşlem yapıldı ama işlem loguna yazılamadı:\n" + ex.Message);
            }
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