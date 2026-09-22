using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DepoStok.Data;

namespace DepoStok
{
    public partial class ReportWindow : Window
    {
        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        private ReportRepository.Summary _summary;

        public ReportWindow()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            try
            {
                _summary = ReportRepository.Build();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Rapor hazırlanamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _summary = new ReportRepository.Summary();
            }

            TypeSummaryText.Text = "Ürün tipi sayısı: " + _summary.ActiveTypeCount +
                                    " (arşivde: " + _summary.ArchivedTypeCount + ")";
            PropertySummaryText.Text = "Alan sayısı: " + _summary.ActivePropertyCount +
                                        " (silinmiş: " + _summary.ArchivedPropertyCount + ")";
            ProductSummaryText.Text = "Aktif ürün sayısı: " + _summary.ActiveProductCount;
            ScrapSummaryText.Text = "Hurdadaki ürün: " + _summary.ScrapProductCount +
                                     ", silinmiş ürün: " + _summary.ArchivedProductCount;

            TypeGrid.ItemsSource = _summary.TypeRows;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            var printDialog = new PrintDialog();

            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            FlowDocument document = BuildPrintDocument(printDialog.PrintableAreaWidth);
            IDocumentPaginatorSource paginatorSource = document;

            try
            {
                printDialog.PrintDocument(paginatorSource.DocumentPaginator, "Genel durum raporu");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Yazdırılamadı:\n" + ex.Message, "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                LogRepository.Write("Rapor yazdırıldı", "Genel durum raporu");
            }
            catch (Exception)
            {
                // Yazdırma zaten tamamlandı; log yazılamaması yazdırmayı engellemez.
            }
        }

        private FlowDocument BuildPrintDocument(double pageWidth)
        {
            var document = new FlowDocument
            {
                PageWidth = pageWidth > 0 ? pageWidth : 750,
                FontSize = 11,
                PagePadding = new Thickness(20)
            };

            document.Blocks.Add(new Paragraph(new Run("Genel durum raporu"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            document.Blocks.Add(new Paragraph(new Run(
                "Tarih: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm", Turkish)))
            {
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 12)
            });

            document.Blocks.Add(new Paragraph(new Run(TypeSummaryText.Text)) { Margin = new Thickness(0, 0, 0, 2) });
            document.Blocks.Add(new Paragraph(new Run(PropertySummaryText.Text)) { Margin = new Thickness(0, 0, 0, 2) });
            document.Blocks.Add(new Paragraph(new Run(ProductSummaryText.Text)) { Margin = new Thickness(0, 0, 0, 2) });
            document.Blocks.Add(new Paragraph(new Run(ScrapSummaryText.Text)) { Margin = new Thickness(0, 0, 0, 12) });

            var table = new Table();

            for (int i = 0; i < 4; i++)
            {
                table.Columns.Add(new TableColumn());
            }

            var rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);

            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(MakePrintCell("Ürün tipi", true));
            headerRow.Cells.Add(MakePrintCell("Aktif ürün", true));
            headerRow.Cells.Add(MakePrintCell("Hurdada", true));
            headerRow.Cells.Add(MakePrintCell("Silinmiş", true));
            rowGroup.Rows.Add(headerRow);

            foreach (var row in _summary.TypeRows)
            {
                var tableRow = new TableRow();
                tableRow.Cells.Add(MakePrintCell(row.TypeName, false));
                tableRow.Cells.Add(MakePrintCell(row.ActiveCount.ToString(Turkish), false));
                tableRow.Cells.Add(MakePrintCell(row.ScrapCount.ToString(Turkish), false));
                tableRow.Cells.Add(MakePrintCell(row.ArchivedCount.ToString(Turkish), false));
                rowGroup.Rows.Add(tableRow);
            }

            document.Blocks.Add(table);
            return document;
        }

        private static TableCell MakePrintCell(string text, bool bold)
        {
            var paragraph = new Paragraph(new Run(text)) { Margin = new Thickness(0) };

            if (bold)
            {
                paragraph.FontWeight = FontWeights.Bold;
            }

            return new TableCell(paragraph)
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 3, 4, 3)
            };
        }
    }
}