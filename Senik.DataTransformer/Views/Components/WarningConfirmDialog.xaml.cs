using System.Threading.Tasks;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace Senik.DataTransformer.Views.Components
{
    public partial class WarningConfirmDialog : UserControl
    {
        public WarningConfirmDialog(string message, string title = "هشدار جایگزینی فایل")
        {
            InitializeComponent();
            TxtMessage.Text = message;
            TxtTitle.Text = title;
        }

        public static async Task<bool> ShowAsync(string message, string title = "هشدار جایگزینی فایل", string dialogIdentifier = "RootDialog")
        {
            var dialog = new WarningConfirmDialog(message, title);
            var result = await DialogHost.Show(dialog, dialogIdentifier);
            return result is bool isConfirmed && isConfirmed;
        }
    }
}
