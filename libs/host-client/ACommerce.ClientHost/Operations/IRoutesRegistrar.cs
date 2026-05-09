using ACommerce.Client.Http;

namespace ACommerce.ClientHost.Operations;

/// <summary>
/// عَقد لِكلّ kit (أَو composition) لِتَسجيل HTTP routes الخاصّة بِها.
/// التَطبيق يُسَجِّل تَنفيذات IRoutesRegistrar عَبر <c>Add&lt;Kit&gt;Routes</c>،
/// و <see cref="ClientOpEngineExtensions.AddClientOpEngine"/> يَجمَعها
/// كلّها في <see cref="HttpRouteRegistry"/> singleton عِند البِناء.
/// </summary>
public interface IRoutesRegistrar
{
    void Register(HttpRouteRegistry routes);
}
