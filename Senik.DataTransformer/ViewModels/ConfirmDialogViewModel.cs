using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;
using Senik.DataTransformer.Validation;
using Senik.DataTransformer.Views.Windows;
using System;
using System.Windows;

namespace Senik.DataTransformer.ViewModels
{
    public partial class ConfirmDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private ValidationReport _report;

        private NavigateToExecutionPageMessage _payload;

        public int ValidRecordsCount => Report?.ValidRowsCount ?? 0;
        public int ErrorRecordsCount => Report?.ErrorRowsCount ?? 0;
        public bool HasErrors => ErrorRecordsCount > 0;

        // اگر خطای Fatal (مانع درج) در کل پکیج صفر باشد، اجازه اجرای SQL صادر می‌شود
        public bool CanExecuteImport => Report?.CanExecute ?? false;

        public ConfirmDialogViewModel(NavigateToExecutionPageMessage payload)
        {
            _payload = payload;
            Report = payload.CombinedReport;
        }

        [RelayCommand]
        private void DownloadErrorReport()
        {
            try
            {
                var reportService = new ErrorReportService();

                // ✨ این خط کلیدی است که جا افتاده بود! فراخوانی موتور تولید اکسل و ورد
                reportService.GenerateGlobalReports(_payload);

                SenikDialog.Show("گزارش جامع خطاها با موفقیت تولید و پوشه آن در Desktop باز شد.", "تولید گزارش", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SenikDialog.Show($"خطا در تولید گزارش:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}