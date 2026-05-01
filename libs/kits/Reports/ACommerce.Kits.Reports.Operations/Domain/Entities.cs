using ACommerce.SharedKernel.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace ACommerce.Kits.Reports.Domain;

/// <summary>
/// عقد البلاغ الأدنى الذي يستهلكه الـ kit. التطبيق يُلصِقه على entity DB
/// (راجع <see cref="ReportEntity"/>) ويمرّره عبر <see cref="ISupportTicket"/>-style
/// مرجع interface — Law 6 في <c>CLAUDE.md</c>.
///
/// <para>الفلسفة: بلاغ على إعلان/مستخدم ≠ شكوى. لا ردود، لا محادثة، فقط
/// "أبلِغ" مع سبب من قائمة + نصّ اختياريّ. الإدارة تراجع، تُغلِق، أو
/// تتّخذ إجراءً (تجميد إعلان، حظر مستخدم، …) خارج هذا الـ kit.</para>
/// </summary>
public interface IReport
{
    string Id { get; }
    /// <summary>المُبَلِّغ (هويّة المستخدم).</summary>
    string ReporterId { get; }
    /// <summary>نوع الكيان: <c>"listing"</c>، <c>"user"</c>، <c>"message"</c>، …</summary>
    string EntityType { get; }
    /// <summary>معرّف الكيان المُبَلَّغ عنه.</summary>
    string EntityId { get; }
    /// <summary>سبب من قائمة <see cref="ReportReasons"/> الثابتة.</summary>
    string Reason { get; }
    /// <summary>وصف اختياريّ من المستخدم — حدّ أقصى 1000 حرف.</summary>
    string? Body { get; }
    /// <summary>open | reviewing | resolved | dismissed</summary>
    string Status { get; }
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
}

/// <summary>
/// كيان قاعدة بيانات البلاغ — يُنفِّذ <see cref="IReport"/>.
/// </summary>
public class ReportEntity : IBaseEntity, IReport
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ReporterId { get; set; }
    [MaxLength(40)]   public string EntityType { get; set; } = "";
    [MaxLength(100)]  public string EntityId   { get; set; } = "";
    [MaxLength(40)]   public string Reason     { get; set; } = "";
    [MaxLength(1000)] public string? Body      { get; set; }
    [MaxLength(20)]   public string Status     { get; set; } = "open";

    string IReport.Id         => Id.ToString();
    string IReport.ReporterId => ReporterId.ToString();
}

/// <summary>
/// قائمة الأسباب المعتبَرة. ثابتة على مستوى المنصّة — بلاغ بسبب خارج هذه
/// القائمة يُرفَض في الـ Analyzer. لو احتجت تخصيصاً أعلِن enum-style على
/// مستوى التطبيق وأضف Validator مخصَّص.
/// </summary>
public static class ReportReasons
{
    public const string Spam              = "spam";
    public const string Scam              = "scam";
    public const string InappropriateImages = "inappropriate_images";
    public const string WrongInfo         = "wrong_info";
    public const string Duplicate         = "duplicate";
    public const string Offensive         = "offensive";
    public const string Other             = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Spam, Scam, InappropriateImages, WrongInfo, Duplicate, Offensive, Other,
    };

    public static bool IsValid(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && All.Contains(reason);
}
