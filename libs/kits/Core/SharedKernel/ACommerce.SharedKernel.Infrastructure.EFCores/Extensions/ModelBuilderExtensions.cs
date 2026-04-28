using Microsoft.EntityFrameworkCore;
using ACommerce.SharedKernel.Domain.Entities;
using System.Linq.Expressions;

namespace ACommerce.SharedKernel.Infrastructure.EFCore.Extensions;

/// <summary>
/// «„ œ«œ«  ModelBuilder · ÿ»Ìﬁ  ﬂÊÌ‰«  „‘ —ﬂ…
/// </summary>
public static class ModelBuilderExtensions
{
	/// <summary>
	///  ÿ»Ìﬁ  ﬂÊÌ‰«  „‘ —ﬂ… ·Ã„Ì⁄ «·ﬂÌ«‰«  «· Ì  ÿ»ﬁ IBaseEntity
	/// </summary>
	public static ModelBuilder ApplyBaseEntityConfiguration(this ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			// «· Õﬁﬁ „‰ √‰ «·ﬂÌ«‰ Ìÿ»ﬁ IBaseEntity
			if (!typeof(IBaseEntity).IsAssignableFrom(entityType.ClrType))
				continue;

			// Index ⁄·Ï CreatedAt
			modelBuilder.Entity(entityType.ClrType)
				.HasIndex(nameof(IBaseEntity.CreatedAt))
				.HasDatabaseName($"IX_{entityType.ClrType.Name}_CreatedAt");

			// Index ⁄·Ï IsDeleted
			modelBuilder.Entity(entityType.ClrType)
				.HasIndex(nameof(IBaseEntity.IsDeleted))
				.HasDatabaseName($"IX_{entityType.ClrType.Name}_IsDeleted");

			// Global Query Filter ·‹ Soft Delete
			modelBuilder.Entity(entityType.ClrType)
				.HasQueryFilter(
					CreateSoftDeleteFilter(entityType.ClrType));
		}

		return modelBuilder;
	}

	/// <summary>
	/// ≈‰‘«¡ Query Filter ··‹ Soft Delete
	/// </summary>
	private static LambdaExpression CreateSoftDeleteFilter(Type entityType)
	{
		var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
		var property = System.Linq.Expressions.Expression.Property(
			parameter,
			nameof(IBaseEntity.IsDeleted));
		var condition = System.Linq.Expressions.Expression.Equal(
			property,
			System.Linq.Expressions.Expression.Constant(false));

		return System.Linq.Expressions.Expression.Lambda(condition, parameter);
	}

	/// <summary>
	///  ÿ»Ìﬁ  ﬂÊÌ‰ «·√⁄„œ… «·„‘ —ﬂ…
	/// </summary>
	public static ModelBuilder ApplyBaseEntityColumnConfiguration(this ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (!typeof(IBaseEntity).IsAssignableFrom(entityType.ClrType))
				continue;

			// Id as Primary Key
			modelBuilder.Entity(entityType.ClrType)
				.HasKey(nameof(IBaseEntity.Id));

			// CreatedAt - Required
			modelBuilder.Entity(entityType.ClrType)
				.Property(nameof(IBaseEntity.CreatedAt))
				.IsRequired()
				.HasDefaultValueSql("GETUTCDATE()");

			// UpdatedAt - Nullable
			modelBuilder.Entity(entityType.ClrType)
				.Property(nameof(IBaseEntity.UpdatedAt))
				.IsRequired(false);

			// IsDeleted - Required with default false
			modelBuilder.Entity(entityType.ClrType)
				.Property(nameof(IBaseEntity.IsDeleted))
				.IsRequired()
				.HasDefaultValue(false);
		}

		return modelBuilder;
	}
}
