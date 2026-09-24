using System.Collections.Generic;

namespace DepoStok.Data
{
    /// <summary>
    /// Teslim-Tesellüm penceresindeki arama kutularının döndürdüğü tek bir sonuç.
    /// </summary>
    public class ProductSearchResult
    {
        public string SerialNo { get; set; }
        public string TypeName { get; set; }

        /// <summary>Listede gösterilecek hazır yazı. Örnek: "Telsiz — AB1234".</summary>
        public string DisplayText
        {
            get { return TypeName + (string.IsNullOrEmpty(SerialNo) ? "" : " — " + SerialNo); }
        }
    }

    /// <summary>
    /// Teslim-Tesellüm penceresindeki Seri No ve Malzemenin cinsi kutularının arama sorgularını çalıştırır.
    /// Hurdadaki ve arşivdeki ürünler hariç, diğer tüm ürünler (zimmetli olanlar dahil) aranır.
    /// </summary>
    public static class HandoverSearchRepository
    {
        private const int MaxResults = 15;

        /// <summary>Sadece seri numarasına göre arar (Seri No kutusu için).</summary>
        public static List<ProductSearchResult> SearchBySerial(string query)
        {
            return Search(query, serialOnly: true);
        }

        /// <summary>Ürün tipi adında ya da seri numarasında arar (Malzemenin cinsi kutusu için).</summary>
        public static List<ProductSearchResult> SearchGeneral(string query)
        {
            return Search(query, serialOnly: false);
        }

        private static List<ProductSearchResult> Search(string query, bool serialOnly)
        {
            var list = new List<ProductSearchResult>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT t.Name, v.TextValue, sv.TextValue " +
                    "FROM Products p " +
                    "JOIN ProductTypes t ON t.Id = p.ProductTypeId " +
                    "LEFT JOIN ProductValues v ON v.ProductId = p.Id " +
                    "  AND v.PropertyId IN (SELECT d.Id FROM PropertyDefinitions d " +
                    "                       JOIN TypeProperties tp ON tp.PropertyId = d.Id " +
                    "                       WHERE d.IsSerialNumber = 1 AND tp.ProductTypeId = p.ProductTypeId) " +
                    "LEFT JOIN ProductValues sv ON sv.ProductId = p.Id " +
                    "  AND sv.PropertyId = (SELECT Id FROM PropertyDefinitions WHERE Name = @sysField LIMIT 1) " +
                    "WHERE p.IsScrap = 0 AND p.IsArchived = 0 AND (" +
                    (serialOnly ? "v.TextValue LIKE @q" : "t.Name LIKE @q OR v.TextValue LIKE @q OR sv.TextValue LIKE @q") +
                    ") " +
                    "ORDER BY t.Name " +
                    "LIMIT " + MaxResults + ";";
                command.Parameters.AddWithValue("@q", "%" + query + "%");
                command.Parameters.AddWithValue("@sysField", PropertyDefinitionRepository.SystemNameFieldName);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string typeName = reader.GetString(0);
                        string systemName = reader.IsDBNull(2) ? "" : reader.GetString(2);

                        list.Add(new ProductSearchResult
                        {
                            TypeName = string.IsNullOrWhiteSpace(systemName) ? typeName : systemName,
                            SerialNo = reader.IsDBNull(1) ? "" : reader.GetString(1)
                        });
                    }
                }
            }

            return list;
        }
    }
}