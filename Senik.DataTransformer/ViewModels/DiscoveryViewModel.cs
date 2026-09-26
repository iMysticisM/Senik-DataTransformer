using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Services;
using Senik.DataTransformer.Views.Windows;
using System.Windows;
using Senik.DataTransformer.Validation;
using System.Collections.Generic;

namespace Senik.DataTransformer.ViewModels
{
    public partial class DiscoveryViewModel : ObservableObject
    {
        public KalaViewModel KalaVM { get; } = new KalaViewModel();
        public PersonViewModel PersonVM { get; } = new PersonViewModel();
        public CheckViewModel CheckVM { get; } = new CheckViewModel();

        [ObservableProperty] private object _currentViewModel;
        [ObservableProperty] private bool _isKalaTabSelected = true;
        [ObservableProperty] private bool _isPersonTabSelected;
        [ObservableProperty] private bool _isCheckTabSelected;

        public DiscoveryViewModel()
        {
            CurrentViewModel = KalaVM;
        }

        partial void OnIsKalaTabSelectedChanged(bool value) { if (value) CurrentViewModel = KalaVM; }
        partial void OnIsPersonTabSelectedChanged(bool value) { if (value) CurrentViewModel = PersonVM; }
        partial void OnIsCheckTabSelectedChanged(bool value) { if (value) CurrentViewModel = CheckVM; }

        [RelayCommand]
        private void GlobalValidateAndProceed()
        {
            // وضعیت انتخاب فایل
            bool hasKalaFile = !string.IsNullOrEmpty(KalaVM.ActualFilePath);
            bool hasPersonFile = !string.IsNullOrEmpty(PersonVM.ActualFilePath);
            bool hasCheckFile = !string.IsNullOrEmpty(CheckVM.ActualFilePath);

            // وضعیت تایید نگاشت
            bool kalaConfirmed = KalaVM.IsMappingConfirmed;
            bool personConfirmed = PersonVM.IsMappingConfirmed;
            bool checkConfirmed = CheckVM.IsMappingConfirmed;

            // 1. بررسی: هیچ کاری انجام نشده است
            if (!hasKalaFile && !hasPersonFile && !hasCheckFile)
            {
                SenikDialog.Show("هیچکدام از تب‌ها فایلی دریافت نکرده‌اند! لطفاً حداقل یک فایل اکسل را تنظیم کنید.", "اخطار سیستم", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. بررسی حیاتی: فایلی انتخاب شده اما تنظیمات نگاشت آن ذخیره نشده است
            List<string> unmappedTabs = new List<string>();
            if (hasKalaFile && !kalaConfirmed) unmappedTabs.Add("کالا و انبار");
            if (hasPersonFile && !personConfirmed) unmappedTabs.Add("اشخاص و طرف‌حساب‌ها");
            if (hasCheckFile && !checkConfirmed) unmappedTabs.Add("چک و اسناد مالی");

            if (unmappedTabs.Count > 0)
            {
                string tabs = string.Join("، ", unmappedTabs);
                SenikDialog.Show($"شما در تب‌های [{tabs}] فایل بارگذاری کرده‌اید اما تنظیمات نگاشت آن‌ها را انجام نداده‌اید!\nلطفاً ابتدا به این تب‌ها رفته و روی دکمه «تنظیمات نگاشت» کلیک کرده و ذخیره را بزنید.", "خطای نگاشت ناقص", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. بررسی هشدار: برخی تب‌ها کاملاً خالی رها شده‌اند
            List<string> missingTabs = new List<string>();
            if (!hasKalaFile) missingTabs.Add("کالا و انبار");
            if (!hasPersonFile) missingTabs.Add("اشخاص و طرف‌حساب‌ها");
            if (!hasCheckFile) missingTabs.Add("چک و اسناد مالی");

            if (missingTabs.Count > 0 && missingTabs.Count < 3)
            {
                string tabs = string.Join("، ", missingTabs);
                var result = SenikDialog.Show($"تب‌های [{tabs}] کاملاً خالی هستند و دیتایی برای آنها ثبت نمی‌شود.\nآیا می‌خواهید عملیات جامع فقط برای بخش‌های آماده و تایید شده انجام شود؟", "هشدار تاییدیه", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No) return;
            }

            // ادامه پردازش...
            var valService = new ValidationService();
            var payload = new NavigateToExecutionPageMessage();

            if (kalaConfirmed)
            {
                var report = valService.ValidateExcelData(KalaVM.ActualFilePath, KalaVM.SelectedSheet, KalaVM.SavedMappings);
                foreach (var err in report.Errors) err.SourceType = "Kala";
                payload.RunKala = true;
                payload.KalaFilePath = KalaVM.ActualFilePath;
                payload.KalaSheetName = KalaVM.SelectedSheet;
                payload.KalaMappings = KalaVM.SavedMappings;
                MergeReports(payload.CombinedReport, report);
            }

            if (personConfirmed)
            {
                var report = valService.ValidatePersonExcelData(PersonVM.ActualFilePath, PersonVM.SelectedSheet, PersonVM.SavedMappings);
                foreach (var err in report.Errors) err.SourceType = "Person";
                payload.RunPerson = true;
                payload.PersonFilePath = PersonVM.ActualFilePath;
                payload.PersonSheetName = PersonVM.SelectedSheet;
                payload.PersonMappings = PersonVM.SavedMappings;
                MergeReports(payload.CombinedReport, report);
            }

            if (checkConfirmed)
            {
                var report = valService.ValidateCheckExcelData(CheckVM.ActualFilePath, CheckVM.SelectedSheet, CheckVM.SavedMappings);
                foreach (var err in report.Errors) err.SourceType = "Check";
                payload.RunCheck = true;
                payload.CheckFilePath = CheckVM.ActualFilePath;
                payload.CheckSheetName = CheckVM.SelectedSheet;
                payload.CheckMappings = CheckVM.SavedMappings;
                MergeReports(payload.CombinedReport, report);
            }

            WeakReferenceMessenger.Default.Send(new OpenConfirmDialogMessage { ExecutionPayload = payload });
        }

        private void MergeReports(ValidationReport target, ValidationReport source)
        {
            target.TotalRowsScanned += source.TotalRowsScanned;
            target.ValidRowsCount += source.ValidRowsCount;
            target.ErrorRowsCount += source.ErrorRowsCount;
            target.Errors.AddRange(source.Errors);
        }
    }
}