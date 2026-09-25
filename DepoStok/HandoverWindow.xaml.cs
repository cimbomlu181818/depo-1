using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using DepoStok.Data;

namespace DepoStok
{
    /// <summary>
    /// Birimler arası malzeme teslim-tesellüm tutanağı: birim adları ve birden fazla
    /// malzeme satırı elle girilir, tarih otomatik bugünün tarihi olarak gelir.
    /// Zimmet (kişiye zimmetleme) tutanağından ayrı, bağımsız bir özelliktir.
    /// </summary>
    public partial class HandoverWindow : Window
    {
        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        public ObservableCollection<HandoverItem> Items { get; } = new ObservableCollection<HandoverItem>();

        public HandoverWindow() : this(null, null)
        {
        }

        /// <summary>
        /// Bir ürün detayından açılınca, ilk satırı o ürünün seri no ve cinsiyle önceden doldurur.
        /// İkisi de boşsa normal boş pencere gibi açılır.
        /// </summary>
        public HandoverWindow(string serialNo, string itemType)
        {
            InitializeComponent();

            DateText.Text = DateTime.Now.ToString("dd.MM.yyyy", Turkish);
            ItemsGrid.ItemsSource = Items;

            if (!string.IsNullOrWhiteSpace(serialNo) || !string.IsNullOrWhiteSpace(itemType))
            {
                Items.Add(new HandoverItem
                {
                    SerialNo = serialNo,
                    ItemType = itemType,
                    Quantity = "1"
                });
            }
        }

        // ---------- SERİ NO / MALZEME CİNSİ ARAMA ----------

        /// <summary>Bir arama kutusunun kendi Popup/Liste referanslarını ve arama türünü tutar.</summary>
        private class AutoCompleteRefs
        {
            public Popup Popup;
            public ListBox List;
            public bool SerialOnly;
        }

        private void SerialBox_Loaded(object sender, RoutedEventArgs e)
        {
            WireSearchBox((TextBox)sender, serialOnly: true);
        }

        private void TypeBox_Loaded(object sender, RoutedEventArgs e)
        {
            WireSearchBox((TextBox)sender, serialOnly: false);
        }

        private void WireSearchBox(TextBox box, bool serialOnly)
        {
            try
            {
                var container = (Grid)box.Parent;
                var popup = container.Children.OfType<Popup>().First();
                var list = (ListBox)((Border)popup.Child).Child;

                box.Tag = new AutoCompleteRefs { Popup = popup, List = list, SerialOnly = serialOnly };
                list.Tag = box;

                box.Focus();
                box.SelectAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Arama kutusu hazırlanamadı (teşhis):\n" + ex, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = (TextBox)sender;
            var refs = box.Tag as AutoCompleteRefs;

            if (refs == null)
            {
                return;
            }

            string query = box.Text.Trim();

            if (query.Length < 2)
            {
                refs.Popup.IsOpen = false;
                return;
            }

            List<ProductSearchResult> results;

            try
            {
                results = refs.SerialOnly
                    ? HandoverSearchRepository.SearchBySerial(query)
                    : HandoverSearchRepository.SearchGeneral(query);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Arama yapılamadı (teşhis):\n" + ex, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                refs.Popup.IsOpen = false;
                return;
            }

            if (results.Count == 0)
            {
                refs.Popup.IsOpen = false;
                return;
            }

            refs.List.ItemsSource = results;
            refs.Popup.IsOpen = true;
        }

        /// <summary>
        /// Açılır listede bir ürüne tıklanınca çalışır. Popup StaysOpen="False" olduğu için
        /// tıklama anında kapanmaya çalışır; bu yüzden PreviewMouseLeftButtonDown kullanılıyor,
        /// böylece tıklanan öğe kaybolmadan önce yakalanmış oluyor.
        /// </summary>
        private void SuggestionsList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var listBox = (ListBox)sender;

            var element = e.OriginalSource as DependencyObject;

            while (element != null && !(element is ListBoxItem))
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element == null)
            {
                return;
            }

            var result = listBox.ItemContainerGenerator.ItemFromContainer(element) as ProductSearchResult;
            var box = listBox.Tag as TextBox;

            if (result == null || box == null)
            {
                return;
            }

            ApplySelection(box, result);
            e.Handled = true;
        }

        private void ApplySelection(TextBox box, ProductSearchResult result)
        {
            var refs = (AutoCompleteRefs)box.Tag;
            var row = box.DataContext as HandoverItem;

            if (row == null)
            {
                return;
            }

            row.SerialNo = result.SerialNo;
            row.ItemType = result.TypeName;

            refs.Popup.IsOpen = false;

            ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (Items.Count > 0 && Items[Items.Count - 1] == row)
            {
                Items.Add(new HandoverItem());
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FromUnitBox.Text))
            {
                ShowWarning("Teslim eden birim boş bırakılamaz.");
                FromUnitBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(ToUnitBox.Text))
            {
                ShowWarning("Teslim alan birim boş bırakılamaz.");
                ToUnitBox.Focus();
                return;
            }

            ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var rows = Items
                .Where(i => !string.IsNullOrWhiteSpace(i.SerialNo) ||
                            !string.IsNullOrWhiteSpace(i.ItemType) ||
                            !string.IsNullOrWhiteSpace(i.Quantity) ||
                            !string.IsNullOrWhiteSpace(i.Note))
                .ToList();

            if (rows.Count == 0)
            {
                ShowWarning("En az bir malzeme satırı girmelisin.");
                return;
            }

            var printDialog = new PrintDialog();

            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            FlowDocument document = BuildDocument(rows, printDialog.PrintableAreaWidth);
            IDocumentPaginatorSource paginatorSource = document;

