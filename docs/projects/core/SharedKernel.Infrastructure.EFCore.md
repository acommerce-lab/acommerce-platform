# ACommerce.SharedKernel.Infrastructure.EFCore

## نظرة عامة | Overview

مكتبة `ACommerce.SharedKernel.Infrastructure.EFCore` توفر التنفيذ الكامل لـ Entity Framework Core مع دعم قواعد بيانات متعددة، وإدارة المعاملات، والتدقيق التلقائي، وعزل المستأجرين، والحذف الناعم.

This library provides the complete Entity Framework Core implementation with multi-database support, transaction management, automatic auditing, tenant isolation, and soft delete functionality.

**المسار | Path:** `SharedKernel/ACommerce.SharedKernel.Infrastructure.EFCore`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- Microsoft.EntityFrameworkCore
- ACommerce.SharedKernel.Abstractions
- Npgsql.EntityFrameworkCore.PostgreSQL (Optional)
- Microsoft.EntityFrameworkCore.SqlServer (Optional)
- Microsoft.EntityFrameworkCore.Sqlite (Optional)

---

## قواعد البيانات المدعومة | Supported Databases

| قاعدة البيانات | الحزمة | حالة الدعم |
|---------------|--------|------------|
| PostgreSQL | Npgsql.EntityFrameworkCore.PostgreSQL | ✅ كامل |
| SQL Server | Microsoft.EntityFrameworkCore.SqlServer | ✅ كامل |
| SQLite | Microsoft.EntityFrameworkCore.Sqlite | ✅ كامل |
| MySQL | Pomelo.EntityFrameworkCore.MySql | ⏳ مخطط |
| MongoDB | MongoDB.EntityFrameworkCore | ⏳ مخطط |

---

## المكونات الرئيسية | Core Components

### 1. ACommerceDbContext

قاعدة DbContext الأساسية مع جميع الميزات المشتركة.

```csharp
public abstract class ACommerceDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantService? _tenantService;
    private readonly ICurrentUserService? _currentUserService;
    private IDbContextTransaction? _currentTransaction;

    protected ACommerceDbContext(
        DbContextOptions options,
        ITenantService? tenantService = null,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _tenantService = tenantService;
        _currentUserService = currentUserService;
    }

    #region Unit of Work Implementation

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _currentTransaction ??= await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction?.CommitAsync(cancellationToken)!;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _currentTransaction?.RollbackAsync(cancellationToken)!;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    #endregion

    #region SaveChanges with Auditing

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        ApplySoftDelete();
        ApplyTenantId();

        var domainEvents = GetDomainEvents();

        var result = await base.SaveChangesAsync(cancellationToken);

        await DispatchDomainEventsAsync(domainEvents);

        return result;
    }

    private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries<IAuditableEntity>();
        var currentUser = _currentUserService?.GetUserId();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = currentUser;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = currentUser;
            }
        }
    }

    private void ApplySoftDelete()
    {
        var entries = ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted);

        foreach (var entry in entries)
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            entry.Entity.DeletedBy = _currentUserService?.GetUserId();
        }
    }

    private void ApplyTenantId()
    {
        var tenantId = _tenantService?.GetCurrentTenantId();
        if (tenantId == null) return;

        var entries = ChangeTracker.Entries<IMultiTenantEntity>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            entry.Entity.TenantId = tenantId.Value;
        }
    }

    #endregion

    #region Domain Events

    private IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        var entities = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToList();

        var events = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        return events;
    }

    private async Task DispatchDomainEventsAsync(IReadOnlyList<IDomainEvent> domainEvents)
    {
        // Domain events are dispatched via MediatR if configured
        // This is handled by the DomainEventDispatcher service
    }

    #endregion

    #region Model Configuration

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global query filters
        ApplySoftDeleteFilter(modelBuilder);
        ApplyTenantFilter(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    private void ApplySoftDeleteFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(property), parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    private void ApplyTenantFilter(ModelBuilder modelBuilder)
    {
        if (_tenantService == null) return;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMultiTenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(IMultiTenantEntity.TenantId));
                var tenantId = Expression.Constant(_tenantService.GetCurrentTenantId());
                var filter = Expression.Lambda(Expression.Equal(property, tenantId), parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    #endregion
}
```

---

### 2. EfCoreRepository<TEntity, TId>

التنفيذ الكامل لنمط المستودع.

