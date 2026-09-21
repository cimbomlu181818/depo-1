using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace DepoStok.Data
{
    /// <summary>
    /// Ana sayfadaki "Son işlem yapılan cihazlar" listesinde görünen bir satır.
    /// </summary>
    public class RecentProductInfo
    {
        public long ProductId { get; set; }
        public long ProductTypeId { get; set; }
        public string TypeName { get; set; }

        /// <summary>
        /// Ürünün kısa tanımı. Örnek: "Seri No: ABC123  |  Marka: Dell  |  RAM: 16".
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// "Eklendi", "Güncellendi" ya da "Hurdada".
        /// </summary>
        public string ActionText { get; set; }

        /// <summary>
        /// Son işlemin zamanı. Örnek: "20.09.2026 23:41".
        /// </summary>
        public string TimeText { get; set; }

        /// <summary>
        /// Arşiv ekranında satırın onay kutusu işaretli mi?
        /// </summary>
        public bool IsChecked { get; set; }
    }

    /// <summary>
    /// Ürünleri tüm ürün tiplerinden toplayıp son işlem zamanına göre en yeniden eskiye doğru verir.
    /// GetRecent normal ürünleri, GetArchived silinmiş (arşive alınmış) ürünleri getirir.
    /// </summary>
    public static class RecentProductsRepository
    {
        private class ValuePart
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public bool IsSerial { get; set; }
        }

        /// <summary>
        /// Silinmemiş ürünlerden en son işlem yapılanları verir.
        /// </summary>
        public static List<RecentProductInfo> GetRecent(int count)
        {
            return Load(false, count);
        }

        /// <summary>
        /// Silinmiş (arşive alınmış) ürünleri, en son silinen en üstte olacak şekilde verir.
        /// </summary>
        public static List<RecentProductInfo> GetArchived(int count)
        {
            return Load(true, count);
        }

        private static List<RecentProductInfo> Load(bool archived, int count)
        {
            var list = new List<RecentProductInfo>();
            var quantities = new Dictionary<long, int>();

            using (var connection = Database.OpenConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT p.Id, p.ProductTypeId, t.Name, p.IsScrap, p.Quantity, " +
                        "p.CreatedAt, p.UpdatedAt " +
                        "FROM Products p " +
                        "JOIN ProductTypes t ON t.Id = p.ProductTypeId " +
                        "WHERE p.IsArchived = @archived AND t.IsArchived = 0 " +
                        "ORDER BY COALESCE(p.UpdatedAt, p.CreatedAt) DESC, p.Id DESC " +
                        "LIMIT @count;";
                    command.Parameters.AddWithValue("@archived", archived ? 1 : 0);
                    command.Parameters.AddWithValue("@count", count);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long productId = reader.GetInt64(0);
                            bool isScrap = reader.GetInt32(3) == 1;
                            string createdAt = reader.GetString(5);
                            string updatedAt = reader.IsDBNull(6) ? null : reader.GetString(6);

                            string actionText;
                            if (archived)
                            {
                                actionText = isScrap ? "Silindi (hurdadaydı)" : "Silindi";
                            }
                            else if (isScrap)
                            {
                                actionText = "Hurdada";
                            }
                            else if (updatedAt == null)
                            {
                                actionText = "Eklendi";
                            }
                            else
                            {
                                actionText = "Güncellendi";
                            }

                            list.Add(new RecentProductInfo
                            {
                                ProductId = productId,
                                ProductTypeId = reader.GetInt64(1),
                                TypeName = reader.GetString(2),
                                ActionText = actionText,
                                TimeText = FormatTime(updatedAt ?? createdAt)
                            });

                            quantities[productId] = reader.GetInt32(4);
                        }
                    }
                }

                if (list.Count == 0)
                {
                    return list;
                }

                Dictionary<long, List<ValuePart>> parts = ReadValues(connection, list);

                foreach (var item in list)
                {
                    List<ValuePart> itemParts;
                    if (!parts.TryGetValue(item.ProductId, out itemParts))
                    {
                        itemParts = new List<ValuePart>();
                    }

                    item.Description = BuildDescription(itemParts, quantities[item.ProductId]);
                }
            }

            return list;
        }

        /// <summary>
        /// Listedeki ürünlerin değerlerini, tipteki alan sırasıyla okur.
        /// </summary>
        private static Dictionary<long, List<ValuePart>> ReadValues(
            SQLiteConnection connection,
            List<RecentProductInfo> products)
        {
            var result = new Dictionary<long, List<ValuePart>>();
            var turkish = new CultureInfo("tr-TR");

            // Numaralar veritabanından gelen tam sayılardır, doğrudan yazmak güvenlidir.
            string idList = string.Join(",", products.Select(p => p.ProductId));

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT v.ProductId, d.Name, d.DataType, d.IsSerialNumber, " +
                    "v.TextValue, v.NumberValue, v.DateValue " +
                    "FROM ProductValues v " +
                    "JOIN PropertyDefinitions d ON d.Id = v.PropertyId " +
                    "LEFT JOIN TypeProperties tp " +
                    "ON tp.ProductTypeId = v.ProductTypeId AND tp.PropertyId = v.PropertyId " +
                    "WHERE v.ProductId IN (" + idList + ") " +
                    "ORDER BY v.ProductId, tp.SortOrder, tp.Id;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long productId = reader.GetInt64(0);
                        string dataType = reader.GetString(2);
                        string text = null;

                        if (!reader.IsDBNull(4))
                        {
                            text = reader.GetString(4);
                        }
                        else if (!reader.IsDBNull(5))
                        {
                            double number = reader.GetDouble(5);

                            text = dataType == "YesNo"
                                ? (number == 1 ? "Evet" : "Hayır")
                                : number.ToString("0.######", turkish);
                        }
                        else if (!reader.IsDBNull(6))
                        {
                            string raw = reader.GetString(6);
                            DateTime date;

                            text = DateTime.TryParseExact(raw, "yyyy-MM-dd",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                                ? date.ToString("dd.MM.yyyy", turkish)
                                : raw;
                        }

                        if (string.IsNullOrEmpty(text))
                        {
                            continue;
                        }

                        List<ValuePart> list;
                        if (!result.TryGetValue(productId, out list))
                        {
                            list = new List<ValuePart>();
                            result[productId] = list;
                        }

                        list.Add(new ValuePart
                        {
                            Name = reader.GetString(1),
                            Value = text,
                            IsSerial = reader.GetInt32(3) == 1
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Seri numarası varsa onu, yoksa miktarı başa koyar; ardından ilk iki değeri ekler.
        /// </summary>
        private static string BuildDescription(List<ValuePart> parts, int quantity)
        {
            var texts = new List<string>();

            ValuePart serial = parts.FirstOrDefault(p => p.IsSerial);

            if (serial != null)
            {
                texts.Add(serial.Name + ": " + serial.Value);
            }
            else
            {
                texts.Add("Miktar: " + quantity);
            }

            foreach (var part in parts.Where(p => !p.IsSerial).Take(2))
            {
                texts.Add(part.Name + ": " + part.Value);
            }

            return string.Join("  |  ", texts);
        }

        private static string FormatTime(string raw)
        {
            DateTime time;

            if (DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
            {
                return time.ToString("dd.MM.yyyy HH:mm", new CultureInfo("tr-TR"));
            }

            return raw;
        }
    }
}