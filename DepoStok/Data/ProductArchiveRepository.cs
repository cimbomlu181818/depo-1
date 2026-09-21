using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Ürünleri arşive alır (silme), hurdaya taşır veya hurdadan geri alır.
    /// Ürün veritabanından yok olmaz, sadece ilgili listelerde görünür ya da görünmez.
    /// </summary>
    public static class ProductArchiveRepository
    {
        /// <summary>
        /// Verilen ürünleri arşive alır (silme) ve işlem loguna yazar.
        /// Hepsi tek seferde yapılır: ya hepsi arşive alınır ya da hiçbiri.
        /// </summary>
        public static void Archive(long productTypeId, List<long> productIds)
        {
            Change(productTypeId, productIds,
                "IsArchived", 1, "Ürün silindi (arşive alındı)");
        }

        /// <summary>
        /// Verilen ürünleri hurdaya taşır ve işlem loguna yazar.
        /// </summary>
        public static void MoveToScrap(long productTypeId, List<long> productIds)
        {
            Change(productTypeId, productIds,
                "IsScrap", 1, "Ürün hurdaya taşındı");
        }

        /// <summary>
        /// Verilen ürünleri hurdadan çıkarıp normal listeye geri alır ve işlem loguna yazar.
        /// </summary>
        public static void RestoreFromScrap(long productTypeId, List<long> productIds)
        {
            Change(productTypeId, productIds,
                "IsScrap", 0, "Ürün hurdadan normal listeye alındı");
        }

        /// <summary>
        /// Silinmiş (arşive alınmış) ürünleri geri getirir ve işlem loguna yazar.
        /// Ürün, silinmeden önceki yerine döner (normal listeye ya da hurdaya).
        /// </summary>
        public static void RestoreFromArchive(long productTypeId, List<long> productIds)
        {
            Change(productTypeId, productIds,
                "IsArchived", 0, "Ürün arşivden geri alındı");
        }

        private static void Change(
            long productTypeId,
            List<long> productIds,
            string columnName,
            int newValue,
            string actionText)
        {
            if (productIds == null || productIds.Count == 0)
            {
                return;
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                foreach (long productId in productIds)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            "UPDATE Products SET " + columnName + " = @value, UpdatedAt = @now " +
                            "WHERE Id = @id AND ProductTypeId = @typeId;";
                        command.Parameters.AddWithValue("@value", newValue);
                        command.Parameters.AddWithValue("@now", now);
                        command.Parameters.AddWithValue("@id", productId);
                        command.Parameters.AddWithValue("@typeId", productTypeId);
                        command.ExecuteNonQuery();
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO ActionLogs (CreatedAt, UserName, Action, Details) " +
                        "VALUES (@createdAt, @userName, @action, @details);";
                    command.Parameters.AddWithValue("@createdAt", now);
                    command.Parameters.AddWithValue("@userName", Environment.UserName);
                    command.Parameters.AddWithValue("@action", actionText);
                    command.Parameters.AddWithValue("@details",
                        LogRepository.DescribeProducts(
                            connection, transaction, productTypeId, productIds));
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }
    }
}