using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;

namespace DaycareApi;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken);
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
