using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
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

namespace Senik.DataTransformer.ViewModels
{
    public partial class KalaViewModel : ObservableObject
    {
        private readonly ExcelReaderService _excelService = new ExcelReaderService();
        private List<string> _currentHeaders = new List<string>();

        public string ActualFilePath { get; private set; } = string.Empty;
        public bool IsMappingConfirmed { get; private set; } = false;

        [ObservableProperty] private ObservableCollection<ColumnMappingItem> _savedMappings = new ObservableCollection<ColumnMappingItem>();
        [ObservableProperty] private string _selectedFilePath = "هیچ فایلی انتخاب نشده";
        [ObservableProperty] private ObservableCollection<string> _availableSheets = new ObservableCollection<string>();
        [ObservableProperty] private string _selectedSheet = string.Empty;
        [ObservableProperty] private DataTable? _previewDataTable;

        [ObservableProperty] private string _statusBadgeText = "در انتظار فایل";
        [ObservableProperty] private string _statusBadgeBackground = "#1A64748B";
        [ObservableProperty] private string _statusBadgeBorder = "#64748B";
        [ObservableProperty] private string _statusBadgeForeground = "#475569";
        [ObservableProperty] private string _quickLogText = "▶ ابتدا فایل اکسل کالای خود را انتخاب کنید...";

        [RelayCommand]
        private void DownloadTemplate()
        {
            var saveFileDialog = new SaveFileDialog { Filter = "Excel Files|*.xlsx", Title = "ذخیره فایل نمونه کالا", FileName = "Template_Kala.xlsx" };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    new TemplateGeneratorService().GenerateTemplateAndGuide(saveFileDialog.FileName, "Kala");
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

            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Excel Files|*.xls;*.xlsx;*.csv", Title = "انتخاب فایل کالا و انبار" };
            if (openFileDialog.ShowDialog() == true)
            {
                ActualFilePath = openFileDialog.FileName;
                SelectedFilePath = openFileDialog.FileName;
                IsMappingConfirmed = false;
                try
                {
                    var sheetNames = _excelService.GetSheetNames(ActualFilePath);
                    AvailableSheets.Clear();
                    foreach (var sheet in sheetNames) AvailableSheets.Add(sheet);
                    if (AvailableSheets.Count > 0) SelectedSheet = AvailableSheets[0];

                    StatusBadgeText = "آماده پیکربندی";
                    StatusBadgeBackground = "#1A0EA5E9";
                    StatusBadgeBorder = "#0EA5E9";
                    StatusBadgeForeground = "#0284C7";
                    QuickLogText = "▶ فایل کالا بارگذاری شد. لطفاً روی «تنظیمات نگاشت» کلیک کنید.";
                }
                catch (Exception ex)
                {
                    string errorMessage = ex.Message;
                    if (errorMessage.Contains("being used by another process")) errorMessage = "این فایل توسط برنامه دیگری (احتمالاً اکسل) باز است.\nلطفاً ابتدا فایل را ببندید.";
                    SenikDialog.Show($"خطا در خواندن فایل:\n{errorMessage}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        partial void OnSelectedSheetChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(ActualFilePath)) return;
            try
            {
                PreviewDataTable = _excelService.GetSheetPreview(ActualFilePath, value, 5000);
                if (PreviewDataTable != null) _currentHeaders = PreviewDataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            }
            catch { }
        }

        [RelayCommand]
        private void OpenMapping()
        {
            if (string.IsNullOrEmpty(SelectedSheet) || _currentHeaders.Count == 0) return;
            AdvancedMappingWindow mappingWindow = new AdvancedMappingWindow(_currentHeaders, SavedMappings, "Kala");
            if (Application.Current.MainWindow != null) mappingWindow.Owner = Application.Current.MainWindow;
            if (mappingWindow.ShowDialog() == true)
            {
                IsMappingConfirmed = true;
                StatusBadgeText = "تایید و آماده درج";
                StatusBadgeBackground = "#1A10B981";
                StatusBadgeBorder = "#10B981";
                StatusBadgeForeground = "#059669";
                QuickLogText = "◀️ نگاشت ستون‌های کالا با موفقیت ذخیره شد.";
            }
        }

        [RelayCommand]
        private void ResetForm()
        {
            ActualFilePath = string.Empty; SelectedFilePath = "هیچ فایلی انتخاب نشده"; AvailableSheets.Clear(); SelectedSheet = string.Empty; PreviewDataTable = null; _currentHeaders.Clear(); IsMappingConfirmed = false;
            StatusBadgeText = "در انتظار فایل"; StatusBadgeBackground = "#1A64748B"; StatusBadgeBorder = "#64748B";
            StatusBadgeForeground = "#475569"; // ✨ باگ هشتگ فیکس شد
            QuickLogText = "◀️ فرم کالا بازنشانی شد. لطفاً فایل جدیدی انتخاب کنید.";
        }
    }
}