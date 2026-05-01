// Legacy shim — Operations.cs الأصلي كان يحوي ثوابت OAM ثلاثة بسلاسل
// مختلفة عن النمط الموحَّد. الآن المرجع الأحاديّ هو ISupportStore.cs الذي
// يعرّف SupportOperationTypes + SupportTags. نتركه فارغاً للتوافق مع
// مَن يُحيل إلى المسار القديم.
namespace ACommerce.Kits.Support.Operations;

/// <summary>توافق خلفيّ — استخدم <see cref="SupportOperationTypes"/>.</summary>
public static class SupportOperations
{
    public const string FileTicket    = SupportOperationTypes.TicketOpen;
    public const string ReplyTicket   = SupportOperationTypes.TicketReply;
    public const string ResolveTicket = SupportOperationTypes.TicketStatusChange;
}
