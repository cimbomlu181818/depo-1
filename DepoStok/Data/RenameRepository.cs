using System.Data.SQLite;

namespace DepoStok.Data
{
    /// <summary>
    /// Ürün tipi ve alan adlarını değiştirir.
    /// Ürünler ve değerler adla değil numarayla bağlı olduğu için, ad değişince hiçbir kayıt bozulmaz.
    /// </summary>
    public static class RenameRepository
    {
        /// <summary>
        /// Ürün tipinin adını değiştirir.
        /// </summary>
        public static void RenameProductType(long productTypeId, string newName)
        {
            Rename("ProductTypes", productTypeId, newName);
        }

        /// <summary>
        /// Alan kütüphanesindeki bir alanın adını değiştirir.
        /// </summary>
        public static void RenameProperty(long propertyId, string newName)
        {
            Rename("PropertyDefinitions", propertyId, newName);
        }

        private static void Rename(string tableName, long id, string newName)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                // Tablo adı yalnızca yukarıdaki iki sabit yazıdan gelir, kullanıcıdan gelmez.
                command.CommandText = "UPDATE " + tableName + " SET Name = @name WHERE Id = @id;";
                command.Parameters.AddWithValue("@name", newName);
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }
    }
}