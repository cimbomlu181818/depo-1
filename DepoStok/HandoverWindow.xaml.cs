using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        public HandoverWindow()
        {
            InitializeComponent();

            DateText.Text = DateTime.Now.ToString("dd.MM.yyyy", Turkish);
            ItemsGrid.ItemsSource = Items;
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
            var document = new FlowDocument
            {
                PageWidth = pageWidth > 0 ? pageWidth : 750,
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
            for (int i = 0; i < 5; i++)
            {
                table.Columns.Add(new TableColumn());
            }

            var rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);

            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(MakeCell("S.N", true));
            headerRow.Cells.Add(MakeCell("SERİ NO", true));
            headerRow.Cells.Add(MakeCell("Malzemenin cinsi", true));
            headerRow.Cells.Add(MakeCell("Miktarı", true));
            headerRow.Cells.Add(MakeCell("DÜŞÜNCELER", true));
            rowGroup.Rows.Add(headerRow);

            // Örnekteki gibi en az 11 satır olacak şekilde, boş satırlar da çizgili basılır.
            int lineCount = Math.Max(rows.Count, 11);

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
                signTable.Columns.Add(new TableColumn());
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
    /// Teslim-tesellüm tutanağındaki bir malzeme satırı.
    /// </summary>
    public class HandoverItem
    {
        public string SerialNo { get; set; }
        public string ItemType { get; set; }
        public string Quantity { get; set; }
        public string Note { get; set; }
    }
}