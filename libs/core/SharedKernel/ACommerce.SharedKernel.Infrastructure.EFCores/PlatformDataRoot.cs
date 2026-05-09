namespace ACommerce.SharedKernel.Infrastructure.EFCores;

/// <summary>
/// يُحدّد المسار المطلق لمجلّد <c>data/</c> المشترك الذي تُخزَّن فيه قواعد
/// بيانات منصّة واحدة — بحيث كل خدمات Order تشترك في ملف واحد، وكل خدمات
/// Ashare في ملف آخر، بغضّ النظر عن:
///   • من أين يُشغَّل التطبيق (Visual Studio / <c>dotnet run</c> من مجلّد فرعي / سكريبت)
///   • على أي نظام تشغيل (Windows / Linux / macOS)
///   • وجود متغيّر بيئة أم لا
///
/// خوارزميّة الاكتشاف (مرتّبة):
///   1. إذا ضُبط <c>ACOMMERCE_DATA_ROOT</c> — استخدمه كما هو.
///   2. ابدأ من <paramref name="contentRoot"/> واصعد دليلاً دليلاً حتى
///      تجد ملفاً بامتداد <c>.sln</c> — هذا دليل الريبو.  ارجع
///      <c>&lt;دليل الريبو&gt;/data</c>.
///   3. لم يُعثر على <c>.sln</c> (مثل نشر منفرد) — ارجع
///      <c>&lt;contentRoot&gt;/data</c>.
///
/// يُنشئ المجلّد تلقائياً إذا لم يكن موجوداً.
/// </summary>
public static class PlatformDataRoot
{
    public static string Resolve(string contentRoot)
    {
        var envVar = Environment.GetEnvironmentVariable("ACOMMERCE_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            Directory.CreateDirectory(envVar);
            return envVar;
        }

        // Walk up from contentRoot looking for a .sln — that's the repo root.
        var dir = new DirectoryInfo(contentRoot);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
            {
                var shared = Path.Combine(dir.FullName, "data");
                Directory.CreateDirectory(shared);
                return shared;
            }
            dir = dir.Parent;
        }

        // No repo root discovered (deployed single-service scenario).  Fall
        // back to content root's data dir — unified mode will be opt-in via
        // env var.
        var fallback = Path.Combine(contentRoot, "data");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    /// <summary>
    /// Shortcut: returns the absolute <c>Data Source=…</c> connection string
    /// for a platform's shared SQLite file.
    /// </summary>
    public static string SqliteConnectionString(string contentRoot, string platformDbFileName)
        => $"Data Source={Path.Combine(Resolve(contentRoot), platformDbFileName)}";
}
