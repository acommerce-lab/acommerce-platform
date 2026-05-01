using ACommerce.Chat.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Chat.Backend;

/// <summary>
/// نقاط النهاية الموحَّدة للدردشة. التطبيق <b>لا يكتبها</b> — يحقن مزوّد
/// <see cref="IChatStore"/> + يضيف <c>AddApplicationPart</c> لهذا التجميع
/// عبر <see cref="ChatKitExtensions.AddChatKit{TStore}"/>.
///
/// <para>التفويض role-agnostic: الـ Controller لا يفرّق بين Customer/Provider/Admin —
/// <see cref="IChatStore.CanParticipateAsync"/> هو من يقرّر. لذا يصلح Controller
/// واحد لكلّ الأدوار.</para>
///
/// <para>المسارات:
///   <c>GET  /conversations</c> — inbox للمستخدم الحاليّ.
///   <c>GET  /conversations/{id}</c> — تفاصيل + الرسائل.
///   <c>POST /conversations/{id}/messages</c> — إرسال رسالة (يبثّ realtime).
///   <c>POST /chat/{convId}/enter</c> — فتح قناة الدردشة (يكتم الإشعارات).
///   <c>POST /chat/{convId}/leave</c> — إغلاقها.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatStore _store;
    private readonly OpEngine _engine;
    private readonly IChatService? _chat;
    private readonly IConnectionTracker? _connections;
    private readonly ChatKitOptions _options;

    public ChatController(
        IChatStore store, OpEngine engine, ChatKitOptions options,
        IChatService? chat = null, IConnectionTracker? connections = null)
    {
        _store = store; _engine = engine; _options = options;
        _chat = chat; _connections = connections;
    }

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing from token");

    private string CallerPartyId => $"{_options.PartyKind}:{CallerId}";

    [HttpGet("/conversations")]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var rows = await _store.ListForUserAsync(CallerId, ct);
        return this.OkEnvelope("conversation.list", rows);
    }

    [HttpGet("/conversations/{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!await _store.CanParticipateAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");
        var conv = await _store.GetConversationAsync(id, ct);
        if (conv is null) return this.NotFoundEnvelope("conversation_not_found");
        var msgs = await _store.GetMessagesAsync(id, ct);
        return this.OkEnvelope("conversation.details", new { conversation = conv, messages = msgs });
    }

    public sealed record SendRequest(string? Text);

    [HttpPost("/conversations/{id}/messages")]
    public async Task<IActionResult> Send(string id, [FromBody] SendRequest req, CancellationToken ct)
    {
        if (!await _store.CanParticipateAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");

        // F5: القيد المحاسبيّ — استبدال السلاسل بـ typed values:
        //   - Type = "message.send" (دلالة عمل، يطابقه interceptors).
        //   - ChatTagKeys.ConversationId + ChatEntityKinds.Message على
        //     TargetEntity tag للـ observability + ربط الكيان.
        //   - Mark(IsChatMessageCreate) → interceptors البثّ/الإشعار يطابقون
        //     عبر marker لا سلاسل.
        //   - WithEntity<IChatMessage>(msg) — F1: typed entity على ctx؛
        //     interceptors تستهلكها مكتَّبة بدل بناء tags وتركيبها.
        //
        // ملاحظة الذرّيّة: <c>IChatStore.AppendMessageAsync</c> يُتَرَك
        // مسؤولاً عن SaveChanges واحد يحفظ الرسالة + تعديلات Conversation
        // معاً ذرّيّاً. لذا لا نضيف <c>DbAction=Create</c> tag — الـ
        // CrudActionInterceptor يخلق سيناريو حفظ مزدوج. كيتس بعمليّات
        // أحاديّة-الكيان (Reports.Submit مثلاً) تستعمل المسار الكامل مع
        // DbAction tag بدلاً من store يدويّ.
        IChatMessage? msg = null;
        var op = Entry.Create("message.send")
            .Describe($"User {CallerId} sends message in conversation {id}")
            .From(CallerPartyId, 1, ("role", "sender"))
            .To($"Conversation:{id}", 1, ("role", "appended"))
            .Tag(ChatTagKeys.ConversationId, id)
            .Tag(OperationTags.TargetEntity, ChatEntityKinds.Message)
            .Mark(ChatMarkers.IsChatMessageCreate)
            .Analyze(new RequiredFieldAnalyzer("text", () => req.Text))
            .Analyze(new MaxLengthAnalyzer("text",    () => req.Text, _options.MaxMessageLength))
            .Execute(async ctx =>
            {
                msg = await _store.AppendMessageAsync(id, CallerId, req.Text ?? "", ctx.CancellationToken);
                if (msg is not null) ctx.WithEntity(msg);
            })
            .SaveAtEnd()  // F6: SaveChanges واحد ذرّيّ (Message + Conversation) في hook AfterExecute
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object?)msg ?? new { }, ct);
        if (env.Operation.Status != "Success" || msg is null)
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "send_failed", env.Operation.ErrorMessage);

        // البثّ على group chat:conv:X — ChatService مسؤول عنه (لا يلامس
        // كيان البيانات). Realtime user-pin + DB notification + FCM ينطلقون
        // كـ post-interceptors تلقائياً عبر مسار composition.
        if (_chat is not null) await _chat.BroadcastNewMessageAsync(msg, ct);
        return this.OkEnvelope("message.send", msg);
    }

    [HttpPost("/chat/{convId}/enter")]
    public async Task<IActionResult> Enter(string convId, CancellationToken ct)
    {
        if (_chat is null) return this.OkEnvelope("chat.enter", new { ok = true });
        if (!await _store.CanParticipateAsync(convId, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");
        var connId = _connections is null ? null : await _connections.GetConnectionIdAsync(CallerPartyId, ct);
        if (string.IsNullOrEmpty(connId))
            return this.OkEnvelope("chat.enter", new { ok = false, reason = "no_connection" });
        await _chat.EnterConversationAsync(convId, CallerPartyId, connId, _options.ChatIdleTimeout, ct);
        return this.OkEnvelope("chat.enter", new { ok = true, conversationId = convId });
    }

    [HttpPost("/chat/{convId}/leave")]
    public async Task<IActionResult> Leave(string convId, CancellationToken ct)
    {
        if (_chat is not null) await _chat.LeaveConversationAsync(convId, CallerPartyId, ct);
        return this.OkEnvelope("chat.leave", new { ok = true, conversationId = convId });
    }
}
