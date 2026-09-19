using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Hurda bölümünün sol listesindeki bir ürün cinsi ve hurdadaki ürün sayısı.
    /// </summary>
    public class ScrapTypeInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }

        public override string ToString()
        {
            return Name + " (" + Count + ")";
        }
    }

    /// <summary>
    /// Bir ürün tipinin ürünlerini, Excel benzeri gösterim için tablo olarak okur.
    /// Ürünler her zaman eklenme sırasıyla gelir. Silinmiş (arşiv) ürünler gelmez.
    /// </summary>
    public static class ProductListRepository
    {
        public const string NoColumn = "No";
        public const string IdColumn = "ProductId";
        public const string QuantityColumn = "Quantity";

        /// <summary>
        /// Bir alanın tablodaki sütun adı. Örnek: alan numarası 5 ise "P5".
        /// Sütunun görünen başlığı (Caption) alanın adıdır.
        /// </summary>
        public static string ColumnNameFor(long propertyId)
        {
            return "P" + propertyId;
        }

        /// <summary>
        /// Hurdada ürünü olan ürün cinslerini, ürün sayılarıyla birlikte Türkçe alfabe sırasıyla verir.
        /// </summary>
        public static List<ScrapTypeInfo> GetScrapTypes()
        {
            var list = new List<ScrapTypeInfo>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT t.Id, t.Name, COUNT(*) FROM Products p " +
                    "JOIN ProductTypes t ON t.Id = p.ProductTypeId " +
                    "WHERE p.IsScrap = 1 AND p.IsArchived = 0 AND t.IsArchived = 0 " +
                    "GROUP BY t.Id, t.Name;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ScrapTypeInfo
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1),
                            Count = Convert.ToInt32(reader.GetValue(2))
                        });
                    }
                }
            }

            var turkish = StringComparer.Create(new CultureInfo("tr-TR"), true);
            list.Sort((a, b) => turkish.Compare(a.Name, b.Name));
            return list;
        }

        /// <summary>
        /// Normal (hurda olmayan) ürünleri tablo olarak verir.
        /// </summary>
        public static DataTable GetTable(
            long productTypeId,
            List<PropertyDefinition> properties,
            bool includeQuantity)
        {
            return GetTable(productTypeId, properties, includeQuantity, false);
        }

        /// <summary>
        /// scrap doğruysa sadece hurda ürünleri, yanlışsa sadece normal ürünleri tablo olarak verir.
        /// </summary>
        public static DataTable GetTable(
            long productTypeId,
            List<PropertyDefinition> properties,
            bool includeQuantity,
            bool scrap)
        {
            var table = new DataTable();
            table.Columns.Add(NoColumn, typeof(int));
            table.Columns.Add(IdColumn, typeof(long));

            if (includeQuantity)
            {
                table.Columns.Add(QuantityColumn, typeof(int));
            }

            var dataTypes = new Dictionary<long, string>();

            foreach (var property in properties)
            {
                DataColumn column = table.Columns.Add(ColumnNameFor(property.Id), typeof(string));
                column.Caption = property.Name;
                dataTypes[property.Id] = property.DataType;
            }

            var rowsById = new Dictionary<long, DataRow>();
            var turkish = new CultureInfo("tr-TR");

            using (var connection = Database.OpenConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT Id, Quantity FROM Products " +
                        "WHERE ProductTypeId = @typeId AND IsArchived = 0 AND IsScrap = @scrap " +
                        "ORDER BY Id;";
                    command.Parameters.AddWithValue("@typeId", productTypeId);
                    command.Parameters.AddWithValue("@scrap", scrap ? 1 : 0);

                    using (var reader = command.ExecuteReader())
                    {
                        int number = 0;

                        while (reader.Read())
                        {
                            long productId = reader.GetInt64(0);

                            DataRow row = table.NewRow();
                            row[NoColumn] = ++number;
                            row[IdColumn] = productId;

                            if (includeQuantity)
                            {
                                row[QuantityColumn] = reader.GetInt32(1);
                            }

                            table.Rows.Add(row);
                            rowsById[productId] = row;
                        }
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT ProductId, PropertyId, TextValue, NumberValue, DateValue " +
                        "FROM ProductValues WHERE ProductTypeId = @typeId;";
                    command.Parameters.AddWithValue("@typeId", productTypeId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long productId = reader.GetInt64(0);
                            long propertyId = reader.GetInt64(1);

                            DataRow row;
                            if (!rowsById.TryGetValue(productId, out row))
                            {
                                continue;
                            }

                            string dataType;
                            if (!dataTypes.TryGetValue(propertyId, out dataType))
                            {
                                continue;
                            }

                            string columnName = ColumnNameFor(propertyId);
                            string text = null;

                            if (!reader.IsDBNull(2))
                            {
                                text = reader.GetString(2);
                            }
                            else if (!reader.IsDBNull(3))
                            {
                                double number = reader.GetDouble(3);

                                if (dataType == "YesNo")
                                {
                                    text = number == 1 ? "Evet" : "Hayır";
                                }
                                else
                                {
                                    text = number.ToString("0.######", turkish);
                                }
                            }
                            else if (!reader.IsDBNull(4))
                            {
                                string raw = reader.GetString(4);
                                DateTime date;

                                if (DateTime.TryParseExact(raw, "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                                {
                                    text = date.ToString("dd.MM.yyyy", turkish);
                                }
                                else
                                {
                                    text = raw;
                                }
                            }

                            if (text != null)
                            {
                                row[columnName] = text;
                            }
                        }
                    }
                }
            }

            return table;
        }
    }
}