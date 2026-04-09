using ACommerce.Authentication.Operations.Abstractions;
using Microsoft.Extensions.Logging;

namespace ACommerce.Authentication.Providers.Token;

/// <summary>
/// بيانات اعتماد: رمز (token).
/// </summary>
public record TokenCredential(string Token) : ICredential
{
    public string CredentialType => "token";
}

/// <summary>
/// واجهة التحقق من رمز.
/// المطور يُطبقها حسب نوع الرمز: JWT, opaque, reference token, etc.
/// </summary>
public interface ITokenValidator
{
    /// <summary>
    /// التحقق من صلاحية رمز وإرجاع معلومات الهوية.
    /// يرمي AuthenticationException عند الفشل.
    /// </summary>
    Task<TokenValidationResult> ValidateAsync(string token, CancellationToken ct = default);
}

/// <summary>
/// نتيجة التحقق من رمز.
/// </summary>
public record TokenValidationResult(
    string UserId,
    string? DisplayName = null,
    IReadOnlyDictionary<string, string>? Claims = null,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// مُصادق بالرموز.
/// </summary>
public class TokenAuthenticator : IAuthenticator
{
    private readonly ITokenValidator _validator;
    private readonly ILogger<TokenAuthenticator> _logger;

    public string Name => "token";
    public string SupportedCredentialType => "token";

    public TokenAuthenticator(ITokenValidator validator, ILogger<TokenAuthenticator> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IPrincipal> AuthenticateAsync(ICredential credential, CancellationToken ct = default)
    {
        if (credential is not TokenCredential tokenCred)
            throw new AuthenticationException("invalid_credential_type",
                $"Expected TokenCredential, got {credential.GetType().Name}");

        if (string.IsNullOrWhiteSpace(tokenCred.Token))
            throw new AuthenticationException("empty_token", "Token is empty");

        try
        {
            var result = await _validator.ValidateAsync(tokenCred.Token, ct);

            if (result.ExpiresAt.HasValue && result.ExpiresAt.Value <= DateTimeOffset.UtcNow)
                throw new AuthenticationException("expired", "Token has expired");

            _logger.LogDebug("[TokenAuth] Validated token for user {UserId}", result.UserId);
            return new TokenPrincipal(result);
        }
        catch (AuthenticationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenAuth] Validation error");
            throw new AuthenticationException("validation_error", ex.Message);
        }
    }
}

/// <summary>
/// هوية مُنتجة من رمز.
/// </summary>
internal class TokenPrincipal : IPrincipal
{
    public string UserId { get; }
    public string? DisplayName { get; }
    public IReadOnlyDictionary<string, string> Claims { get; }

    public TokenPrincipal(TokenValidationResult result)
    {
        UserId = result.UserId;
        DisplayName = result.DisplayName;
        Claims = result.Claims ?? new Dictionary<string, string>();
    }
}
