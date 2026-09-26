namespace Senik.DataTransformer.Validation
{
    public class ValidationError
    {
        public string SourceType { get; set; } = string.Empty; // ✨ مشخص‌کننده منبع خطا (Kala یا Person)
        public int RowIndex { get; set; }
        public string ExcelColumnName { get; set; } = string.Empty;
        public string TargetFieldName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsFatal { get; set; }
        public string InvalidValue { get; set; } = string.Empty;
    }
}