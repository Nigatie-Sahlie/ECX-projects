using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DaycareApi;
using DaycareApi.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DaycareApi.Tests.Features;

[Collection(IntegrationCollection.Name)]
public sealed class PasswordResetTests(ApiFactory factory)
{
    [Fact]
    public async Task Forgot_password_sends_a_reset_link_and_new_password_can_sign_in()
    {
        var (email, oldPassword) = await CreateUser();
        factory.EmailSender.Clear();
        var client = factory.CreateClient();

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Equal(email, factory.EmailSender.Recipient);
        Assert.NotNull(factory.EmailSender.ResetUrl);

        var token = ExtractToken(factory.EmailSender.ResetUrl!);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token,
            newPassword = "NewPassword123!"
        });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = oldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_send_email_for_unknown_account()
    {
        factory.EmailSender.Clear();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = $"missing-{Guid.NewGuid():N}@test.local"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(factory.EmailSender.Recipient);
        Assert.Null(factory.EmailSender.ResetUrl);
    }

    [Fact]
    public async Task Reset_password_token_is_single_use()
    {
        var (email, _) = await CreateUser();
        factory.EmailSender.Clear();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        var token = ExtractToken(factory.EmailSender.ResetUrl!);
        var request = new { token, newPassword = "NewPassword123!" };

        var firstReset = await client.PostAsJsonAsync("/api/auth/reset-password", request);
        var secondReset = await client.PostAsJsonAsync("/api/auth/reset-password", request);

        Assert.Equal(HttpStatusCode.NoContent, firstReset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondReset.StatusCode);
    }

    private static string? ExtractToken(string resetUrl)
    {
        var query = new Uri(resetUrl).Query.TrimStart('?');
        var token = query.Split('&')
            .FirstOrDefault(part => part.StartsWith("token=", StringComparison.Ordinal))
            ?.Split('=', 2)[1];
        return token is null ? null : Uri.UnescapeDataString(token);
    }

    private async Task<(string Email, string Password)> CreateUser()
    {
        var email = $"reset-{Guid.NewGuid():N}@test.local";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.SuperAdmin,
            FullName = "Password Reset Test User"
        });
        await db.SaveChangesAsync();
        return (email, password);
    }
}
