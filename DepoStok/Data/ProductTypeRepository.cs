using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Bir ürün tipi (Bilgisayar, Telsiz, HDMI Kablo...).
    /// </summary>
    public class ProductType
    {
        public long Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Listelerde görünecek yazı.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Ürün tiplerini veritabanından okur ve ekler.
    /// </summary>
    public static class ProductTypeRepository
    {
        /// <summary>
        /// Arşivde olmayan ürün tiplerini (numarasıyla birlikte) Türkçe alfabe sırasıyla verir.
        /// </summary>
        public static List<ProductType> GetAll()
        {
            var list = new List<ProductType>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Name FROM ProductTypes WHERE IsArchived = 0;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ProductType
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            var turkish = StringComparer.Create(new CultureInfo("tr-TR"), true);
            list.Sort((a, b) => turkish.Compare(a.Name, b.Name));
            return list;
        }

        /// <summary>
        /// Arşivde olmayan ürün tiplerinin sadece adlarını Türkçe alfabe sırasıyla verir.
        /// </summary>
        public static List<string> GetNames()
        {
            var names = new List<string>();

            foreach (var type in GetAll())
            {
                names.Add(type.Name);
            }

            return names;
        }

        /// <summary>
        /// Yeni bir ürün tipi ekler.
        /// </summary>
        public static void Add(string name)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO ProductTypes (Name) VALUES (@name);";
                command.Parameters.AddWithValue("@name", name);
                command.ExecuteNonQuery();
            }
        }
    }
}