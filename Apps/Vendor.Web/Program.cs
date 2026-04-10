using Vendor.Web.Components;
using Vendor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<UiPreferences>();

// ── HTTP Client → Order.Api (auth, offers, messages, notifications) ──
var orderApiBase = builder.Configuration["OrderApi:BaseUrl"] ?? "http://localhost:5101";
builder.Services.AddHttpClient("order-api", c =>
{
    c.BaseAddress = new Uri(orderApiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<OrderApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var auth = sp.GetRequiredService<AuthStateService>();
    return new OrderApiClient(factory.CreateClient("order-api"), auth);
});

// ── HTTP Client → Vendor.Api (orders, settings, schedule) ────────────
var vendorApiBase = builder.Configuration["VendorApi:BaseUrl"] ?? "http://localhost:5201";
builder.Services.AddHttpClient("vendor-api", c =>
{
    c.BaseAddress = new Uri(vendorApiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<VendorApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var auth = sp.GetRequiredService<AuthStateService>();
    return new VendorApiClient(factory.CreateClient("vendor-api"), auth);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