```csharp
public class EfCoreRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
{
    protected readonly ACommerceDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public EfCoreRepository(ACommerceDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    #region Query Operations

    public virtual async Task<TEntity?> GetByIdAsync(
        TId id,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new object[] { id! }, cancellationToken);
    }

    public virtual async Task<TEntity?> GetByIdWithIncludesAsync(
        TId id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes)
    {
        var query = DbSet.AsQueryable();

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.FirstOrDefaultAsync(e => e.Id!.Equals(id), cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindWithIncludesAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes)
    {
        var query = DbSet.Where(predicate);

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.ToListAsync(cancellationToken);
    }

    #endregion

    #region Command Operations

    public virtual async Task<TEntity> AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task<IEnumerable<TEntity>> AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        await DbSet.AddRangeAsync(entityList, cancellationToken);
        return entityList;
    }

    public virtual Task UpdateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        Context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(
        TId id,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    #endregion

    #region Existence & Count

    public virtual async Task<bool> ExistsAsync(
        TId id,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(e => e.Id!.Equals(id), cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> CountAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(cancellationToken);
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(predicate, cancellationToken);
    }

    #endregion

    #region Specification Pattern Support

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).ToListAsync(cancellationToken);
    }

    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<int> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).CountAsync(cancellationToken);
    }

    private IQueryable<TEntity> ApplySpecification(ISpecification<TEntity> specification)
    {
        return SpecificationEvaluator<TEntity>.GetQuery(DbSet.AsQueryable(), specification);
    }

    #endregion
}
```

---

### 3. SpecificationEvaluator

تقييم المواصفات وتحويلها لاستعلامات.

```csharp
public static class SpecificationEvaluator<TEntity> where TEntity : class
{
    public static IQueryable<TEntity> GetQuery(
        IQueryable<TEntity> inputQuery,
        ISpecification<TEntity> specification)
    {
        var query = inputQuery;

        // Apply criteria
        if (specification.Criteria != null)
        {
            query = query.Where(specification.Criteria);
        }

        // Apply includes
        query = specification.Includes.Aggregate(
            query,
            (current, include) => current.Include(include));

        // Apply string includes (for nested)
        query = specification.IncludeStrings.Aggregate(
            query,
            (current, include) => current.Include(include));

        // Apply ordering
        if (specification.OrderBy != null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending != null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        // Apply paging
        if (specification.IsPagingEnabled)
        {
            query = query
                .Skip(specification.Skip ?? 0)
                .Take(specification.Take ?? 20);
        }

        return query;
    }
}
```

---

### 4. Entity Type Configuration

تكوين الكيانات باستخدام Fluent API.

```csharp
public abstract class AuditableEntityConfiguration<TEntity, TId>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : class, IEntity<TId>, IAuditableEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        // Primary Key
        builder.HasKey(e => e.Id);

        // Audit Fields
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.UpdatedAt);

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(256);
    }
}

public abstract class SoftDeletableEntityConfiguration<TEntity, TId>
    : AuditableEntityConfiguration<TEntity, TId>
    where TEntity : class, IEntity<TId>, IAuditableEntity, ISoftDeletable
{
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        // Soft Delete Fields
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt);

        builder.Property(e => e.DeletedBy)
            .HasMaxLength(256);

        // Global Query Filter (exclude soft-deleted)
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public abstract class MultiTenantEntityConfiguration<TEntity, TId>
    : SoftDeletableEntityConfiguration<TEntity, TId>
    where TEntity : class, IEntity<TId>, IAuditableEntity, ISoftDeletable, IMultiTenantEntity
{
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        // Tenant Field
        builder.Property(e => e.TenantId)
            .IsRequired();

        // Tenant Index for performance
        builder.HasIndex(e => e.TenantId);
    }
}
```

**مثال على التكوين | Configuration Example:**

```csharp
public class ProductConfiguration : MultiTenantEntityConfiguration<Product, Guid>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);

        builder.ToTable("Products");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.Sku)
            .IsUnique();

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 5. Database Provider Configuration

#### PostgreSQL Configuration

```csharp
public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgreSqlDbContext<TContext>(
        this IServiceCollection services,
        string connectionString)
        where TContext : ACommerceDbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            options.UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
```

#### SQL Server Configuration

```csharp
public static class SqlServerExtensions
{
    public static IServiceCollection AddSqlServerDbContext<TContext>(
        this IServiceCollection services,
        string connectionString)
        where TContext : ACommerceDbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        return services;
    }
}
```

#### SQLite Configuration

```csharp
public static class SqliteExtensions
{
    public static IServiceCollection AddSqliteDbContext<TContext>(
        this IServiceCollection services,
        string connectionString)
        where TContext : ACommerceDbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
            });
        });

        return services;
    }
}
```

---

### 6. Multi-Database Support Pattern

```csharp
public enum DatabaseProvider
{
    PostgreSQL,
    SqlServer,
    SQLite
}

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase<TContext>(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString)
        where TContext : ACommerceDbContext
    {
        return provider switch
        {
            DatabaseProvider.PostgreSQL => services.AddPostgreSqlDbContext<TContext>(connectionString),
            DatabaseProvider.SqlServer => services.AddSqlServerDbContext<TContext>(connectionString),
            DatabaseProvider.SQLite => services.AddSqliteDbContext<TContext>(connectionString),
            _ => throw new ArgumentException($"Unsupported database provider: {provider}")
        };
    }
}

