using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Provider.Web.Store;
using System.Text.Json;

namespace Ashare.V2.Provider.Web.Interpreters;

public class AuthInterpreter : IOperationInterpreter<AppStore>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "auth.nafath.start" or "auth.sign_out";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        if (op.Type == "auth.sign_out")
        {
            store.Auth.UserId      = null;
            store.Auth.FullName    = null;
            store.Auth.NationalId  = null;
            store.Auth.AccessToken = null;
            store.NotifyChanged();
        }
        return Task.CompletedTask;
    }
}
