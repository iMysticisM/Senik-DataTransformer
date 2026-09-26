using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text;

namespace Senik.DataTransformer.Services
{
    public static class SenikSecurityGuard
    {
        private const string SecurityManifestUrl = "https://gist.githubusercontent.com/iMysticisM/1881cb4446ee20072ee37f2674a044aa/raw/SenikTransformerLicense.json";
        private static readonly string CacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Senik", "sec_guard.dat");

        public static async Task StartGuardAsync(CancellationToken cancellationToken)
        {
            try
            {
                // جلوگیری از Zombie شدن پردازش (اگر کاربر قبل از 10 ثانیه برنامه را بست، این Task هم لغو می‌شود)
                await Task.Delay(10000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return; 
            }

            bool isLocked = CheckOfflineCache();
            string lockMessage = "این نسخه از نرم‌افزار منسوخ شده است. لطفا با پشتیبانی سه نیک تماس بگیرید.";

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    string noCacheUrl = $"{SecurityManifestUrl}?t={DateTime.UtcNow.Ticks}";
                    var response = await client.GetAsync(noCacheUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            bool serverLocked = root.TryGetProperty("is_locked", out var l) && l.GetBoolean();
                            string minVerStr = root.TryGetProperty("min_version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0";

                            if (root.TryGetProperty("message", out var msg))
                                lockMessage = msg.GetString() ?? lockMessage;

                            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
                            Version minVersion = new Version(minVerStr);

                            isLocked = serverLocked || currentVersion < minVersion;
                            UpdateOfflineCache(isLocked);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }

            if (isLocked)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(lockMessage, "پایان پشتیبانی نسخه", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown(); // خاموشی ایمن به جای کشتن خشن
                });
            }
        }

        private static bool CheckOfflineCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    // خواندن و رمزگشایی فایل (جلوگیری از دستکاری ساده با Notepad)
                    string encoded = File.ReadAllText(CacheFilePath).Trim();
                    string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    return decoded == "LOCKED";
                }
            }
            catch { }
            return false; 
        }

        private static void UpdateOfflineCache(bool isLocked)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
                string status = isLocked ? "LOCKED" : "OK";
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(status));
                File.WriteAllText(CacheFilePath, encoded);
            }
            catch (Exception ex) { LogException(ex); }
        }

        private static void LogException(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Senik", "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] SecurityGuard: {ex.Message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}