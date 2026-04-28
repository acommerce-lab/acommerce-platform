namespace Ejar.Provider.Web.Store;

public sealed record UserCulture(string Language, string TimeZone, string Currency)
{
    public static UserCulture Default => new("ar", "Asia/Aden", "YER");
}
