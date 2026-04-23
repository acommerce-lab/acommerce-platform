using System.Text.Json;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.V2.Admin.Web.Store;

namespace Ashare.V2.Admin.Web.Interpreters;

public class AuthInterpreter : IOperationInterpreter<AppStore>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "auth.admin.sms.request" or "auth.admin.sms.verify" or "auth.sign_out";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        if (op.Type == "auth.sign_out")
        {
            store.Auth.UserId = null; store.Auth.FullName = null;
            store.Auth.PhoneNumber = null; store.Auth.AccessToken = null;
            store.Auth.ChallengeId = null; store.Auth.PendingUserId = null;
            store.NotifyChanged();
            return Task.CompletedTask;
        }

        if (data == null) return Task.CompletedTask;
        var json = data is JsonElement je ? je : JsonSerializer.SerializeToElement(data, _json);

        switch (op.Type)
        {
            case "auth.admin.sms.request":
                if (json.TryGetProperty("userId",      out var uid)) store.Auth.PendingUserId = Guid.Parse(uid.GetString()!);
                if (json.TryGetProperty("challengeId", out var cid)) store.Auth.ChallengeId   = cid.GetString();
                break;

            case "auth.admin.sms.verify":
                if (json.TryGetProperty("userId",      out var vuid))  store.Auth.UserId      = Guid.Parse(vuid.GetString()!);
                if (json.TryGetProperty("fullName",    out var name))  store.Auth.FullName    = name.GetString();
                if (json.TryGetProperty("phoneNumber", out var phone)) store.Auth.PhoneNumber = phone.GetString();
                if (json.TryGetProperty("accessToken", out var tok))   store.Auth.AccessToken = tok.GetString();
                store.Auth.ChallengeId = null; store.Auth.PendingUserId = null;
                break;
        }

        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
