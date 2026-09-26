using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            // پنجره آپدیت ۵ ثانیه پس از باز شدن پنجره پایگاه داده باز شود
            await Task.Delay(5000);
            if (this.IsLoaded && !this.IsConnected)
            {
                await Senik.DataTransformer.Views.Windows.UpdateWindow.CheckForUpdateAsync(this);
            }
        }

        // 🟢 جستجوی فوق‌سریع و دقیق سرورهای SQL بدون فریز شدن و حذف نام خام رایانه بدون نمونه
        private async Task DiscoverSqlServersAsync()
        {
            try
            {
                // 🔒 قفل کردن المان‌ها در شروع جستجو
                if (BtnRefreshServers != null) BtnRefreshServers.IsEnabled = false;

                MaterialDesignThemes.Wpf.HintAssist.SetHint(CmbServers, "در حال جستجو در شبکه (ممکن است زمان‌بر باشد)...");
                CmbServers.ItemsSource = new string[] { "درحال جستجوی سرورهای شبکه..." };
                CmbServers.IsEnabled = false;

                var servers = await Task.Run(() =>
                {
                    var list = new System.Collections.Generic.List<string>();
                    bool hasDefaultInstance = false;

                    // ۱. جستجوی فوق‌سریع نمونه‌های محلی از طریق رجیستری ویندوز
                    try
                    {
                        string[] regPaths = new[]
                        {
                            @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL",
                            @"SOFTWARE\WOW6432Node\Microsoft\Microsoft SQL Server\Instance Names\SQL"
                        };

                        foreach (var path in regPaths)
                        {
                            using (var key = Registry.LocalMachine.OpenSubKey(path))
                            {
                                if (key != null)
                                {
                                    foreach (var inst in key.GetValueNames())
                                    {
                                        if (inst.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase))
                                        {
                                            hasDefaultInstance = true;
                                            string defaultInst = Environment.MachineName;
                                            if (!list.Contains(defaultInst)) list.Add(defaultInst);
                                        }
                                        else
                                        {
                                            string namedInst = $@"{Environment.MachineName}\{inst}";
                                            if (!list.Contains(namedInst)) list.Add(namedInst);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    // ۲. جستجو در شبکه از طریق SqlDataSourceEnumerator
                    try
                    {
                        Microsoft.Data.Sql.SqlDataSourceEnumerator instance = Microsoft.Data.Sql.SqlDataSourceEnumerator.Instance;
                        System.Data.DataTable table = instance.GetDataSources();
                        foreach (System.Data.DataRow row in table.Rows)
                        {
                            string serverName = row["ServerName"]?.ToString() ?? "";
                            string instanceName = row["InstanceName"]?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(instanceName))
                            {
                                string fullServer = $@"{serverName}\{instanceName}";
                                if (!list.Contains(fullServer)) list.Add(fullServer);
                            }
                            else if (!string.IsNullOrEmpty(serverName))
                            {
                                // فقط اگر سرور راه دور باشد یا دیتابیس پیش‌فرض روی سیستم محلی نصب باشد
                                if (!serverName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || hasDefaultInstance)
                                {
                                    if (!list.Contains(serverName)) list.Add(serverName);
                                }
                            }
                        }
                    }
                    catch { }

                    // ۳. فیلتر کردن قطعی نام خام رایانه بدون نام نمونه در صورتی که دیتابیس پیش‌فرض فعال نباشد
                    if (!hasDefaultInstance)
                    {
                        list.RemoveAll(s => s.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || s.Equals("(local)", StringComparison.OrdinalIgnoreCase));
                    }

                    return list;
                });

                CmbServers.ItemsSource = servers;
                CmbServers.IsEnabled = true;
                if (servers.Count > 0) CmbServers.SelectedIndex = 0;

                MaterialDesignThemes.Wpf.HintAssist.SetHint(CmbServers, "انتخاب سرور SQL");
            }
            catch
            {
                CmbServers.ItemsSource = new string[0];
                CmbServers.IsEnabled = true;
                MaterialDesignThemes.Wpf.HintAssist.SetHint(CmbServers, "انتخاب سرور SQL");
            }
            finally
            {
                // 🔓 باز کردن قفل دکمه در پایان کار
                if (BtnRefreshServers != null) BtnRefreshServers.IsEnabled = true;
            }
        }

        private async void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            await DiscoverSqlServersAsync();
            SenikDialog.Show("جستجوی سرورهای شبکه به پایان رسید.\nاگر سرور شما در لیست نیست، می‌توانید آدرس آن را به صورت دستی تایپ کنید.", "بروزرسانی", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
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

            // فیلتر و هشدار در صورت انتخاب نام دسکتاپ بدون نام اینستنس سرور
            if (!server.Contains("\\") && server.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                SenikDialog.Show("گزینه انتخاب شده فقط نام رایانه شماست و نام نمونه SQL (مانند SENIK2017) در آن مشخص نیست.\nلطفاً نمونه صحیح را از لیست انتخاب کرده یا به صورت ServerName\\InstanceName تایپ کنید.", "اخطار سرور", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                var dbService = new DatabaseService();
                string masterConn = dbService.BuildConnectionString(server, isWinAuth, user, pass, "master");
                var databases = await Task.Run(() => dbService.GetDatabases(masterConn));

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
            finally
            {
                if (btn != null) btn.IsEnabled = true;
                Mouse.OverrideCursor = null;
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