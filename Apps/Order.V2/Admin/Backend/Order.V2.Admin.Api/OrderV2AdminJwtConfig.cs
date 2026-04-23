namespace Order.V2.Admin.Api;

public record OrderV2AdminJwtConfig(string Issuer, string Audience, string SecretKey);
