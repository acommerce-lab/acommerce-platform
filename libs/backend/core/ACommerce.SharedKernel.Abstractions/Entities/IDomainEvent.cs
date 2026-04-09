namespace ACommerce.SharedKernel.Abstractions.Entities;

/// <summary>
/// Marker interface for domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// When the event occurred
    /// </summary>
    DateTimeOffset OccurredAt { get; init; }
}