using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Senik.DataTransformer.Core
{
    public static class ThemeManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.cfg");

        public static bool IsDarkMode()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return File.ReadAllText(ConfigPath).Trim() == "Dark";
            }
            catch { }
            return false;
        }

        public static void SaveTheme(bool isDark)
        {
            try { File.WriteAllText(ConfigPath, isDark ? "Dark" : "Light"); } catch { }
        }

        // این متد روی کل نرم افزار (تمام پنجره ها و دیالوگ ها) اعمال میشود
        public static void ApplyGlobalTheme(bool isDark)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            if (isDark)
            {
                theme.SetBaseTheme(BaseTheme.Dark);
                // شخصی سازی متریال دیزاین: تزریق رنگ سورمه ای عمیق به جای خاکستری در کل برنامه
                Application.Current.Resources["MaterialDesignPaper"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#151136"));
            }
            else
            {
                theme.SetBaseTheme(BaseTheme.Light);
                Application.Current.Resources["MaterialDesignPaper"] = new SolidColorBrush(Colors.White);
            }

            paletteHelper.SetTheme(theme);
        }
    }
}