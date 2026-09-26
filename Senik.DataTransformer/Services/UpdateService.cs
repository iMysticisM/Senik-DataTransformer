using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;

namespace Senik.DataTransformer.Services
{
    public class UpdateInfo
    {
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
    }

    public static class UpdateService
    {
        private const string UpdateUrl = "https://raw.githubusercontent.com/iMysticisM/Senik-DataTransformer/refs/heads/main/update.json";
        private static readonly string TempFilePath = Path.Combine(Path.GetTempPath(), "Senik_Update_New.exe");

        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) })
                {
                    string noCacheUrl = $"{UpdateUrl}?t={DateTime.UtcNow.Ticks}";
                    string json = await client.GetStringAsync(noCacheUrl);

                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        string latestVerStr = root.GetProperty("Version").GetString() ?? "1.0.0";
                        Version latest = new Version(latestVerStr);
                        Version current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

                        if (latest > current)
                        {
                            return new UpdateInfo
                            {
                                DownloadUrl = root.TryGetProperty("ExeUrl", out var exeUrl) ? exeUrl.GetString() ?? "" : "",
                                ReleaseNotes = root.TryGetProperty("Notes", out var notes) ? notes.GetString() ?? "" : "",
                                IsMandatory = root.TryGetProperty("IsMandatory", out var mandatory) && mandatory.GetBoolean()
                            };
                        }
                    }
                }
            }
            catch (Exception ex) { LogError(ex); }
            return null;
        }

        public static async Task DownloadUpdateAsync(string downloadUrl, IProgress<int> progress)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new Exception("لینک دانلود در سرور تنظیم نشده است (حالت دیباگ).");

            if (File.Exists(TempFilePath))
            {
                try { File.Delete(TempFilePath); } catch { }
            }

            using (var client = new HttpClient())
            using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(TempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? 1;
                    var buffer = new byte[16384];
                    long totalRead = 0;
                    int read;

                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        progress.Report((int)((totalRead * 100) / totalBytes));
                    }
                }
            }
        }

        public static void ApplyUpdateAndRestart()
        {
            try
            {
                string currentExePath = Environment.ProcessPath 
                    ?? Process.GetCurrentProcess().MainModule?.FileName 
                    ?? "";

                if (string.IsNullOrWhiteSpace(currentExePath) || !File.Exists(TempFilePath))
                    return;

                int pid = Process.GetCurrentProcess().Id;
                string batPath = Path.Combine(Path.GetTempPath(), "SenikUpdater.bat");

                string batContent = 
                    "@echo off\r\n" +
                    "chcp 65001 >nul\r\n" +
                    $"set PID={pid}\r\n" +
                    $"set TARGET=\"{currentExePath}\"\r\n" +
                    $"set SOURCE=\"{TempFilePath}\"\r\n" +
                    "\r\n" +
                    ":wait_loop\r\n" +
                    "tasklist /fi \"PID eq %PID%\" 2>nul | find \"%PID%\" >nul\r\n" +
                    "if not errorlevel 1 (\r\n" +
                    "    timeout /t 1 /nobreak >nul\r\n" +
                    "    goto wait_loop\r\n" +
                    ")\r\n" +
                    "\r\n" +
                    ":: آزادسازی قفل فایل توسط سیستم‌عامل\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +
                    "\r\n" +
                    ":copy_loop\r\n" +
                    "copy /y %SOURCE% %TARGET% >nul 2>&1\r\n" +
                    "if errorlevel 1 (\r\n" +
                    "    timeout /t 1 /nobreak >nul\r\n" +
                    "    goto copy_loop\r\n" +
                    ")\r\n" +
                    "\r\n" +
                    ":: پاکسازی فایل دانلود شده موقت\r\n" +
                    "del /f /q %SOURCE% >nul 2>&1\r\n" +
                    "\r\n" +
                    ":: راه‌اندازی فایل به‌روزرسانی شده\r\n" +
                    "start \"\" %TARGET%\r\n" +
                    "\r\n" +
                    ":: حذف خود فایل اسکریپت\r\n" +
                    "(goto) 2>nul & del \"%~f0\"\r\n";

                File.WriteAllText(batPath, batContent, System.Text.Encoding.Default);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batPath}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        public static void LogError(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Senik", "error.log");
                string dir = Path.GetDirectoryName(logPath) ?? "";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(logPath, $"[{DateTime.Now}] Updater: {ex.Message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}