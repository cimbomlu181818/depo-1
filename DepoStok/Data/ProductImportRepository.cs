using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace DepoStok.Data
{
    /// <summary>
    /// İçe aktarma sonucunda kaç ürünün eklendiğini ve hangi satırların neden atlandığını taşır.
    /// </summary>
    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// CsvImporter'ın okuduğu satırları denetler ve geçerli olanları veritabanına ekler.
    /// Her satır kendi içinde tek parça kaydedilir: bir satırdaki hata sadece o satırı atlar,
    /// diğer satırları etkilemez.
    /// </summary>
    public static class ProductImportRepository
    {
        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        public static ImportResult Import(
            long productTypeId,
            string typeName,
            List<PropertyDefinition> properties,
            CsvImporter.Result data,
            string fileName)
        {
            var result = new ImportResult();
            var serialProperty = properties.FirstOrDefault(p => p.IsSerialNumber);
            var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CsvRow row in data.Rows)
            {
                string error;

                if (!TryImportRow(productTypeId, properties, serialProperty, seenSerials, row, out error))
                {
                    result.Errors.Add("Satır " + row.LineNumber + ": " + error);
                    continue;
                }

                result.SuccessCount++;
            }

            if (result.SuccessCount > 0)
            {
                LogRepository.Write("İçe aktarıldı",
                    "Ürün tipi: " + typeName + ", dosya: " + fileName + ", " +
                    result.SuccessCount + " başarılı, " + result.Errors.Count + " atlandı");
            }

            return result;
        }

        /// <summary>
        /// Tek bir satırı denetler ve geçerliyse ekler. Geçersizse false verir ve nedenini yazar.
        /// </summary>
        private static bool TryImportRow(
            long productTypeId,
            List<PropertyDefinition> properties,
            PropertyDefinition serialProperty,
            HashSet<string> seenSerials,
            CsvRow row,
            out string error)
        {
            error = null;

            // Her alanı denetle ve veritabanına yazılacak hâline çevir.
            var values = new Dictionary<long, string>();

            foreach (var property in properties)
            {
                string text;

                if (!row.Cells.TryGetValue(property.Name, out text) || text.Trim().Length == 0)
                {
                    continue;
                }

                string storageValue;

                if (!TryConvertValue(property, text.Trim(), out storageValue, out error))
                {
                    error = "\"" + property.Name + "\" alanı - " + error;
                    return false;
                }

                values[property.Id] = storageValue;
            }

            // Seri numarası: varsa zorunlu, dosya içinde ve veritabanında tekil olmalı.
            if (serialProperty != null)
            {
                string serial;

                if (!values.TryGetValue(serialProperty.Id, out serial) || serial.Length == 0)
                {
                    error = "Seri numarası (\"" + serialProperty.Name + "\") boş olamaz.";
                    return false;
                }

                if (!seenSerials.Add(serial))
                {
                    error = "Seri numarası \"" + serial + "\" dosyada birden fazla kez var.";
                    return false;
                }

                if (ProductRepository.SerialNumberExists(serialProperty.Id, serial))
                {
                    error = "Seri numarası \"" + serial + "\" zaten kayıtlı.";
                    return false;
                }
            }

            // Miktar: seri numarasız tiplerde "Miktar" sütunundan okunur, yoksa 1 kabul edilir.
            int quantity = 1;

            if (serialProperty == null)
            {
                string quantityText;

                if (row.Cells.TryGetValue("Miktar", out quantityText) && quantityText.Trim().Length > 0)
                {
                    if (!int.TryParse(quantityText.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out quantity) || quantity < 1)
                    {
                        error = "\"Miktar\" 1 veya daha büyük bir tam sayı olmalı.";
                        return false;
                    }
                }
            }

            try
            {
                ProductRepository.Add(productTypeId, quantity, properties, values);
            }
            catch (Exception ex)
            {
                error = "Kaydedilemedi: " + ex.Message;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Hücredeki yazıyı alanın türüne göre denetler ve veritabanına yazılacak hâle çevirir.
        /// Sayılarda hem nokta hem virgül kabul edilir. Tarihler gg.aa.yyyy biçiminde olmalı.
        /// Evet/Hayır alanına sadece "Evet" veya "Hayır" yazılabilir.
        /// </summary>
        private static bool TryConvertValue(
            PropertyDefinition property, string text, out string storageValue, out string error)
        {
            storageValue = "";
            error = null;

            switch (property.DataType)
            {
                case "Number":
                    double number;
                    string normalized = text.Replace(',', '.');

                    if (!double.TryParse(normalized, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out number))
                    {
                        error = "\"" + text + "\" geçerli bir sayı değil.";
                        return false;
                    }

                    storageValue = normalized;
                    return true;

                case "Date":
                    DateTime date;

                    if (!DateTime.TryParseExact(text, "dd.MM.yyyy", Turkish,
                        DateTimeStyles.None, out date))
                    {
                        error = "\"" + text + "\" geçerli bir tarih değil (örnek: 20.09.2026).";
                        return false;
                    }

                    storageValue = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return true;

                case "YesNo":
                    string lower = text.ToLower(Turkish);

                    if (lower == "evet")
                    {
                        storageValue = "1";
                        return true;
                    }

                    if (lower == "hayır" || lower == "hayir")
                    {
                        storageValue = "0";
                        return true;
                    }

                    error = "\"" + text + "\" yalnızca \"Evet\" ya da \"Hayır\" olabilir.";
                    return false;

                default:
                    storageValue = text;
                    return true;
            }
        }
    }
}