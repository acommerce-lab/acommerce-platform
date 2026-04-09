using ACommerce.OperationEngine.Wire;
using Microsoft.AspNetCore.Mvc;

namespace Order.Api2.Controllers;

/// <summary>
/// مساعدات لتغليف ردود المتحكمات في OperationEnvelope بشكل موحد.
/// </summary>
public static class EnvelopeHelpers
{
    /// <summary>200 OK مع مغلف معلوماتي (read-only) - لـ GET endpoints</summary>
    public static IActionResult OkEnvelope<T>(this ControllerBase ctrl, string opType, T data)
        => ctrl.Ok(OperationEnvelopeFactory.Info(opType, data));

    /// <summary>404 NotFound مع مغلف خطأ مُهيكل</summary>
    public static IActionResult NotFoundEnvelope(this ControllerBase ctrl, string code = "not_found", string? msg = null)
        => ctrl.NotFound(OperationEnvelopeFactory.Error<object>(code, msg));

    /// <summary>400 BadRequest مع مغلف خطأ</summary>
    public static IActionResult BadRequestEnvelope(this ControllerBase ctrl, string code, string? msg = null, string? hint = null)
        => ctrl.BadRequest(OperationEnvelopeFactory.Error<object>(code, msg, hint));

    /// <summary>401 Unauthorized مع مغلف خطأ</summary>
    public static IActionResult UnauthorizedEnvelope(this ControllerBase ctrl, string code = "unauthorized", string? msg = null)
        => ctrl.Unauthorized(OperationEnvelopeFactory.Error<object>(code, msg));

    /// <summary>403 Forbidden مع مغلف خطأ</summary>
    public static IActionResult ForbiddenEnvelope(this ControllerBase ctrl, string code = "forbidden", string? msg = null)
        => ctrl.StatusCode(403, OperationEnvelopeFactory.Error<object>(code, msg));

    /// <summary>204 NoContent مع مغلف فارغ ناجح</summary>
    public static IActionResult NoContentEnvelope(this ControllerBase ctrl, string opType)
        => ctrl.Ok(OperationEnvelopeFactory.Info<object?>(opType, null));
}
