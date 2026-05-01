namespace ACommerce.Kits.Support.Backend;

/// <summary>
/// إعدادات Support kit. التطبيق يضبطها عند <c>AddSupportKit</c>:
/// <code>
/// builder.Services.AddSupportKit&lt;EjarSupportStore&gt;(opts =>
/// {
///     opts.AgentPoolPartyId = Guid.Parse("...support-pool-id...");
///     opts.AgentPoolDisplayName = "فريق دعم إيجار";
///     opts.MaxBodyLength = 4000;
///     opts.PartyKind = "User";
/// });
/// </code>
/// </summary>
public sealed class SupportKitOptions
{
    /// <summary>
    /// <c>PartyKind</c> الذي يستخدمه التطبيق في OAM لرموز الأطراف. يجب أن
    /// يطابق ما تستعمله Chat kit (<c>"User"</c> في إيجار حالياً) ليسجَّل
    /// المستخدم تحت نفس Hash من قبل المعترض الموحَّد.
    /// </summary>
    public string PartyKind { get; set; } = "User";

    /// <summary>
    /// معرّف "حساب جماعيّ" للدعم (pool). كلّ تذكرة جديدة تُنشِئ Conversation
    /// مع <c>PartnerId = AgentPoolPartyId</c> (بدلاً من وكيل بعينه)، حتى
    /// يُخصَّص لاحقاً عبر <c>AssignAgent</c>. لو null، التطبيق يُحاول
    /// قراءته من <c>Configuration["Support:AgentPoolId"]</c>.
    /// </summary>
    public Guid? AgentPoolPartyId { get; set; }

    /// <summary>اسم الـ pool الظاهر للمستخدم — يدخل في <c>Conversation.PartnerName</c>.</summary>
    public string AgentPoolDisplayName { get; set; } = "فريق الدعم";

    /// <summary>الحدّ الأقصى لطول جسم الرسالة (الجسم الأوّل أو الردّ).</summary>
    public int MaxBodyLength { get; set; } = 4000;

    /// <summary>الحدّ الأقصى لطول الموضوع.</summary>
    public int MaxSubjectLength { get; set; } = 200;
}
