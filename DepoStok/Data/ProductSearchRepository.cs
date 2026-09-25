using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Ana sayfadaki arama kutusunun tek bir eşleşmesi.
    /// </summary>
    public class HomeSearchResult
    {
        public long ProductId { get; set; }
        public long ProductTypeId { get; set; }
        public string TypeName { get; set; }
        public string FieldName { get; set; }
        public string MatchText { get; set; }

        /// <summary>
        /// Listede görünecek yazı. Örnek: "Telsiz — Seri No: TS-1234".
        /// </summary>
        public override string ToString()
        {
            return TypeName + " — " + FieldName + ": " + MatchText;
        }
    }

    /// <summary>
    /// Ana sayfadaki arama kutusu için: yazılan metni tüm ürün tiplerindeki
    /// tüm metin alanlarında arar (sadece Seri No'da değil).
    /// Not: Sayı, tarih ve Evet/Hayır tipindeki alanlar bu aramaya dahil değildir,
    /// sadece yazı (metin) tipindeki alanlarda arama yapılır.
    /// </summary>
    public static class ProductSearchRepository
    {
        /// <summary>
        /// Girilen yazıyı içeren ürünleri, hangi alanda eşleştiğiyle birlikte verir.
        /// Tam eşleşen üstte gelir. Silinmiş ve hurdaya ayrılmış ürünler aranmaz.
        /// En fazla maxResults kadar sonuç döner.
        /// </summary>
        public static List<HomeSearchResult> Search(string text, int maxResults = 20)
        {
            var results = new List<HomeSearchResult>();

            text = text.Trim();
            if (text.Length == 0)
            {
                return results;
            }

            string escaped = text
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT p.Id, p.ProductTypeId, t.Name, d.Name, v.TextValue " +
                    "FROM ProductValues v " +
                    "JOIN PropertyDefinitions d ON d.Id = v.PropertyId " +
                    "JOIN Products p ON p.Id = v.ProductId " +
                    "JOIN ProductTypes t ON t.Id = p.ProductTypeId " +
                    "WHERE p.IsArchived = 0 AND p.IsScrap = 0 " +
                    "AND v.TextValue IS NOT NULL " +
                    "AND v.TextValue LIKE @pattern ESCAPE '\\' " +
                    "ORDER BY (v.TextValue = @exact COLLATE NOCASE) DESC, p.Id " +
                    "LIMIT @max;";
                command.Parameters.AddWithValue("@pattern", "%" + escaped + "%");
                command.Parameters.AddWithValue("@exact", text);
                command.Parameters.AddWithValue("@max", maxResults);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new HomeSearchResult
                        {
                            ProductId = reader.GetInt64(0),
                            ProductTypeId = reader.GetInt64(1),
                            TypeName = reader.GetString(2),
                            FieldName = reader.GetString(3),
                            MatchText = reader.IsDBNull(4) ? "" : reader.GetString(4)
                        });
                    }
                }
            }

            return results;
        }
    }
}