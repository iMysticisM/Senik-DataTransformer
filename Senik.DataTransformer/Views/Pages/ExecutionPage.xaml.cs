using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Senik.DataTransformer.ViewModels;

namespace Senik.DataTransformer.Views.Pages
{
    public partial class ExecutionPage : Page
    {
        private DispatcherTimer? _logTimer;

        // رنگ‌بندی حرفه‌ای لاگ‌ها مطابق درخواست کاربر
        private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(74, 222, 128));   // #4ADE80 (سبز زمردی برای لاگ عادی)
        private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(248, 113, 113));     // #F87171 (قرمز برای خطاها)
        private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(250, 204, 21));   // #FACC15 (زرد درخشان برای تعداد موفقیت)
        private static readonly SolidColorBrush PinkBrush = new(Color.FromRgb(244, 114, 182));    // #F472B6 (صورتی برای تعداد شکست)
        private static readonly SolidColorBrush CyanBrush = new(Color.FromRgb(56, 189, 248));     // #38BDF8 (آبی روشن برای سیستم)
        private static readonly SolidColorBrush AmberBrush = new(Color.FromRgb(251, 191, 36));    // #FBBF24 (کهربایی برای هشدارها)
        private static readonly SolidColorBrush WhiteBrush = Brushes.White;

        static ExecutionPage()
        {
            // فریز کردن براش‌ها برای بالاترین کارایی و سرعت در WPF
            GreenBrush.Freeze();
            RedBrush.Freeze();
            YellowBrush.Freeze();
            PinkBrush.Freeze();
            CyanBrush.Freeze();
            AmberBrush.Freeze();
        }

        public ExecutionPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ExecutionViewModel vm)
            {
                // ایجاد تایمر رندرینگ هر 40 میلی‌ثانیه جهت آپدیت کاملاً در لحظه، روان و بدون فریز
                _logTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(40)
                };
                _logTimer.Tick += OnLogTimerTick;
                _logTimer.Start();

                vm.StartExecutionCommand.Execute(null);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _logTimer?.Stop();
        }

        private void OnLogTimerTick(object? sender, EventArgs e)
        {
            if (DataContext is not ExecutionViewModel vm) return;

            // 1. به‌روزرسانی زنده نوار پیشرفت و درصد در ترد UI
            if (!vm.IsCompleted)
            {
                int current = vm.GlobalProcessedRows;
                int total = vm.TotalRowsToProcess;
                int pct = total > 0 ? (int)((current / (double)total) * 100) : 0;
                if (pct > 100) pct = 100;
                vm.ProgressPercentage = pct;

                if (!string.IsNullOrEmpty(vm.CurrentStatusMessage) && vm.StatusMessage != vm.CurrentStatusMessage)
                {
                    vm.StatusMessage = vm.CurrentStatusMessage;
                }
            }

            // 2. تخلیه بافر لاگ‌ها به شکل دسته‌ای و الحاق به کنترل RichTextBox
            bool added = false;
            int count = 0;
            while (count++ < 100 && vm.LogQueue.TryDequeue(out var entry))
            {
                added = true;
                AppendLogEntry(entry);
            }

            if (added)
            {
                RtbLogs.ScrollToEnd();
            }

            // 3. بررسی اتمام فرایند و خاموش کردن تایمر
            if (vm.IsCompleted && vm.LogQueue.IsEmpty)
            {
                vm.ProgressPercentage = 100;
                _logTimer?.Stop();
            }
        }

        private void AppendLogEntry(LogEntry entry)
        {
            if (entry.IsSummary)
            {
                AppendSummaryBlock(entry);
                return;
            }

            string text = entry.Text;
            var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };

            SolidColorBrush brush = GreenBrush;
            if (text.StartsWith("❌"))
            {
                brush = RedBrush;
            }
            else if (text.StartsWith("⚠️"))
            {
                brush = AmberBrush;
            }
            else if (text.StartsWith("▶") || text.StartsWith("⏳") || text.Contains("[System]") || text.Contains("[Target]"))
            {
                brush = CyanBrush;
            }
            else if (text.StartsWith("✨") || text.StartsWith("ℹ️"))
            {
                brush = AmberBrush;
            }

            p.Inlines.Add(new Run(text) { Foreground = brush });
            RtbLogs.Document.Blocks.Add(p);
        }

        private void AppendSummaryBlock(LogEntry summary)
        {
            var pSep1 = new Paragraph(new Run("======================================================") { Foreground = GreenBrush }) 
            { 
                Margin = new Thickness(0, 12, 0, 2) 
            };
            RtbLogs.Document.Blocks.Add(pSep1);

            var pTitle = new Paragraph { Margin = new Thickness(0, 3, 0, 4) };
            if (summary.IsCancelled)
            {
                pTitle.Inlines.Add(new Run("⚠️ پردازش توسط کاربر متوقف گردید.") { Foreground = AmberBrush, FontWeight = FontWeights.Bold, FontSize = 14 });
            }
            else
            {
                pTitle.Inlines.Add(new Run("🎉 عملیات جامع با موفقیت به پایان رسید.") { Foreground = WhiteBrush, FontWeight = FontWeights.Bold, FontSize = 14 });
            }
            RtbLogs.Document.Blocks.Add(pTitle);

            // عدد تعداد موفقیت به رنگ زرد (Yellow)
            var pSuccess = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            pSuccess.Inlines.Add(new Run("✔️ مجموع رکوردهای موفق: ") { Foreground = GreenBrush, FontWeight = FontWeights.Bold });
            pSuccess.Inlines.Add(new Run($" {summary.SuccessCount} ") { Foreground = YellowBrush, FontWeight = FontWeights.ExtraBold, FontSize = 15 });
            pSuccess.Inlines.Add(new Run("رکورد") { Foreground = GreenBrush });
            RtbLogs.Document.Blocks.Add(pSuccess);

            // عدد تعداد شکست به رنگ صورتی (Pink)
            var pFail = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            pFail.Inlines.Add(new Run("❌ مجموع رکوردهای ناموفق (شکست): ") { Foreground = GreenBrush, FontWeight = FontWeights.Bold });
            pFail.Inlines.Add(new Run($" {summary.FailCount} ") { Foreground = PinkBrush, FontWeight = FontWeights.ExtraBold, FontSize = 15 });
            pFail.Inlines.Add(new Run("رکورد") { Foreground = GreenBrush });
            RtbLogs.Document.Blocks.Add(pFail);

            var pSep2 = new Paragraph(new Run("======================================================") { Foreground = GreenBrush }) 
            { 
                Margin = new Thickness(0, 2, 0, 6) 
            };
            RtbLogs.Document.Blocks.Add(pSep2);
        }
    }
}