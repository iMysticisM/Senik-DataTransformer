using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExcelDataReader;
using Senik.DataTransformer.Models;
using Senik.DataTransformer.Validation;

namespace Senik.DataTransformer.Services
{
    public class ValidationService
    {
        public ValidationService()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public ValidationReport ValidateExcelData(string filePath, string sheetName, IEnumerable<ColumnMappingItem> mappings)
        {
            var report = new ValidationReport();
            var activeMappings = mappings.Where(m => !string.IsNullOrWhiteSpace(m.SelectedExcelColumn)).ToList();
            var compositeKeysInMemory = new HashSet<string>();
            var barcodesInMemory = new HashSet<string>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                bool sheetFound = false;
                do { if (reader.Name == sheetName) { sheetFound = true; break; } } while (reader.NextResult());

                if (!sheetFound) { report.Errors.Add(new ValidationError { IsFatal = true, ErrorMessage = "شیت کالا در فایل یافت نشد!" }); return report; }

                var headerIndices = new Dictionary<string, int>();
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetValue(i)?.ToString()?.Trim() ?? $"Column{i}";
                        if (!headerIndices.ContainsKey(colName)) headerIndices.Add(colName, i);
                    }
                }

                int currentRow = 2;
                while (reader.Read())
                {
                    bool rowHasError = false;
                    report.TotalRowsScanned++;
                    string currentIdAnbar = "";
                    string currentIdKala = "";

                    foreach (var map in activeMappings)
                    {
                        if (map.SelectedExcelColumn == null || !headerIndices.TryGetValue(map.SelectedExcelColumn, out int colIndex)) continue;

                        string cellValue = reader.GetValue(colIndex)?.ToString()?.Trim() ?? string.Empty;
                        string target = map.DisplayName.Trim();

                        if (map.IsMandatory && string.IsNullOrEmpty(cellValue))
                        {
                            report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = map.SelectedExcelColumn, TargetFieldName = map.TargetField, ErrorMessage = $"مقدار فیلد اجباری «{map.DisplayName}» نمی‌تواند خالی باشد.", IsFatal = true });
                            rowHasError = true;
                        }

                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            if (target == "کد کالا" || target == "کد انبار")
                            {
                                if (!cellValue.All(char.IsDigit))
                                {
                                    report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = map.SelectedExcelColumn, TargetFieldName = map.TargetField, InvalidValue = cellValue, ErrorMessage = $"فیلدهای کلیدی باید فقط شامل اعداد باشند.", IsFatal = true });
                                    rowHasError = true;
                                }
                            }

                            if (target.Contains("قیمت") || target.Contains("موجودی") || target.Contains("تبدیل") || target.Contains("تجدید") || target.Contains("مالیات"))
                            {
                                string cleanValue = cellValue.Replace("/", ".").Replace(",", "");
                                if (!decimal.TryParse(cleanValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                                {
                                    report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = map.SelectedExcelColumn, TargetFieldName = map.TargetField, InvalidValue = cellValue, ErrorMessage = $"مقدار «{cellValue}» برای فیلد ({target}) عدد معتبری نیست.", IsFatal = true });
                                    rowHasError = true;
                                }
                            }

                            if (target == "کد انبار") currentIdAnbar = cellValue;
                            if (target == "کد کالا") currentIdKala = cellValue;
                            // ستون اختیاری بارکد: در صورت خالی یا صفر بودن، بررسی تکراری بودن نادیده گرفته می‌شود
                            if (target == "بارکد" && cellValue != "0" && !barcodesInMemory.Add(cellValue))
                            {
                                report.Errors.Add(new ValidationError { RowIndex = currentRow, TargetFieldName = map.TargetField, ErrorMessage = $"بارکد «{cellValue}» تکراری است.", IsFatal = true });
                                rowHasError = true;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(currentIdAnbar) && !string.IsNullOrEmpty(currentIdKala))
                    {
                        if (!compositeKeysInMemory.Add($"{currentIdAnbar}_{currentIdKala}"))
                        {
                            report.Errors.Add(new ValidationError { RowIndex = currentRow, ErrorMessage = $"کالا «{currentIdKala}» برای انبار «{currentIdAnbar}» تکراری است.", IsFatal = true });
                            rowHasError = true;
                        }
                    }

                    if (rowHasError) report.ErrorRowsCount++;
                    else report.ValidRowsCount++;
                    currentRow++;
                }
            }
            return report;
        }

        public ValidationReport ValidatePersonExcelData(string filePath, string sheetName, IEnumerable<ColumnMappingItem> mappings)
        {
            var report = new ValidationReport();
            var activeMappings = mappings.Where(m => !string.IsNullOrWhiteSpace(m.SelectedExcelColumn)).ToList();
            var sodorSet = new HashSet<string>();
            var shOzviatSet = new HashSet<string>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                bool sheetFound = false;
                do { if (reader.Name == sheetName) { sheetFound = true; break; } } while (reader.NextResult());

                if (!sheetFound) { report.Errors.Add(new ValidationError { IsFatal = true, ErrorMessage = "شیت اشخاص یافت نشد!" }); return report; }

                var headerIndices = new Dictionary<string, int>();
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string rawCol = reader.GetValue(i)?.ToString()?.Trim() ?? $"Column{i}";
                        string colName = rawCol.Replace("ي", "ی").Replace("ك", "ک");
                        if (!headerIndices.ContainsKey(colName)) headerIndices.Add(colName, i);
                    }
                }

                int currentRow = 2;
                while (reader.Read())
                {
                    bool rowHasError = false;
                    report.TotalRowsScanned++;

                    // 1. بررسی قاعده انحصاری (XOR) بدهکار و بستانکار
                    long bedValue = 0;
                    long besValue = 0;
                    bool bedValid = true;
                    bool besValid = true;

                    var bedMap = activeMappings.FirstOrDefault(m => m.DisplayName.Trim() == "بدهکار");
                    if (bedMap != null && bedMap.SelectedExcelColumn != null)
                    {
                        string cleanCol = bedMap.SelectedExcelColumn.Trim().Replace("ي", "ی").Replace("ك", "ک");
                        if (headerIndices.TryGetValue(cleanCol, out int bedCol))
                        {
                            string bedStr = reader.GetValue(bedCol)?.ToString()?.Trim() ?? string.Empty;
                            if (!string.IsNullOrEmpty(bedStr))
                            {
                                string clean = bedStr.Replace("/", "").Replace(",", "").Trim();
                                if (long.TryParse(clean, out long b)) bedValue = b;
                                else
                                {
                                    bedValid = false;
                                    report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = bedMap.SelectedExcelColumn, TargetFieldName = bedMap.TargetField, InvalidValue = bedStr, ErrorMessage = $"مقدار «{bedStr}» برای (بدهکار) نامعتبر است.", IsFatal = true });
                                    rowHasError = true;
                                }
                            }
                        }
                    }

                    var besMap = activeMappings.FirstOrDefault(m => m.DisplayName.Trim() == "بستانکار");
                    if (besMap != null && besMap.SelectedExcelColumn != null)
                    {
                        string cleanCol = besMap.SelectedExcelColumn.Trim().Replace("ي", "ی").Replace("ك", "ک");
                        if (headerIndices.TryGetValue(cleanCol, out int besCol))
                        {
                            string besStr = reader.GetValue(besCol)?.ToString()?.Trim() ?? string.Empty;
                            if (!string.IsNullOrEmpty(besStr))
                            {
                                string clean = besStr.Replace("/", "").Replace(",", "").Trim();
                                if (long.TryParse(clean, out long b)) besValue = b;
                                else
                                {
                                    besValid = false;
                                    report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = besMap.SelectedExcelColumn, TargetFieldName = besMap.TargetField, InvalidValue = besStr, ErrorMessage = $"مقدار «{besStr}» برای (بستانکار) نامعتبر است.", IsFatal = true });
                                    rowHasError = true;
                                }
                            }
                        }
                    }

                    if (bedValid && besValid)
                    {
                        // هر دو نمی‌توانند همزمان بزرگتر از صفر باشند
                        if (bedValue > 0 && besValue > 0)
                        {
                            report.Errors.Add(new ValidationError
                            {
                                RowIndex = currentRow,
                                ErrorMessage = $"ستون‌های بدهکار ({bedValue:N0}) و بستانکار ({besValue:N0}) نمی‌توانند همزمان بزرگتر از صفر باشند.",
                                IsFatal = true
                            });
                            rowHasError = true;
                        }
                        // اگر هر دو صفر یا خالی باشند کاملاً معتبر است و خطایی ثبت نمی‌شود
                    }

                    // 2. بررسی سایر فیلدهای اشخاص
                    foreach (var map in activeMappings)
                    {
                        string target = map.DisplayName.Trim();
                        // بدهکار و بستانکار قبلاً به صورت جامع اعتبارسنجی شدند
                        if (target == "بدهکار" || target == "بستانکار") continue;

                        string cleanCol = map.SelectedExcelColumn?.Trim().Replace("ي", "ی").Replace("ك", "ک") ?? "";
                        if (string.IsNullOrEmpty(cleanCol) || !headerIndices.TryGetValue(cleanCol, out int colIndex)) continue;

                        string cellValue = reader.GetValue(colIndex)?.ToString()?.Trim() ?? string.Empty;

                        if (map.IsMandatory && string.IsNullOrEmpty(cellValue))
                        {
                            report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = map.SelectedExcelColumn ?? string.Empty, TargetFieldName = map.TargetField, ErrorMessage = $"فیلد اجباری «{map.DisplayName}» خالی است.", IsFatal = true });
                            rowHasError = true;
                        }

                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            if (target == "ردیف" || target == "شناسه" || target == "سرفصل" || target == "معین" ||
                                target == "تفضیلی" || target == "شعبه")
                            {
                                string cleanValue = cellValue.Replace("/", "").Replace(",", "").Trim();
                                if (!long.TryParse(cleanValue, out _))
                                {
                                    report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = map.SelectedExcelColumn ?? string.Empty, TargetFieldName = map.TargetField, InvalidValue = cellValue, ErrorMessage = $"مقدار «{cellValue}» برای ({target}) نامعتبر است.", IsFatal = true });
                                    rowHasError = true;
                                }
                            }

                            if (target == "شناسه" && !shOzviatSet.Add(cellValue)) { report.Errors.Add(new ValidationError { RowIndex = currentRow, ErrorMessage = $"شناسه «{cellValue}» تکراری است.", IsFatal = true }); rowHasError = true; }
                            if (target == "ردیف" && !sodorSet.Add(cellValue)) { report.Errors.Add(new ValidationError { RowIndex = currentRow, ErrorMessage = $"ردیف «{cellValue}» تکراری است.", IsFatal = true }); rowHasError = true; }
                        }
                    }

                    if (rowHasError) report.ErrorRowsCount++;
                    else report.ValidRowsCount++;
                    currentRow++;
                }
            }
            return report;
        }

        // ✨ اعتبارسنجی تب چک و اسناد مالی
        public ValidationReport ValidateCheckExcelData(string filePath, string sheetName, IEnumerable<ColumnMappingItem> mappings)
        {
            var report = new ValidationReport();
            var activeMappings = mappings.Where(m => !string.IsNullOrWhiteSpace(m.SelectedExcelColumn)).ToList();
            var checkIdSet = new HashSet<string>();
            var checkSerialSet = new HashSet<string>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                bool sheetFound = false;
                do { if (reader.Name == sheetName) { sheetFound = true; break; } } while (reader.NextResult());

                if (!sheetFound) { report.Errors.Add(new ValidationError { IsFatal = true, ErrorMessage = "شیت چک در فایل یافت نشد!" }); return report; }

                var headerIndices = new Dictionary<string, int>();
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetValue(i)?.ToString()?.Trim() ?? $"Column{i}";
                        if (!headerIndices.ContainsKey(colName)) headerIndices.Add(colName, i);
                    }
                }

                int currentRow = 2;
                while (reader.Read())
                {
                    bool rowHasError = false;
                    report.TotalRowsScanned++;

                    foreach (var map in activeMappings)
                    {
                        if (map.SelectedExcelColumn == null || !headerIndices.TryGetValue(map.SelectedExcelColumn, out int colIndex)) continue;

                        string cellValue = reader.GetValue(colIndex)?.ToString()?.Trim() ?? string.Empty;
                        string target = map.DisplayName.Trim();

                        if (map.IsMandatory && string.IsNullOrEmpty(cellValue))
                        {
                            report.Errors.Add(new ValidationError { RowIndex = currentRow, ExcelColumnName = map.SelectedExcelColumn, TargetFieldName = map.TargetField, ErrorMessage = $"فیلد اجباری «{map.DisplayName}» خالی است.", IsFatal = true });
                            rowHasError = true;
                        }

                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            if (target == "نوع چک" && cellValue != "دریافتی" && cellValue != "پرداختی")
                            {
                                report.Errors.Add(new ValidationError { RowIndex = currentRow, ErrorMessage = $"ستون نوع چک باید فقط «دریافتی» یا «پرداختی» باشد.", IsFatal = true });
                                rowHasError = true;
                            }

                            if (target == "ردیف چک" || target == "سرفصل پس از وصول" || target == "معین پس از وصول" || target == "حساب پس از وصول" ||
                                target == "سرفصل چک" || target == "معین چک" || target == "حساب چک" || target == "مبلغ")
                            {
                                string cleanValue = cellValue.Replace("/", "").Replace(",", "").Trim();
                                if (!long.TryParse(cleanValue, out _))
                                {
                                    report.Errors.Add(new ValidationError { RowIndex = currentRow, ErrorMessage = $"مقدار «{cellValue}» برای ({target}) باید عدد صحیح باشد.", IsFatal = true });
                                    rowHasError = true;
                                }
                            }

                            if (target == "ردیف چک" && !checkIdSet.Add(cellValue)) { report.Errors.Add(new ValidationError { RowIndex = currentRow, ErrorMessage = $"ردیف چک «{cellValue}» تکراری است.", IsFatal = true }); rowHasError = true; }
                            if (target == "سریال چک" && !checkSerialSet.Add(cellValue)) { report.Errors.Add(new ValidationError { RowIndex = currentRow, ErrorMessage = $"سریال چک «{cellValue}» تکراری است.", IsFatal = true }); rowHasError = true; }
                        }
                    }

                    if (rowHasError) report.ErrorRowsCount++;
                    else report.ValidRowsCount++;
                    currentRow++;
                }
            }
            return report;
        }
    }
}