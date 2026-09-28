using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;
using Senik.DataTransformer.Views.Windows;

namespace Senik.DataTransformer.ViewModels
{
    public class LogEntry
    {
        public string Text { get; set; } = string.Empty;
        public bool IsSummary { get; set; } = false;
        public int SuccessCount { get; set; } = 0;
        public int FailCount { get; set; } = 0;
        public bool IsCancelled { get; set; } = false;

        public LogEntry(string text)
        {
            Text = text;
        }

        public LogEntry() { }
    }

    public partial class ExecutionViewModel : ObservableObject
    {
        [ObservableProperty] private int _progressPercentage = 0;
        [ObservableProperty] private string _statusMessage = "آماده‌سازی موتور SQL...";
        [ObservableProperty] private bool _isCompleted = false;

        public ConcurrentQueue<LogEntry> LogQueue { get; } = new();

        public int TotalRowsToProcess { get; private set; } = 0;
        public int GlobalProcessedRows => _globalProcessedRows;
        private int _globalProcessedRows = 0;

        public string CurrentStatusMessage { get; set; } = "آماده‌سازی موتور SQL...";

        private StringBuilder _logBuilder = new StringBuilder();
        private NavigateToExecutionPageMessage _executionData;
        private CancellationTokenSource? _cancellationTokenSource;
        private string _currentReportFolder = string.Empty;

        public ExecutionViewModel(NavigateToExecutionPageMessage data)
        {
            _executionData = data;
        }

        [RelayCommand]
        public async Task StartExecutionAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            AppState.IsExecuting = true;

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _currentReportFolder = Path.Combine(desktopPath, "Senik_Reports", $"Global_Import_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(_currentReportFolder);

            TotalRowsToProcess = _executionData.CombinedReport.ValidRowsCount;
            _globalProcessedRows = 0;
            CurrentStatusMessage = "آماده‌سازی موتور SQL...";

            Action stepProgress = () =>
            {
                Interlocked.Increment(ref _globalProcessedRows);
            };

            Action<string> updateMessage = (msg) =>
            {
                CurrentStatusMessage = msg;
            };

            AddLog("======================================================");
            AddLog($"▶ [System] عملیات جامع آغاز شد - {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
            AddLog($"▶ [Target] مجموع رکوردهای آماده پردازش: {TotalRowsToProcess}");
            AddLog("======================================================\n");

            await Task.Delay(800);

            int totalSuccess = 0;
            int totalFail = 0;

            try
            {
                var sqlService = new SqlExecutionService();

                await Task.Run(() =>
                {
                    if (_executionData.RunKala)
                    {
                        CurrentStatusMessage = "در حال درج اطلاعات کالاها و انبار...";
                        var (s, f) = sqlService.ExecuteKalaImport(_executionData, AppState.SqlConnectionString, _currentReportFolder, stepProgress, updateMessage, AddLog, _cancellationTokenSource.Token);
                        totalSuccess += s;
                        totalFail += f;
                    }

                    if (_executionData.RunPerson && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        CurrentStatusMessage = "در حال درج تراکنش‌های مالی و اشخاص...";
                        var (s, f) = sqlService.ExecutePersonImport(_executionData, AppState.SqlConnectionString, _currentReportFolder, stepProgress, updateMessage, AddLog, _cancellationTokenSource.Token);
                        totalSuccess += s;
                        totalFail += f;
                    }

                    if (_executionData.RunCheck && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        CurrentStatusMessage = "در حال درج اسناد مالی و چک‌ها...";
                        var (s, f) = sqlService.ExecuteCheckImport(_executionData, AppState.SqlConnectionString, _currentReportFolder, stepProgress, updateMessage, AddLog, _cancellationTokenSource.Token);
                        totalSuccess += s;
                        totalFail += f;
                    }

                }, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    CurrentStatusMessage = "عملیات متوقف شد!";
                    StatusMessage = "عملیات متوقف شد!";
                    AddSummaryLog(totalSuccess, totalFail, isCancelled: true);
                }
                else
                {
                    CurrentStatusMessage = "عملیات جامع با موفقیت به پایان رسید.";
                    StatusMessage = "عملیات جامع با موفقیت به پایان رسید.";
                    ProgressPercentage = 100;
                    AddSummaryLog(totalSuccess, totalFail, isCancelled: false);
                }
            }
            catch (Exception ex)
            {
                CurrentStatusMessage = "خطای بحرانی در موتور!";
                StatusMessage = "خطای بحرانی در موتور!";
                AddLog($"\n❌ [Fatal Error] {ex.Message}");
                AddSummaryLog(totalSuccess, totalFail, isCancelled: false);
            }
            finally
            {
                try
                {
                    string logPath = Path.Combine(_currentReportFolder, "ExecutionLog.txt");
                    File.WriteAllText(logPath, _logBuilder.ToString());
                }
                catch { }

                IsCompleted = true;
                AppState.IsExecuting = false;
            }
        }

        [RelayCommand]
        private void CancelExecution()
        {
            var result = SenikDialog.Show("آیا از لغو عملیات اطمینان دارید؟", "هشدار", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                CurrentStatusMessage = "در حال ارسال سیگنال توقف...";
                StatusMessage = "در حال ارسال سیگنال توقف...";
                _cancellationTokenSource?.Cancel();
            }
        }

        public void AddLog(string log)
        {
            _logBuilder.AppendLine(log);
            LogQueue.Enqueue(new LogEntry(log));
        }

        public void AddSummaryLog(int successCount, int failCount, bool isCancelled)
        {
            var summary = new LogEntry
            {
                IsSummary = true,
                SuccessCount = successCount,
                FailCount = failCount,
                IsCancelled = isCancelled
            };
            LogQueue.Enqueue(summary);

            _logBuilder.AppendLine("\n======================================================");
            if (isCancelled)
            {
                _logBuilder.AppendLine("⚠️ پردازش توسط کاربر متوقف گردید.");
            }
            else
            {
                _logBuilder.AppendLine("🎉 عملیات جامع با موفقیت به پایان رسید.");
            }
            _logBuilder.AppendLine($"✔️ مجموع رکوردهای موفق: {successCount} رکورد");
            _logBuilder.AppendLine($"❌ مجموع رکوردهای ناموفق (شکست): {failCount} رکورد");
            _logBuilder.AppendLine("======================================================");
        }

        [RelayCommand]
        private void OpenReportFolder()
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = _currentReportFolder, UseShellExecute = true }); }
            catch { }
        }

        [RelayCommand]
        private void StartNewOperation()
        {
            WeakReferenceMessenger.Default.Send(new NavigateToDiscoveryPageMessage());
        }
    }
}