using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Models;
using Senik.DataTransformer.Services;
using Senik.DataTransformer.Views.Windows;
using Senik.DataTransformer.Views.Components;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;

namespace Senik.DataTransformer.ViewModels
{
    public partial class PersonViewModel : ObservableObject
    {
        private readonly ExcelReaderService _excelService = new ExcelReaderService();
        private List<string> _currentHeaders = new List<string>();

        // ✨ متغیرهای عمومی برای خواندن در DiscoveryViewModel
        public string ActualFilePath { get; private set; } = string.Empty;
        public bool IsMappingConfirmed { get; private set; } = false;

        [ObservableProperty]
        private ObservableCollection<ColumnMappingItem> _savedMappings = new ObservableCollection<ColumnMappingItem>();

        [ObservableProperty] private string _selectedFilePath = "هیچ فایلی انتخاب نشده";
        [ObservableProperty] private ObservableCollection<string> _availableSheets = new ObservableCollection<string>();
        [ObservableProperty] private string _selectedSheet = string.Empty;
        [ObservableProperty] private DataTable? _previewDataTable;

        [ObservableProperty] private string _statusBadgeText = "در انتظار فایل";
        [ObservableProperty] private string _statusBadgeBackground = "#1A64748B";
        [ObservableProperty] private string _statusBadgeBorder = "#64748B";
        [ObservableProperty] private string _statusBadgeForeground = "#475569";
        [ObservableProperty] private string _quickLogText = "▶ ابتدا فایل اکسل اشخاص/مالی خود را انتخاب کنید...";

        [RelayCommand]
        private void DownloadTemplate()
        {
            var saveFileDialog = new SaveFileDialog { Filter = "Excel Files|*.xlsx", Title = "ذخیره فایل نمونه اشخاص", FileName = "Template_Person.xlsx" };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    new TemplateGeneratorService().GenerateTemplateAndGuide(saveFileDialog.FileName, "Person");
                    SenikDialog.Show("فایل نمونه اکسل به همراه فایل Word راهنما با موفقیت ذخیره شدند.", "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { SenikDialog.Show($"خطا در تولید فایل‌ها:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }
        [RelayCommand]
        private async Task SelectFile()
        {
            if (!string.IsNullOrEmpty(ActualFilePath) && SelectedFilePath != "هیچ فایلی انتخاب نشده")
            {
                bool confirmed = await WarningConfirmDialog.ShowAsync(
                    "فایل دیگری در این تب باز است. آیا مطمئن هستید که میخواهید فایل جدیدی را جایگزین کنید؟",
                    "هشدار جایگزینی فایل");
                if (!confirmed) return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Excel Files|*.xls;*.xlsx;*.csv", Title = "انتخاب فایل اشخاص" };
            if (openFileDialog.ShowDialog() == true)
            {
                string chosenFile = openFileDialog.FileName;

                // ۱. بررسی باز بودن فایل در برنامه دیگر
                if (ExcelReaderService.IsFileLocked(chosenFile))
                {
                    SenikDialog.Show("فایل اکسل در برنامه دیگری باز هست. لطفا آن را ببندید و دوباره امتحان کنید.", "فایل در حال استفاده", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var sheetNames = _excelService.GetSheetNames(chosenFile);
                    if (sheetNames == null || sheetNames.Count == 0)
                    {
                        SenikDialog.Show("فایل اکسل انتخابی فاقد شیت معتبر است.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // ۲. اعتبارسنجی تطابق ستون‌های اجباری با تب اشخاص
                    var initialHeaders = _excelService.GetSheetHeaders(chosenFile, sheetNames[0]);
                    if (!ExcelReaderService.ValidateTabHeaders(initialHeaders, "Person"))
                    {
                        SenikDialog.Show("اکسل انتخاب شده با تب مورد نظر تطابق ندارد لطفاً با دقت بیشتر فایل را انتخاب کنید.", "عدم تطابق فایل", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    ActualFilePath = chosenFile;
                    SelectedFilePath = chosenFile;
                    IsMappingConfirmed = false;

                    AvailableSheets.Clear();
                    foreach (var sheet in sheetNames) AvailableSheets.Add(sheet);
                    if (AvailableSheets.Count > 0) SelectedSheet = AvailableSheets[0];

                    StatusBadgeText = "آماده پیکربندی";
                    StatusBadgeBackground = "#1A0EA5E9";
                    StatusBadgeBorder = "#0EA5E9";
                    StatusBadgeForeground = "#0284C7";
                    QuickLogText = "▶ فایل بارگذاری شد. لطفاً روی «تنظیمات نگاشت» کلیک کنید.";
                }
                catch (Exception ex)
                {
                    string errorMessage = ex.Message;
                    if (errorMessage.Contains("being used by another process"))
                    {
                        errorMessage = "فایل اکسل در برنامه دیگری باز هست. لطفا آن را ببندید و دوباره امتحان کنید.";
                    }

                    SenikDialog.Show($"خطا در خواندن فایل:\n{errorMessage}", "خطای دسترسی", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        partial void OnSelectedSheetChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(ActualFilePath)) return;
            try
            {
                // محدودیت لود برای رم
                PreviewDataTable = _excelService.GetSheetPreview(ActualFilePath, value, 5000);
                if (PreviewDataTable != null) _currentHeaders = PreviewDataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            }
            catch { }
        }

        [RelayCommand]
        private void OpenMapping()
        {
            if (string.IsNullOrEmpty(SelectedSheet) || _currentHeaders.Count == 0)
            {
                SenikDialog.Show("لطفا ابتدا فایل اکسل و شیت معتبر را انتخاب کنید.", "اخطار", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ارسال صریح کلمه "Person" برای لود شدن فرمت اشخاص
            AdvancedMappingWindow mappingWindow = new AdvancedMappingWindow(_currentHeaders, SavedMappings, "Person");
            if (Application.Current.MainWindow != null) mappingWindow.Owner = Application.Current.MainWindow;

            bool? result = mappingWindow.ShowDialog();
            if (result == true)
            {
                IsMappingConfirmed = true; // ✨ قفل تایید فعال شد
                StatusBadgeText = "تایید و آماده درج";
                StatusBadgeBackground = "#1A10B981";
                StatusBadgeBorder = "#10B981";
                StatusBadgeForeground = "#059669";
                QuickLogText = "◀️ نگاشت ستون‌های اشخاص با موفقیت ذخیره شد.";
            }
        }

        [RelayCommand]
        private void ResetForm()
        {
            ActualFilePath = string.Empty;
            SelectedFilePath = "هیچ فایلی انتخاب نشده";
            AvailableSheets.Clear();
            SelectedSheet = string.Empty;
            PreviewDataTable = null;
            _currentHeaders.Clear();
            IsMappingConfirmed = false;

            StatusBadgeText = "در انتظار فایل";
            StatusBadgeBackground = "#1A64748B";
            StatusBadgeBorder = "#64748B";
            StatusBadgeForeground = "#475569";
            QuickLogText = "◀️ فرم اشخاص بازنشانی شد. لطفاً فایل جدیدی انتخاب کنید.";
        }
    }
}