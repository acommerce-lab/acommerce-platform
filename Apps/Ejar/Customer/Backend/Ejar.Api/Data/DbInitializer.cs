using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Support.Domain;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Data;

public static class DbInitializer
{
    public static void Seed(EjarDbContext db)
    {
        if (db.Users.Any()) return;

        // 1. Seed Users
        var userMap = new Dictionary<string, Guid>();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        db.Users.Add(new UserEntity {
            Id = user1Id, FullName = "أمل عبدالله المؤيد", Phone = "+967771234567", PhoneVerified = true,
            Email = "amal@example.ye", EmailVerified = true, City = "صنعاء", MemberSince = new DateTime(2024, 3, 12),
            CreatedAt = new DateTime(2024, 3, 12)
        });
        db.Users.Add(new UserEntity {
            Id = user2Id, FullName = "فهد محمد الجمالي", Phone = "+967773456789", PhoneVerified = true,
            Email = "fahd@example.ye", EmailVerified = false, City = "عدن", MemberSince = new DateTime(2025, 1, 22),
            CreatedAt = new DateTime(2025, 1, 22)
        });

        userMap["U-1"] = user1Id;
        userMap["U-2"] = user2Id;

        // 2. Seed Categories
        foreach (var c in EjarSeed.Categories)
        {
            db.DiscoveryCategories.Add(new DiscoveryCategory {
                Slug = c.Id, Label = c.Label, Icon = c.Emoji, Kind = c.Kind, CreatedAt = DateTime.UtcNow
            });
        }

        // 3. Seed Regions
        foreach (var city in new[] { "صنعاء", "عدن", "تعز", "إب", "الحديدة", "المكلا" })
        {
            db.DiscoveryRegions.Add(new DiscoveryRegion { Name = city, Level = 1, CreatedAt = DateTime.UtcNow });
        }

        // 4. Seed Amenities
        foreach (var a in EjarSeed.Amenities)
        {
            db.DiscoveryAmenities.Add(new DiscoveryAmenity { Slug = a.Id, Label = a.Label, CreatedAt = DateTime.UtcNow });
        }

        // 5. Seed Listings
        foreach (var l in EjarSeed.Listings)
        {
            var ownerId = userMap.TryGetValue(l.OwnerId, out var o) ? o : user2Id;
            db.Listings.Add(new ListingEntity
            {
                Title = l.Title, Description = l.Description, Price = l.Price, TimeUnit = l.TimeUnit,
                PropertyType = l.PropertyType, City = l.City, District = l.District,
                Lat = l.Lat, Lng = l.Lng, OwnerId = ownerId,
                BedroomCount = l.BedroomCount, BathroomCount = l.BathroomCount, AreaSqm = l.AreaSqm,
                IsVerified = l.IsVerified, ViewsCount = l.ViewsCount, Status = l.Status,
                ImagesCsv = l.Images != null ? string.Join(",", l.Images) : "",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
            });
        }

        db.SaveChanges();
    }
}
