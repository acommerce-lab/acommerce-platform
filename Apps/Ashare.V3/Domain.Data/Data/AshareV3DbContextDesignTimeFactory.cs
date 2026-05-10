using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ashare.V3.Data;

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
///   -p Apps/AshareV3/Customer/Backend/Ashare.V3.Api
/// </code>
/// </summary>
public sealed class AshareV3DbContextDesignTimeFactory : IDesignTimeDbContextFactory<AshareV3DbContext>
{
    public AshareV3DbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AshareV3DbContext>()
            .UseSqlServer("Server=(localdb)\\design-time;Database=Ashare.V3.Design;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new AshareV3DbContext(options);
    }
}