            try
            {
                printDialog.PrintDocument(paginatorSource.DocumentPaginator, "Teslim-Tesellüm Tutanağı");
            }
            catch (Exception ex)
            {
                ShowError("Yazdırılamadı:\n" + ex.Message);
                return;
            }

            try
            {
                LogRepository.Write("Teslim-tesellüm tutanağı yazdırıldı",
                    FromUnitBox.Text.Trim() + " → " + ToUnitBox.Text.Trim() + ", " + rows.Count + " kalem");
            }
            catch (Exception)
            {
                // Yazdırma zaten tamamlandı; log yazılamaması yazdırmayı engellemez.
            }
        }

        private FlowDocument BuildDocument(List<HandoverItem> rows, double pageWidth)
        {
            double effectivePageWidth = pageWidth > 0 ? pageWidth : 750;

            var document = new FlowDocument
            {
                PageWidth = effectivePageWidth,
                ColumnWidth = effectivePageWidth,
                FontSize = 11,
                PagePadding = new Thickness(25)
            };

            document.Blocks.Add(new Paragraph(new Run("DEMİRBAŞ / TÜKETİM MALZEME TESLİM TUTANAĞI"))
            {
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            });

            var infoTable = new Table();
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var infoGroup = new TableRowGroup();
            infoTable.RowGroups.Add(infoGroup);

            var infoRow1 = new TableRow();
            infoRow1.Cells.Add(MakePlainCell("Teslim Eden Birim: " + FromUnitBox.Text.Trim()));
            infoRow1.Cells.Add(MakePlainCell("Kategori: " + CategoryBox.Text.Trim()));
            infoGroup.Rows.Add(infoRow1);

            var infoRow2 = new TableRow();
            infoRow2.Cells.Add(MakePlainCell("Teslim Alan Birim: " + ToUnitBox.Text.Trim()));
            infoRow2.Cells.Add(MakePlainCell("Tarih: " + DateText.Text));
            infoGroup.Rows.Add(infoRow2);

            document.Blocks.Add(infoTable);
            document.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 6) });

            var table = new Table();
            var columnWidths = new[] { 0.5, 1.5, 2.5, 1.0, 3.0 }; // S.N, Seri No, Sistem adı, Miktarı, Düşünceler
            foreach (var w in columnWidths)
            {
                table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });
            }

            var rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);

            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(MakeCell("S.N", true));
            headerRow.Cells.Add(MakeCell("SERİ NO", true));
            headerRow.Cells.Add(MakeCell("Sistem adı", true));
            headerRow.Cells.Add(MakeCell("Miktarı", true));
            headerRow.Cells.Add(MakeCell("DÜŞÜNCELER", true));
            rowGroup.Rows.Add(headerRow);

            int lineCount = rows.Count;

            for (int i = 0; i < lineCount; i++)
            {
                var tableRow = new TableRow();
                bool hasItem = i < rows.Count;

                tableRow.Cells.Add(MakeCell((i + 1).ToString(Turkish), false));
                tableRow.Cells.Add(MakeCell(hasItem ? rows[i].SerialNo : "", false));
                tableRow.Cells.Add(MakeCell(hasItem ? rows[i].ItemType : "", false));
                tableRow.Cells.Add(MakeCell(hasItem ? rows[i].Quantity : "", false));
                tableRow.Cells.Add(MakeCell(hasItem ? rows[i].Note : "", false));
                rowGroup.Rows.Add(tableRow);
            }

            document.Blocks.Add(table);

            document.Blocks.Add(new Paragraph { Margin = new Thickness(0, 30, 0, 0) });

            var signTable = new Table();
            for (int i = 0; i < 3; i++)
            {
                signTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            }

            var signGroup = new TableRowGroup();
            signTable.RowGroups.Add(signGroup);

            var signRow = new TableRow();
            signRow.Cells.Add(MakeSignatureCell("TESLİM EDEN"));
            signRow.Cells.Add(MakeSignatureCell("HAZURUN"));
            signRow.Cells.Add(MakeSignatureCell("TESLİM ALAN"));
            signGroup.Rows.Add(signRow);

            document.Blocks.Add(signTable);

            return document;
        }

        private static TableCell MakePlainCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text)) { Margin = new Thickness(0) })
            {
                Padding = new Thickness(0, 2, 0, 2)
            };
        }

        private static TableCell MakeCell(string text, bool bold)
        {
            var paragraph = new Paragraph(new Run(text ?? "")) { Margin = new Thickness(0) };

            if (bold)
            {
                paragraph.FontWeight = FontWeights.Bold;
                paragraph.TextAlignment = TextAlignment.Center;
            }

            return new TableCell(paragraph)
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 4, 4, 20)
            };
        }

        private static TableCell MakeSignatureCell(string title)
        {
            var paragraph = new Paragraph(new Run(title))
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(20, 40, 20, 0)
            };

            return new TableCell(paragraph)
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 0)
            };
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(this, message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Teslim-tesellüm tutanağındaki bir malzeme satırı. Arama sonucundan seçilince
    /// hem tablodaki ilgili hücrelerin hem de o an düzenlenmekte olan hücrenin
    /// anında güncellenebilmesi için değişiklik bildirimi yapar.
    /// </summary>
    public class HandoverItem : INotifyPropertyChanged
    {
        private string _serialNo;
        private string _itemType;
        private string _quantity;
        private string _note;

        public string SerialNo
        {
            get { return _serialNo; }
            set { _serialNo = value; OnChanged("SerialNo"); }
        }

        public string ItemType
        {
            get { return _itemType; }
            set { _itemType = value; OnChanged("ItemType"); }
        }

        public string Quantity
        {
            get { return _quantity; }
            set { _quantity = value; OnChanged("Quantity"); }
        }

        public string Note
        {
            get { return _note; }
            set { _note = value; OnChanged("Note"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}