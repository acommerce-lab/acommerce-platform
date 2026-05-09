namespace ACommerce.ClientHost.Security;

/// <summary>
/// عقد تنقية النصّ الغنيّ القادم من المستخدِم/الخادم قبل عرضه. يُحقَن في
/// templates التي تَعرض نصوصاً بِها HTML (وصف إعلان، رسائل دعم...).
/// التنفيذ الافتراضيّ <see cref="DefaultRichTextSanitizer"/> يُعيد text فقط —
/// لا HTML ⇒ لا XSS. التطبيقات التي تحتاج markdown تُسجِّل sanitizer أقوى.
/// </summary>
public interface IRichTextSanitizer
{
    /// <summary>نقّ <paramref name="raw"/> وأعِد سلسلة آمنة للعرض.</summary>
    string Sanitize(string? raw);
}

/// <summary>
/// التنقية الافتراضيّة: تُجرّد كلّ HTML/script وتُعيد النصّ بـ &amp; &lt; &gt; &quot;
/// مُهرَّبة. أسطر جديدة <c>\n</c> تَبقى كما هي (templates تَستعملها مع
/// <c>white-space: pre-wrap</c>). آمنة للـ <c>@text</c> render في Blazor افتراضاً.
/// </summary>
public sealed class DefaultRichTextSanitizer : IRichTextSanitizer
{
    public string Sanitize(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var sb = new System.Text.StringBuilder(raw.Length);
        var inTag = false;
        foreach (var ch in raw)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (inTag) continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
