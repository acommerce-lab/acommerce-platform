using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// يَتَجاوَز <c>GET /conversations/{id}</c> الكيت الافتِراضي. الكيت يُرجِع
/// <see cref="ACommerce.Chat.Operations.IChatConversation"/> الَّذي يَحوي
/// <c>Id + ParticipantPartyIds</c> فَقَط ⇒ الواجِهَة (ChatRoom.razor) لا
/// تَرى اسم/صورة الطَّرَف الآخَر. هذا الكونترولر يَبني <c>ConvDto</c>
/// مُماثِل لِما يَرجِع مَن <c>/conversations</c> (قائِمَة) ⇒ نَفس wire
/// shape، تَجرِبَة مُتَّسِقَة.
///
/// <para><c>Owner</c> = المُتَّصِل، <c>Partner</c> = الطَرَف الآخَر —
/// تَقرير caller-aware يُجرى عَلى الخادم لا الواجِهَة.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ConversationDetailsController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public ConversationDetailsController(AshareV3DbContext db) => _db = db;

    private string? CallerUserId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("/conversations/{id}", Order = -10)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (CallerUserId is null) return this.UnauthorizedEnvelope();
        if (!Guid.TryParse(id, out var cid)) return this.BadRequestEnvelope("invalid_id");

        var amParticipant = await _db.ChatParticipants.AsNoTracking()
            .AnyAsync(p => p.ChatId == cid && p.UserId == CallerUserId, ct);
        if (!amParticipant) return this.ForbiddenEnvelope("not_a_participant");

        var chat = await _db.Chats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct);
        if (chat is null) return this.NotFoundEnvelope("conversation_not_found");

        var parts = await _db.ChatParticipants.AsNoTracking()
            .Where(p => p.ChatId == cid).ToListAsync(ct);
        var userIds = parts.Select(p => p.UserId).Distinct().ToList();
        var profiles = await _db.Profiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId!))
            .Select(p => new { p.UserId, p.FullName, p.BusinessName, p.Avatar })
            .ToListAsync(ct);
        var prof = profiles.Where(p => !string.IsNullOrEmpty(p.UserId))
                           .ToDictionary(p => p.UserId!, p => p);

        var mePart    = parts.FirstOrDefault(p => p.UserId == CallerUserId);
        var otherPart = parts.FirstOrDefault(p => p.UserId != CallerUserId);
        var meProf    = mePart    is null ? null : prof.GetValueOrDefault(mePart.UserId);
        var otherProf = otherPart is null ? null : prof.GetValueOrDefault(otherPart.UserId);

        var msgs = await _db.Messages.AsNoTracking()
            .Where(m => m.ChatId == cid).OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                id             = m.Id.ToString(),
                conversationId = m.ChatId.ToString(),
                senderPartyId  = "User:" + m.SenderId,
                body           = m.Content,
                sentAt         = m.CreatedAt,
                readAt         = (DateTime?)null,
            })
            .ToListAsync(ct);

        var last = msgs.LastOrDefault();
        var convDto = new
        {
            id            = chat.Id.ToString(),
            ownerId       = mePart?.UserId,
            ownerName     = meProf?.BusinessName ?? meProf?.FullName,
            ownerAvatar   = meProf?.Avatar,
            partnerId     = otherPart?.UserId,
            partnerName   = otherProf?.BusinessName ?? otherProf?.FullName,
            partnerAvatar = otherProf?.Avatar,
            subject       = chat.Title,
            listingId     = (string?)null,
            lastAt        = last is null ? (chat.UpdatedAt ?? chat.CreatedAt) : last.sentAt,
            unreadCount   = 0,
            lastMessage   = last?.body,
        };

        return this.OkEnvelope("conversation.details",
            new { conversation = convDto, messages = msgs });
    }
}
