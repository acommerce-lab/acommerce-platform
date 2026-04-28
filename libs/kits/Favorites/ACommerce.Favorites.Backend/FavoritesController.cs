using Microsoft.AspNetCore.Mvc;
using ACommerce.Favorites.Operations.Entities;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Favorites.Backend;

/// <summary>
/// متحكم المفضلات — يستخدم التاجات القياسية.
/// </summary>
[ApiController, Route("api/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly OpEngine _engine;

    public FavoritesController(OpEngine engine)
    {
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var op = Entry.Create("favorite.list")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(Favorite))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, ctx => {
            return ctx.Get<IReadOnlyList<Favorite>>("db_result");
        });

        return Ok(env);
    }
}
