using System.Collections.Generic;
using System.Linq;

namespace Senik.DataTransformer.Validation
{
    public class ValidationReport
    {
        public int TotalRowsScanned { get; set; } = 0; // کل رکوردهای خوانده شده
        public int ValidRowsCount { get; set; } = 0; // ردیف‌های کاملاً سالم
        public int ErrorRowsCount { get; set; } = 0; // ردیف‌های دارای حداقل یک خطا

        // لیست تمام خطاهای پیدا شده در کل فایل
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

        // آیا اجازه ورود به مرحله درج SQL را داریم؟ (فقط وقتی که خطای Fatal صفر باشد)
        public bool CanExecute => TotalRowsScanned > 0 && !Errors.Any(e => e.IsFatal);
    }
}