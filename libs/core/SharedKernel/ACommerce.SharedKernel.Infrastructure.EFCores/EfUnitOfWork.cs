using ACommerce.SharedKernel.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ACommerce.SharedKernel.Infrastructure.EFCore;

/// <summary>
/// تطبيق <see cref="IUnitOfWork"/> فوق EF Core <see cref="DbContext"/>.
/// يُحقَن مع نفس <c>DbContext</c> الـ Scoped الذي تستهلكه الـ repositories،
/// فحفظ واحد يلتقط كلّ التغييرات المتراكمة بصرف النظر عن أيّ repo سجّلها.
///
/// <para>التسجيل في DI:
/// <code>
/// services.AddScoped&lt;DbContext&gt;(sp => sp.GetRequiredService&lt;EjarDbContext&gt;());
/// services.AddScoped&lt;IUnitOfWork, EfUnitOfWork&gt;();
/// </code></para>
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    public EfUnitOfWork(DbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
