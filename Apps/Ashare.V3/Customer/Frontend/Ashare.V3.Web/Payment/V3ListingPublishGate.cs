using ACommerce.ClientHost.Auth;
using Ejar.Customer.UI.Services;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Ashare.V3.Web.Payment;

/// <summary>
/// V3 يَفرِض دَفعاً لِكُلّ نَشر إعلان (لا باقات اشتِراك). تَدَفُّق:
/// <list type="number">
///   <item><c>POST /payments/listing/initiate</c> ⇒ <c>{reference, paymentUrl}</c>.</item>
///   <item>الـ Mock backend يُرَقّي الحالَة إلى <c>captured</c> بَعد
///         <c>AutoCaptureSeconds</c>. polling عَلى <c>GET /…/status</c>.</item>
///   <item>عِند <c>captured</c> ⇒ نُرجِع <see cref="PublishAuthorization"/>
///         بِـ <c>Scope</c> = <see cref="PaymentRequestContext.Use"/>.</item>
/// </list>
/// الـ <c>using</c> في <c>CreateListing.Publish</c> يَضمَن أَنّ
/// <c>X-Payment-Reference</c> يَخرُج عَلى <c>POST /my-listings</c>،
/// والـ backend interceptor يَستَهلِك الدَفع.
/// </summary>
public sealed class V3ListingPublishGate : IListingPublishGate
{
    private readonly AuthenticatedHttpClient _http;
    private readonly PaymentRequestContext _ctx;
    public V3ListingPublishGate(AuthenticatedHttpClient http, PaymentRequestContext ctx)
    {
        _http = http;
        _ctx = ctx;
    }

    public async Task<PublishAuthorization> AuthorizeAsync(CancellationToken ct = default)
    {
        try
        {
            var initResp = await _http.Client.PostAsJsonAsync("/payments/listing/initiate",
                new { }, ct);
            if (!initResp.IsSuccessStatusCode)
                return new PublishAuthorization(false, $"تعذّر بدء الدَفع ({(int)initResp.StatusCode})");

            var init = await initResp.Content.ReadFromJsonAsync<InitiateEnv>(ct);
            var reference = init?.Data?.Reference;
            if (string.IsNullOrEmpty(reference))
                return new PublishAuthorization(false, "لم يَستَلِم النِظام مرجِع الدَفع");

            // Polling — Mock يَلتَقِط بَعد ~3 ثَوانٍ.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(1500, ct);
                var statusResp = await _http.Client.GetAsync(
                    $"/payments/listing/{Uri.EscapeDataString(reference)}/status", ct);
                if (!statusResp.IsSuccessStatusCode) continue;
                var status = await statusResp.Content.ReadFromJsonAsync<StatusEnv>(ct);
                var s = status?.Data?.Status;
                if (s == "captured")
                    return new PublishAuthorization(true, Scope: _ctx.Use(reference));
                if (s is "failed" or "cancelled")
                    return new PublishAuthorization(false, $"الدَفع {s}");
            }
            return new PublishAuthorization(false, "انتَهت مُهلَة انتِظار الدَفع");
        }
        catch (Exception ex)
        {
            return new PublishAuthorization(false, $"خَطأ شَبكي في الدَفع: {ex.Message}");
        }
    }

    // نَزع envelope حَدّ أَدنى. لا نُريد سَحب OpEngine لِأَجل GET بَسيط.
    private sealed record InitiateEnv([property: JsonPropertyName("data")] InitiateData? Data);
    private sealed record InitiateData([property: JsonPropertyName("reference")] string Reference);
    private sealed record StatusEnv([property: JsonPropertyName("data")] StatusData? Data);
    private sealed record StatusData([property: JsonPropertyName("status")] string Status);
}
