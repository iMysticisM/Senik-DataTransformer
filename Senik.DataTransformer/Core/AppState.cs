namespace Senik.DataTransformer.Core
{
    public static class AppState
    {
        public static string SqlConnectionString { get; set; } = string.Empty;

        // ✨ اضافه شدن قفل اجرای عملیات
        public static bool IsExecuting { get; set; } = false;

        // ✨ نگهداری آخرین تب فعال جهت بازگشت هوشمند به همان تب در عملیات جدید
        public static string LastActiveTab { get; set; } = "Kala";
    }
}