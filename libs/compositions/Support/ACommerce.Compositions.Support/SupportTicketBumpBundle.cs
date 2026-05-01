using ACommerce.Compositions.Core;
using ACommerce.Kits.Support.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Compositions.Support;

/// <summary>
/// Bundle: عند ردّ على تذكرة (<c>op.Type == "message.send"</c> +
/// <see cref="SupportMarkers.IsTicketReply"/>)، حدِّث <c>Ticket.UpdatedAt</c>
/// في DB ليُرتَّب inbox الإدارة من الأحدث.
///
/// <para>هذا تركيب فوق <c>ChatRealtimeComposition</c>: نفس الردّ يُلتقَط
/// مرّتين — مرّة للبثّ realtime (من Chat.Realtime)، ومرّة لتحديث ميتا
/// التذكرة (من هنا). كلّ منهما interceptor مستقلّ يُطابق على tags
/// مختلفة، فلا تداخل. هذا هو نمط "تركيب فوق تركيب" في المثال العمليّ.</para>
/// </summary>
public sealed class SupportTicketBumpBundle : IInterceptorBundle
{
    public string Name => "Support.TicketBump";
    public IEnumerable<Type> InterceptorTypes => new[] { typeof(SupportTicketBumpInterceptor) };
}

public sealed class SupportTicketBumpInterceptor : IOperationInterceptor
{
    private readonly IServiceProvider _root;
    private readonly ILogger<SupportTicketBumpInterceptor> _log;

    public string Name => "Support.TicketBump";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public SupportTicketBumpInterceptor(IServiceProvider root, ILogger<SupportTicketBumpInterceptor> log)
    { _root = root; _log = log; }

    public bool AppliesTo(Operation op)
    {
        // مطابقة على Type + Marker معاً — Marker IsTicketReply يحوي
        // ("kind", "support") فيُميّز تذاكر الدعم عن دردشات عاديّة.
        if (op.Type != SupportOps.TicketReply.Name) return false;
        var kindMarker = SupportMarkers.IsTicketReply;
        return op.Tags.Any(t => t.Key == kindMarker.Key && t.Value == kindMarker.Value);
    }

    public async Task<AnalyzerResult> InterceptAsync(OperationContext ctx, OperationResult? result = null)
    {
        try
        {
            var ticketTag = ctx.Operation.Tags.FirstOrDefault(t => t.Key == SupportTagKeys.TicketId.Name);
            if (string.IsNullOrEmpty(ticketTag.Key)) return AnalyzerResult.Pass();
            var ticketId = ticketTag.Value;

            using var scope = _root.CreateScope();
            var supportStore = scope.ServiceProvider.GetService<ISupportStore>();
            if (supportStore is null) return AnalyzerResult.Pass();

            // GetAsync يكفي للتحقق من وجود التذكرة. بقاء التحديث الفعليّ في
            // EjarSupportStore.SetStatusAsync (أو app-store آخر) — هنا نتركه
            // لأنّ الـ Phase D mvp لا يحتوي حقل LastReplyAt مفصول؛ التذكرة
            // تحدَّث ضمنياً في EjarSupportStore.OpenAsync/SetStatusAsync.
            var ticket = await supportStore.GetAsync(ticketId, ctx.CancellationToken);
            if (ticket is null) return AnalyzerResult.Pass();

            _log.LogInformation("Support.TicketBump: ticket={TicketId} new reply (conv={ConvId})",
                ticketId, ticket.ConversationId);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Support.TicketBump: تجاهل خطأ غير قاتل");
        }
        return AnalyzerResult.Pass();
    }
}
