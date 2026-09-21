using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DepoStok.Data
{
    /// <summary>
    /// Bir tabloyu Excel'in doğrudan açabileceği CSV dosyasına yazar.
    /// Türkçe Excel ayraç olarak noktalı virgül kullandığı için o kullanılır.
    /// </summary>
    public static class CsvExporter
    {
        private const char Separator = ';';

        /// <summary>
        /// Başlıkları ve satırları verilen dosyaya yazar. Dosya varsa üzerine yazılır.
        /// </summary>
        public static void Write(
            string path,
            IList<string> headers,
            IEnumerable<IList<string>> rows)
        {
            // UTF-8 (BOM'lu) yazılır, böylece Excel Türkçe karakterleri doğru okur.
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.NewLine = "\r\n";

                writer.WriteLine(string.Join(Separator.ToString(), headers.Select(Escape)));

                foreach (IList<string> row in rows)
                {
                    writer.WriteLine(string.Join(Separator.ToString(), row.Select(Escape)));
                }
            }
        }

        /// <summary>
        /// Bir hücre değerini CSV'ye uygun hale getirir.
        /// </summary>
        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            // Baştaki sıfırlar Excel'de kaybolmasın: 00123 yazısı ="00123" olarak yazılır.
            if (Regex.IsMatch(text, @"^0\d+$"))
            {
                return "=\"" + text + "\"";
            }

            // Formül gibi başlayan yazıları Excel çalıştırmasın. Gerçek negatif sayılara dokunulmaz.
            if ("=+-@".IndexOf(text[0]) >= 0)
            {
                double number;

                if (!double.TryParse(text, NumberStyles.Any, new CultureInfo("tr-TR"), out number))
                {
                    text = "'" + text;
                }
            }

            if (text.IndexOfAny(new[] { Separator, '"', '\r', '\n' }) >= 0)
            {
                text = "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            return text;
        }
    }
}