// Usage in Program.cs
var dbProvider = builder.Configuration.GetValue<DatabaseProvider>("Database:Provider");
var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddDatabase<AppDbContext>(dbProvider, connectionString);
```

---

## تسجيل الخدمات | Service Registration

```csharp
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEfCoreInfrastructure<TContext>(
        this IServiceCollection services)
        where TContext : ACommerceDbContext
    {
        // Register DbContext as IUnitOfWork
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TContext>());

        // Register generic repository
        services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IReadOnlyRepository<,>), typeof(EfCoreRepository<,>));

        return services;
    }
}

// Usage
builder.Services.AddPostgreSqlDbContext<AppDbContext>(connectionString);
builder.Services.AddEfCoreInfrastructure<AppDbContext>();
```

---

## Migrations

### إنشاء Migration جديد | Creating New Migration

```bash
# PostgreSQL
dotnet ef migrations add InitialCreate -c AppDbContext -o Data/Migrations

# For specific provider
dotnet ef migrations add InitialCreate -c AppDbContext -o Data/Migrations/PostgreSQL -- --provider PostgreSQL
```

### تطبيق Migrations | Applying Migrations

```bash
dotnet ef database update
```

### Migration في الكود | Migration in Code

```csharp
public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync<TContext>(this IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        try
        {
            logger.LogInformation("Applying database migrations...");

            await context.Database.MigrateAsync();

            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying migrations");
            throw;
        }
    }
}

// In Program.cs
var app = builder.Build();
await app.Services.ApplyMigrationsAsync<AppDbContext>();
```

---

## خدمات مساعدة | Helper Services

### ITenantService

```csharp
public interface ITenantService
{
    Guid? GetCurrentTenantId();
    void SetCurrentTenant(Guid tenantId);
}

public class HttpTenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetCurrentTenantId()
    {
        var claim = _httpContextAccessor.HttpContext?.User
            .FindFirst("tenant_id");

        return claim != null ? Guid.Parse(claim.Value) : null;
    }

    public void SetCurrentTenant(Guid tenantId)
    {
        // Usually done via middleware
    }
}
```

### ICurrentUserService

```csharp
public interface ICurrentUserService
{
    string? GetUserId();
    string? GetUserName();
    string? GetUserEmail();
    bool IsAuthenticated();
}

public class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetUserId()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public string? GetUserName()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Name)?.Value;
    }

    public string? GetUserEmail()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Email)?.Value;
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User
            .Identity?.IsAuthenticated ?? false;
    }
}
```

---

## أفضل الممارسات | Best Practices

### 1. استخدام IUnitOfWork للمعاملات
```csharp
// ✅ صحيح
await _unitOfWork.BeginTransactionAsync();
try
{
    await _orderRepository.AddAsync(order);
    await _unitOfWork.SaveChangesAsync();
    await _unitOfWork.CommitTransactionAsync();
}
catch
{
    await _unitOfWork.RollbackTransactionAsync();
    throw;
}
```

### 2. استخدام AsNoTracking للقراءة فقط
```csharp
// ✅ أفضل للأداء عند القراءة فقط
var products = await _context.Products
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToListAsync();
```

### 3. استخدام Projection لتقليل البيانات المنقولة
```csharp
// ✅ أفضل - نقل البيانات المطلوبة فقط
var productDtos = await _context.Products
    .Where(p => p.IsActive)
    .Select(p => new ProductListDto
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price
    })
    .ToListAsync();

// ❌ سيء - نقل جميع البيانات
var products = await _context.Products
    .Where(p => p.IsActive)
    .ToListAsync();
var productDtos = _mapper.Map<List<ProductListDto>>(products);
```

### 4. استخدام Specification Pattern للاستعلامات المعقدة
```csharp
// ✅ قابل لإعادة الاستخدام
var spec = new ActiveProductsInCategorySpec(categoryId);
var products = await _repository.ListAsync(spec);

// ❌ تكرار المنطق
var products = await _context.Products
    .Where(p => p.IsActive && p.CategoryId == categoryId)
    .Include(p => p.Images)
    .OrderByDescending(p => p.CreatedAt)
    .ToListAsync();
```

---

## التكامل | Integration

```
ACommerce.SharedKernel.Abstractions
              ↓
ACommerce.SharedKernel.Infrastructure.EFCore
              ↓
    ┌─────────┴─────────┐
    ↓                   ↓
Domain DbContexts    API Projects
 (per module)       (Uses DbContext)
```

---

## المراجع | References

- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [EF Core Performance Tips](https://learn.microsoft.com/en-us/ef/core/performance/)
- [Repository Pattern with EF Core](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core)
