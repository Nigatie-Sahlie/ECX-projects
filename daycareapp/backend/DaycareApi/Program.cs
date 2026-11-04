using System.Text;
using DaycareApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(cfg.GetConnectionString("Default")));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSignalR();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddHostedService<ExpiredEventCleanupService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                context.Token = accessToken;
            return Task.CompletedTask;
        },
    };
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = cfg["Jwt:Issuer"],
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(cfg["Cors:Origin"]!).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (cfg.GetValue<bool>("Bootstrap:Enabled"))
{
    if (!app.Environment.IsProduction())
        throw new InvalidOperationException("First-admin bootstrap is allowed only in Production.");

    var email = cfg["Bootstrap:Email"]?.Trim().ToLowerInvariant();
    var password = cfg["Bootstrap:Password"];
    var fullName = cfg["Bootstrap:FullName"]?.Trim();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName) ||
        string.IsNullOrWhiteSpace(password) || password.Length < 12)
        throw new InvalidOperationException("Bootstrap email, full name, and a password of at least 12 characters are required.");

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (db.Users.Any(u => u.Role == Roles.SuperAdmin))
        throw new InvalidOperationException("A super-admin already exists; bootstrap cannot be repeated.");
    if (db.Users.Any(u => u.Email == email))
        throw new InvalidOperationException("That email is already assigned to an account.");

    db.Users.Add(new User
    {
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = Roles.SuperAdmin,
        FullName = fullName,
        IsActive = true,
    });
    db.SaveChanges();
    Console.WriteLine($"Created initial super-admin account for {email}.");
    return;
}

app.UseMiddleware<ApiExceptionMiddleware>();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (app.Environment.IsDevelopment())
        db.Database.Migrate();

    // Demo accounts use public, fixed passwords. Never create or reset them outside development.
    if (app.Environment.IsDevelopment())
        Seed.Run(db);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
app.MapAuthApi();
app.MapApi();
app.Run();

public partial class Program { }
