using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace DepoStok.Data
{
    /// <summary>
    /// İşlem geçmişinde görünen bir satır.
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// İşlemin zamanı. Örnek: "20.09.2026 23:41:08".
        /// </summary>
        public string TimeText { get; set; }

        public string UserName { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// İşlem logunu (kim, ne zaman, ne yaptı) yazar ve okur.
    /// </summary>
    public static class LogRepository
    {
        /// <summary>
        /// Loga yeni bir satır ekler. Kullanıcı adı olarak Windows kullanıcı adı yazılır.
        /// </summary>
        public static void Write(string action, string details)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO ActionLogs (CreatedAt, UserName, Action, Details) " +
                    "VALUES (@createdAt, @userName, @action, @details);";
                command.Parameters.AddWithValue("@createdAt", now);
                command.Parameters.AddWithValue("@userName", Environment.UserName);
                command.Parameters.AddWithValue("@action", action);
                command.Parameters.AddWithValue("@details", details);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Log için ürünleri okunur bir yazıya çevirir.
        /// Örnek: "Bilgisayar, Seri No: ABC123 (ürün no: 4)".
        /// Seri numarası yoksa: "Telsiz (ürün no: 7)". En fazla 10 ürün yazılır.
        /// Ürün işlemiyle aynı bağlantı ve işlem (transaction) içinde çağrılmalıdır.
        /// </summary>
        public static string DescribeProducts(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            long productTypeId,
            IList<long> productIds)
        {
            string typeName = null;

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT Name FROM ProductTypes WHERE Id = @id;";
                command.Parameters.AddWithValue("@id", productTypeId);

                object result = command.ExecuteScalar();
                typeName = result == null ? "ürün tipi no: " + productTypeId : Convert.ToString(result);
            }

            var parts = new List<string>();

            foreach (long productId in productIds.Take(10))
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "SELECT d.Name, v.TextValue " +
                        "FROM ProductValues v " +
                        "JOIN PropertyDefinitions d ON d.Id = v.PropertyId " +
                        "WHERE v.ProductId = @productId AND d.IsSerialNumber = 1 " +
                        "LIMIT 1;";
                    command.Parameters.AddWithValue("@productId", productId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(1))
                        {
                            parts.Add(typeName + ", " + reader.GetString(0) + ": " +
                                      reader.GetString(1) + " (ürün no: " + productId + ")");
                        }
                        else
                        {
                            parts.Add(typeName + " (ürün no: " + productId + ")");
                        }
                    }
                }
            }

            string text = string.Join("; ", parts);

            if (productIds.Count > 10)
            {
                text += "; ve " + (productIds.Count - 10) + " ürün daha";
            }

            return text;
        }

        /// <summary>
        /// En son yapılan işlemleri, en yeniden eskiye doğru verir.
        /// </summary>
        public static List<LogEntry> GetRecent(int count)
        {
            var list = new List<LogEntry>();
            var turkish = new CultureInfo("tr-TR");

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT CreatedAt, UserName, Action, Details " +
                    "FROM ActionLogs ORDER BY Id DESC LIMIT @count;";
                command.Parameters.AddWithValue("@count", count);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string raw = reader.GetString(0);
                        DateTime time;

                        string timeText = DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                            ? time.ToString("dd.MM.yyyy HH:mm:ss", turkish)
                            : raw;

                        list.Add(new LogEntry
                        {
                            TimeText = timeText,
                            UserName = reader.GetString(1),
                            Action = reader.GetString(2),
                            Details = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
            }

            return list;
        }
    }
}