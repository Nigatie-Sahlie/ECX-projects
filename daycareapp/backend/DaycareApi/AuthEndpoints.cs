using Microsoft.EntityFrameworkCore;

namespace DaycareApi;

public static class AuthEndpoints
{
    public static void MapAuthApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/auth");
        var protectedApi = api.MapGroup("").RequireAuthorization();

        api.MapPost("/login", async (LoginReq r, AppDb db, TokenService ts) =>
        {
            var email = (r.Email ?? "").Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(r.Password ?? "", user.PasswordHash))
                return Results.Unauthorized();
            if (user.CenterId is Guid centerId && !await db.Centers.AnyAsync(c => c.Id == centerId && c.IsActive))
                return Results.Json(new { error = "This daycare account is disabled" }, statusCode: 403);
            return Results.Ok(new { token = ts.Create(user), user = new { user.Id, user.FullName, user.Role, user.CenterId } });
        });

        protectedApi.MapPost("/change-password", async (ChangePasswordReq r, AppDb db, HttpContext context) =>
        {
            var user = await db.Users.FindAsync(context.User.Uid());
            if (user is null || string.IsNullOrWhiteSpace(r.CurrentPassword) ||
                !BCrypt.Net.BCrypt.Verify(r.CurrentPassword, user.PasswordHash))
                return Results.BadRequest(new { error = "Current password is incorrect" });
            if (string.IsNullOrWhiteSpace(r.NewPassword) || r.NewPassword.Length < 6)
                return Results.BadRequest(new { error = "Password must be at least 6 characters" });
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.NewPassword);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapPost("/forgot-password", async (
            ForgotPasswordReq request,
            AppDb db,
            IEmailSender emailSender,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var email = (request.Email ?? "").Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
            if (user is not null && user.IsActive)
            {
                var rawToken = ResetTokenService.Create();
                db.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = ResetTokenService.Hash(rawToken),
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                });
                await db.SaveChangesAsync(cancellationToken);

                var baseUrl = configuration["Frontend:ResetPasswordUrl"] ?? "http://localhost:5173/reset-password";
                var resetUrl = $"{baseUrl}?token={Uri.EscapeDataString(rawToken)}";
                await emailSender.SendPasswordResetAsync(user.Email, resetUrl, cancellationToken);
            }

            return Results.Ok(new { message = "If an active account exists for that email, a reset link has been sent." });
        });

        api.MapPost("/reset-password", async (ResetPasswordReq request, AppDb db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword) ||
                request.NewPassword.Length < 6)
                return Results.BadRequest(new { error = "A valid token and password of at least 6 characters are required" });

            var hash = ResetTokenService.Hash(request.Token);
            var reset = await db.PasswordResetTokens.FirstOrDefaultAsync(x =>
                x.TokenHash == hash && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
            if (reset is null) return Results.BadRequest(new { error = "This reset link is invalid or expired" });

            var user = await db.Users.FindAsync([reset.UserId], cancellationToken);
            if (user is null || !user.IsActive) return Results.BadRequest(new { error = "This reset link is invalid or expired" });
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            reset.UsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }
}
