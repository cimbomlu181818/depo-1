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
    }
}