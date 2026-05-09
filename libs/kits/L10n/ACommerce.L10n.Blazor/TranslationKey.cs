namespace ACommerce.L10n.Blazor;

/// <summary>
/// مِفتاح تَرجَمَة مُكَتَّب — value type يَلتَفّ حَول string. مُتَّسِق مَع
/// <c>OperationType</c> + <c>TagKey</c> في core (TypedValues.cs) ⇒ نَمَط
/// مُوَحَّد عَبر المَنصَّة لِلـ "مَفاتيح المُكَتَّبَة".
///
/// <para><b>كَيف يُستَخدَم</b>: لا يُكتَب يَدويّاً. <see cref="ACommerce.L10n.SourceGenerator"/>
/// يَقرأ كلّ <c>.resx</c> في المَشروع ويُوَلِّد <c>partial class</c> مُرافِق
/// يَحوي ثوابِت <see cref="TranslationKey"/> — واحِدَة لِكلّ مِفتاح. الصَفحَة
/// تَكتُب <c>L[Strings.HomeTitle]</c> فيُعطي compile-time safety + autocomplete
/// + F12 + refactor-rename. Typo ⇒ compile error.</para>
///
/// <para><b>التَوافُق الرَجعيّ</b>: <c>L[string]</c> ما زال يَعمَل لِلأَكواد
/// القَديمَة. الـ implicit conversion إلى <c>string</c> يَجعل الـ
/// TranslationKey قابِلَة لِلتَمرير أيّ مَكان يَستَقبِل string.</para>
/// </summary>
public sealed record TranslationKey(string Key)
{
    public override string ToString() => Key;
    public static implicit operator string(TranslationKey k) => k.Key;
}
