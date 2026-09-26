using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace Senik.DataTransformer.Services
{
    public static class SenikTourGuide
    {
        public static Dictionary<string, FrameworkElement> Elements = new Dictionary<string, FrameworkElement>();

        private static Window? _overlayWindow;
        private static Canvas? _mainCanvas;
        private static Path? _overlayPath;
        private static Border? _bubbleBorder;
        private static TextBlock? _txtTitle;
        private static StackPanel? _txtDescPanel;
        private static Button? _btnNext;
        private static Button? _btnSkip;

        private static int _currentStep = 0;
        private static List<(string Key, string Title, string Desc)> _steps = new();

        public static void Register(string key, FrameworkElement element)
        {
            Elements[key] = element;
        }

        public static void StartTour()
        {
            if (_overlayWindow != null) return; // جلوگیری از اجرای همزمان

            _steps = new List<(string, string, string)>
            {
                ("Intro", "به سیستم تبدیل اطلاعات سه نیک خوش آمدید!", ""),
                ("SelectFile", "۱. انتخاب فایل موردنظر", "ابتدا از اینجا فایل اکسل مربوط به تب مثل کالا یا اشخاص یا چک خود را وارد سیستم کنید."),
                ("Download", "۲. دانلود نمونه اکسل", "اگر فایل آماده ندارید، یک فایل خام و استاندارد از اینجا دریافت کنید تا فرمت‌ها به هم نریزد."),
                ("Mapping", "۳. تنظیمات نگاشت", "در این بخش ستون‌های اکسل خود را با دیتابیس هماهنگ کنید."),
                ("Start", "۴. شروع عملیات جامع", "پس از تکمیل تب‌ها، موتور پردازش تراکنش‌های SQL را از اینجا استارت بزنید!")
            };

            _currentStep = 0;

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return;

            _overlayWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Owner = mainWindow, // حل مشکل Z-Index (روی بقیه برنامه‌ها نمی‌افتد)
                ShowInTaskbar = false,
                FontFamily = new FontFamily("pack://application:,,,/Assets/Fonts/#Vazirmatn Medium")
            };

            InitializeUIComponents();

            SyncWindow(null, null!);
            mainWindow.LocationChanged += SyncWindow;
            mainWindow.SizeChanged += SyncWindow;

            _overlayWindow.Show();

            // انیمیشن نرم ورود کل تور
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            _mainCanvas!.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            ShowStep();
        }

        private static void SyncWindow(object? sender, EventArgs e)
        {
            if (_overlayWindow != null && Application.Current.MainWindow != null)
            {
                _overlayWindow.Left = Application.Current.MainWindow.Left;
                _overlayWindow.Top = Application.Current.MainWindow.Top;
                _overlayWindow.Width = Application.Current.MainWindow.ActualWidth;
                _overlayWindow.Height = Application.Current.MainWindow.ActualHeight;

                if (_overlayWindow.IsVisible) UpdateHoleAndBubble();
            }
        }

        // فقط یک بار کامپوننت‌ها ساخته می‌شوند تا چشمک زدن رخ ندهد
        private static void InitializeUIComponents()
        {
            _mainCanvas = new Canvas { Opacity = 0 };

            var bgBrush = (Brush)(Application.Current.TryFindResource("MaterialDesignPaper") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")));
            var primaryBrush = (Brush)(Application.Current.TryFindResource("PrimaryHueMidBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0EA5E9")));
            var primaryForeground = (Brush)(Application.Current.TryFindResource("PrimaryHueMidForegroundBrush") ?? Brushes.White);
            var textLightBrush = (Brush)(Application.Current.TryFindResource("MaterialDesignBodyLight") ?? Brushes.Gray);

            _overlayPath = new Path { Fill = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)) };
            _mainCanvas.Children.Add(_overlayPath);

            _bubbleBorder = new Border
            {
                Background = bgBrush,
                BorderBrush = primaryBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                FlowDirection = FlowDirection.RightToLeft
            };
            _bubbleBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 25, ShadowDepth = 5, Opacity = 0.5 };

            var panel = new StackPanel();
            _txtTitle = new TextBlock { FontWeight = FontWeights.Bold, Foreground = primaryBrush, FontSize = 18, Margin = new Thickness(0, 0, 0, 15) };
            panel.Children.Add(_txtTitle);

            _txtDescPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 25) };
            panel.Children.Add(_txtDescPanel);

            var btnPanel = new Grid();

            _btnNext = new Button
            {
                Background = primaryBrush,
                Foreground = primaryForeground,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(15, 0, 15, 0),
                MinHeight = 38,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.Bold
            };
            var btnRes = new Style(typeof(Border));
            btnRes.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(8)));
            _btnNext.Resources.Add(typeof(Border), btnRes);
            _btnNext.Click += (s, e) => { _currentStep++; ShowStep(); };
            btnPanel.Children.Add(_btnNext);

            _btnSkip = new Button
            {
                Content = "خروج",
                Background = Brushes.Transparent,
                Foreground = textLightBrush,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinHeight = 38,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnSkip.Click += (s, e) => EndTour();
            btnPanel.Children.Add(_btnSkip);

            panel.Children.Add(btnPanel);
            _bubbleBorder.Child = panel;

            _mainCanvas.Children.Add(_bubbleBorder);
            _overlayWindow!.Content = _mainCanvas;
        }

        private static void ShowStep()
        {
            if (_currentStep >= _steps.Count)
            {
                EndTour();
                return;
            }

            var step = _steps[_currentStep];

            if (step.Key != "Intro" && (!Elements.ContainsKey(step.Key) || !Elements[step.Key].IsVisible))
            {
                _currentStep++;
                ShowStep();
                return;
            }

            _txtTitle!.Text = step.Title;
            _btnNext!.Content = step.Key == "Intro" ? "شروع راهنما" : (_currentStep == _steps.Count - 1 ? "پایان راهنما" : "متوجه شدم (بعدی)");

            UpdateDescriptionTexts(step);
            UpdateHoleAndBubble();
        }

        private static void UpdateDescriptionTexts((string Key, string Title, string Desc) step)
        {
            _txtDescPanel!.Children.Clear();
            var textBrush = (Brush)(Application.Current.TryFindResource("MaterialDesignBody") ?? Brushes.WhiteSmoke);
            var primaryBrush = (Brush)(Application.Current.TryFindResource("PrimaryHueMidBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0EA5E9")));
            var textLightBrush = (Brush)(Application.Current.TryFindResource("MaterialDesignBodyLight") ?? Brushes.Gray);

            if (step.Key == "Intro")
            {
                _txtDescPanel.Children.Add(new TextBlock { Text = "نرم‌افزار جامع و هوشمند برای تبدیل، اعتبارسنجی و انتقال امن اطلاعات شما به پایگاه داده.", TextWrapping = TextWrapping.Wrap, Foreground = textBrush, FontSize = 14, Margin = new Thickness(0, 0, 0, 20) });
                _txtDescPanel.Children.Add(new TextBlock { Text = "📞 پشتیبانی: 0992-506-3800", Foreground = primaryBrush, FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) });
                _txtDescPanel.Children.Add(new TextBlock { Text = "🏢 گروه نرم افزاری سه نیک؛ نماد هوشمندی و یکپارچگی", Foreground = textBrush, FontSize = 13, Margin = new Thickness(0, 0, 0, 15) });
                _txtDescPanel.Children.Add(new TextBlock { Text = "💻 توسعه دهنده: iMysTicism", Foreground = textLightBrush, FontSize = 12 });
            }
            else
            {
                _txtDescPanel.Children.Add(new TextBlock { Text = step.Desc, TextWrapping = TextWrapping.Wrap, Foreground = textBrush, FontSize = 14, LineHeight = 22 });
            }
        }

        private static void UpdateHoleAndBubble()
        {
            if (_overlayWindow == null || _currentStep >= _steps.Count) return;

            var step = _steps[_currentStep];
            var screenRect = new RectangleGeometry(new Rect(0, 0, _overlayWindow.Width, _overlayWindow.Height));

            if (step.Key == "Intro")
            {
                _overlayPath!.Data = new CombinedGeometry(GeometryCombineMode.Exclude, screenRect, new RectangleGeometry(new Rect(0, 0, 0, 0)));
                _bubbleBorder!.Width = 450;
                Canvas.SetLeft(_bubbleBorder, (_overlayWindow.Width - 450) / 2);
                Canvas.SetTop(_bubbleBorder, (_overlayWindow.Height - 250) / 2);
            }
            else
            {
                FrameworkElement target = Elements[step.Key];

                // 🛠️ حل قطعی فوکوس: محاسبه مختصات دقیق نسبت به پنجره اصلی بدون در نظر گرفتن DPI ویندوز
                Point targetLocation = target.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));

                var holeRect = new RectangleGeometry(new Rect(targetLocation.X - 5, targetLocation.Y - 5, target.ActualWidth + 10, target.ActualHeight + 10), 8, 8);

                // انیمیشن نرم برای حرکت نورافکن (بدون چشمک زدن)
                _overlayPath!.Data = new CombinedGeometry(GeometryCombineMode.Exclude, screenRect, holeRect);

                _bubbleBorder!.Width = 320;
                double bubbleX = targetLocation.X;
                double bubbleY = targetLocation.Y + target.ActualHeight + 15;

                // جلوگیری از خروج حباب از صفحه
                if (bubbleX + 320 > _overlayWindow.Width) bubbleX = _overlayWindow.Width - 340;
                if (bubbleY + 200 > _overlayWindow.Height) bubbleY = targetLocation.Y - 200;

                Canvas.SetLeft(_bubbleBorder, bubbleX);
                Canvas.SetTop(_bubbleBorder, bubbleY);
            }
        }

        private static void EndTour()
        {
            if (_overlayWindow == null) return;

            // جلوگیری از نشت حافظه با Unsubscribe کردن رویدادها
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.LocationChanged -= SyncWindow;
                Application.Current.MainWindow.SizeChanged -= SyncWindow;
            }

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) =>
            {
                _overlayWindow?.Close();
                _overlayWindow = null;
            };
            _mainCanvas?.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}