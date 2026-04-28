namespace ACommerce.Payments.Providers.Moyasar.Options;

/// <summary>Moyasar gateway configuration options.</summary>
public class MoyasarOptions
{
    public string SecretKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.moyasar.com/v1";
    public string CallbackUrl { get; set; } = "";
}
