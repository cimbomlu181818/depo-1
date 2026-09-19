using System.Collections.Generic;
using System.Data.SQLite;

namespace DepoStok.Data
{
    /// <summary>
    /// Bir ürün tipine (Bilgisayar, Telsiz...) hangi alanların bağlı olduğunu okur ve yönetir.
    /// </summary>
    public static class TypePropertyRepository
    {
        /// <summary>
        /// Bir ürün tipine bağlı alanları, eklenme sırasına göre verir.
        /// Arşivdeki alanlar gelmez.
        /// </summary>
        public static List<PropertyDefinition> GetForType(long productTypeId)
        {
            var list = new List<PropertyDefinition>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT p.Id, p.Name, p.DataType, p.IsSerialNumber " +
                    "FROM TypeProperties tp " +
                    "JOIN PropertyDefinitions p ON p.Id = tp.PropertyId " +
                    "WHERE tp.ProductTypeId = @typeId " +
                    "AND tp.IsArchived = 0 " +
                    "AND p.IsArchived = 0 " +
                    "ORDER BY tp.SortOrder, tp.Id;";
                command.Parameters.AddWithValue("@typeId", productTypeId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new PropertyDefinition
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1),
                            DataType = reader.GetString(2),
                            IsSerialNumber = reader.GetInt32(3) == 1
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Bir alanı ürün tipine bağlar.
        /// Daha önce bağlanıp arşive alınmışsa arşivden geri getirir.
        /// </summary>
        public static void Add(long productTypeId, long propertyId)
        {
            using (var connection = Database.OpenConnection())
            {
                using (var update = connection.CreateCommand())
                {
                    update.CommandText =
                        "UPDATE TypeProperties SET IsArchived = 0 " +
                        "WHERE ProductTypeId = @typeId AND PropertyId = @propertyId;";
                    update.Parameters.AddWithValue("@typeId", productTypeId);
                    update.Parameters.AddWithValue("@propertyId", propertyId);

                    if (update.ExecuteNonQuery() > 0)
                    {
                        return;
                    }
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.CommandText =
                        "INSERT INTO TypeProperties (ProductTypeId, PropertyId, SortOrder) " +
                        "VALUES (@typeId, @propertyId, " +
                        "COALESCE((SELECT MAX(SortOrder) FROM TypeProperties " +
                        "WHERE ProductTypeId = @typeId), 0) + 1);";
                    insert.Parameters.AddWithValue("@typeId", productTypeId);
                    insert.Parameters.AddWithValue("@propertyId", propertyId);
                    insert.ExecuteNonQuery();
                }
            }
        }
    }
}