using System;
using System.Windows;

namespace DepoStok
{
    /// <summary>
    /// Küçük "Adı değiştir" penceresi. Ürün tipi ve alan adı değiştirirken ortak kullanılır.
    /// Yeni ad geçerli değilse (boş, zaten var vb.) pencere açık kalır ve nedenini söyler.
    /// </summary>
    public partial class RenameWindow : Window
    {
        private readonly Func<string, string> _validate;

        /// <summary>
        /// Kullanıcının onayladığı yeni ad. Pencere Tamam ile kapanınca dolar.
        /// </summary>
        public string NewName { get; private set; }

        /// <param name="heading">Pencerenin başlık yazısı. Örnek: "Ürün tipi: Telsiz"</param>
        /// <param name="currentName">Şu anki ad, kutuya yazılı gelir.</param>
        /// <param name="validate">
        /// Yeni adı denetler. Sorun varsa açıklamasını, sorun yoksa null verir.
        /// </param>
        public RenameWindow(string heading, string currentName, Func<string, string> validate)
        {
            InitializeComponent();

            _validate = validate;
            HeadingText.Text = heading;
            NameBox.Text = currentName;

            Loaded += (sender, e) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();

            if (name.Length == 0)
            {
                ShowProblem("Lütfen yeni adı yazın.");
                return;
            }

            string problem = _validate == null ? null : _validate(name);

            if (problem != null)
            {
                ShowProblem(problem);
                return;
            }

            NewName = name;
            DialogResult = true;
        }

        private void ShowProblem(string message)
        {
            MessageBox.Show(this, message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            NameBox.SelectAll();
        }
    }
}