using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DepoStok.Data;

namespace DepoStok
{
    /// <summary>
    /// Şu an kimde ne olduğunu gösteren, iade almaya da izin veren genel zimmet listesi.
    /// </summary>
    public partial class AssignmentsWindow : Window
    {
        private List<Assignment> _entries;

        public AssignmentsWindow()
        {
            InitializeComponent();
            LoadAssignments();
            SearchBox.Focus();
        }

        private void LoadAssignments()
        {
            try
            {
                _entries = AssignmentRepository.GetAllActive();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Zimmet listesi okunamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _entries = new List<Assignment>();
            }

            AssignmentsGrid.ItemsSource = _entries;
            ApplyFilter();
            UpdateCount();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
            UpdateCount();
        }

        private void ApplyFilter()
        {
            if (_entries == null)
            {
                return;
            }

            ICollectionView view = CollectionViewSource.GetDefaultView(_entries);
            string text = SearchBox.Text.Trim();

            if (text.Length == 0)
            {
                view.Filter = null;
                return;
            }

            var turkish = new CultureInfo("tr-TR");

            view.Filter = item =>
            {
                var assignment = (Assignment)item;

                return Contains(assignment.TypeName, text, turkish) ||
                       Contains(assignment.ProductDescription, text, turkish) ||
                       Contains(assignment.PersonName, text, turkish) ||
                       Contains(assignment.RegistryNo, text, turkish) ||
                       Contains(assignment.Department, text, turkish);
            };
        }

        private static bool Contains(string source, string text, CultureInfo culture)
        {
            return culture.CompareInfo.IndexOf(source ?? "", text, CompareOptions.IgnoreCase) >= 0;
        }

        private void UpdateCount()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(_entries);

            int shown = 0;
            foreach (object item in view)
            {
                shown++;
            }

            if (SearchBox.Text.Trim().Length == 0)
            {
                CountText.Text = _entries.Count + " zimmet";
            }
            else
            {
                CountText.Text = shown + " / " + _entries.Count + " zimmet";
            }
        }

        /// <summary>
        /// Satırdaki "İade Al" düğmesine basılınca o zimmeti iade alınmış işaretler ve listeyi tazeler.
        /// </summary>
        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var assignment = (Assignment)button.DataContext;

            var answer = MessageBox.Show(this,
                assignment.PersonName + " üzerindeki bu zimmet iade alınacak, onaylıyor musun?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                AssignmentRepository.Return(assignment.Id, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "İade alınamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadAssignments();
        }
    }
}