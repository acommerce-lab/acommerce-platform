using ACommerce.Client.Http;

namespace Ashare.V2.Web.Operations;

/// <summary>
/// سجلّ ربط نوع العملية ← مسار HTTP. حالياً فارغ (Home slice قراءة فقط).
/// سيمتلئ مع الشرائح التالية (catalog.search → POST /home/search، إلخ).
/// </summary>
public static class V2Routes
{
    public static void Register(HttpRouteRegistry registry)
    {
        // catalog.search → GET /home/search  (future slice)
        // category.select → GET /categories/{id}  (future slice)
        // listing.favorite → POST /listings/{id}/favorite  (future slice)
    }
}
