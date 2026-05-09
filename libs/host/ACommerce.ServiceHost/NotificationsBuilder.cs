using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Notification.Providers.Firebase.Options;
using ACommerce.Notification.Providers.Firebase.Storage;
using ACommerce.Notification.Providers.InApp.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ACommerce.ServiceHost;

/// <summary>
/// builder قنوات الإشعارات. الإستخدام:
/// <code>
/// kits.AddNotifications&lt;EjarStore&gt;(notif =&gt; notif
///     .UseInAppProvider()                              // SignalR push
///     .UseFirebaseProvider&lt;EjarDeviceTokenStore&gt;());   // FCM (web + mobile)
/// </code>
///
/// <para>كلّ <c>UseXxxProvider</c> مستقلّ — يمكن استعمال واحد، اثنَين، أو
/// كلّها معاً. التطبيق يَختار حسب البيئة. provider الذي creds غير مكوَّنة
/// يَتخطّى نفسه بصمت بدل تَفجير startup.</para>
/// </summary>
public sealed class NotificationsBuilder
{
    public IServiceCollection Services      { get; }
    public IConfiguration     Configuration { get; }
    public IHostEnvironment?  Environment   { get; }

    internal NotificationsBuilder(IServiceCollection s, IConfiguration c, IHostEnvironment? env)
    {
        Services = s; Configuration = c; Environment = env;
    }

    /// <summary>
    /// in-app SignalR channel — يَبثّ على نفس الـ hub الذي يستهلكه Chat.
    /// لا يَتطلّب providers خارجيّة. يَعمل عند فتح التطبيق فقط (لا
    /// background notifications).
    /// </summary>
    public NotificationsBuilder UseInAppProvider()
    {
        Services.AddInAppNotificationChannel();
        return this;
    }

    /// <summary>
    /// Firebase Cloud Messaging — تسليم لخلف الـ tab + الجوّال المغلق.
    /// يَقرأ <c>Notifications:Firebase</c> section من appsettings:
    /// <code>
    /// "Notifications": {
    ///   "Firebase": {
    ///     "CredentialsFilePath": "Secrets/firebase-service-account.json",
    ///     "ProjectId": "...",
    ///     "RemoveInvalidTokens": true
    ///   }
    /// }
    /// </code>
    ///
    /// <para>يَحلّ المسار النسبيّ على <c>ContentRootPath</c> (لـ IIS/runasp.net
    /// حيث الـ CWD يكون <c>system32</c>). لو الـ creds غير مكوَّنة (لا
    /// <c>CredentialsFilePath</c> ولا <c>CredentialsJson</c>)، يَتخطّى بصمت
    /// — مفيد لـ dev بلا حساب Firebase حقيقيّ.</para>
    ///
    /// <para><typeparamref name="TDeviceTokenStore"/>: مخزن DB-backed لـ
    /// رموز الأجهزة. التطبيق يَكتبه. لو مُحذَف type-param: in-memory
    /// store يُستخدَم.</para>
    /// </summary>
    public NotificationsBuilder UseFirebaseProvider<TDeviceTokenStore>(string sectionName = "Notifications:Firebase")
        where TDeviceTokenStore : class, IDeviceTokenStore
    {
        if (TryResolveFirebaseCreds(sectionName))
        {
            Services.AddSingleton<IDeviceTokenStore, TDeviceTokenStore>();
            Services.AddFirebaseNotificationChannel(Configuration);
        }
        return this;
    }

    /// <summary>Firebase بدون device-token store مخصّص (in-memory).</summary>
    public NotificationsBuilder UseFirebaseProvider(string sectionName = "Notifications:Firebase")
    {
        if (TryResolveFirebaseCreds(sectionName))
            Services.AddFirebaseNotificationChannel(Configuration);
        return this;
    }

    /// <summary>
    /// يَحلّ مسار creds النسبيّ + يُجيب هل الـ creds موجودة.
    /// يُحدِّث <c>fbCfg["CredentialsFilePath"]</c> in-place ليَستهلكه
    /// <c>AddFirebaseNotificationChannel</c> لاحقاً.
    /// </summary>
    private bool TryResolveFirebaseCreds(string sectionName)
    {
        var fb = Configuration.GetSection(sectionName);
        var path = fb["CredentialsFilePath"];

        if (!string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && Environment is not null)
        {
            var abs = Path.Combine(Environment.ContentRootPath, path);
            if (File.Exists(abs)) { fb["CredentialsFilePath"] = abs; path = abs; }
            else                  { path = null; }
        }

        return !string.IsNullOrWhiteSpace(fb["CredentialsJson"])
            || !string.IsNullOrWhiteSpace(path);
    }
}
