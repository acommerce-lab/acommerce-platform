using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Ashare.Admin.Api.Configuration;

/// <summary>
/// Configuration source يستبدل أنماط ${VAR_NAME} بقيم متغيرات البيئة.
/// مثال: "ConnectionString": "${ASHARE_ADMIN_DB_CONNECTION}" → القيمة الفعلية من الـ env.
/// إذا لم يكن المتغير موجوداً يبقى النص الأصلي (لتسهيل اكتشاف الإعدادات الناقصة).
/// </summary>
public class EnvironmentVariableSubstitutionSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider();

    private class Provider : ConfigurationProvider
    {
        private static readonly Regex Pattern = new(@"\$\{([A-Z0-9_]+)\}", RegexOptions.Compiled);

        public override void Load() { /* no-op - يعمل بعد بقية المصادر */ }
    }

    /// <summary>
    /// يطبّق الاستبدال على كل القيم النصية في الـ IConfigurationRoot الحالي.
    /// </summary>
    public static void ApplyToConfiguration(IConfigurationRoot config)
    {
        var pattern = new Regex(@"\$\{([A-Z0-9_]+)\}", RegexOptions.Compiled);

        void Walk(IConfigurationSection section)
        {
            foreach (var child in section.GetChildren())
            {
                if (child.Value != null && pattern.IsMatch(child.Value))
                {
                    var replaced = pattern.Replace(child.Value, m =>
                    {
                        var name = m.Groups[1].Value;
                        var value = Environment.GetEnvironmentVariable(name);
                        return value ?? m.Value; // اترك ${VAR} كما هي إن لم توجد
                    });
                    config[child.Path] = replaced;
                }
                Walk(child);
            }
        }

        foreach (var top in config.GetChildren())
            Walk(top);
    }
}
