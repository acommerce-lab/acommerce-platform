using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// نُقطَة <c>POST /conversations/start</c> لِـ V3 — نَظير
/// <c>Ejar.Api/Controllers/CatalogController.cs:StartConversation</c>. الكيت
/// Chat لا يَكشِف هذا الـ endpoint لِأَنّ السَّياسَة (مَن partner؟) خاصَّة
/// بِالتَطبيق: V3 يَستَنبِط VendorId مِن جَدول ProductListing بَدَل
/// Listings.OwnerId.
///
/// <para>الاستِجابَة: <c>{ id, created }</c> — تَتَطابَق مَع DTO
/// <c>StartConversationDto</c> في <c>DefaultChatStore</c>.</para>
///
/// <para>قَبل هذا الكونترولر كان <c>chat.conversation.start</c> يَفشَل بِـ
/// 404 (نَصّ فارِغ) ⇒ frontend يُحاوِل تَفسير الـ body فارِغاً كَـ JSON ⇒
/// "The input does not contain any JSON tokens".</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ConversationsController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public ConversationsController(AshareV3DbContext db) => _db = db;

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

        // ابحَث عَن Chat قائِم بَين الطَّرفَين (بِغَضّ النَّظَر عَن listing —
        // ChatEntity في V3 لا يَحفَظ ListingId).
        var existingChatId = await FindExistingChatAsync(callerId, partnerId, ct);
        if (existingChatId is Guid existingId)
        {
            await AppendInitialMessageIfAny(existingId, callerId, body.Text, ct);
            return this.OkEnvelope("conversation.start",
                new { id = existingId.ToString(), created = false });
        }

        // أَنشِئ Chat جَديد + ChatParticipants لِكِلا الطَّرفَين + رِسالَة بِدء.
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Title = listing.Title,
            Type = 0, // direct
        };
        _db.Chats.Add(chat);
        _db.ChatParticipants.Add(new ChatParticipantEntity
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ChatId = chat.Id, UserId = callerId, Role = 0,
        });
        _db.ChatParticipants.Add(new ChatParticipantEntity
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ChatId = chat.Id, UserId = partnerId, Role = 0,
        });
        await _db.SaveChangesAsync(ct);
        await AppendInitialMessageIfAny(chat.Id, callerId, body.Text, ct);

        return this.OkEnvelope("conversation.start",
            new { id = chat.Id.ToString(), created = true });
    }

    /// <summary>VendorId في ProductListing يُشير إلى Profile.Id؛ الـ chat يَستَخدِم Profile.UserId (AspNetUsers).</summary>
    private async Task<string> ResolveVendorUserIdAsync(Guid vendorProfileId, CancellationToken ct)
    {
        var vendor = await _db.Profiles.AsNoTracking()
            .Where(p => p.Id == vendorProfileId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(ct);
        return vendor ?? vendorProfileId.ToString();
    }

    /// <summary>
    /// محادَثَة مَوجودَة = Chat فيه كِلا UserIds في ChatParticipants.
    /// ChatEntity.Type=0 (direct) لِتَجَنُّب اقتِران group chats.
    /// </summary>
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
        _db.Messages.Add(new MessageEntity
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ChatId = chatId, SenderId = senderId, Content = text.Trim(), Type = 0,
        });
        await _db.SaveChangesAsync(ct);
    }
}
