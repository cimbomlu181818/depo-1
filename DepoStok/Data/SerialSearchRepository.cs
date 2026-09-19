using System;
using System.Data.SQLite;

namespace DepoStok.Data
{
    /// <summary>
    /// Seri numarasına göre ürün arar (tüm ürün tiplerinde).
    /// </summary>
    public static class SerialSearchRepository
    {
        /// <summary>
        /// Girilen yazıyı seri numarasında içeren ilk ürünün ürün tipi numarasını verir.
        /// Tam eşleşen varsa o öne alınır. Hiç ürün yoksa null verir.
        /// Silinmiş ve hurdaya ayrılmış ürünler aranmaz.
        /// </summary>
        public static long? FindTypeIdBySerial(string serial)
        {
            serial = serial.Trim();

            string escaped = serial
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT p.ProductTypeId " +
                    "FROM ProductValues v " +
                    "JOIN PropertyDefinitions d ON d.Id = v.PropertyId " +
                    "JOIN Products p ON p.Id = v.ProductId " +
                    "WHERE d.IsSerialNumber = 1 " +
                    "AND p.IsArchived = 0 AND p.IsScrap = 0 " +
                    "AND v.TextValue LIKE @pattern ESCAPE '\\' " +
                    "ORDER BY (v.TextValue = @exact COLLATE NOCASE) DESC, p.Id " +
                    "LIMIT 1;";
                command.Parameters.AddWithValue("@pattern", "%" + escaped + "%");
                command.Parameters.AddWithValue("@exact", serial);

                object result = command.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToInt64(result);
            }
        }
    }
}