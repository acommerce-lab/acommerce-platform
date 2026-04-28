using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Authentication.Operations.Analyzers;

/// <summary>
/// محلل بيانات الاعتماد - يُمرّر التحقق إلى IAuthenticator ويفشل إذا رفضه.
/// عند النجاح يضع IPrincipal في الـ context تحت مفتاح "principal".
///
/// هذا المحلل يحل محل .Validate(ctx => authenticator.AuthenticateAsync(...))
/// السابق ويوحّد منطق فحص المصادقة كمحلل قابل للتركيب والتسجيل.
/// </summary>
public class CredentialAnalyzer : IOperationAnalyzer
{
    private readonly ICredential _credential;
    private readonly IAuthenticator _authenticator;

    public string Name => $"CredentialAnalyzer({_authenticator.Name})";

    /// <summary>يراقب أي عملية فيها علامة auth.credential</summary>
    public IReadOnlyList<string> WatchedTagKeys => new[] { AuthTags.Credential.Name };

    public CredentialAnalyzer(ICredential credential, IAuthenticator authenticator)
    {
        _credential = credential;
        _authenticator = authenticator;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(OperationContext context)
    {
        try
        {
            var principal = await _authenticator.AuthenticateAsync(_credential, context.CancellationToken);
            context.Set("principal", principal);

            return new AnalyzerResult
            {
                Passed = true,
                Message = $"authenticated_via_{_authenticator.Name}",
                Data = new Dictionary<string, object>
                {
                    ["userId"] = principal.UserId,
                    ["authenticator"] = _authenticator.Name
                }
            };
        }
        catch (AuthenticationException ex)
        {
            return new AnalyzerResult
            {
                Passed = false,
                IsBlocking = true,
                Message = $"auth_failed: {ex.Reason}",
                Data = new Dictionary<string, object>
                {
                    ["reason"] = ex.Reason,
                    ["authenticator"] = _authenticator.Name
                }
            };
        }
    }
}
