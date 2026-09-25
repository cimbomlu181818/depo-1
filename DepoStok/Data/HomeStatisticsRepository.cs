using System;
using System.Collections.Generic;

namespace DepoStok.Data
{
    /// <summary>
    /// Ana sayfada gösterilen bir istatistik kutusu: seçilen ürün cinsi/özellik ve
    /// o özelliğin kaç üründe doldurulmuş olduğu.
    /// </summary>
    public class HomeStatisticCard
    {
        public long Id { get; set; }
        public long ProductTypeId { get; set; }
        public string ProductTypeName { get; set; }
        public long PropertyId { get; set; }
        public string PropertyName { get; set; }

        public int Count { get; set; }

        /// <summary>Kutuda gösterilecek tam yazı. Örnek: "Bilgisayar / Seri numarası: 15".</summary>
        public string DisplayText
        {
            get { return ProductTypeName + " / " + PropertyName + ": " + Count; }
        }
    }

    /// <summary>
    /// Ana sayfadaki istatistik kutusu seçimlerini kalıcı olarak saklar ve sayılarını hesaplar.
    /// Sayı, seçilen ürün cinsinde o özelliğin kaç ÜRÜNDE doldurulmuş olduğudur
    /// (ProductValues'ta o alan için satır var mı) — arşivdeki ve hurdadaki ürünler sayılmaz.
    /// </summary>
    public static class HomeStatisticsRepository
    {
        /// <summary>
        /// Kayıtlı tüm istatistik kutularını eklenme sırasına göre, güncel sayılarıyla verir.
        /// </summary>
        public static List<HomeStatisticCard> GetAll()
        {
            var list = new List<HomeStatisticCard>();

            using (var connection = Database.OpenConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT h.Id, h.ProductTypeId, t.Name, h.PropertyId, p.Name " +
                        "FROM HomeStatistics h " +
                        "JOIN ProductTypes t ON t.Id = h.ProductTypeId " +
                        "JOIN PropertyDefinitions p ON p.Id = h.PropertyId " +
                        "ORDER BY h.SortOrder, h.Id;";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new HomeStatisticCard
                            {
                                Id = reader.GetInt64(0),
                                ProductTypeId = reader.GetInt64(1),
                                ProductTypeName = reader.GetString(2),
                                PropertyId = reader.GetInt64(3),
                                PropertyName = reader.GetString(4)
                            });
                        }
                    }
                }

                foreach (var card in list)
                {
                    card.Count = CountFilled(connection, card.ProductTypeId, card.PropertyId);
                }
            }

            return list;
        }

        private static int CountFilled(System.Data.SQLite.SQLiteConnection connection, long productTypeId, long propertyId)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM ProductValues v " +
                    "JOIN Products p ON p.Id = v.ProductId " +
                    "WHERE v.ProductTypeId = @typeId AND v.PropertyId = @propertyId " +
                    "AND p.IsArchived = 0 AND p.IsScrap = 0;";
                command.Parameters.AddWithValue("@typeId", productTypeId);
                command.Parameters.AddWithValue("@propertyId", propertyId);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Yeni bir istatistik kutusu ekler, listenin en sonuna koyar.
        /// </summary>
        public static void Add(long productTypeId, long propertyId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO HomeStatistics (ProductTypeId, PropertyId, SortOrder) " +
                    "VALUES (@typeId, @propertyId, " +
                    "COALESCE((SELECT MAX(SortOrder) FROM HomeStatistics), 0) + 1);";
                command.Parameters.AddWithValue("@typeId", productTypeId);
                command.Parameters.AddWithValue("@propertyId", propertyId);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Var olan bir kutunun gösterdiği ürün cinsi/özelliği değiştirir. Sırası aynı kalır.
        /// </summary>
        public static void Update(long id, long productTypeId, long propertyId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "UPDATE HomeStatistics SET ProductTypeId = @typeId, PropertyId = @propertyId " +
                    "WHERE Id = @id;";
                command.Parameters.AddWithValue("@typeId", productTypeId);
                command.Parameters.AddWithValue("@propertyId", propertyId);
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Bir istatistik kutusunu kalıcı olarak kaldırır.
        /// </summary>
        public static void Remove(long id)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM HomeStatistics WHERE Id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }
    }
}