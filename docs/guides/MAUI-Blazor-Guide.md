# دليل إنشاء تطبيقات MAUI Blazor | MAUI Blazor Applications Guide

## مقدمة | Introduction

هذا الدليل يشرح كيفية بناء تطبيقات متعددة المنصات باستخدام .NET MAUI Blazor Hybrid مع مكتبات ACommerce Templates. ستتعلم كيفية إنشاء تطبيق تجارة إلكترونية يعمل على Android و iOS و Windows و macOS.

This guide explains how to build cross-platform applications using .NET MAUI Blazor Hybrid with ACommerce Templates libraries. You'll learn how to create an e-commerce app that runs on Android, iOS, Windows, and macOS.

---

## ما هو MAUI Blazor Hybrid؟ | What is MAUI Blazor Hybrid?

MAUI Blazor Hybrid يجمع بين:
- **.NET MAUI**: إطار عمل متعدد المنصات لبناء تطبيقات أصلية
- **Blazor**: إطار عمل لبناء واجهات مستخدم تفاعلية بـ C# و HTML
- **WebView**: عرض محتوى Blazor داخل تطبيق أصلي

```
┌─────────────────────────────────────────────────────────────────┐
│                    MAUI Blazor Hybrid App                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                     Native Shell                          │  │
│  │  (Status Bar, Navigation, Native Controls)                │  │
│  └───────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                      BlazorWebView                        │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │              Blazor Components                      │  │  │
│  │  │  (Razor Pages, CSS, JavaScript)                     │  │  │
│  │  │  - ACommerce.Templates.Customer                     │  │  │
│  │  │  - Custom Components                                │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                  Native Services                          │  │
│  │  (Camera, GPS, Biometrics, Push Notifications)           │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## المتطلبات | Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 (مع workloads MAUI)
- Android SDK (للتطوير لـ Android)
- Xcode (للتطوير لـ iOS/macOS - يتطلب Mac)
- Windows 11 SDK (للتطوير لـ Windows)

### تثبيت workloads MAUI

```bash
dotnet workload install maui
dotnet workload install maui-android
dotnet workload install maui-ios
dotnet workload install maui-maccatalyst
dotnet workload install maui-windows
```

---

## الخطوة 1: إنشاء المشروع | Step 1: Create Project

```bash
# إنشاء مشروع MAUI Blazor جديد
dotnet new maui-blazor -n MyEShop.Mobile -o src/MyEShop.Mobile

# إضافة للـ Solution
dotnet sln add src/MyEShop.Mobile
```

### تثبيت حزم ACommerce

```bash
cd src/MyEShop.Mobile

# مكتبة القوالب
dotnet add package ACommerce.Templates.Customer

# مكتبات SDK للتواصل مع الـ API
dotnet add package ACommerce.Client.Auth
dotnet add package ACommerce.Client.Products
dotnet add package ACommerce.Client.Cart
dotnet add package ACommerce.Client.Orders
```

---

## الخطوة 2: هيكل المشروع | Step 2: Project Structure

```
MyEShop.Mobile/
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor
│   └── Pages/
│       ├── Home.razor
│       ├── ProductDetails.razor
│       ├── Cart.razor
│       ├── Checkout.razor
│       ├── Orders.razor
│       ├── Profile.razor
│       └── Settings.razor
├── Services/
│   ├── IAppNavigationService.cs
│   ├── AppNavigationService.cs
│   ├── IAuthStateProvider.cs
│   ├── AuthStateProvider.cs
│   ├── ISecureStorageService.cs
│   └── SecureStorageService.cs
├── wwwroot/
│   ├── css/
│   │   └── app.css
│   └── index.html
├── Platforms/
│   ├── Android/
│   ├── iOS/
│   ├── MacCatalyst/
│   └── Windows/
├── App.xaml
├── App.xaml.cs
├── MainPage.xaml
├── MauiProgram.cs
└── appsettings.json
```

---

## الخطوة 3: إعداد MauiProgram.cs | Step 3: Setup MauiProgram.cs

```csharp
using ACommerce.Templates.Customer.Services;
using ACommerce.Templates.Customer.Themes;
using ACommerce.Client.Auth;
using ACommerce.Client.Products;
using ACommerce.Client.Cart;
using ACommerce.Client.Orders;
using Microsoft.Extensions.Logging;

