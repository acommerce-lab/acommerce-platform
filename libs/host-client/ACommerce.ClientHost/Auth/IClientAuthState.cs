namespace ACommerce.ClientHost.Auth;

/// <summary>
/// حالة المُصادَقة الـ scoped على العميل: JWT + هويّة المُستَخدِم. تَطبيقات
/// مختلفة تَستعمل نَفس الـ ClientAuthState — ما يَتَغَيَّر هو
/// <see cref="LocalStorageClientAuthPersistence"/> key + اسم scheme المُصادَقة
/// + اسم HttpClient، ويَتم حَقنها عند التَسجيل.
/// </summary>
public interface IClientAuthState
{
    Guid?    UserId      { get; set; }
    string?  FullName    { get; set; }
    string?  Phone       { get; set; }
    string?  AccessToken { get; set; }
    string?  Role        { get; set; }
    bool     IsAuthenticated { get; }
    event Action? OnChanged;
    void NotifyChanged();
}

/// <summary>تَنفيذ افتراضيّ POCO لـ <see cref="IClientAuthState"/>.</summary>
public sealed class ClientAuthState : IClientAuthState
{
    public Guid?   UserId      { get; set; }
    public string? FullName    { get; set; }
    public string? Phone       { get; set; }
    public string? AccessToken { get; set; }
    public string? Role        { get; set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && UserId is not null;
    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();
}
