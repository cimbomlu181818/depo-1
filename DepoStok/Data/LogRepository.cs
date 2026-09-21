using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

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