using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace DepoStok.Data
{
    /// <summary>
    /// Veritabanının yedeğini alır ve yedekten geri yükler.
    /// Kopyalama SQLite'ın kendi yedekleme özelliğiyle yapılır, bu yüzden yedek her zaman tutarlıdır.
    /// </summary>
    public static class BackupService
    {
        private static readonly string[] RequiredTables =
        {
            "ProductTypes",
            "PropertyDefinitions",
            "TypeProperties",
            "Products",
            "ProductValues",
            "ActionLogs"
        };

        /// <summary>
        /// Önerilen yedek dosyası adı. Örnek: depostok-yedek-2026-09-20_23-15.db
        /// </summary>
        public static string SuggestedFileName()
        {
            return "depostok-yedek-" +
                   DateTime.Now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture) +
                   ".db";
        }

        /// <summary>
        /// Veritabanının yedeğini verilen dosyaya yazar ve işlem loguna kaydeder.
        /// Dosya zaten varsa üzerine yazılır.
        /// </summary>
        public static void CreateBackup(string targetPath)
        {
            CopyDatabaseTo(targetPath);
            WriteLog("Yedek alındı", "Dosya: " + Path.GetFileName(targetPath));
        }

        /// <summary>
        /// Seçilen dosya geçerli bir Depo Stok yedeği mi?
        /// Geçerliyse null verir, değilse sorunun açıklamasını verir.
        /// </summary>
        public static string CheckBackupFile(string path)
        {
            try
            {
                using (var connection = new SQLiteConnection(
                    "Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA integrity_check;";
                        string result = Convert.ToString(command.ExecuteScalar());

                        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                        {
                            return "Dosya bozuk görünüyor.";
                        }
                    }

                    foreach (string table in RequiredTables)
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT COUNT(*) FROM sqlite_master " +
                                "WHERE type = 'table' AND name = @name;";
                            command.Parameters.AddWithValue("@name", table);

                            if (Convert.ToInt64(command.ExecuteScalar()) == 0)
                            {
                                return "Bu dosya bir Depo Stok yedeği değil (\"" + table + "\" tablosu yok).";
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return "Dosya okunamadı. Geçerli bir yedek dosyası değil.";
            }
        }

        /// <summary>
        /// Yedeği geri yükler. Geri yüklemeden önce mevcut verinin otomatik bir kopyasını
        /// "Veri\Yedekler" klasörüne alır. Bu otomatik kopyanın yolunu verir.
        /// </summary>
        public static string RestoreBackup(string backupPath)
        {
            string safetyFolder = Path.Combine(Database.DataFolder, "Yedekler");
            Directory.CreateDirectory(safetyFolder);

            string safetyPath = Path.Combine(
                safetyFolder,
                "geri-yukleme-oncesi-" +
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) +
                ".db");

            CopyDatabaseTo(safetyPath);

            using (var source = new SQLiteConnection(
                "Data Source=" + backupPath + ";Version=3;Read Only=True;"))
            using (var destination = Database.OpenConnection())
            {
                source.Open();
                source.BackupDatabase(destination, "main", "main", -1, null, 0);
            }

            // Eski bir yedekte sonradan eklenen tablo ya da indeks eksikse tamamlanır.
            Database.Initialize();

            WriteLog("Yedekten geri yüklendi", "Dosya: " + Path.GetFileName(backupPath));
            return safetyPath;
        }

        private const int AutomaticBackupRetentionDays = 14;

        /// <summary>
        /// Bugün için henüz otomatik yedek alınmadıysa bir tane alır ve 14 günden eski
        /// otomatik yedekleri siler. Herhangi bir sorun olursa sessizce geçer;
        /// otomatik yedekleme programın açılmasını asla engellemez.
        /// </summary>
        public static void RunAutomaticBackupIfNeeded()
        {
            try
            {
                string folder = Path.Combine(Database.DataFolder, "Yedekler", "Otomatik");
                Directory.CreateDirectory(folder);

                string todayFile = Path.Combine(
                    folder,
                    "otomatik-yedek-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".db");

                if (!File.Exists(todayFile))
                {
                    CopyDatabaseTo(todayFile);
                }

                DateTime cutoff = DateTime.Now.AddDays(-AutomaticBackupRetentionDays);

                foreach (string file in Directory.GetFiles(folder, "otomatik-yedek-*.db"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception)
                        {
                            // Silinemeyen dosyayı görmezden gel, bir sonraki açılışta tekrar denenir.
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Otomatik yedekte sorun olsa da program açılmaya devam etmeli.
            }
        }

        /// <summary>
        /// Çalışan veritabanını verilen dosyaya kopyalar. Dosya varsa üzerine yazar.
        /// </summary>
        private static void CopyDatabaseTo(string targetPath)
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            using (var source = Database.OpenConnection())
            using (var destination = new SQLiteConnection("Data Source=" + targetPath + ";Version=3;"))
            {
                destination.Open();
                source.BackupDatabase(destination, "main", "main", -1, null, 0);
            }
        }

        private static void WriteLog(string action, string details)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO ActionLogs (CreatedAt, UserName, Action, Details) " +
                    "VALUES (@createdAt, @userName, @action, @details);";
                command.Parameters.AddWithValue("@createdAt", now);
                command.Parameters.AddWithValue("@userName", Environment.UserName);
                command.Parameters.AddWithValue("@action", action);
                command.Parameters.AddWithValue("@details", details);
                command.ExecuteNonQuery();
            }
        }
    }
}