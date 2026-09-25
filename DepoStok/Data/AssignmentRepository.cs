using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Bir zimmet kaydı: bir ürünün (tamamının ya da bir kısmının) bir kişiye verilmesi.
    /// Kişi bilgileri ve ürün açıklaması ekranda göstermek için hazır biçimde gelir.
    /// </summary>
    public class Assignment
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public long ProductTypeId { get; set; }

        /// <summary>Ürün tipinin adı. Örnek: "Telsiz".</summary>
        public string TypeName { get; set; }

        /// <summary>Ürünün "Sistem Adı" alanına girilmiş değeri (boşsa ürün tipinin adı).</summary>
        public string SystemName { get; set; }

        /// <summary>Ürünün gerçek seri numarası. Ürünün seri numarası yoksa boş/null olur.</summary>
        public string SerialNumber { get; set; }

        /// <summary>Ürünü tanıyan kısa açıklama. Örnek: "Telsiz, Seri No: ABC123".</summary>
        public string ProductDescription { get; set; }

        public int Quantity { get; set; }

        public string PersonName { get; set; }
        public string RegistryNo { get; set; }
        public string Department { get; set; }

        public string AssignedAtText { get; set; }
        public string AssignedNote { get; set; }

        public string ReturnedAtText { get; set; }
        public string ReturnedNote { get; set; }

        public bool IsReturned { get; set; }
    }

    /// <summary>
    /// Ürünlerin kişilere zimmetlenmesini, iade alınmasını ve zimmet geçmişini yönetir.
    /// Adetli ürünlerde miktarın bir kısmı zimmetlenebilir; seri numaralı ürünlerde miktar hep 1'dir.
    /// </summary>
    public static class AssignmentRepository
    {
        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        private static string FormatTime(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "";
            }

            DateTime time;
            return DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                ? time.ToString("dd.MM.yyyy HH:mm:ss", Turkish)
                : raw;
        }

        /// <summary>
        /// Bir ürünün gerçek seri numarasını verir. Ürünün seri numarası yoksa null döner.
        /// </summary>
        private static string GetSerialNumberValue(SQLiteConnection connection, long productId)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT v.TextValue " +
                    "FROM ProductValues v " +
                    "JOIN PropertyDefinitions d ON d.Id = v.PropertyId " +
                    "WHERE v.ProductId = @productId AND d.IsSerialNumber = 1 " +
                    "LIMIT 1;";
                command.Parameters.AddWithValue("@productId", productId);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read() && !reader.IsDBNull(0))
                    {
                        return reader.GetString(0);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Bir ürünün seri numarasını (varsa) ve tip adını okunur bir yazıya çevirir.
        /// Seri numarası yoksa "(ürün no: 7)" biçiminde yazar.
        /// </summary>
        private static string DescribeProduct(SQLiteConnection connection, long productId, string typeName)
        {
            return DescribeProduct(connection, null, productId, typeName);
        }

        private static string DescribeProduct(
            SQLiteConnection connection, SQLiteTransaction transaction, long productId, string typeName)
        {
            string systemName = PropertyDefinitionRepository.GetSystemName(productId, typeName);

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "SELECT d.Name, v.TextValue " +
                    "FROM ProductValues v " +
                    "JOIN PropertyDefinitions d ON d.Id = v.PropertyId " +
                    "WHERE v.ProductId = @productId AND d.IsSerialNumber = 1 " +
                    "LIMIT 1;";
                command.Parameters.AddWithValue("@productId", productId);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read() && !reader.IsDBNull(1))
                    {
                        return systemName + ", " + reader.GetString(0) + ": " + reader.GetString(1);
                    }
                }
            }

            return systemName + " (ürün no: " + productId + ")";
        }

        /// <summary>
        /// Bir ürünün toplam miktarından, şu an zimmette olan kısmı düşüp kalanı verir.
        /// Seri numaralı (miktarı hep 1 olan) ürünlerde bu 0 ya da 1 olur.
        /// </summary>
        public static int GetAvailableQuantity(long productId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT p.Quantity - COALESCE((SELECT SUM(a.Quantity) FROM Assignments a " +
                    "WHERE a.ProductId = p.Id AND a.IsReturned = 0), 0) " +
                    "FROM Products p WHERE p.Id = @productId;";
                command.Parameters.AddWithValue("@productId", productId);

                object result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// Bir ürünün şu an iade edilmemiş (üzerinde birilerinde duran) zimmetlerini verir.
        /// Genelde tek satır olur; adetli üründe birden fazla kişiye bölünmüş olabilir.
        /// </summary>
        public static List<Assignment> GetActiveForProduct(long productId)
        {
            return GetForProduct(productId, activeOnly: true);
        }

        /// <summary>
        /// Bir ürünün tüm zimmet geçmişini (üzerinde duranlar + iade edilmişler) en yeniden eskiye verir.
        /// </summary>
        public static List<Assignment> GetHistoryForProduct(long productId)
        {
            return GetForProduct(productId, activeOnly: false);
        }

        private static List<Assignment> GetForProduct(long productId, bool activeOnly)
        {
            var list = new List<Assignment>();

            using (var connection = Database.OpenConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT a.Id, a.ProductId, a.ProductTypeId, t.Name, a.Quantity, " +
                        "a.PersonName, a.RegistryNo, a.Department, a.AssignedAt, a.AssignedNote, " +
                        "a.ReturnedAt, a.ReturnedNote, a.IsReturned " +
                        "FROM Assignments a " +
                        "JOIN ProductTypes t ON t.Id = a.ProductTypeId " +
                        "WHERE a.ProductId = @productId " +
                        (activeOnly ? "AND a.IsReturned = 0 " : "") +
                        "ORDER BY a.AssignedAt DESC, a.Id DESC;";
                    command.Parameters.AddWithValue("@productId", productId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReadAssignment(reader));
                        }
                    }
                }

                foreach (var assignment in list)
                {
                    assignment.ProductDescription = DescribeProduct(connection, assignment.ProductId, assignment.TypeName);
                    assignment.SystemName = PropertyDefinitionRepository.GetSystemName(assignment.ProductId, assignment.TypeName);
                    assignment.SerialNumber = GetSerialNumberValue(connection, assignment.ProductId);
                }
            }

            return list;
        }

        /// <summary>
        /// Şu an kimde ne olduğunu gösteren, hiç iade edilmemiş tüm zimmetleri en yeniden eskiye verir.
        /// </summary>
        public static List<Assignment> GetAllActive()
        {
            var list = new List<Assignment>();

            using (var connection = Database.OpenConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT a.Id, a.ProductId, a.ProductTypeId, t.Name, a.Quantity, " +
                        "a.PersonName, a.RegistryNo, a.Department, a.AssignedAt, a.AssignedNote, " +
                        "a.ReturnedAt, a.ReturnedNote, a.IsReturned " +
                        "FROM Assignments a " +
                        "JOIN ProductTypes t ON t.Id = a.ProductTypeId " +
                        "WHERE a.IsReturned = 0 " +
                        "ORDER BY a.AssignedAt DESC, a.Id DESC;";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReadAssignment(reader));
                        }
                    }
                }

                foreach (var assignment in list)
                {
                    assignment.ProductDescription = DescribeProduct(connection, assignment.ProductId, assignment.TypeName);
                    assignment.SystemName = PropertyDefinitionRepository.GetSystemName(assignment.ProductId, assignment.TypeName);
                    assignment.SerialNumber = GetSerialNumberValue(connection, assignment.ProductId);
                }
            }

            return list;
        }

        private static Assignment ReadAssignment(SQLiteDataReader reader)
        {
            return new Assignment
            {
                Id = reader.GetInt64(0),
                ProductId = reader.GetInt64(1),
                ProductTypeId = reader.GetInt64(2),
                TypeName = reader.GetString(3),
                Quantity = reader.GetInt32(4),
                PersonName = reader.GetString(5),
                RegistryNo = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Department = reader.IsDBNull(7) ? "" : reader.GetString(7),
                AssignedAtText = FormatTime(reader.GetString(8)),
                AssignedNote = reader.IsDBNull(9) ? "" : reader.GetString(9),
                ReturnedAtText = reader.IsDBNull(10) ? "" : FormatTime(reader.GetString(10)),
                ReturnedNote = reader.IsDBNull(11) ? "" : reader.GetString(11),
                IsReturned = reader.GetInt32(12) != 0
            };
        }

        /// <summary>
        /// Bir ürünü (ya da adetli üründe bir kısmını) bir kişiye zimmetler.
        /// Elde yeterli miktar yoksa ya da isim boşsa hata fırlatır.
        /// </summary>
        public static void Assign(
            long productId,
            long productTypeId,
            int quantity,
            string personName,
            string registryNo,
            string department,
            string note)
        {
            if (string.IsNullOrWhiteSpace(personName))
            {
                throw new InvalidOperationException("Kişi adı boş bırakılamaz.");
            }

            if (quantity < 1)
            {
                throw new InvalidOperationException("Miktar 1 veya daha büyük olmalı.");
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                int available;

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "SELECT p.Quantity - COALESCE((SELECT SUM(a.Quantity) FROM Assignments a " +
                        "WHERE a.ProductId = p.Id AND a.IsReturned = 0), 0) " +
                        "FROM Products p WHERE p.Id = @productId;";
                    command.Parameters.AddWithValue("@productId", productId);

                    object result = command.ExecuteScalar();
                    available = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }

                if (quantity > available)
                {
                    throw new InvalidOperationException(
                        "Bu üründen zimmetlenebilecek miktar en fazla " + available + ".");
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO Assignments " +
                        "(ProductId, ProductTypeId, Quantity, PersonName, RegistryNo, Department, AssignedAt, AssignedNote, IsReturned) " +
                        "VALUES (@productId, @typeId, @quantity, @personName, @registryNo, @department, @now, @note, 0);";
                    command.Parameters.AddWithValue("@productId", productId);
                    command.Parameters.AddWithValue("@typeId", productTypeId);
                    command.Parameters.AddWithValue("@quantity", quantity);
                    command.Parameters.AddWithValue("@personName", personName.Trim());
                    command.Parameters.AddWithValue("@registryNo",
                        string.IsNullOrWhiteSpace(registryNo) ? (object)DBNull.Value : registryNo.Trim());
                    command.Parameters.AddWithValue("@department",
                        string.IsNullOrWhiteSpace(department) ? (object)DBNull.Value : department.Trim());
                    command.Parameters.AddWithValue("@now", now);
                    command.Parameters.AddWithValue("@note",
                        string.IsNullOrWhiteSpace(note) ? (object)DBNull.Value : note.Trim());
                    command.ExecuteNonQuery();
                }

                string productText = DescribeProduct(
                    connection, transaction, productId, GetTypeName(connection, transaction, productTypeId));

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO ActionLogs (CreatedAt, UserName, Action, Details) " +
                        "VALUES (@createdAt, @userName, @action, @details);";
                    command.Parameters.AddWithValue("@createdAt", now);
                    command.Parameters.AddWithValue("@userName", Environment.UserName);
                    command.Parameters.AddWithValue("@action", "Ürün zimmetlendi");
                    command.Parameters.AddWithValue("@details",
                        productText + ", miktar: " + quantity + " → " + personName.Trim());
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        /// <summary>
        /// Bir zimmeti iade alınmış olarak işaretler.
        /// </summary>
        public static void Return(long assignmentId, string note)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                long productId;
                long productTypeId;
                string personName;

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "SELECT ProductId, ProductTypeId, PersonName FROM Assignments WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", assignmentId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            throw new InvalidOperationException("Zimmet kaydı bulunamadı.");
                        }

                        productId = reader.GetInt64(0);
                        productTypeId = reader.GetInt64(1);
                        personName = reader.GetString(2);
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "UPDATE Assignments SET IsReturned = 1, ReturnedAt = @now, ReturnedNote = @note " +
                        "WHERE Id = @id;";
                    command.Parameters.AddWithValue("@now", now);
                    command.Parameters.AddWithValue("@note",
                        string.IsNullOrWhiteSpace(note) ? (object)DBNull.Value : note.Trim());
                    command.Parameters.AddWithValue("@id", assignmentId);
                    command.ExecuteNonQuery();
                }

                string productText = DescribeProduct(
                    connection, transaction, productId, GetTypeName(connection, transaction, productTypeId));

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO ActionLogs (CreatedAt, UserName, Action, Details) " +
                        "VALUES (@createdAt, @userName, @action, @details);";
                    command.Parameters.AddWithValue("@createdAt", now);
                    command.Parameters.AddWithValue("@userName", Environment.UserName);
                    command.Parameters.AddWithValue("@action", "Zimmet iade alındı");
                    command.Parameters.AddWithValue("@details", productText + ", " + personName + " → depo");
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        private static string GetTypeName(SQLiteConnection connection, SQLiteTransaction transaction, long typeId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT Name FROM ProductTypes WHERE Id = @id;";
                command.Parameters.AddWithValue("@id", typeId);

                object result = command.ExecuteScalar();
                return result == null ? "ürün tipi no: " + typeId : Convert.ToString(result);
            }
        }
    }
}