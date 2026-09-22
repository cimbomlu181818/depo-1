using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Alan kütüphanesindeki bir alan tanımı (Seri No, RAM, Uzunluk gibi).
    /// </summary>
    public class PropertyDefinition
    {
        public long Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Veritabanında tutulan tür: Text, Number, Date veya YesNo.
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// Bu alan seri numarası alanı mı? (Tekil takibi bu belirler.)
        /// </summary>
        public bool IsSerialNumber { get; set; }

        public string DataTypeText
        {
            get
            {
                switch (DataType)
                {
                    case "Number": return "Sayı";
                    case "Date": return "Tarih";
                    case "YesNo": return "Evet/Hayır";
                    default: return "Metin";
                }
            }
        }

        /// <summary>
        /// Listede görünecek yazı.
        /// </summary>
        public override string ToString()
        {
            string text = Name + "  (" + DataTypeText;
            if (IsSerialNumber)
            {
                text += ", seri numarası";
            }
            return text + ")";
        }
    }

    /// <summary>
    /// Alan kütüphanesini veritabanından okur ve yeni alan ekler.
    /// </summary>
    public static class PropertyDefinitionRepository
    {
        /// <summary>
        /// Arşivde olmayan tüm alanları Türkçe alfabe sırasıyla verir.
        /// </summary>
        public static List<PropertyDefinition> GetAll()
        {
            var list = new List<PropertyDefinition>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT Id, Name, DataType, IsSerialNumber " +
                    "FROM PropertyDefinitions WHERE IsArchived = 0;";

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

            var turkish = StringComparer.Create(new CultureInfo("tr-TR"), true);
            list.Sort((a, b) => turkish.Compare(a.Name, b.Name));
            return list;
        }

        /// <summary>
        /// Alan kütüphanesine yeni bir alan ekler.
        /// </summary>
        public static void Add(string name, string dataType, bool isSerialNumber)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO PropertyDefinitions (Name, DataType, IsSerialNumber) " +
                    "VALUES (@name, @dataType, @isSerial);";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@dataType", dataType);
                command.Parameters.AddWithValue("@isSerial", isSerialNumber ? 1 : 0);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Arşivdeki (silinmiş) alanları Türkçe alfabe sırasıyla verir.
        /// </summary>
        public static List<PropertyDefinition> GetArchived()
        {
            var list = new List<PropertyDefinition>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT Id, Name, DataType, IsSerialNumber " +
                    "FROM PropertyDefinitions WHERE IsArchived = 1;";

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

            var turkish = StringComparer.Create(new CultureInfo("tr-TR"), true);
            list.Sort((a, b) => turkish.Compare(a.Name, b.Name));
            return list;
        }

        /// <summary>
        /// Bu alanın, arşivde olmayan bir ürün tipine hâlâ atanmış olup olmadığını söyler.
        /// </summary>
        public static bool IsUsedByAnyType(long propertyId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM TypeProperties tp " +
                    "JOIN ProductTypes t ON t.Id = tp.ProductTypeId " +
                    "WHERE tp.PropertyId = @id AND tp.IsArchived = 0 AND t.IsArchived = 0;";
                command.Parameters.AddWithValue("@id", propertyId);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// Alanı kütüphaneden siler (arşive alır). Herhangi bir ürün tipine atanmışsa
        /// önce oradan çıkarılması gerekir; bu durumda InvalidOperationException fırlatılır.
        /// Ürünlerdeki eski değerler silinmez, sadece görünmez olur.
        /// </summary>
        public static void Archive(long propertyId)
        {
            if (IsUsedByAnyType(propertyId))
            {
                throw new InvalidOperationException(
                    "Bu alan hâlâ bir veya daha fazla ürün tipinde kullanılıyor. " +
                    "Önce ilgili tiplerden çıkarın.");
            }

            SetArchived(propertyId, 1);
        }

        /// <summary>
        /// Arşivdeki bir alanı kütüphaneye geri getirir.
        /// </summary>
        public static void Restore(long propertyId)
        {
            SetArchived(propertyId, 0);
        }

        private static void SetArchived(long propertyId, int archived)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "UPDATE PropertyDefinitions SET IsArchived = @archived WHERE Id = @id;";
                command.Parameters.AddWithValue("@archived", archived);
                command.Parameters.AddWithValue("@id", propertyId);
                command.ExecuteNonQuery();
            }
        }
    }
}