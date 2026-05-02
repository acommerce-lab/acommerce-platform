using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ejar.Api.Data;

/// <summary>
/// Design-time factory يستخدمه <c>dotnet ef</c> فقط (لا يعمل وقت التشغيل).
/// يجبر EF أن يولّد migrations بأنواع SQL Server (uniqueidentifier, datetime2,
/// nvarchar…) بصرف النظر عن الـ Provider الذي يقرأه التطبيق وقت التشغيل من
/// appsettings — لأنّ قاعدة الإنتاج هي SQL Server وأنواع SQLite لا تعمل عليها
/// (TEXT لا يصلح PK في SQL Server → "Column is of a type that is invalid for
/// use as a key column in an index").
///
/// <para>الـ connection string هنا وهميّ — EF لا يفتح اتّصالاً عند توليد
/// migration؛ يستخدم فقط type mapper الـ provider لاختيار أسماء الأنواع.</para>
///
/// <para>للتوليد:</para>
/// <code>
/// dotnet ef migrations add &lt;Name&gt; \
///   -p Apps/Ejar/Customer/Backend/Ejar.Api
/// </code>
/// </summary>
public sealed class EjarDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EjarDbContext>
{
    public EjarDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EjarDbContext>()
            .UseSqlServer("Server=(localdb)\\design-time;Database=Ejar.Design;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new EjarDbContext(options);
    }
}
