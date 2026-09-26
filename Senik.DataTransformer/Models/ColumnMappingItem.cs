using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Senik.DataTransformer.Models
{
    public partial class ColumnMappingItem : ObservableObject
    {
        // عنوان فارسی فیلد در سیستم سه نیک
        [ObservableProperty]
        private string _displayName = string.Empty;

        // نام فیلد در دیتابیس و جدول مقصد
        [ObservableProperty]
        private string _targetField = string.Empty;

        [ObservableProperty]
        private string _targetTable = string.Empty;

        // آیا فیلد اجباری است؟
        [ObservableProperty]
        private bool _isMandatory;

        // توضیحات راهنما برای پشتیبان
        [ObservableProperty]
        private string _description = string.Empty;

        // ستون انتخاب شده از فایل اکسل توسط کاربر
        [ObservableProperty]
        private string? _selectedExcelColumn;

        // دسته‌بندی فیلد جهت نمایش گروهی (مثلا: اطلاعات پایه، مالی و قیمت، انبارداری)
        [ObservableProperty]
        private string _category = string.Empty;

        // آیا مپینگ این ستون معتبر است؟
        public bool IsValid => !IsMandatory || !string.IsNullOrWhiteSpace(SelectedExcelColumn);
    }
}