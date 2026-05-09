namespace ACommerce.Notification.Providers.Email.Options;

/// <summary>SMTP configuration for the email notification channel.</summary>
public class EmailOptions
{
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@acommerce.app";
    public string FromName { get; set; } = "ACommerce";
    public bool UseSsl { get; set; } = true;
}
