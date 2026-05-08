using ACommerce.Templates.Customer.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Templates.Customer.Marketplace;

/// <summary>
/// نُقطة دَخول قالَب Customer Marketplace. يَدلّع لـ Customer.Ledger
/// (وراثَة قالَب) فيَأخُذ كلّ pages + bindings + auth، ثُمّ يَعتَمِد التَطبيق
/// عَلى MarketplaceClasses + branding.css لِيَتَخَصَّص بَصَريّاً. أيّ صَفحَة
/// Marketplace-specific تَفتَرِق عَن Ledger structurally تَدخُل عَبر
/// <see cref="CustomerMarketplaceOptions.ExtraPages"/> أو override في
/// CustomerLedgerOptions.
/// </summary>
public static class CustomerMarketplaceExtensions
{
    public static IServiceCollection AddTemplate_Customer_Marketplace(
        this IServiceCollection services,
        Action<CustomerMarketplaceOptions> configure)
    {
        var opts = new CustomerMarketplaceOptions();
        configure(opts);

        services.AddTemplate_Customer_Ledger(o =>
        {
            o.HttpClientName = opts.HttpClientName;
            o.StorageKey     = opts.StorageKey;
            o.Scheme         = opts.Scheme;
            o.RegisterAuth   = opts.RegisterAuth;
            foreach (var host in opts.UrlAllowlist) o.UrlAllowlist.Add(host);
            foreach (var page in opts.ExtraPages)   o.ExtraPages.Add(page);
            foreach (var ex in opts.ExcludedRoutes) o.ExcludedRoutes.Add(ex);
            foreach (var kv in opts.StoreOverrides) o.StoreOverrides[kv.Key] = kv.Value;
        });

        return services;
    }
}

public sealed class CustomerMarketplaceOptions
{
    public string HttpClientName { get; set; } = "";
    public string StorageKey     { get; set; } = "";
    public string Scheme         { get; set; } = "";
    public bool   RegisterAuth   { get; set; } = true;
    public List<string> UrlAllowlist { get; } = new();
    public List<(string Route, Type Component, bool RequiresAuth)> ExtraPages { get; } = new();
    public HashSet<string> ExcludedRoutes { get; } = new();
    public Dictionary<string, Type> StoreOverrides { get; } = new();
}
