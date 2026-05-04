namespace ACommerce.ClientHost.KitApi;

/// <summary>
/// pre-flight analyzer لكل طلب kit. يُرجع <c>null</c> لَتَمرير الطلب،
/// أو رسالة خطأ لإيقافه قبل الإرسال (مثل MissingAuthAnalyzer،
/// QuotaExceededAnalyzer، RequiredFieldAnalyzer للـ body).
///
/// <para>التَطبيق يُسَجِّلها في DI ⇒ تُطَبَّق على كلّ HttpXxxApiClient في
/// كلّ الكيتس تلقائيّاً. هذا يَضمن الالتزام بالمنهجيّة (مثلاً: لا طلب
/// بدون auth header، لا طلب يَتجاوز quota، إلخ).</para>
/// </summary>
public interface IKitApiAnalyzer
{
    /// <summary>اسم الـ analyzer للـ logs والـ errors.</summary>
    string Name { get; }

    /// <summary>
    /// يُرجع <c>null</c> لو الطلب صالح، أو رسالة خطأ لإيقافه.
    /// </summary>
    Task<string?> CheckAsync(KitApiRequest request, CancellationToken ct);
}

/// <summary>
/// interceptor متقاطع حول كلّ طلب — قبل + بعد. يَفعل ما يَفعله
/// DelegatingHandler لكن على مُستوى kit (يُمكن قراءة <c>KitName</c>،
/// <c>Tags</c>، …). يُسَجَّل في DI ⇒ يَنطبق على كلّ kits.
///
/// <para>أمثلة: TelemetryInterceptor (يُسَجِّل عَدّاد لكلّ kit)،
/// RetryOn401 (يُحَدِّث JWT ويُعيد المحاولة)، CacheInterceptor
/// (يَحفظ ردود GET في memory).</para>
/// </summary>
public interface IKitApiInterceptor
{
    string Name { get; }
    Task BeforeAsync(KitApiRequest request, CancellationToken ct) => Task.CompletedTask;
    Task AfterAsync(KitApiRequest request, KitApiResponse response, CancellationToken ct) => Task.CompletedTask;
}
