using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Order.Web.Store;
using System.Text.Json;

namespace Order.Web.Interpreters;

/// <summary>
/// مُفسّر المصادقة — يحوّل عمليات auth.sms.* إلى تحديثات على AppStore.Auth.
/// </summary>
public class AuthInterpreter : IOperationInterpreter<AppStore>
{
    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "auth.sms.request" or "auth.sms.verify";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        if (data == null) return Task.CompletedTask;

        var json = data is JsonElement je ? je : JsonSerializer.SerializeToElement(data);

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
                    store.Auth.FullName = name.GetString();
                if (json.TryGetProperty("accessToken", out var token))
                    store.Auth.AccessToken = token.GetString();
                // Clear OTP flow state
                store.Auth.ChallengeId = null;
                store.Auth.PendingUserId = null;
                break;
        }

        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
