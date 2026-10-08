using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Senik.DataTransformer.Models;
using Senik.DataTransformer.Views.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Senik.DataTransformer.ViewModels
{
    public partial class AdvancedMappingViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ColumnMappingItem> _mappings;

        [ObservableProperty]
        private ObservableCollection<string> _excelHeaders;

        public AdvancedMappingViewModel(List<string> realHeaders, ObservableCollection<ColumnMappingItem> existingMappings, string importType)
        {
            // ✨ اضافه کردن گزینه صرف‌نظر به ابتدای لیست
            var headers = new List<string> { "-- صرف‌نظر از این فیلد --" };
            headers.AddRange(realHeaders);

            ExcelHeaders = new ObservableCollection<string>(headers);
            Mappings = existingMappings;

            if (Mappings.Count == 0)
            {
                if (importType == "Person") InitializePersonMappings();
                else if (importType == "Check") InitializeCheckMappings(); // ✨ بعداً این رو پر میکنیم
                else InitializeKalaMappings();

                AutoMapColumns();
            }
        }

        // ✨ متد جدید اتومپینگ با پشتیبانی کامل از حروف فارسی و عربی و نیم‌فاصله‌ها
        private void AutoMapColumns()
        {
            foreach (var mapping in Mappings)
            {
                var cleanTarget = NormalizeHeader(mapping.DisplayName);
                // پیدا کردن دقیق‌ترین تطابق (حذف فاصله‌های اضافی و یکسان‌سازی حروف فارسی و عربی)
                var match = ExcelHeaders.FirstOrDefault(h => NormalizeHeader(h) == cleanTarget);

                if (!string.IsNullOrEmpty(match))
                {
                    mapping.SelectedExcelColumn = match;
                }
            }
        }

        private static string NormalizeHeader(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Trim().Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "");
        }

        private void InitializeKalaMappings()
        {
            Mappings.Clear();

            // فیلدهای اجباری
            Mappings.Add(new ColumnMappingItem { DisplayName = "کد انبار", TargetField = "TblKala.IDAnbar", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "کد کالا", TargetField = "TblKala.IDKala", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "نام کالا", TargetField = "TblKala.Name", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "واحد اصلی کالا", TargetField = "TblKala.Unit1", IsMandatory = true });

            // فیلدهای اختیاری (نام انبار به بخش اختیاری و پس از اجباری‌ها منتقل شد)
            Mappings.Add(new ColumnMappingItem { DisplayName = "نام انبار", TargetField = "TblAnbar.Name", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "واحد فرعی کالا", TargetField = "TblKala.Unit2", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "نرخ تبدیل واحد", TargetField = "TblKala.U1toU2", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "گروه کالا", TargetField = "TblGroupKala", IsMandatory = false });

            // ✨ اصلاح تارگت‌های قیمت برای جلوگیری از تداخل
            Mappings.Add(new ColumnMappingItem { DisplayName = "قیمت فروش", TargetField = "TblPrice.Price_1", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "قیمت مصرف", TargetField = "TblPrice.Price_2", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "قیمت آخرین خرید", TargetField = "TblKala.Bprice", IsMandatory = false });

            Mappings.Add(new ColumnMappingItem { DisplayName = "موجودی انبار", TargetField = "TblTRAnbar.Tedad", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "بارکد", TargetField = "TblBarCode.Barcod", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "درصد مالیات کالا", TargetField = "TblKala.DarsadMaliat", IsMandatory = false });

            // ✨ فیلدهای جدید
            Mappings.Add(new ColumnMappingItem { DisplayName = "حد تجدید سفارش", TargetField = "TblKala.Nrequest", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "شناسه کالا مودی", TargetField = "TblKala.MoadiKalaId", IsMandatory = false });
        }
        private void InitializePersonMappings()
        {
            Mappings.Clear();
            // اجباری‌ها (شماره سند طبق درخواست حذف شد و در موتور SQL به صورت مقدار 1 ثبت می‌شود)
            Mappings.Add(new ColumnMappingItem { DisplayName = "ردیف", TargetField = "TR.sodor", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "شناسه", TargetField = "Person.sh_ozviat", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "نام", TargetField = "TblPerson.name", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "سرفصل", TargetField = "Accounting.srfsl", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "معین", TargetField = "Accounting.moin", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "تفضیلی", TargetField = "Accounting.HSB", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "بدهکار", TargetField = "TR.bed", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "بستانکار", TargetField = "TR.bes", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "تاریخ ثبت", TargetField = "TR.DateR", IsMandatory = true });

            // اختیاری‌ها
            Mappings.Add(new ColumnMappingItem { DisplayName = "شرح", TargetField = "TR.sharh", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "نام خانوادگی", TargetField = "TblPerson.Famil", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "نام پدر", TargetField = "TblPerson.FatherName", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "تاریخ تولد", TargetField = "TblPerson.Dt_Tavalod", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "موبایل", TargetField = "TblPerson.mobile", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "تلفن", TargetField = "TblPerson.Tell", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "آدرس", TargetField = "TblPerson.Addres_mk", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "شعبه", TargetField = "Accounting.Shob", IsMandatory = false });
        }

        // ✨ زیرساخت مپینگ تب چک (فعلاً خالی تا زمان دریافت دیتابیس)
        // ✨ مپینگ‌های جدید برای تب چک و اسناد مالی
        private void InitializeCheckMappings()
        {
            Mappings.Clear();
            Mappings.Add(new ColumnMappingItem { DisplayName = "نوع چک", TargetField = "Check.Type", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "ردیف چک", TargetField = "Check.Id", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "سریال چک", TargetField = "Check.serial", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "سرفصل پس از وصول", TargetField = "Check.sarfasl1", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "معین پس از وصول", TargetField = "Check.moin1", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "حساب پس از وصول", TargetField = "Check.hsb1", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "سرفصل چک", TargetField = "Check.sarfasl2", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "معین چک", TargetField = "Check.moin2", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "حساب چک", TargetField = "Check.hsb2", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "تاریخ سررسید", TargetField = "Check.DateChek", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "وضعیت چک", TargetField = "Check.State", IsMandatory = true });
            Mappings.Add(new ColumnMappingItem { DisplayName = "مبلغ", TargetField = "Check.mab", IsMandatory = true });

            // ✨ موارد زیر طبق درخواستت اختیاری شدند (IsMandatory = false)
            Mappings.Add(new ColumnMappingItem { DisplayName = "بانک", TargetField = "Check.Bank", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "شعبه", TargetField = "Check.shob", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "دریافت کننده", TargetField = "Check.sharh", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "تاریخ صدور", TargetField = "Check.DateR", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "کد شعبه", TargetField = "Check.CodShobe", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "شماره حساب", TargetField = "Check.numhesab", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "شماره صیاد", TargetField = "Check.SayadNum", IsMandatory = false });
            Mappings.Add(new ColumnMappingItem { DisplayName = "سری چک", TargetField = "Check.SeriNum", IsMandatory = false });
        }
        public (bool IsValid, string ErrorMessage) ValidateMappings()
        {
            var selectedColumns = new List<string>();

            foreach (var mapping in Mappings)
            {
                // ✨ اگر کاربر "صرف‌نظر" را انتخاب کرده بود، مقدار را null کن تا موتورها نادیده‌اش بگیرند
                if (mapping.SelectedExcelColumn == "-- صرف‌نظر از این فیلد --")
                {
                    mapping.SelectedExcelColumn = null;
                }

                if (mapping.IsMandatory && string.IsNullOrWhiteSpace(mapping.SelectedExcelColumn))
                {
                    return (false, $"فیلد اجباری «{mapping.DisplayName}» خالی است. لطفاً ستون معادل آن را انتخاب کنید.");
                }

                if (!string.IsNullOrWhiteSpace(mapping.SelectedExcelColumn))
                {
                    selectedColumns.Add(mapping.SelectedExcelColumn);
                }
            }

            var duplicates = selectedColumns.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
            {
                return (false, $"ستون اکسل «{duplicates[0]}» به بیش از یک فیلد اختصاص داده شده است! انتخاب تکراری مجاز نیست.");
            }

            return (true, string.Empty);
        }
        [RelayCommand]
        public void ForceAutoMap()
        {
            AutoMapColumns();
            SenikDialog.Show($"نگاشت هوشمند انجام شد. لطفا قبل از ذخیره و اعمال نگاشت، از صحت دقیق ستون های معادل مطمئن شوید.", "موفق", MessageBoxButton.OK, MessageBoxImage.Information);

            // در صورت تمایل می‌توانی اینجا یک پیام موفقیت هم با SenikDialog نشان دهی
        }
        public void ClearMappingSelections()
        {
            foreach (var mapping in Mappings) mapping.SelectedExcelColumn = null;
        }
    }

}