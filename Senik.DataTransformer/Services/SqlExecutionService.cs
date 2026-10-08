using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Data.SqlClient;
using ExcelDataReader;
using ClosedXML.Excel;
using Senik.DataTransformer.Core;
using Senik.DataTransformer.Models;
using Senik.DataTransformer.Validation;

namespace Senik.DataTransformer.Services
{
    public class SqlExecutionService
    {
        // ====================================================================
        // 📦 موتور پردازش کالا و انبار
        // ====================================================================
        public (int SuccessCount, int FailCount) ExecuteKalaImport(NavigateToExecutionPageMessage executionData, string connectionString, string reportFolder, Action stepProgress, Action<string> updateMessage, Action<string> addLog, CancellationToken cancellationToken)
        {
            var activeMappings = executionData.KalaMappings.Where(m => !string.IsNullOrWhiteSpace(m.SelectedExcelColumn)).ToList();
            int successCount = 0;
            var sqlErrorRows = new List<ValidationError>();

            using (var stream = File.Open(executionData.KalaFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                bool sheetFound = false;
                do { if (reader.Name == executionData.KalaSheetName) { sheetFound = true; break; } } while (reader.NextResult());
                if (!sheetFound) return (0, 0);

                var headerIndices = new Dictionary<string, int>();
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetValue(i)?.ToString()?.Trim().Replace("ي", "ی").Replace("ك", "ک") ?? $"Column{i}";
                        if (!headerIndices.ContainsKey(colName)) headerIndices.Add(colName, i);
                    }
                }

                using (var sqlConnection = new SqlConnection(connectionString))
                {
                    sqlConnection.Open();
                    addLog("⏳ [System - کالا] در حال بارگذاری اطلاعات پایه در حافظه رم...");
                    var unitCache = LoadUnitsIntoMemory(sqlConnection);
                    addLog("✔️ [System - کالا] موتور تراکنش‌ها استارت خورد...\n");

                    int rowIndex = 2;
                    while (reader.Read())
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        stepProgress();
                        updateMessage($"در حال پردازش کالا - ردیف در فایل: {rowIndex}");

                        var rowData = ExtractRowData(reader, activeMappings, headerIndices);
                        if (rowData.Count == 0)
                        {
                            rowIndex++;
                            continue;
                        }

                        rowData.TryGetValue("کد کالا", out string? currentIdKala);

                        int? unit1Id = null, unit2Id = null;
                        if (rowData.TryGetValue("واحد اصلی کالا", out string? unit1Name) && !string.IsNullOrWhiteSpace(unit1Name))
                            unit1Id = GetOrAddUnit(sqlConnection, unitCache, unit1Name, addLog);

                        if (rowData.TryGetValue("واحد فرعی کالا", out string? unit2Name) && !string.IsNullOrWhiteSpace(unit2Name))
                            unit2Id = GetOrAddUnit(sqlConnection, unitCache, unit2Name, addLog);

                        using (var transaction = sqlConnection.BeginTransaction())
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(currentIdKala))
                                    throw new Exception("مقدار «کد کالا» در این ردیف یافت نشد یا خالی است.");

                                if (!rowData.TryGetValue("کد انبار", out string? idAnbar) || string.IsNullOrWhiteSpace(idAnbar))
                                    throw new Exception("مقدار «کد انبار» در این ردیف یافت نشد یا خالی است.");

                                string? groupCode = ProcessGroupKala(sqlConnection, transaction, rowData);

                                ProcessTblAnbar(sqlConnection, transaction, idAnbar, rowData);
                                InsertTblKala(sqlConnection, transaction, rowData, currentIdKala, idAnbar, unit1Id, unit2Id, groupCode);
                                InsertTblPrice(sqlConnection, transaction, rowData, currentIdKala, idAnbar);
                                InsertTblTRAnbar(sqlConnection, transaction, rowData, currentIdKala, idAnbar);
                                InsertTblBarCode(sqlConnection, transaction, rowData, currentIdKala, idAnbar);

                                transaction.Commit();
                                successCount++;
                                addLog($"✔️ [کالا - ردیف {rowIndex}] کد «{currentIdKala}» (انبار {idAnbar}) با موفقیت ثبت شد.");
                            }
                            catch (SqlException ex)
                            {
                                transaction.Rollback();
                                string errorMsg = (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50001) ? "خطای کلید یکتا: این رکورد از قبل وجود دارد." : ex.Message;
                                addLog($"❌ [کالا - ردیف {rowIndex}] شکست (کد {currentIdKala ?? "?"}) - دلیل: {errorMsg}");
                                sqlErrorRows.Add(new ValidationError { RowIndex = rowIndex, ErrorMessage = errorMsg, ExcelColumnName = "SQL Engine (Kala)" });
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                addLog($"❌ [کالا - ردیف {rowIndex}] شکست (کد {currentIdKala ?? "?"}) - دلیل: {ex.Message}");
                                sqlErrorRows.Add(new ValidationError { RowIndex = rowIndex, ErrorMessage = ex.Message, ExcelColumnName = "SQL Engine (Kala)" });
                            }
                        }
                        rowIndex++;
                    }
                }
            }

            if (sqlErrorRows.Count > 0)
                GenerateSqlErrorExcel(executionData.KalaFilePath, executionData.KalaSheetName, sqlErrorRows, reportFolder, "Kala_SQL_Errors.xlsx", addLog);

            return (successCount, sqlErrorRows.Count);
        }

        // ====================================================================
        // 👤 موتور پردازش اشخاص و مالی
        // ====================================================================
        public (int SuccessCount, int FailCount) ExecutePersonImport(NavigateToExecutionPageMessage executionData, string connectionString, string reportFolder, Action stepProgress, Action<string> updateMessage, Action<string> addLog, CancellationToken cancellationToken)
        {
            var activeMappings = executionData.PersonMappings.Where(m => !string.IsNullOrWhiteSpace(m.SelectedExcelColumn)).ToList();
            int successCount = 0;
            var sqlErrorRows = new List<ValidationError>();

            using (var stream = File.Open(executionData.PersonFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                bool sheetFound = false;
                do { if (reader.Name == executionData.PersonSheetName) { sheetFound = true; break; } } while (reader.NextResult());
                if (!sheetFound) return (0, 0);

                var headerIndices = new Dictionary<string, int>();
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetValue(i)?.ToString()?.Trim().Replace("ي", "ی").Replace("ك", "ک") ?? $"Column{i}";
                        if (!headerIndices.ContainsKey(colName)) headerIndices.Add(colName, i);
                    }
                }

                using (var sqlConnection = new SqlConnection(connectionString))
                {
                    sqlConnection.Open();
                    addLog("✔️ [System - اشخاص] موتور تراکنش‌های مالی و اشخاص استارت خورد...\n");

                    int rowIndex = 2;
                    while (reader.Read())
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        stepProgress();
                        updateMessage($"در حال پردازش اشخاص - ردیف در فایل: {rowIndex}");

                        var rowData = ExtractRowData(reader, activeMappings, headerIndices);
                        if (rowData.Count == 0)
                        {
                            rowIndex++;
                            continue;
                        }

                        rowData.TryGetValue("شناسه", out string? currentId);

                        using (var transaction = sqlConnection.BeginTransaction())
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(currentId))
                                    throw new Exception("مقدار «شناسه» در این ردیف یافت نشد یا خالی است.");

                                InsertTblPerson(sqlConnection, transaction, rowData, currentId);

                                if (rowData.TryGetValue("تلفن", out string? tell))
                                {
                                    string cleanTell = CleanOptionalString(tell);
                                    if (!string.IsNullOrEmpty(cleanTell))
                                        InsertTblPersonTell(sqlConnection, transaction, currentId, cleanTell);
                                }

                                InsertMaster(sqlConnection, transaction, rowData, currentId);
                                InsertTR(sqlConnection, transaction, rowData);

                                transaction.Commit();
                                successCount++;
                                addLog($"✔️ [اشخاص - ردیف {rowIndex}] شخص با شناسه «{currentId}» در 4 جدول ثبت شد.");
                            }
                            catch (SqlException ex)
                            {
                                transaction.Rollback();
                                string errorMsg = (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50001) ? "خطای کلید یکتا: این شخص یا سند از قبل در سیستم وجود دارد." : ex.Message;
                                addLog($"❌ [اشخاص - ردیف {rowIndex}] شکست (شناسه {currentId ?? "?"}) - دلیل: {errorMsg}");
                                sqlErrorRows.Add(new ValidationError { RowIndex = rowIndex, ErrorMessage = errorMsg, ExcelColumnName = "SQL Engine (Person)" });
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                addLog($"❌ [اشخاص - ردیف {rowIndex}] شکست (شناسه {currentId ?? "?"}) - دلیل: {ex.Message}");
                                sqlErrorRows.Add(new ValidationError { RowIndex = rowIndex, ErrorMessage = ex.Message, ExcelColumnName = "SQL Engine (Person)" });
                            }
                        }
                        rowIndex++;
                    }
                }
            }

            if (sqlErrorRows.Count > 0)
                GenerateSqlErrorExcel(executionData.PersonFilePath, executionData.PersonSheetName, sqlErrorRows, reportFolder, "Person_SQL_Errors.xlsx", addLog);

            return (successCount, sqlErrorRows.Count);
        }

        // ====================================================================
        // 💳 موتور پردازش چک و اسناد مالی
        // ====================================================================
        public (int SuccessCount, int FailCount) ExecuteCheckImport(NavigateToExecutionPageMessage executionData, string connectionString, string reportFolder, Action stepProgress, Action<string> updateMessage, Action<string> addLog, CancellationToken cancellationToken)
        {
            var activeMappings = executionData.CheckMappings.Where(m => !string.IsNullOrWhiteSpace(m.SelectedExcelColumn)).ToList();
            int successCount = 0;
            var sqlErrorRows = new List<ValidationError>();

            using (var stream = File.Open(executionData.CheckFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                bool sheetFound = false;
                do { if (reader.Name == executionData.CheckSheetName) { sheetFound = true; break; } } while (reader.NextResult());
                if (!sheetFound) return (0, 0);

                var headerIndices = new Dictionary<string, int>();
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetValue(i)?.ToString()?.Trim().Replace("ي", "ی").Replace("ك", "ک") ?? $"Column{i}";
                        if (!headerIndices.ContainsKey(colName)) headerIndices.Add(colName, i);
                    }
                }

                using (var sqlConnection = new SqlConnection(connectionString))
                {
                    sqlConnection.Open();
                    addLog("⏳ [System - چک] در حال بارگذاری اطلاعات پایه بانک‌ها و وضعیت‌ها...");
                    var bankCache = LoadBanksIntoMemory(sqlConnection);
                    var stateCache = LoadCheckStatesIntoMemory(sqlConnection);
                    addLog("✔️ [System - چک] موتور تراکنش‌های اسناد مالی استارت خورد...\n");

                    int rowIndex = 2;
                    while (reader.Read())
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        stepProgress();
                        updateMessage($"در حال پردازش چک - ردیف در فایل: {rowIndex}");

                        var rowData = ExtractRowData(reader, activeMappings, headerIndices);
                        if (rowData.Count == 0)
                        {
                            rowIndex++;
                            continue;
                        }

                        rowData.TryGetValue("ردیف چک", out string? currentId);

                        using (var transaction = sqlConnection.BeginTransaction())
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(currentId))
                                    throw new Exception("مقدار «ردیف چک» در این رکورد خالی است.");

                                InsertCheck(sqlConnection, transaction, rowData, bankCache, stateCache);

                                transaction.Commit();
                                successCount++;
                                addLog($"✔️ [چک - ردیف {rowIndex}] چک با شناسه «{currentId}» با موفقیت ثبت شد.");
                            }
                            catch (SqlException ex)
                            {
                                transaction.Rollback();
                                string errorMsg = (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50001) ? "خطای کلید یکتا: این چک از قبل در سیستم وجود دارد." : ex.Message;
                                addLog($"❌ [چک - ردیف {rowIndex}] شکست (شناسه {currentId ?? "?"}) - دلیل: {errorMsg}");
                                sqlErrorRows.Add(new ValidationError { RowIndex = rowIndex, ErrorMessage = errorMsg, ExcelColumnName = "SQL Engine (Check)" });
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                addLog($"❌ [چک - ردیف {rowIndex}] شکست (شناسه {currentId ?? "?"}) - دلیل: {ex.Message}");
                                sqlErrorRows.Add(new ValidationError { RowIndex = rowIndex, ErrorMessage = ex.Message, ExcelColumnName = "SQL Engine (Check)" });
                            }
                        }
                        rowIndex++;
                    }
                }
            }

            if (sqlErrorRows.Count > 0)
                GenerateSqlErrorExcel(executionData.CheckFilePath, executionData.CheckSheetName, sqlErrorRows, reportFolder, "Check_SQL_Errors.xlsx", addLog);

            return (successCount, sqlErrorRows.Count);
        }

        // ====================================================================
        // 🛠 متدهای کمکی و استخراج داده
        // ====================================================================
        private decimal ParseStrictDecimal(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            string cleanValue = value.Replace("/", ".").Replace(",", "").Trim();
            if (decimal.TryParse(cleanValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result)) return result;
            throw new Exception($"مقدار «{value}» برای ({fieldName}) عدد معتبری نیست.");
        }

        private long ParseStrictLong(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            string cleanValue = value.Replace("/", "").Replace(",", "").Replace(".", "").Trim();
            if (long.TryParse(cleanValue, out long result)) return result;
            throw new Exception($"مقدار «{value}» برای ({fieldName}) یک عدد صحیح معتبر نیست.");
        }

        private Dictionary<string, int> LoadBanksIntoMemory(SqlConnection conn)
        {
            var banks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand("SELECT ID, Name FROM TblBank", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    // ✨ حل مشکل کست: استفاده از Convert.ToInt32(reader.GetValue(0))
                    banks[reader.GetString(1).Replace("ي", "ی").Replace("ك", "ک").Trim()] = Convert.ToInt32(reader.GetValue(0));
                }
            }
            return banks;
        }

        private Dictionary<string, int> LoadUnitsIntoMemory(SqlConnection conn)
        {
            var units = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand("SELECT ID, Name FROM TblUnit", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader.GetValue(0));
                    string rawName = reader.GetString(1).Trim();
                    string persianNorm = rawName.Replace("ي", "ی").Replace("ك", "ک").Trim();
                    string compact = persianNorm.Replace("\u200c", "").Replace(" ", "");

                    units[rawName] = id;
                    units[persianNorm] = id;
                    units[compact] = id;

                    // نگاشت مترادف‌های متداول به شناسه دیتابیس
                    if (persianNorm == "کیلو" || compact == "کیلو")
                    {
                        units["کیلوگرم"] = id;
                        units["کيلوگرم"] = id;
                        units["کیلو گرم"] = id;
                        units["کيلو گرم"] = id;
                        units["کیلو‌گرم"] = id;
                    }
                    if (persianNorm == "مترمربع" || compact == "مترمربع")
                    {
                        units["متر مربع"] = id;
                        units["متر‌مربع"] = id;
                    }
                    if (persianNorm == "کارتن" || compact == "کارتن")
                    {
                        units["کارتون"] = id;
                        units["كارتون"] = id;
                    }
                    if (persianNorm == "عدد" || compact == "عدد")
                    {
                        units["تعداد"] = id;
                    }
                    if (persianNorm == "بسته" || compact == "بسته")
                    {
                        units["باکس"] = id;
                        units["بكس"] = id;
                    }
                }
            }
            return units;
        }

        private string ResolveUnitAlias(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

            string cleaned = rawName.Replace("ي", "ی").Replace("ك", "ک").Trim();
            string compact = cleaned.Replace("\u200c", "").Replace(" ", "");

            if (compact == "کیلوگرم" || compact == "کیلو" || compact == "کيلوگرم" || compact == "کيلو")
                return "کیلو";
            if (compact == "مترمربع")
                return "مترمربع";
            if (compact == "کارتون" || compact == "کارتن" || compact == "كارتن" || compact == "كارتون")
                return "کارتن";
            if (compact == "تعداد" || compact == "عدد")
                return "عدد";
            if (compact == "لیتر" || compact == "ليتر")
                return "لیتر";
            if (compact == "بشکه" || compact == "بشكه")
                return "بشکه";
            if (compact == "قوطی" || compact == "قوطي")
                return "قوطی";
            if (compact == "گرم")
                return "گرم";

            return cleaned;
        }

        private int GetOrAddUnit(SqlConnection conn, Dictionary<string, int> unitCache, string rawUnitName, Action<string> addLog)
        {
            if (string.IsNullOrWhiteSpace(rawUnitName)) return 0;

            string alias = ResolveUnitAlias(rawUnitName);

            // 1. بررسی در کش با مترادف
            if (unitCache.TryGetValue(alias, out int cachedId))
                return cachedId;

            // 2. بررسی با نام ورودی خام
            if (unitCache.TryGetValue(rawUnitName, out int rawId))
                return rawId;

            // 3. بررسی با حالت نرمالایز و فشرده
            string norm = rawUnitName.Replace("ي", "ی").Replace("ك", "ک").Trim();
            if (unitCache.TryGetValue(norm, out int normId))
                return normId;

            string compact = norm.Replace("\u200c", "").Replace(" ", "");
            if (unitCache.TryGetValue(compact, out int compactId))
                return compactId;

            // 4. استعلام از دیتابیس یا درج داینامیک در جدول TblUnit
            string unitNameToStore = norm.Length > 50 ? norm.Substring(0, 50) : norm;
            string query = @"
                IF EXISTS (SELECT 1 FROM TblUnit WHERE Name = @Name)
                BEGIN
                    SELECT ID FROM TblUnit WHERE Name = @Name;
                END
                ELSE
                BEGIN
                    DECLARE @NewId INT = ISNULL((SELECT MAX(ID) FROM TblUnit), 0) + 1;
                    INSERT INTO TblUnit (ID, Name, Intiger, SiteSync, NameEn, MoadiCode)
                    VALUES (@NewId, @Name, 0, 0, '', 0);
                    SELECT @NewId;
                END";

            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Name", unitNameToStore);
                object? result = cmd.ExecuteScalar();
                int unitId = Convert.ToInt32(result);

                unitCache[alias] = unitId;
                unitCache[rawUnitName] = unitId;
                unitCache[norm] = unitId;
                unitCache[compact] = unitId;
                unitCache[unitNameToStore] = unitId;

                addLog($"✨ [واحد کالا] واحد جدید «{unitNameToStore}» در سیستم و دیتابیس ثبت شد (شناسه: {unitId}).");
                return unitId;
            }
        }

        private Dictionary<string, byte> LoadCheckStatesIntoMemory(SqlConnection conn)
        {
            var states = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand("SELECT ID, Name FROM TblCheck_State", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    states[reader.GetString(1).Replace("ي", "ی").Replace("ك", "ک").Trim()] = Convert.ToByte(reader.GetValue(0));
                }
            }
            return states;
        }
        private Dictionary<string, string> ExtractRowData(IExcelDataReader reader, List<ColumnMappingItem> activeMappings, Dictionary<string, int> headerIndices)
        {
            var rowData = new Dictionary<string, string>();
            foreach (var map in activeMappings)
            {
                string excelColClean = map.SelectedExcelColumn?.Trim().Replace("ي", "ی").Replace("ك", "ک") ?? "";
                if (!string.IsNullOrEmpty(excelColClean) && headerIndices.TryGetValue(excelColClean, out int colIndex))
                {
                    string value = reader.GetValue(colIndex)?.ToString()?.Trim() ?? string.Empty;
                    rowData[map.DisplayName.Trim()] = value.Replace("ي", "ی").Replace("ك", "ک");
                }
            }
            return rowData;
        }

        // ====================================================================
        // 📦 توابع درج جداول کالا
        // ====================================================================
        private string? ProcessGroupKala(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData)
        {
            if (!rowData.TryGetValue("گروه کالا", out string? groupPath) || string.IsNullOrWhiteSpace(groupPath)) return null;

            string[] nodes = groupPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int parentId = 0;
            string? leafCode = null;

            foreach (var nodeName in nodes)
            {
                bool exists = false;
                string checkQuery = "SELECT TOP 1 Id, Code FROM TblGroupKala WHERE Name = @Name AND Father = @Father";
                using (var cmd = new SqlCommand(checkQuery, conn, trans))
                {
                    cmd.Parameters.AddWithValue("@Name", nodeName.Trim());
                    cmd.Parameters.AddWithValue("@Father", parentId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            parentId = Convert.ToInt32(reader["Id"]);
                            leafCode = reader["Code"]?.ToString();
                            exists = true;
                        }
                    }
                }

                if (!exists)
                {
                    string insertQuery = @"
                        DECLARE @NewId INT, @NewCode NVARCHAR(50), @NewMinCode NVARCHAR(50), @LenNext INT;
                        IF @Father = 0
                        BEGIN
                            SET @LenNext = 2;
                            DECLARE @MaxRoot INT = ISNULL((SELECT MAX(CAST(Code AS INT)) FROM TblGroupKala WHERE Father = 0 AND ISNUMERIC(Code) = 1), 0);
                            SET @NewMinCode = RIGHT('000' + CAST(@MaxRoot + 1 AS VARCHAR), 3);
                            SET @NewCode = @NewMinCode;
                        END
                        ELSE
                        BEGIN
                            SET @LenNext = 0;
                            DECLARE @FatherCode NVARCHAR(50), @FatherMinCode NVARCHAR(50);
                            SELECT @FatherCode = Code, @FatherMinCode = MinCode FROM TblGroupKala WHERE Id = @Father;
                            DECLARE @MaxChildMin INT = (SELECT MAX(CAST(MinCode AS INT)) FROM TblGroupKala WHERE Father = @Father AND ISNUMERIC(MinCode) = 1);
                            IF @MaxChildMin IS NULL SET @MaxChildMin = CAST(ISNULL(@FatherMinCode, '0') AS INT) * 100;
                            SET @NewMinCode = CAST(@MaxChildMin + 1 AS VARCHAR);
                            SET @NewCode = ISNULL(@FatherCode, '') + '-' + @NewMinCode;
                        END
                        IF OBJECTPROPERTY(OBJECT_ID('TblGroupKala'), 'TableHasIdentity') = 1
                        BEGIN
                            INSERT INTO TblGroupKala (Name, Father, Code, LenNextLevel, MinCode, CreateTime) VALUES (@Name, @Father, @NewCode, @LenNext, @NewMinCode, GETDATE());
                            SET @NewId = SCOPE_IDENTITY();
                        END
                        ELSE
                        BEGIN
                            SET @NewId = ISNULL((SELECT MAX(Id) FROM TblGroupKala), 0) + 1;
                            INSERT INTO TblGroupKala (Id, Name, Father, Code, LenNextLevel, MinCode, CreateTime) VALUES (@NewId, @Name, @Father, @NewCode, @LenNext, @NewMinCode, GETDATE());
                        END
                        SELECT @NewId AS NewId, @NewCode AS NewCode;";

                    using (var insertCmd = new SqlCommand(insertQuery, conn, trans))
                    {
                        insertCmd.Parameters.AddWithValue("@Name", nodeName.Trim());
                        insertCmd.Parameters.AddWithValue("@Father", parentId);
                        using (var reader = insertCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                parentId = Convert.ToInt32(reader["NewId"]);
                                leafCode = reader["NewCode"]?.ToString();
                            }
                        }
                    }
                }
            }
            return leafCode;
        }

        private void ProcessTblAnbar(SqlConnection conn, SqlTransaction trans, string idAnbar, Dictionary<string, string> rowData)
        {
            rowData.TryGetValue("نام انبار", out string? nameAnbar);
            string anbarName = string.IsNullOrWhiteSpace(nameAnbar) ? $"انبار {idAnbar.Trim()}" : nameAnbar.Trim();

            string query = @"
                IF NOT EXISTS (SELECT 1 FROM TblAnbar WHERE ID = @ID)
                BEGIN
                    INSERT INTO TblAnbar (ID, Name) VALUES (@ID, @Name)
                END";
            using (var cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@ID", idAnbar.Trim());
                cmd.Parameters.AddWithValue("@Name", anbarName);
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertTblKala(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData, string idKala, string idAnbar, int? unit1Id, int? unit2Id, string? groupCode)
        {
            rowData.TryGetValue("نام کالا", out string? name);
            decimal u1u2 = ParseStrictDecimal(rowData.GetValueOrDefault("نرخ تبدیل واحد"), "نرخ تبدیل واحد");
            decimal bPrice = ParseStrictDecimal(rowData.GetValueOrDefault("قیمت آخرین خرید"), "قیمت آخرین خرید");
            decimal nRequest = ParseStrictDecimal(rowData.GetValueOrDefault("حد تجدید سفارش"), "حد تجدید سفارش");
            decimal maliyatVal = ParseStrictDecimal(rowData.GetValueOrDefault("درصد مالیات کالا"), "درصد مالیات کالا");
            bool hasMaliat = maliyatVal > 0;
            rowData.TryGetValue("شناسه کالا مودی", out string? moadiKalaId);

            string query = @"
                IF NOT EXISTS (SELECT 1 FROM TblKala WHERE IDKala = @IDKala AND IDAnbar = @IDAnbar)
                BEGIN
                    INSERT INTO TblKala (IDKala, IDAnbar, Name, Unit1, Unit2, U1toU2, GroupCode, maliyat, DarsadMaliat, Bprice, Nrequest, MoadiKalaId) 
                    VALUES (@IDKala, @IDAnbar, @Name, @Unit1, @Unit2, @U1toU2, @GroupCode, @Maliyat, @DarsadMaliat, @Bprice, @Nrequest, @MoadiKalaId)
                END
                ELSE
                BEGIN
                    THROW 50001, 'این کالا با این انبار قبلاً در سیستم ثبت شده است.', 1;
                END";

            using (var cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@IDKala", idKala);
                cmd.Parameters.AddWithValue("@IDAnbar", idAnbar);
                cmd.Parameters.AddWithValue("@Name", string.IsNullOrWhiteSpace(name) ? (object)DBNull.Value : name.Trim());
                cmd.Parameters.AddWithValue("@Unit1", unit1Id.HasValue && unit1Id.Value > 0 ? unit1Id.Value : 0);
                cmd.Parameters.AddWithValue("@Unit2", unit2Id.HasValue && unit2Id.Value > 0 ? unit2Id.Value : 0);
                cmd.Parameters.AddWithValue("@U1toU2", u1u2);
                cmd.Parameters.AddWithValue("@GroupCode", string.IsNullOrWhiteSpace(groupCode) ? "0" : groupCode.Trim());
                cmd.Parameters.AddWithValue("@Bprice", bPrice);
                cmd.Parameters.AddWithValue("@Nrequest", nRequest);
                cmd.Parameters.AddWithValue("@MoadiKalaId", string.IsNullOrWhiteSpace(moadiKalaId) ? "0" : moadiKalaId.Trim());
                cmd.Parameters.AddWithValue("@Maliyat", hasMaliat ? 1 : 0);
                cmd.Parameters.AddWithValue("@DarsadMaliat", hasMaliat ? maliyatVal : 0);
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertTblPrice(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData, string idKala, string idAnbar)
        {
            string query = @"
                IF NOT EXISTS (SELECT 1 FROM TblPrice WHERE IDKala = @IDKala AND IDAnbar = @IDAnbar AND IDLable = @IDLable)
                BEGIN
                    INSERT INTO TblPrice (IDLable, IDAnbar, IDKala, Price, SiteSync) 
                    VALUES (@IDLable, @IDAnbar, @IDKala, @Price, 0)
                END";

            if (rowData.TryGetValue("قیمت فروش", out string? priceSaleStr) && !string.IsNullOrWhiteSpace(priceSaleStr))
            {
                decimal p1 = ParseStrictDecimal(priceSaleStr, "قیمت فروش");
                if (p1 > 0)
                {
                    using (var cmd = new SqlCommand(query, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@IDLable", 1);
                        cmd.Parameters.AddWithValue("@IDAnbar", idAnbar);
                        cmd.Parameters.AddWithValue("@IDKala", idKala);
                        cmd.Parameters.AddWithValue("@Price", p1);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            if (rowData.TryGetValue("قیمت مصرف", out string? priceConsumerStr) && !string.IsNullOrWhiteSpace(priceConsumerStr))
            {
                decimal p2 = ParseStrictDecimal(priceConsumerStr, "قیمت مصرف");
                if (p2 > 0)
                {
                    using (var cmd = new SqlCommand(query, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@IDLable", 2);
                        cmd.Parameters.AddWithValue("@IDAnbar", idAnbar);
                        cmd.Parameters.AddWithValue("@IDKala", idKala);
                        cmd.Parameters.AddWithValue("@Price", p2);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void InsertTblTRAnbar(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData, string idKala, string idAnbar)
        {
            decimal tedad = ParseStrictDecimal(rowData.GetValueOrDefault("موجودی انبار"), "موجودی انبار");
            decimal fi = ParseStrictDecimal(rowData.GetValueOrDefault("قیمت آخرین خرید"), "قیمت آخرین خرید");

            if (tedad > 0)
            {
                if (fi < 0) fi = 0;
                using (var cmd = new SqlCommand("INSERT INTO TblTRAnbar (ShFactor, Type, IDAnbar, IDKala, Tedad, Fi, Mablaghekol) VALUES (@ShFactor, @Type, @IDAnbar, @IDKala, @Tedad, @Fi, @Mablaghekol)", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@ShFactor", 1);
                    cmd.Parameters.AddWithValue("@Type", 100);
                    cmd.Parameters.AddWithValue("@IDAnbar", idAnbar);
                    cmd.Parameters.AddWithValue("@IDKala", idKala);
                    cmd.Parameters.AddWithValue("@Tedad", tedad);
                    cmd.Parameters.AddWithValue("@Fi", fi);
                    cmd.Parameters.AddWithValue("@Mablaghekol", tedad * fi);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void InsertTblBarCode(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData, string idKala, string idAnbar)
        {
            if (rowData.TryGetValue("بارکد", out string? barcode) && !string.IsNullOrWhiteSpace(barcode))
            {
                using (var cmd = new SqlCommand("INSERT INTO TblBarCode (IDKala, IDAnbar, Barcod) VALUES (@IDKala, @IDAnbar, @Barcod)", conn, trans))
                {
                    cmd.Parameters.AddWithValue("@IDKala", idKala);
                    cmd.Parameters.AddWithValue("@IDAnbar", idAnbar);
                    cmd.Parameters.AddWithValue("@Barcod", barcode.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ====================================================================
        // 👤 توابع درج جداول اشخاص
        // ====================================================================
        private static string CleanOptionalString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();
            if (trimmed == "0" || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase)) return string.Empty;
            return trimmed;
        }

        private void InsertTblPerson(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData, string shOzviat)
        {
            rowData.TryGetValue("نام", out string? name);
            rowData.TryGetValue("نام خانوادگی", out string? famil);
            rowData.TryGetValue("نام پدر", out string? fatherName);
            rowData.TryGetValue("تاریخ تولد", out string? dtTavalod);
            rowData.TryGetValue("موبایل", out string? mobile);
            rowData.TryGetValue("تلفن", out string? tell);
            rowData.TryGetValue("آدرس", out string? address);

            long srfsl = ParseStrictLong(rowData.GetValueOrDefault("سرفصل"), "سرفصل");
            long moin = ParseStrictLong(rowData.GetValueOrDefault("معین"), "معین");
            long hsb = ParseStrictLong(rowData.GetValueOrDefault("تفضیلی"), "تفضیلی");

            string query = @"
                IF NOT EXISTS (SELECT 1 FROM TblPerson WHERE sh_ozviat = @sh_ozviat)
                BEGIN
                    INSERT INTO TblPerson (sh_ozviat, name, Famil, FatherName, srfsl, moin, Hesab, Dt_Tavalod, Mobile, Tell, Addres_mk)
                    VALUES (@sh_ozviat, @name, @Famil, @FatherName, @srfsl, @moin, @HSB, @Dt_Tavalod, @mobile, @Tell, @Addres_mk)
                END
                ELSE
                BEGIN
                    THROW 50001, 'شخصی با این شناسه قبلاً در سیستم ثبت شده است.', 1;
                END";

            using (var cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@sh_ozviat", shOzviat.Trim());
                cmd.Parameters.AddWithValue("@name", CleanOptionalString(name));
                cmd.Parameters.AddWithValue("@Famil", CleanOptionalString(famil));
                cmd.Parameters.AddWithValue("@FatherName", CleanOptionalString(fatherName));

                cmd.Parameters.AddWithValue("@srfsl", srfsl);
                cmd.Parameters.AddWithValue("@moin", moin);
                cmd.Parameters.AddWithValue("@HSB", hsb);

                cmd.Parameters.AddWithValue("@Dt_Tavalod", CleanOptionalString(dtTavalod));
                cmd.Parameters.AddWithValue("@mobile", CleanOptionalString(mobile));
                cmd.Parameters.AddWithValue("@Tell", CleanOptionalString(tell));
                cmd.Parameters.AddWithValue("@Addres_mk", CleanOptionalString(address));
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertTblPersonTell(SqlConnection conn, SqlTransaction trans, string shOzviat, string tell)
        {
            string query = "INSERT INTO TblPersonTell (ShOzviat, Tell, Type) VALUES (@ShOzviat, @Tell, 2)";
            using (var cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@ShOzviat", shOzviat.Trim());
                cmd.Parameters.AddWithValue("@Tell", tell.Trim());
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertMaster(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData, string personCod)
        {
            long srfsl = ParseStrictLong(rowData.GetValueOrDefault("سرفصل"), "سرفصل");
            long moin = ParseStrictLong(rowData.GetValueOrDefault("معین"), "معین");
            long hsb = ParseStrictLong(rowData.GetValueOrDefault("تفضیلی"), "تفضیلی");

            long shob = 0;
            if (rowData.TryGetValue("شعبه", out string? shobStr) && !string.IsNullOrWhiteSpace(shobStr))
                shob = ParseStrictLong(shobStr, "شعبه");

            string query = @"
                INSERT INTO [Master] (personcod, srfsl, moin, HSB, Shob)
                VALUES (@personcod, @srfsl, @moin, @HSB, @Shob)";

            using (var cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@personcod", personCod.Trim());
                cmd.Parameters.AddWithValue("@srfsl", srfsl);
                cmd.Parameters.AddWithValue("@moin", moin);
                cmd.Parameters.AddWithValue("@HSB", hsb);
                cmd.Parameters.AddWithValue("@Shob", shob);
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertTR(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData)
        {
            long sodor = ParseStrictLong(rowData.GetValueOrDefault("ردیف"), "ردیف");
            long srfsl = ParseStrictLong(rowData.GetValueOrDefault("سرفصل"), "سرفصل");
            long moin = ParseStrictLong(rowData.GetValueOrDefault("معین"), "معین");
            long hsb = ParseStrictLong(rowData.GetValueOrDefault("تفضیلی"), "تفضیلی");
            long bed = ParseStrictLong(rowData.GetValueOrDefault("بدهکار"), "بدهکار");
            long bes = ParseStrictLong(rowData.GetValueOrDefault("بستانکار"), "بستانکار");
            // شماره سند طبق تسک ۷ در موتور SQL روی ۱ هاردکد می‌شود
            long sanadM = 1;

            rowData.TryGetValue("تاریخ ثبت", out string? dateR);
            if (string.IsNullOrWhiteSpace(dateR)) throw new Exception("تاریخ ثبت نمی‌تواند خالی باشد.");

            rowData.TryGetValue("شرح", out string? sharh);
            string finalSharh = CleanOptionalString(sharh);

            long shob = 0;
            if (rowData.TryGetValue("شعبه", out string? shobStr) && !string.IsNullOrWhiteSpace(shobStr))
                shob = ParseStrictLong(shobStr, "شعبه");

            string query = @"
                INSERT INTO TR (sodor, srfsl, moin, HSB, bed, bes, DateR, sharh, SanadM, Shob)
                VALUES (@sodor, @srfsl, @moin, @HSB, @bed, @bes, @DateR, @sharh, @SanadM, @Shob)";

            using (var cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@sodor", sodor);
                cmd.Parameters.AddWithValue("@srfsl", srfsl);
                cmd.Parameters.AddWithValue("@moin", moin);
                cmd.Parameters.AddWithValue("@HSB", hsb);
                cmd.Parameters.AddWithValue("@bed", bed);
                cmd.Parameters.AddWithValue("@bes", bes);
                cmd.Parameters.AddWithValue("@DateR", dateR.Trim());
                cmd.Parameters.AddWithValue("@sharh", finalSharh);
                cmd.Parameters.AddWithValue("@SanadM", sanadM);
                cmd.Parameters.AddWithValue("@Shob", shob);
                cmd.ExecuteNonQuery();
            }
        }

        // ====================================================================
        // 💳 درج چک در پایگاه داده
        // ====================================================================

        private void InsertCheck(SqlConnection conn, SqlTransaction trans, Dictionary<string, string> rowData, Dictionary<string, int> bankCache, Dictionary<string, byte> stateCache)
        {
            string type = rowData.GetValueOrDefault("نوع چک")?.Trim() ?? "";
            if (type != "دریافتی" && type != "پرداختی") throw new Exception("نوع چک فقط باید کلمه «دریافتی» یا «پرداختی» باشد.");

            int id = (int)ParseStrictLong(rowData.GetValueOrDefault("ردیف چک"), "ردیف چک");
            string serial = rowData.GetValueOrDefault("سریال چک") ?? "";

            int sarfasl1 = (int)ParseStrictLong(rowData.GetValueOrDefault("سرفصل پس از وصول"), "سرفصل پس از وصول");
            int moin1 = (int)ParseStrictLong(rowData.GetValueOrDefault("معین پس از وصول"), "معین پس از وصول");
            long hsb1 = ParseStrictLong(rowData.GetValueOrDefault("حساب پس از وصول"), "حساب پس از وصول");

            int sarfasl2 = (int)ParseStrictLong(rowData.GetValueOrDefault("سرفصل چک"), "سرفصل چک");
            int moin2 = (int)ParseStrictLong(rowData.GetValueOrDefault("معین چک"), "معین چک");
            long hsb2 = ParseStrictLong(rowData.GetValueOrDefault("حساب چک"), "حساب چک");
            long mab = ParseStrictLong(rowData.GetValueOrDefault("مبلغ"), "مبلغ");

            string stateName = rowData.GetValueOrDefault("وضعیت چک") ?? "";
            byte stateId = stateCache.TryGetValue(stateName, out byte s) ? s : throw new Exception($"وضعیت «{stateName}» در پایگاه داده تعریف نشده است.");

            // انطباق دقیق نام بانک (بدون حذف خودکار کلمه بانک به دلیل وجود بانک‌هایی نظیر «پست بانک»)
            string rawBankName = rowData.GetValueOrDefault("بانک")?.Trim().Replace("ي", "ی").Replace("ك", "ک") ?? "";
            int finalBankId = 0;

            if (!string.IsNullOrWhiteSpace(rawBankName))
            {
                var foundBank = bankCache.FirstOrDefault(b => b.Key.Equals(rawBankName, StringComparison.OrdinalIgnoreCase));
                if (foundBank.Key != null)
                    finalBankId = foundBank.Value;
                else
                    throw new Exception($"بانک «{rawBankName}» در سیستم تعریف نشده است. (نام بانک باید دقیقاً مطابق با نام‌های ثبت‌شده در سیستم باشد).");
            }

            string rawShob = string.IsNullOrWhiteSpace(rowData.GetValueOrDefault("شعبه")) ? "" : rowData["شعبه"].Trim();
            string query = "";

            if (type == "دریافتی")
            {
                query = @"
            IF NOT EXISTS (SELECT 1 FROM [Check] WHERE Id = @Id)
            BEGIN
                INSERT INTO [Check] 
                (Id, Bank, shob, serial, sharh, sarfasl1, moin1, hsb1, sarfasl2, moin2, hsb2, DateR, DateChek, CodShobe, numhesab, State, mab, SayadNum, SeriNum)
                VALUES 
                (@Id, @Bank, @shob, @serial, @sharh, @sarfasl1, @moin1, @hsb1, @sarfasl2, @moin2, @hsb2, @DateR, @DateChek, @CodShobe, @numhesab, @State, @mab, @SayadNum, @SeriNum)
            END
            ELSE
            BEGIN
                THROW 50001, 'چک با این ردیف قبلاً ثبت شده است.', 1;
            END";
            }
            else // نوع پرداختی
            {
                // ✨ اصلاح کلیدی: تغییر ستون از CShobe به ShobeName (دقیقاً مطابق ساختار دیتابیس)
                query = @"
            IF NOT EXISTS (SELECT 1 FROM CheckPardakht WHERE Id = @Id)
            BEGIN
                INSERT INTO CheckPardakht 
                (Id, Bank, ShobeName, serial, sharh, sarfasl1, moin1, hsb1, sarfasl2, moin2, hsb2, DateR, DateChek, CodeShobe, Numhesab, State, mab, SayadNum, SeriNum)
                VALUES 
                (@Id, @Bank, @shob, @serial, @sharh, @sarfasl1, @moin1, @hsb1, @sarfasl2, @moin2, @hsb2, @DateR, @DateChek, @CodShobe, @numhesab, @State, @mab, @SayadNum, @SeriNum)
            END
            ELSE
            BEGIN
                THROW 50001, 'چک با این ردیف قبلاً ثبت شده است.', 1;
            END";
            }

            using (var cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Bank", finalBankId);
                cmd.Parameters.AddWithValue("@shob", rawShob); // ارسال متن شعبه با خیال راحت
                cmd.Parameters.AddWithValue("@serial", serial);
                cmd.Parameters.AddWithValue("@sharh", string.IsNullOrWhiteSpace(rowData.GetValueOrDefault("دریافت کننده")) ? "" : rowData["دریافت کننده"].Trim());
                cmd.Parameters.AddWithValue("@sarfasl1", sarfasl1);
                cmd.Parameters.AddWithValue("@moin1", moin1);
                cmd.Parameters.AddWithValue("@hsb1", hsb1);
                cmd.Parameters.AddWithValue("@sarfasl2", sarfasl2);
                cmd.Parameters.AddWithValue("@moin2", moin2);
                cmd.Parameters.AddWithValue("@hsb2", hsb2);
                cmd.Parameters.AddWithValue("@DateR", string.IsNullOrWhiteSpace(rowData.GetValueOrDefault("تاریخ صدور")) ? "" : rowData["تاریخ صدور"].Trim());
                cmd.Parameters.AddWithValue("@DateChek", rowData.GetValueOrDefault("تاریخ سررسید") ?? "");
                cmd.Parameters.AddWithValue("@CodShobe", string.IsNullOrWhiteSpace(rowData.GetValueOrDefault("کد شعبه")) ? "" : rowData["کد شعبه"].Trim());
                cmd.Parameters.AddWithValue("@numhesab", string.IsNullOrWhiteSpace(rowData.GetValueOrDefault("شماره حساب")) ? "" : rowData["شماره حساب"].Trim());
                cmd.Parameters.AddWithValue("@State", stateId);
                cmd.Parameters.AddWithValue("@mab", mab);
                cmd.Parameters.AddWithValue("@SayadNum", string.IsNullOrWhiteSpace(rowData.GetValueOrDefault("شماره صیاد")) ? "" : rowData["شماره صیاد"].Trim());
                cmd.Parameters.AddWithValue("@SeriNum", string.IsNullOrWhiteSpace(rowData.GetValueOrDefault("سری چک")) ? "" : rowData["سری چک"].Trim());
               

                cmd.ExecuteNonQuery();
            }
        }
        private void GenerateSqlErrorExcel(string originalExcelPath, string sheetName, List<ValidationError> sqlErrors, string reportFolder, string fileName, Action<string> addLog)
        {
            try
            {
                string outputPath = System.IO.Path.Combine(reportFolder, fileName);
                using (var stream = new FileStream(originalExcelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var originalWb = new XLWorkbook(stream))
                {
                    var originalSheet = originalWb.Worksheet(sheetName);
                    using (var newWb = new XLWorkbook())
                    {
                        newWb.RightToLeft = true;
                        var newSheet = newWb.AddWorksheet("رکوردهای ناموفق");
                        newSheet.RightToLeft = true;
                        int colCount = originalSheet.LastColumnUsed()?.ColumnNumber() ?? 1;

                        for (int c = 1; c <= colCount; c++)
                        {
                            newSheet.Cell(1, c).Value = originalSheet.Cell(1, c).Value;
                            newSheet.Cell(1, c).Style.Font.Bold = true;
                            newSheet.Cell(1, c).Style.Fill.BackgroundColor = XLColor.LightGray;
                        }

                        int errorColIndex = colCount + 1;
                        newSheet.Cell(1, errorColIndex).Value = "علت دقیق شکست درج";
                        newSheet.Cell(1, errorColIndex).Style.Font.Bold = true;
                        newSheet.Cell(1, errorColIndex).Style.Fill.BackgroundColor = XLColor.Yellow;

                        int newRowIndex = 2;
                        foreach (var err in sqlErrors.OrderBy(e => e.RowIndex))
                        {
                            for (int c = 1; c <= colCount; c++) newSheet.Cell(newRowIndex, c).Value = originalSheet.Cell(err.RowIndex, c).Value;
                            newSheet.Cell(newRowIndex, errorColIndex).Value = err.ErrorMessage;
                            newSheet.Cell(newRowIndex, errorColIndex).Style.Font.FontColor = XLColor.Red;
                            newRowIndex++;
                        }
                        newSheet.Columns().AdjustToContents();
                        newWb.SaveAs(outputPath);
                        addLog($"✔️ [System] فایل اکسل خطاها '{fileName}' تولید شد.");
                    }
                }
            }
            catch (Exception ex)
            {
                addLog($"❌ [System Error] خطا در تولید فایل اکسل: {ex.Message}");
            }
        }
    }
}