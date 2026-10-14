using DaycareApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace DaycareApi.Tests.Fixtures;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeEmailSender EmailSender { get; } = new();

    public static string TestConnection =>
        Environment.GetEnvironmentVariable("DAYCARE_TEST_CONNECTION")
        ?? throw new InvalidOperationException(
            "Set DAYCARE_TEST_CONNECTION to a dedicated PostgreSQL test database.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = TestConnection;
        var database = new NpgsqlConnectionStringBuilder(connection).Database ?? "";
        if (string.Equals(database, "daycareapp", StringComparison.OrdinalIgnoreCase) ||
            !database.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Integration tests require a dedicated database whose name contains 'test'; " +
                "the development database is never allowed.");

        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDb>>();
            services.AddDbContext<AppDb>(options => options.UseNpgsql(connection));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // The application seeds during startup. Ensure only the dedicated test
            // database has a schema; never drop or clear an existing database.
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();
        });
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    public string? Recipient { get; private set; }
    public string? ResetUrl { get; private set; }

    public Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken)
    {
        Recipient = recipient;
        ResetUrl = resetUrl;
        return Task.CompletedTask;
    }

    public void Clear()
    {
        Recipient = null;
        ResetUrl = null;
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Daycare API integration";
}
