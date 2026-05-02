using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Notification.Providers.Firebase.Options;
using ACommerce.Notification.Providers.Firebase.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.ServiceHost;

public static class FirebaseModule
{
    /// <summary>
    /// يُسجِّل Firebase FCM channel <i>فقط</i> لو الـ creds موجودة في
    /// <c>Notifications:Firebase</c> section. يحلّ المسارات النسبيّة لـ
    /// <c>CredentialsFilePath</c> على <c>ContentRootPath</c> (يلزم لـ IIS
    /// حيث الـ CWD يكون system32).
    ///
    /// <para>Optional <typeparamref name="TDeviceTokenStore"/>: لو وُجد،
    /// يُسجَّل كـ <c>IDeviceTokenStore</c> قبل الكيت ليتجاوز الـ in-memory
    /// الافتراضيّ. apps بدون store دائم تتركه null.</para>
    /// </summary>
    public static ServiceHostBuilder UseFirebaseIfConfigured<TDeviceTokenStore>(this ServiceHostBuilder host)
        where TDeviceTokenStore : class, IDeviceTokenStore
    {
        var fbCfg = host.Builder.Configuration.GetSection(FirebaseOptions.SectionName);
        var credPath = fbCfg["CredentialsFilePath"];

        if (!string.IsNullOrWhiteSpace(credPath) && !Path.IsPathRooted(credPath))
        {
            var abs = Path.Combine(host.Builder.Environment.ContentRootPath, credPath);
            if (File.Exists(abs)) { fbCfg["CredentialsFilePath"] = abs; credPath = abs; }
            else                  { credPath = null; }
        }

        var hasCreds = !string.IsNullOrWhiteSpace(fbCfg["CredentialsJson"])
                    || !string.IsNullOrWhiteSpace(credPath);
        if (!hasCreds) return host;

        host.Builder.Services.AddSingleton<IDeviceTokenStore, TDeviceTokenStore>();
        host.Builder.Services.AddFirebaseNotificationChannel(host.Builder.Configuration);
        return host;
    }

    /// <summary>نسخة بدون device-token store مخصّص — تستعمل الـ in-memory الافتراضيّ.</summary>
    public static ServiceHostBuilder UseFirebaseIfConfigured(this ServiceHostBuilder host)
    {
        var fbCfg = host.Builder.Configuration.GetSection(FirebaseOptions.SectionName);
        var credPath = fbCfg["CredentialsFilePath"];

        if (!string.IsNullOrWhiteSpace(credPath) && !Path.IsPathRooted(credPath))
        {
            var abs = Path.Combine(host.Builder.Environment.ContentRootPath, credPath);
            if (File.Exists(abs)) { fbCfg["CredentialsFilePath"] = abs; credPath = abs; }
            else                  { credPath = null; }
        }

        var hasCreds = !string.IsNullOrWhiteSpace(fbCfg["CredentialsJson"])
                    || !string.IsNullOrWhiteSpace(credPath);
        if (hasCreds)
            host.Builder.Services.AddFirebaseNotificationChannel(host.Builder.Configuration);

        return host;
    }
}
