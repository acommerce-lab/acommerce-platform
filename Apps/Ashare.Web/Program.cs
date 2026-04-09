using Ashare.Web.Components;
using Ashare.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─────────────────────────────────────────────────────────
// ACommerce client infrastructure
// ─────────────────────────────────────────────────────────

// AuthState scoped per circuit
builder.Services.AddScoped<AuthStateService>();

// HttpClient + AshareApiClient (Scoped per circuit)
var apiBase = builder.Configuration["AshareApi:BaseUrl"] ?? "http://localhost:5500";
builder.Services.AddHttpClient("ashare", client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<AshareApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new AshareApiClient(factory.CreateClient("ashare"), sp.GetRequiredService<AuthStateService>());
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
