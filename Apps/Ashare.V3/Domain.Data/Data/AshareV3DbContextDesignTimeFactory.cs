using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ashare.V3.Data;

/// <summary>
/// مَطلوب مِن EF design-time لِـ <c>dotnet ef migrations add</c>. يَستَخدِم
/// SQL Server local placeholder — لا يَتَّصِل فِعليّاً، فَقَط يَبني الـ
/// DbContext لِيُولِّد migration C# scaffolding.
/// </summary>
public sealed class AshareV3DbContextDesignTimeFactory : IDesignTimeDbContextFactory<AshareV3DbContext>
{
    public AshareV3DbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AshareV3DbContext>()
            .UseSqlServer("Server=localhost;Database=ashare-v3-design;Trusted_Connection=True;Encrypt=False;")
            .Options;
        return new AshareV3DbContext(opts);
    }
}
