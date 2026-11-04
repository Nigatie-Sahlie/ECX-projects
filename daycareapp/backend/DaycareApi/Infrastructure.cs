using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DaycareApi;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken);
    Task SendNotificationAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("Smtp");
        if (!section.GetValue<bool>("Enabled"))
        {
            logger.LogWarning("Password reset email not sent because SMTP is disabled.");
            return;
        }

        using var message = new MailMessage(section["From"] ?? throw new InvalidOperationException("Smtp:From is required"), recipient)
        {
            Subject = "Reset your KidSyncApp password",
            Body = $"Use this link to reset your password. It expires in one hour:\n\n{resetUrl}",
            IsBodyHtml = false,
        };
        using var client = new SmtpClient(section["Host"], section.GetValue<int>("Port", 587))
        {
            EnableSsl = section.GetValue("UseSsl", true),
            Credentials = new NetworkCredential(section["Username"], section["Password"]),
        };
        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendNotificationAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("Smtp");
        if (!section.GetValue<bool>("Enabled"))
        {
            logger.LogWarning("Notification email not sent because SMTP is disabled.");
            return;
        }

        using var message = new MailMessage(section["From"] ?? throw new InvalidOperationException("Smtp:From is required"), recipient)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        using var client = new SmtpClient(section["Host"], section.GetValue<int>("Port", 587))
        {
            EnableSsl = section.GetValue("UseSsl", true),
            Credentials = new NetworkCredential(section["Username"], section["Password"]),
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}

public sealed class NotificationService(
    AppDb db,
    IHubContext<NotificationHub> hub,
    IEmailSender emailSender,
    ILogger<NotificationService> logger)
{
    public async Task NotifyAsync(
        Guid centerId, Guid userId, string type, string title, string body, string? link,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification
            {
                CenterId = centerId, UserId = userId, Type = type, Title = title, Body = body, Link = link,
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);

            var payload = new
            {
                notification.Id, notification.Type, notification.Title, notification.Body,
                notification.Link, notification.CreatedAt, notification.ReadAt,
            };
            await hub.Clients.User(userId.ToString()).SendAsync("notification", payload, cancellationToken);

            var recipient = await db.Users.Where(u => u.Id == userId)
                .Select(u => u.Email).SingleAsync(cancellationToken);
            await emailSender.SendNotificationAsync(recipient, title, body, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Notification delivery failed for user {UserId}; the primary operation will continue.", userId);
        }
    }
}

public sealed class NotificationHub : Hub
{
}

public sealed class ExpiredEventCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredEventCleanupService> logger) : BackgroundService
{
    static readonly TimeZoneInfo LocalTimeZone = FindLocalTimeZone();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LocalTimeZone);
                var events = await db.Events.IgnoreQueryFilters().ToListAsync(stoppingToken);
                var expired = events.Where(e => HasExpired(e, now)).ToList();
                if (expired.Count > 0)
                {
                    db.Events.RemoveRange(expired);
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Removed {Count} expired events.", expired.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Expired event cleanup failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    static bool HasExpired(EventItem item, DateTime now)
    {
        if (item.Recurrence != "one_time" && item.EndDate is null) return false;
        var expirationDate = item.EndDate ?? item.StartDate;
        if (now.Date > expirationDate.ToDateTime(TimeOnly.MinValue).Date) return true;
        if (now.Date < expirationDate.ToDateTime(TimeOnly.MinValue).Date) return false;

        var expirationTime = item.EndTime ?? item.StartTime ?? TimeOnly.MaxValue;
        return now.TimeOfDay > expirationTime.ToTimeSpan();
    }

    static TimeZoneInfo FindLocalTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Addis_Ababa"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("EAT", TimeSpan.FromHours(3), "East Africa Time", "East Africa Time"); }
    }
}

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API exception for {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "An unexpected error occurred." }));
        }
    }
}

public static class ResetTokenService
{
    public static string Create()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }
}
