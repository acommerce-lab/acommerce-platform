using Microsoft.AspNetCore.SignalR;

namespace Ejar.Api.Realtime;

/// <summary>
/// مزوّد UserId المخصّص لـ SignalR. الافتراضيّ يقرأ
/// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>، لكنّ
/// JWT bearer لإيجار يضع <c>MapInboundClaims = false</c> ليُبقي اسم
/// claim الـ <c>user_id</c> كما هو، فيحرم default IUserIdProvider من
/// أيّ NameIdentifier ويعود <c>Context.UserIdentifier = null</c>.
///
/// <para>أثر ذلك: <see cref="ACommerce.Realtime.Operations.Abstractions.IConnectionTracker"/>
/// لا يربط user → connection، فأيّ <c>SendToUserAsync</c> أو
/// <c>SendToGroupAsync</c> ينتهي صامتاً ولا يصل المستخدم. كلّ مكتبة
/// realtime كانت "زينة" لأنّ الجسر هذا مكسور.</para>
///
/// <para>هذا التطبيق يفضّل <c>user_id</c> ثمّ يقع على <c>sub</c>
/// (احتياط لو تغيّرت سياسة المطابقة لاحقاً).</para>
/// </summary>
public sealed class EjarUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("user_id")?.Value
        ?? connection.User?.FindFirst("sub")?.Value;
}
