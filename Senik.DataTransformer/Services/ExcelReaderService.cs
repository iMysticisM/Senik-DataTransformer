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