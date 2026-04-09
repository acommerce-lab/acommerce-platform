using ACommerce.Favorites.Operations.Entities;
using ACommerce.Favorites.Operations.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Repositories;

namespace ACommerce.Favorites.Operations;

/// <summary>
/// واجهة المطور للمفضلات.
/// </summary>
public class FavoriteService
{
    private readonly IBaseAsyncRepository<Favorite> _repo;
    private readonly OpEngine _engine;

    public FavoriteService(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<Favorite>();
        _engine = engine;
    }

    public async Task<Guid?> AddAsync(
        Guid userId, string entityType, Guid entityId,
        string? note = null, string listName = "default", CancellationToken ct = default)
    {
        var op = FavoriteOps.Add(_repo, userId, entityType, entityId, note, listName);
        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return null;
        return result.Context!.TryGet<Guid>("favoriteId", out var id) ? id : null;
    }

    public async Task<int> RemoveAsync(
        Guid userId, string entityType, Guid entityId,
        string listName = "default", CancellationToken ct = default)
    {
        var op = FavoriteOps.Remove(_repo, userId, entityType, entityId, listName);
        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return 0;
        return result.Context!.TryGet<int>("removedCount", out var c) ? c : 0;
    }

    public async Task<bool> IsFavoriteAsync(
        Guid userId, string entityType, Guid entityId,
        string listName = "default", CancellationToken ct = default)
    {
        return await _repo.ExistsAsync(f =>
            f.UserId == userId &&
            f.EntityType == entityType &&
            f.EntityId == entityId &&
            f.ListName == listName,
            cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Favorite>> GetUserFavoritesAsync(
        Guid userId, string? entityType = null, string listName = "default", CancellationToken ct = default)
    {
        return await _repo.GetAllWithPredicateAsync(f =>
            f.UserId == userId &&
            f.ListName == listName &&
            (entityType == null || f.EntityType == entityType));
    }

    public async Task<int> CountAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        return await _repo.CountAsync(
            f => f.EntityType == entityType && f.EntityId == entityId,
            cancellationToken: ct);
    }
}
