namespace Ashare.V2.Web.Store;

/// <summary>
/// ProviderContract: مصدر الترجمات. عقد يسمح بتبديل المصدر بلا لمس صفحة واحدة.
///
/// المحتوى الحاليّ محليّ في <see cref="EmbeddedTranslationProvider"/>.
/// التبديل إلى <c>ApiTranslationProvider</c> (يحمل من CMS/Resx) لاحقاً يتمّ
/// بسطر واحد في Program.cs.
///
/// لماذا Contract لا Service؟ لأنّ المصدر جوهره خارجيّ (قد يكون CMS/JSON/API)
/// حتى لو كان التطبيق الحالي يُضمّنه بصيغة ثابتة.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>يرجع النصّ للمفتاح حسب اللغة. لو لم يوجد، يرجع المفتاح نفسه.</summary>
    string Translate(string key, string language);
}
