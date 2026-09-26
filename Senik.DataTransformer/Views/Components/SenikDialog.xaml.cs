using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Media;
using MaterialDesignThemes.Wpf;

namespace Senik.DataTransformer.Views.Windows
{
    public partial class SenikDialog : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public SenikDialog(object message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            InitializeComponent();

            if (message is string stringMessage)
            {
                stringMessage = stringMessage.Replace("\\n", "\n");

                TextBlock txtMsg = new TextBlock
                {
                    Text = stringMessage,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center
                };
                // اتصال به رنگ متن استاندارد متریال دیزاین (حل مشکل ناخوانایی در دارک مود)
                txtMsg.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesignBody");

                ContentBody.Content = txtMsg;
            }
            else
            {
                ContentBody.Content = message;
            }

            TxtTitle.Text = title;

            // تنظیم رنگ‌ها به صورت استاندارد
            if (image == MessageBoxImage.Error)
            {
                IconAlert.Kind = PackIconKind.CloseCircleOutline;
                TxtTitle.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // قرمز
                IconAlert.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                SystemSounds.Hand.Play();
            }
            else if (image == MessageBoxImage.Warning)
            {
                IconAlert.Kind = PackIconKind.AlertCircleOutline;
                TxtTitle.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // نارنجی اخطار
                IconAlert.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                SystemSounds.Exclamation.Play();
            }
            else if (image == MessageBoxImage.Question)
            {
                IconAlert.Kind = PackIconKind.HelpCircleOutline;
                TxtTitle.Foreground = new SolidColorBrush(Color.FromRgb(14, 165, 233)); // آبی
                IconAlert.Foreground = new SolidColorBrush(Color.FromRgb(14, 165, 233));
                SystemSounds.Question.Play();
            }
            else
            {
                IconAlert.Kind = PackIconKind.InformationOutline;
                TxtTitle.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // سبز موفقیت
                IconAlert.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                SystemSounds.Asterisk.Play();
            }

            SetupButtons(buttons);
        }

        private void SetupButtons(MessageBoxButton buttons)
        {
            SpButtons.Children.Clear();

            if (buttons == MessageBoxButton.OK)
            {
                AddButton("تایید", MessageBoxResult.OK, true);
            }
            else if (buttons == MessageBoxButton.YesNo)
            {
                AddButton("بله", MessageBoxResult.Yes, true);
                AddButton("خیر", MessageBoxResult.No, false);
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool isPrimary)
        {
            Button btn = new Button
            {
                Content = text,
                Width = 120,
                Height = 40,
                FontSize = 14,
                Margin = new Thickness(5, 0, 5, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Vazirmatn Bold")
            };

            ButtonAssist.SetCornerRadius(btn, new CornerRadius(14));

            // حذف سایه زشت و قدیمی دکمه (Elevation = 0)
            ElevationAssist.SetElevation(btn, Elevation.Dp0);

            if (isPrimary)
            {
                btn.Style = Application.Current.TryFindResource("MaterialDesignRaisedButton") as Style;
                btn.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // سبز مدرن
                btn.BorderThickness = new Thickness(0);
                btn.Foreground = new SolidColorBrush(Colors.White);
                btn.IsDefault = true;

                this.Loaded += (s, e) => {
                    btn.Focus();
                    System.Windows.Input.Keyboard.Focus(btn);
                };
            }
            else
            {
                btn.Style = Application.Current.TryFindResource("MaterialDesignOutlinedButton") as Style;
                btn.SetResourceReference(Button.ForegroundProperty, "MaterialDesignBodyLight");
                btn.SetResourceReference(Button.BorderBrushProperty, "MaterialDesignDivider");
                btn.IsCancel = true;
            }

            btn.Click += (s, e) =>
            {
                this.Result = result;
                this.Close();
            };

            SpButtons.Children.Add(btn);
        }

        public static MessageBoxResult Show(object message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            MessageBoxResult dialogResult = MessageBoxResult.None;
            Application.Current.Dispatcher.Invoke(() =>
            {
                SenikDialog dialog = new SenikDialog(message, title, buttons, image);

                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    dialog.Owner = Application.Current.MainWindow;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                dialog.ShowDialog();
                dialogResult = dialog.Result;
            });

            return dialogResult;
        }
    }
}