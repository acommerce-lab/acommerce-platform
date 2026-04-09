using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Providers.Token;
using Microsoft.IdentityModel.Tokens;

namespace Ashare.Api2.Services;

/// <summary>
/// إعدادات JWT.
/// </summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "https://ashare.app";
    public string Audience { get; set; } = "ashare-api";
    public string SecretKey { get; set; } = "ChangeThisInProduction-32-chars-min!!";
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(60);
}

/// <summary>
/// مُصدر/مُتحقق JWT حقيقي - يطبق ITokenIssuer + ITokenValidator.
/// يحل محل AshareTokenStore القديم القائم على Guid.
/// </summary>
public class JwtTokenStore : ITokenIssuer, ITokenValidator
{
    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly SymmetricSecurityKey _key;
    private readonly SigningCredentials _signing;
    private readonly TokenValidationParameters _validation;

    // refreshToken (string) → userId
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _refreshTokens = new();
    // tokens المُلغاة
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _revoked = new();

    public JwtTokenStore(JwtOptions options)
    {
        _options = options;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        _signing = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

        _validation = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    // === ITokenIssuer ===

    public Task<AuthToken> IssueAsync(IPrincipal principal, CancellationToken ct = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(_options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, principal.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new("user_id", principal.UserId)
        };

        if (!string.IsNullOrEmpty(principal.DisplayName))
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, principal.DisplayName));

        foreach (var (k, v) in principal.Claims)
            claims.Add(new Claim(k, v));

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: _signing);

        var accessToken = _handler.WriteToken(jwt);
        var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _refreshTokens[refreshToken] = principal.UserId;

        return Task.FromResult(new AuthToken(accessToken, refreshToken, expires));
    }

    public Task<AuthToken> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (!_refreshTokens.TryRemove(refreshToken, out var userId))
            throw new AuthenticationException("invalid_refresh_token", "Refresh token not found");

        return IssueAsync(new SimplePrincipal(userId), ct);
    }

    public Task RevokeAsync(string token, CancellationToken ct = default)
    {
        _revoked[token] = 1;
        // إذا كان refresh token، احذفه أيضاً
        _refreshTokens.TryRemove(token, out _);
        return Task.CompletedTask;
    }

    // === ITokenValidator ===

    public Task<ACommerce.Authentication.Providers.Token.TokenValidationResult> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (_revoked.ContainsKey(token))
            throw new AuthenticationException("revoked", "Token has been revoked");

        try
        {
            var principal = _handler.ValidateToken(token, _validation, out var validatedToken);

            var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? principal.FindFirstValue("user_id")
                      ?? throw new AuthenticationException("missing_subject", "No sub claim");

            var displayName = principal.FindFirstValue(JwtRegisteredClaimNames.Name);

            var claims = principal.Claims
                .Where(c => c.Type != JwtRegisteredClaimNames.Sub
                         && c.Type != JwtRegisteredClaimNames.Jti
                         && c.Type != JwtRegisteredClaimNames.Iat
                         && c.Type != JwtRegisteredClaimNames.Exp
                         && c.Type != JwtRegisteredClaimNames.Iss
                         && c.Type != JwtRegisteredClaimNames.Aud)
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.First().Value);

            var expiresAt = ((JwtSecurityToken)validatedToken).ValidTo;

            return Task.FromResult(new ACommerce.Authentication.Providers.Token.TokenValidationResult(
                UserId: userId,
                DisplayName: displayName,
                Claims: claims,
                ExpiresAt: expiresAt));
        }
        catch (SecurityTokenExpiredException)
        {
            throw new AuthenticationException("expired", "Token has expired");
        }
        catch (SecurityTokenException ex)
        {
            throw new AuthenticationException("invalid_token", ex.Message);
        }
    }

    private class SimplePrincipal : IPrincipal
    {
        public string UserId { get; }
        public string? DisplayName => null;
        public IReadOnlyDictionary<string, string> Claims { get; } = new Dictionary<string, string>();

        public SimplePrincipal(string userId) => UserId = userId;
    }
}
