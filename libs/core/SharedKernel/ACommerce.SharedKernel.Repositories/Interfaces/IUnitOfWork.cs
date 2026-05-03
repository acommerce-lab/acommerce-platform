namespace ACommerce.SharedKernel.Repositories.Interfaces;

/// <summary>
/// نمط Unit-of-Work: حفظ صريح لكلّ ما تجمَّع في الـ DbContext الواحد
/// خلال هذه الـ scope. يُستعمَل من Execute body في القيد ليجمع تعديلات
/// عدّة repositories في معاملة واحدة، ومن hook AfterExecute عبر
/// <c>OperationBuilder.SaveAtEnd()</c> ليُجبِر الحفظ قبل أيّ Post-interceptor.
///
/// <para>التطبيق الافتراضيّ <c>EfUnitOfWork</c> في
/// <c>ACommerce.SharedKernel.Infrastructure.EFCores</c> يحلّ <c>DbContext</c>
/// المُسجَّل كـ Scoped في DI ويستدعي SaveChangesAsync عليه.</para>
///
/// <para>القاعدة العامّة لكلّ kit مستقبلاً:
/// <list type="number">
///   <item>Execute body يستدعي <c>repo.AddNoSaveAsync(entity)</c> لكلّ كيان.</item>
///   <item>عمليّات أخرى على tracked entities (تحديث conversation، …) تتمّ
///         in-memory فقط — لا SaveChanges يدويّ.</item>
///   <item>السطر الأخير في Execute body — أو <c>.SaveAtEnd()</c> على البناء —
///         يُنادي <c>uow.SaveChangesAsync(ct)</c> فيحفظ الكلّ معاً.</item>
///   <item>Post-interceptors تفحص <c>result.Success</c> ولا تفعل شيئاً عند
///         فشل الحفظ (transaction rollback تلقائيّ).</item>
/// </list></para>
/// </summary>
public interface IUnitOfWork
{
    /// <summary>يحفظ كلّ التغييرات المتراكمة في الـ Scope الحاليّة.
    /// يُرجع عدد الصفوف المتأثّرة.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
