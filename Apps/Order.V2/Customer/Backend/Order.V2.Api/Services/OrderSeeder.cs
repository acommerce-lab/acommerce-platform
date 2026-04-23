using ACommerce.SharedKernel.Abstractions.Repositories;
using Order.V2.Api.Entities;

namespace Order.V2.Api.Services;

public class OrderSeeder
{
    public static class UserIds
    {
        public static readonly Guid CustomerSara  = Guid.Parse("00000000-0000-0000-0001-000000000001");
        public static readonly Guid VendorAhmed   = Guid.Parse("00000000-0000-0000-0002-000000000001");
        public static readonly Guid VendorFatimah = Guid.Parse("00000000-0000-0000-0002-000000000002");
        public static readonly Guid VendorSaad    = Guid.Parse("00000000-0000-0000-0002-000000000003");
        public static readonly Guid VendorLama    = Guid.Parse("00000000-0000-0000-0002-000000000004");
    }

    public static class CategoryIds
    {
        public static readonly Guid Coffee   = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid Meals    = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid Desserts = Guid.Parse("10000000-0000-0000-0000-000000000003");
        public static readonly Guid Drinks   = Guid.Parse("10000000-0000-0000-0000-000000000004");
        public static readonly Guid Specials = Guid.Parse("10000000-0000-0000-0000-000000000005");
    }

    public static class VendorIds
    {
        public static readonly Guid HappinessCafe  = Guid.Parse("20000000-0000-0000-0001-000000000001");
        public static readonly Guid AlAseelKitchen = Guid.Parse("20000000-0000-0000-0001-000000000002");
        public static readonly Guid RiyadhSweets   = Guid.Parse("20000000-0000-0000-0001-000000000003");
        public static readonly Guid CoolBites      = Guid.Parse("20000000-0000-0000-0001-000000000004");
    }

    private readonly IBaseAsyncRepository<User> _users;
    private readonly IBaseAsyncRepository<Category> _categories;
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly IBaseAsyncRepository<Offer> _offers;
    private readonly IBaseAsyncRepository<Notification> _notifications;
    private readonly IBaseAsyncRepository<Conversation> _convs;
    private readonly IBaseAsyncRepository<Message> _msgs;

