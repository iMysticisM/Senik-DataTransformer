using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;

namespace Senik.DataTransformer.Views.Windows
{
    public partial class UpdateWindow : Window
    {
        private string _downloadUrl = "";
        private bool _isDownloaded = false;
        private bool _isMandatory = false;

        public UpdateWindow()
        {
            InitializeComponent();
            ApplyThemeSettings();
        }

        private void ApplyThemeSettings()
        {
            bool isDark = ThemeManager.IsDarkMode();

            if (isDark)
            {
                BgGradient1.Color = (Color)ColorConverter.ConvertFromString("#0F0C29");
                BgGradient2.Color = (Color)ColorConverter.ConvertFromString("#302B63");
                GlassBrush.Color = Colors.White;
                GlassBrush.Opacity = 0.05;
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

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public static async Task CheckForUpdateAsync()
        {
            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                var window = new UpdateWindow();
                window.TxtNotes.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes) 
                    ? "به‌روزرسانی شامل بهبود عملکرد و پایداری سیستم می‌باشد." 
                    : updateInfo.ReleaseNotes;
                    
                window._downloadUrl = updateInfo.DownloadUrl;
                window._isMandatory = updateInfo.IsMandatory;

                if (window._isMandatory)
                {
                    window.BadgeMandatory.Visibility = Visibility.Visible;
                    window.BadgeOptional.Visibility = Visibility.Collapsed;
                    window.BtnCancel.Visibility = Visibility.Collapsed;
                    window.BtnCloseWindow.Visibility = Visibility.Collapsed;

                    // اگر آپدیت اجباری بود، دکمه بستن (ضربدر) کار نخواهد کرد
                    window.Closing += (s, e) =>
                    {
                        if (!window._isDownloaded)
                        {
                            MessageBox.Show("این یک به‌روزرسانی امنیتی و الزامی است. نمی‌توانید آن را لغو کنید.", "اخطار", MessageBoxButton.OK, MessageBoxImage.Warning);
                            e.Cancel = true;
                        }
                    };
                }
                else
                {
                    window.BadgeMandatory.Visibility = Visibility.Collapsed;
                    window.BadgeOptional.Visibility = Visibility.Visible;
                    window.BtnCancel.Visibility = Visibility.Visible;
                    window.BtnCloseWindow.Visibility = Visibility.Visible;
                }

                window.ShowDialog();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_isMandatory && !_isDownloaded)
            {
                MessageBox.Show("این یک به‌روزرسانی امنیتی و الزامی است. لطفاً ابتدا به‌روزرسانی را تکمیل کنید.", "اخطار", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDownloaded)
            {
                BtnAction.IsEnabled = false;
                BtnAction.Content = "⏳ در حال دریافت...";
                PanelProgress.Visibility = Visibility.Visible;
                PbDownload.Value = 0;
                TxtProgressPercent.Text = "0%";
                TxtProgressStatus.Text = "در حال اتصال به سرور و دانلود بسته به‌روزرسانی...";

                var progress = new Progress<int>(percent =>
                {
                    PbDownload.Value = percent;
                    TxtProgressPercent.Text = $"{percent}%";
                });

                try
                {
                    await UpdateService.DownloadUpdateAsync(_downloadUrl, progress);
                    _isDownloaded = true;
                    TxtProgressStatus.Text = "✅ فایل به‌روزرسانی با موفقیت دریافت شد.";
                    BtnAction.IsEnabled = true;
                    BtnAction.Content = "🔄 راه‌اندازی مجدد و اعمال نسخه جدید";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در دریافت فایل آپدیت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateService.LogError(ex);
                    BtnAction.IsEnabled = true;
                    BtnAction.Content = "🔁 تلاش مجدد";
                    PanelProgress.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                UpdateService.ApplyUpdateAndRestart();
            }
        }
    }
}