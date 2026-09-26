using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Senik.DataTransformer.ViewModels;

namespace Senik.DataTransformer.Views.Windows
{
    public partial class AdvancedMappingWindow : Window
    {
        // 🟢 تغییر 8: دریافت existingMappings در کانستراکتور
        public AdvancedMappingWindow(List<string> excelHeaders, System.Collections.ObjectModel.ObservableCollection<Models.ColumnMappingItem> existingMappings, string importType = "Kala")
        {
            InitializeComponent();
            this.DataContext = new AdvancedMappingViewModel(excelHeaders, existingMappings, importType);
            ApplyCurrentTheme();
        }

        private void ApplyCurrentTheme()
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            if (theme.GetBaseTheme() == BaseTheme.Dark)
            {
                BgGradient1.Color = (Color)ColorConverter.ConvertFromString("#0F0C29");
                BgGradient2.Color = (Color)ColorConverter.ConvertFromString("#302B63");

                GlassBrush.Color = Colors.White;
                GlassBrush.Opacity = 0.08;
                GlassBorderBrush.Color = Color.FromArgb(35, 255, 255, 255);
            }
            else
            {
                BgGradient1.Color = (Color)ColorConverter.ConvertFromString("#E0EAFC");
                BgGradient2.Color = (Color)ColorConverter.ConvertFromString("#CFDEF3");

                GlassBrush.Color = Colors.White;
                GlassBrush.Opacity = 0.85;
                GlassBorderBrush.Color = Color.FromArgb(128, 255, 255, 255);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ResetMapping_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is AdvancedMappingViewModel vm)
            {
                // فراخوانی مستقیم متدِ پاکسازی (بدون کلمه Command)
                vm.ClearMappingSelections();
                SenikDialog.Show("نگاشت‌ها به حالت اولیه بازگردانده شدند.", "بازنشانی", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void SaveMapping_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is AdvancedMappingViewModel vm)
            {
                // 🟢 تغییر 9: دریافت نتیجه و پیام خطا
                var validationResult = vm.ValidateMappings();
                if (!validationResult.IsValid)
                {
                    SenikDialog.Show(validationResult.ErrorMessage, "خطای اعتبارسنجی", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            this.DialogResult = true;
            this.Close();
        }
    }
}