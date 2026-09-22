using System;
using System.Data.SQLite;
using System.IO;

namespace DepoStok.Data
{
    /// <summary>
    /// SQLite veritabanı dosyasını oluşturur ve bağlantı açar.
    /// Veritabanı, programın bulunduğu klasördeki "Veri" klasöründe tutulur.
    /// </summary>
    public static class Database
    {
        public static string DataFolder
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Veri"); }
        }

        public static string DatabasePath
        {
            get { return Path.Combine(DataFolder, "depostok.db"); }
        }

        private static string ConnectionString
        {
            get { return "Data Source=" + DatabasePath + ";Version=3;Foreign Keys=True;"; }
        }

        /// <summary>
        /// Açık bir bağlantı verir. Kullanan kişi "using" ile kapatmalıdır.
        /// </summary>
        public static SQLiteConnection OpenConnection()
        {
            var connection = new SQLiteConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Klasörü ve tabloları oluşturur. Zaten varsa hiçbir şeyi silmez veya bozmaz.
        /// </summary>
        public static void Initialize()
        {
            Directory.CreateDirectory(DataFolder);

            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = CreateTablesSql;
                command.ExecuteNonQuery();
            }
        }

        private const string CreateTablesSql = @"
CREATE TABLE IF NOT EXISTS ProductTypes (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT    NOT NULL UNIQUE,
    IsArchived  INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS PropertyDefinitions (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Name            TEXT    NOT NULL UNIQUE,
    DataType        TEXT    NOT NULL,
    IsSerialNumber  INTEGER NOT NULL DEFAULT 0,
    IsArchived      INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS TypeProperties (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductTypeId  INTEGER NOT NULL REFERENCES ProductTypes(Id),
    PropertyId     INTEGER NOT NULL REFERENCES PropertyDefinitions(Id),
    SortOrder      INTEGER NOT NULL DEFAULT 0,
    IsArchived     INTEGER NOT NULL DEFAULT 0,
    UNIQUE (ProductTypeId, PropertyId)
);

CREATE TABLE IF NOT EXISTS Products (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductTypeId  INTEGER NOT NULL REFERENCES ProductTypes(Id),
    Quantity       INTEGER NOT NULL DEFAULT 1,
    IsScrap        INTEGER NOT NULL DEFAULT 0,
    IsArchived     INTEGER NOT NULL DEFAULT 0,
    CreatedAt      TEXT    NOT NULL,
    UpdatedAt      TEXT
);

CREATE TABLE IF NOT EXISTS ProductValues (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId      INTEGER NOT NULL REFERENCES Products(Id),
    ProductTypeId  INTEGER NOT NULL REFERENCES ProductTypes(Id),
    PropertyId     INTEGER NOT NULL REFERENCES PropertyDefinitions(Id),
    TextValue      TEXT,
    NumberValue    REAL,
    DateValue      TEXT,
    UNIQUE (ProductId, PropertyId)
);

CREATE INDEX IF NOT EXISTS IX_ProductValues_Text
    ON ProductValues (ProductTypeId, PropertyId, TextValue);

CREATE INDEX IF NOT EXISTS IX_ProductValues_Number
    ON ProductValues (ProductTypeId, PropertyId, NumberValue);

CREATE TABLE IF NOT EXISTS ActionLogs (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    CreatedAt  TEXT NOT NULL,
    UserName   TEXT NOT NULL,
    Action     TEXT NOT NULL,
    Details    TEXT
);

CREATE TABLE IF NOT EXISTS Assignments (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId      INTEGER NOT NULL REFERENCES Products(Id),
    ProductTypeId  INTEGER NOT NULL REFERENCES ProductTypes(Id),
    Quantity       INTEGER NOT NULL DEFAULT 1,
    PersonName     TEXT    NOT NULL,
    RegistryNo     TEXT,
    Department     TEXT,
    AssignedAt     TEXT    NOT NULL,
    AssignedNote   TEXT,
    ReturnedAt     TEXT,
    ReturnedNote   TEXT,
    IsReturned     INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS IX_Assignments_Product
    ON Assignments (ProductId, IsReturned);

CREATE INDEX IF NOT EXISTS IX_Assignments_Active
    ON Assignments (IsReturned, AssignedAt);
";
    }
}