using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Wire;
using Order.V2.Vendor.Web.Store;
using System.Text.Json;

namespace Order.V2.Vendor.Web.Interpreters;

public class AuthInterpreter : IOperationInterpreter<AppStore>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool CanInterpret(OperationDescriptor op) =>
        op.Type is "auth.sms.request" or "auth.sms.verify"
                or "auth.vendor.sms.request" or "auth.vendor.sms.verify"
                or "auth.sign_out";

    public Task InterpretAsync(OperationDescriptor op, object? data, AppStore store, CancellationToken ct)
    {
        if (op.Type == "auth.sign_out")
        {
            store.Auth.UserId = null; store.Auth.VendorId = null;
            store.Auth.VendorName = null; store.Auth.FullName = null;
            store.Auth.PhoneNumber = null; store.Auth.AccessToken = null;
            store.Auth.ChallengeId = null; store.Auth.PendingUserId = null;
            store.NotifyChanged();
            return Task.CompletedTask;
        }

        if (data is null) return Task.CompletedTask;
        var json = data is JsonElement je ? je : JsonSerializer.SerializeToElement(data, _json);

        switch (op.Type)
        {
            case "auth.sms.request":
            case "auth.vendor.sms.request":
                if (json.TryGetProperty("userId", out var uid))
                    store.Auth.PendingUserId = Guid.Parse(uid.GetString()!);
                if (json.TryGetProperty("challengeId", out var cid))
                    store.Auth.ChallengeId = cid.GetString();
                break;

            case "auth.sms.verify":
            case "auth.vendor.sms.verify":
                if (json.TryGetProperty("userId", out var id))
                    store.Auth.UserId = Guid.Parse(id.GetString()!);
                if (json.TryGetProperty("vendorId", out var vid))
                    store.Auth.VendorId = Guid.Parse(vid.GetString()!);
                if (json.TryGetProperty("vendorName", out var vn))
                    store.Auth.VendorName = vn.GetString();
                if (json.TryGetProperty("phoneNumber", out var ph))
                    store.Auth.PhoneNumber = ph.GetString();
                if (json.TryGetProperty("fullName", out var fn))
                    store.Auth.FullName = fn.GetString();
                if (json.TryGetProperty("accessToken", out var tk))
                    store.Auth.AccessToken = tk.GetString();
                store.Auth.ChallengeId = null;
                store.Auth.PendingUserId = null;
                break;
        }

        store.NotifyChanged();
        return Task.CompletedTask;
    }
}
