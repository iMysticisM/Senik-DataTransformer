using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using ClosedXML.Excel;
using Xceed.Document.NET;
using Xceed.Words.NET;
using Senik.DataTransformer.Validation;
using Senik.DataTransformer.Core;

namespace Senik.DataTransformer.Services
{
    public class ErrorReportService
    {
        public void GenerateGlobalReports(NavigateToExecutionPageMessage payload)
        {
            if (payload.CombinedReport == null || payload.CombinedReport.Errors.Count == 0) return;

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportFolder = Path.Combine(desktopPath, "Senik_Reports", $"Validation_Errors_{timestamp}");

            if (!Directory.Exists(reportFolder)) Directory.CreateDirectory(reportFolder);

            // تولید اکسل کالا
            var kalaErrors = payload.CombinedReport.Errors.Where(e => e.SourceType == "Kala").ToList();
            if (payload.RunKala && kalaErrors.Count > 0)
                CreateExcelReport(payload.KalaFilePath, payload.KalaSheetName, Path.Combine(reportFolder, $"Fix_Kala_Data_{timestamp}.xlsx"), kalaErrors);

            // تولید اکسل اشخاص
            var personErrors = payload.CombinedReport.Errors.Where(e => e.SourceType == "Person").ToList();
            if (payload.RunPerson && personErrors.Count > 0)
                CreateExcelReport(payload.PersonFilePath, payload.PersonSheetName, Path.Combine(reportFolder, $"Fix_Person_Data_{timestamp}.xlsx"), personErrors);

            // ✨ تولید اکسل چک
            var checkErrors = payload.CombinedReport.Errors.Where(e => e.SourceType == "Check").ToList();
            if (payload.RunCheck && checkErrors.Count > 0)
                CreateExcelReport(payload.CheckFilePath, payload.CheckSheetName, Path.Combine(reportFolder, $"Fix_Check_Data_{timestamp}.xlsx"), checkErrors);

            // تولید ورد یکپارچه
            CreateWordReport(Path.Combine(reportFolder, $"Global_Report_{timestamp}.docx"), payload.CombinedReport);

            Process.Start(new ProcessStartInfo { FileName = reportFolder, UseShellExecute = true });
        }