namespace MyEShop.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                // Add Arabic font
                fonts.AddFont("Cairo-Regular.ttf", "CairoRegular");
                fonts.AddFont("Cairo-Bold.ttf", "CairoBold");
            });

        // Add Blazor WebView
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Configure API Base URL
        var apiBaseUrl = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000" // Android Emulator
            : "http://localhost:5000"; // iOS Simulator / Desktop

        // Add ACommerce Customer Template
        builder.Services.AddACommerceCustomerTemplate(options =>
        {
            // Theme Colors
            options.Colors.Primary = "#2563eb";
            options.Colors.Secondary = "#64748b";
            options.Colors.Success = "#22c55e";
            options.Colors.Error = "#ef4444";
            options.Colors.Warning = "#f59e0b";
            options.Colors.Info = "#3b82f6";

            // RTL for Arabic
            options.Direction = TextDirection.RTL;

            // Light/Dark mode
            options.Mode = Application.Current?.RequestedTheme == AppTheme.Dark
                ? ThemeMode.Dark
                : ThemeMode.Light;
        });

        // Add Client SDKs
        builder.Services.AddACommerceAuthClient(options =>
        {
            options.BaseUrl = apiBaseUrl;
        });

        builder.Services.AddACommerceProductsClient(options =>
        {
            options.BaseUrl = apiBaseUrl;
        });

        builder.Services.AddACommerceCartClient(options =>
        {
            options.BaseUrl = apiBaseUrl;
        });

        builder.Services.AddACommerceOrdersClient(options =>
        {
            options.BaseUrl = apiBaseUrl;
        });

        // Add App Services
        builder.Services.AddScoped<IAppNavigationService, AppNavigationService>();
        builder.Services.AddScoped<ISecureStorageService, SecureStorageService>();
        builder.Services.AddScoped<AuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<AuthStateProvider>());

        // Add Authorization
        builder.Services.AddAuthorizationCore();

        return builder.Build();
    }
}
```

---

## الخطوة 4: خدمات التطبيق | Step 4: App Services

### IAppNavigationService.cs

```csharp
namespace MyEShop.Mobile.Services;

public interface IAppNavigationService
{
    void NavigateTo(string uri, bool forceLoad = false);
    void NavigateBack();
    string CurrentUri { get; }
}
```

### AppNavigationService.cs

```csharp
using Microsoft.AspNetCore.Components;

namespace MyEShop.Mobile.Services;

public class AppNavigationService : IAppNavigationService
{
    private readonly NavigationManager _navigationManager;

    public AppNavigationService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public string CurrentUri => _navigationManager.Uri;

    public void NavigateTo(string uri, bool forceLoad = false)
    {
        _navigationManager.NavigateTo(uri, forceLoad);
    }

    public void NavigateBack()
    {
        // استخدام JavaScript للرجوع
        // أو تتبع تاريخ التنقل يدوياً
    }
}
```

### SecureStorageService.cs

```csharp
namespace MyEShop.Mobile.Services;

public interface ISecureStorageService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
    Task ClearAsync();
}

public class SecureStorageService : ISecureStorageService
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            // SecureStorage not available, fallback to Preferences
            return Preferences.Default.Get<string?>(key, null);
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception)
        {
            Preferences.Default.Set(key, value);
        }
    }

    public Task RemoveAsync(string key)
    {
        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch (Exception)
        {
            Preferences.Default.Remove(key);
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.RemoveAll();
        }
        catch (Exception)
        {
            Preferences.Default.Clear();
        }
        return Task.CompletedTask;
    }
}
```

### AuthStateProvider.cs

```csharp
using ACommerce.Client.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace MyEShop.Mobile.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ISecureStorageService _secureStorage;
    private readonly IAuthClient _authClient;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public AuthStateProvider(
        ISecureStorageService secureStorage,
        IAuthClient authClient)
    {
        _secureStorage = secureStorage;
        _authClient = authClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _secureStorage.GetAsync("access_token");

        if (string.IsNullOrEmpty(token))
            return new AuthenticationState(_anonymous);

        var userJson = await _secureStorage.GetAsync("user_info");
        if (string.IsNullOrEmpty(userJson))
            return new AuthenticationState(_anonymous);

        var user = JsonSerializer.Deserialize<UserInfo>(userJson);
        if (user == null)
            return new AuthenticationState(_anonymous);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName ?? user.Email)
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        var principal = new ClaimsPrincipal(identity);

        return new AuthenticationState(principal);
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var result = await _authClient.LoginAsync(email, password);

        if (!result.Succeeded)
            return false;

        await _secureStorage.SetAsync("access_token", result.AccessToken!);
        await _secureStorage.SetAsync("refresh_token", result.RefreshToken!);
        await _secureStorage.SetAsync("user_info", JsonSerializer.Serialize(result.User));

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

        return true;
    }

    public async Task LogoutAsync()
    {
        await _secureStorage.RemoveAsync("access_token");
        await _secureStorage.RemoveAsync("refresh_token");
        await _secureStorage.RemoveAsync("user_info");

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        return await _secureStorage.GetAsync("access_token");
    }
}
```

---

## الخطوة 5: الصفحات | Step 5: Pages

### Home.razor

```razor
@page "/"
@using ACommerce.Templates.Customer.Pages
@using ACommerce.Client.Products
@inject IProductsClient ProductsClient
@inject IAppNavigationService Navigation

