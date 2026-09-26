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
        private static readonly string TempFilePath = Path.Combine(Path.GetTempPath(), "Senik.ِataTransformer.exe");

        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
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

            using (var client = new HttpClient())
            using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(TempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? 1;
                    var buffer = new byte[8192];
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
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string batPath = Path.Combine(Path.GetTempPath(), "SenikUpdater.bat");

            string batContent = $@"
            @echo off
            :loop
            tasklist | find /i ""Senik.DataTransformer.exe"" >nul
            if %errorlevel%==0 (
            timeout /t 1 /nobreak >nul
            goto loop
            )
            move /y ""{TempFilePath}"" ""{currentExePath}""
            start """" ""{currentExePath}""
            del ""%~f0""
            ";
            try
            {
                File.WriteAllText(batPath, batContent);
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
                Application.Current.Shutdown();
            }
            catch (Exception ex) { LogError(ex); }
        }

        public static void LogError(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Senik", "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Updater: {ex.Message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}