# ACommerce.SharedKernel.Infrastructure.EFCore

Entity Framework Core implementation of SharedKernel repository pattern with advanced querying capabilities.

## Features

✅ **Generic Repository** - Full IBaseAsyncRepository implementation  
✅ **Smart Search** - Text search across all string properties  
✅ **Advanced Filtering** - Support for complex filter expressions  
✅ **Soft Delete** - Built-in soft delete with global query filters  
✅ **Paging** - Efficient pagination support  
✅ **Logging** - Comprehensive logging for all operations  

## Installation
```bash
dotnet add package ACommerce.SharedKernel.Infrastructure.EFCore
```

## Usage

### Register in Program.cs
```csharp
// Add DbContext
builder.Services.AddDbContext<YourDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add EF Core Infrastructure
builder.Services.AddEfCoreInfrastructure<YourDbContext>();
```

### Configure DbContext
```csharp
public class YourDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply base entity configurations
        modelBuilder.ApplyBaseEntityConfiguration();
        modelBuilder.ApplyBaseEntityColumnConfiguration();
    }
}
```

### Use Repository
```csharp
public class ProductService
{
    private readonly IBaseAsyncRepository<Product> _repository;
    
    public async Task<PagedResult<Product>> SearchAsync(SmartSearchRequest request)
    {
        return await _repository.SmartSearchAsync(request);
    }
}
```

## Global Query Filters

Soft delete is automatically applied as a global query filter. To include deleted items:
```csharp
// Include deleted items
var allProducts = await _repository.ListAllAsync(includeDeleted: true);

// Or use IgnoreQueryFilters in raw queries
var products = await _context.Products
    .IgnoreQueryFilters()
    .ToListAsync();
```

## License

MIT