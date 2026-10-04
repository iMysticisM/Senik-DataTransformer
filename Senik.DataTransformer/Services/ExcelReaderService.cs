using System.Collections.Generic;
using System.Data;
using System.IO;
using ExcelDataReader;

namespace Senik.DataTransformer.Services
{
    public class ExcelReaderService
    {
        public ExcelReaderService()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        // بررسی قفل بودن فایل یا باز بودن آن در نرم‌افزار اکسل یا سایر برنامه‌ها
        public static bool IsFileLocked(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException ex)
            {
                int errorCode = ex.HResult & 0xFFFF;
                if (errorCode == 32 || errorCode == 33 || ex.Message.Contains("used by another process"))
                    return true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // استخراج نام شیت‌ها بدون بارگذاری کل داده‌ها در RAM
        public List<string> GetSheetNames(string filePath)
        {
            var sheets = new List<string>();
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        sheets.Add(reader.Name);
                    } while (reader.NextResult());
                }
            }
            return sheets;
        }

        // خواندن سریع هدرهای سطر اول شیت جهت اعتبارسنجی اولیه
        public List<string> GetSheetHeaders(string filePath, string sheetName)
        {
            var headers = new List<string>();
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                bool found = false;
                do
                {
                    if (reader.Name == sheetName)
                    {
                        found = true;
                        break;
                    }
                } while (reader.NextResult());

                if (!found) return headers;

                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string rawName = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(rawName))
                        {
                            headers.Add(rawName.Replace("ي", "ی").Replace("ك", "ک"));
                        }
                    }
                }
            }
            return headers;
        }

        // اعتبارسنجی تطابق ستون‌های اجباری فایل با تب انتخاب شده
        public static bool ValidateTabHeaders(List<string> headers, string importType)
        {
            if (headers == null || headers.Count == 0) return false;

            var cleanHeaders = headers.Select(h => CleanHeader(h)).ToList();

            if (importType == "Kala")
            {
                // فیلدهای اصلی کالا: کد انبار، کد کالا، نام کالا، واحد اصلی کالا
                bool hasAnbar = cleanHeaders.Any(h => h.Contains("انبار"));
                bool hasKalaCode = cleanHeaders.Any(h => h == "کدکالا" || h == "کد" || h.Contains("کدکالا") || h == "شناسهکالا");
                bool hasKalaName = cleanHeaders.Any(h => h == "نامکالا" || h == "نام" || h.Contains("شرحکالا") || h.Contains("عنوانکالا") || h == "کالا");
                bool hasUnit = cleanHeaders.Any(h => h.Contains("واحد"));

                int matchCount = (hasAnbar ? 1 : 0) + (hasKalaCode ? 1 : 0) + (hasKalaName ? 1 : 0) + (hasUnit ? 1 : 0);
                return matchCount >= 3;
            }
            else if (importType == "Person")
            {
                // فیلدهای امضایی اشخاص و مالی: بدهکار، بستانکار، سرفصل، معین، تفضیلی، شناسه/ردیف
                bool hasBed = cleanHeaders.Any(h => h.Contains("بدهکار"));
                bool hasBes = cleanHeaders.Any(h => h.Contains("بستانکار"));
                bool hasSarfasl = cleanHeaders.Any(h => h.Contains("سرفصل"));
                bool hasMoin = cleanHeaders.Any(h => h.Contains("معین"));
                bool hasTafsili = cleanHeaders.Any(h => h.Contains("تفضیلی") || h.Contains("حساب"));
                bool hasPersonId = cleanHeaders.Any(h => h.Contains("شناسه") || h.Contains("عضویت") || h.Contains("کدشخص") || h == "ردیف");

                int matchCount = (hasBed ? 1 : 0) + (hasBes ? 1 : 0) + (hasSarfasl ? 1 : 0) + (hasMoin ? 1 : 0) + (hasTafsili ? 1 : 0) + (hasPersonId ? 1 : 0);
                return matchCount >= 4 && (hasBed || hasBes);
            }
            else if (importType == "Check")
            {
                // فیلدهای امضایی چک: نوع چک، سریال، سررسید، مبلغ، وضعیت
                bool hasCheck = cleanHeaders.Any(h => h.Contains("چک"));
                bool hasSerial = cleanHeaders.Any(h => h.Contains("سریال") || h.Contains("شمارهچک"));
                bool hasSarresid = cleanHeaders.Any(h => h.Contains("سررسید") || h.Contains("تاریخ"));
                bool hasMablagh = cleanHeaders.Any(h => h.Contains("مبلغ"));
                bool hasVaziat = cleanHeaders.Any(h => h.Contains("وضعیت"));

                int matchCount = (hasCheck ? 1 : 0) + (hasSerial ? 1 : 0) + (hasSarresid ? 1 : 0) + (hasMablagh ? 1 : 0) + (hasVaziat ? 1 : 0);
                return matchCount >= 3 && hasCheck;
            }

            return true;
        }

        private static string CleanHeader(string s) =>
            s?.Trim().Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Replace("_", "").Replace("-", "") ?? string.Empty;

        // استخراج فقط 200 سطر اول برای پیش‌نمایش سریع بدون اشغال RAM
        public DataTable GetSheetPreview(string filePath, string sheetName, int maxRows = 200)
        {
            var dt = new DataTable(sheetName);
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    // پیمایش تا رسیدن به شیت مورد نظر
                    bool found = false;
                    do
                    {
                        if (reader.Name == sheetName)
                        {
                            found = true;
                            break;
                        }
                    } while (reader.NextResult());

                    if (!found) return dt;

                    // خواندن سطر اول به عنوان Header با یکپارچه‌سازی و نرمال‌سازی حروف فارسی
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string rawName = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;
                            string colName = string.IsNullOrWhiteSpace(rawName)
                                ? $"Column{i}"
                                : rawName.Replace("ي", "ی").Replace("ك", "ک");

                            // جلوگیری از تکراری بودن نام ستون‌ها
                            string finalColName = colName;
                            int duplicateSuffix = 1;
                            while (dt.Columns.Contains(finalColName))
                            {
                                finalColName = $"{colName}_{duplicateSuffix++}";
                            }
                            dt.Columns.Add(finalColName);
                        }
                    }

                    // خواندن سطرها به صورت جریانی (Streaming) و محدود به maxRows
                    int rowCount = 0;
                    while (reader.Read() && rowCount < maxRows)
                    {
                        var row = dt.NewRow();
                        int fieldsToRead = System.Math.Min(dt.Columns.Count, reader.FieldCount);
                        for (int i = 0; i < fieldsToRead; i++)
                        {
                            row[i] = reader.GetValue(i);
                        }
                        dt.Rows.Add(row);
                        rowCount++;
                    }
                }
            }
            return dt;
        }
    }
}