namespace ACommerce.ClientHost.Auth;

/// <summary>
/// يَحفظ ويَستعيد <see cref="IClientAuthState"/> عبر تَخزين دائم (localStorage
/// عادةً). الـ host يَستخدمه لإعادة الـ JWT من جلسة سابقة بعد reload.
/// </summary>
public interface IClientAuthPersistence
{
    Task RestoreAsync();
    Task RestoreCompleted { get; }
}