        private void CreateExcelReport(string originalExcelPath, string sheetName, string outputPath, List<ValidationError> errors)
        {
            using (var stream = new FileStream(originalExcelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var originalWb = new XLWorkbook(stream))
            {
                var originalSheet = originalWb.Worksheet(sheetName);

                using (var newWb = new XLWorkbook())
                {
                    newWb.RightToLeft = true;
                    // تنظیم فونت اکسل
                    newWb.Style.Font.FontName = "Vazir FD";
                    newWb.Style.Font.FontSize = 16;

                    var newSheet = newWb.AddWorksheet("رکوردهای خطا");
                    newSheet.RightToLeft = true;

                    int colCount = originalSheet.LastColumnUsed()?.ColumnNumber() ?? 1;

                    for (int c = 1; c <= colCount; c++)
                    {
                        newSheet.Cell(1, c).Value = originalSheet.Cell(1, c).Value;
                        newSheet.Cell(1, c).Style.Font.Bold = true;
                        newSheet.Cell(1, c).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    int errorColIndex = colCount + 1;
                    newSheet.Cell(1, errorColIndex).Value = "جزئیات خطا (سیستم)";
                    newSheet.Cell(1, errorColIndex).Style.Font.Bold = true;
                    newSheet.Cell(1, errorColIndex).Style.Fill.BackgroundColor = XLColor.Yellow;

                    var errorsByRow = errors.GroupBy(e => e.RowIndex).OrderBy(g => g.Key).ToList();
                    int newRowIndex = 2;

                    foreach (var rowGroup in errorsByRow)
                    {
                        int originalRowIndex = rowGroup.Key;

                        for (int c = 1; c <= colCount; c++)
                            newSheet.Cell(newRowIndex, c).Value = originalSheet.Cell(originalRowIndex, c).Value;

                        List<string> rowErrorMessages = new List<string>();
                        foreach (var err in rowGroup)
                        {
                            rowErrorMessages.Add($"- {err.ErrorMessage}");

                            var colMatch = originalSheet.Row(1).Cells(1, colCount).FirstOrDefault(c => c.GetString().Trim() == err.ExcelColumnName);
                            if (colMatch != null)
                            {
                                int errColNum = colMatch.Address.ColumnNumber;
                                newSheet.Cell(newRowIndex, errColNum).Style.Fill.BackgroundColor = XLColor.LightPink;
                                newSheet.Cell(newRowIndex, errColNum).Style.Font.FontColor = XLColor.DarkRed;
                            }
                        }

                        newSheet.Cell(newRowIndex, errorColIndex).Value = string.Join("\n", rowErrorMessages);
                        newSheet.Cell(newRowIndex, errorColIndex).Style.Alignment.WrapText = true;
                        newRowIndex++;
                    }

                    newSheet.Columns().AdjustToContents();
                    newWb.SaveAs(outputPath);
                }
            }
        }

        private void CreateWordReport(string outputPath, ValidationReport report)
        {
            using (var doc = DocX.Create(outputPath))
            {
                var vazirFont = new Xceed.Document.NET.Font("Vazir FD");

                doc.InsertParagraph("گزارش جامع اعتبارسنجی سیستم سه نیک")
                   .Font(vazirFont).FontSize(18).Bold().Alignment = Alignment.center;
                doc.InsertParagraph($"تاریخ گزارش: {DateTime.Now.ToString("yyyy/MM/dd HH:mm")}")
                   .Font(vazirFont).Alignment = Alignment.center;
                doc.InsertParagraph("__________________________________________________").Alignment = Alignment.center;
                doc.InsertParagraph("").SpacingAfter(10);

                doc.InsertParagraph("خلاصه وضعیت بررسی فایل‌ها:")
                   .Font(vazirFont).FontSize(14).Bold().Direction = Direction.RightToLeft;
                doc.InsertParagraph($"• تعداد کل رکوردهای پردازش شده: {report.TotalRowsScanned} ردیف")
                   .Font(vazirFont).Direction = Direction.RightToLeft;
                doc.InsertParagraph($"• تعداد رکوردهای دارای خطا: {report.ErrorRowsCount} ردیف")
                   .Font(vazirFont).Direction = Direction.RightToLeft;

                doc.InsertParagraph("").SpacingAfter(10);
                doc.InsertParagraph("راهنمای رفع خطا:")
                   .Font(vazirFont).FontSize(14).Bold().Direction = Direction.RightToLeft;
                doc.InsertParagraph("لطفاً فایل‌های اکسل تولید شده در همین پوشه را باز کرده و سلول‌های قرمز رنگ را اصلاح فرمایید.\n")
                   .Font(vazirFont).Direction = Direction.RightToLeft;

                var errorsBySource = report.Errors.GroupBy(e => e.SourceType).ToList();

                foreach (var sourceGroup in errorsBySource)
                {
                    string sectionTitle = sourceGroup.Key == "Kala" ? "خطاهای مربوط به اکسل کالا و انبار" :
                                          sourceGroup.Key == "Person" ? "خطاهای مربوط به اکسل اشخاص و مالی" :
                                          "خطاهای مربوط به اکسل چک و اسناد مالی";

                    doc.InsertParagraph(sectionTitle).Font(vazirFont).FontSize(13).Bold().Direction = Direction.RightToLeft;

                    var table = doc.AddTable(sourceGroup.GroupBy(e => e.RowIndex).Count() + 1, 3);
                    table.Design = TableDesign.LightGridAccent1;
                    table.Alignment = Alignment.center;

                    table.Rows[0].Cells[0].Paragraphs[0].Append("ردیف در اکسل").Font(vazirFont).Bold().Direction = Direction.RightToLeft;
                    table.Rows[0].Cells[1].Paragraphs[0].Append("ستون معیوب").Font(vazirFont).Bold().Direction = Direction.RightToLeft;
                    table.Rows[0].Cells[2].Paragraphs[0].Append("شرح خطا").Font(vazirFont).Bold().Direction = Direction.RightToLeft;

                    int r = 1;
                    foreach (var group in sourceGroup.GroupBy(e => e.RowIndex).OrderBy(g => g.Key))
                    {
                        string cols = string.Join("\n", group.Select(x => x.ExcelColumnName).Distinct());
                        string msgs = string.Join("\n", group.Select(x => $"- {x.ErrorMessage}"));

                        table.Rows[r].Cells[0].Paragraphs[0].Append(group.Key.ToString()).Font(vazirFont).Direction = Direction.RightToLeft;
                        table.Rows[r].Cells[1].Paragraphs[0].Append(cols).Font(vazirFont).Direction = Direction.RightToLeft;
                        table.Rows[r].Cells[2].Paragraphs[0].Append(msgs).Font(vazirFont).Direction = Direction.RightToLeft;
                        r++;
                    }
                    doc.InsertTable(table);
                    doc.InsertParagraph("").SpacingAfter(15);
                }
                doc.Save();
            }
        }
    }
}