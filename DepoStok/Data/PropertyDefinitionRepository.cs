using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

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
        /// <summary>Her zaman kütüphanede hazır bulunan, silinemeyen/adı değiştirilemeyen sabit alan adları.</summary>
        public const string SerialNumberFieldName = "Seri Numara";
        public const string SystemNameFieldName = "Sistem Adı";

        /// <summary>
        /// "Seri Numara" ve "Sistem Adı" alanları kütüphanede yoksa oluşturur.
        /// Uygulama her açılışta çağrılır, zaten varsa hiçbir şey yapmaz.
        /// </summary>
        public static void EnsureDefaults()
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT OR IGNORE INTO PropertyDefinitions (Name, DataType, IsSerialNumber) " +
                    "VALUES (@n1, 'Text', 1);";
                command.Parameters.AddWithValue("@n1", SerialNumberFieldName);
                command.ExecuteNonQuery();
            }

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT OR IGNORE INTO PropertyDefinitions (Name, DataType, IsSerialNumber) " +
                    "VALUES (@n2, 'Text', 0);";
                command.Parameters.AddWithValue("@n2", SystemNameFieldName);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Bu alan, kütüphaneden silinemeyen/adı değiştirilemeyen sabit alanlardan biri mi?</summary>
        public static bool IsProtected(string name)
        {
            return name == SerialNumberFieldName || name == SystemNameFieldName;
        }

        /// <summary>
        /// Ürünün kendi "Sistem Adı" alanına girilmiş değeri getirir. Boşsa ya da alan o
        /// tipe hiç eklenmemişse, fallbackTypeName (genelde ürün tipinin adı) döner —
        /// böylece ekranlarda hiçbir zaman boş bir isim görünmez.
        /// </summary>
        public static string GetSystemName(long productId, string fallbackTypeName)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT v.TextValue FROM ProductValues v " +
                    "JOIN PropertyDefinitions d ON d.Id = v.PropertyId " +
                    "WHERE v.ProductId = @productId AND d.Name = @fieldName " +
                    "LIMIT 1;";
                command.Parameters.AddWithValue("@productId", productId);
                command.Parameters.AddWithValue("@fieldName", SystemNameFieldName);

                object result = command.ExecuteScalar();
                string value = result == null || result == DBNull.Value ? null : Convert.ToString(result);

                return string.IsNullOrWhiteSpace(value) ? fallbackTypeName : value;
            }
        }
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
            var property = GetAll().FirstOrDefault(p => p.Id == propertyId);

            if (property != null && IsProtected(property.Name))
            {
                throw new InvalidOperationException(
                    "\"" + property.Name + "\" sistemin sabit bir alanıdır, kütüphaneden silinemez.");
            }

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

        /// <summary>
        /// Bu alana, herhangi bir üründe (arşivde/hurdada olsun olmasın) girilmiş kaç
        /// değer olduğunu verir. Kalıcı silme öncesi bunun sıfır olması gerekir.
        /// </summary>
        public static int CountValues(long propertyId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM ProductValues WHERE PropertyId = @id;";
                command.Parameters.AddWithValue("@id", propertyId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Silinmiş (arşivdeki) bir alanı kütüphaneden kalıcı olarak siler. Sabit alanlar
        /// zaten arşivlenemediği için buraya gelmez; yine de savunma amaçlı kontrol edilir.
        /// Herhangi bir üründe bu alana girilmiş değer varsa InvalidOperationException
        /// fırlatılır. Bağlı ana sayfa istatistik kutuları da birlikte kaldırılır. Geri alınamaz.
        /// </summary>
        public static void Delete(long propertyId)
        {
            var property = GetArchived().FirstOrDefault(p => p.Id == propertyId);

            if (property != null && IsProtected(property.Name))
            {
                throw new InvalidOperationException(
                    "\"" + property.Name + "\" sistemin sabit bir alanıdır, kalıcı silinemez.");
            }

            if (CountValues(propertyId) > 0)
            {
                throw new InvalidOperationException(
                    "Bu alana en az bir üründe değer girilmiş. Kalıcı silinemez.");
            }

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM HomeStatistics WHERE PropertyId = @id;";
                    command.Parameters.AddWithValue("@id", propertyId);
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM PropertyDefinitions WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", propertyId);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
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