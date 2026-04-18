using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Web.Store;

namespace Ashare.V2.Web.Interpreters;

/// <summary>
/// مُفسّر المصادقة — يستقبل نتيجة auth.nafath.start من الخادم،
/// أو يُفرَّغ AppStore.Auth عند auth.sign_out (عمليّة محلّية).
/// </summary>
public sealed class AuthInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "auth.nafath.start" or "auth.sign_out";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        switch (op.Type)
        {
            case "auth.sign_out":
                store.Auth.UserId = null;
                store.Auth.AccessToken = null;
                store.Auth.FullName = null;
                store.Auth.Phone = null;
                store.NotifyChanged();
                break;

            case "auth.nafath.start":
                // الردّ من الخادم: { randomNumber, challengeId }.
                // في محاكاتنا الحاليّة، المستخدم يرى الرقم وعداد 120 ثانية. نفعّل الجلسة
                // بعد اكتمال المحاكاة (تمرّ عبر الصفحة وليس عبر interpreter).
                break;
        }
        return Task.CompletedTask;
    }
}
