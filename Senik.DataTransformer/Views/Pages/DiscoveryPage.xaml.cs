using Senik.DataTransformer.Services;
using System.Windows.Controls;
using System.Windows.Input;

namespace Senik.DataTransformer.Views.Pages
{
    public partial class DiscoveryPage : Page
    {
        public DiscoveryPage()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                SenikTourGuide.Register("Start", BtnGlobalStart);
            };
        }

        private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // فوکوس مستقیم روی تب کالا تا کیبورد بلافاصله کار کند
            TabKala.Focus();
        }

        private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // حرکت بین تب‌ها با استفاده از کیبورد (راست به چپ)
            if (e.Key == Key.NumPad4 || e.Key == Key.Left)
            {
                if (TabKala.IsChecked == true) TabPerson.IsChecked = true;
                else if (TabPerson.IsChecked == true) TabCheck.IsChecked = true;
                else if (TabCheck.IsChecked == true) TabKala.IsChecked = true;
                e.Handled = true;
            }
            else if (e.Key == Key.NumPad6 || e.Key == Key.Right)
            {
                if (TabKala.IsChecked == true) TabCheck.IsChecked = true;
                else if (TabCheck.IsChecked == true) TabPerson.IsChecked = true;
                else if (TabPerson.IsChecked == true) TabKala.IsChecked = true;
                e.Handled = true;
            }
        }
    }
}