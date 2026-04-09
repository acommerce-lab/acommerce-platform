using ACommerce.Favorites.Operations.Entities;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;

namespace ACommerce.Favorites.Operations.Operations;

public static class FavoriteTags
{
    public static readonly TagKey EntityType = new("favorite_entity_type");
    public static readonly TagKey EntityId = new("favorite_entity_id");
    public static readonly TagKey ListName = new("favorite_list");
    public static readonly TagKey Action = new("favorite_action");
    public static readonly TagKey Role = new("role");
}

/// <summary>
/// قيود المفضلات.
/// Add: User (مدين) ← Entity (دائن) - تعبير عن "اهتمام".
/// Remove: عكس القيد.
/// </summary>
public static class FavoriteOps
{
    public static Operation Add(
        IBaseAsyncRepository<Favorite> repo,
        Guid userId,
        string entityType,
        Guid entityId,
        string? note = null,
        string listName = "default")
    {
        return Entry.Create("favorite.add")
            .Describe($"User:{userId} favorites {entityType}:{entityId}")
            .From($"User:{userId}", 1, (FavoriteTags.Role, "user"))
            .To($"{entityType}:{entityId}", 1, (FavoriteTags.Role, "target"))
            .Tag(FavoriteTags.EntityType, entityType)
            .Tag(FavoriteTags.EntityId, entityId.ToString())
            .Tag(FavoriteTags.ListName, listName)
            .Tag(FavoriteTags.Action, "add")
            // محلل idempotency - يفحص هل المفضلة موجودة سابقاً
            .Analyze(new PredicateAnalyzer("favorite_idempotency", async ctx =>
            {
                var existing = await repo.GetAllWithPredicateAsync(f =>
                    f.UserId == userId &&
                    f.EntityType == entityType &&
                    f.EntityId == entityId &&
                    f.ListName == listName);

                if (existing.Count > 0)
                {
                    ctx.Set("favoriteId", existing[0].Id);
                    ctx.Set("alreadyExists", true);
                }
                return AnalyzerResult.Pass();  // دائماً ينجح - الـ Execute يتعامل
            }))
            .Execute(async ctx =>
            {
                if (ctx.TryGet<bool>("alreadyExists", out var exists) && exists) return;

                var favorite = new Favorite
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    UserId = userId,
                    EntityType = entityType,
                    EntityId = entityId,
                    Note = note,
                    ListName = listName
                };
                await repo.AddAsync(favorite, ctx.CancellationToken);
                ctx.Set("favoriteId", favorite.Id);
            })
            .Build();
    }

    public static Operation Remove(
        IBaseAsyncRepository<Favorite> repo,
        Guid userId,
        string entityType,
        Guid entityId,
        string listName = "default")
    {
        return Entry.Create("favorite.remove")
            .Describe($"User:{userId} unfavorites {entityType}:{entityId}")
            .From($"{entityType}:{entityId}", 1, (FavoriteTags.Role, "target"))
            .To($"User:{userId}", 1, (FavoriteTags.Role, "user"))
            .Tag(FavoriteTags.Action, "remove")
            .Tag(FavoriteTags.ListName, listName)
            .Execute(async ctx =>
            {
                var existing = await repo.GetAllWithPredicateAsync(f =>
                    f.UserId == userId &&
                    f.EntityType == entityType &&
                    f.EntityId == entityId &&
                    f.ListName == listName);

                foreach (var f in existing)
                    await repo.SoftDeleteAsync(f.Id, ctx.CancellationToken);

                ctx.Set("removedCount", existing.Count);
            })
            .Build();
    }
}
