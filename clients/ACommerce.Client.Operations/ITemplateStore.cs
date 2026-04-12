namespace ACommerce.Client.Operations;

/// <summary>
/// عقد حالة التطبيق الذي تحتاجه القوالب.
///
/// القوالب في ACommerce.Templates.* تعتمد على هذه الواجهة بدلاً من
/// AppStore المحدد لكل تطبيق — هذا ما يجعل القوالب محايدة تجاه كل تطبيق.
///
/// كل AppStore يطبّق هذه الواجهة (أو يُغلّف نفسه فيها).
/// </summary>
public interface ITemplateStore
{
    /// <summary>هل المستخدم مسجل دخوله؟</summary>
    bool IsAuthenticated { get; }

    /// <summary>معرف المستخدم الحالي. null إذا لم يسجل دخول.</summary>
    Guid? UserId { get; }

    /// <summary>رمز الوصول الحالي (JWT). null إذا لم يسجل دخول.</summary>
    string? AccessToken { get; }

    /// <summary>السمة الحالية: "light" | "dark" | "system"</summary>
    string Theme { get; }

    /// <summary>اللغة الحالية: "ar" | "en"</summary>
    string Language { get; }

    /// <summary>اختصار: هل اللغة عربية؟</summary>
    bool IsArabic => Language == "ar";

    /// <summary>حدث التغيير — ينبّه المكونات لإعادة الرسم.</summary>
    event Action? OnChanged;
}
