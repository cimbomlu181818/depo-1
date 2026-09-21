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
    public partial class HistoryWindow : Window
    {
        /// <summary>
        /// Ekrana en fazla bu kadar işlem yüklenir (en yeniden eskiye doğru).
        /// </summary>
        private const int MaxRows = 500;

        private List<LogEntry> _entries;

        public HistoryWindow()
        {
            InitializeComponent();
            LoadHistory();
            SearchBox.Focus();
        }

        /// <summary>
        /// Son işlemleri veritabanından okuyup tabloya doldurur.
        /// </summary>
        private void LoadHistory()
        {
            try
            {
                _entries = LogRepository.GetRecent(MaxRows);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "İşlem geçmişi okunamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _entries = new List<LogEntry>();
            }

            HistoryGrid.ItemsSource = _entries;
            UpdateCount();
        }

        /// <summary>
        /// Arama kutusuna yazılan yazıyı; zaman, kullanıcı, işlem ve ayrıntı sütunlarında arar.
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
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
            }
            else
            {
                var turkish = new CultureInfo("tr-TR");

                view.Filter = item =>
                {
                    var entry = (LogEntry)item;

                    return Contains(entry.TimeText, text, turkish) ||
                           Contains(entry.UserName, text, turkish) ||
                           Contains(entry.Action, text, turkish) ||
                           Contains(entry.Details, text, turkish);
                };
            }

            UpdateCount();
        }

        private static bool Contains(string source, string text, CultureInfo culture)
        {
            return culture.CompareInfo.IndexOf(source ?? "", text, CompareOptions.IgnoreCase) >= 0;
        }

        /// <summary>
        /// Üstteki kayıt sayısı yazısını günceller.
        /// </summary>
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
                CountText.Text = "Son " + _entries.Count + " işlem";
            }
            else
            {
                CountText.Text = shown + " / " + _entries.Count + " işlem";
            }
        }
    }
}