<HomePage Categories="@_categories"
          Banners="@_banners"
          FeaturedProducts="@_featuredProducts"
          NewProducts="@_newProducts"
          BestSellers="@_bestSellers"
          IsLoading="@_isLoading"
          OnCategoryClick="HandleCategoryClick"
          OnBannerClick="HandleBannerClick"
          OnProductClick="HandleProductClick"
          OnAddToCart="HandleAddToCart"
          OnSearch="HandleSearch" />

@code {
    private List<HomeCategoryItem>? _categories;
    private List<BannerItem>? _banners;
    private List<HomeProductItem>? _featuredProducts;
    private List<HomeProductItem>? _newProducts;
    private List<HomeProductItem>? _bestSellers;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;

        try
        {
            // Load categories
            var categoriesResult = await ProductsClient.GetCategoriesAsync();
            _categories = categoriesResult.Select(c => new HomeCategoryItem
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon ?? "bi-folder"
            }).ToList();

            // Load featured products
            var featuredResult = await ProductsClient.SearchAsync(new SearchProductsRequest
            {
                IsFeatured = true,
                PageSize = 6
            });
            _featuredProducts = MapToHomeProducts(featuredResult.Items);

            // Load new products
            var newResult = await ProductsClient.SearchAsync(new SearchProductsRequest
            {
                SortBy = "created_at",
                SortDescending = true,
                PageSize = 6
            });
            _newProducts = MapToHomeProducts(newResult.Items);

            // Load best sellers
            var bestSellersResult = await ProductsClient.SearchAsync(new SearchProductsRequest
            {
                SortBy = "sales_count",
                SortDescending = true,
                PageSize = 6
            });
            _bestSellers = MapToHomeProducts(bestSellersResult.Items);

            // Static banners for now
            _banners = new List<BannerItem>
            {
                new() { Id = "1", Title = "تخفيضات الصيف", Subtitle = "خصم حتى 50%", ImageUrl = "images/banner1.jpg" },
                new() { Id = "2", Title = "منتجات جديدة", Subtitle = "اكتشف الأحدث", ImageUrl = "images/banner2.jpg" },
            };
        }
        catch (Exception ex)
        {
            // Handle error
            Console.WriteLine($"Error loading data: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private List<HomeProductItem> MapToHomeProducts(IEnumerable<ProductListDto> products)
    {
        return products.Select(p => new HomeProductItem
        {
            Id = p.Id,
            Name = p.Name,
            Image = p.PrimaryImageUrl ?? "images/placeholder.png",
            Price = p.Price,
            OldPrice = p.CompareAtPrice,
            Rating = p.Rating ?? 0,
            ReviewCount = p.ReviewCount
        }).ToList();
    }

    private void HandleCategoryClick(Guid categoryId)
    {
        Navigation.NavigateTo($"/search?category={categoryId}");
    }

    private void HandleBannerClick(BannerItem banner)
    {
        if (!string.IsNullOrEmpty(banner.LinkUrl))
        {
            Navigation.NavigateTo(banner.LinkUrl);
        }
    }

    private void HandleProductClick(Guid productId)
    {
        Navigation.NavigateTo($"/product/{productId}");
    }

    private async Task HandleAddToCart(Guid productId)
    {
        // TODO: Add to cart via CartClient
    }

    private void HandleSearch(string query)
    {
        Navigation.NavigateTo($"/search?q={Uri.EscapeDataString(query)}");
    }
}
```

### ProductDetails.razor

```razor
@page "/product/{Id:guid}"
@using ACommerce.Templates.Customer.Pages
@using ACommerce.Client.Products
@using ACommerce.Client.Cart
@inject IProductsClient ProductsClient
@inject ICartClient CartClient
@inject IAppNavigationService Navigation

<ProductDetailsPage Product="@_product"
                    IsLoading="@_isLoading"
                    IsAddingToCart="@_isAddingToCart"
                    SelectedQuantity="@_quantity"
                    OnQuantityChange="HandleQuantityChange"
                    OnAttributeChange="HandleAttributeChange"
                    OnAddToCart="HandleAddToCart"
                    OnBuyNow="HandleBuyNow"
                    OnBack="HandleBack"
                    OnShare="HandleShare" />

@code {
    [Parameter]
    public Guid Id { get; set; }

    private ProductDetailsItem? _product;
    private bool _isLoading = true;
    private bool _isAddingToCart;
    private int _quantity = 1;
    private Dictionary<string, string> _selectedAttributes = new();

    protected override async Task OnParametersSetAsync()
    {
        await LoadProductAsync();
    }

    private async Task LoadProductAsync()
    {
        _isLoading = true;

        try
        {
            var result = await ProductsClient.GetByIdAsync(Id);

            if (result != null)
            {
                _product = new ProductDetailsItem
                {
                    Id = result.Id,
                    Name = result.Name,
                    Brand = result.Brand?.Name,
                    ShortDescription = result.ShortDescription,
                    LongDescription = result.LongDescription,
                    Images = result.Images.Select(i => i.Url).ToList(),
                    Price = result.BasePrice,
                    OldPrice = result.CompareAtPrice,
                    Rating = result.Rating ?? 0,
                    ReviewCount = result.ReviewCount,
                    StockQuantity = result.StockQuantity,
                    Sku = result.Sku,
                    Attributes = result.Attributes.Select(a => new DynamicAttribute
                    {
                        Id = a.Code,
                        Label = a.Name,
                        Type = MapAttributeType(a.DisplayType),
                        Values = a.Options?.Select(o => o.Value).ToList() ?? new List<string>(),
                        SelectedValue = a.SelectedValue
                    }).ToList(),
                    Specifications = result.Specifications
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading product: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private AttributeDisplayType MapAttributeType(string type)
    {
        return type.ToLower() switch
        {
            "dropdown" => AttributeDisplayType.Dropdown,
            "buttongroup" => AttributeDisplayType.ButtonGroup,
            "colorswatch" => AttributeDisplayType.ColorSwatch,
            "imageswatch" => AttributeDisplayType.ImageSwatch,
            _ => AttributeDisplayType.Dropdown
        };
    }

    private void HandleQuantityChange(int quantity)
    {
        _quantity = quantity;
    }

    private void HandleAttributeChange(AttributeChange change)
    {
        _selectedAttributes[change.AttributeId] = change.Value;
    }

    private async Task HandleAddToCart()
    {
        if (_product == null) return;

        _isAddingToCart = true;

        try
        {
            await CartClient.AddItemAsync(new AddToCartRequest
            {
                ProductId = _product.Id,
                Quantity = _quantity,
                Attributes = _selectedAttributes
            });

            // Show success message
        }
        catch (Exception ex)
        {
            // Show error message
        }
        finally
        {
            _isAddingToCart = false;
        }
    }

    private async Task HandleBuyNow()
    {
        await HandleAddToCart();
        Navigation.NavigateTo("/cart");
    }

    private void HandleBack()
    {
        Navigation.NavigateBack();
    }

    private async Task HandleShare()
    {
        if (_product == null) return;

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = _product.Name,
            Text = $"تحقق من هذا المنتج: {_product.Name}",
            Uri = $"https://myeshop.com/product/{_product.Id}"
        });
    }
}
```

### Cart.razor

```razor
@page "/cart"
@using ACommerce.Templates.Customer.Pages
@using ACommerce.Client.Cart
@inject ICartClient CartClient
@inject IAppNavigationService Navigation

<CartPage Items="@_items"
          IsLoading="@_isLoading"
          Subtotal="@_subtotal"
          Discount="@_discount"
          ShippingCost="@_shippingCost"
          Tax="@_tax"
          Total="@_total"
          FreeShippingThreshold="200"
          AppliedCoupon="@_appliedCoupon"
          OnQuantityChange="HandleQuantityChange"
          OnRemoveItem="HandleRemoveItem"
          OnApplyCoupon="HandleApplyCoupon"
          OnRemoveCoupon="HandleRemoveCoupon"
          OnCheckout="HandleCheckout"
          OnStartShopping="HandleStartShopping" />

@code {
    private List<CartItemModel>? _items;
    private bool _isLoading = true;
    private decimal _subtotal;
    private decimal _discount;
    private decimal _shippingCost = 25;
    private decimal _tax;
    private decimal _total;
    private CouponModel? _appliedCoupon;

    protected override async Task OnInitializedAsync()
    {
        await LoadCartAsync();
    }

    private async Task LoadCartAsync()
    {
        _isLoading = true;

        try
        {
            var cart = await CartClient.GetCartAsync();

            _items = cart.Items.Select(i => new CartItemModel
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Name = i.ProductName,
                ImageUrl = i.ImageUrl,
                Price = i.UnitPrice,
                OldPrice = i.OriginalPrice,
                Quantity = i.Quantity,
                Attributes = i.Attributes
            }).ToList();

            _subtotal = cart.Subtotal;
            _discount = cart.DiscountAmount;
            _tax = cart.TaxAmount;
            _total = cart.Total;

            if (cart.AppliedCoupon != null)
            {
                _appliedCoupon = new CouponModel
                {
                    Code = cart.AppliedCoupon.Code,
                    DiscountAmount = cart.AppliedCoupon.DiscountAmount,
                    DiscountPercent = cart.AppliedCoupon.DiscountPercent
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cart: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleQuantityChange((Guid ItemId, int Quantity) args)
    {
        try
        {
            await CartClient.UpdateItemQuantityAsync(args.ItemId, args.Quantity);
            await LoadCartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating quantity: {ex.Message}");
        }
    }

    private async Task HandleRemoveItem(Guid itemId)
    {
        try
        {
            await CartClient.RemoveItemAsync(itemId);
            await LoadCartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing item: {ex.Message}");
        }
    }

    private async Task HandleApplyCoupon(string code)
    {
        try
        {
            await CartClient.ApplyCouponAsync(code);
            await LoadCartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying coupon: {ex.Message}");
        }
    }

    private async Task HandleRemoveCoupon()
    {
        try
        {
            await CartClient.RemoveCouponAsync();
            await LoadCartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing coupon: {ex.Message}");
        }
    }

    private void HandleCheckout()
    {
        Navigation.NavigateTo("/checkout");
    }

    private void HandleStartShopping()
    {
        Navigation.NavigateTo("/");
    }
}
```

---

## الخطوة 6: wwwroot/index.html | Step 6: index.html

```html
<!DOCTYPE html>
<html lang="ar" dir="rtl">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <title>MyEShop</title>
    <base href="/" />

    <!-- Bootstrap Icons -->
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.0/font/bootstrap-icons.css" />

    <!-- ACommerce Template CSS -->
    <link rel="stylesheet" href="_content/ACommerce.Templates.Customer/css/acommerce.css" />

    <!-- App CSS -->
    <link rel="stylesheet" href="css/app.css" />
</head>

<body>
    <div class="status-bar-safe-area"></div>

    <div id="app">
        <div class="ac-loading-screen">
            <div class="ac-spinner"></div>
            <p>جاري التحميل...</p>
        </div>
    </div>

    <div id="blazor-error-ui">
        حدث خطأ غير متوقع. يرجى تحديث الصفحة.
        <a href="" class="reload">إعادة التحميل</a>
    </div>

    <script src="_framework/blazor.webview.js" autostart="false"></script>
    <script>
        Blazor.start({
            reconnectionOptions: {
                maxRetries: 3,
                retryIntervalMilliseconds: 2000
            }
        });
    </script>
</body>

</html>
```

---

## الخطوة 7: CSS مخصص | Step 7: Custom CSS

### wwwroot/css/app.css

```css
/* Status Bar Safe Area */
.status-bar-safe-area {
    display: block;
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    height: env(safe-area-inset-top);
    background: var(--ac-bg-primary);
    z-index: 9999;
}

/* Loading Screen */
.ac-loading-screen {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    height: 100vh;
    background: var(--ac-bg-primary);
}

.ac-spinner {
    width: 50px;
    height: 50px;
    border: 3px solid var(--ac-border);
    border-top-color: var(--ac-primary);
    border-radius: 50%;
    animation: spin 1s linear infinite;
}

@keyframes spin {
    to { transform: rotate(360deg); }
}

/* Safe Area Padding */
.page-content {
    padding-top: calc(env(safe-area-inset-top) + 16px);
    padding-bottom: calc(env(safe-area-inset-bottom) + 80px);
    padding-left: env(safe-area-inset-left);
    padding-right: env(safe-area-inset-right);
}

/* Bottom Navigation Safe Area */
.ac-bottom-nav {
    padding-bottom: env(safe-area-inset-bottom);
}

/* Blazor Error UI */
#blazor-error-ui {
    background: #ef4444;
    color: white;
    padding: 16px;
    text-align: center;
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    display: none;
    z-index: 10000;
}

#blazor-error-ui .reload {
    color: white;
    text-decoration: underline;
    margin-left: 8px;
}

/* Platform-specific styles */
@supports (-webkit-touch-callout: none) {
    /* iOS specific */
    .page-wrapper {
        min-height: -webkit-fill-available;
    }
}
```

---

## الخطوة 8: إعدادات المنصات | Step 8: Platform Settings

### Android - AndroidManifest.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application
        android:allowBackup="true"
        android:icon="@mipmap/appicon"
        android:roundIcon="@mipmap/appicon_round"
        android:supportsRtl="true"
        android:usesCleartextTraffic="true">
    </application>

    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.CAMERA" />
    <uses-permission android:name="android.permission.USE_BIOMETRIC" />

    <!-- Deep Links -->
    <queries>
        <intent>
            <action android:name="android.intent.action.VIEW" />
            <data android:scheme="https" />
        </intent>
    </queries>
</manifest>
```

### iOS - Info.plist

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>LSRequiresIPhoneOS</key>
    <true/>
    <key>UIDeviceFamily</key>
    <array>
        <integer>1</integer>
        <integer>2</integer>
    </array>
    <key>UILaunchStoryboardName</key>
    <string>LaunchScreen</string>
    <key>UIRequiredDeviceCapabilities</key>
    <array>
        <string>armv7</string>
    </array>
    <key>UISupportedInterfaceOrientations</key>
    <array>
        <string>UIInterfaceOrientationPortrait</string>
    </array>
    <key>NSCameraUsageDescription</key>
    <string>نحتاج الوصول للكاميرا لمسح الباركود</string>
    <key>NSFaceIDUsageDescription</key>
    <string>استخدام Face ID لتسجيل الدخول السريع</string>
</dict>
</plist>
```

---

## الخطوة 9: تشغيل التطبيق | Step 9: Run Application

```bash
# Android
dotnet build -t:Run -f net9.0-android

# iOS (يتطلب Mac)
dotnet build -t:Run -f net9.0-ios

# Windows
dotnet build -t:Run -f net9.0-windows10.0.19041.0

# macOS
dotnet build -t:Run -f net9.0-maccatalyst
```

---

## أفضل الممارسات | Best Practices

### 1. التخزين الآمن للتوكنات
استخدم `SecureStorage` لتخزين التوكنات بدلاً من `Preferences`.

### 2. معالجة حالة الشبكة
```csharp
var networkAccess = Connectivity.Current.NetworkAccess;
if (networkAccess != NetworkAccess.Internet)
{
    // Show offline message
}
```

### 3. دعم الوضع الداكن
```csharp
Application.Current.RequestedThemeChanged += (s, e) =>
{
    // Update theme
};
```

---

## المراجع | References

- [.NET MAUI Documentation](https://docs.microsoft.com/en-us/dotnet/maui/)
- [Blazor Documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [MAUI Blazor Hybrid](https://docs.microsoft.com/en-us/aspnet/core/blazor/hybrid/)
