using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Senik.DataTransformer.Views.Windows
{
    public partial class ConnectionWindow : Window
    {
        public bool IsConnected { get; private set; } = false;

        public ConnectionWindow()
        {
            InitializeComponent();
            ApplyThemeSettings();

            _ = DiscoverSqlServersAsync();
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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Senik.DataTransformer.Views.Windows.UpdateWindow.CheckForUpdateAsync();
        }

        // 🟢 تغییر ۳: جستجوی فوق‌سریع سرورهای Local از طریق Registry ویندوز (بدون هنگ کردن شبکه)
        private async Task DiscoverSqlServersAsync()
        {
            try
            {
                // 🔒 قفل کردن هر دو اِلمان در شروع جستجو (چه در اجرای اول، چه با کلیک)
                if (BtnRefreshServers != null) BtnRefreshServers.IsEnabled = false;

                MaterialDesignThemes.Wpf.HintAssist.SetHint(CmbServers, "در حال جستجو در شبکه (ممکن است زمان‌بر باشد)...");
                CmbServers.ItemsSource = new string[] { "درحال جستجوی سرورهای شبکه..." };
                CmbServers.IsEnabled = false;

                var servers = await Task.Run(() =>
                {
                    var list = new System.Collections.Generic.List<string>();
                    list.Add(Environment.MachineName);

                    try
                    {
                        Microsoft.Data.Sql.SqlDataSourceEnumerator instance = Microsoft.Data.Sql.SqlDataSourceEnumerator.Instance;
                        System.Data.DataTable table = instance.GetDataSources();
                        foreach (System.Data.DataRow row in table.Rows)
                        {
                            string serverName = row["ServerName"]?.ToString() ?? "";
                            string instanceName = row["InstanceName"]?.ToString() ?? "";
                            string fullServer = string.IsNullOrEmpty(instanceName) ? serverName : $@"{serverName}\{instanceName}";

                            if (!list.Contains(fullServer)) list.Add(fullServer);
                        }
                    }
                    catch { }

                    return list;
                });

                CmbServers.ItemsSource = servers;
                CmbServers.IsEnabled = true;
                if (servers.Count > 0) CmbServers.SelectedIndex = 0;

                MaterialDesignThemes.Wpf.HintAssist.SetHint(CmbServers, "انتخاب سرور SQL");
            }
            catch
            {
                CmbServers.ItemsSource = new string[] { Environment.MachineName };
                CmbServers.IsEnabled = true;
                CmbServers.SelectedIndex = 0;
                MaterialDesignThemes.Wpf.HintAssist.SetHint(CmbServers, "انتخاب سرور SQL");
            }
            finally
            {
                // 🔓 باز کردن قفل دکمه در پایان کار (حتی اگر اروری رخ داده باشد)
                if (BtnRefreshServers != null) BtnRefreshServers.IsEnabled = true;
            }
        }

        private async void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            // دیگر نیازی به قفل کردن دستی اینجا نیست، خود متد بالایی این کار را می‌کند
            await DiscoverSqlServersAsync();

            SenikDialog.Show("جستجوی سرورهای شبکه به پایان رسید.\nاگر سرور شما در لیست نیست، می‌توانید آدرس IP آن را به صورت دستی تایپ کنید.", "بروزرسانی", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void CmbAuthType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtUsername == null || TxtPassword == null) return;
            
            if (CmbAuthType.SelectedIndex == 1) // SQL Authentication
            {
                TxtUsername.IsEnabled = true;
                TxtPassword.IsEnabled = true;
                TxtUsername.Opacity = 1;
                TxtPassword.Opacity = 1;
            }
            else // Windows Authentication
            {
                TxtUsername.IsEnabled = false;
                TxtPassword.IsEnabled = false;
                TxtUsername.Opacity = 0.5;
                TxtPassword.Opacity = 0.5;
            }
        }

        private void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            string server = CmbServers.Text.Trim();
            bool isWinAuth = CmbAuthType.SelectedIndex == 0;
            string user = TxtUsername.Text.Trim();
            string pass = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(server))
            {
                SenikDialog.Show("لطفاً نام سرور را انتخاب کنید.", "اخطار", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dbService = new DatabaseService();
                string masterConn = dbService.BuildConnectionString(server, isWinAuth, user, pass, "master");
                var databases = dbService.GetDatabases(masterConn);

                CmbDatabases.ItemsSource = databases;
                CmbDatabases.IsEnabled = true;
                if (databases.Count > 0) CmbDatabases.SelectedIndex = 0;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                // بررسی دقیق‌تر خطاهای اتصال
                if (ex.Number == 2 || ex.Number == 53 || ex.Number == 40)
                {
                    SenikDialog.Show("سرور یافت نشد یا در دسترس نیست!\n\nاحتمالات:\n۱. سرویس SQL Server متوقف شده است.\n۲. نام سرور انتخاب شده صحیح نیست.", "خطای ارتباط شبکه", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (ex.Number == 18456)
                {
                    SenikDialog.Show("نام کاربری یا رمز عبور اشتباه است! لطفاً اطلاعات ورود را بررسی کنید.", "خطای احراز هویت", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (ex.Number == 18470)
                {
                    SenikDialog.Show("حساب کاربری وارد شده در SQL Server غیرفعال است.", "خطای دسترسی", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (ex.Message.Contains("certificate chain was issued by an authority that is not trusted"))
                {
                    SenikDialog.Show("خطای گواهینامه SSL سرور. لطفاً از فعال بودن TrustServerCertificate در رشته اتصال اطمینان حاصل کنید.", "خطای امنیتی", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    SenikDialog.Show($"خطای ناشناخته دیتابیس (کد {ex.Number}):\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                SenikDialog.Show($"خطای سیستمی در برقراری ارتباط:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (CmbDatabases.SelectedItem == null)
            {
                SenikDialog.Show("لطفاً ابتدا دکمه «بارگذاری دیتابیس‌ها» را زده و دیتابیس مقصد را انتخاب کنید.", "اخطار", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dbService = new DatabaseService();

            AppState.SqlConnectionString = dbService.BuildConnectionString(
                CmbServers.Text.Trim(),
                CmbAuthType.SelectedIndex == 0,
                TxtUsername.Text.Trim(),
                TxtPassword.Password, 
                CmbDatabases.SelectedItem?.ToString() ?? "");
                
            IsConnected = true;
            this.DialogResult = true;
            this.Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            IsConnected = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}