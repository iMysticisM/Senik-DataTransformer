using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace Senik.DataTransformer.Services
{
    public static class FontInstallerService
    {
        [DllImport("gdi32.dll", EntryPoint = "AddFontResourceW", SetLastError = true)]
        private static extern int AddFontResource([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int HWND_BROADCAST = 0xffff;
        private const uint WM_FONTCHANGE = 0x001D;

        private static readonly (string FileName, string Title)[] VazirFonts = new[]
        {
            ("Vazirmatn-Black.ttf", "Vazirmatn Black (TrueType)"),
            ("Vazirmatn-Bold.ttf", "Vazirmatn Bold (TrueType)"),
            ("Vazirmatn-ExtraBold.ttf", "Vazirmatn ExtraBold (TrueType)"),
            ("Vazirmatn-ExtraLight.ttf", "Vazirmatn ExtraLight (TrueType)"),
            ("Vazirmatn-Light.ttf", "Vazirmatn Light (TrueType)"),
            ("Vazirmatn-Medium.ttf", "Vazirmatn Medium (TrueType)"),
            ("Vazirmatn-Regular.ttf", "Vazirmatn Regular (TrueType)"),
            ("Vazirmatn-SemiBold.ttf", "Vazirmatn SemiBold (TrueType)"),
            ("Vazirmatn-Thin.ttf", "Vazirmatn Thin (TrueType)")
        };

        public static void InstallVazirmatnFontsSilentlyInBackground()
        {
            Task.Run(() =>
            {
                try
                {
                    string winFontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                    if (string.IsNullOrEmpty(winFontsPath) || !Directory.Exists(winFontsPath)) return;

                    string baseAppFontsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts");
                    bool anyFontInstalled = false;

                    foreach (var (fileName, title) in VazirFonts)
                    {
                        string destPath = Path.Combine(winFontsPath, fileName);
                        if (!File.Exists(destPath))
                        {
                            string localFilePath = Path.Combine(baseAppFontsDir, fileName);

                            if (File.Exists(localFilePath))
                            {
                                File.Copy(localFilePath, destPath, true);
                            }
                            else
                            {
                                // استخراج از منابع تعبیه‌شده در برنامه (Embedded WPF Resources)
                                var uri = new Uri($"pack://application:,,,/Assets/Fonts/{fileName}", UriKind.Absolute);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        var streamInfo = Application.GetResourceStream(uri);
                                        if (streamInfo != null)
                                        {
                                            using (var fileStream = File.Create(destPath))
                                            {
                                                streamInfo.Stream.CopyTo(fileStream);
                                            }
                                        }
                                    }
                                    catch { }
                                });
                            }

                            if (File.Exists(destPath))
                            {
                                // ثبت فونت در رجیستری ویندوز
                                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", title, fileName, RegistryValueKind.String);
                                AddFontResource(destPath);
                                anyFontInstalled = true;
                            }
                        }
                    }

                    if (anyFontInstalled)
                    {
                        SendMessage((IntPtr)HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                catch
                {
                    // نادیده گرفتن خاموش هرگونه خطا جهت جلوگیری از اختلال در اجرای نرم‌افزار
                }
            });
        }
    }
}
