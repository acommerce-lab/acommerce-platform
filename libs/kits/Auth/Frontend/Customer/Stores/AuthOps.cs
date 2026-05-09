using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

/// <summary>مَصنَع عَمَليّات Auth kit — كلّ سُلوك قَيد محاسبيّ.</summary>
public static class AuthOps
{
    public static Operation RequestOtp(string phone) => Entry
        .Create("auth.otp.request")
        .From("User:current",      1, ("role", "challenger"))
        .To("Server:auth-otp",     1, ("role", "issuer"))
        .Tag("phone", phone)
        .Build();

    public static Operation VerifyOtp(string phone, string code) => Entry
        .Create("auth.otp.verify")
        .From("User:current",      1, ("role", "claimant"))
        .To("Server:auth-otp",     1, ("role", "verifier"))
        .Tag("phone", phone)
        .Tag("code",  code)
        .Build();

    public static Operation SignOut() => Entry
        .Create("auth.sign_out")
        .From("User:current",      1, ("role", "leaver"))
        .To("Server:auth",         1, ("role", "session"))
        .Build();
}
