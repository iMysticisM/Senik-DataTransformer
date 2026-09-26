namespace Senik.DataTransformer.Core
{
    public static class AppState
    {
        public static string SqlConnectionString { get; set; } = string.Empty;

        // ✨ اضافه شدن قفل اجرای عملیات
        public static bool IsExecuting { get; set; } = false;
    }
}