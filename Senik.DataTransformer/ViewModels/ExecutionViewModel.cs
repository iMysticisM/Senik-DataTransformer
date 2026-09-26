using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.SqlClient;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;
using Senik.DataTransformer.Views.Windows;

namespace Senik.DataTransformer.ViewModels
{
    public partial class ExecutionViewModel : ObservableObject
    {
        [ObservableProperty] private int _progressPercentage = 0;
        [ObservableProperty] private string _statusMessage = "آماده‌سازی موتور SQL...";
        [ObservableProperty] private string _executionLogs = "";
        [ObservableProperty] private bool _isCompleted = false;

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
            _currentReportFolder = System.IO.Path.Combine(desktopPath, "Senik_Reports", $"Global_Import_{DateTime.Now:yyyyMMdd_HHmmss}");
            System.IO.Directory.CreateDirectory(_currentReportFolder);

            int totalRowsToProcess = _executionData.CombinedReport.ValidRowsCount;
            int globalProcessedRows = 0;

            // ✨ سیستم یکپارچه نوار پیشرفت (حل باگ خلال دندون!)
            Action stepProgress = () =>
            {
                globalProcessedRows++;
                int pct = totalRowsToProcess > 0 ? (int)((globalProcessedRows / (double)totalRowsToProcess) * 100) : 100;
                Application.Current.Dispatcher.InvokeAsync(() => ProgressPercentage = pct);
            };

            Action<string> updateMessage = (msg) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = msg);
            };

            AddLog("======================================================");
            AddLog($"▶ [System] عملیات جامع آغاز شد - {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
            AddLog($"▶ [Target] مجموع رکوردهای آماده پردازش: {totalRowsToProcess}");
            AddLog("======================================================\n");

            await Task.Delay(800);

            try
            {
                var sqlService = new SqlExecutionService();

                await Task.Run(() =>
                {
                    if (_executionData.RunKala)
                    {
                        updateMessage("در حال درج اطلاعات کالاها و انبار...");
                        sqlService.ExecuteKalaImport(_executionData, AppState.SqlConnectionString, _currentReportFolder, stepProgress, updateMessage, AddLog, _cancellationTokenSource.Token);
                    }

                    if (_executionData.RunPerson && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        updateMessage("در حال درج تراکنش‌های مالی و اشخاص...");
                        sqlService.ExecutePersonImport(_executionData, AppState.SqlConnectionString, _currentReportFolder, stepProgress, updateMessage, AddLog, _cancellationTokenSource.Token);
                    }

                    if (_executionData.RunCheck && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        updateMessage("در حال درج اسناد مالی و چک‌ها...");
                        sqlService.ExecuteCheckImport(_executionData, AppState.SqlConnectionString, _currentReportFolder, stepProgress, updateMessage, AddLog, _cancellationTokenSource.Token);
                    }

                }, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "عملیات متوقف شد!";
                    AddLog("\n⚠️ [Warning] پردازش توسط کاربر متوقف گردید.");
                }
                else
                {
                    StatusMessage = "عملیات جامع با موفقیت به پایان رسید.";
                    ProgressPercentage = 100; // اطمینان از پر شدن نهایی
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "خطای بحرانی در موتور!";
                AddLog($"\n❌ [Fatal Error] {ex.Message}");
            }
            finally
            {
                try
                {
                    string logPath = System.IO.Path.Combine(_currentReportFolder, "ExecutionLog.txt");
                    System.IO.File.WriteAllText(logPath, ExecutionLogs);
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
                StatusMessage = "در حال ارسال سیگنال توقف...";
                _cancellationTokenSource?.Cancel();
            }
        }

        private void AddLog(string log)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _logBuilder.AppendLine(log);
                ExecutionLogs = _logBuilder.ToString();
            });
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