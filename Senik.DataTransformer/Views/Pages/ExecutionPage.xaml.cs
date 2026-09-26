using System.Windows;
using System.Windows.Controls;
using Senik.DataTransformer.ViewModels;

namespace Senik.DataTransformer.Views.Pages
{
    public partial class ExecutionPage : Page
    {
        public ExecutionPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ExecutionViewModel vm)
            {
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.ExecutionLogs))
                        Application.Current.Dispatcher.InvokeAsync(() => TxtLogs.ScrollToEnd());
                };
                vm.StartExecutionCommand.Execute(null);
            }
        }
    }
}