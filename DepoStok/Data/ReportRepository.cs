using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace DepoStok.Data
{
    /// <summary>
    /// Bir ürün tipinin rapordaki satırı.
    /// </summary>
    public class TypeSummaryRow
    {
        public string TypeName { get; set; }
        public int ActiveCount { get; set; }
        public int ScrapCount { get; set; }
        public int ArchivedCount { get; set; }
    }

    /// <summary>
    /// Genel durum raporu için sayıları hazırlar.
    /// </summary>
    public static class ReportRepository
    {
        public class Summary
        {
            public int ActiveTypeCount { get; set; }
            public int ArchivedTypeCount { get; set; }
            public int ActivePropertyCount { get; set; }
            public int ArchivedPropertyCount { get; set; }
            public int ActiveProductCount { get; set; }
            public int ScrapProductCount { get; set; }
            public int ArchivedProductCount { get; set; }
            public List<TypeSummaryRow> TypeRows { get; set; } = new List<TypeSummaryRow>();
        }

        public static Summary Build()
        {
            var summary = new Summary();

            using (var connection = Database.OpenConnection())
            {
                summary.ActiveTypeCount = Scalar(connection,
                    "SELECT COUNT(*) FROM ProductTypes WHERE IsArchived = 0;");
                summary.ArchivedTypeCount = Scalar(connection,
                    "SELECT COUNT(*) FROM ProductTypes WHERE IsArchived = 1;");

                summary.ActivePropertyCount = Scalar(connection,
                    "SELECT COUNT(*) FROM PropertyDefinitions WHERE IsArchived = 0;");
                summary.ArchivedPropertyCount = Scalar(connection,
                    "SELECT COUNT(*) FROM PropertyDefinitions WHERE IsArchived = 1;");

                summary.ActiveProductCount = Scalar(connection,
                    "SELECT COUNT(*) FROM Products WHERE IsArchived = 0 AND IsScrap = 0;");
                summary.ScrapProductCount = Scalar(connection,
                    "SELECT COUNT(*) FROM Products WHERE IsArchived = 0 AND IsScrap = 1;");
                summary.ArchivedProductCount = Scalar(connection,
                    "SELECT COUNT(*) FROM Products WHERE IsArchived = 1;");

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT t.Name, " +
                        "SUM(CASE WHEN p.IsArchived = 0 AND p.IsScrap = 0 THEN 1 ELSE 0 END), " +
                        "SUM(CASE WHEN p.IsArchived = 0 AND p.IsScrap = 1 THEN 1 ELSE 0 END), " +
                        "SUM(CASE WHEN p.IsArchived = 1 THEN 1 ELSE 0 END) " +
                        "FROM ProductTypes t " +
                        "LEFT JOIN Products p ON p.ProductTypeId = t.Id " +
                        "WHERE t.IsArchived = 0 " +
                        "GROUP BY t.Id, t.Name;";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            summary.TypeRows.Add(new TypeSummaryRow
                            {
                                TypeName = reader.GetString(0),
                                ActiveCount = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                                ScrapCount = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                                ArchivedCount = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3))
                            });
                        }
                    }
                }
            }

            var turkish = StringComparer.Create(new CultureInfo("tr-TR"), true);
            summary.TypeRows.Sort((a, b) => turkish.Compare(a.TypeName, b.TypeName));

            return summary;
        }

        private static int Scalar(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}