using System.Text;
using System.Text.Json.Serialization;
using DaycareApp.Api;
using DaycareApp.Api.Data;
using DaycareApp.Api.Domain;
using DaycareApp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Default")));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<Clock>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
        RoleClaimType = "role",
        NameClaimType = "sub"
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy("Staff", p => p.RequireRole("Admin", "Staff")));
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (cfg.GetValue<bool>("App:SeedDemoData") && !await db.Daycares.AnyAsync())
    {
        var h = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var dc = new Daycare { Name = "Sunny Days Daycare" };
        User Make(string name, string email, Role role, string pw) { var u = new User { DaycareId = dc.Id, Name = name, Email = email, Role = role }; u.PasswordHash = h.HashPassword(u, pw); return u; }
        var admin = Make("Admin", "admin@daycare.test", Role.Admin, "Admin123!");
        var parent = Make("Sam Parent", "parent@daycare.test", Role.Parent, "Parent123!");
        db.AddRange(dc, admin, parent);
        db.Children.Add(new Child { DaycareId = dc.Id, ParentId = parent.Id, Name = "Mia", DateOfBirth = new DateOnly(2022, 5, 14) });
        db.Announcements.Add(new Announcement { DaycareId = dc.Id, Title = "Welcome", Body = "Daily reports will appear here.", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapApi();
app.MapGet("/health", () => Results.Ok());
app.Run();
