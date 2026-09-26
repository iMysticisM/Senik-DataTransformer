using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;
using Senik.DataTransformer.ViewModels;
using Senik.DataTransformer.Views.Components;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Senik.DataTransformer.Views
{
    public partial class MainWindow : Window
    {
        private bool _isDarkMode = false;
        private MainViewModel _viewModel;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private const string MainDialogHostName = "RootDialog";

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            // جلوگیری از قفل شدن UI در لحظه اول با اجرای Asynchronous
            Dispatcher.InvokeAsync(() =>
            {
                _isDarkMode = ThemeManager.IsDarkMode();
                ThemeManager.ApplyGlobalTheme(_isDarkMode);
                SyncThemeVisuals(_isDarkMode, TimeSpan.Zero);
            });

            WeakReferenceMessenger.Default.Register<OpenConfirmDialogMessage>(this, async (r, m) =>
            {
                var dialogViewModel = new ConfirmDialogViewModel(m.ExecutionPayload);
                var dialogContent = new ConfirmDialog { DataContext = dialogViewModel };
                var result = await DialogHost.Show(dialogContent, MainDialogHostName);

                if (result is bool isConfirmed && isConfirmed)
                {
                    WeakReferenceMessenger.Default.Send(m.ExecutionPayload);
                }
            });

            WeakReferenceMessenger.Default.Register<NavigateToExecutionPageMessage>(this, (r, m) =>
            {
                MainFrame.Navigate(new Pages.ExecutionPage { DataContext = new ExecutionViewModel(m) });
            });

            WeakReferenceMessenger.Default.Register<NavigateToDiscoveryPageMessage>(this, (r, m) =>
            {
                MainFrame.Navigate(new Pages.DiscoveryPage { DataContext = new DiscoveryViewModel() });
            });

            MainFrame.Navigate(new Pages.DiscoveryPage { DataContext = new DiscoveryViewModel() });

            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed; // جلوگیری از نشت رویدادها
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _cts.Cancel();
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void BtnHelpTour_Click(object sender, RoutedEventArgs e)
        {
            Senik.DataTransformer.Services.SenikTourGuide.StartTour();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();

            var connectionWindow = new Windows.ConnectionWindow();
            bool? result = connectionWindow.ShowDialog();

            if (result == true && connectionWindow.IsConnected)
            {
                this.Show();

                string rawServerName = connectionWindow.CmbServers.Text.Trim();
                string displayServer = rawServerName;

                // پردازش ایمن نام سرور برای جلوگیری از IndexOutOfRangeException
                if (rawServerName.Contains("\\"))
                {
                    var parts = rawServerName.Split('\\');
                    if (parts.Length > 1) displayServer = parts[1];
                }

                _viewModel.ConnectedServerInfo = $"{displayServer}  |  {connectionWindow.CmbDatabases.SelectedItem}";
                _viewModel.IsDatabaseConnected = true;

                // نصب بی‌صدای فونت‌های وزیرمتن در ویندوز در پس‌زمینه
                FontInstallerService.InstallVazirmatnFontsSilentlyInBackground();

                // اجرای زنجیره‌ای وظایف پس از لود موفق
                await RunStartupTasksSequentiallyAsync();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private async Task RunStartupTasksSequentiallyAsync()
        {
            await Senik.DataTransformer.Views.Windows.UpdateWindow.CheckForUpdateAsync();

            // گارد امنیتی بدون قفل کردن نخ اصلی اما با توکن لغو ارسال می‌شود
            _ = Senik.DataTransformer.Services.SenikSecurityGuard.StartGuardAsync(_cts.Token);

            await CheckAndRunFirstTimeTourAsync();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MainBackgroundBorder.CornerRadius = new CornerRadius(30);
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MainBackgroundBorder.CornerRadius = new CornerRadius(0);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.IsExecuting)
            {
                Views.Windows.SenikDialog.Show(
                    "عملیات درج در دیتابیس در حال اجراست!\nبستن نرم‌افزار در این حالت باعث آسیب جدی به پایگاه داده می‌شود. لطفاً ابتدا عملیات را متوقف کنید.",
                    "اخطار امنیتی",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }
            Application.Current.Shutdown();
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ThemeManager.ApplyGlobalTheme(_isDarkMode);
            ThemeManager.SaveTheme(_isDarkMode);
            SyncThemeVisuals(_isDarkMode, TimeSpan.FromMilliseconds(350));
        }

        private void SyncThemeVisuals(bool isDark, TimeSpan duration)
        {
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

            if (isDark)
            {
                AnimateGradientStop(BgGradient1, (Color)ColorConverter.ConvertFromString("#0F0C29"), duration, ease);
                AnimateGradientStop(BgGradient2, (Color)ColorConverter.ConvertFromString("#302B63"), duration, ease);
                AnimateBrushOpacity(GlassBrush, 0.06, duration, ease);
                AnimateSolidColor(GlassBorderBrush, Color.FromArgb(35, 255, 255, 255), duration, ease);
                ThemeIcon.Kind = PackIconKind.WhiteBalanceSunny;
            }
            else
            {
                AnimateGradientStop(BgGradient1, (Color)ColorConverter.ConvertFromString("#E0EAFC"), duration, ease);
                AnimateGradientStop(BgGradient2, (Color)ColorConverter.ConvertFromString("#CFDEF3"), duration, ease);
                AnimateBrushOpacity(GlassBrush, 0.45, duration, ease);
                AnimateSolidColor(GlassBorderBrush, Color.FromArgb(128, 255, 255, 255), duration, ease);
                ThemeIcon.Kind = PackIconKind.WeatherNight;
            }
        }

        // تبدیل به async Task برای جلوگیری از کرش‌های خاموش Async Void
        private async Task CheckAndRunFirstTimeTourAsync()
        {
            try
            {
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Senik");
                string tourFlagFile = Path.Combine(appDataFolder, "tour_seen.dat");

                if (!File.Exists(tourFlagFile))
                {
                    Directory.CreateDirectory(appDataFolder);
                    File.WriteAllText(tourFlagFile, "User has seen the tour");

                    await Task.Delay(1000, _cts.Token);
                    Senik.DataTransformer.Services.SenikTourGuide.StartTour();
                }
            }
            catch { }
        }

        private void AnimateGradientStop(GradientStop stop, Color targetColor, TimeSpan duration, IEasingFunction easing)
        {
            if (duration == TimeSpan.Zero) { stop.Color = targetColor; return; }
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation { To = targetColor, Duration = duration, EasingFunction = easing });
        }

        private void AnimateSolidColor(SolidColorBrush brush, Color targetColor, TimeSpan duration, IEasingFunction easing)
        {
            if (duration == TimeSpan.Zero) { brush.Color = targetColor; return; }
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation { To = targetColor, Duration = duration, EasingFunction = easing });
        }

        private void AnimateBrushOpacity(SolidColorBrush brush, double targetOpacity, TimeSpan duration, IEasingFunction easing)
        {
            if (duration == TimeSpan.Zero) { brush.Opacity = targetOpacity; return; }
            brush.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation { To = targetOpacity, Duration = duration, EasingFunction = easing });
        }
    }
}