# ACommerce.SharedKernel.Abstractions

Core abstractions and interfaces for all Ashare applications - The foundation!

## Overview

The foundational library providing essential abstractions, interfaces, and base models used across all Ashare projects. Defines common patterns for entities, repositories, queries, events, and DTOs.

## Key Features

✅ **Base Entities** - IBaseEntity with common properties  
✅ **Repository Pattern** - IBaseAsyncRepository interface  
✅ **Query Models** - Pagination, filtering, sorting  
✅ **Domain Events** - IDomainEvent interface  
✅ **Result Pattern** - ApiResponse, Result<T>  
✅ **DTOs** - Common data transfer objects  

## Core Interfaces

### IBaseEntity
Base entity interface with common properties

**Properties:**
- `Id` (Guid) - Primary key
- `CreatedAt` (DateTime) - Creation timestamp
- `UpdatedAt` (DateTime?) - Last update timestamp
- `IsDeleted` (bool) - Soft delete flag

### IBaseAsyncRepository<T>
Generic async repository pattern

**Methods:**
- `GetByIdAsync(id)` - Get by ID
- `GetAllAsync()` - Get all entities
- `GetAllWithPredicateAsync(predicate, includeDeleted)` - Filtered query
- `AddAsync(entity)` - Create new
- `UpdateAsync(entity)` - Update existing
- `DeleteAsync(id)` - Soft delete
- `HardDeleteAsync(id)` - Permanent delete
- `CountAsync(predicate)` - Count entities
- `ExistsAsync(predicate)` - Check existence

### IDomainEvent
Marker interface for domain events
```csharp
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
```

## Query Models

### PagedResult<T>
Paginated query result

**Properties:**
- `Items` (List<T>) - Page items
- `TotalCount` (int) - Total items
- `PageNumber` (int) - Current page
- `PageSize` (int) - Items per page
- `TotalPages` (int) - Total pages (calculated)
- `HasPreviousPage` (bool) - Has previous
- `HasNextPage` (bool) - Has next

### PaginationRequest
Pagination parameters

**Properties:**
- `PageNumber` (int) - Default: 1
- `PageSize` (int) - Default: 10

### SortDescriptor
Sorting specification

**Properties:**
- `Field` (string) - Field name
- `Direction` (SortDirection) - Asc/Desc

### FilterDescriptor
Filtering specification

**Properties:**
- `Field` (string) - Field name
- `Operator` (FilterOperator) - Equal, NotEqual, Contains, etc.
- `Value` (object) - Filter value

## Result Pattern

### ApiResponse<T>
Standard API response wrapper

**Properties:**
- `Success` (bool) - Success flag
- `Data` (T) - Response data
- `Message` (string) - Message
- `Errors` (List<string>) - Error list
- `StatusCode` (int) - HTTP status code

**Static Methods:**
- `ApiResponse.Success(data, message)` - Success response
- `ApiResponse.Failure(message, errors)` - Failure response
- `ApiResponse.NotFound(message)` - 404 response
- `ApiResponse.Unauthorized(message)` - 401 response

### Result<T>
Domain result pattern

**Properties:**
- `IsSuccess` (bool) - Success flag
- `Value` (T) - Result value
- `Error` (string) - Error message

**Static Methods:**
- `Result.Success(value)` - Success result
- `Result.Failure(error)` - Failure result

## Common DTOs

### AuditDto
Audit information

**Properties:**
- `CreatedAt` (DateTime)
- `CreatedBy` (string)
- `UpdatedAt` (DateTime?)
- `UpdatedBy` (string?)

### ErrorDto
Error details

**Properties:**
- `Code` (string)
- `Message` (string)
- `Field` (string?)
- `Details` (Dictionary<string, object>)

## Enums

### SortDirection
```csharp
public enum SortDirection
{
    Ascending = 0,
    Descending = 1
}
```

### FilterOperator
```csharp
public enum FilterOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith,
    In,
    NotIn
}
```

## Usage Examples

### Entity Implementation
```csharp
public class Product : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    
    // Product-specific properties
    public string Name { get; set; }
    public decimal Price { get; set; }
}
```

### Repository Usage
```csharp
public class ProductService
{
    private readonly IBaseAsyncRepository<Product> _repository;
    
    public async Task<Product?> GetProduct(Guid id)
    {
        return await _repository.GetByIdAsync(id);
    }
    
    public async Task<PagedResult<Product>> GetProducts(PaginationRequest request)
    {
        var products = await _repository.GetAllAsync();
        
        return new PagedResult<Product>
        {
            Items = products.Skip((request.PageNumber - 1) * request.PageSize)
                           .Take(request.PageSize).ToList(),
            TotalCount = products.Count,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
```

### API Response
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<ApiResponse<Product>>> GetProduct(Guid id)
{
    var product = await _repository.GetByIdAsync(id);
    
    if (product == null)
        return NotFound(ApiResponse<Product>.NotFound("Product not found"));
    
    return Ok(ApiResponse<Product>.Success(product, "Product retrieved"));
}
```

## Installation
```bash
dotnet add package ACommerce.SharedKernel.Abstractions
```

## Dependencies

None - Pure abstractions

## Used By

- ACommerce.SharedKernel.CQRS
- ACommerce.SharedKernel.AspNetCore
- All Ashare.* libraries

## License

MIT