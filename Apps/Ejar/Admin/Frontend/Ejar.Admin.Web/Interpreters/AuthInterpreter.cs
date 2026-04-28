using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ejar.Admin.Web.Store;

namespace Ejar.Admin.Web.Interpreters;

public sealed class AuthInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "auth.otp.verify" or "auth.sign_out";

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

            case "auth.otp.verify":
                // OTP verify response: { token, userId, name, phone }
                // Parsing is done in the Login page after DispatchAsync returns.
                break;
        }
        return Task.CompletedTask;
    }
}
