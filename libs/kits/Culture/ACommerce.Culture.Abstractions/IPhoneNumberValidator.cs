namespace ACommerce.Culture.Abstractions;

/// <summary>
/// التحقّق من صحة أرقام الهاتف بعد تطبيعها.  PhoneNormalization.Normalize يوحّد
/// التنسيق، وهذا الواجهة تتحقق من أن الرقم الموحَّد فعلاً صالح (رقم دولي حقيقي).
/// </summary>
public interface IPhoneNumberValidator
{
    /// <summary>هل الرقم (في صيغة E.164 مع +) صالح كرقم هاتف دولي؟</summary>
    bool IsValid(string e164Phone, string? defaultRegion = null);
}
