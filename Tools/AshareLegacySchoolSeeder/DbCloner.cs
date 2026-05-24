using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace Ashare.Legacy.SchoolSeeder;

/// <summary>
/// يَنسَخ قاعِدَة بَيانات الإنتاج كامِلَةً (كُلّ الجَداوِل + الفَهارِس +
/// المَفاتيح) إلى قاعِدَة جَديدَة، عَبر اتِّصالَين مُنفَصِلَين (مُضيف مُختَلِف):
/// <list type="number">
///   <item>SMO يُولِّد مُخَطَّط المَصدَر كامِلاً وَيُطَبِّقه على الهَدَف.</item>
///   <item>SqlBulkCopy يَنسَخ بَيانات كُلّ جَدوَل (يُحافِظ على المَفاتيح،
///         يَتَجاوَز الأعمِدَة المَحسوبَة/timestamp، بِلا فَحص قُيود).</item>
/// </list>
/// آمِن: لا يَلمِس المَصدَر إلّا قِراءَةً. يَشتَرِط أَن يَكون الهَدَف فارِغاً
/// (لا جَداوِل مُستخدِم) لِتَجَنُّب الازدِواج.
/// </summary>
public sealed class DbCloner
{
    private readonly string _sourceCs;
    private readonly string _targetCs;
    private readonly bool _apply;

    public DbCloner(string sourceCs, string targetCs, bool apply)
    {
        _sourceCs = sourceCs;
        _targetCs = targetCs;
        _apply = apply;
    }

    private static void Log(string m) => Console.WriteLine(m);

    public async Task CloneAsync(CancellationToken ct)
    {
        var srcDbName = new SqlConnectionStringBuilder(_sourceCs).InitialCatalog;
        if (string.IsNullOrWhiteSpace(srcDbName))
            throw new InvalidOperationException("Source connection string بِلا Initial Catalog/Database.");

        // ① تَحَقُّق: الهَدَف يَجِب أَن يَكون فارِغاً.
        var targetTableCount = await ScalarAsync(_targetCs,
            "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0", ct);
        if (targetTableCount > 0)
            throw new InvalidOperationException(
                $"القاعِدَة الهَدَف تَحوي {targetTableCount} جَدوَلاً بِالفِعل. " +
                "أفرِغها (DROP DATABASE ثُمَّ CREATE، أو احذِف الجَداوِل) قَبل الـ clone لِتَجَنُّب الازدِواج.");

        // ② مُخَطَّط المَصدَر عَبر SMO.
        Log("قِراءَة مُخَطَّط المَصدَر عَبر SMO…");
        using var smoConn = new SqlConnection(_sourceCs);
        var server = new Server(new ServerConnection(smoConn));
        // أداء: حَمِّل الحُقول المَطلوبَة فَقَط.
        server.SetDefaultInitFields(typeof(Table), nameof(Table.IsSystemObject), nameof(Table.Name), nameof(Table.Schema));
        server.SetDefaultInitFields(typeof(Column), nameof(Column.Name), nameof(Column.Computed));

        var db = server.Databases[srcDbName]
                 ?? throw new InvalidOperationException($"لَم يُعثَر على قاعِدَة المَصدَر '{srcDbName}'.");

        var tables = db.Tables.Cast<Table>().Where(t => !t.IsSystemObject).ToList();
        Log($"عَدَد الجَداوِل: {tables.Count}");

        var scripter = new Scripter(server)
        {
            Options =
            {
                ScriptSchema = true,
                ScriptData = false,
                ScriptDrops = false,
                Indexes = true,
                ClusteredIndexes = true,
                NonClusteredIndexes = true,
                DriAll = true,            // PK + FK + defaults + checks + unique
                Triggers = false,
                ExtendedProperties = false,
                IncludeIfNotExists = false,
                IncludeDatabaseContext = false,
                NoCollation = false,
                AnsiPadding = false,
                SchemaQualify = true
            }
        };

        var urns = tables.Select(t => t.Urn).ToArray();
        var script = scripter.Script(urns); // StringCollection: CREATE TABLEs ثُمَّ القُيود، مُرَتَّبَة

        if (_apply)
        {
            Log("تَطبيق المُخَطَّط على الهَدَف…");
            using var tcon = new SqlConnection(_targetCs);
            await tcon.OpenAsync(ct);
            int n = 0;
            foreach (var batch in script)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var cmd = new SqlCommand(batch, tcon) { CommandTimeout = 0 };
                await cmd.ExecuteNonQueryAsync(ct);
                n++;
            }
            Log($"  طُبِّق {n} أمر مُخَطَّط.");
        }
        else Log($"  (dry-run) سَيُطَبَّق {script.Count} أمر مُخَطَّط.");

        // ③ نَسخ البَيانات جَدوَلاً جَدوَلاً.
        Log("نَسخ البَيانات (SqlBulkCopy)…");
        long totalRows = 0;
        foreach (var t in tables)
        {
            // أعمِدَة قابِلَة لِلإدراج فَقَط (نَتَجاوَز المَحسوبَة وَ timestamp).
            var cols = t.Columns.Cast<Column>()
                .Where(c => !c.Computed && c.DataType?.SqlDataType != SqlDataType.Timestamp)
                .Select(c => c.Name)
                .ToList();
            if (cols.Count == 0) continue;

            var full = $"[{t.Schema}].[{t.Name}]";
            var colList = string.Join(", ", cols.Select(c => $"[{c}]"));

            using var rc = new SqlConnection(_sourceCs);
            await rc.OpenAsync(ct);
            using var cmd = new SqlCommand($"SELECT {colList} FROM {full}", rc) { CommandTimeout = 0 };
            using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!_apply)
            {
                Log($"  (dry-run) {full}: {cols.Count} عَمود");
                continue;
            }

            using var bulk = new SqlBulkCopy(_targetCs,
                SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls)
            {
                DestinationTableName = full,
                BulkCopyTimeout = 0,
                BatchSize = 2000
            };
            foreach (var c in cols) bulk.ColumnMappings.Add(c, c);

            await bulk.WriteToServerAsync(reader, ct);
            Log($"  ✔ {full}");
            totalRows += bulk.RowsCopied;
        }
        if (_apply) Log($"إجماليّ الصُّفوف المَنسوخَة: {totalRows:N0}");
    }

    private static async Task<long> ScalarAsync(string cs, string sql, CancellationToken ct)
    {
        using var con = new SqlConnection(cs);
        await con.OpenAsync(ct);
        using var cmd = new SqlCommand(sql, con) { CommandTimeout = 0 };
        var o = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(o ?? 0);
    }
}
