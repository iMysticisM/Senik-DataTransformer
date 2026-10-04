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
    public class TemplateColumnMeta
    {
        public string DisplayName { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
        public string DataType { get; set; } = string.Empty;
        public string EmptyRule { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public object SampleValue { get; set; } = string.Empty;
    }

    public class TemplateGeneratorService
    {
        public void GenerateTemplateAndGuide(string excelSavePath, string importType)
        {
            // ۱. دریافت متادیتای ستون‌ها مطابق آخرین تغییرات موتور
            var columns = GetColumnsMetadata(importType);

            // مرتب‌سازی: اول اجباری‌ها، سپس اختیاری‌ها
            var sortedColumns = columns.OrderByDescending(c => c.IsMandatory).ToList();

            // ۲. تولید فایل اکسل با نمونه داده واقعی و رنگ‌بندی استاندارد
            GenerateExcel(excelSavePath, sortedColumns);

            // ۳. تولید فایل Word راهنما و پرامپت آماده برای هوش مصنوعی در همان مسیر
            string directory = Path.GetDirectoryName(excelSavePath) ?? "";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(excelSavePath);
            string wordSavePath = Path.Combine(directory, $"{fileNameWithoutExt}_Guide.docx");

            GenerateWordGuide(wordSavePath, sortedColumns, importType);
        }

        private void GenerateExcel(string path, List<TemplateColumnMeta> columns)
        {
            using (var wb = new XLWorkbook())
            {
                wb.RightToLeft = true;
                wb.Style.Font.FontName = "Vazir FD";
                wb.Style.Font.FontSize = 11;

                var ws = wb.AddWorksheet("فایل نمونه استاندارد");
                ws.RightToLeft = true;

                // ردیف ۱: هدرها با رنگ‌بندی متمایز
                for (int i = 0; i < columns.Count; i++)
                {
                    var col = columns[i];
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = col.DisplayName;
                    cell.Style.Font.Bold = true;

                    if (col.IsMandatory)
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#6DD9FF"); // آبی فیروزه‌ای
                        cell.Style.Font.FontColor = XLColor.DarkSlateGray;
                    }
                    else
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#93FFC4"); // سبز روشن
                        cell.Style.Font.FontColor = XLColor.DarkOliveGreen;
                    }

                    // ردیف ۲: ردیف نمونه داده واقعی
                    var sampleCell = ws.Cell(2, i + 1);
                    sampleCell.Value = XLCellValue.FromObject(col.SampleValue);
                    sampleCell.Style.Font.FontName = "Vazir FD";
                    sampleCell.Style.Font.FontSize = 10;
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
        }

        private void GenerateWordGuide(string path, List<TemplateColumnMeta> columns, string importType)
        {
            string faTitle = importType == "Kala" ? "کالا و انبار" : importType == "Person" ? "اشخاص و طرف‌حساب‌ها" : "چک و اسناد مالی";

            using (var doc = DocX.Create(path))
            {
                var font = new Xceed.Document.NET.Font("Vazir FD");

                // عنوان اصلی
                doc.InsertParagraph($"راهنمای جامع آماده‌سازی فایل و پرامپت هوش مصنوعی ({faTitle})")
                   .Font(font).FontSize(17).Bold().Alignment = Alignment.center;
                doc.InsertParagraph("سیستم تبدیل و بارگذاری اطلاعات نرم‌افزار حسابداری سه نیک").Font(font).FontSize(12).Alignment = Alignment.center;
                doc.InsertParagraph("__________________________________________________________________").Alignment = Alignment.center;
                doc.InsertParagraph("").SpacingAfter(10);

                // بخش ۱: پرامپت آماده هوش مصنوعی برای کارشناس پشتیبانی
                doc.InsertParagraph("🤖 دستورالعمل و پرامپت آماده برای هوش مصنوعی (ChatGPT / Claude / Gemini):")
                   .Font(font).FontSize(14).Bold().Direction = Direction.RightToLeft;

                doc.InsertParagraph("پشتیبان محترم، در صورتی که فایل ارسالی مشتری ساختار متفاوتی دارد یا نامنظم است، می‌توانید متن داخل کادر زیر را به همراه فایل مشتری و جدول مشخصات این سند، به چت‌بات هوش مصنوعی ارسال کنید تا فایل نهایی استاندارد را تولید کند:")
                   .Font(font).Direction = Direction.RightToLeft;

                // کادر پرامپت
                string promptText =
                    "«شما یک دستیار ارشد تبدیل داده‌های مالی و حسابداری برای نرم‌افزار «سه نیک» هستید.\n" +
                    $"وظیفه شما تمیزکاری، نگاشت و تبدیل فایل اکسل ورودی مشتری به فرمت اکسل استاندارد سه نیک برای بخش [{faTitle}] است.\n\n" +
                    "دستورالعمل‌های الزامی و قواعد تجاری:\n" +
                    "۱. ساختار ستون‌های خروجی باید دقیقاً و بدون تغییر مطابق با نام ستون‌های مندرج در جدول مشخصات زیر باشد.\n" +
                    "۲. ترتیب ستون‌ها: ابتدا کلیه ستون‌های اجباری و سپس ستون‌های اختیاری قرار گیرند.\n" +
                    "۳. رفتار سلول‌های خالی: ستون‌هایی که در جدول زیر قید شده «حتماً خالی/Null بماند» را در صورت نبود داده خالی رها کنید و هرگز عدد صفر یا متن در آنها ننویسید (تا در نرم‌افزار با فیلدهای دیگر ترکیب نشود). ستون‌های عددی مالی اختیاری که قید شده «مقدار صفر درج شود» در صورت خالی بودن باید عدد 0 باشند.\n" +
                    "۴. مقادیر عددی: تمام ارقام باید فاقد کاما، ریال، تومان یا حروف باشند.\n" +
                    "۵. تاریخ‌ها: حتماً به صورت تاریخ هجری شمسی با فرمت YYYY/MM/DD (مانند 1403/01/15) درج شوند.\n" +
                    "۶. در اکسل خروجی از قابلیت «Format as Table» استفاده نکنید و داده‌ها در قالب Range معمولی باشند.»";

                var promptPara = doc.InsertParagraph(promptText).Font(font).FontSize(10);
                promptPara.Direction = Direction.RightToLeft;
                promptPara.SpacingAfter(12);

                // بخش ۲: معرفی فایل اکسل
                doc.InsertParagraph("🎯 ساختار بصری فایل اکسل نمونه:")
                   .Font(font).FontSize(13).Bold().Direction = Direction.RightToLeft;
                doc.InsertParagraph("🟦 ستون‌های فیروزه‌ای رنگ (اجباری): پر کردن این ستون‌ها کاملاً الزامی است و در صورت خالی بودن ردیف پذیرفته نمی‌شود.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("🟩 ستون‌های سبز رنگ (اختیاری): تکمیل آنها اختیاری است اما به بهبود جامعیت داده‌ها در پایگاه داده کمک می‌کند.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("💡 ردیف دوم فایل اکسل حاوی داده‌های نمونه استاندارد با فرمت صحیح است که می‌توانید پس از بررسی، آن را حذف یا بازنویسی کنید.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("").SpacingAfter(10);

                // بخش ۳: جدول جامع ۵ ستونه مشخصات
                doc.InsertParagraph("📋 جدول مشخصات فنی، نوع داده‌ها و رفتار مقادیر خالی:")
                   .Font(font).FontSize(13).Bold().Direction = Direction.RightToLeft;

                var table = doc.AddTable(columns.Count + 1, 5);
                table.Design = TableDesign.LightGridAccent1;
                table.Alignment = Alignment.center;

                // هدرهای جدول
                string[] headers = { "نام ستون در اکسل", "وضعیت", "نوع داده و فرمت", "رفتار در صورت خالی بودن", "توضیحات و قوانین تجاری" };
                for (int h = 0; h < headers.Length; h++)
                {
                    table.Rows[0].Cells[h].Paragraphs[0].Append(headers[h]).Font(font).Bold().Direction = Direction.RightToLeft;
                }

                // سطرهای جدول
                for (int i = 0; i < columns.Count; i++)
                {
                    var col = columns[i];
                    var row = table.Rows[i + 1];

                    row.Cells[0].Paragraphs[0].Append(col.DisplayName).Font(font).Bold().Direction = Direction.RightToLeft;
                    row.Cells[1].Paragraphs[0].Append(col.IsMandatory ? "اجباری (فیروزه‌ای)" : "اختیاری (سبز)").Font(font).Direction = Direction.RightToLeft;
                    row.Cells[2].Paragraphs[0].Append(col.DataType).Font(font).Direction = Direction.RightToLeft;
                    row.Cells[3].Paragraphs[0].Append(col.EmptyRule).Font(font).Direction = Direction.RightToLeft;
                    row.Cells[4].Paragraphs[0].Append(col.Notes).Font(font).Direction = Direction.RightToLeft;
                }

                doc.InsertTable(table);
                doc.InsertParagraph("").SpacingAfter(15);

                // بخش ۴: بایدها و نبایدهای مهم
                doc.InsertParagraph("⚠️ بایدها و نبایدهای حیاتی:")
                   .Font(font).FontSize(13).Bold().Direction = Direction.RightToLeft;

                doc.InsertParagraph("❌ نباید ۱: ساخت جدول (Format as Table) در اکسل ممنوع است. فقط سلول‌های عادی اکسل را پر کنید.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("❌ نباید ۲: تغییر نام هدرها ممنوع است؛ زیرا موتور هوشمند بر اساس این نام‌ها ستون‌ها را شناسایی می‌کند.")
                   .Font(font).Direction = Direction.RightToLeft;
                doc.InsertParagraph("❌ نباید ۳: در ستون‌های مالی و مقداری از کاراکترهای ریال، تومان، کاما یا متن استفاده نکنید؛ فقط عدد خالص.")
                   .Font(font).Direction = Direction.RightToLeft;

                doc.Save();
            }
        }

        private List<TemplateColumnMeta> GetColumnsMetadata(string type)
        {
            var list = new List<TemplateColumnMeta>();

            if (type == "Kala")
            {
                // اجباری‌ها
                list.Add(new TemplateColumnMeta { DisplayName = "کد انبار", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی (نباید خالی باشد)", Notes = "کد شناسایی انبار در سیستم (مانند 1)", SampleValue = 1 });
                list.Add(new TemplateColumnMeta { DisplayName = "کد کالا", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی (نباید خالی باشد)", Notes = "کد یکتای کالا در سیستم سه نیک", SampleValue = 101 });
                list.Add(new TemplateColumnMeta { DisplayName = "نام کالا", IsMandatory = true, DataType = "متنی", EmptyRule = "الزامی (نباید خالی باشد)", Notes = "نام و شرح کامل کالا", SampleValue = "روغن موتور ۴ لیتری" });
                list.Add(new TemplateColumnMeta { DisplayName = "واحد اصلی کالا", IsMandatory = true, DataType = "متنی", EmptyRule = "الزامی (نباید خالی باشد)", Notes = "واحد شمارش اصلی کالا (واحدهای رایج نظیر کیلوگرم، کارتن و... خودکار تطبیق یافته و واحدهای جدید مانند کیسه یا گالن خودکار ایجاد می‌شوند)", SampleValue = "عدد" });

                // اختیاری‌ها (نام انبار طبق تسک اختیاری است)
                list.Add(new TemplateColumnMeta { DisplayName = "نام انبار", IsMandatory = false, DataType = "متنی", EmptyRule = "خالی بماند (اختیاری است)", Notes = "نام انبار؛ در صورت خالی بودن بر اساس کد انبار پردازش می‌شود", SampleValue = "انبار مرکزی" });
                list.Add(new TemplateColumnMeta { DisplayName = "واحد فرعی کالا", IsMandatory = false, DataType = "متنی", EmptyRule = "خالی بماند", Notes = "واحد سنجش سطح دوم کالا", SampleValue = "کارتن" });
                list.Add(new TemplateColumnMeta { DisplayName = "نرخ تبدیل واحد", IsMandatory = false, DataType = "عددی", EmptyRule = "مقدار 0 درج شود", Notes = "تعداد واحد اصلی در هر واحد فرعی", SampleValue = 6 });
                list.Add(new TemplateColumnMeta { DisplayName = "گروه کالا", IsMandatory = false, DataType = "متنی", EmptyRule = "خالی بماند", Notes = "مسیر دسته‌بندی با اسلش (مثال: روغنیات/روغن موتور)", SampleValue = "روغنیات/روغن موتور" });
                list.Add(new TemplateColumnMeta { DisplayName = "قیمت فروش", IsMandatory = false, DataType = "عددی", EmptyRule = "مقدار 0 درج شود", Notes = "نرخ فروش نقدی به ریال بدون حروف و کاما", SampleValue = 4500000 });
                list.Add(new TemplateColumnMeta { DisplayName = "قیمت مصرف", IsMandatory = false, DataType = "عددی", EmptyRule = "مقدار 0 درج شود", Notes = "قیمت مصرف‌کننده روی کالا به ریال", SampleValue = 4800000 });
                list.Add(new TemplateColumnMeta { DisplayName = "قیمت آخرین خرید", IsMandatory = false, DataType = "عددی", EmptyRule = "مقدار 0 درج شود", Notes = "بهای آخرین خرید به ریال", SampleValue = 4000000 });
                list.Add(new TemplateColumnMeta { DisplayName = "موجودی انبار", IsMandatory = false, DataType = "عددی", EmptyRule = "مقدار 0 درج شود", Notes = "موجودی اولیه. در صورت 0 بودن، سند کاردکس صادر نمی‌شود تا خطای تریگر رخ ندهد", SampleValue = 120 });
                list.Add(new TemplateColumnMeta { DisplayName = "بارکد", IsMandatory = false, DataType = "متنی/عددی", EmptyRule = "خالی بماند", Notes = "بارکد خطی یا میله‌ای کالا", SampleValue = "6260123456789" });
                list.Add(new TemplateColumnMeta { DisplayName = "درصد مالیات کالا", IsMandatory = false, DataType = "عددی", EmptyRule = "مقدار 0 درج شود", Notes = "درصد مالیات بر ارزش افزوده (مثلاً 9 یا 10)", SampleValue = 9 });
                list.Add(new TemplateColumnMeta { DisplayName = "حد تجدید سفارش", IsMandatory = false, DataType = "عددی", EmptyRule = "مقدار 0 درج شود", Notes = "حداقل نقطه سفارش جهت هشدار کسری موجودی", SampleValue = 20 });
                list.Add(new TemplateColumnMeta { DisplayName = "شناسه کالا مودی", IsMandatory = false, DataType = "متنی/عددی", EmptyRule = "خالی بماند", Notes = "شناسه ۱۳ رقمی سامانه مودیان مالیاتی", SampleValue = "272000012345" });
            }
            else if (type == "Person")
            {
                // اجباری‌ها (شماره سند طبق رویکرد جدید موتور حذف شده و نیازی به آن در اکسل نیست)
                list.Add(new TemplateColumnMeta { DisplayName = "ردیف", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی (نباید خالی باشد)", Notes = "شماره ردیف ثبت سند افتتاحیه", SampleValue = 1 });
                list.Add(new TemplateColumnMeta { DisplayName = "شناسه", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی (غیرتکراری)", Notes = "کد اختصاصی یا کد عضویت شخص در سیستم", SampleValue = 1001 });
                list.Add(new TemplateColumnMeta { DisplayName = "نام", IsMandatory = true, DataType = "متنی", EmptyRule = "الزامی (نباید خالی باشد)", Notes = "نام کوچک یا عنوان شخص/شرکت", SampleValue = "علی" });
                list.Add(new TemplateColumnMeta { DisplayName = "سرفصل", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد کل حسابداری (مانند 103)", SampleValue = 103 });
                list.Add(new TemplateColumnMeta { DisplayName = "معین", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد حساب معین", SampleValue = 10301 });
                list.Add(new TemplateColumnMeta { DisplayName = "تفضیلی", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد حساب تفضیلی", SampleValue = 1001 });
                list.Add(new TemplateColumnMeta { DisplayName = "بدهکار", IsMandatory = true, DataType = "عددی", EmptyRule = "در صورت عدم مانده 0 درج شود", Notes = "مانده بدهکار اول دوره. قانون انحصار: اگر بدهکار > 0 باشد، بستانکار حتماً 0 است", SampleValue = 2500000 });
                list.Add(new TemplateColumnMeta { DisplayName = "بستانکار", IsMandatory = true, DataType = "عددی", EmptyRule = "در صورت عدم مانده 0 درج شود", Notes = "مانده بستانکار اول دوره. قانون انحصار: اگر بستانکار > 0 باشد، بدهکار حتماً 0 است", SampleValue = 0 });
                list.Add(new TemplateColumnMeta { DisplayName = "تاریخ ثبت", IsMandatory = true, DataType = "تاریخ شمسی (YYYY/MM/DD)", EmptyRule = "الزامی", Notes = "تاریخ ثبت سند افتتاحیه (مثال: 1403/01/15)", SampleValue = "1403/01/15" });
                list.Add(new TemplateColumnMeta { DisplayName = "شرح", IsMandatory = true, DataType = "متنی", EmptyRule = "الزامی", Notes = "شرح سند افتتاحیه", SampleValue = "سند افتتاحیه طرف‌حساب" });

                // اختیاری‌ها (فیلدهای هویتی در صورت خالی بودن باید حتماً Null بمانند و نباید صفر شوند)
                list.Add(new TemplateColumnMeta { DisplayName = "نام خانوادگی", IsMandatory = false, DataType = "متنی", EmptyRule = "حتماً خالی (Null) بماند", Notes = "نام خانوادگی شخص. اگر خالی است هرگز 0 نگذارید تا در نام فرد عدد صفر درج نشود", SampleValue = "محمدی" });
                list.Add(new TemplateColumnMeta { DisplayName = "نام پدر", IsMandatory = false, DataType = "متنی", EmptyRule = "حتماً خالی (Null) بماند", Notes = "نام پدر شخص", SampleValue = "رضا" });
                list.Add(new TemplateColumnMeta { DisplayName = "تاریخ تولد", IsMandatory = false, DataType = "تاریخ شمسی", EmptyRule = "حتماً خالی (Null) بماند", Notes = "تاریخ تولد شخص", SampleValue = "1365/04/10" });
                list.Add(new TemplateColumnMeta { DisplayName = "موبایل", IsMandatory = false, DataType = "متنی", EmptyRule = "حتماً خالی (Null) بماند", Notes = "شماره همراه (مثلاً 09121234567)", SampleValue = "09121234567" });
                list.Add(new TemplateColumnMeta { DisplayName = "تلفن", IsMandatory = false, DataType = "متنی", EmptyRule = "حتماً خالی (Null) بماند", Notes = "شماره تلفن ثابت با پیش‌شماره شهر", SampleValue = "02188776655" });
                list.Add(new TemplateColumnMeta { DisplayName = "آدرس", IsMandatory = false, DataType = "متنی", EmptyRule = "حتماً خالی (Null) بماند", Notes = "آدرس پستی محل سکونت یا محل کار", SampleValue = "تهران، خیابان آزادی، پلاک ۱۲" });
                list.Add(new TemplateColumnMeta { DisplayName = "شعبه", IsMandatory = false, DataType = "عددی صحیح", EmptyRule = "مقدار 0 درج شود", Notes = "کد شعبه حسابداری طرف‌حساب (پیش‌فرض 0)", SampleValue = 1 });
            }
            else if (type == "Check")
            {
                // اجباری‌ها
                list.Add(new TemplateColumnMeta { DisplayName = "نوع چک", IsMandatory = true, DataType = "متنی", EmptyRule = "الزامی", Notes = "فقط و فقط یکی از دو عبارت «دریافتی» یا «پرداختی» مجاز است", SampleValue = "دریافتی" });
                list.Add(new TemplateColumnMeta { DisplayName = "ردیف چک", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "شماره ردیف ثبت چک در سیستم", SampleValue = 1 });
                list.Add(new TemplateColumnMeta { DisplayName = "سریال چک", IsMandatory = true, DataType = "متنی/عددی", EmptyRule = "الزامی", Notes = "شماره سریال برگ چک بانکی", SampleValue = "987654" });
                list.Add(new TemplateColumnMeta { DisplayName = "سرفصل پس از وصول", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد کل حسابداری پس از وصول (مثلاً 101)", SampleValue = 101 });
                list.Add(new TemplateColumnMeta { DisplayName = "معین پس از وصول", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد حساب معین پس از وصول", SampleValue = 10101 });
                list.Add(new TemplateColumnMeta { DisplayName = "حساب پس از وصول", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد تفضیلی پس از وصول", SampleValue = 1 });
                list.Add(new TemplateColumnMeta { DisplayName = "سرفصل چک", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد کل اسناد دریافتنی/پرداختنی (مثلاً 104)", SampleValue = 104 });
                list.Add(new TemplateColumnMeta { DisplayName = "معین چک", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد معین اسناد دریافتنی/پرداختنی", SampleValue = 10401 });
                list.Add(new TemplateColumnMeta { DisplayName = "حساب چک", IsMandatory = true, DataType = "عددی صحیح", EmptyRule = "الزامی", Notes = "کد تفضیلی اسناد دریافتنی/پرداختنی", SampleValue = 2 });
                list.Add(new TemplateColumnMeta { DisplayName = "تاریخ سررسید", IsMandatory = true, DataType = "تاریخ شمسی (YYYY/MM/DD)", EmptyRule = "الزامی", Notes = "تاریخ سررسید چک (مثال: 1403/06/20)", SampleValue = "1403/06/20" });
                list.Add(new TemplateColumnMeta { DisplayName = "وضعیت چک", IsMandatory = true, DataType = "متنی", EmptyRule = "الزامی", Notes = "عنوان وضعیت تعریف‌شده در سیستم (مثال: اسناد دریافتنی نزد صندوق یا وصول شده)", SampleValue = "اسناد دریافتنی نزد صندوق" });
                list.Add(new TemplateColumnMeta { DisplayName = "مبلغ", IsMandatory = true, DataType = "عددی", EmptyRule = "الزامی (بزرگتر از صفر)", Notes = "مبلغ چک به ریال بدون کاما یا حروف", SampleValue = 150000000 });

                // اختیاری‌ها
                list.Add(new TemplateColumnMeta { DisplayName = "بانک", IsMandatory = false, DataType = "متنی", EmptyRule = "خالی بماند", Notes = "نام بانک باید دقیقاً مطابق نام‌های سیستم باشد (مثلاً: صادرات، پست بانک). کلمه بانک حذف نمی‌شود", SampleValue = "صادرات" });
                list.Add(new TemplateColumnMeta { DisplayName = "شعبه", IsMandatory = false, DataType = "متنی", EmptyRule = "خالی بماند", Notes = "نام شعبه بانک", SampleValue = "مرکزی" });
                list.Add(new TemplateColumnMeta { DisplayName = "دریافت کننده", IsMandatory = false, DataType = "متنی", EmptyRule = "خالی بماند", Notes = "شخص یا شرکت دریافت‌کننده وجه چک", SampleValue = "شرکت پخش سه نیک" });
                list.Add(new TemplateColumnMeta { DisplayName = "تاریخ صدور", IsMandatory = false, DataType = "تاریخ شمسی (YYYY/MM/DD)", EmptyRule = "خالی بماند", Notes = "تاریخ صدور چک (مثال: 1403/01/20)", SampleValue = "1403/01/20" });
                list.Add(new TemplateColumnMeta { DisplayName = "کد شعبه", IsMandatory = false, DataType = "متنی/عددی", EmptyRule = "خالی بماند", Notes = "کد شعبه بانک", SampleValue = "123" });
                list.Add(new TemplateColumnMeta { DisplayName = "شماره حساب", IsMandatory = false, DataType = "متنی/عددی", EmptyRule = "خالی بماند", Notes = "شماره حساب بانکی درج‌شده روی چک", SampleValue = "0102030405006" });
                list.Add(new TemplateColumnMeta { DisplayName = "شماره صیاد", IsMandatory = false, DataType = "متنی ۱۶ رقمی", EmptyRule = "خالی بماند", Notes = "شناسه ۱۶ رقمی چک صیادی", SampleValue = "1234567890123456" });
                list.Add(new TemplateColumnMeta { DisplayName = "سری چک", IsMandatory = false, DataType = "متنی", EmptyRule = "خالی بماند", Notes = "سری و حرف چک (مثال: الف/12)", SampleValue = "الف/12" });
            }

            return list;
        }
    }
}