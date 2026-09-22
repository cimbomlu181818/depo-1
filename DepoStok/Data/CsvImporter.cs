using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DepoStok.Data
{
    /// <summary>
    /// Bir satırlık okunmuş veri: başlık adına göre hücre değerleri.
    /// </summary>
    public class CsvRow
    {
        public int LineNumber { get; set; }
        public Dictionary<string, string> Cells { get; set; }
    }

    /// <summary>
    /// Excel'den kaydedilmiş CSV dosyalarını okur. Ayraç olarak noktalı virgül veya virgülü
    /// kendisi anlar. Kendi "Dışa Aktar" özelliğimizin yazdığı, sıfırı korumak için
    /// ="00123" biçiminde sarılmış ve formül karışmasın diye başına ' konmuş hücreleri de düzgün okur.
    /// </summary>
    public static class CsvImporter
    {
        public class Result
        {
            public List<string> Headers { get; set; }
            public List<CsvRow> Rows { get; set; }
        }

        public static Result Read(string path)
        {
            List<string> lines = ReadAllLines(path);

            if (lines.Count == 0)
            {
                throw new InvalidDataException("Dosya boş.");
            }

            char separator = DetectSeparator(lines[0]);
            List<string> headers = SplitLine(lines[0], separator);

            var rows = new List<CsvRow>();

            for (int i = 1; i < lines.Count; i++)
            {
                if (lines[i].Trim().Length == 0)
                {
                    continue;
                }

                List<string> cells = SplitLine(lines[i], separator);
                var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int c = 0; c < headers.Count; c++)
                {
                    dictionary[headers[c]] = c < cells.Count ? Unwrap(cells[c]) : "";
                }

                rows.Add(new CsvRow { LineNumber = i + 1, Cells = dictionary });
            }

            return new Result { Headers = headers, Rows = rows };
        }

        /// <summary>
        /// Dosyayı, satırların içindeki tırnaklı çok satırlı hücreleri bozmadan okur.
        /// </summary>
        private static List<string> ReadAllLines(string path)
        {
            var lines = new List<string>();

            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                var current = new StringBuilder();
                bool insideQuotes = false;
                int ch;

                while ((ch = reader.Read()) != -1)
                {
                    char c = (char)ch;

                    if (c == '"')
                    {
                        insideQuotes = !insideQuotes;
                        current.Append(c);
                    }
                    else if (c == '\n' && !insideQuotes)
                    {
                        lines.Add(current.ToString().TrimEnd('\r'));
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }

                if (current.Length > 0)
                {
                    lines.Add(current.ToString().TrimEnd('\r'));
                }
            }

            return lines;
        }

        /// <summary>
        /// Başlık satırında hangi ayracın kullanıldığını tahmin eder.
        /// </summary>
        private static char DetectSeparator(string headerLine)
        {
            int semicolons = CountOutsideQuotes(headerLine, ';');
            int commas = CountOutsideQuotes(headerLine, ',');
            return semicolons >= commas ? ';' : ',';
        }

        private static int CountOutsideQuotes(string text, char target)
        {
            int count = 0;
            bool insideQuotes = false;

            foreach (char c in text)
            {
                if (c == '"')
                {
                    insideQuotes = !insideQuotes;
                }
                else if (c == target && !insideQuotes)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<string> SplitLine(string line, char separator)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool insideQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }
                }
                else if (c == separator && !insideQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }

        /// <summary>
        /// Bizim "Dışa Aktar" özelliğimizin sardığı hücreleri (="00123", 'metin) düz yazıya çevirir.
        /// </summary>
        private static string Unwrap(string text)
        {
            text = text.Trim();

            if (text.StartsWith("=\"") && text.EndsWith("\"") && text.Length >= 3)
            {
                return text.Substring(2, text.Length - 3);
            }

            if (text.StartsWith("'"))
            {
                return text.Substring(1);
            }

            return text;
        }
    }
}