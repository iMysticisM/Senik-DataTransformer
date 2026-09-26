using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Senik.DataTransformer.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace Senik.DataTransformer.Services
{
    public class TemplateGeneratorService
    {
        public void GenerateTemplateAndGuide(string excelSavePath, string importType)
        {
            // 1. دریافت نگاشت‌های پیش‌فرض بر اساس نوع فرم
            var mappings = GetDefaultMappings(importType);

            // ✨ 2. مرتب‌سازی: اول اجباری‌ها، سپس اختیاری‌ها
            var sortedMappings = mappings.OrderByDescending(m => m.IsMandatory).ToList();

            // 3. تولید فایل اکسل با رنگ‌بندی
            GenerateExcel(excelSavePath, sortedMappings);

            // 4. تولید فایل Word در همان مسیر
            string directory = Path.GetDirectoryName(excelSavePath) ?? "";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(excelSavePath);
            string wordSavePath = Path.Combine(directory, $"{fileNameWithoutExt}_Guide.docx");

            GenerateWordGuide(wordSavePath, sortedMappings, importType);
        }

        private void GenerateExcel(string path, List<ColumnMappingItem> mappings)
        {
            using (var wb = new XLWorkbook())
            {
                wb.RightToLeft = true;
                wb.Style.Font.FontName = "Vazir FD";
                wb.Style.Font.FontSize = 11;

                var ws = wb.AddWorksheet("فایل نمونه استاندارد");
                ws.RightToLeft = true;

                for (int i = 0; i < mappings.Count; i++)
                {
                    var map = mappings[i];
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = map.DisplayName;
                    cell.Style.Font.Bold = true;

                    // ✨ استایل رنگی برای اجباری و اختیاری
                    if (map.IsMandatory)
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#6DD9FF"); // آبی فیروزه‌ای
                        cell.Style.Font.FontColor = XLColor.DarkSlateGray;
                    }
                    else
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#93FFC4"); // سبز روشن
                        cell.Style.Font.FontColor = XLColor.DarkOliveGreen;
                    }
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
        }

        private void GenerateWordGuide(string path, List<ColumnMappingItem> mappings, string importType)
        {
            string faTitle = importType == "Kala" ? "کالا و انبار" : importType == "Person" ? "اشخاص و طرف‌حساب‌ها" : "چک و اسناد مالی";

            using (var doc = DocX.Create(path))
            {
                var font = new Xceed.Document.NET.Font("Vazir FD");

                doc.InsertParagraph($"راهنمای جامع تکمیل فایل اکسل ({faTitle})")
                   .Font(font).FontSize(18).Bold().Alignment = Alignment.center;
                doc.InsertParagraph("سیستم تبدیل اطلاعات سه نیک").Font(font).FontSize(12).Alignment = Alignment.center;
                doc.InsertParagraph("__________________________________________________").Alignment = Alignment.center;
                doc.InsertParagraph("").SpacingAfter(10);

                doc.InsertParagraph("🎯 معرفی ساختار فایل اکسل نمونه:")
                   .Font(font).FontSize(14).Bold().Direction = Direction.RightToLeft;

                doc.InsertParagraph("برای راحتی شما، ردیف اول فایل اکسل با دو رنگ متمایز طراحی شده است:")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("🟦 ستون‌های فیروزه‌ای رنگ (اجباری): پر کردن این ستون‌ها کاملاً الزامی است. در صورت خالی بودن، رکورد مربوطه در سیستم ثبت نخواهد شد.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("🟩 ستون‌های سبز رنگ (اختیاری): پر کردن این ستون‌ها دلبخواهی است، اما پیشنهاد می‌شود برای کامل شدن پایگاه داده آن‌ها را وارد کنید.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("").SpacingAfter(10);

                doc.InsertParagraph("⚠️ بایدها و نبایدها (نکات بسیار حیاتی):")
                   .Font(font).FontSize(14).Bold().Direction = Direction.RightToLeft;

                doc.InsertParagraph("❌ نباید ۱: ساخت جدول (Format as Table) در اکسل ممنوع!")
                   .Font(font).Bold().Direction = Direction.RightToLeft;
                doc.InsertParagraph("نرم‌افزار ما به صورت هوشمند انتهای اطلاعات را تشخیص می‌دهد. لطفاً به هیچ‌وجه سلول‌ها را تبدیل به Table نکنید. این کار باعث می‌شود سیستم ردیف‌های خالیِ پایین جدول را نیز بخواند و دچار خطای پردازش شود. (اگر جدول ساخته‌اید، آن را Convert to Range کنید و ردیف‌های خالی را Delete کنید).")
                   .Font(font).Direction = Direction.RightToLeft;

                doc.InsertParagraph("❌ نباید ۲: تغییر نام هدرها ممنوع!")
                   .Font(font).Bold().Direction = Direction.RightToLeft;
                doc.InsertParagraph("نام ستون‌ها (ردیف اول) کلید اصلی هوش مصنوعیِ ما برای نگاشت خودکار است. کلمات را تغییر ندهید.")
                   .Font(font).Direction = Direction.RightToLeft;

                doc.InsertParagraph("❌ نباید ۳: استفاده از متن در ستون‌های عددی و مالی")
                   .Font(font).Bold().Direction = Direction.RightToLeft;
                doc.InsertParagraph("در ستون‌هایی مثل مبالغ، قیمت‌ها، بدهکار/بستانکار، سرفصل‌ها و کدها فقط و فقط از اعداد استفاده کنید. تایپ کلماتی مانند «تومان»، «ریال»، «خرید» یا استفاده از حروف در بین اعداد، موجب توقف عملیات و ثبت خطا می‌شود.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("").SpacingAfter(10);

                doc.InsertParagraph("📋 لیست ستون‌های مورد نیاز در این فایل:")
                   .Font(font).FontSize(14).Bold().Direction = Direction.RightToLeft;

                var table = doc.AddTable(mappings.Count + 1, 2);
                table.Design = TableDesign.LightGridAccent1;
                table.Alignment = Alignment.center;
                table.Rows[0].Cells[0].Paragraphs[0].Append("نام ستون در اکسل").Font(font).Bold().Direction = Direction.RightToLeft;
                table.Rows[0].Cells[1].Paragraphs[0].Append("وضعیت").Font(font).Bold().Direction = Direction.RightToLeft;

                for (int i = 0; i < mappings.Count; i++)
                {
                    table.Rows[i + 1].Cells[0].Paragraphs[0].Append(mappings[i].DisplayName).Font(font).Direction = Direction.RightToLeft;
                    string status = mappings[i].IsMandatory ? "اجباری (فیروزه‌ای)" : "اختیاری (سبز)";
                    table.Rows[i + 1].Cells[1].Paragraphs[0].Append(status).Font(font).Direction = Direction.RightToLeft;
                }
                doc.InsertTable(table);

                doc.Save();
            }
        }

        // دیکشنری اطلاعات هدرها دقیقاً مطابق با تنظیمات نگاشت سیستم
        private List<ColumnMappingItem> GetDefaultMappings(string type)
        {
            var list = new List<ColumnMappingItem>();
            if (type == "Kala")
            {
                list.Add(new ColumnMappingItem { DisplayName = "کد انبار", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "کد کالا", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "نام کالا", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "واحد اصلی کالا", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "نام انبار", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "واحد فرعی کالا", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "نرخ تبدیل واحد", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "گروه کالا", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "قیمت فروش", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "قیمت مصرف", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "قیمت آخرین خرید", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "موجودی انبار", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "بارکد", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "درصد مالیات کالا", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "حد تجدید سفارش", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "شناسه کالا مودی", IsMandatory = false });
            }
            else if (type == "Person")
            {
                list.Add(new ColumnMappingItem { DisplayName = "ردیف", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "شناسه", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "نام", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "سرفصل", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "معین", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "تفضیلی", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "بدهکار", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "بستانکار", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "تاریخ ثبت", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "شرح", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "نام خانوادگی", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "نام پدر", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "تاریخ تولد", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "موبایل", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "تلفن", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "آدرس", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "شعبه", IsMandatory = false });
            }
            else if (type == "Check")
            {
                list.Add(new ColumnMappingItem { DisplayName = "نوع چک", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "ردیف چک", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "سریال چک", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "سرفصل پس از وصول", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "معین پس از وصول", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "حساب پس از وصول", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "سرفصل چک", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "معین چک", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "حساب چک", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "تاریخ سررسید", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "وضعیت چک", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "مبلغ", IsMandatory = true });
                list.Add(new ColumnMappingItem { DisplayName = "بانک", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "شعبه", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "دریافت کننده", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "تاریخ صدور", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "کد شعبه", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "شماره حساب", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "شماره صیاد", IsMandatory = false });
                list.Add(new ColumnMappingItem { DisplayName = "سری چک", IsMandatory = false });
            }
            return list;
        }
    }
}