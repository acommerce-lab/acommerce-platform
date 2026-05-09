using ACommerce.Notification.Providers.Firebase.Storage;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن دائم لرموز FCM في DB. يستبدل <c>InMemoryDeviceTokenStore</c> الافتراضيّ
/// في الـ kit (الذي يفقد كلّ شيء عند إعادة التشغيل). يُسجَّل كـ <b>Singleton</b>
/// لأنّ <c>FirebaseNotificationChannel</c> Singleton ويأخذنا في constructor —
/// ASP.NET DI تحقّق scope-mismatch لو كنّا Scoped. لاستهلاك
/// <see cref="EjarDbContext"/> (Scoped) نُنشئ scope في كلّ استدعاء عبر
/// <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class EjarDeviceTokenStore : IDeviceTokenStore
{
    private readonly IServiceScopeFactory _scopes;
    public EjarDeviceTokenStore(IServiceScopeFactory scopes) => _scopes = scopes;

    private async Task<T> WithDbAsync<T>(Func<EjarDbContext, Task<T>> work)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();
        return await work(db);
    }

    private async Task WithDbAsync(Func<EjarDbContext, Task> work)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();
        await work(db);
    }

    public Task RegisterAsync(string userId, string deviceToken, string? platform = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var uid)) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(deviceToken)) return Task.CompletedTask;

        return WithDbAsync(async db =>
        {
            // upsert: نفس الرمز قد يصدُر مع UserId مختلف لو سجّل مستخدم خروجاً
            // ودخل آخر على نفس الجهاز. نُحدّث UserId/Platform في الحالة.
            var existing = await db.UserPushTokens.FirstOrDefaultAsync(t => t.Token == deviceToken, ct);
            if (existing is not null)
            {
                existing.UserId = uid;
                existing.Platform = platform;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.UserPushTokens.Add(new UserPushTokenEntity
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    UserId = uid,
                    Token = deviceToken,
                    Platform = platform,
                });
            }
            await db.SaveChangesAsync(ct);
        });
    }

    public Task UnregisterAsync(string deviceToken, CancellationToken ct = default) =>
        WithDbAsync(async db =>
        {
            var row = await db.UserPushTokens.FirstOrDefaultAsync(t => t.Token == deviceToken, ct);
            if (row is null) return;
            db.UserPushTokens.Remove(row);
            await db.SaveChangesAsync(ct);
        });

    public Task<IReadOnlyList<string>> GetTokensAsync(string userId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var uid))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        return WithDbAsync(async db =>
        {
            var rows = await db.UserPushTokens.AsNoTracking()
                .Where(t => t.UserId == uid)
                .Select(t => t.Token)
                .ToListAsync(ct);
            return (IReadOnlyList<string>)rows;
        });
    }

    public Task RemoveAllForUserAsync(string userId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var uid)) return Task.CompletedTask;
        return WithDbAsync(async db =>
        {
            var rows = await db.UserPushTokens.Where(t => t.UserId == uid).ToListAsync(ct);
            db.UserPushTokens.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
        });
    }
}