    public OrderSeeder(IRepositoryFactory factory)
    {
        _users = factory.CreateRepository<User>();
        _categories = factory.CreateRepository<Category>();
        _vendors = factory.CreateRepository<Vendor>();
        _offers = factory.CreateRepository<Offer>();
        _notifications = factory.CreateRepository<Notification>();
        _convs = factory.CreateRepository<Conversation>();
        _msgs = factory.CreateRepository<Message>();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await _users.ListAllAsync(ct);
        if (existing.Any()) return;

        var now = DateTime.UtcNow;

        // ───── Users ─────
        await _users.AddAsync(new User
        {
            Id = UserIds.CustomerSara, CreatedAt = now,
            PhoneNumber = "+966500000001", FullName = "سارة العميلة",
            Email = "sara@order.app", Role = "customer", IsActive = true,
            CarModel = "تويوتا كامري", CarColor = "أبيض", CarPlate = "أ ب ج 1234"
        }, ct);
        await _users.AddAsync(new User
        {
            Id = UserIds.VendorAhmed, CreatedAt = now,
            PhoneNumber = "+966501111111", FullName = "أحمد - كافيه السعادة",
            Role = "vendor", IsActive = true
        }, ct);
        await _users.AddAsync(new User
        {
            Id = UserIds.VendorFatimah, CreatedAt = now,
            PhoneNumber = "+966502222222", FullName = "فاطمة - مطعم الأصيل",
            Role = "vendor", IsActive = true
        }, ct);
        await _users.AddAsync(new User
        {
            Id = UserIds.VendorSaad, CreatedAt = now,
            PhoneNumber = "+966503333333", FullName = "سعد - حلويات الرياض",
            Role = "vendor", IsActive = true
        }, ct);
        await _users.AddAsync(new User
        {
            Id = UserIds.VendorLama, CreatedAt = now,
            PhoneNumber = "+966504444444", FullName = "لمى - عصائر كول بايتس",
            Role = "vendor", IsActive = true
        }, ct);

        // ───── Categories ─────
        await _categories.AddAsync(new Category { Id = CategoryIds.Coffee,   CreatedAt = now, Slug = "coffee",   NameAr = "قهوة",       NameEn = "Coffee",   Icon = "☕",  SortOrder = 1 }, ct);
        await _categories.AddAsync(new Category { Id = CategoryIds.Meals,    CreatedAt = now, Slug = "meals",    NameAr = "وجبات",      NameEn = "Meals",    Icon = "🍔",  SortOrder = 2 }, ct);
        await _categories.AddAsync(new Category { Id = CategoryIds.Desserts, CreatedAt = now, Slug = "desserts", NameAr = "حلويات",     NameEn = "Desserts", Icon = "🧁",  SortOrder = 3 }, ct);
        await _categories.AddAsync(new Category { Id = CategoryIds.Drinks,   CreatedAt = now, Slug = "drinks",   NameAr = "مشروبات",    NameEn = "Drinks",   Icon = "🥤",  SortOrder = 4 }, ct);
        await _categories.AddAsync(new Category { Id = CategoryIds.Specials, CreatedAt = now, Slug = "specials", NameAr = "عروض خاصة", NameEn = "Specials", Icon = "⭐",  SortOrder = 5 }, ct);

        // ───── Vendors ─────
        await _vendors.AddAsync(new Vendor
        {
            Id = VendorIds.HappinessCafe, CreatedAt = now,
            OwnerId = UserIds.VendorAhmed, CategoryId = CategoryIds.Coffee,
            Name = "كافيه السعادة", Slug = "happiness-cafe",
            Description = "أفضل قهوة مختصة في المدينة، أجواء عائلية وسرعة في التحضير.",
            City = "الرياض", District = "العليا", Phone = "+966501111111",
            LogoEmoji = "☕", CoverEmoji = "☕",
            Latitude = 24.7136, Longitude = 46.6753,
            OpenHours = "07:00|23:00", Rating = 4.8, RatingCount = 124
        }, ct);
        await _vendors.AddAsync(new Vendor
        {
            Id = VendorIds.AlAseelKitchen, CreatedAt = now,
            OwnerId = UserIds.VendorFatimah, CategoryId = CategoryIds.Meals,
            Name = "مطعم الأصيل", Slug = "al-aseel-kitchen",
            Description = "مأكولات عربية شعبية أصيلة من أفضل المطابخ السعودية.",
            City = "الرياض", District = "النخيل", Phone = "+966502222222",
            LogoEmoji = "🍛", CoverEmoji = "🍔",
            Latitude = 24.7256, Longitude = 46.6890,
            OpenHours = "11:00|23:30", Rating = 4.6, RatingCount = 87
        }, ct);
        await _vendors.AddAsync(new Vendor
        {
            Id = VendorIds.RiyadhSweets, CreatedAt = now,
            OwnerId = UserIds.VendorSaad, CategoryId = CategoryIds.Desserts,
            Name = "حلويات الرياض", Slug = "riyadh-sweets",
            Description = "كنافة، بقلاوة، تشيز كيك… كل الحلويات الشرقية والغربية في مكان واحد.",
            City = "الرياض", District = "الياسمين", Phone = "+966503333333",
            LogoEmoji = "🧁", CoverEmoji = "🍰",
            Latitude = 24.7000, Longitude = 46.7100,
            OpenHours = "09:00|01:00", Rating = 4.9, RatingCount = 213
        }, ct);
        await _vendors.AddAsync(new Vendor
        {
            Id = VendorIds.CoolBites, CreatedAt = now,
            OwnerId = UserIds.VendorLama, CategoryId = CategoryIds.Drinks,
            Name = "كول بايتس", Slug = "cool-bites",
            Description = "عصائر طبيعية، سموذي، ومشروبات منعشة طازجة.",
            City = "الرياض", District = "المروج", Phone = "+966504444444",
            LogoEmoji = "🥤", CoverEmoji = "🍹",
            Latitude = 24.7300, Longitude = 46.7000,
            OpenHours = "09:00|00:00", Rating = 4.5, RatingCount = 56
        }, ct);

        // ───── Offers ─────
        async Task AddOffer(Guid vendorId, Guid catId, string title, string desc, decimal price, decimal? original, string emoji, bool featured = false)
        {
            await _offers.AddAsync(new Offer
            {
                Id = Guid.NewGuid(), CreatedAt = now,
                VendorId = vendorId, CategoryId = catId,
                Title = title, Description = desc,
                Price = price, OriginalPrice = original,
                Currency = "SAR", Emoji = emoji,
                IsActive = true, IsFeatured = featured,
                StartsAt = now.AddDays(-1), EndsAt = now.AddDays(30),
            }, ct);
        }

        await AddOffer(VendorIds.HappinessCafe,  CategoryIds.Coffee,   "لاتيه + كرواسون",   "عرض الصباح: لاتيه ساخن مع كرواسون طازج خارج الفرن.", 18, 25, "☕", featured: true);
        await AddOffer(VendorIds.HappinessCafe,  CategoryIds.Coffee,   "اثنين كابتشينو",    "اثنين كابتشينو بسعر واحد ونصف.",                       27, 36, "☕");
        await AddOffer(VendorIds.HappinessCafe,  CategoryIds.Coffee,   "آيس موكا كبير",     "آيس موكا حجم كبير مع كريمة إضافية ورقائق شوكولاتة.",  16, 22, "🧊");
        await AddOffer(VendorIds.HappinessCafe,  CategoryIds.Specials, "كومبو الإفطار",     "قهوتك المفضلة + كرواسون + عصير برتقال طازج.",          28, 40, "🥐", featured: true);

        await AddOffer(VendorIds.AlAseelKitchen, CategoryIds.Meals,    "كبسة دجاج",         "نصف دجاج، أرز كبسة، سلطة، ومشروب غازي.",               35, 45, "🍛", featured: true);
        await AddOffer(VendorIds.AlAseelKitchen, CategoryIds.Meals,    "شاورما عربي مزدوج",  "شاورمتان دجاج + بطاطس + مشروب.",                       22, 30, "🌯");
        await AddOffer(VendorIds.AlAseelKitchen, CategoryIds.Meals,    "برجر لحم أنقوس",     "برجر لحم أنقوس مشوي، جبنة شيدر، خس، وبطاطس.",         32, 42, "🍔");

        await AddOffer(VendorIds.RiyadhSweets,   CategoryIds.Desserts, "كيلو كنافة",        "كنافة نابلسية فاخرة، طازجة في الفرن.",                  60, 80, "🧁");
        await AddOffer(VendorIds.RiyadhSweets,   CategoryIds.Desserts, "تشيز كيك + قهوة",   "قطعة تشيز كيك مع قهوة تركية أو سعودية.",               25, 35, "🍰", featured: true);
        await AddOffer(VendorIds.RiyadhSweets,   CategoryIds.Desserts, "نصف كيلو بقلاوة",    "تشكيلة من أفضل أنواع البقلاوة بالفستق.",               42, 55, "🥮");

        await AddOffer(VendorIds.CoolBites,      CategoryIds.Drinks,   "سموذي مانجو",       "مانجو طازج مع زبادي وعسل أبيض.",                       14, 20, "🥭");
        await AddOffer(VendorIds.CoolBites,      CategoryIds.Drinks,   "عصير برتقال طبيعي", "كوب كبير عصير برتقال طازج 100%.",                        9, 14, "🍊");
        await AddOffer(VendorIds.CoolBites,      CategoryIds.Drinks,   "ليموناضة بالنعناع", "ليمون طازج، نعناع، وسكر براون.",                        11, 16, "🍋");

        // ───── Notifications for Sara ─────
        await _notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(), CreatedAt = now.AddMinutes(-2), UserId = UserIds.CustomerSara,
            Title = "خصم 28% على عرض الصباح",
            Body = "احصلي على لاتيه + كرواسون من كافيه السعادة بـ 18 ر.س فقط بدل 25 ر.س.",
            Type = "promo", Priority = "normal", Channel = "inapp", DeliveryStatus = "sent", SentAt = now
        }, ct);
        await _notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(), CreatedAt = now.AddHours(-3), UserId = UserIds.CustomerSara,
            Title = "كومبو الإفطار رجع!",
            Body = "كومبو الإفطار الكامل في كافيه السعادة بـ 28 ر.س - متوفر طوال الأسبوع.",
            Type = "promo", Priority = "normal", Channel = "inapp", DeliveryStatus = "sent", SentAt = now, IsRead = true
        }, ct);
        await _notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(), CreatedAt = now.AddDays(-1), UserId = UserIds.CustomerSara,
            Title = "أهلاً بك في اوردر V2",
            Body = "اطلب عروض الكافيهات والمطاعم المفضلة لديك بكل سهولة.",
            Type = "general", Priority = "low", Channel = "inapp", DeliveryStatus = "sent", SentAt = now, IsRead = true
        }, ct);

        // ───── Conversation: Sara <-> Happiness Cafe ─────
        var conv = new Conversation
        {
            Id = Guid.NewGuid(), CreatedAt = now.AddHours(-1),
            CustomerId = UserIds.CustomerSara, VendorId = VendorIds.HappinessCafe,
            LastMessageSnippet = "تأكد، نراك بعد ربع ساعة.",
            LastMessageAt = now.AddMinutes(-12),
            UnreadCustomerCount = 1
        };
        await _convs.AddAsync(conv, ct);
        await _msgs.AddAsync(new Message { Id = Guid.NewGuid(), CreatedAt = now.AddMinutes(-30), ConversationId = conv.Id, SenderId = UserIds.CustomerSara, Content = "السلام عليكم، عرض الصباح موجود الآن؟" }, ct);
        await _msgs.AddAsync(new Message { Id = Guid.NewGuid(), CreatedAt = now.AddMinutes(-25), ConversationId = conv.Id, SenderId = UserIds.VendorAhmed,  Content = "وعليكم السلام، نعم متوفر، تفضلي بالطلب." }, ct);
        await _msgs.AddAsync(new Message { Id = Guid.NewGuid(), CreatedAt = now.AddMinutes(-15), ConversationId = conv.Id, SenderId = UserIds.CustomerSara, Content = "ممتاز، سأطلب لاتيه + كرواسون. هل أستلم من السيارة؟" }, ct);
        await _msgs.AddAsync(new Message { Id = Guid.NewGuid(), CreatedAt = now.AddMinutes(-12), ConversationId = conv.Id, SenderId = UserIds.VendorAhmed,  Content = "تأكد، نراك بعد ربع ساعة." }, ct);
    }
}
