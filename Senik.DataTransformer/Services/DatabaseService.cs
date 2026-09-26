using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Senik.DataTransformer.Services
{
    public class DatabaseService
    {
        // متد ساخت رشته اتصال (ConnectionString)
        public string BuildConnectionString(string server, bool isWindowsAuth, string username, string password, string database = "master")
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                IntegratedSecurity = isWindowsAuth,
                TrustServerCertificate = true, // برای دور زدن خطاهای SSL در SQL 2022+
                ConnectTimeout = 5 // حداکثر 10 ثانیه منتظر اتصال بماند
            };

            if (!isWindowsAuth)
            {
                builder.UserID = username;
                builder.Password = password;
            }

            return builder.ConnectionString;
        }

        // متد نفوذ به SQL و استخراج لیست دیتابیس‌های غیرسیستمی
        public List<string> GetDatabases(string connectionString)
        {
            var databases = new List<string>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open(); // اگر یوزر/پسورد غلط باشد همینجا Exception پرتاب می‌شود

                // کوئری حرفه‌ای برای استخراج دیتابیس‌های کاربر (حذف دیتابیس‌های سیستمی مثل master و tempdb)
                string query = "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name";

                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        databases.Add(reader.GetString(0));
                    }
                }
            }

            return databases;
        }
    }
}