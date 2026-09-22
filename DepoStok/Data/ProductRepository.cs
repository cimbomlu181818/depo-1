using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Bir ürün düzenlenirken değişen tek bir alan.
    /// Değerler kaydedilen biçimdedir: sayı "1234.5", tarih "2026-09-20", Evet/Hayır "1" veya "0".
    /// </summary>
    public class ProductChange
    {
        public PropertyDefinition Property { get; set; }

        /// <summary>
        /// Eski değer. Boş ya da null ise alan boştu.
        /// </summary>
        public string OldValue { get; set; }

        /// <summary>
        /// Yeni değer. Boşsa alan temizlendi demektir.
        /// </summary>
        public string NewValue { get; set; }
    }

    /// <summary>
    /// Gerçek ürünleri (Lenovo ThinkPad, HDMI kablo gibi) veritabanına kaydeder, okur ve günceller.
    /// </summary>
    public static class ProductRepository
    {
        /// <summary>
        /// Sayıyı her yerde aynı biçimde yazmak için kullanılır (eski ve yeni değeri karşılaştırabilmek için).
        /// </summary>
        public static string NormalizeNumber(double number)
        {
            return number.ToString("0.##########", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Bu seri numarası daha önce kaydedilmiş mi?
        /// </summary>
        public static bool SerialNumberExists(long serialPropertyId, string serial)
        {
            return SerialNumberExists(serialPropertyId, serial, 0);
        }

        /// <summary>
        /// Bu seri numarası, verilen ürün dışında başka bir üründe kayıtlı mı?
        /// (Ürünü düzenlerken kendi seri numarası sayılmasın diye kullanılır.)
        /// </summary>
        public static bool SerialNumberExists(long serialPropertyId, string serial, long excludeProductId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM ProductValues " +
                    "WHERE PropertyId = @propertyId AND TextValue = @serial COLLATE NOCASE " +
                    "AND ProductId <> @excludeId;";
                command.Parameters.AddWithValue("@propertyId", serialPropertyId);
                command.Parameters.AddWithValue("@serial", serial.Trim());
                command.Parameters.AddWithValue("@excludeId", excludeProductId);

                return Convert.ToInt64(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// Bir ürünün kayıtlı değerlerini verir: alan numarası -> değer.
        /// Değer biçimi: sayı "1234.5", tarih "2026-09-20", Evet/Hayır "1" veya "0".
        /// Hiç değeri girilmemiş alanlar listede olmaz.
        /// </summary>
        public static Dictionary<long, string> GetValues(long productId)
        {
            var result = new Dictionary<long, string>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT PropertyId, TextValue, NumberValue, DateValue " +
                    "FROM ProductValues WHERE ProductId = @productId;";
                command.Parameters.AddWithValue("@productId", productId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long propertyId = reader.GetInt64(0);

                        if (!reader.IsDBNull(1))
                        {
                            result[propertyId] = reader.GetString(1);
                        }
                        else if (!reader.IsDBNull(2))
                        {
                            result[propertyId] = NormalizeNumber(reader.GetDouble(2));
                        }
                        else if (!reader.IsDBNull(3))
                        {
                            result[propertyId] = reader.GetString(3);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Bir ürünün kayıtlı miktarını verir.
        /// </summary>
        public static int GetQuantity(long productId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Quantity FROM Products WHERE Id = @id;";
                command.Parameters.AddWithValue("@id", productId);

                object value = command.ExecuteScalar();

                if (value == null || value == DBNull.Value)
                {
                    return 1;
                }

                return Convert.ToInt32(value);
            }
        }

        /// <summary>
        /// Yeni bir ürün ekler ve işlem loguna yazar. Ürünün numarasını verir.
        /// Değerler: alan numarası -> kullanıcının girdiği yazı.
        /// Sayı "1234.5", tarih "2026-09-20", Evet/Hayır "1" veya "0" biçiminde gelir.
        /// Boş bırakılan alanlar kaydedilmez.
        /// </summary>
        public static long Add(
            long productTypeId,
            int quantity,
            List<PropertyDefinition> properties,
            Dictionary<long, string> values)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                long productId;

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO Products (ProductTypeId, Quantity, CreatedAt) " +
                        "VALUES (@typeId, @quantity, @createdAt);";
                    command.Parameters.AddWithValue("@typeId", productTypeId);
                    command.Parameters.AddWithValue("@quantity", quantity);
                    command.Parameters.AddWithValue("@createdAt", now);
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT last_insert_rowid();";
                    productId = Convert.ToInt64(command.ExecuteScalar());
                }

                foreach (var property in properties)
                {
                    string value;
                    if (!values.TryGetValue(property.Id, out value) || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    InsertValue(connection, transaction, productId, productTypeId, property, value.Trim());
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO ActionLogs (CreatedAt, UserName, Action, Details) " +
                        "VALUES (@createdAt, @userName, @action, @details);";
                    command.Parameters.AddWithValue("@createdAt", now);
                    command.Parameters.AddWithValue("@userName", Environment.UserName);
                    command.Parameters.AddWithValue("@action", "Ürün eklendi");
                    command.Parameters.AddWithValue("@details",
                        LogRepository.DescribeProducts(
                            connection, transaction, productTypeId, new List<long> { productId }) +
                        ", miktar: " + quantity);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
                return productId;
            }
        }

        /// <summary>
        /// Bir ürünün değişen bilgilerini kaydeder ve değişenleri işlem loguna yazar.
        /// Hepsi tek seferde yapılır: ya hepsi kaydolur ya da hiçbiri.
        /// newQuantity boşsa (seri numaralı tip) miktara dokunulmaz.
        /// </summary>
        public static void Update(
            long productTypeId,
            long productId,
            int oldQuantity,
            int? newQuantity,
            List<ProductChange> changes)
        {
            bool quantityChanged = newQuantity.HasValue && newQuantity.Value != oldQuantity;

            var logParts = new List<string>();

            if (quantityChanged)
            {
                logParts.Add("Miktar: " + oldQuantity + " → " + newQuantity.Value);
            }

            foreach (var change in changes)
            {
                logParts.Add(
                    change.Property.Name + ": " +
                    Describe(change.Property.DataType, change.OldValue) + " → " +
                    Describe(change.Property.DataType, change.NewValue));
            }

            if (logParts.Count == 0)
            {
                return;
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "UPDATE Products SET Quantity = @quantity, UpdatedAt = @now " +
                        "WHERE Id = @id AND ProductTypeId = @typeId;";
                    command.Parameters.AddWithValue("@quantity", quantityChanged ? newQuantity.Value : oldQuantity);
                    command.Parameters.AddWithValue("@now", now);
                    command.Parameters.AddWithValue("@id", productId);
                    command.Parameters.AddWithValue("@typeId", productTypeId);
                    command.ExecuteNonQuery();
                }

                foreach (var change in changes)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            "DELETE FROM ProductValues WHERE ProductId = @productId AND PropertyId = @propertyId;";
                        command.Parameters.AddWithValue("@productId", productId);
                        command.Parameters.AddWithValue("@propertyId", change.Property.Id);
                        command.ExecuteNonQuery();
                    }

                    if (!string.IsNullOrWhiteSpace(change.NewValue))
                    {
                        InsertValue(connection, transaction, productId, productTypeId,
                            change.Property, change.NewValue.Trim());
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
                    command.Parameters.AddWithValue("@action", "Ürün düzenlendi");
                    command.Parameters.AddWithValue("@details",
                        LogRepository.DescribeProducts(
                            connection, transaction, productTypeId, new List<long> { productId }) +
                        ". " + string.Join("; ", logParts));
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        /// <summary>
        /// Verilen ürünlerden, bu alanı boş olanlara aynı değeri yazar. Dolu olanlara dokunmaz.
        /// Hepsi tek seferde yapılır: ya hepsi kaydolur ya da hiçbiri. İşlem loguna da yazılır.
        /// Seri numarası alanı toplu doldurulamaz, çünkü her ürünün seri numarası farklı olmalıdır.
        /// value veritabanına yazılacak hâl (Evet/Hayır için "1" ya da "0", tarih için "yyyy-MM-dd"),
        /// displayValue ise loga yazılacak okunur hâldir.
        /// Kaç ürünün doldurulduğunu verir.
        /// </summary>
        public static int FillEmptyValues(
            long productTypeId,
            PropertyDefinition property,
            string value,
            string displayValue,
            List<long> productIds)
        {
            if (property.IsSerialNumber)
            {
                throw new InvalidOperationException("Seri numarası alanı toplu doldurulamaz.");
            }

            if (productIds == null || productIds.Count == 0)
            {
                return 0;
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            int filled = 0;

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                foreach (long productId in productIds)
                {
                    using (var check = connection.CreateCommand())
                    {
                        check.Transaction = transaction;
                        check.CommandText =
                            "SELECT COUNT(*) FROM ProductValues " +
                            "WHERE ProductId = @productId AND PropertyId = @propertyId;";
                        check.Parameters.AddWithValue("@productId", productId);
                        check.Parameters.AddWithValue("@propertyId", property.Id);

                        if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                        {
                            continue;
                        }
                    }

                    InsertValue(connection, transaction, productId, productTypeId, property, value.Trim());

                    using (var update = connection.CreateCommand())
                    {
                        update.Transaction = transaction;
                        update.CommandText =
                            "UPDATE Products SET UpdatedAt = @now " +
                            "WHERE Id = @id AND ProductTypeId = @typeId;";
                        update.Parameters.AddWithValue("@now", now);
                        update.Parameters.AddWithValue("@id", productId);
                        update.Parameters.AddWithValue("@typeId", productTypeId);
                        update.ExecuteNonQuery();
                    }

                    filled++;
                }

                if (filled > 0)
                {
                    string typeName;

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "SELECT Name FROM ProductTypes WHERE Id = @id;";
                        command.Parameters.AddWithValue("@id", productTypeId);

                        object result = command.ExecuteScalar();
                        typeName = result == null ? "ürün tipi no: " + productTypeId : Convert.ToString(result);
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            "INSERT INTO ActionLogs (CreatedAt, UserName, Action, Details) " +
                            "VALUES (@createdAt, @userName, @action, @details);";
                        command.Parameters.AddWithValue("@createdAt", now);
                        command.Parameters.AddWithValue("@userName", Environment.UserName);
                        command.Parameters.AddWithValue("@action", "Boş alanlar toplu dolduruldu");
                        command.Parameters.AddWithValue("@details",
                            "Ürün tipi: " + typeName + ", alan: " + property.Name +
                            ", değer: " + displayValue + ", " + filled + " ürün");
                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }

            return filled;
        }

        /// <summary>
        /// Bir değeri ProductValues tablosuna, türüne uygun sütuna yazar.
        /// </summary>
        /// <summary>
        /// İçe aktarma gibi başka Data sınıflarının da kullanabilmesi için "internal" yapıldı.
        /// </summary>
        internal static void InsertValue(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            long productId,
            long productTypeId,
            PropertyDefinition property,
            string value)
        {
            object text = DBNull.Value;
            object number = DBNull.Value;
            object date = DBNull.Value;

            switch (property.DataType)
            {
                case "Number":
                    number = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "YesNo":
                    number = value == "1" ? 1.0 : 0.0;
                    break;
                case "Date":
                    date = value;
                    break;
                default:
                    text = value;
                    break;
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO ProductValues " +
                    "(ProductId, ProductTypeId, PropertyId, TextValue, NumberValue, DateValue) " +
                    "VALUES (@productId, @typeId, @propertyId, @text, @number, @date);";
                command.Parameters.AddWithValue("@productId", productId);
                command.Parameters.AddWithValue("@typeId", productTypeId);
                command.Parameters.AddWithValue("@propertyId", property.Id);
                command.Parameters.AddWithValue("@text", text);
                command.Parameters.AddWithValue("@number", number);
                command.Parameters.AddWithValue("@date", date);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Kaydedilen bir değeri, işlem logunda okunacak biçimde yazıya çevirir.
        /// </summary>
        private static string Describe(string dataType, string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "(boş)";
            }

            var turkish = new CultureInfo("tr-TR");

            if (dataType == "YesNo")
            {
                return raw == "1" ? "Evet" : "Hayır";
            }

            if (dataType == "Number")
            {
                double number;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    return number.ToString("0.######", turkish);
                }

                return raw;
            }

            if (dataType == "Date")
            {
                DateTime date;
                if (DateTime.TryParseExact(raw, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return date.ToString("dd.MM.yyyy", turkish);
                }

                return raw;
            }

            return raw;
        }
    }
}