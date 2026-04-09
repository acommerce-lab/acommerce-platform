using System.Collections.Concurrent;

namespace ACommerce.SharedKernel.Abstractions.Entities;

public static class EntityDiscoveryRegistry
{
    private static readonly ConcurrentBag<Type> _registeredTypes = [];

    public static void RegisterEntity<TEntity>() where TEntity : class, IBaseEntity
    {
        if (!_registeredTypes.Contains(typeof(TEntity)))
        {
            _registeredTypes.Add(typeof(TEntity));
        }
    }

    public static void RegisterEntity(Type entityType)
    {
        if (typeof(IBaseEntity).IsAssignableFrom(entityType) && !_registeredTypes.Contains(entityType))
        {
            _registeredTypes.Add(entityType);
        }
    }

    public static IEnumerable<Type> GetRegisteredTypes() => _registeredTypes.Distinct();

    public static void Clear() => _registeredTypes.Clear();
}
