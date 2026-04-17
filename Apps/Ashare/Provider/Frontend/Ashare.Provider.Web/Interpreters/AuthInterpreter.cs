using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Ashare.Provider.Web.Store;
using System.Text.Json;

namespace Ashare.Provider.Web.Interpreters;

public class AuthInterpreter : IOperationInterpreter<AppStore>
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "auth.sms.request" or "auth.sms.verify" or "auth.sign_out";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        if (op.Type == "auth.sign_out")
        {
            store.Auth.UserId = null;
            store.Auth.PhoneNumber = null;
            store.Auth.DisplayName = null;
            store.Auth.AccessToken = null;
            store.Auth.ChallengeId = null;
            store.Auth.PendingUserId = null;
            store.NotifyChanged();
            return Task.CompletedTask;
        }

        if (data == null) return Task.CompletedTask;

        var json = data is JsonElement je ? je : JsonSerializer.SerializeToElement(data, CamelCase);

        switch (op.Type)
        {
            case "auth.sms.request":
                if (json.TryGetProperty("userId", out var uid))
                    store.Auth.PendingUserId = Guid.Parse(uid.GetString()!);
                if (json.TryGetProperty("challengeId", out var cid))
                    store.Auth.ChallengeId = cid.GetString();
                break;

            case "auth.sms.verify":
                if (json.TryGetProperty("userId", out var userId))
                    store.Auth.UserId = Guid.Parse(userId.GetString()!);
                if (json.TryGetProperty("phoneNumber", out var phone))
                    store.Auth.PhoneNumber = phone.GetString();
                if (json.TryGetProperty("fullName", out var name))
                    store.Auth.DisplayName = name.GetString();
                if (json.TryGetProperty("accessToken", out var token))
                    store.Auth.AccessToken = token.GetString();
                store.Auth.ChallengeId = null;
                store.Auth.PendingUserId = null;
                break;
        }

        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
