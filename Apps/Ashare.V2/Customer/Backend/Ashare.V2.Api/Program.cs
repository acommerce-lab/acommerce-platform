var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// CORS — السماح للواجهة بالاتصال محلياً
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.MapControllers();

app.Run();
