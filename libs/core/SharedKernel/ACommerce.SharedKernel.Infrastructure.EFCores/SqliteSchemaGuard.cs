using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ACommerce.SharedKernel.Infrastructure.EFCores;

/// <summary>
/// حارس مخطط SQLite للتطوير: يكشف انجراف المخطط (إضافة/حذف عمود
/// أو جدول في الـ entities دون migration) ويعيد بناء ملف القاعدة بحيث
/// يعمل الـ seeder من جديد.  يعتمد على بصمة (SHA-256) مشتقّة من
/// الجداول والأعمدة وأنواعها كما يراها EF Core.
///
/// كيف يعمل:
///   1) يحسب البصمة الحالية من <c>db.Model</c>.
///   2) إذا كان ملف القاعدة موجوداً يقرأ البصمة المحفوظة في جدول
///      <c>__SchemaFingerprint</c>.  عدم تطابقها أو غيابها = انجراف.
///   3) عند الانجراف يحذف الملف، فيُعاد إنشاؤه عبر EnsureCreatedAsync.
///   4) بعد إنشاء المخطط يكتب البصمة الجديدة.
///
/// آمن لأنه يعمل فقط مع SQLite وفي بيئة التطوير حيث البيانات قابلة
/// لإعادة التوليد عبر السيدر.  لا يلمس SQL Server / PostgreSQL.
/// </summary>
public static class SqliteSchemaGuard
{
    private const string MarkerTable = "__SchemaFingerprint";

    /// <summary>
    /// يستدعى قبل EnsureCreatedAsync.  إن اكتشف انجرافاً يحذف ملف
    /// القاعدة الحالي.  يرجع true إذا أعاد التعيين (ليُشغّل المتصِل
    /// السيدر بعد الإنشاء).
    /// </summary>
    public static bool ResetIfDrifted(DbContext db)
    {
        if (!db.Database.IsSqlite()) return false;

        var dbPath = ExtractDbPath(db.Database.GetConnectionString());
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return false;

        var expected = ComputeFingerprint(db.Model);
        var actual = ReadStoredFingerprint(dbPath);

        if (string.Equals(expected, actual, StringComparison.Ordinal))
            return false;

        // Drift detected — close any pooled connections and remove the file.
        SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
        var walPath = dbPath + "-wal";
        var shmPath = dbPath + "-shm";
        if (File.Exists(walPath)) File.Delete(walPath);
        if (File.Exists(shmPath)) File.Delete(shmPath);
        return true;
    }

    /// <summary>
    /// يستدعى بعد EnsureCreatedAsync لتثبيت البصمة الحالية في القاعدة.
    /// </summary>
    public static void StampFingerprint(DbContext db)
    {
        if (!db.Database.IsSqlite()) return;

        var dbPath = ExtractDbPath(db.Database.GetConnectionString());
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath)) return;

        var fingerprint = ComputeFingerprint(db.Model);
        WriteFingerprint(dbPath, fingerprint);
    }

    private static string? ExtractDbPath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var builder = new SqliteConnectionStringBuilder(connectionString);
        return builder.DataSource;
    }

    private static string ComputeFingerprint(IModel model)
    {
        var sb = new StringBuilder();
        foreach (var entity in model.GetEntityTypes()
            .Where(e => e.GetTableName() != null)
            .OrderBy(e => e.GetTableName(), StringComparer.Ordinal))
        {
            sb.Append(entity.GetTableName()).Append('|');
            foreach (var prop in entity.GetProperties()
                .OrderBy(p => p.GetColumnName(), StringComparer.Ordinal))
            {
                sb.Append(prop.GetColumnName())
                  .Append(':')
                  .Append(prop.GetColumnType())
                  .Append(':')
                  .Append(prop.IsNullable ? '?' : '!')
                  .Append(',');
            }
            sb.Append(';');
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static string? ReadStoredFingerprint(string dbPath)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"SELECT Value FROM {MarkerTable} WHERE Key = 'fingerprint' LIMIT 1;";
            return cmd.ExecuteScalar() as string;
        }
        catch
        {
            // Marker table absent (older DB) → treat as drift.
            return null;
        }
    }

    private static void WriteFingerprint(string dbPath, string fingerprint)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using (var create = conn.CreateCommand())
        {
            create.CommandText =
                $"CREATE TABLE IF NOT EXISTS {MarkerTable} (Key TEXT PRIMARY KEY, Value TEXT);";
            create.ExecuteNonQuery();
        }
        using var upsert = conn.CreateCommand();
        upsert.CommandText =
            $"INSERT INTO {MarkerTable}(Key, Value) VALUES('fingerprint', $v) " +
            "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
        upsert.Parameters.AddWithValue("$v", fingerprint);
        upsert.ExecuteNonQuery();
    }
}
