using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace DepoStok.Data
{
    /// <summary>
    /// Ana sayfadaki arama sonucunda bulunan bir ürün tipi grubu: tipin adı ve
    /// o tipin sayfasındakiyle aynı sütunlara sahip, sadece eşleşen ürünlere
    /// daraltılmış tablo.
    /// </summary>
    public class HomeSearchGroup
    {
        public long ProductTypeId { get; set; }
        public string TypeName { get; set; }
        public DataView View { get; set; }
    }

    /// <summary>
    /// Ana sayfadaki arama kutusu için: yazılan metni tüm ürün tiplerindeki tüm
    /// metin alanlarında arar, sonuçları ürün tipine göre gruplar. Her grup, o
    /// tipin kendi ürün tipi sayfasındaki ile aynı sütunlarla (tüm alanlarla) gelir.
    /// Not: Sayı, tarih ve Evet/Hayır tipindeki alanlar bu aramaya dahil değildir,
    /// sadece yazı (metin) tipindeki alanlarda arama yapılır.
    /// </summary>
    public static class ProductSearchRepository
    {
        /// <summary>
        /// Girilen yazıyı içeren ürünleri, ürün tipine göre gruplanmış olarak verir.
        /// Silinmiş ve hurdaya ayrılmış ürünler aranmaz. En fazla maxProducts kadar
        /// ürün eşleştirilir.
        /// </summary>
        public static List<HomeSearchGroup> Search(string text, int maxProducts = 100)
        {
            var groups = new List<HomeSearchGroup>();

            text = text.Trim();
            if (text.Length == 0)
            {
                return groups;
            }

            string escaped = text
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            // 1) Eşleşen ürünlerin id'lerini, hangi tipte olduklarıyla birlikte bul.
            var matchedIdsByType = new Dictionary<long, HashSet<long>>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT DISTINCT p.Id, p.ProductTypeId " +
                    "FROM ProductValues v " +
                    "JOIN Products p ON p.Id = v.ProductId " +
                    "WHERE p.IsArchived = 0 AND p.IsScrap = 0 " +
                    "AND v.TextValue IS NOT NULL " +
                    "AND v.TextValue LIKE @pattern ESCAPE '\\' " +
                    "LIMIT @max;";
                command.Parameters.AddWithValue("@pattern", "%" + escaped + "%");
                command.Parameters.AddWithValue("@max", maxProducts);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long productId = reader.GetInt64(0);
                        long typeId = reader.GetInt64(1);

                        HashSet<long> set;
                        if (!matchedIdsByType.TryGetValue(typeId, out set))
                        {
                            set = new HashSet<long>();
                            matchedIdsByType[typeId] = set;
                        }

                        set.Add(productId);
                    }
                }
            }

            if (matchedIdsByType.Count == 0)
            {
                return groups;
            }

            // 2) Her tip için, o tipin kendi sayfasındaki tabloyu oluşturup
            //    sadece eşleşen ürünlere daralt.
            var types = ProductTypeRepository.GetAll()
                .Where(t => matchedIdsByType.ContainsKey(t.Id))
                .ToList();

            var turkish = StringComparer.Create(new CultureInfo("tr-TR"), true);
            types.Sort((a, b) => turkish.Compare(a.Name, b.Name));

            foreach (var type in types)
            {
                var properties = TypePropertyRepository.GetForType(type.Id);
                bool includeQuantity = !properties.Any(p => p.IsSerialNumber);

                DataTable table = ProductListRepository.GetTable(type.Id, properties, includeQuantity);

                var ids = matchedIdsByType[type.Id];
                string idList = string.Join(",", ids);

                var view = new DataView(table);
                view.RowFilter = "[" + ProductListRepository.IdColumn + "] IN (" + idList + ")";

                groups.Add(new HomeSearchGroup
                {
                    ProductTypeId = type.Id,
                    TypeName = type.Name,
                    View = view
                });
            }

            return groups;
        }
    }
}