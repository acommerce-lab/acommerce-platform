using ACommerce.SharedKernel.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace ACommerce.Kits.Discovery.Domain;

public class DiscoveryCategory : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(100)] public string Slug { get; set; } = "";
    [MaxLength(100)] public string Label { get; set; } = "";
    [MaxLength(50)]  public string Icon { get; set; } = "";
    [MaxLength(50)]  public string Kind { get; set; } = ""; // residential, commercial, etc.
}

public class DiscoveryRegion : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(100)] public string Name { get; set; } = "";
    public Guid? ParentId { get; set; } // For City -> District hierarchy
    public int Level { get; set; } // 1: City, 2: District
}

public class DiscoveryAmenity : IBaseEntity
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [MaxLength(50)] public string Slug { get; set; } = "";
    [MaxLength(100)] public string Label { get; set; } = "";
}
