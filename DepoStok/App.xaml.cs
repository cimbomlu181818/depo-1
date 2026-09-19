using System;
using System.Windows;
using DepoStok.Data;

namespace DepoStok
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                Database.Initialize();

             
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Veritabanı oluşturulamadı:\n" + ex.Message,
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
