using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// نُقطَة <c>POST /conversations/start</c> لِـ V3. كُلّ كِتابَة هُنا تَمُرّ عَبر
/// <see cref="OpEngine"/> + <c>SaveAtEnd</c> ⇒ لا <c>SaveChangesAsync</c>
/// مُباشِر، تَدقيق كامِل، وَ idempotency.
///
/// <para>الكيت Chat لا يَكشِف هذا الـ endpoint لِأَنّ سِياسَة استِنباط
/// الـ partner (مَن مالِك الإعلان؟) خاصَّة بِالتَطبيق — V3 يَستَخدِم
/// <c>ProductListing.VendorId</c> ثُمّ يَستَنبِط <c>Profile.UserId</c>.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ConversationsController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly OpEngine          _engine;
    public ConversationsController(AshareV3DbContext db, OpEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public sealed record StartConversationBody(string? ListingId, string? PartnerId, string? Text);

    [HttpPost("/conversations/start")]
    public async Task<IActionResult> Start([FromBody] StartConversationBody body, CancellationToken ct)
    {
        var callerId = User.FindFirst("user_id")?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(callerId)) return this.UnauthorizedEnvelope();

        if (string.IsNullOrWhiteSpace(body.ListingId) || !Guid.TryParse(body.ListingId, out var listingId))
            return this.BadRequestEnvelope("missing_or_invalid_listing_id");

        var listing = await _db.ProductListings.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        // partnerId = VendorId الإعلان (V3 ProductListing) ما لَم يُمَرَّر صَراحَةً.
        string partnerId = !string.IsNullOrWhiteSpace(body.PartnerId)
            ? body.PartnerId!
            : await ResolveVendorUserIdAsync(listing.VendorId, ct);

        if (string.IsNullOrEmpty(partnerId)) return this.BadRequestEnvelope("vendor_not_found");
        if (string.Equals(partnerId, callerId, StringComparison.Ordinal))
            return this.BadRequestEnvelope("cannot_chat_with_self");

        // ابحَث عَن Chat قائِم بَين الطَّرفَين (بِغَضّ النَّظَر عَن listing).
        var existingChatId = await FindExistingChatAsync(callerId, partnerId, ct);
        if (existingChatId is Guid existingId)
        {
            await AppendInitialMessageIfAny(existingId, callerId, body.Text, ct);
            return this.OkEnvelope("conversation.start",
                new { id = existingId.ToString(), created = false });
        }

        // ─── إنشاء Chat + ChatParticipants عَبر op + SaveAtEnd ────────────
        var chatId = Guid.NewGuid();
        var op = Entry.Create("conversation.start")
            .Describe($"User {callerId} starts conversation with {partnerId} on listing {listingId}")
            .From($"User:{callerId}",      1, ("role", "initiator"))
            .To($"Conversation:{chatId}",  1, ("role", "created"))
            .Tag("listing_id", listingId.ToString())
            .Tag("partner_id", partnerId)
            .Execute(ctx =>
            {
                _db.Chats.Add(new ChatEntity
                {
                    Id        = chatId,
                    CreatedAt = DateTime.UtcNow,
                    Title     = listing.Title,
                    Type      = 0, // direct
                });
                _db.ChatParticipants.Add(new ChatParticipantEntity
                {
                    Id        = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    ChatId    = chatId,
                    UserId    = callerId,
                    Role      = 0,
                });
                _db.ChatParticipants.Add(new ChatParticipantEntity
                {
                    Id        = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    ChatId    = chatId,
                    UserId    = partnerId,
                    Role      = 0,
                });
                return Task.CompletedTask;
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id = chatId }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "start_failed",
                                           env.Operation.ErrorMessage);

        await AppendInitialMessageIfAny(chatId, callerId, body.Text, ct);
        return this.OkEnvelope("conversation.start",
            new { id = chatId.ToString(), created = true });
    }

    /// <summary>VendorId في ProductListing يُشير إلى Profile.Id؛ الـ chat يَستَخدِم Profile.UserId.</summary>
    private async Task<string> ResolveVendorUserIdAsync(Guid vendorProfileId, CancellationToken ct)
    {
        var vendor = await _db.Profiles.AsNoTracking()
            .Where(p => p.Id == vendorProfileId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(ct);
        return vendor ?? vendorProfileId.ToString();
    }

    private async Task<Guid?> FindExistingChatAsync(string userA, string userB, CancellationToken ct)
    {
        var chatIdsA = _db.ChatParticipants.AsNoTracking()
            .Where(p => p.UserId == userA).Select(p => p.ChatId);
        var chatIdsB = _db.ChatParticipants.AsNoTracking()
            .Where(p => p.UserId == userB).Select(p => p.ChatId);
        return await _db.Chats.AsNoTracking()
            .Where(c => c.Type == 0 && chatIdsA.Contains(c.Id) && chatIdsB.Contains(c.Id))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task AppendInitialMessageIfAny(Guid chatId, string senderId, string? text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var msgId = Guid.NewGuid();
        var op = Entry.Create("message.send")
            .Describe($"Initial message in conversation {chatId} from {senderId}")
            .From($"User:{senderId}",          1, ("role", "sender"))
            .To($"Conversation:{chatId}",      1, ("role", "appended"))
            .Tag("conversation_id", chatId.ToString())
            .Execute(ctx =>
            {
                _db.Messages.Add(new MessageEntity
                {
                    Id        = msgId,
                    CreatedAt = DateTime.UtcNow,
                    ChatId    = chatId,
                    SenderId  = senderId,
                    Content   = text!.Trim(),
                    Type      = 0,
                });
                return Task.CompletedTask;
            })
            .SaveAtEnd()
            .Build();
        await _engine.ExecuteEnvelopeAsync(op, new { id = msgId }, ct);
    }
}
