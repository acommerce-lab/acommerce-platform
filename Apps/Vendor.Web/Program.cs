using Vendor.Web.Components;
using Vendor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<UiPreferences>();

var apiBase = builder.Configuration["OrderApi:BaseUrl"] ?? "http://localhost:5101";
builder.Services.AddHttpClient("order", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<VendorApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var auth = sp.GetRequiredService<AuthStateService>();
    return new VendorApiClient(factory.CreateClient("order"), auth);
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
